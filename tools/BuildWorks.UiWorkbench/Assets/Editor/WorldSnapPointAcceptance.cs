using System;
using System.Collections.Generic;
using System.IO;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

public static class WorldSnapPointAcceptance
{
    public static void Run(string output)
    {
        TestNearestPartAssignments();
        var source = new GameObject("Source");
        var placed = new GameObject("Placed");
        var reloaded = new GameObject("Reloaded");
        try
        {
            Piece prefab = source.AddComponent<Piece>();
            source.transform.localScale = new Vector3(0.5f, 2, 1);
            var part = new CompositeBlueprintStore.Part
            {
                position = new CompositeBlueprintStore.VectorData(new Vector3(4, 2, -1)),
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(25, 60, 10)),
                scale = new CompositeBlueprintStore.VectorData(new Vector3(2, 0.5f, 3))
            };
            Vector3 point = new Vector3(5, -2, 3);
            var blueprint = new CompositeBlueprintStore.Blueprint
            {
                parts = new List<CompositeBlueprintStore.Part> { part },
                customSnapPoints = new List<CompositeBlueprintStore.VectorData> { new CompositeBlueprintStore.VectorData(point) }
            };
            Point3[] local = BlueprintPlacementFrame.SnapLocalSets(blueprint, new[] { prefab })[0];
            Piece piece = placed.AddComponent<Piece>();
            var view = placed.AddComponent<ZNetView>();
            Quaternion rootRotation = Quaternion.Euler(-20, 40, 5);
            Vector3 rootPosition = new Vector3(20, 10, -5);
            float uniform = 1.5f;
            placed.transform.SetPositionAndRotation(rootPosition + rootRotation * (part.position.ToVector3() * uniform),
                rootRotation * part.rotation.ToQuaternion());
            placed.transform.localScale = Vector3.Scale(source.transform.localScale, part.scale.ToVector3()) * uniform;
            BlueprintWorldSnapPoints.Apply(placed, local);
            var nodes = new List<Transform>();
            BlueprintWorldSnapPoints.Append(piece, nodes);
            Require(nodes.Count == 1 && Vector3.Distance(nodes[0].position,
                rootPosition + rootRotation * (point * uniform)) < 0.0001f, "Root pose/XYZ/base scale mismatch");
            var native = new List<Transform>(); piece.GetSnapPoints(native);
            Require(native.Count == 0, "Custom point leaked into native midpoint inputs");
            BlueprintWorldSnapPoints.Append(piece, nodes);
            Require(nodes.Count == 1 && placed.GetComponents<BlueprintWorldSnapPoints>().Length == 1, "Snap queries duplicate nodes");
            Require(BlueprintWorldSnapPoints.TryRead(piece, out var captured) && captured.Length == 1, "Capture lost durable points");
            int key = "BuildWorks_SnapPoints_v1".GetStableHashCode();
            byte[] saved = (byte[])view.GetZDO().GetByteArray(key, null).Clone();
            Piece loaded = reloaded.AddComponent<Piece>();
            var replica = reloaded.AddComponent<ZNetView>(); replica.Owner = false;
            reloaded.transform.SetPositionAndRotation(placed.transform.position, placed.transform.rotation);
            reloaded.transform.localScale = placed.transform.localScale;
            replica.GetZDO().Set(key, saved);
            nodes.Clear(); BlueprintWorldSnapPoints.Append(loaded, nodes);
            Require(nodes.Count == 1 && Vector3.Distance(nodes[0].position, rootPosition + rootRotation * (point * uniform)) < 0.0001f,
                "Reconstructed replica changed world point");
            int writes = replica.GetZDO().Writes;
            bool denied = false;
            try { BlueprintWorldSnapPoints.Apply(reloaded, local); } catch (InvalidOperationException) { denied = true; }
            Require(denied && replica.GetZDO().Writes == writes, "Unowned object accepted write");
            replica.GetZDO().Set(key, new byte[] { 1, 2, 3 });
            nodes.Clear(); BlueprintWorldSnapPoints.Append(loaded, nodes);
            Require(nodes.Count == 0 && !BlueprintWorldSnapPoints.TryRead(loaded, out _), "Malformed package was executed");
            foreach (float badScale in new[] { 0f, 1e-8f })
            {
                source.transform.localScale = Vector3.one * badScale;
                bool rejected = false;
                try { BlueprintPlacementFrame.SnapLocalSets(blueprint, new[] { prefab }); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected, "Unpersistable native prefab scale passed preflight");
            }
            BlueprintWorldSnapPoints.RemoveAll();
            File.WriteAllText(Path.Combine(output, "world-snap-points-check.txt"),
                "PASS: root rotation + XYZ/base/uniform scale; no midpoint duplication; native ZDO bytes reconstruct read-only replica; owner guard; malformed payload refused. Isolated host, not Valheim save/network proof.");
        }
        finally
        {
            Object.DestroyImmediate(source); Object.DestroyImmediate(placed); Object.DestroyImmediate(reloaded);
            BlueprintWorldSnapPoints.RemoveAll();
        }
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }

    private static void TestNearestPartAssignments()
    {
        var first = new GameObject("NearestPrefabA");
        var second = new GameObject("NearestPrefabB");
        try
        {
            var prefabs = new[] { first.AddComponent<Piece>(), second.AddComponent<Piece>() };
            first.transform.localScale = new Vector3(0.5f, 2, 1);
            second.transform.localScale = new Vector3(3, 0.25f, 2);
            var random = new System.Random(120);
            var blueprint = new CompositeBlueprintStore.Blueprint {
                parts = new List<CompositeBlueprintStore.Part>(), customSnapPoints = new List<CompositeBlueprintStore.VectorData>() };
            var pieces = new List<Piece>();
            var expected = new List<Point3>[257];
            for (int i = 0; i < expected.Length; ++i)
            {
                blueprint.parts.Add(new CompositeBlueprintStore.Part {
                    position = new CompositeBlueprintStore.VectorData(RandomPoint(random)),
                    rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.Euler(RandomPoint(random))),
                    scale = new CompositeBlueprintStore.VectorData(new Vector3(0.5f + i % 3, 1 + i % 2, 2)) });
                pieces.Add(prefabs[i % 2]);
            }
            blueprint.parts[0].position = new CompositeBlueprintStore.VectorData(Vector3.right);
            blueprint.parts[1].position = new CompositeBlueprintStore.VectorData(Vector3.left);
            for (int i = 0; i < 1200; ++i)
            {
                Vector3 point = i < 3 ? Vector3.zero : RandomPoint(random);
                blueprint.customSnapPoints.Add(new CompositeBlueprintStore.VectorData(point));
                int nearest = 0; float distance = float.PositiveInfinity;
                for (int partIndex = 0; partIndex < blueprint.parts.Count; ++partIndex)
                {
                    float candidate = (point - blueprint.parts[partIndex].position.ToVector3()).sqrMagnitude;
                    if (candidate < distance) { nearest = partIndex; distance = candidate; }
                }
                var part = blueprint.parts[nearest];
                Vector3 unscaled = Quaternion.Inverse(part.rotation.ToQuaternion()) * (point - part.position.ToVector3());
                Vector3 scale = part.RootScale(pieces[nearest].transform.localScale);
                (expected[nearest] ?? (expected[nearest] = new List<Point3>())).Add(
                    new Point3(unscaled.x / scale.x, unscaled.y / scale.y, unscaled.z / scale.z));
            }
            Point3[][] actual = BlueprintPlacementFrame.SnapLocalSets(blueprint, pieces);
            for (int i = 0; i < expected.Length; ++i)
            {
                Require((expected[i] == null) == (actual[i] == null), "Nearest part membership changed");
                if (expected[i] == null) continue;
                Require(expected[i].Count == actual[i].Length, "Nearest point count changed");
                for (int p = 0; p < actual[i].Length; ++p)
                    Require(expected[i][p].Equals(actual[i][p]), "Float local snap position/order changed");
            }
            blueprint.customSnapPoints.Clear();
            actual = BlueprintPlacementFrame.SnapLocalSets(blueprint, pieces);
            Require(actual.Length == expected.Length && Array.TrueForAll(actual, set => set == null), "Empty snap sets changed");
        }
        finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
    }

    private static Vector3 RandomPoint(System.Random random) => new Vector3(
        (float)(random.NextDouble() * 100 - 50), (float)(random.NextDouble() * 100 - 50),
        (float)(random.NextDouble() * 100 - 50));
}
