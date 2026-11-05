$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/PrecisionPlacementSession.Support.Qa.cs'))
$start = $source.IndexOf('internal void QaReleaseSupportProbe()', [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Actual release method missing.' }
$brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
while ($depth -gt 0 -and $end -lt $source.Length) {
    if ($source[$end] -eq '{') { $depth++ }; if ($source[$end] -eq '}') { $depth-- }; $end++
}
if ($depth -ne 0) { throw 'Actual release method is unbalanced.' }
$method = $source.Substring($start, $end - $start)
$hostCode = @'
using System;
public sealed class QaReleaseFixture {
 public bool qaUnpaidSupportFence, selectingPlacementSupport, ThrowOnCancel;
 public int suppressPlacementFrame=-1, Cancels, States;
 static class Time { internal const int frameCount=17; }
 void Cancel() { Cancels++; if(ThrowOnCancel) throw new InvalidOperationException(); selectingPlacementSupport=false; }
 string QaSupportProbeState() { States++; return "state"; }
 __ACTUAL_RELEASE__
 public void Run() { QaReleaseSupportProbe(); }
}
'@
Add-Type -TypeDefinition ($hostCode.Replace('__ACTUAL_RELEASE__', $method))
$foreign = [QaReleaseFixture]::new(); $foreign.selectingPlacementSupport = $true; $foreign.Run()
if ($foreign.Cancels -ne 0 -or $foreign.States -ne 0 -or $foreign.suppressPlacementFrame -ne -1 -or !$foreign.selectingPlacementSupport) { throw 'Release modified an unowned production phase.' }
$owned = [QaReleaseFixture]::new(); $owned.qaUnpaidSupportFence=$true; $owned.selectingPlacementSupport=$true; $owned.Run()
if ($owned.Cancels -ne 1 -or $owned.qaUnpaidSupportFence -or $owned.suppressPlacementFrame -ne 17) { throw 'Owned release did not cancel and suppress its frame.' }
$finished = [QaReleaseFixture]::new(); $finished.qaUnpaidSupportFence=$true; $finished.Run()
if ($finished.Cancels -ne 0 -or $finished.qaUnpaidSupportFence -or $finished.suppressPlacementFrame -ne 17) { throw 'Finished release failed.' }
$fault = [QaReleaseFixture]::new(); $fault.qaUnpaidSupportFence=$true; $fault.selectingPlacementSupport=$true; $fault.ThrowOnCancel=$true
$threw=$false; try { $fault.Run() } catch { $threw=$true }
if (!$threw -or !$fault.qaUnpaidSupportFence) { throw 'A failed cancellation silently released the fence.' }
'PASS actual QA Release method: unowned phase untouched; owned pending/finished cleanup; exception retains fence. Native cancellation/time substituted; no game proof.'
