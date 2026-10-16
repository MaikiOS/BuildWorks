using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

// Local read-only asset evidence. No native component is instantiated or executed.
public static class PrefabMigrationEvidence
{
    const string Root = @"C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\StreamingAssets\SoftRef";
    [Serializable] public class Report { public string utc, manifestSha256, geometryLimit; public bool productionGeometryPredicate; public List<Bundle> bundles = new(); public List<Prefab> prefabs = new(); }
    [Serializable] public class Bundle { public string name, sha256; public long bytes; }
    [Serializable] public class Prefab { public string name, asset; public List<Node> nodes = new(); }
    [Serializable] public class Node
    {
        public string path, tag; public bool active;
        public Vector3 position, scale; public Quaternion rotation;
        public string meshSha256, rendererType, rendererState; public Vector3 meshCenter, meshSize;
        public List<string> materials = new(), lodData = new();
        public List<string> colliderData = new();
    }
    static string Hash(byte[] bytes) { using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    static string FileHash(string path) { using var input = File.OpenRead(path); using var sha = SHA256.Create(); return BitConverter.ToString(sha.ComputeHash(input)).Replace("-", ""); }
    public static void Run() => Run("prefab-migration-191");
    public static void Run194() => Run("prefab-migration-194");
    private static void Run(string outputFolder)
    {
        var loaded = new Dictionary<string, AssetBundle>();
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts", outputFolder));
        Directory.CreateDirectory(output);
        try
        {
            string manifest = Path.Combine(Root, "manifest_extended");
            string[] lines = File.ReadAllLines(manifest);
            var report = new Report { utc = DateTime.UtcNow.ToString("O"), manifestSha256 = FileHash(manifest),
                geometryLimit = "Installed asset geometry only. No native costs, support, placement, save/load or network proof. No game components instantiated." };
            var dependencies = new Dictionary<string, List<string>>(); string current = null;
            foreach (string line in lines)
            {
                if (line == "asset locations:") break;
                if (line.StartsWith("- bundle: ")) { current = line.Substring(10).Trim(); dependencies[current] = new(); }
                else if (current != null && line.StartsWith("  - ")) dependencies[current].Add(line.Substring(4).Trim());
            }
            AssetBundle Load(string name)
            {
                if (loaded.TryGetValue(name, out var found)) return found;
                if (dependencies.TryGetValue(name, out var required)) foreach (string dependency in required) Load(dependency);
                string path = Path.Combine(Root, "Bundles", name);
                var bundle = AssetBundle.LoadFromFile(path);
                if (!bundle) throw new IOException("Cannot load " + name);
                loaded.Add(name, bundle);
                report.bundles.Add(new Bundle { name = name, bytes = new FileInfo(path).Length, sha256 = FileHash(path) });
                return bundle;
            }
            var prototypes = new List<GameObject>();
            foreach (string name in new[] { "wood_wall_roof", "wood_wall_roof_a" })
            {
                string asset = "Assets/GameElements/Pieces/" + name + ".prefab";
                int row = Array.FindIndex(lines, line => line.Trim() == "path in bundle: " + asset);
                if (row < 2 || !lines[row - 1].Trim().StartsWith("bundle: ")) throw new IOException("Missing manifest entry " + asset);
                string bundleName = lines[row - 1].Trim().Substring(8);
                var prefab = Load(bundleName).LoadAsset<GameObject>(asset);
                if (!prefab) throw new IOException("Missing prefab " + asset);
                prototypes.Add(prefab);
                var evidence = new Prefab { name = name, asset = asset };
                void Walk(Transform node, string path)
                {
                    var entry = new Node { path = path, tag = node.tag, active = node.gameObject.activeSelf,
                        position = node.localPosition, rotation = node.localRotation, scale = node.localScale };
                    var filter = node.GetComponent<MeshFilter>();
                    var mesh = filter ? filter.sharedMesh : null;
                    var renderer = node.GetComponent<Renderer>();
                    if (renderer)
                    {
                        entry.rendererType = renderer.GetType().Name;
                        entry.rendererState = renderer.enabled + ";" + renderer.shadowCastingMode + ";" + renderer.receiveShadows;
                        // IDs compare shared material references within this one loaded-bundle session only.
                        foreach (var material in renderer.sharedMaterials)
                            entry.materials.Add(material ? material.name + ";" + material.GetInstanceID() : "null");
                        if (renderer is SkinnedMeshRenderer) throw new NotSupportedException("Skinned migration evidence requires a separate adapter.");
                    }
                    var lod = node.GetComponent<LODGroup>();
                    if (lod) foreach (var level in lod.GetLODs())
                        entry.lodData.Add(level.screenRelativeTransitionHeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";" +
                            level.fadeTransitionWidth.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ";" +
                            string.Join("|", level.renderers.Select(r => r ? AnimationUtility.CalculateTransformPath(r.transform, prefab.transform) : "null")));
                    if (mesh)
                    {
                        entry.meshCenter = mesh.bounds.center; entry.meshSize = mesh.bounds.size;
                        using var data = new MemoryStream(); using var writer = new BinaryWriter(data, Encoding.UTF8, true);
                        var vertices = mesh.vertices; writer.Write(vertices.Length);
                        foreach (var v in vertices) { writer.Write(v.x); writer.Write(v.y); writer.Write(v.z); }
                        var normals = mesh.normals; writer.Write(normals.Length);
                        foreach (var n in normals) { writer.Write(n.x); writer.Write(n.y); writer.Write(n.z); }
                        var uvs = mesh.uv; writer.Write(uvs.Length);
                        foreach (var uv in uvs) { writer.Write(uv.x); writer.Write(uv.y); }
                        writer.Write(mesh.subMeshCount);
                        for (int s = 0; s < mesh.subMeshCount; s++) { writer.Write((int)mesh.GetTopology(s)); var indices = mesh.GetIndices(s); writer.Write(indices.Length); foreach (int i in indices) writer.Write(i); }
                        writer.Flush(); entry.meshSha256 = Hash(data.ToArray());
                    }
                    foreach (var collider in node.GetComponents<Collider>())
                    {
                        string description = collider.GetType().Name + ";enabled=" + collider.enabled + ";trigger=" + collider.isTrigger;
                        if (collider is BoxCollider box) description += ";center=" + box.center.ToString("R") + ";size=" + box.size.ToString("R");
                        else if (collider is MeshCollider mc) description += ";convex=" + mc.convex + ";mesh=" + (mc.sharedMesh ? mc.sharedMesh.name : "null");
                        else description += ";uncomparedShape=true";
                        entry.colliderData.Add(description);
                    }
                    evidence.nodes.Add(entry);
                    for (int i = 0; i < node.childCount; i++) { var child = node.GetChild(i); Walk(child, path + "/" + i + ":" + child.name); }
                }
                Walk(prefab.transform, "root"); report.prefabs.Add(evidence);
            }
            report.productionGeometryPredicate = OstrixMods.BuildWorks.BlueprintPrefabMigrationGeometry.Matches(prototypes[0], prototypes[1]);
            if (!report.productionGeometryPredicate) throw new InvalidOperationException("Production prefab geometry predicate refused current native pair");
            File.WriteAllText(Path.Combine(output, "geometry.json"), JsonUtility.ToJson(report, true));
            Debug.Log("PASS read-only prefab geometry evidence: " + output);
            foreach (var bundle in loaded.Values.Reverse()) bundle.Unload(true);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { File.WriteAllText(Path.Combine(output, "failure.txt"), error.ToString()); Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
