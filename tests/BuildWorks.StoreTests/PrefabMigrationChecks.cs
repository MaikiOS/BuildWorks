using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PrefabMigrationChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "legacy.blueprint");
            File.WriteAllText(path, "#Name: Migration\n#Pieces\nwood_wall_roof;;1;2;3;0;0;0;1;\"\";1;1;1;\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1;\n");
            byte[] originalBytes = File.ReadAllBytes(path);
            var source = ExternalBlueprintLibrary.LoadFile(path).Single();
            var normalized = ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, ForeignZdoChecks.Hash, 0);
            Require(ReferenceEquals(normalized, ExternalBlueprintLibrary.MigrateLegacyRoof(normalized, false)), "Unverified host migrated");
            var migratedExternal = ExternalBlueprintLibrary.MigrateLegacyRoof(normalized, true);
            var migrated = ExternalBlueprintLibrary.DetachedCopy(migratedExternal);
            Require(CompositeBlueprintStore.Valid(migrated) && normalized.parts[0].prefabName == BlueprintPrefabMigration.OldRoof &&
                migrated.parts[0].prefabName == BlueprintPrefabMigration.CurrentRoof && migrated.parts[0].originalPrefabName == BlueprintPrefabMigration.OldRoof &&
                migrated.parts[0].position.x == normalized.parts[0].position.x && migrated.parts[0].sourceLine == normalized.parts[0].sourceLine &&
                migrated.sourceDocuments[0].text == normalized.sourceDocuments[0].text && File.ReadAllBytes(path).SequenceEqual(originalBytes), "Migration changed source or transform");
            Require(ReferenceEquals(migratedExternal, ExternalBlueprintLibrary.MigrateLegacyRoof(migratedExternal, true)), "Migration repeated");
            bool gate = true;
            var pipeline = new CompositeBlueprintStore(Path.Combine(folder, "pipeline", "library.json"));
            pipeline.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                prefabHash: ForeignZdoChecks.Hash, worldLevel: () => 0, migrateLegacyRoof: () => gate);
            var rawReview = CompositeBlueprintStore.ReviewImportFile(path, ForeignZdoChecks.Hash, 0);
            var review = pipeline.PrepareImportReview(rawReview, ForeignZdoChecks.Hash).GetAwaiter().GetResult();
            Require(review.LegacyRoofMigrated && review.Documents[0].parts[0].originalPrefabName == BlueprintPrefabMigration.OldRoof &&
                rawReview.Documents[0].parts[0].prefabName == BlueprintPrefabMigration.OldRoof, "Import review lost migration or changed raw inspection");
            gate = false;
            Require(!pipeline.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out _) && !Directory.Exists(pipeline.ImportDirectory), "Changed prototype accepted after review");
            gate = true;
            Require(pipeline.TryImportReviewed(review, false, false, ForeignZdoChecks.Hash, out string pipelineError), pipelineError);
            for (int attempt = 0; attempt < 1000; attempt++)
            { pipeline.CatalogAll(); if (!pipeline.CatalogLoading) break; System.Threading.Thread.Sleep(10); }
            Require(!pipeline.CatalogLoading && pipeline.ImportError == null && pipeline.CatalogAll().Single().parts[0].originalPrefabName == BlueprintPrefabMigration.OldRoof &&
                File.ReadAllBytes(path).SequenceEqual(originalBytes), "Reviewed archive commit lost migration/source");
            foreach (var copy in new[] { migrated.ImportCopy(), ExternalBlueprintLibrary.DetachedCopy(migratedExternal),
                BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(migrated))) })
                Require(copy.parts[0].originalPrefabName == BlueprintPrefabMigration.OldRoof && CompositeBlueprintStore.Valid(copy), "Copy lost migration provenance");
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(migrated));
            Require(portable.Contains("\"version\":14"), "Portable migration version missing");
            Require(PlanBuildExport.Review(migrated, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }).PrefabMigrations == 1,
                "Foreign export omitted migration without reporting loss");
            Refuses(() => BlueprintExchangeFormat.Decode(portable.Replace("\"version\":14", "\"version\":12")));
            foreach (Action<CompositeBlueprintStore.Blueprint> tamper in new Action<CompositeBlueprintStore.Blueprint>[] {
                value => value.parts[0].originalPrefabName = "other", value => value.parts[0].prefabName = "woodwall",
                value => value.parts[0].sourceLine++, value => value.parts[0].sourceDocumentId = null })
            {
                var invalid = migrated.ImportCopy(); tamper(invalid);
                Require(!CompositeBlueprintStore.Valid(invalid), "Tampered migration accepted");
            }
            var store = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            Require(store.TryGetOrCreatePlacementCopy(migratedExternal, out var own, out string error), error);
            string library = File.ReadAllText(Path.Combine(folder, "library.json"));
            Require(library.Contains("\"version\":25") && new CompositeBlueprintStore(Path.Combine(folder, "library.json")).All().Single().parts[0].originalPrefabName == BlueprintPrefabMigration.OldRoof,
                "Native migration not saved/reopened");
            File.WriteAllText(Path.Combine(folder, "old-library.json"), library.Replace("\"version\":25", "\"version\":23"));
            Require(new CompositeBlueprintStore(Path.Combine(folder, "old-library.json")).All().Count == 0, "Native23 accepted new migration field");
            var part = migrated.parts[0];
            var editorPart = new BlueprintEditorPart(part.stableId, part.prefabName, part.displayName, new Point3(1, 2, 3), new Rotation3(0, 0, 0, 1),
                sourceDocumentId: part.sourceDocumentId, sourceLine: part.sourceLine, originalPrefabName: part.originalPrefabName);
            var document = new BlueprintEditorDocument("doc", "Migration", "QA", new[] { editorPart }, Array.Empty<BlueprintEditorGroup>(),
                sourceDocuments: migrated.sourceDocuments.Select(value => value.ToGeometry()));
            document.SelectOnly(editorPart.StableId);
            Require(document.DuplicateSelection(new Point3(2, 0, 0), "Copy") && document.Parts.All(value => value.OriginalPrefabName == BlueprintPrefabMigration.OldRoof) &&
                document.Undo() && document.Parts.Count == 1 && document.Redo() && document.Parts.Count == 2, "Editor copy/history lost migration");
            Console.WriteLine("PASS explicit roof migration: verified-host gate, immutable source/TRS, copies, native24/portable13, old version/tamper refusal and editor history");
        }
        private static void Refuses(Action action)
        { try { action(); } catch (InvalidDataException) { return; } throw new Exception("Invalid migration document accepted"); }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
