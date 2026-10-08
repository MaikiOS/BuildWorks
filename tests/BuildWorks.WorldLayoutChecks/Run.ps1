param(
    [string]$BuildWorksRoot = (Join-Path $PSScriptRoot '..\..'),
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.0.61f1\Editor\Unity.exe',
    [string]$ProjectPath = ''
)
$ErrorActionPreference = 'Stop'
$BuildWorksRoot = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$output = Join-Path $BuildWorksRoot 'artifacts\fix-0.19.22\world-layout-checks'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$geometryDll = Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.Geometry.dll'
$executable = Join-Path $output 'WorldLayoutChecks.exe'
$referenceRoot = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$unityCore = Join-Path (Split-Path $UnityPath) 'Data\Managed\UnityEngine\UnityEngine.CoreModule.dll'
if (-not $ProjectPath) { $ProjectPath = Join-Path $BuildWorksRoot 'tools\BuildWorks.UiWorkbench' }
$ProjectPath = [IO.Path]::GetFullPath($ProjectPath)
if ((Split-Path -Parent (Split-Path -Parent $ProjectPath)) -ine $BuildWorksRoot -or
    (Split-Path -Leaf (Split-Path -Parent $ProjectPath)) -notin @('tools', 'artifacts') -or
    -not (Test-Path -LiteralPath (Join-Path $ProjectPath 'Assets\Editor\RuntimeEditorAcceptance.cs'))) {
    throw 'Use only the owned BuildWorks Workbench or its local artifact copy.'
}
if ((Get-Item -LiteralPath $UnityPath).VersionInfo.ProductVersion -notlike '6000.0.61f1*') {
    throw 'World layout checks require Unity 6000.0.61f1.'
}
& dotnet 'C:\Program Files\dotnet\sdk\10.0.201\Roslyn\bincore\csc.dll' -nologo -target:exe '-langversion:latest' "-out:$executable" '-nostdlib+' "-reference:$referenceRoot\mscorlib.dll" "-reference:$referenceRoot\System.dll" "-reference:$referenceRoot\System.Core.dll" "-reference:$referenceRoot\Facades\netstandard.dll" "-reference:$geometryDll" "-reference:$unityCore" (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'World layout check compile failed.' }
Copy-Item -LiteralPath $geometryDll -Destination $output -Force
$runner = Join-Path $BuildWorksRoot 'tools\BuildWorks.UiWorkbench\Assets\Editor\WorldLayoutRunner.cs'
$targetRunner = Join-Path $ProjectPath 'Assets\Editor\WorldLayoutRunner.cs'
if ($runner -ine $targetRunner) { Copy-Item -LiteralPath $runner -Destination $targetRunner -Force }
$hash = (Get-FileHash -LiteralPath (Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.dll')).Hash
$log = Join-Path $output 'unity.log'
$started = [DateTime]::UtcNow
$arguments = "-batchmode -nographics -projectPath `"$ProjectPath`" -executeMethod WorldLayoutRunner.Run -buildWorksRoot `"$BuildWorksRoot`" -buildWorksSha $hash -logFile `"$log`""
$process = Start-Process -FilePath $UnityPath -ArgumentList $arguments -WindowStyle Hidden -PassThru
while (-not $process.WaitForExit(1000)) {
    if (([DateTime]::UtcNow - $started).TotalMinutes -gt 10) {
        $process.Kill()
        throw "World layout Unity process timed out: $log"
    }
}
$result = Get-Item -LiteralPath (Join-Path $output 'result.txt')
Get-Content -LiteralPath $result.FullName
if ($process.ExitCode -ne 0 -or $result.LastWriteTimeUtc -lt $started -or
    -not (Select-String -LiteralPath $result.FullName -SimpleMatch "BUILDWORKS_WORLD_LAYOUT_OK $hash" -Quiet)) {
    throw "World layout check failed: $log"
}
Get-FileHash -LiteralPath (Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.dll') -Algorithm SHA256 |
    Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'assembly-hash.json')
