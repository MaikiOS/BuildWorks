using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class CatalogRegressionChecks
    {
        internal static void Run(string directory)
        {
            string path = Path.Combine(directory, "library.json");
            string imports = Path.Combine(directory, "imports");
            Directory.CreateDirectory(imports);
            File.WriteAllText(Path.Combine(imports, "table.blueprint"),
                "#Name:Table\n#Pieces\nwoodwall;Workbench;0;0;0;0;0;0;1;\"\";1;1;1\nwoodwall;Workbench;1;0;0;0;0;0;1;\"\";1;1;1\n");
            int ownerThread = Thread.CurrentThread.ManagedThreadId, scaleReads = 0;
            var store = new CompositeBlueprintStore(path, backgroundImports: true);
            store.ConfigureImportScale(_ =>
            {
                Require(Thread.CurrentThread.ManagedThreadId == ownerThread, "Worker touched Unity scale resolver");
                ++scaleReads;
                return new CompositeBlueprintStore.VectorData { x = 2, y = 2, z = 2 };
            });
            Wait(store);
            var source = store.CatalogAll().Single();
            Require(source.parts[0].scale.x == 1 && source.parts[0].baseScale.Value.x == 1 && scaleReads == 1,
                "Host snapshot missed distinct-prefab cache");
            Require(store.TryGetOrCreatePlacementCopy(source, out var first, out string error), error);
            byte[] before = File.ReadAllBytes(path); int revision = store.Revision;
            Require(store.TryGetOrCreatePlacementCopy(source, out var second, out error) && ReferenceEquals(first, second), error);
            Require(store.Revision == revision && File.ReadAllBytes(path).SequenceEqual(before) && store.All().Count == 1,
                "Repeated placement mutated library");
            string portable = System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(first));
            Require(!portable.Contains("placementImportId") && first.placementImportId == source.id &&
                BlueprintExchangeFormat.Decode(portable).parts.Count == 2, "Portable export leaked local placement cache identity");
            string invalidOld = Path.Combine(directory, "old-with-cache.json");
            File.WriteAllText(invalidOld, File.ReadAllText(path).Replace(",\"hiddenSources\":[]", "").Replace("\"version\":27", "\"version\":15"));
            Require(new CompositeBlueprintStore(invalidOld).All().Count == 0, "Old native schema accepted placement cache field");
            Require(ReferenceEquals(source, store.CatalogAll().Single(x => x.IsExternal)) && scaleReads == 1,
                "Native save reconverted external library");
            var loaded = new CompositeBlueprintStore(path);
            Require(loaded.TryGetOrCreatePlacementCopy(source, out second, out error) && second.id == first.id && loaded.All().Count == 1,
                "Placement copy was duplicated after reload");
            var changed = CompositeBlueprintStore.CloneParts(second.parts);
            changed[0].position = new CompositeBlueprintStore.VectorData { x = 7, y = 0, z = 0 };
            Directory.CreateDirectory(path + ".tmp");
            Require(!loaded.TryUpdateParts(second, changed, second.anchors, out _) &&
                second.placementImportId == source.id && second.parts[0].position.x == 0 && File.ReadAllBytes(path).SequenceEqual(before),
                "Failed edit did not restore placement identity/document/bytes");
            Require(loaded.TryGetOrCreatePlacementCopy(source, out var afterFailure, out error) && ReferenceEquals(afterFailure, second) &&
                loaded.All().Count == 1, "Failed edit caused another placement copy");
            Directory.Delete(path + ".tmp");
            Require(loaded.TryUpdateParts(second, changed, second.anchors, out error), error);
            Require(loaded.TryGetOrCreatePlacementCopy(source, out var original, out error) && original.id != second.id &&
                original.parts[0].position.x == 0 && second.parts[0].position.x == 7, "Edited copy replaced external original");
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 4, y = 4, z = 4 });
            store.CatalogAll();
            store.ConfigureImportScale(_ => new CompositeBlueprintStore.VectorData { x = 8, y = 8, z = 8 });
            Wait(store);
            var latest = store.CatalogAll().Single(x => x.IsExternal);
            var expected = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Load(imports).Single(),
                _ => new CompositeBlueprintStore.VectorData { x = 8, y = 8, z = 8 });
            Require(latest.id == expected.id && latest.id != source.id && latest.parts[0].baseScale.Value.x == 1,
                "Stale worker result survived host invalidation");
            Console.WriteLine("PASS catalog worker isolation, cached conversion, idempotent placement copy/reload, edited-copy isolation, stale worker rejection");
        }

        private static void Wait(CompositeBlueprintStore store) => Require(SpinWait.SpinUntil(() =>
        { store.CatalogAll(); return !store.CatalogLoading; }, 10000), "Catalog worker timeout");

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
