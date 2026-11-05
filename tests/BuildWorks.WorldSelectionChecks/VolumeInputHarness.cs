using System;

// Run.ps1 injects the actual input handler; substitutes model clicks and plane hits only.
public sealed class VolumeInputChecks
{
    private bool blueprintSelectionDragging, blueprintVolumeDragValid, blueprintVolumeResizing;
    private float blueprintVolumePlane = 0, blueprintVolumeBottom = -2, blueprintVolumeTop = 20, blueprintVolumeDragPlane;
    private Vector3 blueprintVolumeDragOffset;
    private bool blueprintVolumeReady = true, blueprintVolumeDirty = false;
    private Vector2 blueprintSelectionStart;
    private Vector3 blueprintVolumeDragFirst, blueprintVolumeFirst = new Vector3(11), blueprintVolumeSecond = new Vector3(22);
    private string blueprintVolumeStatus = "previous";
    private int suppressPlacementFrame, toggles, refreshes, previews, cancellations;
    private string status;

    // PRODUCTION_METHOD

    private bool MouseOverEditorPanel(Vector2 mouse) => false;
    private bool TryBlueprintAreaPoint(Camera camera, Vector2 mouse, out Vector3 point)
    { point = new Vector3(mouse.x / camera.Scale, blueprintVolumeDragPlane, mouse.y / camera.Scale); return camera.PlaneHit; }
    private bool TryGetPlacedPieceAt(Camera camera, Vector2 mouse, out Piece piece, out int hit)
    { piece = new Piece(); hit = 0; return true; }
    private bool IsBlueprintPieceSelectable(Piece piece) => true;
    private void ToggleBlueprintVolumePiece(Piece piece, bool remove) { ++toggles; }
    private void CancelBlueprintSelectionDrag()
    { ++cancellations; blueprintSelectionDragging = blueprintVolumeDragValid = blueprintVolumeResizing = false; }
    private int BlueprintVolume(Vector3 first, Vector3 second) => 0;
    private void ShowBlueprintVolume(int volume) { ++previews; }
    private void RefreshBlueprintVolumeSelection() { ++refreshes; }
    private void ShowStatus(Player player, string value) { status = value; }

    private static bool IsFinite(Vector3 point) => !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z);

    public static string Run()
    {
        var camera = new Camera { PlaneHit = false };
        var click = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        click.HandleBlueprintVolumeInput(camera, new Vector2(0));
        Require(click.blueprintSelectionDragging && !click.blueprintVolumeDragValid && click.suppressPlacementFrame == Time.frameCount,
            "A visible-piece click was rejected because its ray misses the horizontal plane.");
        Input.Down = Input.Held = false; Input.Up = true;
        click.HandleBlueprintVolumeInput(camera, new Vector2(0));
        Require(click.toggles == 1 && click.refreshes == 0 && click.status == null && click.cancellations == 1 &&
            click.blueprintVolumeFirst.x == 11 && click.blueprintVolumeSecond.x == 22 && !click.blueprintVolumeDirty,
            "Short click must pick the visible piece without changing the area or reporting a plane error.");

        var drag = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        drag.HandleBlueprintVolumeInput(camera, new Vector2(0));
        Input.Down = false;
        drag.HandleBlueprintVolumeInput(camera, new Vector2(10));
        Require(drag.previews == 0, "Invalid start ray produced an area preview.");
        Input.Held = false; Input.Up = true;
        drag.HandleBlueprintVolumeInput(camera, new Vector2(10));
        Require(drag.toggles == 0 && drag.refreshes == 0 && drag.status == "selection.area_ray_failed" &&
            drag.cancellations == 1 && drag.blueprintVolumeReady && !drag.blueprintVolumeDirty &&
            drag.blueprintVolumeFirst.x == 11 && drag.blueprintVolumeSecond.x == 22 && drag.blueprintVolumeStatus == "previous",
            "Invalid area drag must preserve the applied volume and selection.");

        camera.PlaneHit = true;
        var valid = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        valid.HandleBlueprintVolumeInput(camera, new Vector2(0));
        Input.Down = false;
        valid.HandleBlueprintVolumeInput(camera, new Vector2(10));
        Input.Held = false; Input.Up = true;
        valid.HandleBlueprintVolumeInput(camera, new Vector2(10));
        Require(valid.refreshes == 1 && valid.previews == 1 && valid.toggles == 0 && valid.status == null &&
            valid.blueprintVolumeReady && valid.blueprintVolumeDirty && valid.blueprintVolumeStatus == null &&
            valid.blueprintVolumeFirst.x == 0 && valid.blueprintVolumeSecond.x == 10 && !valid.blueprintSelectionDragging &&
            !valid.blueprintVolumeDragValid, "Valid area drag did not reach the explicit selection refresh.");

        var endMiss = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        endMiss.HandleBlueprintVolumeInput(camera, new Vector2(0));
        camera.PlaneHit = false;
        Input.Down = Input.Held = false; Input.Up = true;
        endMiss.HandleBlueprintVolumeInput(camera, new Vector2(10));
        Require(endMiss.refreshes == 0 && endMiss.status == "selection.area_ray_failed" &&
            endMiss.blueprintVolumeFirst.x == 11 && endMiss.blueprintVolumeSecond.x == 22,
            "Invalid end ray changed the previous area.");
        camera = new Camera { PlaneHit = true, ProjectCorners = true, Scale = 10 };
        for (int i = 0; i < 4; ++i)
        {
            var resize = new VolumeInputChecks();
            float x = (i & 1) == 0 ? 11 : 22, z = (i & 2) == 0 ? 11 : 22;
            Input.Down = Input.Held = true; Input.Up = false;
            var start = new Vector2(x * 10 + 3, z * 10 + 4);
            resize.HandleBlueprintVolumeInput(camera, start);
            Require(resize.blueprintVolumeResizing && resize.blueprintVolumeDragPlane == -2 &&
                resize.blueprintVolumeDragFirst.x == ((i & 1) == 0 ? 22 : 11) &&
                resize.blueprintVolumeDragFirst.z == ((i & 2) == 0 ? 22 : 11), "Corner did not fix its opposite on the bottom plane.");
            Require(resize.blueprintVolumeFirst.x == 11 && resize.blueprintVolumeSecond.x == 22 && resize.refreshes == 0,
                "Resize changed applied area before release.");
            Input.Down = false;
            var end = new Vector2(start.x + 30, start.y - 50);
            resize.HandleBlueprintVolumeInput(camera, end);
            Input.Held = false; Input.Up = true;
            resize.HandleBlueprintVolumeInput(camera, end);
            Require(resize.refreshes == 1 && resize.previews == 1 && resize.toggles == 0 &&
                Math.Abs(resize.blueprintVolumeSecond.x - (x + 3)) < .001 &&
                Math.Abs(resize.blueprintVolumeSecond.z - (z - 5)) < .001 && !resize.blueprintVolumeResizing,
                "Corner drag failed to preserve click offset or commit exactly once.");
        }
        var cornerClick = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        cornerClick.HandleBlueprintVolumeInput(camera, new Vector2(110, 110));
        Input.Down = Input.Held = false; Input.Up = true;
        cornerClick.HandleBlueprintVolumeInput(camera, new Vector2(110, 110));
        Require(cornerClick.toggles == 0 && cornerClick.refreshes == 0 && cornerClick.blueprintVolumeFirst.x == 11,
            "Corner click toggled a piece or changed area.");
        var cancelled = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        cancelled.HandleBlueprintVolumeInput(camera, new Vector2(110, 110));
        cancelled.CancelBlueprintSelectionDrag();
        Input.Down = Input.Held = false; Input.Up = true;
        cancelled.HandleBlueprintVolumeInput(camera, new Vector2(150, 150));
        Require(cancelled.refreshes == 0 && cancelled.blueprintVolumeFirst.x == 11 && cancelled.blueprintVolumeSecond.x == 22,
            "Cancelled corner drag changed area.");
        var refused = new VolumeInputChecks();
        Input.Down = Input.Held = true; Input.Up = false;
        refused.HandleBlueprintVolumeInput(camera, new Vector2(110, 110));
        camera.PlaneHit = false;
        Input.Down = Input.Held = false; Input.Up = true;
        refused.HandleBlueprintVolumeInput(camera, new Vector2(150, 150));
        Require(refused.refreshes == 0 && refused.blueprintVolumeFirst.x == 11 && refused.blueprintVolumeSecond.x == 22,
            "Invalid corner end ray changed applied area.");
        Input.Control = true;
        var manual = new VolumeInputChecks();
        Require(!manual.TryBeginBlueprintVolumeResize(camera, new Vector2(110, 110), out _, out _), "Ctrl manual selection captured by corner.");
        Input.Control = false;
        manual.blueprintVolumeBottom = 21;
        Require(!manual.TryBeginBlueprintVolumeResize(camera, new Vector2(110, 110), out _, out _), "Invalid height enabled corner editing.");
        manual.blueprintVolumeBottom = -2; camera.ProjectCorners = false;
        Require(!manual.TryBeginBlueprintVolumeResize(camera, new Vector2(110, 110), out _, out _), "Hidden corner captured input.");
        return "PASS actual corner resize source: four corners/opposite/offset/bottom plane, preview/release, click/cancel/invalid ray/Ctrl/invalid height/behind camera; original volume input source: plane-miss click, invalid start/end drag, valid area drag. Host substitutes only.";
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Camera
    {
        public bool PlaneHit, ProjectCorners;
        public float Scale = 1, nearClipPlane = .1f;
        public Vector3 WorldToScreenPoint(Vector3 point) => new Vector3(point.x * Scale, point.z * Scale, ProjectCorners ? 10 : -10);
    }
    private sealed class Piece { }
    private sealed class Player { public static readonly Player m_localPlayer = new Player(); }
    private struct Vector2
    {
        public float x, y;
        public Vector2(float value) : this(value, 0) { }
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
    }
    private struct Vector3
    {
        public float x, y, z;
        public Vector3(float value) : this(value, value, value) { }
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new Vector3(0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
    }
    private enum KeyCode { LeftControl, RightControl }
    private static class Time { public const int frameCount = 10; }
    private static class Input
    {
        public static bool Down, Held, Up, Control;
        public static bool GetMouseButtonDown(int button) => Down;
        public static bool GetMouseButton(int button) => Held;
        public static bool GetMouseButtonUp(int button) => Up;
        public static bool GetKey(KeyCode key) => Control;
    }
    private static class BuildWorksLocalization { public static string Text(string key) => key; }
}
