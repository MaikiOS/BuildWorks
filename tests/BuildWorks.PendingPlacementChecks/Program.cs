using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;

static class Program
{
    static void Require(bool value, string reason) { if (!value) throw new Exception(reason); }
    static void Refuses(Action action)
    {
        try { action(); } catch (InvalidOperationException) { return; }
        throw new Exception("Invalid transition accepted");
    }
    static int[] Build(params int[][] dependencies)
    {
        var order = new PendingPlacementOrder(dependencies.Length);
        var built = new List<int>();
        while (!order.Complete)
        {
            int candidate = order.Current;
            Require(candidate >= 0, "No-progress pass must stop, never complete");
            if (!dependencies[candidate].All(built.Contains))
            { order.DeferContact(); continue; }
            order.AcceptContact();
            Require(order.ResolvedCount == built.Count, "Preview resolved an unpaid part");
            built.Add(candidate);
            order.Commit(candidate);
        }
        Require(order.ResolvedCount == dependencies.Length, "Not all original indices committed");
        return built.ToArray();
    }
    static void Main()
    {
        Require(Build(new[] { 1 }, Array.Empty<int>()).SequenceEqual(new[] { 1, 0 }), "upper/base original IDs");
        Require(Build(Array.Empty<int>(), new[] { 0 }).SequenceEqual(new[] { 0, 1 }), "base/upper original IDs");
        Require(Build(Array.Empty<int>(), new[] { 2 }, Array.Empty<int>()).SequenceEqual(new[] { 0, 2, 1 }), "independent base must not hide support dependency");
        var none = new PendingPlacementOrder(2);
        Require(none.DeferContact() && !none.DeferContact() && none.Current == -1 && !none.Complete && none.ResolvedCount == 0,
            "Full unsupported pass must retain every pending part");
        Require(none.RestartPass() && none.Current == 0, "External support retry changed IDs");
        Refuses(() => none.Commit(0));
        none.AcceptContact();
        Refuses(() => none.DeferContact());
        Refuses(() => none.Commit(1));
        Require(none.ResolvedCount == 0, "Wrong commit lost a pending part");
        none.Commit(0);
        Refuses(() => none.Commit(0));
        Require(none.Current == 1 && none.ResolvedCount == 1, "Committed part was replayed");
        none.AcceptContact(); none.Commit(1);
        Require(none.Complete && none.Current == -1, "Complete requires all commits");
        var blocked = new PendingPlacementOrder(2);
        blocked.SkipPlayerBlocked(0);
        Require(blocked.Current == 1 && blocked.ResolvedCount == 1, "Existing explicit skip lost its index");
        var saved = new[] { true, false, true, false };
        var resumed = new PendingPlacementOrder(saved); saved[1] = true;
        Require(resumed.Current == 1 && resumed.ResolvedCount == 2, "Saved progress or original slots changed");
        resumed.DeferContact();
        Require(resumed.Current == 3 && resumed.ResolvedCount == 2, "Saved committed slots were probed again");
        resumed.AcceptContact(); resumed.Commit(3);
        Require(resumed.Current == 1 && resumed.ResolvedCount == 3, "Commit did not restart a saved pending pass");
        var complete = new PendingPlacementOrder(new[] { true, true });
        Require(complete.Complete && complete.Current == -1 && !complete.RestartPass(), "Saved complete project restarted placement");
        Console.WriteLine("PENDING_ORDER_OK: dependencies/original IDs/no-progress/no unpaid resolution/wrong and duplicate commit/retry/player-blocked skip");
    }
}
