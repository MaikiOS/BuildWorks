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
$physics=@{passed=$true;realSolver=$true;stagedLedger=$true;velocityAlias=$true;packingRollback=$true;shortageRollback=$true;callbackWaitsInFixture=$true;productionAllocatorTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;gridX=96;gridY=80;gridZ=80;owners=2;ticks=360;fieldComparisons=664;ledgerComparisons=498;atlasComparisons=28;committed=161;rejected=2;metadataComparisons=164;metadataBeforeReceipt=$true;metadataOnlyTested=$true;fractionalVisualClock=$true;borrowedOwnershipValidated=$true;borrowedInvalidCases=11;runtimeArenaValidated=$true}
$arena=@{passed=$true;shortageRollback=$true;packingRollback=$true;nonfiniteRollback=$true;pendingRemoval=$true;reusedSlot=$true;guttersExact=$true;freeBlocksConserved=$true;productionAllocatorTested=$false;fullWallTested=$false;wholeHouseSupported=$false;gameHostTested=$false;owners=8;capacityBlocks=27;committed=47;rejected=3;batchChecks=15;fieldComparisons=480;ledgerComparisons=360}
$frames=@{queuedInputsReplayed=$true;capturedWhilePending=$true;metadataOnlyPending=$true;passed=$true;cachedRepliesAfterExpiry=$true;receiptFirstHandled=$true;pendingResubmitRejected=$true;pendingRetireRetained=$true;generationReuse=$true;lateReplyRejected=$true;pollingHasNoWait=$true;verificationBarriersBetweenBatches=$true;productionAllocatorTested=$false;rendererTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;frames=33;owners=2;committed=54;rejected=0;fieldComparisons=232;ledgerComparisons=174;atlasComparisons=20;metadataComparisons=54}
$published=@{passed=$true;metadataOnly=$true;rejectedRetained=$true;geometryBeforeCpuConfirmation=$true;meshAndAtlasVolume=$true;productionSchedulerTested=$false;wholeHouseSupported=$false;gameHostTested=$false;gpuTimingsMeasured=$false;owners=2;captures=87;comparisons=62;negativeControls=26;legacyCaptures=16;legacyMeanDelta=0.0;maxMeanDelta=0.0;maxLocalDelta=0.0;poisonedCpuDelta=0.0;disabledKeywordDelta=.001;staleGenerationDelta=.001}
$negative=0
foreach($fixture in @(@('ArenaPhysicsOnly','*Arena physics evidence missing*',$physics),@('ArenaOnly','*Arena GPU evidence missing*',$arena),@('ArenaFramesOnly','*Arena frames evidence missing*',$frames))){
    $guard=Guard $fixture[0] $fixture[1];$baseline=$fixture[2];$result=$baseline.Clone(); & $guard
    foreach($field in $baseline.Keys){
        $badValues=if($baseline[$field] -is [bool]){@($null,'true',(-not $baseline[$field]))}elseif($field -eq 'committed' -and $fixture[0] -eq 'ArenaPhysicsOnly'){@($null,'161',[double]::NaN,[double]::PositiveInfinity,160,341)}elseif($field -eq 'frames'){@($null,'33',[double]::NaN,[double]::PositiveInfinity,32,5001)}else{@($null,'bad',[double]::NaN,[double]::PositiveInfinity,($baseline[$field]+1))}
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
    $badValues=if($baseline -is [bool]){@($null,'true',(-not $baseline))}elseif($field -in @('owners','captures','comparisons','negativeControls','legacyCaptures')){@($null,'bad',[double]::NaN,[double]::PositiveInfinity,($baseline+1))}else{
        $limit=switch($field){maxMeanDelta{.0001};maxLocalDelta{.03};poisonedCpuDelta{.000001};legacyMeanDelta{.000001};default{.0001}}
        @($null,'bad',[double]::NaN,[double]::PositiveInfinity,-1,$true,$limit)
    }
    foreach($bad in $badValues){
        $result=$published.Clone();$result[$field]=$bad;$rejected=$false
        try{& $guard}catch{$rejected=$true}
        if(!$rejected){throw "Corrupt PublishedOnly fixture accepted: $field=$bad"};++$negative
    }
}
$guard=Guard 'ArenaPhysicsOnly' '*Arena physics evidence missing*'
$result=$physics.Clone();$result.committed=340;$result.metadataComparisons=343;& $guard
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
$baseline=@{passed=$true;shaderMessagesChecked=$true;colorSpace='Linear';assets=5;kernels=27;arenaPublicationAndReload=$true;sources=$sources;sha256=(Get-FileHash -LiteralPath (Join-Path $output 'buildworks-fog-v11')).Hash}
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
foreach($name in @('BlueprintFogSnapshotState.cs','BlueprintFogInputQueue.cs','BlueprintFogAssetLayout.cs','BlueprintFogArena.cs')){
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
