using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Reconstructed from the native piece's replicated/saveable ZDO, never a hidden carrier.
    internal sealed class BlueprintWorldSnapPoints : MonoBehaviour
    {
        private static readonly int Key = "BuildWorks_SnapPoints_v1".GetStableHashCode();
        private static readonly HashSet<BlueprintWorldSnapPoints> Created = new HashSet<BlueprintWorldSnapPoints>();
        private readonly List<Transform> nodes = new List<Transform>();
        private byte[] cached;

        internal static bool TryRead(Piece piece, out Point3[] points)
        {
            points = Array.Empty<Point3>();
            ZNetView view = piece ? piece.GetComponent<ZNetView>() : null;
            byte[] bytes = view && view.IsValid() ? view.GetZDO().GetByteArray(Key, (byte[])null) : null;
            if (bytes == null || bytes.Length == 0) return true;
            try { points = BlueprintSnapPointCodec.Decode(bytes); return true; }
            catch (ArgumentException) { return false; }
        }

        internal static void Apply(GameObject placed, IReadOnlyList<Point3> points)
        {
            if (points == null || points.Count == 0) return;
            ZNetView view = placed ? placed.GetComponent<ZNetView>() : null;
            if (!view || !view.IsValid() || !view.IsOwner())
                throw new InvalidOperationException("Cannot persist blueprint snap points on an unowned piece.");
            view.GetZDO().Set(Key, BlueprintSnapPointCodec.Encode(points));
        }

        internal static void Append(Piece piece, List<Transform> result)
        {
            if (!piece || result == null) return;
            ZNetView view = piece.GetComponent<ZNetView>();
            byte[] bytes = view && view.IsValid() ? view.GetZDO().GetByteArray(Key, (byte[])null) : null;
            BlueprintWorldSnapPoints component = piece.GetComponent<BlueprintWorldSnapPoints>();
            if (bytes == null || bytes.Length == 0)
            {
                if (component) component.Clear();
                return;
            }
            if (bytes.Length > 8 + BlueprintSnapPointCodec.MaximumPoints * 12)
            { if (component) component.Clear(); return; }
            if (!component)
            {
                component = piece.gameObject.AddComponent<BlueprintWorldSnapPoints>();
                Created.Add(component);
            }
            if (component.cached == null || !component.cached.SequenceEqual(bytes))
            {
                component.Clear();
                // Remember invalid packages too: don't retry hostile bytes every snap query.
                component.cached = (byte[])bytes.Clone();
                Point3[] points;
                try { points = BlueprintSnapPointCodec.Decode(bytes); }
                catch (ArgumentException) { return; }
                foreach (Point3 point in points)
                {
                    var node = new GameObject("BuildWorks_CustomSnap") { hideFlags = HideFlags.DontSave };
                    node.transform.SetParent(piece.transform, false);
                    node.transform.localPosition = new Vector3((float)point.X, (float)point.Y, (float)point.Z);
                    // Untagged: native/midpoint generation must not turn these into extra edge middles.
                    component.nodes.Add(node.transform);
                }
            }
            foreach (Transform node in component.nodes)
                if (node && !result.Contains(node)) result.Add(node);
        }

        internal static void RemoveAll()
        {
            foreach (BlueprintWorldSnapPoints component in Created.ToArray())
                if (component) { component.Clear(); UnityEngine.Object.Destroy(component); }
            Created.Clear();
        }

        private void Clear()
        {
            foreach (Transform node in nodes)
                if (node) { node.gameObject.SetActive(false); UnityEngine.Object.Destroy(node.gameObject); }
            nodes.Clear();
            cached = null;
        }

        private void OnDestroy() { Clear(); Created.Remove(this); }
    }
}
