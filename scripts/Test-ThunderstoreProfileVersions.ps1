[CmdletBinding()]
param(
    [string] $Profile = 'TerrainRamp-1.0-Test',
    [string] $OutputPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profilesRoot = Split-Path -Parent (Split-Path -Parent $projectRoot)
$profileRoot = [IO.Path]::GetFullPath((Join-Path $profilesRoot $Profile))
$manifestPath = Join-Path $profileRoot 'mods.yml'
if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) {
    throw "Profile manifest is missing: $manifestPath"
}

$installed = foreach ($block in [regex]::Split(
        (Get-Content -LiteralPath $manifestPath -Raw), '(?m)(?=^- manifestVersion:)')) {
    if (-not $block.Trim()) { continue }
    $name = [regex]::Match($block, '(?m)^  name: (.+)$')
    $version = [regex]::Match($block,
        '(?ms)^  versionNumber:\s*\r?\n    major: (\d+)\s*\r?\n    minor: (\d+)\s*\r?\n    patch: (\d+)')
    $enabled = [regex]::Match($block, '(?m)^  enabled: (true|false)$')
    if (-not $name.Success -or -not $version.Success) {
        throw 'Could not parse a package record in mods.yml.'
    }
    [pscustomobject]@{
        Name = $name.Groups[1].Value.Trim()
        Version = '{0}.{1}.{2}' -f $version.Groups[1].Value,
            $version.Groups[2].Value, $version.Groups[3].Value
        Enabled = -not $enabled.Success -or $enabled.Groups[1].Value -eq 'true'
    }
}

$catalog = Invoke-RestMethod -Uri 'https://thunderstore.io/c/valheim/api/v1/package/' -Method Get
$byName = @{}
foreach ($package in $catalog) { $byName[$package.full_name] = $package }

$results = foreach ($entry in $installed) {
    $package = $byName[$entry.Name]
    if ($null -eq $package) {
        [pscustomobject]@{
            name = $entry.Name; installed = $entry.Version; latest = $null
            status = 'missing_from_catalog'; deprecated = $null; updated = $null
            url = $null
        }
        continue
    }
    $latest = $package.versions | Where-Object is_active | Select-Object -First 1
    $status = if ($null -eq $latest) { 'no_active_version' }
        elseif ($entry.Version -eq $latest.version_number) { 'current' }
        else { 'outdated' }
    [pscustomobject]@{
        name = $entry.Name; installed = $entry.Version
        latest = if ($null -eq $latest) { $null } else { $latest.version_number }
        status = $status; deprecated = [bool]$package.is_deprecated
        updated = $package.date_updated; url = $package.package_url
    }
}

$report = [ordered]@{
    checkedAtUtc = (Get-Date).ToUniversalTime().ToString('o')
    profile = $Profile
    installedPackages = @($installed).Count
    current = @($results | Where-Object status -eq 'current').Count
    outdated = @($results | Where-Object status -eq 'outdated').Count
    missing = @($results | Where-Object status -eq 'missing_from_catalog').Count
    deprecated = @($results | Where-Object deprecated).Count
    packages = @($results)
}

if ($OutputPath) {
    $absoluteOutput = [IO.Path]::GetFullPath((Join-Path $projectRoot $OutputPath))
    $projectPrefix = $projectRoot.TrimEnd([IO.Path]::DirectorySeparatorChar,
        [IO.Path]::AltDirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if (-not $absoluteOutput.StartsWith($projectPrefix, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'OutputPath must stay inside the BuildWorks project.'
    }
    New-Item -ItemType Directory -Path (Split-Path -Parent $absoluteOutput) -Force | Out-Null
    [IO.File]::WriteAllText($absoluteOutput, ($report | ConvertTo-Json -Depth 6),
        [Text.UTF8Encoding]::new($false))
}

$report | ConvertTo-Json -Depth 6
