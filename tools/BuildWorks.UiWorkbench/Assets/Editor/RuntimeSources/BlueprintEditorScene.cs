using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal enum BlueprintEditorLightingPreset
    {
        Neutral,
        Warm,
        Contour
    }

    internal sealed class BlueprintEditorRenderIsolation : MonoBehaviour
    {
        private readonly Dictionary<Light, int> lightMasks = new Dictionary<Light, int>();
        private Transform editorRoot;
        private int editorMask;
        private BlueprintEditorLightingPreset preset;
        private AmbientMode ambientMode;
        private Color ambientLight;
        private float ambientIntensity;
        private float reflectionIntensity;
        private DefaultReflectionMode reflectionMode;
        private Texture customReflection;
        private bool fog;
        private bool applied;

        internal void Initialize(Transform root, int layer)
        {
            editorRoot = root;
            editorMask = 1 << layer;
        }

        internal void SetPreset(BlueprintEditorLightingPreset value) => preset = value;

        internal void RestoreNow() => Restore();

        private void OnPreCull()
        {
            if (applied || !editorRoot) return;
            applied = true;
            ambientMode = RenderSettings.ambientMode;
            ambientLight = RenderSettings.ambientLight;
            ambientIntensity = RenderSettings.ambientIntensity;
            reflectionIntensity = RenderSettings.reflectionIntensity;
            reflectionMode = RenderSettings.defaultReflectionMode;
            customReflection = RenderSettings.customReflectionTexture;
            fog = RenderSettings.fog;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = preset == BlueprintEditorLightingPreset.Warm
                ? new Color(0.34f, 0.25f, 0.18f)
                : preset == BlueprintEditorLightingPreset.Contour
                    ? new Color(0.10f, 0.13f, 0.17f)
                    : new Color(0.34f, 0.37f, 0.40f);
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;
            RenderSettings.fog = false;

            foreach (Light light in UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsSortMode.None))
            {
                if (!light || light.transform.IsChildOf(editorRoot) ||
                    (light.cullingMask & editorMask) == 0) continue;
                lightMasks[light] = light.cullingMask;
                light.cullingMask &= ~editorMask;
            }
        }

        private void OnPostRender() => Restore();

        private void OnDisable() => Restore();

        private void OnDestroy() => Restore();

        private void Restore()
        {
            if (!applied) return;
            foreach (KeyValuePair<Light, int> entry in lightMasks)
            {
                if (!entry.Key) continue;
                int withoutEditor = entry.Value & ~editorMask;
                entry.Key.cullingMask = entry.Key.cullingMask == withoutEditor
                    ? entry.Value
                    : entry.Key.cullingMask | (entry.Value & editorMask);
            }
            lightMasks.Clear();
            RenderSettings.ambientMode = ambientMode;
            RenderSettings.ambientLight = ambientLight;
            RenderSettings.ambientIntensity = ambientIntensity;
            RenderSettings.reflectionIntensity = reflectionIntensity;
            RenderSettings.defaultReflectionMode = reflectionMode;
            RenderSettings.customReflectionTexture = customReflection;
            RenderSettings.fog = fog;
            applied = false;
        }
    }

    internal sealed class BlueprintEditorScene : IDisposable
    {
        private const float GroundExtent = 1000f;
        private const float ContourEdgeScreenDistance = 30f;
        private const float SnapTargetScreenDistance = 52f;
        private const float SnapReleaseScreenDistance = 78f;
        private const float SnapEdgeScreenDistance = 12f;
        private const float SnapPreviewScreenDistance = 120f;
        private const int MaximumSnapPreviewTargets = 24;
        private const int MaximumNativeAnchors = 512;

        private sealed class PreviewMaterial
        {
            public Material Material;
            public Color BaseColor;
        }

        private sealed class VisualNode
        {
            public string PrefabName;
            public bool Placeholder;
            public bool Locked;
            public GameObject Root;
            public Vector3 BaseScale = Vector3.one;
            public Renderer[] Renderers;
            public bool Selected;
            public bool ActiveSelection;
            public bool Hovered;
            public readonly List<PickSurface> PickSurfaces = new List<PickSurface>();
            public readonly List<Vector3> SnapLocal = new List<Vector3>();
            public readonly List<Vector3> PlacementLocal = new List<Vector3>();
            public readonly List<string> PlacementLabels = new List<string>();
            public readonly List<PreviewMaterial> Materials = new List<PreviewMaterial>();
            public readonly List<UnityEngine.Object> OwnedAssets =
                new List<UnityEngine.Object>();
        }

        private sealed class PickSurface
        {
            public Renderer Renderer;
            public Vector3[] Vertices;
            public int[] Triangles;
            public Edge3[] Edges;
        }

        private sealed class MeshSnapshot
        {
            public Vector3[] Vertices;
            public int[] Triangles;
            public IReadOnlyList<Edge3> Edges;
        }

        private struct SnapCandidate
        {
            public Vector3 Point;
            public bool Native;
            public bool Edge;
            public float Distance;
        }

        private sealed class VisualState
        {
            public VisualNode Visual;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
            public bool Active;
            public bool Locked;
        }

        private readonly Func<string, GameObject> resolveVisualSource;
        private readonly Dictionary<string, VisualNode> visuals =
            new Dictionary<string, VisualNode>(StringComparer.Ordinal);
        private readonly Dictionary<Camera, int> worldCameraMasks =
            new Dictionary<Camera, int>();
        private readonly List<UnityEngine.Object> ownedAssets =
            new List<UnityEngine.Object>();
        private readonly Dictionary<Mesh, MeshSnapshot> meshSnapshots =
            new Dictionary<Mesh, MeshSnapshot>();
        private readonly int editorLayer;
        private readonly GameObject root;
        private readonly Camera camera;
        private readonly Transform ground;
        private readonly Transform minorGrid;
        private readonly Transform majorGrid;
        private readonly Light keyLight;
        private readonly Light fillLight;
        private readonly Light rimLight;
        private readonly TransformGizmoView gizmo;
        private readonly List<VisualNode> contourPreviews = new List<VisualNode>();
        private readonly List<VisualNode> duplicatePreviews = new List<VisualNode>();
        private VisualNode placementPreview;
        internal Vector3? PlacementSnapTarget { get; private set; }
        private BlueprintEditorRenderIsolation renderIsolation;
        private BlueprintEditorLightingPreset lightingPreset;
        private string hoveredId;
        private bool temporarySelectionHighlight;
        private float groundHeight;
        private bool disposed;
        private readonly MaterialPropertyBlock hoverProperties = new MaterialPropertyBlock();
        private string[] cachedAnchorIds;
        private Vector3[] cachedGizmoAnchors;
        private int cachedNativeAnchorStart;

        internal BlueprintEditorScene(
            Camera cameraTemplate,
            Func<string, GameObject> visualSourceResolver)
        {
            if (!cameraTemplate) throw new ArgumentNullException(nameof(cameraTemplate));
            resolveVisualSource = visualSourceResolver ??
                throw new ArgumentNullException(nameof(visualSourceResolver));
            // WearNTear.Highlight uses this tint and emission through MaterialMan.
            // Apply it only to our visual copies; selection retains the original material.
            Color hoverColor = new Color(0.6f, 0.8f, 1f, 1f);
            hoverProperties.SetColor("_Color", hoverColor);
            hoverProperties.SetColor("_BaseColor", hoverColor);
            hoverProperties.SetColor("_EmissionColor", hoverColor * 0.4f);
            editorLayer = FindUnusedEditorLayer();
            root = new GameObject("BuildWorks_BlueprintEditorScene")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            try
            {
                camera = CreateCamera(cameraTemplate);
                keyLight = CreateStudioLight("KeyLight", new Vector3(48f, -35f, 0f));
                fillLight = CreateStudioLight("FillLight", new Vector3(28f, 145f, 0f));
                rimLight = CreateStudioLight("RimLight", new Vector3(18f, 215f, 0f));
                ground = CreateGround().transform;
                minorGrid = CreateGrid(
                    "MinorGrid", 50f, 1f, new Color(0.26f, 0.29f, 0.32f, 0.55f)).transform;
                majorGrid = CreateGrid(
                    "MajorGrid", GroundExtent, 10f,
                    new Color(0.42f, 0.46f, 0.50f, 0.70f)).transform;
                gizmo = new TransformGizmoView(screenSpaceSizing: true);
                gizmo.SetLayer(editorLayer);
                ConfigureStudioLighting();
                EnsureWorldCamerasExcludeEditorLayer();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal Camera Camera => camera;
        internal int Layer => editorLayer;
        internal float GizmoScale => gizmo?.Scale ?? 0f;
        internal float GroundHeight => groundHeight;

        internal void EnsureWorldCamerasExcludeEditorLayer()
        {
            ThrowIfDisposed();
            int editorMask = 1 << editorLayer;
            foreach (Camera other in Resources.FindObjectsOfTypeAll<Camera>())
            {
                if (!other || other == camera || !other.gameObject.scene.IsValid()) continue;
                if (!worldCameraMasks.ContainsKey(other))
                    worldCameraMasks.Add(other, other.cullingMask);
                other.cullingMask &= ~editorMask;
            }
        }

        internal void SetViewport(Rect pixelRect)
        {
            ThrowIfDisposed();
            if (pixelRect.width <= 0f || pixelRect.height <= 0f) return;
            if (camera.pixelRect == pixelRect) return;
            camera.pixelRect = pixelRect;
        }

        internal void SetCameraPose(Vector3 position, Quaternion rotation)
        {
            ThrowIfDisposed();
            if (camera.transform.position == position && camera.transform.rotation == rotation) return;
            camera.transform.SetPositionAndRotation(position, rotation);
            RecenterEnvironment(position);
        }

        internal void SetViewportSettings(float moveScale, float rotationScale, float arrayScale,
            float pointScale, float uniformScaleHandleScale, bool showGrid)
        {
            ThrowIfDisposed();
            gizmo.SetEditorSizes(moveScale, rotationScale, arrayScale, pointScale, uniformScaleHandleScale);
            minorGrid.gameObject.SetActive(showGrid);
            majorGrid.gameObject.SetActive(showGrid);
        }

        internal void SetProjection(bool orthographic, float focusDistance)
        {
            ThrowIfDisposed();
            camera.orthographic = orthographic;
            if (orthographic)
                camera.orthographicSize = Mathf.Max(0.01f, focusDistance) *
                    Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
        }

        internal bool TrySync(BlueprintEditorDocument document, out string warning)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            InvalidateGizmoAnchors();

            var missingPrefabs = new List<string>();
            var staged = new List<VisualNode>();
            var oldStates = new List<VisualState>();
            try
            {
                var sources = new Dictionary<string, GameObject>(StringComparer.Ordinal);
                foreach (BlueprintEditorPart part in document.Parts)
                {
                    if (!TryUnityTransform(part, out _, out _))
                        throw new InvalidOperationException(
                            "Part transform is outside Unity range: " + part.StableId);
                    GameObject source = resolveVisualSource(part.PrefabName);
                    sources.Add(part.StableId, source);
                    if (!source && !missingPrefabs.Contains(part.PrefabName))
                        missingPrefabs.Add(part.PrefabName);
                }
                foreach (VisualNode visual in visuals.Values)
                {
                    oldStates.Add(new VisualState
                    {
                        Visual = visual,
                        Position = visual.Root.transform.localPosition,
                        Rotation = visual.Root.transform.localRotation,
                        Scale = visual.Root.transform.localScale,
                        Active = visual.Root.activeSelf,
                        Locked = visual.Locked
                    });
                }

                var nextVisuals = new Dictionary<string, VisualNode>(StringComparer.Ordinal);
                foreach (BlueprintEditorPart part in document.Parts)
                {
                    GameObject source = sources[part.StableId];
                    bool placeholder = !source;
                    if (!visuals.TryGetValue(part.StableId, out VisualNode visual) ||
                        visual.PrefabName != part.PrefabName || visual.Placeholder != placeholder)
                    {
                        visual = CreateVisual(part, source);
                        visual.Root.SetActive(false);
                        staged.Add(visual);
                    }
                    nextVisuals.Add(part.StableId, visual);
                    ApplyPart(
                        visual,
                        part,
                        document.IsEffectivelyVisible(part.StableId),
                        document.IsEffectivelyLocked(part.StableId));
                }

                UpdateHighlights(document, nextVisuals);
                var retired = new List<VisualNode>();
                foreach (KeyValuePair<string, VisualNode> entry in visuals)
                {
                    if (!nextVisuals.TryGetValue(entry.Key, out VisualNode next) ||
                        !ReferenceEquals(entry.Value, next)) retired.Add(entry.Value);
                }
                visuals.Clear();
                foreach (KeyValuePair<string, VisualNode> entry in nextVisuals)
                    visuals.Add(entry.Key, entry.Value);
                foreach (VisualNode visual in retired) DestroyVisual(visual);
                UpdateGroundHeight();
            }
            catch (Exception exception)
            {
                foreach (VisualState state in oldStates)
                {
                    if (!state.Visual.Root) continue;
                    state.Visual.Root.transform.SetLocalPositionAndRotation(
                        state.Position, state.Rotation);
                    state.Visual.Root.transform.localScale = state.Scale;
                    state.Visual.Root.SetActive(state.Active);
                    state.Visual.Locked = state.Locked;
                }
                foreach (VisualNode visual in staged) DestroyVisual(visual);
                warning = "Blueprint editor scene sync failed: " + exception.Message;
                return false;
            }

            warning = missingPrefabs.Count == 0
                ? null
                : "Missing prefab placeholder: " + string.Join(", ", missingPrefabs);
            return true;
        }

        internal bool TryPick(Vector2 screenPosition, out string stableId)
        {
            return TryPickPoint(screenPosition, out stableId, out _);
        }

        internal bool TryPickPoint(
            Vector2 screenPosition,
            out string stableId,
            out Vector3 point)
        {
            return TryPickPoint(
                screenPosition, includeLocked: false, out stableId, out point, out _);
        }

        private bool TryPickPoint(
            Vector2 screenPosition,
            bool includeLocked,
            out string stableId,
            out Vector3 point)
        {
            return TryPickPoint(
                screenPosition, includeLocked, out stableId, out point, out _);
        }

        private bool TryPickPoint(
            Vector2 screenPosition,
            bool includeLocked,
            out string stableId,
            out Vector3 point,
            out Vector3 normal)
        {
            ThrowIfDisposed();
            Ray ray = camera.ScreenPointToRay(screenPosition);
            float nearest = float.PositiveInfinity;
            stableId = null;
            point = Vector3.zero;
            normal = Vector3.up;
            foreach (KeyValuePair<string, VisualNode> entry in visuals)
            {
                VisualNode visual = entry.Value;
                if (!includeLocked && visual.Locked || !visual.Root.activeInHierarchy ||
                    !TryBounds(visual, out Bounds bounds) ||
                    !bounds.IntersectRay(ray) ||
                    !TryMeshHit(visual, ray, out float distance, out Vector3 hitNormal) ||
                    distance >= nearest)
                    continue;
                nearest = distance;
                stableId = entry.Key;
                point = ray.GetPoint(distance);
                normal = hitNormal;
            }
            return stableId != null;
        }

        internal bool TryPlacementSurface(Vector2 screenPosition, out Vector3 point)
        {
            return TryPlacementSurface(screenPosition, out point, out _);
        }

        internal bool TryPlacementSurface(
            Vector2 screenPosition,
            out Vector3 point,
            out Vector3 normal)
        {
            if (TryPickPoint(
                screenPosition, includeLocked: true, out _, out point, out normal)) return true;
            Ray ray = camera.ScreenPointToRay(screenPosition);
            var plane = new Plane(Vector3.up, new Vector3(0f, groundHeight, 0f));
            if (!plane.Raycast(ray, out float distance))
            {
                normal = Vector3.up;
                return false;
            }
            point = ray.GetPoint(distance);
            normal = Vector3.up;
            return true;
        }

        internal bool IsEditorPointVisible(
            Vector3 point, IReadOnlyList<string> excludedStableIds = null)
        {
            ThrowIfDisposed();
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z < camera.nearClipPlane || !camera.pixelRect.Contains(screen)) return false;
            Ray ray = camera.ScreenPointToRay(screen);
            float distance = Vector3.Dot(point - ray.origin, ray.direction);
            if (distance < 0.001f) return true;
            float maximum = distance - Mathf.Max(0.005f, distance * 0.0001f);
            foreach (KeyValuePair<string, VisualNode> entry in visuals)
            {
                bool excluded = false;
                if (excludedStableIds != null)
                    for (int index = 0; index < excludedStableIds.Count; ++index)
                        if (excludedStableIds[index] == entry.Key) { excluded = true; break; }
                VisualNode visual = entry.Value;
                if (excluded || !visual.Root.activeInHierarchy ||
                    !TryBounds(visual, out Bounds bounds) || !bounds.IntersectRay(ray)) continue;
                if (TryMeshHit(visual, ray, out float hit) && hit < maximum) return false;
            }
            return true;
        }

        internal bool TryFindContourSupports(
            BlueprintEditorDocument document,
            Vector2 screenPosition,
            out List<string> orderedSupportIds,
            out List<Vector3> guidePoints,
            out bool closed,
            out string warning)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            orderedSupportIds = new List<string>();
            guidePoints = new List<Vector3>();
            closed = false;
            warning = null;
            if (!TryPickPoint(
                screenPosition, includeLocked: true, out string seedId, out _))
            {
                warning = "Наведи курсор на деталь опорной цепи или кольца.";
                return false;
            }
            if (document.IsPartSelected(seedId))
            {
                warning = "Выбери опору, а не исходную деталь.";
                return false;
            }

            BlueprintEditorPart seed = FindPart(document, seedId);
            if (seed == null || !visuals.TryGetValue(seedId, out VisualNode seedVisual) ||
                !TrySelectEditorContourEdge(
                    seedVisual, screenPosition, out Edge3 selectedEdge,
                    out Vector3 pathPointLocal))
            {
                warning = "У выбранного ребра нет пары штатных точек соединения.";
                return false;
            }

            var candidateIds = new List<string>();
            var connectionPoints = new List<Point3[]>();
            int seedIndex = -1;
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (!string.Equals(part.PrefabName, seed.PrefabName, StringComparison.Ordinal) ||
                    document.IsPartSelected(part.StableId) ||
                    !document.IsEffectivelyVisible(part.StableId) ||
                    !visuals.TryGetValue(part.StableId, out VisualNode visual) ||
                    !visual.Root.activeInHierarchy)
                    continue;
                if (part.StableId == seedId) seedIndex = candidateIds.Count;
                candidateIds.Add(part.StableId);
                connectionPoints.Add(new[]
                {
                    ToGeometry(visual.Root.transform.TransformPoint(
                        ToUnity(selectedEdge.Start))),
                    ToGeometry(visual.Root.transform.TransformPoint(
                        ToUnity(selectedEdge.End)))
                });
            }
            if (seedIndex < 0 || candidateIds.Count < 2)
            {
                warning = "Нужна цепь минимум из двух одинаковых опор.";
                return false;
            }

            IReadOnlyList<int> ordered = ConstructionLayout.OrderConnectedContour(
                connectionPoints, seedIndex, out closed);
            if (!closed)
            {
                IReadOnlyList<int> touching = ConstructionLayout.OrderTouchingContour(
                    connectionPoints, seedIndex, out bool touchingClosed);
                if (touching.Count > ordered.Count ||
                    touching.Count == ordered.Count && touchingClosed)
                {
                    ordered = touching;
                    closed = touchingClosed;
                }
            }
            if (ordered.Count < 2)
            {
                warning = "Эта опора не входит в простую цепь или кольцо без развилок.";
                return false;
            }
            foreach (int index in ordered)
            {
                string stableId = candidateIds[index];
                orderedSupportIds.Add(stableId);
                guidePoints.Add(visuals[stableId].Root.transform.TransformPoint(pathPointLocal));
            }
            return true;
        }

        internal void ShowContourPreview(IReadOnlyList<BlueprintEditorPart> preview)
        {
            ShowTemporaryPreview(preview, contourPreviews, new Color(1f, 0.58f, 0.12f, 1f));
        }

        internal void ShowDuplicatePreview(IReadOnlyList<BlueprintEditorPart> preview) =>
            ShowTemporaryPreview(preview, duplicatePreviews, null);

        internal void ClearDuplicatePreview() => ClearTemporaryPreview(duplicatePreviews);

        internal int PlacementSourceSnapPointCount
        {
            get
            {
                if (placementPreview?.Root && placementPreview.Root.activeSelf)
                    return placementPreview.PlacementLocal.Count;
                int count = 0;
                foreach (VisualNode visual in duplicatePreviews)
                    count += visual.PlacementLocal.Count;
                return count;
            }
        }

        internal string PlacementSnapPointLabel(int index)
        {
            if (index < 0) return null;
            if (placementPreview?.Root && placementPreview.Root.activeSelf)
                return index < placementPreview.PlacementLabels.Count
                    ? placementPreview.PlacementLabels[index]
                    : null;
            foreach (VisualNode visual in duplicatePreviews)
            {
                if (index < visual.PlacementLabels.Count)
                    return visual.PlacementLabels[index];
                index -= visual.PlacementLabels.Count;
            }
            return null;
        }

        internal void ShowBlueprintPlacementPreview(IReadOnlyList<BlueprintEditorPart> preview,
            Vector3 surfacePoint, out Vector3 offset, bool snap = false,
            int manualSnapPoint = -1)
        {
            PlacementSnapTarget = null;
            ShowDuplicatePreview(preview);
            offset = Vector3.zero;
            bool found = false;
            Bounds bounds = default;
            foreach (VisualNode visual in duplicatePreviews)
                if (TryBounds(visual, out Bounds partBounds))
                {
                    if (found) bounds.Encapsulate(partBounds);
                    else { bounds = partBounds; found = true; }
                }
            if (!found) return;
            offset.y = surfacePoint.y - bounds.min.y;
            foreach (VisualNode visual in duplicatePreviews)
                visual.Root.transform.position += offset;
            if (snap)
            {
                float best = 0.55f * 0.55f;
                Vector3 snapOffset = Vector3.zero;
                int sourceIndex = 0;
                foreach (VisualNode visual in duplicatePreviews)
                    AccumulatePlacementSnapOffset(
                        visual, manualSnapPoint, ref sourceIndex, ref best, ref snapOffset);
                foreach (VisualNode visual in duplicatePreviews)
                    visual.Root.transform.position += snapOffset;
                offset += snapOffset;
            }
        }

        internal void ClearBlueprintPlacementPreview()
        {
            PlacementSnapTarget = null;
            ClearDuplicatePreview();
        }

        private void ShowTemporaryPreview(IReadOnlyList<BlueprintEditorPart> preview,
            List<VisualNode> target, Color? tint)
        {
            ThrowIfDisposed();
            if (preview == null) { ClearTemporaryPreview(target); return; }
            try
            {
                for (int index = 0; index < preview.Count; ++index)
                {
                    BlueprintEditorPart part = preview[index];
                    VisualNode visual = index < target.Count ? target[index] : null;
                    if (visual == null || visual.PrefabName != part.PrefabName)
                    {
                        GameObject source = resolveVisualSource(part.PrefabName);
                        if (!source)
                            throw new InvalidOperationException(
                                "Не найдена деталь предпросмотра: " + part.PrefabName);
                        VisualNode replacement = CreateVisual(part, source, selectable: false);
                        DestroyVisual(visual);
                        visual = replacement;
                        if (index < target.Count) target[index] = visual;
                        else target.Add(visual);
                    }
                    ApplyPart(visual, part, effectivelyVisible: true, effectivelyLocked: true);
                    if (tint.HasValue)
                    {
                        foreach (PreviewMaterial material in visual.Materials)
                        {
                            Color color = Color.Lerp(material.BaseColor, tint.Value, 0.55f);
                            if (material.Material.HasProperty("_Color"))
                                material.Material.SetColor("_Color", color);
                            if (material.Material.HasProperty("_BaseColor"))
                                material.Material.SetColor("_BaseColor", color);
                        }
                    }
                }
                for (int index = target.Count - 1; index >= preview.Count; --index)
                {
                    DestroyVisual(target[index]);
                    target.RemoveAt(index);
                }
            }
            catch
            {
                ClearTemporaryPreview(target);
                throw;
            }
        }

        internal void ShowContourGuide(IReadOnlyList<Vector3> points, bool closed)
        {
            ThrowIfDisposed();
            if (points == null || points.Count < 2)
            {
                gizmo.Hide();
                return;
            }
            gizmo.Show(
                camera,
                points[0],
                Quaternion.identity,
                false,
                GizmoMode.Guide,
                GizmoHandleKind.None,
                GizmoAxis.None,
                Array.Empty<Vector3>(),
                -1,
                -1,
                0,
                false,
                1f,
                false,
                Vector3.zero,
                false,
                GizmoAxis.None,
                false,
                Vector3.zero,
                Vector3.up,
                0f,
                allowMove: false,
                allowRotate: false);
            var path = new List<Vector3>(points);
            if (closed) path.Add(points[0]);
            var aims = new List<Vector3>(points.Count);
            foreach (Vector3 point in points) aims.Add(point + Vector3.up * gizmo.Scale * 0.35f);
            gizmo.ShowLayout(
                camera,
                path,
                contactIsPoint: false,
                Array.Empty<Vector3>(),
                aimIsPoint: false,
                points,
                aims);
        }

        internal void ClearContourPreview()
        {
            ClearTemporaryPreview(contourPreviews);
        }

        private static void ClearTemporaryPreview(List<VisualNode> target)
        {
            foreach (VisualNode visual in target) DestroyVisual(visual);
            target.Clear();
        }

        private static BlueprintEditorPart FindPart(
            BlueprintEditorDocument document,
            string stableId)
        {
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == stableId) return part;
            return null;
        }

        private bool TrySelectEditorContourEdge(
            VisualNode visual,
            Vector2 mouse,
            out Edge3 selected,
            out Vector3 selectedPointLocal)
        {
            selected = default;
            selectedPointLocal = Vector3.zero;
            var native = new List<Point3>(visual.SnapLocal.Count);
            foreach (Vector3 point in visual.SnapLocal) native.Add(ToGeometry(point));
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(native);
            float maximum = ContourEdgeScreenDistance * ContourEdgeScreenDistance;
            float best = maximum;
            foreach (Edge3 edge in edges)
            {
                Vector3 start = ToUnity(edge.Start);
                Vector3 end = ToUnity(edge.End);
                Vector3 startScreen = camera.WorldToScreenPoint(
                    visual.Root.transform.TransformPoint(start));
                Vector3 endScreen = camera.WorldToScreenPoint(
                    visual.Root.transform.TransformPoint(end));
                if (startScreen.z <= 0f || endScreen.z <= 0f) continue;
                Vector2 start2 = new Vector2(startScreen.x, startScreen.y);
                Vector2 direction = new Vector2(
                    endScreen.x - startScreen.x, endScreen.y - startScreen.y);
                float lengthSquared = direction.sqrMagnitude;
                if (lengthSquared < 0.000001f) continue;
                float screenParameter = Mathf.Clamp01(
                    Vector2.Dot(mouse - start2, direction) / lengthSquared);
                float distance = (mouse - (start2 + direction * screenParameter)).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                selected = edge;
                float perspectiveParameter = (float)AnchorAdjustment.PerspectiveSegmentParameter(
                    screenParameter, startScreen.z, endScreen.z);
                selectedPointLocal = Vector3.Lerp(start, end, perspectiveParameter);
            }
            return best < maximum;
        }

        internal List<string> BoxSelect(Rect screenRect)
        {
            ThrowIfDisposed();
            var result = new List<string>();
            foreach (KeyValuePair<string, VisualNode> entry in visuals)
            {
                VisualNode visual = entry.Value;
                if (visual.Locked || !visual.Root.activeInHierarchy ||
                    !TryBounds(visual, out Bounds bounds)) continue;
                if (ScreenRectIntersectsBounds(camera, screenRect, bounds))
                    result.Add(entry.Key);
            }
            return result;
        }

        internal void SetHovered(string stableId, BlueprintEditorDocument document)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (hoveredId == stableId) return;
            hoveredId = stableId;
            UpdateHighlights(document);
        }

        internal void SetTemporarySelectionHighlight(bool enabled, BlueprintEditorDocument document)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            if (temporarySelectionHighlight == enabled) return;
            temporarySelectionHighlight = enabled;
            UpdateHighlights(document);
        }

        internal void ShowGizmo(
            BlueprintEditorTool tool,
            Vector3 pivot,
            Quaternion orientation,
            bool localSpace,
            GizmoHandleKind selectedHandle,
            GizmoAxis selectedAxis,
            Vector3[] anchorPoints,
            int selectedAnchor,
            int nativeAnchorStart,
            bool showAllAnchors,
            bool showSnapTarget,
            Vector3 snapTarget,
            bool snapTargetIsNative,
            int pinnedAnchor = -1,
            GizmoAxis constraintAxis = GizmoAxis.None,
            bool constraintActive = false,
            Vector3 constraintCenter = default,
            Vector3 constraintWorldAxis = default,
            float constraintRadius = 0f)
        {
            ThrowIfDisposed();
            bool transform = tool != BlueprintEditorTool.Select;
            if (!transform)
            {
                gizmo.Hide();
                return;
            }
            gizmo.Show(
                camera,
                pivot,
                orientation,
                localSpace,
                tool == BlueprintEditorTool.Array ? GizmoMode.Repeat : GizmoMode.Move,
                selectedHandle,
                selectedAxis,
                anchorPoints ?? Array.Empty<Vector3>(),
                selectedAnchor,
                pinnedAnchor,
                nativeAnchorStart,
                showAllAnchors,
                1f,
                showSnapTarget,
                snapTarget,
                snapTargetIsNative,
                constraintAxis,
                constraintActive,
                constraintCenter,
                constraintWorldAxis,
                constraintRadius,
                allowMove: true,
                allowRotate: true,
                allowExtended: true,
                pointVisibility: point => IsEditorPointVisible(point));
        }

        internal void ShowSnapCandidates(
            IReadOnlyList<Vector3> candidates,
            IReadOnlyList<bool> nativeCandidates) =>
            gizmo.ShowSnapCandidates(camera, candidates, nativeCandidates, 1f);

        internal int HitTestAnchor(Vector3[] anchorPoints, Vector2 mousePosition)
        {
            ThrowIfDisposed();
            return gizmo.HitTestAnchor(camera, anchorPoints, mousePosition);
        }

        internal bool HitTestScale(Vector2 mousePosition)
        {
            ThrowIfDisposed();
            return gizmo.HitTestScale(camera, mousePosition);
        }

        internal void ShowPlacementTarget(Vector3 surfacePoint, bool snapped)
        {
            ThrowIfDisposed();
            gizmo.Show(camera, surfacePoint, Quaternion.identity, false, GizmoMode.Guide,
                GizmoHandleKind.None, GizmoAxis.None, new[] { surfacePoint }, -1, -1,
                snapped ? 0 : int.MaxValue, true, 1f, false, Vector3.zero, false,
                GizmoAxis.None, false, Vector3.zero, Vector3.zero, 0f,
                allowMove: false, allowRotate: false);
            gizmo.ShowLayout(camera, Array.Empty<Vector3>(), false, Array.Empty<Vector3>(), false,
                Array.Empty<Vector3>(), Array.Empty<Vector3>());
        }

        internal void HidePlacementTarget() => HideGizmo();

        internal GizmoHandleKind HitTestGizmo(
            BlueprintEditorTool tool,
            Vector3 pivot,
            Quaternion orientation,
            bool localSpace,
            Vector2 mousePosition,
            out GizmoAxis axis)
        {
            ThrowIfDisposed();
            axis = GizmoAxis.None;
            if (gizmo.HitTestScale(camera, mousePosition)) return GizmoHandleKind.Scale;
            if (tool == BlueprintEditorTool.Array)
            {
                axis = gizmo.HitTestLayout(
                    camera, pivot, orientation, localSpace, mousePosition);
                if (axis != GizmoAxis.None) return GizmoHandleKind.Layout;
            }
            GizmoHandleKind extra = gizmo.HitTestExtra(
                camera, pivot, orientation, localSpace, mousePosition, out axis);
            if (extra != GizmoHandleKind.None) return extra;
            axis = gizmo.HitTestMove(
                camera, pivot, orientation, localSpace, mousePosition);
            if (axis != GizmoAxis.None) return GizmoHandleKind.Move;
            axis = gizmo.HitTestRotation(
                camera, pivot, orientation, localSpace, mousePosition);
            return axis == GizmoAxis.None
                ? GizmoHandleKind.None
                : GizmoHandleKind.Rotate;
        }

        internal void HideGizmo()
        {
            ThrowIfDisposed();
            gizmo.Hide();
        }

        internal void ShowArrayPreview(IReadOnlyList<BlueprintEditorPart> preview) =>
            ShowContourPreview(preview);

        internal void ClearArrayPreview() => ClearContourPreview();

        internal void PreviewTransform(
            BlueprintEditorDocument document,
            IReadOnlyList<string> stableIds,
            Vector3 translation,
            Quaternion rotation,
            Vector3 pivot,
            float uniformScale = 1f)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            InvalidateGizmoAnchors();
            var selected = new HashSet<string>(stableIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (!selected.Contains(part.StableId) ||
                    !visuals.TryGetValue(part.StableId, out VisualNode visual)) continue;
                Vector3 sourcePosition = new Vector3(
                    (float)part.Position.X,
                    (float)part.Position.Y,
                    (float)part.Position.Z);
                Quaternion sourceRotation = new Quaternion(
                    (float)part.Rotation.X,
                    (float)part.Rotation.Y,
                    (float)part.Rotation.Z,
                    (float)part.Rotation.W);
                visual.Root.transform.SetLocalPositionAndRotation(
                    pivot + rotation * ((sourcePosition - pivot) * uniformScale) + translation,
                    rotation * sourceRotation);
                visual.Root.transform.localScale = Vector3.Scale(
                    visual.BaseScale, ToUnity(part.Scale)) * uniformScale;
            }
            UpdateHighlights(document);
        }

        internal bool ShowPlacementPreview(
            string prefabName,
            Vector3 position,
            Quaternion rotation,
            bool snap,
            out Vector3 finalPosition,
            out string warning,
            int manualSnapPoint = -1)
        {
            return ShowPlacementPreview(
                prefabName, position, Vector3.up, rotation, snap,
                out finalPosition, out warning, manualSnapPoint);
        }

        internal bool ShowPlacementPreview(
            string prefabName,
            Vector3 position,
            Vector3 surfaceNormal,
            Quaternion rotation,
            bool snap,
            out Vector3 finalPosition,
            out string warning,
            int manualSnapPoint = -1)
        {
            ThrowIfDisposed();
            finalPosition = position;
            PlacementSnapTarget = null;
            warning = null;
            try
            {
                if (placementPreview == null ||
                    !string.Equals(
                        placementPreview.PrefabName, prefabName, StringComparison.Ordinal))
                {
                    DestroyVisual(placementPreview);
                    GameObject source = resolveVisualSource(prefabName);
                    if (!source)
                    {
                        placementPreview = null;
                        warning = "Не найдена деталь: " + prefabName;
                        return false;
                    }
                    placementPreview = CreateVisual(
                        new BlueprintEditorPart(
                            "placement-preview",
                            prefabName,
                            prefabName,
                            new Point3(0.0, 0.0, 0.0),
                            new Rotation3(0.0, 0.0, 0.0, 1.0)),
                        source, selectable: false, captureSurfaces: true);
                }
                Vector3 surfacePoint = position;
                placementPreview.Root.transform.SetLocalPositionAndRotation(position, rotation);
                placementPreview.Root.SetActive(true);
                position += PlacementContactOffset(placementPreview, position, surfaceNormal);
                placementPreview.Root.transform.localPosition = position;
                if (snap && manualSnapPoint >= 0 &&
                    manualSnapPoint < placementPreview.PlacementLocal.Count)
                {
                    position += surfacePoint - placementPreview.Root.transform.TransformPoint(
                        placementPreview.PlacementLocal[manualSnapPoint]);
                    placementPreview.Root.transform.localPosition = position;
                }
                if (snap)
                {
                    float best = 0.55f * 0.55f;
                    Vector3 offset = Vector3.zero;
                    int sourceIndex = 0;
                    AccumulatePlacementSnapOffset(
                        placementPreview, manualSnapPoint, ref sourceIndex, ref best, ref offset);
                    position += offset;
                }
                placementPreview.Root.transform.localPosition = position;
                finalPosition = position;
                return true;
            }
            catch (Exception exception)
            {
                DestroyVisual(placementPreview);
                placementPreview = null;
                warning = "Не удалось показать деталь: " + exception.Message;
                return false;
            }
        }

        private void AccumulatePlacementSnapOffset(
            VisualNode source,
            int manualSnapPoint,
            ref int sourceIndex,
            ref float best,
            ref Vector3 offset)
        {
            if (source == null || source.PlacementLocal.Count == 0) return;
            foreach (Vector3 sourceLocal in source.PlacementLocal)
            {
                bool enabled = manualSnapPoint < 0 || manualSnapPoint == sourceIndex;
                ++sourceIndex;
                if (!enabled) continue;
                Vector3 sourceWorld = source.Root.transform.TransformPoint(sourceLocal);
                foreach (VisualNode visual in visuals.Values)
                {
                    if (!visual.Root.activeInHierarchy) continue;
                    foreach (Vector3 targetLocal in visual.PlacementLocal)
                    {
                        Vector3 targetWorld = visual.Root.transform.TransformPoint(targetLocal);
                        float distance = (targetWorld - sourceWorld).sqrMagnitude;
                        if (distance >= best) continue;
                        best = distance;
                        offset = targetWorld - sourceWorld;
                        PlacementSnapTarget = targetWorld;
                    }
                }
            }
        }

        private static Vector3 PlacementContactOffset(
            VisualNode visual,
            Vector3 surfacePoint,
            Vector3 surfaceNormal)
        {
            if (surfaceNormal.sqrMagnitude < 0.000001f) surfaceNormal = Vector3.up;
            surfaceNormal.Normalize();
            float minimum = float.PositiveInfinity;
            foreach (PickSurface surface in visual.PickSurfaces)
            {
                if (!surface.Renderer || !surface.Renderer.enabled ||
                    !surface.Renderer.gameObject.activeInHierarchy) continue;
                foreach (Vector3 vertex in surface.Vertices)
                {
                    float distance = Vector3.Dot(
                        visual.Root.transform.TransformPoint(vertex) - surfacePoint,
                        surfaceNormal);
                    if (distance < minimum) minimum = distance;
                }
            }
            return float.IsInfinity(minimum) ? Vector3.zero : -surfaceNormal * minimum;
        }

        internal void HidePlacementPreview()
        {
            PlacementSnapTarget = null;
            if (placementPreview?.Root) placementPreview.Root.SetActive(false);
        }

        internal bool TryGetContentBounds(out Bounds bounds)
        {
            return TryGetContentBounds(includeHidden: false, out bounds);
        }

        internal bool TryGetSelectionBounds(
            BlueprintEditorDocument document,
            out Bounds bounds)
        {
            ThrowIfDisposed();
            if (document == null) throw new ArgumentNullException(nameof(document));
            bounds = default;
            bool found = false;
            foreach (BlueprintEditorPart part in document.Parts)
            {
                bool selectedPart = document.IsPartSelected(part.StableId);
                if (!selectedPart || !document.IsEffectivelyVisible(part.StableId) ||
                    !visuals.TryGetValue(part.StableId, out VisualNode visual) ||
                    !TryBounds(visual, out Bounds partBounds)) continue;
                if (!found)
                {
                    bounds = partBounds;
                    found = true;
                }
                else bounds.Encapsulate(partBounds);
            }
            return found;
        }

        internal bool TryGetBounds(IReadOnlyList<string> stableIds, out Bounds bounds)
        {
            ThrowIfDisposed();
            bounds = default;
            bool found = false;
            if (stableIds == null) return false;
            foreach (string stableId in stableIds)
            {
                if (!visuals.TryGetValue(stableId, out VisualNode visual) ||
                    !TryBounds(visual, out Bounds partBounds)) continue;
                if (found) bounds.Encapsulate(partBounds);
                else
                {
                    bounds = partBounds;
                    found = true;
                }
            }
            return found;
        }

        internal bool TryGetGizmoAnchors(
            IReadOnlyList<string> stableIds,
            out Vector3[] anchors,
            out int nativeAnchorStart)
        {
            ThrowIfDisposed();
            anchors = Array.Empty<Vector3>();
            nativeAnchorStart = AnchorAdjustment.SelectableAnchorCount;
            if (stableIds == null) return false;
            bool matches = cachedAnchorIds != null && cachedAnchorIds.Length == stableIds.Count;
            for (int index = 0; matches && index < stableIds.Count; ++index)
                matches = string.Equals(cachedAnchorIds[index], stableIds[index], StringComparison.Ordinal);
            if (matches)
            {
                anchors = (Vector3[])cachedGizmoAnchors.Clone();
                nativeAnchorStart = cachedNativeAnchorStart;
                return true;
            }
            if (!TryGetBounds(stableIds, out Bounds bounds)) return false;

            var result = new List<Vector3>(
                AnchorAdjustment.SelectableAnchorCount + 32);
            AddBoundsAnchors(bounds, result);
            nativeAnchorStart = result.Count;
            var snapSets = new List<IReadOnlyList<Point3>>();
            foreach (string stableId in stableIds)
            {
                if (!visuals.TryGetValue(stableId, out VisualNode visual) ||
                    visual.Locked || !visual.Root.activeInHierarchy) continue;
                var points = new Point3[visual.SnapLocal.Count];
                for (int index = 0; index < points.Length; ++index)
                    points[index] = ToGeometry(visual.Root.transform.TransformPoint(
                        visual.SnapLocal[index]));
                snapSets.Add(points);
            }
            // Use the same exterior points as whole-blueprint placement. Keep their
            // native indices even where they coincide with ordinary bounds anchors.
            foreach (Point3 point in AnchorAdjustment.ExternalCompositeSnapPoints(
                snapSets, tolerance: 0.0001))
            {
                if (result.Count - nativeAnchorStart >= MaximumNativeAnchors) break;
                result.Add(ToUnity(point));
            }
            cachedAnchorIds = new string[stableIds.Count];
            for (int index = 0; index < stableIds.Count; ++index)
                cachedAnchorIds[index] = stableIds[index];
            cachedGizmoAnchors = result.ToArray();
            cachedNativeAnchorStart = nativeAnchorStart;
            anchors = (Vector3[])cachedGizmoAnchors.Clone();
            return true;
        }

        private void InvalidateGizmoAnchors()
        {
            cachedAnchorIds = null;
            cachedGizmoAnchors = null;
        }

        internal bool TryFindEditorSnapTarget(
            IReadOnlyList<string> excludedStableIds,
            Vector2 mousePosition,
            bool includeMeshTargets,
            List<Vector3> previewTargets,
            List<bool> previewNative,
            out Vector3 target,
            out bool targetIsNative,
            Vector3? previousTarget = null,
            bool previousTargetIsNative = false)
        {
            ThrowIfDisposed();
            if (previewTargets == null) throw new ArgumentNullException(nameof(previewTargets));
            if (previewNative == null) throw new ArgumentNullException(nameof(previewNative));
            target = Vector3.zero;
            targetIsNative = false;
            previewTargets.Clear();
            previewNative.Clear();
            var excluded = new HashSet<string>(
                excludedStableIds ?? Array.Empty<string>(), StringComparer.Ordinal);
            if (previousTarget.HasValue)
            {
                Vector3 screen = camera.WorldToScreenPoint(previousTarget.Value);
                if (screen.z > 0f && camera.pixelRect.Contains(screen) &&
                    Vector2.Distance(mousePosition, screen) <= SnapReleaseScreenDistance &&
                    IsEditorPointVisible(previousTarget.Value, excludedStableIds))
                {
                    target = previousTarget.Value;
                    targetIsNative = previousTargetIsNative;
                    previewTargets.Add(target);
                    previewNative.Add(targetIsNative);
                    return true;
                }
            }
            var candidates = new List<SnapCandidate>();
            foreach (KeyValuePair<string, VisualNode> entry in visuals)
            {
                VisualNode visual = entry.Value;
                if (excluded.Contains(entry.Key) || !visual.Root.activeInHierarchy) continue;
                foreach (Vector3 localPoint in visual.SnapLocal)
                    AddSnapCandidate(
                        candidates,
                        mousePosition,
                        visual.Root.transform.TransformPoint(localPoint),
                        native: true);
                if (!includeMeshTargets || !TryBounds(visual, out Bounds bounds)) continue;
                var boundsAnchors = new List<Vector3>(AnchorAdjustment.SelectableAnchorCount);
                AddBoundsAnchors(bounds, boundsAnchors);
                foreach (Vector3 point in boundsAnchors)
                    AddSnapCandidate(candidates, mousePosition, point, native: false);
                foreach (PickSurface surface in visual.PickSurfaces)
                {
                    if (!surface.Renderer || !surface.Renderer.enabled ||
                        !surface.Renderer.gameObject.activeInHierarchy) continue;
                    foreach (Edge3 edge in surface.Edges)
                    {
                        Vector3 start = visual.Root.transform.TransformPoint(ToUnity(edge.Start));
                        Vector3 end = visual.Root.transform.TransformPoint(ToUnity(edge.End));
                        if (TransformGizmoView.TryClosestPointOnScreenSegment(
                            camera, mousePosition, start, end, out float screenDistance,
                            out float parameter) && screenDistance <= SnapPreviewScreenDistance)
                            AddSnapCandidate(candidates, mousePosition,
                                Vector3.Lerp(start, end, parameter), native: false, edge: true);
                    }
                }
            }

            candidates.Sort((left, right) => left.Distance.CompareTo(right.Distance));
            SnapCandidate? pointWinner = null;
            SnapCandidate? edgeWinner = null;
            foreach (SnapCandidate candidate in candidates)
            {
                if (!IsEditorPointVisible(candidate.Point, excludedStableIds)) continue;
                previewTargets.Add(candidate.Point);
                previewNative.Add(candidate.Native);
                if (!candidate.Edge && !pointWinner.HasValue &&
                    candidate.Distance < SnapTargetScreenDistance) pointWinner = candidate;
                if (candidate.Edge && !edgeWinner.HasValue &&
                    candidate.Distance < SnapEdgeScreenDistance) edgeWinner = candidate;
                if (previewTargets.Count == MaximumSnapPreviewTargets) break;
            }
            SnapCandidate? winner = pointWinner ?? edgeWinner;
            if (!winner.HasValue) return false;
            target = winner.Value.Point;
            targetIsNative = winner.Value.Native;
            return true;
        }

        private void AddSnapCandidate(
            IList<SnapCandidate> candidates,
            Vector2 mouse,
            Vector3 point,
            bool native,
            bool edge = false)
        {
            Vector3 screen = camera.WorldToScreenPoint(point);
            if (screen.z <= 0f || !camera.pixelRect.Contains(screen)) return;
            float distance = Vector2.Distance(mouse, screen);
            if (distance > SnapPreviewScreenDistance) return;
            for (int index = 0; index < candidates.Count; ++index)
            {
                SnapCandidate existing = candidates[index];
                if ((existing.Point - point).sqrMagnitude >= 0.0004f) continue;
                existing.Native |= native;
                existing.Edge &= edge;
                candidates[index] = existing;
                return;
            }
            candidates.Add(new SnapCandidate
            {
                Point = point, Native = native, Edge = edge, Distance = distance
            });
        }

        private static void AddBoundsAnchors(Bounds bounds, ICollection<Vector3> result)
        {
            AnchorBounds anchors = AnchorAdjustment.CreateBounds(new[]
            {
                new Point3(bounds.min.x, bounds.min.y, bounds.min.z),
                new Point3(bounds.max.x, bounds.max.y, bounds.max.z)
            });
            for (int index = 0; index < AnchorAdjustment.AnchorCount; ++index)
            {
                Point3 point = anchors.Anchor(index);
                result.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
            }
            result.Add(bounds.center);
        }

        internal bool TryGetDocumentBounds(out Bounds bounds)
        {
            return TryGetContentBounds(includeHidden: true, out bounds);
        }

        private bool TryGetContentBounds(bool includeHidden, out Bounds bounds)
        {
            ThrowIfDisposed();
            bounds = default;
            bool found = false;
            foreach (VisualNode visual in visuals.Values)
            {
                if (!TryBounds(visual, includeHidden, out Bounds nodeBounds))
                    continue;
                if (found) bounds.Encapsulate(nodeBounds);
                else
                {
                    bounds = nodeBounds;
                    found = true;
                }
            }
            return found;
        }

        internal void SetLighting(BlueprintEditorLightingPreset preset)
        {
            ThrowIfDisposed();
            lightingPreset = preset;
            renderIsolation?.SetPreset(preset);
            ConfigureStudioLighting();
            foreach (VisualNode visual in visuals.Values) ApplyAppearance(visual);
        }

        public void Dispose()
        {
            if (disposed) return;
            InvalidateGizmoAnchors();
            renderIsolation?.RestoreNow();
            if (root) root.SetActive(false);
            foreach (KeyValuePair<Camera, int> entry in worldCameraMasks)
            {
                if (!entry.Key) continue;
                int editorMask = 1 << editorLayer;
                int withoutEditor = entry.Value & ~editorMask;
                entry.Key.cullingMask = entry.Key.cullingMask == withoutEditor
                    ? entry.Value
                    : entry.Key.cullingMask | (entry.Value & editorMask);
            }
            worldCameraMasks.Clear();
            disposed = true;
            gizmo?.Hide();
            gizmo?.Dispose();
            ClearContourPreview();
            ClearDuplicatePreview();
            DestroyVisual(placementPreview);
            placementPreview = null;
            foreach (VisualNode visual in visuals.Values) DestroyVisual(visual);
            visuals.Clear();
            meshSnapshots.Clear();
            if (root) UnityEngine.Object.Destroy(root);
            foreach (UnityEngine.Object asset in ownedAssets)
                if (asset) UnityEngine.Object.Destroy(asset);
            ownedAssets.Clear();
        }

        private Camera CreateCamera(Camera template)
        {
            var cameraObject = new GameObject("EditorCamera")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            cameraObject.transform.SetParent(root.transform, false);
            Camera result = cameraObject.AddComponent<Camera>();
            result.cullingMask = 1 << editorLayer;
            result.clearFlags = CameraClearFlags.SolidColor;
            result.backgroundColor = new Color(0.115f, 0.155f, 0.195f, 1f);
            result.allowHDR = false;
            result.allowMSAA = true;
            result.orthographic = false;
            result.fieldOfView = template.fieldOfView;
            result.nearClipPlane = Mathf.Max(0.02f, template.nearClipPlane);
            result.farClipPlane = Mathf.Max(GroundExtent * 2f, template.farClipPlane);
            result.depth = template.depth + 100f;
            result.rect = template.rect;
            renderIsolation = cameraObject.AddComponent<BlueprintEditorRenderIsolation>();
            renderIsolation.Initialize(root.transform, editorLayer);
            return result;
        }

        private Light CreateStudioLight(string name, Vector3 eulerAngles)
        {
            var lightObject = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            lightObject.transform.SetParent(root.transform, false);
            lightObject.transform.localEulerAngles = eulerAngles;
            Light result = lightObject.AddComponent<Light>();
            result.type = LightType.Directional;
            result.cullingMask = 1 << editorLayer;
            result.shadows = LightShadows.None;
            return result;
        }

        private GameObject CreateGround()
        {
            var groundObject = new GameObject("Ground")
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            groundObject.transform.SetParent(root.transform, false);
            var mesh = new Mesh { name = "BuildWorks_BlueprintEditorGround" };
            mesh.vertices = new[]
            {
                new Vector3(-GroundExtent, -0.02f, -GroundExtent),
                new Vector3(-GroundExtent, -0.02f, GroundExtent),
                new Vector3(GroundExtent, -0.02f, GroundExtent),
                new Vector3(GroundExtent, -0.02f, -GroundExtent)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            groundObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            Material material = CreateColorMaterial("Ground", new Color(0.12f, 0.14f, 0.16f, 1f), false);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0f);
            if (material.HasProperty("_SpecularHighlights"))
            {
                material.SetFloat("_SpecularHighlights", 0f);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            }
            groundObject.AddComponent<MeshRenderer>().sharedMaterial = material;
            ownedAssets.Add(mesh);
            return groundObject;
        }

        private GameObject CreateGrid(string name, float extent, float spacing, Color color)
        {
            var grid = new GameObject(name)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            grid.transform.SetParent(root.transform, false);
            int lineCount = Mathf.FloorToInt(extent * 2f / spacing) + 1;
            var vertices = new Vector3[lineCount * 4];
            var indices = new int[vertices.Length];
            int vertex = 0;
            for (int line = 0; line < lineCount; ++line)
            {
                float offset = -extent + line * spacing;
                vertices[vertex] = new Vector3(-extent, 0f, offset);
                indices[vertex] = vertex++;
                vertices[vertex] = new Vector3(extent, 0f, offset);
                indices[vertex] = vertex++;
                vertices[vertex] = new Vector3(offset, 0f, -extent);
                indices[vertex] = vertex++;
                vertices[vertex] = new Vector3(offset, 0f, extent);
                indices[vertex] = vertex++;
            }
            var mesh = new Mesh { name = "BuildWorks_BlueprintEditor" + name };
            mesh.vertices = vertices;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            grid.AddComponent<MeshFilter>().sharedMesh = mesh;
            grid.AddComponent<MeshRenderer>().sharedMaterial =
                CreateColorMaterial(name, color, true);
            ownedAssets.Add(mesh);
            return grid;
        }

        private Material CreateColorMaterial(
            string name,
            Color color,
            bool transparent,
            bool overlay = false)
        {
            Shader shader = overlay
                ? FirstSupportedShader("Hidden/Internal-Colored")
                : transparent
                    ? FirstSupportedShader("Hidden/Internal-Colored", "Sprites/Default")
                    : FirstSupportedShader("Standard", "Legacy Shaders/Diffuse", "Unlit/Color");
            if (!shader) throw new InvalidOperationException("No supported editor shader is available.");
            var material = new Material(shader)
            {
                name = "BuildWorks_BlueprintEditor" + name + "Material",
                hideFlags = HideFlags.HideAndDontSave
            };
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_ZWrite"))
                material.SetInt("_ZWrite", transparent || overlay ? 0 : 1);
            if (material.HasProperty("_ZTest"))
                material.SetInt("_ZTest", (int)(overlay
                    ? CompareFunction.Always
                    : CompareFunction.LessEqual));
            if (transparent || overlay)
                material.renderQueue = overlay
                    ? (int)RenderQueue.Overlay
                    : (int)RenderQueue.Transparent;
            ownedAssets.Add(material);
            return material;
        }

        private VisualNode CreateVisual(
            BlueprintEditorPart part,
            GameObject source,
            bool selectable = true,
            bool captureSurfaces = false)
        {
            var result = new VisualNode
            {
                PrefabName = part.PrefabName,
                Placeholder = !source
            };
            try
            {
                result.Root = source
                    ? PlacementGhostPreviewView.CreateVisualClone(
                        source, "Part_" + part.StableId, editorLayer)
                    : CreatePlaceholder(part.StableId, result.OwnedAssets);
                result.Root.transform.SetParent(root.transform, false);
                if (source) result.Root.transform.localScale = source.transform.lossyScale;
                result.BaseScale = result.Root.transform.localScale;
                if (source) CaptureSnapPoints(
                    source.transform, result.SnapLocal,
                    result.PlacementLocal, result.PlacementLabels);
                result.Renderers = result.Root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in result.Renderers)
                {
                    Material[] sourceMaterials = renderer.sharedMaterials;
                    if (sourceMaterials.Length == 0) sourceMaterials = new Material[1];
                    var previewMaterials = new Material[sourceMaterials.Length];
                    for (int index = 0; index < sourceMaterials.Length; ++index)
                    {
                        Material preview = CreatePreviewMaterial(
                            sourceMaterials[index], !source, result.OwnedAssets);
                        previewMaterials[index] = preview;
                        result.Materials.Add(new PreviewMaterial
                        {
                            Material = preview,
                            BaseColor = ReadColor(sourceMaterials[index], !source)
                        });
                    }
                    renderer.sharedMaterials = previewMaterials;
                }
                if (selectable || captureSurfaces) CapturePickSurfaces(result);
                if (result.PlacementLocal.Count == 0) AddBoundsPlacementPoints(result);
                if (selectable) ApplyAppearance(result);
                return result;
            }
            catch
            {
                DestroyVisual(result);
                throw;
            }
        }

        private static void CaptureSnapPoints(
            Transform source,
            List<Vector3> points,
            List<Vector3> placementPoints,
            List<string> placementLabels)
        {
            // Read the same direct children as Piece.GetSnapPoints without invoking
            // its Harmony postfix, which creates midpoint GameObjects on the prefab.
            var nativeNames = new List<string>();
            foreach (Transform child in source)
                if (child.tag == "snappoint" && !child.name.StartsWith(
                    "BuildWorks_MidSnap_", StringComparison.Ordinal))
                {
                    points.Add(child.localPosition);
                    placementPoints.Add(child.localPosition);
                    nativeNames.Add(child.name);
                }
            if (placementPoints.Count > 0)
            {
                Bounds bounds = new Bounds(placementPoints[0], Vector3.zero);
                foreach (Vector3 point in placementPoints) bounds.Encapsulate(point);
                for (int index = 0; index < placementPoints.Count; ++index)
                    placementLabels.Add(PlacementPointLabel(
                        nativeNames[index], placementPoints[index], bounds));
            }
            if (points.Count < 2) return;
            var native = new Point3[points.Count];
            for (int index = 0; index < native.Length; ++index)
                native[index] = ToGeometry(points[index]);
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(native);
            // Match VanillaMidpointSnapPoints, retaining every original native point.
            for (int index = 0; index < edges.Count && index < 32; ++index)
            {
                Vector3 midpoint = ToUnity((edges[index].Start + edges[index].End) * 0.5);
                if (!points.Exists(point => (point - midpoint).sqrMagnitude < 0.00000001f))
                {
                    points.Add(midpoint);
                    placementPoints.Add(midpoint);
                    placementLabels.Add("СЕРЕДИНА");
                }
            }
        }

        private static string PlacementPointLabel(string name, Vector3 point, Bounds bounds)
        {
            string lower = (name ?? string.Empty).ToLowerInvariant();
            if (lower.Contains("bottom") || lower.Contains("down")) return "НИЗ";
            if (lower.Contains("top") || lower.Contains("up")) return "ВЕРХ";
            if (lower.Contains("center") || lower.Contains("middle")) return "ЦЕНТР";
            const float tolerance = 0.001f;
            var labels = new List<string>();
            if (bounds.size.y > tolerance)
            {
                if (Mathf.Abs(point.y - bounds.min.y) < tolerance) labels.Add("НИЗ");
                else if (Mathf.Abs(point.y - bounds.max.y) < tolerance) labels.Add("ВЕРХ");
            }
            if (bounds.size.x > tolerance)
            {
                if (Mathf.Abs(point.x - bounds.min.x) < tolerance) labels.Add("ЛЕВО");
                else if (Mathf.Abs(point.x - bounds.max.x) < tolerance) labels.Add("ПРАВО");
            }
            if (bounds.size.z > tolerance)
            {
                if (Mathf.Abs(point.z - bounds.min.z) < tolerance) labels.Add("НАЗАД");
                else if (Mathf.Abs(point.z - bounds.max.z) < tolerance) labels.Add("ВПЕРЁД");
            }
            return labels.Count > 0 ? string.Join(" · ", labels) : "ТОЧКА";
        }

        private static void AddBoundsPlacementPoints(VisualNode visual)
        {
            bool found = false;
            Bounds bounds = default;
            foreach (PickSurface surface in visual.PickSurfaces)
            {
                if (!surface.Renderer || !surface.Renderer.enabled ||
                    !surface.Renderer.gameObject.activeInHierarchy) continue;
                foreach (Vector3 vertex in surface.Vertices)
                {
                    if (found) bounds.Encapsulate(vertex);
                    else { bounds = new Bounds(vertex, Vector3.zero); found = true; }
                }
            }
            if (!found && TryBounds(visual, out Bounds worldBounds))
            {
                Vector3 worldMin = worldBounds.min, worldMax = worldBounds.max;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = visual.Root.transform.InverseTransformPoint(new Vector3(
                        (corner & 1) == 0 ? worldMin.x : worldMax.x,
                        (corner & 2) == 0 ? worldMin.y : worldMax.y,
                        (corner & 4) == 0 ? worldMin.z : worldMax.z));
                    if (found) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); found = true; }
                }
            }
            if (!found) return;
            Vector3 min = bounds.min, max = bounds.max, center = bounds.center;
            Vector3[] points =
            {
                new Vector3(center.x, min.y, center.z),
                new Vector3(min.x, min.y, min.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(min.x, min.y, max.z),
                center,
                new Vector3(center.x, max.y, center.z)
            };
            string[] labels =
            {
                "НИЗ · ЦЕНТР", "НИЗ · ЛЕВО · НАЗАД", "НИЗ · ПРАВО · НАЗАД",
                "НИЗ · ПРАВО · ВПЕРЁД", "НИЗ · ЛЕВО · ВПЕРЁД", "ЦЕНТР", "ВЕРХ · ЦЕНТР"
            };
            visual.PlacementLocal.AddRange(points);
            visual.PlacementLabels.AddRange(labels);
        }

        private GameObject CreatePlaceholder(
            string stableId,
            ICollection<UnityEngine.Object> visualAssets)
        {
            var placeholder = new GameObject("Missing_" + stableId)
            {
                hideFlags = HideFlags.HideAndDontSave,
                layer = editorLayer
            };
            Mesh mesh = CreateCubeMesh();
            placeholder.AddComponent<MeshFilter>().sharedMesh = mesh;
            placeholder.AddComponent<MeshRenderer>();
            visualAssets.Add(mesh);
            return placeholder;
        }

        private Material CreatePreviewMaterial(
            Material source,
            bool missing,
            ICollection<UnityEngine.Object> visualAssets)
        {
            Material material;
            if (source && source.shader && source.shader.isSupported)
                material = new Material(source);
            else
            {
                Shader shader = FirstSupportedShader(
                    "Standard", "Legacy Shaders/Diffuse", "Unlit/Texture", "Unlit/Color");
                if (!shader)
                    throw new InvalidOperationException(
                        "No supported preview shader is available.");
                material = new Material(shader);
            }
            material.name = "BuildWorks_BlueprintEditorPreviewMaterial";
            material.hideFlags = HideFlags.HideAndDontSave;
            if (source && source.HasProperty("_MainTex") && material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", source.GetTexture("_MainTex"));
                material.SetTextureScale("_MainTex", source.GetTextureScale("_MainTex"));
                material.SetTextureOffset("_MainTex", source.GetTextureOffset("_MainTex"));
            }
            if (source && source.HasProperty("_Cutoff") && material.HasProperty("_Cutoff"))
                material.SetFloat("_Cutoff", source.GetFloat("_Cutoff"));
            Color color = ReadColor(source, missing);
            if (!source || missing)
            {
                if (material.HasProperty("_Color")) material.SetColor("_Color", color);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            }
            visualAssets.Add(material);
            return material;
        }

        private static Color ReadColor(Material source, bool missing)
        {
            if (missing) return new Color(0.95f, 0.18f, 0.16f, 1f);
            if (!source) return Color.white;
            if (source.HasProperty("_BaseColor")) return source.GetColor("_BaseColor");
            if (source.HasProperty("_Color")) return source.GetColor("_Color");
            return Color.white;
        }

        private void ApplyAppearance(VisualNode visual)
        {
            bool selected = visual.Selected || visual.ActiveSelection;
            MaterialPropertyBlock properties = selected ? temporarySelectionHighlight ? hoverProperties : null
                : visual.Hovered ? hoverProperties : null;
            foreach (Renderer renderer in visual.Renderers)
                if (renderer) renderer.SetPropertyBlock(properties);
        }

        private void ConfigureStudioLighting()
        {
            if (!keyLight || !fillLight || !rimLight) return;
            if (lightingPreset == BlueprintEditorLightingPreset.Warm)
            {
                SetLight(keyLight, new Color(1f, 0.78f, 0.58f), 1.15f);
                SetLight(fillLight, new Color(0.52f, 0.66f, 1f), 0.42f);
                SetLight(rimLight, new Color(1f, 0.56f, 0.28f), 0.72f);
            }
            else if (lightingPreset == BlueprintEditorLightingPreset.Contour)
            {
                SetLight(keyLight, new Color(0.82f, 0.88f, 1f), 0.56f);
                SetLight(fillLight, new Color(0.40f, 0.52f, 0.72f), 0.18f);
                SetLight(rimLight, new Color(0.38f, 0.78f, 1f), 1.65f);
            }
            else
            {
                SetLight(keyLight, new Color(1f, 0.96f, 0.90f), 1.18f);
                SetLight(fillLight, new Color(0.64f, 0.76f, 0.94f), 0.62f);
                SetLight(rimLight, new Color(0.82f, 0.90f, 1f), 0.88f);
            }
        }

        private static void SetLight(Light light, Color color, float intensity)
        {
            light.color = color;
            light.intensity = intensity;
        }

        private static void ApplyPart(
            VisualNode visual,
            BlueprintEditorPart part,
            bool effectivelyVisible,
            bool effectivelyLocked)
        {
            TryUnityTransform(part, out Vector3 position, out Quaternion rotation);
            visual.Root.transform.SetLocalPositionAndRotation(position, rotation);
            visual.Root.transform.localScale = Vector3.Scale(
                visual.BaseScale,
                new Vector3((float)part.Scale.X, (float)part.Scale.Y, (float)part.Scale.Z));
            visual.Root.SetActive(effectivelyVisible);
            visual.Locked = effectivelyLocked;
        }

        private static Point3 ToGeometry(Vector3 value) =>
            new Point3(value.x, value.y, value.z);

        private static Vector3 ToUnity(Point3 value) =>
            new Vector3((float)value.X, (float)value.Y, (float)value.Z);

        private void UpdateHighlights(BlueprintEditorDocument document) =>
            UpdateHighlights(document, visuals);

        private void UpdateHighlights(
            BlueprintEditorDocument document,
            IReadOnlyDictionary<string, VisualNode> sourceVisuals)
        {
            List<string> active = ExpandedPartIds(
                document,
                string.IsNullOrEmpty(document.ActiveNodeId)
                    ? Array.Empty<string>()
                    : new[] { document.ActiveNodeId });
            var activeIds = new HashSet<string>(active, StringComparer.Ordinal);
            var selectedIds = new HashSet<string>(
                ExpandedPartIds(document, document.Selection), StringComparer.Ordinal);
            var hoveredIds = new HashSet<string>(
                string.IsNullOrEmpty(hoveredId)
                    ? Array.Empty<string>()
                    : ExpandedPartIds(document, new[] { hoveredId }),
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, VisualNode> entry in sourceVisuals)
            {
                VisualNode visual = entry.Value;
                visual.ActiveSelection = activeIds.Contains(entry.Key);
                visual.Selected = selectedIds.Contains(entry.Key);
                visual.Hovered = hoveredIds.Contains(entry.Key);
                ApplyAppearance(visual);
            }
        }

        private static List<string> ExpandedPartIds(
            BlueprintEditorDocument document,
            IReadOnlyList<string> nodeIds)
        {
            var result = new List<string>();
            foreach (BlueprintEditorPart part in document.Parts)
                if (document.IsPartSelected(part.StableId, nodeIds)) result.Add(part.StableId);
            return result;
        }

        private static bool TryBounds(VisualNode visual, out Bounds bounds)
        {
            return TryBounds(visual, includeHidden: false, out bounds);
        }

        private static bool TryBounds(
            VisualNode visual,
            bool includeHidden,
            out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            foreach (Renderer renderer in visual.Renderers)
            {
                if (!renderer || !renderer.enabled ||
                    !includeHidden && !renderer.gameObject.activeInHierarchy)
                    continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else
                {
                    bounds = renderer.bounds;
                    found = true;
                }
            }
            return found;
        }

        private void CapturePickSurfaces(VisualNode visual)
        {
            foreach (MeshFilter filter in visual.Root.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                AddPickSurface(visual, renderer, filter.sharedMesh, filter.transform);
            }
        }

        private void AddPickSurface(
            VisualNode visual,
            Renderer renderer,
            Mesh mesh,
            Transform meshTransform)
        {
            if (!renderer || !mesh || !meshTransform) return;
            if (!meshSnapshots.TryGetValue(mesh, out MeshSnapshot snapshot))
            {
                if (!BlueprintEditorMeshData.TryRead(
                    mesh, out Vector3[] meshVertices, out int[] meshTriangles, out string error))
                {
                    if (error != null) throw new InvalidOperationException(error);
                    return;
                }
                var geometryVertices = new Point3[meshVertices.Length];
                for (int index = 0; index < meshVertices.Length; ++index)
                    geometryVertices[index] = ToGeometry(meshVertices[index]);
                snapshot = new MeshSnapshot
                {
                    Vertices = meshVertices,
                    Triangles = meshTriangles,
                    Edges = AnchorAdjustment.ExtractFeatureEdges(geometryVertices, meshTriangles)
                };
                // Baked clones and placeholders belong to one visual; retaining their
                // unique mesh keys would keep dead geometry until the editor closes.
                if ((mesh.hideFlags & HideFlags.DontSave) == 0 &&
                    !visual.OwnedAssets.Contains(mesh))
                    meshSnapshots.Add(mesh, snapshot);
            }
            var vertices = new Vector3[snapshot.Vertices.Length];
            for (int index = 0; index < vertices.Length; ++index)
                vertices[index] = visual.Root.transform.InverseTransformPoint(
                    meshTransform.TransformPoint(snapshot.Vertices[index]));
            var edges = new Edge3[snapshot.Edges.Count];
            for (int index = 0; index < edges.Length; ++index)
            {
                Edge3 edge = snapshot.Edges[index];
                edges[index] = new Edge3(
                    ToGeometry(visual.Root.transform.InverseTransformPoint(
                        meshTransform.TransformPoint(ToUnity(edge.Start)))),
                    ToGeometry(visual.Root.transform.InverseTransformPoint(
                        meshTransform.TransformPoint(ToUnity(edge.End)))));
            }
            var surface = new PickSurface
            {
                Renderer = renderer,
                Vertices = vertices,
                Triangles = snapshot.Triangles,
                Edges = edges
            };
            visual.PickSurfaces.Add(surface);
        }

        private static bool TryMeshHit(VisualNode visual, Ray worldRay, out float distance)
        {
            return TryMeshHit(visual, worldRay, out distance, out _);
        }

        private static bool TryMeshHit(
            VisualNode visual,
            Ray worldRay,
            out float distance,
            out Vector3 normal)
        {
            distance = float.PositiveInfinity;
            normal = Vector3.up;
            if (visual.PickSurfaces.Count == 0) return false;
            Transform rootTransform = visual.Root.transform;
            Vector3 origin = rootTransform.InverseTransformPoint(worldRay.origin);
            Vector3 direction = rootTransform.InverseTransformVector(worldRay.direction);
            bool found = false;
            foreach (PickSurface surface in visual.PickSurfaces)
            {
                if (!surface.Renderer || !surface.Renderer.enabled ||
                    !surface.Renderer.gameObject.activeInHierarchy) continue;
                int[] triangles = surface.Triangles;
                Vector3[] vertices = surface.Vertices;
                for (int index = 0; index + 2 < triangles.Length; index += 3)
                {
                    if (!TryTriangleHit(
                        origin,
                        direction,
                        vertices[triangles[index]],
                        vertices[triangles[index + 1]],
                        vertices[triangles[index + 2]],
                        out float hit) || hit >= distance) continue;
                    distance = hit;
                    Vector3 first = rootTransform.TransformPoint(vertices[triangles[index]]);
                    Vector3 second = rootTransform.TransformPoint(vertices[triangles[index + 1]]);
                    Vector3 third = rootTransform.TransformPoint(vertices[triangles[index + 2]]);
                    normal = Vector3.Cross(second - first, third - first).normalized;
                    if (Vector3.Dot(normal, worldRay.direction) > 0f) normal = -normal;
                    found = true;
                }
            }
            return found;
        }

        private static bool TryTriangleHit(
            Vector3 origin,
            Vector3 direction,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            out float distance)
        {
            const float epsilon = 0.000001f;
            Vector3 edge1 = second - first;
            Vector3 edge2 = third - first;
            Vector3 cross = Vector3.Cross(direction, edge2);
            float determinant = Vector3.Dot(edge1, cross);
            if (Mathf.Abs(determinant) < epsilon)
            {
                distance = 0f;
                return false;
            }
            float inverse = 1f / determinant;
            Vector3 fromFirst = origin - first;
            float u = Vector3.Dot(fromFirst, cross) * inverse;
            if (u < 0f || u > 1f)
            {
                distance = 0f;
                return false;
            }
            Vector3 otherCross = Vector3.Cross(fromFirst, edge1);
            float v = Vector3.Dot(direction, otherCross) * inverse;
            if (v < 0f || u + v > 1f)
            {
                distance = 0f;
                return false;
            }
            distance = Vector3.Dot(edge2, otherCross) * inverse;
            return distance > epsilon;
        }

        private static bool ScreenRectIntersectsBounds(
            Camera view,
            Rect screenRect,
            Bounds bounds)
        {
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            bool found = false;
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 screen = view.WorldToScreenPoint(new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z));
                if (screen.z <= 0f) continue;
                found = true;
                minimum = Vector2.Min(minimum, screen);
                maximum = Vector2.Max(maximum, screen);
            }
            return found && minimum.x <= screenRect.xMax && maximum.x >= screenRect.xMin &&
                minimum.y <= screenRect.yMax && maximum.y >= screenRect.yMin;
        }

        private static bool TryUnityTransform(
            BlueprintEditorPart part,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = new Vector3(
                (float)part.Position.X,
                (float)part.Position.Y,
                (float)part.Position.Z);
            rotation = new Quaternion(
                (float)part.Rotation.X,
                (float)part.Rotation.Y,
                (float)part.Rotation.Z,
                (float)part.Rotation.W);
            return IsFinite(position.x) && IsFinite(position.y) && IsFinite(position.z) &&
                IsFinite(rotation.x) && IsFinite(rotation.y) && IsFinite(rotation.z) &&
                IsFinite(rotation.w);
        }

        private void RecenterEnvironment(Vector3 cameraPosition)
        {
            minorGrid.localPosition = new Vector3(
                Mathf.Round(cameraPosition.x / 10f) * 10f,
                groundHeight,
                Mathf.Round(cameraPosition.z / 10f) * 10f);
            majorGrid.localPosition = new Vector3(
                Mathf.Round(cameraPosition.x / 100f) * 100f,
                groundHeight,
                Mathf.Round(cameraPosition.z / 100f) * 100f);
            ground.localPosition = new Vector3(
                Mathf.Round(cameraPosition.x / 500f) * 500f,
                groundHeight,
                Mathf.Round(cameraPosition.z / 500f) * 500f);
        }

        private void UpdateGroundHeight()
        {
            groundHeight = TryGetContentBounds(includeHidden: true, out Bounds bounds)
                ? bounds.min.y
                : 0f;
            RecenterEnvironment(camera.transform.position);
        }

        private static Shader FirstSupportedShader(params string[] names)
        {
            foreach (string name in names)
            {
                Shader shader = Shader.Find(name);
                if (shader && shader.isSupported) return shader;
            }
            return null;
        }

        private static Mesh CreateCubeMesh()
        {
            var mesh = new Mesh { name = "BuildWorks_MissingPrefabPlaceholder" };
            mesh.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                new Vector3(0.5f, 0.5f, -0.5f), new Vector3(-0.5f, 0.5f, -0.5f),
                new Vector3(-0.5f, -0.5f, 0.5f), new Vector3(0.5f, -0.5f, 0.5f),
                new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
            };
            mesh.triangles = new[]
            {
                0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7,
                0, 1, 5, 0, 5, 4, 2, 3, 7, 2, 7, 6,
                1, 2, 6, 1, 6, 5, 3, 0, 4, 3, 4, 7
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int FindUnusedEditorLayer()
        {
            var used = new bool[32];
            foreach (Component component in Resources.FindObjectsOfTypeAll<Component>())
            {
                if (component && component.gameObject.scene.IsValid() &&
                    IsCameraRenderable(component))
                    used[component.gameObject.layer] = true;
            }
            for (int layer = 31; layer >= 8; --layer)
                if (!used[layer]) return layer;
            throw new InvalidOperationException("No unused editor layer is available.");
        }

        private static bool IsCameraRenderable(Component component)
        {
            if (component is Renderer || component is CanvasRenderer) return true;
            string typeName = component.GetType().FullName;
            return typeName == "UnityEngine.Terrain" ||
                typeName == "UnityEngine.Projector" ||
                typeName == "UnityEngine.VFX.VisualEffect";
        }

        private static void DestroyVisual(VisualNode visual)
        {
            if (visual == null) return;
            if (visual.Root)
            {
                visual.Root.SetActive(false);
                UnityEngine.Object.Destroy(visual.Root);
            }
            foreach (UnityEngine.Object asset in visual.OwnedAssets)
                if (asset) UnityEngine.Object.Destroy(asset);
            visual.OwnedAssets.Clear();
        }

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private void ThrowIfDisposed()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintEditorScene));
        }
    }
}
