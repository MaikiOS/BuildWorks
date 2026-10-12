#if BUILDWORKS_TEST_STAND || BUILDWORKS_QA_CONTROL_TESTS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OstrixMods.BuildWorks
{
    // Observations name native controls; actions still travel through the native pointer/input module.
    internal sealed class BuildWorksQaUiTargets
    {
        [Serializable] internal sealed class Entry
        {
            public string id, kind, name, scope, label, value;
            public float x, y, left, bottom, right, top;
            public bool writable, focused, valueTruncated;
        }

        internal sealed class Target
        {
            internal Selectable control;
            internal EventSystem events;
            internal Entry entry;
            internal string label, value;
            internal int width, height;
        }

        private readonly Dictionary<string, Target> targets = new Dictionary<string, Target>();
        private readonly List<RaycastResult> hits = new List<RaycastResult>();
        internal bool Truncated { get; private set; }

        internal void Clear() { targets.Clear(); Truncated = false; }

        internal Entry[] Observe()
        {
            var previous = targets.Values.ToArray();
            Clear();
            var result = new List<Entry>();
            int bytes = 0;
            var events = EventSystem.current;
            if (!events || !events.isActiveAndEnabled || Screen.width < 2 || Screen.height < 2) return result.ToArray();
            // Native piece icons must not exhaust the page before the task's BuildWorks controls.
            foreach (var control in Selectable.allSelectablesArray.OrderByDescending(control => control && Scope(control.transform).Length != 0))
            {
                Entry entry = Describe(control, out string label, out string value);
                if (entry == null || !Hit(events, control, entry)) continue;
                if (result.Count == 32) { Truncated = true; break; }
                var unchanged = previous.FirstOrDefault(target => target.control == control && target.events == events && Same(target, entry, label, value));
                entry.id = unchanged?.entry.id ?? Guid.NewGuid().ToString("N");
                bytes += System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(entry)) + 1;
                if (bytes > 12000) { Truncated = true; break; }
                targets.Add(entry.id, new Target { control = control, events = events, entry = entry,
                    label = label, value = value, width = Screen.width, height = Screen.height });
                result.Add(entry);
            }
            return result.ToArray();
        }

        internal Target Resolve(string id)
        {
            if (id == null || !targets.TryGetValue(id, out Target target))
                throw new InvalidOperationException("QA UI target is not in the latest observation; observe again.");
            Require(target);
            return target;
        }

        internal void Require(Target target)
        {
            if (target == null || !target.control || !target.events || target.events != EventSystem.current ||
                !target.events.isActiveAndEnabled || target.width != Screen.width || target.height != Screen.height)
                throw new InvalidOperationException("QA UI target/session/screen changed; observe again.");
            Entry current = Describe(target.control, out string label, out string value);
            if (current == null || !Same(target, current, label, value) || !Hit(target.events, target.control, current))
                throw new InvalidOperationException("QA UI target moved, changed or is covered; observe again.");
        }

        private static bool Same(Target target, Entry current, string label, string value) =>
            target.width == Screen.width && target.height == Screen.height && label == target.label && value == target.value &&
            current.kind == target.entry.kind && current.name == target.entry.name && current.scope == target.entry.scope && current.writable == target.entry.writable &&
            current.left == target.entry.left && current.bottom == target.entry.bottom &&
            current.right == target.entry.right && current.top == target.entry.top;

        internal void RequireFocused(Target target)
        {
            Require(target);
            bool focused = target.control is TMP_InputField tmp && tmp.isFocused || target.control is InputField field && field.isFocused;
            if (!target.entry.writable || !focused || target.events.currentSelectedGameObject != target.control.gameObject)
                throw new InvalidOperationException("Guarded QA text requires the observed writable field to be focused.");
        }

        private bool Hit(EventSystem events, Selectable control, Entry entry)
        {
            hits.Clear();
            events.RaycastAll(new PointerEventData(events) { position = new Vector2(entry.x * (Screen.width - 1), entry.y * (Screen.height - 1)) }, hits);
            // The first graphic can be an opaque non-selectable blocker. Never search through it.
            return hits.Count > 0 && hits[0].gameObject && hits[0].gameObject.GetComponentInParent<Selectable>() == control;
        }

        private static Entry Describe(Selectable control, out string label, out string value)
        {
            label = value = "";
            if (!control || !control.isActiveAndEnabled || !control.IsInteractable()) return null;
            if ((control.name ?? "").Length > 80) return null;
            var rect = control.transform as RectTransform;
            var canvas = control.GetComponentInParent<Canvas>();
            if (!rect || !canvas || !canvas.isActiveAndEnabled) return null;
            canvas = canvas.rootCanvas;
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && !camera) return null;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            float left = float.PositiveInfinity, bottom = left, right = float.NegativeInfinity, top = right;
            foreach (Vector3 corner in corners)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner);
                if (float.IsNaN(point.x) || float.IsInfinity(point.x) || float.IsNaN(point.y) || float.IsInfinity(point.y)) return null;
                left = Mathf.Min(left, point.x); bottom = Mathf.Min(bottom, point.y);
                right = Mathf.Max(right, point.x); top = Mathf.Max(top, point.y);
            }
            float x = (left + right) * 0.5f, y = (bottom + top) * 0.5f;
            if (right <= left || top <= bottom || x < 0 || y < 0 || x > Screen.width - 1 || y > Screen.height - 1) return null;
            var tmp = control as TMP_InputField; var field = control as InputField;
            var text = control.GetComponentInChildren<TMP_Text>(); var legacy = control.GetComponentInChildren<Text>();
            label = text ? text.text : legacy ? legacy.text : control.name;
            if (tmp) value = tmp.text;
            else if (field) value = field.text;
            else if (control is Toggle toggle) value = toggle.isOn ? "true" : "false";
            else if (control is Slider slider) value = slider.value.ToString("R", CultureInfo.InvariantCulture);
            else if (control is Dropdown dropdown) value = dropdown.value.ToString(CultureInfo.InvariantCulture);
            else if (control is TMP_Dropdown tmpDropdown) value = tmpDropdown.value.ToString(CultureInfo.InvariantCulture);
            bool writable = tmp && !tmp.readOnly || field && !field.readOnly;
            bool focused = tmp && tmp.isFocused || field && field.isFocused;
            return new Entry { kind = control.GetType().Name, name = control.name ?? "", scope = Scope(control.transform),
                label = Bounded(label, 80), value = Bounded(value, 128),
                writable = writable, focused = focused, valueTruncated = value != null && value.Length > 128,
                x = x / (Screen.width - 1), y = y / (Screen.height - 1),
                left = left, bottom = bottom, right = right, top = top };
        }

        private static string Bounded(string text, int length) => string.IsNullOrEmpty(text) ? "" : text.Substring(0, Math.Min(text.Length, length));

        private static string Scope(Transform node)
        {
            for (int depth = 0; node && depth < 64; ++depth, node = node.parent)
                if (node.name != null && node.name.StartsWith("BuildWorks", StringComparison.Ordinal))
                    return node.name.Length <= 80 ? node.name : "";
            return "";
        }
    }
}
#endif
