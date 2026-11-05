using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    // Only native host/journal endpoints are substitutes; actor, persistence, profile/world readers are actual source.
    internal sealed class ArmorStand : Obj { }
    internal sealed partial class ItemStand : Obj
    {
        private ZNetView m_nview;
        private ItemDrop.ItemData m_queuedItem = null;
        internal bool Queued;
        internal bool m_canBeRemoved = true;
        internal new GameObject gameObject;
        private void OnDestroyed() { }
        internal Transform transform => gameObject.transform;
        internal T GetComponent<T>() where T:class => gameObject.GetComponent<T>();
        internal BlueprintForeignPartData Contents;
        internal ItemStand(ZNetView view) { m_nview=view; }
        internal bool HaveAttachment() => Contents != null;
        internal bool IsInvoking(string _) => Queued || m_queuedItem!=null;
    }
    internal sealed partial class ZNetScene
    {
        internal static ZNetScene instance;
        internal GameObject Prefab;
        internal readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>();
        internal GameObject GetPrefab(string name) => Prefabs.TryGetValue(name,out var prefab)?prefab:Prefab?.name == name ? Prefab : null;
        public static implicit operator bool(ZNetScene scene) => scene != null;
    }
    internal sealed partial class BlueprintConstructionProjectRuntime
    {
        internal static bool Admin=true;
        internal static bool IsPrivateLocalPlacement => Player.m_localPlayer && Game.instance && !Game.instance.IsShuttingDown() &&
            ZNet.instance && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated() && !ZNet.IsOpenServer() && ZNet.instance.GetPeers().Count==0 &&
            ZNet.instance.GetWorld().m_fileSource==FileHelpers.FileSource.Local && Admin;
    }
    internal sealed class BlueprintConstructionProjectStore
    {
        internal sealed class Pose { internal float[] Values={0,0,0,0,0,0,1,1,1,1}; }
        internal sealed class Part { internal Guid Id=Guid.NewGuid(); internal int SourceIndex=0; internal Pose WorldPose=new Pose(); }
        internal sealed class Foreign { internal BlueprintForeignPartData Data; internal BlueprintForeignPartData ToGeometry()=>Data; }
        internal sealed class Source { internal string prefabName="itemstand"; internal Foreign foreignData; }
        internal sealed class Blueprint { internal List<Source> parts=new List<Source>(); }
        internal sealed class Record {
            internal Guid Id=Guid.NewGuid(); internal long World=1,Revision=1; internal bool Cancelled;
            internal Blueprint Blueprint=new Blueprint(); internal List<Part> Parts=new List<Part>();
        }
        internal readonly Record Plan=new Record();
        internal bool LegacyReturnProducer;
        internal Record Load(Guid id) {
            if(id!=Plan.Id) throw new InvalidOperationException("unknown project");
            return new Record { Id=Plan.Id,World=Plan.World,Revision=Plan.Revision,Cancelled=Plan.Cancelled,Blueprint=Plan.Blueprint,Parts=Plan.Parts };
        }
    }
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int Intent=1,Observed=2,Committed=3,NoEffect=4;
        internal sealed class Record {
            internal Guid Project,Part,Operation; internal int Phase=Committed,PrefabHash=123;
            internal long World=1,Player=100,Creator=100; internal byte[] Receipt={10,20};
            internal SavePair Saved = new SavePair { BeforeGeneration=5, Generation=6 };
            internal bool RequiresReconciliation => Phase == Intent || Phase == Observed;
            internal PaidRow[] Paid = Array.Empty<PaidRow>();
            internal bool CostFree=false;
        }
        internal sealed class PaidRow { internal string Prefab=null,Properties=null; internal int Before=0,Paid=0,X=0,Y=0,Quality=1,Variant=0; }
        internal sealed class MountRecord {
            internal Guid Project,Part,ParentOperation,Operation; internal long World=1,Player=100; internal int Phase;
            internal string ParentFingerprint,BeforeInfo,BeforeBytes,MountedInfo,MountedBytes,AfterInfo,AfterBytes;
            internal byte[] Receipt;
            internal SavePair Saved;
            internal MountReturnRecord Return;
        }
        internal sealed class SavePair { internal uint BeforeGeneration,Generation; internal string ProfileHash,InventoryHash,WorldPayloadHash; }
        internal static BlueprintConstructionPartJournal Current;
        internal readonly Record Parent;
        internal MountRecord Mount;
        internal SavePair Pair;
        internal readonly List<string> Events=new List<string>();
        internal bool ObserveFails,ConfirmFails;
        internal BlueprintConstructionPartJournal(BlueprintConstructionProjectStore store) { Parent=Current.Parent; Shared=Current; }
        private readonly BlueprintConstructionPartJournal Shared;
        internal BlueprintConstructionPartJournal(Record parent) { Parent=parent; Shared=this; Current=this; }
        internal Record RequireSavedPayment(Guid project,Guid part,long payer) {
            if(Shared.Disk!=null) {
                var actual=Shared.Disk.RequireSavedPayment(project,part,payer);
                DiskFingerprints[actual.Operation]=OstrixMods.BuildWorks.BlueprintConstructionPartJournal.Fingerprint(actual);
                return Copy<Record>(actual);
            }
            if(Parent.Project!=project || Parent.Part!=part || Parent.Player!=payer || Parent.Phase!=Committed)
                throw new InvalidOperationException("unpaid or wrong payer");
            return Parent;
        }
        internal static string Fingerprint(Record record)=>DiskFingerprints.TryGetValue(record.Operation,out var value)?value:record.Operation+"/"+record.Player+"/"+record.PrefabHash;
        internal static readonly Dictionary<Guid,string> DiskFingerprints=new Dictionary<Guid,string>();
        internal MountRecord PrepareMount(Guid project,Guid part,Guid op,long player,long user,uint id,BlueprintForeignPartData before,
            BlueprintForeignPartData mounted,bool emptyTarget,out bool prepared) {
            if(Shared.Disk!=null) return Shared.Refresh(Shared.Disk.PrepareMount(project,part,op,player,user,id,before,mounted,emptyTarget,out prepared));
            prepared=false;
            if(Shared.Mount!=null && Shared.Mount.Phase!=NoEffect) throw new InvalidOperationException("mount replay refused");
            Shared.Events.Add("intent"); prepared=true;
            return Shared.Mount=new MountRecord { Project=project,Part=part,ParentOperation=Parent.Operation,Operation=op,Player=player,Phase=Intent,
                ParentFingerprint=Fingerprint(Parent),BeforeInfo=before.Info,BeforeBytes=before.Zdo,MountedInfo=mounted.Info,MountedBytes=mounted.Zdo };
        }
        internal MountRecord ReadMount(Guid project,Guid part)=>Shared.Disk==null?Shared.Mount:Shared.Refresh(Shared.Disk.ReadMount(project,part));
        internal Record Read(Guid project, Guid part) => Shared.Disk==null?Shared.Parent:Copy<Record>(Shared.Disk.Read(project,part));
        internal MountRecord ObserveMount(Guid project,Guid part,Guid op,BlueprintForeignPartData after,BlueprintForeignPartData actual) {
            if(Shared.ObserveFails) throw new InvalidOperationException("observation write failed");
            if(Shared.Disk!=null) return Shared.Refresh(Shared.Disk.ObserveMount(project,part,op,after,actual));
            var row=Shared.Mount;
            if(row.Phase!=Intent || row.Operation!=op || actual?.Info!=row.MountedInfo || actual.Zdo!=row.MountedBytes)
                throw new InvalidOperationException("unexpected attachment");
            Shared.Events.Add("observed"); row.AfterInfo=after.Info;row.AfterBytes=after.Zdo;row.Receipt=new byte[]{7,8,9};row.Phase=Observed;return row;
        }
        internal void MountNoEffect(Guid project,Guid part,Guid op,BlueprintForeignPartData before,bool emptyTarget) {
            if(Shared.Disk!=null) { Shared.Refresh(Shared.Disk.MountNoEffect(project,part,op,before,emptyTarget));return; }
            Shared.Events.Add("no-effect"); Shared.Mount.Phase=NoEffect;
        }
        internal void ConfirmMountSaved(Guid project,Guid part,Guid op,SavePair pair) {
            if(Shared.ConfirmFails) throw new InvalidOperationException("confirmation write failed");
            if(Shared.Disk!=null) { Shared.Refresh(Shared.Disk.ConfirmMountSaved(project,part,op,Copy<OstrixMods.BuildWorks.BlueprintConstructionPartJournal.SavePair>(pair)));return; }
            Shared.Events.Add("mount-saved");Shared.Pair=pair;Shared.Mount.Saved=pair;Shared.Mount.Phase=Committed;
        }
        internal void ConfirmSaved(Guid project,Guid part,Guid op,SavePair pair) { Shared.Events.Add("parent-saved");Shared.Pair=pair; }
    }
    internal static class BlueprintItemStandCapture {
        internal static bool TryCapture(Piece piece,out BlueprintForeignPartData data) { data=piece.gameObject.Stand.Contents;return true; }
    }
    internal static partial class BlueprintItemStandPlacement
    {
        private static readonly FieldInfo StandView=typeof(ItemStand).GetField("m_nview",BindingFlags.Instance|BindingFlags.NonPublic);
        private static readonly FieldInfo QueuedItem=typeof(ItemStand).GetField("m_queuedItem",BindingFlags.Instance|BindingFlags.NonPublic);
        internal static int NativeCalls;
        internal static int SelectionCalls;
        internal static string Mode;
        internal static Action BeforeNative;
        internal static bool CanRestore(Piece piece,BlueprintForeignPartData data)=>piece && data!=null;
        internal static bool CanRollback(GameObject target,bool forceDestroy=false)=>target && !target.Stand.Queued &&
            (!forceDestroy || !target.Stand.HaveAttachment());
        internal static bool TryApply(Player player,GameObject target,BlueprintForeignPartData data,out bool attempted,out bool rollbackSafe,Supply supply=null) {
            NativeCalls++; BeforeNative?.Invoke(); attempted=true;rollbackSafe=false;
            if(Mode=="no-effect") return false;
            var copy=supply.Item.Clone(); copy.m_equipped=false;
            supply.Item.m_equipped=false;
            if(--supply.Item.m_stack==0) player.Inventory.Items.Remove(supply.Item);
            if(Mode=="partial") return false;
            target.Stand.Contents=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,supply.Contents.Info,SnapshotItem(copy),null);
            target.RootView.Data.ByteArrays[ZDOVars.s_itemData]=Convert.FromBase64String(target.Stand.Contents.Zdo);
            return true;
        }
    }
    internal static class ConstructionMountExecutionChecks
    {
        private static int assertions;
        private static void Check(bool condition,string error) { assertions++;if(!condition)throw new InvalidOperationException("Mount actor: "+error); }
        internal sealed class Rig : IDisposable
        {
            internal readonly string Root=Path.Combine(Path.GetTempPath(),"BuildWorks-mount-"+Guid.NewGuid().ToString("N"));
            internal readonly TransferChecks.Frame Frame=new TransferChecks.Frame();
            internal readonly BlueprintConstructionProjectStore Store=new BlueprintConstructionProjectStore();
            internal readonly GameObject Target=new GameObject { name="itemstand",RootView=new ZNetView() };
            internal readonly ItemDrop.ItemData Sword=Program.Item("SwordIron",1,"actual-supplied");
            internal readonly BlueprintConstructionPartJournal Journal;
            internal readonly PlayerProfile Profile;
            internal BlueprintConstructionMountExecution Actor;
            internal int ProfileSaves,WorldSaves;
            internal bool CorruptWorld,SuppressGeneration;
            internal Guid Part=>Store.Plan.Parts[0].Id;
            internal Rig() {
                Directory.CreateDirectory(Root);SaveSystem.Generation=7;SaveSystem.Flags=0;SaveSystem.Suffix=""; ZNet.Open=false;
                ZNet.instance.World.Directory=Root; BlueprintConstructionProjectRuntime.Admin=true;BlueprintConstructionPartExecution.StorageUnresolved=false;
                BlueprintItemStandPlacement.NativeCalls=0;BlueprintItemStandPlacement.Mode=null;
                Sword.m_quality=2;Sword.m_gridPos=new Vector2i(0,0);Frame.Player.Inventory.Items.Add(Sword);
                Profile=new PlayerProfile { Path=Path.Combine(Root,"test.fch") };Game.instance.Profile=Profile;
                File.WriteAllBytes(Profile.Path,ProfileChecks.FileFor(Frame.Player));
                Store.Plan.Parts.Add(new BlueprintConstructionProjectStore.Part());
                Store.Plan.Blueprint.parts.Add(new BlueprintConstructionProjectStore.Source {
                    foreignData=new BlueprintConstructionProjectStore.Foreign { Data=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,"SwordIron:0:1:0",null,null) } });
                Journal=new BlueprintConstructionPartJournal(new BlueprintConstructionPartJournal.Record { Project=Store.Plan.Id,Part=Part,Operation=Frame.Operation });
                Target.Piece=new Piece { gameObject=Target };Target.Stand=new ItemStand(Target.RootView);
                Target.RootView.Data.Strings[BlueprintConstructionPartExecution.MarkerKey]=Frame.Operation.ToString("N");
                Target.RootView.Data.ByteArrays[BlueprintConstructionPartExecution.ReceiptKey]=Journal.Parent.Receipt;
                ZNetScene.instance=new ZNetScene { Prefab=new GameObject { name="itemstand" } };
                ZNetScene.instance.Prefab.Piece=new Piece { gameObject=ZNetScene.instance.Prefab };
                BlueprintConstructionPartExecution.MountTarget=Target;
                BlueprintItemStandPlacement.BeforeNative=()=> {
                    Check(Journal.Mount?.Phase==BlueprintConstructionPartJournal.Intent,"native call preceded durable intent");
                    Journal.Events.Add("native");
                };
                Target.RootView.Data.BeforeSet=()=>Check(Journal.Mount?.Phase==BlueprintConstructionPartJournal.Observed,"receipt preceded observation");
                Game.instance.SaveAction=()=> {
                    ProfileSaves++;Journal.Events.Add("profile");
                    var writer=new FileWriter(Profile.Path+".new",ProfileChecks.FileFor(Frame.Player));writer.Finish();
                    FileHelpers.ReplaceOldFile(Profile.Path,Profile.Path+".new",Profile.Path+".old",null,FileHelpers.FileSource.Local);
                    PlayerProfile.SavingFinished?.Invoke();
                };
                ZNet.instance.SaveAction=()=> {
                    WorldSaves++;Journal.Events.Add("world");if(!SuppressGeneration)SaveSystem.Generation++;
                    var receipt=Target.RootView.Data.GetByteArray(BlueprintConstructionMountExecution.ReceiptKey);
                    if(CorruptWorld)receipt=new byte[]{99};
                    WorldGenerationChecks.WriteWorld(Frame,Root,SaveSystem.Generation,receipt:Journal.Parent.Receipt,mounted:Target.Stand.Contents,mountReceipt:receipt,
                        worldId:Store.Plan.World,constructionOperation:Journal.Parent.Operation,prefabHash:Journal.Parent.PrefabHash);
                };
            }
            internal bool Begin()=>BlueprintConstructionMountExecution.TryBegin(Store,Store.Plan.Id,Part,out Actor,out _,out _);
            internal void UseDiskJournalBeforeMount()
            {
                var fixture=OstrixMods.BuildWorks.StoreTests.ConstructionMountJournalChecks.RuntimeFixture(Path.Combine(Root,"disk-journal"));
                var parent=fixture.Journal.RequireSavedPayment(fixture.Plan.Id,fixture.Part,100);
                Store.Plan.Id=fixture.Plan.Id;Store.Plan.World=42;Store.Plan.Parts[0].Id=fixture.Part;
                var actual=BlueprintConstructionPartJournal.Copy<BlueprintConstructionPartJournal.Record>(parent);
                foreach(var field in typeof(BlueprintConstructionPartJournal.Record).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))
                    field.SetValue(Journal.Parent,field.GetValue(actual));
                BlueprintConstructionPartJournal.DiskFingerprints[parent.Operation]=OstrixMods.BuildWorks.BlueprintConstructionPartJournal.Fingerprint(parent);
                Target.RootView.Data.BeforeSet=null;Target.RootView.Data.PrefabHash=parent.PrefabHash;
                Target.RootView.Data.Set(BlueprintConstructionPartExecution.MarkerKey,parent.Operation.ToString("N"));
                Target.RootView.Data.Set(BlueprintConstructionPartExecution.ReceiptKey,parent.Receipt);
                ZNet.instance.World.m_uid=42;SaveSystem.Generation=parent.Saved.Generation;Journal.Disk=fixture.Journal;Journal.DiskDirectory=fixture.Plans.DirectoryPath;
            }
            internal void Finish() {
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(!Actor.Finished && DateTime.UtcNow<deadline) { Actor.Tick();Thread.Sleep(1); }
                Check(Actor.Finished,"fixture deadline exceeded");
            }
            public void Dispose() {
                Actor?.Refuse("fixture cleanup");BlueprintItemStandPlacement.BeforeNative=null;BlueprintItemStandPlacement.Mode=null;
                Game.instance.SaveAction=null;ZNet.instance.SaveAction=null;SaveSystem.Flags=0;SaveSystem.Generation=7;PrivateArea.Allowed=true;
                BlueprintConstructionPartExecution.StorageUnresolved=false;BlueprintConstructionProjectRuntime.Admin=true;
                if(!Path.GetFullPath(Root).StartsWith(Path.GetFullPath(Path.GetTempPath())+"BuildWorks-mount-",StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Unexpected fixture cleanup root");
                Directory.Delete(Root,true);
            }
        }
        internal static void Run()
        {
            ConstructionMountAttachRecoveryChecks.Run();
            using(var rig=new Rig()) {
                rig.Journal.Parent.Phase=BlueprintConstructionPartJournal.Observed;
                rig.Frame.Placed.Container.View.Data.Raw=Convert.FromBase64String(Program.Container(3).Zdo);
                Check(BlueprintConstructionProjectPersistence.TryBegin(rig.Journal,rig.Journal.Parent,()=>true,out var saved,out _),"legacy structural save refused");
                var deadline=DateTime.UtcNow.AddSeconds(5);
                while(saved.State!=BlueprintConstructionProjectPersistence.Result.Verified && saved.State!=BlueprintConstructionProjectPersistence.Result.Refused && DateTime.UtcNow<deadline) {
                    saved.Tick();Thread.Sleep(1);
                }
                Check(saved.State==BlueprintConstructionProjectPersistence.Result.Verified && rig.Journal.Events.SequenceEqual(new[]{"profile","world","parent-saved"}) &&
                    rig.ProfileSaves==1 && rig.WorldSaves==1 && rig.Journal.Mount==null,"structural branch crossed into mount confirmation");
            }
            using(var rig=new Rig()) {
                Check(rig.Begin(),"valid prepare refused");Check(BlueprintItemStandPlacement.NativeCalls==0,"prepare invoked native attachment");
                rig.Finish();Check(rig.Actor.Status=="committed" && rig.Journal.Mount.Phase==3,"paired mount did not commit");
                Check(rig.ProfileSaves==1 && rig.WorldSaves==1 && rig.Journal.Pair.Generation==8 && rig.Journal.Pair.BeforeGeneration==7,"save pair not bound to next generation");
                Check(rig.Journal.Events.SequenceEqual(new[]{"intent","native","observed","profile","world","mount-saved"}),"mount/save ordering changed");
                Check(rig.Frame.Player.Inventory.ContainsItem(rig.Frame.Item) && rig.Frame.Item.m_stack==3 && !rig.Frame.Player.Inventory.ContainsItem(rig.Sword),"native mount changed recipe resources or retained supplied item");
                Check(rig.Journal.Mount.MountedInfo=="SwordIron:0:2:0" && rig.Target.RootView.Data.GetByteArray(BlueprintConstructionPartExecution.ReceiptKey).SequenceEqual(rig.Journal.Parent.Receipt),"archived quality replaced actual item or parent receipt changed");
                rig.Actor.Tick();Check(BlueprintItemStandPlacement.NativeCalls==1,"committed actor replayed");
                rig.Actor.Refuse("late cleanup");Check(rig.Actor.Status=="committed","late refusal overwrote committed status");
                Check(!BlueprintConstructionProjectPersistence.TryBeginMount(rig.Journal,rig.Store.Plan.Id,rig.Part,rig.Journal.Mount.Operation,
                    ()=>true,out _,out _) && rig.ProfileSaves==1 && rig.WorldSaves==1,"committed mount started another save pair");
                Check(!BlueprintConstructionMountExecution.TryBegin(rig.Store,rig.Store.Plan.Id,rig.Part,out _,out _,out _),"mounted target prepared again");
            }
            foreach(string mode in new[]{"no-effect","partial"})using(var rig=new Rig()) {
                BlueprintItemStandPlacement.Mode=mode;Check(rig.Begin(),"failure control prepare refused");rig.Finish();rig.Actor.Tick();
                Check(BlueprintItemStandPlacement.NativeCalls==1 && rig.ProfileSaves==0 && rig.WorldSaves==0,"failed native attempt replayed or saved");
                Check(mode=="no-effect" ? rig.Actor.Status=="no-effect" && rig.Journal.Mount.Phase==4 : rig.Actor.Status=="reconciliation" && rig.Journal.Mount.Phase==1,"unknown outcome was inferred or unchanged outcome lost");
            }
            foreach(Action<Rig> change in new Action<Rig>[] {
                r=>PrivateArea.Allowed=false,r=>r.Target.RootView.Owner=false,r=>r.Target.RootView=new ZNetView(),r=>r.Target.Stand.Queued=true,
                r=>r.Target.transform.position=new Vector3(1,0,0),r=>r.Store.Plan.Revision++,r=>r.Frame.Player.Dead=true,r=>ZNet.instance.Peers.Add(new object()),
                r=>Player.m_localPlayerExists=false,r=>ZNet.instance.Saving=true,r=>SaveSystem.Generation=0,r=>SaveSystem.Generation=uint.MaxValue,
                r=>SaveSystem.Flags=SaveSystemSessionFlags.DontSaveCharacter,r=>SaveSystem.Flags=SaveSystemSessionFlags.DontSaveWorld,
                r=>BlueprintConstructionPartExecution.StorageUnresolved=true,r=>r.Frame.Item.Custom="unrelated-inventory-changed"
            })using(var rig=new Rig()) {
                Check(rig.Begin(),"stale control prepare refused");change(rig);rig.Finish();
                Check(rig.Actor.Status=="reconciliation" && BlueprintItemStandPlacement.NativeCalls==0 && rig.Journal.Mount.Phase==1,"stale/foreign custody reached native call");
            }
            foreach(Action<Rig> change in new Action<Rig>[] {
                r=>r.CorruptWorld=true,r=>r.SuppressGeneration=true,r=>r.Journal.ObserveFails=true,r=>r.Journal.ConfirmFails=true,
                r=>Game.instance.SaveAction=()=>throw new IOException("profile save failed")
            })using(var rig=new Rig()) {
                change(rig);Check(rig.Begin(),"save failure control prepare refused");rig.Finish();rig.Actor.Tick();
                Check(rig.Actor.Status=="reconciliation" && BlueprintItemStandPlacement.NativeCalls==1 && rig.Journal.Mount.Phase!=3,"failed evidence committed or native retried");
            }
            foreach(Action<Rig> change in new Action<Rig>[] {
                r=>SaveSystem.Flags=SaveSystemSessionFlags.DontSaveWorld,r=>SaveSystem.Flags=SaveSystemSessionFlags.DontSaveCharacter,
                r=>r.Profile.m_fileSource=FileHelpers.FileSource.Cloud,r=>BlueprintConstructionPartExecution.StorageUnresolved=true,
                r=>BlueprintConstructionProjectRuntime.Admin=false
            })using(var rig=new Rig()) {
                change(rig);Check(!rig.Begin() && rig.Journal.Mount==null && BlueprintItemStandPlacement.NativeCalls==0,"invalid host prepared intent or invoked native path");
            }
            Console.WriteLine("PASS mount actor/pair persistence: "+assertions+" actual-source orchestration/guard assertions with inert native/journal endpoints; real detached profile/world readers, no Valheim proof");
        }
    }
}
