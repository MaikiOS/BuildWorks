using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class PlannerLayerBindingChecks
    {
        private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        internal static void Run(string file)
        {
            string raw = File.ReadAllText(file);
            var model = PlannerBlueprint.Parse(raw);
            // Empty-layer transport is synthetic; the input two-layer file is an original UI export.
            string extended = model.WritePieces(model.Pieces.Select(p => (p.Prefab, p.Position, p.Rotation, p.Record, p.LayerId)),
                layers: model.Layers.Concat(new[] { new PlannerBlueprint.Layer("empty", "Empty") }));
            var source = new BlueprintSourceDocument(PlannerBlueprint.Format, "planner.json", extended);
            var groups = source.Planner.Layers.Select((l, i) => new BlueprintEditorGroup("group" + i, l.Name, true, false, null, null,
                sourceDocumentId: source.Id, sourceLayerId: l.Id)).ToArray();
            var parts = source.Planner.Pieces.Select(p => new BlueprintEditorPart("part" + p.Record, p.Prefab, p.Prefab,
                p.Position, p.Rotation, groups.Single(g => g.SourceLayerId == p.LayerId).StableId,
                sourceDocumentId: source.Id, sourceRecord: p.Record, sourceFrameApplied: true)).ToArray();
            var doc = new BlueprintEditorDocument("source", "Planner", "Default", parts, groups, sourceDocuments: new[] { source });
            Check(doc.Groups.Count == 3 && doc.Groups.All(g => g.SourceDocumentId == source.Id), "Initial layer bindings");
            Check(doc.Rename(groups[0].StableId, "Renamed") && doc.Groups[0].SourceLayerId == "main", "Rename changed binding");
            Check(doc.SelectOnly(groups[0].StableId) && doc.DuplicateSelection(new Point3(2, 0, 0)), "Duplicate layer");
            Check(doc.Groups.Count(g => g.SourceLayerId == "main") == 2 && doc.Groups.Select(g => g.StableId).Distinct().Count() == doc.Groups.Count, "Duplicate lost source/editor identities");
            Check(doc.Undo() && doc.Groups.Count == 3 && doc.Groups.All(g => g.SourceDocumentId == source.Id), "History lost binding");
            var target = new BlueprintEditorDocument(null, "Target", "Default");
            Check(target.InsertBlueprint(doc, new Point3(10, 1, 5), new Rotation3(0, 0, 0, 1)), "Insert failed");
            Check(target.Groups.Count(g => g.SourceDocumentId == source.Id) == 3 &&
                target.Groups.Any(g => g.SourceLayerId == "empty") && target.Parts.All(p => p.SourceDocumentId == source.Id && p.SourceRecord > 0), "Insert lost layer/record provenance");
            bool rejected = false;
            try { new BlueprintEditorDocument(null, "Bad", "Default", parts,
                new[] { new BlueprintEditorGroup("bad", "Bad", true, false, null, null, sourceDocumentId: source.Id, sourceLayerId: "missing") }, sourceDocuments: new[] { source }); }
            catch (ArgumentException) { rejected = true; }
            Check(rejected, "Missing source layer accepted");
            Check(source.HasPartAddress(0, 1, parts[0].PrefabName) && source.HasPlacementFrame && source.HasLayerAddress("empty"), "Source address/frame");
            Check(File.ReadAllText(file) == raw, "Original changed");
            Console.WriteLine("PASS planner group bindings: original2layers + synthetic empty, rename/duplicate/history/insert/source guards; no host/game.");
        }
    }
}
