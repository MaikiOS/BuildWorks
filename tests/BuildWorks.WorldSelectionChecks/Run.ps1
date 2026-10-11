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
