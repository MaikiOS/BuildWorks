using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class VanillaMidpointSnapPoints
    {
        private const string NamePrefix = "BuildWorks_MidSnap_";
        private const int MaximumGeneratedPoints = 32;
        private static readonly HashSet<GameObject> Created = new HashSet<GameObject>();

        public static void Append(Piece piece, List<Transform> result)
        {
            if (!piece || result == null || piece.gameObject.name.StartsWith(
                HammerBlueprintPieceRegistry.PrefabPrefix,
                StringComparison.Ordinal)) return;

            List<Transform> native = new List<Transform>();
            List<Transform> generated = new List<Transform>();
            for (int index = 0; index < piece.transform.childCount; ++index)
            {
                Transform child = piece.transform.GetChild(index);
                if (IsGenerated(child))
                    generated.Add(child);
                else if (child.CompareTag("snappoint"))
                    native.Add(child);
            }
            if (native.Count < 2) return;

            Point3[] positions = new Point3[native.Count];
            for (int index = 0; index < native.Count; ++index)
                positions[index] = ToGeometry(native[index].localPosition);
            IReadOnlyList<Edge3> edges = AnchorAdjustment.ConnectableSnapEdges(positions);
            int count = Math.Min(edges.Count, MaximumGeneratedPoints);
            for (int index = 0; index < count; ++index)
            {
                Transform midpoint = index < generated.Count
                    ? generated[index]
                    : Create(piece.transform, index);
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
                if (!result.Contains(midpoint)) result.Add(midpoint);
            }
            for (int index = count; index < generated.Count; ++index)
            {
                result.Remove(generated[index]);
                generated[index].gameObject.tag = "Untagged";
                generated[index].gameObject.SetActive(false);
            }
        }

        private static Transform Create(Transform parent, int index)
        {
            GameObject point = new GameObject(NamePrefix + index.ToString("00"))
            {
                hideFlags = HideFlags.HideInHierarchy | HideFlags.DontSave
            };
            point.transform.SetParent(parent, false);
            Created.Add(point);
            return point.transform;
        }

        public static void RemoveAll()
        {
            foreach (GameObject point in Created)
            {
                if (point) UnityEngine.Object.Destroy(point);
            }
            Created.Clear();
        }

        public static bool IsGenerated(Transform point) =>
            point && point.name.StartsWith(NamePrefix, StringComparison.Ordinal);

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
