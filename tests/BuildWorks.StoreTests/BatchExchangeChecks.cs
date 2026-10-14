using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class BatchExchangeChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string a = Path.Combine(folder, "a.blueprint"), b = Path.Combine(folder, "b.blueprint"), bad = Path.Combine(folder, "bad.blueprint");
            File.WriteAllText(a, "#Name:Same name\n#Category:FIRST\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
            File.WriteAllText(b, File.ReadAllText(a).Replace("FIRST", "SECOND"));
            File.WriteAllText(bad, "not a supported blueprint");
            var raw = CompositeBlueprintStore.ReviewImportFiles(new[] { a, b, a, bad }, ForeignZdoChecks.Hash, 0);
            Require(raw.Documents.Count == 2 && raw.Sources.Count == 2 && raw.FileErrors.Count == 1, "Batch file review/duplicate/error accounting failed");
            var bytes = new[] { File.ReadAllBytes(a), File.ReadAllBytes(b) };
            var store = NewStore(Path.Combine(folder, "library"));
            var review = store.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            var categories = new Dictionary<int, string> { [1] = "override" };
            var committedStop = new CancellationTokenSource();
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error,
                importCategory: "common", documentCategories: categories, cancellation: committedStop.Token), error);
            Ready(store);
            committedStop.Cancel();
            var imported = store.CatalogAll().ToArray();
            Require(imported.Length == 2 && imported.Select(item => item.category).OrderBy(value => value).SequenceEqual(new[] { "COMMON", "OVERRIDE" }) &&
                File.ReadAllBytes(a).SequenceEqual(bytes[0]) && File.ReadAllBytes(b).SequenceEqual(bytes[1]) &&
                raw.Documents.Select(item => item.category).SequenceEqual(new[] { "FIRST", "SECOND" }), "Batch categories/source/review changed");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            {
                Require(zip.GetEntry("original/manifest.tsv") != null && zip.GetEntry("document_0.buildworks.json") != null, "Batch manifest missing");
                for (int i = 0; i < 2; ++i)
                    using (var entry = zip.GetEntry("original/source_" + i + ".bin").Open())
                    using (var output = new MemoryStream())
                    { entry.CopyTo(output); Require(output.ToArray().SequenceEqual(bytes[i]), "Batch original bytes changed"); }
            }
            Require(NewStore(Path.Combine(folder, "library")).CatalogAll().Count == 2, "Batch import lost on reload");
            var untouched = NewStore(Path.Combine(folder, "cancelled"));
            var cancelledReview = untouched.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            using (var stop = new CancellationTokenSource())
            {
                stop.Cancel();
                Require(!untouched.TryImportReviewed(cancelledReview, false, false, ForeignZdoChecks.Hash, out error, cancellation: stop.Token) &&
                    !Directory.Exists(untouched.ImportDirectory), "Cancelled batch wrote files");
            }
            var stale = NewStore(Path.Combine(folder, "stale"));
            var staleReview = stale.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            File.AppendAllText(b, "\n#Changed");
            Require(stale.TryImportReviewed(staleReview, false, false, ForeignZdoChecks.Hash, out error), error);
            Ready(stale, expectFailure: true);
            Require(!Directory.Exists(stale.ImportDirectory) || Directory.GetFiles(stale.ImportDirectory).Length == 0, "Changed batch source partially committed");
            File.WriteAllBytes(b, bytes[1]);

            string target = store.ExportBatch(imported, folder, "full-batch", false, false, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            var exported = ExternalBlueprintLibrary.LoadFile(target);
            Require(exported.Count == 2 && exported.All(item => item.importError == null) &&
                exported.Select(item => item.category).OrderBy(value => value).SequenceEqual(new[] { "COMMON", "OVERRIDE" }), "Batch ZIP incompatible with our reader");
            using (var zip = ZipFile.OpenRead(target)) Require(zip.GetEntry("manifest.tsv") != null, "Export manifest missing");
            byte[] complete = File.ReadAllBytes(target);
            bool refused = false;
            try { store.ExportBatch(imported, folder, "full-batch", false, false, ForeignZdoChecks.Hash).GetAwaiter().GetResult(); }
            catch (IOException) { refused = true; }
            Require(refused && File.ReadAllBytes(target).SequenceEqual(complete), "Batch export overwrote existing file");
            Require(Directory.GetFiles(folder, "*.tmp.zip").Length == 0 && store.All().Count == 0 && store.CatalogAll().Count == 2,
                "Export leaked temp files or inserted native cards");
            string zipA = Path.Combine(folder, "compressed-a.zip"), zipB = Path.Combine(folder, "compressed-b.zip");
            foreach (string zipPath in new[] { zipA, zipB })
                using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("same.blueprint").Open())) writer.Write(File.ReadAllText(a));
            long oneText = new FileInfo(a).Length;
            var bounded = CompositeBlueprintStore.ReviewImportFiles(new[] { zipA, zipB }, ForeignZdoChecks.Hash, 0, uncompressedBudget: oneText + 5);
            Require(bounded.Documents.Count == 1 && bounded.FileErrors.Count == 1,
                "Each ZIP reset the shared uncompressed budget");
            var exact = CompositeBlueprintStore.ReviewImportFiles(new[] { zipA, zipB }, ForeignZdoChecks.Hash, 0, uncompressedBudget: oneText * 2);
            Require(exact.Documents.Count == 2 && exact.FileErrors.Count == 0, "Exact shared read budget refused");
            string brokenZip = Path.Combine(folder, "mixed-broken.zip");
            using (var zip = ZipFile.Open(brokenZip, ZipArchiveMode.Create))
            {
                using (var text = new StreamWriter(zip.CreateEntry("good.blueprint").Open())) text.Write(File.ReadAllText(a));
                using (var text = new StreamWriter(zip.CreateEntry("bad.blueprint").Open())) text.Write("broken source");
            }
            var mixed = CompositeBlueprintStore.ReviewImportFiles(new[] { brokenZip, a }, ForeignZdoChecks.Hash, 0);
            Require(mixed.Documents.Count == 1 && mixed.Sources.Count == 1 && mixed.FileErrors.Count == 1 &&
                mixed.Sources[0].Path == a, "Broken ZIP was partly accepted or blocked its valid neighboring file");

            string partialSource = Path.Combine(folder, "partial.blueprint");
            File.WriteAllText(partialSource, File.ReadAllText(a) + "\nAbsent_Mod_Piece;;2;0;0;0;0;0;1;\"\";1;1;1");
            var partial = NewStore(Path.Combine(folder, "partial"));
            partial.ConfigureImportScale(name => name == "Absent_Mod_Piece" ? (CompositeBlueprintStore.VectorData?)null :
                new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            var partialReview = partial.PrepareImportReview(CompositeBlueprintStore.ReviewImportFiles(new[] { partialSource }, ForeignZdoChecks.Hash, 0),
                ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(partial.TryImportReviewed(partialReview, false, false, ForeignZdoChecks.Hash, out error,
                importCategory: "common", documentCategories: new Dictionary<int, string> { [0] = "MISSING OVERRIDE" }), error);
            Ready(partial);
            Require(partial.CatalogAll().Single().category == "MISSING OVERRIDE", "Prepared transient ID changed per-document category");

            string committedPath = store.ImportResultPath;
            Require(File.Exists(committedPath) && ExternalBlueprintLibrary.LoadFile(committedPath).Count == 2,
                "Completed import was not fully published");
            bool exportCancelled = false;
            try { store.ExportBatch(imported, folder, "cancel-export", false, false, ForeignZdoChecks.Hash, cancellation: committedStop.Token).GetAwaiter().GetResult(); }
            catch (OperationCanceledException) { exportCancelled = true; }
            committedStop.Dispose();
            Require(exportCancelled && !File.Exists(Path.Combine(folder, "cancel-export.zip")) && File.Exists(committedPath),
                "Cancelled export changed published import or leaked result");
            WritingCancellation(folder, imported[0]);
            ContentsPolicies(folder, imported[0]);
            Console.WriteLine("PASS batch exchange: multi-file/malformed/duplicates, common and per-document categories, exact originals+manifest, reload, source-change atomic refusal, pre-cancel, full ZIP roundtrip and no overwrite/card/temp leak.");
        }

        private static void WritingCancellation(string folder, CompositeBlueprintStore.Blueprint source)
        {
            var document = ExternalBlueprintLibrary.DetachedCopy(source);
            document.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeItemStandFormat,
                info = "SwordIron:0:1:0", zdo = StandPackage() };
            string file = Path.Combine(folder, "cancel-writing.buildworks.json");
            File.WriteAllBytes(file, BlueprintExchangeFormat.Encode(document));
            var store = NewStore(Path.Combine(folder, "cancel-writing"));
            var review = store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFiles(new[] { file }, ForeignZdoChecks.Hash, 0),
                ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            using (var stop = new CancellationTokenSource())
            {
                bool staged = false;
                int HashWhileWriting(string name)
                {
                    if (Directory.Exists(store.ImportDirectory) && Directory.GetFiles(store.ImportDirectory, "*.tmp.zip").Length > 0)
                    { staged = true; stop.Cancel(); }
                    return ForeignZdoChecks.Hash(name);
                }
                Require(store.TryImportReviewed(review, false, true, HashWhileWriting, out string error, cancellation: stop.Token), error);
                Ready(store, expectFailure: true);
                Require(staged && store.ImportResultPath == null && store.CatalogAll().Count == 0 &&
                    Directory.GetFiles(store.ImportDirectory).Length == 0, "Import cancellation after writing leaked temporary/published data");
            }
            int owner = Environment.CurrentManagedThreadId, metadataCalls = 0;
            store.ConfigurePrefabMetadata(_ =>
            {
                Require(Environment.CurrentManagedThreadId == owner, "Batch export invoked live metadata on worker");
                ++metadataCalls; return null;
            });
            string exportFolder = Path.Combine(folder, "cancel-export-writing"); Directory.CreateDirectory(exportFolder);
            var many = Enumerable.Range(0, 32).Select(index =>
            {
                var copy = ExternalBlueprintLibrary.DetachedCopy(source); copy.id = Guid.NewGuid().ToString("N");
                copy.parts = Enumerable.Range(0, 1000).Select(i =>
                { var part = CompositeBlueprintStore.CloneParts(new[] { copy.parts[0] })[0]; part.stableId = "part-" + i; return part; }).ToList();
                return copy;
            }).ToArray();
            using (var stop = new CancellationTokenSource())
            using (var staged = new ManualResetEventSlim())
            using (var watcher = new FileSystemWatcher(exportFolder, "*.tmp.zip"))
            {
                watcher.Created += (_, __) => { stop.Cancel(); staged.Set(); };
                watcher.EnableRaisingEvents = true;
                bool cancelled = false;
                try { store.ExportBatch(many, exportFolder, "cancelled", false, false, ForeignZdoChecks.Hash,
                    cancellation: stop.Token).GetAwaiter().GetResult(); }
                catch (OperationCanceledException) { cancelled = true; }
                Require(staged.Wait(1000) && cancelled && Directory.GetFiles(exportFolder).Length == 0 && metadataCalls == 1,
                    "Export cancellation after staging leaked temporary/published data or metadata was not snapshotted");
            }
            Console.WriteLine("PASS batch cancellation during import/export staging, no target/temp/card leak; export metadata host-thread snapshot");
        }

        private static string StandPackage()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write((byte)109); writer.Write(12345);
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
                writer.Write((byte)64); writer.Write(ForeignZdoChecks.Hash("SwordIron")); writer.Write((byte)0);
                writer.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void ContentsPolicies(string folder, CompositeBlueprintStore.Blueprint source)
        {
            var document = ExternalBlueprintLibrary.DetachedCopy(source);
            document.parts = CompositeBlueprintStore.CloneParts(new[] { document.parts[0], document.parts[0] });
            document.parts[0].stableId = "chest"; document.parts[1].stableId = "stand";
            document.parts[0].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeContainerFormat, info = "5:2:0:0", zdo = "bQAAAAAA" };
            document.parts[1].foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:0:1:0", zdo = StandPackage() };
            string input = Path.Combine(folder, "contents-policies.buildworks.json");
            byte[] original = BlueprintExchangeFormat.Encode(document); File.WriteAllBytes(input, original);
            foreach (bool storage in new[] { false, true })
            foreach (bool mounts in new[] { false, true })
            {
                string label = "policy-" + storage + "-" + mounts;
                var store = NewStore(Path.Combine(folder, label));
                var review = store.PrepareImportReview(CompositeBlueprintStore.ReviewImportFiles(new[] { input }, ForeignZdoChecks.Hash, 0),
                    ForeignZdoChecks.Hash).GetAwaiter().GetResult();
                Require(store.TryImportReviewed(review, storage, mounts, ForeignZdoChecks.Hash, out string error), error); Ready(store);
                var imported = store.CatalogAll().Single();
                Require(imported.parts[0].foreignData.contentsCleared == storage && imported.parts[1].foreignData.contentsCleared == mounts,
                    "Batch import conflated storage and mounts");
                string output = store.ExportBatch(new[] { document }, folder, label, storage, mounts, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
                var exported = ExternalBlueprintLibrary.LoadFile(output).Single();
                Require(exported.parts[0].foreignData.contentsCleared == storage && exported.parts[1].foreignData.contentsCleared == mounts &&
                    File.ReadAllBytes(input).SequenceEqual(original) && !document.parts[0].foreignData.contentsCleared && !document.parts[1].foreignData.contentsCleared,
                    "Batch export conflated policies or changed source");
            }
            Console.WriteLine("PASS batch storage/mount import/export 4 policy combinations and immutable originals");
        }

        private static CompositeBlueprintStore NewStore(string folder)
        {
            var result = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            result.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            return result;
        }
        private static void Ready(CompositeBlueprintStore store, bool expectFailure = false)
        {
            for (int i = 0; i < 1000; ++i)
            {
                store.CatalogAll();
                if (!store.CatalogLoading) { Require(expectFailure ? store.ImportError != null : store.ImportError == null, store.ImportError ?? "Expected failure"); return; }
                Thread.Sleep(5);
            }
            throw new Exception("Batch import timed out");
        }
        private static void Require(bool condition, string error) { if (!condition) throw new Exception(error); }
    }
}
