using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal enum PlacementContactSides { All, Below, Above, NonFloor }
    // Only an exact NoContact result is a clean negative; extra flags mean uncertainty or another rejection.
    [System.Flags]
    internal enum PlacementContactFailure
    { None = 0, NoContact = 1, OutOfRange = 2, Obstructed = 4, UnknownGeometry = 8, InvalidQuery = 16 }

    // Only locates the contact. Piece flags, permissions and placement status
    // remain the responsibility of Player.UpdatePlacementGhost.
    internal static class BuildWorksPlacementValidation
    {
        internal static bool UsesSurfaceAttachment(Piece.UsageTagFlags usage)
        {
            const Piece.UsageTagFlags structural = Piece.UsageTagFlags.Building | Piece.UsageTagFlags.Floor |
                Piece.UsageTagFlags.Wall | Piece.UsageTagFlags.Roof | Piece.UsageTagFlags.Architecture |
                Piece.UsageTagFlags.Stairs | Piece.UsageTagFlags.Doors;
            const Piece.UsageTagFlags functional = Piece.UsageTagFlags.Crafting | Piece.UsageTagFlags.Furniture |
                Piece.UsageTagFlags.Storage;
            // Structural tags win over mixed tags; UI category and native donor strength do not grant attachment.
            return (usage & structural) == 0 && (usage & functional) != 0;
        }

        internal static bool IsSurfaceAttachmentSupport(Collider collider)
        {
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy ||
                collider.isTrigger || collider.attachedRigidbody || collider.GetComponentInParent<Container>()) return false;
            // Native donor flags also cover older building pieces without usage tags.
            WearNTear support = collider.GetComponentInParent<WearNTear>();
            if (!support) return true;
            ZNetView view = support.GetComponent<ZNetView>();
            return support.m_supports && view && view.IsValid();
        }

        private static readonly RaycastHit[] hits = new RaycastHit[32];
        internal const float SurfaceContactTolerance = .002f;

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
            => TryFindContact(ghost, bounds, layerMask, eye, maximumDistance, sides, out contact, out _);

        internal static bool TryFindContact(GameObject ghost, Bounds bounds, int layerMask,
            Vector3 eye, float maximumDistance, PlacementContactSides sides, out RaycastHit contact,
            out PlacementContactFailure failure, bool surfaceAttachment = false)
        {
            contact = default;
            failure = PlacementContactFailure.InvalidQuery;
            if (!ghost || maximumDistance <= 0f || float.IsNaN(maximumDistance) ||
                float.IsInfinity(maximumDistance) || !Finite(eye) || layerMask == 0) return false;
            failure = PlacementContactFailure.NoContact;
            bool found = false;
            float bestGap = float.PositiveInfinity;
            bool knownShape = false;
            bool unhandledShape = false;
            bool invalidShape = false;
            // shortcut: a mesh envelope miss cannot establish missing contact; add exact mesh probes before deferring it.
            bool uncertainGeometry = false;
            bool nativeSupport = !surfaceAttachment && ghost.GetComponent<WearNTear>();
            Collider[] colliders = ghost.GetComponentsInChildren<Collider>(true);
            foreach (Collider collider in colliders)
            {
                if (!UsableCollider(collider, ghost.transform)) continue;
                // shortcut: non-box attachment needs an exact shape adapter; bounds/visuals cannot prove its contact.
                if (surfaceAttachment && !(collider is BoxCollider)) { unhandledShape = true; continue; }
                if (!(collider is BoxCollider) && !(collider is MeshCollider))
                { unhandledShape = true; continue; }
                knownShape = true;
                uncertainGeometry |= collider is MeshCollider;
                if (!TryGetColliderBounds(collider, out Bounds shape, out Quaternion rotation))
                { invalidShape = true; continue; }
                uncertainGeometry |= shape.size.x <= 0f || shape.size.y <= 0f || shape.size.z <= 0f;
                // WearNTear.SetupColliders expands each real shape by .15m per side.
                float margin = surfaceAttachment ? SurfaceContactTolerance : nativeSupport ? .15f : ContactTolerance(shape);
                FindShapeContact(ghost, shape, rotation, margin, layerMask, eye, maximumDistance,
                    sides, ref found, ref bestGap, ref contact, ref failure, surfaceAttachment: surfaceAttachment);
            }
            if (surfaceAttachment && (!knownShape || unhandledShape || invalidShape || uncertainGeometry))
            {
                contact = default;
                failure |= PlacementContactFailure.UnknownGeometry;
                return false;
            }
            // shortcut: collider-less or any unhandled shape retains the whole prefab's strict visual path;
            // add a shape adapter after its inactive/nonuniform native bounds are verified.
            if (unhandledShape)
            {
                contact = default;
                if (invalidShape) { failure |= PlacementContactFailure.UnknownGeometry; return false; }
                found = false;
            }
            if (!knownShape || unhandledShape)
                FindShapeContact(ghost, bounds, Quaternion.identity, ContactTolerance(bounds), layerMask,
                    eye, maximumDistance, sides, ref found, ref bestGap, ref contact, ref failure);
            // Preserve an existing ray contact/normal before trying additional box surfaces.
            if (!found && knownShape && !unhandledShape)
                foreach (Collider collider in colliders)
                {
                    if (!(collider is BoxCollider) || !UsableCollider(collider, ghost.transform) ||
                        !TryGetColliderBounds(collider, out Bounds shape, out Quaternion rotation) ||
                        shape.size.x <= 0f || shape.size.y <= 0f || shape.size.z <= 0f) continue;
                    FindShapeContact(ghost, shape, rotation,
                        surfaceAttachment ? SurfaceContactTolerance : nativeSupport ? .15f : ContactTolerance(shape),
                        layerMask, eye, maximumDistance, sides, ref found, ref bestGap, ref contact, ref failure,
                        sweepBox: true, surfaceAttachment: surfaceAttachment);
                }
            if (!found && nativeSupport && sides == PlacementContactSides.All && knownShape && !unhandledShape)
                foreach (Collider collider in colliders)
                {
                    if (!UsableCollider(collider, ghost.transform) || !collider.gameObject.activeInHierarchy ||
                        !TryGetColliderBounds(collider, out Bounds shape, out Quaternion rotation)) continue;
                    // Native WearNTear uses the live world bounds for non-box colliders.
                    if (collider is MeshCollider) { shape = collider.bounds; rotation = Quaternion.identity; }
                    if (!ValidBounds(shape) || shape.size.x <= 0f || shape.size.y <= 0f || shape.size.z <= 0f) continue;
                    if (!TryFindNativeEnvelopeContact(ghost, shape, rotation, layerMask, eye,
                        maximumDistance, out contact, ref failure)) continue;
                    found = true;
                    break;
                }
            if (found) failure = PlacementContactFailure.None;
            else if (!knownShape || unhandledShape || invalidShape || uncertainGeometry)
                failure |= PlacementContactFailure.UnknownGeometry;
            return found;
        }

        internal static bool UsableCollider(Collider collider, Transform root)
        {
            if (!collider || !collider.enabled || collider.isTrigger || collider.attachedRigidbody) return false;
            for (Transform node = collider.transform; node != root; node = node.parent)
                if (!node || !node.gameObject.activeSelf) return false;
            return true;
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
            ref bool found, ref float bestGap, ref RaycastHit contact, ref PlacementContactFailure failure,
            bool sweepBox = false, bool surfaceAttachment = false)
        {
            bool nativeSupport = !surfaceAttachment && ghost.GetComponent<WearNTear>();
            Bounds bounds = new Bounds(shape.center, Vector3.zero);
            QueryTriggerInteraction triggers = surfaceAttachment ? QueryTriggerInteraction.Ignore : QueryTriggerInteraction.UseGlobal;
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
                for (int sample = 0; sample < (sweepBox ? 1 : 9); ++sample)
                {
                    BuildProbe(bounds, face, sample, margin, out Ray ray, out float reach, out _);
                    float extent;
                    int count;
                    if (sweepBox)
                    {
                        // Sweep the whole box: sparse rays miss narrow supports and pitched corners.
                        extent = margin + .001f;
                        count = Physics.BoxCastNonAlloc(shape.center - ray.direction * extent, shape.extents,
                            ray.direction, hits, rotation, extent * 2f, layerMask, triggers);
                    }
                    else
                    {
                        if (!TryShapeBoundary(shape, inverse, ray, out extent)) continue;
                        count = Physics.RaycastNonAlloc(ray, hits, reach, layerMask, triggers);
                    }
                    // Dense compound colliders must not silently truncate a closer hit.
                    RaycastHit[] candidates = count == hits.Length
                        ? (sweepBox ? Physics.BoxCastAll(shape.center - ray.direction * extent, shape.extents,
                            ray.direction, rotation, extent * 2f, layerMask, triggers)
                            : Physics.RaycastAll(ray, reach, layerMask, triggers))
                        : hits;
                    if (candidates != hits) count = candidates.Length;
                    // Same native exclusions as PieceRayTest; do not look through
                    // a rigidbody to claim the static surface behind it.
                    if (!TryFirstHit(ghost, candidates, count, out RaycastHit first,
                        ignoreTriggers: surfaceAttachment, attachmentSupportOnly: surfaceAttachment)) continue;
                    if (sweepBox && first.distance <= 0f)
                    { failure |= PlacementContactFailure.Obstructed; continue; }
                    if (first.collider.attachedRigidbody)
                    { failure |= PlacementContactFailure.Obstructed; continue; }
                    WearNTear support = first.collider.GetComponentInParent<WearNTear>();
                    if (nativeSupport && support && !support.m_supports) continue;
                    bool outOfRange = Vector3.Distance(eye, first.point) >= maximumDistance;
                    float gap = Mathf.Abs(first.distance - extent);
                    if (gap > margin) continue;
                    if (outOfRange) { failure |= PlacementContactFailure.OutOfRange; continue; }
                    if (!found || gap < bestGap)
                    {
                        found = true;
                        bestGap = gap;
                        contact = first;
                    }
                }
            }
        }

        internal static bool TryFirstHit(GameObject ghost, RaycastHit[] candidates, int count, out RaycastHit first,
            bool ignoreTriggers = false, bool attachmentSupportOnly = false)
        {
            bool found = false;
            first = default;
            for (int index = 0; index < count; ++index)
            {
                RaycastHit hit = candidates[index];
                if (!hit.collider || hit.collider.transform.IsChildOf(ghost.transform) || ignoreTriggers && hit.collider.isTrigger) continue;
                // Non-supporting furniture may overlap, but cannot hide the actual supporting surface.
                if (attachmentSupportOnly && !hit.collider.attachedRigidbody && !IsSurfaceAttachmentSupport(hit.collider)) continue;
                if (!found || hit.distance < first.distance) { first = hit; found = true; }
            }
            return found;
        }

        private static bool TryFindNativeEnvelopeContact(GameObject ghost, Bounds shape, Quaternion rotation,
            int mask, Vector3 eye, float maximumDistance, out RaycastHit contact, ref PlacementContactFailure failure)
        {
            contact = default;
            Bounds expanded = new Bounds(Vector3.zero, shape.size + Vector3.one * .3f);
            Quaternion inverse = Quaternion.Inverse(rotation);
            Collider[] overlaps = Physics.OverlapBox(shape.center, expanded.extents, rotation,
                mask, QueryTriggerInteraction.Ignore);
            // A ray originating inside a rigidbody cannot report that blocker's surface.
            // shortcut: any rigidbody in this envelope stops fallback; narrow after a recorded valid RB-adjacent case.
            foreach (Collider obstacle in overlaps)
                if (obstacle && !obstacle.transform.IsChildOf(ghost.transform) && obstacle.attachedRigidbody)
                { failure |= PlacementContactFailure.Obstructed; return false; }
            foreach (Collider candidate in overlaps)
            {
                if (!candidate || candidate.transform.IsChildOf(ghost.transform)) continue;
                WearNTear support = candidate.GetComponentInParent<WearNTear>();
                if (support && !support.m_supports) continue;
                // shortcut: nonconvex supports retain the existing rays; add a surface query when a recorded case needs it.
                if (candidate is MeshCollider mesh && !mesh.convex)
                { failure |= PlacementContactFailure.UnknownGeometry; continue; }
                for (int sample = 0; sample < 9; ++sample)
                {
                    int corner = sample - 1;
                    Vector3 origin = sample == 0 ? shape.center : shape.center + rotation * new Vector3(
                        (corner & 1) == 0 ? -shape.extents.x : shape.extents.x,
                        (corner & 2) == 0 ? -shape.extents.y : shape.extents.y,
                        (corner & 4) == 0 ? -shape.extents.z : shape.extents.z);
                    Vector3 delta = candidate.ClosestPoint(origin) - origin;
                    float length = delta.magnitude;
                    if (length < .000001f) continue;
                    Ray ray = new Ray(origin, delta / length);
                    int count = Physics.RaycastNonAlloc(ray, hits, length + .001f, mask, QueryTriggerInteraction.Ignore);
                    RaycastHit[] candidates = count == hits.Length
                        ? Physics.RaycastAll(ray, length + .001f, mask, QueryTriggerInteraction.Ignore) : hits;
                    if (candidates != hits) count = candidates.Length;
                    if (!TryFirstHit(ghost, candidates, count, out RaycastHit first) || first.collider != candidate) continue;
                    if (!expanded.Contains(inverse * (first.point - shape.center))) continue;
                    if (Vector3.Distance(eye, first.point) >= maximumDistance)
                    { failure |= PlacementContactFailure.OutOfRange; continue; }
                    contact = first;
                    return true;
                }
            }
            return false;
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
