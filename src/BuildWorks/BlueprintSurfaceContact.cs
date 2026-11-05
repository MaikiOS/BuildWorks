using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.LowLevelPhysics;

namespace OstrixMods.BuildWorks
{
    // Session-owned collider copies. Borrow native geometry only for one synchronous pair query.
    internal sealed class BlueprintSurfaceContact : IDisposable
    {
        private const float Tolerance = BuildWorksPlacementValidation.SurfaceContactTolerance;
        private static AsyncOperation pendingUnload;
        private BlueprintStructuralSnapshot snapshot;
        private BlueprintStructuralSnapshot.Part receiver;
        private bool disposed;
        private NativeArray<GeometryHolder> a, b;
        private NativeArray<ImmediateTransform> pa, pb;
        private NativeArray<ImmediateContact> contacts;
        private NativeArray<int> counts;
        private readonly Collider[] overlaps = new Collider[128];
        private readonly RaycastHit[] hits = new RaycastHit[32];
        private readonly BlueprintTriangleContact triangleContact = new BlueprintTriangleContact();
        internal AsyncOperation Unloading { get; private set; }

        internal static bool NeedsAdapter(GameObject ghost)
        {
            if (!ghost) return false;
            foreach (Collider collider in ghost.GetComponentsInChildren<Collider>(true))
                if (BuildWorksPlacementValidation.UsableCollider(collider, ghost.transform) && !(collider is BoxCollider)) return true;
            return false;
        }

        internal bool TryFind(GameObject ghost, int mask, Vector3 eye, float maximumDistance,
            out RaycastHit hit, out PlacementContactFailure failure)
        {
            triangleContact.Clear();
            try { return TryFindCore(ghost, mask, eye, maximumDistance, out hit, out failure); }
            finally { triangleContact.Clear(); }
        }

        private bool TryFindCore(GameObject ghost, int mask, Vector3 eye, float maximumDistance,
            out RaycastHit hit, out PlacementContactFailure failure)
        {
            hit = default; failure = PlacementContactFailure.InvalidQuery;
            if (disposed || !ghost || mask == 0 || !Finite(eye) || !Finite(maximumDistance) || maximumDistance <= 0) return false;
            failure = PlacementContactFailure.UnknownGeometry;
            if (!ValidTransform(ghost.transform) || (pendingUnload != null && !pendingUnload.isDone)) return false;
            Collider[] source = ghost.GetComponentsInChildren<Collider>(true);
            bool usable = false;
            foreach (Collider collider in source)
            {
                if (!BuildWorksPlacementValidation.UsableCollider(collider, ghost.transform) || BlueprintStructuralSnapshot.IsEmptyMesh(collider)) continue;
                if (!KnownShape(collider) || !ValidTransform(collider.transform)) return false;
                usable = true;
            }
            if (!usable) return false;
            EnsureResources();
            Vector3 origin = ghost.transform.position;
            if (!snapshot.Refresh(receiver, ghost, Vector3.zero, ghost.transform.rotation, ghost.transform.lossyScale))
            {
                Clear();
                receiver = snapshot.Add("surface", ghost, Vector3.zero, ghost.transform.rotation, ghost.transform.lossyScale);
            }
            if (!receiver.CompleteGeometry) return false;
            Physics.SyncTransforms();
            // Validate every usable receiver before a partial compound can grant attachment.
            foreach (Collider collider in receiver.Colliders)
                if (BuildWorksPlacementValidation.UsableCollider(collider, receiver.Root.transform) &&
                    !TryShape(collider, Vector3.zero, out _, out _)) return false;
            failure = PlacementContactFailure.NoContact;
            foreach (Collider collider in receiver.Colliders)
            {
                if (!BuildWorksPlacementValidation.UsableCollider(collider, receiver.Root.transform)) continue;
                Bounds bounds = collider.bounds;
                if (!Finite(bounds.center + origin) || !Finite(bounds.extents) || bounds.extents.sqrMagnitude <= 0)
                { failure |= PlacementContactFailure.UnknownGeometry; continue; }
                int count = Physics.OverlapBoxNonAlloc(bounds.center + origin, bounds.extents + Vector3.one * Tolerance,
                    overlaps, Quaternion.identity, mask, QueryTriggerInteraction.Ignore);
                if (count == overlaps.Length) { failure |= PlacementContactFailure.UnknownGeometry; continue; }
                for (int i = 0; i < count; ++i)
                {
                    Collider target = overlaps[i];
                    if (!target || !target.enabled || !target.gameObject.activeInHierarchy || target.isTrigger ||
                        target.transform.IsChildOf(ghost.transform)) continue;
                    if (!target.attachedRigidbody && !BuildWorksPlacementValidation.IsSurfaceAttachmentSupport(target)) continue;
                    if (!KnownShape(target) || !ValidTransform(target.transform))
                    { failure |= PlacementContactFailure.UnknownGeometry; continue; }
                    if (TryPair(ghost, collider, target, origin, mask, eye, maximumDistance, out hit, ref failure))
                    { failure = PlacementContactFailure.None; return true; }
                }
            }
            return false;
        }

        private bool TryPair(GameObject ghost, Collider source, Collider target, Vector3 origin, int mask,
            Vector3 eye, float maximumDistance, out RaycastHit hit, ref PlacementContactFailure failure)
        {
            hit = default;
            TraceHollow(ghost, "pair entered source=" + source.GetInstanceID() + " target=" + target.GetInstanceID());
            if (!TryShape(source, Vector3.zero, out GeometryHolder first, out ImmediateTransform firstPose) ||
                !TryShape(target, origin, out GeometryHolder second, out ImmediateTransform secondPose))
            { TraceHollow(ghost, "pair rejected shape"); failure |= PlacementContactFailure.UnknownGeometry; return false; }
            try
            {
                TraceHollow(ghost, "shape types=" + first.Type + "/" + second.Type + " poses=" + firstPose.Position + "/" + secondPose.Position);
                if (source is MeshCollider sourceMesh && !sourceMesh.convex && target is MeshCollider targetMesh && !targetMesh.convex)
                {
                    failure |= PlacementContactFailure.UnknownGeometry;
                    if (target.attachedRigidbody) { failure |= PlacementContactFailure.Obstructed; return false; }
                    foreach (BlueprintTriangleContact.NativePair pair in triangleContact.Collect(sourceMesh, targetMesh, origin))
                        if (Witness(ghost, target, pair.Target.point, pair.Target.normal, pair.Distance,
                            mask, eye, maximumDistance, out hit, ref failure)) return true;
                    return false;
                }
                a[0] = first; b[0] = second; pa[0] = firstPose; pb[0] = secondPose; counts[0] = 0;
                int total = ImmediatePhysics.GenerateContacts(a.AsReadOnly(), b.AsReadOnly(), pa.AsReadOnly(), pb.AsReadOnly(),
                    1, contacts, counts, Tolerance);
                if (total == contacts.Length && contacts.Length < 256)
                {
                    contacts.Dispose(); contacts = new NativeArray<ImmediateContact>(256, Allocator.Persistent); counts[0] = 0;
                    total = ImmediatePhysics.GenerateContacts(a.AsReadOnly(), b.AsReadOnly(), pa.AsReadOnly(), pb.AsReadOnly(),
                        1, contacts, counts, Tolerance);
                }
                if (total < 0 || total >= contacts.Length || counts[0] != total)
                { TraceHollow(ghost, "contact capacity/count rejected total=" + total + " count=" + counts[0]); failure |= PlacementContactFailure.UnknownGeometry; return false; }
                TraceHollow(ghost, "immediate total=" + total + " count=" + counts[0] + " capacity=" + contacts.Length);
                for (int i = 0; i < total; ++i)
                {
                    ImmediateContact value = contacts[i];
                    TraceHollow(ghost, "contact i=" + i + " point=" + value.Point + " normal=" + value.Normal + " separation=" + value.Separation.ToString("R"));
                    if (!Finite(value.Point) || !Finite(value.Normal) || !Finite(value.Separation) ||
                        Mathf.Abs(value.Normal.sqrMagnitude - 1f) > .01f)
                    { TraceHollow(ghost, "contact rejected finite/normal"); failure |= PlacementContactFailure.UnknownGeometry; continue; }
                    if (value.Separation > Tolerance) { TraceHollow(ghost, "contact rejected separation"); continue; }
                    if (-value.Separation > source.bounds.size.magnitude + target.bounds.size.magnitude + Tolerance)
                    { TraceHollow(ghost, "contact rejected depth"); failure |= PlacementContactFailure.UnknownGeometry; continue; }
                    if (Witness(ghost, target, value.Point + origin, value.Normal, value.Separation,
                        mask, eye, maximumDistance, out hit, ref failure))
                    {
#if UNITY_EDITOR
                        // Temporary acceptance trace, excluded from both plugin builds.
                        if (ghost.name == "AdapterEmptyMeshCompound")
                            Debug.Log("BUILDWORKS_EMPTY_ACCEPTED source=" + source.GetInstanceID() + ":" + source.GetType().Name +
                                " sourceBounds=" + source.bounds + " sourcePose=" + firstPose.Position.ToString("F6") + ":" + firstPose.Rotation +
                                " target=" + target.GetInstanceID() + " hit=" + hit.collider.GetInstanceID() +
                                " origin=" + origin.ToString("F6") + " contacts=" + total + " point=" + value.Point.ToString("F6") +
                                " normal=" + value.Normal.ToString("F6") + " separation=" + value.Separation.ToString("R") +
                                " hitPoint=" + hit.point.ToString("F6") + " hitNormal=" + hit.normal.ToString("F6"));
#endif
                        return true;
                    }
                }
                if (TryContainedTriangleTarget(ghost, source, target, origin, mask, eye, maximumDistance, out hit, ref failure)) return true;
                // shortcut: a mesh zero is not a proved negative for arbitrary meshes; extend the actual shape gate first.
                if (total == 0 && (source is MeshCollider || target is MeshCollider)) failure |= PlacementContactFailure.UnknownGeometry;
            }
            catch (InvalidOperationException) { TraceHollow(ghost, "catch InvalidOperationException"); failure |= PlacementContactFailure.UnknownGeometry; }
            catch (UnityException) { TraceHollow(ghost, "catch UnityException"); failure |= PlacementContactFailure.UnknownGeometry; }
            finally { a[0] = default; b[0] = default; }
            return false;
        }

        private bool TryContainedTriangleTarget(GameObject ghost, Collider source, Collider target, Vector3 origin,
            int mask, Vector3 eye, float maximumDistance, out RaycastHit hit, ref PlacementContactFailure failure)
        {
            hit = default;
            if (!(target is MeshCollider triangle) || triangle.convex ||
                !(source is BoxCollider || source is SphereCollider || source is CapsuleCollider || source is MeshCollider convex && convex.convex) ||
                !source.enabled || !source.gameObject.activeInHierarchy || !target.enabled || !target.gameObject.activeInHierarchy ||
                source.isTrigger || target.isTrigger) return false;
            if (target.attachedRigidbody) { failure |= PlacementContactFailure.Obstructed; return false; }
            Bounds bounds = target.bounds;
            float radius = bounds.extents.magnitude + Tolerance * 4;
            if (!Finite(bounds.center) || !Finite(radius) || radius <= 0 || !Finite(radius * 2)) return false;
            for (int probe = 0; probe < 6; ++probe)
            {
                if (!triangleContact.ReserveRaySlots(1)) break;
                Vector3 direction = probe < 2 ? Vector3.right : probe < 4 ? Vector3.up : Vector3.forward;
                if ((probe & 1) != 0) direction = -direction;
                var ray = new Ray(bounds.center - direction * radius, direction);
                if (!Finite(ray.origin) || !Finite(ray.direction)) continue;
                if (!target.Raycast(ray, out RaycastHit surface, radius * 2) || surface.collider != target ||
                    !Finite(surface.distance) || surface.distance < 0 || surface.distance > radius * 2 ||
                    !Finite(surface.point) || !Finite(surface.normal) || Mathf.Abs(surface.normal.sqrMagnitude - 1) > .01f) continue;
                var relative = BlueprintTriangleContact.ReconstructRayPoint(ray, surface.distance, origin);
                var point = new Vector3((float)relative.X, (float)relative.Y, (float)relative.Z);
                if (!Finite(point)) continue;
                // Bounds choose rays only. A real target surface must be inside the actual enabled convex source.
                Vector3 closest = source.ClosestPoint(point);
                if (!Finite(closest) || closest.x != point.x || closest.y != point.y || closest.z != point.z) continue;
                if (!triangleContact.ReserveRaySlots(2)) break;
                TraceHollow(ghost, "native containment point=" + point + " closest=" + closest + " target=" + target.GetInstanceID());
                if (Witness(ghost, target, surface.point, surface.normal, 0, mask, eye, maximumDistance, out hit, ref failure)) return true;
            }
            failure |= PlacementContactFailure.UnknownGeometry;
            return false;
        }

        private bool Witness(GameObject ghost, Collider target, Vector3 point, Vector3 normal, float separation,
            int mask, Vector3 eye, float maximumDistance, out RaycastHit hit, ref PlacementContactFailure failure)
        {
            hit = default;
            float reach = Mathf.Abs(separation) + Tolerance * 2;
            if (!Finite(reach)) { failure |= PlacementContactFailure.UnknownGeometry; return false; }
            for (int side = 0; side < 2; ++side)
            {
                float sign = side == 0 ? 1f : -1f;
                Vector3 direction = normal * sign;
                int count = Physics.RaycastNonAlloc(point - direction * reach, direction, hits, reach * 2, mask, QueryTriggerInteraction.Ignore);
                TraceHollow(ghost, "world side=" + side + " count=" + count + " start=" + (point - direction * reach) + " direction=" + direction + " reach=" + reach.ToString("R"));
                if (count == hits.Length) { failure |= PlacementContactFailure.UnknownGeometry; continue; }
                if (!BuildWorksPlacementValidation.TryFirstHit(ghost, hits, count, out RaycastHit first,
                    ignoreTriggers: true, attachmentSupportOnly: true))
                { TraceHollow(ghost, "world no first hit"); continue; }
                TraceHollow(ghost, "world first=" + first.collider.GetInstanceID() + " target=" + target.GetInstanceID() + " point=" + first.point + " gap=" + Vector3.Distance(first.point, point).ToString("R"));
                if (first.collider.attachedRigidbody) { failure |= PlacementContactFailure.Obstructed; continue; }
                if (first.collider != target || !Finite(first.point) || !Finite(first.normal) ||
                    Vector3.Distance(first.point, point) > reach) { TraceHollow(ghost, "world rejected target/finite/point-gap"); continue; }
                if (Vector3.Distance(first.point, eye) >= maximumDistance)
                { TraceHollow(ghost, "world rejected range"); failure |= PlacementContactFailure.OutOfRange; continue; }
                TraceHollow(ghost, "world accepted");
                hit = first; return true;
            }
            failure |= PlacementContactFailure.UnknownGeometry; return false;
        }

        private static bool TryShape(Collider collider, Vector3 origin, out GeometryHolder geometry, out ImmediateTransform pose)
        {
            geometry = default; pose = default;
            if (!collider || !collider.enabled || !collider.gameObject.activeInHierarchy || !ValidTransform(collider.transform)) return false;
            try
            {
                geometry = collider.GeometryHolder;
                Vector3 center = Vector3.zero;
                Quaternion rotation = collider.transform.rotation;
                if (collider is BoxCollider box)
                { center = box.center; Vector3 extents = geometry.As<BoxGeometry>().HalfExtents; if (!Finite(extents) || extents.x <= 0 || extents.y <= 0 || extents.z <= 0) return false; }
                else if (collider is SphereCollider sphere)
                { center = sphere.center; float radius = geometry.As<SphereGeometry>().Radius; if (!Finite(radius) || radius <= 0) return false; }
                else if (collider is CapsuleCollider capsule)
                {
                    center = capsule.center; CapsuleGeometry value = geometry.As<CapsuleGeometry>();
                    if (!Finite(value.Radius) || value.Radius <= 0 || !Finite(value.HalfLength) || value.HalfLength < 0 || capsule.direction < 0 || capsule.direction > 2) return false;
                    rotation *= Quaternion.FromToRotation(Vector3.right, capsule.direction == 0 ? Vector3.right : capsule.direction == 1 ? Vector3.up : Vector3.forward);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh)
                {
                    Vector3 scale = mesh.convex ? geometry.As<ConvexMeshGeometry>().Scale : geometry.As<TriangleMeshGeometry>().Scale;
                    if (!Finite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0) return false;
                }
                else return false;
                pose = new ImmediateTransform { Position = collider.transform.TransformPoint(center) - origin, Rotation = rotation };
                return Finite(pose.Position);
            }
            catch (InvalidOperationException) { return false; }
            catch (UnityException) { return false; }
        }

        private static bool KnownShape(Collider collider) => collider is BoxCollider || collider is SphereCollider ||
            collider is CapsuleCollider || collider is MeshCollider mesh && mesh.sharedMesh;

        private static bool ValidTransform(Transform transform)
        {
            if (!transform || !Finite(transform.position)) return false;
            for (Transform node = transform; node; node = node.parent)
            {
                Vector3 local = node.localScale;
                if (!Finite(local) || local.x <= 0 || local.y <= 0 || local.z <= 0) return false;
            }
            Vector3 scale = transform.lossyScale;
            if (!Finite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0) return false;
            Matrix4x4 matrix = transform.localToWorldMatrix;
            Vector3 x = matrix.MultiplyVector(Vector3.right), y = matrix.MultiplyVector(Vector3.up), z = matrix.MultiplyVector(Vector3.forward);
            if (!Finite(x) || !Finite(y) || !Finite(z) || x.sqrMagnitude <= 0 || y.sqrMagnitude <= 0 || z.sqrMagnitude <= 0) return false;
            x.Normalize(); y.Normalize(); z.Normalize();
            return Vector3.Dot(Vector3.Cross(x, y), z) > .999f && Mathf.Abs(Vector3.Dot(x, y)) < .0001f &&
                Mathf.Abs(Vector3.Dot(x, z)) < .0001f && Mathf.Abs(Vector3.Dot(y, z)) < .0001f;
        }

        private void EnsureResources()
        {
            if (snapshot != null) return;
            try
            {
                snapshot = new BlueprintStructuralSnapshot();
                a = new NativeArray<GeometryHolder>(1, Allocator.Persistent); b = new NativeArray<GeometryHolder>(1, Allocator.Persistent);
                pa = new NativeArray<ImmediateTransform>(1, Allocator.Persistent); pb = new NativeArray<ImmediateTransform>(1, Allocator.Persistent);
                contacts = new NativeArray<ImmediateContact>(64, Allocator.Persistent); counts = new NativeArray<int>(1, Allocator.Persistent);
            }
            catch { Dispose(); throw; }
        }

        internal void Clear() { snapshot?.Clear(); receiver = null; triangleContact.Clear(); }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            triangleContact.Clear();
            if (snapshot != null) { snapshot.Dispose(); Unloading = snapshot.Unloading; pendingUnload = Unloading; snapshot = null; }
            receiver = null;
            if (a.IsCreated) a.Dispose(); if (b.IsCreated) b.Dispose();
            if (pa.IsCreated) pa.Dispose(); if (pb.IsCreated) pb.Dispose();
            if (contacts.IsCreated) contacts.Dispose(); if (counts.IsCreated) counts.Dispose();
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private static void TraceHollow(GameObject ghost, string message)
        {
#if UNITY_EDITOR
            if (ghost && ghost.name == "AdapterHollowSource") Debug.Log("BUILDWORKS_HOLLOW_TRACE " + message);
#endif
        }
    }
}
