using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlannerExportChecks
    {
        private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        private static CompositeBlueprintStore.VectorData? Basis(string _) => new CompositeBlueprintStore.VectorData { x = 2, y = 3, z = 4 };
        private static CompositeBlueprintStore.Blueprint Prepared(string file) => ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.LoadFile(file).Single(), Basis);
        private static string Write(CompositeBlueprintStore.Blueprint source, string directory, string name)
        {
            var review = PlannerExport.Review(source, Basis);
            Check(review.Blockers.Count == 0, "Planner blockers: " + string.Join(",", review.Blockers));
            Check(!Directory.Exists(Path.Combine(directory, name)), "Review wrote before confirmation");
            Check(review.TryWrite(source, Basis, directory, name, out string result, out string error), error);
            var native = BlueprintExchangeFormat.Decode(File.ReadAllText(Path.Combine(result, name + ".buildworks.json")));
            Check(native.sourceDocuments.Count == source.sourceDocuments.Count && native.groups.Count == source.groups.Count, "Full copy lost sources/groups");
            Check(native.sourceDocuments.Select(s => s.text).SequenceEqual(source.sourceDocuments.Select(s => s.text)) &&
                native.groups.Select(g => (g.sourceDocumentId, g.sourceLayerId)).SequenceEqual(source.groups.Select(g => (g.sourceDocumentId, g.sourceLayerId))),
                "Native companion lost exact originals or layer bindings");
            return File.ReadAllText(Path.Combine(result, name + ".json"));
        }

        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string fixtures = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "planner");
            foreach (string path in Directory.GetFiles(fixtures, "*.json"))
            {
                var original = Prepared(path); string raw = File.ReadAllText(path);
                Check(Write(original, directory, Path.GetFileNameWithoutExtension(path)) == raw, "No-edit changed original planner tokens");
            }
            string file = Path.Combine(directory, "opaque.json");
            string input = File.ReadAllText(Path.Combine(fixtures, "planner-three-pieces.json")).Replace("\"name\": \"Main\"", "\"name\": \"Main\",\"futureLayer\":{\"answer\":42}");
            File.WriteAllText(file, input, new UTF8Encoding(false)); var source = Prepared(file);
            Check(source.sourceDocuments.Single().ToGeometry().Planner.UnknownPaths.Count == 1, "Opaque layer fixture ineffective");
            var copied = source.ImportCopy(); var donor = copied.groups[0]; string copyId = Guid.NewGuid().ToString("N");
            copied.groups.Add(new CompositeBlueprintStore.Group { stableId = copyId, name = "Copy", sourceDocumentId = donor.sourceDocumentId, sourceLayerId = donor.sourceLayerId });
            var newPart = source.ImportCopy().parts[0]; newPart.stableId = Guid.NewGuid().ToString("N"); newPart.parentGroupId = copyId; copied.parts.Add(newPart);
            string duplicate = Write(copied, directory, "duplicate"); var model = PlannerBlueprint.Parse(duplicate);
            Check(model.Layers.Count == 3 && Regex.Matches(duplicate, "futureLayer").Count == 2 && model.Pieces.Select(p => p.Uid).Distinct().Count() == 4 &&
                model.Pieces[3].LayerId != model.Pieces[0].LayerId, "Layer/part duplicate loses raw donor, IDs or membership");
            Check(model.Layers.All(l => l.Id.Length <= 64), "Original planner truncates generated layer IDs beyond64 characters");
            string longFile = Path.Combine(directory, "long-layer.json");
            File.WriteAllText(longFile, input.Replace(source.groups[1].sourceLayerId, new string('l', 65)), new UTF8Encoding(false));
            var longSource = Prepared(longFile); var longReview = PlannerExport.Review(longSource, Basis);
            Check(longReview.Blockers.Any(b => b.Reason == PlannerExport.BlockReason.Format) &&
                !longReview.TryWrite(longSource, Basis, directory, "blocked-layer", out _, out _) && !Directory.Exists(Path.Combine(directory, "blocked-layer")),
                "Unsafe original layer ID exported or left output files");
            string boundaryFile = Path.Combine(directory, "layer64.json");
            File.WriteAllText(boundaryFile, input.Replace(source.groups[1].sourceLayerId, new string('l', 64)), new UTF8Encoding(false));
            Check(Write(Prepared(boundaryFile), directory, "layer64") == File.ReadAllText(boundaryFile), "Safe64-character source layer changed or blocked");
            Check(Write(copied, directory, "duplicate-repeat") == duplicate, "Same snapshot changed generated layer IDs or UIDs");
            var omittedLong = Prepared(longFile); string omittedId = omittedLong.groups[1].stableId;
            omittedLong.parts.RemoveAll(p => p.parentGroupId == omittedId); omittedLong.groups.RemoveAt(1);
            Check(PlannerExport.Review(omittedLong, Basis).Blockers.Count == 0, "Archived but omitted long layer ID blocked safe output");
            string collisionFile = Path.Combine(directory, "collision-layer.json");
            File.WriteAllText(collisionFile, input.Replace(source.groups[1].sourceLayerId, model.Layers[2].Id), new UTF8Encoding(false));
            var collision = Prepared(collisionFile); var collisionDonor = collision.groups[0];
            collision.groups.Add(new CompositeBlueprintStore.Group { stableId = copyId, name = "Collision", sourceDocumentId = collisionDonor.sourceDocumentId, sourceLayerId = collisionDonor.sourceLayerId });
            var collisionModel = PlannerBlueprint.Parse(Write(collision, directory, "collision"));
            Check(collisionModel.Layers[2].Id == model.Layers[2].Id + "_1" && collisionModel.Layers.All(l => l.Id.Length <= 64), "Reserved original layer collision lost suffix or length budget");
            var renamed = source.ImportCopy(); renamed.groups[0].name = "Renamed";
            Check(PlannerBlueprint.Parse(Write(renamed, directory, "renamed")).Layers[0].Name == "Renamed", "Layer rename failed");
            var moved = source.ImportCopy(); moved.parts[0].position.x += 1;
            Check(Math.Abs(PlannerBlueprint.Parse(Write(moved, directory, "moved")).Pieces[0].Position.X - moved.parts[0].position.x) < 1e-6, "Edited frame failed");
            var stale = PlannerExport.Review(source, Basis);
            Check(!stale.TryWrite(moved, Basis, directory, "stale", out _, out _) && !Directory.Exists(Path.Combine(directory, "stale")), "Stale pair published");
            var cancelled = PlannerExport.Review(source, Basis); cancelled.CancelWrite(); bool stopped = false;
            try { cancelled.TryWrite(source, Basis, directory, "cancelled", out _, out _); } catch (OperationCanceledException) { stopped = true; }
            Check(stopped && !Directory.Exists(Path.Combine(directory, "cancelled")), "Cancelled pair published");
            Check(!stale.TryWrite(source, Basis, directory, "bad.buildworks", out _, out _) && !Directory.Exists(Path.Combine(directory, "bad.buildworks")), "Reserved name published");
            Check(!stale.TryWrite(source, Basis, directory, "duplicate", out _, out _) && File.ReadAllText(file) == input, "Existing pair/source overwritten");
            var pitch = source.ImportCopy(); pitch.parts[0].rotation.x = .1f;
            Check(PlannerExport.Review(pitch, Basis).Blockers.Any(b => b.Reason == PlannerExport.BlockReason.Rotation), "Pitch silently lost");
            var scaled = source.ImportCopy(); scaled.parts[0].scale.x = 2;
            Check(PlannerExport.Review(scaled, Basis).Blockers.Any(b => b.Reason == PlannerExport.BlockReason.Scale), "Absolute nonunit scale silently lost");
            var fresh = source.ImportCopy(); fresh.sourceDocuments.Clear();
            foreach (var g in fresh.groups) { g.sourceDocumentId = null; g.sourceLayerId = null; }
            foreach (var p in fresh.parts) { p.sourceDocumentId = null; p.sourceRecord = 0; p.sourceFrameApplied = false; }
            Check(PlannerBlueprint.Parse(Write(fresh, directory, "fresh")).Pieces.Count == 3, "Native-only blueprint cannot export to planner");
            foreach (var p in fresh.parts) p.baseScale = null;
            Check(PlannerExport.Review(fresh, _ => null).Blockers.Any(b => b.Reason == PlannerExport.BlockReason.PrefabScaleUnknown), "Unknown intrinsic scale assumed unit");
            Check(PlannerExport.Review(fresh, Basis).Blockers.Any(b => b.Reason == PlannerExport.BlockReason.Scale), "Unit-relative confused with absolute unit");
            var multiple = source.ImportCopy(); multiple.sourceDocuments.Add(CompositeBlueprintStore.SourceDocument.FromGeometry(
                new BlueprintSourceDocument(PlannerBlueprint.Format, "other.json", input)));
            Check(PlannerExport.Review(multiple, Basis).Blockers.Any(b => b.Reason == PlannerExport.BlockReason.MultipleSources), "Multiple original root records silently merged");
            var ungrouped = source.ImportCopy(); ungrouped.parts[0].parentGroupId = null;
            var ungroupedModel = PlannerBlueprint.Parse(Write(ungrouped, directory, "ungrouped"));
            Check(ungroupedModel.Layers.Count == 3 && ungroupedModel.Pieces[0].LayerId != source.groups[0].sourceLayerId, "Ungrouped part silently merged into source layer");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "async-library.json")); int calls = 0, owner = System.Threading.Thread.CurrentThread.ManagedThreadId;
            typeof(CompositeBlueprintStore).GetField("importPrefabScale", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(store, (Func<string, CompositeBlueprintStore.VectorData?>)(id => { Check(System.Threading.Thread.CurrentThread.ManagedThreadId == owner, "Prefab lookup escaped owner thread"); ++calls; return Basis(id); }));
            var asyncReview = store.ReviewPlannerExport(source).GetAwaiter().GetResult();
            Check(asyncReview.Blockers.Count == 0 && calls == source.parts.Select(p => p.prefabName).Distinct().Count(), "Main-thread scale snapshot incomplete");
            Check(Directory.Exists(store.ExportPlanner(asyncReview, source, directory, "async").GetAwaiter().GetResult()), "Async pair did not publish");
            Console.WriteLine("PASS planner pair no-edit/rename/move/opaque-layer duplicate/new source/stale/cancel/no-overwrite/scale/pitch; no UI/game proof.");
        }

        internal static void Actual(string cases, string directory)
        {
            Run(directory);
            foreach (string stem in new[] { "catalog-yaw0", "catalog-yaw22-added" })
            {
                string file = Path.Combine(cases, "producer-catalog-v1", stem + ".json");
                Check(Write(Prepared(file), directory, stem) == File.ReadAllText(file), "Actual catalogue no-edit bytes changed");
                Console.WriteLine("PASS original planner pair byte-exact " + stem);
            }
        }
    }
}
