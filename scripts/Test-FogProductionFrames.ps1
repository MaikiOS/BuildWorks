[CmdletBinding()]
param(
    [ValidateSet('Controls','NativeCadence','NativeControls','PollingCandidate','PostCaptureCandidate')][string]$Mode='Controls',
    [string]$UnityPath='C:/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe'
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest

# Pure receipt gate; CPU tests extract this function without launching Unity.
function Assert-FogProductionFramesEvidence([string]$Path,[datetime]$StartedUtc,[string]$ExpectedMode,[string]$RunId) {
    if(!(Test-Path -LiteralPath $Path -PathType Leaf) -or (Get-Item -LiteralPath $Path).LastWriteTimeUtc -lt $StartedUtc){throw 'Fresh production-frame report missing.'}
    $result=Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    function Number($value,[bool]$integer=$false) {
        ($null -ne $value) -and ($value.GetType() -in @([int],[long],[double],[single],[decimal])) -and
        [double]::IsFinite([double]$value) -and (!$integer -or $value.GetType() -in @([int],[long]))
    }
    $postCapture=$ExpectedMode -ceq 'PostCaptureCandidate'
    $polling=$postCapture -or $ExpectedMode -ceq 'PollingCandidate'
    $native=$ExpectedMode -cne 'Controls'; $duration=if($native){48}else{22}
    $pairs=@(switch($ExpectedMode){
        'Controls' {@('False:False','True:False','False:True','True:True')}
        'NativeControls' {@('True:False','False:True')}
        'NativeCadence' {@('True:True')}
        'PollingCandidate' {@('True:True')}
        'PostCaptureCandidate' {@('True:True')}
        default {throw 'Unknown production-frame mode.'}
    })
    if($result.passed -isnot [bool] -or !$result.passed -or $result.mode -cne $ExpectedMode -or $result.runId -cne $RunId -or
       $result.explicitPolling -isnot [bool] -or $result.explicitPolling -ne $polling -or
       $result.postCapturePump -isnot [bool] -or $result.postCapturePump -ne $postCapture -or
       !(Number $result.requestedFrameRate $true) -or $result.requestedFrameRate -ne $(if($native){36}else{-1}) -or
       $result.configuration -cne '32slots/32768blocks/Spatial3D/4resident;28procedural walls'){throw 'Production-frame identity/configuration failed.'}
    foreach($flag in @('gameHostTested','nativeHouseGeometryTested','gpuTimingsMeasured')){
        if($result.$flag -isnot [bool] -or $result.$flag){throw ('Unsupported production-frame scope: '+$flag)}
    }
    if(@($result.cases).Count -ne $pairs.Count){throw 'Production-frame case count differs.'}
    $seen=[Collections.Generic.HashSet[string]]::new()
    foreach($case in $result.cases){
        if($case.passed -isnot [bool] -or !$case.passed -or ![string]::IsNullOrEmpty($case.failure) -or
           $case.contact -isnot [bool] -or $case.capture -isnot [bool]){throw 'Production-frame case failed.'}
        $pair=('{0}:{1}' -f $case.contact,$case.capture)
        if($pair -cnotin $pairs -or !$seen.Add($pair)){throw 'Wrong or repeated production-frame case.'}
        foreach($key in @('scalarComparisons','fieldComparisons','hostFrames','maxQueue','captures','captureBytes','receiptUpdates','velocityUpdates','recoveryUpdates','postCapturePumps')){
            if(!(Number $case.$key $true) -or $case.$key -lt 0){throw ('Invalid production-frame counter: '+$key)}
        }
        foreach($key in @('inputSeconds','clock','drainSeconds','hostFramesPerSecond','pollingMilliseconds','maxPollingMilliseconds')){
            if(!(Number $case.$key) -or $case.$key -lt 0){throw ('Invalid production-frame measurement: '+$key)}
        }
        if($case.scalarComparisons -ne 140 -or $case.fieldComparisons -ne 196 -or $case.maxQueue -ge 64 -or
           $case.inputSeconds -lt $duration -or $case.inputSeconds -gt $duration+5 -or $case.clock -lt $duration-.5 -or
           $case.clock -gt $case.inputSeconds+.5 -or $case.drainSeconds -gt 5 -or $case.hostFrames -lt 1 -or
           [math]::Abs($case.hostFramesPerSecond-$case.hostFrames/$case.inputSeconds) -gt .001 -or ($native -and $case.hostFramesPerSecond -gt 40)){
            throw 'Production-frame coverage/cadence incomplete.'
        }
        $updates=$case.receiptUpdates+$case.velocityUpdates+$case.recoveryUpdates
        if(($postCapture -and ($case.postCapturePumps -ne $case.captures -or $case.postCapturePumps -lt 1)) -or
           (!$postCapture -and $case.postCapturePumps -ne 0)){throw 'Post-capture servicing discriminator/counters differ.'}
        if(($polling -and ($case.receiptUpdates -lt 1 -or $case.velocityUpdates -lt 1 -or $case.pollingMilliseconds -le 0)) -or
           (!$polling -and ($updates -ne 0 -or $case.pollingMilliseconds -ne 0 -or $case.maxPollingMilliseconds -ne 0)) -or
           $case.maxPollingMilliseconds -gt $case.pollingMilliseconds){throw 'Explicit polling discriminator/counters differ.'}
        if(@($case.captureMilliseconds).Count -ne $case.captures -or $case.captureBytes -gt 256MB -or
           ($case.capture -and ($case.captures -lt 1 -or $case.captureBytes -lt 1)) -or
           (!$case.capture -and ($case.captures -ne 0 -or $case.captureBytes -ne 0))){throw 'Capture evidence incomplete.'}
        foreach($elapsed in $case.captureMilliseconds){if(!(Number $elapsed) -or $elapsed -le 0){throw 'Invalid capture timing.'}}
    }
}

$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $root 'tools/BuildWorks.UiWorkbench'
if(Get-Process valheim,valheim_server,Unity -ErrorAction SilentlyContinue){throw 'Existing game/Unity session; GPU diagnostic not started.'}
if((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike '6000.0.61f1*'){throw 'Production-frame check requires Unity6000.0.61f1.'}
$candidate=Get-Content -LiteralPath (Join-Path $root 'artifacts/fog-runtime-assets-candidate/verification.json') -Raw | ConvertFrom-Json
$names=@('AccumulatedSmoke.compute','FogGhostVolume.shader','TexturedGhost.shader','GhostMatter.cginc','FogVolumePair.cginc','FogVolumeAtlas.cginc','FogPublished.cginc','FogArena.compute','FogSnapshotMetadata.compute')
if($candidate.passed -isnot [bool] -or !$candidate.passed -or $candidate.shaderMessagesChecked -isnot [bool] -or !$candidate.shaderMessagesChecked -or
   $candidate.colorSpace -cne 'Linear' -or @($candidate.sources).Count -ne 9 -or $candidate.assets -ne 5 -or $candidate.kernels -ne 28 -or
   $candidate.arenaPublicationAndReload -isnot [bool] -or !$candidate.arenaPublicationAndReload -or
   (($candidate.sources.name | Sort-Object)-join '|') -cne (($names | Sort-Object)-join '|')){throw 'Candidate shader build proof incomplete.'}
foreach($entry in $candidate.sources){
    if((Get-FileHash -LiteralPath (Join-Path $root ('src/BuildWorks/Assets/FogGhostRuntime/'+$entry.name))).Hash -cne $entry.sha256){throw 'Candidate shader source changed; rebuild before testing.'}
}
if((Get-FileHash -LiteralPath (Join-Path $root 'artifacts/fog-runtime-assets-candidate/buildworks-fog-v11')).Hash -cne $candidate.sha256){throw 'Candidate bundle hash differs.'}
$inputs=@(Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Editor/RuntimeSources') -Filter 'BlueprintFog*.cs')+
    @(Get-Item -LiteralPath (Join-Path $project 'Assets/Editor/RuntimeSources/BlueprintEditorMeshData.cs'))
foreach($runtimeFile in $inputs){
    if((Get-FileHash -LiteralPath $runtimeFile.FullName).Hash -cne (Get-FileHash -LiteralPath (Join-Path $root ('src/BuildWorks/'+$runtimeFile.Name))).Hash){throw ('Runtime mirror differs: '+$runtimeFile.Name)}
}
$id=[guid]::NewGuid().ToString('N')
$output=Join-Path $root ('artifacts/fog-production-frames/'+$Mode+'-'+[datetime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+$id.Substring(0,8))
New-Item -ItemType Directory -Path $output | Out-Null
Get-ChildItem -LiteralPath (Join-Path $project 'Assets/Editor') -Filter '*.cs' -Recurse | ForEach-Object {
    @{file=[IO.Path]::GetRelativePath($project,$_.FullName);sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}
} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'sources.json')
$method=switch($Mode){'Controls'{'Run'}'NativeCadence'{'RunNativeCadence'}'NativeControls'{'RunNativeControls'}'PollingCandidate'{'RunPollingCandidate'}'PostCaptureCandidate'{'RunPostCaptureCandidate'}}
$settings=Join-Path $project 'ProjectSettings/ProjectSettings.asset'; $saved=[IO.File]::ReadAllBytes($settings)
$process=$null; $started=[datetime]::UtcNow
try {
    $arguments=@('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',('FogProductionFramesCheck.'+$method),
        '-logFile',('"'+(Join-Path $output 'unity.log')+'"'),'-fogEvidenceId',$id,'-fogEvidenceOutput',('"'+$output+'"'))
    $process=Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
    @{pid=$process.Id;runId=$id;mode=$Mode;startedUtc=$started.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process.json')
    while(!$process.WaitForExit(1000)){
        if(([datetime]::UtcNow-$started).TotalMinutes -gt 12){throw ('Diagnostic deadline exceeded; own PID'+$process.Id+' remains unconfirmed. Inspect before another launch; output='+$output)}
    }
    @{pid=$process.Id;exitCode=$process.ExitCode;completedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'process-exit.json')
    if($process.ExitCode -ne 0){throw ('Unity diagnostic failed; evidence retained: '+$output)}
    Assert-FogProductionFramesEvidence (Join-Path $output 'verification.json') $started $Mode $id
    Write-Output ('PASS production-frame diagnostic only; no Valheim/native-house acceptance: '+$output)
} finally {
    if($null -eq $process -or $process.HasExited){[IO.File]::WriteAllBytes($settings,$saved)}
    else {Write-Warning ('Unity PID'+$process.Id+' still live; settings cleanup pending. No forced termination.')}
}
