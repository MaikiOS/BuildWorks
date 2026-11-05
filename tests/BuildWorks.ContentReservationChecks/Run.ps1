param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintItemStandPlacement.cs'),
    [string]$PreviewSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/PlacementGhostPreviewView.cs'),
    [string]$TransferSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageTransfer.cs'),
    [string]$JournalSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageTransferJournal.cs'),
    [string]$ProfileSourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStoragePlayerProfile.cs'),
    [switch]$NoRestore
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
$returnRecoveryPath = Join-Path $root 'src/BuildWorks/BlueprintStorageTransfer.ReturnRecovery.cs'
$pairedRecoveryPath = Join-Path $root 'src/BuildWorks/BlueprintStorageTransfer.PairedRecovery.cs'
[IO.File]::WriteAllText((Join-Path $output 'PairedRecoverySource.cs'),
    [IO.File]::ReadAllText($pairedRecoveryPath).Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'ReturnRecoverySource.cs'),
    [IO.File]::ReadAllText($returnRecoveryPath).Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'), [Text.UTF8Encoding]::new($false))
$capturePath = Join-Path $root 'src/BuildWorks/BlueprintStorageCapture.cs'
$capture = [IO.File]::ReadAllText($capturePath)
$captureHash = (Get-FileHash -LiteralPath $capturePath).Hash
$journalPath = (Resolve-Path -LiteralPath $JournalSourcePath).Path
$journal = [IO.File]::ReadAllText($journalPath)
$journalHash = (Get-FileHash -LiteralPath $journalPath).Hash
$profilePath = (Resolve-Path -LiteralPath $ProfileSourcePath).Path
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
$creditEnd = $creditSource.IndexOf('        internal static bool WithinReach(', [StringComparison]::Ordinal)
if ($creditStart -lt 0 -or $creditEnd -le $creditStart) { throw 'Credit custody predicate boundary changed.' }
$creditPredicates = $creditSource.Substring($creditStart, $creditEnd - $creditStart).Replace('private bool', 'internal bool')
foreach ($required in @('TryReadSavedCustomData(player, SavedCustody', '!SavedCustody())', 'if (SavedCustody() && Matches(after)',
    'bool AfterCustody() => CreditedCustody(after)', 'if (saved && AfterCustody())', 'if (credit.Phase == Journal.ProfileCredited) journal.ConfirmCredit',
    'bool UnsettledPair() => SavedCustody() && player.m_customData.TryGetValue(key, out string receipt) && receipt == value',
    'TryReadSavedCredit(player, credited, UnsettledPair', 'recovery.TryVerifySavedCredit(out full, out readError) && AfterCustody()')) {
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
$ownedView = Segment '        internal static bool OwnsView(' '        internal static bool CanSupply('
$generated = "using System; using System.Collections.Generic; using System.Globalization; using OstrixMods.BuildWorks.Geometry;`nnamespace ReservationChecks { internal static partial class BlueprintItemStandPlacement {`n" + $methods + $ownedView + "`n} internal static class PreviewSelector { internal static BlueprintForeignPartData Select(TransformSnapshot pose) => " + $selector + "; } }"
[IO.File]::WriteAllText((Join-Path $output 'ReservationSource.cs'), $generated, [Text.UTF8Encoding]::new($false))
$mountHashes = @{}
function ExactSegment([string]$body, [string]$startText, [string]$endText) {
    $start = $body.IndexOf($startText, [StringComparison]::Ordinal)
    $end = $body.IndexOf($endText, $start + 1, [StringComparison]::Ordinal)
    if ($start -lt 0 -or $end -le $start) { throw 'Credit projection source boundary changed.' }
    $body.Substring($start, $end - $start)
}
$payoutBody = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionPayoutJournal.cs'))
$partBody = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionPartJournal.cs'))
$mountCreditBody = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionPartJournal.MountCredit.cs'))
$projection = ExactSegment $payoutBody '        internal static BlueprintForeignPartData CreditInventory(BlueprintForeignPartData before, string sourceItem' '        internal Record Read(Guid project)'
$projection += ExactSegment $payoutBody '        private static BlueprintContainerInventory.Item ParseItem(' '        private void Write(Record r, bool replace)'
$validation = ExactSegment $partBody '        internal static IReadOnlyList<BlueprintContainerInventory.Item> ValidateInventory(' '        private static byte[] Receipt('
$receipt = ExactSegment $mountCreditBody '        internal static string MountCreditKey(' '        private static void ValidateMountReturnCredit('
$reach = ExactSegment $creditSource '        internal static bool WithinReach(' '        private bool SourceMatches('
$digest = [regex]::Match($partBody, '(?m)^        private static string Digest\(byte\[\] bytes\)[^\r\n]*').Value
if ([string]::IsNullOrEmpty($digest)) { throw 'Part journal digest boundary changed.' }
[IO.File]::WriteAllText((Join-Path $output 'MountCreditProjectionSource.cs'),
    "using System; using System.IO; using System.Linq; using System.Globalization; using System.Collections.Generic; using System.Security.Cryptography; using OstrixMods.BuildWorks.Geometry; using Parts=ReservationChecks.BlueprintConstructionPartJournal; namespace ReservationChecks { internal sealed partial class BlueprintConstructionPayoutJournal {`n" +
    $projection + "`n} internal sealed partial class BlueprintConstructionPartJournal {`n" + $validation + $receipt + $digest +
    "`n} internal static class BlueprintConstructionPayoutCollection {`n" + $reach + "`n} }", [Text.UTF8Encoding]::new($false))
foreach ($entry in @(@('BlueprintConstructionMountExecution.cs','MountExecutionSource.cs'), @('BlueprintConstructionMountContext.cs','MountContextSource.cs'), @('BlueprintConstructionMountContext.ReturnSource.cs','MountReturnBindingSource.cs'),
    @('BlueprintConstructionMountDetachExecution.cs','MountDetachSource.cs'), @('BlueprintConstructionMountDetachExecution.Credit.cs','MountCreditExecutionSource.cs'), @('BlueprintConstructionMountDetachExecution.Resume.cs','MountResumeSource.cs'), @('BlueprintConstructionMountDetachExecution.Cleanup.cs','MountCleanupSource.cs'), @('BlueprintConstructionProjectPersistence.cs','ProjectPersistenceSource.cs'),
    @('BlueprintConstructionPayoutSeal.cs','PayoutSealSource.cs'), @('BlueprintConstructionPayoutSeal.Mount.cs','MountSealSource.cs'))) {
    $path = Join-Path $root ('src/BuildWorks/' + $entry[0])
    $mountHashes[$entry[0]] = (Get-FileHash -LiteralPath $path).Hash
    $body = [IO.File]::ReadAllText($path).Replace('using UnityEngine;', '').Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks').
        Replace('using Plans = OstrixMods.BuildWorks.', 'using Plans = ReservationChecks.').Replace('using Journal = OstrixMods.BuildWorks.', 'using Journal = ReservationChecks.').
        Replace('global::Player', 'global::ReservationChecks.Player')
    [IO.File]::WriteAllText((Join-Path $output $entry[1]), $body, [Text.UTF8Encoding]::new($false))
}
$cleanupBody=[IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionPayoutCleanup.cs'))
$witness=ExactSegment $cleanupBody '        internal sealed class Witness' '        private readonly Journal journal;'
$seam=ExactSegment $cleanupBody '        internal static void DestroyExact(' '        internal void Refuse('
[IO.File]::WriteAllText((Join-Path $output 'ExactDestroySource.cs'), "using System; using System.Collections.Generic; using System.Reflection; namespace ReservationChecks { internal sealed class BlueprintConstructionPayoutCleanup {`n"+$witness+$seam+"`n} }",[Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText((Join-Path $output 'StorageTransferSource.cs'),
    $transferSource.Replace('using UnityEngine;', '').Replace('namespace OstrixMods.BuildWorks', 'namespace ReservationChecks'),
    [Text.UTF8Encoding]::new($false))
$constructionTransfer = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintStorageTransfer.Construction.cs'))
$settleStart = $constructionTransfer.IndexOf('        internal bool TrySettleConstruction(', [StringComparison]::Ordinal)
$settleEnd = $constructionTransfer.LastIndexOf("`n    }", [StringComparison]::Ordinal)
if ($settleStart -lt 0 -or $settleEnd -le $settleStart) { throw 'Actual settlement source boundary changed.' }
[IO.File]::WriteAllText((Join-Path $output 'StorageSettlementSource.cs'),
    "using System; namespace ReservationChecks { internal sealed partial class BlueprintStorageTransfer {`n" +
    $constructionTransfer.Substring($settleStart, $settleEnd - $settleStart) + "`n} }", [Text.UTF8Encoding]::new($false))
$partExecution = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionPartExecution.cs'))
$returnCoordinator = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionContentsReturn.cs'))
$advanceStart = $returnCoordinator.IndexOf('        private void Advance()', [StringComparison]::Ordinal)
$advanceEnd = $returnCoordinator.LastIndexOf("`n    }", [StringComparison]::Ordinal)
if ($advanceStart -lt 0 -or $advanceEnd -le $advanceStart) { throw 'Actual return coordinator boundary changed.' }
[IO.File]::WriteAllText((Join-Path $output 'ContentsReturnTickSource.cs'),
    "using System; namespace ReservationChecks { internal sealed partial class ContentsReturnTickProbe {`n" +
    $returnCoordinator.Substring($advanceStart, $advanceEnd - $advanceStart) + "`n} }", [Text.UTF8Encoding]::new($false))
$tickStart = $partExecution.IndexOf('        private void TickContents()', [StringComparison]::Ordinal)
$tickEnd = $partExecution.IndexOf('        private bool Context()', $tickStart, [StringComparison]::Ordinal)
if ($tickStart -lt 0 -or $tickEnd -le $tickStart) { throw 'Actual contents tick boundary changed.' }
[IO.File]::WriteAllText((Join-Path $output 'PartContentsTickSource.cs'),
    "using System; namespace ReservationChecks { internal sealed partial class PartContentsTickProbe {`n" +
    $partExecution.Substring($tickStart, $tickEnd - $tickStart) + "`n} }", [Text.UTF8Encoding]::new($false))
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
$callbackSource = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintConstructionContainerGuard.cs'))
$callbackStart = $callbackSource.IndexOf('        private const BindingFlags InstanceFields', [StringComparison]::Ordinal)
$callbackEnd = $callbackSource.IndexOf('        internal static bool NativeRoot(', $callbackStart, [StringComparison]::Ordinal)
if ($callbackStart -lt 0 -or $callbackEnd -le $callbackStart) { throw 'Native callback predicate boundary changed.' }
[IO.File]::WriteAllText((Join-Path $output 'ContainerCallbacksSource.cs'),
    "using System; using System.Reflection; namespace ReservationChecks { internal static class BlueprintConstructionContainerGuard {`n" +
    $callbackSource.Substring($callbackStart, $callbackEnd - $callbackStart) + "`n} }", [Text.UTF8Encoding]::new($false))
$captureEnd = $capture.IndexOf('        // Unity-thread classification;', [StringComparison]::Ordinal)
if ($captureStart -lt 0 -or $captureEnd -le $captureStart) { throw 'Capture cancellation guard boundary missing.' }
[IO.File]::WriteAllText((Join-Path $output 'StorageRemovalSource.cs'),
    "using System; using System.Reflection; using OstrixMods.BuildWorks.Geometry; namespace ReservationChecks { internal static partial class BlueprintStorageCapture {`n" +
    $capture.Substring($captureStart, $captureEnd - $captureStart) + "`n} }", [Text.UTF8Encoding]::new($false))
$buildArgs = @('build', (Join-Path $PSScriptRoot 'BuildWorks.ContentReservationChecks.csproj'), '-c', 'Release', '--nologo', '-m:1')
if ($NoRestore) { $buildArgs += '--no-restore' }
& dotnet @buildArgs
if ($LASTEXITCODE -ne 0) { throw 'Reservation CPU harness build failed.' }
& (Join-Path $PSScriptRoot 'bin/Release/net472/BuildWorks.ContentReservationChecks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Reservation CPU checks failed. Native transfer is not exercised.' }
@{ status = 'passed_cpu_host_substitutes'; sourceSHA256 = $sourceHash; previewSHA256 = $previewHash; transferSHA256 = $transferHash; captureSHA256 = $captureHash; journalSHA256 = $journalHash; profileAdapterSHA256 = $profileHash; profileParserSHA256 = $profileParserHash; loadedInventorySHA256 = $loadedHash;
    worldGenerationAdapterSHA256 = $worldHash; worldGenerationAdapterTestedWithHostSubstitutes = $true;
    batchAbsenceAdapterTestedWithHostSubstitutes = $true; paidCancellationImplemented = $false;
    pairedPersistenceSHA256 = $persistenceHash; pairedPersistenceHostSubstitutes = $true; profileWriteObserverSHA256 = $profileWriteHash;
    canonicalLocalProfileParserTested = $true; livePlayerProfileReadbackTested = $false;
    playerSaveGuardSHA256 = $saveGuardHash; playerSaveGuardHostSubstitutes = $true; creditCustodySHA256 = $creditHash; creditCustodyPredicatesAndWiring = $true;
    mountExecutionSHA256 = $mountHashes['BlueprintConstructionMountExecution.cs']; projectPersistenceSHA256 = $mountHashes['BlueprintConstructionProjectPersistence.cs'];
    mountCreditExecutionSHA256 = $mountHashes['BlueprintConstructionMountDetachExecution.Credit.cs'];
    creditProjectionSourceSHA256 = (Get-FileHash (Join-Path $root 'src/BuildWorks/BlueprintConstructionPayoutJournal.cs')).Hash;
    mountCreditJournalSHA256 = (Get-FileHash (Join-Path $root 'src/BuildWorks/BlueprintConstructionPartJournal.MountCredit.cs')).Hash;
    mountActorAndPairOrchestrationWithHostSubstitutes = $true; nativeMountTested = $false;
    journalIOTested = $true; explicitExactRecoveryTestedWithHostSubstitutes = $true; durableRecoveryImplemented = $false; populatedPlacementEnabled = $false; nativeTransferTested = $false; gameTested = $false } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification-status.json') -Encoding utf8
