using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Ostrix.ValheimQa
{
    // QA setup only: merge identical owned stacks through the native inventory API.
    public static class ProjectInventoryProbe
    {
        private const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        private static Guid project, operation;
        private static Player owner;
        private static Inventory inventory;
        private static int expectedReceipts;
        private static bool used;
        private static Type Type(string name) => AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType("OstrixMods.BuildWorks." + name, false)).Single(t => t != null);
        private static object Get(object value, string name) => value?.GetType().GetField(name, Members)?.GetValue(value) ??
            value?.GetType().GetProperty(name, Members)?.GetValue(value);
        private static object Runtime => Type("BuildWorksPlugin").GetProperty("QaConstructionProjects", Members).GetValue(null);
        private static object Journal()
        {
            var journal = Activator.CreateInstance(Type("BlueprintConstructionPayoutJournal"), Members, null, new[] { Get(Runtime, "store") }, null);
            return journal.GetType().GetMethod("Read", Members).Invoke(journal, new object[] { project });
        }
        private static Player Require()
        {
            var player = (Player)RenderProbe.RequireQaWorld();
            if (!player || player.IsDead() || player.GetPlayerID() != 376043676 || ZNet.instance.GetWorldUID() != 1137745799 ||
                (string)Type("BuildWorksPlugin").GetField("PluginVersion", Members).GetRawConstantValue() != "0.19.279")
                throw new InvalidOperationException("Exact living isolated QA identity and .279 required.");
            return player;
        }
        private static string Package(ItemDrop.ItemData item) => Convert.ToBase64String((byte[])Type("BlueprintConstructionPayoutSeal")
            .GetMethod("Package", Members).Invoke(null, new object[] { item }));
        private static ItemDrop.ItemData Copy(ItemDrop.ItemData item)
        {
            var copy = item.Clone();
            if (ReferenceEquals(copy, item) || ReferenceEquals(copy.m_customData, item.m_customData))
                throw new InvalidOperationException("Independent native clone required.");
            return copy;
        }
        private static string Normalized(ItemDrop.ItemData item)
        {
            var copy = Copy(item); copy.m_stack = 1; copy.m_gridPos = new Vector2i(0, 0);
            return Package(copy);
        }
        private static bool SameLive(ItemDrop.ItemData a, ItemDrop.ItemData b) =>
            ReferenceEquals(a.m_dropPrefab, b.m_dropPrefab) && ReferenceEquals(a.m_shared, b.m_shared) &&
            a.m_durability == b.m_durability && a.m_quality == b.m_quality && a.m_variant == b.m_variant && a.m_worldLevel == b.m_worldLevel &&
            a.m_pickedUp == b.m_pickedUp && a.m_equipped == b.m_equipped && a.m_cheated == b.m_cheated &&
            a.m_crafterID == b.m_crafterID && a.m_crafterName == b.m_crafterName &&
            a.m_customData.Count == b.m_customData.Count && a.m_customData.All(pair => b.m_customData.TryGetValue(pair.Key, out string value) && value == pair.Value);
        private static bool SameMergeCandidate(ItemDrop.ItemData source, ItemDrop.ItemData target)
        {
            var prefab = ObjectDB.instance.GetItemPrefab("Wood");
            if (!prefab) return false;
            string canonical = UnityEngine.JsonUtility.ToJson(prefab.GetComponent<ItemDrop>().m_itemData.m_shared);
            if (canonical.Length < 64 || UnityEngine.JsonUtility.ToJson(source.m_shared) != canonical ||
                UnityEngine.JsonUtility.ToJson(target.m_shared) != canonical) return false;
            // Distinct shared defaults can have equal values; retain each live reference after the move.
            var copy = Copy(source); copy.m_shared = target.m_shared;
            return SameLive(copy, target) && Normalized(source) == Normalized(target);
        }
        public static string Arm(string id)
        {
            var player = Require();
            if (project != Guid.Empty) throw new InvalidOperationException("QA inventory setup may only be armed once per process.");
            Guid candidate = Guid.ParseExact(id, "N");
            var plan = Runtime.GetType().GetMethod("Read", Members).Invoke(Runtime, new object[] { candidate });
            if ((string)Get(Get(plan, "Blueprint"), "id") != "cc552d6270924447b3ebf51e66e09a5b" || !(bool)Get(plan, "Cancelled"))
                throw new InvalidOperationException("Existing ordinary two-wall cancelled QA project required.");
            project = candidate;
            var record = Journal(); var collections = Get(record, "Collections") as Array;
            bool initial = collections?.Cast<object>().Any(value => value != null) != true &&
                player.GetInventory().GetAllItems().Count == 31 && player.GetInventory().CountItems("$item_wood", -1, true) == 63;
            bool resumed = candidate == Guid.ParseExact("5fb62f0d5c9a4400bbdf9113951844bf", "N") &&
                collections?.Length == 2 && collections.GetValue(0) == null && (int?)Get(collections.GetValue(1), "Phase") == 2 &&
                (int?)Get(Get(collections.GetValue(1), "Removal"), "Phase") == 2 &&
                player.GetInventory().GetAllItems().Count == 32 && player.GetInventory().CountItems("$item_wood", -1, true) == 65;
            if ((int)Get(record, "Phase") != 3 || (long)Get(record, "Player") != player.GetPlayerID() ||
                (long)Get(record, "World") != 1137745799 || Get(record, "Terminal") == null || ((Array)Get(record, "Items")).Length != 2 ||
                !initial && !resumed)
            { project = Guid.Empty; throw new InvalidOperationException("New issuance or exact existing316 full-inventory continuation required."); }
            operation = (Guid)Get(record, "Operation"); owner = player; inventory = player.GetInventory(); expectedReceipts = player.m_customData.Count + (initial ? 1 : 0);
            return JsonConvert.SerializeObject(new { armed = true, project = project.ToString("N"), expectedReceipts, resumed, used });
        }
        public static string FreeOneCell()
        {
            if (project == Guid.Empty || used) throw new InvalidOperationException("One unused armed QA inventory setup required.");
            var player = Require(); var record = Journal(); var collection = Get(Runtime, "Collection");
            var credits = Get(record, "Collections") as Array;
            string message = (string)Type("BuildWorksLocalization").GetMethod("Text", Members, null, new[] { typeof(string), typeof(object[]) }, null)
                .Invoke(null, new object[] { "project.refund_inventory_full", Array.Empty<object>() });
            if (player != owner || !ReferenceEquals(player.GetInventory(), inventory) || (Guid)Get(record, "Operation") != operation ||
                (int)Get(record, "Phase") != 3 || credits == null || credits.Length != 2 || credits.GetValue(0) != null ||
                (int?)Get(credits.GetValue(1), "Phase") != 2 || (int?)Get(Get(credits.GetValue(1), "Removal"), "Phase") != 2 ||
                (Guid?)Get(collection, "Project") != project || (int?)Get(collection, "Index") != 0 ||
                (string)Get(collection, "Status") != "reconciliation" || (string)Get(collection, "Reason") != message ||
                inventory.GetWidth() != 8 || inventory.GetHeight() != 4 || inventory.GetAllItems().Count != 32 ||
                inventory.CountItems("$item_wood", -1, true) != 65 || player.m_customData.Count != expectedReceipts)
                throw new InvalidOperationException("Exact full-inventory refusal before index0 credit required.");
            var rows = inventory.GetAllItems().ToArray();
            var wood = rows.Where(item => item.m_dropPrefab && item.m_dropPrefab.name == "Wood" && item.m_stack == 2 && !item.m_equipped).ToArray();
            ItemDrop.ItemData from = null, to = null;
            foreach (var source in wood)
            {
                var target = wood.FirstOrDefault(item => !ReferenceEquals(item, source) && SameMergeCandidate(source, item) &&
                    ReferenceEquals(inventory.GetItemAt(item.m_gridPos.x, item.m_gridPos.y), item) &&
                    ReferenceEquals(inventory.GetItemAt(source.m_gridPos.x, source.m_gridPos.y), source) &&
                    item.m_stack + source.m_stack <= item.m_shared.m_maxStackSize && Normalized(item) == Normalized(source));
                if (target != null) { from = source; to = target; break; }
            }
            if (from == null) throw new InvalidOperationException("Two identical owned Wood stacks required; no fallback edits.");
            var expectedTarget = Copy(to); expectedTarget.m_stack += from.m_stack;
            var expected = rows.Where(item => !ReferenceEquals(item, from)).ToDictionary(item => item,
                item => ReferenceEquals(item, to) ? expectedTarget : Copy(item));
            var expectedShared = expected.ToDictionary(pair => pair.Key, pair => UnityEngine.JsonUtility.ToJson(pair.Value.m_shared));
            var oldReceipts = player.m_customData.ToArray(); int amount = from.m_stack;
            used = true;
            bool moved = inventory.MoveItemToThis(inventory, from, amount, to.m_gridPos.x, to.m_gridPos.y);
            if (!moved || inventory.ContainsItem(from) || inventory.GetAllItems().Count != 31 || expected.Keys.Any(item => !inventory.ContainsItem(item)) ||
                inventory.CountItems("$item_wood", -1, true) != 65 || inventory.GetAllItems().Any(item => !expected.TryGetValue(item, out var previous) ||
                    !SameLive(item, previous) || item.m_stack != previous.m_stack || item.m_gridPos.x != previous.m_gridPos.x ||
                    item.m_gridPos.y != previous.m_gridPos.y || Package(item) != Package(previous) ||
                    UnityEngine.JsonUtility.ToJson(item.m_shared) != expectedShared[item]) ||
                player.m_customData.Count != oldReceipts.Length || oldReceipts.Any(pair => !player.m_customData.TryGetValue(pair.Key, out string value) || value != pair.Value))
                throw new InvalidOperationException("Native merge differs from exact expected pair; no automatic repeat or repair.");
            return JsonConvert.SerializeObject(new { nativeMove = true, used, count = 31, wood = 65, receipts = player.m_customData.Count,
                expectedPropertiesExact = true, movedUnits = amount, noGrant = true, noProfileSaveRequestedByHelper = true });
        }
    }
}
