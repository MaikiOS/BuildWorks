using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // One synchronous validation only. Both cooked colliders must witness each raw candidate.
    internal sealed class BlueprintTriangleContact
    {
        internal readonly struct NativePair
        {
            internal NativePair(RaycastHit target, double distance) { Target = target; Distance = (float)distance; }
            internal RaycastHit Target { get; }
            internal float Distance { get; }
        }

        private sealed class RawMesh
        {
            internal Vector3[] Vertices;
            internal int[] Indices;
        }

        private const float Tolerance = BuildWorksPlacementValidation.SurfaceContactTolerance;
        private const int MaximumNativeRays = 128;
        private readonly Dictionary<Mesh, RawMesh> meshes = new Dictionary<Mesh, RawMesh>();
        private readonly TriangleContactCandidates candidates = new TriangleContactCandidates();
        private readonly List<NativePair> pairs = new List<NativePair>();
        private readonly IReadOnlyList<NativePair> view;
        private TriangleContactCandidates.Budget budget;
        private int readVertices, readTriangles, nativeRays;

        internal BlueprintTriangleContact() { view = pairs.AsReadOnly(); }

        internal void Clear()
        {
            meshes.Clear(); pairs.Clear(); budget = default;
            readVertices = readTriangles = nativeRays = 0;
        }

        // Caller checks static shapes and positive, unsheared transforms. Empty results mean Unknown.
        internal IReadOnlyList<NativePair> Collect(MeshCollider source, MeshCollider target, Vector3 origin)
        {
            pairs.Clear();
            if (!Read(source.sharedMesh, out RawMesh first) || !Read(target.sharedMesh, out RawMesh second) ||
                first.Vertices.Length + second.Vertices.Length > TriangleContactCandidates.MaximumVerticesPerValidation - budget.Vertices ||
                first.Indices.Length / 3 + second.Indices.Length / 3 > TriangleContactCandidates.MaximumTrianglesPerValidation - budget.Triangles)
                return view;
            Point3[] a = Transform(first.Vertices, source.transform.localToWorldMatrix, Vector3.zero);
            Point3[] b = Transform(second.Vertices, target.transform.localToWorldMatrix, origin);
            if (!candidates.TryCollect(a, first.Indices, b, second.Indices, Tolerance, ref budget)) return view;
            foreach (TriangleContactCandidates.Candidate candidate in candidates.Results)
            {
                if (nativeRays >= MaximumNativeRays) break;
                if (!Normal(a, first.Indices, candidate.FirstTriangle, out Vector3 firstNormal) ||
                    !Normal(b, second.Indices, candidate.SecondTriangle, out Vector3 secondNormal) ||
                    !TryWitness(source, candidate.First, firstNormal, Vector3.zero, out RaycastHit firstHit, out Point3 onSource, out Ray firstRay) ||
                    !TryWitness(target, candidate.Second, secondNormal, origin, out RaycastHit hit, out Point3 onTarget, out Ray secondRay)) continue;
                // Reserve the existing world's two possible casts as well as the paired collider probes.
                if (TryRepresentationUpperBound(firstRay, firstHit.distance, onSource, secondRay, hit.distance, onTarget,
                    out double upper) && upper <= Tolerance && ReserveRaySlots(2)) pairs.Add(new NativePair(hit, upper));
            }
            return view;
        }

        private bool Read(Mesh mesh, out RawMesh raw)
        {
            if (!mesh) { raw = null; return false; }
            if (meshes.TryGetValue(mesh, out raw)) return raw != null;
            if (BlueprintEditorMeshData.TryReadContact(mesh, ref readVertices, ref readTriangles,
                out Vector3[] vertices, out int[] indices, out _)) raw = new RawMesh { Vertices = vertices, Indices = indices };
            meshes.Add(mesh, raw);
            return raw != null;
        }

        internal bool ReserveRaySlots(int count)
        {
            if (count < 0 || count > MaximumNativeRays - nativeRays) return false;
            nativeRays += count; return true;
        }

        internal static Point3 ReconstructRayPoint(Ray ray, float distance, Vector3 anchor) =>
            new Point3((double)ray.origin.x - anchor.x, (double)ray.origin.y - anchor.y,
                (double)ray.origin.z - anchor.z) + Point(ray.direction) * distance;

        // shortcut: representation bounds do not certify native solver error; qualify the admitted host/geometry before release.
        internal static bool TryRepresentationUpperBound(Ray firstRay, float firstDistance, Point3 first,
            Ray secondRay, float secondDistance, Point3 second, out double upper)
        {
            upper = double.NaN;
            double a = RepresentationError(firstRay, firstDistance, first), b = RepresentationError(secondRay, secondDistance, second);
            double squared = (first - second).LengthSquared;
            if (!Finite(a) || !Finite(b) || !Finite(squared)) return false;
            double value = Math.Sqrt(squared) + a + b;
            upper = value + Math.Max(1, value) * (8 * 2.220446049250313e-16);
            return Finite(upper);
        }

        private static double RepresentationError(Ray ray, float distance, Point3 point)
        {
            if (!Finite(ray.origin) || !Finite(ray.direction) || !Finite(distance) || distance < 0 || !Finite(point)) return double.NaN;
            double length = Math.Sqrt(Point(ray.direction).LengthSquared);
            if (!Finite(length) || Math.Abs(length - 1) > .01) return double.NaN;
            // One relative float ULP conservatively covers nearest-float storage, including the subnormal floor.
            double scalar = Math.Abs((double)distance) * 1.1920928955078125e-7 + float.Epsilon;
            double magnitude = 1d + Math.Abs(ray.origin.x) + Math.Abs(ray.origin.y) + Math.Abs(ray.origin.z) +
                Math.Abs(point.X) + Math.Abs(point.Y) + Math.Abs(point.Z) + Math.Abs(distance);
            return scalar * length + Math.Abs(distance) * Math.Abs(length - 1) + magnitude * (32 * 2.220446049250313e-16);
        }

        // hit.point has already lost world-coordinate precision; retain actual ray bits and native scalar distance.
        internal bool TryWitness(Collider collider, Point3 point, Vector3 normal, Vector3 anchor,
            out RaycastHit hit, out Point3 witnessed, out Ray witnessRay)
        {
            hit = default; witnessed = default; witnessRay = default;
            const float reach = Tolerance * 4;
            if (!Finite(point) || !Finite(normal) || Mathf.Abs(normal.sqrMagnitude - 1) > .01f) return false;
            for (int side = 0; side < 2; ++side)
            {
                if (!ReserveRaySlots(1)) return false;
                Vector3 direction = side == 0 ? normal : -normal;
                Point3 start = point - Point(direction) * reach;
                var ray = new Ray(new Vector3((float)(start.X + anchor.x), (float)(start.Y + anchor.y),
                    (float)(start.Z + anchor.z)), direction);
                if (!Finite(ray.origin) || !Finite(ray.direction)) return false;
                if (!collider.Raycast(ray, out RaycastHit value, reach * 2 + Tolerance)) continue;
                if (value.collider != collider || !Finite(value.distance) || value.distance < 0 || value.distance > reach * 2 + Tolerance ||
                    !Finite(value.point) || !Finite(value.normal) || Mathf.Abs(value.normal.sqrMagnitude - 1) > .01f) continue;
                Point3 actual = ReconstructRayPoint(ray, value.distance, anchor);
                if (!Finite(actual) || (actual - point).LengthSquared > (double)Tolerance * Tolerance) continue;
                hit = value; witnessed = actual; witnessRay = ray; return true;
            }
            return false;
        }

        private static Point3[] Transform(Vector3[] vertices, Matrix4x4 matrix, Vector3 origin)
        {
            var result = new Point3[vertices.Length];
            for (int i = 0; i < vertices.Length; ++i)
            {
                Vector3 p = vertices[i];
                result[i] = new Point3((double)matrix.m00 * p.x + (double)matrix.m01 * p.y + (double)matrix.m02 * p.z + ((double)matrix.m03 - origin.x),
                    (double)matrix.m10 * p.x + (double)matrix.m11 * p.y + (double)matrix.m12 * p.z + ((double)matrix.m13 - origin.y),
                    (double)matrix.m20 * p.x + (double)matrix.m21 * p.y + (double)matrix.m22 * p.z + ((double)matrix.m23 - origin.z));
            }
            return result;
        }

        private static bool Normal(Point3[] vertices, int[] indices, int triangle, out Vector3 normal)
        {
            Point3 a = vertices[indices[triangle * 3 + 1]] - vertices[indices[triangle * 3]],
                b = vertices[indices[triangle * 3 + 2]] - vertices[indices[triangle * 3]];
            Point3 cross = new Point3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
            double length = Math.Sqrt(cross.LengthSquared);
            normal = length > 0 && Finite(length) ? Vector(cross * (1 / length)) : default;
            return Finite(normal) && Mathf.Abs(normal.sqrMagnitude - 1) <= .01f;
        }

        private static Point3 Point(Vector3 v) => new Point3(v.x, v.y, v.z);
        private static Vector3 Vector(Point3 v) => new Vector3((float)v.X, (float)v.Y, (float)v.Z);
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        private static bool Finite(Point3 v) => Finite(v.X) && Finite(v.Y) && Finite(v.Z);
        private static bool Finite(Vector3 v) => Finite(v.x) && Finite(v.y) && Finite(v.z);
    }
}
