param([switch]$Fog)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$taskRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskProject=Join-Path $taskRoot 'tools/BuildWorks.UiWorkbench'
if(Get-Process valheim,valheim_server,Unity -ErrorAction SilentlyContinue){throw 'Existing game/editor must not be interrupted.'}
if((Test-Path -LiteralPath (Join-Path $taskProject 'Assets/csc.rsp')) -or (Test-Path -LiteralPath (Join-Path $taskProject 'Assets/csc.rsp.meta'))){throw 'Unexpected compiler response/meta file.'}
if($Fog){
 $taskCandidate=Get-Content -LiteralPath (Join-Path $taskRoot 'artifacts/fog-runtime-assets-candidate/verification.json') -Raw | ConvertFrom-Json
 if($taskCandidate.passed -isnot [bool] -or !$taskCandidate.passed -or @($taskCandidate.sources).Count -ne 9){throw 'Candidate source manifest invalid.'}
 foreach($taskSource in $taskCandidate.sources){
  if((Get-FileHash -LiteralPath (Join-Path $taskRoot ('src/BuildWorks/Assets/FogGhostRuntime/'+$taskSource.name))).Hash -cne $taskSource.sha256){throw 'Candidate source changed; rebuild bundle.'}
 }
 if((Get-FileHash -LiteralPath (Join-Path $taskRoot 'artifacts/fog-runtime-assets-candidate/buildworks-fog-v11')).Hash -cne $taskCandidate.sha256){throw 'Candidate bundle differs.'}
 $taskMirrors=@(Get-ChildItem -LiteralPath (Join-Path $taskProject 'Assets/Editor/RuntimeSources') -Filter 'BlueprintFog*.cs')+
  @('FogPerformanceCounters.cs','BlueprintEditorMeshData.cs' | ForEach-Object {Get-Item -LiteralPath (Join-Path $taskProject ('Assets/Editor/RuntimeSources/'+$_))})
 foreach($taskMirror in $taskMirrors){
  if((Get-FileHash -LiteralPath $taskMirror.FullName).Hash -cne (Get-FileHash -LiteralPath (Join-Path $taskRoot ('src/BuildWorks/'+$taskMirror.Name))).Hash){throw ('Runtime mirror differs; rebuild candidate: '+$taskMirror.Name)}
 }
}
$taskOutput=Join-Path (Join-Path $taskRoot 'artifacts/fog-gpu-timing') ($(if($Fog){'fog-timing-'}else{'capability-'})+[datetime]::UtcNow.ToString('yyyyMMdd-HHmmss')+'-'+[guid]::NewGuid().ToString('N').Substring(0,8))
New-Item -ItemType Directory -Path $taskOutput -ErrorAction Stop > $null
$taskSettings=Join-Path $taskProject 'ProjectSettings/ProjectSettings.asset'
$taskBefore=[IO.File]::ReadAllBytes($taskSettings)
$taskSettingsHash=(Get-FileHash -LiteralPath $taskSettings).Hash
$taskRsp=Join-Path $taskProject 'Assets/csc.rsp'
$taskRspText="-define:BUILDWORKS_FOG_GPU_RESIDENT_CANDIDATE`n"
$taskProcess=$null
try {
 if($Fog){[IO.File]::WriteAllText($taskRsp,$taskRspText)}
 $taskMethod=if($Fog){'FogProductionFramesCheck.RunFogGpuTiming'}else{'FogProductionFramesCheck.RunGpuTimingCapability'}
 $taskArgs=@('-batchmode','-force-d3d11','-projectPath',('"'+$taskProject+'"'),'-executeMethod',$taskMethod,
  '-logFile',('"'+(Join-Path $taskOutput 'unity.log')+'"'),'-fogEvidenceOutput',('"'+$taskOutput+'"'))
 Get-Item -LiteralPath (Join-Path $taskProject 'Assets/Editor/FogProductionFramesCheck.cs'),(Join-Path $taskProject 'Assets/Editor/FogProductionFramesCheck.GpuTiming.cs') |
  ForEach-Object {@{name=$_.Name;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash}} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'sources.json')
 $taskProcess=Start-Process -FilePath 'C:/Program Files/Unity/Hub/Editor/6000.0.61f1/Editor/Unity.exe' -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
 @{pid=$taskProcess.Id;startedUtc=[datetime]::UtcNow.ToString('o')} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'process.json')
 if(!$taskProcess.WaitForExit(120000)){throw 'GPU capability deadline; retain own process and inspect before retry.'}
 @{pid=$taskProcess.Id;exitCode=$taskProcess.ExitCode} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'process-exit.json')
 if($taskProcess.ExitCode -ne 0){throw ('Unity failed; inspect unity.log: '+$taskOutput)}
 $taskProof=Get-Content -LiteralPath (Join-Path $taskOutput 'verification.json') -Raw | ConvertFrom-Json
 if($taskProof.passed -isnot [bool] -or !$taskProof.passed){throw ('GPU timing unavailable/failed: '+$taskOutput)}
 if($taskProof.resourcesReleased -isnot [bool] -or !$taskProof.resourcesReleased -or $taskProof.gameHostTested -isnot [bool] -or $taskProof.gameHostTested){throw 'GPU report scope/cleanup invalid.'}
 if($Fog){
  if(@($taskProof.rows).Count -ne 4 -or $taskProof.nativeServiceHeadroomProved -isnot [bool] -or $taskProof.nativeServiceHeadroomProved){throw 'Fog GPU timing scope invalid.'}
  for($taskIndex=0;$taskIndex -lt 4;++$taskIndex){
   $taskRow=$taskProof.rows[$taskIndex]
   $taskLight=if($taskIndex -eq 0 -or $taskIndex -eq 3){0.0}else{1.6}
   if(($taskRow.renderCalls -isnot [int] -and $taskRow.renderCalls -isnot [long]) -or $taskRow.renderCalls -ne 26 -or
      @($taskRow.samples).Count -ne 20 -or
      ($taskRow.beamStrength -isnot [int] -and $taskRow.beamStrength -isnot [long] -and $taskRow.beamStrength -isnot [double]) -or
      ![double]::IsFinite([double]$taskRow.beamStrength) -or [math]::Abs($taskRow.beamStrength-$taskLight) -gt 0.00001){throw 'Fog GPU timing coverage/order incomplete.'}
   foreach($taskSample in $taskRow.samples){
    if(($taskSample.gpuNanoseconds -isnot [int] -and $taskSample.gpuNanoseconds -isnot [long]) -or $taskSample.gpuNanoseconds -le 0 -or
       ($taskSample.blocks -isnot [int] -and $taskSample.blocks -isnot [long]) -or $taskSample.blocks -ne 1){throw 'Fog GPU sample invalid.'}
   }
   if(@($taskRow.samples.frame | Select-Object -Unique).Count -ne 20){throw 'GPU sample host frames repeated.'}
  }
  if(!$taskProof.resourcesReleased -or $taskProof.gameHostTested -or $taskProof.bundleSha256 -cne (Get-FileHash -LiteralPath (Join-Path $taskRoot 'artifacts/fog-runtime-assets-candidate/buildworks-fog-v11')).Hash){throw 'Fog GPU identity/cleanup invalid.'}
 } else {
 if($taskProof.submissions -isnot [long] -and $taskProof.submissions -isnot [int]){throw 'GPU submission counter invalid.'}
 if($taskProof.submissions -ne 36 -or @($taskProof.rows).Count -ne 30 -or $taskProof.nonzeroSamples -lt 8 -or
  !$taskProof.resourcesReleased -or $taskProof.gameHostTested -or $taskProof.fogGpuTimeMeasured){throw 'GPU capability scope incomplete.'}
 }
 Write-Output ('PASS GPU recorder '+$(if($Fog){'frozen fog timing'}else{'capability only'})+': '+$taskOutput)
} finally {
 if($null -eq $taskProcess -or $taskProcess.HasExited){
  [IO.File]::WriteAllBytes($taskSettings,$taskBefore)
  if((Get-FileHash -LiteralPath $taskSettings).Hash -cne $taskSettingsHash){throw 'Settings restoration differs.'}
  if($Fog){
   if([IO.File]::ReadAllText($taskRsp) -cne $taskRspText){throw 'Own compiler response differs.'}
   Remove-Item -LiteralPath $taskRsp
   if(Test-Path -LiteralPath ($taskRsp+'.meta')){Remove-Item -LiteralPath ($taskRsp+'.meta')}
  }
  @{settingsRestored=$true;sha256=$taskSettingsHash} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $taskOutput 'cleanup.json')
 } else {Write-Warning 'Own Unity still live; do not restore settings or relaunch blindly.'}
}
