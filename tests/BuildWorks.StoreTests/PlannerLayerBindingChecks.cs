using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlannerLayerBindingChecks
    {
        private static void Check(bool ok, string why) { if (!ok) throw new Exception(why); }
        internal static void Run(string file, string directory)
        {
            Directory.CreateDirectory(directory);
            string raw = File.ReadAllText(file);
            var evidence = new BlueprintSourceDocument(PlannerBlueprint.Format, "planner.json", raw);
            var source = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "Planner", category = "Default", prefabName = "wood_floor", importSource = file };
            source.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
            foreach (var l in evidence.Planner.Layers)
                source.groups.Add(new CompositeBlueprintStore.Group { stableId = "group" + source.groups.Count, name = l.Name, sourceDocumentId = evidence.Id, sourceLayerId = l.Id });
            foreach (var p in evidence.Planner.Pieces)
                source.parts.Add(new CompositeBlueprintStore.Part { stableId = "part" + p.Record, prefabName = p.Prefab, displayName = p.Prefab,
                    position = new CompositeBlueprintStore.VectorData { x = (float)p.Position.X, y = (float)p.Position.Y, z = (float)p.Position.Z },
                    rotation = new CompositeBlueprintStore.QuaternionData { x = (float)p.Rotation.X, y = (float)p.Rotation.Y, z = (float)p.Rotation.Z, w = (float)p.Rotation.W },
                    parentGroupId = source.groups.Single(g => g.sourceLayerId == p.LayerId).stableId,
                    sourceDocumentId = evidence.Id, sourceRecord = p.Record, sourceFrameApplied = true });
            Check(CompositeBlueprintStore.Valid(source), "Initial bound document invalid");
            void Bound(CompositeBlueprintStore.Blueprint b) => Check(b.groups.Count == evidence.Planner.Layers.Count && b.groups.All(g =>
                g.sourceDocumentId == evidence.Id && evidence.HasLayerAddress(g.sourceLayerId)) && b.sourceDocuments.Single().text == raw, "Binding/raw source lost");
            var copy = source.ImportCopy(); Bound(copy);
            string originalName = source.groups[0].name; copy.groups[0].name = "Copy renamed";
            Check(source.groups[0].name == originalName, "ImportCopy shares mutable groups with source");
            var detached = ExternalBlueprintLibrary.DetachedCopy(source); Bound(detached);
            Check(detached.groups[0].stableId != source.groups[0].stableId && detached.parts[0].parentGroupId == detached.groups[0].stableId, "Detach did not remap editor IDs");
            string path = Path.Combine(directory, "library.json"); var store = new CompositeBlueprintStore(path);
            Check(store.TrySaveDocument(source.name, source.category, source.parts, source.groups, source.anchors,
                null, null, source.supports, null, out var saved, out string error, sourceDocuments: source.sourceDocuments), error);
            Bound(saved); Bound(new CompositeBlueprintStore(path).All().Single());
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)); Bound(BlueprintExchangeFormat.Decode(portable));
            try { BlueprintExchangeFormat.Decode(portable.Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":22")); throw new Exception("Planner downgraded to portable22"); }
            catch (InvalidDataException) { }
            string oldPath = Path.Combine(directory, "old.json"); string old = File.ReadAllText(path).Replace("\"version\":" + CompositeBlueprintStore.FormatVersion, "\"version\":34"); File.WriteAllText(oldPath, old);
            var blocked = new CompositeBlueprintStore(oldPath); Check(blocked.All().Count == 0 && !blocked.TryAddCategory("no", out _) && File.ReadAllText(oldPath) == old, "Planner downgraded or old library changed");
            source.groups[0].sourceLayerId = "missing"; Check(!CompositeBlueprintStore.Valid(source), "Warm-cache missing layer accepted");
            source.groups[0].sourceLayerId = null; Check(!CompositeBlueprintStore.Valid(source), "Half source binding accepted");
            const string nullFields = "{\"blueprints\":[{\"groups\":[{\"sourceDocumentId\":null,\"sourceLayerId\":null}]}]}";
            void Structure(bool allow) => BlueprintExchangeFormat.ValidateLibraryJson(nullFields, false, true, true, true, true, true, true, true, allowGroupSourceFields: allow);
            Structure(true); bool refused = false; try { Structure(false); } catch (InvalidDataException) { refused = true; }
            Check(refused, "Null source fields bypassed pre35 field gate");
            Check(File.ReadAllText(file) == raw, "Original changed");
            Console.WriteLine("PASS planner group bindings native35/portable23: copy/detach/reopen/raw source/old-schema and field-presence guards; no editorUI/game.");
        }
    }
}
