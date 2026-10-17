[CmdletBinding()]
param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',[switch]$MeshOnly,[switch]$VisualOnly,[switch]$ColorOnly,[switch]$ChannelsOnly,[switch]$PairOnly,[switch]$CompactOnly,[switch]$OwnersOnly,[switch]$AtlasOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $root 'tools/BuildWorks.UiWorkbench'
if(@($MeshOnly,$VisualOnly,$ColorOnly,$ChannelsOnly,$PairOnly,$CompactOnly,$OwnersOnly,$AtlasOnly|Where-Object{$_}).Count -gt 1){throw 'Choose one fog check mode.'}
$output=Join-Path $root $(if($AtlasOnly){'artifacts/fog-runtime-atlas'}elseif($OwnersOnly){'artifacts/fog-runtime-owners'}elseif($CompactOnly){'artifacts/fog-runtime-compact'}elseif($PairOnly){'artifacts/fog-runtime-pair'}elseif($ChannelsOnly){'artifacts/fog-runtime-channels'}elseif($ColorOnly){'artifacts/fog-color-check'}elseif($VisualOnly){'artifacts/fog-runtime-visual'}elseif($MeshOnly){'artifacts/fog-runtime-mesh'}else{'artifacts/fog-runtime-simulation'})
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike '6000.0.61f1*') { throw 'Fog reference check requires Unity6000.0.61f1.' }
if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*'+$project+'*') }) { throw 'Workbench already open; do not replace its session.' }
$reference=Join-Path $project 'Assets/FogRuntimeReference/FogMatterSimulation.cs'
$bytes=[Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($reference).Replace('namespace BuildWorks.FogRuntimeReference','namespace BuildWorks.FogStudy'))
$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
if($hash -cne '7576245AB043F81F739F5232DD361D9BB56EBBEC9F49D0B30247AD0C01D87150'){throw 'Frozen simulation reference differs from approved v11.'}
$frozenCompute=Join-Path $project 'Assets/FogGhostLab/AccumulatedSmoke.compute'
$approved=Join-Path $root 'src/BuildWorks/Assets/FogGhostV11/AccumulatedSmoke.compute'
if((Get-FileHash -LiteralPath $frozenCompute).Hash -cne (Get-FileHash -LiteralPath $approved).Hash){throw 'Frozen compute reference differs.'}
foreach($name in @('BlueprintFogAssets.cs','BlueprintFogSimulation.cs','BlueprintEditorMeshData.cs','BlueprintFogContactMesh.cs','BlueprintFogSource.cs','BlueprintFogVisual.cs','BlueprintFogChannels.cs','BlueprintFogVolume.cs','BlueprintFogAtlas.cs')){
    $source=Join-Path $root ('src/BuildWorks/'+$name)
    $dest=Join-Path $project ('Assets/Editor/RuntimeSources/'+$name)
    Copy-Item -LiteralPath $source -Destination $dest
    if((Get-FileHash -LiteralPath $source).Hash -cne (Get-FileHash -LiteralPath $dest).Hash){throw 'Runtime source copy differs.'}
}
New-Item -ItemType Directory -Path $output -Force|Out-Null
$started=[datetime]::UtcNow
$method=if($AtlasOnly){'FogRuntimeAtlasCheck.Run'}elseif($OwnersOnly){'FogRuntimeOwnersCheck.Run'}elseif($CompactOnly){'FogRuntimeCompactCheck.Run'}elseif($PairOnly){'FogRuntimePairCheck.Run'}elseif($ChannelsOnly){'FogRuntimeChannelsCheck.Run'}elseif($ColorOnly){'FogRuntimeColorCheck.Run'}elseif($VisualOnly){'FogRuntimeVisualCheck.Run'}elseif($MeshOnly){'FogRuntimeMeshCheck.Run'}else{'FogRuntimeSimulationCheck.Run'}
$settings=Join-Path $project 'ProjectSettings/ProjectSettings.asset'
$settingsBytes=if($ColorOnly -or $PairOnly -or $OwnersOnly -or $AtlasOnly){[IO.File]::ReadAllBytes($settings)}else{$null}
try {
    $process=Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+(Join-Path $output 'unity.log')+'"')) -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){
        if(([datetime]::UtcNow-$started).TotalMinutes -gt 3){$process.Kill();$process.WaitForExit();throw 'Fog simulation timed out; stopped only its own Unity process.'}
    }
    if($process.ExitCode -ne 0){throw ('Fog simulation check failed: '+(Join-Path $output 'unity.log'))}
} finally { if($null -ne $settingsBytes){[IO.File]::WriteAllBytes($settings,$settingsBytes)} }
$proof=Join-Path $output 'verification.json'
if(!(Test-Path -LiteralPath $proof) -or (Get-Item -LiteralPath $proof).LastWriteTimeUtc -lt $started){throw 'Fresh fog simulation proof missing.'}
$result=Get-Content -LiteralPath $proof -Raw|ConvertFrom-Json
if(!$result.passed -or ($AtlasOnly -and ($result.frames -ne 4 -or $result.owners -ne 3 -or $result.houseLayoutBytes -le 0 -or $result.houseLayoutBytes -gt 134217728 -or !$result.layoutRolled -or !$result.allocationFailureRecovered -or !$result.packedFieldsExact -or !$result.guardsRetainSimulations -or !$result.cleanup -or $result.maxOrderDelta -ge .000001 -or $result.pairDelta -ge .00001 -or $result.removalDelta -ne 0 -or $result.missingOwnerDelta -le .001 -or $result.edgeDelta -ge .00001 -or $result.occlusionDelta -ne 0 -or $result.translatedDelta -ge .005)) -or ($OwnersOnly -and ($result.owners -ne 3 -or $result.ticks -ne 420 -or $result.frames -ne 3 -or $result.maxImageDelta -ge .005 -or $result.maxForegroundDelta -ge .03 -or $result.minimumEmptyDelta -le .03 -or $result.translatedDelta -ge .005 -or !$result.geometryRetained -or !$result.physicalDetailRetained -or !$result.pigmentIndependent -or !$result.removalIsolated -or !$result.budgetRejectBeforeFactory -or !$result.reservationMismatchDisposed -or !$result.largeRejected -or !$result.recoveredReleased)) -or ($CompactOnly -and ($result.gridX -ne 32 -or $result.gridY -ne 40 -or $result.gridZ -ne 36 -or $result.fields -ne 10 -or $result.ticks -ne 300 -or $result.fieldBytes -ne 3686400 -or !$result.mixedBudgetExact -or !$result.independentOwner -or !$result.recoveredReleased -or !$result.invalidRejected -or !$result.originalGridRetained)) -or ($PairOnly -and ($result.frames -ne 4 -or !$result.orderIndependent -or !$result.singleExact -or !$result.sequentialNegative -or !$result.removalIsolated -or !$result.pooledVisualGuards -or !$result.spanRejected -or !$result.opaqueDepthExact -or !$result.reusedOwnerRetained -or $result.colorSpace -cne 'Linear')) -or ($ChannelsOnly -and ($result.capacity -ne 2 -or $result.independentFieldComparisons -ne 6 -or $result.overlappingVoxels -le 0 -or !$result.inactiveOwnerUnchanged -or !$result.staleRecoveryRejected -or !$result.noEviction -or !$result.removalIsolated -or !$result.recoveredReleased -or !$result.emptySlotClean -or !$result.duplicateTransportRejected -or !$result.smokeOnlyRetained -or !$result.surfaceOnlyRetained -or !$result.factoryReentryRejected -or !$result.factoryDisposeRejected)) -or ($ColorOnly -and ($result.hostColorSpace -cne 'Linear' -or !$result.gammaHostRejected -or !$result.gammaMismatchReproduced -or $result.linearBundleDelta -ge .005)) -or ($VisualOnly -and ($result.colorSpace -cne 'Gamma' -or !$result.shapeEqual -or !$result.pigmentEqual -or !$result.noiseEqual -or $result.frames.Count -ne 6)) -or ($MeshOnly -and ($result.cases -ne 3 -or $result.checkedVertices -le 6)) -or (!$MeshOnly -and !$VisualOnly -and !$ColorOnly -and !$ChannelsOnly -and !$PairOnly -and !$CompactOnly -and !$OwnersOnly -and !$AtlasOnly -and ($result.ticks -ne 270 -or $result.exactFieldComparisons -ne 18 -or !$result.released -or !$result.invalidInputsRejected))){throw 'Fog verification evidence incomplete.'}
$bundle=if($VisualOnly){'artifacts/fog-runtime-assets-gamma/buildworks-fog-v11'}else{'src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11'}
@{ runtimeSha=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintFogSimulation.cs')).Hash; referenceSha=$hash; bundleSha=(Get-FileHash -LiteralPath (Join-Path $root $bundle)).Hash; completedAtUtc=[datetime]::UtcNow.ToString('o') }|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'sources.json')
$result|ConvertTo-Json
