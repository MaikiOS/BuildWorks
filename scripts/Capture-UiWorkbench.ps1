[CmdletBinding()]
param(
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',
    [string] $ProjectPath = '',
    [switch] $ContactOnly,
    [switch] $ReleaseOnly
)

$ErrorActionPreference = 'Stop'
if ($ContactOnly -and $ReleaseOnly) { throw 'Choose one Workbench check mode.' }
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
$logPath = Join-Path $repositoryRoot $(if ($ReleaseOnly) { 'artifacts\release-214\unity.log' } elseif ($ContactOnly) { 'artifacts\placement-contact\unity.log' } else { 'artifacts\unity-workbench-capture.log' })
$outputPath = Join-Path $repositoryRoot 'artifacts\ui-workbench'
$startedAt = [DateTime]::UtcNow
$statusPath = Join-Path $repositoryRoot $(if ($ReleaseOnly) { 'artifacts\release-214\verification-status.json' } elseif ($ContactOnly) { 'artifacts\placement-contact\verification-status.json' } else { 'artifacts\ui-runtime\verification-status.json' })
New-Item -ItemType Directory -Path (Split-Path -Parent $statusPath) -Force | Out-Null
$runningStatus = if ($ReleaseOnly) { 'running_release214' } elseif ($ContactOnly) { 'running_placement_contact' } else { 'running_runtime_workbench' }
@{ status = $runningStatus; startedAtUtc = $startedAt.ToString('o'); gameHostTested = $false } |
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
foreach ($dependency in @('0Harmony.dll', 'Mono.Cecil.dll', 'MonoMod.Utils.dll', 'MonoMod.RuntimeDetour.dll')) {
    $dependencyPath = Join-Path (Split-Path -Parent $repositoryRoot) ('BepInEx\core\' + $dependency)
    Copy-Item -LiteralPath $dependencyPath -Destination $pluginsPath -Force
    if ((Get-FileHash -LiteralPath $dependencyPath).Hash -ne
        (Get-FileHash -LiteralPath (Join-Path $pluginsPath $dependency)).Hash) {
        throw "Workbench Harmony dependency copy mismatch: $dependency"
    }
}

# Compile the actual editor implementation. No separate visual facsimile is accepted.
$runtimeSources = Join-Path $projectPath 'Assets\Editor\RuntimeSources'
New-Item -ItemType Directory -Path $runtimeSources -Force | Out-Null
$runtimeFiles = @('BlueprintRotationState.cs', 'BlueprintSignPreview.cs', 'BlueprintPrefabMigrationGeometry.cs', 'BuildWorksQaUiCancellation.cs', 'NativePlacementMaterialPreview.cs', 'BlueprintMaterialVariation.cs', 'BlueprintEditorView.cs', 'BlueprintEditorCatalogView.cs', 'BlueprintEditorSkin.cs', 'BlueprintEditorInput.cs', 'BlueprintEditorController.cs',
    'BlueprintEditorScene.cs', 'BlueprintEditorMeshData.cs', 'BlueprintEditorIconLibrary.cs',
    'TransformGizmoView.cs', 'PlacementGhostPreviewView.cs', 'BlueprintBatchedPreview.cs', 'BlueprintWorldSnapPoints.cs', 'BlueprintItemStandPreview.cs', 'BlueprintItemStandCapture.cs', 'BlueprintItemStandPlacement.cs', 'BlueprintStorageCapture.cs', 'BlueprintStorageTransferJournal.cs', 'BlueprintMaterialState.cs', 'BlueprintThumbnailRenderer.cs', 'BlueprintPlacementFrame.cs', 'BlueprintHammerSnapSearch.cs', 'CompositeBlueprintStore.cs', 'BlueprintPrefabMetadataCapture.cs', 'ExternalBlueprintLibrary.cs', 'BlueprintExchangeFormat.cs',
    'BlueprintExchangeServerView.cs', 'BlueprintServerClient.cs', 'BlueprintServerProtocol.cs', 'BlueprintServerEndpoint.cs', 'ServerBlueprintLibrary.cs',
    'PrecisionPlacementHudView.cs', 'WorldSelectionHud.cs', 'BlueprintResourceHudView.cs', 'WorldSelectionHighlight.cs', 'BuildWorksPlacementValidation.cs', 'BuildWorksTestPlacementDiagnostics.cs', 'BlueprintSignText.cs', 'BlueprintExchangeView.cs', 'BlueprintExchangeBatchView.cs', 'PlanBuildExport.cs', 'VanillaMidpointSnapPoints.cs')
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
$sourceProof | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $statusPath) 'source-sha256.txt')
if ($ReleaseOnly) { "ReleaseRegression214.cs $((Get-FileHash -LiteralPath (Join-Path $projectPath 'Assets/Editor/ReleaseRegression214.cs')).Hash)" | Add-Content -LiteralPath (Join-Path (Split-Path -Parent $statusPath) 'source-sha256.txt') }

New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
$method = if ($ReleaseOnly) { 'ReleaseRegression214.Run' } elseif ($ContactOnly) { 'PlacementContactAcceptance.Verify' } else { 'RuntimeEditorAcceptance.CaptureAll' }
$graphics = if ($ContactOnly) { '-nographics ' } else { '' }
$arguments = "-batchmode $graphics-projectPath `"$projectPath`" " +
    "-executeMethod $method -logFile `"$logPath`""
if ($ReleaseOnly) { $arguments += ' -force-d3d11' }
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments `
    -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(1000)) {
    if (([DateTime]::UtcNow - $startedAt).TotalMinutes -gt 10) {
        $process.Kill()
        @{ status = 'failed_workbench_timeout'; mode = $runningStatus; completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
        throw "Unity Workbench timed out; stopped only its process $($process.Id). See $logPath"
    }
}
if ($process.ExitCode -ne 0) {
    $existingStatus = Get-Content -LiteralPath $statusPath -Raw | ConvertFrom-Json
    if ($existingStatus.status -eq $runningStatus) {
        @{ status = $(if ($ReleaseOnly) { 'failed_release214_workbench' } elseif ($ContactOnly) { 'failed_placement_contact' } else { 'failed_runtime_workbench' }); completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
    }
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Get-Content -LiteralPath $logPath -Tail 120
    }
    throw "Unity UI Workbench failed with exit code $($process.ExitCode)"
}

if ($ReleaseOnly) {
    $proofPath = Join-Path $repositoryRoot 'artifacts/release-214/ui-verification.json'
    if (!(Test-Path -LiteralPath $proofPath) -or (Get-Item -LiteralPath $proofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh release214 proof missing.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.passed -isnot [bool] -or !$proof.passed -or $proof.gameHostTested -isnot [bool] -or $proof.gameHostTested -or
        ($proof.rectangles -isnot [int] -and $proof.rectangles -isnot [long]) -or $proof.rectangles -ne 48 -or
        ($proof.catalogCases -isnot [int] -and $proof.catalogCases -isnot [long]) -or $proof.catalogCases -ne 3 -or
        !(Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_RELEASE214_OK' -Quiet)) { throw 'Release214 UI coverage incomplete.' }
    @{ status = 'passed_release214_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); gameHostTested = $false } | ConvertTo-Json | Set-Content -LiteralPath $statusPath
    $proof | ConvertTo-Json
    return
}
if ($ContactOnly) {
    if (-not (Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_CONTACT_OK' -Quiet)) {
        throw "Unity placement contact success marker is missing: $logPath"
    }
    @{ status = 'passed_placement_contact'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
        ConvertTo-Json | Set-Content -LiteralPath $statusPath
    Write-Host 'BuildWorks placement contact: Physics and diagnostic observation OK; Valheim not tested.'
    return
}

$outputPath = Join-Path $repositoryRoot 'artifacts\ui-runtime'
$expected = foreach ($state in 'server-list-ru', 'server-review-ru', 'select', 'transform', 'scale-xyz-ru', 'array', 'contour', 'catalog', 'outliner', 'outliner-filter-ru', 'outliner-fields-ru', 'outliner-empty-ru', 'interior-camera-ru', 'settings', 'catalog-blueprints', 'catalog-materials', 'catalog-quick', 'catalog-crafting', 'catalog-managed-ru', 'catalog-hidden-ru', 'delete-confirm-ru', 'hide-confirm-ru', 'save-metadata', 'save-as', 'save-as-ru', 'supports', 'exchange-import', 'exchange-import-ru', 'exchange-import-path-ru', 'exchange-export', 'share-ru', 'share-error-ru', 'batch-select-ru', 'batch-review-ru', 'batch-picker-ru', 'batch-export-ru', 'exchange-contents', 'exchange-contents-ru', 'exchange-import-review', 'exchange-import-review-ru', 'exchange-import-migration-ru', 'exchange-import-category-ru', 'exchange-import-category-picker-ru', 'exchange-armor', 'exchange-armor-ru', 'exchange-missing', 'exchange-missing-ru', 'replace-tools', 'replace-confirm', 'replace-confirm-ru', 'replace-entry-ru', 'replace-help-ru') {
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
