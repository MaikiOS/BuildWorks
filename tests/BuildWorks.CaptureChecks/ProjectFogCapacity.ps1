$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/BlueprintConstructionProjectFog.cs'))
function Read-Method([string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Method missing: $signature" }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }; if ($source[$end] -eq '}') { $depth-- }; $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced method.' }
    $source.Substring($start, $end - $start)
}
$methods = (Read-Method 'internal void Add(') + (Read-Method 'internal void Refresh(') +
    (Read-Method 'private void FailEntry(') + (Read-Method 'private void Fail(')
if ($source.Contains('private int ReservedParts()')) {
    $methods += [regex]::Match($source, 'private int ReservedParts\(\) =>[^;]+;').Value
}
$methods += if ($source.Contains('private void RetryCapacityAfterBudgetDecrease()')) {
    Read-Method 'private void RetryCapacityAfterBudgetDecrease()'
} else { 'private void RetryCapacityAfterBudgetDecrease() {}' }
if ($source.Contains('private void RetryCapacityAfterBudgetDecrease()') -and
    $source -notmatch 'channels\.PumpSources\(\);\s*RetryCapacityAfterBudgetDecrease\(\);\s*bool preparedOne') {
    throw 'Capacity retry must run after confirmed pumping and before the entry enumeration.'
}
$fixture = @'
using System;
using System.Collections.Generic;
using System.Linq;
public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} }
public struct Vector2 {}
public struct Quaternion { public Quaternion(float x,float y,float z,float w){} public static Quaternion identity => new Quaternion(); }
public class ZNetScene { public static ZNetScene instance=new ZNetScene(); public object GetPrefab(string name)=>name; }
public class Pose { public float[] Values=new float[10]; }
public class Part { public Guid Id=Guid.NewGuid(); public int SourceIndex; public Pose WorldPose=new Pose(); }
public class Blueprint { public List<string> parts=new List<string>(); }
public class Record {
 public Guid Id=Guid.NewGuid(); public List<Part> Parts=new List<Part>(); public Pose RootPose=new Pose();
 public Blueprint Blueprint => new Blueprint { parts=Parts.Select(p=>p.SourceIndex.ToString()).ToList() };
}
public static class Journal { public const int Committed=7, NoEffect=3; }
public static class BlueprintFogAtlas { public const int MaximumOwners=32; }
public class FakeChannels { public int RetiringSources; }
public class FakeRuntime {
 public Dictionary<Guid,int> Phases=new Dictionary<Guid,int>(); public int Reads;
 public int? ReadPartPhase(Guid project,Guid part){Reads++;return Phases.TryGetValue(part,out int p)?p:(int?)null;}
}
public class BlueprintFogBuilding {
 public static int Creates; public static bool ThrowNext; public int PlannedParts; public int[] Indices;
 public BlueprintFogBuilding(object assets,FakeChannels channels,Blueprint bp,Func<string,object> resolve,
  Vector3 root,Quaternion q,Vector3 actor,Vector2 size,bool deferPreparation,
  (Vector3,Quaternion,Vector3)[] worldPoses,bool sharedDisplay){
  if(ThrowNext){ThrowNext=false;throw new InvalidOperationException("GPU failure");}
  Creates++;PlannedParts=worldPoses.Length;Indices=worldPoses.Select(p=>(int)p.Item1.x).ToArray();
 }
 public IEnumerable<object> Prepare()=>new object[0]; public void Dispose(){} public void SetDisplayActive(bool active){}
 public void RemovePart(int index){PlannedParts--;} public void SetPartVisible(int index,bool visible){}
}
public class FogCapacityFixture {
 public class Entry {
  public Record Record; public Part[] Parts; public BlueprintFogBuilding Building;
  public IEnumerator<object> Preparation; public string Error; public bool Admitted,WaitingForCapacity; public float Clock;
 }
 public Dictionary<Guid,Entry> entries=new Dictionary<Guid,Entry>();
 public FakeRuntime runtime=new FakeRuntime(); public FakeChannels channels=new FakeChannels();
 public object assets=new object(),atlas=new object(); public Action<Exception> log=null;
 public bool closing=false,layoutDirty=false; public string failure=null; public int lastReserved=0;
 private void EnsureResources(){} private static void Body(out Vector3 actor,out Vector2 size){actor=new Vector3();size=new Vector2();}
 __METHODS__
 static Record Plan(int count){var r=new Record();for(int i=0;i<count;i++){var p=new Part{SourceIndex=i};p.WorldPose.Values[0]=i;r.Parts.Add(p);}return r;}
 static void Require(bool ok,string why){if(!ok)throw new InvalidOperationException(why);}
 public static string Run(){
  var f=new FogCapacityFixture();var old=Plan(28);f.Add(old);var next=Plan(5);f.Add(next);
  Require(f.entries[next.Id].Building==null,"Expected capacity denial");
  int reads=f.runtime.Reads;f.RetryCapacityAfterBudgetDecrease();Require(f.runtime.Reads==reads,"Unchanged budget reread journals");
  f.runtime.Phases[next.Parts[2].Id]=Journal.Committed;f.Refresh(next.Id);
  Require(f.entries[next.Id].Building!=null,"Capacity-denied project did not recover after phase commit");
  Require(f.entries[next.Id].Building.Indices.SequenceEqual(new[]{0,1,3,4}),"Original indices changed");
  Require(f.entries[old.Id].Building.PlannedParts==28,"Existing project changed");
  int creates=BlueprintFogBuilding.Creates;f.Refresh(next.Id);f.RetryCapacityAfterBudgetDecrease();
  Require(BlueprintFogBuilding.Creates==creates,"Duplicate admission");
  var drain=new FogCapacityFixture();drain.Add(Plan(28));drain.channels.RetiringSources=1;var four=Plan(4);drain.Add(four);
  drain.Refresh(four.Id);Require(drain.entries[four.Id].Building==null,"Ignored unconfirmed retiring owner");
  drain.channels.RetiringSources=0;drain.RetryCapacityAfterBudgetDecrease();
  Require(drain.entries[four.Id].Building?.PlannedParts==4,"Confirmed drain did not recover capacity");
  var interrupted=new FogCapacityFixture();interrupted.Add(Plan(28));interrupted.channels.RetiringSources=1;
  var waiting=Plan(4);interrupted.Add(waiting);interrupted.channels.RetiringSources=0;
  interrupted.Add(Plan(33));interrupted.RetryCapacityAfterBudgetDecrease();
  Require(interrupted.entries[waiting.Id].Building?.PlannedParts==4,"Another denied Add consumed the budget-decrease wakeup");
  var generic=new FogCapacityFixture();BlueprintFogBuilding.ThrowNext=true;var broken=Plan(1);generic.Add(broken);
  creates=BlueprintFogBuilding.Creates;generic.Refresh(broken.Id);generic.RetryCapacityAfterBudgetDecrease();
  Require(generic.entries[broken.Id].Building==null && BlueprintFogBuilding.Creates==creates,"Retried generic GPU failure");
  var global=new FogCapacityFixture();global.Add(Plan(28));var denied=Plan(5);global.Add(denied);
  global.runtime.Phases[denied.Parts[2].Id]=Journal.Committed;global.failure="global GPU failure";global.Refresh(denied.Id);
  Require(global.entries[denied.Id].Building==null,"Admitted during global GPU failure");
  global.failure=null;global.closing=true;global.Refresh(denied.Id);global.RetryCapacityAfterBudgetDecrease();
  Require(global.entries[denied.Id].Building==null,"Admitted while closing");
  return "PASS actual Add/Refresh capacity:28+5 denied; committed2 -> original[0,1,3,4]; retiring drain; no duplicate/generic/global retry. GPU/native substituted.";
 }
}
'@
$fixture = $fixture.Replace('__METHODS__', $methods).Replace('Store.Record', 'Record')
Add-Type -TypeDefinition $fixture
[FogCapacityFixture]::Run()
