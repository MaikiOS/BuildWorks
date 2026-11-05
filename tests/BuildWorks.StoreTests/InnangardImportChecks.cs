using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class InnangardImportChecks
    {
        // The author sample stays outside distributed fixtures; pass a private, pinned local file.
        internal static void Run(string file, string directory)
        {
            byte[] original = File.ReadAllBytes(file);
            Require(Hash(original) == "497CF83820530AD75F0C7990BB90876D8DE3C09E4BC07836DFD0092453167960",
                "Expected the original Innangard 0.3.7 bundled cabin");
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(source.importError == null && source.parts.Count == 56 && source.name == "3 bed cabin" &&
                source.category == "Innangard" && source.importAbsoluteScale && source.groups.Count == 0,
                "Innangard cabin failed existing blueprint reader: " + source.importError);
            CheckRows(source, original);
            Require(source.parts.Select(p => p.prefabName).Distinct().Count() == 12 &&
                source.parts.Count(p => p.prefabName == "bed") == 3 &&
                source.parts.Count(p => p.prefabName == "piece_chest_wood") == 2, "Actual cabin composition changed");
            StoreCycle(file, directory, source, original);
            Console.WriteLine("PASS Innangard0.3.7 cabin:56parts/12prefabs;all13fields/source rows;review/import/archive;" +
                "native35/portable23/full-field reopen/snapshot/paired export;originalSHA unchanged. " +
                "Unit prefab bases only;no actual prefab/editor/placement/reload/network proof.");
        }

        internal static void RunThreePartFile(string file, string expectedSha256, string directory)
        {
            using (var pinned = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(pinned))
            {
                Require(pinned.Length > 0 && pinned.Length <= 65536, "Three-part file size exceeds the writer bound");
                byte[] original = reader.ReadBytes((int)pinned.Length);
                Require(original.Length == pinned.Length && string.Equals(Hash(original), expectedSha256, StringComparison.OrdinalIgnoreCase),
                    "Expected pinned three-part file SHA256");
                var source = ExternalBlueprintLibrary.LoadFile(file).Single();
                Require(source.importError == null && source.parts.Count == 3 && source.category == "Innangard" &&
                    source.importAbsoluteScale && source.groups.Count == 0 &&
                    source.parts.Count(p => p.prefabName == "wood_floor") == 2 && source.parts.Count(p => p.prefabName == "woodwall") == 1,
                    "Expected the two-floor/one-wall source layout");
                CheckRows(source, original);
                StoreCycle(file, directory, source, original);
                Console.WriteLine("PASS pinned Innangard-family three-part file:13fields/source addresses;review/import/archive;" +
                    "native35/portable23/full-field reopen/snapshot/paired export. Unit bases only;" +
                    "this command does not establish author/native-capture provenance or game/editor/placement/network proof.");
            }
        }

        private static void CheckRows(CompositeBlueprintStore.Blueprint source, byte[] original)
        {
            var evidence = source.sourceDocuments.Single().ToGeometry();
            Require(evidence.Format == "blueprint" && !evidence.HasOpaqueSections &&
                evidence.OriginalBytes().SequenceEqual(original), "Source format/headers/bytes changed");
            string[] lines = Encoding.UTF8.GetString(original).Split('\n');
            int partIndex = 0;
            foreach (var row in lines.Select((text, index) => (text: text.TrimEnd('\r'), line: index + 1))
                .Where(row => row.text.Length > 0 && row.text[0] != '#'))
            {
                string[] fields = row.text.Split(';');
                Require(fields.Length == 13 && fields[9] == "", "Unexpected actual writer field layout");
                var part = source.parts[partIndex++];
                Require(part.prefabName == fields[0] && part.sourceLine == row.line &&
                    evidence.HasPartAt(row.line, fields[0]) && part.foreignData == null && part.signText == null,
                    "Part order/source address or absent object state changed");
                float Number(int index) => float.Parse(fields[index], CultureInfo.InvariantCulture);
                Require(part.position.x == Number(2) && part.position.y == Number(3) && part.position.z == Number(4) &&
                    part.scale.x == Number(10) && part.scale.y == Number(11) && part.scale.z == Number(12),
                    "Actual position/absolute scale differs");
                double norm = Math.Sqrt(Enumerable.Range(5, 4).Sum(i => (double)Number(i) * Number(i)));
                Require(Math.Abs(part.rotation.x - Number(5) / norm) < 1e-6 &&
                    Math.Abs(part.rotation.y - Number(6) / norm) < 1e-6 &&
                    Math.Abs(part.rotation.z - Number(7) / norm) < 1e-6 &&
                    Math.Abs(part.rotation.w - Number(8) / norm) < 1e-6, "Actual normalized orientation differs");
            }
            Require(partIndex == source.parts.Count, "Source row count changed");
        }

        private static void StoreCycle(string file, string directory, CompositeBlueprintStore.Blueprint source, byte[] original)
        {
            CompositeBlueprintStore.VectorData? Unit(string _) =>
                new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source, Unit, ForeignZdoChecks.Hash, 0);
            Require(prepared.importError == null && !prepared.importAbsoluteScale &&
                prepared.parts.All(p => !p.scaleIsAbsolute && p.foreignData == null), "Unit-base preparation failed");

            Directory.CreateDirectory(directory);
            string library = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(library);
            store.ConfigureImportScale(Unit, prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            var review = CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash, 0);
            review = store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(!Directory.Exists(store.ImportDirectory) && review.Documents.Single().parts.Count == source.parts.Count,
                "Review changed the library or lost parts");
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error);
            var task = (Task)typeof(CompositeBlueprintStore).GetField("importTask",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
            Require(task != null && task.Wait(60000), "Import worker timeout");
            for (int i = 0; i < 1000; ++i)
            { store.CatalogAll(); if (!store.CatalogLoading) break; System.Threading.Thread.Sleep(10); }
            Require(!store.CatalogLoading && store.ImportError == null, "Import catalog failed");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var copy = new MemoryStream())
            {
                using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(copy);
                Require(copy.ToArray().SequenceEqual(original), "Reviewed original archive changed");
            }
            var imported = store.CatalogAll().Single();
            Require(imported.parts.Count == source.parts.Count && imported.sourceDocuments.Single().ToGeometry().OriginalBytes()
                .SequenceEqual(original), "Catalog/source data changed");
            RequireWorkingModel(prepared, imported);
            var position = imported.parts[0].position;
            try
            {
                imported.parts[0].position = new CompositeBlueprintStore.VectorData
                    { x = position.x + 1, y = position.y, z = position.z };
                Require(imported.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original),
                    "Geometry negative control changed archived bytes");
                bool rejected = false;
                try { RequireWorkingModel(prepared, imported); }
                catch (InvalidOperationException ex) when (ex.Message == "Working part fields changed from prepared source")
                { rejected = true; }
                Require(rejected, "Geometry negative control was not rejected");
            }
            finally { imported.parts[0].position = position; }
            RequireWorkingModel(prepared, imported);
            Require(store.TryGetOrCreatePlacementCopy(imported, out var saved, out error), error);
            RequireWorkingModel(prepared, saved);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            var decoded = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(portable), out int version);
            var reopened = new CompositeBlueprintStore(library).All().Single();
            var reimported = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "cabin.buildworks.json");
            reimported.id = saved.id;
            Require(version == BlueprintExchangeFormat.Version && File.ReadAllText(library).Contains("\"version\":" + CompositeBlueprintStore.FormatVersion) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(reopened)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(reimported)) &&
                BlueprintExportPair.Snapshot(saved).Document.sourceDocuments.Single().ToGeometry().OriginalBytes()
                    .SequenceEqual(original), "Native35/portable23/snapshot/reimport changed fields or source");
            var export = PlanBuildExport.Review(saved, Unit);
            Require(export.Parts == source.parts.Count && export.OmittedState.Count == 0 && export.OmittedSigns.Count == 0,
                "Paired export invented/omitted object state");
            Require(export.TryWrite(saved, Unit, directory, "paired", out string result, out error), error);
            var foreign = ExternalBlueprintLibrary.LoadFile(Path.Combine(result, "paired.blueprint")).Single();
            var backup = ExternalBlueprintLibrary.LoadFile(Path.Combine(result, "paired.buildworks.json")).Single();
            Require(foreign.importError == null && foreign.parts.Count == source.parts.Count &&
                foreign.parts.Select(p => p.prefabName).SequenceEqual(saved.parts.Select(p => p.prefabName)) &&
                backup.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(original) &&
                File.ReadAllBytes(file).SequenceEqual(original), "Paired reopen/original preservation failed");
            for (int i = 0; i < saved.parts.Count; ++i)
            {
                var a = saved.parts[i]; var b = foreign.parts[i];
                Require(a.position.x == b.position.x && a.position.y == b.position.y && a.position.z == b.position.z &&
                    a.scale.x == b.scale.x && a.scale.y == b.scale.y && a.scale.z == b.scale.z &&
                    Math.Abs(a.rotation.x - b.rotation.x) < 1e-6 && Math.Abs(a.rotation.y - b.rotation.y) < 1e-6 &&
                    Math.Abs(a.rotation.z - b.rotation.z) < 1e-6 && Math.Abs(a.rotation.w - b.rotation.w) < 1e-6,
                    "Paired blueprint geometry changed at part " + i);
            }
            Console.WriteLine("PASS prepared-to-imported/placement persisted fields; changed geometry with unchanged source rejected.");
        }

        private static void RequireWorkingModel(CompositeBlueprintStore.Blueprint expected, CompositeBlueprintStore.Blueprint actual)
        {
            Require(actual.parts.Count == expected.parts.Count && actual.groups.Count == 0 && expected.groups.Count == 0 &&
                actual.name == (expected.name ?? "").Trim() &&
                (actual.category ?? "").Trim().ToUpperInvariant() == (expected.category ?? "").Trim().ToUpperInvariant(),
                "Working model count/name/category differs from source");
            string[] ids = actual.parts.Select(p => p.stableId).ToArray();
            Require(ids.All(id => !string.IsNullOrWhiteSpace(id)) && ids.Distinct(StringComparer.Ordinal).Count() == ids.Length,
                "Working part identities are invalid");
            int expectedPrimary = expected.parts.FindIndex(p => p.stableId == expected.primaryPartId);
            int actualPrimary = actual.parts.FindIndex(p => p.stableId == actual.primaryPartId);
            Require(expectedPrimary == actualPrimary && (actual.primaryPartId == null || actualPrimary >= 0), "Primary part binding changed");
            try
            {
                // Import/copy may allocate identities; every other persisted part field must stay exact.
                for (int i = 0; i < ids.Length; ++i) actual.parts[i].stableId = expected.parts[i].stableId;
                Require(Parts(expected).SequenceEqual(Parts(actual)), "Working part fields changed from prepared source");
            }
            finally { for (int i = 0; i < ids.Length; ++i) actual.parts[i].stableId = ids[i]; }
        }

        private static byte[] Parts(CompositeBlueprintStore.Blueprint blueprint)
        {
            using (var output = new MemoryStream())
            { new DataContractJsonSerializer(blueprint.parts.GetType()).WriteObject(output, blueprint.parts); return output.ToArray(); }
        }

        private static string Hash(byte[] bytes)
        { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
