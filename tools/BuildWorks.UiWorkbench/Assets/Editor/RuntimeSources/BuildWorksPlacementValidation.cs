using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal enum PlacementContactSides { All, Below, Above, NonFloor }

    // Only locates the contact. Piece flags, permissions and placement status
    // remain the responsibility of Player.UpdatePlacementGhost.
    internal static class BuildWorksPlacementValidation
    {
        private static readonly RaycastHit[] hits = new RaycastHit[32];

        internal static bool TryGetBounds(GameObject ghost, out Bounds bounds)
        {
            bounds = default;
            if (!ghost) return false;
            bool found = false;
            foreach (Renderer renderer in ghost.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer || !renderer.enabled) continue;
                bool visible = true;
                for (Transform node = renderer.transform; node != ghost.transform; node = node.parent)
                    if (!node || !node.gameObject.activeSelf) { visible = false; break; }
                if (!visible) continue;
                Bounds local = renderer.localBounds;
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = renderer.transform.TransformPoint(new Vector3(
                        (corner & 1) == 0 ? local.min.x : local.max.x,
                        (corner & 2) == 0 ? local.min.y : local.max.y,
                        (corner & 4) == 0 ? local.min.z : local.max.z));
                    if (found) bounds.Encapsulate(point);
                    else { bounds = new Bounds(point, Vector3.zero); found = true; }
                }
            }
            return found;
        }

        internal static float ContactTolerance(Bounds bounds) =>
            Mathf.Clamp(bounds.size.magnitude * 0.01f, 0.002f, 0.1f);

        internal static bool TryFindContact(GameObject ghost, Bounds bounds, int layerMask,
            Vector3 eye, float maximumDistance, PlacementContactSides sides, out RaycastHit contact)
        {
            contact = default;
            if (!ghost || maximumDistance <= 0f) return false;
            bool found = false;
            float bestGap = float.PositiveInfinity;
            bool knownShape = false;
            bool unhandledShape = false;
            bool invalidShape = false;
            bool nativeSupport = ghost.GetComponent<WearNTear>();
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
            {
                if (!collider || !collider.enabled || collider.isTrigger || collider.attachedRigidbody) continue;
                bool active = true;
                for (Transform node = collider.transform; node != ghost.transform; node = node.parent)
                    if (!node || !node.gameObject.activeSelf) { active = false; break; }
                if (!active) continue;
                if (!(collider is BoxCollider) && !(collider is MeshCollider))
                { unhandledShape = true; continue; }
                knownShape = true;
                if (!TryGetColliderBounds(collider, out Bounds shape, out Quaternion rotation))
                { invalidShape = true; continue; }
                // WearNTear.SetupColliders expands each real shape by .15m per side.
                float margin = nativeSupport ? .15f : ContactTolerance(shape);
                FindShapeContact(ghost, shape, rotation, margin, layerMask, eye, maximumDistance,
                    sides, ref found, ref bestGap, ref contact);
            }
            // shortcut: collider-less or any unhandled shape retains the whole prefab's strict visual path;
            // add a shape adapter after its inactive/nonuniform native bounds are verified.
            if (unhandledShape)
            {
                contact = default;
                if (invalidShape) return false;
                found = false;
            }
            if (!knownShape || unhandledShape)
                FindShapeContact(ghost, bounds, Quaternion.identity, ContactTolerance(bounds), layerMask,
                    eye, maximumDistance, sides, ref found, ref bestGap, ref contact);
            return found;
        }

        internal static bool TryGetColliderBounds(Collider collider, out Bounds bounds, out Quaternion rotation)
        {
            bounds = default;
            rotation = Quaternion.identity;
            if (collider is BoxCollider box)
            {
                Vector3 scale = box.transform.lossyScale;
                bounds = new Bounds(box.transform.TransformPoint(box.center), Vector3.Scale(box.size,
                    new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))));
                rotation = box.transform.rotation;
                return ValidBounds(bounds);
            }
            if (!(collider is MeshCollider mesh) || !mesh.sharedMesh) return false;
            Bounds local = mesh.sharedMesh.bounds;
            for (int corner = 0; corner < 8; ++corner)
            {
                Vector3 point = mesh.transform.TransformPoint(new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z));
                if (corner == 0) bounds = new Bounds(point, Vector3.zero);
                else bounds.Encapsulate(point);
            }
            return ValidBounds(bounds);
        }

        private static bool ValidBounds(Bounds bounds) => Finite(bounds.center) && Finite(bounds.size) &&
            bounds.size.x >= 0f && bounds.size.y >= 0f && bounds.size.z >= 0f && bounds.size.sqrMagnitude > 0f;

        private static bool Finite(Vector3 value) =>
            !float.IsNaN(value.x) && !float.IsInfinity(value.x) &&
            !float.IsNaN(value.y) && !float.IsInfinity(value.y) &&
            !float.IsNaN(value.z) && !float.IsInfinity(value.z);

        private static void FindShapeContact(GameObject ghost, Bounds shape, Quaternion rotation, float margin,
            int layerMask, Vector3 eye, float maximumDistance, PlacementContactSides sides,
            ref bool found, ref float bestGap, ref RaycastHit contact)
        {
            Bounds bounds = new Bounds(shape.center, Vector3.zero);
            for (int corner = 0; corner < 8; ++corner)
                bounds.Encapsulate(shape.center + rotation * new Vector3(
                    (corner & 1) == 0 ? -shape.extents.x : shape.extents.x,
                    (corner & 2) == 0 ? -shape.extents.y : shape.extents.y,
                    (corner & 4) == 0 ? -shape.extents.z : shape.extents.z));
            Quaternion inverse = Quaternion.Inverse(rotation);
            for (int face = 0; face < 6; ++face)
            {
                if (sides == PlacementContactSides.Below && face != 0 ||
                    sides == PlacementContactSides.Above && face != 1 ||
                    sides == PlacementContactSides.NonFloor && face == 0) continue;
                for (int sample = 0; sample < 9; ++sample)
                {
                    BuildProbe(bounds, face, sample, margin, out Ray ray, out float reach, out _);
                    if (!TryShapeBoundary(shape, inverse, ray, out float extent)) continue;
                    int count = Physics.RaycastNonAlloc(ray, hits, reach, layerMask,
                        QueryTriggerInteraction.UseGlobal);
                    // Dense compound colliders must not silently truncate a closer hit.
                    RaycastHit[] candidates = count == hits.Length
                        ? Physics.RaycastAll(ray, reach, layerMask, QueryTriggerInteraction.UseGlobal)
                        : hits;
                    if (candidates != hits) count = candidates.Length;
                    bool rayFound = false;
                    RaycastHit first = default;
                    for (int index = 0; index < count; ++index)
                    {
                        RaycastHit hit = candidates[index];
                        if (!hit.collider || hit.collider.transform.IsChildOf(ghost.transform)) continue;
                        if (!rayFound || hit.distance < first.distance) { first = hit; rayFound = true; }
                    }
                    // Same native exclusions as PieceRayTest; do not look through
                    // a rigidbody to claim the static surface behind it.
                    if (!rayFound || first.collider.attachedRigidbody ||
                        Vector3.Distance(eye, first.point) >= maximumDistance) continue;
                    float gap = Mathf.Abs(first.distance - extent);
                    if (gap > margin) continue;
                    if (!found || gap < bestGap)
                    {
                        found = true;
                        bestGap = gap;
                        contact = first;
                    }
                }
            }
        }

        private static bool TryShapeBoundary(Bounds shape, Quaternion inverse, Ray ray, out float extent)
        {
            Vector3 origin = inverse * (ray.origin - shape.center);
            Vector3 direction = inverse * ray.direction;
            float entry = float.NegativeInfinity;
            extent = float.PositiveInfinity;
            for (int axis = 0; axis < 3; ++axis)
            {
                if (Mathf.Abs(direction[axis]) < .000001f)
                {
                    if (Mathf.Abs(origin[axis]) > shape.extents[axis]) return false;
                    continue;
                }
                float first = (-shape.extents[axis] - origin[axis]) / direction[axis];
                float last = (shape.extents[axis] - origin[axis]) / direction[axis];
                entry = Mathf.Max(entry, Mathf.Min(first, last));
                extent = Mathf.Min(extent, Mathf.Max(first, last));
                if (entry > extent) return false;
            }
            return extent >= 0f && !float.IsInfinity(extent);
        }

        internal static void BuildProbe(Bounds bounds, int face, int sample,
            out Ray ray, out float reach, out float extent)
            => BuildProbe(bounds, face, sample, ContactTolerance(bounds), out ray, out reach, out extent);

        private static void BuildProbe(Bounds bounds, int face, int sample, float margin,
            out Ray ray, out float reach, out float extent)
        {
            int axis = face < 2 ? 1 : face < 4 ? 0 : 2;
            int first = (axis + 1) % 3;
            int second = (axis + 2) % 3;
            Vector3 direction = Vector3.zero;
            direction[axis] = (face & 1) == 0 ? -1f : 1f;
            Vector3 origin = bounds.center;
            origin[first] += (sample % 3 - 1) * bounds.extents[first] * 0.8f;
            origin[second] += (sample / 3 - 1) * bounds.extents[second] * 0.8f;
            extent = bounds.extents[axis];
            reach = extent + margin;
            ray = new Ray(origin, direction);
        }
    }
}
