using System;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class QaUiCancellationAcceptance
{
    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("QA UI cancellation: " + message); }

    internal static string Run()
    {
        var root = new GameObject("QaNativeWidgetAbort", typeof(RectTransform));
        try
        {
            var button = root.AddComponent<Button>();
            var probe = root.AddComponent<QaAbortEventProbe>();
            int clicks = 0; button.onClick.AddListener(() => ++clicks);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                pointerPress = root, rawPointerPress = root, pointerClick = root, eligibleForClick = true };
            button.OnPointerDown(data);
            var pressed = typeof(Selectable).GetProperty("isPointerDown", BindingFlags.Instance | BindingFlags.NonPublic);
            Require((bool)pressed.GetValue(button), "native Button was not pressed");
            BuildWorksQaUiCancellation.Cancel(data);
            Require(!(bool)pressed.GetValue(button) && probe.Up == 1 && probe.PressContext && clicks == 0 && probe.Click == 0 && probe.Drop == 0,
                "abort left native Button pressed or committed a click/drop");
            BuildWorksQaUiCancellation.Cancel(data);
            Require(probe.Up == 1 && data.pointerPress == null && data.pointerClick == null && !data.eligibleForClick,
                "repeat abort or pointer cleanup failed");
            button.OnPointerClick(new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left });
            Require(clicks == 1, "next normal click failed");
            Object.DestroyImmediate(button);

            var slider = root.AddComponent<Slider>();
            data.pointerPress = root; slider.OnPointerDown(data);
            Require((bool)pressed.GetValue(slider), "native Slider was not pressed");
            BuildWorksQaUiCancellation.Cancel(data);
            Require(!(bool)pressed.GetValue(slider), "abort left native Slider pressed");
            Object.DestroyImmediate(slider);

            var scroll = root.AddComponent<ScrollRect>();
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(root.transform, false);
            scroll.content = (RectTransform)content.transform;
            data.pointerDrag = root; data.dragging = true;
            scroll.OnBeginDrag(data);
            var dragging = typeof(ScrollRect).GetField("m_Dragging", BindingFlags.Instance | BindingFlags.NonPublic);
            Require((bool)dragging.GetValue(scroll), "native ScrollRect drag did not start");
            BuildWorksQaUiCancellation.Cancel(data);
            Require(!(bool)dragging.GetValue(scroll) && probe.End == 1 && probe.DragContext && probe.Click == 0 && probe.Drop == 0,
                "abort left native ScrollRect dragging or dispatched click/drop");
            BuildWorksQaUiCancellation.Cancel(data);
            Require(probe.End == 1 && !data.dragging && data.pointerDrag == null, "repeat drag cancellation failed");
            return "QA native UGUI abort: Button/Slider pressed state and ScrollRect drag cleared, no click/drop, repeated stop harmless, next ordinary click works. Real game InputSystem module still needs host smoke.";
        }
        finally { Object.DestroyImmediate(root); }
    }
}

internal sealed class QaAbortEventProbe : MonoBehaviour, IPointerUpHandler, IEndDragHandler, IPointerClickHandler, IDropHandler
{
    internal int Up, End, Click, Drop;
    internal bool PressContext, DragContext;
    public void OnPointerUp(PointerEventData data) { ++Up; PressContext = data.pointerPress == gameObject; }
    public void OnEndDrag(PointerEventData data) { ++End; DragContext = data.pointerDrag == gameObject && data.dragging; }
    public void OnPointerClick(PointerEventData data) { ++Click; }
    public void OnDrop(PointerEventData data) { ++Drop; }
}
