using System;
using System.Collections.Generic;
using System.Globalization;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    /// <summary>
    /// Coordinates editor input and transactions between the deterministic
    /// document, Unity scene projection, UI view, and persistent store.
    /// </summary>
    internal sealed class BlueprintEditorController : IDisposable
    {
        private enum EditorState
        {
            Closed,
            Opening,
            Editing,
            Saving,
            Closing
        }

        private readonly CompositeBlueprintStore store;
        private readonly Camera cameraTemplate;
        private readonly TMP_Text textTemplate;
        private readonly Func<string, GameObject> resolveVisualSource;
        private readonly Func<string, string> resolveDisplayName;
        private readonly Func<IReadOnlyList<BlueprintEditorCatalogItem>> catalogItems;
        private readonly Func<int> initialPlacementSnapPoint;
        private readonly Action<string> logError;
        private readonly Action<CompositeBlueprintStore.Blueprint> saved;
        private readonly List<Canvas> hiddenGameCanvases = new List<Canvas>();
        private BlueprintEditorDocument document;
        private BlueprintEditorScene scene;
        private BlueprintEditorView view;
        private readonly IBlueprintEditorInput input;
        private CompositeBlueprintStore.Blueprint sourceBlueprint;
        private EditorState state;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool restoreCursor;
        private bool saveClosesOnSuccess;
        private Vector3 cameraFocus;
        private float cameraDistance = 8f;
        private float cameraYaw = 45f;
        private float cameraPitch = 25f;
        private Vector2 previousMouse;
        private Vector2 selectionStart;
        private readonly List<string> dragIds = new List<string>();
        private readonly List<string> gizmoIds = new List<string>();
        private readonly List<string> contourSupportIds = new List<string>();
        private readonly List<Vector3> contourGuidePoints = new List<Vector3>();
        private BlueprintEditorTool activeTool;
        private int arrayCountX = 2;
        private int arrayCountY = 1;
        private Vector3 arrayStepX = Vector3.right;
        private Vector3 arrayStepY = Vector3.forward;
        private float arrayRotation;
        private float arrayScaleStepX;
        private float arrayRise;
        private float arrayPitch;
        private float arrayRoll;
        private bool arraySymmetric;
        private RepeatDistributionMode arrayDistribution;
        private int arraySpacingIndex;
        private int arrayExactIndex;
        private bool arrayLastSecond;
        private bool dragStartArrayLastSecond;
        private float arrayDistanceX;
        private float arrayDistanceY;
        private float dragStartArrayDistanceX;
        private float dragStartArrayDistanceY;
        private static readonly float[] ArrayGaps = { 0f, .01f, .05f, .10f, .50f };
        private static readonly float[] ArrayExactSteps = { .25f, .50f, 1f, 2f, 4f };
        private int arrayPreviewCount;
        private GizmoAxis arrayPrimaryAxis;
        private GizmoAxis dragStartArrayPrimaryAxis;
        private int dragStartArrayCountX;
        private int dragStartArrayCountY;
        private Vector3 dragStartArrayStepX;
        private Vector3 dragStartArrayStepY;
        private bool dragArraySecond;
        private float dragArrayStepLength;
        private bool contourClosed;
        private int contourPreviewCount;
        private GizmoHandleKind dragHandle;
        private GizmoFamily gizmoFamily = GizmoFamily.Combined;
        private string keyboardNumber = "";
        private Vector3 keyboardStartViewAxis;
        private bool keyboardPreview;
        private bool keyboardPaused;
        private bool keyboardValid;
        private bool keyboardSurface;
        private bool keyboardConfirmRequested;
        private bool keyboardMoved;
        private Vector3 keyboardBaseTranslation;
        private Quaternion keyboardBaseRotation;
        private float keyboardBaseScale;
        private Quaternion keyboardOrientation;
        private Vector3 keyboardPlaneFirst;
        private Vector3 keyboardPlaneSecond;
        private int keyboardSourceIndex;
        private int preferredCursorSource = -1;
        private readonly List<string> preferredCursorIds = new List<string>();
        private int keyboardAutoSource = -1;
        private int keyboardHoveredSource = -1;
        private int dragNativeAnchorEnd;
        private bool keyboardHelpersEnabled;
        private bool keyboardSourceChanged;
        private readonly List<int> keyboardSourceChoices = new List<int>();
        private readonly List<int> keyboardSnapSources = new List<int>();
        private Vector3? keyboardSavedSelectionPoint;
        private Vector3? keyboardSavedPin;
        private GizmoAxis keyboardSavedConstraint;
        private bool keyboardSavedLocalSpace;
        private GizmoAxis dragAxis;
        private Vector2 dragStartMouse;
        private Vector2 dragScreenDirection;
        private Vector3 dragPivot;
        private Vector3 dragAxisWorld;
        private Vector3 dragTranslation;
        private Quaternion dragRotation = Quaternion.identity;
        private float dragScale = 1f;
        private int pinnedAnchorPoint = -1;
        private Vector3? pinnedAnchorWorld;
        private int selectedAnchorPoint = -1;
        private Vector3? selectedAnchorWorld;
        private Vector3[] dragSourceAnchors = Array.Empty<Vector3>();
        private GizmoAxis anchorConstraintAxis;
        private bool dragMagneticMove;
        private bool dragDuplicate;
        private Vector3 dragMovingVector;
        private Vector3 dragPlaneStart;
        private bool dragConstraintActive;
        private Vector3 dragConstraintCenter;
        private float dragConstraintRadius;
        private BlueprintEditorCatalogItem placementItem;
        private BlueprintEditorDocument placementBlueprint;
        private Vector3 placementPosition;
        private float placementYaw;
        private bool placementSnap = true;
        private int placementManualSnapPoint = -1;
        private bool customPivot;
        private BlueprintEditorPivotMode pivotMode = BlueprintEditorPivotMode.SelectionCenter;
        private int boundsPivotIndex = 4;
        private bool localSpace = true;
        private float translationStep = 0.05f;
        private float rotationStep = 1f;
        private float contourScaleStep;
        private float dragWorldUnitsPerPixel;
        private float dragStartAngle;
        private Vector3 dragStartDirection;
        private bool dragUsesRotationPlane;
        private Vector3[] gizmoAnchors = Array.Empty<Vector3>();
        private int gizmoNativeAnchorStart = AnchorAdjustment.SelectableAnchorCount;
        private int gizmoNativeAnchorEnd;
        private int dragAnchorPoint = -1;
        private Vector3 dragAnchorStartWorld;
        private Plane dragAnchorPlane;
        private bool showAllAnchors = true;
        private bool meshSnapEnabled;
        private bool snapTargetVisible;
        private Vector3 snapTargetWorld;
        private bool snapTargetIsNative;
        private readonly List<Vector3> snapPreviewTargets = new List<Vector3>();
        private readonly List<bool> snapPreviewNative = new List<bool>();
        private bool cameraDragging;
        private Vector2 middleCameraStart;
        private bool middleCameraMoved;
        private string middleDeleteTarget;
        private bool rightCameraTracking;
        private bool cameraFlying;
        private bool orthographic;
        private Vector2 rightCameraStart;
        private bool selectionPending;
        private int blockWorldInputThroughFrame = -1;
        private bool disposed;

        internal BlueprintEditorController(
            CompositeBlueprintStore blueprintStore,
            Camera editorCameraTemplate,
            TMP_Text editorTextTemplate,
            Func<string, GameObject> visualSourceResolver,
            Func<string, string> displayNameResolver,
            Func<IReadOnlyList<BlueprintEditorCatalogItem>> catalogItemProvider,
            Action<string> errorLogger,
            Action<CompositeBlueprintStore.Blueprint> onSaved = null,
            IBlueprintEditorInput editorInput = null,
            Func<int> initialPlacementSnapPointProvider = null)
        {
            store = blueprintStore ?? throw new ArgumentNullException(nameof(blueprintStore));
            cameraTemplate = editorCameraTemplate
                ? editorCameraTemplate
                : throw new ArgumentNullException(nameof(editorCameraTemplate));
            textTemplate = editorTextTemplate;
            resolveVisualSource = visualSourceResolver ??
                throw new ArgumentNullException(nameof(visualSourceResolver));
            resolveDisplayName = displayNameResolver ?? (name => name);
            catalogItems = catalogItemProvider ??
                (() => Array.Empty<BlueprintEditorCatalogItem>());
            initialPlacementSnapPoint = initialPlacementSnapPointProvider ?? (() => -1);
            logError = errorLogger ?? (_ => { });
            saved = onSaved;
            input = editorInput ?? UnityBlueprintEditorInput.Instance;
        }

        internal event Action CatalogRequested;
        internal event Action<float> InterfaceScaleChanged;

        internal bool IsOpen => state == EditorState.Editing || state == EditorState.Saving;
        internal bool BlocksWorldInput => IsOpen || Time.frameCount <= blockWorldInputThroughFrame;
        internal BlueprintEditorDocument Document => document;

        internal bool OpenNew(out string error) => Open(
            new BlueprintEditorDocument(
                null,
                BuildWorksLocalization.Text("editor.new_blueprint"),
                CompositeBlueprintStore.DefaultCategory),
            null,
            out error);

        internal bool OpenExisting(
            CompositeBlueprintStore.Blueprint source,
            out string error)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            try
            {
                return Open(DocumentFromBlueprint(source), source, out error);
            }
            catch (Exception exception)
            {
                error = BuildWorksLocalization.Text(
                    "editor.open_data_failed", exception.Message);
                logError("BuildWorks blueprint conversion failed: " + exception);
                return false;
            }
        }

        internal bool AddPart(string prefabName, string displayName = null)
        {
            if (!IsEditing || string.IsNullOrWhiteSpace(prefabName)) return false;
            return ApplyDocumentEdit(() => document.AddPart(new BlueprintEditorPart(
                Guid.NewGuid().ToString("N"),
                prefabName,
                string.IsNullOrWhiteSpace(displayName) ? DisplayName(prefabName) : displayName,
                ToPoint(cameraFocus),
                ToRotation(Quaternion.identity))));
        }

        internal bool ApplyTransformDelta(
            Vector3 translation,
            Quaternion rotation,
            Vector3 pivot,
            float uniformScale = 1f)
        {
            if (!IsEditing) return false;
            bool changed = ApplyDocumentEdit(() => document.ApplyTransformDelta(
                ToPoint(translation),
                ToRotation(rotation),
                ToPoint(pivot),
                uniformScale), preserveModifier: true);
            if (changed)
            {
                if (pinnedAnchorWorld.HasValue)
                    pinnedAnchorWorld = pivot + rotation * ((pinnedAnchorWorld.Value - pivot) * uniformScale) + translation;
                if (selectedAnchorWorld.HasValue)
                    selectedAnchorWorld = pivot + rotation * ((selectedAnchorWorld.Value - pivot) * uniformScale) + translation;
                if (activeTool == BlueprintEditorTool.Array && localSpace)
                {
                    arrayStepX = rotation * arrayStepX;
                    arrayStepY = rotation * arrayStepY;
                }
                RefreshModifierPreview();
                UpdateGizmo();
            }
            return changed;
        }

        internal void SetUiScale(float value)
        {
            if (view != null) view.SetUiScale(value);
        }

        internal void Update()
        {
            if (!IsEditing) return;
            keyboardConfirmRequested = false;
            HideGameCanvases(view.RootCanvas);
            scene.EnsureWorldCamerasExcludeEditorLayer();
            view.Tick();
            view.SetSelectionBox(selectionStart, input.MousePosition, false);
            RefreshContextHints();
            Rect viewport = view.ViewportScreenRect();
            scene.SetViewport(viewport);
            scene.SetTemporarySelectionHighlight(ShiftHeld && !IsGizmoDragging, document);
            if (!input.HasFocus)
            {
                if (IsGizmoDragging) CancelGizmoDrag();
                cameraDragging = middleCameraMoved = rightCameraTracking = cameraFlying = selectionPending = false;
                middleDeleteTarget = null;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                ApplyCamera();
                return;
            }
            if (cameraDragging && (view.HasModal || view.HasCatalog || view.HasOutlinerMenu ||
                view.HasViewportSettings || view.HasToolsMenu || view.IsTextInputFocused || view.IsOutlinerDragging))
            { cameraDragging = false; middleDeleteTarget = null; }
            if (cameraDragging && (input.GetMouseButton(0) || input.GetMouseButton(1) || ShiftHeld ||
                input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl) ||
                input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt)))
                middleDeleteTarget = null;
            if (view.HasModal)
            {
                if (input.GetKeyDown(KeyCode.Escape)) view.HideDialog();
                scene.HideGizmo();
                ApplyCamera();
                return;
            }
            if (view.HasCatalog)
            {
                if (input.GetKeyDown(KeyCode.Escape)) view.HideCatalog();
                scene.HideGizmo();
                ApplyCamera();
                return;
            }
            if (view.HasOutlinerMenu)
            {
                if (input.GetKeyDown(KeyCode.Escape)) view.HideOutlinerMenu();
                ApplyCamera();
                return;
            }
            if (view.HasViewportSettings)
            {
                if (keyboardPreview) keyboardPaused = true;
                if (input.GetKeyDown(KeyCode.Escape)) view.HideViewportSettings();
                else if (input.GetMouseButtonDown(0) && !view.IsViewportSettingsHit(input.MousePosition))
                    view.HideViewportSettings();
                ApplyCamera();
                return;
            }
            if (view.HasToolsMenu)
            {
                view.HandleToolsMenuInput();
                ApplyCamera();
                return;
            }
            if (view.IsOutlinerDragging)
            {
                if (input.GetKeyDown(KeyCode.Escape)) view.CancelOutlinerDrag();
                ApplyCamera();
                return;
            }
            if (view.IsTextInputFocused)
            {
                if (keyboardPreview) keyboardPaused = true;
                if (input.GetKeyDown(KeyCode.Escape)) view.CancelTextEdit();
                ApplyCamera();
                return;
            }

            if (HandleShortcuts() || !IsEditing) return;
            ApplyCamera();
            HandleViewport(viewport);
            view.SetSelectionBox(selectionStart, input.MousePosition,
                selectionPending && input.GetMouseButton(0));
            ApplyCamera();
            UpdateGizmo();
        }

        internal void LateUpdate()
        {
            if (IsEditing)
            {
                HideGameCanvases(view.RootCanvas);
                ApplyCamera();
                scene.UpdateOccluders();
                if (view.HasCatalog || view.HasModal) scene.HideGizmo();
                else UpdateGizmo();
            }
        }

        internal void Close(bool discardChanges)
        {
            if (!IsEditing) return;
            if (keyboardPreview) { CancelGizmoDrag(); return; }
            if (!discardChanges && document.IsDirty)
            {
                view.ShowUnsavedDialog(document.Name);
                return;
            }
            Cleanup();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Cleanup();
        }

        private bool Open(
            BlueprintEditorDocument nextDocument,
            CompositeBlueprintStore.Blueprint source,
            out string error)
        {
            error = null;
            if (disposed)
            {
                error = BuildWorksLocalization.Text("editor.already_disposed");
                return false;
            }
            if (state != EditorState.Closed)
            {
                error = BuildWorksLocalization.Text("editor.already_open");
                return false;
            }

            state = EditorState.Opening;
            try
            {
                document = nextDocument ?? throw new ArgumentNullException(nameof(nextDocument));
                sourceBlueprint = source;
                scene = new BlueprintEditorScene(cameraTemplate, ResolveVisualSource);
                if (!scene.TrySync(document, out string warning))
                    throw new InvalidOperationException(warning);
                view = new BlueprintEditorView(textTemplate, input);
                view.SetProjection(orthographic);
                HideGameCanvases(view.RootCanvas);
                SubscribeView();
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                restoreCursor = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                state = EditorState.Editing;
                FrameAll();
                Bind(warning);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                logError("BuildWorks blueprint editor open failed: " + exception);
                Cleanup();
                return false;
            }
        }

        private bool IsEditing => state == EditorState.Editing;

        private void SubscribeView()
        {
            view.CloseRequested += () => Close(discardChanges: false);
            view.SaveRequested += () => TrySave(closeAfterSave: false);
            view.UndoRequested += () => ApplyHistory(document.Undo, document.Redo);
            view.RedoRequested += () => ApplyHistory(document.Redo, document.Undo);
            view.GroupRequested += () => ApplyDocumentEdit(() => document.CreateGroup(
                Guid.NewGuid().ToString("N"), BuildWorksLocalization.Text(
                    "editor.group_name", document.Groups.Count + 1)));
            view.CreateGroupRequested += () => ApplyDocumentEdit(() => document.CreateEmptyGroup(
                Guid.NewGuid().ToString("N"), BuildWorksLocalization.Text(
                    "editor.group_name", document.Groups.Count + 1)));
            view.ShowAllRequested += () => ApplyDocumentEdit(document.ShowAll);
            view.HideSelectionRequested += () => ApplyDocumentEdit(() =>
                document.SetSelectionVisibility(false));
            view.LockSelectionRequested += () => ApplyDocumentEdit(() =>
                document.SetSelectionLocked(true));
            view.UngroupRequested += () => ApplyDocumentEdit(document.UngroupSelection);
            view.MoveSelectionToGroupRequested += groupId => ApplyDocumentEdit(() =>
                document.SetSelectionGroup(groupId));
            view.DuplicateSelectionRequested += () => ApplyDocumentEdit(() =>
                document.DuplicateSelection(new Point3(0.5, 0.0, 0.5),
                    BuildWorksLocalization.Text("editor.generated.copy")));
            view.DeleteSelectionRequested += () => ApplyDocumentEdit(() =>
                document.DeleteSelection(false));
            view.PrimaryPartRequested += stableId => ApplyDocumentEdit(() =>
                document.SetPrimaryPart(stableId));
            view.PrimaryGroupRequested += stableId => ApplyDocumentEdit(() =>
                document.SetPrimaryGroup(stableId));
            view.GroupPivotRequested += (groupId, partId) => ApplyDocumentEdit(() =>
                document.SetGroupPivot(groupId, partId));
            view.UiScaleChanged += value => InterfaceScaleChanged?.Invoke(value);
            view.SnapChanged += SetSnap;
            view.SpaceRequested += ToggleSpace;
            view.FrameSelectionRequested += FrameSelectionOrAll;
            view.AnchorVisibilityRequested += ToggleAnchorVisibility;
            view.MeshSnapRequested += ToggleMeshSnap;
            view.PinSelectedAnchorRequested += ToggleSelectedAnchorPin;
            view.AnchorConstraintRequested += SetAnchorConstraint;
            view.PivotRequested += TogglePivotMode;
            view.PivotModeRequested += SetPivotMode;
            view.BoundsPivotRequested += SetBoundsPivot;
            view.ResetTransformRequested += ResetSelectionTransform;
            view.FrameAllRequested += FrameAll;
            view.ToolSelected += tool =>
            {
                if (placementItem != null) CancelPartPlacement();
                SetActiveTool(tool);
            };
            view.GizmoFamilySelected += family =>
            {
                if (IsGizmoDragging) return;
                gizmoFamily = family;
                view.SetGizmoFamily(family);
                UpdateGizmo();
            };
            view.PreviewAxisRequested += axis => { if (keyboardPreview) SetKeyboardAxis(axis); };
            view.CursorSourceRequested += ChooseCursorSource;
            view.CursorSourceHovered += index => { keyboardHoveredSource = index; UpdateGizmo(); };
            view.CursorHelpersRequested += () =>
            {
                keyboardHelpersEnabled = !keyboardHelpersEnabled;
                if (keyboardPreview && keyboardSurface) { RefreshCursorSources(); ChooseCursorSource(keyboardSourceIndex); }
            };
            view.SetGizmoFamily(gizmoFamily);
            view.LightingChanged += scene.SetLighting;
            view.ViewportSettingsChanged += (move, rotation, array, points, scale, grid) =>
                scene.SetViewportSettings(move, rotation, array, points, scale, grid);
            view.ProjectionChanged += value => { orthographic = value; ApplyCamera(); };
            view.FieldOfViewChanged += degrees => { scene.SetFieldOfView(degrees); ApplyCamera(); };
            view.FieldOfViewResetRequested += () =>
            {
                scene.SetFieldOfView(cameraTemplate.fieldOfView);
                view.SetFieldOfView(scene.Camera.fieldOfView, orthographic);
                ApplyCamera();
            };
            view.SetFieldOfView(scene.Camera.fieldOfView, orthographic);
            view.OccluderFadeRequested += ToggleOccluderFade;
            view.NodeClicked += SelectNode;
            view.VisibilityChanged += (id, visible) => ApplyDocumentEdit(() =>
                document.SetVisibility(id, visible));
            view.LockChanged += (id, locked) => ApplyDocumentEdit(() =>
                document.SetLocked(id, locked));
            view.DialogDecided += HandleDialog;
            view.CatalogPartSelected += BeginPartPlacement;
            view.InspectorSubmitted += ApplyInspector;
            view.RenameSubmitted += (id, name) => ApplyDocumentEdit(() =>
                document.Rename(id, name));
            view.BlueprintMetadataSubmitted += (name, category) => ApplyDocumentEdit(() =>
                document.SetMetadata(name, category));
            view.SelectionVisibilityRequested += visible => ApplyDocumentEdit(() =>
                document.SetSelectionVisibility(visible));
            view.SelectionLockRequested += locked => ApplyDocumentEdit(() =>
                document.SetSelectionLocked(locked));
            view.ArrayChanged += ChangeArrayParameters;
            view.ArrayDistributionRequested += () =>
            {
                arrayDistribution = (RepeatDistributionMode)(((int)arrayDistribution + 1) % 3);
                RecalculateArraySteps();
                PreviewArray();
            };
            view.ArrayStepRequested += () =>
            {
                if (arrayDistribution == RepeatDistributionMode.Fit) return;
                if (arrayDistribution == RepeatDistributionMode.Pack)
                    arraySpacingIndex = (arraySpacingIndex + 1) % ArrayGaps.Length;
                else arrayExactIndex = (arrayExactIndex + 1) % ArrayExactSteps.Length;
                RecalculateArraySteps();
                PreviewArray();
            };
            view.ArrayMoveStepRequested += () =>
            {
                translationStep = NextPreset(translationStep, PrecisionStepPresets.Translation);
                UpdateTransformContext();
                PreviewArray();
            };
            view.ArrayAngleStepRequested += () =>
            {
                rotationStep = NextPreset(rotationStep, PrecisionStepPresets.Rotation);
                UpdateTransformContext();
                PreviewArray();
            };
            view.ArraySymmetryRequested += () => { arraySymmetric = !arraySymmetric; PreviewArray(); };
            view.ApplyArrayRequested += ApplyArray;
            view.CancelArrayRequested += CancelArray;
            view.ApplyContourRequested += ApplyContour;
            view.CancelContourRequested += CancelContour;
            view.ContourChanged += PreviewContour;
        }

        private bool HandleShortcuts()
        {
            bool control = input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl);
            bool shift = ShiftHeld;
            bool alt = input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt);
            if (control && !alt && !shift && !cameraDragging && !rightCameraTracking &&
                !input.GetMouseButton(1) && !input.GetMouseButton(2))
            {
                for (int index = 0; index < 4; ++index)
                    if (input.GetKeyDown(KeyCode.Alpha1 + index))
                    {
                        if (placementItem != null) CancelPartPlacement();
                        SetActiveTool((BlueprintEditorTool)index);
                        return true;
                    }
            }
            if (keyboardPreview)
            {
                if (input.GetKeyDown(KeyCode.Escape)) { CancelGizmoDrag(); return true; }
                if (input.GetMouseButton(1) || input.GetMouseButton(2)) return false;
                if (control && (input.GetKeyDown(KeyCode.Z) || input.GetKeyDown(KeyCode.Y)))
                { CancelGizmoDrag(); return true; }
                if (control || alt) return false;
                if (!shift && (input.GetKeyDown(KeyCode.G) || input.GetKeyDown(KeyCode.R) || input.GetKeyDown(KeyCode.S)))
                {
                    GizmoHandleKind next = input.GetKeyDown(KeyCode.R) ? GizmoHandleKind.Rotate :
                        input.GetKeyDown(KeyCode.S) ? GizmoHandleKind.Scale : GizmoHandleKind.Move;
                    CancelGizmoDrag();
                    BeginKeyboardPreview(next);
                    return true;
                }
                ReadKeyboardNumber();
                if (input.GetKeyDown(KeyCode.Return) || input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    keyboardConfirmRequested = true;
                    return false;
                }
                GizmoAxis axis = input.GetKeyDown(KeyCode.X) ? GizmoAxis.X :
                    input.GetKeyDown(KeyCode.Y) ? GizmoAxis.Y :
                    input.GetKeyDown(KeyCode.Z) ? GizmoAxis.Z : GizmoAxis.None;
                if (axis != GizmoAxis.None && dragHandle != GizmoHandleKind.Scale)
                {
                    SetKeyboardAxis(axis);
                }
                else if (input.GetKeyDown(KeyCode.P) &&
                    (dragHandle == GizmoHandleKind.Move || dragHandle == GizmoHandleKind.MovePlane))
                {
                    keyboardSurface = !keyboardSurface;
                    dragAxis = GizmoAxis.None;
                    dragHandle = keyboardSurface ? GizmoHandleKind.Move : GizmoHandleKind.MovePlane;
                    if (keyboardSurface) RefreshCursorSources();
                    else view.SetCursorSourcesAvailable(false);
                    RebaseKeyboardPreview(input.MousePosition);
                }
                else if (keyboardNumber.Length == 0 && keyboardSurface && dragSourceAnchors.Length > 0 &&
                    (input.GetKeyDown(KeyCode.Q) || input.GetKeyDown(KeyCode.E)))
                {
                    int step = input.GetKeyDown(KeyCode.E) ? 1 : -1;
                    int choice = keyboardSourceChoices.IndexOf(keyboardSourceIndex) + 1;
                    choice = (choice + step + keyboardSourceChoices.Count + 1) % (keyboardSourceChoices.Count + 1);
                    ChooseCursorSource(choice == 0 ? -1 : keyboardSourceChoices[choice - 1]);
                }
                return false;
            }
            if (placementItem != null && input.GetKeyDown(KeyCode.Escape))
            {
                CancelPartPlacement();
                return true;
            }
            if (placementItem != null &&
                (input.GetKeyDown(KeyCode.G) || input.GetKeyDown(KeyCode.F9)))
            {
                CancelPartPlacement();
                SetActiveTool(BlueprintEditorTool.Transform);
                return true;
            }
            if (placementItem != null)
            {
                bool placementControl = input.GetKey(KeyCode.LeftControl) ||
                    input.GetKey(KeyCode.RightControl);
                bool placementShift = input.GetKey(KeyCode.LeftShift) ||
                    input.GetKey(KeyCode.RightShift);
                if (placementControl && input.GetKeyDown(KeyCode.Z))
                {
                    if (placementShift) ApplyHistory(document.Redo, document.Undo);
                    else ApplyHistory(document.Undo, document.Redo);
                    return true;
                }
                if (placementControl && input.GetKeyDown(KeyCode.Y))
                {
                    ApplyHistory(document.Redo, document.Undo);
                    return true;
                }
                if (input.GetKeyDown(KeyCode.Delete) && view.ViewportScreenRect().Contains(input.MousePosition) &&
                    scene.TryPick(input.MousePosition, out string removeId))
                {
                    document.SelectOnly(removeId);
                    ApplyDocumentEdit(() => document.DeleteSelection(false));
                    return true;
                }
                if (!input.GetMouseButton(1) && input.GetKeyDown(KeyCode.Q))
                {
                    CyclePlacementSnapPoint(-1);
                    return true;
                }
                if (!input.GetMouseButton(1) && input.GetKeyDown(KeyCode.E))
                {
                    CyclePlacementSnapPoint(1);
                    return true;
                }
            }
            if (placementItem != null) return false;
            if (IsGizmoDragging && input.GetKeyDown(KeyCode.Escape))
            {
                CancelGizmoDrag();
                return true;
            }
            if (IsGizmoDragging)
            {
                if (dragAnchorPoint >= 0 && !dragMagneticMove &&
                    !input.GetKey(KeyCode.LeftControl) && !input.GetKey(KeyCode.RightControl))
                {
                    if (input.GetKeyDown(KeyCode.X)) SetAnchorConstraint(GizmoAxis.X);
                    else if (input.GetKeyDown(KeyCode.Y)) SetAnchorConstraint(GizmoAxis.Y);
                    else if (input.GetKeyDown(KeyCode.Z)) SetAnchorConstraint(GizmoAxis.Z);
                }
                return false;
            }
            if (rightCameraTracking || input.GetMouseButton(1) &&
                view.ViewportScreenRect().Contains(input.MousePosition)) return false;
            if (input.GetKeyDown(KeyCode.F7))
            {
                ToggleOccluderFade();
                return true;
            }
            if (input.GetKeyDown(KeyCode.Tab))
            {
                RequestCatalog();
                return true;
            }
            if (activeTool == BlueprintEditorTool.Contour)
            {
                if (input.GetKeyDown(KeyCode.Escape))
                {
                    CancelContour();
                    return true;
                }
                if (input.GetKeyDown(KeyCode.Return) ||
                    input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ApplyContour();
                    return true;
                }
            }
            if (activeTool == BlueprintEditorTool.Array)
            {
                if (input.GetKeyDown(KeyCode.Escape))
                {
                    CancelArray();
                    return true;
                }
                if (input.GetKeyDown(KeyCode.Return) ||
                    input.GetKeyDown(KeyCode.KeypadEnter))
                {
                    ApplyArray();
                    return true;
                }
            }
            if (!alt && !shift && input.GetKeyDown(KeyCode.A))
            {
                document.ClearSelection();
                foreach (BlueprintEditorPart part in document.Parts)
                    if (document.IsEffectivelyVisible(part.StableId) &&
                        !document.IsEffectivelyLocked(part.StableId))
                        document.ToggleSelection(part.StableId);
                RefreshSelection();
                SetSelectionTool();
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.Z))
            {
                if (shift) ApplyHistory(document.Redo, document.Undo);
                else ApplyHistory(document.Undo, document.Redo);
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.Y))
            {
                ApplyHistory(document.Redo, document.Undo);
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.S))
            {
                TrySave(closeAfterSave: false);
                return true;
            }
            if (shift && input.GetKeyDown(KeyCode.A))
            {
                RequestCatalog();
                return true;
            }
            if (shift && input.GetKeyDown(KeyCode.S))
            {
                view.FocusMoveStep();
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.D))
            {
                ApplyDocumentEdit(() =>
                    document.DuplicateSelection(new Point3(0.5, 0.0, 0.5),
                        BuildWorksLocalization.Text("editor.generated.copy")));
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.G))
            {
                ApplyDocumentEdit(() => document.CreateGroup(
                    Guid.NewGuid().ToString("N"), BuildWorksLocalization.Text(
                        "editor.group_name", document.Groups.Count + 1)));
                return true;
            }
            if (input.GetKeyDown(KeyCode.H))
            {
                if (alt) ApplyDocumentEdit(document.ShowAll);
                else if (control) ApplyDocumentEdit(document.ShowAllExceptSelection);
                else if (shift) ApplyDocumentEdit(document.IsolateSelection);
                else ApplyDocumentEdit(() => document.SetSelectionVisibility(false));
                return true;
            }
            if (input.GetKeyDown(KeyCode.Delete))
            {
                ApplyDocumentEdit(() => document.DeleteSelection(false));
                return true;
            }
            if (input.GetKeyDown(KeyCode.F))
            {
                FrameSelectionOrAll();
                return true;
            }
            if (input.GetKeyDown(KeyCode.Home))
            {
                FrameAll();
                return true;
            }
            if (control || alt || shift) return false;
            if (input.GetKeyDown(KeyCode.F9))
                SetActiveTool(BlueprintEditorTool.Transform);
            else if (input.GetKeyDown(KeyCode.G))
            {
                if (SelectionGizmoTool == BlueprintEditorTool.Transform) BeginKeyboardPreview(GizmoHandleKind.Move);
                else SetActiveTool(BlueprintEditorTool.Transform);
            }
            else if (input.GetKeyDown(KeyCode.R) || input.GetKeyDown(KeyCode.S))
            {
                if (SelectionGizmoTool == BlueprintEditorTool.Transform)
                    BeginKeyboardPreview(input.GetKeyDown(KeyCode.R) ? GizmoHandleKind.Rotate : GizmoHandleKind.Scale);
                else view.SetStatus(BuildWorksLocalization.Text("editor.hint.open_gizmo"));
            }
            else if (input.GetKeyDown(KeyCode.C)) SetActiveTool(BlueprintEditorTool.Contour);
            else if (input.GetKeyDown(KeyCode.Space) || input.GetKeyDown(KeyCode.F3))
                view.ShowToolsMenu(input.MousePosition, input.GetKeyDown(KeyCode.F3));
            else if (input.GetKeyDown(KeyCode.X) || input.GetKeyDown(KeyCode.Y) || input.GetKeyDown(KeyCode.Z))
            {
                GizmoAxis axis = input.GetKeyDown(KeyCode.X) ? GizmoAxis.X : input.GetKeyDown(KeyCode.Y) ? GizmoAxis.Y : GizmoAxis.Z;
                if (SelectionGizmoTool == BlueprintEditorTool.Transform && gizmoFamily != GizmoFamily.Points)
                { BeginKeyboardPreview(GizmoHandleKind.Move); if (keyboardPreview) SetKeyboardAxis(axis); }
                else if (SelectionGizmoTool == BlueprintEditorTool.Transform) SetAnchorConstraint(axis);
            }
            else if (input.GetKeyDown(KeyCode.Escape))
            {
                if (activeTool == BlueprintEditorTool.Transform)
                { SetActiveTool(BlueprintEditorTool.Select); return true; }
                if (document.Selection.Count > 0)
                {
                    document.ClearSelection();
                    SetActiveTool(BlueprintEditorTool.Select);
                    RefreshSelection();
                    return true;
                }
                Close(discardChanges: false);
                return true;
            }
            return false;
        }

        private void RefreshContextHints()
        {
            string hints;
            if (view.HasModal)
                hints = BuildWorksLocalization.Text("editor.hint.modal");
            else if (view.HasOutlinerContextMenu)
                hints = BuildWorksLocalization.Text("editor.hint.context_menu");
            else if (view.HasOutlinerMenu)
                hints = BuildWorksLocalization.Text("editor.hint.outliner_menu");
            else if (view.HasViewportSettings)
                hints = BuildWorksLocalization.Text("editor.hint.viewport_settings");
            else if (view.HasToolsMenu)
                hints = BuildWorksLocalization.Text("editor.hint.tools_menu");
            else if (view.HasCatalog)
                hints = view.IsTextInputFocused
                    ? BuildWorksLocalization.Text("editor.hint.catalog_input")
                    : BuildWorksLocalization.Text("editor.hint.catalog");
            else if (view.IsOutlinerDragging)
                hints = BuildWorksLocalization.Text("editor.hint.reparent");
            else if (view.IsNumericScrubbing)
                hints = BuildWorksLocalization.Text("editor.hint.numeric_scrub");
            else if (view.IsTextInputFocused)
                hints = BuildWorksLocalization.Text("editor.hint.text_input");
            else if (placementItem != null)
                hints = BuildWorksLocalization.Text("editor.hint.part_placement") + " · " +
                    BuildWorksLocalization.Text(
                        "editor.snap_selected",
                        PlacementSnapLabel(scene.PlacementSourceSnapPointCount));
            else if (IsGizmoDragging)
                hints = BuildWorksLocalization.Text(keyboardPreview ? "editor.hint.cursor_preview" : "editor.hint.gizmo_drag");
            else if (rightCameraTracking || input.GetMouseButton(1) &&
                view.ViewportScreenRect().Contains(input.MousePosition))
                hints = BuildWorksLocalization.Text("editor.hint.camera");
            else if (activeTool == BlueprintEditorTool.Array)
                hints = arrayPreviewCount > 0
                    ? BuildWorksLocalization.Text("editor.hint.array_ready")
                    : BuildWorksLocalization.Text("editor.hint.array_begin");
            else if (activeTool == BlueprintEditorTool.Contour)
                hints = contourPreviewCount > 0
                    ? BuildWorksLocalization.Text("editor.hint.contour_ready")
                    : BuildWorksLocalization.Text("editor.hint.contour_begin");
            else if (activeTool == BlueprintEditorTool.Transform)
                hints = BuildWorksLocalization.Text("editor.hint.transform");
            else if (document.Parts.Count == 0)
                hints = BuildWorksLocalization.Text("editor.hint.empty");
            else
            {
                hints = BuildWorksLocalization.Text("editor.hint.select");
                if (document.EditablePartSelectionCount > 0)
                    hints = BuildWorksLocalization.Text("editor.hint.transform");
            }
            view.SetNextAction(keyboardPreview
                ? BuildWorksLocalization.Text("editor.hint.preview", BuildWorksLocalization.Text(
                    dragHandle == GizmoHandleKind.Rotate ? "editor.view.family_rotate" :
                    dragHandle == GizmoHandleKind.Scale ? "editor.view.family_scale" : "editor.view.family_move"),
                    keyboardSurface ? BuildWorksLocalization.Text("editor.view.surface_controls") + " · " +
                        (keyboardSourceIndex < 0 ? BuildWorksLocalization.Text("editor.view.source_auto") :
                            BuildWorksLocalization.Text(keyboardSourceIndex >= gizmoNativeAnchorStart && keyboardSourceIndex < dragNativeAnchorEnd
                                ? "editor.view.source_native" : "editor.view.source_helper",
                                keyboardSourceIndex >= gizmoNativeAnchorStart ? keyboardSourceIndex - gizmoNativeAnchorStart + 1 : keyboardSourceIndex + 1)) :
                    dragAxis == GizmoAxis.None ? BuildWorksLocalization.Text("editor.view.screen_plane") :
                    dragAxis + " · " + BuildWorksLocalization.Text(localSpace ? "editor.view.axes_local" : "editor.view.axes_world"),
                    keyboardNumber.Length > 0 ? BuildWorksLocalization.Text("editor.hint.numeric_value", keyboardNumber,
                        keyboardValid ? "" : BuildWorksLocalization.Text("editor.hint.numeric_invalid")) :
                    keyboardValid ? "" : BuildWorksLocalization.Text("editor.view.no_surface"))
                : !IsGizmoDragging && placementItem == null && !view.HasModal && !view.HasCatalog &&
                    !view.HasToolsMenu && !view.HasViewportSettings &&
                    (activeTool == BlueprintEditorTool.Select || activeTool == BlueprintEditorTool.Transform)
                    ? BuildWorksLocalization.Text(document.EditablePartSelectionCount > 0 ?
                        activeTool == BlueprintEditorTool.Transform ? "editor.hint.gizmo_ready" : "editor.hint.next_selected" :
                        "editor.hint.next_empty") : hints);
            view.SetPreviewAxes(keyboardPreview && dragHandle != GizmoHandleKind.Scale, dragAxis);
            if (!view.HasModal && !view.HasOutlinerContextMenu && !view.HasOutlinerMenu &&
                !view.HasViewportSettings && !view.HasToolsMenu && !view.HasCatalog && !view.IsOutlinerDragging &&
                !view.IsNumericScrubbing && !view.IsTextInputFocused && placementItem == null &&
                !IsGizmoDragging && !rightCameraTracking && !input.GetMouseButton(1))
            {
                if ((activeTool == BlueprintEditorTool.Select || activeTool == BlueprintEditorTool.Transform) &&
                    view.ViewportScreenRect().Contains(input.MousePosition) &&
                    !view.IsViewportControlHit(input.MousePosition) &&
                    TrySelectionPivot(out Vector3 hoverPivot, out Quaternion hoverOrientation, gizmoIds))
                {
                    GizmoHandleKind hoverHandle = scene.HitTestGizmo(SelectionGizmoTool, hoverPivot,
                        hoverOrientation, localSpace, input.MousePosition, out _);
                    bool copyArrow = (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt)) &&
                        hoverHandle == GizmoHandleKind.Move;
                    int point = hoverHandle == GizmoHandleKind.Scale || copyArrow
                        ? -1 : scene.HitTestAnchor(gizmoAnchors, input.MousePosition);
                    if (point >= 0)
                        view.SetNextAction(BuildWorksLocalization.Text("editor.view.point_hover",
                            BuildWorksLocalization.Text(point == pinnedAnchorPoint ? "editor.view.point_pin" :
                                point >= gizmoNativeAnchorEnd ? "editor.view.point_active" :
                                point >= gizmoNativeAnchorStart ? "editor.view.point_native" :
                                point == AnchorAdjustment.CenterAnchorIndex ? "editor.view.point_centre" :
                                point < 8 ? "editor.view.point_corner" : "editor.view.point_midpoint")));
                }
                bool selected = document.EditablePartSelectionCount > 0;
                view.SetContextHintGroups(
                    BuildWorksLocalization.Text(selected ? "editor.hint.modes_selected" : "editor.hint.modes_empty"),
                    hints,
                    BuildWorksLocalization.Text(selected ? "editor.hint.selection_actions" : "editor.hint.selection_empty"),
                    BuildWorksLocalization.Text("editor.hint.commands"),
                    BuildWorksLocalization.Text(selected ? "editor.hint.visibility_selected" : "editor.hint.visibility_empty"),
                    BuildWorksLocalization.Text("editor.hint.camera_idle"));
                return;
            }
            view.SetContextHints(hints);
        }

        private void ToggleOccluderFade()
        {
            scene.SetOccluderFade(!scene.OccluderFadeEnabled);
            view.SetOccluderFade(scene.OccluderFadeEnabled);
        }

        private void HandleViewport(Rect viewport)
        {
            Vector2 mouse = input.MousePosition;
            bool inside = viewport.Contains(mouse);
            if ((!IsGizmoDragging || keyboardPreview) && !cameraDragging && !rightCameraTracking &&
                view.IsViewportControlHit(mouse))
            {
                selectionPending = false;
                scene.SetHovered(null, document);
                view.SetHoveredNode(null);
                if (keyboardPreview) keyboardPaused = true;
                return;
            }
            if (inside && input.MouseScrollDelta.y != 0f)
            {
                bool control = input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl);
                if (keyboardPreview && keyboardNumber.Length == 0 && keyboardSurface && !control && !input.GetMouseButton(1))
                {
                    int sourceIndex = keyboardSourceIndex >= 0 ? keyboardSourceIndex : keyboardAutoSource;
                    Vector3 source = sourceIndex >= 0 ? dragSourceAnchors[sourceIndex] : dragPivot;
                    Vector3 offset = source - dragPivot;
                    Vector3 oldOffset = dragRotation * offset * dragScale;
                    dragRotation = Quaternion.AngleAxis(input.MouseScrollDelta.y > 0f ? 22.5f : -22.5f,
                        Vector3.up) * dragRotation;
                    dragTranslation += oldOffset - dragRotation * offset * dragScale;
                    RebaseKeyboardPreview(mouse);
                    scene.PreviewTransform(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
                }
                else if (placementItem != null && !control && !input.GetMouseButton(1))
                    placementYaw += input.MouseScrollDelta.y > 0f ? 22.5f : -22.5f;
                else if (activeTool == BlueprintEditorTool.Array && arrayPrimaryAxis != GizmoAxis.None &&
                    !control && !IsGizmoDragging)
                    AdjustArrayCount(input.MouseScrollDelta.y > 0f ? 1 : -1);
                else
                {
                    cameraDistance = Mathf.Clamp(
                        cameraDistance * Mathf.Pow(0.88f, input.MouseScrollDelta.y), .5f, 500f);
                    if (keyboardPreview) keyboardPaused = true;
                }
            }

            if (inside && (!IsGizmoDragging || keyboardPreview) && input.GetMouseButtonDown(2))
            {
                cameraDragging = true;
                middleDeleteTarget = null;
                if (!keyboardPreview && (activeTool == BlueprintEditorTool.Select || placementItem != null) &&
                    !input.GetMouseButton(0) && !input.GetMouseButton(1) &&
                    !ShiftHeld && !input.GetKey(KeyCode.LeftControl) && !input.GetKey(KeyCode.RightControl) &&
                    !input.GetKey(KeyCode.LeftAlt) && !input.GetKey(KeyCode.RightAlt) &&
                    scene.TryPick(mouse, out string deleteTarget) && !document.IsEffectivelyLocked(deleteTarget))
                    middleDeleteTarget = deleteTarget;
                if (keyboardPreview) keyboardPaused = true;
                middleCameraStart = mouse;
                middleCameraMoved = false;
                previousMouse = mouse;
                selectionPending = false;
            }
            if (cameraDragging && input.GetMouseButton(2))
            {
                if (!middleCameraMoved)
                {
                    if ((mouse - middleCameraStart).sqrMagnitude <= 36f)
                    {
                        previousMouse = mouse;
                        return;
                    }
                    middleCameraMoved = true;
                    previousMouse = middleCameraStart;
                }
                Vector2 delta = mouse - previousMouse;
                previousMouse = mouse;
                bool pan = input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift);
                if (pan)
                {
                    Quaternion rotation = CameraRotation();
                    float scale = cameraDistance * 0.0015f;
                    cameraFocus -= rotation * Vector3.right * delta.x * scale;
                    cameraFocus -= rotation * Vector3.up * delta.y * scale;
                }
                else
                {
                    cameraYaw += delta.x * 0.25f;
                    cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.25f, -85f, 85f);
                }
            }
            if (cameraDragging && input.GetMouseButtonUp(2))
            {
                cameraDragging = false;
                string target = middleDeleteTarget;
                middleDeleteTarget = null;
                if (!middleCameraMoved && inside && !view.IsViewportControlHit(mouse) && !keyboardPreview &&
                    !ShiftHeld && !input.GetKey(KeyCode.LeftControl) && !input.GetKey(KeyCode.RightControl) &&
                    !input.GetKey(KeyCode.LeftAlt) && !input.GetKey(KeyCode.RightAlt) && target != null &&
                    (activeTool == BlueprintEditorTool.Select || placementItem != null) &&
                    scene.TryPick(mouse, out string currentTarget) && currentTarget == target &&
                    document.IsEffectivelyVisible(target) && !document.IsEffectivelyLocked(target))
                {
                    DeleteViewportPart(target);
                    return;
                }
            }

            if (inside && !cameraDragging && (!IsGizmoDragging || keyboardPreview) &&
                input.GetMouseButtonDown(1))
            {
                rightCameraTracking = true;
                if (keyboardPreview) keyboardPaused = true;
                rightCameraStart = mouse;
            }
            if (rightCameraTracking && input.GetMouseButton(1))
            {
                bool moving = input.GetKey(KeyCode.W) || input.GetKey(KeyCode.A) ||
                    input.GetKey(KeyCode.S) || input.GetKey(KeyCode.D) ||
                    input.GetKey(KeyCode.Q) || input.GetKey(KeyCode.E);
                if (!cameraFlying && ((mouse - rightCameraStart).sqrMagnitude > 36f || moving))
                {
                    cameraFlying = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                if (cameraFlying)
                {
                    Vector3 position = cameraFocus - CameraRotation() * Vector3.forward * cameraDistance;
                    cameraYaw += input.GetAxis("Mouse X") * 3f;
                    cameraPitch = Mathf.Clamp(cameraPitch - input.GetAxis("Mouse Y") * 3f, -85f, 85f);
                    Quaternion rotation = CameraRotation();
                    Vector3 movement = Vector3.zero;
                    if (input.GetKey(KeyCode.W)) movement += rotation * Vector3.forward;
                    if (input.GetKey(KeyCode.S)) movement -= rotation * Vector3.forward;
                    if (input.GetKey(KeyCode.D)) movement += rotation * Vector3.right;
                    if (input.GetKey(KeyCode.A)) movement -= rotation * Vector3.right;
                    if (input.GetKey(KeyCode.E)) movement += Vector3.up;
                    if (input.GetKey(KeyCode.Q)) movement -= Vector3.up;
                    float speed = Mathf.Max(1.5f, cameraDistance * 0.5f) * input.UnscaledDeltaTime;
                    if (input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift)) speed *= 3f;
                    cameraFocus = position + movement.normalized * speed + rotation * Vector3.forward * cameraDistance;
                }
                if (placementItem != null)
                {
                    ApplyCamera();
                    HandlePartPlacement(cameraFlying ? viewport.center : mouse, inside || cameraFlying);
                }
                return;
            }
            if (rightCameraTracking && !input.GetMouseButton(1))
            {
                bool click = !cameraFlying && inside && !keyboardPreview;
                rightCameraTracking = cameraFlying = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                if (click)
                {
                    if (placementItem != null) CancelPartPlacement();
                    RequestCatalog();
                    return;
                }
                if (placementItem != null)
                {
                    ApplyCamera();
                    HandlePartPlacement(mouse, inside);
                }
                return;
            }

            if (keyboardPreview)
            {
                if (!inside || cameraDragging || input.GetMouseButton(1) || input.GetMouseButton(2))
                { keyboardPaused = true; return; }
                if (keyboardPaused)
                { RebaseKeyboardPreview(mouse); keyboardMoved = keyboardSourceChanged; keyboardPaused = false; return; }
                PreviewKeyboardTransform(mouse);
                if ((input.GetMouseButtonDown(0) || keyboardConfirmRequested) && keyboardValid) CommitGizmoDrag();
                return;
            }

            if (placementItem != null)
            {
                ApplyCamera();
                HandlePartPlacement(mouse, inside);
                return;
            }

            if (inside && !cameraDragging && input.GetMouseButtonDown(0))
            {
                if (TryBeginGizmoDrag(mouse))
                {
                    selectionPending = false;
                }
                else if (activeTool == BlueprintEditorTool.Contour)
                {
                    SelectContourSupport(mouse);
                    return;
                }
                else if (activeTool == BlueprintEditorTool.Array)
                {
                    return;
                }
                else if (activeTool == BlueprintEditorTool.Select ||
                    activeTool == BlueprintEditorTool.Transform)
                {
                    selectionStart = mouse;
                    selectionPending = true;
                }
            }
            if (IsGizmoDragging && input.GetMouseButton(0))
                PreviewGizmoDrag(mouse);
            if (IsGizmoDragging && input.GetMouseButtonUp(0))
                CommitGizmoDrag();
            if (selectionPending && input.GetMouseButtonUp(0))
            {
                selectionPending = false;
                SelectViewport(selectionStart, mouse);
            }

            string hovered = null;
            if (inside && (activeTool == BlueprintEditorTool.Select ||
                activeTool == BlueprintEditorTool.Transform) &&
                !cameraDragging && !IsGizmoDragging)
                scene.TryPick(mouse, out hovered);
            scene.SetHovered(hovered, document);
            view.SetHoveredNode(hovered);
        }

        private void BeginKeyboardPreview(GizmoHandleKind handle)
        {
            if (document.EditablePartSelectionCount == 0) return;
            SetActiveTool(BlueprintEditorTool.Transform);
            if (!TrySelectionPivot(out Vector3 pivot, out keyboardOrientation, dragIds)) return;
            UpdateGizmo();
            keyboardSavedSelectionPoint = selectedAnchorWorld;
            keyboardSavedPin = pinnedAnchorWorld;
            keyboardSavedConstraint = anchorConstraintAxis;
            keyboardSavedLocalSpace = localSpace;
            keyboardStartViewAxis = scene.Camera.transform.forward;
            keyboardNumber = "";
            dragPivot = selectedAnchorWorld ?? pivot;
            dragSourceAnchors = (Vector3[])gizmoAnchors.Clone();
            dragNativeAnchorEnd = gizmoNativeAnchorEnd;
            keyboardSourceIndex = selectedAnchorPoint >= 0 && selectedAnchorPoint < dragSourceAnchors.Length
                ? selectedAnchorPoint : -1;
            if (keyboardSourceIndex < 0 && preferredCursorIds.Count == dragIds.Count &&
                preferredCursorIds.TrueForAll(id => dragIds.Contains(id)) &&
                preferredCursorSource < dragSourceAnchors.Length)
                keyboardSourceIndex = preferredCursorSource;
            dragTranslation = Vector3.zero;
            dragRotation = Quaternion.identity;
            dragScale = 1f;
            dragHandle = handle;
            dragAxis = GizmoAxis.None;
            keyboardPreview = true;
            keyboardSurface = handle == GizmoHandleKind.Move;
            keyboardAutoSource = keyboardHoveredSource = -1;
            keyboardSourceChanged = false;
            RefreshCursorSources();
            gizmoFamily = GizmoFamily.Combined;
            view.SetGizmoFamily(gizmoFamily);
            view.SetPreviewControls(true);
            if (!keyboardSurface) view.SetCursorSourcesAvailable(false);
            RebaseKeyboardPreview(input.MousePosition);
            selectionPending = false;
            UpdateGizmo();
        }

        private void RefreshCursorSources()
        {
            keyboardSourceChoices.Clear();
            for (int index = gizmoNativeAnchorStart; index < dragNativeAnchorEnd; ++index)
                keyboardSourceChoices.Add(index);
            bool fallback = keyboardSourceChoices.Count == 0;
            if (keyboardHelpersEnabled || fallback)
                for (int index = 0; index < Mathf.Min(gizmoNativeAnchorStart, dragSourceAnchors.Length); ++index)
                    keyboardSourceChoices.Add(index);
            var labels = new List<string>();
            foreach (int index in keyboardSourceChoices)
            {
                Vector3 delta = Quaternion.Inverse(keyboardOrientation) * (dragSourceAnchors[index] - dragPivot);
                labels.Add(BuildWorksLocalization.Text(index >= gizmoNativeAnchorStart && index < dragNativeAnchorEnd
                    ? "editor.view.source_native" : "editor.view.source_helper",
                    index >= gizmoNativeAnchorStart ? index - gizmoNativeAnchorStart + 1 : index + 1) +
                    " · " + delta.x.ToString("0.##", CultureInfo.CurrentCulture) + "/" +
                    delta.y.ToString("0.##", CultureInfo.CurrentCulture) + "/" + delta.z.ToString("0.##", CultureInfo.CurrentCulture));
            }
            view.SetCursorSources(keyboardSourceChoices, gizmoNativeAnchorStart, dragNativeAnchorEnd,
                keyboardSourceIndex, keyboardHelpersEnabled, fallback, labels);
        }

        private void ChooseCursorSource(int index)
        {
            if (!keyboardPreview || !keyboardSurface || index < -1 || index >= dragSourceAnchors.Length) return;
            keyboardSourceIndex = index;
            preferredCursorSource = index;
            preferredCursorIds.Clear();
            preferredCursorIds.AddRange(dragIds);
            keyboardSourceChanged = true;
            keyboardAutoSource = keyboardHoveredSource = -1;
            snapTargetVisible = false;
            RebaseKeyboardPreview(input.MousePosition);
            keyboardMoved = true;
            RefreshCursorSources();
            // A chooser click remains UI input. Evaluate only when the cursor
            // returns to the viewport; Q/E evaluates in this same frame.
        }

        private void RebaseKeyboardPreview(Vector2 mouse)
        {
            keyboardBaseTranslation = dragTranslation;
            keyboardBaseRotation = dragRotation;
            keyboardBaseScale = dragScale;
            keyboardMoved = false;
            dragStartMouse = mouse;
            Vector3 origin = dragPivot + dragTranslation;
            Quaternion orientation = dragRotation * keyboardOrientation;
            dragAxisWorld = dragAxis == GizmoAxis.None ? scene.Camera.transform.forward :
                TransformGizmoView.AxisVector(dragAxis, orientation, localSpace);
            dragAnchorPlane = new Plane(dragAxisWorld, origin);
            Ray ray = scene.Camera.ScreenPointToRay(mouse);
            keyboardValid = !keyboardSurface && dragAnchorPlane.Raycast(ray, out _);
            dragPlaneStart = dragAnchorPlane.Raycast(ray, out float distance) ? ray.GetPoint(distance) : origin;
            keyboardPlaneFirst = Vector3.Cross(dragAxisWorld,
                Mathf.Abs(Vector3.Dot(dragAxisWorld, Vector3.up)) < .9f ? Vector3.up : Vector3.right).normalized;
            keyboardPlaneSecond = Vector3.Cross(dragAxisWorld, keyboardPlaneFirst).normalized;
            Vector3 start = scene.Camera.WorldToScreenPoint(origin);
            Vector3 end = scene.Camera.WorldToScreenPoint(origin + dragAxisWorld);
            Vector2 screenAxis = new Vector2(end.x - start.x, end.y - start.y);
            dragScreenDirection = screenAxis.normalized;
            dragWorldUnitsPerPixel = 1f / Mathf.Max(2f, screenAxis.magnitude);
            dragStartAngle = Mathf.Atan2(mouse.y - start.y, mouse.x - start.x) * Mathf.Rad2Deg;
            if (dragHandle == GizmoHandleKind.Rotate)
            {
                dragUsesRotationPlane = TryRotationDirection(mouse, origin, dragAxisWorld, out dragStartDirection);
                keyboardValid = dragUsesRotationPlane;
            }
            if (dragHandle == GizmoHandleKind.Scale) keyboardValid = true;
            snapTargetVisible = false;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
        }

        private void SetKeyboardAxis(GizmoAxis axis)
        {
            if (dragHandle == GizmoHandleKind.Scale) return;
            if (axis == dragAxis) localSpace = !localSpace;
            dragAxis = axis;
            keyboardSurface = false;
            view.SetCursorSourcesAvailable(false);
            if (dragHandle != GizmoHandleKind.Rotate)
                dragHandle = ShiftHeld ? GizmoHandleKind.MovePlane : GizmoHandleKind.Move;
            // Changing constraint starts at the original operation pose. Camera
            // rebasing still retains the preview; it is a different operation.
            dragTranslation = Vector3.zero;
            dragRotation = Quaternion.identity;
            dragScale = 1f;
            scene.PreviewTransform(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
            RebaseKeyboardPreview(input.MousePosition);
            UpdateGizmo();
        }

        private void PreviewKeyboardTransform(Vector2 mouse)
        {
            if (keyboardNumber.Length > 0)
            { PreviewKeyboardNumber(); return; }
            if (!keyboardMoved && (mouse - dragStartMouse).sqrMagnitude < 1f)
            {
                if (keyboardSurface) keyboardValid = scene.TryCursorSurface(mouse, dragIds, out _, out _);
                return;
            }
            keyboardMoved = true;
            keyboardValid = true;
            if (keyboardSurface)
            {
                bool surfaceHit = scene.TryCursorSurface(mouse, dragIds, out Vector3 target, out Vector3 normal);
                keyboardValid = surfaceHit;
                if (!keyboardValid)
                { snapTargetVisible = false; snapPreviewTargets.Clear(); snapPreviewNative.Clear(); return; }
                Vector3 source = keyboardSourceIndex >= 0 && dragSourceAnchors.Length > 0
                    ? dragSourceAnchors[keyboardSourceIndex] : dragPivot;
                dragTranslation = target - (dragPivot + dragRotation * (source - dragPivot) * dragScale);
                if (keyboardSourceIndex < 0)
                {
                    scene.PreviewTransform(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
                    dragTranslation += scene.CursorContactOffset(dragIds, target, normal);
                }
                keyboardSnapSources.Clear();
                if (keyboardSourceIndex >= 0) keyboardSnapSources.Add(keyboardSourceIndex);
                else keyboardSnapSources.AddRange(keyboardSourceChoices);
                var sources = new Vector3[dragSourceAnchors.Length];
                for (int index = 0; index < sources.Length; ++index)
                    sources[index] = dragPivot + dragRotation * (dragSourceAnchors[index] - dragPivot) * dragScale + dragTranslation;
                bool wasSnapped = snapTargetVisible;
                Vector3 previousTarget = snapTargetWorld;
                snapTargetVisible = !ShiftHeld && scene.TryCursorSnap(dragIds, sources, keyboardSnapSources,
                    gizmoNativeAnchorStart, dragNativeAnchorEnd, keyboardHelpersEnabled, snapPreviewTargets, snapPreviewNative,
                    out keyboardAutoSource, out snapTargetWorld, out snapTargetIsNative,
                    wasSnapped ? keyboardAutoSource : -1, wasSnapped ? previousTarget : (Vector3?)null);
                if (snapTargetVisible) dragTranslation += snapTargetWorld - sources[keyboardAutoSource];
                else keyboardAutoSource = -1;
                if (ShiftHeld) { snapPreviewTargets.Clear(); snapPreviewNative.Clear(); }
                keyboardSourceChanged = false;
            }
            else if (dragHandle == GizmoHandleKind.Move)
            {
                float delta = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection) * dragWorldUnitsPerPixel;
                if (!ShiftHeld) delta = Mathf.Round(delta / translationStep) * translationStep;
                keyboardValid = dragScreenDirection.sqrMagnitude > .01f;
                dragTranslation = keyboardBaseTranslation + dragAxisWorld * delta;
            }
            else if (dragHandle == GizmoHandleKind.MovePlane)
            {
                Ray ray = scene.Camera.ScreenPointToRay(mouse);
                keyboardValid = Mathf.Abs(Vector3.Dot(ray.direction, dragAxisWorld)) > .001f &&
                    dragAnchorPlane.Raycast(ray, out _);
                if (!keyboardValid) return;
                dragAnchorPlane.Raycast(ray, out float distance);
                Vector3 delta = ray.GetPoint(distance) - dragPlaneStart;
                float first = Vector3.Dot(delta, keyboardPlaneFirst), second = Vector3.Dot(delta, keyboardPlaneSecond);
                if (!ShiftHeld)
                {
                    first = Mathf.Round(first / translationStep) * translationStep;
                    second = Mathf.Round(second / translationStep) * translationStep;
                }
                dragTranslation = keyboardBaseTranslation + keyboardPlaneFirst * first + keyboardPlaneSecond * second;
            }
            else if (dragHandle == GizmoHandleKind.Rotate)
            {
                keyboardValid = TryRotationDirection(mouse, dragPivot + keyboardBaseTranslation,
                    dragAxisWorld, out Vector3 direction);
                if (!keyboardValid) return;
                if (!dragUsesRotationPlane) { dragStartDirection = direction; dragUsesRotationPlane = true; return; }
                float degrees = Vector3.SignedAngle(dragStartDirection, direction, dragAxisWorld);
                if (!ShiftHeld) degrees = Mathf.Round(degrees / rotationStep) * rotationStep;
                dragRotation = Quaternion.AngleAxis(degrees, dragAxisWorld) * keyboardBaseRotation;
            }
            else if (dragHandle == GizmoHandleKind.Scale)
            {
                float percent = mouse.y - dragStartMouse.y;
                if (!ShiftHeld) percent = Mathf.Round(percent / view.ScaleStepPercent) * view.ScaleStepPercent;
                float minimum = .01f, maximum = 400f;
                foreach (BlueprintEditorPart part in document.Parts)
                {
                    if (!dragIds.Contains(part.StableId)) continue;
                    Vector3 scale = ToVector(part.Scale);
                    minimum = Mathf.Max(minimum, .01f / Mathf.Min(scale.x, scale.y, scale.z));
                    maximum = Mathf.Min(maximum, 4f / Mathf.Max(scale.x, scale.y, scale.z));
                }
                dragScale = Mathf.Clamp(keyboardBaseScale * Mathf.Max(.001f, 1f + percent * .01f), minimum, maximum);
            }
            scene.PreviewTransform(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
            UpdateGizmo();
        }

        private void ReadKeyboardNumber()
        {
            string previous = keyboardNumber;
            foreach (char character in input.InputString ?? "")
            {
                if (character == '\b')
                    keyboardNumber = keyboardNumber.Length == 0 ? "" : keyboardNumber.Substring(0, keyboardNumber.Length - 1);
                else if (keyboardNumber.Length < 32 &&
                    (character >= '0' && character <= '9' || character == '-' || character == '.' || character == ','))
                    keyboardNumber += character;
            }
            if (input.GetKeyDown(KeyCode.Delete)) keyboardNumber = "";
            if (previous.Length > 0 && keyboardNumber.Length == 0)
                RebaseKeyboardPreview(input.MousePosition);
        }

        private void PreviewKeyboardNumber()
        {
            // Numeric values replace the entire mouse delta. The document/pivot stay
            // unchanged throughout the preview, even when camera navigation rebases input.
            float value = 0f;
            keyboardValid = !keyboardNumber.EndsWith(".", StringComparison.Ordinal) &&
                !keyboardNumber.EndsWith(",", StringComparison.Ordinal) &&
                float.TryParse(keyboardNumber.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
            if (!keyboardValid) return;
            Vector3 axis = dragAxis == GizmoAxis.None ? keyboardStartViewAxis :
                TransformGizmoView.AxisVector(dragAxis, keyboardOrientation, localSpace);
            Vector3 translation = Vector3.zero;
            Quaternion rotation = Quaternion.identity;
            float scale = 1f;
            if (dragHandle == GizmoHandleKind.Rotate)
                rotation = Quaternion.AngleAxis(value, axis);
            else if (dragHandle == GizmoHandleKind.Scale)
            {
                scale = value;
                keyboardValid = scale > 0f;
                foreach (BlueprintEditorPart part in document.Parts)
                {
                    if (!dragIds.Contains(part.StableId)) continue;
                    Vector3 candidate = ToVector(part.Scale) * scale;
                    keyboardValid &= Mathf.Min(candidate.x, candidate.y, candidate.z) >= .01f &&
                        Mathf.Max(candidate.x, candidate.y, candidate.z) <= 4f;
                }
            }
            else
            {
                keyboardValid = dragAxis != GizmoAxis.None && dragHandle == GizmoHandleKind.Move;
                translation = axis * value;
                keyboardValid &= IsFinite(translation.sqrMagnitude);
            }
            if (!keyboardValid) return;
            dragTranslation = translation;
            dragRotation = rotation;
            dragScale = scale;
            snapTargetVisible = false;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            scene.PreviewTransform(document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
            UpdateGizmo();
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private bool TryBeginGizmoDrag(Vector2 mouse)
        {
            // Additive object selection wins over handles on a different object.
            if (activeTool == BlueprintEditorTool.Select &&
                (ShiftHeld || input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl)) &&
                scene.TryPick(mouse, out string pointedId) && !document.IsPartSelected(pointedId)) return false;
            if (!TrySelectionPivot(
                    out Vector3 pivot, out Quaternion orientation, dragIds)) return false;
            UpdateGizmo();
            GizmoHandleKind hoveredHandle = scene.HitTestGizmo(
                SelectionGizmoTool, pivot, orientation, localSpace, mouse, out GizmoAxis hoveredAxis);
            bool copyArrow = (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt)) &&
                hoveredHandle == GizmoHandleKind.Move;
            int anchor = gizmoFamily != GizmoFamily.Points && gizmoFamily != GizmoFamily.Combined && activeTool != BlueprintEditorTool.Array ||
                hoveredHandle == GizmoHandleKind.Scale || copyArrow ||
                activeTool == BlueprintEditorTool.Array && hoveredHandle == GizmoHandleKind.Layout
                ? -1 : scene.HitTestAnchor(gizmoAnchors, mouse);
            if (anchor >= 0)
            {
                selectedAnchorPoint = anchor;
                selectedAnchorWorld = gizmoAnchors[anchor];
                if (input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift))
                {
                    dragIds.Clear();
                    ToggleSelectedAnchorPin();
                    return true;
                }
                dragMagneticMove = input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl);
                int fixedAnchor = pinnedAnchorPoint >= 0 && pinnedAnchorPoint < gizmoAnchors.Length
                    ? pinnedAnchorPoint
                    : anchor < AnchorAdjustment.AnchorCount ? AnchorAdjustment.OppositeAnchor(anchor) : -1;
                if (!dragMagneticMove && (fixedAnchor < 0 || fixedAnchor == anchor))
                {
                    dragIds.Clear();
                    view.SetStatus(BuildWorksLocalization.Text("editor.anchor_controls"));
                    return true;
                }
                dragAnchorPoint = anchor;
                dragSourceAnchors = (Vector3[])gizmoAnchors.Clone();
                dragAnchorStartWorld = gizmoAnchors[anchor];
                dragPivot = dragMagneticMove ? pivot : gizmoAnchors[fixedAnchor];
                dragMovingVector = dragAnchorStartWorld - dragPivot;
                dragAnchorPlane = new Plane(
                    scene.Camera.transform.forward, dragAnchorStartWorld);
                if (!ConfigureAnchorConstraint(orientation))
                {
                    ResetGizmoDrag();
                    UpdateGizmo();
                    view.SetStatus(BuildWorksLocalization.Text("editor.anchor_on_axis"));
                    return true;
                }
                dragStartMouse = mouse;
                dragTranslation = Vector3.zero;
                dragRotation = Quaternion.identity;
                dragScale = 1f;
                selectionPending = false;
                view.SetStatus(BuildWorksLocalization.Text(dragMagneticMove
                    ? "editor.anchor_drag_magnetic"
                    : "editor.anchor_drag_rotate"));
                return true;
            }

            if (ShiftHeld) return false;

            dragHandle = hoveredHandle;
            if (dragHandle == GizmoHandleKind.None) return false;

            dragDuplicate = dragHandle == GizmoHandleKind.Move &&
                (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt));
            if (dragDuplicate)
            {
                try
                {
                    CopyDocument(document).DuplicateSelection(default,
                        BuildWorksLocalization.Text("editor.generated.copy"));
                }
                catch (Exception exception)
                {
                    ResetGizmoDrag();
                    view.SetStatus(exception.Message, error: true);
                    return true;
                }
            }

            dragAxis = hoveredAxis;
            dragSourceAnchors = (Vector3[])gizmoAnchors.Clone();
            dragPivot = pivot;
            dragStartMouse = mouse;
            dragTranslation = Vector3.zero;
            dragRotation = Quaternion.identity;
            dragScale = 1f;
            dragAxisWorld = TransformGizmoView.AxisVector(
                dragAxis, orientation, localSpace);
            Vector3 pivotScreen = scene.Camera.WorldToScreenPoint(pivot);
            if (dragHandle == GizmoHandleKind.Move || dragHandle == GizmoHandleKind.Layout)
            {
                if (dragHandle == GizmoHandleKind.Layout)
                {
                    dragStartArrayPrimaryAxis = arrayPrimaryAxis;
                    dragStartArrayCountX = arrayCountX;
                    dragStartArrayCountY = arrayCountY;
                    dragStartArrayStepX = arrayStepX;
                    dragStartArrayStepY = arrayStepY;
                    dragArraySecond = arrayPrimaryAxis != GizmoAxis.None && dragAxis != arrayPrimaryAxis;
                    dragStartArrayLastSecond = arrayLastSecond;
                    dragStartArrayDistanceX = arrayDistanceX;
                    dragStartArrayDistanceY = arrayDistanceY;
                    dragArrayStepLength = ArrayConfiguredStep(dragAxisWorld);
                }
                float axisLength = Mathf.Max(0.01f, scene.GizmoScale * 0.85f);
                Vector3 end = scene.Camera.WorldToScreenPoint(pivot + dragAxisWorld * axisLength);
                Vector2 screenAxis = new Vector2(end.x - pivotScreen.x, end.y - pivotScreen.y);
                float pixels = screenAxis.magnitude;
                if (pixels < 2f)
                {
                    ResetGizmoDrag();
                    return false;
                }
                dragScreenDirection = screenAxis / pixels;
                dragWorldUnitsPerPixel = axisLength / pixels;
            }
            else if (dragHandle == GizmoHandleKind.MovePlane)
            {
                dragAnchorPlane = new Plane(dragAxisWorld, pivot);
                Ray ray = scene.Camera.ScreenPointToRay(mouse);
                if (!dragAnchorPlane.Raycast(ray, out float distance))
                {
                    ResetGizmoDrag();
                    return false;
                }
                dragPlaneStart = ray.GetPoint(distance);
            }
            else if (dragHandle == GizmoHandleKind.Rotate)
            {
                dragStartAngle = Mathf.Atan2(
                    mouse.y - pivotScreen.y,
                    mouse.x - pivotScreen.x) * Mathf.Rad2Deg;
                dragUsesRotationPlane = TryRotationDirection(
                    mouse, dragPivot, dragAxisWorld, out dragStartDirection);
            }
            selectionPending = false;
            return true;
        }

        private void PreviewGizmoDrag(Vector2 mouse)
        {
            if (dragHandle == GizmoHandleKind.Layout)
            {
                PreviewArrayDrag(mouse);
                return;
            }
            Vector3 previousTranslation = dragTranslation;
            Quaternion previousRotation = dragRotation;
            float previousScale = dragScale;
            if (dragAnchorPoint >= 0)
            {
                Ray ray = scene.Camera.ScreenPointToRay(mouse);
                Vector3 target = dragAnchorStartWorld;
                if (dragAnchorPlane.Raycast(ray, out float distance))
                    target = ray.GetPoint(distance);
                bool free = input.GetKey(KeyCode.LeftShift) ||
                    input.GetKey(KeyCode.RightShift);
                if (!dragMagneticMove)
                {
                    Vector3 targetVector = target - (dragConstraintActive ? dragConstraintCenter : dragPivot);
                    if (targetVector.sqrMagnitude < 0.000001f) return;
                    if (dragConstraintActive)
                    {
                        float degrees = (float)AnchorAdjustment.ConstrainedAngleDegrees(
                            ToPoint(dragMovingVector), ToPoint(targetVector), ToPoint(dragAxisWorld));
                        if (!free) degrees = Mathf.Round(degrees / rotationStep) * rotationStep;
                        dragRotation = Quaternion.AngleAxis(degrees, dragAxisWorld);
                    }
                    else
                    {
                        Quaternion delta = Quaternion.FromToRotation(dragMovingVector, targetVector);
                        delta.ToAngleAxis(out float degrees, out Vector3 axis);
                        if (degrees > 180f) degrees -= 360f;
                        if (!free) degrees = Mathf.Round(degrees / rotationStep) * rotationStep;
                        dragRotation = Quaternion.AngleAxis(degrees, axis);
                    }
                    dragTranslation = Vector3.zero;
                }
                else
                {
                    if (free)
                    {
                        snapTargetVisible = false;
                        snapTargetIsNative = false;
                        snapPreviewTargets.Clear();
                        snapPreviewNative.Clear();
                    }
                    else snapTargetVisible = scene.TryFindEditorSnapTarget(
                        dragIds, mouse, meshSnapEnabled, snapPreviewTargets, snapPreviewNative,
                        out snapTargetWorld, out snapTargetIsNative,
                        snapTargetVisible ? snapTargetWorld : (Vector3?)null, snapTargetIsNative);
                    if (snapTargetVisible) target = snapTargetWorld;
                    dragTranslation = target - dragAnchorStartWorld;
                    dragRotation = Quaternion.identity;
                }
            }
            else if (dragHandle == GizmoHandleKind.Move)
            {
                float distance = Vector2.Dot(
                    mouse - dragStartMouse, dragScreenDirection) * dragWorldUnitsPerPixel;
                if (!input.GetKey(KeyCode.LeftShift) &&
                    !input.GetKey(KeyCode.RightShift))
                {
                    float step = translationStep;
                    distance = Mathf.Round(distance / step) * step;
                }
                dragTranslation = dragAxisWorld * distance;
                dragRotation = Quaternion.identity;
            }
            else if (dragHandle == GizmoHandleKind.MovePlane)
            {
                Ray ray = scene.Camera.ScreenPointToRay(mouse);
                if (!dragAnchorPlane.Raycast(ray, out float distance)) return;
                Vector3 delta = ray.GetPoint(distance) - dragPlaneStart;
                TrySelectionBasis(out _, out Quaternion orientation, null);
                Vector3 local = localSpace ? Quaternion.Inverse(orientation) * delta : delta;
                if (!input.GetKey(KeyCode.LeftShift) && !input.GetKey(KeyCode.RightShift))
                    local = new Vector3(Mathf.Round(local.x / translationStep) * translationStep,
                        Mathf.Round(local.y / translationStep) * translationStep,
                        Mathf.Round(local.z / translationStep) * translationStep);
                dragTranslation = localSpace ? orientation * local : local;
            }
            else if (dragHandle == GizmoHandleKind.Scale)
            {
                float minimum = 0f, maximum = float.PositiveInfinity;
                float referenceScale = 1f;
                foreach (BlueprintEditorPart part in document.Parts)
                {
                    if (!dragIds.Contains(part.StableId)) continue;
                    Vector3 scale = ToVector(part.Scale);
                    if (dragIds.Count == 1) referenceScale = scale.x;
                    minimum = Mathf.Max(minimum, 0.01f / Mathf.Min(scale.x, scale.y, scale.z));
                    maximum = Mathf.Min(maximum, 4f / Mathf.Max(scale.x, scale.y, scale.z));
                }
                float percent = mouse.y - dragStartMouse.y;
                if (!ShiftHeld)
                    percent = Mathf.Round(percent / view.ScaleStepPercent) * view.ScaleStepPercent;
                dragScale = Mathf.Clamp(1f + percent * 0.01f / referenceScale, minimum, maximum);
            }
            else
            {
                float degrees;
                if (dragUsesRotationPlane && TryRotationDirection(
                    mouse, dragPivot, dragAxisWorld, out Vector3 direction))
                {
                    degrees = Vector3.SignedAngle(
                        dragStartDirection, direction, dragAxisWorld);
                }
                else
                {
                    Vector3 screen = scene.Camera.WorldToScreenPoint(dragPivot);
                    float angle = Mathf.Atan2(
                        mouse.y - screen.y,
                        mouse.x - screen.x) * Mathf.Rad2Deg;
                    degrees = Mathf.DeltaAngle(dragStartAngle, angle);
                }
                if (!input.GetKey(KeyCode.LeftShift) &&
                    !input.GetKey(KeyCode.RightShift))
                {
                    float step = rotationStep;
                    degrees = Mathf.Round(degrees / step) * step;
                }
                dragTranslation = Vector3.zero;
                dragRotation = Quaternion.AngleAxis(degrees, dragAxisWorld);
            }
            if (previousTranslation == dragTranslation && previousRotation == dragRotation &&
                previousScale == dragScale) return;
            if (dragDuplicate)
            {
                var preview = CopyDocument(document);
                preview.DuplicateSelection(ToPoint(dragTranslation),
                    BuildWorksLocalization.Text("editor.generated.copy"));
                scene.ShowDuplicatePreview(NewParts(preview));
            }
            else
            {
                scene.PreviewTransform(
                    document, dragIds, dragTranslation, dragRotation, dragPivot, dragScale);
                RefreshModifierPreview();
            }
            UpdateGizmo();
        }

        private void CommitGizmoDrag()
        {
            if (dragHandle == GizmoHandleKind.Layout)
            {
                ResetGizmoDrag();
                RefreshModifierPreview();
                UpdateGizmo();
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.array_drag_ready", arrayCountX, arrayCountY));
                return;
            }
            Vector3 translation = dragTranslation;
            Quaternion rotation = dragRotation;
            Vector3 pivot = dragPivot;
            float scale = dragScale;
            bool duplicate = dragDuplicate;
            ResetGizmoDrag();
            if (translation.sqrMagnitude < 0.0000001f &&
                Quaternion.Angle(Quaternion.identity, rotation) < 0.001f && Mathf.Abs(scale - 1f) < 0.00001f)
            {
                scene.TrySync(document, out _);
                return;
            }
            if (duplicate)
                ApplyDocumentEdit(() => document.DuplicateSelection(ToPoint(translation),
                    BuildWorksLocalization.Text("editor.generated.copy")));
            else ApplyTransformDelta(translation, rotation, pivot, scale);
        }

        private void CancelGizmoDrag()
        {
            if (keyboardPreview)
            {
                selectedAnchorWorld = keyboardSavedSelectionPoint;
                pinnedAnchorWorld = keyboardSavedPin;
                anchorConstraintAxis = keyboardSavedConstraint;
                localSpace = keyboardSavedLocalSpace;
            }
            if (dragHandle == GizmoHandleKind.Layout) RestoreArrayDrag();
            ResetGizmoDrag();
            scene.TrySync(document, out string warning);
            RefreshModifierPreview();
            Bind(warning);
        }

        private void ResetGizmoDrag()
        {
            keyboardNumber = "";
            keyboardPreview = keyboardPaused = keyboardConfirmRequested = keyboardMoved = false;
            keyboardSourceChanged = false;
            keyboardAutoSource = keyboardHoveredSource = -1;
            view?.SetPreviewControls(false);
            scene?.ClearDuplicatePreview();
            dragDuplicate = false;
            dragHandle = GizmoHandleKind.None;
            dragAxis = GizmoAxis.None;
            dragAnchorPoint = -1;
            dragSourceAnchors = Array.Empty<Vector3>();
            snapTargetVisible = false;
            snapTargetWorld = Vector3.zero;
            snapTargetIsNative = false;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            dragIds.Clear();
            dragTranslation = Vector3.zero;
            dragRotation = Quaternion.identity;
            dragStartDirection = Vector3.zero;
            dragUsesRotationPlane = false;
            dragScale = 1f;
            dragMagneticMove = false;
            dragConstraintActive = false;
            dragConstraintCenter = Vector3.zero;
            dragConstraintRadius = 0f;
            dragWorldUnitsPerPixel = 0f;
            dragScreenDirection = Vector2.zero;
        }

        private void SetAnchorConstraint(GizmoAxis axis)
        {
            anchorConstraintAxis = anchorConstraintAxis == axis ? GizmoAxis.None : axis;
            if (dragAnchorPoint >= 0 && !dragMagneticMove)
            {
                TrySelectionBasis(out _, out Quaternion orientation, null);
                if (!ConfigureAnchorConstraint(orientation))
                {
                    CancelGizmoDrag();
                    view.SetStatus(BuildWorksLocalization.Text("editor.anchor_on_axis"));
                    return;
                }
                dragRotation = Quaternion.identity;
                scene.PreviewTransform(document, dragIds, Vector3.zero, Quaternion.identity, dragPivot);
                RefreshModifierPreview();
            }
            view.SetStatus(BuildWorksLocalization.Text(anchorConstraintAxis == GizmoAxis.None
                ? "editor.anchor_rotation_free"
                : "editor.anchor_rotation_axis", anchorConstraintAxis));
            UpdateGizmo();
        }

        private bool ConfigureAnchorConstraint(Quaternion orientation)
        {
            dragConstraintActive = !dragMagneticMove && anchorConstraintAxis != GizmoAxis.None;
            dragAnchorPlane = new Plane(scene.Camera.transform.forward, dragAnchorStartWorld);
            if (!dragConstraintActive) return true;
            dragAxisWorld = TransformGizmoView.AxisVector(anchorConstraintAxis, orientation, localSpace);
            dragConstraintCenter = dragPivot + dragAxisWorld * Vector3.Dot(dragMovingVector, dragAxisWorld);
            dragConstraintRadius = Vector3.Distance(dragAnchorStartWorld, dragConstraintCenter);
            if (dragConstraintRadius < 0.0001f) return false;
            dragAnchorPlane = new Plane(dragAxisWorld, dragConstraintCenter);
            return true;
        }

        private void ToggleSelectedAnchorPin()
        {
            if (!selectedAnchorWorld.HasValue) return;
            bool pinned = pinnedAnchorWorld.HasValue &&
                (pinnedAnchorWorld.Value - selectedAnchorWorld.Value).sqrMagnitude < 0.000001f;
            pinnedAnchorWorld = pinned ? (Vector3?)null : selectedAnchorWorld;
            pinnedAnchorPoint = -1;
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(pinned
                ? "editor.anchor_unpinned"
                : "editor.anchor_pinned"));
        }

        private BlueprintEditorDocument ModifierDocument()
        {
            if (!IsGizmoDragging || dragHandle == GizmoHandleKind.Layout) return document;
            var preview = CopyDocument(document);
            preview.ApplyTransformDelta(ToPoint(dragTranslation), ToRotation(dragRotation),
                ToPoint(dragPivot), dragScale);
            return preview;
        }

        private static BlueprintEditorDocument CopyDocument(BlueprintEditorDocument source)
        {
            var copy = new BlueprintEditorDocument(source.SourceBlueprintId, source.Name,
                source.Category, source.Parts, source.Groups, source.PrimaryPartId,
                source.PrimaryGroupId);
            foreach (string id in source.Selection) copy.ToggleSelection(id);
            return copy;
        }

        private IReadOnlyList<BlueprintEditorPart> NewParts(BlueprintEditorDocument preview)
        {
            var originalIds = new HashSet<string>();
            foreach (BlueprintEditorPart part in document.Parts) originalIds.Add(part.StableId);
            var added = new List<BlueprintEditorPart>();
            foreach (BlueprintEditorPart part in preview.Parts)
                if (!originalIds.Contains(part.StableId) && preview.IsEffectivelyVisible(part.StableId))
                    added.Add(part);
            return added;
        }

        private bool TryRotationDirection(
            Vector2 mouse,
            Vector3 pivot,
            Vector3 axis,
            out Vector3 direction)
        {
            Ray ray = scene.Camera.ScreenPointToRay(mouse);
            Plane plane = new Plane(axis, pivot);
            if (!plane.Raycast(ray, out float distance))
            {
                direction = Vector3.zero;
                return false;
            }
            direction = ray.GetPoint(distance) - pivot;
            if (direction.sqrMagnitude < 0.000001f) return false;
            direction.Normalize();
            return true;
        }

        private void SetActiveTool(BlueprintEditorTool tool)
        {
            if (IsGizmoDragging) CancelGizmoDrag();
            bool available = tool == BlueprintEditorTool.Select ||
                document.EditablePartSelectionCount > 0;
            if (!available)
            {
                ClearArrayState();
                ClearContourState();
                activeTool = BlueprintEditorTool.Select;
                view.SetTool(activeTool);
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.select_available_first"), error: true);
                return;
            }
            if (tool != BlueprintEditorTool.Array) ClearArrayState();
            else if (activeTool != BlueprintEditorTool.Array) ClearArrayState();
            if (tool != BlueprintEditorTool.Contour) ClearContourState();
            else if (activeTool != BlueprintEditorTool.Contour) ClearContourState();
            activeTool = tool;
            if (tool == BlueprintEditorTool.Transform)
            { gizmoFamily = GizmoFamily.Combined; view.SetGizmoFamily(gizmoFamily); }
            selectionPending = false;
            view.SetTool(tool);
            UpdateGizmo();
            if (tool == BlueprintEditorTool.Contour)
            {
                RefreshContourPanel();
                view.SetStatus(BuildWorksLocalization.Text("editor.contour_tool_hint"));
            }
            else if (tool == BlueprintEditorTool.Array)
            {
                PreviewArray();
                view.SetStatus(BuildWorksLocalization.Text("editor.array_tool_hint"));
            }
            else if (tool == BlueprintEditorTool.Transform)
                view.SetStatus(BuildWorksLocalization.Text("editor.transform_tool_hint"));
            else view.SetStatus(BuildWorksLocalization.Text("editor.select_tool_hint"));
        }

        private void UpdateGizmo()
        {
            if (scene == null) return;
            scene.GizmoFamily = activeTool == BlueprintEditorTool.Array ? GizmoFamily.Combined : gizmoFamily;
            scene.GizmoMouse = input.MousePosition;
            if (placementItem != null) return;
            scene.SetTemporarySelectionHighlight(ShiftHeld && !IsGizmoDragging, document);
            if (activeTool == BlueprintEditorTool.Contour)
            {
                if (contourSupportIds.Count == 0 && document.EditablePartSelectionCount > 0 &&
                    !IsGizmoDragging && !cameraDragging &&
                    !rightCameraTracking && !input.GetMouseButton(1) && !view.HasModal &&
                    !view.HasCatalog && !view.HasOutlinerMenu && !view.HasOutlinerContextMenu &&
                    !view.HasViewportSettings && !view.IsTextInputFocused &&
                    view.ViewportScreenRect().Contains(input.MousePosition) &&
                    !view.IsViewportControlHit(input.MousePosition) &&
                    scene.TryFindContourSupports(document, input.MousePosition, out _,
                        out List<Vector3> hoverGuide, out bool hoverClosed, out _))
                    scene.ShowContourGuide(hoverGuide, hoverClosed);
                else scene.ShowContourGuide(contourGuidePoints, contourClosed);
            }
            if (TrySelectionPivot(out Vector3 pivot, out Quaternion orientation, gizmoIds))
            {
                UsePrimaryGizmoFrame(gizmoIds);
                if (IsGizmoDragging)
                {
                    pivot = dragPivot + dragTranslation;
                    orientation = dragRotation * orientation;
                }
                if (IsGizmoDragging)
                {
                    gizmoAnchors = new Vector3[dragSourceAnchors.Length];
                    for (int index = 0; index < gizmoAnchors.Length; ++index)
                        gizmoAnchors[index] = dragPivot + dragRotation *
                            ((dragSourceAnchors[index] - dragPivot) * dragScale) + dragTranslation;
                }
                else if (!scene.TryGetGizmoAnchors(
                    gizmoIds, out gizmoAnchors, out gizmoNativeAnchorStart))
                {
                    gizmoAnchors = Array.Empty<Vector3>();
                    gizmoNativeAnchorStart = AnchorAdjustment.SelectableAnchorCount;
                }
                if (!IsGizmoDragging)
                {
                    gizmoNativeAnchorEnd = gizmoAnchors.Length;
                    pinnedAnchorPoint = -1;
                    if (pinnedAnchorWorld.HasValue)
                    {
                        pinnedAnchorPoint = gizmoAnchors.Length;
                        Array.Resize(ref gizmoAnchors, gizmoAnchors.Length + 1);
                        gizmoAnchors[pinnedAnchorPoint] = pinnedAnchorWorld.Value;
                    }
                    selectedAnchorPoint = -1;
                    if (selectedAnchorWorld.HasValue)
                    {
                        for (int index = gizmoAnchors.Length - 1; index >= 0; --index)
                            if ((gizmoAnchors[index] - selectedAnchorWorld.Value).sqrMagnitude < 0.000001f)
                            { selectedAnchorPoint = index; break; }
                        if (selectedAnchorPoint < 0)
                        {
                            selectedAnchorPoint = gizmoAnchors.Length;
                            Array.Resize(ref gizmoAnchors, gizmoAnchors.Length + 1);
                            gizmoAnchors[selectedAnchorPoint] = selectedAnchorWorld.Value;
                        }
                    }
                }
                scene.ShowGizmo(
                    SelectionGizmoTool,
                    pivot,
                    orientation,
                    localSpace,
                    dragHandle,
                    dragAxis,
                    gizmoAnchors,
                    keyboardPreview && keyboardSurface ? keyboardHoveredSource >= 0 ? keyboardHoveredSource :
                        keyboardSourceIndex >= 0 ? keyboardSourceIndex : keyboardAutoSource :
                        dragAnchorPoint >= 0 ? dragAnchorPoint : selectedAnchorPoint,
                    gizmoNativeAnchorStart,
                    showAllAnchors,
                    snapTargetVisible,
                    snapTargetWorld,
                    snapTargetIsNative,
                    pinnedAnchorPoint,
                    anchorConstraintAxis,
                    anchorConstraintAxis != GizmoAxis.None && (dragConstraintActive || pinnedAnchorWorld.HasValue),
                    dragConstraintActive ? dragConstraintCenter : pinnedAnchorWorld ?? pivot,
                    dragConstraintActive ? dragAxisWorld
                        : TransformGizmoView.AxisVector(anchorConstraintAxis, orientation, localSpace),
                    dragConstraintActive ? dragConstraintRadius : 0f);
                scene.ShowSnapCandidates(
                    dragAnchorPoint >= 0 || keyboardSurface && keyboardPreview ? snapPreviewTargets : Array.Empty<Vector3>(),
                    dragAnchorPoint >= 0 || keyboardSurface && keyboardPreview ? snapPreviewNative : Array.Empty<bool>());
            }
            else scene.ShowGizmo(
                BlueprintEditorTool.Select,
                Vector3.zero,
                Quaternion.identity,
                localSpace: false,
                GizmoHandleKind.None,
                GizmoAxis.None,
                Array.Empty<Vector3>(),
                -1,
                AnchorAdjustment.SelectableAnchorCount,
                false,
                false,
                Vector3.zero,
                false);
            UpdateOperationMetrics();
            view.SetAnchorState(selectedAnchorWorld.HasValue,
                selectedAnchorWorld.HasValue && pinnedAnchorWorld.HasValue &&
                (selectedAnchorWorld.Value - pinnedAnchorWorld.Value).sqrMagnitude < 0.000001f,
                anchorConstraintAxis);
        }

        private void UpdateOperationMetrics()
        {
            if (view == null || document == null) return;
            int selectedCount = document.EditablePartSelectionCount;
            float scale = dragScale;
            if (selectedCount == 1)
                foreach (BlueprintEditorPart part in document.Parts)
                    if (document.IsPartSelected(part.StableId) && document.IsEffectivelyVisible(part.StableId) &&
                        !document.IsEffectivelyLocked(part.StableId)) { scale *= (float)part.Scale.X; break; }
            view.SetOperationMetrics(selectedCount,
                dragTranslation, dragRotation.eulerAngles, scale,
                arrayPreviewCount + contourPreviewCount, document.Parts.Count);
        }

        private bool IsGizmoDragging =>
            dragHandle != GizmoHandleKind.None || dragAnchorPoint >= 0;

        private bool ShiftHeld => input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift);
        private bool IgnorePrimaryFrame => input.GetKey(KeyCode.N);
        private BlueprintEditorTool SelectionGizmoTool => activeTool == BlueprintEditorTool.Select
            ? BlueprintEditorTool.Transform : activeTool;

        private void SetSelectionTool() => SetActiveTool(activeTool != BlueprintEditorTool.Select &&
            document.EditablePartSelectionCount > 0 ? BlueprintEditorTool.Transform : BlueprintEditorTool.Select);

        private bool TrySelectionPivot(
            out Vector3 pivot,
            out Quaternion orientation,
            List<string> ids)
        {
            List<string> pivotIds = ids;
            if (pivotMode == BlueprintEditorPivotMode.Bounds && pivotIds == null)
                pivotIds = new List<string>();
            if (!TrySelectionBasis(out pivot, out orientation, pivotIds)) return false;
            BlueprintEditorPart activePart = ActiveEditablePart();
            if (pivotMode == BlueprintEditorPivotMode.ActiveObject && activePart != null)
                pivot = ToVector(activePart.Position);
            else if (pivotMode == BlueprintEditorPivotMode.Bounds &&
                scene.TryGetBounds(pivotIds, out Bounds bounds))
            {
                int column = boundsPivotIndex % 3 - 1;
                int row = 1 - boundsPivotIndex / 3;
                Vector3 right = scene.Camera.transform.right;
                Vector3 up = scene.Camera.transform.up;
                Vector3 extents = bounds.extents;
                float horizontal = Mathf.Abs(right.x) * extents.x +
                    Mathf.Abs(right.y) * extents.y + Mathf.Abs(right.z) * extents.z;
                float vertical = Mathf.Abs(up.x) * extents.x +
                    Mathf.Abs(up.y) * extents.y + Mathf.Abs(up.z) * extents.z;
                pivot = bounds.center + right * (column * horizontal) +
                    up * (row * vertical);
            }
            BlueprintEditorPart primary = IgnorePrimaryFrame ? null : SelectedPrimaryPart();
            if (primary != null)
            {
                pivot = ToVector(primary.Position);
                orientation = ToQuaternion(primary.Rotation);
            }
            return true;
        }

        private BlueprintEditorPart SelectedPrimaryPart()
        {
            if (document.Selection.Count > 1)
            {
                BlueprintEditorPart worldPrimary = SelectedWorldPrimaryPart();
                if (worldPrimary != null) return worldPrimary;
            }
            BlueprintEditorGroup activeGroup = null;
            foreach (BlueprintEditorGroup group in document.Groups)
                if (group.StableId == document.ActiveNodeId &&
                    Contains(document.Selection, group.StableId))
                {
                    activeGroup = group;
                    break;
                }
            if (activeGroup != null)
            {
                if (string.IsNullOrEmpty(activeGroup.PivotPartId)) return null;
                foreach (BlueprintEditorPart part in document.Parts)
                    if (part.StableId == activeGroup.PivotPartId &&
                        document.IsEffectivelyVisible(part.StableId) &&
                        !document.IsEffectivelyLocked(part.StableId)) return part;
                return null;
            }
            return SelectedWorldPrimaryPart();
        }

        private BlueprintEditorPart SelectedWorldPrimaryPart()
        {
            if (string.IsNullOrEmpty(document.PrimaryPartId) ||
                !document.IsPartSelected(document.PrimaryPartId) ||
                !document.IsEffectivelyVisible(document.PrimaryPartId) ||
                document.IsEffectivelyLocked(document.PrimaryPartId)) return null;
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == document.PrimaryPartId) return part;
            return null;
        }

        private void UsePrimaryGizmoFrame(List<string> ids)
        {
            if (IgnorePrimaryFrame) return;
            BlueprintEditorPart primary = SelectedPrimaryPart();
            if (primary != null)
            {
                ids.Clear();
                ids.Add(primary.StableId);
                return;
            }
            if (string.IsNullOrEmpty(document.PrimaryGroupId) ||
                !Contains(document.Selection, document.PrimaryGroupId)) return;
            ids.RemoveAll(stableId =>
                !document.IsPartInGroup(stableId, document.PrimaryGroupId));
        }

        private bool TrySelectionBasis(
            out Vector3 pivot,
            out Quaternion orientation,
            List<string> ids)
        {
            pivot = Vector3.zero;
            orientation = Quaternion.identity;
            List<string> selectedIds = ids ?? new List<string>();
            selectedIds.Clear();
            int count = 0;
            BlueprintEditorPart activePart = null;
            foreach (BlueprintEditorPart part in document.Parts)
            {
                bool selected = document.IsPartSelected(part.StableId);
                if (!selected || !document.IsEffectivelyVisible(part.StableId) ||
                    document.IsEffectivelyLocked(part.StableId)) continue;
                pivot += ToVector(part.Position);
                if (part.StableId == document.ActiveNodeId) activePart = part;
                selectedIds.Add(part.StableId);
                ++count;
            }
            if (count == 0) return false;
            pivot = scene.TryGetBounds(selectedIds, out Bounds bounds)
                ? bounds.center
                : pivot / count;
            if (activePart != null) orientation = ToQuaternion(activePart.Rotation);
            return true;
        }

        private BlueprintEditorPart ActiveEditablePart()
        {
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == document.ActiveNodeId &&
                    document.IsEffectivelyVisible(part.StableId) &&
                    !document.IsEffectivelyLocked(part.StableId)) return part;
            return null;
        }

        private void SelectViewport(Vector2 start, Vector2 end)
        {
            customPivot = false;
            bool additive = input.GetKey(KeyCode.LeftControl) ||
                input.GetKey(KeyCode.RightControl) ||
                input.GetKey(KeyCode.LeftShift) || input.GetKey(KeyCode.RightShift);
            if ((end - start).sqrMagnitude <= 36f)
            {
                if (scene.TryPick(end, out string stableId))
                {
                    if (additive) document.ToggleSelection(stableId);
                    else document.SelectOnly(stableId);
                }
                else if (!additive) document.ClearSelection();
            }
            else
            {
                Rect rect = Rect.MinMaxRect(
                    Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y),
                    Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
                if (!additive) document.ClearSelection();
                foreach (string stableId in scene.BoxSelect(rect))
                    if (!Contains(document.Selection, stableId)) document.ToggleSelection(stableId);
            }
            RefreshSelection();
            SetSelectionTool();
        }

        private void SelectNode(string stableId, bool toggle, bool range)
        {
            if (keyboardPreview) return;
            if (activeTool == BlueprintEditorTool.Array) ClearArrayState();
            if (activeTool == BlueprintEditorTool.Contour) ClearContourState();
            customPivot = false;
            if (range) document.SelectRange(stableId);
            else if (toggle) document.ToggleSelection(stableId);
            else document.SelectOnly(stableId);
            RefreshSelection();
            SetSelectionTool();
        }

        private void RefreshSelection()
        {
            selectedAnchorPoint = -1;
            selectedAnchorWorld = null;
            pinnedAnchorPoint = -1;
            pinnedAnchorWorld = null;
            if (!scene.TrySync(document, out string warning))
            {
                view.SetStatus(warning, error: true);
                return;
            }
            Bind(warning);
        }

        private bool ApplyDocumentEdit(Func<bool> edit, bool preserveModifier = false)
        {
            if (!IsEditing || keyboardPreview) return false;
            if (!preserveModifier)
            {
                selectedAnchorPoint = -1;
                selectedAnchorWorld = null;
                pinnedAnchorPoint = -1;
                pinnedAnchorWorld = null;
            }
            if (!preserveModifier && activeTool == BlueprintEditorTool.Array &&
                arrayPreviewCount > 0)
                ClearArrayState();
            if (!preserveModifier && activeTool == BlueprintEditorTool.Contour &&
                contourSupportIds.Count > 0)
                ClearContourState();
            try
            {
                if (!edit()) return false;
                if (scene.TrySync(document, out string warning))
                {
                    document.AcceptLastEdit();
                    Bind(warning);
                    return true;
                }
                document.RollbackLastEdit();
                if (!scene.TrySync(document, out string rollbackWarning))
                {
                    view.SetStatus(rollbackWarning, error: true);
                    return false;
                }
                Bind(null);
                view.SetStatus(warning, error: true);
            }
            catch (Exception exception)
            {
                logError("BuildWorks blueprint editor command failed: " + exception);
                view.SetStatus(exception.Message, error: true);
            }
            return false;
        }

        private void ApplyHistory(Func<bool> change, Func<bool> rollback)
        {
            if (keyboardPreview) return;
            if (activeTool == BlueprintEditorTool.Array) ClearArrayState();
            if (activeTool == BlueprintEditorTool.Contour) ClearContourState();
            if (!IsEditing || !change()) return;
            if (!scene.TrySync(document, out string warning))
            {
                rollback();
                scene.TrySync(document, out string rollbackWarning);
                Bind(null);
                if (!string.IsNullOrEmpty(rollbackWarning)) warning += " " + rollbackWarning;
                view.SetStatus(warning, error: true);
                return;
            }
            pinnedAnchorWorld = null;
            pinnedAnchorPoint = -1;
            selectedAnchorPoint = -1;
            selectedAnchorWorld = null;
            Bind(warning);
        }

        private void RequestCatalog()
        {
            if (!IsEditing || keyboardPreview) return;
            try
            {
                scene.HideGizmo();
                IReadOnlyList<BlueprintEditorCatalogItem> items = catalogItems();
                if (items == null || items.Count == 0)
                {
                    view.SetStatus(BuildWorksLocalization.Text(
                        "editor.catalog_empty"), error: true);
                    return;
                }
                view.ShowCatalog(items);
                CatalogRequested?.Invoke();
            }
            catch (Exception exception)
            {
                logError("BuildWorks blueprint editor catalog failed: " + exception);
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.catalog_open_failed"), error: true);
            }
        }

        private void BeginPartPlacement(BlueprintEditorCatalogItem item, bool snap)
        {
            if (!IsEditing || item == null || keyboardPreview) return;
            if (IsGizmoDragging) CancelGizmoDrag();
            ClearArrayState();
            ClearContourState();
            scene.HidePlacementPreview();
            scene.ClearBlueprintPlacementPreview();
            placementBlueprint = item.IsBlueprint ? DocumentFromBlueprint(item.Blueprint) : null;
            view.HideCatalog();
            placementItem = item;
            placementYaw = 0f;
            placementSnap = snap;
            placementManualSnapPoint = snap && placementBlueprint == null
                ? Math.Max(-1, initialPlacementSnapPoint())
                : -1;
            activeTool = BlueprintEditorTool.Select;
            view.SetTool(activeTool);
            scene.ShowGizmo(
                BlueprintEditorTool.Select,
                Vector3.zero,
                Quaternion.identity,
                localSpace: false,
                GizmoHandleKind.None,
                GizmoAxis.None,
                Array.Empty<Vector3>(),
                -1,
                AnchorAdjustment.SelectableAnchorCount,
                false,
                false,
                Vector3.zero,
                false);
            SetPlacementStatus();
        }

        private void CyclePlacementSnapPoint(int direction)
        {
            int count = scene.PlacementSourceSnapPointCount;
            if (!placementSnap || count <= 0)
            {
                placementManualSnapPoint = -1;
                view.SetTransientStatus(placementSnap
                    ? BuildWorksLocalization.Text("editor.part_has_no_snaps")
                    : BuildWorksLocalization.Text("editor.snap_free"));
                return;
            }
            placementManualSnapPoint += direction;
            if (placementManualSnapPoint < -1) placementManualSnapPoint = count - 1;
            else if (placementManualSnapPoint >= count) placementManualSnapPoint = -1;
            view.SetTransientStatus(BuildWorksLocalization.Text(
                "editor.snap_selected", PlacementSnapLabel(count)));
            RefreshContextHints();
        }

        private string PlacementSnapLabel(int count) => !placementSnap
            ? BuildWorksLocalization.Text("editor.snap_mode_free")
            : placementManualSnapPoint < 0 || count <= 0
                ? BuildWorksLocalization.Text("editor.snap_mode_auto")
                : (placementManualSnapPoint + 1) + "/" + count +
                    (string.IsNullOrEmpty(scene.PlacementSnapPointLabel(placementManualSnapPoint))
                        ? string.Empty
                        : " · " + scene.PlacementSnapPointLabel(placementManualSnapPoint));

        private void SetPlacementStatus()
        {
            if (placementItem == null) return;
            view.SetStatus(BuildWorksLocalization.Text(
                "editor.part_placement_started", placementItem.DisplayName));
        }

        private static BlueprintEditorDocument DocumentFromBlueprint(CompositeBlueprintStore.Blueprint source)
        {
            var groups = new List<BlueprintEditorGroup>();
            foreach (CompositeBlueprintStore.Group group in source.groups ?? new List<CompositeBlueprintStore.Group>())
                groups.Add(new BlueprintEditorGroup(group.stableId, group.name, group.editorVisible,
                    group.editorLocked, group.parentGroupId, group.pivotPartId));
            var parts = new List<BlueprintEditorPart>();
            foreach (CompositeBlueprintStore.Part part in source.parts ?? new List<CompositeBlueprintStore.Part>())
                parts.Add(new BlueprintEditorPart(part.stableId, part.prefabName, part.displayName,
                    ToPoint(part.position.ToVector3()), ToRotation(part.rotation.ToQuaternion()), part.parentGroupId,
                    part.editorVisible, part.editorLocked, ToPoint(part.scale.ToVector3())));
            return new BlueprintEditorDocument(source.id, source.name,
                source.category ?? CompositeBlueprintStore.DefaultCategory, parts, groups,
                source.primaryPartId, source.primaryGroupId);
        }

        private void ApplyInspector(
            string stableId,
            string displayName,
            Vector3 position,
            Vector3 euler,
            float uniformScale,
            bool delta)
        {
            if (delta)
            {
                if (!TrySelectionPivot(out Vector3 pivot, out Quaternion orientation, null))
                    return;
                Vector3 translation = localSpace ? orientation * position : position;
                Quaternion rotation = Quaternion.Euler(euler);
                if (localSpace)
                    rotation = orientation * rotation * Quaternion.Inverse(orientation);
                ApplyTransformDelta(translation, rotation, pivot, uniformScale);
                return;
            }
            ApplyDocumentEdit(() => document.SetPartProperties(
                stableId,
                displayName,
                ToPoint(position),
                ToRotation(Quaternion.Euler(euler)),
                ToPoint(Vector3.one * uniformScale)));
        }

        private void HandlePartPlacement(Vector2 mouse, bool inside)
        {
            if (!inside)
            {
                scene.HidePlacementPreview();
                scene.ClearBlueprintPlacementPreview();
                scene.HidePlacementTarget();
                return;
            }
            if (!scene.TryPlacementSurface(mouse, out Vector3 point, out Vector3 surfaceNormal))
            {
                scene.HidePlacementPreview();
                scene.ClearBlueprintPlacementPreview();
                scene.HidePlacementTarget();
                return;
            }
            placementPosition = placementSnap
                ? new Vector3(
                    Mathf.Round(point.x / translationStep) * translationStep,
                    point.y,
                    Mathf.Round(point.z / translationStep) * translationStep)
                : point;
            if (placementBlueprint != null)
            {
                try
                {
                    var preview = CopyDocument(document);
                    preview.InsertBlueprint(placementBlueprint, ToPoint(placementPosition),
                        ToRotation(Quaternion.Euler(0f, placementYaw, 0f)));
                    scene.ShowBlueprintPlacementPreview(
                        NewParts(preview), placementPosition, out Vector3 offset,
                        snap: placementSnap, manualSnapPoint: placementManualSnapPoint);
                    placementPosition += offset;
                }
                catch (Exception exception)
                {
                    scene.ClearBlueprintPlacementPreview();
                    scene.HidePlacementTarget();
                    view.SetStatus(exception.Message, error: true);
                    return;
                }
            }
            else if (!scene.ShowPlacementPreview(
                placementItem.PrefabName,
                placementPosition,
                surfaceNormal,
                Quaternion.Euler(0f, placementYaw, 0f),
                placementSnap,
                out placementPosition,
                out string warning,
                placementManualSnapPoint))
            {
                scene.HidePlacementTarget();
                view.SetStatus(warning, error: true);
                return;
            }
            scene.ShowPlacementTarget(scene.PlacementSnapTarget ?? point, scene.PlacementSnapTarget.HasValue);
            scene.ShowSnapCandidates(
                scene.PlacementSnapPreviewTargets,
                scene.PlacementSnapPreviewNative);
            if (!input.GetMouseButtonDown(0) || cameraDragging || rightCameraTracking) return;
            BlueprintEditorCatalogItem item = placementItem;
            bool added = placementBlueprint != null
                ? ApplyDocumentEdit(() => document.InsertBlueprint(placementBlueprint, ToPoint(placementPosition),
                    ToRotation(Quaternion.Euler(0f, placementYaw, 0f))))
                : ApplyDocumentEdit(() => document.AddPart(new BlueprintEditorPart(
                Guid.NewGuid().ToString("N"),
                item.PrefabName,
                item.DisplayName,
                ToPoint(placementPosition),
                ToRotation(Quaternion.Euler(0f, placementYaw, 0f)))));
            if (!added) return;
            view.SetStatus(BuildWorksLocalization.Text("editor.part_added"));
        }

        private void DeleteViewportPart(string stableId)
        {
            if (string.IsNullOrEmpty(stableId) ||
                !document.IsEffectivelyVisible(stableId) ||
                document.IsEffectivelyLocked(stableId)) return;
            document.SelectOnly(stableId);
            ApplyDocumentEdit(() => document.DeleteSelection(false));
        }

        private void CancelPartPlacement()
        {
            placementItem = null;
            placementBlueprint = null;
            scene?.HidePlacementPreview();
            scene?.ClearBlueprintPlacementPreview();
            scene?.HidePlacementTarget();
            SetSelectionTool();
            Bind(BuildWorksLocalization.Text("editor.part_placement_finished"));
        }

        private void ChangeArrayParameters(int countX, int countY, float rise,
            float yaw, float pitch, float roll, float scaleStep)
        {
            arrayCountX = countX;
            arrayCountY = countY;
            arrayRise = rise;
            arrayRotation = yaw;
            arrayPitch = pitch;
            arrayRoll = roll;
            arrayScaleStepX = scaleStep;
            if (arrayDistribution == RepeatDistributionMode.Fit) RecalculateArraySteps();
            PreviewArray();
        }

        private void ArrayFrame(out Vector3 pivot, out Vector3 stepX, out Vector3 stepY, out Vector3 axis,
            out float degrees, out Vector3 back, out Vector3 front)
        {
            TrySelectionPivot(out pivot, out Quaternion orientation, gizmoIds);
            UsePrimaryGizmoFrame(gizmoIds);
            stepX = arrayStepX;
            stepY = arrayStepY;
            if (IsGizmoDragging && dragHandle != GizmoHandleKind.Layout)
            {
                pivot = dragPivot + dragRotation * ((pivot - dragPivot) * dragScale) + dragTranslation;
                orientation = dragRotation * orientation;
                if (localSpace) { stepX = dragRotation * stepX; stepY = dragRotation * stepY; }
            }
            TransformGizmoView.RepeatStepAxisAngle(arrayPrimaryAxis, stepX, orientation, localSpace,
                arrayRotation, arrayPitch, arrayRoll, out axis, out degrees);
            back = -stepX * .5f;
            front = stepX * .5f;
            if (!scene.TryGetGizmoAnchors(gizmoIds, out Vector3[] anchors, out _) || anchors.Length < 8) return;
            Vector3 direction = stepX.normalized;
            Vector3 center = Vector3.zero;
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            for (int index = 0; index < 8; ++index)
            {
                center += anchors[index] * .125f;
                float projection = Vector3.Dot(anchors[index] - pivot, direction);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            if (maximum - minimum < .0001f) return;
            Vector3 offset = center - pivot;
            float centerProjection = Vector3.Dot(offset, direction);
            back = offset + direction * (minimum - centerProjection);
            front = offset + direction * (maximum - centerProjection);
        }

        private void PreviewArray()
        {
            if (!IsEditing || activeTool != BlueprintEditorTool.Array) return;
            string stepLabel = arrayDistribution == RepeatDistributionMode.Fit
                ? BuildWorksLocalization.Text("editor.array_fit")
                : arrayDistribution == RepeatDistributionMode.Exact
                    ? BuildWorksLocalization.Text(
                        "editor.array_exact_step", ArrayExactSteps[arrayExactIndex].ToString("0.##"))
                    : ArrayGaps[arraySpacingIndex] == 0f
                        ? BuildWorksLocalization.Text("editor.array_no_gap")
                        : BuildWorksLocalization.Text(
                            "editor.array_gap", ArrayGaps[arraySpacingIndex].ToString("0.##"));
            string stepInfo = BuildWorksLocalization.Text(
                "editor.array_step_info",
                arrayStepX.magnitude.ToString("0.###"),
                arrayCountY > 1 ? arrayStepY.magnitude.ToString("0.###") : "—");
            view.SetArrayParameters(arrayCountX, arrayCountY, arrayDistribution, stepLabel,
                arraySymmetric, arrayPrimaryAxis != GizmoAxis.None, stepInfo,
                translationStep, rotationStep);
            if (arrayPrimaryAxis == GizmoAxis.None)
            {
                arrayPreviewCount = 0;
                scene.ClearArrayPreview();
                view.SetArrayState(0);
                return;
            }
            try
            {
                ArrayFrame(out Vector3 pivot, out Vector3 stepX, out Vector3 stepY, out Vector3 axis,
                    out float degrees, out Vector3 back, out Vector3 front);
                IReadOnlyList<BlueprintEditorPart> preview = ModifierDocument().PreviewArray(
                    arrayCountX, arrayCountY, ToPoint(stepX), ToPoint(stepY), ToPoint(axis),
                    degrees, arrayRise, arraySymmetric, arrayScaleStepX, ToPoint(back),
                    ToPoint(front), ToPoint(pivot),
                    BuildWorksLocalization.Text("editor.generated.array"));
                arrayPreviewCount = preview.Count;
                scene.ShowArrayPreview(preview);
                view.SetArrayState(preview.Count);
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.array_preview", arrayCountX, arrayCountY));
            }
            catch (Exception exception)
            {
                arrayPreviewCount = 0;
                scene.ClearArrayPreview();
                view.SetArrayState(0, BuildWorksLocalization.Text(
                    "editor.array_rejected", exception.Message));
            }
        }

        private void ApplyArray()
        {
            if (activeTool != BlueprintEditorTool.Array || arrayPreviewCount == 0)
            {
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.array_preview_required"), error: true);
                return;
            }
            int copies = arrayPreviewCount;
            int countX = arrayCountX, countY = arrayCountY;
            float rise = arrayRise, scaleStep = arrayScaleStepX;
            bool symmetric = arraySymmetric;
            ArrayFrame(out Vector3 pivot, out Vector3 stepX, out Vector3 stepY, out Vector3 axis,
                out float degrees, out Vector3 back, out Vector3 front);
            if (!ApplyDocumentEdit(() => document.ApplyArray(countX, countY,
                ToPoint(stepX), ToPoint(stepY), ToPoint(axis), degrees, rise,
                symmetric, scaleStep, ToPoint(back), ToPoint(front),
                ToPoint(pivot), BuildWorksLocalization.Text("editor.generated.array")))) return;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus(BuildWorksLocalization.Text("editor.array_applied", copies));
        }

        private void CancelArray()
        {
            ClearArrayState();
            SetActiveTool(document.EditablePartSelectionCount > 0
                ? BlueprintEditorTool.Transform
                : BlueprintEditorTool.Select);
            view.SetStatus(BuildWorksLocalization.Text("editor.array_cancelled"));
        }

        private void ClearArrayState()
        {
            arrayCountX = 2;
            arrayCountY = 1;
            arrayStepX = Vector3.right;
            arrayStepY = Vector3.forward;
            arrayRotation = 0f;
            arrayScaleStepX = 0f;
            arrayRise = 0f;
            arrayPitch = 0f;
            arrayRoll = 0f;
            arraySymmetric = false;
            arrayDistribution = RepeatDistributionMode.Pack;
            arraySpacingIndex = 0;
            arrayExactIndex = 0;
            arrayPrimaryAxis = GizmoAxis.None;
            arrayLastSecond = false;
            arrayDistanceX = arrayDistanceY = 0f;
            arrayPreviewCount = 0;
            scene?.ClearArrayPreview();
            view?.SetArrayState(0);
        }

        private float ArrayStepLength(Vector3 direction)
        {
            TrySelectionBasis(out _, out _, gizmoIds);
            if (!scene.TryGetGizmoAnchors(gizmoIds, out Vector3[] points, out int nativeStart)) return 1f;
            float span = ProjectedAnchorSpan(points, direction, nativeStart, points.Length);
            if (span < 0.0001f) span = ProjectedAnchorSpan(points, direction, 0, Math.Min(8, points.Length));
            return span > 0.0001f ? span : 1f;
        }

        private static float ProjectedAnchorSpan(Vector3[] points, Vector3 direction, int start, int end)
        {
            if (end <= start) return 0f;
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            for (int index = start; index < end; ++index)
            {
                float projection = Vector3.Dot(points[index], direction);
                minimum = Mathf.Min(minimum, projection);
                maximum = Mathf.Max(maximum, projection);
            }
            return maximum - minimum;
        }

        private void PreviewArrayDrag(Vector2 mouse)
        {
            float distance = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection) * dragWorldUnitsPerPixel;
            if (Mathf.Abs(distance) < (arrayDistribution == RepeatDistributionMode.Fit ? 0.05f :
                dragArraySecond ? dragArrayStepLength * 0.5f : Mathf.Min(0.05f, dragArrayStepLength * 0.25f)))
            {
                RestoreArrayDrag();
                RefreshModifierPreview();
                return;
            }
            int maximumInstances = 1 + (BlueprintEditorDocument.MaximumParts - document.Parts.Count) /
                Math.Max(1, document.EditablePartSelectionCount);
            int otherCount = dragArraySecond ? arrayCountX : arrayCountY;
            int maximumCount = maximumInstances / Math.Max(1, otherCount);
            if (maximumCount < 2)
            {
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.array_limit_direction"), error: true);
                return;
            }
            int count = arrayDistribution == RepeatDistributionMode.Fit
                ? Mathf.Clamp(dragArraySecond ? arrayCountY : arrayCountX, 2, maximumCount)
                : Mathf.Clamp(1 + Mathf.RoundToInt(Mathf.Abs(distance) / dragArrayStepLength), 2, maximumCount);
            float length = arrayDistribution == RepeatDistributionMode.Fit
                ? Mathf.Abs(distance) / (count - 1) : dragArrayStepLength;
            Vector3 step = dragAxisWorld * (length * Mathf.Sign(distance));
            arrayLastSecond = dragArraySecond;
            if (dragArraySecond)
            {
                arrayCountY = count;
                arrayStepY = step;
                arrayDistanceY = Mathf.Abs(distance);
            }
            else
            {
                arrayPrimaryAxis = dragAxis;
                arrayCountX = count;
                arrayStepX = step;
                arrayDistanceX = Mathf.Abs(distance);
            }
            RefreshModifierPreview();
            UpdateGizmo();
        }

        private void RestoreArrayDrag()
        {
            arrayPrimaryAxis = dragStartArrayPrimaryAxis;
            arrayCountX = dragStartArrayCountX;
            arrayCountY = dragStartArrayCountY;
            arrayStepX = dragStartArrayStepX;
            arrayStepY = dragStartArrayStepY;
            arrayLastSecond = dragStartArrayLastSecond;
            arrayDistanceX = dragStartArrayDistanceX;
            arrayDistanceY = dragStartArrayDistanceY;
        }

        private float ArrayConfiguredStep(Vector3 direction) =>
            arrayDistribution == RepeatDistributionMode.Exact ? ArrayExactSteps[arrayExactIndex] :
                ArrayStepLength(direction) + ArrayGaps[arraySpacingIndex];

        private void RecalculateArraySteps()
        {
            if (arrayPrimaryAxis == GizmoAxis.None) return;
            arrayStepX = arrayStepX.normalized * (arrayDistribution == RepeatDistributionMode.Fit
                ? arrayDistanceX / Mathf.Max(1, arrayCountX - 1) : ArrayConfiguredStep(arrayStepX.normalized));
            if (arrayDistanceY > 0f)
                arrayStepY = arrayStepY.normalized * (arrayDistribution == RepeatDistributionMode.Fit
                    ? arrayDistanceY / Mathf.Max(1, arrayCountY - 1) : ArrayConfiguredStep(arrayStepY.normalized));
        }

        private void AdjustArrayCount(int delta)
        {
            int budget = 1 + (BlueprintEditorDocument.MaximumParts - document.Parts.Count) /
                Math.Max(1, document.EditablePartSelectionCount);
            int other = arrayLastSecond ? arrayCountX : arrayCountY;
            int maximum = Math.Max(1, budget / Math.Max(1, other));
            if (arrayLastSecond) arrayCountY = Mathf.Clamp(arrayCountY + delta, 1, maximum);
            else arrayCountX = Mathf.Clamp(arrayCountX + delta, 1, maximum);
            if (arrayDistribution == RepeatDistributionMode.Fit) RecalculateArraySteps();
            PreviewArray();
        }

        private void SelectContourSupport(Vector2 mouse)
        {
            if (!scene.TryFindContourSupports(
                document,
                mouse,
                out List<string> supports,
                out List<Vector3> guide,
                out bool closed,
                out string warning))
            {
                ClearContourState();
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.contour_warning", warning), error: true);
                return;
            }
            try
            {
                IReadOnlyList<BlueprintEditorPart> preview = document.PreviewContour(
                    supports, closed, contourScaleStep);
                contourSupportIds.Clear();
                contourSupportIds.AddRange(supports);
                contourGuidePoints.Clear();
                contourGuidePoints.AddRange(guide);
                contourClosed = closed;
                contourPreviewCount = preview.Count;
                scene.ShowContourPreview(preview);
                RefreshContourPanel();
                UpdateGizmo();
                view.SetStatus(BuildWorksLocalization.Text(
                    closed ? "editor.contour_ring_preview" : "editor.contour_chain_preview",
                    supports.Count,
                    preview.Count));
            }
            catch (Exception exception)
            {
                ClearContourState();
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.contour_rejected", exception.Message), error: true);
            }
        }

        private void PreviewContour(float scaleStep)
        {
            if (!IsEditing || activeTool != BlueprintEditorTool.Contour) return;
            contourScaleStep = Mathf.Clamp(scaleStep, -0.99f, 3f);
            if (contourSupportIds.Count < 2)
            {
                RefreshContourPanel();
                return;
            }
            try
            {
                IReadOnlyList<BlueprintEditorPart> preview = ModifierDocument().PreviewContour(
                    contourSupportIds, contourClosed, contourScaleStep,
                    BuildWorksLocalization.Text("editor.generated.contour"));
                contourPreviewCount = preview.Count;
                scene.ShowContourPreview(preview);
                RefreshContourPanel();
                UpdateGizmo();
            }
            catch (Exception exception)
            {
                contourPreviewCount = 0;
                scene.ClearContourPreview();
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.contour_rejected", exception.Message), error: true);
                RefreshContourPanel();
            }
        }

        private void RefreshModifierPreview()
        {
            if (activeTool == BlueprintEditorTool.Array)
                PreviewArray();
            else if (activeTool == BlueprintEditorTool.Contour)
                PreviewContour(contourScaleStep);
        }

        private void ApplyContour()
        {
            if (contourSupportIds.Count < 2 || contourPreviewCount == 0)
            {
                view.SetStatus(BuildWorksLocalization.Text(
                    "editor.contour_select_chain_first"), error: true);
                return;
            }
            var supports = new List<string>(contourSupportIds);
            bool closed = contourClosed;
            int copies = contourPreviewCount;
            ClearContourState();
            if (!ApplyDocumentEdit(() => document.ApplyContour(
                supports, closed, contourScaleStep,
                BuildWorksLocalization.Text("editor.generated.contour")))) return;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus(BuildWorksLocalization.Text("editor.contour_applied", copies));
        }

        private void CancelContour()
        {
            ClearContourState();
            SetActiveTool(document.EditablePartSelectionCount > 0
                ? BlueprintEditorTool.Transform
                : BlueprintEditorTool.Select);
            view.SetStatus(BuildWorksLocalization.Text("editor.contour_cancelled"));
        }

        private void ClearContourState()
        {
            contourSupportIds.Clear();
            contourGuidePoints.Clear();
            contourClosed = false;
            contourPreviewCount = 0;
            scene?.ClearContourPreview();
            RefreshContourPanel();
        }

        private void RefreshContourPanel()
        {
            if (view == null || document == null) return;
            BlueprintEditorPart active = ActiveEditablePart();
            string source = active != null
                ? active.DisplayName
                : document.EditablePartSelectionCount > 0
                    ? BuildWorksLocalization.Text(
                        "editor.part_count", document.EditablePartSelectionCount)
                    : null;
            view.SetContourState(
                source,
                contourSupportIds.Count,
                contourPreviewCount,
                contourClosed);
        }

        private void SetSnap(float moveStep, float angleStep)
        {
            translationStep = Mathf.Clamp(moveStep, 0.001f, 10f);
            rotationStep = Mathf.Clamp(angleStep, 0.1f, 90f);
            string status = BuildWorksLocalization.Text(
                "editor.snap_steps",
                translationStep.ToString("0.###"),
                rotationStep.ToString("0.#"));
            UpdateTransformContext();
            view.SetStatus(status);
        }

        private static float NextPreset(float current, IReadOnlyList<float> presets)
        {
            for (int index = 0; index < presets.Count; ++index)
                if (presets[index] > current + 0.0001f) return presets[index];
            return presets[0];
        }

        private void ToggleSpace()
        {
            localSpace = !localSpace;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(
                localSpace && ActiveEditablePart() != null
                    ? "editor.axes_local"
                    : "editor.axes_world"));
        }

        private void ToggleAnchorVisibility()
        {
            showAllAnchors = !showAllAnchors;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(showAllAnchors
                ? "editor.anchors_all"
                : "editor.anchors_nearby"));
        }

        private void ToggleMeshSnap()
        {
            meshSnapEnabled = !meshSnapEnabled;
            snapTargetVisible = false;
            snapTargetIsNative = false;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(meshSnapEnabled
                ? "editor.mesh_snap"
                : "editor.native_snap"));
        }

        private void TogglePivotMode()
        {
            if (customPivot)
            {
                customPivot = false;
                pivotMode = BlueprintEditorPivotMode.SelectionCenter;
                UpdateTransformContext();
                UpdateGizmo();
                view.SetStatus(BuildWorksLocalization.Text("editor.pivot_selection"));
                return;
            }
            customPivot = false;
            pivotMode = BlueprintEditorPivotMode.SelectionCenter;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus(BuildWorksLocalization.Text("editor.pivot_selection"));
        }

        private void SetPivotMode(BlueprintEditorPivotMode mode)
        {
            customPivot = false;
            pivotMode = mode == BlueprintEditorPivotMode.ActiveObject &&
                ActiveEditablePart() == null
                ? BlueprintEditorPivotMode.SelectionCenter
                : mode;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(
                pivotMode == BlueprintEditorPivotMode.ActiveObject
                    ? "editor.pivot_active"
                    : "editor.pivot_selection"));
        }

        private void SetBoundsPivot(int index)
        {
            boundsPivotIndex = Mathf.Clamp(index, 0, 8);
            customPivot = false;
            pivotMode = BlueprintEditorPivotMode.Bounds;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(BuildWorksLocalization.Text(
                "editor.pivot_bounds", boundsPivotIndex + 1));
        }

        private void ResetSelectionTransform()
        {
            if (!TrySelectionBasis(out Vector3 pivot, out Quaternion orientation, null))
                return;
            customPivot = false;
            if (ApplyTransformDelta(-pivot, Quaternion.Inverse(orientation), pivot))
                view.SetStatus(BuildWorksLocalization.Text("editor.transform_reset"));
            else view.SetStatus(BuildWorksLocalization.Text("editor.transform_already_reset"));
        }

        private void UpdateTransformContext()
        {
            bool hasActivePart = ActiveEditablePart() != null;
            if (!hasActivePart && pivotMode == BlueprintEditorPivotMode.ActiveObject)
                pivotMode = BlueprintEditorPivotMode.SelectionCenter;
            view?.SetTransformContext(
                localSpace && hasActivePart,
                translationStep,
                rotationStep,
                customPivot,
                pivotMode,
                boundsPivotIndex);
            view?.SetGizmoOptions(showAllAnchors, meshSnapEnabled);
        }

        private bool TrySave(bool closeAfterSave)
        {
            if (keyboardPreview) return false;
            if (!IsEditing) return false;
            saveClosesOnSuccess = closeAfterSave;
            if (document.Parts.Count < 2)
            {
                view.ShowError(
                    BuildWorksLocalization.Text("editor.cannot_save_title"),
                    BuildWorksLocalization.Text("editor.minimum_two_parts"),
                    canRetry: false);
                return false;
            }
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (!ResolveVisualSource(part.PrefabName))
                {
                    view.ShowError(
                        BuildWorksLocalization.Text("editor.cannot_save_title"),
                        BuildWorksLocalization.Text("editor.part_not_found", part.PrefabName),
                        canRetry: false);
                    return false;
                }
            }

            state = EditorState.Saving;
            try
            {
                List<CompositeBlueprintStore.Part> parts = BuildStoreParts();
                List<CompositeBlueprintStore.VectorData> anchors = BuildBoundsAnchors();
                bool success;
                string error;
                List<CompositeBlueprintStore.Group> groups = BuildStoreGroups();
                if (sourceBlueprint == null)
                    success = store.TrySaveDocument(
                        document.Name,
                        document.Category,
                        parts,
                        groups,
                        anchors,
                        document.PrimaryPartId,
                        document.PrimaryGroupId,
                        out sourceBlueprint,
                        out error);
                else
                    success = store.TryUpdateDocument(
                        sourceBlueprint,
                        document.Name,
                        document.Category,
                        parts,
                        groups,
                        anchors,
                        document.PrimaryPartId,
                        document.PrimaryGroupId,
                        out error);
                if (!success)
                {
                    state = EditorState.Editing;
                    view.ShowError(
                        BuildWorksLocalization.Text("editor.save_error_title"), error);
                    return false;
                }
                document.AdoptSavedIdentity(
                    sourceBlueprint.id, sourceBlueprint.name, sourceBlueprint.category);
                document.MarkClean();
                state = EditorState.Editing;
                Bind(BuildWorksLocalization.Text("editor.blueprint_saved"));
                try
                {
                    saved?.Invoke(sourceBlueprint);
                }
                catch (Exception callbackException)
                {
                    logError("BuildWorks blueprint saved callback failed: " +
                        callbackException);
                    view.SetStatus(BuildWorksLocalization.Text(
                        "editor.library_refresh_failed"), error: true);
                }
                if (closeAfterSave) Cleanup();
                return true;
            }
            catch (Exception exception)
            {
                state = EditorState.Editing;
                logError("BuildWorks blueprint editor save failed: " + exception);
                view.ShowError(
                    BuildWorksLocalization.Text("editor.save_error_title"), exception.Message);
                return false;
            }
        }

        private List<CompositeBlueprintStore.Part> BuildStoreParts()
        {
            var result = new List<CompositeBlueprintStore.Part>(document.Parts.Count);
            foreach (BlueprintEditorPart part in document.Parts)
            {
                result.Add(new CompositeBlueprintStore.Part
                {
                    prefabName = part.PrefabName,
                    position = new CompositeBlueprintStore.VectorData(ToVector(part.Position)),
                    rotation = new CompositeBlueprintStore.QuaternionData(ToQuaternion(part.Rotation)),
                    scale = new CompositeBlueprintStore.VectorData(ToVector(part.Scale)),
                    stableId = part.StableId,
                    displayName = part.DisplayName,
                    parentGroupId = part.ParentGroupId,
                    editorVisible = part.Visible,
                    editorLocked = part.Locked
                });
            }
            return result;
        }

        private List<CompositeBlueprintStore.Group> BuildStoreGroups()
        {
            var result = new List<CompositeBlueprintStore.Group>(document.Groups.Count);
            foreach (BlueprintEditorGroup group in document.Groups)
            {
                result.Add(new CompositeBlueprintStore.Group
                {
                    stableId = group.StableId,
                    name = group.Name,
                    parentGroupId = group.ParentGroupId,
                    pivotPartId = group.PivotPartId,
                    editorVisible = group.Visible,
                    editorLocked = group.Locked
                });
            }
            return result;
        }

        private List<CompositeBlueprintStore.VectorData> BuildBoundsAnchors()
        {
            var ids = new List<string>();
            if (!string.IsNullOrEmpty(document.PrimaryPartId))
            {
                ids.Add(document.PrimaryPartId);
            }
            else if (!string.IsNullOrEmpty(document.PrimaryGroupId))
            {
                foreach (BlueprintEditorPart groupedPart in document.Parts)
                    if (document.IsPartInGroup(groupedPart.StableId, document.PrimaryGroupId))
                        ids.Add(groupedPart.StableId);
            }
            else
            {
                foreach (BlueprintEditorPart blueprintPart in document.Parts)
                    ids.Add(blueprintPart.StableId);
            }
            if (!scene.TryGetGizmoAnchors(ids, out Vector3[] anchors, out _))
                throw new InvalidOperationException(BuildWorksLocalization.Text(
                    "editor.blueprint_points_failed"));
            var result = new List<CompositeBlueprintStore.VectorData>(
                Math.Min(anchors.Length, 512));
            for (int index = 0; index < anchors.Length && index < 512; ++index)
                result.Add(new CompositeBlueprintStore.VectorData(anchors[index]));
            return result;
        }

        private void HandleDialog(BlueprintEditorDialogDecision decision)
        {
            switch (decision)
            {
                case BlueprintEditorDialogDecision.SaveAndExit:
                    view.HideDialog();
                    TrySave(closeAfterSave: true);
                    break;
                case BlueprintEditorDialogDecision.DiscardAndExit:
                    view.HideDialog();
                    Cleanup();
                    break;
                case BlueprintEditorDialogDecision.Cancel:
                case BlueprintEditorDialogDecision.Acknowledge:
                    view.HideDialog();
                    break;
                case BlueprintEditorDialogDecision.Retry:
                    view.HideDialog();
                    TrySave(saveClosesOnSuccess);
                    break;
            }
        }

        private void Bind(string status)
        {
            var missing = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in document.Parts)
                if (!ResolveVisualSource(part.PrefabName)) missing.Add(part.StableId);
            UpdateTransformContext();
            Vector3? boundsSize = scene.TryGetDocumentBounds(out Bounds bounds)
                ? bounds.size
                : (Vector3?)null;
            view.Bind(document, missing.Count == 0, missing, boundsSize);
            UpdateOperationMetrics();
            if (!string.IsNullOrEmpty(status))
                view.SetStatus(status, status.StartsWith("Blueprint editor scene sync failed:",
                    StringComparison.Ordinal));
        }

        private void FrameAll()
        {
            if (scene == null || !scene.TryGetContentBounds(out Bounds bounds))
            {
                cameraFocus = Vector3.zero;
                cameraDistance = 8f;
            }
            else
            {
                cameraFocus = bounds.center;
                cameraDistance = Mathf.Clamp(bounds.extents.magnitude * 2.6f, 2f, 500f);
            }
            ApplyCamera();
        }

        private void FrameSelectionOrAll()
        {
            if (scene == null ||
                !scene.TryGetSelectionBounds(document, out Bounds bounds))
            {
                FrameAll();
                return;
            }
            cameraFocus = bounds.center;
            cameraDistance = Mathf.Clamp(bounds.extents.magnitude * 2.6f, 2f, 500f);
            ApplyCamera();
        }

        private void ApplyCamera()
        {
            if (scene == null) return;
            Quaternion rotation = CameraRotation();
            scene.SetCameraPose(
                cameraFocus - rotation * Vector3.forward * cameraDistance,
                rotation);
            scene.SetProjection(orthographic, cameraDistance);
        }

        private Quaternion CameraRotation() => Quaternion.Euler(cameraPitch, cameraYaw, 0f);

        private void Cleanup()
        {
            if (state == EditorState.Closed) return;
            blockWorldInputThroughFrame = Math.Max(
                blockWorldInputThroughFrame, Time.frameCount);
            state = EditorState.Closing;
            selectionPending = false;
            cameraDragging = false;
            middleDeleteTarget = null;
            rightCameraTracking = cameraFlying = false;
            placementItem = null;
            contourSupportIds.Clear();
            contourGuidePoints.Clear();
            ResetGizmoDrag();
            activeTool = BlueprintEditorTool.Select;
            pinnedAnchorPoint = -1;
            pinnedAnchorWorld = null;
            selectedAnchorPoint = -1;
            selectedAnchorWorld = null;
            anchorConstraintAxis = GizmoAxis.None;
            customPivot = false;
            arrayPreviewCount = 0;
            contourPreviewCount = 0;
            try
            {
                try
                {
                    view?.Dispose();
                }
                catch (Exception exception)
                {
                    logError("BuildWorks blueprint editor view cleanup failed: " + exception);
                }
                try
                {
                    scene?.Dispose();
                }
                catch (Exception exception)
                {
                    logError("BuildWorks blueprint editor scene cleanup failed: " + exception);
                }
            }
            finally
            {
                view = null;
                scene = null;
                document = null;
                sourceBlueprint = null;
                RestoreGameCanvases();
                if (restoreCursor)
                {
                    Cursor.lockState = previousCursorLock;
                    Cursor.visible = previousCursorVisible;
                    restoreCursor = false;
                }
                state = EditorState.Closed;
            }
        }

        private void HideGameCanvases(Canvas editorCanvas)
        {
            foreach (Canvas candidate in Resources.FindObjectsOfTypeAll<Canvas>())
            {
                if (!candidate || candidate == editorCanvas ||
                    candidate.gameObject.layer == scene.Layer || !candidate.enabled ||
                    !candidate.gameObject.scene.IsValid()) continue;
                if (!hiddenGameCanvases.Contains(candidate))
                    hiddenGameCanvases.Add(candidate);
                candidate.enabled = false;
            }
        }

        private void RestoreGameCanvases()
        {
            foreach (Canvas candidate in hiddenGameCanvases)
                if (candidate) candidate.enabled = true;
            hiddenGameCanvases.Clear();
        }

        private GameObject ResolveVisualSource(string prefabName)
        {
            try
            {
                return resolveVisualSource(prefabName);
            }
            catch (Exception exception)
            {
                logError("BuildWorks blueprint visual resolver failed for " +
                    prefabName + ": " + exception);
                return null;
            }
        }

        private string DisplayName(string prefabName)
        {
            try
            {
                string value = resolveDisplayName(prefabName);
                return string.IsNullOrWhiteSpace(value) ? prefabName : value;
            }
            catch
            {
                return prefabName;
            }
        }

        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; ++index)
                if (values[index] == value) return true;
            return false;
        }

        private static Point3 ToPoint(Vector3 value) =>
            new Point3(value.x, value.y, value.z);

        private static Vector3 ToVector(Point3 value) =>
            new Vector3((float)value.X, (float)value.Y, (float)value.Z);

        private static Rotation3 ToRotation(Quaternion value) =>
            new Rotation3(value.x, value.y, value.z, value.w);

        private static Quaternion ToQuaternion(Rotation3 value) =>
            new Quaternion((float)value.X, (float)value.Y, (float)value.Z, (float)value.W);
    }
}
