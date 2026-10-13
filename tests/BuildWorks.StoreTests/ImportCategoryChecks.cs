using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ImportCategoryChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string source = Path.Combine(folder, "two-categories.zip");
            using (var zip = ZipFile.Open(source, ZipArchiveMode.Create))
                foreach (string category in new[] { "SOURCE A", "SOURCE B" })
                    using (var text = new StreamWriter(zip.CreateEntry(category + ".blueprint").Open()))
                        text.Write("#Name:" + category + "\n#Category:" + category +
                            "\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
            byte[] original = File.ReadAllBytes(source);
            var raw = CompositeBlueprintStore.ReviewImportFile(source, ForeignZdoChecks.Hash, 0);
            Require(raw.Documents.Count == 2 && raw.Documents.Select(item => item.category).Distinct().Count() == 2,
                "Different source categories not represented by fixture");
            string[] originalCategories = raw.Documents.Select(item => item.category).ToArray();
            foreach (string value in new[] { "", "   ", "<tag>", "bad\ncategory", new string('x', 25) })
            {
                var refused = NewStore(Path.Combine(folder, "invalid-" + Guid.NewGuid().ToString("N")));
                var review = refused.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
                Require(!refused.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error,
                    importCategory: value) && error == "$buildworks_exchange.invalid_category" &&
                    !refused.CatalogLoading && refused.ImportOperationPath == null &&
                    !Directory.Exists(refused.ImportDirectory) && refused.CatalogAll().Count == 0,
                    "Invalid category wrote data or started import: " + value);
            }
            int caseIndex = 0;
            foreach (string category in new[] { null, " existing ", "Новая категория", "ПРОЧЕЕ" })
            {
                string path = Path.Combine(folder, "case-" + caseIndex++);
                var store = NewStore(path);
                var review = store.PrepareImportReview(raw, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
                Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error,
                    importCategory: category), error);
                Ready(store);
                var documents = store.CatalogAll().ToArray();
                Require(documents.Length == 2 && store.Categories().Count == 1,
                    "Category review mutated native category registry or lost documents");
                CompositeBlueprintStore.TryNormalizeImportCategory(category, out string normalized);
                Require(documents.All(item => item.category == (normalized ?? originalCategories.Single(sourceCategory => sourceCategory == item.name))),
                    "Common or original category not applied to each saved document");
                Require(review.Documents.Select(item => item.category).SequenceEqual(originalCategories) &&
                    raw.Documents.Select(item => item.category).SequenceEqual(originalCategories) &&
                    review.Bytes.SequenceEqual(original) && File.ReadAllBytes(source).SequenceEqual(original),
                    "Import category changed review or source bytes");
                using (var zip = ZipFile.OpenRead(store.ImportResultPath))
                using (var input = zip.GetEntry("original/source.bin").Open())
                using (var bytes = new MemoryStream())
                { input.CopyTo(bytes); Require(bytes.ToArray().SequenceEqual(original), "Archived source changed"); }
                var reopened = NewStore(path);
                Require(reopened.CatalogAll().Select(item => item.category).SequenceEqual(documents.Select(item => item.category)),
                    "Category lost on a new store/reload");
            }
            Console.WriteLine("PASS import category: original multi-document categories, normalized common category, new/legacy values, no native-category mutation, source/review bytes and reload; invalid input refused before writes.");
        }

        private static CompositeBlueprintStore NewStore(string folder)
        {
            var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            return store;
        }

        private static void Ready(CompositeBlueprintStore store)
        {
            for (int i = 0; i < 1000; ++i)
            {
                store.CatalogAll();
                if (!store.CatalogLoading) { Require(store.ImportError == null, store.ImportError); return; }
                Thread.Sleep(5);
            }
            throw new InvalidOperationException("Category import timed out");
        }

        private static void Require(bool condition, string error)
        { if (!condition) throw new InvalidOperationException(error); }
    }
}
