using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Only newly placed, locally owned holders. Never load archive bytes into an item or ZDO.
    internal static class BlueprintItemStandPlacement
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo StandView = typeof(ItemStand).GetField("m_nview", Private);
        private static readonly FieldInfo QueuedItem = typeof(ItemStand).GetField("m_queuedItem", Private);
        private static readonly FieldInfo VisualItem = typeof(ItemStand).GetField("m_visualItemDrop", Private);
        private static readonly FieldInfo Orientations = typeof(ItemStand).GetField("m_orientationSettings", Private);
        private static readonly MethodInfo UpdateAttach = typeof(ItemStand).GetMethod("UpdateAttach", Private);

        internal sealed class Reservation
        {
            internal readonly ItemDrop.ItemData Item;
            internal readonly int Amount;
            internal readonly string Snapshot;
            internal Reservation(ItemDrop.ItemData item, int amount, string snapshot)
            { Item = item; Amount = amount; Snapshot = snapshot; }
        }

        internal sealed class Supply
        {
            internal readonly ItemDrop.ItemData Item;
            internal readonly BlueprintForeignPartData Contents;
            internal readonly IReadOnlyList<Reservation> Items;
            internal Supply(ItemDrop.ItemData item, BlueprintForeignPartData contents)
            { Item = item; Contents = contents; Items = Array.AsReadOnly(new[] { new Reservation(item, 1, contents.Zdo) }); }
            internal Supply(List<Reservation> items)
            { Items = Array.AsReadOnly(items.ToArray()); }
        }

        internal static bool SelectSupplies(Player player, IEnumerable<BlueprintForeignPartData> contents,
            List<Supply> result)
        {
            var reserved = new Dictionary<ItemDrop.ItemData, int>();
            var snapshots = new Dictionary<ItemDrop.ItemData, string>();
            bool complete = true;
            int index = 0;
            foreach (BlueprintForeignPartData data in contents)
            {
                Supply supply = null;
                if (NeedsSupply(data))
                {
                    if (data.Format == BlueprintForeignPartData.NativeContainerFormat)
                    {
                        supply = ReserveContainer(player, data, reserved, snapshots);
                        if (supply == null) complete = false;
                    }
                    else
                    {
                        ItemDrop.ItemData item = FindItem(player, data, reserved, snapshots);
                        if (item == null) complete = false;
                        else
                        {
                            reserved.TryGetValue(item, out int count);
                            reserved[item] = checked(count + 1);
                            string info = item.m_dropPrefab.name + ":" + item.m_variant.ToString(CultureInfo.InvariantCulture) + ":" +
                                item.m_quality.ToString(CultureInfo.InvariantCulture) + ":" + data.Info.Split(':')[3];
                            Supply previous = index < result.Count ? result[index] : null;
                            supply = previous != null && ReferenceEquals(previous.Item, item) && previous.Contents != null &&
                                previous.Contents.Info == info && previous.Contents.Zdo == snapshots[item] ? previous :
                                new Supply(item, new BlueprintForeignPartData(
                                    BlueprintForeignPartData.NativeItemStandFormat, info, snapshots[item], null));
                        }
                    }
                }
                if (index < result.Count) result[index] = supply;
                else result.Add(supply);
                ++index;
            }
            if (result.Count > index) result.RemoveRange(index, result.Count - index);
            return complete;
        }

        internal static bool IsAvailable(Player player, Supply supply)
        {
            if (!player || supply == null) return false;
            try
            {
                if (supply.Item != null && !Matches(supply.Item, supply.Contents)) return false;
                foreach (Reservation reservation in supply.Items)
                    if (!player.GetInventory().ContainsItem(reservation.Item) || reservation.Amount < 1 ||
                        reservation.Item.m_stack < reservation.Amount || SnapshotItem(reservation.Item) != reservation.Snapshot)
                        return false;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is NullReferenceException)
            { return false; }
        }

        internal static bool SuppliesAvailable(Player player, List<Supply> supplies, Piece payingPiece = null)
        {
            if (!player || supplies == null) return false;
            if (supplies.Count == 0) return true;
            try
            {
                var reserved = new Dictionary<ItemDrop.ItemData, int>();
                var snapshots = new Dictionary<ItemDrop.ItemData, string>();
                foreach (Supply supply in supplies)
                {
                    if (supply == null) continue;
                    foreach (Reservation reservation in supply.Items)
                    {
                        if (!snapshots.TryGetValue(reservation.Item, out string snapshot))
                        {
                            if (!player.GetInventory().ContainsItem(reservation.Item)) return false;
                            snapshots.Add(reservation.Item, snapshot = SnapshotItem(reservation.Item));
                        }
                        if (snapshot != reservation.Snapshot || reservation.Amount < 1) return false;
                        reserved.TryGetValue(reservation.Item, out int count);
                        int total = checked(count + reservation.Amount);
                        if (total > reservation.Item.m_stack) return false;
                        reserved[reservation.Item] = total;
                    }
                }
                if (reserved.Count == 0 || !payingPiece) return true;
                if (!ZoneSystem.instance) return false;
                if (ZoneSystem.instance.GetGlobalKey(payingPiece.FreeBuildKey())) return true;

                Inventory inventory = player.GetInventory();
                var remaining = new Dictionary<ItemDrop.ItemData, int>();
                foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                {
                    if (item == null || item.m_shared == null || item.m_stack < 0) return false;
                    remaining.Add(item, item.m_stack);
                }
                CraftingStation station = player.GetCurrentCraftingStation();
                // Native payment uses shared names and inventory list order, including prefab aliases.
                foreach (Piece.Requirement requirement in payingPiece.m_resources ?? Array.Empty<Piece.Requirement>())
                {
                    if (requirement == null) return false;
                    if (station ? station.m_upgrader != requirement.m_upgraderResource : requirement.m_upgraderResource)
                        continue;
                    if (!requirement.m_resItem) continue;
                    int amount = requirement.GetAmount(0);
                    if (amount <= 0) continue;
                    string name = requirement.m_resItem.m_itemData.m_shared.m_name;
                    foreach (ItemDrop.ItemData item in inventory.GetAllItems())
                    {
                        if (item.m_shared.m_name != name || item.m_worldLevel < Game.m_worldLevel) continue;
                        int debit = Math.Min(amount, remaining[item]);
                        remaining[item] -= debit;
                        amount -= debit;
                        if (amount == 0) break;
                    }
                    if (amount != 0) return false;
                }
                foreach (var entry in reserved)
                    if (!remaining.TryGetValue(entry.Key, out int count) || count < entry.Value) return false;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is InvalidOperationException ||
                error is NullReferenceException || error is OverflowException)
            { return false; }
        }

        private static Supply ReserveContainer(Player player, BlueprintForeignPartData data,
            Dictionary<ItemDrop.ItemData, int> reserved, Dictionary<ItemDrop.ItemData, string> snapshots)
        {
            if (!player || !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var demands)) return null;
            string[] grid = data.Info.Split('|')[0].Split(':');
            int cells = checked(int.Parse(grid[0], CultureInfo.InvariantCulture) * int.Parse(grid[1], CultureInfo.InvariantCulture));
            var amounts = new Dictionary<ItemDrop.ItemData, int>();
            foreach (var demand in demands)
            {
                int needed = demand.Stack;
                foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
                {
                    if (item?.m_shared == null || !item.m_dropPrefab || item.m_dropPrefab.name != demand.Prefab ||
                        item.m_stack < 1 || item.m_shared.m_maxStackSize < 1) continue;
                    reserved.TryGetValue(item, out int held);
                    amounts.TryGetValue(item, out int amount);
                    int available = Math.Min(item.m_stack - held - amount, item.m_shared.m_maxStackSize - amount);
                    if (available <= 0) continue;
                    if (amount == 0 && amounts.Count == cells) continue;
                    if (!snapshots.ContainsKey(item))
                    {
                        try { snapshots[item] = SnapshotItem(item); }
                        catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is NullReferenceException)
                        { continue; }
                    }
                    int take = Math.Min(needed, available);
                    amounts[item] = checked(amount + take);
                    needed -= take;
                    if (needed == 0) break;
                }
                if (needed != 0) return null;
            }
            var items = new List<Reservation>();
            foreach (var entry in amounts)
            {
                reserved.TryGetValue(entry.Key, out int held);
                reserved[entry.Key] = checked(held + entry.Value);
                items.Add(new Reservation(entry.Key, entry.Value, snapshots[entry.Key]));
            }
            return new Supply(items);
        }

        internal static bool CanRestore(Piece piece, BlueprintForeignPartData data)
        {
            if (data == null) return true;
            if (data.Format == BlueprintForeignPartData.NativeContainerFormat)
                return BlueprintStorageCapture.CanPlaceWithoutContents(piece, data);
            if (!piece || piece.GetComponent<ArmorStand>() ||
                data.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                data.Zdo != null && !BlueprintItemStandContents.TryRead(data, name => name.GetStableHashCode(), out _))
                return false;
            ItemStand stand = piece.GetComponent<ItemStand>();
            if (!stand || stand.GetType() != typeof(ItemStand) || StandView == null || QueuedItem == null || UpdateAttach == null) return false;
            if (data.ContentsCleared) return true;
            string[] fields = data.Info.Split(':');
            GameObject prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(fields[0]) : null;
            int hash = fields[0].GetStableHashCode();
            if (hash == 0 || !prefab || ObjectDB.instance.GetItemPrefab(hash) != prefab) return false;
            ItemDrop drop = prefab ? prefab.GetComponent<ItemDrop>() : null;
            ItemDrop.ItemData item = drop ? drop.m_itemData : null;
            if (item?.m_shared == null || !ItemStand.GetAttachPrefab(prefab) ||
                !BlueprintItemStandPreview.Supports(stand, item) ||
                int.Parse(fields[1], CultureInfo.InvariantCulture) >= Math.Max(1, item.m_shared.m_variants) ||
                int.Parse(fields[2], CultureInfo.InvariantCulture) > item.m_shared.m_maxQuality ||
                item.m_shared.m_itemStandOffsets == null || !stand.GetAttach(item)) return false;
            var offsets = new List<ItemStand.OrientationSettings>();
            stand.GetOrientationSettings(item, ref offsets);
            int orientation = int.Parse(fields[3], CultureInfo.InvariantCulture);
            return orientation < offsets.Count && offsets[orientation] != null &&
                (orientation == 0 || VisualItem != null && Orientations != null);
        }

        internal static bool CanRollback(GameObject placed, bool forceDestroy = false)
        {
            if (placed && placed.GetComponent<Container>())
                return BlueprintStorageCapture.CanRemoveEmpty(placed, Player.m_localPlayer);
            ItemStand stand = placed ? placed.GetComponent<ItemStand>() : null;
            if (!stand) return true;
            var view = StandView?.GetValue(stand) as ZNetView;
            return view && view.IsValid() && view.IsOwner() && Player.m_localPlayer &&
                PrivateArea.CheckAccess(placed.transform.position, 0f, false, false) &&
                (!forceDestroy || !stand.HaveAttachment() && QueuedItem?.GetValue(stand) == null);
        }

        internal static bool CanSupply(Player player, IEnumerable<BlueprintForeignPartData> contents)
        {
            return SelectSupplies(player, contents, new List<Supply>());
        }

        internal static bool NeedsSupply(BlueprintForeignPartData data) => data != null && !data.ContentsCleared &&
            (data.Format != BlueprintForeignPartData.NativeContainerFormat ||
                !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) || items.Count != 0);

        internal static ItemDrop.ItemData FindItem(Player player, BlueprintForeignPartData data,
            Dictionary<ItemDrop.ItemData, int> reserved = null, Dictionary<ItemDrop.ItemData, string> snapshots = null)
        {
            if (!player || data == null || data.ContentsCleared ||
                data.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                data.Zdo != null && !BlueprintItemStandContents.TryRead(data, name => name.GetStableHashCode(), out _)) return null;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item == null || reserved != null && reserved.TryGetValue(item, out int count) && count >= item.m_stack) continue;
                try
                {
                    if (item?.m_dropPrefab?.name != data.Info.Split(':')[0]) continue;
                    if (snapshots != null && snapshots.ContainsKey(item)) return item;
                    if (Matches(item, data))
                    {
                        if (snapshots != null) snapshots[item] = SnapshotItem(item);
                        return item;
                    }
                }
                catch (Exception error) when (error is ArgumentException || error is InvalidOperationException || error is NullReferenceException)
                { Debug.LogWarning("BuildWorks cannot inspect inventory item: " + error.GetType().Name); }
            }
            return null;
        }

        private static bool Matches(ItemDrop.ItemData item, BlueprintForeignPartData data)
        {
            if (item?.m_shared == null || !item.m_dropPrefab || item.m_stack < 1 ||
                item.m_dropPrefab.name != data.Info.Split(':')[0]) return false;
            // Survival supplies the player's own item, never the archived item's properties.
            SnapshotItem(item);
            return true;
        }

        internal static string SnapshotItem(ItemDrop.ItemData item, bool transferProperties = false)
        {
            // Validate the complete supplied native record before consuming the real inventory object.
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = 1;
            if (transferProperties)
            {
                copy.m_gridPos = new Vector2i(0, 0);
                copy.m_equipped = false;
            }
            var package = new ZPackage();
            package.Write((byte)109);
            copy.Save(package);
            byte[] bytes = package.GetArray();
            if (bytes.Length > 24576) throw new ArgumentException("Mounted item exceeds the native snapshot limit.");
            string encoded = Convert.ToBase64String(bytes);
            var snapshot = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                item.m_dropPrefab.name + ":" + item.m_variant.ToString(CultureInfo.InvariantCulture) + ":" +
                item.m_quality.ToString(CultureInfo.InvariantCulture) + ":0", encoded, null);
            if (!BlueprintItemStandContents.TryRead(snapshot, name => name.GetStableHashCode(), out _))
                throw new ArgumentException("Mounted item has unreadable native properties.");
            return encoded;
        }

        internal static bool TryApply(Player player, GameObject placed, BlueprintForeignPartData data,
            out bool attempted, out bool rollbackSafe, Supply supply = null)
        {
            attempted = false;
            rollbackSafe = true;
            if (!player || !placed || !CanRestore(placed.GetComponent<Piece>(), data)) return false;
            if (!NeedsSupply(data)) return true;
            ItemStand stand = placed.GetComponent<ItemStand>();
            var view = StandView.GetValue(stand) as ZNetView;
            ItemDrop.ItemData item = supply == null ? FindItem(player, data) :
                IsAvailable(player, supply) ? supply.Item : null;
            if (supply != null && supply.Contents.Info.Split(':')[0] != data.Info.Split(':')[0]) return false;
            if (player != Player.m_localPlayer || !view || !view.IsValid() || !view.IsOwner() ||
                !PrivateArea.CheckAccess(placed.transform.position, 0f, false, false) ||
                stand.HaveAttachment() || QueuedItem.GetValue(stand) != null ||
                stand.IsInvoking("UpdateAttach") || item == null ||
                !BlueprintItemStandPreview.Supports(stand, item)) return false;
            Inventory inventory = player.GetInventory();
            int before = item.m_stack;
            bool complete = false;
            string expectedPackage = null;
            string expectedInfo = null;
            try
            {
                expectedPackage = SnapshotItem(item);
                int wanted = int.Parse(data.Info.Split(':')[3], CultureInfo.InvariantCulture);
                expectedInfo = item.m_dropPrefab.name + ":" + item.m_variant.ToString(CultureInfo.InvariantCulture) + ":" +
                    item.m_quality.ToString(CultureInfo.InvariantCulture) + ":" + wanted.ToString(CultureInfo.InvariantCulture);
                attempted = true;
                rollbackSafe = false;
                // UseItem's bool is not an acknowledgement. Execute its exact native queue only while owner.
                stand.UseItem(player, item);
                if (ReferenceEquals(QueuedItem.GetValue(stand), item) && view.IsValid() && view.IsOwner() &&
                    ReferenceEquals(StandView.GetValue(stand), view) && inventory.ContainsItem(item) &&
                    Matches(item, data) && SnapshotItem(item) == expectedPackage &&
                    PrivateArea.CheckAccess(placed.transform.position, 0f, false, false))
                    UpdateAttach.Invoke(stand, null);
                int remaining = inventory.ContainsItem(item) ? item.m_stack : 0;
                if (before - remaining != 1 || !view.IsValid() || !view.IsOwner() ||
                    !ReferenceEquals(StandView.GetValue(stand), view)) return false;
                if (wanted != 0)
                {
                    var offsets = Orientations.GetValue(stand) as List<ItemStand.OrientationSettings>;
                    // Avoid Interact's unrelated drop/guardian-power branches if the native visual is not ready.
                    if (!(VisualItem.GetValue(stand) is ItemDrop visual) || !visual ||
                        offsets == null || offsets.Count <= wanted) return false;
                    for (int step = 0; step < offsets.Count && view.GetZDO().GetInt(ZDOVars.s_type, 0) != wanted; ++step)
                    {
                        if (!view.IsValid() || !view.IsOwner() || !ReferenceEquals(StandView.GetValue(stand), view) ||
                            !PrivateArea.CheckAccess(placed.transform.position, 0f, false, false) ||
                            stand.IsInvoking("UpdateOrientation")) return false;
                        int previous = view.GetZDO().GetInt(ZDOVars.s_type, 0);
                        try { stand.Interact(player, false, true); }
                        finally { stand.CancelInvoke("UpdateOrientation"); }
                        if (!view.IsValid() || !view.IsOwner() || !ReferenceEquals(StandView.GetValue(stand), view)) return false;
                        if (view.GetZDO().GetInt(ZDOVars.s_type, 0) == previous) return false;
                    }
                }
                complete = BlueprintItemStandCapture.TryCapture(placed.GetComponent<Piece>(), out var captured) &&
                    captured != null && captured.Info == expectedInfo &&
                    captured.Zdo == expectedPackage;
                return complete;
            }
            catch (Exception error)
            {
                Debug.LogWarning("BuildWorks native item attachment failed: " + (error.InnerException ?? error).GetType().Name);
                complete = view && view.IsValid() && view.IsOwner() && ReferenceEquals(StandView.GetValue(stand), view) &&
                    before - (inventory.ContainsItem(item) ? item.m_stack : 0) == 1 &&
                    BlueprintItemStandCapture.TryCapture(placed.GetComponent<Piece>(), out var captured) &&
                    captured != null && captured.Info == expectedInfo && captured.Zdo == expectedPackage;
                return complete;
            }
            finally
            {
                if (stand)
                {
                    if (ReferenceEquals(QueuedItem.GetValue(stand), item))
                    {
                        stand.CancelInvoke("UpdateAttach");
                        QueuedItem.SetValue(stand, null);
                    }
                    int remaining = inventory.ContainsItem(item) ? item.m_stack : 0;
                    int consumed = before - remaining;
                    rollbackSafe = consumed == 0 && !stand.HaveAttachment() || consumed == 1 &&
                        BlueprintItemStandCapture.TryCapture(placed.GetComponent<Piece>(), out var held) &&
                        held != null && held.Zdo == expectedPackage;
                    if (!complete && consumed == 0 && stand.HaveAttachment() && view && view.IsValid() && view.IsOwner() &&
                        ReferenceEquals(StandView.GetValue(stand), view) && QueuedItem.GetValue(stand) == null &&
                        PrivateArea.CheckAccess(placed.transform.position, 0f, false, false) &&
                        stand.GetAttachedItem() == data.Info.Split(':')[0].GetStableHashCode())
                    {
                        // Native UpdateAttach writes the holder before consuming. Never drop that unspent copy.
                        try
                        {
                            stand.DestroyAttachment();
                            rollbackSafe = !stand.HaveAttachment();
                        }
                        catch (Exception error)
                        { Debug.LogWarning("BuildWorks native unspent attachment cleanup failed: " + error.GetType().Name); }
                    }
                    if (QueuedItem.GetValue(stand) != null || !view || !view.IsValid() ||
                        !ReferenceEquals(StandView.GetValue(stand), view)) rollbackSafe = false;
                }
                else rollbackSafe = false;
            }
        }
    }
}
