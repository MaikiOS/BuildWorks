using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Parts = RefundabilityChecks.BlueprintConstructionPartJournal;
namespace RefundabilityChecks {
 public class HostObject { public static implicit operator bool(HostObject value)=>value!=null; }
 public class Piece:HostObject {} public class WearNTear:HostObject { public Action Removed;public void Remove(bool suppress){RemovalJournal.Deletes++;Removed();} } public class Rigidbody:HostObject {}
 public struct Vector2i { public int x,y;public Vector2i(int x,int y){this.x=x;this.y=y;} }
 public sealed class GameObject:HostObject {
  public readonly Dictionary<Type,object> Components=new Dictionary<Type,object>();
  public T GetComponent<T>() where T:class=>Components.TryGetValue(typeof(T),out var value)?value as T:null;
 }
 public sealed class ZNetView:HostObject { public bool m_persistent=true; }
 public sealed class ObjectDB:HostObject {
  public static ObjectDB instance;public readonly Dictionary<string,GameObject> Prefabs=new Dictionary<string,GameObject>();
  public GameObject GetItemPrefab(string name)=>Prefabs.TryGetValue(name,out var value)?value:null;
 }
 public static class Version { public enum Item:byte { Current=110 } }
 public sealed class ZPackage { readonly byte[] bytes;public ZPackage(byte[] bytes){this.bytes=bytes;}public byte ReadByte()=>bytes[0]; }
 public static class TextHash { public static int GetStableHashCode(this string text)=>text=="Wood"?1:text=="Resin"?2:3; }
 public sealed class ItemDrop:HostObject {
  public ItemData m_itemData=new ItemData();public Action m_onDrop;
  public sealed class Shared { public int m_maxStackSize=50,m_variants=4; }
  public sealed class Damage { public float Value;public float GetTotalDamage()=>Value; }
  public sealed class ItemData {
   public Shared m_shared=new Shared();public Dictionary<string,string> m_customData=new Dictionary<string,string>();
   public int m_quality=1,m_variant,m_stack=1,CloneMode;public bool m_equipped,m_cheated;public float DamageValue=1;
   public GameObject m_dropPrefab;public Vector2i m_gridPos;
   public static int Clones,Loads,LoadHash=1,LoadQuality=1,LoadVariant;public static bool LoadCustody;
   public ItemData Clone() {
    Clones++;if(CloneMode==1)return null;if(CloneMode==2)return this;
    var copy=(ItemData)MemberwiseClone();if(CloneMode!=3)copy.m_customData=new Dictionary<string,string>(m_customData);return copy;
   }
   public static int Load(ZPackage package,ItemData item,Version.Item version) {
    Loads++;item.m_quality=LoadQuality;item.m_variant=LoadVariant;item.m_customData.Clear();
    if(LoadCustody)item.m_customData[Journal.CustodyKey]="already";return LoadHash;
   }
   public Damage GetDamage()=>new Damage{Value=DamageValue};
  }
 }
 public sealed class Plans {
  public sealed class Part { public Guid Id=Guid.NewGuid(); }
  public sealed class Record { public Part[] Parts; }
  public Record Plan=new Record();public readonly Dictionary<Guid,BlueprintConstructionPartJournal.Record> Receipts=new Dictionary<Guid,BlueprintConstructionPartJournal.Record>();
  public Record Load(Guid project)=>Plan;
 }
 public sealed class BlueprintConstructionPartJournal {
  public const int Intent=1,Observed=2,Committed=3,NoEffect=4;
  public sealed class PaidRow { public string Prefab="Wood",Properties=Convert.ToBase64String(new byte[]{110});public int Quality=1,Variant,Paid=2; }
  public sealed class Record { public int Phase=Committed;public long Player=7;public PaidRow[] Paid={new PaidRow()};public bool RequiresReconciliation=>Phase==Intent||Phase==Observed; }
  readonly Plans plans;public BlueprintConstructionPartJournal(Plans plans){this.plans=plans;}
  public Record Read(Guid project,Guid part)=>plans.Receipts[part];
 }
 public static class Journal {
  public const string CustodyKey="custody";public static int Validated,CapacityChecks;public static bool RejectProperties,RejectCapacity;
  public sealed class Entry { public Guid Id;public string Blob; }
  public static void RequireIssuanceBudget(BlueprintForeignPartData inventory,IEnumerable<Entry> entries) {
   var copy=entries.ToArray();CapacityChecks++;if(inventory==null||RejectCapacity)throw new InvalidDataException("Issuance budget refused");
  }
  public static void ValidateItem(BlueprintConstructionPartJournal.PaidRow row,Entry entry) {
   if(RejectProperties)throw new InvalidDataException("Exact property guard refused");
   if(entry.Id==Guid.Empty||entry.Blob!=Convert.ToBase64String(new byte[]{110}))throw new Exception("Preflight skipped packaged entry");Validated++;
  }
 }
 public static class BlueprintConstructionPayoutSeal { public static byte[] Package(ItemDrop.ItemData item)=>new byte[]{110}; }
 public sealed class BlueprintForeignPartData {}
 public static class BuildWorksLocalization { public static string Text(string key)=>key; }
 public static class SaveSystem { public static uint GetSaveNumber()=>5; }
 public sealed class RemovalJournal {
  public sealed class Entry { public int Phase;public Guid Operation=Guid.NewGuid(); }
  public sealed class Record { public Entry[] Parts; }
  public static int Prepares,Intents,Deletes;readonly Plans plans;Record record;
  public RemovalJournal(Plans plans){this.plans=plans;}
  public Record Prepare(Guid project,long revision,Guid operation,long payer){Prepares++;return record=new Record{Parts=plans.Receipts.Values.Select(r=>new Entry{Phase=r.Phase}).ToArray()};}
  public Record BeginRemoval(Guid project,Guid operation,uint generation){Intents++;return record;}
 }
 public static class BlueprintConstructionPayoutIssuance {
  __PREFLIGHT__
  __RESTORE__
  static readonly BlueprintForeignPartData beforeInventory=new BlueprintForeignPartData();
  static void RequireRefundable(Plans plans,Guid project,long player)=>RequireRefundable(plans,project,player,beforeInventory);
  sealed class Data { public bool IsValid()=>true; }
  sealed class Witness { public WearNTear Wear=new WearNTear();public Data Data=new Data();public bool WearConfirmed,NetworkConfirmed; }
  sealed class Payer { public long GetPlayerID()=>7; }
  static Witness active;static RemovalJournal.Record record;static Guid[] operations;static string Status;
  static bool Custody()=>true;
  static void Refuse(string reason){Status="reconciliation";}
  static void StartRemoval(Plans plans,Guid project) {
   long revision=1;Guid operation=Guid.NewGuid();var player=new Payer();var journal=new RemovalJournal(plans);
   var targets=plans.Receipts.Values.Where(r=>r.Phase==Parts.Committed).Select(r=>new Witness()).ToArray();
   foreach(var target in targets)target.Wear.Removed=()=>{target.WearConfirmed=target.NetworkConfirmed=true;};
__REMOVAL__
  }
  static int checks;
  static void Check(bool valid,string reason){checks++;if(!valid)throw new Exception(reason);}
  static (Plans plans,GameObject prefab,ItemDrop drop) Fresh() {
   ObjectDB.instance=new ObjectDB();var prefab=new GameObject();var drop=new ItemDrop();
   prefab.Components[typeof(ItemDrop)]=drop;prefab.Components[typeof(ZNetView)]=new ZNetView();prefab.Components[typeof(Rigidbody)]=new Rigidbody();
   ObjectDB.instance.Prefabs["Wood"]=prefab;drop.m_itemData.m_dropPrefab=prefab;drop.m_itemData.m_customData["original"]="kept";
   ItemDrop.ItemData.Clones=ItemDrop.ItemData.Loads=0;ItemDrop.ItemData.LoadHash=ItemDrop.ItemData.LoadQuality=1;
   ItemDrop.ItemData.LoadVariant=0;ItemDrop.ItemData.LoadCustody=false;Journal.Validated=0;Journal.RejectProperties=false;
   Journal.CapacityChecks=0;Journal.RejectCapacity=false;
   RemovalJournal.Prepares=RemovalJournal.Intents=RemovalJournal.Deletes=0;Status=null;
   var plans=new Plans();plans.Plan.Parts=new[]{new Plans.Part(),new Plans.Part()};
   foreach(var part in plans.Plan.Parts)plans.Receipts[part.Id]=new BlueprintConstructionPartJournal.Record();
   return (plans,prefab,drop);
  }
  static void Refuse(Action<Plans,GameObject,ItemDrop> change,string name,bool beforeLoad=false) {
   var f=Fresh();change(f.plans,f.prefab,f.drop);
   try {RequireRefundable(f.plans,Guid.NewGuid(),7);throw new Exception("Unsupported refund admitted: "+name);}
   catch(Exception error) when(error is InvalidOperationException||error is InvalidDataException) {}
   Check(f.drop.m_itemData.m_customData.Count==1&&f.drop.m_itemData.m_customData["original"]=="kept","Preflight modified prefab: "+name);
   if(beforeLoad)Check(ItemDrop.ItemData.Loads==0,"Shared/null clone reached native Load: "+name);
  }
  public static int Run() {
   checks=0;var f=Fresh();RequireRefundable(f.plans,Guid.NewGuid(),7);
   Check(ItemDrop.ItemData.Clones==2&&ItemDrop.ItemData.Loads==2&&Journal.Validated==2,"Not every committed row restored and validated");
   Check(f.drop.m_itemData.m_customData.Count==1&&f.drop.m_itemData.m_customData["original"]=="kept","Successful preflight modified prefab");
   foreach(int phase in new[]{BlueprintConstructionPartJournal.Intent,BlueprintConstructionPartJournal.Observed})
    Refuse((p,g,d)=>p.Receipts[p.Plan.Parts[1].Id].Phase=phase,"unsettled receipt",true);
   Refuse((p,g,d)=>p.Receipts[p.Plan.Parts[1].Id].Player=8,"mixed payer",true);
   f=Fresh();f.plans.Receipts[f.plans.Plan.Parts[0].Id].Phase=BlueprintConstructionPartJournal.NoEffect;
   f.plans.Receipts[f.plans.Plan.Parts[0].Id].Player=8;RequireRefundable(f.plans,Guid.NewGuid(),7);
   Check(Journal.Validated==1,"NoEffect row treated as a refundable contribution");
   f=Fresh();foreach(var row in f.plans.Receipts.Values)row.Paid=Array.Empty<BlueprintConstructionPartJournal.PaidRow>();RequireRefundable(f.plans,Guid.NewGuid(),7);
   Check(Journal.Validated==0,"Cost-free committed parts invented a paid row");
   Refuse((p,g,d)=>ObjectDB.instance.Prefabs.Clear(),"missing prefab");
   Refuse((p,g,d)=>g.Components.Remove(typeof(ItemDrop)),"missing ItemDrop");
   Refuse((p,g,d)=>g.Components.Remove(typeof(ZNetView)),"missing network view");
   Refuse((p,g,d)=>((ZNetView)g.Components[typeof(ZNetView)]).m_persistent=false,"nonpersistent item");
   Refuse((p,g,d)=>g.Components.Remove(typeof(Rigidbody)),"missing body");
   Refuse((p,g,d)=>g.Components[typeof(Piece)]=new Piece(),"piece refund");
   Refuse((p,g,d)=>g.Components[typeof(WearNTear)]=new WearNTear(),"wear refund");
   Refuse((p,g,d)=>d.m_onDrop=()=>{},"drop callback");
   Refuse((p,g,d)=>p.Receipts[p.Plan.Parts[1].Id].Paid[0].Paid=0,"zero debit");
   Refuse((p,g,d)=>d.m_itemData.m_shared.m_maxStackSize=1,"stack overflow");
   for(int mode=1;mode<=3;mode++){int selected=mode;Refuse((p,g,d)=>d.m_itemData.CloneMode=selected,"shared/null clone",true);}
   Refuse((p,g,d)=>ItemDrop.ItemData.LoadHash=2,"wrong native prefab hash");
   Refuse((p,g,d)=>ItemDrop.ItemData.LoadQuality=2,"wrong native quality");
   Refuse((p,g,d)=>ItemDrop.ItemData.LoadVariant=1,"wrong native variant");
   Refuse((p,g,d)=>ItemDrop.ItemData.LoadCustody=true,"existing custody marker");
   Refuse((p,g,d)=>{ItemDrop.ItemData.LoadVariant=4;foreach(var row in p.Receipts.Values)row.Paid[0].Variant=4;},"native Start variant normalization");
   Refuse((p,g,d)=>d.m_itemData.DamageValue=10001,"native Start damage normalization");
   Refuse((p,g,d)=>Journal.RejectProperties=true,"shared exact property validator");
   f=Fresh();f.plans.Receipts[f.plans.Plan.Parts[1].Id].Paid[0].Prefab="MissingLast";
   try {RequireRefundable(f.plans,Guid.NewGuid(),7);throw new Exception("Last unsupported row admitted");}catch(InvalidOperationException){}
   Check(Journal.Validated==1,"Last-row refusal did not validate the preceding row first");
   f=Fresh();f.plans.Receipts[f.plans.Plan.Parts[1].Id].Paid[0].Prefab="MissingLast";
   try {StartRemoval(f.plans,Guid.NewGuid());}catch(InvalidOperationException){}
   Check(RemovalJournal.Prepares==0&&RemovalJournal.Intents==0&&RemovalJournal.Deletes==0,"Last unsupported row entered cancellation/deletion");
   f=Fresh();StartRemoval(f.plans,Guid.NewGuid());
   Check(RemovalJournal.Prepares==1&&RemovalJournal.Intents==1&&RemovalJournal.Deletes==2&&Status=="queued-removal","Valid refundability blocked the actual removal sequence");
   f=Fresh();Journal.RejectCapacity=true;try {StartRemoval(f.plans,Guid.NewGuid());}catch(InvalidDataException){}
   Check(RemovalJournal.Prepares==0&&RemovalJournal.Intents==0&&RemovalJournal.Deletes==0,"Known over-budget issuance entered cancellation/deletion");
   Check(Journal.Validated==2&&Journal.CapacityChecks==1,"Budget refused before validating the exact returned entries");
   Refuse((p,g,d)=>p.Receipts[p.Plan.Parts[0].Id].Paid=Enumerable.Repeat(new BlueprintConstructionPartJournal.PaidRow(),65537).ToArray(),"existing row budget",true);
   return checks;
  }
 }
}
