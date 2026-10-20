[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskSource = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPayoutCleanup.cs') -Raw
function Extract-Block([string]$signature) {
    $start = $taskSource.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw ('Source member missing: '+$signature) }
    $brace = $taskSource.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $taskSource.Length) {
        if ($taskSource[$end] -eq '{') { $depth++ }
        if ($taskSource[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced source member' }
    $taskSource.Substring($start, $end-$start)
}
$taskWitness = Extract-Block 'internal sealed class Witness'
$taskAllow = Extract-Block 'internal static bool AllowNetwork('
$taskConfirm = Extract-Block 'internal static void ConfirmNetwork('
$taskCode = @'
using System;
using System.Collections.Generic;
using System.Reflection;
public struct ZDOID {
 public int Value;
 public static bool operator ==(ZDOID a,ZDOID b)=>a.Value==b.Value;
 public static bool operator !=(ZDOID a,ZDOID b)=>a.Value!=b.Value;
 public override bool Equals(object other)=>other is ZDOID id && this==id;
 public override int GetHashCode()=>Value;
}
public sealed class ZDO { public ZDOID m_uid; public bool Held; }
public sealed class ZDOMan {
 public static ZDOMan instance;
 private List<ZDOID> m_destroySendList = new List<ZDOID>();
 public List<ZDOID> Queue=>m_destroySendList;
 public void LoseQueue()=>m_destroySendList=null;
}
public static class BlueprintConstructionPayoutSeal { public static bool IsHeld(ZDO data)=>data!=null && data.Held; }
public static class CleanupGuardChecks {
 private static Witness active;
 private static readonly FieldInfo DestroyQueue=typeof(ZDOMan).GetField("m_destroySendList",BindingFlags.Instance|BindingFlags.NonPublic);
 __WITNESS__
 __ALLOW__
 __CONFIRM__
 private static int count;
 private static void Check(bool yes,string message) { count++;if(!yes)throw new Exception(message); }
 public static int Run() {
  ZDO target=null; ZDOMan manager=null; bool valid=false;
  void Reset() { manager=new ZDOMan();ZDOMan.instance=manager; target=new ZDO{Held=true,m_uid=new ZDOID{Value=42}};valid=true;
   active=new Witness{Data=target,Id=target.m_uid,Manager=manager,BeforeCount=0,Custody=()=>valid}; }
  Reset(); active=null;
  Check(!AllowNetwork(target,out var state)&&state==null,"Held source escaped without scope");
  Check(AllowNetwork(new ZDO(),out state)&&state==null,"Ordinary native destruction changed");
  Reset();Check(AllowNetwork(target,out state)&&ReferenceEquals(state,active)&&state.Attempted,"Exact synchronous scope not admitted");
  manager.Queue.Add(target.m_uid);ConfirmNetwork(state,null);Check(state.Confirmed,"Exact appended destruction not confirmed");
  Check(!AllowNetwork(target,out var again)&&again==null,"Same scope admitted twice");
  active=null;ConfirmNetwork(state,null);Check(!state.Confirmed,"Disposed scope accepted a late finalizer");
  Reset(); Check(!AllowNetwork(new ZDO{Held=true,m_uid=target.m_uid},out state)&&state==null&&!active.Attempted,"Same UID on another object escaped");
  Check(AllowNetwork(new ZDO(),out state)&&!active.Attempted,"Unrelated ordinary call consumed cleanup scope");
  valid=false;Check(!AllowNetwork(target,out state)&&state==null&&!active.Attempted,"Changed custody admitted deletion");
  Reset();target.m_uid=new ZDOID{Value=43};Check(!AllowNetwork(target,out state),"Changed target UID admitted");
  Reset();ZDOMan.instance=new ZDOMan();Check(!AllowNetwork(target,out state),"Changed manager admitted");
  Reset();manager.Queue.Add(target.m_uid);active.BeforeCount=1;Check(!AllowNetwork(target,out state),"Already queued UID admitted again");
  Reset();manager.Queue.Add(new ZDOID{Value=7});Check(!AllowNetwork(target,out state),"Changed queue count admitted");
  Reset();manager.LoseQueue();Check(!AllowNetwork(target,out state),"Unavailable native queue admitted");
  Reset();Check(AllowNetwork(target,out state),"Missing-append case not admitted");ConfirmNetwork(state,null);Check(!state.Confirmed,"No native append falsely confirmed");
  Reset();AllowNetwork(target,out state);manager.Queue.Add(new ZDOID{Value=7});ConfirmNetwork(state,null);Check(!state.Confirmed,"Wrong appended UID confirmed");
  Reset();AllowNetwork(target,out state);manager.Queue.Add(new ZDOID{Value=7});manager.Queue.Add(target.m_uid);ConfirmNetwork(state,null);Check(!state.Confirmed,"Extra queue effect confirmed");
  Reset();AllowNetwork(target,out state);manager.Queue.Add(target.m_uid);ConfirmNetwork(state,new Exception("native callback fault"));Check(!state.Confirmed,"Native fault swallowed");
  Reset();AllowNetwork(target,out state);manager.Queue.Add(target.m_uid);ZDOMan.instance=new ZDOMan();ConfirmNetwork(state,null);Check(!state.Confirmed,"Changed finalizer manager confirmed");
  Reset();state=active;manager.Queue.Add(target.m_uid);ConfirmNetwork(state,null);Check(!state.Confirmed,"Unattempted witness confirmed");
  ConfirmNetwork(null,null);Check(true,"Null finalizer state failed");active=null;return count;
 }
}
'@
$taskCode = $taskCode.Replace('__WITNESS__',$taskWitness).Replace('__ALLOW__',$taskAllow).Replace('__CONFIRM__',$taskConfirm)
Add-Type -TypeDefinition $taskCode
$taskAssertions = [CleanupGuardChecks]::Run()
$taskPlugin = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BuildWorksPlugin.cs') -Raw
if ($taskPlugin -notmatch 'BlueprintConstructionPayoutCleanup.AllowNetwork\(__0, out __state\)' -or
    $taskPlugin -notmatch 'BlueprintConstructionPayoutCleanup.ConfirmNetwork\(__state, __exception\); return __exception;') {
    throw 'Actual Harmony prefix/finalizer wiring or exception preservation differs'
}
"PASS cleanup native guard: $taskAssertions assertions from actual source methods; native host execution is not claimed."
$taskTick = Extract-Block 'internal void Tick()'
$taskHost = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PayoutCleanupTickHost.cs') -Raw
Add-Type -TypeDefinition ($taskHost.Replace('__ACTUAL_TICK__',$taskTick))
$taskTickAssertions = [CleanupTickChecks.Runner]::Run()
"PASS cleanup state machine: $taskTickAssertions assertions from actual Tick; inert host/save/readback boundaries, no native execution."
