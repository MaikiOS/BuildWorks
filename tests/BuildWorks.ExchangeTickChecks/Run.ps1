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
$storeSource = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/CompositeBlueprintStore.cs'))
$constants = ''
foreach ($name in @('DefaultCategory', 'LegacyDefaultCategory')) {
    $constant = [regex]::Match($storeSource, '\bconst\s+string\s+' + $name + '\s*=\s*"[^"]*";')
    if (!$constant.Success) { throw "Category constant not found: $name" }
    $constants += $constant.Value + "`n"
}
$harness = $harness.Replace('// PRODUCTION_CATEGORY_CONSTANTS', $constants)
foreach ($spec in @(
    @($storeSource, '        private static string NormalizeCategory(', '        private static string UserError(', '// PRODUCTION_CATEGORY_NORMALIZATION'),
    @($batchSource, '        private static string BatchKey(', '        private void CreateBatchToggle(', '// PRODUCTION_BATCH_CATEGORIES'),
    @($batchSource, '        private void AppendBatchReview(', '        private void OpenBatchDocuments(', '// PRODUCTION_BATCH_PREVIEW'),
    @($source, '        private bool ImportCategoryIsValid()', '        private void UpdateCategoryControls(', '// PRODUCTION_CATEGORY_VALIDITY'),
    @($source, '        private void PrepareImportDocuments(', '        private void RefreshMissing(', '// PRODUCTION_PREPARE_DOCUMENTS')
)) {
    $begin = $spec[0].IndexOf($spec[1], [StringComparison]::Ordinal)
    if ($begin -lt 0) { throw "Batch preview method not found: $($spec[1])" }
    $finish = $spec[0].IndexOf($spec[2], $begin + 1, [StringComparison]::Ordinal)
    if ($finish -le $begin) { throw 'Batch preview method boundaries changed.' }
    $harness = $harness.Replace($spec[3], $spec[0].Substring($begin, $finish - $begin))
}
$apply = $batchSource.IndexOf('Button("ExchangeBatchPickerClose"', [StringComparison]::Ordinal)
if ($apply -lt 0) { throw 'Batch picker Apply callback not found.' }
$applyBody = $batchSource.IndexOf('{', $apply)
$applyEnd = $batchSource.IndexOf('}, batchPicker);', $applyBody, [StringComparison]::Ordinal)
if ($applyBody -lt 0 -or $applyEnd -le $applyBody) { throw 'Batch Apply callback boundaries changed.' }
$harness = $harness.Replace('// PRODUCTION_BATCH_APPLY', $batchSource.Substring($applyBody, $applyEnd - $applyBody + 1))
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
$exportStart = $editor.IndexOf('view.CatalogExportRequested += blueprint =>', [StringComparison]::Ordinal)
$exportBody = $editor.IndexOf('{', $exportStart)
$exportEnd = $editor.IndexOf('            };', $exportBody, [StringComparison]::Ordinal)
if ($exportStart -lt 0 -or $exportBody -lt 0 -or $exportEnd -le $exportBody) { throw 'Editor export callback boundaries changed.' }
$caller = $caller.Replace('// EDITOR_EXPORT_CALLBACK', $editor.Substring($exportBody, $exportEnd + '            }'.Length - $exportBody))
foreach ($spec in @(
    @('        private static void ExportBlueprintFile(', '        private static void ShowExchange(', '// HAMMER_EXPORT_BODY'),
    @('        private static void ShowExchange(', '        internal static bool ShowPlacementContents(', '// HAMMER_EXCHANGE_BODY')
)) {
    $method = $hammer.IndexOf($spec[0], [StringComparison]::Ordinal)
    $body = $hammer.IndexOf('{', $method)
    $finish = $hammer.IndexOf($spec[1], $body, [StringComparison]::Ordinal)
    if ($method -lt 0 -or $body -lt 0 -or $finish -le $body) { throw 'Hammer export method boundaries changed.' }
    $caller = $caller.Replace($spec[2], $hammer.Substring($body, $finish - $body))
}
$catalogStart = $catalog.IndexOf('        internal bool ShowImportedCatalog(', [StringComparison]::Ordinal)
$catalogEnd = $catalog.IndexOf('        private static bool SameCatalogItem(', [StringComparison]::Ordinal)
if ($catalogStart -lt 0 -or $catalogEnd -le $catalogStart) { throw 'Imported catalog boundaries changed.' }
$caller = $caller.Replace('// CATALOG_METHOD', $catalog.Substring($catalogStart, $catalogEnd - $catalogStart))
$geometry = Join-Path $root 'artifacts/bin/BuildWorks.Geometry.dll'
$null = [Reflection.Assembly]::LoadFrom($geometry)
$references = @($geometry) + @(Get-ChildItem -LiteralPath (Join-Path $PSHOME 'ref') -Filter '*.dll' | ForEach-Object FullName)
Add-Type -ReferencedAssemblies $references -TypeDefinition $caller
[ExchangeCallerChecks]::Run()
