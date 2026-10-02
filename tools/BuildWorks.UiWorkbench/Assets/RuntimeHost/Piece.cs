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
        public Sprite Icon;
        public Sprite GetIcon() => Icon;
    }
    public class SharedData { public string m_name = "$wood"; }
}
