param([string]$SourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BuildWorksPlacementValidation.cs'))
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$output = Join-Path $root 'artifacts/placement-contact-cpu'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath))
$statusPath = Join-Path $output 'verification-status.json'
$sourceHash = (Get-FileHash -LiteralPath $SourcePath).Hash
@{ status = 'not_passed'; sourceSHA256 = $sourceHash; unityPhysicsTested = $false; gameTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath $statusPath -Encoding utf8
$aliases = @('GameObject','Transform','Renderer','Collider','BoxCollider','MeshCollider','Physics','RaycastHit','Quaternion','QueryTriggerInteraction') |
    ForEach-Object { "using $_ = ContactHost.$_;" }
$generated = Join-Path $output 'BuildWorksPlacementValidation.cs'
[IO.File]::WriteAllText($generated, ($aliases -join "`n") + "`n" + $source, [Text.UTF8Encoding]::new($false))
& dotnet build (Join-Path $PSScriptRoot 'BuildWorks.PlacementContactChecks.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'CPU contact harness compilation failed.' }
& (Join-Path $PSScriptRoot 'bin/Release/net472/BuildWorks.PlacementContactChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'CPU contact regression failed. Host substitutes do not prove native physics.' }
@{ status = 'passed_cpu_host_substitutes'; sourceSHA256 = $sourceHash;
    unityPhysicsTested = $false; gameTested = $false } | ConvertTo-Json |
    Set-Content -LiteralPath $statusPath -Encoding utf8
