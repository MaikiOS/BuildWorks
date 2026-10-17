using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Private integration gate: real v11 steps, fixed GPU pool, borrowed workspace. Not the game renderer.
public static class FogArenaPhysicsProbe
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-arena-physics"); Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity"); PlayerSettings.colorSpace = ColorSpace.Linear;
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
        var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(type);
        var original = (GameObject)type.GetField("solidBuilding").GetValue(lab);
        var ghost = (GameObject)type.GetField("ghostBuilding").GetValue(lab);
        var actor = (Transform)type.GetField("actor").GetValue(lab); Bounds bounds = actor.GetComponent<Collider>().bounds;
        var body = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y);
        lab.enabled = false; original.SetActive(false); ghost.SetActive(false);
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
        using (var source = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, Position(0, 0), body))
        using (var codec = new FogSparseStorageProbe())
        {
            int lifecycleCases = FogRuntimeChannelLifecycleCheck.Run(assets, source);
            CheckLogicalSourceReplay(assets, source, body, codec);
            int fields, ledgers, atlases, committed, rejected, metadataChecks;
            using (var test = new Probe(assets, source, body, 8192, codec))
            {
                test.CheckBorrowing(); test.CheckAll(true);
                int tick = 0;
                for (int batch = 0; tick < 360; ++batch)
                {
                    tick += Math.Min(batch % 8 + 1, 360 - tick);
                    for (int visit = 0; visit < 2; ++visit) test.Send(batch % 2 == 0 ? visit : 1 - visit, tick);
                    if (batch == 0)
                    {
                        bool pendingBlocked = false; try { test.RefreshMetadata(0, tick * BlueprintFogSimulation.Step + .014f); } catch (InvalidOperationException) { pendingBlocked = true; }
                        Require(pendingBlocked, "Pending fields accepted an independent metadata update.");
                    }
                    test.Wait(); test.CheckAll((batch + 1) % 20 == 0);
                }
                test.CheckRecovery();
                test.RefreshMetadata(0, 360 * BlueprintFogSimulation.Step + .014f);
                // Candidate CPU history is speculative on rejection; never reuse it to retry automatically.
                test.Send(0, 368, true); test.Send(1, 368); test.Wait(); test.CheckAll(true);
                Require(test.LastFlags[0] == 8 && test.States[0].State == BlueprintFogSnapshotState.Phase.Faulted && test.States[0].ConfirmedTick == 360 &&
                    test.States[1].ConfirmedTick == 368, "Packing rejection changed a confirmed clock or blocked its neighbour.");
                bool blocked = false; try { test.Send(0, 368); } catch (InvalidOperationException) { blocked = true; }
                Require(blocked, "Rejected speculative history was reused.");
                Require(test.Committed == 162 && test.Rejected == 1 && test.Fields == 656 && test.Ledgers == 492 && test.Atlases == 24,
                    "Full physics coverage incomplete.");
                fields = test.Fields; ledgers = test.Ledgers; atlases = test.Atlases; committed = test.Committed; rejected = test.Rejected; metadataChecks = test.MetadataChecks;
            }
            using (var shortage = new Probe(assets, source, body, 1, codec))
            {
                for (int tick = 1; tick <= 180 && shortage.Rejected == 0; ++tick) { shortage.Send(0, tick); shortage.Wait(); }
                Require(shortage.Rejected == 1 && shortage.LastFlags[0] == 2, "Real v11 did not exercise fixed-pool exhaustion.");
                shortage.CheckAll(true);
                fields += shortage.Fields; ledgers += shortage.Ledgers; atlases += shortage.Atlases; committed += shortage.Committed; rejected += shortage.Rejected; metadataChecks += shortage.MetadataChecks;
            }
            int recoveryTicks = 0; bool activeObserved = false, recoveredObserved = false;
            using (var recovery = new Probe(assets, source, body, 8192, codec))
            {
                for (int tick = 8; tick <= 1800; tick += 8)
                {
                    recovery.Send(0, tick); recovery.Send(1, tick); recovery.Wait();
                    if (tick % 120 != 0) continue;
                    var flags = recovery.CheckRecovery();
                    activeObserved |= flags.Any(value => value != 0);
                    if (activeObserved && flags.All(value => value == 0))
                    { recoveryTicks = tick; recoveredObserved = true; recovery.CheckAll(true); break; }
                }
            }
            Require(activeObserved && recoveredObserved, "Real wall smoke/deformation never completed committed recovery after leaving.");
            Require(metadataChecks == committed + rejected, "Metadata publication coverage incomplete.");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, gridX = 96, gridY = 80, gridZ = 80, owners = 2, ticks = 360,
                fieldComparisons = fields, ledgerComparisons = ledgers, atlasComparisons = atlases,
                committed = committed, rejected = rejected, metadataComparisons = metadataChecks,
                metadataBeforeReceipt = true, metadataOnlyTested = true, fractionalVisualClock = true, borrowedOwnershipValidated = true, borrowedInvalidCases = 11, runtimeArenaValidated = true,
                runtimeChannelsValidated = true, runtimeLifecycleCases = lifecycleCases, committedRecoveryComparisons = 2,
                logicalSourcePhysicsComparisons = 7,
                physicalRecoveryObservedActive = activeObserved, physicalRecoveryCompleted = recoveredObserved, physicalRecoveryTicks = recoveryTicks,
                realSolver = true, stagedLedger = true, velocityAlias = true,
                packingRollback = true, shortageRollback = true, callbackWaitsInFixture = true,
                productionAllocatorTested = false, wholeHouseSupported = false, gameHostTested = false, gpuTimingsMeasured = false,
                note = "Two full-wall owners, batches1..8, alternate order, exact four fields/three ledgers and six publication checks. " +
                    "64-byte metadata is checked before CPU confirmation; one zero-step publication. Reference/source/atlas/temporary construction allocations excluded from production budget claims. This physics gate does not prove game integration, throughput or whole-house rendering. Runtime Channels own input/receipt/retirement lifecycle;38 fault cases passed." }, true));
        }
    }

    private static void CheckLogicalSourceReplay(BlueprintFogAssets assets, BlueprintFogVisual visual, Vector2 body, FogSparseStorageProbe codec)
    {
        var initial = new BlueprintFogInputQueue.Sample(0, new Point3(-1.8f, .95f, 0), body.x, body.y, true, true, true);
        BlueprintFogSimulation Make(BlueprintFogInputQueue.Sample baseline, BlueprintFogScratch scratch = null, BlueprintFogSimulation.StagedState staged = null)
            => new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, Actor(baseline), 37,
                initialClock: baseline.Clock, scratch: scratch, stagedState: staged);
        Vector3 Actor(BlueprintFogInputQueue.Sample sample) => new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z);
        using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
        using (var dense = Make(initial))
        {
            var blocker = pool.AcquireSnapshot(0, initial, (scratch, staged) => Make(initial, scratch, staged));
            float factoryClock = -1;
            var source = pool.RegisterSnapshotSource(initial, (scratch, staged, baseline) => {
                factoryClock = baseline.Clock; return Make(baseline, scratch, staged);
            });
            for (int i = 1; i <= 8; ++i)
            {
                var sample = new BlueprintFogInputQueue.Sample(i * .06f, new Point3(-1.8f + i * .225f, .95f, 0), body.x, body.y, true, true, true);
                pool.CaptureSource(source, sample); dense.AdvanceTo(sample.Clock, Actor(sample), body, true);
            }
            pool.PumpSources();
            Require(source.Snapshot == null && source.Inputs.Count == 8 && factoryClock == -1, "Occupied slot invoked source factory or dropped accepted wall movement.");
            pool.RetireSnapshot(blocker); Require(!pool.TryReleaseSnapshot(blocker), "Source replay blocker released without confirmation.");
            AsyncGPUReadback.WaitAllRequests(); Require(pool.TryReleaseSnapshot(blocker), "Source replay blocker retirement did not confirm.");
            for (int attempt = 0; attempt < 32; ++attempt)
            {
                pool.PumpSources(); AsyncGPUReadback.WaitAllRequests();
                if (source.Inputs.Count == 0 && source.Snapshot.State.State == BlueprintFogSnapshotState.Phase.Ready) break;
            }
            var owner = source.Snapshot;
            Require(factoryClock == 0 && source.Inputs.Count == 0 && owner != null && source.Inputs.Committed.Clock == .48f,
                "Delayed source did not replay from its original baseline.");
            pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
            var scratchState = pool.SnapshotArena.Scratch;
            codec.BeginComparison(); codec.Compare(scratchState.ReadColour, dense.Smoke); codec.Compare(scratchState.ReadOrigin, FogRuntimeSparseCheck.Current(dense, "origin"));
            codec.Compare(scratchState.ReadSurface, dense.Surface); codec.Compare(owner.Velocity, dense.Flow);
            for (int i = 0; i < 3; ++i) codec.Compare(owner.Ledger[i], FogRuntimeSparseCheck.Buffer(dense, new[] { "away", "peak", "state" }[i]));
            codec.RequireClean(); var flags = new uint[1]; owner.Ledger[2].GetData(flags);
            Require(flags[0] != 0, "Delayed logical replay never generated real wall smoke/deformation.");
        }
    }

    internal sealed class Probe : IDisposable
    {
        private const int Owners = 2, MapWords = 28800;
        private static readonly Vector3Int Grid = new Vector3Int(96, 80, 80);
        private static readonly string[] FieldNames = { "colour", "origin", "surface" };
        private readonly int capacity;
        private readonly Vector2 body;
        private readonly BlueprintFogAssets assets;
        private readonly BlueprintFogVisual source;
        private readonly FogSparseStorageProbe codec;
        private readonly BlueprintFogArena arena;
        private readonly BlueprintFogChannels pool;
        private readonly BlueprintFogChannels.SnapshotChannel[] owners = new BlueprintFogChannels.SnapshotChannel[Owners];
        private readonly ComputeShader shader;
        private readonly IReadOnlyDictionary<string, ComputeBuffer> buffers;
        private readonly BlueprintFogSimulation[] reference = new BlueprintFogSimulation[Owners];
        private readonly RenderTexture[] reads = new RenderTexture[3], velocity = new RenderTexture[Owners];
        private readonly ComputeBuffer[][] ledger = new ComputeBuffer[Owners][];
        private readonly BlueprintFogScratch scratch;
        private readonly RenderTexture smoke, surface;

        private readonly uint[][] published = Enumerable.Range(0, Owners).Select(_ => new uint[16]).ToArray();
        private readonly BlueprintFogSnapshotState.Submission[] confirmed = new BlueprintFogSnapshotState.Submission[Owners];
        private readonly List<AsyncGPUReadbackRequest> requests = new List<AsyncGPUReadbackRequest>();
        private string failure;
        private readonly List<Action> heldConfirmations = new List<Action>();
        internal ComputeBuffer PublishedMetadata => buffers["_PublishedMetadata"];
        internal RenderTexture PublishedSmoke => smoke;
        internal RenderTexture PublishedSurface => surface;
        internal uint[] Binding(int slot) => new[] { (uint)slot, (uint)confirmed[slot].Owner, (uint)(confirmed[slot].Owner >> 32), (uint)confirmed[slot].Generation, (uint)(confirmed[slot].Generation >> 32) };
        internal void ReleaseConfirmations() { foreach (var release in releases) release(); releases.Clear(); pool.PollSnapshots(); foreach (var complete in heldConfirmations) complete(); }
        internal BlueprintFogSnapshotState[] States => owners.Select(o => o.State).ToArray();
        internal readonly uint[] LastFlags = new uint[Owners];
        internal int Committed, Rejected, Fields, Ledgers, Atlases, MetadataChecks, ReceiptFirst;

        internal Probe(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, int capacity, FogSparseStorageProbe codec)
        {
            this.capacity = capacity; this.body = body; this.codec = codec; this.assets = assets; this.source = source;
            try
            {
                pool = BlueprintFogChannels.CreateArena(assets, Owners, capacity); arena = pool.SnapshotArena;
                shader = arena.Program; buffers = arena.Buffers; scratch = arena.Scratch;
                reads[0] = scratch.ReadColour; reads[1] = scratch.ReadOrigin; reads[2] = scratch.ReadSurface;
                smoke = arena.Smoke; surface = arena.Surface;
                for (int i = 0; i < Owners; ++i) CreateOwner(i);
            }
            catch { Dispose(); throw; }
        }
        private void CreateOwner(int i)
        {
            BlueprintFogSimulation Make(BlueprintFogScratch workspace = null, BlueprintFogSimulation.StagedState staged = null)
                => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                    source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, Position(i, 0), 37 + i,
                    scratch: workspace, stagedState: staged);
            reference[i] = Make();
            owners[i] = pool.AcquireSnapshot(i, Observation(i, 0), (workspace, staged) => Make(workspace, staged));
            velocity[i] = owners[i].Velocity; ledger[i] = owners[i].Ledger;
            var initial = owners[i].LastSubmission;
            published[i] = Metadata(initial, Position(i, 0), body, 0, 0, 0); confirmed[i] = initial;
        }
        private void Dispatch(string name, int x = 1, int y = 1, int z = 1) => arena.Dispatch(name, x, y, z);
        private void GridDispatch(string name) => Dispatch(name, 24, 20, 20);
        private void Owner(int slot) => pool.BindSnapshot(owners[slot]);
        internal void Retire(int slot) => pool.RetireSnapshot(owners[slot]);
        internal void DeliverStale(int slot, BlueprintFogSnapshotState.Submission token) => owners[slot].Receive(token, null);
        internal void CheckBorrowing()
        {
            Require(arena.ReservedFieldBytes == 58779980, "Arena reservation omitted owner metadata or committed fields.");
            void RejectArena(int owners, int capacity)
            {
                bool rejected = false;
                try { using (var invalid = new BlueprintFogArena(shader, assets.Metadata, owners, capacity)) { } }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Arena accepted invalid sizing or a budget overflow.");
            }
            RejectArena(0, 1); RejectArena(2, 0); RejectArena(8, 24860); RejectArena(32, 1);
            for (int slot = -1; slot <= 2; slot += 3)
            {
                bool rejected = false;
                try { arena.PublishMetadata(slot, new BlueprintFogSnapshotState.Submission(1, 1, 0, 0, 0), Vector3.zero, body, 0, 0, 0); }
                catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "Arena published metadata outside its slots.");
            }
            var away = buffers["_StageAway"]; var peak = buffers["_StagePeak"]; var state = buffers["_StageState"];
            BlueprintFogSimulation.StagedState State(RenderTexture colour = null, RenderTexture origin = null, ComputeBuffer a = null, ComputeBuffer p = null, ComputeBuffer s = null)
                => new BlueprintFogSimulation.StagedState(colour ?? reads[0], origin ?? reads[1], reads[2], a ?? away, p ?? peak, s ?? state);
            BlueprintFogSimulation Make(BlueprintFogSimulation.StagedState staged, BlueprintFogScratch workspace)
                => new BlueprintFogSimulation(assets, source.Source.Shape, source.Source.Pigment, source.Source.Noise,
                    source.FieldMinimum, source.FieldMaximum, source.Source.Minimum, source.Source.Maximum, Position(0, 0), 37,
                    scratch: workspace, stagedState: staged);
            away.SetData(Enumerable.Repeat(17u, 1024).ToArray()); state.SetData(new[] { 23u });
            void Reject(BlueprintFogSimulation.StagedState staged, BlueprintFogScratch workspace)
            {
                bool rejected = false; try { using (var invalid = Make(staged, workspace)) { } } catch (ArgumentException) { rejected = true; }
                Require(rejected, "Invalid staged resources accepted.");
                var marker = new uint[1024]; away.GetData(marker); var flags = new uint[1]; state.GetData(flags);
                Require(marker.All(v => v == 17) && flags[0] == 23, "Staged validation wrote to borrowed buffers before rejecting.");
            }
            Reject(State(), null);
            Reject(new BlueprintFogSimulation.StagedState(null, reads[1], reads[2], away, peak, state), scratch);
            Reject(State(origin: reads[0]), scratch); Reject(State(colour: scratch.Colour), scratch);
            Reject(new BlueprintFogSimulation.StagedState(reads[0], reads[1], reads[2], null, peak, state), scratch);
            Reject(State(p: away), scratch); Reject(State(s: away), scratch);
            using (var dead = new ComputeBuffer(1024, 4)) { dead.Release(); Reject(State(a: dead), scratch); }
            reads[0].filterMode = FilterMode.Point;
            try { Reject(State(), scratch); } finally { reads[0].filterMode = FilterMode.Trilinear; }
            reads[1].wrapModeV = TextureWrapMode.Repeat;
            try { Reject(State(), scratch); } finally { reads[1].wrapModeV = TextureWrapMode.Clamp; }
            scratch.PredictedColour.filterMode = FilterMode.Point;
            try { Reject(State(), scratch); } finally { scratch.PredictedColour.filterMode = FilterMode.Trilinear; }
            using (var view = Make(State(), scratch))
            {
                Require(view.AllocatedFieldBytes == 0, "Borrowed fields charged as owned dense allocations.");
                Require(scratch.FieldBytes == 44236800, "Full workspace must account for all nine fields.");
                bool blocked = false; try { view.TryGetRecovered(); } catch (InvalidOperationException) { blocked = true; }
                Require(blocked, "Borrowed workspace ledger authorized per-owner recovery.");
            }
            Require(reads.All(t => t && t.IsCreated()) && scratch.PredictedColour.IsCreated() && away.IsValid() && peak.IsValid() && state.IsValid(),
                "Disposing staged solver released arena resources.");
        }
        internal BlueprintFogInputQueue.Sample Observation(int slot, float clock)
        {
            Vector3 actor = Position(slot, clock);
            return new BlueprintFogInputQueue.Sample(clock, new Point3(actor.x, actor.y, actor.z), body.x, body.y, true, true, true);
        }
        internal void Capture(int slot, BlueprintFogInputQueue.Sample sample) => pool.CaptureSnapshot(owners[slot], sample);
        internal uint[] CheckRecovery()
        {
            foreach (var owner in owners) Require(!pool.TryGetRecoveredSnapshot(owner), "Committed recovery skipped its readback.");
            AsyncGPUReadback.WaitAllRequests();
            var flags = new uint[Owners];
            foreach (var owner in owners)
            {
                var state = new uint[1]; owner.Ledger[2].GetData(state);
                Require(pool.TryGetRecoveredSnapshot(owner) == (state[0] == 0), "Recovery differs from real v11 committed smoke/deformation.");
                flags[owner.Slot] = state[0];
            }
            return flags;
        }
        internal BlueprintFogInputQueue InputQueue(int slot) => owners[slot].Inputs;
        internal BlueprintFogSnapshotState.Submission Send(int slot, int tick, bool inject = false,
            BlueprintFogInputQueue input = null, bool holdConfirmation = false)
        {
            var channel = owners[slot]; var owner = channel.State; var queue = channel.Inputs;
            if (owner.State != BlueprintFogSnapshotState.Phase.Ready) throw new InvalidOperationException("Queued owner is not ready.");
            if (input != null && !ReferenceEquals(input, queue)) throw new InvalidOperationException("Input queue is not owned by Channels.");
            if (input == null) pool.CaptureSnapshot(channel, Observation(slot, tick * BlueprintFogSimulation.Step + .005f));
            var before = published[slot]; uint[] observed = null;
            BlueprintFogSnapshotState.Submission token;
            shader.SetInt("_InjectPackFailure", inject ? 1 : 0);
            try
            {
                if (!pool.TrySubmitSnapshot(channel, out token)) throw new InvalidOperationException("Runtime channel did not submit its queued prefix.");
            }
            finally { shader.SetInt("_InjectPackFailure", 0); }
            Require(token.Tick == tick, "Queued fixture did not reach its requested bounded tick.");
            var observations = Enumerable.Range(0, queue.PendingCount).Select(queue.At).ToArray();
            var last = observations[observations.Length - 1];
            var next = Metadata(token, new Vector3((float)last.Actor.X, (float)last.Actor.Y, (float)last.Actor.Z),
                new Vector2(last.Radius, last.HalfHeight), last.Clock, last.Nearby ? 1 : 0);
            bool completed = false;
            void Confirm()
            {
                if (holdConfirmation || completed || observed == null || channel.LastSubmission.Sequence != token.Sequence) return;
                completed = true;
                Require(channel.LastCompletion != BlueprintFogSnapshotState.Completion.Unknown, "Runtime channel lost receipt identity.");
                Require(observed.SequenceEqual(channel.LastFlags == 0 ? next : before), "Metadata differs from committed fields or changed on rejection.");
                LastFlags[slot] = channel.LastFlags;
                if (channel.LastCompletion == BlueprintFogSnapshotState.Completion.Committed)
                {
                    published[slot] = next; confirmed[slot] = token; ++Committed;
                    foreach (var sample in observations) reference[slot].AdvanceTo(sample.Clock,
                        new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z),
                        new Vector2(sample.Radius, sample.HalfHeight), sample.Emit, sample.EmissionPossible);
                }
                else ++Rejected;
            }
            heldConfirmations.Add(() => { if (!holdConfirmation) Confirm(); });
            if (holdConfirmation) releases.Add(() => holdConfirmation = false);
            Action<AsyncGPUReadbackRequest> metadataCallback = request => {
                try
                {
                    Require(!request.hasError, "Published metadata readback failed.");
                    var data = request.GetData<uint>(); Require(data.Length == Owners * 16, "Published metadata layout differs.");
                    if (channel.HasReceipt) ++ReceiptFirst;
                    observed = data.Skip(slot * 16).Take(16).ToArray(); ++MetadataChecks; Confirm();
                }
                catch (Exception error) { failure = error.Message; }
            };
            // Independent observation, never a production confirmation dependency.
            requests.Add(AsyncGPUReadback.Request(buffers["_PublishedMetadata"], metadataCallback));
            return token;
        }
        private readonly List<Action> releases = new List<Action>();
        internal void RefreshMetadata(int slot, float clock)
        {
            if (States[slot].State != BlueprintFogSnapshotState.Phase.Ready || States[slot].ConfirmedTick == 0 ||
                Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f) != States[slot].ConfirmedTick)
                throw new InvalidOperationException("Metadata-only publication needs a confirmed, unchanged tick.");
            pool.CaptureSnapshot(owners[slot], Observation(slot, clock));
            Send(slot, States[slot].ConfirmedTick, input: owners[slot].Inputs); Wait();
        }
        private static uint[] Metadata(BlueprintFogSnapshotState.Submission token, Vector3 actor, Vector2 body, float clock, float weight, uint flags = 3)
        {
            uint Float(float value) => BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            return new[] { Float(actor.x), Float(actor.y), Float(actor.z), Float(body.x), Float(body.y), Float(clock), Float(weight), flags,
                (uint)token.Owner, (uint)(token.Owner >> 32), (uint)token.Generation, (uint)(token.Generation >> 32),
                (uint)token.Sequence, (uint)(token.Sequence >> 32), (uint)token.Tick, (uint)token.EmissionRevision };
        }
        internal bool RepliesComplete => owners.All(o => o != null && (o.HasReceipt || (o.State.State != BlueprintFogSnapshotState.Phase.Pending && o.State.State != BlueprintFogSnapshotState.Phase.Retiring)));
        internal bool HasExpiredRequest => requests.Any(r => r.done && r.hasError);
        internal bool Poll()
        {
            if (failure != null) throw new InvalidOperationException(failure);
            if (releases.Count != 0) return false;
            pool.PollSnapshots(); foreach (var complete in heldConfirmations) complete();
            if (!RepliesComplete || MetadataChecks != Committed + Rejected) return false;
            heldConfirmations.Clear(); requests.Clear(); return true;
        }
        internal void ResetOwner(int slot)
        {
            if (!Poll() || !States[slot].CanReleaseResources) throw new InvalidOperationException("A live/pending owner cannot reuse its slot.");
            if (!pool.TryReleaseSnapshot(owners[slot])) { AsyncGPUReadback.WaitAllRequests(); Require(pool.TryReleaseSnapshot(owners[slot]), "GPU retirement was not confirmed."); }
            reference[slot]?.Dispose(); LastFlags[slot] = 0; CreateOwner(slot);
        }
        internal void Wait()
        {
            AsyncGPUReadback.WaitAllRequests();
            Require(Poll(), failure ?? "Runtime channel callbacks incomplete.");
        }
        internal void CheckAll(bool atlas)
        {
            codec.BeginComparison();
            for (int slot = 0; slot < Owners; ++slot)
            {
                Owner(slot); Dispatch("BeginStep"); GridDispatch("RestoreFields");
                for (int f = 0; f < 3; ++f) { codec.Compare(reads[f], FogRuntimeSparseCheck.Current(reference[slot], FieldNames[f])); ++Fields; }
                codec.Compare(velocity[slot], reference[slot].Flow); ++Fields;
                for (int f = 0; f < 3; ++f) { codec.Compare(ledger[slot][f], FogRuntimeSparseCheck.Buffer(reference[slot], new[] { "away", "peak", "state" }[f])); ++Ledgers; }
                Require(reference[slot].Tick == States[slot].ConfirmedTick, "Oracle differs from confirmed clock.");
            }
            codec.RequireClean();
            var control = new uint[16]; buffers["_Control"].GetData(control);
            var maps = new uint[Owners * 2 * MapWords]; buffers["_Maps"].GetData(maps); var free = new uint[capacity]; buffers["_Free"].GetData(free);
            Require(control[0] <= capacity, "Physics free count overflow.");
            var ids = free.Take((int)control[0]).ToList();
            for (int slot = 0; slot < Owners; ++slot) ids.AddRange(maps.Skip((slot * 2 + owners[slot].Bank) * MapWords).Take(MapWords).Where(v => v != uint.MaxValue));
            Require(ids.Count == capacity && ids.Distinct().Count() == capacity && ids.All(v => v < capacity), "Physics pool lost/duplicated blocks.");
            if (atlas) { CheckAtlas(smoke, "colour"); CheckAtlas(surface, "surface"); }
        }
        private void CheckAtlas(RenderTexture atlas, string name)
        {
            var actual = FogRuntimeSparseCheck.Read(atlas);
            for (int slot = 0; slot < Owners; ++slot)
            {
                var expected = FogRuntimeSparseCheck.Read(FogRuntimeSparseCheck.Current(reference[slot], name));
                for (int z = 0; z < 82; ++z) for (int y = 0; y < 82; ++y) for (int x = 0; x < 98; ++x)
                {
                    int a = (x + 98 * (y + 82 * (z + slot * 82))) * 4;
                    int e = (Mathf.Clamp(x - 1, 0, 95) + 96 * (Mathf.Clamp(y - 1, 0, 79) + 80 * Mathf.Clamp(z - 1, 0, 79))) * 4;
                    for (int c = 0; c < 4; ++c) Require(actual[a + c] == expected[e + c], "Physics publication/gutter changed committed bits.");
                }
                ++Atlases;
            }
        }
        public void Dispose()
        {
            AsyncGPUReadback.WaitAllRequests(); requests.Clear(); pool?.Dispose();
            for (int slot = 0; slot < Owners; ++slot) reference[slot]?.Dispose();
        }

    }
    private static Vector3 Position(int owner, float time)
    {
        float x = time < 3 ? Mathf.Lerp(-1.8f, 0, Mathf.Clamp01(time - 2)) : time < 4.5f ? 0
            : time < 8 ? Mathf.Lerp(0, 1.8f, Mathf.Clamp01(time - 4.5f)) : time < 9 ? Mathf.Lerp(1.8f, 0, time - 8)
            : Mathf.Lerp(0, -1.8f, Mathf.Clamp01(time - 9));
        return new Vector3(owner == 0 ? x : -x, .95f, owner * .07f);
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, realSolver, stagedLedger, velocityAlias, packingRollback, shortageRollback, callbackWaitsInFixture,
            metadataBeforeReceipt, metadataOnlyTested, fractionalVisualClock, borrowedOwnershipValidated, runtimeArenaValidated, runtimeChannelsValidated,
            productionAllocatorTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured, physicalRecoveryObservedActive, physicalRecoveryCompleted;
        public int borrowedInvalidCases, runtimeLifecycleCases, committedRecoveryComparisons, gridX, gridY, gridZ, owners, ticks, fieldComparisons, ledgerComparisons, atlasComparisons, committed, rejected, metadataComparisons;
        public string note;
        public int physicalRecoveryTicks;
        public int logicalSourcePhysicsComparisons;
    }
}
