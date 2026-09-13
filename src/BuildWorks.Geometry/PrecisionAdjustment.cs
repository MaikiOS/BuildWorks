using System;

namespace OstrixMods.BuildWorks.Geometry
{
    public static class PrecisionAdjustment
    {
        public const double MaximumOffset = 10.0;

        public static double Quantize(double value, double step)
        {
            if (!GeometryMath.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Value must be finite.");
            }
            if (!GeometryMath.IsFinite(step) || step <= 0.0)
            {
                throw new ArgumentOutOfRangeException(nameof(step), "Step must be finite and positive.");
            }

            double result = Math.Round(value / step, MidpointRounding.AwayFromZero) * step;
            return Math.Abs(result) < step * 1e-9 ? 0.0 : result;
        }

        public static double NormalizeDegrees(double degrees)
        {
            if (!GeometryMath.IsFinite(degrees))
            {
                throw new ArgumentOutOfRangeException(nameof(degrees), "Angle must be finite.");
            }

            double normalized = degrees % 360.0;
            if (normalized >= 180.0)
            {
                normalized -= 360.0;
            }
            if (normalized < -180.0)
            {
                normalized += 360.0;
            }
            return normalized;
        }

        public static double ScreenAlignedRotationDelta(
            double screenDeltaDegrees,
            double axisTowardCamera)
        {
            if (!GeometryMath.IsFinite(screenDeltaDegrees) ||
                !GeometryMath.IsFinite(axisTowardCamera))
            {
                throw new ArgumentOutOfRangeException(nameof(screenDeltaDegrees));
            }
            return screenDeltaDegrees * (axisTowardCamera >= 0.0 ? 1.0 : -1.0);
        }

        public static double SignedForDirection(double value, double directionProjection)
        {
            if (!GeometryMath.IsFinite(value) ||
                !GeometryMath.IsFinite(directionProjection))
            {
                throw new ArgumentOutOfRangeException(nameof(value));
            }
            return directionProjection < 0.0 ? -value : value;
        }

        public static double ClampOffset(double value)
        {
            if (!GeometryMath.IsFinite(value))
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Offset must be finite.");
            }
            return Math.Max(-MaximumOffset, Math.Min(MaximumOffset, value));
        }
    }
}
