using System.Collections.Generic;
using UnityEngine;

// Only the read-only host contract, checked against current Valheim IL.
// Inert ZDO reads only; no RPC, inventory or scripted attachment path.
public class ItemStand : MonoBehaviour
{
    private ZNetView m_nview = null;
    public int GetAttachedItem() => m_nview && m_nview.IsValid() ? m_nview.GetZDO().GetInt(ZDOVars.s_item, 0) : 0;
    public enum Orientation { None = 0, Vertical = 1, Horizontal = 2, All = 3 }
    public class OrientationSettings
    {
        public Vector3 m_position, m_rotation;
        public Vector3 m_scale = Vector3.one;
        public Orientation m_orientations = Orientation.All;
    }
    public Transform m_attachOther;
    public Orientation m_orientationType;
    public List<ItemDrop.ItemData.ItemType> m_supportedTypes = new List<ItemDrop.ItemData.ItemType>();
    public List<ItemDrop> m_supportedItems = new List<ItemDrop>();
    public List<ItemDrop> m_unsupportedItems = new List<ItemDrop>();
    public Transform GetAttach(ItemDrop.ItemData item) => m_attachOther;
    public static GameObject GetAttachPrefab(GameObject prefab) => prefab.transform.Find("attach")?.gameObject;
    public static GameObject GetAttachGameObject(GameObject attach) => attach.transform.Find("attachobj")?.gameObject ?? attach;
    public bool IsUnsupported(ItemDrop.ItemData item) => m_unsupportedItems.Exists(drop => drop.m_itemData.m_shared.m_name == item.m_shared.m_name);
    public bool IsSupported(ItemDrop.ItemData item) => m_supportedItems.Count == 0 || m_supportedItems.Exists(drop => drop.m_itemData.m_shared.m_name == item.m_shared.m_name);
    public void GetOrientationSettings(ItemDrop.ItemData item, ref List<OrientationSettings> settings)
    {
        settings.Clear();
        foreach (OrientationSettings offset in item.m_shared.m_itemStandOffsets)
            if ((m_orientationType & offset.m_orientations) != 0) settings.Add(offset);
        if (settings.Count == 0) settings.Insert(0, new OrientationSettings());
    }
}

internal interface IEquipmentVisual { void Setup(int variant); }
public class ItemStyle : MonoBehaviour, IEquipmentVisual
{
    public void Setup(int variant) => throw new System.InvalidOperationException("Scripted item Setup must never execute in the editor.");
}
public class UnsupportedItemVisual : MonoBehaviour, IEquipmentVisual
{
    public void Setup(int variant) => throw new System.InvalidOperationException("Unknown equipment script executed.");
}
public class ParticleIntensityScaler : MonoBehaviour
{
    public void SetQuality(int quality) => throw new System.InvalidOperationException("Particle quality script executed.");
}

public class ObjectDB
{
    public static ObjectDB instance;
    public readonly Dictionary<string, GameObject> Items = new Dictionary<string, GameObject>();
    public readonly Dictionary<int, GameObject> ItemHashes = new Dictionary<int, GameObject>();
    public GameObject GetItemPrefab(string name) => Items.TryGetValue(name, out GameObject item) ? item : null;
    public GameObject GetItemPrefab(int hash) => ItemHashes.TryGetValue(hash, out GameObject item) ? item : null;
    public static implicit operator bool(ObjectDB value) => value != null;
}
public class ArmorStand : MonoBehaviour { }
