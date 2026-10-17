using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
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
        using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
        using (var source = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one, null, Vector3.zero, Position(0, 0), body))
        using (var codec = new FogSparseStorageProbe())
        {
            int fields, ledgers, atlases, committed, rejected, metadataChecks;
            using (var test = new Probe(assets, source, body, 8192, codec))
            {
                test.CheckAll(true);
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
                metadataBeforeReceipt = true, metadataOnlyTested = true, fractionalVisualClock = true,
                realSolver = true, stagedLedger = true, velocityAlias = true,
                packingRollback = true, shortageRollback = true, callbackWaitsInFixture = true,
                productionAllocatorTested = false, wholeHouseSupported = false, gameHostTested = false, gpuTimingsMeasured = false,
                note = "Two full-wall owners, batches1..8, alternate order, exact four fields/three ledgers and six publication checks. " +
                    "64-byte metadata is checked before CPU confirmation; one zero-step publication. Reference/source/atlas/temporary construction allocations excluded from production budget claims. No real-frame callbacks or shader-consumer/render proof." }, true));
        }
    }

    private sealed class Probe : IDisposable
    {
        private const int Owners = 2, MapWords = 28800;
        private static readonly Vector3Int Grid = new Vector3Int(96, 80, 80);
        private static readonly string[] FieldNames = { "colour", "origin", "surface" };
        private readonly int capacity;
        private readonly Vector2 body;
        private readonly FogSparseStorageProbe codec;
        private readonly ComputeShader shader, metadataShader;
        private readonly int initMetadata, publishMetadata;
        private readonly Dictionary<string, int> kernels = new Dictionary<string, int>();
        private readonly Dictionary<string, ComputeBuffer> buffers = new Dictionary<string, ComputeBuffer>();
        private readonly BlueprintFogSimulation[] candidate = new BlueprintFogSimulation[Owners], reference = new BlueprintFogSimulation[Owners];
        private readonly RenderTexture[] reads = new RenderTexture[3], velocity = new RenderTexture[Owners];
        private readonly ComputeBuffer[][] ledger = new ComputeBuffer[Owners][];
        private readonly BlueprintFogScratch scratch;
        private readonly RenderTexture smoke, surface;
        private readonly int[] banks = new int[Owners];
        private readonly uint[][] published = Enumerable.Range(0, Owners).Select(_ => new uint[16]).ToArray();
        private readonly BlueprintFogSnapshotState.Submission[] confirmed = new BlueprintFogSnapshotState.Submission[Owners];
        private readonly List<AsyncGPUReadbackRequest> requests = new List<AsyncGPUReadbackRequest>();
        private string failure;
        internal readonly BlueprintFogSnapshotState[] States = new BlueprintFogSnapshotState[Owners];
        internal readonly uint[] LastFlags = new uint[Owners];
        internal int Committed, Rejected, Fields, Ledgers, Atlases, MetadataChecks;

        internal Probe(BlueprintFogAssets assets, BlueprintFogVisual source, Vector2 body, int capacity, FogSparseStorageProbe codec)
        {
            this.capacity = capacity; this.body = body; this.codec = codec;
            try
            {
                shader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/FogSparseArenaProbe.compute"));
                if (!shader) throw new InvalidOperationException("Arena compute missing.");
                foreach (string name in new[] { "InitArena", "BeginStep", "StageLedger", "IndexFields", "ValidateCurrent", "ReserveBlocks", "ResolveBlocks", "PackFields", "CheckPacking", "CommitVelocity", "CommitLedger", "PublishFields", "CommitMaps", "FinishStep", "RestoreFields" }) kernels.Add(name, shader.FindKernel(name));
                Add("_Maps", Owners * 2 * MapWords, 4); Add("_Free", capacity, 4); Add("_Control", 16, 4); Add("_Receipts", Owners * 16, 4); Add("_Payload", capacity * 64, 8);
                Add("_StageAway", 1024, 4); Add("_StagePeak", 1024, 4); Add("_StageState", 1, 4);
                metadataShader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/FogSnapshotMetadataProbe.compute"));
                if (!metadataShader) throw new InvalidOperationException("Metadata compute missing.");
                initMetadata = metadataShader.FindKernel("InitMetadata"); publishMetadata = metadataShader.FindKernel("PublishMetadata");
                Add("_PublishedMetadata", Owners * 16, 4);
                metadataShader.SetInt("_MetadataWords", Owners * 16);
                metadataShader.SetBuffer(initMetadata, "_PublishedMetadata", buffers["_PublishedMetadata"]);
                metadataShader.SetBuffer(publishMetadata, "_PublishedMetadata", buffers["_PublishedMetadata"]);
                metadataShader.SetBuffer(publishMetadata, "_Control", buffers["_Control"]);
                metadataShader.Dispatch(initMetadata, 1, 1, 1);
                shader.SetInts("_Grid", 96, 80, 80); shader.SetInts("_Blocks", 24, 20, 20);
                shader.SetInt("_Capacity", capacity); shader.SetInt("_MapWords", MapWords); shader.SetInt("_TotalMapWords", Owners * 2 * MapWords); shader.SetInt("_ReceiptWords", Owners * 16);
                Bind("InitArena", "_Maps", "_Free", "_Control", "_Receipts"); Bind("BeginStep", "_Control");
                Bind("StageLedger", "_StageAway", "_StagePeak", "_StageState"); Bind("CommitLedger", "_StageAway", "_StagePeak", "_StageState", "_Control");
                Bind("IndexFields", "_Maps", "_Control"); Bind("ValidateCurrent", "_Maps", "_Control"); Bind("ReserveBlocks", "_Control");
                Bind("ResolveBlocks", "_Maps", "_Free", "_Control"); Bind("PackFields", "_Maps", "_Control", "_Payload"); Bind("CheckPacking", "_Control");
                Bind("CommitVelocity", "_Control"); Bind("PublishFields", "_Control"); Bind("CommitMaps", "_Maps", "_Free", "_Control"); Bind("FinishStep", "_Control", "_Receipts");
                Bind("RestoreFields", "_Maps", "_Payload", "_Control");
                scratch = new BlueprintFogScratch(Grid);
                for (int f = 0; f < 3; ++f) reads[f] = BlueprintFogSimulation.AllocateField(Grid, "Arena physics read " + f);
                foreach (string name in new[] { "IndexFields", "PackFields", "PublishFields" })
                {
                    shader.SetTexture(kernels[name], "_ColourIn", reads[0]); shader.SetTexture(kernels[name], "_SurfaceIn", reads[2]);
                    if (name != "PublishFields") shader.SetTexture(kernels[name], "_OriginIn", reads[1]);
                }
                for (int f = 0; f < 3; ++f) shader.SetTexture(kernels["RestoreFields"], new[] { "_ColourOut", "_OriginOut", "_SurfaceOut" }[f], reads[f]);
                shader.SetTexture(kernels["CommitVelocity"], "_NextVelocity", scratch.Velocity);
                smoke = Atlas(); surface = Atlas(); shader.SetTexture(kernels["PublishFields"], "_PublishedSmoke", smoke); shader.SetTexture(kernels["PublishFields"], "_PublishedSurface", surface);
                Dispatch("InitArena", (Math.Max(Owners * 2 * MapWords, capacity) + 63) / 64);
                for (int i = 0; i < Owners; ++i)
                {
                    BlueprintFogSimulation Make(BlueprintFogScratch temporary = null) => new BlueprintFogSimulation(assets,
                        source.Source.Shape, source.Source.Pigment, source.Source.Noise, source.FieldMinimum, source.FieldMaximum,
                        source.Source.Minimum, source.Source.Maximum, Position(i, 0), 37 + i, scratch: temporary);
                    reference[i] = Make(); candidate[i] = Make(scratch);
                    // Owner resources stay committed; the actual AdvanceTo may write only the shared workspace/ledger.
                    for (int f = 0; f < 3; ++f) { var pair = Pair(candidate[i], FieldNames[f]); Release(pair[0]); pair[0] = reads[f]; }
                    velocity[i] = Pair(candidate[i], "velocity")[0]; Pair(candidate[i], "velocity")[0] = scratch.PredictedColour;
                    ledger[i] = new ComputeBuffer[3];
                    for (int f = 0; f < 3; ++f)
                    {
                        string name = new[] { "away", "peak", "state" }[f]; ledger[i][f] = FogRuntimeSparseCheck.Buffer(candidate[i], name);
                        Member(name).SetValue(candidate[i], buffers[new[] { "_StageAway", "_StagePeak", "_StageState" }[f]]);
                    }
                    var solver = (ComputeShader)Member("solver").GetValue(candidate[i]);
                    foreach (string name in new[] { "ClearLedger", "AdvanceVelocity", "AccumulateLedger" }) solver.SetBuffer(solver.FindKernel(name), "_AwayMass", buffers["_StageAway"]);
                    foreach (string name in new[] { "ClearLedger", "AdvanceVelocity" }) solver.SetBuffer(solver.FindKernel(name), "_PeakAway", buffers["_StagePeak"]);
                    foreach (string name in new[] { "ClearLedger", "AccumulateLedger", "UpdateSurface" }) solver.SetBuffer(solver.FindKernel(name), "_SurfaceState", buffers["_StageState"]);
                    States[i] = new BlueprintFogSnapshotState((1L << 33) + i + 1, (1L << 34) + 1);
                    Owner(i); Dispatch("BeginStep"); GridDispatch("RestoreFields"); Publish();
                }
            }
            catch { Dispose(); throw; }
        }
        private void Add(string name, int count, int stride) => buffers.Add(name, new ComputeBuffer(count, stride));
        private void Bind(string kernel, params string[] names) { foreach (string name in names) shader.SetBuffer(kernels[kernel], name, buffers[name]); }
        private void Dispatch(string name, int x = 1, int y = 1, int z = 1) => shader.Dispatch(kernels[name], x, y, z);
        private void GridDispatch(string name) => Dispatch(name, 24, 20, 20);
        private void MapDispatch(string name) => Dispatch(name, (MapWords + 63) / 64);
        private void Publish() => Dispatch("PublishFields", 25, 21, 21);
        private static RenderTexture Atlas()
        {
            var t = new RenderTexture(98, 82, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) {
                dimension = TextureDimension.Tex3D, volumeDepth = 82 * Owners, enableRandomWrite = true, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            if (!t.Create()) { UnityEngine.Object.DestroyImmediate(t); throw new InvalidOperationException("Physics atlas allocation failed."); } return t;
        }
        private void Owner(int slot)
        {
            shader.SetInt("_Slot", slot); shader.SetInt("_CurrentBase", (slot * 2 + banks[slot]) * MapWords); shader.SetInt("_PendingBase", (slot * 2 + 1 - banks[slot]) * MapWords);
            shader.SetInts("_RenderOffset", 0, 0, slot * 82); shader.SetTexture(kernels["CommitVelocity"], "_OwnerVelocity", velocity[slot]);
            foreach (string name in new[] { "StageLedger", "CommitLedger" }) for (int f = 0; f < 3; ++f)
                shader.SetBuffer(kernels[name], new[] { "_OwnerAway", "_OwnerPeak", "_OwnerState" }[f], ledger[slot][f]);
        }
        internal void Send(int slot, int tick, bool inject = false)
        {
            var owner = States[slot]; var token = owner.Submit(tick, owner.ConfirmedEmissionRevision + 1); Owner(slot);
            int pendingBase = (slot * 2 + 1 - banks[slot]) * MapWords;
            float clock = tick * BlueprintFogSimulation.Step + .005f;
            Vector3 actor = Position(slot, clock);
            uint[] before = published[slot], next = Metadata(token, actor, body, clock, 1), observed = null;
            try
            {
                Dispatch("BeginStep"); GridDispatch("RestoreFields"); Dispatch("StageLedger", 16);
                // Only AdvanceVelocity reads this alias; Matter overwrites it after that read finishes.
                Graphics.CopyTexture(velocity[slot], scratch.PredictedColour);
                candidate[slot].AdvanceTo(clock, actor, body, true);
                Bits("_OwnerBits", token.Owner); Bits("_GenerationBits", token.Generation); Bits("_SequenceBits", token.Sequence);
                shader.SetInt("_Tick", token.Tick); shader.SetInt("_EmissionRevision", token.EmissionRevision); shader.SetInt("_InjectPackFailure", inject ? 1 : 0);
                GridDispatch("IndexFields"); MapDispatch("ValidateCurrent"); Dispatch("ReserveBlocks"); MapDispatch("ResolveBlocks"); GridDispatch("PackFields"); Dispatch("CheckPacking");
                GridDispatch("CommitVelocity"); Dispatch("CommitLedger", 16); Publish(); PublishMetadata(slot, token, actor, clock, 1); MapDispatch("CommitMaps"); Dispatch("FinishStep");
                requests.Add(AsyncGPUReadback.Request(buffers["_PublishedMetadata"], request => {
                    if (request.hasError) { failure = "Published metadata readback failed."; return; }
                    var data = request.GetData<uint>();
                    if (data.Length != Owners * 16 || owner.State != BlueprintFogSnapshotState.Phase.Pending)
                    { failure = "Published metadata was not observed before CPU confirmation."; return; }
                    observed = data.Skip(slot * 16).Take(16).ToArray(); ++MetadataChecks;
                }));
                requests.Add(AsyncGPUReadback.Request(buffers["_Receipts"], request => {
                    if (request.hasError) { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); failure = "Physics receipt readback failed."; return; }
                    var data = request.GetData<uint>(); int at = slot * 16;
                    if (data.Length != Owners * 16 || data[at + 9] != pendingBase || (data[at + 8] & ~15u) != 0)
                    { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); failure = "Physics receipt layout/status differs."; return; }
                    var reply = new BlueprintFogSnapshotState.Submission(Long(data[at], data[at + 1]), Long(data[at + 2], data[at + 3]), Long(data[at + 4], data[at + 5]), (int)data[at + 6], (int)data[at + 7]);
                    if (observed == null || !observed.SequenceEqual(data[at + 8] == 0 ? next : before))
                    { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); failure = "Metadata differs from the committed fields or changed on rejection."; return; }
                    if (!owner.Complete(reply, data[at + 8] == 0 ? BlueprintFogSnapshotState.Completion.Committed : BlueprintFogSnapshotState.Completion.Rejected))
                    { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); failure = "Physics receipt identity differs."; return; }
                    LastFlags[slot] = data[at + 8];
                    if (LastFlags[slot] == 0)
                    {
                        banks[slot] = 1 - banks[slot]; published[slot] = next; confirmed[slot] = token; ++Committed;
                        reference[slot].AdvanceTo(clock, actor, body, true);
                    }
                    else ++Rejected;
                }));
            }
            catch { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); throw; }
        }
        private void Bits(string name, long value) => shader.SetInts(name, unchecked((int)value), (int)(value >> 32));
        private void PublishMetadata(int slot, BlueprintFogSnapshotState.Submission token, Vector3 actor, float clock, float weight)
        {
            metadataShader.SetInt("_Slot", slot); metadataShader.SetInt("_Tick", token.Tick); metadataShader.SetInt("_EmissionRevision", token.EmissionRevision); metadataShader.SetInt("_PublishFlags", 3);
            metadataShader.SetInts("_OwnerBits", unchecked((int)token.Owner), (int)(token.Owner >> 32));
            metadataShader.SetInts("_GenerationBits", unchecked((int)token.Generation), (int)(token.Generation >> 32));
            metadataShader.SetInts("_SequenceBits", unchecked((int)token.Sequence), (int)(token.Sequence >> 32));
            metadataShader.SetVector("_ActorRadius", new Vector4(actor.x, actor.y, actor.z, body.x));
            metadataShader.SetVector("_ShapeClock", new Vector4(body.y, clock, weight, 0));
            metadataShader.Dispatch(publishMetadata, 1, 1, 1);
        }
        internal void RefreshMetadata(int slot, float clock)
        {
            if (States[slot].State != BlueprintFogSnapshotState.Phase.Ready || States[slot].ConfirmedTick == 0 ||
                Mathf.FloorToInt(clock / BlueprintFogSimulation.Step + .0001f) != States[slot].ConfirmedTick)
                throw new InvalidOperationException("Metadata-only publication needs a confirmed, unchanged tick.");
            Vector3 actor = Position(slot, clock); var token = confirmed[slot];
            candidate[slot].AdvanceTo(clock, actor, body, true); reference[slot].AdvanceTo(clock, actor, body, true);
            Owner(slot); Dispatch("BeginStep"); PublishMetadata(slot, token, actor, clock, 0);
            var next = Metadata(token, actor, body, clock, 0);
            var request = AsyncGPUReadback.Request(buffers["_PublishedMetadata"]); request.WaitForCompletion();
            Require(!request.hasError && request.GetData<uint>().Skip(slot * 16).Take(16).SequenceEqual(next), "Zero-step metadata differs.");
            published[slot] = next; ++MetadataChecks;
        }
        private static uint[] Metadata(BlueprintFogSnapshotState.Submission token, Vector3 actor, Vector2 body, float clock, float weight)
        {
            uint Float(float value) => BitConverter.ToUInt32(BitConverter.GetBytes(value), 0);
            return new[] { Float(actor.x), Float(actor.y), Float(actor.z), Float(body.x), Float(body.y), Float(clock), Float(weight), 3u,
                (uint)token.Owner, (uint)(token.Owner >> 32), (uint)token.Generation, (uint)(token.Generation >> 32),
                (uint)token.Sequence, (uint)(token.Sequence >> 32), (uint)token.Tick, (uint)token.EmissionRevision };
        }
        private static long Long(uint low, uint high) => ((long)high << 32) | low;
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
            foreach (var simulation in candidate) if (simulation != null)
            {
                // The diagnostic owns all detached resources; a partially constructed owner may still own some.
                foreach (string name in FieldNames.Concat(new[] { "velocity" }))
                {
                    var pair = Pair(simulation, name);
                    if (reads.Any(t => ReferenceEquals(t, pair[0])) || ReferenceEquals(scratch?.PredictedColour, pair[0])) pair[0] = null;
                }
                foreach (string name in new[] { "away", "peak", "state" })
                    if (buffers.Values.Any(b => ReferenceEquals(b, Member(name).GetValue(simulation)))) Member(name).SetValue(simulation, null);
                simulation.Dispose();
            }
            foreach (var simulation in reference) simulation?.Dispose();
            foreach (var row in ledger) if (row != null) foreach (var buffer in row) buffer?.Release();
            foreach (var buffer in buffers.Values) buffer.Release();
            foreach (var texture in reads.Concat(velocity).Concat(new[] { smoke, surface })) Release(texture);
            scratch?.Dispose(); if (shader) UnityEngine.Object.DestroyImmediate(shader); if (metadataShader) UnityEngine.Object.DestroyImmediate(metadataShader);
        }
    }
    private static FieldInfo Member(string name) => typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
    private static RenderTexture[] Pair(BlueprintFogSimulation simulation, string name) => (RenderTexture[])Member(name).GetValue(simulation);
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
            metadataBeforeReceipt, metadataOnlyTested, fractionalVisualClock,
            productionAllocatorTested, wholeHouseSupported, gameHostTested, gpuTimingsMeasured;
        public int gridX, gridY, gridZ, owners, ticks, fieldComparisons, ledgerComparisons, atlasComparisons, committed, rejected, metadataComparisons;
        public string note;
    }
}
