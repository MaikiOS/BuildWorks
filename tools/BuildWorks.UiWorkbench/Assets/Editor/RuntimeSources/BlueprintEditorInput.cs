using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Shared by the controller and native UI rows so modifier chords use one input source.
    internal interface IBlueprintEditorInput
    {
        Vector2 MousePosition { get; }
        Vector2 MouseScrollDelta { get; }
        float UnscaledDeltaTime { get; }
        bool GetKey(KeyCode key);
        bool GetKeyDown(KeyCode key);
        bool GetMouseButton(int button);
        bool GetMouseButtonDown(int button);
        bool GetMouseButtonUp(int button);
        float GetAxis(string axis);
    }

    internal sealed class UnityBlueprintEditorInput : IBlueprintEditorInput
    {
        internal static readonly UnityBlueprintEditorInput Instance = new UnityBlueprintEditorInput();
        private UnityBlueprintEditorInput() { }
        public Vector2 MousePosition => Input.mousePosition;
        public Vector2 MouseScrollDelta => Input.mouseScrollDelta;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
        public bool GetKey(KeyCode key) => Input.GetKey(key);
        public bool GetKeyDown(KeyCode key) => Input.GetKeyDown(key);
        public bool GetMouseButton(int button) => Input.GetMouseButton(button);
        public bool GetMouseButtonDown(int button) => Input.GetMouseButtonDown(button);
        public bool GetMouseButtonUp(int button) => Input.GetMouseButtonUp(button);
        public float GetAxis(string axis) => Input.GetAxis(axis);
    }
}
