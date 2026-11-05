using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class TriangleDistanceChecks
    {
        private static readonly int[][] orders = { new[] { 0, 1, 2 }, new[] { 0, 2, 1 }, new[] { 1, 0, 2 },
            new[] { 1, 2, 0 }, new[] { 2, 0, 1 }, new[] { 2, 1, 0 } };

        internal static void Run()
        {
            Point3[] face = { P(-2, -2), P(2, -2), P(0, 2) };
            Check("coplanar containment", face, new[] { P(.1, 0), P(.2, 0), P(.1, .1) }, 0);
            Check("edge through face without vertex contact", face, new[] { P(0, 0, -1), P(0, 0, 1), P(3, 0) }, 0);
            Check("vertex-face", face, new[] { P(0, 0, 2), P(-1, 0, 3), P(1, 0, 3) }, 2);
            Check("skew edge-edge", new[] { P(0, 0), P(1, 0), P(0, -1, -1) },
                new[] { P(.5, -.5, 1), P(.5, .5, 1), P(1.5, 0, 2) }, 1);
            Check("coplanar disjoint", new[] { P(0, 0), P(1, 0), P(0, 1) }, new[] { P(3, 0), P(4, 0), P(3, 1) }, 2);
            Check("identical", face, face, 0);
            foreach (float gap in new[] { .0019f, .002f, .0021f, .01f })
            {
                var other = new[] { face[0] + P(0, 0, gap), face[1] + P(0, 0, gap), face[2] + P(0, 0, gap) };
                Check("parallel actual-float gap " + gap, face, other, gap);
                Require(Closest(face, other, out _, out _) == (double)gap, "Parallel float gap changed before policy comparison");
            }
            HollowFrame();
            RandomControls();
            foreach (Point3[] bad in new[] {
                new[] { P(0, 0), P(1, 0), P(2, 0) }, new[] { P(0, 0), P(0, 0), P(1, 1) },
                new[] { P(0, 0), P(1, 0), P(1, 1e-10) },
                new[] { P(double.NaN, 0), P(1, 0), P(0, 1) },
                new[] { P(double.PositiveInfinity, 0), P(1, 0), P(0, 1) },
                new[] { P(0, 0), P(1e150, 0), P(0, 1e-150) } })
            {
                Require(!Query(face, bad, out Point3 first, out Point3 second) && first.LengthSquared == 0 && second.LengthSquared == 0,
                    "Invalid/ill-conditioned geometry returned a usable pair");
            }
            var far = new[] { P(1e200, 0), P(1e200, 1), P(1e200, 0, 1) };
            Require(!Query(face, far, out _, out _), "Nonfinite arithmetic became a valid distance");
            Console.WriteLine("PASS triangle closest pairs: analytic face/edge/coplanar/gaps, permutations/symmetry/rotation/12000, hollow frame,64 sampled controls, invalid arithmetic. Raw CPU geometry only.");
        }

        private static void Check(string label, Point3[] a, Point3[] b, double expected)
        {
            foreach (int mode in new[] { 0, 1, 2 })
            {
                double scale = mode == 2 ? 2 : 1;
                var rotation = mode == 0 ? new Rotation3(0, 0, 0, 1) : new Rotation3(.1, .2, .3, .9).Normalized();
                Point3 shift = mode == 0 ? default : P(12000, -12000, 12000);
                Point3 Transform(Point3 point) => rotation.Rotate(point * scale) + shift;
                foreach (int[] ia in orders)
                    foreach (int[] ib in orders)
                    {
                        var x = new[] { Transform(a[ia[0]]), Transform(a[ia[1]]), Transform(a[ia[2]]) };
                        var y = new[] { Transform(b[ib[0]]), Transform(b[ib[1]]), Transform(b[ib[2]]) };
                        double distance = Closest(x, y, out Point3 onA, out Point3 onB);
                        Require(Math.Abs(distance - expected * scale) < 1e-8, label + " distance mismatch");
                        Require(OnTriangle(onA, x) && OnTriangle(onB, y), label + " closest point is outside a triangle");
                        Require(Math.Abs(Closest(y, x, out _, out _) - distance) < 1e-8, label + " asymmetric distance");
                    }
            }
        }

        private static void HollowFrame()
        {
            Point3[] inside = { P(0, 0), P(.2, 0), P(0, .2) };
            double closest = double.PositiveInfinity;
            foreach (var bounds in new[] { (-2d, -2d, 2d, -1d), (-2d, 1d, 2d, 2d), (-2d, -1d, -1d, 1d), (1d, -1d, 2d, 1d) })
            {
                Point3 a = P(bounds.Item1, bounds.Item2), b = P(bounds.Item3, bounds.Item2);
                Point3 c = P(bounds.Item3, bounds.Item4), d = P(bounds.Item1, bounds.Item4);
                closest = Math.Min(closest, Closest(inside, new[] { a, b, c }, out _, out _));
                closest = Math.Min(closest, Closest(inside, new[] { a, c, d }, out _, out _));
            }
            Require(Math.Abs(closest - .8) < 1e-12, "Outer bounds filled the frame's empty hole");
        }

        private static void RandomControls()
        {
            var random = new Random(191103);
            Point3 Vertex() => P(random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2, random.NextDouble() * 4 - 2);
            for (int sample = 0; sample < 64; ++sample)
            {
                var a = new[] { Vertex(), Vertex(), Vertex() }; var b = new[] { Vertex(), Vertex(), Vertex() };
                double distance = Closest(a, b, out Point3 first, out Point3 second);
                Require(OnTriangle(first, a) && OnTriangle(second, b), "Random closest pair is not feasible");
                Require(Math.Abs(Closest(b, a, out _, out _) - distance) < 1e-9, "Random symmetry failed");
                // A sampled feasible pair is an upper bound, not an exact distance oracle.
                foreach (Point3 pa in Samples(a))
                    foreach (Point3 pb in Samples(b))
                        Require(distance * distance <= (pa - pb).LengthSquared + 1e-10, "Closest pair missed a nearer sampled pair");
            }
        }

        private static IEnumerable<Point3> Samples(Point3[] triangle)
        {
            for (int i = 0; i <= 6; ++i)
                for (int j = 0; j <= 6 - i; ++j)
                    yield return triangle[0] + (triangle[1] - triangle[0]) * (i / 6d) + (triangle[2] - triangle[0]) * (j / 6d);
        }
        private static bool OnTriangle(Point3 point, Point3[] triangle)
        {
            Point3 u = triangle[1] - triangle[0], v = triangle[2] - triangle[0], p = point - triangle[0];
            double uu = Dot(u, u), uv = Dot(u, v), vv = Dot(v, v), up = Dot(u, p), vp = Dot(v, p);
            double determinant = uu * vv - uv * uv;
            double a = (up * vv - vp * uv) / determinant, b = (vp * uu - up * uv) / determinant;
            return a >= -1e-7 && b >= -1e-7 && a + b <= 1 + 1e-7 && (p - u * a - v * b).LengthSquared < 1e-14;
        }
        private static double Closest(Point3[] a, Point3[] b, out Point3 first, out Point3 second)
        {
            Require(Query(a, b, out first, out second), "Valid triangle query unexpectedly failed");
            return Math.Sqrt((first - second).LengthSquared);
        }
        private static bool Query(Point3[] a, Point3[] b, out Point3 first, out Point3 second) =>
            TriangleDistance.TryClosest(a[0], a[1], a[2], b[0], b[1], b[2], out first, out second);
        private static Point3 P(double x, double y, double z = 0) => new Point3(x, y, z);
        private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
