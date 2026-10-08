using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintPrefabMetadataCapture
    {
        internal static CompositeBlueprintStore.PrefabMetadata Capture(GameObject prefab)
        {
            if (!prefab) return null;
            Piece piece = prefab.GetComponent<Piece>();
            string name = piece && !string.IsNullOrWhiteSpace(piece.m_name) ? piece.m_name : prefab.name;
            if (Localization.instance != null) name = Localization.instance.Localize(name);
            bool boundsKnown = false;
            Bounds bounds = default;
            // ponytail: skinned envelopes stay unknown until a validated native bounds adapter exists.
            if (prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0)
                boundsKnown = TryGetLocalVisualBounds(prefab, out bounds, includeInactiveRoot: true);
            try
            {
                return CompositeBlueprintStore.PrefabMetadata.FromGeometry(new Geometry.BlueprintPrefabMetadata(
                    1, prefab.name, name, piece ? "Piece" : "WorldObject",
                    typeof(Piece).Assembly.ManifestModule.ModuleVersionId.ToString("N"), Point(prefab.transform.localScale),
                    boundsKnown ? Point(bounds.min) : (Geometry.Point3?)null,
                    boundsKnown ? Point(bounds.max) : (Geometry.Point3?)null));
            }
            catch (ArgumentException error)
            {
                Debug.LogWarning("BuildWorks prefab metadata unavailable for " + prefab.name + ": " + error.Message);
                return null;
            }
        }

        private static Geometry.Point3 Point(Vector3 value) => new Geometry.Point3(value.x, value.y, value.z);

        internal static bool TryGetLocalVisualBounds(GameObject root, out Bounds bounds, bool includeInactiveRoot = false)
        {
            bounds = default;
            bool found = false;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Renderer renderer = filter ? filter.GetComponent<Renderer>() : null;
                if (!filter || !filter.sharedMesh || !renderer || !renderer.enabled ||
                    !IsActiveWithin(root.transform, filter.transform, includeInactiveRoot)) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = new Vector3(
                        (corner & 1) == 0 ? meshBounds.min.x : meshBounds.max.x,
                        (corner & 2) == 0 ? meshBounds.min.y : meshBounds.max.y,
                        (corner & 4) == 0 ? meshBounds.min.z : meshBounds.max.z);
                    Vector3 local = root.transform.InverseTransformPoint(filter.transform.TransformPoint(point));
                    if (!found) { bounds = new Bounds(local, Vector3.zero); found = true; }
                    else bounds.Encapsulate(local);
                }
            }
            return found;
        }

        private static bool IsActiveWithin(Transform root, Transform child, bool includeInactiveRoot)
        {
            for (Transform current = child; current; current = current.parent)
            {
                if (current == root && includeInactiveRoot) return true;
                if (!current.gameObject.activeSelf) return false;
                if (current == root) return true;
            }
            return false;
        }
    }
}
