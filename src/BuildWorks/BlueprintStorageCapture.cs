using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Read-only snapshot. Never calls Container.Save/Load, Inventory.Load, ownership or RPC.
    internal static class BlueprintStorageCapture
    {
        private static readonly FieldInfo ContainerView = typeof(Container).GetField("m_nview", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo ContainerAccess = typeof(Container).GetMethod("CheckAccess", BindingFlags.Instance | BindingFlags.NonPublic);

        // Only construction compatibility; do not load, clear or alter a live inventory.
        internal static bool CanPlaceWithoutContents(Piece piece, BlueprintForeignPartData data)
        {
            if (!piece || data == null || data.Format != BlueprintForeignPartData.NativeContainerFormat ||
                !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) ||
                !data.ContentsCleared && items.Count != 0 || piece.GetComponent<TombStone>() ||
                piece.GetComponent<Corpse>() || piece.GetComponent<Incinerator>() ||
                piece.GetComponent<ItemStand>() || piece.GetComponent<ArmorStand>()) return false;
            Container[] containers = piece.GetComponentsInChildren<Container>(true);
            return containers.Length == 1 && containers[0].gameObject == piece.gameObject &&
                containers[0].GetType() == typeof(Container) && !containers[0].m_autoDestroyEmpty && !containers[0].m_wagon &&
                // shortcut: default-loot prefabs need a scoped pre-Awake policy before they can be constructed empty.
                containers[0].m_defaultItems?.m_drops != null && containers[0].m_defaultItems.m_drops.Count == 0;
        }

        internal static bool TryCapture(Piece piece, Player player, out BlueprintForeignPartData data)
        {
            data = null;
            if (!piece) return false;
            Container[] containers = piece.GetComponentsInChildren<Container>(true);
            if (containers.Length == 0) return true;
            if (containers.Length != 1 || containers[0].gameObject != piece.gameObject) return false;
            Container container = containers[0];
            if (!player || container.GetType() != typeof(Container) || container.m_autoDestroyEmpty || container.m_wagon ||
                piece.GetComponent<TombStone>() || piece.GetComponent<Corpse>() || piece.GetComponent<Incinerator>() ||
                piece.GetComponent<ItemStand>() || piece.GetComponent<ArmorStand>()) return false;
            try
            {
                if (container.IsInUse() || ContainerAccess == null ||
                    !(bool)ContainerAccess.Invoke(container, new object[] { player.GetPlayerID() }) ||
                    container.m_checkGuardStone && !PrivateArea.CheckAccess(piece.transform.position, 0f, false, false))
                    return false;
                var view = ContainerView?.GetValue(container) as ZNetView;
                if (!view || !view.IsValid()) return false;
                ZDO zdo = view.GetZDO();
                Inventory inventory = container.GetInventory();
                if (zdo == null || inventory == null) return false;
                uint revision = zdo.DataRevision;
                byte[] raw = zdo.GetByteArray(ZDOVars.s_items, null);
                if (raw != null && (raw.Length < 6 || raw.Length > BlueprintForeignPartData.MaximumContainerBytes)) return false;
                raw = raw == null ? null : (byte[])raw.Clone();
                int width = inventory.GetWidth(), height = inventory.GetHeight();
                List<ItemDrop.ItemData> items = inventory.GetAllItems();
                if (items == null || width < 1 || height < 1 || width > 256 || height > 256 ||
                    items.Count > 4096 || items.Count > width * height) return false;
                // A new, initialized empty native chest may legitimately have no s_items yet.
                // Missing bytes are not otherwise proof of an empty inventory.
                if (raw == null && (items.Count != 0 || !zdo.GetBool(ZDOVars.s_addedDefaultItems, false))) return false;
                var prefabs = new SortedSet<string>(StringComparer.Ordinal);
                var cells = new HashSet<int>();
                long units = 0, serializationBudget = 6;
                foreach (ItemDrop.ItemData item in items)
                {
                    if (item?.m_shared == null || !item.m_dropPrefab || item.m_customData == null || item.m_customData.Count > 32767 ||
                        item.m_stack < 1 || item.m_stack > ushort.MaxValue || item.m_quality < 1 || item.m_quality > ushort.MaxValue ||
                        item.m_gridPos.x < 0 || item.m_gridPos.x >= width || item.m_gridPos.y < 0 || item.m_gridPos.y >= height ||
                        item.m_worldLevel < 0 || item.m_worldLevel > byte.MaxValue ||
                        item.m_dropPrefab.name.IndexOf('|') >= 0 ||
                        !cells.Add(item.m_gridPos.y * width + item.m_gridPos.x) || !ObjectDB.instance) return false;
                    GameObject resolved = ObjectDB.instance.GetItemPrefab(item.m_dropPrefab.name);
                    if (!resolved || !string.Equals(resolved.name, item.m_dropPrefab.name, StringComparison.Ordinal)) return false;
                    prefabs.Add(item.m_dropPrefab.name);
                    units += item.m_stack;
                    // Upper bound for the verified native Save fields; prevent a huge temporary package.
                    serializationBudget += 40;
                    if (item.m_crafterID != 0)
                    {
                        if (item.m_crafterName == null) return false;
                        serializationBudget += 5L + Encoding.UTF8.GetByteCount(item.m_crafterName);
                    }
                    foreach (var value in item.m_customData)
                    {
                        if (value.Key == null || value.Value == null) return false;
                        serializationBudget += 10L + Encoding.UTF8.GetByteCount(value.Key) + Encoding.UTF8.GetByteCount(value.Value);
                        if (serializationBudget > BlueprintForeignPartData.MaximumContainerBytes) return false;
                    }
                    if (serializationBudget > BlueprintForeignPartData.MaximumContainerBytes) return false;
                }
                string info = string.Join(":", new[] { width, height, items.Count, checked((int)units) }
                    .Select(value => value.ToString(CultureInfo.InvariantCulture)));
                if (prefabs.Count > 0) info += "|" + string.Join("|", prefabs);
                if (info.Length > 32768) return false;
                var package = new ZPackage();
                inventory.Save(package); // Only writes this new private package.
                byte[] canonical = package.GetArray();
                if (raw != null && !canonical.SequenceEqual(raw) || zdo.DataRevision != revision || container.IsInUse()) return false;
                raw = raw ?? canonical;
                var snapshot = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(raw), null);
                if (!BlueprintContainerInventory.TryRead(snapshot, name => name.GetStableHashCode(), out _)) return false;
                data = snapshot;
                return true;
            }
            catch (Exception error) when (error is ArgumentException || error is OverflowException || error is TargetInvocationException)
            { return false; }
        }
    }
}
