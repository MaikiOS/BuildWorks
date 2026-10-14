using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ShareFileChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string library = Path.Combine(folder, "blueprints.json");
            var store = new CompositeBlueprintStore(library);
            var parts = new[] { new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall",
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
                displayName = "Wall", scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 } } };
            Require(store.TrySave("Share wall", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var wall, out string error), error);
            Require(store.ShareError == null, store.ShareError);
            string target = Path.Combine(store.ShareDirectory, wall.id + ".buildworks.json");
            Require(Read(target).id == wall.id && Read(target).parts[0].stableId == "wall", "Autosave identities changed");
            byte[] primary = File.ReadAllBytes(library), first = File.ReadAllBytes(target);
            DateTime stamp = File.GetLastWriteTimeUtc(target);
            Require(store.TryPrepareShareFile(wall, out string shared, out error) && shared == target, error);
            Require(stamp == File.GetLastWriteTimeUtc(target) && first.SequenceEqual(File.ReadAllBytes(target)) &&
                primary.SequenceEqual(File.ReadAllBytes(library)) && store.All().Count == 1, "Share rewrote identical bytes or library");
            Require(store.TryRename(wall, "Renamed", out error) && Read(target).name == "Renamed", error);
            Require(store.TryAddCategory("NEW", out error) && store.TrySetCategory(wall, "NEW", out error) && Read(target).category == "NEW", error);
            Require(store.TrySetPreview(wall, 60, 20, 2, out error) && Read(target).previewYaw == 60, error);
            Require(store.TryDeleteCategory("NEW", out error) && Read(target).category == "OTHER", error);
            Require(store.TrySave("Copy", parts, Array.Empty<CompositeBlueprintStore.VectorData>(), out var copy, out error), error);
            Require(copy.id != wall.id && Directory.GetFiles(store.ShareDirectory).Length == 2, "Save As did not create independent file");

            File.Delete(target); Directory.CreateDirectory(target);
            Require(store.TryRename(wall, "Saved despite copy failure", out error) && store.ShareErrorCount == 1 &&
                store.ShareError != null && new CompositeBlueprintStore(library).All()[0].name == wall.name,
                "Copy failure rolled back committed library");
            Require(!store.TryPrepareShareFile(wall, out _, out error) && store.All().Count == 2, "File retry duplicated card");
            Require(store.TryRename(copy, "Unrelated copy", out error) && store.ShareErrorCount == 1,
                "Unrelated successful save erased outstanding failed file");
            Directory.Delete(target);
            Require(store.TryRepairShareFiles(out error) && store.ShareErrorCount == 0 && Read(target).name == wall.name && store.All().Count == 2, error);

            File.WriteAllBytes(target, File.ReadAllBytes(Path.Combine(store.ShareDirectory, copy.id + ".buildworks.json")));
            byte[] foreign = File.ReadAllBytes(target);
            Require(!store.TryPrepareShareFile(wall, out _, out error) && foreign.SequenceEqual(File.ReadAllBytes(target)), "Foreign file overwritten");
            Require(!store.TryDelete(wall, out error) && store.All().Count == 2 && foreign.SequenceEqual(File.ReadAllBytes(target)), "Delete removed foreign file/card");
            File.Delete(target);
            Require(store.TryPrepareShareFile(wall, out _, out error), error);
            using (var locked = new FileStream(library, FileMode.Open, FileAccess.Read, FileShare.Read))
                Require(!store.TryDelete(wall, out error) && store.All().Contains(wall) && Read(target).id == wall.id,
                    "Primary failure did not restore deleted share file");
            Require(store.TryDelete(copy, out error) && !File.Exists(Path.Combine(store.ShareDirectory, copy.id + ".buildworks.json")), error);
            Require(Directory.GetFiles(store.ShareDirectory, "*.tmp").Length == 0, "Temporary file leaked");

            Directory.CreateDirectory(store.ImportDirectory);
            string source = Path.Combine(store.ImportDirectory, "foreign.blueprint");
            File.WriteAllText(source, "#Name:Foreign\n#Category:QA\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1");
            byte[] sourceBytes = File.ReadAllBytes(source);
            var externalStore = new CompositeBlueprintStore(library);
            externalStore.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            var external = externalStore.CatalogAll().Single(item => item.IsExternal);
            string externalId = external.id;
            primary = File.ReadAllBytes(library);
            Require(externalStore.TryPrepareShareFile(external, out shared, out error), error);
            var portable = Read(shared);
            Require(Path.GetFileName(shared) == externalId + ".buildworks.json" && portable.id == externalId.Substring(9, 32) &&
                external.id == externalId && external.IsExternal && portable.sourceDocuments.Count > 0 &&
                portable.parts[0].stableId == external.parts[0].stableId && sourceBytes.SequenceEqual(File.ReadAllBytes(source)) &&
                primary.SequenceEqual(File.ReadAllBytes(library)) && externalStore.All().Count == 1, "External Share changed origin/library/IDs");
            first = File.ReadAllBytes(shared);
            Require(externalStore.TryPrepareShareFile(external, out _, out error) && first.SequenceEqual(File.ReadAllBytes(shared)), error);
            var forged = wall.ImportCopy(); forged.name = "unsaved payload";
            Require(!store.TryPrepareShareFile(forged, out _, out error) && Read(target).name == wall.name, "Unsaved detached payload shared");
            Console.WriteLine("PASS share files: autosave/rename/category/preview/copy/delete/reload, identical no-write, primary vs derived failure, file-only retry, ownership/conflict, external provenance/no card, no temp leaks.");
        }

        private static CompositeBlueprintStore.Blueprint Read(string path) => BlueprintExchangeFormat.Decode(File.ReadAllText(path));
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
