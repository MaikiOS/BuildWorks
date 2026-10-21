using System.Diagnostics;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
        private FogPerformanceCounters performance;
        internal void EnablePerformanceProbe()
        {
            RequireSnapshots();
            if (performance != null || snapshotCount != 0 || pumpingSources || creating) throw new System.InvalidOperationException("Enable measurement before creating snapshot owners.");
            performance = new FogPerformanceCounters(Stopwatch.Frequency);
        }
        internal FogPerformanceCounters.Snapshot ReadPerformanceProbe() => performance?.Read();
#if BUILDWORKS_TEST_STAND
        [System.Serializable] internal sealed class ReceiptInput
        {
            public int queued, pending, confirmedTick, capturedTick;
            public bool faulted;
        }
        internal ReceiptInput[] ReadReceiptInputs()
        {
            var result = new ReceiptInput[sources.Count];
            for (int i = 0; i < sources.Count; ++i)
            {
                var source = sources[i]; var input = source.Inputs;
                float lastClock = input.Count > 0 ? input.At(input.Count - 1).Clock : input.Committed.Clock;
                result[i] = new ReceiptInput { queued = input.Count, pending = input.PendingCount,
                    capturedTick = UnityEngine.Mathf.FloorToInt(lastClock / BlueprintFogSimulation.Step + .0001f),
                    confirmedTick = source.Snapshot?.State.ConfirmedTick ?? 0,
                    faulted = input.Faulted || source.Error != null || source.Snapshot?.Error != null };
            }
            return result;
        }
        internal bool ReceiptInputLimitReached()
        {
            foreach (var source in sources)
                if (source.Inputs.Count >= 48 || source.Inputs.Faulted || source.Error != null || source.Snapshot?.Error != null) return true;
            return false;
        }
#endif
#endif
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfReceiptReadback(long bytes)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.ReceiptReadback(bytes);
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfStart(ref long time)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            if (performance != null) time = Stopwatch.GetTimestamp();
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfElapsed(FogPerformanceCounters.Stage stage, long start)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.Elapsed(stage, start, Stopwatch.GetTimestamp());
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfRequested(SnapshotChannel owner, FogPerformanceCounters.Kind kind, BlueprintFogSnapshotState.Submission stamp, long bytes)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.Requested(owner.Slot, stamp.Owner, stamp.Generation, stamp.Sequence, kind, bytes, Stopwatch.GetTimestamp());
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfReceived(SnapshotChannel owner, FogPerformanceCounters.Kind kind, BlueprintFogSnapshotState.Submission stamp, long copiedBytes)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.Received(owner.Slot, stamp.Owner, stamp.Generation, stamp.Sequence, kind, copiedBytes, Stopwatch.GetTimestamp());
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfAccepted(SnapshotChannel owner, FogPerformanceCounters.Kind kind, BlueprintFogSnapshotState.Submission stamp, bool accepted)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.Accepted(owner.Slot, stamp.Owner, stamp.Generation, stamp.Sequence, kind, accepted, Stopwatch.GetTimestamp());
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfVelocityAccepted(SnapshotChannel owner, bool accepted)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            if (owner.evictionRelease && owner.evictionReply != null && ReferenceEquals(owner.evictionReply, owner.coldVelocity) && SnapshotChannel.Same(owner.evictionStamp, owner.coldVelocityStamp)) return;
            PerfAccepted(owner, FogPerformanceCounters.Kind.Velocity, owner.evictionStamp, accepted);
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfFailed(SnapshotChannel owner, FogPerformanceCounters.Kind kind, BlueprintFogSnapshotState.Submission stamp)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.SubmissionFailed(owner.Slot, stamp.Owner, stamp.Generation, stamp.Sequence, kind);
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfBatch(int count, int ticks, bool metadata)
        {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.Batch(count, ticks, metadata);
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfCached() {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.CachedPage();
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfNoVictim() {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.NoVictim();
#endif
        }
        [Conditional("BUILDWORKS_TEST_STAND"), Conditional("UNITY_EDITOR")]
        private void PerfWaiting() {
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            performance?.WaitingVelocity();
#endif
        }
    }
}
