[CmdletBinding()]
param([string]$ReportPath='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source=Join-Path $root 'src/BuildWorks/BlueprintFogAtlas.cs'
$hostFile=Join-Path $PSScriptRoot 'FogAtlasLayoutHost.cs'
$checksFile=Join-Path $PSScriptRoot 'FogAtlasLayoutChecks.cs'
Add-Type -Path @($source,$hostFile,$checksFile)
$count=[FogAtlasChecks.Runner]::Run()
if($count -ne 47){throw "Fog layout regression coverage incomplete: $count"}
if($ReportPath){
    @{passed=$true;checks=$count;wholeProductionAtlasCompiled=$true;fakeUnityAndGpu=$true;nativeVisualTested=$false;
      sourceSha256=(Get-FileHash -LiteralPath $source).Hash;
      hostSha256=(Get-FileHash -LiteralPath $hostFile).Hash;
      checksSha256=(Get-FileHash -LiteralPath $checksFile).Hash} | ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
"PASS fog layout: $count assertions on whole production Atlas; inert Unity/GPU boundary, not native/visual proof."
