$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/Test-FogSimulation.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors|Out-String)}
function Guard([string]$mode,[string]$pattern){
    $clauses=@($ast.FindAll({param($n) $n -is [Management.Automation.Language.IfStatementAst]},$true)|ForEach-Object{$_.Clauses}|Where-Object{$_.Item1.Extent.Text -eq ('$'+$mode) -and $_.Item2.Extent.Text -like $pattern})
    if($clauses.Count -ne 1){throw "Verifier not unique: $mode"}
    $body=$clauses[0].Item2.Extent.Text
    [scriptblock]::Create($body.Substring(1,$body.Length-2))
}
# These are validator fixtures, never receipts from a GPU run.
$physics=@{passed=$true;realSolver=$true;stagedLedger=$true;velocityAlias=$true;packingRollback=$true;shortageRollback=$true;callbackWaitsInFixture=$true;productionAllocatorTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;gridX=96;gridY=80;gridZ=80;owners=2;ticks=360;fieldComparisons=664;ledgerComparisons=498;atlasComparisons=28;committed=161;rejected=2;metadataComparisons=164;metadataBeforeReceipt=$true;metadataOnlyTested=$true;fractionalVisualClock=$true}
$arena=@{passed=$true;shortageRollback=$true;packingRollback=$true;nonfiniteRollback=$true;pendingRemoval=$true;reusedSlot=$true;guttersExact=$true;freeBlocksConserved=$true;productionAllocatorTested=$false;fullWallTested=$false;wholeHouseSupported=$false;gameHostTested=$false;owners=8;capacityBlocks=27;committed=47;rejected=3;batchChecks=15;fieldComparisons=480;ledgerComparisons=360}
$frames=@{passed=$true;cachedRepliesAfterExpiry=$true;receiptFirstHandled=$true;pendingResubmitRejected=$true;pendingRetireRetained=$true;generationReuse=$true;lateReplyRejected=$true;pollingHasNoWait=$true;verificationBarriersBetweenBatches=$true;productionAllocatorTested=$false;rendererTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;frames=32;owners=2;committed=52;rejected=0;fieldComparisons=224;ledgerComparisons=168;atlasComparisons=16;metadataComparisons=52}
$negative=0
foreach($fixture in @(@('ArenaPhysicsOnly','*Arena physics evidence missing*',$physics),@('ArenaOnly','*Arena GPU evidence missing*',$arena),@('ArenaFramesOnly','*Arena frames evidence missing*',$frames))){
    $guard=Guard $fixture[0] $fixture[1];$baseline=$fixture[2];$result=$baseline.Clone(); & $guard
    foreach($field in $baseline.Keys){
        $badValues=if($baseline[$field] -is [bool]){@($null,'true',(-not $baseline[$field]))}elseif($field -eq 'committed' -and $fixture[0] -eq 'ArenaPhysicsOnly'){@($null,'161',[double]::NaN,[double]::PositiveInfinity,160,341)}elseif($field -eq 'frames'){@($null,'32',[double]::NaN,[double]::PositiveInfinity,31,5001)}else{@($null,'bad',[double]::NaN,[double]::PositiveInfinity,($baseline[$field]+1))}
        foreach($bad in $badValues){
            $result=$baseline.Clone();$result[$field]=$bad;$rejected=$false
            try{& $guard}catch{$rejected=$true}
            if(!$rejected){throw "Corrupt $($fixture[0]) fixture accepted: $field=$bad"};++$negative
        }
    }
}
$guard=Guard 'ArenaPhysicsOnly' '*Arena physics evidence missing*'
$result=$physics.Clone();$result.committed=340;$result.metadataComparisons=343;& $guard
$guard=Guard 'ArenaFramesOnly' '*Arena frames evidence missing*'
$result=$frames.Clone();$result.frames=5000;& $guard
$probe=[IO.File]::ReadAllText((Join-Path $root 'tools/BuildWorks.UiWorkbench/Assets/Editor/FogArenaPhysicsProbe.cs'))
$poll=[regex]::Match($probe,'internal bool Poll\(\)[\s\S]*?\n        \}')
if(!$poll.Success -or $poll.Value -match 'WaitForCompletion|WaitAllRequests|GetData'){throw 'Polling must use copied callback results without blocking/native data reads.'}
@{passed=$true;gpuExecuted=$false;fixturesOnly=$true;negativeCases=$negative;parserErrors=0}|ConvertTo-Json
