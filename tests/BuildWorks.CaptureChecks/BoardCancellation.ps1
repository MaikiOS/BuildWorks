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
$taskView = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionProjectView.cs') -Raw
$taskConfirmation = [regex]::Match($taskView, 'private static string ConfirmationKey\([^;]+;').Value
if (!$taskConfirmation) { throw 'Confirmation mapping missing.' }
$taskCode = $taskCode.Replace('__CONFIRMATION__', $taskConfirmation)
Add-Type -TypeDefinition $taskCode
$taskChecks = [BoardCancellationChecks.BlueprintConstructionProjectRuntime]::Run()
if ($taskChecks -ne 30) { throw ('Board cancellation coverage differs: '+$taskChecks) }
if ($taskView -match 'runtime.CancelUnpaid\(' -or $taskView -notmatch 'runtime.RequestCancellationFromBoard\(') { throw 'Board view bypasses cancellation router.' }
if ($taskView -notmatch 'bool building = action == ConstructionCancellationAction.Unpaid \|\| action == ConstructionCancellationAction.Remove;' -or
    $taskView -notmatch 'continueButton.interactable = building;' -or
    $taskView -notmatch 'cancelButton.interactable = action != ConstructionCancellationAction.Review && action != ConstructionCancellationAction.AwaitCollection;' -or
    $taskView -notmatch 'BuildWorksLocalization.Text\(ConfirmationKey\(action\)\)') { throw 'View mutation availability or confirmation wiring differs.' }
if ($ReportPath) {
    @{passed=$true;checks=$taskChecks;actualRouterAndResumeCompiled=$true;fakeHost=$true;gameHostTested=$false;
      runtimeSha256=(Get-FileHash -LiteralPath $taskRuntimePath).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
"PASS board cancellation: $taskChecks actual-router assertions; native operations substituted, no game/payment/refund proof."
