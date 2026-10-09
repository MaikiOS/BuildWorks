[CmdletBinding()]
param(
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',
    [string] $ProjectPath = ''
)

$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (-not $ProjectPath) { $ProjectPath = Join-Path $repositoryRoot 'tools\BuildWorks.UiWorkbench' }
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
if ((Split-Path -Parent (Split-Path -Parent $ProjectPath)) -ine $repositoryRoot -or
    (Split-Path -Leaf (Split-Path -Parent $ProjectPath)) -notin @('tools', 'artifacts') -or
    -not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets\Editor\RuntimeEditorAcceptance.cs') -PathType Leaf)) {
    throw 'Workbench must be a project copy directly under BuildWorks/tools or BuildWorks/artifacts; arbitrary Unity projects are not supported.'
}
if (-not (Test-Path -LiteralPath (Join-Path $ProjectPath 'ProjectSettings\ProjectVersion.txt') -PathType Leaf)) {
    throw "Unity Workbench project is missing: $ProjectPath"
}
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
$runtimeFiles = @('BlueprintEditorView.cs', 'BlueprintEditorCatalogView.cs', 'BlueprintEditorSkin.cs', 'BlueprintEditorInput.cs', 'BlueprintEditorController.cs',
    'BlueprintEditorScene.cs', 'BlueprintEditorMeshData.cs', 'BlueprintEditorIconLibrary.cs',
    'TransformGizmoView.cs', 'PlacementGhostPreviewView.cs', 'BlueprintBatchedPreview.cs', 'BlueprintWorldSnapPoints.cs', 'BlueprintItemStandPreview.cs', 'BlueprintItemStandCapture.cs', 'BlueprintItemStandPlacement.cs', 'BlueprintStorageCapture.cs', 'BlueprintThumbnailRenderer.cs', 'BlueprintPlacementFrame.cs', 'CompositeBlueprintStore.cs', 'BlueprintPrefabMetadataCapture.cs', 'ExternalBlueprintLibrary.cs', 'BlueprintExchangeFormat.cs',
    'PrecisionPlacementHudView.cs', 'BlueprintResourceHudView.cs', 'WorldSelectionHighlight.cs', 'BuildWorksPlacementValidation.cs', 'BlueprintSignText.cs', 'BlueprintExchangeView.cs')
$sourceProof = foreach ($name in $runtimeFiles) {
    $source = Join-Path $repositoryRoot ('src\BuildWorks\' + $name)
    $destination = Join-Path $runtimeSources $name
    if ($name -eq 'BlueprintWorldSnapPoints.cs') {
        # MonoBehaviour belongs to the runtime assembly, not Assembly-CSharp-Editor.
        if (Test-Path -LiteralPath $destination -PathType Leaf) { Remove-Item -LiteralPath $destination }
        $destination = Join-Path $projectPath 'Assets\RuntimeHost\BlueprintWorldSnapPoints.cs'
    }
    Copy-Item -LiteralPath $source -Destination $destination -Force
    $hash = (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash
    if ($hash -ne (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash) {
        throw "Runtime source copy mismatch: $name"
    }
    "$name $hash"
}
$englishCatalog = Join-Path $repositoryRoot 'src\BuildWorks\Translations\English.tsv'
Copy-Item -LiteralPath $englishCatalog -Destination (Join-Path $runtimeSources 'English.tsv') -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'src\BuildWorks\Translations\Russian.tsv') -Destination (Join-Path $runtimeSources 'Russian.tsv') -Force
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
$expected = foreach ($state in 'select', 'transform', 'array', 'contour', 'catalog', 'outliner', 'settings', 'catalog-blueprints', 'catalog-materials', 'catalog-quick', 'catalog-crafting', 'save-metadata', 'save-as', 'save-as-ru', 'supports', 'exchange-import', 'exchange-import-ru', 'exchange-import-path-ru', 'exchange-export', 'exchange-contents', 'exchange-contents-ru', 'exchange-import-review', 'exchange-import-review-ru', 'exchange-armor', 'exchange-armor-ru', 'exchange-missing', 'exchange-missing-ru', 'replace-tools', 'replace-confirm', 'replace-confirm-ru') {
    foreach ($size in '1280x720', '1920x1080', '2560x1440', '3440x1440') {
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
