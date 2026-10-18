using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
        [Serializable] internal sealed class SnapshotFault
        {
            public int slot, tick, emissionRevision, confirmedTick, queued, pending, expectedBase;
            public long owner, generation, sequence;
            public bool receiptValid, metadataOnly;
            public float committedClock, firstClock, lastClock;
            public uint[] receipt;
            public string reason;
        }
        internal SnapshotFault FirstSnapshotFault { get; private set; }

        // Strict snapshots never expose the speculative solver as an ordinary committed simulation.
        internal sealed class SnapshotChannel
        {
            internal readonly int Slot;
            internal readonly long Identity, Generation;
            internal readonly BlueprintFogInputQueue Inputs;
            internal BlueprintFogSnapshotState State { get; }
            internal RenderTexture Velocity;
            internal readonly ComputeBuffer[] Ledger = new ComputeBuffer[3];
            internal BlueprintFogSnapshotState.Submission LastSubmission { get; set; }
            internal BlueprintFogSnapshotState.Completion LastCompletion { get; set; }
            internal uint LastFlags { get; set; }
            internal string Error { get; set; }
            internal bool HasReceipt => received;
            internal int Bank { get; set; }
            internal BlueprintFogSimulation simulation;
            internal BlueprintFogSnapshotState.Submission pending;
            internal bool received, pendingBatch, nativePending; private bool released;
            internal uint[] reply;
            internal bool releasePending, releaseReceived;
            internal uint[] releaseReply;
            internal AsyncGPUReadbackRequest request;
            internal AsyncGPUReadbackRequest recoveryRequest;
            internal bool recoveryNativePending, recoveryReceived;
            internal ushort[] coldVelocity, evictionReply;
            internal bool evictionPending, evictionNativePending, evictionReceived;
            internal AsyncGPUReadbackRequest evictionRequest;
            internal BlueprintFogSnapshotState.Submission evictionStamp;
            internal BlueprintFogSnapshotState.Submission coldVelocityStamp;
            internal bool evictionRelease;
            internal long velocityUse;
            internal uint? recoveryFlags;
            internal BlueprintFogSnapshotState.Submission recoveryStamp;

            internal SnapshotChannel(int slot, long identity, long generation, BlueprintFogInputQueue.Sample initial, BlueprintFogInputQueue inputs, bool resident = true)
            {
                Slot = slot; Identity = identity; Generation = generation;
                State = new BlueprintFogSnapshotState(identity, generation, Mathf.FloorToInt(initial.Clock / BlueprintFogSimulation.Step + .0001f));
                Inputs = inputs ?? new BlueprintFogInputQueue(State, initial);
                LastSubmission = new BlueprintFogSnapshotState.Submission(identity, generation, 0, State.ConfirmedTick, 0);
                try
                {
                    if (resident) Velocity = BlueprintFogSimulation.AllocateField(new Vector3Int(96, 80, 80), "Fog committed velocity " + slot);
                    else coldVelocity = new ushort[VelocityWords];
                    for (int i = 0; i < Ledger.Length; ++i) { Ledger[i] = new ComputeBuffer(i == 2 ? 1 : 1024, 4); Ledger[i].SetData(new uint[i == 2 ? 1 : 1024]); }
                }
                catch { Release(); throw; }
            }

            // Native callbacks copy data here; only PollSnapshots may confirm or mutate GPU ownership.
            internal void Receive(BlueprintFogSnapshotState.Submission token, uint[] words)
            {
                if (released || !pendingBatch || received || !Same(token, pending)) return;
                reply = words == null ? null : (uint[])words.Clone(); received = true;
            }
            internal void ReceiveRelease(uint[] words)
            {
                if (released || !releasePending || releaseReceived) return;
                releaseReply = words == null ? null : (uint[])words.Clone(); releaseReceived = true;
            }
            internal void ReceiveRecovery(BlueprintFogSnapshotState.Submission stamp, uint? flags)
            {
                if (released || recoveryReceived || !Same(stamp, recoveryStamp)) return;
                recoveryFlags = flags; recoveryReceived = true;
            }
            internal void ReceiveVelocity(BlueprintFogSnapshotState.Submission stamp, ushort[] words)
            {
                if (released || !evictionPending || evictionReceived || !Same(stamp, evictionStamp)) return;
                evictionReply = words; evictionReceived = true;
            }
            internal static bool Same(BlueprintFogSnapshotState.Submission a, BlueprintFogSnapshotState.Submission b)
                => a.Owner == b.Owner && a.Generation == b.Generation && a.Sequence == b.Sequence && a.Tick == b.Tick && a.EmissionRevision == b.EmissionRevision;
            internal void Release()
            {
                if (released) return;
                simulation?.Dispose(); simulation = null;
                foreach (var buffer in Ledger) buffer?.Release();
                if (Velocity) { Velocity.Release(); UnityEngine.Object.DestroyImmediate(Velocity); }
                coldVelocity = evictionReply = null;
                released = true;
            }
        }

        private BlueprintFogArena arena;
        private SnapshotChannel[] snapshotSlots;
        private long[] generations;
        private long nextIdentity;
        private int snapshotCount;
        private bool snapshotTeardown, snapshotsDisposed;
        internal BlueprintFogArena SnapshotArena => arena;
        internal bool RequiresSnapshotTeardown => snapshotTeardown;
        internal int SnapshotCapacity { get { RequireSnapshots(); return capacity; } }

        internal void WriteSnapshotBinding(SnapshotChannel owner, uint[] words, int offset)
        {
            RequireSnapshots();
            if (words == null || offset < 0 || offset > words.Length - 5) throw new ArgumentException("Fog binding range invalid.");
            if (owner != null) RequireOwner(owner);
            words[offset] = owner == null ? uint.MaxValue : (uint)owner.Slot;
            long identity = owner?.Identity ?? 0, generation = owner?.Generation ?? 0;
            words[offset + 1] = (uint)identity; words[offset + 2] = (uint)(identity >> 32);
            words[offset + 3] = (uint)generation; words[offset + 4] = (uint)(generation >> 32);
        }

        internal static BlueprintFogChannels CreateArena(BlueprintFogAssets assets, int ownerLimit, int blockCapacity, bool denseReference = false, int residentOwners = 0)
        {
            if (assets == null) throw new ArgumentNullException(nameof(assets));
            var pool = new BlueprintFogChannels(ownerLimit, MaximumFieldBudget);
            if (residentOwners == 0)
            {
                residentOwners = ownerLimit;
                while (residentOwners > 1 && BlueprintFogSnapshotState.EstimateFieldBytes(ownerLimit, blockCapacity, residentOwners) > MaximumFieldBudget) --residentOwners;
            }
            pool.arena = new BlueprintFogArena(assets.Arena, assets.Metadata, ownerLimit, blockCapacity, denseReference, residentOwners);
            pool.snapshotSlots = new SnapshotChannel[ownerLimit]; pool.generations = new long[ownerLimit];
            return pool;
        }

        private void RequireLegacy()
        { if (arena != null) throw new InvalidOperationException("Snapshot owners require the strict channel API."); }
        private void RequireSnapshots()
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (arena == null) throw new InvalidOperationException("This pool owns ordinary simulation channels.");
            if (creating) throw new InvalidOperationException("Fog channel factory must not re-enter its pool.");
        }
        private void RequireOwner(SnapshotChannel owner)
        {
            RequireSnapshots();
            if (owner == null || owner.Slot < 0 || owner.Slot >= capacity || !ReferenceEquals(snapshotSlots[owner.Slot], owner) || !ReferenceEquals(owner.Inputs.Owner, owner.State))
                throw new ArgumentException("Fog snapshot owner is stale or belongs to another pool.");
        }

        internal SnapshotChannel AcquireSnapshot(int slot, BlueprintFogInputQueue.Sample initial,
            Func<BlueprintFogScratch, BlueprintFogSimulation.StagedState, BlueprintFogSimulation> create, BlueprintFogInputQueue inputs = null)
        {
            RequireSnapshots();
            if (snapshotTeardown) throw new InvalidOperationException("Uncertain fog pool requires teardown.");
            if (slot < 0 || slot >= capacity || snapshotSlots[slot] != null || create == null) throw new ArgumentException("Fog snapshot slot/factory invalid.");
            if (inputs != null && (inputs.Owner != null || inputs.Faulted || !inputs.Committed.Equals(initial)))
                throw new ArgumentException("Fog source input differs from its detached baseline.");
            SnapshotChannel owner = null; bool initializing = false; creating = true;
            try
            {
                owner = new SnapshotChannel(slot, checked(++nextIdentity), checked(++generations[slot]), initial, inputs, ResidentSnapshotCount < arena.ResidentCapacity);
                var scratch = arena.Scratch; var buffers = arena.Buffers;
                var staged = new BlueprintFogSimulation.StagedState(scratch.ReadColour, scratch.ReadOrigin,
                    scratch.ReadSurface, buffers["_StageAway"], buffers["_StagePeak"], buffers["_StageState"]);
                var candidate = create(scratch, staged);
                foreach (var existing in snapshotSlots) if (existing != null && ReferenceEquals(existing.simulation, candidate))
                    throw new InvalidOperationException("Fog solver already belongs to another snapshot owner.");
                owner.simulation = candidate;
                if (owner.simulation == null || owner.simulation.GridSize != new Vector3Int(96, 80, 80) ||
                    owner.simulation.Tick != owner.State.ConfirmedTick || owner.simulation.AllocatedFieldBytes != 0 || !ReferenceEquals(owner.simulation.Scratch, scratch) || !owner.simulation.UsesStagedState(staged))
                    throw new InvalidOperationException("Fog snapshot factory differs from its full-field reservation.");
                initializing = true;
                if (owner.Velocity) Graphics.CopyTexture(scratch.PredictedColour, owner.Velocity);
                BindSnapshotCore(owner); arena.Dispatch("BeginStep"); arena.Dispatch("RestoreFields", 24, 20, 20); arena.Dispatch("PublishFields", 25, 21, 21);
                arena.PublishMetadata(slot, owner.LastSubmission, Actor(initial), Body(initial), initial.Clock, 0, 0);
                inputs?.Attach(owner.State);
                snapshotSlots[slot] = owner; ++snapshotCount; return owner;
            }
            catch
            {
                if (initializing)
                {
                    snapshotTeardown = true; owner.Error = "Fog owner initialization failed after GPU submission.";
                    snapshotSlots[slot] = owner; ++snapshotCount;
                }
                else owner?.Release();
                throw;
            }
            finally { creating = false; }
        }

        internal void CaptureSnapshot(SnapshotChannel owner, BlueprintFogInputQueue.Sample sample)
        {
            RequireOwner(owner);
            if (snapshotTeardown) throw new InvalidOperationException("Uncertain fog pool retains accepted inputs until teardown.");
            owner.Inputs.Capture(sample);
        }

        internal bool TrySubmitSnapshot(SnapshotChannel owner, out BlueprintFogSnapshotState.Submission token)
        {
            RequireOwner(owner);
            if (snapshotTeardown) throw new InvalidOperationException("Uncertain fog pool cannot submit more work.");
            token = default;
            if (owner.Inputs.Count == 0 || owner.State.State != BlueprintFogSnapshotState.Phase.Ready || !EnsureSnapshotVelocity(owner)) return false;
            if (!owner.Inputs.TryBegin(out token)) return false;
            owner.pending = token; owner.pendingBatch = true; owner.received = false; owner.reply = null;
            bool metadataOnly = owner.Inputs.PendingMetadataOnly;
            int pendingBase = (owner.Slot * 2 + (metadataOnly ? owner.Bank : 1 - owner.Bank)) * BlueprintFogArena.MapWords;
            try
            {
                BindSnapshot(owner); arena.Dispatch("BeginStep");
                if (!metadataOnly)
                {
                    arena.Dispatch("RestoreFields", 24, 20, 20); arena.Dispatch("StageLedger", 16);
                    Graphics.CopyTexture(owner.Velocity, arena.Scratch.PredictedColour);
                }
                for (int i = 0; i < owner.Inputs.PendingCount; ++i)
                {
                    var sample = owner.Inputs.At(i);
                    owner.simulation.AdvanceTo(sample.Clock, Actor(sample), Body(sample), sample.Emit, sample.EmissionPossible);
                }
                var shader = arena.Program;
                Bits(shader, "_OwnerBits", token.Owner); Bits(shader, "_GenerationBits", token.Generation); Bits(shader, "_SequenceBits", token.Sequence);
                shader.SetInt("_Tick", token.Tick); shader.SetInt("_EmissionRevision", token.EmissionRevision);
                if (!metadataOnly)
                {
                    arena.Dispatch("IndexFields", 24, 20, 20); arena.Dispatch("ValidateCurrent", 450); arena.Dispatch("ReserveBlocks");
                    arena.Dispatch("ResolveBlocks", 450); arena.Dispatch("PackFields", 24, 20, 20); arena.Dispatch("CheckPacking");
                    arena.Dispatch("CommitVelocity", 24, 20, 20); arena.Dispatch("CommitLedger", 16); arena.Dispatch("PublishFields", 25, 21, 21); arena.Dispatch("CommitMaps", 450);
                }
                shader.SetInt("_PendingBase", pendingBase);
                var last = owner.Inputs.At(owner.Inputs.PendingCount - 1);
                arena.PublishMetadata(owner.Slot, token, Actor(last), Body(last), last.Clock, last.Nearby ? 1 : 0, mapBase: pendingBase); arena.Dispatch("FinishStep");
                if (arena.VelocityUpload && !metadataOnly) RequestSnapshotVelocity(owner, token, false);
                var stamp = token; owner.nativePending = true;
                owner.request = AsyncGPUReadback.Request(arena.Buffers["_Receipts"], request => {
                    try
                    {
                        if (request.hasError) { snapshotTeardown = true; owner.Receive(stamp, null); return; }
                        var data = request.GetData<uint>();
                        if (data.Length != capacity * 16) { snapshotTeardown = true; owner.Receive(stamp, null); return; }
                        var copy = new uint[16]; for (int i = 0; i < 16; ++i) copy[i] = data[owner.Slot * 16 + i];
                        owner.Receive(stamp, copy);
                        if ((copy[8] & 4) != 0) snapshotTeardown = true;
                    }
                    catch { snapshotTeardown = true; owner.Receive(stamp, null); }
                    finally { owner.nativePending = false; }
                });
            }
            catch (Exception error)
            {
                RecordSnapshotFault(owner, token, null, false, pendingBase, "submission: " + error);
                owner.nativePending = false; owner.Inputs.Complete(token, BlueprintFogSnapshotState.Completion.Unknown);
                owner.pendingBatch = false; owner.Error = "Fog dispatch/readback submission failed."; snapshotTeardown = true; throw;
            }
            return true;
        }

        internal bool PollSnapshots()
        {
            RequireSnapshots(); bool complete = true;
            foreach (var owner in snapshotSlots)
            {
                if (owner == null || !owner.pendingBatch) continue;
                if (!owner.received) { complete = false; continue; }
                var token = owner.pending; var words = owner.reply;
                bool metadataOnly = owner.Inputs.PendingMetadataOnly;
                bool retainedVelocity = metadataOnly && owner.coldVelocity != null && SnapshotChannel.Same(owner.coldVelocityStamp, owner.LastSubmission);
                int expectedBase = (owner.Slot * 2 + (owner.Inputs.PendingMetadataOnly ? owner.Bank : 1 - owner.Bank)) * BlueprintFogArena.MapWords;
                bool valid = words != null && words.Length == 16 && Long(words[0], words[1]) == token.Owner && Long(words[2], words[3]) == token.Generation &&
                    Long(words[4], words[5]) == token.Sequence && words[6] == token.Tick && words[7] == token.EmissionRevision && (words[8] & ~15u) == 0 && words[9] == expectedBase;
                var outcome = !valid ? BlueprintFogSnapshotState.Completion.Unknown : words[8] == 0 ? BlueprintFogSnapshotState.Completion.Committed : BlueprintFogSnapshotState.Completion.Rejected;
                if (outcome != BlueprintFogSnapshotState.Completion.Committed)
                    RecordSnapshotFault(owner, token, words, valid, expectedBase, valid ? "GPU rejected" : "receipt invalid or unavailable");
                if (outcome == BlueprintFogSnapshotState.Completion.Committed && !owner.Inputs.PendingMetadataOnly) owner.Bank = 1 - owner.Bank;
                if (!owner.Inputs.Complete(token, outcome)) throw new InvalidOperationException("Fog channel lost its pending identity.");
                owner.LastSubmission = token; owner.LastCompletion = outcome; owner.LastFlags = valid ? words[8] : uint.MaxValue;
                if (outcome == BlueprintFogSnapshotState.Completion.Committed)
                {
                    if (retainedVelocity) owner.coldVelocityStamp = token;
                    else owner.coldVelocity = null;
                }
                owner.pendingBatch = false; owner.reply = null;
                if (!valid || (words[8] & 4) != 0) { owner.Error = "Fog receipt or allocator failed; pool teardown required."; snapshotTeardown = true; }
            }
            PollSnapshotVelocity();
            foreach (var owner in snapshotSlots) if (owner != null && owner.evictionPending) complete = false;
            return complete;
        }

        private void RecordSnapshotFault(SnapshotChannel owner, BlueprintFogSnapshotState.Submission token, uint[] words,
            bool valid, int expectedBase, string reason)
        {
            // Complete clears pending observations; retain the first fault before teardown loses its evidence.
            if (FirstSnapshotFault != null) return;
            FirstSnapshotFault = new SnapshotFault {
                slot = owner.Slot, owner = token.Owner, generation = token.Generation, sequence = token.Sequence,
                tick = token.Tick, emissionRevision = token.EmissionRevision, confirmedTick = owner.State.ConfirmedTick,
                queued = owner.Inputs.Count, pending = owner.Inputs.PendingCount, expectedBase = expectedBase,
                metadataOnly = owner.Inputs.PendingMetadataOnly, receiptValid = valid, committedClock = owner.Inputs.Committed.Clock,
                firstClock = owner.Inputs.At(0).Clock, lastClock = owner.Inputs.At(owner.Inputs.Count - 1).Clock,
                receipt = words == null ? null : (uint[])words.Clone(), reason = reason
            };
        }

        internal bool TryGetRecoveredSnapshot(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (snapshotTeardown || !owner.Inputs.CanRecover(owner.State.ConfirmedEmissionRevision)) return false;
            return ObserveSnapshotRecovery(owner);
        }

        private bool ObserveSnapshotRecovery(SnapshotChannel owner)
        {
            if (snapshotTeardown || !owner.Inputs.CanObserveRecovery(owner.State.ConfirmedEmissionRevision)) return false;
            if (owner.recoveryReceived && SnapshotChannel.Same(owner.recoveryStamp, owner.LastSubmission))
                return owner.recoveryFlags == 0;
            if (owner.recoveryNativePending) return false;
            var stamp = owner.LastSubmission;
            owner.recoveryStamp = stamp; owner.recoveryReceived = false; owner.recoveryFlags = null;
            owner.recoveryNativePending = true;
            try
            {
                // Read the committed owner's ledger, never the workspace most recently borrowed by a neighbour.
                owner.recoveryRequest = AsyncGPUReadback.Request(owner.Ledger[2], request => {
                    try
                    {
                        if (request.hasError) { owner.ReceiveRecovery(stamp, null); return; }
                        var data = request.GetData<uint>();
                        owner.ReceiveRecovery(stamp, data.Length == 1 && (data[0] & ~3u) == 0 ? (uint?)data[0] : null);
                    }
                    catch { owner.ReceiveRecovery(stamp, null); }
                    finally { owner.recoveryNativePending = false; }
                });
            }
            catch { owner.recoveryNativePending = false; owner.ReceiveRecovery(stamp, null); throw; }
            return false;
        }

        internal void RetireSnapshot(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (sources.Exists(source => ReferenceEquals(source.Snapshot, owner) && !source.Removed))
                throw new InvalidOperationException("A live fog source retains its state until explicit removal.");
            owner.State.Retire();
        }
        internal bool TryReleaseSnapshot(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (snapshotTeardown || owner.nativePending || owner.recoveryNativePending || owner.evictionPending || !owner.State.CanReleaseResources) return false;
            if (owner.releasePending)
            {
                if (!owner.releaseReceived) return false;
                var control = owner.releaseReply;
                if (control == null || control.Length != 16 || control[2] != 0 || control[0] > arena.BlockCapacity)
                { snapshotTeardown = true; owner.Error = "Fog GPU retirement was not confirmed; pool teardown required."; return false; }
                owner.Release(); snapshotSlots[owner.Slot] = null; --snapshotCount; return true;
            }
            owner.releasePending = true;
            try
            {
                BindSnapshot(owner); arena.Dispatch("BeginStep"); arena.Dispatch("ReleaseCurrent", 450); arena.Dispatch("ClearOwnerMaps", 450);
                arena.Dispatch("RestoreFields", 24, 20, 20); arena.Dispatch("PublishFields", 25, 21, 21);
                arena.PublishMetadata(owner.Slot, default, Vector3.zero, Vector2.one, 0, 0, 0);
                owner.nativePending = true;
                owner.request = AsyncGPUReadback.Request(arena.Buffers["_Control"], request => {
                    try
                    {
                        if (request.hasError) { snapshotTeardown = true; owner.ReceiveRelease(null); return; }
                        var data = request.GetData<uint>();
                        if (data.Length != 16) { snapshotTeardown = true; owner.ReceiveRelease(null); return; }
                        var copy = new uint[16]; for (int i = 0; i < copy.Length; ++i) copy[i] = data[i];
                        owner.ReceiveRelease(copy);
                        if (copy[2] != 0 || copy[0] > arena.BlockCapacity) snapshotTeardown = true;
                    }
                    catch { snapshotTeardown = true; owner.ReceiveRelease(null); }
                    finally { owner.nativePending = false; }
                });
            }
            catch { owner.nativePending = false; snapshotTeardown = true; owner.Error = "Fog retirement failed; pool teardown required."; throw; }
            return false;
        }

        internal void BindSnapshot(SnapshotChannel owner)
        { RequireOwner(owner); BindSnapshotCore(owner); }
        private void BindSnapshotCore(SnapshotChannel owner)
        {
            var shader = arena.Program; int words = BlueprintFogArena.MapWords;
            shader.SetInt("_Slot", owner.Slot); shader.SetInt("_CurrentBase", (owner.Slot * 2 + owner.Bank) * words);
            shader.SetInt("_PendingBase", (owner.Slot * 2 + 1 - owner.Bank) * words); shader.SetInts("_RenderOffset", 0, 0, owner.Slot * 82);
            shader.SetTexture(arena.Kernel("CommitVelocity"), "_OwnerVelocity", owner.Velocity ? owner.Velocity : arena.Scratch.PredictedColour);
            foreach (string name in new[] { "StageLedger", "CommitLedger" }) for (int i = 0; i < 3; ++i)
                shader.SetBuffer(arena.Kernel(name), new[] { "_OwnerAway", "_OwnerPeak", "_OwnerState" }[i], owner.Ledger[i]);
        }
        private static Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
        private static Vector2 Body(BlueprintFogInputQueue.Sample sample) => new Vector2(sample.Radius, sample.HalfHeight);
        private static void Bits(ComputeShader shader, string name, long value) => shader.SetInts(name, unchecked((int)value), (int)(value >> 32));
        private static long Long(uint low, uint high) => ((long)high << 32) | low;

        private void DisposeSnapshots()
        {
            if (snapshotsDisposed) return;
            if (creating || pumpingSources) throw new InvalidOperationException("Fog channel callback must not dispose its pool.");
            disposed = true;
            foreach (var owner in snapshotSlots) if (owner != null) owner.State.Retire();
            foreach (var owner in snapshotSlots) if (owner != null && owner.nativePending) owner.request.WaitForCompletion();
            foreach (var owner in snapshotSlots) if (owner != null && owner.recoveryNativePending) owner.recoveryRequest.WaitForCompletion();
            foreach (var owner in snapshotSlots) if (owner != null && owner.evictionNativePending) owner.evictionRequest.WaitForCompletion();
            // Also fences a dispatch that failed before its receipt request was created.
            var barrier = AsyncGPUReadback.Request(arena.Buffers["_Control"]); barrier.WaitForCompletion();
            if (barrier.hasError) throw new InvalidOperationException("Fog pool GPU drain was not confirmed; resources retained for retry.");
            foreach (var owner in snapshotSlots) if (owner != null) { owner.State.ConfirmPoolTeardown(); owner.Release(); }
            arena.Dispose(); snapshotCount = 0; snapshotsDisposed = true;
            CloseSources();
        }
    }
}
