using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeSparseCheck
{
    private static readonly string[] Fields = { "colour", "origin", "surface" };
    private const long FieldBudget = 98304000;

    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-sparse"); Directory.CreateDirectory(output);
        EditorSceneManager.OpenScene("Assets/FogGhostLab/FogWallStudy.unity"); PlayerSettings.colorSpace = ColorSpace.Linear;
        Type type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("BuildWorks.FogStudy.FogGhostLab")).First(t => t != null);
        var lab = (Behaviour)UnityEngine.Object.FindFirstObjectByType(type);
        object Value(string name) => type.GetField(name, BindingFlags.Public | BindingFlags.Instance).GetValue(lab);
        var original = (GameObject)Value("solidBuilding"); var ghost = (GameObject)Value("ghostBuilding");
        var actor = (Transform)Value("actor"); Bounds bounds = actor.GetComponent<Collider>().bounds;
        var body = new Vector2(Mathf.Max(bounds.extents.x, bounds.extents.z), bounds.extents.y);
        lab.enabled = false; original.SetActive(false); ghost.SetActive(false);
        int fieldComparisons = 0, ledgerComparisons = 0, cpuComparisons = 0, liveSteps = 0;
        long peakStored = 0, peakGenerations = 0; double synchronizedMs = 0;
        bool firstSmoke = false, repeatedSmoke = false;
        var stored = new FogSparseStorageProbe.Encoded[3];
        using (var codec = new FogSparseStorageProbe())
        {
            CheckRawFixtures(codec);
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var visual = new BlueprintFogVisual(assets, original, Vector3.zero, Quaternion.identity, Vector3.one,
                null, Vector3.zero, Position(0), body))
            using (var candidate = new BlueprintFogSimulation(assets, visual.Source.Shape, visual.Source.Pigment, visual.Source.Noise,
                visual.FieldMinimum, visual.FieldMaximum, visual.Source.Minimum, visual.Source.Maximum, Position(0), 37))
            {
                BlueprintFogSimulation reference = visual.Simulation;
                try
                {
                    for (int tick = 0; tick <= 360; ++tick)
                    {
                        float time = tick * BlueprintFogSimulation.Step;
                        reference.AdvanceTo(time, Position(time), body, true); candidate.AdvanceTo(time, Position(time), body, true);
                        var next = new FogSparseStorageProbe.Encoded[3]; var watch = Stopwatch.StartNew();
                        try
                        {
                            for (int i = 0; i < Fields.Length; ++i) next[i] = codec.Encode(Current(candidate, Fields[i]), FieldBudget);
                            long previousBytes = stored.Where(s => s != null).Sum(s => s.Bytes), bytes = next.Sum(s => s.Bytes);
                            peakStored = Math.Max(peakStored, bytes); peakGenerations = Math.Max(peakGenerations, previousBytes + bytes);
                            if (next.Any(s => s.LiveBlocks != 0)) ++liveSteps;
                            if (tick < 240 && next[0].LiveBlocks != 0) firstSmoke = true;
                            if (tick >= 270 && next[0].LiveBlocks != 0) repeatedSmoke = true;
                            // Restore first poisons the entire dense target; no original values can hide a missing block.
                            for (int i = 0; i < Fields.Length; ++i) codec.Restore(next[i], Current(candidate, Fields[i]));
                            watch.Stop(); synchronizedMs += watch.Elapsed.TotalMilliseconds;
                            codec.BeginComparison();
                            foreach (string name in new[] { "colour", "velocity", "origin", "surface" })
                            { codec.Compare(Current(candidate, name), Current(reference, name)); ++fieldComparisons; }
                            foreach (string name in new[] { "away", "peak", "state" })
                            { codec.Compare(Buffer(candidate, name), Buffer(reference, name)); ++ledgerComparisons; }
                            codec.RequireClean();
                            if (tick == 90 || tick == 165 || tick == 240 || tick == 360)
                                foreach (string name in new[] { "colour", "velocity", "origin", "surface" })
                                { Require(Read(Current(candidate, name)).SequenceEqual(Read(Current(reference, name))), "CPU raw comparison differs at " + tick + "/" + name); ++cpuComparisons; }
                            foreach (var old in stored) old?.Dispose(); stored = next; next = null;
                        }
                        finally { if (next != null) foreach (var item in next) item?.Dispose(); }
                    }
                    Require(fieldComparisons == 1444 && ledgerComparisons == 1083 && cpuComparisons == 16 && liveSteps > 0 && firstSmoke && repeatedSmoke,
                        "Sparse comparison did not exercise the full live route.");
                    var shared = FogSparseSharedProbe.Run(codec, assets, visual);
                    long voxels = 96L * 80 * 80, workspace = voxels * 8 * 9, velocities = voxels * 8 * 8;
                    long projection = workspace + velocities + 9 * peakStored + 8 * (1024 * 8L + 4) + codec.CommonBufferBytes;
                    File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                        passed = true, shared = shared, gameHostTested = false, wholeHouseSupported = false, productionAllocatorTested = false,
                        gpuTimingsMeasured = false, ticks = 360, fieldComparisons = fieldComparisons, ledgerComparisons = ledgerComparisons,
                        cpuRawComparisons = cpuComparisons, finiteHalfPatterns = 63488, rawFixtures = 3, negativeControls = 4,
                        recontact = repeatedSmoke, liveSteps = liveSteps, peakStoredBytes = peakStored, peakOldNewBytes = peakGenerations,
                        commonBufferBytes = codec.CommonBufferBytes, comparisonDenseTextureBytes = 2 * BlueprintFogSimulation.FieldBytes,
                        projectedEightIdenticalOwnersBytes = projection, projectionFitsFieldCap = projection <= FieldBudget,
                        projectionIsHouseAdmission = false, synchronizedRoundTripMilliseconds = synchronizedMs,
                        note = "CPU count readbacks and allocations are a diagnostic. Projection includes one new generation; driver staging/source/atlas/meshes are excluded. This is not the shared-workspace implementation."
                    }, true));
                }
                finally { foreach (var item in stored) item?.Dispose(); }
            }
        }
    }

    private static void CheckRawFixtures(FogSparseStorageProbe codec)
    {
        // Every finite half bit pattern, including negative zero and subnormals, occurs before shader conversion.
        var words = Enumerable.Range(0, 65536).Select(i => (ushort)((i & 0x7c00) == 0x7c00 ? 0 : i)).ToArray();
        Fixture(codec, new Vector3Int(32, 32, 16), words, 256);
        var sparse = new ushort[8 * 8 * 8 * 4];
        sparse[4 * 4] = 0x8000; // Negative zero alone must retain its block even when alpha is zero.
        sparse[(4 * 8) * 4 + 1] = 1; // Subnormal in another block, also with zero alpha.
        Fixture(codec, new Vector3Int(8, 8, 8), sparse, 2);
        Fixture(codec, new Vector3Int(8, 8, 8), new ushort[sparse.Length], 0);
    }

    private static void Fixture(FogSparseStorageProbe codec, Vector3Int grid, ushort[] bits, int expectedBlocks)
    {
        var texture = new Texture3D(grid.x, grid.y, grid.z, TextureFormat.RGBAHalf, false);
        RenderTexture input = null, restored = null;
        try
        {
            texture.SetPixelData(bits, 0); texture.Apply(false, false);
            input = BlueprintFogSimulation.AllocateField(grid, "Sparse fixture input");
            restored = BlueprintFogSimulation.AllocateField(grid, "Sparse fixture restored");
            Graphics.CopyTexture(texture, input);
            Require(Read(input).SequenceEqual(bits), "Fixture upload changed its original half bits.");
            using (var saved = codec.Encode(input, FieldBudget))
            {
                Require(saved.LiveBlocks == expectedBlocks, "Sparse block occupancy differs from its fixture.");
                codec.Restore(saved, restored); Require(Read(restored).SequenceEqual(bits), "Raw fixture roundtrip lost half bits.");
                if (expectedBlocks != 2) return;
                ExpectFailure(() => codec.Encode(input, saved.Map.count * 4L).Dispose());
                codec.Restore(saved, restored); Require(Read(restored).SequenceEqual(bits), "Budget rejection changed retained state.");
                var zeros = new ushort[bits.Length]; texture.SetPixelData(zeros, 0); texture.Apply(false, false); Graphics.CopyTexture(texture, input);
                codec.BeginComparison(); codec.Compare(restored, input); ExpectFailure(codec.RequireClean);
                zeros[0] = 0x7c00; texture.SetPixelData(zeros, 0); texture.Apply(false, false); Graphics.CopyTexture(texture, input);
                ExpectFailure(() => codec.Encode(input, FieldBudget).Dispose());
                saved.Dispose(); ExpectFailure(() => codec.Restore(saved, restored));
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
            if (input) { input.Release(); UnityEngine.Object.DestroyImmediate(input); }
            if (restored) { restored.Release(); UnityEngine.Object.DestroyImmediate(restored); }
        }
    }

    private static Vector3 Position(float time)
    {
        float x;
        if (time < 3) x = Mathf.Lerp(-1.8f, 0, Mathf.Clamp01(time - 2));
        else if (time <= 4.5f) x = 0;
        else if (time < 8) x = Mathf.Lerp(0, 1.8f, Mathf.Clamp01(time - 4.5f));
        else if (time < 9) x = Mathf.Lerp(1.8f, 0, time - 8);
        else x = Mathf.Lerp(0, -1.8f, Mathf.Clamp01(time - 9));
        return new Vector3(x, .95f, 0);
    }
    internal static RenderTexture Current(BlueprintFogSimulation simulation, string name)
    {
        var type = typeof(BlueprintFogSimulation);
        int index = (int)type.GetField("current", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
        return ((RenderTexture[])type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation))[index];
    }
    internal static ComputeBuffer Buffer(BlueprintFogSimulation simulation, string name)
        => (ComputeBuffer)typeof(BlueprintFogSimulation).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(simulation);
    internal static ushort[] Read(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture); request.WaitForCompletion();
        Require(!request.hasError, "Sparse raw readback failed.");
        var result = new ushort[checked(texture.width * texture.height * texture.volumeDepth * 4)]; int offset = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        {
            var values = request.GetData<ushort>(layer);
            Require(offset + values.Length <= result.Length, "Sparse raw readback exceeds its expected size.");
            Array.Copy(values.ToArray(), 0, result, offset, values.Length); offset += values.Length;
        }
        Require(offset == result.Length, "Incomplete sparse raw readback."); return result;
    }
    private static void ExpectFailure(Action action)
    {
        bool failed = false;
        try { action(); } catch (ArgumentException) { failed = true; } catch (InvalidOperationException) { failed = true; }
        Require(failed, "Sparse negative control was accepted.");
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, gameHostTested, wholeHouseSupported, productionAllocatorTested, gpuTimingsMeasured, recontact,
            projectionFitsFieldCap, projectionIsHouseAdmission;
        public FogSparseSharedProbe.Report shared;
        public int ticks, fieldComparisons, ledgerComparisons, cpuRawComparisons, finiteHalfPatterns, rawFixtures, negativeControls, liveSteps;
        public long peakStoredBytes, peakOldNewBytes, commonBufferBytes, comparisonDenseTextureBytes, projectedEightIdenticalOwnersBytes;
        public double synchronizedRoundTripMilliseconds; public string note;
    }
}
