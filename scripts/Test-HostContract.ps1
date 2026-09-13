[CmdletBinding()]
param(
    [string] $ValheimManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profileRoot = Split-Path -Parent $projectRoot
$cecilPath = Join-Path $profileRoot 'BepInEx\core\Mono.Cecil.dll'
$hostPath = Join-Path $ValheimManagedDir 'assembly_valheim.dll'
$utilsPath = Join-Path $ValheimManagedDir 'assembly_utils.dll'
$pluginPath = Join-Path $projectRoot 'artifacts\bin\BuildWorks.dll'
$geometryPath = Join-Path $projectRoot 'artifacts\bin\BuildWorks.Geometry.dll'
$pluginSourcePath = Join-Path $projectRoot 'src\BuildWorks\BuildWorksPlugin.cs'
$sessionSourcePath = Join-Path $projectRoot 'src\BuildWorks\PrecisionPlacementSession.cs'
$hudSourcePath = Join-Path $projectRoot 'src\BuildWorks\PrecisionPlacementHudView.cs'
$blueprintRegistrySourcePath = Join-Path $projectRoot `
    'src\BuildWorks\HammerBlueprintPieceRegistry.cs'
$catalogSourcePath = Join-Path $projectRoot 'src\BuildWorks\UnifiedHammerCatalog.cs'
$blueprintStoreSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\CompositeBlueprintStore.cs'
$thumbnailSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\BlueprintThumbnailRenderer.cs'
$previewSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\PlacementGhostPreviewView.cs'
$midpointSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\VanillaMidpointSnapPoints.cs'
$editorDocumentSourcePath = Join-Path $projectRoot `
    'src\BuildWorks.Geometry\BlueprintEditorDocument.cs'
$editorSceneSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\BlueprintEditorScene.cs'
$editorViewSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\BlueprintEditorView.cs'
$editorControllerSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\BlueprintEditorController.cs'
$gizmoSourcePath = Join-Path $projectRoot `
    'src\BuildWorks\TransformGizmoView.cs'
$editorIconLibrarySourcePath = Join-Path $projectRoot `
    'src\BuildWorks\BlueprintEditorIconLibrary.cs'
$editorIconRoot = Join-Path $projectRoot `
    'src\BuildWorks\Assets\BlueprintEditorIcons'
$requiredEditorIcons = @(
    'select', 'move', 'rotate', 'pivot', 'axes', 'snap', 'add', 'duplicate',
    'delete', 'group', 'undo', 'redo', 'lighting', 'visibility', 'hidden',
    'lock', 'unlock', 'frame', 'save', 'exit', 'back', 'search', 'filter',
    'menu', 'close'
)
$buildCameraPath = Join-Path $profileRoot `
    'BepInEx\plugins\Azumatt-Build_Camera_Custom_Hammers_Edition\Build Camera.dll'

foreach ($path in @(
    $cecilPath,
    $hostPath,
    $utilsPath,
    $pluginPath,
    $geometryPath,
    $pluginSourcePath,
    $sessionSourcePath,
    $hudSourcePath,
    $blueprintRegistrySourcePath,
    $catalogSourcePath,
    $blueprintStoreSourcePath,
    $thumbnailSourcePath,
    $previewSourcePath,
    $midpointSourcePath,
    $editorDocumentSourcePath,
    $editorSceneSourcePath,
    $editorViewSourcePath,
    $editorControllerSourcePath,
    $gizmoSourcePath,
    $editorIconLibrarySourcePath)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required host-contract input is missing: $path"
    }
}
foreach ($iconName in $requiredEditorIcons) {
    $iconPath = Join-Path $editorIconRoot ($iconName + '.png')
    if (-not (Test-Path -LiteralPath $iconPath -PathType Leaf)) {
        throw "Required Blueprint Editor icon is missing: $iconPath"
    }
}

Add-Type -Path $cecilPath
Add-Type -AssemblyName System.Drawing.Common

function Assert-Contract {
    param(
        [Parameter(Mandatory = $true)]
        [bool] $Condition,
        [Parameter(Mandatory = $true)]
        [string] $Message
    )
    if (-not $Condition) {
        throw $Message
    }
}

foreach ($iconName in $requiredEditorIcons) {
    $iconPath = Join-Path $editorIconRoot ($iconName + '.png')
    $image = $null
    $bitmap = $null
    try {
        $image = [Drawing.Image]::FromFile($iconPath)
        $bitmap = [Drawing.Bitmap]::new($image)
        [void] $bitmap.GetPixel(0, 0)
        Assert-Contract ($bitmap.Width -eq 128 -and $bitmap.Height -eq 128) `
            "Blueprint Editor icon must be 128x128: $iconPath"
        Assert-Contract ([Drawing.Image]::IsAlphaPixelFormat($bitmap.PixelFormat)) `
            "Blueprint Editor icon has no alpha channel: $iconPath"
    } catch {
        throw "Blueprint Editor icon cannot be decoded: $iconPath. $($_.Exception.Message)"
    } finally {
        if ($null -ne $bitmap) { $bitmap.Dispose() }
        if ($null -ne $image) { $image.Dispose() }
    }
}

$pluginSource = Get-Content -LiteralPath $pluginSourcePath -Raw
$sessionSource = Get-Content -LiteralPath $sessionSourcePath -Raw
$hudSource = Get-Content -LiteralPath $hudSourcePath -Raw
$blueprintRegistrySource = Get-Content -LiteralPath $blueprintRegistrySourcePath -Raw
$catalogSource = Get-Content -LiteralPath $catalogSourcePath -Raw
$blueprintStoreSource = Get-Content -LiteralPath $blueprintStoreSourcePath -Raw
$thumbnailSource = Get-Content -LiteralPath $thumbnailSourcePath -Raw
$previewSource = Get-Content -LiteralPath $previewSourcePath -Raw
$midpointSource = Get-Content -LiteralPath $midpointSourcePath -Raw
$editorDocumentSource = Get-Content -LiteralPath $editorDocumentSourcePath -Raw
$editorSceneSource = Get-Content -LiteralPath $editorSceneSourcePath -Raw
$editorViewSource = Get-Content -LiteralPath $editorViewSourcePath -Raw
$editorControllerSource = Get-Content -LiteralPath $editorControllerSourcePath -Raw
$gizmoSource = Get-Content -LiteralPath $gizmoSourcePath -Raw
$editorIconLibrarySource = Get-Content -LiteralPath $editorIconLibrarySourcePath -Raw
Assert-Contract ($editorIconLibrarySource -match 'GetManifestResourceStream' -and
    $editorIconLibrarySource -match 'ImageConversionModule' -and
    $editorIconLibrarySource -match 'Sprite\.Create' -and
    $editorIconLibrarySource -match 'if \(sprite\) UnityEngine\.Object\.Destroy\(sprite\)' -and
    ([regex]::Matches($editorIconLibrarySource,
        'if \(texture && !resourceTextures\.Contains\(texture\)\) UnityEngine\.Object\.Destroy\(texture\)')).Count -eq 2 -and
    $editorIconLibrarySource -match 'resourceTextures\.Add\(resource\)' -and
    $editorIconLibrarySource -match 'resourceTextures\.Clear\(\)' -and
    $editorIconLibrarySource -match 'failures\.Add\(iconName\)') `
    'Blueprint Editor icon loader must release owned sprites/textures and preserve borrowed Resources textures.'
Assert-Contract ($editorViewSource -match 'CreateIconButton' -and
    $editorViewSource -match '"select", "↖"' -and
    $editorViewSource -match 'BlueprintEditorTool\.Transform' -and
    $editorViewSource -match 'BlueprintEditorTool\.Array' -and
    $editorViewSource -match 'BlueprintEditorTool\.Contour' -and
    ([regex]::Matches($editorViewSource, 'AddTool\(railContent')).Count -eq 4 -and
    $editorViewSource -match '"visibility" : "hidden"' -and
    $editorViewSource -match '"lock" : "unlock"') `
    'Blueprint Editor controls no longer consume the production icon set.'
Assert-Contract ($editorViewSource -match
    'ISelectHandler, IDeselectHandler' -and
    $editorViewSource -match
    '"Undo", top, "undo", "↶".*UndoRequested' -and
    $editorViewSource -match
    '"Redo", top, "redo", "↷".*RedoRequested' -and
    $editorViewSource -match
    'hover\.Initialize\(tooltipValue, BeginTooltip, EndTooltip\)' -and
    $editorViewSource -match
    'button\.transform\.Find\("Icon"\)' -and
    $editorViewSource -match
    'button\.interactable \? TextColor : MutedColor') `
    'Blueprint Editor icon tooltip, callback, focus, or disabled-state wiring regressed.'
Assert-Contract ($editorDocumentSource -notmatch
    'UnityEngine|\bPlayer\b|\bHud\b|\bZDO\b|\bGameObject\b') `
    'Blueprint editor document leaked a world, HUD, network, or Unity dependency.'
Assert-Contract ($editorDocumentSource -match 'SiblingOrder' -and
    $editorDocumentSource -match 'SetSelectionGroup' -and
    $editorDocumentSource -match 'UngroupSelection' -and
    $editorDocumentSource -match 'SetSelectionVisibility' -and
    $editorDocumentSource -match 'SetSelectionLocked' -and
    $editorDocumentSource -match 'ShowAll' -and
    $editorDocumentSource -match 'SetMetadata' -and
    $editorDocumentSource -match 'AcceptLastEdit' -and
    $editorDocumentSource -match 'public string Name;' -and
    $editorDocumentSource -match 'public string Category;') `
    'Blueprint editor document is missing atomic Outliner, metadata, or rollback state.'
Assert-Contract ($editorDocumentSource -match 'public string PivotPartId' -and
    $editorDocumentSource -match 'public bool SetGroupPivot' -and
    $editorDocumentSource -match 'ClearInvalidGroupPivots' -and
    $editorDocumentSource -match 'partIds\.TryGetValue\(pivotPartId' -and
    $editorControllerSource -match 'pivotPartId = group\.PivotPartId' -and
    $editorViewSource -match 'ОПОРА ЧЕРТЕЖА' -and
    $editorViewSource -match 'ОПОРА ГРУППЫ') `
    'Blueprint world/group anchor hierarchy or copied-pivot remapping is missing.'
Assert-Contract (([regex]::Matches($sessionSource,
        'TryAddBlueprintLayoutInstance\(')).Count -eq 3 -and
    $sessionSource -match 'pivotPosition - rotation \* \(blueprintPivotLocal \* uniformScale\)' -and
    $sessionSource -match 'blueprintPivotLocal = ToUnity\(bounds\.Center\)' -and
    $sessionSource -match 'currentPosition - currentRotation \* blueprintPivotLocal' -and
    $sessionSource -match 'blueprintRootPosition = snapshot\.Position' -and
    $sessionSource -match 'baseSnapshot = CaptureHistorySnapshot\(\);\s*history\.Reset\(baseSnapshot\);') `
    'F9 Repeat/Contour pivot-to-root conversion or center-pivot baseline is missing.'
Assert-Contract ($editorControllerSource -match
        'ArrayFrame\(out Vector3 pivot, out Vector3 stepX' -and
    $editorControllerSource -match
        'TrySelectionPivot\(out pivot, out Quaternion orientation, gizmoIds\)' -and
    $editorControllerSource -match 'ToPoint\(front\), ToPoint\(pivot\)' -and
    $editorDocumentSource -match 'Point3\? pivot = null' -and
    $editorDocumentSource -match 'Point3 pivot = requestedPivot \?\? default') `
    'Editor Array no longer shares the resolved group pivot with preview and apply.'
Assert-Contract ($editorControllerSource -match
        'placementItem != null &&\s*\(input\.GetKeyDown\(KeyCode\.G\)' -and
    $editorControllerSource -match
        'placementYaw \+= input\.MouseScrollDelta\.y > 0f \? 22\.5f : -22\.5f' -and
    $editorControllerSource -match 'CyclePlacementSnapPoint\(-1\)' -and
    $editorControllerSource -match 'CyclePlacementSnapPoint\(1\)' -and
    $editorControllerSource -match 'placementManualSnapPoint' -and
    $editorSceneSource -match 'manualSnapPoint == sourceIndex' -and
    $editorControllerSource -match
        'placementControl && input\.GetKeyDown\(KeyCode\.Z\)' -and
    $editorControllerSource -match
        'placementControl && input\.GetKeyDown\(KeyCode\.Y\)' -and
    $editorControllerSource -match 'private void DeleteViewportPart\(string stableId\)' -and
    $editorControllerSource -match
        '!document\.IsPartSelected\(document\.PrimaryPartId\)' -and
    $editorViewSource -match
        'HideCatalog\(\)\s*\{[^}]*SetSelectedGameObject\(null\)') `
    'Blueprint Editor world-style placement input or multi-group world pivot regressed.'
Assert-Contract ($editorSceneSource -match 'HideFlags\.HideAndDontSave' -and
    $editorSceneSource -match 'cameraObject\.AddComponent<Camera>\(\)' -and
    $editorSceneSource -match 'result\.cullingMask = 1 << editorLayer' -and
    $editorSceneSource -match 'UnityEngine\.Object\.Destroy\(root\)') `
    'Blueprint editor scene does not own an isolated camera and disposable root.'
Assert-Contract ($editorSceneSource -notmatch
    '\bCollider\b|Physics\.') `
    'Blueprint editor scene can still participate in world physics.'
Assert-Contract ($editorSceneSource -match 'BlueprintEditorRenderIsolation' -and
    $editorSceneSource -match 'OnPreCull' -and
    $editorSceneSource -match 'OnPostRender' -and
    $editorSceneSource -match 'OnDisable' -and
    $editorSceneSource -match 'OnDestroy' -and
    $editorSceneSource -match 'RestoreNow' -and
    $editorSceneSource -match 'DefaultReflectionMode\.Custom' -and
    $editorSceneSource -match 'customReflectionTexture = null' -and
    $editorSceneSource -match 'customReflectionTexture = customReflection' -and
    $editorSceneSource -match 'CreateStudioLight' -and
    $editorSceneSource -match 'Resources\.FindObjectsOfTypeAll<Component>' -and
    $editorSceneSource -match 'component\.gameObject\.scene\.IsValid\(\)' -and
    $editorSceneSource -match 'component is Renderer \|\| component is CanvasRenderer' -and
    $editorSceneSource -match 'UnityEngine\.Terrain' -and
    $editorSceneSource -match 'UnityEngine\.Projector' -and
    $editorSceneSource -match 'UnityEngine\.VFX\.VisualEffect' -and
    $editorSceneSource -match 'for \(int layer = 31; layer >= 8; --layer\)' -and
    $editorSceneSource -notmatch 'LayerMask\.LayerToName' -and
    $editorSceneSource -match 'EnsureWorldCamerasExcludeEditorLayer' -and
    $editorSceneSource -match 'Resources\.FindObjectsOfTypeAll<Camera>' -and
    $editorSceneSource -match 'entry\.Key\.cullingMask == withoutEditor' -and
    $editorSceneSource -match 'entry\.Key\.cullingMask \| \(entry\.Value & editorMask\)') `
    'Blueprint editor scene does not scope studio lighting to its camera safely.'
Assert-Contract ($editorSceneSource -match 'sourceMaterials\.Length == 0' -and
    $editorSceneSource -match 'DestroyVisual\(visual\)' -and
    $editorSceneSource -match 'visual\.OwnedAssets' -and
    $editorSceneSource -match 'Sprites/Default' -and
    $editorSceneSource -match 'overlay\s*\?\s*FirstSupportedShader\("Hidden/Internal-Colored"\)' -and
    $editorSceneSource -match 'transparent \|\| overlay \? 0 : 1') `
    'Blueprint editor preview can leak assets, hide placeholders, or break ground depth.'
Assert-Contract ($editorSceneSource -match 'CapturePickSurfaces' -and
    $editorSceneSource -match 'TryMeshHit' -and
    $editorSceneSource -match 'TryTriangleHit' -and
    $editorSceneSource -match 'BlueprintEditorMeshData\.TryRead' -and
    $editorSceneSource -match 'MeshFilter' -and
    $previewSource -match 'SkinnedMeshRenderer' -and $previewSource -match 'BakeMesh' -and
    $editorSceneSource -notmatch 'TryBounds\(visual, out Bounds bounds\)[\s\S]{0,160}stableId = entry\.Key') `
    'Blueprint editor picking fell back to renderer bounds instead of visible mesh triangles.'
$meshDataSource = Get-Content (Join-Path $projectRoot 'src\BuildWorks\BlueprintEditorMeshData.cs') -Raw
Assert-Contract ($meshDataSource -match 'mesh\.isReadable' -and
    $meshDataSource -match 'Mesh\.AcquireReadOnlyMeshData' -and
    $meshDataSource -match 'data\.GetVertices' -and $meshDataSource -match 'data\.GetIndices' -and
    $meshDataSource -match 'GetVertexBuffer' -and $meshDataSource -match 'GetIndexBuffer' -and
    $meshDataSource -match 'AsyncGPUReadback\.Request') `
    'Blueprint mesh picker must support both readable and GPU-only geometry.'
# Catalogue items carry the selected blueprint as data; the view still cannot own a store.
Assert-Contract (($editorViewSource -replace 'CompositeBlueprintStore\.Blueprint', 'BlueprintPayload') -notmatch
    '\bPlayer\b|\bHud\b|\bZDO\b|CompositeBlueprintStore|PrecisionPlacementSession') `
    'Blueprint editor view leaked a world, store, or precision-session dependency.'
Assert-Contract ($editorViewSource -match 'Screen\.safeArea' -and
    $editorViewSource -match 'BlueprintEditorLayout\.Compute' -and
    $editorViewSource -match 'OutlinerRowHeight' -and
    $editorViewSource -match 'ScaleMode\.ScaleWithScreenSize' -and
    $editorViewSource -match 'referenceResolution' -and
    $editorViewSource -match 'SetUiScale' -and
    $editorViewSource -match 'catalogSearch' -and
    $editorViewSource -match 'RebuildCatalog' -and
    $editorViewSource -match 'BlueprintEditorLayout\.CatalogPageSize' -and
    $editorViewSource -match 'RebuildCatalogFilters' -and
    $editorViewSource -match 'selectedCatalogMaterial' -and
    $editorViewSource -match 'selectedCatalogSource' -and
    $editorViewSource -match 'catalogSources' -and
    $editorViewSource -match 'catalogCategoryTabs' -and
    $editorViewSource -match 'Matches\("#" \+ \(item\.Index \+ 1\), filter\)' -and
    $editorViewSource -match 'CatalogPartSelected\?\.Invoke\(captured, true\)' -and
    $editorViewSource -notmatch 'CatalogDetails|CatalogAdd|CatalogDoubleClick' -and
    $editorViewSource -match 'collapsedGroups' -and
    $editorViewSource -match 'MoveSelectionToGroupRequested' -and
    $editorViewSource -match 'inspectorTabButtons' -and
    $editorViewSource -match 'InspectorSubmitted' -and
    $editorViewSource -match 'BlueprintMetadataSubmitted' -and
    $editorViewSource -match 'SelectionVisibilityRequested' -and
    $editorViewSource -match 'SelectionLockRequested' -and
    $editorViewSource -match 'inspectorDelta' -and
    $editorViewSource -match 'ArrayChanged' -and
    $editorViewSource -match 'ApplyArrayRequested' -and
    $editorViewSource -match 'arrayEditPanel' -and
    $editorViewSource -match 'ResetTransformRequested' -and
    $editorViewSource -match 'CreateRect\("InspectorViewport"' -and
    $editorViewSource -match 'inspectorViewport\.gameObject\.AddComponent<RectMask2D>' -and
    $editorViewSource -notmatch 'inspectorScroll' -and
    $editorViewSource -match 'CreateRect\(\s*"CatalogCategoryViewport"' -and
    $editorViewSource -match 'categoryScroll\.content = catalogCategories' -and
    $editorViewSource -match 'catalogPanel\.localScale' -and
    $editorViewSource -match 'width \* 0\.94 / 1420\.0' -and
    $editorViewSource -match 'IsTextInputFocused' -and
    $editorViewSource -match 'CancelTextEdit' -and
    $editorViewSource -match 'catch\s*\{\s*Dispose\(\);\s*throw;' -and
    $editorViewSource -notmatch 'Math\.Max\(640\.0, safeRoot\.rect\.width\)' -and
    $editorViewSource -notmatch 'Math\.Max\(480\.0, safeRoot\.rect\.height\)' -and
    $editorViewSource -notmatch 'SetAnchored\(') `
    'Blueprint editor view bypasses the safe-area layout contract.'
Assert-Contract ($editorControllerSource -notmatch
    '\bPlayer\b|\bHud\b|\bZDO\b|PrecisionPlacementSession|SelectPiece|UpdatePlacementGhost') `
    'Blueprint editor controller leaked a world placement dependency.'
Assert-Contract (([regex]::Matches($editorControllerSource,
        'view\.DuplicateSelectionRequested \+=')).Count -eq 1 -and
    ([regex]::Matches($editorControllerSource,
        'view\.DeleteSelectionRequested \+=')).Count -eq 1 -and
    ([regex]::Matches($editorControllerSource,
        'view\.PrimaryPartRequested \+=')).Count -eq 1) `
    'Outliner context actions must have exactly one controller subscription.'
Assert-Contract ($editorControllerSource -match 'OpenNew' -and
    $editorControllerSource -match 'OpenExisting' -and
    $editorControllerSource -match 'TrySave' -and
    $editorControllerSource -match 'TrySaveDocument' -and
    $editorControllerSource -match 'TryUpdateDocument' -and
    $editorControllerSource -match 'BuildStoreGroups' -and
    $editorControllerSource -match 'AcceptLastEdit' -and
    $editorControllerSource -match 'RollbackLastEdit' -and
    $editorControllerSource -match 'part\.StableId == document\.ActiveNodeId' -and
    $editorControllerSource -match 'orientation \* rotation \* Quaternion\.Inverse\(orientation\)' -and
    $editorControllerSource -match 'view\.BlueprintMetadataSubmitted' -and
    $editorControllerSource -match 'view\.ArrayChanged' -and
    $editorControllerSource -match 'ModifierDocument\(\)\.PreviewArray\(' -and
    $editorControllerSource -match 'document\.ApplyArray\(' -and
    $editorControllerSource -match 'scene\.HideGizmo\(\)' -and
    $editorControllerSource -match 'view\.ResetTransformRequested' -and
    $editorControllerSource -match 'ResetSelectionTransform' -and
    $editorControllerSource -match 'Quaternion\.Inverse\(orientation\)' -and
    $editorControllerSource -match 'BlueprintEditorPivotMode\.Bounds' -and
    $editorControllerSource -match 'scene\.EnsureWorldCamerasExcludeEditorLayer\(\)' -and
    $editorControllerSource -match 'HideGameCanvases\(view\.RootCanvas\)' -and
    $editorControllerSource -match 'candidate\.gameObject\.layer == scene\.Layer' -and
    $editorControllerSource -match 'hiddenGameCanvases\.Contains\(candidate\)' -and
    $editorControllerSource -match 'LateUpdate\(\)[\s\S]*?HideGameCanvases\(view\.RootCanvas\)' -and
    $editorControllerSource -match 'RestoreGameCanvases\(\)' -and
    $editorControllerSource -match 'scene\.HitTestAnchor' -and
    $editorControllerSource -match 'scene\.TryFindEditorSnapTarget' -and
    $editorControllerSource -match 'scene\.ShowSnapCandidates' -and
    $editorControllerSource -match 'view\.AnchorVisibilityRequested' -and
    $editorControllerSource -match 'view\.MeshSnapRequested' -and
    $editorControllerSource -match 'snapTargetIsNative' -and
    $editorSceneSource -match 'MaximumSnapPreviewTargets = 24' -and
    $editorSceneSource -match 'nativeAnchorStart' -and
    $editorViewSource -match 'ТОЧКИ: ВСЕ' -and
    $editorViewSource -match 'МАГНИТ: ИГРА' -and
    $gizmoSource -match 'camera\.cullingMask' -and
    $editorControllerSource -match 'if \(IsGizmoDragging\)[\s\S]*?return false;' -and
    $editorControllerSource -match '!IsGizmoDragging &&\s*input\.GetMouseButtonDown\(1\)' -and
    $editorSceneSource -match 'allowMove: true' -and
    $editorSceneSource -match 'allowRotate: true' -and
    $editorControllerSource -match 'TryGetDocumentBounds' -and
    $editorControllerSource -match 'TryGetSelectionBounds' -and
    $editorControllerSource -match 'KeyCode\.Home' -and
    $editorControllerSource -match 'view\.IsTextInputFocused' -and
    $editorControllerSource -match 'BlocksWorldInput' -and
    $editorControllerSource -match 'Time\.frameCount <= blockWorldInputThroughFrame' -and
    $editorControllerSource -match 'Cleanup' -and
    $editorControllerSource -match 'Cursor\.lockState') `
    'Blueprint editor controller lifecycle or save path is incomplete.'
Assert-Contract ($previewSource -match 'SkinnedMeshRenderer' -and
    $previewSource -match 'BakeMesh' -and
    $previewSource -match 'VisualCloneOwnedAssets' -and
    $previewSource -match 'owned\.Track\(baked\)') `
    'Visual clone does not preserve skinned-only prefab geometry safely.'
Assert-Contract ($pluginSource -match
        'if \(blueprintEditor\?\.BlocksWorldInput == true\)[\s\S]{0,200}blueprintEditor\.IsOpen[\s\S]{0,80}blueprintEditor\.Update\(\);[\s\S]{0,80}return;' -and
    $pluginSource -match
        'private void LateUpdate\(\)[\s\S]{0,240}blueprintEditor\?\.BlocksWorldInput == true[\s\S]{0,160}session\?\.LateUpdate\(\);' -and
    $pluginSource -match
        'if \(instance\?\.blueprintEditor\?\.BlocksWorldInput == true\) return false;' -and
    $pluginSource -match
        'if \(instance\?\.blueprintEditor\?\.BlocksWorldInput == true\) return true;' -and
    $pluginSource -match 'current\.SuspendForBlueprintEditor\(\)' -and
    $sessionSource -match 'internal void SuspendForBlueprintEditor\(\)') `
    'Blueprint editor is not a hard runtime branch from world input.'
Assert-Contract ($sessionSource -match
    'state == PlacementState\.Armed && requestAutomaticPlacement &&\s*!automaticPlacementRunning') `
    'The first manual placement is blocked together with queued automatic copies.'

$hostAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($hostPath)
$utilsAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($utilsPath)
$player = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'Player'
Assert-Contract ($null -ne $player) 'Player type is missing.'
$placementStatus = $player.NestedTypes | Where-Object Name -eq 'PlacementStatus'
$blockedByPlayer = $placementStatus.Fields | Where-Object Name -eq 'BlockedbyPlayer'
Assert-Contract ($null -ne $blockedByPlayer -and
    [int]$blockedByPlayer.Constant -eq 2) `
    'Player-blocked placement status changed.'
$humanoid = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'Humanoid'
Assert-Contract ($null -ne $humanoid) 'Humanoid type is missing.'
$itemDrop = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'ItemDrop'
Assert-Contract ($null -ne $itemDrop) 'ItemDrop type is missing.'
$itemData = $itemDrop.NestedTypes | Where-Object Name -eq 'ItemData'
Assert-Contract ($null -ne $itemData) 'ItemDrop.ItemData type is missing.'
$sharedData = $itemData.NestedTypes | Where-Object Name -eq 'SharedData'
Assert-Contract ($null -ne $sharedData) 'ItemDrop.ItemData.SharedData type is missing.'
$gameCamera = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'GameCamera'
Assert-Contract ($null -ne $gameCamera) 'GameCamera type is missing.'
$playerController = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'PlayerController'
Assert-Contract ($null -ne $playerController) 'PlayerController type is missing.'
$pieceType = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'Piece'
Assert-Contract ($null -ne $pieceType) 'Piece type is missing.'
$pieceTableType = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'PieceTable'
Assert-Contract ($null -ne $pieceTableType) 'PieceTable type is missing.'
$hudType = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'Hud'
Assert-Contract ($null -ne $hudType) 'Hud type is missing.'
$buildUiType = $hostAssembly.MainModule.Types | Where-Object FullName -eq 'BuildUi'
Assert-Contract ($null -ne $buildUiType) 'Valheim 1.0 BuildUi type is missing.'
$buildUiPieceButtonType = $hostAssembly.MainModule.Types |
    Where-Object FullName -eq 'BuildUiPieceButton'
Assert-Contract ($null -ne $buildUiPieceButtonType -and
    @($buildUiPieceButtonType.Fields | Where-Object {
        $_.Name -eq 'm_favoriteStar' -and
        $_.FieldType.FullName -eq 'UnityEngine.UI.Image'
    }).Count -eq 1) 'Valheim 1.0 native favorite-star field changed.'
Assert-Contract (@($buildUiType.Fields | Where-Object {
    $_.Name -eq 'm_pieceButtons' -and
    $_.FieldType.FullName -eq
        'System.Collections.Generic.List`1<BuildUiPieceButton>'
}).Count -eq 1) 'Valheim 1.0 favorite-button pool changed.'
foreach ($callbackName in @('OnFavoritePieceAdded', 'OnFavoritePieceRemoved')) {
    Assert-Contract (@($buildUiType.Methods | Where-Object {
        $_.Name -eq $callbackName -and $_.IsPrivate -and
        $_.Parameters.Count -eq 1 -and
        $_.Parameters[0].ParameterType.FullName -eq 'System.String'
    }).Count -eq 1) "Valheim 1.0 favorite callback changed: $callbackName"
}
Assert-Contract (@($buildUiPieceButtonType.Properties | Where-Object {
        $_.Name -eq 'Piece' -and $_.PropertyType.FullName -eq 'Piece'
    }).Count -eq 1 -and
    @($buildUiPieceButtonType.Methods | Where-Object {
        $_.Name -eq 'RefreshFavorite' -and $_.IsPublic -and $_.Parameters.Count -eq 0
    }).Count -eq 1) 'Valheim 1.0 favorite-button refresh API changed.'
$availableByCategory = @($pieceTableType.Fields | Where-Object {
    $_.Name -eq 'm_availablePiecesByCategory' -and
    $_.FieldType.FullName -eq
        'System.Collections.Generic.List`1<System.Collections.Generic.List`1<Piece>>'
})
Assert-Contract ($availableByCategory.Count -eq 1) `
    'Valheim 1.0 PieceTable category storage changed.'
$availablePieces = @($pieceTableType.Fields | Where-Object {
    $_.Name -eq 'm_availablePieces' -and
    $_.FieldType.FullName -eq 'System.Collections.Generic.HashSet`1<Piece>'
})
Assert-Contract ($availablePieces.Count -eq 1) `
    'Valheim 1.0 BuildUi available-piece storage changed.'
foreach ($fieldName in @('m_tabContainer', 'm_tagListContainer', 'm_pieceView')) {
    Assert-Contract (@($buildUiType.Fields | Where-Object {
        $_.Name -eq $fieldName -and
        $_.FieldType.FullName -eq 'UnityEngine.RectTransform'
    }).Count -eq 1) "Valheim 1.0 BuildUi layout field changed: $fieldName"
}
foreach ($methodName in @('IsFavoritePiece', 'ToggleFavorite', 'RemoveFromFavorites')) {
    Assert-Contract (@($buildUiType.Methods | Where-Object {
        $_.Name -eq $methodName -and $_.IsPublic
    }).Count -eq 1) "Valheim 1.0 BuildUi favorite API changed: $methodName"
}
Assert-Contract (@($buildUiType.Fields | Where-Object {
    $_.Name -eq 'm_tabButtons' -and
    $_.FieldType.FullName -eq
        'System.Collections.Generic.List`1<UnityEngine.UI.Button>'
}).Count -eq 1) 'Valheim 1.0 BuildUi tab-button storage changed.'
$isPieceSelectionVisible = @($hudType.Methods | Where-Object {
    $_.Name -eq 'IsPieceSelectionVisible' -and $_.IsStatic -and
    $_.Parameters.Count -eq 0 -and $_.ReturnType.FullName -eq 'System.Boolean'
})
Assert-Contract ($isPieceSelectionVisible.Count -eq 1) `
    'Valheim 1.0 Hammer visibility API changed.'
$hudUpdate = $hudType.Methods | Where-Object {
    $_.Name -eq 'Update' -and $_.Parameters.Count -eq 0
}
$hudUpdateOperands = @(
    $hudUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($hudUpdateOperands -match 'Hud::UpdateBuild') `
    'Hud.Update no longer drives the per-frame Hammer visibility transition.'
Assert-Contract ($blueprintRegistrySource -match 'm_availablePiecesByCategory' -and
    $sessionSource -match 'm_availablePiecesByCategory' -and
    $blueprintRegistrySource -notmatch '"m_availablePieces"' -and
    $sessionSource -notmatch '"m_availablePieces"') `
    'BuildWorks still reads the pre-1.0 category list field.'
Assert-Contract ($pluginSource -match 'Hud\.IsPieceSelectionVisible\(\)' -and
    $pluginSource -match 'Hud\.CloseBuildUi\(\)' -and
    $catalogSource -match 'Hud\.IsPieceSelectionVisible\(\)' -and
    $sessionSource -match 'Hud\.IsPieceSelectionVisible\(\)') `
    'BuildWorks still assumes the pre-1.0 Hammer window lifecycle.'
Assert-Contract ($catalogSource -match
        'internal static bool NativeBuildUiActive => Hud\.instance && Hud\.instance\.m_buildUi' -and
    $catalogSource -match
        'private static bool NativeCatalogActive =>[\s\S]{0,120}!HammerCatalogView\.NativeIndexActive;' -and
    $catalogSource -match
        'if \(NativeCatalogActive\)[\s\S]{0,420}FullLists\[key\] = new List<Piece>\(pieces\);[\s\S]{0,220}RequestedPages\.Remove\(key\);' -and
    $catalogSource -match
        'if \(NativeCatalogActive\) return visible\.Contains\(piece\);' -and
    $catalogSource -match
        'public static bool NativeIndexActive => nativeIndexMode;' -and
    $blueprintRegistrySource -match 'table\.m_availablePieces\.Add\(blueprintPiece\)' -and
    $blueprintRegistrySource -match 'registeredTable\.m_availablePieces\.Remove\(piece\)' -and
    $catalogSource -match 'NativeIndexEntry' -and
    $catalogSource -match 'nativeBuildUiWasVisible' -and
    $catalogSource -match 'm_pieceSelectionWindow' -and
    $catalogSource -match 'EnterNativeIndex' -and
    $catalogSource -match 'ExitNativeIndex' -and
    $catalogSource -match 'hud\.m_pieceSelectionWindow\.SetActive\(true\)') `
    'Valheim 1.0 BuildUi blueprint visibility/switch adapter is incomplete.'
$zInput = $utilsAssembly.MainModule.Types | Where-Object FullName -eq 'ZInput'
Assert-Contract ($null -ne $zInput) 'ZInput type is missing.'

$update = @($player.Methods | Where-Object Name -eq 'UpdatePlacementGhost')
$tryPlace = @($player.Methods | Where-Object Name -eq 'TryPlacePiece')
$takeInput = @($player.Methods | Where-Object Name -eq 'TakeInput')
$updatePlacement = @($player.Methods | Where-Object Name -eq 'UpdatePlacement')
$noPlacementCost = @($player.Fields | Where-Object Name -eq 'm_noPlacementCost')
$lastToolUseTime = @($player.Fields | Where-Object Name -eq 'm_lastToolUseTime')
$placePressedTime = @($player.Fields | Where-Object Name -eq 'm_placePressedTime')
$manualSnapPoint = @($player.Fields | Where-Object Name -eq 'm_manualSnapPoint')
$buildPieces = @($player.Fields | Where-Object Name -eq 'm_buildPieces')
$getRightItem = @($humanoid.Methods | Where-Object {
    $_.Name -eq 'GetRightItem' -and $_.Parameters.Count -eq 0 -and
    $_.ReturnType.FullName -eq 'ItemDrop/ItemData'
})
$pieceSnapPoints = @($pieceType.Methods | Where-Object {
    $_.Name -eq 'GetSnapPoints' -and $_.Parameters.Count -eq 1 -and
    $_.Parameters[0].ParameterType.FullName -eq
        'System.Collections.Generic.List`1<UnityEngine.Transform>'
})
Assert-Contract ($update.Count -eq 1) 'UpdatePlacementGhost must have exactly one overload.'
Assert-Contract ($update[0].Parameters.Count -eq 1 -and
    $update[0].Parameters[0].ParameterType.FullName -eq 'System.Boolean') `
    'UpdatePlacementGhost(bool) signature changed.'
Assert-Contract ($tryPlace.Count -eq 1 -and
    $tryPlace[0].Parameters.Count -eq 1 -and
    $tryPlace[0].Parameters[0].ParameterType.FullName -eq 'Piece') `
    'TryPlacePiece(Piece) signature changed.'
Assert-Contract ($takeInput.Count -eq 1 -and
    $takeInput[0].Parameters.Count -eq 0 -and
    $takeInput[0].ReturnType.FullName -eq 'System.Boolean') `
    'Player.TakeInput() signature changed.'
Assert-Contract ($updatePlacement.Count -eq 1 -and
    $updatePlacement[0].Parameters.Count -eq 2 -and
    $updatePlacement[0].Parameters[0].ParameterType.FullName -eq 'System.Boolean' -and
    $updatePlacement[0].Parameters[1].ParameterType.FullName -eq 'System.Single') `
    'Player.UpdatePlacement(bool, float) signature changed.'
Assert-Contract ($noPlacementCost.Count -eq 1 -and
    $noPlacementCost[0].FieldType.FullName -eq 'System.Boolean') `
    'Player.m_noPlacementCost field changed.'
Assert-Contract ($lastToolUseTime.Count -eq 1 -and
    $lastToolUseTime[0].FieldType.FullName -eq 'System.Single') `
    'Player.m_lastToolUseTime field changed.'
Assert-Contract ($placePressedTime.Count -eq 1 -and
    $placePressedTime[0].FieldType.FullName -eq 'System.Single') `
    'Player.m_placePressedTime field changed.'
Assert-Contract ($manualSnapPoint.Count -eq 1 -and
    $manualSnapPoint[0].FieldType.FullName -eq 'System.Int32') `
    'Player.m_manualSnapPoint field changed.'
Assert-Contract ($buildPieces.Count -eq 1 -and
    $buildPieces[0].FieldType.FullName -eq 'PieceTable') `
    'Player.m_buildPieces field changed.'
foreach ($fieldName in @('m_pieces', 'm_canRemovePieces')) {
    Assert-Contract (@($pieceTableType.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "PieceTable field changed: $fieldName"
}
foreach ($fieldName in @('m_pieceCategoryTabs', 'm_pieceSelectionWindow',
    'm_pieceCategoryRoot', 'm_pieceListRoot', 'm_pieceIconPrefab')) {
    Assert-Contract (@($hudType.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "Hammer HUD field changed: $fieldName"
}
foreach ($methodName in @('OnLeftClickCategory', 'HidePieceSelection',
    'IsPieceSelectionVisible')) {
    Assert-Contract (@($hudType.Methods | Where-Object Name -eq $methodName).Count -eq 1) `
        "Hammer HUD method changed: $methodName"
}
Assert-Contract ($getRightItem.Count -eq 1) 'Humanoid.GetRightItem() signature changed.'
Assert-Contract ($pieceSnapPoints.Count -eq 1) `
    'Piece.GetSnapPoints(List<Transform>) signature changed.'
Assert-Contract (@($itemData.Fields | Where-Object {
    $_.Name -eq 'm_durability' -and $_.FieldType.FullName -eq 'System.Single'
}).Count -eq 1) 'ItemData.m_durability field changed.'
foreach ($fieldName in @('m_useDurability', 'm_buildPieces')) {
    Assert-Contract (@($sharedData.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "ItemData.SharedData.$fieldName field changed."
}
$getButtonDown = @($zInput.Methods | Where-Object {
    $_.Name -eq 'GetButtonDown' -and $_.Parameters.Count -eq 1 -and
    $_.Parameters[0].ParameterType.FullName -eq 'System.String' -and
    $_.ReturnType.FullName -eq 'System.Boolean'
})
Assert-Contract ($getButtonDown.Count -eq 1) 'ZInput.GetButtonDown(string) signature changed.'
$updatePlacementOperands = @(
    $updatePlacement[0].Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($updatePlacementOperands -match 'ZInput::GetButtonDown\(System.String\)' -and
    $updatePlacementOperands -match 'Player::HaveRequirements' -and
    $updatePlacementOperands -match 'Player::TryPlacePiece' -and
    $updatePlacementOperands -match 'Player::ConsumeResources') `
    'Player.UpdatePlacement no longer owns input, requirements, placement, and costs.'
$controllerTakeInput = @($playerController.Methods | Where-Object Name -eq 'TakeInput')
Assert-Contract ($controllerTakeInput.Count -eq 1 -and
    $controllerTakeInput[0].Parameters.Count -eq 1 -and
    $controllerTakeInput[0].Parameters[0].ParameterType.FullName -eq 'System.Boolean' -and
    $controllerTakeInput[0].ReturnType.FullName -eq 'System.Boolean') `
    'PlayerController.TakeInput(bool) signature changed.'
$updateCamera = @($gameCamera.Methods | Where-Object Name -eq 'UpdateCamera')
$updateMouseCapture = @($gameCamera.Methods | Where-Object Name -eq 'UpdateMouseCapture')
Assert-Contract ($updateCamera.Count -eq 1 -and
    $updateCamera[0].Parameters.Count -eq 1 -and
    $updateCamera[0].Parameters[0].ParameterType.FullName -eq 'System.Single') `
    'GameCamera.UpdateCamera(float) signature changed.'
Assert-Contract ($updateMouseCapture.Count -eq 1 -and
    $updateMouseCapture[0].Parameters.Count -eq 0) `
    'GameCamera.UpdateMouseCapture() signature changed.'

$instructions = @($update[0].Body.Instructions)
$rayCall = -1
$closestSnapCall = -1
$finalValidation = -1
for ($i = 0; $i -lt $instructions.Count; ++$i) {
    $operand = [string] $instructions[$i].Operand
    if ($rayCall -lt 0 -and $operand -match 'Player::PieceRayTest\(') { $rayCall = $i }
    if ($closestSnapCall -lt 0 -and
        $operand -match 'Player::FindClosestSnapPoints\(') { $closestSnapCall = $i }
    if ($finalValidation -lt 0 -and $operand -match 'Location::IsInsideNoBuildLocation\(') {
        $finalValidation = $i
    }
}

Assert-Contract ($rayCall -ge 6) 'PieceRayTest anchor changed.'
Assert-Contract ($instructions[$rayCall - 6].OpCode.Name -eq 'ldloca.s') `
    'PieceRayTest point-local layout changed.'
Assert-Contract ($instructions[$rayCall - 4].OpCode.Name -eq 'ldloca.s' -and
    $instructions[$rayCall - 4].Operand.VariableType.FullName -eq 'Piece') `
    'PieceRayTest aimed-piece local layout changed.'
$contactLocalTypes = @('UnityEngine.Vector3', 'UnityEngine.Vector3', 'Piece', 'Heightmap', 'UnityEngine.Collider')
for ($contactArgument = 0; $contactArgument -lt $contactLocalTypes.Count; ++$contactArgument) {
    $contactLoad = $instructions[$rayCall - 6 + $contactArgument]
    Assert-Contract ($contactLoad.OpCode.Name -eq 'ldloca.s' -and
        $contactLoad.Operand.VariableType.FullName -eq $contactLocalTypes[$contactArgument]) `
        "Native complete placement contact local changed: $contactArgument"
}
Assert-Contract ($instructions[$rayCall - 1].OpCode.Name -eq 'ldloc.2') `
    'Native PieceRayTest water-mask argument changed.'
Assert-Contract ($instructions[$rayCall + 1].OpCode.FlowControl.ToString() -eq 'Cond_Branch') `
    'PieceRayTest success branch layout changed.'
Assert-Contract ($closestSnapCall -ge 4 -and
    $instructions[$closestSnapCall - 4].OpCode.Name -eq 'ldloca.s' -and
    $instructions[$closestSnapCall - 3].OpCode.Name -eq 'ldloca.s' -and
    $instructions[$closestSnapCall + 1].OpCode.FlowControl.ToString() -eq 'Cond_Branch') `
    'Native source/target snap-pair layout changed.'
Assert-Contract ($finalValidation -gt $rayCall) 'Final location validation anchor changed.'

$lastRotation = -1
for ($i = 0; $i -lt $finalValidation; ++$i) {
    if ([string]$instructions[$i].Operand -match 'UnityEngine\.Transform::set_rotation\(') {
        $lastRotation = $i
    }
}
Assert-Contract ($lastRotation -gt $rayCall) 'Final placement-ghost rotation anchor changed.'

$tryInstructions = @($tryPlace[0].Body.Instructions)
$callsUpdate = $false
$callsPlace = $false
foreach ($instruction in $tryInstructions) {
    $operand = [string] $instruction.Operand
    $callsUpdate = $callsUpdate -or $operand -match 'Player::UpdatePlacementGhost\(System.Boolean\)'
    $callsPlace = $callsPlace -or $operand -match 'Player::PlacePiece\(Piece,UnityEngine.Vector3,UnityEngine.Quaternion,System.Boolean,System.Boolean\)'
}
Assert-Contract $callsUpdate 'TryPlacePiece no longer refreshes the placement ghost.'
Assert-Contract $callsPlace 'TryPlacePiece no longer places from an explicit transform.'
$placePiece = @($player.Methods | Where-Object Name -eq 'PlacePiece')
Assert-Contract ($placePiece.Count -eq 1) 'PlacePiece hook requires one native overload.'
Assert-Contract ($placePiece[0].Parameters.Count -eq 5 -and
    $placePiece[0].Parameters[0].ParameterType.FullName -eq 'Piece' -and
    $placePiece[0].Parameters[1].ParameterType.FullName -eq 'UnityEngine.Vector3' -and
    $placePiece[0].Parameters[2].ParameterType.FullName -eq 'UnityEngine.Quaternion' -and
    $placePiece[0].Parameters[3].ParameterType.FullName -eq 'System.Boolean' -and
    $placePiece[0].Parameters[4].ParameterType.FullName -eq 'System.Boolean') `
    'PlacePiece(Piece, Vector3, Quaternion, bool, bool) signature changed.'
$placeInstructions = @($placePiece[0].Body.Instructions)
$nativeInstantiates = @($placeInstructions | Where-Object {
    [string]$_.Operand -match 'Object::Instantiate<UnityEngine.GameObject>\(!!0,UnityEngine.Vector3,UnityEngine.Quaternion\)'
})
Assert-Contract ($nativeInstantiates.Count -eq 1) 'PlacePiece must instantiate exactly one native piece.'
$onPlaced = @($placeInstructions | Where-Object { [string]$_.Operand -match 'WearNTear::OnPlaced\(' })
Assert-Contract ($onPlaced.Count -eq 1 -and $onPlaced[0].Offset -gt $nativeInstantiates[0].Offset) `
    'Native scale hook must run before WearNTear.OnPlaced.'
$zNetView = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ZNetView'
$setScaleOperands = (($zNetView.Methods | Where-Object Name -eq 'SetLocalScale').Body.Instructions |
    ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($setScaleOperands -match 'ZNetView::m_syncInitialScale' -and
    $setScaleOperands -match 'ZNetView::IsOwner' -and $setScaleOperands -match 'ZNetView::SyncScale') `
    'Native scale persistence/ownership contract changed.'

$pluginResolver = [Mono.Cecil.DefaultAssemblyResolver]::new()
foreach ($directory in @(
    $ValheimManagedDir,
    (Join-Path $profileRoot 'BepInEx\core'),
    (Split-Path -Parent $pluginPath))) {
    $pluginResolver.AddSearchDirectory($directory)
}
$pluginReader = [Mono.Cecil.ReaderParameters]::new()
$pluginReader.AssemblyResolver = $pluginResolver
$plugin = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($pluginPath, $pluginReader)
$unresolvedHostMembers = @()
$checkedHostMembers = 0
foreach ($member in $plugin.MainModule.GetMemberReferences()) {
    $scopeName = $member.DeclaringType.Scope.Name
    if ($scopeName -notlike 'Unity*' -and
        $scopeName -notin @('assembly_valheim', 'assembly_utils', 'assembly_guiutils')) {
        continue
    }
    ++$checkedHostMembers
    try {
        if ($null -eq $member.Resolve()) {
            $unresolvedHostMembers += "$scopeName :: $member"
        }
    } catch {
        $unresolvedHostMembers += "$scopeName :: $member"
    }
}
Assert-Contract ($checkedHostMembers -gt 0) 'Compiled plugin has no host/Unity member references.'
Assert-Contract ($unresolvedHostMembers.Count -eq 0) `
    ("Compiled plugin has unresolved host/Unity members:`n" +
        (($unresolvedHostMembers | Select-Object -First 12) -join "`n"))
$pluginType = $plugin.MainModule.Types | Where-Object Name -eq 'BuildWorksPlugin'
$scalePatch = $pluginType.NestedTypes | Where-Object Name -eq 'NativePieceScalePlacementPatch'
$scaleWrapper = $scalePatch.Methods | Where-Object Name -eq 'InstantiatePiece'
$scaleWrapperCalls = @($scaleWrapper.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($scaleWrapperCalls -match 'Object::Instantiate<UnityEngine.GameObject>' -and
    $scaleWrapperCalls -match 'PrecisionPlacementSession::ApplyNativePlacementScale' -and
    $scaleWrapperCalls -match 'ZNetScene::Destroy' -and
    $scaleWrapperCalls -match 'TerrainModifier::SetTriggerOnPlaced' -and
    $scaleWrapper.Body.ExceptionHandlers.Count -gt 0) `
    'Native scale wrapper must clean up an instantiated piece on failure.'
$precisionSessionType = $plugin.MainModule.Types | Where-Object {
    $_.Name -eq 'PrecisionPlacementSession'
}
$applyPlacementScale = $precisionSessionType.Methods | Where-Object {
    $_.Name -eq 'ApplyNativePlacementScale'
}
$applyPlacementScaleOperands = @(
    $applyPlacementScale.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$cancelBlueprintPlacement = $precisionSessionType.Methods | Where-Object {
    $_.Name -eq 'CancelBlueprintPlacement'
}
$cancelBlueprintOperands = @(
    $cancelBlueprintPlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$tryRollbackBlueprintPiece = $precisionSessionType.Methods | Where-Object {
    $_.Name -eq 'TryRollbackBlueprintPiece'
}
$tryRollbackBlueprintOperands = @(
    $tryRollbackBlueprintPiece.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$cancelSession = $precisionSessionType.Methods | Where-Object {
    $_.Name -eq 'Cancel'
}
$cancelSessionOperands = @(
    $cancelSession.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($applyPlacementScaleOperands -match 'currentBlueprintPlacements' -and
    $applyPlacementScaleOperands -match
        'System.Collections.Generic.List`1<UnityEngine.GameObject>::Add' -and
    $cancelSessionOperands -match 'PrecisionPlacementSession::CancelBlueprintPlacement' -and
    $tryRollbackBlueprintOperands -match 'WearNTear::Remove' -and
    $tryRollbackBlueprintOperands -match 'Piece::DropResources' -and
    $tryRollbackBlueprintOperands -match 'ZNetScene::Destroy' -and
    $cancelBlueprintOperands -match 'currentBlueprintPlacements' -and
    $cancelBlueprintOperands -match 'pendingBlueprintRollbacks' -and
    $cancelBlueprintOperands -notmatch 'lastRegularPalettePiece' -and
    $sessionSource -match 'pendingBlueprintRollbacks' -and
    $sessionSource -match 'RetryPendingBlueprintRollbacks\(force: true\)' -and
    $sessionSource -match 'TryRollbackBlueprintPiece' -and
    $sessionSource -match 'pendingBlueprintRollbacks\.RemoveAt\(index\)' -and
    $sessionSource -match
        'item\.RemovalNotified = true;[\s\S]{0,180}OnRemoved\(\)' -and
    $sessionSource -match
        'item\.ResourcesDropped = true;[\s\S]{0,180}DropResources\(null\)' -and
    $sessionSource -match 'wear\.Remove\(true\)' -and
    $sessionSource -match
        'item\.WaitForNativeRemoval && Time\.unscaledTime < item\.ForceDestroyAt' -and
    $tryRollbackBlueprintOperands -match 'ZNetView::ClaimOwnership' -and
    $tryRollbackBlueprintPiece.Body.ExceptionHandlers.Count -gt 0) `
    'Incomplete blueprint placement is no longer tracked and rolled back atomically.'
Assert-Contract ($plugin.Name.Version.ToString() -eq '0.19.33.0') `
    "Unexpected BuildWorks artifact version: $($plugin.Name.Version)"
$pluginResourceNames = @($plugin.MainModule.Resources | ForEach-Object Name)
foreach ($iconName in $requiredEditorIcons) {
    $resourceName = 'OstrixMods.BuildWorks.Assets.BlueprintEditorIcons.' +
        $iconName + '.png'
    Assert-Contract ($pluginResourceNames -contains $resourceName) `
        "Compiled plugin is missing embedded editor icon: $resourceName"
}

function Get-AllPluginTypes {
    param([Mono.Cecil.TypeDefinition] $Type)
    $Type
    foreach ($nested in $Type.NestedTypes) {
        Get-AllPluginTypes $nested
    }
}

$pluginTypes = @()
foreach ($type in $plugin.MainModule.Types) {
    $pluginTypes += @(Get-AllPluginTypes $type)
}
$pluginMethods = @($pluginTypes | ForEach-Object Methods)
$geometry = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($geometryPath)
$anchorAdjustment = $geometry.MainModule.Types | Where-Object {
    $_.FullName -eq 'OstrixMods.BuildWorks.Geometry.AnchorAdjustment'
}
Assert-Contract ($null -ne $anchorAdjustment -and
    @($anchorAdjustment.Methods | Where-Object Name -eq 'ConnectableSnapEdges').Count -eq 1) `
    'Compiled geometry has no midpoint-neighbor policy.'
$requiredHooks = @(
    'ApplyPassiveAutoJoinFromNativeSnap',
    'ApplyPrecisionValidationPoint',
    'ApplyBlueprintValidationContact',
    'ApplyPrecisionTransformBeforeValidation',
    'ShouldRunNativeGhostUpdate',
    'UsePrecisionFreePlacement'
)
$transpiler = $pluginMethods | Where-Object {
    $_.Name -eq 'Transpiler' -and
    $_.DeclaringType.Name -eq 'TransformGhostBeforeNativeValidationPatch'
}
Assert-Contract ($null -ne $transpiler) 'Compiled placement transpiler is missing.'
$transpilerOperands = @($transpiler.Body.Instructions | ForEach-Object { [string]$_.Operand })
foreach ($hook in $requiredHooks) {
    Assert-Contract (@($pluginMethods | Where-Object Name -eq $hook).Count -eq 1) `
        "Compiled hook is missing or ambiguous: $hook"
    if ($hook -ne 'ShouldRunNativeGhostUpdate') {
        Assert-Contract ($transpilerOperands -contains $hook) `
            "Compiled transpiler does not reference hook: $hook"
    }
}
$passiveHook = $pluginMethods | Where-Object {
    $_.Name -eq 'ApplyPassiveAutoJoinFromNativeSnap' -and
    $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
Assert-Contract ($passiveHook.Parameters.Count -eq 4 -and
    $passiveHook.ReturnType.FullName -eq 'System.Boolean' -and
    $passiveHook.Parameters[1].ParameterType.FullName -eq 'UnityEngine.Transform&' -and
    $passiveHook.Parameters[2].ParameterType.FullName -eq 'UnityEngine.Transform&' -and
    $passiveHook.Parameters[3].ParameterType.FullName -eq 'Piece') `
    'Passive auto-join hook no longer receives mutable snap points and the aimed piece.'
$contactHook = $pluginMethods | Where-Object {
    $_.Name -eq 'ApplyBlueprintValidationContact' -and $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
$contactSignature = @('System.Boolean', 'Player', 'UnityEngine.Vector3&', 'UnityEngine.Vector3&',
    'Piece&', 'Heightmap&', 'UnityEngine.Collider&', 'System.Boolean')
Assert-Contract ($contactHook.ReturnType.FullName -eq 'System.Boolean' -and
    $contactHook.Parameters.Count -eq $contactSignature.Count) `
    'Blueprint contact must preserve native bool and receive all five mutable ray results.'
for ($contactArgument = 0; $contactArgument -lt $contactSignature.Count; ++$contactArgument) {
    Assert-Contract ($contactHook.Parameters[$contactArgument].ParameterType.FullName -eq $contactSignature[$contactArgument]) `
        "Blueprint contact hook parameter changed: $contactArgument"
}
$contactOperands = @($contactHook.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('TryPrepareBlueprintValidation', 'TryGetBounds', 'TryFindContact',
    'Character::GetEyePoint', 'Piece::m_extraPlacementDistance', 'Floating::GetLiquidLevel',
    'Floating::GetWaterLevel', 'GetComponentInParent<Piece>', 'GetComponent<Heightmap>')) {
    Assert-Contract ($contactOperands -match [regex]::Escape($contactRequirement)) `
        "Blueprint contact lost native context requirement: $contactRequirement"
}
Assert-Contract (@($contactHook.Body.Instructions | Where-Object {
    $_.OpCode.Name -eq 'stfld' -and [string]$_.Operand -match 'Piece::m_|Player::m_placementStatus'
}).Count -eq 0 -and $contactHook.Body.ExceptionHandlers.Count -gt 0) `
    'Blueprint contact must not modify Piece restrictions or force native placement status.'
$contactPrepare = $pluginMethods | Where-Object {
    $_.Name -eq 'TryPrepareBlueprintValidation' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$contactPrepareOperands = @($contactPrepare.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('activeBlueprint', 'allowNativeGhostUpdate', 'SupportsPrecisionPlacement',
    'PlacementSelectionStillValid', 'placementPlanPieces', 'placementPlanIndex',
    'Transform::SetPositionAndRotation', 'Transform::set_localScale')) {
    Assert-Contract ($contactPrepareOperands -match [regex]::Escape($contactRequirement)) `
        "Frozen contact preparation lost gate/pose requirement: $contactRequirement"
}
Assert-Contract ($contactPrepareOperands -notmatch 'ContextStillValid') `
    'Inactive invalid-contact ghost must still be revalidated on retry.'
$contactGeometry = $pluginTypes | Where-Object Name -eq 'BuildWorksPlacementValidation'
$contactSearch = $contactGeometry.Methods | Where-Object Name -eq 'TryFindContact'
$contactSearchOperands = @($contactSearch.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('Physics::RaycastNonAlloc', 'Physics::RaycastAll', 'Transform::IsChildOf',
    'Collider::get_attachedRigidbody', 'Vector3::Distance', 'ContactTolerance')) {
    Assert-Contract ($contactSearchOperands -match [regex]::Escape($contactRequirement)) `
        "Actual contact probes lost a surface/range/self exclusion: $contactRequirement"
}
Assert-Contract ($pluginSource -match 'contactInjection = code\.FindIndex\(instruction => instruction\.Calls\(rayTestMethod\)\) \+ 1' -and
    $pluginSource -match 'code\.InsertRange\(contactInjection, contactCode\)' -and
    $pluginSource -match 'contactInjection - 1 - argument') `
    'Full-contact hook must precede the native ray-success branch and replay its actual locals.'
$ghostPrefix = $pluginMethods | Where-Object {
    $_.Name -eq 'Prefix' -and
    $_.DeclaringType.Name -eq 'TransformGhostBeforeNativeValidationPatch'
}
$ghostPrefixOperands = @(
    $ghostPrefix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($null -ne $ghostPrefix -and
    $ghostPrefixOperands -match 'ShouldRunNativeGhostUpdate') `
    'Compiled ghost-freeze prefix does not reference ShouldRunNativeGhostUpdate.'

$requiredControlPatches = @(
    'PrecisionEditorPlayerInputPatch',
    'PrecisionEditorControllerInputPatch',
    'PrecisionEditorCameraPatch',
    'PrecisionEditorMouseCapturePatch',
    'ContinuousPlacementInputPatch'
)
foreach ($patchType in $requiredControlPatches) {
    Assert-Contract (@($pluginTypes | Where-Object Name -eq $patchType).Count -eq 1) `
        "Compiled control patch is missing or ambiguous: $patchType"
}
$continuousPrefix = $pluginMethods | Where-Object {
    $_.Name -eq 'Prefix' -and $_.DeclaringType.Name -eq 'ContinuousPlacementInputPatch'
}
$continuousOperands = @(
    $continuousPrefix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($continuousOperands -match 'PrepareNativePlacementUpdate') `
    'Compiled UpdatePlacement prefix does not route through BuildWorks.'
Assert-Contract (@($pluginTypes | Where-Object Name -eq 'AutomaticPlacementButtonPatch').Count -eq 0) `
    'Fragile synthetic ZInput placement patch is still compiled.'

Assert-Contract (@($pluginTypes | Where-Object Name -eq 'TransformGizmoView').Count -eq 1) `
    'Compiled anchored gizmo view is missing.'
Assert-Contract (@($pluginTypes | Where-Object Name -eq 'GizmoHandleKind').Count -eq 1) `
    'Compiled universal gizmo handle kind is missing.'
$editorScene = $pluginTypes | Where-Object Name -eq 'BlueprintEditorScene'
Assert-Contract (@($editorScene).Count -eq 1) `
    'Compiled independent blueprint editor scene is missing or ambiguous.'
$editorSceneReferences = @(foreach ($type in @(Get-AllPluginTypes $editorScene)) {
    foreach ($field in $type.Fields) { $field.FieldType.FullName }
    foreach ($method in $type.Methods) {
        $method.FullName
        if ($method.HasBody) {
            foreach ($variable in $method.Body.Variables) { $variable.VariableType.FullName }
            foreach ($instruction in $method.Body.Instructions) { [string] $instruction.Operand }
        }
    }
}) -join "`n"
Assert-Contract ($editorSceneReferences -notmatch
    '\bPlayer\b|\bHud\b|\bZDO\b|\bZNetView\b|\bWearNTear\b|\bRigidbody\b') `
    'Compiled blueprint editor scene leaked a player, HUD, network, or gameplay component dependency.'
foreach ($method in @(
    'TrySync', 'TryPick', 'BoxSelect', 'TryGetSelectionBounds',
    'TryFindEditorSnapTarget', 'ShowSnapCandidates', 'TryFindContourSupports',
    'ShowContourPreview',
    'ShowContourGuide', 'ClearContourPreview', 'SetLighting', 'Dispose')) {
    Assert-Contract (@($editorScene.Methods | Where-Object Name -eq $method).Count -eq 1) `
        "Compiled blueprint editor scene method is missing: $method"
}
$editorSceneDispose = $editorScene.Methods | Where-Object Name -eq 'Dispose'
$editorSceneDisposeOperands = @(
    $editorSceneDispose.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorSceneDisposeOperands -match 'RestoreNow' -and
    $editorSceneDisposeOperands -match 'ClearContourPreview' -and
    $editorSceneDisposeOperands -match 'DestroyVisual') `
    'Compiled blueprint editor scene does not restore lighting or release visual assets.'
$editorContourDiscovery = $editorScene.Methods | Where-Object Name -eq 'TryFindContourSupports'
$editorContourDiscoveryOperands = @(
    $editorContourDiscovery.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorContourDiscoveryOperands -match 'TrySelectEditorContourEdge' -and
    $editorContourDiscoveryOperands -match 'ConstructionLayout::OrderConnectedContour' -and
    $editorContourDiscoveryOperands -match 'ConstructionLayout::OrderTouchingContour' -and
    $editorContourDiscoveryOperands -match 'BlueprintEditorDocument::IsPartSelected') `
    'Blueprint editor contour no longer finds an isolated support chain.'
$editorContourEdge = $editorScene.Methods |
    Where-Object Name -eq 'TrySelectEditorContourEdge'
$editorContourEdgeOperands = @(
    $editorContourEdge.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorContourEdgeOperands -match 'AnchorAdjustment::ConnectableSnapEdges' -and
    $editorContourEdgeOperands -match 'Camera::WorldToScreenPoint' -and
    $editorContourEdgeOperands -match 'AnchorAdjustment::PerspectiveSegmentParameter') `
    'Blueprint editor contour no longer follows a visible native snap edge.'
$editorView = $pluginTypes | Where-Object Name -eq 'BlueprintEditorView'
Assert-Contract (@($editorView).Count -eq 1) `
    'Compiled independent blueprint editor view is missing or ambiguous.'
foreach ($method in @('Tick', 'Bind', 'ShowUnsavedDialog', 'Dispose')) {
    Assert-Contract (@($editorView.Methods | Where-Object Name -eq $method).Count -eq 1) `
        "Compiled blueprint editor view method is missing: $method"
}
$editorController = $pluginTypes | Where-Object Name -eq 'BlueprintEditorController'
Assert-Contract (@($editorController).Count -eq 1) `
    'Compiled independent blueprint editor controller is missing or ambiguous.'
foreach ($method in @(
    'OpenNew', 'OpenExisting', 'AddPart', 'Update', 'LateUpdate', 'Close', 'Dispose',
    'ToggleAnchorVisibility', 'ToggleMeshSnap')) {
    Assert-Contract (@($editorController.Methods | Where-Object Name -eq $method).Count -eq 1) `
        "Compiled blueprint editor controller method is missing: $method"
}
$previewEditorArray = $editorController.Methods | Where-Object Name -eq 'PreviewArray'
$previewEditorArrayOperands = @(
    $previewEditorArray.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($previewEditorArrayOperands -match 'BlueprintEditorDocument::PreviewArray' -and
    $previewEditorArrayOperands -match 'ShowArrayPreview') `
    'Blueprint editor array no longer builds a document preview.'
$applyEditorArray = $editorController.Methods | Where-Object Name -eq 'ApplyArray'
$applyEditorArrayOperands = @(
    $applyEditorArray.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorControllerSource -match 'document\.ApplyArray\(' -and
    $applyEditorArrayOperands -match 'SetActiveTool') `
    'Blueprint editor array no longer commits once and returns to transform.'
$selectEditorContour = $editorController.Methods |
    Where-Object Name -eq 'SelectContourSupport'
$selectEditorContourOperands = @(
    $selectEditorContour.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($selectEditorContourOperands -match 'TryFindContourSupports' -and
    $selectEditorContourOperands -match 'BlueprintEditorDocument::PreviewContour' -and
    $selectEditorContourOperands -match 'ShowContourPreview') `
    'Blueprint editor contour no longer builds the document preview from scene supports.'
$applyEditorContour = $editorController.Methods | Where-Object Name -eq 'ApplyContour'
$applyEditorContourOperands = @(
    $applyEditorContour.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorControllerSource -match 'document\.ApplyContour\(' -and
    $applyEditorContourOperands -match 'ClearContourState' -and
    $applyEditorContourOperands -match 'SetActiveTool') `
    'Blueprint editor contour no longer commits once and returns to transform.'
foreach ($method in @('HitTestMove', 'HitTestRotation')) {
    Assert-Contract (@($pluginMethods | Where-Object Name -eq $method).Count -eq 1) `
        "Compiled universal gizmo method is missing: $method"
}
Assert-Contract (@($pluginMethods | Where-Object Name -eq 'HitTestLayout').Count -ge 2) `
    'Compiled array and guide layout hit tests are missing.'
Assert-Contract (@($pluginMethods | Where-Object Name -eq 'BeginAnchorDrag').Count -eq 1) `
    'Compiled anchored drag entry point is missing.'
Assert-Contract (@($pluginTypes | Where-Object Name -eq 'PrecisionPlacementHudView').Count -eq 1) `
    'Compiled integrated HUD view is missing.'
$handleGizmoInput = $pluginMethods | Where-Object {
    $_.Name -eq 'HandleGizmoInput' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$handleGizmoOperands = @(
    $handleGizmoInput.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($handleGizmoOperands -match 'HitTestAnchor' -and
    $handleGizmoOperands -match 'HitTestMove' -and
    $handleGizmoOperands -match 'HitTestRotation' -and
    $handleGizmoOperands -match 'HitTestLayout' -and
    $handleGizmoOperands -match 'RebuildActiveLayout' -and
    $handleGizmoOperands -match 'RotateLocalLayoutFromDragStart') `
    'Compiled universal editor input does not combine anchors, transforms, and array handles.'
$handleGizmoOperandList = @(
    $handleGizmoInput.Body.Instructions | ForEach-Object { [string]$_.Operand }
)
$moveHit = -1
$rotateHit = -1
for ($i = 0; $i -lt $handleGizmoOperandList.Count; ++$i) {
    if ($moveHit -lt 0 -and $handleGizmoOperandList[$i] -match 'HitTestMove') {
        $moveHit = $i
    }
    if ($rotateHit -lt 0 -and $handleGizmoOperandList[$i] -match 'HitTestRotation') {
        $rotateHit = $i
    }
}
Assert-Contract ($moveHit -ge 0 -and $rotateHit -gt $moveHit) `
    'Move handles must win intersections with rotation rings.'
Assert-Contract ($sessionSource -match
    'if \(gizmoMode != GizmoMode\.Repeat\)\s*SelectGizmoMode\(GizmoMode\.Repeat\);') `
    'Passive array entry can still toggle an existing Repeat mode off.'
$cancelNewest = $pluginMethods | Where-Object Name -eq 'CancelNewestStage'
$cancelNewestOperands = @(
    $cancelNewest.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($cancelNewestOperands -match 'RestoreDragLayoutState') `
    'Escape during drag does not restore the array state snapshot.'
$restartAnchor = $pluginMethods | Where-Object Name -eq 'RestartAnchorDragForConstraint'
$restartAnchorOperands = @(
    $restartAnchor.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($restartAnchorOperands -match 'RestoreDragLayoutState') `
    'Changing an anchor constraint can still accumulate local array rotation.'
foreach ($methodName in @('UpdateRepeatDrag', 'UpdatePlaneDrag')) {
    $dragMethod = $pluginMethods | Where-Object Name -eq $methodName
    $dragOperands = @(
        $dragMethod.Body.Instructions | ForEach-Object { [string]$_.Operand }
    ) -join "`n"
    Assert-Contract ($dragOperands -match 'RestoreDragLayoutState') `
        "A click without dragging can still erase the layout in $methodName."
}
$planeDrag = $pluginMethods | Where-Object Name -eq 'UpdatePlaneDrag'
$planeDragOperands = @(
    $planeDrag.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($planeDragOperands -match 'ExactRepeatSteps') `
    'The second array axis ignores the selected exact step.'
Assert-Contract ($sessionSource -match
    'UpdatePlaneDrag\(Vector2 mouse\)[\s\S]*?RestoreDragLayoutState\(\);\s*planeDraggingSecond = true;\s*return;') `
    'The second array axis can still fall back to editing the first row inside its drag dead zone.'
$hudShowEditing = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowEditing' -and $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$hudShowOperands = @(
    $hudShowEditing.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($hudShowOperands -match 'МАССИВ' -and $hudShowOperands -match 'КОНТУР') `
    'Compiled HUD does not expose the unified Array and Guide controls.'
Assert-Contract ($hudShowOperands -match 'ТОЧКИ: ВСЕ' -and
    $hudShowOperands -match 'ТОЧКИ: РЯДОМ' -and
    $hudShowOperands -match 'РУЧКИ') `
    'Compiled HUD does not expose contextual anchor visibility and scale.'
Assert-Contract ($hudShowOperands -match 'МАГНИТ: ИГРА' -and
    $hudShowOperands -match 'МАГНИТ: МЕШ') `
    'Compiled HUD does not expose separate vanilla and mesh snap modes.'
Assert-Contract ($hudShowOperands -match 'АВТОСТЫК: ВКЛ' -and
    $hudShowOperands -match 'АВТОСТЫК: ВЫКЛ' -and
    $hudShowOperands -match 'ВЕРНУТЬ') `
    'Compiled HUD does not expose automatic joining and redo.'
foreach ($method in @(
    'RefreshSnapTargets',
    'TryFindSnapTarget',
    'FocusFreeView',
    'SelectAnchorConstraint',
    'UndoTransform',
    'RedoTransform',
    'CommitTransformIfChanged',
    'AddMeshSnapEdges',
    'GetMeshFeatureEdges',
    'EnsureAnchorHandleCount',
    'RebuildRepeatPlan',
    'RepeatHingeAnchors',
    'RebuildActiveLayout',
    'RotateLocalLayoutFromDragStart',
    'CaptureDragLayoutState',
    'RestoreDragLayoutState',
    'TryBuildContourFromClick',
    'TryGetPlacedPieceAt',
    'TryCreateContourSupport',
    'TrySelectContourEdge',
    'RebuildContourPlan',
    'AddSnapPreviewTarget',
    'TryBeginPassiveAxisDrag',
    'ActivatePassiveCursor',
    'CaptureContinuation',
    'CancelNewestStage',
    'ContinueAutomaticPlacement',
    'HasAutomaticPlacementStamina',
    'HasUsableBuildTool',
    'RepeatStepAxisAngle',
    'AdjustRepeatTurn',
    'AdjustRepeatPitch',
    'AdjustRepeatRoll',
    'AdjustRepeatAngle',
    'ToggleAutoAlignment',
    'ApplyPassiveAutoJoin',
    'TryAutoAlignToTouchingPiece',
    'TryFindAlignedSnapPair',
    'FreezeEditor',
    'ShowFrozen',
    'ShowAlignment',
    'CycleHandleScale',
    'SetLayer',
    'IsPointVisible',
    'CullHiddenSnapPreviewTargets',
    'IsSnapPreviewTarget',
    'SaveCurrentBlueprint',
    'TryBeginWorldPrecision',
    'BeginBlueprintEditor',
    'AddBlueprintWorkspacePart',
    'HandleBlueprintWorkspaceInput',
    'OpenBlueprintWorkspaceCatalog',
    'UpdateBlueprintWorkspaceGhost',
    'RemoveBlueprintWorkspacePart',
    'FindBlueprintWorkspacePart',
    'BeginBlueprintWorkspacePrecision',
    'EndBlueprintWorkspacePrecision',
    'ClearBlueprintWorkspace',
    'TrySaveBlueprintWorkspace',
    'BeginBlueprintPlacement',
    'RebuildBlueprintPlan',
    'TryAddPlacementInstance',
    'ApplyBlueprintAnchors',
    'HandleBlueprintSelectionInput',
    'SelectBlueprintArea',
    'SelectBuildPiece',
    'SelectPlanPiece',
    'UsesHammerPieceTable',
    'GetBuildPieceTable',
    'CaptureSelectedPieceAnchors',
    'ContinueCatalogSelection',
    'ConfirmEditor',
    'ApplyEditorGhostLayer',
    'RestoreEditorGhostLayers')) {
    $requiredMethodCandidates = @($pluginMethods | Where-Object Name -eq $method)
    if ($method -eq 'RepeatStepAxisAngle') {
        $requiredMethodCandidates = @($requiredMethodCandidates | Where-Object {
            $_.DeclaringType.Name -eq 'TransformGizmoView'
        })
    }
    Assert-Contract ($requiredMethodCandidates.Count -eq 1) `
        "Compiled BuildWorks 0.19.8 method is missing: $method"
}
$blueprintRegistry = $pluginTypes | Where-Object Name -eq 'HammerBlueprintPieceRegistry'
Assert-Contract ($null -ne $blueprintRegistry) `
    'Compiled plugin has no native Hammer blueprint-piece registry.'
$thumbnailRenderer = $pluginTypes | Where-Object Name -eq 'BlueprintThumbnailRenderer'
Assert-Contract ($null -ne $thumbnailRenderer -and @(
    $thumbnailRenderer.Methods | Where-Object Name -eq 'GetOrQueue'
).Count -eq 1 -and @(
    $thumbnailRenderer.Methods | Where-Object Name -eq 'ProcessOne'
).Count -eq 1 -and @(
    $thumbnailRenderer.Methods | Where-Object Name -eq 'BeginPreview'
).Count -eq 1) 'Blueprint cards no longer queue, generate, and edit a composite thumbnail.'
$registryUpdate = $blueprintRegistry.Methods | Where-Object Name -eq 'Update'
$registryUpdateOperands = @(
    $registryUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($registryUpdateOperands -match
    'PrecisionPlacementSession::TryResolveBlueprintPieces' -and
    $registryUpdateOperands -match 'PieceTable::m_pieces') `
    'Blueprint pieces are no longer filtered and registered in the current Hammer.'
Assert-Contract ($blueprintRegistrySource -match 'ApplyCatalogLayout\(table\)') `
    'Saved blueprints can disappear before the internal Hammer catalog refreshes.'
Assert-Contract ($blueprintRegistrySource -match 'CategoryMarker' -and
    $blueprintRegistrySource -match 'markerPiece\.m_enabled = false' -and
    $blueprintRegistrySource -match
        'markerPiece\.m_resources = Array\.Empty<Piece\.Requirement>\(\)' -and
    $blueprintRegistrySource -match 'UnifiedHammerCatalog\.RefreshCategoryTabs\(\)' -and
    $blueprintRegistrySource -notmatch
        'AvailablePiecesField == null \|\|\s*blueprintsByPiece\.Count == 0') `
    'An empty library can lose the blueprints tab or require resources to create one.'
Assert-Contract ($blueprintRegistrySource -match
    'if \(markerPiece\) pieces\.Remove\(markerPiece\)' -and
    $blueprintRegistrySource -match
        'HammerCatalogOrganizer\.Group\(piece\) == "ДЕЙСТВИЯ"' -and
    $blueprintRegistrySource -match 'pieces\.Insert\(0, repairPiece\)') `
    'The blueprint category can expose its marker or put Repair after blueprints.'
Assert-Contract ($blueprintRegistrySource -match
    'selectedReplacement \? selectedReplacement : selectedPiece' -and
    $blueprintRegistrySource -match 'player\.SetSelectedPiece\(replacement\)') `
    'Blueprint registration no longer preserves the current Hammer selection.'
Assert-Contract ($blueprintRegistrySource -match
    'BuildSignature\(store\.All\(\), table\)' -and
    $blueprintRegistrySource -match
    'table\.m_pieces') `
    'Blueprint registry no longer refreshes when Hammer catalog contents change.'
$buildSignature = $blueprintRegistry.Methods | Where-Object Name -eq 'BuildSignature'
$buildSignatureOperands = @(
    $buildSignature.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($buildSignatureOperands -match
    'HammerBlueprintPieceRegistry::AppendText' -and
    $buildSignatureOperands -match 'HammerBlueprintPieceRegistry::AppendVector' -and
    $buildSignatureOperands -match 'HammerBlueprintPieceRegistry::AppendQuaternion') `
    'Blueprint registry no longer refreshes when saved blueprint content changes.'
Assert-Contract ($blueprintRegistrySource -match
    'Detach\(\);\s*thumbnailRenderer\.Dispose\(\);') `
    'Changed blueprints can keep a stale generated thumbnail.'
Assert-Contract ($blueprintRegistrySource -match
    'AvailablePiecesField\.GetValue\(registeredTable\)' -and
    $blueprintRegistrySource -match 'blueprintsByPiece\.ContainsKey\(piece\)' -and
    $blueprintRegistrySource -match 'category\.RemoveAt\(index\)') `
    'Blueprint registry leaves stale virtual pieces in the Hammer availability cache.'
$createBlueprintPrefab = $blueprintRegistry.Methods | Where-Object Name -eq 'CreatePrefab'
$createBlueprintPrefabOperands = @(
    $createBlueprintPrefab.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($createBlueprintPrefabOperands -match
    'PlacementGhostPreviewView::CreateVisualClone' -and
    $createBlueprintPrefabOperands -match 'BlueprintThumbnailRenderer::GetOrQueue' -and
    $createBlueprintPrefabOperands -match 'Piece::m_icon' -and
    $createBlueprintPrefabOperands -match
        'HammerBlueprintPieceRegistry::TryGetLocalVisualBounds' -and
    $createBlueprintPrefabOperands -match 'Transform::set_localPosition' -and
    $createBlueprintPrefabOperands -match 'UnityEngine.BoxCollider') `
    'Blueprint Hammer pieces no longer carry a movable composite ghost and icon.'
Assert-Contract ($blueprintRegistrySource -match
    'CompositeBlueprintStore\.HasExplicitFrame\(blueprint\)' -and
    $blueprintRegistrySource -match 'CompositeBlueprintStore\.IsFramePart\(blueprint, index\)' -and
    $blueprintRegistrySource -match 'Bounds frameBounds = visualBounds' -and
    $blueprintRegistrySource -match 'frameBounds = transformed' -and
    $blueprintRegistrySource -match
        'placementOrigin = new Vector3\(\s*frameBounds\.center\.x,\s*frameBounds\.min\.y,\s*frameBounds\.center\.z\)' -and
    $blueprintRegistrySource -match 'AnchorAdjustment\.AnchorCount' -and
    $blueprintRegistrySource -match 'bounds\.Anchor\(index\)' -and
    $blueprintRegistrySource -match 'BlueprintNativeSnapPoints\(blueprint, pieces\)' -and
    $blueprintRegistrySource -match 'collider\.center = frameBounds\.center - placementOrigin' -and
    $blueprintRegistrySource -match 'Mathf\.Max\(frameBounds\.size\.x, 0\.05f\)' -and
    $blueprintRegistrySource -match 'AnchorAdjustment\.ExternalCompositeSnapPoints') `
    'Blueprint placement no longer uses the selected part/group frame or composite fallback.'
Assert-Contract ($blueprintRegistrySource -match
    'if \(!CompositeBlueprintStore\.HasExplicitFrame\(blueprint\) \|\| nativeSnapPoints\.Count == 0\)' -and
    $blueprintRegistrySource -match
    'foreach \(Vector3 point in nativeSnapPoints\)') `
    'Explicit Hammer blueprint frames must prefer their native snaps; bounds are fallback only.'
Assert-Contract ($pluginSource -match
    '"BlueprintCompositeFrame",\s*KeyCode\.N') `
    'Whole-blueprint composite-frame modifier must default to N instead of RightShift.'
Assert-Contract ($sessionSource -match
    'int frameIndex = CompositeBlueprintStore\.FramePartIndex\(blueprint\);\s*if \(!SelectBuildPiece\(player, pieces\[frameIndex\]\)\)' -and
    $sessionSource -match
    'if \(isolatedEditorView && !SelectBlueprintEditTarget\(frameIndex\)\)' -and
    $sessionSource -match
    'int blueprintGhostIndex = blueprintEditPartIndex >= 0\s*\? blueprintEditPartIndex\s*:\s*CompositeBlueprintStore\.FramePartIndex\(activeBlueprint\)' -and
    $sessionSource -match
    'previews\.Show\([\s\S]{0,360}CompositeBlueprintStore\.FramePartIndex\(activeBlueprint\)') `
    'Whole-blueprint F9 editing does not use the same frame piece for host ghost and preview hiding.'
Assert-Contract ($midpointSource -match
    'HammerBlueprintPieceRegistry\.PrefabPrefix' -and
    $midpointSource -match 'StringComparison\.Ordinal') `
    'Synthetic blueprint pieces can still multiply their group snap points.'
Assert-Contract ($previewSource -match
    'private static void CopyVisualHierarchy' -and
    $previewSource -match
    'targetChild\.SetActive\(sourceChild\.gameObject\.activeSelf\)' -and
    $previewSource -match 'renderer\.enabled = sourceRenderer\.enabled;' -and
    $previewSource -notmatch
        'renderer\.enabled = sourceRenderer\.enabled \|\|' -and
    $blueprintRegistrySource -match 'IsActiveWithin\(root\.transform, filter\.transform\)' -and
    $thumbnailSource -match '!renderer\.gameObject\.activeInHierarchy') `
    'Composite preview can still activate hidden wear or damage variants.'
$catalogLayout = $blueprintRegistry.Methods | Where-Object Name -eq 'ApplyCatalogLayout'
$catalogLayoutOperands = @(
    $catalogLayout.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogLayoutOperands -match
    'UnifiedHammerCatalog::get_BlueprintCategory' -and
    $catalogLayoutOperands -match 'System.Collections.Generic.List`1<Piece>::Insert') `
    'Blueprint pieces no longer occupy their own internal catalog category.'
$catalogLayoutPatch = $pluginTypes | Where-Object Name -eq 'HammerCatalogLayoutPatch'
Assert-Contract (@($catalogLayoutPatch).Count -eq 1) `
    'BuildWorks no longer refreshes its internal Hammer catalog after availability changes.'
$catalogLayoutPrefix = $catalogLayoutPatch.Methods | Where-Object Name -eq 'Prefix'
$catalogLayoutPostfix = $catalogLayoutPatch.Methods | Where-Object Name -eq 'Postfix'
$catalogLayoutPatchOperands = @(
    $catalogLayoutPrefix.Body.Instructions
    $catalogLayoutPostfix.Body.Instructions
    | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogLayoutPatchOperands -match
    'HammerCatalogOrganizer::CaptureSelection' -and
    $catalogLayoutPatchOperands -match 'HammerCatalogOrganizer::Reorder') `
    'Hammer availability rebuild no longer preserves the selected construction piece.'
$unifiedCatalog = $pluginTypes | Where-Object Name -eq 'UnifiedHammerCatalog'
$catalogOrganizer = $pluginTypes | Where-Object Name -eq 'HammerCatalogOrganizer'
$catalogView = $pluginTypes | Where-Object Name -eq 'HammerCatalogView'
Assert-Contract ($null -ne $unifiedCatalog -and $null -ne $catalogOrganizer -and
    $null -ne $catalogView) `
    'The unified Hammer catalog is not compiled into BuildWorks.'
Assert-Contract ($catalogSource -match 'BlueprintCategoryReady' -and
    $blueprintRegistrySource -match
        'if \(!UnifiedHammerCatalog\.BlueprintCategoryReady\)' -and
    $catalogSource -match
        'bool blueprints = UnifiedHammerCatalog\.BlueprintCategoryReady') `
    'Missing Jotunn can still turn the vanilla Misc category into blueprints.'
$catalogUpdate = $unifiedCatalog.Methods | Where-Object Name -eq 'Update'
$catalogUpdateOperands = @(
    $catalogUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogUpdateOperands -match 'ObjectDB::GetItemPrefab' -and
    $catalogUpdateOperands -match 'Piece::m_category' -and
    $catalogUpdateOperands -match 'System.Collections.Generic.List`1<UnityEngine.GameObject>::Add' -and
    $catalogUpdateOperands -match 'System.Collections.Generic.List`1<UnityEngine.GameObject>::Remove') `
    'The unified catalog no longer moves mod pieces into the vanilla Hammer.'
Assert-Contract ($catalogSource -match
        'piece\.m_category != Piece\.PieceCategory\.DeepNorth' -and
    $catalogSource -match
        'piece\.m_category = Piece\.PieceCategory\.BuildingWorkbench' -and
    $catalogSource -match 'reclassifiedPieces\.Add\(piece\)' -and
    $catalogSource -match
        'piece\.m_category = Piece\.PieceCategory\.DeepNorth') `
    'DeepNorth pieces are not reversibly reindexed into Construction.'
Assert-Contract ($catalogSource -match
    'if \(!TryResolveJotunn\([\s\S]{0,220}\)\)\s*\{\s*return;\s*\}') `
    'A temporary Jotunn lookup failure can permanently hide the blueprints category.'
$catalogDispose = $unifiedCatalog.Methods | Where-Object Name -eq 'Dispose'
$catalogDisposeOperands = @(
    $catalogDispose.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogDisposeOperands -match
    'System.Collections.Generic.List`1<UnityEngine.GameObject>::Remove' -and
    $catalogDisposeOperands -match 'System.Collections.Generic.List`1<UnityEngine.GameObject>::Insert' -and
    $catalogDisposeOperands -match 'Piece::m_category') `
    'The unified catalog no longer restores moved pieces to their source Hammer.'
$openBlueprints = $catalogOrganizer.Methods | Where-Object Name -eq 'OpenBlueprints'
$openBlueprintOperands = @(
    $openBlueprints.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($openBlueprintOperands -match
        'PieceTable::SetCategory\(Piece/PieceCategory\)' -and
    $openBlueprintOperands -notmatch 'PieceTable::SetCategory\(System.Int32\)') `
    'Blueprint entry still passes an enum value as a category-list index.'
$populateFavorites = $catalogOrganizer.Methods | Where-Object Name -eq 'PopulateFavorites'
$toggleFavorite = $catalogOrganizer.Methods | Where-Object Name -eq 'ToggleFavorite'
$favoriteOperands = @(
    $populateFavorites.Body.Instructions + $toggleFavorite.Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogSource -match 'FavoriteCategoryName = "ИЗБРАННОЕ"' -and
    $catalogSource -match 'AddFavoriteCategoryKeeper' -and
    $favoriteOperands -match 'BuildUi::IsFavoritePiece' -and
    $favoriteOperands -match 'BuildUi::ToggleFavorite' -and
    $favoriteOperands -match 'BuildUi::RemoveFromFavorites' -and
    $catalogSource -match 'class HammerCatalogFavoriteClick' -and
    $catalogSource -match 'data\.button != PointerEventData\.InputButton\.Middle' -and
    $catalogSource -match 'private static void AddFavoriteControls' -and
    $catalogSource -match 'typeof\(BuildUiPieceButton\)\.GetField' -and
    $catalogSource -match 'm_pieceButtons' -and
    $catalogSource -match 'button\.RefreshFavorite\(\)' -and
    $pluginSource -match 'HarmonyPatch\(typeof\(BuildUi\), "OnFavoritePieceAdded"\)' -and
    $pluginSource -match 'HarmonyPatch\(typeof\(BuildUi\), "OnFavoritePieceRemoved"\)' -and
    $catalogSource -notmatch 'favorite \? "★" : "☆"') `
    'Indexed Hammer favorites are no longer shared with native BuildUi.'
$catalogViewApply = $catalogView.Methods | Where-Object Name -eq 'Apply'
$catalogViewApplyOperands = @(
    $catalogViewApply.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogViewApplyOperands -match 'HammerCatalogView::DecorationsAreAlive') `
    'Hammer catalog decorations are rebuilt on every HUD refresh.'
$applyNative = $catalogView.Methods | Where-Object Name -eq 'ApplyNative'
$positionNativeEntry = $catalogView.Methods | Where-Object Name -eq 'PositionNativeIndexEntry'
$applyNativeOperands = @(
    $applyNative.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$positionNativeEntryOperands = @(
    $positionNativeEntry.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($applyNativeOperands -match
        'HammerCatalogView::PositionNativeIndexEntry' -and
    $positionNativeEntryOperands -match
        'Hud::m_pieceSelectionWindow' -and
    $positionNativeEntryOperands -match
        'UnityEngine.RectTransformUtility::CalculateRelativeRectTransformBounds' -and
    $positionNativeEntryOperands -match
        'UnityEngine.Transform::set_localPosition' -and
    $positionNativeEntryOperands -match
        'UnityEngine.Transform::SetAsLastSibling' -and
    $catalogSource -match
        'AddNativeIndexEntry[\s\S]{0,260}AddButton\(\s*hud,\s*root,' -and
    $catalogSource -notmatch
        'AddNativeIndexEntry[\s\S]{0,180}root\.parent') `
    'The external BuildWorks entry no longer follows the live frame from the stable BuildUi root.'
Assert-Contract ($catalogSource -match
        'if \(!nativeBuildUiWasVisible\)[\s\S]{0,80}OpenDefaultIndex\(hud, player\);' -and
    $catalogSource -match
        'OpenDefaultIndex[\s\S]{0,120}if \(nativeBuildUiWasVisible \|\|[\s\S]{0,300}nativeBuildUiWasVisible = true;[\s\S]{0,100}nativeIndexMode = true;[\s\S]{0,140}SetIndexEnabled\(true, player\);[\s\S]{0,100}EnterNativeIndex\(hud\);' -and
    $catalogSource -match
        'MarkNativeBuildUiClosed[\s\S]{0,100}nativeBuildUiWasVisible = false;') `
    'A newly opened native Hammer no longer enters the BuildWorks index exactly once.'
$defaultOpenPatch = $pluginType.NestedTypes | Where-Object {
    $_.Name -eq 'HammerCatalogDefaultOpenPatch'
}
$defaultOpenPostfix = $defaultOpenPatch.Methods | Where-Object Name -eq 'Postfix'
$defaultOpenOperands = @(
    $defaultOpenPostfix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$defaultResetPatch = $pluginType.NestedTypes | Where-Object {
    $_.Name -eq 'HammerCatalogDefaultOpenResetPatch'
}
$defaultResetPostfix = $defaultResetPatch.Methods | Where-Object Name -eq 'Postfix'
$defaultResetOperands = @(
    $defaultResetPostfix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($pluginSource -match
        'HarmonyPatch\(typeof\(BuildUi\), nameof\(BuildUi\.OpenBuildMenu\)\)' -and
    $defaultOpenOperands -match 'HammerCatalogView::OpenDefaultIndex' -and
    $pluginSource -match
        'HarmonyPatch\(typeof\(BuildUi\), nameof\(BuildUi\.Close\)\)' -and
    $defaultResetOperands -match 'HammerCatalogView::MarkNativeBuildUiClosed') `
    'BuildWorks default index is no longer armed before the first rendered Hammer frame.'
Assert-Contract ($catalogSource -match
        '"NativeIndexEntry",\s*"BUILDWORKS",') `
    'The native return button still describes only the blueprint subsection.'
Assert-Contract ($catalogViewApplyOperands -match
    'HammerCatalogOrganizer::get_IndexEnabled' -and
    $catalogViewApplyOperands -match 'HammerCatalogView::AddVanillaModeControls') `
    'Vanilla Hammer view still builds the indexed catalog UI.'
$catalogRail = $catalogView.Methods | Where-Object Name -eq 'AddIndexRail'
$sourceTabs = $catalogView.Methods | Where-Object Name -eq 'AddSourceTabs'
$blueprintLibrary = $catalogView.Methods | Where-Object Name -eq 'AddBlueprintLibrary'
$previewEditor = $catalogView.Methods | Where-Object Name -eq 'AddPreviewEditor'
$blueprintCardSource = [regex]::Match(
    $catalogSource,
    'private static void AddBlueprintCard[\s\S]*?(?=private static void AddBlueprintInspector)'
).Value
$catalogRailOperands = @(
    $catalogRail.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($null -ne $catalogRail -and $null -ne $sourceTabs -and
    $null -ne $blueprintLibrary -and $null -ne $previewEditor -and
    $catalogRailOperands -match 'HammerCatalogView::AddRailButton' -and
    $catalogSource -match 'HammerCatalogOrganizer\.GoToPage' -and
    $catalogSource -match 'HammerCatalogOrganizer\.GoToGroup' -and
    $catalogSource -notmatch 'AddMaterialBand') `
    'Hammer catalog no longer exposes indexed sources, blueprints, preview, and pages.'
Assert-Contract ($blueprintCardSource -match 'HammerCatalogPointerClick' -and
    $blueprintCardSource -match 'RightPressed[\s\S]{0,180}contextBlueprint = blueprint' -and
    $blueprintCardSource -match 'RightReleased = ReleaseBlueprintContextMenu' -and
    $blueprintCardSource -match 'bool doubleClick' -and
    $blueprintCardSource -match 'if \(doubleClick\) blueprintAction\?\.Invoke\(blueprint, false\)' -and
    $blueprintCardSource -match 'else Invalidate\(\)' -and
    $blueprintCardSource -match 'AddBlueprintContextMenu' -and
    $blueprintCardSource -match '"РАЗМЕСТИТЬ"' -and
    $blueprintCardSource -match '"РЕДАКТИРОВАТЬ"' -and
    $blueprintCardSource -match '"ПЕРЕИМЕНОВАТЬ"' -and
    $blueprintCardSource -match '"ОБНОВИТЬ ПРЕВЬЮ"' -and
    $catalogSource -match 'ShouldCaptureBlueprintContext' -and
    $catalogSource -match 'Input\.GetMouseButtonDown\(1\)' -and
    $catalogSource -match 'eventSystem\.RaycastAll\(pointer, hits\)' -and
    $catalogSource -match 'GetComponentInParent<HammerCatalogPointerClick>' -and
    $pluginSource -match
        '\[HarmonyPatch\(typeof\(BuildUi\), "NavigationUpdate"\)\][\s\S]{0,260}?ShouldCaptureBlueprintContext' -and
    $blueprintCardSource -match '"РЕСУРСЫ"' -and
    $blueprintCardSource -match '"УДАЛИТЬ"') `
    'Blueprint cards lost single-click selection, double-click placement, or the RMB action menu.'
Assert-Contract ($catalogSource -match 'ApplyLayout\(hud, table\)' -and
    $catalogSource -match 'Screen\.safeArea' -and
    $catalogSource -match 'layoutCanvas\.scaleFactor' -and
    $catalogSource -match 'layoutWindowWidth = window\.rect\.width' -and
    $catalogSource -match 'layoutWindowHeight = window\.rect\.height' -and
    $catalogSource -notmatch 'window\.anchoredPosition\s*=' -and
    $catalogSource -notmatch 'window\.sizeDelta\s*=' -and
    $catalogSource -notmatch 'window\.anchorMin\s*=' -and
    $catalogSource -match 'PlaceInsideWindow\(mask, window, contentLeft, contentTop, contentSize\)' -and
    $catalogSource -match 'PlaceInsideWindow\(listRoot, window, contentLeft, contentTop, Vector2\.zero\)' -and
    $catalogSource -notmatch 'PlaceInsideWindow\(\s*categories' -and
    $catalogSource -match 'parent\.InverseTransformPoint\(worldTopLeft\)' -and
    $catalogSource -match 'parent\.InverseTransformPoint\(worldBottomRight\)' -and
    $catalogSource -match 'rect\.anchorMin = anchorMin' -and
    $catalogSource -notmatch 'ReserveLabelSpace') `
    'Hammer index no longer adapts its reserved layout to the active screen.'
Assert-Contract ($catalogSource -match 'private const int IndexVisibleColumns = 12' -and
    $catalogSource -match 'private const int IndexVisibleRows = 4' -and
    $catalogSource -match 'private const int VanillaVisibleRows = 6' -and
    $catalogSource -match 'VisibleColumnCount \* VisibleRowCount' -and
    $catalogSource -match 'ApplyPage\(pieces, full, page\)' -and
    $catalogSource -match 'RequestedPages\.Remove\(key\)') `
    'Hammer index no longer keeps every piece reachable through stable pages.'
Assert-Contract ($catalogSource -match 'private static void AddModeToggle' -and
    $catalogSource -match '"РЕЖИМ ИНДЕКСА"' -and
    $catalogSource -match 'indexMode \? layoutRailWidth \+ 20f : 12f' -and
    $catalogSource -match 'indexMode \? 8f : -36f' -and
    $catalogSource -match 'VanillaPreviousPage[\s\S]{0,1100}new Vector2\(164f, -36f\)' -and
    $catalogSource -match 'VanillaNextPage[\s\S]{0,700}new Vector2\(264f, -36f\)' -and
    $catalogSource -match 'blueprintAction\?\.Invoke\(blueprint, false\)' -and
    $catalogSource -match '"ManageBlueprint"') `
    'Vanilla Index still overlaps a piece or the explicit blueprint inspector action is missing.'
Assert-Contract ($catalogSource -match
    'if \(!indexEnabled\)[\s\S]{0,220}FullLists\[key\] = new List<Piece>\(pieces\)' -and
    $catalogSource -match 'AddVanillaModeControls[\s\S]{0,2400}VanillaPreviousPage' -and
    $catalogSource -match 'VanillaNextPage' -and
    $catalogSource -notmatch
        '!hud\.m_pieceSelectionWindow\.activeInHierarchy \|\| !indexEnabled') `
    'Vanilla Hammer pages or their visible controls are missing.'
$catalogViewPatch = $pluginTypes | Where-Object Name -eq 'HammerCatalogViewPatch'
$catalogViewPatchOperands = @(
    ($catalogViewPatch.Methods | Where-Object Name -eq 'Postfix').Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
$pluginUpdateOperands = @(
    (($pluginTypes | Where-Object Name -eq 'BuildWorksPlugin').Methods |
        Where-Object Name -eq 'Update').Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogSource -match 'public static void HandlePaging' -and
    $pluginUpdateOperands -match 'HammerCatalogOrganizer::HandlePaging' -and
    $catalogLayoutPatchOperands -match 'HammerCatalogOrganizer::DisablePagination' -and
    $catalogViewPatchOperands -notmatch 'HammerCatalogOrganizer::DisablePagination') `
    'Hammer catalog failure fallback can leave hidden pages unreachable.'
$categoryNavigation = $catalogOrganizer.Methods |
    Where-Object Name -eq 'EnsureCategoryNavigation'
$categoryNavigationOperands = @(
    $categoryNavigation.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($catalogSource -match 'KeyCode\.Q' -and
    $catalogSource -match 'KeyCode\.E' -and
    $categoryNavigationOperands -match 'PieceTable::PrevCategory' -and
    $categoryNavigationOperands -match 'PieceTable::NextCategory' -and
    $catalogViewPatchOperands -match
        'HammerCatalogOrganizer::EnsureCategoryNavigation') `
    'Q/E no longer guarantee Hammer category navigation.'
Assert-Contract ($catalogSource -match 'public static bool MoveIndexSelection' -and
    $catalogSource -match 'int columns = IndexVisibleColumns' -and
    $catalogSource -match 'pieces\.IndexOf\(table\.GetSelectedPiece\(\)\)' -and
    $catalogSource -match 'target % nativeWidth, target / nativeWidth') `
    'Index-mode logical navigation no longer follows the visible 12-column grid.'
foreach ($patchName in @(
    'HammerCatalogLeftPiecePatch',
    'HammerCatalogRightPiecePatch',
    'HammerCatalogUpPiecePatch',
    'HammerCatalogDownPiecePatch')) {
    $movementPatch = $pluginTypes | Where-Object Name -eq $patchName
    $movementOperands = @(
        ($movementPatch.Methods | Where-Object Name -eq 'Prefix').Body.Instructions |
            ForEach-Object { [string]$_.Operand }
    ) -join "`n"
    Assert-Contract (@($movementPatch).Count -eq 1 -and
        $movementOperands -match 'HammerCatalogOrganizer::MoveIndexSelection') `
        "Index-mode movement patch is missing or disconnected: $patchName"
}
Assert-Contract ($catalogSource -match 'private static void NormalizeNativeCategoryTabs' -and
    $catalogSource -notmatch 'AddBlueprintShortcut' -and
    $catalogSource -match 'GetComponentsInChildren<TMP_Text>\(true\)' -and
    $catalogSource -match 'GetTotalCount\(' -and
    $catalogSource -match 'AllLists\.TryGetValue\(key' -and
    $catalogSource -match 'Mathf\.Max\(1f, layoutWindowWidth - contentLeft - 8f\)' -and
    $catalogSource -match 'Mathf\.Max\(1f, layoutWindowHeight - contentTop - 58f\)' -and
    $catalogSource -match 'private static Vector2 FitGridSize' -and
    $catalogSource -match 'GridLayoutGroup grid' -and
    $catalogSource -match 'HammerCatalogOrganizer\.VisibleColumnCount' -and
    $catalogSource -match 'HammerCatalogOrganizer\.VisibleRowCount' -and
    $catalogSource -match 'private static void ArrangeIndexGrid' -and
    $catalogSource -match 'GridLayoutGroup\.Constraint\.FixedColumnCount' -and
    $catalogSource -match 'grid\.constraintCount = columns' -and
    $catalogSource -match 'root\.SetActive\(PieceIconActiveStates\[index\]\)' -and
    $catalogSource -match 'rows \* grid\.cellSize\.y' -and
    $catalogSource -match 'fallback\.x / Mathf\.Max\(1f, width \* scaleX\)' -and
    $catalogSource -match 'fallback\.y / Mathf\.Max\(1f, height \* scaleY\)' -and
    $catalogSource -match 'listRoot\.localScale = new Vector3\(scale\.x \* fit, scale\.y \* fit, scale\.z\)' -and
    $catalogSource -match 'rect\.localScale = scale' -and
    $catalogSource -notmatch 'categories\.gameObject\.AddComponent<RectMask2D>\(\)' -and
    $catalogSource -match 'LayoutRebuilder\.ForceRebuildLayoutImmediate\(categoryRoot\)' -and
    $catalogSource -match 'RectTransformUtility\.CalculateRelativeRectTransformBounds' -and
    $catalogSource -match 'window\.rect\.yMax - bounds\.min\.y' -and
    $blueprintRegistrySource -match
        'while \(available\.Count <= categoryIndex\) available\.Add') `
    'The native blueprint tab can duplicate, wrap, or report only one page.'
Assert-Contract ($catalogSource -notmatch 'nativeCategoryTabStates' -and
    $catalogSource -notmatch 'GetCategoryColumns' -and
    $catalogSource -notmatch 'RestoreNativeCategoryLayout' -and
    $catalogSource -notmatch 'transform\.Find\("Bkg2"\)' -and
    $catalogSource -notmatch 'background\.offsetMax' -and
    $catalogSource -match
        'float contentTop = layoutCategoryBottom \+ 66f \+ sourceOffset' -and
    $catalogSource -match
        '-layoutCategoryBottom - 30f' -and
    $catalogSource -match 'float top = layoutCategoryBottom \+ 30f' -and
    $catalogSource -match 'float top = layoutCategoryBottom \+ 20f') `
    'Index mode must preserve the native Hammer header and start below its actual rows.'
foreach ($categoryBottom in 42, 76, 110) {
    foreach ($sourceRows in 1..2) {
        $sourceTabs = $categoryBottom + 30
        $sourceHeight = 30 + ($sourceRows - 1) * 28
        $contentTop = $categoryBottom + 66 + ($sourceRows - 1) * 28
        Assert-Contract (($sourceTabs + $sourceHeight) -lt $contentTop) `
            "Index filters overlap the grid below a $categoryBottom px native header."
    }
}
Assert-Contract ($sessionSource -match 'private const int EditorLayer = 30;' -and
    $sessionSource -match 'camera\.cullingMask = 1 << EditorLayer' -and
    $previewSource -match 'placementGhost\.layer') `
    'The isolated blueprint editor can leak shared-layer objects or hide its composite preview.'
$wheelCapturePatch = $pluginTypes | Where-Object Name -eq 'HammerCatalogWheelCapturePatch'
$wheelCaptureOperands = @(
    ($wheelCapturePatch.Methods | Where-Object Name -eq 'Prefix').Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($null -ne $wheelCapturePatch -and
    $wheelCaptureOperands -match 'HammerCatalogView::get_ShouldCaptureWheel') `
    'Vanilla Hammer input can consume wheel paging together with BuildWorks.'
Assert-Contract (@($pluginTypes | Where-Object Name -eq 'GameLogoutCleanupPatch').Count -eq 1) `
    'BuildWorks no longer cleans the unified catalog before Game.Logout destroys the host.'
$logoutCleanup = $pluginMethods | Where-Object Name -eq 'HandleHostLogout'
$logoutCleanupOperands = @(
    $logoutCleanup.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($logoutCleanupOperands -match 'BuildWorksPlugin::DisposeSession' -and
    $logoutCleanupOperands -match 'UnifiedHammerCatalog::Dispose') `
    'Logout cleanup no longer detaches blueprint state before restoring source Hammers.'
Assert-Contract (@($plugin.MainModule.AssemblyReferences | Where-Object {
    $_.Name -eq 'Jotunn' -or $_.Name -eq 'CraftIndex' -or $_.Name -eq 'HammerTime'
}).Count -eq 0) 'The unified catalog gained a hard dependency on another gameplay mod.'
Assert-Contract (@($pluginTypes | Where-Object Name -eq 'HammerBlueprintLibraryView').Count -eq 0) `
    'The obsolete custom Hammer blueprint tab is still compiled.'
Assert-Contract ($hudSource -notmatch 'ЗАГР\. ЧЕРТЕЖ' -and
    $hudSource -notmatch 'BlueprintPrevious|BlueprintNext') `
    'The F9 editor still contains blueprint-library navigation.'
$blueprintStore = $pluginTypes | Where-Object Name -eq 'CompositeBlueprintStore'
Assert-Contract ($null -ne $blueprintStore) `
    'Compiled plugin has no local composite-blueprint store.'
$blueprintPart = $pluginTypes | Where-Object {
    $_.Name -eq 'Part' -and $_.DeclaringType -eq $blueprintStore
}
Assert-Contract ($null -ne $blueprintPart) 'Blueprint part record is missing.'
foreach ($fieldName in @('prefabName', 'stableId', 'displayName', 'parentGroupId',
    'editorVisible', 'editorLocked')) {
    Assert-Contract (@($blueprintPart.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "Blueprint part field is missing: $fieldName"
}
$blueprintGroup = $pluginTypes | Where-Object {
    $_.Name -eq 'Group' -and $_.DeclaringType -eq $blueprintStore
}
Assert-Contract ($null -ne $blueprintGroup) 'Blueprint group record is missing.'
foreach ($fieldName in @('stableId', 'name', 'editorVisible', 'editorLocked', 'pivotPartId')) {
    Assert-Contract (@($blueprintGroup.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "Blueprint group field is missing: $fieldName"
}
$blueprintType = $pluginTypes | Where-Object {
    $_.Name -eq 'Blueprint' -and $_.DeclaringType -eq $blueprintStore
}
foreach ($fieldName in @('category', 'previewYaw', 'previewPitch', 'previewZoom', 'groups', 'primaryPartId', 'primaryGroupId')) {
    Assert-Contract (@($blueprintType.Fields | Where-Object Name -eq $fieldName).Count -eq 1) `
        "Blueprint library field is missing: $fieldName"
}
foreach ($methodName in @('TryRename', 'TryDelete', 'TrySetCategory',
    'TryAddCategory', 'TryDeleteCategory', 'TrySetPreview')) {
    Assert-Contract (@($blueprintStore.Methods | Where-Object Name -eq $methodName).Count -eq 1) `
        "Blueprint library action is missing: $methodName"
}
$saveDocumentMethods = @($blueprintStore.Methods | Where-Object Name -eq 'TrySaveDocument')
$updateDocumentMethods = @($blueprintStore.Methods | Where-Object Name -eq 'TryUpdateDocument')
Assert-Contract ($saveDocumentMethods.Count -eq 4 -and
    @($saveDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryPartId').Count -eq 1
    }).Count -eq 2 -and
    @($saveDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryGroupId').Count -eq 1
    }).Count -eq 1) `
    'Blueprint frame save overload or a compatibility overload is missing.'
Assert-Contract ($updateDocumentMethods.Count -eq 4 -and
    @($updateDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryPartId').Count -eq 1
    }).Count -eq 2 -and
    @($updateDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryGroupId').Count -eq 1
    }).Count -eq 1) `
    'Blueprint frame update overload or a compatibility overload is missing.'
Assert-Contract ($blueprintStoreSource -match 'private const int FormatVersion = 9' -and
    $blueprintStoreSource -match 'loaded\.version < 6' -and
    $blueprintStoreSource -match 'ValidScale\(part\.scale\)' -and
    $blueprintStoreSource -match 'if \(loaded\.version < 4\)' -and
    $blueprintStoreSource -match 'part\.stableId = blueprint\.id' -and
    $blueprintStoreSource -match 'ValidDocument\(blueprint\.parts, blueprint\.groups\)' -and
    $blueprintStoreSource -match 'ValidPrimaryPart\(blueprint\.parts, blueprint\.primaryPartId\)' -and
    $blueprintStoreSource -match 'ValidPrimaryGroup\(blueprint\.parts, blueprint\.groups, blueprint\.primaryGroupId\)' -and
    $blueprintStoreSource -match
        'HashSet<string>\(StringComparer\.OrdinalIgnoreCase\)' -and
    $blueprintStoreSource -match 'ids\.Add\(blueprint\.id\)' -and
    $thumbnailSource -match 'Path\.Combine\([\s\S]{0,80}"thumbnails"\)' -and
    $thumbnailSource -match 'could not remove temporary thumbnail' -and
    $thumbnailSource -match 'camera\.backgroundColor = PreviewBackground' -and
    $thumbnailSource -match 'camera\.backgroundColor = ThumbnailBackground' -and
    $thumbnailSource -match 'MakeBackgroundTransparent\(texture\)' -and
    $thumbnailSource -match 'new Color\(0\.04f, 0\.015f, 0\.10f, 0f\)' -and
    $thumbnailSource -match 'int alpha = Math\.Max\(pixel\.a, derivedAlpha\)' -and
    $thumbnailSource -match 'HasLegacyOpaqueBackground\(texture\)' -and
    $thumbnailSource -match 'Math\.Abs\(pixel\.r - background\.r\)' -and
    $thumbnailSource -match 'private const int ThumbnailLayer = 31') `
    'Blueprint v3/v4 migration, nested groups, or isolated persistent previews are missing.'
Assert-Contract ($catalogSource -match 'typeof\(RectMask2D\)' -and
    $catalogSource -match 'popup\.AddComponent<ScrollRect>\(\)' -and
    $catalogSource -match 'modalCanvas\.overrideSorting = true' -and
    $catalogSource -match 'panel\.AddComponent<GraphicRaycaster>\(\)' -and
    $catalogSource -match
        'BlocksCatalogPaging => previewSession != null \|\| showResources') `
    'Blueprint resource scrolling or preview modal input isolation is missing.'
Assert-Contract (@($plugin.MainModule.AssemblyReferences | Where-Object {
    $_.Name -eq 'UnityEngine.JSONSerializeModule'
}).Count -eq 0) 'Blueprint persistence still depends on Unity JsonUtility.'
$blueprintSave = $blueprintStore.Methods | Where-Object Name -eq 'Save'
$blueprintSaveOperands = @(
    $blueprintSave.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($blueprintSaveOperands -match 'CompositeBlueprintStore::Serialize' -and
    $blueprintSaveOperands -match 'CompositeBlueprintStore::Deserialize' -and
    $blueprintSaveOperands -match 'System.IO.File::ReadAllText' -and
    $blueprintSaveOperands -match 'System.IO.File::Replace') `
    'Blueprint persistence no longer verifies and atomically replaces its JSON file.'
$selectBuildPiece = $pluginMethods | Where-Object Name -eq 'SelectBuildPiece'
$selectBuildPieceOperands = @(
    $selectBuildPiece.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($selectBuildPieceOperands -match
    'HammerBlueprintPieceRegistry::SelectPiece') `
    'Mixed blueprints do not switch their native Valheim piece automatically.'
Assert-Contract ($selectBuildPieceOperands -match 'HammerCatalogOrganizer::EnsureVisible') `
    'Paged Hammer catalog can hide a blueprint part from native placement.'
Assert-Contract ($catalogSource -match
    '(?s)public static bool EnsureVisible\(PieceTable table, Piece piece\).*?entry in AllLists\).*?visible\.Add\(piece\);') `
    'Filtered Hammer pieces can no longer be exposed for blueprint placement.'
$resolveBuildPiece = $pluginMethods | Where-Object Name -eq 'TryResolveBuildPiece'
$resolveBuildPieceOperands = @(
    $resolveBuildPiece.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($resolveBuildPieceOperands -match 'PrecisionPlacementSession::BuildPiecesField' -and
    $resolveBuildPieceOperands -match 'PieceTable::m_pieces' -and
    $resolveBuildPieceOperands -match 'PrecisionPlacementSession::IsBuildPieceAvailable' -and
    $resolveBuildPieceOperands -notmatch 'PieceTable::IsPieceAvailable') `
    'Blueprint resolution no longer searches every unlocked Hammer category.'
$isBuildPieceAvailable = $pluginMethods | Where-Object Name -eq 'IsBuildPieceAvailable'
$isBuildPieceAvailableOperands = @(
    $isBuildPieceAvailable.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($isBuildPieceAvailableOperands -match
    'HammerCatalogOrganizer::ContainsAvailable' -and
    $isBuildPieceAvailableOperands -match 'PrecisionPlacementSession::AvailablePiecesField' -and
    $isBuildPieceAvailableOperands -match 'System.Collections.Generic.List`1<Piece>::Contains') `
    'Blueprint availability no longer scans every unlocked Hammer category.'
$resolveBlueprintPieces = $pluginMethods | Where-Object Name -eq 'TryResolveBlueprintPieces'
$resolveBlueprintPiecesOperands = @(
    $resolveBlueprintPieces.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($resolveBlueprintPiecesOperands -match
    'PrecisionPlacementSession::TryResolveBuildPiece' -and
    $resolveBlueprintPiecesOperands -match
    'PrecisionPlacementSession::SupportsBlueprintWorkspacePiece' -and
    $resolveBlueprintPiecesOperands -match 'CompositeBlueprintStore::ValidScale' -and
    $resolveBlueprintPiecesOperands -match 'ZNetView::m_syncInitialScale' -and
    $resolveBlueprintPiecesOperands -notmatch 'PrecisionPlacementSession::SupportsPrecisionPlacement') `
    'Blueprint resolution must allow available furniture while retaining native scale validation.'
foreach ($workspaceGateName in @('BlueprintEditorCatalogItems', 'EnableNativePieceScale', 'IsBlueprintPieceSelectable')) {
    $workspaceGate = $pluginMethods | Where-Object { $_.Name -eq $workspaceGateName -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
    $workspaceGateOperands = @($workspaceGate.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($workspaceGateOperands -match 'SupportsBlueprintWorkspacePiece' -and
        $workspaceGateOperands -notmatch 'SupportsPrecisionPlacement') `
        "Furniture support is still cut off before save/place: $workspaceGateName"
}
$workspacePredicate = @($pluginMethods | Where-Object {
    $_.Name -eq 'SupportsBlueprintWorkspacePiece' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
})
Assert-Contract ($workspacePredicate.Count -eq 1) 'Expected one blueprint workspace piece predicate.'
$workspacePredicateFields = @($workspacePredicate[0].Body.Instructions | Where-Object {
    $_.OpCode.Name -eq 'ldfld'
} | ForEach-Object { [string]$_.Operand } | Sort-Object -Unique)
$workspacePredicateCalls = @($workspacePredicate[0].Body.Instructions | Where-Object {
    $_.OpCode.Name -in @('call', 'callvirt')
} | ForEach-Object { [string]$_.Operand })
Assert-Contract ($workspacePredicateFields.Count -eq 2 -and
    $workspacePredicateFields -contains 'System.Boolean Piece::m_repairPiece' -and
    $workspacePredicateFields -contains 'System.Boolean Piece::m_removePiece' -and
    $workspacePredicateCalls.Count -eq 1 -and
    $workspacePredicateCalls[0] -match 'UnityEngine.Object::op_Implicit') `
    'Blueprint catalog eligibility must exclude only repair/remove, never spatial flags or StationExtension.'
$refreshBlueprintWorkspaceSelection = $pluginMethods |
    Where-Object Name -eq 'RefreshBlueprintWorkspaceSelection'
$refreshBlueprintWorkspaceSelectionOperands = @(
    $refreshBlueprintWorkspaceSelection.Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($refreshBlueprintWorkspaceSelectionOperands -match
    'PrecisionPlacementSession::SupportsBlueprintWorkspacePiece' -and
    $refreshBlueprintWorkspaceSelectionOperands -notmatch
    'PrecisionPlacementSession::SupportsPrecisionPlacement') `
    'Blueprint workspace still rejects valid parts using world-precision restrictions.'
Assert-Contract ($blueprintRegistrySource -match
    '(?s)"BuildWorks_PlacementBounds".{0,400}layer\s*=\s*prefab\.layer') `
    'Blueprint bounds collider is created on the ghost layer and will be disabled by Valheim.'
$beginBlueprintPlacement = $pluginMethods | Where-Object Name -eq 'BeginBlueprintPlacement'
$beginBlueprintPlacementOperands = @(
    $beginBlueprintPlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($beginBlueprintPlacement.Parameters.Count -eq 3 -and
    $beginBlueprintPlacement.Parameters[2].Name -eq 'editBeforePlacement' -and
    $beginBlueprintPlacementOperands -match 'ArmPlacement' -and
    $beginBlueprintPlacementOperands -match 'SelectBuildPiece' -and
    $beginBlueprintPlacementOperands -match 'ApplyBlueprintAnchors' -and
    $beginBlueprintPlacementOperands -match 'BeginFreeView' -and
    $beginBlueprintPlacementOperands -match
        'HammerBlueprintPieceRegistry::TryGetPlacementOrigin' -and
    $beginBlueprintPlacementOperands -notmatch 'TryBegin\(' -and
    $sessionSource -match
        'BeginBlueprintPlacement\(player, blueprint, editBeforePlacement: true\)' -and
    $sessionSource -notmatch
        'редактор чертежа открывается только кнопкой РЕДАКТИРОВАТЬ') `
    'World blueprint F9 no longer enters its separate whole-group precision path.'
$placementInstructions = $beginBlueprintPlacement.Body.Instructions
$plainPlacementModeReset = $placementInstructions | Where-Object {
    $_.OpCode.Code -eq 'Stfld' -and $_.Operand.Name -eq 'gizmoMode'
}
$resetIndex = if (@($plainPlacementModeReset).Count -eq 1) {
    $placementInstructions.IndexOf($plainPlacementModeReset)
} else { -1 }
$clearLayoutInstruction = $placementInstructions | Where-Object {
    $_.OpCode.FlowControl -eq 'Call' -and $_.Operand.Name -eq 'ClearLayout'
} | Select-Object -First 1
Assert-Contract ($resetIndex -ge 4 -and
    $placementInstructions[$resetIndex - 4].OpCode.Code -eq 'Ldarg_3' -and
    $placementInstructions[$resetIndex - 3].OpCode.Code -in @('Brtrue', 'Brtrue_S') -and
    $placementInstructions[$resetIndex - 3].Operand.Offset -eq $placementInstructions[$resetIndex + 1].Offset -and
    $placementInstructions[$resetIndex - 1].OpCode.Code -eq 'Ldc_I4_0' -and
    $plainPlacementModeReset.Offset -lt $clearLayoutInstruction.Offset) `
    'Normal hammer blueprint placement must clear a previous Repeat/Guide mode before ClearLayout; F9 must retain its operation.'
$applyBlueprintAnchors = $pluginMethods | Where-Object Name -eq 'ApplyBlueprintAnchors'
$applyBlueprintAnchorsOperands = @(
    $applyBlueprintAnchors.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($applyBlueprintAnchorsOperands -match 'AnchorBounds::Anchor' -and
    $applyBlueprintAnchorsOperands -match 'Piece::GetSnapPoints' -and
    $applyBlueprintAnchorsOperands -match 'ExternalCompositeSnapPoints' -and
    $applyBlueprintAnchorsOperands -match 'Part::scale' -and
    $applyBlueprintAnchorsOperands -notmatch 'Blueprint::anchors') `
    'Whole-blueprint precision must combine current scaled prefab native/midpoint snaps after bounds anchors.'
$armPlacement = $pluginMethods | Where-Object Name -eq 'ArmPlacement'
$armPlacementOperands = @(
    $armPlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($armPlacementOperands -match 'RebuildBlueprintPlan') `
    'A blueprint can still arm only its first piece instead of the complete plan.'
Assert-Contract (@($pluginMethods | Where-Object Name -eq 'StageBlueprintPlacement').Count -eq 0) `
    'Legacy F9 staging still couples blueprint editing to world placement.'
$sessionMode = $pluginTypes | Where-Object {
    $_.Name -eq 'SessionMode' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$sessionModeNames = @($sessionMode.Fields | Where-Object IsStatic | ForEach-Object Name)
Assert-Contract (
    $sessionModeNames -contains 'WorldPrecision' -and
    $sessionModeNames -contains 'BlueprintWorldPlacement' -and
    $sessionModeNames -contains 'BlueprintEditor' -and
    $sessionModeNames -notcontains 'BlueprintCapture') `
    'Precision, world blueprint placement, and blueprint workspace are not hard-separated states.'
$beginBlueprintEditor = $pluginMethods | Where-Object Name -eq 'BeginBlueprintEditor'
$beginBlueprintEditorOperands = @(
    $beginBlueprintEditor.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($beginBlueprintEditorOperands -match 'TryResolveBlueprintPieces' -and
    $beginBlueprintEditorOperands -match 'SelectPiece' -and
    $beginBlueprintEditorOperands -match 'BeginFreeView' -and
    $beginBlueprintEditorOperands -match 'AddBlueprintWorkspacePart' -and
    $beginBlueprintEditorOperands -notmatch 'TryBegin\(' -and
    $beginBlueprintEditorOperands -notmatch 'ActivateBlueprint') `
    'Blueprint editor no longer opens a separate Hammer-like temporary workspace.'
$workspaceAdd = $pluginMethods | Where-Object Name -eq 'AddBlueprintWorkspacePart'
$workspaceAddOperands = @(
    $workspaceAdd.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($workspaceAddOperands -match 'PlacementGhostPreviewView::CreateVisualClone' -and
    $workspaceAddOperands -match 'UnityEngine.BoxCollider' -and
    $workspaceAddOperands -notmatch 'UnityEngine.Object::Instantiate' -and
    $workspaceAddOperands -notmatch 'ZNetView') `
    'Blueprint workspace parts are no longer visual-only temporary construction pieces.'
$workspaceSave = $pluginMethods | Where-Object Name -eq 'TrySaveBlueprintWorkspace'
$workspaceSaveOperands = @(
    $workspaceSave.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($workspaceSaveOperands -match 'CompositeBlueprintStore::TrySave' -and
    $workspaceSaveOperands -match 'CompositeBlueprintStore::TryUpdateParts' -and
    $workspaceSaveOperands -match 'AnchorAdjustment::ExternalCompositeSnapPoints') `
    'Blueprint workspace no longer serializes new and edited layouts without world placement.'
$workspaceClear = $pluginMethods | Where-Object Name -eq 'ClearBlueprintWorkspace'
$workspaceClearOperands = @(
    $workspaceClear.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($workspaceClearOperands -match 'UnityEngine.Object::Destroy') `
    'Temporary blueprint workspace objects are no longer destroyed on exit.'
$workspacePrecision = $pluginMethods |
    Where-Object Name -eq 'BeginBlueprintWorkspacePrecision'
$workspacePrecisionOperands = @(
    $workspacePrecision.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$workspacePrecisionEnd = $pluginMethods |
    Where-Object Name -eq 'EndBlueprintWorkspacePrecision'
$workspacePrecisionEndOperands = @(
    $workspacePrecisionEnd.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$disablePrecision = $pluginMethods | Where-Object Name -eq 'DisablePrecision'
$disablePrecisionOperands = @(
    $disablePrecision.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($workspacePrecisionOperands -match 'FindBlueprintWorkspacePart' -and
    $workspacePrecisionOperands -match 'CaptureAnchorPoints' -and
    $workspacePrecisionOperands -notmatch 'ArmPlacement|TryBegin\(' -and
    $workspacePrecisionEndOperands -match 'Transform::SetPositionAndRotation' -and
    $disablePrecisionOperands -match 'EndBlueprintWorkspacePrecision' -and
    $disablePrecisionOperands -match 'CancelBlueprintEditor' -and
    $sessionSource -match 'blueprintWorkspaceEditPart == null' -and
    $sessionSource -match
        'EndBlueprintWorkspacePrecision\(restoreTransform: false\)' -and
    $sessionSource -match
        'EndBlueprintWorkspacePrecision\(restoreTransform: true\)') `
    'Isolated blueprint workspace no longer has reversible F9 precision for one temporary part.'
Assert-Contract ($sessionSource -match 'OpenBlueprintWorkspaceCatalog\(\)' -and
    $sessionSource -match 'blueprintWorkspaceRightTracking' -and
    $sessionSource -match
        '(?s)HandleBlueprintWorkspaceCameraInput\(\s*freeViewCamera,\s*Input\.mousePosition\).*if \(!GameplayInputAvailable\(player\)\)' -and
    $hudSource -match '"WorkspaceCatalog"' -and
    $hudSource -match '"КАТАЛОГ"' -and
    $hudSource -match '"ВЫБРАТЬ В МИРЕ"' -and
    $hudSource -match 'SetButtonVisible\(blueprintSaveText,\s*!blueprintWorkspacePartEditing') `
    'Blueprint workspace no longer has reliable Hammer catalog access.'
$worldCapture = $pluginMethods | Where-Object {
    $_.Name -eq 'TrySaveWorldSelection' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$worldCaptureCalls = @($worldCapture.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$beginWorldSelection = $pluginMethods | Where-Object {
    $_.Name -eq 'BeginWorldBlueprintSelection' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$beginWorldSelectionCalls = @(
    $beginWorldSelection.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($null -ne $worldCapture -and
    $worldCaptureCalls -match 'CompositeBlueprintStore::TrySave' -and
    $worldCaptureCalls -match 'CompositeBlueprintStore::ValidScale' -and
    $worldCaptureCalls -match 'IsBlueprintPieceSelectable' -and
    $worldCaptureCalls -notmatch '::Destroy|::ClaimOwnership|::SetPositionAndRotation|::set_position|::set_rotation|::SetLocalScale' -and
    $sessionSource -match 'pendingWorldBlueprintSelection && GameplayInputAvailable' -and
    $sessionSource -match
        'private void Finish\([^)]*\)\s*\{[\s\S]{0,180}?pendingWorldBlueprintSelection = false;' -and
    $hudSource -match 'confirmText.text = selectingBlueprint \? "СОХРАНИТЬ"' -and
    $sessionSource -match 'if \(selectingBlueprint\)\s*\{\s*SaveCurrentBlueprint\(\);\s*return;' -and
    $sessionSource -match 'TrySaveWorldSelection[\s\S]*?Finish\(\);\s*RefreshBlueprintEditorLibrary' -and
    $catalogSource -match '"ВЫБРАТЬ В МИРЕ"') `
    'World selection save is unreachable, bypasses validation, or mutates world transforms.'
Assert-Contract ($null -ne $beginWorldSelection -and
    $beginWorldSelectionCalls -match 'TrySuspendExternalBuildCamera' -and
    $beginWorldSelectionCalls -match 'BeginFreeView' -and
    $beginWorldSelectionCalls -match 'BeginBlueprintSelection' -and
    $beginWorldSelectionCalls -notmatch 'SelectBuildPiece|UpdatePlacementGhostMethod' -and
    $sessionSource -match
        'pendingWorldBlueprintSelection = false;\s*BeginWorldBlueprintSelection\(player\);' -and
    $sessionSource -match
        'sessionMode = SessionMode\.BlueprintWorldSelection;\s*state = PlacementState\.Editing;' -and
    $sessionSource -match
        'pendingWorldBlueprintSelection \|\| selectingBlueprint \|\|\s*sessionMode == SessionMode\.BlueprintWorldSelection' -and
    $sessionSource -match
        'sessionMode != SessionMode\.BlueprintWorldSelection &&\s*state != PlacementState\.Inactive' -and
    $sessionSource -match
        'sessionMode != SessionMode\.BlueprintWorldSelection && placementGhost' -and
    $sessionSource -match
        'if \(sessionMode == SessionMode\.BlueprintEditor \|\|\s*sessionMode == SessionMode\.BlueprintWorldSelection\)' -and
    $sessionSource -match
        'if \(sessionMode == SessionMode\.BlueprintWorldSelection\)[\s\S]{0,420}?HideWorldSelectionGhost\(player\)' -and
    $sessionSource -match
        'HideWorldSelectionGhost\(Player player\)[\s\S]{0,420}?PlacementGhostField\.GetValue\(player\)[\s\S]{0,240}?hostGhost\.SetActive\(false\)[\s\S]{0,120}?previews\.Hide\(\)' -and
    $sessionSource -match
        'restoreWorldSelectionGhost[\s\S]*?UpdatePlacementGhostMethod\.Invoke' -and
    $sessionSource -match
        'if \(sessionMode == SessionMode\.BlueprintWorldSelection\)\s*\{\s*Finish\(\);') `
    'World selection can still depend on a Hammer ghost, leak placement input, or survive Escape.'
Assert-Contract ($sessionSource -match
    'player\.GetSelectedPiece\(\) != piece &&\s*!blueprintPieceRegistry\.SelectPiece\(player, piece\)') `
    'Automatic blueprint placement can change selection without rebuilding the native ghost.'
$previewShow = $pluginMethods | Where-Object {
    $_.Name -eq 'Show' -and $_.DeclaringType.Name -eq 'PlacementGhostPreviewView'
}
$passiveBlueprintPreview = $pluginMethods |
    Where-Object Name -eq 'ShowPassiveBlueprintPreview'
$passiveBlueprintPreviewOperands = @(
    $passiveBlueprintPreview.Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($previewShow.Parameters.Count -eq 4 -and
    $previewShow.Parameters[3].Name -eq 'hiddenIndex' -and
    $previewSource -match 'bool hideOne = hiddenIndex >= 0' -and
    $sessionSource -match 'ShowPassiveBlueprintPreview' -and
    $sessionSource -notmatch 'passiveBlueprintPieces' -and
    $passiveBlueprintPreviewOperands -notmatch 'Renderer::set_enabled' -and
    $passiveBlueprintPreviewOperands -match 'PlacementGhostPreviewView::Hide') `
    'Blueprint preview overrides source renderer visibility or duplicates the native ghost.'
$beginFreeView = $pluginMethods | Where-Object Name -eq 'BeginFreeView'
$beginFreeViewOperands = @(
    $beginFreeView.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$endFreeView = $pluginMethods | Where-Object Name -eq 'EndFreeView'
$endFreeViewOperands = @(
    $endFreeView.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
$gizmoType = $pluginTypes | Where-Object Name -eq 'TransformGizmoView'
$setGizmoLayer = $gizmoType.Methods | Where-Object Name -eq 'SetLayer'
$setGizmoLayerOperands = @(
    $setGizmoLayer.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($beginFreeViewOperands -match 'Camera::set_cullingMask' -and
    $beginFreeViewOperands -match 'Camera::set_clearFlags' -and
    $beginFreeViewOperands -match 'TransformGizmoView::SetLayer' -and
    $endFreeViewOperands -match 'TransformGizmoView::SetLayer' -and
    $setGizmoLayerOperands -match 'GameObject::set_layer' -and
    $beginFreeViewOperands -match 'ApplyEditorGhostLayer' -and
    $sessionSource -match 'if \(isolatedEditorView\)' -and
    $sessionSource -match 'CreateEditorPlatform\(BlueprintWorkspaceFloorY\(\)\)' -and
    $sessionSource -match 'CreateEditorPlatformMaterial\(\)' -and
    $sessionSource -match 'PrimitiveType\.Plane' -and
    $sessionSource -match 'EditorFloorSize = 2000f' -and
    $sessionSource -match 'Shader\.Find\("Unlit/Color"\)' -and
    $sessionSource -match 'ConfigureEditorLighting\(\)' -and
    $sessionSource -match 'CycleEditorLighting\(\)' -and
    $hudSource -match 'СВЕТ: " \+ editorLighting' -and
    $sessionSource -notmatch 'Shader\.Find\("Standard"\)' -and
    $sessionSource -match 'DisableEditorCameraEffects\(camera\)' -and
    $sessionSource -match 'BeginFreeView\(camera, isolateBlueprint: true\)' -and
    $sessionSource -match 'ClearBlueprintWorkspace\(\)' -and
    $sessionSource -match 'EndFreeView\(restoreCamera: true\)') `
    'Blueprint workspace isolation is no longer separate from ordinary F9 world editing.'
Assert-Contract ($catalogSource -match
    'float inspectorWidth = Mathf\.Clamp\(width \* 0\.27f, 244f, 300f\)' -and
    $catalogSource -match
        'Mathf\.Max\(160f, width - 220f - 18f\)' -and
    $catalogSource -match
        'float cardsWidth = Mathf\.Max\(1f, width - inspectorWidth - 18f\)' -and
    $catalogSource -match 'new Vector2\(16f, y\)') `
    'Blueprint inspector no longer keeps a responsive width and safe inner padding.'
Assert-Contract ($catalogSource -match 'const float actionHeight = 28f' -and
    $catalogSource -match 'const int actionCount = 7' -and
    $catalogSource -match
        'height - actionCount \* actionHeight - 8f') `
    'Blueprint inspector actions can escape below their panel.'
foreach ($inspectorHeight in 358, 477) {
    $actionHeight = 28
    $actionTop = [Math]::Min(184, 86 + $inspectorHeight * 0.20)
    $actionTop = [Math]::Min(
        $actionTop,
        [Math]::Max(86, $inspectorHeight - 7 * $actionHeight - 8))
    Assert-Contract (($actionTop + 7 * $actionHeight) -le ($inspectorHeight - 8)) `
        "Blueprint inspector actions exceed a $inspectorHeight px panel."
}
Assert-Contract ($thumbnailSource -match
    'int alpha = Math\.Max\(pixel\.a, derivedAlpha\)' -and
    $thumbnailSource -match 'texture\.SetPixels32\(pixels\)') `
    'Transparent blueprint thumbnails can reject shaders that render RGB with zero alpha.'
Assert-Contract ($sessionSource -match
    'MovePieceWithoutMovingCameraFocus\(candidate\);[\s\S]{0,120}TryAutoAlignToTouchingPiece\(\)') `
    'Dragging a move arrow can still drag the editor camera together with the object.'
Assert-Contract ($catalogSource -match '"\+ СОЗДАТЬ ЧЕРТЁЖ"' -and
    $sessionSource -match 'HandleCreateBlueprintCatalogAction' -and
    $sessionSource -match 'ContinueBlueprintCreation') `
    'The blueprint library no longer exposes its creation workflow.'
Assert-Contract (([regex]::Matches($sessionSource,
        'suppressPlacementFrame = Time\.frameCount;\s*if \(Hud\.IsPieceSelectionVisible\(\)\) Hud\.CloseBuildUi\(\);')).Count -ge 2 -and
    $sessionSource -match
    'if \(suppressPlacementFrame == Time\.frameCount \|\|\s*Hud\.IsPieceSelectionVisible\(\)\)\s*\{\s*takeInput = false;\s*return;') `
    'Hammer-menu clicks can start or immediately place a selected blueprint.'
$indexSelectionPatch = $pluginType.NestedTypes | Where-Object {
    $_.Name -eq 'HammerCatalogIndexSelectionInputPatch'
}
$indexSelectionPrefix = $indexSelectionPatch.Methods | Where-Object Name -eq 'Prefix'
$indexSelectionOperands = @(
    $indexSelectionPrefix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($pluginSource -match
        'HarmonyPatch\(typeof\(Hud\), "OnLeftClickPiece"\)' -and
    $indexSelectionOperands -match 'HammerCatalogView::get_NativeIndexActive' -and
    $indexSelectionOperands -match 'UnityEngine.Time::get_frameCount' -and
    $indexSelectionOperands -match 'PlayerController::SetTakeInputDelay' -and
    $pluginSource -match
        'suppressHammerSelectionPlacementFrame == Time\.frameCount[\s\S]{0,100}takeInput = false;') `
    'A legacy indexed-Hammer click can still reach world placement in the same frame.'
$continueCatalogSelection = $pluginMethods | Where-Object Name -eq 'ContinueCatalogSelection'
$continueCatalogSelectionOperands = @(
    $continueCatalogSelection.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($continueCatalogSelectionOperands -match
    'PrecisionPlacementSession::UpdatePlacementGhostMethod' -and
    $continueCatalogSelectionOperands -match 'System.Reflection.MethodBase::Invoke' -and
    $continueCatalogSelectionOperands -match 'BeginBlueprintEditor') `
    'Catalog actions no longer split world placement from explicit editor entry.'
$tryBeginWorldPrecision = $pluginMethods | Where-Object Name -eq 'TryBeginWorldPrecision'
$tryBeginWorldPrecisionOperands = @(
    $tryBeginWorldPrecision.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($tryBeginWorldPrecisionOperands -match 'TryBegin' -and
    $tryBeginWorldPrecisionOperands -match 'HammerBlueprintPieceRegistry::TryGetBlueprint' -and
    $tryBeginWorldPrecisionOperands -notmatch 'BeginBlueprintEditor' -and
    $tryBeginWorldPrecisionOperands -notmatch 'ActivateBlueprint') `
    'Explicit F9 can still enter or manipulate the blueprint editor.'
$selectBlueprintArea = $pluginMethods | Where-Object Name -eq 'SelectBlueprintArea'
$selectBlueprintAreaOperands = @(
    $selectBlueprintArea.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($selectBlueprintAreaOperands -match 'Piece::GetAllPiecesInRadius') `
    'Blueprint area selection no longer searches placed world pieces.'
Assert-Contract (@($anchorAdjustment.Methods | Where-Object {
    $_.Name -eq 'ExternalCompositeSnapPoints'
}).Count -eq 1) 'Composite blueprints do not remove internal snap joins.'
$sessionUpdate = $pluginMethods | Where-Object {
    $_.Name -eq 'Update' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$sessionUpdateInstructions = @($sessionUpdate.Body.Instructions)
$gameplayInputCalls = @()
for ($i = 0; $i -lt $sessionUpdateInstructions.Count; ++$i) {
    if ([string]$sessionUpdateInstructions[$i].Operand -match 'GameplayInputAvailable') {
        $gameplayInputCalls += $i
    }
}
Assert-Contract ($gameplayInputCalls.Count -ge 2) `
    'Compiled session update no longer checks active-editor native input.'
Assert-Contract ($sessionSource -match
    'state == PlacementState\.Armed \|\| state == PlacementState\.Frozen\) return;') `
    'Armed series or frozen editor no longer survive native input blocking.'
$validatePlacement = $pluginMethods | Where-Object Name -eq 'ValidateCurrentPlacement'
$validateOperands = @(
    $validatePlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($validateOperands -match 'UpdatePlacementGhost' -and
    $validateOperands -match 'Player::GetPlacementStatus') `
    'Compiled series placement does not revalidate every planned transform.'
$anchorDrag = $pluginMethods | Where-Object Name -eq 'UpdateAnchorDrag'
$anchorDragOperands = @($anchorDrag.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($anchorDragOperands -match 'AnchorAdjustment::ConstrainedAngleDegrees') `
    'Compiled anchor drag does not use tested constrained-angle geometry.'
$undoMethod = $pluginMethods | Where-Object Name -eq 'UndoTransform'
$undoOperands = @($undoMethod.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($undoOperands -match 'BoundedUndoHistory.*::TryUndo') `
    'Compiled editor undo does not use bounded transform history.'
$redoMethod = $pluginMethods | Where-Object Name -eq 'RedoTransform'
$redoOperands = @($redoMethod.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($redoOperands -match 'BoundedUndoHistory.*::TryRedo') `
    'Compiled editor redo does not use bounded transform history.'
Assert-Contract ($sessionSource -match
    'Quaternion layoutDelta = snapshot\.Rotation \* Quaternion\.Inverse\(currentRotation\);\s*repeatStep = layoutDelta \* repeatStep;\s*planeSecondStep = layoutDelta \* planeSecondStep;') `
    'Undo/redo no longer restores local row and plane directions with the source rotation.'
$autoSnap = $pluginMethods | Where-Object Name -eq 'TryFindAlignedSnapPair'
$autoSnapOperands = @($autoSnap.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($autoSnapOperands -match 'Piece::GetSnapPoints' -and
    $sessionSource -match 'AutoSnapDistance = 0\.75f') `
    'Automatic joining no longer searches the target piece snap points.'
$autoAlign = $pluginMethods | Where-Object Name -eq 'TryAutoAlignToTouchingPiece'
$autoAlignOperands = @($autoAlign.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($autoAlignOperands -match 'TryFindAlignedSnapPair' -and
    $autoAlignOperands -match 'AnchorAdjustment::PositionForFixedAnchor') `
    'Automatic joining no longer aligns orientation and position together.'
$passiveAutoJoin = $pluginMethods | Where-Object {
    $_.Name -eq 'ApplyPassiveAutoJoin' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$passiveAutoJoinOperands = @(
    $passiveAutoJoin.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($passiveAutoJoin.Parameters.Count -eq 4 -and
    $passiveAutoJoin.ReturnType.FullName -eq 'System.Boolean' -and
    $passiveAutoJoin.Parameters[1].ParameterType.FullName -eq 'UnityEngine.Transform&' -and
    $passiveAutoJoin.Parameters[2].ParameterType.FullName -eq 'UnityEngine.Transform&' -and
    $passiveAutoJoin.Parameters[3].ParameterType.FullName -eq 'Piece' -and
    $sessionSource -match 'private bool autoAlignmentEnabled = true;' -and
    $sessionSource -match 'ManualSnapPointField\.GetValue\(player\) < 0' -and
    $sessionSource -match
        'if \(!aimedPiece \|\| aimedPiece\.transform == ghost\.transform[\s\S]*?return false;' -and
    $sessionSource -match
        'if \(bestSource && bestTarget\)[\s\S]*?else\s*\{\s*return false;\s*\}' -and
    $sessionSource -match
        'targetPiece\.transform\.rotation \* ghost\.transform\.rotation;' -and
    $passiveAutoJoinOperands -match 'Player::get_AlternativePlacementActive' -and
    $passiveAutoJoinOperands -match 'Piece::GetSnapPoints' -and
    $passiveAutoJoinOperands -match 'Transform::IsChildOf' -and
    $passiveAutoJoinOperands -match 'Quaternion::op_Multiply' -and
    $passiveAutoJoinOperands -match 'Transform::set_rotation') `
    'Passive auto-join no longer rejects foreign fallback targets in automatic wheel rotation.'
$meshEdges = $pluginMethods | Where-Object Name -eq 'GetMeshFeatureEdges'
$meshEdgeOperands = @($meshEdges.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($meshEdgeOperands -match 'AnchorAdjustment::ExtractFeatureEdges' -and
    $meshEdgeOperands -match 'Mesh::GetIndexCount' -and
    $meshEdgeOperands -match 'Mesh::get_vertexCount') `
    'Compiled magnetic snapping does not use tested real-mesh feature edges.'
$addMeshEdges = $pluginMethods | Where-Object Name -eq 'AddMeshSnapEdges'
$addMeshOperands = @($addMeshEdges.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($addMeshOperands -match 'LODGroup::GetLODs') `
    'Compiled magnetic snapping does not filter mesh renderers by LOD.'
$captureAnchors = $pluginMethods | Where-Object Name -eq 'CaptureAnchorPoints'
$captureOperands = @($captureAnchors.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($captureOperands -match 'Piece::GetSnapPoints' -and
    $captureOperands -match 'Quaternion::Inverse') `
    'Compiled active-piece handles do not include native local snap points.'
$findSnap = $pluginMethods | Where-Object Name -eq 'TryFindSnapTarget'
$findSnapOperands = @($findSnap.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($findSnapOperands -match 'freeSnapTargets' -and
    $findSnapOperands -match 'snapEdges' -and
    $findSnapOperands -match 'snapPreviewTargets' -and
    $findSnapOperands -match 'AddSnapPreviewTarget' -and
    $findSnapOperands -match 'CullHiddenSnapPreviewTargets' -and
    $findSnapOperands -match 'IsSnapPreviewTarget') `
    'Compiled magnetic target policy does not expose point and edge targets.'
$visibilityCull = $pluginMethods | Where-Object Name -eq 'CullHiddenSnapPreviewTargets'
$visibilityCullOperands = @(
    $visibilityCull.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($visibilityCullOperands -match 'IsPointVisible') `
    'Compiled magnetic preview no longer filters its bounded candidate list by visibility.'
$pointVisibility = $pluginMethods | Where-Object Name -eq 'IsPointVisible'
$pointVisibilityOperands = @(
    $pointVisibility.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($pointVisibilityOperands -match 'Physics::RaycastNonAlloc') `
    'Compiled camera visibility check allocates physics hit arrays in the drag hot path.'
$gizmoShow = $pluginMethods | Where-Object {
    $_.Name -eq 'Show' -and $_.DeclaringType.Name -eq 'TransformGizmoView'
}
$gizmoShowOperands = @(
    $gizmoShow.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($gizmoShowOperands -match 'IsPointVisible' -and
    $gizmoShowOperands -match 'visibleAnchors') `
    'Compiled contextual handles do not share camera visibility with hit testing.'
$refreshSnap = $pluginMethods | Where-Object Name -eq 'RefreshSnapTargets'
$refreshOperands = @($refreshSnap.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($refreshOperands -match 'Piece::GetSnapPoints' -and
    $refreshOperands -match 'Piece::GetAllPiecesInRadius' -and
    $refreshOperands -match 'TryCaptureAnchorBounds' -and
    $refreshOperands -match 'AddMeshSnapEdges' -and
    $refreshOperands -match 'meshSnapEnabled') `
    'Compiled magnetic snap discovery does not use current Piece APIs.'
Assert-Contract ($sessionSource -match
    'if \(dragMagneticMove\)[\s\S]*?currentRotation = dragStartRotation;' -and
    $sessionSource -notmatch 'Input\.GetKey\(KeyCode\.R\)') `
    'Magnetic drag is no longer restricted to position-only point snapping.'
$autoAlignment = $pluginMethods | Where-Object Name -eq 'TryAutoAlignToTouchingPiece'
$autoAlignmentOperands = @(
    $autoAlignment.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($autoAlignmentOperands -match 'TryGetPlacedPieceAt' -and
    $autoAlignmentOperands -match 'Collider::ClosestPoint' -and
    $autoAlignmentOperands -match 'AnchorAdjustment::PositionForFixedAnchor') `
    'Compiled automatic alignment does not use the pointed piece, contact geometry, and a fixed source anchor.'
$midpointType = $pluginTypes | Where-Object Name -eq 'VanillaMidpointSnapPoints'
Assert-Contract ($null -ne $midpointType) 'Compiled vanilla midpoint helper is missing.'
$midpointAppend = $midpointType.Methods | Where-Object Name -eq 'Append'
Assert-Contract ($null -ne $midpointAppend) 'Compiled vanilla midpoint append method is missing.'
$midpointOperands = @(
    $midpointAppend.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($midpointOperands -match 'AnchorAdjustment::ConnectableSnapEdges' -and
    $midpointOperands -match 'GameObject::set_tag') `
    'Compiled midpoint helper does not create real tagged Valheim snap points.'
$midpointRemove = $midpointType.Methods | Where-Object Name -eq 'RemoveAll'
Assert-Contract ($null -ne $midpointRemove) 'Compiled vanilla midpoint cleanup is missing.'
$pluginOnDestroy = $pluginMethods | Where-Object {
    $_.Name -eq 'OnDestroy' -and $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
$pluginOnDestroyOperands = @(
    $pluginOnDestroy.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($pluginOnDestroyOperands -match 'VanillaMidpointSnapPoints::RemoveAll') `
    'Plugin unload no longer removes generated vanilla snap points.'
$midpointPatch = $pluginTypes | Where-Object Name -eq 'VanillaMidpointSnapPointPatch'
Assert-Contract ($null -ne $midpointPatch) 'Piece.GetSnapPoints Harmony patch is missing.'
$midpointPostfix = $midpointPatch.Methods | Where-Object Name -eq 'Postfix'
Assert-Contract ($null -ne $midpointPostfix) 'Piece.GetSnapPoints Harmony postfix is missing.'
$midpointPatchOperands = @(
    $midpointPostfix.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($midpointPatchOperands -match 'VanillaMidpointSnapPoints::Append') `
    'Piece.GetSnapPoints patch no longer appends vanilla midpoint snaps.'
$showSnapCandidates = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowSnapCandidates' -and
    $_.DeclaringType.Name -eq 'TransformGizmoView'
}
Assert-Contract ($showSnapCandidates.Parameters.Count -eq 4 -and
    $showSnapCandidates.Parameters[2].ParameterType.FullName -eq
        'System.Collections.Generic.IReadOnlyList`1<System.Boolean>') `
    'Compiled magnetic preview no longer distinguishes native target points.'
$showFrozen = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowFrozen' -and $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$showFrozenOperands = @(
    $showFrozen.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($showFrozenOperands -match 'ПОЛОЖЕНИЕ ЗАМОРОЖЕНО') `
    'Compiled HUD has no explicit frozen editor state.'
Assert-Contract ($showFrozen.Parameters.Count -eq 0) `
    'Ordinary F9 frozen HUD still accepts blueprint-editor state.'
Assert-Contract ($sessionSource -match
    'private bool showAllAnchors;' -and
    $sessionSource -notmatch 'meshAnchorsEnabled') `
    'Anchor visibility no longer starts in the uncluttered nearby-only mode.'
$contourBuild = $pluginMethods | Where-Object Name -eq 'TryBuildContourFromClick'
$contourBuildOperands = @(
    $contourBuild.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($contourBuildOperands -match 'UpdateContourHover' -and
    $contourBuildOperands -match 'List`1<.*ContourSupport>::AddRange' -and
    $contourBuildOperands -match 'RebuildContourPlan' -and
    $contourBuildOperands -match 'Quaternion::Inverse' -and
    $contourBuildOperands -notmatch 'MovePieceWithoutMovingCameraFocus|HitTestAnchor') `
    'Compiled contour click no longer commits the highlighted chain through the normal contour plan.'
$contourDiscovery = $pluginMethods | Where-Object Name -eq 'TryFindContourCandidate'
$contourDiscoveryOperands = @(
    $contourDiscovery.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($contourDiscoveryOperands -match 'Piece::GetAllPiecesInRadius' -and
    $contourDiscoveryOperands -match 'ConstructionLayout::OrderConnectedContour' -and
    $contourDiscoveryOperands -match 'ConstructionLayout::OrderTouchingContour' -and
    $contourDiscoveryOperands -match 'TrySelectContourEdge') `
    'Compiled contour preview no longer uses nearby built pieces and tested chain/ring ordering.'
$contourHover = $pluginMethods | Where-Object Name -eq 'UpdateContourHover'
$contourHoverOperands = @(
    $contourHover.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($contourHoverOperands -match 'TryFindContourCandidate' -and
    $contourHoverOperands -match 'RebuildContourPath' -and
    $contourHoverOperands -match 'Time::get_unscaledTime') `
    'Compiled contour mode no longer previews the same candidate used by click selection.'
$showLayoutPreview = $pluginMethods | Where-Object Name -eq 'ShowLayoutPreview'
$showLayoutPreviewOperands = @(
    $showLayoutPreview.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($showLayoutPreviewOperands -match 'contourHoverPath' -and
    $showLayoutPreviewOperands -match 'TransformGizmoView::ShowLayout') `
    'Compiled contour preview is no longer sent to the editor gizmo.'
Assert-Contract ($sessionSource -match
    'Piece\.GetAllPiecesInRadius\(seed\.transform\.position, ContourSearchRadius, nearbyPieces\)') `
    'Contour discovery no longer uses its large-ring search radius.'
$contourPlan = $pluginMethods | Where-Object Name -eq 'RebuildContourPlan'
$contourPlanOperands = @(
    $contourPlan.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($contourPlanOperands -match 'ContourSupport::get_Position' -and
    $contourPlanOperands -match 'ContourSupport::get_Rotation' -and
    $contourPlanOperands -match 'contourGhostPositionLocal' -and
    $contourPlanOperands -match 'contourGhostRotationLocal' -and
    $contourPlanOperands -match 'contourClosed') `
    'Compiled contour placement does not preserve the full source transform.'
Assert-Contract ($sessionSource -notmatch 'guideContactAnchor' -and
    $sessionSource -match 'contourSeedSupportIndex') `
    'Contour does not keep the clicked support as the visible first placement.'
$selectContourEdge = $pluginMethods | Where-Object Name -eq 'TrySelectContourEdge'
$selectContourEdgeOperands = @(
    $selectContourEdge.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($selectContourEdgeOperands -match 'Piece::GetSnapPoints' -and
    $selectContourEdgeOperands -match 'AnchorAdjustment::ConnectableSnapEdges' -and
    $selectContourEdgeOperands -match 'Camera::WorldToScreenPoint' -and
    $selectContourEdgeOperands -match 'TransformGizmoView::IsPointVisible' -and
    $selectContourEdgeOperands -match 'AnchorAdjustment::PerspectiveSegmentParameter' -and
    $selectContourEdgeOperands -match 'VanillaMidpointSnapPoints::IsGenerated') `
    'Contour no longer follows the visible screen edge between native Valheim snap points.'
$repeatPlan = $pluginMethods | Where-Object Name -eq 'RebuildRepeatPlan'
$repeatPlanOperands = @(
    $repeatPlan.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($repeatPlanOperands -match 'GuidePathSampling::SampleRepeat' -and
    $repeatPlanOperands -match 'RepeatHingeAnchors' -and
    $repeatPlanOperands -match 'planeSecondStep' -and
    $repeatPlanOperands -match 'RepeatStepAxisAngle') `
    'Compiled array plan does not preserve row sampling while adding a second axis.'
Assert-Contract ($gizmoSource -match
    'Quaternion\.AngleAxis\(yaw, Vector3\.up\)' -and
    $gizmoSource -match 'Quaternion\.AngleAxis\(pitch, pitchAxis\)' -and
    $gizmoSource -match 'Quaternion\.AngleAxis\(roll, tangent\)') `
    'Three-axis array no longer composes turn, pitch, and movement-axis roll.'
Assert-Contract ($sessionSource -match
    'PrecisionAdjustment\.NormalizeDegrees\(angle \+ delta\)') `
    'Free array-angle adjustment no longer accepts arbitrary numeric values.'
Assert-Contract ($hudSource -match 'input\.onValueChanged\.AddListener\(PreviewTextEdit\)' -and
    $hudSource -match 'input\.selectionAnchorPosition = 0' -and
    $hudSource -match 'input\.wasCanceled') `
    'Blender-style direct angle entry no longer previews, replaces, and cancels safely.'
$repeatStep = $pluginMethods | Where-Object Name -eq 'RepeatStepLength'
$repeatStepOperands = @(
    $repeatStep.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($repeatStepOperands -match 'ProjectedAnchorSpan') `
    'Compiled automatic repeat spacing does not prefer native snap-point span.'
$preparePlacement = $pluginMethods | Where-Object {
    $_.Name -eq 'PrepareNativePlacementUpdate' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$prepareOperands = @(
    $preparePlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($prepareOperands -match 'Player::HaveRequirements' -and
    $prepareOperands -match 'ArmPlacement' -and
    $prepareOperands -match 'ValidateCurrentPlacement' -and
    $prepareOperands -match 'LastToolUseTimeField' -and
    $prepareOperands -match 'GameplayInputAvailable' -and
    $prepareOperands -match 'automaticPlacementRunning') `
    'Compiled continuous placement does not retain requirements, arming, cooldown release, and per-copy validation.'
$skipBlocked = $pluginMethods | Where-Object Name -eq 'TrySkipPlayerBlockedPlacement'
$skipBlockedOperands = @(
    $skipBlocked.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($sessionSource -match 'Player\.PlacementStatus\.BlockedbyPlayer' -and
    $skipBlockedOperands -match 'Player::GetPlacementStatus' -and
    $skipBlockedOperands -match 'placementPlanIndex' -and
    $skipBlockedOperands -match 'requestAutomaticPlacement') `
    'Player-blocked array cells no longer advance to the next planned placement.'
foreach ($methodName in @('PrepareNativePlacementUpdate', 'OnNativePlacementFinished',
    'ArmPlacement')) {
    $method = $pluginMethods | Where-Object Name -eq $methodName
    $operands = @($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($operands -match 'TrySkipPlayerBlockedPlacement') `
        "$methodName no longer preserves the array after a player-blocked cell."
}
Assert-Contract ($sessionSource -match
    'automaticWaitingForTool\s*&&\s*!HasUsableBuildTool\(player\)\s*\)\s*\{\s*return;\s*\}' -and
    $sessionSource -match 'SelectPlanPiece\(player, placementPlanIndex\)') `
    'Broken-tool recovery no longer waits for the tool and restores the required blueprint piece.'
Assert-Contract ($sessionSource -match
    'automaticWaitingForTool = true;\s*SaveAndUnlockCursor\(\);' -and
    $sessionSource -match
    'automaticWaitingForTool = false;\s*RestoreCursor\(\);') `
    'Broken-tool recovery no longer releases and restores the normal cursor.'
Assert-Contract ($sessionSource -match
    'contourGhostPositionLocal = inverse \* \(currentPosition - seed\.Position\);') `
    'Contour group editing contract is missing.'
$constructionLayout = $geometry.MainModule.Types | Where-Object {
    $_.FullName -eq 'OstrixMods.BuildWorks.Geometry.ConstructionLayout'
}
$maximumContourCopies = $constructionLayout.Fields | Where-Object Name -eq 'MaximumContourCopies'
Assert-Contract ($null -ne $maximumContourCopies -and
    $maximumContourCopies.HasConstant -and
    [int]$maximumContourCopies.Constant -eq 128) `
    'Contour capacity no longer supports a 72-piece five-degree ring.'
$maximumCopies = $constructionLayout.Fields | Where-Object Name -eq 'MaximumCopies'
Assert-Contract ($null -ne $maximumCopies -and
    $maximumCopies.HasConstant -and
    [int]$maximumCopies.Constant -eq 128) `
    'Rows and planes are capped below 128 pieces again.'
$automaticDriver = $pluginMethods | Where-Object {
    $_.Name -eq 'ContinueAutomaticPlacement' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$automaticDriverOperands = @(
    $automaticDriver.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($automaticDriverOperands -match 'PlacePressedTimeField' -and
    $automaticDriverOperands -match 'UpdatePlacementMethod' -and
    $automaticDriverOperands -match 'System.Reflection.MethodBase::Invoke' -and
    $automaticDriverOperands -match 'automaticPlacementReachedTryPlace' -and
    $automaticDriverOperands -match 'HasAutomaticPlacementStamina' -and
    $automaticDriverOperands -match 'HasUsableBuildTool' -and
    $automaticDriverOperands -match 'automaticWaitingForStamina' -and
    $automaticDriverOperands -match 'automaticWaitingForTool' -and
    $automaticDriverOperands -match 'PlacementSelectionStillValid' -and
    $automaticDriverOperands -match 'GameplayInputAvailable' -and
    $automaticDriverOperands -match '-9999') `
    'Compiled series driver does not execute and verify the native placement path.'
$automaticDriverOperandList = @(
    $automaticDriver.Body.Instructions | ForEach-Object { [string]$_.Operand }
)
$toolCheck = [Array]::IndexOf($automaticDriverOperandList,
    'System.Boolean OstrixMods.BuildWorks.PrecisionPlacementSession::HasUsableBuildTool(Player)')
$selectionCheck = [Array]::IndexOf($automaticDriverOperandList,
    'System.Boolean OstrixMods.BuildWorks.PrecisionPlacementSession::PlacementSelectionStillValid(Player)')
Assert-Contract ($toolCheck -ge 0 -and $selectionCheck -gt $toolCheck) `
    'Broken-tool wait no longer protects a temporarily missing placement context.'
$usableTool = $pluginMethods | Where-Object Name -eq 'HasUsableBuildTool'
$usableToolOperands = @(
    $usableTool.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($usableToolOperands -match 'm_buildPieces' -and
    $usableToolOperands -match 'm_useDurability' -and
    $usableToolOperands -match 'm_durability') `
    'Build-tool recovery no longer checks tool identity and durability.'
$hudShowArmed = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowArmed' -and $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$hudShowArmedOperands = @(
    $hudShowArmed.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($hudShowArmedOperands -match 'ЗАМЕНИ МОЛОТОК' -and
    $hudShowArmedOperands -match 'ВЫБЕРИ ПРЕЖНЮЮ ДЕТАЛЬ' -and
    $hudShowArmedOperands -match 'ЖДЁМ ВЫНОСЛИВОСТЬ') `
    'Armed HUD no longer distinguishes broken-tool and stamina waits.'
$hudCreateText = $pluginMethods | Where-Object {
    $_.Name -eq 'CreateText' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$hudCreateTextOperands = @(
    $hudCreateText.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($hudCreateTextOperands -match 'UnityEngine.Object::Instantiate') `
    'Precision HUD labels no longer inherit the native Valheim font template.'
Assert-Contract ($hudSource -match
    'background\.raycastTarget = false;') `
    'Precision HUD background can intercept inventory clicks during a tool wait.'
Assert-Contract ($sessionSource -match
    'установлено " \+ placementPlanIndex \+ "/" \+\s*placementPlan\.Count \+ " — возьми исправный молоток') `
    'Broken Hammer wait is silent and can look like a missing blueprint part.'
Assert-Contract ($sessionSource -match
    'if \(!SelectPlanPiece\(player, placementPlanIndex\)\)\s*\{\s*if \(automaticWaitingForTool\)\s*\{\s*UpdateHud\(\);\s*return;') `
    'Selecting another build tool can still destroy a waiting blueprint queue.'
$hudShowPassive = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowPassive' -and $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$passivePanelWidth = @($hudShowPassive.Body.Instructions | Where-Object {
    $_.OpCode.Name -eq 'ldc.r4' -and [single]$_.Operand -eq 580
})
Assert-Contract ($passivePanelWidth.Count -ge 1) `
    'Passive HUD background no longer covers its 560-pixel text fields.'
$armPlacement = $pluginMethods | Where-Object {
    $_.Name -eq 'ArmPlacement' -and
    $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$armPlacementOperands = @(
    $armPlacement.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($armPlacementOperands -match 'requestAutomaticPlacement') `
    'The HUD Install action no longer starts the native placement series directly.'
Assert-Contract ($sessionSource -match
    'BuildWorks: чертёж установлен — " \+\s*placementPlan\.Count \+ " деталей') `
    'Completed blueprint placement no longer reports the confirmed native piece count.'
$pluginUpdate = $pluginMethods | Where-Object {
    $_.Name -eq 'Update' -and $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
$pluginUpdateOperands = @(
    $pluginUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }
)
$sessionUpdateCall = [Array]::IndexOf($pluginUpdateOperands, 'System.Void OstrixMods.BuildWorks.PrecisionPlacementSession::Update()')
$continueCall = [Array]::IndexOf($pluginUpdateOperands, 'System.Void OstrixMods.BuildWorks.PrecisionPlacementSession::ContinueAutomaticPlacement(Player)')
Assert-Contract ($continueCall -ge 0 -and $sessionUpdateCall -gt $continueCall) `
    'Queued native placement can still be erased by the generic session update.'
$interactionHitTest = $pluginMethods | Where-Object Name -eq 'IsEditorInteractionAt'
$interactionOperands = @(
    $interactionHitTest.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($interactionOperands -match 'HitTestAnchor' -and
    $interactionOperands -match 'HitTestLayout') `
    'Compiled placement guard does not exclude anchors and guide preview lines.'
$lateUpdate = $pluginMethods | Where-Object {
    $_.Name -eq 'LateUpdate' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$lateUpdateOperands = @($lateUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($lateUpdateOperands -match 'PlacementGhostPreviewView::Show' -and
    $lateUpdateOperands -match 'TransformGizmoView::ShowSnapCandidates') `
    'Compiled layout preview is not fed from the placement session plan.'
$previewType = $pluginTypes | Where-Object Name -eq 'PlacementGhostPreviewView'
Assert-Contract ($null -ne $previewType) 'Compiled renderer-only placement preview is missing.'
$previewOperands = @(
    $previewType.Methods | ForEach-Object Body | ForEach-Object Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($previewOperands -match 'MeshFilter' -and $previewOperands -match 'MeshRenderer') `
    'Placement preview does not construct renderer-only visual copies.'
Assert-Contract ($previewOperands -match 'Renderer::set_enabled') `
    'Placement preview does not preserve source renderer enabled state for LOD safety.'
Assert-Contract ($previewOperands -match 'LODGroup::GetLODs') `
    'Placement preview does not explicitly restrict copied LOD renderers to LOD0.'
Assert-Contract ($previewOperands -notmatch 'Object::Instantiate|ZNetView|WearNTear|Collider|Rigidbody|MonoBehaviour') `
    'Placement preview references unsafe gameplay, network, or physics cloning APIs.'
$passiveEntry = $pluginMethods | Where-Object Name -eq 'TryBeginPassiveAxisDrag'
$passiveOperands = @($passiveEntry.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($passiveOperands -match 'HitTest' -and $passiveOperands -match 'TryBegin') `
    'Passive axis drag does not enter the existing precision editor safely.'

Assert-Contract (@($pluginTypes | Where-Object Name -like 'Adaptive*').Count -eq 0) `
    'Retired adaptive panel runtime types leaked into the core plugin.'
Assert-Contract (@($geometry.MainModule.Types | Where-Object Name -like 'Adaptive*').Count -eq 0) `
    'Retired adaptive panel geometry leaked into the core geometry assembly.'
Assert-Contract (-not (@($plugin.MainModule.AssemblyReferences | ForEach-Object Name) -contains 'Jotunn')) `
    'BuildWorks must not depend on Jotunn.'
Assert-Contract (-not (@($plugin.MainModule.AssemblyReferences | ForEach-Object Name) -contains 'Build Camera')) `
    'Build Camera compatibility must remain optional and reflection-only.'
$pluginEntry = $pluginTypes | Where-Object Name -eq 'BuildWorksPlugin'
Assert-Contract (@($pluginEntry.Methods | Where-Object Name -eq 'OnGUI').Count -eq 0) `
    'Standalone IMGUI entry point still exists.'
Assert-Contract ($pluginSource -match 'SkyCleanupWorld = "TerrainRamp_Lab"' -and
    $pluginSource -match 'new Vector3\(69\.24f, 214\.63f, 335\.88f\)' -and
    $pluginSource -match 'new Vector3\(68\.74f, 214\.63f, 335\.68f\)' -and
    $pluginSource -match 'new Vector3\(68\.24f, 214\.63f, 335\.88f\)' -and
    $pluginSource -match '0\.25f \* 0\.25f' -and
    $pluginSource -notmatch 'GetGroundHeight') `
    'One-shot cleanup is no longer restricted to the three exact legacy sky pieces.'
Assert-Contract ($sessionSource -match 'PrecisionAdjustment\.ScreenAlignedRotationDelta' -and
    $sessionSource -match 'camera\.transform\.position - currentPosition') `
    'Rotation drag direction no longer accounts for the camera side.'
Assert-Contract ($sessionSource -match
    'precisionEnabled = true;\s*if \(!TryBeginWorldPrecision\(player, showErrors: true, useContinuation: false\)\)') `
    'F9 no longer opens the editor directly from the inactive state.'
$tryBegin = $pluginMethods | Where-Object {
    $_.Name -eq 'TryBegin' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$tryBeginOperands = @($tryBegin.Body.Instructions | ForEach-Object { [string]$_.Operand })
$suspendCamera = [Array]::IndexOf($tryBeginOperands,
    'System.Boolean OstrixMods.BuildWorks.PrecisionPlacementSession::TrySuspendExternalBuildCamera()')
$beginFreeView = [Array]::IndexOf($tryBeginOperands,
    'System.Void OstrixMods.BuildWorks.PrecisionPlacementSession::BeginFreeView(UnityEngine.Camera,System.Boolean)')
Assert-Contract ($suspendCamera -ge 0 -and $beginFreeView -gt $suspendCamera) `
    'F9 does not suspend an active external build camera before starting BuildWorks free view.'
$finish = $pluginMethods | Where-Object {
    $_.Name -eq 'Finish' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession'
}
$finishOperands = @($finish.Body.Instructions | ForEach-Object { [string]$_.Operand })
$endFreeView = [Array]::IndexOf($finishOperands,
    'System.Void OstrixMods.BuildWorks.PrecisionPlacementSession::EndFreeView(System.Boolean)')
$restoreCamera = [Array]::IndexOf($finishOperands,
    'System.Void OstrixMods.BuildWorks.PrecisionPlacementSession::RestoreExternalBuildCamera()')
Assert-Contract ($endFreeView -ge 0 -and $restoreCamera -gt $endFreeView) `
    'BuildWorks does not restore its captured camera before resuming the external build camera.'
foreach ($methodName in @('TrySuspendExternalBuildCamera', 'RestoreExternalBuildCamera',
    'ResolveExternalBuildCamera')) {
    Assert-Contract (@($pluginMethods | Where-Object Name -eq $methodName).Count -eq 1) `
        "Compiled camera compatibility method is missing: $methodName"
}

if (Test-Path -LiteralPath $buildCameraPath -PathType Leaf) {
    $buildCameraAssembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($buildCameraPath)
    $buildCameraUtils = $buildCameraAssembly.MainModule.Types | Where-Object {
        $_.FullName -eq 'Valheim_Build_Camera.Utils'
    }
    Assert-Contract ($null -ne $buildCameraUtils) 'Installed Build Camera has no Utils type.'
    foreach ($methodName in @('InBuildMode', 'DisableBuildMode', 'EnableBuildMode')) {
        Assert-Contract (@($buildCameraUtils.Methods | Where-Object {
            $_.Name -eq $methodName -and $_.IsStatic -and $_.Parameters.Count -eq 0
        }).Count -eq 1) "Installed Build Camera contract changed: $methodName()."
    }
    $buildCameraPlugin = $buildCameraAssembly.MainModule.Types | Where-Object {
        $_.FullName -eq 'Valheim_Build_Camera.Valheim_Build_CameraPlugin'
    }
    Assert-Contract (@($buildCameraPlugin.Fields | Where-Object {
        $_.Name -eq 'buildCameraViewDirection' -and $_.IsStatic
    }).Count -eq 1) 'Installed Build Camera no longer exposes its saved view state.'
}

Write-Output 'PASS: current Valheim host contract matches BuildWorks 0.19.33 and Valheim Steam build 25185596.'
