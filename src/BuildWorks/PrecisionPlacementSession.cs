using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using AnchorAdjustment = OstrixMods.BuildWorks.Geometry.AnchorAdjustment;
using AnchorBounds = OstrixMods.BuildWorks.Geometry.AnchorBounds;
using BlueprintEditorPart = OstrixMods.BuildWorks.Geometry.BlueprintEditorPart;
using BoundedUndoHistory = OstrixMods.BuildWorks.Geometry.BoundedUndoHistory<OstrixMods.BuildWorks.PrecisionPlacementSession.TransformSnapshot>;
using ConstructionLayout = OstrixMods.BuildWorks.Geometry.ConstructionLayout;
using Edge3 = OstrixMods.BuildWorks.Geometry.Edge3;
using GuidePathSampling = OstrixMods.BuildWorks.Geometry.GuidePathSampling;
using LayoutTransform3 = OstrixMods.BuildWorks.Geometry.LayoutTransform3;
using Point3 = OstrixMods.BuildWorks.Geometry.Point3;
using Rotation3 = OstrixMods.BuildWorks.Geometry.Rotation3;
using PrecisionAdjustment = OstrixMods.BuildWorks.Geometry.PrecisionAdjustment;

namespace OstrixMods.BuildWorks
{
    internal sealed class PrecisionPlacementSession : IDisposable
    {
        private static readonly List<ZNetView> scaleEnabledPrefabs = new List<ZNetView>();

        internal static void EnableNativePieceScale(ZNetScene scene)
        {
            if (!scene) return;
            foreach (GameObject prefab in scene.m_prefabs)
            {
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                ZNetView view = prefab ? prefab.GetComponent<ZNetView>() : null;
                if (!SupportsBlueprintWorkspacePiece(piece) || !view || view.m_syncInitialScale) continue;
                view.m_syncInitialScale = true;
                scaleEnabledPrefabs.Add(view);
            }
        }

        internal static void RestoreNativePieceScale()
        {
            foreach (ZNetView view in scaleEnabledPrefabs)
                if (view) view.m_syncInitialScale = false;
            scaleEnabledPrefabs.Clear();
        }

        internal void ApplyNativePlacementScale(GameObject placed, Piece source)
        {
            if (state != PlacementState.Armed ||
                source != selectedPiece || placementPlanIndex >= placementPlan.Count || !placed)
                return;
            Vector3 scale = placementPlan[placementPlanIndex].Scale;
            if (scale != Vector3.one)
            {
                ZNetView view = placed.GetComponent<ZNetView>();
                if (!view || !view.IsValid() || !view.IsOwner() || !view.m_syncInitialScale)
                    throw new InvalidOperationException("Размещённая деталь не поддерживает сохранение масштаба Valheim.");
                view.SetLocalScale(Vector3.Scale(source.transform.localScale, scale));
            }
            if (sessionMode == SessionMode.BlueprintWorldPlacement)
                currentBlueprintPlacements.Add(placed);
        }

        private enum PlacementState
        {
            Inactive,
            Editing,
            Frozen,
            Armed
        }

        private enum SessionMode
        {
            None,
            WorldPrecision,
            BlueprintWorldPlacement,
            BlueprintWorldSelection,
            BlueprintEditor
        }

        private sealed class BlueprintWorkspacePart
        {
            public Piece Source;
            public GameObject Visual;
            public Vector3 Scale = Vector3.one;
            public readonly List<Vector3> SnapLocal = new List<Vector3>();
        }

        internal readonly struct TransformSnapshot
        {
            public TransformSnapshot(Vector3 position, Quaternion rotation, Vector3? scale = null)
            {
                Position = position;
                Rotation = rotation;
                Scale = scale ?? Vector3.one;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private readonly struct SnapEdge
        {
            public SnapEdge(Vector3 start, Vector3 end)
            {
                Start = start;
                End = end;
            }

            public Vector3 Start { get; }
            public Vector3 End { get; }
        }

        private readonly struct ContourSupport
        {
            public ContourSupport(
                Vector3 position,
                Quaternion rotation,
                Vector3[] connectionPoints)
            {
                Position = position;
                Rotation = rotation;
                ConnectionPoints = connectionPoints;
            }

            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3[] ConnectionPoints { get; }
        }

        private static readonly FieldInfo PlacementGhostField = typeof(Player).GetField(
            "m_placementGhost",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo BuildPiecesField = typeof(Player).GetField(
            "m_buildPieces",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo AvailablePiecesField = typeof(PieceTable).GetField(
            "m_availablePiecesByCategory",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo ManualSnapPointField = typeof(Player).GetField(
            "m_manualSnapPoint",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdatePlacementGhostMethod = typeof(Player).GetMethod(
            "UpdatePlacementGhost",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo SetPlacementGhostValidMethod = typeof(Player).GetMethod(
            "SetPlacementGhostValid",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo TakeInputMethod = typeof(Player).GetMethod(
            "TakeInput",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo GetRightItemMethod = typeof(Humanoid).GetMethod(
            "GetRightItem",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo UpdatePlacementMethod = typeof(Player).GetMethod(
            "UpdatePlacement",
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            new[] { typeof(bool), typeof(float) },
            null);
        private static readonly FieldInfo NoPlacementCostField = typeof(Player).GetField(
            "m_noPlacementCost",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo LastToolUseTimeField = typeof(Player).GetField(
            "m_lastToolUseTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly FieldInfo PlacePressedTimeField = typeof(Player).GetField(
            "m_placePressedTime",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private const string ExternalBuildCameraUtilsTypeName = "Valheim_Build_Camera.Utils";
        private const string ExternalBuildCameraPluginTypeName =
            "Valheim_Build_Camera.Valheim_Build_CameraPlugin";
        private static bool externalBuildCameraResolved;
        private static bool externalBuildCameraDetected;
        private static MethodInfo externalBuildCameraInModeMethod;
        private static MethodInfo externalBuildCameraDisableMethod;
        private static MethodInfo externalBuildCameraEnableMethod;
        private static FieldInfo externalBuildCameraViewField;
        private static readonly float[] RepeatSpacings = { 0f, 0.01f, 0.05f, 0.10f, 0.50f };
        private static readonly float[] ExactRepeatSteps = { 0.25f, 0.50f, 1f, 2f, 4f };
        private static readonly float[] HandleScales = { 0.50f, 0.75f, 1f };
        private static readonly IReadOnlyList<Edge3> EmptyFeatureEdges = Array.Empty<Edge3>();
        private const float SnapSearchRadius = 12f;
        private const float ContourSearchRadius = 64f;
        private const float ContourEdgeScreenDistance = 30f;
        private const float SnapScreenDistance = 52f;
        private const float SnapReleaseScreenDistance = 78f;
        private const float SnapEdgeScreenDistance = 12f;
        private const float SnapPreviewScreenRadius = 120f;
        private const int MaximumSnapPreviewTargets = 24;
        private const int MaximumSnapVisibilityCandidates = 32;
        private const int MaximumSnapEdges = 3000;
        private const int MaximumFreeSnapTargets = 512;
        private const int MaximumMeshTriangles = 20000;
        private const int MaximumMeshVertices = 50000;
        private const int NativeAnchorStart = AnchorAdjustment.SelectableAnchorCount;
        private const int MaximumSourceNativeSnapPoints = 32;
        private const int MaximumBlueprintSourceNativeSnapPoints = 512;
        private const int MaximumExpandedPlacements = 512;
        private const int EditorLayer = 30;
        private const float EditorFloorSize = 2000f;
        private const float BlueprintWorkspaceRotationStep = 22.5f;
        private const float BlueprintWorkspaceSnapDistance = 0.55f;
        private static readonly Vector3 EditorWorkspaceOrigin = Vector3.zero;
        private const float AutoAlignmentDistance = 0.35f;
        private const float AutoSnapDistance = 0.75f;

        private readonly TransformGizmoView gizmo = new TransformGizmoView();
        private readonly PlacementGhostPreviewView previews = new PlacementGhostPreviewView();
        private readonly BoundedUndoHistory history = new BoundedUndoHistory(20);
        private readonly PrecisionPlacementHudView hud;
        private readonly CompositeBlueprintStore blueprintStore = new CompositeBlueprintStore();
        private readonly HammerBlueprintPieceRegistry blueprintPieceRegistry;
        private PlacementState state;
        private SessionMode sessionMode;
        private bool precisionEnabled;
        private GameObject placementGhost;
        private Piece selectedPiece;
        private Vector3 basePosition;
        private Quaternion baseRotation;
        private TransformSnapshot baseSnapshot;
        private Vector3 currentPosition;
        private Quaternion currentRotation;
        private GizmoMode gizmoMode = GizmoMode.Move;
        private GizmoAxis dragAxis;
        private GizmoHandleKind dragHandle;
        private bool localSpace = true;
        private bool showAllAnchors;
        private int handleScaleIndex;
        private int translationStepIndex = 1;
        private int rotationStepIndex = 2;
        private int repeatSpacingIndex;
        private int exactRepeatStepIndex = 2;
        private float repeatRise;
        private float repeatScaleStep;
        private bool repeatCountSecond;
        private string repeatPlanError;
        private float repeatTurnDegrees;
        private float repeatPitchDegrees;
        private float repeatRollDegrees;
        private RepeatDistributionMode repeatDistribution;
        private bool repeatSymmetric;
        private bool chainEnabled;
        private bool meshSnapEnabled;
        private bool autoAlignmentEnabled = true;
        private Vector2 dragStartMouse;
        private Vector3 dragStartPosition;
        private Quaternion dragStartRotation;
        private Vector3 dragStartRepeatStep;
        private GizmoAxis dragStartRepeatAxis;
        private float dragStartRepeatDraggedDistance;
        private int dragStartCopyCount;
        private Vector3 dragStartPlaneSecondStep;
        private int dragStartPlaneSecondCount;
        private float dragStartPlaneDistance;
        private Vector3 dragAxisWorld;
        private Vector2 dragScreenDirection;
        private float dragWorldUnitsPerPixel;
        private float dragStartScreenAngle;
        private float dragRotationScreenSign = 1f;
        private Vector3[] anchorLocalPoints = Array.Empty<Vector3>();
        private Vector3[] anchorWorldPoints = Array.Empty<Vector3>();
        private Vector3[] singlePieceAnchorLocalPoints = Array.Empty<Vector3>();
        private readonly List<Point3> anchorBoundsPoints = new List<Point3>();
        private readonly List<Vector3> snapTargets = new List<Vector3>();
        private readonly List<Vector3> freeSnapTargets = new List<Vector3>();
        private readonly List<Vector3> snapPreviewTargets = new List<Vector3>();
        private readonly List<bool> snapPreviewNative = new List<bool>();
        private readonly List<SnapEdge> snapEdges = new List<SnapEdge>();
        private readonly List<Piece> nearbyPieces = new List<Piece>();
        private readonly List<ContourSupport> contourCandidates = new List<ContourSupport>();
        private readonly List<ContourSupport> contourSupports = new List<ContourSupport>();
        private readonly List<Vector3> contourPath = new List<Vector3>();
        private readonly List<ContourSupport> contourHoverSupports = new List<ContourSupport>();
        private readonly List<Vector3> contourHoverPath = new List<Vector3>();
        private readonly List<Transform> nativeSnapPoints = new List<Transform>();
        private readonly List<Transform> sourceNativeSnapPoints = new List<Transform>();
        private readonly List<Piece> nativeSnapOwners = new List<Piece>();
        private readonly Dictionary<Mesh, IReadOnlyList<Edge3>> meshFeatureEdgeCache =
            new Dictionary<Mesh, IReadOnlyList<Edge3>>();
        private readonly HashSet<Renderer> lodRenderers = new HashSet<Renderer>();
        private readonly HashSet<Renderer> lodZeroRenderers = new HashSet<Renderer>();
        private int dragAnchorPoint = -1;
        private int pinnedAnchorPoint = -1;
        private GizmoAxis anchorConstraintAxis;
        private bool dragMagneticMove;
        private bool dragSourceIsNative;
        private bool snapTargetVisible;
        private bool snapTargetIsNative;
        private Vector3 snapTargetWorld;
        private bool alignmentTargetVisible;
        private Vector3 alignmentTargetPosition;
        private Quaternion alignmentTargetRotation = Quaternion.identity;
        private Vector3 dragFixedAnchorWorld;
        private Vector3 dragFixedAnchorLocal;
        private Vector3 dragMovingAnchorLocal;
        private Vector3 dragMovingVector;
        private Plane dragAnchorPlane;
        private bool dragConstraintActive;
        private Vector3 dragConstraintAxisWorld;
        private Vector3 dragConstraintCenter;
        private float dragConstraintRadius;
        private bool anchorBoundsAvailable;
        private CursorLockMode previousCursorLock;
        private bool previousCursorVisible;
        private bool cursorStateSaved;
        private bool passiveCursorActive;
        private Camera freeViewCamera;
        private Vector3 previousCameraPosition;
        private Quaternion previousCameraRotation;
        private Vector3 freeViewPanOffset;
        private float freeViewYaw;
        private float freeViewPitch;
        private float freeViewDistance;
        private Vector2 freeViewPreviousMouse;
        private bool freeViewActive;
        private bool freeViewDragging;
        private bool freeViewFlyLooking;
        private bool externalBuildCameraSuspended;
        private object externalBuildCameraView;
        private bool probingNativeInput;
        private bool allowNativeGhostUpdate;
        private bool requestAutomaticPlacement;
        private bool automaticPlacementRunning;
        private bool automaticPlacementReachedTryPlace;
        private bool automaticWaitingForStamina;
        private bool automaticWaitingForTool;
        private bool automaticPlacementPaused;
        private string automaticPauseReason;
        private bool automaticCooldownOverridden;
        private float automaticPreviousLastToolUseTime;
        private int suppressPlacementFrame = -1;
        private readonly List<TransformSnapshot> placementPlan = new List<TransformSnapshot>();
        private readonly List<Piece> placementPlanPieces = new List<Piece>();
        private readonly List<GameObject> currentBlueprintPlacements = new List<GameObject>();
        private readonly List<BlueprintRollbackItem> pendingBlueprintRollbacks = new List<BlueprintRollbackItem>();
        private float nextBlueprintRollbackRetry;
        private readonly List<Vector3> previewContacts = new List<Vector3>();
        private readonly List<Vector3> previewAims = new List<Vector3>();
        private int placementPlanIndex;
        private int automaticSkippedPlacements;
        private int copyCount = 5;
        private GizmoAxis repeatAxis;
        private Vector3 repeatStep;
        private float repeatDraggedDistance;
        private Vector3 planeSecondStep;
        private float planeDraggedDistance;
        private int planeSecondCount;
        private bool planeDraggingSecond;
        private Vector3 contourGhostPositionLocal;
        private Quaternion contourGhostRotationLocal = Quaternion.identity;
        private Vector3 contourPathPointLocal;
        private bool contourClosed;
        private int contourSeedSupportIndex;
        private Vector3 contourHoverPathPointLocal;
        private bool contourHoverClosed;
        private string contourHoverError;
        private float nextContourHoverUpdate;
        private bool continuationPending;
        private Piece continuationPiece;
        private TransformSnapshot continuationTransform;
        private CompositeBlueprintStore.Blueprint activeBlueprint;
        private Piece blueprintPalettePiece;
        private Piece editorReturnPiece;
        private readonly List<Piece> activeBlueprintPieces = new List<Piece>();
        private List<CompositeBlueprintStore.Part> originalBlueprintParts;
        private List<CompositeBlueprintStore.VectorData> originalBlueprintAnchors;
        private Vector3 blueprintRootPosition;
        private Quaternion blueprintRootRotation = Quaternion.identity;
        private Vector3 blueprintPivotLocal;
        private bool blueprintCompositeFrame;
        private int blueprintEditPartIndex = -1;
        private bool blueprintPartsDirty;
        private readonly List<Piece> blueprintSelection = new List<Piece>();
        private Piece blueprintHoverPiece;
        private readonly WorldSelectionHighlight blueprintHighlights = new WorldSelectionHighlight();
        private bool selectingBlueprint;
        private bool pendingWorldBlueprintSelection;
        private bool blueprintSelectionDragging;
        private Vector2 blueprintSelectionStart;
        private TransformSnapshot editorRootBeforePlacement;
        private bool expandedPlanLimited;
        private CompositeBlueprintStore.Blueprint pendingCatalogBlueprint;
        private bool pendingCatalogEditor;
        private bool pendingBlueprintCreation;
        private Piece lastRegularPalettePiece;
        private readonly List<BlueprintWorkspacePart> blueprintWorkspaceParts =
            new List<BlueprintWorkspacePart>();
        private BlueprintWorkspacePart blueprintWorkspaceEditPart;
        private float blueprintWorkspaceRotation;
        private bool blueprintWorkspaceRightTracking;
        private bool blueprintWorkspaceRightDragged;
        private Vector2 blueprintWorkspaceRightStart;
        private int previousCameraCullingMask;
        private CameraClearFlags previousCameraClearFlags;
        private Color previousCameraBackground;
        private bool previousCameraAllowHdr;
        private bool previousCameraAllowMsaa;
        private bool isolatedEditorView;
        private GameObject editorPlatform;
        private Material editorPlatformMaterial;
        private int editorLightingPreset;
        private readonly List<Behaviour> disabledEditorCameraEffects =
            new List<Behaviour>();
        private readonly Dictionary<GameObject, int> editorGhostLayers =
            new Dictionary<GameObject, int>();
        private readonly Dictionary<Light, int> editorLightMasks =
            new Dictionary<Light, int>();
        private readonly List<GameObject> editorLights = new List<GameObject>();
        public static bool HostApiAvailable =>
            PlacementGhostField != null && UpdatePlacementGhostMethod != null &&
            BuildPiecesField != null &&
            ManualSnapPointField != null &&
            TakeInputMethod != null && GetRightItemMethod != null &&
            UpdatePlacementMethod != null &&
            NoPlacementCostField != null && LastToolUseTimeField != null &&
            PlacePressedTimeField != null;

        public PrecisionPlacementSession(
            Action<CompositeBlueprintStore.Blueprint> openBlueprintEditor = null,
            Action createBlueprint = null)
        {
            blueprintPieceRegistry = new HammerBlueprintPieceRegistry(blueprintStore);
            hud = new PrecisionPlacementHudView(
                SelectGizmoMode,
                SelectAnchorConstraint,
                ToggleSpace,
                ToggleAnchorVisibility,
                CycleHandleScale,
                CycleTranslationStep,
                CycleRotationStep,
                CycleArrayStep,
                CycleDistribution,
                AdjustRepeatRise,
                AdjustRepeatTurn,
                AdjustRepeatPitch,
                AdjustRepeatRoll,
                AdjustRepeatScale,
                ToggleSymmetry,
                ToggleMeshSnap,
                ToggleAutoAlignment,
                AdjustCopyCount,
                SaveCurrentBlueprint,
                OpenBlueprintWorkspaceCatalog,
                CycleEditorLighting,
                UndoTransform,
                RedoTransform,
                ResetTransform,
                DisablePrecision,
                ConfirmEditor);
            HammerCatalogView.Configure(
                blueprintStore,
                blueprintPieceRegistry,
                (blueprint, edit) =>
                {
                    if (edit && openBlueprintEditor != null)
                        openBlueprintEditor(blueprint);
                    else HandleBlueprintCatalogAction(blueprint, edit);
                },
                createBlueprint ?? HandleCreateBlueprintCatalogAction,
                () =>
                {
                    pendingWorldBlueprintSelection = true;
                    suppressPlacementFrame = Time.frameCount;
                    if (Hud.IsPieceSelectionVisible()) Hud.CloseBuildUi();
                });
        }

        internal CompositeBlueprintStore BlueprintStore => blueprintStore;

        internal void SuspendForBlueprintEditor()
        {
            if (state != PlacementState.Inactive || sessionMode != SessionMode.None)
                Cancel();
            precisionEnabled = false;
            sessionMode = SessionMode.None;
        }

        internal GameObject ResolveBlueprintEditorVisual(string prefabName)
        {
            return TryResolveBuildPiece(
                Player.m_localPlayer, prefabName, out Piece piece)
                ? piece.gameObject
                : null;
        }

        internal string ResolveBlueprintEditorName(string prefabName)
        {
            if (!TryResolveBuildPiece(
                Player.m_localPlayer, prefabName, out Piece piece)) return prefabName;
            string name = piece.m_name;
            return Localization.instance != null && !string.IsNullOrWhiteSpace(name)
                ? Localization.instance.Localize(name)
                : string.IsNullOrWhiteSpace(name) ? prefabName : name;
        }

        internal IReadOnlyList<BlueprintEditorCatalogItem> BlueprintEditorCatalogItems()
        {
            var result = new List<BlueprintEditorCatalogItem>();
            Player player = Player.m_localPlayer;
            PieceTable table = GetBuildPieceTable(player);
            if (!table || table.m_pieces == null) return result;
            var names = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < table.m_pieces.Count; ++index)
            {
                GameObject prefab = table.m_pieces[index];
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (!SupportsBlueprintWorkspacePiece(piece) || !IsBuildPieceAvailable(table, piece) ||
                    blueprintPieceRegistry.TryGetBlueprint(piece, out _)) continue;
                string prefabName = PrefabName(piece);
                if (!names.Add(prefabName)) continue;
                result.Add(new BlueprintEditorCatalogItem(
                    prefabName,
                    ResolveBlueprintEditorName(prefabName),
                    piece.m_icon,
                    BlueprintEditorCategoryName(piece.m_category),
                    HammerCatalogOrganizer.Group(piece),
                    UnifiedHammerCatalog.OdinSourceGroup(piece) ?? "ВАНИЛЬНОЕ",
                    index));
            }
            result.Sort((left, right) =>
            {
                int priority = CatalogPriority(left).CompareTo(CatalogPriority(right));
                return priority != 0 ? priority : left.Index.CompareTo(right.Index);
            });
            foreach (CompositeBlueprintStore.Blueprint blueprint in blueprintStore.All())
            {
                if (!blueprintPieceRegistry.TryGetPiece(blueprint, out Piece piece) || !piece)
                    continue;
                result.Add(new BlueprintEditorCatalogItem(PrefabName(piece), blueprint.name,
                    piece.m_icon, blueprint.category, "ЧЕРТЕЖИ", "МОИ ЧЕРТЕЖИ",
                    result.Count, blueprint));
            }
            return result;
        }

        private static int CatalogPriority(BlueprintEditorCatalogItem item)
        {
            bool vanilla = string.Equals(item.Source, "ВАНИЛЬНОЕ", StringComparison.Ordinal);
            bool building = string.Equals(item.Category, "СТРОИТЕЛЬСТВО", StringComparison.Ordinal);
            return vanilla && building ? 0 : vanilla ? 1 : building ? 2 : 3;
        }

        private static string BlueprintEditorCategoryName(Piece.PieceCategory category)
        {
            switch (category)
            {
                case Piece.PieceCategory.Crafting:
                    return "РЕМЕСЛО";
                case Piece.PieceCategory.BuildingWorkbench:
                    return "СТРОИТЕЛЬСТВО";
                case Piece.PieceCategory.BuildingStonecutter:
                    return "ТЯЖ. ПОСТРОЙКИ";
                case Piece.PieceCategory.Furniture:
                    return "МЕБЕЛЬ";
                case Piece.PieceCategory.Misc:
                    return "ПРОЧЕЕ";
                default:
                    return category.ToString().ToUpperInvariant();
            }
        }

        internal void RefreshBlueprintEditorLibrary()
        {
            if (Player.m_localPlayer) blueprintPieceRegistry.Update(Player.m_localPlayer);
            HammerCatalogView.Invalidate();
        }

        public void Update()
        {
            RetryPendingBlueprintRollbacks();
            Player player = Player.m_localPlayer;
            if (!player)
            {
                precisionEnabled = false;
                Cancel(restoreCursor: false);
                return;
            }

            if (state == PlacementState.Inactive)
            {
                blueprintPieceRegistry.Update(player);
                Piece palettePiece = player.GetSelectedPiece();
                if (palettePiece && SupportsPrecisionPlacement(palettePiece) &&
                    !blueprintPieceRegistry.TryGetBlueprint(palettePiece, out _))
                    lastRegularPalettePiece = palettePiece;
                ContinueCatalogSelection(player);
                ContinueBlueprintCreation(player);
                if (state == PlacementState.Inactive)
                    SyncWorldPlacementMode(player.GetSelectedPiece());
            }

            if (pendingWorldBlueprintSelection && GameplayInputAvailable(player))
            {
                pendingWorldBlueprintSelection = false;
                BeginWorldBlueprintSelection(player);
                return;
            }

            if (Input.GetKeyDown(BuildWorksPlugin.TogglePrecisionKey.Value))
            {
                if (sessionMode == SessionMode.BlueprintEditor)
                {
                    if (blueprintWorkspaceEditPart != null)
                        EndBlueprintWorkspacePrecision(restoreTransform: false);
                    else
                        BeginBlueprintWorkspacePrecision(player, freeViewCamera, Input.mousePosition);
                    return;
                }
                if (state != PlacementState.Inactive)
                {
                    DisablePrecision();
                    return;
                }
                if (!GameplayInputAvailable(player))
                {
                    ShowStatus(player, "BuildWorks: сначала закрой меню или другой интерфейс.");
                    return;
                }
                precisionEnabled = true;
                if (!TryBeginWorldPrecision(player, showErrors: true, useContinuation: false))
                    precisionEnabled = false;
                return;
            }

            if (state == PlacementState.Inactive)
            {
                UpdatePassiveCursor(player);
                if (precisionEnabled &&
                    Input.GetKeyDown(BuildWorksPlugin.LockPrecisionKey.Value) &&
                    GameplayInputAvailable(player))
                {
                    TryBeginWorldPrecision(player, showErrors: true, useContinuation: false);
                }
                return;
            }

            if (Input.GetKeyDown(BuildWorksPlugin.LockPrecisionKey.Value))
            {
                Cancel();
                return;
            }

            if (hud.IsEditingAngle)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (sessionMode == SessionMode.BlueprintEditor)
                {
                    if (blueprintWorkspaceEditPart != null)
                    {
                        EndBlueprintWorkspacePrecision(restoreTransform: true);
                        return;
                    }
                    CancelBlueprintEditor();
                    return;
                }
                if (sessionMode == SessionMode.BlueprintWorldPlacement)
                {
                    CancelBlueprintPlacement(player);
                    return;
                }
                if (selectingBlueprint)
                {
                    if (sessionMode == SessionMode.BlueprintWorldSelection)
                    {
                        Finish();
                        ShowStatus(player, "BuildWorks: выбор деталей отменён.");
                        return;
                    }
                    EndBlueprintSelection(clearSelection: true);
                    return;
                }
                if (state == PlacementState.Armed)
                {
                    if (automaticPlacementPaused)
                    {
                        Finish();
                        ShowStatus(player, "BuildWorks: серия закончена; уже установленные детали сохранены.");
                        return;
                    }
                    ClearLayout();
                    ReopenEditor(player, "BuildWorks: установка отменена, редактирование продолжено.");
                    return;
                }
                if (state == PlacementState.Frozen)
                {
                    state = PlacementState.Editing;
                    return;
                }
                if (dragAxis != GizmoAxis.None || dragAnchorPoint >= 0)
                {
                    CancelNewestStage();
                    return;
                }
                FreezeEditor();
                return;
            }

            if (!ContextStillValid(player))
            {
                if (state == PlacementState.Armed && requestAutomaticPlacement &&
                    (automaticWaitingForTool || automaticPlacementPaused)) return;
                if (state == PlacementState.Armed && requestAutomaticPlacement &&
                    PlacementSelectionStillValid(player)) return;
                Cancel();
                return;
            }
            if (sessionMode == SessionMode.BlueprintEditor &&
                blueprintWorkspaceEditPart == null &&
                state == PlacementState.Editing)
            {
                if (Hud.IsPieceSelectionVisible())
                {
                    blueprintWorkspaceRightTracking = false;
                    blueprintWorkspaceRightDragged = false;
                }
                else if (HandleBlueprintWorkspaceCameraInput(
                    freeViewCamera,
                    Input.mousePosition)) return;
            }
            if (!GameplayInputAvailable(player))
            {
                if (sessionMode == SessionMode.BlueprintEditor ||
                    sessionMode == SessionMode.BlueprintWorldSelection) return;
                // Valheim temporarily blocks TakeInput during the hammer animation.
                // Keep an armed series alive until native placement accepts input again.
                if (state == PlacementState.Armed || state == PlacementState.Frozen) return;
                Cancel(restoreCursor: false);
                return;
            }

            if (state == PlacementState.Editing)
            {
                if (sessionMode == SessionMode.BlueprintEditor &&
                    blueprintWorkspaceEditPart == null)
                {
                    HandleBlueprintWorkspaceInput(player, freeViewCamera);
                    return;
                }
                bool controlHeld = Input.GetKey(KeyCode.LeftControl) ||
                    Input.GetKey(KeyCode.RightControl);
                bool shiftHeld = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);
                if (controlHeld && dragAxis == GizmoAxis.None && dragAnchorPoint < 0 &&
                    (Input.GetKeyDown(KeyCode.Y) ||
                    shiftHeld && Input.GetKeyDown(KeyCode.Z)))
                {
                    RedoTransform();
                    return;
                }
                if (controlHeld && Input.GetKeyDown(KeyCode.Z) &&
                    dragAxis == GizmoAxis.None && dragAnchorPoint < 0)
                {
                    UndoTransform();
                    return;
                }
                HandleFreeViewInput(freeViewCamera);
                if (sessionMode == SessionMode.BlueprintWorldPlacement &&
                    blueprintEditPartIndex < 0 && dragAxis == GizmoAxis.None &&
                    dragAnchorPoint < 0)
                    UpdateBlueprintFrameOverride();
                if (selectingBlueprint)
                    HandleBlueprintSelectionInput(freeViewCamera, Input.mousePosition);
                else
                    HandleGizmoInput(freeViewCamera);
            }
        }

        public void ApplyBlueprintCatalogLayout(PieceTable table)
        {
            blueprintPieceRegistry.ApplyCatalogLayout(table);
        }

        private void SyncWorldPlacementMode(Piece piece)
        {
            bool blueprintSelected = blueprintPieceRegistry.TryGetBlueprint(piece, out _);
            if (blueprintSelected)
            {
                if (sessionMode == SessionMode.None ||
                    sessionMode == SessionMode.BlueprintWorldPlacement)
                    sessionMode = SessionMode.BlueprintWorldPlacement;
                return;
            }
            if (sessionMode == SessionMode.BlueprintWorldPlacement)
            {
                sessionMode = SessionMode.None;
                previews.Clear();
            }
        }

        private void HandleBlueprintCatalogAction(
            CompositeBlueprintStore.Blueprint blueprint,
            bool edit)
        {
            Player player = Player.m_localPlayer;
            if (!player || !blueprintPieceRegistry.TryGetPiece(blueprint, out _))
            {
                ShowStatus(player, "BuildWorks: чертёж сейчас недоступен в молотке.");
                return;
            }
            pendingCatalogBlueprint = blueprint;
            pendingCatalogEditor = edit;
            suppressPlacementFrame = Time.frameCount;
            if (Hud.IsPieceSelectionVisible()) Hud.CloseBuildUi();
        }

        private void HandleCreateBlueprintCatalogAction()
        {
            pendingCatalogBlueprint = null;
            pendingCatalogEditor = false;
            pendingBlueprintCreation = true;
            suppressPlacementFrame = Time.frameCount;
            if (Hud.IsPieceSelectionVisible()) Hud.CloseBuildUi();
        }

        private void ContinueBlueprintCreation(Player player)
        {
            if (!pendingBlueprintCreation || !GameplayInputAvailable(player)) return;
            pendingBlueprintCreation = false;
            BeginBlueprintEditor(player, null);
        }

        private Piece FindBlueprintCreationPiece(Player player)
        {
            if (lastRegularPalettePiece && SupportsPrecisionPlacement(lastRegularPalettePiece) &&
                !blueprintPieceRegistry.TryGetBlueprint(lastRegularPalettePiece, out _))
                return lastRegularPalettePiece;
            PieceTable table = GetBuildPieceTable(player);
            if (!table || table.m_pieces == null) return null;
            foreach (GameObject prefab in table.m_pieces)
            {
                Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
                if (SupportsPrecisionPlacement(piece) &&
                    !blueprintPieceRegistry.TryGetBlueprint(piece, out _))
                    return piece;
            }
            return null;
        }

        private bool BeginWorldBlueprintSelection(Player player)
        {
            if (!player) return false;
            if (!TrySuspendExternalBuildCamera())
            {
                ShowStatus(player,
                    "BuildWorks: не удалось временно отключить Build Camera.");
                return false;
            }
            Camera camera = Camera.main;
            if (!camera)
            {
                RestoreExternalBuildCamera();
                ShowStatus(player, "BuildWorks: игровая камера не найдена.");
                return false;
            }

            sessionMode = SessionMode.BlueprintWorldSelection;
            state = PlacementState.Editing;
            passiveCursorActive = false;
            selectedPiece = player.GetSelectedPiece();
            placementGhost = PlacementGhostField.GetValue(player) as GameObject;
            currentPosition = basePosition = placementGhost
                ? placementGhost.transform.position
                : player.transform.position;
            currentRotation = baseRotation = placementGhost
                ? placementGhost.transform.rotation
                : Quaternion.identity;
            baseSnapshot = CaptureHistorySnapshot();
            history.Reset(baseSnapshot);
            activeBlueprint = null;
            activeBlueprintPieces.Clear();
            ClearLayout();
            SaveAndUnlockCursor();
            BeginFreeView(camera, isolateBlueprint: false);
            BeginBlueprintSelection();
            suppressPlacementFrame = Time.frameCount;
            HideWorldSelectionGhost(player);
            return true;
        }

        private void HideWorldSelectionGhost(Player player)
        {
            if (placementGhost) placementGhost.SetActive(false);
            GameObject hostGhost = player
                ? PlacementGhostField.GetValue(player) as GameObject
                : null;
            if (hostGhost && !ReferenceEquals(hostGhost, placementGhost))
                hostGhost.SetActive(false);
            previews.Hide();
        }

        private void ContinueCatalogSelection(Player player)
        {
            if (pendingCatalogBlueprint == null || !GameplayInputAvailable(player)) return;
            CompositeBlueprintStore.Blueprint blueprint = pendingCatalogBlueprint;
            bool openEditor = pendingCatalogEditor;
            pendingCatalogBlueprint = null;
            pendingCatalogEditor = false;
            if (openEditor)
            {
                BeginBlueprintEditor(player, blueprint);
                return;
            }
            if (!blueprintPieceRegistry.Select(player, blueprint))
            {
                ShowStatus(player, "BuildWorks: не удалось подготовить призрак чертежа.");
                return;
            }
            try
            {
                UpdatePlacementGhostMethod.Invoke(player, new object[] { false });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not position blueprint editor ghost: " +
                    exception);
                ShowStatus(player, "BuildWorks: не удалось поставить призрак перед персонажем.");
                return;
            }
            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            if (!ghost || !ghost.activeInHierarchy)
            {
                ShowStatus(player, "BuildWorks: Valheim не создал видимый призрак чертежа.");
                return;
            }
            sessionMode = SessionMode.BlueprintWorldPlacement;
            ShowStatus(player,
                "BuildWorks: ЛКМ установить; редактирование открывается только из библиотеки.");
        }

        private bool TryBeginWorldPrecision(
            Player player,
            bool showErrors,
            bool useContinuation)
        {
            Piece palettePiece = player ? player.GetSelectedPiece() : null;
            if (blueprintPieceRegistry.TryGetBlueprint(
                palettePiece,
                out CompositeBlueprintStore.Blueprint blueprint))
            {
                if (useContinuation) return false;
                return BeginBlueprintPlacement(player, blueprint, editBeforePlacement: true);
            }
            sessionMode = SessionMode.WorldPrecision;
            if (TryBegin(player, showErrors, useContinuation, isolateBlueprint: false))
                return true;
            sessionMode = SessionMode.None;
            return false;
        }

        private bool BeginBlueprintEditor(
            Player player,
            CompositeBlueprintStore.Blueprint blueprint)
        {
            if (!player)
            {
                return false;
            }

            List<Piece> pieces = new List<Piece>();
            if (blueprint != null && !TryResolveBlueprintPieces(
                player, blueprint, out pieces, out string missingPrefab))
            {
                ShowStatus(player, "BuildWorks: в текущем молотке нет детали " +
                    missingPrefab + ".");
                return false;
            }
            Piece entryPiece = pieces.Count > 0 ? pieces[0] : FindBlueprintCreationPiece(player);
            if (!entryPiece)
            {
                ShowStatus(player,
                    "BuildWorks: нет обычной детали для открытия редактора чертежей.");
                return false;
            }

            Piece previous = player.GetSelectedPiece();
            if (blueprintPieceRegistry.TryGetBlueprint(previous, out _))
                previous = lastRegularPalettePiece;
            editorReturnPiece = previous;
            sessionMode = SessionMode.BlueprintEditor;
            activeBlueprint = blueprint;
            if (!blueprintPieceRegistry.SelectPiece(player, entryPiece))
            {
                Finish();
                ShowStatus(player, "BuildWorks: не удалось подготовить деталь редактора.");
                return false;
            }
            try
            {
                allowNativeGhostUpdate = true;
                UpdatePlacementGhostMethod.Invoke(player, new object[] { false });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not prepare isolated blueprint editor: " +
                    exception);
                Finish();
                ShowStatus(player, "BuildWorks: не удалось открыть редактор чертежа.");
                return false;
            }
            finally
            {
                allowNativeGhostUpdate = false;
            }
            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            if (!ghost)
            {
                Finish();
                ShowStatus(player, "BuildWorks: Valheim не создал временную деталь редактора.");
                return false;
            }

            selectedPiece = entryPiece;
            placementGhost = ghost;
            ghost.SetActive(true);
            currentPosition = basePosition = EditorWorkspaceOrigin;
            currentRotation = baseRotation = Quaternion.identity;
            if (!TrySuspendExternalBuildCamera())
            {
                Finish();
                ShowStatus(player, "BuildWorks: не удалось временно отключить Build Camera.");
                return false;
            }
            Camera camera = Camera.main;
            if (!camera)
            {
                Finish();
                ShowStatus(player, "BuildWorks: игровая камера не найдена.");
                return false;
            }

            state = PlacementState.Editing;
            passiveCursorActive = false;
            ClearLayout();
            SaveAndUnlockCursor();
            BeginFreeView(camera, isolateBlueprint: true);
            float floorY = 0f;
            if (blueprint != null)
            {
                for (int index = 0; index < blueprint.parts.Count; ++index)
                {
                    CompositeBlueprintStore.Part part = blueprint.parts[index];
                    if (!AddBlueprintWorkspacePart(
                        pieces[index],
                        EditorWorkspaceOrigin + part.position.ToVector3(),
                        part.rotation.ToQuaternion(),
                        part.scale.ToVector3()))
                    {
                        Finish();
                        ShowStatus(player,
                            "BuildWorks: не удалось создать временную копию детали " +
                            pieces[index].m_name + ".");
                        return false;
                    }
                }
                floorY = BlueprintWorkspaceFloorY();
            }
            CreateEditorPlatform(floorY);
            FocusBlueprintWorkspace();
            RefreshBlueprintWorkspaceSelection(player);
            ShowStatus(player, blueprint == null
                ? "BuildWorks: пустой редактор открыт — выбери детали молотком и построй чертёж."
                : "BuildWorks: " + blueprint.name +
                    " открыт как отдельные временные детали.");
            return true;
        }

        private bool AddBlueprintWorkspacePart(
            Piece source,
            Vector3 position,
            Quaternion rotation,
            Vector3? scale = null)
        {
            if (!source || blueprintWorkspaceParts.Count >= CompositeBlueprintStore.MaximumParts)
                return false;
            GameObject visual = PlacementGhostPreviewView.CreateVisualClone(
                source.gameObject,
                "BuildWorks_BlueprintWorkspacePart_" + blueprintWorkspaceParts.Count,
                EditorLayer);
            if (!visual) return false;
            visual.transform.SetPositionAndRotation(position, rotation);
            Vector3 partScale = scale ?? Vector3.one;
            visual.transform.localScale = Vector3.Scale(source.transform.lossyScale, partScale);
            visual.SetActive(true);
            if (!TryCaptureAnchorBounds(visual, position, rotation, out AnchorBounds bounds))
            {
                UnityEngine.Object.Destroy(visual);
                return false;
            }

            Vector3 minimum = ToUnity(bounds.Minimum);
            Vector3 maximum = ToUnity(bounds.Maximum);
            BoxCollider collider = visual.AddComponent<BoxCollider>();
            Vector3 visualScale = visual.transform.lossyScale;
            Vector3 inverseScale = new Vector3(
                Mathf.Abs(visualScale.x) > 0.000001f ? 1f / visualScale.x : 1f,
                Mathf.Abs(visualScale.y) > 0.000001f ? 1f / visualScale.y : 1f,
                Mathf.Abs(visualScale.z) > 0.000001f ? 1f / visualScale.z : 1f);
            collider.center = Vector3.Scale((minimum + maximum) * 0.5f, inverseScale);
            Vector3 size = maximum - minimum;
            collider.size = new Vector3(
                Mathf.Max(size.x, 0.05f) * Mathf.Abs(inverseScale.x),
                Mathf.Max(size.y, 0.05f) * Mathf.Abs(inverseScale.y),
                Mathf.Max(size.z, 0.05f) * Mathf.Abs(inverseScale.z));

            var workspacePart = new BlueprintWorkspacePart
            {
                Source = source,
                Visual = visual,
                Scale = partScale
            };
            var sourceSnaps = new List<Transform>();
            source.GetSnapPoints(sourceSnaps);
            foreach (Transform snap in sourceSnaps)
            {
                if (snap)
                    workspacePart.SnapLocal.Add(
                        source.transform.InverseTransformPoint(snap.position));
            }
            blueprintWorkspaceParts.Add(workspacePart);
            return true;
        }

        private float BlueprintWorkspaceFloorY()
        {
            float floor = 0f;
            bool found = false;
            foreach (BlueprintWorkspacePart part in blueprintWorkspaceParts)
            {
                if (part?.Visual == null) continue;
                foreach (Renderer renderer in part.Visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer) continue;
                    floor = found ? Mathf.Min(floor, renderer.bounds.min.y) : renderer.bounds.min.y;
                    found = true;
                }
            }
            return found ? floor - 0.03f : 0f;
        }

        private void FocusBlueprintWorkspace()
        {
            Bounds bounds = new Bounds(new Vector3(0f, 1f, 0f), Vector3.one * 2f);
            bool found = false;
            foreach (BlueprintWorkspacePart part in blueprintWorkspaceParts)
            {
                if (part?.Visual == null) continue;
                foreach (Renderer renderer in part.Visual.GetComponentsInChildren<Renderer>(true))
                {
                    if (!renderer) continue;
                    if (found) bounds.Encapsulate(renderer.bounds);
                    else
                    {
                        bounds = renderer.bounds;
                        found = true;
                    }
                }
            }
            currentPosition = found ? bounds.center : new Vector3(0f, 1f, 0f);
            freeViewPanOffset = Vector3.zero;
            freeViewYaw = 35f;
            freeViewPitch = 24f;
            freeViewDistance = Mathf.Clamp(
                found ? bounds.extents.magnitude * 2.5f : 8f,
                4f,
                30f);
            ApplyFreeView();
        }

        private bool RefreshBlueprintWorkspaceSelection(Player player)
        {
            if (sessionMode != SessionMode.BlueprintEditor || !player) return false;
            Piece candidate = player.GetSelectedPiece();
            if (!SupportsBlueprintWorkspacePiece(candidate) ||
                blueprintPieceRegistry.TryGetBlueprint(candidate, out _))
            {
                if (placementGhost) placementGhost.SetActive(false);
                return false;
            }

            GameObject hostGhost = PlacementGhostField.GetValue(player) as GameObject;
            if (candidate == selectedPiece && hostGhost && ReferenceEquals(hostGhost, placementGhost))
            {
                ApplyEditorGhostLayer(placementGhost);
                return true;
            }

            RestoreEditorGhostLayers();
            try
            {
                allowNativeGhostUpdate = true;
                UpdatePlacementGhostMethod.Invoke(player, new object[] { false });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not change the blueprint editor piece: " +
                    exception);
                return false;
            }
            finally
            {
                allowNativeGhostUpdate = false;
            }
            selectedPiece = candidate;
            placementGhost = PlacementGhostField.GetValue(player) as GameObject;
            blueprintWorkspaceRotation = 0f;
            if (!placementGhost) return false;
            placementGhost.SetActive(true);
            ApplyEditorGhostLayer(placementGhost);
            return true;
        }

        private void HandleBlueprintWorkspaceInput(Player player, Camera camera)
        {
            if (!camera || Hud.IsPieceSelectionVisible())
            {
                if (placementGhost) placementGhost.SetActive(false);
                return;
            }
            Vector2 mouse = Input.mousePosition;
            if (!RefreshBlueprintWorkspaceSelection(player) ||
                !UpdateBlueprintWorkspaceGhost(player, camera, mouse)) return;
            if (MouseOverEditorPanel(mouse)) return;

            if (Input.GetMouseButtonDown(0))
            {
                if (!AddBlueprintWorkspacePart(
                    selectedPiece,
                    placementGhost.transform.position,
                    placementGhost.transform.rotation))
                {
                    ShowStatus(player, "BuildWorks: достигнут лимит или деталь не поддерживается.");
                    return;
                }
                ShowStatus(player, "BuildWorks: временная деталь добавлена без расхода ресурсов.");
            }
            if (Input.GetMouseButtonDown(2)) RemoveBlueprintWorkspacePart(camera, mouse);
        }

        private void OpenBlueprintWorkspaceCatalog()
        {
            if (sessionMode != SessionMode.BlueprintEditor ||
                blueprintWorkspaceEditPart != null || !Hud.instance) return;
            if (!Hud.IsPieceSelectionVisible()) Hud.instance.TogglePieceSelection();
            if (placementGhost) placementGhost.SetActive(false);
        }

        private bool HandleBlueprintWorkspaceCameraInput(Camera camera, Vector2 mouse)
        {
            bool overPanel = MouseOverEditorPanel(mouse);
            if (Input.GetKeyDown(KeyCode.F) && !overPanel) FocusBlueprintWorkspace();
            if (Input.GetMouseButtonDown(1) && !overPanel)
            {
                blueprintWorkspaceRightTracking = true;
                blueprintWorkspaceRightStart = mouse;
                freeViewPreviousMouse = mouse;
                blueprintWorkspaceRightDragged = false;
            }
            if (blueprintWorkspaceRightTracking && Input.GetMouseButton(1))
            {
                Vector2 delta = mouse - freeViewPreviousMouse;
                freeViewPreviousMouse = mouse;
                if ((mouse - blueprintWorkspaceRightStart).sqrMagnitude > 16f)
                    blueprintWorkspaceRightDragged = true;
                if (blueprintWorkspaceRightDragged)
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        float scale = freeViewDistance * 0.0015f;
                        freeViewPanOffset -= camera.transform.right * (delta.x * scale);
                        freeViewPanOffset -= camera.transform.up * (delta.y * scale);
                    }
                    else
                    {
                        freeViewYaw += delta.x * 0.25f;
                        freeViewPitch = Mathf.Clamp(freeViewPitch - delta.y * 0.25f, -85f, 85f);
                    }
                    ApplyFreeView();
                }
            }
            if (Input.GetMouseButtonUp(1) && blueprintWorkspaceRightTracking)
            {
                blueprintWorkspaceRightTracking = false;
                if (!blueprintWorkspaceRightDragged) OpenBlueprintWorkspaceCatalog();
                return true;
            }

            if (!overPanel)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl))
                    {
                        freeViewDistance = Mathf.Clamp(
                            freeViewDistance * Mathf.Pow(0.85f, wheel),
                            0.75f,
                            30f);
                        ApplyFreeView();
                    }
                    else
                    {
                        blueprintWorkspaceRotation += Mathf.Sign(wheel) *
                            BlueprintWorkspaceRotationStep;
                    }
                    return true;
                }
            }
            return blueprintWorkspaceRightTracking;
        }

        private bool UpdateBlueprintWorkspaceGhost(
            Player player,
            Camera camera,
            Vector2 mouse)
        {
            if (!placementGhost ||
                !TryGetBlueprintWorkspaceHit(camera, mouse, out RaycastHit hit))
            {
                if (placementGhost) placementGhost.SetActive(false);
                return false;
            }

            Quaternion rotation = Quaternion.Euler(0f, blueprintWorkspaceRotation, 0f);
            placementGhost.SetActive(true);
            placementGhost.transform.SetPositionAndRotation(hit.point, rotation);
            SnapBlueprintWorkspaceGhost();
            currentRotation = placementGhost.transform.rotation;
            if (SetPlacementGhostValidMethod != null)
                SetPlacementGhostValidMethod.Invoke(player, new object[] { true });
            return true;
        }

        private bool TryGetBlueprintWorkspaceHit(
            Camera camera,
            Vector2 mouse,
            out RaycastHit hit)
        {
            hit = default;
            if (!camera) return false;
            RaycastHit[] hits = Physics.RaycastAll(
                camera.ScreenPointToRay(mouse),
                2000f,
                1 << EditorLayer,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit candidate in hits)
            {
                if (!candidate.collider || placementGhost &&
                    candidate.collider.transform.IsChildOf(placementGhost.transform)) continue;
                hit = candidate;
                return true;
            }
            return false;
        }

        private void SnapBlueprintWorkspaceGhost()
        {
            Piece ghostPiece = placementGhost ? placementGhost.GetComponent<Piece>() : null;
            if (!ghostPiece || blueprintWorkspaceParts.Count == 0) return;
            var sourceSnaps = new List<Transform>();
            ghostPiece.GetSnapPoints(sourceSnaps);
            float closest = BlueprintWorkspaceSnapDistance * BlueprintWorkspaceSnapDistance;
            Vector3 adjustment = Vector3.zero;
            bool found = false;
            foreach (Transform sourceSnap in sourceSnaps)
            {
                if (!sourceSnap) continue;
                foreach (BlueprintWorkspacePart part in blueprintWorkspaceParts)
                {
                    if (part?.Visual == null) continue;
                    foreach (Vector3 targetLocal in part.SnapLocal)
                    {
                        Vector3 delta = part.Visual.transform.TransformPoint(targetLocal) -
                            sourceSnap.position;
                        float distance = delta.sqrMagnitude;
                        if (distance >= closest) continue;
                        closest = distance;
                        adjustment = delta;
                        found = true;
                    }
                }
            }
            if (found) placementGhost.transform.position += adjustment;
        }

        private void RemoveBlueprintWorkspacePart(Camera camera, Vector2 mouse)
        {
            if (!TryGetBlueprintWorkspaceHit(camera, mouse, out RaycastHit hit)) return;
            BlueprintWorkspacePart part = FindBlueprintWorkspacePart(hit.collider);
            if (part == null) return;
            blueprintWorkspaceParts.Remove(part);
            UnityEngine.Object.Destroy(part.Visual);
            ShowStatus(Player.m_localPlayer, "BuildWorks: временная деталь удалена.");
        }

        private BlueprintWorkspacePart FindBlueprintWorkspacePart(Collider collider)
        {
            if (!collider) return null;
            for (int index = blueprintWorkspaceParts.Count - 1; index >= 0; --index)
            {
                BlueprintWorkspacePart part = blueprintWorkspaceParts[index];
                if (part?.Visual && (collider.gameObject == part.Visual ||
                    collider.transform.IsChildOf(part.Visual.transform))) return part;
            }
            return null;
        }

        private bool BeginBlueprintWorkspacePrecision(
            Player player,
            Camera camera,
            Vector2 mouse)
        {
            if (sessionMode != SessionMode.BlueprintEditor || !player || !camera ||
                MouseOverEditorPanel(mouse) ||
                !TryGetBlueprintWorkspaceHit(camera, mouse, out RaycastHit hit))
            {
                ShowStatus(player,
                    "BuildWorks: наведи курсор на временную деталь и нажми F9.");
                return false;
            }
            BlueprintWorkspacePart part = FindBlueprintWorkspacePart(hit.collider);
            if (part?.Visual == null)
            {
                ShowStatus(player,
                    "BuildWorks: наведи курсор на временную деталь и нажми F9.");
                return false;
            }

            GameObject hostGhost = PlacementGhostField.GetValue(player) as GameObject;
            if (hostGhost) hostGhost.SetActive(false);
            blueprintWorkspaceEditPart = part;
            placementGhost = part.Visual;
            selectedPiece = part.Source;
            MovePieceWithoutMovingCameraFocus(part.Visual.transform.position);
            currentRotation = part.Visual.transform.rotation;
            basePosition = currentPosition;
            baseRotation = currentRotation;
            baseSnapshot = CaptureHistorySnapshot();
            history.Reset(baseSnapshot);
            ClearLayout();
            gizmoMode = GizmoMode.Move;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            pinnedAnchorPoint = -1;
            anchorConstraintAxis = GizmoAxis.None;
            alignmentTargetVisible = false;
            ClearSnapDrag();
            anchorBoundsAvailable = CaptureAnchorPoints(part.Visual);
            singlePieceAnchorLocalPoints = (Vector3[])anchorLocalPoints.Clone();
            suppressPlacementFrame = Time.frameCount;
            ShowStatus(player,
                "BuildWorks: F9 редактирует только эту временную деталь; ПРИМЕНИТЬ вернёт к строительству чертежа.");
            return true;
        }

        private void EndBlueprintWorkspacePrecision(bool restoreTransform)
        {
            BlueprintWorkspacePart part = blueprintWorkspaceEditPart;
            if (sessionMode != SessionMode.BlueprintEditor || part?.Visual == null) return;
            if (restoreTransform)
            {
                currentPosition = basePosition;
                currentRotation = baseRotation;
            }
            part.Visual.transform.SetPositionAndRotation(currentPosition, currentRotation);
            blueprintWorkspaceEditPart = null;
            ClearLayout();
            history.Clear();
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            pinnedAnchorPoint = -1;
            anchorConstraintAxis = GizmoAxis.None;
            alignmentTargetVisible = false;
            ClearSnapDrag();
            anchorLocalPoints = Array.Empty<Vector3>();
            anchorWorldPoints = Array.Empty<Vector3>();
            singlePieceAnchorLocalPoints = Array.Empty<Vector3>();
            anchorBoundsAvailable = false;
            gizmo.Hide();
            previews.Hide();

            Player player = Player.m_localPlayer;
            selectedPiece = player ? player.GetSelectedPiece() : null;
            placementGhost = player
                ? PlacementGhostField.GetValue(player) as GameObject
                : null;
            if (placementGhost)
            {
                placementGhost.SetActive(true);
                ApplyEditorGhostLayer(placementGhost);
            }
            ShowStatus(player, restoreTransform
                ? "BuildWorks: изменение положения временной детали отменено."
                : "BuildWorks: положение временной детали применено.");
        }

        private void ClearBlueprintWorkspace()
        {
            foreach (BlueprintWorkspacePart part in blueprintWorkspaceParts)
                if (part != null && part.Visual) UnityEngine.Object.Destroy(part.Visual);
            blueprintWorkspaceParts.Clear();
            blueprintWorkspaceEditPart = null;
            blueprintWorkspaceRotation = 0f;
            blueprintWorkspaceRightTracking = false;
            blueprintWorkspaceRightDragged = false;
        }

        private bool BeginBlueprintPlacement(
            Player player,
            CompositeBlueprintStore.Blueprint blueprint,
            bool editBeforePlacement = false)
        {
            Piece palettePiece = player ? player.GetSelectedPiece() : null;
            GameObject ghost = player
                ? PlacementGhostField.GetValue(player) as GameObject
                : null;
            string missingPrefab = string.Empty;
            List<Piece> pieces = null;
            if (!palettePiece || !ghost || !ghost.activeInHierarchy ||
                !TryResolveBlueprintPieces(player, blueprint, out pieces, out missingPrefab))
            {
                ShowStatus(player, string.IsNullOrEmpty(missingPrefab)
                    ? "BuildWorks: чертёж сейчас нельзя установить."
                    : "BuildWorks: в текущем молотке нет детали " + missingPrefab + ".");
                return false;
            }
            Vector3 placementOrigin = blueprintPieceRegistry.TryGetPlacementOrigin(
                palettePiece,
                out Vector3 origin)
                ? origin
                : Vector3.zero;

            placementGhost = ghost;
            selectedPiece = palettePiece;
            blueprintPalettePiece = palettePiece;
            sessionMode = SessionMode.BlueprintWorldPlacement;
            // A normal hammer click places one blueprint, regardless of the last F9 tool.
            if (!editBeforePlacement) gizmoMode = GizmoMode.Move;
            blueprintPivotLocal = placementOrigin;
            basePosition = currentPosition = ghost.transform.position;
            baseRotation = currentRotation = ghost.transform.rotation;
            baseSnapshot = CaptureHistorySnapshot();
            history.Reset(baseSnapshot);
            activeBlueprint = blueprint;
            blueprintCompositeFrame = !CompositeBlueprintStore.HasExplicitFrame(blueprint);
            activeBlueprintPieces.Clear();
            activeBlueprintPieces.AddRange(pieces);
            blueprintRootPosition = currentPosition - currentRotation * blueprintPivotLocal;
            blueprintRootRotation = currentRotation;
            Debug.Log("BuildWorks blueprint placement root " + blueprintRootPosition +
                ", ghost " + ghost.transform.position + ", origin " + placementOrigin + ".");
            blueprintEditPartIndex = -1;
            blueprintPartsDirty = false;
            state = PlacementState.Editing;
            ClearLayout();
            suppressPlacementFrame = Time.frameCount;
            if (editBeforePlacement)
            {
                int frameIndex = CompositeBlueprintStore.FramePartIndex(blueprint);
                if (!SelectBuildPiece(player, pieces[frameIndex]))
                {
                    ShowStatus(player,
                        "BuildWorks: опорная деталь чертежа больше недоступна.");
                    Finish();
                    return false;
                }
                currentPosition = blueprintRootPosition + blueprintRootRotation * blueprintPivotLocal;
                currentRotation = blueprintRootRotation;
                RebuildBlueprintPlan();
                ApplyEditingGhostTransform();
                if (!ApplyBlueprintAnchors())
                {
                    ShowStatus(player,
                        "BuildWorks: не удалось определить общие границы чертежа.");
                    Finish();
                    return false;
                }
                baseSnapshot = CaptureHistorySnapshot();
                history.Reset(baseSnapshot);
                if (!TrySuspendExternalBuildCamera())
                {
                    ShowStatus(player,
                        "BuildWorks: не удалось временно отключить Build Camera.");
                    Finish();
                    return false;
                }
                Camera camera = Camera.main;
                if (!camera)
                {
                    ShowStatus(player, "BuildWorks: игровая камера не найдена.");
                    Finish();
                    return false;
                }
                SaveAndUnlockCursor();
                BeginFreeView(camera, isolateBlueprint: false);
                ShowStatus(player,
                    "BuildWorks: F9 — точное положение всего чертежа; ПРИМЕНИТЬ установит его в мире.");
                return true;
            }
            bool armed = ArmPlacement();
            if (!armed && state != PlacementState.Armed &&
                sessionMode == SessionMode.BlueprintWorldPlacement)
            {
                ShowStatus(player, "BuildWorks: чертёж не удалось подготовить к установке.");
                Finish();
            }
            return armed || state == PlacementState.Armed;
        }

        public void LateUpdate()
        {
            if (state == PlacementState.Inactive)
            {
                ShowPassiveAssist();
                return;
            }

            Player player = Player.m_localPlayer;
            if (!player || !ContextStillValid(player))
            {
                if (player && state == PlacementState.Armed && requestAutomaticPlacement &&
                    (automaticWaitingForTool || automaticPlacementPaused)) return;
                if (player && state == PlacementState.Armed && requestAutomaticPlacement &&
                    PlacementSelectionStillValid(player)) return;
                Cancel(restoreCursor: player && GameplayInputAvailable(player));
                return;
            }

            if (sessionMode == SessionMode.BlueprintEditor &&
                blueprintWorkspaceEditPart == null)
            {
                gizmo.Hide();
                previews.Hide();
                if (Hud.IsPieceSelectionVisible() ||
                    !RefreshBlueprintWorkspaceSelection(player))
                {
                    if (placementGhost) placementGhost.SetActive(false);
                }
                else
                {
                    UpdateBlueprintWorkspaceGhost(player, freeViewCamera, Input.mousePosition);
                }
                UpdateHud();
                return;
            }
            if (sessionMode == SessionMode.BlueprintWorldSelection)
            {
                HideWorldSelectionGhost(player);
            }
            else ApplyEditingGhostTransform();
            if (state == PlacementState.Editing || state == PlacementState.Frozen)
            {
                if (!freeViewCamera)
                {
                    Cancel();
                    return;
                }
                Cursor.lockState = freeViewFlyLooking
                    ? CursorLockMode.Locked
                    : CursorLockMode.None;
                Cursor.visible = !freeViewFlyLooking;
                ApplyFreeView();
                if (state == PlacementState.Frozen)
                {
                    gizmo.Hide();
                    previews.Hide();
                    UpdateHud();
                    return;
                }
                if (selectingBlueprint)
                {
                    gizmo.Hide();
                    previews.Hide();
                    UpdateHud();
                    return;
                }
                Vector3[] anchors = UpdateAnchorWorldPoints();
                bool showConstraintPath = anchorConstraintAxis != GizmoAxis.None &&
                    (dragConstraintActive || pinnedAnchorPoint >= 0);
                Vector3 constraintCenter = dragConstraintActive
                    ? dragConstraintCenter
                    : pinnedAnchorPoint >= 0 && anchors != null
                        ? anchors[pinnedAnchorPoint]
                        : currentPosition;
                Vector3 constraintAxisWorld = dragConstraintActive
                    ? dragConstraintAxisWorld
                    : TransformGizmoView.AxisVector(
                        anchorConstraintAxis,
                        currentRotation,
                        localSpace);
                gizmo.Show(
                    freeViewCamera,
                    currentPosition,
                    currentRotation,
                    localSpace,
                    gizmoMode,
                    dragHandle,
                    dragAxis,
                    anchors,
                    dragAnchorPoint,
                    pinnedAnchorPoint,
                    NativeAnchorStart,
                    showAllAnchors,
                    HandleScales[handleScaleIndex],
                    snapTargetVisible,
                    snapTargetWorld,
                    snapTargetIsNative,
                    anchorConstraintAxis,
                    showConstraintPath,
                    constraintCenter,
                    constraintAxisWorld,
                    dragConstraintActive ? dragConstraintRadius : 0f);
                gizmo.ShowSnapCandidates(
                    freeViewCamera,
                    dragMagneticMove
                        ? snapPreviewTargets
                        : Array.Empty<Vector3>(),
                    dragMagneticMove
                        ? snapPreviewNative
                        : Array.Empty<bool>(),
                    HandleScales[handleScaleIndex]);
                gizmo.ShowAlignment(
                    freeViewCamera,
                    alignmentTargetVisible,
                    alignmentTargetPosition,
                    alignmentTargetRotation,
                    HandleScales[handleScaleIndex]);
                UpdateContourHover(freeViewCamera, Input.mousePosition);
                ShowLayoutPreview(freeViewCamera);
                previews.Show(
                    placementGhost,
                    placementPlan,
                    placementPlanPieces,
                    activeBlueprint != null && blueprintEditPartIndex >= 0
                        ? blueprintEditPartIndex
                        : CompositeBlueprintStore.FramePartIndex(activeBlueprint));
            }
            else
            {
                gizmo.Hide();
                if (state == PlacementState.Armed && placementPlan.Count > 0)
                    previews.Show(
                        placementGhost,
                        placementPlan,
                        placementPlanPieces,
                        Mathf.Clamp(placementPlanIndex, 0, placementPlan.Count - 1));
                else
                    previews.Hide();
            }
            UpdateHud();
        }

        private void ShowPassiveAssist()
        {
            Player player = Player.m_localPlayer;
            if (!player)
            {
                gizmo.Hide();
                previews.Hide();
                hud.Hide();
                return;
            }
            GameObject ghost = player
                ? PlacementGhostField.GetValue(player) as GameObject
                : null;
            Piece piece = player ? player.GetSelectedPiece() : null;
            if (sessionMode == SessionMode.BlueprintWorldPlacement)
            {
                gizmo.Hide();
                hud.Hide();
                if (!ShowPassiveBlueprintPreview(ghost, piece)) previews.Hide();
                return;
            }
            if (!GameplayInputAvailable(player))
            {
                gizmo.Hide();
                previews.Hide();
                hud.Hide();
                return;
            }
            previews.Hide();
            if (!precisionEnabled)
            {
                gizmo.Hide();
                hud.Hide();
                return;
            }

            Camera camera = Camera.main;
            if (continuationPending && piece != continuationPiece) ClearContinuation();
            hud.ShowPassive(continuationPending);
            if (!camera || !ghost || !ghost.activeInHierarchy ||
                !SupportsPrecisionPlacement(piece))
            {
                gizmo.Hide();
                return;
            }

            Vector3 passivePosition = continuationPending
                ? continuationTransform.Position
                : ghost.transform.position;
            Quaternion passiveRotation = continuationPending
                ? continuationTransform.Rotation
                : ghost.transform.rotation;
            gizmo.Show(
                camera,
                passivePosition,
                passiveRotation,
                localSpace,
                GizmoMode.Plane,
                GizmoHandleKind.None,
                GizmoAxis.None,
                null,
                -1,
                -1,
                NativeAnchorStart,
                false,
                HandleScales[handleScaleIndex],
                false,
                Vector3.zero,
                false,
                GizmoAxis.None,
                false,
                Vector3.zero,
                Vector3.zero,
                0f);
            gizmo.ShowLayout(
                camera,
                Array.Empty<Vector3>(),
                false,
                Array.Empty<Vector3>(),
                false,
                Array.Empty<Vector3>(),
                Array.Empty<Vector3>());
        }

        private bool ShowPassiveBlueprintPreview(GameObject ghost, Piece palettePiece)
        {
            if (sessionMode != SessionMode.BlueprintWorldPlacement ||
                !ghost || !ghost.activeInHierarchy ||
                !blueprintPieceRegistry.TryGetBlueprint(
                    palettePiece,
                    out _))
            {
                return false;
            }

            previews.Hide();
            return true;
        }

        public void ApplyTransformBeforeValidation(Player player)
        {
            if (sessionMode == SessionMode.BlueprintEditor ||
                state == PlacementState.Inactive || !ContextStillValid(player))
            {
                return;
            }
            ApplyEditingGhostTransform();
        }

        private void ApplyEditingGhostTransform()
        {
            if (!placementGhost) return;
            if (blueprintWorkspaceEditPart?.Visual == placementGhost)
            {
                placementGhost.transform.SetPositionAndRotation(
                    currentPosition,
                    currentRotation);
                return;
            }
            int blueprintGhostIndex = blueprintEditPartIndex >= 0
                ? blueprintEditPartIndex
                : CompositeBlueprintStore.FramePartIndex(activeBlueprint);
            TransformSnapshot transform = activeBlueprint != null &&
                (state == PlacementState.Editing || state == PlacementState.Frozen) &&
                blueprintGhostIndex < placementPlan.Count
                ? placementPlan[blueprintGhostIndex]
                : new TransformSnapshot(currentPosition, currentRotation);
            placementGhost.transform.SetPositionAndRotation(
                transform.Position,
                transform.Rotation);
            Vector3 scale = state == PlacementState.Armed &&
                placementPlanIndex < placementPlan.Count
                ? placementPlan[placementPlanIndex].Scale
                : activeBlueprint != null && blueprintGhostIndex < activeBlueprint.parts.Count
                    ? activeBlueprint.parts[blueprintGhostIndex].scale.ToVector3()
                : transform.Scale;
            placementGhost.transform.localScale = Vector3.Scale(selectedPiece.transform.localScale, scale);
        }

        public bool ApplyPassiveAutoJoin(
            Player player,
            ref Transform sourceSnap,
            ref Transform targetSnap,
            Piece aimedPiece)
        {
            if (state != PlacementState.Inactive || !autoAlignmentEnabled ||
                player != Player.m_localPlayer || player.AlternativePlacementActive ||
                !sourceSnap || !targetSnap)
            {
                return true;
            }

            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            Piece selected = player.GetSelectedPiece();
            if (!ghost || !ghost.activeInHierarchy ||
                !SupportsPrecisionPlacement(selected))
            {
                return true;
            }

            if ((int)ManualSnapPointField.GetValue(player) < 0)
            {
                if (!aimedPiece || aimedPiece.transform == ghost.transform ||
                    aimedPiece.transform.IsChildOf(ghost.transform))
                {
                    return false;
                }

                sourceNativeSnapPoints.Clear();
                nativeSnapPoints.Clear();
                ghost.GetComponent<Piece>()?.GetSnapPoints(sourceNativeSnapPoints);
                aimedPiece.GetSnapPoints(nativeSnapPoints);
                float bestDistance = 0.5f * 0.5f;
                Transform bestSource = null;
                Transform bestTarget = null;
                foreach (Transform source in sourceNativeSnapPoints)
                {
                    if (!source) continue;
                    foreach (Transform target in nativeSnapPoints)
                    {
                        if (!target) continue;
                        float distance = (target.position - source.position).sqrMagnitude;
                        if (distance >= bestDistance) continue;
                        bestDistance = distance;
                        bestSource = source;
                        bestTarget = target;
                    }
                }
                if (bestSource && bestTarget)
                {
                    sourceSnap = bestSource;
                    targetSnap = bestTarget;
                }
                else
                {
                    return false;
                }
            }

            Piece targetPiece = targetSnap.GetComponentInParent<Piece>();
            if (!targetPiece ||
                !sourceSnap.IsChildOf(ghost.transform) ||
                targetSnap.IsChildOf(ghost.transform))
            {
                return true;
            }

            ghost.transform.rotation =
                targetPiece.transform.rotation * ghost.transform.rotation;
            return true;
        }

        public void ApplyValidationPoint(Player player, ref Vector3 point)
        {
            // Surface-dependent blueprint parts get a fresh contact at their pose.
            if (activeBlueprint != null && !SupportsPrecisionPlacement(selectedPiece)) return;
            if (sessionMode != SessionMode.BlueprintEditor &&
                state != PlacementState.Inactive && ContextStillValid(player))
            {
                point = currentPosition;
            }
        }

        internal bool TryPrepareBlueprintValidation(Player player, out GameObject ghost, out Piece source)
        {
            ghost = null;
            source = null;
            if (activeBlueprint == null || SupportsPrecisionPlacement(selectedPiece) || !allowNativeGhostUpdate ||
                state == PlacementState.Inactive || !PlacementSelectionStillValid(player) ||
                placementPlanIndex >= placementPlan.Count ||
                placementPlanIndex >= placementPlanPieces.Count ||
                placementPlanPieces[placementPlanIndex] != selectedPiece) return false;
            TransformSnapshot pose = placementPlan[placementPlanIndex];
            placementGhost.transform.SetPositionAndRotation(pose.Position, pose.Rotation);
            placementGhost.transform.localScale = Vector3.Scale(selectedPiece.transform.localScale, pose.Scale);
            ghost = placementGhost;
            source = selectedPiece;
            return true;
        }

        public bool UsesFrozenPrecisionTransform(Player player)
        {
            return sessionMode != SessionMode.BlueprintEditor &&
                sessionMode != SessionMode.BlueprintWorldSelection &&
                state != PlacementState.Inactive && ContextStillValid(player);
        }

        public bool AllowsNativeGhostUpdate(Player player)
        {
            return state == PlacementState.Inactive ||
                allowNativeGhostUpdate ||
                player != Player.m_localPlayer;
        }

        public bool ShouldBlockNativePlacement(Player player, Piece piece)
        {
            if (player == Player.m_localPlayer &&
                (pendingWorldBlueprintSelection || selectingBlueprint ||
                sessionMode == SessionMode.BlueprintWorldSelection)) return true;
            if (state == PlacementState.Inactive || player != Player.m_localPlayer)
            {
                return false;
            }
            if (sessionMode == SessionMode.BlueprintEditor) return true;
            if (automaticPlacementPaused) return true;

            bool approvedContext = piece && piece == selectedPiece &&
                ContextStillValid(player);
            if (!approvedContext)
            {
                Cancel();
                return true;
            }

            if (state == PlacementState.Armed &&
                placementPlanIndex < placementPlan.Count &&
                placementPlan[placementPlanIndex].Scale != Vector3.one)
            {
                ZNetView view = piece.GetComponent<ZNetView>();
                if (!view || !view.m_syncInitialScale)
                {
                    PauseAutomaticPlacement(player, "деталь не поддерживает сохранение масштаба Valheim");
                    return true;
                }
            }

            return state != PlacementState.Armed;
        }

        public void PrepareNativePlacementUpdate(Player player, ref bool takeInput)
        {
            if (player != Player.m_localPlayer)
            {
                return;
            }
            if (pendingWorldBlueprintSelection || selectingBlueprint ||
                sessionMode == SessionMode.BlueprintWorldSelection)
            {
                takeInput = false;
                return;
            }
            if (state == PlacementState.Inactive)
            {
                if (suppressPlacementFrame == Time.frameCount ||
                    Hud.IsPieceSelectionVisible())
                {
                    takeInput = false;
                    return;
                }
                if (takeInput && Input.GetMouseButtonDown(0) &&
                    GameplayInputAvailable(player) &&
                    sessionMode == SessionMode.BlueprintWorldPlacement &&
                    blueprintPieceRegistry.TryGetBlueprint(
                        player.GetSelectedPiece(),
                        out CompositeBlueprintStore.Blueprint blueprint))
                {
                    takeInput = false;
                    BeginBlueprintPlacement(player, blueprint);
                    return;
                }
                bool passiveCursorHeld = precisionEnabled && takeInput &&
                    Input.GetKey(BuildWorksPlugin.PassiveCursorKey.Value);
                if (passiveCursorHeld) ActivatePassiveCursor();
                if (precisionEnabled &&
                    (Input.GetKeyDown(BuildWorksPlugin.TogglePrecisionKey.Value) ||
                    Input.GetKeyDown(BuildWorksPlugin.LockPrecisionKey.Value)))
                {
                    takeInput = false;
                    return;
                }
                if (passiveCursorHeld || passiveCursorActive)
                {
                    takeInput = false;
                }
                if (precisionEnabled && passiveCursorActive && Input.GetMouseButtonDown(0) &&
                    GameplayInputAvailable(player) &&
                    TryBeginPassiveAxisDrag(player, Camera.main, Input.mousePosition))
                {
                    takeInput = false;
                }
                return;
            }
            if (sessionMode == SessionMode.BlueprintEditor)
            {
                takeInput = false;
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(BuildWorksPlugin.TogglePrecisionKey.Value) ||
                !GameplayInputAvailable(player))
            {
                takeInput = false;
                return;
            }

            if (state == PlacementState.Armed && requestAutomaticPlacement &&
                !automaticPlacementRunning)
            {
                if (automaticWaitingForTool && !HasUsableBuildTool(player))
                {
                    return;
                }
                takeInput = false;
                return;
            }

            if (state == PlacementState.Armed && requestAutomaticPlacement)
            {
                bool noCost = (bool)NoPlacementCostField.GetValue(player);
                if (!noCost && !player.HaveRequirements(
                    selectedPiece,
                    Player.RequirementMode.CanBuild))
                {
                    PauseAutomaticPlacement(player, "не хватает ресурсов");
                    takeInput = false;
                    return;
                }
                if (!ValidateCurrentPlacement(player))
                {
                    if (TrySkipPlayerBlockedPlacement(player))
                    {
                        takeInput = false;
                        return;
                    }
                    Debug.LogWarning("BuildWorks series stopped: next transform status is " +
                        player.GetPlacementStatus() + ".");
                    PauseAutomaticPlacement(player, "следующая деталь не проходит проверку Valheim");
                    takeInput = false;
                    return;
                }
                if (!automaticCooldownOverridden)
                {
                    automaticPreviousLastToolUseTime =
                        (float)LastToolUseTimeField.GetValue(player);
                    automaticCooldownOverridden = true;
                }
                LastToolUseTimeField.SetValue(player, -9999f);
                takeInput = true;
                return;
            }

            if (state == PlacementState.Editing || state == PlacementState.Frozen)
            {
                takeInput = false;
            }

            if (state != PlacementState.Editing || !Input.GetMouseButtonDown(0) ||
                suppressPlacementFrame == Time.frameCount ||
                IsEditorInteractionAt(Camera.main, Input.mousePosition))
            {
                return;
            }
            if ((gizmoMode == GizmoMode.Repeat || gizmoMode == GizmoMode.Guide) &&
                placementPlan.Count == 0)
            {
                takeInput = false;
                return;
            }
            if (ArmPlacement())
            {
                takeInput = true;
            }
        }

        private bool TryBeginPassiveAxisDrag(Player player, Camera camera, Vector2 mouse)
        {
            if (!camera || hud.ContainsScreenPoint(mouse))
            {
                return false;
            }
            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            Piece piece = player.GetSelectedPiece();
            if (!ghost || !ghost.activeInHierarchy || !SupportsPrecisionPlacement(piece))
            {
                return false;
            }
            Vector3 hitPosition = continuationPending && continuationPiece == piece
                ? continuationTransform.Position
                : ghost.transform.position;
            Quaternion hitRotation = continuationPending && continuationPiece == piece
                ? continuationTransform.Rotation
                : ghost.transform.rotation;
            GizmoAxis axis = gizmo.HitTestLayout(
                camera,
                hitPosition,
                hitRotation,
                localSpace,
                mouse);
            if (axis == GizmoAxis.None ||
                !TryBeginWorldPrecision(player, showErrors: false, useContinuation: true))
            {
                return false;
            }

            if (gizmoMode != GizmoMode.Repeat)
                SelectGizmoMode(GizmoMode.Repeat);
            dragAxis = axis;
            dragHandle = GizmoHandleKind.Layout;
            gizmo.Show(
                camera,
                currentPosition,
                currentRotation,
                localSpace,
                gizmoMode,
                dragHandle,
                dragAxis,
                null,
                -1,
                -1,
                NativeAnchorStart,
                false,
                HandleScales[handleScaleIndex],
                false,
                Vector3.zero,
                false,
                GizmoAxis.None,
                false,
                Vector3.zero,
                Vector3.zero,
                0f);
            BeginDrag(camera, mouse);
            suppressPlacementFrame = Time.frameCount;
            return true;
        }

        public void ContinueAutomaticPlacement(Player player)
        {
            if (state != PlacementState.Armed || !requestAutomaticPlacement ||
                automaticPlacementRunning || player != Player.m_localPlayer)
            {
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) ||
                Input.GetKeyDown(BuildWorksPlugin.TogglePrecisionKey.Value) ||
                Input.GetKeyDown(BuildWorksPlugin.LockPrecisionKey.Value))
            {
                return;
            }
            if (automaticPlacementPaused)
            {
                if (!GameplayInputAvailable(player) ||
                    (!Input.GetKeyDown(KeyCode.Return) && !Input.GetKeyDown(KeyCode.KeypadEnter)))
                    return;
                automaticPlacementPaused = false;
                automaticPauseReason = null;
                RestoreCursor();
            }
            if (!HasUsableBuildTool(player))
            {
                if (!automaticWaitingForTool)
                {
                    automaticWaitingForTool = true;
                    SaveAndUnlockCursor();
                    ShowStatus(player, "BuildWorks: установлено " + placementPlanIndex + "/" +
                        placementPlan.Count + " — возьми исправный молоток, продолжим автоматически.");
                    Debug.Log("BuildWorks series waiting for a usable build tool before " +
                        (placementPlanIndex + 1) + "/" + placementPlan.Count + ".");
                }
                automaticWaitingForStamina = false;
                RestoreAutomaticCooldown(player);
                UpdateHud();
                return;
            }
            if (!GameplayInputAvailable(player))
            {
                return;
            }
            if (!SelectPlanPiece(player, placementPlanIndex))
            {
                if (automaticWaitingForTool)
                {
                    UpdateHud();
                    return;
                }
                Debug.LogWarning("BuildWorks series stopped before native placement " +
                    (placementPlanIndex + 1) + "/" + placementPlan.Count +
                    ": required blueprint piece is unavailable.");
                PauseAutomaticPlacement(player, "нужная деталь недоступна в текущем молотке");
                return;
            }
            if (!PlacementSelectionStillValid(player))
            {
                if (automaticWaitingForTool)
                {
                    UpdateHud();
                    return;
                }
                Debug.LogWarning("BuildWorks series stopped before native placement " +
                    (placementPlanIndex + 1) + "/" + placementPlan.Count +
                    ": placement context changed.");
                Cancel(restoreCursor: false);
                return;
            }
            if (automaticWaitingForTool)
            {
                automaticWaitingForTool = false;
                RestoreCursor();
                Debug.Log("BuildWorks series resumed after build tool replacement.");
            }
            if (!HasAutomaticPlacementStamina(player))
            {
                if (!automaticWaitingForStamina)
                {
                    automaticWaitingForStamina = true;
                    Debug.Log("BuildWorks series waiting for stamina before " +
                        (placementPlanIndex + 1) + "/" + placementPlan.Count + ".");
                    ShowStatus(player, "BuildWorks: ждём восстановления выносливости — " +
                        (placementPlanIndex + 1) + "/" + placementPlan.Count + ".");
                }
                return;
            }
            if (automaticWaitingForStamina)
            {
                automaticWaitingForStamina = false;
                Debug.Log("BuildWorks series resumed after stamina recovery.");
            }

            int planIndexBeforeNativeUpdate = placementPlanIndex;
            automaticPlacementRunning = true;
            automaticPlacementReachedTryPlace = false;
            try
            {
                Debug.Log("BuildWorks series driving native placement for " +
                    (placementPlanIndex + 1) + "/" + placementPlan.Count + " at " +
                    currentPosition + ".");
                PlacePressedTimeField.SetValue(player, Time.time);
                UpdatePlacementMethod.Invoke(
                    player,
                    new object[] { true, Time.deltaTime });

                if (state == PlacementState.Armed && requestAutomaticPlacement && !automaticPlacementPaused &&
                    !automaticPlacementReachedTryPlace &&
                    placementPlanIndex == planIndexBeforeNativeUpdate)
                {
                    if (!HasAutomaticPlacementStamina(player))
                    {
                        automaticWaitingForStamina = true;
                        RestoreAutomaticCooldown(player);
                        ShowStatus(player, "BuildWorks: ждём восстановления выносливости — " +
                            (placementPlanIndex + 1) + "/" + placementPlan.Count + ".");
                        return;
                    }
                    Debug.LogWarning("BuildWorks series stopped: Valheim did not start native " +
                        "placement for " + (placementPlanIndex + 1) + "/" +
                        placementPlan.Count + ".");
                    RestoreAutomaticCooldown(player);
                    PauseAutomaticPlacement(player, "Valheim не начал установку; проверь молоток и выносливость");
                }
            }
            catch (Exception exception)
            {
                Exception cause = exception.InnerException ?? exception;
                Debug.LogError("BuildWorks automatic native placement failed: " + cause);
                OnNativePlacementException(player, selectedPiece);
            }
            finally
            {
                PlacePressedTimeField.SetValue(player, -9999f);
                automaticPlacementRunning = false;
            }
        }

        private bool IsEditorInteractionAt(Camera camera, Vector2 mouse)
        {
            if (!camera || MouseOverEditorPanel(mouse) || freeViewFlyLooking ||
                selectingBlueprint || dragAxis != GizmoAxis.None || dragAnchorPoint >= 0)
            {
                return true;
            }
            if (gizmo.HitTestAnchor(camera, anchorWorldPoints, mouse) >= 0)
                return true;
            if (activeBlueprint != null && HitTestBlueprintPart(camera, mouse) >= 0)
                return true;
            if (gizmoMode == GizmoMode.Guide)
                return gizmo.HitTestLayout(camera, mouse);
            return gizmo.HitTestMove(
                    camera, currentPosition, currentRotation, localSpace, mouse) != GizmoAxis.None ||
                gizmo.HitTestRotation(
                    camera, currentPosition, currentRotation, localSpace, mouse) != GizmoAxis.None ||
                gizmoMode == GizmoMode.Repeat && gizmo.HitTestLayout(
                    camera, currentPosition, currentRotation, localSpace, mouse) != GizmoAxis.None;
        }

        public bool ShouldBlockPlayerInput(Player player)
        {
            return state != PlacementState.Inactive && state != PlacementState.Armed &&
                !probingNativeInput &&
                player == Player.m_localPlayer;
        }

        public bool ShouldBlockGameCamera()
        {
            return state != PlacementState.Inactive &&
                !(state == PlacementState.Armed && (automaticWaitingForTool || automaticPlacementPaused)) ||
                passiveCursorActive;
        }

        public bool ShouldBlockCharacterControls()
        {
            return state != PlacementState.Inactive &&
                !(state == PlacementState.Armed && (automaticWaitingForTool || automaticPlacementPaused)) ||
                passiveCursorActive;
        }

        public void OnNativePlacementStarting(Player player, Piece piece)
        {
            if (state == PlacementState.Armed && player == Player.m_localPlayer &&
                piece && piece == selectedPiece)
            {
                automaticPlacementReachedTryPlace = automaticPlacementRunning;
                allowNativeGhostUpdate = true;
                requestAutomaticPlacement = false;
            }
        }

        public void OnNativePlacementFinished(Player player, Piece piece, bool placed)
        {
            allowNativeGhostUpdate = false;
            if (state == PlacementState.Inactive && player == Player.m_localPlayer && placed)
            {
                ClearContinuation();
                return;
            }
            if (state != PlacementState.Armed || player != Player.m_localPlayer ||
                !piece || piece != selectedPiece)
            {
                return;
            }

            if (placed)
            {
                automaticCooldownOverridden = false;
                ++placementPlanIndex;
                Debug.Log("BuildWorks series placed " + placementPlanIndex + "/" +
                    placementPlan.Count + ".");
                if (placementPlanIndex < placementPlan.Count)
                {
                    TransformSnapshot next = placementPlan[placementPlanIndex];
                    currentPosition = next.Position;
                    currentRotation = next.Rotation;
                    requestAutomaticPlacement = true;
                    return;
                }
                if (automaticSkippedPlacements > 0)
                {
                    ShowStatus(player, "BuildWorks: серия завершена; пропущено из-за персонажа: " +
                        automaticSkippedPlacements + ".");
                }
                else if (activeBlueprint != null)
                {
                    ShowStatus(player, "BuildWorks: чертёж установлен — " +
                        placementPlan.Count + " деталей.");
                }
                CaptureContinuation();
                currentBlueprintPlacements.Clear();
                Finish(preserveContinuation: true);
                return;
            }

            RestoreAutomaticCooldown(player);
            Debug.LogWarning("BuildWorks series native placement returned false at " +
                (placementPlanIndex + 1) + "/" + placementPlan.Count +
                "; status " + player.GetPlacementStatus() + ".");
            if (TrySkipPlayerBlockedPlacement(player))
            {
                return;
            }
            PauseAutomaticPlacement(player, "Valheim отклонил следующую деталь");
        }

        private void PauseAutomaticPlacement(Player player, string reason)
        {
            RestoreAutomaticCooldown(player);
            automaticPlacementPaused = true;
            automaticPauseReason = reason;
            requestAutomaticPlacement = true;
            automaticWaitingForStamina = false;
            SaveAndUnlockCursor();
            UpdateHud();
            ShowStatus(player, "BuildWorks: установлено " +
                (placementPlanIndex - automaticSkippedPlacements) + "/" +
                placementPlan.Count + " — " + reason + ". Enter — продолжить с детали " +
                (placementPlanIndex + 1) + "; Esc — закончить серию.");
        }

        private bool TrySkipPlayerBlockedPlacement(Player player)
        {
            if (player.GetPlacementStatus() != Player.PlacementStatus.BlockedbyPlayer ||
                placementPlanIndex >= placementPlan.Count)
            {
                return false;
            }

            int skipped = placementPlanIndex + 1;
            ++automaticSkippedPlacements;
            ++placementPlanIndex;
            Debug.LogWarning("BuildWorks series skipped player-blocked placement " + skipped +
                "/" + placementPlan.Count + ".");
            if (placementPlanIndex >= placementPlan.Count)
            {
                ShowStatus(player, "BuildWorks: серия завершена; пропущено из-за персонажа: " +
                    automaticSkippedPlacements + ".");
                Finish();
                return true;
            }

            TransformSnapshot next = placementPlan[placementPlanIndex];
            currentPosition = next.Position;
            currentRotation = next.Rotation;
            requestAutomaticPlacement = true;
            ShowStatus(player, "BuildWorks: ячейка " + skipped + " занята персонажем — пропускаем.");
            return true;
        }

        public void OnNativePlacementException(Player player, Piece piece)
        {
            allowNativeGhostUpdate = false;
            RestoreAutomaticCooldown(player);
            if (state == PlacementState.Armed && player == Player.m_localPlayer &&
                piece && piece == selectedPiece)
            {
                ShowStatus(
                    player,
                    "BuildWorks: ошибка установки. Проверь мир перед повторной попыткой.");
                Cancel();
            }
        }

        private static bool HasAutomaticPlacementStamina(Player player)
        {
            ItemDrop.ItemData item = GetRightItemMethod.Invoke(player, null) as ItemDrop.ItemData;
            Attack attack = item?.m_shared?.m_attack;
            return attack == null || player.HaveStamina(attack.m_attackStamina);
        }

        private static bool HasUsableBuildTool(Player player)
        {
            ItemDrop.ItemData item = GetRightItemMethod.Invoke(player, null) as ItemDrop.ItemData;
            return item?.m_shared?.m_buildPieces != null &&
                (!item.m_shared.m_useDurability || item.m_durability > 0f);
        }

        public void Dispose()
        {
            precisionEnabled = false;
            Cancel();
            HammerCatalogView.Unconfigure(blueprintStore);
            blueprintPieceRegistry.Dispose();
            gizmo.Dispose();
            previews.Dispose();
            hud.Dispose();
        }

        public void DisposeForHostTransition()
        {
            precisionEnabled = false;
            Cancel(restoreCursor: false);
            HammerCatalogView.Unconfigure(blueprintStore);
            blueprintPieceRegistry.Dispose();
            gizmo.Dispose();
            previews.Dispose();
            hud.Dispose();
        }

        private bool TryBegin(
            Player player,
            bool showErrors,
            bool useContinuation,
            bool isolateBlueprint)
        {
            Piece piece = player.GetSelectedPiece();
            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            if (!piece)
            {
                if (showErrors)
                    ShowStatus(player, "BuildWorks: выбери строительную деталь молотом.");
                return false;
            }
            if (!ghost || !ghost.activeInHierarchy)
            {
                if (showErrors)
                    ShowStatus(player, "BuildWorks: сначала наведи видимую деталь на точку привязки.");
                return false;
            }
            if (!SupportsPrecisionPlacement(piece))
            {
                if (showErrors)
                    ShowStatus(player, "BuildWorks: эта специальная деталь пока не поддерживается.");
                return false;
            }
            placementGhost = ghost;
            selectedPiece = piece;
            bool continueLayout = useContinuation && continuationPending &&
                continuationPiece == piece;
            if (continuationPending && !continueLayout) ClearContinuation();
            basePosition = currentPosition = continueLayout
                ? continuationTransform.Position
                : ghost.transform.position;
            baseRotation = currentRotation = continueLayout
                ? continuationTransform.Rotation
                : ghost.transform.rotation;
            if (continueLayout) ClearContinuation();
            baseSnapshot = CaptureHistorySnapshot();
            history.Reset(baseSnapshot);
            activeBlueprint = null;
            activeBlueprintPieces.Clear();
            anchorBoundsAvailable = CaptureAnchorPoints(ghost);
            singlePieceAnchorLocalPoints = (Vector3[])anchorLocalPoints.Clone();
            ClearLayout();
            if (!TrySuspendExternalBuildCamera())
            {
                if (showErrors)
                    ShowStatus(player, "BuildWorks: не удалось временно отключить Build Camera.");
                return false;
            }
            Camera camera = Camera.main;
            if (!camera)
            {
                RestoreExternalBuildCamera();
                if (showErrors)
                    ShowStatus(player, "BuildWorks: игровая камера не найдена.");
                return false;
            }
            state = PlacementState.Editing;
            passiveCursorActive = false;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            SaveAndUnlockCursor();
            BeginFreeView(camera, isolateBlueprint);
            Vector3 unalignedPosition = currentPosition;
            Quaternion unalignedRotation = currentRotation;
            if (!isolateBlueprint && TryAutoAlignToTouchingPiece())
                CommitTransformIfChanged(unalignedPosition, unalignedRotation);
            return true;
        }

        private void HandleFreeViewInput(Camera camera)
        {
            if (!camera)
            {
                return;
            }

            Vector2 mouse = Input.mousePosition;
            bool overPanel = MouseOverEditorPanel(mouse);
            if (Input.GetKeyDown(KeyCode.F) && !overPanel)
            {
                FocusFreeView(camera);
            }

            if (Input.GetMouseButtonDown(1) && !overPanel)
            {
                freeViewFlyLooking = true;
            }
            if (freeViewFlyLooking && Input.GetMouseButton(1))
            {
                Vector3 cameraPosition = camera.transform.position;
                freeViewYaw += Input.GetAxis("Mouse X") * 3f;
                freeViewPitch = Mathf.Clamp(
                    freeViewPitch - Input.GetAxis("Mouse Y") * 3f,
                    -85f,
                    85f);
                Quaternion rotation = Quaternion.Euler(freeViewPitch, freeViewYaw, 0f);
                freeViewPanOffset = cameraPosition +
                    rotation * Vector3.forward * freeViewDistance - currentPosition;

                Vector3 movement = Vector3.zero;
                if (Input.GetKey(KeyCode.W)) movement += rotation * Vector3.forward;
                if (Input.GetKey(KeyCode.S)) movement -= rotation * Vector3.forward;
                if (Input.GetKey(KeyCode.D)) movement += rotation * Vector3.right;
                if (Input.GetKey(KeyCode.A)) movement -= rotation * Vector3.right;
                if (Input.GetKey(KeyCode.E)) movement += Vector3.up;
                if (Input.GetKey(KeyCode.Q)) movement -= Vector3.up;
                if (movement.sqrMagnitude > 0.001f)
                {
                    float speed = Mathf.Max(1.5f, freeViewDistance * 0.5f) *
                        Time.unscaledDeltaTime;
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        speed *= 3f;
                    }
                    freeViewPanOffset += movement.normalized * speed;
                }
            }
            if (Input.GetMouseButtonUp(1))
            {
                freeViewFlyLooking = false;
            }

            if (!freeViewFlyLooking && Input.GetMouseButtonDown(2) && !overPanel)
            {
                freeViewDragging = true;
                freeViewPreviousMouse = mouse;
            }

            if (freeViewDragging && Input.GetMouseButton(2))
            {
                Vector2 delta = mouse - freeViewPreviousMouse;
                freeViewPreviousMouse = mouse;
                bool pan = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (pan)
                {
                    float scale = freeViewDistance * 0.0015f;
                    freeViewPanOffset -= camera.transform.right * (delta.x * scale);
                    freeViewPanOffset -= camera.transform.up * (delta.y * scale);
                }
                else
                {
                    freeViewYaw += delta.x * 0.25f;
                    freeViewPitch = Mathf.Clamp(freeViewPitch - delta.y * 0.25f, -85f, 85f);
                }
            }

            if (Input.GetMouseButtonUp(2))
            {
                freeViewDragging = false;
            }

            if (!overPanel && !freeViewFlyLooking)
            {
                float wheel = Input.mouseScrollDelta.y;
                if (Mathf.Abs(wheel) > 0.01f)
                {
                    bool countMode = !selectingBlueprint && gizmoMode == GizmoMode.Repeat &&
                        repeatAxis != GizmoAxis.None &&
                        !Input.GetKey(KeyCode.LeftControl) && !Input.GetKey(KeyCode.RightControl);
                    if (countMode)
                    {
                        AdjustCopyCount(wheel > 0f ? 1 : -1);
                    }
                    else
                    {
                        freeViewDistance = Mathf.Clamp(
                            freeViewDistance * Mathf.Pow(0.85f, wheel),
                            0.75f,
                            30f);
                    }
                }
            }
        }

        private void HandleGizmoInput(Camera camera)
        {
            if (!camera)
            {
                return;
            }

            if (sessionMode != SessionMode.BlueprintWorldPlacement &&
                blueprintWorkspaceEditPart == null && activeBlueprint != null &&
                Input.GetKeyDown(KeyCode.Tab))
            {
                bool backwards = Input.GetKey(KeyCode.LeftShift) ||
                    Input.GetKey(KeyCode.RightShift);
                CycleBlueprintEditTarget(backwards ? -1 : 1);
                return;
            }

            if (!freeViewFlyLooking && !Input.GetKey(KeyCode.LeftControl) &&
                !Input.GetKey(KeyCode.RightControl))
            {
                if (Input.GetKeyDown(KeyCode.X)) SelectAnchorConstraint(GizmoAxis.X);
                if (Input.GetKeyDown(KeyCode.Y)) SelectAnchorConstraint(GizmoAxis.Y);
                if (Input.GetKeyDown(KeyCode.Z)) SelectAnchorConstraint(GizmoAxis.Z);
            }

            Vector2 mouse = Input.mousePosition;
            if (Input.GetMouseButtonDown(0) && !MouseOverEditorPanel(mouse))
            {
                if (gizmoMode == GizmoMode.Guide && contourSupports.Count == 0)
                {
                    TryBuildContourFromClick(camera, mouse);
                    suppressPlacementFrame = Time.frameCount;
                    return;
                }

                int anchor = gizmo.HitTestAnchor(camera, anchorWorldPoints, mouse);
                if (anchor >= 0)
                {
                    if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    {
                        pinnedAnchorPoint = pinnedAnchorPoint == anchor ? -1 : anchor;
                        ClearSnapDrag();
                    }
                    else
                    {
                        bool magneticMove = Input.GetKey(KeyCode.LeftControl) ||
                            Input.GetKey(KeyCode.RightControl);
                        BeginAnchorDrag(camera, anchor, magneticMove);
                    }
                    return;
                }

                dragHandle = GizmoHandleKind.None;
                if (gizmoMode == GizmoMode.Repeat)
                {
                    dragAxis = gizmo.HitTestLayout(
                        camera, currentPosition, currentRotation, localSpace, mouse);
                    if (dragAxis != GizmoAxis.None)
                        dragHandle = GizmoHandleKind.Layout;
                }
                if (dragHandle == GizmoHandleKind.None && gizmoMode != GizmoMode.Guide)
                {
                    dragAxis = gizmo.HitTestMove(
                        camera, currentPosition, currentRotation, localSpace, mouse);
                    if (dragAxis != GizmoAxis.None)
                        dragHandle = GizmoHandleKind.Move;
                }
                if (dragHandle == GizmoHandleKind.None)
                {
                    dragAxis = gizmo.HitTestRotation(
                        camera, currentPosition, currentRotation, localSpace, mouse);
                    if (dragAxis != GizmoAxis.None)
                        dragHandle = GizmoHandleKind.Rotate;
                }
                if (dragAxis == GizmoAxis.None &&
                    sessionMode != SessionMode.BlueprintWorldPlacement &&
                    blueprintWorkspaceEditPart == null &&
                    activeBlueprint != null)
                {
                    int part = HitTestBlueprintPart(camera, mouse);
                    if (part >= 0)
                    {
                        SelectBlueprintEditTarget(part);
                        suppressPlacementFrame = Time.frameCount;
                        return;
                    }
                }
                if (dragAxis != GizmoAxis.None)
                {
                    BeginDrag(camera, mouse);
                }
            }

            if (dragAnchorPoint >= 0 && Input.GetMouseButton(0))
            {
                UpdateAnchorDrag(camera, mouse);
                RotateLocalLayoutFromDragStart();
                RebuildActiveLayout();
            }
            else if (dragAxis != GizmoAxis.None && Input.GetMouseButton(0))
            {
                if (dragHandle == GizmoHandleKind.Move)
                {
                    float pixels = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection);
                    float rawDistance = pixels * dragWorldUnitsPerPixel;
                    float distance = (float)PrecisionAdjustment.Quantize(
                        rawDistance,
                        Geometry.PrecisionStepPresets.Translation[translationStepIndex]);
                    Vector3 candidate = dragStartPosition + dragAxisWorld * distance;
                    Vector3 offset = candidate - basePosition;
                    float alongAxis = Vector3.Dot(offset, dragAxisWorld);
                    float limited = (float)PrecisionAdjustment.ClampOffset(alongAxis);
                    candidate += dragAxisWorld * (limited - alongAxis);
                    MovePieceWithoutMovingCameraFocus(candidate);
                    TryAutoAlignToTouchingPiece();
                    RebuildActiveLayout();
                }
                else if (dragHandle == GizmoHandleKind.Layout)
                {
                    if (planeDraggingSecond) UpdatePlaneDrag(mouse);
                    else UpdateRepeatDrag(mouse);
                }
                else if (dragHandle == GizmoHandleKind.Rotate)
                {
                    alignmentTargetVisible = false;
                    Vector3 pivotScreen = camera.WorldToScreenPoint(currentPosition);
                    float currentAngle = Mathf.Atan2(
                        mouse.y - pivotScreen.y,
                        mouse.x - pivotScreen.x) * Mathf.Rad2Deg;
                    float rawDegrees =
                        Mathf.DeltaAngle(dragStartScreenAngle, currentAngle) *
                        dragRotationScreenSign;
                    float degrees = (float)PrecisionAdjustment.Quantize(
                        rawDegrees,
                        Geometry.PrecisionStepPresets.Rotation[rotationStepIndex]);
                    Vector3 canonicalAxis = CanonicalAxis(dragAxis);
                    currentRotation = localSpace
                        ? dragStartRotation * Quaternion.AngleAxis(degrees, canonicalAxis)
                        : Quaternion.AngleAxis(degrees, canonicalAxis) * dragStartRotation;
                    RotateLocalLayoutFromDragStart();
                    RebuildActiveLayout();
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                bool completedRow = dragHandle == GizmoHandleKind.Layout &&
                    !planeDraggingSecond && repeatAxis != GizmoAxis.None &&
                    planeSecondCount < 2;
                bool completedPlane = dragHandle == GizmoHandleKind.Layout &&
                    planeDraggingSecond && planeSecondCount >= 2;
                if (dragAxis != GizmoAxis.None || dragAnchorPoint >= 0)
                {
                    if (dragHandle != GizmoHandleKind.Layout || dragAnchorPoint >= 0)
                    {
                        CommitTransformIfChanged(dragStartPosition, dragStartRotation);
                    }
                }
                if (dragHandle == GizmoHandleKind.Layout && repeatAxis != GizmoAxis.None)
                    repeatCountSecond = planeDraggingSecond && planeSecondCount >= 2;
                dragAxis = GizmoAxis.None;
                dragHandle = GizmoHandleKind.None;
                planeDraggingSecond = false;
                dragAnchorPoint = -1;
                dragConstraintActive = false;
                ClearSnapDrag();
                if (completedRow)
                    ShowStatus(Player.m_localPlayer, "BuildWorks: ряд " + copyCount +
                        "×1 готов. Для ширины потяни другую золотую стрелку.");
                else if (completedPlane)
                    ShowStatus(Player.m_localPlayer, "BuildWorks: площадка " + copyCount +
                        "×" + planeSecondCount + " готова.");
            }
        }

        private void HandleBlueprintSelectionInput(Camera camera, Vector2 mouse)
        {
            Piece hovered = null;
            if (camera && !freeViewFlyLooking && !MouseOverEditorPanel(mouse) &&
                TryGetPlacedPieceAt(camera, mouse, out Piece hit, out _) &&
                IsBlueprintPieceSelectable(hit)) hovered = hit;
            if (blueprintHoverPiece != hovered)
            {
                Piece previous = blueprintHoverPiece;
                blueprintHoverPiece = hovered;
                SetBlueprintPieceHighlight(previous, blueprintSelection.Contains(previous));
                SetBlueprintPieceHighlight(hovered, blueprintSelection.Contains(hovered));
            }
            if (!camera || freeViewFlyLooking) return;
            if (Input.GetMouseButtonDown(0) && !MouseOverEditorPanel(mouse))
            {
                blueprintSelectionDragging = true;
                blueprintSelectionStart = mouse;
                suppressPlacementFrame = Time.frameCount;
            }
            if (blueprintSelectionDragging && Input.GetMouseButton(0))
            {
                if ((mouse - blueprintSelectionStart).sqrMagnitude >= 64f)
                    hud.ShowSelectionBox(blueprintSelectionStart, mouse);
            }
            if (!blueprintSelectionDragging || !Input.GetMouseButtonUp(0)) return;

            blueprintSelectionDragging = false;
            hud.HideSelectionBox();
            bool remove = Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);
            if ((mouse - blueprintSelectionStart).sqrMagnitude < 64f)
            {
                if (TryGetPlacedPieceAt(camera, mouse, out Piece piece, out _) &&
                    IsBlueprintPieceSelectable(piece))
                    SetBlueprintPieceSelected(piece, remove, toggle: !remove);
                return;
            }
            SelectBlueprintArea(camera, blueprintSelectionStart, mouse, remove);
        }

        private void SelectBlueprintArea(
            Camera camera,
            Vector2 first,
            Vector2 second,
            bool remove)
        {
            Rect area = Rect.MinMaxRect(
                Mathf.Min(first.x, second.x),
                Mathf.Min(first.y, second.y),
                Mathf.Max(first.x, second.x),
                Mathf.Max(first.y, second.y));
            nearbyPieces.Clear();
            Piece.GetAllPiecesInRadius(
                camera.transform.position + camera.transform.forward * 48f,
                128f,
                nearbyPieces);
            List<Piece> visible = new List<Piece>();
            foreach (Piece piece in nearbyPieces)
            {
                if (!IsBlueprintPieceSelectable(piece) ||
                    !TryPieceScreenPoint(camera, piece, out Vector2 screen) ||
                    !area.Contains(screen) ||
                    !TryGetPlacedPieceAt(camera, screen, out Piece hit, out _) ||
                    hit != piece)
                    continue;
                visible.Add(piece);
            }
            Vector2 center = area.center;
            visible.Sort((left, right) =>
            {
                TryPieceScreenPoint(camera, left, out Vector2 leftScreen);
                TryPieceScreenPoint(camera, right, out Vector2 rightScreen);
                return (leftScreen - center).sqrMagnitude.CompareTo(
                    (rightScreen - center).sqrMagnitude);
            });
            foreach (Piece piece in visible)
                SetBlueprintPieceSelected(piece, remove, toggle: false);
        }

        private bool IsBlueprintPieceSelectable(Piece piece)
        {
            if (!piece || !piece.gameObject.activeInHierarchy ||
                placementGhost && (piece.gameObject == placementGhost ||
                piece.transform.IsChildOf(placementGhost.transform)))
                return false;
            return TryResolveBuildPiece(Player.m_localPlayer, PrefabName(piece), out Piece prefab) &&
                SupportsBlueprintWorkspacePiece(prefab);
        }

        private void SetBlueprintPieceSelected(Piece piece, bool remove, bool toggle)
        {
            int index = blueprintSelection.IndexOf(piece);
            bool selected = index >= 0;
            if (remove || toggle && selected)
            {
                if (selected)
                {
                    blueprintSelection.RemoveAt(index);
                    SetBlueprintPieceHighlight(piece, false);
                }
                return;
            }
            if (selected || blueprintSelection.Count >= CompositeBlueprintStore.MaximumParts)
                return;
            blueprintSelection.Add(piece);
            SetBlueprintPieceHighlight(piece, true);
        }

        private void SetBlueprintPieceHighlight(Piece piece, bool selected)
        {
            if (piece) blueprintHighlights.Set(piece.gameObject, selected, piece == blueprintHoverPiece);
        }
        private static bool TryPieceScreenPoint(
            Camera camera,
            Piece piece,
            out Vector2 screenPoint)
        {
            screenPoint = Vector2.zero;
            Renderer[] renderers = piece.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            Bounds bounds = renderers[0].bounds;
            for (int index = 1; index < renderers.Length; ++index)
                bounds.Encapsulate(renderers[index].bounds);
            Vector3 screen = camera.WorldToScreenPoint(bounds.center);
            if (screen.z <= 0f) return false;
            screenPoint = new Vector2(screen.x, screen.y);
            return true;
        }

        private void BeginAnchorDrag(Camera camera, int movingAnchor, bool magneticMove)
        {
            alignmentTargetVisible = false;
            dragHandle = GizmoHandleKind.None;
            dragStartPosition = currentPosition;
            dragStartRotation = currentRotation;
            CaptureDragLayoutState();
            dragMovingAnchorLocal = anchorLocalPoints[movingAnchor];
            dragSourceIsNative = movingAnchor >= NativeAnchorStart;
            dragConstraintActive = false;
            if (magneticMove)
            {
                dragAnchorPoint = movingAnchor;
                dragMagneticMove = true;
                RefreshSnapTargets();
                return;
            }

            int fixedAnchor = pinnedAnchorPoint >= 0
                ? pinnedAnchorPoint
                : movingAnchor < AnchorAdjustment.AnchorCount
                    ? AnchorAdjustment.OppositeAnchor(movingAnchor)
                    : -1;
            if (fixedAnchor < 0 || fixedAnchor == movingAnchor)
            {
                if (dragSourceIsNative)
                {
                    ShowStatus(Player.m_localPlayer,
                        "BuildWorks: для вращения vanilla snap сначала закрепи другую опору через Shift.");
                }
                return;
            }

            dragFixedAnchorLocal = anchorLocalPoints[fixedAnchor];
            dragFixedAnchorWorld = dragStartPosition + dragStartRotation * dragFixedAnchorLocal;
            dragMovingVector = anchorWorldPoints[movingAnchor] - dragFixedAnchorWorld;
            if (anchorConstraintAxis == GizmoAxis.None)
            {
                dragAnchorPlane = new Plane(camera.transform.forward, anchorWorldPoints[movingAnchor]);
            }
            else
            {
                dragConstraintAxisWorld = TransformGizmoView.AxisVector(
                    anchorConstraintAxis,
                    dragStartRotation,
                    localSpace).normalized;
                float axialDistance = Vector3.Dot(dragMovingVector, dragConstraintAxisWorld);
                dragConstraintCenter = dragFixedAnchorWorld +
                    dragConstraintAxisWorld * axialDistance;
                Vector3 radial = dragMovingVector -
                    dragConstraintAxisWorld * axialDistance;
                if (radial.sqrMagnitude < 0.0001f)
                {
                    return;
                }
                dragConstraintRadius = radial.magnitude;
                dragAnchorPlane = new Plane(dragConstraintAxisWorld, dragConstraintCenter);
                dragConstraintActive = true;
            }
            dragAnchorPoint = movingAnchor;
        }

        private void UpdateAnchorDrag(Camera camera, Vector2 mouse)
        {
            if (dragMagneticMove)
            {
                snapTargetVisible = TryFindSnapTarget(
                    camera,
                    mouse,
                    out snapTargetWorld,
                    out snapTargetIsNative);
                currentRotation = dragStartRotation;
                Vector3 candidate = snapTargetVisible
                    ? ToUnity(AnchorAdjustment.PositionForFixedAnchor(
                        ToGeometry(snapTargetWorld),
                        ToGeometry(dragMovingAnchorLocal),
                        ToGeometry(currentRotation)))
                    : dragStartPosition;
                MovePieceWithoutMovingCameraFocus(candidate);
                return;
            }

            Ray ray = camera.ScreenPointToRay(mouse);
            if (!dragAnchorPlane.Raycast(ray, out float distance))
            {
                return;
            }

            if (dragConstraintActive)
            {
                Vector3 targetRadial = ray.GetPoint(distance) - dragConstraintCenter;
                if (targetRadial.sqrMagnitude < 0.0001f)
                {
                    return;
                }
                float constrainedDegrees = (float)AnchorAdjustment.ConstrainedAngleDegrees(
                    ToGeometry(dragMovingVector),
                    ToGeometry(targetRadial),
                    ToGeometry(dragConstraintAxisWorld));
                float quantizedDegrees = (float)PrecisionAdjustment.Quantize(
                    constrainedDegrees,
                    Geometry.PrecisionStepPresets.Rotation[rotationStepIndex]);
                currentRotation = Quaternion.AngleAxis(
                    quantizedDegrees,
                    dragConstraintAxisWorld) * dragStartRotation;
                MovePieceWithoutMovingCameraFocus(ToUnity(AnchorAdjustment.PositionForFixedAnchor(
                    ToGeometry(dragFixedAnchorWorld),
                    ToGeometry(dragFixedAnchorLocal),
                    ToGeometry(currentRotation))));
                return;
            }

            Vector3 targetVector = ray.GetPoint(distance) - dragFixedAnchorWorld;
            if (targetVector.sqrMagnitude < 0.0001f || dragMovingVector.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion delta = Quaternion.FromToRotation(dragMovingVector, targetVector);
            delta.ToAngleAxis(out float degrees, out Vector3 axis);
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            float quantized = (float)PrecisionAdjustment.Quantize(
                degrees,
                Geometry.PrecisionStepPresets.Rotation[rotationStepIndex]);
            Quaternion adjustedDelta = Quaternion.AngleAxis(quantized, axis);
            currentRotation = adjustedDelta * dragStartRotation;
            MovePieceWithoutMovingCameraFocus(ToUnity(AnchorAdjustment.PositionForFixedAnchor(
                ToGeometry(dragFixedAnchorWorld),
                ToGeometry(dragFixedAnchorLocal),
                ToGeometry(currentRotation))));
        }

        private void RefreshSnapTargets()
        {
            snapTargetVisible = false;
            snapTargetIsNative = false;
            snapTargetWorld = Vector3.zero;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            snapTargets.Clear();
            freeSnapTargets.Clear();
            snapEdges.Clear();
            nativeSnapPoints.Clear();
            nativeSnapOwners.Clear();
            if (sessionMode == SessionMode.BlueprintEditor) return;
            Piece.GetSnapPoints(
                currentPosition,
                SnapSearchRadius,
                nativeSnapPoints,
                nativeSnapOwners);
            for (int index = 0; index < nativeSnapPoints.Count; ++index)
            {
                Transform point = nativeSnapPoints[index];
                if (point && (!placementGhost ||
                    (point != placementGhost.transform &&
                    !point.IsChildOf(placementGhost.transform))))
                {
                    snapTargets.Add(point.position);
                }
            }

            if (!meshSnapEnabled) return;

            nearbyPieces.Clear();
            Piece.GetAllPiecesInRadius(currentPosition, SnapSearchRadius, nearbyPieces);
            nearbyPieces.RemoveAll(piece => !piece);
            nearbyPieces.Sort((left, right) =>
                (left.transform.position - currentPosition).sqrMagnitude.CompareTo(
                    (right.transform.position - currentPosition).sqrMagnitude));
            foreach (Piece piece in nearbyPieces)
            {
                if (!piece || !piece.gameObject.activeInHierarchy ||
                    (placementGhost &&
                    (piece.gameObject == placementGhost ||
                    piece.transform.IsChildOf(placementGhost.transform))))
                {
                    continue;
                }

                AddMeshSnapEdges(piece.gameObject);
                if (!TryCaptureAnchorBounds(
                    piece.gameObject,
                    piece.transform.position,
                    piece.transform.rotation,
                    out AnchorBounds bounds))
                {
                    continue;
                }
                for (int anchor = 0; anchor < AnchorAdjustment.AnchorCount; ++anchor)
                {
                    if (freeSnapTargets.Count >= MaximumFreeSnapTargets) break;
                    freeSnapTargets.Add(piece.transform.position +
                        piece.transform.rotation * ToUnity(bounds.Anchor(anchor)));
                }
                if (snapEdges.Count >= MaximumSnapEdges &&
                    freeSnapTargets.Count >= MaximumFreeSnapTargets) break;
            }
        }

        private bool TryFindSnapTarget(
            Camera camera,
            Vector2 mouse,
            out Vector3 target,
            out bool nativeTarget)
        {
            bool hadLockedTarget = snapTargetVisible;
            Vector3 lockedTarget = snapTargetWorld;
            bool lockedTargetIsNative = snapTargetIsNative;
            target = Vector3.zero;
            nativeTarget = false;
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            if (hadLockedTarget)
            {
                Vector3 lockedScreen = camera.WorldToScreenPoint(lockedTarget);
                if (lockedScreen.z > 0f && Vector2.Distance(
                    mouse,
                    new Vector2(lockedScreen.x, lockedScreen.y)) <=
                    SnapReleaseScreenDistance)
                {
                    snapPreviewTargets.Add(lockedTarget);
                    snapPreviewNative.Add(lockedTargetIsNative);
                    target = lockedTarget;
                    nativeTarget = lockedTargetIsNative;
                    return true;
                }
            }
            foreach (Vector3 candidate in snapTargets)
                AddSnapPreviewTarget(camera, mouse, candidate, native: true);
            foreach (Vector3 candidate in freeSnapTargets)
                AddSnapPreviewTarget(camera, mouse, candidate, native: false);
            foreach (SnapEdge edge in snapEdges)
            {
                if (TransformGizmoView.TryClosestPointOnScreenSegment(
                    camera,
                    mouse,
                    edge.Start,
                    edge.End,
                    out float previewDistance,
                    out float previewPosition) &&
                    previewDistance <= SnapPreviewScreenRadius)
                {
                    AddSnapPreviewTarget(
                        camera,
                        mouse,
                        Vector3.Lerp(edge.Start, edge.End, previewPosition),
                        native: false);
                }
            }
            CullHiddenSnapPreviewTargets(camera);
            float bestDistance = SnapScreenDistance;
            bool found = false;
            for (int index = 0; index < snapTargets.Count; ++index)
            {
                Vector3 candidate = snapTargets[index];
                Vector3 screen = camera.WorldToScreenPoint(candidate);
                if (screen.z <= 0f)
                {
                    continue;
                }
                float distance = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
                if (distance < bestDistance && IsSnapPreviewTarget(candidate))
                {
                    bestDistance = distance;
                    target = candidate;
                    nativeTarget = true;
                    found = true;
                }
            }
            for (int index = 0; index < freeSnapTargets.Count; ++index)
            {
                Vector3 candidate = freeSnapTargets[index];
                Vector3 screen = camera.WorldToScreenPoint(candidate);
                if (screen.z <= 0f)
                {
                    continue;
                }
                float distance = Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
                if (distance < bestDistance && IsSnapPreviewTarget(candidate))
                {
                    bestDistance = distance;
                    target = candidate;
                    nativeTarget = false;
                    found = true;
                }
            }
            if (!found)
            {
                bestDistance = SnapEdgeScreenDistance;
                foreach (SnapEdge edge in snapEdges)
                {
                    if (TransformGizmoView.TryClosestPointOnScreenSegment(
                        camera,
                        mouse,
                        edge.Start,
                        edge.End,
                        out float distance,
                        out float edgePosition) &&
                        distance < bestDistance)
                    {
                        Vector3 edgeTarget = Vector3.Lerp(
                            edge.Start,
                            edge.End,
                            edgePosition);
                        if (!IsSnapPreviewTarget(edgeTarget)) continue;
                        bestDistance = distance;
                        target = edgeTarget;
                        nativeTarget = false;
                        found = true;
                    }
                }
            }
            return found;
        }

        private void AddSnapPreviewTarget(
            Camera camera,
            Vector2 mouse,
            Vector3 candidate,
            bool native)
        {
            Vector3 screen = camera.WorldToScreenPoint(candidate);
            Vector2 candidateScreen = new Vector2(screen.x, screen.y);
            float candidateDistance = (candidateScreen - mouse).sqrMagnitude;
            if (screen.z <= 0f || screen.x < 0f || screen.y < 0f ||
                screen.x > camera.pixelWidth || screen.y > camera.pixelHeight ||
                candidateDistance > SnapPreviewScreenRadius * SnapPreviewScreenRadius)
                return;
            int insertAt = snapPreviewTargets.Count;
            for (int index = 0; index < snapPreviewTargets.Count; ++index)
            {
                Vector3 existing = snapPreviewTargets[index];
                if ((existing - candidate).sqrMagnitude < 0.0025f)
                {
                    if (native) snapPreviewNative[index] = true;
                    return;
                }
                Vector3 existingScreen = camera.WorldToScreenPoint(existing);
                float existingDistance = (
                    new Vector2(existingScreen.x, existingScreen.y) - mouse).sqrMagnitude;
                if (insertAt == snapPreviewTargets.Count &&
                    candidateDistance < existingDistance)
                {
                    insertAt = index;
                }
            }
            if (snapPreviewTargets.Count >= MaximumSnapVisibilityCandidates &&
                insertAt == snapPreviewTargets.Count)
                return;
            snapPreviewTargets.Insert(insertAt, candidate);
            snapPreviewNative.Insert(insertAt, native);
            if (snapPreviewTargets.Count > MaximumSnapVisibilityCandidates)
            {
                snapPreviewTargets.RemoveAt(snapPreviewTargets.Count - 1);
                snapPreviewNative.RemoveAt(snapPreviewNative.Count - 1);
            }
        }

        private void CullHiddenSnapPreviewTargets(Camera camera)
        {
            for (int index = snapPreviewTargets.Count - 1; index >= 0; --index)
            {
                if (!TransformGizmoView.IsPointVisible(
                    camera,
                    snapPreviewTargets[index]))
                {
                    snapPreviewTargets.RemoveAt(index);
                    snapPreviewNative.RemoveAt(index);
                }
            }
            if (snapPreviewTargets.Count > MaximumSnapPreviewTargets)
            {
                snapPreviewTargets.RemoveRange(
                    MaximumSnapPreviewTargets,
                    snapPreviewTargets.Count - MaximumSnapPreviewTargets);
                snapPreviewNative.RemoveRange(
                    MaximumSnapPreviewTargets,
                    snapPreviewNative.Count - MaximumSnapPreviewTargets);
            }
        }

        private bool IsSnapPreviewTarget(Vector3 candidate)
        {
            foreach (Vector3 visible in snapPreviewTargets)
            {
                if ((visible - candidate).sqrMagnitude < 0.0025f) return true;
            }
            return false;
        }

        private bool AddMeshSnapEdges(GameObject target)
        {
            if (snapEdges.Count >= MaximumSnapEdges)
            {
                return true;
            }
            int initialCount = snapEdges.Count;
            lodRenderers.Clear();
            lodZeroRenderers.Clear();
            foreach (LODGroup group in target.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] levels = group.GetLODs();
                for (int level = 0; level < levels.Length; ++level)
                {
                    foreach (Renderer renderer in levels[level].renderers)
                    {
                        if (!renderer) continue;
                        lodRenderers.Add(renderer);
                        if (level == 0) lodZeroRenderers.Add(renderer);
                    }
                }
            }
            foreach (MeshRenderer renderer in target.GetComponentsInChildren<MeshRenderer>(true))
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                Mesh mesh = filter ? filter.sharedMesh : null;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    !mesh || (lodRenderers.Contains(renderer) &&
                    !lodZeroRenderers.Contains(renderer)))
                {
                    continue;
                }

                foreach (Edge3 edge in GetMeshFeatureEdges(mesh))
                {
                    snapEdges.Add(new SnapEdge(
                        filter.transform.TransformPoint(ToUnity(edge.Start)),
                        filter.transform.TransformPoint(ToUnity(edge.End))));
                    if (snapEdges.Count >= MaximumSnapEdges)
                    {
                        return true;
                    }
                }
            }
            return snapEdges.Count > initialCount;
        }

        private IReadOnlyList<Edge3> GetMeshFeatureEdges(Mesh mesh)
        {
            if (meshFeatureEdgeCache.TryGetValue(mesh, out IReadOnlyList<Edge3> cached))
            {
                return cached;
            }

            IReadOnlyList<Edge3> edges = EmptyFeatureEdges;
            try
            {
                long indexCount = 0;
                for (int subMesh = 0; subMesh < mesh.subMeshCount; ++subMesh)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    {
                        indexCount = long.MaxValue;
                        break;
                    }
                    indexCount += mesh.GetIndexCount(subMesh);
                }
                if (mesh.isReadable && mesh.vertexCount <= MaximumMeshVertices &&
                    indexCount / 3 <= MaximumMeshTriangles)
                {
                    Vector3[] vertices = mesh.vertices;
                    List<Point3> geometryVertices = new List<Point3>(vertices.Length);
                    foreach (Vector3 vertex in vertices)
                    {
                        geometryVertices.Add(ToGeometry(vertex));
                    }
                    edges = AnchorAdjustment.ExtractFeatureEdges(
                        geometryVertices,
                        mesh.triangles);
                }
            }
            catch (ArgumentException)
            {
                edges = EmptyFeatureEdges;
            }
            catch (InvalidOperationException)
            {
                edges = EmptyFeatureEdges;
            }
            catch (UnityException)
            {
                edges = EmptyFeatureEdges;
            }
            meshFeatureEdgeCache[mesh] = edges;
            return edges;
        }

        private void MovePieceWithoutMovingCameraFocus(Vector3 position)
        {
            if (position == currentPosition)
            {
                return;
            }
            Vector3 oldPosition = currentPosition;
            currentPosition = position;
            freeViewPanOffset = ToUnity(AnchorAdjustment.CompensateFocusOffset(
                ToGeometry(freeViewPanOffset),
                ToGeometry(oldPosition),
                ToGeometry(currentPosition)));
        }

        private void ClearSnapDrag()
        {
            dragMagneticMove = false;
            dragSourceIsNative = false;
            dragConstraintActive = false;
            snapTargetVisible = false;
            snapTargetIsNative = false;
            snapTargets.Clear();
            freeSnapTargets.Clear();
            snapPreviewTargets.Clear();
            snapPreviewNative.Clear();
            snapEdges.Clear();
        }

        private void BeginDrag(Camera camera, Vector2 mouse)
        {
            dragStartMouse = mouse;
            dragStartPosition = currentPosition;
            dragStartRotation = currentRotation;
            CaptureDragLayoutState();
            dragAxisWorld = TransformGizmoView.AxisVector(dragAxis, currentRotation, localSpace);
            Vector3 pivotScreen = camera.WorldToScreenPoint(currentPosition);
            if (dragHandle != GizmoHandleKind.Rotate)
            {
                if (dragHandle == GizmoHandleKind.Layout)
                {
                    planeDraggingSecond = repeatAxis != GizmoAxis.None &&
                        dragAxis != repeatAxis;
                }
                Vector3 endScreen = camera.WorldToScreenPoint(
                    currentPosition + dragAxisWorld * gizmo.Scale);
                Vector2 screenAxis = new Vector2(
                    endScreen.x - pivotScreen.x,
                    endScreen.y - pivotScreen.y);
                float pixels = Mathf.Max(1f, screenAxis.magnitude);
                dragScreenDirection = screenAxis / pixels;
                dragWorldUnitsPerPixel = gizmo.Scale / pixels;
            }
            else
            {
                dragStartScreenAngle = Mathf.Atan2(
                    mouse.y - pivotScreen.y,
                    mouse.x - pivotScreen.x) * Mathf.Rad2Deg;
                dragRotationScreenSign = (float)
                    PrecisionAdjustment.ScreenAlignedRotationDelta(
                        1.0,
                        Vector3.Dot(
                            dragAxisWorld,
                            camera.transform.position - currentPosition));
            }
        }

        private void UpdateRepeatDrag(Vector2 mouse)
        {
            float pixels = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection);
            float rawDistance = pixels * dragWorldUnitsPerPixel;
            float step = repeatDistribution == RepeatDistributionMode.Exact
                ? ExactRepeatSteps[exactRepeatStepIndex]
                : RepeatStepLength(dragAxisWorld);
            if (Mathf.Abs(rawDistance) < 0.05f)
            {
                RestoreDragLayoutState();
                return;
            }

            repeatAxis = dragAxis;
            repeatDraggedDistance = Mathf.Abs(rawDistance);
            if (repeatDistribution == RepeatDistributionMode.Fit)
            {
                step = Mathf.Abs(rawDistance) / Mathf.Max(1, copyCount - 1);
            }
            else
            {
                copyCount = Mathf.Clamp(
                    1 + Mathf.RoundToInt(Mathf.Abs(rawDistance) / step),
                    2,
                    ConstructionLayout.MaximumCopies);
            }
            repeatStep = dragAxisWorld * step * Mathf.Sign(rawDistance);
            RebuildRepeatPlan();
        }

        private void UpdatePlaneDrag(Vector2 mouse)
        {
            float pixels = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection);
            float rawDistance = pixels * dragWorldUnitsPerPixel;
            float stepLength = repeatDistribution == RepeatDistributionMode.Exact
                ? ExactRepeatSteps[exactRepeatStepIndex]
                : RepeatStepLength(dragAxisWorld);
            if (Mathf.Abs(rawDistance) < (repeatDistribution == RepeatDistributionMode.Fit ? 0.05f : stepLength * 0.5f))
            {
                RestoreDragLayoutState();
                planeDraggingSecond = true;
                return;
            }
            int count = repeatDistribution == RepeatDistributionMode.Fit
                ? Mathf.Max(2, dragStartPlaneSecondCount)
                : 1 + Mathf.RoundToInt(Mathf.Abs(rawDistance) / stepLength);
            planeSecondCount = Mathf.Clamp(
                count,
                2,
                Mathf.Max(2,
                    ConstructionLayout.MaximumCopies / Mathf.Max(2, copyCount)));
            if (repeatDistribution == RepeatDistributionMode.Fit)
                stepLength = Mathf.Abs(rawDistance) / Mathf.Max(1, planeSecondCount - 1);
            planeSecondStep = dragAxisWorld * stepLength * Mathf.Sign(rawDistance);
            planeDraggedDistance = Mathf.Abs(rawDistance);
            RebuildRepeatPlan();
        }

        private float RepeatStepLength(Vector3 axisWorld)
        {
            float nativeSpan = ProjectedAnchorSpan(
                axisWorld,
                NativeAnchorStart,
                anchorLocalPoints.Length);
            float span = nativeSpan > 0.01f
                ? nativeSpan
                : ProjectedAnchorSpan(axisWorld, 0, Math.Min(8, anchorLocalPoints.Length));
            return span > 0.01f
                ? span + RepeatSpacings[repeatSpacingIndex]
                : 1f;
        }

        private float ProjectedAnchorSpan(Vector3 axisWorld, int start, int end)
        {
            if (start < 0 || end <= start || end > anchorLocalPoints.Length)
                return 0f;
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int index = start; index < end; ++index)
            {
                float projected = Vector3.Dot(
                    currentRotation * anchorLocalPoints[index],
                    axisWorld);
                minimum = Mathf.Min(minimum, projected);
                maximum = Mathf.Max(maximum, projected);
            }
            return maximum - minimum;
        }

        private void RepeatHingeAnchors(out Vector3 back, out Vector3 front)
        {
            Vector3 directionWorld = repeatStep.normalized;
            if (!anchorBoundsAvailable ||
                anchorLocalPoints.Length <= AnchorAdjustment.CenterAnchorIndex)
            {
                back = -repeatStep * 0.5f;
                front = repeatStep * 0.5f;
                return;
            }

            Vector3 directionLocal = Quaternion.Inverse(currentRotation) * directionWorld;
            Vector3 center = anchorLocalPoints[AnchorAdjustment.CenterAnchorIndex];
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            for (int index = 0; index < AnchorAdjustment.AnchorCount; ++index)
            {
                float projected = Vector3.Dot(anchorLocalPoints[index], directionLocal);
                minimum = Mathf.Min(minimum, projected);
                maximum = Mathf.Max(maximum, projected);
            }
            if (maximum - minimum <= 0.01f)
            {
                back = -repeatStep * 0.5f;
                front = repeatStep * 0.5f;
                return;
            }
            float centerProjection = Vector3.Dot(center, directionLocal);
            back = currentRotation *
                (center + directionLocal * (minimum - centerProjection));
            front = currentRotation *
                (center + directionLocal * (maximum - centerProjection));
        }

        private void RebuildRepeatPlan()
        {
            placementPlan.Clear();
            placementPlanPieces.Clear();
            previewContacts.Clear();
            previewAims.Clear();
            expandedPlanLimited = false;
            repeatPlanError = null;
            Vector3 previewDirection = repeatStep.normalized *
                Mathf.Min(repeatStep.magnitude * 0.35f, 1f);
            TransformGizmoView.RepeatStepAxisAngle(repeatAxis, repeatStep, currentRotation,
                localSpace, repeatTurnDegrees, repeatPitchDegrees, repeatRollDegrees,
                out Vector3 rotationAxis, out float rotationDegrees);
            RepeatHingeAnchors(out Vector3 backAnchor, out Vector3 frontAnchor);
            IReadOnlyList<LayoutTransform3> samples;
            try
            {
                samples = GuidePathSampling.SampleRepeat(
                ToGeometry(currentPosition),
                ToGeometry(repeatStep),
                copyCount,
                repeatRise,
                ToGeometry(rotationAxis),
                rotationDegrees,
                repeatSymmetric,
                ToGeometry(backAnchor),
                ToGeometry(frontAnchor),
                repeatScaleStep);
            }
            catch (ArgumentOutOfRangeException)
            {
                repeatPlanError = "МАССИВ: уменьши шаг масштаба или количество — масштаб копии должен быть положительным";
                return;
            }
            int maximumRows = Mathf.Max(
                1,
                ConstructionLayout.MaximumCopies / Mathf.Max(1, samples.Count));
            if (planeSecondCount >= 2)
                planeSecondCount = Mathf.Min(planeSecondCount, maximumRows);
            int rows = planeSecondCount >= 2 ? planeSecondCount : 1;
            for (int row = 0; row < rows; ++row)
            {
                int rowIndex = repeatSymmetric
                    ? row == 0 ? 0 : (row & 1) != 0 ? (row + 1) / 2 : -row / 2
                    : row;
                Vector3 rowOffset = planeSecondStep * rowIndex;
                foreach (LayoutTransform3 sample in samples)
                {
                    Vector3 position = ToUnity(sample.Position) + rowOffset;
                    Quaternion rotation = Quaternion.AngleAxis(
                        (float)sample.IncrementalRotationDegrees,
                        ToUnity(sample.RotationAxis)) * currentRotation;
                    if (!TryAddBlueprintLayoutInstance(
                        position,
                        rotation,
                        (float)sample.UniformScale))
                    {
                        placementPlan.Clear();
                        placementPlanPieces.Clear();
                        previewContacts.Clear();
                        previewAims.Clear();
                        return;
                    }
                    previewContacts.Add(position);
                    previewAims.Add(position + previewDirection);
                }
            }
        }

        private void RebuildActiveLayout()
        {
            if (blueprintWorkspaceEditPart != null) return;
            if (gizmoMode == GizmoMode.Repeat && repeatAxis != GizmoAxis.None)
                RebuildRepeatPlan();
            else if (gizmoMode == GizmoMode.Guide && contourSupports.Count > 0)
            {
                ContourSupport seed = contourSupports[contourSeedSupportIndex];
                Quaternion inverse = Quaternion.Inverse(seed.Rotation);
                contourGhostPositionLocal = inverse * (currentPosition - seed.Position);
                contourGhostRotationLocal = inverse * currentRotation;
                RebuildContourPlan();
            }
            else if (activeBlueprint != null)
                RebuildBlueprintPlan();
        }

        private void RotateLocalLayoutFromDragStart()
        {
            if (!localSpace || gizmoMode != GizmoMode.Repeat ||
                repeatAxis == GizmoAxis.None)
                return;
            Quaternion delta = currentRotation * Quaternion.Inverse(dragStartRotation);
            repeatStep = delta * dragStartRepeatStep;
            planeSecondStep = delta * dragStartPlaneSecondStep;
        }

        private void CaptureDragLayoutState()
        {
            dragStartRepeatAxis = repeatAxis;
            dragStartRepeatStep = repeatStep;
            dragStartRepeatDraggedDistance = repeatDraggedDistance;
            dragStartCopyCount = copyCount;
            dragStartPlaneSecondStep = planeSecondStep;
            dragStartPlaneSecondCount = planeSecondCount;
            dragStartPlaneDistance = planeDraggedDistance;
        }

        private void RestoreDragLayoutState()
        {
            repeatAxis = dragStartRepeatAxis;
            repeatStep = dragStartRepeatStep;
            repeatDraggedDistance = dragStartRepeatDraggedDistance;
            copyCount = dragStartCopyCount;
            planeSecondStep = dragStartPlaneSecondStep;
            planeSecondCount = dragStartPlaneSecondCount;
            planeDraggedDistance = dragStartPlaneDistance;
            planeDraggingSecond = false;
            if (gizmoMode == GizmoMode.Repeat && repeatAxis == GizmoAxis.None)
            {
                placementPlan.Clear();
                placementPlanPieces.Clear();
                previewContacts.Clear();
                previewAims.Clear();
                previews.Hide();
                if (activeBlueprint != null) RebuildBlueprintPlan();
                return;
            }
            RebuildActiveLayout();
        }

        private void TryBuildContourFromClick(Camera camera, Vector2 mouse)
        {
            UpdateContourHover(camera, mouse, force: true);
            if (contourHoverSupports.Count < 2)
            {
                ShowStatus(Player.m_localPlayer,
                    string.IsNullOrEmpty(contourHoverError)
                        ? "BuildWorks: щёлкни подсвеченную цепь или кольцо."
                        : contourHoverError);
                return;
            }

            contourSupports.Clear();
            contourSupports.AddRange(contourHoverSupports);
            contourPathPointLocal = contourHoverPathPointLocal;
            contourClosed = contourHoverClosed;

            contourSeedSupportIndex = 0;
            float nearestDistance = float.PositiveInfinity;
            for (int index = 0; index < contourSupports.Count; ++index)
            {
                float distance = (contourSupports[index].Position - currentPosition).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearestDistance = distance;
                contourSeedSupportIndex = index;
            }
            ContourSupport reference = contourSupports[contourSeedSupportIndex];
            Quaternion inverseReference = Quaternion.Inverse(reference.Rotation);
            contourGhostPositionLocal = inverseReference *
                (currentPosition - reference.Position);
            contourGhostRotationLocal = inverseReference * currentRotation;

            ClearContourHover();
            RebuildContourPlan();
            ShowStatus(Player.m_localPlayer,
                "BuildWorks: " + (contourClosed ? "замкнутый" : "открытый") +
                " контур найден — " + contourSupports.Count +
                " деталей; исходник связан с ближайшей опорой.");
        }

        private void UpdateContourHover(
            Camera camera,
            Vector2 mouse,
            bool force = false)
        {
            if (gizmoMode != GizmoMode.Guide || contourSupports.Count > 0 ||
                freeViewFlyLooking || MouseOverEditorPanel(mouse))
            {
                ClearContourHover();
                return;
            }
            if (!force && Time.unscaledTime < nextContourHoverUpdate) return;
            nextContourHoverUpdate = Time.unscaledTime + 0.08f;
            if (!TryFindContourCandidate(
                camera,
                mouse,
                contourHoverSupports,
                out contourHoverPathPointLocal,
                out contourHoverClosed,
                out contourHoverError))
            {
                contourHoverPath.Clear();
                return;
            }
            RebuildContourPath(
                contourHoverSupports,
                contourHoverPathPointLocal,
                contourHoverClosed,
                contourHoverPath);
        }

        private bool TryFindContourCandidate(
            Camera camera,
            Vector2 mouse,
            List<ContourSupport> orderedSupports,
            out Vector3 pathPointLocal,
            out bool closed,
            out string error)
        {
            orderedSupports.Clear();
            pathPointLocal = Vector3.zero;
            closed = false;
            error = null;
            if (!TryGetPlacedPieceAt(camera, mouse, out Piece seed, out _))
            {
                error = "BuildWorks: наведи курсор на установленную деталь цепи или кольца.";
                return false;
            }

            if (!TrySelectContourEdge(
                seed,
                camera,
                mouse,
                out Edge3 contourEdge,
                out pathPointLocal))
            {
                error = "BuildWorks: у выбранного ребра нет пары штатных точек соединения.";
                return false;
            }

            nearbyPieces.Clear();
            Piece.GetAllPiecesInRadius(seed.transform.position, ContourSearchRadius, nearbyPieces);
            if (!nearbyPieces.Contains(seed)) nearbyPieces.Add(seed);
            contourCandidates.Clear();
            int seedIndex = -1;
            foreach (Piece piece in nearbyPieces)
            {
                if (!piece || !piece.gameObject.activeInHierarchy ||
                    piece.m_name != seed.m_name ||
                    placementGhost && (piece.gameObject == placementGhost ||
                    piece.transform.IsChildOf(placementGhost.transform)) ||
                    !TryCreateContourSupport(piece, contourEdge, out ContourSupport support))
                    continue;
                if (piece == seed) seedIndex = contourCandidates.Count;
                contourCandidates.Add(support);
            }
            if (seedIndex < 0 || contourCandidates.Count < 2)
            {
                error = "BuildWorks: рядом не найдена цепь минимум из двух одинаковых деталей.";
                return false;
            }

            Point3[][] connectionPoints = new Point3[contourCandidates.Count][];
            for (int index = 0; index < contourCandidates.Count; ++index)
            {
                Vector3[] source = contourCandidates[index].ConnectionPoints;
                connectionPoints[index] = new Point3[source.Length];
                for (int point = 0; point < source.Length; ++point)
                    connectionPoints[index][point] = ToGeometry(source[point]);
            }
            IReadOnlyList<int> contour = ConstructionLayout.OrderConnectedContour(
                connectionPoints,
                seedIndex,
                out closed);
            if (!closed)
            {
                IReadOnlyList<int> touching = ConstructionLayout.OrderTouchingContour(
                    connectionPoints,
                    seedIndex,
                    out bool touchingClosed);
                if (touching.Count > contour.Count ||
                    touching.Count == contour.Count && touchingClosed)
                {
                    contour = touching;
                    closed = touchingClosed;
                }
            }
            if (contour.Count < 2)
            {
                error = "BuildWorks: не найдена простая цепь или петля без развилок.";
                return false;
            }
            for (int index = 0; index < contour.Count; ++index)
                orderedSupports.Add(contourCandidates[contour[index]]);
            return true;
        }

        private bool TryGetPlacedPieceAt(
            Camera camera,
            Vector2 mouse,
            out Piece piece,
            out Vector3 hitPoint)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                camera.ScreenPointToRay(mouse),
                200f,
                ~0,
                QueryTriggerInteraction.Ignore);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (RaycastHit hit in hits)
            {
                Piece candidate = hit.collider
                    ? hit.collider.GetComponentInParent<Piece>()
                    : null;
                if (!candidate || !candidate.gameObject.activeInHierarchy ||
                    placementGhost && (candidate.gameObject == placementGhost ||
                    candidate.transform.IsChildOf(placementGhost.transform)))
                    continue;
                piece = candidate;
                hitPoint = hit.point;
                return true;
            }
            piece = null;
            hitPoint = Vector3.zero;
            return false;
        }

        private bool TryCreateContourSupport(
            Piece piece,
            Edge3 localEdge,
            out ContourSupport support)
        {
            support = default;
            Vector3 first = piece.transform.TransformPoint(ToUnity(localEdge.Start));
            Vector3 second = piece.transform.TransformPoint(ToUnity(localEdge.End));
            if (!IsFinite(first) || !IsFinite(second)) return false;
            support = new ContourSupport(
                piece.transform.position,
                piece.transform.rotation,
                new[] { first, second });
            return true;
        }

        private static bool TrySelectContourEdge(
            Piece piece,
            Camera camera,
            Vector2 mouse,
            out Edge3 selected,
            out Vector3 selectedPoint)
        {
            selected = default;
            selectedPoint = Vector3.zero;
            List<Transform> points = new List<Transform>();
            piece.GetSnapPoints(points);
            List<Point3> native = new List<Point3>(points.Count);
            foreach (Transform point in points)
            {
                if (point && !VanillaMidpointSnapPoints.IsGenerated(point))
                    native.Add(ToGeometry(piece.transform.InverseTransformPoint(point.position)));
            }
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(native);
            if (edges.Count == 0) return false;

            float bestDistance = ContourEdgeScreenDistance * ContourEdgeScreenDistance;
            for (int index = 0; index < edges.Count; ++index)
            {
                Vector3 start = ToUnity(edges[index].Start);
                Vector3 end = ToUnity(edges[index].End);
                Vector3 startWorld = piece.transform.TransformPoint(start);
                Vector3 endWorld = piece.transform.TransformPoint(end);
                Vector3 startScreen = camera.WorldToScreenPoint(startWorld);
                Vector3 endScreen = camera.WorldToScreenPoint(endWorld);
                if (startScreen.z <= 0f || endScreen.z <= 0f ||
                    !TransformGizmoView.IsPointVisible(
                        camera,
                        (startWorld + endWorld) * 0.5f))
                    continue;
                Vector2 direction = new Vector2(
                    endScreen.x - startScreen.x,
                    endScreen.y - startScreen.y);
                float lengthSquared = direction.sqrMagnitude;
                if (lengthSquared < 0.000001f) continue;
                float position = Mathf.Clamp01(
                    Vector2.Dot(mouse - new Vector2(startScreen.x, startScreen.y), direction) /
                    lengthSquared);
                Vector2 closest = new Vector2(startScreen.x, startScreen.y) +
                    direction * position;
                float distance = (mouse - closest).sqrMagnitude;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                selected = edges[index];
                float perspectivePosition = (float)AnchorAdjustment.PerspectiveSegmentParameter(
                    position,
                    startScreen.z,
                    endScreen.z);
                selectedPoint = Vector3.Lerp(start, end, perspectivePosition);
            }
            return bestDistance < ContourEdgeScreenDistance * ContourEdgeScreenDistance;
        }

        private void RebuildContourPlan()
        {
            placementPlan.Clear();
            placementPlanPieces.Clear();
            previewContacts.Clear();
            previewAims.Clear();
            expandedPlanLimited = false;
            RebuildContourPath(
                contourSupports,
                contourPathPointLocal,
                contourClosed,
                contourPath);
            if (contourSupports.Count == 0) return;

            for (int placementIndex = 0; placementIndex < contourSupports.Count;
                ++placementIndex)
            {
                int supportIndex = placementIndex == 0
                    ? contourSeedSupportIndex
                    : placementIndex - 1 < contourSeedSupportIndex
                        ? placementIndex - 1
                        : placementIndex;
                ContourSupport support = contourSupports[supportIndex];
                Quaternion rotation = support.Rotation * contourGhostRotationLocal;
                Vector3 position = support.Position +
                    support.Rotation * contourGhostPositionLocal;
                Vector3 pathPoint = support.Position +
                    support.Rotation * contourPathPointLocal;
                if (!TryAddBlueprintLayoutInstance(position, rotation)) return;
                previewContacts.Add(pathPoint);
                previewAims.Add(pathPoint + rotation * Vector3.up * 0.65f);
            }
        }

        private void RebuildBlueprintPlan()
        {
            CommitBlueprintEditTarget();
            placementPlan.Clear();
            placementPlanPieces.Clear();
            previewContacts.Clear();
            previewAims.Clear();
            expandedPlanLimited = false;
            TryAddPlacementInstance(blueprintRootPosition, blueprintRootRotation);
        }

        private bool TryAddBlueprintLayoutInstance(
            Vector3 pivotPosition,
            Quaternion rotation,
            float uniformScale = 1f)
        {
            Vector3 rootPosition = activeBlueprint == null
                ? pivotPosition
                : pivotPosition - rotation * (blueprintPivotLocal * uniformScale);
            return TryAddPlacementInstance(rootPosition, rotation, uniformScale);
        }

        private bool TryAddPlacementInstance(Vector3 position, Quaternion rotation, float uniformScale = 1f)
        {
            try
            {
                if (activeBlueprint == null)
                    BlueprintEditorPart.NormalizeScale(new Point3(uniformScale, uniformScale, uniformScale), nameof(uniformScale));
                else
                    foreach (CompositeBlueprintStore.Part part in activeBlueprint.parts)
                        BlueprintEditorPart.NormalizeScale(ToGeometry(part.scale.ToVector3()) * uniformScale, nameof(uniformScale));
            }
            catch (ArgumentOutOfRangeException)
            {
                repeatPlanError = "МАССИВ: масштаб каждой детали должен оставаться в пределах 1–400%; уменьши шаг или количество";
                return false;
            }
            if (activeBlueprint == null)
            {
                placementPlan.Add(new TransformSnapshot(position, rotation, Vector3.one * uniformScale));
                placementPlanPieces.Add(selectedPiece);
                return true;
            }
            if (activeBlueprintPieces.Count != activeBlueprint.parts.Count)
                return false;
            if (placementPlan.Count + activeBlueprint.parts.Count > MaximumExpandedPlacements)
            {
                expandedPlanLimited = true;
                return false;
            }
            for (int index = 0; index < activeBlueprint.parts.Count; ++index)
            {
                CompositeBlueprintStore.Part part = activeBlueprint.parts[index];
                Vector3 localPosition = part.position.ToVector3();
                Quaternion localRotation = part.rotation.ToQuaternion();
                placementPlan.Add(new TransformSnapshot(
                    position + rotation * (localPosition * uniformScale),
                    rotation * localRotation,
                    ToUnity(BlueprintEditorPart.NormalizeScale(
                        ToGeometry(part.scale.ToVector3()) * uniformScale, nameof(uniformScale)))));
                placementPlanPieces.Add(activeBlueprintPieces[index]);
            }
            return true;
        }

        private static void RebuildContourPath(
            IReadOnlyList<ContourSupport> supports,
            Vector3 pathPointLocal,
            bool closed,
            List<Vector3> path)
        {
            path.Clear();
            for (int index = 0; index < supports.Count; ++index)
            {
                ContourSupport support = supports[index];
                path.Add(support.Position + support.Rotation * pathPointLocal);
            }
            if (closed && path.Count >= 3) path.Add(path[0]);
        }

        private void ClearContourHover()
        {
            contourHoverSupports.Clear();
            contourHoverPath.Clear();
            contourHoverClosed = false;
            contourHoverError = null;
        }

        private void AdjustCopyCount(int change)
        {
            if (repeatCountSecond && planeSecondStep.sqrMagnitude > 0.000001f)
            {
                planeSecondCount = Mathf.Clamp(planeSecondCount + Math.Sign(change), 1,
                    Mathf.Max(1, ConstructionLayout.MaximumCopies / Mathf.Max(2, copyCount)));
                if (repeatDistribution == RepeatDistributionMode.Fit)
                    planeSecondStep = planeSecondStep.normalized *
                        (planeDraggedDistance / Mathf.Max(1, planeSecondCount - 1));
                RebuildRepeatPlan();
                return;
            }
            copyCount = Mathf.Clamp(
                copyCount + Math.Sign(change),
                2,
                Mathf.Max(2, ConstructionLayout.MaximumCopies / Mathf.Max(1, planeSecondCount)));
            if (gizmoMode == GizmoMode.Repeat && repeatAxis != GizmoAxis.None)
            {
                if (repeatDistribution == RepeatDistributionMode.Fit)
                {
                    repeatStep = repeatStep.normalized *
                        (repeatDraggedDistance / Mathf.Max(1, copyCount - 1));
                }
                RebuildRepeatPlan();
            }
        }

        private void SaveCurrentBlueprint()
        {
            if (sessionMode == SessionMode.BlueprintEditor)
            {
                SaveAndCloseBlueprintEditor();
                return;
            }
            if (!selectingBlueprint)
            {
                BeginBlueprintSelection();
                return;
            }
            if (!TrySaveWorldSelection(out CompositeBlueprintStore.Blueprint saved, out string error))
            {
                ShowStatus(Player.m_localPlayer, "BuildWorks: " + error);
                return;
            }
            Finish();
            RefreshBlueprintEditorLibrary();
            ShowStatus(Player.m_localPlayer, "BuildWorks: " + saved.name +
                " сохранён в ЧЕРТЕЖИ → " + saved.category + ". Детали мира сохранены на месте.");
        }

        private bool TrySaveWorldSelection(out CompositeBlueprintStore.Blueprint saved, out string error)
        {
            saved = null;
            PruneBlueprintSelection();
            error = "выбери от 2 до " + CompositeBlueprintStore.MaximumParts + " деталей";
            if (blueprintSelection.Count < 2 ||
                blueprintSelection.Count > CompositeBlueprintStore.MaximumParts) return false;
            Quaternion rotation = blueprintSelection[0].transform.rotation;
            Quaternion inverse = Quaternion.Inverse(rotation);
            Vector3 origin = blueprintSelection[0].transform.position;
            var parts = new List<CompositeBlueprintStore.Part>(blueprintSelection.Count);
            var snapSets = new List<IReadOnlyList<Point3>>(blueprintSelection.Count);
            foreach (Piece piece in blueprintSelection)
            {
                if (!IsBlueprintPieceSelectable(piece) || !TryResolveBuildPiece(
                    Player.m_localPlayer, PrefabName(piece), out Piece prefab))
                {
                    error = "выбранная деталь больше недоступна в молотке";
                    return false;
                }
                Vector3 baseScale = prefab.transform.localScale;
                Vector3 worldScale = piece.transform.lossyScale;
                CompositeBlueprintStore.VectorData scale;
                try { scale = new CompositeBlueprintStore.VectorData(RelativeBlueprintScale(worldScale, baseScale)); }
                catch (ArgumentException)
                {
                    error = "масштаб детали " + PrefabName(piece) + " вне поддерживаемого диапазона";
                    return false;
                }
                ZNetView netView = prefab.GetComponent<ZNetView>();
                if (!CompositeBlueprintStore.ValidScale(scale) ||
                    scale.ToVector3() != Vector3.one && (!netView || !netView.m_syncInitialScale))
                {
                    error = "масштаб детали " + PrefabName(piece) + " нельзя сохранить штатно";
                    return false;
                }
                parts.Add(new CompositeBlueprintStore.Part
                {
                    prefabName = PrefabName(piece),
                    position = new CompositeBlueprintStore.VectorData(inverse * (piece.transform.position - origin)),
                    rotation = new CompositeBlueprintStore.QuaternionData(inverse * piece.transform.rotation),
                    scale = scale
                });
                var points = new List<Transform>();
                piece.GetSnapPoints(points);
                var localPoints = new List<Point3>(points.Count);
                foreach (Transform point in points)
                    if (point) localPoints.Add(ToGeometry(inverse * (point.position - origin)));
                snapSets.Add(localPoints);
            }
            var anchors = new List<CompositeBlueprintStore.VectorData>();
            foreach (Point3 point in AnchorAdjustment.ExternalCompositeSnapPoints(snapSets, tolerance: 0.0001))
            {
                if (anchors.Count >= MaximumBlueprintSourceNativeSnapPoints) break;
                anchors.Add(new CompositeBlueprintStore.VectorData(ToUnity(point)));
            }
            return blueprintStore.TrySave(parts, anchors, out saved, out error);
        }

        internal static Vector3 RelativeBlueprintScale(Vector3 worldScale, Vector3 sourceScale) =>
            ToUnity(BlueprintEditorPart.NormalizeScale(new Point3(
                worldScale.x / sourceScale.x, worldScale.y / sourceScale.y, worldScale.z / sourceScale.z),
                nameof(worldScale)));

        private void BeginBlueprintSelection()
        {
            ClearBlueprintSelection();
            selectingBlueprint = true;
            blueprintSelectionDragging = false;
            ClearLayout();
            alignmentTargetVisible = false;
            ClearSnapDrag();
            ShowStatus(Player.m_localPlayer,
                "BuildWorks: выбирай детали ЛКМ или рамкой; Ctrl удаляет из выбора.");
        }

        private void EndBlueprintSelection(bool clearSelection)
        {
            selectingBlueprint = false;
            blueprintSelectionDragging = false;
            Piece previousHover = blueprintHoverPiece;
            blueprintHoverPiece = null;
            SetBlueprintPieceHighlight(previousHover, false);
            hud.HideSelectionBox();
            if (clearSelection) ClearBlueprintSelection();
        }

        private void ClearBlueprintSelection()
        {
            blueprintHoverPiece = null;
            blueprintHighlights.Clear();
            blueprintSelection.Clear();
        }

        private void PruneBlueprintSelection()
        {
            for (int index = blueprintSelection.Count - 1; index >= 0; --index)
                if (!blueprintSelection[index]) blueprintSelection.RemoveAt(index);
        }

        private bool ActivateBlueprint(CompositeBlueprintStore.Blueprint blueprint)
        {
            if (blueprint == null || blueprint.parts == null || blueprint.parts.Count < 2)
                return false;
            Player player = Player.m_localPlayer;
            if (!TryResolveBlueprintPieces(player, blueprint, out List<Piece> pieces,
                out string missingPrefab))
            {
                ShowStatus(player,
                    "BuildWorks: в текущем молотке нет детали " + missingPrefab + ".");
                return false;
            }

            Vector3 rootPosition = currentPosition;
            Quaternion rootRotation = currentRotation;
            int frameIndex = CompositeBlueprintStore.FramePartIndex(blueprint);
            if (!SelectBuildPiece(player, pieces[frameIndex]))
            {
                ShowStatus(player, "BuildWorks: не удалось выбрать опорную деталь чертежа.");
                return false;
            }
            currentPosition = rootPosition;
            currentRotation = rootRotation;
            if (!CaptureSelectedPieceAnchors())
            {
                ShowStatus(player,
                    "BuildWorks: у опорной детали чертежа не удалось определить границы.");
                return false;
            }

            activeBlueprint = blueprint;
            blueprintCompositeFrame = !CompositeBlueprintStore.HasExplicitFrame(blueprint);
            activeBlueprintPieces.Clear();
            activeBlueprintPieces.AddRange(pieces);
            originalBlueprintParts = CompositeBlueprintStore.CloneParts(blueprint.parts);
            originalBlueprintAnchors = new List<CompositeBlueprintStore.VectorData>(
                blueprint.anchors);
            blueprintRootPosition = currentPosition;
            blueprintRootRotation = currentRotation;
            blueprintPivotLocal = Vector3.zero;
            blueprintEditPartIndex = -1;
            blueprintPartsDirty = false;
            gizmoMode = GizmoMode.Move;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            ClearSnapDrag();
            if (!ApplyBlueprintAnchors())
            {
                activeBlueprint = null;
                activeBlueprintPieces.Clear();
                RestoreSinglePieceAnchors();
                ShowStatus(Player.m_localPlayer,
                    "BuildWorks: у чертежа не удалось восстановить общие границы.");
                return false;
            }
            ClearLayout();
            RebuildBlueprintPlan();
            if (isolatedEditorView) CreateEditorPlatform(BlueprintWorkspaceFloorY());
            if (isolatedEditorView && !SelectBlueprintEditTarget(frameIndex))
            {
                RestoreBlueprintDraft();
                activeBlueprint = null;
                activeBlueprintPieces.Clear();
                ShowStatus(Player.m_localPlayer,
                    "BuildWorks: не удалось выбрать опорную деталь для редактирования.");
                return false;
            }
            ShowStatus(Player.m_localPlayer,
                "BuildWorks: загружена " + blueprint.name + " — " +
                blueprint.parts.Count + " деталей; клик или Tab переключает детали.");
            return true;
        }

        private bool SelectBlueprintEditTarget(int index)
        {
            if (activeBlueprint == null || activeBlueprintPieces.Count != activeBlueprint.parts.Count ||
                index < -1 || index >= activeBlueprint.parts.Count)
                return false;
            if (blueprintEditPartIndex == index && index >= 0) return true;

            ClearLayout();
            blueprintEditPartIndex = index;
            gizmoMode = GizmoMode.Move;
            Player player = Player.m_localPlayer;
            int pieceIndex = index >= 0
                ? index
                : CompositeBlueprintStore.FramePartIndex(activeBlueprint);
            if (!SelectBuildPiece(player, activeBlueprintPieces[pieceIndex])) return false;

            if (index >= 0)
            {
                CompositeBlueprintStore.Part part = activeBlueprint.parts[index];
                currentPosition = blueprintRootPosition + blueprintRootRotation *
                    part.position.ToVector3();
                currentRotation = blueprintRootRotation * part.rotation.ToQuaternion();
                placementGhost.transform.SetPositionAndRotation(currentPosition, currentRotation);
                placementGhost.transform.localScale = Vector3.Scale(
                    selectedPiece.transform.localScale, part.scale.ToVector3());
                anchorBoundsAvailable = CaptureAnchorPoints(placementGhost);
                if (!anchorBoundsAvailable) return false;
                singlePieceAnchorLocalPoints = (Vector3[])anchorLocalPoints.Clone();
            }
            else
            {
                currentPosition = blueprintRootPosition + blueprintRootRotation * blueprintPivotLocal;
                currentRotation = blueprintRootRotation;
                RebuildBlueprintPlan();
                ApplyEditingGhostTransform();
                anchorBoundsAvailable = ApplyBlueprintAnchors();
                if (!anchorBoundsAvailable) return false;
            }

            basePosition = currentPosition;
            baseRotation = currentRotation;
            baseSnapshot = CaptureHistorySnapshot();
            history.Reset(baseSnapshot);
            pinnedAnchorPoint = -1;
            anchorConstraintAxis = GizmoAxis.None;
            ClearSnapDrag();
            return true;
        }

        private void CycleBlueprintEditTarget(int direction)
        {
            if (activeBlueprint == null || activeBlueprint.parts.Count == 0) return;
            int count = activeBlueprint.parts.Count + 1;
            int current = blueprintEditPartIndex + 1;
            int next = (current + Math.Sign(direction) + count) % count;
            if (!SelectBlueprintEditTarget(next - 1))
                ShowStatus(Player.m_localPlayer,
                    "BuildWorks: не удалось переключить редактируемую деталь.");
        }

        private void CommitBlueprintEditTarget()
        {
            if (activeBlueprint == null) return;
            if (blueprintEditPartIndex < 0)
            {
                blueprintRootPosition = currentPosition - currentRotation * blueprintPivotLocal;
                blueprintRootRotation = currentRotation;
                return;
            }

            CompositeBlueprintStore.Part part = activeBlueprint.parts[blueprintEditPartIndex];
            Quaternion inverse = Quaternion.Inverse(blueprintRootRotation);
            Vector3 localPosition = inverse * (currentPosition - blueprintRootPosition);
            Quaternion localRotation = inverse * currentRotation;
            Vector3 previousPosition = part.position.ToVector3();
            Quaternion previousRotation = part.rotation.ToQuaternion();
            if (!TransformChanged(previousPosition, previousRotation, localPosition, localRotation))
                return;
            part.position = new CompositeBlueprintStore.VectorData(localPosition);
            part.rotation = new CompositeBlueprintStore.QuaternionData(localRotation);
            blueprintPartsDirty = true;
        }

        private bool SaveBlueprintDraft(out string error)
        {
            error = null;
            if (activeBlueprint == null) return true;
            CommitBlueprintEditTarget();
            if (!blueprintPartsDirty) return true;
            if (!TryBuildBlueprintAnchors(out List<CompositeBlueprintStore.VectorData> anchors))
            {
                error = "не удалось пересчитать точки привязки";
                return false;
            }
            if (!blueprintStore.TryUpdateParts(activeBlueprint, activeBlueprint.parts, anchors,
                out error))
                return false;
            originalBlueprintParts = null;
            originalBlueprintAnchors = null;
            blueprintPartsDirty = false;
            blueprintPieceRegistry.DeleteThumbnail(activeBlueprint.id);
            HammerCatalogView.Invalidate();
            return true;
        }

        private void RestoreBlueprintDraft()
        {
            if (activeBlueprint != null && blueprintPartsDirty && originalBlueprintParts != null)
            {
                activeBlueprint.parts = originalBlueprintParts;
                activeBlueprint.anchors = originalBlueprintAnchors ??
                    new List<CompositeBlueprintStore.VectorData>();
            }
            originalBlueprintParts = null;
            originalBlueprintAnchors = null;
            blueprintPartsDirty = false;
            blueprintEditPartIndex = -1;
        }

        private bool TryBuildBlueprintAnchors(
            out List<CompositeBlueprintStore.VectorData> anchors)
        {
            anchors = null;
            if (activeBlueprint == null ||
                activeBlueprintPieces.Count != activeBlueprint.parts.Count) return false;
            var snapPoints = new List<IReadOnlyList<Point3>>(activeBlueprint.parts.Count);
            bool explicitFrame = CompositeBlueprintStore.HasExplicitFrame(activeBlueprint);
            for (int index = 0; index < activeBlueprint.parts.Count; ++index)
            {
                if (explicitFrame && !CompositeBlueprintStore.IsFramePart(activeBlueprint, index))
                    continue;
                Piece piece = activeBlueprintPieces[index];
                if (!piece) return false;
                CompositeBlueprintStore.Part part = activeBlueprint.parts[index];
                var native = new List<Transform>();
                piece.GetSnapPoints(native);
                var local = new List<Point3>(native.Count);
                Quaternion inversePiece = Quaternion.Inverse(piece.transform.rotation);
                foreach (Transform point in native)
                {
                    if (!point) continue;
                    Vector3 pieceLocal = inversePiece * (point.position - piece.transform.position);
                    Vector3 rootLocal = part.position.ToVector3() +
                        part.rotation.ToQuaternion() * pieceLocal;
                    local.Add(ToGeometry(rootLocal));
                }
                snapPoints.Add(local);
            }
            IReadOnlyList<Point3> external = explicitFrame && snapPoints.Count == 1
                ? snapPoints[0]
                : AnchorAdjustment.ExternalCompositeSnapPoints(snapPoints);
            anchors = new List<CompositeBlueprintStore.VectorData>(external.Count);
            foreach (Point3 point in external)
                anchors.Add(new CompositeBlueprintStore.VectorData(ToUnity(point)));
            return true;
        }

        private int HitTestBlueprintPart(Camera camera, Vector2 mouse)
        {
            if (!camera || activeBlueprint == null || activeBlueprintPieces.Count !=
                activeBlueprint.parts.Count) return -1;
            int best = -1;
            float bestDepth = float.PositiveInfinity;
            for (int index = 0; index < activeBlueprint.parts.Count; ++index)
            {
                Piece piece = activeBlueprintPieces[index];
                if (!piece || !TryCaptureAnchorBounds(piece.gameObject,
                    piece.transform.position, piece.transform.rotation, out AnchorBounds bounds))
                    continue;
                CompositeBlueprintStore.Part part = activeBlueprint.parts[index];
                Vector3 partPosition = part.position.ToVector3();
                Quaternion partRotation = part.rotation.ToQuaternion();
                Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
                Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
                float depth = float.PositiveInfinity;
                bool visible = false;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 world = blueprintRootPosition + blueprintRootRotation *
                        (partPosition + partRotation * ToUnity(bounds.Corner(corner)));
                    Vector3 screen = camera.WorldToScreenPoint(world);
                    if (screen.z <= 0f) continue;
                    visible = true;
                    minimum = Vector2.Min(minimum, screen);
                    maximum = Vector2.Max(maximum, screen);
                    depth = Mathf.Min(depth, screen.z);
                }
                if (!visible) continue;
                Rect rect = Rect.MinMaxRect(
                    minimum.x - 5f, minimum.y - 5f, maximum.x + 5f, maximum.y + 5f);
                if (rect.Contains(mouse) && depth < bestDepth)
                {
                    best = index;
                    bestDepth = depth;
                }
            }
            return best;
        }

        internal static bool TryResolveBlueprintPieces(
            Player player,
            CompositeBlueprintStore.Blueprint blueprint,
            out List<Piece> pieces,
            out string missingPrefab)
        {
            pieces = new List<Piece>(blueprint?.parts?.Count ?? 0);
            missingPrefab = string.Empty;
            if (!player || blueprint?.parts == null) return false;
            foreach (CompositeBlueprintStore.Part part in blueprint.parts)
            {
                if (!TryResolveBuildPiece(player, part.prefabName, out Piece piece) ||
                    !SupportsBlueprintWorkspacePiece(piece) || !CompositeBlueprintStore.ValidScale(part.scale))
                {
                    missingPrefab = part.prefabName;
                    return false;
                }
                ZNetView view = piece.GetComponent<ZNetView>();
                if (part.scale.ToVector3() != Vector3.one && (!view || !view.m_syncInitialScale))
                {
                    missingPrefab = part.prefabName + " (нет штатного сохранения масштаба Valheim)";
                    return false;
                }
                pieces.Add(piece);
            }
            return pieces.Count >= 2;
        }

        private bool ApplyBlueprintAnchors()
        {
            if (activeBlueprint == null ||
                activeBlueprintPieces.Count != activeBlueprint.parts.Count)
                return false;
            List<Point3> corners = new List<Point3>();
            var partSnapSets = new List<IReadOnlyList<Point3>>();
            var partSnaps = new List<Transform>();
            bool composite = !CompositeBlueprintStore.HasExplicitFrame(activeBlueprint) ||
                blueprintCompositeFrame;
            for (int index = 0; index < activeBlueprint.parts.Count; ++index)
            {
                if (!composite && !CompositeBlueprintStore.IsFramePart(activeBlueprint, index))
                    continue;
                Piece piece = activeBlueprintPieces[index];
                if (!TryCaptureAnchorBounds(
                    piece.gameObject,
                    piece.transform.position,
                    piece.transform.rotation,
                    out AnchorBounds partBounds))
                    return false;
                CompositeBlueprintStore.Part part = activeBlueprint.parts[index];
                Vector3 partPosition = part.position.ToVector3();
                Quaternion partRotation = part.rotation.ToQuaternion();
                for (int corner = 0; corner < 8; ++corner)
                {
                    corners.Add(ToGeometry(partPosition +
                        partRotation * Vector3.Scale(ToUnity(partBounds.Corner(corner)), part.scale.ToVector3())));
                }
                partSnaps.Clear();
                piece.GetSnapPoints(partSnaps);
                var nativePoints = new List<Point3>();
                Quaternion sourceInverse = Quaternion.Inverse(piece.transform.rotation);
                foreach (Transform snap in partSnaps)
                {
                    if (!snap) continue;
                    Vector3 local = sourceInverse * (snap.position - piece.transform.position);
                    Vector3 point = partPosition + partRotation *
                        Vector3.Scale(local, part.scale.ToVector3());
                    if (!IsFinite(point)) continue;
                    nativePoints.Add(ToGeometry(point));
                }
                partSnapSets.Add(nativePoints);
            }

            AnchorBounds bounds;
            try
            {
                bounds = AnchorAdjustment.CreateBounds(corners);
            }
            catch (ArgumentException)
            {
                return false;
            }
            List<Vector3> points = new List<Vector3>();
            blueprintPivotLocal = ToUnity(bounds.Center);
            currentPosition = blueprintRootPosition + blueprintRootRotation * blueprintPivotLocal;
            for (int anchor = 0; anchor < AnchorAdjustment.AnchorCount; ++anchor)
                points.Add(ToUnity(bounds.Anchor(anchor)) - blueprintPivotLocal);
            points.Add(ToUnity(bounds.Center) - blueprintPivotLocal);
            // Keep native (including generated midpoint) handles after the bounds
            // handles so their identity and world-F9 appearance are preserved.
            IReadOnlyList<Point3> externalSnaps = !composite && partSnapSets.Count == 1
                ? partSnapSets[0]
                : AnchorAdjustment.ExternalCompositeSnapPoints(
                    partSnapSets, tolerance: 0.0001);
            for (int index = 0; index < externalSnaps.Count &&
                index < MaximumBlueprintSourceNativeSnapPoints; ++index)
                points.Add(ToUnity(externalSnaps[index]) - blueprintPivotLocal);
            anchorLocalPoints = points.ToArray();
            anchorWorldPoints = new Vector3[anchorLocalPoints.Length];
            anchorBoundsAvailable = true;
            return true;
        }

        private void UpdateBlueprintFrameOverride()
        {
            bool composite = !CompositeBlueprintStore.HasExplicitFrame(activeBlueprint) ||
                Input.GetKey(BuildWorksPlugin.BlueprintCompositeFrameKey.Value);
            if (blueprintCompositeFrame == composite) return;
            CommitBlueprintEditTarget();
            blueprintCompositeFrame = composite;
            if (!ApplyBlueprintAnchors()) return;
            RebuildBlueprintPlan();
            ApplyEditingGhostTransform();
        }

        private void RestoreSinglePieceAnchors()
        {
            anchorLocalPoints = (Vector3[])singlePieceAnchorLocalPoints.Clone();
            anchorWorldPoints = new Vector3[anchorLocalPoints.Length];
            anchorBoundsAvailable = anchorLocalPoints.Length >= NativeAnchorStart;
        }

        private bool CaptureSelectedPieceAnchors()
        {
            if (!placementGhost) return false;
            Vector3 rootPosition = currentPosition;
            Quaternion rootRotation = currentRotation;
            currentPosition = placementGhost.transform.position;
            currentRotation = placementGhost.transform.rotation;
            bool captured = CaptureAnchorPoints(placementGhost);
            if (captured)
                singlePieceAnchorLocalPoints = (Vector3[])anchorLocalPoints.Clone();
            currentPosition = rootPosition;
            currentRotation = rootRotation;
            return captured;
        }

        private bool SelectBuildPiece(Player player, Piece piece)
        {
            if (!player || !piece) return false;
            if (isolatedEditorView) RestoreEditorGhostLayers();
            PieceTable table = GetBuildPieceTable(player);
            if (table) HammerCatalogOrganizer.EnsureVisible(table, piece);
            if (player.GetSelectedPiece() != piece &&
                !blueprintPieceRegistry.SelectPiece(player, piece))
                return false;
            GameObject ghost = PlacementGhostField.GetValue(player) as GameObject;
            if (!ghost) return false;
            selectedPiece = piece;
            placementGhost = ghost;
            if (isolatedEditorView) ApplyEditorGhostLayer(ghost);
            return true;
        }

        private bool SelectPlanPiece(Player player, int index)
        {
            return index >= 0 && index < placementPlanPieces.Count &&
                SelectBuildPiece(player, placementPlanPieces[index]);
        }

        private static string PrefabName(Piece piece) => piece
            ? piece.gameObject.name.Replace("(Clone)", string.Empty).Trim()
            : string.Empty;

        internal static bool TryResolveBuildPiece(
            Player player,
            string prefabName,
            out Piece piece)
        {
            piece = null;
            if (!player || string.IsNullOrWhiteSpace(prefabName)) return false;
            PieceTable table = BuildPiecesField.GetValue(player) as PieceTable;
            if (!table || table.m_pieces == null) return false;
            foreach (GameObject candidateObject in table.m_pieces)
            {
                Piece candidate = candidateObject
                    ? candidateObject.GetComponent<Piece>()
                    : null;
                if (candidate && string.Equals(
                    PrefabName(candidate),
                    prefabName,
                    StringComparison.Ordinal) && IsBuildPieceAvailable(table, candidate))
                {
                    piece = candidate;
                    return true;
                }
            }
            return false;
        }

        internal static bool IsBuildPieceAvailable(PieceTable table, Piece piece)
        {
            if (!table || !piece || AvailablePiecesField == null) return false;
            if (HammerCatalogOrganizer.ContainsAvailable(table, piece)) return true;
            List<List<Piece>> available = AvailableBuildPieces(table);
            if (available == null) return false;
            foreach (List<Piece> category in available)
                if (category != null && category.Contains(piece)) return true;
            return false;
        }

        internal static List<List<Piece>> AvailableBuildPieces(PieceTable table) =>
            table && AvailablePiecesField != null
                ? AvailablePiecesField.GetValue(table) as List<List<Piece>>
                : null;

        internal static List<Piece> AvailableBuildPieces(
            PieceTable table,
            Piece.PieceCategory category)
        {
            List<List<Piece>> available = AvailableBuildPieces(table);
            int index = (int)category;
            return available != null && index >= 0 && index < available.Count
                ? available[index]
                : null;
        }

        internal static bool UsesHammerPieceTable(Player player)
        {
            PieceTable table = GetBuildPieceTable(player);
            return table && table.m_canRemovePieces;
        }

        internal static PieceTable GetBuildPieceTable(Player player) => player
            ? BuildPiecesField.GetValue(player) as PieceTable
            : null;

        private void ClearLayout()
        {
            placementPlan.Clear();
            placementPlanPieces.Clear();
            previewContacts.Clear();
            previewAims.Clear();
            placementPlanIndex = 0;
            automaticSkippedPlacements = 0;
            requestAutomaticPlacement = false;
            automaticWaitingForStamina = false;
            automaticWaitingForTool = false;
            automaticPlacementPaused = false;
            automaticPauseReason = null;
            repeatAxis = GizmoAxis.None;
            repeatCountSecond = false;
            repeatPlanError = null;
            repeatStep = Vector3.zero;
            repeatDraggedDistance = 0f;
            planeSecondStep = Vector3.zero;
            planeDraggedDistance = 0f;
            planeSecondCount = 0;
            planeDraggingSecond = false;
            contourClosed = false;
            contourSeedSupportIndex = 0;
            contourSupports.Clear();
            contourPath.Clear();
            ClearContourHover();
            nextContourHoverUpdate = 0f;
            expandedPlanLimited = false;
            previews.Hide();
            if (activeBlueprint != null && sessionMode != SessionMode.BlueprintEditor)
                RebuildBlueprintPlan();
        }

        private bool CancelNewestStage()
        {
            if (dragAxis != GizmoAxis.None || dragAnchorPoint >= 0)
            {
                currentPosition = dragStartPosition;
                currentRotation = dragStartRotation;
                RestoreDragLayoutState();
                dragAxis = GizmoAxis.None;
                dragHandle = GizmoHandleKind.None;
                dragAnchorPoint = -1;
                dragConstraintActive = false;
                ClearSnapDrag();
                return true;
            }
            if (gizmoMode == GizmoMode.Guide)
            {
                if (contourSupports.Count > 0)
                {
                    contourSupports.Clear();
                    contourPath.Clear();
                    placementPlan.Clear();
                    placementPlanPieces.Clear();
                    previewContacts.Clear();
                    previewAims.Clear();
                    if (activeBlueprint != null) RebuildBlueprintPlan();
                }
                else
                {
                    return false;
                }
                previews.Hide();
                return true;
            }
            if (gizmoMode == GizmoMode.Repeat && repeatAxis != GizmoAxis.None)
            {
                if (planeSecondCount >= 2)
                {
                    planeSecondStep = Vector3.zero;
                    planeDraggedDistance = 0f;
                    planeSecondCount = 0;
                    repeatCountSecond = false;
                    RebuildRepeatPlan();
                    return true;
                }
                ClearLayout();
                return true;
            }
            if (pinnedAnchorPoint >= 0)
            {
                pinnedAnchorPoint = -1;
                return true;
            }
            if (anchorConstraintAxis != GizmoAxis.None)
            {
                anchorConstraintAxis = GizmoAxis.None;
                return true;
            }
            if (history.TryUndo(out TransformSnapshot snapshot))
            {
                ApplyHistorySnapshot(snapshot);
                RebuildActiveLayout();
                return true;
            }
            return false;
        }

        private void SelectGizmoMode(GizmoMode mode)
        {
            if (mode == gizmoMode && mode != GizmoMode.Move)
                mode = GizmoMode.Move;
            if ((mode == GizmoMode.Guide || mode == GizmoMode.Repeat) &&
                !anchorBoundsAvailable)
            {
                ShowStatus(Player.m_localPlayer, "BuildWorks: у детали не найдена геометрия опор.");
                return;
            }
            if (mode != gizmoMode)
            {
                ClearLayout();
            }
            gizmoMode = mode;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            ClearSnapDrag();
        }

        private void SelectAnchorConstraint(GizmoAxis axis)
        {
            if (!anchorBoundsAvailable)
            {
                ShowStatus(Player.m_localPlayer, "BuildWorks: у детали не найдена геометрия опор.");
                return;
            }
            anchorConstraintAxis = axis == GizmoAxis.None
                ? GizmoAxis.None
                : anchorConstraintAxis == axis ? GizmoAxis.None : axis;
            RestartAnchorDragForConstraint();
        }

        private void RestartAnchorDragForConstraint()
        {
            if (dragAnchorPoint < 0 || dragMagneticMove || !freeViewCamera)
            {
                return;
            }

            int movingAnchor = dragAnchorPoint;
            MovePieceWithoutMovingCameraFocus(dragStartPosition);
            currentRotation = dragStartRotation;
            RestoreDragLayoutState();
            dragAnchorPoint = -1;
            dragConstraintActive = false;
            UpdateAnchorWorldPoints();
            BeginAnchorDrag(freeViewCamera, movingAnchor, magneticMove: false);
        }

        private void ToggleSpace()
        {
            if (gizmoMode == GizmoMode.Repeat)
            {
                ClearLayout();
            }
            localSpace = !localSpace;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            RestartAnchorDragForConstraint();
        }

        private void ToggleAnchorVisibility()
        {
            showAllAnchors = !showAllAnchors;
        }

        private void CycleHandleScale()
        {
            handleScaleIndex = (handleScaleIndex + 1) % HandleScales.Length;
        }

        private void CycleTranslationStep()
        {
            translationStepIndex = (translationStepIndex + 1) % Geometry.PrecisionStepPresets.Translation.Count;
        }

        private void CycleRotationStep()
        {
            rotationStepIndex = (rotationStepIndex + 1) % Geometry.PrecisionStepPresets.Rotation.Count;
        }

        private void CycleArrayStep()
        {
            if (gizmoMode == GizmoMode.Repeat)
            {
                if (repeatDistribution == RepeatDistributionMode.Fit) return;
                if (repeatDistribution == RepeatDistributionMode.Exact)
                    exactRepeatStepIndex = (exactRepeatStepIndex + 1) % ExactRepeatSteps.Length;
                else
                    repeatSpacingIndex = (repeatSpacingIndex + 1) % RepeatSpacings.Length;
                if (repeatAxis != GizmoAxis.None)
                {
                    RecalculateRepeatSteps();
                    RebuildRepeatPlan();
                }
            }
        }

        private void CycleDistribution()
        {
            repeatDistribution = (RepeatDistributionMode)(((int)repeatDistribution + 1) % 3);
            if (repeatAxis != GizmoAxis.None)
            {
                RecalculateRepeatSteps();
                RebuildRepeatPlan();
            }
        }

        private void RecalculateRepeatSteps()
        {
            repeatStep = RepeatConfiguredStep(repeatStep, repeatDraggedDistance, copyCount);
            if (planeSecondStep.sqrMagnitude > 0.000001f)
                planeSecondStep = RepeatConfiguredStep(planeSecondStep, planeDraggedDistance, planeSecondCount);
        }

        private Vector3 RepeatConfiguredStep(Vector3 step, float span, int count) =>
            step.normalized * (repeatDistribution == RepeatDistributionMode.Fit
                ? span / Mathf.Max(1, count - 1)
                : repeatDistribution == RepeatDistributionMode.Exact
                    ? ExactRepeatSteps[exactRepeatStepIndex] : RepeatStepLength(step.normalized));

        private void AdjustRepeatRise(float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta)) return;
            repeatRise = Mathf.Clamp(repeatRise + delta, -100f, 100f);
            if (repeatAxis != GizmoAxis.None) RebuildRepeatPlan();
        }

        private void AdjustRepeatScale(float percentDelta)
        {
            if (float.IsNaN(percentDelta) || float.IsInfinity(percentDelta)) return;
            repeatScaleStep = Mathf.Clamp(repeatScaleStep + percentDelta / 100f, -3f, 3f);
            if (repeatAxis != GizmoAxis.None) RebuildRepeatPlan();
        }

        private void AdjustRepeatTurn(float delta)
        {
            AdjustRepeatAngle(ref repeatTurnDegrees, delta);
        }

        private void AdjustRepeatPitch(float delta)
        {
            AdjustRepeatAngle(ref repeatPitchDegrees, delta);
        }

        private void AdjustRepeatRoll(float delta)
        {
            AdjustRepeatAngle(ref repeatRollDegrees, delta);
        }

        private void AdjustRepeatAngle(ref float angle, float delta)
        {
            if (float.IsNaN(delta) || float.IsInfinity(delta)) return;
            angle = (float)PrecisionAdjustment.NormalizeDegrees(angle + delta);
            if (repeatAxis != GizmoAxis.None) RebuildRepeatPlan();
        }

        private void ToggleSymmetry()
        {
            repeatSymmetric = !repeatSymmetric;
            if (repeatAxis != GizmoAxis.None) RebuildRepeatPlan();
        }

        private void ToggleChain()
        {
            chainEnabled = !chainEnabled;
            if (!chainEnabled) ClearContinuation();
        }

        private void ToggleMeshSnap()
        {
            meshSnapEnabled = !meshSnapEnabled;
            if (dragMagneticMove) RefreshSnapTargets();
            ShowStatus(Player.m_localPlayer, meshSnapEnabled
                ? "BuildWorks: магнит по модели — Ctrl совмещает только выбранные точки."
                : "BuildWorks: магнит использует только штатные точки Valheim.");
        }

        private void ToggleAutoAlignment()
        {
            autoAlignmentEnabled = !autoAlignmentEnabled;
            alignmentTargetVisible = false;
            if (autoAlignmentEnabled)
            {
                Vector3 oldPosition = currentPosition;
                Quaternion oldRotation = currentRotation;
                if (TryAutoAlignToTouchingPiece())
                    CommitTransformIfChanged(oldPosition, oldRotation);
            }
            ShowStatus(Player.m_localPlayer, autoAlignmentEnabled
                ? "BuildWorks: автостык включён — ориентация и штатные точки совмещаются вместе."
                : "BuildWorks: автостык выключен.");
        }

        private bool TryAutoAlignToTouchingPiece()
        {
            alignmentTargetVisible = false;
            if (sessionMode == SessionMode.BlueprintEditor) return false;
            if (!autoAlignmentEnabled || !placementGhost || anchorLocalPoints.Length == 0)
                return false;

            if (!freeViewCamera || !TryGetPlacedPieceAt(
                freeViewCamera,
                Input.mousePosition,
                out Piece targetPiece,
                out _))
                return false;

            Vector3 oldPosition = currentPosition;
            Quaternion oldRotation = currentRotation;
            Quaternion targetRotation = targetPiece.transform.rotation;
            if (TryFindAlignedSnapPair(
                targetPiece,
                targetRotation,
                out Vector3 sourceLocal,
                out Vector3 targetWorld))
            {
                currentRotation = targetRotation;
                MovePieceWithoutMovingCameraFocus(ToUnity(AnchorAdjustment.PositionForFixedAnchor(
                    ToGeometry(targetWorld),
                    ToGeometry(sourceLocal),
                    ToGeometry(currentRotation))));
                alignmentTargetVisible = true;
                alignmentTargetPosition = targetWorld;
                alignmentTargetRotation = targetRotation;
                return TransformChanged(
                    oldPosition, oldRotation, currentPosition, currentRotation);
            }

            float bestDistance = AutoAlignmentDistance * AutoAlignmentDistance;
            Vector3 bestPoint = Vector3.zero;
            Vector3 bestAnchor = Vector3.zero;
            Vector3 bestAnchorWorld = Vector3.zero;
            Collider[] colliders = targetPiece.GetComponentsInChildren<Collider>();
            foreach (Collider collider in colliders)
            {
                if (!collider || !collider.enabled || collider.isTrigger ||
                    !collider.gameObject.activeInHierarchy)
                    continue;
                foreach (Vector3 localAnchor in anchorLocalPoints)
                {
                    Vector3 source = currentPosition + currentRotation * localAnchor;
                    Vector3 closest = collider.ClosestPoint(source);
                    float distance = (closest - source).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    bestPoint = closest;
                    bestAnchor = localAnchor;
                    bestAnchorWorld = source;
                }
            }
            if (bestDistance >= AutoAlignmentDistance * AutoAlignmentDistance) return false;

            currentRotation = targetRotation;
            if (Quaternion.Angle(oldRotation, currentRotation) > 0.0001f)
            {
                MovePieceWithoutMovingCameraFocus(ToUnity(AnchorAdjustment.PositionForFixedAnchor(
                    ToGeometry(bestAnchorWorld),
                    ToGeometry(bestAnchor),
                    ToGeometry(currentRotation))));
            }
            alignmentTargetVisible = true;
            alignmentTargetPosition = bestPoint;
            alignmentTargetRotation = targetRotation;
            return TransformChanged(oldPosition, oldRotation, currentPosition, currentRotation);
        }

        private bool TryFindAlignedSnapPair(
            Piece targetPiece,
            Quaternion targetRotation,
            out Vector3 sourceLocal,
            out Vector3 targetWorld)
        {
            sourceLocal = Vector3.zero;
            targetWorld = Vector3.zero;
            nativeSnapPoints.Clear();
            targetPiece.GetSnapPoints(nativeSnapPoints);
            float bestDistance = AutoSnapDistance * AutoSnapDistance;
            bool found = false;
            for (int source = NativeAnchorStart; source < anchorLocalPoints.Length; ++source)
            {
                Vector3 candidateSource = anchorLocalPoints[source];
                Vector3 sourceWorld = currentPosition + targetRotation * candidateSource;
                foreach (Transform target in nativeSnapPoints)
                {
                    if (!target) continue;
                    float distance = (target.position - sourceWorld).sqrMagnitude;
                    if (distance >= bestDistance) continue;
                    bestDistance = distance;
                    sourceLocal = candidateSource;
                    targetWorld = target.position;
                    found = true;
                }
            }
            return found;
        }

        private void UpdateHud()
        {
            if (sessionMode == SessionMode.BlueprintEditor &&
                blueprintWorkspaceEditPart == null)
            {
                hud.ShowBlueprintWorkspace(
                    activeBlueprint?.name,
                    blueprintWorkspaceParts.Count,
                    selectedPiece ? selectedPiece.m_name : string.Empty,
                    EditorLightingName());
                return;
            }
            if (state == PlacementState.Armed)
            {
                hud.ShowArmed(
                    placementPlanIndex,
                    placementPlan.Count,
                    automaticWaitingForStamina,
                    automaticWaitingForTool,
                    automaticWaitingForTool && Player.m_localPlayer &&
                        Player.m_localPlayer.GetSelectedPiece() != selectedPiece,
                    automaticPlacementPaused,
                    automaticPauseReason);
                return;
            }
            if (state == PlacementState.Frozen)
            {
                hud.ShowFrozen();
                return;
            }
            Vector3 displayedOffset = localSpace
                ? Quaternion.Inverse(baseRotation) * (currentPosition - basePosition)
                : currentPosition - basePosition;
            Quaternion displayedRotation = localSpace
                ? Quaternion.Inverse(baseRotation) * currentRotation
                : currentRotation * Quaternion.Inverse(baseRotation);
            Vector3 displayedEuler = SignedEuler(displayedRotation);
            string modeHint = LayoutHint();
            string prefix = localSpace ? "Лок." : "Мир.";
            string step = gizmoMode == GizmoMode.Repeat
                ? repeatDistribution == RepeatDistributionMode.Fit
                    ? "ПО ОТРЕЗКУ"
                    : repeatDistribution == RepeatDistributionMode.Exact
                    ? ExactRepeatSteps[exactRepeatStepIndex].ToString("0.##") + " м"
                    : RepeatSpacings[repeatSpacingIndex] <= 0f
                        ? "ПО РАЗМЕРУ"
                        : "РАЗМЕР + " +
                            (RepeatSpacings[repeatSpacingIndex] * 100f).ToString("0") + " см"
                : string.Empty;
            PruneBlueprintSelection();
            bool canSaveBlueprint = sessionMode != SessionMode.BlueprintEditor;
            hud.ShowEditing(
                gizmoMode,
                anchorConstraintAxis,
                localSpace,
                showAllAnchors,
                (HandleScales[handleScaleIndex] * 100f).ToString("0") + "%",
                history.UndoCount,
                history.RedoCount,
                repeatDistribution,
                gizmoMode == GizmoMode.Guide
                    ? contourSupports.Count > 0
                        ? contourSupports.Count
                        : contourHoverSupports.Count
                    : copyCount,
                planeSecondCount,
                repeatRise,
                repeatTurnDegrees,
                repeatPitchDegrees,
                repeatRollDegrees,
                repeatScaleStep * 100f,
                repeatSymmetric,
                meshSnapEnabled,
                autoAlignmentEnabled,
                (Geometry.PrecisionStepPresets.Translation[translationStepIndex] * 100f).ToString("0") + " см",
                Geometry.PrecisionStepPresets.Rotation[rotationStepIndex].ToString("0.#") + "°",
                step,
                activeBlueprint?.name,
                activeBlueprint?.parts.Count ?? 0,
                blueprintEditPartIndex,
                sessionMode == SessionMode.BlueprintWorldPlacement,
                blueprintWorkspaceEditPart != null,
                EditorLightingName(),
                canSaveBlueprint,
                selectingBlueprint,
                blueprintSelection.Count,
                modeHint,
                string.Format(
                    "{0} смещение  X {1:F2}  Y {2:F2}  Z {3:F2} м",
                    prefix,
                    displayedOffset.x,
                    displayedOffset.y,
                    displayedOffset.z),
                string.Format(
                    "{0} вращение  X {1:F1}°  Y {2:F1}°  Z {3:F1}°",
                    prefix,
                    displayedEuler.x,
                    displayedEuler.y,
                    displayedEuler.z));
        }

        private string LayoutHint()
        {
            if (!string.IsNullOrEmpty(repeatPlanError)) return repeatPlanError;
            if (blueprintWorkspaceEditPart != null)
                return "ДЕТАЛЬ ЧЕРТЕЖА: стрелки и кольца меняют только её · " +
                    "F9/ПРИМЕНИТЬ — вернуться к строительству · ОТМЕНА — откатить";
            if (expandedPlanLimited)
                return "ЧЕРТЕЖ: превышен безопасный предел " +
                    MaximumExpandedPlacements + " деталей; уменьши массив";
            if (gizmoMode == GizmoMode.Repeat)
            {
                if (repeatAxis == GizmoAxis.None)
                    return "ПОТЯНИ ЗОЛОТУЮ СТРЕЛКУ — задай ряд; параметры применятся к следующим копиям";
                return "Колесо: " + (repeatCountSecond ? "2-е" : "1-е") +
                    " направление · Ctrl+колесо: камера · параметры на каждую следующую копию";
            }
            if (gizmoMode != GizmoMode.Guide)
            {
                if (sessionMode == SessionMode.BlueprintWorldPlacement)
                    return "ЧЕРТЕЖ В МИРЕ: двигается вся группа · МАССИВ / КОНТУР повторяют группу · " +
                        "УСТАНОВИТЬ — разместить · F9/Esc — вернуться";
                return activeBlueprint != null
                    ? blueprintEditPartIndex < 0
                        ? activeBlueprint.name + ": двигается вся группа · " +
                            "клик/Tab — выбрать деталь · ПРИМЕНИТЬ — сохранить · Esc — выйти"
                        : activeBlueprint.name + ": деталь " +
                            (blueprintEditPartIndex + 1) + "/" + activeBlueprint.parts.Count +
                            " · клик/Tab — выбрать другую · ПРИМЕНИТЬ — сохранить · Esc — выйти"
                    : null;
            }
            if (contourSupports.Count == 0)
            {
                if (contourHoverSupports.Count > 0)
                    return (contourHoverClosed ? "Кольцо" : "Цепь") +
                        " подсвечено: " + contourHoverSupports.Count +
                        " деталей; ЛКМ выбирает этот контур";
                return "КОНТУР: наведи на видимое ребро цепи — она подсветится; ЛКМ выбирает";
            }
            return (contourClosed ? "Замкнутый" : "Открытый") +
                " контур готов: " + placementPlan.Count +
                " деталей сохраняют точное положение исходной; УСТАНОВИТЬ размещает цепь";
        }

        private void ShowLayoutPreview(Camera camera)
        {
            gizmo.ShowLayout(
                camera,
                gizmoMode == GizmoMode.Guide
                    ? contourSupports.Count > 0 ? contourPath : contourHoverPath
                    : Array.Empty<Vector3>(),
                false,
                Array.Empty<Vector3>(),
                false,
                previewContacts,
                previewAims);
        }

        private void ConfirmEditor()
        {
            if (selectingBlueprint)
            {
                SaveCurrentBlueprint();
                return;
            }
            if (state == PlacementState.Armed && automaticPlacementPaused)
            {
                automaticPlacementPaused = false;
                automaticPauseReason = null;
                RestoreCursor();
                UpdateHud();
                return;
            }
            if (sessionMode == SessionMode.BlueprintEditor)
            {
                if (blueprintWorkspaceEditPart != null)
                {
                    EndBlueprintWorkspacePrecision(restoreTransform: false);
                    return;
                }
                SaveAndCloseBlueprintEditor();
                return;
            }
            if (sessionMode == SessionMode.WorldPrecision ||
                sessionMode == SessionMode.BlueprintWorldPlacement) ArmPlacement();
        }

        private void SaveAndCloseBlueprintEditor()
        {
            if (sessionMode != SessionMode.BlueprintEditor ||
                state == PlacementState.Inactive) return;
            Player player = Player.m_localPlayer;
            if (!TrySaveBlueprintWorkspace(
                out CompositeBlueprintStore.Blueprint savedBlueprint,
                out string saveError))
            {
                ShowStatus(player, "BuildWorks: не удалось сохранить правки чертежа — " +
                    saveError + ".");
                return;
            }
            string blueprintName = savedBlueprint.name;
            Finish();
            blueprintPieceRegistry.Update(player);
            ShowStatus(player, "BuildWorks: " + blueprintName +
                " сохранён; редактор закрыт без размещения в мире.");
        }

        private bool TrySaveBlueprintWorkspace(
            out CompositeBlueprintStore.Blueprint savedBlueprint,
            out string error)
        {
            savedBlueprint = activeBlueprint;
            error = null;
            if (blueprintWorkspaceParts.Count < 2)
            {
                error = "нужно построить минимум две детали";
                return false;
            }

            var corners = new List<Point3>();
            foreach (BlueprintWorkspacePart workspacePart in blueprintWorkspaceParts)
            {
                if (workspacePart?.Visual == null || workspacePart.Source == null ||
                    !TryCaptureAnchorBounds(
                        workspacePart.Visual,
                        workspacePart.Visual.transform.position,
                        workspacePart.Visual.transform.rotation,
                        out AnchorBounds partBounds))
                {
                    error = "не удалось определить границы временной детали";
                    return false;
                }
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 world = workspacePart.Visual.transform.position +
                        workspacePart.Visual.transform.rotation *
                        ToUnity(partBounds.Corner(corner));
                    corners.Add(ToGeometry(world));
                }
            }

            AnchorBounds groupBounds;
            try
            {
                groupBounds = AnchorAdjustment.CreateBounds(corners);
            }
            catch (ArgumentException)
            {
                error = "временная конструкция не имеет корректных границ";
                return false;
            }
            Vector3 rootPosition = ToUnity(groupBounds.Center);
            var parts = new List<CompositeBlueprintStore.Part>(blueprintWorkspaceParts.Count);
            var snapSets = new List<IReadOnlyList<Point3>>(blueprintWorkspaceParts.Count);
            foreach (BlueprintWorkspacePart workspacePart in blueprintWorkspaceParts)
            {
                Transform visual = workspacePart.Visual.transform;
                parts.Add(new CompositeBlueprintStore.Part
                {
                    prefabName = PrefabName(workspacePart.Source),
                    position = new CompositeBlueprintStore.VectorData(
                        visual.position - rootPosition),
                    rotation = new CompositeBlueprintStore.QuaternionData(visual.rotation),
                    scale = new CompositeBlueprintStore.VectorData(workspacePart.Scale)
                });
                var snaps = new List<Point3>(workspacePart.SnapLocal.Count);
                foreach (Vector3 snapLocal in workspacePart.SnapLocal)
                    snaps.Add(ToGeometry(visual.TransformPoint(snapLocal) - rootPosition));
                snapSets.Add(snaps);
            }

            IReadOnlyList<Point3> external =
                AnchorAdjustment.ExternalCompositeSnapPoints(snapSets);
            var anchors = new List<CompositeBlueprintStore.VectorData>(external.Count);
            foreach (Point3 point in external)
                anchors.Add(new CompositeBlueprintStore.VectorData(ToUnity(point)));

            bool saved = activeBlueprint == null
                ? blueprintStore.TrySave(parts, anchors, out savedBlueprint, out error)
                : blueprintStore.TryUpdateParts(activeBlueprint, parts, anchors, out error);
            if (!saved) return false;
            if (activeBlueprint != null)
                blueprintPieceRegistry.DeleteThumbnail(activeBlueprint.id);
            HammerCatalogView.Invalidate();
            return true;
        }

        private void CancelBlueprintEditor()
        {
            if (sessionMode != SessionMode.BlueprintEditor) return;
            Player player = Player.m_localPlayer;
            Finish();
            ShowStatus(player, "BuildWorks: редактор чертежа закрыт без сохранения.");
        }

        private bool ArmPlacement()
        {
            if (state != PlacementState.Editing ||
                sessionMode != SessionMode.WorldPrecision &&
                sessionMode != SessionMode.BlueprintWorldPlacement)
            {
                return false;
            }
            if (expandedPlanLimited)
            {
                ShowStatus(Player.m_localPlayer,
                    "BuildWorks: уменьши массив — максимум " +
                    MaximumExpandedPlacements + " деталей за одну установку.");
                return false;
            }

            Player player = Player.m_localPlayer;
            Piece piece = selectedPiece;
            if (!player || !piece || !ContextStillValid(player))
            {
                Cancel();
                return false;
            }
            if (gizmoMode == GizmoMode.Repeat && repeatAxis == GizmoAxis.None ||
                gizmoMode == GizmoMode.Guide && contourSupports.Count == 0 ||
                (gizmoMode == GizmoMode.Repeat || gizmoMode == GizmoMode.Guide) &&
                    placementPlan.Count == 0)
            {
                ShowStatus(player, "BuildWorks: сначала закончи разметку.");
                return false;
            }
            if (placementPlan.Count == 0)
            {
                if (activeBlueprint != null) RebuildBlueprintPlan();
                else
                {
                    placementPlan.Add(new TransformSnapshot(currentPosition, currentRotation));
                    placementPlanPieces.Add(selectedPiece);
                }
            }
            if (placementPlanPieces.Count != placementPlan.Count)
            {
                ShowStatus(player, "BuildWorks: план чертежа повреждён; создай его заново.");
                return false;
            }
            editorRootBeforePlacement = new TransformSnapshot(currentPosition, currentRotation);
            placementPlanIndex = 0;
            automaticSkippedPlacements = 0;
            if (sessionMode == SessionMode.BlueprintWorldPlacement)
                currentBlueprintPlacements.Clear();
            if (!SelectPlanPiece(player, placementPlanIndex))
            {
                ShowStatus(player,
                    "BuildWorks: первая деталь чертежа недоступна в текущем молотке.");
                return false;
            }
            currentPosition = placementPlan[0].Position;
            currentRotation = placementPlan[0].Rotation;

            EndFreeView(restoreCamera: true);
            RestoreCursor();
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            ClearSnapDrag();
            gizmo.Hide();

            if (ValidateCurrentPlacement(player))
            {
                state = PlacementState.Armed;
                requestAutomaticPlacement = true;
                return true;
            }

            if (player.GetPlacementStatus() == Player.PlacementStatus.BlockedbyPlayer)
            {
                state = PlacementState.Armed;
                requestAutomaticPlacement = true;
                TrySkipPlayerBlockedPlacement(player);
                return false;
            }

            ReopenEditor(player, "BuildWorks: Valheim отклонил положение. Исправь деталь и повтори.",
                preserveLayout: true);
            return false;
        }

        private bool ValidateCurrentPlacement(Player player)
        {
            try
            {
                allowNativeGhostUpdate = true;
                UpdatePlacementGhostMethod.Invoke(player, new object[] { false });
                return placementGhost && placementGhost.activeInHierarchy &&
                    player.GetPlacementStatus() == Player.PlacementStatus.Valid;
            }
            catch (Exception exception)
            {
                Debug.LogError("BuildWorks placement validation failed: " + exception);
                return false;
            }
            finally
            {
                allowNativeGhostUpdate = false;
            }
        }

        private void ReopenEditor(Player player, string message, bool preserveLayout = false)
        {
            if (sessionMode == SessionMode.BlueprintWorldPlacement && !preserveLayout)
            {
                ShowStatus(player, message);
                Finish();
                return;
            }
            state = PlacementState.Editing;
            if (preserveLayout)
            {
                currentPosition = editorRootBeforePlacement.Position;
                currentRotation = editorRootBeforePlacement.Rotation;
            }
            if (activeBlueprint != null)
            {
                currentPosition = editorRootBeforePlacement.Position;
                currentRotation = editorRootBeforePlacement.Rotation;
                if (preserveLayout) RebuildActiveLayout();
                else RebuildBlueprintPlan();
                if (!SelectPlanPiece(player, 0))
                {
                    ShowStatus(player,
                        "BuildWorks: первая деталь чертежа больше недоступна.");
                    Finish();
                    return;
                }
            }
            if (placementGhost)
            {
                placementGhost.SetActive(true);
                ApplyEditingGhostTransform();
            }
            SaveAndUnlockCursor();
            Camera camera = Camera.main;
            if (!camera)
            {
                Cancel();
                return;
            }
            BeginFreeView(camera, isolateBlueprint: false);
            ShowStatus(player, message);
        }

        private void ResetTransform()
        {
            if (state != PlacementState.Editing)
            {
                return;
            }
            ClearLayout();
            TransformSnapshot current = CaptureHistorySnapshot();
            if (!HistorySnapshotChanged(baseSnapshot, current))
            {
                return;
            }
            ApplyHistorySnapshot(baseSnapshot);
            RebuildActiveLayout();
            freeViewPanOffset = Vector3.zero;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            pinnedAnchorPoint = -1;
            alignmentTargetVisible = false;
            ClearSnapDrag();
            history.Commit(CaptureHistorySnapshot());
        }

        private void UndoTransform()
        {
            if (state != PlacementState.Editing || dragAxis != GizmoAxis.None ||
                dragAnchorPoint >= 0 || !history.TryUndo(out TransformSnapshot snapshot))
            {
                return;
            }

            ApplyHistorySnapshot(snapshot);
            RebuildActiveLayout();
            dragConstraintActive = false;
            ClearSnapDrag();
        }

        private void RedoTransform()
        {
            if (state != PlacementState.Editing || dragAxis != GizmoAxis.None ||
                dragAnchorPoint >= 0 || !history.TryRedo(out TransformSnapshot snapshot))
            {
                return;
            }

            ApplyHistorySnapshot(snapshot);
            RebuildActiveLayout();
            dragConstraintActive = false;
            ClearSnapDrag();
        }

        private TransformSnapshot CaptureHistorySnapshot()
        {
            Vector3 position = activeBlueprint != null && blueprintEditPartIndex < 0
                ? currentPosition - currentRotation * blueprintPivotLocal
                : currentPosition;
            return new TransformSnapshot(position, currentRotation);
        }

        private void ApplyHistorySnapshot(TransformSnapshot snapshot)
        {
            if (localSpace && gizmoMode == GizmoMode.Repeat &&
                repeatAxis != GizmoAxis.None)
            {
                Quaternion layoutDelta = snapshot.Rotation * Quaternion.Inverse(currentRotation);
                repeatStep = layoutDelta * repeatStep;
                planeSecondStep = layoutDelta * planeSecondStep;
            }
            Vector3 position = snapshot.Position;
            if (activeBlueprint != null && blueprintEditPartIndex < 0)
            {
                blueprintRootPosition = snapshot.Position;
                blueprintRootRotation = snapshot.Rotation;
                position += snapshot.Rotation * blueprintPivotLocal;
            }
            MovePieceWithoutMovingCameraFocus(position);
            currentRotation = snapshot.Rotation;
        }

        private void CommitTransformIfChanged(Vector3 oldPosition, Quaternion oldRotation)
        {
            if (TransformChanged(oldPosition, oldRotation, currentPosition, currentRotation))
            {
                history.Commit(CaptureHistorySnapshot());
            }
        }

        private static bool TransformChanged(
            Vector3 oldPosition,
            Quaternion oldRotation,
            Vector3 newPosition,
            Quaternion newRotation)
        {
            return (oldPosition - newPosition).sqrMagnitude > 0.00000001f ||
                Quaternion.Angle(oldRotation, newRotation) > 0.0001f;
        }

        private static bool HistorySnapshotChanged(
            TransformSnapshot left,
            TransformSnapshot right)
        {
            return TransformChanged(left.Position, left.Rotation, right.Position, right.Rotation);
        }

        private void Cancel(bool restoreCursor = true)
        {
            if (sessionMode == SessionMode.BlueprintEditor &&
                blueprintWorkspaceEditPart != null)
            {
                EndBlueprintWorkspacePrecision(restoreTransform: true);
                return;
            }
            if (sessionMode == SessionMode.BlueprintWorldPlacement)
            {
                CancelBlueprintPlacement(Player.m_localPlayer);
                return;
            }
            if (sessionMode != SessionMode.BlueprintWorldSelection && placementGhost)
            {
                ApplyHistorySnapshot(baseSnapshot);
                placementGhost.transform.SetPositionAndRotation(basePosition, baseRotation);
            }
            Finish(restoreCursor);
        }

        private void FreezeEditor()
        {
            state = PlacementState.Frozen;
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            planeDraggingSecond = false;
            freeViewDragging = false;
            freeViewFlyLooking = false;
            alignmentTargetVisible = false;
            ClearSnapDrag();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            if (placementGhost)
                ApplyEditingGhostTransform();
        }

        private void DisablePrecision()
        {
            if (sessionMode == SessionMode.BlueprintEditor)
            {
                if (blueprintWorkspaceEditPart != null)
                {
                    EndBlueprintWorkspacePrecision(restoreTransform: true);
                    return;
                }
                CancelBlueprintEditor();
                return;
            }
            precisionEnabled = false;
            Cancel();
        }

        private void CancelBlueprintPlacement(Player player)
        {
            int requested = currentBlueprintPlacements.Count;
            for (int index = currentBlueprintPlacements.Count - 1; index >= 0; --index)
            {
                GameObject placed = currentBlueprintPlacements[index];
                if (placed) pendingBlueprintRollbacks.Add(new BlueprintRollbackItem(placed));
            }
            currentBlueprintPlacements.Clear();
            int failed = RetryPendingBlueprintRollbacks(force: true);
            precisionEnabled = false;
            Finish();
            ShowStatus(player, failed == 0
                ? "BuildWorks: незавершённый чертёж отменён; удалено деталей: " + requested + "."
                : "BuildWorks: отмена завершается; повтор удаления деталей: " + failed + ".");
        }

        private int RetryPendingBlueprintRollbacks(bool force = false)
        {
            if (pendingBlueprintRollbacks.Count == 0) return 0;
            if (!force && Time.unscaledTime < nextBlueprintRollbackRetry)
                return pendingBlueprintRollbacks.Count;
            nextBlueprintRollbackRetry = Time.unscaledTime + 1f;
            for (int index = pendingBlueprintRollbacks.Count - 1; index >= 0; --index)
            {
                BlueprintRollbackItem item = pendingBlueprintRollbacks[index];
                if (!item.Placed || TryRollbackBlueprintPiece(item))
                    pendingBlueprintRollbacks.RemoveAt(index);
            }
            return pendingBlueprintRollbacks.Count;
        }

        private static bool TryRollbackBlueprintPiece(BlueprintRollbackItem item)
        {
            if (!item.Placed) return true;
            if (!item.RemovalNotified)
            {
                item.RemovalNotified = true;
                try
                {
                    item.Placed.GetComponent<IRemoved>()?.OnRemoved();
                }
                catch (Exception exception)
                {
                    Debug.LogError("BuildWorks blueprint removal listener failed: " + exception);
                }
            }
            if (!item.ResourcesDropped)
            {
                item.ResourcesDropped = true;
                try
                {
                    item.Placed.GetComponent<Piece>()?.DropResources(null);
                }
                catch (Exception exception)
                {
                    Debug.LogError("BuildWorks blueprint resource refund failed: " + exception);
                }
            }
            if (!item.NativeRemovalRequested)
            {
                item.NativeRemovalRequested = true;
                WearNTear wear = item.Placed.GetComponent<WearNTear>();
                if (wear)
                {
                    item.WaitForNativeRemoval = true;
                    item.ForceDestroyAt = Time.unscaledTime + 1f;
                    try
                    {
                        wear.Remove(true);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogError("BuildWorks native blueprint removal failed; using fallback: " + exception);
                    }
                }
            }
            if (!item.Placed) return true;
            if (item.WaitForNativeRemoval && Time.unscaledTime < item.ForceDestroyAt)
                return false;
            try
            {
                ZNetView view = item.Placed.GetComponent<ZNetView>();
                if (view && view.IsValid() && !view.IsOwner()) view.ClaimOwnership();
                if (ZNetScene.instance) ZNetScene.instance.Destroy(item.Placed);
                else UnityEngine.Object.Destroy(item.Placed);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError("BuildWorks could not roll back blueprint piece; retrying: " + exception);
                return false;
            }
        }

        private sealed class BlueprintRollbackItem
        {
            public readonly GameObject Placed;
            public bool RemovalNotified;
            public bool ResourcesDropped;
            public bool NativeRemovalRequested;
            public bool WaitForNativeRemoval;
            public float ForceDestroyAt;

            public BlueprintRollbackItem(GameObject placed) => Placed = placed;
        }

        private void Finish(bool restoreCursor = true, bool preserveContinuation = false)
        {
            bool restoreWorldSelectionGhost =
                sessionMode == SessionMode.BlueprintWorldSelection;
            pendingWorldBlueprintSelection = false;
            EndBlueprintSelection(clearSelection: true);
            Piece restorePiece = sessionMode == SessionMode.BlueprintEditor
                ? editorReturnPiece
                : blueprintPalettePiece;
            ClearBlueprintWorkspace();
            RestoreBlueprintDraft();
            RestoreAutomaticCooldown(Player.m_localPlayer);
            if (!preserveContinuation) ClearContinuation();
            state = PlacementState.Inactive;
            passiveCursorActive = false;
            placementGhost = null;
            selectedPiece = null;
            activeBlueprint = null;
            activeBlueprintPieces.Clear();
            currentBlueprintPlacements.Clear();
            blueprintRootPosition = Vector3.zero;
            blueprintRootRotation = Quaternion.identity;
            blueprintPivotLocal = Vector3.zero;
            blueprintCompositeFrame = false;
            blueprintPalettePiece = null;
            editorReturnPiece = null;
            sessionMode = SessionMode.None;
            singlePieceAnchorLocalPoints = Array.Empty<Vector3>();
            dragAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            pinnedAnchorPoint = -1;
            anchorConstraintAxis = GizmoAxis.None;
            dragConstraintActive = false;
            alignmentTargetVisible = false;
            ClearSnapDrag();
            ClearLayout();
            automaticPlacementRunning = false;
            automaticPlacementReachedTryPlace = false;
            history.Clear();
            anchorBoundsAvailable = false;
            gizmo.Hide();
            previews.Clear();
            hud.Hide();
            EndFreeView(restoreCamera: true);
            RestoreExternalBuildCamera();
            if (restoreCursor)
            {
                RestoreCursor();
            }
            else
            {
                cursorStateSaved = false;
            }
            Player player = Player.m_localPlayer;
            if (player && restoreWorldSelectionGhost)
            {
                try
                {
                    allowNativeGhostUpdate = true;
                    UpdatePlacementGhostMethod.Invoke(player, new object[] { false });
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "BuildWorks could not restore the Hammer ghost after world selection: " +
                        exception);
                }
                finally
                {
                    allowNativeGhostUpdate = false;
                }
            }
            if (player && restorePiece)
            {
                if (blueprintPieceRegistry.TryGetBlueprint(
                    restorePiece,
                    out CompositeBlueprintStore.Blueprint blueprint))
                {
                    blueprintPieceRegistry.Select(player, blueprint);
                }
                else
                {
                    PieceTable table = GetBuildPieceTable(player);
                    if (table) HammerCatalogOrganizer.EnsureVisible(table, restorePiece);
                    blueprintPieceRegistry.SelectPiece(player, restorePiece);
                }
            }
        }

        private void RestoreAutomaticCooldown(Player player)
        {
            if (!automaticCooldownOverridden) return;
            if (player)
                LastToolUseTimeField.SetValue(player, automaticPreviousLastToolUseTime);
            automaticCooldownOverridden = false;
        }

        private void UpdatePassiveCursor(Player player)
        {
            bool shouldRelease = precisionEnabled && GameplayInputAvailable(player) &&
                Input.GetKey(BuildWorksPlugin.PassiveCursorKey.Value);
            if (shouldRelease)
            {
                ActivatePassiveCursor();
            }
            else if (passiveCursorActive)
            {
                passiveCursorActive = false;
                RestoreCursor();
            }
        }

        private void ActivatePassiveCursor()
        {
            if (!passiveCursorActive)
            {
                SaveAndUnlockCursor();
                passiveCursorActive = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CaptureContinuation()
        {
            ClearContinuation();
            if (activeBlueprint != null || !chainEnabled || repeatSymmetric || planeSecondCount >= 2 ||
                placementPlan.Count < 2 || !selectedPiece)
                return;

            TransformSnapshot previous = placementPlan[placementPlan.Count - 2];
            TransformSnapshot last = placementPlan[placementPlan.Count - 1];
            Quaternion delta = last.Rotation * Quaternion.Inverse(previous.Rotation);
            continuationTransform = new TransformSnapshot(
                last.Position + (last.Position - previous.Position),
                delta * last.Rotation);
            continuationPiece = selectedPiece;
            continuationPending = true;
        }

        private void ClearContinuation()
        {
            continuationPending = false;
            continuationPiece = null;
            continuationTransform = default(TransformSnapshot);
        }

        private bool ContextStillValid(Player player)
        {
            if (sessionMode == SessionMode.BlueprintEditor ||
                sessionMode == SessionMode.BlueprintWorldSelection)
                return player && player == Player.m_localPlayer;
            return PlacementSelectionStillValid(player) && placementGhost.activeInHierarchy;
        }

        private bool PlacementSelectionStillValid(Player player)
        {
            if (!player || player != Player.m_localPlayer || !selectedPiece ||
                player.GetSelectedPiece() != selectedPiece) return false;

            GameObject hostGhost = PlacementGhostField.GetValue(player) as GameObject;
            if (!hostGhost) return false;
            if (!ReferenceEquals(hostGhost, placementGhost))
            {
                if (state != PlacementState.Armed) return false;
                placementGhost = hostGhost;
            }
            return true;
        }

        private bool GameplayInputAvailable(Player player)
        {
            try
            {
                probingNativeInput = true;
                object result = TakeInputMethod.Invoke(player, null);
                return result is bool allowed && allowed;
            }
            catch
            {
                return false;
            }
            finally
            {
                probingNativeInput = false;
            }
        }

        private void BeginFreeView(Camera camera, bool isolateBlueprint)
        {
            freeViewCamera = camera;
            previousCameraPosition = camera.transform.position;
            previousCameraRotation = camera.transform.rotation;
            previousCameraCullingMask = camera.cullingMask;
            previousCameraClearFlags = camera.clearFlags;
            previousCameraBackground = camera.backgroundColor;
            previousCameraAllowHdr = camera.allowHDR;
            previousCameraAllowMsaa = camera.allowMSAA;
            isolatedEditorView = isolateBlueprint;
            gizmo.SetLayer(isolatedEditorView ? EditorLayer : Physics.IgnoreRaycastLayer);
            if (isolatedEditorView)
            {
                camera.cullingMask = 1 << EditorLayer;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.18f, 0.20f, 0.22f, 1f);
                camera.allowHDR = false;
                camera.allowMSAA = false;
                ApplyEditorGhostLayer(placementGhost);
                DisableEditorCameraEffects(camera);
                ConfigureEditorLighting();
            }
            freeViewPanOffset = Vector3.zero;

            Vector3 look = currentPosition - previousCameraPosition;
            freeViewDistance = Mathf.Clamp(look.magnitude, 0.75f, 30f);
            if (look.sqrMagnitude < 0.01f)
            {
                look = camera.transform.forward;
            }
            else
            {
                look.Normalize();
            }
            freeViewYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            freeViewPitch = -Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg;
            freeViewActive = true;
            ApplyFreeView();
        }

        private void FocusFreeView(Camera camera)
        {
            Vector3 look = currentPosition - camera.transform.position;
            if (look.sqrMagnitude < 0.01f)
            {
                return;
            }
            freeViewDistance = Mathf.Clamp(look.magnitude, 0.75f, 30f);
            look.Normalize();
            freeViewYaw = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
            freeViewPitch = -Mathf.Asin(Mathf.Clamp(look.y, -1f, 1f)) * Mathf.Rad2Deg;
            freeViewPanOffset = Vector3.zero;
        }

        private void ApplyFreeView()
        {
            if (!freeViewActive || !freeViewCamera)
            {
                return;
            }

            Quaternion rotation = Quaternion.Euler(freeViewPitch, freeViewYaw, 0f);
            Vector3 pivot = currentPosition + freeViewPanOffset;
            // ponytail: no collision raycast yet; add one only if walls make editing impractical.
            Vector3 position = pivot - rotation * Vector3.forward * freeViewDistance;
            freeViewCamera.transform.SetPositionAndRotation(position, rotation);
            if (isolatedEditorView) UpdateEditorLightDirections(rotation);
        }

        private void EndFreeView(bool restoreCamera)
        {
            if (freeViewActive && freeViewCamera)
            {
                if (restoreCamera)
                {
                    freeViewCamera.transform.SetPositionAndRotation(
                        previousCameraPosition,
                        previousCameraRotation);
                }
                freeViewCamera.cullingMask = previousCameraCullingMask;
                freeViewCamera.clearFlags = previousCameraClearFlags;
                freeViewCamera.backgroundColor = previousCameraBackground;
                freeViewCamera.allowHDR = previousCameraAllowHdr;
                freeViewCamera.allowMSAA = previousCameraAllowMsaa;
            }
            RestoreEditorLighting();
            RestoreEditorCameraEffects();
            DestroyEditorPlatform();
            RestoreEditorGhostLayers();
            gizmo.SetLayer(Physics.IgnoreRaycastLayer);
            freeViewCamera = null;
            isolatedEditorView = false;
            freeViewActive = false;
            freeViewDragging = false;
            freeViewFlyLooking = false;
            freeViewPanOffset = Vector3.zero;
        }

        private void DisableEditorCameraEffects(Camera camera)
        {
            disabledEditorCameraEffects.Clear();
            foreach (Behaviour effect in camera.GetComponents<Behaviour>())
            {
                if (!effect || !effect.enabled) continue;
                Type type = effect.GetType();
                string identity = type.FullName ?? type.Name;
                if (identity.IndexOf("PostProcess", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                effect.enabled = false;
                disabledEditorCameraEffects.Add(effect);
            }
        }

        private void RestoreEditorCameraEffects()
        {
            foreach (Behaviour effect in disabledEditorCameraEffects)
                if (effect) effect.enabled = true;
            disabledEditorCameraEffects.Clear();
        }

        private void CreateEditorPlatform(float floorY)
        {
            DestroyEditorPlatform();
            if (!isolatedEditorView) return;

            editorPlatform = GameObject.CreatePrimitive(PrimitiveType.Plane);
            editorPlatform.name = "BuildWorks_EditorPlatform";
            editorPlatform.hideFlags = HideFlags.HideAndDontSave;
            editorPlatform.layer = EditorLayer;
            editorPlatform.transform.SetPositionAndRotation(
                new Vector3(EditorWorkspaceOrigin.x, floorY, EditorWorkspaceOrigin.z),
                Quaternion.identity);
            editorPlatform.transform.localScale = new Vector3(
                EditorFloorSize / 10f,
                1f,
                EditorFloorSize / 10f);
            editorPlatformMaterial = CreateEditorPlatformMaterial();
            if (!editorPlatformMaterial)
            {
                Debug.LogWarning("BuildWorks editor platform has no supported neutral material.");
                DestroyEditorPlatform();
                return;
            }
            editorPlatform.GetComponent<Renderer>().sharedMaterial = editorPlatformMaterial;
        }

        private void DestroyEditorPlatform()
        {
            if (editorPlatform) UnityEngine.Object.Destroy(editorPlatform);
            if (editorPlatformMaterial) UnityEngine.Object.Destroy(editorPlatformMaterial);
            editorPlatform = null;
            editorPlatformMaterial = null;
        }

        private static Material CreateEditorPlatformMaterial()
        {
            Shader shader = Shader.Find("Unlit/Color");
            Material template = null;
            if (!shader || !shader.isSupported)
            {
                PieceTable table = GetBuildPieceTable(Player.m_localPlayer);
                foreach (GameObject prefab in table?.m_pieces ?? new List<GameObject>())
                {
                    Renderer renderer = prefab
                        ? prefab.GetComponentInChildren<Renderer>(true)
                        : null;
                    if (!renderer || !renderer.sharedMaterial ||
                        !renderer.sharedMaterial.shader ||
                        !renderer.sharedMaterial.shader.isSupported) continue;
                    template = renderer.sharedMaterial;
                    break;
                }
                if (!template) return null;
            }
            var material = shader && shader.isSupported
                ? new Material(shader)
                : new Material(template.shader);
            material.name = "BuildWorks_EditorFloorMaterial";
            material.hideFlags = HideFlags.HideAndDontSave;
            Color neutral = new Color(0.26f, 0.28f, 0.30f, 1f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", neutral);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", neutral);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", Texture2D.grayTexture);
            return material;
        }

        private void ConfigureEditorLighting()
        {
            RestoreEditorLighting();
            const int editorMask = 1 << EditorLayer;
            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None))
            {
                if (!light || !light.enabled || (light.cullingMask & editorMask) == 0)
                    continue;
                editorLightMasks[light] = light.cullingMask;
                light.cullingMask &= ~editorMask;
            }
            switch (editorLightingPreset)
            {
                case 1:
                    CreateEditorLight("Key", new Color(1f, 0.95f, 0.88f), 1.00f);
                    CreateEditorLight("Fill", new Color(0.78f, 0.88f, 1f), 0.55f);
                    CreateEditorLight("Rim", Color.white, 0.35f);
                    break;
                case 2:
                    CreateEditorLight("Key", new Color(1f, 0.78f, 0.58f), 0.90f);
                    CreateEditorLight("Fill", new Color(0.62f, 0.75f, 1f), 0.65f);
                    CreateEditorLight("Rim", new Color(1f, 0.90f, 0.72f), 0.45f);
                    break;
                default:
                    CreateEditorLight("Key", new Color(1f, 0.98f, 0.94f), 0.80f);
                    CreateEditorLight("Fill", new Color(0.88f, 0.93f, 1f), 0.72f);
                    CreateEditorLight("Rim", Color.white, 0.38f);
                    break;
            }
            if (freeViewCamera) UpdateEditorLightDirections(freeViewCamera.transform.rotation);
        }

        private void CreateEditorLight(string suffix, Color color, float intensity)
        {
            var lightObject = new GameObject("BuildWorks_Editor" + suffix + "Light")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = EditorLayer
            };
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = color;
            light.intensity = intensity;
            light.cullingMask = 1 << EditorLayer;
            light.shadows = LightShadows.None;
            editorLights.Add(lightObject);
        }

        private void UpdateEditorLightDirections(Quaternion viewRotation)
        {
            if (editorLights.Count < 3) return;
            editorLights[0].transform.rotation = viewRotation * Quaternion.Euler(20f, -35f, 0f);
            editorLights[1].transform.rotation = viewRotation * Quaternion.Euler(330f, 120f, 0f);
            editorLights[2].transform.rotation = viewRotation * Quaternion.Euler(5f, 170f, 0f);
        }

        private void CycleEditorLighting()
        {
            if (!isolatedEditorView) return;
            editorLightingPreset = (editorLightingPreset + 1) % 3;
            ConfigureEditorLighting();
        }

        private string EditorLightingName()
        {
            switch (editorLightingPreset)
            {
                case 1: return "СТУДИЯ";
                case 2: return "ТЁПЛЫЙ";
                default: return "МЯГКИЙ";
            }
        }

        private void RestoreEditorLighting()
        {
            foreach (KeyValuePair<Light, int> entry in editorLightMasks)
                if (entry.Key) entry.Key.cullingMask = entry.Value;
            editorLightMasks.Clear();
            foreach (GameObject light in editorLights)
                if (light) UnityEngine.Object.Destroy(light);
            editorLights.Clear();
        }

        private void ApplyEditorGhostLayer(GameObject ghost)
        {
            if (!ghost) return;
            foreach (Transform child in ghost.GetComponentsInChildren<Transform>(true))
            {
                GameObject target = child.gameObject;
                if (!editorGhostLayers.ContainsKey(target))
                    editorGhostLayers.Add(target, target.layer);
                target.layer = EditorLayer;
            }
        }

        private void RestoreEditorGhostLayers()
        {
            foreach (KeyValuePair<GameObject, int> entry in editorGhostLayers)
                if (entry.Key) entry.Key.layer = entry.Value;
            editorGhostLayers.Clear();
        }

        private bool TrySuspendExternalBuildCamera()
        {
            ResolveExternalBuildCamera();
            if (!externalBuildCameraDetected)
            {
                return true;
            }
            if (externalBuildCameraInModeMethod == null ||
                externalBuildCameraDisableMethod == null ||
                externalBuildCameraEnableMethod == null)
            {
                Debug.LogWarning("BuildWorks found an unsupported Build Camera version.");
                return false;
            }

            try
            {
                if (!(externalBuildCameraInModeMethod.Invoke(null, null) is bool active) || !active)
                {
                    return true;
                }

                externalBuildCameraView = externalBuildCameraViewField?.GetValue(null);
                externalBuildCameraSuspended = true;
                externalBuildCameraDisableMethod.Invoke(null, null);
                if (externalBuildCameraInModeMethod.Invoke(null, null) is bool stillActive &&
                    !stillActive)
                {
                    return true;
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not suspend Build Camera: " +
                    (exception.InnerException ?? exception));
            }

            RestoreExternalBuildCamera();
            return false;
        }

        private void RestoreExternalBuildCamera()
        {
            if (!externalBuildCameraSuspended)
            {
                return;
            }

            externalBuildCameraSuspended = false;
            Player player = Player.m_localPlayer;
            if (!player || externalBuildCameraEnableMethod == null)
            {
                externalBuildCameraView = null;
                return;
            }

            try
            {
                externalBuildCameraEnableMethod.Invoke(null, null);
                if (externalBuildCameraView != null && externalBuildCameraViewField != null)
                {
                    externalBuildCameraViewField.SetValue(null, externalBuildCameraView);
                }
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks could not restore Build Camera: " +
                    (exception.InnerException ?? exception));
                ShowStatus(player, "BuildWorks: включи Build Camera вручную.");
            }
            finally
            {
                externalBuildCameraView = null;
            }
        }

        private static void ResolveExternalBuildCamera()
        {
            if (externalBuildCameraResolved)
            {
                return;
            }
            externalBuildCameraResolved = true;

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type utils = assembly.GetType(ExternalBuildCameraUtilsTypeName, false);
                if (utils == null)
                {
                    continue;
                }

                externalBuildCameraDetected = true;
                BindingFlags methods = BindingFlags.Static | BindingFlags.Public |
                    BindingFlags.NonPublic;
                externalBuildCameraInModeMethod = utils.GetMethod(
                    "InBuildMode", methods, null, Type.EmptyTypes, null);
                externalBuildCameraDisableMethod = utils.GetMethod(
                    "DisableBuildMode", methods, null, Type.EmptyTypes, null);
                externalBuildCameraEnableMethod = utils.GetMethod(
                    "EnableBuildMode", methods, null, Type.EmptyTypes, null);
                Type plugin = assembly.GetType(ExternalBuildCameraPluginTypeName, false);
                externalBuildCameraViewField = plugin?.GetField(
                    "buildCameraViewDirection", methods);
                return;
            }
        }

        private static void ShowStatus(Player player, string text)
        {
            if (player)
            {
                player.Message(MessageHud.MessageType.Center, text, 0, null);
            }
        }

        private static bool SupportsPrecisionPlacement(Piece piece)
        {
            return piece &&
                !piece.m_repairPiece &&
                !piece.m_removePiece &&
                !piece.m_groundPiece &&
                !piece.m_groundOnly &&
                !piece.m_cultivatedGroundOnly &&
                !piece.m_vegetationGroundOnly &&
                !piece.m_waterPiece &&
                !piece.m_noInWater &&
                !piece.m_notOnWood &&
                !piece.m_notOnTiltingSurface &&
                !piece.m_inCeilingOnly &&
                !piece.m_notOnFloor &&
                !piece.m_mustConnectTo &&
                piece.m_blockRadius <= 0f &&
                !piece.GetComponent<StationExtension>();
        }

        private static bool SupportsBlueprintWorkspacePiece(Piece piece)
        {
            return piece && !piece.m_repairPiece && !piece.m_removePiece;
        }

        private bool CaptureAnchorPoints(GameObject ghost)
        {
            if (!TryCaptureAnchorBounds(
                ghost,
                currentPosition,
                currentRotation,
                out AnchorBounds boundsResult))
            {
                return false;
            }

            List<Vector3> points = new List<Vector3>(
                NativeAnchorStart + MaximumSourceNativeSnapPoints);
            for (int anchor = 0; anchor < AnchorAdjustment.AnchorCount; ++anchor)
            {
                points.Add(ToUnity(boundsResult.Anchor(anchor)));
            }
            points.Add(ToUnity(boundsResult.Center));

            sourceNativeSnapPoints.Clear();
            Piece ghostPiece = ghost.GetComponent<Piece>();
            if (ghostPiece)
            {
                ghostPiece.GetSnapPoints(sourceNativeSnapPoints);
            }
            for (int index = 0;
                index < sourceNativeSnapPoints.Count &&
                index < MaximumSourceNativeSnapPoints;
                ++index)
            {
                Transform snapPoint = sourceNativeSnapPoints[index];
                if (!snapPoint) continue;
                Vector3 local = Quaternion.Inverse(currentRotation) *
                    (snapPoint.position - currentPosition);
                if (!IsFinite(local)) continue;
                points.Add(local);
            }

            anchorLocalPoints = points.ToArray();
            anchorWorldPoints = new Vector3[anchorLocalPoints.Length];
            return true;
        }

        private bool TryCaptureAnchorBounds(
            GameObject target,
            Vector3 origin,
            Quaternion rotation,
            out AnchorBounds result)
        {
            result = default;
            MeshRenderer[] renderers = target.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length == 0)
            {
                return false;
            }

            Quaternion inverseRotation = Quaternion.Inverse(rotation);
            anchorBoundsPoints.Clear();
            foreach (MeshRenderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (!filter || !filter.sharedMesh)
                {
                    continue;
                }
                Bounds bounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 meshPoint = new Vector3(
                        (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                        (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                    Vector3 world = filter.transform.TransformPoint(meshPoint);
                    Vector3 local = inverseRotation * (world - origin);
                    anchorBoundsPoints.Add(ToGeometry(local));
                }
            }

            try
            {
                result = AnchorAdjustment.CreateBounds(anchorBoundsPoints);
            }
            catch (ArgumentException)
            {
                return false;
            }
            return true;
        }

        private Vector3[] UpdateAnchorWorldPoints()
        {
            if (!anchorBoundsAvailable)
            {
                return null;
            }
            for (int anchor = 0; anchor < anchorWorldPoints.Length; ++anchor)
            {
                anchorWorldPoints[anchor] = currentPosition +
                    currentRotation * anchorLocalPoints[anchor];
            }
            return anchorWorldPoints;
        }

        private static Point3 ToGeometry(Vector3 value)
        {
            return new Point3(value.x, value.y, value.z);
        }

        private static Rotation3 ToGeometry(Quaternion value)
        {
            return new Rotation3(value.x, value.y, value.z, value.w);
        }

        private static Vector3 ToUnity(Point3 value)
        {
            return new Vector3((float)value.X, (float)value.Y, (float)value.Z);
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
                !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
                !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }

        private bool MouseOverEditorPanel(Vector2 mouse)
        {
            return hud.ContainsScreenPoint(mouse);
        }

        private void SaveAndUnlockCursor()
        {
            if (!cursorStateSaved)
            {
                previousCursorLock = Cursor.lockState;
                previousCursorVisible = Cursor.visible;
                cursorStateSaved = true;
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void RestoreCursor()
        {
            if (!cursorStateSaved)
            {
                return;
            }
            Cursor.lockState = previousCursorLock;
            Cursor.visible = previousCursorVisible;
            cursorStateSaved = false;
        }

        private static Vector3 CanonicalAxis(GizmoAxis axis)
        {
            switch (axis)
            {
                case GizmoAxis.X: return Vector3.right;
                case GizmoAxis.Y: return Vector3.up;
                case GizmoAxis.Z: return Vector3.forward;
                default: return Vector3.zero;
            }
        }

        private static Vector3 SignedEuler(Quaternion rotation)
        {
            Vector3 euler = rotation.eulerAngles;
            euler.x = (float)PrecisionAdjustment.NormalizeDegrees(euler.x);
            euler.y = (float)PrecisionAdjustment.NormalizeDegrees(euler.y);
            euler.z = (float)PrecisionAdjustment.NormalizeDegrees(euler.z);
            return euler;
        }

    }
}

