#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
using UnityEngine;
using UnityEngine.EventSystems;

namespace OstrixMods.BuildWorks
{
    // Abort a native UI gesture without pointer-click or drop; callers cancel their own transaction first.
    internal static class BuildWorksQaUiCancellation
    {
        internal static void Cancel(PointerEventData data)
        {
            if (data == null) return;
            GameObject pressed = data.pointerPress, dragged = data.pointerDrag;
            data.eligibleForClick = false;
            bool dragging = data.dragging;
            try { if (pressed) ExecuteEvents.Execute(pressed, data, ExecuteEvents.pointerUpHandler); }
            finally
            {
                try { if (dragging && dragged) ExecuteEvents.Execute(dragged, data, ExecuteEvents.endDragHandler); }
                finally { data.pointerPress = null; data.rawPointerPress = null; data.pointerClick = null; data.pointerDrag = null; data.dragging = false; }
            }
        }
    }
}
#endif
