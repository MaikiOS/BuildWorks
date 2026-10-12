#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksTestPlacementDiagnostics
    {
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
