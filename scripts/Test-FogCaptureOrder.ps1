param([string]$Assembly = (Join-Path $PSScriptRoot '../artifacts/bin/BuildWorks.dll'), [switch]$QaCompositeObserver)
$ErrorActionPreference = 'Stop'
$profile = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../TerrainRamp-1.0-Test'))
Add-Type -LiteralPath (Join-Path $profile 'BepInEx/core/Mono.Cecil.dll')
$module = [Mono.Cecil.ModuleDefinition]::ReadModule([IO.Path]::GetFullPath($Assembly))
try {
    $update = $module.GetType('OstrixMods.BuildWorks.BlueprintConstructionProjectFog').Methods | Where-Object Name -eq 'Update'
    $calls = @($update.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { $_.Operand.FullName })
    $service = [array]::LastIndexOf($calls, 'System.Void OstrixMods.BuildWorks.BlueprintFogChannels::PumpSources()')
    $capture = [array]::IndexOf($calls, 'System.Void OstrixMods.BuildWorks.BlueprintFogBuilding::AdvanceTo(System.Single,UnityEngine.Vector3,UnityEngine.Vector2,System.Boolean)')
    if ($service -lt 0 -or $capture -lt 0 -or $service -ge $capture) { throw 'Project fog must service accepted receipts before capturing another observation.' }
    $atlas = $module.GetType('OstrixMods.BuildWorks.BlueprintFogAtlas')
    $hook = @($atlas.Fields | Where-Object Name -eq 'QaCompositeObserver')
    $render = $atlas.Methods | Where-Object Name -eq 'RenderImage'
    $reads = @($render.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.FieldReference] -and $_.Operand.Name -eq 'QaCompositeObserver' })
    if ($hook.Count -ne [int]$QaCompositeObserver.IsPresent -or $reads.Count -ne [int]$QaCompositeObserver.IsPresent) { throw 'QA composite observer escaped or disappeared from its build boundary.' }
    [pscustomobject]@{passed=$true; assembly=[IO.Path]::GetFullPath($Assembly); serviceCall=$service; captureCall=$capture;qaObserver=$QaCompositeObserver.IsPresent} | ConvertTo-Json
} finally { $module.Dispose() }
