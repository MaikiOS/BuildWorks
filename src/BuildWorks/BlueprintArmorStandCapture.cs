using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal static class BlueprintArmorStandCapture
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static readonly FieldInfo View = typeof(ArmorStand).GetField("m_nview", Private);
        private static readonly FieldInfo Queue = typeof(ArmorStand).GetField("m_queuedItem", Private);
        private static readonly FieldInfo Pose = typeof(ArmorStand).GetField("m_pose", Private);

        internal static bool TryCapture(ArmorStand stand, out BlueprintForeignPartData data)
        {
            data = null;
            if (!stand || stand.GetType() != typeof(ArmorStand) || View == null || Queue == null || Pose == null ||
                Queue.GetValue(stand) != null || stand.m_slots == null || stand.m_slots.Count > BlueprintArmorStandState.MaximumSlots)
                return false;
            var view = View.GetValue(stand) as ZNetView;
            if (!view || !view.IsValid()) return false;
            ZDO zdo = view.GetZDO();
            if (zdo == null) return false;
            uint revision = zdo.DataRevision;
            int fallbackPose = (int)Pose.GetValue(stand);
            var layout = stand.m_slots;
            var descriptors = layout.ToArray();
            if (descriptors.Any(slot => slot == null)) return false;
            int[] visualSlots = descriptors.Select(slot => (int)slot.m_slot).ToArray();
            int pose = zdo.GetInt(ZDOVars.s_pose, fallbackPose);
            var slots = new BlueprintArmorStandState.Slot[descriptors.Length];
            int bytes = 1;
            try
            {
                for (int i = 0; i < slots.Length; ++i)
                {
                    string prefix = i.ToString(CultureInfo.InvariantCulture) + "_";
                    int id = zdo.GetInt((prefix + "item").GetStableHashCode(), 0);
                    int variant = zdo.GetInt((prefix + "variant").GetStableHashCode(), 0);
                    byte[] packet = zdo.GetByteArray((prefix + "itemData").GetStableHashCode(), null);
                    if (packet?.Length > BlueprintArmorStandState.MaximumItemBytes) return false;
                    bytes += 532 + (packet?.Length ?? 0);
                    if (bytes > BlueprintForeignPartData.MaximumContainerBytes) return false;
                    packet = packet == null ? null : (byte[])packet.Clone();
                    GameObject item = id != 0 && ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(id) : null;
                    string name = item && item.GetComponent<ItemDrop>() && item.name.GetStableHashCode() == id ? item.name : null;
                    slots[i] = new BlueprintArmorStandState.Slot(visualSlots[i], id, name, variant, packet);
                }
                var snapshot = new BlueprintArmorStandState(pose, slots).ToForeignData();
                if (!stand || !view || !view.IsValid() || !ReferenceEquals(View.GetValue(stand), view) ||
                    !ReferenceEquals(view.GetZDO(), zdo) || zdo.DataRevision != revision || Queue.GetValue(stand) != null ||
                    (int)Pose.GetValue(stand) != fallbackPose || !ReferenceEquals(stand.m_slots, layout) || layout.Count != descriptors.Length)
                    return false;
                for (int i = 0; i < descriptors.Length; ++i)
                    if (!ReferenceEquals(layout[i], descriptors[i]) || (int)layout[i].m_slot != visualSlots[i]) return false;
                data = snapshot; return true;
            }
            catch (ArgumentException) { return false; }
        }
    }
}
