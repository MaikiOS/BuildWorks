using System.Collections.Generic;
using UnityEngine;

// Inert host fixtures checked against current Valheim IL; no real RPC/world execution.
public class ItemStand : MonoBehaviour
{
    private ZNetView m_nview = null;
    private ItemDrop.ItemData m_queuedItem;
    private ItemDrop m_visualItemDrop;
    private List<OrientationSettings> m_orientationSettings = new List<OrientationSettings>();
    public System.Action OnUse, AfterWrite, AfterConsume, OnInteract;
    public bool RejectUse, SkipConsume, DelayDestroy;
    public int Uses, Attaches, Destroys;
    public bool HaveAttachment() => GetAttachedItem() != 0;
    public int GetOrientation() => m_nview.GetZDO().GetInt(ZDOVars.s_type, 0);
    public bool UseItem(Humanoid player, ItemDrop.ItemData item)
    {
        ++Uses;
        if (HaveAttachment()) return false;
        if (RejectUse) return true;
        m_queuedItem = item;
        InvokeRepeating("UpdateAttach", 0, .1f);
        OnUse?.Invoke();
        return true;
    }
    private void UpdateAttach()
    {
        if (!m_nview.IsOwner()) return;
        CancelInvoke("UpdateAttach");
        if (m_queuedItem != null && Player.m_localPlayer && Player.m_localPlayer.GetInventory().ContainsItem(m_queuedItem) && !HaveAttachment())
        {
            ++Attaches;
            ItemDrop.ItemData copy = m_queuedItem.Clone(); copy.m_stack = 1;
            ZDO data = m_nview.GetZDO();
            data.Set(ZDOVars.s_item, copy.m_dropPrefab.name.GetStableHashCode());
            AfterWrite?.Invoke();
            var package = new ZPackage(); package.Write((byte)109); copy.Save(package);
            data.Set(ZDOVars.s_itemData, package.GetArray());
            data.Set(ZDOVars.s_variant, copy.m_variant); data.Set(ZDOVars.s_quality, copy.m_quality);
            if (!SkipConsume) Player.m_localPlayer.GetInventory().RemoveOneItem(m_queuedItem);
            AfterConsume?.Invoke();
            m_visualItemDrop = copy.m_dropPrefab.GetComponent<ItemDrop>();
            GetOrientationSettings(copy, ref m_orientationSettings);
        }
        m_queuedItem = null;
    }
    public bool Interact(Humanoid player, bool hold, bool alt)
    {
        if (!PrivateArea.CheckAccess(transform.position, 0, false, false)) return true;
        if (!alt || hold || !m_visualItemDrop || m_orientationSettings.Count < 2)
            throw new System.InvalidOperationException("Fixture refuses unrelated native interaction branches");
        OnInteract?.Invoke();
        if (!m_nview.IsOwner())
        {
            InvokeRepeating("UpdateOrientation", 0, .1f);
            return true;
        }
        m_nview.GetZDO().Set(ZDOVars.s_type, (GetOrientation() + 1) % m_orientationSettings.Count);
        return false;
    }
    private void UpdateOrientation() { if (m_nview.IsOwner()) CancelInvoke("UpdateOrientation"); }
    public void DestroyAttachment()
    {
        ++Destroys;
        if (!DelayDestroy) m_nview.GetZDO().Set(ZDOVars.s_item, 0);
    }
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
public class ArmorStand : MonoBehaviour
{
    // Read-side shape only; actual capture behavior is covered by the CPU host fixture.
    public sealed class ArmorStandSlot { public int m_slot; }
    public List<ArmorStandSlot> m_slots = new List<ArmorStandSlot>();
}
