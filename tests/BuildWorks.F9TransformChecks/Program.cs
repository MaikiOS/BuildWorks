using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Quaternion = HostQuaternion;
using Camera = HostCamera;
using Input = HostInput;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private enum SessionMode { None, WorldPrecision, BlueprintWorldPlacement, BlueprintEditor, BlueprintWorldSelection, ProjectPart }
        private enum PlacementState { Inactive, Editing, Frozen, Armed }
        private PlacementState state = PlacementState.Editing;
        private SessionMode sessionMode = SessionMode.None;
        private BlueprintWorkspacePart blueprintWorkspaceEditPart = null;
        private CompositeBlueprintStore.Blueprint activeBlueprint;
        private readonly List<HostPiece> activeBlueprintPieces = new List<HostPiece>();
        private bool freeViewFlyLooking = false, localSpace, alignmentTargetVisible = false;
        private readonly List<int> contourSupports = new List<int>();
        private readonly TransformGizmoView gizmo = new TransformGizmoView();
        private GizmoMode gizmoMode = GizmoMode.Move;
        private GizmoAxis dragAxis = GizmoAxis.None, repeatAxis = GizmoAxis.None;
        private GizmoHandleKind dragHandle = GizmoHandleKind.None;
        private bool planeDraggingSecond = false, repeatCountSecond = false, dragConstraintActive = false;
        private int pinnedAnchorPoint = -1, dragAnchorPoint = -1, suppressPlacementFrame = 0;
        private int selectedAnchorPoint = -1, anchorDragCalls;
        private int translationStepIndex = 0, rotationStepIndex = 0, planeSecondCount = 1, copyCount = 1;
        private Vector3 currentPosition = new Vector3(2, 3, 4), basePosition = new Vector3(2, 3, 4);
        private Quaternion currentRotation = Quaternion.AngleAxis(30, Vector3.forward), dragStartRotation;
        private Quaternion baseRotation;
        private Vector3 currentScale = Vector3.one, dragStartScale = Vector3.one;
        private TransformSnapshot dragStartSnapshot, baseSnapshot;
        private readonly BoundedUndoHistory<TransformSnapshot> history = new BoundedUndoHistory<TransformSnapshot>(20);
        private TransformSnapshot[] blueprintPartTransforms;
        private Point3[][] blueprintSnapLocalSets;
        private bool blueprintPartsDirty = false;
        private Vector3 blueprintRootPosition, blueprintPivotLocal, repeatStep, planeSecondStep;
        private Quaternion blueprintRootRotation = Quaternion.identity;
        private OstrixMods.BuildWorks.Geometry.BlueprintEditorPart dragTransformPart;
        private Vector3 dragTransformPivot;
        private HostPiece selectedPiece = new HostPiece();
        private Vector3 dragStartPosition, dragAxisWorld, freeViewPanOffset = Vector3.zero;
        private Vector2 dragStartMouse, dragScreenDirection;
        private float dragWorldUnitsPerPixel, dragStartScreenAngle, dragRotationScreenSign;
        private bool anchorBoundsAvailable = true;
        private Vector3[] anchorLocalPoints = { new Vector3(2, 0, 0) };
        private Vector3[] anchorWorldPoints = new Vector3[1];
        private GizmoAxis anchorConstraintAxis = GizmoAxis.None;
        private bool blueprintCompositeFrame, recaptureSucceeds = true;
        private Vector3[] recapturedAnchors;
        private int frameRebuilds, recaptureCalls, blueprintEditPartIndex = -1;
        private bool dragMagneticMove, dragSourceIsNative, snapTargetVisible, snapTargetIsNative;
        private readonly List<int> snapTargets = new List<int>(), freeSnapTargets = new List<int>(),
            snapPreviewTargets = new List<int>(), snapPreviewNative = new List<int>(), snapEdges = new List<int>();
        private const int MaximumExpandedPlacements = BlueprintEditorDocument.MaximumParts;
        private readonly List<TransformSnapshot> placementPlan = new List<TransformSnapshot>();
        private readonly List<HostPiece> placementPlanPieces = new List<HostPiece>();
        private readonly List<int> previewContacts = new List<int>(), previewAims = new List<int>();
        private readonly List<Vector3> contourPath = new List<Vector3>();
        private readonly HostPreviews previews = new HostPreviews();
        private string repeatPlanError, lastStatus;
        private bool expandedPlanLimited = false, selectingBlueprint = false, automaticPlacementPaused = false, gameplayAllowed = true;
        private bool precisionEnabled = false;
        private int consumedWorldSelectionEscapeFrame, freezes, modeCancels, cameraBackwards;
        private float planeDraggedDistance = 0;
        private readonly Camera freeViewCamera = new Camera();
        private readonly HostHud hud = new HostHud();
        private sealed class HostHud { internal bool IsEditingAngle = false; }
        private HostGameObject placementGhost;
        private Vector3[] singlePieceAnchorLocalPoints;
        private bool continuationPending = false, passiveCursorActive = false;
        private HostPiece continuationPiece = null;
        private TransformSnapshot continuationTransform = default;
        private BlueprintWorkspacePart workspaceHit;
        private sealed class BlueprintWorkspacePart { internal HostGameObject Visual; internal HostPiece Source; }
        // Entry-route seams supply host selection/hit/bounds; production methods retain all setup ordering.
        private sealed class HostGhostField { internal object GetValue(Player player) => player.Ghost; }
        private static readonly HostGhostField PlacementGhostField = new HostGhostField();
        private sealed class HostBlueprintRegistry
        {
            internal bool TryGetBlueprint(HostPiece piece, out CompositeBlueprintStore.Blueprint blueprint)
            { blueprint = null; return false; }
        }
        private readonly HostBlueprintRegistry blueprintPieceRegistry = new HostBlueprintRegistry();
        private static bool SupportsWorldPrecisionPlacement(HostPiece piece) => true;
        private bool BeginBlueprintPlacement(Player player, CompositeBlueprintStore.Blueprint blueprint, bool editBeforePlacement)
            => throw new NotSupportedException();
        private bool TrySuspendExternalBuildCamera() => true;
        private void RestoreExternalBuildCamera() { }
        private void SaveAndUnlockCursor() { }
        private void BeginFreeView(Camera camera, bool isolateBlueprint) { }
        private void ClearContinuation() { continuationPending = false; }
        private bool TryGetBlueprintWorkspaceHit(Camera camera, Vector2 mouse, out HostRaycastHit hit)
        { hit = new HostRaycastHit { collider = workspaceHit }; return workspaceHit != null; }
        private BlueprintWorkspacePart FindBlueprintWorkspacePart(object collider) => (BlueprintWorkspacePart)collider;
        private bool CaptureAnchorPoints(HostGameObject ghost)
        {
            anchorLocalPoints = ghost.CapturedAnchors;
            anchorWorldPoints = new Vector3[anchorLocalPoints.Length];
            selectedAnchorPoint = -1;
            return anchorLocalPoints.Length > 0;
        }

        // These layout/UI branches are outside the mouse-ring state test. Physics and placement never run.
        private void CaptureDragLayoutState() { }
        private void RestoreDragLayoutState() { }
        private void RotateLocalLayoutFromDragStart() { }
        private void RebuildActiveLayout() { if (IsBlueprintTransform) RebuildBlueprintPlan(); }
        private void RebuildRepeatPlan() { }
        private void ClearLayout()
        {
            placementPlan.Clear(); placementPlanPieces.Clear();
            repeatAxis = GizmoAxis.None; contourSupports.Clear(); contourPath.Clear();
        }
        // Only bounds capture and host visuals are substituted; the actual frame-switch method owns state clearing.
        private bool ApplyBlueprintAnchors()
        {
            ++recaptureCalls;
            if (!recaptureSucceeds) return false;
            anchorLocalPoints = recapturedAnchors;
            anchorWorldPoints = new Vector3[anchorLocalPoints.Length];
            return true;
        }
        private void ApplyEditingGhostTransform() { ++frameRebuilds; }
        private bool MouseOverEditorPanel(Vector2 m) => false;
        private void CycleBlueprintEditTarget(int step) => throw new NotSupportedException();
        private void RestartAnchorDragForConstraint() { }
        private void TryBuildContourFromClick(Camera c, Vector2 m) => throw new NotSupportedException();
        private void BeginAnchorDrag(Camera c, int index, bool magnetic) { ++anchorDragCalls; }
        private int HitTestBlueprintPart(Camera c, Vector2 m) => throw new NotSupportedException();
        private void SelectBlueprintEditTarget(int part) => throw new NotSupportedException();
        private void UpdateAnchorDrag(Camera c, Vector2 m) => throw new NotSupportedException();
        private bool TryAutoAlignToTouchingPiece() => false;
        private void UpdatePlaneDrag(Vector2 m) => throw new NotSupportedException();
        private void UpdateRepeatDrag(Vector2 m) => throw new NotSupportedException();
        private void ShowStatus(object player, string message) { lastStatus = message; }
        private static bool IsFinite(Vector3 value) => !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        private bool GameplayInputAvailable(object player) => gameplayAllowed;
        private void UpdatePassiveCursor(object player) { }
        private void HandleBlueprintWorkspaceInput(object player, Camera camera) { }
        private void HandleBlueprintSelectionInput(Camera camera, Vector3 mouse) { }
        private void FreezeEditor() { ++freezes; state = PlacementState.Frozen; }
        private void Cancel(bool restoreCursor = true) { ++modeCancels; state = PlacementState.Inactive; }
        private void Finish() { state = PlacementState.Inactive; }
        private void CancelBlueprintPlacement(object player) { ++modeCancels; state = PlacementState.Inactive; }
        private void CancelBlueprintEditor() { ++modeCancels; state = PlacementState.Inactive; }
        private void EndBlueprintWorkspacePrecision(bool restoreTransform) { ++modeCancels; blueprintWorkspaceEditPart = null; }
        private void EndBlueprintSelection(bool clearSelection) { selectingBlueprint = false; }
        private void CancelBlueprintSelectionDrag() { }
        private void ReopenEditor(object player, string message) { state = PlacementState.Editing; }
        private void HandleFreeViewInput(Camera camera)
        {
            freeViewFlyLooking = Input.GetMouseButton(1);
            if (freeViewFlyLooking && MovementFromActualCamera(Quaternion.identity).z < 0) ++cameraBackwards;
        }

        private static int checks, failures;
        private static void Check(bool value, string name)
        { ++checks; if (!value) { ++failures; Console.WriteLine("FAIL " + name); } }

        private static void Ring(bool local, bool select, bool shift = true)
        {
            var session = new PrecisionPlacementSession { localSpace = local };
            var camera = new Camera();
            camera.transform.position = session.currentPosition + new Vector3(0, 10, 10);
            Vector3 rootBefore = session.currentPosition;
            Vector3 anchorBefore = session.UpdateAnchorWorldPoints()[0];
            Quaternion rotationBefore = session.currentRotation;
            if (select)
            {
                session.gizmo.Anchor = 0;
                Input.Frame(anchorBefore, true, true, shift);
                session.HandleGizmoInput(camera);
                Check(session.selectedAnchorPoint == 0 && session.pinnedAnchorPoint == (shift ? 0 : -1),
                    "actual click did not select the pivot independently from the fixed pin");
            }
            session.gizmo.Anchor = -1;
            Vector3 pivot = select ? anchorBefore : rootBefore;
            Input.Frame(pivot + Vector3.right * 100, true, true);
            session.HandleGizmoInput(camera);
            Check(Vector3.Distance(session.gizmo.LastRotationHitPivot, pivot) < 0.0001f,
                (local ? "local" : "world") + " ring hit-test ignores selected pivot=" + select);
            Input.Frame(pivot + Vector3.up * 100, false, true);
            session.HandleGizmoInput(camera);
            Check(Vector3.Distance(rotationBefore * Vector3.forward, session.currentRotation * Vector3.forward) > 0.1f,
                "positive control: actual ring route did not rotate");
            Check(select ? Vector3.Distance(session.UpdateAnchorWorldPoints()[0], anchorBefore) < 0.0001f :
                Vector3.Distance(session.currentPosition, rootBefore) < 0.0001f,
                (local ? "local" : "world") + " ring moved " + (select ? "selected anchor" : "default root center"));
        }

        internal static int Run()
        {
            Check(Vector3.Distance(Quaternion.AngleAxis(90, Vector3.up) * Vector3.forward, Vector3.right) < 0.0001f,
                "host quaternion positive control");
            Ring(false, true); Ring(true, true); Ring(false, false);
            Ring(false, true, false); Ring(true, true, false);
            NormalChoice();
            FrameSwitch(false, 3, 0, 5);
            FrameSwitch(true, 5, 0, 3);
            FrameSwitch(true, 5, 4, 3);
            FrameSwitch(false, 3, 0, 5, false);
            FrameSwitch(true, 5, 4, 3, false);
            UnchangedFrame();
            EscapePriority();
            KeyboardMoveAndRotation();
            NumericPlanePair();
            RelativeScaleLifecycle();
            ScaleBoundsAndAtomicRefusal();
            GroupScaleAndHistory();
            GroupShearRefusal();
            InputOwnershipAndIdlePreview();
            EntryAnchorHistory();
            Console.WriteLine((failures == 0 ? "PASS " : "FAIL ") + checks + " checks; failures=" + failures +
                "; actual session keyboard/Escape/TRS/history/final-plan routes; engine/render/physics/native host substituted.");
            return failures == 0 ? 0 : 1;
        }

        private static PrecisionPlacementSession Fresh(float scale = 1f, Vector3? basis = null)
        {
            Input.Focused = true;
            Input.Frame(new Vector3(100, 100, 0), false, false);
            var session = new PrecisionPlacementSession { currentRotation = Quaternion.identity, currentScale = Vector3.one * scale };
            session.selectedPiece.transform.localScale = basis ?? Vector3.one;
            session.baseSnapshot = session.CaptureHistorySnapshot();
            session.history.Reset(session.baseSnapshot);
            return session;
        }

        private static void EntryAnchorHistory()
        {
            foreach (bool workspace in new[] { false, true })
            {
                Input.Focused = true;
                Input.Frame(new Vector3(100, 100, 0), false, false);
                var session = new PrecisionPlacementSession {
                    state = PlacementState.Inactive, anchorBoundsAvailable = false,
                    anchorLocalPoints = Array.Empty<Vector3>(), anchorWorldPoints = Array.Empty<Vector3>() };
                for (int entry = 0; entry < 2; ++entry)
                {
                    var expected = entry == 0 ? new[] { Vector3.right * 2, Vector3.up * 3 } :
                        new[] { Vector3.left * 7, Vector3.down * 4, Vector3.forward * 5 };
                    var piece = new HostPiece { name = "entry_" + entry };
                    piece.transform.localScale = new Vector3(2, 3, 4);
                    var ghost = new HostGameObject { CapturedAnchors = (Vector3[])expected.Clone() };
                    ghost.transform.position = new Vector3(10 + entry, 20, 30);
                    ghost.transform.localScale = piece.transform.localScale * (workspace ? .5f : 1f);
                    var player = new Player { SelectedPiece = piece, Ghost = workspace ? new HostGameObject() : ghost };
                    session.state = workspace ? PlacementState.Editing : PlacementState.Inactive;
                    session.sessionMode = workspace ? SessionMode.BlueprintEditor : SessionMode.None;
                    session.workspaceHit = new BlueprintWorkspacePart { Source = piece, Visual = ghost };
                    bool entered = workspace
                        ? session.BeginBlueprintWorkspacePrecision(player, session.freeViewCamera, Vector2.zero)
                        : session.TryBeginWorldPrecision(player, showErrors: true, useContinuation: false);
                    string label = (workspace ? "workspace" : "ordinary") + " entry " + entry;
                    Check(entered && session.state == PlacementState.Editing && !session.passiveCursorActive &&
                        session.selectedPiece == piece && session.selectedAnchorPoint == -1,
                        label + " did not initialize the actual entry route");
                    Check(session.anchorBoundsAvailable && session.anchorLocalPoints.SequenceEqual(expected) &&
                        session.baseSnapshot.AnchorPoints.SequenceEqual(expected) &&
                        session.singlePieceAnchorLocalPoints.SequenceEqual(expected) &&
                        session.baseSnapshot.PartTransforms == null && !session.baseSnapshot.BlueprintPivot.HasValue &&
                        Near(session.baseSnapshot.Position, session.currentPosition) &&
                        Near(session.baseRotation * Vector3.forward, session.currentRotation * Vector3.forward),
                        label + " baseline captured empty/stale anchors or previous blueprint state");
                    Vector3 baselinePosition = session.currentPosition, baselineScale = session.currentScale;
                    SelectPoint(session);
                    Vector3 pivot = session.ResolveTransformPivot();
                    Key(session, "", HostKeyCode.G); Key(session, "", HostKeyCode.X);
                    Key(session, "2"); Key(session, "", HostKeyCode.Return);
                    Check(!session.keyboardPreview && !Near(session.currentPosition, baselinePosition) &&
                        session.history.UndoCount == 1, label + " positive control: G must commit before Undo");
                    session.UndoTransform();
                    Check(session.anchorBoundsAvailable && session.anchorLocalPoints.SequenceEqual(expected) &&
                        Near(session.currentPosition, baselinePosition) && Near(session.currentScale, baselineScale) &&
                        session.selectedAnchorPoint == 0 && Near(session.ResolveTransformPivot(), pivot),
                        label + " G/Undo lost the current anchors or selected point");
                    session.RedoTransform();
                    Check(session.anchorBoundsAvailable && session.anchorLocalPoints.SequenceEqual(expected) &&
                        Near(session.currentPosition, baselinePosition + Vector3.right * 2) &&
                        Near(session.currentScale, baselineScale) &&
                        Near(session.currentRotation * Vector3.forward, session.baseRotation * Vector3.forward) &&
                        session.selectedAnchorPoint == 0 && Near(session.ResolveTransformPivot(), pivot + Vector3.right * 2),
                        label + " G/Redo lost the committed TRS, current anchors or selected pivot");
                    session.UndoTransform();
                    Check(session.anchorLocalPoints.SequenceEqual(expected) &&
                        Near(session.currentPosition, baselinePosition) && Near(session.currentScale, baselineScale) &&
                        session.selectedAnchorPoint == 0 && Near(session.ResolveTransformPivot(), pivot),
                        label + " second Undo failed to restore the entry baseline before S/Reset");
                    Key(session, "", HostKeyCode.S); Key(session, "2"); Key(session, "", HostKeyCode.Return);
                    Check(!session.keyboardPreview && Near(session.currentScale, baselineScale * 2),
                        label + " positive control: S must commit before Reset");
                    session.ResetTransform();
                    Check(session.anchorBoundsAvailable && session.anchorLocalPoints.SequenceEqual(expected) &&
                        Near(session.currentPosition, baselinePosition) && Near(session.currentScale, baselineScale) &&
                        session.selectedAnchorPoint == -1,
                        label + " S/Reset restored empty/stale anchors or retained selection");
                    if (session.anchorLocalPoints.Length > 0) SelectPoint(session);
                    Check(session.selectedAnchorPoint == 0 && Near(session.ResolveTransformPivot(), pivot),
                        label + " current point is not selectable again after Reset");
                }
            }
            var previousBlueprint = new PrecisionPlacementSession {
                activeBlueprint = new CompositeBlueprintStore.Blueprint(),
                blueprintPartTransforms = Array.Empty<TransformSnapshot>(), state = PlacementState.Inactive };
            var nextGhost = new HostGameObject { CapturedAnchors = new[] { Vector3.right * 9 } };
            var nextPlayer = new Player { SelectedPiece = new HostPiece(), Ghost = nextGhost };
            Check(previousBlueprint.TryBeginWorldPrecision(nextPlayer, true, false) &&
                previousBlueprint.baseSnapshot.PartTransforms == null &&
                previousBlueprint.baseSnapshot.AnchorPoints.SequenceEqual(nextGhost.CapturedAnchors),
                "ordinary entry captured previous blueprint transforms before clearing activeBlueprint");
        }

        private static PrecisionPlacementSession Group(bool rotated = false)
        {
            var session = Fresh();
            session.sessionMode = SessionMode.BlueprintWorldPlacement;
            session.activeBlueprint = new CompositeBlueprintStore.Blueprint();
            session.blueprintRootPosition = session.currentPosition;
            for (int index = 0; index < 2; ++index)
            {
                var piece = new HostPiece { name = "fixture_" + index };
                piece.transform.localScale = index == 0 ? new Vector3(2, 1, 1) : new Vector3(1, 3, 1);
                session.activeBlueprintPieces.Add(piece);
                session.activeBlueprint.parts.Add(new CompositeBlueprintStore.Part {
                    stableId = "fixture-part-" + index, prefabName = piece.name,
                    signText = "Fixture sign " + index, materialSeed = 7 + index,
                    position = new CompositeBlueprintStore.VectorData(Vector3.right * (index == 0 ? -1 : 1)),
                    rotation = new CompositeBlueprintStore.QuaternionData(rotated && index == 1
                        ? Quaternion.AngleAxis(45, Vector3.forward) : Quaternion.identity)
                });
            }
            session.activeBlueprint.ExtraLocalPoints = new[] { new[] { new Point3(.25, 0, 0) }, null };
            session.activeBlueprint.customSnapPoints = new List<CompositeBlueprintStore.VectorData> {
                new CompositeBlueprintStore.VectorData(new Vector3(-.5f, 0, 0)) };
            session.selectedPiece = session.activeBlueprintPieces[0];
            session.baseSnapshot = session.CaptureHistorySnapshot();
            session.history.Reset(session.baseSnapshot);
            return session;
        }

        private static void Key(PrecisionPlacementSession session, string text = "", params HostKeyCode[] keys)
        {
            Input.Command(text, keys);
            session.KeyboardFromActualUpdate(session.freeViewCamera);
        }

        private static void SelectPoint(PrecisionPlacementSession session)
        {
            session.gizmo.Anchor = 0;
            Input.Frame(session.UpdateAnchorWorldPoints()[0], true, true);
            session.HandleGizmoInput(session.freeViewCamera);
            Input.Frame(Input.mousePosition, false, false, up: true);
            session.HandleGizmoInput(session.freeViewCamera);
            session.gizmo.Anchor = -1;
        }

        private static bool Near(Vector3 left, Vector3 right) => Vector3.Distance(left, right) < .0002f;

        private static void EscapePriority()
        {
            for (int mode = 0; mode < 3; ++mode)
            {
                var session = mode == 2 ? Group() : Fresh();
                SelectPoint(session);
                if (mode == 1) session.gizmoMode = GizmoMode.Guide;
                Input.Command("", HostKeyCode.Escape);
                session.EscapeFromActualUpdate();
                Check(session.selectedAnchorPoint == -1 && session.state == PlacementState.Editing &&
                    session.freezes == 0 && session.modeCancels == 0, "actual Escape must deselect before mode/Guide cancellation " + mode);
                Input.Command("", HostKeyCode.Escape);
                session.EscapeFromActualUpdate();
                Check(mode == 2 ? session.modeCancels == 1 : session.freezes == 1,
                    "Escape without selection must preserve original mode behavior " + mode);
            }
        }

        private static void KeyboardMoveAndRotation()
        {
            var session = Fresh();
            SelectPoint(session);
            Vector3 start = session.currentPosition;
            Key(session, "", HostKeyCode.G); Key(session, "", HostKeyCode.X);
            Input.Frame(Input.mousePosition + Vector3.right * 20, false, false);
            session.KeyboardFromActualUpdate(session.freeViewCamera);
            Check(!Near(session.currentPosition, start), "positive control: mouse G preview must move");
            Key(session, "2");
            Check(Near(session.currentPosition, start + Vector3.right * 2), "numeric G replaces the complete mouse delta in meters");
            Key(session, "", HostKeyCode.Return);
            Check(!session.keyboardPreview && session.history.UndoCount == 1, "Enter commits one operation, not native placement");
            session.UndoTransform(); Check(Near(session.currentPosition, start), "G Undo restores full starting pose");
            session.RedoTransform(); Check(Near(session.currentPosition, start + Vector3.right * 2), "G Redo restores committed pose");

            session = Fresh();
            session.currentRotation = Quaternion.AngleAxis(30, Vector3.forward);
            SelectPoint(session);
            Vector3 pivot = session.ResolveTransformPivot();
            Quaternion original = session.currentRotation;
            Key(session, "", HostKeyCode.R); Key(session, "", HostKeyCode.X); Key(session, "", HostKeyCode.X); Key(session, "90");
            Quaternion expected = original * Quaternion.AngleAxis(90, Vector3.right);
            Check(session.localSpace && Quaternion.Angle(session.currentRotation, expected) < .05f, "repeated R axis toggles local frame");
            Check(Near(session.ResolveTransformPivot(), pivot), "numeric R keeps selected world pivot fixed");
            Key(session, "", HostKeyCode.Escape);
            Check(!session.keyboardPreview && Quaternion.Angle(session.currentRotation, original) < .05f, "actual Escape restores R operation before deselecting");
        }

        private static void NumericPlanePair()
        {
            var session = Fresh(); Vector3 start = session.currentPosition;
            Key(session, "", HostKeyCode.G); Key(session, "", HostKeyCode.X); Key(session, "", HostKeyCode.Y); Key(session, "2");
            Check(session.keyboardValid && Near(session.currentPosition, start + new Vector3(2, 2, 0)), "explicit numeric G pair sets both components");
            Key(session, "", HostKeyCode.Escape);
            Key(session, "", HostKeyCode.G); Key(session, "", HostKeyCode.LeftShift, HostKeyCode.Z); Key(session, "2");
            Key(session, "", HostKeyCode.Return);
            Check(session.keyboardPreview && !session.keyboardValid && session.history.UndoCount == 0 && Near(session.currentPosition, start),
                "ambiguous scalar Shift-plane refuses Enter in the actual route");
            Key(session, "", HostKeyCode.Escape);
        }

        private static void RelativeScaleLifecycle()
        {
            Vector3 basis = new Vector3(2, 3, 4);
            var session = Fresh(.5f, basis); SelectPoint(session);
            Vector3 pivot = session.ResolveTransformPivot();
            Key(session, "", HostKeyCode.S); Key(session, "", HostKeyCode.X); Key(session, "2");
            Check(session.keyboardValid && Near(session.currentScale, new Vector3(1, .5f, .5f)), "S factor modifies only its selected relative axis");
            Check(Near(session.ResolveTransformPivot(), pivot) && Near(session.selectedPiece.transform.localScale, basis), "S fixes pivot and leaves prefab base scale intact");
            Key(session, "", HostKeyCode.Return);
            Check(session.TryAddPlacementInstance(session.currentPosition, session.currentRotation) &&
                Near(session.placementPlan[0].Scale, new Vector3(1, .5f, .5f)) &&
                Near(Vector3.Scale(basis, session.placementPlan[0].Scale), new Vector3(2, 1.5f, 2)), "actual final-plan scale applies prefab basis exactly once");
            session.UndoTransform(); Check(Near(session.currentScale, Vector3.one * .5f), "S Undo restores scale");
            session.RedoTransform(); Check(Near(session.currentScale, new Vector3(1, .5f, .5f)), "S Redo restores XYZ scale");
            session.ResetTransform(); Check(Near(session.currentScale, Vector3.one * .5f), "Reset restores baseline scale");
            Key(session, "", HostKeyCode.S); Key(session, "2"); Key(session, "", HostKeyCode.Escape);
            Check(Near(session.currentScale, Vector3.one * .5f), "actual Escape restores scale preview");
            Key(session, "", HostKeyCode.S); Key(session, "", HostKeyCode.X); Key(session, "2");
            session.SelectAnchorConstraint(GizmoAxis.None);
            Check(session.keyboardPreview && session.dragAxis == GizmoAxis.None && session.keyboardNumber == "" &&
                Near(session.currentScale, Vector3.one * .5f), "HUD Free restarts the same transform without stale axis or numeric delta");
            Key(session, "", HostKeyCode.Escape);
            session.currentScale = Vector3.one * 2;
            session.selectedPiece.View.m_syncInitialScale = false;
            session.placementPlan.Clear(); session.placementPlanPieces.Clear();
            Check(!session.TryAddPlacementInstance(session.currentPosition, session.currentRotation) && session.placementPlan.Count == 0,
                "unsupported native scale persistence is refused before plan placement");
        }

        private static void ScaleBoundsAndAtomicRefusal()
        {
            foreach (var row in new[] { new { text="0.02", valid=true, result=.01f }, new { text="20", valid=true, result=10f },
                new { text="0.019", valid=false, result=.5f }, new { text="21", valid=false, result=.5f },
                new { text="0", valid=false, result=.5f }, new { text="-1", valid=false, result=.5f },
                new { text="1.", valid=false, result=.5f }, new { text="100000000000000000000000000000", valid=false, result=.5f } })
            {
                var session = Fresh(.5f);
                Key(session, "", HostKeyCode.S); Key(session, row.text); Key(session, "", HostKeyCode.Return);
                Check(session.keyboardPreview != row.valid && Near(session.currentScale, Vector3.one * row.result) &&
                    session.history.UndoCount == (row.valid ? 1 : 0), "relative result bounds, not factor bounds: " + row.text);
            }
            foreach (string text in new[] { "NaN", "Infinity", "-Infinity" })
                Check(!TransformKeyboardInput.TryNumber(GizmoHandleKind.Scale, GizmoAxis.None, GizmoAxis.None,
                    Quaternion.identity, false, Vector3.forward, text, out _, out _, out _), "nonfinite numeric evaluator refusal " + text);
            foreach (float value in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                var session = Fresh(); Key(session, "", HostKeyCode.S);
                TransformSnapshot before = session.CaptureHistorySnapshot();
                session.keyboardScale = value;
                Check(!session.TryPreviewKeyboardTransform() && !HistorySnapshotChanged(before, session.CaptureHistorySnapshot()) &&
                    session.history.UndoCount == 0 && !session.blueprintPartsDirty, "actual staging refuses invalid scale atomically " + value);
            }
            var overflow = Fresh(1, new Vector3(float.MaxValue, 1, 1));
            Key(overflow, "", HostKeyCode.S); Key(overflow, "2");
            Check(!overflow.keyboardValid && Near(overflow.currentScale, Vector3.one), "prefab base-scale overflow is atomic");
            var mouse = Fresh(.5f); Key(mouse, "", HostKeyCode.S);
            Vector3 pointer = Input.mousePosition;
            Input.Frame(pointer + Vector3.up * 5000, false, false); mouse.KeyboardFromActualUpdate(mouse.freeViewCamera);
            Check(Near(mouse.currentScale, Vector3.one * 10), "mouse S clamps the resulting upper scale bound");
            Input.Frame(pointer - Vector3.up * 5000, false, false); mouse.KeyboardFromActualUpdate(mouse.freeViewCamera);
            Check(Near(mouse.currentScale, Vector3.one * .01f), "mouse S clamps the resulting lower scale bound");
        }

        private static void GroupScaleAndHistory()
        {
            var session = Group();
            Vector3 rawPosition = session.activeBlueprint.parts[0].position.ToVector3();
            Key(session, "", HostKeyCode.S); Key(session, "2");
            Check(session.keyboardValid && Near(session.placementPlan[0].Scale, Vector3.one * 2) &&
                Near(session.placementPlan[1].Scale, Vector3.one * 2) && session.placementPlan[0].SignText == "Fixture sign 0" &&
                session.placementPlan[0].MaterialSeed == 7, "group preview stages every relative scale and preserves native metadata");
            Key(session, "", HostKeyCode.Return);
            Check(Near(session.activeBlueprint.parts[0].position.ToVector3(), rawPosition) &&
                Near(session.activeBlueprint.parts[0].scale.ToVector3(), Vector3.one) &&
                Near(session.activeBlueprint.customSnapPoints[0].ToVector3(), new Vector3(-.5f, 0, 0)), "group preview/commit do not mutate source blueprint records");
            Check(Near(session.BlueprintCustomPoints().First(), new Vector3(-1, 0, 0)) &&
                session.placementPlan[0].CustomSnapPoints[0].X == .25, "extra native point follows the same scaled part-local attachment");
            session.UndoTransform();
            Check(Near(session.placementPlan[0].Scale, Vector3.one) && !session.blueprintPartsDirty, "group Undo restores poses, scale and dirty state");
            session.RedoTransform();
            Check(Near(session.placementPlan[0].Scale, Vector3.one * 2), "group Redo restores final-plan XYZ scales");
            session.ResetTransform();
            Check(Near(session.placementPlan[0].Scale, Vector3.one), "group Reset restores baseline plan");
        }

        private static void GroupShearRefusal()
        {
            var session = Group(true);
            Key(session, "", HostKeyCode.S); Key(session, "", HostKeyCode.X);
            TransformSnapshot before = session.CaptureHistorySnapshot();
            Vector3[] anchors = session.anchorLocalPoints;
            Key(session, "2");
            Check(!session.keyboardValid && !HistorySnapshotChanged(before, session.CaptureHistorySnapshot()) &&
                ReferenceEquals(anchors, session.anchorLocalPoints) && !session.blueprintPartsDirty && session.history.UndoCount == 0,
                "second-part shear refuses group without publishing the first staged part");
            Key(session, "", HostKeyCode.Return);
            Check(session.keyboardPreview && session.history.UndoCount == 0, "invalid group Enter cannot commit stale valid preview");
            Key(session, "", HostKeyCode.Escape);
        }

        private static void InputOwnershipAndIdlePreview()
        {
            var session = Fresh(); session.state = PlacementState.Inactive;
            Key(session, "", HostKeyCode.S);
            Check(!session.keyboardPreview && session.state == PlacementState.Inactive, "F9 OFF does not start or confirm S");
            session = Fresh(); Input.Command("", HostKeyCode.S); Input.RightHeld = true;
            session.KeyboardFromActualUpdate(session.freeViewCamera);
            Check(!session.keyboardPreview && session.cameraBackwards == 1, "actual camera S key branch remains backward flight");
            session = Fresh(); Key(session, "", HostKeyCode.S); Key(session, "2");
            int updates = session.frameRebuilds;
            Key(session); Key(session);
            Check(session.frameRebuilds == updates && session.keyboardValid, "unchanged numeric reuses transform preview only");
            Key(session, "\b0"); Key(session, "", HostKeyCode.Return);
            Check(!session.keyboardValid && session.keyboardPreview && session.history.UndoCount == 0,
                "valid to invalid numeric blocks Enter despite the old valid pose");
            Key(session, "\b2");
            Check(session.keyboardValid && Near(session.currentScale, Vector3.one * 2) && session.keyboardError == null,
                "return to the same valid numeric restores validity and status");
            Key(session, "\b3"); Check(Near(session.currentScale, Vector3.one * 3), "changed numeric refreshes preview");
            session.gameplayAllowed = false; Key(session, "", HostKeyCode.Return);
            Check(session.keyboardPreview && session.keyboardPaused && session.history.UndoCount == 0, "chat/menu cannot confirm paused transform");
            session.gameplayAllowed = true; Key(session);
            updates = session.frameRebuilds; Key(session);
            Check(session.frameRebuilds == updates + 1 && session.keyboardValid, "camera/input rebase invalidates transform cache once");
            Input.Focused = false; Key(session);
            Check(!session.keyboardPreview && Near(session.currentScale, Vector3.one), "focus loss cancels preview without committing");
            Input.Focused = true;
            session = Fresh(); Key(session, "", HostKeyCode.S);
            Vector3 pointer = Input.mousePosition + Vector3.up * 20;
            Input.Frame(pointer, false, false); session.KeyboardFromActualUpdate(session.freeViewCamera);
            updates = session.frameRebuilds;
            Input.Frame(pointer, false, false); session.KeyboardFromActualUpdate(session.freeViewCamera);
            Check(session.frameRebuilds == updates, "unchanged moved mouse no longer stages the whole group every frame");
            Input.Frame(pointer + Vector3.up * 10, false, false); session.KeyboardFromActualUpdate(session.freeViewCamera);
            Check(session.frameRebuilds == updates + 1, "changed mouse invalidates preview");
        }

        private static void FrameSwitch(bool composite, int oldCount, int pin, int newCount, bool succeeds = true)
        {
            var session = new PrecisionPlacementSession {
                blueprintCompositeFrame = composite,
                anchorLocalPoints = new Vector3[oldCount], anchorWorldPoints = new Vector3[oldCount],
                recapturedAnchors = new Vector3[newCount], recaptureSucceeds = succeeds
            };
            var camera = new Camera();
            session.gizmo.Anchor = pin;
            Input.Frame(session.currentPosition, true, true, true);
            session.HandleGizmoInput(camera);
            Check(session.pinnedAnchorPoint == pin, "frame control: actual Shift-click selected old anchor");
            session.activeBlueprint = new CompositeBlueprintStore.Blueprint();
            session.sessionMode = SessionMode.BlueprintWorldPlacement;
            session.dragAxis = GizmoAxis.X;
            session.dragHandle = GizmoHandleKind.Rotate;
            session.dragAnchorPoint = pin;
            session.anchorConstraintAxis = GizmoAxis.Y;
            session.dragConstraintActive = session.alignmentTargetVisible = true;
            session.dragMagneticMove = session.dragSourceIsNative = true;
            session.snapTargetVisible = session.snapTargetIsNative = true;
            session.snapTargets.Add(1);
            Input.Frame(Vector3.zero, false, false);
            if (!composite) Input.Keys.Add(BuildWorksPlugin.BlueprintCompositeFrameKey.Value);
            session.UpdateFrameFromActualCaller();
            Check(session.recaptureCalls == 0 && session.pinnedAnchorPoint == pin,
                "actual caller guard must defer frame switch during active drag");
            session.dragAxis = GizmoAxis.None;
            session.dragAnchorPoint = -1;
            Vector3[] oldAnchors = session.anchorLocalPoints;
            session.UpdateFrameFromActualCaller();
            string name = "frame " + composite + "->" + !composite + ", pin=" + pin + ", captured=" + succeeds;
            Check(session.blueprintCompositeFrame == (succeeds ? !composite : composite) &&
                session.frameRebuilds == (succeeds ? 1 : 0) && session.recaptureCalls == 1,
                name + ": real frame transition did not execute");
            Check(ReferenceEquals(session.anchorLocalPoints, succeeds ? session.recapturedAnchors : oldAnchors),
                name + ": applied frame and anchor set diverged");
            Check(session.pinnedAnchorPoint == -1 && session.selectedAnchorPoint == -1 &&
                session.anchorConstraintAxis == GizmoAxis.None,
                name + ": stale pin/constraint axis");
            Check(session.dragAxis == GizmoAxis.None && session.dragHandle == GizmoHandleKind.None &&
                session.dragAnchorPoint == -1 && !session.dragConstraintActive && !session.alignmentTargetVisible,
                name + ": stale drag/constraint state");
            Check(!session.dragMagneticMove && !session.dragSourceIsNative && !session.snapTargetVisible &&
                !session.snapTargetIsNative && session.snapTargets.Count == 0,
                name + ": stale magnetic snap state");
            Check(Vector3.Distance(session.ResolveTransformPivot(), session.currentPosition) < 0.0001f,
                name + ": pivot did not return to root");
            if (!succeeds)
            {
                session.recaptureSucceeds = true;
                session.UpdateFrameFromActualCaller();
                Check(session.recaptureCalls == 2 && session.frameRebuilds == 1 &&
                    session.blueprintCompositeFrame == !composite &&
                    ReferenceEquals(session.anchorLocalPoints, session.recapturedAnchors),
                    "failed recapture must retry while the same frame key state remains held");
            }
        }

        private static void UnchangedFrame()
        {
            var session = new PrecisionPlacementSession { activeBlueprint = new CompositeBlueprintStore.Blueprint(), pinnedAnchorPoint = 0, selectedAnchorPoint = 0,
                sessionMode = SessionMode.BlueprintWorldPlacement };
            Input.Frame(Vector3.zero, false, false);
            session.UpdateFrameFromActualCaller();
            Check(session.pinnedAnchorPoint == 0 && session.selectedAnchorPoint == 0 &&
                session.frameRebuilds == 0 && session.recaptureCalls == 0,
                "unchanged frame must preserve selected pin");
        }

        private static void NormalChoice()
        {
            var session = new PrecisionPlacementSession {
                anchorLocalPoints = new[] { Vector3.right * 2, Vector3.up * 3 }, anchorWorldPoints = new Vector3[2]
            };
            var camera = new Camera();
            Vector3 position = session.currentPosition;
            Quaternion rotation = session.currentRotation;
            session.gizmo.Anchor = 0;
            Input.Frame(session.UpdateAnchorWorldPoints()[0], true, true);
            session.HandleGizmoInput(camera);
            Check(session.selectedAnchorPoint == 0 && session.pinnedAnchorPoint == -1 && session.anchorDragCalls == 1,
                "normal click must select without pinning and retain the anchor-drag entry point");
            Input.Frame(Vector3.zero, false, false, up: true);
            session.HandleGizmoInput(camera);
            Check(session.selectedAnchorPoint == 0 && session.dragAnchorPoint < 0,
                "normal-click pivot must persist after actual mouse-up handler");
            session.pinnedAnchorPoint = 0;
            session.gizmo.Anchor = 1;
            Input.Frame(session.UpdateAnchorWorldPoints()[1], true, true);
            session.HandleGizmoInput(camera);
            Check(session.selectedAnchorPoint == 1 && session.pinnedAnchorPoint == 0 &&
                Vector3.Distance(session.ResolveTransformPivot(), session.UpdateAnchorWorldPoints()[1]) < 0.0001f,
                "new normal-click pivot must be distinct from the existing fixed pin");
            Check(Vector3.Distance(position, session.currentPosition) < 0.0001f &&
                Vector3.Distance(rotation * Vector3.forward, session.currentRotation * Vector3.forward) < 0.0001f,
                "selection state must not change the pose at the anchor-drag boundary");
        }
    }
}
internal static class Program
{
    private static int Main()
    {
        try { return OstrixMods.BuildWorks.PrecisionPlacementSession.Run(); }
        catch (Exception error) { Console.WriteLine("HOST_ERROR " + error); return 2; }
    }
}
