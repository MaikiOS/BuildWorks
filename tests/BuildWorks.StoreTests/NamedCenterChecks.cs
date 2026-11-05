using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class NamedCenterChecks
    {
        private const string Text = "#Name:Frame\n#Center:wood_floor\n#SnapPoints\n3;1;0\n#Pieces\n" +
            "wood_floor;;2;1;0;0;.707;0;.707;\"\";2;.5;1.25\nwood_wall;;5;2;4;0;0;0;1;\"\";1;2;3\n";

        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var raw = Parse(Text);
            CheckPose(raw);
            Near(raw.customSnapPoints.Single(), new Point3(0, 0, 1));
            Require(raw.parts.All(part => part.sourceFrameApplied) &&
                !CompositeBlueprintStore.UnresolvedSources(raw).Any(), "Known frame remained unresolved");
            Require(raw.parts[0].scale.x == 2 && raw.parts[0].scale.y == .5f && raw.parts[1].scale.z == 3 &&
                raw.parts[0].sourceLine == 6 && raw.sourceDocuments.Single().text == Text,
                "Frame conversion changed scale, source row or raw evidence");
            CheckPose(Parse(Text.Replace("0;.707;0;.707", "0;-.707;0;-.707")));
            var duplicate = Parse(Text.Replace("wood_wall;", "wood_floor;"));
            Near(duplicate.parts[1].position, new Point3(-4, 1, 3));
            var absent = Parse(Text.Replace("#Center:wood_floor", "#Center:missing"));
            Near(absent.parts[0].position, new Point3(-.5, 0, -2));
            Near(absent.parts[1].position, new Point3(2.5, 1, 2));
            foreach (string withoutFrame in new[] { Text.Replace("#Center:wood_floor", "#Center:"), Text.Replace("#Center:wood_floor\n", "") })
            {
                var unchanged = Parse(withoutFrame);
                Near(unchanged.parts[0].position, new Point3(2, 1, 0));
                Require(unchanged.parts.All(part => !part.sourceFrameApplied), "Generic empty/default frame was guessed");
            }
            foreach (string invalid in new[] {
                Text.Replace("#Center:wood_floor", "#Center:wood_floor\n#Center:wood_wall"),
                Text.Replace("#Center:wood_floor", "#Center:wood_floor\n#Center"),
                Text.Replace("#Center:wood_floor\n", "") + "#Center:wood_floor\n",
                Text.Replace("#Center:wood_floor", "#Center:bad/name") })
            {
                var retained = Parse(invalid);
                Require(retained.parts.All(part => !part.sourceFrameApplied) && CompositeBlueprintStore.UnresolvedSources(retained).Any(),
                    "Invalid/late/duplicate frame was consumed");
            }
            var malformed = ExternalBlueprintLibrary.Parse(Text.Replace("wood_wall;;5;2;4", "wood_wall;;NaN;2;4"), "frame.blueprint");
            Require(malformed.importError != null && malformed.parts.Count == 0, "Bad later row partially normalized a frame");
            var overflow = ExternalBlueprintLibrary.Parse(Text.Replace("wood_floor;;2;1;0", "wood_floor;;-512;1;0")
                .Replace("wood_wall;;5;2;4", "wood_wall;;512;2;4"), "frame.blueprint");
            Require(overflow.importError != null && overflow.parts.Count == 0, "Out-of-range rebase partially imported");
            var pending = ExternalBlueprintLibrary.PrepareForHost(raw, _ => null);
            Require(pending.parts.All(part => part.sourceFrameApplied && part.scaleIsAbsolute && !part.baseScale.HasValue),
                "Frame consumption invented a missing prefab base");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(pending, Basis);
            CheckPose(prepared);
            Require(ReferenceEquals(prepared, ExternalBlueprintLibrary.PrepareForHost(prepared, Basis)), "Frame/base applied twice");
            var saved = CheckPersistence(prepared, directory);
            CheckVersions(saved, directory);
            CheckGeometry(prepared);
            Console.WriteLine("PASS named-center frame: matching/first/fallback/q-signs/snaps/raw/scale; native30/portable18; legacy/mixed flags fail closed; copy/edit/history retain consumed state");
        }

        internal static void RunAuthor(string text, string sourceName, string directory)
        {
            var source = ExternalBlueprintLibrary.Parse(text, sourceName);
            Require(source.importError == null && source.parts.Count == 2, "Author I02 failed parse");
            CheckPose(source);
            Require(source.parts.All(part => part.sourceFrameApplied) && CompositeBlueprintStore.UnresolvedSources(source).Any() &&
                source.sourceDocuments.Single().text == text && text.Contains("#TerrainHeight:5,7,10;30;1"),
                "Author I02 terrain/source was rewritten or admitted");
            CheckPersistence(ExternalBlueprintLibrary.PrepareForHost(source, Basis), directory);
            Console.WriteLine("PASS exact I02: floor origin/identity, wall(-4,1,3)/yaw-90, consumed frame/native/portable exact; terrain yaw30/raw/refusal retained");
        }

        private static CompositeBlueprintStore.Blueprint CheckPersistence(CompositeBlueprintStore.Blueprint source, string directory)
        {
            Directory.CreateDirectory(directory);
            var detached = ExternalBlueprintLibrary.DetachedCopy(source);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "frame.json"));
            Require(store.TrySaveDocument(detached.name, detached.category, detached.parts, detached.groups, detached.anchors,
                detached.primaryPartId, detached.primaryGroupId, detached.supports, detached.placementSupportId,
                out var saved, out string error, detached.customSnapPoints, sourceDocuments: detached.sourceDocuments), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "frame.json")).All().Single();
            var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            var reimport = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(portable)), "frame.buildworks.json");
            foreach (var copy in new[] { detached, saved, reopened, portable, reimport })
            {
                CheckPose(copy);
                Require(copy.parts.All(part => part.sourceFrameApplied) && copy.sourceDocuments[0].text == source.sourceDocuments[0].text &&
                    CompositeBlueprintStore.UnresolvedSources(copy).Any() == CompositeBlueprintStore.UnresolvedSources(source).Any(),
                    "Copy/native/portable/reimport lost frame consumption or raw/terrain blocker");
            }
            var mixed = CompositeBlueprintStore.CopyWithContentsPolicy(portable, false, false, null);
            mixed.parts[1].sourceFrameApplied = false;
            Require(CompositeBlueprintStore.UnresolvedSources(mixed).Any(), "One true flag consumed a shared mixed source");
            Require(CompositeBlueprintStore.CopyWithContentsPolicy(mixed, false, false, null).parts[0].sourceFrameApplied,
                "Contents copy lost frame consumption");
            return saved;
        }

        private static void CheckVersions(CompositeBlueprintStore.Blueprint source, string directory)
        {
            var prepared = ExternalBlueprintLibrary.PrepareForHost(Parse(Text.Replace("#Center:wood_floor\n", "")), Basis);
            var evidence = CompositeBlueprintStore.SourceDocument.FromGeometry(new BlueprintSourceDocument("blueprint", "frame.blueprint", Text));
            prepared.sourceDocuments.Clear(); prepared.sourceDocuments.Add(evidence);
            foreach (var part in prepared.parts) { part.sourceDocumentId = evidence.id; ++part.sourceLine; }
            var store = new CompositeBlueprintStore(Path.Combine(directory, "old.json"));
            Require(store.TryGetOrCreatePlacementCopy(prepared, out _, out string error), error);
            string native = File.ReadAllText(Path.Combine(directory, "old.json")).Replace("\"version\":" + CompositeBlueprintStore.FormatVersion, "\"version\":28");
            File.WriteAllText(Path.Combine(directory, "old.json"), native);
            var old = new CompositeBlueprintStore(Path.Combine(directory, "old.json")).All().Single();
            Near(old.parts[0].position, new Point3(2, 1, 0));
            Require(old.parts.All(part => !part.sourceFrameApplied) && CompositeBlueprintStore.UnresolvedSources(old).Any(),
                "Old native archive was silently normalized/admitted");
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(old)).Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":16");
            Require(CompositeBlueprintStore.UnresolvedSources(BlueprintExchangeFormat.Decode(portable)).Any(), "Old portable frame became known");
            foreach (string value in new[] { "true", "false" })
            {
                string bad = portable.Replace("\"position\":{", "\"sourceFrameApplied\":" + value + ",\"position\":{");
                Reject(() => BlueprintExchangeFormat.Decode(bad), "Old portable accepted the new field");
                string path = Path.Combine(directory, "old-" + value + ".json");
                File.WriteAllText(path, native.Replace("\"position\":{", "\"sourceFrameApplied\":" + value + ",\"position\":{"));
                Require(new CompositeBlueprintStore(path).All().Count == 0, "Old native accepted the new field");
            }
            string current = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(source));
            foreach (string badValue in new[] { "null", "0", "\"true\"", "[]", "{}" })
                Reject(() => BlueprintExchangeFormat.Decode(current.Replace("\"sourceFrameApplied\":true", "\"sourceFrameApplied\":" + badValue)),
                    "Malformed frame marker accepted");
            Reject(() => BlueprintExchangeFormat.Decode(current.Replace("\"sourceFrameApplied\":true", "\"sourceFrameApplied\":true,\"sourceFrameApplied\":true")),
                "Duplicate frame marker accepted");
            var unbound = CompositeBlueprintStore.CopyWithContentsPolicy(source, false, false, null);
            unbound.parts[0].sourceDocumentId = null; unbound.parts[0].sourceLine = 0;
            Reject(() => BlueprintExchangeFormat.Encode(unbound), "Unbound frame marker accepted");
            var unnamed = Parse(Text.Replace("#Center:wood_floor", "#Center:"));
            unnamed.parts[0].sourceFrameApplied = true;
            Require(!CompositeBlueprintStore.ValidSources(unnamed.parts, unnamed.sourceDocuments), "Marker without a named frame accepted");
        }

        private static void CheckGeometry(CompositeBlueprintStore.Blueprint source)
        {
            var parts = source.parts.Select(part => new BlueprintEditorPart(part.stableId, part.prefabName, part.displayName,
                Point(part.position), new Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w),
                scale: Point(part.scale), sourceDocumentId: part.sourceDocumentId, sourceLine: part.sourceLine,
                baseScale: Point(part.baseScale.Value), sourceFrameApplied: part.sourceFrameApplied));
            var document = new BlueprintEditorDocument(null, "Frame", "Other", parts,
                sourceDocuments: source.sourceDocuments.Select(item => item.ToGeometry()));
            document.SelectOnly(document.Parts[0].StableId);
            Require(document.DuplicateSelection(new Point3(10, 0, 0)) && document.Parts.All(part => part.SourceFrameApplied), "Duplicate lost frame marker");
            Require(document.Undo() && document.Redo() && document.Parts.All(part => part.SourceFrameApplied), "History lost frame marker");
            var target = new BlueprintEditorDocument(null, "Target", "Other", new BlueprintEditorPart[0]);
            Require(target.InsertBlueprint(document, new Point3(1, 2, 3), new Rotation3(0, 0, 0, 1)) &&
                target.Parts.All(part => part.SourceFrameApplied), "Insertion lost frame marker");
            document.Undo();
            Require(document.ApplyArray(2, 1, new Point3(1, 0, 0), new Point3(0, 0, 1), new Point3(0, 1, 0), 0, 0, false, 0,
                new Point3(-.5, 0, 0), new Point3(.5, 0, 0)) && document.Parts.All(part => part.SourceFrameApplied), "Array lost frame marker");
            document.Undo();
            document.AddPart(new BlueprintEditorPart("left", "support", "Left", new Point3(-2, 0, 0), new Rotation3(0, 0, 0, 1)));
            document.AddPart(new BlueprintEditorPart("right", "support", "Right", new Point3(2, 0, 0), new Rotation3(0, 0, 0, 1)));
            document.SelectOnly(document.Parts[0].StableId);
            Require(document.ApplyContour(new[] { "left", "right" }, false, 0) &&
                document.Parts.Where(part => part.SourceDocumentId != null).All(part => part.SourceFrameApplied), "Contour lost frame marker");
        }

        private static CompositeBlueprintStore.Blueprint Parse(string text)
        {
            var result = ExternalBlueprintLibrary.Parse(text, "frame.blueprint");
            Require(result.importError == null, result.importError); return result;
        }
        private static CompositeBlueprintStore.VectorData? Basis(string _) => new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 };
        private static Point3 Point(CompositeBlueprintStore.VectorData value) => new Point3(value.x, value.y, value.z);
        private static void CheckPose(CompositeBlueprintStore.Blueprint source)
        {
            Near(source.parts[0].position, new Point3()); Near(source.parts[1].position, new Point3(-4, 1, 3));
            var first = source.parts[0].rotation; var second = source.parts[1].rotation;
            Require((new Rotation3(first.x, first.y, first.z, first.w).Rotate(new Point3(1, 0, 0)) - new Point3(1, 0, 0)).LengthSquared < 1e-10 &&
                (new Rotation3(second.x, second.y, second.z, second.w).Rotate(new Point3(1, 0, 0)) - new Point3(0, 0, 1)).LengthSquared < 1e-10,
                "Named-center rotations disagree with the independent frame");
        }
        private static void Near(CompositeBlueprintStore.VectorData actual, Point3 expected) =>
            Require((Point(actual) - expected).LengthSquared < 1e-10, "Named-center position disagrees with the independent frame");
        private static void Reject(Action action, string message)
        {
            bool rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; } catch (ArgumentException) { rejected = true; }
            Require(rejected, message);
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
