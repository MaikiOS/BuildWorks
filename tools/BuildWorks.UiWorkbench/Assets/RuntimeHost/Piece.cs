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
        public enum ItemType { None, OneHandedWeapon }
        public Vector3 GetScale(float quality) => Vector3.one * (1f + (quality - 1f) * m_shared.m_scaleByQuality);
        public Sprite Icon;
        public Sprite GetIcon() => Icon;
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
