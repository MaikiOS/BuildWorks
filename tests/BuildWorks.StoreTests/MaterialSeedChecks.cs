using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class MaterialSeedChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            var part = new CompositeBlueprintStore.Part { stableId = "seed", prefabName = "woodwall",
                displayName = "Wall", rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } };
            Require(store.TrySave("Material", new[] { part }, Array.Empty<CompositeBlueprintStore.VectorData>(), out var saved, out string error), error);
            string json = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            string seeded = json.Replace("\"stableId\":\"seed\"", "\"stableId\":\"seed\",\"materialSeed\":0");
            var decoded = BlueprintExchangeFormat.Decode(seeded);
            Require(decoded.parts.Single().materialSeed == 0, "Zero appearance seed was lost");
            Require(!json.Contains("materialSeed"), "An unspecified appearance seed was invented");
            foreach (int seed in new[] { 0, 1, 12344, int.MaxValue })
            {
                part.materialSeed = seed;
                Require(store.TryUpdateDocument(saved, new[] { part }, saved.groups, saved.anchors, out error), error);
                Require(saved.parts.Single().materialSeed == seed, "Document update lost its seed");
                Require(new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single().parts.Single().materialSeed == seed,
                    "Native reopen lost its seed");
                var copy = CompositeBlueprintStore.CloneParts(saved.parts);
                copy.Single().materialSeed = null;
                Require(saved.parts.Single().materialSeed == seed, "Part clone aliased the original");
                var portable = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)), "seed.buildworks.json");
                Require(ExternalBlueprintLibrary.DetachedCopy(portable).parts.Single().materialSeed == seed,
                    "Portable/detached copy lost its seed");
                Require(CompositeBlueprintStore.HasForeignData(saved) && CompositeBlueprintStore.InspectContents(saved, null).Unknown == 1,
                    "An unapplied appearance recipe was reported ready for construction");
                Require(store.TryExportFile(saved, directory, "seed-" + seed, true, out string path, out error), error);
                Require(ExternalBlueprintLibrary.LoadFile(path).Single().parts.Single().materialSeed == seed, "ZIP lost its seed");
                Geometry(seed);
            }
            string native = File.ReadAllText(Path.Combine(directory, "library.json"));
            string current = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved));
            Require(BlueprintExchangeFormat.Decode(current.Replace("\"materialSeed\":2147483647", "\"materialSeed\":null")).parts.Single().materialSeed == null,
                "Explicit null seed was invented");
            foreach (string invalid in new[] { "-1", "2147483648", "1.5", "\"1\"", "true", "{}", "[]" })
                Refuse(() => BlueprintExchangeFormat.Decode(current.Replace("\"materialSeed\":2147483647", "\"materialSeed\":" + invalid)));
            Refuse(() => BlueprintExchangeFormat.Decode(current.Replace("\"version\":14", "\"version\":9")));
            Refuse(() => BlueprintExchangeFormat.Decode(current.Replace("\"materialSeed\":2147483647", "\"materialSeed\":0,\"materialSeed\":1")));
            Refuse(() => BlueprintExchangeFormat.Decode(current.Replace("\"materialSeed\":2147483647", "\"materialSeed\":0,\"futureAppearance\":1")));
            part.materialSeed = -1;
            Require(!store.TryUpdateDocument(saved, new[] { part }, saved.groups, saved.anchors, out error) &&
                File.ReadAllText(Path.Combine(directory, "library.json")) == native, "Invalid seed changed the native file");
            string oldPath = Path.Combine(directory, "old.json");
            File.WriteAllText(oldPath, native.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":25", "\"version\":19"));
            var oldStore = new CompositeBlueprintStore(oldPath);
            Require(oldStore.All().Count == 0 && !oldStore.TrySave("Invalid", saved.parts, saved.anchors, out _, out _) &&
                File.ReadAllText(oldPath) == native.Replace(",\"hiddenSources\":[]", "").Replace("\"version\":25", "\"version\":19"), "Old schema accepted or rewrote an appearance field");
            part.materialSeed = null;
            Require(store.TryUpdateParts(saved, new[] { part }, saved.anchors, out error) && saved.parts.Single().materialSeed == int.MaxValue,
                "Legacy part-only update lost its retained appearance recipe");
            part.foreignData = new CompositeBlueprintStore.ForeignPartData {
                format = BlueprintForeignPartData.NativeItemStandFormat, info = "SwordIron:2:3:0",
                zdo = Convert.ToBase64String(new byte[] { 109, 2, 0, 255, 67 }) };
            part.materialSeed = 37;
            Require(store.TryUpdateDocument(saved, new[] { part }, saved.groups, saved.anchors, out error), error);
            var mounted = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved))).parts.Single();
            Require(mounted.materialSeed == 37 && mounted.foreignData.info == "SwordIron:2:3:0" && mounted.foreignData.zdo == part.foreignData.zdo,
                "Appearance recipe displaced independent mount contents");
            part.foreignData = null;
            part.materialSeed = null;
            Require(store.TryUpdateDocument(saved, new[] { part }, saved.groups, saved.anchors, out error) &&
                !File.ReadAllText(Path.Combine(directory, "library.json")).Contains("materialSeed"), "Explicit document clear did not clear the seed");
            foreach (int version in new[] { 8, 9 })
                Require(BlueprintExchangeFormat.Decode(json.Replace("\"version\":14", "\"version\":" + version)).parts.Single().materialSeed == null,
                    "Legacy portable without an appearance field stopped loading");
            Require(!CompositeBlueprintStore.HasForeignData(saved), "Unspecified seed left an appearance blocker");
            foreach (int version in new[] { 18, 19 })
            {
                string legacy = File.ReadAllText(Path.Combine(directory, "library.json")).Replace(",\"hiddenSources\":[]", "").Replace("\"version\":25", "\"version\":" + version);
                File.WriteAllText(oldPath, legacy);
                Require(new CompositeBlueprintStore(oldPath).All().Single().parts.Single().materialSeed == null && File.ReadAllText(oldPath) == legacy,
                    "Legacy native without appearance changed on read");
            }
            Console.WriteLine("PASS material seed native20/portable10: zero/max/null, clone/detached/ZIP/save/reopen/Undo/array, strict type/range/version/duplicate guards; appearance remains pending");
        }

        private static void Geometry(int seed)
        {
            var part = new BlueprintEditorPart("a", "woodwall", "Wall", new Point3(), new Rotation3(0, 0, 0, 1), materialSeed: seed);
            var document = new BlueprintEditorDocument("doc", "Material", "OTHER", new[] { part });
            document.SelectOnly("a");
            Require(document.DuplicateSelection(new Point3(1, 0, 0)) && document.Parts.All(value => value.MaterialSeed == seed) &&
                document.Undo() && document.Redo() && document.Parts.All(value => value.MaterialSeed == seed), "Duplicate/history lost its seed");
            document.SelectOnly("a");
            var copies = document.PreviewArray(3, 1, new Point3(2, 0, 0), new Point3(0, 0, 2), new Point3(0, 1, 0), 0, 0, false, 0,
                new Point3(-.5, 0, 0), new Point3(.5, 0, 0));
            Require(copies.Count == 2 && copies.All(value => value.MaterialSeed == seed), "Array preview lost its seed");
            Require(document.InsertBlueprint(new BlueprintEditorDocument("insert", "Insert", "OTHER", new[] { part }),
                new Point3(0, 0, 3), new Rotation3(0, 0, 0, 1)) && document.Parts.All(value => value.MaterialSeed == seed),
                "Nested document insertion lost its seed");
            Refuse(() => new BlueprintEditorPart("bad", "woodwall", "Wall", new Point3(), new Rotation3(0, 0, 0, 1), materialSeed: -1));
        }

        private static void Refuse(Action action)
        {
            bool refused = false;
            try { action(); } catch (ArgumentException) { refused = true; } catch (InvalidDataException) { refused = true; }
            Require(refused, "Invalid appearance data accepted");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
