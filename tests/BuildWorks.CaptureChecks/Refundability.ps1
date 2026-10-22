[CmdletBinding()]
param([string]$ReportPath = '', [switch]$OrderNegativeControl)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Extract-Block([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw ('Source member missing: ' + $signature) }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced source member' }
    $source.Substring($start, $end-$start)
}
$taskSource = Get-Content (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPayoutIssuance.cs') -Raw
$taskRemoval = Get-Content (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionCancellationRemoval.cs') -Raw
$taskGuard = 'BlueprintConstructionPayoutIssuance.RequireRefundable(plans, project, player.GetPlayerID());'
function Require-Order([string]$source) {
    $guard = $source.IndexOf($taskGuard, [StringComparison]::Ordinal)
    if ($guard -lt 0 -or $guard -gt $source.IndexOf('record = journal.Prepare(', [StringComparison]::Ordinal) -or
        $guard -gt $source.IndexOf('record = journal.BeginRemoval(', [StringComparison]::Ordinal) -or
        $guard -gt $source.IndexOf('target.Wear.Remove(true)', [StringComparison]::Ordinal)) {
        throw 'Refundability must precede cancellation preparation, intent and native deletion.'
    }
}
Require-Order $taskRemoval
$taskConstructor = Extract-Block $taskRemoval 'internal BlueprintConstructionCancellationRemoval('
$taskSequence = $taskConstructor.Substring($taskConstructor.IndexOf($taskGuard, [StringComparison]::Ordinal))
$taskSequence = $taskSequence.Substring(0, $taskSequence.LastIndexOf('}'))
if ($OrderNegativeControl) { $taskSequence = $taskSequence.Replace($taskGuard, '') }
$taskCode = Get-Content (Join-Path $PSScriptRoot 'RefundabilityHost.cs') -Raw
$taskCode = $taskCode.Replace('__PREFLIGHT__', (Extract-Block $taskSource 'internal static void RequireRefundable('))
$taskCode = $taskCode.Replace('__RESTORE__', (Extract-Block $taskSource 'private static ItemDrop.ItemData Restore('))
$taskCode = $taskCode.Replace('__REMOVAL__', $taskSequence)
Add-Type -TypeDefinition $taskCode
$taskObserved = ''
try { $taskChecks = [RefundabilityChecks.BlueprintConstructionPayoutIssuance]::Run() } catch { $taskObserved = $_.Exception.ToString() }
if ($OrderNegativeControl) {
    if (!$taskObserved.Contains('Last unsupported row entered cancellation/deletion')) { throw ('Missing-preflight negative control was not detected: '+$taskObserved) }
    'PASS refundability ordering negative control: removing early guard enters mocked cancellation/deletion and is detected.'
    return
}
if ($taskObserved) { throw $taskObserved }
if ($taskChecks -ne 38) { throw ('Refundability coverage differs: ' + $taskChecks) }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualPreflightAndRestoreCompiled=$true;nativeBoundariesSubstituted=$true;exactPropertyValidatorSubstituted=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
"PASS refundability: $taskChecks actual-preflight/Restore checks and early-call wiring; native and exact-property validation boundaries substituted."
