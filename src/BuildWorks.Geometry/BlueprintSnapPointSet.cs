using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public sealed class BlueprintSnapPointSet
    {
        private readonly int maximumPoints;
        private readonly List<Point3> points = new List<Point3>();
        private readonly Dictionary<(long, long, long), List<Point3>> cells =
            new Dictionary<(long, long, long), List<Point3>>();
        private readonly List<Point3> unindexed = new List<Point3>();

        public BlueprintSnapPointSet(int maximumPoints)
        {
            if (maximumPoints < 1 || maximumPoints > BlueprintEditorDocument.MaximumParts)
                throw new ArgumentOutOfRangeException(nameof(maximumPoints));
            this.maximumPoints = maximumPoints;
            Points = points.AsReadOnly();
        }

        public IReadOnlyList<Point3> Points { get; }

        public bool TryAdd(Point3 value)
        {
            float x = (float)value.X, y = (float)value.Y, z = (float)value.Z;
            if (float.IsNaN(x) || float.IsNaN(y) || float.IsNaN(z) ||
                float.IsInfinity(x) || float.IsInfinity(y) || float.IsInfinity(z)) return false;
            var point = new Point3(x, y, z);
            double cx = Math.Floor(x / 0.0002), cy = Math.Floor(y / 0.0002), cz = Math.Floor(z / 0.0002);
            bool indexed = Math.Abs(cx) < 1e12 && Math.Abs(cy) < 1e12 && Math.Abs(cz) < 1e12;
            if (indexed)
            {
                for (int dx = -1; dx <= 1; ++dx)
                for (int dy = -1; dy <= 1; ++dy)
                for (int dz = -1; dz <= 1; ++dz)
                    if (cells.TryGetValue(((long)cx + dx, (long)cy + dy, (long)cz + dz), out var bucket))
                        foreach (Point3 existing in bucket)
                            if (Duplicate(existing, point)) return true;
                foreach (Point3 existing in unindexed)
                    if (Duplicate(existing, point)) return true;
            }
            else
            {
                // shortcut: extreme coordinates retain the full scan; normal world ranges use cells.
                foreach (Point3 existing in points)
                    if (Duplicate(existing, point)) return true;
            }
            if (points.Count == maximumPoints) return false;
            points.Add(point);
            if (indexed)
            {
                var cell = ((long)cx, (long)cy, (long)cz);
                if (!cells.TryGetValue(cell, out var bucket)) cells.Add(cell, bucket = new List<Point3>());
                bucket.Add(point);
            }
            else unindexed.Add(point);
            return true;
        }

        private static bool Duplicate(Point3 a, Point3 b)
        {
            // Preserve the capture path's Unity float arithmetic and strict threshold.
            float x = (float)a.X - (float)b.X, y = (float)a.Y - (float)b.Y, z = (float)a.Z - (float)b.Z;
            return x * x + y * y + z * z < 0.00000001f;
        }
    }
}
