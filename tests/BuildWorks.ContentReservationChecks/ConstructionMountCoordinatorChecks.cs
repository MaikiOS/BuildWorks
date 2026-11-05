using System;
using System.Linq;
using System.Threading;
namespace ReservationChecks
{
    internal static partial class ConstructionMountDetachChecks
    {
        private static void ProductionReturnCoordinatorChecks()
        {
            foreach(string mode in new[]{"normal","close-context","queued-teardown","already-returning","runtime-board","runtime-world","runtime-dispose"})
            using(var r=new Rig()) {
                r.UseDiskJournal();UnityEngine.Object.Loaded.Clear();ZDOMan.instance=new ZDOMan();
                foreach(var item in r.Player.Inventory.Items)item.m_pickedUp=true;
                ZNetScene.instance.Prefab.Stand=new ItemStand(new ZNetView());
                r.Stand.AfterBody=()=>{
                    var drop=r.Stand.Returned.Single();drop.gameObject.Colliders=new[]{new Collider{Owner=drop.gameObject}};
                    UnityEngine.Object.Loaded.Add(drop);
                };
                ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,ZDOMan.instance.DestroyCalls==0?"ok":"missing-drop");};
                bool context=true;
                BlueprintConstructionProjectRuntime runtime=null;
                BlueprintConstructionProjectBoard board=null;
                BlueprintConstructionContentsReturn caller;
                if(mode.StartsWith("runtime",StringComparison.Ordinal)) {
                    r.Forward.Target.Wear=new WearNTear(r.Forward.Target.RootView) { m_onDestroyed=(Action)Delegate.CreateDelegate(typeof(Action),r.Stand,"OnDestroyed") };
                    runtime=new BlueprintConstructionProjectRuntime(r.Forward);board=runtime.Board(r.Forward.Store.Plan.Id);
                    board.transform.position=new Vector3(-10,0,0);r.Player.transform.position=board.transform.position;
                    Check(runtime.Open(board,r.Player),"board did not open");
                    runtime.BeginPaidRemoval(board.Id,r.Forward.Store.Plan.Revision,Guid.NewGuid());caller=runtime.TestReturn;
                    runtime.Close();r.Player.transform.position=new Vector3();r.Player.Eye=new Vector3();
                    Check(ReferenceEquals(caller,runtime.TestReturn)&&!runtime.BlocksInput&&runtime.TestBusy,"panel close erased request or waiting blocked movement");
                    if(mode=="runtime-world") {
                        var old=ZNet.instance.World;ZNet.instance.World=new World{m_uid=old.m_uid,Directory=old.Directory};runtime.TestTick();
                        Check(caller.Finished&&caller.Status=="reconciliation"&&r.Journal.Mount.Return==null,"same-ID new world retained confirmation");runtime.Dispose();continue;
                    }
                    if(mode=="runtime-dispose") {
                        runtime.Dispose();Check(runtime.TestReturn==null&&!caller.TryBeginStand(r.Stand,r.Player,out _)&&r.Stand.DetachCalls==0,"Dispose retained volatile request");continue;
                    }
                }
                else caller=new BlueprintConstructionContentsReturn(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Store.Plan.Revision,Guid.NewGuid(),()=>context);
                Check(!caller.Finished&&!caller.Busy&&caller.Status=="awaiting-stand","production caller did not wait for E");
                Check(r.Stand.DetachCalls==0&&caller.IsPendingStand(r.Stand),"waiting caller dispatched native operation");
                if(mode=="close-context") {
                    context=false;caller.Tick();Check(caller.Finished&&caller.Status=="reconciliation"&&r.Stand.DetachCalls==0,"lost context dispatched detach");continue;
                }
                if(mode=="queued-teardown")r.Stand.DetachMode="queued";
                Check(caller.TryBeginStand(r.Stand,r.Player,out var why),why);
                Check(!caller.TryBeginStand(r.Stand,r.Player,out _)&&r.Stand.DetachCalls==0,"second actor started before tick");
                caller.Tick();
                if(mode=="queued-teardown") {
                    caller.Refuse("suspend");
                    Check(!BlueprintConstructionPayoutSeal.HasActiveDropObservation,"cancelled queued request retained the global observation lease");
                    r.Stand.Deliver();
                    Check(!BlueprintConstructionPayoutSeal.BeginMountDropHandler(r.Stand,out var late)&&late==null,"cancelled old stand lost its late-handler fence");
                    Check(r.Stand.OwnerBodies==0&&!BlueprintConstructionPayoutSeal.HasActiveDropObservation,"teardown kept receiver lease or let late callback through");continue;
                }
                if(mode=="already-returning") {
                    caller.Refuse("close/re-enter");
                    caller=new BlueprintConstructionContentsReturn(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Store.Plan.Revision,Guid.NewGuid(),()=>context);
                    Check(caller.TryBeginStand(r.Stand,r.Player,out why),why);
                }
                var until=DateTime.UtcNow.AddSeconds(8);
                while(!caller.Finished&&DateTime.UtcNow<until) {
                    if(ZDOMan.instance.DestroyCalls!=0)UnityEngine.Object.Loaded.Clear();
                    caller.Tick();Thread.Sleep(1);
                }
                Check(caller.Finished&&caller.Status=="returned",caller.Reason??"production caller deadline");
                Check(r.Journal.Disk.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Return.Phase==5&&r.Stand.DetachCalls==1&&
                    r.Player.Inventory.CreditCalls==1&&ZDOMan.instance.DestroyCalls==1,"production return replayed or stopped before terminal");
                Check(!caller.Busy&&caller.Remaining==0,"terminal caller still blocks input");
                if(runtime!=null) {
                    runtime.TestTick();
                    Check(BlueprintConstructionCancellationRemoval.Started==0&&runtime.ReturnReady(board.Id)&&!runtime.BlocksInput,"terminal auto-removed shells or kept input blocked");
                    Check(!runtime.Open(board,r.Player),"remote board opened");
                    bool remote=false;try{runtime.BeginPaidRemoval(board.Id,r.Forward.Store.Plan.Revision,Guid.NewGuid());}catch(InvalidOperationException){remote=true;}
                    Check(remote&&BlueprintConstructionCancellationRemoval.Started==0,"remote shell removal started");
                    r.Player.transform.position=board.transform.position;Check(runtime.Open(board,r.Player),"return board did not reopen");
                    runtime.BeginPaidRemoval(board.Id,r.Forward.Store.Plan.Revision,Guid.NewGuid());
                    Check(BlueprintConstructionCancellationRemoval.Started==1&&runtime.TestReturn==null&&runtime.BlocksInput,"board did not hand terminal children to native removal");
                    runtime.Dispose();
                }
            }
            Console.WriteLine("PASS production contents-return caller: explicit E, terminal pipeline, existing-return reentry, context/queued teardown; real journal/profile/world readers, inert native host. Board/runtime and mixed multi-object coverage remain separate.");
        }
    }
}
