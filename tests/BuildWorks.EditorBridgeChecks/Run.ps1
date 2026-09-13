param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$BuildWorksRoot = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$referenceRoot = 'C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.7.2'
$output = Join-Path $BuildWorksRoot 'artifacts\fix-0.19.22\editor-bridge-checks'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$geometryDll = Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.Geometry.dll'
$executable = Join-Path $output 'EditorBridgeChecks.exe'
& dotnet 'C:\Program Files\dotnet\sdk\10.0.201\Roslyn\bincore\csc.dll' -nologo -target:exe '-langversion:latest' "-out:$executable" '-nostdlib+' "-reference:$referenceRoot\mscorlib.dll" "-reference:$referenceRoot\System.dll" "-reference:$referenceRoot\System.Core.dll" "-reference:$referenceRoot\Facades\netstandard.dll" "-reference:$geometryDll" (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'Editor bridge check compile failed.' }
Copy-Item -LiteralPath $geometryDll -Destination $output -Force
& $executable $BuildWorksRoot $output 2>&1 | Tee-Object -LiteralPath (Join-Path $output 'result.txt')
if ($LASTEXITCODE -ne 0) { throw 'Editor bridge check failed.' }
