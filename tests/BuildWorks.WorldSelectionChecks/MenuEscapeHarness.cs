using System;
#if BUILDWORKS_TEST_STAND
using Input = QaInput;
#else
using Input = NormalInput;
#endif

public sealed class MenuEscapeChecks
{
    private enum SessionMode { None, BlueprintWorldSelection, BlueprintEditor, BlueprintWorldPlacement }
    private enum PlacementState { Inactive, Editing }
    private SessionMode sessionMode = SessionMode.BlueprintWorldSelection;
    private PlacementState state = PlacementState.Editing;
    private int consumedWorldSelectionEscapeFrame = -1;
    private static MenuEscapeChecks instance;
    private MenuEscapeChecks session => this;
    private bool menuVisible, stampedBeforeFinish;
    private object player = new object();

    // GUARD

    private void CancelByEscape()
    {
        // CANCEL_BODY
    }

    // PREFIX

    private void Finish()
    {
        stampedBeforeFinish = consumedWorldSelectionEscapeFrame == Time.frameCount;
        sessionMode = SessionMode.None;
        state = PlacementState.Inactive;
    }
    private void ShowStatus(object value, string text) { }
    private void MenuUpdate()
    {
        if (Prefix() && Input.GetKeyDown(KeyCode.Escape)) menuVisible = !menuVisible;
    }
    private static int checks;
    private static void Require(bool value, string text)
    {
        ++checks;
        if (!value) throw new Exception(text);
    }
    public static string Run()
    {
        foreach (bool menuFirst in new[] { true, false })
        {
            Time.frameCount = 100;
            Input.Down = true;
            instance = new MenuEscapeChecks();
            if (menuFirst) instance.MenuUpdate();
            instance.CancelByEscape();
            if (!menuFirst) instance.MenuUpdate();
            Require(!instance.menuVisible && instance.state == PlacementState.Inactive,
                "Selection Escape must cancel without opening Menu in either update order.");
            Require(instance.stampedBeforeFinish, "Escape ownership must survive Finish cleanup.");
            ++Time.frameCount;
            Input.Down = false;
            Require(Prefix(), "Release must not block Menu updates.");
            ++Time.frameCount;
            Input.Down = true;
            instance.MenuUpdate();
            Require(instance.menuVisible, "Fresh Escape outside selection must open Menu.");
        }
        instance = new MenuEscapeChecks();
        Time.frameCount = 200;
        Input.Down = false;
        Require(Prefix(), "Selection without Escape must not suppress Menu updates.");
        instance.Finish(); // UI Cancel does not consume a key.
        Input.Down = true;
        instance.MenuUpdate();
        Require(instance.menuVisible, "UI Cancel must not consume the next Escape, even in the same frame.");
        foreach (var mode in new[] { SessionMode.None, SessionMode.BlueprintEditor, SessionMode.BlueprintWorldPlacement })
        {
            instance = new MenuEscapeChecks { sessionMode = mode };
            Require(Prefix(), "Other modes must keep their current Menu behaviour.");
        }
        instance = new MenuEscapeChecks { state = PlacementState.Inactive };
        Require(Prefix(), "Inactive selection mode must not claim Escape.");
        instance = null;
        Require(Prefix(), "Missing/unloaded plugin must not block Menu.");
        return "Menu Escape production guard/cancel/prefix: " + checks + " checks PASS";
    }
}
public static class NormalInput { public static bool Down; public static bool GetKeyDown(KeyCode key) => Down; }
public static class QaInput { public static bool Down; public static bool GetKeyDown(KeyCode key) => Down; }
public enum KeyCode { Escape }
public static class Time { public static int frameCount; }
public static class BuildWorksLocalization { public static string Text(string key) => key; }
