using System;
using System.Diagnostics;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    // Opt-in CPU measurements, not a Unity/render/game performance claim.
    internal static class BlueprintDocumentBenchmark
    {
        internal static int Run(string[] args)
        {
            int count = args.Length > 1 ? int.Parse(args[1]) : 128;
            var parts = Enumerable.Range(0, count).Select(i => new BlueprintEditorPart(
                "part" + i, "woodwall", "Wall", new Point3(i % 100, i / 100, 0),
                new Rotation3(0, 0, 0, 1))).ToArray();
            var clock = Stopwatch.StartNew();
            var document = new BlueprintEditorDocument(null, "Benchmark", "OTHER", parts);
            double open = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            for (int pass = 0; pass < 20; ++pass)
                foreach (var part in document.Parts)
                    if (!document.IsEffectivelyVisible(part.StableId) || document.IsEffectivelyLocked(part.StableId))
                        throw new InvalidOperationException("Visibility/lock mismatch.");
            double lookup = clock.Elapsed.TotalMilliseconds / 20;
            document.SelectOnly(parts[0].StableId);
            clock.Restart();
            for (int pass = 0; pass < 20; ++pass)
            {
                document.ApplyTransformDelta(new Point3(1, 0, 0), new Rotation3(0, 0, 0, 1), new Point3());
                if (!document.Undo() || !document.Redo() || !document.Undo())
                    throw new InvalidOperationException("History mismatch.");
            }
            Console.WriteLine("parts,open_ms,all_state_queries_ms,transform_undo_redo_undo_ms,managed_bytes");
            Console.WriteLine(FormattableString.Invariant($"{count},{open:F3},{lookup:F3},{clock.Elapsed.TotalMilliseconds / 20:F3},{GC.GetTotalMemory(false)}"));
            return 0;
        }
    }
}
