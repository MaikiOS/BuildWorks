using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using UnityEngine.Rendering;

// Faults enter the real receipt boundary; no second owner/scheduler implementation lives here.
internal static class FogRuntimeChannelLifecycleCheck
{
    internal static int Run(BlueprintFogAssets assets, BlueprintFogVisual source)
    {
        int cases = 0;
        BlueprintFogInputQueue.Sample Sample(float clock) => new BlueprintFogInputQueue.Sample(clock, new Point3(-1.8, .95, 0), .3f, .95f, true, true, true);
        BlueprintFogSimulation Make(BlueprintFogScratch scratch, BlueprintFogSimulation.StagedState staged, float clock = 0)
            => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, new Vector3(-1.8f, .95f, 0), 37,
                initialClock: clock, scratch: scratch, stagedState: staged);
        void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); ++cases; }
        void Reject(Action action, string message) { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, message); }
        void Drain(BlueprintFogChannels pool) { AsyncGPUReadback.WaitAllRequests(); if (!pool.PollSnapshots()) throw new InvalidOperationException("Runtime receipt did not complete."); }

        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        {
            var a = pool.AcquireSnapshot(0, Sample(1.125f), (scratch, staged) => Make(scratch, staged, 1.125f));
            Check(a.State.ConfirmedTick == 33 && a.Inputs.Committed.Clock == 1.125f, "Nonzero initial clock was reset.");
            Reject(() => pool.AcquireSnapshot(1, Sample(1.125f), (scratch, staged) => a.simulation), "Duplicate solver accepted.");
            Reject(() => pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => Make(scratch,
                new BlueprintFogSimulation.StagedState(staged.Origin, staged.Colour, staged.Surface, staged.Away, staged.Peak, staged.State))), "Wrong borrowed fields accepted.");
            Reject(() => pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => { pool.CaptureSnapshot(a, Sample(1.13f)); return null; }), "Factory reentry accepted.");
            var b = pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => Make(scratch, staged));
            Check(pool.Count == 2 && pool.FieldBytes == pool.SnapshotArena.ReservedFieldBytes, "Owner reservation/failed acquisition leaked.");
            Reject(() => pool.TryAcquire("legacy", () => null, out var unused), "Ordinary channel API accepted strict pool.");
            pool.CaptureSnapshot(a, Sample(1.13f)); pool.TrySubmitSnapshot(a, out var metadata);
            AsyncGPUReadback.WaitAllRequests();
            Check(a.HasReceipt && a.State.State == BlueprintFogSnapshotState.Phase.Pending, "Native callback confirmed outside Poll.");
            Check(pool.PollSnapshots() && a.Bank == 0 && a.Inputs.Count == 0 && a.LastSubmission.Sequence == metadata.Sequence, "Metadata-only receipt did not confirm independently.");
            pool.CaptureSnapshot(a, Sample(1.205f)); pool.TrySubmitSnapshot(a, out var old);
            pool.CaptureSnapshot(b, Sample(.105f)); pool.TrySubmitSnapshot(b, out var neighbour);
            Check(!pool.TrySubmitSnapshot(a, out var duplicate), "Pending owner submitted twice.");
            pool.RetireSnapshot(a); Check(!pool.TryReleaseSnapshot(a), "Pending owner released early.");
            Drain(pool);
            Check(a.Bank == 1 && a.State.CanReleaseResources && b.LastSubmission.Sequence == neighbour.Sequence, "Retirement lost committed bank or blocked neighbour.");
            Check(!pool.TryReleaseSnapshot(a) && pool.Count == 2, "GPU release skipped its confirmation.");
            Reject(() => pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged)), "Unconfirmed release reused a slot.");
            AsyncGPUReadback.WaitAllRequests();
            Check(pool.TryReleaseSnapshot(a) && pool.Count == 1, "Confirmed retired owner did not release.");
            var fresh = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            Reject(() => pool.CaptureSnapshot(a, Sample(1.21f)), "Stale handle used a reused slot.");
            pool.CaptureSnapshot(fresh, Sample(.105f)); pool.TrySubmitSnapshot(fresh, out var current);
            fresh.Receive(old, null); a.Receive(old, null);
            Check(!fresh.HasReceipt && fresh.Generation != a.Generation, "Late receipt changed the reused owner.");
            fresh.Receive(current, null); Drain(pool);
            Check(pool.RequiresSnapshotTeardown && fresh.State.State == BlueprintFogSnapshotState.Phase.Faulted && fresh.Inputs.Count == 1, "Unknown result inferred rollback or lost inputs.");
            Reject(() => pool.CaptureSnapshot(b, Sample(.11f)), "Unknown pool accepted neighbour work.");
            pool.RetireSnapshot(fresh); Check(!pool.TryReleaseSnapshot(fresh), "Unknown commit recycled an individual owner.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            pool.CaptureSnapshot(owner, Sample(.105f)); pool.TrySubmitSnapshot(owner, out var token); pool.Dispose();
            Check(pool.Count == 0 && pool.FieldBytes == 0 && owner.State.CanReleaseResources && !owner.Velocity, "Pending Dispose leaked owner resources.");
            owner.Receive(token, null); Check(owner.State.CanReleaseResources, "Late callback revived a disposed pool.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            var other = pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => Make(scratch, staged));
            pool.CaptureSnapshot(owner, Sample(.105f)); pool.TrySubmitSnapshot(owner, out var token);
            var words = new uint[] { (uint)token.Owner, (uint)(token.Owner >> 32), (uint)token.Generation, (uint)(token.Generation >> 32),
                (uint)token.Sequence, (uint)(token.Sequence >> 32), (uint)token.Tick, (uint)token.EmissionRevision, 4, 28800, 0, 0, 0, 0, 0, 0 };
            owner.Receive(token, words); words[8] = 0; Drain(pool);
            Check(pool.RequiresSnapshotTeardown && owner.LastFlags == 4, "Allocator damage was local or callback data was not copied.");
            Reject(() => pool.CaptureSnapshot(other, Sample(.105f)), "Corrupt allocator accepted neighbour work.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        {
            Reject(() => pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => {
                var simulation = Make(scratch, staged); UnityEngine.Object.DestroyImmediate(pool.SnapshotArena.Program); return simulation;
            }), "GPU initialization fault accepted.");
            Check(pool.RequiresSnapshotTeardown && pool.Count == 1, "Initialization fault lost resources before a proven drain.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged)); pool.RetireSnapshot(owner);
            UnityEngine.Object.DestroyImmediate(pool.SnapshotArena.Program);
            Reject(() => pool.TryReleaseSnapshot(owner), "Retirement fault accepted.");
            Check(pool.RequiresSnapshotTeardown && pool.Count == 1 && owner.Velocity, "Retirement fault freed unknown GPU ownership.");
            Check(!pool.TryReleaseSnapshot(owner), "Partly failed retirement was retried.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            var buffers = (Dictionary<string, ComputeBuffer>)pool.SnapshotArena.Buffers;
            var control = buffers["_Control"]; control.GetData(new uint[16]); control.Release();
            Reject(pool.Dispose, "Failed drain was accepted.");
            Check(pool.Count == 1 && pool.FieldBytes > 0 && owner.Velocity, "Failed drain forgot retained resources.");
            // Restore only the fence target; no subsequent compute dispatch uses the deliberately released control.
            buffers["_Control"] = new ComputeBuffer(16, 4); pool.Dispose();
            Check(pool.Count == 0 && pool.FieldBytes == 0 && !owner.Velocity, "Second Dispose masked the failed drain as a no-op.");
        }
        foreach (int corruption in new[] { 0, 1, 2 }) using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            pool.CaptureSnapshot(owner, Sample(.105f)); pool.TrySubmitSnapshot(owner, out var token);
            var words = new uint[] { (uint)token.Owner, (uint)(token.Owner >> 32), (uint)token.Generation, (uint)(token.Generation >> 32),
                (uint)token.Sequence, (uint)(token.Sequence >> 32), (uint)token.Tick, (uint)token.EmissionRevision, 0, 28800, 0, 0, 0, 0, 0, 0 };
            if (corruption == 0) ++words[0]; else if (corruption == 1) words[9] = 0; else words[8] = 16;
            owner.Receive(token, words); Drain(pool);
            Check(pool.RequiresSnapshotTeardown && owner.LastCompletion == BlueprintFogSnapshotState.Completion.Unknown, "Damaged receipt authorized resource reuse.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            var other = pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => Make(scratch, staged));
            pool.RetireSnapshot(owner); pool.TryReleaseSnapshot(owner);
            var damaged = new uint[16]; damaged[2] = 4; owner.ReceiveRelease(damaged); AsyncGPUReadback.WaitAllRequests();
            Check(!pool.TryReleaseSnapshot(owner) && pool.RequiresSnapshotTeardown && owner.Velocity, "Damaged retirement freed owner resources.");
            Check(!pool.TryReleaseSnapshot(owner) && pool.Count == 2, "Damaged retirement retried the free-list mutation.");
            Reject(() => pool.CaptureSnapshot(other, Sample(.105f)), "Retirement damage allowed neighbour work.");
        }
        using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
        {
            var owner = pool.AcquireSnapshot(0, Sample(0), (scratch, staged) => Make(scratch, staged));
            var other = pool.AcquireSnapshot(1, Sample(0), (scratch, staged) => Make(scratch, staged));
            pool.RetireSnapshot(owner); pool.TryReleaseSnapshot(owner);
            pool.SnapshotArena.Program.SetInt("_InjectPackFailure", 1);
            pool.CaptureSnapshot(other, Sample(.105f)); pool.TrySubmitSnapshot(other, out var token);
            pool.SnapshotArena.Program.SetInt("_InjectPackFailure", 0); Drain(pool);
            Check(other.LastFlags == 8 && pool.TryReleaseSnapshot(owner) && !pool.RequiresSnapshotTeardown,
                "Retirement consumed its neighbour's later control flags.");
        }
        return cases;
    }
}
