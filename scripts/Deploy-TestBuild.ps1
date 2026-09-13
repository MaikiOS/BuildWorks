[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'Medium')]
param(
    [Parameter(Mandatory = $true)]
    [ValidateNotNullOrEmpty()]
    [string] $TargetProfile,

    [switch] $ArmSkyCleanup
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceProfileRoot = Split-Path -Parent $projectRoot
$profilesRoot = Split-Path -Parent $sourceProfileRoot

if ((Split-Path -Leaf $projectRoot) -ine 'BuildWorks' -or
    (Split-Path -Leaf $sourceProfileRoot) -ine 'Default') {
    throw 'Expected source code at profiles\Default\BuildWorks. This directory is not an installed mod.'
}

if ($TargetProfile -ieq 'Default') {
    throw 'Refusing to deploy BuildWorks into Default.'
}

if ($TargetProfile -ine 'TerrainRamp-1.0-Test') {
    throw "BuildWorks test builds may target only TerrainRamp-1.0-Test, not '$TargetProfile'."
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
$targetInstall = Join-Path $targetProfileRoot 'BepInEx\plugins\Ostrix-BuildWorks'
$targetBepInEx = Join-Path $targetProfileRoot 'BepInEx'
$targetPlugins = Join-Path $targetBepInEx 'plugins'
$defaultPlugins = Join-Path $profilesRoot 'Default\BepInEx\plugins'
$defaultInstall = Join-Path $defaultPlugins 'Ostrix-BuildWorks'
$dllNames = @('BuildWorks.dll', 'BuildWorks.Geometry.dll')

Assert-NotReparsePoint $targetBepInEx
Assert-NotReparsePoint $targetPlugins
Assert-NotReparsePoint $targetInstall

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

$sourceHashes = @{}
$needsDeploy = $false

foreach ($name in $dllNames) {
    $source = Join-Path $artifactRoot $name
    $destination = Join-Path $targetInstall $name

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
    Write-Output 'TerrainRamp-1.0-Test already contains the current artifact pair; no files copied.'
}

foreach ($name in $dllNames) {
    $destination = Join-Path $targetInstall $name
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

if ($ArmSkyCleanup) {
    $cleanupDirectory = Join-Path $targetBepInEx 'config\BuildWorks'
    Assert-NotReparsePoint $cleanupDirectory
    New-Item -ItemType Directory -Path $cleanupDirectory -Force -Confirm:$false | Out-Null
    $cleanupMarker = Join-Path $cleanupDirectory 'cleanup-sky-TerrainRamp_Lab.once'
    Set-Content -LiteralPath $cleanupMarker -Value 'TerrainRamp_Lab' -Encoding Ascii
    Write-Output "Armed one-shot BuildWorks sky cleanup: $cleanupMarker"
}

Write-Output "Verified BuildWorks test deployment: $targetInstall"
