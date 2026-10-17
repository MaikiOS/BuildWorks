using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
        // A logical part keeps its accepted path while a physical slot retires or is unavailable.
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

        internal SnapshotSource RegisterSnapshotSource(BlueprintFogInputQueue.Sample initial,
            Func<BlueprintFogScratch, BlueprintFogSimulation.StagedState, BlueprintFogInputQueue.Sample, BlueprintFogSimulation> create, Action release = null)
        {
            RequireSnapshots();
            if (pumpingSources || snapshotTeardown || sources.Count >= 32 || create == null)
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
            if (source.Snapshot == null && source.Inputs.Count == 0 && !(sample.Emit && sample.EmissionPossible)) source.Inputs.TrackDormant(sample);
            else source.Inputs.Capture(sample);
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
            pumpingSources = true;
            try
            {
                PollSnapshots();
                if (snapshotTeardown) throw new InvalidOperationException("Fog source receipt requires whole-pool teardown.");
                // Confirm every retirement before choosing the next waiting source; a retiring source cannot seize its own slot again.
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
                    { source.Error = "Fog source failed; published state and accepted history retained."; continue; }
                    if (owner == null)
                    {
                        // shortcut: replay accepted idle tails in a new slot; verify cold-state fast-forward before house FPS acceptance.
                        if (source.Inputs.Count == 0) continue;
                        int slot = Array.FindIndex(snapshotSlots, value => value == null);
                        if (slot < 0) continue;
                        var initial = source.Inputs.Committed;
                        source.Snapshot = owner = AcquireSnapshot(slot, initial, (scratch, staged) => source.Create(scratch, staged, initial), source.Inputs);
                        sourceCursor = (index + 1) % count;
                    }
                    if (owner.State.State != BlueprintFogSnapshotState.Phase.Ready) continue;
                    var last = source.Inputs.Committed;
                    bool contact = source.Inputs.HasEmission || (source.Inputs.Count == 0 && last.Emit && last.EmissionPossible);
                    if (owner.LastSubmission.Sequence != 0 && !contact)
                    {
                        // Freeze submissions, not capture, until this exact committed recovery observation arrives.
                        bool recovered = ObserveSnapshotRecovery(owner);
                        if (!owner.recoveryReceived || !SnapshotChannel.Same(owner.recoveryStamp, owner.LastSubmission)) continue;
                        if (recovered) { owner.State.Retire(); continue; }
                    }
                    TrySubmitSnapshot(owner, out var token);
                }
            }
            finally
            {
                sources.RemoveAll(source => source.Released);
                if (sources.Count == 0) sourceCursor = 0; else sourceCursor %= sources.Count;
                pumpingSources = false;
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
