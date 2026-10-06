using System;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Converts the Hammer placement frame without rebasing stored part poses or
    // changing the separate bounds pivot used by the world precision editor.
    internal static class BlueprintPlacementFrame
    {
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
