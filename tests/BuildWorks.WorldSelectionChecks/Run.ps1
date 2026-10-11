param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/PrecisionPlacementSession.cs'))
$start = $source.IndexOf('        private void SelectBlueprintArea(', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private bool IsBlueprintPieceSelectable(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'World selection method boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
Add-Type -TypeDefinition $harness.Replace('// PRODUCTION_METHOD', $source.Substring($start, $end - $start))
[WorldSelectionChecks]::Run()
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/WorldBlueprintVolumeSelection.cs'))
$start = $source.IndexOf('        private bool TryCommitBlueprintVolumeSelection(', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private static bool TryBlueprintVolumeZones(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Volume commit method boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VolumeCommitHarness.cs'))
foreach ($qa in @($false, $true)) {
    $name = if ($qa) { 'QaVolumeCommitChecks' } else { 'NormalVolumeCommitChecks' }
    $definition = $harness.Replace('// PRODUCTION_METHOD', $source.Substring($start, $end - $start)).Replace('VolumeCommitChecks', $name)
    if ($qa) { $definition = "#define BUILDWORKS_TEST_STAND`n" + $definition }
    Add-Type -TypeDefinition $definition
    $type = $name -as [type]
    $type::Run()
}
$start = $source.IndexOf('        private void HandleBlueprintVolumeInput(', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private void ToggleBlueprintVolumeRow(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Volume input method boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VolumeInputHarness.cs'))
Add-Type -TypeDefinition $harness.Replace('// PRODUCTION_METHOD', $source.Substring($start, $end - $start))
[VolumeInputChecks]::Run()
$start = $source.IndexOf('        private void AdjustBlueprintVolumeHeight(', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private bool TryBlueprintAreaPoint(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Volume height method boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'VolumeHudHarness.cs')).Replace('// HEIGHT_METHOD', $source.Substring($start, $end - $start))
$start = $source.IndexOf('        private void UpdateBlueprintVolumeHud(', [StringComparison]::Ordinal)
$end = $source.LastIndexOf("`n    }", [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Volume HUD method boundaries changed; update the offline harness.' }
Add-Type -TypeDefinition $harness.Replace('// HUD_METHOD', $source.Substring($start, $end - $start))
[VolumeHudChecks]::Run()
