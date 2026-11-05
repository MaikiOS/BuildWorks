using System;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
namespace ReservationChecks
{
    internal static class ConstructionMountAttachRecoveryChecks
    {
        private static int assertions;
        private static void Check(bool value,string error) { assertions++; if(!value)throw new InvalidOperationException("Attach recovery: "+error); }
        private static BlueprintForeignPartData Snapshot(Inventory inventory) { Check(BlueprintStorageCapture.TrySnapshotInventory(inventory,out var data),"snapshot");return data; }
        private static void Finish(BlueprintConstructionMountExecution actor) {
            var until=DateTime.UtcNow.AddSeconds(6);while(!actor.Finished&&DateTime.UtcNow<until){actor.Tick();Thread.Sleep(1);}
            Check(actor.Finished,"deadline");Check(actor.Status=="committed",actor.Reason??actor.Status);
        }
        private static void Loaded(ConstructionMountExecutionChecks.Rig r,BlueprintForeignPartData raw) {
            BlueprintConstructionMountContext.BeginReturnWorldLoad();
            r.Target.RootView.Data.m_uid.Id+=100;
            BlueprintConstructionMountExecution.NativeTargetLoaded(r.Target.RootView.Data,null);
            BlueprintConstructionMountContext.EndReturnWorldLoad(null);
            ConstructionMountDetachChecks.LoadInventoryNative(r.Frame.Player.Inventory,raw);
        }
        internal static void Run()
        {
            foreach(string mode in new[]{"paid-parent","missing-then-continue","return-block"})
            using(var r=new ConstructionMountExecutionChecks.Rig()) {
                r.UseDiskJournalBeforeMount();
                var parentPath=System.IO.Path.Combine(r.Journal.DiskPath(),r.Store.Plan.Id.ToString("N")+"."+r.Part.ToString("N")+".bwpart");
                byte[] paid=System.IO.File.ReadAllBytes(parentPath);
                if(mode=="missing-then-continue") {
                    r.Frame.Player.Inventory.Items.Remove(r.Sword);
                    var waiting=new PartContentsTickProbe(r);
                    Check(waiting.Finished&&waiting.Status=="awaiting-contents"&&BlueprintItemStandPlacement.NativeCalls==0&&
                        r.Journal.ReadMount(r.Store.Plan.Id,r.Part)==null,"missing contribution rebuilt/debited the paid parent");
                    r.Frame.Player.Inventory.Items.Add(r.Sword);
                }
                var spare=Program.Item("SwordIron",1,"caller-spare-own");spare.m_quality=3;spare.m_gridPos=new Vector2i(1,0);
                r.Frame.Player.Inventory.Items.Add(spare);string spareBefore=BlueprintItemStandPlacement.SnapshotItem(spare);
                int selections=BlueprintItemStandPlacement.SelectionCalls;
                var caller=new PartContentsTickProbe(r);var until=DateTime.UtcNow.AddSeconds(6);
                Check(BlueprintItemStandPlacement.SelectionCalls==selections+1,"fresh paid-parent mount selected more than once");
                while(!caller.Finished&&DateTime.UtcNow<until){caller.Tick();Thread.Sleep(1);}
                Check(caller.Finished&&caller.Status=="committed",caller.Reason??"paid parent caller deadline");
                Check(System.IO.File.ReadAllBytes(parentPath).SequenceEqual(paid)&&BlueprintItemStandPlacement.NativeCalls==1,
                    "contents caller rewrote parent payment or repeated native attachment");
                Check(BlueprintItemStandPlacement.SnapshotItem(spare)==spareBefore&&r.Sword.m_stack==0,"caller substituted another equal-prefab item");
                selections=BlueprintItemStandPlacement.SelectionCalls;
                if(mode=="return-block") {
                    var mount=r.Journal.ReadMount(r.Store.Plan.Id,r.Part);
                    r.Journal.Disk.PrepareMountReturn(r.Store.Plan.Id,r.Part,mount.Operation,Guid.NewGuid(),100,SaveSystem.Generation,
                        Snapshot(r.Frame.Player.Inventory),new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,mount.MountedInfo,mount.MountedBytes,null),out _);
                    bool blocked=false;try{new PartContentsTickProbe(r);}catch(InvalidOperationException){blocked=true;}
                    Check(blocked&&BlueprintItemStandPlacement.NativeCalls==1,"return intent resumed mounting");
                }
                else Check(new PartContentsTickProbe(r).Finished&&BlueprintItemStandPlacement.NativeCalls==1,"committed parent replayed contents");
                Check(BlueprintItemStandPlacement.SelectionCalls==selections,"completed/return caller selected again");
            }
            foreach(int stack in new[]{1,2,3,17})
            using(var r=new ConstructionMountExecutionChecks.Rig()) {
                r.Sword.m_stack=stack;r.Sword.m_equipped=true;
                var another=Program.Item("SwordIron",1,"second-own-item");another.m_gridPos=new Vector2i(1,0);another.m_quality=3;another.m_durability=.05f;
                r.Frame.Player.Inventory.Items.Add(another);
                var before=Snapshot(r.Frame.Player.Inventory);
                var rawAfter=BlueprintContainerInventory.DebitOne(before,0,0,n=>n.GetStableHashCode());
                r.Sword.m_equipped=false;if(--r.Sword.m_stack==0)r.Frame.Player.Inventory.Items.Remove(r.Sword);
                Check(Snapshot(r.Frame.Player.Inventory).Zdo==rawAfter.Zdo,"debit reconstruction changed raw packets or equipment");
                Check(BlueprintContainerInventory.TryRead(rawAfter,n=>n.GetStableHashCode(),out var rows)&&rows.Any(item=>item.CustomData.Values.Contains("second-own-item")),"second equal-name item changed");
            }
            foreach(string mode in new[]{"intent","intent-loaded","observed-pair","observed-missing-receipt","committed-changed-inventory","conflict","identity","holder-before-debit","wrong-orientation"})
            using(var r=new ConstructionMountExecutionChecks.Rig()) {
                r.UseDiskJournalBeforeMount();r.Sword.m_equipped=true;
                var parentFile=System.IO.Path.Combine(r.Journal.DiskPath(),r.Store.Plan.Id.ToString("N")+"."+r.Part.ToString("N")+".bwpart");
                byte[] parentBytes=System.IO.File.ReadAllBytes(parentFile);
                var spare=Program.Item("SwordIron",1,"second-own-item");spare.m_gridPos=new Vector2i(1,0);spare.m_quality=3;spare.m_durability=.05f;
                r.Frame.Player.Inventory.Items.Add(spare);
                var before=Snapshot(r.Frame.Player.Inventory);
                r.Journal.ObserveFails=mode.StartsWith("intent",StringComparison.Ordinal)||mode=="holder-before-debit"||mode=="identity"||mode=="wrong-orientation";
                r.Journal.ConfirmFails=mode=="observed-pair"||mode=="observed-missing-receipt"||mode=="conflict";
                Check(r.Begin(),"begin "+mode);r.Actor.Tick();
                if(mode=="committed-changed-inventory") { Finish(r.Actor);r.Frame.Player.Inventory.Items.Clear(); }
                else { var until=DateTime.UtcNow.AddSeconds(6);while(!r.Actor.Finished&&DateTime.UtcNow<until){r.Actor.Tick();Thread.Sleep(1);}Check(r.Actor.Finished,"failure window did not stop"); }
                var row=r.Journal.ReadMount(r.Store.Plan.Id,r.Part);
                var after=row.AfterBytes==null?BlueprintContainerInventory.DebitOne(before,0,0,n=>n.GetStableHashCode()):new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,row.AfterInfo,row.AfterBytes,null);
                string mountedInfo=row.MountedInfo,mountedBytes=row.MountedBytes;
                Guid childOperation=row.Operation;
                int calls=BlueprintItemStandPlacement.NativeCalls,profiles=r.ProfileSaves,worlds=r.WorldSaves;
                r.Journal.ObserveFails=false;r.Journal.ConfirmFails=false;
                if(mode=="intent-loaded") {
                    Game.instance.SaveAction();
                    ZNet.instance.SaveAction();
                    Loaded(r,after);
                }
                else if(mode=="observed-pair") Loaded(r,after);
                else if(mode=="observed-missing-receipt") r.Target.RootView.Data.ByteArrays.Remove(BlueprintConstructionMountExecution.ReceiptKey);
                else if(mode=="conflict") r.Target.RootView.Data.Set(BlueprintConstructionMountExecution.ReceiptKey,new byte[]{99});
                else if(mode=="identity") r.Target.RootView.Data.m_uid.Id++;
                else if(mode=="holder-before-debit") ConstructionMountDetachChecks.LoadInventoryNative(r.Frame.Player.Inventory,before);
                else if(mode=="wrong-orientation") r.Target.Stand.Contents=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"SwordIron:0:2:1",r.Target.Stand.Contents.Zdo,null);
                int selected=BlueprintItemStandPlacement.SelectionCalls;
                bool resumed;string reason=null;
                if(mode=="intent"||mode=="observed-pair") {
                    var caller=new PartContentsTickProbe(r);var until=DateTime.UtcNow.AddSeconds(6);
                    while(!caller.Finished&&DateTime.UtcNow<until){caller.Tick();Thread.Sleep(1);}
                    resumed=caller.Finished&&caller.Status=="committed";reason=caller.Reason;
                    var child=r.Journal.ReadMount(r.Store.Plan.Id,r.Part);
                    Check(System.IO.File.ReadAllBytes(parentFile).SequenceEqual(parentBytes),"interrupted caller rewrote paid parent "+mode);
                    Check(child.Operation==childOperation&&child.MountedInfo==mountedInfo&&child.MountedBytes==mountedBytes&&
                        child.AfterInfo==after.Info&&child.AfterBytes==after.Zdo,"interrupted caller replaced immutable child payload "+mode);
                }
                else {
                    resumed=BlueprintConstructionMountExecution.TryResume(r.Store,r.Store.Plan.Id,r.Part,out var actor,out reason);
                    if(resumed)Finish(actor);
                }
                Check(BlueprintItemStandPlacement.SelectionCalls==selected,"recovery selected another supplied item");
                bool refused=mode=="conflict"||mode=="identity"||mode=="holder-before-debit"||mode=="wrong-orientation";
                Check(resumed!=refused,"classification "+mode+": "+reason);
                Check(BlueprintItemStandPlacement.NativeCalls==calls,"native replay "+mode);
                if(mode=="observed-pair"||mode=="committed-changed-inventory")Check(r.ProfileSaves==profiles&&r.WorldSaves==worlds,"unnecessary save "+mode);
                if(mode=="intent-loaded")Check(r.ProfileSaves==profiles+1&&r.WorldSaves==worlds+2&&
                    r.Journal.ReadMount(r.Store.Plan.Id,r.Part).AfterBytes==after.Zdo,"raw intent After was reserialized during missing-receipt recovery");
                if(refused)Check(r.ProfileSaves==profiles&&r.WorldSaves==worlds,"refusal saved "+mode);
            }
            foreach(string mode in new[]{"finished-no-effect","loaded-no-effect","queued-intent"})
            using(var r=new ConstructionMountExecutionChecks.Rig()) {
                r.UseDiskJournalBeforeMount();Game.instance.SaveAction();
                var before=Snapshot(r.Frame.Player.Inventory);Check(r.Begin(),"no-effect prepare");
                if(mode=="finished-no-effect") { BlueprintItemStandPlacement.Mode="no-effect";r.Actor.Tick(); }
                else if(mode=="loaded-no-effect") { ZNet.instance.SaveAction();Loaded(r,before); }
                else r.Target.Stand.Queued=true;
                int native=BlueprintItemStandPlacement.NativeCalls,profiles=r.ProfileSaves,worlds=r.WorldSaves;
                bool resumed=BlueprintConstructionMountExecution.TryResume(r.Store,r.Store.Plan.Id,r.Part,out var actor,out var why);
                if(mode=="queued-intent")Check(!resumed&&r.Journal.ReadMount(r.Store.Plan.Id,r.Part).Phase==1,"queued intent became no-effect");
                else {
                    Check(resumed,why);var until=DateTime.UtcNow.AddSeconds(6);while(!actor.Finished&&DateTime.UtcNow<until){actor.Tick();Thread.Sleep(1);}
                    Check(actor.Finished&&actor.Status=="no-effect"&&r.Journal.ReadMount(r.Store.Plan.Id,r.Part).Phase==4,actor.Reason??"no-effect recovery");
                }
                Check(BlueprintItemStandPlacement.NativeCalls==native&&r.ProfileSaves==profiles&&r.WorldSaves==worlds,"no-effect replay/save");
            }
            Console.WriteLine("PASS "+assertions+" attach recovery controls: actual actor + real journal/profile/world readers; exact supplied cell, raw debit and no replay; inert native host only.");
        }
    }
}
