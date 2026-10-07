using System;
using System.Reflection;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Object = UnityEngine.Object;

// Runs the actual adapter against inert API fixtures, not against a world or real inventory.
internal static class ItemStandPlacementAcceptance
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException("ItemStand placement: " + reason); }
    private static void Set(ItemStand stand, string field, object value) => typeof(ItemStand).GetField(field, Private).SetValue(stand, value);
    private static object Queue(ItemStand stand) => typeof(ItemStand).GetField("m_queuedItem", Private).GetValue(stand);

    internal static void Run(GameObject prefab)
    {
        Player previous = Player.m_localPlayer;
        bool access = PrivateArea.Allowed;
        var playerObject = new GameObject("InertMountPlayer");
        Player player = playerObject.AddComponent<Player>();
        Player.m_localPlayer = player;
        PrivateArea.Allowed = true;
        try
        {
            foreach (string scenario in new[] { "own-properties", "own-properties-recipe", "recipe-stack", "full-stack1", "orientation", "missing", "mismatch",
                "ward", "owner", "false-ack", "owner-loss", "stale-item", "throw-write", "throw-consume",
                "unspent", "delayed-clear", "lost-after-consume", "double-consume", "foreign-queue",
                "rebound-write", "ward-write", "foreign-queue-write", "orientation-owner-loss",
                "changed-full", "changed-recipe", "oversized-recipe", "hash-collision" })
            {
                var placed = new GameObject("InertMount_" + scenario);
                placed.AddComponent<Piece>();
                ItemStand stand = placed.AddComponent<ItemStand>();
                stand.m_attachOther = placed.transform;
                stand.m_orientationType = ItemStand.Orientation.All;
                ZNetView view = placed.AddComponent<ZNetView>();
                Set(stand, "m_nview", view);
                var item = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                item.m_dropPrefab = prefab; item.m_stack = scenario == "full-stack1" ? 1 : 3;
                item.m_quality = 3; item.m_variant = 2; item.m_durability = 125.05f;
                item.m_gridPos = new Vector2Int(4, 1); item.m_worldLevel = 3; item.m_cheated = true;
                item.m_customData.Clear(); item.m_customData.Add("mod:fixture", "exact\0value\r\n🪵");
                player.Inventory.Items.Clear(); player.Inventory.Items.Add(item);
                var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                    prefab.name + ":2:3:" + (scenario == "orientation" || scenario == "orientation-owner-loss" ? "1" : "0"),
                    scenario == "own-properties-recipe" || scenario == "recipe-stack" || scenario == "orientation" || scenario == "changed-recipe" ||
                    scenario == "oversized-recipe" || scenario == "hash-collision" ? null :
                        Convert.ToBase64String(ItemStandPreviewAcceptance.ItemPackage(prefab.name, 3, 2)), null);
                string archivedInfo = data.Info, archivedPackage = data.Zdo;
                if (scenario == "own-properties" || scenario == "own-properties-recipe")
                {
                    item.m_quality = 1; item.m_variant = 0; item.m_durability = 7.53f;
                    item.m_gridPos = new Vector2Int(0, 2); item.m_worldLevel = 0; item.m_cheated = false;
                    item.m_customData.Clear(); item.m_customData.Add("mod:owner", "my supplied item");
                }
                var supplied = item.Clone(); supplied.m_stack = 1;
                var suppliedPacket = new ZPackage(); suppliedPacket.Write((byte)109); supplied.Save(suppliedPacket);
                string suppliedPackage = Convert.ToBase64String(suppliedPacket.GetArray());
                Require(BlueprintItemStandPlacement.CanRestore(placed.GetComponent<Piece>(), data), "native allowlist " + scenario);
                Require(BlueprintItemStandPlacement.CanSupply(player, new[] { data, data }) == (item.m_stack > 1), "same stack allocation " + scenario);
                Require(!BlueprintItemStandPlacement.CanSupply(player, new[] { data, data, data, data }), "mount shortage accepted " + scenario);
                Require(!BlueprintItemStandPlacement.CanRestore(placed.GetComponent<Piece>(),
                    new BlueprintForeignPartData("blueprint", "x", "inert", null)), "foreign raw state allowed");
                bool beforeAccess = PrivateArea.Allowed;
                switch (scenario)
                {
                    case "missing": player.Inventory.Items.Clear(); break;
                    case "mismatch": item.m_dropPrefab = placed; break;
                    case "ward": PrivateArea.Allowed = false; break;
                    case "owner": view.Owner = false; break;
                    case "false-ack": stand.RejectUse = true; break;
                    case "owner-loss": stand.OnUse = () => view.Owner = false; break;
                    case "stale-item": stand.OnUse = () => item.m_durability = 1; break;
                    case "throw-write": stand.AfterWrite = () => throw new InvalidOperationException("before consumption"); break;
                    case "throw-consume": stand.AfterConsume = () => throw new InvalidOperationException("after consumption"); break;
                    case "unspent": stand.SkipConsume = true; break;
                    case "delayed-clear": stand.SkipConsume = true; stand.DelayDestroy = true; break;
                    case "lost-after-consume": stand.AfterConsume = () => view.GetZDO().Set(ZDOVars.s_item, 0); break;
                    case "double-consume": stand.AfterConsume = () => player.Inventory.RemoveOneItem(item); break;
                    case "foreign-queue": stand.OnUse = () => Set(stand, "m_queuedItem", prefab.GetComponent<ItemDrop>().m_itemData); break;
                    case "orientation-owner-loss": stand.OnInteract = () => view.Owner = false; break;
                    case "changed-full":
                    case "changed-recipe": stand.AfterConsume = () =>
                        {
                            byte[] bytes = (byte[])view.GetZDO().GetByteArray(ZDOVars.s_itemData, null).Clone();
                            bytes[1] ^= 1; // Still valid native durability, but not the consumed item.
                            view.GetZDO().Set(ZDOVars.s_itemData, bytes);
                        }; break;
                    case "oversized-recipe": item.m_customData["mod:fixture"] = new string('x', 25000); break;
                    case "hash-collision": ObjectDB.instance.ItemHashes[prefab.name.GetStableHashCode()] = placed; break;
                    case "rebound-write": stand.AfterWrite = () =>
                        {
                            ZNetView rebound = placed.AddComponent<ZNetView>();
                            rebound.GetZDO().Set(ZDOVars.s_item, prefab.name.GetStableHashCode());
                            Set(stand, "m_nview", rebound);
                            throw new InvalidOperationException("rebound before consumption");
                        }; break;
                    case "ward-write": stand.AfterWrite = () =>
                        { PrivateArea.Allowed = false; throw new InvalidOperationException("access revoked"); }; break;
                    case "foreign-queue-write": stand.AfterWrite = () =>
                        {
                            Set(stand, "m_queuedItem", prefab.GetComponent<ItemDrop>().m_itemData);
                            throw new InvalidOperationException("foreign queue before consumption");
                        }; break;
                }
                bool applied = BlueprintItemStandPlacement.TryApply(player, placed, data, out bool attempted, out bool safe);
                ObjectDB.instance.ItemHashes[prefab.name.GetStableHashCode()] = prefab;
                PrivateArea.Allowed = beforeAccess;
                bool expected = scenario == "own-properties" || scenario == "own-properties-recipe" ||
                    scenario == "recipe-stack" || scenario == "full-stack1" || scenario == "orientation" || scenario == "throw-consume";
                Require(applied == expected, "confirmation " + scenario);
                bool started = scenario != "missing" && scenario != "mismatch" && scenario != "ward" && scenario != "owner" &&
                    scenario != "oversized-recipe" && scenario != "hash-collision";
                Require(attempted == started && stand.Uses == (started ? 1 : 0), "native use count " + scenario);
                bool uncertain = scenario == "delayed-clear" || scenario == "lost-after-consume" || scenario == "double-consume" ||
                    scenario == "foreign-queue" || scenario == "rebound-write" || scenario == "ward-write" || scenario == "foreign-queue-write" ||
                    scenario == "changed-full" || scenario == "changed-recipe";
                Require(safe != uncertain, "rollback classification " + scenario);
                Require(scenario == "foreign-queue" || scenario == "foreign-queue-write" ||
                    Queue(stand) == null && !stand.IsInvoking("UpdateAttach"), "native queue leaked " + scenario);
                Require(!stand.IsInvoking("UpdateOrientation"), "native orientation queue leaked " + scenario);
                if (scenario == "changed-full" || scenario == "changed-recipe")
                    Require(item.m_stack == 2 && stand.HaveAttachment() && stand.Destroys == 0,
                        "replacement was treated as the consumed item " + scenario);
                if (scenario == "rebound-write" || scenario == "ward-write" || scenario == "foreign-queue-write")
                    Require(stand.Destroys == 0 && item.m_stack == 3 && stand.HaveAttachment(),
                        "destructive cleanup acted on a denied/rebound/foreign holder " + scenario);
                if (expected)
                {
                    Require(BlueprintItemStandPlacement.CanRollback(placed) &&
                        !BlueprintItemStandPlacement.CanRollback(placed, true), "occupied holder force destruction " + scenario);
                    Require((player.Inventory.ContainsItem(item) ? item.m_stack : 0) == (scenario == "full-stack1" ? 0 : 2), "native exact-one consumption " + scenario);
                    Require(BlueprintItemStandCapture.TryCapture(placed.GetComponent<Piece>(), out var captured) &&
                        captured.Info == prefab.name + ":" + supplied.m_variant + ":" + supplied.m_quality + ":" + data.Info.Split(':')[3] &&
                        captured.Zdo == suppliedPackage, "supplied item's complete properties were not retained " + scenario);
                    Require(data.Info == archivedInfo && data.Zdo == archivedPackage, "source blueprint changed " + scenario);
                }
                if (scenario == "throw-write" || scenario == "unspent")
                    Require(stand.Destroys == 1 && !stand.HaveAttachment() && item.m_stack == 3 &&
                        BlueprintItemStandPlacement.CanRollback(placed, true), "unspent clone cleanup " + scenario);
                if (scenario == "ward" || scenario == "owner" || scenario == "owner-loss" || scenario == "orientation-owner-loss")
                {
                    if (scenario == "ward") PrivateArea.Allowed = false;
                    Require(!BlueprintItemStandPlacement.CanRollback(placed), "denied holder rollback " + scenario);
                    PrivateArea.Allowed = beforeAccess;
                }
                if (scenario == "foreign-queue")
                    Require(Queue(stand) != null && stand.IsInvoking("UpdateAttach"), "unrelated queue cancelled");
                stand.CancelInvoke("UpdateAttach");
                Object.DestroyImmediate(placed);
            }
            var reservedHolder = new GameObject("InertReservedRecipe");
            try
            {
                reservedHolder.AddComponent<Piece>();
                ItemStand stand = reservedHolder.AddComponent<ItemStand>();
                stand.m_attachOther = reservedHolder.transform;
                ZNetView view = reservedHolder.AddComponent<ZNetView>();
                Set(stand, "m_nview", view);
                var exactItem = prefab.GetComponent<ItemDrop>().m_itemData.Clone();
                exactItem.m_dropPrefab = prefab; exactItem.m_stack = 1;
                exactItem.m_quality = 3; exactItem.m_variant = 2; exactItem.m_durability = 125.05f;
                exactItem.m_gridPos = new Vector2Int(4, 1); exactItem.m_worldLevel = 3; exactItem.m_cheated = true;
                exactItem.m_customData.Clear(); exactItem.m_customData.Add("mod:fixture", "exact\0value\r\n🪵");
                var flexibleItem = exactItem.Clone();
                flexibleItem.m_durability = 100;
                var exact = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                    prefab.name + ":2:3:0", Convert.ToBase64String(ItemStandPreviewAcceptance.ItemPackage(prefab.name, 3, 2)), null);
                var recipe = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, exact.Info, null, null);
                player.Inventory.Items.Clear(); player.Inventory.Items.Add(exactItem);
                Require(!BlueprintItemStandPlacement.CanSupply(player, new[] { recipe, exact }) &&
                    BlueprintItemStandPlacement.FindItem(player, recipe) == exactItem,
                    "survival preflight lost item-count allocation");
                player.Inventory.Items.Add(flexibleItem);
                var supplies = new System.Collections.Generic.List<BlueprintItemStandPlacement.Supply>();
                Require(BlueprintItemStandPlacement.SelectSupplies(player, new[] { recipe, exact }, supplies) &&
                    supplies[0].Item == exactItem && supplies[1].Item == flexibleItem, "binding lost inventory order");
                exactItem.m_durability = 1;
                Require(!BlueprintItemStandPlacement.IsAvailable(player, supplies[0]) &&
                    !BlueprintItemStandPlacement.TryApply(player, reservedHolder, recipe, out bool changedAttempt, out bool changedSafe, supplies[0]) &&
                    !changedAttempt && changedSafe && !stand.HaveAttachment() && flexibleItem.m_stack == 1,
                    "changed selected item silently replaced or consumed");
                exactItem.m_durability = 125.05f;
                player.Inventory.Items.Remove(exactItem);
                Require(!BlueprintItemStandPlacement.TryApply(player, reservedHolder, recipe, out changedAttempt, out changedSafe, supplies[0]) &&
                    !changedAttempt && changedSafe && !stand.HaveAttachment() && flexibleItem.m_stack == 1,
                    "construction-spent selected item silently replaced or consumed");
                player.Inventory.Items.Insert(0, exactItem);
                Require(BlueprintItemStandPlacement.CanSupply(player, new[] { recipe, exact }) &&
                    BlueprintItemStandPlacement.TryApply(player, reservedHolder, recipe, out bool attempted, out bool safe, supplies[0]) &&
                    attempted && safe && !player.Inventory.ContainsItem(exactItem) &&
                    player.Inventory.ContainsItem(flexibleItem) && flexibleItem.m_stack == 1 &&
                    BlueprintItemStandPlacement.IsAvailable(player, supplies[1]),
                    "survival placement consumed more than its selected item");
            }
            finally { Object.DestroyImmediate(reservedHolder); }
        }
        finally
        {
            Player.m_localPlayer = previous;
            PrivateArea.Allowed = access;
            Object.DestroyImmediate(playerObject);
        }
    }
}
