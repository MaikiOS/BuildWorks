using System;
using System.Linq;
using System.Collections.Generic;
namespace PayoutInteractionChecks {
 public class HostObject { public static implicit operator bool(HostObject value)=>value!=null; }
 public struct Vector3 {
  public float x,y,z; public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
  public static float Distance(Vector3 a,Vector3 b)=>(float)Math.Sqrt((a.x-b.x)*(a.x-b.x)+(a.y-b.y)*(a.y-b.y)+(a.z-b.z)*(a.z-b.z));
 }
 public sealed class Collider:HostObject {
  public bool enabled=true; public ItemDrop Drop; public Vector3 Point;
  public Vector3 ClosestPoint(Vector3 eye)=>Point;
  public T GetComponentInParent<T>() where T:class=>Drop as T;
 }
 public sealed class GameObject { public ItemDrop Drop; public bool activeInHierarchy=true; public T GetComponentInParent<T>() where T:class=>Drop as T; }
 public class Humanoid:HostObject {}
 public sealed class Player:Humanoid {
  public static Player m_localPlayer; public bool Dead; public GameObject Hover; public long Id=7; public int Messages;
  public float m_maxInteractDistance=5; public Vector3 Eye; public Vector3 GetEyePoint()=>Eye;
  public bool IsDead()=>Dead; public long GetPlayerID()=>Id; public GameObject GetHoverObject()=>Hover;
  public void Message(MessageHud.MessageType type,string text) { Messages++; }
 }
 public static class MessageHud { public enum MessageType { Center } }
 public sealed class ItemDrop:HostObject {
  public GameObject gameObject=new GameObject();
  public Collider[] Colliders; public float Offset;
  public T[] GetComponentsInChildren<T>() where T:class=>Colliders.Cast<T>().ToArray();
  public float GetHoverOffset()=>Offset;
  public bool Held=true; public ZNetView View=new ZNetView(); public T GetComponent<T>() where T:class=>View as T;
 }
 public sealed class ZNetView:HostObject {
  public bool Valid=true,Owner=true; public ZDO Data=new ZDO();
  public bool IsValid()=>Valid; public bool IsOwner()=>Owner; public ZDO GetZDO()=>Data;
 }
 public struct ZDOID { public bool None; public bool IsNone()=>None; }
 public sealed class ZDO {
  public bool Valid=true,Persistent=true; public ZDOID m_uid; public long Owner=7; public string Marker;
  public bool IsValid()=>Valid; public long GetLong(int key,long fallback)=>Owner; public string GetString(int key,string fallback)=>Marker??fallback;
 }
 public static class BlueprintConstructionPayoutSeal {
  public const int OwnerKey=1,MarkerKey=2; public static bool IsHeld(ItemDrop source)=>source && source.Held;
 }
 public static class BuildWorksLocalization { public static string Text(string key)=>key; }
 public sealed class Store {
  public readonly Dictionary<Guid,BlueprintConstructionPayoutJournal.Record> Records=new Dictionary<Guid,BlueprintConstructionPayoutJournal.Record>();
  public int Reads; public bool Broken;
  public IEnumerable<Guid> Ids()=>Records.Keys;
 }
 public sealed class BlueprintConstructionPayoutJournal {
  public const int IssuedSaved=3,ProfileCredited=2,CreditIntent=1,CleanupIntent=1,CleanupSaved=2;
  public sealed class Entry { public Guid Id=Guid.NewGuid(); }
  public sealed class Removal { public int Phase; }
  public sealed class Credit { public int Phase=ProfileCredited; public Removal Removal; }
  public sealed class Record {
   public int Phase=IssuedSaved; public long Player=7,World=11; public Entry[] Items={new Entry(),new Entry()}; public Credit[] Collections;
  }
  private readonly Store store; public BlueprintConstructionPayoutJournal(Store store) { this.store=store; }
  public Record Read(Guid id) { store.Reads++; if(store.Broken)throw new InvalidOperationException("Corrupt journal"); return store.Records[id]; }
 }
 public sealed class BlueprintConstructionPayoutCollection {
  __REACH__
  public static int Created; public static bool Throw,AlreadySaved;
  public Guid Project; public int Index; public string Status="checking-removal",Reason="credit failed",TickResult;
  public bool Finished=>Status=="profile-credited"||Status=="reconciliation";
  public BlueprintConstructionPayoutCollection(Store store,Guid id,int index) {
   if(Throw)throw new InvalidOperationException("Credit constructor refused exact evidence");
   Created++;Project=id;Index=index;if(AlreadySaved)Status="profile-credited";
  }
  public void Tick() { if(TickResult!=null)Status=TickResult; }
 }
 public sealed class BlueprintConstructionPayoutCleanup {
  public static int Created,Attempts; public static bool Throw;
  public Guid Project; public int Index; public string Status="checking-source",Reason="cleanup failed",TickResult;
  public bool Finished=>Status=="cleanup-saved"||Status=="reconciliation";
  public BlueprintConstructionPayoutCleanup(Store store,Guid id,int index) {
   Attempts++;if(Throw)throw new InvalidOperationException("Cleanup constructor refused N/N+1 evidence");
   Created++;Project=id;Index=index;
  }
  public void Tick() { if(TickResult!=null)Status=TickResult; }
 }
 public sealed class Operation { public bool Finished; }
 public static class BuildWorksPlugin {
  public static BlueprintConstructionProjectRuntime ConstructionProjects;
  __PREFIX__
  public static bool Interact(ItemDrop source,Humanoid actor,bool repeat,bool alt,ref bool result)=>Prefix(source,actor,repeat,alt,ref result);
 }
 public sealed class BlueprintConstructionProjectRuntime {
  private Store store=new Store(); private long world=11; private bool context=true;
  internal static bool IsPrivateLocalPlacement=true;
  private Guid queued; private Operation execution,removal,payout;
  private BlueprintConstructionPayoutCollection collection; private BlueprintConstructionPayoutCleanup cleanup;
  private readonly HashSet<Guid> cleanupProjects=new HashSet<Guid>();
  private string Failure; private Action<Exception> log=error=>{}; private int closed;
  private bool PayoutBusy=>payout!=null&&!payout.Finished||collection!=null&&!collection.Finished||cleanup!=null&&!cleanup.Finished;
  private void RequireContext() { if(!context)throw new InvalidOperationException("Context closed"); }
  private void Close() { closed++; }
  __START__
  __BEGIN__
  __TICK__
  private static int count;
  private static void Check(bool ok,string message) { count++;if(!ok)throw new Exception(message); }
  private static (BlueprintConstructionProjectRuntime R,ItemDrop D,Guid Id) Reset() {
   BlueprintConstructionPayoutCollection.Created=0;BlueprintConstructionPayoutCollection.Throw=false;BlueprintConstructionPayoutCollection.AlreadySaved=false;
   BlueprintConstructionPayoutCleanup.Created=0;BlueprintConstructionPayoutCleanup.Attempts=0;BlueprintConstructionPayoutCleanup.Throw=false;
   IsPrivateLocalPlacement=true;var r=new BlueprintConstructionProjectRuntime();var id=Guid.NewGuid();
   var record=new BlueprintConstructionPayoutJournal.Record();r.store.Records.Add(id,record);
   var d=new ItemDrop();d.Colliders=new[]{new Collider{Drop=d}};d.View.Data.Marker=record.Items[0].Id.ToString("N");
   Player.m_localPlayer=new Player { Hover=new GameObject {Drop=d} };BuildWorksPlugin.ConstructionProjects=r;
   return (r,d,id);
  }
  private static void Refused(Action<BlueprintConstructionProjectRuntime,ItemDrop,Guid> change,string message) {
   var t=Reset();change(t.R,t.D,t.Id);
   Check(!t.R.TryBeginCollection(t.D,Player.m_localPlayer)&&BlueprintConstructionPayoutCollection.Created==0,message);
  }
  public static int Run() {
   count=0;
   var t=Reset();bool result=false;t.D.Held=false;
   Check(BuildWorksPlugin.Interact(t.D,Player.m_localPlayer,false,false,ref result)&&!result,"Ordinary native interaction intercepted");
   t.D.Held=true;result=true;
   Check(!BuildWorksPlugin.Interact(t.D,Player.m_localPlayer,true,false,ref result)&&!result&&BlueprintConstructionPayoutCollection.Created==0,"Held repeat started a credit");
   Check(!BuildWorksPlugin.Interact(t.D,Player.m_localPlayer,false,true,ref result)&&result&&BlueprintConstructionPayoutCollection.Created==1,"Explicit alt-use did not route exactly once");
   Check(t.R.collection.Project==t.Id&&t.R.collection.Index==0&&t.R.closed==1,"Interaction lost journal identity");
   Check(!BuildWorksPlugin.Interact(t.D,Player.m_localPlayer,false,false,ref result)&&result&&BlueprintConstructionPayoutCollection.Created==1,"Busy repeat routed another credit");
   t=Reset();Check(!t.R.TryBeginCollection(t.D,new Humanoid()),"Remote actor admitted");
   t=Reset();Check(!t.R.TryBeginCollection(null,Player.m_localPlayer),"Null source admitted");
   Refused((r,d,id)=>Player.m_localPlayer=null,"Missing local player admitted");
   Refused((r,d,id)=>Player.m_localPlayer.Dead=true,"Dead payer admitted");
   Refused((r,d,id)=>r.context=false,"Closed context admitted");
   Refused((r,d,id)=>IsPrivateLocalPlacement=false,"Public/remote context admitted");
   Refused((r,d,id)=>d.Held=false,"Unsealed source admitted");
   Refused((r,d,id)=>Player.m_localPlayer.Hover=null,"Missing native hover admitted");
   Refused((r,d,id)=>Player.m_localPlayer.Hover.Drop=new ItemDrop(),"Another hover admitted");
   Refused((r,d,id)=>Player.m_localPlayer.Eye=new Vector3(20,0,0),"Stale distant hover admitted");
   Refused((r,d,id)=>d.gameObject.activeInHierarchy=false,"Inactive source admitted");
   Refused((r,d,id)=>d.Colliders[0].enabled=false,"Disabled source collider admitted");
   Refused((r,d,id)=>d.Colliders[0].Drop=new ItemDrop(),"Another child's collider admitted");
   Refused((r,d,id)=>d.Colliders=Array.Empty<Collider>(),"Source with no native collider admitted");
   Refused((r,d,id)=>Player.m_localPlayer.m_maxInteractDistance=float.PositiveInfinity,"Unbounded native reach admitted");
   Refused((r,d,id)=>d.Offset=float.NaN,"NaN source reach admitted");
   Refused((r,d,id)=>Player.m_localPlayer.Eye=new Vector3(5,0,0),"Exact native excluded distance admitted");
   t=Reset();Player.m_localPlayer.Eye=new Vector3(4.99f,0,0);
   Check(BlueprintConstructionPayoutCollection.WithinReach(Player.m_localPlayer,t.D),"Native within-range collider rejected");
   Player.m_localPlayer.Eye=new Vector3(5.5f,0,0);t.D.Offset=1;
   Check(BlueprintConstructionPayoutCollection.WithinReach(Player.m_localPlayer,t.D),"Native hover offset ignored");
   Player.m_localPlayer.Eye=new Vector3(8,0,0);
   Check(!BlueprintConstructionPayoutCollection.WithinReach(Player.m_localPlayer,t.D),"Async movement did not revoke reach");
   Refused((r,d,id)=>r.queued=Guid.NewGuid(),"Queued construction admitted collection");
   Refused((r,d,id)=>r.execution=new Operation(),"Active native execution admitted collection");
   Refused((r,d,id)=>r.removal=new Operation(),"Active removal admitted collection");
   Refused((r,d,id)=>r.payout=new Operation(),"Active issuance admitted collection");
   Refused((r,d,id)=>d.View=null,"Missing view admitted");
   Refused((r,d,id)=>d.View.Valid=false,"Invalid view admitted");
   Refused((r,d,id)=>d.View.Owner=false,"Unowned view admitted");
   Refused((r,d,id)=>d.View.Data=null,"Missing data admitted");
   Refused((r,d,id)=>d.View.Data.Valid=false,"Invalid data admitted");
   Refused((r,d,id)=>d.View.Data.Persistent=false,"Transient data admitted");
   Refused((r,d,id)=>d.View.Data.m_uid=new ZDOID{None=true},"Missing network identity admitted");
   Refused((r,d,id)=>d.View.Data.Owner=8,"Wrong source payer admitted");
   Refused((r,d,id)=>d.View.Data.Marker=Guid.NewGuid().ToString("D"),"Noncanonical marker admitted");
   Refused((r,d,id)=>d.View.Data.Marker=Guid.Empty.ToString("N"),"Empty marker admitted");
   Refused((r,d,id)=>d.View.Data.Marker=Guid.NewGuid().ToString("N"),"Unknown entitlement admitted");
   Refused((r,d,id)=>r.store.Records[id].Phase=1,"Unsaved issuance admitted");
   Refused((r,d,id)=>r.store.Records[id].Player=8,"Wrong journal payer admitted");
   Refused((r,d,id)=>r.store.Records[id].World=12,"Wrong journal world admitted");
   Refused((r,d,id)=>r.store.Records.Add(Guid.NewGuid(),r.store.Records[id]),"Duplicate entitlement admitted");
   Refused((r,d,id)=>r.store.Broken=true,"Corrupt journal admitted");
   Refused((r,d,id)=>BlueprintConstructionPayoutCollection.Throw=true,"Rejected package/profile/order bypassed constructor");
   t=Reset();BlueprintConstructionPayoutCollection.AlreadySaved=true;
   Check(t.R.TryBeginCollection(t.D,Player.m_localPlayer)&&t.R.cleanupProjects.Contains(t.Id),"Saved receipt recovery did not schedule cleanup");
   t=Reset();t.R.cleanupProjects.Add(t.Id);t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCollection.Created==0&&BlueprintConstructionPayoutCleanup.Created==0&&t.R.cleanupProjects.Count==0,"Fresh issuance auto-collected/cleaned");
   t=Reset();var record=t.R.store.Records[t.Id];record.Collections=new[]{new BlueprintConstructionPayoutJournal.Credit()};
   t.R.cleanupProjects.Add(t.Id);t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Created==1&&t.R.cleanup.Index==0&&record.Items.Length==2,"Partial collected prefix waited for every item");
   t.R.TickPayoutCollection();Check(BlueprintConstructionPayoutCleanup.Created==1,"Pending cleanup restarted");
   record.Collections[0].Removal=new BlueprintConstructionPayoutJournal.Removal{Phase=2};t.R.cleanup.TickResult="cleanup-saved";
   t.R.TickPayoutCollection();t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Created==1&&t.R.cleanupProjects.Count==0,"Saved cleanup retried deletion");
   t=Reset();record=t.R.store.Records[t.Id];record.Collections=new[]{new BlueprintConstructionPayoutJournal.Credit{Phase=1}};
   t.R.cleanupProjects.Add(t.Id);t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Created==0&&t.R.cleanupProjects.Count==0,"Uncertain credit started cleanup");
   t=Reset();record=t.R.store.Records[t.Id];record.Collections=new[]{new BlueprintConstructionPayoutJournal.Credit(),new BlueprintConstructionPayoutJournal.Credit{Removal=new BlueprintConstructionPayoutJournal.Removal{Phase=1}}};
   t.R.cleanupProjects.Add(t.Id);t.R.TickPayoutCollection();
   Check(t.R.cleanup.Index==1&&BlueprintConstructionPayoutCleanup.Created==1,"Existing cleanup intent lost priority");
   t.R.cleanup.TickResult="reconciliation";t.R.TickPayoutCollection();t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Created==1&&t.R.Failure=="cleanup failed"&&t.R.cleanupProjects.Count==0,"Failed cleanup retried or hid reason");
   t=Reset();record=t.R.store.Records[t.Id];record.Collections=new[]{new BlueprintConstructionPayoutJournal.Credit()};
   t.R.cleanupProjects.Add(t.Id);BlueprintConstructionPayoutCleanup.Throw=true;t.R.TickPayoutCollection();t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Attempts==1&&t.R.Failure!=null,"Constructor failure repeated every frame");
   t=Reset();Check(t.R.TryBeginCollection(t.D,Player.m_localPlayer),"Explicit collection failed");
   t.R.store.Records[t.Id].Collections=new[]{new BlueprintConstructionPayoutJournal.Credit()};t.R.collection.TickResult="profile-credited";t.R.TickPayoutCollection();
   Check(BlueprintConstructionPayoutCleanup.Created==1,"Completed collection did not automatically start cleanup");
   t=Reset();record=t.R.store.Records[t.Id];record.Collections=new[]{new BlueprintConstructionPayoutJournal.Credit()};t.R.cleanupProjects.Add(t.Id);
   t.R.queued=Guid.NewGuid();t.R.TickPayoutCollection();Check(BlueprintConstructionPayoutCleanup.Created==0&&t.R.cleanupProjects.Contains(t.Id),"Busy construction lost or ran cleanup work");
   t.R.queued=Guid.Empty;IsPrivateLocalPlacement=false;t.R.TickPayoutCollection();Check(BlueprintConstructionPayoutCleanup.Created==0,"Remote scheduler admitted cleanup");
   t=Reset();t.R.cleanupProjects.Add(t.Id);t.R.TickPayoutCollection();int reads=t.R.store.Reads;
   for(int i=0;i<10;i++)t.R.TickPayoutCollection();Check(t.R.store.Reads==reads,"Idle frames reread all project journals");
   return count;
  }
 }
}
