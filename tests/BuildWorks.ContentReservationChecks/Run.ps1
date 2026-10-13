param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintItemStandPlacement.cs'),
    [string]$PreviewSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/PlacementGhostPreviewView.cs'),
    [string]$TransferSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageTransfer.cs')
)
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$output = Join-Path $root 'artifacts/content-reservations'
New-Item -ItemType Directory -Path $output -Force | Out-Null
$source = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath))
$sourceHash = (Get-FileHash -LiteralPath $SourcePath).Hash
$preview = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $PreviewSourcePath))
$previewHash = (Get-FileHash -LiteralPath $PreviewSourcePath).Hash
$transferSource = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $TransferSourcePath))
$transferHash = (Get-FileHash -LiteralPath $TransferSourcePath).Hash
$capturePath = Join-Path $root 'src/BuildWorks/BlueprintStorageCapture.cs'
$capture = [IO.File]::ReadAllText($capturePath)
$captureHash = (Get-FileHash -LiteralPath $capturePath).Hash
@{ status = 'not_passed'; sourceSHA256 = $sourceHash; previewSHA256 = $previewHash; transferSHA256 = $transferHash; captureSHA256 = $captureHash; nativeTransferTested = $false; gameTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification-status.json') -Encoding utf8
function Segment([string]$from, [string]$until) {
    $start = $source.IndexOf($from, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Reservation source boundary missing: $from" }
    $end = $source.IndexOf($until, $start, [StringComparison]::Ordinal)
    if ($end -le $start) { throw "Reservation source boundary missing: $until" }
    $source.Substring($start, $end - $start)
}
$methods = (Segment '        internal sealed class Reservation' '        internal static bool CanRestore') +
    (Segment '        internal static bool NeedsSupply' '        internal static bool TryApply')
if ($source -notmatch 'return BlueprintStorageCapture.CanPlaceWithoutContents\(piece, data\);') {
    throw 'Populated storage placement guard changed; the reservation test does not verify transfers.'
}
$prefix = 'plan.Select(pose =>'
$start = $preview.IndexOf($prefix, [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Preview selection boundary missing.' }
$start += $prefix.Length
$end = $preview.IndexOf('), liveSupplies', $start, [StringComparison]::Ordinal)
if ($end -le $start) { throw 'Preview selection end missing.' }
$selector = $preview.Substring($start, $end - $start)
$generated = "using System; using System.Collections.Generic; using System.Globalization; using OstrixMods.BuildWorks.Geometry;`nnamespace ReservationChecks { internal static class BlueprintItemStandPlacement {`n" + $methods + "`n} internal static class PreviewSelector { internal static BlueprintForeignPartData Select(TransformSnapshot pose) => " + $selector + "; } }"
[IO.File]::WriteAllText((Join-Path $output 'ReservationSource.cs'), $generated, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'StorageTransferSource.cs'),
    $transferSource.Replace('using UnityEngine;', '').Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'),
    [Text.UTF8Encoding]::new($false))
$captureStart = $capture.IndexOf('        private static readonly FieldInfo ContainerView', [StringComparison]::Ordinal)
$captureEnd = $capture.IndexOf('        // Only construction compatibility', [StringComparison]::Ordinal)
if ($captureStart -lt 0 -or $captureEnd -le $captureStart) { throw 'Capture cancellation guard boundary missing.' }
[IO.File]::WriteAllText((Join-Path $output 'StorageRemovalSource.cs'),
    "using System; using System.Reflection; using OstrixMods.BuildWorks.Geometry; namespace ReservationChecks { internal static partial class BlueprintStorageCapture {`n" +
    $capture.Substring($captureStart, $captureEnd - $captureStart) + "`n} }", [Text.UTF8Encoding]::new($false))
& dotnet build (Join-Path $PSScriptRoot 'BuildWorks.ContentReservationChecks.csproj') -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Reservation CPU harness build failed.' }
& (Join-Path $PSScriptRoot 'bin/Release/net472/BuildWorks.ContentReservationChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Reservation CPU checks failed. Native transfer is not exercised.' }
@{ status = 'passed_cpu_host_substitutes'; sourceSHA256 = $sourceHash; previewSHA256 = $previewHash; transferSHA256 = $transferHash; captureSHA256 = $captureHash;
    populatedPlacementEnabled = $false; nativeTransferTested = $false; gameTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification-status.json') -Encoding utf8
