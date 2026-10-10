using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class LargeSnapPersistenceAcceptance
{
    internal static void Run(string output)
    {
        string archivePath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../artifacts/external-blueprints/2v2t-5.0.0/package.zip"));
        string originalHash = Hash(archivePath);
        Require(originalHash == "4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88", "Unexpected large corpus");
        string[] names = { "Arani_Forte_Negro.blueprint", "Big_Bridge_1.blueprint", "Decay.blueprint", "Flabellum.blueprint" };
        int[] counts = { 6596, 2012, 11790, 17125 }, maxima = { 46, 14, 24, 17 };
        var receipts = new List<string>();
        int key = "BuildWorks_SnapPoints_v1".GetStableHashCode();
        using (var archive = ZipFile.OpenRead(archivePath))
        for (int sample = 0; sample < names.Length; ++sample)
        {
            GameObject root = new GameObject("LargeSnapFixture"), prefabObject = new GameObject("UnitBaseProbe");
            try
            {
                var entry = archive.Entries.Single(e => Path.GetFileName(e.FullName) == names[sample]);
                Require(entry.Length <= BlueprintSourceDocument.MaximumBytes, "Oversized source");
                string text; using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                var source = ExternalBlueprintLibrary.Parse(text, entry.FullName);
                Require(source.importError == null && source.customSnapPoints.Count == counts[sample], "Large source refused");
                // Inert known-unit bases exercise transforms/codec, not actual model or prefab availability.
                var blueprint = ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
                Require(blueprint.importError == null, blueprint.importError);
                Piece prefab = prefabObject.AddComponent<Piece>();
                var watch = Stopwatch.StartNew();
                var hammer = new GameObject("OrderedHammerChildren"); hammer.transform.SetParent(root.transform, false);
                Piece hammerPiece = hammer.AddComponent<Piece>();
                foreach (var point in blueprint.customSnapPoints)
                {
                    var node = new GameObject("snappoint") { tag = "snappoint", hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave };
                    node.transform.SetParent(hammer.transform, false); node.transform.localPosition = point.ToVector3();
                }
                double hammerCreationMs = watch.Elapsed.TotalMilliseconds;
                var hammerPoints = new List<Transform>(); watch.Restart(); hammerPiece.GetSnapPoints(hammerPoints);
                double hammerEnumerationMs = watch.Elapsed.TotalMilliseconds;
                Require(hammerPoints.Count == counts[sample], "Hammer source children truncated");
                for (int p = 0; p < hammerPoints.Count; ++p)
                    Require(Point(hammerPoints[p].localPosition).Equals(Point(blueprint.customSnapPoints[p].ToVector3())), "Hammer source order changed");
                watch.Restart();
                Point3[][] sets = BlueprintPlacementFrame.SnapLocalSets(blueprint, Enumerable.Repeat(prefab, blueprint.parts.Count).ToArray());
                double assignmentMs = watch.Elapsed.TotalMilliseconds;
                Require(sets.Sum(s => s?.Length ?? 0) == counts[sample] && sets.Max(s => s?.Length ?? 0) == maxima[sample], "Partition count changed");
                var originals = new List<Vector3>[sets.Length];
                var index = new BlueprintNearestPartIndex(blueprint.parts.Select(p => Point(p.position.ToVector3())).ToArray());
                foreach (var point in blueprint.customSnapPoints)
                {
                    int p = index.FindNearest(Point(point.ToVector3()));
                    (originals[p] ?? (originals[p] = new List<Vector3>())).Add(point.ToVector3());
                }
                Quaternion rotation = Quaternion.Euler(17, 61, -9); Vector3 origin = new Vector3(20, 8, -35);
                var captured = new BlueprintSnapPointSet(BlueprintEditorDocument.MaximumCustomSnapPoints);
                var placedPieces = new List<Piece>();
                double coldMs = 0, warmMs = 0, replicaMs = 0;
                int created = 0;
                for (int p = 0; p < sets.Length; ++p)
                {
                    if (sets[p] == null) continue;
                    var part = blueprint.parts[p];
                    var placed = new GameObject("Placed_" + p); placed.transform.SetParent(root.transform, false);
                    placed.transform.SetPositionAndRotation(origin + rotation * part.position.ToVector3(), rotation * part.rotation.ToQuaternion());
                    placed.transform.localScale = part.RootScale(prefab.transform.localScale);
                    Piece piece = placed.AddComponent<Piece>(); var view = placed.AddComponent<ZNetView>();
                    placedPieces.Add(piece);
                    BlueprintWorldSnapPoints.Apply(placed, sets[p]);
                    // World codec writes Single coordinates, not intermediate Geometry doubles.
                    Require(BlueprintWorldSnapPoints.TryRead(piece, out var read) && read.SequenceEqual(sets[p].Select(FloatPoint)), "Per-piece float32 bytes changed local positions/order");
                    var nodes = new List<Transform>(); watch.Restart(); BlueprintWorldSnapPoints.Append(piece, nodes); coldMs += watch.Elapsed.TotalMilliseconds;
                    Require(nodes.Count == sets[p].Length && placed.transform.childCount == nodes.Count, "Missing or excess child transforms");
                    int writes = view.GetZDO().Writes;
                    watch.Restart(); BlueprintWorldSnapPoints.Append(piece, nodes); warmMs += watch.Elapsed.TotalMilliseconds;
                    Require(nodes.Count == sets[p].Length && view.GetZDO().Writes == writes, "Warm query duplicates or writes state");
                    for (int s = 0; s < nodes.Count; ++s)
                    {
                        Require(Point(nodes[s].localPosition).Equals(read[s]) &&
                            Vector3.Distance(nodes[s].position, origin + rotation * originals[p][s]) < .001f, "Root/XYZ/source position changed");
                        Vector3 local = Quaternion.Inverse(rotation) * (nodes[s].position - origin);
                        Require(captured.TryAdd(Point(local)), "Aggregate reverse collector refused valid point");
                    }
                    var native = new List<Transform>(); piece.GetSnapPoints(native);
                    Require(native.Count == 0, "Custom nodes became native midpoint inputs");
                    var replica = new GameObject("Replica_" + p); replica.transform.SetParent(root.transform, false);
                    replica.transform.SetPositionAndRotation(placed.transform.position, placed.transform.rotation); replica.transform.localScale = placed.transform.localScale;
                    Piece loaded = replica.AddComponent<Piece>(); var replicaView = replica.AddComponent<ZNetView>(); replicaView.Owner = false;
                    replicaView.GetZDO().Set(key, (byte[])view.GetZDO().GetByteArray(key, null).Clone());
                    int replicaWrites = replicaView.GetZDO().Writes;
                    var restored = new List<Transform>(); watch.Restart(); BlueprintWorldSnapPoints.Append(loaded, restored); replicaMs += watch.Elapsed.TotalMilliseconds;
                    Require(restored.Count == nodes.Count && replicaView.GetZDO().Writes == replicaWrites, "Read-only replica mutated state/count");
                    for (int s = 0; s < nodes.Count; ++s)
                        Require(restored[s].localPosition == nodes[s].localPosition && restored[s].position == nodes[s].position, "Replica changed transformed point");
                    created += nodes.Count;
                }
                Require(created == counts[sample] && captured.Points.Count > 512, "Large count/capture path was truncated");
                var all = new List<Transform>(); watch.Restart();
                foreach (Piece piece in placedPieces) BlueprintWorldSnapPoints.Append(piece, all);
                double aggregateMs = watch.Elapsed.TotalMilliseconds;
                Require(all.Count == created, "Shared world-query list lost nodes");
                string collection = WorldSnapCollectionAcceptance.Measure(placedPieces);
                receipts.Add(names[sample] + $": parts={blueprint.parts.Count}, points={created}, maxPerPiece={maxima[sample]}, capturedUnique={captured.Points.Count}; hammerChildren={hammerCreationMs:F4}ms, hammerEnumeration={hammerEnumerationMs:F4}ms, assignment={assignmentMs:F4}ms, coldAppend={coldMs:F4}ms, warmAppend={warmMs:F4}ms, replicaAppend={replicaMs:F4}ms, sharedListAppend={aggregateMs:F4}ms; {collection}");
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(prefabObject); BlueprintWorldSnapPoints.RemoveAll(); }
        }
        Require(Hash(archivePath) == originalHash, "Source archive changed");
        File.WriteAllLines(Path.Combine(output, "large-snap-persistence-check.txt"), new[] {
            "PASS:4 actual archival sources -> production nearest-part partition -> per-piece512 codec -> Apply/Append -> byte-copy read-only replica; exact local order/world positions, no duplicated native snaps, large bounded reverse collector. Inert unit-base prefabs/ZDO fixtures, not actual models, Session capture, Valheim save/network/FPS. Single samples, not medians."
        }.Concat(receipts));
    }
    private static Point3 Point(Vector3 p) => new Point3(p.x, p.y, p.z);
    private static Point3 FloatPoint(Point3 p) => new Point3((float)p.X, (float)p.Y, (float)p.Z);
    private static string Hash(string path)
    { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
    private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
}
