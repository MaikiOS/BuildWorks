using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    // Opt-in read-only source assignment benchmark; not a parser bypass or game benchmark.
    internal static class NearestPartBenchmark
    {
        internal static int Run(string[] args)
        {
            if (args.Length != 3) throw new ArgumentException("--snap-benchmark <archive.zip> <report.json>");
            string archivePath = Path.GetFullPath(args[1]), reportPath = Path.GetFullPath(args[2]);
            if (string.Equals(archivePath, reportPath, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Report must not overwrite the archive.");
            string before = Hash(archivePath);
            var results = new List<object>();
            using (var archive = ZipFile.OpenRead(archivePath))
            foreach (string name in new[] { "Arani_Forte_Negro.blueprint", "Big_Bridge_1.blueprint", "Decay.blueprint", "Flabellum.blueprint" })
            {
                ZipArchiveEntry[] entries = archive.Entries.Where(entry => entry.Name == name).ToArray();
                if (entries.Length != 1 || entries[0].Length > 8 * 1024 * 1024)
                    throw new InvalidDataException("Unexpected source entry: " + name);
                var parts = new List<Point3>();
                var points = new List<Point3>();
                string section = "";
                using (var reader = new StreamReader(entries[0].Open()))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.StartsWith("#", StringComparison.Ordinal)) { section = line; continue; }
                        if (line.Length == 0) continue;
                        if (section == "#SnapPoints") points.Add(Position(line, 0));
                        else if (section == "#Pieces") parts.Add(Position(line, 2));
                        if (parts.Count > BlueprintEditorDocument.MaximumParts || points.Count > 50000)
                            throw new InvalidDataException("Oversized source assignment set.");
                    }
                }
                Point3[] origins = parts.ToArray(), queries = points.ToArray();
                int[] expected = queries.Select(point => NearestPartChecks.BruteForce(origins, point)).ToArray();
                var warm = new BlueprintNearestPartIndex(origins);
                for (int i = 0; i < queries.Length; ++i)
                    if (warm.FindNearest(queries[i]) != expected[i]) throw new InvalidDataException("Assignment differs: " + name);

                var buildTimes = new List<double>(5); var queryTimes = new List<double>(5); var bruteTimes = new List<double>(5);
                long allocated = 0;
                for (int pass = 0; pass < 5; ++pass)
                {
                    var clock = Stopwatch.StartNew();
                    long memory = GC.GetAllocatedBytesForCurrentThread();
                    var index = new BlueprintNearestPartIndex(origins);
                    clock.Stop();
                    allocated = GC.GetAllocatedBytesForCurrentThread() - memory;
                    buildTimes.Add(clock.Elapsed.TotalMilliseconds);
                    clock.Restart();
                    var actual = new int[queries.Length];
                    for (int i = 0; i < queries.Length; ++i) actual[i] = index.FindNearest(queries[i]);
                    queryTimes.Add(clock.Elapsed.TotalMilliseconds);
                    if (!actual.SequenceEqual(expected)) throw new InvalidDataException("Repeated assignment differs: " + name);
                    clock.Restart();
                    for (int i = 0; i < queries.Length; ++i)
                        if (NearestPartChecks.BruteForce(origins, queries[i]) != expected[i])
                            throw new InvalidDataException("Reference differs: " + name);
                    bruteTimes.Add(clock.Elapsed.TotalMilliseconds);
                }
                int[] counts = new int[origins.Length];
                foreach (int index in expected) ++counts[index];
                results.Add(new {
                    source = entries[0].FullName, parts = origins.Length, points = queries.Length,
                    allSourceIndicesEqual = true, maximumAssignedPointsPerPart = counts.Max(),
                    naiveDistanceEvaluations = (long)origins.Length * queries.Length,
                    medianBuildMs = Median(buildTimes), medianQueryMs = Median(queryTimes),
                    medianFullScanMs = Median(bruteTimes), indexBuildAllocatedBytesLastPass = allocated
                });
            }
            if (Hash(archivePath) != before) throw new InvalidDataException("Source archive changed.");
            string json = JsonSerializer.Serialize(new {
                utc = DateTime.UtcNow, archiveSha256 = before, repetitions = 5, sourceChanged = false,
                scope = "Production nearest-part index vs old float full scan for every raw source point. No rotation/scale, import acceptance, Unity rendering, Valheim or network proof.",
                measurements = results
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(reportPath, json);
            Console.WriteLine(json);
            return 0;
        }

        private static double Median(List<double> values) { values.Sort(); return values[values.Count / 2]; }
        private static string Hash(string path)
        { using (var stream = File.OpenRead(path)) return Convert.ToHexString(SHA256.HashData(stream)); }

        private static Point3 Position(string row, int start)
        {
            string[] fields = row.Split(';');
            if ((start == 0 && fields.Length != 3) || fields.Length < start + 3)
                throw new InvalidDataException("Malformed source coordinate row.");
            float[] xyz = new float[3];
            for (int i = 0; i < 3; ++i)
            {
                xyz[i] = float.Parse(fields[start + i], NumberStyles.Float, CultureInfo.InvariantCulture);
                if (float.IsNaN(xyz[i]) || Math.Abs(xyz[i]) > 512)
                    throw new InvalidDataException("Source coordinate out of document bounds.");
            }
            return new Point3(xyz[0], xyz[1], xyz[2]);
        }
    }
}
