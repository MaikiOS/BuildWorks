using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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

        internal static bool CanRestore(Piece piece, BlueprintForeignPartData data)
        {
            if (data == null) return true;
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
            ItemStand stand = placed ? placed.GetComponent<ItemStand>() : null;
            if (!stand) return true;
            var view = StandView?.GetValue(stand) as ZNetView;
            return view && view.IsValid() && view.IsOwner() && Player.m_localPlayer &&
                PrivateArea.CheckAccess(placed.transform.position, 0f, false, false) &&
                (!forceDestroy || !stand.HaveAttachment() && QueuedItem?.GetValue(stand) == null);
        }

        internal static bool CanSupply(Player player, IEnumerable<BlueprintForeignPartData> contents)
        {
            var reserved = new Dictionary<ItemDrop.ItemData, int>();
            foreach (BlueprintForeignPartData data in contents.Where(value => value != null && !value.ContentsCleared))
            {
                ItemDrop.ItemData item = FindItem(player, data, reserved);
                if (item == null) return false;
                reserved.TryGetValue(item, out int count);
                reserved[item] = count + 1;
            }
            return true;
        }

        internal static ItemDrop.ItemData FindItem(Player player, BlueprintForeignPartData data,
            Dictionary<ItemDrop.ItemData, int> reserved = null)
        {
            if (!player || data == null || data.ContentsCleared ||
                data.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                data.Zdo != null && !BlueprintItemStandContents.TryRead(data, name => name.GetStableHashCode(), out _)) return null;
            foreach (ItemDrop.ItemData item in player.GetInventory().GetAllItems())
            {
                if (item == null || reserved != null && reserved.TryGetValue(item, out int count) && count >= item.m_stack) continue;
                try { if (Matches(item, data)) return item; }
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

        private static string SnapshotItem(ItemDrop.ItemData item)
        {
            // Validate the complete supplied native record before consuming the real inventory object.
            ItemDrop.ItemData copy = item.Clone();
            copy.m_stack = 1;
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
            out bool attempted, out bool rollbackSafe)
        {
            attempted = false;
            rollbackSafe = true;
            if (!player || !placed || !CanRestore(placed.GetComponent<Piece>(), data)) return false;
            if (data == null || data.ContentsCleared) return true;
            ItemStand stand = placed.GetComponent<ItemStand>();
            var view = StandView.GetValue(stand) as ZNetView;
            ItemDrop.ItemData item = FindItem(player, data);
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
