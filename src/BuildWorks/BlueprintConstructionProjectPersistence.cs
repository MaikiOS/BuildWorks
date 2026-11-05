using System;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // One completed native operation, paired Local profile/world save; never debits, places or refunds.
    internal sealed class BlueprintConstructionProjectPersistence
    {
        internal enum Result { Saving, Reading, Verified, Refused }
        private readonly BlueprintConstructionPartJournal journal;
        private readonly BlueprintConstructionPartJournal.Record operation;
        private readonly BlueprintConstructionPartJournal.MountRecord mount;
        private readonly Func<bool> custody;
        private readonly BlueprintStoragePlayerProfile profile;
        private readonly ZNet network;
        private readonly World world;
        private uint before;
        private readonly bool existingMountPair, noEffectMount;
        private bool checkingUnreceiptedMount, checkingUnreceiptedEmpty, recoveredMissingReceipt;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private NativePlayerProfileReadback.Evidence savedProfile;
        private BlueprintStorageWorldGeneration generation;
        private BlueprintStorageWorldGeneration dropGeneration;
        private bool Returning => mount?.Return != null;
        internal Result State { get; private set; } = Result.Saving;
        internal string Reason { get; private set; }

        private BlueprintConstructionProjectPersistence(BlueprintConstructionPartJournal journal, BlueprintConstructionPartJournal.Record operation,
            BlueprintConstructionPartJournal.MountRecord mount, Func<bool> custody, BlueprintStoragePlayerProfile profile, uint? originalGeneration = null,
            bool existingMountPair = false, bool noEffectMount = false)
        { this.journal = journal; this.operation = operation; this.mount = mount; this.custody = custody; this.profile = profile;
            network = ZNet.instance; world = network.GetWorld(); before = originalGeneration ?? SaveSystem.GetSaveNumber();
            this.existingMountPair = existingMountPair; this.noEffectMount = noEffectMount; }

        internal static bool TryBegin(BlueprintConstructionPartJournal journal, BlueprintConstructionPartJournal.Record operation,
            Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
            => TryBeginCore(journal, operation, null, custody, out verification, out reason);

        internal static bool TryBeginMount(BlueprintConstructionPartJournal journal, Guid project, Guid part, Guid childOperation,
            Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
        {
            verification = null; reason = null;
            try
            {
                if (journal == null || !Player.m_localPlayer) throw new InvalidOperationException("Current mount payer is unavailable.");
                var mount = journal.ReadMount(project, part);
                var parent = journal.RequireSavedPayment(project, part, Player.m_localPlayer.GetPlayerID());
                if (mount?.Phase != BlueprintConstructionPartJournal.Observed || mount.Operation != childOperation ||
                    mount.Player != parent.Player || mount.ParentOperation != parent.Operation ||
                    mount.ParentFingerprint != BlueprintConstructionPartJournal.Fingerprint(parent))
                    throw new InvalidOperationException("Observed mount does not match the saved paid parent.");
                var after = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount.AfterInfo, mount.AfterBytes, null);
                uint current = SaveSystem.GetSaveNumber();
                // Read canonical disk evidence before any save can serialize native Load(After) again.
                if (current > (parent.Saved?.Generation ?? 0) && custody() && !ZNet.instance.IsSaving() &&
                    BlueprintStoragePlayerProfile.TryReadSavedCredit(Player.m_localPlayer, after, custody, out var saved, out _))
                {
                    verification = new BlueprintConstructionProjectPersistence(journal, parent, mount, custody, null, current - 1, existingMountPair: true);
                    verification.savedProfile = saved; return true;
                }
                if (!BlueprintStorageCapture.TrySnapshotInventory(Player.m_localPlayer.GetInventory(), out var live) ||
                    live.Info != after.Info || live.Zdo != after.Zdo)
                    throw new InvalidOperationException("No canonical attachment pair matches immutable After; saving its loaded projection is forbidden.");
                return TryBeginCore(journal, parent, mount, custody, out verification, out reason);
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal static bool TryReadMountNoEffect(BlueprintConstructionPartJournal journal, Guid project, Guid part, Guid childOperation,
            Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
        {
            verification = null; reason = null;
            try
            {
                var mount = journal.ReadMount(project, part);
                var parent = journal.RequireSavedPayment(project, part, Player.m_localPlayer.GetPlayerID());
                uint current = SaveSystem.GetSaveNumber();
                if (mount == null || mount.Operation != childOperation || mount.Return != null ||
                    (mount.Phase != BlueprintConstructionPartJournal.Intent && mount.Phase != BlueprintConstructionPartJournal.NoEffect) ||
                    current == 0 || current < (parent.Saved?.Generation ?? 0) || !custody() || ZNet.instance.IsSaving() ||
                    !BlueprintStoragePlayerProfile.TryReadSavedCredit(Player.m_localPlayer,
                        new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount.BeforeInfo, mount.BeforeBytes, null),
                        custody, out var saved, out reason)) throw new InvalidOperationException(reason ?? "Saved no-effect pair is unavailable.");
                verification = new BlueprintConstructionProjectPersistence(journal, parent, mount, custody, null, current - 1,
                    existingMountPair: true, noEffectMount: true);
                verification.savedProfile = saved; return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal static bool TryBeginMountReturn(BlueprintConstructionPartJournal journal, Guid project, Guid part, Guid returnOperation,
            Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
        {
            verification = null; reason = null;
            try
            {
                if (journal == null || !Player.m_localPlayer) throw new InvalidOperationException("Current return payer is unavailable.");
                var mount = journal.ReadMount(project, part);
                var parent = journal.RequireSavedPayment(project, part, Player.m_localPlayer.GetPlayerID());
                if (mount?.Phase != BlueprintConstructionPartJournal.Committed ||
                    mount.Return?.Phase != BlueprintConstructionPartJournal.MountReturnObserved || mount.Return.Operation != returnOperation ||
                    mount.Player != parent.Player || mount.ParentOperation != parent.Operation ||
                    mount.ParentFingerprint != BlueprintConstructionPartJournal.Fingerprint(parent) ||
                    (SaveSystem.GetSaveNumber() < mount.Return.BeforeGeneration || SaveSystem.GetSaveNumber() > mount.Return.BeforeGeneration + 1))
                    throw new InvalidOperationException("Observed return does not match the saved paid parent/current generation.");
                if (SaveSystem.GetSaveNumber() == mount.Return.BeforeGeneration + 1)
                {
                    if (!custody() || !BlueprintStoragePlayerProfile.TryCapture(Player.m_localPlayer, returnOperation, custody, out var profile, out reason) ||
                        !BlueprintStoragePlayerProfile.TryReadSavedCredit(Player.m_localPlayer,
                            new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount.Return.BeforeInfo, mount.Return.BeforeBytes, null),
                            custody, out var saved, out reason)) throw new InvalidOperationException(reason ?? "Saved return recovery profile differs.");
                    var recovered = new BlueprintConstructionProjectPersistence(journal, parent, mount, custody, profile, mount.Return.BeforeGeneration);
                    recovered.savedProfile = saved; verification = recovered; return true;
                }
                return TryBeginCore(journal, parent, mount, custody, out verification, out reason);
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private static bool TryBeginCore(BlueprintConstructionPartJournal journal, BlueprintConstructionPartJournal.Record operation,
            BlueprintConstructionPartJournal.MountRecord mount, Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
        {
            verification = null; reason = null; BlueprintConstructionProjectPersistence candidate = null;
            try
            {
                if (journal == null || operation?.Phase != (mount == null ? BlueprintConstructionPartJournal.Observed : BlueprintConstructionPartJournal.Committed) || custody == null || !custody() ||
                    !ZNet.instance || !ZNet.instance.IsServer() || ZNet.instance.IsDedicated() || ZNet.IsOpenServer() || ZNet.instance.GetPeers().Count != 0 ||
                    ZNet.instance.IsSaving() || ZNet.instance.GetWorld()?.m_fileSource != FileHelpers.FileSource.Local ||
                    ZNet.instance.GetWorldUID() != operation.World || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) ||
                    SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                    !BlueprintStoragePlayerProfile.TryCapture(Player.m_localPlayer, mount?.Return?.Operation ?? mount?.Operation ?? operation.Operation, custody, out var profile, out reason))
                    throw new InvalidOperationException(reason ?? "Construction persistence needs one idle private Local host and exact completed custody.");
                candidate = new BlueprintConstructionProjectPersistence(journal, operation, mount, custody, profile);
                if (!profile.TrySaveAndVerify(out candidate.savedProfile, out reason)) throw new InvalidOperationException(reason);
                if (!candidate.Current() || candidate.network.IsSaving() || SaveSystem.GetSaveNumber() != candidate.before)
                    throw new InvalidOperationException("Construction host or inventory changed during profile save.");
                candidate.network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
                if (!candidate.Current() || !candidate.network.IsSaving() && SaveSystem.GetSaveNumber() != candidate.before + 1)
                    throw new InvalidOperationException("Construction world save did not start the requested next generation.");
                verification = candidate; return true;
            }
            catch (Exception error) { candidate?.Refuse(error.Message); reason = error.Message; return false; }
        }

        private bool Current() => Thread.CurrentThread.ManagedThreadId == thread && network && ReferenceEquals(ZNet.instance, network) &&
            network.IsServer() && !network.IsDedicated() && !ZNet.IsOpenServer() && network.GetPeers().Count == 0 &&
            ReferenceEquals(network.GetWorld(), world) && world.m_uid == operation.World && world.m_fileSource == FileHelpers.FileSource.Local &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && custody();

        internal Result Tick()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) return Result.Refused;
            if (State == Result.Verified || State == Result.Refused) return State;
            try
            {
                if (!Current() || cancellation.IsCancellationRequested || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Construction save context or custody changed; reconciliation is required.");
                if (network.IsSaving()) return State;
                if (SaveSystem.GetSaveNumber() != before + 1) throw new InvalidOperationException("Construction save committed a different generation.");
                if (State == Result.Saving)
                {
                    string error;
                    bool captured;
                    if (Returning)
                    {
                        var returned = mount.Return;
                        var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, returned.ObservedInfo, returned.ObservedBytes, null);
                        if (!BlueprintItemStandContents.TryRead(data, name => name.GetStableHashCode(), out var item))
                            throw new InvalidOperationException("Observed actual-drop packet is unavailable.");
                        captured = BlueprintStorageWorldGeneration.TryCaptureEmptyStand(mount.ParentOperation, operation.PrefabHash, operation.Creator,
                            BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionMountExecution.ReceiptKey, mount.Receipt,
                            custody, out generation, out error) &&
                            BlueprintStorageWorldGeneration.TryCaptureBlob(returned.Operation, item.Prefab.GetStableHashCode(), mount.Player,
                                BlueprintConstructionPayoutSeal.OwnerKey, BlueprintConstructionPayoutSeal.MountMarkerKey, ZDOVars.s_itemData,
                                Convert.FromBase64String(returned.ObservedBytes), custody, out dropGeneration, out error);
                    }
                    else if (noEffectMount) captured = BlueprintStorageWorldGeneration.TryCaptureEmptyStand(mount.ParentOperation, operation.PrefabHash, operation.Creator,
                        BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionPartExecution.ReceiptKey, operation.Receipt,
                        custody, out generation, out error);
                    else captured = mount == null ? BlueprintStorageWorldGeneration.TryCaptureReceipt(operation.Operation, operation.PrefabHash, operation.Creator,
                        BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionPartExecution.ReceiptKey, operation.Receipt,
                        custody, out generation, out error) :
                        BlueprintStorageWorldGeneration.TryCaptureMount(mount.ParentOperation, operation.PrefabHash, operation.Creator,
                            BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionMountExecution.ReceiptKey, mount.Receipt,
                            new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.MountedInfo, mount.MountedBytes, null),
                            custody, out generation, out error);
                    if (!captured || generation.Generation != before + 1 ||
                        Returning && dropGeneration.Generation != generation.Generation ||
                        !generation.TryStart(cancellation.Token, out error) || Returning && !dropGeneration.TryStart(cancellation.Token, out error))
                        throw new InvalidOperationException(error ?? "Construction world readback could not start.");
                    State = Result.Reading;
                }
                if (!generation.IsCompleted || Returning && !dropGeneration.IsCompleted) return State;
                NativeWorldStorageReadback.Evidence savedDrop = null;
                if (Returning && !dropGeneration.TryFinish(out savedDrop, out string dropError))
                    throw new InvalidOperationException(dropError ?? "Protected actual drop was not saved with the empty holder.");
                if (!generation.TryFinish(out var savedWorld, out string reason))
                {
                    if (!existingMountPair || noEffectMount || checkingUnreceiptedEmpty || recoveredMissingReceipt ||
                        checkingUnreceiptedMount && (!BlueprintStorageCapture.TrySnapshotInventory(Player.m_localPlayer.GetInventory(), out var rawAfter) ||
                            rawAfter.Info != mount.AfterInfo || rawAfter.Zdo != mount.AfterBytes) ||
                        !BlueprintStorageWorldGeneration.TryCaptureUnreceiptedMount(mount.ParentOperation, operation.PrefabHash, operation.Creator,
                            operation.Receipt, new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.MountedInfo, mount.MountedBytes, null),
                            custody, out generation, out reason, emptyStand: checkingUnreceiptedMount) || !generation.TryStart(cancellation.Token, out reason))
                        throw new InvalidOperationException(reason ?? "Canonical attachment world differs.");
                    if (checkingUnreceiptedMount) checkingUnreceiptedEmpty = true;
                    checkingUnreceiptedMount = true; return State;
                }
                if (!ReadProfile(out var currentProfile, out reason) ||
                    currentProfile.ProfileHash != savedProfile.ProfileHash || currentProfile.InventoryHash != savedProfile.InventoryHash ||
                    !Current() || network.IsSaving()) throw new InvalidOperationException(reason ?? "Paired construction evidence changed.");
                if (checkingUnreceiptedMount)
                {
                    // Both old halves are proved; persist only the repaired child receipt, preserving raw profile After.
                    before = SaveSystem.GetSaveNumber();
                    if (before == uint.MaxValue) throw new InvalidOperationException("No successor generation is available.");
                    checkingUnreceiptedMount = false; checkingUnreceiptedEmpty = false; recoveredMissingReceipt = true; generation = null; State = Result.Saving;
                    network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false); return State;
                }
                var pair = new BlueprintConstructionPartJournal.SavePair {
                    BeforeGeneration = before, Generation = generation.Generation, ProfileHash = currentProfile.ProfileHash,
                    InventoryHash = currentProfile.InventoryHash, WorldPayloadHash = savedWorld.PayloadSha256 };
                if (mount == null) journal.ConfirmSaved(operation.Project, operation.Part, operation.Operation, pair);
                else if (Returning) journal.ConfirmMountReturnSaved(mount.Project, mount.Part, mount.Return.Operation, pair, savedDrop.PayloadSha256);
                else if (noEffectMount) journal.MountNoEffect(mount.Project, mount.Part, mount.Operation,
                    new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount.BeforeInfo, mount.BeforeBytes, null), true);
                else journal.ConfirmMountSaved(mount.Project, mount.Part, mount.Operation, pair);
                State = Result.Verified;
            }
            catch (Exception error) { Refuse(error.Message); }
            return State;
        }
        private bool ReadProfile(out NativePlayerProfileReadback.Evidence evidence, out string reason) => Returning ?
            BlueprintStoragePlayerProfile.TryReadSavedCredit(Player.m_localPlayer,
                new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount.Return.BeforeInfo, mount.Return.BeforeBytes, null),
                custody, out evidence, out reason) : existingMountPair ? BlueprintStoragePlayerProfile.TryReadSavedCredit(Player.m_localPlayer,
                    new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                        noEffectMount ? mount.BeforeInfo : mount.AfterInfo, noEffectMount ? mount.BeforeBytes : mount.AfterBytes, null),
                    custody, out evidence, out reason) : profile.TryVerify(out evidence, out reason);
        internal void Cancel() { if (State != Result.Verified && State != Result.Refused) Refuse("Construction persistence interrupted; reconciliation remains required."); }
        private void Refuse(string reason) { Reason = reason; State = Result.Refused; cancellation.Cancel(); }
    }
}
