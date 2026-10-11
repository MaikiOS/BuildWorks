using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    public readonly struct WorldSelectionVolume
    {
        public WorldSelectionVolume(Point3 first, Point3 second)
        {
            if (!Finite(first) || !Finite(second)) throw new ArgumentException("Non-finite selection volume.");
            Minimum = new Point3(Math.Min(first.X, second.X), Math.Min(first.Y, second.Y), Math.Min(first.Z, second.Z));
            Maximum = new Point3(Math.Max(first.X, second.X), Math.Max(first.Y, second.Y), Math.Max(first.Z, second.Z));
            Center = Minimum * .5 + Maximum * .5;
            // The host radius query uses a strict inequality, including for corner points.
            QueryRadius = Math.Sqrt((Maximum - Minimum).LengthSquared) * .5 + .01;
            if (double.IsInfinity(QueryRadius)) throw new ArgumentException("Selection volume overflow.");
        }

        public Point3 Minimum { get; }
        public Point3 Maximum { get; }
        public Point3 Center { get; }
        public double QueryRadius { get; }
        public bool Contains(Point3 position) => Finite(position) &&
            position.X >= Minimum.X && position.X <= Maximum.X &&
            position.Y >= Minimum.Y && position.Y <= Maximum.Y &&
            position.Z >= Minimum.Z && position.Z <= Maximum.Z;

        public bool TrySelect<T>(IEnumerable<T> loaded, Func<T, Point3> position,
            IEnumerable<T> included, ISet<T> excluded, int budget, out List<T> selection)
        {
            if (loaded == null || position == null || included == null || excluded == null || budget < 1)
                throw new ArgumentException("Invalid selection input.");
            var result = new List<T>();
            var seen = new HashSet<T>();
            foreach (T item in loaded)
                if (Contains(position(item)) && !excluded.Contains(item) && seen.Add(item))
                {
                    if (result.Count == budget) { selection = null; return false; }
                    result.Add(item);
                }
            foreach (T item in included)
                if (!excluded.Contains(item) && seen.Add(item))
                {
                    if (result.Count == budget) { selection = null; return false; }
                    result.Add(item);
                }
            selection = result;
            return true;
        }

        private static bool Finite(Point3 p) =>
            !double.IsNaN(p.X) && !double.IsInfinity(p.X) &&
            !double.IsNaN(p.Y) && !double.IsInfinity(p.Y) &&
            !double.IsNaN(p.Z) && !double.IsInfinity(p.Z);
    }
}
