using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public sealed class AdaptiveMeshData
    {
        internal AdaptiveMeshData(
            Point3[] vertices,
            Point3[] normals,
            Point4[] tangents,
            Point2[] uv,
            int[] triangles,
            Point3[] snapPoints)
        {
            Vertices = vertices;
            Normals = normals;
            Tangents = tangents;
            UV = uv;
            Triangles = triangles;
            SnapPoints = snapPoints;
        }

        public IReadOnlyList<Point3> Vertices { get; }
        public IReadOnlyList<Point3> Normals { get; }
        public IReadOnlyList<Point4> Tangents { get; }
        public IReadOnlyList<Point2> UV { get; }
        public IReadOnlyList<int> Triangles { get; }
        public IReadOnlyList<Point3> SnapPoints { get; }
    }

    public static class AdaptiveGablePanel
    {
        public static AdaptiveMeshData Generate(
            double width,
            double leftHeight,
            double rightHeight,
            double thickness)
        {
            Validate(width, 0.25, 8.0, nameof(width));
            Validate(leftHeight, 0.05, 6.0, nameof(leftHeight));
            Validate(rightHeight, 0.05, 6.0, nameof(rightHeight));
            Validate(thickness, 0.05, 0.50, nameof(thickness));

            double front = -thickness * 0.5;
            double back = thickness * 0.5;
            Point3 f0 = new Point3(0.0, 0.0, front);
            Point3 f1 = new Point3(width, 0.0, front);
            Point3 f2 = new Point3(width, rightHeight, front);
            Point3 f3 = new Point3(0.0, leftHeight, front);
            Point3 b0 = new Point3(0.0, 0.0, back);
            Point3 b1 = new Point3(width, 0.0, back);
            Point3 b2 = new Point3(width, rightHeight, back);
            Point3 b3 = new Point3(0.0, leftHeight, back);

            List<Point3> vertices = new List<Point3>(24);
            List<Point3> normals = new List<Point3>(24);
            List<Point4> tangents = new List<Point4>(24);
            List<Point2> uv = new List<Point2>(24);
            List<int> triangles = new List<int>(36);
            AddQuad(vertices, normals, tangents, uv, triangles,
                f0, f3, f2, f1,
                new Point2(0.0, 0.0), new Point2(0.0, leftHeight),
                new Point2(width, rightHeight), new Point2(width, 0.0),
                new Point3(1.0, 0.0, 0.0), -1.0);
            AddQuad(vertices, normals, tangents, uv, triangles,
                b0, b1, b2, b3,
                new Point2(0.0, 0.0), new Point2(width, 0.0),
                new Point2(width, rightHeight), new Point2(0.0, leftHeight),
                new Point3(1.0, 0.0, 0.0), 1.0);
            AddQuad(vertices, normals, tangents, uv, triangles,
                f0, f1, b1, b0,
                new Point2(0.0, 0.0), new Point2(width, 0.0),
                new Point2(width, thickness), new Point2(0.0, thickness),
                new Point3(1.0, 0.0, 0.0), 1.0);
            double slope = Math.Sqrt(width * width +
                (rightHeight - leftHeight) * (rightHeight - leftHeight));
            Point3 slopeTangent = new Point3(
                width / slope,
                (rightHeight - leftHeight) / slope,
                0.0);
            AddQuad(vertices, normals, tangents, uv, triangles,
                f3, b3, b2, f2,
                new Point2(0.0, 0.0), new Point2(0.0, thickness),
                new Point2(slope, thickness), new Point2(slope, 0.0),
                slopeTangent, -1.0);
            AddQuad(vertices, normals, tangents, uv, triangles,
                f0, b0, b3, f3,
                new Point2(0.0, 0.0), new Point2(thickness, 0.0),
                new Point2(thickness, leftHeight), new Point2(0.0, leftHeight),
                new Point3(0.0, 0.0, 1.0), 1.0);
            AddQuad(vertices, normals, tangents, uv, triangles,
                f1, f2, b2, b1,
                new Point2(0.0, 0.0), new Point2(0.0, rightHeight),
                new Point2(thickness, rightHeight), new Point2(thickness, 0.0),
                new Point3(0.0, 0.0, 1.0), -1.0);

            return new AdaptiveMeshData(
                vertices.ToArray(),
                normals.ToArray(),
                tangents.ToArray(),
                uv.ToArray(),
                triangles.ToArray(),
                new[] { f0, f1, f2, f3, b0, b1, b2, b3 });
        }

        private static void AddQuad(
            ICollection<Point3> vertices,
            ICollection<Point3> normals,
            ICollection<Point4> tangents,
            ICollection<Point2> uv,
            ICollection<int> triangles,
            Point3 first,
            Point3 second,
            Point3 third,
            Point3 fourth,
            Point2 uvFirst,
            Point2 uvSecond,
            Point2 uvThird,
            Point2 uvFourth,
            Point3 tangent,
            double tangentHandedness)
        {
            int start = vertices.Count;
            Point3 normal = Normalize(Cross(second - first, third - first));
            Point3 tangentUnit = Normalize(tangent);
            Point4 tangent4 = new Point4(
                tangentUnit.X,
                tangentUnit.Y,
                tangentUnit.Z,
                tangentHandedness);
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            vertices.Add(fourth);
            for (int index = 0; index < 4; ++index)
            {
                normals.Add(normal);
                tangents.Add(tangent4);
            }
            uv.Add(uvFirst);
            uv.Add(uvSecond);
            uv.Add(uvThird);
            uv.Add(uvFourth);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static Point3 Cross(Point3 left, Point3 right) =>
            new Point3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);

        private static Point3 Normalize(Point3 value)
        {
            double length = Math.Sqrt(value.LengthSquared);
            if (length < 1e-9) throw new ArgumentOutOfRangeException(nameof(value));
            return value * (1.0 / length);
        }

        private static void Validate(double value, double minimum, double maximum, string name)
        {
            if (!GeometryMath.IsFinite(value) || value < minimum || value > maximum)
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
