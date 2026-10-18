using System;
using System.Collections.Generic;
using System.Linq;

public sealed class VolumeHudChecks
{
    private readonly List<Piece> blueprintSelection = new List<Piece>();
    private readonly List<Piece> blueprintVolumeRows = new List<Piece>();
    private (string name, bool selected)[] blueprintVolumeHudItems = Array.Empty<(string, bool)>();
    private bool selectingBlueprint = true, blueprintVolumeMode = true, blueprintVolumeReady = true, blueprintVolumeDirty = false;
    private float blueprintVolumeTop = 20, blueprintVolumeBottom = -2, blueprintVolumePlane = 0;
    private string blueprintVolumeStatus = "previous";
    private int blueprintVolumeFirst = 0, blueprintVolumeSecond = 1;
    private readonly View blueprintVolumeView = new View();
    private readonly Hud hud = new Hud();
    private int refreshes;
    private bool refuseRefresh;

    // HEIGHT_METHOD
    // HUD_METHOD

    private int BlueprintVolume(int first, int second) => 0;
    private void ShowBlueprintVolume(int volume) { }
    private bool RefreshBlueprintVolumeSelection()
    {
        ++refreshes;
        if (refuseRefresh) return false;
        blueprintVolumeDirty = false; return true;
    }

    public static string Run()
    {
        var check = new VolumeHudChecks();
        for (int i = 0; i < 50000; ++i)
        { var piece = new Piece { m_name = "piece" + i }; check.blueprintVolumeRows.Add(piece); check.blueprintSelection.Add(piece); }
        check.UpdateBlueprintVolumeHud();
        var snapshot = check.hud.Items;
        Require(snapshot.Count == 50000 && Localization.instance.Calls == 50000, "Initial row snapshot was not prepared.");
        for (int i = 0; i < 64; ++i) check.AdjustBlueprintVolumeHeight(.05f, true);
        Require(ReferenceEquals(snapshot, check.hud.Items) && Localization.instance.Calls == 50000 && check.hud.Updates == 65 &&
            check.blueprintVolumeDirty && check.blueprintVolumeStatus == null && check.blueprintVolumeTop > 20,
            "Height-only edits rebuilt/localized the heavy row snapshot or failed to update the HUD.");
        check.CompleteBlueprintVolumeHeightEdit(); check.CompleteBlueprintVolumeHeightEdit();
        Require(check.refreshes == 1 && !check.blueprintVolumeDirty, "Completed height gesture did not request exactly one refresh.");
        check.AdjustBlueprintVolumeHeight(.05f, false); check.refuseRefresh = true;
        check.CompleteBlueprintVolumeHeightEdit();
        Require(check.refreshes == 2 && check.blueprintVolumeDirty, "Refused refresh incorrectly cleared stale selection.");
        check.refuseRefresh = false; check.CompleteBlueprintVolumeHeightEdit();
        Require(check.refreshes == 3 && !check.blueprintVolumeDirty, "Refresh retry failed.");
        check.blueprintSelection.RemoveAt(0);
        check.UpdateBlueprintVolumeHud();
        Require(!ReferenceEquals(snapshot, check.hud.Items) && !check.hud.Items[0].selected &&
            check.hud.Items[1].selected && Localization.instance.Calls == 100000,
            "Membership update left the row snapshot stale.");
        check.blueprintVolumeTop = 512;
        check.AdjustBlueprintVolumeHeight(1, true); check.AdjustBlueprintVolumeHeight(0, false);
        check.CompleteBlueprintVolumeHeightEdit();
        Require(check.refreshes == 3 && !check.blueprintVolumeDirty, "Clamped/equal edits queried unchanged membership.");
        check.AdjustBlueprintVolumeHeight(-.05f, false); check.AdjustBlueprintVolumeHeight(1, true);
        Require(check.blueprintVolumeDirty, "No-op edit cleared an earlier pending change.");
        check.CompleteBlueprintVolumeHeightEdit();
        Require(check.refreshes == 4 && !check.blueprintVolumeDirty, "Pending change was lost at the clamp edge.");
        return "PASS actual height/HUD source: 50000 rows, 64 previews reuse snapshot, completion requests one refresh; refusal/retry retained, membership snapshot rebuilds. Host substitutes only.";
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Piece
    {
        public string m_name;
        public static implicit operator bool(Piece piece) => piece != null;
    }
    private sealed class View { public void Hide() { } }
    private sealed class Hud
    {
        public IReadOnlyList<(string name, bool selected)> Items;
        public int Updates;
        public void SetWorldSelectionState(bool volume, float bottom, float top, string status,
            IReadOnlyList<(string name, bool selected)> items) { Items = items; ++Updates; }
    }
    private sealed class Localization
    {
        public static readonly Localization instance = new Localization();
        public int Calls;
        public string Localize(string name) { ++Calls; return name; }
    }
    private static class Mathf { public static float Clamp(float value, float min, float max) => Math.Min(max, Math.Max(min, value)); }
    private static class BuildWorksLocalization { public static string Text(string key, params object[] args) => key; }
}
