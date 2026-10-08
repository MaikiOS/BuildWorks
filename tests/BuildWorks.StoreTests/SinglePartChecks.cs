using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class SinglePartChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string sourcePath = Path.Combine(directory, "11-single-part.blueprint");
            const string source = "#Name:QA112 Single Wall\n#Pieces\nwoodwall;;3;4;5;0;0.7071068;0;0.7071068;\"\";2;.5;1.25\n";
            File.WriteAllText(sourcePath, source);
            var raw = ExternalBlueprintLibrary.Parse(source, sourcePath);
            Require(raw.importError == null && raw.parts.Count == 1, raw.importError ?? "Single external row rejected");
            var prepared = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(raw,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            var part = prepared.parts.Single();
            part.parentGroupId = "group";
            part.signText = "Привет, Ostrix!";
            var groups = new[] { new CompositeBlueprintStore.Group { stableId = "group", name = "Group", pivotPartId = part.stableId } };
            var supports = new[] { new CompositeBlueprintStore.Support { stableId = "support", name = "Вход",
                position = new CompositeBlueprintStore.VectorData { x = 2, y = 3, z = 4 },
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } } };
            var points = new[] { new CompositeBlueprintStore.VectorData { x = 7, y = 8, z = 9 } };
            Require(store.TrySaveDocument(prepared.name, "OTHER", prepared.parts, groups,
                Array.Empty<CompositeBlueprintStore.VectorData>(), part.stableId, "group", supports, "support",
                out var saved, out string error, points, sourceDocuments: prepared.sourceDocuments), error);
            string native = File.ReadAllText(path);
            Require(native.Contains("\"version\":20"), "Single native version missing");
            var reopened = new CompositeBlueprintStore(path).All().Single();
            Require(reopened.parts.Count == 1 && reopened.parts[0].signText == part.signText &&
                reopened.parts[0].scale.x == 1 && reopened.parts[0].baseScale.Value.x == 2 &&
                reopened.parts[0].baseScale.Value.y == .5f && reopened.parts[0].rotation.y == part.rotation.y &&
                reopened.parts[0].position.x == 3 && reopened.parts[0].position.y == 4 && reopened.parts[0].position.z == 5 &&
                reopened.primaryPartId == part.stableId && reopened.primaryGroupId == "group" &&
                reopened.groups[0].pivotPartId == part.stableId && reopened.placementSupportId == "support" &&
                reopened.supports[0].position.z == 4 && reopened.customSnapPoints[0].z == 9 &&
                reopened.sourceDocuments.Single().text == source, "Single native roundtrip lost construction data");
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            Require(portable.Contains("\"version\":10") && BlueprintExchangeFormat.Decode(portable).parts.Count == 1,
                "Single portable roundtrip failed");
            foreach (bool archive in new[] { false, true })
            {
                Require(store.TryExportFile(reopened, directory, archive ? "single-archive" : "single-json", archive,
                    out string exported, out error), error);
                Require(ExternalBlueprintLibrary.LoadFile(exported).Single().parts.Count == 1, "Single JSON/ZIP export failed");
            }
            var review = CompositeBlueprintStore.ReviewImportFile(sourcePath, ForeignZdoChecks.Hash, 0);
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            Require(store.TryImportReviewed(review, false, false, null, out error), error);
            Require(System.Threading.SpinWait.SpinUntil(() => { store.CatalogAll(); return !store.CatalogLoading; }, 10000),
                "Single import worker timeout");
            Require(store.ImportError == null &&
                ExternalBlueprintLibrary.LoadFile(store.ImportResultPath).Single().parts.Count == 1 &&
                File.ReadAllText(sourcePath) == source, "Reviewed single import lost row or changed source");

            Require(ExternalBlueprintLibrary.Parse(portable.Replace("\"version\":10", "\"version\":7"),
                "old.buildworks.json").importError != null, "Legacy portable schema accepted an invalid single part");
            string legacy = native.Replace("\"version\":20", "\"version\":17");
            File.WriteAllText(path, legacy);
            var blocked = new CompositeBlueprintStore(path);
            Require(blocked.All().Count == 0 && !blocked.TrySave(saved.parts, saved.anchors, out _, out _) &&
                File.ReadAllText(path) == legacy, "Legacy invalid native entry was rewritten");
            File.WriteAllText(path, native);
            store = new CompositeBlueprintStore(path);
            saved = store.All().Single();
            Require(!store.TryUpdateParts(saved, Array.Empty<CompositeBlueprintStore.Part>(), saved.anchors, out _) &&
                saved.parts.Count == 1 && File.ReadAllText(path) == native, "Empty update destroyed a single part");
            Require(!store.TrySave(Array.Empty<CompositeBlueprintStore.Part>(), saved.anchors, out _, out _) &&
                File.ReadAllText(path) == native, "Empty save changed library");
            var empty = saved.ImportCopy(); empty.parts.Clear();
            bool rejected = false;
            try { BlueprintExchangeFormat.Encode(empty); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Empty portable document accepted");
            var unknown = ExternalBlueprintLibrary.PrepareForHost(raw, _ => null);
            Require(unknown.parts.Count == 1 && unknown.parts[0].scaleIsAbsolute && !unknown.parts[0].baseScale.HasValue &&
                unknown.sourceDocuments.Single().text == source, "Single missing prefab lost its unresolved scale/source");
            Require(ExternalBlueprintLibrary.Parse("woodwall 0 0 0 1 0 0 0", "single.vbuild").parts.Count == 1,
                "Single vbuild row rejected");
            var two = saved.ImportCopy();
            var second = CompositeBlueprintStore.CloneParts(saved.parts).Single();
            second.stableId = "second"; second.parentGroupId = null;
            two.parts.Add(second);
            string oldPortable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(two)).Replace("\"version\":10", "\"version\":7");
            Require(BlueprintExchangeFormat.Decode(oldPortable).parts.Count == 2, "Valid portable7 stopped loading");
            Require(store.TryUpdateParts(saved, two.parts, saved.anchors, out error), error);
            string twoNative = File.ReadAllText(path).Replace("\"version\":20", "\"version\":17");
            File.WriteAllText(path, twoNative);
            store = new CompositeBlueprintStore(path); saved = store.All().Single();
            Require(saved.parts.Count == 2 && File.ReadAllText(path) == twoNative, "Valid native17 changed on read");
            Require(store.TryUpdateParts(saved, saved.parts.Take(1).ToArray(), saved.anchors, out error) &&
                new CompositeBlueprintStore(path).All().Single().parts.Count == 1 && File.ReadAllText(path).Contains("\"version\":20"),
                error ?? "Two-to-one edit failed to migrate/write one remaining part");
            Console.WriteLine("PASS single-part native18/portable8/import/export/source/scale/sign/group/support/snap; empty and legacy-invalid documents refused atomically; missing prefab retained");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
