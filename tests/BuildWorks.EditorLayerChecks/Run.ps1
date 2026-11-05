param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$source = [IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/BlueprintEditorScene.cs'))
$start = $source.IndexOf('        private static int FindUnusedEditorLayer()', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private static void DestroyVisual(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Editor layer method boundaries changed.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
Add-Type -TypeDefinition $harness.Replace('// PRODUCTION_METHODS', $source.Substring($start, $end - $start))
[EditorLayerChecks]::Run()
