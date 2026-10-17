using System;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogDetachedInputChecks
    {
        private static BlueprintFogInputQueue.Sample Sample(float time, double x = 0, bool contact = true)
            => new BlueprintFogInputQueue.Sample(time, new Point3(x, 1, 0), .3f, .95f, true, contact, true);
        internal static void Run()
        {
            var queue = new BlueprintFogInputQueue(Sample(0, contact: false));
            queue.TrackDormant(Sample(.1f, 1, false));
            Require(queue.Owner == null && queue.Count == 0 && queue.Committed.Clock == .1f, "Dormant baseline allocated input/state.");
            Reject(() => queue.TrackDormant(Sample(.11f)));
            queue.Capture(Sample(.2f, 2)); queue.Capture(Sample(.3f, 3, false));
            Require(queue.HasEmission && !queue.TryBegin(out _) && queue.Count == 2, "Waiting for a slot lost an accepted path.");
            Reject(() => queue.TrackDormant(Sample(.31f, contact: false)));
            Reject(() => queue.Attach(new BlueprintFogSnapshotState(1, 1)));
            var a = new BlueprintFogSnapshotState(1, 1, 3); queue.Attach(a);
            Reject(() => queue.Attach(a)); Reject(() => queue.Detach(a));
            Require(queue.TryBegin(out var token) && queue.PendingCount == 2, "Attach did not preserve the queued prefix.");
            a.Retire(); queue.Capture(Sample(.4f, 4));
            Reject(() => queue.Detach(a));
            Require(queue.Complete(token, BlueprintFogSnapshotState.Completion.Committed) && a.CanReleaseResources && queue.Count == 1,
                "Pending retirement lost captured reentry.");
            queue.Detach(a); queue.Capture(Sample(.5f, 5, false));
            var b = new BlueprintFogSnapshotState(2, 2, 9); queue.Attach(b);
            Require(ReferenceEquals(queue.Owner, b) && a.State == BlueprintFogSnapshotState.Phase.Retired && queue.Committed.Clock == .3f &&
                queue.At(0).Actor.X == 4 && queue.At(1).Actor.X == 5, "New generation reset baseline/path or changed old generation state.");
            Require(queue.TryBegin(out token) && token.Tick == 15 && token.EmissionRevision == 1, "New generation inherited an old revision or skipped reentry.");
            queue.Complete(token, BlueprintFogSnapshotState.Completion.Committed);
            queue.Capture(Sample(.6f, 6, false));
            Require(queue.CanObserveRecovery(1) && !queue.CanRecover(1) && !queue.HasEmission, "Recovery barrier confused queued noncontact input with empty history.");
            b.Retire(); queue.Detach(b);
            var c = new BlueprintFogSnapshotState(3, 1, 15); queue.Attach(c); queue.TryBegin(out token);
            queue.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); c.Retire();
            Reject(() => queue.Detach(c));
            Require(queue.Faulted && queue.Count == 1 && c.RequiresGpuDrain, "Unknown logical commit was silently reset.");
            var full = new BlueprintFogInputQueue(Sample(0));
            for (int i = 0; i < 64; ++i) full.Capture(Sample(0, i));
            Reject(() => full.Capture(Sample(0))); Reject(() => full.Attach(new BlueprintFogSnapshotState(4, 1)));
            Require(full.Faulted && full.Count == 64 && full.At(63).Actor.X == 63, "Waiting overflow dropped accepted history.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        { bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; } Require(rejected, "Invalid logical input operation accepted."); }
    }
}
