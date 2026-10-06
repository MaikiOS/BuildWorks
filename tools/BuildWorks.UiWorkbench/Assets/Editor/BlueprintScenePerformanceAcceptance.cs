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
        public int rootGeometryEntries;
        public double openMs, resyncMs, pickMs;
        public long liveManagedDeltaBytes;
        public long pickGeometryBytes, uniquePickGeometryBytes;
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
                var uniqueGeometry = new HashSet<Array>();
                foreach (DictionaryEntry entry in visuals)
                    foreach (object surface in (IEnumerable)entry.Value.GetType().GetField("PickSurfaces", Private).GetValue(entry.Value))
                        foreach (var field in new[] { ("Vertices", 12), ("Triangles", 4), ("Edges", 48) })
                        {
                            var array = (Array)surface.GetType().GetField(field.Item1, Private).GetValue(surface);
                            long bytes = (long)array.Length * field.Item2;
                            sample.pickGeometryBytes += bytes;
                            if (uniqueGeometry.Add(array)) sample.uniquePickGeometryBytes += bytes;
                        }
                sample.rootGeometryEntries = ((IDictionary)typeof(BlueprintEditorScene).GetField("rootMeshGeometry", Private).GetValue(scene)).Count;
                Require(sample.rootGeometryEntries <= sample.renderers / count &&
                    sample.uniquePickGeometryBytes <= sample.pickGeometryBytes / count,
                    "Repeated wall geometry still grows with part count");
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
        CheckNestedGeometry(template);
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

    private static void CheckNestedGeometry(Camera template)
    {
        GameObject source = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = Object.Instantiate(source.GetComponent<MeshFilter>().sharedMesh);
        mesh.hideFlags = HideFlags.None;
        Vector3[] originalVertices = mesh.vertices;
        int[] triangles = mesh.triangles;
        mesh.UploadMeshData(true);
        Object.DestroyImmediate(source.GetComponent<MeshRenderer>());
        Object.DestroyImmediate(source.GetComponent<MeshFilter>());
        source.transform.SetPositionAndRotation(new Vector3(20, 3, -4), Quaternion.Euler(14, 27, 8));
        source.transform.localScale = new Vector3(1.4f, .8f, 1.2f);
        var nested = new GameObject("Nested"); nested.transform.SetParent(source.transform, false);
        nested.transform.localPosition = new Vector3(.3f, .7f, -.2f);
        nested.transform.localRotation = Quaternion.Euler(17, 31, 12);
        nested.transform.localScale = new Vector3(.7f, 1.6f, 1.1f);
        for (int i = 0; i < 2; ++i)
        {
            var child = new GameObject("Surface" + i); child.transform.SetParent(nested.transform, false);
            child.transform.localPosition = new Vector3(i * 4, .2f, -.4f);
            child.transform.localRotation = Quaternion.Euler(9, 11 + i * 30, 23);
            child.transform.localScale = new Vector3(i == 0 ? -.8f : .9f, 1.3f, .6f);
            child.AddComponent<MeshFilter>().sharedMesh = mesh;
            child.AddComponent<MeshRenderer>().sharedMaterial = Resources.Load<GameObject>("VanillaPreview/woodwall")
                .GetComponentInChildren<Renderer>(true).sharedMaterial;
        }
        source.SetActive(false);
        Quaternion turn = Quaternion.Euler(13, 25, 7);
        var document = new BlueprintEditorDocument(null, "Nested geometry", "OTHER", new[] {
            new BlueprintEditorPart("a", "fixture", "A", new Point3(0, 4, 0),
                new Rotation3(turn.x, turn.y, turn.z, turn.w), scale: new Point3(1.2, .6, 1.7)),
            new BlueprintEditorPart("b", "fixture", "B", new Point3(20, 4, 0), new Rotation3(0, 0, 0, 1)) });
        try
        {
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                Require(scene.TrySync(document, out _), "Nested sync failed");
                scene.SetViewport(new Rect(0, 0, 1920, 1080));
                IDictionary visuals = (IDictionary)typeof(BlueprintEditorScene).GetField("visuals", Private).GetValue(scene);
                object firstNode = visuals["a"];
                IList surfaces = (IList)firstNode.GetType().GetField("PickSurfaces", Private).GetValue(firstNode);
                IList secondSurfaces = (IList)visuals["b"].GetType().GetField("PickSurfaces", Private).GetValue(visuals["b"]);
                IDictionary rawCache = (IDictionary)typeof(BlueprintEditorScene).GetField("meshSnapshots", Private).GetValue(scene);
                IDictionary rootCache = (IDictionary)typeof(BlueprintEditorScene).GetField("rootMeshGeometry", Private).GetValue(scene);
                Require(rawCache.Count == 1 && rootCache.Count == 2, "GPU mesh must be read once but retain both child transforms");
                for (int i = 0; i < surfaces.Count; ++i)
                    foreach (string field in new[] { "Vertices", "Edges", "Triangles" })
                        Require(ReferenceEquals(surfaces[i].GetType().GetField(field, Private).GetValue(surfaces[i]),
                            secondSurfaces[i].GetType().GetField(field, Private).GetValue(secondSurfaces[i])), "Corresponding nested geometry not shared");
                Require(!ReferenceEquals(surfaces[0].GetType().GetField("Vertices", Private).GetValue(surfaces[0]),
                    surfaces[1].GetType().GetField("Vertices", Private).GetValue(surfaces[1])), "Different child matrices aliased");
                foreach (bool preview in new[] { false, true, false })
                {
                    if (preview) scene.PreviewTransform(document, new[] { "a" }, new Vector3(.4f, .8f, -.7f),
                        Quaternion.Euler(5, 19, 3), new Vector3(.2f, 1, -.3f), 1.4f);
                    else Require(scene.TrySync(document, out _), "Nested preview reset failed");
                    foreach (object surface in surfaces)
                    {
                        Renderer renderer = (Renderer)surface.GetType().GetField("Renderer", Private).GetValue(surface);
                        Vector3[] vertices = (Vector3[])surface.GetType().GetField("Vertices", Private).GetValue(surface);
                        GameObject root = VisualRoot(firstNode);
                        Bounds expected = new Bounds(renderer.transform.TransformPoint(originalVertices[0]), Vector3.zero);
                        for (int index = 0; index < vertices.Length; ++index)
                        {
                            Vector3 world = renderer.transform.TransformPoint(originalVertices[index]);
                            Require(Vector3.Distance(root.transform.TransformPoint(vertices[index]), world) < .00005f,
                                "Nested cached vertex differs from rendered mesh");
                            expected.Encapsulate(world);
                        }
                        foreach (Edge3 edge in (Edge3[])surface.GetType().GetField("Edges", Private).GetValue(surface))
                            foreach (Point3 endpoint in new[] { edge.Start, edge.End })
                            {
                                Vector3 world = root.transform.TransformPoint(new Vector3((float)endpoint.X, (float)endpoint.Y, (float)endpoint.Z));
                                Require(Array.Exists(originalVertices, v => Vector3.Distance(renderer.transform.TransformPoint(v), world) < .00005f),
                                    "Nested feature edge is not an actual vertex");
                            }
                        Require(Vector3.Distance(expected.center, renderer.bounds.center) < .0001f &&
                            Vector3.Distance(expected.size, renderer.bounds.size) < .0001f, "Nested bounds mismatch");
                        Vector3 a = renderer.transform.TransformPoint(originalVertices[triangles[0]]);
                        Vector3 b = renderer.transform.TransformPoint(originalVertices[triangles[1]]);
                        Vector3 c = renderer.transform.TransformPoint(originalVertices[triangles[2]]);
                        Vector3 center = (a + b + c) / 3;
                        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                        if (Vector3.Dot(normal, center - renderer.transform.TransformPoint(mesh.bounds.center)) < 0)
                            normal = -normal;
                        scene.SetCameraPose(center + normal * 3, Quaternion.LookRotation(-normal));
                        Vector2 cursor = scene.Camera.WorldToScreenPoint(center);
                        Require(scene.TryPickPoint(cursor, out string picked, out Vector3 hit) && picked == "a" &&
                            Vector3.Distance(hit, center) < .0001f, "Nested mesh pick/preview mismatch: " +
                            renderer.name + " preview=" + preview + " picked=" + picked + " hit=" + hit + " expected=" + center);
                        Require(scene.TryPlacementSurface(cursor, out Vector3 contact, out Vector3 contactNormal) &&
                            Vector3.Distance(contact, center) < .0001f && Vector3.Dot(normal, contactNormal) > .999f,
                            "Nested mesh contact/normal mismatch");
                    }
                    Require(rootCache.Count == 2, "Part preview changed the root-space cache");
                }
                scene.ExperimentalGeometryPoints = true;
                Require(scene.TryGetGizmoAnchors(new[] { "a" }, out Vector3[] anchors, out _) && scene.GeometryAnchorCount > 0,
                    "Nested fixture lost mesh corner grips");
                for (int index = scene.GeometryAnchorStart; index < scene.GeometryAnchorStart + scene.GeometryAnchorCount; ++index)
                    Require(Array.Exists(VisualRoot(firstNode).GetComponentsInChildren<MeshFilter>(true),
                        filter => Array.Exists(originalVertices, v => Vector3.Distance(filter.transform.TransformPoint(v), anchors[index]) < .0001f)),
                        "Nested geometry grip is not a rendered vertex");
                document.SelectOnly("a");
                Require(document.DeleteSelection(false) && scene.TrySync(document, out _) && document.Undo() && scene.TrySync(document, out _),
                    "Nested delete/Undo failed");
                IList restored = (IList)visuals["a"].GetType().GetField("PickSurfaces", Private).GetValue(visuals["a"]);
                Require(ReferenceEquals(restored[0].GetType().GetField("Vertices", Private).GetValue(restored[0]),
                    surfaces[0].GetType().GetField("Vertices", Private).GetValue(surfaces[0])), "Undo did not reuse nested geometry");
                for (int index = 0; index < 3; ++index)
                {
                    nested.transform.localPosition += Vector3.right;
                    Require(scene.ShowPlacementPreview("preview" + index, Vector3.zero, Quaternion.identity, false, out _, out _),
                        "Nested placement preview failed");
                    Require(rootCache.Count == 2, "Catalog preview retained newly browsed root geometry");
                    object node = typeof(BlueprintEditorScene).GetField("placementPreview", Private).GetValue(scene);
                    foreach (object surface in (IEnumerable)node.GetType().GetField("PickSurfaces", Private).GetValue(node))
                    {
                        var renderer = (Renderer)surface.GetType().GetField("Renderer", Private).GetValue(surface);
                        var vertices = (Vector3[])surface.GetType().GetField("Vertices", Private).GetValue(surface);
                        for (int vertex = 0; vertex < vertices.Length; ++vertex)
                            Require(Vector3.Distance(VisualRoot(node).transform.TransformPoint(vertices[vertex]),
                                renderer.transform.TransformPoint(originalVertices[vertex])) < .00005f,
                                "Uncached preview geometry differs from rendered mesh");
                    }
                }
                scene.Dispose();
                Require(rootCache.Count == 0 && rawCache.Count == 0, "Scene disposal retained geometry caches");
            }
            mesh.hideFlags = HideFlags.HideAndDontSave;
            using (var scene = new BlueprintEditorScene(template, _ => source))
            {
                Require(scene.TrySync(document, out _), "Transient mesh sync failed");
                Require(((IDictionary)typeof(BlueprintEditorScene).GetField("rootMeshGeometry", Private).GetValue(scene)).Count == 0 &&
                    ((IDictionary)typeof(BlueprintEditorScene).GetField("meshSnapshots", Private).GetValue(scene)).Count == 0,
                    "Transient meshes retained in shared geometry caches");
            }
        }
        finally { Object.Destroy(source); Object.Destroy(mesh); }
    }
}
