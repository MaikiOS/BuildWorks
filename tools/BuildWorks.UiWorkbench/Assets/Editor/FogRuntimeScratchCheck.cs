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

public static class FogRuntimeScratchCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-scratch"); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        ColorSpace previous = PlayerSettings.colorSpace;
        var shape = new Texture3D(16, 16, 16, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp };
        var noise = new Texture3D(8, 8, 8, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat };
        var pigments = new Texture3D[3]; var legacy = new BlueprintFogSimulation[3];
        int comparisons = 0; long memoryBytes = 0;
        try
        {
            PlayerSettings.colorSpace = ColorSpace.Linear; AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            shape.SetPixelData(Enumerable.Range(0, 4096).Select(i => i % 16 >= 7 && i % 16 <= 8 ? (byte)255 : (byte)0).ToArray(), 0); shape.Apply();
            noise.SetPixelData(Enumerable.Repeat((byte)128, 512).ToArray(), 0); noise.Apply();
            for (int i = 0; i < pigments.Length; ++i)
            {
                pigments[i] = new Texture3D(2, 2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                pigments[i].SetPixels(Enumerable.Repeat(i == 0 ? Color.red : i == 1 ? Color.blue : Color.green, 8).ToArray()); pigments[i].Apply();
            }
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            {
                BlueprintFogSimulation Make(Vector3Int grid, int pigment, BlueprintFogScratch scratch = null)
                {
                    Vector3 minimum = new Vector3(-grid.x * .075f / 2, -.2f, -grid.z * .0675f / 2);
                    Vector3 maximum = minimum + Vector3.Scale((Vector3)grid, new Vector3(.075f, .06375f, .0675f));
                    return new BlueprintFogSimulation(assets, shape, pigments[pigment], noise, minimum, maximum,
                        minimum + Vector3.one * .1f, maximum - Vector3.one * .1f, Actor(pigment, 0), 37, grid, 0, scratch);
                }
                using (var pool = new BlueprintFogChannels(32, BlueprintFogChannels.MaximumFieldBudget, true))
                {
                    var shared = new BlueprintFogSimulation[3];
                    var grids = new[] { new Vector3Int(32, 40, 36), new Vector3Int(32, 40, 36), new Vector3Int(40, 40, 36) };
                    for (int i = 0; i < shared.Length; ++i)
                    {
                        int slot = i; legacy[i] = Make(grids[i], i);
                        Require(pool.TryAcquire("owner-" + i, grids[i], s => Make(grids[slot], slot, s), out shared[i]), "Shared owner not admitted");
                    }
                    Require(ReferenceEquals(shared[0].Scratch, shared[1].Scratch) && !ReferenceEquals(shared[0].Scratch, shared[2].Scratch), "Scratch did not use exact grid identity");
                    Require(!ReferenceEquals(shared[0].Smoke, shared[1].Smoke) && !ReferenceEquals(shared[0].Surface, shared[1].Surface), "Persistent matter is shared");
                    for (int tick = 1; tick <= 360; ++tick)
                    {
                        if (tick >= 64 && tick <= 70) continue; // Exercise eight copies/dispatch steps in one advance.
                        for (int i = 0; i < shared.Length; ++i)
                        {
                            Vector3 actor = Actor(i, tick); bool contact = tick >= 30 && tick <= 160;
                            legacy[i].AdvanceTo(tick * BlueprintFogSimulation.Step, actor, new Vector2(.3f, .95f), true, contact);
                            shared[i].AdvanceTo(tick * BlueprintFogSimulation.Step, actor, new Vector2(.3f, .95f), true, contact);
                        }
                        if (tick == 90 || tick == 180 || tick == 360)
                        {
                            for (int i = 0; i < shared.Length; ++i)
                                foreach (string name in new[] { "colour", "velocity", "origin", "surface" })
                                {
                                    Require(Read(Current(legacy[i], name)).SequenceEqual(Read(Current(shared[i], name))), "Shared scratch differs from legacy " + name + " at " + tick + "/" + i);
                                    ++comparisons;
                                }
                            if (tick == 90)
                            {
                                Require(!Read(shared[0].Smoke).SequenceEqual(Read(shared[1].Smoke)) && MaxAlpha(shared[0].Smoke) > 0, "Owner isolation negative control has no distinct live matter");
                                CheckFactoryRollback(pool, shared[0], grids[0], s => Make(grids[0], 0, s));
                            }
                        }
                    }
                    var scratch = shared[1].Scratch; ushort[] before = Read(shared[1].Smoke);
                    pool.Remove("owner-0");
                    Require(pool.Count == 2 && scratch.Colour && Read(shared[1].Smoke).SequenceEqual(before), "Removing one owner changed its neighbor");
                    Require(!pool.TryReleaseRecovered("owner-1") && !pool.TryReleaseRecovered("owner-2"), "Recovery must wait for individual readbacks");
                    AsyncGPUReadback.WaitAllRequests();
                    Require(pool.TryReleaseRecovered("owner-1") && pool.TryReleaseRecovered("owner-2"), "Recovered shared owner was retained");
                    Require(pool.Count == 0 && pool.FieldBytes == 0 && !scratch.Colour, "Last owner did not release shared scratch");
                }
                foreach (var simulation in legacy) simulation.Dispose(); Array.Clear(legacy, 0, legacy.Length);
                using (var pool = new BlueprintFogChannels(32, BlueprintFogChannels.MaximumFieldBudget, true))
                {
                    // Actual saved Forest Hut bounds, not a synthetic single-grid memory estimate.
                    var unique = new[] { new Vector3Int(40,24,64), new Vector3Int(48,48,64), new Vector3Int(64,24,64),
                        new Vector3Int(64,48,40), new Vector3Int(72,40,40), new Vector3Int(80,40,68) };
                    var grids = unique.Concat(Enumerable.Repeat(unique[5], 3)).Concat(Enumerable.Repeat(unique[1], 3)).ToArray();
                    long legacyBytes = 0;
                    for (int i = 0; i < grids.Length; ++i)
                    {
                        Vector3Int grid = grids[i]; legacyBytes += BlueprintFogSimulation.FieldVoxels(grid) * 80;
                        Require(pool.TryAcquire("bounds-" + i, grid, s => Make(grid, 0, s), out _), "Saved-bounds owner did not fit");
                    }
                    memoryBytes = pool.FieldBytes;
                    Require(memoryBytes == 96075776 && pool.Count == 12 && legacyBytes > BlueprintFogChannels.MaximumFieldBudget, "Saved-bounds memory accounting differs");
                    bool factory = false;
                    Require(!pool.TryAcquire("over-budget", unique[4], s => { factory = true; return Make(unique[4], 0, s); }, out _) && !factory && pool.FieldBytes == memoryBytes, "Budget refusal mutated state or ran its factory");
                    pool.Remove("bounds-5"); Require(pool.FieldBytes == memoryBytes - BlueprintFogSimulation.FieldVoxels(unique[5]) * 32, "Shared grid released too early");
                    foreach (int i in new[] { 6, 7, 8 }) pool.Remove("bounds-" + i);
                    Require(pool.FieldBytes == memoryBytes - BlueprintFogSimulation.FieldVoxels(unique[5]) * (4 * 32 + 48), "Last saved-grid scratch did not release");
                }
            }
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, ticks = 360, exactFieldComparisons = comparisons, owners = 3, budgetOwners = 12, budgetBytes = memoryBytes,
                exactGridSharing = true, persistentIndependent = true, factoryRollback = true, recoveredReleased = true,
                budgetRejectBeforeFactory = true, gameHostTested = false, wholeHouseSupported = false }, true));
        }
        finally
        {
            foreach (var simulation in legacy) simulation?.Dispose();
            UnityEngine.Object.DestroyImmediate(shape); UnityEngine.Object.DestroyImmediate(noise);
            foreach (var pigment in pigments) if (pigment) UnityEngine.Object.DestroyImmediate(pigment);
            PlayerSettings.colorSpace = previous;
        }
    }

    private static void CheckFactoryRollback(BlueprintFogChannels pool, BlueprintFogSimulation live, Vector3Int grid,
        Func<BlueprintFogScratch, BlueprintFogSimulation> make)
    {
        long bytes = pool.FieldBytes; int count = pool.Count; var original = Read(live.Smoke); var scratch = live.Scratch;
        Reject(() => pool.TryAcquire("alias", grid, s => live, out _));
        Reject(() => pool.TryAcquire("throw", grid, s => throw new InvalidOperationException("Injected factory failure"), out _));
        Reject(() => pool.TryAcquire("reenter", grid, s => { pool.Remove("owner-0"); return make(s); }, out _));
        BlueprintFogScratch failed = null;
        var other = new Vector3Int(48, 40, 36);
        Reject(() => pool.TryAcquire("new-grid-failure", other, s => { failed = s; throw new InvalidOperationException("Injected new-grid failure"); }, out _));
        Require(failed != null && !failed.Colour, "New failed scratch survived its factory");
        bool called = false;
        bool invalid = false;
        try { pool.TryAcquire("invalid-grid", new Vector3Int(int.MaxValue, 8, 4), s => { called = true; return make(s); }, out _); }
        catch (ArgumentException) { invalid = true; }
        Require(invalid && !called, "Invalid grid reached the allocation factory");
        Require(pool.TryAcquire("owner-0", grid, s => { called = true; return make(s); }, out var same) && !called && ReferenceEquals(same, live), "Duplicate owner changed allocation");
        Require(pool.FieldBytes == bytes && pool.Count == count && scratch.Colour && Read(live.Smoke).SequenceEqual(original), "Factory rollback changed existing matter or budget");
    }
    private static Vector3 Actor(int owner, int tick)
    {
        Vector3 outside = owner == 0 ? new Vector3(-3, 1, 0) : owner == 1 ? new Vector3(3, 1, .3f) : new Vector3(-3, 1, -.3f);
        Vector3 inside = new Vector3(0, 1, owner == 1 ? .3f : owner == 2 ? -.3f : 0);
        return tick <= 120 ? Vector3.Lerp(outside, inside, Mathf.Clamp01((tick - (owner == 1 ? 60 : 30)) / 30f))
            : Vector3.Lerp(inside, outside, Mathf.Clamp01((tick - 120) / 30f));
    }
    private static RenderTexture Current(BlueprintFogSimulation simulation, string name)
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        int current = (int)typeof(BlueprintFogSimulation).GetField("current", flags).GetValue(simulation);
        return ((RenderTexture[])typeof(BlueprintFogSimulation).GetField(name, flags).GetValue(simulation))[current];
    }
    private static ushort[] Read(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture, 0); request.WaitForCompletion();
        Require(!request.hasError, "Scratch readback failed"); var data = new List<ushort>();
        for (int layer = 0; layer < request.layerCount; ++layer) data.AddRange(request.GetData<ushort>(layer).ToArray());
        Require(data.Count == texture.width * texture.height * texture.volumeDepth * 4, "Incomplete scratch readback"); return data.ToArray();
    }
    private static float MaxAlpha(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        Require(!request.hasError, "Scratch live-matter readback failed"); float maximum = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        { var data = request.GetData<float>(layer); for (int i = 3; i < data.Length; i += 4) maximum = Mathf.Max(maximum, data[i]); }
        return maximum;
    }
    private static void Reject(Action action)
    {
        bool rejected = false; try { action(); } catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Invalid shared-scratch operation was accepted");
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, exactGridSharing, persistentIndependent, factoryRollback, recoveredReleased, budgetRejectBeforeFactory, gameHostTested, wholeHouseSupported;
        public int ticks, exactFieldComparisons, owners, budgetOwners; public long budgetBytes;
    }
}
