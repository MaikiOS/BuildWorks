using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
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
            IBlueprintEditorInput editorInput = null)
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
                "Новый чертёж",
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
                error = "Не удалось открыть данные чертежа: " + exception.Message;
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
            HideGameCanvases(view.RootCanvas);
            scene.EnsureWorldCamerasExcludeEditorLayer();
            view.Tick();
            RefreshContextHints();
            Rect viewport = view.ViewportScreenRect();
            scene.SetViewport(viewport);
            scene.SetTemporarySelectionHighlight(ShiftHeld && !IsGizmoDragging, document);
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
                if (input.GetKeyDown(KeyCode.Escape)) view.HideViewportSettings();
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
                if (input.GetKeyDown(KeyCode.Escape)) view.CancelTextEdit();
                ApplyCamera();
                return;
            }

            if (HandleShortcuts() || !IsEditing) return;
            ApplyCamera();
            HandleViewport(viewport);
            ApplyCamera();
            UpdateGizmo();
        }

        internal void LateUpdate()
        {
            if (IsEditing)
            {
                HideGameCanvases(view.RootCanvas);
                ApplyCamera();
                if (view.HasCatalog || view.HasModal) scene.HideGizmo();
                else UpdateGizmo();
            }
        }

        internal void Close(bool discardChanges)
        {
            if (!IsEditing) return;
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
                error = "редактор уже отключён";
                return false;
            }
            if (state != EditorState.Closed)
            {
                error = "редактор уже открыт";
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
                Guid.NewGuid().ToString("N"), "Группа " + (document.Groups.Count + 1)));
            view.CreateGroupRequested += () => ApplyDocumentEdit(() => document.CreateEmptyGroup(
                Guid.NewGuid().ToString("N"), "Группа " + (document.Groups.Count + 1)));
            view.ShowAllRequested += () => ApplyDocumentEdit(document.ShowAll);
            view.HideSelectionRequested += () => ApplyDocumentEdit(() =>
                document.SetSelectionVisibility(false));
            view.LockSelectionRequested += () => ApplyDocumentEdit(() =>
                document.SetSelectionLocked(true));
            view.UngroupRequested += () => ApplyDocumentEdit(document.UngroupSelection);
            view.MoveSelectionToGroupRequested += groupId => ApplyDocumentEdit(() =>
                document.SetSelectionGroup(groupId));
            view.DuplicateSelectionRequested += () => ApplyDocumentEdit(() =>
                document.DuplicateSelection(new Point3(0.5, 0.0, 0.5)));
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
            view.LightingChanged += scene.SetLighting;
            view.ViewportSettingsChanged += (move, rotation, array, points, scale, grid) =>
                scene.SetViewportSettings(move, rotation, array, points, scale, grid);
            view.ProjectionChanged += value => { orthographic = value; ApplyCamera(); };
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
            bool control = input.GetKey(KeyCode.LeftControl) ||
                input.GetKey(KeyCode.RightControl);
            bool shift = input.GetKey(KeyCode.LeftShift) ||
                input.GetKey(KeyCode.RightShift);
            bool alt = input.GetKey(KeyCode.LeftAlt) ||
                input.GetKey(KeyCode.RightAlt);
            if (control && input.GetKeyDown(KeyCode.A))
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
                    document.DuplicateSelection(new Point3(0.5, 0.0, 0.5)));
                return true;
            }
            if (control && input.GetKeyDown(KeyCode.G))
            {
                ApplyDocumentEdit(() => document.CreateGroup(
                    Guid.NewGuid().ToString("N"), "Группа " + (document.Groups.Count + 1)));
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
            if (input.GetKeyDown(KeyCode.Q)) SetActiveTool(BlueprintEditorTool.Select);
            else if (input.GetKeyDown(KeyCode.G) || input.GetKeyDown(KeyCode.F9))
                SetActiveTool(BlueprintEditorTool.Transform);
            else if (input.GetKeyDown(KeyCode.R)) SetActiveTool(BlueprintEditorTool.Transform);
            else if (input.GetKeyDown(KeyCode.A)) SetActiveTool(BlueprintEditorTool.Array);
            else if (input.GetKeyDown(KeyCode.C)) SetActiveTool(BlueprintEditorTool.Contour);
            else if (input.GetKeyDown(KeyCode.Space)) ToggleSpace();
            else if (input.GetKeyDown(KeyCode.X)) SetAnchorConstraint(GizmoAxis.X);
            else if (input.GetKeyDown(KeyCode.Y)) SetAnchorConstraint(GizmoAxis.Y);
            else if (input.GetKeyDown(KeyCode.Z)) SetAnchorConstraint(GizmoAxis.Z);
            else if (input.GetKeyDown(KeyCode.Escape))
            {
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
                hints = "Esc — продолжить редактирование · выбери действие в окне";
            else if (view.HasOutlinerContextMenu)
                hints = "Удерживай ПКМ · наведи на действие · отпусти · вне меню — отмена";
            else if (view.HasOutlinerMenu)
                hints = "ЛКМ — выбрать действие · Esc — закрыть меню дерева";
            else if (view.HasViewportSettings)
                hints = "ЛКМ — изменить настройки вида · Esc — закрыть настройки";
            else if (view.HasCatalog)
                hints = view.IsTextInputFocused
                    ? "Ввод текста · Esc — закрыть каталог"
                    : "ЛКМ — выбрать · Q/E или PgUp/PgDn — страницы · Esc — закрыть";
            else if (view.IsOutlinerDragging)
                hints = "Отпусти над группой — перенести · над корнем — в ROOT · Esc — отменить";
            else if (view.IsNumericScrubbing)
                hints = "Тяни — изменить · Shift — точнее · Ctrl — быстрее · ПКМ — сброс";
            else if (view.IsTextInputFocused)
                hints = "Ввод значения · Enter — применить · Esc — отменить ввод";
            else if (placementItem != null)
                hints = "ЛКМ — добавить · колесо — повернуть · Q/E — точка привязки · СКМ — удалить · Ctrl+Z/Y — история · G — трансформация · ПКМ — каталог";
            else if (IsGizmoDragging)
                hints = "Отпусти ЛКМ — применить · Esc — отменить · Shift — без шага";
            else if (rightCameraTracking || input.GetMouseButton(1) &&
                view.ViewportScreenRect().Contains(input.MousePosition))
                hints = "WASD — камера · Q/E — вниз/вверх · Shift — быстрее · отпусти ПКМ — закончить";
            else if (activeTool == BlueprintEditorTool.Array)
                hints = arrayPreviewCount > 0
                    ? "Enter — создать копии · Esc — отменить · колесо — количество · Ctrl+колесо — камера"
                    : "Потяни золотую стрелку — первый ряд · другую — второй · Esc — отменить";
            else if (activeTool == BlueprintEditorTool.Contour)
                hints = contourPreviewCount > 0
                    ? "ЛКМ — другая цепь · Enter — создать копии · Esc — отменить"
                    : "ЛКМ по опоре — найти цепь · Esc — отменить";
            else if (activeTool == BlueprintEditorTool.Transform)
                hints = "Тяни — изменить · N — общий frame · H/Shift+H/Ctrl+H/Alt+H — видимость · Q — выбор";
            else if (document.Parts.Count == 0)
                hints = "Tab — добавить деталь · СКМ — камера";
            else
                hints = "ЛКМ — выбрать · G — трансформация · N — общий frame · H/Shift+H/Ctrl+H/Alt+H — видимость · Tab — добавить";
            view.SetContextHints(hints);
        }

        private void HandleViewport(Rect viewport)
        {
            Vector2 mouse = input.MousePosition;
            bool inside = viewport.Contains(mouse);
            if (!IsGizmoDragging && !cameraDragging && !rightCameraTracking &&
                view.IsViewportControlHit(mouse))
            {
                selectionPending = false;
                scene.SetHovered(null, document);
                view.SetHoveredNode(null);
                return;
            }
            if (inside && input.MouseScrollDelta.y != 0f)
            {
                bool control = input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl);
                if (placementItem != null && !input.GetMouseButton(1))
                    placementYaw += input.MouseScrollDelta.y > 0f ? 22.5f : -22.5f;
                else if (activeTool == BlueprintEditorTool.Array && arrayPrimaryAxis != GizmoAxis.None &&
                    !control && !IsGizmoDragging)
                    AdjustArrayCount(input.MouseScrollDelta.y > 0f ? 1 : -1);
                else cameraDistance = Mathf.Clamp(
                    cameraDistance * Mathf.Pow(0.88f, input.MouseScrollDelta.y), .5f, 500f);
            }

            if (inside && !IsGizmoDragging && input.GetMouseButtonDown(2))
            {
                cameraDragging = true;
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
                string stableId = null;
                bool delete = !middleCameraMoved && inside &&
                    scene.TryPick(mouse, out stableId);
                cameraDragging = false;
                if (delete)
                {
                    DeleteViewportPart(stableId);
                    return;
                }
            }

            if (inside && !cameraDragging && !IsGizmoDragging &&
                input.GetMouseButtonDown(1))
            {
                rightCameraTracking = true;
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
                bool click = !cameraFlying && inside;
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
                else if (activeTool == BlueprintEditorTool.Select)
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
            if (inside && activeTool == BlueprintEditorTool.Select &&
                !cameraDragging && !IsGizmoDragging)
                scene.TryPick(mouse, out hovered);
            scene.SetHovered(hovered, document);
            view.SetHoveredNode(hovered);
        }

        private bool TryBeginGizmoDrag(Vector2 mouse)
        {
            if (!TrySelectionPivot(
                    out Vector3 pivot, out Quaternion orientation, dragIds)) return false;
            if (activeTool == BlueprintEditorTool.Select &&
                (input.GetKey(KeyCode.LeftControl) || input.GetKey(KeyCode.RightControl) || ShiftHeld) &&
                scene.TryPick(mouse, out string pointedId) && !document.IsPartSelected(pointedId))
                return false;
            UpdateGizmo();
            GizmoHandleKind hoveredHandle = scene.HitTestGizmo(
                SelectionGizmoTool, pivot, orientation, localSpace, mouse, out GizmoAxis hoveredAxis);
            bool copyArrow = (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt)) &&
                hoveredHandle == GizmoHandleKind.Move;
            int anchor = hoveredHandle == GizmoHandleKind.Scale || copyArrow ||
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
                    view.SetStatus("Shift + ЛКМ закрепляет точку · Ctrl + тяни — магнитное перемещение.");
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
                    view.SetStatus("Точка лежит на оси вращения. Выбери другую точку или ось.");
                    return true;
                }
                dragStartMouse = mouse;
                dragTranslation = Vector3.zero;
                dragRotation = Quaternion.identity;
                dragScale = 1f;
                selectionPending = false;
                view.SetStatus(dragMagneticMove
                    ? "Магнит: тяни к точке другой детали · Shift — свободно."
                    : "Вращение вокруг закреплённой точки · X/Y/Z задают ось до перетаскивания.");
                return true;
            }

            if (ShiftHeld) return false;

            dragHandle = hoveredHandle;
            if (dragHandle == GizmoHandleKind.None) return false;

            dragDuplicate = dragHandle == GizmoHandleKind.Move &&
                (input.GetKey(KeyCode.LeftAlt) || input.GetKey(KeyCode.RightAlt));
            if (dragDuplicate)
            {
                try { CopyDocument(document).DuplicateSelection(default); }
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
                preview.DuplicateSelection(ToPoint(dragTranslation));
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
                view.SetStatus("Массив: " + arrayCountX + " × " + arrayCountY +
                    " · другая золотая стрелка задаёт второе направление · Enter применить.");
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
                ApplyDocumentEdit(() => document.DuplicateSelection(ToPoint(translation)));
            else ApplyTransformDelta(translation, rotation, pivot, scale);
        }

        private void CancelGizmoDrag()
        {
            if (dragHandle == GizmoHandleKind.Layout) RestoreArrayDrag();
            ResetGizmoDrag();
            scene.TrySync(document, out string warning);
            RefreshModifierPreview();
            Bind(warning);
        }

        private void ResetGizmoDrag()
        {
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
                    view.SetStatus("Точка лежит на оси вращения. Выбери другую точку или ось.");
                    return;
                }
                dragRotation = Quaternion.identity;
                scene.PreviewTransform(document, dragIds, Vector3.zero, Quaternion.identity, dragPivot);
                RefreshModifierPreview();
            }
            view.SetStatus(anchorConstraintAxis == GizmoAxis.None
                ? "Вращение точки: свободное."
                : "Вращение точки: ось " + anchorConstraintAxis + ". Повторное нажатие снимает ограничение.");
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
            view.SetStatus(pinned ? "Закрепление точки снято."
                : "Точка закреплена: тяни другую точку для вращения.");
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
            bool available = tool == BlueprintEditorTool.Select ||
                document.EditablePartSelectionCount > 0;
            if (!available)
            {
                activeTool = BlueprintEditorTool.Select;
                view.SetTool(activeTool);
                view.SetStatus("Сначала выбери доступную деталь или группу.", error: true);
                return;
            }
            if (IsGizmoDragging) CancelGizmoDrag();
            if (tool != BlueprintEditorTool.Array) ClearArrayState();
            else if (activeTool != BlueprintEditorTool.Array) ClearArrayState();
            if (tool != BlueprintEditorTool.Contour) ClearContourState();
            else if (activeTool != BlueprintEditorTool.Contour) ClearContourState();
            activeTool = tool;
            selectionPending = false;
            view.SetTool(tool);
            UpdateGizmo();
            if (tool == BlueprintEditorTool.Contour)
            {
                RefreshContourPanel();
                view.SetStatus(
                    "Контур: ЛКМ по ребру опоры — найти цепь · Enter применить · Esc отменить.");
            }
            else if (tool == BlueprintEditorTool.Array)
            {
                PreviewArray();
                view.SetStatus(
                    "Массив: потяни золотую стрелку для ряда, другую — для площадки · Enter применить · Esc отменить.");
            }
            else if (tool == BlueprintEditorTool.Transform)
                view.SetStatus("Точка: вращение · Shift+ЛКМ: закрепить · Ctrl+тяни: магнит · X/Y/Z: ось · ПКМ+WASD: камера");
            else view.SetStatus("ЛКМ: выбор · Ctrl+ЛКМ: добавить к выбору · рамка: несколько деталей · ПКМ: каталог");
        }

        private void UpdateGizmo()
        {
            if (scene == null) return;
            if (placementItem != null) return;
            scene.SetTemporarySelectionHighlight(ShiftHeld && !IsGizmoDragging, document);
            if (activeTool == BlueprintEditorTool.Contour)
                scene.ShowContourGuide(contourGuidePoints, contourClosed);
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
                    dragAnchorPoint >= 0 ? snapPreviewTargets : Array.Empty<Vector3>(),
                    dragAnchorPoint >= 0 ? snapPreviewNative : Array.Empty<bool>());
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
            if (!IsEditing) return false;
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
            if (!IsEditing) return;
            try
            {
                scene.HideGizmo();
                IReadOnlyList<BlueprintEditorCatalogItem> items = catalogItems();
                if (items == null || items.Count == 0)
                {
                    view.SetStatus("Каталог деталей пуст.", error: true);
                    return;
                }
                view.ShowCatalog(items);
                CatalogRequested?.Invoke();
            }
            catch (Exception exception)
            {
                logError("BuildWorks blueprint editor catalog failed: " + exception);
                view.SetStatus("Не удалось открыть каталог деталей.", error: true);
            }
        }

        private void BeginPartPlacement(BlueprintEditorCatalogItem item, bool snap)
        {
            if (!IsEditing || item == null) return;
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
            placementManualSnapPoint = -1;
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
                    ? "У детали нет штатных точек привязки."
                    : "Точка привязки: СВОБОДНО");
                return;
            }
            placementManualSnapPoint += direction;
            if (placementManualSnapPoint < -1) placementManualSnapPoint = count - 1;
            else if (placementManualSnapPoint >= count) placementManualSnapPoint = -1;
            view.SetTransientStatus("Точка привязки: " + PlacementSnapLabel(count));
        }

        private string PlacementSnapLabel(int count) => !placementSnap
            ? "СВОБОДНО"
            : placementManualSnapPoint < 0 || count <= 0
                ? "АВТО"
                : (placementManualSnapPoint + 1) + "/" + count +
                    (string.IsNullOrEmpty(scene.PlacementSnapPointLabel(placementManualSnapPoint))
                        ? string.Empty
                        : " · " + scene.PlacementSnapPointLabel(placementManualSnapPoint));

        private void SetPlacementStatus()
        {
            if (placementItem == null) return;
            view.SetStatus(
                "ЛКМ — добавить «" + placementItem.DisplayName +
                "» · колесо — поворот · Q/E — точка · ПКМ+WASD — камера · Esc — закончить");
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
            view.SetStatus(
                "Добавлено. ЛКМ — ещё одна · колесо — поворот · G — трансформация · Esc — закончить.");
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
            Bind("Добавление детали завершено.");
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
            string stepLabel = arrayDistribution == RepeatDistributionMode.Fit ? "ПО ДЛИНЕ" :
                arrayDistribution == RepeatDistributionMode.Exact ? ArrayExactSteps[arrayExactIndex].ToString("0.##") + " М" :
                ArrayGaps[arraySpacingIndex] == 0f ? "БЕЗ ЗАЗОРА" : "+ " + ArrayGaps[arraySpacingIndex].ToString("0.##") + " М";
            string stepInfo = "Шаг: " + arrayStepX.magnitude.ToString("0.###") + " / " +
                (arrayCountY > 1 ? arrayStepY.magnitude.ToString("0.###") : "—") + " м";
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
                    ToPoint(front), ToPoint(pivot));
                arrayPreviewCount = preview.Count;
                scene.ShowArrayPreview(preview);
                view.SetArrayState(preview.Count);
                view.SetStatus("Массив: " + arrayCountX + " × " + arrayCountY +
                    " · колесо: количество · Ctrl+колесо: масштаб вида · Enter: применить.");
            }
            catch (Exception exception)
            {
                arrayPreviewCount = 0;
                scene.ClearArrayPreview();
                view.SetArrayState(0, "Массив отклонён: " + exception.Message);
            }
        }

        private void ApplyArray()
        {
            if (activeTool != BlueprintEditorTool.Array || arrayPreviewCount == 0)
            {
                view.SetStatus("Массив: сначала настрой корректный предпросмотр.", error: true);
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
                ToPoint(pivot)))) return;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus("Массив применён: " + copies + " копий · одна операция Undo.");
        }

        private void CancelArray()
        {
            ClearArrayState();
            SetActiveTool(document.EditablePartSelectionCount > 0
                ? BlueprintEditorTool.Transform
                : BlueprintEditorTool.Select);
            view.SetStatus("Массив отменён.");
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
                view.SetStatus("Массив: для этого направления недостаточно места в лимите 128 деталей.", error: true);
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
                view.SetStatus("Контур: " + warning, error: true);
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
                view.SetStatus(
                    "Контур: " + (closed ? "кольцо" : "цепь") + " из " +
                    supports.Count + " опор · " + preview.Count +
                    " копий · Enter применить · Esc отменить.");
            }
            catch (Exception exception)
            {
                ClearContourState();
                view.SetStatus("Контур отклонён: " + exception.Message, error: true);
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
                    contourSupportIds, contourClosed, contourScaleStep);
                contourPreviewCount = preview.Count;
                scene.ShowContourPreview(preview);
                RefreshContourPanel();
                UpdateGizmo();
            }
            catch (Exception exception)
            {
                contourPreviewCount = 0;
                scene.ClearContourPreview();
                view.SetStatus("Контур отклонён: " + exception.Message, error: true);
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
                view.SetStatus("Контур: сначала выбери опорную цепь.", error: true);
                return;
            }
            var supports = new List<string>(contourSupportIds);
            bool closed = contourClosed;
            int copies = contourPreviewCount;
            ClearContourState();
            if (!ApplyDocumentEdit(() => document.ApplyContour(
                supports, closed, contourScaleStep))) return;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus("Контур применён: " + copies + " копий · одна операция Undo.");
        }

        private void CancelContour()
        {
            ClearContourState();
            SetActiveTool(document.EditablePartSelectionCount > 0
                ? BlueprintEditorTool.Transform
                : BlueprintEditorTool.Select);
            view.SetStatus("Контур отменён.");
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
                    ? document.EditablePartSelectionCount + " деталей"
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
            string status = "Шаг: " + translationStep.ToString("0.###") +
                " м · " + rotationStep.ToString("0.#") + "°";
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
            view.SetStatus(localSpace && ActiveEditablePart() != null
                ? "Оси: локальные."
                : "Оси: мировые.");
        }

        private void ToggleAnchorVisibility()
        {
            showAllAnchors = !showAllAnchors;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus(showAllAnchors
                ? "Точки: показаны все точки выбранной детали."
                : "Точки: показаны только ближайшие видимые точки.");
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
            view.SetStatus(meshSnapEnabled
                ? "Магнит: ванильные точки и границы моделей."
                : "Магнит: только ванильные точки соединения.");
        }

        private void TogglePivotMode()
        {
            if (customPivot)
            {
                customPivot = false;
                pivotMode = BlueprintEditorPivotMode.SelectionCenter;
                UpdateTransformContext();
                UpdateGizmo();
                view.SetStatus("Pivot: центр выбранного.");
                return;
            }
            customPivot = false;
            pivotMode = BlueprintEditorPivotMode.SelectionCenter;
            SetActiveTool(BlueprintEditorTool.Transform);
            view.SetStatus("Pivot: центр выбранного.");
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
            view.SetStatus(pivotMode == BlueprintEditorPivotMode.ActiveObject
                ? "Pivot: активная деталь."
                : "Pivot: центр выбранного.");
        }

        private void SetBoundsPivot(int index)
        {
            boundsPivotIndex = Mathf.Clamp(index, 0, 8);
            customPivot = false;
            pivotMode = BlueprintEditorPivotMode.Bounds;
            UpdateTransformContext();
            UpdateGizmo();
            view.SetStatus("Pivot: точка границ " + (boundsPivotIndex + 1) + "/9.");
        }

        private void ResetSelectionTransform()
        {
            if (!TrySelectionBasis(out Vector3 pivot, out Quaternion orientation, null))
                return;
            customPivot = false;
            if (ApplyTransformDelta(-pivot, Quaternion.Inverse(orientation), pivot))
                view.SetStatus("Трансформация выбранного сброшена одной операцией Undo.");
            else view.SetStatus("Трансформация уже сброшена.");
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
            if (!IsEditing) return false;
            saveClosesOnSuccess = closeAfterSave;
            if (document.Parts.Count < 2)
            {
                view.ShowError("НЕЛЬЗЯ СОХРАНИТЬ",
                    "Для чертежа нужны минимум две детали.", canRetry: false);
                return false;
            }
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (!ResolveVisualSource(part.PrefabName))
                {
                    view.ShowError("НЕЛЬЗЯ СОХРАНИТЬ",
                        "Не найдена деталь: " + part.PrefabName, canRetry: false);
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
                    view.ShowError("ОШИБКА СОХРАНЕНИЯ", error);
                    return false;
                }
                document.AdoptSavedIdentity(
                    sourceBlueprint.id, sourceBlueprint.name, sourceBlueprint.category);
                document.MarkClean();
                state = EditorState.Editing;
                Bind("Чертёж сохранён.");
                try
                {
                    saved?.Invoke(sourceBlueprint);
                }
                catch (Exception callbackException)
                {
                    logError("BuildWorks blueprint saved callback failed: " +
                        callbackException);
                    view.SetStatus(
                        "Чертёж сохранён, но список библиотеки не обновился.", error: true);
                }
                if (closeAfterSave) Cleanup();
                return true;
            }
            catch (Exception exception)
            {
                state = EditorState.Editing;
                logError("BuildWorks blueprint editor save failed: " + exception);
                view.ShowError("ОШИБКА СОХРАНЕНИЯ", exception.Message);
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
                throw new InvalidOperationException("Не удалось определить точки чертежа.");
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
