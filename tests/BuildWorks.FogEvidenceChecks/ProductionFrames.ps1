$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$tokens=$null; $errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/Test-FogProductionFrames.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw ($errors | Out-String)}
$guard=$ast.Find({param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Assert-FogProductionFramesEvidence'},$true)
. ([scriptblock]::Create($guard.Extent.Text))
$directory=Join-Path $root ('artifacts/fog-evidence-fixtures/production-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $directory | Out-Null
$path=Join-Path $directory 'synthetic-validator-input.json'
$started=[datetime]::UtcNow.AddSeconds(-1); $id=[guid]::NewGuid().ToString('N'); $rejections=0
function Measurement {
    $flows=@(foreach($k in 0,1){$bytes=if($k -eq 0){2048}else{4915200};$copy=if($k -eq 0){64}else{4915200}
        @{requested=10;callbacks=10;accepted=10;rejected=0;submissionFailures=0;unreadable=0;requestedBytes=10*$bytes;cpuCopiedBytes=10*$copy;
          awaitingCallback=0;awaitingAcceptance=0;requestToCallbackMilliseconds=20;maxRequestToCallbackMilliseconds=3;
          callbackToAcceptMilliseconds=10;maxCallbackToAcceptMilliseconds=2}})
    @{valid=$true;capacity=32;invalid=0;threadMismatches=0;staleEvents=0;unobservedEvents=0;batches=10;samples=80;ticks=80;
      stageNames=@('Submit','SolverSubmission','RestoreCreate','PageRelease','Upload','CopyEnqueue','CallbackCopy');flowNames=@('Receipt','Velocity');
      stages=@(foreach($s in 0..6){@{count=10;totalMilliseconds=20;maxMilliseconds=3}});flows=$flows}
}
function Receipt([string]$mode='NativeCadence') {
    $native=$mode -cne 'Controls'; $measurement=$mode -ceq 'TelemetryMeasurement'; $postCapture=$mode -ceq 'PostCaptureCandidate'; $polling=$postCapture -or $mode -ceq 'PollingCandidate'; $duration=if($measurement){20}elseif($native){48}else{22}
    $profiling=$measurement -or $mode -cin @('ReuseCandidate','AsyncCaptureCandidate')
    $pairs=switch($mode){'Controls'{0,1,2,3}'NativeControls'{1,2}default{3}}
    $cases=@(foreach($pair in $pairs){
        $capture=($pair -band 2) -ne 0
        @{passed=$true;contact=($pair -band 1) -ne 0;capture=$capture;failure='';scalarComparisons=140;fieldComparisons=196;
          inputSeconds=$duration;clock=$duration;drainSeconds=.2;hostFrames=$duration*30;hostFramesPerSecond=30;maxQueue=12;
          captures=$(if($capture){2}else{0});captureBytes=$(if($capture){1000}else{0});captureMilliseconds=@(if($capture){55;56});
          receiptUpdates=$(if($polling){10}else{0});velocityUpdates=$(if($polling){10}else{0});recoveryUpdates=0;
          postCapturePumps=$(if($postCapture){2}else{0});
          captureStageCount=$(if($profiling){2}else{0});captureRenderMilliseconds=10;captureReadPixelsMilliseconds=10;capturePngMilliseconds=10;captureDiskMilliseconds=1;
          managedBytesStart=1000000;managedBytesEnd=2000000;gcCollections=@(1,1,1);
          captureRequests=2;captureReferenceComparisons=1;captureCpuCopyMilliseconds=1;
          performanceWindow=$(if($profiling){Measurement}else{$null});performanceDrained=$(if($profiling){Measurement}else{$null});
          pagesWindow=@{limit=36;freeLimit=4;words=2457600;allocated=32;active=28;free=4;peak=32;allocations=32;reuses=100;bytes=157286400};
          pagesDrained=@{limit=36;freeLimit=4;words=2457600;allocated=32;active=28;free=4;peak=32;allocations=32;reuses=110;bytes=157286400};
          pollingMilliseconds=$(if($polling){1}else{0});maxPollingMilliseconds=$(if($polling){.1}else{0})}
    })
    @{passed=$true;velocityPageReuseCandidate=$true;gameHostTested=$false;nativeHouseGeometryTested=$false;gpuTimingsMeasured=$false;
      explicitPolling=$polling;postCapturePump=$postCapture;measurementOnly=$measurement;asyncCapture=($mode -ceq 'AsyncCaptureCandidate');mode=$mode;runId=$id;requestedFrameRate=$(if($native){36}else{-1});
      configuration='32slots/32768blocks/Spatial3D/4resident;28procedural walls';cases=$cases}
}
function Reject([string]$name,[scriptblock]$edit,[string]$expected='NativeCadence') {
    $value=Receipt $expected; & $edit $value
    $value | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $path
    $rejected=$false
    try {Assert-FogProductionFramesEvidence $path $started $expected $id} catch {$rejected=$true}
    if(!$rejected){throw ('Invalid production-frame evidence accepted: '+$name)}
    $script:rejections++
}
# Synthetic validator fixtures, never GPU/Valheim evidence.
foreach($mode in @('Controls','NativeCadence','NativeControls','PollingCandidate','PostCaptureCandidate','TelemetryMeasurement','ReuseCandidate','AsyncCaptureCandidate')){
    Receipt $mode | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $path
    Assert-FogProductionFramesEvidence $path $started $mode $id
}
Reject 'exit0/failed report' {param($v) $v.passed=$false}
Reject 'string bool' {param($v) $v.passed='true'}
Reject 'wrong run' {param($v) $v.runId='old'}
Reject 'wrong mode' {param($v) $v.mode='Controls'}
Reject 'wrong pool' {param($v) $v.configuration='smaller'}
Reject 'wrong cap' {param($v) $v.requestedFrameRate=-1}
foreach($key in @('gameHostTested','nativeHouseGeometryTested','gpuTimingsMeasured')){
    Reject $key {param($v) $v[$key]=$true}
}
Reject 'missing case' {param($v) $v.cases=@()}
Reject 'duplicate case' {param($v) $v.cases=@($v.cases[0],$v.cases[0])}
Reject 'wrong pair' {param($v) $v.cases[0].contact=$false}
Reject 'failed case' {param($v) $v.cases[0].passed=$false}
Reject 'failure under passed' {param($v) $v.cases[0].failure='queue full'}
Reject 'string comparison count' {param($v) $v.cases[0].fieldComparisons='196'}
Reject 'short scalar coverage' {param($v) $v.cases[0].scalarComparisons=139}
Reject 'short field coverage' {param($v) $v.cases[0].fieldComparisons=195}
Reject 'queue64' {param($v) $v.cases[0].maxQueue=64}
Reject 'fractional counter' {param($v) $v.cases[0].maxQueue=12.5}
Reject 'null counter' {param($v) $v.cases[0].hostFrames=$null}
Reject 'bool counter' {param($v) $v.cases[0].hostFrames=$true}
Reject '22s presented as48s' {param($v) $v.cases[0].inputSeconds=22}
Reject 'stopped clock' {param($v) $v.cases[0].clock=.03}
Reject 'clock ahead of elapsed time' {param($v) $v.cases[0].clock=100}
Reject 'unbounded drain' {param($v) $v.cases[0].drainSeconds=6}
Reject 'nonfinite measurement' {param($v) $v.cases[0].inputSeconds=[double]::NaN}
Reject 'wrong measured fps' {param($v) $v.cases[0].hostFramesPerSecond=150}
Reject 'capture count differs' {param($v) $v.cases[0].captures=3}
Reject 'bad capture duration' {param($v) $v.cases[0].captureMilliseconds=@(55,-1)}
Reject 'oversized captures' {param($v) $v.cases[0].captureBytes=257MB}
Reject 'polling disguised as ordinary' {param($v) $v.explicitPolling=$true}
Reject 'unreported polling' {param($v) $v.cases[0].receiptUpdates=1}
Reject 'ordinary report as candidate' {param($v) $v.explicitPolling=$false} 'PollingCandidate'
Reject 'candidate without updates' {param($v) $v.cases[0].velocityUpdates=0} 'PollingCandidate'
Reject 'candidate without timing' {param($v) $v.cases[0].pollingMilliseconds=0} 'PollingCandidate'
Reject 'post-capture disguised as polling' {param($v) $v.postCapturePump=$true} 'PollingCandidate'
Reject 'post-capture without flag' {param($v) $v.postCapturePump=$false} 'PostCaptureCandidate'
Reject 'post-capture flag string' {param($v) $v.postCapturePump='true'} 'PostCaptureCandidate'
Reject 'post-capture missing calls' {param($v) $v.cases[0].postCapturePumps=0} 'PostCaptureCandidate'
Reject 'post-capture count differs' {param($v) $v.cases[0].postCapturePumps=1} 'PostCaptureCandidate'
Reject 'post-capture fractional calls' {param($v) $v.cases[0].postCapturePumps=2.5} 'PostCaptureCandidate'
Reject 'post-capture without polling' {param($v) $v.explicitPolling=$false} 'PostCaptureCandidate'
Reject '20s measurement presented as48s' {param($v) $v.measurementOnly=$true}
Reject 'measurement flag missing' {param($v) $v.measurementOnly=$false} 'TelemetryMeasurement'
Reject 'measurement no actual snapshots' {param($v) $v.cases[0].performanceWindow=$null} 'TelemetryMeasurement'
Reject 'measurement invalid' {param($v) $v.cases[0].performanceWindow.valid=$false} 'TelemetryMeasurement'
Reject 'measurement foreign thread' {param($v) $v.cases[0].performanceWindow.threadMismatches=1} 'TelemetryMeasurement'
Reject 'measurement wrong counter type' {param($v) $v.cases[0].performanceWindow.invalid='0'} 'TelemetryMeasurement'
Reject 'measurement wrong phases' {param($v) $v.cases[0].performanceWindow.stageNames[0]='GPU time'} 'TelemetryMeasurement'
Reject 'measurement allocation not reached' {param($v) $v.cases[0].performanceWindow.stages[2].count=0} 'TelemetryMeasurement'
Reject 'measurement no capture stages' {param($v) $v.cases[0].captureStageCount=0} 'TelemetryMeasurement'
Reject 'measurement invalid capture timing' {param($v) $v.cases[0].captureRenderMilliseconds=[double]::NaN} 'TelemetryMeasurement'
Reject 'measurement missing GC deltas' {param($v) $v.cases[0].gcCollections=@()} 'TelemetryMeasurement'
Reject 'measurement negative GC delta' {param($v) $v.cases[0].gcCollections[1]=-1} 'TelemetryMeasurement'
Reject 'measurement completion count differs' {param($v) $v.cases[0].performanceWindow.flows[0].callbacks=9} 'TelemetryMeasurement'
Reject 'measurement requested bytes differ' {param($v) $v.cases[0].performanceWindow.flows[0].requestedBytes=640} 'TelemetryMeasurement'
Reject 'measurement copy bytes differ' {param($v) $v.cases[0].performanceWindow.flows[0].cpuCopiedBytes=20480} 'TelemetryMeasurement'
Reject 'measurement batch count differs' {param($v) $v.cases[0].performanceWindow.batches=9} 'TelemetryMeasurement'
Reject 'measurement reset in drain' {param($v) $v.cases[0].performanceDrained.stages[0].count=9} 'TelemetryMeasurement'
Reject 'reuse missing page accounting' {param($v) $v.cases[0].pagesWindow=$null} 'ReuseCandidate'
Reject 'reuse page type' {param($v) $v.cases[0].pagesWindow.active='28'} 'ReuseCandidate'
Reject 'reuse not bounded' {param($v) $v.cases[0].pagesWindow.peak=37} 'ReuseCandidate'
Reject 'reuse excess free pages' {param($v) $v.cases[0].pagesWindow.free=8} 'ReuseCandidate'
Reject 'reuse bad bytes' {param($v) $v.cases[0].pagesWindow.bytes=1} 'ReuseCandidate'
Reject 'reuse repeated allocations' {param($v) $v.cases[0].pagesWindow.allocations=100} 'ReuseCandidate'
Reject 'reuse growing after warmup' {param($v) $v.cases[0].pagesDrained.allocations=33; $v.cases[0].pagesDrained.peak=33} 'ReuseCandidate'
Reject 'reuse no reuse' {param($v) $v.cases[0].pagesWindow.reuses=0} 'ReuseCandidate'
Reject 'reuse short48' {param($v) $v.cases[0].inputSeconds=20} 'ReuseCandidate'
Reject 'async disguised as sync' {param($v) $v.asyncCapture=$true} 'ReuseCandidate'
Reject 'async missing flag' {param($v) $v.asyncCapture=$false} 'AsyncCaptureCandidate'
Reject 'async lost frame' {param($v) $v.cases[0].captureRequests=3} 'AsyncCaptureCandidate'
Reject 'async no reference' {param($v) $v.cases[0].captureReferenceComparisons=0} 'AsyncCaptureCandidate'
Reject 'async no copy' {param($v) $v.cases[0].captureCpuCopyMilliseconds=0} 'AsyncCaptureCandidate'
Reject 'async short48' {param($v) $v.cases[0].inputSeconds=20} 'AsyncCaptureCandidate'
Reject 'candidate mislabelled production' {param($v) $v.velocityPageReuseCandidate=$false} 'AsyncCaptureCandidate'
$stale=Receipt; $stale | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $path
(Get-Item -LiteralPath $path).LastWriteTimeUtc=$started.AddMinutes(-1)
try {Assert-FogProductionFramesEvidence $path $started 'NativeCadence' $id; throw 'STALE_ACCEPTED'}catch{if($_.Exception.Message -eq 'STALE_ACCEPTED'){throw}; $rejections++}
'{' | Set-Content -LiteralPath $path
try {Assert-FogProductionFramesEvidence $path $started 'NativeCadence' $id; throw 'BAD_JSON_ACCEPTED'}catch{if($_.Exception.Message -eq 'BAD_JSON_ACCEPTED'){throw}; $rejections++}
try {Assert-FogProductionFramesEvidence (Join-Path $directory 'absent.json') $started 'NativeCadence' $id; throw 'ABSENT_ACCEPTED'}catch{if($_.Exception.Message -eq 'ABSENT_ACCEPTED'){throw}; $rejections++}
$manifestAst=$ast.Find({param($node) $node -is [Management.Automation.Language.IfStatementAst] -and $node.Extent.Text.StartsWith('if($candidate.passed')},$true)
$manifestCheck=[scriptblock]::Create($manifestAst.Extent.Text)
$names=@('AccumulatedSmoke.compute','FogGhostVolume.shader','TexturedGhost.shader','GhostMatter.cginc','FogVolumePair.cginc','FogVolumeAtlas.cginc','FogPublished.cginc','FogArena.compute','FogSnapshotMetadata.compute')
function Manifest {@{passed=$true;shaderMessagesChecked=$true;colorSpace='Linear';sources=@($names | ForEach-Object {@{name=$_}});assets=5;kernels=28;arenaPublicationAndReload=$true}}
$candidate=Manifest; & $manifestCheck
foreach($invalid in @('duplicate','missing','stringFlag')){
    $candidate=Manifest
    switch($invalid){'duplicate'{$candidate.sources[8].name=$names[0]}'missing'{$candidate.sources=$candidate.sources[0..7]}'stringFlag'{$candidate.shaderMessagesChecked='false'}}
    $rejected=$false; try {& $manifestCheck}catch{$rejected=$true}
    if(!$rejected){throw ('Invalid shader manifest accepted: '+$invalid)}; $rejections++
}
@{passed=$true;syntheticValidatorOnly=$true;positiveModes=8;positiveManifests=1;rejected=$rejections;gpuStarted=$false;
  validatorSha256=(Get-FileHash -LiteralPath (Join-Path $root 'scripts/Test-FogProductionFrames.ps1')).Hash;
  suiteSha256=(Get-FileHash -LiteralPath $PSCommandPath).Hash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'verification.json')
Write-Output ('PASS production-frame receipt gate:8 modes/1 manifest/'+$rejections+' negative cases; CPU only. '+$directory)
