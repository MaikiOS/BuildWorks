using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct CubicBezierCurve
    {
        // ponytail: bounded allocation for untrusted future network input; raise only with measured placement needs.
        public const int MaximumSampleSegments = 4096;

        public CubicBezierCurve(Point2 start, Point2 control1, Point2 control2, Point2 end)
        {
            Validate(start, nameof(start));
            Validate(control1, nameof(control1));
            Validate(control2, nameof(control2));
            Validate(end, nameof(end));
            Start = start;
            Control1 = control1;
            Control2 = control2;
            End = end;
        }

        public Point2 Start { get; }
        public Point2 Control1 { get; }
        public Point2 Control2 { get; }
        public Point2 End { get; }

        public Point2 Evaluate(double t)
        {
            if (!GeometryMath.IsFinite(t) || t < 0.0 || t > 1.0)
            {
                throw new ArgumentOutOfRangeException(nameof(t), "Curve position must be between zero and one.");
            }

            double u = 1.0 - t;
            double uu = u * u;
            double tt = t * t;
            return new Point2(
                uu * u * Start.X + 3.0 * uu * t * Control1.X + 3.0 * u * tt * Control2.X + tt * t * End.X,
                uu * u * Start.Z + 3.0 * uu * t * Control1.Z + 3.0 * u * tt * Control2.Z + tt * t * End.Z);
        }

        public IReadOnlyList<Point2> Sample(int segments)
        {
            if (segments < 1 || segments > MaximumSampleSegments)
            {
                throw new ArgumentOutOfRangeException(nameof(segments), $"Segments must be between 1 and {MaximumSampleSegments}.");
            }

            Point2[] samples = new Point2[segments + 1];
            for (int i = 0; i <= segments; ++i)
            {
                samples[i] = Evaluate((double)i / segments);
            }

            return samples;
        }

        private static void Validate(Point2 point, string name)
        {
            if (!GeometryMath.IsFinite(point.X) || !GeometryMath.IsFinite(point.Z))
            {
                throw new ArgumentOutOfRangeException(name, "Curve points must be finite.");
            }
        }
    }
}
