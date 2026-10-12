#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksTestPlacementDiagnostics
    {
#if BUILDWORKS_TEST_STAND
        private static Transform nativeSource, nativeTarget, chosenSource, chosenTarget;
        private static Piece aimedPiece, contactSource;
        private static GameObject contactGhost;
        private static Collider contactCollider;
        private static Vector3 nativeSourcePosition, nativeTargetPosition, chosenSourcePosition,
            chosenTargetPosition, contactPoint, contactNormal;
        private static int snapFrame = -1, contactFrame = -1;
        private static bool snapAccepted;
        private static PlacementContactSides contactSides;

        internal static void Reset()
        {
            snapFrame = contactFrame = -1;
            nativeSource = nativeTarget = chosenSource = chosenTarget = null;
            aimedPiece = contactSource = null; contactGhost = null; contactCollider = null;
        }

        // Keep the latest observation only; formatting happens on a QA reply, never per placement frame.
        internal static void BeginSnap(Transform source, Transform target, Piece aimed)
        {
            snapFrame = Time.frameCount;
            nativeSource = source; nativeTarget = target; aimedPiece = aimed;
            nativeSourcePosition = source ? source.position : Vector3.zero;
            nativeTargetPosition = target ? target.position : Vector3.zero;
        }

        internal static void EndSnap(Transform source, Transform target, bool accepted)
        {
            chosenSource = source; chosenTarget = target; snapAccepted = accepted;
            chosenSourcePosition = source ? source.position : Vector3.zero;
            chosenTargetPosition = target ? target.position : Vector3.zero;
        }

        internal static string DescribeLastSnap() => snapFrame < 0 ? "No native pair observed." :
            "frame=" + snapFrame + " accepted=" + snapAccepted + " aimed=" + Label(aimedPiece) +
            " nativeSource=" + Label(nativeSource) + "@" + Vector(nativeSourcePosition) +
            " nativeTarget=" + Label(nativeTarget) + "@" + Vector(nativeTargetPosition) +
            " nativeTargetOwner=" + Label(nativeTarget ? nativeTarget.GetComponentInParent<Piece>() : null) +
            " chosenSource=" + Label(chosenSource) + " afterHook=" + Vector(chosenSourcePosition) +
            " chosenTarget=" + Label(chosenTarget) + " afterHook=" + Vector(chosenTargetPosition) +
            " chosenTargetOwner=" + Label(chosenTarget ? chosenTarget.GetComponentInParent<Piece>() : null) +
            " liveSource=" + (chosenSource ? Vector(chosenSource.position) : "gone") +
            " liveTarget=" + (chosenTarget ? Vector(chosenTarget.position) : "gone");

        internal static void RecordContact(GameObject ghost, Piece source, RaycastHit contact, PlacementContactSides sides)
        {
            contactFrame = Time.frameCount; contactGhost = ghost; contactSource = source;
            contactCollider = contact.collider; contactPoint = contact.point; contactNormal = contact.normal;
            contactSides = sides;
        }

        internal static string DescribeLastContact() => contactFrame < 0 ? "No accepted contact observed." :
            "frame=" + contactFrame + " ghost=" + Label(contactGhost) + " source=" + Label(contactSource) +
            " collider=" + Label(contactCollider) + " point=" + Vector(contactPoint) + " normal=" + Vector(contactNormal) +
            " sides=" + contactSides + (contactSource ? " notOnTilting=" + contactSource.m_notOnTiltingSurface +
            " ceilingOnly=" + contactSource.m_inCeilingOnly + " notOnFloor=" + contactSource.m_notOnFloor +
            " noClipping=" + contactSource.m_noClipping : " sourceGone");

        internal static string DescribeInventory(Player player)
        {
            var items = player.GetInventory().GetAllItems();
            var result = new StringBuilder("frame=" + Time.frameCount + " stacks=" + items.Count);
            for (int index = 0; index < items.Count && index < 64; ++index)
            {
                var item = items[index];
                result.Append('\n').Append(index).Append(':').Append(Label(item?.m_dropPrefab))
                    .Append(" count=").Append(item?.m_stack);
            }
            if (items.Count > 64) result.Append("\ntruncated after 64 stacks");
            return result.ToString();
        }

        private static string Label(Object value)
        {
            if (!value) return "gone";
            string name = value.name ?? string.Empty;
            return (name.Length > 32 ? name.Substring(0, 32) + "..." : name) + "#" + value.GetInstanceID();
        }
#endif
        // Longer centre probes explain a refusal; they never supply a placement contact.
        internal static string Describe(GameObject ghost, Bounds bounds, int mask, Vector3 eye,
            float maximumDistance, PlacementContactSides sides, bool nativeHit)
        {
            var result = new StringBuilder("BuildWorks QA contact refused; diagnostic centre probes only.");
            result.Append(" prefab=").Append(ghost.name).Append(" position=").Append(Vector(ghost.transform.position))
                .Append(" scale=").Append(Vector(ghost.transform.lossyScale))
                .Append(" boundsMin=").Append(Vector(bounds.min)).Append(" boundsMax=").Append(Vector(bounds.max))
                .Append(" eye=").Append(Vector(eye)).Append(" maxDistance=").Append(Number(maximumDistance))
                .Append(" tolerance=").Append(Number(BuildWorksPlacementValidation.ContactTolerance(bounds)))
                .Append(" mask=").Append(mask).Append(" sides=").Append(sides).Append(" nativeHit=").Append(nativeHit);
            var hits = new RaycastHit[32];
            for (int face = 0; face < 6; ++face)
            {
                BuildWorksPlacementValidation.BuildProbe(bounds, face, 4, out Ray ray, out float reach, out float extent);
                int count = Physics.RaycastNonAlloc(ray, hits, reach + .25f, mask, QueryTriggerInteraction.UseGlobal);
                bool found = false;
                RaycastHit first = default;
                for (int index = 0; index < count; ++index)
                {
                    RaycastHit hit = hits[index];
                    if (!hit.collider || hit.collider.transform.IsChildOf(ghost.transform)) continue;
                    if (!found || hit.distance < first.distance) { first = hit; found = true; }
                }
                result.Append("\nface=").Append(face).Append(" hits=").Append(count)
                    .Append(" truncated=").Append(count == hits.Length);
                if (!found) { result.Append(" no-external-hit"); continue; }
                Transform surface = first.collider.transform;
                result.Append(" collider=").Append(surface.name).Append(" root=").Append(surface.root.name)
                    .Append(" point=").Append(Vector(first.point)).Append(" normal=").Append(Vector(first.normal))
                    .Append(" gap=").Append(Number(Mathf.Abs(first.distance - extent)))
                    .Append(" eyeDistance=").Append(Number(Vector3.Distance(eye, first.point)))
                    .Append(" rigidbody=").Append((bool)first.collider.attachedRigidbody)
                    .Append(" colliderMin=").Append(Vector(first.collider.bounds.min))
                    .Append(" colliderMax=").Append(Vector(first.collider.bounds.max))
                    .Append(" surfacePosition=").Append(Vector(surface.position));
            }
            return result.ToString();
        }

        private static string Number(float value) => value.ToString("G9", CultureInfo.InvariantCulture);
        private static string Vector(Vector3 value) => "(" + Number(value.x) + "," + Number(value.y) + "," + Number(value.z) + ")";
    }
}
#endif
