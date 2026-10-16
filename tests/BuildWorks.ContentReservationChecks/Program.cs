using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    // Inert host substitutes. These do not invoke Unity, native Inventory or a world.
    internal class Obj { public static implicit operator bool(Obj value) => value != null; internal int GetInstanceID() => GetHashCode(); }
    internal sealed class Prefab : Obj { internal string name; }
    internal static class ItemDrop
    {
        internal sealed class Shared { internal string m_name; internal int m_maxStackSize = 50, m_maxQuality = 5, m_variants = 4; internal bool m_questItem; }
        internal sealed class ItemData
        {
            internal Shared m_shared;
            internal Prefab m_dropPrefab;
            internal int m_stack, m_quality = 1, m_variant, m_worldLevel;
            internal float m_durability = 5;
            internal Vector2i m_gridPos;
            internal bool m_equipped;
            internal bool m_pickedUp, m_cheated;
            internal long m_crafterID;
            internal string m_crafterName = "";
            internal Dictionary<string, string> m_customData = new Dictionary<string, string> { ["property"] = "own" };
            internal string Custom { get => m_customData["property"]; set => m_customData["property"] = value; }
            internal ItemData Clone() { var copy = (ItemData)MemberwiseClone(); copy.m_customData = new Dictionary<string, string>(m_customData); return copy; }
            internal void Save(ZPackage package) => Program.WriteItem(package.Writer, this);
        }
        internal sealed class Drop : Obj { internal ItemData m_itemData; }
    }
    internal sealed class ZPackage
    {
        private readonly MemoryStream stream = new MemoryStream();
        internal readonly BinaryWriter Writer;
        internal ZPackage() { Writer = new BinaryWriter(stream, Encoding.UTF8, true); }
        internal void Write(byte value) => Writer.Write(value);
        internal byte[] GetArray() { Writer.Flush(); return stream.ToArray(); }
    }
    internal sealed partial class Inventory
    {
        internal readonly List<ItemDrop.ItemData> Items = new List<ItemDrop.ItemData>();
        internal List<ItemDrop.ItemData> GetAllItems() => Items;
        internal bool ContainsItem(ItemDrop.ItemData item) => Items.Contains(item);
    }
    internal sealed partial class Player : Obj
    {
        internal Inventory Inventory = new Inventory();
        internal CraftingStation Station;
        internal Inventory GetInventory() => Inventory;
        internal CraftingStation GetCurrentCraftingStation() => Station;
    }
    internal sealed class CraftingStation : Obj { internal bool m_upgrader; }
    internal sealed partial class Piece : Obj
    {
        internal sealed class Requirement
        {
            internal bool m_upgraderResource;
            internal ItemDrop.Drop m_resItem;
            internal int Amount;
            internal int GetAmount(int _) => Amount;
        }
        internal Requirement[] m_resources;
        internal string FreeBuildKey() => "build";
    }
    internal sealed class ZoneSystem : Obj
    {
        internal static ZoneSystem instance = new ZoneSystem();
        internal readonly HashSet<string> Keys = new HashSet<string>();
        internal bool GetGlobalKey(string key) => Keys.Contains(key);
    }
    internal sealed class Game : Obj
    {
        internal static int m_worldLevel;
        internal static Game instance = new Game();
        internal bool ShuttingDown;
        internal PlayerProfile Profile;
        internal Action SaveAction;
        internal void SavePlayerProfile(bool setLogoutPoint, bool isFromRpc) { SaveAction?.Invoke(); }
        internal PlayerProfile GetPlayerProfile() => Profile;
        internal bool IsShuttingDown() => ShuttingDown;
    }
    internal static class Debug { internal static void LogWarning(string _) { } }
    internal struct TransformSnapshot { internal BlueprintForeignPartData ItemStandContents; }
    internal static class Hashes
    {
        internal static int GetStableHashCode(this string value)
        {
            unchecked
            {
                int a = 5381, b = a;
                for (int i = 0; i < value.Length && value[i] != 0; i += 2)
                {
                    a = ((a << 5) + a) ^ value[i];
                    if (i == value.Length - 1 || value[i + 1] == 0) break;
                    b = ((b << 5) + b) ^ value[i + 1];
                }
                return a + b * 1566083941;
            }
        }
    }
    internal static class Program
    {
        private static int assertions;
        private static void Check(bool condition, string reason)
        { assertions++; if (!condition) throw new InvalidOperationException(reason); }
        internal static ItemDrop.ItemData Item(string name, int amount, string custom = "own") => new ItemDrop.ItemData {
            m_dropPrefab = new Prefab { name = name }, m_shared = new ItemDrop.Shared { m_name = name }, m_stack = amount, Custom = custom };

        internal static void WriteItem(BinaryWriter writer, ItemDrop.ItemData item)
        {
            writer.Write((int)(item.m_durability * 100)); writer.Write((byte)item.m_gridPos.x); writer.Write((byte)item.m_gridPos.y); writer.Write((byte)item.m_worldLevel);
            int flags = 64 | (item.m_customData.Count != 0 ? 128 : 0) | (item.m_pickedUp ? 1 : 0) | (item.m_equipped ? 2 : 0) | (item.m_quality != 1 ? 4 : 0) | (item.m_stack != 1 ? 8 : 0) | (item.m_variant != 0 ? 16 : 0) | (item.m_crafterID != 0 ? 32 : 0);
            writer.Write((byte)flags);
            if (item.m_quality != 1) writer.Write((ushort)item.m_quality);
            if (item.m_stack != 1) writer.Write((ushort)item.m_stack);
            if (item.m_variant != 0) writer.Write(item.m_variant);
            if (item.m_crafterID != 0) { writer.Write(item.m_crafterID); writer.Write(item.m_crafterName); }
            writer.Write(item.m_dropPrefab.name.GetStableHashCode());
            if (item.m_customData.Count != 0) { writer.Write((byte)item.m_customData.Count); foreach (var pair in item.m_customData) { writer.Write(pair.Key); writer.Write(pair.Value); } }
            writer.Write((byte)(item.m_cheated ? 1 : 0));
        }
        internal static BlueprintForeignPartData Container(int width, params ItemDrop.ItemData[] demands)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(109); writer.Write((ushort)demands.Length);
                for (int i = 0; i < demands.Length; i++)
                {
                    using (var row = new MemoryStream())
                    using (var bytes = new BinaryWriter(row, Encoding.UTF8, true))
                    {
                        WriteItem(bytes, demands[i]); bytes.Flush(); byte[] record = row.ToArray(); record[4] = (byte)i; writer.Write(record);
                    }
                }
                writer.Flush();
                string info = width + ":1:" + demands.Length + ":" + demands.Sum(item => item.m_stack);
                if (demands.Length > 0) info += "|" + string.Join("|", demands.Select(item => item.m_dropPrefab.name).Distinct().OrderBy(name => name, StringComparer.Ordinal));
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(stream.ToArray()), null);
            }
        }
        private static BlueprintForeignPartData Mount(string prefab) => new BlueprintForeignPartData(
            BlueprintForeignPartData.NativeItemStandFormat, prefab + ":0:1:0", null, null);
        private static Piece Cost(ItemDrop.ItemData item, int amount) => new Piece { m_resources = new[] {
            new Piece.Requirement { m_resItem = new ItemDrop.Drop { m_itemData = item }, Amount = amount } } };

        private static int Main(string[] args)
        {
            if (args.Length == 2 && args[0] == "--read-journal")
            {
                try { var journal = BlueprintStorageTransferJournal.Read(args[1]); Console.WriteLine(journal.Prepared.Operation.ToString("N")); return journal.RequiresReconciliation ? 0 : 2; }
                catch (Exception error) { Console.Error.WriteLine(error.Message); return 1; }
            }
            try
            {
                var player = new Player(); var wood = Item("Wood", 10); player.Inventory.Items.Add(wood);
                var chest = Container(2, Item("Wood", 3, "archived"));
                var mount = Mount("Wood");
                Check(PreviewSelector.Select(new TransformSnapshot { ItemStandContents = chest }) == null &&
                    ReferenceEquals(PreviewSelector.Select(new TransformSnapshot { ItemStandContents = mount }), mount) &&
                    PreviewSelector.Select(new TransformSnapshot()) == null, "per-frame preview decoded/reserved container contents");
                var visualSupplies = new List<BlueprintItemStandPlacement.Supply>();
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] {
                    new TransformSnapshot { ItemStandContents = chest }, new TransformSnapshot { ItemStandContents = mount }
                }.Select(PreviewSelector.Select), visualSupplies) && visualSupplies[0] == null && visualSupplies[1].Items.Single().Amount == 1,
                    "preview did not retain mount-only supplied appearance");
                var plan = new List<BlueprintItemStandPlacement.Supply>();
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { chest, Mount("Wood"), chest }, plan), "two containers plus mount setup");
                Check(plan.Count == 3 && plan[0].Items.Single().Amount == 3 && plan[1].Items.Single().Amount == 1 && plan[2].Items.Single().Amount == 3,
                    "shared quantities do not match decoded demand");
                Check(plan[0].Contents == null && plan[0].Item == null && plan[1].Contents.Format == BlueprintForeignPartData.NativeItemStandFormat,
                    "container reservation leaked archived appearance into mount preview");
                Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan), "complete shared reservation rejected");
                Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, plan, Cost(wood, 4)) &&
                    BlueprintItemStandPlacement.SuppliesAvailable(player, plan, Cost(wood, 3)), "Wood counted both as cost and contents");
                wood.m_stack = 7; Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan), "legitimate native material debit invalidated properties");
                wood.m_stack = 6; Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, plan), "aggregate shortfall accepted");
                Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan.GetRange(1, 2)), "completed container reservation not releasable");
                Check(!BlueprintItemStandPlacement.SelectSupplies(player, new[] { chest, Mount("Wood"), chest }, new List<BlueprintItemStandPlacement.Supply>()),
                    "selection allocated one source quantity to multiple containers/mounts");
                wood.m_stack = 10; wood.Custom = "changed";
                Check(!BlueprintItemStandPlacement.IsAvailable(player, plan[0]) && !BlueprintItemStandPlacement.SuppliesAvailable(player, plan), "changed custom properties silently rebound");
                wood.Custom = "own"; var clone = wood.Clone(); player.Inventory.Items[0] = clone;
                Check(!BlueprintItemStandPlacement.IsAvailable(player, plan[0]), "equal clone substituted for exact source reference");
                player.Inventory.Items[0] = wood;

                var swordA = Item("SwordIron", 1, "A"); swordA.m_durability = 7; swordA.m_quality = 1;
                var swordB = Item("SwordIron", 1, "B"); swordB.m_durability = 2; swordB.m_quality = 2; swordB.m_variant = 1;
                player.Inventory.Items.Clear(); player.Inventory.Items.Add(swordA); player.Inventory.Items.Add(swordB);
                var archived = Item("SwordIron", 2, "admin"); archived.m_quality = 3; archived.m_durability = 999;
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { Container(2, archived) }, plan), "own properties wrongly required to match archive");
                Check(plan[0].Items.Count == 2 && plan[0].Items[0].Snapshot != plan[0].Items[1].Snapshot,
                    "different own stacks merged or properties discarded");
                Check(!BlueprintItemStandPlacement.SelectSupplies(player, new[] { Container(1, archived) }, plan) && plan[0] == null,
                    "split attributes exceeded recorded cell capacity");
                player.Inventory.Items.Clear(); player.Inventory.Items.Add(wood);
                Check(!BlueprintItemStandPlacement.SelectSupplies(player, new[] { Container(2, Item("Wood", 3), Item("Stone", 1)), Mount("Wood") }, plan) &&
                    plan[0] == null && plan[1]?.Item == wood && BlueprintItemStandPlacement.IsAvailable(player, plan[1]), "failed container leaked a partial reservation");
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { Container(2, Item("Wood", 2), Item("Wood", 3)) }, plan) &&
                    plan[0].Items.Count == 1 && plan[0].Items.Single().Amount == 5, "same exact source used unnecessary cells");
                var held = plan[0]; Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { Mount("Wood") }, plan) &&
                    held.Items.Single().Amount == 5 && held.Contents == null, "preview refresh mutated held container reservation or failed mount transition");
                var entries = new List<BlueprintItemStandPlacement.Reservation>(held.Items);
                var immutable = new BlueprintItemStandPlacement.Supply(entries); entries.Clear();
                Check(immutable.Items.Count == 1, "reservation aliases caller list");
                bool readOnly = false;
                try { ((IList<BlueprintItemStandPlacement.Reservation>)immutable.Items).Clear(); } catch (NotSupportedException) { readOnly = true; }
                Check(readOnly, "reservation list mutable after preparation");
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { chest.WithoutContents(name => name.GetStableHashCode()), Container(1) }, plan) &&
                    plan.All(supply => supply == null), "disabled/empty storage reserved items");
                Check(wood.m_stack == 10 && swordA.m_durability == 7 && swordB.m_durability == 2 &&
                    swordA.Custom == "A" && swordB.Custom == "B" && archived.Custom == "admin", "planning mutated inventory or archive");

                // Preserve native ordered-cost semantics from the existing mounted path.
                var spare = Item("AliasWood", 5); spare.m_shared.m_name = "Wood";
                Check(BlueprintItemStandPlacement.SelectSupplies(player, new[] { chest }, plan), "cost fixture setup");
                player.Inventory.Items.Insert(0, spare);
                var cost = Cost(wood, 5);
                Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "shared-name prefab alias ignored");
                spare.m_worldLevel = 0; Game.m_worldLevel = 1;
                Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "world-level cost filter ignored");
                Game.m_worldLevel = 0; cost.m_resources[0].m_upgraderResource = true;
                Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "upgrader cost applied without station");
                player.Station = new CraftingStation { m_upgrader = true }; cost.m_resources[0].Amount = 13;
                Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "upgrader matching cost ignored");
                ZoneSystem.instance.Keys.Add("build"); Check(BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "own free-build key ignored");
                wood.m_stack = 1; Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, plan, cost), "free building waived container items");
                wood.m_stack = int.MaxValue;
                var overflow = new BlueprintItemStandPlacement.Supply(new List<BlueprintItemStandPlacement.Reservation> {
                    new BlueprintItemStandPlacement.Reservation(wood, int.MaxValue, held.Items[0].Snapshot),
                    new BlueprintItemStandPlacement.Reservation(wood, 1, held.Items[0].Snapshot) });
                Check(!BlueprintItemStandPlacement.SuppliesAvailable(player, new List<BlueprintItemStandPlacement.Supply> { overflow }), "reservation overflow accepted");
                TransferChecks.Run();
                Console.WriteLine("PASS " + assertions + " content reservation assertions: actual selectors/payment checks and native-record decoder with inert host/serializer; reservation subset only; no native game execution.");
                return 0;
            }
            catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
    }
}
