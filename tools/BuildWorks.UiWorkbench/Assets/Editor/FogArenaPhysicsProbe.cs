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
                test.RefreshMetadata(0, 360 * BlueprintFogSimulation.Step + .014f);
                // Candidate CPU history is speculative on rejection; never reuse it to retry automatically.
                test.Send(0, 368, true); test.Send(1, 368); test.Wait(); test.CheckAll(true);
                Require(test.LastFlags[0] == 8 && test.States[0].State == BlueprintFogSnapshotState.Phase.Faulted && test.States[0].ConfirmedTick == 360 &&
                    test.States[1].ConfirmedTick == 368, "Packing rejection changed a confirmed clock or blocked its neighbour.");
                bool blocked = false; try { test.Send(0, 368); } catch (InvalidOperationException) { blocked = true; }
                Require(blocked, "Rejected speculative history was reused.");
                Require(test.Committed == 161 && test.Rejected == 1 && test.Fields == 656 && test.Ledgers == 492 && test.Atlases == 24,
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
            Require(metadataChecks == committed + rejected + 1, "Metadata publication coverage incomplete.");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, gridX = 96, gridY = 80, gridZ = 80, owners = 2, ticks = 360,
                fieldComparisons = fields, ledgerComparisons = ledgers, atlasComparisons = atlases,
                committed = committed, rejected = rejected, metadataComparisons = metadataChecks,
                metadataBeforeReceipt = true, metadataOnlyTested = true, fractionalVisualClock = true, borrowedOwnershipValidated = true, borrowedInvalidCases = 11, runtimeArenaValidated = true,
                realSolver = true, stagedLedger = true, velocityAlias = true,
                packingRollback = true, shortageRollback = true, callbackWaitsInFixture = true,
                productionAllocatorTested = false, wholeHouseSupported = false, gameHostTested = false, gpuTimingsMeasured = false,
                note = "Two full-wall owners, batches1..8, alternate order, exact four fields/three ledgers and six publication checks. " +
                    "64-byte metadata is checked before CPU confirmation; one zero-step publication. Reference/source/atlas/temporary construction allocations excluded from production budget claims. No real-frame callbacks or shader-consumer/render proof." }, true));
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
        private readonly ComputeShader shader;
        private readonly IReadOnlyDictionary<string, ComputeBuffer> buffers;
        private readonly BlueprintFogSimulation[] candidate = new BlueprintFogSimulation[Owners], reference = new BlueprintFogSimulation[Owners];
        private readonly RenderTexture[] reads = new RenderTexture[3], velocity = new RenderTexture[Owners];
        private readonly ComputeBuffer[][] ledger = new ComputeBuffer[Owners][];
        private readonly BlueprintFogScratch scratch;
        private readonly RenderTexture smoke, surface;
        private readonly int[] banks = new int[Owners];
        private readonly long[] generations = { 1L << 34, 1L << 34 };
        private readonly uint[][] published = Enumerable.Range(0, Owners).Select(_ => new uint[16]).ToArray();
        private readonly BlueprintFogSnapshotState.Submission[] confirmed = new BlueprintFogSnapshotState.Submission[Owners];
        private readonly List<AsyncGPUReadbackRequest> requests = new List<AsyncGPUReadbackRequest>();
        private string failure;
        private readonly List<Action> heldConfirmations = new List<Action>();
        internal ComputeBuffer PublishedMetadata => buffers["_PublishedMetadata"];
        internal RenderTexture PublishedSmoke => smoke;
        internal RenderTexture PublishedSurface => surface;
        internal uint[] Binding(int slot) => new[] { (uint)slot, (uint)confirmed[slot].Owner, (uint)(confirmed[slot].Owner >> 32), (uint)confirmed[slot].Generation, (uint)(confirmed[slot].Generation >> 32) };
        internal void ReleaseConfirmations() { foreach (var complete in heldConfirmations) complete(); heldConfirmations.Clear(); }
        internal readonly BlueprintFogSnapshotState[] States = new BlueprintFogSnapshotState[Owners];
        internal readonly uint[] LastFlags = new uint[Owners];
        internal int Committed, Rejected, Fields, Ledgers, Atlases, MetadataChecks, ReceiptFirst;

        internal Probe(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, int capacity, FogSparseStorageProbe codec)
        {
            this.capacity = capacity; this.body = body; this.codec = codec; this.assets = assets; this.source = source;
            try
            {
                arena = new BlueprintFogArena(assets.Arena,
                    assets.Metadata, Owners, capacity);
                shader = arena.Program; buffers = arena.Buffers; scratch = arena.Scratch;
                reads[0] = scratch.ReadColour; reads[1] = scratch.ReadOrigin; reads[2] = scratch.ReadSurface;
                smoke = arena.Smoke; surface = arena.Surface;
                for (int i = 0; i < Owners; ++i) CreateOwner(i);
            }
            catch { Dispose(); throw; }
        }
        private void CreateOwner(int i)
        {
            BlueprintFogSimulation Make(BlueprintFogSimulation.StagedState staged = null) => new BlueprintFogSimulation(assets,
                source.Source.Shape, source.Source.Pigment, source.Source.Noise, source.FieldMinimum, source.FieldMaximum,
                source.Source.Minimum, source.Source.Maximum, Position(i, 0), 37 + i, scratch: staged == null ? null : scratch, stagedState: staged);
            reference[i] = Make();
            velocity[i] = BlueprintFogSimulation.AllocateField(Grid, "Arena committed velocity " + i);
            ledger[i] = new ComputeBuffer[3];
            for (int f = 0; f < 3; ++f) { ledger[i][f] = new ComputeBuffer(f == 2 ? 1 : 1024, 4); ledger[i][f].SetData(new uint[f == 2 ? 1 : 1024]); }
            candidate[i] = Make(new BlueprintFogSimulation.StagedState(reads[0], reads[1], reads[2], buffers["_StageAway"], buffers["_StagePeak"], buffers["_StageState"]));
            Graphics.CopyTexture(scratch.PredictedColour, velocity[i]);
            States[i] = new BlueprintFogSnapshotState((1L << 33) + i + 1, ++generations[i]);
            Owner(i); Dispatch("BeginStep"); GridDispatch("RestoreFields"); Publish();
            var initial = new BlueprintFogSnapshotState.Submission((1L << 33) + i + 1, generations[i], 0, 0, 0);
            PublishMetadata(i, initial, Position(i, 0), body, 0, 0, 0);
            published[i] = Metadata(initial, Position(i, 0), body, 0, 0, 0); confirmed[i] = initial;
        }
        private void Dispatch(string name, int x = 1, int y = 1, int z = 1) => arena.Dispatch(name, x, y, z);
        private void GridDispatch(string name) => Dispatch(name, 24, 20, 20);
        private void MapDispatch(string name) => Dispatch(name, (MapWords + 63) / 64);
        private void Publish() => Dispatch("PublishFields", 25, 21, 21);
        private void Owner(int slot)
        {
            shader.SetInt("_Slot", slot); shader.SetInt("_CurrentBase", (slot * 2 + banks[slot]) * MapWords); shader.SetInt("_PendingBase", (slot * 2 + 1 - banks[slot]) * MapWords);
            shader.SetInts("_RenderOffset", 0, 0, slot * 82); shader.SetTexture(arena.Kernel("CommitVelocity"), "_OwnerVelocity", velocity[slot]);
            foreach (string name in new[] { "StageLedger", "CommitLedger" }) for (int f = 0; f < 3; ++f)
                shader.SetBuffer(arena.Kernel(name), new[] { "_OwnerAway", "_OwnerPeak", "_OwnerState" }[f], ledger[slot][f]);
        }
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
        internal BlueprintFogInputQueue CreateInputQueue(int slot) => new BlueprintFogInputQueue(States[slot], Observation(slot, 0));
        internal BlueprintFogSnapshotState.Submission Send(int slot, int tick, bool inject = false, bool reverseCallbacks = false,
            BlueprintFogInputQueue input = null, bool holdConfirmation = false)
        {
            var owner = States[slot]; BlueprintFogSnapshotState.Submission token;
            if (input != null)
            {
                if (!ReferenceEquals(input.Owner, owner) || !input.TryBegin(out token)) throw new InvalidOperationException("Queued owner is not ready.");
                if (token.Tick != tick) { input.Complete(token, BlueprintFogSnapshotState.Completion.Rejected); throw new InvalidOperationException("Queued fixture did not reach its requested bounded tick."); }
            }
            else token = owner.Submit(tick, owner.ConfirmedEmissionRevision + 1);
            var observations = input == null ? new[] { Observation(slot, tick * BlueprintFogSimulation.Step + .005f) }
                : Enumerable.Range(0, input.PendingCount).Select(input.At).ToArray();
            bool metadataOnly = token.Tick == owner.ConfirmedTick;
            int pendingBase = (slot * 2 + (metadataOnly ? banks[slot] : 1 - banks[slot])) * MapWords;
            var last = observations[observations.Length - 1]; float clock = last.Clock;
            var actor = new Vector3((float)last.Actor.X, (float)last.Actor.Y, (float)last.Actor.Z);
            var capsule = new Vector2(last.Radius, last.HalfHeight); float weight = last.Nearby ? 1 : 0;
            uint[] before = published[slot], next = Metadata(token, actor, capsule, clock, weight), observed = null, receipt = null;
            bool completed = false;
            bool Complete(BlueprintFogSnapshotState.Submission stamp, BlueprintFogSnapshotState.Completion outcome)
                => input == null ? owner.Complete(stamp, outcome) : input.Complete(stamp, outcome);
            void Replay(BlueprintFogSimulation simulation)
            {
                foreach (var sample in observations) simulation.AdvanceTo(sample.Clock,
                    new Vector3((float)sample.Actor.X, (float)sample.Actor.Y, (float)sample.Actor.Z),
                    new Vector2(sample.Radius, sample.HalfHeight), sample.Emit, sample.EmissionPossible);
            }
            void Fail(string message) { Complete(token, BlueprintFogSnapshotState.Completion.Unknown); failure = message; }
            void Confirm()
            {
                if (holdConfirmation || completed || observed == null || receipt == null) return;
                completed = true;
                if (!ReferenceEquals(States[slot], owner) || receipt[9] != pendingBase || (receipt[8] & ~15u) != 0)
                { Fail("Physics receipt layout/status/slot differs."); return; }
                var reply = new BlueprintFogSnapshotState.Submission(Long(receipt[0], receipt[1]), Long(receipt[2], receipt[3]), Long(receipt[4], receipt[5]), (int)receipt[6], (int)receipt[7]);
                if (!observed.SequenceEqual(receipt[8] == 0 ? next : before))
                { Fail("Metadata differs from the committed fields or changed on rejection."); return; }
                if (!Complete(reply, receipt[8] == 0 ? BlueprintFogSnapshotState.Completion.Committed : BlueprintFogSnapshotState.Completion.Rejected))
                { Fail("Physics receipt identity differs."); return; }
                LastFlags[slot] = receipt[8];
                if (LastFlags[slot] == 0)
                {
                    if (!metadataOnly) banks[slot] = 1 - banks[slot];
                    published[slot] = next; confirmed[slot] = token; ++Committed; Replay(reference[slot]);
                }
                else ++Rejected;
            }
            if (holdConfirmation) heldConfirmations.Add(() => { holdConfirmation = false; Confirm(); });
            try
            {
                Owner(slot); Dispatch("BeginStep");
                if (!metadataOnly)
                {
                    GridDispatch("RestoreFields"); Dispatch("StageLedger", 16);
                    // Only AdvanceVelocity reads this alias; Matter overwrites it after that read finishes.
                    Graphics.CopyTexture(velocity[slot], scratch.PredictedColour);
                }
                Replay(candidate[slot]);
                Bits("_OwnerBits", token.Owner); Bits("_GenerationBits", token.Generation); Bits("_SequenceBits", token.Sequence);
                shader.SetInt("_Tick", token.Tick); shader.SetInt("_EmissionRevision", token.EmissionRevision); shader.SetInt("_InjectPackFailure", inject ? 1 : 0);
                if (!metadataOnly)
                {
                    GridDispatch("IndexFields"); MapDispatch("ValidateCurrent"); Dispatch("ReserveBlocks"); MapDispatch("ResolveBlocks"); GridDispatch("PackFields"); Dispatch("CheckPacking");
                    GridDispatch("CommitVelocity"); Dispatch("CommitLedger", 16); Publish(); MapDispatch("CommitMaps");
                }
                shader.SetInt("_PendingBase", pendingBase);
                PublishMetadata(slot, token, actor, capsule, clock, weight); Dispatch("FinishStep");
                Action<AsyncGPUReadbackRequest> metadataCallback = request => {
                    if (request.hasError) { Fail("Published metadata readback failed."); return; }
                    var data = request.GetData<uint>();
                    if (data.Length != Owners * 16 || (owner.State != BlueprintFogSnapshotState.Phase.Pending && owner.State != BlueprintFogSnapshotState.Phase.Retiring))
                    { Fail("Published metadata was not observed before CPU confirmation."); return; }
                    observed = data.Skip(slot * 16).Take(16).ToArray(); ++MetadataChecks; Confirm();
                };
                Action<AsyncGPUReadbackRequest> receiptCallback = request => {
                    if (request.hasError) { Fail("Physics receipt readback failed."); return; }
                    var data = request.GetData<uint>();
                    if (data.Length != Owners * 16) { Fail("Physics receipt layout differs."); return; }
                    if (observed == null) ++ReceiptFirst;
                    receipt = data.Skip(slot * 16).Take(16).ToArray(); Confirm();
                };
                if (reverseCallbacks)
                {
                    requests.Add(AsyncGPUReadback.Request(buffers["_Receipts"], receiptCallback));
                    requests.Add(AsyncGPUReadback.Request(buffers["_PublishedMetadata"], metadataCallback));
                }
                else
                {
                    requests.Add(AsyncGPUReadback.Request(buffers["_PublishedMetadata"], metadataCallback));
                    requests.Add(AsyncGPUReadback.Request(buffers["_Receipts"], receiptCallback));
                }
            }
            catch { Complete(token, BlueprintFogSnapshotState.Completion.Unknown); throw; }
            return token;
        }
        private void Bits(string name, long value) => shader.SetInts(name, unchecked((int)value), (int)(value >> 32));
        private void PublishMetadata(int slot, BlueprintFogSnapshotState.Submission token, Vector3 actor, Vector2 capsule, float clock, float weight, uint flags = 3)
            => arena.PublishMetadata(slot, token, actor, capsule, clock, weight, flags);
        internal void RefreshMetadata(int slot, float clock)
        {
            if (States[slot].State != BlueprintFogSnapshotState.Phase.Ready || States[slot].ConfirmedTick == 0 ||
                Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f) != States[slot].ConfirmedTick)
                throw new InvalidOperationException("Metadata-only publication needs a confirmed, unchanged tick.");
            Vector3 actor = Position(slot, clock); var token = confirmed[slot];
            candidate[slot].AdvanceTo(clock, actor, body, true); reference[slot].AdvanceTo(clock, actor, body, true);
            Owner(slot); Dispatch("BeginStep"); PublishMetadata(slot, token, actor, body, clock, 0);
            var next = Metadata(token, actor, body, clock, 0);
            var request = AsyncGPUReadback.Request(buffers["_PublishedMetadata"]); request.WaitForCompletion();
            Require(!request.hasError && request.GetData<uint>().Skip(slot * 16).Take(16).SequenceEqual(next), "Zero-step metadata differs.");
            published[slot] = next; ++MetadataChecks;
        }
        private static uint[] Metadata(BlueprintFogSnapshotState.Submission token, Vector3 actor, Vector2 body, float clock, float weight, uint flags = 3)
        {
            uint Float(float value) => BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            return new[] { Float(actor.x), Float(actor.y), Float(actor.z), Float(body.x), Float(body.y), Float(clock), Float(weight), flags,
                (uint)token.Owner, (uint)(token.Owner >> 32), (uint)token.Generation, (uint)(token.Generation >> 32),
                (uint)token.Sequence, (uint)(token.Sequence >> 32), (uint)token.Tick, (uint)token.EmissionRevision };
        }
        private static long Long(uint low, uint high) => ((long)high << 32) | low;
        internal bool RepliesComplete => States.All(s => s != null && (s.State == BlueprintFogSnapshotState.Phase.Ready || s.State == BlueprintFogSnapshotState.Phase.Retired || s.State == BlueprintFogSnapshotState.Phase.Faulted));
        internal bool HasExpiredRequest => requests.Any(r => r.done && r.hasError);
        internal bool Poll()
        {
            if (failure != null) throw new InvalidOperationException(failure);
            if (!RepliesComplete) return false;
            // Callback data is already copied. Native request handles may have expired several frames ago.
            requests.Clear(); return true;
        }
        internal void ResetOwner(int slot)
        {
            if (!Poll() || !States[slot].CanReleaseResources) throw new InvalidOperationException("A live/pending owner cannot reuse its slot.");
            Owner(slot); Dispatch("BeginStep"); MapDispatch("ReleaseCurrent"); MapDispatch("ClearOwnerMaps");
            DisposeOwner(slot); banks[slot] = 0; LastFlags[slot] = 0; CreateOwner(slot);
        }
        internal void Wait()
        {
            foreach (var request in requests) request.WaitForCompletion(); requests.Clear();
            Require(failure == null && States.All(s => s.State == BlueprintFogSnapshotState.Phase.Ready || s.State == BlueprintFogSnapshotState.Phase.Faulted), failure ?? "Physics callbacks incomplete.");
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
            for (int slot = 0; slot < Owners; ++slot) ids.AddRange(maps.Skip((slot * 2 + banks[slot]) * MapWords).Take(MapWords).Where(v => v != uint.MaxValue));
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
            foreach (var request in requests) request.WaitForCompletion(); requests.Clear();
            for (int slot = 0; slot < Owners; ++slot) DisposeOwner(slot);
            arena?.Dispose();
        }
        private void DisposeOwner(int slot)
        {
            candidate[slot]?.Dispose();
            candidate[slot] = null; reference[slot]?.Dispose(); reference[slot] = null;
            if (ledger[slot] != null) foreach (var buffer in ledger[slot]) buffer?.Release(); ledger[slot] = null;
            Release(velocity[slot]); velocity[slot] = null;
        }
    }
    private static void Release(RenderTexture texture) { if (texture) { texture.Release(); UnityEngine.Object.DestroyImmediate(texture); } }
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
            metadataBeforeReceipt, metadataOnlyTested, fractionalVisualClock, borrowedOwnershipValidated, runtimeArenaValidated,
            productionAllocatorTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured;
        public int borrowedInvalidCases, gridX, gridY, gridZ, owners, ticks, fieldComparisons, ledgerComparisons, atlasComparisons, committed, rejected, metadataComparisons;
        public string note;
    }
}
