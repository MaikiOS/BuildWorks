[CmdletBinding()]
param([string]$ReportPath = '', [switch]$InventoryNegativeControl)
$ErrorActionPreference = 'Stop'
if ($InventoryNegativeControl) {
    $root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
    $output = Join-Path $root 'artifacts/saved-credit-recovery-negative'
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $profile = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintStoragePlayerProfile.cs'))
    $bad = [regex]::Replace($profile, '(?s)bool InventoryMatches\(\) => .*?;', 'bool InventoryMatches() => true;')
    if ($bad -eq $profile) { throw 'Actual inventory predicate mutation missing.' }
    $badPath = Join-Path $output 'MissingInventoryFence.cs'
    [IO.File]::WriteAllText($badPath, $bad)
    $observed = & pwsh.exe -NoLogo -NoProfile -File (Join-Path $root 'tests/BuildWorks.ContentReservationChecks/Run.ps1') -ProfileSourcePath $badPath 2>&1 | Out-String
    $observed | Set-Content (Join-Path $output 'negative.log')
    if (!$observed.Contains('9412 fixture accepted extra wear')) { throw ('Negative control failed: ' + $observed) }
    'PASS saved-credit recovery negative control: actual reader without inventory fence accepts altered durability and is detected.'
    return
}
$taskSource = Get-Content (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintConstructionPayoutCollection.cs') -Raw
$taskStart = $taskSource.IndexOf('            bool saved = disk.CustomData.TryGetValue', [StringComparison]::Ordinal)
$taskEnd = $taskSource.IndexOf('            if (loaded || credit != null ||', $taskStart, [StringComparison]::Ordinal)
if ($taskStart -lt 0 -or $taskEnd -lt $taskStart) { throw 'Actual saved-credit recovery block missing.' }
$taskBlock = $taskSource.Substring($taskStart, $taskEnd - $taskStart)
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
  public static Action DuringCapture;public static object Proof=new object();public static Func<BlueprintForeignPartData,bool> Matches;
  public static bool TryReadSavedCredit(Player player,BlueprintForeignPartData expected,Func<bool> guard,out object proof,out string error) {
   Captures++;DuringCapture?.Invoke();proof=Proof;error="saved proof refused";
   if(!Capture||!guard()||!Matches(expected))return false;
   Verifications++;return Verify;
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
  public bool Matches(BlueprintForeignPartData expected)=>expected.Info==ActualInfo&&expected.Zdo==ActualBytes;
  public void Recover() {
   string error;
   // Actual constructor branch; profile reader and journal are host-boundary substitutes.
__BLOCK__
   throw new InvalidOperationException("No saved receipt/recovery branch");
  }
 }
 static int checks;
 static Fixture Fresh() {
  BlueprintStoragePlayerProfile.Captures=BlueprintStoragePlayerProfile.Verifications=0;
  BlueprintStoragePlayerProfile.Capture=BlueprintStoragePlayerProfile.Verify=true;BlueprintStoragePlayerProfile.DuringCapture=null;
  var fixture=new Fixture();BlueprintStoragePlayerProfile.Matches=fixture.Matches;return fixture;
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
if ($taskObserved) { throw $taskObserved }
if ($taskChecks -ne 17) { throw ('Recovery coverage differs: ' + $taskChecks) }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualRecoveryBranchCompiled=$true;nativeBoundariesSubstituted=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
'PASS saved-credit recovery: 17 actual-branch checks, full saved proof required; native boundaries substituted.'
