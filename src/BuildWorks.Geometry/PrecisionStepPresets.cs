using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public static class PrecisionStepPresets
    {
        public static IReadOnlyList<float> Translation { get; } =
            Array.AsReadOnly(new[] { 0.01f, 0.05f, 0.10f, 0.50f, 1f });
        public static IReadOnlyList<float> Rotation { get; } =
            Array.AsReadOnly(new[] { 0.1f, 0.5f, 1f, 5f, 15f });
    }
}
