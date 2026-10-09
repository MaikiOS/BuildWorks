using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks.Geometry
{
    // Matches Unity float squared distance and first-source-index ties.
    public sealed class BlueprintNearestPartIndex
    {
        private readonly Entry[] entries;
        private static readonly IComparer<Entry>[] Comparers =
            { new AxisComparer(0), new AxisComparer(1), new AxisComparer(2) };

        public BlueprintNearestPartIndex(IReadOnlyList<Point3> origins)
        {
            if (origins == null || origins.Count == 0 || origins.Count > BlueprintEditorDocument.MaximumParts)
                throw new ArgumentException("Invalid part origins.", nameof(origins));
            entries = new Entry[origins.Count];
            for (int index = 0; index < origins.Count; ++index)
                entries[index] = new Entry(origins[index], index);
            Build(0, entries.Length, 0);
        }

        public int FindNearest(Point3 point)
        {
            var query = new Entry(point, 0);
            int nearest = 0;
            float distance = float.PositiveInfinity;
            Search(query, 0, entries.Length, 0, ref nearest, ref distance);
            return nearest;
        }

        private void Build(int start, int count, int axis)
        {
            if (count <= 1) return;
            Array.Sort(entries, start, count, Comparers[axis]);
            int left = count / 2;
            int next = (axis + 1) % 3;
            Build(start, left, next);
            Build(start + left + 1, count - left - 1, next);
        }

        private void Search(Entry query, int start, int count, int axis, ref int nearest, ref float distance)
        {
            if (count == 0 || (distance == 0 && nearest == 0)) return;
            int left = count / 2;
            Entry entry = entries[start + left];
            float x = query.X - entry.X, y = query.Y - entry.Y, z = query.Z - entry.Z;
            float candidate = x * x + y * y + z * z;
            if (candidate < distance || (candidate == distance && entry.Index < nearest))
            { nearest = entry.Index; distance = candidate; }

            float delta = query.Axis(axis) - entry.Axis(axis);
            int next = (axis + 1) % 3;
            int rightStart = start + left + 1, rightCount = count - left - 1;
            if (delta <= 0)
            {
                Search(query, start, left, next, ref nearest, ref distance);
                // Equality can hide an earlier source index on the opposite side.
                if (delta * delta <= distance)
                    Search(query, rightStart, rightCount, next, ref nearest, ref distance);
            }
            else
            {
                Search(query, rightStart, rightCount, next, ref nearest, ref distance);
                if (delta * delta <= distance)
                    Search(query, start, left, next, ref nearest, ref distance);
            }
        }

        private readonly struct Entry
        {
            public Entry(Point3 point, int index)
            {
                X = (float)point.X; Y = (float)point.Y; Z = (float)point.Z; Index = index;
                if (float.IsNaN(X) || float.IsNaN(Y) || float.IsNaN(Z) ||
                    float.IsInfinity(X) || float.IsInfinity(Y) || float.IsInfinity(Z))
                    throw new ArgumentException("Non-finite part or snap coordinate.");
            }
            public readonly float X, Y, Z;
            public readonly int Index;
            public float Axis(int axis) => axis == 0 ? X : axis == 1 ? Y : Z;
        }

        private sealed class AxisComparer : IComparer<Entry>
        {
            private readonly int axis;
            public AxisComparer(int axis) { this.axis = axis; }
            public int Compare(Entry a, Entry b)
            {
                int result = a.Axis(axis).CompareTo(b.Axis(axis));
                return result != 0 ? result : a.Index.CompareTo(b.Index);
            }
        }
    }
}
