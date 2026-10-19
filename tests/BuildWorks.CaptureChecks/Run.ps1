param([string]$ReportPath='')
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$geometry=Join-Path $root 'artifacts/bin/BuildWorks.Geometry.dll'
$null=[Reflection.Assembly]::LoadFrom($geometry)
$source=Get-Content -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintItemStandCapture.cs') -Raw
$fixture=Get-Content -LiteralPath (Join-Path $PSScriptRoot 'ItemStandCaptureHost.cs') -Raw
$references=@($geometry)+@(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
$null=Add-Type -ReferencedAssemblies $references -TypeDefinition ($source+[Environment]::NewLine+$fixture)
$count=[CaptureChecks.Runner]::Run()
if($count -ne 11){throw 'Capture regression coverage incomplete.'}
if($ReportPath){
    @{passed=$true;checks=$count;actualSourceCompiled=$true;fakeHost=$true;gameHostTested=$false;
      sourceSha256=(Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks/BlueprintItemStandCapture.cs')).Hash;
      fixtureSha256=(Get-FileHash -LiteralPath (Join-Path $PSScriptRoot 'ItemStandCaptureHost.cs')).Hash} |
        ConvertTo-Json | Set-Content -LiteralPath $ReportPath
}
Write-Output ('PASS actual ItemStand capture source: '+$count+' snapshot/identity checks; fake host, CPU only, no Unity/native calls.')
