using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks;
using UnityEngine;
using Object=UnityEngine.Object;
internal static class AdmissionChecks
{
    internal static void Run()
    {
        int checks=0;var failures=new List<string>();var roots=new List<GameObject>();
        void Check(bool value,string why){++checks;if(!value)failures.Add(why);}
        GameObject Root(string name){var go=new GameObject(name);roots.Add(go);go.AddComponent<Piece>();go.AddComponent<ZNetView>();go.AddComponent<WearNTear>();return go;}
        void Append(GameObject go)=>VanillaMidpointSnapPoints.Append(go.GetComponent<Piece>(),new List<Transform>());
        try
        {
            var item=new GameObject("SwordIron");roots.Add(item);item.AddComponent<ItemDrop>();ObjectDB.instance.m_items.Add(item);
            var chest=Root("chest");var container=chest.AddComponent<Container>();var wear=chest.GetComponent<WearNTear>();var view=chest.GetComponent<ZNetView>();container.Bind(view,wear);
            Check(BlueprintConstructionContainerGuard.NativeRoot(chest,wear),"native chest fixture denied before Append");Append(chest);
            Check(BlueprintConstructionContainerGuard.NativeRoot(chest,wear),"P1 chest NativeRoot changed after production Append");
            Check(BlueprintStorageCapture.SupportsKgStateProjection(chest)==false,"special chest promoted to KG ordinary");
            var stand=Root("stand");var holder=stand.AddComponent<ItemStand>();var sw=stand.GetComponent<WearNTear>();var sv=stand.GetComponent<ZNetView>();holder.Bind(sv,sw);
            Check(AdmissionProbe.NativeStandRoot(stand,sw,sv),"native stand fixture denied before Append");
            var before=BlueprintUnboundImport.ItemStandItems(stand)?.ToArray();Check(before?.Length==1,"Unbound fixture lacks real admitted item metadata");Append(stand);
            Check(AdmissionProbe.NativeStandRoot(stand,sw,sv),"P1 stand NativeStandRoot changed after production Append");
            var after=BlueprintUnboundImport.ItemStandItems(stand)?.ToArray();Check(after!=null&&before.SequenceEqual(after),"P1 Unbound ItemStandItems changed after production Append");
            Check(BlueprintStorageCapture.SupportsKgStateProjection(stand)==false,"special stand promoted to KG ordinary");
            var floor=Root("floor");Check(BlueprintStorageCapture.SupportsKgStateProjection(floor)==true,"KG fixture denied before Append");Append(floor);
            Check(BlueprintStorageCapture.SupportsKgStateProjection(floor)==true,"P1 KG ordinary changed after production Append");
            var foreign=chest.AddComponent<BuildWorks_ForeignBehaviour>();Check(!BlueprintConstructionContainerGuard.NativeRoot(chest,wear),"foreign root behaviour admitted to chest");Object.DestroyImmediate(foreign);
            foreign=stand.AddComponent<BuildWorks_ForeignBehaviour>();Check(!AdmissionProbe.NativeStandRoot(stand,sw,sv)&&BlueprintUnboundImport.ItemStandItems(stand)==null,"foreign root behaviour admitted to stand/Unbound");Object.DestroyImmediate(foreign);
            foreign=floor.AddComponent<BuildWorks_ForeignBehaviour>();Check(BlueprintStorageCapture.SupportsKgStateProjection(floor)==false,"same-namespace foreign behaviour admitted to KG");Object.DestroyImmediate(foreign);
            foreach(var root in new[]{chest,stand,floor})
            {
                var child=new GameObject("child");child.transform.SetParent(root.transform,false);child.AddComponent<VanillaMidpointSnapPoints>();
                Check(root==chest?!BlueprintConstructionContainerGuard.NativeRoot(chest,wear):root==stand?!AdmissionProbe.NativeStandRoot(stand,sw,sv)&&BlueprintUnboundImport.ItemStandItems(stand)==null:BlueprintStorageCapture.SupportsKgStateProjection(floor)==false,"child cache granted root ownership");Object.DestroyImmediate(child);
            }
            var duplicate=chest.AddComponent<Piece>();Check(!BlueprintConstructionContainerGuard.NativeRoot(chest,wear),"native four-count weakened by duplicate Piece");Object.DestroyImmediate(duplicate);
            wear.m_onDestroyed+=()=>{};Check(!BlueprintConstructionContainerGuard.NativeRoot(chest,wear),"additional native callback admitted");container.Bind(view,wear);
            sw.Bind(view);Check(!AdmissionProbe.NativeStandRoot(stand,sw,sv),"foreign view admitted to stand");holder.Bind(sv,sw);
            var duplicateCache=floor.AddComponent<VanillaMidpointSnapPoints>();Check(BlueprintStorageCapture.SupportsKgStateProjection(floor)==false,"second cache admitted without exact root identity");Object.DestroyImmediate(duplicateCache);
            if(failures.Count!=0)throw new InvalidOperationException(string.Join("; ",failures));
            Console.WriteLine($"PASS {checks} actual Append→Container/Stand/KG/Unbound admission checks; exact native counts/callbacks/views/root identity/foreign denial retained; inert host only.");
        }
        finally {ObjectDB.instance.m_items.Clear();foreach(var root in roots)Object.DestroyImmediate(root);}
    }
}
