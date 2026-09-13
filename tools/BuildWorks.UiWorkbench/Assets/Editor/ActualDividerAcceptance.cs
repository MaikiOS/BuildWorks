using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

// Read-only test of installed native assets. Only BuildWorks visual clones are
// created; native prefab components are never instantiated or executed.
public static class ActualDividerAcceptance
{
    private const string SoftRef = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\StreamingAssets\SoftRef";
    private const string Divider = "ashwood_decowall_divider";
    private const string Wall = "woodwall";
    private static readonly string[] PlacementProbes =
        { "woodwall", "wood_beam_26", "wood_roof", "piece_chair" };

    public static void Run(string outputDirectory)
    {
        var evidence = new List<string> { "Exact installed ashwood_decowall_divider acceptance", "UTC: " + DateTime.UtcNow.ToString("O") };
        var loaded = new List<AssetBundle>();
        GameObject probe = null;
        GameObject cameraObject = null;
        try
        {
            Dictionary<string, List<string>> dependencies = Dependencies();
            var bundles = new Dictionary<string, AssetBundle>();
            AssetBundle LoadBundle(string name)
            {
                if (bundles.TryGetValue(name, out AssetBundle existing)) return existing;
                if (dependencies.TryGetValue(name, out List<string> required))
                    foreach (string dependency in required) LoadBundle(dependency);
                string path = Path.Combine(SoftRef, "Bundles", name);
                AssetBundle bundle = AssetBundle.LoadFromFile(path);
                Require(bundle, "Could not load installed bundle: " + path);
                loaded.Add(bundle);
                bundles.Add(name, bundle);
                evidence.Add("Loaded read-only: " + path + "; bytes=" + new FileInfo(path).Length);
                return bundle;
            }

            GameObject LoadPrefab(string prefabName)
            {
                string expected = "Assets/GameElements/Pieces/" + prefabName + ".prefab";
                string[] lines = File.ReadAllLines(Path.Combine(SoftRef, "manifest_extended"));
                int index = Array.FindIndex(lines, line => line.Trim() == "path in bundle: " + expected);
                Require(index >= 2 && lines[index - 1].Trim().StartsWith("bundle: "), "Missing manifest mapping: " + expected);
                string bundleName = lines[index - 1].Trim().Substring("bundle: ".Length);
                AssetBundle bundle = LoadBundle(bundleName);
                string assetName = bundle.GetAllAssetNames().FirstOrDefault(name =>
                    string.Equals(name, expected, StringComparison.OrdinalIgnoreCase));
                Require(assetName != null, "Native bundle has no named asset: " + expected);
                evidence.Add(lines[index - 2].Trim() + "; " + assetName + "; bundle=" + bundleName);
                GameObject prefab = bundle.LoadAsset<GameObject>(assetName);
                Require(prefab, "Native prefab could not be deserialized: " + assetName);
                return prefab;
            }

            GameObject divider = LoadPrefab(Divider);
            GameObject wall = LoadPrefab(Wall);
            var placementPrefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
            foreach (string prefabName in PlacementProbes)
            {
                GameObject prefab = LoadPrefab(prefabName);
                placementPrefabs[prefabName] = prefab;
                RecordPlacementMetadata(prefab, evidence);
            }
            EditorInteractionAcceptance.Run(outputDirectory, divider, wall);
            probe = PlacementGhostPreviewView.CreateVisualClone(divider, "ExactDividerVisualProbe", 0);
            probe.transform.localScale = divider.transform.lossyScale;
            Renderer[] renderers = probe.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            Require(found, "Native divider has no active LOD0 renderers.");
            int unreadable = 0, meshCount = 0;
            foreach (MeshFilter filter in probe.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (!mesh) continue;
                ++meshCount;
                if (!mesh.isReadable) ++unreadable;
                evidence.Add("LOD0 mesh=" + mesh.name + "; vertices=" + mesh.vertexCount +
                    "; submeshes=" + mesh.subMeshCount + "; isReadable=" + mesh.isReadable);
                var hierarchy = new List<string>();
                for (Transform node = filter.transform; node && node != probe.transform; node = node.parent)
                    hierarchy.Insert(0, node.name + "[active=" + node.gameObject.activeSelf + "]");
                evidence.Add("Visual hierarchy: " + string.Join("/", hierarchy));
                Require(BlueprintEditorMeshData.TryRead(mesh, out Vector3[] vertices, out int[] triangles, out string readError),
                    "Native mesh readback failed: " + readError);
                evidence.Add("Actual mesh snapshot: vertices=" + vertices.Length + "; triangles=" + triangles.Length / 3);
            }
            Require(meshCount > 0, "Native divider visual clone has no meshes.");
            probe.SetActive(false);
            Vector3 dividerPosition = new Vector3(-0.5445061f, 0.004989624f, -0.1852417f);
            bounds.center += dividerPosition;
            cameraObject = new GameObject("ExactDividerCameraTemplate", typeof(Camera));
            Camera template = cameraObject.GetComponent<Camera>();
            template.enabled = false;
            RunPlacementAcceptance(template, placementPrefabs, evidence);
            var document = new BlueprintEditorDocument(null, "Группа 1 — installed asset proof", "Test", new[]
            {
                Part("divider-left", Divider, dividerPosition)
            });
            using (var scene = new BlueprintEditorScene(template, name => name == Divider ? divider : wall))
            {
                Require(scene.TrySync(document, out string error), "Exact divider scene: " + error);
                scene.Camera.orthographic = true;
                scene.Camera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x) * 1.3f;
                scene.SetViewport(new Rect(0, 0, 1024, 1024));
                scene.SetCameraPose(bounds.center - Vector3.forward * 6f, Quaternion.identity);
                const int steps = 64;
                var points = new Vector2[steps, steps];
                var surface = new bool[steps, steps];
                for (int y = 0; y < steps; ++y)
                    for (int x = 0; x < steps; ++x)
                    {
                        Vector3 world = new Vector3(
                            Mathf.Lerp(bounds.min.x, bounds.max.x, (x + 0.5f) / steps),
                            Mathf.Lerp(bounds.min.y, bounds.max.y, (y + 0.5f) / steps), bounds.center.z);
                        points[x, y] = scene.Camera.WorldToScreenPoint(world);
                        surface[x, y] = scene.TryPick(points[x, y], out string id) && id == "divider-left";
                    }
                var holes = new List<Vector2>();
                var solids = new List<Vector2>();
                for (int y = 1; y < steps - 1; ++y)
                    for (int x = 1; x < steps - 1; ++x)
                    {
                        if (surface[x, y]) { solids.Add(points[x, y]); continue; }
                        bool left = false, right = false, above = false, below = false;
                        for (int i = 0; i < x; ++i) left |= surface[i, y];
                        for (int i = x + 1; i < steps; ++i) right |= surface[i, y];
                        for (int i = 0; i < y; ++i) below |= surface[x, i];
                        for (int i = y + 1; i < steps; ++i) above |= surface[x, i];
                        if (left && right && above && below) holes.Add(points[x, y]);
                    }
                Require(solids.Count > 0, "Actual divider cannot be picked on empty background.");
                evidence.Add("PASS empty background: " + solids.Count + " solid probes; enclosed hole probes=" + holes.Count);
                document.AddPart(Part("wall", Wall, new Vector3(-0.044506073f, 0.004989624f, 0.014755249f)));
                document.AddPart(Part("divider-right", Divider, new Vector3(0.455493927f, 0.004989624f, -0.1852417f)));
                Require(scene.TrySync(document, out error), "User arrangement scene: " + error);
                int solidHits = 0, holeHits = 0;
                Vector2 solidPoint = default, holePoint = default;
                foreach (Vector2 point in solids)
                    if (scene.TryPick(point, out string id) && id == "divider-left")
                    { ++solidHits; solidPoint = point; }
                foreach (Vector2 point in holes)
                    if (scene.TryPick(point, out string id) && id == "wall")
                    { ++holeHits; holePoint = point; }
                Require(solidHits > 0, "Woodwall intercepts every real divider surface probe.");
                Require(holes.Count > 0 && holeHits > 0, "Could not verify a real enclosed divider hole selecting woodwall.");
                evidence.Add("PASS exact saved three-part positions: divider beats wall at " + solidHits +
                    " probes; enclosed holes select wall at " + holeHits + " probes.");
                evidence.Add("Example screen probes: divider=" + solidPoint + "; hole→wall=" + holePoint);
                // Frame the complete saved arrangement for the evidence image; the
                // fixed close view above is used only for the exact pixel probes.
                Require(scene.TryGetContentBounds(out Bounds allBounds), "Exact arrangement bounds unavailable.");
                scene.Camera.orthographicSize = Mathf.Max(allBounds.extents.x, allBounds.extents.y) * 1.15f;
                scene.SetCameraPose(allBounds.center - Vector3.forward * 6f, Quaternion.identity);
                document.SelectOnly("divider-left");
                Require(scene.TrySync(document, out error), "Selected exact divider: " + error);
                scene.SetHovered("divider-left", document);
                AssertHover(scene, "divider-left", false);
                AssertHover(scene, "divider-right", false);
                AssertHover(scene, "wall", false);
                Capture(scene.Camera, Path.Combine(outputDirectory, "actual-divider-selected.png"));
                document.ClearSelection();
                Require(scene.TrySync(document, out error), "Clear exact selection: " + error);
                scene.SetHovered("divider-left", document);
                AssertHover(scene, "divider-left", true);
                AssertHover(scene, "divider-right", false);
                AssertHover(scene, "wall", false);
                Capture(scene.Camera, Path.Combine(outputDirectory, "actual-divider-hover.png"));
                scene.SetHovered(null, document);
                AssertHover(scene, "divider-left", false);
                document.SelectOnly("divider-left");
                document.ToggleSelection("divider-right");
                Require(document.CreateGroup("dividers", "Dividers"), "Could not group exact dividers.");
                document.ToggleSelection("wall");
                Require(document.CreateGroup("assembly", "Assembly"), "Could not nest exact divider group.");
                document.ClearSelection();
                Require(scene.TrySync(document, out error), "Exact nested groups: " + error);
                scene.SetHovered("assembly", document);
                foreach (string id in new[] { "divider-left", "divider-right", "wall" })
                    AssertHover(scene, id, true);
                document.SelectOnly("assembly");
                Require(scene.TrySync(document, out error), "Select exact nested group: " + error);
                foreach (string id in new[] { "divider-left", "divider-right", "wall" })
                    AssertHover(scene, id, false);
                evidence.Add("PASS exact mesh hover tint/emission; unhover and selected+hover restore; nested group hover/selection affects all descendants.");
                evidence.Add("Selected (original material) and hover (native blue tint) images captured with real Scene and real native mesh.");
            }
            evidence.Add("PASS exact installed asset; LOD0 meshes=" + meshCount + "; unreadable=" + unreadable);
            Debug.Log("BUILDWORKS_ACTUAL_DIVIDER_OK");
        }
        catch (Exception exception)
        {
            evidence.Add("BLOCKED/FAILED exact asset: " + exception);
            Debug.LogWarning("BUILDWORKS_ACTUAL_DIVIDER_BLOCKED " + exception.Message);
        }
        finally
        {
            if (probe) Object.Destroy(probe);
            if (cameraObject) Object.Destroy(cameraObject);
            foreach (AssetBundle bundle in loaded) if (bundle) bundle.Unload(true);
            File.WriteAllLines(Path.Combine(outputDirectory, "actual-divider-check.txt"), evidence);
        }
    }

    private static Dictionary<string, List<string>> Dependencies()
    {
        var result = new Dictionary<string, List<string>>();
        string current = null;
        foreach (string line in File.ReadLines(Path.Combine(SoftRef, "manifest_extended")))
        {
            if (line == "asset locations:") break;
            if (line.StartsWith("- bundle: "))
            { current = line.Substring("- bundle: ".Length).Trim(); result[current] = new List<string>(); }
            else if (current != null && line.StartsWith("  - "))
                result[current].Add(line.Substring(4).Trim());
        }
        return result;
    }

    private static BlueprintEditorPart Part(string id, string name, Vector3 position) =>
        new BlueprintEditorPart(id, name, name, new Point3(position.x, position.y, position.z), new Rotation3(0, 0, 0, 1));

    private static void RecordPlacementMetadata(GameObject prefab, ICollection<string> evidence)
    {
        GameObject clone = PlacementGhostPreviewView.CreateVisualClone(
            prefab, "PlacementMetadata_" + prefab.name, 0);
        try
        {
            clone.transform.localScale = prefab.transform.lossyScale;
            Renderer[] renderers = clone.GetComponentsInChildren<Renderer>(true);
            bool found = false;
            Bounds bounds = default;
            foreach (Renderer renderer in renderers)
            {
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (found) bounds.Encapsulate(renderer.bounds);
                else { bounds = renderer.bounds; found = true; }
            }
            Require(found, "Placement probe has no active renderer: " + prefab.name);
            var snaps = new List<string>();
            foreach (Transform child in prefab.transform)
                if (child.CompareTag("snappoint"))
                    snaps.Add(child.name + "=" + child.localPosition.ToString("F4"));
            evidence.Add("PLACEMENT " + prefab.name +
                "; root=" + prefab.transform.position.ToString("F4") +
                "; boundsCenter=" + bounds.center.ToString("F4") +
                "; boundsMin=" + bounds.min.ToString("F4") +
                "; boundsMax=" + bounds.max.ToString("F4") +
                "; native=" + snaps.Count + " [" + string.Join(", ", snaps) + "]");
        }
        finally
        {
            if (clone) Object.DestroyImmediate(clone);
        }
    }

    private static void RunPlacementAcceptance(
        Camera template,
        IReadOnlyDictionary<string, GameObject> prefabs,
        ICollection<string> evidence)
    {
        using (var scene = new BlueprintEditorScene(template,
            name => prefabs.TryGetValue(name, out GameObject prefab) ? prefab : null))
        {
            foreach (string prefabName in PlacementProbes)
            {
                Vector3 surface = new Vector3(3f, 2f, -1f);
                Quaternion rotation = Quaternion.Euler(0f, 37f, 0f);
                Require(scene.ShowPlacementPreview(
                    prefabName, surface, Vector3.up, rotation, false,
                    out _, out string error), "Exact placement preview: " + error);
                object visual = PlacementVisual(scene);
                GameObject visualRoot = VisualField<GameObject>(visual, "Root");
                float rendererMinimumY = float.PositiveInfinity;
                foreach (Renderer renderer in VisualField<Renderer[]>(visual, "Renderers"))
                    if (renderer && renderer.enabled && renderer.gameObject.activeInHierarchy)
                        rendererMinimumY = Mathf.Min(rendererMinimumY, renderer.bounds.min.y);
                float meshMinimumY = float.PositiveInfinity;
                foreach (object pickSurface in VisualField<IList>(visual, "PickSurfaces"))
                {
                    Renderer renderer = VisualField<Renderer>(pickSurface, "Renderer");
                    if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                    foreach (Vector3 vertex in VisualField<Vector3[]>(pickSurface, "Vertices"))
                        meshMinimumY = Mathf.Min(meshMinimumY,
                            visualRoot.transform.TransformPoint(vertex).y);
                }
                evidence.Add("CONTACT " + prefabName + "; root=" +
                    visualRoot.transform.position.ToString("F4") +
                    "; meshMinY=" + meshMinimumY.ToString("F6") +
                    "; rendererMinY=" + rendererMinimumY.ToString("F6") +
                    "; targetY=" + surface.y.ToString("F6"));
                Require(Mathf.Abs(meshMinimumY - surface.y) < .001f,
                    prefabName + " does not rest on the aimed surface: " +
                    meshMinimumY.ToString("F6") + " vs " + surface.y.ToString("F6"));
                evidence.Add("PASS native-like support contact: " + prefabName +
                    "; minY=" + meshMinimumY.ToString("F4"));
            }

            Require(scene.PlacementSourceSnapPointCount >= 7,
                "Furniture without native snap points has no precise editor points");
            Require(!string.IsNullOrEmpty(scene.PlacementSnapPointLabel(0)),
                "Furniture fallback point has no Q/E guidance");

            Vector3 selectedPoint = new Vector3(-2f, -1f, 3f);
            Require(scene.ShowPlacementPreview(
                "wood_beam_26", selectedPoint, Vector3.up, Quaternion.identity, true,
                out _, out string beamError, 0), "Beam manual placement: " + beamError);
            object beamVisual = PlacementVisual(scene);
            Transform beamRoot = VisualField<GameObject>(beamVisual, "Root").transform;
            List<Vector3> beamPoints = VisualField<List<Vector3>>(beamVisual, "PlacementLocal");
            Require(beamPoints.Count >= 2 &&
                Vector3.Distance(beamRoot.TransformPoint(beamPoints[0]), selectedPoint) < .001f,
                "Q/E does not place the selected beam endpoint under the cursor");
            Require(scene.PlacementSnapPointLabel(0).Contains(
                BuildWorksLocalization.Text("blueprint.snap.bottom")),
                "Beam Q/E guidance does not identify its native lower endpoint");
            evidence.Add("PASS Q/E endpoint alignment and furniture fallback placement points.");
        }
    }

    private static object PlacementVisual(BlueprintEditorScene scene)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance;
        return typeof(BlueprintEditorScene).GetField("placementPreview", flags).GetValue(scene);
    }

    private static T VisualField<T>(object visual, string name)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        return (T)visual.GetType().GetField(name, flags).GetValue(visual);
    }

    private static void AssertHover(BlueprintEditorScene scene, string id, bool expected)
    {
        const BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
        var visuals = (IDictionary)typeof(BlueprintEditorScene).GetField("visuals", flags).GetValue(scene);
        object visual = visuals[id];
        var renderers = (Renderer[])visual.GetType().GetField("Renderers", flags).GetValue(visual);
        Require(renderers.Length > 0, "No exact renderers: " + id);
        var properties = new MaterialPropertyBlock();
        foreach (Renderer renderer in renderers)
        {
            renderer.GetPropertyBlock(properties);
            if (expected)
            {
                Color tint = new Color(0.6f, 0.8f, 1f, 1f);
                Require(properties.GetColor("_Color") == tint &&
                    properties.GetColor("_EmissionColor") == tint * 0.4f,
                    "Exact native hover tint/emission missing: " + id + "/" + renderer.name);
            }
            else Require(properties.isEmpty, "Selected/unhovered exact mesh retains tint: " + id);
        }
    }

    private static void Capture(Camera camera, string path)
    {
        var target = new RenderTexture(1024, 1024, 24);
        var texture = new Texture2D(1024, 1024, TextureFormat.RGB24, false);
        RenderTexture previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            texture.ReadPixels(new Rect(0, 0, 1024, 1024), 0, 0);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = null;
            RenderTexture.active = previous;
            Object.Destroy(texture);
            Object.Destroy(target);
        }
    }

    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
