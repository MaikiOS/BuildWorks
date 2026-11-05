[CmdletBinding()]
param([string]$ReportPath='')
$ErrorActionPreference='Stop'
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
function Extract-Block([string]$source,[string]$signature){
    $start=$source.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw "Missing source member: $signature"}
    $brace=$source.IndexOf('{',$start);$depth=1;$end=$brace+1
    while($depth -gt 0 -and $end -lt $source.Length){if($source[$end] -eq '{'){$depth++};if($source[$end] -eq '}'){$depth--};$end++}
    if($depth -ne 0){throw 'Unbalanced source member'}
    $source.Substring($start,$end-$start)
}
$runtime=Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionProjectRuntime.cs') -Raw
$code=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PaymentReadHost.cs') -Raw
$members=@{
    '__READ_PAYMENT__'='internal (Journal.Record Legacy, Journal.SourcesRecord Sources, Journal.OutcomeRecord Outcome) ReadPartPayment('
    '__READ_PHASE__'='internal int? ReadPartPhase('
    '__PROGRESS__'='internal (int Built, int Total, bool NeedsReview) Progress('
    '__NEXT__'='private Store.Part NextPart('
    '__PENDING__'='private Store.Part[] PendingParts('
}
foreach($key in $members.Keys){$code=$code.Replace($key,(Extract-Block $runtime $members[$key]))}
$execution=Get-Content -LiteralPath (Join-Path $taskRoot 'src/BuildWorks/BlueprintConstructionPartExecution.cs') -Raw
$start=$execution.IndexOf('var payment = journal.ReadPayment(project, partId);',[StringComparison]::Ordinal)
$end=$execution.IndexOf('var existing = payment.Legacy;',[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start -or $execution.IndexOf('journal.Prepare(',[StringComparison]::Ordinal) -lt $end){throw 'Source start guard missing or after prepare'}
$code=$code.Replace('__START_GUARD__',$execution.Substring($start,$end-$start))
foreach($name in @('BlueprintConstructionProjectView.cs','BlueprintConstructionProjectFog.cs')){
    $text=Get-Content -LiteralPath (Join-Path $taskRoot ('src/BuildWorks/'+$name)) -Raw
    if($text -match 'runtime.ReadPart\(' -or $text -notmatch 'runtime.ReadPartPhase\('){throw "Legacy phase binding retained: $name"}
}
Add-Type -TypeDefinition $code
$checks=[PaymentReadChecks.Runtime]::Run()
if($checks -ne 39){throw "Payment runtime coverage differs: $checks"}
if($ReportPath){@{passed=$true;checks=$checks;actualRuntimeMethodsAndStartGuard=$true;fakeJournalAndNative=$true;nativeTested=$false}|ConvertTo-Json|Set-Content -LiteralPath $ReportPath}
"PASS source-aware runtime read: $checks actual-method/start-guard assertions; phase View/Fog bindings checked; journal/native boundaries substituted, no game/provider proof."
