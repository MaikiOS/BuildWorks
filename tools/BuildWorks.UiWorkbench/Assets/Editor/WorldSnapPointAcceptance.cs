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
}
