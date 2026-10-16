using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Exact first native Load projection. Never Save/Load an inventory or rewrite provided items.
    internal static class BlueprintStorageLoadedInventory
    {
        internal static bool Matches(Inventory live, BlueprintForeignPartData saved)
        {
            if (live == null || saved == null || saved.ContentsCleared ||
                !BlueprintContainerInventory.TryRead(saved, name => name.GetStableHashCode(), out var rows)) return false;
            int version = BitConverter.ToInt32(Convert.FromBase64String(saved.Zdo), 0);
            if (version != 109 && version != 110) return false;
            string[] grid = saved.Info.Split('|')[0].Split(':');
            if (live.GetWidth() != int.Parse(grid[0], CultureInfo.InvariantCulture) ||
                live.GetHeight() != int.Parse(grid[1], CultureInfo.InvariantCulture)) return false;
            var items = live.GetAllItems();
            if (items == null || items.Count != rows.Count) return false;
            var cells = new Dictionary<int, ItemDrop.ItemData>();
            foreach (var item in items)
            {
                if (item == null || item.m_gridPos.x < 0 || item.m_gridPos.x >= live.GetWidth() ||
                    item.m_gridPos.y < 0 || item.m_gridPos.y >= live.GetHeight()) return false;
                int cell = item.m_gridPos.y * live.GetWidth() + item.m_gridPos.x;
                if (cells.ContainsKey(cell)) return false;
                cells.Add(cell, item);
            }
            foreach (var row in rows)
            {
                if (row.SourceVersion != 109 && row.SourceVersion != 110 || !row.DurabilityHundredths.HasValue ||
                    row.DurabilityHundredths.Value < 0) return false;
                if (!cells.TryGetValue(row.Y * live.GetWidth() + row.X, out var current)) return false;
                if (current.m_shared == null || !current.m_dropPrefab || current.m_dropPrefab.name != row.Prefab ||
                    current.m_stack != row.Stack || current.m_quality != row.Quality || current.m_variant != row.Variant ||
                    current.m_worldLevel != row.WorldLevel || current.m_pickedUp != row.PickedUp || current.m_equipped != row.Equipped ||
                    current.m_cheated != row.Cheated || current.m_crafterID != row.CrafterId || current.m_crafterName != row.CrafterName ||
                    current.m_stack > current.m_shared.m_maxStackSize || current.m_quality > current.m_shared.m_maxQuality ||
                    row.Variant < 0 || row.Variant >= Math.Max(1, current.m_shared.m_variants) ||
                    // Match native stfld Single; Mono may otherwise compare an extended-precision product.
                    current.m_durability != (float)((float)row.DurabilityHundredths.Value * 0.01f) ||
                    current.m_customData == null || current.m_customData.Count != row.CustomData.Count ||
                    row.CustomData.Any(pair => !current.m_customData.TryGetValue(pair.Key, out string value) || value != pair.Value)) return false;
            }
            return true;
        }
    }
}
