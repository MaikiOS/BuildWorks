[CmdletBinding()]
param([switch]$CheckLaunchProfileOnly)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profile = Join-Path (Split-Path (Split-Path $root)) 'TerrainRamp-1.0-Test'
$qaPath = Join-Path $root 'artifacts/test-stand-bin/BuildWorks.dll'
$normalPath = Join-Path $root 'artifacts/bin/BuildWorks.dll'
if ($CheckLaunchProfileOnly) {
    $bootstrap = @(Get-ChildItem -LiteralPath (Join-Path $profile 'BepInEx/plugins') -Recurse -File -Filter 'TerrainRamp.TestBootstrap.dll')
    if ($bootstrap.Count) {
        if ($bootstrap.Count -ne 1 -or (Get-FileHash -LiteralPath $bootstrap[0].FullName).Hash -ne '67EDB9A9BCA33974C879BB9554AC51876B62996BFCA7729C17E852130DB35E41') {
            throw 'Unknown or duplicate TerrainRamp bootstrap: save-writing behavior requires review before QA launch.'
        }
        $configuration = Join-Path $profile 'BepInEx/config/com.ostrix.terrainramp.testbootstrap.cfg'
        if (-not (Test-Path -LiteralPath $configuration -PathType Leaf) -or (Get-Item -LiteralPath $configuration).Length -gt 128KB) {
            throw 'Missing or invalid TerrainRamp bootstrap config; automatic save creation defaults to enabled.'
        }
        $inBootstrap = $false
        $values = @()
        foreach ($line in Get-Content -LiteralPath $configuration) {
            if ($line -match '^\s*\[([^\]]+)\]\s*$') { $inBootstrap = $Matches[1] -eq 'Bootstrap' }
            elseif ($inBootstrap -and $line -match '^\s*CreateMissingSaves\s*=\s*(.*?)\s*$') { $values += $Matches[1] }
        }
        if ($values.Count -ne 1 -or $values[0] -ine 'false') {
            throw "QA launch blocked: $configuration [Bootstrap] CreateMissingSaves must be false before starting Valheim. No profile changes were made."
        }
    }
    'PASS preflight: known TerrainRamp automatic save writer is absent or explicitly disabled. This does not launch the game or prove every other plugin.'
    return
}
Add-Type -Path (Join-Path $profile 'BepInEx/core/Mono.Cecil.dll')
function Require([bool]$value, [string]$message) { if (-not $value) { throw $message } }
$qa = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($qaPath)
$normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($normalPath)
$typeName = 'OstrixMods.BuildWorks.BuildWorksTestIsolation'
$type = $qa.MainModule.GetType($typeName)
Require ($null -ne $type -and $null -eq $normal.MainModule.GetType($typeName)) 'QA isolation leaked into normal assembly or is missing from the QA build.'
$activate = $type.Methods | Where-Object Name -eq 'Activate'
$calls = @($activate.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') } | ForEach-Object { [string]$_.Operand })
$cold = $activate.Body.Instructions | Where-Object { [string]$_.Operand -match '::RequireColdStorage' } | Select-Object -First 1
$owned = $activate.Body.Instructions | Where-Object { [string]$_.Operand -match '::EnsureOwnedRoot' } | Select-Object -First 1
Require (($calls -join "`n") -match 'RequireColdStorage' -and ($calls -join "`n") -match 'Utils::SetSaveDataPath' -and
    $cold.Offset -lt $owned.Offset) 'Native root activation lost the cold-storage-before-write guard.'
$strings = @($activate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('CloudStorageSupported','CloudStorageSupportedAndEnabled','BeforeStorage','BeforeSetRoot','BeforeResetRoot')) {
    Require ($strings -contains $name) "QA isolation omits native guard: $name"
}
$managed = 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed'
foreach ($entry in @(@('assembly_valheim.dll','25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE'),
    @('assembly_utils.dll','6BE2AFEB97B5B26FD3176E1B8584C07E57AD48DDCD6E8E5E79E32BEE943827C1'))) {
    Require ($strings -contains $entry[1] -and (Get-FileHash -LiteralPath (Join-Path $managed $entry[0])).Hash -eq $entry[1]) 'QA native save contract fingerprint changed.'
}
$allCalls = ($type.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Require ($allCalls -notmatch 'ResetSaveDataPath\(\)|SaveSystem::ForceRefreshCache|Directory::Delete|File::Delete|File::Move') 'QA isolation gained cache clearing, root reset or destructive filesystem operations.'
$qa.Dispose(); $normal.Dispose()

# Only BCL path/ownership functions execute here. No native API/root setter/save is invoked.
[Reflection.Assembly]::LoadFrom((Join-Path $profile 'BepInEx/core/0Harmony.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $root 'artifacts/test-stand-bin/BuildWorks.Geometry.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom($qaPath)
$runtimeType = $assembly.GetType($typeName,$true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
function Invoke-PathCheck([string]$name, [string[]]$arguments) {
    $runtimeType.GetMethod($name,$flags).Invoke($null,$arguments)
}
function Refuses([scriptblock]$action, [string]$message) {
    $refused = $false
    try { & $action | Out-Null } catch {
        if ($_.Exception.GetBaseException() -isnot [IO.InvalidDataException]) { throw }
        $refused = $true
    }
    Require $refused $message
}
$output = Join-Path $root ('artifacts/qa-isolation-checks/' + [guid]::NewGuid().ToString('N'))
$config = Join-Path $output 'TerrainRamp-1.0-Test/BepInEx/config'
[IO.Directory]::CreateDirectory($config) | Out-Null
$token = [guid]::NewGuid().ToString('N')
$isolated = Invoke-PathCheck 'ResolveRoot' @($config,$token)
Require ($isolated -eq [IO.Path]::GetFullPath((Join-Path $config "BuildWorks/qa-saves/$token"))) 'QA root is not fixed under its own config.'
Refuses { Invoke-PathCheck 'ResolveRoot' @($config,'../escape') } 'Traversal token accepted.'
Refuses { Invoke-PathCheck 'ResolveRoot' @($config,[guid]::Empty.ToString('N')) } 'Empty GUID accepted.'
Refuses { Invoke-PathCheck 'ResolveRoot' @((Join-Path $output 'Default/BepInEx/config'),$token) } 'Default profile accepted.'
Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,$token)
$owner = Join-Path $isolated 'buildworks-qa-root.txt'
Require ([IO.File]::ReadAllText($owner) -eq $token) 'Root owner token was not written exactly.'
$sentinel = Join-Path $isolated 'untouched.txt'
[IO.File]::WriteAllText($sentinel,'sentinel')
Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,$token)
Require ([IO.File]::ReadAllText($sentinel) -eq 'sentinel') 'Reopening owned root changed existing data.'
Refuses { Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,[guid]::NewGuid().ToString('N')) } 'Foreign root ownership accepted.'
$unknown = Join-Path $output 'unknown'
[IO.Directory]::CreateDirectory($unknown) | Out-Null
[IO.File]::WriteAllText((Join-Path $unknown 'untouched.txt'),'sentinel')
Refuses { Invoke-PathCheck 'EnsureOwnedRoot' @($unknown,$token) } 'Unowned nonempty root adopted.'
Require (-not [IO.File]::Exists((Join-Path $unknown 'buildworks-qa-root.txt'))) 'Refusal wrote an owner marker.'
$runtimeType.GetField('root',$flags).SetValue($null,$isolated)
Invoke-PathCheck 'ValidateSavePath' @((Join-Path $isolated 'characters_local/BW_QA_12345678.fch'))
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path ($isolated+'-sibling') 'escape.fch')) } 'Sibling path prefix escaped isolation.'
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path $isolated '../escape.fch')) } 'Traversal save destination escaped isolation.'
$link = Join-Path $isolated 'linked'
New-Item -ItemType Junction -Path $link -Value $unknown | Out-Null
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path $link 'escape.fch')) } 'Junction escaped isolated save destination.'
@{status='passed_paths_and_compiled_contract_only';qaSha256=(Get-FileHash $qaPath).Hash;normalSha256=(Get-FileHash $normalPath).Hash;gameHostTested=$false;saveFilesModified=$false;fixtures=$output} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification.json')
'PASS: QA-only assembly, native guard wiring, root/ownership/traversal/junction refusal; no native saves or game execution.'
