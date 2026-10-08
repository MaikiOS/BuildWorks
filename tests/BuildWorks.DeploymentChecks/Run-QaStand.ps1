[CmdletBinding()]
param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$source = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$output = Join-Path $source ('artifacts/qa-deployment-checks/' + [guid]::NewGuid().ToString('N'))
$replica = Join-Path $output 'profiles/Default/BuildWorks'
$target = Join-Path $output 'profiles/TerrainRamp-1.0-Test'
function Copy-Replica([string]$from, [string]$to) {
    [IO.Directory]::CreateDirectory((Split-Path $to)) | Out-Null
    Copy-Item -LiteralPath $from -Destination $to
}
foreach ($relative in @('scripts/Deploy-TestBuild.ps1', 'scripts/Test-QASaveIsolation.ps1',
    'artifacts/bin/BuildWorks.dll', 'artifacts/bin/BuildWorks.Geometry.dll',
    'artifacts/test-stand-bin/BuildWorks.dll', 'artifacts/test-stand-bin/BuildWorks.Geometry.dll')) {
    Copy-Replica (Join-Path $source $relative) (Join-Path $replica $relative)
}
foreach ($name in @('01-walls.blueprint','02-sign.blueprint','03-opaque.blueprint',
    '04-snap.blueprint','05-missing.blueprint','06-lamp.blueprint')) {
    $relative = 'tests/fixtures/player-exchange/' + $name
    Copy-Replica (Join-Path $source $relative) (Join-Path $replica $relative)
}
foreach ($name in @('hs_ashlands_madzobuild_coffeetable.blueprint','hs_blackforest_hut.blueprint')) {
    $relative = 'artifacts/external-blueprints/BiomeBlueprints-1.0.3/smoke/' + $name
    Copy-Replica (Join-Path $source $relative) (Join-Path $replica $relative)
}
$liveProfile = Join-Path (Split-Path (Split-Path $source)) 'TerrainRamp-1.0-Test'
Copy-Replica (Join-Path $liveProfile 'BepInEx/core/Mono.Cecil.dll') (Join-Path $target 'BepInEx/core/Mono.Cecil.dll')
[IO.Directory]::CreateDirectory((Join-Path $target 'BepInEx/plugins')) | Out-Null
$deploy = Join-Path $replica 'scripts/Deploy-TestBuild.ps1'
$token = '82630d2d72bc43e286b3c64cbb4a1818'
$marker = Join-Path $target 'BepInEx/config/BuildWorks/qa-isolation.token'
$installed = Join-Path $target 'BepInEx/plugins/Ostrix-BuildWorks/BuildWorks.dll'
function Require([bool]$condition,[string]$message) { if (-not $condition) { throw $message } }
function Snapshot {
    (Get-ChildItem -LiteralPath $output -Recurse -Force | Sort-Object FullName | ForEach-Object {
        $_.FullName.Substring($output.Length) + '|' + $(if ($_.PSIsContainer) { 'directory' } else { (Get-FileHash -LiteralPath $_.FullName).Hash })
    }) -join "`n"
}
function Refuses([scriptblock]$action,[string]$expected) {
    $before = Snapshot
    $rejected = $false
    try { & $action | Out-Null } catch {
        if ($_.Exception.Message -notmatch $expected) { throw }
        $rejected = $true
    }
    Require $rejected "Deployment accepted forbidden condition: $expected"
    Require ((Snapshot) -ceq $before) "Refused deployment changed files: $expected"
}
$cases = 0
$before = Snapshot
& $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token -WhatIf
Require ((Snapshot) -ceq $before) 'QA WhatIf changed replica files or directories.'
$cases++
foreach ($invalid in @('../escape',[guid]::Empty.ToString('N'),'not-a-guid')) {
    Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $invalid -WhatIf } 'nonzero GUID in N format'
    $cases++
}
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token -ArmSkyCleanup -WhatIf } 'cannot arm cleanup'
$cases++
$external = Join-Path $replica 'artifacts/external-blueprints/BiomeBlueprints-1.0.3/smoke/hs_blackforest_hut.blueprint'
$original = [IO.File]::ReadAllBytes($external)
[IO.File]::AppendAllText($external,'changed')
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token } 'QA source fixture hash changed'
$cases++
[IO.File]::WriteAllBytes($external,$original)
# Junction named like a DLL also redirects Copy-Item outside the target profile.
[IO.Directory]::CreateDirectory((Split-Path $installed)) | Out-Null
$outside = Join-Path $output 'outside-profile'
[IO.Directory]::CreateDirectory($outside) | Out-Null
$sentinel = Join-Path $outside 'untouched.txt'
[IO.File]::WriteAllText($sentinel,'untouched')
New-Item -ItemType Junction -Path $installed -Target $outside | Out-Null
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token } 'junction or symbolic link'
Require ([IO.File]::ReadAllText($sentinel) -ceq 'untouched' -and @(Get-ChildItem -LiteralPath $outside -File).Count -eq 1) 'DLL link refusal changed the external target.'
Remove-Item -LiteralPath $installed -Force
$cases++
& $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token
Require ([IO.File]::ReadAllText($marker) -ceq $token) 'Explicit QA identity marker not written exactly.'
Require ((Get-FileHash $installed).Hash -ceq (Get-FileHash (Join-Path $replica 'artifacts/test-stand-bin/BuildWorks.dll')).Hash) 'QA DLL was not selected.'
$fixtureTarget = Join-Path $target 'BepInEx/config/BuildWorks/test-fixtures'
Require (@(Get-ChildItem -LiteralPath $fixtureTarget -File).Count -eq 8) 'Expected six owned and two external local QA files.'
Require (-not (Test-Path -LiteralPath (Join-Path $target 'BepInEx/config/BuildWorks/qa-saves'))) 'Deployment created a game save root.'
$cases++
$before = Snapshot
& $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token
Require ((Snapshot) -ceq $before) 'Repeated deployment rewrote files or rearmed identity.'
$cases++
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test } 'requires explicit -QaStandToken'
$cases++
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken ([guid]::NewGuid().ToString('N')) } 'different QA stand token'
$cases++
$fixture = Join-Path $fixtureTarget '02-sign.blueprint'
[IO.File]::AppendAllText($fixture,'changed')
Refuses { & $deploy -TargetProfile TerrainRamp-1.0-Test -QaStandToken $token } 'different contents; refusing overwrite'
$cases++
@{status='passed_replica_only';cases=$cases;replica=$replica;liveProfileModified=$false;gameExecuted=$false;saveFilesModified=$false} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification.json')
"PASS: $cases QA deployment cases in a workspace replica only; live profile, game and saves unchanged."
