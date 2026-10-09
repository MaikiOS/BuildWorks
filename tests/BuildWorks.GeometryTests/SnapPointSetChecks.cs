using System;
using System.Collections.Generic;
using System.Diagnostics;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class SnapPointSetChecks
    {
        internal static void Run()
        {
            foreach (int bad in new[] { 0, -1, BlueprintEditorDocument.MaximumParts + 1 })
            {
                bool rejected = false;
                try { new BlueprintSnapPointSet(bad); } catch (ArgumentOutOfRangeException) { rejected = true; }
                Require(rejected, "Invalid point capacity accepted");
            }
            var bounded = new BlueprintSnapPointSet(1);
            Require(bounded.TryAdd(new Point3()) && bounded.TryAdd(new Point3(0.00009f, 0, 0)), "Duplicate at capacity refused");
            Require(!bounded.TryAdd(new Point3(1, 0, 0)) && bounded.Points.Count == 1, "Overflow changed set");
            foreach (double bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity, double.MaxValue })
            {
                Require(!bounded.TryAdd(new Point3(bad, 0, 0)) && !bounded.TryAdd(new Point3(0, bad, 0)) &&
                    !bounded.TryAdd(new Point3(0, 0, bad)) && bounded.Points.Count == 1, "Non-finite input changed set");
            }

            var edges = new List<Point3>();
            foreach (float origin in new[] { -200000000f, -1f, -0.0002f, 0f, 0.0002f, 1f, 200000000f, float.MaxValue })
                foreach (float offset in new[] { 0f, 0.00009f, 0.0001f, -0.0001f, 0.00018f, -0.00018f })
                    edges.Add(new Point3(origin + offset, offset, -offset));
            for (int bits = BitConverter.SingleToInt32Bits(0.0001f) - 4; bits <= BitConverter.SingleToInt32Bits(0.0001f) + 4; ++bits)
            {
                float near = BitConverter.Int32BitsToSingle(bits);
                edges.Add(new Point3(near, 0, 0)); edges.Add(new Point3(-near, 0, 0));
            }
            Compare(edges, 20000);
            // A near B, B near C, A not near C: only retained representatives deduplicate.
            Compare(new[] { new Point3(), new Point3(0.00009f, 0, 0), new Point3(0.00018f, 0, 0) }, 20000);
            var random = new Random(122);
            var dense = new List<Point3>();
            for (int i = 0; i < 1800; ++i)
            {
                float x = (float)(random.NextDouble() * 0.001 - 0.0005);
                float y = (float)(random.NextDouble() * 0.001 - 0.0005);
                float z = (float)(random.NextDouble() * 0.001 - 0.0005);
                dense.Add(new Point3(x, y, z));
                if (i % 3 == 0) dense.Add(new Point3(x + 0.00009f, y, z));
            }
            Compare(dense, 512); Compare(dense, 20000);
            var large = new List<Point3>();
            for (int i = 0; i < 17125; ++i)
            {
                var point = new Point3(i % 137, (i / 137) % 137, i / (137 * 137));
                large.Add(point);
                if (i % 7 == 0) large.Add(point);
            }
            Compare(large, 512); Compare(large, 20000);
        }

        private static void Compare(IReadOnlyList<Point3> input, int maximum)
        {
            var expected = new List<Point3>();
            var accepted = new bool[input.Count];
            var watch = Stopwatch.StartNew();
            for (int i = 0; i < input.Count; ++i)
            {
                var p = new Point3((float)input[i].X, (float)input[i].Y, (float)input[i].Z);
                bool duplicate = false;
                foreach (Point3 existing in expected)
                {
                    float x = (float)existing.X - (float)p.X, y = (float)existing.Y - (float)p.Y, z = (float)existing.Z - (float)p.Z;
                    if (x * x + y * y + z * z < 0.00000001f) { duplicate = true; break; }
                }
                accepted[i] = duplicate || expected.Count < maximum;
                if (!duplicate && accepted[i]) expected.Add(p);
            }
            double oldMs = watch.Elapsed.TotalMilliseconds;
            watch.Restart();
            var actual = new BlueprintSnapPointSet(maximum);
            for (int i = 0; i < input.Count; ++i)
                Require(actual.TryAdd(input[i]) == accepted[i], "Duplicate/limit result differs from float scan");
            double newMs = watch.Elapsed.TotalMilliseconds;
            Require(actual.Points.Count == expected.Count, "Retained point count changed");
            for (int i = 0; i < expected.Count; ++i)
                Require(actual.Points[i].Equals(expected[i]), "Retained point coordinates/order changed");
            if (actual.Points.Count == 17125)
                Console.WriteLine($"Capture dedup: {input.Count} inputs, {expected.Count} retained; old {oldMs:F4} ms, spatial {newMs:F4} ms. Standalone .NET, not Unity/FPS.");
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
