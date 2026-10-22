[CmdletBinding()]
param([string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Extract-Block([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw ('Source member missing: '+$signature) }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced source member' }
    $source.Substring($start, $end-$start)
}
$taskRuntimePath = Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionProjectRuntime.cs'
$taskFinalizerPath = Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPaidCancellation.cs'
$taskRuntime = Get-Content -LiteralPath $taskRuntimePath -Raw
$taskFinalizer = Get-Content -LiteralPath $taskFinalizerPath -Raw
$taskCode = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PaidCancellationHost.cs') -Raw
$taskCode = $taskCode.Replace('__FINALIZER__', (Extract-Block $taskFinalizer 'internal sealed class BlueprintConstructionPaidCancellation'))
$taskCode = $taskCode.Replace('__SCHEDULER__', (Extract-Block $taskRuntime 'private void TickPaidCancellation()'))
Add-Type -TypeDefinition $taskCode
$taskChecks = [PaidCancellationChecks.OstrixMods.BuildWorks.BlueprintConstructionProjectRuntime]::Run()
if ($taskChecks -ne 56) { throw ('Finalization coverage differs: '+$taskChecks) }
$taskTick = Extract-Block $taskRuntime 'internal void Tick()'
if ($taskTick.IndexOf('payout?.Tick()') -gt $taskTick.IndexOf('TickPaidCancellation()') -or
    $taskTick.IndexOf('TickPaidCancellation()') -gt $taskTick.IndexOf('TickPayoutCollection()') -or
    $taskTick -notmatch 'paying && payout.Finished && payout.Status == "issued-saved"' -or
    $taskTick -notmatch 'if \(!record.Cancelled\) cancellationProjects.Add\(id\)' -or
    (Extract-Block $taskRuntime 'internal void Suspend()') -notmatch 'paidCancellation\?\.Refuse' -or
    (Extract-Block $taskRuntime 'internal void Suspend()') -notmatch 'cancellationProjects.Clear\(\)') { throw 'Finalizer ordering/startup/suspend wiring differs.' }
$taskCollection = Extract-Block $taskRuntime 'private void TickPayoutCollection()'
if ($taskCollection -notmatch 'cancellationProjects.Add\(collection.Project\)' -or
    $taskCollection -notmatch 'cancellationProjects.Add\(cleanup.Project\)') { throw 'Settled intent lost finalization requeue.' }
if ($ReportPath) {
 @{passed=$true;checks=$taskChecks;actualFinalizerAndSchedulerCompiled=$true;fakeNativeBoundaries=$true;gameHostTested=$false;
 runtimeSha256=(Get-FileHash -LiteralPath $taskRuntimePath).Hash;finalizerSha256=(Get-FileHash -LiteralPath $taskFinalizerPath).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
"PASS paid cancellation finalizer/scheduler: $taskChecks actual-code assertions; native readback/commit boundaries substituted, no in-game proof."
