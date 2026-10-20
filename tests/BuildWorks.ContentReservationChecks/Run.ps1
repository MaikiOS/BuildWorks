param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintItemStandPlacement.cs'),
    [string]$PreviewSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/PlacementGhostPreviewView.cs'),
    [string]$TransferSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageTransfer.cs'),
    [string]$JournalSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageTransferJournal.cs')
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
$journalPath = (Resolve-Path -LiteralPath $JournalSourcePath).Path
$journal = [IO.File]::ReadAllText($journalPath)
$journalHash = (Get-FileHash -LiteralPath $journalPath).Hash
$profilePath = Join-Path $root 'src/BuildWorks/BlueprintStoragePlayerProfile.cs'
$profileSource = [IO.File]::ReadAllText($profilePath)
$profileHash = (Get-FileHash -LiteralPath $profilePath).Hash
$profileWritePath = Join-Path $root 'src/BuildWorks/BlueprintStorageProfileWrite.cs'
$profileWriteHash = (Get-FileHash -LiteralPath $profileWritePath).Hash
$saveGuardPath = Join-Path $root 'src/BuildWorks/BlueprintPlayerSaveGuard.cs'
$saveGuardHash = (Get-FileHash -LiteralPath $saveGuardPath).Hash
[IO.File]::WriteAllText((Join-Path $output 'PlayerSaveGuardSource.cs'),
    [IO.File]::ReadAllText($saveGuardPath).Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
$creditPath = Join-Path $root 'src/BuildWorks/BlueprintConstructionPayoutCollection.cs'
$creditSource = [IO.File]::ReadAllText($creditPath)
$creditHash = (Get-FileHash -LiteralPath $creditPath).Hash
$creditStart = $creditSource.IndexOf('        private bool SavedCustody()', [StringComparison]::Ordinal)
$creditEnd = $creditSource.IndexOf('        private bool SourceMatches()', [StringComparison]::Ordinal)
if ($creditStart -lt 0 -or $creditEnd -le $creditStart) { throw 'Credit custody predicate boundary changed.' }
$creditPredicates = $creditSource.Substring($creditStart, $creditEnd - $creditStart).Replace('private bool', 'internal bool')
foreach ($required in @('TryReadSavedCustomData(player, SavedCustody', '!SavedCustody())', 'if (SavedCustody() && Matches(after)',
    'bool AfterCustody() => CreditedCustody(after)', 'if (saved && AfterCustody())', 'if (credit.Phase == Journal.ProfileCredited) journal.ConfirmCredit',
    'bool UnsettledPair() => SavedCustody() && Matches(credited)', 'recovery.TryVerifySavedCredit(out full, out readError) && AfterCustody()')) {
    if (-not $creditSource.Contains($required)) { throw "Credit confirmation fence/wiring changed: $required" }
}
if ([regex]::Matches($creditSource, 'journal\.Confirm(?:Saved)?Credit\(').Count -ne 4) { throw 'Review the new credit confirmation route.' }
[IO.File]::WriteAllText((Join-Path $output 'CreditCustodySource.cs'),
    "using OstrixMods.BuildWorks.Geometry; namespace ReservationChecks { internal sealed partial class CreditCustodyProbe {`n" +
    $creditPredicates + "`n} }", [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'StorageProfileWriteSource.cs'),
    [IO.File]::ReadAllText($profileWritePath).Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
$profileParserHash = (Get-FileHash -LiteralPath (Join-Path $root 'src/BuildWorks.Geometry/NativePlayerProfileReadback.cs')).Hash
$loadedPath = Join-Path $root 'src/BuildWorks/BlueprintStorageLoadedInventory.cs'
$loadedHash = (Get-FileHash -LiteralPath $loadedPath).Hash
[IO.File]::WriteAllText((Join-Path $output 'StorageLoadedSource.cs'),
    [IO.File]::ReadAllText($loadedPath).Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
@{ status = 'not_passed'; sourceSHA256 = $sourceHash; previewSHA256 = $previewHash; transferSHA256 = $transferHash; captureSHA256 = $captureHash; journalSHA256 = $journalHash; nativeTransferTested = $false; gameTested = $false } |
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
[IO.File]::WriteAllText((Join-Path $output 'StorageJournalSource.cs'),
    $journal.Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'StorageProfileSource.cs'),
    $profileSource.Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
$worldPath = Join-Path $root 'src/BuildWorks/BlueprintStorageWorldGeneration.cs'
$worldSource = [IO.File]::ReadAllText($worldPath)
$worldHash = (Get-FileHash -LiteralPath $worldPath).Hash
$persistencePath = Join-Path $root 'src/BuildWorks/BlueprintStoragePersistence.cs'
$persistenceSource = [IO.File]::ReadAllText($persistencePath)
$persistenceHash = (Get-FileHash -LiteralPath $persistencePath).Hash
[IO.File]::WriteAllText((Join-Path $output 'StoragePersistenceSource.cs'),
    $persistenceSource.Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'StorageWorldSource.cs'),
    $worldSource.Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
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
@{ status = 'passed_cpu_host_substitutes'; sourceSHA256 = $sourceHash; previewSHA256 = $previewHash; transferSHA256 = $transferHash; captureSHA256 = $captureHash; journalSHA256 = $journalHash; profileAdapterSHA256 = $profileHash; profileParserSHA256 = $profileParserHash; loadedInventorySHA256 = $loadedHash;
    worldGenerationAdapterSHA256 = $worldHash; worldGenerationAdapterTestedWithHostSubstitutes = $true;
    batchAbsenceAdapterTestedWithHostSubstitutes = $true; paidCancellationImplemented = $false;
    pairedPersistenceSHA256 = $persistenceHash; pairedPersistenceHostSubstitutes = $true; profileWriteObserverSHA256 = $profileWriteHash;
    canonicalLocalProfileParserTested = $true; livePlayerProfileReadbackTested = $false;
    playerSaveGuardSHA256 = $saveGuardHash; playerSaveGuardHostSubstitutes = $true; creditCustodySHA256 = $creditHash; creditCustodyPredicatesAndWiring = $true;
    journalIOTested = $true; explicitExactRecoveryTestedWithHostSubstitutes = $true; durableRecoveryImplemented = $false; populatedPlacementEnabled = $false; nativeTransferTested = $false; gameTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification-status.json') -Encoding utf8
