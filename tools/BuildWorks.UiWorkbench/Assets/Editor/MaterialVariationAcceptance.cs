using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class MaterialVariationAcceptance
{
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("Material variation: " + reason); }

    internal static void RunPlacement(string output)
    {
        var usedLayers = new bool[32];
        foreach (Renderer renderer in Resources.FindObjectsOfTypeAll<Renderer>())
            if (renderer && renderer.gameObject.scene.IsValid()) usedLayers[renderer.gameObject.layer] = true;
        int layer = 28;
        while (layer >= 8 && usedLayers[layer]) --layer;
        Require(layer >= 8, "no isolated proof layer available");
        var source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        source.name = "Material placement source";
        source.layer = layer;
        Object.DestroyImmediate(source.GetComponent<Collider>());
        Piece piece = source.AddComponent<Piece>();
        var component = source.AddComponent<RandomMaterialValues>();
        var property = new RandomMaterialValues.VectorVariationProperty {
            m_minimum = new Vector4(.05f, .05f, .05f, 1), m_maximum = new Vector4(.95f, .95f, .95f, 1),
            m_propertyNames = new List<string> { "_Variation" } };
        component.m_vectorProperties.Add(property);
        var material = new Material(Shader.Find("BuildWorksTests/MaterialVariationProof"));
        source.GetComponent<Renderer>().sharedMaterial = material;
        source.SetActive(false);
        var cameraObject = new GameObject("Material placement render camera");
        var camera = cameraObject.AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << layer;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
        camera.orthographic = true; camera.orthographicSize = 2;
        camera.transform.position = new Vector3(0, 0, -10);
        var target = new RenderTexture(256, 256, 24);
        target.antiAliasing = 1; camera.targetTexture = target;
        RenderTexture previous = RenderTexture.active;
        var plan = new List<PrecisionPlacementSession.TransformSnapshot>();
        var pieces = new List<Piece>();
        var clones = new List<GameObject>();
        using (var preview = new BlueprintBatchedPreview())
        using (var small = new PlacementGhostPreviewView())
        try
        {
            var caller = new MaterialPropertyBlock(); caller.SetFloat("_Sentinel", 37);
            var actual = new MaterialPropertyBlock();
            Color32[] reference = null;
            foreach (int seed in new[] { 0, 1234, 0 })
            {
                Vector3 position = new Vector3((plan.Count - 1) * 1.3f, 0, 0);
                plan.Add(new PrecisionPlacementSession.TransformSnapshot(position, Quaternion.identity, materialSeed: seed));
                pieces.Add(piece);
                GameObject clone = PlacementGhostPreviewView.CreateVisualClone(source, "Material baseline", layer, seed);
                clone.transform.position = position;
                clone.GetComponent<Renderer>().GetPropertyBlock(actual);
                actual.SetFloat("_Sentinel", 37); clone.GetComponent<Renderer>().SetPropertyBlock(actual);
                clones.Add(clone);
            }
            foreach (bool instanceProperty in new[] { false, true })
            {
                if (instanceProperty) material.EnableKeyword("VARIATION_INSTANCED");
                else material.DisableKeyword("VARIATION_INSTANCED");
                foreach (GameObject clone in clones) clone.SetActive(true);
                material.enableInstancing = true;
                Color32[] baseline = ReadRender(camera, target, output, "material-clones-" + instanceProperty);
                if (reference == null) reference = baseline;
                Require(!SamePixel(baseline[128 * 256 + 45], baseline[128 * 256 + 128]), "render did not distinguish seeds");
                Require(SamePixel(baseline[128 * 256 + 45], baseline[128 * 256 + 211]), "repeated seed changed appearance");
                foreach (int center in new[] { 45, 128, 211 })
                    for (int y = 110; y < 146; ++y)
                        for (int x = center - 16; x < center + 16; ++x)
                            Require(SamePixel(baseline[y * 256 + x], baseline[128 * 256 + center]), "foreign geometry contaminated a proof cube");
                foreach (GameObject clone in clones) clone.SetActive(false);
                preview.Prepare(plan, pieces, layer);
                Require(preview.Ready && preview.TemplateCount == 1, "seed variants created geometry templates");
                foreach (bool instanced in new[] { true, false })
                {
                    material.enableInstancing = instanced;
                    preview.Draw(plan, caller, layer, -1, Matrix4x4.identity, camera);
                    Color32[] rendered = ReadRender(camera, target, output, "material-batch-" + instanceProperty + "-" + instanced);
                    int different = 0;
                    for (int index = 0; index < baseline.Length; ++index)
                        if (!SamePixel(baseline[index], rendered[index])) ++different;
                    Require(different < 40, "render differs from individual clones: " + instanceProperty + "/" + instanced + " pixels=" + different);
                }
            }
            // A reused property block must also handle larger batches and a shorter final flush.
            for (int index = 0; index < 2050; ++index)
            {
                plan.Add(new PrecisionPlacementSession.TransformSnapshot(new Vector3(1000 + index, 0, 0), Quaternion.identity, materialSeed: 0));
                pieces.Add(piece);
            }
            preview.Prepare(plan, pieces, layer);
            material.enableInstancing = true;
            preview.Draw(plan, caller, layer, -1, Matrix4x4.identity, camera);
            Color32[] grown = ReadRender(camera, target, output, "material-grown-batch");
            for (int index = 0; index < reference.Length; ++index)
                Require(SamePixel(reference[index], grown[index]), "growing or multi-flush batch changed appearance");
            plan.RemoveRange(3, plan.Count - 3); pieces.RemoveRange(3, pieces.Count - 3);
            caller.SetVector("_Variation", Vector4.one);
            preview.Prepare(plan, pieces, layer);
            preview.Draw(plan, caller, layer, -1, Matrix4x4.identity, camera);
            Color32[] conflict = ReadRender(camera, target, output, "material-caller-conflict");
            for (int index = 0; index < reference.Length; ++index)
                Require(SamePixel(reference[index], conflict[index]), "caller scalar collision corrupted variation");
            Require(caller.GetVector("_Variation") == Vector4.one, "caller scalar collision mutated caller");
            caller.Clear(); caller.SetFloat("_Sentinel", 37);
            Require(caller.GetFloat("_Sentinel") == 37 && !caller.HasProperty("_Variation"), "Draw changed caller MPB");
            source.GetComponent<Renderer>().GetPropertyBlock(actual);
            Require(!actual.HasProperty("_Variation"), "Draw changed source renderer");

            small.Show(source, plan, pieces, -1);
            var instances = (List<GameObject>)typeof(PlacementGhostPreviewView).GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(small);
            instances[0].GetComponent<Renderer>().GetPropertyBlock(actual);
            Require(actual.GetVector("_Variation") == property.GetValue(0), "small placement lost seed zero");
            GameObject before = instances[0];
            plan[0] = new PrecisionPlacementSession.TransformSnapshot(plan[0].Position, Quaternion.identity, materialSeed: 1234);
            small.Show(source, plan, pieces, -1);
            instances[0].GetComponent<Renderer>().GetPropertyBlock(actual);
            Require(instances[0] != before && !before.activeSelf && actual.GetVector("_Variation") == property.GetValue(1234), "small placement seed cache stale");
            small.Hide();
            caller.SetColor("_Color", Color.green);
            preview.Prepare(plan, pieces, layer);
            preview.Draw(plan, caller, layer, -1, Matrix4x4.identity, camera);
            foreach (object entry in (System.Collections.IEnumerable)typeof(BlueprintBatchedPreview).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview))
            {
                object batch = entry.GetType().GetProperty("Value").GetValue(entry);
                var block = (MaterialPropertyBlock)batch.GetType().GetField("Properties", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(batch);
                Require(block.GetColor("_Color") == Color.green && block.GetFloat("_Sentinel") == 37, "batch erased native ghost tint or caller properties");
            }

            // Start the unique-seed allocation case without the intentionally grown reusable buffer.
            preview.Dispose();
            plan.Clear(); pieces.Clear();
            for (int index = 0; index < 50000; ++index)
            {
                plan.Add(new PrecisionPlacementSession.TransformSnapshot(new Vector3(index, 0, 0), Quaternion.identity, materialSeed: index));
                pieces.Add(piece);
            }
            preview.Prepare(plan, pieces, layer);
            Require(!preview.Ready && preview.PreparedCount <= 258, "variant preparation was not bounded");
            int calls = 1;
            while (!preview.Ready && ++calls < 200) preview.Prepare(plan, pieces, layer);
            Require(preview.Ready && preview.TemplateCount == 1 && preview.PreparedCount == 50000, "heavy seeded plan lost parts or duplicated templates");
            // Camera sees only the first few parts, but submission covers every distinct seed.
            preview.Draw(plan, caller, layer, -1, Matrix4x4.identity, camera);
            int matrices = 0;
            foreach (object entry in (System.Collections.IEnumerable)typeof(BlueprintBatchedPreview).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview))
            {
                object batch = entry.GetType().GetProperty("Value").GetValue(entry);
                matrices += ((Matrix4x4[])batch.GetType().GetField("Matrices", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(batch)).Length;
            }
            Require(matrices <= 50004, "distinct seeds allocated full 1023-matrix buffers");
            plan.RemoveRange(1, plan.Count - 1); pieces.RemoveRange(1, pieces.Count - 1);
            preview.Prepare(plan, pieces, layer);
            Require(((System.Collections.IDictionary)typeof(BlueprintBatchedPreview).GetField("variants", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview)).Count == 1,
                "old variants retained after plan change");
            Require(((System.Collections.IDictionary)typeof(BlueprintBatchedPreview).GetField("batches", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview)).Count == 0,
                "old batch buffers retained after plan change");
            plan[0] = new PrecisionPlacementSession.TransformSnapshot(Vector3.zero, Quaternion.identity, materialSeed: -1);
            small.Show(source, plan, pieces, -1);
            Require(small.HeavyError != null && instances.TrueForAll(instance => !instance || !instance.activeSelf), "unsupported small preview leaked visible objects");
            File.WriteAllText(Path.Combine(output, "material-placement-check.txt"), "PASS small preview/cache/error cleanup; uniform and instanced shader properties; clones/batched/fallback GPU comparison; growing/multiple flush batches and scalar caller collision; native tint/caller isolation; 50000 distinct seeds/one geometry template/bounded preparation and buffers/cache pruning. Native ghost, placement, reload, network, FPS and Valheim shader appearance remain unverified.\n");
        }
        finally
        {
            RenderTexture.active = previous; camera.targetTexture = null;
            target.Release(); Object.DestroyImmediate(target); Object.DestroyImmediate(cameraObject);
            foreach (GameObject clone in clones) Object.DestroyImmediate(clone);
            Object.DestroyImmediate(source); Object.DestroyImmediate(material);
        }
    }

    private static bool SamePixel(Color32 a, Color32 b) =>
        Math.Abs(a.r - b.r) <= 2 && Math.Abs(a.g - b.g) <= 2 && Math.Abs(a.b - b.b) <= 2;

    private static Color32[] ReadRender(Camera camera, RenderTexture target, string output, string name)
    {
        camera.Render(); RenderTexture.active = target;
        var pixels = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
        try
        {
            pixels.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); pixels.Apply();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), pixels.EncodeToPNG());
            return pixels.GetPixels32();
        }
        finally { Object.DestroyImmediate(pixels); }
    }

    internal static void Run(Camera camera, GameObject wall)
    {
        GameObject source = PlacementGhostPreviewView.CreateVisualClone(wall, "Variation source", wall.layer);
        var branch = new GameObject("Nested scope");
        branch.transform.SetParent(source.transform, false);
        GameObject shape = PlacementGhostPreviewView.CreateVisualClone(wall, "Nested mesh", wall.layer);
        shape.transform.SetParent(branch.transform, false);
        var component = branch.AddComponent<RandomMaterialValues>();
        var property = new RandomMaterialValues.VectorVariationProperty {
            m_minimum = new Vector4(.1f, .2f, .3f, .4f), m_maximum = Vector4.one,
            m_propertyNames = new List<string> { "_Variation", "_VariationAlias" } };
        component.m_vectorProperties.Add(property);
        component.m_vectorProperties.Add(property);
        GameObject a = null, b = null;
        try
        {
            var original = new MaterialPropertyBlock(); original.SetFloat("_Unrelated", 42);
            shape.GetComponentInChildren<Renderer>().SetPropertyBlock(original);
            a = PlacementGhostPreviewView.CreateVisualClone(source, "Seed zero", source.layer, 0);
            b = PlacementGhostPreviewView.CreateVisualClone(source, "Seed maximum", source.layer, int.MaxValue);
            var actual = new MaterialPropertyBlock();
            Renderer aNested = a.transform.GetChild(a.transform.childCount - 1).GetComponentInChildren<Renderer>();
            Renderer bNested = b.transform.GetChild(b.transform.childCount - 1).GetComponentInChildren<Renderer>();
            aNested.GetPropertyBlock(actual);
            Vector4 zero = property.GetValue(1);
            Require(actual.GetVector("_Variation") == zero && actual.GetVector("_VariationAlias") == zero,
                "seed zero, nested renderer scope or native property-index offset differs");
            bNested.GetPropertyBlock(actual);
            Require(actual.GetVector("_Variation") == property.GetValue(int.MinValue), "Int32 seed wrap differs");
            shape.GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
            Require(actual.GetFloat("_Unrelated") == 42 && actual.GetVector("_Variation") == Vector4.zero,
                "source renderer properties were mutated");
            Renderer outside = a.GetComponent<Renderer>();
            if (outside)
            {
                outside.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == Vector4.zero, "child variation escaped its scope");
            }

            using (var scene = new BlueprintEditorScene(camera, name => source))
            {
                var part = new BlueprintEditorPart("seeded", "woodwall", "Seeded", new Point3(0, 0, 0),
                    new Rotation3(0, 0, 0, 1), materialSeed: 0);
                var doc = new BlueprintEditorDocument(null, "Variation", "OTHER", new[] { part });
                Require(scene.TrySync(doc, out string error), error);
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                object visual = ((System.Collections.IDictionary)scene.GetType().GetField("visuals", flags).GetValue(scene))["seeded"];
                var publicFlags = BindingFlags.Instance | BindingFlags.Public;
                var root = (GameObject)visual.GetType().GetField("Root", publicFlags).GetValue(visual);
                Renderer nested = root.transform.GetChild(root.transform.childCount - 1).GetComponentInChildren<Renderer>();
                visual.GetType().GetField("Hovered", publicFlags).SetValue(visual, true);
                scene.GetType().GetMethod("ApplyAppearance", flags).Invoke(scene, new[] { visual });
                nested.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == zero && actual.GetColor("_Color").b == 1,
                    "hover erased variation or tint");
                visual.GetType().GetField("Hovered", publicFlags).SetValue(visual, false);
                scene.GetType().GetMethod("ApplyAppearance", flags).Invoke(scene, new[] { visual });
                nested.GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == zero && actual.GetColor("_Color") == Color.clear,
                    "hover exit lost variation or retained tint");
                var changed = new BlueprintEditorPart("seeded", "woodwall", "Seeded", new Point3(0, 0, 0),
                    new Rotation3(0, 0, 0, 1), materialSeed: 1234);
                Require(scene.TrySync(new BlueprintEditorDocument(null, "Changed", "OTHER", new[] { changed }), out error), error);
                object replacement = ((System.Collections.IDictionary)scene.GetType().GetField("visuals", flags).GetValue(scene))["seeded"];
                var replacementRoot = (GameObject)replacement.GetType().GetField("Root", publicFlags).GetValue(replacement);
                replacementRoot.transform.GetChild(replacementRoot.transform.childCount - 1).GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
                Require(replacementRoot != root && !root.activeSelf && actual.GetVector("_Variation") == property.GetValue(1235),
                    "changed seed reused stale renderer state");
                scene.ShowDuplicatePreview(new[] { part });
                scene.ShowDuplicatePreview(new[] { changed });
                var previews = (System.Collections.IList)scene.GetType().GetField("duplicatePreviews", flags).GetValue(scene);
                object temporary = previews[0];
                var temporaryRoot = (GameObject)temporary.GetType().GetField("Root", publicFlags).GetValue(temporary);
                temporaryRoot.transform.GetChild(temporaryRoot.transform.childCount - 1).GetComponentInChildren<Renderer>().GetPropertyBlock(actual);
                Require(actual.GetVector("_Variation") == property.GetValue(1235), "temporary copy reused stale seed");
            }
            source.AddComponent<RandomMaterialValues>().m_vectorProperties.Add(property);
            Require(!BlueprintMaterialVariation.IsSupported(source), "overlapping native scopes were guessed");
            try { BlueprintMaterialVariation.ApplyPreview(source, a, 0); throw new InvalidOperationException("overlap accepted"); }
            catch (ArgumentException) { }
        }
        finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); Object.DestroyImmediate(source); }
    }
}
