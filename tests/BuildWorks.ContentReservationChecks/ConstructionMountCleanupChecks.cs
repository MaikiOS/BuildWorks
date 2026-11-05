using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
namespace UnityEngine
{
    internal enum FindObjectsInactive { Include }
    internal enum FindObjectsSortMode { None }
    internal static class Object
    {
        internal static readonly List<object> Loaded = new List<object>();
        internal static T[] FindObjectsByType<T>(FindObjectsInactive _, FindObjectsSortMode __) => Loaded.OfType<T>().ToArray();
    }
}
namespace ReservationChecks
{
    internal sealed partial class ZNetScene
    {
        internal GameObject FindInstance(ZDOID id) => UnityEngine.Object.Loaded.OfType<ItemDrop>().FirstOrDefault(d=>d.GetComponent<ZNetView>().GetZDO().m_uid==id)?.gameObject;
    }
    internal sealed partial class ZDOMan
    {
        private readonly List<ZDOID> m_destroySendList = new List<ZDOID>();
        internal Action BeforeDestroy;
        internal int DestroyCalls;
        internal ZDO GetZDO(ZDOID id) => UnityEngine.Object.Loaded.OfType<ItemDrop>().Select(d=>d.GetComponent<ZNetView>().GetZDO()).FirstOrDefault(d=>d.m_uid==id);
        internal void DestroyZDO(ZDO data)
        {
            DestroyCalls++; BeforeDestroy?.Invoke();
            if (!BlueprintConstructionPayoutCleanup.AllowNetwork(data,out var witness)) throw new InvalidOperationException("native destroy refused");
            var removal=BlueprintConstructionCancellationRemoval.CaptureNetwork(data);
            m_destroySendList.Add(data.m_uid); BlueprintConstructionPayoutCleanup.ConfirmNetwork(witness,null);
            BlueprintConstructionCancellationRemoval.ConfirmNetwork(removal,null);
        }
    }
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountReturnComplete=5;
        internal sealed class MountCleanup { internal uint BeforeGeneration,SavedGeneration;internal string ProfileHash;internal bool Dispatched; }
        internal MountRecord BeginMountCleanup(Guid project,Guid part,Guid op,uint generation,NativePlayerProfileReadback.CustomDataEvidence proof)
        { if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.BeginMountCleanup(project,part,op,generation,proof)); Shared.Mount.Return.Cleanup=new MountCleanup{BeforeGeneration=generation,ProfileHash=proof.ProfileHash};return Shared.Mount; }
        internal MountRecord DispatchMountCleanup(Guid project,Guid part,Guid op)
        { if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.DispatchMountCleanup(project,part,op)); if(Shared.Mount.Return.Cleanup.Dispatched)throw new InvalidOperationException("repeated destroy");Shared.Mount.Return.Cleanup.Dispatched=true;return Shared.Mount; }
        internal MountRecord CompleteMountReturn(Guid project,Guid part,Guid op,NativeWorldStorageReadback.AbsenceEvidence absent,NativePlayerProfileReadback.CustomDataEvidence proof)
        { if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.CompleteMountReturn(project,part,op,absent,proof)); if(absent.Generation<=Shared.Mount.Return.Cleanup.BeforeGeneration||absent.Operations[0]!=op)throw new InvalidOperationException("absence differs");Shared.Mount.Return.Phase=MountReturnComplete;Shared.Mount.Return.Cleanup.SavedGeneration=absent.Generation;return Shared.Mount; }
        internal MountRecord RecoverMountReturnCredit(Guid project,Guid part,Guid op,NativePlayerProfileReadback.CustomDataEvidence proof)
        { if(Shared.Disk!=null)return Shared.Refresh(Shared.Disk.RecoverMountReturnCredit(project,part,op,proof)); var m=Shared.Mount;if(!proof.CustomData.TryGetValue(MountCreditKey(op),out var v)||v!=MountCreditValue(m))throw new InvalidOperationException("receipt absent");m.Return.Credit.Phase=MountProfileCredited;return m; }
    }
    internal static partial class ConstructionMountDetachChecks
    {
        private static void CleanupChecks()
        {
            foreach(var mode in new[]{"ok","spent","missing-receipt","source-change","resume-queued","resume-terminal","replay-source","real-journal","real-bound","real-observed","real-save-window","real-saved","real-credit-intent","real-cleanup-intent","real-queued","wrong-absence","missing-chunk"})
            using(var r=new Rig())
            {
                UnityEngine.Object.Loaded.Clear();ZDOMan.instance=new ZDOMan();
                if(mode.StartsWith("real",StringComparison.Ordinal))r.UseDiskJournal();
                foreach(var item in r.Player.Inventory.Items)item.m_pickedUp=true;
                Check(r.Begin(),"cleanup detach prepare");
                if(mode=="real-bound")r.Journal.ReturnObserveFails=true;
                r.Actor.Tick();
                var drop=r.Stand.Returned.Single();drop.gameObject.Colliders=new[]{new Collider{Owner=drop.gameObject}};
                UnityEngine.Object.Loaded.Add(drop);
                if(mode=="real-bound"||mode=="real-observed"){
                    r.Journal.ReturnObserveFails=false;
                    BlueprintConstructionMountContext.BeginReturnWorldLoad();
                    var oldWorld=ZNet.instance.World;ZNet.instance.World=new World{m_uid=oldWorld.m_uid,Directory=oldWorld.Directory};
                    BlueprintConstructionMountContext.EndReturnWorldLoad(null);
                    drop.GetComponent<ZNetView>().Data.m_uid.Id=29;BlueprintConstructionPayoutSeal.AfterAwake(drop,null);
                    Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out var resumedWhy),resumedWhy);
                }
                ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,"ok");};
                Check(r.Actor.TryBeginPersistence(out var pair,out var why),why);
                if(mode=="real-save-window"){
                    pair.Cancel();Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out why),why);
                    uint savedGeneration=SaveSystem.Generation;
                    Check(r.Actor.TryBeginPersistence(out pair,out why),why);Check(SaveSystem.Generation==savedGeneration,"recovery repeated world save");
                }
                Wait(()=>pair.State==BlueprintConstructionProjectPersistence.Result.Verified||pair.State==BlueprintConstructionProjectPersistence.Result.Refused,()=>pair.Tick());
                Check(pair.State==BlueprintConstructionProjectPersistence.Result.Verified,"cleanup saved pair:"+mode+":"+pair.Reason);
                if(mode=="real-saved")Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out why),why);
                if(mode=="real-credit-intent")r.Journal.CreditConfirmFails=true;
                bool credited=r.Actor.TryCredit(out why);
                if(mode=="real-credit-intent"){
                    Check(!credited&&r.Journal.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Return.Credit.Phase==1,"credit failure was not durable intent");
                    r.Journal.CreditConfirmFails=false;
                    Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out why),why);
                    Check(r.Actor.TryRecoverCredit(out why),why);
                }else Check(credited,why);
                if(mode=="spent"){r.Player.Inventory.Items.RemoveAt(r.Player.Inventory.Items.Count-1);System.IO.File.WriteAllBytes(r.Forward.Profile.Path,ProfileChecks.FileFor(r.Player));}
                if(mode=="missing-receipt")r.Player.m_customData.Clear();
                if(mode=="source-change")drop.m_itemData.Custom="other";
                Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out var actor,out why)==(mode!="missing-receipt"&&mode!="source-change"),"cleanup preflight:"+mode+":"+why);
                if(actor==null)continue;
                if(mode=="real-cleanup-intent"){
                    actor.Cancel("simulate crash before dispatch");
                    Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out actor,out why),why);
                }
                ZDOMan.instance.BeforeDestroy=()=>Check(r.Journal.Mount.Return.Cleanup.Dispatched,"native destruction preceded dispatch WAL");
                Wait(()=>actor.Status=="queued-removal"||actor.Finished,actor.Tick);
                Check(actor.Status=="queued-removal"&&ZDOMan.instance.DestroyCalls==1,"exact queued destruction missing:"+actor.Reason);
                if(mode=="replay-source"){
                    Check(!BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out _,out _),"uncertain source deleted twice");continue;
                }
                UnityEngine.Object.Loaded.Clear();
                if(mode=="resume-queued"||mode=="real-queued"){
                    actor.Cancel("simulate reload");Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out actor,out why),why);
                }
                ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,mode=="wrong-absence"?"ok":"missing-drop");
                    if(mode=="missing-chunk")System.IO.File.Delete(System.IO.Path.Combine(r.Forward.Root,"01_02__3_"+SaveSystem.Generation+".chunk"));};
                // The resumed constructor may already have requested its save.
                if(actor.Status=="saving")WriteReturnWorld(r,"missing-drop");
                Wait(()=>actor.Finished,actor.Tick);
                if(mode=="wrong-absence"||mode=="missing-chunk"){Check(actor.Status=="reconciliation"&&r.Journal.Mount.Return.Phase==4,"incomplete absence accepted");continue;}
                Check(actor.Status=="terminal"&&r.Journal.Mount.Return.Phase==5,"cleanup not terminal:"+actor.Reason);
                if(mode=="resume-terminal"){
                    Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out actor,out why),why);
                    Wait(()=>actor.Finished,actor.Tick);Check(actor.Status=="terminal","terminal revalidation failed");
                }
                if(mode.StartsWith("real",StringComparison.Ordinal))Check(r.Journal.Disk.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Return.Phase==5,"real file journal not terminal");
                Check(ZDOMan.instance.DestroyCalls==1&&r.Stand.DetachCalls==1&&r.Player.Inventory.CreditCalls==1,"return replayed native effects");
                UnityEngine.Object.Loaded.Clear();
            }
            Console.WriteLine("PASS actual return pipeline: actor + real journal IO + profile/world readers reach terminal; remapped UID, bound/observed/save-window/saved/credit-intent/cleanup-intent/queued recovery. Native host remains inert.");
            void Wait(Func<bool> done,Action tick){var end=DateTime.UtcNow.AddSeconds(5);while(!done()&&DateTime.UtcNow<end){tick();Thread.Sleep(1);}Check(done(),"cleanup timeout");}
        }
    }
}
