using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Frozen = BuildWorks.FogRuntimeReference.FogMatterSimulation;

public static class FogRuntimeSimulationCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-simulation");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output, "samples.txt"), "");
        var shape = new Texture3D(96, 80, 80, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        var pigment = new Texture3D(16, 16, 16, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
        Texture3D noise = Noise();
        var host = new GameObject("Frozen v11 simulation verifier");
        ComputeShader referenceSolver = null;
        BlueprintFogSimulation candidate = null;
        int comparisons = 0, ticks = 0;
        float contact = 0, recovered = 0, peakSmoke = 0;
        Vector3 restMin = new Vector3(-2.4f, -.7f, -1.7f), restMax = new Vector3(2.4f, 3.8f, 1.7f);
        Vector3 minimum = restMin - new Vector3(1.2f, 0, 1), maximum = restMax + new Vector3(1.2f, .6f, 1);
        try
        {
            var density = new byte[96 * 80 * 80];
            for (int z = 0; z < 80; ++z) for (int y = 0; y < 80; ++y) for (int x = 0; x < 96; ++x)
            {
                Vector3 p = new Vector3(Mathf.Lerp(restMin.x, restMax.x, (x + .5f) / 96),
                    Mathf.Lerp(restMin.y, restMax.y, (y + .5f) / 80), Mathf.Lerp(restMin.z, restMax.z, (z + .5f) / 80));
                density[x + 96 * (y + 80 * z)] = p.y > 0 && p.y < 2 && Mathf.Abs(p.z) < 1
                    ? (byte)Mathf.RoundToInt(255 * Mathf.Exp(-p.x * p.x / .008f)) : (byte)0;
            }
            shape.SetPixelData(density, 0); shape.Apply();
            pigment.SetPixels(Enumerable.Range(0, 16 * 16 * 16).Select(i => new Color(.25f + i % 16 * .02f, .2f, .1f, 1)).ToArray());
            pigment.Apply();
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostV11/buildworks-fog-v11"))))
            {
                referenceSolver = UnityEngine.Object.Instantiate(assets.Solver);
                var reference = host.AddComponent<Frozen>();
                reference.solver = referenceSolver; reference.shape = shape; reference.pigment = pigment; reference.noise = noise;
                reference.minimum = minimum; reference.maximum = maximum; reference.restMinimum = restMin; reference.restMaximum = restMax;
                Vector3 start = Actor(0);
                reference.ResetState(0, start, false, 0, 37);
                candidate = new BlueprintFogSimulation(assets, shape, pigment, noise, minimum, maximum, restMin, restMax, start, 37);
                Reject(() => candidate.AdvanceTo(float.NaN, start, new Vector2(.3f, .95f), true));
                Reject(() => candidate.AdvanceTo(1, start, new Vector2(.3f, .95f), true));
                Reject(() => candidate.AdvanceTo(0, start, Vector2.zero, true));
                Reject(() => candidate.AdvanceTo(0, new Vector3(2500, 0, 0), new Vector2(.3f, .95f), true));
                Require(candidate.Tick == 0, "Rejected input changed simulation clock");
                foreach (int frame in Enumerable.Range(0, 271))
                {
                    if (frame >= 64 && frame <= 70) continue; // Exercise the maximum eight-step catch-up during motion.
                    float time = frame * BlueprintFogSimulation.Step + (frame % 2 == 0 ? .01f : 0);
                    Vector3 actor = Actor(time);
                    reference.Evaluate(time, actor, new Vector2(.3f, .95f), false, 0, 37, true);
                    candidate.AdvanceTo(time, actor, new Vector2(.3f, .95f), true);
                    ticks = candidate.Tick;
                    if (frame == 0 || frame == 78 || frame == 96 || frame == 120 || frame == 180 || frame == 270)
                    {
                        float smoke = MaxAlpha(candidate.Smoke), damage = MaxAlpha(candidate.Surface);
                        peakSmoke = Mathf.Max(peakSmoke, smoke);
                        File.AppendAllText(Path.Combine(output, "samples.txt"), "tick=" + frame + " smoke=" + smoke + " damage=" + damage + "\n");
                        Equal(reference.Smoke, candidate.Smoke, "smoke", frame);
                        Equal(reference.Flow, candidate.Flow, "flow", frame);
                        Equal(reference.Surface, candidate.Surface, "surface", frame);
                        comparisons += 3;
                    }
                    if (frame == 96)
                    {
                        contact = MaxAlpha(candidate.Surface);
                    }
                }
                recovered = MaxAlpha(candidate.Surface);
                Require(peakSmoke > 0, "Body contact emitted no transported matter");
                Require(contact > .5f && recovered < contact, "Standing body / later recovery not exercised");
                Reject(() => candidate.AdvanceTo(0, start, new Vector2(.3f, .95f), true));
                candidate.Dispose(); candidate.Dispose();
                Require(!candidate.Smoke && !candidate.Flow && !candidate.Surface, "Disposed fields remain alive");
                bool disposedRejected = false;
                try { candidate.AdvanceTo(9, start, new Vector2(.3f, .95f), true); }
                catch (ObjectDisposedException) { disposedRejected = true; }
                Require(disposedRejected, "Disposed simulation accepted an advance");
                Vector3 inside = new Vector3(0, .95f, 0);
                candidate = new BlueprintFogSimulation(assets, shape, pigment, noise, minimum, maximum, restMin, restMax, inside, 37);
                candidate.AdvanceTo(8 * BlueprintFogSimulation.Step, inside, new Vector2(.3f, .95f), false);
                Require(MaxAlpha(candidate.Surface) == 0 && MaxAlpha(candidate.Smoke) == 0, "Disabled emission changed surface or created smoke");
                candidate.Dispose();
            }
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, unity = Application.unityVersion, graphics = SystemInfo.graphicsDeviceType.ToString(),
                ticks = ticks, exactFieldComparisons = comparisons, fieldBytes = BlueprintFogSimulation.FieldBytes,
                standingDamage = contact, recoveredDamage = recovered, invalidInputsRejected = true, released = true,
                gameHostTested = false, renderedLookTested = false, fractionalIntervals = true, eightStepCatchUp = true, disabledEmission = true
            }, true));
            Debug.Log("BuildWorks runtime v11 simulation: frozen-reference exact GPU fields / contact / return / validation / disposal PASSED");
        }
        finally
        {
            candidate?.Dispose(); UnityEngine.Object.DestroyImmediate(host);
            if (referenceSolver) UnityEngine.Object.DestroyImmediate(referenceSolver);
            UnityEngine.Object.DestroyImmediate(shape); UnityEngine.Object.DestroyImmediate(pigment); UnityEngine.Object.DestroyImmediate(noise);
        }
    }

    private static Vector3 Actor(float time)
    {
        float x = time <= 2.6f ? Mathf.Lerp(-1.8f, 0, Mathf.Clamp01((time - 2) / .6f))
            : time <= 3.3f ? 0 : Mathf.Lerp(0, 1.8f, Mathf.Clamp01((time - 3.3f) / .6f));
        return new Vector3(x, .95f, 0);
    }
    private static byte[] Read(RenderTexture field)
    {
        var request = AsyncGPUReadback.Request(field, 0); request.WaitForCompletion();
        Require(!request.hasError, "GPU field readback failed");
        byte[] bytes = new byte[BlueprintFogSimulation.Nx * BlueprintFogSimulation.Ny * BlueprintFogSimulation.Nz * 8];
        int offset = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        {
            var data = request.GetData<byte>(layer);
            Require(offset + data.Length <= bytes.Length, "GPU field exceeds expected byte count");
            Array.Copy(data.ToArray(), 0, bytes, offset, data.Length); offset += data.Length;
        }
        Require(offset == bytes.Length, "GPU comparison must read the entire 3D field"); return bytes;
    }
    private static void Equal(RenderTexture reference, RenderTexture actual, string label, int frame)
    { Require(Read(reference).SequenceEqual(Read(actual)), "Frozen v11 " + label + " differs at tick " + frame); }
    private static float MaxAlpha(RenderTexture field)
    {
        var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        Require(!request.hasError, "GPU alpha readback failed"); float maximum = 0; int count = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        {
            var values = request.GetData<float>(layer); count += values.Length;
            for (int i = 3; i < values.Length; i += 4) { Require(!float.IsNaN(values[i]) && !float.IsInfinity(values[i]), "Non-finite matter"); maximum = Mathf.Max(maximum, values[i]); }
        }
        Require(count == BlueprintFogSimulation.Nx * BlueprintFogSimulation.Ny * BlueprintFogSimulation.Nz * 4, "Alpha probe must read all 3D layers");
        return maximum;
    }
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Require(rejected, "Invalid input accepted"); }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static Texture3D Noise()
    {
        const int n = 64; var bytes = new byte[n * n * n];
        float Hash(int x, int y, int z)
        {
            uint h = unchecked((uint)((x & 15) * 374761393 + (y & 15) * 668265263 + (z & 15) * 1274126177));
            h = (h ^ (h >> 13)) * 1274126177u; return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
        for (int z = 0; z < n; ++z) for (int y = 0; y < n; ++y) for (int x = 0; x < n; ++x)
        {
            int ix = x / 4, iy = y / 4, iz = z / 4;
            float u = Mathf.SmoothStep(0, 1, x % 4 / 4f), v = Mathf.SmoothStep(0, 1, y % 4 / 4f), w = Mathf.SmoothStep(0, 1, z % 4 / 4f);
            float low = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz), Hash(ix + 1, iy, iz), u), Mathf.Lerp(Hash(ix, iy + 1, iz), Hash(ix + 1, iy + 1, iz), u), v);
            float high = Mathf.Lerp(Mathf.Lerp(Hash(ix, iy, iz + 1), Hash(ix + 1, iy, iz + 1), u), Mathf.Lerp(Hash(ix, iy + 1, iz + 1), Hash(ix + 1, iy + 1, iz + 1), u), v);
            bytes[x + n * (y + n * z)] = (byte)(Mathf.Lerp(low, high, w) * 255);
        }
        var texture = new Texture3D(n, n, n, TextureFormat.R8, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear };
        texture.SetPixelData(bytes, 0); texture.Apply(); return texture;
    }
    [Serializable] private sealed class Report
    {
        public bool passed, invalidInputsRejected, released, gameHostTested, renderedLookTested, fractionalIntervals, eightStepCatchUp, disabledEmission;
        public string unity, graphics; public int ticks, exactFieldComparisons; public long fieldBytes;
        public float standingDamage, recoveredDamage;
    }
}
