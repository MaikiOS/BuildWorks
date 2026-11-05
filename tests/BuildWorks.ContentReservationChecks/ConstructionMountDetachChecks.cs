using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountReturnIntent=1,MountReturnBound=2,MountReturnObserved=3,MountReturnSaved=4;
        internal sealed class MountReturnRecord {
            internal Guid Operation;internal int Phase;internal uint BeforeGeneration;
            internal string BeforeInfo,BeforeBytes,ObservedInfo,ObservedBytes;
            internal long ObjectUser;internal uint ObjectId;internal Guid SourceEpoch;
            internal SavePair Saved; internal string SavedDropHash;
            internal MountReturnCredit Credit; internal MountCleanup Cleanup;
        }
        internal bool ReturnBindFails,ReturnObserveFails,ReturnFailAfterBind;
        internal MountRecord PrepareMountReturn(Guid project,Guid part,Guid mountOp,Guid operation,long payer,uint generation,
            BlueprintForeignPartData before,BlueprintForeignPartData actual,out bool prepared)
        {
            if(Shared.Disk!=null){var diskRow=Shared.Disk.PrepareMountReturn(project,part,mountOp,operation,payer,generation,before,actual,out prepared);return Shared.Refresh(diskRow);}
            prepared=false;var row=Shared.Mount;
            if(row?.Phase!=Committed||row.Return!=null||row.Operation!=mountOp||row.Player!=payer||actual.Info!=row.MountedInfo||actual.Zdo!=row.MountedBytes)
                throw new InvalidOperationException("return replay/parent/source refused");
            row.Return=new MountReturnRecord {Operation=operation,Phase=MountReturnIntent,BeforeGeneration=generation,BeforeInfo=before.Info,BeforeBytes=before.Zdo};
            Shared.Events.Add("return-intent");prepared=true;return row;
        }
        internal MountRecord BindMountReturnIdentity(Guid project,Guid part,Guid operation,long user,uint id,Guid sourceEpoch=default)
        {
            if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.BindMountReturnIdentity(project,part,operation,user,id,sourceEpoch));
            if(Shared.ReturnBindFails)throw new InvalidOperationException("return identity write failed");
            var row=Shared.Mount;var pending=row.Return;
            if(pending?.Operation!=operation||pending.Phase!=MountReturnIntent)throw new InvalidOperationException("missing return intent");
            pending.SourceEpoch=sourceEpoch;pending.ObjectUser=user;pending.ObjectId=id;pending.Phase=MountReturnBound;Shared.Events.Add("return-bound");
            if(Shared.ReturnFailAfterBind)throw new InvalidOperationException("unknown result after durable identity");
            return row;
        }
        internal MountRecord ObserveMountReturn(Guid project,Guid part,Guid operation,BlueprintForeignPartData actual,bool emptyHolder)
        {
            if(Shared.Disk!=null && Shared.ReturnObserveFails)throw new InvalidOperationException("simulated durable bound crash");
            if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.ObserveMountReturn(project,part,operation,actual,emptyHolder));
            if(Shared.ReturnObserveFails)throw new InvalidOperationException("return observation write failed");
            var row=Shared.Mount;var pending=row.Return;
            if(!emptyHolder||pending?.Operation!=operation||pending.Phase!=MountReturnBound)throw new InvalidOperationException("missing bound drop");
            pending.ObservedInfo=actual.Info;pending.ObservedBytes=actual.Zdo;pending.Phase=MountReturnObserved;
            Shared.Events.Add("return-observed");return row;
        }
        internal void ConfirmMountReturnSaved(Guid project,Guid part,Guid operation,SavePair pair,string dropHash)
        {
            if(Shared.Disk!=null){Shared.Refresh(Shared.Disk.ConfirmMountReturnSaved(project,part,operation,Copy<OstrixMods.BuildWorks.BlueprintConstructionPartJournal.SavePair>(pair),dropHash));return;}
            if(Shared.ConfirmFails)throw new InvalidOperationException("return save confirmation failed");
            var row=Shared.Mount;
            if(row.Return?.Operation!=operation||row.Return.Phase!=MountReturnObserved)throw new InvalidOperationException("missing observed return");
            row.Return.Saved=pair;row.Return.SavedDropHash=dropHash;row.Return.Phase=MountReturnSaved;Shared.Events.Add("return-saved");
        }
    }
    internal sealed partial class ItemStand
    {
        internal int DetachCalls,OwnerBodies;
        internal string DetachMode;
        internal Action BeforeInteract,AfterLoad,AfterBody;
        internal readonly List<ItemDrop> Returned=new List<ItemDrop>();
        internal bool Interact(Player player,bool hold,bool alt)
        {
            DetachCalls++;
            if(!ReferenceEquals(player,Player.m_localPlayer)||!hold||alt)throw new InvalidOperationException("wrong native interaction arguments");
            BeforeInteract?.Invoke();
            if(DetachMode=="false")return false;
            if(DetachMode=="throw")throw new InvalidOperationException("native outcome unknown");
            if(DetachMode!="queued")Deliver();
            return true;
        }
        internal void Deliver()
        {
            Exception failure=null;
            bool run=BlueprintConstructionPayoutSeal.BeginMountDropHandler(this,out var handler);
            try
            {
                if(run){
                    OwnerBodies++;
                    var prefab=Contents.Info.Split(':')[0];
                    var drop=Spawn(prefab);
                    if(DetachMode=="duplicate")Spawn(prefab);
                    var load=BlueprintConstructionPayoutSeal.BeginMountDropLoad(drop,m_nview.GetZDO());
                    Exception loadError=null;
                    try{
                        ItemDrop.LoadFromZDO(drop.m_itemData,m_nview.GetZDO());AfterLoad?.Invoke();
                        var data=drop.GetComponent<ZNetView>().GetZDO();
                        if(BlueprintConstructionPayoutSeal.AllowSave(drop.m_itemData,data,-1))data.Set(ZDOVars.s_itemData,BlueprintConstructionPayoutSeal.Package(drop.m_itemData));
                    }catch(Exception error){loadError=error;}
                    var loaded=BlueprintConstructionPayoutSeal.EndMountDropLoad(load,drop,loadError);if(loaded!=null)throw loaded;
                    Contents=null;AfterBody?.Invoke();
                }
            }catch(Exception error){failure=error;}
            var final=BlueprintConstructionPayoutSeal.EndMountDropHandler(handler,failure);if(final!=null)throw final;
        }
        private ItemDrop Spawn(string prefab)
        {
            var drop=ItemDrop.Make(Program.Item(prefab,1,"prefab-default"));Returned.Add(drop);
            BlueprintConstructionPayoutSeal.BeforeAwake(drop);BlueprintConstructionPayoutSeal.AfterAwake(drop,null);return drop;
        }
    }
    internal static partial class ConstructionMountDetachChecks
    {
        private static int assertions;
        private static void Check(bool value,string why){assertions++;if(!value)throw new InvalidOperationException("Mount detach actor: "+why);}
        private static string InventorySnapshot(Inventory inventory)
        {
            if(!BlueprintStorageCapture.TrySnapshotInventory(inventory,out var snapshot))throw new InvalidOperationException("inventory snapshot unavailable");
            return snapshot.Info+"\n"+snapshot.Zdo;
        }
        private sealed class Rig:IDisposable
        {
            internal readonly ConstructionMountExecutionChecks.Rig Forward=new ConstructionMountExecutionChecks.Rig();
            internal BlueprintConstructionMountDetachExecution Actor;
            internal ItemStand Stand=>Forward.Target.Stand;
            internal BlueprintConstructionPartJournal Journal=>Forward.Journal;
            internal Player Player=>Forward.Frame.Player;
            internal readonly string InventoryBefore;
            internal Rig(float? durability=null)
            {
                if(durability.HasValue)Forward.Sword.m_durability=durability.Value;
                Check(Forward.Begin(),"fixture mount prepare failed");Forward.Finish();
                Check(Forward.Actor.Status=="committed","fixture mount did not commit");
                Stand.gameObject=Forward.Target;Forward.Target.RootView.Data.BeforeSet=null;
                Forward.Target.RootView.Data.Set(ZDOVars.s_itemData,Convert.FromBase64String(Stand.Contents.Zdo));
                Forward.Target.Colliders=new[]{new Collider{Owner=Forward.Target}};Player.Hover=Forward.Target;
                InventoryBefore=InventorySnapshot(Player.Inventory);Journal.Events.Clear();
                Stand.BeforeInteract=()=>{
                    Check(Journal.Mount.Return?.Phase==BlueprintConstructionPartJournal.MountReturnIntent,"native interaction preceded WAL");
                    Journal.Events.Add("return-interact");
                };
            }
            internal void UseDiskJournal()=>ConnectDisk(this);
            internal bool Begin()=>BlueprintConstructionMountDetachExecution.TryBegin(Forward.Store,Forward.Store.Plan.Id,Forward.Part,out Actor,out _);
            public void Dispose()
            {
                Actor?.Refuse("fixture cleanup");
                if(BlueprintConstructionPayoutSeal.HasActiveDropObservation){
                    BlueprintConstructionPayoutSeal.BeginMountDropHandler(Stand,out var state);
                    BlueprintConstructionPayoutSeal.EndMountDropHandler(state,null);
                }
                foreach(var drop in Stand.Returned)BlueprintConstructionPayoutSeal.Destroyed(drop);
                Forward.Dispose();
            }
        }
        internal static void Run()
        {
            using(var r=new Rig()){
                Check(r.Begin(),"valid return prepare refused");Check(r.Stand.DetachCalls==0&&r.Journal.Mount.Return.Phase==1,"prepare detached or omitted intent");
                r.Actor.Tick();Check(r.Actor.Finished&&r.Actor.Status=="observed"&&r.Journal.Mount.Return.Phase==3,"normal actual return not observed");
                Check(r.Journal.Events.SequenceEqual(new[]{"return-intent","return-interact","return-bound","return-observed"}),"durable/native event order differs");
                var drop=r.Stand.Returned.Single();Check(BlueprintConstructionPayoutSeal.IsHeld(drop)&&!BlueprintConstructionPayoutSeal.Tagged(drop.m_itemData),"actual source was released or altered");
                Check(InventorySnapshot(r.Player.Inventory)==r.InventoryBefore&&r.Forward.ProfileSaves==1&&r.Forward.WorldSaves==1,"detach credited inventory or saved before paired verification");
                r.Actor.Tick();Check(r.Stand.DetachCalls==1&&r.Stand.OwnerBodies==1,"finished return replayed native detach");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"pre-dispatch cancel setup refused");r.Actor.Refuse("cancel before native request");
                Check(r.Stand.DetachCalls==0&&r.Journal.Mount.Return.Phase==1,"pre-dispatch cancellation changed native custody");
                Check(!BlueprintConstructionPayoutSeal.HasActiveDropObservation,"pre-dispatch cancellation leaked the global drop lease");
                Check(BlueprintConstructionPayoutSeal.TryObserveMountDrop(r.Player,r.Stand,Guid.NewGuid(),_=>true,_=>{},out var other,out _),"pre-dispatch cancellation blocked another drop lease");
                other.Dispose();
            }
            foreach(var change in new Action<Rig>[] {
                r=>r.Player.Hover=null,r=>r.Forward.Target.Colliders[0].Point=new Vector3(100,0,0),r=>r.Forward.Target.Colliders[0].enabled=false,
                r=>r.Forward.Target.Colliders[0].isTrigger=true,r=>r.Player.m_maxInteractDistance=float.NaN,
                r=>r.Stand.Queued=true,r=>r.Stand.m_canBeRemoved=false,r=>r.Player.Dead=true,
                r=>SaveSystem.Flags=SaveSystemSessionFlags.DontSaveWorld,r=>SaveSystem.Generation=0,
                r=>PrivateArea.Allowed=false,r=>ZNet.instance.Peers.Add(new object()),r=>BlueprintConstructionPartExecution.StorageUnresolved=true,
                r=>r.Forward.Profile.Id=999,r=>r.Forward.Target.transform.position=new Vector3(3,0,0),
                r=>r.Forward.Target.RootView.Data.Set(BlueprintConstructionMountExecution.ReceiptKey,new byte[]{99}) })
            using(var r=new Rig()){
                change(r);Check(!r.Begin()&&r.Stand.DetachCalls==0&&r.Journal.Mount.Return==null,"invalid preflight wrote intent or reached native");
            }
            foreach(var change in new Action<Rig>[] {
                r=>r.Forward.Profile.Path+=".changed",r=>r.Forward.Profile.Name="other",r=>r.Forward.Profile.Filename="other",
                r=>ZNet.instance.World=new World{m_uid=1},r=>r.Player.Hover=null,r=>r.Stand.Queued=true,
                r=>SaveSystem.Generation++,r=>BlueprintConstructionPartExecution.StorageUnresolved=true,
                r=>r.Player.Inventory.Items.Add(Program.Item("Wood",1)) })
            using(var r=new Rig()){
                Check(r.Begin(),"late-change setup refused");change(r);r.Actor.Tick();r.Actor.Tick();
                Check(r.Actor.Status=="reconciliation"&&r.Stand.DetachCalls==0&&r.Journal.Mount.Return.Phase==1,"late context change executed or replayed native");
                Check(!BlueprintConstructionPayoutSeal.HasActiveDropObservation,"late preflight failure leaked an unsent lease");
            }
            using(var r=new Rig()){
                Check(r.Begin(),"busy-at-dispatch setup refused");
                Check(BlueprintConstructionPayoutSeal.TryObserveMountDrop(r.Player,r.Stand,Guid.NewGuid(),_=>true,_=>{},out var other,out _),"other lease setup refused");
                r.Actor.Tick();
                Check(r.Actor.Status=="reconciliation"&&r.Stand.DetachCalls==0&&other.OwnsLease,"refused dispatch stole or cleared another lease");
                other.Dispose();
            }
            using(var r=new Rig()){
                r.Stand.DetachMode="queued";Check(r.Begin(),"queued setup refused");r.Actor.Tick();r.Actor.Tick();
                Check(!r.Actor.Finished&&r.Stand.DetachCalls==1&&r.Stand.OwnerBodies==0,"queued request repeated or claimed effect");
                r.Stand.Deliver();r.Actor.Tick();Check(r.Actor.Status=="observed"&&r.Stand.DetachCalls==1,"queued owner result not bound once");
            }
            foreach(var mode in new[]{"false","throw","duplicate","bind-fail","bind-unknown","observe-fail","changed-load","changed-marker","changed-view"})
            using(var r=new Rig()){
                if(mode=="bind-fail")r.Journal.ReturnBindFails=true;
                else if(mode=="bind-unknown")r.Journal.ReturnFailAfterBind=true;
                else if(mode=="observe-fail")r.Journal.ReturnObserveFails=true;
                else if(mode=="changed-load")r.Stand.AfterLoad=()=>r.Stand.Returned[0].m_itemData.Custom="changed";
                else if(mode=="changed-marker")r.Stand.AfterBody=()=>r.Stand.Returned[0].GetComponent<ZNetView>().Data.Set(BlueprintConstructionPayoutSeal.MountMarkerKey,Guid.NewGuid().ToString("N"));
                else if(mode=="changed-view")r.Stand.AfterBody=()=>r.Stand.Returned[0].gameObject.RootView=new ZNetView();
                else r.Stand.DetachMode=mode;
                Check(r.Begin(),"failure setup refused:"+mode);r.Actor.Tick();r.Actor.Tick();
                Check(r.Actor.Finished&&r.Actor.Status=="reconciliation"&&r.Stand.DetachCalls==1&&r.Journal.Mount.Return.Phase!=3,"uncertain native outcome settled or replayed:"+mode);
                Check(InventorySnapshot(r.Player.Inventory)==r.InventoryBefore,"failure credited or debited inventory:"+mode);
            }
            using(var r=new Rig()){
                r.Stand.DetachMode="queued";Check(r.Begin(),"cancel setup refused");r.Actor.Tick();r.Actor.Refuse("cancelled");
                bool run=BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Stand,out var state);
                Check(!run,"cancelled delayed owner request entered native body");BlueprintConstructionPayoutSeal.EndMountDropHandler(state,null);
                r.Actor.Tick();Check(r.Stand.DetachCalls==1&&r.Stand.OwnerBodies==0&&r.Journal.Mount.Return.Phase==1,"cancel repeated or settled queued operation");
            }
            SavedPairChecks();
            CreditChecks();
            CleanupChecks();
            RecoveryPipelineChecks();
            Console.WriteLine("PASS mount detach actor: "+assertions+" orchestration/guard assertions including saved empty-holder/drop pair; actual context/actor/seal/readers, substituted journal/native/save APIs. No native detach/save/credit/cleanup/reload or authority proof.");
        }

        private static void SavedPairChecks()
        {
            foreach(string mode in new[]{"ok","filled","holder-receipt","missing-drop","wrong-marker","wrong-owner","wrong-prefab","wrong-packet","duplicate-drop","generation","custody","confirm-fail","cancel"})
            using(var r=new Rig()){
                Check(r.Begin(),"save setup refused");
                Check(!r.Actor.TryBeginPersistence(out _,out _) && r.Forward.ProfileSaves==1,"unobserved return began persistence");
                r.Actor.Tick();Check(r.Actor.Status=="observed","save fixture detach failed");
                ZNet.instance.SaveAction=()=>{
                    r.Forward.WorldSaves++;SaveSystem.Generation++;
                    WriteReturnWorld(r,mode);
                };
                Check(r.Actor.TryBeginPersistence(out var verification,out string error),error??"return persistence refused");
                if(mode=="generation")SaveSystem.Generation++;
                if(mode=="custody")r.Stand.Returned.Single().m_itemData.Custom="changed";
                if(mode=="confirm-fail")r.Journal.ConfirmFails=true;
                if(mode=="cancel")verification.Cancel();
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(verification.State!=BlueprintConstructionProjectPersistence.Result.Verified && verification.State!=BlueprintConstructionProjectPersistence.Result.Refused && DateTime.UtcNow<deadline){verification.Tick();Thread.Sleep(1);}
                bool passed=mode=="ok";
                Check(verification.State==(passed?BlueprintConstructionProjectPersistence.Result.Verified:BlueprintConstructionProjectPersistence.Result.Refused),"wrong paired result:"+mode+":"+verification.Reason);
                Check(r.Journal.Mount.Return.Phase==(passed?4:3),"failed pair changed durable phase:"+mode);
                Check(r.Forward.ProfileSaves==2&&r.Forward.WorldSaves==2&&r.Stand.DetachCalls==1,"pair repeated saves/native detach:"+mode);
                Check(InventorySnapshot(r.Player.Inventory)==r.InventoryBefore&&BlueprintConstructionPayoutSeal.IsHeld(r.Stand.Returned.Single()),"pair credited/released actual source:"+mode);
                if(passed){
                    var pair=r.Journal.Mount.Return.Saved;
                    Check(pair.BeforeGeneration==8&&pair.Generation==9&&r.Journal.Events.Last()=="return-saved","saved return generation/order differs");
                    Check(!r.Actor.TryBeginPersistence(out _,out _)&&r.Forward.WorldSaves==2,"verified return saved again");
                    verification.Tick();Check(r.Journal.Events.Count(e=>e=="return-saved")==1,"verified pair replayed journal confirmation");
                }
            }
            foreach(string mode in new[]{"owner","view","uid","marker","receipt","inventory","record","profile"})
            using(var r=new Rig()){
                Check(r.Begin(),"late custody setup refused");r.Actor.Tick();
                var drop=r.Stand.Returned.Single();var view=drop.GetComponent<ZNetView>();
                if(mode=="owner")view.Owner=false;
                if(mode=="view")drop.gameObject.RootView=new ZNetView();
                if(mode=="uid")view.Data.m_uid=new ZDOID { User=999,Id=99 };
                if(mode=="marker")view.Data.Set(BlueprintConstructionPayoutSeal.MountMarkerKey,Guid.NewGuid().ToString("N"));
                if(mode=="receipt")r.Forward.Target.RootView.Data.Set(BlueprintConstructionMountExecution.ReceiptKey,new byte[]{99});
                if(mode=="inventory")r.Player.Inventory.Items[0].m_stack++;
                if(mode=="record"){
                    var bytes=Convert.FromBase64String(r.Journal.Mount.Return.ObservedBytes);bytes[bytes.Length-1]^=1;
                    r.Journal.Mount.Return.ObservedBytes=Convert.ToBase64String(bytes);
                }
                if(mode=="profile")Game.instance.Profile=new PlayerProfile();
                Check(!r.Actor.TryBeginPersistence(out _,out _)&&r.Forward.ProfileSaves==1&&r.Forward.WorldSaves==1,
                    "changed custody started a saved pair:"+mode);
                Check(r.Stand.DetachCalls==1&&r.Journal.Mount.Return.Phase==3,"changed custody replayed or settled return:"+mode);
            }
        }

        private static void WriteReturnWorld(Rig r,string mode)
        {
            var f=r.Forward;uint generation=SaveSystem.Generation;string root=f.Root;
            WorldGenerationChecks.WriteWorld(f.Frame,root,generation,worldId:ZNet.instance.World.m_uid);
            int objects=mode=="missing-drop"?1:mode=="duplicate-drop"?3:2;
            byte[] Bytes(Action<BinaryWriter> action){using(var memory=new MemoryStream()){using(var w=new BinaryWriter(memory,Encoding.UTF8,true))action(w);return memory.ToArray();}}
            File.WriteAllBytes(Path.Combine(root,"_main."+generation+".chunks"),Bytes(w=>{w.Write((short)41);w.Write(objects);w.Write(1);w.Write((ushort)258);w.Write((byte)3);w.Write(generation);w.Write(objects);}));
            File.WriteAllBytes(Path.Combine(root,"01_02__3_"+generation+".chunk"),Bytes(w=>{
                w.Write((short)41);w.Write(objects);
                void Object(int prefab,int ownerKey,long owner,int markerKey,Guid operation,int blobKey,byte[] packet,bool filled=false){
                    w.Write((ushort)(256|32|64|128|(filled?16:0)));w.Write(0f);w.Write(0f);w.Write(0f);w.Write(prefab);
                    if(filled){w.Write((byte)1);w.Write("item".GetStableHashCode());w.Write(123);}
                    w.Write((byte)1);w.Write(ownerKey);w.Write(owner);
                    w.Write((byte)1);w.Write(markerKey);w.Write(operation.ToString("N"));
                    w.Write((byte)1);w.Write(blobKey);w.Write(packet.Length);w.Write(packet);
                }
                Object(r.Journal.Parent.PrefabHash,ZDOVars.s_creator,r.Journal.Parent.Creator,BlueprintConstructionPartExecution.MarkerKey,r.Journal.Parent.Operation,
                    BlueprintConstructionMountExecution.ReceiptKey,mode=="holder-receipt"?new byte[]{99}:r.Journal.Mount.Receipt,mode=="filled");
                for(int i=1;i<objects;i++){
                    var returned=r.Journal.Mount.Return;byte[] packet=returned.ObservedBytes==null?r.Stand.Returned.Single().GetComponent<ZNetView>().GetZDO().GetByteArray(ZDOVars.s_itemData):Convert.FromBase64String(returned.ObservedBytes);
                    if(mode=="wrong-packet"){packet=(byte[])packet.Clone();packet[packet.Length-1]^=1;}
                    Object((mode=="wrong-prefab"?"ShieldBanded":"SwordIron").GetStableHashCode(),BlueprintConstructionPayoutSeal.OwnerKey,mode=="wrong-owner"?101:100,
                        BlueprintConstructionPayoutSeal.MountMarkerKey,mode=="wrong-marker"?Guid.NewGuid():returned.Operation,ZDOVars.s_itemData,packet);
                }
            }));
        }
    }
}
