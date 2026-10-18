using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using OstrixMods.BuildWorks;

// Inject the actual shared control; substitutes expose events and text state, not its interaction logic.
// CONTROL

public static class AngleCommitChecks
{
    public static string Run()
    {
        var control = new AngleDragControl(); var input = new TMP_InputField(); var label = new TMP_Text();
        int previews = 0, completed = 0;
        control.Initialize(delta => { ++previews; control.SetValue(control.Value + delta, "height"); }, input, label, false, .05f, -512, 512);
        control.EditFinished = () => { Require(!input.enabled, "Completion left text editing active."); ++completed; };
        var drag = new PointerEventData { button = PointerEventData.InputButton.Left };
        control.OnBeginDrag(drag);
        for (int i = 1; i <= 64; ++i) { drag.position = new Vector2(i, 0); control.OnDrag(drag); }
        Require(previews == 64 && completed == 0, "Drag preview repeatedly refreshed membership.");
        control.OnEndDrag(drag); control.OnEndDrag(drag);
        Require(completed == 1, "Drag did not complete exactly once.");
        control.OnScroll(new PointerEventData { scrollDelta = new Vector2(0, 1) });
        control.OnScroll(new PointerEventData());
        Require(completed == 2, "Wheel completion was missing or fired for zero movement.");
        control.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Right });
        Require(control.Value == 0 && completed == 3, "Reset failed to complete.");
        void Edit(string preview, string final, bool cancel)
        {
            control.OnPointerClick(new PointerEventData { button = PointerEventData.InputButton.Left });
            input.onValueChanged.Invoke(preview);
            Require(input.enabled, "Preview ended the text gesture.");
            input.wasCanceled = cancel; input.onEndEdit.Invoke(final);
            Require(!input.enabled, "Text gesture did not release input."); input.wasCanceled = false;
        }
        Edit("12.5", "12.5", false);
        Require(control.Value == 12.5f && completed == 4, "Valid text failed to complete once.");
        Edit("30", "30", true);
        Require(control.Value == 12.5f && completed == 5, "Cancelled text did not roll back before completion.");
        Edit("40", "invalid", false);
        Require(control.Value == 12.5f && completed == 6, "Invalid text did not roll back before completion.");
        var right = new PointerEventData { button = PointerEventData.InputButton.Right };
        control.OnBeginDrag(right); control.OnDrag(right); control.OnEndDrag(right);
        Require(completed == 6, "Unrelated pointer input committed height.");
        control.EditFinished = null; control.OnScroll(new PointerEventData { scrollDelta = new Vector2(0, 1) });
        return "PASS actual AngleDragControl: 64 previews/one completion, wheel/reset/text/cancel/invalid/duplicate/unrelated input; inert UI substitutes, not Unity/Valheim.";
    }
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}

namespace UnityEngine
{
    public class MonoBehaviour { }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public enum KeyCode { LeftControl, RightControl, LeftShift, RightShift }
    public static class Input { public static bool GetKey(KeyCode key) => false; }
    public static class Mathf
    {
        public static float Abs(float value) => Math.Abs(value);
        public static float Sign(float value) => Math.Sign(value);
        public static float Clamp(float value, float min, float max) => Math.Min(max, Math.Max(min, value));
    }
}
namespace UnityEngine.EventSystems
{
    public interface IBeginDragHandler { }
    public interface IDragHandler { }
    public interface IEndDragHandler { }
    public interface IScrollHandler { }
    public interface IPointerClickHandler { }
    public sealed class PointerEventData
    {
        public enum InputButton { Left, Right }
        public InputButton button; public bool eligibleForClick, dragging;
        public Vector2 position, scrollDelta;
        public void Use() { }
    }
}
namespace TMPro
{
    public sealed class TextEvent
    {
        private Action<string> action;
        public void AddListener(Action<string> callback) { action += callback; }
        public void Invoke(string text) { action?.Invoke(text); }
    }
    public sealed class TMP_InputField
    {
        public bool enabled, wasCanceled; public string text = ""; public int selectionAnchorPosition, selectionFocusPosition;
        public readonly TextEvent onValueChanged = new TextEvent(), onEndEdit = new TextEvent();
        public void SetTextWithoutNotify(string value) { text = value; }
        public void ActivateInputField() { }
        public static implicit operator bool(TMP_InputField input) => input != null;
    }
    public sealed class TMP_Text { public string text; }
}
namespace OstrixMods.BuildWorks
{
    public static class PrecisionAdjustment { public static double NormalizeDegrees(double value) => value; }
}
