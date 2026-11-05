using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class HomesteadHeaderChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "homestead", "H03.blueprint");
            byte[] bytes = File.ReadAllBytes(file);
            using (var hash = SHA256.Create())
                Require(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") ==
                    "20D71D7AC92962A55B1A0F049F7660DA86FCC2C71451158514FD69C5588472C9", "Real H03 bytes changed");
            string raw = Encoding.UTF8.GetString(bytes);
            var parsed = ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(parsed.importError == null && parsed.parts.Count == 1, parsed.importError);
            var evidence = parsed.sourceDocuments.Single().ToGeometry();
            Require(!evidence.HasOpaqueSections, "Real known Homestead metadata/contacts falsely treated as unknown state");
            Require(evidence.HasPartAt(25, "piece_workbench") && parsed.parts[0].sourceLine == 25 &&
                raw.Contains("#HomesteadWorld:BW_QA_43d3719a") &&
                raw.Contains("#HomesteadSavedAt:2026-10-26 18:12:46 +03:00") &&
                raw.Split('\n').Count(line => line.StartsWith("#HomesteadTerrainContact:")) == 15,
                "Real producer provenance/contacts/physical piece row lost");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            Require(!CompositeBlueprintStore.HasForeignData(prepared) &&
                CompositeBlueprintStore.InspectContents(prepared, null).Unknown == 0,
                "Known metadata is still offered as unknown contents");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0);
            var review = CompositeBlueprintStore.ReviewImportFile(file, ForeignZdoChecks.Hash, 0);
            review = store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(!Directory.Exists(store.ImportDirectory), "Review wrote the import library");
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string importError), importError);
            var task = (Task)typeof(CompositeBlueprintStore).GetField("importTask",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(store);
            Require(task != null && task.Wait(60000), "Reviewed import timeout");
            for (int i = 0; i < 1000; ++i)
            { store.CatalogAll(); if (!store.CatalogLoading) break; System.Threading.Thread.Sleep(10); }
            Require(!store.CatalogLoading && store.ImportError == null &&
                store.CatalogAll().Single().sourceDocuments.Single().text == raw, "Imported catalog lost source");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var copy = new MemoryStream())
            {
                using (var input = zip.GetEntry("original/source.bin").Open()) input.CopyTo(copy);
                Require(copy.ToArray().SequenceEqual(bytes), "Archived original changed");
            }
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            var decoded = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
            var reimported = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "copy.buildworks.json");
            reimported.id = saved.id;
            foreach (var document in new[] { reopened, decoded, reimported })
                Require(BlueprintExchangeFormat.Encode(document).SequenceEqual(portable) &&
                    document.sourceDocuments.Single().text == raw &&
                    !document.sourceDocuments.Single().ToGeometry().HasOpaqueSections &&
                    document.parts.Single().sourceLine == 25,
                    "Homestead native/portable roundtrip changed raw/fields/provenance/classification");
            Require(File.ReadAllBytes(file).SequenceEqual(bytes), "Producer file was mutated");
            foreach (string name in new[] { "sample_001.blueprint", "sample_002.blueprint", "sample_003.blueprint", "sample_snap.blueprint" })
            {
                string sampleFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "homestead", name);
                var sample = ExternalBlueprintLibrary.LoadFile(sampleFile).Single();
                Require(sample.importError == null && !sample.sourceDocuments.Single().ToGeometry().HasOpaqueSections &&
                    sample.sourceDocuments.Single().text == File.ReadAllText(sampleFile), "Actual Homestead sample failed: " + name);
                var copy = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(sample,
                    _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
                byte[] encoded = BlueprintExchangeFormat.Encode(copy);
                Require(encoded.SequenceEqual(BlueprintExchangeFormat.Encode(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(encoded)))),
                    "Actual sample portable fields changed: " + name);
            }

            const string piece = "#Pieces\nwoodwall;;3;4;5;0;0;0;1;\"\";1;1;1\n";
            foreach (string known in new[] { "#HomesteadVersion:1\n", "#HomesteadWorld:\n",
                "#HomesteadSavedAt:\n", "#HomesteadSavedAt:producer descriptive timestamp\n", "#HomesteadRadius:0\n", "#HomesteadRadius:2,5\n",
                "#HomesteadTerrainContact:-1,5;2;3\n" })
                Require(!Parse((known.StartsWith("#HomesteadVersion:") ? "" : "#HomesteadVersion:1\n") + known + piece).sourceDocuments.Single().ToGeometry().HasOpaqueSections,
                    "Known inert metadata was rejected: " + known);
            foreach (string unknown in new[] { "#HomesteadFuture:value\n", "#HomesteadWorldExtra:world\n",
                "#HomesteadVersion:0\n", "#HomesteadVersion:2\n", "#HomesteadVersion:no\n", "#HomesteadVersion:999999999999\n",
                "#HomesteadVersion:1\n#HomesteadVersion:1\n", "#HomesteadWorld:a\n#HomesteadWorld:b\n",
                "#HomesteadSavedAt:a\n#HomesteadSavedAt:b\n", "#HomesteadRadius:1\n#HomesteadRadius:2\n",
                "#HomesteadSavedAt:a\0b\n", "#HomesteadRadius:-1\n", "#HomesteadRadius:NaN\n",
                "#HomesteadRadius:Infinity\n", "#HomesteadRadius:\n", "#HomesteadTerrainContact:1;2\n", "#HomesteadTerrainContact:1;;3\n",
                "#HomesteadTerrainContactExtra:1;2;3\n", "#HomesteadTerrainContact:1;2;3;future\n", "#HomesteadWorld\n",
                "#HomesteadTerrainContact:1;NaN;3\n", "#HomesteadTerrainContact:1;2;1e999\n",
                "#HomesteadWorld:a\0b\n", "#HomesteadWorld:" + new string('x', 257) + "\n" })
            {
                string rawFuture = (unknown.StartsWith("#HomesteadVersion:") ? "" : "#HomesteadVersion:1\n") + unknown + piece;
                var future = Parse(rawFuture);
                Require(future.importError == null && future.parts.Count == 1 &&
                    future.sourceDocuments.Single().text == rawFuture &&
                    future.sourceDocuments.Single().ToGeometry().HasOpaqueSections &&
                    CompositeBlueprintStore.InspectContents(future, null).Unknown > 0,
                    "Future/malformed metadata lost raw or its unknown warning/blocker: " + unknown);
                bool blocked = false;
                try { CompositeBlueprintStore.CopyWithContentsPolicy(future, true, false, null); }
                catch (InvalidDataException) { blocked = true; }
                Require(blocked, "Contents policy cleared unknown source metadata");
            }
            var late = Parse("#HomesteadVersion:1\n" + piece + "#HomesteadWorld:world\nwoodwall;;0;0;0;0;0;0;1\n");
            var hidden = Parse("#Future\n#HomesteadVersion:1\n#HomesteadWorld:world\nwoodwall;;0;0;0;0;0;0;1\n" + piece);
            Require(late.parts.Count == 1 && late.sourceDocuments.Single().ToGeometry().HasOpaqueSections &&
                hidden.parts.Count == 1 && hidden.sourceDocuments.Single().ToGeometry().HasOpaqueSections &&
                !hidden.sourceDocuments.Single().ToGeometry().HasPartAt(4, "woodwall"),
                "Metadata resumed a late/unknown section or indexed its hidden row");
            Require(new BlueprintSourceDocument("vbuild", "other.vbuild", "#HomesteadVersion:1\n#HomesteadWorld:world\n" + piece).HasOpaqueSections,
                "Homestead metadata leaked into vbuild");
            Require(Parse("#HomesteadWorld:world\n" + piece).sourceDocuments.Single().ToGeometry().HasOpaqueSections,
                "Missing Homestead version was guessed");
            var contactAndState = Parse("#HomesteadVersion:1\n#HomesteadTerrainContact:1;2;3\n#Pieces\nchest;;0;0;0;0;0;0;1;opaque\n");
            Require(CompositeBlueprintStore.InspectContents(contactAndState, null).Unknown > 0,
                "Known contact cleared unknown per-part gameplay data");
            string contentsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "contents", "25-contents-populated.buildworks.json");
            var typed = BlueprintExchangeFormat.Decode(File.ReadAllText(contentsPath));
            typed.sourceDocuments = new System.Collections.Generic.List<CompositeBlueprintStore.SourceDocument>
                { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
            foreach (bool storage in new[] { false, true })
            foreach (bool mounts in new[] { false, true })
            {
                var policy = CompositeBlueprintStore.CopyWithContentsPolicy(typed, storage, mounts, ForeignZdoChecks.Hash);
                Require(policy.parts[2].foreignData.contentsCleared == storage &&
                    policy.parts[3].foreignData.contentsCleared == mounts && policy.sourceDocuments.Last().text == raw,
                    "Known metadata coupled or blocked independent typed contents policies");
            }
            Console.WriteLine("PASS real Homestead H03 metadata/15 inert contacts, native/portable source exact, malformed/future/late/dialect/state blockers retained");
        }

        private static CompositeBlueprintStore.Blueprint Parse(string raw) => ExternalBlueprintLibrary.Parse(raw, "headers.blueprint");
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
