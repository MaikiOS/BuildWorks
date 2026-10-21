[CmdletBinding()]
param([string]$ReportPath = '', [switch]$DistanceNegativeControl)
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
$taskPluginPath = Join-Path $taskRoot 'src/BuildWorks/BuildWorksPlugin.cs'
$taskRuntime = Get-Content -LiteralPath $taskRuntimePath -Raw
$taskPlugin = Get-Content -LiteralPath $taskPluginPath -Raw
$taskCollection = Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPayoutCollection.cs') -Raw
$taskCode = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PayoutInteractionHost.cs') -Raw
foreach ($taskMember in @(
    @('__START__', 'private void StartCollection('),
    @('__BEGIN__', 'internal bool TryBeginCollection('),
    @('__TICK__', 'private void TickPayoutCollection()'))) {
    $taskCode = $taskCode.Replace($taskMember[0], (Extract-Block $taskRuntime $taskMember[1]))
}
$taskPatch = Extract-Block $taskPlugin 'private static class BlueprintPayoutInteractPatch'
$taskCode = $taskCode.Replace('__PREFIX__', (Extract-Block $taskPatch 'private static bool Prefix('))
$taskCode = $taskCode.Replace('__REACH__', (Extract-Block $taskCollection 'internal static bool WithinReach('))
if ($DistanceNegativeControl) {
    $taskGuard = '!BlueprintConstructionPayoutCollection.WithinReach(player, source) ||'
    if (!$taskCode.Contains($taskGuard)) { throw 'Distance negative control has no actual guard to remove.' }
    $taskCode = $taskCode.Replace($taskGuard, '').Replace('namespace PayoutInteractionChecks', 'namespace PayoutInteractionNegativeControl')
    Add-Type -TypeDefinition $taskCode
    $taskObserved = ''
    try { $null = [PayoutInteractionNegativeControl.BlueprintConstructionProjectRuntime]::Run() }
    catch { $taskObserved = $_.Exception.ToString() }
    if (!$taskObserved.Contains('Stale distant hover admitted')) { throw ('Distance negative control did not detect the reproduced gap: '+$taskObserved) }
    'PASS distance negative control: removing the actual entry guard is rejected: Stale distant hover admitted. No native game execution.'
    return
}
Add-Type -TypeDefinition $taskCode
$taskChecks = [PayoutInteractionChecks.BlueprintConstructionProjectRuntime]::Run()
if ($taskChecks -ne 60) { throw ('Interaction coverage incomplete: '+$taskChecks) }
$taskTick = Extract-Block $taskRuntime 'internal void Tick()'
$taskSuspend = Extract-Block $taskRuntime 'internal void Suspend()'
if ($taskTick -notmatch 'TickPayoutCollection\(\)' -or $taskTick -notmatch 'cleanupProjects.Add\(id\)' -or
    $taskSuspend -notmatch 'cleanupProjects.Clear\(\)') { throw 'Scheduler tick/startup/session cleanup wiring differs.' }
if ($taskPlugin -notmatch '\[HarmonyPatch\(typeof\(ItemDrop\), nameof\(ItemDrop.Interact\), new Type\[\] \{ typeof\(Humanoid\), typeof\(bool\), typeof\(bool\) \}\)\]\s*private static class BlueprintPayoutInteractPatch') {
    throw 'Manual native Interact overload is not wired exactly.'
}
if ((Extract-Block $taskCollection 'private bool SourceMatches()') -notmatch 'return WithinReach\(player, source\)' -or
    $taskCollection -notmatch 'private bool BeforeCustody\(\) => SavedCustody\(\)' -or
    $taskCollection -notmatch 'private bool SavedCustody\(\) =>[^;]+SourceMatches\(\)') { throw 'Async credit lost its source-distance custody check.' }
if ($ReportPath) {
    @{passed=$true;checks=$taskChecks;actualSourceCompiled=$true;fakeHost=$true;gameHostTested=$false;
      runtimeSha256=(Get-FileHash -LiteralPath $taskRuntimePath).Hash;
      collectionSha256=(Get-FileHash -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPayoutCollection.cs')).Hash;
      pluginSha256=(Get-FileHash -LiteralPath $taskPluginPath).Hash;
      fixtureSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'PayoutInteractionHost.cs')).Hash} |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
"PASS payout manual interaction and cleanup scheduling: $taskChecks actual-method assertions; inert constructors, no native game/inventory/disk proof."
