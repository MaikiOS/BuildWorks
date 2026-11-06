using System;
using System.Collections.Generic;
using UnityEngine;

// Vector2/Vector3/Mathf are managed Unity math. No engine Camera/Input/Time or quaternion icalls run.
internal struct HostQuaternion
{
    private System.Numerics.Quaternion value;
    internal HostQuaternion(float x, float y, float z, float w) { value = new System.Numerics.Quaternion(x, y, z, w); }
    internal float x => value.X;
    internal float y => value.Y;
    internal float z => value.Z;
    internal float w => value.W;
    internal static HostQuaternion identity => new HostQuaternion { value = System.Numerics.Quaternion.Identity };
    internal static HostQuaternion Inverse(HostQuaternion q) => new HostQuaternion { value = System.Numerics.Quaternion.Inverse(q.value) };
    internal static HostQuaternion AngleAxis(float degrees, Vector3 axis) => new HostQuaternion {
        value = System.Numerics.Quaternion.CreateFromAxisAngle(
            System.Numerics.Vector3.Normalize(new System.Numerics.Vector3(axis.x, axis.y, axis.z)), degrees * (float)Math.PI / 180) };
    internal static float Angle(HostQuaternion a, HostQuaternion b)
    {
        float dot = Math.Abs(System.Numerics.Quaternion.Dot(
            System.Numerics.Quaternion.Normalize(a.value), System.Numerics.Quaternion.Normalize(b.value)));
        return (float)(2 * Math.Acos(Math.Min(1f, dot)) * 180 / Math.PI);
    }
    public static HostQuaternion operator *(HostQuaternion a, HostQuaternion b) =>
        new HostQuaternion { value = a.value * b.value };
    public static Vector3 operator *(HostQuaternion q, Vector3 p)
    {
        var r = System.Numerics.Vector3.Transform(new System.Numerics.Vector3(p.x, p.y, p.z), q.value);
        return new Vector3(r.X, r.Y, r.Z);
    }
}
internal sealed class HostCamera
{
    internal static readonly HostCamera main = new HostCamera();
    internal sealed class Pose { public Vector3 position; public Vector3 forward = Vector3.forward; }
    internal readonly Pose transform = new Pose();
    internal Vector3 WorldToScreenPoint(Vector3 point) => point;
    internal Ray ScreenPointToRay(Vector3 point) => new Ray(new Vector3(point.x, point.y, -10), Vector3.forward);
    public static implicit operator bool(HostCamera camera) => camera != null;
}
internal enum HostKeyCode { Tab, LeftShift, RightShift, LeftControl, RightControl, LeftAlt, RightAlt, X, Y, Z, W, A, D, E, Q, F, G, R, S, Escape, Return, KeypadEnter, Delete }
internal static class HostInput
{
    internal static Vector3 mousePosition;
    internal static bool Down, Held, Up, RightHeld, MiddleHeld;
    internal static readonly HashSet<HostKeyCode> Keys = new HashSet<HostKeyCode>();
    internal static readonly HashSet<HostKeyCode> DownKeys = new HashSet<HostKeyCode>();
    internal static string Text = "";
    internal static bool Focused = true;
    internal static bool GetKey(HostKeyCode key) => Keys.Contains(key);
    internal static bool GetKeyDown(HostKeyCode key) => DownKeys.Contains(key);
    internal static bool GetMouseButtonDown(int button) => button == 0 && Down;
    internal static bool GetMouseButton(int button) => button == 0 ? Held : button == 1 ? RightHeld : button == 2 && MiddleHeld;
    internal static bool GetMouseButtonUp(int button) => button == 0 && Up;
    internal static void Frame(Vector3 mouse, bool down, bool held, bool shift = false, bool up = false)
    {
        mousePosition = mouse; Down = down; Held = held; Up = up; RightHeld = MiddleHeld = false; Keys.Clear(); DownKeys.Clear(); Text = "";
        if (shift) Keys.Add(HostKeyCode.LeftShift);
        ++HostTime.frameCount;
    }
    internal static void Command(string text = "", params HostKeyCode[] keys)
    {
        Frame(mousePosition, false, false);
        Text = text;
        foreach (var key in keys) { Keys.Add(key); DownKeys.Add(key); }
    }
}
internal static class HostTime { internal static int frameCount; }
internal sealed class HostPiece
{
    internal string name = "wood_floor";
    internal sealed class Pose
    {
        public Vector3 position, localScale = Vector3.one;
        public HostQuaternion rotation = HostQuaternion.identity;
        public void SetPositionAndRotation(Vector3 p, HostQuaternion r) { position = p; rotation = r; }
    }
    internal readonly Pose transform = new Pose();
    internal readonly OstrixMods.BuildWorks.ZNetView View = new OstrixMods.BuildWorks.ZNetView();
    internal bool HasView = true;
    internal T GetComponent<T>() where T : class => HasView ? View as T : null;
    internal int GetInstanceID() => 42;
    public static implicit operator bool(HostPiece piece) => piece != null;
}
internal sealed class HostGameObject
{
    internal readonly HostPiece.Pose transform = new HostPiece.Pose();
    internal bool activeInHierarchy = true;
    internal Vector3[] CapturedAnchors;
    internal void SetActive(bool active) { activeInHierarchy = active; }
    public static implicit operator bool(HostGameObject value) => value != null;
}
internal struct HostRaycastHit { internal object collider; }
namespace OstrixMods.BuildWorks
{
    internal enum GizmoAxis { None, X, Y, Z }
    internal enum GizmoHandleKind { None, Move, MovePlane, Rotate, Scale, Layout }
    internal enum GizmoMode { Move, Guide, Repeat, Plane }
    internal enum GizmoFamily { Combined, Move, Rotate, Scale }
    internal sealed class TransformGizmoView
    {
        internal GizmoFamily Family;
        internal int Anchor = -1;
        internal Vector3 LastRotationHitPivot;
        internal float Scale = 1;
        internal int HitTestAnchor(HostCamera c, Vector3[] points, Vector2 mouse) => Anchor;
        internal GizmoAxis HitTestMove(HostCamera c, Vector3 p, HostQuaternion q, bool local, Vector2 m) => GizmoAxis.None;
        internal GizmoAxis HitTestLayout(HostCamera c, Vector3 p, HostQuaternion q, bool local, Vector2 m) => GizmoAxis.None;
        internal GizmoAxis HitTestRotation(HostCamera c, Vector3 p, HostQuaternion q, bool local, Vector2 m)
        { LastRotationHitPivot = p; return GizmoAxis.Y; }
        internal static Vector3 AxisVector(GizmoAxis axis, HostQuaternion q, bool local)
        {
            var direction = axis == GizmoAxis.X ? Vector3.right : axis == GizmoAxis.Y ? Vector3.up : axis == GizmoAxis.Z ? Vector3.forward : Vector3.zero;
            return local ? q * direction : direction;
        }
    }
    internal sealed class Player
    {
        internal static readonly Player m_localPlayer = new Player();
        internal HostPiece SelectedPiece;
        internal HostGameObject Ghost;
        internal HostPiece GetSelectedPiece() => SelectedPiece;
        public static implicit operator bool(Player value) => value != null;
    }
    internal static partial class CompositeBlueprintStore
    {
        internal static bool HasExplicitFrame(object blueprint) => true;
        internal sealed class Blueprint
        {
            internal readonly List<Part> parts = new List<Part>();
            internal List<VectorData> customSnapPoints;
            internal OstrixMods.BuildWorks.Geometry.Point3[][] ExtraLocalPoints;
        }
        internal sealed partial class Part
        {
            internal string stableId = "fixture-part", prefabName = "wood_floor", displayName = "Fixture floor", signText;
            internal VectorData position = new VectorData(Vector3.zero), scale = new VectorData(Vector3.one);
            internal VectorData? baseScale = null;
            internal QuaternionData rotation = new QuaternionData(HostQuaternion.identity);
            internal bool scaleIsAbsolute = false;
            internal ForeignPartData foreignData = null;
            internal int? materialSeed;
        }
        internal struct VectorData
        {
            internal float x, y, z;
            internal VectorData(Vector3 value) { x = value.x; y = value.y; z = value.z; }
            internal Vector3 ToVector3() => new Vector3(x, y, z);
        }
        internal struct QuaternionData
        {
            private readonly HostQuaternion value;
            internal QuaternionData(HostQuaternion rotation) { value = rotation; }
            internal HostQuaternion ToQuaternion() => value;
        }
        internal sealed class ForeignPartData
        {
            internal OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData ToGeometry() => null;
        }
    }
    internal sealed class ZNetView
    {
        internal bool m_syncInitialScale = true;
        public static implicit operator bool(ZNetView view) => view != null;
    }
    internal static class BlueprintPlacementFrame
    {
        internal static OstrixMods.BuildWorks.Geometry.Point3[][] SnapLocalSets(CompositeBlueprintStore.Blueprint blueprint, IReadOnlyList<HostPiece> pieces) =>
            blueprint.ExtraLocalPoints ?? new OstrixMods.BuildWorks.Geometry.Point3[pieces.Count][];
    }
    internal sealed class UnityBlueprintEditorInput : IBlueprintEditorInput
    {
        internal static readonly UnityBlueprintEditorInput Instance = new UnityBlueprintEditorInput();
        public Vector2 MousePosition => HostInput.mousePosition;
        public Vector2 MouseScrollDelta => Vector2.zero;
        public float UnscaledDeltaTime => 1f / 60;
        public bool HasFocus => HostInput.Focused;
        public string InputString => HostInput.Text;
        public bool GetKey(HostKeyCode key) => HostInput.GetKey(key);
        public bool GetKeyDown(HostKeyCode key) => HostInput.GetKeyDown(key);
        public bool GetMouseButton(int button) => HostInput.GetMouseButton(button);
        public bool GetMouseButtonDown(int button) => HostInput.GetMouseButtonDown(button);
        public bool GetMouseButtonUp(int button) => HostInput.GetMouseButtonUp(button);
        public float GetAxis(string axis) => 0;
        public bool TryWarpMousePosition(Vector2 position) => false;
    }
    internal sealed class HostPreviews { internal void Hide() { } }
    internal static class BuildWorksPlugin
    {
        internal sealed class KeySetting { internal HostKeyCode Value = HostKeyCode.Tab; }
        internal static readonly KeySetting BlueprintCompositeFrameKey = new KeySetting();
        internal static readonly KeySetting LockPrecisionKey = new KeySetting();
    }
    internal static class BuildWorksLocalization { internal static string Text(string key, params object[] args) => key; }
}
