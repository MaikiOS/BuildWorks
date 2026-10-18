using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks;
using UnityEngine;

public static class FogPreparationCheck
{
    private static string BaselinePath => Path.Combine(Application.dataPath, "Editor/FogPreparationBaselineLinear.json");
    public static void CaptureBaseline()
    {
        if (File.Exists(BaselinePath)) throw new InvalidOperationException("The pinned .218 baseline must not be overwritten.");
        Require(Hash(File.ReadAllBytes(Path.Combine(Application.dataPath, "Editor/RuntimeSources/BlueprintFogSource.cs"))) ==
            "2E147AFF3C118A7ECF44C61E95C0FB7289B43715711D00BD38128CA41505FF73", "Baseline capture requires the original .218 source.");
        UnityEditor.PlayerSettings.colorSpace = ColorSpace.Linear;
        var baseline = new Baseline { sourceVersion = "0.19.218", cases = new Signature[2] };
        for (int i = 0; i < 2; ++i)
        {
            var root = Fixture(i);
            try { using (var source = new BlueprintFogSource(root, Vector3.zero)) baseline.cases[i] = Read(source); }
            finally { DestroyFixture(root); }
        }
        File.WriteAllText(BaselinePath, JsonUtility.ToJson(baseline, true));
    }
    internal static void Check(BlueprintFogAssets assets, out int cases, out double maximumStepMilliseconds)
    {
        Require(QualitySettings.activeColorSpace == ColorSpace.Linear, "Source baseline requires the native Linear color space.");
        var baseline = JsonUtility.FromJson<Baseline>(File.ReadAllText(BaselinePath));
        Require(baseline.sourceVersion == "0.19.218" && baseline.cases.Length == 2, "Pinned source baseline missing.");
        int signatures = 0, cancelled = 0, steps = 0; double maximumMs = 0;
        for (int variant = 0; variant < 2; ++variant)
        {
            var root = Fixture(variant);
            try
            {
                using (var source = new BlueprintFogSource(root, Vector3.zero))
                { Require(JsonUtility.ToJson(Read(source)) == JsonUtility.ToJson(baseline.cases[variant]), "Synchronous source changed its .218 bytes."); ++signatures; }
                using (var source = new BlueprintFogSource(root, Vector3.zero, deferPreparation: true))
                {
                    using (var pending = source.Prepare().GetEnumerator())
                    {
                        bool more;
                        do
                        {
                            var watch = System.Diagnostics.Stopwatch.StartNew(); more = pending.MoveNext();
                            maximumMs = Math.Max(maximumMs, watch.Elapsed.TotalMilliseconds); ++steps;
                            Require(steps < 10000, "Preparation did not terminate.");
                        } while (more);
                    }
                    Require(JsonUtility.ToJson(Read(source)) == JsonUtility.ToJson(baseline.cases[variant]), "Cooperative source changed its .218 bytes."); ++signatures;
                    var meshes = source.Parts.Select(part => part.Dense).ToArray();
                    source.Dispose(); Require(meshes.All(mesh => !mesh), "Source mesh survived disposal.");
                }
            }
            finally { DestroyFixture(root); }
        }
        foreach (string phase in new[] { "sampling", "filter", "upload", "shape-uploaded", "pigment-uploaded", "noise", "mesh" })
        {
            var root = Fixture(0);
            try
            {
                using (var source = new BlueprintFogSource(root, Vector3.zero, deferPreparation: true))
                {
                    using (var pending = source.Prepare().GetEnumerator())
                    {
                        int count = 0;
                        while (pending.MoveNext() && source.PreparationPhase != phase) Require(++count < 10000, "Cancel phase not reached.");
                        Require(source.PreparationPhase == phase, "Cancel did not reach " + phase);
                    }
                    Require(source.Parts.Count == 0 && !source.Shape && !source.Pigment && !source.Noise, "Cancelled source retained resources at " + phase);
                    ++cancelled;
                }
            }
            finally { DestroyFixture(root); }
        }
        var invalid = Fixture(0); invalid.transform.position = Vector3.one * 100;
        try
        {
            using (var source = new BlueprintFogSource(invalid, Vector3.zero, deferPreparation: true))
            {
                bool rejected = false;
                try { foreach (var step in source.Prepare()) { } } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && source.Parts.Count == 0 && !source.Shape && !source.Noise, "Faulted preparation retained resources or accepted an invalid domain.");
            }
        }
        finally { DestroyFixture(invalid); }
        CheckBuildingPreparation(assets, cancel: true); CheckBuildingPreparation(assets, cancel: false);
        CheckVisualReentry(assets);
        cases = signatures + cancelled + 1 + 2 + 1; maximumStepMilliseconds = maximumMs;
        File.WriteAllText(Path.Combine(Application.dataPath, "../../../artifacts/fog-published-render/preparation.json"), JsonUtility.ToJson(new PreparationReceipt {
            passed = true, sourceSignatureCases = signatures, cancelledPhases = cancelled, faultCases = 1, buildingCases = 2, visualReentryCases = 1,
            steps = steps, maxStepMilliseconds = maximumMs }, true));
    }
    private static void CheckVisualReentry(BlueprintFogAssets assets)
    {
        var prefab = Fixture(0);
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 1, 8192))
            using (var visual = new BlueprintFogVisual(assets, prefab, Vector3.up, Quaternion.identity, Vector3.one, null,
                Vector3.zero, Vector3.one * 20, new Vector2(.3f, .95f), pool, "reentry", lazyFields: true, deferPreparation: true))
            using (var first = visual.Prepare().GetEnumerator())
            {
                Require(first.MoveNext(), "First preparation did not start.");
                bool rejected = false;
                using (var second = visual.Prepare().GetEnumerator())
                    try { second.MoveNext(); } catch (InvalidOperationException) { rejected = true; }
                Require(rejected && visual.Root && visual.Source != null && !visual.Root.activeSelf, "Second preparation destroyed the first owner.");
                int steps = 0;
                while (first.MoveNext()) Require(++steps < 10000, "First preparation failed to finish after rejected reentry.");
                Require(visual.LogicalSource != null && visual.Source.Shape && visual.Root, "Rejected reentry invalidated the original preparation.");
            }
        }
        finally { DestroyFixture(prefab); }
    }
    private static void CheckBuildingPreparation(BlueprintFogAssets assets, bool cancel)
    {
        var prefab = Fixture(0); var blueprint = new CompositeBlueprintStore.Blueprint();
        for (int i = 0; i < 2; ++i) blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "staged-" + i, prefabName = "wall",
            position = new CompositeBlueprintStore.VectorData(new Vector3(i, 0, 0)), rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
            scale = new CompositeBlueprintStore.VectorData(Vector3.one) });
        try
        {
            using (var pool = BlueprintFogChannels.CreateArena(assets, 2, 8192))
            using (var building = new BlueprintFogBuilding(assets, pool, blueprint, _ => prefab, Vector3.zero, Quaternion.identity, Vector3.one * 20,
                new Vector2(.3f, .95f), deferPreparation: true))
            {
                var registered = (System.Collections.ICollection)typeof(BlueprintFogChannels)
                    .GetField("sources", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(pool);
                blueprint.parts[0].position = new CompositeBlueprintStore.VectorData(Vector3.one * 1000);
                var parts = (System.Collections.Generic.List<BlueprintFogVisual>)typeof(BlueprintFogBuilding)
                    .GetField("parts", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(building);
                using (var pending = building.Prepare().GetEnumerator())
                {
                    int steps = 0;
                    while (pending.MoveNext())
                    {
                        Require(!building.Ready && building.Clock == 0 && parts.All(part => !part.Root.activeSelf) && pool.Count == 0,
                            "Incomplete building became visible or advanced its clock.");
                        if (cancel && building.PreparedParts == 1) break;
                        Require(++steps < 10000, "Building preparation did not terminate.");
                    }
                }
                if (cancel) Require(!building.Ready && parts.Count == 0 && pool.Count == 0 && registered.Count == 0, "Cancel retained staged building parts or source registrations.");
                else Require(building.Ready && building.PreparedParts == 2 && parts.All(part => part.Root.activeSelf) &&
                    parts[0].Root.transform.position == Vector3.zero && parts.All(part => part.Source.Parts.All(mesh => mesh.Renderer.sharedMaterials.All(material => material.IsKeywordEnabled("BUILDWORKS_FOG_PUBLISHED")))),
                    "Building failed atomic activation, frozen transforms or pre-geometry publication.");
                building.Dispose(); Require(registered.Count == 0, "Disposed building retained source registrations.");
            }
        }
        finally { DestroyFixture(prefab); }
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    [Serializable] private sealed class PreparationReceipt { public bool passed; public int sourceSignatureCases, cancelledPhases, faultCases, buildingCases, visualReentryCases, steps; public double maxStepMilliseconds; }
    private static GameObject Fixture(int variant)
    {
        var root = GameObject.CreatePrimitive(PrimitiveType.Cube); root.name = "Fog preparation fixture";
        root.transform.SetPositionAndRotation(new Vector3(.15f, 1.1f, -.1f), Quaternion.Euler(variant * 12, variant * 65, variant * 7));
        root.transform.localScale = new Vector3(.2f, 1.8f, 2.1f);
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "Preparation owned albedo" };
        texture.SetPixels(Enumerable.Range(0, 16).Select(i => new Color((i % 4) / 3f, .3f, (i / 4) / 3f, i % 3 == 0 ? 0 : 1)).ToArray());
        texture.Apply();
        var material = new Material(Shader.Find("Standard")) { mainTexture = texture, color = new Color(.7f, .4f, .2f) };
        material.mainTextureScale = new Vector2(1.3f, .7f); material.mainTextureOffset = new Vector2(-.2f, .15f);
        material.SetFloat("_Cutoff", .3f); root.GetComponent<Renderer>().sharedMaterial = material; root.SetActive(false);
        return root;
    }
    private static void DestroyFixture(GameObject root)
    {
        var material = root.GetComponent<Renderer>().sharedMaterial;
        UnityEngine.Object.DestroyImmediate(material.mainTexture); UnityEngine.Object.DestroyImmediate(material); UnityEngine.Object.DestroyImmediate(root);
    }
    private static Signature Read(BlueprintFogSource source)
    {
        using (var bytes = new MemoryStream())
        using (var writer = new BinaryWriter(bytes))
        {
            foreach (var part in source.Parts)
            {
                Mesh mesh = part.Dense;
                foreach (Vector3 point in mesh.vertices) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                foreach (Vector3 point in mesh.normals) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); }
                foreach (Vector4 point in mesh.tangents) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); writer.Write(point.w); }
                for (int channel = 0; channel < 4; ++channel)
                {
                    var uv = new System.Collections.Generic.List<Vector4>(); mesh.GetUVs(channel, uv);
                    foreach (var point in uv) { writer.Write(point.x); writer.Write(point.y); writer.Write(point.z); writer.Write(point.w); }
                }
                foreach (Color color in mesh.colors) { writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a); }
                for (int sub = 0; sub < mesh.subMeshCount; ++sub) foreach (int index in mesh.GetTriangles(sub)) writer.Write(index);
            }
            return new Signature { shape = Hash(source.Shape.GetPixelData<byte>(0).ToArray()), pigment = Hash(source.Pigment.GetPixelData<byte>(0).ToArray()),
                noise = Hash(source.Noise.GetPixelData<byte>(0).ToArray()), mesh = Hash(bytes.ToArray()), occupied = source.Occupied };
        }
    }
    private static string Hash(byte[] data) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(data)).Replace("-", ""); }
    [Serializable] private sealed class Baseline { public string sourceVersion; public Signature[] cases; }
    [Serializable] private sealed class Signature { public string shape, pigment, noise, mesh; public int occupied; }
}
