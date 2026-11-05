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
$taskRuntime = Get-Content -LiteralPath $taskRuntimePath -Raw
$taskActions = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionCancellationAction.cs') -Raw
$taskCode = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'BoardCancellationHost.cs') -Raw
$taskCode = $taskCode.Replace('__ENUM__', (Extract-Block $taskActions 'internal enum ConstructionCancellationAction'))
$taskCode = $taskCode.Replace('__ROUTER__', (Extract-Block $taskRuntime 'internal void RequestCancellationFromBoard('))
$taskCode = $taskCode.Replace('__RESUME__', (Extract-Block $taskRuntime 'internal void ResumePreparedRemoval('))
$taskCode = $taskCode.Replace('__BUSY__', [regex]::Match($taskRuntime, 'private bool OtherPayoutBusy\s*=>[^;]+;').Value)
$taskCode = $taskCode.Replace('__PENDING__', [regex]::Match($taskRuntime, 'internal bool ReturnPending\([^;]+;').Value)
$taskView = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionProjectView.cs') -Raw
$taskConfirmation = [regex]::Match($taskView, 'private static string ConfirmationKey\([^;]+;').Value
if (!$taskConfirmation) { throw 'Confirmation mapping missing.' }
$taskCode = $taskCode.Replace('__CONFIRMATION__', $taskConfirmation)
$taskBuilding = [regex]::Match($taskView, 'bool building = [^;]+;').Value
$taskSummaryStart = $taskView.IndexOf('var progress = runtime.Progress(board.Id);', [StringComparison]::Ordinal)
$taskSummaryEnd = $taskView.IndexOf('else if (missing)', [Math]::Max(0, $taskSummaryStart), [StringComparison]::Ordinal)
if (!$taskBuilding -or $taskSummaryStart -lt 0 -or $taskSummaryEnd -le $taskSummaryStart) { throw 'Phase summary source fragment missing.' }
$taskCode = $taskCode.Replace('__BUILDING__', $taskBuilding).Replace('__SUMMARY__', $taskView.Substring($taskSummaryStart, $taskSummaryEnd-$taskSummaryStart))
$taskContinue = [regex]::Match($taskView, 'continueButton.interactable = [^;]+;').Value
if (!$taskContinue) { throw 'Continue gate missing.' }
$taskCode = $taskCode.Replace('__CONTINUE__', $taskContinue)
Add-Type -TypeDefinition $taskCode
$taskChecks = [BoardCancellationChecks.BlueprintConstructionProjectRuntime]::Run()
if ($taskChecks -ne 54) { throw ('Board cancellation coverage differs: '+$taskChecks) }
if ($taskView -match 'runtime.CancelUnpaid\(' -or $taskView -notmatch 'runtime.RequestCancellationFromBoard\(') { throw 'Board view bypasses cancellation router.' }
if ($taskView -notmatch 'bool building = action == ConstructionCancellationAction.Unpaid \|\| action == ConstructionCancellationAction.Remove;' -or
    $taskView -notmatch 'continueButton.interactable = building && !runtime.ReturnPending\(board.Id\);' -or
    $taskView -notmatch 'cancelButton.interactable = action != ConstructionCancellationAction.Review && action != ConstructionCancellationAction.AwaitCollection;' -or
    $taskView -notmatch 'BuildWorksLocalization.Text\(ConfirmationKey\(action\)\)') { throw 'View mutation availability or confirmation wiring differs.' }
if ($ReportPath) {
    @{passed=$true;checks=$taskChecks;routerChecks=38;phaseSummaryChecks=12;continueChecks=4;actualPhaseSummaryCompiled=$true;actualRouterAndResumeCompiled=$true;fakeHost=$true;gameHostTested=$false;
      runtimeSha256=(Get-FileHash -LiteralPath $taskRuntimePath).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
"PASS board cancellation: 38 actual-router assertions +12 actual phase-summary +4 Continue cases; native operations substituted, no game/payment/refund proof."
