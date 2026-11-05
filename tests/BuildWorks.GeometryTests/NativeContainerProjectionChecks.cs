using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class NativeContainerProjectionChecks
    {
        private const int Axe = 1790496580, Wood = -151837501;
        internal static void Run()
        {
            string Resolve(int key) => key == Axe ? "AxeIron" : key == Wood ? "Wood" : null;
            int Hash(string name) => name == "AxeIron" ? Axe : Wood;
            byte[] packet = ContainerInventoryTests.Packet(firstHash: Axe, secondHash: Wood);
            string source = Convert.ToBase64String(packet);
            foreach (int version in new[] { 109, 110 })
            {
                byte[] bytes = (byte[])packet.Clone(); bytes[0] = (byte)version;
                string input = Convert.ToBase64String(bytes);
                Check(BlueprintContainerInventory.TryGetNativePrefabHashes(input, out var keys) && keys.SequenceEqual(new[] { Wood, Axe }), "Independent literal prefab hashes");
                int calls = 0;
                Check(BlueprintContainerInventory.TryConvertNative(input, 5, 2, id => { ++calls; return Resolve(id); }, out var data) &&
                    calls == 2 && data.Info == "5:2:2:13|AxeIron|Wood" && data.Zdo == input && !data.ContentsCleared,
                    "Native demand/bytes/name manifest");
                Check(BlueprintContainerInventory.TryRead(data, Hash, out var items) && items.Count == 2, "Converted inventory unreadable");
                var first = items[0];
                Check(first.SourceVersion == version && first.Prefab == "AxeIron" && first.Stack == 12 && first.Quality == 3 && first.Variant == 2 &&
                    first.X == 4 && first.Y == 1 && first.WorldLevel == 3 && first.DurabilityHundredths == 12505 &&
                    first.CrafterId == -123 && first.CrafterName == "Острикс 🪓" && first.PickedUp && first.Equipped &&
                    first.Cheated == (version == 110) && first.CustomData["мод:данные"] == "exact\0payload\r\n🪵" &&
                    items[1].Stack == 1 && items[1].Quality == 1 && !items[1].Cheated, "Native item properties changed");
                bool immutable = false; try { ((IList<int>)keys).Clear(); } catch (NotSupportedException) { immutable = true; }
                Check(immutable && data.WithoutContents(Hash).Zdo == input && !data.ContentsCleared, "Mutable hash list/contents alias");
            }
            string legacy = Convert.ToBase64String(LegacyContainerInventoryTests.Packet());
            Check(BlueprintContainerInventory.TryGetNativePrefabHashes(legacy, out var oldKeys) && oldKeys.SequenceEqual(new[] { Axe }) &&
                BlueprintContainerInventory.TryConvertNative(legacy, 5, 2, null, out var old) && old.Zdo == legacy &&
                BlueprintContainerInventory.TryRead(old, Hash, out var oldItems) && oldItems[0].LegacyDurability == 20.125f && oldItems[0].Stack == 3,
                "Native106 exact-name/legacy attributes");
            foreach (byte version in new byte[] { 106, 109, 110 })
            {
                var bytes = new byte[version == 106 ? 8 : 6]; bytes[0] = version; string empty = Convert.ToBase64String(bytes);
                Check(BlueprintContainerInventory.TryGetNativePrefabHashes(empty, out var names) && names.Count == 0 &&
                    BlueprintContainerInventory.TryConvertNative(empty, 8, 4, null, out var demand) && demand.Info == "8:4:0:0" && demand.Zdo == empty,
                    "Known empty native package");
            }
            string duplicateName = Convert.ToBase64String(ContainerInventoryTests.Packet(firstHash: Axe, secondHash: Axe));
            int lookups = 0;
            Check(BlueprintContainerInventory.TryGetNativePrefabHashes(duplicateName, out var oneKey) && oneKey.SequenceEqual(new[] { Axe }) &&
                BlueprintContainerInventory.TryConvertNative(duplicateName, 5, 2, id => { ++lookups; return Resolve(id); }, out var repeated) &&
                lookups == 1 && repeated.Info == "5:2:2:13|AxeIron", "Repeated prefab lookup/units");
            int rejected = 0;
            void Reject(string input)
            {
                Check(!BlueprintContainerInventory.TryGetNativePrefabHashes(input, out var keys) && keys == null &&
                    !BlueprintContainerInventory.TryConvertNative(input, 5, 2, Resolve, out var data) && data == null, "Malformed native package accepted"); ++rejected;
            }
            for (int length = 0; length < packet.Length; ++length) Reject(Convert.ToBase64String(packet.Take(length).ToArray()));
            Reject(Convert.ToBase64String(packet.Concat(new byte[] { 0 }).ToArray()));
            foreach (int version in new[] { 0, 105, 107, 108, 111, int.MaxValue })
            { var changed = (byte[])packet.Clone(); Array.Copy(BitConverter.GetBytes(version), changed, 4); Reject(Convert.ToBase64String(changed)); }
            foreach (string invalid in new[] { null, "", "!", " " + source, source + "\t", new string('A', BlueprintForeignPartData.MaximumContainerBase64 + 1) }) Reject(invalid);
            Reject(Convert.ToBase64String(ContainerInventoryTests.Packet(firstHash: 0, secondHash: Wood)));
            foreach (Func<int, string> resolver in new Func<int, string>[] { null, _ => null, _ => "Wrong", _ => "../Wood", _ => " Wood" })
            { Check(!BlueprintContainerInventory.TryConvertNative(source, 5, 2, resolver, out var data) && data == null, "Missing/wrong name resolver accepted"); ++rejected; }
            int pipeLookups = 0;
            string pipeName = Convert.ToBase64String(ContainerInventoryTests.Packet(firstHash: -775945277, secondHash: Wood));
            Check(!BlueprintContainerInventory.TryConvertNative(pipeName, 5, 2, id => { ++pipeLookups; return id == Wood ? "Wood" : "A|B"; }, out _) &&
                pipeLookups == 1, "Manifest delimiter rejected only after interpreting another item"); ++rejected;
            foreach (var grid in new[] { (0, 2), (257, 2), (5, 0), (5, 257), (1, 1), (4, 2) })
            { Check(!BlueprintContainerInventory.TryConvertNative(source, grid.Item1, grid.Item2, Resolve, out var data) && data == null, "Invalid or occupied grid accepted"); ++rejected; }
            var duplicateCell = (byte[])packet.Clone(); duplicateCell[packet.Length - 9] = 4; duplicateCell[packet.Length - 8] = 1;
            Check(BlueprintContainerInventory.TryGetNativePrefabHashes(Convert.ToBase64String(duplicateCell), out _) &&
                !BlueprintContainerInventory.TryConvertNative(Convert.ToBase64String(duplicateCell), 5, 2, Resolve, out _), "Hash scan mistaken for placement demand");
            Check(Convert.ToBase64String(packet) == source, "Input modified");
            Console.WriteLine("PASS native106/109/110 inventory name scan and exact demand conversion: properties/catalogue/grid/bytes/empty/lookup cache;" +
                rejected + " malformed or unresolved controls. Synthetic wire fixtures only; no host snapshot, item grants or game proof.");
        }
        private static void Check(bool ok, string why) { if (!ok) throw new InvalidOperationException(why); }
    }
}
