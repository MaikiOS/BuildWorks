using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class LegacyStorageChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string package = Package(), original = Source(package);
            Require(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "planbuild", "29-planbuild-storage106.blueprint")) == original,
                "Owned manual Inventory106 fixture differs from the checked source");
            int Hash(string name) => name == "AxeIron" ? -101 : 100;
            var parsed = ExternalBlueprintLibrary.Parse(original, "legacy-storage.blueprint");
            CompositeBlueprintStore.VectorData? Scale(string _) => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
            (int Width, int Height)? Grid(string name) => name == "piece_chest_wood" ? (5, 2) : ((int, int)?)null;
            var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, Hash, containerGrid: Grid);
            Require(parsed.parts[1].foreignData.format == "blueprint" && prepared.parts[1].foreignData.format == BlueprintForeignPartData.NativeContainerFormat,
                "Host-bound storage conversion missing or mutated caller");
            Check(prepared, original, package, Hash);
            var summary = CompositeBlueprintStore.InspectContents(prepared, Hash);
            Require(summary.Unknown == 0 && summary.Storages == 1 && summary.StoredItems == 1,
                "Known storage was reported unknown or empty");
            var unit = ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, Hash, containerGrid: _ => (1, 1));
            Require(unit.parts[1].foreignData.format == "blueprint", "Out-of-grid inventory was silently accepted");
            foreach (string token in new[] { package, "\"" + package + "\"" })
                Check(ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(Source(token, raw: true), "raw.blueprint"),
                    Scale, Hash, containerGrid: Grid), Source(token, raw: true), package, Hash);
            foreach (var unsupported in new[] {
                ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, Hash),
                ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, Hash, containerGrid: _ => null),
                ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, _ => 0, containerGrid: Grid) })
                Require(unsupported.parts[1].foreignData.format == "blueprint", "Unverified host/hash was guessed");
            foreach (Action<CompositeBlueprintStore.Part> tamper in new Action<CompositeBlueprintStore.Part>[] {
                part => part.foreignData.info = "\"AAAA\"", part => part.foreignData.zdo = "AAAA",
                part => part.foreignData.chance = "0.5", part => part.sourceLine = 3,
                part => part.prefabName = "other_chest" })
            {
                var changed = parsed.ImportCopy(); tamper(changed.parts[1]);
                Require(ExternalBlueprintLibrary.PrepareForHost(changed, Scale, Hash, containerGrid: _ => (5, 2))
                    .parts[1].foreignData.format == "blueprint", "Storage recipe escaped source-row binding");
            }
            var malformed = ExternalBlueprintLibrary.Parse(Source("\"" + package + "\" true", raw: true), "trailing.blueprint");
            Require(ExternalBlueprintLibrary.PrepareForHost(malformed, Scale, Hash, containerGrid: Grid).parts[1].foreignData.format == "blueprint",
                "JSON trailing data accepted");
            var largerGrid = ExternalBlueprintLibrary.PrepareForHost(parsed, Scale, Hash, containerGrid: _ => (6, 2));
            Require(prepared.id != largerGrid.id, "Different host inventory manifests share catalog identity");
            var clear = CompositeBlueprintStore.CopyWithContentsPolicy(prepared, true, false, Hash);
            Require(clear.parts[1].foreignData.contentsCleared && clear.parts[1].foreignData.zdo == package &&
                !prepared.parts[1].foreignData.contentsCleared && CompositeBlueprintStore.InspectContents(clear, Hash).StoredItems == 0,
                "Clear-storage policy destroyed archive or mutated original");
            var mountOnly = CompositeBlueprintStore.CopyWithContentsPolicy(prepared, false, true, Hash);
            Require(!mountOnly.parts[1].foreignData.contentsCleared && mountOnly.parts[1].foreignData.zdo == package,
                "Mount-only choice cleared storage");
            var copy = ExternalBlueprintLibrary.DetachedCopy(prepared);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "native.json"));
            Require(store.TrySaveDocument(copy.name, copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId, copy.primaryGroupId, copy.supports, copy.placementSupportId, out var saved, out string error, sourceDocuments: copy.sourceDocuments), error);
            Check(new CompositeBlueprintStore(Path.Combine(directory, "native.json")).All().Single(), original, package, Hash);
            Check(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved))), original, package, Hash);
            Require(store.TryExportFile(saved, directory, "clear", false, out string exported, out error, clearStorage: true, prefabHash: Hash), error);
            var cleared = ExternalBlueprintLibrary.LoadFile(exported).Single();
            Require(cleared.parts[1].foreignData.contentsCleared && cleared.parts[1].foreignData.zdo == package &&
                !saved.parts[1].foreignData.contentsCleared, "Export policy lost v106 evidence");
            WorkerPaths(Path.Combine(directory, "worker"), original, package, Hash);
            Console.WriteLine("PASS source-bound Inventory106 host grid, exact float/bytes, disk/portable/export/clear, catalog/import snapshots and stale context; no inventory restoration");
        }

        private static void WorkerPaths(string directory, string source, string package, Func<string, int> hash)
        {
            string imports = Path.Combine(directory, "imports"); Directory.CreateDirectory(imports);
            string input = Path.Combine(directory, "source.blueprint"); File.WriteAllText(input, source);
            File.WriteAllText(Path.Combine(imports, "legacy.blueprint"), source);
            object context = new object(); int owner = Thread.CurrentThread.ManagedThreadId, gridReads = 0;
            var store = new CompositeBlueprintStore(Path.Combine(directory, "native.json"), true);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                () => context, hash, () => 0, containerGrid: name =>
                {
                    Require(Thread.CurrentThread.ManagedThreadId == owner, "Worker read Unity container grid");
                    ++gridReads; return name == "piece_chest_wood" ? (5, 2) : ((int, int)?)null;
                });
            Wait(store);
            var catalog = store.CatalogAll().Single(); Check(catalog, source, package, hash);
            Require(gridReads == 2, "Grid snapshot did not cache distinct prefabs");
            var rawReview = CompositeBlueprintStore.ReviewImportFile(input, hash);
            Require(rawReview.Contents.Unknown > 0, "File worker guessed container without host");
            var review = store.PrepareImportReview(rawReview, hash).GetAwaiter().GetResult();
            Check(review.Documents.Single(), source, package, hash);
            Require(review.Contents.Unknown == 0 && review.Contents.StoredItems == 1, "Host refinement did not update contents choices");
            context = new object();
            Require(!store.TryImportReviewed(review, true, false, hash, out _), "Stale host review imported");
            review = store.PrepareImportReview(rawReview, hash).GetAwaiter().GetResult();
            Require(store.TryImportReviewed(review, true, false, hash, out string error), error);
            Wait(store); Require(store.ImportError == null, store.ImportError);
            var imported = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
            Require(imported.parts[1].foreignData.contentsCleared && imported.parts[1].foreignData.zdo == package &&
                File.ReadAllText(input) == source, "Reviewed import lost bytes or changed input");
            context = null; Wait(store);
            Require(store.CatalogAll().Any(document => document.parts[1].foreignData.format == "blueprint"),
                "Absent host reused old container classification");
        }

        private static void Wait(CompositeBlueprintStore store) => Require(SpinWait.SpinUntil(() =>
        { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Storage worker timeout");

        private static void Check(CompositeBlueprintStore.Blueprint document, string source, string package, Func<string, int> hash)
        {
            var state = document.parts[1].foreignData;
            Require(state.zdo == package && document.sourceDocuments.Single().text == source &&
                BlueprintContainerInventory.TryRead(state.ToGeometry(), hash, out var items) && items.Count == 1 &&
                items[0].LegacyDurability == 20.125f && items[0].SourceVersion == 106 && !items[0].DurabilityHundredths.HasValue &&
                items[0].CustomData["mod:exact"] == "source\0🪵", "v106 attributes/source/bytes changed");
        }

        private static string Source(string package, bool raw = false) =>
            "#Name:Legacy storage\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1\n" +
            "piece_chest_wood;;1;0;0;0;0;0;1;" + (raw ? package : "\"" + package + "\"") + ";1;1;1\n";

        private static string Package()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(106); writer.Write(1); writer.Write("AxeIron"); writer.Write(1); writer.Write(20.125f);
                writer.Write(4); writer.Write(1); writer.Write(false); writer.Write(2); writer.Write(0);
                writer.Write(-123L); writer.Write("Острикс"); writer.Write(1); writer.Write("mod:exact"); writer.Write("source\0🪵");
                writer.Write(0); writer.Write(true); writer.Flush(); return Convert.ToBase64String(stream.ToArray());
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
