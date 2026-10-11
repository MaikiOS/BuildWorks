using System;

// Run.ps1 injects the actual input handler; substitutes model clicks and plane hits only.
public sealed class VolumeInputChecks
{
    private bool blueprintSelectionDragging, blueprintVolumeDragValid;
    private bool blueprintVolumeReady = true, blueprintVolumeDirty = false;
    private Vector2 blueprintSelectionStart;
    private Vector3 blueprintVolumeDragFirst, blueprintVolumeFirst = new Vector3(11), blueprintVolumeSecond = new Vector3(22);
    private string blueprintVolumeStatus = "previous";
    private int suppressPlacementFrame, toggles, refreshes, previews, cancellations;
    private string status;

    // PRODUCTION_METHOD

    private bool MouseOverEditorPanel(Vector2 mouse) => false;
    private bool TryBlueprintAreaPoint(Camera camera, Vector2 mouse, out Vector3 point)
    { point = new Vector3(mouse.x); return camera.PlaneHit; }
    private bool TryGetPlacedPieceAt(Camera camera, Vector2 mouse, out Piece piece, out int hit)
    { piece = new Piece(); hit = 0; return true; }
    private bool IsBlueprintPieceSelectable(Piece piece) => true;
    private void ToggleBlueprintVolumePiece(Piece piece, bool remove) { ++toggles; }
    private void CancelBlueprintSelectionDrag()
    { ++cancellations; blueprintSelectionDragging = blueprintVolumeDragValid = false; }
    private int BlueprintVolume(Vector3 first, Vector3 second) => 0;
    private void ShowBlueprintVolume(int volume) { ++previews; }
    private void RefreshBlueprintVolumeSelection() { ++refreshes; }
    private void ShowStatus(Player player, string value) { status = value; }

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
        return "PASS actual volume input source: plane-miss click, invalid start/end drag, valid area drag. Host substitutes only.";
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Camera { public bool PlaneHit; }
    private sealed class Piece { }
    private sealed class Player { public static readonly Player m_localPlayer = new Player(); }
    private struct Vector2
    {
        public float x;
        public Vector2(float value) { x = value; }
        public float sqrMagnitude => x * x;
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x);
    }
    private struct Vector3 { public float x; public Vector3(float value) { x = value; } }
    private enum KeyCode { LeftControl, RightControl }
    private static class Time { public const int frameCount = 10; }
    private static class Input
    {
        public static bool Down, Held, Up;
        public static bool GetMouseButtonDown(int button) => Down;
        public static bool GetMouseButton(int button) => Held;
        public static bool GetMouseButtonUp(int button) => Up;
        public static bool GetKey(KeyCode key) => false;
    }
    private static class BuildWorksLocalization { public static string Text(string key) => key; }
}
