param([string]$SourcePath = (Join-Path $PSScriptRoot 'ProjectInventoryProbe.cs'))
$ErrorActionPreference = 'Stop'
$source = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath))
$start = $source.IndexOf('        private static bool SameLive(')
$end = $source.IndexOf('        private static bool SameMergeCandidate(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Actual SameLive source boundary missing.' }
$method = $source.Substring($start, $end - $start).Replace('private static bool', 'public static bool')
$candidateEnd = $source.IndexOf('        public static string Arm(', $end)
if ($candidateEnd -le $end) { throw 'Actual merge-candidate source boundary missing.' }
$method += $source.Substring($end, $candidateEnd - $end).Replace('private static bool', 'public static bool')
$code = @'
using System; using System.Collections.Generic; using System.Linq;
public class SharedFixture { public string Json=new string('a',100); }
public class ItemDrop {
 public ItemData m_itemData;
 public class ItemData {
  public object m_dropPrefab,m_shared; public float m_durability; public int m_quality,m_variant,m_worldLevel,m_stack,x,y;
  public bool m_pickedUp,m_equipped,m_cheated;public long m_crafterID;public string m_crafterName="owner";
  public string Signature="same";
  public Dictionary<string,string> m_customData=new Dictionary<string,string>{{"own","value"}};
  public ItemData Copy(){var result=(ItemData)MemberwiseClone();result.m_customData=new Dictionary<string,string>(m_customData);return result;}
 }
}
public class PrefabFixture {
 public ItemDrop Drop=new ItemDrop{m_itemData=new ItemDrop.ItemData{m_shared=new SharedFixture()}};
 public T GetComponent<T>() where T:class=>Drop as T;
 public static implicit operator bool(PrefabFixture value)=>value!=null;
}
public class ObjectDB {
 public static ObjectDB instance=new ObjectDB();public PrefabFixture Wood=new PrefabFixture();
 public PrefabFixture GetItemPrefab(string name)=>Wood;
}
namespace UnityEngine { public static class JsonUtility { public static string ToJson(object value)=>((SharedFixture)value).Json; } }
public static class QaInventoryEqualityChecks {
__METHOD__
 static ItemDrop.ItemData Copy(ItemDrop.ItemData item)=>item.Copy();
 static string Normalized(ItemDrop.ItemData item)=>item.Signature;
 static int CandidateChecks() {
  int checks=0;Action<bool,string> check=(ok,message)=>{checks++;if(!ok)throw new Exception(message);};
  var a=new ItemDrop.ItemData{m_dropPrefab=new object(),m_shared=new SharedFixture(),m_durability=100};
  var b=a.Copy();b.m_shared=new SharedFixture();var aShared=a.m_shared;var bShared=b.m_shared;
  check(!SameLive(a,b),"Live identity must still distinguish distinct shared refs");
  check(SameMergeCandidate(a,b),"Canonical equal shared values refused");
  check(Object.ReferenceEquals(a.m_shared,aShared)&&Object.ReferenceEquals(b.m_shared,bShared),"Candidate comparison rewrote live shared");
  ((SharedFixture)a.m_shared).Json="changed";check(!SameMergeCandidate(a,b),"Changed source shared accepted");((SharedFixture)a.m_shared).Json=new string('a',100);
  ((SharedFixture)b.m_shared).Json="changed";check(!SameMergeCandidate(a,b),"Changed target shared accepted");((SharedFixture)b.m_shared).Json=new string('a',100);
  ((SharedFixture)ObjectDB.instance.Wood.Drop.m_itemData.m_shared).Json="{}";check(!SameMergeCandidate(a,b),"Opaque canonical shared accepted");
  ((SharedFixture)ObjectDB.instance.Wood.Drop.m_itemData.m_shared).Json=new string('a',100);
  b.m_durability-=0.0001f;check(!SameMergeCandidate(a,b),"Extra live wear accepted");b.m_durability=a.m_durability;
  b.Signature="different";check(!SameMergeCandidate(a,b),"Different native package accepted");b.Signature=a.Signature;
  b.m_dropPrefab=new object();check(!SameMergeCandidate(a,b),"Different prefab identity accepted");b.m_dropPrefab=a.m_dropPrefab;
  ObjectDB.instance.Wood=null;check(!SameMergeCandidate(a,b),"Missing canonical prefab accepted");return checks;
 }
 public static int Run() {
  var a=new ItemDrop.ItemData{m_dropPrefab=new object(),m_shared=new object(),m_durability=94.1199951f};int checks=0;
  if(!SameLive(a,a.Copy()))throw new Exception("Exact clone refused");checks++;
  foreach(string fault in new[]{"prefab","shared","durability","quality","variant","world","picked","equipped","cheated","crafter","name","value","key"}){
   var b=a.Copy();switch(fault){
    case "prefab":b.m_dropPrefab=new object();break;case "shared":b.m_shared=new object();break;
    case "durability":b.m_durability-=0.0001f;break;case "quality":b.m_quality++;break;
    case "variant":b.m_variant++;break;case "world":b.m_worldLevel++;break;
    case "picked":b.m_pickedUp=!b.m_pickedUp;break;case "equipped":b.m_equipped=!b.m_equipped;break;
    case "cheated":b.m_cheated=!b.m_cheated;break;case "crafter":b.m_crafterID++;break;
    case "name":b.m_crafterName="different";break;case "value":b.m_customData["own"]="different";break;
    case "key":b.m_customData.Add("different","value");break;
   }if(SameLive(a,b))throw new Exception("Changed live property accepted: "+fault);checks++;
  }
  var moved=a.Copy();moved.m_stack=4;moved.x=2;moved.y=1;
  if(!SameLive(a,moved))throw new Exception("Quantity/grid must be checked separately by exact postcondition");return checks+1+CandidateChecks();
 }
}
'@
Add-Type -TypeDefinition $code.Replace('__METHOD__', $method)
$checks = [QaInventoryEqualityChecks]::Run()
if ($checks -ne 25) { throw "Unexpected live property coverage: $checks" }
if (!$source.Contains('expected.Keys.Any(item => !inventory.ContainsItem(item))')) { throw 'Exact remaining-reference coverage missing.' }
if (!$source.Contains('UnityEngine.JsonUtility.ToJson(item.m_shared) != expectedShared[item]')) { throw 'Captured shared values postcondition missing.' }
"PASS $checks actual live-property/candidate checks and remaining-reference/shared-value wiring; canonical JSON/package/native move boundaries substituted, no game invoked."
