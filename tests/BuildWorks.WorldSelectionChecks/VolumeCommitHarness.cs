using System;
using System.Collections.Generic;
using System.Linq;

// The real commit method is injected by Run.ps1, once for each build mode.
public sealed class VolumeCommitChecks
{
    private readonly List<Piece> blueprintSelection = new List<Piece>();
    private int highlightChanges;

    // PRODUCTION_METHOD

    private void SetBlueprintPieceHighlight(Piece piece, bool selected) { ++highlightChanges; }

    public static string Run()
    {
        var check = new VolumeCommitChecks();
        var old = new Piece();
        check.blueprintSelection.Add(old);
        var tooMany = Enumerable.Range(0, CompositeBlueprintStore.EditorPartBudget + 1).Select(_ => new Piece()).ToList();
        Require(!check.TryCommitBlueprintVolumeSelection(tooMany, out string error) && error != null &&
            check.blueprintSelection.SequenceEqual(new[] { old }) && check.highlightChanges == 0 && BuildWorksTestIsolation.Calls == 0,
            "Over-budget commit touched the previous selection.");
#if BUILDWORKS_TEST_STAND
        var large = Enumerable.Range(0, 513).Select(_ => new Piece()).ToList();
        BuildWorksTestIsolation.Refuse = true;
        Require(!check.TryCommitBlueprintVolumeSelection(large, out error) && error.Contains("isolation refused") &&
            check.blueprintSelection.SequenceEqual(new[] { old }) && check.highlightChanges == 0 && BuildWorksTestIsolation.Calls == 1,
            "Isolation refusal changed selection/highlights or hid its cause.");
        BuildWorksTestIsolation.Refuse = false;
        Require(check.TryCommitBlueprintVolumeSelection(large, out error) && error == null &&
            check.blueprintSelection.SequenceEqual(large) && check.highlightChanges == 514 && BuildWorksTestIsolation.Calls == 2,
            "Authorized large commit failed or skipped its gate.");
        check.highlightChanges = 0;
        Require(check.TryCommitBlueprintVolumeSelection(large, out error) && check.highlightChanges == 0 && BuildWorksTestIsolation.Calls == 3,
            "Unchanged selection must recheck permission without repainting every piece.");
#endif
        check.highlightChanges = 0;
        Require(check.TryCommitBlueprintVolumeSelection(new List<Piece>(), out error) && check.blueprintSelection.Count == 0,
            "Empty volume did not clear the prior selection.");
        return "PASS actual volume commit source: budget/refusal/unchanged selection/delta highlights/clear; mode=" +
            CompositeBlueprintStore.EditorPartBudget + ". Host substitutes only.";
    }

    private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    private sealed class Piece { }
    private static class CompositeBlueprintStore
    {
        public const int InteractivePartBudget = 512;
#if BUILDWORKS_TEST_STAND
        public const int EditorPartBudget = 50000;
#else
        public const int EditorPartBudget = 512;
#endif
    }
    private static class BuildWorksTestIsolation
    {
        public static int Calls;
        public static bool Refuse = false;
        public static void RequireLoadedSession()
        {
            ++Calls;
            if (Refuse) throw new InvalidOperationException("isolation refused");
        }
    }
    private static class BuildWorksLocalization
    {
        public static string Text(string key, params object[] args) { return key + " " + string.Join(" ", args); }
    }
}
