using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal enum GizmoMode
    {
        Move,
        Repeat,
        Plane,
        Guide
    }

    internal enum RepeatDistributionMode
    {
        Pack,
        Fit,
        Exact
    }

    internal enum GizmoAxis
    {
        None,
        X,
        Y,
        Z
    }

    internal enum GizmoHandleKind
    {
        None,
        Move,
        Rotate,
        Layout,
        MovePlane,
        Scale
    }

    internal sealed class TransformGizmoView : IDisposable
    {
        private const int RingSegments = 48;
        private static readonly RaycastHit[] VisibilityHits = new RaycastHit[32];
        private static readonly Color[] AxisColors =
        {
            new Color(1f, 0.25f, 0.25f, 1f),
            new Color(0.35f, 1f, 0.35f, 1f),
            new Color(0.3f, 0.65f, 1f, 1f)
        };

        private readonly GameObject root;
        private readonly Material material;
        private readonly Material anchorMaterial;
        private readonly LineRenderer[] moveLines = new LineRenderer[3];
        private readonly LineRenderer[] movePlanes = new LineRenderer[3];
        private readonly LineRenderer scaleHandle;
        private readonly LineRenderer[] rotationRings = new LineRenderer[3];
        private readonly LineRenderer[] layoutAxisLines = new LineRenderer[3];
        private readonly LineRenderer[] alignmentAxisLines = new LineRenderer[3];
        private readonly List<LineRenderer> anchorHandles = new List<LineRenderer>();
        private readonly List<bool> visibleAnchors = new List<bool>();
        private readonly bool screenSpaceSizing;
        private float anchorHandleScale = 1f;
        private int nativeAnchorStartIndex = int.MaxValue;
        private float moveSize = 1f;
        private float rotationSize = 1f;
        private float arraySize = 1f;
        private float pointSize = 1f;
        private float uniformScaleHandleSize = 1f;
        private readonly List<LineRenderer> snapCandidateHandles = new List<LineRenderer>();
        private readonly LineRenderer snapTargetHandle;
        private readonly LineRenderer constraintPath;
        private readonly LineRenderer contactGuide;
        private readonly LineRenderer aimGuide;
        private readonly List<LineRenderer> layoutHandles = new List<LineRenderer>();

        public TransformGizmoView(bool screenSpaceSizing = false)
        {
            this.screenSpaceSizing = screenSpaceSizing;
            Shader shader = Shader.Find("Hidden/Internal-Colored") ??
                Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            if (!shader)
            {
                throw new InvalidOperationException("No compatible gizmo shader is available.");
            }

            root = new GameObject("BuildWorks_TransformGizmo")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = 2
            };
            material = new Material(shader)
            {
                name = "BuildWorks_TransformGizmoMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)CompareFunction.Always);
            material.renderQueue = (int)RenderQueue.Overlay;
            anchorMaterial = new Material(shader)
            {
                name = "BuildWorks_AnchorMaterial",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (anchorMaterial.HasProperty("_ZWrite")) anchorMaterial.SetInt("_ZWrite", 0);
            if (anchorMaterial.HasProperty("_ZTest"))
                anchorMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            anchorMaterial.renderQueue = (int)RenderQueue.Transparent;

            for (int i = 0; i < 3; ++i)
            {
                moveLines[i] = CreateLine("Move" + ((GizmoAxis)(i + 1)), 5);
                movePlanes[i] = CreateLine("MovePlane" + ((GizmoAxis)(i + 1)), 5);
                rotationRings[i] = CreateLine("Rotate" + ((GizmoAxis)(i + 1)), RingSegments + 1);
                rotationRings[i].loop = false;
                layoutAxisLines[i] = CreateLine("LayoutAxis" + ((GizmoAxis)(i + 1)), 5);
                alignmentAxisLines[i] = CreateLine(
                    "AlignmentAxis" + ((GizmoAxis)(i + 1)), 5, depthTested: true);
            }
            for (int i = 0; i < AnchorAdjustment.SelectableAnchorCount; ++i)
            {
                anchorHandles.Add(CreateLine("Anchor" + i, 5, depthTested: true));
                visibleAnchors.Add(false);
            }
            snapTargetHandle = CreateLine("SnapTarget", 5, depthTested: true);
            scaleHandle = CreateLine("UniformScale", 5);
            constraintPath = CreateLine("AnchorConstraintPath", RingSegments + 1);
            contactGuide = CreateLine("ContactGuide", 2);
            aimGuide = CreateLine("AimGuide", 2);
            root.SetActive(false);
        }

        public float Scale { get; private set; }

        public void SetLayer(int layer)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                child.gameObject.layer = layer;
        }

        internal void SetEditorSizes(float move, float rotation, float array, float points, float uniformScale)
        {
            if (!screenSpaceSizing) return;
            moveSize = Mathf.Clamp(move, 0.5f, 2.5f);
            rotationSize = Mathf.Clamp(rotation, 0.5f, 2.5f);
            arraySize = Mathf.Clamp(array, 0.5f, 2.5f);
            pointSize = Mathf.Clamp(points, 0.5f, 2.5f);
            uniformScaleHandleSize = Mathf.Clamp(uniformScale, 0.5f, 2.5f);
        }

        public void Show(
            Camera camera,
            Vector3 pivot,
            Quaternion rotation,
            bool localSpace,
            GizmoMode mode,
            GizmoHandleKind selectedHandle,
            GizmoAxis selectedAxis,
            Vector3[] anchorPoints,
            int selectedAnchor,
            int pinnedAnchor,
            int nativeAnchorStart,
            bool showAllAnchors,
            float handleScale,
            bool showSnapTarget,
            Vector3 snapTarget,
            bool snapTargetIsNative,
            GizmoAxis constraintAxis,
            bool showConstraintPath,
            Vector3 constraintCenter,
            Vector3 constraintAxisWorld,
            float constraintRadius,
            bool allowMove = true,
            bool allowRotate = true,
            bool allowExtended = false,
            Func<Vector3, bool> pointVisibility = null)
        {
            if (!camera)
            {
                Hide();
                return;
            }

            Scale = GizmoScale(camera, pivot);
            handleScale *= pointSize;
            anchorHandleScale = handleScale;
            nativeAnchorStartIndex = nativeAnchorStart;
            root.SetActive(true);
            foreach (LineRenderer line in alignmentAxisLines)
                line.gameObject.SetActive(false);
            bool showMove = allowMove && mode != GizmoMode.Guide && mode != GizmoMode.Plane;
            bool showRotation = allowRotate && mode != GizmoMode.Plane;
            bool showLayoutAxes = mode == GizmoMode.Repeat || mode == GizmoMode.Plane;
            for (int i = 0; i < 3; ++i)
            {
                GizmoAxis axis = (GizmoAxis)(i + 1);
                Color moveColor = selectedHandle == GizmoHandleKind.Move && axis == selectedAxis
                    ? new Color(1f, 0.9f, 0.2f, 1f)
                    : new Color(AxisColors[i].r, AxisColors[i].g, AxisColors[i].b, 0.58f);
                Color rotateColor = selectedHandle == GizmoHandleKind.Rotate && axis == selectedAxis
                    ? new Color(1f, 0.9f, 0.2f, 1f)
                    : new Color(AxisColors[i].r, AxisColors[i].g, AxisColors[i].b, 0.48f);
                Color layoutColor = selectedHandle == GizmoHandleKind.Layout && axis == selectedAxis
                    ? new Color(1f, 0.95f, 0.35f, 1f)
                    : new Color(1f, 0.72f, 0.12f, 0.9f);
                moveLines[i].gameObject.SetActive(showMove);
                movePlanes[i].gameObject.SetActive(showMove && allowExtended);
                if (showMove && allowExtended)
                {
                    PlaneBasis(axis, rotation, localSpace, out Vector3 first, out Vector3 second);
                    float low = Scale * moveSize * 0.18f, high = Scale * moveSize * 0.35f;
                    movePlanes[i].SetPositions(new[] {
                        pivot + first * low + second * low, pivot + first * high + second * low,
                        pivot + first * high + second * high, pivot + first * low + second * high,
                        pivot + first * low + second * low });
                    SetColorAndWidth(movePlanes[i], selectedHandle == GizmoHandleKind.MovePlane && axis == selectedAxis
                        ? Color.yellow : AxisColors[i]);
                }
                rotationRings[i].gameObject.SetActive(showRotation);
                layoutAxisLines[i].gameObject.SetActive(showLayoutAxes);
                if (showMove)
                {
                    Vector3 direction = AxisVector(axis, rotation, localSpace);
                    DrawArrow(moveLines[i], camera, pivot, direction, moveColor, 0f, 0.85f, moveSize);
                }
                if (showRotation)
                {
                    Vector3 direction = AxisVector(axis, rotation, localSpace);
                    DrawRing(rotationRings[i], pivot, direction, rotateColor, Scale * rotationSize * 0.62f);
                }
                if (showLayoutAxes)
                {
                    Vector3 direction = AxisVector(axis, rotation, localSpace);
                    float start = 0.76f;
                    float end = screenSpaceSizing ? 1.75f : 1.35f;
                    if (screenSpaceSizing && anchorPoints != null)
                    {
                        float scale = AxisGizmoScale(camera, pivot, direction, arraySize);
                        Vector3 pivotScreen = camera.WorldToScreenPoint(pivot);
                        Vector3 axisScreen = camera.WorldToScreenPoint(pivot + direction * scale);
                        Vector2 screenDirection = new Vector2(
                            axisScreen.x - pivotScreen.x, axisScreen.y - pivotScreen.y);
                        float pixelsPerFraction = screenDirection.magnitude;
                        screenDirection /= Mathf.Max(1f, pixelsPerFraction);
                        float farthestPixels = 0f;
                        for (int index = 0; index < anchorPoints.Length; ++index)
                        {
                            Vector3 anchorScreen = camera.WorldToScreenPoint(anchorPoints[index]);
                            if (anchorScreen.z <= 0f) continue;
                            farthestPixels = Mathf.Max(farthestPixels, Vector2.Dot(
                                new Vector2(anchorScreen.x - pivotScreen.x, anchorScreen.y - pivotScreen.y),
                                screenDirection));
                        }
                        start = Mathf.Max(start,
                            (farthestPixels + 40f * anchorHandleScale) / Mathf.Max(1f, pixelsPerFraction));
                        end = Mathf.Max(end, start + 0.99f);
                    }
                    DrawArrow(
                        layoutAxisLines[i],
                        camera,
                        pivot,
                        direction,
                        layoutColor,
                        start,
                        end,
                        arraySize);
                }
            }
            scaleHandle.gameObject.SetActive(allowExtended && showMove);
            if (allowExtended && showMove)
            {
                Color color = selectedHandle == GizmoHandleKind.Scale ? Color.yellow : Color.white;
                if (screenSpaceSizing) DrawScaleSquare(camera, pivot, color);
                else DrawAnchor(scaleHandle, camera, pivot - camera.transform.up * Scale * 0.9f,
                    color, 0.065f, 0.018f);
            }
            int anchorCount = anchorPoints?.Length ?? 0;
            EnsureAnchorHandleCount(anchorCount);
            bool showAnchors = mode != GizmoMode.Plane && anchorCount > 0;
            Vector2 mouse = Input.mousePosition;
            for (int i = 0; i < anchorHandles.Count; ++i)
            {
                bool important = i == selectedAnchor || i == pinnedAnchor;
                Vector3 screen = showAnchors && i < anchorCount
                    ? camera.WorldToScreenPoint(anchorPoints[i])
                    : Vector3.back;
                float screenDistance = screen.z > 0f
                    ? Vector2.Distance(mouse, new Vector2(screen.x, screen.y))
                    : float.PositiveInfinity;
                bool hovered = screenDistance <= AnchorHitRadius(i);
                bool showAnchor = showAnchors && i < anchorCount && screen.z > 0f &&
                    (showAllAnchors || important || screenDistance <= 120f) &&
                    (showAllAnchors || (pointVisibility != null
                        ? pointVisibility(anchorPoints[i]) : IsPointVisible(camera, anchorPoints[i])));
                if (screenSpaceSizing && showAnchor)
                {
                    // Appended selected/pinned points can coincide with a native or
                    // bounds handle. One glyph owns that position; pin has priority.
                    bool sharesPin = pinnedAnchor >= 0 && pinnedAnchor < anchorCount && i != pinnedAnchor &&
                        (anchorPoints[i] - anchorPoints[pinnedAnchor]).sqrMagnitude < 0.00000001f;
                    bool sharesSelection = selectedAnchor >= 0 && selectedAnchor < anchorCount &&
                        i != selectedAnchor && i != pinnedAnchor &&
                        (anchorPoints[i] - anchorPoints[selectedAnchor]).sqrMagnitude < 0.00000001f;
                    showAnchor = !sharesPin && !sharesSelection;
                    anchorHandles[i].sortingOrder = i == pinnedAnchor ? short.MaxValue
                        : i == selectedAnchor ? short.MaxValue - 1 : short.MaxValue - 2;
                }
                visibleAnchors[i] = showAnchor;
                anchorHandles[i].gameObject.SetActive(showAnchor);
                anchorHandles[i].sharedMaterial = showAllAnchors ? material : anchorMaterial;
                if (showAnchor)
                {
                    bool nativeAnchor = i >= nativeAnchorStart;
                    Color color = AnchorColor(i, selectedAnchor, pinnedAnchor, nativeAnchorStart);
                    color.a = hovered ? 1f : important ? 0.90f : nativeAnchor
                        ? screenSpaceSizing ? 0.95f : showAllAnchors ? 0.48f : 0.65f
                        : screenSpaceSizing ? 0.75f : showAllAnchors ? 0.22f : 0.45f;
                    DrawAnchor(
                        anchorHandles[i],
                        camera,
                        anchorPoints[i],
                        color,
                        (nativeAnchor ? hovered ? 0.17f : 0.115f
                            : screenSpaceSizing ? 0.06f : hovered ? 0.12f : 0.075f) * handleScale,
                        hovered ? 0.018f * handleScale + 0.003f
                            : nativeAnchor ? 0.010f : 0.007f, nativeAnchor);
                }
            }
            bool drawSnapTarget = showAnchors && showSnapTarget;
            snapTargetHandle.gameObject.SetActive(drawSnapTarget);
            if (drawSnapTarget)
            {
                Vector3 targetScreen = camera.WorldToScreenPoint(snapTarget);
                bool targetHovered = targetScreen.z > 0f && Vector2.Distance(
                    Input.mousePosition,
                    new Vector2(targetScreen.x, targetScreen.y)) <= 16f;
                Color targetColor = snapTargetIsNative
                    ? new Color(1f, 0.95f, 0.25f, 1f)
                    : new Color(1f, 0.55f, 0.1f, 1f);
                targetColor.a = targetHovered ? 1f : screenSpaceSizing ? 0.85f : 0.35f;
                DrawAnchor(
                    snapTargetHandle,
                    camera,
                    snapTarget,
                    targetColor,
                    (targetHovered ? 0.17f : snapTargetIsNative ? 0.115f : 0.06f) *
                        handleScale,
                    targetHovered ? 0.018f * handleScale + 0.003f
                        : snapTargetIsNative ? 0.010f : 0.006f, snapTargetIsNative);
            }
            bool drawConstraint = showAnchors && showConstraintPath &&
                constraintAxis != GizmoAxis.None && constraintAxisWorld.sqrMagnitude > 0.001f;
            constraintPath.gameObject.SetActive(drawConstraint);
            if (drawConstraint)
            {
                DrawRing(
                    constraintPath,
                    constraintCenter,
                    constraintAxisWorld,
                    AxisColors[(int)constraintAxis - 1],
                    constraintRadius > 0.001f ? constraintRadius : Scale * 0.72f);
            }
        }

        public void ShowLayout(
            Camera camera,
            IReadOnlyList<Vector3> contactPath,
            bool contactIsPoint,
            IReadOnlyList<Vector3> aimPath,
            bool aimIsPoint,
            IReadOnlyList<Vector3> previewContacts,
            IReadOnlyList<Vector3> previewAims)
        {
            DrawGuide(contactGuide, camera, contactPath,
                contactIsPoint, new Color(0.25f, 0.95f, 1f, 1f));
            DrawGuide(aimGuide, camera, aimPath,
                aimIsPoint, new Color(1f, 0.45f, 0.85f, 1f));

            int count = Math.Min(previewContacts?.Count ?? 0, previewAims?.Count ?? 0);
            while (layoutHandles.Count < count)
            {
                layoutHandles.Add(CreateLine("Layout" + layoutHandles.Count, 2));
            }
            for (int index = 0; index < layoutHandles.Count; ++index)
            {
                bool visible = index < count;
                LineRenderer line = layoutHandles[index];
                line.gameObject.SetActive(visible);
                if (!visible) continue;
                line.SetPosition(0, previewContacts[index]);
                line.SetPosition(1, previewAims[index]);
                SetColorAndWidth(line, new Color(1f, 0.82f, 0.2f, 0.75f));
            }
        }

        public void ShowSnapCandidates(
            Camera camera,
            IReadOnlyList<Vector3> candidates,
            IReadOnlyList<bool> nativeCandidates,
            float handleScale)
        {
            handleScale *= pointSize;
            int count = candidates?.Count ?? 0;
            while (snapCandidateHandles.Count < count)
            {
                snapCandidateHandles.Add(CreateLine(
                    "SnapCandidate" + snapCandidateHandles.Count,
                    5,
                    depthTested: true));
            }
            for (int index = 0; index < snapCandidateHandles.Count; ++index)
            {
                bool visible = camera && index < count;
                LineRenderer line = snapCandidateHandles[index];
                line.gameObject.SetActive(visible);
                if (visible)
                {
                    bool native = nativeCandidates != null &&
                        index < nativeCandidates.Count && nativeCandidates[index];
                    Vector3 screen = camera.WorldToScreenPoint(candidates[index]);
                    float distance = Vector2.Distance(
                        Input.mousePosition,
                        new Vector2(screen.x, screen.y));
                    bool close = distance <= 16f;
                    DrawAnchor(
                        line,
                        camera,
                        candidates[index],
                        new Color(1f, native ? 0.95f : 0.82f, native ? 0.25f : 0.2f,
                            close ? 0.98f : screenSpaceSizing ? native ? 0.85f : 0.6f
                                : native ? 0.42f : 0.08f),
                        (close ? native ? 0.17f : 0.12f
                            : native ? 0.115f : 0.04f) * handleScale,
                        close ? 0.018f * handleScale + 0.003f
                            : native ? 0.010f : 0.005f, native);
                }
            }
        }

        public void ShowAlignment(
            Camera camera,
            bool visible,
            Vector3 position,
            Quaternion rotation,
            float handleScale)
        {
            for (int index = 0; index < alignmentAxisLines.Length; ++index)
            {
                LineRenderer line = alignmentAxisLines[index];
                line.gameObject.SetActive(visible && camera);
                if (!visible || !camera) continue;
                Color color = AxisColors[index];
                color.a = 0.72f;
                DrawArrow(
                    line,
                    camera,
                    position,
                    rotation * AxisVector((GizmoAxis)(index + 1), Quaternion.identity, false),
                    color,
                    0f,
                    0.48f * handleScale);
            }
        }

        public GizmoAxis HitTestMove(
            Camera camera,
            Vector3 pivot,
            Quaternion rotation,
            bool localSpace,
            Vector2 mousePosition)
        {
            return HitTestAxes(camera, pivot, rotation, localSpace, mousePosition, 0f, 0.85f, moveSize);
        }

        public GizmoHandleKind HitTestExtra(Camera camera, Vector3 pivot, Quaternion rotation,
            bool localSpace, Vector2 mouse, out GizmoAxis axis)
        {
            axis = GizmoAxis.None;
            if (!camera || !scaleHandle.gameObject.activeInHierarchy) return GizmoHandleKind.None;
            if (HitTestScale(camera, mouse)) return GizmoHandleKind.Scale;
            float scale = GizmoScale(camera, pivot) * moveSize;
            Ray ray = camera.ScreenPointToRay(mouse);
            float closest = float.PositiveInfinity;
            for (int i = 0; i < 3; ++i)
            {
                GizmoAxis candidate = (GizmoAxis)(i + 1);
                Vector3 normal = AxisVector(candidate, rotation, localSpace);
                if (Mathf.Abs(Vector3.Dot(ray.direction, normal)) < 0.15f) continue;
                if (!new Plane(normal, pivot).Raycast(ray, out float distance) || distance >= closest) continue;
                PlaneBasis(candidate, rotation, localSpace, out Vector3 first, out Vector3 second);
                Vector3 point = (ray.GetPoint(distance) - pivot) / scale;
                float u = Vector3.Dot(point, first), v = Vector3.Dot(point, second);
                if (u < 0.18f || u > 0.35f || v < 0.18f || v > 0.35f) continue;
                axis = candidate;
                closest = distance;
            }
            return axis == GizmoAxis.None ? GizmoHandleKind.None : GizmoHandleKind.MovePlane;
        }

        internal bool HitTestScale(Camera camera, Vector2 mouse)
        {
            if (!camera || !scaleHandle.gameObject.activeInHierarchy) return false;
            Vector3 first = camera.WorldToScreenPoint(scaleHandle.GetPosition(0));
            Vector3 opposite = camera.WorldToScreenPoint(scaleHandle.GetPosition(2));
            if (first.z <= 0f || opposite.z <= 0f) return false;
            Vector2 center = (new Vector2(first.x, first.y) + new Vector2(opposite.x, opposite.y)) * 0.5f;
            if (!screenSpaceSizing) return Vector2.Distance(mouse, center) <= 8f;
            float half = Mathf.Abs(opposite.x - first.x) * 0.5f + 3f;
            return Mathf.Abs(mouse.x - center.x) <= half && Mathf.Abs(mouse.y - center.y) <= half;
        }

        private void DrawScaleSquare(Camera camera, Vector3 pivot, Color color)
        {
            Vector3 center = pivot + (camera.transform.right - camera.transform.up) * Scale * 1.05f;
            float half = Scale * 0.1f * uniformScaleHandleSize;
            Vector3 right = camera.transform.right * half, up = camera.transform.up * half;
            scaleHandle.SetPositions(new[] { center - right - up, center + right - up,
                center + right + up, center - right + up, center - right - up });
            SetColorAndWidth(scaleHandle, color, 0.025f * uniformScaleHandleSize);
        }

        private static void PlaneBasis(GizmoAxis normal, Quaternion rotation, bool localSpace,
            out Vector3 first, out Vector3 second)
        {
            first = AxisVector(normal == GizmoAxis.X ? GizmoAxis.Y : GizmoAxis.X, rotation, localSpace);
            second = AxisVector(normal == GizmoAxis.Z ? GizmoAxis.Y : GizmoAxis.Z, rotation, localSpace);
        }

        public GizmoAxis HitTestRotation(
            Camera camera,
            Vector3 pivot,
            Quaternion rotation,
            bool localSpace,
            Vector2 mousePosition)
        {
            if (!camera) return GizmoAxis.None;
            float scale = GizmoScale(camera, pivot);
            float bestDistance = screenSpaceSizing ? 6f : 18f;
            GizmoAxis bestAxis = GizmoAxis.None;
            for (int i = 0; i < 3; ++i)
            {
                GizmoAxis axis = (GizmoAxis)(i + 1);
                float distance = ScreenDistanceToRing(
                    camera,
                    mousePosition,
                    pivot,
                    AxisVector(axis, rotation, localSpace),
                    scale * rotationSize * 0.62f);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAxis = axis;
                }
            }
            return bestAxis;
        }

        public GizmoAxis HitTestLayout(
            Camera camera,
            Vector3 pivot,
            Quaternion rotation,
            bool localSpace,
            Vector2 mousePosition)
        {
            if (!camera) return GizmoAxis.None;
            float bestDistance = screenSpaceSizing ? 6f : 18f;
            GizmoAxis bestAxis = GizmoAxis.None;
            for (int index = 0; index < layoutAxisLines.Length; ++index)
            {
                LineRenderer line = layoutAxisLines[index];
                if (!line.gameObject.activeInHierarchy) continue;
                float distance = ScreenDistanceToSegment(
                    camera, mousePosition, line.GetPosition(0), line.GetPosition(1));
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                bestAxis = (GizmoAxis)(index + 1);
            }
            return bestAxis;
        }

        private GizmoAxis HitTestAxes(
            Camera camera,
            Vector3 pivot,
            Quaternion rotation,
            bool localSpace,
            Vector2 mousePosition,
            float startFraction,
            float endFraction,
            float sizeScale = 1f)
        {
            if (!camera) return GizmoAxis.None;
            float bestDistance = screenSpaceSizing ? 6f : 18f;
            GizmoAxis bestAxis = GizmoAxis.None;
            for (int i = 0; i < 3; ++i)
            {
                GizmoAxis axis = (GizmoAxis)(i + 1);
                Vector3 axisWorld = AxisVector(axis, rotation, localSpace);
                float scale = AxisGizmoScale(camera, pivot, axisWorld, sizeScale);
                float distance = ScreenDistanceToSegment(
                    camera,
                    mousePosition,
                    pivot + axisWorld * scale * startFraction,
                    pivot + axisWorld * scale * endFraction);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestAxis = axis;
                }
            }
            return bestAxis;
        }

        public int HitTestAnchor(Camera camera, Vector3[] points, Vector2 mousePosition)
        {
            if (!camera || points == null)
            {
                return -1;
            }

            int bestAnchor = -1;
            float bestDistance = float.PositiveInfinity;
            float bestDepth = float.PositiveInfinity;
            for (int i = points.Length - 1; i >= 0; --i)
            {
                if (i >= visibleAnchors.Count || !visibleAnchors[i])
                {
                    continue;
                }
                Vector3 screen = camera.WorldToScreenPoint(points[i]);
                if (screen.z <= 0f)
                {
                    continue;
                }
                float distance = Vector2.Distance(
                    mousePosition,
                    new Vector2(screen.x, screen.y));
                if (distance > AnchorHitRadius(i)) continue;
                if (distance < bestDistance - 0.5f ||
                    distance <= bestDistance + 0.5f && screen.z < bestDepth)
                {
                    bestDistance = distance;
                    bestDepth = screen.z;
                    bestAnchor = i;
                }
            }
            return bestAnchor;
        }

        private float AnchorHitRadius(int index)
        {
            return screenSpaceSizing
                ? (index >= nativeAnchorStartIndex ? 36f : 12f) * anchorHandleScale
                : 16f;
        }

        internal static bool IsPointVisible(Camera camera, Vector3 point)
        {
            if (!camera) return false;
            Vector3 delta = point - camera.transform.position;
            float distance = delta.magnitude;
            float maximumDistance = distance - Mathf.Max(0.08f, distance * 0.002f);
            if (maximumDistance <= 0f) return true;
            int hitCount = Physics.RaycastNonAlloc(
                camera.transform.position,
                delta / distance,
                VisibilityHits,
                maximumDistance,
                camera.cullingMask,
                QueryTriggerInteraction.Collide);
            Player player = Player.m_localPlayer;
            for (int index = 0; index < hitCount; ++index)
            {
                RaycastHit hit = VisibilityHits[index];
                if (player && hit.collider &&
                    (hit.collider.transform == player.transform ||
                    hit.collider.transform.IsChildOf(player.transform)))
                    continue;
                if (hit.collider && hit.collider.isTrigger &&
                    !hit.collider.GetComponentInParent<Piece>())
                    continue;
                return false;
            }
            return true;
        }

        public bool HitTestLayout(Camera camera, Vector2 mousePosition)
        {
            if (!camera)
            {
                return false;
            }
            if (HitTestLine(camera, mousePosition, contactGuide, 14f) ||
                HitTestLine(camera, mousePosition, aimGuide, 14f))
            {
                return true;
            }
            foreach (LineRenderer line in layoutHandles)
            {
                if (line.gameObject.activeInHierarchy &&
                    ScreenDistanceToSegment(
                        camera,
                        mousePosition,
                        line.GetPosition(0),
                        line.GetPosition(1)) < 10f)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool HitTestLine(
            Camera camera,
            Vector2 mouse,
            LineRenderer line,
            float threshold)
        {
            if (!line.gameObject.activeInHierarchy) return false;
            for (int index = 1; index < line.positionCount; ++index)
            {
                if (ScreenDistanceToSegment(
                    camera,
                    mouse,
                    line.GetPosition(index - 1),
                    line.GetPosition(index)) < threshold)
                    return true;
            }
            return false;
        }

        public void Hide()
        {
            if (root)
            {
                root.SetActive(false);
            }
        }

        private void DrawGuide(
            LineRenderer line,
            Camera camera,
            IReadOnlyList<Vector3> points,
            bool isPoint,
            Color color)
        {
            bool visible = points != null && points.Count > 0;
            line.gameObject.SetActive(visible);
            if (!visible) return;
            if (isPoint)
            {
                Vector3 start = points[0];
                float size = Math.Max(0.08f, Scale * 0.12f);
                start -= camera.transform.right * size;
                Vector3 end = start + camera.transform.right * size * 2f;
                line.positionCount = 2;
                line.SetPosition(0, start);
                line.SetPosition(1, end);
            }
            else
            {
                line.positionCount = Math.Max(2, points.Count);
                for (int index = 0; index < points.Count; ++index)
                    line.SetPosition(index, points[index]);
                if (points.Count == 1) line.SetPosition(1, points[0]);
            }
            SetColorAndWidth(line, color);
        }

        public void Dispose()
        {
            if (material)
            {
                UnityEngine.Object.Destroy(material);
            }
            if (anchorMaterial)
            {
                UnityEngine.Object.Destroy(anchorMaterial);
            }
            if (root)
            {
                UnityEngine.Object.Destroy(root);
            }
        }

        public static Vector3 AxisVector(GizmoAxis axis, Quaternion rotation, bool localSpace)
        {
            Vector3 direction;
            switch (axis)
            {
                case GizmoAxis.X: direction = Vector3.right; break;
                case GizmoAxis.Y: direction = Vector3.up; break;
                case GizmoAxis.Z: direction = Vector3.forward; break;
                default: return Vector3.zero;
            }
            return localSpace ? rotation * direction : direction;
        }

        public static void RepeatStepAxisAngle(
            GizmoAxis repeatAxis, Vector3 repeatStep, Quaternion rotation, bool localSpace,
            float yaw, float pitch, float roll, out Vector3 axis, out float degrees)
        {
            Quaternion basis = localSpace ? rotation : Quaternion.identity;
            axis = basis * Vector3.up;
            degrees = 0f;
            if (Mathf.Abs(yaw) + Mathf.Abs(pitch) + Mathf.Abs(roll) < 0.0001f) return;
            Vector3 positiveDirection = repeatAxis == GizmoAxis.None
                ? repeatStep.normalized : AxisVector(repeatAxis, rotation, localSpace);
            if (positiveDirection.sqrMagnitude < 0.0001f) positiveDirection = basis * Vector3.forward;
            Vector3 tangent = Quaternion.Inverse(basis) * positiveDirection.normalized;
            Vector3 pitchAxis = Vector3.Cross(tangent, Vector3.up);
            pitchAxis = pitchAxis.sqrMagnitude < 0.0001f ? Vector3.right : pitchAxis.normalized;
            Quaternion localDelta = Quaternion.AngleAxis(yaw, Vector3.up) *
                Quaternion.AngleAxis(pitch, pitchAxis) * Quaternion.AngleAxis(roll, tangent);
            if (repeatAxis != GizmoAxis.None && repeatStep.sqrMagnitude >= 0.0001f &&
                Vector3.Dot(repeatStep, positiveDirection) < 0f)
                localDelta = Quaternion.Inverse(localDelta);
            (basis * localDelta * Quaternion.Inverse(basis)).ToAngleAxis(out degrees, out axis);
            if (float.IsNaN(degrees) || float.IsInfinity(degrees) ||
                float.IsNaN(axis.x) || float.IsNaN(axis.y) || float.IsNaN(axis.z) ||
                axis.sqrMagnitude < 0.0001f)
            {
                axis = basis * Vector3.up;
                degrees = 0f;
            }
            else axis.Normalize();
        }

        private LineRenderer CreateLine(
            string name,
            int positionCount,
            bool depthTested = false)
        {
            GameObject child = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = root.layer
            };
            child.transform.SetParent(root.transform, false);
            LineRenderer line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = depthTested ? anchorMaterial : material;
            line.useWorldSpace = true;
            line.positionCount = positionCount;
            line.startWidth = 0.025f;
            line.endWidth = 0.025f;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.sortingOrder = short.MaxValue;
            return line;
        }

        private void EnsureAnchorHandleCount(int count)
        {
            while (anchorHandles.Count < count)
            {
                anchorHandles.Add(CreateLine(
                    "Anchor" + anchorHandles.Count,
                    5,
                    depthTested: true));
                visibleAnchors.Add(false);
            }
        }

        private void DrawArrow(
            LineRenderer line,
            Camera camera,
            Vector3 pivot,
            Vector3 axis,
            Color color,
            float startFraction,
            float endFraction,
            float sizeScale = 1f)
        {
            float arrowScale = AxisGizmoScale(camera, pivot, axis, sizeScale);
            float length = arrowScale * Mathf.Max(0.1f, endFraction - startFraction);
            Vector3 start = pivot + axis * arrowScale * startFraction;
            Vector3 end = pivot + axis * arrowScale * endFraction;
            Vector3 view = (camera.transform.position - end).normalized;
            Vector3 side = Vector3.Cross(axis, view);
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(axis, Vector3.up);
            }
            if (side.sqrMagnitude < 0.001f)
            {
                side = Vector3.Cross(axis, Vector3.right);
            }
            side.Normalize();
            Vector3 back = -axis * length * 0.28f;
            Vector3 wing = side * length * 0.13f;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.SetPosition(2, end + back + wing);
            line.SetPosition(3, end);
            line.SetPosition(4, end + back - wing);
            SetColorAndWidth(line, color);
        }

        private void DrawRing(
            LineRenderer line,
            Vector3 pivot,
            Vector3 axis,
            Color color,
            float radius)
        {
            RingBasis(axis, out Vector3 first, out Vector3 second);
            for (int i = 0; i <= RingSegments; ++i)
            {
                float angle = (float)(Math.PI * 2.0 * i / RingSegments);
                line.SetPosition(
                    i,
                    pivot + (first * Mathf.Cos(angle) + second * Mathf.Sin(angle)) * radius);
            }
            SetColorAndWidth(line, color);
        }

        private void DrawAnchor(
            LineRenderer line,
            Camera camera,
            Vector3 position,
            Color color,
            float sizeFraction,
            float width,
            bool nativeAnchor = false)
        {
            if (screenSpaceSizing)
            {
                sizeFraction *= nativeAnchor ? 2f : 1.5f;
                width = Mathf.Max(width, nativeAnchor ? 0.02f : 0.015f) * pointSize;
            }
            float pointScale = screenSpaceSizing ? GizmoScale(camera, position) : Scale;
            float size = pointScale * sizeFraction;
            Vector3 right = camera.transform.right * size;
            Vector3 up = camera.transform.up * size;
            line.SetPosition(0, position + up);
            line.SetPosition(1, position + right);
            line.SetPosition(2, position - up);
            line.SetPosition(3, position - right);
            line.SetPosition(4, position + up);
            SetColorAndWidth(line, color, width);
            line.startWidth = line.endWidth = screenSpaceSizing ? width * pointScale : width;
        }

        private static Color AnchorColor(
            int index,
            int selected,
            int pinned,
            int nativeAnchorStart)
        {
            if (index == pinned) return new Color(0.85f, 0.3f, 1f, 1f);
            if (index == selected) return new Color(1f, 0.9f, 0.2f, 1f);
            if (index >= nativeAnchorStart) return new Color(1f, 0.62f, 0.12f, 1f);
            if (index == AnchorAdjustment.CenterAnchorIndex)
                return new Color(1f, 0.4f, 0.8f, 1f);
            return index < 8
                ? new Color(0.25f, 0.95f, 1f, 1f)
                : new Color(0.35f, 1f, 0.45f, 1f);
        }

        private void SetColorAndWidth(
            LineRenderer line,
            Color color,
            float width = 0.018f)
        {
            line.startColor = color;
            line.endColor = color;
            line.startWidth = line.endWidth = screenSpaceSizing ? width * Scale : width;
        }

        private static void RingBasis(Vector3 axis, out Vector3 first, out Vector3 second)
        {
            Vector3 reference = Mathf.Abs(Vector3.Dot(axis, Vector3.up)) < 0.9f
                ? Vector3.up
                : Vector3.right;
            first = Vector3.Cross(axis, reference).normalized;
            second = Vector3.Cross(axis, first).normalized;
        }

        private static float ScreenDistanceToRing(
            Camera camera,
            Vector2 mouse,
            Vector3 pivot,
            Vector3 axis,
            float radius)
        {
            RingBasis(axis, out Vector3 first, out Vector3 second);
            float best = float.PositiveInfinity;
            Vector3 previous = pivot + first * radius;
            for (int i = 1; i <= RingSegments; ++i)
            {
                float angle = (float)(Math.PI * 2.0 * i / RingSegments);
                Vector3 current = pivot + (first * Mathf.Cos(angle) + second * Mathf.Sin(angle)) * radius;
                best = Mathf.Min(best, ScreenDistanceToSegment(camera, mouse, previous, current));
                previous = current;
            }
            return best;
        }

        private static float ScreenDistanceToSegment(
            Camera camera,
            Vector2 point,
            Vector3 startWorld,
            Vector3 endWorld)
        {
            return TryClosestPointOnScreenSegment(
                camera,
                point,
                startWorld,
                endWorld,
                out float distance,
                out _)
                ? distance
                : float.PositiveInfinity;
        }

        internal static bool TryClosestPointOnScreenSegment(
            Camera camera,
            Vector2 point,
            Vector3 startWorld,
            Vector3 endWorld,
            out float distance,
            out float segmentPosition)
        {
            Vector3 start = camera.WorldToScreenPoint(startWorld);
            Vector3 end = camera.WorldToScreenPoint(endWorld);
            if (start.z <= 0f || end.z <= 0f)
            {
                distance = float.PositiveInfinity;
                segmentPosition = 0f;
                return false;
            }

            Vector2 segment = new Vector2(end.x - start.x, end.y - start.y);
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared < 0.001f)
            {
                distance = Vector2.Distance(point, new Vector2(start.x, start.y));
                segmentPosition = 0f;
                return true;
            }

            Vector2 fromStart = point - new Vector2(start.x, start.y);
            float screenPosition = Mathf.Clamp01(Vector2.Dot(fromStart, segment) / lengthSquared);
            distance = Vector2.Distance(
                point,
                new Vector2(start.x, start.y) + segment * screenPosition);
            segmentPosition = camera.orthographic
                ? screenPosition
                : (float)AnchorAdjustment.PerspectiveSegmentParameter(
                    screenPosition,
                    start.z,
                    end.z);
            return true;
        }

        private float GizmoScale(Camera camera, Vector3 pivot)
        {
            if (!screenSpaceSizing)
                return Mathf.Clamp(Vector3.Distance(camera.transform.position, pivot) * 0.18f, 0.75f, 3f);
            float depth = Mathf.Max(camera.nearClipPlane,
                Vector3.Dot(pivot - camera.transform.position, camera.transform.forward));
            float height = camera.orthographic ? 2f * camera.orthographicSize
                : 2f * depth * Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            return height * 100f / Mathf.Max(1, camera.pixelHeight);
        }

        private float AxisGizmoScale(Camera camera, Vector3 pivot, Vector3 axis, float sizeScale)
        {
            float scale = GizmoScale(camera, pivot);
            if (!screenSpaceSizing) return scale * sizeScale;
            Vector3 start = camera.WorldToScreenPoint(pivot);
            Vector3 end = camera.WorldToScreenPoint(pivot + axis * scale);
            float pixels = Vector2.Distance(new Vector2(start.x, start.y), new Vector2(end.x, end.y));
            return scale * sizeScale * Mathf.Clamp(100f / Mathf.Max(1f, pixels), 1f, 4f);
        }

    }
}
