using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class LargeSnapChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            int maximum = BlueprintEditorDocument.MaximumCustomSnapPoints;
            string text = "#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"\n#SnapPoints\n" +
                string.Join("\n", Enumerable.Range(0, maximum).Select(i => (i / 1000f).ToString("R", CultureInfo.InvariantCulture) + ";0;0"));
            var parsed = ExternalBlueprintLibrary.Parse(text, "bounded.blueprint");
            Require(parsed.importError == null && parsed.customSnapPoints.Count == maximum, "Aggregate20000 refused");
            Require(ExternalBlueprintLibrary.Parse(text + "\n0;0;0", "overflow.blueprint").importError != null, "Aggregate20001 accepted");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed, _ => UnitScale);
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            string native = File.ReadAllText(path);
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            Require(native.Contains("\"version\":22") && portable.Contains("\"version\":11"), "Large-list versions missing");
            Compare(saved, new CompositeBlueprintStore(path).All().Single());
            Compare(saved, BlueprintExchangeFormat.Decode(portable));
            var document = Document(saved);
            Require(document.CustomSnapPoints.Count == maximum, "Editor document lost large point set");
            var empty = new BlueprintEditorDocument(null, "Empty", "OTHER");
            Require(empty.InsertBlueprint(document, new Point3(), new Rotation3(0, 0, 0, 1)) &&
                empty.CustomSnapPoints.SequenceEqual(document.CustomSnapPoints), "Large insertion/order changed");
            Require(empty.Undo() && empty.CustomSnapPoints.Count == 0 && empty.Parts.Count == 0 && empty.Redo() &&
                empty.CustomSnapPoints.Count == maximum, "Large insertion Undo/Redo lost points");
            var full = new BlueprintEditorDocument(null, "Full", "OTHER", sourceCustomSnapPoints: new[] { new Point3() });
            int revision = full.RevisionToken;
            bool refused = false;
            try { full.InsertBlueprint(document, new Point3(), new Rotation3(0, 0, 0, 1)); }
            catch (InvalidOperationException) { refused = true; }
            Require(refused && full.Parts.Count == 0 &&
                full.CustomSnapPoints.Count == 1 && full.RevisionToken == revision, "Overflow insertion mutated document");
            refused = false;
            try { new BlueprintEditorDocument(null, "Overflow", "OTHER", sourceCustomSnapPoints:
                document.CustomSnapPoints.Concat(new[] { new Point3() })); }
            catch (InvalidOperationException) { refused = true; }
            Require(refused, "Geometry accepted20001 points");
            var overflow = saved.ImportCopy(); overflow.customSnapPoints.Add(new CompositeBlueprintStore.VectorData());
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, saved.parts, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, out error, overflow.customSnapPoints) && File.ReadAllText(path) == native,
                "Store overflow changed original");
            Refuse(() => BlueprintExchangeFormat.Encode(overflow));
            string extra = portable.Replace("\"customSnapPoints\":[", "\"customSnapPoints\":[{\"x\":0,\"y\":0,\"z\":0},");
            Refuse(() => BlueprintExchangeFormat.Decode(extra));
            string damagedPath = Path.Combine(directory, "overflow-native.json");
            string damaged = native.Replace("\"customSnapPoints\":[", "\"customSnapPoints\":[{\"x\":0,\"y\":0,\"z\":0},");
            File.WriteAllText(damagedPath, damaged);
            Require(!new CompositeBlueprintStore(damagedPath).TryAddCategory("Denied", out error) &&
                File.ReadAllText(damagedPath) == damaged, "Oversized native list was rewritten");
            Refuse(() => BlueprintExchangeFormat.Decode(portable.Replace("\"version\":11", "\"version\":10")));
            string legacyPath = Path.Combine(directory, "legacy-invalid.json");
            string legacy = native.Replace("\"version\":22", "\"version\":21");
            File.WriteAllText(legacyPath, legacy);
            var blocked = new CompositeBlueprintStore(legacyPath);
            Require(!blocked.TryAddCategory("Denied", out error) && File.ReadAllText(legacyPath) == legacy,
                "Legacy tag accepted oversized set or rewrote original");
            var small = saved.ImportCopy(); small.customSnapPoints = small.customSnapPoints.Take(512).ToList();
            Compare(small, BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(small))
                .Replace("\"version\":11", "\"version\":10")));
            string oldPath = Path.Combine(directory, "legacy-valid.json");
            var oldStore = new CompositeBlueprintStore(oldPath);
            Require(oldStore.TrySaveDocument("Legacy", small.category, small.parts, small.groups, small.anchors,
                small.primaryPartId, small.primaryGroupId, small.supports, small.placementSupportId, out _, out error,
                small.customSnapPoints, small, small.sourceDocuments), "Legacy fixture save: " + error);
            File.WriteAllText(oldPath, File.ReadAllText(oldPath).Replace("\"version\":22", "\"version\":21"));
            Require(new CompositeBlueprintStore(oldPath).All().Single().customSnapPoints.Count == 512, "Legacy512 failed migration");
            string anchors = string.Join(",", Enumerable.Repeat("{\"x\":0,\"y\":0,\"z\":0}", 513));
            Refuse(() => BlueprintExchangeFormat.Decode(portable.Replace("\"anchors\":[]", "\"anchors\":[" + anchors + "]")));
            Console.WriteLine("PASS aggregate snap20000/20001 parser/document/insert/Undo/native22/portable11; legacy21/10 bounded migration; anchors512 and atomic refusal retained");
        }

        internal static void RunCorpus(string archivePath, string directory)
        {
            Directory.CreateDirectory(directory);
            string originalHash = Hash(archivePath);
            Require(originalHash == "4A6E6A53F246B5CF4C7EA9472F726D9F35687BCE978F28AB47A020AEE759DD88", "Unexpected corpus bytes");
            string[] names = { "Arani_Forte_Negro.blueprint", "Big_Bridge_1.blueprint", "Decay.blueprint", "Flabellum.blueprint" };
            int[] parts = { 5062, 770, 4070, 6265 }, snaps = { 6596, 2012, 11790, 17125 };
            using (var archive = ZipFile.OpenRead(archivePath))
            for (int i = 0; i < names.Length; ++i)
            {
                var entry = archive.Entries.Single(e => Path.GetFileName(e.FullName) == names[i]);
                Require(entry.Length <= BlueprintSourceDocument.MaximumBytes, "Oversized corpus member");
                string text; using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                var source = ExternalBlueprintLibrary.Parse(text, entry.FullName);
                Require(source.importError == null && source.parts.Count == parts[i] && source.customSnapPoints.Count == snaps[i],
                    names[i] + " parser failed: " + source.importError);
                var expected = text.Split('\n').SkipWhile(line => line.Trim() != "#SnapPoints").Skip(1)
                    .TakeWhile(line => !line.TrimStart().StartsWith("#", StringComparison.Ordinal)).Where(line => !string.IsNullOrWhiteSpace(line))
                    .Select(line => line.Split(';').Select(value => float.Parse(value, CultureInfo.InvariantCulture)).ToArray()).ToArray();
                Require(expected.Length == snaps[i], "Raw point count changed");
                for (int p = 0; p < expected.Length; ++p)
                    Require(source.customSnapPoints[p].x == expected[p][0] && source.customSnapPoints[p].y == expected[p][1] &&
                        source.customSnapPoints[p].z == expected[p][2], "Raw coordinates/order changed");
                // Unit bases are an inert conversion probe, never evidence of actual prefab availability or scale.
                var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => UnitScale);
                Require(prepared.importError == null, "Unit-base probe failed: " + prepared.importError);
                var document = Document(prepared);
                Require(document.Parts.Count == parts[i] && document.CustomSnapPoints.Count == snaps[i], "Document lost rows");
                string path = Path.Combine(directory, names[i] + ".json");
                var store = new CompositeBlueprintStore(path);
                Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                var reopened = new CompositeBlueprintStore(path).All().Single(); Compare(prepared, reopened);
                string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
                Compare(reopened, BlueprintExchangeFormat.Decode(portable));
                var imported = ExternalBlueprintLibrary.Parse(portable, names[i] + ".buildworks.json");
                Require(imported.importError == null, imported.importError); Compare(reopened, imported);
                Require(store.TrySaveDocument("Copy", saved.category, saved.parts, saved.groups, saved.anchors,
                    saved.primaryPartId, saved.primaryGroupId, saved.supports, saved.placementSupportId, out var copy, out error,
                    saved.customSnapPoints, saved, saved.sourceDocuments) && copy.id != saved.id, error);
                Compare(saved, copy);
                Require(new CompositeBlueprintStore(path).All().Count == 2, "SaveAs did not retain both records");
                Console.WriteLine("PASS actual large storage roundtrip " + names[i] + ": " + parts[i] + " parts / " + snaps[i] +
                    " snaps; raw order/source -> document -> native reopen -> portable reimport -> SaveAs; not Valheim build/scale proof");
            }
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                var entry = archive.Entries.Single(e => Path.GetFileName(e.FullName) == "TheDecay CornerStairwell - Apollyon.blueprint");
                string text; using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                var source = ExternalBlueprintLibrary.Parse(text, entry.FullName);
                Require(source.importError == null && source.parts.Count == source.importPartCount, "Legacy sign corpus member refused or lost rows");
                const string literal = "\"3.6 Roentgen. Not Great. Not Terrible\" Achievement";
                Require(source.parts.Single(p => p.sourceLine == 174).signText == literal, "Actual legacy sign changed");
                var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => UnitScale);
                string path = Path.Combine(directory, "legacy-sign.json");
                var store = new CompositeBlueprintStore(path);
                Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                var reopened = new CompositeBlueprintStore(path).All().Single();
                var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
                Compare(source, reopened); Compare(source, portable);
                Require(reopened.parts.Single(p => p.sourceLine == 174).signText == literal &&
                    portable.parts.Single(p => p.sourceLine == 174).signText == literal, "Actual legacy sign lost in roundtrip");
                Console.WriteLine("PASS actual CornerStairwell legacy sign: " + source.parts.Count + " rows / exact sign174 / source / native reopen / portable; inert unit bases, not game proof");
            }
            int total = 0, supported = 0;
            using (var archive = ZipFile.OpenRead(archivePath))
            foreach (var entry in archive.Entries.Where(e => e.FullName.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) ||
                e.FullName.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase)))
            {
                Require(entry.Length <= BlueprintSourceDocument.MaximumBytes, "Oversized corpus entry");
                string text; using (var reader = new StreamReader(entry.Open())) text = reader.ReadToEnd();
                var parsed = ExternalBlueprintLibrary.Parse(text, entry.FullName); ++total;
                if (parsed.importError == null) ++supported;
                else Console.WriteLine("Remaining parser refusal: " + entry.FullName + " " + parsed.importError);
            }
            Require(total == 209 && supported == 202, "Whole corpus regression: " + supported + "/" + total);
            Console.WriteLine("PASS actual209 corpus:202 parsed,7 explicitly refused; legacy leading-quote sign refusal cleared, no new refusal");
            Require(Hash(archivePath) == originalHash, "Corpus archive changed");
        }

        private static BlueprintEditorDocument Document(CompositeBlueprintStore.Blueprint source) => new BlueprintEditorDocument(
            source.id, source.name, source.category, source.parts.Select(p => new BlueprintEditorPart(p.stableId, p.prefabName, p.displayName,
                Point(p.position), new Rotation3(p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w), p.parentGroupId,
                p.editorVisible, p.editorLocked, Point(p.scale), p.signText, p.sourceDocumentId, p.sourceLine, p.foreignData?.ToGeometry(),
                p.scaleIsAbsolute, p.baseScale.HasValue ? Point(p.baseScale.Value) : (Point3?)null, p.metadata?.ToGeometry(), p.materialSeed)),
            source.groups.Select(g => new BlueprintEditorGroup(g.stableId, g.name, g.editorVisible, g.editorLocked, g.parentGroupId, g.pivotPartId)),
            source.primaryPartId, source.primaryGroupId, source.customSnapPoints?.Select(Point), source.supports?.Select(s =>
                new BlueprintEditorSupport(s.stableId, s.name, Point(s.position), new Rotation3(s.rotation.x, s.rotation.y, s.rotation.z, s.rotation.w))),
            source.placementSupportId, source.sourceDocuments?.Select(s => s.ToGeometry()));

        private static Point3 Point(CompositeBlueprintStore.VectorData p) => new Point3(p.x, p.y, p.z);
        private static CompositeBlueprintStore.VectorData UnitScale => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
        private static string Hash(string path)
        { using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", ""); }
        private static void Compare(CompositeBlueprintStore.Blueprint expected, CompositeBlueprintStore.Blueprint actual)
        {
            Require(expected.parts.Count == actual.parts.Count && expected.customSnapPoints.Count == actual.customSnapPoints.Count &&
                expected.customSnapPoints.Select(Point).SequenceEqual(actual.customSnapPoints.Select(Point)), "Roundtrip changed point count/order/coordinates");
            Require(expected.sourceDocuments.Select(s => s.text).SequenceEqual(actual.sourceDocuments.Select(s => s.text)), "Original source changed");
            for (int i = 0; i < expected.parts.Count; ++i)
                Require(expected.parts[i].prefabName == actual.parts[i].prefabName && Point(expected.parts[i].position).Equals(Point(actual.parts[i].position)) &&
                    expected.parts[i].sourceLine == actual.parts[i].sourceLine && expected.parts[i].sourceDocumentId == actual.parts[i].sourceDocumentId,
                    "Roundtrip changed source-bound part");
        }
        private static void Refuse(Action action)
        { bool refused = false; try { action(); } catch (InvalidDataException) { refused = true; } Require(refused, "Invalid exchange accepted"); }
        private static void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    }
}
