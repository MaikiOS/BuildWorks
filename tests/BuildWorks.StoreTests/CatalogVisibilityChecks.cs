using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class CatalogVisibilityChecks
    {
        internal static void Run(string directory)
        {
            string imports = Path.Combine(directory, "imports"), path = Path.Combine(directory, "library.json");
            Directory.CreateDirectory(imports);
            string raw = "#Name:Table\n#Pieces\nwoodwall;Workbench;0;0;0;0;0;0;1;\"\";1;1;1\n";
            string sourcePath = Path.Combine(imports, "table.blueprint"), zipPath = Path.Combine(imports, "source.zip");
            File.WriteAllText(sourcePath, raw);
            using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("nested/table.blueprint").Open())) writer.Write(raw);
            byte[] zipBytes = File.ReadAllBytes(zipPath), sourceBytes = File.ReadAllBytes(sourcePath);
            var store = new CompositeBlueprintStore(path);
            var source = store.CatalogAll().Single(item => item.importSource == "table.blueprint");
            Require(!store.TryDelete(source, out _) && !File.Exists(path), "Source accepted owned deletion");
            Require(store.TryGetOrCreatePlacementCopy(source, out var owned, out string error), error);
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(owned));
            Require(store.TrySetHidden(source, true, out error), error);
            Require(store.CatalogAll().Count == 2 && store.CatalogAll(true).Count == 3 &&
                store.CatalogAll().Contains(owned) && store.IsHidden(source), "Hide removed owned copy or wrong ZIP entry");
            Require(ReferenceEquals(store.CatalogAll(), store.CatalogAll()) &&
                ReferenceEquals(store.CatalogAll(true), store.CatalogAll(true)), "Catalog allocates on every query");
            int revision = store.Revision;
            byte[] native = File.ReadAllBytes(path);
            Require(store.TrySetHidden(source, true, out error) && store.Revision == revision &&
                native.SequenceEqual(File.ReadAllBytes(path)), "Repeated hide rewrote the file");
            Require(!store.TrySetHidden(owned, true, out _) && !store.IsHidden(owned), "Owned copy accepted source hiding");
            var reloaded = new CompositeBlueprintStore(path);
            reloaded.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 7, y = 7, z = 7 });
            var changedSource = reloaded.CatalogAll(true).Single(item => item.importSource == source.importSource);
            Require(changedSource.id != source.id && reloaded.IsHidden(changedSource) &&
                reloaded.CatalogAll().All(item => item.importSource != source.importSource), "Host conversion identity reset visibility");
            Directory.CreateDirectory(path + ".tmp");
            Require(!reloaded.TrySetHidden(changedSource, false, out _) && reloaded.IsHidden(changedSource) &&
                reloaded.CatalogAll().Count == 2 && native.SequenceEqual(File.ReadAllBytes(path)), "Failed restore changed mask/cache/bytes");
            Directory.Delete(path + ".tmp");
            Require(reloaded.TrySetHidden(changedSource, false, out error) && reloaded.CatalogAll().Count == 3, error);
            Require(reloaded.TryDelete(reloaded.All().Single(), out error), error);
            var afterDelete = new CompositeBlueprintStore(path);
            Require(afterDelete.All().Count == 0 && afterDelete.CatalogAll().Count == 2 &&
                !afterDelete.IsHidden(afterDelete.CatalogAll().First()), "Owned deletion affected source visibility");
            Require(portable == Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(owned)) && !portable.Contains("hiddenSources"),
                "Local visibility leaked into portable blueprint");
            Require(zipBytes.SequenceEqual(File.ReadAllBytes(zipPath)) && sourceBytes.SequenceEqual(File.ReadAllBytes(sourcePath)),
                "Source file or ZIP was modified");
            string json = File.ReadAllText(path);
            string legacy = json.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":24", "\"version\":20");
            string oldPath = Path.Combine(directory, "legacy.json");
            File.WriteAllText(oldPath, legacy);
            var old = new CompositeBlueprintStore(oldPath);
            Require(old.TryAddCategory("Legacy", out error) && File.ReadAllText(oldPath).Contains("\"version\":24"), error);
            foreach (string invalid in new[] { "[\"same\",\"same\"]", "[\"\"]", "[null]", "[\" \" ]",
                "[" + string.Join(",", Enumerable.Range(0, ExternalBlueprintLibrary.MaximumFiles + 1).Select(i => "\"s" + i + "\"")) + "]" })
                Refuse(oldPath, json.Replace("\"hiddenSources\":[]", "\"hiddenSources\":" + invalid));
            Refuse(oldPath, json.Replace("\"version\":24", "\"version\":20"));
            Refuse(oldPath, json.Replace("\"version\":24", "\"version\":25"));
            Console.WriteLine("PASS catalogue visibility: source/ZIP immutable, copy independent, host identity/reload/cache, restore/write rollback, owned delete, native21 migration/guards, portable10 unchanged");
        }

        private static void Refuse(string path, string json)
        {
            File.WriteAllText(path, json);
            Require(!new CompositeBlueprintStore(path).TryAddCategory("Refused", out _) && File.ReadAllText(path) == json,
                "Invalid visibility schema was accepted or overwritten");
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
