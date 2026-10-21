using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
        // Each admitted part retains its logical slot; only confirmed velocity pages may leave GPU memory.
        internal sealed class SnapshotSource
        {
            internal readonly BlueprintFogInputQueue Inputs;
            internal SnapshotChannel Snapshot { get; set; }
            internal bool Released { get; private set; }
            internal string Error { get; set; }
            internal bool Removed;
            internal readonly Func<BlueprintFogScratch, BlueprintFogSimulation.StagedState, BlueprintFogInputQueue.Sample, BlueprintFogSimulation> Create;
            private readonly Action release;
            internal SnapshotSource(BlueprintFogInputQueue.Sample initial,
                Func<BlueprintFogScratch, BlueprintFogSimulation.StagedState, BlueprintFogInputQueue.Sample, BlueprintFogSimulation> create, Action release)
            { Inputs = new BlueprintFogInputQueue(initial); Create = create; this.release = release; }
            internal void Close()
            {
                if (Released) return;
                Released = true; Snapshot = null; release?.Invoke();
            }
        }

        private readonly List<SnapshotSource> sources = new List<SnapshotSource>();
        private int sourceCursor;
        private bool pumpingSources;
        internal int RetiringSources
        { get { int count = 0; foreach (var source in sources) if (source.Removed && !source.Released) ++count; return count; } }

        internal SnapshotSource RegisterSnapshotSource(BlueprintFogInputQueue.Sample initial,
            Func<BlueprintFogScratch, BlueprintFogSimulation.StagedState, BlueprintFogInputQueue.Sample, BlueprintFogSimulation> create, Action release = null)
        {
            RequireSnapshots();
            if (pumpingSources || snapshotTeardown || sources.Count >= capacity || create == null)
                throw new InvalidOperationException("Fog logical source registration unavailable.");
            var source = new SnapshotSource(initial, create, release); sources.Add(source); return source;
        }

        private void RequireSource(SnapshotSource source)
        {
            RequireSnapshots();
            if (pumpingSources) throw new InvalidOperationException("Fog source callback must not re-enter scheduling.");
            if (source == null || source.Removed || source.Released || !sources.Contains(source))
                throw new ArgumentException("Fog logical source is closed or belongs to another pool.");
        }

        internal void CaptureSource(SnapshotSource source, BlueprintFogInputQueue.Sample sample)
        {
            RequireSource(source);
            if (snapshotTeardown || source.Error != null) throw new InvalidOperationException("Stopped fog source retains its accepted history.");
            source.Inputs.Capture(sample);
        }

        internal void RemoveSource(SnapshotSource source)
        {
            RequireSource(source); source.Removed = true;
            if (source.Snapshot == null && !snapshotTeardown) { sources.Remove(source); source.Close(); }
        }

        internal void PumpSources()
        {
            RequireSnapshots();
            if (pumpingSources || snapshotTeardown) throw new InvalidOperationException("Fog source scheduling is stopped or re-entered.");
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
            receiptBatch = QaImmediateReceiptReads ? null : new List<ReceiptRead>(capacity);
#else
            receiptBatch = new List<ReceiptRead>(capacity);
#endif
            pumpingSources = true;
            try
            {
                PollSnapshots();
                if (snapshotTeardown) throw new InvalidOperationException("Fog source receipt requires whole-pool teardown.");
                // Confirm explicitly removed sources' GPU retirement before scheduling accepted observations.
                foreach (var source in sources)
                {
                    var owner = source.Snapshot;
                    if (source.Removed && owner != null) owner.State.Retire();
                    if (owner == null || (owner.State.State != BlueprintFogSnapshotState.Phase.Retired && owner.State.State != BlueprintFogSnapshotState.Phase.Retiring)) continue;
                    if (!TryReleaseSnapshot(owner)) continue;
                    source.Inputs.Detach(owner.State); source.Snapshot = null;
                }
                int count = sources.Count;
                int start = sourceCursor;
                for (int visit = 0; visit < count; ++visit)
                {
                    int index = (start + visit) % count;
                    var source = sources[index]; var owner = source.Snapshot;
                    if (source.Removed) { if (owner == null) source.Close(); continue; }
                    if (source.Inputs.Faulted || (owner != null && owner.State.State == BlueprintFogSnapshotState.Phase.Faulted))
                    {
                        source.Error = "Fog source failed: inputFaulted=" + source.Inputs.Faulted + ", state=" + owner?.State.State +
                            ", flags=" + owner?.LastFlags + ", queued=" + source.Inputs.Count + ", pending=" + source.Inputs.PendingCount +
                            "; published state and accepted history retained.";
                        continue;
                    }
                    if (owner == null)
                    {
                        // Start at the original baseline, including every accepted idle observation.
                        if (source.Inputs.Count == 0) continue;
                        int slot = Array.FindIndex(snapshotSlots, value => value == null);
                        if (slot < 0) continue;
                        var initial = source.Inputs.Committed;
                        source.Snapshot = owner = AcquireSnapshot(slot, initial, (scratch, staged) => source.Create(scratch, staged, initial), source.Inputs);
                        sourceCursor = (index + 1) % count;
                    }
                    if (owner.State.State != BlueprintFogSnapshotState.Phase.Ready) continue;
                    // An empty/recovered surface does not reset the v11 background flow or CPU integrator.
                    // Only explicit source removal may retire this generation.
                    if (TrySubmitSnapshot(owner, out var token)) sourceCursor = (index + 1) % count;
                }
            }
            finally
            {
                try { FlushReceiptBatch(); }
                finally
                {
                    receiptBatch = null;
                    sources.RemoveAll(source => source.Released);
                    if (sources.Count == 0) sourceCursor = 0; else sourceCursor %= sources.Count;
                    pumpingSources = false;
                }
            }
        }

        private void CloseSources()
        {
            List<Exception> errors = null;
            foreach (var source in sources) try { source.Close(); }
                catch (Exception error) { (errors ?? (errors = new List<Exception>())).Add(error); }
            sources.Clear();
            if (errors != null) throw new AggregateException("Fog source cleanup failed after GPU drain.", errors);
        }
    }
}
