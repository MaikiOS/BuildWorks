using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Shared by the controller and native UI rows so modifier chords use one input source.
    internal interface IBlueprintEditorInput
    {
        Vector2 MousePosition { get; }
        Vector2 MouseScrollDelta { get; }
        float UnscaledDeltaTime { get; }
        bool HasFocus { get; }
        string InputString { get; }
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        bool GetMouseButtonUp(int button);
        float GetAxis(string axis);
        bool TryWarpMousePosition(Vector2 position);
    }

    internal sealed class UnityBlueprintEditorInput : IBlueprintEditorInput
    {
        internal static readonly UnityBlueprintEditorInput Instance = new UnityBlueprintEditorInput();
        private UnityBlueprintEditorInput() { }
        public Vector2 MousePosition => Input.mousePosition;
        public Vector2 MouseScrollDelta => Input.mouseScrollDelta;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
        public bool HasFocus => Application.isFocused;
        public string InputString => Input.inputString;
        public bool GetKey(KeyCode key) => Input.GetKey(key);
        public bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
        public bool GetMouseButton(int button) => Input.GetMouseButton(button);
        public bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
        public bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
        public float GetAxis(string axis) => Input.GetAxis(axis);

        private PropertyInfo currentMouse, mouseNative, mouseEnabled, mouseAdded;
        private MethodInfo createWarp, executeWarp;
        private bool warpApiChecked;

        public bool TryWarpMousePosition(Vector2 position)
        {
            if (!Application.isFocused || Cursor.lockState != CursorLockMode.None ||
                !new Rect(0, 0, Screen.width, Screen.height).Contains(Input.mousePosition) ||
                float.IsNaN(position.x) || float.IsInfinity(position.x) ||
                float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                position.x < 0 || position.x >= Screen.width || position.y < 0 || position.y >= Screen.height) return false;
            try
            {
                if (!warpApiChecked)
                {
                    warpApiChecked = true;
                    Type mouseType = Type.GetType("UnityEngine.InputSystem.Mouse, Unity.InputSystem", false);
                    Type commandType = Type.GetType("UnityEngine.InputSystem.LowLevel.WarpMousePositionCommand, Unity.InputSystem", false);
                    currentMouse = mouseType?.GetProperty("current", BindingFlags.Public | BindingFlags.Static);
                    mouseNative = mouseType?.GetProperty("native");
                    mouseEnabled = mouseType?.GetProperty("enabled");
                    mouseAdded = mouseType?.GetProperty("added");
                    createWarp = commandType?.GetMethod("Create", new[] { typeof(Vector2) });
                    MethodInfo execute = mouseType?.GetMethods().FirstOrDefault(method => method.Name == "ExecuteCommand" &&
                        method.IsGenericMethodDefinition && method.GetParameters().Length == 1);
                    executeWarp = commandType != null ? execute?.MakeGenericMethod(commandType) : null;
                }
                object mouse = currentMouse?.GetValue(null);
                if (mouse == null || createWarp == null || executeWarp == null ||
                    !(mouseNative?.GetValue(mouse) is bool native && native) ||
                    !(mouseEnabled?.GetValue(mouse) is bool enabled && enabled) ||
                    !(mouseAdded?.GetValue(mouse) is bool added && added)) return false;
                // WarpCursorPosition discards this result; an unsupported native command must not move our logical pointer.
                object command = createWarp.Invoke(null, new object[] { position });
                return (long)executeWarp.Invoke(mouse, new[] { command }) >= 0;
            }
            catch (Exception exception)
            {
                Debug.LogWarning("BuildWorks cursor warp unavailable: " + exception.GetBaseException().Message);
                return false;
            }
        }
    }
}
