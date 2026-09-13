#pragma warning disable 0618
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks.UiWorkbench
{
    public enum WorkbenchTool
    {
        Select,
        Transform,
        Array,
        Contour
    }

    internal sealed class WorkbenchPartMarker : MonoBehaviour
    {
        internal string StableId;
        internal string PrefabName;
        internal bool VanillaPreview;
    }

    public sealed class BlueprintEditorWorkbench : MonoBehaviour
    {
        private static readonly Color Background = Hex("21364A");
        private static readonly Color Panel = Hex("0B0D0E", 0.98f);
        private static readonly Color PanelRaised = Hex("151411", 0.99f);
        private static readonly Color Cell = Hex("211F1B", 0.99f);
        private static readonly Color Border = Hex("806037");
        private static readonly Color Bronze = Hex("D5A24C");
        private static readonly Color TextMain = Hex("EEE0C7");
        private static readonly Color TextMuted = Hex("B9AA92");
        private static readonly Color Blue = Hex("43A6E8");

        private readonly Dictionary<WorkbenchTool, Button> toolButtons =
            new Dictionary<WorkbenchTool, Button>();
        private readonly Dictionary<string, GameObject> parts =
            new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly Dictionary<string, Image> treeRows =
            new Dictionary<string, Image>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> prefabBaseScales =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly List<GameObject> selectedParts = new List<GameObject>();
        private readonly List<GameObject> hoverVisuals = new List<GameObject>();
        private readonly List<GameObject> transformVisuals = new List<GameObject>();
        private readonly List<GameObject> arrayPreviewVisuals = new List<GameObject>();
        private readonly List<GameObject> contourPreviewVisuals = new List<GameObject>();
        private readonly List<GameObject> contourGuideVisuals = new List<GameObject>();
        private readonly List<string> contourSupportIds = new List<string>();
        private readonly List<PartPose> dragStartPoses = new List<PartPose>();
        private readonly Dictionary<Color, Material> solidMaterials =
            new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> lineMaterials =
            new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> overlayMaterials =
            new Dictionary<Color, Material>();
        private Font font;
        private Transform sceneRoot;
        private Transform blueprintRoot;
        private Camera viewCamera;
        private Canvas canvas;
        private RectTransform viewportArea;
        private RectTransform inspectorBody;
        private RectTransform outlinerPanel;
        private RectTransform treeContent;
        private RectTransform tooltip;
        private Text tooltipText;
        private GameObject catalog;
        private Text statusText;
        private Text lightingText;
        private Text documentNameText;
        private Button undoButton;
        private Button redoButton;
        private Button saveButton;
        private BlueprintEditorDocument document;
#if UNITY_EDITOR
        private bool failNextSceneSyncForTests;
#endif
        private WorkbenchTool activeTool;
        private GameObject hoveredPart;
        private GameObject gizmoRoot;
        private Vector3[] gizmoAnchors = Array.Empty<Vector3>();
        private Vector3 cameraFocus = new Vector3(0.85f, 1.05f, 0f);
        private float cameraDistance = 9f;
        private float cameraPitch = 10f;
        private float cameraYaw = -10f;
        private bool cameraDragging;
        private Vector2 previousMouse;
        private DragKind dragKind;
        private int dragAxis = -1;
        private int dragAnchor = -1;
        private Vector2 dragStartMouse;
        private Vector2 dragScreenDirection;
        private float dragWorldUnitsPerPixel;
        private Vector3 dragPivot;
        private Vector3 dragAxisWorld;
        private Vector3 dragAnchorWorld;
        private Vector3 dragStartDirection;
        private Vector3 previewTranslation;
        private Quaternion previewRotation = Quaternion.identity;
        private float previewScale = 1f;
        private Plane dragPlane;
        private float gizmoWorldScale = 1f;
        private int lightingPreset;
        private bool localSpace = true;
        private bool showAllAnchors = true;
        private bool meshSnapEnabled;
        private int arrayCountX = 6;
        private int arrayCountY = 1;
        private Vector3 arrayStepX = Vector3.right;
        private Vector3 arrayStepY = Vector3.forward;
        private float arrayRotationDegrees;
        private float arrayScaleStepX = -0.1f;
        private float arrayScaleStepY;
        private bool suppressArrayPreview;
        private bool contourClosed;
        private float contourScaleStep = -0.01f;
        private string contourSeedSupportId;
        private bool suppressContourPreview;
        private bool initialized;

        private enum DragKind { None, Move, Rotate, Scale, Anchor }

        private readonly struct PartPose
        {
            public PartPose(Transform transform)
            {
                Transform = transform;
                Position = transform.position;
                Rotation = transform.rotation;
                Scale = transform.localScale;
            }

            public Transform Transform { get; }
            public Vector3 Position { get; }
            public Quaternion Rotation { get; }
            public Vector3 Scale { get; }
        }

        private struct EdgeInfo
        {
            public Vector3 Normal;
            public int Count;
            public bool IsCrease;
        }

        public Camera ViewCamera => viewCamera;
        public WorkbenchTool ActiveTool => activeTool;
        public int SelectedCount => document?.Selection.Count ?? 0;
        public string HoveredPartName => hoveredPart ? hoveredPart.name : null;
        public bool HasTransformGizmo => gizmoRoot && gizmoRoot.activeSelf;
        public int SelectablePartCount => parts.Count;
        public int VanillaPreviewPartCount
        {
            get
            {
                int count = 0;
                foreach (GameObject part in parts.Values)
                {
                    WorkbenchPartMarker marker = part.GetComponent<WorkbenchPartMarker>();
                    if (marker != null && marker.VanillaPreview) ++count;
                }
                return count;
            }
        }
        public int TreePartCount => document?.Parts.Count ?? 0;
        public bool HasTreeRow(string stableId) =>
            stableId != null && treeRows.ContainsKey(stableId);
        public bool UsesLocalSpace => localSpace;
        public int DocumentPartCount => document?.Parts.Count ?? 0;
        public int DocumentGroupCount => document?.Groups.Count ?? 0;
        public int ArrayPreviewCount => arrayPreviewVisuals.Count;
        public int ContourPreviewCount => contourPreviewVisuals.Count;
        public string CurrentStatus => statusText ? statusText.text : string.Empty;
        public bool DocumentIsDirty => document != null && document.IsDirty;
        public bool CanUndoDocument => document != null && document.CanUndo;
        public bool CanRedoDocument => document != null && document.CanRedo;
        public string ActivePartId => document?.ActiveNodeId;

        public static BlueprintEditorWorkbench Create()
        {
            var root = new GameObject("BuildWorks_UI_Workbench");
            BlueprintEditorWorkbench workbench = root.AddComponent<BlueprintEditorWorkbench>();
            workbench.Initialize();
            return workbench;
        }

        private void Awake() => Initialize();

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildViewport();
            BuildInterface();
            SetTool(WorkbenchTool.Select);
        }

        private void Update()
        {
            if (catalog.activeSelf)
            {
                if (Input.GetKeyDown(KeyCode.Escape)) CloseCatalog();
                return;
            }
            bool control = Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);
            if (control && Input.GetKeyDown(KeyCode.Z))
            {
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                    RedoDocument();
                else UndoDocument();
                return;
            }
            if (control && Input.GetKeyDown(KeyCode.Y))
            {
                RedoDocument();
                return;
            }
            if (control && Input.GetKeyDown(KeyCode.S))
            {
                SaveDocument(DefaultDocumentPath());
                return;
            }
            if (control && Input.GetKeyDown(KeyCode.D))
            {
                DuplicateSelection();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Delete))
            {
                DeleteSelection();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetTool(WorkbenchTool.Select);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetTool(WorkbenchTool.Transform);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetTool(WorkbenchTool.Array);
            if (Input.GetKeyDown(KeyCode.Alpha4)) SetTool(WorkbenchTool.Contour);
            if ((activeTool == WorkbenchTool.Array || activeTool == WorkbenchTool.Contour) &&
                (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)))
            {
                if (activeTool == WorkbenchTool.Array) ApplyArray();
                else ApplyContour();
                return;
            }
            if ((activeTool == WorkbenchTool.Array || activeTool == WorkbenchTool.Contour) &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                SetTool(WorkbenchTool.Transform);
                return;
            }
            if ((Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift)) &&
                Input.GetKeyDown(KeyCode.A))
            {
                OpenCatalog();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) && dragKind != DragKind.None)
                CancelDrag();
            else if (Input.GetKeyDown(KeyCode.Escape) && activeTool == WorkbenchTool.Select)
                ClearSelection();
            HandleViewportInput();
        }

        public void SetTool(WorkbenchTool tool)
        {
            CancelActiveManipulation(true);
            ClearArrayPreview();
            ClearContourPreview();
            activeTool = tool;
            foreach (KeyValuePair<WorkbenchTool, Button> pair in toolButtons)
            {
                ColorBlock colors = pair.Value.colors;
                colors.normalColor = pair.Key == tool ? Hex("5A3816") : Cell;
                colors.highlightedColor = pair.Key == tool ? Hex("71491E") : Hex("30383D");
                pair.Value.colors = colors;
            }

            for (int i = 0; i < transformVisuals.Count; ++i)
                transformVisuals[i].SetActive(tool != WorkbenchTool.Select);
            for (int i = 0; i < hoverVisuals.Count; ++i)
                hoverVisuals[i].SetActive(tool == WorkbenchTool.Select);
            if (tool == WorkbenchTool.Array) RebuildArrayPreview();
            if (tool == WorkbenchTool.Contour)
            {
                contourSupportIds.Clear();
                contourSeedSupportId = null;
            }
            RebuildInspector();
            RefreshStatus();
            RefreshTreeRows();
        }

        public bool SelectPart(string name, bool additive = false)
        {
            if (document == null || !parts.ContainsKey(name)) return false;
            return SelectNode(name, additive);
        }

        public bool SelectNode(string stableId, bool additive = false)
        {
            if (document == null) return false;
            bool selected = additive
                ? document.ToggleSelection(stableId)
                : document.SelectOnly(stableId);
            if (!selected) return false;
            SyncSelectedParts();
            SetHoveredObject(null);
            RefreshGizmo();
            SetTool(!additive && selectedParts.Count > 0
                ? WorkbenchTool.Transform : WorkbenchTool.Select);
            return true;
        }

        public bool SetHoveredPart(string name)
        {
            GameObject part = null;
            if (!string.IsNullOrEmpty(name) && !parts.TryGetValue(name, out part)) return false;
            SetHoveredObject(part);
            return true;
        }

        public void ClearSelection()
        {
            document?.ClearSelection();
            SyncSelectedParts();
            RefreshGizmo();
            RefreshTreeRows();
            RefreshStatus();
        }

        public bool TryPickPartAtWorldPoint(Vector3 worldPoint, out string partName)
        {
            Physics.SyncTransforms();
            bool found = TryPickPart(viewCamera.WorldToScreenPoint(worldPoint),
                out GameObject part);
            partName = found ? part.GetComponent<WorkbenchPartMarker>().StableId : null;
            return found;
        }

        private void HandleViewportInput()
        {
            Vector2 mouse = Input.mousePosition;
            bool inside = viewportArea && RectTransformUtility.RectangleContainsScreenPoint(
                viewportArea, mouse, viewCamera);
            if (inside && Input.mouseScrollDelta.y != 0f)
            {
                cameraDistance = Mathf.Clamp(
                    cameraDistance * Mathf.Pow(0.88f, Input.mouseScrollDelta.y), 4f, 40f);
                ApplyCamera();
                RefreshGizmo();
            }

            if (inside && dragKind == DragKind.None && Input.GetMouseButtonDown(2))
            {
                cameraDragging = true;
                previousMouse = mouse;
                SetHoveredObject(null);
            }
            if (cameraDragging && Input.GetMouseButton(2))
            {
                Vector2 delta = mouse - previousMouse;
                previousMouse = mouse;
                Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    float scale = cameraDistance * 0.0015f;
                    cameraFocus -= rotation * Vector3.right * delta.x * scale;
                    cameraFocus -= rotation * Vector3.up * delta.y * scale;
                }
                else
                {
                    cameraYaw += delta.x * 0.25f;
                    cameraPitch = Mathf.Clamp(cameraPitch - delta.y * 0.25f, -85f, 85f);
                }
                ApplyCamera();
            }
            if (Input.GetMouseButtonUp(2))
            {
                cameraDragging = false;
                RefreshGizmo();
            }
            if (cameraDragging) return;

            if (dragKind != DragKind.None)
            {
                if (Input.GetMouseButton(0)) PreviewDrag(mouse);
                if (Input.GetMouseButtonUp(0)) EndDrag();
                return;
            }

            if (!inside)
            {
                if (activeTool == WorkbenchTool.Select) SetHoveredObject(null);
                return;
            }
            if (Input.GetMouseButtonDown(1))
            {
                OpenCatalog();
                return;
            }

            if (activeTool != WorkbenchTool.Select && Input.GetMouseButtonDown(0) &&
                TryBeginDrag(mouse)) return;

            if (activeTool == WorkbenchTool.Contour && Input.GetMouseButtonDown(0))
            {
                if (TryPickPart(mouse, out GameObject support))
                    SelectContourSupport(
                        support.GetComponent<WorkbenchPartMarker>().StableId);
                else
                    statusText.text = "Контур: щёлкни по детали связанной опорной цепи.";
                return;
            }

            if (activeTool == WorkbenchTool.Select)
            {
                TryPickPart(mouse, out GameObject hit);
                SetHoveredObject(hit);
                if (Input.GetMouseButtonDown(0))
                {
                    if (hit) SelectPart(hit.GetComponent<WorkbenchPartMarker>().StableId,
                        Input.GetKey(KeyCode.LeftControl) ||
                        Input.GetKey(KeyCode.RightControl));
                    else ClearSelection();
                }
            }
        }

        private bool TryPickPart(Vector2 mouse, out GameObject part)
        {
            part = null;
            float nearest = float.PositiveInfinity;
            RaycastHit[] hits = Physics.RaycastAll(viewCamera.ScreenPointToRay(mouse), 200f);
            for (int i = 0; i < hits.Length; ++i)
            {
                WorkbenchPartMarker marker =
                    hits[i].collider.GetComponentInParent<WorkbenchPartMarker>();
                if (!marker || hits[i].distance >= nearest) continue;
                if (!parts.TryGetValue(marker.StableId, out GameObject candidate)) continue;
                nearest = hits[i].distance;
                part = candidate;
            }
            return part;
        }

        private bool TryBeginDrag(Vector2 mouse)
        {
            if (!TrySelectionBounds(out Bounds bounds)) return false;
            dragPivot = bounds.center;
            dragKind = DragKind.None;
            dragAxis = -1;
            dragAnchor = ClosestAnchor(mouse, 16f);
            Vector3 scaleHandle = dragPivot - ActiveAxis(1) * 1.45f * gizmoWorldScale;
            if (ScreenDistance(mouse, scaleHandle) <= 18f)
                dragKind = DragKind.Scale;
            else if (dragAnchor >= 0)
                dragKind = DragKind.Anchor;
            else
            {
                float best = 18f;
                for (int axis = 0; axis < 3; ++axis)
                {
                    Vector3 direction = ActiveAxis(axis);
                    float distance = ScreenDistanceToSegment(
                        mouse, dragPivot, dragPivot + direction * 1.45f * gizmoWorldScale);
                    if (distance >= best) continue;
                    best = distance;
                    dragKind = DragKind.Move;
                    dragAxis = axis;
                }
                if (dragKind == DragKind.None)
                {
                    for (int axis = 0; axis < 3; ++axis)
                    {
                        float distance = ScreenDistanceToRing(mouse, dragPivot, ActiveAxis(axis),
                            (0.78f + axis * 0.04f) * gizmoWorldScale);
                        if (distance >= best) continue;
                        best = distance;
                        dragKind = DragKind.Rotate;
                        dragAxis = axis;
                    }
                }
            }
            if (dragKind == DragKind.None) return false;

            dragStartPoses.Clear();
            for (int i = 0; i < selectedParts.Count; ++i)
                if (selectedParts[i]) dragStartPoses.Add(new PartPose(selectedParts[i].transform));
            dragStartMouse = mouse;
            previewTranslation = Vector3.zero;
            previewRotation = Quaternion.identity;
            previewScale = 1f;
            if (dragKind == DragKind.Move)
            {
                dragAxisWorld = ActiveAxis(dragAxis);
                Vector3 start = viewCamera.WorldToScreenPoint(dragPivot);
                Vector3 end = viewCamera.WorldToScreenPoint(
                    dragPivot + dragAxisWorld * 1.45f * gizmoWorldScale);
                Vector2 screenAxis = new Vector2(end.x - start.x, end.y - start.y);
                if (screenAxis.magnitude < 2f) return CancelDragStart();
                dragScreenDirection = screenAxis.normalized;
                dragWorldUnitsPerPixel = 1.45f * gizmoWorldScale / screenAxis.magnitude;
            }
            else if (dragKind == DragKind.Rotate)
            {
                dragAxisWorld = ActiveAxis(dragAxis);
                dragPlane = new Plane(dragAxisWorld, dragPivot);
                Ray ray = viewCamera.ScreenPointToRay(mouse);
                if (!dragPlane.Raycast(ray, out float distance)) return CancelDragStart();
                dragStartDirection = (ray.GetPoint(distance) - dragPivot).normalized;
                if (dragStartDirection.sqrMagnitude < 0.5f) return CancelDragStart();
            }
            else if (dragKind == DragKind.Anchor)
            {
                dragAnchorWorld = gizmoAnchors[dragAnchor];
                dragPlane = new Plane(viewCamera.transform.forward, dragAnchorWorld);
            }
            return true;
        }

        private bool CancelDragStart()
        {
            dragKind = DragKind.None;
            dragAxis = -1;
            dragAnchor = -1;
            dragStartPoses.Clear();
            return false;
        }

        private void PreviewDrag(Vector2 mouse)
        {
            RestoreDragStart();
            bool free = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            Vector3 translation = Vector3.zero;
            float degrees = 0f;
            float scale = 1f;
            if (dragKind == DragKind.Move)
            {
                float distance = Vector2.Dot(mouse - dragStartMouse, dragScreenDirection) *
                    dragWorldUnitsPerPixel;
                if (!free) distance = Mathf.Round(distance / 0.1f) * 0.1f;
                translation = dragAxisWorld * distance;
            }
            else if (dragKind == DragKind.Rotate)
            {
                Ray ray = viewCamera.ScreenPointToRay(mouse);
                if (dragPlane.Raycast(ray, out float distance))
                {
                    Vector3 direction = (ray.GetPoint(distance) - dragPivot).normalized;
                    degrees = Vector3.SignedAngle(dragStartDirection, direction, dragAxisWorld);
                }
                if (!free) degrees = Mathf.Round(degrees / 5f) * 5f;
            }
            else if (dragKind == DragKind.Scale)
            {
                scale = Mathf.Clamp(1f + (mouse.y - dragStartMouse.y) * 0.005f, 0.01f, 4f);
                if (!free) scale = Mathf.Round(scale * 100f) / 100f;
            }
            else
            {
                Ray ray = viewCamera.ScreenPointToRay(mouse);
                Vector3 target = dragAnchorWorld;
                if (dragPlane.Raycast(ray, out float distance)) target = ray.GetPoint(distance);
                if (!free && TryFindSnapTarget(mouse, out Vector3 snapped)) target = snapped;
                translation = target - dragAnchorWorld;
            }

            Quaternion rotation = dragKind == DragKind.Rotate
                ? Quaternion.AngleAxis(degrees, dragAxisWorld)
                : Quaternion.identity;
            previewTranslation = translation;
            previewRotation = rotation;
            previewScale = scale;
            for (int i = 0; i < dragStartPoses.Count; ++i)
            {
                PartPose pose = dragStartPoses[i];
                if (dragKind == DragKind.Rotate)
                {
                    pose.Transform.position = dragPivot + rotation * (pose.Position - dragPivot);
                    pose.Transform.rotation = rotation * pose.Rotation;
                }
                else if (dragKind == DragKind.Scale)
                {
                    pose.Transform.position = dragPivot + (pose.Position - dragPivot) * scale;
                    pose.Transform.localScale = pose.Scale * scale;
                }
                else pose.Transform.position = pose.Position + translation;
            }
            if (gizmoRoot && TrySelectionBounds(out Bounds bounds))
                gizmoRoot.transform.position = bounds.center;
            statusText.text = dragKind == DragKind.Rotate
                ? "Поворот: " + degrees.ToString("0.0") + "°     Shift — без шага"
                : dragKind == DragKind.Scale
                    ? "Масштаб: " + (scale * 100f).ToString("0") + "%     Диапазон 1–400%"
                    : "Смещение: X " + translation.x.ToString("0.00") + "  Y " +
                        translation.y.ToString("0.00") + "  Z " + translation.z.ToString("0.00") +
                        "     Shift — без привязки";
        }

        private void EndDrag()
        {
            Vector3 translation = previewTranslation;
            Quaternion rotation = previewRotation;
            float scale = previewScale;
            RestoreDragStart();
            ResetDragState();
            ApplyDocumentEdit(() => document.ApplyTransformDelta(
                ToPoint(translation),
                ToRotation(rotation),
                ToPoint(dragPivot),
                scale), "Трансформация применена.");
        }

        private void CancelDrag()
        {
            RestoreDragStart();
            ResetDragState();
            RefreshGizmo();
            RefreshStatus();
        }

        private void RestoreDragStart()
        {
            for (int i = 0; i < dragStartPoses.Count; ++i)
            {
                PartPose pose = dragStartPoses[i];
                if (!pose.Transform) continue;
                pose.Transform.position = pose.Position;
                pose.Transform.rotation = pose.Rotation;
                pose.Transform.localScale = pose.Scale;
            }
        }

        private void CancelActiveManipulation(bool restore)
        {
            cameraDragging = false;
            if (dragKind == DragKind.None) return;
            if (restore) RestoreDragStart();
            ResetDragState();
            RefreshGizmo();
        }

        private void ResetDragState()
        {
            dragKind = DragKind.None;
            dragAxis = -1;
            dragAnchor = -1;
            dragStartPoses.Clear();
            previewTranslation = Vector3.zero;
            previewRotation = Quaternion.identity;
            previewScale = 1f;
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) CancelActiveManipulation(true);
        }

        private int ClosestAnchor(Vector2 mouse, float limit)
        {
            int closest = -1;
            float best = limit;
            for (int i = 0; i < gizmoAnchors.Length; ++i)
            {
                float distance = ScreenDistance(mouse, gizmoAnchors[i]);
                if (distance >= best) continue;
                best = distance;
                closest = i;
            }
            return closest;
        }

        private bool TryFindSnapTarget(Vector2 mouse, out Vector3 target)
        {
            target = default;
            float best = 20f;
            foreach (GameObject part in parts.Values)
            {
                if (!part || selectedParts.Contains(part) ||
                    !TryRenderBounds(part, out Bounds bounds)) continue;
                Vector3[] anchors = PartAnchors(part, bounds, meshSnapEnabled);
                for (int i = 0; i < anchors.Length; ++i)
                {
                    float distance = ScreenDistance(mouse, anchors[i]);
                    if (distance >= best) continue;
                    best = distance;
                    target = anchors[i];
                }
            }
            return best < 20f;
        }

        private static Vector3[] BoundsAnchors(Bounds bounds)
        {
            AnchorBounds source = AnchorAdjustment.CreateBounds(new[]
            {
                ToPoint(bounds.min),
                ToPoint(bounds.max)
            });
            var result = new Vector3[AnchorAdjustment.SelectableAnchorCount];
            for (int index = 0; index < AnchorAdjustment.AnchorCount; ++index)
                result[index] = ToVector(source.Anchor(index));
            result[AnchorAdjustment.CenterAnchorIndex] = bounds.center;
            return result;
        }

        private static Vector3[] PartAnchors(
            GameObject part,
            Bounds bounds,
            bool includeBounds = true)
        {
            var result = includeBounds
                ? new List<Vector3>(BoundsAnchors(bounds))
                : new List<Vector3>();
            foreach (Transform child in part.GetComponentsInChildren<Transform>(true))
            {
                if (child.name.IndexOf("snappoint", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                Vector3 point = child.position;
                bool duplicate = false;
                foreach (Vector3 existing in result)
                    if ((existing - point).sqrMagnitude < 0.0004f)
                    {
                        duplicate = true;
                        break;
                    }
                if (!duplicate) result.Add(point);
            }
            return result.ToArray();
        }

        private float ScreenDistance(Vector2 mouse, Vector3 world)
        {
            Vector3 screen = viewCamera.WorldToScreenPoint(world);
            return screen.z <= 0f ? float.PositiveInfinity :
                Vector2.Distance(mouse, new Vector2(screen.x, screen.y));
        }

        private float ScreenDistanceToSegment(Vector2 mouse, Vector3 startWorld, Vector3 endWorld)
        {
            Vector3 start = viewCamera.WorldToScreenPoint(startWorld);
            Vector3 end = viewCamera.WorldToScreenPoint(endWorld);
            if (start.z <= 0f || end.z <= 0f) return float.PositiveInfinity;
            Vector2 a = new Vector2(start.x, start.y);
            Vector2 segment = new Vector2(end.x - start.x, end.y - start.y);
            float length = segment.sqrMagnitude;
            if (length < 0.001f) return float.PositiveInfinity;
            float t = Mathf.Clamp01(Vector2.Dot(mouse - a, segment) / length);
            return Vector2.Distance(mouse, a + segment * t);
        }

        private float ScreenDistanceToRing(Vector2 mouse, Vector3 center, Vector3 normal,
            float radius)
        {
            Vector3 tangent = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? Vector3.right : Vector3.up;
            Vector3 first = Vector3.Cross(normal, tangent).normalized;
            Vector3 second = Vector3.Cross(normal, first).normalized;
            float best = float.PositiveInfinity;
            Vector3 previous = center + first * radius;
            for (int i = 1; i <= 32; ++i)
            {
                float angle = i * Mathf.PI * 2f / 32f;
                Vector3 current = center + (first * Mathf.Cos(angle) +
                    second * Mathf.Sin(angle)) * radius;
                best = Mathf.Min(best, ScreenDistanceToSegment(mouse, previous, current));
                previous = current;
            }
            return best;
        }

        private Vector3 ActiveAxis(int index)
        {
            Vector3 axis = index == 0 ? Vector3.right :
                index == 1 ? Vector3.up : Vector3.forward;
            return localSpace && selectedParts.Count > 0 && selectedParts[0]
                ? selectedParts[0].transform.rotation * axis : axis;
        }

        private void ApplyCamera()
        {
            Quaternion rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
            viewCamera.transform.position = cameraFocus - rotation * Vector3.forward * cameraDistance;
            viewCamera.transform.rotation = rotation;
        }

        public void OpenCatalog()
        {
            CancelActiveManipulation(true);
            if (gizmoRoot) gizmoRoot.SetActive(false);
            catalog.SetActive(true);
            catalog.transform.SetAsLastSibling();
        }

        public void CloseCatalog()
        {
            catalog.SetActive(false);
            RefreshGizmo();
        }

        private void BuildViewport()
        {
            sceneRoot = new GameObject("WorkbenchScene").transform;
            sceneRoot.SetParent(transform, false);
            viewCamera = new GameObject("WorkbenchCamera", typeof(Camera)).GetComponent<Camera>();
            viewCamera.transform.SetParent(sceneRoot, false);
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
            viewCamera.backgroundColor = Background;
            viewCamera.fieldOfView = 43f;
            viewCamera.nearClipPlane = 0.05f;
            viewCamera.farClipPlane = 200f;
            ApplyCamera();

            var key = new GameObject("Key", typeof(Light)).GetComponent<Light>();
            key.transform.SetParent(sceneRoot, false);
            key.type = LightType.Directional;
            key.color = new Color(1f, 0.86f, 0.69f);
            key.intensity = 1.45f;
            key.transform.rotation = Quaternion.Euler(38f, -32f, 0f);
            var fill = new GameObject("Fill", typeof(Light)).GetComponent<Light>();
            fill.transform.SetParent(sceneRoot, false);
            fill.type = LightType.Directional;
            fill.color = new Color(0.45f, 0.68f, 1f);
            fill.intensity = 0.78f;
            fill.transform.rotation = Quaternion.Euler(25f, 145f, 0f);
            RenderSettings.ambientLight = new Color(0.28f, 0.32f, 0.37f);

            BuildGrid();
            BuildGate();
            CreateInitialDocument();
        }

        private void BuildGrid()
        {
            Material minor = LineMaterial(new Color(0.22f, 0.34f, 0.43f, 0.38f));
            Material major = LineMaterial(new Color(0.34f, 0.52f, 0.65f, 0.58f));
            for (int i = -25; i <= 25; ++i)
            {
                float coordinate = i;
                Color lineColor = i % 5 == 0 ? major.color : minor.color;
                Material material = i % 5 == 0 ? major : minor;
                AddLine("GridX", new Vector3(-25f, 0f, coordinate),
                    new Vector3(25f, 0f, coordinate), lineColor, 0.012f, material);
                AddLine("GridZ", new Vector3(coordinate, 0f, -25f),
                    new Vector3(coordinate, 0f, 25f), lineColor, 0.012f, material);
            }
        }

        private void BuildGate()
        {
            var gate = new GameObject("SampleBlueprint");
            gate.transform.SetParent(sceneRoot, false);
            blueprintRoot = gate.transform;
            CreatePart(gate.transform, "Столб_левый", "wood_pole",
                new Vector3(-2f, 0f, 0f), Vector3.one);
            CreatePart(gate.transform, "Столб_центральный", "wood_pole",
                Vector3.zero, Vector3.one);
            CreatePart(gate.transform, "Столб_правый", "wood_pole",
                new Vector3(2f, 0f, 0f), Vector3.one);
            CreatePart(gate.transform, "Балка_верхняя", "wood_beam",
                new Vector3(-1f, 2f, 0f), Vector3.one);
            CreatePart(gate.transform, "Брус_горизонтальный", "wood_beam",
                new Vector3(1f, 2f, 0f), Vector3.one);
            CreatePart(gate.transform, "Створка_левая", "woodwall",
                new Vector3(-2f, 0f, 0.08f), Vector3.one);
            CreatePart(gate.transform, "Створка_правая", "woodwall",
                new Vector3(0f, 0f, 0.08f), Vector3.one);
            CreatePart(gate.transform, "Створка_третья", "woodwall",
                new Vector3(2f, 0f, 0.08f), Vector3.one);
        }

        private void CreateInitialDocument()
        {
            var sourceParts = new List<BlueprintEditorPart>(parts.Count);
            foreach (KeyValuePair<string, GameObject> pair in parts)
            {
                Transform item = pair.Value.transform;
                WorkbenchPartMarker marker = pair.Value.GetComponent<WorkbenchPartMarker>();
                sourceParts.Add(new BlueprintEditorPart(
                    pair.Key,
                    marker.PrefabName,
                    pair.Value.name,
                    ToPoint(item.localPosition),
                    ToRotation(item.localRotation),
                    pair.Key.StartsWith("Створка_", StringComparison.Ordinal)
                        ? "group-gate" : "group-wall"));
            }
            document = new BlueprintEditorDocument(
                "workbench-sample",
                "Группа 1",
                "ПРОЧЕЕ",
                sourceParts,
                new[]
                {
                    new BlueprintEditorGroup("group-wall", "КАРКАС"),
                    new BlueprintEditorGroup("group-gate", "ВОРОТА")
                });
            document.MarkClean();
        }

        private void BuildGizmo(Bounds bounds)
        {
            Vector3 center = bounds.center;
            Quaternion orientation = localSpace && selectedParts.Count > 0 && selectedParts[0]
                ? selectedParts[0].transform.rotation : Quaternion.identity;
            gizmoWorldScale = Mathf.Clamp(
                Vector3.Distance(viewCamera.transform.position, center) / 12.5f, 0.35f, 3.2f);
            gizmoRoot = new GameObject("CombinedTransformGizmo");
            gizmoRoot.transform.SetParent(sceneRoot, false);
            gizmoRoot.transform.position = center;
            gizmoRoot.transform.rotation = orientation;
            transformVisuals.Add(gizmoRoot);
            AddGizmoLine("GizmoX", Vector3.zero,
                Vector3.right * 1.45f * gizmoWorldScale, Hex("F15B4A"), 0.055f * gizmoWorldScale);
            AddGizmoLine("GizmoY", Vector3.zero,
                Vector3.up * 1.45f * gizmoWorldScale, Hex("72C85B"), 0.055f * gizmoWorldScale);
            AddGizmoLine("GizmoZ", Vector3.zero,
                Vector3.forward * 1.45f * gizmoWorldScale, Hex("4B88FF"), 0.055f * gizmoWorldScale);
            AddGizmoRing("RotateX", Vector3.right, Hex("F15B4A"), 0.78f * gizmoWorldScale);
            AddGizmoRing("RotateY", Vector3.up, Hex("72C85B"), 0.82f * gizmoWorldScale);
            AddGizmoRing("RotateZ", Vector3.forward, Hex("4B88FF"), 0.86f * gizmoWorldScale);
            int nativeAnchorStart = AnchorAdjustment.SelectableAnchorCount;
            gizmoAnchors = selectedParts.Count == 1
                ? PartAnchors(selectedParts[0], bounds)
                : BoundsAnchors(bounds);
            for (int i = 0; i < gizmoAnchors.Length; ++i)
            {
                bool native = i >= nativeAnchorStart;
                Color color = native
                    ? new Color(1f, 0.62f, 0.12f, 0.82f)
                    : i == AnchorAdjustment.CenterAnchorIndex
                        ? new Color(1f, 0.4f, 0.8f, 0.68f)
                        : new Color(0.25f, 0.95f, 1f, 0.55f);
                AddGizmoAnchor("SnapPoint" + i, gizmoAnchors[i], color,
                    (native ? 0.115f : 0.075f) * gizmoWorldScale);
            }
            GameObject scale = GameObject.CreatePrimitive(PrimitiveType.Cube);
            scale.name = "UniformScaleHandle";
            scale.transform.SetParent(gizmoRoot.transform, false);
            scale.transform.localPosition = Vector3.down * 1.45f * gizmoWorldScale;
            scale.transform.localScale = Vector3.one * 0.18f * gizmoWorldScale;
            scale.GetComponent<Renderer>().sharedMaterial = OverlayMaterial(Color.white);
            DestroyImmediateSafe(scale.GetComponent<Collider>());
        }

        private void BuildInterface()
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem),
                typeof(StandaloneInputModule));
            eventSystem.transform.SetParent(transform, false);

            canvas = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler),
                typeof(GraphicRaycaster)).GetComponent<Canvas>();
            canvas.transform.SetParent(transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = viewCamera;
            canvas.planeDistance = 0.2f;
            CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(2560f, 1440f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;

            RectTransform root = PanelRect("SafeArea", canvas.transform, Color.clear);
            Stretch(root, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            root.GetComponent<Image>().raycastTarget = false;
            viewportArea = new GameObject("ViewportArea", typeof(RectTransform))
                .GetComponent<RectTransform>();
            viewportArea.SetParent(root, false);
            Stretch(viewportArea, Vector2.zero, Vector2.one,
                new Vector2(86f, 54f), new Vector2(-430f, -76f));
            RectTransform top = PanelRect("Top", root, Panel);
            Stretch(top, new Vector2(0f, 1f), Vector2.one,
                new Vector2(0f, -76f), Vector2.zero);
            BorderLine(top, false);
            BuildTopBar(top);

            RectTransform rail = PanelRect("ToolRail", root, Panel);
            Stretch(rail, Vector2.zero, new Vector2(0f, 1f), new Vector2(0f, 54f), new Vector2(86f, -76f));
            BorderLine(rail, true);
            BuildToolRail(rail);

            RectTransform right = PanelRect("RightColumn", root, Panel);
            Stretch(right, new Vector2(1f, 0f), Vector2.one, new Vector2(-430f, 54f), new Vector2(0f, -76f));
            BorderLine(right, true);
            BuildRightColumn(right);

            RectTransform status = PanelRect("Status", root, Panel);
            Stretch(status, Vector2.zero, new Vector2(1f, 0f), Vector2.zero, new Vector2(0f, 54f));
            BorderLine(status, false);
            statusText = Label("StatusText", status, "", 20, TextMain, TextAnchor.MiddleLeft);
            Stretch(statusText.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(26f, 0f), new Vector2(-20f, 0f));

            tooltip = PanelRect("Tooltip", root, Hex("080A0B", 0.98f));
            TopLeft(tooltip, 96f, 96f, 260f, 44f);
            tooltipText = Label("TooltipText", tooltip, "", 18, TextMain, TextAnchor.MiddleCenter);
            Stretch(tooltipText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            tooltip.gameObject.SetActive(false);

            BuildCatalog(root);
        }

        private void BuildTopBar(RectTransform top)
        {
            Button back = ButtonWithText("Back", top, "НАЗАД", 0, false);
            TopLeft(back.GetComponent<RectTransform>(), 18f, 10f, 160f, 56f);
            Text title = Label("Title", top, "✣  BUILDWORKS — РЕДАКТОР ЧЕРТЕЖА", 23,
                TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            TopLeft(title.rectTransform, 196f, 10f, 590f, 56f);
            documentNameText = Label("Name", top, "Группа 1", 21, Color.white,
                TextAnchor.MiddleCenter);
            TopCenter(documentNameText.rectTransform, 0f, 10f, 300f, 56f);

            float[] right = { -812f, -740f, -632f, -394f, -176f, -18f };
            string[] actions = { "ОТМ", "ПОВ", "ВИД", "СВЕТ: СТУДИЯ", "СОХРАНИТЬ", "ВЫЙТИ" };
            float[] widths = { 64f, 64f, 100f, 230f, 210f, 150f };
            for (int i = 0; i < actions.Length; ++i)
            {
                Button button = ButtonWithText("TopAction" + i, top, actions[i], 0, false);
                TopRight(button.GetComponent<RectTransform>(), right[i], 10f, widths[i], 56f);
                if (i == 3)
                {
                    lightingText = button.GetComponentInChildren<Text>();
                    button.onClick.AddListener(CycleLighting);
                }
                else if (i == 0)
                {
                    undoButton = button;
                    button.onClick.AddListener(() => UndoDocument());
                }
                else if (i == 1)
                {
                    redoButton = button;
                    button.onClick.AddListener(() => RedoDocument());
                }
                else if (i == 4)
                {
                    saveButton = button;
                    button.onClick.AddListener(() => SaveDocument(DefaultDocumentPath()));
                }
            }
            RefreshDocumentChrome();
        }

        private void BuildToolRail(RectTransform rail)
        {
            AddTool(rail, WorkbenchTool.Select, "select", "ВЫБОР ОБЪЕКТА", 30f);
            AddTool(rail, WorkbenchTool.Transform, "axes", "ОБЩАЯ ТРАНСФОРМАЦИЯ", 126f);
            AddTool(rail, WorkbenchTool.Array, "duplicate", "МАССИВ", 222f);
            AddTool(rail, WorkbenchTool.Contour, "snap", "КОНТУР", 318f);
        }

        private void AddTool(RectTransform rail, WorkbenchTool tool, string iconName,
            string hint, float y)
        {
            Button button = ButtonWithText(tool.ToString(), rail, "", 0, false);
            TopLeft(button.GetComponent<RectTransform>(), 13f, y, 60f, 60f);
            Texture2D icon = Resources.Load<Texture2D>("Icons/" + iconName);
            if (icon != null)
            {
                RawImage image = new GameObject("Icon", typeof(RectTransform),
                    typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(button.transform, false);
                image.texture = icon;
                image.color = TextMain;
                image.raycastTarget = false;
                Stretch(image.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(10f, 10f), new Vector2(-10f, -10f));
            }
            button.onClick.AddListener(() => SetTool(tool));
            AddTooltip(button.gameObject, hint, y + 18f);
            toolButtons.Add(tool, button);
        }

        private void BuildRightColumn(RectTransform right)
        {
            outlinerPanel = PanelRect("Outliner", right, PanelRaised);
            Stretch(outlinerPanel, new Vector2(0f, 0.44f), Vector2.one,
                new Vector2(0f, 2f), Vector2.zero);
            BorderLine(outlinerPanel, true);
            RectTransform inspector = PanelRect("Inspector", right, PanelRaised);
            Stretch(inspector, Vector2.zero, new Vector2(1f, 0.44f), Vector2.zero,
                new Vector2(0f, -2f));
            BorderLine(inspector, true);

            Text treeTitle = Label("TreeTitle", outlinerPanel, "ДЕРЕВО ОБЪЕКТОВ", 21,
                TextMain, TextAnchor.MiddleLeft, FontStyle.Bold);
            TopLeft(treeTitle.rectTransform, 20f, 8f, 300f, 42f);
            Button add = ButtonWithText("AddPart", outlinerPanel, "+", 28, true);
            TopRight(add.GetComponent<RectTransform>(), -64f, 7f, 48f, 42f);
            add.onClick.AddListener(OpenCatalog);
            Button addGroup = ButtonWithText("AddGroup", outlinerPanel, "G+", 16, false);
            TopRight(addGroup.GetComponent<RectTransform>(), -116f, 7f, 48f, 42f);
            addGroup.onClick.AddListener(() => CreateGroupFromSelection());
            Text search = Label("Search", PanelRect("SearchPanel", outlinerPanel, Cell),
                "Поиск...", 18, TextMuted, TextAnchor.MiddleLeft);
            TopLeft(search.transform.parent.GetComponent<RectTransform>(), 16f, 58f, 398f, 42f);
            Stretch(search.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(12f, 0f), new Vector2(-8f, 0f));

            RectTransform treeViewport = PanelRect("TreeViewport", outlinerPanel, Color.clear);
            Stretch(treeViewport, Vector2.zero, Vector2.one,
                new Vector2(12f, 12f), new Vector2(-12f, -112f));
            treeViewport.gameObject.AddComponent<RectMask2D>();
            treeContent = new GameObject("TreeContent", typeof(RectTransform))
                .GetComponent<RectTransform>();
            treeContent.SetParent(treeViewport, false);
            treeContent.anchorMin = new Vector2(0f, 1f);
            treeContent.anchorMax = Vector2.one;
            treeContent.pivot = new Vector2(0.5f, 1f);
            treeContent.anchoredPosition = Vector2.zero;
            treeContent.sizeDelta = Vector2.zero;
            ScrollRect scroll = treeViewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = treeViewport;
            scroll.content = treeContent;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;

            RebuildTree();

            Text inspectorTitle = Label("InspectorTitle", inspector,
                "ПАРАМЕТРЫ ИНСТРУМЕНТА", 21, TextMain, TextAnchor.MiddleLeft,
                FontStyle.Bold);
            TopLeft(inspectorTitle.rectTransform, 20f, 8f, 390f, 44f);
            inspectorBody = PanelRect("InspectorBody", inspector, Color.clear);
            Stretch(inspectorBody, Vector2.zero, Vector2.one,
                new Vector2(16f, 14f), new Vector2(-16f, -58f));
        }

        private void RebuildTree()
        {
            if (!outlinerPanel || document == null) return;
            treeRows.Clear();
            for (int index = treeContent.childCount - 1; index >= 0; --index)
                DestroyImmediateSafe(treeContent.GetChild(index).gameObject);

            float top = 0f;
            AddTreeRow("TreeRoot", "▾  ЧЕРТЁЖ: " + document.Name, null, top,
                0, true, false);
            top += 39f;
            foreach (BlueprintEditorGroup group in document.Groups)
                if (group.ParentGroupId == null) AddTreeBranch(group, 1, ref top);
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.ParentGroupId != null) continue;
                AddTreeRow("TreeRow_" + part.StableId, "   ├  " + part.DisplayName,
                    part.StableId, top, 1, part.Visible, part.Locked);
                top += 39f;
            }
            treeContent.sizeDelta = new Vector2(0f, top);
            RefreshTreeRows();
        }

        private void AddTreeBranch(
            BlueprintEditorGroup group, int depth, ref float top)
        {
            AddTreeRow("TreeGroup_" + group.StableId, "▾  " + group.Name,
                group.StableId, top, depth, group.Visible, group.Locked);
            top += 39f;
            foreach (BlueprintEditorGroup child in document.Groups)
                if (child.ParentGroupId == group.StableId)
                    AddTreeBranch(child, depth + 1, ref top);
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.ParentGroupId != group.StableId) continue;
                AddTreeRow("TreeRow_" + part.StableId, "├  " + part.DisplayName,
                    part.StableId, top, depth + 1, part.Visible, part.Locked);
                top += 39f;
            }
        }

        private void AddTreeRow(
            string name,
            string text,
            string stableId,
            float top,
            int depth,
            bool visible,
            bool locked)
        {
            RectTransform row = PanelRect(name, treeContent, Color.clear);
            TopLeft(row, 12f, top, 406f, 38f);
            Text label = Label("RowLabel", row, text, 17, TextMain, TextAnchor.MiddleLeft);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(10f + depth * 18f, 0f),
                stableId == null ? new Vector2(-5f, 0f) : new Vector2(-78f, 0f));
            if (stableId == null) return;
            treeRows[stableId] = row.GetComponent<Image>();
            Button select = row.gameObject.AddComponent<Button>();
            select.transition = Selectable.Transition.None;
            select.onClick.AddListener(() => SelectNode(stableId,
                Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)));
            Button eye = ButtonWithText("Visibility", row, visible ? "●" : "○", 16, false);
            TopRight(eye.GetComponent<RectTransform>(), -38f, 2f, 34f, 34f);
            eye.onClick.AddListener(() => ToggleNodeVisibility(stableId));
            Button lockButton = ButtonWithText("Lock", row, locked ? "L" : "U", 14, false);
            TopRight(lockButton.GetComponent<RectTransform>(), -2f, 2f, 34f, 34f);
            lockButton.onClick.AddListener(() => ToggleNodeLock(stableId));
        }

        private void SyncSelectedParts()
        {
            selectedParts.Clear();
            if (document == null) return;
            foreach (BlueprintEditorPart item in document.Parts)
                if (document.IsPartSelected(item.StableId) &&
                    document.IsEffectivelyVisible(item.StableId) &&
                    !document.IsEffectivelyLocked(item.StableId) &&
                    parts.TryGetValue(item.StableId, out GameObject part) && part)
                    selectedParts.Add(part);
        }

        private bool IsSelected(string stableId)
        {
            if (document == null) return false;
            foreach (string selectedId in document.Selection)
                if (selectedId == stableId) return true;
            return false;
        }

        private void SetHoveredObject(GameObject part)
        {
            if (hoveredPart == part) return;
            hoveredPart = part;
            ClearVisuals(hoverVisuals);
            if (part) AddMeshOutline(part);
            for (int i = 0; i < hoverVisuals.Count; ++i)
                hoverVisuals[i].SetActive(activeTool == WorkbenchTool.Select);
            RefreshTreeRows();
            if (activeTool == WorkbenchTool.Select) RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (!statusText) return;
            string dirty = document != null && document.IsDirty ? "НЕ СОХРАНЕНО     " : "";
            statusText.text = activeTool switch
            {
                WorkbenchTool.Select => dirty + "Наведено: " +
                    (hoveredPart ? hoveredPart.name : "—") + "     Выбрано: " +
                    SelectedCount + "     ЛКМ — выбрать  •  Ctrl+ЛКМ — несколько",
                WorkbenchTool.Transform => dirty + "Выбрано: " + SelectedCount +
                    "     Δ X  0,00   Y  0,00   Z  0,00     Поворот  0,0°     Масштаб  100%     Shift — отключить привязку",
                WorkbenchTool.Array => dirty + "Предпросмотр массива: " + ArrayPreviewCount +
                    " копий     Итог документа: " + (DocumentPartCount + ArrayPreviewCount) +
                    " / 128     Enter — применить  •  Esc — отменить",
                _ => dirty + (contourSupportIds.Count == 0
                    ? "Контур: ЛКМ по опоре — найти связанную цепь одинаковых деталей"
                    : "Контур: " + (contourClosed ? "кольцо" : "цепь") + " из " +
                        contourSupportIds.Count + " опор     Копий: " +
                        ContourPreviewCount + "     Enter — применить  •  Esc — отменить")
            };
            RefreshDocumentChrome();
        }

        private void RefreshTreeRows()
        {
            foreach (KeyValuePair<string, Image> pair in treeRows)
            {
                WorkbenchPartMarker marker = hoveredPart
                    ? hoveredPart.GetComponent<WorkbenchPartMarker>() : null;
                pair.Value.color = IsSelected(pair.Key) ? Hex("4B3017") :
                    marker != null && marker.StableId == pair.Key ? Hex("17364A") : Color.clear;
            }
        }

        public bool AddCatalogPart(int index)
        {
            if (index < 1 || index > 163 || document == null) return false;
            string prefabName = "catalog-piece-" + index.ToString("D3");
            Vector3 baseScale = CatalogBaseScale(index);
            prefabBaseScales[prefabName] = baseScale;
            string stableId = Guid.NewGuid().ToString("N");
            int column = document.Parts.Count % 5 - 2;
            bool added = ApplyDocumentEdit(() => document.AddPart(new BlueprintEditorPart(
                stableId,
                prefabName,
                "Деталь #" + index,
                new Point3(column * 1.1, baseScale.y * 0.5, 1.4),
                new Rotation3(0.0, 0.0, 0.0, 1.0))), "Деталь добавлена.");
            if (!added) return false;
            CloseCatalog();
            SetTool(WorkbenchTool.Transform);
            return true;
        }

        public bool DuplicateSelection() => ApplyDocumentEdit(
            () => document.DuplicateSelection(new Point3(0.5, 0.0, 0.5)),
            "Выбранные детали продублированы.");

        public bool DeleteSelection() => ApplyDocumentEdit(
            () => document.DeleteSelection(false),
            "Выбранные детали удалены.");

        public bool CreateGroupFromSelection()
        {
            if (document == null || document.MovableNodeSelectionCount < 2) return false;
            bool created = ApplyDocumentEdit(
                () => document.CreateGroup(
                    Guid.NewGuid().ToString("N"),
                    "ГРУППА " + (document.Groups.Count + 1)),
                "Группа создана.");
            if (created) SetTool(WorkbenchTool.Transform);
            return created;
        }

        public bool ToggleNodeVisibility(string stableId)
        {
            if (!TryNodeState(stableId, out bool visible, out _)) return false;
            return ApplyDocumentEdit(
                () => document.SetVisibility(stableId, !visible),
                visible ? "Объект скрыт." : "Объект показан.");
        }

        public bool ToggleNodeLock(string stableId)
        {
            if (!TryNodeState(stableId, out _, out bool locked)) return false;
            return ApplyDocumentEdit(
                () => document.SetLocked(stableId, !locked),
                locked ? "Объект разблокирован." : "Объект заблокирован.");
        }

        public bool RenameNode(string stableId, string name) => ApplyDocumentEdit(
            () => document.Rename(stableId, name),
            "Объект переименован.");

        public bool MoveSelectionToGroup(string groupId) => ApplyDocumentEdit(
            () => document.SetSelectionGroup(groupId),
            groupId == null ? "Объекты перенесены в корень." :
                "Объекты перенесены в группу.");

        public bool UngroupSelection() => ApplyDocumentEdit(
            document.UngroupSelection,
            "Группа разобрана.");

        private bool TryNodeState(
            string stableId, out bool visible, out bool locked)
        {
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.StableId != stableId) continue;
                visible = part.Visible;
                locked = part.Locked;
                return true;
            }
            foreach (BlueprintEditorGroup group in document.Groups)
            {
                if (group.StableId != stableId) continue;
                visible = group.Visible;
                locked = group.Locked;
                return true;
            }
            visible = false;
            locked = false;
            return false;
        }

        public bool UndoDocument() => document != null &&
            ApplyHistory(document.Undo, document.Redo, "Отмена выполнена.");

        public bool RedoDocument() => document != null &&
            ApplyHistory(document.Redo, document.Undo, "Повтор выполнен.");

        public bool ApplySelectedTransform(
            Vector3 translation, Quaternion rotation, float uniformScale)
        {
            if (!TrySelectionBounds(out Bounds bounds)) return false;
            return ApplyDocumentEdit(() => document.ApplyTransformDelta(
                ToPoint(translation),
                ToRotation(rotation),
                ToPoint(bounds.center),
                uniformScale), "Трансформация применена.");
        }

        public bool ConfigureArray(
            int countX,
            int countY,
            Vector3 stepX,
            Vector3 stepY,
            float rotationDegrees,
            float scaleStepX,
            float scaleStepY)
        {
            if (document == null) return false;
            try
            {
                if (scaleStepY != 0f)
                    throw new ArgumentException("Legacy diagnostic: second-axis scale is retired; use the actual editor's uniform row profile.");
                document.PreviewArray(countX, countY, ToPoint(stepX), ToPoint(stepY),
                    new Point3(0,1,0), rotationDegrees, 0, false, scaleStepX,
                    ToPoint(stepX * -.5f), ToPoint(stepX * .5f));
                arrayCountX = countX;
                arrayCountY = countY;
                arrayStepX = stepX;
                arrayStepY = stepY;
                arrayRotationDegrees = rotationDegrees;
                arrayScaleStepX = scaleStepX;
                arrayScaleStepY = scaleStepY;
                if (activeTool == WorkbenchTool.Array) RebuildArrayPreview();
                RebuildInspector();
                RefreshStatus();
                return true;
            }
            catch (Exception exception)
            {
                statusText.text = "Массив отклонён: " + exception.Message;
                RebuildInspector();
                return false;
            }
        }

        public bool ApplyArray()
        {
            if (document == null || activeTool != WorkbenchTool.Array) return false;
            suppressArrayPreview = true;
            bool applied;
            try
            {
                applied = ApplyDocumentEdit(() => document.ApplyArray(
                    arrayCountX, arrayCountY, ToPoint(arrayStepX), ToPoint(arrayStepY),
                    new Point3(0,1,0), arrayRotationDegrees, 0, false, arrayScaleStepX,
                    ToPoint(arrayStepX * -.5f), ToPoint(arrayStepX * .5f)),
                    "Массив применён.");
            }
            finally
            {
                suppressArrayPreview = false;
            }
            if (applied) SetTool(WorkbenchTool.Transform);
            else RebuildArrayPreview();
            return applied;
        }

        public bool SelectContourSupport(string stableId)
        {
            if (document == null || activeTool != WorkbenchTool.Contour) return false;
            BlueprintEditorPart seed = FindDocumentPart(stableId);
            if (seed == null || document.IsPartSelected(stableId))
            {
                statusText.text = "Контур: выбери опору, а не исходную деталь.";
                return false;
            }

            var supports = new List<BlueprintEditorPart>();
            int seedIndex = -1;
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.PrefabName == seed.PrefabName &&
                    document.IsEffectivelyVisible(part.StableId) &&
                    !document.IsPartSelected(part.StableId))
                {
                    if (part.StableId == stableId) seedIndex = supports.Count;
                    supports.Add(part);
                }
            if (seedIndex < 0 || supports.Count < 2)
            {
                statusText.text = "Контур: нужна цепь минимум из двух одинаковых опор.";
                return false;
            }
            double minX = double.PositiveInfinity;
            double maxX = double.NegativeInfinity;
            double minZ = double.PositiveInfinity;
            double maxZ = double.NegativeInfinity;
            foreach (BlueprintEditorPart part in supports)
            {
                minX = Math.Min(minX, part.Position.X);
                maxX = Math.Max(maxX, part.Position.X);
                minZ = Math.Min(minZ, part.Position.Z);
                maxZ = Math.Max(maxZ, part.Position.Z);
            }
            bool orderByX = maxX - minX >= maxZ - minZ;
            var connectionPoints = new Point3[supports.Count][];
            for (int index = 0; index < supports.Count; ++index)
            {
                if (!parts.TryGetValue(supports[index].StableId, out GameObject visual) ||
                    !TryRenderBounds(visual, out Bounds bounds)) return false;
                connectionPoints[index] = orderByX
                    ? new[]
                    {
                        ToPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z)),
                        ToPoint(new Vector3(bounds.max.x, bounds.center.y, bounds.center.z))
                    }
                    : new[]
                    {
                        ToPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.min.z)),
                        ToPoint(new Vector3(bounds.center.x, bounds.center.y, bounds.max.z))
                    };
            }
            IReadOnlyList<int> ordered = ConstructionLayout.OrderConnectedContour(
                connectionPoints, seedIndex, out bool closed);
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
                statusText.text = "Контур: эта опора не входит в простую цепь или кольцо.";
                return false;
            }

            contourSupportIds.Clear();
            foreach (int index in ordered)
                contourSupportIds.Add(supports[index].StableId);
            contourSeedSupportId = stableId;
            contourClosed = closed;
            return ConfigureContour(contourScaleStep);
        }

        public bool ConfigureContour(float scaleStep)
        {
            if (document == null || contourSupportIds.Count < 2) return false;
            try
            {
                document.PreviewContour(contourSupportIds, contourClosed, scaleStep);
                contourScaleStep = scaleStep;
                if (activeTool == WorkbenchTool.Contour) RebuildContourPreview();
                RebuildInspector();
                RefreshStatus();
                return true;
            }
            catch (Exception exception)
            {
                statusText.text = "Контур отклонён: " + exception.Message;
                RebuildInspector();
                return false;
            }
        }

        public bool ApplyContour()
        {
            if (document == null || activeTool != WorkbenchTool.Contour) return false;
            suppressContourPreview = true;
            bool applied;
            try
            {
                applied = ApplyDocumentEdit(() => document.ApplyContour(
                    contourSupportIds, contourClosed, contourScaleStep),
                    "Контур применён.");
            }
            finally
            {
                suppressContourPreview = false;
            }
            if (applied) SetTool(WorkbenchTool.Transform);
            else RebuildContourPreview();
            return applied;
        }

        public bool TryGetPartPose(
            string stableId, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            position = default;
            rotation = default;
            scale = default;
            if (document == null) return false;
            foreach (BlueprintEditorPart part in document.Parts)
            {
                if (part.StableId != stableId) continue;
                position = ToVector(part.Position);
                rotation = ToQuaternion(part.Rotation);
                scale = ToVector(part.Scale);
                return true;
            }
            return false;
        }

        public bool TryGetGroupParent(string stableId, out string parentGroupId)
        {
            parentGroupId = null;
            if (document == null) return false;
            foreach (BlueprintEditorGroup group in document.Groups)
            {
                if (group.StableId != stableId) continue;
                parentGroupId = group.ParentGroupId;
                return true;
            }
            return false;
        }

        public bool SaveDocument(string path)
        {
            if (document == null) return false;
            try
            {
                WorkbenchDocumentStore.Save(document, path);
                document.MarkClean();
                RefreshDocumentChrome();
                statusText.text = "Чертёж сохранён: " + Path.GetFileName(path);
                return true;
            }
            catch (Exception exception)
            {
                statusText.text = "Ошибка сохранения: " + exception.Message;
                return false;
            }
        }

        public bool ReopenDocument(string path)
        {
            BlueprintEditorDocument previous = document;
            try
            {
                BlueprintEditorDocument candidate = WorkbenchDocumentStore.Load(path);
                SetHoveredObject(null);
                document = candidate;
                SyncSceneFromDocument();
                statusText.text = "Сохранённый чертёж открыт повторно.";
                return true;
            }
            catch (Exception exception)
            {
                document = previous;
                SetHoveredObject(null);
                try
                {
                    SyncSceneFromDocument();
                }
                catch (Exception recoveryException)
                {
                    Debug.LogException(recoveryException);
                }
                statusText.text = "Ошибка открытия: " + exception.Message;
                return false;
            }
        }

        private bool ApplyDocumentEdit(Func<bool> edit, string success)
        {
            if (document == null || edit == null) return false;
            try
            {
                if (!edit()) return false;
                SyncSceneFromDocument();
                document.AcceptLastEdit();
                statusText.text = success;
                return true;
            }
            catch (Exception exception)
            {
                if (document.RollbackLastEdit())
                {
                    try
                    {
                        SyncSceneFromDocument();
                    }
                    catch (Exception recoveryException)
                    {
                        Debug.LogException(recoveryException);
                    }
                }
                statusText.text = "Команда отклонена: " + exception.Message;
                return false;
            }
        }

        private bool ApplyHistory(Func<bool> change, Func<bool> revert, string success)
        {
            bool changed = false;
            try
            {
                if (change == null || !(changed = change())) return false;
                SyncSceneFromDocument();
                statusText.text = success;
                return true;
            }
            catch (Exception exception)
            {
                if (changed && revert != null && revert())
                {
                    try
                    {
                        SyncSceneFromDocument();
                    }
                    catch (Exception recoveryException)
                    {
                        Debug.LogException(recoveryException);
                    }
                }
                statusText.text = "История отклонена: " + exception.Message;
                return false;
            }
        }

        private void SyncSceneFromDocument()
        {
#if UNITY_EDITOR
            if (failNextSceneSyncForTests)
            {
                failNextSceneSyncForTests = false;
                throw new InvalidOperationException("Injected scene sync failure.");
            }
#endif
            var live = new HashSet<string>(StringComparer.Ordinal);
            foreach (BlueprintEditorPart part in document.Parts) live.Add(part.StableId);
            var stale = new List<string>();
            foreach (KeyValuePair<string, GameObject> pair in parts)
                if (!live.Contains(pair.Key)) stale.Add(pair.Key);
            foreach (string stableId in stale)
            {
                if (hoveredPart == parts[stableId]) SetHoveredObject(null);
                parts[stableId].SetActive(false);
                DestroyImmediateSafe(parts[stableId]);
                parts.Remove(stableId);
            }

            foreach (BlueprintEditorPart part in document.Parts)
            {
                parts.TryGetValue(part.StableId, out GameObject visual);
                WorkbenchPartMarker marker = visual
                    ? visual.GetComponent<WorkbenchPartMarker>() : null;
                if (visual && (marker == null || marker.PrefabName != part.PrefabName))
                {
                    if (hoveredPart == visual) SetHoveredObject(null);
                    visual.SetActive(false);
                    DestroyImmediateSafe(visual);
                    parts.Remove(part.StableId);
                    visual = null;
                }
                if (!visual)
                    visual = CreateDocumentVisual(part);
                visual.name = part.DisplayName;
                visual.transform.localPosition = ToVector(part.Position);
                visual.transform.localRotation = ToQuaternion(part.Rotation);
                visual.transform.localScale = Vector3.Scale(
                    BaseScale(part.PrefabName), ToVector(part.Scale));
                visual.SetActive(document.IsEffectivelyVisible(part.StableId));
            }
            SyncSelectedParts();
            RebuildTree();
            RefreshGizmo();
            if (activeTool == WorkbenchTool.Array && !suppressArrayPreview)
                RebuildArrayPreview();
            if (activeTool == WorkbenchTool.Contour && !suppressContourPreview)
                RebuildContourPreview();
            RebuildInspector();
            RefreshStatus();
        }

        private GameObject CreateDocumentVisual(BlueprintEditorPart part)
        {
            GameObject vanilla = CreatePart(
                blueprintRoot,
                part.DisplayName,
                part.PrefabName,
                ToVector(part.Position),
                BaseScale(part.PrefabName),
                ToQuaternion(part.Rotation),
                part.StableId);
            if (vanilla.GetComponent<WorkbenchPartMarker>().VanillaPreview) return vanilla;

            int catalogIndex = CatalogIndex(part.PrefabName);
            Material material = catalogIndex > 0
                ? SolidMaterial(catalogIndex % 3 == 0 ? Hex("8A6B45") :
                    catalogIndex % 3 == 1 ? Hex("5D4330") : Hex("6C513A"))
                : SolidMaterial(Hex("4B3020"));
            parts.Remove(part.StableId);
            DestroyImmediateSafe(vanilla);
            return CreateCube(
                blueprintRoot,
                part.DisplayName,
                ToVector(part.Position),
                BaseScale(part.PrefabName),
                material,
                ToQuaternion(part.Rotation),
                true,
                part.StableId,
                part.PrefabName);
        }

        private GameObject CreatePart(Transform parent, string name, string prefabName,
            Vector3 position, Vector3 fallbackScale, Quaternion? rotation = null,
            string stableId = null)
        {
            GameObject source = Resources.Load<GameObject>("VanillaPreview/" + prefabName);
            if (!source)
                return CreateCube(parent, name, position, fallbackScale,
                    SolidMaterial(Hex("4B3020")), rotation, true, stableId, prefabName);

            GameObject result = Instantiate(source, parent, false);
            result.name = name;
            result.transform.localPosition = position;
            result.transform.localRotation = rotation ?? Quaternion.identity;
            result.transform.localScale = Vector3.one;
            string id = stableId ?? name;
            WorkbenchPartMarker marker = result.AddComponent<WorkbenchPartMarker>();
            marker.StableId = id;
            marker.PrefabName = prefabName;
            marker.VanillaPreview = true;
            parts[id] = result;
            prefabBaseScales[prefabName] = Vector3.one;
            return result;
        }

        private Vector3 BaseScale(string prefabName)
        {
            if (prefabBaseScales.TryGetValue(prefabName, out Vector3 scale)) return scale;
            int index = CatalogIndex(prefabName);
            scale = index > 0 ? CatalogBaseScale(index) : Vector3.one;
            prefabBaseScales[prefabName] = scale;
            return scale;
        }

        private static Vector3 CatalogBaseScale(int index) => new Vector3(
            0.65f + index % 4 * 0.12f,
            0.9f + index % 5 * 0.18f,
            0.35f + index % 3 * 0.08f);

        private static int CatalogIndex(string prefabName)
        {
            const string prefix = "catalog-piece-";
            return prefabName != null && prefabName.StartsWith(prefix, StringComparison.Ordinal) &&
                int.TryParse(prefabName.Substring(prefix.Length), out int index) ? index : 0;
        }

        private void RefreshDocumentChrome()
        {
            if (documentNameText)
                documentNameText.text = document.Name + (document.IsDirty ? "  *" : string.Empty);
            if (undoButton) undoButton.interactable = document.CanUndo;
            if (redoButton) redoButton.interactable = document.CanRedo;
            if (saveButton) saveButton.interactable = document.IsDirty;
        }

        private static string DefaultDocumentPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string repositoryRoot = Directory.GetParent(
                Directory.GetParent(projectRoot).FullName).FullName;
            return Path.Combine(repositoryRoot, "artifacts", "ui-workbench",
                "workbench-blueprint.json");
        }

        private static Point3 ToPoint(Vector3 value) =>
            new Point3(value.x, value.y, value.z);

        private static Vector3 ToVector(Point3 value) =>
            new Vector3((float)value.X, (float)value.Y, (float)value.Z);

        private static Rotation3 ToRotation(Quaternion value)
        {
            value = value.normalized;
            return new Rotation3(value.x, value.y, value.z, value.w);
        }

        private static Quaternion ToQuaternion(Rotation3 value) =>
            new Quaternion((float)value.X, (float)value.Y, (float)value.Z,
                (float)value.W).normalized;

        private void RefreshGizmo()
        {
            ClearVisuals(transformVisuals);
            gizmoRoot = null;
            gizmoAnchors = Array.Empty<Vector3>();
            if (TrySelectionBounds(out Bounds bounds)) BuildGizmo(bounds);
            for (int i = 0; i < transformVisuals.Count; ++i)
                transformVisuals[i].SetActive(activeTool != WorkbenchTool.Select);
        }

        private bool TrySelectionBounds(out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            for (int i = 0; i < selectedParts.Count; ++i)
            {
                if (!selectedParts[i] ||
                    !TryRenderBounds(selectedParts[i], out Bounds partBounds)) continue;
                if (!found) bounds = partBounds;
                else bounds.Encapsulate(partBounds);
                found = true;
            }
            return found;
        }

        private static bool TryRenderBounds(GameObject part, out Bounds bounds)
        {
            bounds = default;
            Renderer[] renderers = part.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            for (int i = 0; i < renderers.Length; ++i)
            {
                if (!renderers[i].enabled) continue;
                if (!found) bounds = renderers[i].bounds;
                else bounds.Encapsulate(renderers[i].bounds);
                found = true;
            }
            return found;
        }

        private static void ClearVisuals(List<GameObject> visuals)
        {
            for (int i = visuals.Count - 1; i >= 0; --i)
                DestroyImmediateSafe(visuals[i]);
            visuals.Clear();
        }

        private void ClearArrayPreview() => ClearVisuals(arrayPreviewVisuals);

        private void RebuildArrayPreview()
        {
            ClearArrayPreview();
            if (document == null || activeTool != WorkbenchTool.Array || suppressArrayPreview)
                return;
            try
            {
                IReadOnlyList<BlueprintEditorPart> preview = document.PreviewArray(
                    arrayCountX, arrayCountY, ToPoint(arrayStepX), ToPoint(arrayStepY),
                    new Point3(0,1,0), arrayRotationDegrees, 0, false, arrayScaleStepX,
                    ToPoint(arrayStepX * -.5f), ToPoint(arrayStepX * .5f));
                for (int i = 0; i < preview.Count; ++i)
                    arrayPreviewVisuals.Add(CreatePreviewVisual(preview[i]));
            }
            catch (Exception exception)
            {
                statusText.text = "Массив отклонён: " + exception.Message;
            }
        }

        private GameObject CreatePreviewVisual(BlueprintEditorPart part)
        {
            GameObject source = Resources.Load<GameObject>("VanillaPreview/" + part.PrefabName);
            GameObject visual = source ? Instantiate(source, blueprintRoot, false) :
                GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Preview_" + part.DisplayName;
            if (!source) visual.transform.SetParent(blueprintRoot, false);
            visual.transform.localPosition = ToVector(part.Position);
            visual.transform.localRotation = ToQuaternion(part.Rotation);
            visual.transform.localScale = Vector3.Scale(BaseScale(part.PrefabName), ToVector(part.Scale));
            WorkbenchPartMarker marker = visual.GetComponent<WorkbenchPartMarker>();
            if (marker) DestroyImmediateSafe(marker);
            Collider[] colliders = visual.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; ++i) colliders[i].enabled = false;
            var tint = new MaterialPropertyBlock();
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; ++i)
            {
                renderers[i].GetPropertyBlock(tint);
                tint.SetColor("_Color", new Color(1f, 0.68f, 0.28f, 1f));
                renderers[i].SetPropertyBlock(tint);
            }
            return visual;
        }

        private void ClearContourPreview()
        {
            ClearVisuals(contourPreviewVisuals);
            ClearVisuals(contourGuideVisuals);
        }

        private void RebuildContourPreview()
        {
            ClearContourPreview();
            if (document == null || activeTool != WorkbenchTool.Contour ||
                suppressContourPreview || contourSupportIds.Count < 2) return;
            try
            {
                IReadOnlyList<BlueprintEditorPart> preview = document.PreviewContour(
                    contourSupportIds, contourClosed, contourScaleStep);
                for (int i = 0; i < preview.Count; ++i)
                    contourPreviewVisuals.Add(CreatePreviewVisual(preview[i]));
                BuildContourGuides();
            }
            catch (Exception exception)
            {
                statusText.text = "Контур отклонён: " + exception.Message;
            }
        }

        private void BuildContourGuides()
        {
            if (contourSupportIds.Count < 2) return;
            var points = new List<Vector3>(contourSupportIds.Count);
            foreach (string stableId in contourSupportIds)
            {
                BlueprintEditorPart support = FindDocumentPart(stableId);
                if (support != null) points.Add(ToVector(support.Position) + Vector3.up * 0.08f);
            }
            int segmentCount = points.Count - 1 + (contourClosed ? 1 : 0);
            for (int index = 0; index < segmentCount; ++index)
            {
                Vector3 start = points[index];
                Vector3 end = points[(index + 1) % points.Count];
                contourGuideVisuals.Add(AddLine(
                    "ContourGuide", start, end, Bronze, 0.035f, OverlayMaterial(Bronze)));
            }
            for (int index = 0; index < points.Count; ++index)
            {
                GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                point.name = contourSupportIds[index] == contourSeedSupportId
                    ? "ContourSeed" : "ContourSupport";
                point.transform.SetParent(sceneRoot, false);
                point.transform.position = points[index];
                point.transform.localScale = Vector3.one *
                    (contourSupportIds[index] == contourSeedSupportId ? 0.22f : 0.14f);
                point.GetComponent<Renderer>().sharedMaterial = OverlayMaterial(
                    contourSupportIds[index] == contourSeedSupportId ? Color.white : Bronze);
                DestroyImmediateSafe(point.GetComponent<Collider>());
                contourGuideVisuals.Add(point);
            }
        }

        private void RebuildInspector()
        {
            if (inspectorBody == null) return;
            for (int i = inspectorBody.childCount - 1; i >= 0; --i)
                DestroyImmediateSafe(inspectorBody.GetChild(i).gameObject);

            if (activeTool == WorkbenchTool.Select)
            {
                AddInspectorHeading("ВЫБОР ОБЪЕКТА", 0f);
                AddInspectorLine("ЛКМ — выбрать объект", 56f);
                AddInspectorLine("Ctrl + ЛКМ — добавить к выбору", 98f);
                AddInspectorLine("Протащить по пустоте — рамка выбора", 140f);
                AddInspectorLine("Esc — снять выбор", 182f);
                AddInspectorMuted("После одиночного выбора автоматически откроется\nобщая трансформация.", 270f);
                return;
            }

            if (activeTool == WorkbenchTool.Transform)
            {
                BlueprintEditorPart active = ActiveDocumentPart();
                Vector3 position = active == null ? Vector3.zero : ToVector(active.Position);
                Vector3 euler = active == null ? Vector3.zero :
                    ToQuaternion(active.Rotation).eulerAngles;
                float scale = active == null ? 1f : (float)active.Scale.X;
                AddInspectorHeading("ОБЩАЯ ТРАНСФОРМАЦИЯ", 0f);
                AddVectorFields("ПОЛОЖЕНИЕ", position.x.ToString("0.00") + " м",
                    position.y.ToString("0.00") + " м",
                    position.z.ToString("0.00") + " м", 42f);
                AddVectorFields("ПОВОРОТ", euler.x.ToString("0.0") + "°",
                    euler.y.ToString("0.0") + "°",
                    euler.z.ToString("0.0") + "°", 126f);
                AddInspectorField("РАВНОМЕРНЫЙ МАСШТАБ",
                    (scale * 100f).ToString("0") + "%", 210f);
                AddTransformSpaceChoice(294f);
                Button points = ButtonWithText(
                    "AnchorVisibility", inspectorBody,
                    showAllAnchors ? "ТОЧКИ: ВСЕ" : "ТОЧКИ: РЯДОМ", 15, true);
                TopLeft(points.GetComponent<RectTransform>(), 0f, 378f, 194f, 42f);
                points.onClick.AddListener(() =>
                {
                    showAllAnchors = !showAllAnchors;
                    RefreshGizmo();
                    RebuildInspector();
                });
                Button magnet = ButtonWithText(
                    "MagnetMode", inspectorBody,
                    meshSnapEnabled ? "МАГНИТ: МЕШ" : "МАГНИТ: ИГРА", 15, true);
                TopLeft(magnet.GetComponent<RectTransform>(), 204f, 378f, 194f, 42f);
                magnet.onClick.AddListener(() =>
                {
                    meshSnapEnabled = !meshSnapEnabled;
                    RebuildInspector();
                });
                AddInspectorMuted(
                    "Шаг 0,10 м / 5° · голубые — границы · оранжевые — ванильные точки.",
                    432f);
                return;
            }

            if (activeTool == WorkbenchTool.Array)
            {
                AddInspectorHeading("МАССИВ", 0f);
                AddEditableFields("КОЛИЧЕСТВО X / Y",
                    new[] { arrayCountX.ToString(), arrayCountY.ToString() }, 32f,
                    UpdateArrayCount);
                AddEditableFields("ШАГ X · X / Y / Z, М",
                    new[] { Number(arrayStepX.x), Number(arrayStepX.y), Number(arrayStepX.z) },
                    100f, UpdateArrayStepX);
                AddEditableFields("ШАГ Y · X / Y / Z, М",
                    new[] { Number(arrayStepY.x), Number(arrayStepY.y), Number(arrayStepY.z) },
                    168f, UpdateArrayStepY);
                AddEditableFields("ПОВОРОТ НА ШАГ, °",
                    new[] { Number(arrayRotationDegrees) }, 236f, UpdateArrayRotation);
                AddEditableFields("МАСШТАБ НА ШАГ X / Y, %",
                    new[] { Number(arrayScaleStepX * 100f), Number(arrayScaleStepY * 100f) },
                    304f, UpdateArrayScale);
                AddInspectorLine("ПРЕДПРОСМОТР: " + ArrayPreviewCount + " КОПИЙ", 390f);
                AddInspectorMuted("Лимит документа: 128 деталей.", 420f);
                AddPrimaryButton("ПРИМЕНИТЬ МАССИВ", 450f).onClick.AddListener(() => ApplyArray());
                return;
            }

            AddInspectorHeading("КОНТУР", 0f);
            AddInspectorLine("ИСТОЧНИК: " +
                (ActiveDocumentPart()?.DisplayName ?? "—"), 48f);
            AddInspectorLine("ОПОРНАЯ ЦЕПЬ: " +
                (contourSupportIds.Count == 0
                    ? "НЕ ВЫБРАНА"
                    : contourSupportIds.Count + " ДЕТАЛИ"), 90f);
            AddInspectorMuted("ЛКМ по опоре — найти цепь одинаковых деталей.", 140f);
            AddEditableFields("МАСШТАБ НА ШАГ, %",
                new[] { Number(contourScaleStep * 100f) }, 214f, UpdateContourScale);
            AddInspectorLine("ПРЕДПРОСМОТР: " + ContourPreviewCount + " КОПИЙ", 298f);
            AddInspectorMuted("Локальная поза сохраняется для каждой опоры.", 340f);
            AddPrimaryButton("ПРИМЕНИТЬ КОНТУР", 406f).onClick.AddListener(() => ApplyContour());
        }

        private BlueprintEditorPart ActiveDocumentPart()
        {
            return FindDocumentPart(document?.ActiveNodeId);
        }

        private BlueprintEditorPart FindDocumentPart(string stableId)
        {
            if (document == null || stableId == null) return null;
            foreach (BlueprintEditorPart part in document.Parts)
                if (part.StableId == stableId) return part;
            return null;
        }

        private void BuildCatalog(RectTransform root)
        {
            catalog = PanelRect("CatalogOverlay", root, new Color(0f, 0f, 0f, 0.72f)).gameObject;
            Stretch(catalog.GetComponent<RectTransform>(), Vector2.zero, Vector2.one,
                Vector2.zero, Vector2.zero);
            RectTransform panel = PanelRect("Catalog", catalog.transform, PanelRaised);
            Center(panel, 0f, 0f, 1740f, 1030f);
            BorderLine(panel, true);
            Text title = Label("CatalogTitle", panel, "КАТАЛОГ ДЕТАЛЕЙ", 31,
                TextMain, TextAnchor.MiddleCenter, FontStyle.Bold);
            TopCenter(title.rectTransform, 0f, 12f, 520f, 58f);
            Button close = ButtonWithText("CatalogClose", panel, "×", 34, false);
            TopRight(close.GetComponent<RectTransform>(), -18f, 12f, 56f, 54f);
            close.onClick.AddListener(CloseCatalog);

            RectTransform searchPanel = PanelRect("CatalogSearch", panel, Cell);
            TopLeft(searchPanel, 292f, 82f, 790f, 50f);
            Text search = Label("Text", searchPanel, "⌕  Поиск по названию или #индексу",
                19, TextMuted, TextAnchor.MiddleLeft);
            Stretch(search.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(14f, 0f), new Vector2(-8f, 0f));
            string[] source = { "ВСЕ", "ВАНИЛЬНЫЕ", "МОДЫ" };
            for (int i = 0; i < source.Length; ++i)
            {
                Button sourceButton = ButtonWithText("Source" + i, panel, source[i], 17, i == 0);
                TopLeft(sourceButton.GetComponent<RectTransform>(), 1105f + i * 180f,
                    82f, 170f, 50f);
            }

            RectTransform filters = PanelRect("Filters", panel, Hex("12171A"));
            TopLeft(filters, 18f, 150f, 252f, 762f);
            string[] filterRows =
            {
                "КАТЕГОРИИ", "ДЕРЕВО                         69", "КРУГЛЯК                      17",
                "ТЁМНОЕ ДЕРЕВО           22", "ЯСЕНЬ                          25",
                "КАМЕНЬ                        16", "МЕТАЛЛ                        16",
                "СТЕКЛО                          13", "", "ТИП", "СТРОИТЕЛЬСТВО",
                "МЕБЕЛЬ", "ДЕКОР"
            };
            for (int i = 0; i < filterRows.Length; ++i)
            {
                if (filterRows[i].Length == 0) continue;
                RectTransform row = PanelRect("Filter" + i, filters,
                    i == 1 ? Hex("5A3816") : Color.clear);
                TopLeft(row, 8f, 12f + i * 54f, 236f, 48f);
                Text label = Label("Text", row, filterRows[i], i == 0 || i == 9 ? 19 : 17,
                    TextMain, TextAnchor.MiddleLeft, i == 0 || i == 9 ? FontStyle.Bold : FontStyle.Normal);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(10f, 0f), new Vector2(-6f, 0f));
            }

            RectTransform grid = PanelRect("Grid", panel, Color.clear);
            TopLeft(grid, 292f, 150f, 1418f, 762f);
            var layout = grid.gameObject.AddComponent<GridLayoutGroup>();
            layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            layout.constraintCount = 12;
            layout.cellSize = new Vector2(110f, 174f);
            layout.spacing = new Vector2(7f, 10f);
            layout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            for (int i = 1; i <= 48; ++i)
            {
                RectTransform card = PanelRect("Piece" + i, grid,
                    i == 1 ? Hex("47311D") : Cell);
                int catalogIndex = i;
                Button choose = card.gameObject.AddComponent<Button>();
                choose.transition = Selectable.Transition.None;
                choose.onClick.AddListener(() => AddCatalogPart(catalogIndex));
                Text index = Label("Index", card, i.ToString(), 15, Color.white,
                    TextAnchor.UpperLeft);
                Stretch(index.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(7f, 6f), new Vector2(-6f, -5f));
                RectTransform piece = PanelRect("Preview", card,
                    i % 3 == 0 ? Hex("8A6B45") : i % 3 == 1 ? Hex("5D4330") : Hex("6C513A"));
                Center(piece, 0f, 5f, 40f + i % 4 * 8f, 86f - i % 5 * 7f);
                piece.localRotation = Quaternion.Euler(0f, 0f, (i % 7 - 3) * 5f);
            }

            Text range = Label("Range", panel, "ИНДЕКС: 1–48 ИЗ 163", 19,
                TextMain, TextAnchor.MiddleLeft);
            TopLeft(range.rectTransform, 28f, 946f, 370f, 54f);
            Button previous = ButtonWithText("Previous", panel, "‹", 34, false);
            TopCenter(previous.GetComponent<RectTransform>(), -92f, 946f, 64f, 54f);
            Text page = Label("Page", panel, "1 / 4", 20, TextMain, TextAnchor.MiddleCenter);
            TopCenter(page.rectTransform, 0f, 946f, 90f, 54f);
            Button next = ButtonWithText("Next", panel, "›", 34, false);
            TopCenter(next.GetComponent<RectTransform>(), 92f, 946f, 64f, 54f);
            Text hint = Label("Hint", panel, "ЛКМ — выбрать  •  Esc — закрыть", 18,
                TextMuted, TextAnchor.MiddleRight);
            TopRight(hint.rectTransform, -28f, 946f, 520f, 54f);
            catalog.SetActive(false);
        }

        private void CycleLighting()
        {
            lightingPreset = (lightingPreset + 1) % 3;
            lightingText.text = lightingPreset == 0 ? "СВЕТ: СТУДИЯ" :
                lightingPreset == 1 ? "СВЕТ: ТЁПЛЫЙ" : "СВЕТ: КОНТУР";
            RenderSettings.ambientLight = lightingPreset == 0
                ? new Color(0.17f, 0.22f, 0.28f)
                : lightingPreset == 1
                    ? new Color(0.28f, 0.19f, 0.12f)
                    : new Color(0.08f, 0.14f, 0.22f);
        }

        private void AddInspectorHeading(string text, float y)
        {
            Text label = Label("Heading", inspectorBody, text, 20, TextMain,
                TextAnchor.MiddleLeft, FontStyle.Bold);
            TopLeft(label.rectTransform, 0f, y, 390f, 42f);
        }

        private void AddInspectorLine(string text, float y)
        {
            Text label = Label("Line", inspectorBody, text, 17, TextMain,
                TextAnchor.MiddleLeft);
            TopLeft(label.rectTransform, 0f, y, 398f, 38f);
        }

        private void AddInspectorMuted(string text, float y)
        {
            Text label = Label("Muted", inspectorBody, text, 16, TextMuted,
                TextAnchor.UpperLeft);
            TopLeft(label.rectTransform, 0f, y, 398f,
                text.IndexOf('\n') >= 0 ? 64f : 34f);
        }

        private void AddVectorFields(string title, string x, string y, string z, float top)
        {
            AddInspectorLine(title, top);
            string[] values = { "X  " + x, "Y  " + y, "Z  " + z };
            for (int i = 0; i < 3; ++i)
            {
                RectTransform field = PanelRect("VectorField", inspectorBody, Cell);
                TopLeft(field, i * 134f, top + 40f, 126f, 42f);
                Text label = Label("Value", field, values[i], 16,
                    i == 0 ? Hex("E76A52") : i == 1 ? Hex("8CC568") : Hex("63A5E8"),
                    TextAnchor.MiddleCenter);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
        }

        private void AddInspectorField(string title, string value, float top)
        {
            AddInspectorLine(title, top);
            RectTransform field = PanelRect("Field", inspectorBody, Cell);
            TopLeft(field, 0f, top + 40f, 398f, 42f);
            Text label = Label("Value", field, value, 17, TextMain, TextAnchor.MiddleCenter);
            Stretch(label.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        private void AddChoice(string title, string first, string second, float top,
            bool firstSelected = true, Action<bool> changed = null)
        {
            AddInspectorLine(title, top);
            Button left = ButtonWithText("ChoiceA", inspectorBody, first, 15, firstSelected);
            TopLeft(left.GetComponent<RectTransform>(), 0f, top + 40f, 194f, 42f);
            Button right = ButtonWithText("ChoiceB", inspectorBody, second, 15, !firstSelected);
            TopLeft(right.GetComponent<RectTransform>(), 204f, top + 40f, 194f, 42f);
            if (changed == null) return;
            left.onClick.AddListener(() => changed(true));
            right.onClick.AddListener(() => changed(false));
        }

        private void AddEditableFields(
            string title, string[] values, float top, Action<int, string> committed)
        {
            AddInspectorLine(title, top);
            float gap = 8f;
            float width = (398f - gap * (values.Length - 1)) / values.Length;
            for (int i = 0; i < values.Length; ++i)
            {
                RectTransform field = PanelRect("EditableField", inspectorBody, Cell);
                TopLeft(field, i * (width + gap), top + 40f, width, 42f);
                Text label = Label("Value", field, values[i], 17, TextMain,
                    TextAnchor.MiddleCenter);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(6f, 0f), new Vector2(-6f, 0f));
                InputField input = field.gameObject.AddComponent<InputField>();
                input.targetGraphic = field.GetComponent<Image>();
                input.textComponent = label;
                input.contentType = InputField.ContentType.DecimalNumber;
                input.text = values[i];
                int index = i;
                input.onEndEdit.AddListener(value => committed(index, value));
            }
        }

        private void UpdateArrayCount(int index, string value)
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out int parsed))
            {
                RejectArrayInput();
                return;
            }
            ConfigureArray(index == 0 ? parsed : arrayCountX,
                index == 1 ? parsed : arrayCountY, arrayStepX, arrayStepY,
                arrayRotationDegrees, arrayScaleStepX, arrayScaleStepY);
        }

        private void UpdateArrayStepX(int index, string value)
        {
            if (!TryNumber(value, out float parsed))
            {
                RejectArrayInput();
                return;
            }
            Vector3 step = arrayStepX;
            step[index] = parsed;
            ConfigureArray(arrayCountX, arrayCountY, step, arrayStepY,
                arrayRotationDegrees, arrayScaleStepX, arrayScaleStepY);
        }

        private void UpdateArrayStepY(int index, string value)
        {
            if (!TryNumber(value, out float parsed))
            {
                RejectArrayInput();
                return;
            }
            Vector3 step = arrayStepY;
            step[index] = parsed;
            ConfigureArray(arrayCountX, arrayCountY, arrayStepX, step,
                arrayRotationDegrees, arrayScaleStepX, arrayScaleStepY);
        }

        private void UpdateArrayRotation(int _, string value)
        {
            if (!TryNumber(value, out float parsed))
            {
                RejectArrayInput();
                return;
            }
            ConfigureArray(arrayCountX, arrayCountY, arrayStepX, arrayStepY,
                parsed, arrayScaleStepX, arrayScaleStepY);
        }

        private void UpdateArrayScale(int index, string value)
        {
            if (!TryNumber(value, out float parsed))
            {
                RejectArrayInput();
                return;
            }
            float step = parsed / 100f;
            ConfigureArray(arrayCountX, arrayCountY, arrayStepX, arrayStepY,
                arrayRotationDegrees,
                index == 0 ? step : arrayScaleStepX,
                index == 1 ? step : arrayScaleStepY);
        }

        private void RejectArrayInput()
        {
            statusText.text = "Массив отклонён: введи число.";
            RebuildInspector();
        }

        private void UpdateContourScale(int _, string value)
        {
            if (!TryNumber(value, out float parsed))
            {
                RejectContourInput();
                return;
            }
            ConfigureContour(parsed / 100f);
        }

        private void RejectContourInput()
        {
            statusText.text = "Контур отклонён: введи число.";
            RebuildInspector();
        }

        private static bool TryNumber(string value, out float result) =>
            float.TryParse(value?.Replace(',', '.'), NumberStyles.Float,
                CultureInfo.InvariantCulture, out result) && !float.IsNaN(result) &&
            !float.IsInfinity(result);

        private static string Number(float value) =>
            value.ToString("0.##", CultureInfo.CurrentCulture);

        private void AddTransformSpaceChoice(float top)
        {
            AddInspectorLine("ПРОСТРАНСТВО", top);
            Button local = ButtonWithText("LocalSpace", inspectorBody, "ЛОКАЛЬНОЕ", 15,
                localSpace);
            TopLeft(local.GetComponent<RectTransform>(), 0f, top + 40f, 194f, 42f);
            local.onClick.AddListener(() => SetTransformSpace(true));
            Button world = ButtonWithText("WorldSpace", inspectorBody, "МИРОВОЕ", 15,
                !localSpace);
            TopLeft(world.GetComponent<RectTransform>(), 204f, top + 40f, 194f, 42f);
            world.onClick.AddListener(() => SetTransformSpace(false));
        }

        public void SetTransformSpace(bool useLocal)
        {
            if (localSpace == useLocal) return;
            CancelActiveManipulation(true);
            localSpace = useLocal;
            RefreshGizmo();
            RebuildInspector();
            statusText.text = (useLocal ? "Локальные" : "Мировые") +
                " оси активны     Shift — отключить привязку";
        }

        private Button AddPrimaryButton(string text, float top)
        {
            Button button = ButtonWithText("Primary", inspectorBody, text, 17, true);
            TopLeft(button.GetComponent<RectTransform>(), 0f, top, 398f, 48f);
            return button;
        }

        private void AddTooltip(GameObject target, string text, float top)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            enter.callback.AddListener(_ =>
            {
                tooltipText.text = text;
                TopLeft(tooltip, 96f, top, Mathf.Max(220f, text.Length * 11f), 44f);
                tooltip.gameObject.SetActive(true);
                tooltip.SetAsLastSibling();
            });
            var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
            exit.callback.AddListener(_ => tooltip.gameObject.SetActive(false));
            trigger.triggers.Add(enter);
            trigger.triggers.Add(exit);
        }

        private Button ButtonWithText(string name, Transform parent, string text,
            int size, bool primary)
        {
            RectTransform rect = PanelRect(name, parent, primary ? Hex("563611") : Cell);
            Button button = rect.gameObject.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.normalColor = primary ? Hex("563611") : Cell;
            colors.highlightedColor = primary ? Hex("70491C") : Hex("31393E");
            colors.pressedColor = Hex("8A5D25");
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = primary ? Bronze : new Color(
                Border.r, Border.g, Border.b, 0.55f);
            outline.effectDistance = new Vector2(1f, -1f);
            if (!string.IsNullOrEmpty(text))
            {
                Text label = Label("Text", rect, text, size <= 0 ? 18 : size,
                    primary ? Color.white : TextMain, TextAnchor.MiddleCenter,
                    primary ? FontStyle.Bold : FontStyle.Normal);
                Stretch(label.rectTransform, Vector2.zero, Vector2.one,
                    new Vector2(4f, 0f), new Vector2(-4f, 0f));
            }
            return button;
        }

        private RectTransform PanelRect(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.color = color;
            return go.GetComponent<RectTransform>();
        }

        private Text Label(string name, Transform parent, string value, int size,
            Color color, TextAnchor anchor, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text label = go.GetComponent<Text>();
            label.font = font;
            label.text = value;
            label.fontSize = size;
            label.fontStyle = style;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max,
            Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void TopLeft(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void TopRight(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void TopCenter(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void Center(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private void BorderLine(RectTransform parent, bool full)
        {
            if (full)
            {
                Outline outline = parent.gameObject.AddComponent<Outline>();
                outline.effectColor = Border;
                outline.effectDistance = new Vector2(2f, -2f);
                return;
            }
            RectTransform line = PanelRect("Border", parent, Border);
            Stretch(line, Vector2.zero, new Vector2(1f, 0f), Vector2.zero,
                new Vector2(0f, 2f));
            line.GetComponent<Image>().raycastTarget = false;
        }

        private GameObject CreateCube(Transform parent, string name, Vector3 position,
            Vector3 scale, Material material, Quaternion? rotation = null,
            bool selectable = true, string stableId = null, string prefabName = null)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = position;
            cube.transform.localScale = scale;
            cube.transform.localRotation = rotation ?? Quaternion.identity;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            if (selectable)
            {
                string id = stableId ?? name;
                string prefab = prefabName ?? name;
                WorkbenchPartMarker marker = cube.AddComponent<WorkbenchPartMarker>();
                marker.StableId = id;
                marker.PrefabName = prefab;
                parts[id] = cube;
                prefabBaseScales[prefab] = scale;
            }
            return cube;
        }

        private void AddGizmoLine(string name, Vector3 start, Vector3 end, Color color,
            float width)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(gizmoRoot.transform, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = OverlayMaterial(color);
            line.startColor = line.endColor = color;
        }

        private void AddGizmoRing(string name, Vector3 normal, Color color, float radius)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(gizmoRoot.transform, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = 64;
            line.startWidth = line.endWidth = 0.025f * gizmoWorldScale;
            line.sharedMaterial = OverlayMaterial(color);
            Vector3 tangent = Mathf.Abs(Vector3.Dot(normal, Vector3.up)) > 0.9f
                ? Vector3.right : Vector3.up;
            Vector3 first = Vector3.Cross(normal, tangent).normalized;
            Vector3 second = Vector3.Cross(normal, first).normalized;
            for (int i = 0; i < line.positionCount; ++i)
            {
                float angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, (first * Mathf.Cos(angle) + second * Mathf.Sin(angle)) * radius);
            }
        }

        private void AddGizmoAnchor(string name, Vector3 position, Color color, float size)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            go.transform.SetParent(gizmoRoot.transform, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = 4;
            line.startWidth = line.endWidth = 0.008f * gizmoWorldScale;
            line.sharedMaterial = OverlayMaterial(color);
            line.startColor = line.endColor = color;
            Vector3 right = viewCamera.transform.right * size;
            Vector3 up = viewCamera.transform.up * size;
            line.SetPosition(0, position + up);
            line.SetPosition(1, position + right);
            line.SetPosition(2, position - up);
            line.SetPosition(3, position - right);
        }

        private GameObject AddLine(string name, Vector3 start, Vector3 end, Color color,
            float width, Material material = null)
        {
            var go = new GameObject(name, typeof(LineRenderer));
            if (sceneRoot) go.transform.SetParent(sceneRoot, false);
            LineRenderer line = go.GetComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, start);
            line.SetPosition(1, end);
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = material ?? LineMaterial(color);
            line.startColor = line.endColor = color;
            return go;
        }

        private void AddMeshOutline(GameObject part)
        {
            MeshFilter[] sources = part.GetComponentsInChildren<MeshFilter>(true);
            for (int sourceIndex = 0; sourceIndex < sources.Length; ++sourceIndex)
            {
                MeshFilter source = sources[sourceIndex];
                if (!source || !source.sharedMesh) continue;
                Mesh mesh = source.sharedMesh;
                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                var edges = new Dictionary<(Vector3, Vector3), EdgeInfo>();
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]];
                    Vector3 b = vertices[triangles[i + 1]];
                    Vector3 c = vertices[triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                    AddFeatureEdge(edges, a, b, normal);
                    AddFeatureEdge(edges, b, c, normal);
                    AddFeatureEdge(edges, c, a, normal);
                }
                foreach (KeyValuePair<(Vector3, Vector3), EdgeInfo> pair in edges)
                {
                    if (pair.Value.Count > 1 && !pair.Value.IsCrease) continue;
                    hoverVisuals.Add(AddLine("HoverMeshEdge",
                        source.transform.TransformPoint(pair.Key.Item1),
                        source.transform.TransformPoint(pair.Key.Item2), Blue, 0.025f));
                }
            }
        }

        private static void AddFeatureEdge(
            Dictionary<(Vector3, Vector3), EdgeInfo> edges,
            Vector3 first, Vector3 second, Vector3 normal)
        {
            (Vector3, Vector3) key = Compare(first, second) <= 0
                ? (first, second) : (second, first);
            if (!edges.TryGetValue(key, out EdgeInfo info))
                info = new EdgeInfo { Normal = normal };
            else if (Vector3.Dot(info.Normal, normal) < 0.999f)
                info.IsCrease = true;
            ++info.Count;
            edges[key] = info;
        }

        private static int Compare(Vector3 first, Vector3 second)
        {
            int x = first.x.CompareTo(second.x);
            if (x != 0) return x;
            int y = first.y.CompareTo(second.y);
            return y != 0 ? y : first.z.CompareTo(second.z);
        }

        private Material SolidMaterial(Color color)
        {
            if (solidMaterials.TryGetValue(color, out Material existing)) return existing;
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.18f);
            solidMaterials[color] = material;
            return material;
        }

        private Material LineMaterial(Color color)
        {
            if (lineMaterials.TryGetValue(color, out Material existing)) return existing;
            Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color };
            lineMaterials[color] = material;
            return material;
        }

        private Material OverlayMaterial(Color color)
        {
            if (overlayMaterials.TryGetValue(color, out Material existing)) return existing;
            Shader shader = Shader.Find("Hidden/Internal-Colored") ??
                Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            var material = new Material(shader) { color = color, renderQueue = 4000 };
            if (material.HasProperty("_ZTest")) material.SetInt("_ZTest", 8);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", 0);
            overlayMaterials[color] = material;
            return material;
        }

        private void OnDestroy()
        {
            foreach (Material material in solidMaterials.Values) DestroyImmediateSafe(material);
            foreach (Material material in lineMaterials.Values) DestroyImmediateSafe(material);
            foreach (Material material in overlayMaterials.Values) DestroyImmediateSafe(material);
            solidMaterials.Clear();
            lineMaterials.Clear();
            overlayMaterials.Clear();
        }

        private static Color Hex(string value, float alpha = 1f)
        {
            ColorUtility.TryParseHtmlString("#" + value, out Color color);
            color.a = alpha;
            return color;
        }

        private static void DestroyImmediateSafe(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
