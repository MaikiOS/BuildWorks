using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class MissingPartChecks
    {
        private static readonly CompositeBlueprintStore.VectorData Unit = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            const string text = "#Name:QA113 Missing Choice\r\n#Pieces\r\nwoodwall;;1;2;3;0;.7071068;0;.7071068;\"\";2;.5;1.25\r\n" +
                "qa_absent;;8;9;10;0;0;0;1;\"unknown-state\";.01;4;2\r\n";
            string sourcePath = Path.Combine(directory, "12-missing-choice.blueprint");
            File.WriteAllText(sourcePath, text);
            byte[] original = File.ReadAllBytes(sourcePath);
            var review = CompositeBlueprintStore.ReviewImportFile(sourcePath, ForeignZdoChecks.Hash, 0);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            var unknown = store.InspectMissing(review.Documents, ForeignZdoChecks.Hash);
            Require(!unknown.Known && !unknown.CanOmit && review.Documents[0].parts.Count == 2, "Absent resolver guessed missing prefabs");
            Require(!store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult().Missing.Known,
                "Background review invented availability without a resolver");
            object context = null;
            var installed = new HashSet<string>(StringComparer.Ordinal) { "woodwall", "world_tree" };
            store.ConfigureImportScale(name => installed.Contains(name) ? Unit : (CompositeBlueprintStore.VectorData?)null, () => context);
            Require(!store.InspectMissing(review.Documents, ForeignZdoChecks.Hash).Known, "Unloaded host guessed absent prefabs");
            Require(!store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult().Missing.Known,
                "Background review guessed availability in an unloaded host");
            context = new object();
            var missing = store.InspectMissing(review.Documents, ForeignZdoChecks.Hash);
            Require(missing.Known && missing.CanOmit && missing.Parts == 1 && missing.PrefabIds.SetEquals(new[] { "qa_absent" }) &&
                missing.RetainedContents.Unknown == 0 && review.Contents.Unknown == 1 &&
                missing.Sources.Single().Key == "12-missing-choice.blueprint · qa_absent", "Missing review count/source/retained state wrong");
            Require(!Directory.Exists(store.ImportDirectory), "Availability review wrote a copy");
            var preparedReview = store.PrepareImportReview(review, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            var backgroundMissing = preparedReview.Missing;
            Require(backgroundMissing.Known && ReferenceEquals(backgroundMissing.HostContext, missing.HostContext) &&
                backgroundMissing.Parts == missing.Parts && backgroundMissing.EmptyDocuments == missing.EmptyDocuments &&
                backgroundMissing.PrefabIds.SetEquals(missing.PrefabIds) && backgroundMissing.Sources.SequenceEqual(missing.Sources) &&
                backgroundMissing.RetainedContents.Unknown == 0 && preparedReview.Contents.Unknown == 1 &&
                review.Documents[0].parts.Count == 2, "Background missing/retained review differs from host review or mutates source");
            var plain = CompositeBlueprintStore.CopyWithoutMissing(preparedReview.Documents[0], backgroundMissing.PrefabIds);
            var repeated = store.PrepareImportReview(new CompositeBlueprintStore.ImportReview { Source = review.Source, Bytes = review.Bytes,
                Documents = new System.Collections.Generic.List<CompositeBlueprintStore.Blueprint> { plain, plain } }, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(repeated.Documents.Count == 2 && ReferenceEquals(repeated.Documents[0], repeated.Documents[1]) &&
                repeated.Missing.Known && repeated.Missing.Parts == 0 && repeated.Missing.RetainedContents.Unknown == repeated.Contents.Unknown,
                "Repeated normalized document reference broke retained summary reuse");
            var validationOnly = store.InspectMissing(review.Documents,
                _ => throw new InvalidOperationException("Missing validation parsed retained contents"), inspectRetainedContents: false);
            Require(validationOnly.Known == missing.Known && ReferenceEquals(validationOnly.HostContext, missing.HostContext) &&
                validationOnly.Parts == missing.Parts && validationOnly.EmptyDocuments == missing.EmptyDocuments &&
                validationOnly.PrefabIds.SetEquals(missing.PrefabIds) && validationOnly.Sources.SequenceEqual(missing.Sources),
                "Lightweight missing validation changed availability/counts/context/provenance");

            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string error), error); Wait(store);
            Require(ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single().parts.Count == 2,
                "Default import discarded a missing record");
            Require(store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out error, omitMissing: backgroundMissing), error); Wait(store);
            var imported = ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single();
            Require(imported.parts.Count == 1 && imported.parts[0].prefabName == "woodwall" &&
                imported.parts[0].position.x == 1 && imported.parts[0].position.z == 3 &&
                imported.parts[0].baseScale.Value.x == 2 && imported.parts[0].baseScale.Value.y == .5f &&
                imported.parts[0].rotation.y == review.Documents[0].parts[0].rotation.y &&
                imported.sourceDocuments.Single().text == text && File.ReadAllBytes(sourcePath).SequenceEqual(original),
                "Explicit omission changed source, retained pose/basis or synthesized pieces");
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
            using (var entry = zip.GetEntry("original/source.bin").Open())
            using (var bytes = new MemoryStream())
            {
                entry.CopyTo(bytes); Require(bytes.ToArray().SequenceEqual(original), "Original archive bytes changed");
            }
            var prepared = ExternalBlueprintLibrary.PrepareForHost(review.Documents[0],
                name => installed.Contains(name) ? Unit : (CompositeBlueprintStore.VectorData?)null);
            foreach (bool archive in new[] { false, true })
            {
                Require(store.TryExportFile(prepared, directory, archive ? "omitted-zip" : "omitted-json", archive,
                    out string path, out error, prefabHash: ForeignZdoChecks.Hash, omitMissing: missing), error);
                Require(ExternalBlueprintLibrary.LoadFile(path).Single().parts.Count == 1 && prepared.parts.Count == 2,
                    "Export omission mutated source or resurrected removed source rows");
            }
            int before = Directory.GetFiles(store.ImportDirectory).Length;
            installed.Add("qa_absent");
            Require(!store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out error, omitMissing: backgroundMissing) &&
                error.Contains("missing_changed") && Directory.GetFiles(store.ImportDirectory).Length == before, "Changed availability published stale removal");
            installed.Remove("qa_absent");
            context = new object();
            Require(!store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out error, omitMissing: missing) &&
                error.Contains("missing_changed"), "Changed host context accepted old review");
            context = null;
            Require(!store.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out error, omitMissing: missing) &&
                error.Contains("missing_unavailable"), "Unloaded host accepted removal");
            context = new object();

            string mixedPath = Path.Combine(directory, "mixed.zip");
            using (var zip = ZipFile.Open(mixedPath, ZipArchiveMode.Create))
            {
                using (var writer = new StreamWriter(zip.CreateEntry("mixed.blueprint").Open())) writer.Write(text);
                using (var writer = new StreamWriter(zip.CreateEntry("all-missing.blueprint").Open()))
                    writer.Write("#Pieces\nqa_absent;;0;0;0;0;0;0;1;\"\";1;1;1");
            }
            var mixed = CompositeBlueprintStore.ReviewImportFile(mixedPath, ForeignZdoChecks.Hash, 0);
            var emptyReview = store.InspectMissing(mixed.Documents, ForeignZdoChecks.Hash);
            var backgroundEmpty = store.PrepareImportReview(mixed, ForeignZdoChecks.Hash).GetAwaiter().GetResult().Missing;
            Require(backgroundEmpty.EmptyDocuments == 1 && !backgroundEmpty.CanOmit && backgroundEmpty.Parts == emptyReview.Parts &&
                backgroundEmpty.Sources.SequenceEqual(emptyReview.Sources), "Background review accepted an all-missing ZIP member");
            byte[] mixedBytes = File.ReadAllBytes(mixedPath);
            string previousResult = store.ImportResultPath;
            bool acceptedEmpty = store.TryImportReviewed(mixed, false, false, ForeignZdoChecks.Hash, out error);
            if (acceptedEmpty) Wait(store);
            Require(!acceptedEmpty &&
                error.Contains("missing_import_empty") && !store.CatalogLoading && store.ImportResultPath == previousResult &&
                Directory.GetFiles(store.ImportDirectory).Length == before && File.ReadAllBytes(mixedPath).SequenceEqual(mixedBytes),
                "Default keep-missing import published a ZIP member with no available parts");
            string absentPath = Path.Combine(directory, "all-missing.blueprint");
            File.WriteAllText(absentPath, "#Pieces\nqa_absent;;0;0;0;0;0;0;1;\"\";1;1;1");
            var absentReview = CompositeBlueprintStore.ReviewImportFile(absentPath, ForeignZdoChecks.Hash, 0);
            Require(!store.TryImportReviewed(absentReview, false, false, ForeignZdoChecks.Hash, out error) &&
                error.Contains("missing_import_empty") && !store.CatalogLoading && Directory.GetFiles(store.ImportDirectory).Length == before,
                "Default keep-missing import accepted a standalone blueprint with no available parts");
            var absentPrepared = ExternalBlueprintLibrary.PrepareForHost(absentReview.Documents.Single(),
                name => installed.Contains(name) ? Unit : (CompositeBlueprintStore.VectorData?)null);
            Require(store.TryExportFile(absentPrepared, directory, "missing-backup", false,
                out string backup, out error, prefabHash: ForeignZdoChecks.Hash) &&
                ExternalBlueprintLibrary.LoadFile(backup).Single().parts.Single().prefabName == "qa_absent",
                "Import refusal blocked a lossless export backup of an existing missing record: " + error);
            Require(emptyReview.EmptyDocuments == 1 && !emptyReview.CanOmit &&
                !store.TryImportReviewed(mixed, false, false, ForeignZdoChecks.Hash, out error, omitMissing: emptyReview) &&
                error.Contains("missing_empty") && Directory.GetFiles(store.ImportDirectory).Length == before,
                "All-missing member partially published its ZIP");

            Hierarchy(prepared);
            var withTree = prepared.ImportCopy();
            withTree.parts.Add(new CompositeBlueprintStore.Part { stableId = "tree", prefabName = "world_tree",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } });
            Require(store.InspectMissing(new[] { withTree }, ForeignZdoChecks.Hash).Parts == 1,
                "Present world-only prefab was classified missing because it has no recipe");
            string imagePath = Path.Combine(directory, "preview.zip");
            using (var zip = ZipFile.Open(imagePath, ZipArchiveMode.Create))
            {
                using (var entry = zip.CreateEntry("model.blueprint").Open()) entry.Write(original, 0, original.Length);
                byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVQIHWP4z8DwHwAFgAI/ScLbtAAAAABJRU5ErkJggg==");
                using (var entry = zip.CreateEntry("model.png").Open()) entry.Write(png, 0, png.Length);
            }
            var imageReview = CompositeBlueprintStore.ReviewImportFile(imagePath, ForeignZdoChecks.Hash, 0);
            Require(store.TryImportReviewed(imageReview, false, false, ForeignZdoChecks.Hash, out error,
                omitMissing: store.InspectMissing(imageReview.Documents, ForeignZdoChecks.Hash)), error); Wait(store);
            using (var zip = ZipFile.OpenRead(store.ImportResultPath))
                Require(!zip.Entries.Any(entry => entry.FullName.EndsWith(".png")), "Removed-part copy retained misleading old preview");
            Console.WriteLine("PASS explicit missing-part copy: unknown host/default keep/fresh context+IDs; source bytes, retained transforms, JSON/ZIP, hierarchy/support/snap, all-missing atomic refusal and stale preview removal");
        }

        private static void Hierarchy(CompositeBlueprintStore.Blueprint source)
        {
            var document = source.ImportCopy();
            string known = document.parts[0].stableId, absent = document.parts[1].stableId;
            document.parts[0].parentGroupId = "child"; document.parts[1].parentGroupId = "dead";
            document.groups = new List<CompositeBlueprintStore.Group> {
                new CompositeBlueprintStore.Group { stableId = "root", name = "Root", pivotPartId = absent },
                new CompositeBlueprintStore.Group { stableId = "child", name = "Child", parentGroupId = "root", pivotPartId = known },
                new CompositeBlueprintStore.Group { stableId = "dead", name = "Dead", parentGroupId = "root", pivotPartId = absent },
                new CompositeBlueprintStore.Group { stableId = "future", name = "Future roof" },
                new CompositeBlueprintStore.Group { stableId = "future-child", name = "Future child", parentGroupId = "future" } };
            document.primaryPartId = absent; document.primaryGroupId = "dead";
            document.supports = new List<CompositeBlueprintStore.Support> { new CompositeBlueprintStore.Support {
                stableId = "support", name = "Support", position = new CompositeBlueprintStore.VectorData { x = 7 },
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } } };
            document.placementSupportId = "support";
            document.customSnapPoints = new List<CompositeBlueprintStore.VectorData> { new CompositeBlueprintStore.VectorData { y = 8 } };
            document.anchors = new List<CompositeBlueprintStore.VectorData>(document.anchors);
            document.anchors.Add(new CompositeBlueprintStore.VectorData { z = 9 });
            var copy = CompositeBlueprintStore.CopyWithoutMissing(document, new HashSet<string> { "qa_absent" });
            Require(copy.groups.Count == 5 && copy.groups[0].pivotPartId == null && copy.groups[1].parentGroupId == "root" &&
                copy.groups[2].pivotPartId == null && copy.groups[2].parentGroupId == "root" &&
                copy.groups[3].name == "Future roof" && copy.groups[4].parentGroupId == "future" &&
                copy.groups[1].pivotPartId == known && copy.primaryPartId == null && copy.primaryGroupId == null &&
                copy.prefabName == "woodwall" && copy.placementSupportId == "support" && copy.supports[0].position.x == 7 &&
                copy.customSnapPoints[0].y == 8 && copy.anchors.Last().z == 9 &&
                document.parts.Count == 2 && document.groups.Count == 5 && document.groups[0].pivotPartId == absent &&
                document.groups[2].pivotPartId == absent && document.primaryPartId == absent,
                "Omission destroyed empty groups, hierarchy, independent frames or source");
            var detached = ExternalBlueprintLibrary.DetachedCopy(copy);
            var roundtrip = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(detached)));
            Require(roundtrip.parts.Count == 1 && roundtrip.groups.Count == 5 &&
                roundtrip.groups.Single(group => group.name == "Future child").parentGroupId ==
                roundtrip.groups.Single(group => group.name == "Future roof").stableId,
                "Retained empty group graph did not roundtrip");
        }

        private static void Wait(CompositeBlueprintStore store)
        {
            Require(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Import timeout");
            Require(store.ImportError == null, store.ImportError);
        }
        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
