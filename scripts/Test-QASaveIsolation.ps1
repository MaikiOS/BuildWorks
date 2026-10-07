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
$loaded = $type.Methods | Where-Object Name -eq 'RequireLoadedSession'
$loadedCalls = @($loaded.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('AssertPinned','IsServer','IsDedicated','IsOpenServer','GetPeers','IsShuttingDown','IsDead','IsTeleporting',
    'GetInventory','NoCostCheat','GetGlobalKey','GetWorld','GetPlayerProfile','GetSaveFWLPath','GetPath','GetFilename',
    'GetPlayerID','ReadIdentityReceipt','RequireIdentityMatch')) {
    Require ([bool]($loadedCalls -match "::$name\(")) "Loaded QA session omits native identity/survival guard: $name"
}
$loadedFields = @($loaded.Body.Instructions | Where-Object { $_.OpCode.Name -in @('ldfld','ldsfld') } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('m_debugMode','m_worldLevel','m_fileSource','m_worldName','m_uid')) {
    Require ([bool]($loadedFields -match "::$name$")) "Loaded QA session omits native identity/survival field: $name"
}
$pluginType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin')
$coroutine = $pluginType.NestedTypes | Where-Object Name -Like '<PrepareQaSaves>*'
Require ([bool](@($coroutine.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -match '::RequireLoadedSession\(')) 'QA loaded gate is not called by the actual plugin coroutine.'
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
# Execute the real receipt/matching helpers; native host methods above are compiled-contract proof only.
$name = 'BW_QA_' + $token.Substring(0,8)
[string[]]$receipt = @('BuildWorks-QA-1',$token,$name,'9223372036854775806',$name,'-9223372036854775807','started','complete')
function Invoke-IdentityCheck([string]$method, [object[]]$arguments) {
    $runtimeType.GetMethod($method,$flags).Invoke($null,$arguments)
}
$identityChecks = 0
[object[]]$identity = @($receipt,$token,$name,[long]9223372036854775806,$name,[long]::Parse('-9223372036854775807'),[long]::Parse('-9223372036854775807'))
Invoke-IdentityCheck 'RequireIdentityMatch' $identity
$identityChecks++
foreach ($index in 0..7) {
    [string[]]$badReceipt = $receipt.Clone()
    $badReceipt[$index] = 'invalid'
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($badReceipt,$token) } "Malformed receipt field $index accepted."
    $identityChecks++
}
foreach ($invalidId in @(@{index=3;value='0'},@{index=5;value='0'},@{index=3;value='9223372036854775808'},@{index=5;value='-9223372036854775809'})) {
    [string[]]$lines = $receipt.Clone(); $lines[$invalidId.index] = $invalidId.value
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($lines,$token) } 'Zero or overflowing native ID accepted.'
    $identityChecks++
}
foreach ($length in @(7,9)) {
    [string[]]$lines = if ($length -eq 7) { $receipt[0..6] } else { @($receipt) + @('extra') }
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($lines,$token) } 'Partial or extended receipt accepted.'
    $identityChecks++
}
Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($null,$token) } 'Null receipt accepted.'
$identityChecks++
foreach ($badToken in @([guid]::Empty.ToString('N'),[guid]::NewGuid().ToString('N'),'../escape')) {
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($receipt,$badToken) } 'Foreign or malformed identity token accepted.'
    $identityChecks++
}
foreach ($index in 2..6) {
    [object[]]$mismatch = $identity.Clone()
    $mismatch[$index] = if ($index -in @(2,4)) { 'other' } else { [long]17 }
    Refuses { Invoke-IdentityCheck 'RequireIdentityMatch' $mismatch } "Loaded identity mismatch $index accepted."
    $identityChecks++
}
[object[]]$noZdo = $identity.Clone(); $noZdo[6] = [long]0
Refuses { Invoke-IdentityCheck 'RequireIdentityMatch' $noZdo } 'Uninitialized local player ZDO ID accepted.'
$identityChecks++
@{status='passed_paths_receipts_and_compiled_contract_only';identityCases=$identityChecks;qaSha256=(Get-FileHash $qaPath).Hash;normalSha256=(Get-FileHash $normalPath).Hash;gameHostTested=$false;saveFilesModified=$false;fixtures=$output} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification.json')
"PASS: QA-only assembly, native loaded guard wiring, $identityChecks receipt/identity cases, root/ownership/traversal/junction refusal; no native saves or game execution."
