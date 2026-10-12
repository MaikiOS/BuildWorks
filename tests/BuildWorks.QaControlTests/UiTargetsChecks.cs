using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Inert host objects run the production observer/guard. They do not prove Unity raycasting or clicks.
internal static class UiTargetsChecks
{
    private static int checks;
    private static Button Button(string label = "Сохранить") => new Button { name = label,
        canvas = new Canvas(), transform = new RectTransform(), child = new TMPro.TMP_Text { text = label } };
    private static void Reject(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { ++checks; return; }
        throw new Exception("Stale/covered UI target accepted.");
    }
    internal static void Run()
    {
        var observer = new BuildWorksQaUiTargets();
        for (int fault = 0; fault < 13; ++fault)
        {
            Screen.width = 1000; Screen.height = 800;
            var button = Button();
            Selectable.allSelectablesArray = new Selectable[] { button };
            var events = new EventSystem { hit = button.gameObject }; EventSystem.current = events;
            var list = observer.Observe();
            if (list.Length != 1 || list[0].label != "Сохранить" || list[0].id.Length != 32 || list[0].x <= 0 || list[0].x >= 1)
                throw new Exception("Visible native control missing/corrupted.");
            var target = observer.Resolve(list[0].id); observer.Require(target); ++checks;
            switch (fault)
            {
                case 0: button.isActiveAndEnabled = false; break;
                case 1: button.interactable = false; break;
                case 2: ((RectTransform)button.transform).offset = 1; break;
                case 3: button.child.text = "Удалить"; break;
                case 4: events.hit = new GameObject(); break; // Non-selectable blocker, not a button underneath.
                case 5: events.hit = Button().gameObject; break;
                case 6: EventSystem.current = new EventSystem { hit = button.gameObject }; break;
                case 7: Screen.width++; break;
                case 8: Screen.height++; break;
                case 9: button.canvas.isActiveAndEnabled = false; break;
                case 10: events.isActiveAndEnabled = false; break;
                case 11: button.destroyed = true; break;
                case 12: ((RectTransform)button.transform).offset = float.NaN; break;
            }
            Reject(() => observer.Require(target));
        }
        Screen.width = 1000; Screen.height = 800;
        var field = new TMPro.TMP_InputField { canvas = new Canvas(), transform = new RectTransform(), text = new string('a', 140) };
        Selectable.allSelectablesArray = new Selectable[] { field };
        EventSystem.current = new EventSystem { hit = field.gameObject };
        var entry = observer.Observe()[0]; var original = observer.Resolve(entry.id);
        if (!entry.writable || entry.value.Length != 128 || !entry.valueTruncated) throw new Exception("Field metadata not bounded/writable/truncated."); ++checks;
        Reject(() => observer.RequireFocused(original));
        field.isFocused = true; EventSystem.current.currentSelectedGameObject = field.gameObject;
        observer.RequireFocused(original); ++checks;
        EventSystem.current.currentSelectedGameObject = new GameObject(); Reject(() => observer.RequireFocused(original));
        EventSystem.current.currentSelectedGameObject = field.gameObject;
        field.text += "b"; Reject(() => observer.Require(original)); // Change beyond exported128 chars still invalidates identity.
        original = observer.Resolve(observer.Observe()[0].id); field.readOnly = true;
        Reject(() => observer.Require(original));
        field.readOnly = false;
        string stale = observer.Observe()[0].id; field.text += "changed"; observer.Observe(); Reject(() => observer.Resolve(stale));
        stale = observer.Observe()[0].id;
        if (observer.Observe()[0].id != stale) throw new Exception("Unchanged fresh observation invalidated a model decision."); ++checks;
        observer.Clear(); Reject(() => observer.Resolve(stale));
        var child = new GameObject { parent = field }; EventSystem.current.hit = child;
        if (observer.Observe().Length != 1) throw new Exception("Raycast child was not associated with its native field."); ++checks;
        EventSystem.current.hit = new GameObject(); EventSystem.current.underneath = field.gameObject;
        if (observer.Observe().Length != 0) throw new Exception("Observer looked through an opaque blocker."); ++checks;
        EventSystem.current.underneath = null;
        EventSystem.current = null;
        if (observer.Observe().Length != 0) throw new Exception("Missing EventSystem must yield no targets."); ++checks;
        var many = new List<Selectable>(); for (int i = 0; i < 40; ++i)
        { var button = Button(); ((RectTransform)button.transform).offset = i; many.Add(button); }
        Selectable.allSelectablesArray = many.ToArray(); EventSystem.current = new EventSystem { eachOwn = true };
        if (observer.Observe().Length != 32 || !observer.Truncated) throw new Exception("UI count bound missing."); ++checks;
        JsonUtility.length = 5000;
        if (observer.Observe().Length != 2 || !observer.Truncated) throw new Exception("UI byte budget missing."); ++checks;
        JsonUtility.length = 100;
        var scoped = Button();
        scoped.transform.parent = new Transform { name = "BuildWorksExchange" };
        Selectable.allSelectablesArray = new Selectable[] { scoped };
        EventSystem.current = new EventSystem { hit = scoped.gameObject };
        var scopedEntry = observer.Observe()[0];
        if (scopedEntry.name != scoped.name || scopedEntry.scope != "BuildWorksExchange") throw new Exception("UI name/scope missing."); ++checks;
        ((RectTransform)scoped.transform).offset = 80;
        Selectable.allSelectablesArray = many.Concat(new[] { scoped }).ToArray();
        EventSystem.current = new EventSystem { eachOwn = true };
        if (observer.Observe()[0].scope != "BuildWorksExchange" || !observer.Truncated) throw new Exception("Native controls exhausted the page before BuildWorks."); ++checks;
        Selectable.allSelectablesArray = new Selectable[] { scoped };
        EventSystem.current = new EventSystem { hit = scoped.gameObject };
        scopedEntry = observer.Observe()[0];
        var scopedTarget = observer.Resolve(scopedEntry.id);
        scoped.transform.parent.name = "BuildWorks_BlueprintEditorView"; Reject(() => observer.Require(scopedTarget));
        scopedTarget = observer.Resolve(observer.Observe()[0].id);
        scoped.name = "AnotherControl"; Reject(() => observer.Require(scopedTarget));
        scoped.name = new string('x', 81);
        if (observer.Observe().Length != 0) throw new Exception("Truncated control name exposed an ambiguous policy target."); ++checks;
        Console.WriteLine("PASS QA UI targets: " + checks + " production observer/guard assertions. Inert UI; real host execution unverified.");
    }
}

namespace UnityEngine
{
    public class Object
    {
        public bool destroyed;
        public static implicit operator bool(Object value) => value != null && !value.destroyed;
    }
    public class GameObject : Object
    {
        public Component parent;
        public T GetComponentInParent<T>() where T : class => parent as T;
    }
    public class Component : Object
    {
        public string name;
        public Transform transform;
        public Canvas canvas;
        public TMPro.TMP_Text child;
        private GameObject own;
        public GameObject gameObject => own ?? (own = new GameObject { parent = this });
        public T GetComponentInParent<T>() where T : class => canvas as T;
        public T GetComponentInChildren<T>() where T : class => child as T;
    }
    public class Transform : Object { public string name; public Transform parent; }
    public class RectTransform : Transform
    {
        public float offset;
        public void GetWorldCorners(Vector3[] points)
        {
            points[0] = new Vector3(100 + offset, 100); points[1] = new Vector3(100 + offset, 200);
            points[2] = new Vector3(200 + offset, 200); points[3] = new Vector3(200 + offset, 100);
        }
    }
    public class Canvas : Component { public bool isActiveAndEnabled = true; public RenderMode renderMode; public Camera worldCamera; public Canvas rootCanvas => this; }
    public class Camera : Object { }
    public enum RenderMode { ScreenSpaceOverlay, ScreenSpaceCamera, WorldSpace }
    public struct Vector2 { public float x, y; public Vector2(float x, float y) { this.x = x; this.y = y; } }
    public struct Vector3 { public float x, y; public Vector3(float x, float y) { this.x = x; this.y = y; } }
    public static class RectTransformUtility { public static Vector2 WorldToScreenPoint(Camera camera, Vector3 p) => new Vector2(p.x, p.y); }
    public static class Screen { public static int width = 1000, height = 800; }
    public static class Mathf { public static float Min(float a, float b) => Math.Min(a, b); public static float Max(float a, float b) => Math.Max(a, b); }
    public static class JsonUtility { public static int length = 100; public static string ToJson(object value) => new string('j', length); }
}
namespace UnityEngine.UI
{
    public class Selectable : Component
    {
        public static Selectable[] allSelectablesArray;
        public bool isActiveAndEnabled = true, interactable = true;
        public bool IsInteractable() => interactable;
    }
    public class Button : Selectable { }
    public class InputField : Selectable { public bool readOnly, isFocused; public string text; }
    public class Toggle : Selectable { public bool isOn; }
    public class Slider : Selectable { public float value; }
    public class Dropdown : Selectable { public int value; }
    public class Text : Component { public string text; }
}
namespace TMPro
{
    public class TMP_InputField : Selectable { public bool readOnly, isFocused; public string text; }
    public class TMP_Text : Component { public string text; }
    public class TMP_Dropdown : Selectable { public int value; }
}
namespace UnityEngine.EventSystems
{
    public class PointerEventData { public Vector2 position; public PointerEventData(EventSystem events) { } }
    public struct RaycastResult { public GameObject gameObject; }
    public class EventSystem : UnityEngine.Object
    {
        public static EventSystem current;
        public bool isActiveAndEnabled = true, eachOwn;
        public GameObject hit, underneath;
        public GameObject currentSelectedGameObject;
        public void RaycastAll(PointerEventData data, List<RaycastResult> results)
        {
            if (eachOwn)
            {
                foreach (var control in Selectable.allSelectablesArray)
                    if (Math.Abs(data.position.x - (150 + ((RectTransform)control.transform).offset)) < 0.01)
                    { results.Add(new RaycastResult { gameObject = control.gameObject }); break; }
            }
            else if (hit) results.Add(new RaycastResult { gameObject = hit });
            if (underneath) results.Add(new RaycastResult { gameObject = underneath });
        }
    }
}
