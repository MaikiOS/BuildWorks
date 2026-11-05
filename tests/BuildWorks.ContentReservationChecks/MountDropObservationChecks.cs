using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal sealed class Rigidbody : Obj
    {
        internal bool isKinematic;
        internal ItemDrop Drop;
        internal T GetComponent<T>() where T:class => Drop as T;
    }
    internal sealed class Collider : Obj
    {
        internal Rigidbody attachedRigidbody;
        internal GameObject Owner;
        internal Vector3 Point;
        internal bool enabled=true,isTrigger;
        internal T GetComponentInParent<T>() where T:class=>Owner?.GetComponent<T>();
        internal Vector3 ClosestPoint(Vector3 _) => Point;
    }
    internal static class Physics { internal static Collider[] Nearby=Array.Empty<Collider>(); internal static Collider[] OverlapSphere(Vector3 p,float r,int m)=>Nearby; }
    internal static class LayerMask { internal static int GetMask(string n)=>1; }
    internal static class Utils { internal static string GetPrefabName(GameObject go)=>go.name; }
    internal sealed partial class BlueprintConstructionPayoutJournal
    {
        internal const int Prepared=1,Attempted=1;
        internal const string CustodyKey="bw_test_material_custody";
        internal static readonly int ItemMarkerKey="bw_test_material_drop".GetStableHashCode();
        internal sealed class Entry { internal Guid Id; internal int State=Attempted;internal string Blob; }
        internal sealed class Record { internal Guid Project=Guid.NewGuid(),Operation=Guid.NewGuid();internal long Player=100;internal int Phase=Prepared;internal Entry[] Items; }
        internal Record Value;
        internal Record Read(Guid id)=>Value;
    }
    internal sealed partial class ItemDrop
    {
        internal GameObject gameObject;
        internal Transform transform=>gameObject.transform;
        internal ItemData m_itemData;
        internal bool m_autoPickup=true,m_autoDestroy=true,enabled=true;
        internal static Action BeforeNativeDrop;
        internal T GetComponent<T>() where T:class=>gameObject.GetComponent<T>();
        internal static ItemDrop Make(ItemData item)
        {
            var go=new GameObject {name=item.m_dropPrefab.name,RootView=new ZNetView()};
            go.RootView.Data.PrefabHash=item.m_dropPrefab.name.GetStableHashCode();
            var drop=new ItemDrop {gameObject=go,m_itemData=item};go.Drop=drop;go.Body=new Rigidbody {Drop=drop};return drop;
        }
        internal static ItemDrop DropItem(ItemData item,int amount,Vector3 position,Quaternion rotation)
        {
            BeforeNativeDrop?.Invoke();
            BlueprintConstructionPayoutSeal.BeforeDrop(item);
            var drop=Make(item.Clone());BlueprintConstructionPayoutSeal.BeforeAwake(drop);BlueprintConstructionPayoutSeal.AfterAwake(drop,null);
            var data=drop.GetComponent<ZNetView>().Data;
            if(BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,data,-1))data.Set(ZDOVars.s_itemData,BlueprintConstructionPayoutSeal.Package(drop.m_itemData));
            return drop;
        }
        internal static void LoadFromZDO(ItemData item,ZDO data)
        {
            byte[] packet=data.GetByteArray(ZDOVars.s_itemData);
            if(packet==null)return;
            byte[] inventory;
            using(var stream=new MemoryStream())using(var w=new BinaryWriter(stream)){
                w.Write((int)packet[0]);w.Write((ushort)1);w.Write(packet,1,packet.Length-1);inventory=stream.ToArray();
            }
            var package=new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,"8:8:1:1|"+item.m_dropPrefab.name,Convert.ToBase64String(inventory),null);
            if(!BlueprintContainerInventory.TryRead(package,n=>n.GetStableHashCode(),out var rows))throw new Exception("invalid fake native load input");
            var row=rows.Single();item.m_gridPos=new Vector2i(row.X,row.Y);item.m_worldLevel=row.WorldLevel;item.m_stack=row.Stack;
            item.m_quality=row.Quality;item.m_variant=row.Variant;item.m_pickedUp=row.PickedUp;item.m_equipped=row.Equipped;item.m_cheated=row.Cheated;
            item.m_durability=(float)row.DurabilityHundredths.Value*0.01f;item.m_crafterID=row.CrafterId;item.m_crafterName=row.CrafterName;
            item.m_customData=row.CustomData.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        }
    }
    internal static class MountDropObservationChecks
    {
        private static int assertions;
        private static void Check(bool value,string why){assertions++;if(!value)throw new InvalidOperationException("Mount drop observation: "+why);}
        private sealed class Rig:IDisposable
        {
            internal readonly TransferChecks.Frame Frame=new TransferChecks.Frame();
            internal readonly ItemDrop.ItemData Item=Program.Item("SwordIron",1,"actual-source");
            internal readonly GameObject Holder=new GameObject {name="itemstand",RootView=new ZNetView()};
            internal readonly Guid Operation=Guid.NewGuid();
            internal BlueprintForeignPartData Original;
            internal BlueprintConstructionPayoutSeal.MountDropObservation Observation;
            internal bool Intent=true,FailBind;
            internal int BindCalls;
            internal readonly List<ItemDrop> Created=new List<ItemDrop>();
            internal Rig()
            {
                SaveSystem.Flags=0;SaveSystem.Generation=7;ZNet.Open=false;BlueprintConstructionProjectRuntime.Admin=true;
                Holder.Piece=new Piece {gameObject=Holder};Holder.Stand=new ItemStand(Holder.RootView){gameObject=Holder};
                Item.m_quality=2;Item.m_durability=94.12f;Item.m_gridPos=new Vector2i(5,4);Item.m_crafterID=777;Item.m_crafterName="Острикс";
                Item.m_cheated=true;Original=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"SwordIron:0:2:0",BlueprintItemStandPlacement.SnapshotItem(Item),null);
                Holder.Stand.Contents=Original;Holder.RootView.Data.Set(ZDOVars.s_itemData,Convert.FromBase64String(Original.Zdo));
            }
            internal bool Begin()=>BlueprintConstructionPayoutSeal.TryObserveMountDrop(Frame.Player,Holder.Stand,Operation,
                actual=>Intent&&actual.Info==Original.Info&&actual.Zdo==Original.Zdo,drop=>{
                    BindCalls++;Check(drop.GetComponent<ZNetView>().Data.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey,"")=="","identity was not persisted before normal marker");
                    if(FailBind)throw new IOException("identity write failed");
                },out Observation,out _);
            internal ItemDrop Spawn()
            {
                var drop=ItemDrop.Make(Program.Item("SwordIron",1,"prefab-default"));Created.Add(drop);
                BlueprintConstructionPayoutSeal.BeforeAwake(drop);BlueprintConstructionPayoutSeal.AfterAwake(drop,null);return drop;
            }
            internal Exception Load(ItemDrop drop,bool mutate=false)
            {
                BlueprintConstructionPayoutSeal.MountDropObservation state=null;Exception error=null;
                try{
                    state=BlueprintConstructionPayoutSeal.BeginMountDropLoad(drop,Holder.RootView.Data);
                    ItemDrop.LoadFromZDO(drop.m_itemData,Holder.RootView.Data);if(mutate)drop.m_itemData.Custom="wrong-after-load";
                    var data=drop.GetComponent<ZNetView>().Data;
                    if(BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,data,-1))data.Set(ZDOVars.s_itemData,BlueprintConstructionPayoutSeal.Package(drop.m_itemData));
                }catch(Exception failure){error=failure;}
                return BlueprintConstructionPayoutSeal.EndMountDropLoad(state,drop,error);
            }
            public void Dispose()
            {
                if(Observation!=null&&!Observation.HandlerFinished){Observation.Dispose();BlueprintConstructionPayoutSeal.BeginMountDropHandler(Holder.Stand,out var s);BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null);}
                Observation?.Dispose();foreach(var drop in Created)BlueprintConstructionPayoutSeal.Destroyed(drop);
                Physics.Nearby=Array.Empty<Collider>();PrivateArea.Allowed=true;SaveSystem.Flags=0;
            }
        }
        internal static void Run()
        {
            using(var r=new Rig()){
                Check(r.Begin(),"valid observation refused");Check(r.BindCalls==0,"observation performed native creation/binding");
                Check(BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var handler),"owner handler denied");
                var drop=r.Spawn();Check(r.BindCalls==1&&BlueprintConstructionPayoutSeal.IsHeld(drop),"early identity or hold missing");
                Check(!BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,drop.GetComponent<ZNetView>().Data,-1),"prefab defaults overwrote source packet");
                Check(r.Load(drop)==null,"actual source load refused");r.Holder.Stand.Contents=null;
                Check(BlueprintConstructionPayoutSeal.EndMountDropHandler(handler,null)==null&&r.Observation.Complete,"normal owner result not observed");
                Check(!BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var repeated)&&repeated==null,"duplicate completed handler acquired finalizer state");
                BlueprintConstructionPayoutSeal.EndMountDropHandler(repeated,null);
                Check(r.Observation.Complete&&r.BindCalls==1,"refused duplicate altered a completed observation");
                Check(r.Observation.Observed.Zdo!=r.Original.Zdo&&r.Original.Zdo==BlueprintItemStandPlacement.SnapshotItem(r.Item),"native load bytes conflated or original rewritten");
                Check(drop.m_itemData.Custom=="actual-source"&&!drop.m_itemData.m_customData.ContainsKey(BlueprintConstructionPayoutJournal.CustodyKey),"return injected a material custody tag");
                Check(!drop.m_autoPickup&&!drop.m_autoDestroy&&drop.GetComponent<Rigidbody>().isKinematic&&!BlueprintConstructionPayoutSeal.AllowStack(drop),"actual drop protection incomplete");
                var ordinary=ItemDrop.Make(Program.Item("SwordIron",1));Physics.Nearby=new[]{new Collider {attachedRigidbody=drop.GetComponent<Rigidbody>()}};
                Check(!BlueprintConstructionPayoutSeal.AllowStack(ordinary),"unsealed neighbour can stack a held return");
                Check(BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,drop.GetComponent<ZNetView>().Data,-1),"same observed packet cannot save");
                drop.m_itemData.Custom="changed";Check(!BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,drop.GetComponent<ZNetView>().Data,-1),"held packet mutation allowed");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"cancel control refused");r.Observation.Dispose();
                Check(!BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var s),"cancelled queued request entered native handler");
                BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null);Check(r.BindCalls==0&&!r.Observation.Complete,"cancelled request claims a source");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"wrong-source control refused");BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var s);var drop=r.Spawn();
                Check(BlueprintConstructionPayoutSeal.BeginMountDropLoad(drop,new ZDO())==null,"different source ZDO granted a load binding");
                Check(BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null)!=null&&!r.Observation.Complete,"unrelated load settled return");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"duplicate control refused");BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var s);var a=r.Spawn();r.Spawn();
                Check(r.Load(a)!=null&&BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null)!=null&&!r.Observation.Complete,"duplicate receiver accepted");
            }
            using(var r=new Rig()){
                r.FailBind=true;Check(r.Begin(),"bind failure setup refused");BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var s);var drop=r.Spawn();
                Check(r.Load(drop)!=null&&BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null)!=null&&!r.Observation.IdentityBound,"failed bind authorized native packet");
                Check(r.BindCalls==1&&!drop.enabled&&drop.GetComponent<ZNetView>().Data.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey,"")==r.Operation.ToString("N"),"failed binding retried or orphan lost quarantine marker");
                Check(!BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,drop.GetComponent<ZNetView>().Data,-1),"unbound default drop may become ordinary saved loot");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"property failure setup refused");BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var s);var drop=r.Spawn();
                Check(r.Load(drop,mutate:true)!=null&&BlueprintConstructionPayoutSeal.EndMountDropHandler(s,null)!=null&&!r.Observation.Complete,"changed native loaded properties accepted");
            }
            using(var r=new Rig()){
                var item=Program.Item("Wood",2,"material");var id=Guid.NewGuid();item.m_customData[BlueprintConstructionPayoutJournal.CustodyKey]=id.ToString("N");
                var entry=new BlueprintConstructionPayoutJournal.Entry{Id=id,Blob=Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(item))};
                var record=new BlueprintConstructionPayoutJournal.Record{Items=new[]{entry}};var journal=new BlueprintConstructionPayoutJournal{Value=record};
                var drop=BlueprintConstructionPayoutSeal.Create(journal,record,0,item,new Vector3());r.Created.Add(drop);
                Check(BlueprintConstructionPayoutSeal.IsHeld(drop)&&drop.GetComponent<ZNetView>().Data.GetString(BlueprintConstructionPayoutSeal.MarkerKey,"")==id.ToString("N")&&
                    drop.GetComponent<ZNetView>().Data.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey,"")=="","material seal path changed");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"material-overlap control refused");
                Check(BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var handler),"overlap handler refused");
                var item=Program.Item("SwordIron",1);var id=Guid.NewGuid();item.m_customData[BlueprintConstructionPayoutJournal.CustodyKey]=id.ToString("N");
                var entry=new BlueprintConstructionPayoutJournal.Entry{Id=id,Blob=Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(item))};
                var record=new BlueprintConstructionPayoutJournal.Record{Items=new[]{entry}};var journal=new BlueprintConstructionPayoutJournal{Value=record};
                int nativeCalls=0;ItemDrop.BeforeNativeDrop=()=>{nativeCalls++;throw new InvalidOperationException("native boundary reached");};
                try{BlueprintConstructionPayoutSeal.Create(journal,record,0,item,new Vector3());}
                catch(InvalidOperationException){}
                finally{ItemDrop.BeforeNativeDrop=null;}
                Check(nativeCalls==0,"material native creation entered the mounted handler lease");
                Check(r.BindCalls==0&&!r.Observation.HandlerFinished,"refused material creation altered the observation");
                BlueprintConstructionPayoutSeal.EndMountDropHandler(handler,null);
            }
            using(var r=new Rig()){
                Check(r.Begin(),"reentrant control refused");
                Check(BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var outer),"outer handler refused");
                Check(!BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Holder.Stand,out var inner),"nested handler entered native body");
                BlueprintConstructionPayoutSeal.EndMountDropHandler(inner,null);
                var drop=r.Spawn();
                Check(BlueprintConstructionPayoutSeal.IsHeld(drop),"nested finalizer removed the outer handler seal");
                Check(!r.Observation.HandlerFinished,"nested finalizer finished the outer handler");
                Check(r.Load(drop)!=null&&BlueprintConstructionPayoutSeal.EndMountDropHandler(outer,null)!=null&&!r.Observation.Complete,"reentrant handler settled a return");
            }
            Check(BlueprintConstructionPayoutSeal.BeginMountDropHandler(null,out var none)&&none==null,"no request alters ordinary handler");
            Console.WriteLine("PASS mounted-drop observation: "+assertions+" actual seal/lease assertions, inert native hooks/objects and intent callbacks; no native RPC, return journal or credit execution");
        }
    }
}
