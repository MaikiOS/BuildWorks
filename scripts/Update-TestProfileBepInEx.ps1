[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [string] $TargetProfile = 'TerrainRamp-1.0-Test',
    [string] $PackageRoot = 'artifacts\host-1.0-compat\packages\denikson-BepInExPack_Valheim-5.4.2350'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profilesRoot = Split-Path -Parent (Split-Path -Parent $projectRoot)
$targetRoot = [IO.Path]::GetFullPath((Join-Path $profilesRoot $TargetProfile))
$packageRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot $PackageRoot))
$payloadRoot = Join-Path $packageRoot 'BepInExPack_Valheim'
$sourceCore = Join-Path $payloadRoot 'BepInEx\core'
$targetCore = Join-Path $targetRoot 'BepInEx\core'
$modsPath = Join-Path $targetRoot 'mods.yml'

if ($TargetProfile -ine 'TerrainRamp-1.0-Test' -or
    (Split-Path -Leaf $targetRoot) -ine 'TerrainRamp-1.0-Test' -or
    (Split-Path -Parent $targetRoot) -ine $profilesRoot) {
    throw 'BepInEx update is restricted to TerrainRamp-1.0-Test.'
}
$projectPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar,
    [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $packageRoot.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'PackageRoot must stay inside the BuildWorks project.'
}
foreach ($required in @($targetRoot, $packageRoot, $sourceCore, $targetCore, $modsPath)) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required path is missing: $required" }
}
foreach ($root in @($targetRoot, $packageRoot)) {
    if ((Get-Item -LiteralPath $root -Force).Attributes -band [IO.FileAttributes]::ReparsePoint) {
        throw "BepInEx update refuses a reparse-point root: $root"
    }
    $reparsePoint = Get-ChildItem -LiteralPath $root -Recurse -Force |
        Where-Object { $_.Attributes -band [IO.FileAttributes]::ReparsePoint } |
        Select-Object -First 1
    if ($reparsePoint) { throw "BepInEx update refuses reparse points: $($reparsePoint.FullName)" }
}
if (Get-Process -Name valheim, valheim_server -ErrorAction SilentlyContinue) {
    throw 'Valheim must be closed before the BepInEx update.'
}

$package = Get-Content -LiteralPath (Join-Path $packageRoot 'manifest.json') -Raw | ConvertFrom-Json
if ($package.name -ne 'BepInExPack_Valheim' -or $package.version_number -ne '5.4.2350') {
    throw 'Expected the verified BepInExPack_Valheim 5.4.2350 package.'
}

$manifest = Get-Content -LiteralPath $modsPath -Raw
$blocks = [regex]::Split($manifest, '(?m)(?=^- manifestVersion:)')
$matched = 0
for ($index = 0; $index -lt $blocks.Count; $index++) {
    if ($blocks[$index] -notmatch '(?m)^  name: denikson-BepInExPack_Valheim\s*$') { continue }
    $matched++
    $blocks[$index] = [regex]::Replace(
        $blocks[$index],
        '(?ms)(^  versionNumber:\s*\r?\n    major: )\d+(\s*\r?\n    minor: )\d+(\s*\r?\n    patch: )\d+',
        '${1}5${2}4${3}2350')
}
if ($matched -ne 1) { throw "Expected one BepInEx manifest record, found $matched." }
$updatedManifest = $blocks -join ''

$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$backupRoot = Join-Path $projectRoot "artifacts\host-1.0-compat\bepinex-backup-$stamp"
if (-not $PSCmdlet.ShouldProcess($targetCore,
        "Replace with BepInEx 5.4.23.5; update profile record to 5.4.2350; backup to $backupRoot")) {
    Write-Output "WHATIF backup: $backupRoot"
    return
}

New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
Copy-Item -LiteralPath $targetCore -Destination (Join-Path $backupRoot 'core') -Recurse -Force
Copy-Item -LiteralPath $modsPath -Destination (Join-Path $backupRoot 'mods.yml') -Force

try {
    Remove-Item -LiteralPath $targetCore -Recurse -Force
    Copy-Item -LiteralPath $sourceCore -Destination $targetCore -Recurse -Force
    [IO.File]::WriteAllText($modsPath, $updatedManifest, [Text.UTF8Encoding]::new($false))

    $expectedCore = Get-ChildItem -LiteralPath $sourceCore -File | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    }
    $installedCore = Get-ChildItem -LiteralPath $targetCore -File | ForEach-Object {
        [pscustomobject]@{ Name = $_.Name; Hash = (Get-FileHash -Algorithm SHA256 -LiteralPath $_.FullName).Hash }
    }
    if (Compare-Object $expectedCore $installedCore -Property Name, Hash) {
        throw 'Installed BepInEx core differs from the verified package.'
    }
    $installedVersion = [Reflection.AssemblyName]::GetAssemblyName(
        (Join-Path $targetCore 'BepInEx.dll')).Version.ToString()
    if ($installedVersion -ne '5.4.23.5') {
        throw "Unexpected installed BepInEx assembly version: $installedVersion"
    }
    if ((Get-Content -LiteralPath $modsPath -Raw) -notmatch
        '(?ms)^  name: denikson-BepInExPack_Valheim.*?^    patch: 2350\s*$') {
        throw 'TerrainRamp-1.0-Test mods.yml was not updated to BepInExPack 5.4.2350.'
    }

    Write-Output "Updated profile: $targetRoot"
    Write-Output "Backup: $backupRoot"
    Get-FileHash -Algorithm SHA256 -LiteralPath (Join-Path $targetCore 'BepInEx.dll')
} catch {
    $failure = $_.Exception.Message
    try {
        if (Test-Path -LiteralPath $targetCore) {
            Remove-Item -LiteralPath $targetCore -Recurse -Force
        }
        Copy-Item -LiteralPath (Join-Path $backupRoot 'core') -Destination $targetCore -Recurse -Force
        Copy-Item -LiteralPath (Join-Path $backupRoot 'mods.yml') -Destination $modsPath -Force
    } catch {
        throw "BepInEx update failed; rollback also failed. Original: $failure Rollback: $($_.Exception.Message)"
    }
    throw "BepInEx update failed and rollback was applied. $failure"
}
