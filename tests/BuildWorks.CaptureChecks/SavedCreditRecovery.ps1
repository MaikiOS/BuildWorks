[CmdletBinding()]
param([string]$ReportPath = '', [switch]$InventoryNegativeControl)
$ErrorActionPreference = 'Stop'
$taskSource = Get-Content (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintConstructionPayoutCollection.cs') -Raw
$taskStart = $taskSource.IndexOf('            bool saved = disk.CustomData.TryGetValue', [StringComparison]::Ordinal)
$taskEnd = $taskSource.IndexOf('            if (loaded || credit != null ||', $taskStart, [StringComparison]::Ordinal)
if ($taskStart -lt 0 -or $taskEnd -lt $taskStart) { throw 'Actual saved-credit recovery block missing.' }
$taskBlock = $taskSource.Substring($taskStart, $taskEnd - $taskStart)
if ($InventoryNegativeControl) { $taskBlock = $taskBlock.Replace('SavedCustody() && Matches(credited) &&', 'SavedCustody() &&') }
$taskCode = @'
using System;
using System.Collections.Generic;
public static class SavedCreditRecoveryChecks {
 class BlueprintForeignPartData {
  public const string NativeContainerFormat="native";
  public string Info,Zdo;
  public BlueprintForeignPartData(string format,string info,string zdo,object unused) { Info=info;Zdo=zdo; }
 }
 class Credit { public int Phase=1;public string AfterInfo="after-info",AfterBytes="after-bytes"; }
 class Record { public Guid Project=Guid.NewGuid(),Operation=Guid.NewGuid();public Credit[] Collections=new[]{new Credit()};public Item[] Items=new[]{new Item(),new Item()}; }
 class Player { public Dictionary<string,string> m_customData=new Dictionary<string,string>(); }
 class Disk { public Dictionary<string,string> CustomData=new Dictionary<string,string>(); }
 class Journal {
  public const int ProfileCredited=2;
  public int Existing,Recovered;
  public void ConfirmCredit(Guid project,Guid operation,int index,Disk proof) { Existing++; }
  public void ConfirmSavedCredit(Guid project,Guid operation,int index,object proof) {
   if(!Object.ReferenceEquals(proof,BlueprintStoragePlayerProfile.Proof))throw new Exception("Wrong full saved proof forwarded");Recovered++;
  }
 }
 static class BlueprintStoragePlayerProfile {
  public static int Captures,Verifications; public static bool Capture=true,Verify=true;
  public static Action DuringCapture;public static object Proof=new object();
  public static bool TryCapture(Player player,Guid entry,Func<bool> guard,out Snapshot recovery,out string error) {
   Captures++;DuringCapture?.Invoke();recovery=new Snapshot();error="capture refused";return Capture&&guard();
  }
  public sealed class Snapshot {
   public bool TryVerifySavedCredit(out object proof,out string error) { Verifications++;proof=Proof;error="saved proof refused";return Verify; }
  }
 }
 class Item { public Guid Id=Guid.NewGuid(); }
 class Fixture {
  public Record record=new Record();public Journal journal=new Journal();public Disk disk=new Disk();public Player player=new Player();
  public string key="receipt",value="canonical",Status="initial";public int index=1;public bool Custody=true;Guid project;
  public string ActualInfo="after-info",ActualBytes="after-bytes";
  public Fixture() {
   project=record.Project;record.Collections=new Credit[]{null,new Credit()};
   disk.CustomData[key]=value;player.m_customData[key]=value;
  }
  bool SavedCustody()=>Custody;
  bool Matches(BlueprintForeignPartData expected)=>expected.Info==ActualInfo&&expected.Zdo==ActualBytes;
  public void Recover() {
   string error;
   // The actual branch also reads record.Items[index].Id; fixtures only replace host boundaries.
__BLOCK__
   throw new InvalidOperationException("No saved receipt/recovery branch");
  }
 }
 static int checks;
 static Fixture Fresh() {
  BlueprintStoragePlayerProfile.Captures=BlueprintStoragePlayerProfile.Verifications=0;
  BlueprintStoragePlayerProfile.Capture=BlueprintStoragePlayerProfile.Verify=true;BlueprintStoragePlayerProfile.DuringCapture=null;
  return new Fixture();
 }
 static void Check(bool valid,string message) { checks++;if(!valid)throw new Exception(message); }
 static void Refuse(Action<Fixture> change,string name) {
  var f=Fresh();change(f);try { f.Recover();throw new Exception("Unsafe recovery accepted: "+name); }catch(InvalidOperationException){}
  Check(f.Status=="initial"&&f.journal.Recovered==0&&f.journal.Existing==0,"Refusal changed credit: "+name);
 }
 public static int Run() {
  checks=0;var f=Fresh();f.Recover();
  Check(f.Status=="profile-credited"&&f.journal.Recovered==1&&f.journal.Existing==0,"Sparse saved intent did not recover exactly once");
  Check(BlueprintStoragePlayerProfile.Captures==1&&BlueprintStoragePlayerProfile.Verifications==1,"Recovery skipped full saved-inventory proof");
  f=Fresh();f.record.Collections[1].Phase=Journal.ProfileCredited;f.Recover();
  Check(f.Status=="profile-credited"&&f.journal.Existing==1&&f.journal.Recovered==0&&BlueprintStoragePlayerProfile.Captures==0,"Confirmed replay took insertion/recovery path");
  Refuse(x=>x.record.Collections=null,"no credit record");
  Refuse(x=>x.record.Collections=new Credit[1],"slot outside sparse prefix");
  Refuse(x=>x.record.Collections[1]=null,"empty indexed slot");
  Refuse(x=>{x.disk.CustomData.Clear();x.record.Collections[1].Phase=Journal.ProfileCredited;},"credited without disk receipt");
  Refuse(x=>x.disk.CustomData[x.key]="wrong","wrong disk receipt");
  Refuse(x=>x.player.m_customData.Clear(),"no loaded receipt");
  Refuse(x=>x.player.m_customData[x.key]="wrong","wrong loaded receipt");
  Refuse(x=>x.Custody=false,"custody changed");
  Refuse(x=>x.ActualInfo="different","exact inventory summary");
  Refuse(x=>x.ActualBytes="different","exact inventory bytes");
  Refuse(x=>BlueprintStoragePlayerProfile.Capture=false,"capture refused");
  Refuse(x=>BlueprintStoragePlayerProfile.Verify=false,"full saved proof refused");
  Refuse(x=>BlueprintStoragePlayerProfile.DuringCapture=()=>x.player.m_customData.Clear(),"receipt changed during capture");
  Refuse(x=>BlueprintStoragePlayerProfile.DuringCapture=()=>x.Custody=false,"custody changed during capture");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $taskCode.Replace('__BLOCK__', $taskBlock)
$taskObserved = ''
try { $taskChecks = [SavedCreditRecoveryChecks]::Run() } catch { $taskObserved = $_.Exception.ToString() }
if ($InventoryNegativeControl) {
    if (!$taskObserved.Contains('Unsafe recovery accepted: exact inventory summary')) { throw ('Negative control failed: ' + $taskObserved) }
    'PASS saved-credit recovery negative control: missing exact inventory comparison detected.'
    return
}
if ($taskObserved) { throw $taskObserved }
if ($taskChecks -ne 17) { throw ('Recovery coverage differs: ' + $taskChecks) }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualRecoveryBranchCompiled=$true;nativeBoundariesSubstituted=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
'PASS saved-credit recovery: 17 actual-branch checks, full saved proof required; native boundaries substituted.'
