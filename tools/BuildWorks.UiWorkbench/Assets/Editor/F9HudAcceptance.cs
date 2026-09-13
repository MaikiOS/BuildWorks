using System;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// The actual product HUD is copied by Capture-UiWorkbench.ps1. These are only
// its external host dependencies; no visibility or button logic is replaced.
public class Hud : MonoBehaviour
{
    public static Hud instance;
    public GameObject m_rootObject;
    public GameObject m_pieceSelectionWindow;
    public GameObject m_pieceIconPrefab;
    public TMP_Text m_buildSelection;
}

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksPlugin
    {
        internal sealed class DockSetting { public int Value; }
        internal static readonly DockSetting EditorHudDock = new DockSetting();
        internal static readonly DockSetting InterfaceScalePercent = new DockSetting { Value = 80 };
    }
}

internal static class F9HudAcceptance
{
    internal static string Run(TMP_FontAsset font)
    {
        Hud previous = Hud.instance;
        var host = new GameObject("F9HudHost", typeof(RectTransform), typeof(Canvas), typeof(Hud));
        var templateObject = new GameObject("F9HudFont", typeof(RectTransform));
        templateObject.SetActive(false);
        templateObject.transform.SetParent(host.transform, false);
        TMP_Text template = templateObject.AddComponent<TextMeshProUGUI>();
        template.font = font;
        Hud.instance = host.GetComponent<Hud>();
        Hud.instance.m_rootObject = host;
        Hud.instance.m_buildSelection = template;
        Action noop = () => { };
        GizmoMode clickedMode = GizmoMode.Move;
        int dispatches = 0;
        float rise = 0f, turn = 0f, pitch = 0f, roll = 0f, scale = 0f;
        using (var view = new PrecisionPlacementHudView(
            selectMode: mode => { clickedMode = mode; ++dispatches; },
            selectConstraint: _ => { }, toggleSpace: noop, toggleAnchorVisibility: noop,
            cycleHandleScale: noop, cycleTranslationStep: noop, cycleRotationStep: noop,
            cycleArrayStep: noop, cycleDistribution: noop,
            adjustRise: delta => rise = Mathf.Clamp(rise + delta, -100f, 100f),
            adjustRepeatTurn: delta => turn = (float)PrecisionAdjustment.NormalizeDegrees(turn + delta),
            adjustRepeatPitch: delta => pitch = (float)PrecisionAdjustment.NormalizeDegrees(pitch + delta),
            adjustRepeatRoll: delta => roll = (float)PrecisionAdjustment.NormalizeDegrees(roll + delta),
            adjustRepeatScale: delta => scale = Mathf.Clamp(scale + delta, -300f, 300f),
            toggleSymmetry: noop, toggleMeshSnap: noop, toggleAutoAlignment: noop,
            adjustCount: _ => { }, saveBlueprint: noop, openWorkspaceCatalog: noop,
            cycleEditorLighting: noop, undo: noop, redo: noop, reset: noop,
            cancel: noop, confirm: noop))
        {
            try
            {
                Show(view, GizmoMode.Move);
                RectTransform panel = (RectTransform)typeof(PrecisionPlacementHudView)
                    .GetField("panel", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);
                Require(Mathf.Abs(panel.localScale.x - .8f) < .0001f &&
                    Text(view, "interfaceScaleText").text == "UI 80%",
                    "F9 HUD did not apply the shared 80% interface scale");
                Click(Button(view, "interfaceScaleText"));
                Require(Mathf.Abs(panel.localScale.x - .9f) < .0001f &&
                    BuildWorksPlugin.InterfaceScalePercent.Value == 90,
                    "F9 UI scale button did not update the real panel");
                BuildWorksPlugin.InterfaceScalePercent.Value = 80;
                Show(view, GizmoMode.Move);
                Button repeat = Button(view, "repeatText");
                Button guide = Button(view, "guideText");
                Require(repeat.gameObject.activeInHierarchy && guide.gameObject.activeInHierarchy,
                    "Single-piece F9 hides Array/Contour");
                Click(repeat);
                Require(clickedMode == GizmoMode.Repeat && dispatches == 1,
                    "F9 Array button does not dispatch Repeat");
                Show(view, clickedMode);
                Require(Text(view, "countText").text ==
                    BuildWorksLocalization.Text("hud.array_count", 3, 2) &&
                    Button(view, "distributionText").gameObject.activeInHierarchy,
                    "F9 Repeat does not expose its two-dimensional layout controls");
                Click(guide);
                Require(clickedMode == GizmoMode.Guide && dispatches == 2,
                    "F9 Contour button does not dispatch Guide");
                Show(view, clickedMode);
                Require(!Button(view, "distributionText").gameObject.activeInHierarchy &&
                    Text(view, "countText").text ==
                        BuildWorksLocalization.Text("hud.contour_count", 3),
                    "F9 Contour still displays Array parameters");

                Show(view, GizmoMode.Move, blueprint: true);
                Require(repeat.gameObject.activeInHierarchy && guide.gameObject.activeInHierarchy,
                    "World-blueprint F9 hides Array/Contour");
                Click(repeat);
                Require(clickedMode == GizmoMode.Repeat && dispatches == 3,
                    "Whole-blueprint Array button does not dispatch Repeat");
                Show(view, clickedMode, blueprint: true);
                Require(Text(view, "countText").text ==
                    BuildWorksLocalization.Text("hud.array_count", 3, 2) &&
                    Button(view, "distributionText").gameObject.activeInHierarchy,
                    "Whole-blueprint Repeat parameters remain hidden");
                Click(guide);
                Require(clickedMode == GizmoMode.Guide && dispatches == 4,
                    "Whole-blueprint Contour button does not dispatch Guide");
                Show(view, clickedMode, blueprint: true);
                Require(Text(view, "countText").text ==
                    BuildWorksLocalization.Text("hud.contour_count", 3) &&
                    !Button(view, "distributionText").gameObject.activeInHierarchy,
                    "Whole-blueprint Contour still displays Array parameters");
                view.ShowArmed(1, 3, false, false, false, paused: true, pauseReason: "resources");
                Show(view, GizmoMode.Move);
                Require(repeat.gameObject.activeInHierarchy && guide.gameObject.activeInHierarchy,
                    "Returning from paused blueprint placement loses single-piece F9 controls");
                view.ShowBlueprintWorkspace(null, 3, "piece", "day");
                Show(view, GizmoMode.Move);
                Require(repeat.gameObject.activeInHierarchy && guide.gameObject.activeInHierarchy,
                    "Returning from workspace loses single-piece F9 controls");

                Show(view, GizmoMode.Repeat);
                GameObject turnObject = Text(view, "repeatTurnText").transform.parent.gameObject;
                var module = EventSystem.current.GetComponent<StandaloneInputModule>();
                Require(module, "F9 pointer test requires actual StandaloneInputModule");
                var pointer = new PointerEventData(EventSystem.current) {
                    button = PointerEventData.InputButton.Left, eligibleForClick = true,
                    pointerPress = turnObject, pointerClick = turnObject, pointerDrag = turnObject,
                    pressPosition = new Vector2(100, 100), position = new Vector2(112, 100),
                    delta = new Vector2(12, 0)
                };
                typeof(PointerInputModule).GetMethod("ProcessDrag", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(module, new object[] { pointer });
                pointer.position += new Vector2(8, 0);
                pointer.delta = new Vector2(8, 0);
                typeof(PointerInputModule).GetMethod("ProcessDrag", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(module, new object[] { pointer });
                Require(!pointer.eligibleForClick && turn > 0f, "F9 angle drag does not dispatch or still permits click");
                typeof(StandaloneInputModule).GetMethod("ReleaseMouse", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(module, new object[] { pointer, turnObject });
                Require(!view.IsEditingAngle && !turnObject.GetComponent<TMP_InputField>().enabled,
                    "F9 drag release spuriously starts text editing");
                float beforeScroll = turn;
                Scroll(turnObject);
                Scroll(Text(view, "repeatPitchText").transform.parent.gameObject);
                Scroll(Text(view, "repeatRollText").transform.parent.gameObject);
                Require(turn > beforeScroll && pitch > 0f && roll > 0f, "F9 angle wheel callbacks are blocked after drag");

                TMP_InputField scaleInput = BeginEdit(view, "repeatScaleText");
                scaleInput.text = "400";
                Require(Mathf.Abs(scale - 300f) < 0.001f, "F9 scale overshoot did not clamp to 300%");
                scaleInput.text = "20";
                Require(Mathf.Abs(scale - 20f) < 0.001f, "F9 scale overshoot poisoned the next absolute input");
                scaleInput.DeactivateInputField(true);
                Show(view, GizmoMode.Repeat, scale: scale);
                scaleInput = BeginEdit(view, "repeatScaleText");
                scaleInput.text = "-400";
                Require(Mathf.Abs(scale + 300f) < 0.001f, "F9 negative scale step did not clamp");
                CancelEdit(scaleInput);
                Require(Mathf.Abs(scale - 20f) < 0.001f && !view.IsEditingAngle,
                    "F9 Escape after clamped input did not restore starting scale");
                scaleInput = BeginEdit(view, "repeatScaleText");
                scaleInput.text = "not a number";
                CancelEdit(scaleInput);
                Require(Mathf.Abs(scale - 20f) < 0.001f && !view.IsEditingAngle,
                    "F9 invalid text then Escape changed starting scale");
                TMP_InputField riseInput = BeginEdit(view, "riseText");
                riseInput.text = "101";
                Require(Mathf.Abs(rise - 100f) < 0.001f, "F9 rise overshoot did not clamp");
                riseInput.text = "1";
                Require(Mathf.Abs(rise - 1f) < 0.001f, "F9 rise overshoot poisoned next absolute input");
                riseInput.DeactivateInputField(true);
                Require(!view.IsEditingAngle, "F9 completed numeric edit blocks Session input");
                return "Actual F9 HUD: single and whole-blueprint Array/Contour pointer dispatch, " +
                    "3x2 controls, pause/workspace restore; native ProcessDrag/ReleaseMouse + angle scroll, " +
                    "TMP scale 400->20, clamped/invalid Escape restore, rise 101->1";
            }
            finally
            {
                Hud.instance = previous;
                host.SetActive(false);
                Object.Destroy(host);
            }
        }
    }

    private static void Show(PrecisionPlacementHudView view, GizmoMode mode, bool blueprint = false, float scale = 0f) =>
        view.ShowEditing(mode, GizmoAxis.None, true, false, "100%", 0, 0,
            RepeatDistributionMode.Pack, 3, 2, 0f, 0f, 0f, 0f, scale, false, true, true,
            "1 см", "1°", "ПО РАЗМЕРУ", blueprint ? "Fixture" : null,
            blueprint ? 3 : 0, -1, blueprint, false, "day", false, false, 0,
            "", "offset", "rotation");

    private static void Scroll(GameObject target) => ExecuteEvents.Execute(target,
        new PointerEventData(EventSystem.current) { scrollDelta = Vector2.up }, ExecuteEvents.scrollHandler);

    private static TMP_InputField BeginEdit(PrecisionPlacementHudView view, string field)
    {
        GameObject target = Text(view, field).transform.parent.gameObject;
        ExecuteEvents.Execute(target, new PointerEventData(EventSystem.current) {
            button = PointerEventData.InputButton.Left }, ExecuteEvents.pointerClickHandler);
        TMP_InputField input = target.GetComponent<TMP_InputField>();
        Require(input.enabled && view.IsEditingAngle, "F9 ordinary click does not begin numeric text editing");
        // ActivateInputField defers this native lifecycle method until the next TMP LateUpdate.
        typeof(TMP_InputField).GetMethod("ActivateInputFieldInternal", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(input, null);
        return input;
    }

    private static void CancelEdit(TMP_InputField input)
    {
        input.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.Escape });
        Require(input.wasCanceled, "Native TMP Escape did not mark edit cancelled");
        input.DeactivateInputField(true);
    }

    private static TMP_Text Text(PrecisionPlacementHudView view, string field) =>
        (TMP_Text)typeof(PrecisionPlacementHudView).GetField(field,
            BindingFlags.NonPublic | BindingFlags.Instance).GetValue(view);

    private static Button Button(PrecisionPlacementHudView view, string field) =>
        Text(view, field).GetComponentInParent<Button>(includeInactive: true);

    private static void Click(Button button) => ExecuteEvents.Execute(button.gameObject,
        new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left },
        ExecuteEvents.pointerClickHandler);

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
