[CmdletBinding()]
param([string]$UnityPath='C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',[switch]$MeshOnly,[switch]$VisualOnly)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$project=Join-Path $root 'tools/BuildWorks.UiWorkbench'
if($MeshOnly -and $VisualOnly){throw 'Choose mesh or visual check.'}
$output=Join-Path $root $(if($VisualOnly){'artifacts/fog-runtime-visual'}elseif($MeshOnly){'artifacts/fog-runtime-mesh'}else{'artifacts/fog-runtime-simulation'})
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike '6000.0.61f1*') { throw 'Fog reference check requires Unity6000.0.61f1.' }
if (Get-CimInstance Win32_Process -Filter "Name = 'Unity.exe'" | Where-Object { $_.CommandLine -like ('*'+$project+'*') }) { throw 'Workbench already open; do not replace its session.' }
$reference=Join-Path $project 'Assets/FogRuntimeReference/FogMatterSimulation.cs'
$bytes=[Text.Encoding]::UTF8.GetBytes([IO.File]::ReadAllText($reference).Replace('namespace BuildWorks.FogRuntimeReference','namespace BuildWorks.FogStudy'))
$hash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes))
if($hash -cne '7576245AB043F81F739F5232DD361D9BB56EBBEC9F49D0B30247AD0C01D87150'){throw 'Frozen simulation reference differs from approved v11.'}
foreach($name in @('BlueprintFogAssets.cs','BlueprintFogSimulation.cs','BlueprintEditorMeshData.cs','BlueprintFogContactMesh.cs','BlueprintFogSource.cs','BlueprintFogVisual.cs')){
    $source=Join-Path $root ('src/BuildWorks/'+$name)
    $dest=Join-Path $project ('Assets/Editor/RuntimeSources/'+$name)
    Copy-Item -LiteralPath $source -Destination $dest
    if((Get-FileHash -LiteralPath $source).Hash -cne (Get-FileHash -LiteralPath $dest).Hash){throw 'Runtime source copy differs.'}
}
New-Item -ItemType Directory -Path $output -Force|Out-Null
$started=[datetime]::UtcNow
$method=if($VisualOnly){'FogRuntimeVisualCheck.Run'}elseif($MeshOnly){'FogRuntimeMeshCheck.Run'}else{'FogRuntimeSimulationCheck.Run'}
$process=Start-Process -FilePath $UnityPath -ArgumentList @('-batchmode','-force-d3d11','-projectPath',('"'+$project+'"'),'-executeMethod',$method,'-quit','-logFile',('"'+(Join-Path $output 'unity.log')+'"')) -WindowStyle Hidden -PassThru
while(!$process.WaitForExit(1000)){
    if(([datetime]::UtcNow-$started).TotalMinutes -gt 3){$process.Kill();$process.WaitForExit();throw 'Fog simulation timed out; stopped only its own Unity process.'}
}
if($process.ExitCode -ne 0){throw ('Fog simulation check failed: '+(Join-Path $output 'unity.log'))}
$proof=Join-Path $output 'verification.json'
if(!(Test-Path -LiteralPath $proof) -or (Get-Item -LiteralPath $proof).LastWriteTimeUtc -lt $started){throw 'Fresh fog simulation proof missing.'}
$result=Get-Content -LiteralPath $proof -Raw|ConvertFrom-Json
if(!$result.passed -or ($VisualOnly -and (!$result.shapeEqual -or !$result.pigmentEqual -or !$result.noiseEqual -or $result.frames.Count -ne 6)) -or ($MeshOnly -and ($result.cases -ne 3 -or $result.checkedVertices -le 6)) -or (!$MeshOnly -and !$VisualOnly -and ($result.ticks -ne 270 -or $result.exactFieldComparisons -ne 18 -or !$result.released -or !$result.invalidInputsRejected))){throw 'Fog verification evidence incomplete.'}
@{ runtimeSha=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintFogSimulation.cs')).Hash; referenceSha=$hash; bundleSha=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/Assets/FogGhostRuntime/buildworks-fog-v11')).Hash; completedAtUtc=[datetime]::UtcNow.ToString('o') }|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $output 'sources.json')
$result|ConvertTo-Json
