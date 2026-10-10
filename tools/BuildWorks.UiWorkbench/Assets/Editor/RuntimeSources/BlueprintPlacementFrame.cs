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
        internal static bool HasRigidParent(Transform transform)
        {
            if (!transform) return false;
            if (!transform.parent) return true;
            Matrix4x4 matrix = transform.parent.localToWorldMatrix;
            Vector3 x = matrix.GetColumn(0), y = matrix.GetColumn(1), z = matrix.GetColumn(2);
            Quaternion rotation = transform.parent.rotation;
            const float toleranceSquared = 0.0000000001f;
            return (x - rotation * Vector3.right).sqrMagnitude <= toleranceSquared &&
                (y - rotation * Vector3.up).sqrMagnitude <= toleranceSquared &&
                (z - rotation * Vector3.forward).sqrMagnitude <= toleranceSquared;
        }

        internal static IEnumerable<Vector3> HammerSnapPoints(
            CompositeBlueprintStore.Blueprint blueprint, IReadOnlyList<Piece> pieces,
            Bounds bounds, Vector3 origin, Quaternion orientation)
        {
            IReadOnlyList<Vector3> native = NativeSnapPoints(blueprint, pieces);
            // Envelope corners are not sockets of rotated pieces.
            if (native.Count == 0 && (blueprint.customSnapPoints == null || blueprint.customSnapPoints.Count == 0))
            {
                AnchorBounds anchors = AnchorAdjustment.CreateBounds(new[] {
                    new Point3(bounds.min.x, bounds.min.y, bounds.min.z),
                    new Point3(bounds.max.x, bounds.max.y, bounds.max.z) });
                for (int index = 0; index < AnchorAdjustment.AnchorCount; ++index)
                {
                    Point3 point = anchors.Anchor(index);
                    yield return ToLocal(new Vector3((float)point.X, (float)point.Y, (float)point.Z), origin, orientation);
                }
            }
            foreach (Vector3 point in native) yield return ToLocal(point, origin, orientation);
            foreach (CompositeBlueprintStore.VectorData point in blueprint.customSnapPoints ??
                Enumerable.Empty<CompositeBlueprintStore.VectorData>())
                yield return ToLocal(point.ToVector3(), origin, orientation);
        }

        internal static Quaternion AutoJoinRotation(Quaternion target, Quaternion ghost, bool blueprint)
            => blueprint ? ghost : target * ghost;

        internal static IReadOnlyList<Vector3> NativeSnapPoints(
            CompositeBlueprintStore.Blueprint blueprint,
            IReadOnlyList<Piece> pieces)
        {
            var sets = new List<IReadOnlyList<Point3>>();
            bool explicitFrame = CompositeBlueprintStore.HasExplicitFrame(blueprint);
            for (int index = 0; index < pieces.Count; ++index)
            {
                if (explicitFrame && !CompositeBlueprintStore.IsFramePart(blueprint, index))
                    continue;
                Piece piece = pieces[index];
                CompositeBlueprintStore.Part part = blueprint.parts[index];
                var native = new List<Transform>();
                piece.GetSnapPoints(native);
                var local = new List<Point3>(native.Count);
                foreach (Transform snap in native)
                {
                    if (!snap) continue;
                    Vector3 pieceLocal = piece.transform.InverseTransformPoint(snap.position);
                    Vector3 point = part.position.ToVector3() + part.rotation.ToQuaternion() *
                        Vector3.Scale(pieceLocal, part.RootScale(piece.transform.localScale));
                    if (float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                        float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                        float.IsNaN(point.z) || float.IsInfinity(point.z)) continue;
                    local.Add(new Point3(point.x, point.y, point.z));
                }
                sets.Add(local);
            }
            IReadOnlyList<Point3> selected = explicitFrame && sets.Count == 1
                ? sets[0]
                : AnchorAdjustment.ExternalCompositeSnapPoints(sets, tolerance: 0.0001);
            var result = new List<Vector3>(selected.Count);
            foreach (Point3 point in selected)
            {
                if (result.Count == 512) break;
                result.Add(new Vector3((float)point.X, (float)point.Y, (float)point.Z));
            }
            return result;
        }

        internal static Point3[][] SnapLocalSets(CompositeBlueprintStore.Blueprint blueprint, IReadOnlyList<Piece> pieces)
        {
            var sets = new List<Point3>[blueprint.parts.Count];
            var points = blueprint.customSnapPoints;
            BlueprintNearestPartIndex origins = points?.Count > 0
                ? new BlueprintNearestPartIndex(blueprint.parts.Select(part =>
                    new Point3(part.position.x, part.position.y, part.position.z)).ToArray())
                : null;
            foreach (CompositeBlueprintStore.VectorData point in points ??
                Enumerable.Empty<CompositeBlueprintStore.VectorData>())
            {
                Vector3 position = point.ToVector3();
                int nearest = origins.FindNearest(new Point3(point.x, point.y, point.z));
                var part = blueprint.parts[nearest];
                Vector3 unscaled = Quaternion.Inverse(part.rotation.ToQuaternion()) * (position - part.position.ToVector3());
                Vector3 scale = part.RootScale(pieces[nearest].transform.localScale);
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
