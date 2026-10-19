using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class NativeArmorStateChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            void Require(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
            var state = new BlueprintArmorStandState(2, new[] {
                new BlueprintArmorStandState.Slot(4, 0, null, 3, new byte[] { 110, 2, 7 }),
                new BlueprintArmorStandState.Slot(9, 1234, null, 1, new byte[] { 111, 9 }),
                new BlueprintArmorStandState.Slot(7, "SwordIron".GetStableHashCode(), "SwordIron", 8, new byte[] { 111, 9 }) }).ToForeignData();
            var model = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(
                ExternalBlueprintLibrary.Parse("#Name:Armor capture\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"\nArmorStand;;1;0;0;0;0;0;1;\"\"", "armor.blueprint"),
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
            model.parts[1].foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(state);
            var summary = CompositeBlueprintStore.InspectContents(model, _ => 1);
            Require(summary.NativeArmorStates == 1 && summary.ArmorStates == 0 && summary.Mounts == 2 && summary.Unknown == 0 && summary.UninspectedArmorSlots == 2,
                "Native armor evidence confused with external omission or inactive item.");
            var clear = CompositeBlueprintStore.CopyWithContentsPolicy(model, false, true, _ => 1, true);
            Require(clear.parts[1].foreignData.contentsCleared && clear.parts[1].foreignData.zdo == state.Zdo &&
                clear.parts[1].foreignData.info == state.Info && !model.parts[1].foreignData.contentsCleared &&
                CompositeBlueprintStore.InspectContents(clear, _ => 1).NativeArmorStates == 1 &&
                CompositeBlueprintStore.InspectContents(clear, _ => 1).Mounts == 0, "Equipment exclusion erased pose/archive.");
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TrySaveDocument(model.name, model.category, model.parts, model.groups, model.anchors,
                null, null, model.supports, null, out var saved, out string error, sourceDocuments: model.sourceDocuments), error);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            string portable = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened));
            var decoded = BlueprintExchangeFormat.Decode(portable);
            Require(decoded.parts[1].foreignData.zdo == state.Zdo && decoded.parts[1].foreignData.info == state.Info &&
                decoded.parts[1].position.x == 1 && CompositeBlueprintStore.HasForeignData(decoded), "Save/portable roundtrip lost armor state.");
            Require(portable.Contains("\"requiredPrefabIds\":[\"ArmorStand\",\"SwordIron\",\"woodwall\"]") &&
                portable.Contains("\"version\":14") && File.ReadAllText(path).Contains("\"version\":25"),
                "Unknown/stale item became a prefab dependency or versions were not advanced.");
            try { BlueprintExchangeFormat.Decode(portable.Replace("\"version\":14", "\"version\":13"));
                throw new Exception("Armor state accepted under legacy portable version."); }
            catch (InvalidDataException) { }
            string old = File.ReadAllText(path).Replace("\"version\":25", "\"version\":24");
            string oldPath = Path.Combine(directory, "old.json"); File.WriteAllText(oldPath, old);
            var blocked = new CompositeBlueprintStore(oldPath);
            Require(blocked.All().Count == 0 && !blocked.TryAddCategory("Refuse", out _) && File.ReadAllText(oldPath) == old,
                "New armor schema was accepted/rewritten under old native version.");
            Console.WriteLine("PASS native armor archive: slots/pose, contents policy, local25/portable14, legacy rejection; no native restoration.");
        }
    }
}
