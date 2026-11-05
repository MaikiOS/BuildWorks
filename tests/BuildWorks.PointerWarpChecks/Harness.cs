using System;

// Run.ps1 injects production FinishPointerWarp. Only host input, viewport and commit effects are substituted.
public sealed class PointerWarpChecks
{
    private bool pointerWarpPending = true, keyboardPreview = true, keyboardValid = true, pointerWrapUnavailable = false;
    private float pointerWarpWait = 0;
    private Vector2 transformPointerOffset = new Vector2(), pointerWarpFrom = new Vector2(2, 50), pointerWarpTo = new Vector2(90, 50);
    private readonly Input input = new Input();
    private readonly View view = new View();
    private int commits, cancels, rebases;

    // PRODUCTION_METHOD

    private void CancelGizmoDrag() { ++cancels; pointerWarpPending = false; }
    private void CommitGizmoDrag() { ++commits; pointerWarpPending = false; }
    private void RebaseKeyboardPreview(Vector2 mouse) { ++rebases; pointerWarpPending = false; }
    private void Tick() { if (pointerWarpPending) FinishPointerWarp(new Rect()); }

    public static string Run()
    {
        var click = new PointerWarpChecks(); click.input.Down = true;
        click.Tick(); click.Tick();
        Require(click.commits == 1 && !click.pointerWarpPending, "LMB confirmation during pending warp was lost or committed twice.");
        foreach (int heldButton in new[] { 1, 2 })
        {
            var camera = new PointerWarpChecks(); camera.input.Down = true; camera.input.HeldButton = heldButton;
            camera.Tick();
            Require(camera.commits == 0 && camera.pointerWarpPending, "Camera button plus LMB committed pending preview.");
        }
        foreach (bool control in new[] { false, true })
        {
            var toolbar = new PointerWarpChecks(); toolbar.input.Down = true; toolbar.input.Control = control; toolbar.view.ControlHit = true;
            toolbar.Tick();
            Require(toolbar.commits == 0 && toolbar.pointerWarpPending, "Toolbar click implicitly committed preview.");
            var inside = new PointerWarpChecks(); inside.input.Down = true; inside.input.Control = control;
            inside.Tick(); Require(inside.commits == 1, "Viewport click lost existing Ctrl confirmation behavior.");
        }
        var outside = new PointerWarpChecks(); outside.input.Down = true; outside.input.MousePosition = new Vector2(-1, 50);
        outside.Tick(); Require(outside.commits == 0 && outside.pointerWarpPending, "Outside click committed preview.");
        var invalid = new PointerWarpChecks { keyboardValid = false }; invalid.input.Down = true;
        invalid.Tick(); Require(invalid.commits == 0 && invalid.pointerWarpPending, "Invalid preview committed on LMB.");
        invalid.input.Down = false; invalid.input.Key = KeyCode.Return;
        invalid.Tick(); Require(invalid.commits == 0, "Invalid preview committed on Enter.");
        foreach (KeyCode key in new[] { KeyCode.Return, KeyCode.KeypadEnter })
        {
            var enter = new PointerWarpChecks(); enter.input.Key = key;
            enter.Tick(); Require(enter.commits == 1 && !enter.pointerWarpPending, "Pending Enter failed.");
        }
        var escape = new PointerWarpChecks(); escape.input.Key = KeyCode.Escape; escape.input.Down = true;
        escape.Tick(); Require(escape.cancels == 1 && escape.commits == 0 && !escape.pointerWarpPending, "Escape lost priority over confirmation.");
        var physical = new PointerWarpChecks { keyboardPreview = false, keyboardValid = false }; physical.input.Up = true;
        physical.Tick(); Require(physical.commits == 1 && !physical.pointerWarpPending, "Physical release lost previous preview.");
        var ack = new PointerWarpChecks(); ack.input.MousePosition = new Vector2(94, 53);
        ack.Tick(); Require(!ack.pointerWarpPending && ack.commits == 0 && ack.transformPointerOffset.x == -88,
            "Warp acknowledgement changed continuity offset or committed.");
        var noAck = new PointerWarpChecks(); noAck.Tick();
        Require(noAck.pointerWarpPending && noAck.commits == 0 && noAck.rebases == 0, "Ordinary pending frame changed preview.");
        noAck.input.UnscaledDeltaTime = .25f; noAck.Tick();
        Require(noAck.pointerWrapUnavailable && !noAck.pointerWarpPending && noAck.rebases == 1 && noAck.commits == 0,
            "Keyboard timeout lost rebase/fallback.");
        var held = new PointerWarpChecks { keyboardPreview = false }; held.input.UnscaledDeltaTime = .25f;
        held.Tick(); Require(held.pointerWrapUnavailable && held.pointerWarpPending && held.rebases == 0 && held.commits == 0,
            "Physical timeout should retain preview until release or Escape.");
        return "PASS actual FinishPointerWarp source: pending LMB/once, Ctrl, toolbar/outside/invalid guards, Enter/keypad/Escape, physical release, ACK offset, keyboard/physical timeout. Host doubles only.";
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Input
    {
        public Vector2 MousePosition = new Vector2(2, 50);
        public float UnscaledDeltaTime = .016f;
        public bool Down = false, Up = false, Control = false;
        public int HeldButton = -1;
        public KeyCode Key = KeyCode.None;
        public bool GetKeyDown(KeyCode key) => Key == key;
        public bool GetKey(KeyCode key) => Control;
        public bool GetMouseButtonDown(int button) => Down;
        public bool GetMouseButtonUp(int button) => Up;
        public bool GetMouseButton(int button) => HeldButton == button;
    }
    private sealed class View { public bool ControlHit = false; public void SetStatus(string value) { } public bool IsViewportControlHit(Vector2 mouse) => ControlHit; }
    private struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public float sqrMagnitude => x * x + y * y;
        public static Vector2 operator +(Vector2 a, Vector2 b) => new Vector2(a.x + b.x, a.y + b.y);
        public static Vector2 operator -(Vector2 a, Vector2 b) => new Vector2(a.x - b.x, a.y - b.y);
    }
    private struct Rect { public bool Contains(Vector2 mouse) => mouse.x >= 0 && mouse.x < 100 && mouse.y >= 0 && mouse.y < 100; }
    private enum KeyCode { None, Escape, Return, KeypadEnter }
    private static class Mathf { public static float Max(float a, float b) => Math.Max(a, b); }
    private static class BuildWorksLocalization { public static string Text(string key) => key; }
}
