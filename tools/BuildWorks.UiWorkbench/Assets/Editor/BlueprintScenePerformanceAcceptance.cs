using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Bounded real Unity scene measurements, not an FPS or large-world capacity claim.
public static class BlueprintScenePerformanceAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    [Serializable] private sealed class Sample
    {
        public int parts, renderers, uniqueMaterials;
        public double openMs, resyncMs, pickMs;
        public long liveManagedDeltaBytes;
    }
    [Serializable] private sealed class Report
    {
        public string completedAtUtc = DateTime.UtcNow.ToString("o");
        public string unityVersion = Application.unityVersion;
        public bool gameHostTested = false;
        public Sample[] samples;
    }

    public static IEnumerable Run(Camera template, string output)
    {
        GameObject source = Resources.Load<GameObject>("VanillaPreview/woodwall");
        Require(source, "Performance fixture woodwall missing");
        var originals = new Dictionary<Material, string>();
        foreach (Renderer renderer in source.GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                if (material && !originals.ContainsKey(material))
                {
                    // Imported MaterialToggle values need their Editor keyword initialization
                    // before a full serialized invariant; do not ignore later keyword changes.
                    MaterialEditor.ApplyMaterialPropertyDrawers(material);
                    originals.Add(material, EditorJsonUtility.ToJson(material));
                }
        var samples = new List<Sample>();
        foreach (int count in new[] { 128, 512 })
        {
            var parts = new List<BlueprintEditorPart>();
            for (int index = 0; index < count; ++index)
                parts.Add(new BlueprintEditorPart("p" + index, "woodwall", "Wall",
                    new Point3(index % 16 * 2.3, 0, index / 16 * 2.3), new Rotation3(0, 0, 0, 1)));
            var document = new BlueprintEditorDocument(null, "Scene measurement", "OTHER", parts);
            Material[] materials;
            GameObject sceneRoot;
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                // Scene setup is not part of the measured sync; warm shaders/mesh read on one piece.
                Require(scene.TrySync(new BlueprintEditorDocument(null, "Warmup", "OTHER", new[] { parts[0] }), out _), "Scene warmup failed");
                Require(scene.TrySync(new BlueprintEditorDocument(null, "Empty", "OTHER"), out _), "Warmup clear failed");
                yield return null;
                GC.Collect();
                long before = GC.GetTotalMemory(false);
                var watch = Stopwatch.StartNew();
                Require(scene.TrySync(document, out string warning), "Scene sync failed: " + warning);
                watch.Stop();
                var sample = new Sample { parts = count, openMs = watch.Elapsed.TotalMilliseconds,
                    liveManagedDeltaBytes = GC.GetTotalMemory(false) - before };
                var unique = new HashSet<Material>();
                IDictionary visuals = (IDictionary)typeof(BlueprintEditorScene).GetField("visuals", Private).GetValue(scene);
                Require(visuals.Count == count, "Scene dropped document parts");
                foreach (DictionaryEntry entry in visuals)
                {
                    GameObject visual = VisualRoot(entry.Value);
                    foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        ++sample.renderers;
                        foreach (Material material in renderer.sharedMaterials) if (material) unique.Add(material);
                    }
                }
                materials = new Material[unique.Count]; unique.CopyTo(materials);
                sample.uniqueMaterials = unique.Count;
                Require(unique.Count <= originals.Count && unique.Count < count,
                    "Document material clones still grow with part count");
                foreach (Material material in unique) Require(!originals.ContainsKey(material), "Scene uses mutable prefab material directly");
                watch.Restart();
                for (int repeat = 0; repeat < 10; ++repeat) Require(scene.TrySync(document, out _), "Resync failed");
                watch.Stop(); sample.resyncMs = watch.Elapsed.TotalMilliseconds / 10;
                Require(scene.TryGetBounds(new[] { "p0" }, out Bounds bounds), "First piece bounds missing");
                scene.SetCameraPose(bounds.center + Vector3.back * 6, Quaternion.identity);
                Vector2 mouse = scene.Camera.WorldToScreenPoint(bounds.center);
                Require(scene.TryPick(mouse, out string picked) && picked == "p0", "First wall mesh cannot be picked");
                watch.Restart();
                for (int repeat = 0; repeat < 20; ++repeat) scene.TryPick(mouse, out _);
                watch.Stop(); sample.pickMs = watch.Elapsed.TotalMilliseconds / 20;
                CheckMaterialIsolation(scene, document, visuals, materials);
                foreach (var original in originals)
                {
                    string after = EditorJsonUtility.ToJson(original.Key);
                    if (after != original.Value)
                    {
                        File.WriteAllText(Path.Combine(output, "material-before.json"), original.Value);
                        File.WriteAllText(Path.Combine(output, "material-after.json"), after);
                        throw new InvalidOperationException("Prefab material was modified: " + original.Key.name);
                    }
                }
                sceneRoot = (GameObject)typeof(BlueprintEditorScene).GetField("root", Private).GetValue(scene);
                samples.Add(sample);
                yield return null;
            }
            yield return null;
            Require(!sceneRoot, "Scene root survived disposal");
            foreach (Material material in materials) Require(!material, "Owned material survived scene disposal");
        }
        File.WriteAllText(Path.Combine(output, "scene-performance.json"),
            JsonUtility.ToJson(new Report { samples = samples.ToArray() }, true));
    }

    private static GameObject VisualRoot(object node) =>
        (GameObject)node.GetType().GetField("Root", Private).GetValue(node);

    private static void CheckMaterialIsolation(BlueprintEditorScene scene, BlueprintEditorDocument document,
        IDictionary visuals, Material[] materials)
    {
        Renderer first = VisualRoot(visuals["p0"]).GetComponentInChildren<Renderer>();
        Renderer second = VisualRoot(visuals["p1"]).GetComponentInChildren<Renderer>();
        var properties = new MaterialPropertyBlock();
        scene.SetHovered("p0", document);
        first.GetPropertyBlock(properties); Require(!properties.isEmpty, "Hover does not affect aimed part");
        second.GetPropertyBlock(properties); Require(properties.isEmpty, "Hover spread to adjacent part");
        var before = new Dictionary<Material, string>();
        foreach (Material material in materials) before.Add(material, EditorJsonUtility.ToJson(material));
        scene.ShowContourPreview(new[] { document.Parts[0] });
        IList previews = (IList)typeof(BlueprintEditorScene).GetField("contourPreviews", Private).GetValue(scene);
        foreach (Renderer renderer in VisualRoot(previews[0]).GetComponentsInChildren<Renderer>(true))
            foreach (Material material in renderer.sharedMaterials)
                Require(!before.ContainsKey(material), "Tinted preview shares document material");
        foreach (var entry in before) Require(EditorJsonUtility.ToJson(entry.Key) == entry.Value, "Preview tint modified document material");
        scene.ClearContourPreview();
        document.SelectOnly("p0");
        Require(scene.TrySync(document, out _), "Selection sync failed");
        first.GetPropertyBlock(properties); Require(properties.isEmpty, "Selected part stayed tinted");
        Require(document.DeleteSelection(false) && scene.TrySync(document, out _), "Delete sync failed");
        foreach (Material material in materials) Require(material, "Deleting one part destroyed a shared material");
        Require(document.Undo() && scene.TrySync(document, out _), "Deletion Undo failed");
        Require(ReferenceEquals(VisualRoot(visuals["p0"]).GetComponentInChildren<Renderer>().sharedMaterial, first.sharedMaterial),
            "Undo did not reuse scene material");
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
