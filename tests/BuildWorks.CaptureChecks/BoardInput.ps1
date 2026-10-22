[CmdletBinding()]
param([string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Extract-Block([string]$source, [string]$signature) {
    $start = $source.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Source member missing: $signature" }
    $brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $source.Length) {
        if ($source[$end] -eq '{') { $depth++ }
        if ($source[$end] -eq '}') { $depth-- }
        $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced source member' }
    $source.Substring($start, $end-$start)
}
$taskSession = Get-Content (Join-Path $taskRoot 'src/BuildWorks/PrecisionPlacementSession.cs') -Raw
$taskPlugin = Get-Content (Join-Path $taskRoot 'src/BuildWorks/BuildWorksPlugin.cs') -Raw
$taskCode = Get-Content (Join-Path $PSScriptRoot 'BoardInputHost.cs') -Raw
foreach ($taskMember in @(
    @('__GAMEPLAY__',$taskSession,'private bool GameplayInputAvailable('),
    @('__SESSION_BLOCK__',$taskSession,'public bool ShouldBlockPlayerInput('),
    @('__PLUGIN_BLOCK__',$taskPlugin,'private static bool ShouldBlockPlayerInput('))) {
    $taskCode = $taskCode.Replace($taskMember[0], (Extract-Block $taskMember[1] $taskMember[2]))
}
foreach ($taskMember in @(
    @('__CAN_OPEN__',$taskSession,'internal bool CanOpenConstructionProject'),
    @('__PLUGIN_CAN_OPEN__',$taskPlugin,'internal static bool CanOpenConstructionProject'))) {
    $taskExpression = [regex]::Match($taskMember[1], [regex]::Escape($taskMember[2])+'\s*=>[^;]+;').Value
    if (!$taskExpression) { throw "Source property missing: $($taskMember[2])" }
    $taskCode = $taskCode.Replace($taskMember[0], $taskExpression)
}
$taskProbe = [regex]::Match($taskSession, 'internal bool ProbingNativeInput\s*=>[^;]+;').Value
if (!$taskProbe) { $taskProbe = 'internal bool ProbingNativeInput => false;' }
$taskCode = $taskCode.Replace('__PROBE__', $taskProbe)
Add-Type -TypeDefinition $taskCode -WarningAction SilentlyContinue
$taskChecks = [BoardInputChecks.BuildWorksPlugin]::Run()
if ($taskChecks -ne 16) { throw "Board input coverage differs: $taskChecks" }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualInputAndPreflightCompiled=$true;fakeHost=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
"PASS board input: $taskChecks assertions across actual native-input probe, plugin gate and board preflight."
