using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ImportCatalogRecoveryChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string source = Path.Combine(folder, "input.blueprint");
            File.WriteAllText(source, "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
            byte[] original = File.ReadAllBytes(source);
            var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"), true);
            object host = new object();
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                () => host, ForeignZdoChecks.Hash, () => 0);
            Ready(store);
            var raw = CompositeBlueprintStore.ReviewImportFile(source, ForeignZdoChecks.Hash, 0);
            var review = store.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error);
            string operation = store.ImportOperationPath;
            Require(operation != null && operation != source && !store.RetryImportCatalog(operation, out _),
                "Operation identity or busy refresh guard failed");
            FieldInfo import = typeof(CompositeBlueprintStore).GetField("importTask", BindingFlags.Instance | BindingFlags.NonPublic);
            ((Task)import.GetValue(store)).GetAwaiter().GetResult();
            Require(store.ImportResultPath == operation && File.Exists(operation), "Commit receipt not published");
            store.CatalogAll();
            FieldInfo preparation = typeof(CompositeBlueprintStore).GetField("preparationTask", BindingFlags.Instance | BindingFlags.NonPublic);
            ((Task)preparation.GetValue(store)).GetAwaiter().GetResult();
            preparation.SetValue(store, Task.FromException<System.Collections.Generic.List<CompositeBlueprintStore.Blueprint>>(
                new IOException("fixture catalogue failure")));
            store.CatalogAll();
            Require(store.ImportError == "fixture catalogue failure" && !store.CatalogLoading &&
                store.ImportResultPath == operation && Directory.GetFiles(store.ImportDirectory, "*.zip").Length == 1,
                "Post-commit refresh failure lost receipt or wrote extra archives");
            Require(!store.RetryImportCatalog("wrong-operation", out _), "Wrong operation refreshed catalogue");
            Require(store.RetryImportCatalog(operation, out error), error);
            Ready(store);
            Require(store.ImportError == null && store.ImportProgress == CompositeBlueprintStore.ImportStage.Complete &&
                store.ImportOperationPath == operation && store.ImportResultPath == operation &&
                store.CatalogAll().Count == 1 && Directory.GetFiles(store.ImportDirectory, "*.zip").Length == 1 &&
                File.ReadAllBytes(source).SequenceEqual(original), "Read-only refresh duplicated/lost source/result");
            using (var zip = ZipFile.OpenRead(operation))
            using (var stream = zip.GetEntry("original/source.bin").Open())
            using (var bytes = new MemoryStream())
            { stream.CopyTo(bytes); Require(bytes.ToArray().SequenceEqual(original), "Archived source bytes changed"); }
            store.ConfigureImportScale(_ => throw new IOException("host snapshot failed"), () => host, ForeignZdoChecks.Hash, () => 0);
            bool failed = false;
            try { store.CatalogAll(); } catch (IOException) { failed = true; }
            Require(failed && store.ImportResultPath == operation && !store.CatalogLoading,
                "Host snapshot failure lost the committed operation");
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                () => host, ForeignZdoChecks.Hash, () => 0);
            Require(store.RetryImportCatalog(operation, out error), error);
            Ready(store);
            Require(store.CatalogAll().Count == 1 && Directory.GetFiles(store.ImportDirectory, "*.zip").Length == 1,
                "Host snapshot retry created another archive");
            Console.WriteLine("PASS actual import: unique operation/commit receipt, post-commit catalogue failure and read-only retry, one archive, exact original bytes, ready stage.");
        }

        private static void Ready(CompositeBlueprintStore store)
        {
            Require(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Catalogue timeout");
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
