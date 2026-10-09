using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class NearestPartChecks
    {
        internal static void Run()
        {
            Reject(() => new BlueprintNearestPartIndex(null));
            Reject(() => new BlueprintNearestPartIndex(Array.Empty<Point3>()));
            Reject(() => new BlueprintNearestPartIndex(new Point3[BlueprintEditorDocument.MaximumParts + 1]));
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
            {
                Reject(() => new BlueprintNearestPartIndex(new[] { new Point3(bad, 0, 0) }));
                var valid = new BlueprintNearestPartIndex(new[] { new Point3() });
                Reject(() => valid.FindNearest(new Point3(0, bad, 0)));
            }
            Check(new[] { new Point3(-1, 0, 0), new Point3(1, 0, 0) }, new[] { new Point3() });
            Check(new[] { new Point3(1, 0, 0), new Point3(-1, 0, 0) }, new[] { new Point3() });
            // The farther side of the split contains index0 at exactly the same float distance.
            Check(new[] { new Point3(1, 0, 0), new Point3(-1, 0, 0), new Point3(0, 2, 0) }, new[] { new Point3() });
            var random = new Random(120);
            foreach (int count in new[] { 1, 2, 3, 16, 127, 1024, 6265 })
            {
                Point3[] parts = Enumerable.Range(0, count).Select(_ => Next(random)).ToArray();
                Point3[] original = (Point3[])parts.Clone();
                Point3[] queries = Enumerable.Range(0, 500).Select(_ => Next(random)).Concat(parts.Take(30)).ToArray();
                Check(parts, queries);
                if (!parts.SequenceEqual(original)) throw new InvalidOperationException("Origins reordered.");
                Check(parts.Select(p => new Point3(p.X, 0, 0)).ToArray(), queries);
                Check(parts.Select(p => new Point3(0, p.Y, p.Z)).ToArray(), queries);
            }
            var grid = Enumerable.Range(0, 1024).Select(i => new Point3(i % 8, (i / 8) % 8, i / 64)).ToArray();
            Check(grid.Reverse().ToArray(), grid.Select(p => p + new Point3(0.5, 0.5, 0.5)).ToArray());
            // Underflow, overflowing float squared distance, and rounded ties use the old rule too.
            Check(new[] { new Point3(float.MaxValue, 0, 0), new Point3(-float.MaxValue, 0, 0) },
                new[] { new Point3(), new Point3(float.MaxValue, float.MaxValue, 0) });
            Check(new[] { new Point3(1e-30f, 0, 0), new Point3() }, new[] { new Point3() });
            Check(new[] { new Point3(1, 0.0001f, 0), new Point3(1, 0, 0) }, new[] { new Point3() });
            var coincident = new BlueprintNearestPartIndex(new Point3[BlueprintEditorDocument.MaximumParts]);
            if (coincident.FindNearest(new Point3()) != 0) throw new InvalidOperationException("Coincident tie changed.");
            var large = Enumerable.Range(0, BlueprintEditorDocument.MaximumParts)
                .Select(i => new Point3(i % 250, i / 250, 0)).ToArray();
            Check(large, new[] { large[large.Length - 1], new Point3(250, 200, -512), new Point3(-512, -512, 512) });
            var mutable = new[] { new Point3(1, 0, 0), new Point3(10, 0, 0) };
            var snapshot = new BlueprintNearestPartIndex(mutable);
            mutable[0] = new Point3(100, 0, 0);
            if (snapshot.FindNearest(new Point3()) != 0) throw new InvalidOperationException("Index borrowed mutable source.");
        }

        internal static int BruteForce(Point3[] parts, Point3 query)
        {
            int nearest = 0;
            float distance = float.PositiveInfinity;
            for (int index = 0; index < parts.Length; ++index)
            {
                float x = (float)query.X - (float)parts[index].X;
                float y = (float)query.Y - (float)parts[index].Y;
                float z = (float)query.Z - (float)parts[index].Z;
                float candidate = x * x + y * y + z * z;
                if (candidate < distance) { nearest = index; distance = candidate; }
            }
            return nearest;
        }

        private static Point3 Next(Random random) => new Point3(
            (float)(random.NextDouble() * 1024 - 512), (float)(random.NextDouble() * 1024 - 512),
            (float)(random.NextDouble() * 1024 - 512));

        private static void Check(Point3[] parts, Point3[] queries)
        {
            var index = new BlueprintNearestPartIndex(parts);
            foreach (Point3 query in queries)
                if (index.FindNearest(query) != BruteForce(parts, query))
                    throw new InvalidOperationException("Nearest source ID differs from full float scan.");
        }

        private static void Reject(Action action)
        {
            try { action(); } catch (ArgumentException) { return; }
            throw new InvalidOperationException("Invalid nearest-part input accepted.");
        }
    }
}
