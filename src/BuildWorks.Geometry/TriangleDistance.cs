using System;

namespace OstrixMods.BuildWorks.Geometry
{
    // Raw triangle geometry only. A closest pair does not certify a cooked collider contact.
    public static class TriangleDistance
    {
        public static bool TryClosest(Point3 a0, Point3 a1, Point3 a2, Point3 b0, Point3 b1, Point3 b2,
            out Point3 first, out Point3 second)
        {
            first = second = default;
            if (!Finite(a0) || !Finite(a1) || !Finite(a2) || !Finite(b0) || !Finite(b1) || !Finite(b2)) return false;
            var a = new Triangle(default, a1 - a0, a2 - a0);
            var b = new Triangle(b0 - a0, b1 - a0, b2 - a0);
            if (!a.Valid || !b.Valid) return false;
            var closest = new Pair { Squared = double.PositiveInfinity };
            for (int edge = 0; edge < 3; ++edge)
            {
                if (!SegmentTriangle(a.Vertex(edge), a.Vertex((edge + 1) % 3), b, out Pair ab) ||
                    !SegmentTriangle(b.Vertex(edge), b.Vertex((edge + 1) % 3), a, out Pair ba) ||
                    !closest.Add(ab.First, ab.Second) || !closest.Add(ba.Second, ba.First)) return false;
            }
            Point3 resultFirst = closest.First + a0, resultSecond = closest.Second + a0;
            if (!Finite(resultFirst) || !Finite(resultSecond)) return false;
            first = resultFirst; second = resultSecond;
            return true;
        }

        private static bool SegmentTriangle(Point3 start, Point3 end, Triangle triangle, out Pair closest)
        {
            closest = new Pair { Squared = double.PositiveInfinity };
            Point3 direction = end - start;
            double divisor = Dot(triangle.Normal, direction);
            if (divisor != 0)
            {
                double t = Dot(triangle.Normal, triangle.A - start) / divisor;
                if (t >= 0 && t <= 1)
                {
                    Point3 crossing = start + direction * t;
                    if (triangle.ContainsProjection(crossing)) return closest.Add(crossing, crossing);
                }
            }
            if (!closest.Add(start, triangle.Closest(start)) || !closest.Add(end, triangle.Closest(end))) return false;
            for (int edge = 0; edge < 3; ++edge)
            {
                if (!SegmentSegment(start, end, triangle.Vertex(edge), triangle.Vertex((edge + 1) % 3),
                    out Point3 onSegment, out Point3 onTriangle) || !closest.Add(onSegment, onTriangle)) return false;
            }
            return true;
        }

        private static bool SegmentSegment(Point3 p0, Point3 p1, Point3 q0, Point3 q1,
            out Point3 first, out Point3 second)
        {
            first = second = default;
            Point3 u = p1 - p0, v = q1 - q0, offset = p0 - q0;
            double uu = u.LengthSquared, vv = v.LengthSquared;
            double uv = Dot(u, v), uo = Dot(u, offset), vo = Dot(v, offset);
            Point3 normal = Cross(u, v);
            double denominator = normal.LengthSquared;
            if (!GeometryMath.IsFinite(denominator) || !GeometryMath.IsFinite(uv) ||
                !GeometryMath.IsFinite(uo) || !GeometryMath.IsFinite(vo) || uu <= 0 || vv <= 0 ||
                (denominator == 0 && (normal.X != 0 || normal.Y != 0 || normal.Z != 0))) return false;
            double rawS = denominator == 0 ? 0 : Dot(Cross(q0 - p0, v), normal) / denominator;
            if (!GeometryMath.IsFinite(rawS)) return false;
            double s = Clamp(rawS);
            double t = (uv * s + vo) / vv;
            if (!GeometryMath.IsFinite(t)) return false;
            if (t < 0) { t = 0; s = Clamp(-uo / uu); }
            else if (t > 1) { t = 1; s = Clamp((uv - uo) / uu); }
            first = p0 + u * s; second = q0 + v * t;
            return Finite(first) && Finite(second);
        }

        private struct Pair
        {
            internal Point3 First, Second;
            internal double Squared;
            internal bool Add(Point3 first, Point3 second)
            {
                double squared = (first - second).LengthSquared;
                if (!Finite(first) || !Finite(second) || !GeometryMath.IsFinite(squared)) return false;
                if (squared < Squared) { First = first; Second = second; Squared = squared; }
                return true;
            }
        }

        private readonly struct Triangle
        {
            internal readonly Point3 A, B, C, Normal;
            private readonly Point3 u, v;
            private readonly double normalSquared;
            internal readonly bool Valid;
            internal Triangle(Point3 a, Point3 b, Point3 c)
            {
                A = a; B = b; C = c; u = b - a; v = c - a; Normal = Cross(u, v);
                normalSquared = Normal.LengthSquared;
                double longestSquared = Math.Max(u.LengthSquared, Math.Max(v.LengthSquared, (c - b).LengthSquared));
                double scale = longestSquared * longestSquared;
                // shortcut: ill-conditioned triangles stay unknown; add robust predicates before accepting them.
                Valid = Finite(a) && Finite(b) && Finite(c) && GeometryMath.IsFinite(scale) &&
                    GeometryMath.IsFinite(normalSquared) && scale > 0 && normalSquared > scale * 1e-16;
            }
            internal Point3 Vertex(int index) => index == 0 ? A : index == 1 ? B : C;
            internal bool ContainsProjection(Point3 point)
            {
                Point3 offset = point - A;
                double alongU = Dot(Cross(offset, v), Normal) / normalSquared;
                double alongV = Dot(Cross(u, offset), Normal) / normalSquared;
                return alongU >= 0 && alongV >= 0 && alongU + alongV <= 1;
            }
            internal Point3 Closest(Point3 point)
            {
                Point3 projection = point - Normal * (Dot(point - A, Normal) / normalSquared);
                if (ContainsProjection(projection)) return projection;
                Point3 closest = OnEdge(point, A, B);
                double squared = (point - closest).LengthSquared;
                for (int edge = 1; edge < 3; ++edge)
                {
                    Point3 candidate = OnEdge(point, Vertex(edge), Vertex((edge + 1) % 3));
                    double distance = (point - candidate).LengthSquared;
                    if (distance < squared) { closest = candidate; squared = distance; }
                }
                return closest;
            }
        }

        private static Point3 OnEdge(Point3 point, Point3 a, Point3 b)
        {
            Point3 edge = b - a;
            return a + edge * Clamp(Dot(point - a, edge) / edge.LengthSquared);
        }
        private static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
        private static bool Finite(Point3 point) => GeometryMath.IsFinite(point.X) && GeometryMath.IsFinite(point.Y) && GeometryMath.IsFinite(point.Z);
        private static double Dot(Point3 a, Point3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        private static Point3 Cross(Point3 a, Point3 b) => new Point3(a.Y * b.Z - a.Z * b.Y,
            a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
    }
}
