using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class PrecisionPlacementSession
    {
        private readonly IBlueprintEditorInput transformInput = UnityBlueprintEditorInput.Instance;
        private bool keyboardPreview, keyboardPaused, keyboardMoved, keyboardValid, keyboardSavedLocalSpace;
        private string keyboardNumber = "", keyboardError;
        private GizmoAxis keyboardSecondAxis;
        private Quaternion keyboardOrientation, keyboardRotation = Quaternion.identity, keyboardBaseRotation = Quaternion.identity;
        private Vector3 keyboardViewAxis, keyboardTranslation, keyboardBaseTranslation, keyboardRootStart, keyboardRootPreview;
        private Quaternion keyboardRootRotationStart, keyboardRootRotationPreview;
        private float keyboardScale = 1f, keyboardBaseScale = 1f, keyboardValidScale = 1f;
        private TransformKeyboardInput.Projection keyboardProjection;
        private BlueprintEditorPart[] keyboardParts;
        private int[] keyboardPartIndices;
        private TransformSnapshot[] keyboardOriginalPlan, keyboardPreviewPlan, keyboardStagingPlan;
        private Vector3[] keyboardSourceAnchors, keyboardPreviewAnchors, keyboardStagingAnchors;
        private bool keyboardAttemptCached, keyboardAttemptValid;
        private Vector3 keyboardAttemptTranslation, keyboardAttemptScale;
        private Quaternion keyboardAttemptRotation, keyboardAttemptFrame;

        private bool IsBlueprintTransform => activeBlueprint != null && blueprintWorkspaceEditPart == null &&
            (sessionMode != SessionMode.BlueprintEditor || activeBlueprintPieces.Count == activeBlueprint.parts.Count);

        private bool HandleKeyboardTransform(Camera camera)
        {
            if (!camera || state != PlacementState.Editing || sessionMode == SessionMode.ProjectPart) return false;
            bool control = transformInput.GetKey(KeyCode.LeftControl) || transformInput.GetKey(KeyCode.RightControl);
            bool alt = transformInput.GetKey(KeyCode.LeftAlt) || transformInput.GetKey(KeyCode.RightAlt);
            bool shift = transformInput.GetKey(KeyCode.LeftShift) || transformInput.GetKey(KeyCode.RightShift);
            if (!transformInput.HasFocus)
            {
                if (keyboardPreview) { CancelKeyboardTransform(); return true; }
                return false;
            }
            if (freeViewFlyLooking || transformInput.GetMouseButton(1) || transformInput.GetMouseButton(2) ||
                MouseOverEditorPanel(transformInput.MousePosition))
            {
                if (keyboardPreview) keyboardPaused = true;
                return keyboardPreview;
            }
            if (keyboardPreview && (transformInput.MouseScrollDelta.sqrMagnitude > 0 || transformInput.GetKeyDown(KeyCode.F)))
            { keyboardPaused = true; return true; }
            bool move = transformInput.GetKeyDown(KeyCode.G), rotate = transformInput.GetKeyDown(KeyCode.R), scale = transformInput.GetKeyDown(KeyCode.S);
            if (!keyboardPreview)
            {
                if (control || alt || shift || dragAxis != GizmoAxis.None || dragAnchorPoint >= 0 || !move && !rotate && !scale) return false;
                BeginKeyboardTransform(camera, rotate ? GizmoHandleKind.Rotate : scale ? GizmoHandleKind.Scale : GizmoHandleKind.MovePlane);
                return true;
            }
            if (transformInput.GetKeyDown(KeyCode.Escape) ||
                control && (transformInput.GetKeyDown(KeyCode.Z) || transformInput.GetKeyDown(KeyCode.Y)))
            { CancelKeyboardTransform(); return true; }
            if (control || alt) { keyboardPaused = true; return true; }
            if (!shift && (move || rotate || scale))
            {
                CancelKeyboardTransform();
                BeginKeyboardTransform(camera, rotate ? GizmoHandleKind.Rotate : scale ? GizmoHandleKind.Scale : GizmoHandleKind.MovePlane);
                return true;
            }
            if (keyboardPaused)
            { RebaseKeyboardTransform(camera); keyboardPaused = false; return true; }
            string previous = keyboardNumber;
            keyboardNumber = TransformKeyboardInput.ReadNumber(keyboardNumber, transformInput.InputString, transformInput.GetKeyDown(KeyCode.Delete));
            if (previous != keyboardNumber) keyboardAttemptCached = false;
            if (previous.Length > 0 && keyboardNumber.Length == 0) RebaseKeyboardTransform(camera);
            GizmoAxis axis = transformInput.GetKeyDown(KeyCode.X) ? GizmoAxis.X :
                transformInput.GetKeyDown(KeyCode.Y) ? GizmoAxis.Y : transformInput.GetKeyDown(KeyCode.Z) ? GizmoAxis.Z : GizmoAxis.None;
            if (axis != GizmoAxis.None) SetKeyboardTransformAxis(camera, axis);
            PreviewKeyboardTransform(camera, shift);
            if (transformInput.GetKeyDown(KeyCode.Return) || transformInput.GetKeyDown(KeyCode.KeypadEnter) || transformInput.GetMouseButtonDown(0))
                ConfirmKeyboardTransform();
            return true;
        }

        private void BeginKeyboardTransform(Camera camera, GizmoHandleKind handle)
        {
            if (!selectedPiece) return;
            ClearLayout();
            gizmoMode = GizmoMode.Move;
            if (IsBlueprintTransform)
            {
                RebuildBlueprintPlan();
                if (placementPlan.Count != activeBlueprint.parts.Count)
                { ShowStatus(Player.m_localPlayer, repeatPlanError ?? BuildWorksLocalization.Text("layout.scale_bounds")); return; }
                keyboardOriginalPlan = placementPlan.ToArray();
            }
            else keyboardOriginalPlan = new[] { new TransformSnapshot(currentPosition, currentRotation, currentScale) };
            dragStartSnapshot = CaptureHistorySnapshot();
            dragStartPosition = currentPosition;
            dragStartRotation = currentRotation;
            dragStartScale = currentScale;
            dragTransformPivot = ResolveTransformPivot();
            keyboardOrientation = currentRotation;
            keyboardSavedLocalSpace = localSpace;
            keyboardViewAxis = camera.transform.forward;
            keyboardRootStart = IsBlueprintTransform ? blueprintRootPosition : currentPosition;
            keyboardRootRotationStart = IsBlueprintTransform ? blueprintRootRotation : currentRotation;
            keyboardRootPreview = keyboardRootStart;
            keyboardRootRotationPreview = keyboardRootRotationStart;
            keyboardPartIndices = IsBlueprintTransform && blueprintEditPartIndex < 0
                ? Enumerable.Range(0, keyboardOriginalPlan.Length).ToArray() : new[] { IsBlueprintTransform ? blueprintEditPartIndex : 0 };
            try
            {
                keyboardParts = new BlueprintEditorPart[keyboardPartIndices.Length];
                for (int index = 0; index < keyboardParts.Length; ++index)
                {
                    int sourceIndex = keyboardPartIndices[index];
                    Piece piece = IsBlueprintTransform ? activeBlueprintPieces[sourceIndex] : selectedPiece;
                    CompositeBlueprintStore.Part part = IsBlueprintTransform ? activeBlueprint.parts[sourceIndex] : null;
                    TransformSnapshot pose = keyboardOriginalPlan[sourceIndex];
                    keyboardParts[index] = new BlueprintEditorPart(
                        part != null ? part.stableId : piece.GetInstanceID().ToString(System.Globalization.CultureInfo.InvariantCulture),
                        part?.prefabName ?? piece.name, part?.displayName ?? piece.name,
                        ToGeometry(pose.Position), ToGeometry(pose.Rotation), scale: ToGeometry(pose.Scale),
                        baseScale: ToGeometry(piece.transform.localScale));
                }
            }
            catch (ArgumentException)
            { ClearKeyboardTransform(); ShowStatus(Player.m_localPlayer, BuildWorksLocalization.Text("editor.hint.numeric_invalid")); return; }
            keyboardPreviewPlan = (TransformSnapshot[])keyboardOriginalPlan.Clone();
            keyboardStagingPlan = new TransformSnapshot[keyboardOriginalPlan.Length];
            keyboardSourceAnchors = (Vector3[])UpdateAnchorWorldPoints().Clone();
            keyboardPreviewAnchors = new Vector3[keyboardSourceAnchors.Length];
            keyboardStagingAnchors = new Vector3[keyboardSourceAnchors.Length];
            keyboardNumber = "";
            keyboardError = null;
            keyboardTranslation = Vector3.zero;
            keyboardRotation = Quaternion.identity;
            keyboardScale = keyboardValidScale = 1f;
            dragHandle = handle;
            dragAxis = keyboardSecondAxis = GizmoAxis.None;
            keyboardPreview = keyboardValid = true;
            keyboardPaused = false;
            keyboardAttemptCached = false;
            ClearSnapDrag();
            RebaseKeyboardTransform(camera);
            suppressPlacementFrame = Time.frameCount;
        }

        private void SetKeyboardTransformAxis(Camera camera, GizmoAxis axis)
        {
            keyboardAttemptCached = false;
            TransformKeyboardInput.SelectAxis(ref dragHandle, ref dragAxis, ref keyboardSecondAxis, ref localSpace, axis,
                transformInput.GetKey(KeyCode.LeftShift) || transformInput.GetKey(KeyCode.RightShift));
            keyboardTranslation = Vector3.zero;
            keyboardRotation = Quaternion.identity;
            keyboardScale = keyboardValidScale = 1f;
            TryPreviewKeyboardTransform();
            RebaseKeyboardTransform(camera);
        }

        private void RebaseKeyboardTransform(Camera camera)
        {
            keyboardAttemptCached = false;
            keyboardBaseTranslation = keyboardTranslation;
            keyboardBaseRotation = keyboardRotation;
            keyboardBaseScale = keyboardScale;
            keyboardMoved = false;
            dragStartMouse = transformInput.MousePosition;
            keyboardProjection = new TransformKeyboardInput.Projection(camera, dragStartMouse,
                dragTransformPivot + keyboardTranslation, keyboardRotation * keyboardOrientation,
                dragHandle, dragAxis, keyboardSecondAxis, localSpace);
            keyboardValid = dragHandle == GizmoHandleKind.Scale ||
                (dragHandle == GizmoHandleKind.Rotate ? keyboardProjection.RotationValid : keyboardProjection.PlaneValid);
        }

        private void PreviewKeyboardTransform(Camera camera, bool precise)
        {
            Vector3 previousTranslation = keyboardTranslation;
            Quaternion previousRotation = keyboardRotation;
            float previousScale = keyboardScale;
            if (keyboardNumber.Length > 0)
            {
                keyboardValid = TransformKeyboardInput.TryNumber(dragHandle, dragAxis, keyboardSecondAxis,
                    keyboardOrientation, localSpace, keyboardViewAxis, keyboardNumber,
                    out Vector3 translation, out Quaternion rotation, out float scale);
                if (!keyboardValid) { keyboardError = "editor.hint.numeric_invalid"; return; }
                keyboardTranslation = translation;
                keyboardRotation = rotation;
                keyboardScale = scale;
                if (!TryPreviewKeyboardTransform())
                {
                    keyboardTranslation = previousTranslation;
                    keyboardRotation = previousRotation;
                    keyboardScale = previousScale;
                }
                return;
            }
            Vector2 mouse = transformInput.MousePosition;
            if (!keyboardMoved && (mouse - dragStartMouse).sqrMagnitude < 1f) return;
            keyboardMoved = true;
            keyboardValid = true;
            if (dragHandle == GizmoHandleKind.Move)
            {
                keyboardValid = keyboardProjection.ScreenDirection.sqrMagnitude > .01f;
                keyboardTranslation = keyboardBaseTranslation + keyboardProjection.Axis * TransformKeyboardInput.AxisTranslation(
                    mouse, dragStartMouse, keyboardProjection.ScreenDirection, keyboardProjection.UnitsPerPixel,
                    (float)PrecisionStepPresets.Translation[translationStepIndex], precise);
            }
            else if (dragHandle == GizmoHandleKind.MovePlane)
            {
                Ray ray = camera.ScreenPointToRay(mouse);
                keyboardValid = Mathf.Abs(Vector3.Dot(ray.direction, keyboardProjection.Axis)) > .001f &&
                    keyboardProjection.Plane.Raycast(ray, out _);
                if (!keyboardValid) return;
                keyboardProjection.Plane.Raycast(ray, out float distance);
                Vector3 delta = ray.GetPoint(distance) - keyboardProjection.PlaneStart;
                float first = Vector3.Dot(delta, keyboardProjection.PlaneFirst), second = Vector3.Dot(delta, keyboardProjection.PlaneSecond);
                if (!precise)
                {
                    float step = (float)PrecisionStepPresets.Translation[translationStepIndex];
                    first = Mathf.Round(first / step) * step;
                    second = Mathf.Round(second / step) * step;
                }
                keyboardTranslation = keyboardBaseTranslation + keyboardProjection.PlaneFirst * first + keyboardProjection.PlaneSecond * second;
            }
            else if (dragHandle == GizmoHandleKind.Rotate)
            {
                keyboardValid = TransformKeyboardInput.TryRotationDirection(camera, mouse,
                    dragTransformPivot + keyboardBaseTranslation, keyboardProjection.Axis, out Vector3 direction);
                if (!keyboardValid) return;
                if (!keyboardProjection.RotationValid) { RebaseKeyboardTransform(camera); return; }
                float degrees = Vector3.SignedAngle(keyboardProjection.RotationDirection, direction, keyboardProjection.Axis);
                if (!precise)
                {
                    float step = (float)PrecisionStepPresets.Rotation[rotationStepIndex];
                    degrees = Mathf.Round(degrees / step) * step;
                }
                keyboardRotation = Quaternion.AngleAxis(degrees, keyboardProjection.Axis) * keyboardBaseRotation;
            }
            else if (dragHandle == GizmoHandleKind.Scale)
            {
                float requested = TransformKeyboardInput.MouseScale(mouse, dragStartMouse, keyboardBaseScale, 10f, precise);
                keyboardScale = TransformKeyboardInput.ClampMouseScale(requested, keyboardParts,
                    dragAxis == GizmoAxis.None ? Vector3.one : TransformKeyboardInput.AxisMask(dragAxis, keyboardSecondAxis),
                    TransformKeyboardInput.ScaleFrame(dragHandle, dragAxis, localSpace, keyboardOrientation));
            }
            if (!keyboardValid || !TryPreviewKeyboardTransform())
            {
                keyboardTranslation = previousTranslation;
                keyboardRotation = previousRotation;
                keyboardScale = previousScale;
            }
        }

        private bool TryPreviewKeyboardTransform()
        {
            try
            {
                Vector3 scale = TransformKeyboardInput.ScaleVector(dragHandle, dragAxis, keyboardSecondAxis, keyboardScale);
                Quaternion frame = TransformKeyboardInput.ScaleFrame(dragHandle, dragAxis, localSpace, keyboardOrientation);
                if (keyboardAttemptCached && keyboardAttemptTranslation.Equals(keyboardTranslation) &&
                    keyboardAttemptRotation.Equals(keyboardRotation) && keyboardAttemptScale.Equals(scale) && keyboardAttemptFrame.Equals(frame))
                {
                    keyboardValid = keyboardAttemptValid;
                    if (keyboardValid) keyboardError = null;
                    else keyboardScale = keyboardValidScale;
                    return keyboardValid;
                }
                keyboardAttemptCached = true;
                keyboardAttemptValid = false;
                keyboardAttemptTranslation = keyboardTranslation;
                keyboardAttemptRotation = keyboardRotation;
                keyboardAttemptScale = scale;
                keyboardAttemptFrame = frame;
                Array.Copy(keyboardOriginalPlan, keyboardStagingPlan, keyboardOriginalPlan.Length);
                for (int index = 0; index < keyboardParts.Length; ++index)
                {
                    var pose = BlueprintEditorDocument.TransformPart(keyboardParts[index], ToGeometry(keyboardTranslation),
                        ToGeometry(keyboardRotation), ToGeometry(dragTransformPivot), ToGeometry(scale), ToGeometry(frame));
                    Vector3 position = ToUnity(pose.Position), relativeScale = ToUnity(pose.Scale);
                    if (!IsFinite(position) || !IsFinite(relativeScale)) throw new ArgumentOutOfRangeException(nameof(pose));
                    TransformSnapshot original = keyboardOriginalPlan[keyboardPartIndices[index]];
                    keyboardStagingPlan[keyboardPartIndices[index]] = new TransformSnapshot(position, ToUnityRotation(pose.Rotation),
                        relativeScale, original.SignText, original.ItemStandContents, original.CustomSnapPoints, original.MaterialSeed);
                }
                bool group = IsBlueprintTransform && blueprintEditPartIndex < 0;
                TransformSnapshot primary = keyboardStagingPlan[keyboardPartIndices[0]];
                Vector3 center = group ? TransformKeyboardInput.TransformPoint(dragStartPosition, dragTransformPivot,
                    keyboardTranslation, keyboardRotation, scale, frame) : primary.Position;
                Quaternion rotation = group ? keyboardRotation * dragStartRotation : primary.Rotation;
                Vector3 relative = group ? Vector3.one : primary.Scale;
                Vector3 root = IsBlueprintTransform && !group || dragHandle == GizmoHandleKind.Scale ? keyboardRootStart :
                    TransformKeyboardInput.TransformPoint(keyboardRootStart, dragTransformPivot, keyboardTranslation, keyboardRotation, scale, frame);
                Quaternion rootRotation = IsBlueprintTransform && !group ? keyboardRootRotationStart : keyboardRotation * keyboardRootRotationStart;
                Vector3 inverseScale = new Vector3(1f / relative.x, 1f / relative.y, 1f / relative.z);
                if (!IsFinite(center) || !IsFinite(root)) throw new ArgumentOutOfRangeException(nameof(center));
                for (int index = 0; index < keyboardSourceAnchors.Length; ++index)
                {
                    Vector3 point = TransformKeyboardInput.TransformPoint(keyboardSourceAnchors[index], dragTransformPivot,
                        keyboardTranslation, keyboardRotation, scale, frame);
                    keyboardStagingAnchors[index] = Vector3.Scale(Quaternion.Inverse(rotation) * (point - center), inverseScale);
                    if (!IsFinite(keyboardStagingAnchors[index])) throw new ArgumentOutOfRangeException(nameof(point));
                }
                Vector3 pivotLocal = group ? Quaternion.Inverse(rootRotation) * (center - root) : blueprintPivotLocal;
                if (!IsFinite(pivotLocal)) throw new ArgumentOutOfRangeException(nameof(pivotLocal));
                var previousPlan = keyboardPreviewPlan;
                keyboardPreviewPlan = keyboardStagingPlan;
                keyboardStagingPlan = previousPlan;
                var previousAnchors = keyboardPreviewAnchors;
                keyboardPreviewAnchors = keyboardStagingAnchors;
                keyboardStagingAnchors = previousAnchors;
                anchorLocalPoints = keyboardPreviewAnchors;
                MovePieceWithoutMovingCameraFocus(center);
                currentRotation = rotation;
                currentScale = relative;
                if (group) blueprintPivotLocal = pivotLocal;
                keyboardRootPreview = root;
                keyboardRootRotationPreview = rootRotation;
                keyboardValidScale = keyboardScale;
                keyboardError = null;
                keyboardValid = true;
                if (IsBlueprintTransform) RebuildBlueprintPlan();
                else RebuildActiveLayout();
                ApplyEditingGhostTransform();
                keyboardAttemptValid = true;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException || exception is InvalidOperationException)
            {
                keyboardValid = false;
                keyboardAttemptValid = false;
                keyboardScale = keyboardValidScale;
                string error = exception.Message == BlueprintEditorDocument.NonUniformScaleShearError
                    ? "editor.scale.shear" : "editor.hint.numeric_invalid";
                if (keyboardError != error) ShowStatus(Player.m_localPlayer, BuildWorksLocalization.Text(error));
                keyboardError = error;
                return false;
            }
        }

        private void ConfirmKeyboardTransform()
        {
            suppressPlacementFrame = Time.frameCount;
            if (!keyboardValid) return;
            if (IsBlueprintTransform)
            {
                var local = new TransformSnapshot[keyboardPreviewPlan.Length];
                Quaternion inverse = Quaternion.Inverse(keyboardRootRotationPreview);
                bool changed = false;
                for (int index = 0; index < local.Length; ++index)
                {
                    TransformSnapshot pose = keyboardPreviewPlan[index];
                    Vector3 position = inverse * (pose.Position - keyboardRootPreview);
                    if (!IsFinite(position)) { keyboardValid = false; return; }
                    local[index] = new TransformSnapshot(position, inverse * pose.Rotation, pose.Scale);
                    changed |= HistorySnapshotChanged(BlueprintPartTransform(index), local[index]);
                }
                if (changed) blueprintPartTransforms = local;
                blueprintRootPosition = keyboardRootPreview;
                blueprintRootRotation = keyboardRootRotationPreview;
                blueprintPartsDirty |= changed;
            }
            keyboardPreview = false;
            CommitTransformIfChanged(dragStartPosition, dragStartRotation, dragStartSnapshot);
            ClearKeyboardTransform();
            RebuildActiveLayout();
        }

        private void CancelKeyboardTransform()
        {
            ApplyHistorySnapshot(dragStartSnapshot);
            localSpace = keyboardSavedLocalSpace;
            ClearKeyboardTransform();
            RebuildActiveLayout();
            ApplyEditingGhostTransform();
            suppressPlacementFrame = Time.frameCount;
        }

        private void ClearKeyboardTransform()
        {
            gizmo.Family = GizmoFamily.Combined;
            keyboardPreview = keyboardPaused = keyboardMoved = false;
            keyboardAttemptCached = false;
            keyboardNumber = "";
            keyboardError = null;
            keyboardParts = null;
            keyboardPartIndices = null;
            keyboardOriginalPlan = keyboardPreviewPlan = keyboardStagingPlan = null;
            keyboardSourceAnchors = keyboardPreviewAnchors = keyboardStagingAnchors = null;
            dragAxis = keyboardSecondAxis = GizmoAxis.None;
            dragHandle = GizmoHandleKind.None;
            dragAnchorPoint = -1;
            ClearSnapDrag();
        }

        private static Quaternion ToUnityRotation(Rotation3 rotation) =>
            new Quaternion((float)rotation.X, (float)rotation.Y, (float)rotation.Z, (float)rotation.W);
    }
}
