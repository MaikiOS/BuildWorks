using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct GuidePair3
    {
        public GuidePair3(Point3 contact, Point3 aim)
        {
            Contact = contact;
            Aim = aim;
        }

        public Point3 Contact { get; }
        public Point3 Aim { get; }
    }

    public static class ConstructionLayout
    {
        public const int MaximumCopies = 128;
        public const int MaximumContourCopies = 128;

        public static IReadOnlyList<Point3> SampleStraightRepeat(
            Point3 origin,
            Point3 step,
            int count)
        {
            ValidateCount(count);
            if (!IsFinite(origin) || !IsFinite(step) || step.LengthSquared < 0.000000000001)
            {
                throw new ArgumentOutOfRangeException(nameof(step));
            }

            Point3[] samples = new Point3[count];
            for (int index = 0; index < count; ++index)
            {
                samples[index] = origin + step * index;
            }
            return samples;
        }

        public const double ContourConnectionTolerance = 0.20;

        public static IReadOnlyList<int> OrderConnectedContour(
            IReadOnlyList<Point3[]> connectionPoints,
            int seedIndex,
            out bool closed)
        {
            closed = false;
            ValidateContourInput(connectionPoints, seedIndex);

            // Each support is an edge between two real snap points. Clustering the
            // endpoints avoids false "branches" when three pieces share one joint.
            int endpointCount = connectionPoints.Count * 2;
            int[] parents = new int[endpointCount];
            for (int endpoint = 0; endpoint < endpointCount; ++endpoint)
                parents[endpoint] = endpoint;
            double toleranceSquared =
                ContourConnectionTolerance * ContourConnectionTolerance;
            for (int left = 0; left < endpointCount; ++left)
            {
                Point3 leftPoint = connectionPoints[left / 2][left % 2];
                for (int right = left + 1; right < endpointCount; ++right)
                {
                    Point3 rightPoint = connectionPoints[right / 2][right % 2];
                    if ((leftPoint - rightPoint).LengthSquared <= toleranceSquared)
                        Union(parents, left, right);
                }
            }

            int[] firstNode = new int[connectionPoints.Count];
            int[] secondNode = new int[connectionPoints.Count];
            for (int support = 0; support < connectionPoints.Count; ++support)
            {
                firstNode[support] = FindRoot(parents, support * 2);
                secondNode[support] = FindRoot(parents, support * 2 + 1);
                if (firstNode[support] == secondNode[support])
                    return Array.Empty<int>();
            }

            // ponytail: O(n²) is deliberate; contour output is capped at 128 pieces.
            bool[] connected = new bool[connectionPoints.Count];
            connected[seedIndex] = true;
            bool changed;
            do
            {
                changed = false;
                for (int left = 0; left < connectionPoints.Count; ++left)
                {
                    if (!connected[left]) continue;
                    for (int right = 0; right < connectionPoints.Count; ++right)
                    {
                        if (connected[right] || !SharesNode(
                            firstNode, secondNode, left, right)) continue;
                        connected[right] = true;
                        changed = true;
                    }
                }
            }
            while (changed);

            int connectedCount = CountActive(connected);
            if (connectedCount > MaximumContourCopies) return Array.Empty<int>();
            int[] degrees = NodeDegrees(connected, firstNode, secondNode, endpointCount);
            int firstEndNode = -1;
            int endCount = 0;
            bool simpleChain = true;
            for (int node = 0; node < degrees.Length; ++node)
            {
                if (degrees[node] == 1)
                {
                    if (firstEndNode < 0) firstEndNode = node;
                    ++endCount;
                }
                else if (degrees[node] > 2)
                {
                    simpleChain = false;
                }
            }
            if (simpleChain && endCount == 2)
                return TraverseOpenChain(
                    connected, firstNode, secondNode, firstEndNode, connectedCount);

            bool[] inCycle = (bool[])connected.Clone();
            do
            {
                changed = false;
                degrees = NodeDegrees(inCycle, firstNode, secondNode, endpointCount);
                for (int support = 0; support < inCycle.Length; ++support)
                {
                    if (!inCycle[support] ||
                        degrees[firstNode[support]] > 1 &&
                        degrees[secondNode[support]] > 1) continue;
                    inCycle[support] = false;
                    changed = true;
                }
            }
            while (changed);

            if (!inCycle[seedIndex]) return Array.Empty<int>();
            int cycleCount = CountActive(inCycle);
            if (cycleCount < 3 || cycleCount > MaximumContourCopies) return Array.Empty<int>();
            degrees = NodeDegrees(inCycle, firstNode, secondNode, endpointCount);
            for (int node = 0; node < degrees.Length; ++node)
                if (degrees[node] != 0 && degrees[node] != 2)
                    return Array.Empty<int>();

            List<int> ring = new List<int> { seedIndex };
            int currentNode = secondNode[seedIndex];
            int previousSupport = seedIndex;
            while (ring.Count < cycleCount)
            {
                int next = FindIncidentSupport(
                    inCycle, firstNode, secondNode, currentNode, previousSupport);
                if (next < 0 || next == seedIndex) return Array.Empty<int>();
                ring.Add(next);
                currentNode = firstNode[next] == currentNode
                    ? secondNode[next]
                    : firstNode[next];
                previousSupport = next;
            }
            if (FindIncidentSupport(
                inCycle, firstNode, secondNode, currentNode, previousSupport) != seedIndex)
                return Array.Empty<int>();
            closed = true;
            return ring;
        }

        public static IReadOnlyList<int> OrderTouchingContour(
            IReadOnlyList<Point3[]> connectionPoints,
            int seedIndex,
            out bool closed)
        {
            closed = false;
            ValidateContourInput(connectionPoints, seedIndex);
            int count = connectionPoints.Count;
            int[,] choices = new int[count, 2];
            double[,] scores = new double[count, 2];
            int[,] neighbors = new int[count, 2];
            for (int index = 0; index < count; ++index)
            {
                choices[index, 0] = choices[index, 1] = -1;
                scores[index, 0] = scores[index, 1] = double.PositiveInfinity;
                neighbors[index, 0] = neighbors[index, 1] = -1;
            }

            double toleranceSquared =
                ContourConnectionTolerance * ContourConnectionTolerance;
            for (int left = 0; left < count; ++left)
            {
                Point3 start = connectionPoints[left][0];
                Point3 end = connectionPoints[left][1];
                Point3 direction = end - start;
                double length = Math.Sqrt(direction.LengthSquared);
                direction *= 1.0 / length;
                Point3 middle = (start + end) * 0.5;
                for (int right = 0; right < count; ++right)
                {
                    if (right == left || SegmentDistanceSquared(
                        start,
                        end,
                        connectionPoints[right][0],
                        connectionPoints[right][1]) > toleranceSquared)
                        continue;
                    Point3 otherMiddle =
                        (connectionPoints[right][0] + connectionPoints[right][1]) * 0.5;
                    Point3 offset = otherMiddle - middle;
                    double side = Dot(offset, direction);
                    if (Math.Abs(side) <= length * 0.05) continue;
                    double score = offset.LengthSquared;
                    int slot = side < 0.0 ? 0 : 1;
                    if (score >= scores[left, slot]) continue;
                    choices[left, slot] = right;
                    scores[left, slot] = score;
                }
            }

            for (int left = 0; left < count; ++left)
            {
                for (int slot = 0; slot < 2; ++slot)
                {
                    int right = choices[left, slot];
                    if (right < 0 ||
                        choices[right, 0] != left && choices[right, 1] != left)
                        continue;
                    AddNeighbor(neighbors, left, right);
                    AddNeighbor(neighbors, right, left);
                }
            }

            bool[] connected = new bool[count];
            connected[seedIndex] = true;
            bool changed;
            do
            {
                changed = false;
                for (int index = 0; index < count; ++index)
                {
                    if (!connected[index]) continue;
                    for (int slot = 0; slot < 2; ++slot)
                    {
                        int neighbor = neighbors[index, slot];
                        if (neighbor < 0 || connected[neighbor]) continue;
                        connected[neighbor] = true;
                        changed = true;
                    }
                }
            }
            while (changed);

            int connectedCount = CountActive(connected);
            if (connectedCount < 2 || connectedCount > MaximumContourCopies)
                return Array.Empty<int>();
            int firstEnd = -1;
            int endCount = 0;
            for (int index = 0; index < count; ++index)
            {
                if (!connected[index]) continue;
                int degree = (neighbors[index, 0] >= 0 ? 1 : 0) +
                    (neighbors[index, 1] >= 0 ? 1 : 0);
                if (degree == 1)
                {
                    if (firstEnd < 0) firstEnd = index;
                    ++endCount;
                }
                else if (degree != 2)
                {
                    return Array.Empty<int>();
                }
            }
            if (endCount != 0 && endCount != 2) return Array.Empty<int>();
            closed = endCount == 0;
            int first = closed ? seedIndex : firstEnd;
            List<int> ordered = new List<int>(connectedCount);
            int previous = -1;
            int current = first;
            while (ordered.Count < connectedCount && current >= 0)
            {
                ordered.Add(current);
                int next = neighbors[current, 0] != previous
                    ? neighbors[current, 0]
                    : neighbors[current, 1];
                previous = current;
                current = next;
            }
            if (ordered.Count != connectedCount ||
                closed && current != first ||
                !closed && current >= 0)
                return Array.Empty<int>();
            return ordered;
        }

        public static IReadOnlyList<GuidePair3> SampleStraightGuides(
            Point3 contactStart,
            Point3 contactEnd,
            bool contactIsPoint,
            Point3 aimStart,
            Point3 aimEnd,
            bool aimIsPoint,
            int count)
        {
            ValidateCount(count);
            ValidateGuide(contactStart, contactEnd, contactIsPoint, nameof(contactStart));
            ValidateGuide(aimStart, aimEnd, aimIsPoint, nameof(aimStart));

            GuidePair3[] samples = new GuidePair3[count];
            for (int index = 0; index < count; ++index)
            {
                double position = (double)index / (count - 1);
                samples[index] = new GuidePair3(
                    contactIsPoint ? contactStart : Lerp(contactStart, contactEnd, position),
                    aimIsPoint ? aimStart : Lerp(aimStart, aimEnd, position));
            }
            return samples;
        }

        private static Point3 Lerp(Point3 start, Point3 end, double position)
        {
            return start + (end - start) * position;
        }

        private static int FindRoot(int[] parents, int value)
        {
            while (parents[value] != value)
            {
                parents[value] = parents[parents[value]];
                value = parents[value];
            }
            return value;
        }

        private static void Union(int[] parents, int left, int right)
        {
            int leftRoot = FindRoot(parents, left);
            int rightRoot = FindRoot(parents, right);
            if (leftRoot != rightRoot) parents[rightRoot] = leftRoot;
        }

        private static bool SharesNode(
            int[] firstNode,
            int[] secondNode,
            int left,
            int right) =>
            firstNode[left] == firstNode[right] ||
            firstNode[left] == secondNode[right] ||
            secondNode[left] == firstNode[right] ||
            secondNode[left] == secondNode[right];

        private static void AddNeighbor(int[,] neighbors, int support, int neighbor)
        {
            if (neighbors[support, 0] == neighbor || neighbors[support, 1] == neighbor)
                return;
            if (neighbors[support, 0] < 0) neighbors[support, 0] = neighbor;
            else if (neighbors[support, 1] < 0) neighbors[support, 1] = neighbor;
        }

        private static double SegmentDistanceSquared(
            Point3 firstStart,
            Point3 firstEnd,
            Point3 secondStart,
            Point3 secondEnd)
        {
            Point3 first = firstEnd - firstStart;
            Point3 second = secondEnd - secondStart;
            Point3 offset = firstStart - secondStart;
            double a = Dot(first, first);
            double b = Dot(first, second);
            double c = Dot(second, second);
            double d = Dot(first, offset);
            double e = Dot(second, offset);
            double denominator = a * c - b * b;
            double firstNumerator;
            double secondNumerator;
            double firstDenominator = denominator;
            double secondDenominator = denominator;
            if (denominator < 0.000000000001)
            {
                firstNumerator = 0.0;
                firstDenominator = 1.0;
                secondNumerator = e;
                secondDenominator = c;
            }
            else
            {
                firstNumerator = b * e - c * d;
                secondNumerator = a * e - b * d;
                if (firstNumerator < 0.0)
                {
                    firstNumerator = 0.0;
                    secondNumerator = e;
                    secondDenominator = c;
                }
                else if (firstNumerator > firstDenominator)
                {
                    firstNumerator = firstDenominator;
                    secondNumerator = e + b;
                    secondDenominator = c;
                }
            }
            if (secondNumerator < 0.0)
            {
                secondNumerator = 0.0;
                if (-d < 0.0) firstNumerator = 0.0;
                else if (-d > a) firstNumerator = firstDenominator;
                else
                {
                    firstNumerator = -d;
                    firstDenominator = a;
                }
            }
            else if (secondNumerator > secondDenominator)
            {
                secondNumerator = secondDenominator;
                if (-d + b < 0.0) firstNumerator = 0.0;
                else if (-d + b > a) firstNumerator = firstDenominator;
                else
                {
                    firstNumerator = -d + b;
                    firstDenominator = a;
                }
            }
            double firstPosition = Math.Abs(firstNumerator) < 0.000000000001
                ? 0.0
                : firstNumerator / firstDenominator;
            double secondPosition = Math.Abs(secondNumerator) < 0.000000000001
                ? 0.0
                : secondNumerator / secondDenominator;
            Point3 separation = offset + first * firstPosition - second * secondPosition;
            return separation.LengthSquared;
        }

        private static double Dot(Point3 left, Point3 right) =>
            left.X * right.X + left.Y * right.Y + left.Z * right.Z;

        private static void ValidateContourInput(
            IReadOnlyList<Point3[]> connectionPoints,
            int seedIndex)
        {
            if (connectionPoints == null || seedIndex < 0 ||
                seedIndex >= connectionPoints.Count)
                throw new ArgumentOutOfRangeException(nameof(seedIndex));
            for (int index = 0; index < connectionPoints.Count; ++index)
            {
                Point3[] points = connectionPoints[index];
                if (points == null || points.Length != 2 ||
                    (points[0] - points[1]).LengthSquared < 0.000000000001)
                    throw new ArgumentOutOfRangeException(nameof(connectionPoints));
                for (int point = 0; point < points.Length; ++point)
                    if (!IsFinite(points[point]))
                        throw new ArgumentOutOfRangeException(nameof(connectionPoints));
            }
        }

        private static int CountActive(bool[] active)
        {
            int count = 0;
            for (int index = 0; index < active.Length; ++index)
                if (active[index]) ++count;
            return count;
        }

        private static int[] NodeDegrees(
            bool[] active,
            int[] firstNode,
            int[] secondNode,
            int nodeCount)
        {
            int[] degrees = new int[nodeCount];
            for (int support = 0; support < active.Length; ++support)
            {
                if (!active[support]) continue;
                ++degrees[firstNode[support]];
                ++degrees[secondNode[support]];
            }
            return degrees;
        }

        private static IReadOnlyList<int> TraverseOpenChain(
            bool[] active,
            int[] firstNode,
            int[] secondNode,
            int firstNodeInChain,
            int expectedCount)
        {
            List<int> chain = new List<int>();
            int currentNode = firstNodeInChain;
            int previousSupport = -1;
            while (chain.Count < expectedCount)
            {
                int next = FindIncidentSupport(
                    active, firstNode, secondNode, currentNode, previousSupport);
                if (next < 0) break;
                chain.Add(next);
                currentNode = firstNode[next] == currentNode
                    ? secondNode[next]
                    : firstNode[next];
                previousSupport = next;
            }
            return chain.Count == expectedCount ? chain : Array.Empty<int>();
        }

        private static int FindIncidentSupport(
            bool[] active,
            int[] firstNode,
            int[] secondNode,
            int node,
            int excluded)
        {
            for (int support = 0; support < active.Length; ++support)
            {
                if (active[support] && support != excluded &&
                    (firstNode[support] == node || secondNode[support] == node))
                    return support;
            }
            return -1;
        }

        private static void ValidateCount(int count)
        {
            if (count < 2 || count > MaximumCopies)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }
        }

        private static void ValidateGuide(
            Point3 start,
            Point3 end,
            bool isPoint,
            string parameterName)
        {
            if (!IsFinite(start) || !IsFinite(end) ||
                (!isPoint && (end - start).LengthSquared < 0.000000000001))
            {
                throw new ArgumentOutOfRangeException(parameterName);
            }
        }

        private static bool IsFinite(Point3 point)
        {
            return GeometryMath.IsFinite(point.X) &&
                GeometryMath.IsFinite(point.Y) &&
                GeometryMath.IsFinite(point.Z);
        }
    }
}
