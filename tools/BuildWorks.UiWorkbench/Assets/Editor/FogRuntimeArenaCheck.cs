using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

// Fixed-buffer GPU transaction test. Fixture uploads/readback waits are deliberately outside the proposed game path.
public static class FogRuntimeArenaCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-arena"); Directory.CreateDirectory(output);
        using (var test = new Probe())
        {
            for (int i = 0; i < 8; ++i) test.Send(i, 1, new[] { 1, 1, 1 });
            test.Wait(); test.CheckAll();
            for (int round = 0; round < 4; ++round)
            {
                for (int visit = 0; visit < 8; ++visit) test.Send(round % 2 == 0 ? visit : 7 - visit, round + 2, new[] { 1, 1, 1 });
                test.Wait(); test.CheckAll();
            }
            test.Send(0, 10, new[] { 1, 1, 2 }); test.Wait(); test.CheckAll(); test.RequireRejected(0, 2);
            test.Reset(0); test.Send(0, 11, new[] { 1, 1, 1 }); test.Wait(); test.CheckAll();
            test.Send(1, 12, new[] { 1, 1, 1 }, true); test.Wait(); test.CheckAll(); test.RequireRejected(1, 8);
            test.Reset(1); test.Send(1, 13, new[] { 1, 1, 1 }); test.Wait(); test.CheckAll();
            test.Send(2, 14, new[] { 1, 1, 1 }, false, true); test.Wait(); test.CheckAll(); test.RequireRejected(2, 1);
            test.Reset(2); test.Send(2, 15, new[] { 1, 1, 1 }); test.Wait(); test.CheckAll();
            var old = test.Send(3, 16, new[] { 1, 1, 1 }); test.States[3].Retire();
            Require(!test.States[3].CanReleaseResources, "Pending removal released a live transaction.");
            test.Wait(); test.CheckAll(); Require(test.States[3].CanReleaseResources, "Completed pending removal was retained.");
            test.Reset(3); test.Send(3, 17, new[] { 1, 1, 1 });
            Require(!test.States[3].Complete(old, BlueprintFogSnapshotState.Completion.Committed), "Reused slot accepted its old receipt.");
            bool blocked = false; try { test.Send(3, 18, new[] { 1, 1, 1 }); } catch (InvalidOperationException) { blocked = true; }
            Require(blocked, "Pending owner accepted a second step."); test.Wait(); test.CheckAll();
            test.Send(0, 19, new[] { 0, 0, 0 }); test.Wait(); test.CheckAll();
            test.Send(1, 20, new[] { 1, 1, 2 }); test.Wait(); test.CheckAll();
            Require(test.Committed == 47 && test.Rejected == 3 && test.BatchChecks == 15, "Arena test coverage incomplete.");
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, owners = 8, capacityBlocks = 27, committed = test.Committed, rejected = test.Rejected,
                batchChecks = test.BatchChecks, fieldComparisons = test.BatchChecks * 8 * 4, ledgerComparisons = test.BatchChecks * 8 * 3,
                shortageRollback = true, packingRollback = true, nonfiniteRollback = true, pendingRemoval = true, reusedSlot = true,
                guttersExact = true, freeBlocksConserved = true, productionAllocatorTested = false, fullWallTested = false,
                wholeHouseSupported = false, gameHostTested = false,
                note = "Eight synthetic8x8x8 owners. Fixed GPU buffers, GPU-only reservation; fixture uploads/checks wait on readbacks. No v11 physics, production wiring, real callback cadence, full-size budget or FPS proof." }, true));
        }
    }
    private sealed class Probe : IDisposable
    {
        private const int Grid = 8, MapWords = 24, Capacity = 27;
        private readonly ComputeShader shader;
        private readonly Dictionary<string, int> kernels = new Dictionary<string, int>();
        private readonly Dictionary<string, ComputeBuffer> buffers = new Dictionary<string, ComputeBuffer>();
        private readonly List<AsyncGPUReadbackRequest> requests = new List<AsyncGPUReadbackRequest>();
        private readonly RenderTexture[] input = new RenderTexture[4], restored = new RenderTexture[3], velocity = new RenderTexture[8];
        private readonly ComputeBuffer[][] ledger = new ComputeBuffer[8][];
        private readonly RenderTexture smoke, surface;
        private readonly Texture3D upload;
        private readonly int[] banks = new int[8], generations = new int[8], versions = new int[8];
        private readonly uint[] lastFlags = new uint[8];
        private readonly ushort[][][] expected = new ushort[8][][];
        internal readonly BlueprintFogSnapshotState[] States = new BlueprintFogSnapshotState[8];
        private string callbackFailure;
        internal int Committed, Rejected, BatchChecks;

        internal Probe()
        {
            try
            {
                shader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/BuildWorksFogRuntime/FogArena.compute"));
                if (!shader) throw new InvalidOperationException("Arena compute asset missing.");
                foreach (string name in new[] { "InitArena", "BeginStep", "StageLedger", "IndexFields", "ValidateCurrent", "ReserveBlocks", "ResolveBlocks", "PackFields", "CheckPacking", "CommitVelocity", "CommitLedger", "PublishFields", "CommitMaps", "FinishStep", "RestoreFields", "ReleaseCurrent", "ClearOwnerMaps" }) kernels.Add(name, shader.FindKernel(name));
                Add("_Maps", 8 * 2 * MapWords, 4); Add("_Free", Capacity, 4); Add("_Control", 16, 4); Add("_Receipts", 8 * 16, 4); Add("_Payload", Capacity * 64, 8);
                Add("_StageAway", 1024, 4); Add("_StagePeak", 1024, 4); Add("_StageState", 1, 4);
                shader.SetInts("_Grid", Grid, Grid, Grid); shader.SetInts("_Blocks", 2, 2, 2);
                shader.SetInt("_Capacity", Capacity); shader.SetInt("_MapWords", MapWords); shader.SetInt("_TotalMapWords", 8 * 2 * MapWords); shader.SetInt("_ReceiptWords", 8 * 16);
                Bind("InitArena", "_Maps", "_Free", "_Control", "_Receipts"); Bind("BeginStep", "_Control");
                Bind("IndexFields", "_Maps", "_Control"); Bind("ValidateCurrent", "_Maps", "_Control"); Bind("ReserveBlocks", "_Control");
                Bind("ResolveBlocks", "_Maps", "_Free", "_Control"); Bind("PackFields", "_Maps", "_Control", "_Payload"); Bind("CheckPacking", "_Control");
                Bind("CommitVelocity", "_Control"); Bind("PublishFields", "_Control"); Bind("CommitMaps", "_Maps", "_Free", "_Control"); Bind("FinishStep", "_Control", "_Receipts");
                Bind("RestoreFields", "_Maps", "_Payload", "_Control"); Bind("ReleaseCurrent", "_Maps", "_Free", "_Control"); Bind("ClearOwnerMaps", "_Maps");
                Bind("StageLedger", "_StageAway", "_StagePeak", "_StageState"); Bind("CommitLedger", "_StageAway", "_StagePeak", "_StageState", "_Control");
                upload = new Texture3D(Grid, Grid, Grid, TextureFormat.RGBAHalf, false);
                for (int i = 0; i < input.Length; ++i) input[i] = Field("Input " + i);
                for (int i = 0; i < restored.Length; ++i) restored[i] = Field("Restored " + i);
                foreach (string name in new[] { "IndexFields", "PackFields", "PublishFields" })
                {
                    shader.SetTexture(kernels[name], "_ColourIn", input[0]); shader.SetTexture(kernels[name], "_SurfaceIn", input[2]);
                    if (name != "PublishFields") shader.SetTexture(kernels[name], "_OriginIn", input[1]);
                }
                shader.SetTexture(kernels["CommitVelocity"], "_NextVelocity", input[3]);
                for (int i = 0; i < 3; ++i) shader.SetTexture(kernels["RestoreFields"], new[] { "_ColourOut", "_OriginOut", "_SurfaceOut" }[i], restored[i]);
                smoke = Atlas(); surface = Atlas(); shader.SetTexture(kernels["PublishFields"], "_PublishedSmoke", smoke); shader.SetTexture(kernels["PublishFields"], "_PublishedSurface", surface);
                Dispatch("InitArena", 6);
                for (int i = 0; i < 8; ++i)
                {
                    velocity[i] = Field("Owner velocity " + i); ledger[i] = new ComputeBuffer[3];
                    for (int f = 0; f < 3; ++f) ledger[i][f] = new ComputeBuffer(f == 2 ? 1 : 1024, 4);
                    Reset(i);
                }
            }
            catch { Dispose(); throw; }
        }
        private void Add(string name, int count, int stride) => buffers.Add(name, new ComputeBuffer(count, stride));
        private void Bind(string kernel, params string[] names) { foreach (string name in names) shader.SetBuffer(kernels[kernel], name, buffers[name]); }
        private void Dispatch(string name, int x = 1, int y = 1, int z = 1) => shader.Dispatch(kernels[name], x, y, z);
        private static RenderTexture Field(string label) => BlueprintFogSimulation.AllocateField(new Vector3Int(Grid, Grid, Grid), label);
        private static RenderTexture Atlas()
        {
            var t = new RenderTexture(10, 10, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear) { dimension = TextureDimension.Tex3D, volumeDepth = 80, enableRandomWrite = true, filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
            if (!t.Create()) { UnityEngine.Object.DestroyImmediate(t); throw new InvalidOperationException("Arena test atlas allocation failed."); } return t;
        }
        private void Owner(int slot)
        {
            shader.SetInt("_Slot", slot); shader.SetInt("_CurrentBase", (slot * 2 + banks[slot]) * MapWords); shader.SetInt("_PendingBase", (slot * 2 + 1 - banks[slot]) * MapWords);
            shader.SetInts("_RenderOffset", 0, 0, slot * 10); shader.SetTexture(kernels["CommitVelocity"], "_OwnerVelocity", velocity[slot]);
            foreach (string kernel in new[] { "StageLedger", "CommitLedger" }) for (int i = 0; i < 3; ++i) shader.SetBuffer(kernels[kernel], new[] { "_OwnerAway", "_OwnerPeak", "_OwnerState" }[i], ledger[slot][i]);
        }
        private void Write(ushort[] bits, RenderTexture target) { upload.SetPixelData(bits, 0); upload.Apply(false, false); Graphics.CopyTexture(upload, target); }
        internal void Reset(int slot)
        {
            if (States[slot] != null) { States[slot].Retire(); Require(States[slot].CanReleaseResources, "Slot reused before its pending receipt."); }
            Owner(slot); Dispatch("BeginStep"); Dispatch("ReleaseCurrent"); Dispatch("ClearOwnerMaps");
            banks[slot] = 0; States[slot] = new BlueprintFogSnapshotState(slot + 1, ++generations[slot]); versions[slot] = 0;
            expected[slot] = Enumerable.Range(0, 4).Select(_ => new ushort[Grid * Grid * Grid * 4]).ToArray();
            Write(expected[slot][3], velocity[slot]); foreach (var buffer in ledger[slot]) buffer.SetData(new uint[buffer.count]);
            for (int f = 0; f < 3; ++f) Write(expected[slot][f], input[f]); Dispatch("PublishFields", 3, 3, 3);
        }
        internal BlueprintFogSnapshotState.Submission Send(int slot, int version, int[] liveBlocks, bool packingFailure = false, bool nonfinite = false)
        {
            var owner = States[slot]; var token = owner.Submit(owner.ConfirmedTick + 1, version); Owner(slot);
            int pendingBase = (slot * 2 + 1 - banks[slot]) * MapWords;
            var data = new ushort[4][];
            for (int f = 0; f < 4; ++f)
            {
                data[f] = new ushort[Grid * Grid * Grid * 4];
                for (int block = 0; block < (f == 3 ? 8 : liveBlocks[f]); ++block)
                {
                    int b = (block + slot + f + version) % 8, x = (b % 2) * 4, y = (b / 2 % 2) * 4, z = (b / 4) * 4;
                    int at = (x + Grid * (y + Grid * z)) * 4; data[f][at] = (ushort)(0x3000 + version * 8 + slot);
                    data[f][at + 1] = 0x8000; data[f][at + 2] = 1;
                }
                if (f == 0 && nonfinite) data[f][0] = 0x7c00;
                Write(data[f], input[f]);
            }
            SetBits("_OwnerBits", token.Owner); SetBits("_GenerationBits", token.Generation); SetBits("_SequenceBits", token.Sequence);
            shader.SetInt("_Tick", token.Tick); shader.SetInt("_EmissionRevision", token.EmissionRevision); shader.SetInt("_InjectPackFailure", packingFailure ? 1 : 0);
            Dispatch("BeginStep"); Dispatch("StageLedger");
            buffers["_StageAway"].SetData(Enumerable.Repeat((uint)version, 1024).ToArray()); buffers["_StagePeak"].SetData(Enumerable.Repeat((uint)(version + 100), 1024).ToArray()); buffers["_StageState"].SetData(new[] { (uint)(version + 200) });
            Dispatch("IndexFields", 2, 2, 2); Dispatch("ValidateCurrent"); Dispatch("ReserveBlocks"); Dispatch("ResolveBlocks"); Dispatch("PackFields", 2, 2, 2); Dispatch("CheckPacking");
            Dispatch("CommitVelocity", 2, 2, 2); Dispatch("CommitLedger", 16); Dispatch("PublishFields", 3, 3, 3); Dispatch("CommitMaps"); Dispatch("FinishStep");
            requests.Add(AsyncGPUReadback.Request(buffers["_Receipts"], request => {
                if (request.hasError) { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); callbackFailure = "Arena receipt readback failed."; return; }
                var row = request.GetData<uint>(); int at = slot * 16;
                if (row.Length != 128 || row[at+9] != pendingBase || (row[at+8] & ~15u) != 0)
                { owner.Complete(token, BlueprintFogSnapshotState.Completion.Unknown); callbackFailure = "Arena receipt layout/status invalid."; return; }
                var reply = new BlueprintFogSnapshotState.Submission(Long(row[at], row[at+1]), Long(row[at+2], row[at+3]), Long(row[at+4], row[at+5]), (int)row[at+6], (int)row[at+7]);
                lastFlags[slot] = row[at+8];
                if (!ReferenceEquals(States[slot], owner) || !owner.Complete(reply, row[at+8] == 0 ? BlueprintFogSnapshotState.Completion.Committed : BlueprintFogSnapshotState.Completion.Rejected)) { callbackFailure = "Arena receipt identity mismatch."; return; }
                if (row[at+8] == 0) { banks[slot] = 1 - banks[slot]; expected[slot] = data; versions[slot] = version; ++Committed; } else ++Rejected;
            }));
            return token;
        }
        private void SetBits(string name, long value) => shader.SetInts(name, unchecked((int)value), (int)(value >> 32));
        private static long Long(uint low, uint high) => ((long)high << 32) | low;
        internal void Wait()
        {
            foreach (var request in requests) request.WaitForCompletion(); requests.Clear();
            Require(callbackFailure == null && States.All(s => s.State != BlueprintFogSnapshotState.Phase.Pending && s.State != BlueprintFogSnapshotState.Phase.Retiring), callbackFailure ?? "Arena callbacks did not complete.");
        }
        internal void RequireRejected(int slot, uint flags) => Require(lastFlags[slot] == flags && States[slot].State == BlueprintFogSnapshotState.Phase.Faulted, "Expected transaction rejection differs.");
        internal void CheckAll()
        {
            for (int i = 0; i < 8; ++i)
            {
                Owner(i); Dispatch("RestoreFields", 2, 2, 2);
                for (int f = 0; f < 3; ++f) Require(FogRuntimeSparseCheck.Read(restored[f]).SequenceEqual(expected[i][f]), "Arena changed an owner's encoded half bits.");
                Require(FogRuntimeSparseCheck.Read(velocity[i]).SequenceEqual(expected[i][3]), "Rejected snapshot changed velocity.");
                for (int f = 0; f < 3; ++f)
                {
                    var words = new uint[ledger[i][f].count]; ledger[i][f].GetData(words);
                    uint value = versions[i] == 0 ? 0 : (uint)(versions[i] + f * 100);
                    Require(words.All(v => v == value), "Rejected snapshot changed ledger.");
                }
            }
            CheckAtlas(smoke, 0); CheckAtlas(surface, 2);
            var control = new uint[16]; buffers["_Control"].GetData(control); Require(control[0] <= Capacity, "Free count overflow.");
            var maps = new uint[8 * 2 * MapWords]; var free = new uint[Capacity]; buffers["_Maps"].GetData(maps); buffers["_Free"].GetData(free);
            var ids = free.Take((int)control[0]).ToList(); for (int i = 0; i < 8; ++i) ids.AddRange(maps.Skip((i * 2 + banks[i]) * MapWords).Take(MapWords).Where(v => v != uint.MaxValue));
            Require(ids.Count == Capacity && ids.Distinct().Count() == Capacity && ids.All(v => v < Capacity), "GPU pool lost, duplicated or evicted blocks."); ++BatchChecks;
        }
        private void CheckAtlas(RenderTexture atlas, int field)
        {
            ushort[] bits = FogRuntimeSparseCheck.Read(atlas);
            for (int slot = 0; slot < 8; ++slot) for (int z = 0; z < 10; ++z) for (int y = 0; y < 10; ++y) for (int x = 0; x < 10; ++x)
            {
                int source = (Mathf.Clamp(x - 1, 0, 7) + 8 * (Mathf.Clamp(y - 1, 0, 7) + 8 * Mathf.Clamp(z - 1, 0, 7))) * 4;
                int target = (x + 10 * (y + 10 * (z + slot * 10))) * 4;
                for (int c = 0; c < 4; ++c) Require(bits[target+c] == expected[slot][field][source+c], "Published atlas/gutter changed or crossed owners.");
            }
        }
        public void Dispose()
        {
            foreach (var request in requests) request.WaitForCompletion(); requests.Clear();
            foreach (var b in buffers.Values) b.Release(); foreach (var row in ledger) if (row != null) foreach (var b in row) b?.Release();
            foreach (var t in input.Concat(restored).Concat(velocity).Concat(new[] { smoke, surface })) if (t) { t.Release(); UnityEngine.Object.DestroyImmediate(t); }
            if (upload) UnityEngine.Object.DestroyImmediate(upload); if (shader) UnityEngine.Object.DestroyImmediate(shader);
        }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, shortageRollback, packingRollback, nonfiniteRollback, pendingRemoval, reusedSlot, guttersExact, freeBlocksConserved,
            productionAllocatorTested, fullWallTested, wholeHouseSupported, gameHostTested;
        public int owners, capacityBlocks, committed, rejected, batchChecks, fieldComparisons, ledgerComparisons; public string note;
    }
}
