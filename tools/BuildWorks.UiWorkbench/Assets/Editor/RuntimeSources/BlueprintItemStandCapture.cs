using System;
using System.Globalization;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Capture and inert inspection only: no native Load, inventory consumption or ZDO writes.
    internal static class BlueprintItemStandCapture
    {
        private static readonly FieldInfo StandView = typeof(ItemStand).GetField(
            "m_nview", BindingFlags.Instance | BindingFlags.NonPublic);

        internal static bool TryCapture(Piece piece, out BlueprintForeignPartData data)
        {
            data = null;
            if (!piece) return false;
            // Armour contents and pose need a separate native slot adapter.
            if (piece.GetComponent<ArmorStand>()) return false;
            ItemStand stand = piece.GetComponent<ItemStand>();
            if (!stand) return true;
            var view = StandView?.GetValue(stand) as ZNetView;
            if (!view || !view.IsValid()) return false;
            ZDO zdo = view.GetZDO();
            if (zdo == null) return false;
            int hash = stand.GetAttachedItem();
            if (hash == 0) return true;
            GameObject item = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(hash) : null;
            if (!item || !item.GetComponent<ItemDrop>()) return false;
            int variant = zdo.GetInt(ZDOVars.s_variant, 0);
            int quality = zdo.GetInt(ZDOVars.s_quality, 1);
            int orientation = zdo.GetInt(ZDOVars.s_type, 0);
            byte[] package = zdo.GetByteArray(ZDOVars.s_itemData, null);
            // Bound before Base64 allocation; the immutable-state limit is 32768 chars.
            if (package != null && package.Length > 24576) return false;
            try
            {
                string info = item.name + ":" + variant.ToString(CultureInfo.InvariantCulture) + ":" +
                    quality.ToString(CultureInfo.InvariantCulture) + ":" + orientation.ToString(CultureInfo.InvariantCulture);
                var snapshot = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, info,
                    package == null ? null : Convert.ToBase64String(package), null);
                if (package != null && !BlueprintItemStandContents.TryRead(snapshot, name => name.GetStableHashCode(), out _))
                    return false;
                data = snapshot;
                return true;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
