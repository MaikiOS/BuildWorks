using OstrixMods.BuildWorks.Geometry;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintUnboundImport
    {
        // Unity thread only: workers receive immutable values, never prefab objects or live callbacks.
        internal static UnboundImportContext Capture(GameObject prefab)
        {
            if (!prefab || !prefab.GetComponent<Piece>()) return null;
            var view = prefab.GetComponent<ZNetView>();
            if (!view) return null;
            var metadata = BlueprintPrefabMetadataCapture.Capture(prefab)?.ToGeometry();
            if (metadata == null || metadata.PrefabId != Utils.GetPrefabName(prefab)) return null;
            var sign = prefab.GetComponent<Sign>();
            return new UnboundImportContext(metadata, view.m_syncInitialScale,
                BlueprintStorageCapture.SupportsKgStateProjection(prefab) == true, sign && sign.GetType() == typeof(Sign),
                BlueprintMaterialState.Supports(prefab), BlueprintStorageCapture.PrefabGrid(prefab),
                (Utils.GetPrefabName(prefab).GetStableHashCode(), view.m_persistent, view.m_distant, (byte)view.m_type), ItemStandItems(prefab));
        }

        private static IEnumerable<UnboundItemStandItem> ItemStandItems(GameObject prefab)
        {
            var stands = prefab.GetComponentsInChildren<ItemStand>(true);
            var views = prefab.GetComponentsInChildren<ZNetView>(true);
            var pieces = prefab.GetComponentsInChildren<Piece>(true);
            if (stands.Length != 1 || stands[0].gameObject != prefab || stands[0].GetType() != typeof(ItemStand) ||
                views.Length != 1 || views[0].gameObject != prefab || pieces.Length != 1 || pieces[0].gameObject != prefab ||
                !ObjectDB.instance || ObjectDB.instance.m_items == null ||
                prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component => !component ||
                    component != stands[0] && !BlueprintStorageCapture.KgOrdinaryComponent(component.GetType()))) return null;
            var result = new List<UnboundItemStandItem>();
            foreach (var group in ObjectDB.instance.m_items.Where(item => item).Distinct().GroupBy(item => item.name.GetStableHashCode()))
            {
                if (group.Key == 0 || group.Count() != 1) continue;
                GameObject itemPrefab = group.Single();
                if (ObjectDB.instance.GetItemPrefab(group.Key) != itemPrefab || ObjectDB.instance.GetItemPrefab(itemPrefab.name) != itemPrefab) continue;
                var drop = itemPrefab.GetComponent<ItemDrop>();
                if (!drop || drop.GetType() != typeof(ItemDrop) || drop.m_itemData?.m_shared == null) continue;
                var item = drop.m_itemData;
                var offsets = new List<ItemStand.OrientationSettings>();
                if (item.m_shared.m_itemStandOffsets != null && ItemStand.GetAttachPrefab(itemPrefab) &&
                    BlueprintItemStandPreview.Supports(stands[0], item) && stands[0].GetAttach(item))
                    stands[0].GetOrientationSettings(item, ref offsets);
                var orientations = new List<int>();
                for (int index = 0; index < offsets.Count; ++index)
                    if (offsets[index] != null && BlueprintItemStandPlacement.CanRestore(prefab.GetComponent<Piece>(),
                        new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                            itemPrefab.name + ":0:1:" + index.ToString(CultureInfo.InvariantCulture), null, null))) orientations.Add(index);
                result.Add(new UnboundItemStandItem(itemPrefab.name, Math.Max(1, item.m_shared.m_variants),
                    Math.Max(0, item.m_shared.m_maxQuality), orientations));
            }
            return result;
        }
    }
}
