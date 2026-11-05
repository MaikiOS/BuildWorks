param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'), [string]$SourceRef)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
if ($SourceRef) {
    $lines = git -C $root show "${SourceRef}:src/BuildWorks/BlueprintEditorController.cs"
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read requested production revision.' }
    $source = $lines -join "`n"
} else {
    $source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintEditorController.cs'))
}
$start = $source.IndexOf('        private bool FinishPointerWarp(', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private void BeginKeyboardPreview(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Pointer warp method boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
Add-Type -TypeDefinition $harness.Replace('// PRODUCTION_METHOD', $source.Substring($start, $end - $start))
[PointerWarpChecks]::Run()
