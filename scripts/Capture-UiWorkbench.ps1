[CmdletBinding()]
param(
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe'
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repositoryRoot 'tools\BuildWorks.UiWorkbench'
$geometryProject = Join-Path $repositoryRoot 'src\BuildWorks.Geometry\BuildWorks.Geometry.csproj'
$geometryDll = Join-Path $repositoryRoot 'src\BuildWorks.Geometry\bin\Release\netstandard2.0\BuildWorks.Geometry.dll'
$pluginsPath = Join-Path $projectPath 'Assets\Plugins'
$logPath = Join-Path $repositoryRoot 'artifacts\unity-workbench-capture.log'
$outputPath = Join-Path $repositoryRoot 'artifacts\ui-workbench'
$startedAt = [DateTime]::UtcNow
$statusPath = Join-Path $repositoryRoot 'artifacts\ui-runtime\verification-status.json'
New-Item -ItemType Directory -Path (Split-Path -Parent $statusPath) -Force | Out-Null
@{ status = 'running_runtime_workbench'; startedAtUtc = $startedAt.ToString('o'); gameHostTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath $statusPath
$requiredVersion = '6000.0.61f1'

if (-not (Test-Path -LiteralPath $UnityPath -PathType Leaf)) {
    throw "Unity $requiredVersion not found: $UnityPath"
}
$installedVersion = (Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion
if ($installedVersion -notlike "$requiredVersion*") {
    throw "Unity version mismatch: expected $requiredVersion, found $installedVersion"
}

& dotnet build $geometryProject -c Release --nologo --no-restore `
    '-p:TargetPlatformSdkPath=C:\Program Files (x86)\Windows Kits\10\' `
    '-p:TargetPlatformDisplayName=Windows'
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $geometryDll -PathType Leaf)) {
    throw 'BuildWorks.Geometry build failed.'
}
New-Item -ItemType Directory -Path $pluginsPath -Force | Out-Null
Copy-Item -LiteralPath $geometryDll -Destination $pluginsPath -Force

# Compile the actual editor implementation. No separate visual facsimile is accepted.
$runtimeSources = Join-Path $projectPath 'Assets\Editor\RuntimeSources'
New-Item -ItemType Directory -Path $runtimeSources -Force | Out-Null
$runtimeFiles = @('BlueprintEditorView.cs', 'BlueprintEditorSkin.cs', 'BlueprintEditorInput.cs', 'BlueprintEditorController.cs',
    'BlueprintEditorScene.cs', 'BlueprintEditorMeshData.cs', 'BlueprintEditorIconLibrary.cs',
    'TransformGizmoView.cs', 'PlacementGhostPreviewView.cs', 'CompositeBlueprintStore.cs',
    'PrecisionPlacementHudView.cs', 'WorldSelectionHighlight.cs', 'BuildWorksPlacementValidation.cs')
$sourceProof = foreach ($name in $runtimeFiles) {
    $source = Join-Path $repositoryRoot ('src\BuildWorks\' + $name)
    $destination = Join-Path $runtimeSources $name
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
        throw "Runtime source copy mismatch: $name"
    }
    "$name $hash"
}
$iconDestination = Join-Path $projectPath 'Assets\Resources\BuildWorks\Icons'
New-Item -ItemType Directory -Path $iconDestination -Force | Out-Null
Get-ChildItem -LiteralPath (Join-Path $repositoryRoot 'src\BuildWorks\Assets\BlueprintEditorIcons') -Filter '*.png' |
    Copy-Item -Destination $iconDestination -Force
New-Item -ItemType Directory -Path (Join-Path $repositoryRoot 'artifacts\ui-runtime') -Force | Out-Null
$sourceProof | Set-Content -LiteralPath (Join-Path $repositoryRoot 'artifacts\ui-runtime\source-sha256.txt')

New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
$arguments = "-batchmode -projectPath `"$projectPath`" " +
    "-executeMethod RuntimeEditorAcceptance.CaptureAll -logFile `"$logPath`""
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(1000)) {
    if (([DateTime]::UtcNow - $startedAt).TotalMinutes -gt 10) {
        $process.Kill()
        throw "Unity Workbench timed out; stopped only its process $($process.Id). See $logPath"
    }
}
if ($process.ExitCode -ne 0) {
    $existingStatus = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
    if ($existingStatus.status -eq 'running_runtime_workbench') {
        @{ status = 'failed_runtime_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
    }
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Get-Content -LiteralPath $logPath -Tail 120
    }
    throw "Unity UI Workbench failed with exit code $($process.ExitCode)"
}

$outputPath = Join-Path $repositoryRoot 'artifacts\ui-runtime'
$expected = foreach ($state in 'select', 'transform', 'array', 'contour', 'catalog', 'outliner', 'settings', 'catalog-blueprints', 'catalog-materials') {
    foreach ($size in '1920x1080', '2560x1440', '3440x1440') {
        foreach ($scale in 100,120,140) { "$state-$size-$scale.png" }
    }
}
foreach ($name in $expected) {
    $path = Join-Path $outputPath $name
    $file = Get-Item -LiteralPath $path -ErrorAction SilentlyContinue
    if ($null -eq $file -or $file.Length -lt 10000 -or $file.LastWriteTimeUtc -lt $startedAt) {
        throw "Missing, stale, or invalid UI Workbench screenshot: $path"
    }
}
if (-not (Select-String -LiteralPath $logPath -SimpleMatch `
        "BUILDWORKS_RUNTIME_EDITOR_OK $($expected.Count) screenshots" -Quiet)) {
    throw "Unity UI Workbench success marker is missing: $logPath"
}

Write-Host "BuildWorks UI Workbench: $($expected.Count) screenshots OK"
