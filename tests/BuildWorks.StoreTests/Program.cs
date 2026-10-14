using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--benchmark") return BenchmarkExchange(int.Parse(args[1]));
            string directory = Path.Combine(
                Path.GetTempPath(), "BuildWorks.StoreTests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try
            {
                if (args.Length == 2 && args[0] == "--roundtrip-corpus")
                { CorpusRoundTripChecks.Run(args[1], directory); return 0; }
                if ((args.Length == 2 || args.Length == 3) && args[0] == "--import-timings")
                    return ImportTimingChecks.Run(args[1], args.Length == 3 ? args[2] : null, directory);
                if (args.Length == 2 && args[0] == "--catalog-timings")
                    return ImportTimingChecks.RunCatalog(args[1], directory);
                if (args.Length == 2 && args[0] == "--catalog-phases")
                    return ImportTimingChecks.RunCatalogPhases(args[1], directory);
                if (args.Length == 2 && args[0] == "--planbuild-export")
                { PlanBuildExportChecks.Run(args[1]); return 0; }
                if (args.Length == 1 && args[0] == "--batch-exchange")
                { BatchExchangeChecks.Run(Path.Combine(directory, "batch-exchange")); return 0; }
                if (args.Length == 1 && args[0] == "--planbuild-snap-tails")
                { PlanBuildSnapTailChecks.Run(Path.Combine(directory, "snap-tails")); return 0; }
                PlanBuildSnapTailChecks.Run(Path.Combine(directory, "snap-tails"));
                SourceEvidenceCacheChecks.Run();
                PlacementContentsFixtureChecks.Run();
                PlanBuildExportChecks.Run(Path.Combine(directory, "planbuild-export"));
                ValidationReadChecks.Run(Path.Combine(directory, "validation-reads"));
                ImportErrorChecks.Run();
                ImportCatalogRecoveryChecks.Run(Path.Combine(directory, "import-catalogue-recovery"));
                ImportCategoryChecks.Run(Path.Combine(directory, "import-category"));
                ShareFileChecks.Run(Path.Combine(directory, "share-files"));
                BatchExchangeChecks.Run(Path.Combine(directory, "batch-exchange"));
                LargeSnapChecks.Run(Path.Combine(directory, "large-snap-bounds"));
                VbuildDialectChecks.Run(Path.Combine(directory, "legacy-vbuild"));
                MaterialSeedChecks.Run(Path.Combine(directory, "material-seed"));
                RoundTripV9(Path.Combine(directory, "roundtrip.json"));
                PrimaryPartRoundTrip(Path.Combine(directory, "primary.json"));
                PrimaryGroupRoundTrip(Path.Combine(directory, "primary-group.json"));
                MigrateV5Scale(Path.Combine(directory, "migration-v5.json"));
                RejectInvalidScale(Path.Combine(directory, "scale-invalid.json"));
                ScaleUpdateAndRollback(Path.Combine(directory, "scale-update.json"));
                MetadataRoundTrip(Path.Combine(directory, "metadata.json"));
                MigrateV3(Path.Combine(directory, "migration.json"));
                RejectGroupCycle(Path.Combine(directory, "cycle.json"));
                FailedWritePreservesOriginal(Path.Combine(directory, "failure.json"));
                ExternalImports(directory);
                ForeignScaleSemantics(Path.Combine(directory, "foreign-scale.json"));
                PinnedScaleBasis(Path.Combine(directory, "pinned-scale.json"));
                ExchangeFileWorkflow(Path.Combine(directory, "file-flow"));
                CatalogRegressionChecks.Run(Path.Combine(directory, "catalog-regressions"));
                CatalogVisibilityChecks.Run(Path.Combine(directory, "catalog-visibility"));
                ForeignSignText(Path.Combine(directory, "signs"));
                ForeignSourcesRoundTrip(Path.Combine(directory, "sources.json"));
                ForeignObjectRoundTrip(Path.Combine(directory, "foreign-objects.json"));
                ForeignItemStandRecipes(Path.Combine(directory, "foreign-holder.json"));
                ForeignZdoChecks.Run();
                NativeItemStandRoundTrip(Path.Combine(directory, "native-holder.json"));
                NativeContainerRoundTrip(Path.Combine(directory, "native-container.json"));
                LegacyStorageChecks.Run(Path.Combine(directory, "legacy-storage"));
                ContentsPolicyRoundTrip(Path.Combine(directory, "contents-policy"));
                ArmorStatePolicy(Path.Combine(directory, "armor-state-policy"));
                ReviewedImportWorkflow(Path.Combine(directory, "reviewed-import"));
                ExchangeRoundTrip(Path.Combine(directory, "exchange-library.json"));
                SupportRoundTrip(Path.Combine(directory, "supports.json"));
                SinglePartChecks.Run(Path.Combine(directory, "single-part"));
                MissingPartChecks.Run(Path.Combine(directory, "missing-parts"));
                MetadataChecks.Run(Path.Combine(directory, "metadata"));
                SourcePlacementHeaderChecks.Run(Path.Combine(directory, "placement-headers"));
                if (args.Length == 2 && args[0] == "--large-snap-corpus") LargeSnapChecks.RunCorpus(args[1], Path.Combine(directory, "large-snap-corpus"));
                else if (args.Length == 2 && args[0] == "--player-fixtures") PlayerFixtures(args[1]);
                else if (args.Length > 0) InspectActualLibrary(args[0]);
                Console.WriteLine("All BuildWorks store tests passed.");
                return 0;
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception);
                return 1;
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        private static void PlayerFixtures(string directory)
        {
            var fixtures = ExternalBlueprintLibrary.Load(directory);
            Require(fixtures.Count == 6 && fixtures.All(item => item.importError == null && item.parts.Count == 2), "Player fixture parse failed");
            foreach (var source in fixtures)
            {
                var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => UnitScale);
                var copy = ExternalBlueprintLibrary.DetachedCopy(prepared);
                var decoded = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(copy)));
                Require(decoded.parts.Count == 2 && decoded.sourceDocuments[0].text == source.sourceDocuments[0].text, "Player fixture conversion lost source");
            }
            Require(fixtures.Single(item => item.name == "TEST 02 Sign").parts[1].signText == "BuildWorks: Привет, Ostrix!", "Player sign fixture changed");
            Require(fixtures.Single(item => item.name == "TEST 03 Opaque").sourceDocuments[0].ToGeometry().HasOpaqueSections, "Opaque fixture not detected");
            Require(fixtures.Single(item => item.name == "TEST 04 Snap").customSnapPoints.Count == 1, "Player snap fixture lost");
            var missing = fixtures.Single(item => item.name == "TEST 05 Missing");
            Require(ExternalBlueprintLibrary.PrepareForHost(missing, name => name == "woodwall" ? UnitScale : (CompositeBlueprintStore.VectorData?)null).parts.Count == 2,
                "Missing-prefab fixture discarded source records");
            Console.WriteLine("PASS six named player fixtures: conversion/source/sign/snap/opaque/missing record retention; no game proof");
        }

        private static void ForeignSourcesRoundTrip(string path)
        {
            string text = "\uFEFF#Name:Sources\r\n#Creator:Автор\r\n#Description:" + new string('x', 40000) +
                "\r\n#Terrain\r\nsquare;ignored\r\n#Pieces\r\nwall;;0;0;0;0;0;0;1;\"\"\r\nbeam;;1;0;0;0;0;0;1;\"\"\r\n";
            var external = ExternalBlueprintLibrary.Parse(text, "archive/Original.blueprint");
            Require(external.importError == null, external.importError);
            var converted = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(external, _ => UnitScale));
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySaveDocument(converted.name, converted.category, converted.parts, converted.groups, converted.anchors,
                null, null, converted.supports, null, out var saved, out string error, sourceDocuments: converted.sourceDocuments), error);
            string id = saved.sourceDocuments[0].id;
            var cachedSource = saved.sourceDocuments[0];
            Require(ReferenceEquals(cachedSource.ToGeometry(), cachedSource.ToGeometry()), "Validated source was rehashed unnecessarily");
            cachedSource.text += "changed";
            Require(!CompositeBlueprintStore.ValidSources(saved.parts, saved.sourceDocuments), "Cached source bypassed content identity validation");
            cachedSource.text = text;
            Require(CompositeBlueprintStore.ValidSources(saved.parts, saved.sourceDocuments), "Restored source did not validate");
            converted.sourceDocuments[0].text = "changed caller";
            Require(saved.sourceDocuments[0].text == text, "Save retained caller-owned mutable source object");
            var reopened = new CompositeBlueprintStore(path).All()[0];
            var decoded = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            Require(decoded.sourceDocuments[0].text == text && decoded.sourceDocuments[0].name == "Original.blueprint" &&
                decoded.sourceDocuments[0].id == id && decoded.parts[0].sourceDocumentId == id && decoded.parts[0].sourceLine == 7,
                "Original source, BOM, line endings or row binding was lost");
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                null, null, out error), error);
            Require(new CompositeBlueprintStore(path).All()[0].sourceDocuments[0].text == text, "Legacy update discarded source evidence");
            var reconstructed = CompositeBlueprintStore.CloneParts(saved.parts);
            foreach (var part in reconstructed) { part.sourceDocumentId = null; part.sourceLine = 0; }
            Require(store.TryUpdateParts(saved, reconstructed, saved.anchors, out error), error);
            Require(saved.parts[0].sourceDocumentId == id && saved.parts[0].sourceLine == 7,
                "Legacy parts-only update discarded source row binding");
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            foreach (string invalid in new[] { json.Replace("\"version\":11", "\"version\":2"),
                json.Replace("\"sourceLine\":7", "\"sourceLine\":6"), json.Replace("\"sourceLine\":7", "\"sourceLine\":0"),
                json.Replace("Original.blueprint", "../Original.blueprint"), json.Replace("Автор", "Other author"),
                json.Replace("\"sourceDocuments\":[", "\"sourceDocuments\":[null,"),
                json.Replace("\"sourceLine\":7", "\"sourceLine\":7,\"sourceLine\":7") })
            {
                bool rejected = false;
                try { BlueprintExchangeFormat.Decode(invalid); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Invalid source identity, row, path, duplicate or old schema was accepted");
            }
            var duplicate = CompositeBlueprintStore.CloneSources(saved.sourceDocuments);
            duplicate.Add(duplicate[0]);
            string original = File.ReadAllText(path);
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                null, null, saved.supports, null, out error, sourceDocuments: duplicate) && File.ReadAllText(path) == original,
                "Duplicate source update was not atomic");
            var invalidParts = CompositeBlueprintStore.CloneParts(saved.parts);
            invalidParts[0].sourceDocumentId = "missing";
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, invalidParts, saved.groups, saved.anchors,
                null, null, out error) && saved.parts[0].sourceDocumentId == id && File.ReadAllText(path) == original,
                "Missing row source changed the saved document");
            var updatedSources = CompositeBlueprintStore.CloneSources(saved.sourceDocuments);
            updatedSources.Add(CompositeBlueprintStore.SourceDocument.FromGeometry(new Geometry.BlueprintSourceDocument(
                "vbuild", "Extra.vbuild", "wall 0 0 0 1 0 0 0\r\n")));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(!store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                    null, null, saved.supports, null, out error, sourceDocuments: updatedSources) && saved.sourceDocuments.Count == 1,
                    "Failed write did not restore source documents");
            Require(File.ReadAllText(path) == original, "Failed source update replaced original file");
            string oversizedScalar = json.Replace("\"name\":\"Sources\"", "\"name\":\"" + new string('x', 32769) + "\"");
            bool quotaRejected = false;
            try { BlueprintExchangeFormat.Decode(oversizedScalar); } catch (InvalidDataException) { quotaRejected = true; }
            Require(quotaRejected, "Source text quota also enlarged ordinary scalar fields");
            string legacy = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(new CompositeBlueprintStore.Blueprint {
                id = Guid.NewGuid().ToString("N"), name = "Legacy", category = "OTHER", prefabName = "wall",
                parts = new List<CompositeBlueprintStore.Part> { Part("a", "wall", "A", null, true, false, 0),
                    Part("b", "wall", "B", null, true, false, 1) } })).Replace("\"version\":11", "\"version\":2");
            Require(BlueprintExchangeFormat.Decode(legacy).sourceDocuments == null, "Clean v2 document stopped migrating");
            var sourceOnly = reopened.ImportCopy();
            foreach (var part in sourceOnly.parts)
            {
                part.scale = new CompositeBlueprintStore.VectorData(part.RootScale(UnityEngine.Vector3.one));
                part.baseScale = null; // Construct an actual pre-basis schema fixture.
            }
            string sourceOnlyJson = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(sourceOnly));
            var sourceOnlyV3 = BlueprintExchangeFormat.Decode(sourceOnlyJson.Replace("\"version\":11", "\"version\":3"));
            Require(sourceOnlyV3.sourceDocuments[0].text == text && sourceOnlyV3.parts[0].foreignData == null,
                "Source-only portable v3 stopped migrating");
            File.WriteAllText(path, Json(new CompositeBlueprintStore.LibraryData { version = 12, hiddenSources = null,
                blueprints = new List<CompositeBlueprintStore.Blueprint> { sourceOnly } }));
            var nativeV12 = new CompositeBlueprintStore(path);
            Require(nativeV12.All().Count == 1 && nativeV12.All()[0].sourceDocuments[0].text == text,
                "Source-only native v12 stopped migrating");
            Console.WriteLine("PASS source v18/v8 exact UTF-8/BOM/CRLF/header retention; stable rows; detached/save/update/exchange; strict old-version/hash/reference/duplicate rejection");
        }

        private static void ReviewedImportWorkflow(string directory)
        {
            Directory.CreateDirectory(directory);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 });
            var parts = new[] { Part("chest", "piece_chest", "Chest", null, true, false, 0),
                Part("stand", "itemstand", "Stand", null, true, false, 1) };
            parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
            parts[1].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:0:1:0", zdo = StandPackage() };
            Require(store.TrySave("Contents", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            byte[] bytes = BlueprintExchangeFormat.Encode(saved);
            string source = Path.Combine(directory, "source.zip");
            using (var zip = ZipFile.Open(source, ZipArchiveMode.Create))
            {
                using (var entry = zip.CreateEntry("one.buildworks.json").Open()) entry.Write(bytes, 0, bytes.Length);
                using (var entry = zip.CreateEntry("two.buildworks.json").Open()) entry.Write(bytes, 0, bytes.Length);
            }
            byte[] original = File.ReadAllBytes(source);
            var review = CompositeBlueprintStore.ReviewImportFile(source, _ => 100);
            Require(review.Documents.Count == 2 && review.Contents.Storages == 2 && review.Contents.Mounts == 2 &&
                !Directory.Exists(store.ImportDirectory), "Review wrote to library or did not inspect entire archive");
            foreach (bool storage in new[] { false, true })
            foreach (bool mounts in new[] { false, true })
            {
                Require(store.TryImportReviewed(review, storage, mounts, _ => 100, out error), error); WaitForImport(store);
                Require(store.ImportError == null && File.Exists(store.ImportResultPath), store.ImportError);
                var copies = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath);
                Require(copies.Count == 2 && copies.All(copy => copy.importError == null &&
                    copy.parts[0].foreignData.contentsCleared == storage && copy.parts[1].foreignData.contentsCleared == mounts &&
                    copy.parts[0].scale.x == 1), "Reviewed ZIP changed native scale or coupled choices");
                Require(store.CatalogAll().Where(copy => (copy.importSource ?? "").StartsWith(Path.GetFileName(store.ImportResultPath) + "/", StringComparison.Ordinal))
                    .Select(copy => copy.id).SequenceEqual(copies.Select(copy => copy.id)), "Committed import changed cache identity after reload");
                using (var zip = ZipFile.OpenRead(store.ImportResultPath))
                using (var input = zip.GetEntry("original/source.bin").Open())
                using (var retained = new MemoryStream())
                { input.CopyTo(retained); Require(retained.ToArray().SequenceEqual(original), "Exact original ZIP was lost"); }
                Require(File.ReadAllBytes(source).SequenceEqual(original) && !saved.parts[0].foreignData.contentsCleared &&
                    !saved.parts[1].foreignData.contentsCleared, "Import changed original or current document");
            }
            var damaged = CompositeBlueprintStore.CopyWithContentsPolicy(saved, false, false, null);
            damaged.parts[1].foreignData.zdo = "bQIA";
            string damagedSource = Path.Combine(directory, "damaged-mount.buildworks.json");
            File.WriteAllBytes(damagedSource, BlueprintExchangeFormat.Encode(damaged));
            var damagedReview = CompositeBlueprintStore.ReviewImportFile(damagedSource, _ => 100);
            Require(damagedReview.Contents.Unknown == 1 && damagedReview.Contents.UnreadableMounts == 1 &&
                damagedReview.Contents.CanClearStorage && !damagedReview.Contents.CanClearMounts,
                "Import aggregation lost independent unreadable mount policy");
            Require(store.TryImportReviewed(damagedReview, true, false, _ => 100, out error), error); WaitForImport(store);
            var damagedCopy = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
            Require(store.ImportError == null && damagedCopy.parts[0].foreignData.contentsCleared &&
                damagedCopy.parts[1].foreignData.zdo == "bQIA" && !damagedCopy.parts[1].foreignData.contentsCleared,
                "Import discarded unreadable mount evidence or coupled choices");
            int before = Directory.GetFiles(store.ImportDirectory).Length;
            File.AppendAllText(source, "changed");
            Require(store.TryImportReviewed(review, false, false, null, out error), error); WaitForImport(store);
            Require(store.ImportError.Contains("source_changed") && store.ImportResultPath == null &&
                Directory.GetFiles(store.ImportDirectory).Length == before && Directory.GetDirectories(store.ImportDirectory).Length == 0,
                "Changed source committed or left staging files");
            string bad = Path.Combine(directory, "mixed.zip");
            using (var zip = ZipFile.Open(bad, ZipArchiveMode.Create))
            {
                using (var entry = zip.CreateEntry("good.buildworks.json").Open()) entry.Write(bytes, 0, bytes.Length);
                using (var writer = new StreamWriter(zip.CreateEntry("bad.buildworks.json").Open())) writer.Write("invalid");
            }
            try { CompositeBlueprintStore.ReviewImportFile(bad, _ => 100); throw new Exception("Mixed archive accepted"); }
            catch (InvalidDataException) { }
            Require(!store.TryImportFile(bad, out error) && Directory.GetFiles(store.ImportDirectory).Length == before,
                "Legacy caller bypassed whole-archive rejection");
            saved.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData { format = "vbuild", zdo = "opaque" };
            string unknown = Path.Combine(directory, "unknown.buildworks.json");
            File.WriteAllBytes(unknown, BlueprintExchangeFormat.Encode(saved));
            var unknownReview = CompositeBlueprintStore.ReviewImportFile(unknown, _ => 100);
            Require(!unknownReview.Contents.CanClearStorage && !unknownReview.Contents.CanClearMounts && unknownReview.Contents.Unknown == 1,
                "Unknown import contents were treated as empty");
            Require(store.TryImportReviewed(unknownReview, false, true, _ => 100, out error), error); WaitForImport(store);
            Require(store.ImportError != null && store.ImportResultPath == null && Directory.GetFiles(store.ImportDirectory).Length == before &&
                Directory.GetDirectories(store.ImportDirectory).Length == 0, "Failed contents conversion committed partial data or leaked staging files");
            string foreign = Path.Combine(directory, "scale.blueprint");
            File.WriteAllText(foreign, "#Name:Scale\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\";1;1;1\nwall;;2;0;0;0;0;0;1;\"\";1;1;1");
            Require(store.TryImportFile(foreign, out error), error); WaitForImport(store);
            var normalized = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
            Require(normalized.parts[0].scale.x == 1 && normalized.parts[0].baseScale.Value.x == 1 &&
                !normalized.importAbsoluteScale, "Foreign conversion did not pin its original root scale");
            ReviewedImportLimits(Path.Combine(directory, "limits"), bytes);
            Console.WriteLine("PASS reviewed atomic archive import, four independent choices, exact original bytes, cancel/no writes, stale source and mixed-member rejection, normalized native copy");
        }

        private static void ReviewedImportLimits(string directory, byte[] document)
        {
            Directory.CreateDirectory(directory);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            Directory.CreateDirectory(store.ImportDirectory);
            var large = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(document));
            large.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> {
                CompositeBlueprintStore.SourceDocument.FromGeometry(new Geometry.BlueprintSourceDocument("blueprint", "large.blueprint",
                    "#Description:" + new string('x', 7 * 1024 * 1024) + "\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;1;\"\"")) };
            byte[] largeDocument = BlueprintExchangeFormat.Encode(large);
            void Archive(string path, int count)
            {
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                for (int i = 0; i < count; ++i)
                {
                    string name = (count == 17 && i >= 15 ? "duplicate" : "piece" + i) + ".buildworks.json";
                    using (var entry = zip.CreateEntry(name).Open()) entry.Write(largeDocument, 0, largeDocument.Length);
                }
            }
            string baseline = Path.Combine(store.ImportDirectory, "zzz-existing.zip"); Archive(baseline, 17);
            Require(store.CatalogAll().Count == 17 && store.CatalogAll().All(copy => copy.importError == null), "Budget baseline invalid");
            string source = Path.Combine(directory, "crosses-budget.zip"); Archive(source, 2);
            var review = CompositeBlueprintStore.ReviewImportFile(source, _ => 100);
            Require(review.Documents.Count == 2, "Budget candidate did not load independently");
            Require(store.TryImportReviewed(review, false, false, null, out string error), error);
            WaitForImport(store);
            Require(store.ImportError != null && store.ImportResultPath == null && Directory.GetFiles(store.ImportDirectory).Length == 1 &&
                ExternalBlueprintLibrary.Load(store.ImportDirectory).All(copy => copy.importError == null),
                "Aggregate-budget import broke existing library on reload or left a candidate");
            File.Delete(baseline); // This test owns only this generated file.
            for (int i = 0; i < 1999; ++i) File.WriteAllText(Path.Combine(store.ImportDirectory, i + ".txt"), "ignored");
            string small = Path.Combine(directory, "small.buildworks.json"); File.WriteAllBytes(small, document);
            review = CompositeBlueprintStore.ReviewImportFile(small, _ => 100);
            Require(store.TryImportReviewed(review, false, false, null, out error), error); WaitForImport(store);
            Require(store.ImportError == null && Directory.GetFiles(store.ImportDirectory).Length == 2000,
                "Exactly 2000 library files rejected because staging counted twice: " + store.ImportError);
            Require(store.TryImportReviewed(review, false, false, null, out error), error); WaitForImport(store);
            Require(store.ImportError != null && Directory.GetFiles(store.ImportDirectory).Length == 2000 &&
                Directory.GetDirectories(store.ImportDirectory).Length == 0, "2001st file committed or failed import leaked staging");
            string crowded = Path.Combine(directory, "crowded.zip");
            using (var zip = ZipFile.Open(crowded, ZipArchiveMode.Create))
                for (int i = 0; i < 1999; ++i)
                    using (var entry = zip.CreateEntry("piece" + i + ".buildworks.json").Open()) entry.Write(document, 0, document.Length);
            try { CompositeBlueprintStore.ReviewImportFile(crowded, _ => 100); throw new Exception("Projected ZIP overflow passed preflight"); }
            catch (InvalidDataException) { }
            Console.WriteLine("PASS atomic import shared 128MiB library budget, exact future ordering and 2000/2001 file boundary");
        }

        private static string StandPackage(int variant = 0, ushort quality = 1)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)109); writer.Write(12345);
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((byte)(64 | (quality != 1 ? 4 : 0) | (variant != 0 ? 16 : 0)));
                if (quality != 1) writer.Write(quality);
                if (variant != 0) writer.Write(variant);
                writer.Write(100); writer.Write((byte)0);
                writer.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void ArmorStatePolicy(string directory)
        {
            Directory.CreateDirectory(directory);
            const string row = "ArmorStand;;2;3;4;0;0;0;1;\"2:1:HelmetIron:3:\";1;1;1";
            const string text = "#Name:Armor policy\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n" + row;
            var parsed = ExternalBlueprintLibrary.Parse(text, "Armor.blueprint");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale);
            var summary = CompositeBlueprintStore.InspectContents(prepared, _ => 100);
            Require(summary.Unknown == 1 && summary.ArmorStates == 1 && summary.RemainingUnknown(true) == 0 &&
                summary.ArmorStateSources["Armor.blueprint · ArmorStand"] == 1, "Armor loss review lost source/count");
            var retained = CompositeBlueprintStore.CopyWithContentsPolicy(prepared, false, false, null);
            var omitted = CompositeBlueprintStore.CopyWithContentsPolicy(prepared, false, false, null, true);
            Require(CompositeBlueprintStore.HasForeignData(retained) && !CompositeBlueprintStore.HasForeignData(omitted) &&
                retained.parts[1].foreignData.info == "\"2:1:HelmetIron:3:\"" && prepared.parts[1].foreignData != null &&
                parsed.sourceDocuments[0].text == text && omitted.sourceDocuments[0].text == text &&
                omitted.parts[1].sourceLine == 4 && omitted.parts[1].position.x == 2 && omitted.parts[1].position.y == 3 &&
                omitted.parts[1].position.z == 4 && omitted.parts[1].rotation.w == 1 && omitted.parts[1].baseScale.Value.x == 1 &&
                omitted.parts[1].scale.x == 1, "Armor omission changed source, geometry or default policy");
            var reopened = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(omitted)));
            Require(ExternalBlueprintLibrary.PrepareForHost(reopened, _ => UnitScale, ForeignZdoChecks.Hash, 0).parts[1].foreignData == null &&
                reopened.sourceDocuments[0].text == text, "Armor state resurrected from inert source");
            foreach (string badRow in new[] { row.Replace("ArmorStand", "mod_ArmorStand"),
                row.Replace("2:1:HelmetIron:3:", "2:2:HelmetIron:3:"), row.Replace("2:1:HelmetIron:3:", "-1:1:HelmetIron:3:"),
                row.Replace("2:1:HelmetIron:3:", "2:1:HelmetIron:-3:"), row.Replace("2:1:HelmetIron:3:", "2:2147483647:HelmetIron:3:"),
                row + ";opaque", row + ";;0.5" })
            {
                var blocked = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(text.Replace(row, badRow), "Armor.blueprint"), _ => UnitScale);
                Require(blocked.importError == null && CompositeBlueprintStore.InspectContents(blocked, _ => 100).ArmorStates == 0 &&
                    CompositeBlueprintStore.CopyWithContentsPolicy(blocked, false, false, null, true).parts[1].foreignData != null,
                    "Armor policy omitted unknown prefab/grammar/ZDO/chance");
            }
            var opaque = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(text + "\n#Rotation\n0;45;0", "Armor.blueprint"), _ => UnitScale);
            Require(CompositeBlueprintStore.InspectContents(opaque, _ => 100).ArmorStates == 0 &&
                CompositeBlueprintStore.HasForeignData(CompositeBlueprintStore.CopyWithContentsPolicy(opaque, false, false, null, true)),
                "Unknown geometry header was silently omitted");
            var mismatch = prepared.ImportCopy(); mismatch.parts[1].foreignData.info = "\"0:1:HelmetIron:3:\"";
            Require(CompositeBlueprintStore.InspectContents(mismatch, _ => 100).ArmorStates == 0, "Unbound armor state accepted");
            mismatch = prepared.ImportCopy(); mismatch.parts[1].foreignData.format = "vbuild";
            Require(CompositeBlueprintStore.InspectContents(mismatch, _ => 100).ArmorStates == 0, "Other dialect accepted");
            var duplicate = ExternalBlueprintLibrary.Parse(text + "\n" + row, "Armor.blueprint");
            var duplicateSummary = CompositeBlueprintStore.InspectContents(duplicate, _ => 100);
            Require(duplicateSummary.ArmorStates == 2 && duplicateSummary.ArmorStateSources.Count == 1 &&
                duplicateSummary.ArmorStateSources.Values.Single() == 2, "Armor losses were not grouped with exact count");

            var mixed = prepared.ImportCopy();
            var chest = Part("chest", "piece_chest", "Chest", null, true, false, 0);
            chest.foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
            var mount = Part("mount", "itemstand", "Mount", null, true, false, 0);
            mount.foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:0:1:0", zdo = StandPackage() };
            mixed.parts.Add(chest); mixed.parts.Add(mount);
            foreach (bool storage in new[] { false, true })
                foreach (bool mounts in new[] { false, true })
                {
                    var copy = CompositeBlueprintStore.CopyWithContentsPolicy(mixed, storage, mounts, _ => 100, true);
                    Require(copy.parts[1].foreignData == null && copy.parts[2].foreignData.contentsCleared == storage &&
                        copy.parts[3].foreignData.contentsCleared == mounts && mixed.parts[1].foreignData != null &&
                        !mixed.parts[2].foreignData.contentsCleared && !mixed.parts[3].foreignData.contentsCleared,
                        "Armor choice changed independent readable contents or source");
                }
            string source = Path.Combine(directory, "Armor.blueprint"); File.WriteAllText(source, text);
            byte[] original = File.ReadAllBytes(source);
            var review = CompositeBlueprintStore.ReviewImportFile(source, ForeignZdoChecks.Hash, 0);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            store.ConfigureImportScale(_ => UnitScale);
            Require(review.Contents.ArmorStates == 1 && !Directory.Exists(store.ImportDirectory), "Armor review wrote a copy");
            Require(store.TryImportReviewed(review, false, false, null, out string error, true), error); WaitForImport(store);
            Require(store.ImportError == null && File.ReadAllBytes(source).SequenceEqual(original), "Reviewed armor import changed source");
            var imported = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
            Require(imported.parts[1].foreignData == null && imported.sourceDocuments[0].text == text, "Reviewed armor policy lost evidence");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var entry = zip.GetEntry("original/source.bin").Open())
            using (var bytes = new MemoryStream())
            {
                entry.CopyTo(bytes);
                Require(bytes.ToArray().SequenceEqual(original), "Armor original archive changed bytes");
            }
            Require(store.TryExportFile(prepared, directory, "empty-armor", false, out string exported, out error,
                omitArmorState: true), error);
            Require(ExternalBlueprintLibrary.LoadFile(exported).Single().parts[1].foreignData == null && prepared.parts[1].foreignData != null,
                "Armor export changed source or ignored explicit choice");
            string imageSource = Path.Combine(directory, "armor-image.zip");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
            using (var zip = ZipFile.Open(imageSource, ZipArchiveMode.Create))
            {
                using (var entry = zip.CreateEntry("armor.blueprint").Open()) entry.Write(original, 0, original.Length);
                using (var entry = zip.CreateEntry("armor.png").Open()) entry.Write(png, 0, png.Length);
            }
            review = CompositeBlueprintStore.ReviewImportFile(imageSource, ForeignZdoChecks.Hash, 0);
            foreach (bool omit in new[] { false, true })
            {
                Require(store.TryImportReviewed(review, false, false, null, out error, omit), error); WaitForImport(store);
                Require(store.ImportError == null, store.ImportError);
                using (var zip = ZipFile.OpenRead(store.ImportResultPath))
                    Require(zip.Entries.Count(entry => entry.FullName.EndsWith(".png", StringComparison.Ordinal)) == (omit ? 0 : 1),
                        "Changed armor copy retained misleading source preview or unchanged copy lost it");
            }
            var unresolved = ExternalBlueprintLibrary.PrepareForHost(parsed, _ => null);
            var unresolvedCopy = CompositeBlueprintStore.CopyWithContentsPolicy(unresolved, false, false, null, true);
            Require(unresolvedCopy.parts[1].scaleIsAbsolute && !unresolvedCopy.parts[1].baseScale.HasValue &&
                unresolved.parts[1].scaleIsAbsolute && unresolved.parts[1].foreignData != null, "Armor omission fabricated a missing prefab scale");
            Console.WriteLine("PASS explicit ArmorStand pose/equipment omission: bound grammar-only Info, default keep, exact source, import/export/reopen, independent contents, unknown/ZDO/chance/header rejection, missing prefab remains pending");
        }

        private static void ContentsPolicyRoundTrip(string directory)
        {
            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, "library.json");
            string package;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(109); writer.Write((ushort)1);
                writer.Write(0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((byte)64); writer.Write(100); writer.Write((byte)0);
                writer.Flush(); package = Convert.ToBase64String(stream.ToArray());
            }
            var parts = new[] { Part("chest", "piece_chest", "Chest", null, true, false, 0),
                Part("stand", "itemstand", "Stand", null, true, false, 1) };
            parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:1:1|Wood", zdo = package };
            parts[1].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:2:3:0", zdo = StandPackage(2, 3) };
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySave("Contents", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            string original = File.ReadAllText(path);
            var inspected = CompositeBlueprintStore.InspectContents(saved, _ => 100);
            Require(inspected.CanClearStorage && inspected.CanClearMounts && inspected.Storages == 1 && inspected.StoredItems == 1 &&
                inspected.Mounts == 1 && inspected.Unknown == 0 && inspected.AlreadyCleared == 0,
                "Contents preflight did not report independent effective counts");
            var unavailable = CompositeBlueprintStore.InspectContents(saved, name => name == "Wood" ? 0 : 100);
            Require(!unavailable.CanClearStorage && unavailable.CanClearMounts && unavailable.Storages == 0 && unavailable.Unknown == 1 && unavailable.Mounts == 1,
                "Unreadable contents were reported as an empty known storage");
            var clearedUnreadable = CompositeBlueprintStore.CopyWithContentsPolicy(saved, true, false, _ => 100);
            var invalidPacket = Convert.FromBase64String(package); invalidPacket[14] = 0;
            clearedUnreadable.parts[0].foreignData.zdo = Convert.ToBase64String(invalidPacket);
            var archived = CompositeBlueprintStore.InspectContents(clearedUnreadable, _ => 100);
            Require(!archived.CanClearStorage && archived.CanClearMounts && archived.AlreadyCleared == 1 && archived.Unknown == 1,
                "Previously cleared unreadable evidence bypassed preflight");
            var unreadableMount = CompositeBlueprintStore.CopyWithContentsPolicy(saved, false, false, null);
            unreadableMount.parts[1].foreignData.zdo = "bQIA";
            var mountSummary = CompositeBlueprintStore.InspectContents(unreadableMount, _ => 100);
            Require(mountSummary.CanClearStorage && !mountSummary.CanClearMounts && mountSummary.Unknown == 1 &&
                mountSummary.UnreadableMounts == 1 && mountSummary.Mounts == 0 && mountSummary.Storages == 1,
                "Unreadable mount was counted as known or blocked independent storage clearing");
            var storageOnly = CompositeBlueprintStore.CopyWithContentsPolicy(unreadableMount, true, false, _ => 100);
            Require(storageOnly.parts[0].foreignData.contentsCleared && storageOnly.parts[1].foreignData.zdo == "bQIA",
                "Independent storage clear discarded unreadable mounted evidence");
            unreadableMount.parts[1].foreignData.contentsCleared = true;
            Require(!CompositeBlueprintStore.InspectContents(unreadableMount, _ => 100).CanClearMounts,
                "Already-cleared mounted evidence bypassed validation");
            foreach (bool wasCleared in new[] { false, true })
            {
                unreadableMount.parts[1].foreignData.contentsCleared = wasCleared;
                Require(!store.TryExportFile(unreadableMount, directory, "bad-mount", false, out _, out error,
                    clearMounts: true, prefabHash: _ => 100) && !File.Exists(Path.Combine(directory, "bad-mount.buildworks.json")),
                    "Unreadable mount clear produced partial output");
            }
            foreach (bool storage in new[] { false, true })
            foreach (bool mounts in new[] { false, true })
            {
                string name = "contents-" + storage + "-" + mounts;
                Require(store.TryExportFile(saved, directory, name, mounts, out string exported, out error,
                    clearStorage: storage, clearMounts: mounts, prefabHash: _ => 100), error);
                var output = ExternalBlueprintLibrary.LoadFile(exported).Single();
                Require(output.importError == null && output.parts[0].foreignData.contentsCleared == storage &&
                    output.parts[1].foreignData.contentsCleared == mounts, "Storage/mount choices were coupled or lost");
                var retained = CompositeBlueprintStore.CopyWithContentsPolicy(output, false, false, null);
                var summary = CompositeBlueprintStore.InspectContents(retained, _ => 100);
                Require(summary.CanClearStorage && summary.CanClearMounts && summary.Storages == (storage ? 0 : 1) && summary.StoredItems == (storage ? 0 : 1) &&
                    summary.Mounts == (mounts ? 0 : 1) && summary.AlreadyCleared == (storage ? 1 : 0) + (mounts ? 1 : 0),
                    "Contents review counted archival items as effective contents");
                string portableOutput = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(retained));
                var dependencies = new List<string> { "itemstand", "piece_chest" };
                if (!storage) dependencies.Add("Wood");
                if (!mounts) dependencies.Add("SwordIron");
                string manifest = "\"requiredPrefabIds\":[" + string.Join(",", dependencies.OrderBy(value => value, StringComparer.Ordinal)
                    .Select(value => "\"" + value + "\"")) + "]";
                Require(portableOutput.Contains(manifest), "Cleared archival contents remained a required prefab dependency");
                Require(output.parts[0].foreignData.zdo == package && output.parts[0].foreignData.info == "5:2:1:1|Wood" &&
                    output.parts[1].foreignData.zdo == StandPackage(2, 3) && output.parts[1].foreignData.info == "SwordIron:2:3:0",
                    "Intentional clear destroyed original contents evidence");
                Require(retained.parts[0].foreignData.contentsCleared == storage && retained.parts[1].foreignData.contentsCleared == mounts &&
                    !saved.parts[0].foreignData.contentsCleared && !saved.parts[1].foreignData.contentsCleared &&
                    File.ReadAllText(path) == original, "Retain resurrected contents or export changed source");
                if (storage && mounts)
                {
                    Require(store.TryImportFile(exported, out error), error); WaitForImport(store);
                    Require(store.ImportError == null && store.CatalogAll().Any(item => item.IsExternal &&
                        item.parts.Count == 2 && item.parts.All(part => part.foreignData.contentsCleared)), "File import lost empty intent");
                    Require(store.TryUpdateDocument(saved, retained.parts, retained.groups, retained.anchors, out error), error);
                    var reopened = new CompositeBlueprintStore(path).All().Single();
                    Require(reopened.parts.All(part => part.foreignData.contentsCleared) && reopened.parts[0].foreignData.zdo == package &&
                        CompositeBlueprintStore.HasForeignData(reopened), "Save/reopen lost clear intent or bypassed world-state guard");
                    string portable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
                    foreach (string invalid in new[] { portable.Replace("\"version\":11", "\"version\":4"),
                        portable.Replace("\"contentsCleared\":true", "\"contentsCleared\":\"true\""),
                        portable.Replace("\"contentsCleared\":true", "\"contentsCleared\":true,\"contentsCleared\":true"),
                        portable.Replace("valheim-itemstand-v1", "blueprint") })
                    {
                        Require(invalid != portable, "Clear-state test did not mutate input");
                        bool rejected = false;
                        try { BlueprintExchangeFormat.Decode(invalid); } catch (InvalidDataException) { rejected = true; }
                        Require(rejected, "Old schema, mistyped/duplicate or unknown clear state accepted");
                    }
                    string currentNative = File.ReadAllText(path);
                    File.WriteAllText(path, currentNative.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":13"));
                    var blocked = new CompositeBlueprintStore(path);
                    Require(blocked.All().Count == 0 && !blocked.TrySave(parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _),
                        "Old native schema silently accepted new clear semantics");
                    File.WriteAllText(path, currentNative);
                    using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                        Require(!store.TryUpdateDocument(saved, parts, saved.groups, saved.anchors, out error), "Locked clear update succeeded");
                    Require(File.ReadAllText(path) == currentNative && saved.parts.All(part => part.foreignData.contentsCleared),
                        "Failed update discarded prior clear state");
                }
            }
            // Migration keeps pre-policy contents populated and does not rewrite the source on read.
            foreach (int version in new[] { 13, 14 })
            {
                string old = original.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":" + version);
                File.WriteAllText(path, old);
                var previous = new CompositeBlueprintStore(path).All().Single();
                Require(previous.parts.All(part => !part.foreignData.contentsCleared) && File.ReadAllText(path) == old, "Native migration cleared old contents");
                string portable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(previous)).Replace("\"version\":11", "\"version\":4");
                Require(BlueprintExchangeFormat.Decode(portable).parts.All(part => !part.foreignData.contentsCleared), "Portable v4 migration changed contents");
            }
            var unreadable = saved.ImportCopy();
            unreadable.parts[0].foreignData.zdo = Convert.ToBase64String(new byte[19] { 109, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            unreadable.parts[0].foreignData.contentsCleared = false;
            Require(!store.TryExportFile(unreadable, directory, "unreadable", false, out _, out error,
                clearStorage: true, prefabHash: _ => 100) && !File.Exists(Path.Combine(directory, "unreadable.buildworks.json")),
                "Unreadable container treated as empty or created a partial output");
            foreach (Func<string, int> hash in new Func<string, int>[] { null, _ => 0, _ => -101 })
                Require(!store.TryExportFile(saved, directory, "bad-hash", false, out _, out error, clearStorage: true, prefabHash: hash) &&
                    !File.Exists(Path.Combine(directory, "bad-hash.buildworks.json")), "Missing/incorrect hash resolver cleared inventory");
            var opaque = saved.ImportCopy();
            opaque.parts[1].foreignData = new CompositeBlueprintStore.ForeignPartData { format = "blueprint", info = "opaque" };
            Require(!store.TryExportFile(opaque, directory, "unknown", false, out _, out error, clearStorage: true, prefabHash: _ => 100) &&
                !File.Exists(Path.Combine(directory, "unknown.buildworks.json")), "Unknown object silently emptied");
            var sections = saved.ImportCopy();
            sections.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> {
                CompositeBlueprintStore.SourceDocument.FromGeometry(new Geometry.BlueprintSourceDocument("blueprint", "Original.blueprint",
                    "#Pieces\n#FutureStorage\nopaque\n")) };
            Require(!store.TryExportFile(sections, directory, "unknown-sections", false, out _, out error,
                clearMounts: true) && !File.Exists(Path.Combine(directory, "unknown-sections.buildworks.json")),
                "Unknown source section silently excluded from clear policy");
            Require(!Directory.EnumerateFiles(directory).Any(file => file.EndsWith(".tmp")), "Failed policy export left temporary data");
            Console.WriteLine("PASS independent contents choices, original evidence, native15/portable6/ZIP/import/reopen, v13/v4 migration, no resurrection, unknown rejection and atomic failure");
        }

        private static void NativeContainerRoundTrip(string path)
        {
            // Inert structural fixture, not a claim that synthetic item records are native-loadable.
            var bytes = new byte[40000]; bytes[0] = 109; bytes[4] = 2;
            string package = Convert.ToBase64String(bytes);
            var parts = new List<CompositeBlueprintStore.Part> {
                Part("a", "piece_chest", "Chest", null, true, false, 0), Part("b", "woodwall", "Wall", null, true, false, 2) };
            parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:2:12|AxeIron|Wood", zdo = package };
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySave("Native storage", parts, new List<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            parts[0].foreignData.zdo = "changed caller";
            var reopened = new CompositeBlueprintStore(path).All().Single();
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            const string dependencies = "\"requiredPrefabIds\":[\"AxeIron\",\"Wood\",\"piece_chest\",\"woodwall\"]";
            Require(json.Contains(dependencies), "Container item dependencies absent");
            var decoded = BlueprintExchangeFormat.Decode(json);
            Require(saved.parts[0].foreignData.zdo == package && decoded.parts[0].foreignData.zdo == package &&
                decoded.parts[0].foreignData.info == "5:2:2:12|AxeIron|Wood" && CompositeBlueprintStore.HasForeignData(decoded),
                "Container package lost/aliased or allowed into native world placement");
            var changed = CompositeBlueprintStore.CloneParts(saved.parts); changed[0].foreignData.info = "5:2:2:13|AxeIron|Wood";
            string original = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(!store.TryUpdateDocument(saved, changed, saved.groups, saved.anchors, out error), "Locked container update succeeded");
            Require(File.ReadAllText(path) == original && saved.parts[0].foreignData.info == "5:2:2:12|AxeIron|Wood", "Failed update changed inventory state");
            foreach (string malformed in new[] { json.Replace(dependencies, "\"requiredPrefabIds\":[\"piece_chest\",\"woodwall\"]"),
                json.Replace("valheim-container-v1", "blueprint"), json.Replace("5:2:2:12|AxeIron|Wood", "5:2:1:12|Wood"),
                json.Replace("\"version\":11", "\"version\":3") })
            {
                bool rejected = false;
                try { BlueprintExchangeFormat.Decode(malformed); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Mismatched container manifest/dependencies or old version accepted");
            }
            var empty = new byte[] { 109, 0, 0, 0, 0, 0 };
            using (var nativeStream = new MemoryStream())
            using (var writer = new BinaryWriter(nativeStream))
            {
                writer.Write(109); writer.Write((ushort)2);
                writer.Write(12505); writer.Write((byte)4); writer.Write((byte)1); writer.Write((byte)3); writer.Write((byte)204);
                writer.Write((ushort)3); writer.Write((ushort)12); writer.Write(-101);
                writer.Write((byte)1); writer.Write("mod:opaque"); writer.Write(new string('x', 40000) + "\0🪵"); writer.Write((byte)0);
                writer.Write(0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)64);
                writer.Write(100); writer.Write((byte)0); writer.Flush();
                string nativePackage = Convert.ToBase64String(nativeStream.ToArray());
                changed[0].foreignData.info = "5:2:2:13|AxeIron|Wood"; changed[0].foreignData.zdo = nativePackage;
                Require(store.TryUpdateDocument(saved, changed, saved.groups, saved.anchors, out error), error);
                var nativeReload = new CompositeBlueprintStore(path).All().Single();
                var portable = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(nativeReload)));
                Require(portable.parts[0].foreignData.zdo == nativePackage &&
                    Geometry.BlueprintContainerInventory.TryRead(portable.parts[0].foreignData.ToGeometry(),
                        name => name == "AxeIron" ? -101 : 100, out var inventory) && inventory.Count == 2 &&
                    inventory[0].Stack == 12 && inventory[0].Quality == 3 && inventory[0].WorldLevel == 3 &&
                    inventory[0].CustomData["mod:opaque"] == new string('x', 40000) + "\0🪵",
                    "Verified native records lost during save/reopen/portable inspection");
                Require(CompositeBlueprintStore.HasForeignData(portable), "Read-only inspection accidentally enabled restoration");
            }
            changed[0].foreignData.info = "5:2:0:0"; changed[0].foreignData.zdo = Convert.ToBase64String(empty);
            Require(store.TryUpdateDocument(saved, changed, saved.groups, saved.anchors, out error), error);
            Require(BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)))
                .parts[0].foreignData.zdo == Convert.ToBase64String(empty), "Verified empty package lost");
            Console.WriteLine("PASS native Container exact >32K package store/portable roundtrip, full record inspection, dependencies, immutable copy, atomic failure and known empty state; no native restore");
        }

        private static void NativeItemStandRoundTrip(string path)
        {
            const string nativeFormat = OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeItemStandFormat;
            string package = Convert.ToBase64String(new byte[] { 109, 2, 0, 255, 67 });
            var parts = new List<CompositeBlueprintStore.Part> {
                Part("a", "itemstand", "Stand", null, true, false, 0),
                Part("b", "woodwall", "Wall", null, true, false, 2) };
            parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = nativeFormat, info = "SwordIron:2:3:0", zdo = package };
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySave("Native holder", parts, new List<CompositeBlueprintStore.VectorData>(),
                out var saved, out string error), error);
            parts[0].foreignData.zdo = "changed";
            var reopened = new CompositeBlueprintStore(path).All()[0];
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            string serializedPackage = package.Replace("/", "\\/");
            Require(json.Contains(serializedPackage), "Native package fixture not found in actual serializer output");
            const string dependencies = "\"requiredPrefabIds\":[\"SwordIron\",\"itemstand\",\"woodwall\"]";
            Require(json.Contains(dependencies), "Native attachment prefab omitted from sorted exact dependencies");
            var portable = BlueprintExchangeFormat.Decode(json);
            Require(saved.parts[0].foreignData.zdo == package && portable.parts[0].foreignData.zdo == package &&
                portable.parts[0].foreignData.format == nativeFormat && portable.parts[0].foreignData.info == "SwordIron:2:3:0" &&
                CompositeBlueprintStore.HasForeignData(portable), "Native item state lost, aliased or allowed into world placement");
            var changed = CompositeBlueprintStore.CloneParts(saved.parts);
            changed[0].foreignData.info = "SwordIron:1:2:1";
            string before = File.ReadAllText(path);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(!store.TryUpdateParts(saved, changed, saved.anchors, out error), "Locked native state update succeeded");
            Require(File.ReadAllText(path) == before && saved.parts[0].foreignData.info == "SwordIron:2:3:0",
                "Failed write changed native attachment state");
            foreach (string malformed in new[] {
                json.Replace(serializedPackage, "not-base64"), json.Replace(serializedPackage, "AAAA"),
                json.Replace(serializedPackage, serializedPackage + " "), json.Replace("SwordIron:2:3:0", "SwordIron:2:0:0"),
                json.Replace("SwordIron:2:3:0", "../SwordIron:2:3:0"),
                json.Replace(dependencies, "\"requiredPrefabIds\":[\"itemstand\",\"woodwall\"]"),
                json.Replace("\"version\":11", "\"version\":3") })
            {
                bool rejected = false;
                Require(malformed != json, "Malformed native fixture did not change serialized input");
                try { BlueprintExchangeFormat.Decode(malformed); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Malformed native state or old envelope accepted");
            }
            Console.WriteLine("PASS native ItemStand snapshot -> store/reopen/portable, exact dependencies, immutable copy, failed write and malformed package rejection");
        }

        private static void ForeignItemStandRecipes(string path)
        {
            string header = "#Name:Mounted\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"\n";
            foreach (string holder in new[] { "itemstand", "itemstandh" })
                foreach (var recipe in new[] {
                Tuple.Create("ShieldIronTower:5", "ShieldIronTower:5:1:0"),
                Tuple.Create("ShieldIronTower:5:3", "ShieldIronTower:5:3:0"),
                Tuple.Create("ShieldIronTower:5:3:2", "ShieldIronTower:5:3:2") })
                    foreach (string token in new[] { "\"" + recipe.Item1 + "\"", recipe.Item1 })
                    {
                        string source = header + holder + ";;1;0;0;0;0;0;1;" + token + ";1;1;1";
                        var parsed = ExternalBlueprintLibrary.Parse(source, "mounts.blueprint");
                        var data = parsed.parts[1].foreignData;
                        Require(parsed.importError == null && data.format == Geometry.BlueprintForeignPartData.NativeItemStandFormat &&
                            data.info == recipe.Item2 && data.zdo == null && parsed.sourceDocuments[0].text == source,
                            "Known foreign holder recipe did not convert or lost source evidence");
                        var converted = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale));
                        var roundTrip = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(converted)));
                        Require(roundTrip.parts[1].foreignData.info == data.info && roundTrip.sourceDocuments[0].text == source,
                            "Native round-trip lost foreign mounted recipe");
                        var summary = CompositeBlueprintStore.InspectContents(converted, ForeignZdoChecks.Hash);
                        Require(summary.Mounts == 1 && summary.Unknown == 0 && summary.CanClearMounts,
                            "Recognized holder recipe still counted as unknown contents");
                        var cleared = CompositeBlueprintStore.CopyWithContentsPolicy(converted, false, true, ForeignZdoChecks.Hash);
                        Require(cleared.parts[1].foreignData.contentsCleared && !converted.parts[1].foreignData.contentsCleared &&
                            cleared.parts[1].foreignData.info == recipe.Item2 && cleared.sourceDocuments[0].text == source &&
                            CompositeBlueprintStore.InspectContents(cleared, ForeignZdoChecks.Hash).Mounts == 0,
                            "Clearing recognized holder contents destroyed source or changed the original");
                        var store = new CompositeBlueprintStore(path);
                        Require(store.TrySaveDocument(converted.name, converted.category, converted.parts, converted.groups, converted.anchors,
                            null, null, converted.supports, null, out var saved, out string error, sourceDocuments: converted.sourceDocuments), error);
                        var reopened = new CompositeBlueprintStore(path).All().Single(item => item.id == saved.id);
                        Require(reopened.parts[1].foreignData.info == recipe.Item2 && reopened.sourceDocuments[0].text == source,
                            "Disk round-trip changed normalized holder recipe or exact original");
                    }
            foreach (string row in new[] {
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0:1:0\";1;1;1;opaque",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0:1:0\";1;1;1;;0.5",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0\";1;1;1;opaque",
                "itemstandh;;1;0;0;0;0;0;1;\"SwordIron:0:3\";1;1;1;;0.5",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron\"",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0:1:0:future\"",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:-1\"",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:2147483648\"",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0:0\"",
                "itemstand;;1;0;0;0;0;0;1;\"SwordIron:0:NaN\"",
                "itemstand;;1;0;0;0;0;0;1;\"../SwordIron:0\"",
                "itemstand;;1;0;0;0;0;0;1;\"future:shape\"",
                "ArmorStand;;1;0;0;0;0;0;1;\"SwordIron:0:1:0\"" })
            {
                var parsed = ExternalBlueprintLibrary.Parse(header + row, "opaque.blueprint");
                Require(parsed.importError == null && parsed.parts[1].foreignData.format == "blueprint",
                    "Opaque/chance/armour payload incorrectly became an executable native holder recipe");
                Require(parsed.sourceDocuments[0].text == header + row &&
                    CompositeBlueprintStore.InspectContents(parsed, ForeignZdoChecks.Hash).Unknown > 0,
                    "Rejected holder recipe lost evidence or unknown-state warning");
            }
            Console.WriteLine("PASS foreign vertical/horizontal holder recipes convert once; exact original, portable round-trip and unknown state preserved");
        }

        private static void ForeignObjectRoundTrip(string path)
        {
            string text = "#Creator:Автор\r\n#Pieces\r\nitemstand;Furniture;0;0;0;0;0;0;1;\"SwordIron:2:3:3\";1;1;1;opaque-zdo;0.5\r\n" +
                "ArmorStand;Furniture;1;0;0;0;0;0;1;\"1:2:HelmetIron:0::0:\"\r\n#FutureData\r\nwall;not-a-piece\r\n";
            var parsed = ExternalBlueprintLibrary.Parse(text, "holders.blueprint");
            Require(parsed.importError == null && parsed.parts.Count == 2 && parsed.importPartCount == 2 &&
                parsed.parts[0].foreignData.info == "\"SwordIron:2:3:3\"" && parsed.parts[0].foreignData.zdo == "opaque-zdo" &&
                parsed.parts[0].foreignData.chance == "0.5" && CompositeBlueprintStore.HasForeignData(parsed),
                "Holder state or unknown section was discarded/parsed/executable");
            var copied = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale));
            Require(!ReferenceEquals(copied.parts[0].foreignData, parsed.parts[0].foreignData), "Detached foreign payload aliases caller");
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySaveDocument(copied.name, copied.category, copied.parts, copied.groups, copied.anchors,
                null, null, copied.supports, null, out var saved, out string error, sourceDocuments: copied.sourceDocuments), error);
            copied.parts[0].foreignData.info = "changed";
            Require(saved.parts[0].foreignData.info == parsed.parts[0].foreignData.info, "Save aliases foreign payload");
            var reopened = new CompositeBlueprintStore(path).All()[0];
            var portable = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            Require(portable.sourceDocuments[0].text == text && portable.parts[1].foreignData.info == parsed.parts[1].foreignData.info &&
                portable.parts[0].foreignData.chance == "0.5" && CompositeBlueprintStore.HasForeignData(portable), "Foreign state lost on disk/portable");
            var reconstructed = CompositeBlueprintStore.CloneParts(saved.parts);
            foreach (var part in reconstructed) part.foreignData = null;
            Require(store.TryUpdateParts(saved, reconstructed, saved.anchors, out error) && saved.parts[0].foreignData != null,
                "Legacy update silently erased foreign state");
            string before = File.ReadAllText(path);
            var changed = CompositeBlueprintStore.CloneParts(saved.parts);
            changed[0].foreignData.zdo = "updated";
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                Require(!store.TryUpdateDocument(saved, saved.name, saved.category, changed, saved.groups, saved.anchors,
                    null, null, out error), "Locked update succeeded");
            Require(File.ReadAllText(path) == before && saved.parts[0].foreignData.zdo == "opaque-zdo", "Failed write corrupted foreign state");
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            foreach (string invalid in new[] { json.Replace("\"version\":11", "\"version\":3"),
                json.Replace("\"format\":\"blueprint\",\"info\"", "\"format\":\"lua\",\"info\""),
                json.Replace("opaque-zdo", new string('x', 32769)), json.Replace("\"chance\":\"0.5\"", "\"chance\":\"0.5\",\"chance\":\"0.5\"") })
            {
                bool rejected = false;
                try { BlueprintExchangeFormat.Decode(invalid); } catch (InvalidDataException) { rejected = true; }
                Require(rejected, "Old schema/invalid format/oversized/duplicate foreign data accepted");
            }
            File.WriteAllText(path, before.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":12"));
            Require(new CompositeBlueprintStore(path).All().Count == 0 && File.ReadAllText(path).Contains("\"version\":12"),
                "Mislabeled native foreign data was accepted or overwritten");
            var empty = ExternalBlueprintLibrary.Parse("#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;1;\"\"", "empty.blueprint");
            Require(!CompositeBlueprintStore.HasForeignData(empty), "Compatible geometry requires an unnecessary adapter");
            var future = ExternalBlueprintLibrary.Parse("#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;1;\"\"\n#Future\ndata", "future.blueprint");
            Require(CompositeBlueprintStore.HasForeignData(future), "Unknown root section bypassed pending adapter gate");
            var vbuild = ExternalBlueprintLibrary.Parse("wall;0;0;0;1;0;0;0;opaque;.25\nwall;0;0;0;1;1;0;0", "extra.vbuild");
            Require(vbuild.parts.Count == 2 && vbuild.parts[0].foreignData.format == "vbuild" && vbuild.parts[0].foreignData.chance == ".25",
                "vbuild extension was lost");
            Console.WriteLine("PASS inert holder/object/ZDO/chance/unknown-section preservation; native15/portable6; clone/legacy update/rollback; pending execution gate");
        }

        private static void ForeignSignText(string directory)
        {
            Directory.CreateDirectory(directory);
            int sampleIndex = 0;
            const string prefix = "#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nsign;;1;0;0;0;0;0;1;";
            foreach (var sample in new[] {
                Tuple.Create("\"Добро пожаловать!\\n\\\"Дом\\\"\"", "Добро пожаловать!\n\"Дом\""),
                Tuple.Create(" <color=white>Dock 1 ", " <color=white>Dock 1 "),
                Tuple.Create("\"\"", (string)null), Tuple.Create("null", (string)null),
                Tuple.Create("\"3.6 Roentgen. Not Great. Not Terrible\" Achievement", "\"3.6 Roentgen. Not Great. Not Terrible\" Achievement"),
                Tuple.Create("\"broken", "\"broken"), Tuple.Create("\"valid\" trailing", "\"valid\" trailing")
            })
            {
                var result = ExternalBlueprintLibrary.Parse(prefix + sample.Item1, "sign.blueprint");
                Require(result.importError == null && result.parts.Count == 2 && result.parts[1].signText == sample.Item2,
                    "Foreign sign text changed or failed to decode");
                var prepared = ExternalBlueprintLibrary.PrepareForHost(result, _ => UnitScale);
                string path = Path.Combine(directory, (++sampleIndex).ToString() + ".json");
                var store = new CompositeBlueprintStore(path);
                Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                var reopened = new CompositeBlueprintStore(path).All().Single();
                var portable = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
                foreach (var copy in new[] { saved, reopened, portable })
                    Require(copy.parts[1].signText == sample.Item2 && copy.parts[1].sourceLine == 3 &&
                        copy.sourceDocuments.Single().text == prefix + sample.Item1, "Sign/source changed in disk or portable roundtrip");
            }
            foreach (string text in new[] { "\"Bad\\u0001text\"", "\"raw" + (char)1, new string('x', 4097), "\"" + new string('x', 4096) })
            {
                var result = ExternalBlueprintLibrary.Parse(prefix + text, "bad-sign.blueprint");
                Require(result.importError != null && result.parts.Count == 0, "Invalid sign accepted partially");
            }
            foreach (string prefab in new[] { "wood_door", "itemstand", "ArmorStand", "piece_chest", "mod_sign" })
            {
                var imported = ExternalBlueprintLibrary.Parse(prefix.Replace("\nsign;", "\n" + prefab + ";") + "\"1\"", "data.blueprint");
                Require(imported.parts.Count == 2 && imported.parts[1].signText == null &&
                    imported.parts[1].foreignData.info == "\"1\"" && CompositeBlueprintStore.HasForeignData(imported),
                    "Ambiguous object info was lost or treated as sign text");
            }
            var extended = ExternalBlueprintLibrary.Parse(prefix + "\"Text\";1;1;1;opaque", "zdo.blueprint");
            Require(extended.parts.Count == 2 && extended.parts[1].signText == "Text" && extended.parts[1].foreignData.zdo == "opaque",
                "Extended sign ZDO was discarded");
            foreach (string token in new[] { "\"broken", "\"valid\" trailing" })
            {
                bool refused = false;
                try { BlueprintExchangeFormat.DecodeSignText(token); } catch (InvalidDataException) { refused = true; }
                Require(refused, "Strict non-sign decoder accepted a legacy literal");
            }
            Console.WriteLine("PASS PlanBuild JSON/plain/leading-quote signs; controls/length reject atomically; non-sign decoder remains strict");
        }

        private static void ExternalImports(string directory)
        {
            const string rows = "wall;BuildingWorkbench;1;2;3;0;0;0;1;\"\";1;2;1\n" +
                "beam;BuildingWorkbench;-4;5;6;0;.7071068;0;.7071068;\"\";1;1;1\n";
            string source = "#Name:Example\n#Category:Building\n#Terrain\nsquare;NaN;malicious;ignored\n#Pieces\n" + rows;
            var parsed = ExternalBlueprintLibrary.Parse(source, "example.blueprint");
            Require(parsed.importError == null && parsed.parts.Count == 2 && parsed.IsExternal, "Basic blueprint/terrain separation failed");
            Require(parsed.parts[0].position.x == 1 && parsed.parts[0].position.y == 2 && parsed.parts[0].position.z == 3 &&
                parsed.parts[0].scale.y == 2 && Math.Abs(parsed.parts[1].rotation.y - .7071068f) < .000001f, "Position/rotation/scale ordering changed");
            var vbuild = ExternalBlueprintLibrary.Parse("wall;0;0;0;1;1;2;3\nbeam;0;0;0;1;-4;5;6", "sample.vbuild");
            Require(vbuild.importError == null && vbuild.parts[1].position.z == 6 && vbuild.parts[0].rotation.w == 1, "vbuild ordering");
            var whitespace = ExternalBlueprintLibrary.Parse("wall 0 0 0 1 1 2 3\nbeam\t0 0 0 1 -4 5 6", "buildshare.vbuild");
            Require(whitespace.importError == null && whitespace.parts[0].position.y == 2, "BuildShare whitespace dialect");
            Require(ExternalBlueprintLibrary.Parse("#Pieces\n" + rows, "no-headers.blueprint").importError == null, "Header-light source");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale);
            var copyA = ExternalBlueprintLibrary.DetachedCopy(prepared);
            var copyB = ExternalBlueprintLibrary.DetachedCopy(prepared);
            Require(copyA.id != copyB.id && copyA.parts[0].stableId != copyB.parts[0].stableId &&
                copyA.parts[0].stableId != parsed.parts[0].stableId, "Duplicate conversion identities collide");
            foreach (string invalid in new[] {
                source.Replace("1;2;3", "NaN;2;3"), source.Replace("1;2;3", "513;2;3"),
                source.Replace("0;0;0;1;", "0;0;0;0;"),
                source.Replace(";1;2;1", ";1;0;1"),
                "#Pieces\n" + string.Concat(Enumerable.Repeat(rows, CompositeBlueprintStore.MaximumParts / 2 + 1)), "#Pieces\n"
            })
            {
                var rejected = ExternalBlueprintLibrary.Parse(invalid, "bad.blueprint");
                Require(rejected.importError != null && rejected.parts.Count == 0, "Unsafe/partial source was accepted");
            }
            string folder = Path.Combine(directory, "imports"); Directory.CreateDirectory(folder);
            string original = Path.Combine(folder, "example.blueprint"); File.WriteAllText(original, source);
            string zipPath = Path.Combine(folder, "archive.zip");
            using (var output = File.Create(zipPath))
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("../outside.vbuild").Open())) writer.Write("wall;0;0;0;1;1;2;3\nbeam;0;0;0;1;4;5;6");
                using (var writer = new StreamWriter(zip.CreateEntry("never-load.dll").Open())) writer.Write("not code");
            }
            var library = ExternalBlueprintLibrary.Load(folder);
            Require(library.Count == 2 && !File.Exists(Path.Combine(directory, "outside.vbuild")), "Archive paths extracted or DLL treated as blueprint");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "import-copy.json"));
            Require(store.All().Count == 0 && store.CatalogAll().Count == 2 && !File.Exists(Path.Combine(directory, "import-copy.json")), "Browsing sources mutated native store");
            Require(!store.TryDelete(parsed, out _) && File.ReadAllText(original) == source, "Source library is writable");
            Require(store.TrySaveDocument(copyA.name, copyA.category, copyA.parts, copyA.groups, copyA.anchors,
                null, null, copyA.supports, null, out CompositeBlueprintStore.Blueprint saved, out string error,
                sourceDocuments: copyA.sourceDocuments), error);
            Require(saved.id != parsed.id && !saved.IsExternal, "Imported copy shares source identity");
            var loaded = new CompositeBlueprintStore(Path.Combine(directory, "import-copy.json"));
            Require(loaded.All().Count == 1 && loaded.All()[0].parts[0].baseScale.Value.y == 2 &&
                loaded.All()[0].parts[0].scale.y == 1 && loaded.CatalogAll().Count == 3,
                "Native copy cannot be reopened with original sources");
            Require(File.ReadAllText(original) == source, "Import/save rewrote original file");
            Console.WriteLine("PASS read-only .blueprint/.vbuild -> separate native v18 copy, exact poses/scales, inert terrain retention, rejection without partial pieces, inert archive");
        }

        private static CompositeBlueprintStore.VectorData UnitScale => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };

        private static void PinnedScaleBasis(string path)
        {
            var canonical = new UnityEngine.Vector3(2, .5f, 3);
            var hammer = new UnityEngine.Vector3(.5f, 2, .25f);
            var parts = new[] { Part("a", "wall", "Wall", null, true, false, 0),
                Part("b", "wall", "Wall", null, true, false, 2) };
            parts[0].baseScale = UnitScale;
            Require(parts[0].RootScale(canonical) == UnityEngine.Vector3.one &&
                parts[0].RootScale(hammer) == UnityEngine.Vector3.one &&
                parts[0].PlacementScale(hammer) == new UnityEngine.Vector3(2, .5f, 4),
                "Different canonical/Hammer bases changed the pinned root size");
            Require(parts[1].RootScale(canonical) == canonical, "Legacy unpinned scale changed meaning");
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySave("Pinned", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            string native = File.ReadAllText(path);
            string portable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            var loaded = new CompositeBlueprintStore(path).All().Single();
            var decoded = BlueprintExchangeFormat.Decode(portable);
            Require(loaded.parts[0].baseScale.Value.x == 1 && decoded.parts[0].PlacementScale(hammer) == new UnityEngine.Vector3(2, .5f, 4) &&
                !loaded.parts[1].baseScale.HasValue && !decoded.parts[1].baseScale.HasValue,
                "Pinned/legacy basis lost in native or portable roundtrip");
            var incoming = CompositeBlueprintStore.CloneParts(saved.parts);
            incoming[0].baseScale = null;
            Require(store.TryUpdateParts(saved, incoming, saved.anchors, out error) && saved.parts[0].baseScale.HasValue, error);
            incoming[0].baseScale = new CompositeBlueprintStore.VectorData { x = 2, y = 1, z = 1 };
            Require(store.TryUpdateParts(saved, incoming, saved.anchors, out error) && saved.parts[0].baseScale.Value.x == 2, error);
            incoming[0].baseScale = null;
            incoming[0].scaleIsAbsolute = true;
            Require(store.TryUpdateDocument(saved, incoming, saved.groups, saved.anchors, out error) &&
                saved.parts[0].scaleIsAbsolute && !saved.parts[0].baseScale.HasValue,
                "Full update silently retained an obsolete pinned basis: " + error);
            incoming[0].baseScale = UnitScale;
            incoming[0].scaleIsAbsolute = false;
            Require(store.TryUpdateParts(saved, incoming, saved.anchors, out error) && !saved.parts[0].scaleIsAbsolute, error);
            bool oldPortableRejected = false;
            try { BlueprintExchangeFormat.Decode(portable.Replace("\"version\":11", "\"version\":6")); }
            catch (InvalidDataException) { oldPortableRejected = true; }
            Require(oldPortableRejected, "Portable6 accepted a field from7");
            File.WriteAllText(path + ".old", native.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":16"));
            Require(new CompositeBlueprintStore(path + ".old").All().Count == 0, "Native16 accepted a field from17");
            var legacy = new CompositeBlueprintStore(path + ".legacy");
            parts[0].baseScale = null;
            Require(legacy.TrySave("Legacy", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var legacySaved, out error), error);
            File.WriteAllText(path + ".legacy", File.ReadAllText(path + ".legacy").Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":16"));
            Require(new CompositeBlueprintStore(path + ".legacy").All().Single().parts.All(part => !part.baseScale.HasValue), "Legacy16 guessed a base");
            string legacyPortable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(legacySaved));
            Require(BlueprintExchangeFormat.Decode(legacyPortable.Replace("\"scale\":{", "\"baseScale\":null,\"scale\":{")).parts.All(part => !part.baseScale.HasValue),
                "Explicit nullable base was rejected in the current schema");
            Require(BlueprintExchangeFormat.Decode(legacyPortable.Replace("\"version\":11", "\"version\":6")).parts.All(part => !part.baseScale.HasValue),
                "Legacy6 guessed a base");
            incoming = CompositeBlueprintStore.CloneParts(legacySaved.parts);
            incoming[0].baseScale = new CompositeBlueprintStore.VectorData { x = float.MaxValue, y = 1, z = 1 };
            incoming[0].scale.x = 4;
            Require(!legacy.TryUpdateParts(legacySaved, incoming, legacySaved.anchors, out _), "Overflowing absolute product accepted");
            incoming[0].baseScale = UnitScale;
            incoming[0].scaleIsAbsolute = true;
            Require(!legacy.TryUpdateParts(legacySaved, incoming, legacySaved.anchors, out _), "Pinned and pending scales accepted together");
            var tiny = parts[0];
            tiny.baseScale = new CompositeBlueprintStore.VectorData { x = .001f, y = .001f, z = .001f };
            Require(tiny.PlacementScale(tiny.baseScale.Value.ToVector3()) == UnityEngine.Vector3.one, "Tiny native base was clamped");
            bool rangeRejected = false;
            try { tiny.PlacementScale(UnityEngine.Vector3.one); } catch (ArgumentException) { rangeRejected = true; }
            Require(rangeRejected && tiny.RootScale(UnityEngine.Vector3.one).x == .001f, "Editor size was changed to force world admission");
            Console.WriteLine("PASS pinned scale basis: mismatched prefab bases, native18/portable8, legacy16/6, partial updates, strict versions, overflow and exact tiny-size world refusal");
        }

        private static void ForeignScaleSemantics(string path)
        {
            const string text = "#Pieces\nwall;;0;0;0;0;0;0;1;\"\";1;1;1\nsmall;;1;0;0;0;0;0;1;\"\";.001;.001;.001";
            var raw = ExternalBlueprintLibrary.Parse(text, "scaled.blueprint");
            Require(raw.importError == null && raw.importAbsoluteScale, "Absolute scale provenance missing");
            CompositeBlueprintStore.VectorData? Base(string name) => name == "wall"
                ? new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 }
                : new CompositeBlueprintStore.VectorData { x = .001f, y = .001f, z = .001f };
            var prepared = ExternalBlueprintLibrary.PrepareForHost(raw, Base);
            Require(prepared.importError == null && !prepared.importAbsoluteScale &&
                prepared.parts[0].scale.x == 1 && prepared.parts[0].baseScale.Value.x == 1 &&
                prepared.parts[0].baseScale.Value.y == 1 && prepared.parts[1].baseScale.Value.x == .001f &&
                prepared.parts[1].scale.x == 1,
                "Absolute localScale was changed instead of pinned");
            Require(raw.parts[0].scale.x == 1 && raw.parts[1].scale.x == .001f && raw.importAbsoluteScale &&
                ReferenceEquals(prepared, ExternalBlueprintLibrary.PrepareForHost(prepared, Base)), "Source mutated or divided twice");
            Require(prepared.id != raw.id && prepared.id == ExternalBlueprintLibrary.PrepareForHost(raw, Base).id &&
                prepared.id != ExternalBlueprintLibrary.PrepareForHost(raw, name => name == "wall" ? UnitScale : Base(name)).id,
                "Derived thumbnail identity reuses old absolute scale or varies without changed prefab scale");
            var missing = ExternalBlueprintLibrary.PrepareForHost(raw, name => name == "wall" ? Base(name) : null);
            Require(missing.parts.Count == 2 && missing.importError == null && !missing.importAbsoluteScale &&
                !missing.parts[0].scaleIsAbsolute && missing.parts[1].scaleIsAbsolute &&
                missing.parts[0].baseScale.Value.x == 1 && !missing.parts[1].baseScale.HasValue &&
                missing.parts[1].scale.x == .001f && raw.parts.Count == 2,
                "Missing prefab lost its record or explicit source scale basis");
            var incomplete = ExternalBlueprintLibrary.DetachedCopy(missing);
            incomplete.parts[0].position.x += .25f;
            incomplete.parts[1].position.z += 2;
            var incompleteStore = new CompositeBlueprintStore(path + ".pending");
            Require(incompleteStore.TrySaveDocument(incomplete.name, incomplete.category, incomplete.parts,
                incomplete.groups, incomplete.anchors, null, null, incomplete.supports, null,
                out var incompleteSaved, out string pendingError, sourceDocuments: incomplete.sourceDocuments), pendingError);
            var incompleteLoaded = new CompositeBlueprintStore(path + ".pending").All().Single();
            string pendingJson = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(incompleteLoaded));
            var incompleteDecoded = BlueprintExchangeFormat.Decode(pendingJson);
            Require(incompleteDecoded.parts.Count == 2 && incompleteDecoded.parts[1].scaleIsAbsolute &&
                incompleteDecoded.parts[1].scale.x == .001f && incompleteDecoded.parts[1].position.z == 2 &&
                incompleteDecoded.parts[0].position.x == .25f && !incompleteDecoded.parts[0].scaleIsAbsolute &&
                incompleteDecoded.sourceDocuments[0].text == text && raw.sourceDocuments[0].text == text,
                "Incomplete native/portable roundtrip lost scale basis, edits or exact source");
            var resolvedLater = ExternalBlueprintLibrary.PrepareForHost(incompleteDecoded, Base);
            Require(resolvedLater.id == incompleteDecoded.id && !resolvedLater.parts[1].scaleIsAbsolute &&
                resolvedLater.parts[1].scale.x == 1 && resolvedLater.parts[1].baseScale.Value.x == .001f &&
                resolvedLater.parts[0].baseScale.Value.x == 1 &&
                incompleteDecoded.parts[1].scaleIsAbsolute &&
                ReferenceEquals(resolvedLater, ExternalBlueprintLibrary.PrepareForHost(resolvedLater, Base)),
                "Late prefab resolution changed identity/source, divided known scale twice or left unknown basis");
            bool oldPortableRejected = false;
            try { BlueprintExchangeFormat.Decode(pendingJson.Replace("\"version\":11", "\"version\":5")); }
            catch (InvalidDataException) { oldPortableRejected = true; }
            Require(oldPortableRejected, "Pending scale silently accepted under old portable version");
            string pendingNative = File.ReadAllText(path + ".pending");
            File.WriteAllText(path + ".pending", pendingNative.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":14"));
            Require(new CompositeBlueprintStore(path + ".pending").All().Count == 0,
                "Pending scale silently accepted under old native version");
            var store = new CompositeBlueprintStore(path);
            Require(!store.TryExportFile(raw, out _, out string error) && error.Contains("scale_pending"), "Raw absolute scale exported as native multiplier");
            var copy = ExternalBlueprintLibrary.DetachedCopy(prepared);
            Require(store.TrySaveDocument(copy.name, copy.category, copy.parts, copy.groups, copy.anchors,
                null, null, copy.supports, null, out var saved, out error, sourceDocuments: copy.sourceDocuments), error);
            var reopened = new CompositeBlueprintStore(path).All()[0];
            var decoded = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            Require(decoded.parts[0].baseScale.Value.x == 1 && decoded.parts[0].baseScale.Value.y == 1 &&
                decoded.parts[0].baseScale.Value.z == 1 && decoded.parts[1].baseScale.Value.x == .001f &&
                decoded.parts[1].scale.x == 1 &&
                !decoded.importAbsoluteScale, "Native round-trip changed absolute size or provenance");
            var invalid = ExternalBlueprintLibrary.PrepareForHost(raw, _ => new CompositeBlueprintStore.VectorData { x = 0, y = 1, z = 1 });
            Require(invalid.parts.Count == 0 && invalid.importError != null, "Zero prefab base accepted");
            Console.WriteLine("PASS foreign absolute localScale, nonunit/small prefab bases, missing-scale retention, native round-trip, no double normalization");
        }

        private static void ExchangeFileWorkflow(string directory)
        {
            Directory.CreateDirectory(directory);
            string source = Path.Combine(directory, "selected.blueprint");
            const string text = "#Name:Selected\n#Pieces\nwall;Workbench;1;2;3;0;0;0;1;\"\";1.234567;2;.01001\nbeam;Workbench;0;0;0;0;0;0;1;\"\";1;1;1\n";
            File.WriteAllText(source, text);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"), backgroundImports: true);
            store.ConfigureImportScale(_ => UnitScale);
            WaitForImport(store);
            Require(store.CatalogAll().Count == 0 && store.TryImportFile(source, out string error), "Import start");
            WaitForImport(store);
            Require(store.ImportError == null && store.CatalogAll().Count == 1, "Import after first browsing failed: " + store.ImportError);
            Require(File.ReadAllText(source) == text && File.Exists(store.ImportResultPath),
                "Import must leave original unchanged and retain separate copy");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var reader = new StreamReader(zip.GetEntry("original/source.bin").Open()))
                Require(reader.ReadToEnd() == text, "Converted import lost exact original");
            var imported = store.CatalogAll()[0];
            string externalId = imported.id;
            Require(store.TryExportFile(imported, out string externalExport, out error), error);
            var externalDecoded = BlueprintExchangeFormat.Decode(File.ReadAllText(externalExport));
            Require(externalDecoded.parts[0].baseScale.Value.x == 1.234567f &&
                externalDecoded.parts[0].scale.x == 1 && imported.id == externalId,
                "Foreign export changed source or XYZ");
            Require(store.TryImportFile(externalExport, out error), error);
            WaitForImport(store);
            var portable = store.CatalogAll().Last(x => x.importSource.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase));
            Require(store.TryExportFile(portable, out string portableExport, out error) &&
                BlueprintExchangeFormat.Decode(File.ReadAllText(portableExport)).parts.Count == 2,
                "Imported native portable file cannot be re-exported: " + error);
            imported.previewYaw = 27; imported.previewPitch = -18; imported.previewZoom = 1.4f;
            var copy = ExternalBlueprintLibrary.DetachedCopy(imported);
            Require(store.TrySaveDocument(copy.name, copy.category, copy.parts, copy.groups, copy.anchors,
                copy.primaryPartId, copy.primaryGroupId, copy.supports, copy.placementSupportId,
                out var saved, out error, copy.customSnapPoints, copy), error);
            Require(saved.previewYaw == 27 && saved.previewPitch == -18 && saved.previewZoom == 1.4f &&
                saved.parts[0].baseScale.Value.x == 1.234567f && saved.parts[0].baseScale.Value.y == 2f &&
                saved.parts[0].baseScale.Value.z == .01001f && saved.parts[0].scale.x == 1,
                "Import/save changed preview or XYZ");
            var snapshot = store.CatalogAll();
            Require(ReferenceEquals(snapshot, store.CatalogAll()), "Catalog allocates a new snapshot on every call");
            Require(store.TryRename(saved, "Renamed", out error) && !ReferenceEquals(snapshot, store.CatalogAll()), "Metadata did not invalidate snapshot");
            saved.parts[0].prefabName = "missing_prefab";
            var evidence = saved.sourceDocuments[0];
            saved.sourceDocuments[0] = CompositeBlueprintStore.SourceDocument.FromGeometry(new Geometry.BlueprintSourceDocument(
                evidence.format, evidence.name, evidence.text.Replace("wall;", "missing_prefab;")));
            foreach (var part in saved.parts) part.sourceDocumentId = saved.sourceDocuments[0].id;
            Require(store.TryExportFile(saved, out string exported, out error), error);
            var decoded = BlueprintExchangeFormat.Decode(File.ReadAllText(exported));
            Require(decoded.parts[0].prefabName == "missing_prefab" && decoded.previewPitch == -18 &&
                decoded.parts[0].baseScale.Value.z == .01001f && decoded.parts[0].scale.z == 1,
                "Export consulted host or lost payload");
            byte[] firstExport = File.ReadAllBytes(exported);
            Require(store.TryExportFile(saved, out string exportedAgain, out error) && exportedAgain != exported &&
                File.ReadAllBytes(exported).SequenceEqual(firstExport), "Export overwrote existing file");
            Require(store.TryExportFile(saved, directory, "Chosen name", true, out string zipExport, out error), error);
            var zipped = ExternalBlueprintLibrary.LoadFile(zipExport);
            Require(Path.GetFileName(zipExport) == "Chosen name.zip" && zipped.Count == 1 && zipped[0].importError == null &&
                zipped[0].parts[0].baseScale.Equals(saved.parts[0].baseScale) &&
                zipped[0].parts[0].scale.z == saved.parts[0].scale.z, "Chosen filename/ZIP lost native document");
            byte[] zipBytes = File.ReadAllBytes(zipExport);
            Require(!store.TryExportFile(saved, directory, "Chosen name", true, out _, out error) &&
                File.ReadAllBytes(zipExport).SequenceEqual(zipBytes), "Named export overwrote an existing document");
            Require(!store.TryExportFile(saved, directory, "../escape", false, out _, out error) &&
                !File.Exists(Path.Combine(directory, "..", "escape.buildworks.json")), "Export filename escaped folder");
            Require(!store.TryExportFile(saved, directory, "unsafe.", false, out _, out error), "Trailing-dot filename accepted");
            Require(CompositeBlueprintStore.BrowseExchangeDirectory(directory).Contains(zipExport) &&
                !CompositeBlueprintStore.BrowseExchangeDirectory(directory).Contains(Path.Combine(directory, "library.json")),
                "Browser omitted supported files or exposed native internal store");
            Require(!store.TryExportFile(new CompositeBlueprintStore.Blueprint { importError = "$buildworks_import.format" }, out _, out error),
                "Blocked document exported as valid");
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
            string archive = Path.Combine(directory, "paired.zip");
            using (var file = File.Create(archive))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("nested/model.blueprint").Open())) writer.Write(text);
                using (var image = zip.CreateEntry("nested/model.png").Open()) image.Write(png, 0, png.Length);
                using (var image = zip.CreateEntry("unpaired.png").Open()) image.Write(png, 0, png.Length);
            }
            byte[] zipBefore = File.ReadAllBytes(archive);
            var sources = ExternalBlueprintLibrary.LoadFile(archive);
            Require(sources.Count == 1 && sources[0].importPreview.SequenceEqual(png) &&
                File.ReadAllBytes(archive).SequenceEqual(zipBefore) && !Directory.Exists(Path.Combine(directory, "nested")),
                "Paired preview absent, extra image exposed, source rewritten or package extracted");
            png[16] = 127;
            Require(!ExternalBlueprintLibrary.ValidPreview(png) && !ExternalBlueprintLibrary.ValidPreview(new byte[30]),
                "Oversized/corrupt PNG passed decoder boundary");
            Console.WriteLine("PASS background import, separate disk copy, cached snapshot, native save exact XYZ/preview, host-independent atomic export, bounded paired PNG");
        }

        private static void WaitForImport(CompositeBlueprintStore store)
        {
            Require(System.Threading.SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Import worker timeout");
        }

        private static int BenchmarkExchange(int count)
        {
            var blueprint = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"),
                name = "Benchmark", category = "OTHER", prefabName = "woodwall" };
            for (int i = 0; i < count; ++i)
            {
                var part = Part("part" + i, "woodwall", "Wall", null, true, false, 1);
                part.position = Vector(i % 100, i / 100, 0);
                blueprint.parts.Add(part);
            }
            var clock = System.Diagnostics.Stopwatch.StartNew();
            byte[] bytes = BlueprintExchangeFormat.Encode(blueprint);
            double encode = clock.Elapsed.TotalMilliseconds;
            clock.Restart();
            var decoded = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(bytes));
            Require(decoded.parts.Count == count && decoded.parts[count - 1].stableId == "part" + (count - 1), "Large roundtrip truncated");
            Console.WriteLine("parts,json_bytes,encode_ms,strict_decode_ms,managed_live_bytes");
            Console.WriteLine(FormattableString.Invariant($"{count},{bytes.Length},{encode:F3},{clock.Elapsed.TotalMilliseconds:F3},{GC.GetTotalMemory(false)}"));
            return 0;
        }

        private static void InspectActualLibrary(string path)
        {
            int total = 0, supported = 0, blocked = 0, packets = 0, convertedStates = 0, remainingStates = 0, scaleBlocked = 0;
            int armorStates = 0, armorOnlyDocuments = 0;
            int placementProvenanceDocuments = 0, placementProvenanceOnlyBlocker = 0;
            using (Stream input = File.OpenRead(path))
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read))
                foreach (ZipArchiveEntry entry in zip.Entries.Where(x => x.FullName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase)))
                {
                    using (var reader = new StreamReader(entry.Open()))
                    {
                        string text = reader.ReadToEnd();
                        foreach (string line in text.Split('\n'))
                        {
                            string[] fields = line.TrimEnd('\r').Split(';');
                            if (fields.Length < 14 || fields[13] == "" || fields[13] == "null" || fields[13] == "\"\"") continue;
                            Require(Geometry.BlueprintForeignZdo.TryRead(fields[13], out _), "Actual ZDO reader rejected " + entry.FullName);
                            ++packets;
                        }
                        var parsed = ExternalBlueprintLibrary.Parse(text, entry.FullName);
                        if (parsed.importError == null)
                        {
                            var converted = ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale, ForeignZdoChecks.Hash, 0);
                            if (converted.importError != null)
                            {
                                Require(converted.importError.StartsWith("$buildworks_import.scale_range\t", StringComparison.Ordinal),
                                    "Actual native-rules conversion failed: " + entry.FullName + " " + converted.importError);
                                ++scaleBlocked; // Unit prefab bases are a file-only probe, not actual host scale availability.
                            }
                            else
                            {
                                var summary = CompositeBlueprintStore.InspectContents(converted, ForeignZdoChecks.Hash);
                                if (text.Contains("#Coordinates:") && !parsed.sourceDocuments[0].ToGeometry().HasOpaqueSections)
                                {
                                    ++placementProvenanceDocuments;
                                    if (summary.Unknown == 0) ++placementProvenanceOnlyBlocker;
                                }
                                armorStates += summary.ArmorStates;
                                if (summary.ArmorStates > 0 && summary.RemainingUnknown(true) == 0) ++armorOnlyDocuments;
                                for (int index = 0; index < parsed.parts.Count; ++index)
                                if (parsed.parts[index].foreignData != null)
                                {
                                    if (converted.parts[index].foreignData == null || converted.parts[index].foreignData.format == Geometry.BlueprintForeignPartData.NativeItemStandFormat)
                                        ++convertedStates;
                                    else ++remainingStates;
                                }
                            }
                        }
                        PlanBuildSnapTailChecks.CheckActual(entry.FullName, text, parsed);
                        ++total; if (parsed.importError == null) ++supported; else ++blocked;
                        if (parsed.name == "CoffeeTable") Require(parsed.importError == null && parsed.parts.Count == 4, "Actual CoffeeTable cannot be converted");
                        if (parsed.name == "WoodCouch1") Require(parsed.importError == null &&
                            parsed.parts.Count(part => part.prefabName == "sign" && part.signText == "...") == 2,
                            "Actual WoodCouch1 sign text was lost or changed");
                    }
                }
            Require(total == 353 && supported > 0 && total == supported + blocked, "Downloaded library coverage");
            Console.WriteLine("PASS actual BiomeBlueprints: " + total + " visible source entries; " + supported + " format-compatible; " + blocked + " explicitly blocked (host prefab availability is separate)");
            Require(packets == 133738, "Actual packet coverage changed");
            Console.WriteLine("PASS actual typed ZDO: " + packets + " exact EOF; " + convertedStates + " known states converted under native construction rules; " + remainingStates + " retain explicit blockers; " + scaleBlocked + " plans correctly refused by unit-base scale probe");
            Console.WriteLine("PASS actual ArmorStand omission review: " + armorStates + " eligible Info-only records; " + armorOnlyDocuments + " documents have armor as their only unknown state (not host placement acceptance)");
            Console.WriteLine("PASS actual original-placement headers: " + placementProvenanceDocuments +
                " parsed documents retain understood provenance; " + placementProvenanceOnlyBlocker +
                " previously blocked only by these headers now have no unknown state (prefab availability/build/reload not proven)");
        }

        private static void ExchangeRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[] { Part("sign-a", "sign", "Sign", "group-a", false, true, 1),
                Part("wall-b", "woodwall", "Wall", null, true, false, 2) };
            parts[0].signText = "Дом Ostrix\nВход → здесь <color=yellow>текст</color>";
            var groups = new[] { new CompositeBlueprintStore.Group { stableId = "group-a", name = "Group", pivotPartId = "sign-a" } };
            var points = new[] { Vector(2, 3, 4) };
            Require(store.TrySaveDocument("Exchange", "OTHER", parts, groups, Array.Empty<CompositeBlueprintStore.VectorData>(),
                "sign-a", "group-a", out var saved, out string error, points), error);
            var reopened = new CompositeBlueprintStore(path).All()[0];
            Require(reopened.parts[0].signText == parts[0].signText && reopened.customSnapPoints[0].z == 4, "Native fields lost");
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            var decoded = BlueprintExchangeFormat.Decode(json);
            Require(decoded.parts[0].signText == parts[0].signText && decoded.primaryPartId == "sign-a" &&
                decoded.groups[0].pivotPartId == "sign-a" && decoded.customSnapPoints[0].y == 3, "Portable fields lost");
            var external = ExternalBlueprintLibrary.Parse(json, "roundtrip.buildworks.json");
            Require(external.importError == null && external.IsExternal, "Portable file not available in source catalog");
            Require(external.id != reopened.id && ExternalBlueprintLibrary.Parse(json.Replace("Wall", "Other wall"),
                "roundtrip.buildworks.json").id != external.id && ExternalBlueprintLibrary.Parse(json, "other.buildworks.json").id != external.id,
                "Portable sources collide with native/revised runtime identities");
            var copy = ExternalBlueprintLibrary.DetachedCopy(external);
            Require(copy.groups[0].stableId != "group-a" && copy.parts[0].parentGroupId == copy.groups[0].stableId &&
                copy.groups[0].pivotPartId == copy.parts[0].stableId && copy.primaryPartId == copy.parts[0].stableId &&
                copy.primaryGroupId == copy.groups[0].stableId && copy.parts[0].signText == parts[0].signText &&
                copy.customSnapPoints[0].x == 2 && !copy.parts[0].editorVisible && copy.parts[0].editorLocked, "Detached copy lost hierarchy/data");
            foreach (string invalid in new[] { json.Replace("\"version\":11", "\"version\":1099"),
                json.Replace("\"format\":", "\"future\":42,\"format\":"),
                json.Replace("\"version\":11", "\"version\":5,\"version\":5"),
                json.Replace("\"x\":2", "\"x\":null"), json.Replace("\"x\":2", "\"extra\":2,\"x\":2"),
                json.Replace("\"previewZoom\":1,", ""), json.Replace("\"name\":\"Group\"", "\"name\":\" Group \""),
                json.Replace("\"previewZoom\":1", "\"previewZoom\":0") })
                Require(ExternalBlueprintLibrary.Parse(invalid, "bad.buildworks.json").importError != null, "Unknown/invalid portable data accepted");
            var snapSource = ExternalBlueprintLibrary.Parse("#Name:Snap\n#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;1;\"\"\n#SnapPoints\n2;3;4", "snap.blueprint");
            Require(snapSource.importError == null && snapSource.customSnapPoints.Count == 1 &&
                ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(snapSource, _ => UnitScale)).customSnapPoints[0].z == 4, "Foreign explicit snap point lost");
            Require(ExternalBlueprintLibrary.Parse("#Pieces\nwall;;0;0;0;0;0;0;1;\"\"\nwall;;1;0;0;0;0;0;0;1;\"\"\n#SnapPoints\nNaN;0;0", "bad.blueprint").importError != null, "Nonfinite snap accepted");
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, out error), error);
            Require(new CompositeBlueprintStore(path).All()[0].customSnapPoints.Count == 1, "Legacy save path cleared custom points");
            string original = File.ReadAllText(path);
            File.WriteAllText(path, original.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"future\":123,\"version\":14"));
            var unknown = new CompositeBlueprintStore(path);
            Require(!unknown.TrySave(parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _) &&
                File.ReadAllText(path).Contains("\"future\":123"), "Unknown native fields silently discarded");
            Console.WriteLine("PASS v18/public v8 roundtrip, sign UTF-8, custom sockets, strict unknown fields, detached hierarchy");
        }

        private static void SupportRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[] { Part("a", "woodwall", "A", "group", true, false, 0),
                Part("b", "woodwall", "B", null, true, false, 1) };
            var groups = new[] { new CompositeBlueprintStore.Group { stableId = "group", name = "Group" } };
            var supports = new[] { new CompositeBlueprintStore.Support { stableId = "support", name = "Вход",
                position = Vector(2, 3, 4), rotation = new CompositeBlueprintStore.QuaternionData { y = .7071068f, w = .7071068f } } };
            var points = new[] { Vector(7, 8, 9) };
            Require(store.TrySaveDocument("Supports", "OTHER", parts, groups, Array.Empty<CompositeBlueprintStore.VectorData>(),
                "a", "group", supports, "support", out var saved, out string error, points), error);
            supports[0].position = Vector(9, 9, 9);
            supports[0].name = "Changed";
            Require(saved.supports[0].position.x == 2 && saved.supports[0].name == "Вход", "Saved support aliases caller");
            var reopened = new CompositeBlueprintStore(path).All()[0];
            Require(reopened.supports.Count == 1 && reopened.placementSupportId == "support" &&
                reopened.supports[0].position.z == 4 && reopened.supports[0].rotation.y == .7071068f &&
                reopened.parts[0].position.x == 0 && reopened.primaryPartId == "a" && reopened.primaryGroupId == "group",
                "Support native roundtrip changed pose, roots or fallback frame");
            string json = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            Require(json.Contains("\"version\":11"), "Portable version was not bumped");
            var decoded = BlueprintExchangeFormat.Decode(json);
            Require(decoded.supports[0].name == "Вход" && decoded.placementSupportId == "support" &&
                decoded.supports[0].rotation.w == .7071068f, "Portable support pose/default lost");
            var external = ExternalBlueprintLibrary.Parse(json, "supports.buildworks.json");
            var copy = ExternalBlueprintLibrary.DetachedCopy(external);
            Require(copy.supports[0].stableId != "support" && copy.placementSupportId == copy.supports[0].stableId &&
                copy.supports[0].position.x == 2 && copy.parts[0].position.x == 0 && copy.customSnapPoints[0].z == 9,
                "External copy lost support, geometry, sockets or stable reference remap");
            copy.supports[0].name = "Copied";
            Require(external.supports[0].name == "Вход", "Detached support aliases source");
            Require(store.TryUpdateParts(saved, saved.parts, saved.anchors, out error), error);
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, out error), error);
            Require(new CompositeBlueprintStore(path).All()[0].placementSupportId == "support" && saved.supports.Count == 1,
                "Legacy updates cleared supports");

            string original = File.ReadAllText(path);
            var originalSupports = saved.supports;
            Directory.CreateDirectory(path + ".tmp");
            Require(!store.TryUpdateDocument(saved, "Failed", "NEW", saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, supports, null, out error, Array.Empty<CompositeBlueprintStore.VectorData>()),
                "Blocked support write succeeded");
            Require(ReferenceEquals(saved.supports, originalSupports) && saved.placementSupportId == "support" &&
                saved.customSnapPoints[0].z == 9 && !store.Categories().Contains("NEW") && File.ReadAllText(path) == original,
                "Failed support write lost memory/default/socket/category/original data");
            Directory.Delete(path + ".tmp");
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, saved.supports, null, out error), error);
            Require(saved.supports.Count == 1 && saved.placementSupportId == null &&
                new CompositeBlueprintStore(path).All()[0].placementSupportId == null, "Explicit clear retained old default");
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, Array.Empty<CompositeBlueprintStore.Support>(), null, out error), error);
            Require(new CompositeBlueprintStore(path).All()[0].supports.Count == 0, "Explicit empty support list retained old data");

            // Legacy files have no support fields; reading them must not rewrite the original.
            decoded.supports = null; decoded.placementSupportId = null;
            string legacyPortable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(decoded))
                .Replace("\"version\":11", "\"version\":1");
            Require(BlueprintExchangeFormat.Decode(legacyPortable).supports.Count == 0, "Portable v1 not migrated");
            string legacyNative = Json(new CompositeBlueprintStore.LibraryData { version = 10, hiddenSources = null,
                blueprints = new List<CompositeBlueprintStore.Blueprint> { decoded } });
            File.WriteAllText(path, legacyNative);
            var legacy = new CompositeBlueprintStore(path);
            Require(legacy.All().Count == 1 && legacy.All()[0].supports.Count == 0 && File.ReadAllText(path) == legacyNative,
                "Native v10 not migrated without overwriting source");
            Require(legacy.TryRename(legacy.All()[0], "Migrated", out error) &&
                File.ReadAllText(path).Contains("\"version\":22"), "Legacy migration not saved as v17");

            foreach (string invalid in new[] { json.Replace("\"version\":11", "\"version\":1"),
                json.Replace("\"stableId\":\"support\"", "\"stableId\":\"a\""),
                json.Replace("\"stableId\":\"support\"", "\"stableId\":\"group\""),
                json.Replace("\"placementSupportId\":\"support\"", "\"placementSupportId\":\"missing\""),
                json.Replace("\"name\":\"Вход\"", "\"name\":\"<b>Вход\""),
                json.Replace("\"name\":\"Вход\"", "\"name\":\" Вход\""),
                json.Replace("\"name\":\"Вход\"", "\"future\":1,\"name\":\"Вход\""),
                json.Replace("\"name\":\"Вход\"", "\"name\":\"Вход\",\"name\":\"Duplicate\""),
                json.Replace("\"x\":2", "\"x\":513"),
                json.Replace("\"supports\":[", "\"supports\":[null,"),
                json.Replace("0.7071068", "0.5") })
            {
                Require(invalid != json, "Invalid test did not mutate the source");
                Require(ExternalBlueprintLibrary.Parse(invalid, "invalid.buildworks.json").importError != null,
                    "Invalid support accepted via portable decoder");
            }

            foreach (var invalid in new[] { new CompositeBlueprintStore.Support(),
                new CompositeBlueprintStore.Support { stableId = "support", name = "Bad", position = Vector(float.NaN, 0, 0),
                    rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } },
                new CompositeBlueprintStore.Support { stableId = "support", name = new string('x', 49),
                    rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } } })
                Require(!store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                    saved.primaryPartId, saved.primaryGroupId, new[] { invalid }, null, out error), "Invalid support pose/label accepted");
            var many = Enumerable.Range(0, 64).Select(i => new CompositeBlueprintStore.Support { stableId = "s" + i,
                name = "Support", rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } }).ToList();
            Require(store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, many, "s63", out error), error);
            string maximumJson = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            Require(BlueprintExchangeFormat.Decode(maximumJson).supports.Count == 64, "Maximum support count rejected");
            many.Add(new CompositeBlueprintStore.Support { stableId = "s64", name = "Support",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } });
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, many, null, out error), "65 supports accepted");
            Require(ExternalBlueprintLibrary.Parse(maximumJson.Replace("\"supports\":[", "\"supports\":[" + Json(many[64]) + ","),
                "overflow.buildworks.json").importError != null, "Decoder accepted 65 supports");

            foreach (string invalidNative in new[] { original.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":10"),
                original.Replace("\"name\":\"Вход\"", "\"future\":true,\"name\":\"Вход\""),
                original.Replace("\"placementSupportId\":\"support\"", "\"placementSupportId\":\"missing\"") })
            {
                File.WriteAllText(path, invalidNative);
                var blocked = new CompositeBlueprintStore(path);
                Require(!blocked.TrySave(parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _) &&
                    File.ReadAllText(path) == invalidNative, "Invalid native support file was rewritten");
            }
            Console.WriteLine("PASS support v18/v8 disk/exchange/copy; v10/v1 migration; explicit clear; legacy retain; atomic rollback; strict IDs/pose/labels/version/count");
        }

        private static string Json<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return System.Text.Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static void RoundTripV9(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", "group-inner", false, true, 1f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 2f)
            };
            var groups = new List<CompositeBlueprintStore.Group>
            {
                new CompositeBlueprintStore.Group
                {
                    stableId = "group-a",
                    name = "Hidden group",
                    editorVisible = false,
                    editorLocked = true
                },
                new CompositeBlueprintStore.Group
                {
                    stableId = "group-inner",
                    name = "Nested group",
                    parentGroupId = "group-a",
                    pivotPartId = "part-a"
                }
            };
            parts[0].scale = Vector(2f, 2f, 2f);
            var anchors = new List<CompositeBlueprintStore.VectorData>
            {
                Vector(0f, 0f, 0f),
                Vector(2f, 3f, 4f)
            };

            Require(store.TrySaveDocument(
                parts, groups, anchors, out CompositeBlueprintStore.Blueprint saved,
                out string error), error);
            Require(File.ReadAllText(path).Contains("\"version\":22"), "v17 was not written");

            var reopened = new CompositeBlueprintStore(path);
            Equal(1, reopened.All().Count, "blueprint count");
            CompositeBlueprintStore.Blueprint blueprint = reopened.All()[0];
            Equal(saved.id, blueprint.id, "blueprint id");
            Equal(2, blueprint.groups.Count, "group count");
            Equal("group-a", blueprint.groups[0].stableId, "group id");
            Equal(false, blueprint.groups[0].editorVisible, "group visibility");
            Equal(true, blueprint.groups[0].editorLocked, "group lock");
            Equal("group-a", blueprint.groups[1].parentGroupId, "nested group parent");
            Equal("part-a", blueprint.groups[1].pivotPartId, "nested group pivot");
            Equal("part-a", blueprint.parts[0].stableId, "part id");
            Equal("Wall A", blueprint.parts[0].displayName, "part name");
            Equal("group-inner", blueprint.parts[0].parentGroupId, "part parent");
            Equal(false, blueprint.parts[0].editorVisible, "part visibility");
            Equal(true, blueprint.parts[0].editorLocked, "part lock");
            Equal(1f, blueprint.parts[0].position.x, "part position");
            Equal(2f, blueprint.parts[0].scale.x, "part scale x");
            Equal(2f, blueprint.parts[0].scale.y, "part scale y");
            Equal(2f, blueprint.parts[0].scale.z, "part scale z");
            Equal(1f, blueprint.parts[1].scale.x, "default part scale");
            blueprint.groups[1].pivotPartId = "part-b";
            Require(!store.TryUpdateDocument(blueprint, blueprint.parts, blueprint.groups,
                blueprint.anchors, out error), "out-of-group pivot was accepted");
        }

        private static void MigrateV5Scale(string path)
        {
            RoundTripV9(path);
            string original = System.Text.RegularExpressions.Regex.Replace(
                File.ReadAllText(path).Replace(",\"hiddenSources\":[]", "").Replace("\"version\":22", "\"version\":5").Replace(",\"supports\":[]", ""),
                ",\"scale\":\\{[^}]+\\}", string.Empty);
            File.WriteAllText(path, original);
            var migrated = new CompositeBlueprintStore(path);
            Equal(1, migrated.All().Count, "v5 migration count");
            foreach (CompositeBlueprintStore.Part part in migrated.All()[0].parts)
                Equal(1f, part.scale.x, "v5 migration unit scale");
            Equal(original, File.ReadAllText(path), "migration does not modify original until Save");
        }

        private static void PrimaryPartRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 1f),
                Part("part-b", "wood_pole2", "Pole", null, true, false, 1f)
            };
            Require(store.TrySaveDocument("Primary", "ПРОЧЕЕ", parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "part-b",
                out CompositeBlueprintStore.Blueprint saved, out string error), error);
            Equal("part-b", saved.primaryPartId, "saved primary part");
            Equal("wood_pole2", saved.prefabName, "primary prefab identity");

            var reopened = new CompositeBlueprintStore(path);
            Equal("part-b", reopened.All()[0].primaryPartId, "reopened primary part");
            Require(!reopened.TryUpdateDocument(reopened.All()[0], "Primary", "ПРОЧЕЕ",
                parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "missing", out error),
                "missing primary part was accepted");
        }

        private static void PrimaryGroupRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var groups = new[]
            {
                new CompositeBlueprintStore.Group { stableId = "frame", name = "Frame" },
                new CompositeBlueprintStore.Group
                    { stableId = "nested", name = "Nested", parentGroupId = "frame" }
            };
            var parts = new[]
            {
                Part("part-a", "woodwall", "A", "nested", true, false, 1f),
                Part("part-b", "wood_pole2", "B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument("Group frame", "ПРОЧЕЕ", parts, groups,
                Array.Empty<CompositeBlueprintStore.VectorData>(), null, "frame",
                out CompositeBlueprintStore.Blueprint saved, out string error), error);
            Equal("frame", saved.primaryGroupId, "saved primary group");
            Equal(0, CompositeBlueprintStore.FramePartIndex(saved), "group frame part");
            Equal(true, CompositeBlueprintStore.IsFramePart(saved, 0), "nested group part included");
            Equal(false, CompositeBlueprintStore.IsFramePart(saved, 1), "root part excluded");

            Require(store.TryUpdateDocument(saved, saved.name, saved.category, parts, groups,
                saved.anchors, "part-b", "frame", out error), error);
            Equal(1, CompositeBlueprintStore.FramePartIndex(saved), "primary part wins over group");
            Equal(false, CompositeBlueprintStore.IsFramePart(saved, 0), "group loses to primary part");
            Equal(true, CompositeBlueprintStore.IsFramePart(saved, 1), "primary part selected");
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, parts, groups,
                saved.anchors, null, "missing", out error), "missing primary group was accepted");
        }

        private static void RejectInvalidScale(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[]
            {
                Part("a", "woodwall", "A", null, true, false, 0f),
                Part("b", "woodwall", "B", null, true, false, 1f)
            };
            foreach (float scale in new[] { 0f, -1f, 0.009f, 10.01f, float.NaN, float.PositiveInfinity })
            {
                parts[0].scale = Vector(scale, 1f, 1f);
                Require(!store.TrySaveDocument(parts, Array.Empty<CompositeBlueprintStore.Group>(),
                    Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _),
                    "invalid scale accepted: " + scale);
                Equal(0, store.All().Count, "invalid scale mutated library");
                Require(!File.Exists(path), "invalid scale created file");
            }
        }

        private static void ScaleUpdateAndRollback(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new[]
            {
                Part("a", "woodwall", "A", null, true, false, 0f),
                Part("b", "woodwall", "B", null, true, false, 1f)
            };
            parts[0].scale = Vector(0.01f, 1.25f, 10f);
            Require(store.TrySaveDocument(parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), out var blueprint, out string error), error);
            Equal(10f, new CompositeBlueprintStore(path).All()[0].parts[0].scale.z, "1000% native reopen");
            var portable = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(blueprint)));
            Equal(10f, portable.parts[0].scale.z, "1000% portable roundtrip");
            parts[0].scale = Vector(2f, 3f, 0.5f);
            Equal(0.01f, blueprint.parts[0].scale.x, "saved scale is detached from caller");
            Require(store.TryUpdateDocument(blueprint, parts, blueprint.groups, blueprint.anchors,
                out error), error);
            var reopened = new CompositeBlueprintStore(path).All()[0];
            Equal(2f, reopened.parts[0].scale.x, "updated scale x");
            Equal(3f, reopened.parts[0].scale.y, "updated scale y");
            Equal(0.5f, reopened.parts[0].scale.z, "updated scale z");

            parts[0].scale = Vector(2.5f, 3.5f, 0.75f);
            Require(store.TryUpdateParts(blueprint, parts, blueprint.anchors, out error), error);
            reopened = new CompositeBlueprintStore(path).All()[0];
            Equal(2.5f, reopened.parts[0].scale.x, "workspace update retains incoming scale");
            Equal(3.5f, reopened.parts[0].scale.y, "workspace update scale y");
            Equal(0.75f, reopened.parts[0].scale.z, "workspace update scale z");

            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");
            parts[0].scale = Vector(1.5f, 1.5f, 1.5f);
            Require(!store.TryUpdateDocument(blueprint, parts, blueprint.groups, blueprint.anchors,
                out error), "blocked scale write succeeded");
            Equal(2.5f, blueprint.parts[0].scale.x, "failed scale write rolls back memory");
            Equal(3.5f, blueprint.parts[0].scale.y, "failed scale write rolls back all axes");
            Equal(original, File.ReadAllText(path), "failed scale write preserves file");
        }

        private static void MigrateV3(string path)
        {
            string id = Guid.NewGuid().ToString("N");
            File.WriteAllText(path,
                "{\"version\":3,\"blueprints\":[{" +
                "\"id\":\"" + id + "\",\"name\":\"Legacy\"," +
                "\"prefabName\":\"woodwall\",\"category\":\"ПРОЧЕЕ\"," +
                "\"previewYaw\":45,\"previewPitch\":30,\"previewZoom\":1," +
                "\"parts\":[" + LegacyPart(0f) + "," + LegacyPart(1f) + "]," +
                "\"anchors\":[{\"x\":0,\"y\":0,\"z\":0}]}]," +
                "\"categories\":[\"ПРОЧЕЕ\"]}");

            var migrated = new CompositeBlueprintStore(path);
            Equal(1, migrated.All().Count, "migrated blueprint count");
            CompositeBlueprintStore.Blueprint blueprint = migrated.All()[0];
            Equal(0, blueprint.groups.Count, "migrated group count");
            Equal(id + "_part_000", blueprint.parts[0].stableId, "migrated first id");
            Equal(id + "_part_001", blueprint.parts[1].stableId, "migrated second id");
            Equal("woodwall", blueprint.parts[0].displayName, "migrated name");
            Equal(true, blueprint.parts[0].editorVisible, "migrated visibility");

            Require(migrated.TryUpdateDocument(
                blueprint, blueprint.parts, blueprint.groups, blueprint.anchors,
                out string error), error);
            Require(File.ReadAllText(path).Contains("\"version\":22"),
                "migration was not persisted as v17");
            Equal(id + "_part_000",
                new CompositeBlueprintStore(path).All()[0].parts[0].stableId,
                "migrated id after reopen");
        }

        private static void MetadataRoundTrip(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 0f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument(
                "Bridge", "wood",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint blueprint,
                out string error), error);
            Equal("Bridge", blueprint.name, "saved metadata name");
            Equal("WOOD", blueprint.category, "saved metadata category");
            Require(store.Categories().Contains("WOOD"), "saved category missing");

            Require(store.TryUpdateDocument(
                blueprint,
                "Stone bridge",
                "stone",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out error), error);

            CompositeBlueprintStore.Blueprint reopened =
                new CompositeBlueprintStore(path).All()[0];
            Equal("Stone bridge", reopened.name, "updated metadata name");
            Equal("STONE", reopened.category, "updated metadata category");
        }

        private static void RejectGroupCycle(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var groups = new[]
            {
                new CompositeBlueprintStore.Group
                    { stableId = "a", name = "A", parentGroupId = "b" },
                new CompositeBlueprintStore.Group
                    { stableId = "b", name = "B", parentGroupId = "a" }
            };
            Require(!store.TrySaveDocument(
                new[]
                {
                    Part("part-a", "woodwall", "Wall A", "a", true, false, 0f),
                    Part("part-b", "woodwall", "Wall B", "b", true, false, 1f)
                },
                groups,
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out _, out _), "cyclic groups were accepted");
            Require(!File.Exists(path), "invalid group cycle was written");
        }

        private static void FailedWritePreservesOriginal(string path)
        {
            var store = new CompositeBlueprintStore(path);
            var parts = new List<CompositeBlueprintStore.Part>
            {
                Part("part-a", "woodwall", "Wall A", null, true, false, 0f),
                Part("part-b", "woodwall", "Wall B", null, true, false, 1f)
            };
            Require(store.TrySaveDocument(
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint blueprint,
                out string error), error);
            string original = File.ReadAllText(path);
            Directory.CreateDirectory(path + ".tmp");

            Require(!store.TryRename(blueprint, "Must roll back", out error),
                "blocked write unexpectedly succeeded");
            Equal("Group 1", blueprint.name, "failed rename rollback");
            Require(!store.TryUpdateDocument(
                blueprint,
                "Must roll back",
                "NEW CATEGORY",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out error), "blocked metadata write unexpectedly succeeded");
            Equal("Group 1", blueprint.name, "failed metadata name rollback");
            Equal(CompositeBlueprintStore.DefaultCategory, blueprint.category,
                "failed metadata category rollback");
            Require(!store.Categories().Contains("NEW CATEGORY"),
                "failed metadata category remained in memory");
            Require(!store.TrySaveDocument(
                "Failed new blueprint",
                "FAILED CREATE CATEGORY",
                parts,
                Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(),
                out CompositeBlueprintStore.Blueprint failedBlueprint,
                out error), "blocked initial save unexpectedly succeeded");
            Equal<CompositeBlueprintStore.Blueprint>(
                null, failedBlueprint, "failed initial save result");
            Equal(1, store.All().Count, "failed initial save blueprint rollback");
            Require(!store.Categories().Contains("FAILED CREATE CATEGORY"),
                "failed initial save category remained in memory");
            Equal(original, File.ReadAllText(path), "original file after failed write");
        }

        private static CompositeBlueprintStore.Part Part(
            string id,
            string prefab,
            string name,
            string group,
            bool visible,
            bool locked,
            float x) => new CompositeBlueprintStore.Part
            {
                stableId = id,
                prefabName = prefab,
                displayName = name,
                parentGroupId = group,
                editorVisible = visible,
                editorLocked = locked,
                position = Vector(x, 0f, 0f),
                rotation = new CompositeBlueprintStore.QuaternionData
                {
                    x = 0f,
                    y = 0f,
                    z = 0f,
                    w = 1f
                }
            };

        private static string LegacyPart(float x) =>
            "{\"prefabName\":\"woodwall\",\"position\":{\"x\":" +
            x.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            ",\"y\":0,\"z\":0},\"rotation\":{\"x\":0,\"y\":0,\"z\":0,\"w\":1}}";

        private static CompositeBlueprintStore.VectorData Vector(float x, float y, float z) =>
            new CompositeBlueprintStore.VectorData { x = x, y = y, z = z };

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message ?? "operation failed");
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
                throw new InvalidOperationException(
                    name + ": expected " + expected + ", actual " + actual);
        }
    }
}
