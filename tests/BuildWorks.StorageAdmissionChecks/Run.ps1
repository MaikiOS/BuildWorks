param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$session = [IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/PrecisionPlacementSession.cs'))
$project = [IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/PrecisionPlacementSession.Project.cs'))
$start = $session.IndexOf('        private static bool SupportsPrecisionPlacement(Piece piece)', [StringComparison]::Ordinal)
$end = $session.IndexOf('        private static bool SupportsBlueprintWorkspacePiece(', $start, [StringComparison]::Ordinal)
$projectStart = $project.IndexOf('        internal static bool SupportsProjectPart(', [StringComparison]::Ordinal)
$projectEnd = $project.IndexOf('        // Consume the confirmation here;', $projectStart, [StringComparison]::Ordinal)
if ($start -lt 0 -or $end -le $start -or $projectStart -lt 0 -or $projectEnd -le $projectStart) { throw 'Admission source boundaries changed.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
$code = $harness.Replace('// PRODUCTION_PRECISION', $session.Substring($start, $end-$start)).Replace('// PRODUCTION_PROJECT', $project.Substring($projectStart, $projectEnd-$projectStart))
Add-Type -TypeDefinition $code
[BuildWorksAdmissionChecks.PrecisionPlacementSession]::Run()
