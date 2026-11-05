using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundImportChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            const string raw = "\uFEFFvtbp 2\r\n#terrain|3|-4.000|-3.000|-2.000|-1.000|0.000|1.000|2.000|3.000|4.000\r\n\r\n" +
                "wood_floor|1.25|0|2.5|0|0|0|1|AAHqcqjB\nwood_wall|-2|1|3|0|0.70710677|0|0.70710677|AAF4PGYC\r\n";
            byte[] original = new UTF8Encoding(false, true).GetBytes(raw);
            string file = Path.Combine(directory, "Unbound sample.VTBP"); File.WriteAllBytes(file, original);
            Check(ExternalBlueprintLibrary.Supported(file) && !ExternalBlueprintLibrary.Supported("wrong.vtbp2"), "Physical extension mismatch");
            var parsed = ExternalBlueprintLibrary.LoadFile(file).Single();
            Check(parsed.importError == null && parsed.name == "Unbound sample" && parsed.parts.Count == 2 &&
                parsed.importPartCount == 2 && parsed.parts[0].position.x == 1.25 && parsed.parts[0].position.z == 2.5 &&
                parsed.parts[1].position.x == -2 && parsed.parts.Select(p => p.sourceLine).SequenceEqual(new[] { 4, 5 }) &&
                parsed.parts.All(p => p.sourceRecord == 0 && p.sourceFrameApplied && p.foreignData.format == UnboundBlueprint.Format) &&
                CompositeBlueprintStore.Valid(ExternalBlueprintLibrary.DetachedCopy(parsed)), "Actual file dispatch/bindings/local frame: " + parsed.importError);
            Check(parsed.sourceDocuments.Single().text == raw && parsed.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),
                "Physical UTF8/BOM source changed");
            Check(CompositeBlueprintStore.HasForeignData(parsed) && CompositeBlueprintStore.InspectContents(parsed, ForeignZdoChecks.Hash).Unknown > 0,
                "Unconverted native packets became construction-ready");
            long budget = ExternalBlueprintLibrary.MaximumTotalBytes;
            Check(ExternalBlueprintLibrary.BrowsableFile(file, ref budget), "File browser excludes actual extension");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"), backgroundImports: true);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            Ready(store);
            var rawReview = CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash);
            var review = store.PrepareImportReview(rawReview, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Check(!Directory.Exists(store.ImportDirectory), "Review modified import library");
            Check(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error); Ready(store);
            Check(store.ImportError == null, "Import operation failed: " + store.ImportError);
            var catalog = store.CatalogAll().Single();
            Check(catalog.sourceDocuments.Single().text == raw && catalog.parts.Count == 2 && CompositeBlueprintStore.HasForeignData(catalog),
                "Actual catalog lost raw/native state or admitted it silently");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            {
                using (var bytes = new MemoryStream())
                { using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(bytes); Check(bytes.ToArray().SequenceEqual(original), "Archived original bytes differ"); }
                using (var reader = new StreamReader(zip.GetEntry("original/name.txt").Open(), Encoding.UTF8))
                    Check(reader.ReadToEnd() == Path.GetFileName(file), "Original filename lost");
                using (var reader = new StreamReader(zip.GetEntry("document_0.buildworks.json").Open(), Encoding.UTF8))
                    Check(BlueprintExchangeFormat.Decode(reader.ReadToEnd()).sourceDocuments.Single().text == raw, "Prepared archive document lost source");
            }
            Check(store.TryGetOrCreatePlacementCopy(catalog, out var saved, out error), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            var portable = BlueprintExchangeFormat.Encode(saved);
            Check(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)) && reopened.parts[0].foreignData.zdo == "AAHqcqjB",
                "Native36 reload changed data");
            string portablePath = Path.Combine(directory, "copy.buildworks.json"); File.WriteAllBytes(portablePath, portable);
            var reimported = ExternalBlueprintLibrary.LoadFile(portablePath).Single(); reimported.id = saved.id;
            Check(reimported.importError == null && BlueprintExchangeFormat.Encode(reimported).SequenceEqual(portable), "Portable24 file reimport differs");
            string mixed = Path.Combine(directory, "mixed.zip");
            using (var zip = ZipFile.Open(mixed, ZipArchiveMode.Create))
            {
                using (var output = zip.CreateEntry("folder/sample.vtbp").Open()) output.Write(original, 0, original.Length);
                using (var writer = new StreamWriter(zip.CreateEntry("legacy.blueprint").Open())) writer.Write("#Pieces\nwood_floor;;0;0;0;0;0;0;1;\"\"\n");
                using (var writer = new StreamWriter(zip.CreateEntry("manifest.json").Open())) writer.Write("{\"name\":\"package\"}");
            }
            var zipped = ExternalBlueprintLibrary.LoadFile(mixed);
            Check(zipped.Count == 2 && zipped.All(p => p.importError == null) && zipped.Single(p => p.sourceDocuments[0].format == UnboundBlueprint.Format)
                .sourceDocuments[0].text == raw, "Mixed ZIP discovery/source preservation");
            int rejected = 0;
            foreach (string bad in new[] { "vtbp 2", "vtbp 3\n" + raw, raw.Replace("wood_floor|1.25", "wood_floor|513"),
                raw.Replace("AAHqcqjB", "AAF4PGYC"), raw.Replace("wood_floor|", new string('x', 129) + "|"), raw + "garbage" })
            {
                var blocked = ExternalBlueprintLibrary.Parse(bad, "bad.vtbp");
                Check(blocked.importError != null && blocked.parts.Count == 0, "Malformed input exposed partial parts"); ++rejected;
            }
            string invalidUtf8 = Path.Combine(directory, "invalid.vtbp"); File.WriteAllBytes(invalidUtf8, new byte[] { 0xff, 0xfe, 0, 0 });
            Check(ExternalBlueprintLibrary.LoadFile(invalidUtf8).Single().importError != null, "Invalid UTF8 accepted"); ++rejected;
            string stale = Path.Combine(directory, "stale.vtbp"); File.WriteAllBytes(stale, original);
            var staleReview = CompositeBlueprintStore.ReviewImportFile(stale, ForeignZdoChecks.Hash);
            File.AppendAllText(stale, "\n");
            Check(store.TryImportReviewed(staleReview, false, false, ForeignZdoChecks.Hash, out error), error); Ready(store);
            Check(store.ImportError != null && Directory.GetFiles(store.ImportDirectory, "*.zip").Length == 1, "Changed source committed another archive"); ++rejected;
            Check(File.ReadAllBytes(file).SequenceEqual(original), "Producer input changed");
            Console.WriteLine("PASS Unbound physical UTF8/BOM/.vtbp/mixedZIP/review/import/archive/catalog/native36/portable24 cycle;" + rejected +
                " malformed/stale controls. Synthetic file only; native state conversion, author output and game NOT RUN.");
        }
        private static void Ready(CompositeBlueprintStore store) => Check(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Import/catalog timeout");
        private static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    }
}
