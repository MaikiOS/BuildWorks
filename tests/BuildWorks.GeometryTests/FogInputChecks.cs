using System;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogInputChecks
    {
        private static BlueprintFogInputQueue.Sample Sample(float clock, float x = 0, bool emit = true, bool possible = true, bool nearby = true)
            => new BlueprintFogInputQueue.Sample(clock, new Point3(x, .95f, -0f), .3f, .95f, emit, possible, nearby);
        internal static void Run()
        {
            var owner = new BlueprintFogSnapshotState(7, 9); var queue = new BlueprintFogInputQueue(owner, Sample(0));
            Require(!queue.TryBegin(out _) && queue.CanRecover(0), "Empty confirmed input queue submitted work or blocked recovery.");
            queue.Capture(Sample(.005f, 1, nearby: false));
            Require(!queue.CanRecover(0), "Queued input was discarded as recovered.");
            Require(queue.TryBegin(out var idle) && queue.PendingMetadataOnly && owner.State == BlueprintFogSnapshotState.Phase.Pending, "Zero-step input did not acquire pending ownership.");
            queue.Capture(Sample(1f / 30 + .005f, 2));
            Require(queue.PendingCount == 1 && queue.Count == 2 && queue.At(0).Actor.X == 1 && !queue.At(0).Nearby, "Capture overwrote in-flight metadata or confused nearby with emit.");
            Require(!queue.TryBegin(out _), "A pending prefix was submitted twice.");
            Require(!queue.Complete(new BlueprintFogSnapshotState.Submission(7, 10, idle.Sequence, idle.Tick, idle.EmissionRevision), BlueprintFogSnapshotState.Completion.Committed) && queue.Count == 2, "Stale generation removed input.");
            Require(queue.Complete(idle, BlueprintFogSnapshotState.Completion.Committed) && queue.Count == 1 && queue.Committed.Clock == .005f, "Metadata confirmation lost the queued successor.");
            Require(!queue.Complete(idle, BlueprintFogSnapshotState.Completion.Committed), "Duplicate receipt removed more input.");
            Require(queue.TryBegin(out var first) && !queue.PendingMetadataOnly && first.Tick == 1 && first.EmissionRevision == 1, "First physics observation changed clock/revision.");
            queue.Complete(first, BlueprintFogSnapshotState.Completion.Committed);
            for (int tick = 2; tick <= 17; ++tick) queue.Capture(Sample(tick / 30f + .005f, tick, emit: tick % 2 == 0, possible: tick != 4));
            Require(queue.TryBegin(out var batch) && queue.PendingCount == 8 && batch.Tick == 9 && batch.EmissionRevision == 4, "Eight-tick prefix dropped observations or changed emission semantics.");
            for (int i = 0; i < queue.PendingCount; ++i) Require(queue.At(i).Actor.X == i + 2, "Batch reordered the actor path.");
            queue.Complete(batch, BlueprintFogSnapshotState.Completion.Committed);
            Require(queue.Count == 8 && queue.TryBegin(out batch) && batch.Tick == 17, "Batch removed a future prefix.");
            queue.Complete(batch, BlueprintFogSnapshotState.Completion.Committed);
            for (int i = 0; i < 1000; ++i)
            {
                float clock = (18 + i) / 30f + .005f; queue.Capture(Sample(clock, i % 60));
                Require(queue.TryBegin(out var token) && queue.PendingCount == 1 && queue.At(0).Actor.X == i % 60, "Ring reuse lost its oldest sample.");
                queue.Complete(token, BlueprintFogSnapshotState.Completion.Committed);
            }
            CheckFailure(BlueprintFogSnapshotState.Completion.Rejected); CheckFailure(BlueprintFogSnapshotState.Completion.Unknown);
            var full = new BlueprintFogInputQueue(new BlueprintFogSnapshotState(1, 1), Sample(0));
            for (int i = 0; i < BlueprintFogInputQueue.Capacity; ++i) full.Capture(Sample(.005f, i));
            Require(full.TryBegin(out var pending), "Full accepted queue could not submit its prefix.");
            Reject(() => full.Capture(Sample(.006f)));
            Require(full.Faulted && full.Count == 64 && full.PendingCount == 16 && full.At(63).Actor.X == 63, "Overflow overwrote accepted/in-flight input or exceeded batch work.");
            Require(full.Complete(pending, BlueprintFogSnapshotState.Completion.Committed), "Overflow discarded an already submitted valid reply.");
            Require(full.Count == 48 && full.At(0).Actor.X == 16 && full.Faulted && !full.CanRecover(0), "Success cleared a sticky overflow fault or lost unsent input.");
            Reject(() => full.TryBegin(out _));
            Reject(() => new BlueprintFogInputQueue(new BlueprintFogSnapshotState(1, 1), default));
            Reject(() => Sample(float.NaN)); Reject(() => Sample(float.PositiveInfinity)); Reject(() => Sample(-1)); Reject(() => Sample(86401));
            Reject(() => Sample(0, 65)); Reject(() => Sample(0, float.NaN));
            Reject(() => new BlueprintFogInputQueue.Sample(0, new Point3(0, 0, 0), 0, 1, true, true, true));
            Reject(() => new BlueprintFogInputQueue(new BlueprintFogSnapshotState(1, 1, 2), Sample(0)));
            var input = new BlueprintFogInputQueue(new BlueprintFogSnapshotState(2, 1), Sample(0));
            Reject(() => input.Capture(Sample(.31f))); input.Capture(Sample(.02f)); Reject(() => input.Capture(Sample(.01f)));
            Require(input.Count == 1 && !input.Faulted, "Invalid observation damaged accepted input.");
            var retired = new BlueprintFogSnapshotState(3, 1); var stopped = new BlueprintFogInputQueue(retired, Sample(0)); retired.Retire();
            Reject(() => stopped.Capture(Sample(0)));
            var metadata = new BlueprintFogSnapshotState(4, 1);
            Reject(() => metadata.Submit(1, 0, true)); Reject(() => metadata.Submit(0, 1, true));
            var stamp = metadata.Submit(0, 0, true); metadata.Retire();
            Require(!metadata.CanReleaseResources && metadata.Complete(stamp, BlueprintFogSnapshotState.Completion.Committed) && metadata.CanReleaseResources, "Metadata-only retirement escaped pending lifetime.");
            CheckSameClockAndBounds();
        }
        private static void CheckFailure(BlueprintFogSnapshotState.Completion outcome)
        {
            var owner = new BlueprintFogSnapshotState(5, 1); var queue = new BlueprintFogInputQueue(owner, Sample(0));
            queue.Capture(Sample(.04f, 2)); queue.TryBegin(out var receipt); queue.Capture(Sample(.07f, 3));
            Require(queue.Complete(receipt, outcome) && queue.Faulted && queue.Count == 2 && queue.At(0).Actor.X == 2 && owner.ConfirmedTick == 0, "Failed/uncertain step dropped accepted history.");
            Reject(() => queue.TryBegin(out _)); Reject(() => queue.Capture(Sample(.08f)));
            owner.Retire(); Require(owner.CanReleaseResources == (outcome != BlueprintFogSnapshotState.Completion.Unknown), "Input failure changed GPU retirement certainty.");
        }
        private static void CheckSameClockAndBounds()
        {
            var state = new BlueprintFogSnapshotState(8, 1); var queue = new BlueprintFogInputQueue(state, Sample(0));
            queue.Capture(Sample(0, 1)); queue.Capture(Sample(0, -1));
            float edge = 1f / 30;
            queue.Capture(new BlueprintFogInputQueue.Sample(edge, new Point3(2, .95f, 0), .4f, 1.1f, false, true, false));
            queue.Capture(Sample(edge, 3)); queue.Capture(Sample(2 * edge, 4, possible: false));
            Require(queue.TryBegin(out var receipt) && receipt.Tick == 2 && receipt.EmissionRevision == 0 && queue.PendingCount == 5, "Equal timestamps, body changes or emission flags changed the revision.");
            Require(queue.At(0).Actor.X == 1 && queue.At(1).Actor.X == -1 && queue.At(2).Radius == .4f && queue.At(2).HalfHeight == 1.1f, "Input replay changed captured order or body.");
            queue.Complete(receipt, BlueprintFogSnapshotState.Completion.Committed);
            Require(queue.Committed.Clock == 2 * edge && queue.Committed.Actor.X == 4 && queue.CanRecover(0), "Confirmation did not retain the final input history.");
            var boundaries = new BlueprintFogInputQueue(new BlueprintFogSnapshotState(9, 1), Sample(0));
            foreach (float clock in new[] { .0333299f, .03333f, .033333f, .033334f }) boundaries.Capture(Sample(clock));
            Require(boundaries.TryBegin(out var token) && token.Tick == (int)Math.Floor(.033334f / (1f / 30) + .0001f), "Queue tick arithmetic differs from the solver's float expression.");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        {
            bool failed = false; try { action(); } catch (ArgumentException) { failed = true; } catch (InvalidOperationException) { failed = true; }
            Require(failed, "Invalid fog input/state was accepted.");
        }
    }
}
