param([string]$BuildWorksRoot=(Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$registry=[IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/HammerBlueprintPieceRegistry.cs'))
$start=$registry.IndexOf('        private void RefreshPresentationPermission()',[StringComparison]::Ordinal)
$end=$registry.IndexOf('        internal bool ProcessThumbnail(', $start,[StringComparison]::Ordinal)
if($start -lt 0 -or $end -le $start){throw 'Registry permission method boundaries changed.'}
$presentation=[IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/BlueprintSourcePresentation.cs'))
$literalStart=$presentation.IndexOf('        internal static string Literal(', [StringComparison]::Ordinal)
$literalEnd=$presentation.IndexOf('        internal static CompositeBlueprintStore.SourceDocument[] KgSources(', $literalStart,[StringComparison]::Ordinal)
if($literalStart -lt 0 -or $literalEnd -le $literalStart){throw 'Literal method boundaries changed.'}
$fixture=[IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
$fixture=$fixture.Replace('// REGISTRY_METHOD',$registry.Substring($start,$end-$start)).Replace('// LITERAL_METHOD',$presentation.Substring($literalStart,$literalEnd-$literalStart))
Add-Type -TypeDefinition $fixture
[PresentationChecks.Cases]::Run()
