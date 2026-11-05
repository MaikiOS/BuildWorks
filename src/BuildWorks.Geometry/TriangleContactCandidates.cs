using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    // Raw candidates only: an empty/completed scan never certifies native NoContact.
    public sealed class TriangleContactCandidates
    {
        public const int MaximumVerticesPerMesh = 8192;
        public const int MaximumTrianglesPerMesh = 16384;
        public const int MaximumVerticesPerValidation = 32768;
        public const int MaximumTrianglesPerValidation = 32768;
        public const int MaximumPairBoundsTests = 65536;
        public const int MaximumExactPairs = 4096;

        public struct Budget
        {
            public int Vertices { get; internal set; }
            public int Triangles { get; internal set; }
            public int PairBoundsTests { get; internal set; }
            public int ExactPairs { get; internal set; }
            public int UnresolvedPairs { get; internal set; }
        }

        public readonly struct Candidate
        {
            internal Candidate(int firstTriangle, int secondTriangle, Point3 first, Point3 second)
            { FirstTriangle = firstTriangle; SecondTriangle = secondTriangle; First = first; Second = second; }
            public int FirstTriangle { get; }
            public int SecondTriangle { get; }
            public Point3 First { get; }
            public Point3 Second { get; }
        }

        private readonly List<int> targetTriangles = new List<int>();
        private readonly List<Candidate> results = new List<Candidate>();
        private readonly IReadOnlyList<Candidate> view;

        public TriangleContactCandidates() { view = results.AsReadOnly(); }

        // Reused view, valid until the next call. Inputs must remain unchanged during this synchronous call.
        public IReadOnlyList<Candidate> Results => view;

        // Share one budget across every mesh-pair scan in a host validation; reset only for the next validation.
        public bool TryCollect(IReadOnlyList<Point3> firstVertices, IReadOnlyList<int> firstIndices,
            IReadOnlyList<Point3> secondVertices, IReadOnlyList<int> secondIndices, double tolerance, ref Budget budget)
        {
            results.Clear(); targetTriangles.Clear();
            double toleranceSquared = tolerance * tolerance;
            if (!GeometryMath.IsFinite(tolerance) || tolerance < 0 || !GeometryMath.IsFinite(toleranceSquared) ||
                (tolerance > 0 && toleranceSquared == 0) || !Fits(firstVertices, firstIndices) || !Fits(secondVertices, secondIndices)) return false;
            int vertices = firstVertices.Count + secondVertices.Count;
            int triangles = firstIndices.Count / 3 + secondIndices.Count / 3;
            if (vertices > MaximumVerticesPerValidation - budget.Vertices ||
                triangles > MaximumTrianglesPerValidation - budget.Triangles) return false;
            budget.Vertices += vertices; budget.Triangles += triangles;
            if (!ValidData(firstVertices, firstIndices) || !ValidData(secondVertices, secondIndices)) return false;
            if (firstIndices.Count == 0 || secondIndices.Count == 0) return true;

            AnchorBounds sourceBounds = BoundsOf(firstVertices, firstIndices, 0, firstIndices.Count);
            for (int target = 0; target < secondIndices.Count / 3; ++target)
                if (Near(sourceBounds, BoundsOf(secondVertices, secondIndices, target * 3, 3), tolerance)) targetTriangles.Add(target);

            for (int source = 0; source < firstIndices.Count / 3 && targetTriangles.Count > 0; ++source)
            {
                AnchorBounds bounds = BoundsOf(firstVertices, firstIndices, source * 3, 3);
                foreach (int target in targetTriangles)
                {
                    if (budget.PairBoundsTests >= MaximumPairBoundsTests) return Fail();
                    ++budget.PairBoundsTests;
                    if (!Near(bounds, BoundsOf(secondVertices, secondIndices, target * 3, 3), tolerance)) continue;
                    if (budget.ExactPairs >= MaximumExactPairs) return Fail();
                    ++budget.ExactPairs;
                    bool found = TriangleDistance.TryClosest(
                        firstVertices[firstIndices[source * 3]], firstVertices[firstIndices[source * 3 + 1]], firstVertices[firstIndices[source * 3 + 2]],
                        secondVertices[secondIndices[target * 3]], secondVertices[secondIndices[target * 3 + 1]], secondVertices[secondIndices[target * 3 + 2]],
                        out Point3 first, out Point3 second);
                    double squared = (first - second).LengthSquared;
                    if (!found || !GeometryMath.IsFinite(squared)) { ++budget.UnresolvedPairs; continue; }
                    if (squared <= toleranceSquared) results.Add(new Candidate(source, target, first, second));
                }
            }
            return true;
        }

        private bool Fail() { results.Clear(); return false; }

        private static bool Fits(IReadOnlyList<Point3> vertices, IReadOnlyList<int> indices) =>
            vertices != null && indices != null && vertices.Count >= 0 && vertices.Count <= MaximumVerticesPerMesh &&
            indices.Count >= 0 && indices.Count % 3 == 0 && indices.Count / 3 <= MaximumTrianglesPerMesh;

        private static bool ValidData(IReadOnlyList<Point3> vertices, IReadOnlyList<int> indices)
        {
            foreach (Point3 point in vertices)
                if (!GeometryMath.IsFinite(point.X) || !GeometryMath.IsFinite(point.Y) || !GeometryMath.IsFinite(point.Z)) return false;
            foreach (int index in indices) if (index < 0 || index >= vertices.Count) return false;
            return true;
        }

        private static AnchorBounds BoundsOf(IReadOnlyList<Point3> vertices, IReadOnlyList<int> indices, int offset, int count)
        {
            Point3 min = vertices[indices[offset]], max = min;
            for (int i = offset + 1; i < offset + count; ++i)
            {
                Point3 p = vertices[indices[i]];
                min = new Point3(Math.Min(min.X, p.X), Math.Min(min.Y, p.Y), Math.Min(min.Z, p.Z));
                max = new Point3(Math.Max(max.X, p.X), Math.Max(max.Y, p.Y), Math.Max(max.Z, p.Z));
            }
            return new AnchorBounds(min, max);
        }

        private static bool Near(AnchorBounds a, AnchorBounds b, double tolerance) =>
            b.Minimum.X - a.Maximum.X <= tolerance && a.Minimum.X - b.Maximum.X <= tolerance &&
            b.Minimum.Y - a.Maximum.Y <= tolerance && a.Minimum.Y - b.Maximum.Y <= tolerance &&
            b.Minimum.Z - a.Maximum.Z <= tolerance && a.Minimum.Z - b.Maximum.Z <= tolerance;
    }
}
