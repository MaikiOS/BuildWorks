#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using NativeInput = UnityEngine.Input;

namespace OstrixMods.BuildWorks
{
    // Only the QA process sees these devices. No OS events or desktop cursor ownership.
    internal sealed class BuildWorksQaInput
    {
        internal static bool Active { get; private set; }
        private static readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        private static readonly HashSet<KeyCode> previous = new HashSet<KeyCode>();
        private static int buttons, previousButtons;
        private static Vector2 position, scroll, look;
        private int legacyFrame = -1;
        private Keyboard keyboard, physicalKeyboard;
        private Mouse mouse, physicalMouse;
        private StandaloneInputModule module;
        private BaseInputModule activeModule;
        private FieldInfo pointerStates;
        private BaseInput originalOverride;
        private BuildWorksQaUiInput uiInput;

        internal void Begin(GameObject owner)
        {
            activeModule = EventSystem.current ? EventSystem.current.currentInputModule : null;
            module = activeModule as StandaloneInputModule;
            if (activeModule && !module && !(activeModule is InputSystemUIInputModule))
                throw new InvalidOperationException("Unsupported QA UI module: " + activeModule.GetType().FullName);
            if (activeModule is InputSystemUIInputModule)
            {
                pointerStates = typeof(InputSystemUIInputModule).GetField("m_PointerStates", BindingFlags.Instance | BindingFlags.NonPublic);
                var layout = pointerStates?.FieldType;
                var model = layout?.GetField("firstValue")?.FieldType;
                if (layout?.GetField("length") == null || layout.GetField("additionalValues") == null ||
                    model?.GetField("leftButton")?.FieldType.GetMethod("CopyPressStateTo") == null ||
                    model.GetField("rightButton") == null || model.GetField("middleButton") == null || model.GetField("eventData") == null)
                    throw new InvalidOperationException("QA UI pointer layout changed.");
            }
            if (module && typeof(PointerInputModule).GetField("m_PointerData", BindingFlags.Instance | BindingFlags.NonPublic) == null)
                throw new InvalidOperationException("QA legacy UI pointer layout changed.");
            physicalKeyboard = Keyboard.current; physicalMouse = Mouse.current;
            position = NativeInput.mousePosition;
            keyboard = InputSystem.AddDevice<Keyboard>(); mouse = InputSystem.AddDevice<Mouse>();
            Active = true;
            if (module) { originalOverride = module.inputOverride; uiInput = owner.AddComponent<BuildWorksQaUiInput>(); module.inputOverride = uiInput; }
        }

        internal static KeyCode ParseKey(string name)
        {
            if (!Enum.TryParse(name, false, out KeyCode key) || !Enum.IsDefined(typeof(KeyCode), key) ||
                !Enum.TryParse(ToSystemKey(key), false, out Key _) || key == KeyCode.F12 || key == KeyCode.None)
                throw new ArgumentException("Unsupported QA key: " + name);
            return key;
        }

        private static string ToSystemKey(KeyCode key)
        {
            string name = key.ToString();
            if (name.StartsWith("Alpha", StringComparison.Ordinal)) return "Digit" + name.Substring(5);
            if (key == KeyCode.Return) return "Enter";
            if (key == KeyCode.LeftControl) return "LeftCtrl";
            if (key == KeyCode.RightControl) return "RightCtrl";
            if (key == KeyCode.BackQuote) return "Backquote";
            return name;
        }

        internal void Feed(BuildWorksQaControlProtocol.Step step, bool first, Vector2 from, float fraction, float delta)
        {
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            if (legacyFrame != Time.frameCount)
            { previous.Clear(); previous.UnionWith(held); previousButtons = buttons; legacyFrame = Time.frameCount; }
            held.Clear();
            foreach (string name in step.keys ?? Array.Empty<string>()) held.Add(ParseKey(name));
            buttons = step.buttons;
            if (step.pointer) position = Vector2.Lerp(from, new Vector2(step.x * (Screen.width - 1), step.y * (Screen.height - 1)), fraction);
            look = new Vector2(step.lookX, step.lookY) * delta;
            scroll = first ? new Vector2(0, step.scroll) : Vector2.zero;
            var keys = new List<Key>();
            foreach (KeyCode key in held) keys.Add((Key)Enum.Parse(typeof(Key), ToSystemKey(key)));
            InputState.Change(keyboard, new KeyboardState(keys.ToArray()));
            InputState.Change(mouse, new MouseState { position = position, delta = look, scroll = scroll * 120,
                buttons = (ushort)buttons });
        }

        internal void Release(bool abort = false)
        {
            Active = false; held.Clear(); previous.Clear(); buttons = previousButtons = 0; scroll = look = Vector2.zero;
            try { if (abort) CancelUiPointers(); }
            catch (Exception error) { Debug.LogError("BuildWorks QA UI cancellation failed: " + error.GetBaseException().Message); }
            finally
            {
                try { if (activeModule && abort)
                {
                    bool enabled = activeModule.enabled;
                    activeModule.enabled = false; activeModule.enabled = enabled;
                } }
                catch (Exception error) { Debug.LogError("BuildWorks QA UI reset failed: " + error.Message); }
            }
            try { if (module && module.inputOverride == uiInput) module.inputOverride = originalOverride; }
            catch (Exception error) { Debug.LogError("BuildWorks QA UI restore failed: " + error.Message); }
            module = null; activeModule = null; pointerStates = null; originalOverride = null;
            if (uiInput) UnityEngine.Object.Destroy(uiInput);
            uiInput = null;
            try { if (keyboard != null && keyboard.added) { try { InputState.Change(keyboard, new KeyboardState()); } finally { InputSystem.RemoveDevice(keyboard); } } }
            catch (Exception error) { Debug.LogError("BuildWorks QA keyboard release failed: " + error.Message); }
            try { if (mouse != null && mouse.added) { try { InputState.Change(mouse, new MouseState()); } finally { InputSystem.RemoveDevice(mouse); } } }
            catch (Exception error) { Debug.LogError("BuildWorks QA mouse release failed: " + error.Message); }
            keyboard = null; mouse = null;
            if (physicalKeyboard != null && physicalKeyboard.added) physicalKeyboard.MakeCurrent();
            if (physicalMouse != null && physicalMouse.added) physicalMouse.MakeCurrent();
            physicalKeyboard = null; physicalMouse = null;
            legacyFrame = -1;
        }

        private void CancelUiPointers()
        {
            var pointers = new List<(object state, PointerEventData data, PointerEventData.InputButton button)>();
            if (module)
            {
                var data = typeof(PointerInputModule).GetField("m_PointerData", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(module) as IDictionary;
                foreach (object value in data.Values) if (value is PointerEventData pointer) pointers.Add((null, pointer, pointer.button));
            }
            else if (activeModule is InputSystemUIInputModule)
            {
                // Preserve the host's ExtendedPointerEventData (device, raycasts, coordinates); each button supplies its own press state.
                object states = pointerStates.GetValue(activeModule);
                var layout = states.GetType(); int count = (int)layout.GetField("length").GetValue(states);
                var additional = layout.GetField("additionalValues").GetValue(states) as Array;
                for (int i = 0; i < count; ++i)
                {
                    object model = i == 0 ? layout.GetField("firstValue").GetValue(states) : additional.GetValue(i - 1);
                    var type = model.GetType();
                    foreach (string button in new[] { "leftButton", "rightButton", "middleButton" })
                    {
                        object state = type.GetField(button).GetValue(model);
                        var data = (PointerEventData)type.GetField("eventData").GetValue(model);
                        pointers.Add((state, data, button == "leftButton" ? PointerEventData.InputButton.Left : button == "rightButton" ? PointerEventData.InputButton.Right : PointerEventData.InputButton.Middle));
                    }
                }
            }
            foreach (var pointer in pointers)
                try
                {
                    if (pointer.state != null)
                    {
                        pointer.data.button = pointer.button;
                        pointer.state.GetType().GetMethod("CopyPressStateTo").Invoke(pointer.state, new object[] { pointer.data });
                    }
                    BuildWorksQaUiCancellation.Cancel(pointer.data);
                }
                catch (Exception error) { Debug.LogError("BuildWorks QA UI handler failed: " + error.GetBaseException().Message); }
            if (module) module.DeactivateModule();
        }

        internal bool UiModuleMatches => (EventSystem.current ? EventSystem.current.currentInputModule : null) == activeModule;

        internal bool PhysicalIntervention => physicalKeyboard != null && physicalKeyboard.anyKey.isPressed ||
            physicalMouse != null && (physicalMouse.leftButton.isPressed || physicalMouse.rightButton.isPressed ||
                physicalMouse.middleButton.isPressed || physicalMouse.delta.ReadValue().sqrMagnitude > 4 || physicalMouse.scroll.ReadValue().sqrMagnitude > 0);

        public static Vector3 mousePosition => Active ? (Vector3)position : NativeInput.mousePosition;
        public static Vector2 mouseScrollDelta => Active ? scroll : NativeInput.mouseScrollDelta;
        public static string inputString => Active ? "" : NativeInput.inputString;
        public static bool GetKey(KeyCode key) => Active ? held.Contains(key) : NativeInput.GetKey(key);
        public static bool GetKeyDown(KeyCode key) => Active ? held.Contains(key) && !previous.Contains(key) : NativeInput.GetKeyDown(key);
        public static bool GetMouseButton(int button) => Active ? (buttons & 1 << button) != 0 : NativeInput.GetMouseButton(button);
        public static bool GetMouseButtonDown(int button) => Active ? GetMouseButton(button) && (previousButtons & 1 << button) == 0 : NativeInput.GetMouseButtonDown(button);
        public static bool GetMouseButtonUp(int button) => Active ? !GetMouseButton(button) && (previousButtons & 1 << button) != 0 : NativeInput.GetMouseButtonUp(button);
        public static float GetAxis(string axis) => Active ? axis == "Mouse X" ? look.x : axis == "Mouse Y" ? look.y : 0 : NativeInput.GetAxis(axis);

    }

    internal sealed class BuildWorksQaUiInput : BaseInput
    {
        public override Vector2 mousePosition { get => BuildWorksQaInput.mousePosition; }
        public override Vector2 mouseScrollDelta { get => BuildWorksQaInput.mouseScrollDelta; }
        public override bool mousePresent => BuildWorksQaInput.Active || base.mousePresent;
        public override bool GetMouseButton(int button) => BuildWorksQaInput.GetMouseButton(button);
        public override bool GetMouseButtonDown(int button) => BuildWorksQaInput.GetMouseButtonDown(button);
        public override bool GetMouseButtonUp(int button) => BuildWorksQaInput.GetMouseButtonUp(button);
        public override float GetAxisRaw(string axis) => BuildWorksQaInput.Active ? 0 : base.GetAxisRaw(axis);
        public override bool GetButtonDown(string name) => BuildWorksQaInput.Active ? name == "Submit" ? BuildWorksQaInput.GetKeyDown(KeyCode.Return) : name == "Cancel" && BuildWorksQaInput.GetKeyDown(KeyCode.Escape) : base.GetButtonDown(name);
    }
}
#endif
