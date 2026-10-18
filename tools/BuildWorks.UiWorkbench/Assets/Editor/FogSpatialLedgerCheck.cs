using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogSpatialLedgerCheck
{
    private const BlueprintFogLedgerLayout Spatial = BlueprintFogLedgerLayout.Spatial3D;
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-spatial-ledger-223"); Directory.CreateDirectory(output);
        var report = new Report();
        try
        {
            using (var old = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            {
                bool rejected = false;
                try { using (var invalid = BlueprintFogChannels.CreateArena(old, 1, 1, ledgerLayout: Spatial)) { } }
                catch (NotSupportedException) { rejected = true; }
                Require(rejected, "Legacy bundle accepted spatial allocation."); report.oldBundleRejected = true;
            }
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "artifacts/fog-runtime-assets-candidate/buildworks-fog-v11"))))
            {
                VerifyBuffers(assets, report);
                for (int variant = 0; variant < 3; ++variant) VerifyCycle(assets, variant, report);
            }
            report.passed = true;
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(report, true));
            Debug.Log("FOG_SPATIAL_LEDGER_PASSED " + JsonUtility.ToJson(report)); EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
    }
    private static void VerifyBuffers(BlueprintFogAssets assets, Report report)
    {
        var shape = new Texture3D(1, 1, 1, TextureFormat.RGBAHalf, false); shape.SetPixels(new[] { Color.clear }); shape.Apply(false, false);
        try
        {
            bool rejected = false;
            try { using (var invalid = BlueprintFogChannels.CreateArena(assets, 28, 8192, residentOwners: 8, ledgerLayout: Spatial)) { } }
            catch (ArgumentException) { rejected = true; }
            Require(rejected, "Spatial budget overflow accepted."); report.budgetRejected = true;
            using (var pool = BlueprintFogChannels.CreateArena(assets, 28, 8192, ledgerLayout: Spatial))
            {
                var initial = new BlueprintFogInputQueue.Sample(0, new OstrixMods.BuildWorks.Geometry.Point3(50, 50, 50), .3f, .95f, false, false, false);
                BlueprintFogChannels.SnapshotChannel first = null;
                long actual = pool.SnapshotArena.Buffers.Values.Sum(b => (long)b.count * b.stride) + pool.SnapshotArena.Scratch.FieldBytes + 96L * 80 * 80 * 8;
                for (int i = 0; i < 28; ++i)
                {
                    var owner = pool.AcquireSnapshot(i, initial, (scratch, stage) => new BlueprintFogSimulation(assets,
                        shape, shape, shape, Vector3.one * -2, Vector3.one * 2, -Vector3.one, Vector3.one, Vector3.one * 50, 37, scratch: scratch, stagedState: stage));
                    if (i == 0) first = owner;
                    actual += owner.Ledger.Sum(b => (long)b.count * b.stride);
                    if (owner.Velocity) actual += 96L * 80 * 80 * 8;
                }
                Require(pool.SnapshotArena.ResidentCapacity == 7 && actual == 98041636 && pool.FieldBytes == actual, "Spatial actual buffer layout/reserve differs: " + actual);
                report.allocatedFieldBytes = actual;
                var a = Enumerable.Range(0, 16384).Select(i => (uint)(i * 13 + 1)).ToArray();
                var b = a.Select(v => v + 17).ToArray(); first.Ledger[0].SetData(a); first.Ledger[1].SetData(b);
                pool.BindSnapshot(first); var arena = pool.SnapshotArena;
                arena.Dispatch("BeginStep"); arena.Dispatch("StageLedger", 256);
                var words = new uint[16384]; arena.Buffers["_StageAway"].GetData(words); Require(words.SequenceEqual(a), "Spatial stage truncated away ledger.");
                arena.Buffers["_StagePeak"].GetData(words); Require(words.SequenceEqual(b), "Spatial stage truncated peak ledger.");
                arena.Buffers["_StageAway"].SetData(b); var control = new uint[16]; control[2] = 8; arena.Buffers["_Control"].SetData(control);
                arena.Dispatch("CommitLedger", 256); first.Ledger[0].GetData(words); Require(words.SequenceEqual(a), "Rejected spatial commit mutated ledger.");
                arena.Dispatch("BeginStep"); arena.Dispatch("CommitLedger", 256); first.Ledger[0].GetData(words); Require(words.SequenceEqual(b), "Spatial commit truncated ledger.");
                report.stageCommitFull16384 = report.rejectedCommitRetained = true;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(shape); }
    }
    private static void VerifyCycle(BlueprintFogAssets assets, int variant, Report report)
    {
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.name = "Owned spatial return fixture";
        prefab.transform.localScale = new Vector3(.18f, 2, 1.8f); prefab.SetActive(false);
        var material = new Material(Shader.Find("Standard")); material.mainTexture = Texture2D.whiteTexture;
        prefab.GetComponent<Renderer>().sharedMaterial = material;
        if (variant == 2)
        {
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube); child.transform.SetParent(prefab.transform, false);
            child.transform.localPosition = new Vector3(2, 0, 0); child.transform.localScale = new Vector3(6, .6f, .1f);
            child.GetComponent<Renderer>().sharedMaterial = material;
        }
        Vector3 direction = variant == 1 ? Vector3.forward : Vector3.right;
        Vector3 Actor(float t) => Vector3.up * .95f + direction * (t < 1 ? -2 : t < 2 ? Mathf.Lerp(-2, 0, t - 1) : t < 4 ? 0 : t < 5 ? Mathf.Lerp(0, 2, t - 4) : 2);
        Quaternion facing = Quaternion.Euler(0, variant == 1 ? 90 : 0, 0);
        var body = new Vector2(.3f, .95f);
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192, ledgerLayout: Spatial))
            using (var actual = new BlueprintFogVisual(assets, prefab, Vector3.up, facing, Vector3.one, null, Vector3.zero, Actor(0), body, pool))
            using (var expected = new BlueprintFogVisual(assets, prefab, Vector3.up, facing, Vector3.one, null, Vector3.zero, Actor(0), body, ledgerLayout: Spatial))
            using (var codec = new FogSparseStorageProbe())
            {
                bool contact = false;
                for (int i = 1; i <= 80; ++i)
                {
                    float t = i * .2f; Vector3 actor = Actor(t);
                    actual.AdvanceTo(t, actor, body, true); expected.AdvanceTo(t, actor, body, true);
                    pool.PumpSources(); AsyncGPUReadback.WaitAllRequests(); pool.PumpSources();
                    if (i % 10 != 0 && i != 80) continue;
                    var owner = actual.Snapshot;
                    Require(owner != null && owner.State.State == BlueprintFogSnapshotState.Phase.Ready && actual.LogicalSource.Inputs.Count == 0, "Spatial source did not commit.");
                    pool.BindSnapshot(owner); pool.SnapshotArena.Dispatch("BeginStep"); pool.SnapshotArena.Dispatch("RestoreFields", 24, 20, 20);
                    codec.BeginComparison(); codec.Compare(pool.SnapshotArena.Scratch.ReadColour, expected.Simulation.Smoke);
                    codec.Compare(pool.SnapshotArena.Scratch.ReadOrigin, FogRuntimeSparseCheck.Current(expected.Simulation, "origin"));
                    codec.Compare(pool.SnapshotArena.Scratch.ReadSurface, expected.Simulation.Surface); codec.Compare(owner.Velocity, expected.Simulation.Flow);
                    for (int j = 0; j < 3; ++j) codec.Compare(owner.Ledger[j], FogRuntimeSparseCheck.Buffer(expected.Simulation, new[] { "away", "peak", "state" }[j]));
                    codec.RequireClean(); report.exactComparisons += 7;
                    var flags = new uint[1]; owner.Ledger[2].GetData(flags);
                    if (t == 4) contact = flags[0] != 0;
                    if (i == 80) Require(contact && flags[0] == 0, "Spatial contact/full recovery not exercised: variant=" + variant + " flags=" + flags[0]);
                }
                ++report.fullCycles;
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(material); }
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, oldBundleRejected, budgetRejected, stageCommitFull16384, rejectedCommitRetained;
        public int fullCycles, exactComparisons; public long allocatedFieldBytes;
        public bool nativeGameTested = false, visualParityAccepted = false;
    }
}
