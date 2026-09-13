param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$BuildWorksRoot = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$output = Join-Path $BuildWorksRoot 'artifacts\fix-0.19.22\world-layout-checks'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$geometryDll = Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.Geometry.dll'
$executable = Join-Path $output 'WorldLayoutChecks.dll'
$references = Get-ChildItem -LiteralPath 'C:\Program Files\dotnet\packs\Microsoft.NETCore.App.Ref\10.0.5\ref\net10.0' -Filter '*.dll' |
    ForEach-Object { '-reference:' + $_.FullName }
& dotnet 'C:\Program Files\dotnet\sdk\10.0.201\Roslyn\bincore\csc.dll' -nologo -target:exe '-langversion:latest' "-out:$executable" '-nostdlib+' @references "-reference:$geometryDll" (Join-Path $PSScriptRoot 'Program.cs')
if ($LASTEXITCODE -ne 0) { throw 'World layout check compile failed.' }
Copy-Item -LiteralPath $geometryDll -Destination $output -Force
'{"runtimeOptions":{"tfm":"net10.0","framework":{"name":"Microsoft.NETCore.App","version":"10.0.0"}}}' |
    Set-Content -LiteralPath (Join-Path $output 'WorldLayoutChecks.runtimeconfig.json')
& dotnet $executable $BuildWorksRoot 2>&1 | Tee-Object -LiteralPath (Join-Path $output 'result.txt')
if ($LASTEXITCODE -ne 0) { throw 'World layout check failed.' }
Get-FileHash -LiteralPath (Join-Path $BuildWorksRoot 'artifacts\bin\BuildWorks.dll') -Algorithm SHA256 |
    Select-Object Path,Hash | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'assembly-hash.json')
