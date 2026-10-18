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
$physics=@{velocityPagingComparisons=252;velocityPagingLogicalOwners=28;velocityPagingResidentOwners=7;passed=$true;realSolver=$true;stagedLedger=$true;velocityAlias=$true;packingRollback=$true;shortageRollback=$true;callbackWaitsInFixture=$true;productionAllocatorTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;gridX=96;gridY=80;gridZ=80;owners=2;ticks=360;fieldComparisons=664;ledgerComparisons=498;atlasComparisons=28;committed=161;rejected=2;metadataComparisons=163;metadataBeforeReceipt=$true;metadataOnlyTested=$true;fractionalVisualClock=$true;borrowedOwnershipValidated=$true;borrowedInvalidCases=11;runtimeArenaValidated=$true;runtimeChannelsValidated=$true;runtimeLifecycleCases=38;committedRecoveryComparisons=2;physicalRecoveryTicks=120;physicalRecoveryObservedActive=$true;physicalRecoveryCompleted=$true;logicalSourcePhysicsComparisons=7}
$arena=@{passed=$true;shortageRollback=$true;packingRollback=$true;nonfiniteRollback=$true;pendingRemoval=$true;reusedSlot=$true;guttersExact=$true;freeBlocksConserved=$true;productionAllocatorTested=$false;fullWallTested=$false;wholeHouseSupported=$false;gameHostTested=$false;owners=8;capacityBlocks=27;committed=47;rejected=3;batchChecks=15;fieldComparisons=480;ledgerComparisons=360}
$frames=@{velocityHouseSamples=2688;velocityHouseComparisons=196;velocityHouseContacts=6;velocityHouseSeconds=3.2;velocityOverlapRejected=$true;velocityPagingSeconds=3.2;velocityPagingFrames=96;velocityPagingSamples=288;velocityPagingComparisons=21;runtimeChannelsValidated=$true;queuedInputsReplayed=$true;capturedWhilePending=$true;metadataOnlyPending=$true;passed=$true;cachedRepliesAfterExpiry=$true;receiptFirstHandled=$true;pendingResubmitRejected=$true;pendingRetireRetained=$true;generationReuse=$true;lateReplyRejected=$true;pollingHasNoWait=$true;verificationBarriersBetweenBatches=$true;productionAllocatorTested=$false;rendererTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;frames=33;owners=2;committed=54;rejected=0;fieldComparisons=232;ledgerComparisons=174;atlasComparisons=20;metadataComparisons=54}
$published=@{buildingAdapterValidated=$true;buildingComparisons=4;pageSamplingNegativeCases=15;hardwareFilteredPages=$true;coldIdleControlSensitive=$true;continuousV11Parity=$true;coldIdleMaxLocalDelta=.03;logicalVisualValidated=$true;logicalVisualDeferredDisposal=$true;dormantClockAndBodyValidated=$true;logicalVisualComparisons=18;continuousStateComparisons=56;logicalVisualMaxMeanDelta=0.0;logicalVisualMaxLocalDelta=0.0;runtimeVisualAtlasValidated=$true;staticSourceSeparated=$true;runtimeWrongMappingDelta=.001;runtimeVisualAtlasCaptures=30;runtimeVisualAtlasComparisons=14;runtimeMappingNegativeCases=7;runtimeStaticBytes=8122272;runtimeCombinedRenderBytes=22601184;runtimeMaxMeanDelta=0.0;runtimeMaxLocalDelta=0.0;runtimeChannelsValidated=$true;passed=$true;metadataOnly=$true;rejectedRetained=$true;geometryBeforeCpuConfirmation=$true;meshAndAtlasVolume=$true;productionSchedulerTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;owners=2;captures=87;comparisons=62;negativeControls=26;legacyCaptures=16;legacyMeanDelta=0.0;maxMeanDelta=0.0;maxLocalDelta=0.0;poisonedCpuDelta=0.0;disabledKeywordDelta=.001;staleGenerationDelta=.001}
$recovery=@{passed=$true;expiredHandleObserved=$true;delayedReadbackRetained=$true;staleRecoveryRejected=$true;pendingDisposeSafe=$true;snapshotRecoveryValidated=$true;snapshotWorkspaceIsolated=$true;snapshotGenerationReuse=$true;snapshotPendingRetirement=$true;snapshotSmokeAndSurfaceRetained=$true;snapshotReadbackFailureRetained=$true;logicalSourcesValidated=$true;sourceResidentEmptyState=$true;sourceAdmissionBounded=$true;sourceRecoveryReentry=$true;sourceExplicitRetireGuarded=$true;sourceRemovalAfterDrain=$true;sourceAcceptedTailReplayed=$true;sourceFailedAcquireRetained=$true;sourceFrames=42;gameHostTested=$false;frames=48}
$negative=0
$idle=@{collected=$true;exactReplayValidated=$true;runtimePolicyImplemented=$false;wholeHouseSupported=$false;gameHostTested=$false;scenarios=2;rows=@(foreach($scenario in 0..1){foreach($variant in @(0,2,4,8,12,240,400)){
    $checkpoint=if($variant -ge 240){$variant}else{0}; $window=if($checkpoint -eq 240){4}elseif($checkpoint -eq 400){0}else{$variant}
    $start=if($checkpoint){$checkpoint}elseif($window -eq 0){360}else{[math]::Floor((360-$window*30)/8)*8}
    @{scenario=$scenario;windowSeconds=$window;imageComparisons=$(if($checkpoint -eq 400){6}else{12});initialClock=[double]$(if($start -eq 0){0.0}else{$start/30+.005});differentHalfWords=0;differentLedgerWords=0;fieldMaxDelta=0.0;meanRgbDelta=0.0;localRgbDelta=0.0;synchronizedPrewarmMilliseconds=1.0;synchronizedRestoreMilliseconds=$(if($checkpoint){1.0}else{0.0});checkpointTick=$checkpoint;checkpointFieldLedgerBytes=$(if($checkpoint){19668996}else{0});checkpointRestored=($checkpoint -ne 0);exactReplayControl=($window -eq 12);withinExistingImageLimits=$true}
}})}
$guard=Guard 'IdleOnly' '*Idle replay diagnostic incomplete*'; $result=$idle; & $guard
$copyIdle={ $copy=$idle.Clone(); $copy.rows=@($idle.rows|ForEach-Object{$_.Clone()});$copy }
foreach($field in @('collected','exactReplayValidated','runtimePolicyImplemented','wholeHouseSupported','gameHostTested','scenarios')){
    $badValues=if($idle[$field] -is [bool]){@($null,'true',(-not $idle[$field]))}else{@($null,'2',1,3,[double]::NaN)}
    foreach($bad in $badValues){$result=&$copyIdle;$result[$field]=$bad;$rejected=$false;try{&$guard}catch{$rejected=$true};if(!$rejected){throw ('Bad idle summary accepted: '+$field)};++$negative}
}
foreach($index in 0..13){foreach($field in $idle.rows[$index].Keys){
    $badValues=if($idle.rows[$index][$field] -is [bool]){@($null,'true',(-not $idle.rows[$index][$field]))}else{@($null,'bad',[double]::NaN,[double]::PositiveInfinity,-1)}
    foreach($bad in $badValues){$result=&$copyIdle;$result.rows[$index][$field]=$bad;$rejected=$false;try{&$guard}catch{$rejected=$true};if(!$rejected){throw ("Bad idle row accepted: $index/$field")};++$negative}
}}
foreach($mutation in @({$result.rows=$result.rows[0..12]},{$result.rows[1]=$result.rows[0]},{$result.rows[0].windowSeconds=3},{$result.rows[0].imageComparisons=11},{$result.rows[0].initialClock+=.01},{$result.rows[0].withinExistingImageLimits=$false},{$result.rows[4].differentHalfWords=1},{$result.rows[4].fieldMaxDelta=.001},{$result.rows[4].meanRgbDelta=.000001},{$result.rows[4].localRgbDelta=.001},{$result.rows[0].synchronizedPrewarmMilliseconds=0},{$result.rows[5].checkpointTick=400},{$result.rows[5].checkpointFieldLedgerBytes=0},{$result.rows[5].synchronizedRestoreMilliseconds=0},{$result.rows[5].differentLedgerWords=1},{$result.rows[0].synchronizedRestoreMilliseconds=1})){
    $result=&$copyIdle; & $mutation; $rejected=$false;try{&$guard}catch{$rejected=$true};if(!$rejected){throw 'Bad idle coverage/control accepted.'};++$negative
}
foreach($fixture in @(@('ArenaPhysicsOnly','*Arena physics evidence missing*',$physics),@('ArenaOnly','*Arena GPU evidence missing*',$arena),@('ArenaFramesOnly','*Arena frames evidence missing*',$frames),@('RecoveryOnly','*Recovery evidence missing*',$recovery))){
    $guard=Guard $fixture[0] $fixture[1];$baseline=$fixture[2];$result=$baseline.Clone(); & $guard
    foreach($field in $baseline.Keys){
        $badValues=if($baseline[$field] -is [bool]){@($null,'true',(-not $baseline[$field]))}elseif($field -eq 'committed' -and $fixture[0] -eq 'ArenaPhysicsOnly'){@($null,'161',[double]::NaN,[double]::PositiveInfinity,160,341)}elseif($field -in @('velocityPagingSeconds','velocityHouseSeconds')){@($null,'3.2',[double]::NaN,[double]::PositiveInfinity,3.19,5.01)}elseif($field -eq 'velocityHouseContacts'){@($null,'6',[double]::NaN,[double]::PositiveInfinity,5,9)}elseif($field -eq 'velocityPagingFrames'){@($null,'96',[double]::NaN,[double]::PositiveInfinity,95)}elseif($field -in @('frames','sourceFrames')){@($null,'33',[double]::NaN,[double]::PositiveInfinity,($baseline[$field]-1),5001)}else{@($null,'bad',[double]::NaN,[double]::PositiveInfinity,($baseline[$field]+1))}
        foreach($bad in $badValues){
            $result=$baseline.Clone();$result[$field]=$bad;$rejected=$false
            try{& $guard}catch{$rejected=$true}
            if(!$rejected){throw "Corrupt $($fixture[0]) fixture accepted: $field=$bad"};++$negative
        }
    }
}
$guard=Guard 'PublishedOnly' '*Published render evidence missing*'
$result=$published.Clone(); & $guard
foreach($field in $published.Keys){
    $baseline=$published[$field]
    $badValues=if($baseline -is [bool]){@($null,'true',(-not $baseline))}elseif($field -in @('continuousStateComparisons','logicalVisualComparisons','runtimeVisualAtlasCaptures','runtimeVisualAtlasComparisons','runtimeMappingNegativeCases','runtimeStaticBytes','runtimeCombinedRenderBytes','owners','captures','comparisons','negativeControls','legacyCaptures')){@($null,'bad',[double]::NaN,[double]::PositiveInfinity,($baseline+1))}else{
        $limit=switch($field){coldIdleMaxLocalDelta{.029};logicalVisualMaxMeanDelta{.0001};logicalVisualMaxLocalDelta{.03};runtimeMaxMeanDelta{.0001};runtimeMaxLocalDelta{.03};maxMeanDelta{.0001};maxLocalDelta{.03};poisonedCpuDelta{.000001};legacyMeanDelta{.000001};default{.0001}}
        @($null,'bad',[double]::NaN,[double]::PositiveInfinity,-1,$true,$limit)
    }
    foreach($bad in $badValues){
        $result=$published.Clone();$result[$field]=$bad;$rejected=$false
        try{& $guard}catch{$rejected=$true}
        if(!$rejected){throw "Corrupt PublishedOnly fixture accepted: $field=$bad"};++$negative
    }
}
$guard=Guard 'ArenaPhysicsOnly' '*Arena physics evidence missing*'
$result=$physics.Clone();$result.committed=340;$result.metadataComparisons=342;& $guard
$guard=Guard 'ArenaFramesOnly' '*Arena frames evidence missing*'
$result=$frames.Clone();$result.frames=5000;& $guard
$guard=Guard 'arenaBundle' '*source manifest invalid*'
$actualRoot=$root
$root=Join-Path $root 'artifacts/fog-evidence-candidate-fixture'
$folder=Join-Path $root 'src/BuildWorks/Assets/FogGhostRuntime';$output=Join-Path $root 'artifacts/fog-runtime-assets-candidate'
New-Item -ItemType Directory -Path $folder,$output -Force|Out-Null
$names=@('AccumulatedSmoke.compute','FogGhostVolume.shader','TexturedGhost.shader','GhostMatter.cginc','FogVolumePair.cginc','FogVolumeAtlas.cginc','FogPublished.cginc','FogArena.compute','FogSnapshotMetadata.compute')
$sources=@(foreach($name in $names){Set-Content -LiteralPath (Join-Path $folder $name) -Value ('validator fixture '+$name);@{name=$name;sha256=(Get-FileHash -LiteralPath (Join-Path $folder $name)).Hash}})
Set-Content -LiteralPath (Join-Path $output 'buildworks-fog-v11') -Value 'validator fixture only'
$baseline=@{passed=$true;shaderMessagesChecked=$true;colorSpace='Linear';assets=5;kernels=28;arenaPublicationAndReload=$true;sources=$sources;sha256=(Get-FileHash -LiteralPath (Join-Path $output 'buildworks-fog-v11')).Hash}
try{
    $baseline|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'verification.json');& $guard
    foreach($case in @('missing','duplicate','unknown','sourceHash','bundleHash','shaderCheck','passed','colorSpace','assets','kernels','arenaPublication')){
        $bad=$baseline|ConvertTo-Json -Depth 5|ConvertFrom-Json
        switch($case){
            missing{$bad.sources=@($bad.sources|Select-Object -Skip 1)}
            duplicate{$bad.sources[1].name=$bad.sources[0].name}
            unknown{$bad.sources[0].name='../Unknown.cginc'}
            sourceHash{$bad.sources[0].sha256='BAD'}
            bundleHash{$bad.sha256='BAD'}
            shaderCheck{$bad.shaderMessagesChecked='true'}
            passed{$bad.passed='true'}
            colorSpace{$bad.colorSpace='Gamma'}
            assets{$bad.assets=3}
            kernels{$bad.kernels=8}
            arenaPublication{$bad.arenaPublicationAndReload='true'}
        }
        $bad|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $output 'verification.json');$rejected=$false
        try{& $guard}catch{$rejected=$true}
        if(!$rejected){throw "Invalid candidate source manifest accepted: $case"};++$negative
    }
}finally{$root=$actualRoot}
$builder=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/Build-FogGhostAssets.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors|Out-String)}
$copy=@($builder.FindAll({param($n) $n -is [Management.Automation.Language.ForEachStatementAst] -and $n.Condition.Extent.Text -like '*BlueprintFogAssets.cs*'},$true))
if($copy.Count -ne 1 -or $copy[0].Condition.Extent.Text -notlike '*BlueprintFogSnapshotState.cs*' -or $copy[0].Condition.Extent.Text -notlike '*BlueprintFogInputQueue.cs*'){throw 'Asset builder must stage both fog input types on a clean checkout.'}
$project=Join-Path $root ('artifacts/fog-build-copy-fixture/'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path (Join-Path $project 'Assets/Editor/RuntimeSources') -Force|Out-Null
& ([scriptblock]::Create($copy[0].Extent.Text))
foreach($name in @('BlueprintFogSnapshotState.cs','BlueprintFogInputQueue.cs','BlueprintFogAssetLayout.cs','BlueprintFogArena.cs','BlueprintFogChannels.Arena.cs','BlueprintFogChannels.Sources.cs')){
    if((Get-FileHash -LiteralPath (Join-Path $root ('src/BuildWorks/'+$name))).Hash -cne (Get-FileHash -LiteralPath (Join-Path $project ('Assets/Editor/RuntimeSources/'+$name))).Hash){throw 'Clean asset-build staging differs.'}
}
$assetCopy=@($builder.FindAll({param($n) $n -is [Management.Automation.Language.ForEachStatementAst] -and $n.Condition.Extent.Text -eq "@('FogArena.compute','FogSnapshotMetadata.compute')"},$true))
if($assetCopy.Count -ne 1){throw 'Asset builder must stage both runtime compute programs.'}
$ported=Join-Path $root 'src/BuildWorks/Assets/FogGhostRuntime';$runtime=Join-Path $project 'Assets/BuildWorksFogRuntime'
New-Item -ItemType Directory -Path $runtime -Force|Out-Null
& ([scriptblock]::Create($assetCopy[0].Extent.Text))
foreach($name in @('FogArena.compute','FogSnapshotMetadata.compute')){
    foreach($suffix in @('','.meta')){if((Get-FileHash -LiteralPath (Join-Path $ported ($name+$suffix))).Hash -cne (Get-FileHash -LiteralPath (Join-Path $runtime ($name+$suffix))).Hash){throw 'Runtime compute staging differs.'}}
}
$probe=[IO.File]::ReadAllText((Join-Path $root 'tools/BuildWorks.UiWorkbench/Assets/Editor/FogArenaPhysicsProbe.cs'))
$poll=[regex]::Match($probe,'internal bool Poll\(\)[\s\S]*?\n        \}')
if(!$poll.Success -or $poll.Value -match 'WaitForCompletion|WaitAllRequests|GetData'){throw 'Polling must use copied callback results without blocking/native data reads.'}
if($probe -match 'candidate\[|banks\[|generations\['){throw 'Probe must not duplicate runtime channel lifecycle.'}
$channelSource=[IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintFogChannels.Arena.cs'))
$runtimePoll=[regex]::Match($channelSource,'internal bool PollSnapshots\(\)[\s\S]*?\n        \}')
if(!$runtimePoll.Success -or $runtimePoll.Value -match 'WaitForCompletion|WaitAllRequests|GetData'){throw 'Runtime polling must use copied receipts without waiting/native data reads.'}
# Evaluate the actual C# dispatch expression against the actual kernel width, without a Unity process.
$arenaSource=[IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintFogArena.cs'))
$dispatch=[regex]::Match($arenaSource,'metadataShader\.Dispatch\(initMetadata, (.+), 1, 1\);')
$metadataSource=[IO.File]::ReadAllText((Join-Path $ported 'FogSnapshotMetadata.compute'))
$width=[regex]::Match($metadataSource,'\[numthreads\((\d+),1,1\)\]\s*void InitMetadata').Groups[1].Value
if(!$dispatch.Success -or !$width){throw 'Metadata initialization coverage could not be checked.'}
$typeName='FogMetadataCoverage'+[guid]::NewGuid().ToString('N')
$coverage=Add-Type -TypeDefinition "public static class $typeName { public static int Groups(int owners) { return $($dispatch.Groups[1].Value); } }" -PassThru
foreach($owners in 1..32){$groups=$coverage::Groups($owners);$words=$owners*16;if($groups*[int]$width -lt $words -or ($groups-1)*[int]$width -ge $words){throw "Metadata dispatch does not cover all words: owners=$owners"}}
@{passed=$true;gpuExecuted=$false;fixturesOnly=$true;negativeCases=$negative;parserErrors=0;metadataCoverageCases=32}|ConvertTo-Json
