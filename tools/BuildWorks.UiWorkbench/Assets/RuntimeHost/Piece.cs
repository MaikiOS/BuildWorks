using System.Collections.Generic;
using UnityEngine;

// External host contract only. A runtime assembly is required for AddComponent
// in native snap fixtures; the actual editor implementation remains unchanged.
public sealed class Piece : MonoBehaviour
{
    public class Requirement { public ItemDrop m_resItem; public int m_amount; }
    public void GetSnapPoints(List<Transform> points)
    {
        foreach (Transform child in transform)
            if (child.tag == "snappoint") points.Add(child);
    }
}

public class ItemDrop : MonoBehaviour
{
    public readonly ItemData m_itemData = new ItemData();
    public class ItemData
    {
        public readonly SharedData m_shared = new SharedData();
        public GameObject m_dropPrefab;
        public readonly Dictionary<string, string> m_customData = new Dictionary<string, string>();
        public int m_stack = 1, m_quality = 1, m_worldLevel, m_variant;
        public float m_durability;
        public bool m_pickedUp, m_equipped, m_cheated;
        public Vector2Int m_gridPos;
        public long m_crafterID;
        public string m_crafterName = "";
        public enum ItemType { None, OneHandedWeapon }
        public Vector3 GetScale(float quality) => Vector3.one * (1f + (quality - 1f) * m_shared.m_scaleByQuality);
        public Sprite Icon;
        public Sprite GetIcon() => Icon;
        public ItemData Clone() => (ItemData)MemberwiseClone();
        // Native codec fixture, independently pinned to the installed ItemData.Save IL.
        public void Save(ZPackage package)
        {
            using (var writer = package.Writer)
            {
                int flags = (m_pickedUp ? 1 : 0) | (m_equipped ? 2 : 0) | (m_quality != 1 ? 4 : 0) |
                    (m_stack != 1 ? 8 : 0) | (m_variant != 0 ? 16 : 0) | (m_crafterID != 0 ? 32 : 0) |
                    (m_dropPrefab ? 64 : 0) | (m_customData.Count != 0 ? 128 : 0);
                writer.Write((int)(m_durability * 100)); writer.Write((byte)m_gridPos.x);
                writer.Write((byte)m_gridPos.y); writer.Write((byte)m_worldLevel); writer.Write((byte)flags);
                if (m_quality != 1) writer.Write((ushort)m_quality);
                if (m_stack != 1) writer.Write((ushort)m_stack);
                if (m_variant != 0) writer.Write(m_variant);
                if (m_crafterID != 0) { writer.Write(m_crafterID); writer.Write(m_crafterName); }
                if (m_dropPrefab) writer.Write(m_dropPrefab.name.GetStableHashCode());
                if (m_customData.Count != 0)
                {
                    writer.Write((byte)m_customData.Count);
                    foreach (var pair in m_customData) { writer.Write(pair.Key); writer.Write(pair.Value); }
                }
                writer.Write((byte)(m_cheated ? 1 : 0));
            }
        }
    }
    public class SharedData
    {
        public string m_name = "$wood";
        public int m_maxQuality = 1;
        public int m_variants = 1;
        public float m_scaleByQuality;
        public ItemData.ItemType m_itemType;
        public List<ItemStand.OrientationSettings> m_itemStandOffsets = new List<ItemStand.OrientationSettings>();
    }
}
