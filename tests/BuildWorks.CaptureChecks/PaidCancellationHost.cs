using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
namespace PaidCancellationChecks {
public class HostObject { public static implicit operator bool(HostObject value)=>value!=null; }
public sealed class Inventory {}
public sealed class Player:HostObject {
 public static Player m_localPlayer; public bool Dead; public long Id=7; public Inventory Inventory=new Inventory();
 public readonly Dictionary<string,string> m_customData=new Dictionary<string,string>();
 public bool IsDead()=>Dead; public long GetPlayerID()=>Id; public Inventory GetInventory()=>Inventory;
}
public static class FileHelpers { public enum FileSource { Local,Cloud } }
public sealed class PlayerProfile { public long Id=7;public FileHelpers.FileSource m_fileSource;public string Path="payer.fch",Name="payer",Filename="payer";public long GetPlayerID()=>Id;public string GetPath()=>Path;public string GetName()=>Name;public string GetFilename()=>Filename; }
public sealed class Game:HostObject {
 public static Game instance;public PlayerProfile Identity=new PlayerProfile();public bool ShuttingDown;
 public bool IsShuttingDown()=>ShuttingDown;public PlayerProfile GetPlayerProfile()=>Identity;
}
public sealed class World {public long m_uid=11;}
public sealed class ZNet:HostObject {
 public static ZNet instance;public World World=new World();public bool Saving;
 public World GetWorld()=>World;public bool IsSaving()=>Saving;
}
public enum SaveSystemSessionFlags {DontSaveWorld,DontSaveCharacter}
public static class SaveSystem {
 public static uint Generation=11;public static readonly HashSet<SaveSystemSessionFlags> Flags=new HashSet<SaveSystemSessionFlags>();
 public static uint GetSaveNumber()=>Generation;public static bool HasSessionFlag(SaveSystemSessionFlags flag)=>Flags.Contains(flag);
}
namespace OstrixMods.BuildWorks.Geometry {
 public sealed class BlueprintForeignPartData {public string Info,Zdo;}
 public static class NativePlayerProfileReadback {
  public sealed class CustomDataEvidence {public long Player=7;public string ProfileHash="PROFILE";public Dictionary<string,string> CustomData=new Dictionary<string,string>();}
 }
 public static class NativeWorldStorageReadback {public sealed class AbsenceEvidence {public uint Generation;}}
}
namespace OstrixMods.BuildWorks {
 using OstrixMods.BuildWorks.Geometry;
 using Plans=OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
 using Journal=OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;
 __FINALIZER__
 public static class BlueprintStorageCapture {
  public static bool Valid=true;public static string Bytes="inventory";
  public static bool TrySnapshotInventory(Inventory inventory,out BlueprintForeignPartData data) {
   data=new BlueprintForeignPartData{Info="inventory",Zdo=Bytes};return Valid;
  }
 }
 public static class BlueprintStoragePlayerProfile {
  public static bool Valid=true;public static NativePlayerProfileReadback.CustomDataEvidence Evidence=new NativePlayerProfileReadback.CustomDataEvidence();
  public static bool TryReadSavedCustomData(Player player,Func<bool> custody,out NativePlayerProfileReadback.CustomDataEvidence profile,out string error) {
   profile=Evidence;error="Profile refused";return Valid&&custody();
  }
 }
 public sealed class BlueprintStorageWorldGeneration {
  public static bool Complete,Valid=true,Started=true,Captured=true;public static int Starts;
  private Func<bool> custody;public uint Generation;public bool IsCompleted=>Complete;
  public static bool TryCaptureAbsence(Guid op,int marker,IEnumerable<Guid> ids,Func<bool> custody,out BlueprintStorageWorldGeneration scan,out string error) {
   scan=new BlueprintStorageWorldGeneration{custody=custody,Generation=SaveSystem.Generation};error="Scan refused";return Captured&&custody();
  }
  public bool TryStart(System.Threading.CancellationToken token,out string error) {Starts++;error="Start refused";return Started&&!token.IsCancellationRequested&&custody();}
  public bool TryFinishAbsence(out NativeWorldStorageReadback.AbsenceEvidence proof,out string error) {
   proof=new NativeWorldStorageReadback.AbsenceEvidence{Generation=Generation};error="Absence refused";return Valid&&custody();
  }
 }
 public sealed class BlueprintConstructionProjectStore {
  public const int ConstructionMarkerKey=42;
  public sealed class Record {public bool Cancelled;public Guid CancelOperation;}
  public Record Plan=new Record();public BlueprintConstructionPayoutJournal.Record Payout=new BlueprintConstructionPayoutJournal.Record();
  public bool BeforeCommit,AfterCommit,CorruptTerminal;public int Commits;
  public Record Load(Guid id)=>Plan;
  public void CancelPaid(Guid id,long revision,Guid operation,uint generation,NativeWorldStorageReadback.AbsenceEvidence absence,NativePlayerProfileReadback.CustomDataEvidence profile) {
   Commits++;if(BeforeCommit)throw new Exception("Commit refused");
   Plan.Cancelled=true;Plan.CancelOperation=operation;Payout.Terminal=new BlueprintConstructionPayoutJournal.TerminalBinding{IssuanceHash=CorruptTerminal?"wrong":Payout.Hash};
   if(AfterCommit)throw new Exception("Postcommit failure");
  }
 }
 public sealed class BlueprintConstructionPayoutJournal {
  public const int IssuedSaved=3,ProfileCredited=2,CreditIntent=1,CleanupIntent=1,CleanupSaved=2;
  public sealed class Entry {public Guid Id=Guid.NewGuid();}
  public sealed class Cleanup {public int Phase;}
  public sealed class Collection {public Guid Entry;public int Phase=ProfileCredited;public Cleanup Removal;}
  public sealed class TerminalBinding {public string IssuanceHash;}
  public sealed class Record {
   public Guid Project=Guid.NewGuid(),Operation=Guid.NewGuid();public int Phase=IssuedSaved;public long Player=7,World=11,Revision=5;
   public uint MinimumWorldGeneration=9;public string Hash="ISSUANCE";public TerminalBinding Terminal;
   public Entry[] Items={new Entry()};public Collection[] Collections;
  }
  private readonly BlueprintConstructionProjectStore store;
  public BlueprintConstructionPayoutJournal(BlueprintConstructionProjectStore store) {this.store=store;}
  public Record Read(Guid project) {
   var r=store.Payout;return new Record{Project=r.Project,Operation=r.Operation,Phase=r.Phase,Player=r.Player,World=r.World,Revision=r.Revision,
    MinimumWorldGeneration=r.MinimumWorldGeneration,Hash=r.Hash,Terminal=r.Terminal,Items=r.Items,
    Collections=r.Collections?.Select(c=>new Collection{Entry=c.Entry,Phase=c.Phase,Removal=c.Removal==null?null:new Cleanup{Phase=c.Removal.Phase}}).ToArray()};
  }
  public static string IssuanceFingerprint(Record record)=>record.Hash;
  public static string ReceiptKey(Guid id)=>id.ToString("N");public static string ReceiptValue(Record record,int index)=>record.Items[index].Id.ToString("N")+"receipt";
 }
 public static class BlueprintConstructionPartJournal {public const int Committed=4;}
 public sealed class BlueprintConstructionCancellationJournal {
  public sealed class Entry {public int Phase=4;public Guid Operation=Guid.NewGuid();}
  public sealed class Record {public Entry[] Parts={new Entry()};}
  public BlueprintConstructionCancellationJournal(BlueprintConstructionProjectStore store) {}
  public Record Read(Guid project)=>new Record();
 }
 public sealed class BlueprintConstructionProjectRuntime {
  public static bool IsPrivateLocalPlacement=true;
  private readonly BlueprintConstructionProjectStore store;
  private readonly HashSet<Guid> cancellationProjects=new HashSet<Guid>(),cleanupProjects=new HashSet<Guid>();
  private Guid queued=Guid.Empty;private Operation execution=null,removal=null,collection=null,cleanup=null,payout=null;
  private BlueprintConstructionPaidCancellation paidCancellation;
  private bool PayoutBusy=>paidCancellation!=null&&!paidCancellation.Finished||new[]{execution,removal,collection,cleanup,payout}.Any(x=>x!=null&&!x.Finished);
  private string Failure;private Action<Exception> log=e=>{};private int retired;
  private sealed class Operation {public bool Finished=false;}
  private void RetireProject(Guid id) {retired++;}
  private BlueprintConstructionProjectRuntime(BlueprintConstructionProjectStore store) {this.store=store;}
  __SCHEDULER__
  private static int checks;
  private static void Check(bool value,string message) {checks++;if(!value)throw new Exception(message);}
  private static BlueprintConstructionProjectStore Reset() {
   Player.m_localPlayer=new Player();Game.instance=new Game();ZNet.instance=new ZNet();SaveSystem.Generation=11;SaveSystem.Flags.Clear();
   BlueprintStorageCapture.Valid=true;BlueprintStorageCapture.Bytes="inventory";BlueprintStoragePlayerProfile.Valid=true;
   BlueprintStoragePlayerProfile.Evidence=new NativePlayerProfileReadback.CustomDataEvidence();IsPrivateLocalPlacement=true;
   BlueprintStorageWorldGeneration.Complete=false;BlueprintStorageWorldGeneration.Valid=true;BlueprintStorageWorldGeneration.Captured=true;
   BlueprintStorageWorldGeneration.Started=true;BlueprintStorageWorldGeneration.Starts=0;return new BlueprintConstructionProjectStore();
  }
  private static void ConstructorRefused(Action<BlueprintConstructionProjectStore> change,string reason) {
   var s=Reset();change(s);try {new BlueprintConstructionPaidCancellation(s,s.Payout.Project);}catch(InvalidOperationException){Check(s.Commits==0,reason);return;}throw new Exception("Accepted "+reason);
  }
  private static void DuringRefused(Action<BlueprintConstructionProjectStore> change,string reason) {
   var s=Reset();var f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);change(s);BlueprintStorageWorldGeneration.Complete=true;f.Tick();
   Check(f.Status=="reconciliation"&&s.Commits==0,reason);f.Tick();Check(s.Commits==0,"Refused operation retried");
  }
  public static int Run() {
   checks=0;
   var s=Reset();var f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);f.Tick();Check(!f.Finished&&s.Commits==0,"Incomplete scan committed");
   BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(f.Status=="cancelled"&&s.Commits==1,"Fresh evidence did not commit");f.Tick();Check(s.Commits==1,"Completed operation committed twice");
   ConstructorRefused(s=>s.Payout.Phase=1,"Unsaved issuance");ConstructorRefused(s=>s.Plan.Cancelled=true,"Existing terminal");
   ConstructorRefused(s=>s.Payout.Player=8,"Wrong payer");ConstructorRefused(s=>s.Payout.World=12,"Wrong world");
   ConstructorRefused(s=>SaveSystem.Generation=8,"Rollback");ConstructorRefused(s=>SaveSystem.Generation=0,"Zero generation");ConstructorRefused(s=>SaveSystem.Generation=uint.MaxValue,"Maximum generation");
   ConstructorRefused(s=>IsPrivateLocalPlacement=false,"Remote or public context");ConstructorRefused(s=>Game.instance.Identity.m_fileSource=FileHelpers.FileSource.Cloud,"Cloud profile");
   ConstructorRefused(s=>BlueprintStoragePlayerProfile.Valid=false,"Invalid profile");ConstructorRefused(s=>BlueprintStorageWorldGeneration.Captured=false,"Unavailable scan");
   ConstructorRefused(s=>BlueprintStorageWorldGeneration.Started=false,"Unstarted scan");
   ConstructorRefused(s=>s.Payout.Collections=new[]{new BlueprintConstructionPayoutJournal.Collection{Phase=1}},"Pending credit");
   ConstructorRefused(s=>s.Payout.Collections=new[]{new BlueprintConstructionPayoutJournal.Collection{Removal=new BlueprintConstructionPayoutJournal.Cleanup{Phase=1}}},"Pending cleanup");
   ConstructorRefused(s=>s.Payout.Collections=new[]{new BlueprintConstructionPayoutJournal.Collection{Entry=s.Payout.Items[0].Id}},"Missing saved receipt");
   DuringRefused(s=>SaveSystem.Generation++,"Changed world generation");DuringRefused(s=>ZNet.instance.Saving=true,"Concurrent save");
   DuringRefused(s=>BlueprintStorageCapture.Bytes="changed","Changed inventory");DuringRefused(s=>Player.m_localPlayer.Dead=true,"Death");
   DuringRefused(s=>Game.instance.Identity=new PlayerProfile(),"Profile identity changed");DuringRefused(s=>BlueprintStoragePlayerProfile.Evidence.ProfileHash="changed","Saved profile changed");
   DuringRefused(s=>s.Payout.Revision++,"Journal revision changed");DuringRefused(s=>s.Payout.Hash="changed","Immutable issuance changed");
   DuringRefused(s=>BlueprintStorageWorldGeneration.Valid=false,"Construction still present");
   DuringRefused(s=>Game.instance.Identity.Path="other.fch","Profile path changed");DuringRefused(s=>ZNet.instance=new ZNet(),"Network instance changed");DuringRefused(s=>Player.m_localPlayer=new Player(),"Payer instance changed");
   DuringRefused(s=>SaveSystem.Flags.Add(SaveSystemSessionFlags.DontSaveCharacter),"Character saves disabled");
   s=Reset();f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);f.Refuse("suspend");BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(s.Commits==0&&f.Reason=="suspend","Suspend committed late");
   s=Reset();f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);s.BeforeCommit=true;BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(f.Status=="reconciliation"&&!s.Plan.Cancelled,"Failed commit removed project");
   s=Reset();f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);s.AfterCommit=true;BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(f.Status=="cancelled"&&s.Commits==1,"Confirmed postcommit failure repeated issuance");
   s=Reset();f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);s.AfterCommit=true;s.CorruptTerminal=true;BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(f.Status=="reconciliation","Wrong terminal fingerprint accepted");
   s=Reset();var item=s.Payout.Items[0].Id;var key=BlueprintConstructionPayoutJournal.ReceiptKey(item);var value=BlueprintConstructionPayoutJournal.ReceiptValue(s.Payout,0);
   s.Payout.Collections=new[]{new BlueprintConstructionPayoutJournal.Collection{Entry=item}};BlueprintStoragePlayerProfile.Evidence.CustomData[key]=value;
   try {new BlueprintConstructionPaidCancellation(s,s.Payout.Project);throw new Exception("Missing loaded receipt accepted");}catch(InvalidOperationException){Check(true,"Loaded receipt guard");}
   Player.m_localPlayer.m_customData[key]=value;f=new BlueprintConstructionPaidCancellation(s,s.Payout.Project);BlueprintStorageWorldGeneration.Complete=true;f.Tick();Check(f.Status=="cancelled","Settled credit prevented close before all cleanup");
   s=Reset();var r=new BlueprintConstructionProjectRuntime(s);r.cancellationProjects.Add(s.Payout.Project);r.TickPaidCancellation();
   Check(r.paidCancellation!=null&&r.PayoutBusy&&BlueprintStorageWorldGeneration.Starts==1,"Startup did not start one blocking finalizer");r.TickPaidCancellation();Check(BlueprintStorageWorldGeneration.Starts==1&&r.retired==0,"Scheduler repeated unfinished scan");
   BlueprintStorageWorldGeneration.Complete=true;r.TickPaidCancellation();Check(r.retired==1&&s.Plan.Cancelled&&r.cleanupProjects.Contains(s.Payout.Project),"Committed close did not retire presentation/retain cleanup");
   r.TickPaidCancellation();Check(r.retired==1&&s.Commits==1,"Scheduler repeated terminal cleanup");
   s=Reset();r=new BlueprintConstructionProjectRuntime(s);s.Payout.Collections=new[]{new BlueprintConstructionPayoutJournal.Collection{Phase=1}};
   r.cancellationProjects.Add(s.Payout.Project);r.TickPaidCancellation();Check(r.paidCancellation==null&&!r.PayoutBusy&&r.cancellationProjects.Count==0,"Pending intent locked recovery or scheduled infinite retries");
   s.Payout.Collections=null;r.cancellationProjects.Add(s.Payout.Project);r.TickPaidCancellation();Check(r.paidCancellation!=null,"Settled intent did not requeue finalization");
   return checks;
  }
 }
}
}
