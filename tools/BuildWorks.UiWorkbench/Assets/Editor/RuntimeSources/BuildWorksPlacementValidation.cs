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
            for (int face = 0; face < 6; ++face)
            {
                if (sides == PlacementContactSides.Below && face != 0 ||
                    sides == PlacementContactSides.Above && face != 1 ||
                    sides == PlacementContactSides.NonFloor && face == 0) continue;
                for (int sample = 0; sample < 9; ++sample)
                {
                    BuildProbe(bounds, face, sample, out Ray ray, out float reach, out float extent);
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
                    if (gap > ContactTolerance(bounds)) continue;
                    if (!found || gap < bestGap)
                    {
                        found = true;
                        bestGap = gap;
                        contact = first;
                    }
                }
            }
            return found;
        }

        internal static void BuildProbe(Bounds bounds, int face, int sample,
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
            reach = extent + ContactTolerance(bounds);
            ray = new Ray(origin, direction);
        }
    }
}
