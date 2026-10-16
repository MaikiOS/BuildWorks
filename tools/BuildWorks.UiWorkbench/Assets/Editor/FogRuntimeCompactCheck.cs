using System;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using UnityEngine;
using UnityEngine.Rendering;

public static class FogRuntimeCompactCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-compact"); Directory.CreateDirectory(output);
        Vector3 minimum = new Vector3(-1.2f, -.7f, -1.2f), maximum = new Vector3(1.2f, 1.5f, 1.2f);
        Vector3 restMin = new Vector3(-.35f, -.45f, -.35f), restMax = new Vector3(.35f, .8f, .35f);
        Vector3Int grid = BlueprintFogSimulation.CompactGrid(minimum, maximum);
        Require(grid == new Vector3Int(32, 40, 36), "Unexpected compact dispatch grid");
        Vector3Int original = BlueprintFogSimulation.CompactGrid(new Vector3(-3.6f, -.7f, -2.7f), new Vector3(3.6f, 4.4f, 2.7f));
        Require(original == new Vector3Int(96, 80, 80), "Original physical spacing changed");
        Reject(() => BlueprintFogSimulation.CompactGrid(maximum, minimum));
        Reject(() => BlueprintFogSimulation.CompactGrid(Vector3.zero, new Vector3(float.NaN, 1, 1)));
        Reject(() => BlueprintFogSimulation.CompactGrid(Vector3.zero, Vector3.one * 30));
        var shape = new Texture3D(16, 16, 16, TextureFormat.R8, false) { filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp };
        var values = new byte[4096];
        for (int z = 1; z < 15; ++z) for (int y = 1; y < 15; ++y) for (int x = 7; x <= 8; ++x) values[x + 16 * (y + 16 * z)] = 255;
        shape.SetPixelData(values, 0); shape.Apply();
        var pigment = new Texture3D(4, 4, 4, TextureFormat.RGBA32, false);
        pigment.SetPixels(Enumerable.Repeat(new Color(.35f, .2f, .1f, 1), 64).ToArray()); pigment.Apply();
        var noise = new Texture3D(4, 4, 4, TextureFormat.R8, false);
        noise.SetPixelData(Enumerable.Repeat((byte)127, 64).ToArray(), 0); noise.Apply();
        Vector3 start = new Vector3(-.9f, .2f, 0); Vector2 body = new Vector2(.25f, .65f);
        float damage = 0, smoke = 0, finalDamage = 0, finalSmoke = 0; long fieldBytes = 0; int fields = 0;
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var pool = new BlueprintFogChannels())
            {
                BlueprintFogSimulation Make(Vector3Int? size) => new BlueprintFogSimulation(assets, shape, pigment, noise,
                    minimum, maximum, restMin, restMax, start, 37, size);
                foreach (var invalid in new[] { Vector3Int.zero, new Vector3Int(31,40,36), new Vector3Int(256,256,256), new Vector3Int(8,8,4), new Vector3Int(int.MaxValue,8,4) })
                    Reject(() => { using (var bad = Make(invalid)) { } });
                Require(pool.TryAcquire("part-A:patch-0", () => Make(grid), out var compact), "Compact region not admitted");
                Require(pool.TryAcquire("part-B:patch-0", () => Make(null), out var legacy), "Mixed region not admitted");
                fieldBytes = compact.AllocatedFieldBytes;
                Require(fieldBytes == 3686400 && pool.FieldBytes == fieldBytes + BlueprintFogSimulation.FieldBytes, "Mixed allocation accounting incorrect");
                foreach (var field in typeof(BlueprintFogSimulation).GetFields(BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    object value = field.GetValue(compact);
                    RenderTexture[] textures = value is RenderTexture texture ? new[] { texture } : value as RenderTexture[];
                    if (textures == null) continue;
                    foreach (RenderTexture rt in textures)
                    {
                        Require(rt && rt.IsCreated() && rt.width == grid.x && rt.height == grid.y && rt.volumeDepth == grid.z && rt.format == RenderTextureFormat.ARGBHalf,
                            "Actual field dimensions/format differ from compact budget"); ++fields;
                    }
                }
                Require(fields == 10 && MaxAlpha(compact.Smoke) == 0 && MaxAlpha(compact.Surface) == 0, "Compact allocation not clean");
                for (int tick = 1; tick <= 300; ++tick)
                {
                    Vector3 actor = tick < 30 ? Vector3.Lerp(start, new Vector3(0, .2f, 0), tick / 30f)
                        : tick <= 90 ? new Vector3(0, .2f, 0) : new Vector3(.9f, .2f, .9f);
                    compact.AdvanceTo(tick * BlueprintFogSimulation.Step, actor, body, tick <= 90);
                    if (tick == 60 || tick == 90)
                    {
                        damage = Mathf.Max(damage, MaxAlpha(compact.Surface)); smoke = Mathf.Max(smoke, MaxAlpha(compact.Smoke));
                        Require(MaxAlpha(compact.Surface) > .5f && MaxAlpha(compact.Smoke) > 0, "Compact contact/standing did not retain its hole");
                        Require(MaxAlpha(legacy.Surface) == 0 && MaxAlpha(legacy.Smoke) == 0, "Compact contact changed inactive owner");
                    }
                }
                finalDamage = MaxAlpha(compact.Surface); finalSmoke = MaxAlpha(compact.Smoke);
                Require(finalDamage == 0 && finalSmoke == 0, "Compact matter did not return completely");
                Require(!pool.TryReleaseRecovered("part-A:patch-0"), "Compact recovery accepted without readback");
                AsyncGPUReadback.WaitAllRequests();
                Require(pool.TryReleaseRecovered("part-A:patch-0") && pool.FieldBytes == BlueprintFogSimulation.FieldBytes && legacy.Smoke,
                    "Compact recovery removed another owner or retained its budget");
                Require(!compact.Smoke && !compact.Surface, "Compact fields survived recovery");
            }
            File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                passed = true, gridX = grid.x, gridY = grid.y, gridZ = grid.z, fields = fields, ticks = 300,
                fieldBytes = fieldBytes, originalFieldBytes = BlueprintFogSimulation.FieldBytes,
                contactDamage = damage, contactSmoke = smoke, recoveredDamage = finalDamage, recoveredSmoke = finalSmoke,
                mixedBudgetExact = true, independentOwner = true, recoveredReleased = true, invalidRejected = true,
                originalGridRetained = true, gameHostTested = false, wholeHouseSupported = false }, true));
        }
        finally { UnityEngine.Object.DestroyImmediate(shape); UnityEngine.Object.DestroyImmediate(pigment); UnityEngine.Object.DestroyImmediate(noise); }
    }

    private static float MaxAlpha(RenderTexture field)
    {
        var request = AsyncGPUReadback.Request(field, 0, TextureFormat.RGBAFloat); request.WaitForCompletion();
        Require(!request.hasError, "Compact readback failed"); int count = 0; float maximum = 0;
        for (int layer = 0; layer < request.layerCount; ++layer)
        {
            var data = request.GetData<float>(layer);
            for (int i = 0; i < data.Length; ++i)
            { Require(!float.IsNaN(data[i]) && !float.IsInfinity(data[i]), "Nonfinite compact field"); if (i % 4 == 3) maximum = Mathf.Max(maximum, data[i]); }
            count += data.Length;
        }
        Require(count == field.width * field.height * field.volumeDepth * 4, "Incomplete compact field readback"); return maximum;
    }
    private static void Reject(Action action)
    { bool rejected = false; try { action(); } catch (ArgumentException) { rejected = true; } Require(rejected, "Invalid compact grid accepted"); }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, mixedBudgetExact, independentOwner, recoveredReleased, invalidRejected, originalGridRetained, gameHostTested, wholeHouseSupported;
        public int gridX, gridY, gridZ, fields, ticks; public long fieldBytes, originalFieldBytes;
        public float contactDamage, contactSmoke, recoveredDamage, recoveredSmoke;
    }
}
