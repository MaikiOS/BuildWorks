param([string] $BuildWorksRoot = (Join-Path $PSScriptRoot '..\..'))
$ErrorActionPreference = 'Stop'
$deploy = Join-Path (Resolve-Path -LiteralPath $BuildWorksRoot).Path 'scripts\Deploy-TestBuild.ps1'
foreach ($target in @('Default', 'Default-Compat-Test', 'TerrainRamp-Test', '..\TerrainRamp-1.0-Test')) {
    $rejected = $false
    try { & $deploy -TargetProfile $target -WhatIf }
    catch {
        if ($_.Exception.Message -notmatch 'Refusing to deploy BuildWorks into Default|may target only TerrainRamp-1.0-Test') {
            throw
        }
        $rejected = $true
    }
    if (-not $rejected) { throw "Forbidden profile was accepted: $target" }
    Write-Output "PASS: deployment refuses $target before any profile writes"
}
