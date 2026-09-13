using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct LayoutTransform3
    {
        public LayoutTransform3(
            Point3 position,
            Point3 rotationAxis,
            double incrementalRotationDegrees,
            double uniformScale = 1.0)
        {
            Position = position;
            RotationAxis = rotationAxis;
            IncrementalRotationDegrees = incrementalRotationDegrees;
            UniformScale = uniformScale;
        }

        public Point3 Position { get; }
        public Point3 RotationAxis { get; }
        public double IncrementalRotationDegrees { get; }
        public double UniformScale { get; }
    }

    public static class GuidePathSampling
    {
        public static IReadOnlyList<Point3> SampleLine(Point3 start, Point3 end, int count)
        {
            ValidateCount(count);
            ValidatePoint(start, nameof(start));
            ValidatePoint(end, nameof(end));
            if ((end - start).LengthSquared < 1e-12)
                throw new ArgumentOutOfRangeException(nameof(end));

            Point3[] result = new Point3[count];
            for (int index = 0; index < count; ++index)
                result[index] = Lerp(start, end, (double)index / (count - 1));
            return result;
        }

        public static IReadOnlyList<Point3> SamplePolyline(
            IReadOnlyList<Point3> points,
            int count)
        {
            ValidateCount(count);
            if (points == null || points.Count < 2)
                throw new ArgumentOutOfRangeException(nameof(points));

            double[] cumulative = new double[points.Count];
            for (int index = 0; index < points.Count; ++index)
            {
                ValidatePoint(points[index], nameof(points));
                if (index > 0)
                    cumulative[index] = cumulative[index - 1] + Length(points[index] - points[index - 1]);
            }
            double total = cumulative[cumulative.Length - 1];
            if (total < 1e-6) throw new ArgumentOutOfRangeException(nameof(points));

            Point3[] result = new Point3[count];
            result[0] = points[0];
            result[count - 1] = points[points.Count - 1];
            int segment = 1;
            for (int sample = 1; sample < count - 1; ++sample)
            {
                double target = total * sample / (count - 1);
                while (segment < cumulative.Length - 1 && cumulative[segment] < target)
                    ++segment;
                double segmentLength = cumulative[segment] - cumulative[segment - 1];
                result[sample] = segmentLength < 1e-9
                    ? points[segment]
                    : Lerp(
                        points[segment - 1],
                        points[segment],
                        (target - cumulative[segment - 1]) / segmentLength);
            }
            return result;
        }

        public static IReadOnlyList<Point3> SampleArc(
            Point3 start,
            Point3 through,
            Point3 end,
            int count)
        {
            ValidateCount(count);
            ValidatePoint(start, nameof(start));
            ValidatePoint(through, nameof(through));
            ValidatePoint(end, nameof(end));
            Point3 first = through - start;
            Point3 second = end - start;
            Point3 normal = Cross(first, second);
            double normalSquared = normal.LengthSquared;
            if (normalSquared < 1e-12) throw new ArgumentOutOfRangeException(nameof(through));

            Point3 center = start +
                (Cross(normal, first) * second.LengthSquared +
                Cross(second, normal) * first.LengthSquared) * (0.5 / normalSquared);
            Point3 startVector = Normalize(start - center);
            Point3 throughVector = Normalize(through - center);
            Point3 endVector = Normalize(end - center);
            Point3 normalUnit = Normalize(normal);
            double throughAngle = PositiveAngle(startVector, throughVector, normalUnit);
            double endAngle = PositiveAngle(startVector, endVector, normalUnit);
            if (throughAngle > endAngle + 1e-9) endAngle += Math.PI * 2.0;

            double radius = Length(start - center);
            Point3 side = Normalize(Cross(normalUnit, startVector));
            Point3[] result = new Point3[count];
            for (int index = 0; index < count; ++index)
            {
                double angle = endAngle * index / (count - 1);
                result[index] = center +
                    (startVector * Math.Cos(angle) + side * Math.Sin(angle)) * radius;
            }
            result[0] = start;
            result[count - 1] = end;
            return result;
        }

        public static IReadOnlyList<Point3> SampleBezier(
            Point3 start,
            Point3 control1,
            Point3 control2,
            Point3 end,
            int count)
        {
            ValidateCount(count);
            ValidatePoint(start, nameof(start));
            ValidatePoint(control1, nameof(control1));
            ValidatePoint(control2, nameof(control2));
            ValidatePoint(end, nameof(end));
            return SampleByDistance(
                t =>
                {
                    double u = 1.0 - t;
                    return start * (u * u * u) +
                        control1 * (3.0 * u * u * t) +
                        control2 * (3.0 * u * t * t) +
                        end * (t * t * t);
                },
                count);
        }

        private static IReadOnlyList<Point3> SampleByDistance(
            Func<double, Point3> evaluate,
            int count)
        {
            int segments = Math.Max(32, count * 8);
            Point3[] fine = new Point3[segments + 1];
            double[] cumulative = new double[segments + 1];
            fine[0] = evaluate(0.0);
            for (int index = 1; index <= segments; ++index)
            {
                fine[index] = evaluate((double)index / segments);
                cumulative[index] = cumulative[index - 1] + Length(fine[index] - fine[index - 1]);
            }
            double total = cumulative[segments];
            if (total < 1e-6) throw new ArgumentOutOfRangeException(nameof(evaluate));

            Point3[] result = new Point3[count];
            result[0] = fine[0];
            result[count - 1] = fine[segments];
            int segment = 1;
            for (int sample = 1; sample < count - 1; ++sample)
            {
                double target = total * sample / (count - 1);
                while (segment < segments && cumulative[segment] < target) ++segment;
                double length = cumulative[segment] - cumulative[segment - 1];
                result[sample] = Lerp(
                    fine[segment - 1],
                    fine[segment],
                    (target - cumulative[segment - 1]) / length);
            }
            return result;
        }

        public static IReadOnlyList<GuidePair3> PairSamples(
            IReadOnlyList<Point3> contact,
            IReadOnlyList<Point3> aim,
            int count)
        {
            ValidateCount(count);
            if (contact == null || aim == null ||
                contact.Count != 1 && contact.Count != count ||
                aim.Count != 1 && aim.Count != count)
                throw new ArgumentOutOfRangeException(nameof(contact));

            GuidePair3[] result = new GuidePair3[count];
            for (int index = 0; index < count; ++index)
            {
                Point3 first = contact[contact.Count == 1 ? 0 : index];
                Point3 second = aim[aim.Count == 1 ? 0 : index];
                ValidatePoint(first, nameof(contact));
                ValidatePoint(second, nameof(aim));
                if ((second - first).LengthSquared < 1e-12)
                    throw new ArgumentOutOfRangeException(nameof(aim));
                result[index] = new GuidePair3(first, second);
            }
            return result;
        }

        public static IReadOnlyList<LayoutTransform3> SampleRepeat(
            Point3 origin,
            Point3 step,
            int count,
            double rise,
            Point3 rotationAxis,
            double rotationDegrees,
            bool symmetric,
            Point3 backAnchor,
            Point3 frontAnchor,
            double scaleStep = 0.0)
        {
            ValidateCount(count);
            ValidatePoint(origin, nameof(origin));
            ValidatePoint(step, nameof(step));
            ValidatePoint(rotationAxis, nameof(rotationAxis));
            ValidatePoint(backAnchor, nameof(backAnchor));
            ValidatePoint(frontAnchor, nameof(frontAnchor));
            if (step.LengthSquared < 1e-12 || !GeometryMath.IsFinite(rise) ||
                !GeometryMath.IsFinite(rotationDegrees) || !GeometryMath.IsFinite(scaleStep) ||
                rotationAxis.LengthSquared < 1e-12 ||
                (frontAnchor - backAnchor).LengthSquared < 1e-12)
                throw new ArgumentOutOfRangeException(nameof(step));

            Point3 axis = Normalize(rotationAxis);
            Point3 gap = step - (frontAnchor - backAnchor);
            LayoutTransform3[] result = new LayoutTransform3[count];
            for (int index = 0; index < count; ++index)
            {
                int logical = symmetric ? SymmetricIndex(index) : index;
                double scale = 1.0 + scaleStep * logical;
                if (!GeometryMath.IsFinite(scale) || scale <= 0.0)
                    throw new ArgumentOutOfRangeException(nameof(scaleStep), "Each repeated scale must be greater than zero.");
                result[index] = new LayoutTransform3(
                    origin + RepeatOffset(
                        backAnchor,
                        frontAnchor,
                        gap,
                        axis,
                        rotationDegrees,
                        logical,
                        scaleStep) +
                        new Point3(0.0, rise * logical, 0.0),
                    axis,
                    rotationDegrees * logical,
                    scale);
            }
            return result;
        }

        private static Point3 RepeatOffset(
            Point3 backAnchor,
            Point3 frontAnchor,
            Point3 gap,
            Point3 axis,
            double rotationDegrees,
            int logical,
            double scaleStep)
        {
            Point3 offset = default;
            if (logical > 0)
            {
                for (int index = 1; index <= logical; ++index)
                {
                    offset += RotateAroundAxis(
                        frontAnchor * (1.0 + scaleStep * (index - 1)) + gap,
                        axis,
                        rotationDegrees * (index - 1));
                    offset -= RotateAroundAxis(
                        backAnchor * (1.0 + scaleStep * index),
                        axis,
                        rotationDegrees * index);
                }
            }
            else
            {
                for (int index = -1; index >= logical; --index)
                {
                    offset += RotateAroundAxis(
                        backAnchor * (1.0 + scaleStep * (index + 1)),
                        axis,
                        rotationDegrees * (index + 1));
                    offset -= RotateAroundAxis(
                        frontAnchor * (1.0 + scaleStep * index) + gap,
                        axis,
                        rotationDegrees * index);
                }
            }
            return offset;
        }

        private static Point3 RotateAroundAxis(Point3 value, Point3 axis, double degrees)
        {
            double radians = degrees * Math.PI / 180.0;
            double cosine = Math.Cos(radians);
            double sine = Math.Sin(radians);
            return value * cosine + Cross(axis, value) * sine +
                axis * (Dot(axis, value) * (1.0 - cosine));
        }

        public static IReadOnlyList<Point3> SamplePlane(
            Point3 origin,
            Point3 firstStep,
            int firstCount,
            Point3 secondStep,
            int secondCount,
            bool symmetric)
        {
            ValidateCount(firstCount);
            ValidateCount(secondCount);
            ValidatePoint(origin, nameof(origin));
            ValidatePoint(firstStep, nameof(firstStep));
            ValidatePoint(secondStep, nameof(secondStep));
            if ((long)firstCount * secondCount > ConstructionLayout.MaximumCopies ||
                firstStep.LengthSquared < 1e-12 || secondStep.LengthSquared < 1e-12 ||
                Cross(firstStep, secondStep).LengthSquared < 1e-12)
                throw new ArgumentOutOfRangeException(nameof(secondStep));

            Point3[] result = new Point3[firstCount * secondCount];
            int output = 0;
            for (int second = 0; second < secondCount; ++second)
            {
                int secondIndex = symmetric ? SymmetricIndex(second) : second;
                for (int first = 0; first < firstCount; ++first)
                {
                    int firstIndex = symmetric ? SymmetricIndex(first) : first;
                    result[output++] = origin +
                        firstStep * firstIndex + secondStep * secondIndex;
                }
            }
            return result;
        }

        private static int SymmetricIndex(int index) =>
            index == 0 ? 0 : (index & 1) != 0 ? (index + 1) / 2 : -index / 2;

        private static double PositiveAngle(Point3 from, Point3 to, Point3 normal)
        {
            double angle = Math.Atan2(Dot(normal, Cross(from, to)), Dot(from, to));
            return angle < 0.0 ? angle + Math.PI * 2.0 : angle;
        }

        private static Point3 Lerp(Point3 start, Point3 end, double t) =>
            start + (end - start) * t;

        private static double Length(Point3 value) => Math.Sqrt(value.LengthSquared);

        private static Point3 Normalize(Point3 value)
        {
            double length = Length(value);
            if (length < 1e-9) throw new ArgumentOutOfRangeException(nameof(value));
            return value * (1.0 / length);
        }

        private static double Dot(Point3 left, Point3 right) =>
            left.X * right.X + left.Y * right.Y + left.Z * right.Z;

        private static Point3 Cross(Point3 left, Point3 right) =>
            new Point3(
                left.Y * right.Z - left.Z * right.Y,
                left.Z * right.X - left.X * right.Z,
                left.X * right.Y - left.Y * right.X);

        private static void ValidateCount(int count)
        {
            if (count < 2 || count > ConstructionLayout.MaximumCopies)
                throw new ArgumentOutOfRangeException(nameof(count));
        }

        private static void ValidatePoint(Point3 point, string name)
        {
            if (!GeometryMath.IsFinite(point.X) || !GeometryMath.IsFinite(point.Y) ||
                !GeometryMath.IsFinite(point.Z))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
