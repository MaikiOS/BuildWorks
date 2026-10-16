[CmdletBinding()]
param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',[switch]$GammaReference)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $root 'tools/BuildWorks.UiWorkbench'
$output=Join-Path $root $(if($GammaReference){'artifacts/fog-runtime-assets-gamma'}else{'artifacts/fog-runtime-assets'})
$source=Join-Path $root 'src/BuildWorks/Assets/FogGhostV11'
$ported=Join-Path $root 'src/BuildWorks/Assets/FogGhostRuntime'
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike '6000.0.61f1*') { throw 'Approved shader build requires Unity6000.0.61f1.' }
if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*'+$project+'*') }) { throw 'Workbench already open; do not replace its session.' }
$runtime=Join-Path $project 'Assets/BuildWorksFogRuntime'
New-Item -ItemType Directory -Path $runtime,$output -Force|Out-Null
$expected=@('AccumulatedSmoke.compute','FogGhostVolume.shader','TexturedGhost.shader','GhostMatter.cginc')
$manifest=@(Get-Content -LiteralPath (Join-Path $source 'approved-sources.json') -Raw|ConvertFrom-Json)
if($manifest.Count -ne 4 -or (($manifest.name|Sort-Object) -join '|') -cne (($expected|Sort-Object) -join '|')){throw 'Approved shader source list differs.'}
foreach($entry in $manifest){
    $path=Join-Path $source $entry.name
    if((Get-FileHash -LiteralPath $path).Hash -cne $entry.sha256){throw ('Approved v11 source differs: '+$entry.name)}
    Copy-Item -LiteralPath (Join-Path $ported $entry.name) -Destination (Join-Path $runtime $entry.name)
}
foreach($name in @('BlueprintFogAssets.cs','BlueprintFogSimulation.cs','BlueprintEditorMeshData.cs','BlueprintFogContactMesh.cs','BlueprintFogSource.cs','BlueprintFogVisual.cs','BlueprintFogChannels.cs')){
    Copy-Item -LiteralPath (Join-Path $root ('src/BuildWorks/'+$name)) -Destination (Join-Path $project ('Assets/Editor/RuntimeSources/'+$name))
}
$proof=Join-Path $output 'verification.json'
$settings=Join-Path $project 'ProjectSettings/ProjectSettings.asset'
$settingsBytes=[IO.File]::ReadAllBytes($settings)
$started=[datetime]::UtcNow
$method=if($GammaReference){'FogRuntimeAssetBuild.RunGammaReference'}else{'FogRuntimeAssetBuild.Run'}
try {
    $process=Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+(Join-Path $output 'build.log')+'"')) -WindowStyle Hidden -PassThru
    while(!$process.WaitForExit(1000)){if(([datetime]::UtcNow-$started).TotalMinutes -gt 3){$process.Kill();$process.WaitForExit();throw 'Fog asset build timed out; stopped only its own Unity process.'}}
    if($process.ExitCode -ne 0){throw ('Fog bundle build/check failed: '+(Join-Path $output 'build.log'))}
} finally { [IO.File]::WriteAllBytes($settings,$settingsBytes) }
if(!(Test-Path -LiteralPath $proof) -or (Get-Item -LiteralPath $proof).LastWriteTimeUtc -lt $started){throw 'Fresh fog asset proof missing.'}
$result=Get-Content -LiteralPath $proof -Raw|ConvertFrom-Json
$bundle=Join-Path $output 'buildworks-fog-v11'
if(!$result.passed -or $result.colorSpace -cne $(if($GammaReference){'Gamma'}else{'Linear'}) -or $result.assets -ne 3 -or $result.kernels -ne 7 -or (Get-FileHash -LiteralPath $bundle).Hash -cne $result.sha256){throw 'Fog bundle evidence differs.'}
if(!$GammaReference){Copy-Item -LiteralPath $bundle -Destination (Join-Path $ported 'buildworks-fog-v11')}
$result|ConvertTo-Json
