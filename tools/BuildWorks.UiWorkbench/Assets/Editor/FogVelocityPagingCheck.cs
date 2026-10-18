using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

// Fixture waits are deliberate; frame throughput and house rendering have separate gates.
public static class FogVelocityPagingCheck
{
    internal static IEnumerator Frames(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, FogSparseStorageProbe codec, string diagnostic = null, Action<double> measured = null, int sourceCount = 3, int residentLimit = 1, bool expectOverlapShortage = false, Action<int> contacts = null)
    {
        Vector3 Position(int slot, int tick) => new Vector3(Mathf.Lerp(-1.8f, .4f, tick / 96f) - (sourceCount > 3 && !expectOverlapShortage ? slot % 7 * .8f : 0),
                .95f, .03f - (sourceCount > 3 && !expectOverlapShortage ? slot / 7 * .5f : 0));
        BlueprintFogInputQueue.Sample Sample(int slot, int tick)
        {
            var current = Position(slot, tick);
            return new BlueprintFogInputQueue.Sample(tick * BlueprintFogSimulation.Step, new Point3(current.x, current.y, current.z),
                body.x, body.y, true, source.Source.MightContact(Position(slot, Math.Max(0, tick - 1)), current, body), true);
        }
        Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
        BlueprintFogSimulation Make(int slot, BlueprintFogScratch scratch = null, BlueprintFogSimulation.StagedState staged = null)
            => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, Actor(Sample(slot, 0)), 37 + slot,
                scratch: scratch, stagedState: staged);
        using (var pool = BlueprintFogChannels.CreateArena(assets, sourceCount, 8192, residentOwners: residentLimit))
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            int maximumContacts = 0;
            if (diagnostic != null)
                foreach (var spacing in new[] { new Vector2(.6f, .7f), new Vector2(.8f, .7f), new Vector2(.6f, .5f), new Vector2(.8f, .5f) })
                {
                    int peak = 0;
                    for (int tick = 1; tick <= 96; ++tick)
                    {
                        int count = 0;
                        for (int slot = 0; slot < 28; ++slot)
                        {
                            Vector3 local = new Vector3(Mathf.Lerp(-1.8f, .4f, tick / 96f) - slot % 7 * spacing.x, .95f, .03f - slot / 7 * spacing.y);
                            if (source.Source.MightContact(local - Vector3.right * (2.2f / 96), local, body)) ++count;
                        }
                        peak = Math.Max(peak, count);
                    }
                    File.AppendAllText(diagnostic, "grid=" + spacing + " contacts=" + peak + "\n");
                }
            var sources = new BlueprintFogChannels.SnapshotSource[sourceCount];
            for (int i = 0; i < sources.Length; ++i) { int slot = i; sources[i] = pool.RegisterSnapshotSource(Sample(slot, 0), (scratch, staged, baseline) => Make(slot, scratch, staged)); }
            for (int tick = 1; tick <= (diagnostic == null ? 96 : 16); ++tick)
            {
                // Editor player frames can exceed 250Hz; accepted physics observations still run at v11's 30Hz.
                while (diagnostic == null && timer.Elapsed.TotalSeconds < tick * BlueprintFogSimulation.Step)
                { Require(timer.Elapsed.TotalSeconds <= 5, "Paging input exceeded its five-second deadline."); pool.PumpSources(); yield return null; }
                var rejected = sources.FirstOrDefault(s => s.Error != null);
                if (expectOverlapShortage && rejected != null)
                {
                    Require(rejected.Snapshot.LastFlags == 2 && rejected.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Faulted &&
                        rejected.Inputs.Count > 0, "Overlap failure was not a retained capacity rejection.");
                    measured?.Invoke(timer.Elapsed.TotalSeconds); yield break;
                }
                int touching = 0;
                for (int i = 0; i < sources.Length; ++i)
                { var sample = Sample(i, tick); if (sample.EmissionPossible) ++touching; pool.CaptureSource(sources[i], sample); }
                maximumContacts = Math.Max(maximumContacts, touching);
                pool.PumpSources();
                if (diagnostic != null) File.AppendAllText(diagnostic, tick + " ms=" + timer.Elapsed.TotalMilliseconds.ToString("F1", System.Globalization.CultureInfo.InvariantCulture) + ": " + string.Join("; ", sources.Select(s =>
                    $"slot={s.Snapshot.Slot} count={s.Inputs.Count} tick={s.Snapshot.State.ConfirmedTick} state={s.Snapshot.State.State} resident={(bool)s.Snapshot.Velocity} receipt={s.Snapshot.nativePending} page={s.Snapshot.evictionPending}/{s.Snapshot.evictionNativePending}/{s.Snapshot.evictionReceived}")) + "\n");
                Require(!pool.RequiresSnapshotTeardown && pool.ResidentSnapshotCount <= pool.SnapshotArena.ResidentCapacity && pool.FieldBytes <= 98304000, "Frame paging failed its bounded ownership.");
                yield return null;
            }
            if (diagnostic != null) yield break;
            Require(!expectOverlapShortage, "Overlap control did not reach the expected capacity rejection.");
            while (sources.Any(s => s.Inputs.Count != 0))
            { Require(timer.Elapsed.TotalSeconds <= 5, "Frame paging did not drain accepted observations."); pool.PumpSources(); yield return null; }
            Require(timer.Elapsed.TotalSeconds >= 96 * BlueprintFogSimulation.Step && timer.Elapsed.TotalSeconds <= 5, "Paging did not preserve real30Hz input cadence.");
            measured?.Invoke(timer.Elapsed.TotalSeconds);
            if (sourceCount == 28) Require(maximumContacts >= 6 && maximumContacts <= 8, "Spatial house fixture missed real contact concurrency.");
            contacts?.Invoke(maximumContacts);
            var verificationTimer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < sources.Length; ++i)
            {
                var owner = sources[i].Snapshot;
                while (!pool.EnsureSnapshotVelocity(owner))
                { Require(verificationTimer.Elapsed.TotalSeconds <= 5, "Frame paging could not promote confirmed velocity."); pool.PollSnapshots(); yield return null; }
                Require(owner.State.ConfirmedTick == 96 && owner.Inputs.PendingCount == 0 && owner.LastFlags == 0, "Frame paging dropped accepted history.");
                using (var reference = Make(i))
                {
                    for (int tick = 1; tick <= 96; ++tick) { var sample = Sample(i, tick); reference.AdvanceTo(sample.Clock, Actor(sample), body, sample.Emit, sample.EmissionPossible); }
                    foreach (string name in new[] { "previous", "inputPosition", "inputTime", "tick", "emissionRevision" })
                    {
                        var field = typeof(BlueprintFogSimulation).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
                        Require(Equals(field.GetValue(owner.simulation), field.GetValue(reference)), "Frame paging changed CPU integrator history.");
                    }
                    pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                    codec.BeginComparison(); var fields = pool.SnapshotArena.Scratch;
                    codec.Compare(fields.ReadColour, reference.Smoke); codec.Compare(fields.ReadOrigin, FogRuntimeSparseCheck.Current(reference, "origin"));
                    codec.Compare(fields.ReadSurface, reference.Surface); codec.Compare(owner.Velocity, reference.Flow);
                    for (int f = 0; f < 3; ++f) codec.Compare(owner.Ledger[f], FogRuntimeSparseCheck.Buffer(reference, new[] { "away", "peak", "state" }[f]));
                    codec.RequireClean();
                }
            }
        }
    }
    internal static int Run(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, FogSparseStorageProbe codec)
    {
        var initial = Sample(0);
        BlueprintFogInputQueue.Sample Sample(float time) => new BlueprintFogInputQueue.Sample(time,
            new Point3(Mathf.Lerp(-1.8f, .4f, time / 3), .95f, .03f), body.x, body.y, true, true, true);
        Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
        BlueprintFogSimulation Make(int slot, BlueprintFogScratch scratch = null, BlueprintFogSimulation.StagedState staged = null)
            => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, Actor(initial), 37 + slot,
                scratch: scratch, stagedState: staged);
        void Drain(BlueprintFogChannels pool) { AsyncGPUReadback.WaitAllRequests(); pool.PollSnapshots(); }
        void Resident(BlueprintFogChannels pool, BlueprintFogChannels.SnapshotChannel owner)
        {
            for (int i = 0; i < 8 && !pool.EnsureSnapshotVelocity(owner); ++i) Drain(pool);
            Require(owner.Velocity && !owner.evictionPending, "Paged owner could not restore its slot.");
        }
        int comparisons = 0;
        var references = new BlueprintFogSimulation[3];
        Texture3D upload;
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 3, 8192, residentOwners: 1))
            {
                upload = pool.SnapshotArena.VelocityUpload;
                var owners = new BlueprintFogChannels.SnapshotChannel[3];
                for (int i = 0; i < owners.Length; ++i)
                { int slot = i; references[i] = Make(slot); owners[i] = pool.AcquireSnapshot(slot, initial, (scratch, staged) => Make(slot, scratch, staged)); }
                Require(pool.ResidentSnapshotCount == 1 && pool.VelocityRamBytes == 9830400, "Initial cold owners were not reserved exactly.");
                for (int batch = 1; batch <= 12; ++batch)
                {
                    for (int visit = 0; visit < 3; ++visit)
                    {
                        int slot = batch % 2 == 0 ? visit : 2 - visit;
                        var owner = owners[slot]; var sample = Sample(batch * 8 * BlueprintFogSimulation.Step);
                        pool.CaptureSnapshot(owner, sample); Resident(pool, owner);
                        Require(pool.TrySubmitSnapshot(owner, out _), "Restored owner failed to submit queued history.");
                        references[slot].AdvanceTo(sample.Clock, Actor(sample), body, true, true); Drain(pool);
                        Require(owner.State.State == BlueprintFogSnapshotState.Phase.Ready && owner.Inputs.Count == 0 &&
                            owner.State.ConfirmedTick == batch * 8, "Paged step lost confirmed history.");
                        pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                        codec.BeginComparison(); var fields = pool.SnapshotArena.Scratch;
                        codec.Compare(fields.ReadColour, references[slot].Smoke);
                        codec.Compare(fields.ReadOrigin, FogRuntimeSparseCheck.Current(references[slot], "origin"));
                        codec.Compare(fields.ReadSurface, references[slot].Surface); codec.Compare(owner.Velocity, references[slot].Flow);
                        for (int f = 0; f < 3; ++f) codec.Compare(owner.Ledger[f], FogRuntimeSparseCheck.Buffer(references[slot], new[] { "away", "peak", "state" }[f]));
                        codec.RequireClean(); comparisons += 7;
                        var velocity = owner.Velocity;
                        var scalars = new[] { "previous", "inputPosition", "inputTime", "tick", "emissionRevision" }
                            .Select(name => typeof(BlueprintFogSimulation).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)).ToArray();
                        object[] before = scalars.Select(f => f.GetValue(owner.simulation)).ToArray();
                        Require(!pool.PageSnapshotVelocity(owner) && owner.Velocity == velocity, "Eviction released before callback confirmation.");
                        Require(!pool.TrySubmitSnapshot(owner, out _), "Paging accepted a new submission.");
                        var stamp = owner.evictionStamp;
                        bool received = owner.evictionReceived; var reply = owner.evictionReply;
                        owner.ReceiveVelocity(new BlueprintFogSnapshotState.Submission(stamp.Owner, stamp.Generation + 1, stamp.Sequence, stamp.Tick, stamp.EmissionRevision), null);
                        Require(owner.evictionReceived == received && ReferenceEquals(owner.evictionReply, reply), "Stale page callback was accepted.");
                        AsyncGPUReadback.WaitAllRequests(); Require(owner.Velocity == velocity, "Native callback freed a GPU texture outside Poll.");
                        pool.PollSnapshots(); Require(!owner.Velocity && owner.coldVelocity.Length == 2457600, "Confirmed page not retained in RAM.");
                        for (int f = 0; f < scalars.Length; ++f) Require(Equals(before[f], scalars[f].GetValue(owner.simulation)), "Paging changed a CPU integrator value.");
                        Require(pool.FieldBytes <= BlueprintFogArena.MaximumFieldBytes && pool.ResidentSnapshotCount <= 1, "Paging exceeded its field reservation.");
                    }
                }
                // A zero-step metadata update must restore cold velocity without resetting it.
                var last = owners[2]; Resident(pool, last);
                ushort[] beforeMetadata = FogRuntimeSparseCheck.Read(last.Velocity);
                Require(beforeMetadata.Any(value => value != 0), "Paging control has no accumulated velocity.");
                pool.PageSnapshotVelocity(last); Drain(pool);
                pool.CaptureSnapshot(last, Sample(last.Inputs.Committed.Clock + .001f)); Resident(pool, last);
                Require(pool.TrySubmitSnapshot(last, out _) && last.Inputs.PendingMetadataOnly, "Cold metadata update did not preserve zero-step semantics.");
                Drain(pool); Require(beforeMetadata.SequenceEqual(FogRuntimeSparseCheck.Read(last.Velocity)), "Metadata update reset paged velocity.");
            }
            Require(!upload, "Paging upload texture leaked after pool drain.");
        }
        finally { foreach (var reference in references) reference?.Dispose(); }

        using (var pool = BlueprintFogChannels.CreateArena(assets, 28, 8192))
        {
            for (int i = 0; i < 28; ++i) { int slot = i; pool.AcquireSnapshot(slot, initial, (scratch, staged) => Make(slot, scratch, staged)); }
            Require(pool.Count == 28 && pool.SnapshotArena.ResidentCapacity == 7 && pool.ResidentSnapshotCount == 7 &&
                pool.FieldBytes == 94478116 && pool.VelocityRamBytes == 103219200, "28 logical owners exceeded their exact bounded reservation.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192, residentOwners: 1))
        {
            var owner = pool.AcquireSnapshot(0, initial, (scratch, staged) => Make(0, scratch, staged));
            pool.CaptureSnapshot(owner, Sample(.1f)); var velocity = owner.Velocity;
            pool.PageSnapshotVelocity(owner); owner.evictionReply = null; owner.evictionReceived = true; Drain(pool);
            Require(pool.RequiresSnapshotTeardown && owner.Velocity == velocity && owner.Inputs.Count == 1 &&
                owner.Inputs.PendingCount == 0, "Failed page lost GPU state or accepted history.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192, residentOwners: 1))
        {
            var owner = pool.AcquireSnapshot(0, initial, (scratch, staged) => Make(0, scratch, staged));
            pool.PageSnapshotVelocity(owner); pool.RetireSnapshot(owner);
            Require(!pool.TryReleaseSnapshot(owner), "Retired owner released an in-flight page.");
            Drain(pool); Require(!pool.TryReleaseSnapshot(owner), "Retirement skipped map-release confirmation.");
            Drain(pool); Require(pool.TryReleaseSnapshot(owner), "Retirement could not release a confirmed cold page.");
        }
        return comparisons;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
}
