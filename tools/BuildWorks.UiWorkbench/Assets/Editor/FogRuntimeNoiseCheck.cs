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

public static class FogRuntimeNoiseCheck
{
    public static void Run()
    {
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));
        string output = Path.Combine(root, "artifacts/fog-runtime-noise"); Directory.CreateDirectory(output);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); PlayerSettings.colorSpace = ColorSpace.Linear;
        var prefab = GameObject.CreatePrimitive(PrimitiveType.Cube); prefab.transform.localScale = new Vector3(.2f, 2, 2);
        var original = new Material(Shader.Find("Standard")); prefab.GetComponent<MeshRenderer>().sharedMaterial = original;
        prefab.SetActive(false);
        var probe = new Material(Shader.Find("Hidden/BuildWorks/FogNoiseProbe"));
        Vector3 origin = new Vector3(2500, 40, -1900); double maximum = 0, negative = 0;
        try
        {
            using (var assets = new BlueprintFogAssets(File.ReadAllBytes(Path.Combine(root, "src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11"))))
            using (var pool = new BlueprintFogChannels(32, BlueprintFogChannels.MaximumFieldBudget, true))
            using (var building = new BlueprintFogBuilding(assets, pool, Blueprint(3), name => prefab,
                origin, Quaternion.identity, origin - Vector3.right * 5, new Vector2(.3f, .95f)))
            {
                var parts = (List<BlueprintFogVisual>)typeof(BlueprintFogBuilding).GetField("parts", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(building);
                probe.SetVector("_ProbeOrigin", origin);
                for (int sample = 0; sample < 3; ++sample)
                {
                    probe.SetFloat("_Clock", sample * 1.5f);
                    Color[] reference = null;
                    for (int i = 0; i < parts.Count; ++i)
                    {
                        Material surface = parts[i].Root.GetComponent<MeshRenderer>().sharedMaterial;
                        Vector3 offset = surface.GetVector("_NoiseOffset");
                        Require(offset == Vector3.right * (i * 2), "Building source does not use a shared noise frame: " + i);
                        probe.SetVector("_FogOrigin", parts[i].Origin); probe.SetVector("_NoiseOffset", offset); probe.SetTexture("_Noise", parts[i].Source.Noise);
                        Color[] values = Capture(probe);
                        if (reference == null) reference = values; else maximum = Math.Max(maximum, Difference(reference, values));
                        if (i == 1)
                        {
                            probe.SetVector("_NoiseOffset", Vector3.zero);
                            negative = Math.Max(negative, Difference(reference, Capture(probe)));
                        }
                    }
                }
                Require(maximum < .000001 && negative > .001, "Shared noise GPU samples differ or old-pivot negative control is ineffective.");
                CheckTransport(assets, parts[0]); CheckReturn(parts[0].Source.Noise);
                CheckRenderBindings(assets, pool, prefab, building, origin, output);
                bool rejected = false;
                try { using (var invalid = new BlueprintFogVisual(assets, prefab, origin, Quaternion.identity, Vector3.one, null,
                    origin, origin, new Vector2(.3f, .95f), noiseOrigin: new Vector3(float.NaN, 0, 0))) { } }
                catch (ArgumentException) { rejected = true; }
                Require(rejected && pool.Count == 0, "Invalid noise origin reached the GPU or changed the field pool.");
                File.WriteAllText(Path.Combine(output, "verification.json"), JsonUtility.ToJson(new Report {
                    passed = true, owners = parts.Count, clocks = 3, maxNoiseDelta = maximum, oldPivotDelta = negative,
                    transportExact = true, emissionExact = true, returnExact = true, pairBinding = true, atlasBinding = true, invalidOriginRejected = true,
                    gameHostTested = false, wholeHouseSupported = false
                }, true));
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(probe); UnityEngine.Object.DestroyImmediate(prefab); UnityEngine.Object.DestroyImmediate(original); }
    }
    private static void CheckTransport(BlueprintFogAssets assets, BlueprintFogVisual source)
    {
        var minimum = new Vector3(-.5f, -.125f, -.5f); var maximum = minimum + Vector3.one;
        var shift = Vector3.right * 2; var actor = new Vector3(-2, 1, 0); var body = new Vector2(.3f, .95f);
        var grid = new Vector3Int(16, 16, 16);
        BlueprintFogSimulation Make(Vector3 frame, Vector3 phase) => new BlueprintFogSimulation(assets, source.Source.Shape,
            source.Source.Pigment, source.Source.Noise, minimum - frame, maximum - frame,
            minimum + Vector3.one * .125f - frame, maximum - Vector3.one * .125f - frame,
            actor - frame, 37, grid, noiseOffset: phase);
        using (var first = Make(Vector3.zero, Vector3.zero)) using (var second = Make(shift, shift)) using (var oldPivot = Make(shift, Vector3.zero))
        {
            first.AdvanceTo(8 * BlueprintFogSimulation.Step, actor, body, false);
            second.AdvanceTo(8 * BlueprintFogSimulation.Step, actor - shift, body, false);
            oldPivot.AdvanceTo(8 * BlueprintFogSimulation.Step, actor - shift, body, false);
            byte[] expected = Read(first.Flow);
            Require(expected.SequenceEqual(Read(second.Flow)), "Shared compute noise depends on its transport origin.");
            Require(!expected.SequenceEqual(Read(oldPivot.Flow)), "Compute old-pivot negative control is ineffective.");
        }
        actor = new Vector3(0, .5f, 0);
        using (var first = Make(Vector3.zero, Vector3.zero)) using (var second = Make(shift, shift))
        {
            first.AdvanceTo(BlueprintFogSimulation.Step, actor, body, true);
            second.AdvanceTo(BlueprintFogSimulation.Step, actor - shift, body, true);
            byte[] expected = Read(first.Smoke);
            Require(expected.Any(value => value != 0) && expected.SequenceEqual(Read(second.Smoke)), "Shared emission grain depends on its field origin.");
            Require(Read(first.Surface).SequenceEqual(Read(second.Surface)), "Shared contact noise depends on its field origin.");
        }
    }
    private static void CheckReturn(Texture3D noise)
    {
        var shader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Editor/FogNoiseTransportProbe.compute"));
        try
        {
            int kernel = shader.FindKernel("SampleReturn"); shader.SetTexture(kernel, "_Noise", noise); shader.SetFloat("_Seed", 37);
            using (var buffer = new ComputeBuffer(1, 16))
            {
                shader.SetBuffer(kernel, "_ProbeResult", buffer); float[] expected = null;
                for (int variant = 0; variant < 3; ++variant)
                {
                    Vector3 shift = variant == 0 ? Vector3.zero : Vector3.right * 2;
                    shader.SetVector("_ProbePosition", new Vector3(.125f, .5f, -.25f) - shift);
                    shader.SetVector("_NoiseOffset", variant == 2 ? Vector3.zero : shift); shader.Dispatch(kernel, 1, 1, 1);
                    var request = AsyncGPUReadback.Request(buffer); request.WaitForCompletion();
                    Require(!request.hasError, "Return-phase readback failed."); float[] values = request.GetData<float>().ToArray();
                    Require(values.Length == 4 && values.All(x => !float.IsNaN(x) && !float.IsInfinity(x)), "Incomplete/nonfinite return-phase probe.");
                    if (variant == 0) expected = values;
                    else Require(values.SequenceEqual(expected) == (variant == 1), "Return phase ignores the building frame or its negative control.");
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(shader); }
    }
    private static byte[] Read(RenderTexture texture)
    {
        var request = AsyncGPUReadback.Request(texture, 0); request.WaitForCompletion();
        Require(!request.hasError, "Noise transport readback failed."); var bytes = new List<byte>();
        for (int layer = 0; layer < request.layerCount; ++layer) bytes.AddRange(request.GetData<byte>(layer).ToArray());
        Require(bytes.Count == texture.width * texture.height * texture.volumeDepth * 8, "Incomplete noise transport readback."); return bytes.ToArray();
    }
    private static object Field(object target, string name) => target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
    private static void CheckRenderBindings(BlueprintFogAssets assets, BlueprintFogChannels pool, GameObject prefab,
        BlueprintFogBuilding building, Vector3 origin, string output)
    {
        var node = new GameObject("Noise binding camera"); var camera = node.AddComponent<Camera>();
        camera.transform.position = origin + new Vector3(3, 2, -4); camera.transform.LookAt(origin + Vector3.up);
        camera.depthTextureMode = DepthTextureMode.Depth; var probe = node.AddComponent<FogRenderProbe>();
        try
        {
            FogRuntimePairCheck.Capture(camera, probe, (image, target) => building.Render(camera, image, target, origin, Vector3.right, 0), output, "atlas-bindings");
            var atlas = Field(Field(building, "volume"), "atlas"); var material = (Material)Field(atlas, "material");
            Vector4[] offsets = material.GetVectorArray("_RegionNoiseOffset");
            Require(offsets.Length >= 3 && (Vector3)offsets[1] == Vector3.right * 2 && (Vector3)offsets[2] == Vector3.right * 4,
                "Atlas omitted building noise coordinates.");
            Quaternion rotation = Quaternion.Euler(0, 90, 0);
            using (var pair = new BlueprintFogBuilding(assets, pool, Blueprint(2), name => prefab, origin, rotation,
                origin - Vector3.right * 5, new Vector2(.3f, .95f)))
            {
                FogRuntimePairCheck.Capture(camera, probe, (image, target) => pair.Render(camera, image, target, origin, Vector3.right, 0), output, "pair-bindings");
                material = (Material)Field(Field(pair, "volume"), "material");
                Require((Vector3)material.GetVector("_NoiseOffsetB") == rotation * Vector3.right * 2, "Rotated pair omitted building noise coordinates.");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(node); }
    }
    private static CompositeBlueprintStore.Blueprint Blueprint(int count)
    {
        var result = new CompositeBlueprintStore.Blueprint();
        for (int i = 0; i < count; ++i) result.parts.Add(new CompositeBlueprintStore.Part {
            stableId = "noise-" + i, prefabName = "noise-wall", position = new CompositeBlueprintStore.VectorData(Vector3.right * (i * 2)),
            rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity), scale = new CompositeBlueprintStore.VectorData(Vector3.one)
        });
        return result;
    }
    private static Color[] Capture(Material material)
    {
        var target = RenderTexture.GetTemporary(16, 16, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var previous = RenderTexture.active; Texture2D read = null;
        try
        {
            Graphics.Blit(Texture2D.blackTexture, target, material); RenderTexture.active = target;
            read = new Texture2D(16, 16, TextureFormat.RGBAFloat, false, true); read.ReadPixels(new Rect(0, 0, 16, 16), 0, 0); read.Apply();
            return read.GetPixels();
        }
        finally { RenderTexture.active = previous; if (read) UnityEngine.Object.DestroyImmediate(read); RenderTexture.ReleaseTemporary(target); }
    }
    private static double Difference(Color[] first, Color[] second)
    {
        double maximum = 0;
        for (int i = 0; i < first.Length; ++i) for (int c = 0; c < 4; ++c)
        {
            if (float.IsNaN(first[i][c]) || float.IsNaN(second[i][c]) || float.IsInfinity(first[i][c]) || float.IsInfinity(second[i][c]))
                throw new InvalidOperationException("Nonfinite noise probe.");
            maximum = Math.Max(maximum, Math.Abs(first[i][c] - second[i][c]));
        }
        return maximum;
    }
    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    [Serializable] private sealed class Report
    {
        public bool passed, gameHostTested, wholeHouseSupported, transportExact, emissionExact, returnExact, pairBinding, atlasBinding, invalidOriginRejected;
        public int owners, clocks; public double maxNoiseDelta, oldPivotDelta;
    }
}
