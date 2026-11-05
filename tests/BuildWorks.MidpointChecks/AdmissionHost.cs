using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class Inventory {internal Action m_onChanged;}
public class WearNTear:MonoBehaviour
{
    private ZNetView m_nview;
    public Action m_onDestroyed;
    public void Bind(ZNetView view){m_nview=view;}
    public ZNetView TestView=>m_nview;
}
public class Container:MonoBehaviour
{
    private ZNetView m_nview;
    public ZNetView m_rootObjectOverride=null;
    private readonly Inventory inventory=new Inventory();
    public Inventory GetInventory()=>inventory;
    private void OnDestroyed(){}
    private void OnContainerChanged(){}
    public void Bind(ZNetView view,WearNTear wear){m_nview=view;wear.Bind(view);wear.m_onDestroyed=OnDestroyed;inventory.m_onChanged=OnContainerChanged;}
    public ZNetView TestView=>m_nview;
}
public class ItemStand:MonoBehaviour
{
    private void OnDestroyed(){}
    public void Bind(ZNetView view,WearNTear wear){wear.Bind(view);wear.m_onDestroyed=OnDestroyed;}
    public class OrientationSettings{}
    public static GameObject GetAttachPrefab(GameObject item)=>null;
    public bool GetAttach(ItemDrop.ItemData item)=>false;
    public void GetOrientationSettings(ItemDrop.ItemData item,ref List<OrientationSettings> settings){}
}
public class ItemDrop:MonoBehaviour
{
    public ItemData m_itemData=new ItemData();
    public class ItemData {public Shared m_shared=new Shared();}
    public class Shared {public List<ItemStand.OrientationSettings> m_itemStandOffsets;public int m_variants=2,m_maxQuality=3;}
}
public class ObjectDB:UnityEngine.Object
{
    public static ObjectDB instance=new ObjectDB();public List<GameObject> m_items=new List<GameObject>();
    public GameObject GetItemPrefab(int id)=>m_items.SingleOrDefault(p=>p.name.GetStableHashCode()==id);
    public GameObject GetItemPrefab(string name)=>m_items.SingleOrDefault(p=>p.name==name);
}
public class Sign:MonoBehaviour{} public class RandomMaterialValues:MonoBehaviour{} public class SimpleMeshCombine:MonoBehaviour{}
namespace TMPro {public class TextMeshPro:MonoBehaviour{} public class TextMeshProUGUI:MonoBehaviour{}}
namespace UnityEngine.UI {public class CanvasScaler:MonoBehaviour{}}
namespace OstrixMods.BuildWorks
{
    internal static class BlueprintItemStandPreview {internal static bool Supports(ItemStand stand,ItemDrop.ItemData item)=>false;}
    internal static class BlueprintItemStandPlacement {internal static bool CanRestore(Piece piece,Geometry.BlueprintForeignPartData data)=>false;}
    // Deliberately in our namespace: namespace/prefix must never grant admission.
    internal sealed class BuildWorks_ForeignBehaviour:MonoBehaviour{}
}
