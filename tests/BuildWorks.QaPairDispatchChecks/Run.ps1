param([string]$AfterInputSource = (Join-Path $PSScriptRoot '../../src/BuildWorks/BuildWorksQaControl.cs'))
$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../src/BuildWorks/BuildWorksQaControl.cs'))
$dispatch=[IO.File]::ReadAllText($AfterInputSource)
function Slice([string]$text,[string]$start,[string]$end) {
 $a=$text.IndexOf($start,[StringComparison]::Ordinal); $b=$text.IndexOf($end,$a,[StringComparison]::Ordinal)
 if($a -lt 0 -or $b -le $a){throw 'QA production method boundaries changed.'}; $text.Substring($a,$b-$a)
}
$h=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
$protocol=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../src/BuildWorks/BuildWorksQaControlProtocol.cs'))
$shapes=[regex]::Matches($protocol,'(?m)^\s*internal bool (FocusThenText|InventoryPair) =>[^\r\n]+;')
if($shapes.Count -ne 2){throw 'QA request shape properties changed.'}
$h=$h.Replace('// PRODUCTION_REQUEST_SHAPES',(($shapes | ForEach-Object Value) -join "`n"))
$targets=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../src/BuildWorks/BuildWorksQaUiTargets.cs'))
$h=$h.Replace('// PRODUCTION_REQUIRE_FOCUSED',(Slice $targets '        internal void RequireFocused(' '        private bool Hit('))
$h=$h.Replace('// PRODUCTION_SELECT',(Slice $source '        private void SelectUiTarget(' '        private void AfterInput()'))
$h=$h.Replace('// PRODUCTION_DISPATCH',(Slice $dispatch '        private void AfterInput()' '        private static void SubmitFocusedText('))
Add-Type -TypeDefinition $h
[QaPairDispatchChecks]::Run()
