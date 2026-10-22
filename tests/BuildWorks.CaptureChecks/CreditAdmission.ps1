[CmdletBinding()]
param([string]$ReportPath = '', [switch]$UnsettledNegativeControl)
$ErrorActionPreference = 'Stop'
$taskSource = Get-Content (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintConstructionPayoutCollection.cs') -Raw
$taskStart = $taskSource.IndexOf('            if (loaded || credit != null ||', [StringComparison]::Ordinal)
$taskEnd = $taskSource.IndexOf('            // Native pickup', $taskStart, [StringComparison]::Ordinal)
if ($taskStart -lt 0 -or $taskEnd -lt $taskStart) { throw 'Actual fresh-credit admission block missing.' }
$taskGuard = $taskSource.Substring($taskStart, $taskEnd - $taskStart)
if ($taskEnd -gt $taskSource.IndexOf('TryCaptureAbsence', $taskStart, [StringComparison]::Ordinal)) { throw 'Admission follows source scan.' }
if ($UnsettledNegativeControl) {
    $taskGuard = [regex]::Replace($taskGuard, 'record.Collections\?\.Any\([^\r\n]+\) == true \|\|', '')
}
$taskCode = @'
using System;
using System.Linq;
using System.Collections.Generic;
public static class CreditAdmissionChecks {
 class Journal { public const int ProfileCredited=2,CleanupIntent=1; }
 class Cleanup { public int Phase; }
 class Credit { public int Phase=2; public Cleanup Removal; }
 class Record { public Credit[] Collections; }
 class Player { public Dictionary<string,string> m_customData=new Dictionary<string,string>(); }
 static class BlueprintStorageCapture {
  public static int Snapshots;
  public static bool TrySnapshotInventory(object inventory,out object before) { Snapshots++; before=new object();return true; }
 }
 static bool Admit(Credit[] rows,bool loaded=false,Credit credit=null) {
  var record=new Record{Collections=rows};var player=new Player();object inventory=null,before;
__GUARD__
  return true;
 }
 public static int Run() {
  int count=0;
  foreach(var rows in new[]{new Credit[]{null,new Credit{Phase=1}},new Credit[]{new Credit{Phase=1},null},new Credit[]{null,new Credit{Removal=new Cleanup{Phase=1}}}}) {
   BlueprintStorageCapture.Snapshots=0;
   try { Admit(rows);throw new Exception("Other unsettled row admitted before baseline save"); }
   catch(InvalidOperationException) { if(BlueprintStorageCapture.Snapshots!=0)throw new Exception("Rejected admission reached snapshot");count++; }
  }
  if(!Admit(new Credit[]{null,new Credit{Removal=new Cleanup{Phase=2}}}))throw new Exception("Settled sparse row blocked next pickup");count++;
  if(!Admit(null))throw new Exception("Legacy issuance blocked");count++;
  try { Admit(null,true);throw new Exception("Loaded-only receipt admitted"); }catch(InvalidOperationException){count++;}
  try { Admit(null,false,new Credit());throw new Exception("Existing slot admitted"); }catch(InvalidOperationException){count++;}
  return count;
 }
}
'@
Add-Type -TypeDefinition $taskCode.Replace('__GUARD__', $taskGuard)
$taskObserved = ''
try { $taskChecks = [CreditAdmissionChecks]::Run() } catch { $taskObserved = $_.Exception.ToString() }
if ($UnsettledNegativeControl) {
    if (!$taskObserved.Contains('Other unsettled row admitted before baseline save')) { throw ('Negative control failed: '+$taskObserved) }
    'PASS fresh-credit admission negative control: removed global guard detected.'
    return
}
if ($taskObserved) { throw $taskObserved }
if ($taskChecks -ne 7) { throw ('Admission coverage differs: '+$taskChecks) }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualAdmissionCompiled=$true;nativeBoundariesSubstituted=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
'PASS fresh-credit admission: 7 actual-guard assertions, no native profile effects.'
