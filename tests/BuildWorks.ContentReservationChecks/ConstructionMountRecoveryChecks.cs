using System;
using System.Linq;
using System.IO;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
namespace ReservationChecks
{
    internal static partial class ConstructionMountDetachChecks
    {
        private static void RecoveryPipelineChecks()
        {
            RecoveryRegression("identity"); RecoveryRegression("load");
            WorldLoadControls();
            ProcessRecovery();
            foreach(string stage in new[]{"bound","observed","save-window","saved","credit-intent","cleanup-intent","same-epoch-instance","same-epoch-save","wrong-property","one-bit","streaming","native-load-same-epoch"})
            using(var r=new Rig(0.05f))
            {
                r.UseDiskJournal(); ZDOMan.instance=new ZDOMan();UnityEngine.Object.Loaded.Clear();
                foreach(var item in r.Player.Inventory.Items){item.m_pickedUp=true;item.m_durability=0.05f;}
                File.WriteAllBytes(r.Forward.Profile.Path,ProfileChecks.FileFor(r.Player));
                Check(r.Begin(),"near-cent recovery setup");if(stage=="bound")r.Journal.ReturnObserveFails=true;
                r.Actor.Tick();var drop=r.Stand.Returned.Single();UnityEngine.Object.Loaded.Add(drop);
                var record=r.Journal.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part);
                Check(BitConverter.ToInt32(Convert.FromBase64String(record.MountedBytes),1)==5&&BitConverter.ToInt32(drop.GetComponent<ZNetView>().Data.GetByteArray(ZDOVars.s_itemData),1)==4,"immutable A5→B4 oracle changed");
                r.Journal.ReturnObserveFails=false;
                ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,"ok");};
                if(stage=="bound"||stage=="observed") {SaveSystem.Generation++;WriteReturnWorld(r,"ok");}
                else if(stage!="same-epoch-instance"&&stage!="same-epoch-save"&&stage!="streaming"&&stage!="native-load-same-epoch")
                {
                    Check(r.Actor.TryBeginPersistence(out var pair,out var why),why);
                    if(stage=="save-window")pair.Cancel();else{WaitRecovery(()=>pair.State==BlueprintConstructionProjectPersistence.Result.Verified||pair.State==BlueprintConstructionProjectPersistence.Result.Refused,()=>pair.Tick());Check(pair.State==BlueprintConstructionProjectPersistence.Result.Verified,pair.Reason);}
                }
                string oldReceipt=null;
                if(stage=="credit-intent"||stage=="cleanup-intent")
                {
                    drop.gameObject.Colliders=new[]{new Collider{Owner=drop.gameObject}};
                    r.Journal.CreditConfirmFails=stage=="credit-intent";
                    bool ok=r.Actor.TryCredit(out var why);Check(ok==(stage=="cleanup-intent"),"old epoch credit outcome:"+why);r.Journal.CreditConfirmFails=false;
                    oldReceipt=BlueprintConstructionPartJournal.MountCreditValue(r.Journal.Mount);
                    if(stage=="cleanup-intent"){Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out var cleanup,out why),why);cleanup.Cancel("crash before dispatch");}
                }
                var beforeReload=r.Journal.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part);
                if(stage=="same-epoch-save")SaveSystem.Generation++;
                bool newWorld=stage!="same-epoch-instance"&&stage!="same-epoch-save"&&stage!="streaming"&&stage!="native-load-same-epoch";
                if(newWorld){BlueprintConstructionMountContext.BeginReturnWorldLoad();BlueprintConstructionMountContext.EndReturnWorldLoad(null);}
                var oldDrop=drop;
                if(stage=="streaming")BlueprintConstructionPayoutSeal.Destroyed(oldDrop);
                if(stage!="native-load-same-epoch")drop=ReloadDrop(r,oldDrop,!newWorld);
                else {ItemDrop.LoadFromZDO(drop.m_itemData,drop.GetComponent<ZNetView>().Data);BlueprintConstructionMountContext.ReturnSourceNativeLoaded(drop,null);}
                if(newWorld)
                {
                    var c=beforeReload.Return.Credit;
                    LoadInventoryNative(r.Player.Inventory,new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                        c?.AfterInfo??beforeReload.Return.BeforeInfo,c?.AfterBytes??beforeReload.Return.BeforeBytes,null));
                }
                if(stage=="wrong-property")drop.m_itemData.Custom="corrupt";
                if(stage=="one-bit"){var bytes=drop.GetComponent<ZNetView>().Data.GetByteArray(ZDOVars.s_itemData);bytes[bytes.Length-1]^=1;}
                if(stage=="same-epoch-instance"||stage=="same-epoch-save"||stage=="wrong-property"||stage=="one-bit")
                {
                    int calls=r.Player.Inventory.CreditCalls;var revision=r.Journal.Disk.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Revision;
                    Check(!BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out _,out _),"invalid lifetime/property accepted:"+stage);
                    Check(r.Journal.Disk.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Revision==revision&&r.Player.Inventory.CreditCalls==calls&&ZDOMan.instance.DestroyCalls==0,"refused resume had effects");continue;
                }
                Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out var reason),"loaded resume:"+stage+":"+reason);
                if(stage=="streaming"||stage=="native-load-same-epoch")continue;
                if(r.Journal.Mount.Return.Phase<4)
                {
                    Check(r.Actor.TryBeginPersistence(out var pair,out reason),"loaded pair:"+stage+":"+reason);
                    WaitRecovery(()=>pair.State==BlueprintConstructionProjectPersistence.Result.Verified||pair.State==BlueprintConstructionProjectPersistence.Result.Refused,()=>pair.Tick());Check(pair.State==BlueprintConstructionProjectPersistence.Result.Verified,pair.Reason);
                }
                if(stage=="credit-intent")Check(r.Actor.TryRecoverCredit(out reason),reason);
                else if(stage!="cleanup-intent")Check(r.Actor.TryCredit(out reason),"loaded credit:"+stage+":"+reason);
                if(oldReceipt!=null)Check(BlueprintConstructionPartJournal.MountCreditValue(r.Journal.Mount)==oldReceipt,"old receipt identity changed on Load");
                else Check(r.Journal.Mount.Return.Credit.LoadedSource&&BitConverter.ToInt32(Convert.FromBase64String(r.Journal.Mount.Return.Credit.ItemBytes),1)==3,"loaded credit must be exact B4->C3, not rawB reuse or another Load");
                Check(BitConverter.ToInt32(Convert.FromBase64String(r.Journal.Mount.Return.ObservedBytes),1)==4,"immutable rawB rewritten");
                Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out var removal,out reason),"loaded cleanup:"+stage+":"+reason);
                WaitRecovery(()=>removal.Status=="queued-removal"||removal.Finished,removal.Tick);Check(removal.Status=="queued-removal",removal.Reason);
                UnityEngine.Object.Loaded.Clear();ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,"missing-drop");};
                WaitRecovery(()=>removal.Finished,removal.Tick);Check(removal.Status=="terminal",removal.Reason);
                Check(r.Stand.DetachCalls==1&&r.Player.Inventory.CreditCalls==1&&ZDOMan.instance.DestroyCalls==1,"recovery repeated effect:"+stage);
            }
            Console.WriteLine("PASS near-cent A5/rawB4/native Load live4 pipeline: exact epoch/lifecycle, immutable receipt, real journal/readers, recovery boundaries and negative controls; inert host only.");
        }
        private static void WorldLoadControls()
        {
            using(var r=new Rig())
            {
                var drop=ItemDrop.Make(Program.Item("SwordIron",1));
                try
                {
                    foreach(string kind in new[]{"normal-new-world-no-db","legacy-world"})
                    {
                        ZNet.m_loadError=false;BlueprintConstructionMountContext.BeginReturnWorldLoad();
                        // ServerLoadWorld completion succeeds for both routes; inner LoadWorld's false is not an error flag.
                        BlueprintConstructionMountContext.EndReturnWorldLoad(null);
                        Check(BlueprintConstructionMountContext.BindReturnSource(Guid.NewGuid(),drop)!=null,"normal world binding rejected:"+kind);
                    }
                    Guid operation=Guid.NewGuid();var previous=BlueprintConstructionMountContext.BindReturnSource(operation,drop);
                    var record=new BlueprintConstructionPartJournal.MountRecord{Return=new BlueprintConstructionPartJournal.MountReturnRecord{Operation=operation,SourceEpoch=previous.Epoch}};
                    ZNet.m_loadError=true;BlueprintConstructionMountContext.BeginReturnWorldLoad();BlueprintConstructionMountContext.EndReturnWorldLoad(null);
                    bool refused=false;try{BlueprintConstructionMountContext.BindReturnSource(Guid.NewGuid(),drop);}catch(InvalidOperationException){refused=true;}Check(refused,"actual load error granted binding");
                    var world=ZNet.instance.World;ZNet.instance.World=new World{m_uid=world.m_uid,Directory=world.Directory};ZNet.m_loadError=false;
                    Check(BlueprintConstructionMountContext.BindReturnSource(Guid.NewGuid(),drop)!=null,"failed previous world poisoned new world identity");
                    refused=false;try{BlueprintConstructionMountContext.ResolveReturnSource(record,drop);}catch(InvalidOperationException){refused=true;}Check(refused,"world transition alone allowed remap without native Load");
                    BlueprintConstructionMountContext.BeginReturnWorldLoad();BlueprintConstructionMountContext.EndReturnWorldLoad(new InvalidOperationException("native loader exception"));
                    refused=false;try{BlueprintConstructionMountContext.BindReturnSource(Guid.NewGuid(),drop);}catch(InvalidOperationException){refused=true;}Check(refused,"failed loader exception granted binding");
                    BlueprintConstructionMountContext.BeginReturnWorldLoad();BlueprintConstructionMountContext.EndReturnWorldLoad(null);
                    Check(BlueprintConstructionMountContext.BindReturnSource(Guid.NewGuid(),drop)!=null,"successful later load remained poisoned");
                }
                finally{ZNet.m_loadError=false;BlueprintConstructionMountContext.BeginReturnWorldLoad();BlueprintConstructionMountContext.EndReturnWorldLoad(null);}
            }
            Console.WriteLine("PASS world-load boundary: normal empty/legacy worlds, real error/exception refusal, healthy next-world recovery; transition alone never permits source remap.");
        }
        private static void ProcessRecovery()
        {
            using(var r=new Rig(0.05f))
            {
                r.UseDiskJournal();ZDOMan.instance=new ZDOMan();UnityEngine.Object.Loaded.Clear();
                foreach(var item in r.Player.Inventory.Items)item.m_pickedUp=true;
                Check(r.Begin(),"process recovery setup");r.Actor.Tick();
                ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,"ok");};
                Check(r.Actor.TryBeginPersistence(out var pair,out var why),why);
                WaitRecovery(()=>pair.State==BlueprintConstructionProjectPersistence.Result.Verified||pair.State==BlueprintConstructionProjectPersistence.Result.Refused,()=>pair.Tick());Check(pair.State==BlueprintConstructionProjectPersistence.Result.Verified,pair.Reason);
                string request=Path.Combine(r.Forward.Root,"reload-request.txt");
                File.WriteAllLines(request,new[]{Path.Combine(r.Forward.Root,"disk-journal","projects"),r.Forward.Store.Plan.Id.ToString("N"),r.Forward.Part.ToString("N"),r.Forward.Root,r.Forward.Profile.Path,SaveSystem.Generation.ToString()});
                var start=new System.Diagnostics.ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--mount-recovery-process \""+request+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
                using(var child=System.Diagnostics.Process.Start(start))
                {
                    Check(child.WaitForExit(15000),"new-process recovery timeout");string output=child.StandardOutput.ReadToEnd(),errors=child.StandardError.ReadToEnd();
                    Check(child.ExitCode==0&&output.Contains("PASS new-process near-cent recovery"),"child recovery:"+errors+output);
                    Console.WriteLine(output.Trim());
                }
                Check(r.Journal.Disk.ReadMount(r.Forward.Store.Plan.Id,r.Forward.Part).Return.Phase==5,"child did not persist terminal");
            }
        }
        internal static void RecoveryChild(string request)
        {
            string[] args=File.ReadAllLines(request);
            using(var r=new Rig())
            {
                ReconnectDisk(r,args[0],Guid.Parse(args[1]),Guid.Parse(args[2]));ZDOMan.instance=new ZDOMan();UnityEngine.Object.Loaded.Clear();
                foreach(string path in Directory.GetFiles(args[3],"_main.*"))File.Copy(path,Path.Combine(r.Forward.Root,Path.GetFileName(path)),true);
                foreach(string path in Directory.GetFiles(args[3],"*.chunk"))File.Copy(path,Path.Combine(r.Forward.Root,Path.GetFileName(path)),true);
                File.Copy(args[4],r.Forward.Profile.Path,true);SaveSystem.Generation=uint.Parse(args[5]);
                var raw=r.Journal.Mount.Return;LoadInventoryNative(r.Player.Inventory,new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,raw.BeforeInfo,raw.BeforeBytes,null));
                var drop=ItemDrop.Make(Program.Item("SwordIron",1,"prefab-default"));var data=drop.GetComponent<ZNetView>().Data;data.m_uid.Id=444;
                data.Set(BlueprintConstructionPayoutSeal.MountMarkerKey,raw.Operation.ToString("N"));data.Set(BlueprintConstructionPayoutSeal.OwnerKey,100L);data.Set(ZDOVars.s_itemData,Convert.FromBase64String(raw.ObservedBytes));
                drop.gameObject.Colliders=new[]{new Collider{Owner=drop.gameObject}};r.Stand.Returned.Clear();r.Stand.Returned.Add(drop);UnityEngine.Object.Loaded.Add(drop);
                BlueprintConstructionPayoutSeal.BeforeAwake(drop);BlueprintConstructionPayoutSeal.AfterAwake(drop,null);
                Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out r.Actor,out var why),why);
                Check(r.Actor.TryCredit(out why),why);
                Check(BlueprintConstructionMountReturnCleanup.TryBegin(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out var cleanup,out why),why);
                WaitRecovery(()=>cleanup.Status=="queued-removal"||cleanup.Finished,cleanup.Tick);Check(cleanup.Status=="queued-removal",cleanup.Reason);
                UnityEngine.Object.Loaded.Clear();ZNet.instance.SaveAction=()=>{SaveSystem.Generation++;WriteReturnWorld(r,"missing-drop");};WaitRecovery(()=>cleanup.Finished,cleanup.Tick);
                Check(cleanup.Status=="terminal"&&r.Stand.DetachCalls==0&&r.Player.Inventory.CreditCalls==1&&ZDOMan.instance.DestroyCalls==1,cleanup.Reason??"child replayed effects");
                Console.WriteLine("PASS new-process near-cent recovery");
            }
        }
        private static void WaitRecovery(Func<bool> done,Action tick){var end=DateTime.UtcNow.AddSeconds(8);while(!done()&&DateTime.UtcNow<end){tick();Thread.Sleep(1);}Check(done(),"recovery deadline");}
        private static ItemDrop ReloadDrop(Rig r,ItemDrop old,bool sameData)
        {
            var loaded=ItemDrop.Make(Program.Item(old.m_itemData.m_dropPrefab.name,1,"prefab-default"));
            var prior=old.GetComponent<ZNetView>().Data;var data=loaded.GetComponent<ZNetView>().Data;
            if(sameData)loaded.gameObject.RootView=old.gameObject.RootView;
            else {data.m_uid.Id=prior.m_uid.Id+100;data.Set(BlueprintConstructionPayoutSeal.MountMarkerKey,prior.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey,""));data.Set(BlueprintConstructionPayoutSeal.OwnerKey,prior.GetLong(BlueprintConstructionPayoutSeal.OwnerKey,0));data.Set(ZDOVars.s_itemData,prior.GetByteArray(ZDOVars.s_itemData));}
            loaded.gameObject.Colliders=new[]{new Collider{Owner=loaded.gameObject}};
            r.Stand.Returned.Clear();r.Stand.Returned.Add(loaded);UnityEngine.Object.Loaded.Clear();UnityEngine.Object.Loaded.Add(loaded);
            BlueprintConstructionPayoutSeal.BeforeAwake(loaded);BlueprintConstructionPayoutSeal.AfterAwake(loaded,null);return loaded;
        }
        internal static void LoadInventoryNative(Inventory inventory,BlueprintForeignPartData raw)
        {
            Check(BlueprintContainerInventory.TryRead(raw,n=>n.GetStableHashCode(),out var rows),"loaded inventory fixture unreadable");inventory.Items.Clear();
            foreach(var row in rows){var item=Program.Item(row.Prefab,row.Stack);item.m_durability=(float)row.DurabilityHundredths.Value*0.01f;
                item.m_gridPos=new Vector2i(row.X,row.Y);item.m_worldLevel=row.WorldLevel;item.m_quality=row.Quality;item.m_variant=row.Variant;item.m_pickedUp=row.PickedUp;item.m_equipped=row.Equipped;item.m_cheated=row.Cheated;
                item.m_crafterID=row.CrafterId;item.m_crafterName=row.CrafterName;item.m_customData=row.CustomData.ToDictionary(p=>p.Key,p=>p.Value);inventory.Items.Add(item);}
        }
        internal static void RecoveryRegression(string test)
        {
            using(var r=new Rig(test=="load"?(float?)0.05f:null))
            {
                r.UseDiskJournal();UnityEngine.Object.Loaded.Clear();
                foreach(var item in r.Player.Inventory.Items)item.m_pickedUp=true;
                Check(r.Begin(),"regression prepare");r.Actor.Tick();
                var drop=r.Stand.Returned.Single();UnityEngine.Object.Loaded.Add(drop);
                if(test=="identity")
                {
                    drop.GetComponent<ZNetView>().Data.m_uid.Id+=29;
                    Check(!BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out _,out _),
                        "same loaded-world epoch accepted substituted UID through a fresh actor");
                }
                else
                {
                    byte[] a=Convert.FromBase64String(r.Journal.Mount.MountedBytes),b=Convert.FromBase64String(r.Journal.Mount.Return.ObservedBytes);
                    Check(BitConverter.ToInt32(a,1)==5&&BitConverter.ToInt32(b,1)==4,"near-cent oracle must remain mounted5→observed4");
                    BlueprintConstructionMountContext.BeginReturnWorldLoad();
                    var old=ZNet.instance.World;ZNet.instance.World=new World{m_uid=old.m_uid,Directory=old.Directory};
                    BlueprintConstructionMountContext.EndReturnWorldLoad(null);
                    BlueprintConstructionPayoutSeal.AfterAwake(drop,null);
                    Check(drop.m_itemData.m_durability==(float)(4*0.01f)&&drop.GetComponent<ZNetView>().Data.GetByteArray(ZDOVars.s_itemData).SequenceEqual(b),"Load oracle/rawB changed");
                    Check(BlueprintConstructionMountDetachExecution.TryResume(r.Forward.Store,r.Forward.Store.Plan.Id,r.Forward.Part,out _,out var why),
                        "exact loaded rawB could not resume:"+why);
                }
            }
            Console.WriteLine("PASS recovery regression:"+test);
        }
    }
}
