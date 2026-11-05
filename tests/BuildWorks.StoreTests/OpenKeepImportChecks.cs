using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class OpenKeepImportChecks
    {
        internal static void Run()
        {
            string file = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "openkeep", "hut_2x2.json");
            byte[] bytes = File.ReadAllBytes(file);
            using (var hash = SHA256.Create())
                Require(BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "") ==
                    "BE692809F8B5CF31FC8FF85FADDC345A6C380ED4718139FEC3F8B84EEF5138D0", "Real author fixture bytes changed");
            var loaded = ExternalBlueprintLibrary.LoadFile(file).ToArray();
            Require(loaded.Length == 1, "Real OpenKeep/DevBridge JSON is not discovered by the current library: " + loaded.Length);
            var source = loaded[0];
            Require(source.importError == null && source.parts.Count == 28,
                "Real OpenKeep/DevBridge28-piece JSON is not imported: " + source.importError);
            Require(source.sourceDocuments.Single().ToGeometry().OpenKeep.LevelHalf == 2.5, "Real author site changed");
            Require(source.sourceDocuments.Single().name == "hut_2x2.json" &&
                source.sourceDocuments.Single().text == Encoding.UTF8.GetString(bytes) &&
                File.ReadAllBytes(file).SequenceEqual(bytes), "Original JSON/name changed");
            Require(source.parts.Select((p, i) => p.sourceLine == 0 && p.sourceRecord == i + 1 && !p.scaleIsAbsolute).All(x => x),
                "JSON ordinals became physical lines or absolute unit scales");
            var scale = source.parts[0].RootScale(new UnityEngine.Vector3(2, .5f, 3));
            Require(scale.x == 2 && scale.y == .5f && scale.z == 3, "Inherited prefab basis lost");
            var oldRoof = ExternalBlueprintLibrary.Parse("{\"pieces\":[[\"wood_wall_roof\",0,0,0,0]]}", "roof.json");
            var migrated = ExternalBlueprintLibrary.MigrateLegacyRoof(oldRoof, true);
            Require(migrated.parts.Single().prefabName == BlueprintPrefabMigration.CurrentRoof &&
                migrated.parts.Single().sourceRecord == 1 && migrated.parts.Single().originalPrefabName == BlueprintPrefabMigration.OldRoof &&
                CompositeBlueprintStore.ValidSources(migrated.parts, migrated.sourceDocuments), "Approved roof migration lost JSON origin");
            string directory = Path.Combine(Path.GetTempPath(), "BuildWorks.OpenKeep." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            try { RoundTrip(source, directory); MixedArchives(source, directory); }
            finally { Directory.Delete(directory, true); }
            Console.WriteLine("PASS real OpenKeep author import,ordinals,relative scale,editor history,native30/portable18 and legacy field refusal; full2.12 writer/game acceptance is separate");
        }

        internal static void RoundTripFile(string file, string directory)
        {
            file = Path.GetFullPath(file); directory = Path.GetFullPath(directory);
            byte[] original = File.ReadAllBytes(file);
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(source.importError == null && source.parts.Count == 4, "Expected four-record QA input: " + source.importError);
            var model = source.sourceDocuments.Single().ToGeometry().OpenKeep;
            Require(model.Relief != null && model.Relief.Offsets.Count > 0, "Expected real or explicitly synthetic relief input");
            Require(!Directory.Exists(directory) && !File.Exists(directory), "Output must be a new artifact directory");
            Directory.CreateDirectory(directory);
            RoundTrip(source, directory);
            var saved = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            CorpusRoundTripChecks.RunSaved(Path.Combine(directory, "library.json"), saved.id);
            File.WriteAllBytes(Path.Combine(directory, "portable.buildworks.json"), BlueprintExchangeFormat.Encode(saved));
            var review = OpenKeepExport.Review(saved, OpenKeepExport.Consumer.OpenKeep, _ => null);
            Require(review.TryWrite(saved, _ => null, directory, "reexport", out string output, out string error), error);
            var restored = ExternalBlueprintLibrary.LoadFile(Path.Combine(output, "reexport.json")).Single();
            var relief = restored.sourceDocuments.Single().ToGeometry().OpenKeep.Relief;
            Require(restored.parts.Count == 4 && relief.Width == model.Relief.Width && relief.Depth == model.Relief.Depth &&
                relief.X == model.Relief.X && relief.Z == model.Relief.Z && relief.Offsets.SequenceEqual(model.Relief.Offsets),
                "Known relief grid changed during native/portable/re-export");
            Require(File.ReadAllBytes(file).SequenceEqual(original), "Input original bytes changed");
            Console.WriteLine("PASS four-record file: typed relief/source/records/editor history/native30/portable18/full-field reimport/re-export; provenance must be checked separately");
        }

        private static void RoundTrip(CompositeBlueprintStore.Blueprint source, string directory)
        {
            var copy = ExternalBlueprintLibrary.DetachedCopy(source);
            Require(CompositeBlueprintStore.ValidSources(copy.parts, copy.sourceDocuments), "Copied source address invalid");
            var parts = copy.parts.Select(p => new BlueprintEditorPart(p.stableId, p.prefabName, p.displayName,
                new Point3(p.position.x, p.position.y, p.position.z), new Rotation3(p.rotation.x, p.rotation.y, p.rotation.z, p.rotation.w),
                sourceDocumentId: p.sourceDocumentId, sourceRecord: p.sourceRecord)).ToArray();
            var editor = new BlueprintEditorDocument(copy.id, copy.name, copy.category, parts,
                sourceDocuments: copy.sourceDocuments.Select(s => s.ToGeometry()));
            Require(editor.CopyParts().Select((p, i) => p.SourceRecord == i + 1).All(x => x), "Editor copy lost record");
            editor.SelectOnly(parts[0].StableId);
            Require(editor.DuplicateSelection(new Point3(2, 0, 0)) && editor.Parts.Last().SourceRecord == 1 &&
                editor.Undo() && editor.Parts.Count == source.parts.Count && editor.Redo() && editor.Parts.Last().SourceRecord == 1, "Origin lost through duplicate/Undo/Redo");
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySaveDocument(copy.name, copy.category, copy.parts, copy.groups, copy.anchors,
                null, null, copy.supports, null, out var saved, out string error, sourceDocuments: copy.sourceDocuments), error);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            string jsonPath = Path.Combine(directory, "hut.json");
            File.WriteAllText(jsonPath, source.sourceDocuments.Single().text);
            string[] browsed = CompositeBlueprintStore.BrowseExchangeDirectory(directory);
            Require(browsed.Contains(jsonPath) && !browsed.Contains(path), "Browser hid JSON construction or exposed internal native library");
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            var decoded = BlueprintExchangeFormat.Decode(portable);
            Require(decoded.sourceDocuments.Single().text == source.sourceDocuments.Single().text &&
                decoded.sourceDocuments.Single().ToGeometry().OpenKeep.LevelHalf == source.sourceDocuments.Single().ToGeometry().OpenKeep.LevelHalf &&
                decoded.parts.Select((p, i) => p.sourceRecord == i + 1 && p.sourceLine == 0).All(x => x), "Full JSON evidence/site/address round trip lost");
            var changed = CompositeBlueprintStore.CloneParts(saved.parts);
            foreach (var part in changed) { part.sourceDocumentId = null; part.sourceRecord = 0; }
            Require(store.TryUpdateParts(saved, changed, saved.anchors, out error) && saved.parts[0].sourceRecord == 1, "Compatibility update lost record");
            foreach (int record in new[] { -1, 0, source.parts.Count + 1 })
            {
                var invalid = CompositeBlueprintStore.CloneParts(saved.parts); invalid[0].sourceRecord = record;
                Require(!CompositeBlueprintStore.ValidSources(invalid, saved.sourceDocuments), "Bad ordinal accepted");
            }
            var wrong = CompositeBlueprintStore.CloneParts(saved.parts); wrong[0].sourceLine = 1;
            Require(!CompositeBlueprintStore.ValidSources(wrong, saved.sourceDocuments), "Line+record accepted");
            wrong = CompositeBlueprintStore.CloneParts(saved.parts); wrong[0].prefabName = "other";
            Require(!CompositeBlueprintStore.ValidSources(wrong, saved.sourceDocuments), "Wrong source prefab accepted");
            foreach (string old in new[] { portable.Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":17") })
            {
                bool refused = false; try { BlueprintExchangeFormat.Decode(old); } catch (InvalidDataException) { refused = true; }
                Require(refused, "New sourceRecord smuggled under portable17");
            }
            const string zero = "{\"blueprints\":[{\"parts\":[{\"sourceRecord\":0}]}]}";
            void Structure(bool allow) => BlueprintExchangeFormat.ValidateLibraryJson(zero, requireFields: false,
                allowSupportFields: true, allowSourceFields: true, allowForeignFields: true, allowContentsFields: true,
                allowAbsoluteScaleFields: true, allowPlacementImportFields: true, allowBaseScaleFields: true, allowSourceRecordFields: allow);
            Structure(true);
            bool zeroRefused = false; try { Structure(false); } catch (InvalidDataException) { zeroRefused = true; }
            Require(zeroRefused, "Presence of zero record bypassed old schema field gate");
            var orphan = decoded.ImportCopy();
            foreach (var part in orphan.parts) { part.sourceDocumentId = null; part.sourceRecord = 0; }
            string orphanPortable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(orphan));
            bool orphanRefused = false;
            try { BlueprintExchangeFormat.Decode(orphanPortable.Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":17")); }
            catch (InvalidDataException) { orphanRefused = true; }
            Require(orphanRefused && !orphanPortable.Contains("\"sourceRecord\""), "Unbound JSON source bypassed old format guard");
            string oldNative = File.ReadAllText(path).Replace("\"version\":" + CompositeBlueprintStore.FormatVersion, "\"version\":29");
            string legacyPath = Path.Combine(directory, "legacy-library.json");
            File.WriteAllText(legacyPath, oldNative);
            var blocked = new CompositeBlueprintStore(legacyPath);
            Require(blocked.All().Count == 0 && !blocked.TrySave(saved.parts, saved.anchors, out _, out _) &&
                File.ReadAllText(legacyPath) == oldNative, "Legacy native version admitted/overwrote record field");
        }

        internal static void Corpus(string directory)
        {
            int paths = 0, parts = 0;
            foreach (string path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                byte[] before = File.ReadAllBytes(path);
                var document = ExternalBlueprintLibrary.LoadFile(path).Single();
                Require(document.importError == null, path + ": " + document.importError);
                var evidence = document.sourceDocuments.Single().ToGeometry();
                Require(evidence.Format == OpenKeepBlueprint.Format && evidence.Text == new UTF8Encoding(false, true).GetString(before) &&
                    document.parts.Count == evidence.OpenKeep.Pieces.Count && CompositeBlueprintStore.ValidSources(document.parts, document.sourceDocuments) &&
                    File.ReadAllBytes(path).SequenceEqual(before), "Actual JSON import/source/bytes mismatch");
                Require(document.parts.Select((p, i) => p.sourceRecord == i + 1 && p.sourceLine == 0 && !p.scaleIsAbsolute).All(x => x), "Actual JSON address or scale mismatch");
                ++paths; parts += document.parts.Count;
            }
            Require(paths == 23 && parts == 17975, "Actual library corpus count changed");
            Console.WriteLine("PASS actual OpenKeep library import23paths/17975parts;exact sources,record binding,relative scale,no input mutation;not interactive-budget or native admission proof");
        }

        private static void MixedArchives(CompositeBlueprintStore.Blueprint source, string directory)
        {
            string text = source.sourceDocuments.Single().text, mixed = Path.Combine(directory, "mixed.zip");
            using (var zip = ZipFile.Open(mixed, ZipArchiveMode.Create))
            {
                void Entry(string name, string data) { using (var writer = new StreamWriter(zip.CreateEntry(name).Open())) writer.Write(data); }
                Entry("hut.json", text);
                Entry("legacy.blueprint", "#Pieces\nwood_floor;;0;0;0;0;0;0;1;\"\"\n");
                Entry("manifest.json", "{\"name\":\"package\",\"version_number\":\"1.0\"}");
                Entry("config.json", "{\"enabled\":true}");
                Entry("library.json", File.ReadAllText(Path.Combine(directory, "library.json")));
            }
            byte[] before = File.ReadAllBytes(mixed);
            var documents = ExternalBlueprintLibrary.LoadFile(mixed);
            Require(documents.Count == 2 && documents.All(d => d.importError == null) && documents.Sum(d => d.parts.Count) == 29 &&
                File.ReadAllBytes(mixed).SequenceEqual(before), "Valid metadata/internal JSON broke mixed ZIP import");
            foreach (string invalid in new[] { "{\"pieces\":[[\"wood_floor\",\"bad\",0,0,0]]}", "{\"pieces\":[" })
            {
                string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".zip");
                using (var zip = ZipFile.Open(path, ZipArchiveMode.Create))
                using (var writer = new StreamWriter(zip.CreateEntry("broken.json").Open())) writer.Write(invalid);
                var broken = ExternalBlueprintLibrary.LoadFile(path);
                Require(broken.Count == 1 && broken[0].importError != null && broken[0].parts.Count == 0, "Recognized malformed JSON silently skipped in ZIP");
            }
            string hut = Path.Combine(directory, "hut.json");
            long budget = new FileInfo(hut).Length - 1, originalBudget = budget;
            bool limited = false;
            try { ExternalBlueprintLibrary.BrowsableFile(hut, ref budget); }
            catch (InvalidDataException error) { limited = error.Message == "$buildworks_import.file_limit"; }
            Require(limited && budget == originalBudget, "JSON budget exhaustion silently hid later construction");
            Require(ExternalBlueprintLibrary.LoadFile(Path.Combine(directory, "library.json")).Count == 0,
                "Internal native library misidentified as OpenKeep JSON");
            Console.WriteLine("PASS mixed ZIP profile discrimination,unchanged archive,malformed known JSON retained as refusal,explicit browser budget failure");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
