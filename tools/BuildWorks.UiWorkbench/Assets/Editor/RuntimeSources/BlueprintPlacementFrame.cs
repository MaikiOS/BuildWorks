using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Converts the Hammer placement frame without rebasing stored part poses or
    // changing the separate bounds pivot used by the world precision editor.
    internal static class BlueprintPlacementFrame
    {
        internal static Point3[][] SnapLocalSets(CompositeBlueprintStore.Blueprint blueprint, IReadOnlyList<Piece> pieces)
        {
            var sets = new List<Point3>[blueprint.parts.Count];
            foreach (CompositeBlueprintStore.VectorData point in blueprint.customSnapPoints ??
                Enumerable.Empty<CompositeBlueprintStore.VectorData>())
            {
                Vector3 position = point.ToVector3();
                int nearest = 0;
                float distance = float.PositiveInfinity;
                for (int index = 0; index < blueprint.parts.Count; ++index)
                {
                    float candidate = (position - blueprint.parts[index].position.ToVector3()).sqrMagnitude;
                    if (candidate < distance) { nearest = index; distance = candidate; }
                }
                var part = blueprint.parts[nearest];
                Vector3 unscaled = Quaternion.Inverse(part.rotation.ToQuaternion()) * (position - part.position.ToVector3());
                Vector3 scale = Vector3.Scale(pieces[nearest].transform.localScale, part.scale.ToVector3());
                if (!(scale.x > 0) || !(scale.y > 0) || !(scale.z > 0) ||
                    float.IsInfinity(scale.x) || float.IsInfinity(scale.y) || float.IsInfinity(scale.z))
                    throw new ArgumentException("Invalid native prefab scale for snap points.");
                var local = new Point3(unscaled.x / scale.x, unscaled.y / scale.y, unscaled.z / scale.z);
                (sets[nearest] ?? (sets[nearest] = new List<Point3>())).Add(local);
            }
            Point3[][] result = sets.Select(set => set?.ToArray()).ToArray();
            foreach (Point3[] set in result)
                if (set != null) BlueprintSnapPointCodec.Encode(set);
            return result;
        }

        internal static bool TryResolve(CompositeBlueprintStore.Blueprint blueprint,
            out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (blueprint?.supports == null || blueprint.placementSupportId == null) return false;
            foreach (CompositeBlueprintStore.Support support in blueprint.supports)
            {
                if (support == null || !string.Equals(support.stableId,
                    blueprint.placementSupportId, StringComparison.Ordinal)) continue;
                position = support.position.ToVector3();
                rotation = support.rotation.ToQuaternion();
                return true;
            }
            return false;
        }

        internal static void ToRootPose(Vector3 framePosition, Quaternion frameRotation,
            Vector3 supportPosition, Quaternion supportRotation, float uniformScale,
            out Vector3 rootPosition, out Quaternion rootRotation)
        {
            rootRotation = frameRotation * Quaternion.Inverse(supportRotation);
            rootPosition = framePosition - rootRotation * (supportPosition * uniformScale);
        }

        internal static Vector3 ToLocal(Vector3 point, Vector3 origin, Quaternion orientation) =>
            Quaternion.Inverse(orientation) * (point - origin);

        internal static Bounds RebaseVisuals(Transform root, Bounds bounds,
            Vector3 origin, Quaternion orientation)
        {
            Quaternion inverse = Quaternion.Inverse(orientation);
            foreach (Transform child in root)
            {
                child.localPosition = inverse * (child.localPosition - origin);
                child.localRotation = inverse * child.localRotation;
            }
            Bounds result = default;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 point = new Vector3(
                    (corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                    (corner & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (corner & 4) == 0 ? bounds.min.z : bounds.max.z);
                Vector3 local = inverse * (point - origin);
                if (corner == 0) result = new Bounds(local, Vector3.zero);
                else result.Encapsulate(local);
            }
            return result;
        }
    }
}
