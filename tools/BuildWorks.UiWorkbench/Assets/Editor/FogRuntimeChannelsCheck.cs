using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeChannelsCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-channels"); Directory.CreateDirectory(output);
        Texture3D aShape = Shape(false), bShape = Shape(true), red = Pigment(Color.red), blue = Pigment(Color.blue), noise = Pigment(Color.gray);
        Vector3 restMin = new Vector3(-2.4f, -.7f, -1.7f), restMax = new Vector3(2.4f, 3.8f, 1.7f);
        Vector3 minimum = restMin - new Vector3(1.2f, 0, 1), maximum = restMax + new Vector3(1.2f, .6f, 1);
        Vector2 body = new Vector2(.3f, .95f); Vector3 start = Actor(0);
        int comparisons = 0, overlap = 0; float bBeforeRemoval = 0;
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var pool = new BlueprintFogChannels())
            using (var alone = new BlueprintFogSimulation(assets, aShape, red, noise, minimum, maximum, restMin, restMax, start, 37))
            {
                BlueprintFogSimulation Make(Texture3D shape, Texture3D pigment) => new BlueprintFogSimulation(assets, shape, pigment, noise, minimum, maximum, restMin, restMax, start, 37);
                Require(pool.TryAcquire("part-A:region-0", () => Make(aShape, red), out var a), "A not admitted");
                Require(pool.TryAcquire("part-B:region-0", () => Make(bShape, blue), out var b), "B not admitted");
                Require(pool.Count == 2 && pool.FieldBytes == 2 * BlueprintFogSimulation.FieldBytes, "Unbounded channel allocation");
                Require(pool.TryAcquire("part-A:region-0", () => throw new Exception("Duplicate owner allocated again"), out var same) && ReferenceEquals(a, same), "Owner identity unstable");
                Require(!pool.TryAcquire("part-C:region-0", () => throw new Exception("Full pool allocated a third field"), out _), "Full pool evicted an owner");
                Require(!pool.TryReleaseRecovered("part-A:region-0"), "Async recovery was assumed before readback");
                for (int tick = 1; tick <= 150; ++tick)
                {
                    Vector3 actor = Actor(tick); float time = tick * BlueprintFogSimulation.Step;
                    a.AdvanceTo(time, actor, body, true); b.AdvanceTo(time, actor, body, true); alone.AdvanceTo(time, actor, body, true);
                    if (tick == 45)
                    {
                        Require(MaxAlpha(a.Surface) > .5f && MaxAlpha(a.Smoke) > 0, "A-only contact absent");
                        Require(MaxAlpha(b.Surface) == 0 && MaxAlpha(b.Smoke) == 0, "A affected the perpendicular B owner");
                        AsyncGPUReadback.WaitAllRequests();
                        Require(!pool.TryReleaseRecovered("part-A:region-0"), "Stale idle readback released a new contact");
                    }
                    if (tick == 45 || tick == 150)
                    {
                        Equal(a.Smoke, alone.Smoke); Equal(a.Flow, alone.Flow); Equal(a.Surface, alone.Surface); comparisons += 3;
                    }
                }
                float[] smokeA = Read(a.Smoke), smokeB = Read(b.Smoke);
                for (int i = 0; i < smokeA.Length; i += 4)
                {
                    Require(smokeA[i + 1] == 0 && smokeA[i + 2] == 0, "A pigment acquired another owner");
                    Require(smokeB[i] == 0 && smokeB[i + 1] == 0, "B pigment acquired another owner");
                    if (smokeA[i + 3] > .001f && smokeB[i + 3] > .001f) ++overlap;
                }
                Require(overlap > 0, "Both owners never occupied the same physical voxels");
                bBeforeRemoval = MaxAlpha(b.Surface); Require(bBeforeRemoval > .5f, "B contact absent");
                float[] before = Read(b.Smoke), beforeSurface = Read(b.Surface);
                pool.Remove("part-A:region-0");
                Require(pool.Count == 1 && !a.Smoke && !a.Surface, "Completed A channel survived");
                Require(before.SequenceEqual(Read(b.Smoke)) && beforeSurface.SequenceEqual(Read(b.Surface)), "Removing A changed unfinished B");
                Require(pool.TryAcquire("part-C:region-0", () => Make(aShape, red), out var c), "Removed owner did not free its slot");
                Require(MaxAlpha(c.Smoke) == 0 && MaxAlpha(c.Surface) == 0, "New owner inherited old smoke/damage");
                FillCell(c.Smoke);
                c.AdvanceTo(BlueprintFogSimulation.Step, new Vector3(2, .95f, 2), body, false);
                Require(MaxAlpha(c.Surface) == 0 && MaxAlpha(c.Smoke) > 0, "Smoke-only recovery case absent");
                Require(!pool.TryReleaseRecovered("part-C:region-0"), "Smoke-only owner released before query");
                AsyncGPUReadback.WaitAllRequests();
                Require(!pool.TryReleaseRecovered("part-C:region-0"), "Recovered surface discarded remaining smoke");
                pool.Remove("part-C:region-0");
                for (int tick = 151; tick <= 420; ++tick)
                    b.AdvanceTo(tick * BlueprintFogSimulation.Step, new Vector3(2, .95f, 2), body, false);
                Require(MaxAlpha(b.Surface) == 0 && MaxAlpha(b.Smoke) == 0, "B did not fully recover independently");
                Require(!pool.TryReleaseRecovered("part-B:region-0"), "Missing recovery query accepted");
                AsyncGPUReadback.WaitAllRequests();
                Require(pool.TryReleaseRecovered("part-B:region-0") && pool.Count == 0 && pool.FieldBytes == 0, "Recovered owner stayed allocated");
                Require(!b.Smoke && !b.Surface, "Recovered channel fields survived");
                bool duplicateRejected = false;
                Require(pool.TryAcquire("part-A:region-0", () => Make(aShape, red), out a), "Empty pool did not recover");
                try { pool.TryAcquire("part-B:region-0", () => a, out _); }
                catch (InvalidOperationException) { duplicateRejected = true; }
                Require(duplicateRejected && pool.Count == 1 && a.Smoke, "Same transport received two owners or was destroyed");
                bool reentryRejected = false, factoryDisposeRejected = false;
                try { pool.TryAcquire("part-B:region-0", () => { pool.TryAcquire("part-C:region-0", () => Make(aShape, red), out _); return Make(bShape, blue); }, out _); }
                catch (InvalidOperationException) { reentryRejected = true; }
                try { pool.TryAcquire("part-B:region-0", () => { pool.Dispose(); return Make(bShape, blue); }, out _); }
                catch (InvalidOperationException) { factoryDisposeRejected = true; }
                Require(reentryRejected && factoryDisposeRejected && pool.Count == 1 && a.Smoke, "Factory exceeded capacity or disposed live owners");
                Require(pool.TryAcquire("part-B:region-0", () => Make(bShape, blue), out _), "Rejected factory corrupted its pool");
                FillCell(a.Surface);
                a.AdvanceTo(BlueprintFogSimulation.Step, new Vector3(2, .95f, 2), body, false);
                Require(MaxAlpha(a.Smoke) == 0 && MaxAlpha(a.Surface) > 0, "Surface-only recovery case absent");
                Require(!pool.TryReleaseRecovered("part-A:region-0"), "Surface-only owner released before query");
                AsyncGPUReadback.WaitAllRequests();
                Require(!pool.TryReleaseRecovered("part-A:region-0"), "Empty smoke discarded damaged surface");
            }
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, independentFieldComparisons = comparisons, overlappingVoxels = overlap, bDamageBeforeRemoval = bBeforeRemoval,
                activeFieldBytes = 2 * BlueprintFogSimulation.FieldBytes, capacity = BlueprintFogChannels.Capacity,
                inactiveOwnerUnchanged = true, staleRecoveryRejected = true, noEviction = true, removalIsolated = true,
                recoveredReleased = true, emptySlotClean = true, duplicateTransportRejected = true, smokeOnlyRetained = true, surfaceOnlyRetained = true,
                factoryReentryRejected = true, factoryDisposeRejected = true,
                gameHostTested = false, renderedLookTested = false, wholeHouseSupported = false }, true));
        }
        finally { foreach (Texture3D texture in new[] { aShape, bShape, red, blue, noise }) UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static Vector3 Actor(int tick)
    {
        if (tick < 30) return Vector3.Lerp(new Vector3(-1.2f, .95f, .8f), new Vector3(0, .95f, .8f), tick / 30f);
        if (tick <= 45) return new Vector3(0, .95f, .8f);
        if (tick < 90) return Vector3.Lerp(new Vector3(0, .95f, .8f), new Vector3(.8f, .95f, 0), (tick - 45) / 45f);
        if (tick < 120) return Vector3.Lerp(new Vector3(.8f, .95f, 0), new Vector3(0, .95f, 0), (tick - 90) / 30f);
        return new Vector3(0, .95f, 0);
    }
    private static Texture3D Shape(bool perpendicular)
    {
        var texture = new Texture3D(96, 80, 80, TextureFormat.R8, false) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
        var values = new byte[96 * 80 * 80];
        for (int z = 0; z < 80; ++z) for (int y = 0; y < 80; ++y) for (int x = 0; x < 96; ++x)
        {
            float px = Mathf.Lerp(-2.4f, 2.4f, (x + .5f) / 96), py = Mathf.Lerp(-.7f, 3.8f, (y + .5f) / 80), pz = Mathf.Lerp(-1.7f, 1.7f, (z + .5f) / 80);
            float normal = perpendicular ? pz : px, along = perpendicular ? px : pz;
            values[x + 96 * (y + 80 * z)] = py > 0 && py < 2 && Mathf.Abs(along) < 1 ? (byte)Mathf.RoundToInt(255 * Mathf.Exp(-normal * normal / .008f)) : (byte)0;
        }
        texture.SetPixelData(values, 0); texture.Apply(); return texture;
    }
    private static Texture3D Pigment(Color color)
    { var texture = new Texture3D(4, 4, 4, TextureFormat.RGBA32, false); texture.SetPixels(Enumerable.Repeat(color, 64).ToArray()); texture.Apply(); return texture; }
    private static void FillCell(RenderTexture field)
    {
        var texture = new Texture3D(field.width, field.height, field.volumeDepth, TextureFormat.RGBAHalf, false);
        try
        {
            var values = new Color[field.width * field.height * field.volumeDepth];
            values[field.width / 2 + field.width * (field.height / 2 + field.height * (field.volumeDepth / 2))] = new Color(0, 0, 0, 1);
            texture.SetPixels(values); texture.Apply(); Graphics.CopyTexture(texture, field);
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }
    private static float[] Read(RenderTexture field)
    {
        var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        Require(!request.hasError, "Channel readback failed");
        var values = new float[field.width * field.height * field.volumeDepth * 4]; int offset = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        { var data = request.GetData<float>(layer); for (int i = 0; i < data.Length; ++i) values[offset + i] = data[i]; offset += data.Length; }
        Require(offset == values.Length && values.All(v => !float.IsNaN(v) && !float.IsInfinity(v)), "Incomplete/nonfinite channel readback"); return values;
    }
    private static float MaxAlpha(RenderTexture field)
    { float[] values = Read(field); float maximum = 0; for (int i = 3; i < values.Length; i += 4) maximum = Mathf.Max(maximum, values[i]); return maximum; }
    private static void Equal(RenderTexture a, RenderTexture b) => Require(Read(a).SequenceEqual(Read(b)), "Another owner changed transport fields");
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, inactiveOwnerUnchanged, staleRecoveryRejected, noEviction, removalIsolated, recoveredReleased,
            emptySlotClean, duplicateTransportRejected, smokeOnlyRetained, surfaceOnlyRetained, factoryReentryRejected, factoryDisposeRejected,
            gameHostTested, renderedLookTested, wholeHouseSupported;
        public int independentFieldComparisons, overlappingVoxels, capacity;
        public long activeFieldBytes; public float bDamageBeforeRemoval;
    }
}
