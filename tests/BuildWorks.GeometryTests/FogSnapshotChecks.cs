using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogSnapshotChecks
    {
        internal static void Run()
        {
            var first = new BlueprintFogSnapshotState(1, 7); var second = new BlueprintFogSnapshotState(2, 7);
            var a = first.Submit(1, 1); var b = second.Submit(2, 1);
            Require(first.ConfirmedTick == 0 && !first.CanRecover(0) && !first.CanReleaseResources, "Submission published unconfirmed state.");
            Reject(() => first.Submit(2, 2)); Reject(first.ConfirmPoolTeardown);
            Require(!first.Complete(b, BlueprintFogSnapshotState.Completion.Committed), "Receipt crossed owners.");
            Require(second.Complete(b, BlueprintFogSnapshotState.Completion.Committed) && second.ConfirmedTick == 2 && second.CanRecover(1), "Independent receipt was blocked.");
            Require(first.Complete(a, BlueprintFogSnapshotState.Completion.Committed) && first.ConfirmedTick == 1 && first.CanRecover(1), "Success did not confirm the exact tick/revision.");
            Require(!first.Complete(a, BlueprintFogSnapshotState.Completion.Committed), "Duplicate receipt was accepted.");
            var next = first.Submit(9, 2);
            Require(!first.Complete(a, BlueprintFogSnapshotState.Completion.Committed), "Old sequence confirmed a newer step.");
            Require(!first.Complete(new BlueprintFogSnapshotState.Submission(1, 8, next.Sequence, next.Tick, next.EmissionRevision), BlueprintFogSnapshotState.Completion.Committed), "Reused owner accepted an old generation.");
            Require(!first.Complete(new BlueprintFogSnapshotState.Submission(1, 7, next.Sequence, 8, 2), BlueprintFogSnapshotState.Completion.Committed), "Wrong tick confirmed a step.");
            Require(!first.Complete(new BlueprintFogSnapshotState.Submission(1, 7, next.Sequence, 9, 3), BlueprintFogSnapshotState.Completion.Committed), "Wrong revision confirmed a step.");
            Reject(() => first.Complete(next, (BlueprintFogSnapshotState.Completion)99));
            Require(first.Complete(next, BlueprintFogSnapshotState.Completion.Rejected) && first.ConfirmedTick == 1 && !first.CanRecover(1), "Rejected step changed the old clock or allowed recovery.");
            Reject(() => first.Submit(2, 2)); first.Retire(); Require(first.CanReleaseResources, "Known rollback retained a retired owner forever.");
            CheckRetirement(BlueprintFogSnapshotState.Completion.Committed);
            CheckRetirement(BlueprintFogSnapshotState.Completion.Rejected);
            CheckRetirement(BlueprintFogSnapshotState.Completion.Unknown);
            var unknown = new BlueprintFogSnapshotState(9, 3); var uncertain = unknown.Submit(1, 0);
            Require(unknown.Complete(uncertain, BlueprintFogSnapshotState.Completion.Unknown) && unknown.RequiresGpuDrain && !unknown.CanRecover(0), "Readback error inferred rollback.");
            Require(!unknown.Complete(uncertain, BlueprintFogSnapshotState.Completion.Committed), "Duplicate reply repaired an uncertain commit.");
            Reject(unknown.ConfirmPoolTeardown); unknown.Retire(); Require(!unknown.CanReleaseResources, "Unknown commit released retired resources too early.");
            unknown.ConfirmPoolTeardown(); Require(unknown.CanReleaseResources, "Proven pool teardown did not release retirement.");
            var drained = new BlueprintFogSnapshotState(10, 1); var late = drained.Submit(1, 0); drained.Retire(); drained.ConfirmPoolTeardown();
            Require(!drained.Complete(late, BlueprintFogSnapshotState.Completion.Committed), "Callback revived a owner whose pool was torn down.");
            Reject(() => new BlueprintFogSnapshotState(0, 1)); Reject(() => new BlueprintFogSnapshotState(1, 0));
            Reject(() => new BlueprintFogSnapshotState(1, 1, -1)); Reject(() => new BlueprintFogSnapshotState(1, 1, 2592001));
            var clock = new BlueprintFogSnapshotState(11, 1, 2591999);
            Reject(() => clock.Submit(2591999, 0)); Reject(() => clock.Submit(2592001, 0)); Reject(() => clock.Submit(2592000, -1));
            Require(clock.Complete(clock.Submit(2592000, 3), BlueprintFogSnapshotState.Completion.Committed), "Maximum legal clock rejected.");
            Reject(() => new BlueprintFogSnapshotState(12, 1).Submit(9, 0));
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(8, 24859) == 98303728, "Fixed pool byte accounting changed.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(8, 24860) > 98304000, "One extra block hid a budget overflow.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(8, 24576) == 98157700, "Rounded capacity lost its explicit reserve.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(32, 1) > 98304000, "Owner capacity was confused with actual field admission.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, 7) == 94478116, "Paged pool omitted logical metadata, ledger or shared upload.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, 8) == 99393316, "An eighth resident velocity hid a budget overflow.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, 7, BlueprintFogLedgerLayout.Spatial3D) == 98041636, "Spatial owner/stage ledgers escaped the complete reserve.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, 8, BlueprintFogLedgerLayout.Spatial3D) > 98304000, "Spatial eighth resident must exceed the budget.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(32, 32768, 4, BlueprintFogLedgerLayout.Spatial3D) == 97423684 &&
                BlueprintFogSnapshotState.EstimateFieldBytes(32, 32768, 5, BlueprintFogLedgerLayout.Spatial3D) == 102338884,
                "House reservation no longer selects four residents under the normal cap.");
            Require(BlueprintFogSnapshotState.EstimateFieldBytes(32, 32768, 32, BlueprintFogLedgerLayout.Spatial3D) == 230134084,
                "Full GPU residency must remove the shared upload reserve without losing owner fields.");
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(32, 98304000 / 516 + 1, 32, BlueprintFogLedgerLayout.Spatial3D));
            Require(BlueprintFogSnapshotState.LedgerCells(BlueprintFogLedgerLayout.Spatial3D) == 16384 && BlueprintFogSnapshotState.LedgerCells(BlueprintFogLedgerLayout.LegacyYZ) == 1024, "Ledger layouts changed.");
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(1, 1, 1, (BlueprintFogLedgerLayout)123));
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, 29));
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(28, 8192, -1));
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(0, 1)); Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(33, 1));
            Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(8, 0)); Reject(() => BlueprintFogSnapshotState.EstimateFieldBytes(8, int.MaxValue));
            CheckReservationOracle();
        }

        private static void CheckRetirement(BlueprintFogSnapshotState.Completion completion)
        {
            var owner = new BlueprintFogSnapshotState(3, 5); var receipt = owner.Submit(1, 1);
            owner.Retire(); owner.Retire(); Require(owner.State == BlueprintFogSnapshotState.Phase.Retiring && !owner.CanReleaseResources, "Pending owner was freed.");
            Require(owner.Complete(receipt, completion) && !owner.CanRecover(1), "Retired owner was revived or receipt ignored.");
            Require(owner.CanReleaseResources == (completion != BlueprintFogSnapshotState.Completion.Unknown), "Retirement ignored commit certainty.");
            if (!owner.CanReleaseResources) owner.ConfirmPoolTeardown();
            Reject(() => owner.Submit(2, 2)); Require(owner.CanReleaseResources, "Retired resources remain unavailable after drain.");
        }

        // CPU oracle for the next GPU reservation kernels, not evidence that those kernels exist or run.
        private sealed class Arena
        {
            internal readonly Stack<int> Free;
            internal readonly Dictionary<int, int[]> Current = new Dictionary<int, int[]>();
            internal Arena(int capacity) { Free = new Stack<int>(Enumerable.Range(0, capacity)); }
            internal int[] Reserve(int count) => count > Free.Count ? null : Enumerable.Range(0, count).Select(_ => Free.Pop()).ToArray();
            internal void Finish(int owner, int[] next, bool packed)
            {
                if (next == null) return;
                int[] release = next;
                if (packed) { Current.TryGetValue(owner, out release); Current[owner] = next; }
                if (release != null) foreach (int block in release) Free.Push(block);
            }
            internal void Check(int capacity, params int[][] pending)
            {
                int[] all = Free.Concat(Current.Values.SelectMany(v => v)).Concat(pending.Where(v => v != null).SelectMany(v => v)).ToArray();
                Require(all.Length == capacity && all.Distinct().Count() == capacity && all.All(v => v >= 0 && v < capacity), "Pool duplicated, leaked or evicted a block.");
            }
        }
        private static void CheckReservationOracle()
        {
            var pool = new Arena(9); pool.Finish(1, pool.Reserve(3), true); pool.Finish(2, pool.Reserve(2), true); pool.Check(9);
            int[] old = pool.Current[1].ToArray(); int available = pool.Free.Count;
            Require(pool.Reserve(available + 1) == null && pool.Free.Count == available && pool.Current[1].SequenceEqual(old), "One-block shortage mutated the old state.");
            int[] failed = pool.Reserve(2); pool.Check(9, failed); pool.Finish(1, failed, false); pool.Check(9);
            Require(pool.Current[1].SequenceEqual(old), "Packing failure replaced current blocks.");
            int[] next = pool.Reserve(4); pool.Check(9, next); pool.Finish(1, next, true); pool.Check(9);
            Require(pool.Current[1].SequenceEqual(next) && pool.Free.Count == 3 && pool.Current[2].Length == 2, "Commit returned the wrong owner's blocks.");
            int[] other = pool.Reserve(2); pool.Check(9, other); pool.Finish(2, other, true); pool.Check(9);
            for (int i = 0; i < 1000; ++i)
            {
                int owner = i % 2 + 1, count = i % 4;
                int[] reserved = pool.Reserve(count); pool.Check(9, reserved); pool.Finish(owner, reserved, i % 3 != 0); pool.Check(9);
            }
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; } catch (InvalidOperationException) { return; }
            throw new InvalidOperationException("Invalid fog snapshot operation accepted.");
        }
    }
}
