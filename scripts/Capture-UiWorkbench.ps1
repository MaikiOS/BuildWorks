[CmdletBinding()]
param(
    [string] $UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',
    [string] $ProjectPath = '',
    [switch] $ContactOnly,
    [switch] $ReleaseOnly,
    [switch] $ExchangeOnly,
    [switch] $PlannerOnly,
    [switch] $ControllerInputOnly
)

$ErrorActionPreference = 'Stop'
if (@($ContactOnly, $ReleaseOnly, $ExchangeOnly, $PlannerOnly, $ControllerInputOnly).Where({ $_ }).Count -gt 1) { throw 'Choose one Workbench check mode.' }
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
$logPath = Join-Path $repositoryRoot $(if ($PlannerOnly) { 'artifacts\point-2.17-20261029\ui-planner324-v1\unity.log' } elseif ($ExchangeOnly) { 'artifacts\point-2.16-20261028\ui-legacy321\unity.log' } elseif ($ReleaseOnly) { 'artifacts\release-regression\unity.log' } elseif ($ContactOnly) { 'artifacts\placement-contact\unity.log' } else { 'artifacts\unity-workbench-capture.log' })
$outputPath = Join-Path $repositoryRoot 'artifacts\ui-workbench'
$startedAt = [DateTime]::UtcNow
$statusPath = Join-Path $repositoryRoot $(if ($PlannerOnly) { 'artifacts\point-2.17-20261029\ui-planner324-v1\verification-status.json' } elseif ($ExchangeOnly) { 'artifacts\point-2.16-20261028\ui-legacy321\verification-status.json' } elseif ($ReleaseOnly) { 'artifacts\release-regression\verification-status.json' } elseif ($ContactOnly) { 'artifacts\placement-contact\verification-status.json' } else { 'artifacts\ui-runtime\verification-status.json' })
New-Item -ItemType Directory -Path (Split-Path -Parent $statusPath) -Force | Out-Null
$runningStatus = if ($PlannerOnly) { 'running_planner_exports' } elseif ($ExchangeOnly) { 'running_exchange_exports' } elseif ($ReleaseOnly) { 'running_release_regression' } elseif ($ContactOnly) { 'running_placement_contact' } else { 'running_runtime_workbench' }
if ($ControllerInputOnly) {
    $logPath = Join-Path $repositoryRoot 'artifacts\ui-controller-input\unity.log'
    $statusPath = Join-Path $repositoryRoot 'artifacts\ui-controller-input\verification-status.json'
    $runningStatus = 'running_controller_input_workbench'
    New-Item -ItemType Directory -Path (Split-Path -Parent $statusPath) -Force | Out-Null
    $inputProof = Join-Path (Split-Path -Parent $statusPath) 'controller-input-check.txt'
    if (Test-Path -LiteralPath $inputProof -PathType Leaf) { Remove-Item -LiteralPath $inputProof }
}
@{ status = $runningStatus; startedAtUtc = $startedAt.ToString('o'); gameHostTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath $statusPath
$requiredVersion = '6000.0.61f1'
trap {
    if ($ControllerInputOnly) {
        @{ status = 'failed_controller_input_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); error = $_.Exception.Message; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
        throw $_
    }
    if ($ReleaseOnly) {
        @{ status = 'failed_release_regression_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); error = $_.Exception.Message; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
    }
    throw $_
}

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
foreach ($dependency in @('YamlDotNet.dll', 'THIRD_PARTY_NOTICES.md')) {
    $source = Join-Path (Split-Path -Parent $geometryDll) $dependency
    $destination = Join-Path $pluginsPath $dependency
    Copy-Item -LiteralPath $source -Destination $destination -Force
    if ((Get-FileHash -LiteralPath $source).Hash -ne (Get-FileHash -LiteralPath $destination).Hash) {
        throw "Workbench YAML dependency copy mismatch: $dependency"
    }
}
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
    'BlueprintEditorScene.cs', 'BlueprintEditorScene.Structural.cs', 'BlueprintEditorMeshData.cs', 'BlueprintEditorIconLibrary.cs', 'BlueprintStructuralSnapshot.cs', 'BlueprintStructuralAnalysis.cs', 'BlueprintSurfaceContact.cs', 'BlueprintTriangleContact.cs',
    'TransformGizmoView.cs', 'PlacementGhostPreviewView.cs', 'BlueprintBatchedPreview.cs', 'BlueprintWorldSnapPoints.cs', 'BlueprintItemStandPreview.cs', 'BlueprintItemStandCapture.cs', 'BlueprintItemStandPlacement.cs', 'BlueprintStorageCapture.cs', 'BlueprintStorageTransferJournal.cs', 'BlueprintMaterialState.cs', 'BlueprintThumbnailRenderer.cs', 'BlueprintPlacementFrame.cs', 'BlueprintHammerSnapSearch.cs', 'CompositeBlueprintStore.cs', 'BlueprintPrefabMetadataCapture.cs', 'ExternalBlueprintLibrary.cs', 'BlueprintExchangeFormat.cs',
    'BlueprintExchangeServerView.cs', 'BlueprintExchangePeerView.cs', 'BlueprintTransferBudget.cs', 'BlueprintPeerProtocol.cs', 'BlueprintPeerClient.cs', 'BlueprintServerClient.cs', 'BlueprintServerProtocol.cs', 'BlueprintServerEndpoint.cs', 'ServerBlueprintLibrary.cs', 'BlueprintUnboundImport.cs',
    'PrecisionPlacementHudView.cs', 'WorldSelectionHud.cs', 'BlueprintResourceHudView.cs', 'WorldSelectionHighlight.cs', 'BuildWorksPlacementValidation.cs', 'BuildWorksTestPlacementDiagnostics.cs', 'BlueprintSignText.cs', 'BlueprintExchangeView.cs', 'BlueprintExchangeBatchView.cs', 'PlanBuildExport.cs', 'BlueprintExportPair.cs', 'OpenKeepExport.cs', 'KgExport.cs', 'BetterBuildingExport.cs', 'PlannerExport.cs', 'BlueprintSourcePresentation.cs', 'VanillaMidpointSnapPoints.cs')
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
$sourceProof += "BuildWorks.Geometry.dll $((Get-FileHash -LiteralPath $geometryDll -Algorithm SHA256).Hash)"
foreach ($name in @('RuntimeEditorAcceptance.cs', 'ControllerInputAcceptance.cs', 'ControllerInputAcceptance.Structural.cs', 'StructuralSnapshotAcceptance.cs', 'StructuralAnalysisAcceptance.cs', 'PlacementContactAcceptance.cs', 'ControllerInputBbpAcceptance.cs', 'ControllerInputPlannerAcceptance.cs', 'ControllerInputKgAcceptance.cs', 'ControllerInputKgPresentationAcceptance.cs', 'ControllerInputOpenKeepAcceptance.cs')) {
    $sourceProof += "Editor/$name $((Get-FileHash -LiteralPath (Join-Path $projectPath ('Assets/Editor/' + $name)) -Algorithm SHA256).Hash)"
}
$sourceProof += "RuntimeHost/WearNTear.cs $((Get-FileHash -LiteralPath (Join-Path $projectPath 'Assets/RuntimeHost/WearNTear.cs') -Algorithm SHA256).Hash)"
foreach ($name in @('English.tsv', 'Russian.tsv')) { $sourceProof += "Translations/$name $((Get-FileHash -LiteralPath (Join-Path $runtimeSources $name) -Algorithm SHA256).Hash)" }
$sourceProof | Set-Content -LiteralPath (Join-Path (Split-Path -Parent $statusPath) 'source-sha256.txt')
if ($ReleaseOnly) { "ReleaseRegression.cs $((Get-FileHash -LiteralPath (Join-Path $projectPath 'Assets/Editor/ReleaseRegression.cs')).Hash)" | Add-Content -LiteralPath (Join-Path (Split-Path -Parent $statusPath) 'source-sha256.txt') }

New-Item -ItemType Directory -Path (Split-Path -Parent $logPath) -Force | Out-Null
$method = if ($ControllerInputOnly) { 'RuntimeEditorAcceptance.VerifyControllerInput' } elseif ($PlannerOnly) { 'RuntimeEditorAcceptance.VerifyPlannerExports' } elseif ($ExchangeOnly) { 'RuntimeEditorAcceptance.VerifyExchangeExports' } elseif ($ReleaseOnly) { 'ReleaseRegression.Run' } elseif ($ContactOnly) { 'PlacementContactAcceptance.VerifyContactOnly' } else { 'RuntimeEditorAcceptance.CaptureAll' }
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
        @{ status = $(if ($ReleaseOnly) { 'failed_release_regression_workbench' } elseif ($ContactOnly) { 'failed_placement_contact' } else { 'failed_runtime_workbench' }); completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
            ConvertTo-Json | Set-Content -LiteralPath $statusPath
    }
    if (Test-Path -LiteralPath $logPath -PathType Leaf) {
        Get-Content -LiteralPath $logPath -Tail 120
    }
    throw "Unity UI Workbench failed with exit code $($process.ExitCode)"
}

if ($PlannerOnly) {
    $proofPath = Join-Path $repositoryRoot 'artifacts/point-2.17-20261029/ui-planner324-v1/planner-ui-acceptance.json'
    if (!(Test-Path -LiteralPath $proofPath) -or (Get-Item -LiteralPath $proofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh planner UI proof missing.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    foreach ($flag in @('passed','editorLayers','pairedFiles','backNoWrite','pitchBlocked','nativeScrollEvent','sourceImmutable')) {
        if ($proof.$flag -isnot [bool] -or !$proof.$flag) { throw "Planner UI coverage incomplete: $flag" }
    }
    if ($proof.languages -ne 2 -or $proof.screenshots -ne 4 -or $proof.gameHostTested -isnot [bool] -or $proof.gameHostTested -or
        !(Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_PLANNER_EXPORTS_OK' -Quiet)) { throw 'Planner UI proof scope differs.' }
    foreach ($name in 'planner-en.png','planner-ru.png','planner-blocked-en.png','planner-blocked-ru.png') {
        $image = Get-Item -LiteralPath (Join-Path (Split-Path -Parent $proofPath) $name)
        if ($image.Length -lt 10000 -or $image.LastWriteTimeUtc -lt $startedAt) { throw "Fresh planner frame missing: $name" }
    }
    @{ status = 'passed_planner_exports'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); gameHostTested = $false } | ConvertTo-Json | Set-Content -LiteralPath $statusPath
    $proof | ConvertTo-Json
    return
}

if ($ExchangeOnly) {
    $proofPath = Join-Path $repositoryRoot 'artifacts/point-2.16-20261028/ui-legacy321/acceptance.json'
    if (!(Test-Path -LiteralPath $proofPath) -or (Get-Item -LiteralPath $proofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh exchange export proof missing.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.passed -isnot [bool] -or !$proof.passed -or $proof.gameHostTested -isnot [bool] -or $proof.gameHostTested -or
        $proof.languages -ne 2 -or $proof.targets -ne 2 -or $proof.existingExchangeLifecycle -ne $true -or
        $proof.nativeScrollEvent -ne $true -or $proof.freshDeferredScreenshots -ne 2 -or
        !(Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_EXCHANGE_EXPORTS_OK' -Quiet)) { throw 'Exchange export UI coverage incomplete.' }
    $bbpProofPath = Join-Path (Split-Path -Parent $proofPath) 'bbp-ui-acceptance.json'
    if (!(Test-Path -LiteralPath $bbpProofPath) -or (Get-Item -LiteralPath $bbpProofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh BBP export proof missing.' }
    $bbpProof = Get-Content -LiteralPath $bbpProofPath -Raw | ConvertFrom-Json
    if ($bbpProof.passed -ne $true -or $bbpProof.languages -ne 2 -or $bbpProof.originalWriterFixture -ne $true -or
        $bbpProof.pairedFiles -ne $true -or $bbpProof.backNoWrite -ne $true -or $bbpProof.staleTerrainBlocked -ne $true -or
        $bbpProof.rawSignAttachmentImageGuard -ne $true -or $bbpProof.ugcRevokeGrant -ne $true -or $bbpProof.sourceImmutable -ne $true -or $bbpProof.metadataCounts -ne $true -or
        $bbpProof.nativeGame -isnot [bool] -or $bbpProof.nativeGame) { throw 'BBP export UI coverage incomplete.' }
    $legacyProofPath = Join-Path (Split-Path -Parent $proofPath) 'legacy-json-ui-acceptance.json'
    if (!(Test-Path -LiteralPath $legacyProofPath) -or (Get-Item -LiteralPath $legacyProofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh legacy JSON export proof missing.' }
    $legacyProof = Get-Content -LiteralPath $legacyProofPath -Raw | ConvertFrom-Json
    foreach ($flag in @('passed','originalWriterFixture','pairedFiles','backNoWrite','staleTerrainBlocked','rawSignAttachmentImageGuard','ugcRevokeGrant','sourceImmutable','metadataCounts','legacyJson','reservedName','unknownWarningAndArchive')) {
        if ($legacyProof.$flag -isnot [bool] -or !$legacyProof.$flag) { throw "Legacy JSON UI coverage incomplete: $flag" }
    }
    if ($legacyProof.languages -ne 2 -or $legacyProof.nativeGame -isnot [bool] -or $legacyProof.nativeGame) { throw 'Legacy JSON UI proof scope differs.' }
    @{ status = 'passed_exchange_exports'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); gameHostTested = $false } | ConvertTo-Json | Set-Content -LiteralPath $statusPath
    $proof | ConvertTo-Json
    return
}
if ($ReleaseOnly) {
    $proofPath = Join-Path $repositoryRoot 'artifacts/release-regression/ui-verification.json'
    if (!(Test-Path -LiteralPath $proofPath) -or (Get-Item -LiteralPath $proofPath).LastWriteTimeUtc -lt $startedAt) { throw 'Fresh release_regression proof missing.' }
    $proof = Get-Content -LiteralPath $proofPath -Raw | ConvertFrom-Json
    if ($proof.passed -isnot [bool] -or !$proof.passed -or $proof.gameHostTested -isnot [bool] -or $proof.gameHostTested -or
        ($proof.rectangles -isnot [int] -and $proof.rectangles -isnot [long]) -or $proof.rectangles -ne 48 -or
        ($proof.catalogCases -isnot [int] -and $proof.catalogCases -isnot [long]) -or $proof.catalogCases -ne 3 -or
        ($proof.heightControls -isnot [int] -and $proof.heightControls -isnot [long]) -or $proof.heightControls -ne 4 -or
        !(Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_RELEASE_REGRESSION_OK' -Quiet)) { throw 'Release regression UI coverage incomplete.' }
    @{ status = 'passed_release_regression_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); gameHostTested = $false } | ConvertTo-Json | Set-Content -LiteralPath $statusPath
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
if ($ControllerInputOnly) {
    $proof = Get-Item -LiteralPath $inputProof -ErrorAction SilentlyContinue
    if ($null -eq $proof -or $proof.Length -eq 0 -or $proof.LastWriteTimeUtc -lt $startedAt -or
        !(Select-String -LiteralPath $logPath -SimpleMatch 'BUILDWORKS_CONTROLLER_INPUT_OK' -Quiet)) {
        throw "Missing or stale actual controller input proof: $inputProof"
    }
    @{ status = 'passed_controller_input_workbench'; completedAtUtc = [DateTime]::UtcNow.ToString('o'); logPath = $logPath; gameHostTested = $false } |
        ConvertTo-Json | Set-Content -LiteralPath $statusPath
    Write-Host 'Actual controller input passed in Unity; full Workbench and Valheim host not established.'
    return
}
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
