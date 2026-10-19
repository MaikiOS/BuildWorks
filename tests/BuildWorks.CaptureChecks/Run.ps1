param([string]$ReportPath='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$geometry=Join-Path $root 'artifacts/bin/BuildWorks.Geometry.dll'
$null=[Reflection.Assembly]::LoadFrom($geometry)
$references=@($geometry)+@(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
$null=Add-Type -ReferencedAssemblies $references -Path @(
    (Join-Path $root 'src/BuildWorks/BlueprintItemStandCapture.cs'),
    (Join-Path $root 'src/BuildWorks/BlueprintArmorStandCapture.cs'),
    (Join-Path $PSScriptRoot 'ItemStandCaptureHost.cs'))
$count=[CaptureChecks.Runner]::Run()
if($count -ne 28){throw ('Capture regression coverage incomplete: '+$count)}
if($ReportPath){
    @{passed=$true;checks=$count;actualSourceCompiled=$true;fakeHost=$true;gameHostTested=$false;
      sourceSha256=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintItemStandCapture.cs')).Hash;
      armorSourceSha256=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintArmorStandCapture.cs')).Hash;
      fixtureSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ItemStandCaptureHost.cs')).Hash} |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
Write-Output ('PASS actual ItemStand/ArmorStand capture sources: '+$count+' snapshot/identity checks; fake host, CPU only, no Unity/native calls.')
