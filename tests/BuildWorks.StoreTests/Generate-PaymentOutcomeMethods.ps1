$ErrorActionPreference='Stop'
function Member([string]$path,[string]$signature) {
    $text=[IO.File]::ReadAllText((Join-Path $PSScriptRoot ('../../src/BuildWorks/'+$path)))
    $start=$text.IndexOf($signature,[StringComparison]::Ordinal)
    if($start -lt 0){throw "Missing production payment member: $signature"}
    $brace=$text.IndexOf('{',$start);$depth=1;$end=$brace+1
    while($depth -gt 0 -and $end -lt $text.Length){if($text[$end] -eq '{'){$depth++};if($text[$end] -eq '}'){$depth--};$end++}
    if($depth -ne 0){throw 'Unbalanced production payment member'}
    $text.Substring($start,$end-$start)
}
$execution=@((Member 'BlueprintConstructionPartExecution.cs' 'internal void EndNative('),
    (Member 'BlueprintConstructionPartExecution.cs' 'internal void BeforeNative('),
    (Member 'BlueprintConstructionPartExecution.cs' 'internal void StopBeforeNative(')) -join "`n"
$session=@((Member 'PrecisionPlacementSession.cs' 'internal BlueprintConstructionRefund BeginConstructionPayment('),
    (Member 'PrecisionPlacementSession.cs' 'internal void EndConstructionPayment('),
    (Member 'PrecisionPlacementSession.cs' 'public void OnNativePlacementException(')) -join "`n"
$code="using System; using System.Linq; using OstrixMods.BuildWorks; using Journal=OstrixMods.BuildWorks.BlueprintConstructionPartJournal;`nnamespace PaymentOutcomeBoundary { internal sealed partial class Execution { $execution } internal sealed partial class Session { $session } }"
[IO.Directory]::CreateDirectory((Join-Path $PSScriptRoot 'obj')) | Out-Null
[IO.File]::WriteAllText((Join-Path $PSScriptRoot 'obj/PaymentOutcome.generated.cs'),$code,[Text.UTF8Encoding]::new($false))
