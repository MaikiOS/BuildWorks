using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using Scan = OstrixMods.BuildWorks.Geometry.TriangleContactCandidates;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class TriangleCandidateChecks
    {
        private const double Tolerance = (double).002f;
        private static readonly int[] oneTriangle = { 0, 1, 2 };
        private static readonly Point3[] unit = { P(0, 0), P(1, 0), P(0, 1) };

        internal static void Run()
        {
            OracleControls();
            RefreshAndValidation();
            Budgets();
            Console.WriteLine("PASS bounded triangle candidates: exhaustive-small pair oracle, float gaps/rotation/12000, fresh same-buffer edits, malformed data, reused readonly view and shared work budgets. No native contact verdict.");
        }

        private static void OracleControls()
        {
            var random = new Random(611103);
            for (int sample = 0; sample < 32; ++sample)
            {
                var first = new List<Point3>(); var second = new List<Point3>();
                for (int triangle = 0; triangle < 12; ++triangle)
                {
                    Point3 origin = P(random.NextDouble() * 8 - 4, random.NextDouble() * 8 - 4);
                    first.Add(origin); first.Add(origin + P(.4, 0)); first.Add(origin + P(0, .7));
                }
                for (int triangle = 0; triangle < 16; ++triangle)
                {
                    Point3 shift = P(triangle >= 12 ? 100 : 0, 0, triangle % 2 == 0 ? .0015f : .0021f);
                    for (int vertex = 0; vertex < 3; ++vertex) second.Add(first[(triangle % 12) * 3 + vertex] + shift);
                }
                var rotation = sample % 2 == 0 ? new Rotation3(0, 0, 0, 1) : new Rotation3(.2, .4, .1, .9).Normalized();
                Point3 world = sample % 3 == 0 ? P(12000, -12000, 12000) : default;
                Point3 Transform(Point3 p) => rotation.Rotate(P(p.X * 2, p.Y * .5, p.Z * 1.25)) + world;
                Point3[] a = first.Select(Transform).ToArray(), b = second.Select(Transform).ToArray();
                int[] ai = Enumerable.Range(0, a.Length).ToArray(), bi = Enumerable.Range(0, b.Length).ToArray();
                Require(CompareOracle(a, ai, b, bi) > 0 && CompareOracle(b, bi, a, ai) > 0,
                    "Random oracle controls lost all positive pairs");
            }
            foreach (float gap in new[] { 0f, .0019f, .002f, .0021f, .01f })
                CompareOracle(unit, oneTriangle, unit.Select(p => p + P(0, 0, gap)).ToArray(), oneTriangle);

            var scan = new Scan(); var budget = new Scan.Budget();
            Require(scan.TryCollect(new[] { P(0, 0), P(-1, 0), P(0, -1) }, oneTriangle,
                new[] { P(.0015, .0015), P(1.0015, .0015), P(.0015, 1.0015) }, oneTriangle, Tolerance, ref budget) &&
                budget.ExactPairs == 1 && scan.Results.Count == 0, "Overlapping expanded bounds replaced Euclidean distance");
            budget = default;
            var unused = new[] { unit[0], unit[1], unit[2], P(1000, 1000) };
            Require(scan.TryCollect(unused, oneTriangle, unit.Select(p => p + P(100, 100)).ToArray(), oneTriangle,
                Tolerance, ref budget) && budget.PairBoundsTests == 0, "Unreferenced vertices expanded source search bounds");
            var frame = new List<Point3>();
            foreach (var rectangle in new[] { (-2d, -2d, 2d, -1d), (-2d, 1d, 2d, 2d), (-2d, -1d, -1d, 1d), (1d, -1d, 2d, 1d) })
            {
                Point3 a = P(rectangle.Item1, rectangle.Item2), b = P(rectangle.Item3, rectangle.Item2);
                Point3 c = P(rectangle.Item3, rectangle.Item4), d = P(rectangle.Item1, rectangle.Item4);
                frame.AddRange(new[] { a, b, c, a, c, d });
            }
            budget = default;
            Require(scan.TryCollect(frame, Enumerable.Range(0, frame.Count).ToArray(), unit.Select(p => p * .2).ToArray(), oneTriangle,
                Tolerance, ref budget) && scan.Results.Count == 0 && budget.ExactPairs == 0,
                "The frame's whole bounds filled its empty hole or defeated triangle culling");
        }

        private static int CompareOracle(Point3[] a, int[] ai, Point3[] b, int[] bi)
        {
            var expected = new HashSet<string>();
            for (int i = 0; i < ai.Length / 3; ++i)
                for (int j = 0; j < bi.Length / 3; ++j)
                    if (TriangleDistance.TryClosest(a[ai[i * 3]], a[ai[i * 3 + 1]], a[ai[i * 3 + 2]],
                        b[bi[j * 3]], b[bi[j * 3 + 1]], b[bi[j * 3 + 2]], out Point3 p, out Point3 q) &&
                        (p - q).LengthSquared <= Tolerance * Tolerance) expected.Add(i + ":" + j);
            var scan = new Scan(); var budget = new Scan.Budget();
            Require(scan.TryCollect(a, ai, b, bi, Tolerance, ref budget), "Small valid scan exhausted/refused unexpectedly");
            var actual = new HashSet<string>(scan.Results.Select(item => item.FirstTriangle + ":" + item.SecondTriangle));
            Require(actual.Count == scan.Results.Count && actual.SetEquals(expected), "Bounds scan differs from exhaustive exact-pair oracle");
            foreach (var candidate in scan.Results)
                Require((candidate.First - candidate.Second).LengthSquared <= Tolerance * Tolerance, "Candidate exceeds distance policy input");
            Require(budget.ExactPairs <= budget.PairBoundsTests && budget.PairBoundsTests <= ai.Length / 3 * (bi.Length / 3), "Scan counters exceed unculled pair count");
            return actual.Count;
        }

        private static void RefreshAndValidation()
        {
            var scan = new Scan(); var budget = new Scan.Budget();
            var square = new[] { P(0, 0), P(2, 0), P(0, 2), P(2, 2) };
            var indices = new[] { 0, 1, 2 };
            var target = new[] { P(1.8, 1.7), P(1.9, 1.7), P(1.8, 1.75) };
            Require(scan.TryCollect(square, indices, target, oneTriangle, Tolerance, ref budget) && scan.Results.Count == 0,
                "The empty side of a triangle's bounds gained a candidate");
            indices[2] = 3;
            Require(scan.TryCollect(square, indices, target, oneTriangle, Tolerance, ref budget) && scan.Results.Count == 1,
                "Same-count/bounds index edit was cached");
            var view = scan.Results;
            try { ((IList<Scan.Candidate>)view)[0] = default; throw new Exception("Mutable candidate results escaped"); }
            catch (NotSupportedException) { }
            square[3] = P(0, 2);
            Require(scan.TryCollect(square, indices, target, oneTriangle, Tolerance, ref budget) && view.Count == 0,
                "Same-count/bounds vertex edit or reused result clear was stale");

            budget = default;
            Require(scan.TryCollect(unit, oneTriangle, unit, oneTriangle, 0, ref budget) && view.Count == 1,
                "Exact zero-distance collection failed");
            foreach (double invalid in new[] { -1d, double.NaN, double.PositiveInfinity, 1e300, 1e-300 })
            {
                budget = default;
                Require(!scan.TryCollect(unit, oneTriangle, unit, oneTriangle, invalid, ref budget) && view.Count == 0,
                    "Invalid tolerance returned data or retained stale results");
            }
            foreach (int[] invalid in new[] { new[] { -1, 1, 2 }, new[] { 0, 1, 3 }, new[] { 0, 1 } })
            {
                budget = default;
                Require(!scan.TryCollect(unit, invalid, unit, oneTriangle, Tolerance, ref budget) && view.Count == 0,
                    "Malformed indices were accepted");
            }
            budget = default;
            Require(!scan.TryCollect(null, oneTriangle, unit, oneTriangle, Tolerance, ref budget), "Null input accepted");
            var bad = new[] { unit[0], unit[1], unit[2], P(double.NaN, 0) };
            budget = default;
            Require(!scan.TryCollect(bad, oneTriangle, unit, oneTriangle, Tolerance, ref budget), "Unread invalid vertex was silently skipped");
            budget = default;
            Require(scan.TryCollect(new[] { P(0, 0), P(1, 0), P(2, 0) }, oneTriangle, unit, oneTriangle,
                Tolerance, ref budget) && budget.UnresolvedPairs == 1 && view.Count == 0, "Degenerate pair lost its UNKNOWN observation");
            Require(scan.TryCollect(unit, oneTriangle, unit, oneTriangle, Tolerance, ref budget) &&
                budget.UnresolvedPairs == 1 && view.Count == 1, "Unresolved history disappeared or hid another raw candidate");
            budget = default;
            Require(scan.TryCollect(Array.Empty<Point3>(), Array.Empty<int>(), unit, oneTriangle, Tolerance, ref budget) && view.Count == 0,
                "Empty geometry was not collected as an empty raw result");
        }

        private static void Budgets()
        {
            var scan = new Scan(); var budget = new Scan.Budget();
            for (int pair = 0; pair < Scan.MaximumExactPairs - 1; ++pair)
                Require(scan.TryCollect(unit, oneTriangle, unit, oneTriangle, Tolerance, ref budget), "Exact budget ended early");
            Require(!scan.TryCollect(unit, new[] { 0, 1, 2, 0, 1, 2 }, unit, oneTriangle, Tolerance, ref budget) &&
                budget.ExactPairs == Scan.MaximumExactPairs && scan.Results.Count == 0, "Exact cap overflowed or exposed partial candidates");

            budget = default;
            var a = new List<Point3>(); var b = new List<Point3>();
            for (int i = 0; i < 100; ++i)
                foreach (Point3 point in unit) { a.Add(point + P(i * 4, 0)); b.Add(point + P(i * 4 + 2, 0)); }
            int[] cells = Enumerable.Range(0, a.Count).ToArray();
            int completed = 0;
            while (scan.TryCollect(a, cells, b, cells, Tolerance, ref budget))
                Require(++completed < 10, "Pair-bounds budget did not accumulate across calls");
            Require(budget.PairBoundsTests == Scan.MaximumPairBoundsTests && budget.ExactPairs == 0 && scan.Results.Count == 0,
                "Pair-bounds cap failed or disjoint cells became candidates");

            var largeVertices = new Point3[Scan.MaximumVerticesPerMesh];
            budget = default;
            for (int i = 0; i < 2; ++i)
                Require(scan.TryCollect(largeVertices, Array.Empty<int>(), largeVertices, Array.Empty<int>(), Tolerance, ref budget), "Vertex cap ended early");
            Require(!scan.TryCollect(unit, oneTriangle, unit, oneTriangle, Tolerance, ref budget) &&
                budget.Vertices == Scan.MaximumVerticesPerValidation, "Validation vertex cap not shared");
            budget = default;
            Require(!scan.TryCollect(new Point3[Scan.MaximumVerticesPerMesh + 1], Array.Empty<int>(), unit, oneTriangle, Tolerance, ref budget),
                "Per-mesh vertex cap ignored");

            int[] repeated = Enumerable.Range(0, Scan.MaximumTrianglesPerMesh * 3).Select(i => i % 3).ToArray();
            budget = default;
            Require(scan.TryCollect(unit, repeated, unit.Select(p => p + P(100, 0)).ToArray(), repeated, Tolerance, ref budget) &&
                budget.Triangles == Scan.MaximumTrianglesPerValidation && budget.ExactPairs == 0, "Disjoint large triangle inputs were not bounded");
            Require(!scan.TryCollect(unit, oneTriangle, unit, oneTriangle, Tolerance, ref budget), "Validation triangle cap not shared");
            budget = default;
            Require(!scan.TryCollect(unit, new int[(Scan.MaximumTrianglesPerMesh + 1) * 3], unit, oneTriangle, Tolerance, ref budget),
                "Per-mesh triangle cap ignored");
        }

        private static Point3 P(double x, double y, double z = 0) => new Point3(x, y, z);
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
