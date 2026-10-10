[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $TargetProfile,

    [switch] $ArmSkyCleanup,

    [string] $BlueprintLibraryArchive,

    [switch] $PlayerTestFixtures,

    [ValidateNotNullOrEmpty()]
    [string] $QaStandToken
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceProfileRoot = Split-Path -Parent $projectRoot
$profilesRoot = Split-Path -Parent $sourceProfileRoot

if ($TargetProfile -ieq 'Default') {
    throw 'Refusing to deploy BuildWorks into Default.'
}

if ($TargetProfile -ine 'TerrainRamp-1.0-Test') {
    throw "BuildWorks test builds may target only TerrainRamp-1.0-Test, not '$TargetProfile'."
}

if ((Split-Path -Leaf $projectRoot) -ine 'BuildWorks' -or
    (Split-Path -Leaf $sourceProfileRoot) -ine 'Default') {
    throw 'Expected source code at profiles\Default\BuildWorks. This directory is not an installed mod.'
}

$targetProfileRoot = Join-Path $profilesRoot $TargetProfile
if (-not (Test-Path -LiteralPath $targetProfileRoot -PathType Container)) {
    throw "Target profile does not exist: $targetProfileRoot"
}

if (Get-Process -Name valheim, valheim_server -ErrorAction SilentlyContinue) {
    throw 'Valheim must be closed before deploying BuildWorks.'
}

function Assert-NotReparsePoint {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Path
    )

    if (Test-Path -LiteralPath $Path) {
        $item = Get-Item -LiteralPath $Path -Force
        if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "Refusing deployment through a junction or symbolic link: $Path"
        }
    }
}

Assert-NotReparsePoint $targetProfileRoot

$artifactRoot = Join-Path $projectRoot 'artifacts\bin'
if ($QaStandToken) { $artifactRoot = Join-Path $projectRoot 'artifacts\test-stand-bin' }
$targetInstall = Join-Path $targetProfileRoot 'BepInEx\plugins\Ostrix-BuildWorks'
$targetBepInEx = Join-Path $targetProfileRoot 'BepInEx'
$targetPlugins = Join-Path $targetBepInEx 'plugins'
$defaultPlugins = Join-Path $profilesRoot 'Default\BepInEx\plugins'
$defaultInstall = Join-Path $defaultPlugins 'Ostrix-BuildWorks'
$dllNames = @('BuildWorks.dll', 'BuildWorks.Geometry.dll')

Assert-NotReparsePoint $targetBepInEx
Assert-NotReparsePoint $targetPlugins
Assert-NotReparsePoint $targetInstall

$qaConfig = Join-Path $targetBepInEx 'config\BuildWorks'
$qaMarker = Join-Path $qaConfig 'qa-isolation.token'
foreach ($path in @((Join-Path $targetBepInEx 'config'), $qaConfig, $qaMarker)) { Assert-NotReparsePoint $path }
if ($QaStandToken) {
    $sessionId = [guid]::Empty
    if (-not [guid]::TryParseExact($QaStandToken, 'N', [ref]$sessionId) -or $sessionId -eq [guid]::Empty) {
        throw 'QA stand token must be a nonzero GUID in N format.'
    }
    $QaStandToken = $sessionId.ToString('N')
    if ($ArmSkyCleanup -or $BlueprintLibraryArchive) { throw 'QA stand deployment cannot arm cleanup or add an unrelated library.' }
    if (Test-Path -LiteralPath $qaMarker) {
        if ((Get-Item -LiteralPath $qaMarker).Length -gt 64 -or [IO.File]::ReadAllText($qaMarker).Trim() -cne $QaStandToken) {
            throw 'A different QA stand token already exists; refusing to replace its identity.'
        }
    }
    & (Join-Path $PSScriptRoot 'Test-QASaveIsolation.ps1') -CheckLaunchProfileOnly
    & (Join-Path $PSScriptRoot 'Test-QASaveIsolation.ps1') -CheckCompiledOnly
    $PlayerTestFixtures = $true
}
elseif (Test-Path -LiteralPath $qaMarker) {
    throw 'An armed QA isolation token requires explicit -QaStandToken; normal deployment will not silently replace it.'
}

function Assert-DefaultClean {
    if (Test-Path -LiteralPath $defaultInstall) {
        throw "Unsafe state: the Default install directory exists: $defaultInstall"
    }

    if (Test-Path -LiteralPath $defaultPlugins) {
        $strayDlls = @(
            Get-ChildItem -LiteralPath $defaultPlugins -Recurse -Force -File `
                -Filter 'BuildWorks*.dll'
        )

        if ($strayDlls.Count -gt 0) {
            throw "Unsafe state: BuildWorks DLL found under Default plugins: $($strayDlls.FullName -join ', ')"
        }
    }
}

Assert-DefaultClean

# Preflight every source/destination before the first DLL/profile write.
$fixtureCopies = @()
if ($PlayerTestFixtures) {
    $fixtureSource = Join-Path $projectRoot 'tests\fixtures\player-exchange'
    $fixtureTarget = Join-Path $qaConfig 'test-fixtures'
    foreach ($path in @($fixtureSource, $fixtureTarget)) { Assert-NotReparsePoint $path }
    foreach ($name in '01-walls.blueprint', '02-sign.blueprint', '03-opaque.blueprint',
        '04-snap.blueprint', '05-missing.blueprint', '06-lamp.blueprint', '16-supported-snap.blueprint',
        '17-supported-snap-a.blueprint', '18-supported-snap-b.blueprint') {
        $fixtureCopies += @{ Source = (Join-Path $fixtureSource $name); Name = $name; Expected = $null }
    }
    Assert-NotReparsePoint (Join-Path $projectRoot 'tests\fixtures\contents')
    foreach ($name in '24-contents-empty.buildworks.json', '25-contents-populated.buildworks.json') {
        $fixtureCopies += @{ Source = (Join-Path $projectRoot ('tests\fixtures\contents\' + $name)); Name = $name; Expected = $null }
    }
    if ($QaStandToken) {
        Add-Type -Path (Join-Path $targetBepInEx 'core\Mono.Cecil.dll')
        $qaAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $artifactRoot 'BuildWorks.dll'))
        try {
            $supplies = $qaAssembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestSupplies')
            $manifest = @((($supplies.Methods | Where-Object Name -eq '.cctor').Body.Instructions |
                Where-Object { $_.OpCode.Name -eq 'ldstr' }).Operand)
            if ($manifest.Count -ne 7) { throw 'Unexpected QA source fixture manifest.' }
            foreach ($field in @(@('SupportedSnapFixture','16-supported-snap.blueprint'),
                @('SupportedSnapAFixture','17-supported-snap-a.blueprint'), @('SupportedSnapBFixture','18-supported-snap-b.blueprint'),
                @('ContentsEmptyFixture','24-contents-empty.buildworks.json'), @('ContentsPopulatedFixture','25-contents-populated.buildworks.json'))) {
                $supplement = @($supplies.Fields | Where-Object Name -CEQ $field[0])
                if ($supplement.Count -ne 1 -or -not $supplement[0].HasConstant -or
                    $supplement[0].Constant -cnotmatch ('^' + [regex]::Escape($field[1]) + '\|[A-F0-9]{64}$')) {
                    throw 'Unexpected supplemental QA fixture manifest.'
                }
                $manifest += [string]$supplement[0].Constant
            }
            foreach ($entry in $manifest) {
                if ($entry -cnotmatch '^([a-zA-Z0-9_-]+\.(?:blueprint|buildworks\.json))\|([A-F0-9]{64})$') { throw 'Invalid QA source fixture manifest entry.' }
                $name = $Matches[1]; $expected = $Matches[2]
                $owned = @($fixtureCopies | Where-Object Name -CEQ $name)
                if ($owned.Count -eq 1) { $owned[0].Expected = $expected }
                elseif ($name -cin @('hs_ashlands_madzobuild_coffeetable.blueprint', 'hs_blackforest_hut.blueprint')) {
                    $external = Join-Path $projectRoot 'artifacts\external-blueprints\BiomeBlueprints-1.0.3\smoke'
                    Assert-NotReparsePoint (Split-Path $external)
                    Assert-NotReparsePoint $external
                    $fixtureCopies += @{ Source = (Join-Path $external $name); Name = $name; Expected = $expected }
                }
                else { throw "Unreviewed QA source fixture: $name" }
            }
        }
        finally { $qaAssembly.Dispose() }
    }
    foreach ($copy in $fixtureCopies) {
        Assert-NotReparsePoint $copy.Source
        $copy.Destination = Join-Path $fixtureTarget $copy.Name
        Assert-NotReparsePoint $copy.Destination
        $copy.Hash = (Get-FileHash -LiteralPath $copy.Source -Algorithm SHA256).Hash
        if ($copy.Expected -and $copy.Hash -cne $copy.Expected) { throw "QA source fixture hash changed: $($copy.Name)" }
        if ((Test-Path -LiteralPath $copy.Destination) -and
            (Get-FileHash -LiteralPath $copy.Destination -Algorithm SHA256).Hash -cne $copy.Hash) {
            throw "Test fixture already exists with different contents; refusing overwrite: $($copy.Destination)"
        }
    }
}

# Copy only an inert archive into BuildWorks' own config. Runtime reads bounded
# text entries; no third-party DLL, terrain or code is extracted/installed.
$librarySource = $null
if ($BlueprintLibraryArchive) {
    $librarySource = (Resolve-Path -LiteralPath $BlueprintLibraryArchive).Path
    Assert-NotReparsePoint $librarySource
    if ([IO.Path]::GetExtension($librarySource) -ine '.zip' -or
        (Get-Item -LiteralPath $librarySource).Length -gt 64MB) {
        throw 'Expected a blueprint ZIP archive no larger than 64 MiB.'
    }
    $libraryHash = (Get-FileHash -LiteralPath $librarySource -Algorithm SHA256).Hash
    $libraryConfig = Join-Path $targetBepInEx 'config'
    $libraryRoot = Join-Path $libraryConfig 'BuildWorks'
    $libraryImports = Join-Path $libraryRoot 'imports'
    foreach ($libraryDirectory in @($libraryConfig, $libraryRoot, $libraryImports)) {
        Assert-NotReparsePoint $libraryDirectory
    }
    $libraryDestination = Join-Path $libraryImports ('library-' + $libraryHash + '.zip')
    Assert-NotReparsePoint $libraryDestination
    if ((Test-Path -LiteralPath $libraryDestination) -and
        (Get-FileHash -LiteralPath $libraryDestination -Algorithm SHA256).Hash -ne $libraryHash) {
        throw 'Existing blueprint library hash does not match; refusing overwrite.'
    }
}

$sourceHashes = @{}
$needsDeploy = $false

foreach ($name in $dllNames) {
    $source = Join-Path $artifactRoot $name
    $destination = Join-Path $targetInstall $name
    Assert-NotReparsePoint $source
    Assert-NotReparsePoint $destination

    if (-not (Test-Path -LiteralPath $source -PathType Leaf)) {
        throw "Required build artifact is missing: $source"
    }

    $sourceHashes[$name] = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash

    if (-not (Test-Path -LiteralPath $destination -PathType Leaf) -or
        (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $sourceHashes[$name]) {
        $needsDeploy = $true
    }
}

if ($needsDeploy) {
    $operation = 'Deploy BuildWorks.dll and BuildWorks.Geometry.dll as one required pair'
    if (-not $PSCmdlet.ShouldProcess($targetInstall, $operation)) {
        Assert-DefaultClean
        Write-Output "Validated BuildWorks test target; no files copied: $targetInstall"
        return
    }

    $scratchName = '.Ostrix-BuildWorks.deploy-' + [guid]::NewGuid().ToString('N')
    $scratchRoot = Join-Path $targetPlugins $scratchName
    $scratchFull = [IO.Path]::GetFullPath($scratchRoot)
    $targetPluginsFull = [IO.Path]::GetFullPath($targetPlugins)

    if ([IO.Path]::GetFullPath((Split-Path -Parent $scratchFull)) -ne $targetPluginsFull) {
        throw "Unsafe deployment scratch path: $scratchFull"
    }

    New-Item -ItemType Directory -Path $scratchRoot -Force -Confirm:$false | Out-Null

    try {
        foreach ($name in $dllNames) {
            $staged = Join-Path $scratchRoot $name
            Copy-Item `
                -LiteralPath (Join-Path $artifactRoot $name) `
                -Destination $staged `
                -Confirm:$false

            if ((Get-FileHash -LiteralPath $staged -Algorithm SHA256).Hash -ne
                $sourceHashes[$name]) {
                throw "SHA256 mismatch while staging: $name"
            }
        }

        $backupRoot = Join-Path $scratchRoot 'backup'
        $hadOriginal = @{}
        $originalHashes = @{}
        New-Item -ItemType Directory -Path $targetInstall -Force -Confirm:$false | Out-Null

        foreach ($name in $dllNames) {
            $destination = Join-Path $targetInstall $name
            $hadOriginal[$name] = Test-Path -LiteralPath $destination -PathType Leaf
            if ($hadOriginal[$name]) {
                $originalHashes[$name] = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
                New-Item -ItemType Directory -Path $backupRoot -Force -Confirm:$false | Out-Null
                $backup = Join-Path $backupRoot $name
                Copy-Item `
                    -LiteralPath $destination `
                    -Destination $backup `
                    -Confirm:$false

                if ((Get-FileHash -LiteralPath $backup -Algorithm SHA256).Hash -ne
                    $originalHashes[$name]) {
                    throw "SHA256 mismatch while backing up: $name"
                }
            }
        }

        try {
            foreach ($name in $dllNames) {
                Assert-NotReparsePoint (Join-Path $targetInstall $name)
                Copy-Item `
                    -LiteralPath (Join-Path $scratchRoot $name) `
                    -Destination (Join-Path $targetInstall $name) `
                    -Force `
                    -Confirm:$false
            }

            foreach ($name in $dllNames) {
                $installedHash = (Get-FileHash `
                    -LiteralPath (Join-Path $targetInstall $name) `
                    -Algorithm SHA256).Hash
                if ($installedHash -ne $sourceHashes[$name]) {
                    throw "SHA256 mismatch after deployment: $name"
                }
            }
        }
        catch {
            $deployError = $_
            $rollbackErrors = [Collections.Generic.List[string]]::new()
            foreach ($name in $dllNames) {
                try {
                    $destination = Join-Path $targetInstall $name
                    Assert-NotReparsePoint $destination
                    if ($hadOriginal[$name]) {
                        Copy-Item `
                            -LiteralPath (Join-Path $backupRoot $name) `
                            -Destination $destination `
                            -Force `
                            -Confirm:$false

                        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne
                            $originalHashes[$name]) {
                            throw 'restored SHA256 does not match the original'
                        }
                    }
                    elseif (Test-Path -LiteralPath $destination -PathType Leaf) {
                        Remove-Item -LiteralPath $destination -Force -Confirm:$false
                    }
                }
                catch {
                    $rollbackErrors.Add("$name`: $($_.Exception.Message)")
                }
            }

            if ($rollbackErrors.Count -gt 0) {
                throw "Deployment failed: $($deployError.Exception.Message). Rollback incomplete: $($rollbackErrors -join '; ')"
            }

            throw $deployError
        }
    }
    finally {
        if (Test-Path -LiteralPath $scratchFull -PathType Container) {
            Remove-Item -LiteralPath $scratchFull -Recurse -Force -Confirm:$false
        }
    }
}
else {
    Write-Output "$TargetProfile already contains the current artifact pair; no files copied."
}

foreach ($name in $dllNames) {
    $destination = Join-Path $targetInstall $name
    Assert-NotReparsePoint $destination
    if (-not (Test-Path -LiteralPath $destination -PathType Leaf)) {
        throw "Deployment verification failed; missing: $destination"
    }

    $installedHash = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
    if ($installedHash -ne $sourceHashes[$name]) {
        throw "SHA256 mismatch after deployment: $name"
    }

    Write-Output "$name SHA256 $installedHash"
}

Assert-DefaultClean

if ($librarySource -and -not (Test-Path -LiteralPath $libraryDestination)) {
    if ($PSCmdlet.ShouldProcess($libraryDestination, 'Add a read-only source blueprint library archive')) {
        New-Item -ItemType Directory -Path $libraryImports -Force -Confirm:$false | Out-Null
        Copy-Item -LiteralPath $librarySource -Destination $libraryDestination -Confirm:$false
        if ((Get-FileHash -LiteralPath $libraryDestination -Algorithm SHA256).Hash -ne $libraryHash) {
            throw 'Blueprint library SHA256 mismatch after copying.'
        }
        Write-Output "Read-only blueprint library SHA256 $libraryHash"
    }
}

if ($ArmSkyCleanup) {
    $cleanupDirectory = Join-Path $targetBepInEx 'config\BuildWorks'
    Assert-NotReparsePoint $cleanupDirectory
    New-Item -ItemType Directory -Path $cleanupDirectory -Force -Confirm:$false | Out-Null
    $cleanupMarker = Join-Path $cleanupDirectory 'cleanup-sky-TerrainRamp_Lab.once'
    Set-Content -LiteralPath $cleanupMarker -Value 'TerrainRamp_Lab' -Encoding Ascii
    Write-Output "Armed one-shot BuildWorks sky cleanup: $cleanupMarker"
}

if ($PlayerTestFixtures) {
    foreach ($copy in $fixtureCopies) {
        Assert-NotReparsePoint $copy.Destination
        if (-not (Test-Path -LiteralPath $copy.Destination) -and
            $PSCmdlet.ShouldProcess($copy.Destination, 'Add a hash-checked blueprint fixture; no world/save changes')) {
            New-Item -ItemType Directory -Path $fixtureTarget -Force -Confirm:$false | Out-Null
            Copy-Item -LiteralPath $copy.Source -Destination $copy.Destination -Confirm:$false
            if ((Get-FileHash -LiteralPath $copy.Destination -Algorithm SHA256).Hash -cne $copy.Hash) {
                throw "Test fixture SHA256 mismatch: $($copy.Destination)"
            }
        }
    }
    Write-Output "Player test fixture directory: $fixtureTarget"
}

# Arm only after the entire DLL pair and every fixture have been verified.
if ($QaStandToken -and -not $WhatIfPreference) {
    foreach ($copy in $fixtureCopies) {
        if (-not (Test-Path -LiteralPath $copy.Destination -PathType Leaf) -or
            (Get-FileHash -LiteralPath $copy.Destination -Algorithm SHA256).Hash -cne $copy.Hash) {
            throw "QA fixture is not installed exactly; isolation was not armed: $($copy.Destination)"
        }
    }
    if (Get-Process -Name valheim, valheim_server -ErrorAction SilentlyContinue) {
        throw 'Valheim started during deployment; QA isolation was not armed.'
    }
}
if ($QaStandToken -and -not (Test-Path -LiteralPath $qaMarker) -and
    $PSCmdlet.ShouldProcess($qaMarker, 'Arm the explicit isolated QA stand; saves are created only on an authorized game launch')) {
    New-Item -ItemType Directory -Path $qaConfig -Force -Confirm:$false | Out-Null
    $stream = [IO.File]::Open($qaMarker, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try {
        $bytes = [Text.Encoding]::ASCII.GetBytes($QaStandToken)
        $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true)
    }
    finally { $stream.Dispose() }
    Write-Output "Armed isolated BuildWorks QA identity: $QaStandToken"
}

Write-Output "Verified BuildWorks test deployment: $targetInstall"
