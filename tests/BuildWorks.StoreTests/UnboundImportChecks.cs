using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class UnboundImportChecks
    {
        internal static void FileCycle(string file, string expectedHash, string directory)
        {
            file = Path.GetFullPath(file); directory = Path.GetFullPath(directory);
            byte[] original = File.ReadAllBytes(file);
            using (var hash = SHA256.Create())
                Check(BitConverter.ToString(hash.ComputeHash(original)).Replace("-", "") == expectedHash, "Genuine input SHA mismatch");
            Check(!Directory.Exists(directory) && !File.Exists(directory), "Output must be a new artifact directory");
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Check(source.importError == null && source.parts.Count == 1 && source.parts[0].prefabName == "wood_floor" &&
                source.sourceDocuments.Single().format == UnboundBlueprint.Format, "Expected pinned single-floor capture");
            CheckFloor(source);
            string library = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(library, backgroundImports: true);
            // Unit prefab bases only; no native capability/profile is inferred from the captured packet.
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            Ready(store);
            var review = store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash),
                ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Check(!Directory.Exists(store.ImportDirectory), "Review wrote an import archive");
            Check(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error); Ready(store);
            Check(store.ImportError == null, "Genuine import failed: " + store.ImportError);
            var catalog = store.CatalogAll().Single();
            Check(CompositeBlueprintStore.HasForeignData(catalog) && catalog.parts[0].foreignData?.format == UnboundBlueprint.Format &&
                catalog.parts[0].foreignData.zdo == source.parts[0].foreignData.zdo &&
                catalog.parts[0].sourceDocumentId == source.parts[0].sourceDocumentId && catalog.parts[0].sourceLine == 2 &&
                catalog.parts[0].sourceRecord == 0 && catalog.parts[0].sourceFrameApplied &&
                catalog.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),
                "Raw state/source changed or became implicitly supported");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var bytes = new MemoryStream())
            {
                using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(bytes);
                Check(bytes.ToArray().SequenceEqual(original), "Genuine import archive changed original bytes");
            }
            Check(store.TryGetOrCreatePlacementCopy(catalog, out var saved, out error), error);
            var reopened = new CompositeBlueprintStore(library).All().Single();
            Check(BlueprintExchangeFormat.Encode(saved).SequenceEqual(BlueprintExchangeFormat.Encode(reopened)), "Native reopen changed fields");
            UnboundSourceChecks.Editor(reopened);
            CorpusRoundTripChecks.RunSaved(library, saved.id);
            File.WriteAllBytes(Path.Combine(directory, "portable.buildworks.json"), BlueprintExchangeFormat.Encode(saved));
            var capturedHeader = source.sourceDocuments.Single().ToGeometry().Unbound.Pieces.Single().State;
            Check(capturedHeader.Persistent == true && capturedHeader.Distant == false && capturedHeader.NativeObjectType == 2, "Captured native header changed");
            var context = new UnboundImportContext(new BlueprintPrefabMetadata(1, "wood_floor", "wood_floor", "Piece",
                "substituted-cpu-host", new Point3(1, 1, 1)), false, true, false, false, null,
                (ForeignZdoChecks.Hash("wood_floor"), true, false, (byte)0));
            var projected = ExternalBlueprintLibrary.PrepareForHost(reopened, _ => null, ForeignZdoChecks.Hash,
                unboundContext: _ => context);
            Check(projected.importError == null && !CompositeBlueprintStore.HasForeignData(projected) &&
                projected.parts.Single().baseScale.HasValue, "Substituted profile could not project captured fields");
            CheckFloor(projected);
            var export = UnboundExport.Review(projected, _ => context);
            Check(export.HeaderChanges == 1, "Genuine type2 to current prefab type0 conversion was not disclosed");
            Check(export.Blockers.Count == 0, "Genuine paired export review blocked: " + string.Join(",", export.Blockers) +
                "; captured header " + capturedHeader.Persistent + "/" + capturedHeader.Distant + "/" + capturedHeader.NativeObjectType);
            Check(export.TryWrite(projected, _ => context, directory, "pair", out string pair, out error), "Genuine paired export failed: " + error);
            var companion = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(pair, "pair.buildworks.json")));
            Check(companion.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original) &&
                companion.sourceDocuments.Single().ToGeometry().Unbound.Pieces.Single().State.NativeObjectType == 2,
                "Companion lost original capture or instance header");
            var foreign = ExternalBlueprintLibrary.LoadFile(Path.Combine(pair, "pair.vtbp")).Single();
            Check(foreign.sourceDocuments.Single().ToGeometry().Unbound.Pieces.Single().State.NativeObjectType == 0,
                "Foreign export replayed the historical instance type instead of the trusted prefab type");
            var again = ExternalBlueprintLibrary.PrepareForHost(foreign, _ => null, ForeignZdoChecks.Hash, unboundContext: _ => context);
            Check(again.importError == null && !CompositeBlueprintStore.HasForeignData(again) && again.parts.Single().baseScale.HasValue &&
                again.parts[0].baseScale.Value.x == 1 && again.parts[0].baseScale.Value.y == 1 && again.parts[0].baseScale.Value.z == 1,
                "Paired foreign file changed working state/scale");
            CheckFloor(again);
            Check(source.sourceDocuments.Single().ToGeometry().Unbound.Pieces.Single().State.HasHash(ForeignZdoChecks.Hash("creator")) &&
                !foreign.sourceDocuments.Single().ToGeometry().Unbound.Pieces.Single().State.HasHash(ForeignZdoChecks.Hash("creator")),
                "Creator authority was replayed or the genuine source control is missing");
            var changed = new UnboundImportContext(new BlueprintPrefabMetadata(1, "wood_floor", "wood_floor", "Piece",
                "changed-cpu-host", new Point3(1, 1, 1)), false, true, false, false, null, context.NativeHeader);
            Check(!export.TryWrite(projected, _ => changed, directory, "stale", out _, out error) &&
                error == "$buildworks_exchange.plan_changed" && !Directory.Exists(Path.Combine(directory, "stale")),
                "Changed profile wrote a paired result");
            Check(File.ReadAllBytes(file).SequenceEqual(original), "Genuine producer file changed");
            Console.WriteLine("PASS pinned genuine one-floor Unbound file: import/archive/catalog/native36/editor model/portable24/full-field reimport; raw stays blocked without context. Paired export/reimport, independent pose, creator sanitization/full companion and stale profile refusal passed with substituted CPU facts only. No foreign writer, live UI, placement or game reload.");
        }

        private static void CheckFloor(CompositeBlueprintStore.Blueprint blueprint)
        {
            Check(blueprint.parts.Count == 1, "One captured floor required");
            var part = blueprint.parts[0];
            Check(part.prefabName == "wood_floor" && part.position.x == 0 && part.position.y == 0 && part.position.z == 0 &&
                part.rotation.x == 0 && part.rotation.z == 0 && Math.Abs(part.rotation.y - .9999905f) < 1e-7f &&
                Math.Abs(part.rotation.w - .00436336035f) < 1e-7f && part.signText == null && !part.materialSeed.HasValue,
                "Captured local pose or working fields differ from the native observation");
        }

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
