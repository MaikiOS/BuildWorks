param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintExchangeView.cs'))
$start = $source.IndexOf('        internal void Tick()', [StringComparison]::Ordinal)
$end = $source.IndexOf('        private void UpdateImportProgress(', [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start) { throw 'Exchange Tick boundaries changed; update the offline harness.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
$batchSource = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintExchangeBatchView.cs'))
$batchStart = $batchSource.IndexOf('        private string BatchResultSummary(', [StringComparison]::Ordinal)
$batchEnd = $batchSource.IndexOf('        private void UpdateBatchControls(', [StringComparison]::Ordinal)
if ($batchStart -lt 0 -or $batchEnd -le $batchStart) { throw 'Batch summary boundaries changed; update the offline harness.' }
$harness = $harness.Replace('// PRODUCTION_BATCH_SUMMARY', $batchSource.Substring($batchStart, $batchEnd - $batchStart))
$disposeStart = $source.IndexOf('        public void Dispose()', [StringComparison]::Ordinal)
$disposeEnd = $source.LastIndexOf("`n    }", [StringComparison]::Ordinal)
if ($disposeStart -lt 0 -or $disposeEnd -le $disposeStart) { throw 'Dispose boundaries changed; update the offline harness.' }
$harness = $harness.Replace('// PRODUCTION_DISPOSE', $source.Substring($disposeStart, $disposeEnd - $disposeStart))
$backStart = $source.IndexOf('contentsBack = Button("ExchangeContentsBack"', [StringComparison]::Ordinal)
if ($backStart -lt 0) { throw 'Back handler not found.' }
$backStart = $source.IndexOf('{', $backStart)
$backEnd = $source.IndexOf('});', $backStart, [StringComparison]::Ordinal)
if ($backEnd -le $backStart) { throw 'Back handler boundaries changed; update the offline harness.' }
$harness = $harness.Replace('// PRODUCTION_BACK', $source.Substring($backStart, $backEnd - $backStart + 1))
Add-Type -TypeDefinition $harness.Replace('// PRODUCTION_METHOD', $source.Substring($start, $end - $start))
[ExchangeTickChecks]::Run()
$caller = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CallerHarness.cs'))
$editor = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintEditorController.cs'))
$hammer = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/UnifiedHammerCatalog.cs'))
foreach ($spec in @(
    @($editor, 'view.CatalogImportRequested +=', '}), () =>', '// EDITOR_CALLBACK'),
    @($hammer, 'frontCanvas: true, report: report, imported: imported =>', '});', '// HAMMER_CALLBACK')
)) {
    $callback = $spec[0].IndexOf($spec[1], [StringComparison]::Ordinal)
    if ($callback -lt 0) { throw "Caller start not found: $($spec[1])" }
    $callback = $spec[0].IndexOf('imported =>', $callback, [StringComparison]::Ordinal)
    $body = $spec[0].IndexOf('{', $callback)
    $finish = $spec[0].IndexOf($spec[2], $body, [StringComparison]::Ordinal)
    if ($callback -lt 0 -or $body -lt 0 -or $finish -le $body) { throw 'Import callback boundaries changed.' }
    $caller = $caller.Replace($spec[3], $spec[0].Substring($body, $finish - $body + 1))
}
$catalog = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintEditorCatalogView.cs'))
$catalogStart = $catalog.IndexOf('        internal bool ShowImportedCatalog(', [StringComparison]::Ordinal)
$catalogEnd = $catalog.IndexOf('        private static bool SameCatalogItem(', [StringComparison]::Ordinal)
if ($catalogStart -lt 0 -or $catalogEnd -le $catalogStart) { throw 'Imported catalog boundaries changed.' }
$caller = $caller.Replace('// CATALOG_METHOD', $catalog.Substring($catalogStart, $catalogEnd - $catalogStart))
$geometry = Join-Path $root 'artifacts/bin/BuildWorks.Geometry.dll'
$null = [Reflection.Assembly]::LoadFrom($geometry)
$references = @($geometry) + @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
Add-Type -ReferencedAssemblies $references -TypeDefinition $caller
[ExchangeCallerChecks]::Run()
