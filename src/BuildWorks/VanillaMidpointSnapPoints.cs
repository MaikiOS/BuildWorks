using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed class VanillaMidpointSnapPoints : MonoBehaviour
    {
        private const string NamePrefix = "BuildWorks_MidSnap_";
        private const int MaximumGeneratedPoints = 32;
        private readonly List<Transform> native = new List<Transform>();
        private readonly List<Transform> generated = new List<Transform>();
        private readonly List<ChildState> children = new List<ChildState>();
        // Unity remaps these child references when a decorated prefab is cloned.
        [SerializeField] private List<GameObject> owned = new List<GameObject>();
        [SerializeField] private Transform owner;
        private int activeCount;
        private bool ready, removing;

        private struct ChildState
        {
            internal Transform Node;
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal bool Generated, Snap, Active;
        }

        public static void Append(Piece piece, List<Transform> result)
        {
            if (!piece || result == null || piece.gameObject.name.StartsWith(
                HammerBlueprintPieceRegistry.PrefabPrefix,
                StringComparison.Ordinal)) return;

            var cache = piece.GetComponent<VanillaMidpointSnapPoints>();
            if (!cache)
            {
                cache = piece.gameObject.AddComponent<VanillaMidpointSnapPoints>();
            }
            if (cache.removing) return;
            if (!cache.Matches(piece.transform)) cache.Rebuild(piece.transform, result);
            for (int index = cache.activeCount; index < cache.generated.Count; ++index)
                BlueprintWorldSnapPoints.RemoveRetired(result, cache.generated[index]);
            for (int index = 0; index < cache.activeCount; ++index)
                BlueprintWorldSnapPoints.AppendUnique(result, cache.generated[index]);
        }

        private bool Matches(Transform root)
        {
            if (!ready || root.childCount != children.Count) return false;
            for (int index = 0; index < children.Count; ++index)
            {
                ChildState state = children[index];
                Transform node = root.GetChild(index);
                if (!node || node != state.Node || IsGenerated(node) != state.Generated ||
                    node.CompareTag("snappoint") != state.Snap ||
                    (state.Generated && node.gameObject.activeSelf != state.Active) ||
                    !node.localPosition.Equals(state.Position) || !node.localRotation.Equals(state.Rotation))
                    return false;
            }
            return true;
        }

        private void Rebuild(Transform root, List<Transform> result)
        {
            ready = false;
            if (owner != root) { owned.Clear(); owner = root; }
            for (int index = owned.Count - 1; index >= 0; --index)
            {
                GameObject point = owned[index];
                if (point && point.transform.parent == root) continue;
                if (point)
                {
                    BlueprintWorldSnapPoints.RemoveRetired(result, point.transform);
                    point.tag = "Untagged";
                    point.SetActive(false);
                    UnityEngine.Object.Destroy(point);
                }
                owned.RemoveAt(index);
            }
            native.Clear();
            generated.Clear();
            for (int index = 0; index < root.childCount; ++index)
            {
                Transform child = root.GetChild(index);
                if (IsGenerated(child)) generated.Add(child);
                else if (child.CompareTag("snappoint")) native.Add(child);
            }

            Point3[] positions = new Point3[native.Count];
            for (int index = 0; index < native.Count; ++index)
                positions[index] = ToGeometry(native[index].localPosition);
            IReadOnlyList<Edge3> edges = native.Count < 2
                ? Array.Empty<Edge3>() : AnchorAdjustment.ConnectableSnapEdges(positions);
            int count = Math.Min(edges.Count, MaximumGeneratedPoints);
            for (int index = 0; index < count; ++index)
            {
                if (index == generated.Count) generated.Add(Create(root, index));
                Transform midpoint = generated[index];
                Edge3 edge = edges[index];
                midpoint.localPosition = ToUnity((edge.Start + edge.End) * 0.5);
                int first = FindPoint(positions, edge.Start);
                int second = FindPoint(positions, edge.End);
                midpoint.localRotation = first >= 0 && second >= 0
                    ? Quaternion.Slerp(
                        native[first].localRotation,
                        native[second].localRotation,
                        0.5f)
                    : Quaternion.identity;
                midpoint.gameObject.tag = "snappoint";
                midpoint.gameObject.SetActive(true);
            }
            for (int index = count; index < generated.Count; ++index)
            {
                generated[index].gameObject.tag = "Untagged";
                generated[index].gameObject.SetActive(false);
            }
            activeCount = count;
            children.Clear();
            for (int index = 0; index < root.childCount; ++index)
            {
                Transform node = root.GetChild(index);
                children.Add(new ChildState { Node = node, Position = node.localPosition,
                    Rotation = node.localRotation, Generated = IsGenerated(node), Snap = node.CompareTag("snappoint"),
                    Active = node.gameObject.activeSelf });
            }
            ready = true;
        }

        private Transform Create(Transform parent, int index)
        {
            GameObject point = new GameObject(NamePrefix + index.ToString("00"))
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };
            point.transform.SetParent(parent, false);
            owned.Add(point);
            return point.transform;
        }

        public static void RemoveAll()
        {
            // Teardown only: includes never-active/unqueued clones without rooting them in a static registry.
            foreach (var cache in Resources.FindObjectsOfTypeAll<VanillaMidpointSnapPoints>())
                if (cache)
                {
                    cache.ClearOwned();
                    UnityEngine.Object.Destroy(cache);
                }
        }

        private void ClearOwned()
        {
            removing = true;
            ready = false;
            if (owner != transform) { owned.Clear(); return; }
            foreach (GameObject point in owned)
            {
                if (!point) continue;
                point.tag = "Untagged";
                point.SetActive(false);
                // Do not let another query reuse a node awaiting deferred destruction.
                point.transform.SetParent(null, false);
                UnityEngine.Object.Destroy(point);
            }
            owned.Clear();
        }

        private void OnDestroy()
        {
            ClearOwned();
        }

        public static bool IsGenerated(Transform point) =>
            point && point.name.StartsWith(NamePrefix, StringComparison.Ordinal);

        internal static bool IsRootCache(MonoBehaviour component, GameObject root) =>
            component && root && component.GetType() == typeof(VanillaMidpointSnapPoints) &&
            component.gameObject == root && ReferenceEquals(root.GetComponent<VanillaMidpointSnapPoints>(), component);

        internal static bool IsOwned(Transform point)
        {
            if (!IsGenerated(point) || !point.parent) return false;
            var cache = point.parent.GetComponent<VanillaMidpointSnapPoints>();
            return IsRootCache(cache, point.parent.gameObject) && cache.owned.Contains(point.gameObject);
        }

        private static int FindPoint(IReadOnlyList<Point3> points, Point3 target)
        {
            for (int index = 0; index < points.Count; ++index)
            {
                if ((points[index] - target).LengthSquared < 0.00000001) return index;
            }
            return -1;
        }

        private static Point3 ToGeometry(Vector3 point) =>
            new Point3(point.x, point.y, point.z);

        private static Vector3 ToUnity(Point3 point) =>
            new Vector3((float)point.X, (float)point.Y, (float)point.Z);
    }
}
