using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
namespace ReservationChecks
{
    internal static class ConstructionMixedLifecycleChecks
    {
        private static int assertions;
        private static void Check(bool value,string reason){assertions++;if(!value)throw new InvalidOperationException("Mixed lifecycle: "+reason);}
        private static void Wait(Func<bool> finished,Action tick){var until=DateTime.UtcNow.AddSeconds(8);while(!finished()&&DateTime.UtcNow<until){tick();Thread.Sleep(1);}Check(finished(),"deadline");}
        internal static void Run()
        {
            using(var r=new ConstructionMountExecutionChecks.Rig()) {
                var f=OstrixMods.BuildWorks.StoreTests.ConstructionMountJournalChecks.RuntimeMixedFixture(Path.Combine(r.Root,"mixed-disk"));
                r.Store.Plan.Id=f.Plan.Id;r.Store.Plan.World=42;r.Store.Plan.Parts.Clear();r.Store.Plan.Blueprint.parts.Clear();
                r.Journal.Disk=f.Journal;r.Journal.DiskDirectory=f.Plans.DirectoryPath;ZNet.instance.World.m_uid=42;SaveSystem.Generation=8;
                var targets=new Dictionary<Guid,GameObject>();var parents=new Dictionary<Guid,BlueprintConstructionPartJournal.Record>();
                BlueprintConstructionPartExecution.MountTargets.Clear();UnityEngine.Object.Loaded.Clear();
                var prefabStand=new GameObject{name="itemstand",RootView=new ZNetView()};prefabStand.Piece=new Piece{gameObject=prefabStand};prefabStand.Stand=new ItemStand(prefabStand.RootView){gameObject=prefabStand};
                var prefabChest=new GameObject{name="piece_chest_wood",Container=new Container(3)};prefabChest.Piece=new Piece{gameObject=prefabChest};prefabChest.RootView=prefabChest.Container.View;
                ZNetScene.instance.Prefabs["itemstand"]=prefabStand;ZNetScene.instance.Prefabs["piece_chest_wood"]=prefabChest;
                foreach(var actual in f.Plan.Parts) {
                    var source=f.Plan.Blueprint.parts[actual.SourceIndex];
                    var part=new BlueprintConstructionProjectStore.Part{Id=actual.Id,SourceIndex=actual.SourceIndex,WorldPose=new BlueprintConstructionProjectStore.Pose{Values=actual.WorldPose.Values}};
                    r.Store.Plan.Parts.Add(part);r.Store.Plan.Blueprint.parts.Add(new BlueprintConstructionProjectStore.Source{prefabName=source.prefabName,
                        foreignData=new BlueprintConstructionProjectStore.Foreign{Data=source.foreignData.ToGeometry()}});
                    var parent=new BlueprintConstructionPartJournal(r.Store).RequireSavedPayment(f.Plan.Id,actual.Id,100);parents.Add(actual.Id,parent);
                    var target=new GameObject{name=source.prefabName};target.Piece=new Piece{gameObject=target};
                    if(actual.SourceIndex==2){target.Container=new Container(3);target.RootView=target.Container.View;target.Wear=target.Container.NativeCallbacksForTest();}
                    else {target.RootView=new ZNetView();target.Stand=new ItemStand(target.RootView){gameObject=target,NextDropId=(uint)(1000+actual.SourceIndex)};
                        target.Wear=new WearNTear(target.RootView){m_onDestroyed=(Action)Delegate.CreateDelegate(typeof(Action),target.Stand,"OnDestroyed")};
                        target.Stand.AfterBody=()=>{var drop=target.Stand.Returned.Single();drop.gameObject.transform.position=target.transform.position;drop.gameObject.Colliders=new[]{new Collider{Owner=drop.gameObject,Point=target.transform.position}};UnityEngine.Object.Loaded.Add(drop);};}
                    var pose=actual.WorldPose.Values;target.transform.position=new Vector3(pose[0],pose[1],pose[2]);
                    target.RootView.Data.m_uid.Id=(uint)(100+actual.SourceIndex);target.RootView.Data.PrefabHash=parent.PrefabHash;
                    target.RootView.Data.Set(BlueprintConstructionPartExecution.MarkerKey,parent.Operation.ToString("N"));target.RootView.Data.Set(BlueprintConstructionPartExecution.ReceiptKey,parent.Receipt);
                    target.Colliders=new[]{new Collider{Owner=target,Point=target.transform.position}};targets.Add(actual.Id,target);BlueprintConstructionPartExecution.MountTargets.Add(parent.Operation,target);
                }
                var copied=BlueprintConstructionPartJournal.Copy<BlueprintConstructionPartJournal.Record>(f.Journal.RequireSavedPayment(f.Plan.Id,f.Part,100));
                foreach(var field in typeof(BlueprintConstructionPartJournal.Record).GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic))field.SetValue(r.Journal.Parent,field.GetValue(copied));
                var second=Program.Item("SwordIron",1,"second-actual");second.m_quality=3;second.m_gridPos=new Vector2i(1,0);r.Frame.Player.Inventory.Items.Add(second);
                foreach(var item in r.Frame.Player.Inventory.Items)item.m_pickedUp=true;
                ZNet.instance.SaveAction=()=>{r.WorldSaves++;SaveSystem.Generation++;WriteWorld(r,targets,parents);};
                foreach(var part in r.Store.Plan.Parts) {
                    Console.WriteLine("Mixed forward part "+part.SourceIndex);
                    int selections=BlueprintItemStandPlacement.SelectionCalls;
                    var caller=new PartContentsTickProbe(r,part.Id);Wait(()=>caller.Finished,()=>{
                        foreach(var target in targets.Values)target.Container?.SyncCacheForTest();caller.Tick();
                    });Check(caller.Status=="committed",caller.Reason??"contents not committed");
                    Check(BlueprintItemStandPlacement.SelectionCalls==selections+1,"mount/storage caller selected supplies more than once");
                }
                Check(BlueprintItemStandPlacement.NativeCalls==2&&targets[r.Store.Plan.Parts[2].Id].Container.Inventory.Items.Count==1,"forward parts did not contribute exactly once");
                var runtime=new BlueprintConstructionProjectRuntime(r);var board=runtime.Board(f.Plan.Id);r.Frame.Player.transform.position=board.transform.position;
                Check(runtime.Open(board,r.Frame.Player),"open board");runtime.BeginPaidRemoval(board.Id,1,Guid.NewGuid());Check(!runtime.BlocksInput&&runtime.TestReturn.Remaining==2,"return did not wait for any stand");
                // Return the second stand first; native hover/reach identifies one explicit request.
                foreach(int index in new[]{1,0}) {
                    var target=targets[r.Store.Plan.Parts[index].Id];r.Frame.Player.Eye=target.transform.position;
                    if(index==1)BlueprintConstructionContentsReturn.QaArmPause(f.Journal.ReadMount(f.Plan.Id,r.Store.Plan.Parts[index].Id).Operation);
                    var foreign=new GameObject{RootView=new ZNetView()};foreign.Stand=new ItemStand(foreign.RootView){gameObject=foreign};
                    bool ordinary=false;Check(BuildWorksPlugin.TestMountInteract(foreign.Stand,r.Frame.Player,false,false,ref ordinary),"foreign interaction intercepted");
                    GameObject hover=null;r.Frame.Player.NativeRayHit=foreign;int queries=r.Frame.Player.NativeHoverQueries;
                    BlueprintConstructionProjectHover.Postfix(r.Frame.Player,null,ref hover);
                    Check(hover==null&&r.Frame.Player.NativeHoverQueries==queries+1,"hover bypassed first native hit/occlusion");
                    r.Frame.Player.NativeRayHit=target;BlueprintConstructionProjectHover.Postfix(r.Frame.Player,null,ref hover);
                    Check(ReferenceEquals(hover,target),"pending stand was not restored from native hover");r.Frame.Player.Hover=hover;
                    string hint="native";BuildWorksPlugin.TestMountHover(target.Stand,ref hint);Check(hint.Contains("$KEY_Use"),"pending E binding absent");
                    Check(!target.Stand.Interact(r.Frame.Player,true,false)&&!target.Stand.Interact(r.Frame.Player,false,true)&&target.Stand.DetachCalls==0,"manual hold/alt bypassed pending E");
                    Check(target.Stand.Interact(r.Frame.Player,false,false),"E was not handled");
                    Check(runtime.Failure==null&&runtime.TestReturn.Busy,"E did not start its actor: "+runtime.Failure);
                    int previousDestroy=ZDOMan.instance.DestroyCalls;
                    if(index==1) {
                        Wait(()=>runtime.TestReturn.Status=="qa-paused-credited"||runtime.TestReturn.Finished,runtime.TestTick);
                        var credited=f.Journal.ReadMount(f.Plan.Id,r.Store.Plan.Parts[index].Id);
                        Check(credited.Return.Credit.Phase==2&&credited.Return.Cleanup==null&&ZDOMan.instance.DestroyCalls==previousDestroy,"QA gate did not stop after saved credit before dispatch");
                        for(int i=0;i<3;i++)runtime.TestTick();
                        Check(credited.Return.Operation.ToString("N")==BlueprintConstructionContentsReturn.QaPauseState.Split(':')[1]&&ZDOMan.instance.DestroyCalls==previousDestroy,"QA pause was not stable/observable");
                        runtime.Suspend();runtime.TestTick();
                        Check(!BlueprintConstructionContentsReturn.QaPauseArmed&&ZDOMan.instance.DestroyCalls==previousDestroy&&
                            f.Journal.ReadMount(f.Plan.Id,r.Store.Plan.Parts[index].Id).Return.Cleanup==null,"teardown dispatched hidden cleanup or retained QA pause");
                        runtime=new BlueprintConstructionProjectRuntime(r);board=runtime.Board(f.Plan.Id);
                        Check(runtime.Open(board,r.Frame.Player),"reentry board open");runtime.BeginPaidRemoval(board.Id,1,Guid.NewGuid());
                        Check(runtime.TryReturnStand(target.Stand,r.Frame.Player)&&target.Stand.DetachCalls==1,"credited reentry repeated detach");
                    }
                    Wait(()=>runtime.TestReturn.Finished||!runtime.TestReturn.Busy,()=>{
                        if(ZDOMan.instance.DestroyCalls>previousDestroy)UnityEngine.Object.Loaded.Clear();
                        foreach(var current in targets.Values)current.Container?.SyncCacheForTest();runtime.TestTick();
                    });
                    Check(runtime.TestReturn.Status!="reconciliation",runtime.TestReturn.Reason);
                }
                Wait(()=>runtime.TestReturn.Finished,()=>{foreach(var current in targets.Values)current.Container?.SyncCacheForTest();runtime.TestTick();});
                Check(runtime.ReturnReady(board.Id)&&r.Store.Plan.Parts.Take(2).All(part=>f.Journal.ReadMount(f.Plan.Id,part.Id).Return.Phase==5),"children did not reach terminal");
                Check(targets.Values.Where(target=>target.Container).All(target=>target.Container.Inventory.Items.Count==0),"container was not emptied");
                Check(runtime.Open(board,r.Frame.Player),"return board open");runtime.BeginPaidRemoval(board.Id,1,Guid.NewGuid());
                Check(BlueprintConstructionCancellationRemoval.Started==3&&runtime.TestRemoval.Status=="queued-removal","actual empty/callback/pose guards rejected handoff: "+runtime.TestRemoval.Reason);
                targets.Clear();BlueprintConstructionPartExecution.MountTargets.Clear();
                Wait(()=>runtime.TestRemoval.Finished,runtime.TestTick);
                Check(runtime.TestRemoval.Status=="removed-saved",runtime.TestRemoval.Reason??"removal not saved");
                Check(runtime.Open(board,r.Frame.Player),"payout board open");runtime.BeginPayout(board.Id);
                Wait(()=>runtime.TestPayout.Finished,runtime.TestTick);
                Check(runtime.TestPayout.Status=="issued-saved",runtime.TestPayout.Reason??"payout not saved");
                var payout=new OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal(f.Plans).Read(f.Plan.Id);
                Check(payout.Phase==3&&payout.Items.Length==3&&UnityEngine.Object.Loaded.OfType<ItemDrop>().All(drop=>drop.m_itemData.m_dropPrefab.name=="Wood"&&drop.m_itemData.m_stack==2),"material payout contains attachment or wrong amount");
                runtime.Dispose();BlueprintConstructionPartExecution.MountTargets.Clear();
            }
            Console.WriteLine("PASS "+assertions+" mixed lifecycle assertions: actual paid-parent/factories/runtime/return/removal/payout + real journals/readers; container + two stands, reverse E order, terminal then board-only removal and exact material issuance. Native effects are inert substitutes; no game proof.");
        }
        private static void WriteWorld(ConstructionMountExecutionChecks.Rig r,Dictionary<Guid,GameObject> targets,Dictionary<Guid,BlueprintConstructionPartJournal.Record> parents)
        {
            uint generation=SaveSystem.Generation;string root=r.Root;
            WorldGenerationChecks.WriteWorld(r.Frame,root,generation,worldId:42);
            byte[] Bytes(Action<BinaryWriter> action){using(var output=new MemoryStream()){using(var writer=new BinaryWriter(output,System.Text.Encoding.UTF8,true))action(writer);return output.ToArray();}}
            var drops=UnityEngine.Object.Loaded.OfType<ItemDrop>().ToArray();int count=targets.Count+drops.Length;
            File.WriteAllBytes(Path.Combine(root,"_main."+generation+".chunks"),Bytes(w=>{w.Write((short)41);w.Write(count);w.Write(1);w.Write((ushort)258);w.Write((byte)3);w.Write(generation);w.Write(count);}));
            File.WriteAllBytes(Path.Combine(root,"01_02__3_"+generation+".chunk"),Bytes(w=>{
                w.Write((short)41);w.Write(count);
                foreach(var pair in targets){var target=pair.Value;var parent=parents[pair.Key];var data=target.RootView.Data;var mounted=target.Stand?.Contents;
                    var strings=new Dictionary<int,string>{{BlueprintConstructionPartExecution.MarkerKey,parent.Operation.ToString("N")}};
                    string transfer=data.GetString(BlueprintStorageCapture.TransferMarkerKey,"");if(transfer!="")strings.Add(BlueprintStorageCapture.TransferMarkerKey,transfer);
                    var blobs=new Dictionary<int,byte[]>{{BlueprintConstructionPartExecution.ReceiptKey,parent.Receipt}};
                    var receipt=data.GetByteArray(BlueprintConstructionMountExecution.ReceiptKey);if(receipt!=null)blobs.Add(BlueprintConstructionMountExecution.ReceiptKey,receipt);
                    if(mounted!=null)blobs.Add(ZDOVars.s_itemData,Convert.FromBase64String(mounted.Zdo));
                    if(target.Container)blobs.Add(ZDOVars.s_items,data.Raw);
                    w.Write((ushort)(256|32|64|128|(mounted!=null?16:0)));w.Write(0f);w.Write(0f);w.Write(0f);w.Write(parent.PrefabHash);
                    if(mounted!=null){var info=mounted.Info.Split(':');w.Write((byte)4);w.Write("item".GetStableHashCode());w.Write(info[0].GetStableHashCode());
                        foreach(var item in new[]{new{Key="variant",Index=1},new{Key="quality",Index=2},new{Key="type",Index=3}}){w.Write(item.Key.GetStableHashCode());w.Write(int.Parse(info[item.Index]));}}
                    w.Write((byte)1);w.Write(ZDOVars.s_creator);w.Write(100L);w.Write((byte)strings.Count);foreach(var text in strings){w.Write(text.Key);w.Write(text.Value);}
                    w.Write((byte)blobs.Count);foreach(var blob in blobs){w.Write(blob.Key);w.Write(blob.Value.Length);w.Write(blob.Value);}
                }
                foreach(var drop in drops){var data=drop.GetComponent<ZNetView>().Data;w.Write((ushort)(256|32|64|128));w.Write(0f);w.Write(0f);w.Write(0f);w.Write(drop.m_itemData.m_dropPrefab.name.GetStableHashCode());
                    int marker=data.GetString(BlueprintConstructionPayoutSeal.MarkerKey,"")==""?BlueprintConstructionPayoutSeal.MountMarkerKey:BlueprintConstructionPayoutSeal.MarkerKey;
                    w.Write((byte)1);w.Write(BlueprintConstructionPayoutSeal.OwnerKey);w.Write(100L);w.Write((byte)1);w.Write(marker);w.Write(data.GetString(marker,""));
                    var packet=data.GetByteArray(ZDOVars.s_itemData);w.Write((byte)1);w.Write(ZDOVars.s_itemData);w.Write(packet.Length);w.Write(packet);}
            }));
        }
    }
}
