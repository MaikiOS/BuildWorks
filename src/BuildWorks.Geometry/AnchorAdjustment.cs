using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct Point3
    {
        public Point3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double LengthSquared => X * X + Y * Y + Z * Z;

        public static Point3 operator +(Point3 left, Point3 right) =>
            new Point3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Point3 operator -(Point3 left, Point3 right) =>
            new Point3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Point3 operator *(Point3 value, double scale) =>
            new Point3(value.X * scale, value.Y * scale, value.Z * scale);
    }

    public readonly struct Rotation3
    {
        public Rotation3(double x, double y, double z, double w)
        {
            X = x;
            Y = y;
            Z = z;
            W = w;
        }

        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double W { get; }

        public Rotation3 Normalized()
        {
            double length = Math.Sqrt(X * X + Y * Y + Z * Z + W * W);
            if (!GeometryMath.IsFinite(length) || length < 0.000001)
            {
                throw new ArgumentOutOfRangeException(nameof(length));
            }
            double inverse = 1.0 / length;
            return new Rotation3(X * inverse, Y * inverse, Z * inverse, W * inverse);
        }

        public Point3 Rotate(Point3 value)
        {
            Rotation3 rotation = Normalized();
            Point3 axis = new Point3(rotation.X, rotation.Y, rotation.Z);
            Point3 twiceCross = Cross(axis, value) * 2.0;
            return value + twiceCross * rotation.W + Cross(axis, twiceCross);
        }

        private static Point3 Cross(Point3 left, Point3 right)
        {
            return new Point3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);
        }
    }

    public readonly struct Edge3
    {
        public Edge3(Point3 start, Point3 end)
        {
            Start = start;
            End = end;
        }

        public Point3 Start { get; }
        public Point3 End { get; }
    }

    public readonly struct AnchorBounds
    {
        internal AnchorBounds(Point3 minimum, Point3 maximum)
        {
            Minimum = minimum;
            Maximum = maximum;
        }

        public Point3 Minimum { get; }
        public Point3 Maximum { get; }
        public Point3 Center => (Minimum + Maximum) * 0.5;

        public Point3 Corner(int index)
        {
            if (index < 0 || index > 7)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return new Point3(
                (index & 1) == 0 ? Minimum.X : Maximum.X,
                (index & 2) == 0 ? Minimum.Y : Maximum.Y,
                (index & 4) == 0 ? Minimum.Z : Maximum.Z);
        }

        public Point3 Anchor(int index)
        {
            if (index < 0 || index >= AnchorAdjustment.AnchorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            if (index < 8)
            {
                return Corner(index);
            }

            int edge = index - 8;
            int direction = edge / 4;
            int sides = edge % 4;
            bool firstMaximum = (sides & 1) != 0;
            bool secondMaximum = (sides & 2) != 0;
            switch (direction)
            {
                case 0:
                    return new Point3(
                        Center.X,
                        firstMaximum ? Maximum.Y : Minimum.Y,
                        secondMaximum ? Maximum.Z : Minimum.Z);
                case 1:
                    return new Point3(
                        firstMaximum ? Maximum.X : Minimum.X,
                        Center.Y,
                        secondMaximum ? Maximum.Z : Minimum.Z);
                default:
                    return new Point3(
                        firstMaximum ? Maximum.X : Minimum.X,
                        secondMaximum ? Maximum.Y : Minimum.Y,
                        Center.Z);
            }
        }
    }

    public static class AnchorAdjustment
    {
        public const int AnchorCount = 20;
        public const int CenterAnchorIndex = AnchorCount;
        public const int SelectableAnchorCount = AnchorCount + 1;

        public static int OppositeAnchor(int index)
        {
            if (index < 0 || index >= AnchorCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return index < 8 ? index ^ 7 : index - ((index - 8) % 4) + (((index - 8) % 4) ^ 3);
        }

        public static AnchorBounds CreateBounds(IEnumerable<Point3> points)
        {
            if (points == null)
            {
                throw new ArgumentNullException(nameof(points));
            }

            Point3 minimum = new Point3(
                double.PositiveInfinity,
                double.PositiveInfinity,
                double.PositiveInfinity);
            Point3 maximum = new Point3(
                double.NegativeInfinity,
                double.NegativeInfinity,
                double.NegativeInfinity);
            bool found = false;
            foreach (Point3 point in points)
            {
                if (!IsFinite(point))
                {
                    throw new ArgumentOutOfRangeException(nameof(points));
                }
                minimum = new Point3(
                    Math.Min(minimum.X, point.X),
                    Math.Min(minimum.Y, point.Y),
                    Math.Min(minimum.Z, point.Z));
                maximum = new Point3(
                    Math.Max(maximum.X, point.X),
                    Math.Max(maximum.Y, point.Y),
                    Math.Max(maximum.Z, point.Z));
                found = true;
            }
            if (!found || (maximum - minimum).LengthSquared < 0.0001)
            {
                throw new ArgumentOutOfRangeException(nameof(points));
            }
            return new AnchorBounds(minimum, maximum);
        }

        public static Point3 PositionForFixedAnchor(
            Point3 fixedWorld,
            Point3 fixedLocal,
            Rotation3 rotation)
        {
            if (!IsFinite(fixedWorld) || !IsFinite(fixedLocal))
            {
                throw new ArgumentOutOfRangeException(nameof(fixedWorld));
            }
            return fixedWorld - rotation.Rotate(fixedLocal);
        }

        public static Point3 CompensateFocusOffset(
            Point3 currentOffset,
            Point3 oldPosition,
            Point3 newPosition)
        {
            if (!IsFinite(currentOffset) || !IsFinite(oldPosition) || !IsFinite(newPosition))
            {
                throw new ArgumentOutOfRangeException(nameof(currentOffset));
            }
            return currentOffset + oldPosition - newPosition;
        }

        public static double ConstrainedAngleDegrees(
            Point3 startVector,
            Point3 targetVector,
            Point3 axis)
        {
            if (!IsFinite(startVector) || !IsFinite(targetVector) || !IsFinite(axis))
            {
                throw new ArgumentOutOfRangeException(nameof(startVector));
            }

            double axisLength = Math.Sqrt(axis.LengthSquared);
            if (axisLength < 0.000001)
            {
                throw new ArgumentOutOfRangeException(nameof(axis));
            }
            Point3 normalizedAxis = axis * (1.0 / axisLength);
            Point3 start = startVector - normalizedAxis * Dot(startVector, normalizedAxis);
            Point3 target = targetVector - normalizedAxis * Dot(targetVector, normalizedAxis);
            if (start.LengthSquared < 0.000001 || target.LengthSquared < 0.000001)
            {
                throw new ArgumentOutOfRangeException(nameof(targetVector));
            }

            double sine = Dot(normalizedAxis, Cross(start, target));
            double cosine = Dot(start, target);
            return Math.Atan2(sine, cosine) * 180.0 / Math.PI;
        }

        public static IReadOnlyList<Edge3> ConnectableSnapEdges(
            IReadOnlyList<Point3> points)
        {
            if (points == null) throw new ArgumentNullException(nameof(points));
            List<Point3> unique = new List<Point3>();
            foreach (Point3 point in points)
            {
                if (!IsFinite(point)) throw new ArgumentOutOfRangeException(nameof(points));
                bool duplicate = false;
                foreach (Point3 existing in unique)
                {
                    if ((existing - point).LengthSquared < 0.00000001)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (!duplicate) unique.Add(point);
            }

            List<Edge3> edges = new List<Edge3>();
            for (int first = 0; first < unique.Count; ++first)
            {
                for (int second = first + 1; second < unique.Count; ++second)
                {
                    Point3 middle = (unique[first] + unique[second]) * 0.5;
                    double radiusSquared = (unique[first] - middle).LengthSquared;
                    bool blocked = false;
                    for (int other = 0; other < unique.Count; ++other)
                    {
                        if (other == first || other == second) continue;
                        if ((unique[other] - middle).LengthSquared <=
                            radiusSquared + Math.Max(0.00000001, radiusSquared * 0.000001))
                        {
                            blocked = true;
                            break;
                        }
                    }
                    if (!blocked) edges.Add(new Edge3(unique[first], unique[second]));
                }
            }
            return edges;
        }

        public static IReadOnlyList<Point3> ExternalCompositeSnapPoints(
            IReadOnlyList<IReadOnlyList<Point3>> parts,
            double tolerance = 0.12)
        {
            if (parts == null) throw new ArgumentNullException(nameof(parts));
            if (!GeometryMath.IsFinite(tolerance) || tolerance <= 0.0)
                throw new ArgumentOutOfRangeException(nameof(tolerance));

            double toleranceSquared = tolerance * tolerance;
            List<SnapCluster> clusters = new List<SnapCluster>();
            for (int part = 0; part < parts.Count; ++part)
            {
                IReadOnlyList<Point3> points = parts[part] ??
                    throw new ArgumentNullException(nameof(parts));
                foreach (Point3 point in points)
                {
                    if (!IsFinite(point)) throw new ArgumentOutOfRangeException(nameof(parts));
                    SnapCluster cluster = null;
                    foreach (SnapCluster candidate in clusters)
                    {
                        if ((candidate.Point - point).LengthSquared <= toleranceSquared)
                        {
                            cluster = candidate;
                            break;
                        }
                    }
                    if (cluster == null)
                    {
                        cluster = new SnapCluster(point, part);
                        clusters.Add(cluster);
                    }
                    else if (cluster.Owner != part)
                    {
                        cluster.Shared = true;
                    }
                }
            }

            List<Point3> external = new List<Point3>();
            foreach (SnapCluster cluster in clusters)
            {
                if (!cluster.Shared) external.Add(cluster.Point);
            }
            return external;
        }

        public static IReadOnlyList<Edge3> ExtractFeatureEdges(
            IReadOnlyList<Point3> vertices,
            IReadOnlyList<int> triangles,
            double creaseDegrees = 25.0)
        {
            if (vertices == null) throw new ArgumentNullException(nameof(vertices));
            if (triangles == null) throw new ArgumentNullException(nameof(triangles));
            if (triangles.Count % 3 != 0 || !GeometryMath.IsFinite(creaseDegrees) ||
                creaseDegrees < 0.0 || creaseDegrees > 90.0)
            {
                throw new ArgumentOutOfRangeException(nameof(triangles));
            }

            Dictionary<VertexKey, int> weldedIds = new Dictionary<VertexKey, int>();
            List<Point3> weldedVertices = new List<Point3>();
            int[] vertexMap = new int[vertices.Count];
            for (int index = 0; index < vertices.Count; ++index)
            {
                Point3 vertex = vertices[index];
                if (!IsFinite(vertex)) throw new ArgumentOutOfRangeException(nameof(vertices));
                VertexKey key = new VertexKey(vertex);
                if (!weldedIds.TryGetValue(key, out int welded))
                {
                    welded = weldedVertices.Count;
                    weldedIds.Add(key, welded);
                    weldedVertices.Add(vertex);
                }
                vertexMap[index] = welded;
            }

            double creaseCosine = Math.Cos(creaseDegrees * Math.PI / 180.0);
            Dictionary<EdgeKey, EdgeUse> uses = new Dictionary<EdgeKey, EdgeUse>();
            for (int index = 0; index < triangles.Count; index += 3)
            {
                int first = TriangleVertex(triangles[index], vertexMap);
                int second = TriangleVertex(triangles[index + 1], vertexMap);
                int third = TriangleVertex(triangles[index + 2], vertexMap);
                Point3 normal = Cross(
                    weldedVertices[second] - weldedVertices[first],
                    weldedVertices[third] - weldedVertices[first]);
                double length = Math.Sqrt(normal.LengthSquared);
                if (first == second || second == third || third == first || length < 0.000001)
                {
                    continue;
                }
                normal = normal * (1.0 / length);
                AddEdgeUse(uses, first, second, normal, creaseCosine);
                AddEdgeUse(uses, second, third, normal, creaseCosine);
                AddEdgeUse(uses, third, first, normal, creaseCosine);
            }

            List<EdgeKey> keys = new List<EdgeKey>(uses.Keys);
            keys.Sort((left, right) => left.First != right.First
                ? left.First.CompareTo(right.First)
                : left.Second.CompareTo(right.Second));
            List<Edge3> result = new List<Edge3>();
            foreach (EdgeKey key in keys)
            {
                EdgeUse use = uses[key];
                if (use.FaceCount == 1 || use.FaceCount > 2 || use.IsCrease)
                {
                    result.Add(new Edge3(
                        weldedVertices[key.First],
                        weldedVertices[key.Second]));
                }
            }
            return result;
        }

        public static double PerspectiveSegmentParameter(
            double screenParameter,
            double startDepth,
            double endDepth)
        {
            if (!GeometryMath.IsFinite(screenParameter) || screenParameter < 0.0 ||
                screenParameter > 1.0 || !GeometryMath.IsFinite(startDepth) ||
                !GeometryMath.IsFinite(endDepth) || startDepth <= 0.0 || endDepth <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(screenParameter));
            }
            return screenParameter * startDepth /
                ((1.0 - screenParameter) * endDepth + screenParameter * startDepth);
        }

        private static int TriangleVertex(int index, int[] vertexMap)
        {
            if (index < 0 || index >= vertexMap.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
            return vertexMap[index];
        }

        private static void AddEdgeUse(
            IDictionary<EdgeKey, EdgeUse> uses,
            int first,
            int second,
            Point3 normal,
            double creaseCosine)
        {
            EdgeKey key = new EdgeKey(first, second);
            if (!uses.TryGetValue(key, out EdgeUse use))
            {
                uses.Add(key, new EdgeUse(normal));
                return;
            }
            ++use.FaceCount;
            use.IsCrease |= Math.Abs(Dot(use.Normal, normal)) < creaseCosine;
            uses[key] = use;
        }

        private static double Dot(Point3 left, Point3 right) =>
            left.X * right.X + left.Y * right.Y + left.Z * right.Z;

        private static Point3 Cross(Point3 left, Point3 right) =>
            new Point3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);

        private readonly struct VertexKey : IEquatable<VertexKey>
        {
            private const double WeldScale = 100000.0;

            public VertexKey(Point3 point)
            {
                X = Quantize(point.X);
                Y = Quantize(point.Y);
                Z = Quantize(point.Z);
            }

            private long X { get; }
            private long Y { get; }
            private long Z { get; }

            public bool Equals(VertexKey other) => X == other.X && Y == other.Y && Z == other.Z;
            public override bool Equals(object obj) => obj is VertexKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = X.GetHashCode();
                    hash = hash * 397 ^ Y.GetHashCode();
                    return hash * 397 ^ Z.GetHashCode();
                }
            }

            private static long Quantize(double value)
            {
                if (Math.Abs(value) > 1000000000.0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value));
                }
                return (long)Math.Round(value * WeldScale, MidpointRounding.AwayFromZero);
            }
        }

        private readonly struct EdgeKey : IEquatable<EdgeKey>
        {
            public EdgeKey(int first, int second)
            {
                First = Math.Min(first, second);
                Second = Math.Max(first, second);
            }

            public int First { get; }
            public int Second { get; }

            public bool Equals(EdgeKey other) => First == other.First && Second == other.Second;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode() => unchecked(First * 397 ^ Second);
        }

        private struct EdgeUse
        {
            public EdgeUse(Point3 normal)
            {
                Normal = normal;
                FaceCount = 1;
                IsCrease = false;
            }

            public Point3 Normal;
            public int FaceCount;
            public bool IsCrease;
        }

        private sealed class SnapCluster
        {
            public SnapCluster(Point3 point, int owner)
            {
                Point = point;
                Owner = owner;
            }

            public Point3 Point { get; }
            public int Owner { get; }
            public bool Shared { get; set; }
        }

        private static bool IsFinite(Point3 value)
        {
            return GeometryMath.IsFinite(value.X) &&
                GeometryMath.IsFinite(value.Y) &&
                GeometryMath.IsFinite(value.Z);
        }
    }

    public sealed class BoundedUndoHistory<T>
    {
        private readonly int capacity;
        private readonly List<T> states = new List<T>();
        private int current = -1;

        public BoundedUndoHistory(int capacity)
        {
            if (capacity < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity));
            }
            this.capacity = capacity;
        }

        public int UndoCount => Math.Max(0, current);
        public int RedoCount => Math.Max(0, states.Count - current - 1);

        public void Reset(T initial)
        {
            states.Clear();
            states.Add(initial);
            current = 0;
        }

        public void Commit(T state)
        {
            if (current + 1 < states.Count)
                states.RemoveRange(current + 1, states.Count - current - 1);
            if (states.Count == capacity + 1)
            {
                states.RemoveAt(0);
                --current;
            }
            states.Add(state);
            current = states.Count - 1;
        }

        public bool TryUndo(out T state)
        {
            if (current <= 0)
            {
                state = default;
                return false;
            }
            state = states[--current];
            return true;
        }

        public bool TryRedo(out T state)
        {
            if (current < 0 || current + 1 >= states.Count)
            {
                state = default;
                return false;
            }
            state = states[++current];
            return true;
        }

        public void Clear()
        {
            states.Clear();
            current = -1;
        }
    }
}
