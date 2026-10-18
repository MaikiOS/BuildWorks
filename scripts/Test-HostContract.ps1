[CmdletBinding()]
param(
    [string] $ValheimManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed',
    [string] $ProfileRoot = ''
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
if ([string]::IsNullOrWhiteSpace($ProfileRoot)) {
    $ProfileRoot = Join-Path (Split-Path -Parent (Split-Path -Parent $projectRoot)) 'TerrainRamp-1.0-Test'
}
$profileRoot = [IO.Path]::GetFullPath($ProfileRoot)
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
    'menu', 'close', 'gizmo-move', 'gizmo-plane', 'gizmo-rotate', 'gizmo-scale', 'gizmo-source', 'gizmo-target',
    'snap-native', 'snap-helper', 'snap-selected', 'snap-geometry'
)
$buildCameraPath = Join-Path $profileRoot `
    'BepInEx\plugins\Matheba-Build_Camera_Custom_Hammers_Edition\Build Camera.dll'

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

function Get-NativeLayoutHash($method) {
    $layout = ($method.Body.Instructions | ForEach-Object ToString) -join "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    try { return [BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($layout))).Replace('-', '') }
    finally { $sha.Dispose() }
}

foreach ($iconName in $requiredEditorIcons) {
    $iconPath = Join-Path $editorIconRoot ($iconName + '.png')
    $image = $null
    $bitmap = $null
    try {
        $image = [Drawing.Image]::FromFile($iconPath)
        $bitmap = [Drawing.Bitmap]::new($image)
        [void] $bitmap.GetPixel(0, 0)
        $expectedSize = if ($iconName -like 'snap-*') { 1254 } else { 128 }
        Assert-Contract ($bitmap.Width -eq $expectedSize -and $bitmap.Height -eq $expectedSize) `
            "Blueprint Editor icon must be ${expectedSize}x${expectedSize}: $iconPath"
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
$blueprintFrameSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src/BuildWorks/BlueprintPlacementFrame.cs') -Raw
$prefabMetadataSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src/BuildWorks/BlueprintPrefabMetadataCapture.cs') -Raw
$catalogSource = Get-Content -LiteralPath $catalogSourcePath -Raw
$blueprintStoreSource = Get-Content -LiteralPath $blueprintStoreSourcePath -Raw
$thumbnailSource = Get-Content -LiteralPath $thumbnailSourcePath -Raw
$previewSource = Get-Content -LiteralPath $previewSourcePath -Raw
$midpointSource = Get-Content -LiteralPath $midpointSourcePath -Raw
$editorDocumentSource = Get-Content -LiteralPath $editorDocumentSourcePath -Raw
$editorSceneSource = Get-Content -LiteralPath $editorSceneSourcePath -Raw
$editorViewSource = Get-Content -LiteralPath $editorViewSourcePath -Raw
$editorViewSource += Get-Content -LiteralPath (Join-Path $projectRoot 'src\BuildWorks\BlueprintEditorCatalogView.cs') -Raw
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
    $editorViewSource -match 'editor\.view\.blueprint_frame' -and
    $editorViewSource -match 'editor\.view\.group_pivot') `
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
    $editorViewSource -match 'BlueprintCatalogLayout\.Compute' -and
    $editorViewSource -match 'OpenCatalogPicker' -and
    $editorViewSource -match 'CatalogFilter\.Material' -and
    $editorViewSource -match 'CatalogFilter\.Source' -and
    $editorViewSource -match 'catalogPartFilters' -and
    $editorViewSource -match 'catalogBlueprintFilters' -and
    $editorViewSource -match 'Matches\("#" \+ \(item\.Index \+ 1\), f.Query\)' -and
    $editorViewSource -match 'CatalogPartSelected\?\.Invoke\(captured, true\)' -and
    $editorViewSource -notmatch 'CatalogAdd|CatalogDoubleClick' -and
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
    $editorViewSource -match 'inspectorScroll\.viewport = inspectorViewport' -and
    $editorViewSource -match 'inspectorScroll\.content = inspectorBody' -and
    $editorViewSource -match 'inspectorScroll\.horizontal = false' -and
    $editorViewSource -match 'inspectorScroll\.movementType = ScrollRect\.MovementType\.Clamped' -and
    $editorViewSource -match 'CreateRect\(\s*"CatalogPickerViewport"' -and
    $editorViewSource -match 'scroll\.content = content' -and
    $editorViewSource -match 'catalogPanel\.localScale = Vector3.one' -and
    $editorViewSource -match 'grid\.constraintCount = catalogLayout.Columns' -and
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
    $editorViewSource -match 'editor\.view\.points_all' -and
    $editorViewSource -match 'editor\.view\.magnet_game' -and
    $gizmoSource -match 'camera\.cullingMask' -and
    $editorControllerSource -match 'if \(IsGizmoDragging\)[\s\S]*?return false;' -and
    $editorControllerSource -match '\(!IsGizmoDragging \|\| keyboardPreview\) &&\s*input\.GetMouseButtonDown\(1\)' -and
    $editorControllerSource -match '!input\.HasFocus[\s\S]*?IsGizmoDragging\) CancelGizmoDrag' -and
    $editorControllerSource -match 'ReadKeyboardNumber' -and
    $editorControllerSource -match 'PreviewKeyboardNumber' -and
    $editorControllerSource -match 'middleDeleteTarget' -and
    $editorControllerSource -match 'scene\.PreviewTransform\(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale\)' -and
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
    $previewSource -match 'sourceSkinned\.BakeMesh\(baked, true\)' -and
    $previewSource -match 'VisualCloneOwnedAssets' -and
    $previewSource -match 'owned\.Track\(baked\)') `
    'Visual clone does not preserve skinned-only prefab geometry safely.'
Assert-Contract ($pluginSource -match
        'if \(blueprintEditor\?\.BlocksWorldInput == true\)[\s\S]{0,400}blueprintEditor\.IsOpen[\s\S]{0,80}blueprintEditor\.Update\(\);[\s\S]{0,80}return;' -and
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
        $scopeName -notin @('assembly_valheim', 'assembly_utils', 'assembly_guiutils', 'Splatform')) {
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
    $scaleWrapperCalls.IndexOf('PrecisionPlacementSession::SharesNativePlacementData') -ge 0 -and
    $scaleWrapperCalls.IndexOf('PrecisionPlacementSession::SharesNativePlacementData') -lt
        $scaleWrapperCalls.IndexOf('ZNetScene::Destroy') -and
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
$rotationState = $plugin.MainModule.Types | Where-Object Name -eq 'BlueprintRotationState'
$rotationWriter = $rotationState.Methods | Where-Object Name -eq 'ApplyCreated'
$rotationReader = $rotationState.Methods | Where-Object Name -eq 'TryRestore'
$rotationChange = $rotationState.Methods | Where-Object Name -eq 'Invalidate'
$rotationWriteCalls = @($rotationWriter.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$rotationReadCalls = @($rotationReader.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$rotationChangeCalls = @($rotationChange.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$nativeZdo = $hostAssembly.MainModule.GetType('ZDO')
$rawRotation = @($nativeZdo.Fields | Where-Object Name -eq 'm_rotation')
Assert-Contract ($rawRotation.Count -eq 1 -and $rawRotation[0].FieldType.FullName -eq 'UnityEngine.Vector3') `
    'Exact rotation witness requires the actual native Euler field.'
foreach ($method in $nativeZdo.Methods | Where-Object Name -in @('Load','Deserialize')) {
    Assert-Contract ((@($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n") -notmatch 'ZDO::SetRotation') `
        'Native hydration must not invalidate the exact rotation through SetRotation.'
}
Assert-Contract ($rotationWriteCalls -match 'ZNetView::IsOwner' -and $rotationWriteCalls -match 'BlueprintMaterialState::SharesData' -and
    $rotationWriteCalls -match 'ZPackage::WriteSmallRotation' -and $rotationWriteCalls -match 'ZPackage::ReadSmallRotation' -and
    $rotationWriteCalls -match 'ZDO::Set' -and $rotationWriteCalls -match 'ZDO::GetQuaternion' -and
    $rotationWriteCalls -notmatch 'ClaimOwnership|SetOwner|Deserialize|Load|ZDO::SetRotation') `
    'Exact rotation must use an owned distinct created ZDO and the native codec without identity/native rotation replacement.'
Assert-Contract ($rotationReadCalls -match 'ZDO::GetQuaternion' -and $rotationReadCalls -match 'BlueprintRotationState::Valid' -and
    $rotationReadCalls -match 'BlueprintRotationState::Close' -and $rotationReadCalls -notmatch 'ZDO::Set|ZDO::Remove|ClaimOwnership|SetOwner') `
    'Exact rotation reads must validate both quaternions and native witness without writes.'
Assert-Contract ($rotationChangeCalls -match 'ZDO::RemoveQuaternion' -and $rotationChangeCalls -match 'BlueprintRotationState::Close' -and
    $rotationChangeCalls -notmatch 'ZDO::SetRotation') 'Real native rotation changes must invalidate, not restore, the old exact rotation.'
$beginPayment = $precisionSessionType.Methods | Where-Object Name -eq 'BeginConstructionPayment'
$beginPaymentCalls = @($beginPayment.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$placementFinished = $precisionSessionType.Methods | Where-Object Name -eq 'OnNativePlacementFinished'
$placementFinishedCalls = @($placementFinished.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($beginPaymentCalls.IndexOf('BlueprintRotationState::RequireFreshCreation') -ge 0 -and
    $beginPaymentCalls.IndexOf('BlueprintRotationState::RequireFreshCreation') -lt $beginPaymentCalls.IndexOf('BlueprintConstructionRefund::.ctor') -and
    $applyPlacementScaleOperands -notmatch 'BlueprintRotationState::ApplyCreated' -and
    $placementFinishedCalls.IndexOf('BlueprintRotationState::ApplyCreated') -gt $placementFinishedCalls.IndexOf('TakePendingPlacementSign') -and
    $placementFinishedCalls.IndexOf('BlueprintRotationState::ApplyCreated') -lt $placementFinishedCalls.IndexOf('CompleteNativePlacement') -and
    $sessionSource -match 'placed && sessionMode == SessionMode.BlueprintWorldPlacement') `
    'Exact rotation must preflight before debit and write only after blueprint enrollment, so failures use the existing refund transaction.'
$materialStateType = $plugin.MainModule.Types | Where-Object Name -eq 'BlueprintMaterialState'
$materialPreviewType = $plugin.MainModule.Types | Where-Object Name -eq 'NativePlacementMaterialPreview'
$materialPreviewShow = $materialPreviewType.Methods | Where-Object Name -eq 'Show'
$materialPreviewCalls = @($materialPreviewShow.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($materialPreviewCalls -match 'BlueprintMaterialVariation::CapturePreview' -and
    $materialPreviewCalls -match 'Renderer::set_forceRenderingOff' -and
    $materialPreviewCalls -notmatch 'ZDO::|ZNetView::|Material::Set|Renderer::get_materials') `
    'Native material preview must own only a visual copy without network or shared material mutation.'
$materialCreationGuard = $precisionSessionType.Methods | Where-Object Name -eq 'RequireNativePlacementMaterialCreation'
Assert-Contract ((@($materialCreationGuard.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n") -match
    'NativePlacementMaterialPreview::Matches') 'Seeded native placement must require its matching visible material preview.'
$materialApply = $materialStateType.Methods | Where-Object Name -eq 'ApplyCreated'
$materialCalls = @($materialApply.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($materialCalls -match 'ZNetView::IsOwner' -and $materialCalls -match 'ZDO::Set' -and
    $materialCalls -match 'ZDO::GetInt' -and $materialCalls -match 'BlueprintMaterialState::SharesData' -and
    $materialCalls -notmatch 'ClaimOwnership|SetOwner|Deserialize|Load') `
    'Material restoration must write/read only an owned distinct ZDO without importing identity or foreign state.'
Assert-Contract ($scaleWrapperCalls.IndexOf('RequireNativePlacementMaterialCreation') -ge 0 -and
    $scaleWrapperCalls.IndexOf('RequireNativePlacementMaterialCreation') -lt
        $scaleWrapperCalls.IndexOf('Object::Instantiate<UnityEngine.GameObject>') -and
    $applyPlacementScaleOperands.IndexOf('BlueprintMaterialState::ApplyCreated') -ge 0 -and
    $applyPlacementScaleOperands.IndexOf('BlueprintMaterialState::ApplyCreated') -lt
        $applyPlacementScaleOperands.IndexOf('currentBlueprintPlacements')) `
    'Material initialization must reject reused ZDOs before Instantiate and write before registering construction/payment.'
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
    $tryRollbackBlueprintOperands -notmatch 'Piece::DropResources' -and
    $tryRollbackBlueprintOperands -match 'PrecisionPlacementSession::TryRefundRemovedPiece' -and
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
    $sessionSource -match 'constructionPayments\[placed\] = nativeConstructionPayment' -and
    $sessionSource -match 'pendingConstructionCompletion = true' -and
    $sessionSource -match 'payment\.Complete\(\)' -and
    $sessionSource -match 'wear\.Remove\(true\)' -and
    $sessionSource -match
        'item\.WaitForNativeRemoval && Time\.unscaledTime < item\.ForceDestroyAt' -and
    $tryRollbackBlueprintOperands -notmatch 'ZNetView::ClaimOwnership' -and
    $tryRollbackBlueprintPiece.Body.ExceptionHandlers.Count -gt 0) `
    'Incomplete blueprint placement no longer follows the tracked cancellation path.'
Assert-Contract ($cancelBlueprintOperands.IndexOf('SharesNativePlacementData') -ge 0 -and
    $cancelBlueprintOperands.IndexOf('SharesNativePlacementData') -lt $cancelBlueprintOperands.IndexOf('BlueprintRollbackItem::.ctor')) `
    'Rollback must refuse a created object sharing source/ghost network identity before queueing native removal.'
Assert-Contract ($pluginSource -match 'BlueprintRollbackRemovalPatch' -and
    $pluginSource -match 'typeof\(HitData\), typeof\(bool\)' -and
    $pluginSource -match '__state\?\.RecordRemoval\(__1, __exception == null\)' -and
    $pluginSource -match 'EndConstructionPayment\(__instance, __state, __exception\)') `
    'Actual debit completion or native drop-suppression receipt is missing.'
Assert-Contract ($sessionSource -match 'pendingRollbackIds\[rollback.NetworkId\]' -and
    $sessionSource -match 'GetZDO\(item.NetworkId\) == null' -and
    $sessionSource -match 'ReferenceEquals\(ZDOMan.instance, item.NetworkObjects\)' -and
    $sessionSource -match 'SendNativeDestroyed.Invoke\(ZDOMan.instance, null\)' -and
    $pluginSource -match 'BlueprintRollbackNetworkRemovalPatch') `
    'Refund is not bound to the original native network identity and live removal batch.'
$zdoManType = @($hostAssembly.MainModule.Types) + @($utilsAssembly.MainModule.Types) | Where-Object Name -eq 'ZDOMan'
$destroyQueue = $zdoManType.Fields | Where-Object Name -eq 'm_destroySendList'
$destroyZdo = $zdoManType.Methods | Where-Object { $_.Name -eq 'DestroyZDO' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.Name -eq 'ZDO' }
$destroyZdoCalls = ($destroyZdo.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$sendDestroyed = $zdoManType.Methods | Where-Object Name -eq 'SendDestroyed'
$sendDestroyedCalls = ($sendDestroyed.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($destroyQueue.IsPrivate -and $destroyQueue.FieldType.FullName -eq 'System.Collections.Generic.List`1<ZDOID>' -and
    $destroyZdo.IsPublic -and $destroyZdoCalls -match 'ZDO::IsOwner' -and
    $destroyZdoCalls -match 'm_destroySendList' -and $destroyZdoCalls -match 'List`1<ZDOID>::Add' -and
    $sendDestroyed.IsPrivate -and $sendDestroyed.Parameters.Count -eq 0 -and
    $sendDestroyedCalls -match 'ZRoutedRpc::InvokeRoutedRPC') `
    'Native owned network removal queue/flush contract changed; disable construction rollback.'
Assert-Contract ($plugin.Name.Version.ToString() -eq '0.19.218.0') `
    "Unexpected BuildWorks artifact version: $($plugin.Name.Version)"
$fogResource = @($plugin.MainModule.Resources | Where-Object Name -eq 'BuildWorks.FogGhostV11')
Assert-Contract ($fogResource.Count -eq 1 -and $fogResource[0] -is [Mono.Cecil.EmbeddedResource]) 'Compiled v11 fog resource is missing or duplicated.'
$fogHash = [Security.Cryptography.SHA256]::Create()
try { $embeddedFogHash = [BitConverter]::ToString($fogHash.ComputeHash($fogResource[0].GetResourceData())).Replace('-', '') }
finally { $fogHash.Dispose() }
Assert-Contract ($embeddedFogHash -ceq (Get-FileHash -LiteralPath (Join-Path $projectRoot 'src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11')).Hash) 'Compiled fog resource differs from the verified bundle.'
$serverAuthority = $plugin.MainModule.GetType('OstrixMods.BuildWorks.BlueprintServerAuthority')
Assert-Contract ($null -ne $serverAuthority) 'Compiled server blueprint authority adapter is missing.'
$resolveAuthority = $serverAuthority.Methods | Where-Object Name -eq 'TryResolve'
$adminAuthority = $serverAuthority.Methods | Where-Object Name -eq 'IsAdministrator'
$resolveCalls = @($resolveAuthority.Body.Instructions | ForEach-Object { [string]$_.Operand })
$adminCalls = @($adminAuthority.Body.Instructions | ForEach-Object { [string]$_.Operand })
foreach ($call in 'ZNet::IsServer()', 'ZNet::GetPeers()', 'ZRpc::IsConnected()', 'ZRpc::GetSocket()',
    'ZNetPeer::IsReady()', 'ISocket::IsConnected()', 'ISocket::GetHostName()', 'ZNetPeer::m_rpc', 'ZNetPeer::m_socket') {
    Assert-Contract (@($resolveCalls | Where-Object { $_.Contains($call) }).Count -gt 0) `
        "Server identity must use the current direct connection API: $call"
}
Assert-Contract (@($adminCalls | Where-Object { $_.Contains('ZNet::IsAdmin(System.String)') }).Count -eq 1) `
    'Server blueprint rights must use native IsAdmin for the socket principal.'
$nativeAdmin = $hostAssembly.MainModule.GetType('ZNet').Methods | Where-Object { $_.Name -eq 'IsAdmin' -and $_.Parameters.Count -eq 1 }
Assert-Contract ($nativeAdmin.IsPublic -and -not $nativeAdmin.IsStatic -and
    @($nativeAdmin.Body.Instructions | Where-Object { [string]$_.Operand -match 'ZNet::ListContainsId' }).Count -eq 1) `
    'Native IsAdmin no longer delegates to the server admin-list identity policy.'
Write-Output 'PASS server blueprint authority API: compiled direct peer/socket binding and native admin-list policy; no real network authentication executed.'
$serverRuntime = $plugin.MainModule.GetType('OstrixMods.BuildWorks.BlueprintServerRuntime')
Assert-Contract ($null -ne $serverRuntime) 'Compiled direct server library runtime is missing.'
$runtimeTick = $serverRuntime.Methods | Where-Object Name -eq 'Tick'
$runtimeSend = $serverRuntime.Methods | Where-Object Name -eq 'Send'
$runtimeCalls = @($serverRuntime.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } })
foreach ($call in 'ZRpc::Register<ZPackage>', 'ZRpc::Unregister(System.String)', 'ZNet::GetWorldUID()',
    'ZNet::GetServerPeer()', 'ZNet::GetServerRPC()', 'ZNet::LocalPlayerIsAdminOrHost()', 'ZPackage::Size()', 'ZPackage::GetArray()') {
    Assert-Contract (@($runtimeCalls | Where-Object { $_.Contains($call) }).Count -gt 0) "Direct server runtime host API changed: $call"
}
Assert-Contract (@($runtimeCalls | Where-Object { $_ -match 'ZRoutedRpc' }).Count -eq 0) 'Server library must not trust a routed sender.'
$sendCalls = @($runtimeSend.Body.Instructions | ForEach-Object { [string]$_.Operand })
Assert-Contract (@($sendCalls | Where-Object { $_.Contains('ZPackage::.ctor(System.Byte[])') }).Count -eq 1 -and
    @($sendCalls | Where-Object { $_.Contains('ZPackage::Write(System.Byte[])') }).Count -eq 0 -and
    @($sendCalls | Where-Object { $_.Contains('ZRpc::Invoke(System.String,System.Object[])') }).Count -eq 1) `
    'Server protocol frames must use raw ZPackage construction, without an extra byte-array length prefix.'
$packageType = $hostAssembly.MainModule.GetType('ZPackage')
$rawPackage = $packageType.Methods | Where-Object { $_.Name -eq '.ctor' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Byte[]' }
$rawCalls = @($rawPackage.Body.Instructions | ForEach-Object { [string]$_.Operand })
Assert-Contract ($rawPackage.IsPublic -and @($rawCalls | Where-Object { $_.Contains('System.IO.Stream::Write(System.Byte[],System.Int32,System.Int32)') }).Count -eq 1 -and
    @($rawCalls | Where-Object { $_.Contains('System.IO.BinaryWriter::Write(System.Int32)') }).Count -eq 0) `
    'Native byte-array package constructor no longer preserves the raw frame.'
$pluginUpdate = $plugin.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin').Methods | Where-Object Name -eq 'Update'
$serverTickCall = $pluginUpdate.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and [string]$_.Operand -match 'BlueprintServerRuntime::Tick' } | Select-Object -First 1
$playerGate = $pluginUpdate.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.FieldReference] -and [string]$_.Operand -match 'Player::m_localPlayer' } | Select-Object -First 1
Assert-Contract ($null -ne $serverTickCall -and $null -ne $playerGate -and $serverTickCall.Offset -lt $playerGate.Offset) `
    'Dedicated server library tick must precede the local-player early return.'
Write-Output 'PASS direct server library runtime API: raw package framing, typed peer RPC, world/local-host boundary and dedicated tick order. Inert checks only; live network remains unverified.'
$pluginLifecycle = $plugin.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin')
foreach ($entry in @(@('OnDisable', 'Suspend'), @('OnDestroy', 'Dispose'))) {
    $cleanup = $pluginLifecycle.Methods | Where-Object Name -eq $entry[0]
    Assert-Contract (@($cleanup.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and [string]$_.Operand -match ('BlueprintServerRuntime::' + $entry[1]) }).Count -eq 1) `
        "Plugin $($entry[0]) must release owned server callbacks and operations."
}
$pluginIdentity = $plugin.MainModule.Types | Where-Object Name -eq 'BuildWorksPlugin'
$loaderIdentity = $pluginIdentity.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' }
Assert-Contract ($null -ne $loaderIdentity -and
    [string]$loaderIdentity.ConstructorArguments[2].Value -eq $plugin.Name.Version.ToString(3)) `
    'BepInEx/QA version does not match the compiled assembly version.'
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
$awake = $pluginMethods | Where-Object {
    $_.Name -eq 'Awake' -and $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
$registrationCheck = $pluginMethods | Where-Object {
    $_.Name -eq 'HasRegisteredPlacementTranspiler' -and
    $_.DeclaringType.Name -eq 'BuildWorksPlugin'
}
$awakeOperands = @($awake.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$registrationOperands = @($registrationCheck.Body.Instructions |
    ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($awakeOperands -match 'HasRegisteredPlacementTranspiler' -and
    $registrationOperands -match 'Harmony::GetPatchInfo' -and
    $registrationOperands -match 'Patches::Transpilers' -and
    $registrationOperands -match 'Patch::get_PatchMethod') `
    'Startup must verify the registered placement transpiler, not its eager execution.'
$geometry = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($geometryPath)
$documentLimits = $geometry.MainModule.GetType('OstrixMods.BuildWorks.Geometry.BlueprintEditorDocument')
$worldSnapCodec = $geometry.MainModule.GetType('OstrixMods.BuildWorks.Geometry.BlueprintSnapPointCodec')
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
$nativeWearSetup = $hostAssembly.MainModule.GetType('WearNTear').Methods | Where-Object Name -eq 'SetupColliders'
Assert-Contract ((Get-NativeLayoutHash $nativeWearSetup) -eq '8A99C4811FC2286E20BDB211390B02D224D36A9D68D826FC4BC4F516AF829143') `
    'Native WearNTear collider/support envelope changed; review the geometry and .15m contact context before shipping.'
$contactDispatch = $contactGeometry.Methods | Where-Object Name -eq 'TryFindContact'
$contactDispatchOperands = @($contactDispatch.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('GetComponent<WearNTear>', 'GetComponentsInChildren<UnityEngine.Collider>',
    'Collider::get_enabled', 'Collider::get_isTrigger', 'TryGetColliderBounds', 'FindShapeContact')) {
    Assert-Contract ($contactDispatchOperands -match [regex]::Escape($contactRequirement)) `
        "Collider contact dispatch lost a shape/native-support requirement: $contactRequirement"
}
Assert-Contract ([bool]($contactDispatch.Body.Instructions | Where-Object {
    $_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs([double]$_.Operand - 0.15) -lt 0.00001
})) 'WearNTear contact envelope no longer matches the current native .15m per side.'
$contactBounds = $contactGeometry.Methods | Where-Object Name -eq 'TryGetColliderBounds'
$contactBoundsOperands = @($contactBounds.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('BoxCollider::get_center', 'BoxCollider::get_size', 'Transform::get_lossyScale',
    'Transform::TransformPoint', 'Transform::get_rotation', 'MeshCollider::get_sharedMesh', 'Mesh::get_bounds')) {
    Assert-Contract ($contactBoundsOperands -match [regex]::Escape($contactRequirement)) `
        "Collider bounds lost inactive/pose geometry requirement: $contactRequirement"
}
Assert-Contract ($contactBoundsOperands -notmatch 'Collider::get_bounds|SyncTransforms|::set_|::SetActive') `
    'Contact geometry must not use stale/empty native collider bounds or activate/move the ghost.'
$contactSearch = $contactGeometry.Methods | Where-Object Name -eq 'FindShapeContact'
$contactSearchOperands = @($contactSearch.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($contactRequirement in @('Physics::RaycastNonAlloc', 'Physics::RaycastAll', 'Transform::IsChildOf',
    'Collider::get_attachedRigidbody', 'Vector3::Distance', 'Quaternion::Inverse', 'TryShapeBoundary')) {
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
    $editorContourEdgeOperands -match 'UnityEngine.Mathf::Clamp01' -and
    $editorContourDiscoveryOperands -match 'Edge3::get_Start' -and
    $editorContourDiscoveryOperands -match 'Edge3::get_End' -and
    $editorContourDiscoveryOperands -notmatch 'selectedPointLocal') `
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
$editorOpenOperands = @(
    ($editorController.Methods | Where-Object Name -eq 'Open').Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($editorOpenOperands -match 'BlueprintEditorScene::BeginInitialLoad' -and
    $editorOpenOperands -notmatch 'BuildWorksTestIsolation::RequireLoadedSession') `
    'Normal editor must retain staged loading without QA isolation code.'
$storeType = $pluginTypes | Where-Object Name -eq 'CompositeBlueprintStore'
Assert-Contract (($documentLimits.Fields | Where-Object Name -eq 'MaximumCustomSnapPoints').Constant -eq 20000 -and
    ($storeType.Fields | Where-Object Name -eq 'MaximumCustomSnapPoints').Constant -eq 20000 -and
    ($storeType.Fields | Where-Object Name -eq 'MaximumAnchors').Constant -eq 512 -and
    ($worldSnapCodec.Fields | Where-Object Name -eq 'MaximumPoints').Constant -eq 512) `
    'Blueprint custom capacity must be separate from ordinary anchors and per-piece network codec.'
Assert-Contract (($storeType.Fields | Where-Object Name -eq 'EditorPartBudget').Constant -eq 512) `
    'Normal editor must retain the 512-part gameplay limit.'
$legacyEditorOperands = @(
    ($pluginMethods | Where-Object { $_.Name -eq 'BeginBlueprintEditor' -and
        $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }).Body.Instructions |
        ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($legacyEditorOperands -match 'requestBlueprintEditor' -and
    $legacyEditorOperands.IndexOf('requestBlueprintEditor') -lt $legacyEditorOperands.IndexOf('TryResolveBlueprintPieces')) `
    'Legacy editor entry must route through the modern controller before its synchronous fallback.'
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
Assert-Contract ($hudShowOperands -match 'hud\.array' -and $hudShowOperands -match 'hud\.contour') `
    'Compiled HUD does not expose the unified Array and Guide controls.'
Assert-Contract ($hudShowOperands -match 'hud\.points_all' -and
    $hudShowOperands -match 'hud\.points_near' -and
    $hudShowOperands -match 'hud\.handles') `
    'Compiled HUD does not expose contextual anchor visibility and scale.'
Assert-Contract ($hudShowOperands -match 'hud\.snap_game' -and
    $hudShowOperands -match 'hud\.snap_mesh') `
    'Compiled HUD does not expose separate vanilla and mesh snap modes.'
Assert-Contract ($hudShowOperands -match 'hud\.auto_align_on' -and
    $hudShowOperands -match 'hud\.auto_align_off' -and
    $hudShowOperands -match 'hud\.redo') `
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
$registryDescription = $blueprintRegistry.Methods | Where-Object Name -eq 'Description'
$registryDescriptionOperands = @($registryDescription.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$pieceInfoUpdate = $precisionSessionType.Methods | Where-Object Name -eq 'UpdateBlueprintPieceInfo'
$pieceInfoOperands = @($pieceInfoUpdate.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($registryDescriptionOperands -match 'BuildWorksLocalization::Text' -and
    $registryDescriptionOperands -notmatch 'Piece::m_resources' -and
    $pieceInfoOperands -match 'Localization::GetSelectedLanguage' -and
    $pieceInfoOperands -match 'TMP_Text::set_text' -and
    $pieceInfoOperands -match 'BlueprintResourceHudView::Show' -and
    $pluginSource -match 'UpdateBlueprintPieceInfo\(__instance, player\)') `
    'Blueprint HUD must refresh localized description and native icon cards as display-only information.'
$resourceHud = $pluginTypes | Where-Object Name -eq 'BlueprintResourceHudView'
$resourceHudShow = $resourceHud.Methods | Where-Object Name -eq 'Show'
$resourceHudOperands = @($resourceHudShow.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($resourceHudOperands -match 'InventoryGui::SetupRequirement' -and
    $resourceHudOperands -match 'Inventory::CountItems' -and
    $resourceHudOperands -match 'CraftingStation::HaveBuildStationInRange' -and
    $resourceHudOperands -match 'CraftingStation::m_icon' -and
    $resourceHudOperands -notmatch 'Piece::m_resources' -and
    $resourceHudOperands -notmatch 'ConsumeResources') `
    'Blueprint resource cards must use native item/station rendering without mutating placement costs.'
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
        'HammerCatalogOrganizer\.Group\(piece\) == HammerCatalogOrganizer\.ActionsGroup' -and
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
    $blueprintFrameSource -match 'AnchorAdjustment\.AnchorCount' -and
    $blueprintFrameSource -match 'anchors\.Anchor\(index\)' -and
    $blueprintRegistrySource -match 'BlueprintPlacementFrame\.HammerSnapPoints\(' -and
    $blueprintRegistrySource -match 'collider\.center = localBounds\.center' -and
    $blueprintRegistrySource -match 'Mathf\.Max\(localBounds\.size\.x, 0\.05f\)' -and
    $blueprintFrameSource -match 'AnchorAdjustment\.ExternalCompositeSnapPoints') `
    'Blueprint placement no longer uses the selected part/group frame or composite fallback.'
Assert-Contract ($createBlueprintPrefabOperands -match 'BlueprintPlacementFrame::TryResolve' -and
    $createBlueprintPrefabOperands -match 'BlueprintPlacementFrame::RebaseVisuals' -and
    $createBlueprintPrefabOperands -match 'BlueprintPlacementFrame::HammerSnapPoints' -and
    $sessionSource -match 'BlueprintPlacementFrame\.ToRootPose\(ghost\.transform\.position, ghost\.transform\.rotation' -and
    $sessionSource -match 'baseRotation = currentRotation = blueprintRootRotation' -and
    $sessionSource -match 'blueprintEditPartIndex = -1;\s*baseSnapshot = CaptureHistorySnapshot\(\);\s*history\.Reset\(baseSnapshot\)') `
    'Hammer visuals/snaps/collider and placement root do not share the independent support frame or coherent initial history.'
Assert-Contract ($blueprintFrameSource -match
    'if \(native\.Count == 0 && \(blueprint\.customSnapPoints == null \|\| blueprint\.customSnapPoints\.Count == 0\)\)' -and
    $blueprintFrameSource -match 'foreach \(Vector3 point in native\)' -and
    $blueprintFrameSource -match 'foreach \(CompositeBlueprintStore\.VectorData point in blueprint\.customSnapPoints') `
    'Hammer blueprint bounds must be fallback only when native and custom sockets are absent.'
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
    $blueprintRegistrySource -match 'BlueprintPrefabMetadataCapture\.TryGetLocalVisualBounds\(root, out bounds\)' -and
    $prefabMetadataSource -match 'bool includeInactiveRoot = false' -and
    $prefabMetadataSource -match 'IsActiveWithin\(root\.transform, filter\.transform, includeInactiveRoot\)' -and
    $prefabMetadataSource -match 'if \(!current\.gameObject\.activeSelf\) return false;' -and
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
Assert-Contract ($catalogSource -match 'FavoriteCategoryName[\s\S]{0,100}catalog\.category\.favorites' -and
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
    $blueprintCardSource -notmatch 'bool doubleClick' -and
    $blueprintCardSource -match 'blueprintAction\?\.Invoke\(blueprint, false\)' -and
    $blueprintCardSource -match 'AddBlueprintContextMenu' -and
    $blueprintCardSource -match 'catalog\.action\.place' -and
    $blueprintCardSource -match 'catalog\.action\.edit' -and
    $blueprintCardSource -match 'catalog\.action\.rename' -and
    $blueprintCardSource -match 'catalog\.action\.refresh_preview' -and
    $catalogSource -match 'ShouldCaptureBlueprintContext' -and
    $catalogSource -match 'Input\.GetMouseButtonDown\(1\)' -and
    $catalogSource -match 'eventSystem\.RaycastAll\(pointer, hits\)' -and
    $catalogSource -match 'GetComponentInParent<HammerCatalogPointerClick>' -and
    $pluginSource -match
        '\[HarmonyPatch\(typeof\(BuildUi\), "NavigationUpdate"\)\][\s\S]{0,260}?ShouldCaptureBlueprintContext' -and
    $blueprintCardSource -match 'catalog\.action\.resources' -and
    $blueprintCardSource -match 'catalog\.action\.delete') `
    'Blueprint cards lost single-click placement or the RMB action menu.'
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
    $catalogSource -match 'catalog\.action\.index_mode' -and
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
Assert-Contract ($hudSource -notmatch 'BlueprintPrevious|BlueprintNext') `
    'The F9 editor still contains blueprint-library navigation.'
$blueprintStore = $pluginTypes | Where-Object Name -eq 'CompositeBlueprintStore'
Assert-Contract ($null -ne $blueprintStore) `
    'Compiled plugin has no local composite-blueprint store.'
$blueprintPart = $pluginTypes | Where-Object {
    $_.Name -eq 'Part' -and $_.DeclaringType -eq $blueprintStore
}
Assert-Contract ($null -ne $blueprintPart) 'Blueprint part record is missing.'
$pinnedScaleField = $blueprintPart.Fields | Where-Object Name -eq 'baseScale'
Assert-Contract ($null -ne $pinnedScaleField -and $pinnedScaleField.FieldType.FullName -match
    'System.Nullable`1<OstrixMods.BuildWorks.CompositeBlueprintStore/VectorData>') `
    'Pinned scale must remain an optional vector; legacy documents cannot acquire a guessed base.'
$rootScaleCalls = @(($blueprintPart.Methods | Where-Object Name -eq 'RootScale').Body.Instructions |
    ForEach-Object { [string]$_.Operand }) -join "`n"
$placementScaleCalls = @(($blueprintPart.Methods | Where-Object Name -eq 'PlacementScale').Body.Instructions |
    ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($rootScaleCalls -match 'Part::baseScale' -and $rootScaleCalls -match 'Vector3::Scale' -and
    $rootScaleCalls -match 'CompositeBlueprintStore::PositiveScale' -and
    $placementScaleCalls -match 'Part::RootScale' -and $placementScaleCalls -match 'CompositeBlueprintStore::RelativeScale') `
    'Visual and world scales must share the pinned root scale and validated native-relative conversion.'
foreach ($fieldName in @('prefabName', 'stableId', 'displayName', 'parentGroupId',
    'editorVisible', 'editorLocked', 'sourceDocumentId', 'sourceLine')) {
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
foreach ($fieldName in @('category', 'previewYaw', 'previewPitch', 'previewZoom', 'groups', 'primaryPartId', 'primaryGroupId', 'supports', 'placementSupportId', 'sourceDocuments')) {
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
Assert-Contract ($saveDocumentMethods.Count -eq 5 -and
    @($saveDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryPartId').Count -eq 1
    }).Count -eq 3 -and
    @($saveDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryGroupId').Count -eq 1
    }).Count -eq 2 -and
    @($saveDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'supports').Count -eq 1 -and
        @($_.Parameters | Where-Object Name -eq 'placementSupportId').Count -eq 1
    }).Count -eq 1) `
    'Blueprint frame save overload or a compatibility overload is missing.'
Assert-Contract ($updateDocumentMethods.Count -eq 5 -and
    @($updateDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryPartId').Count -eq 1
    }).Count -eq 3 -and
    @($updateDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'primaryGroupId').Count -eq 1
    }).Count -eq 2 -and
    @($updateDocumentMethods | Where-Object {
        @($_.Parameters | Where-Object Name -eq 'supports').Count -eq 1 -and
        @($_.Parameters | Where-Object Name -eq 'placementSupportId').Count -eq 1
    }).Count -eq 1) `
    'Blueprint frame update overload or a compatibility overload is missing.'
Assert-Contract ($blueprintStoreSource -match 'private const int FormatVersion = 24' -and
    $blueprintStoreSource -match 'loaded\.version < 6' -and
    $blueprintStoreSource -match 'ValidScale\(part\.scale\)' -and
    $blueprintStoreSource -match 'if \(loaded\.version < 4\)' -and
    $blueprintStoreSource -match 'part\.stableId = blueprint\.id' -and
    $blueprintStoreSource -match 'ValidDocument\(blueprint\.parts, blueprint\.groups, blueprint\.supports\)' -and
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
    $thumbnailSource -match 'internal const int ThumbnailLayer = 31' -and
    $editorSceneSource -match 'used\[BlueprintThumbnailRenderer.ThumbnailLayer\] = true' -and
    $thumbnailSource -match '-contents-v4-ugc' -and
    $thumbnailSource -match 'RefreshTextPermission\(\)') `
    'Blueprint v3/v4 migration, nested groups, or isolated persistent previews are missing.'
$catalogManagement = $catalogView.Methods | Where-Object Name -eq 'DeleteBlueprint'
$catalogManagementOperands = @($catalogManagement.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($catalogManagementOperands -match 'CompositeBlueprintStore::TrySetHidden' -and
    $catalogManagementOperands -match 'CompositeBlueprintStore::TryDelete' -and
    $catalogManagementOperands -match 'CompositeBlueprintStore::IsHidden' -and
    $catalogSource -match 'private static void CancelBlueprintDeletion' -and
    $catalogSource -match 'CatalogAll\(showHiddenBlueprints\)' -and
    $sessionSource -match 'blueprintStore.CatalogAll\(true\)' -and
    $sessionSource -match 'hidden: blueprintStore.IsHidden\(blueprint\)' -and
    $editorControllerSource -match 'view.CatalogManageRequested \+= ManageCatalogBlueprint' -and
    $editorControllerSource -match 'document.MarkUnsaved\(\)' -and
    $editorViewSource -match 'item.Hidden == \(catalogBlueprintMode && f.Section == "hidden"\)' -and
    $blueprintStoreSource -match 'allowHiddenSources: loaded.version >= 21') `
    'Catalogue delete/hide/restore producer, persistence, cancellation or editor draft wiring is missing.'
Assert-Contract ($catalogSource -match 'typeof\(RectMask2D\)' -and
    $catalogSource -match 'popup\.AddComponent<ScrollRect>\(\)' -and
    $catalogSource -match 'modalCanvas\.overrideSorting = true' -and
    $catalogSource -match 'panel\.AddComponent<GraphicRaycaster>\(\)' -and
    $catalogSource -match
        'BlocksCatalogPaging => BlocksExchangeInput \|\| previewSession != null \|\| showResources') `
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
    'PieceTable::m_availablePieces' -and
    $isBuildPieceAvailableOperands -match 'System.Collections.Generic.HashSet`1<Piece>::Contains' -and
    $isBuildPieceAvailableOperands -notmatch 'ContainsAvailable|AvailablePiecesField|AvailableBuildPieces') `
    'Blueprint availability must use the complete native eligibility set, not mutable UI category caches.'
$resolveBlueprintPieces = $pluginMethods | Where-Object Name -eq 'TryResolveBlueprintPieces'
$resolveBlueprintPiecesOperands = @(
    $resolveBlueprintPieces.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($resolveBlueprintPiecesOperands -match
    'PrecisionPlacementSession::TryResolveBuildPiece' -and
    $resolveBlueprintPiecesOperands -match
    'PrecisionPlacementSession::SupportsBlueprintWorkspacePiece' -and
    $resolveBlueprintPiecesOperands -match 'CompositeBlueprintStore/Part::PlacementScale' -and
    $resolveBlueprintPiecesOperands -match 'ZNetView::m_syncInitialScale' -and
    $resolveBlueprintPiecesOperands -notmatch 'PrecisionPlacementSession::SupportsPrecisionPlacement') `
    'Blueprint resolution must allow available furniture while retaining native scale validation.'
Assert-Contract ($resolveBlueprintPiecesOperands -match 'CompositeBlueprintStore/Part::scaleIsAbsolute') `
    'World blueprint resolution must reject unresolved source scale basis.'
$resolveScaleInstructions = $resolveBlueprintPieces.Body.Instructions
$editorRootScale = $resolveScaleInstructions | Where-Object { [string]$_.Operand -match 'Part::RootScale' } | Select-Object -First 1
$worldRelativeScale = $resolveScaleInstructions | Where-Object { [string]$_.Operand -match 'Part::PlacementScale' } | Select-Object -First 1
$editorScaleArgument = $resolveScaleInstructions | Where-Object {
    $_.Offset -lt [Math]::Min($editorRootScale.Offset, $worldRelativeScale.Offset) -and
    $_.OpCode.Code -eq 'Ldarg_S' -and $_.Operand.Name -eq 'forEditor'
} | Select-Object -Last 1
$editorScaleBranch = if ($null -ne $editorScaleArgument) {
    $resolveScaleInstructions[$resolveScaleInstructions.IndexOf($editorScaleArgument) + 1]
} else { $null }
$editorSelectsRoot = $null -ne $editorScaleBranch -and (
    ($editorScaleBranch.OpCode.Code -in @('Brtrue', 'Brtrue_S') -and
        $editorScaleBranch.Operand.Offset -gt $worldRelativeScale.Offset -and
        $editorScaleBranch.Operand.Offset -lt $editorRootScale.Offset) -or
    ($editorScaleBranch.OpCode.Code -in @('Brfalse', 'Brfalse_S') -and
        $editorScaleBranch.Operand.Offset -gt $editorRootScale.Offset -and
        $editorScaleBranch.Operand.Offset -lt $worldRelativeScale.Offset))
$scaleAlternativeSkip = @($resolveScaleInstructions | Where-Object {
    $_.Offset -gt [Math]::Min($editorRootScale.Offset, $worldRelativeScale.Offset) -and
    $_.Offset -lt [Math]::Max($editorRootScale.Offset, $worldRelativeScale.Offset) -and
    $_.OpCode.Code -in @('Br', 'Br_S') -and
    $_.Operand.Offset -gt [Math]::Max($editorRootScale.Offset, $worldRelativeScale.Offset)
})
Assert-Contract ($null -ne $editorRootScale -and $null -ne $worldRelativeScale -and
    $editorSelectsRoot -and $scaleAlternativeSkip.Count -eq 1) `
    'Editor resolution must validate exact RootScale while only world resolution applies the native relative range.'
foreach ($methodName in 'ShowPassiveBlueprintPreview', 'ApplyEditingGhostTransform', 'TryAddPlacementInstance', 'ApplyBlueprintAnchors') {
    $scaleConsumer = $precisionSessionType.Methods | Where-Object Name -eq $methodName
    $scaleConsumerCalls = @($scaleConsumer.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($scaleConsumerCalls -match 'Part::PlacementScale') `
        "World scale path bypasses the pinned scale basis: $methodName"
}
$addInstance = $precisionSessionType.Methods | Where-Object Name -eq 'TryAddPlacementInstance'
$scalePreflight = $addInstance.Body.Instructions | Where-Object {
    [string]$_.Operand -match 'ZNetView::m_syncInitialScale'
} | Select-Object -First 1
$firstPlanAppend = $addInstance.Body.Instructions | Where-Object {
    [string]$_.Operand -match 'List`1<OstrixMods.BuildWorks.PrecisionPlacementSession/TransformSnapshot>::Add'
} | Select-Object -First 1
Assert-Contract ($null -ne $scalePreflight -and $null -ne $firstPlanAppend -and
    $scalePreflight.Offset -lt $firstPlanAppend.Offset -and
    $sessionSource -match '(?s)relativeScales\[index\] = ToUnity\(BlueprintEditorPart.NormalizeScale\(.*?\* uniformScale.*?m_syncInitialScale.*?placementPlan.Add') `
    'All blueprint scales and native persistence must be preflighted before appending a placement instance.'
$editorVisual = $pluginMethods | Where-Object { $_.Name -eq 'ResolveBlueprintEditorVisual' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$editorVisualCalls = ($editorVisual.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($editorVisualCalls -match 'ZNetScene::GetPrefab' -and $editorVisualCalls -notmatch 'TryResolveBuildPiece|IsBuildPieceAvailable') `
    'Editor visuals must not require unlocked survival recipes.'
foreach ($workspaceGateName in @('BlueprintEditorCatalogItems', 'EnableNativePieceScale', 'IsBlueprintPieceSelectable')) {
    $workspaceGate = $pluginMethods | Where-Object { $_.Name -eq $workspaceGateName -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
    $workspaceGateOperands = @($workspaceGate.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($workspaceGateOperands -match 'SupportsBlueprintWorkspacePiece' -and
        $workspaceGateOperands -notmatch 'SupportsPrecisionPlacement') `
        "Furniture support is still cut off before save/place: $workspaceGateName"
}
$catalogItemsMethod = $pluginMethods | Where-Object { $_.Name -eq 'BlueprintEditorCatalogItems' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$catalogItemsOperands = @($catalogItemsMethod.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($catalogItemsOperands -match 'BlueprintEditorController::EditorCatalogStatus' -and
    $catalogItemsOperands -match 'HammerBlueprintPieceRegistry::ResourceCosts' -and
    $catalogItemsOperands -notmatch 'HammerBlueprintPieceRegistry::Resources\(' -and
    $sessionSource -match 'warningReason: warning, resourcesKnown: resolved' -and
    $sessionSource -notmatch 'unavailable = BuildWorksLocalization.Text\("placement.hammer_piece_missing", missing\)') `
    'Editor catalogue must separate missing-model warnings from file/limit blocks and unknown construction costs.'
Assert-Contract ($catalogItemsOperands -match 'StationExtension::m_craftingStation' -and
    $catalogItemsOperands -match 'CraftingStation::m_name' -and
    $catalogItemsOperands -match 'CraftingStation::m_icon' -and
    $catalogItemsOperands -match 'Piece::m_craftingStation' -and
    $sessionSource -match 'CraftingStation targetStation = extension \? extension.m_craftingStation : ownStation;' -and
    $sessionSource -match 'stationId: targetStation' -and
    $sessionSource -match 'requiredStation: piece.m_craftingStation' -and
    $sessionSource -match 'piece.m_usage' -and
    $sessionSource -match 'UsageTagFlags.Stacks' -and
    $sessionSource -match 'UsageTagFlags.Decor') `
    'Editor catalog must keep the actual upgrade workstation separate from the construction station requirement.'
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
    $sessionSource -notmatch 'OpenBlueprintEditorFromWorld') `
    'World blueprint F9 no longer enters its separate whole-group precision path.'
$placementInstructions = $beginBlueprintPlacement.Body.Instructions
$contentsGate = $placementInstructions | Where-Object {
    $_.OpCode.FlowControl -eq 'Call' -and $_.Operand.Name -eq 'RequestContentsChoice'
} | Select-Object -First 1
$strictContents = $placementInstructions | Where-Object {
    $_.OpCode.FlowControl -eq 'Call' -and $_.Operand.Name -eq 'TryResolveBlueprintPieces'
} | Select-Object -First 1
$strictIndex = $placementInstructions.IndexOf($strictContents)
$contentsResolver = $pluginMethods | Where-Object Name -eq 'TryResolveBlueprintPieces'
Assert-Contract ($contentsGate -and $strictContents -and $contentsGate.Offset -lt $strictContents.Offset -and
    $strictIndex -ge 2 -and $placementInstructions[$strictIndex - 1].OpCode.Code -eq 'Ldc_I4_0' -and
    $placementInstructions[$strictIndex - 2].OpCode.Code -eq 'Ldc_I4_0' -and
    $contentsResolver.Parameters.Count -eq 6 -and $contentsResolver.Parameters[5].Name -eq 'forContentsReview' -and
    $contentsResolver.Parameters[5].Constant -eq $false) `
    'Placement must review contents before strict validation; review eligibility must never become build permission.'
Assert-Contract ($catalogSource -match
    'exchangeView\.Tick\(\);\s*if \(Input.GetKeyDown\(KeyCode.Escape\)\) \{ exchangeView\?\.Dispose\(\);') `
    'Escape must tolerate a placement modal that has closed itself during Tick.'
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
    $applyBlueprintAnchorsOperands -match 'Part::PlacementScale' -and
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
    $hudSource -match 'hud\.workspace\.catalog' -and
    $hudSource -match 'hud\.select_in_world' -and
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
    $worldCaptureCalls -match 'BlueprintPlacementFrame::HasRigidParent' -and
    $worldCaptureCalls -match 'Transform::get_localScale' -and
    $worldCaptureCalls -notmatch 'Transform::get_lossyScale' -and
    $worldCaptureCalls -match 'IsBlueprintPieceSelectable' -and
    $worldCaptureCalls -match 'BlueprintSnapPointSet::.ctor' -and
    $worldCaptureCalls -match 'BlueprintSnapPointSet::TryAdd' -and
    $worldCaptureCalls -match 'BlueprintSnapPointSet::get_Points' -and
    $sessionSource -match 'new Geometry\.BlueprintSnapPointSet\(CompositeBlueprintStore\.MaximumCustomSnapPoints\)' -and
    $worldCaptureCalls -notmatch '::Destroy|::ClaimOwnership|::SetPositionAndRotation|::set_position|::set_rotation|::SetLocalScale' -and
    $sessionSource -match 'pendingWorldBlueprintSelection && GameplayInputAvailable' -and
    $sessionSource -match
        'private void Finish\([^)]*\)\s*\{\s*HammerCatalogView\.CancelPlacementContents\(\); ClearContentsRecipe\(\);[\s\S]{0,180}?pendingWorldBlueprintSelection = false;' -and
    $hudSource -match 'confirmText\.text = selectingBlueprint \? BuildWorksLocalization\.Text\("hud\.save"\)' -and
    $sessionSource -match 'if \(selectingBlueprint\)\s*\{\s*SaveCurrentBlueprint\(\);\s*return;' -and
    $sessionSource -match 'TrySaveWorldSelection[\s\S]*?Finish\(\);\s*RefreshBlueprintEditorLibrary' -and
    $catalogSource -match 'catalog\.action\.select_in_world') `
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
Assert-Contract ($previewShow.Parameters.Count -eq 8 -and
    $previewShow.Parameters[3].Name -eq 'hiddenIndex' -and
    $previewShow.Parameters[7].Name -eq 'attachmentsOnly' -and
    $previewSource -match 'bool hideOne = hiddenIndex >= 0' -and
    $previewSource -match 'BlueprintItemStandPreview.TryAdd' -and
    $previewSource -match 'BlueprintItemStandPlacement.SelectSupplies' -and
    $sessionSource -match 'attachmentsOnly: blueprint.parts.Count <= CompositeBlueprintStore.InteractivePartBudget' -and
    $sessionSource -match 'ShowPassiveBlueprintPreview' -and
    $sessionSource -notmatch 'passiveBlueprintPieces' -and
    $passiveBlueprintPreviewOperands -notmatch 'Renderer::set_enabled' -and
    $passiveBlueprintPreviewOperands -match 'PlacementGhostPreviewView::Hide' -and
    $passiveBlueprintPreviewOperands -match 'PlacementGhostPreviewView::Show' -and
    $passiveBlueprintPreviewOperands -match 'BlueprintPlacementFrame::ToRootPose') `
    'Blueprint preview overrides source renderer visibility or duplicates the native ghost.'
Assert-Contract ($sessionSource -match 'SuppliesAvailable\(player, heldPlacementSupplies\)' -and
    $sessionSource -match 'out safe, pendingItemStandSupply' -and
    $sessionSource -match 'supplies: placementSupplies') `
    'Confirmed mount visuals and deferred native attachment must retain the selected supplied item.'
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
    $hudSource -match 'BuildWorksLocalization\.Text\("hud\.light", editorLighting\)' -and
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
    $catalogSource -match 'int actionCount = deleteConfirmationId == blueprint.id \? 11 : 10' -and
    $catalogSource -match 'BlueprintActionsViewport' -and
    $catalogSource -match 'viewport.AddComponent<RectMask2D>' -and
    $catalogSource -match 'viewport.AddComponent<ScrollRect>' -and
    $catalogSource -notmatch
        'height - actionCount \* actionHeight - 8f') `
    'Blueprint inspector actions can escape below their panel.'
foreach ($inspectorHeight in 240, 358, 400, 477, 720) {
    $actionHeight = 28
    $actionTop = [Math]::Min(184, 86 + $inspectorHeight * 0.20)
    $actionTop = [Math]::Min($actionTop, [Math]::Max(0, $inspectorHeight - 28))
    $previewBottom = 74 + [Math]::Min(104, $inspectorHeight * 0.20)
    Assert-Contract ($actionTop -ge $previewBottom) `
        "Blueprint inspector actions overlap the preview in a $inspectorHeight px panel."
    Assert-Contract (($inspectorHeight - $actionTop - 8) -gt 0) `
        "Blueprint inspector actions exceed a $inspectorHeight px panel."
}
Assert-Contract ($thumbnailSource -match
    'int alpha = Math\.Max\(pixel\.a, derivedAlpha\)' -and
    $thumbnailSource -match 'texture\.SetPixels32\(pixels\)') `
    'Transparent blueprint thumbnails can reject shaders that render RGB with zero alpha.'
Assert-Contract ($sessionSource -match
    'MovePieceWithoutMovingCameraFocus\(candidate\);[\s\S]{0,120}TryAutoAlignToTouchingPiece\(\)') `
    'Dragging a move arrow can still drag the editor camera together with the object.'
Assert-Contract ($catalogSource -match 'catalog\.action\.create_blueprint' -and
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
        'if \(!aimedPiece \|\| aimedPiece\.transform == ghost\.transform[\s\S]*?return true;' -and
    $sessionSource -match
        'if \(bestSource && bestTarget\)[\s\S]*?else\s*\{\s*return true;\s*\}' -and
    $sessionSource -match 'if \(distance > 0\.5f \|\| !\(distance < bestDistance\)\) continue;' -and
    $sessionSource -match 'blueprintPieceRegistry\.TryGetBlueprint\(selected, out _\)' -and
    $passiveAutoJoinOperands -match 'Player::get_AlternativePlacementActive' -and
    $passiveAutoJoinOperands -match 'Piece::GetSnapPoints' -and
    $passiveAutoJoinOperands -match 'Vector3::Distance' -and
    $passiveAutoJoinOperands -match 'Transform::IsChildOf' -and
    $passiveAutoJoinOperands -match 'HammerBlueprintPieceRegistry::TryGetBlueprint' -and
    $passiveAutoJoinOperands -notmatch 'Transform::set_rotation' -and
    $passiveAutoJoinOperands -notmatch 'AutoJoinRotation') `
    'Passive auto-join must preserve ordinary native pair/rotation and composite inclusive ordered distance selection.'
& (Join-Path $PSScriptRoot 'Test-PassiveAutoJoin.ps1')
& (Join-Path $PSScriptRoot 'Test-NativePlacementIdentity.ps1')
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
$gizmoPrepare = $pluginMethods | Where-Object {
    $_.Name -eq 'PrepareAnchorHandles' -and $_.DeclaringType.Name -eq 'TransformGizmoView'
}
$gizmoPrepareOperands = @(
    $gizmoPrepare.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($gizmoShowOperands -match 'PrepareAnchorHandles' -and
    $gizmoShowOperands -match 'visibleAnchors' -and
    $gizmoShowOperands -match 'anchorHandleIndices' -and
    $gizmoPrepareOperands -match 'CheckAnchorVisibility' -and
    $gizmoPrepareOperands -match 'visibleAnchors' -and
    $gizmoPrepareOperands -match 'ConsiderAnchor') `
    'Compiled contextual handles do not share camera visibility with hit testing.'
$gizmoVisibility = $pluginMethods | Where-Object {
    $_.Name -eq 'CheckAnchorVisibility' -and $_.DeclaringType.Name -eq 'TransformGizmoView'
}
$gizmoConsider = $pluginMethods | Where-Object {
    $_.Name -eq 'ConsiderAnchor' -and $_.DeclaringType.Name -eq 'TransformGizmoView'
}
$gizmoVisibilityOperands = @($gizmoVisibility.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$gizmoConsiderOperands = @($gizmoConsider.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($gizmoVisibilityOperands -match 'IsPointVisible' -and
    $gizmoConsiderOperands -match 'CheckAnchorVisibility' -and
    $gizmoConsiderOperands -match 'ProjectAnchor' -and
    $gizmoShowOperands -match 'PrepareAnchorProjections') `
    'Lazy anchor queries lost fresh visibility or the camera/coordinate projection cache.'
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
Assert-Contract ($midpointPatchOperands -match 'BlueprintWorldSnapPoints::Append' -and
    $pluginOnDestroyOperands -match 'BlueprintWorldSnapPoints::RemoveAll' -and
    $midpointPatchOperands.IndexOf('VanillaMidpointSnapPoints::Append') -lt
        $midpointPatchOperands.IndexOf('BlueprintWorldSnapPoints::Append')) `
    'World custom snap-point reconstruction or unload cleanup is missing.'
$worldSnapType = $pluginTypes | Where-Object Name -eq 'BlueprintWorldSnapPoints'
$worldCollectionPatch = $pluginTypes | Where-Object {
    $_.Name -eq 'CollectionPatch' -and $_.DeclaringType.Name -eq 'BlueprintWorldSnapPoints'
}
Assert-Contract ($worldCollectionPatch -and
    @($worldCollectionPatch.Methods | Where-Object Name -eq 'Prefix').Count -eq 1 -and
    @($worldCollectionPatch.Methods | Where-Object Name -eq 'Finalizer').Count -eq 1 -and
    @($worldSnapType.Fields | Where-Object { $_.Name -eq 'current' -and
        @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.ThreadStaticAttribute' }).Count -eq 1 }).Count -eq 1) `
    'World snap collection must remain scoped and exception-safe per thread.'
$snapLocalSets = $pluginMethods | Where-Object {
    $_.Name -eq 'SnapLocalSets' -and $_.DeclaringType.Name -eq 'BlueprintPlacementFrame'
}
$snapLocalOperands = @($snapLocalSets.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($snapLocalOperands -match 'BlueprintNearestPartIndex::.ctor' -and
    $snapLocalOperands -match 'BlueprintNearestPartIndex::FindNearest' -and
    $snapLocalOperands -match 'BlueprintSnapPointCodec::Encode' -and
    $snapLocalOperands -match 'Quaternion::Inverse' -and
    $snapLocalOperands -match 'Part::RootScale') `
    'Snap assignment must use the shared nearest index while retaining rotation, prefab scale and per-piece codec validation.'
$worldSnapApply = $worldSnapType.Methods | Where-Object Name -eq 'Apply'
$worldSnapApplyOperands = @(
    $worldSnapApply.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($worldSnapApplyOperands -match 'ZNetView::IsOwner' -and
    $worldSnapApplyOperands -match 'ZDO::Set' -and
    $worldSnapApplyOperands -notmatch 'ClaimOwnership') `
    'Custom snap persistence must require ownership without claiming another player piece.'
$snapNativeApply = $pluginMethods | Where-Object Name -eq 'ApplyNativePlacementScale'
Assert-Contract ((@($snapNativeApply.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n") -match
    'BlueprintWorldSnapPoints::Apply') 'Native placement no longer persists the approved custom snap payload.'
$showSnapCandidates = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowSnapCandidates' -and
    $_.DeclaringType.Name -eq 'TransformGizmoView'
}
Assert-Contract ($showSnapCandidates.Parameters.Count -eq 5 -and
    $showSnapCandidates.Parameters[2].ParameterType.FullName -eq
        'System.Collections.Generic.IReadOnlyList`1<System.Boolean>') `
    'Compiled magnetic preview no longer distinguishes native target points.'
$showFrozen = $pluginMethods | Where-Object {
    $_.Name -eq 'ShowFrozen' -and $_.DeclaringType.Name -eq 'PrecisionPlacementHudView'
}
$showFrozenOperands = @(
    $showFrozen.Body.Instructions | ForEach-Object { [string]$_.Operand }
) -join "`n"
Assert-Contract ($showFrozenOperands -match 'hud\.frozen\.title') `
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
Assert-Contract ($hudShowArmedOperands -match 'hud\.armed\.tool_hint' -and
    $hudShowArmedOperands -match 'hud\.armed\.selection_hint' -and
    $hudShowArmedOperands -match 'hud\.armed\.stamina_hint') `
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
Assert-Contract ($sessionSource -match 'placement\.progress\.tool') `
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
Assert-Contract ($sessionSource -match 'placement\.complete\.blueprint') `
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
    $legacyView = @($buildCameraPlugin.Fields | Where-Object {
        $_.Name -eq 'buildCameraViewDirection' -and $_.IsStatic
    }).Count -eq 1
    $yaw = @($buildCameraPlugin.Fields | Where-Object {
        $_.Name -eq 'CameraYaw' -and $_.IsStatic -and $_.FieldType.FullName -eq 'System.Single'
    }).Count -eq 1
    $pitch = @($buildCameraPlugin.Fields | Where-Object {
        $_.Name -eq 'CameraPitch' -and $_.IsStatic -and $_.FieldType.FullName -eq 'System.Single'
    }).Count -eq 1
    Assert-Contract ($legacyView -or ($yaw -and $pitch)) `
        'Installed Build Camera no longer exposes restorable view state.'
    Assert-Contract ($sessionSource -match 'externalBuildCameraYawField.*GetValue' -and
        $sessionSource -match 'externalBuildCameraPitchField.*GetValue' -and
        $sessionSource -match 'externalBuildCameraYawField\.SetValue' -and
        $sessionSource -match 'externalBuildCameraPitchField\.SetValue') `
        'BuildWorks does not restore the current Build Camera yaw and pitch.'
}

$signType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'Sign'
Assert-Contract (@($signType.Fields | Where-Object { $_.Name -eq 'm_isViewable' -and $_.IsPrivate -and $_.FieldType.FullName -eq 'System.Boolean' }).Count -eq 1) 'Sign view permission field changed.'
Assert-Contract (@($signType.Fields | Where-Object { $_.Name -eq 'm_characterLimit' -and $_.IsPublic -and $_.FieldType.FullName -eq 'System.Int32' }).Count -eq 1) 'Sign character limit changed.'
$nativeSignSetter = $signType.Methods | Where-Object { $_.Name -eq 'SetText' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'System.String' }
$nativeSignCalls = ($nativeSignSetter.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($nativeSignCalls -match 'PrivateArea::CheckAccess' -and $nativeSignCalls -match 'ZNetView::ClaimOwnership' -and $nativeSignCalls -match 'ZDOVars::s_text') 'Native sign access/ownership/storage changed.'
$signApply = $pluginMethods | Where-Object { $_.Name -eq 'TryApply' -and $_.DeclaringType.Name -eq 'BlueprintSignText' }
$signCalls = ($signApply.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($signCalls -match 'Sign::SetText' -and $signCalls -match 'CheckPrivilege' -and $signCalls -match 'ZDO::GetString' -and $signCalls -notmatch 'ZDO::Set\(') 'Blueprint sign writes bypass native setters or do not verify stored text.'
$nativeFinished = $pluginMethods | Where-Object { $_.Name -eq 'OnNativePlacementFinished' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$signInstruction = @($nativeFinished.Body.Instructions | Where-Object { [string]$_.Operand -match 'BlueprintSignText::TryApply' })
$completePlacement = $pluginMethods | Where-Object { $_.Name -eq 'CompleteNativePlacement' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$indexWrite = @($completePlacement.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stfld' -and [string]$_.Operand -match '::placementPlanIndex$' })
$completionCall = @($nativeFinished.Body.Instructions | Where-Object { [string]$_.Operand -match '::CompleteNativePlacement' })
Assert-Contract ($signInstruction.Count -eq 1 -and $indexWrite.Count -gt 0 -and $completionCall.Count -eq 1 -and
    $signInstruction[0].Offset -lt $completionCall[0].Offset) 'Sign payload must apply before advancing the native queue.'
Assert-Contract (@($nativeFinished.Body.Instructions | Where-Object { [string]$_.Operand -match 'BlueprintItemStandPlacement::TryApply' }).Count -eq 0 -and
    $automaticDriverOperands -match 'BlueprintItemStandPlacement::TryApply' -and
    $sessionSource -match 'pendingItemStandContents != null' -and
    $sessionSource -match 'if \(!BlueprintItemStandPlacement.CanRollback\(item.Placed, forceDestroy: true\)\) return false;') 'Mount work must be deferred out of TryPlacePiece postfix and content-bearing rollback must not force-destroy.'

$resolveBlueprint = $pluginMethods | Where-Object { $_.Name -eq 'TryResolveBlueprintPieces' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$openBlueprintEditor = $pluginMethods | Where-Object { $_.Name -eq 'BeginBlueprintEditor' -and $_.DeclaringType.Name -eq 'PrecisionPlacementSession' }
$editorResolveCall = @($openBlueprintEditor.Body.Instructions | Where-Object { [string]$_.Operand -match 'PrecisionPlacementSession::TryResolveBlueprintPieces' })
Assert-Contract ($editorResolveCall.Count -eq 1 -and
    $editorResolveCall[0].Previous.OpCode.Name -eq 'ldc.i4.0' -and
    $editorResolveCall[0].Previous.Previous.OpCode.Name -eq 'ldc.i4.1') `
    'Opening the isolated editor must use forEditor=true, not world placement gates.'
$resolveCalls = ($resolveBlueprint.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($resolveCalls -match 'BlueprintItemStandPlacement::CanRestore' -and
    $sessionSource -match 'sourceDocuments\?\.Any\(source => source.ToGeometry\(\).HasOpaqueSections\)' -and
    $resolveCalls -match 'BlueprintMaterialState::CanRestore') `
    'Only supported material recipes and allowlisted native ItemStand state may pass world placement; opaque sections remain blocked.'

$nativeRandomMaterial = $hostAssembly.MainModule.Types | Where-Object Name -eq 'RandomMaterialValues'
$nativeMaterialStart = $nativeRandomMaterial.Methods | Where-Object Name -eq 'Start'
$nativeMaterialStartCalls = @($nativeMaterialStart.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($nativeMaterialStartCalls -match 'GetComponentInParent<ZNetView>' -and
    $nativeMaterialStartCalls -match 'GetComponentInParent<Piece>' -and
    $nativeMaterialStartCalls -match 'InvokeRepeating' -and $nativeMaterialStartCalls -match 'CheckMaterial' -and
    $nativeMaterialStartCalls -notmatch 'ZDO::Set|ZNetView::SetZDO|Deserialize') `
    'Native material Start binding/scheduling or seed ownership contract changed.'

$standType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ItemStand'
foreach ($name in 'GetAttach', 'GetAttachPrefab', 'GetAttachGameObject', 'GetOrientationSettings', 'IsUnsupported', 'IsSupported') {
    $nativeHelper = @($standType.Methods | Where-Object { $_.Name -eq $name -and $_.IsPublic })
    Assert-Contract ($nativeHelper.Count -eq 1) "ItemStand read-only helper changed: $name"
    $operands = ($nativeHelper[0].Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($operands -notmatch 'ZDO::|ZNetView::|Inventory::|Object::Instantiate|::Setup\(') "ItemStand helper gained side effects: $name"
}
$nativeStandVisual = $standType.Methods | Where-Object { $_.Name -eq 'SetVisualItem' -and $_.Parameters.Count -eq 4 }
$standVisualCalls = ($nativeStandVisual.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($standVisualCalls -match 'ItemStand::GetOrientationSettings' -and $standVisualCalls -match 'ItemDrop/ItemData::GetScale' -and
    $standVisualCalls -match 'ZDO::Set' -and $standVisualCalls -match 'IEquipmentVisual::Setup' -and
    $standVisualCalls -match 'ParticleIntensityScaler::SetQuality') 'Native attachment schema changed; preview parity must be reviewed.'
$standPreviewMethods = @($pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintItemStandPreview' })
$nativeStyle = ($hostAssembly.MainModule.Types | Where-Object Name -eq 'ItemStyle').Methods | Where-Object Name -eq 'Setup'
$styleCalls = ($nativeStyle.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($styleCalls -match 'ShaderProps::_Style' -and $styleCalls -match 'MaterialMan::SetValue<System.Int32>' -and
    $styleCalls -notmatch 'ZDO::|ZNetView::|Inventory::|Object::Instantiate') 'Native ItemStyle material-only contract changed.'
$standPreviewCalls = ($standPreviewMethods | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Assert-Contract ($standPreviewCalls -match 'PlacementGhostPreviewView::CreateVisualClone' -and
    $standPreviewCalls -match 'ItemStand::GetOrientationSettings' -and $standPreviewCalls -match 'ItemDrop/ItemData::GetScale' -and
    $standPreviewCalls -match 'GetComponentInChildren<ParticleIntensityScaler>' -and
    $standPreviewCalls -notmatch 'ZDO::|ZNetView::|Inventory::|ItemStand::SetVisualItem|Object::Instantiate|ItemStyle::Setup|ParticleIntensityScaler::SetQuality') `
    'Editor item contents execute native inventory/network/equipment scripts or omit native attachment rules.'
$sceneCalls = ($pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintEditorScene' -and $_.Name -eq 'CreateVisual' } |
    ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Assert-Contract ($sceneCalls -match 'BlueprintItemStandPreview::TryAdd') 'Editor geometry omits item-stand preview.'
$thumbnailCalls = ($pluginMethods | Where-Object { $_.DeclaringType.Name -in @('BlueprintThumbnailRenderer', 'PreviewSession') } |
    ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Assert-Contract ([regex]::Matches($thumbnailCalls, 'BlueprintItemStandPreview::TryAdd').Count -eq 1 -and
    [regex]::Matches($thumbnailCalls, 'BlueprintThumbnailRenderer::CreateVisuals').Count -eq 2) `
    'Thumbnail and rotating preview must both reach the shared content adapter.'
$thumbnailVisualCalls = ($thumbnailRenderer.Methods | Where-Object Name -eq 'CreateVisuals' |
    ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Assert-Contract ($thumbnailVisualCalls -match 'BlueprintEditorScene::CreatePlaceholder' -and
    $thumbnailVisualCalls -match 'BlueprintItemStandPreview::TryAdd' -and
    $thumbnailVisualCalls -match 'VisualCloneOwnedAssets::Track' -and
    $thumbnailVisualCalls -notmatch 'ZDO::|Inventory::|ConsumeResources') `
    'Partial previews must retain marker ownership and contents without world mutation.'

$nativeCapture = ($pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintItemStandCapture' -and $_.Name -eq 'TryCapture' })
$captureCalls = ($nativeCapture.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($captureCalls -match 'ItemStand::GetAttachedItem' -and $captureCalls -match 'ObjectDB::GetItemPrefab' -and
    $captureCalls -match 'ZDOVars::s_itemData' -and $captureCalls -match 'ZDO::GetByteArray' -and
    $captureCalls -match 'BlueprintItemStandContents::TryRead' -and $captureCalls -match 'ZDO::get_DataRevision' -and
    $captureCalls -match 'System.Array::Clone' -and
    $captureCalls -notmatch 'ZDO::Set|ZNetView::InvokeRPC|Inventory::|Object::Instantiate|ItemData::Load|ItemDrop::LoadFromZDO') `
    'Native ItemStand capture must preserve the raw package without executing or mutating it.'
$standView = $standType.Fields | Where-Object Name -eq 'm_nview'
Assert-Contract ($standView.IsPrivate -and $standView.FieldType.FullName -eq 'ZNetView') 'ItemStand native view field changed.'
$standQueue = $standType.Fields | Where-Object Name -eq 'm_queuedItem'
Assert-Contract ($standQueue.IsPrivate -and $standQueue.FieldType.FullName -eq 'ItemDrop/ItemData') 'Native stand queue reference changed.'
$mountMethods = @($pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintItemStandPlacement' -and $_.HasBody })
$mountCalls = ($mountMethods | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
foreach ($pattern in 'ZNetView::IsOwner', 'PrivateArea::CheckAccess', 'ItemStand::UseItem', 'Inventory::ContainsItem',
    'ItemDrop/ItemData::Clone', 'ItemDrop/ItemData::Save', 'BlueprintItemStandCapture::TryCapture',
    'ItemStand::DestroyAttachment', 'ItemStand::Interact', 'MonoBehaviour::CancelInvoke') {
    Assert-Contract ($mountCalls -match $pattern) "Native mount adapter omits gate: $pattern"
}
Assert-Contract ($mountCalls -notmatch 'ZDO::Set|Inventory::RemoveOneItem|Inventory::AddItem|ItemData::Load|ItemDrop::LoadFromZDO|ZNetView::InvokeRPC|Object::Instantiate|ClaimOwnership') 'Mount adapter bypasses native inventory/ownership or executes archive bytes.'
$mountSource = Get-Content -LiteralPath (Join-Path $projectRoot 'src/BuildWorks/BlueprintItemStandPlacement.cs') -Raw
# C# lowers ReferenceEquals to reference branches/ceq, not a System.Object call.
Assert-Contract ([regex]::Matches($mountSource, 'ReferenceEquals\(QueuedItem.GetValue\(stand\), item\)').Count -eq 2 -and
    [regex]::Matches($mountSource, 'ReferenceEquals\(StandView.GetValue\(stand\), view\)').Count -ge 4) 'Mount queue/view identity checks changed.'
$nativePlacementOrder = ($player.Methods | Where-Object Name -eq 'UpdatePlacement').Body.Instructions
$nativePlaceCall = @($nativePlacementOrder | Where-Object { [string]$_.Operand -match 'Player::TryPlacePiece' })
$nativeSpendCall = @($nativePlacementOrder | Where-Object { [string]$_.Operand -match 'Player::ConsumeResources' })
Assert-Contract ($nativePlaceCall.Count -eq 1 -and $nativeSpendCall.Count -eq 1 -and $nativePlaceCall[0].Offset -lt $nativeSpendCall[0].Offset) 'Native construction spending order changed; review deferred attachment.'
$spendIndex = $nativePlacementOrder.IndexOf($nativeSpendCall[0])
Assert-Contract ($nativePlacementOrder[$spendIndex - 3].OpCode.Name -eq 'ldc.i4.0' -and
    $nativePlacementOrder[$spendIndex - 2].OpCode.Name -eq 'ldc.i4.m1' -and
    $nativePlacementOrder[$spendIndex - 1].OpCode.Name -eq 'ldc.i4.1') 'Native construction payment arguments changed; reservation expects quality0/all qualities/multiplier1.'
$supplyGate = $mountMethods | Where-Object Name -eq 'SuppliesAvailable'
$supplyGateCalls = ($supplyGate.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($pattern in 'BlueprintItemStandPlacement::SnapshotItem', 'Inventory::ContainsItem', 'Supply::Items',
        'Reservation::Amount', 'Reservation::Snapshot', 'Inventory::GetAllItems', 'Player::GetCurrentCraftingStation',
        'Piece::FreeBuildKey', 'ZoneSystem::GetGlobalKey', 'Piece/Requirement::GetAmount', '::m_upgraderResource', '::m_worldLevel') {
    Assert-Contract ($supplyGateCalls -match $pattern) "Supply reservation omits native payment gate: $pattern"
}
Assert-Contract (@($supplyGate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'add.ovf' }).Count -gt 0) `
    'Shared contents quantities must reject integer overflow.'
$containerReservation = $mountMethods | Where-Object Name -eq 'ReserveContainer'
$containerReservationCalls = ($containerReservation.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($containerReservationCalls -match 'BlueprintContainerInventory::TryRead' -and
    $containerReservationCalls -match 'BlueprintItemStandPlacement::SnapshotItem' -and
    $containerReservationCalls -notmatch 'Inventory::(?:AddItem|RemoveItem|Load)|Container::|ZNetView::|ZDO::Set|RPC') `
    'Container reservation must decode demands and snapshot provided properties without moving items or changing world state.'
foreach ($name in 'ShouldBlockNativePlacement','ContinueAutomaticPlacement') {
    $method = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'PrecisionPlacementSession' -and $_.Name -eq $name }
    $calls = ($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($calls -match 'BlueprintItemStandPlacement::SuppliesAvailable') "Placement bypasses held contents in $name"
}
$nativeStandDrop = $standType.Methods | Where-Object Name -eq 'DropItem'
$standDropCalls = ($nativeStandDrop.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($standDropCalls -match 'ItemDrop::LoadFromExternalZDO') 'Native stand item restoration path changed.'
$itemDropType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ItemDrop'
$externalItemLoad = $itemDropType.Methods | Where-Object Name -eq 'LoadFromExternalZDO'
$externalLoadCalls = ($externalItemLoad.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($externalLoadCalls -match 'ItemDrop::LoadFromZDO') 'Native external item load no longer reads the item package.'
$nativeItemLoad = $itemDropType.Methods | Where-Object Name -eq 'LoadFromZDO'
$loadItemCalls = ($nativeItemLoad.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($nativeItemLoad.IsPublic -and $loadItemCalls -match 'ZDOVars::s_itemData' -and
    $loadItemCalls -match 'ZDO::GetByteArray') 'Native item package storage changed.'
$worldSelection = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'PrecisionPlacementSession' -and $_.Name -eq 'TrySaveWorldSelection' }
$worldCaptureCalls = ($worldSelection.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($worldCaptureCalls -match 'BlueprintItemStandCapture::TryCapture' -and
    $worldCaptureCalls -match 'ForeignPartData::FromGeometry') 'World selection discards native holder state.'

$knownMaterial = ($hostAssembly.MainModule.Types | Where-Object Name -eq 'Player').Methods | Where-Object Name -eq 'IsMaterialKnown'
$knownCalls = ($knownMaterial.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($knownMaterial.IsPublic -and $knownCalls -match '::m_knownMaterial' -and $knownCalls -match '::Contains' -and
    $knownCalls -notmatch 'Inventory::|ZDO::Set|RPC') 'Known-item catalogue filter no longer reads native player progression.'
$itemList = ($hostAssembly.MainModule.Types | Where-Object Name -eq 'ObjectDB').Fields | Where-Object Name -eq 'm_items'
Assert-Contract ($itemList.IsPublic -and $itemList.FieldType.FullName -eq 'System.Collections.Generic.List`1<UnityEngine.GameObject>') 'Native item catalogue list changed.'
$itemProvider = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'PrecisionPlacementSession' -and $_.Name -eq 'BlueprintEditorItemCatalogItems' }
$providerCalls = ($itemProvider.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($providerCalls -match 'Player::IsMaterialKnown' -and $providerCalls -match 'ObjectDB::m_items' -and
    $providerCalls -notmatch 'Inventory::|ZDO::|Object::Instantiate|ItemDrop/ItemData::Load') 'Item catalogue lost native progression or creates/loads runtime objects.'
$invalidItemHandlers = @($itemProvider.Body.ExceptionHandlers | Where-Object { $_.CatchType -and $_.CatchType.FullName -eq 'System.ArgumentException' })
Assert-Contract ($invalidItemHandlers.Count -eq 1) 'Invalid mod-items must have a narrow ArgumentException handler.'
$invalidItemHandler = $invalidItemHandlers[0]
$invalidItemInstructions = @($itemProvider.Body.Instructions | Where-Object { $_.Offset -ge $invalidItemHandler.HandlerStart.Offset -and $_.Offset -lt $invalidItemHandler.HandlerEnd.Offset })
$invalidItemCalls = ($invalidItemInstructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$catalogueSort = $itemProvider.Body.Instructions | Where-Object { [string]$_.Operand -match '::OrderBy<' } | Select-Object -First 1
$invalidItemLeave = @($invalidItemInstructions | Where-Object { $_.OpCode.Name -like 'leave*' })
Assert-Contract ($invalidItemCalls -match 'Debug::LogWarning' -and
    @($invalidItemInstructions | Where-Object { $_.OpCode.Name -in @('throw', 'rethrow') }).Count -eq 0 -and
    $invalidItemLeave.Count -eq 1 -and $catalogueSort -and
    $invalidItemLeave[0].Operand.Offset -ge $invalidItemHandler.HandlerEnd.Offset -and
    $invalidItemLeave[0].Operand.Offset -lt $catalogueSort.Offset) 'An invalid mod-item must be reported and skipped without aborting the catalogue.'
$continuationCalls = ($itemProvider.Body.Instructions | Where-Object {
    $_.Offset -ge $invalidItemLeave[0].Operand.Offset -and $_.Offset -lt $catalogueSort.Offset
} | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($continuationCalls -match '::MoveNext\(') 'Invalid-item handler must continue the item enumeration.'

$containerType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'Container'
$inventoryType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'Inventory'
$containerView = $containerType.Fields | Where-Object Name -eq 'm_nview'
Assert-Contract ($containerView.IsPrivate -and $containerView.FieldType.FullName -eq 'ZNetView') 'Container view contract changed.'
$containerAwake = $containerType.Methods | Where-Object Name -eq 'Awake'
$containerAwakeCalls = ($containerAwake.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($containerAwakeCalls -match 'Container::m_rootObjectOverride' -and $containerAwakeCalls -match 'Container::m_nview') 'Container capture must use the native cached override-aware view.'
foreach ($entry in @(@($containerType, 'GetInventory'), @($containerType, 'IsInUse'), @($containerType, 'CheckAccess'),
        @($inventoryType, 'GetWidth'), @($inventoryType, 'GetHeight'), @($inventoryType, 'GetAllItems'))) {
    $method = @($entry[0].Methods | Where-Object { $_.Name -eq $entry[1] -and
        $_.Parameters.Count -eq $(if ($entry[1] -eq 'CheckAccess') { 1 } else { 0 }) })
    Assert-Contract ($method.Count -eq 1 -and $method[0].HasBody) "Storage read helper changed: $($entry[1])"
    $calls = ($method[0].Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($calls -notmatch 'ZDO::Set|RPC|Object::Instantiate|Inventory::Load|Inventory::Changed' -and
        @($method[0].Body.Instructions | Where-Object { $_.OpCode.Name -in @('stfld', 'stsfld') }).Count -eq 0) "Storage read helper gained mutations: $($entry[1])"
}
$containerAccess = $containerType.Methods | Where-Object Name -eq 'CheckAccess'
Assert-Contract ($containerAccess.IsPrivate -and $containerAccess.Parameters.Count -eq 1 -and
    $containerAccess.Parameters[0].ParameterType.FullName -eq 'System.Int64' -and
    (($containerAccess.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n") -match 'Piece::GetCreator') 'Container private access/creator gate changed.'
$inventorySave = $inventoryType.Methods | Where-Object Name -eq 'Save'
$inventorySaveCalls = ($inventorySave.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($inventorySave.IsPublic -and $inventorySave.Parameters.Count -eq 1 -and
    @($inventorySave.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldc.i4.s' -and [int]$_.Operand -eq 110 }).Count -eq 1 -and
    $inventorySaveCalls -match 'ZPackage::Write\(System.Int32\)' -and $inventorySaveCalls -match 'ZPackage::Write\(System.UInt16\)' -and
    $inventorySaveCalls -match 'ItemDrop/ItemData::Save\(ZPackage\)' -and
    $inventorySaveCalls -notmatch 'ZDO::|RPC|Object::Instantiate|Inventory::Changed|Inventory::Load' -and
    @($inventorySave.Body.Instructions | Where-Object { $_.OpCode.Name -in @('stfld', 'stsfld') }).Count -eq 0) 'Native private Inventory.Save packet/version/count or side effects changed.'
$itemDataType = ($hostAssembly.MainModule.Types | Where-Object Name -eq 'ItemDrop').NestedTypes | Where-Object Name -eq 'ItemData'
Assert-Contract ($itemDataType.BaseType.FullName -eq 'System.Object' -and
    @($itemDataType.Methods | Where-Object { $_.Name -in @('Equals', 'GetHashCode') }).Count -eq 0 -and
    @($itemDataType.Interfaces | Where-Object { [string]$_.InterfaceType -match 'IEquatable' }).Count -eq 0) `
    'ItemData identity semantics changed; construction debit and contents reservations require reference equality.'
$itemSave = $itemDataType.Methods | Where-Object { $_.Name -eq 'Save' -and $_.Parameters.Count -eq 1 }
$itemSaveCalls = ($itemSave.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($field in 'm_durability', 'm_gridPos', 'm_worldLevel', 'm_quality', 'm_stack', 'm_variant', 'm_crafterID', 'm_crafterName', 'm_dropPrefab', 'm_customData') {
    Assert-Contract ($itemSaveCalls -match "ItemDrop/ItemData::$field") "Inventory serializer field changed: $field"
}
Assert-Contract ($itemSaveCalls -notmatch 'ZDO::|RPC|Object::Instantiate|Inventory::Load|Inventory::Changed' -and
    @($itemSave.Body.Instructions | Where-Object { $_.OpCode.Name -in @('stfld', 'stsfld') }).Count -eq 0) 'Native ItemData.Save gained live-state side effects.'
$nativeContainerSave = $containerType.Methods | Where-Object Name -eq 'Save'
$nativeContainerSaveCalls = ($nativeContainerSave.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($nativeContainerSaveCalls -match 'ZDOVars::s_items' -and $nativeContainerSaveCalls -match 'ZDO::Set\(System.Int32,System.Byte\[\]\)') 'Native Container no longer persists its exact byte package.'
# A future survival transfer must use exact empty cells, then acknowledge both inventories;
# this native operation can partially mutate before false/exception and is not a transaction.
$moveToCell = @($inventoryType.Methods | Where-Object { $_.Name -eq 'MoveItemToThis' -and $_.Parameters.Count -eq 5 })
$addToCell = @($inventoryType.Methods | Where-Object { $_.Name -eq 'AddItem' -and $_.Parameters.Count -eq 5 -and
    $_.Parameters[4].ParameterType.FullName -eq 'System.Boolean' })
$inventoryChanged = @($inventoryType.Methods | Where-Object { $_.Name -eq 'Changed' -and $_.Parameters.Count -eq 2 })
$containerChanged = @($containerType.Methods | Where-Object Name -eq 'OnContainerChanged')
$itemCloneLayout = @($itemDataType.Methods | Where-Object Name -eq 'Clone')
Assert-Contract ($moveToCell.Count -eq 1 -and $moveToCell[0].IsPublic -and
    $moveToCell[0].FullName -eq 'System.Boolean Inventory::MoveItemToThis(Inventory,ItemDrop/ItemData,System.Int32,System.Int32,System.Int32)' -and
    $addToCell.Count -eq 1 -and $addToCell[0].IsPrivate -and
    $inventoryChanged.Count -eq 1 -and $inventoryChanged[0].IsPrivate -and
    $containerChanged.Count -eq 1 -and $containerChanged[0].IsPrivate -and
    $itemCloneLayout.Count -eq 1 -and $itemCloneLayout[0].IsPublic) 'Native positional inventory transfer signatures changed.'
$moveToCell = $moveToCell[0]; $addToCell = $addToCell[0]
$inventoryChanged = $inventoryChanged[0]; $containerChanged = $containerChanged[0]; $itemCloneLayout = $itemCloneLayout[0]
$emptyCellClone = @($addToCell.Body.Instructions | Where-Object { [string]$_.Operand -match 'ItemDrop/ItemData::Clone\(' })
$destinationAdd = @($addToCell.Body.Instructions | Where-Object { [string]$_.Operand -match 'List.*ItemDrop/ItemData.*::Add\(' })
$sourceDebit = $addToCell.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'stfld' -and
    [string]$_.Operand -match 'ItemDrop/ItemData::m_stack' } | Select-Object -Last 1
$localPlayerFlag = @($addToCell.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldsfld' -and
    [string]$_.Operand -match 'Player::m_localPlayerExists' })
$destinationChanged = @($addToCell.Body.Instructions | Where-Object { [string]$_.Operand -match 'Inventory::Changed\(' })
Assert-Contract ($emptyCellClone.Count -eq 1 -and $destinationAdd.Count -eq 1 -and
    $localPlayerFlag.Count -eq 1 -and $destinationChanged.Count -eq 1 -and
    $emptyCellClone[0].Offset -lt $destinationAdd[0].Offset -and $destinationAdd[0].Offset -lt $sourceDebit.Offset -and
    $sourceDebit.Offset -lt $localPlayerFlag[0].Offset -and $localPlayerFlag[0].Offset -lt $destinationChanged[0].Offset -and
    $localPlayerFlag[0].Next.OpCode.Name -in @('brfalse','brfalse.s') -and
    $localPlayerFlag[0].Next.Operand.Offset -gt $destinationChanged[0].Offset) `
    'Native empty-cell transfer copy/debit/callback order or local-player-only save boundary changed.'
$containerChangedCalls = ($containerChanged.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$inventoryChangedCalls = ($inventoryChanged.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$cloneCalls = ($itemCloneLayout.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($containerChangedCalls -match 'Container::m_loading' -and $containerChangedCalls -match 'Container::IsOwner\(' -and
    $containerChangedCalls -match 'Container::Save\(' -and $containerAwakeCalls -match 'Container::OnContainerChanged\(' -and
    $containerAwakeCalls -match 'Inventory::m_onChanged' -and $inventoryChangedCalls -match 'Inventory::m_onChanged' -and
    $inventoryChangedCalls -match 'Inventory::UpdateTotalWeight\(') 'Native container owner/loading/callback save chain changed.'
Assert-Contract ($cloneCalls -match 'Object::MemberwiseClone\(' -and $cloneCalls -match 'ItemDrop/ItemData::m_customData' -and
    $cloneCalls -match 'Dictionary.*::\.ctor\(' -and $cloneCalls -notmatch 'ItemDrop/ItemData::Load|Inventory::|ZDO::') `
    'Native provided-item clone no longer preserves properties with an independent custom-data map.'
# Postpayment container transfer is deliberately gated off in world placement.
# Validate both the host APIs and the compiled local primitive; a true native bool is not custody proof.
foreach ($field in @(@('m_loading', 'System.Boolean'), @('m_lastRevision', 'System.UInt32'))) {
    $member = @($containerType.Fields | Where-Object Name -eq $field[0])
    Assert-Contract ($member.Count -eq 1 -and $member[0].IsPrivate -and $member[0].FieldType.FullName -eq $field[1]) "Container transfer metadata changed: $($field[0])"
}
$humanoidType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'Humanoid'
foreach ($api in @(@($player, 'RemoveEquipAction', 1, 'System.Void'), @($player, 'IsEquipActionQueued', 1, 'System.Boolean'),
        @($humanoidType, 'IsItemEquiped', 1, 'System.Boolean'), @($humanoidType, 'UnequipItem', 2, 'System.Void'))) {
    $method = @($api[0].Methods | Where-Object { $_.Name -eq $api[1] -and $_.Parameters.Count -eq $api[2] })
    Assert-Contract ($method.Count -eq 1 -and $method[0].IsPublic -and -not $method[0].IsStatic -and
        $method[0].ReturnType.FullName -eq $api[3] -and $method[0].Parameters[0].ParameterType.FullName -eq 'ItemDrop/ItemData') "Native equip transfer API changed: $($api[1])"
}
$inventoryGuiType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'InventoryGui'
$visible = @($inventoryGuiType.Methods | Where-Object { $_.Name -eq 'IsVisible' -and $_.Parameters.Count -eq 0 })
Assert-Contract ($visible.Count -eq 1 -and $visible[0].IsPublic -and $visible[0].IsStatic -and
    $visible[0].ReturnType.FullName -eq 'System.Boolean') 'Native inventory UI visibility gate changed.'
$transferType = $pluginTypes | Where-Object Name -eq 'BlueprintStorageTransfer'
Assert-Contract ($null -ne $transferType) 'Compiled container transfer primitive is missing.'
$move = $transferType.Methods | Where-Object Name -eq 'MoveNext'
$moveCalls = ($move.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($pattern in '::ContextValid', '::DeliveredItemsIntact', '::RemainingAvailable', '::Counts', '::SameCounts',
        '(Player|Humanoid)::RemoveEquipAction', 'Humanoid::UnequipItem', 'Inventory::MoveItemToThis', 'BlueprintStorageCapture::TryCapture',
        'Entry::SourceAfter', 'Entry::DestinationAfter', 'Entry::LocalSaveConfirmed') {
    Assert-Contract ($moveCalls -match $pattern) "Container transfer observation/gate missing: $pattern"
}
Assert-Contract (@($move.Body.Instructions | Where-Object { [string]$_.Operand -match 'Inventory::MoveItemToThis' }).Count -eq 1 -and
    $move.Body.ExceptionHandlers.Count -ge 2 -and $moveCalls -notmatch 'Container::Save|Inventory::Load|ZDO::Set|ItemData::Load|RPC|ItemDrop::DropItem') 'Container transfer gained replay/archive load or lost exception observations.'
$destinationReady = $transferType.Methods | Where-Object Name -eq 'DestinationReady'
$destinationReadyCalls = ($destinationReady.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($moveCalls -match '::DestinationReady' -and $destinationReadyCalls -match '::DeliveredItemsIntact' -and
    $destinationReadyCalls -match 'BlueprintStorageCapture::TryCapture' -and $destinationReadyCalls -match 'ZDO::get_DataRevision' -and
    $destinationReadyCalls -match 'Inventory::GetAllItems') 'Transfer must validate exact live/saved destination before any mutation.'
$beforeJournal = @($move.Body.Instructions | Where-Object { [string]$_.Operand -match 'BlueprintStorageTransferJournal::BeforeMove' })
$unequipTransfer = @($move.Body.Instructions | Where-Object { [string]$_.Operand -match 'Humanoid::UnequipItem' })
$nativeTransfer = @($move.Body.Instructions | Where-Object { [string]$_.Operand -match 'Inventory::MoveItemToThis' })
Assert-Contract ($beforeJournal.Count -eq 1 -and $beforeJournal[0].Offset -lt $unequipTransfer[0].Offset -and
    $unequipTransfer[0].Offset -lt $nativeTransfer[0].Offset -and $moveCalls -match 'BlueprintStorageTransferJournal::Observed' -and
    $moveCalls -match '::get_JournalError|::set_JournalError') 'Write-ahead journal must precede unequip/native mutation and failures must stop transfer.'
$journalType = $pluginTypes | Where-Object Name -eq 'BlueprintStorageTransferJournal'
$returnBegin = $transferType.Methods | Where-Object Name -eq 'TryBeginReturn'
$returnCalls = ($returnBegin.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$returnShared = $transferType.Methods | Where-Object Name -eq 'BeginReturn'
$returnSharedCalls = ($returnShared.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($returnCalls -match '::ReturnOperationId' -and $returnCalls -match '::CanStartReturn' -and
    $returnCalls -match 'BlueprintStorageTransferJournal::Read' -and $returnCalls -match '::cancelling' -and
    $returnCalls -match '::BeginReturn' -and $returnSharedCalls -match 'BlueprintStorageTransferJournal::Begin' -and $returnSharedCalls -match '::cancelling' -and
    $moveCalls -match '::ReceiverItemsIntact' -and
    $moveCalls -match '::cancelling' -and $returnCalls -notmatch 'Inventory::MoveItem|ItemData::Load|RPC|DropItem') 'Return must bind current evidence, freeze its parent and reuse the observed native mover.'
$recoveryRead = $transferType.Methods | Where-Object Name -eq 'ReadRecovery'
$recoveryCalls = ($recoveryRead.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
$recoveryMatch = $transferType.Methods | Where-Object Name -eq 'MatchesRecovery'
$recoveryMatchCalls = ($recoveryMatch.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($recoveryCalls -match '::TransferMarker' -and $recoveryCalls -match '::CanStartReturn' -and $recoveryCalls -match '::MatchesRecovery' -and
    $recoveryCalls -notmatch 'ZDO::Set|Inventory::Move|Inventory::Load|RPC|DropItem' -and
    $recoveryMatchCalls -match '::InventoryDigest' -and $recoveryMatchCalls -match '::BytesDigest' -and $recoveryMatchCalls -notmatch 'Observation::Revision') 'Recovery must bind the persistent marker/current full state, never old revision or mutating replay.'
$prepare = $transferType.Methods | Where-Object Name -eq 'TryBegin'
$prepareJournal = @($prepare.Body.Instructions | Where-Object { [string]$_.Operand -match 'BlueprintStorageTransferJournal::Begin' })
$prepareMarker = @($prepare.Body.Instructions | Where-Object { [string]$_.Operand -match 'ZDO::Set\(System.Int32,System.String\)' })
Assert-Contract ($prepareJournal.Count -eq 1 -and $prepareMarker.Count -eq 1 -and $prepareJournal[0].Offset -lt $prepareMarker[0].Offset -and
    $moveCalls -match '::waitingForMarker' -and $moveCalls -match 'Stopwatch::GetTimestamp') 'Persistent marker must follow WAL preparation and wait boundedly for native cache synchronization.'
$journalCalls = ($journalType.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($journalCalls -match 'FileStream::Flush\(System.Boolean\)' -and $journalCalls -match 'SHA256::Create' -and
    $journalCalls -match '::CheckParents' -and $journalCalls -notmatch 'File::Delete|\bInventory::|ZDO::Set|RPC|DropItem') 'Persisted evidence must flush and validate without replay, deletion or world mutation.'
$loadedInventory = $plugin.MainModule.GetType('OstrixMods.BuildWorks.BlueprintStorageLoadedInventory')
Assert-Contract ($null -ne $loadedInventory) 'Exact loaded inventory comparison is missing.'
$loadedCalls = ($loadedInventory.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($loadedCalls -match 'BlueprintContainerInventory::TryRead' -and $loadedCalls -match 'ItemData::m_durability' -and
    $loadedCalls -match 'ItemData::m_cheated' -and $loadedCalls -match 'ItemData::m_crafterID' -and
    $loadedCalls -notmatch '\bInventory::Save|\bInventory::Load|ItemData::Save|ZDO::Set|DropItem|MoveItem') 'Loaded comparison must read exact native fields without reserialization or mutation.'
$loadedMatch = $loadedInventory.Methods | Where-Object Name -eq 'Matches'
$loadDurabilityProduct = @($loadedMatch.Body.Instructions | Where-Object {
    $_.OpCode.Code -eq 'Mul' -and $_.Previous.OpCode.Code -eq 'Ldc_R4' -and [single]$_.Previous.Operand -eq [single]0.01
})
Assert-Contract ($loadDurabilityProduct.Count -eq 1 -and $loadDurabilityProduct[0].Next.OpCode.Code -eq 'Conv_R4') 'Loaded durability must round to native Single before comparison on Mono.'
Assert-Contract ($journalCalls -match '::ValidatePlayerInventory' -and $journalCalls -match 'Persistence::PlayerInventory' -and
    $journalCalls -match 'Persistence::SourceSession') 'Paired reload evidence must retain validated inventory bytes and source session.'
$nativeId = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ZDOID'
foreach ($property in @(@('UserID','System.Int64'), @('ID','System.UInt32'))) {
    $member = @($nativeId.Properties | Where-Object Name -eq $property[0])
    Assert-Contract ($member.Count -eq 1 -and $member[0].GetMethod.IsPublic -and $member[0].PropertyType.FullName -eq $property[1]) 'Session UID diagnostics must use resolved UserID/ID; restart identity uses the persistent operation marker.'
}
$nativeZdo = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ZDO'
$nativeZdoExtra = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ZDOExtraData'
foreach ($entry in @(@(($nativeZdo.Methods | Where-Object Name -eq 'Save'), 'D0B1C9D4C16E6FF5D21AA04B37DDD95B8744A55A0D9A45F1D97376EA3E5A57F9'),
        @(($nativeZdo.Methods | Where-Object Name -eq 'Load'), '89E0096EE970E61426FDCAFB0606C602B9F3B40831C77DAA4B33A5A0C3E2D121'),
        @(($nativeZdo.Methods | Where-Object { $_.Name -eq 'Set' -and $_.Parameters.Count -eq 2 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Int32' -and $_.Parameters[1].ParameterType.FullName -eq 'System.String' }), '5AB27031232D58248CAF1903E53252E1DACC2A01F88D627D429D3AFF17F5A998'),
        @(($nativeZdo.Methods | Where-Object { $_.Name -eq 'GetString' -and $_.Parameters.Count -eq 2 -and $_.Parameters[0].ParameterType.FullName -eq 'System.Int32' -and $_.Parameters[1].ParameterType.FullName -eq 'System.String' }), '5911E65B99DDD609D923A3F0B2D7C8890C7FAACBEF91DC4826D004ADC6B7129F'),
        @(($nativeZdoExtra.Methods | Where-Object Name -eq 'PrepareSave'), 'FA2B8D3BEE7F1A75F2F1FC5265A99B7E18D7079294C3AB3D32CEB4B30E619B30'),
        @(($containerType.Methods | Where-Object Name -eq 'CheckForChanges'), 'E27229CAD43E40597CFA39C3A9A1C8216D71A5101ADA47DE1971AFC14CBB422A'),
        @(($containerType.Methods | Where-Object Name -eq 'Load'), 'E42C0B1DBC05A9CAA58BCAFD3CA75E9DBA6A907318C9D2AA026A0A03118D9C5D'))) {
    Assert-Contract ((Get-NativeLayoutHash $entry[0]) -eq $entry[1]) "Native marker/reload contract changed: $($entry[0].FullName)"
}
$rollback = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintItemStandPlacement' -and $_.Name -eq 'CanRollback' }
$rollbackCalls = ($rollback.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($rollbackCalls -match 'GetComponent<Container>' -and $rollbackCalls -match 'BlueprintStorageCapture::CanRemoveEmpty') 'Both normal and forced rollback must refuse filled/unverified storage.'
$removal = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageCapture' -and $_.Name -eq 'CanRemoveEmpty' }
$removalCalls = ($removal.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($removalCalls -match '::TryCapture' -and $removalCalls -match 'Inventory::GetAllItems' -and
    $removalCalls -match 'Piece::GetCreator' -and $removalCalls -match 'ZNetView::IsOwner' -and
    $removalCalls -notmatch 'Container::Save|Inventory::Load|ZDO::Set|RPC|DropAllItems|Destroy') 'Empty-storage cancellation guard must stay read-only.'
$storageCapture = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageCapture' -and $_.Name -eq 'TryCapture' }
$inventorySnapshot = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageCapture' -and $_.Name -eq 'TrySnapshotInventory' }
$storageCalls = (@($storageCapture.Body.Instructions) + @($inventorySnapshot.Body.Instructions) | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ((($storageCapture.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n") -match '::TrySnapshotInventory') 'Container capture must use the shared bounded native snapshot.'
foreach ($pattern in 'Player::GetPlayerID', 'PrivateArea::CheckAccess', 'Container::IsInUse', 'Container::GetInventory',
    'Inventory::GetAllItems', 'Inventory::GetWidth', 'Inventory::GetHeight', 'Inventory::Save', 'ZDO::GetByteArray', 'ZDO::get_DataRevision',
    'ZDOVars::s_items', 'ZDOVars::s_addedDefaultItems', 'ZDO::GetBool', 'GetComponentsInChildren<Container>') {
    Assert-Contract ($storageCalls -match $pattern) "Container snapshot omits required gate: $pattern"
}
Assert-Contract ($storageCalls -notmatch 'Container::Save|Container::Load|Inventory::Load|ZDO::Set|RPC|Object::Instantiate|ItemDrop/ItemData::Load') 'Container snapshot mutates, restores or executes live state.'
Assert-Contract ($storageCalls -match 'BlueprintContainerInventory::TryRead') 'Container snapshot skips full inert record/dependency validation.'
$profileType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'PlayerProfile'
foreach ($entry in @(@(($profileType.Methods | Where-Object Name -eq 'SavePlayerToDisk'), '817B5007558E60B21D52418B2963669DB2E2A959EF79EB7EBBA119D0FF5824DB'),
        @(($profileType.Methods | Where-Object Name -eq 'GetPath'), '48ED45B9C17740FB81DF61B2C27FAE258D29E61AAA73E5F3BB527CA2D31354FA'),
        @((($hostAssembly.MainModule.Types | Where-Object Name -eq 'Player').Methods | Where-Object { $_.Name -eq 'Save' -and $_.Parameters.Count -eq 1 }), 'FA7F189A3414903FAE720CFFC442B3266A1CDE38C957A27DEB7D88DEA19E60E2'),
        @((($hostAssembly.MainModule.Types | Where-Object Name -eq 'ZPackage').Methods | Where-Object Name -eq 'GenerateHash'), 'CBA20AF2027558EB886A8CB8C7BB1C177882F220EA4DA66E5003CF0822D9BFD2'))) {
    Assert-Contract ((Get-NativeLayoutHash $entry[0]) -eq $entry[1]) "Native canonical-profile contract changed: $($entry[0].FullName)"
}
$profileAdapter = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStoragePlayerProfile' -and $_.Name -ne 'TrySaveAndVerify' }
$profileCalls = ($profileAdapter.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($profileCalls -match 'NativePlayerProfileReadback::TryVerify' -and $profileCalls -match '::TrySnapshotInventory' -and
    $profileCalls -match 'PlayerProfile::m_fileSource' -and $profileCalls -match 'PlayerProfile::GetPath' -and
    $profileCalls -notmatch 'Inventory::Load|PlayerProfile::Save|Game::SavePlayerProfile|Inventory::Move|ZDO::Set|RPC|DropItem|Destroy') 'Profile evidence must read canonical Local data without saves, restore or custody mutation.'
$profileSave = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStoragePlayerProfile' -and $_.Name -eq 'TrySaveAndVerify' }
$profileSaveCalls = ($profileSave.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($profileSaveCalls -match 'Player::Save' -and $profileSaveCalls -match 'Game::SavePlayerProfile' -and
    $profileSaveCalls -match 'BlueprintStorageProfileWrite::Begin' -and $profileSaveCalls -match '::get_Completed' -and
    $profileSaveCalls -notmatch 'm_customData|Inventory::Load|Inventory::Move|ZDO::Set|RPC|DropItem|Destroy') 'Fresh profile save must observe native write/replacement without modifying player metadata or custody.'
$nativeWriter = $utilsAssembly.MainModule.Types | Where-Object Name -eq 'FileWriter'
$writerSource = @($nativeWriter.Properties | Where-Object Name -eq 'm_fileSource')
$writerFinish = $nativeWriter.Methods | Where-Object Name -eq 'Finish'
$writerFinishCalls = ($writerFinish.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($writerSource.Count -eq 1 -and $writerSource[0].GetMethod.IsPublic -and
    @($nativeWriter.Fields | Where-Object { $_.Name -eq 'm_path' -and $_.FieldType.FullName -eq 'System.String' }).Count -eq 1 -and
    $writerFinish.IsPublic -and $writerFinishCalls -match 'FileStream::Flush\(System.Boolean\)') 'Native Local profile writer must expose source as a property, exact path and flushing Finish.'
$nativeReplace = ($utilsAssembly.MainModule.Types | Where-Object Name -eq 'FileHelpers').Methods | Where-Object { $_.Name -eq 'ReplaceOldFile' -and $_.Parameters.Count -eq 5 }
Assert-Contract ($nativeReplace.IsPublic -and $nativeReplace.IsStatic -and
    $nativeReplace.Parameters[4].ParameterType.FullName -eq 'FileHelpers/FileSource') 'Native profile replacement signature changed.'
$observerMethods = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageProfileWrite' }
$observerCalls = ($observerMethods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($observerCalls -match 'FileWriter::get_m_fileSource' -and $observerCalls -match 'FileWriter::get_Status' -and
    $observerCalls -notmatch 'File::Write|File::Move|File::Delete|m_customData|Inventory::|ZDO::Set') 'Save observer must only observe the current native writer and replacement.'
$playerReadback = $transferType.Methods | Where-Object Name -eq 'PlayerReadbackCustodyValid'
$playerReadbackCalls = ($playerReadback.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($playerReadbackCalls -match '::get_CompleteInMemory' -and $playerReadbackCalls -match '::InventoryDigest' -and
    $playerReadbackCalls -match 'BlueprintStorageTransferJournal::Read' -and $playerReadbackCalls -match '::DestinationReady') 'Player evidence must remain bound to exact completed journal custody.'
$emptyContainer = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageCapture' -and $_.Name -eq 'CanPlaceWithoutContents' }
$emptyContainerCalls = ($emptyContainer.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($pattern in 'BlueprintContainerInventory::TryRead', 'get_ContentsCleared', 'BlueprintStorageCapture::PrefabGrid') {
    Assert-Contract ($emptyContainerCalls -match $pattern) "Empty-container construction lacks gate: $pattern"
}
Assert-Contract ($emptyContainerCalls -notmatch 'GetDropListItems|(?<![A-Za-z])Inventory::|ZDO::|RPC|Object::Instantiate|Container::Load') 'Empty-container eligibility executes loot, inventory or world operations.'
$prefabGrid = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'BlueprintStorageCapture' -and $_.Name -eq 'PrefabGrid' }
$prefabGridCalls = ($prefabGrid.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
foreach ($pattern in 'GetComponent<Piece>', 'GetComponent<TombStone>', 'GetComponent<Corpse>', 'GetComponent<Incinerator>',
    'GetComponent<ItemStand>', 'GetComponent<ArmorStand>', 'GetComponentsInChildren<Container>', 'get_gameObject',
    'GetType', 'Container::m_autoDestroyEmpty', 'Container::m_wagon', 'Container::m_defaultItems', 'DropTable::m_drops',
    'get_Count', 'Container::m_width', 'Container::m_height') {
    Assert-Contract ($prefabGridCalls -match $pattern) "Prefab container grid lacks eligibility gate: $pattern"
}
Assert-Contract ($prefabGridCalls -notmatch 'GetDropListItems|(?<![A-Za-z])Inventory::|ZDO::|RPC|Object::Instantiate|Container::Load|Container::Save|stfld') 'Prefab grid executes state or changes the prefab.'
foreach ($field in 'm_width', 'm_height') {
    $nativeGridField = $containerType.Fields | Where-Object Name -eq $field
    Assert-Contract ($nativeGridField.IsPublic -and $nativeGridField.FieldType.FullName -eq 'System.Int32') "Native Container grid changed: $field"
}
foreach ($name in 'SelectSupplies','TryApply','ContinueAutomaticPlacement','OnNativePlacementFinished') {
    $method = $pluginMethods | Where-Object { $_.Name -eq $name -and $_.DeclaringType.Name -in @('BlueprintItemStandPlacement','PrecisionPlacementSession') }
    $calls = ($method.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($calls -match 'BlueprintItemStandPlacement::NeedsSupply') "Empty-container supply policy is bypassed by $name"
}
# The read-only v109 decoder relies on exact native field/flag order and custom-map count encoding.
# A host update must fail this gate until the new serializer is reviewed, not silently misread bytes.
$packageType = $hostAssembly.MainModule.Types | Where-Object Name -eq 'ZPackage'
$numItemsSave = $packageType.Methods | Where-Object Name -eq 'WriteNumItems'
$inventoryLoadLayout = $inventoryType.Methods | Where-Object { $_.Name -eq 'Load' -and $_.Parameters.Count -eq 1 }
$hashType = @($hostAssembly.MainModule.Types) + @($utilsAssembly.MainModule.Types) | Where-Object Name -eq 'StringExtensionMethods'
$prefabHashLayout = $hashType.Methods | Where-Object Name -eq 'GetStableHashCode'
$standItemSaveLayout = $itemDropType.Methods | Where-Object Name -eq 'SaveToZDO'
$standItemLoadLayout = $itemDataType.Methods | Where-Object { $_.Name -eq 'Load' -and $_.Parameters.Count -eq 3 }
$standAttachLayout = $standType.Methods | Where-Object Name -eq 'UpdateAttach'
foreach ($entry in @(@($itemSave, 'DAA9052430B2B766A8F15E221AB0D24C63FD55A6723EEBDCC65405E48F8D5494'),
        @(($standType.Methods | Where-Object Name -eq 'UseItem'), '3EF78FC042D7E631C130758E846162014078DC92A495001E984AC8017DF4BC6A'),
        @(($standType.Methods | Where-Object Name -eq 'Interact'), 'CFC7C1C57E2591E62CC01FE461664BB8E2DC9AAD9B5F822990279EF25D8BFD52'),
        @(($standType.Methods | Where-Object Name -eq 'DestroyAttachment'), '61403E7D10E8BA39FFAC1A5934BA9D6E33B97245D908CC17F3BB9B3C8372991B'),
        @(($standType.Methods | Where-Object Name -eq 'RPC_DestroyAttachment'), '6F7557FD696A8DEF565913F3967AB29152274BD0E563A31BFD1F6817D9930645'),
        @($standItemSaveLayout, '2AA920721B18F2C92DF93A183B8B4A2CF822516700DF8384208D31365DFADFB7'),
        @($nativeItemLoad, '63E24A305F38DB26E0D0AEEA4922D5421799DA1AB162641999F1541BF85C9475'),
        @($standItemLoadLayout, '4115D3B4262E05B05634A1117801E96E4A9162CAA176B0CB7D1A227DC46EA8FA'),
        @($standAttachLayout, '0E107474A83CB6A695BCC5FEBD2792760CB85264CFFA0F2172EA9F1A62CCCAE2'),
        @($numItemsSave, 'EFCB67745DEF1986C47B3526000AC07C0114FDA19AF57F0DABCDE653A6DC456E'),
        @($inventoryLoadLayout, 'E0E6FF90F203C6FFE961DE66768CDEDF609B896945E637A8166B121911BEF935'),
        @(($inventoryType.Methods | Where-Object Name -eq 'LoadOld'), '1A33375F234E0650DB11140173303220E957FB25A68275EB82E2848118482EE7'),
        @(($packageType.Methods | Where-Object Name -eq 'ReadBool'), '99C0897070FC758C95897695FD521DE5D606B2B4C645410D01BD9D4FEADB9DFA'),
        @(($packageType.Methods | Where-Object Name -eq 'ReadSingle'), 'A26CCB88BA6A99268C3C34E1BCB10546722A400C5B1E8F767C969C508C0A3499'),
        @(($packageType.Methods | Where-Object Name -eq 'ReadString'), '60396F6EDEACF4733F0A5286A20EFBB85B472DD64AD3B47F1319D902967D6C0E'),
        @(($packageType.Methods | Where-Object Name -eq 'ReadVector2i'), '643B0E2ED41A36A548F986294B27BFCEDF726BF1FFAECCD30FF8EC6E4A1FD184'),
        @($prefabHashLayout, '8D6089461A9226DC1BDF23B87784C88F378A57F95C1FF6FFB6CCB85E731BF821'),
        @(($player.Methods | Where-Object Name -eq 'ConsumeResources'), 'C3BE324204B7736C506C3C3774B7E8E2A118D6B6688FEBBBB3BF676F38689646'),
        @(($inventoryType.Methods | Where-Object { $_.Name -eq 'RemoveItem' -and $_.Parameters.Count -eq 4 }), '08D969317BA4AD9B43DFDF811968D8E6413DDB990F174B98F9A957E0F213C312'),
        @((($pieceType.NestedTypes | Where-Object Name -eq 'Requirement').Methods | Where-Object Name -eq 'GetAmount'), 'F1F1B591B27BE1A0B2A998C0E56AD8FF90B97FFA25C51482E6EE1DBEA0B0BE65'),
        @(($pieceType.Methods | Where-Object Name -eq 'FreeBuildKey'), 'BD493FCB49A05CE3B5441DCD6EDD62D63ED1C395D7D99A54C3373E77D1ED62E7'),
        @($moveToCell, 'A7F221EBB6B801A2EE8EE8607A952A3DC9624BFD3F702BC98C778E7DB01134B6'),
        @($addToCell, '788DD46A71168FE42327FE69F22A32705EDCA5E53C992AAF45D49CD26A491A8D'),
        @($inventoryChanged, '3418372BCABB4C4CB300ACBD8A36CB704AC0F5A7A3F2FC1709798C4050A8DE0E'),
        @($containerChanged, '199FB6EE05F8AD394E5E9D1170F8B18D6EFF0442F2C92DD17E48FE06AE59B440'),
        @($nativeContainerSave, '50BECD898BAE9E6988DB0D86792B5857C524281AB019E2C78D8DBCB6848D903B'),
        @($itemCloneLayout, 'AB7409FF6E820D982A70170C29CFA9321141BC5FDAD23BFBB4912E300462ED71'))) {
    Assert-Contract ((Get-NativeLayoutHash $entry[0]) -eq $entry[1]) "Native inventory/holder layout changed: $($entry[0].FullName)"
}
foreach ($entry in @(@($emptyCellClone[0], $addToCell),
        @(($containerChanged.Body.Instructions | Where-Object { [string]$_.Operand -match 'Container::IsOwner\(' }), $containerChanged))) {
    $instruction = $entry[0]; $method = $entry[1]
    $originalOperand = $instruction.Operand
    $originalHash = Get-NativeLayoutHash $method
    try {
        $instruction.Operand = $inventorySave
        Assert-Contract ((Get-NativeLayoutHash $method) -cne $originalHash) 'Broken native copy/owner negative control was accepted.'
    }
    finally { $instruction.Operand = $originalOperand }
    Assert-Contract ((Get-NativeLayoutHash $method) -ceq $originalHash) 'Native transfer negative control was not restored in memory.'
}
Write-Output 'PASS native storage transfer contract: exact signatures/copy/debit/local-player callback/owner-save chain and two in-memory negative controls; no transfers, native game execution or network/disk acknowledgement tested.'
foreach ($primitive in 'System.Byte', 'System.Int32', 'System.UInt16', 'System.Int64', 'System.String') {
    $writer = $packageType.Methods | Where-Object { $_.Name -eq 'Write' -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq $primitive }
    $writerCalls = @($writer.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call', 'callvirt') } | ForEach-Object { [string]$_.Operand })
    Assert-Contract ($writerCalls.Count -eq 1 -and $writerCalls[0] -eq "System.Void System.IO.BinaryWriter::Write($primitive)") "Native ZPackage byte codec changed: $primitive"
}
$worldCapture = $pluginMethods | Where-Object { $_.DeclaringType.Name -eq 'PrecisionPlacementSession' -and $_.Name -eq 'TrySaveWorldSelection' }
$worldCaptureCalls = ($worldCapture.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($worldCaptureCalls -match 'BlueprintStorageCapture::TryCapture' -and $worldCaptureCalls -match 'CompositeBlueprintStore/ForeignPartData::FromGeometry') 'World selection does not preserve container snapshots.'

$nativePlayer = $hostAssembly.MainModule.Types | Where-Object Name -eq 'Player'
$nativeSnapOuter = @($nativePlayer.Methods | Where-Object Name -eq 'FindClosestSnapPoints')
$nativeSnapInner = @($nativePlayer.Methods | Where-Object Name -eq 'FindClosestSnappoint')
Assert-Contract ($nativeSnapOuter.Count -eq 1 -and $nativeSnapInner.Count -eq 1 -and
    -not $nativeSnapOuter[0].IsStatic -and -not $nativeSnapInner[0].IsStatic -and
    $nativeSnapOuter[0].Parameters.Count -eq 5 -and $nativeSnapInner[0].Parameters.Count -eq 5 -and
    $nativeSnapOuter[0].Parameters[0].ParameterType.FullName -eq 'UnityEngine.Transform' -and
    $nativeSnapInner[0].Parameters[0].ParameterType.FullName -eq 'UnityEngine.Vector3' -and
    $nativeSnapInner[0].Parameters[3].ParameterType.FullName -eq 'UnityEngine.Transform&' -and
    $nativeSnapInner[0].Parameters[4].ParameterType.FullName -eq 'System.Single&') 'Native snap selector signatures changed.'
$innerCalls = @($nativeSnapOuter[0].Body.Instructions | Where-Object { [string]$_.Operand -match 'Player::FindClosestSnappoint\(' })
Assert-Contract ($innerCalls.Count -eq 2 -and @($nativePlayer.Fields | Where-Object Name -eq 'm_tempSnapPoints2').Count -eq 1 -and
    @($nativePlayer.Fields | Where-Object Name -eq 'm_manualSnapPoint').Count -eq 1) 'Native two-call/manual/target-list snap contract changed.'
$snapSelector = $pluginTypes | Where-Object Name -eq 'BlueprintHammerSnapSearch'
Assert-Contract ($null -ne $snapSelector) 'Compiled Hammer snap adapter missing.'
$selectorCalls = @($snapSelector.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($selectorCalls -match 'BlueprintNearestPartIndex::FindNearestByDistance' -and
    $selectorCalls -match 'BlueprintNearestPartIndex::.ctor' -and $selectorCalls -match 'Vector3::Distance' -and
    $selectorCalls -match 'NativeSearch::Invoke' -and $selectorCalls -match 'Harmony::GetPatchInfo' -and
    $selectorCalls -match 'get_Owners' -and @($snapSelector.Methods | Where-Object Name -eq 'Finalizer').Count -eq 1 -and
    @($snapSelector.Fields | Where-Object { $_.Name -eq 'current' -and
        @($_.CustomAttributes | Where-Object { $_.AttributeType.FullName -eq 'System.ThreadStaticAttribute' }).Count -eq 1 }).Count -eq 1) `
    'Hammer snap index/fallback/foreign-patch/scoped cleanup contract missing.'
$nativeUnityCore = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ValheimManagedDir 'UnityEngine.CoreModule.dll'))
$nativeVector = $nativeUnityCore.MainModule.Types | Where-Object FullName -eq 'UnityEngine.Vector3'
$nativeDistance = $nativeVector.Methods | Where-Object Name -eq 'Distance'
$nativeDistanceCalls = @($nativeDistance.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
Assert-Contract ($nativeDistanceCalls -match 'System.Math::Sqrt' -and
    @($nativeDistance.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'sub' }).Count -eq 3 -and
    @($nativeDistance.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'mul' }).Count -eq 3 -and
    @($nativeDistance.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'add' }).Count -eq 2 -and
    @($nativeDistance.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'conv.r4' }).Count -ge 1) `
    'Native Vector3.Distance float/sqrt metric changed.'

$nativeInputSystem = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $ValheimManagedDir 'Unity.InputSystem.dll'))
try {
    $nativeMouse = $nativeInputSystem.MainModule.GetType('UnityEngine.InputSystem.Mouse')
    $nativeDevice = $nativeInputSystem.MainModule.GetType('UnityEngine.InputSystem.InputDevice')
    $warpCommand = $nativeInputSystem.MainModule.GetType('UnityEngine.InputSystem.LowLevel.WarpMousePositionCommand')
    $warp = $nativeMouse.Methods | Where-Object Name -eq 'WarpCursorPosition'
    $warpCalls = @($warp.Body.Instructions | ForEach-Object { [string]$_.Operand }) -join "`n"
    Assert-Contract ($warp.IsPublic -and $warpCalls -match 'WarpMousePositionCommand::Create' -and
        $warpCalls -match 'InputDevice::ExecuteCommand' -and
        @($nativeMouse.Properties | Where-Object { $_.Name -eq 'current' -and $_.GetMethod.IsPublic -and $_.GetMethod.IsStatic }).Count -eq 1 -and
        @($nativeDevice.Properties | Where-Object { $_.Name -in @('native', 'enabled', 'added') -and $_.GetMethod.IsPublic }).Count -eq 3 -and
        @($nativeDevice.Methods | Where-Object { $_.Name -eq 'ExecuteCommand' -and $_.IsPublic -and
            $_.HasGenericParameters -and $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.IsByReference -and
            $_.ReturnType.FullName -eq 'System.Int64' }).Count -eq 1 -and
        @($warpCommand.Methods | Where-Object { $_.Name -eq 'Create' -and $_.IsPublic -and $_.IsStatic -and
            $_.Parameters.Count -eq 1 -and $_.Parameters[0].ParameterType.FullName -eq 'UnityEngine.Vector2' }).Count -eq 1) `
        'Optional cursor warp reflection contract changed; this check does not prove runtime backend/device availability.'
}
finally { $nativeInputSystem.Dispose() }

$steamBuild = 'unavailable'
$steamManifest = Join-Path ([IO.Path]::GetFullPath((Join-Path $ValheimManagedDir '../../../..'))) 'appmanifest_892970.acf'
if (Test-Path -LiteralPath $steamManifest -PathType Leaf) {
    $manifest = Get-Item -LiteralPath $steamManifest
    Assert-Contract ($manifest.Length -le 1MB) 'Steam manifest exceeds the metadata read limit.'
    $buildIds = [regex]::Matches([IO.File]::ReadAllText($steamManifest), '"buildid"\s+"([0-9]+)"')
    Assert-Contract ($buildIds.Count -eq 1) 'Steam manifest does not provide one buildid.'
    $steamBuild = $buildIds[0].Groups[1].Value
}
$hostSha = (Get-FileHash -LiteralPath $hostPath -Algorithm SHA256).Hash
Write-Output "PASS: current Valheim host contract matches BuildWorks $($plugin.Name.Version); Steam manifest build $steamBuild; assembly_valheim SHA256 $hostSha."
