using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            DiagnoseLantern(LoadPrefab("piece_dvergr_lantern"), evidence);
            DiagnoseLantern(LoadPrefab("piece_Lavalantern"), evidence);
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
            RunRealExternalBlueprints(template, LoadPrefab, outputDirectory, evidence);
            RunPlacementAcceptance(template, placementPrefabs, evidence);
            DiagnoseChairCorners(template, LoadPrefab("piece_chair02"), outputDirectory, evidence);
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
                evidence.Add("PASS exact mesh reversible cyan hover; selected mesh retains original materials; nested descendants stay untinted.");
                evidence.Add("Untinted selection and hover blue images captured with real Scene and real native mesh.");
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

    private static void DiagnoseLantern(GameObject prefab, ICollection<string> evidence)
    {
        GameObject clone = PlacementGhostPreviewView.CreateVisualClone(prefab, "LanternScaleProbe", 0);
        try
        {
            clone.transform.localScale = prefab.transform.localScale;
            evidence.Add("LANTERN " + prefab.name + " root local=" + prefab.transform.localScale.ToString("R") +
                "; lossy=" + prefab.transform.lossyScale.ToString("R"));
            foreach (SkinnedMeshRenderer skin in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                evidence.Add("SKIN " + prefab.name + "/" + skin.name + "; active=" + skin.gameObject.activeInHierarchy +
                    "; localScale=" + skin.transform.localScale.ToString("R") + "; lossy=" + skin.transform.lossyScale.ToString("R") +
                    "; localBounds=" + skin.localBounds + "; meshBounds=" + skin.sharedMesh.bounds + "; bones=" + skin.bones.Length);
                foreach (Transform bone in skin.bones.Take(3))
                    evidence.Add("SKIN bone=" + (bone ? bone.name + "; matrix=" +
                        (skin.transform.worldToLocalMatrix * bone.localToWorldMatrix).ToString("R") : "null"));
                foreach (bool useScale in new[] { false, true })
                {
                    var baked = new Mesh();
                    try { skin.BakeMesh(baked, useScale); baked.RecalculateBounds(); evidence.Add("SKIN baked useScale=" + useScale + "; bounds=" + baked.bounds); }
                    finally { Object.DestroyImmediate(baked); }
                }
            }
            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh)
                    evidence.Add("LANTERN " + prefab.name + " native " + filter.name + "; active=" + filter.gameObject.activeInHierarchy +
                        "; matrix=" + (prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix).ToString("R") +
                        "; bounds=" + filter.sharedMesh.bounds);
            foreach (Renderer renderer in clone.GetComponentsInChildren<Renderer>(true))
                evidence.Add("LANTERN " + prefab.name + " clone " + renderer.name + "; active=" + renderer.gameObject.activeInHierarchy +
                    "; enabled=" + renderer.enabled + "; bounds=" + renderer.bounds);
            if (prefab.name == "piece_Lavalantern")
            {
                Renderer shell = clone.GetComponentsInChildren<Renderer>(true).Single(renderer => renderer.name == "Sphere.004");
                Renderer reference = clone.GetComponentsInChildren<Renderer>(true).Single(renderer => renderer.name == "high");
                Require((shell.bounds.size - reference.bounds.size).magnitude < .05f &&
                    (shell.bounds.center - reference.bounds.center).magnitude < .05f,
                    "Lava Lantern skinned clone does not match the independent native static shell dimensions.");
                evidence.Add("PASS piece_Lavalantern animated shell matches native static high shell within0.05m; root scale unchanged.");
            }
        }
        finally { Object.DestroyImmediate(clone); }
    }

    private static void RunRealExternalBlueprints(Camera template, Func<string, GameObject> load,
        string output, ICollection<string> evidence)
    {
        string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
        string corpus = Path.Combine(repository, "artifacts", "external-blueprints", "BiomeBlueprints-1.0.3");
        string archivePath = Path.Combine(corpus, "package.zip");
        string Hash(byte[] bytes)
        {
            using (var hash = System.Security.Cryptography.SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
        }
        const string archiveHash = "36C5A04DF80294C6B49B21C916D808FCE60698B17479BFAB7C95F850BE12C707";
        Require(Hash(File.ReadAllBytes(archivePath)) == archiveHash, "External corpus differs from reviewed archive");
        using (var archive = ZipFile.OpenRead(archivePath))
        foreach (var fixture in new[] {
            ("hs_ashlands_madzobuild_coffeetable.blueprint", "0DE7C847036390D74CEA27242FB47B9375668B1775BA9A7228EADDF7ECC4DD53", 4),
            ("hs_blackforest_hut.blueprint", "492E78E16FEEFCCF3B58C1503342732AEFC68A9F9B891401EB3B9BC06E588F49", 28) })
        {
            byte[] bytes;
            using (var input = archive.GetEntry("blueprints/" + fixture.Item1).Open())
            using (var buffer = new MemoryStream()) { input.CopyTo(buffer); bytes = buffer.ToArray(); }
            Require(Hash(bytes) == fixture.Item2, "External fixture bytes changed: " + fixture.Item1);
            string original = new System.Text.UTF8Encoding(false, true).GetString(bytes);
            string fixtureDirectory = Path.Combine(corpus, "smoke");
            Directory.CreateDirectory(fixtureDirectory);
            string fixturePath = Path.Combine(fixtureDirectory, fixture.Item1);
            if (!File.Exists(fixturePath)) File.WriteAllBytes(fixturePath, bytes);
            Require(File.ReadAllBytes(fixturePath).SequenceEqual(bytes), "Existing external fixture changed; refusing overwrite");
            var raw = ExternalBlueprintLibrary.Parse(original, fixturePath);
            Require(raw.importError == null && raw.parts.Count == fixture.Item3 &&
                !raw.sourceDocuments[0].ToGeometry().HasOpaqueSections, "External fixture cannot enter known pipeline");
            if (fixture.Item3 == 4)
            {
                var floors = raw.parts.Where(part => part.prefabName == "ashwood_floor_1x1").ToArray();
                Require(floors.Length == 2 && floors.All(part => Quaternion.Angle(part.rotation.ToQuaternion(),
                    Quaternion.Euler(0, -33.75f, 0)) < .001f) &&
                    (floors[1].position.ToVector3() - floors[0].position.ToVector3() -
                        new Vector3(.5555696f, 0, -.83145138f)).sqrMagnitude < .00000001f,
                    "Actual CoffeeTable source yaw/axes changed or were silently straightened");
                evidence.Add("PASS original CoffeeTable yaw-33.75 degrees and floor delta(.5555696,0,-.83145138) retained; no automatic axis straightening.");
            }
            var prefabs = raw.parts.Select(part => part.prefabName).Distinct().ToDictionary(name => name, load);
            string runDirectory = Path.Combine(output, "external-real-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(runDirectory);
            string storePath = Path.Combine(runDirectory, "library.json");
            var store = new CompositeBlueprintStore(storePath);
            store.ConfigureImportScale(name => new CompositeBlueprintStore.VectorData(prefabs[name].transform.localScale),
                () => prefabs, name => name.GetStableHashCode(), () => 0);
            var review = CompositeBlueprintStore.ReviewImportFile(fixturePath, name => name.GetStableHashCode(), 0);
            Require(store.TryImportReviewed(review, false, false, name => name.GetStableHashCode(), out string importError), importError);
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (store.CatalogLoading && wait.ElapsedMilliseconds < 10000)
            { store.CatalogAll(); System.Threading.Thread.Sleep(10); }
            Require(!store.CatalogLoading && store.ImportError == null, "Real reviewed import failed: " + store.ImportError);
            var imported = store.CatalogAll().Single();
            Require(imported.importError == null && !imported.importAbsoluteScale &&
                !CompositeBlueprintStore.HasForeignData(imported), "Actual reviewed import normalization failed");
            using (var editor = new BlueprintEditorController(store, template, null,
                name => prefabs.TryGetValue(name, out var prefab) ? prefab : null, name => name,
                () => Array.Empty<BlueprintEditorCatalogItem>(), message => { throw new InvalidOperationException(message); }))
            {
                Require(editor.OpenExisting(imported, out string error), "Real external editor open: " + error);
                editor.Document.SelectOnly(editor.Document.Parts[0].StableId);
                Require(editor.ApplyTransformDelta(Vector3.right * .25f, Quaternion.identity, Vector3.zero), "Real external edit failed");
                Quaternion supportRotation = Quaternion.Euler(0, 37, 0);
                Require(editor.Document.AddSupport(new BlueprintEditorSupport("smoke-support", "Опора испытания",
                    new Point3(1.25, -.25, .5), new Rotation3(supportRotation.x, supportRotation.y, supportRotation.z, supportRotation.w))) &&
                    editor.Document.SetPlacementSupport("smoke-support"), "Real external placement support failed");
                Require((bool)typeof(BlueprintEditorController).GetMethod("TrySave", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(editor, new object[] { false, true }), "Real external native copy save failed");
                editor.Close(true);
                var reopenedStore = new CompositeBlueprintStore(storePath);
                var saved = reopenedStore.All().Single();
                Require(editor.OpenExisting(saved, out error), "Real external saved editor reopen: " + error);
                Require(Math.Abs(saved.parts[0].position.x - raw.parts[0].position.x - .25f) < .0001 &&
                    saved.sourceDocuments[0].text == original && saved.placementSupportId == "smoke-support" &&
                    editor.Document.Parts.Count == fixture.Item3 && editor.Document.Supports.Count == 1,
                    "Real external disk/editor roundtrip lost source/edit/support");
                for (int index = 0; index < raw.parts.Count; ++index)
                {
                    var before = raw.parts[index]; var after = saved.parts[index];
                    Vector3 expectedPosition = before.position.ToVector3() + (index == 0 ? Vector3.right * .25f : Vector3.zero);
                    Vector3 expectedScale = Vector3.one;
                    Vector3 intrinsic = prefabs[before.prefabName].transform.localScale;
                    Require((after.position.ToVector3() - expectedPosition).sqrMagnitude < .00000001f &&
                        Quaternion.Angle(after.rotation.ToQuaternion(), before.rotation.ToQuaternion()) < .001f &&
                        (after.scale.ToVector3() - expectedScale).sqrMagnitude < .00000001f &&
                        after.baseScale.HasValue && after.baseScale.Value.ToVector3() == before.scale.ToVector3() &&
                        after.sourceLine == before.sourceLine && after.sourceDocumentId == before.sourceDocumentId,
                        "Real external import/edit/save changed another pose, scale or source row");
                    var openedPart = editor.Document.Parts[index];
                    Require((openedPart.Position - new Point3(expectedPosition.x, expectedPosition.y, expectedPosition.z)).LengthSquared < .00000001 &&
                        (openedPart.Scale - new Point3(expectedScale.x, expectedScale.y, expectedScale.z)).LengthSquared < .00000001 &&
                        openedPart.ResolveScale(new Point3(intrinsic.x, intrinsic.y, intrinsic.z)).Equals(
                            new Point3(before.scale.x, before.scale.y, before.scale.z)) &&
                        Quaternion.Angle(new Quaternion((float)openedPart.Rotation.X, (float)openedPart.Rotation.Y,
                            (float)openedPart.Rotation.Z, (float)openedPart.Rotation.W), before.rotation.ToQuaternion()) < .001f,
                        "Real external reopened editor changed part pose/scale");
                }
                Require(reopenedStore.TryExportFile(saved, runDirectory, "edited", false, out string exported, out error), error);
                var reimported = ExternalBlueprintLibrary.LoadFile(exported).Single();
                Require(reimported.importError == null && reimported.parts.Count == fixture.Item3 &&
                    reimported.sourceDocuments[0].text == original && reimported.placementSupportId == saved.placementSupportId,
                    "Real external export/reimport failed");
                foreach (var roundtrip in new[] { saved, reimported })
                {
                    Require(roundtrip.supports.Count == 1 &&
                        BlueprintPlacementFrame.TryResolve(roundtrip, out Vector3 supportPosition, out Quaternion restoredRotation) &&
                        (supportPosition - new Vector3(1.25f, -.25f, .5f)).sqrMagnitude < .00000001f &&
                        Quaternion.Angle(restoredRotation, supportRotation) < .001f,
                        "Real external roundtrip changed authored support pose");
                    for (int index = 0; index < saved.parts.Count; ++index)
                    {
                        var expected = saved.parts[index]; var actual = roundtrip.parts[index];
                        Require(actual.prefabName == expected.prefabName && actual.stableId == expected.stableId &&
                            (actual.position.ToVector3() - expected.position.ToVector3()).sqrMagnitude < .00000001f &&
                            Quaternion.Angle(actual.rotation.ToQuaternion(), expected.rotation.ToQuaternion()) < .001f &&
                            (actual.scale.ToVector3() - expected.scale.ToVector3()).sqrMagnitude < .00000001f &&
                            actual.baseScale.Equals(expected.baseScale) && actual.scaleIsAbsolute == expected.scaleIsAbsolute &&
                            actual.sourceDocumentId == expected.sourceDocumentId && actual.sourceLine == expected.sourceLine,
                            "Real external export/reimport changed part identity, pose, scale or source row");
                    }
                }
                var editorSupport = editor.Document.Supports.Single();
                Require(editorSupport.StableId == "smoke-support" && editorSupport.Name == "Опора испытания" &&
                    (editorSupport.Position - new Point3(1.25, -.25, .5)).LengthSquared < .00000001 &&
                    Quaternion.Angle(new Quaternion((float)editorSupport.Rotation.X, (float)editorSupport.Rotation.Y,
                        (float)editorSupport.Rotation.Z, (float)editorSupport.Rotation.W), supportRotation) < .001f,
                    "Real external reopened editor changed authored support pose/name");
                editor.Close(true);
                Bounds bounds = VerifyRealWorldPreview(saved, raw, prefabs, template,
                    Path.Combine(output, "real-" + Path.GetFileNameWithoutExtension(fixture.Item1) + "-world-preview.png"));
                evidence.Add("PASS real external " + raw.name + "; parts=" + fixture.Item3 + "; source=" + fixturePath +
                    "; sourceSHA256=" + fixture.Item2 + "; native/export=" + runDirectory);
                evidence.Add("EXTERNAL world renderer bounds size=" + bounds.size.ToString("R"));
                foreach (var pair in prefabs) evidence.Add("EXTERNAL prefab=" + pair.Key +
                    "; actual intrinsic scale=" + pair.Value.transform.localScale.ToString("R"));
            }
            Require(Hash(File.ReadAllBytes(fixturePath)) == fixture.Item2, "External workflow modified source");
        }
        Require(Hash(File.ReadAllBytes(archivePath)) == archiveHash, "External workflow modified archive");
        evidence.Add("PASS actual external pipeline; real assets, edit, free support, disk/editor reopen, export/reimport, renderer-only world preview. Native construction/world reload/network NOT executed.");
    }

    private static Bounds VerifyRealWorldPreview(CompositeBlueprintStore.Blueprint saved,
        CompositeBlueprintStore.Blueprint raw, IReadOnlyDictionary<string, GameObject> prefabs, Camera camera, string image)
    {
        var sources = new List<GameObject>();
        var ghost = new GameObject("RealExternalGhostFrame");
        var lightObject = new GameObject("RealExternalProofLight", typeof(Light));
        var light = lightObject.GetComponent<Light>();
        light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.None;
        lightObject.transform.rotation = Quaternion.Euler(50, -30, 0);
        using (var preview = new PlacementGhostPreviewView())
        try
        {
            var pieces = new List<Piece>();
            var plan = new List<PrecisionPlacementSession.TransformSnapshot>();
            Require(BlueprintPlacementFrame.TryResolve(saved, out Vector3 support, out Quaternion orientation), "Saved external support missing");
            BlueprintPlacementFrame.ToRootPose(new Vector3(25, 4, -15), Quaternion.Euler(0, 73, 0),
                support, orientation, 1f, out Vector3 position, out Quaternion rotation);
            foreach (var part in saved.parts)
            {
                GameObject prefab = prefabs[part.prefabName];
                var source = PlacementGhostPreviewView.CreateVisualClone(prefab, "RealExternalInertSource", 0);
                source.transform.localScale = prefab.transform.localScale;
                source.SetActive(false); sources.Add(source); pieces.Add(source.AddComponent<Piece>());
                plan.Add(new PrecisionPlacementSession.TransformSnapshot(position + rotation * part.position.ToVector3(),
                    rotation * part.rotation.ToQuaternion(), part.PlacementScale(source.transform.localScale)));
            }
            preview.Show(ghost, plan, pieces, -1);
            var instances = (List<GameObject>)typeof(PlacementGhostPreviewView).GetField("instances", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preview);
            Require(instances.Count == saved.parts.Count, "Real external world ghost omitted pieces");
            Bounds bounds = default; bool found = false;
            for (int index = 0; index < instances.Count; ++index)
            {
                Matrix4x4 expected = Matrix4x4.TRS(plan[index].Position, plan[index].Rotation, raw.parts[index].scale.ToVector3());
                Matrix4x4 actual = instances[index].transform.localToWorldMatrix;
                for (int element = 0; element < 16; ++element)
                    Require(Mathf.Abs(expected[element] - actual[element]) < .0001f, "Real external ghost has wrong scale/pose");
                bool visiblePart = false;
                foreach (Renderer renderer in instances[index].GetComponentsInChildren<Renderer>(true))
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy &&
                        renderer.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0)
                    {
                        visiblePart = true;
                        if (found) bounds.Encapsulate(renderer.bounds); else { bounds = renderer.bounds; found = true; }
                    }
                Require(visiblePart, "Real external ghost omitted native meshes for part " + index);
            }
            Require(found, "Real external ghost has no visible native meshes");
            camera.orthographic = true; camera.orthographicSize = bounds.extents.magnitude * 1.25f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.14f, .16f, .18f);
            camera.transform.SetPositionAndRotation(bounds.center + new Vector3(1, .65f, -1).normalized * bounds.size.magnitude * 2,
                Quaternion.LookRotation(new Vector3(-1, -.65f, 1)));
            Capture(camera, image);
            return bounds;
        }
        finally
        {
            foreach (var source in sources) Object.DestroyImmediate(source);
            Object.DestroyImmediate(ghost); Object.DestroyImmediate(lightObject);
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

    private static void DiagnoseChairCorners(Camera template, GameObject chair,
        string outputDirectory, ICollection<string> evidence)
    {
        Material[] originals = chair.GetComponentsInChildren<Renderer>(true)
            .SelectMany(renderer => renderer.sharedMaterials).Where(material => material &&
                material.shader.name == "Custom/Piece" && material.HasProperty("_RippleDistance")).Distinct().ToArray();
        float[] originalNoise = originals.Select(material => material.GetFloat("_RippleDistance")).ToArray();
        Require(originalNoise.Any(value => value > 0), "Native chair fixture lost its decorative vertex noise");
        Quaternion rotation = Quaternion.Euler(0, 90, 0);
        var document = new BlueprintEditorDocument(null, "Native chair corner proof", "Test", new[]
        {
            new BlueprintEditorPart("chair-corners", chair.name, "Chair", new Point3(4.387, 0, -.153),
                new Rotation3(rotation.x, rotation.y, rotation.z, rotation.w))
        });
        using (var scene = new BlueprintEditorScene(template, _ => chair))
        {
            Require(scene.TrySync(document, out string error), error);
            scene.ExperimentalGeometryPoints = true; scene.NativePointsOnly = false;
            scene.TryGetGizmoAnchors(new[] { "chair-corners" }, out Vector3[] points, out int nativeStart);
            Require(scene.GeometryAnchorCount > 0, "Native chair has no experimental corners");
            var visuals = (IDictionary)typeof(BlueprintEditorScene).GetField("visuals",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(scene);
            GameObject visual = VisualField<GameObject>(visuals["chair-corners"], "Root");
            var vertices = new List<Vector3>();
            Bounds bounds = default; bool first = true;
            foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter.GetComponent<Renderer>();
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Require(BlueprintEditorMeshData.TryRead(filter.sharedMesh, out Vector3[] raw, out _, out error), error);
                foreach (Vector3 vertex in raw) vertices.Add(filter.transform.TransformPoint(vertex));
                foreach (Material material in renderer.sharedMaterials)
                    if (material.shader.name == "Custom/Piece" && material.HasProperty("_RippleDistance"))
                    {
                        Require(material.GetFloat("_RippleDistance") == 0, "Editor chair still deforms its visible vertices");
                        Require(!originals.Contains(material), "Editor changed a shared native material");
                    }
                if (first) { bounds = renderer.bounds; first = false; } else bounds.Encapsulate(renderer.bounds);
            }
            int start = scene.GeometryAnchorStart, count = scene.GeometryAnchorCount;
            float vertexError = 0, drawError = 0;
            for (int point = start; point < start + count; ++point)
                vertexError = Mathf.Max(vertexError, vertices.Min(vertex => Vector3.Distance(vertex, points[point])));
            Require(vertexError < .00001f, "Native chair corner is offset from the actual mesh vertex");
            scene.SetViewport(new Rect(0, 0, 1024, 1024));
            scene.GizmoFamily = GizmoFamily.Points;
            Vector3 cameraPosition = bounds.center + new Vector3(.85f, .35f, -1.05f);
            scene.SetCameraPose(cameraPosition, Quaternion.LookRotation(bounds.center - cameraPosition));
            scene.SetGizmoModelSize(new[] { "chair-corners" });
            scene.ShowGizmo(BlueprintEditorTool.Transform, bounds.center, rotation, true,
                GizmoHandleKind.None, GizmoAxis.None, points, -1, nativeStart, true, false, default, false);
            TransformGizmoView gizmo = VisualField<TransformGizmoView>(scene, "gizmo");
            var handles = VisualField<IList>(gizmo, "anchorHandles");
            bool picked = false, snapped = false;
            for (int point = start; point < start + count; ++point)
            {
                var line = (LineRenderer)handles[point];
                Vector3 centre = (line.GetPosition(0) + line.GetPosition(8)) * .5f;
                drawError = Mathf.Max(drawError, Vector3.Distance(centre, points[point]));
                Vector3 screen = scene.Camera.WorldToScreenPoint(points[point]);
                picked |= scene.HitTestAnchor(points, screen) == point;
                var targets = new List<Vector3>(); var native = new List<bool>();
                if (scene.TryCursorSnap(Array.Empty<string>(), new[] { points[point] + Vector3.back * .02f },
                    new[] { 0 }, 0, 0, targets, native, out _, out Vector3 captured, out bool isNative, cursor: screen))
                    snapped |= !isNative && Vector3.Distance(captured, points[point]) < .00001f;
            }
            Require(drawError < .00001f && picked && snapped, "Native chair dot draw/pick/magnet mismatch");
            for (int point = 0; point < handles.Count; ++point)
                if (point < start || point >= start + count) ((LineRenderer)handles[point]).gameObject.SetActive(false);
            Capture(scene.Camera, Path.Combine(outputDirectory, "chair02-exact-corners.png"));
            for (int index = 0; index < originals.Length; ++index)
                Require(originals[index].GetFloat("_RippleDistance") == originalNoise[index],
                    "Editor changed the world's native chair material");
            evidence.Add("PASS native piece_chair02: " + count + " exact dots; vertex/draw errors=" +
                vertexError.ToString("F9") + "/" + drawError.ToString("F9") +
                "; point picking and helper magnet; editor-only noise disabled; native materials unchanged.");
        }
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
            else Require(properties.isEmpty, "Unhovered exact mesh retains tint: " + id);
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
