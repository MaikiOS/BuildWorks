using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class MetadataChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var snapshot = new BlueprintPrefabMetadata(1, "woodwall", "Wall", "Piece", "host-a", new Point3(2, .5, 3),
                new Point3(1, -2, -3), new Point3(4, 5, 6));
            var input = new[] { Part("a"), Part("b") };
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            int calls = 0;
            store.ConfigurePrefabMetadata(_ => { ++calls; return CompositeBlueprintStore.PrefabMetadata.FromGeometry(snapshot); });
            Require(store.TrySave("Metadata", input, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            Require(calls == 1 && input.All(part => part.metadata == null) && saved.parts.All(part => part.metadata.hostId == "host-a"),
                "Capture was not unique-per-prefab or mutated the input");
            var unnamed = Part("invalid"); unnamed.prefabName = null;
            Require(!store.TrySave("Invalid", new[] { unnamed }, Array.Empty<CompositeBlueprintStore.VectorData>(), out _, out _),
                "Configured metadata capture bypassed normal invalid-name refusal");
            var clone = CompositeBlueprintStore.CloneParts(saved.parts); clone[0].metadata.name = "Changed";
            Require(saved.parts[0].metadata.name == "Wall", "Metadata DTO clone aliases source");
            string text = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            var decoded = BlueprintExchangeFormat.Decode(text);
            Require(decoded.parts[0].metadata.minimum.Value.x == 1 && decoded.parts[0].metadata.prefabScale.y == .5f,
                "Portable metadata roundtrip lost local bounds/root scale");
            Refuse(() => BlueprintExchangeFormat.Decode(text.Replace("\"version\":11", "\"version\":8")));
            Refuse(() => BlueprintExchangeFormat.Decode(text.Replace("\"kind\":\"Piece\"", "\"kind\":\"Piece\",\"unknown\":1")));
            Refuse(() => BlueprintExchangeFormat.Decode(text.Replace("\"prefabId\":\"woodwall\"", "\"prefabId\":\"other\"")));
            var detached = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.Parse(text, "metadata.buildworks.json"));
            Require(detached.parts[0].metadata.name == "Wall", "Detached copy lost metadata");
            foreach (bool archive in new[] { false, true })
            {
                Require(store.TryExportFile(saved, directory, archive ? "meta-zip" : "meta-json", archive, out string path, out error), error);
                Require(ExternalBlueprintLibrary.LoadFile(path).Single().parts[0].metadata.maximum.Value.z == 6,
                    "JSON/ZIP export lost metadata");
            }
            var missingStore = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            missingStore.ConfigurePrefabMetadata(_ => null);
            var reopened = missingStore.All().Single();
            Require(missingStore.TryUpdateParts(reopened, reopened.parts, reopened.anchors, out error) &&
                reopened.parts[0].metadata.minimum.Value.x == 1, error ?? "Missing prefab erased its snapshot");
            snapshot = new BlueprintPrefabMetadata(1, "woodwall", "Updated", "Piece", "host-b", new Point3(1, 1, 1),
                new Point3(-7, -1, -1), new Point3(7, 1, 1));
            calls = 0;
            Require(store.TryUpdateParts(saved, saved.parts, saved.anchors, out error) && calls == 1 &&
                saved.parts[0].metadata.hostId == "host-b" && saved.parts[0].metadata.minimum.Value.x == -7, "Save reused a stale snapshot");
            var invalid = CompositeBlueprintStore.CloneParts(saved.parts); invalid[0].metadata.version = 2;
            string before = File.ReadAllText(Path.Combine(directory, "library.json"));
            missingStore.ConfigurePrefabMetadata(null);
            Require(!missingStore.TryUpdateParts(reopened, invalid, reopened.anchors, out error) &&
                File.ReadAllText(Path.Combine(directory, "library.json")) == before, "Invalid metadata was published");

            string source = Path.Combine(directory, "source.blueprint");
            File.WriteAllText(source, "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
            var review = CompositeBlueprintStore.ReviewImportFile(source, null);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            Require(store.TryImportReviewed(review, false, false, null, out error), error);
            Require(SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000) && store.ImportError == null, store.ImportError ?? "Import timeout");
            Require(ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single().parts[0].metadata.hostId == "host-b" &&
                review.Documents[0].parts[0].metadata == null, "Reviewed import did not capture without mutating review");
            var noMeta = CompositeBlueprintStore.CloneParts(saved.parts); foreach (var part in noMeta) part.metadata = null;
            var old = saved.ImportCopy(); old.parts = noMeta;
            Require(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(old)).Replace("\"version\":11", "\"version\":8")).parts.Count == 2,
                "Legacy portable8 without metadata stopped loading");
            Geometry(snapshot);
            string fixture = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../../..", "tests/fixtures/metadata/13-recorded-bounds.buildworks.json"));
            var fixtureCopy = ExternalBlueprintLibrary.LoadFile(fixture).Single();
            Require(fixtureCopy.importError == null, fixtureCopy.importError);
            Require(fixtureCopy.parts.Count == 2 && fixtureCopy.parts[0].metadata.minimum.Value.x == -1 &&
                fixtureCopy.parts[0].baseScale.Value.x == 2 && fixtureCopy.parts[1].metadata == null,
                "Ready metadata fixture lost known frame or unknown-size marker");
            var fixtureRoundtrip = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(fixtureCopy))));
            Require(fixtureRoundtrip.parts[0].position.x == 0 && fixtureRoundtrip.parts[1].position.x == 6 &&
                fixtureRoundtrip.parts[0].metadata.hostId == "synthetic-fixture",
                "Ready metadata fixture changed positions or misrepresented provenance");
            var replacement = ExternalBlueprintLibrary.LoadFile(Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(fixture), "../replacement/14-replace-missing.buildworks.json"))).Single();
            Require(replacement.importError == null, replacement.importError);
            var replacementCopy = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(replacement))));
            Require(replacementCopy.parts.Count == 3 && replacementCopy.primaryPartId == replacementCopy.parts[0].stableId &&
                replacementCopy.parts[0].metadata.hostId == "synthetic-fixture" &&
                replacementCopy.parts.Skip(1).All(part => part.prefabName == "woodwall") &&
                replacementCopy.parts[2].position.x == 2, "Ready replacement fixture lost its target or own walls");
            var unknown = ExternalBlueprintLibrary.LoadFile(Path.Combine(Path.GetDirectoryName(fixture),
                "19-unknown-preview.buildworks.json")).Single();
            Require(unknown.importError == null, unknown.importError);
            var unknownCopy = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(unknown))));
            Require(unknownCopy.parts.Count == 3 && unknownCopy.parts[0].metadata == null &&
                unknownCopy.parts[0].position.z == 3 && unknownCopy.parts.Skip(1).All(part => part.prefabName == "woodwall"),
                "Ready unknown-size preview fixture lost available walls or invented metadata");
            var tree = ExternalBlueprintLibrary.LoadFile(Path.Combine(Path.GetDirectoryName(fixture),
                "20-tree-filters.buildworks.json")).Single();
            Require(tree.importError == null, tree.importError);
            var treeCopy = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(tree))));
            string innerId = treeCopy.groups.Single(group => group.name == "QA room").stableId;
            Require(treeCopy.parts.Count == 3 && treeCopy.groups.Count == 3 &&
                treeCopy.parts[0].parentGroupId == innerId && treeCopy.parts[1].parentGroupId == innerId &&
                treeCopy.parts[2].parentGroupId == treeCopy.groups.Single(group => group.name == "QA other").stableId &&
                treeCopy.groups.Single(group => group.stableId == innerId).parentGroupId ==
                    treeCopy.groups.Single(group => group.name == "QA assembly").stableId &&
                treeCopy.groups.All(group => tree.groups.All(source => source.stableId != group.stableId)) &&
                treeCopy.parts[0].metadata.hostId == "synthetic-fixture" && treeCopy.parts[0].metadata.sourceId == null,
                "Ready tree filter fixture lost group paths or invented missing provenance");
            var room = ExternalBlueprintLibrary.LoadFile(Path.Combine(Path.GetDirectoryName(fixture),
                "21-camera-room.buildworks.json")).Single();
            Require(room.importError == null, room.importError);
            var roomCopy = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(
                BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(room))));
            Require(roomCopy.parts.Count == 6 && roomCopy.groups.Count == 1 &&
                roomCopy.parts.All(part => part.parentGroupId == roomCopy.groups[0].stableId) &&
                roomCopy.parts.Count(part => part.prefabName == "woodwall") == 4 &&
                roomCopy.parts.Count(part => part.prefabName == "wood_floor") == 2 &&
                roomCopy.parts.Single(part => part.displayName == "QA ceiling").position.y == 2,
                "Ready interior camera fixture lost its room records or parent");
            Console.WriteLine("PASS metadata native19/portable9: capture/save/update/import/export/reopen, unique snapshots/fresh host, missing retain, immutable copies/Undo, strict versions/IDs/fields/bounds, legacy8 and ready fixtures20/21");
        }

        private static void Geometry(BlueprintPrefabMetadata metadata)
        {
            var part = new BlueprintEditorPart("a", "woodwall", "Wall", new Point3(0, 0, 0), new Rotation3(0, 0, 0, 1), metadata: metadata);
            var document = new BlueprintEditorDocument("doc", "Metadata", "OTHER", new[] { part });
            document.SelectOnly("a");
            Require(document.DuplicateSelection(new Point3(1, 0, 0)) && document.Parts.All(value => ReferenceEquals(value.Metadata, metadata)) &&
                document.Undo() && document.Redo() && document.Parts.Count == 2, "Document duplicate/history lost metadata");
            Refuse(() => new BlueprintEditorPart("b", "other", "Other", new Point3(), new Rotation3(0, 0, 0, 1), metadata: metadata));
            Refuse(() => new BlueprintPrefabMetadata(1, "id", "Name", "Piece", "host", new Point3(1, 1, 1), new Point3(1, 0, 0), new Point3(0, 1, 1)));
            Refuse(() => new BlueprintPrefabMetadata(1, "id", "Name", "Piece", "host", new Point3(1, 1, 1), new Point3(double.NaN, 0, 0), new Point3(1, 1, 1)));
        }
        private static CompositeBlueprintStore.Part Part(string id) => new CompositeBlueprintStore.Part {
            stableId = id, prefabName = "woodwall", displayName = "Wall", rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } };
        private static void Refuse(Action action)
        { bool refused = false; try { action(); } catch (ArgumentException) { refused = true; } catch (InvalidDataException) { refused = true; } Require(refused, "Invalid metadata accepted"); }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
