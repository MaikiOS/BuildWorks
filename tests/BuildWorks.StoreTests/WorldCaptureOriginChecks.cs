using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class WorldCaptureOriginChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "library.json");
            var store = new CompositeBlueprintStore(path);
            var source = CompositeBlueprintStore.SourceDocument.FromGeometry(BlueprintSourceDocument.CaptureWorld(
                77, new Point3(10000, 4, -2000), new Rotation3(0, 0, 0, 1),
                "11111111222233334444555555555555", "0.19.307"));
            var parts = new[] { Part("a", 0), Part("b", 1) };
            Require(store.TrySaveDocument("Capture", "OTHER", parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), null, null, null, null,
                out var saved, out string error, sourceDocuments: new[] { source }), error);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            byte[] portable = BlueprintExchangeFormat.Encode(reopened);
            var decoded = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(portable), out int version);
            var parsed = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "capture.buildworks.json");
            Require(parsed.importError == null, parsed.importError);
            parsed.id = saved.id;
            Require(version == BlueprintExchangeFormat.Version && decoded.sourceDocuments.Single().text == source.text &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(decoded)) &&
                portable.SequenceEqual(BlueprintExchangeFormat.Encode(parsed)), "World origin lost on reopen/portable/reimport");
            var copy = decoded.ImportCopy(); copy.sourceDocuments[0].name = "changed.txt";
            Require(decoded.sourceDocuments[0].name == source.name &&
                CompositeBlueprintStore.InspectContents(decoded, _ => 1).Unknown == 0 &&
                !CompositeBlueprintStore.HasForeignData(decoded), "World origin aliases a copy or is treated as unknown gameplay state");
            string native = File.ReadAllText(path);
            var bound = CompositeBlueprintStore.CloneParts(saved.parts);
            bound[0].sourceDocumentId = source.id; bound[0].sourceLine = 3;
            Require(!store.TryUpdateDocument(saved, saved.name, saved.category, bound, saved.groups, saved.anchors,
                saved.primaryPartId, saved.primaryGroupId, out error) && File.ReadAllText(path) == native,
                "World origin was executed as a foreign part row");
            bool refused = false;
            try { BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable).Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":15")); }
            catch (InvalidDataException) { refused = true; }
            Require(refused, "World origin was silently backdated to portable15");
            string oldPath = Path.Combine(folder, "backdated.json"), backdated = native.Replace("\"version\":" + CompositeBlueprintStore.FormatVersion, "\"version\":27");
            File.WriteAllText(oldPath, backdated);
            var old = new CompositeBlueprintStore(oldPath);
            Require(old.All().Count == 0 && !old.TryAddCategory("Blocked", out _) && File.ReadAllText(oldPath) == backdated,
                "World origin was silently backdated to native27 or refused input was overwritten");

            string cleanPath = Path.Combine(folder, "legacy27.json");
            var clean = new CompositeBlueprintStore(cleanPath);
            Require(clean.TrySaveDocument("Legacy", "OTHER", parts, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), null, null, out var legacy, out error), error);
            string legacyNative = File.ReadAllText(cleanPath).Replace("\"version\":" + CompositeBlueprintStore.FormatVersion, "\"version\":27");
            File.WriteAllText(cleanPath, legacyNative);
            Require(new CompositeBlueprintStore(cleanPath).All().Count == 1 && File.ReadAllText(cleanPath) == legacyNative,
                "Reading legacy27 without world origin failed or rewrote input");
            var legacyPortable = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(legacy))
                .Replace("\"version\":" + BlueprintExchangeFormat.Version, "\"version\":15"), out version);
            Require(version == 15 && legacyPortable.sourceDocuments == null, "Legacy portable15 compatibility was lost");
            Console.WriteLine("PASS inert world capture origin: native30/portable18 exact reopen/reimport/copy; no opaque/gameplay/row binding; backdating refused atomically; native27/portable15 remain readable without rewriting");
        }

        private static CompositeBlueprintStore.Part Part(string id, float x) => new CompositeBlueprintStore.Part {
            prefabName = "wall", displayName = "Wall", stableId = id, position = new CompositeBlueprintStore.VectorData { x = x },
            rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } };
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
