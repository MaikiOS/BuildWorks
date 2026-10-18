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
        private readonly Func<bool> custody;
        private readonly BlueprintStoragePlayerProfile profile;
        private readonly ZNet network;
        private readonly World world;
        private readonly uint before;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private NativePlayerProfileReadback.Evidence savedProfile;
        private BlueprintStorageWorldGeneration generation;
        internal Result State { get; private set; } = Result.Saving;
        internal string Reason { get; private set; }

        private BlueprintConstructionProjectPersistence(BlueprintConstructionPartJournal journal, BlueprintConstructionPartJournal.Record operation,
            Func<bool> custody, BlueprintStoragePlayerProfile profile)
        { this.journal = journal; this.operation = operation; this.custody = custody; this.profile = profile;
            network = ZNet.instance; world = network.GetWorld(); before = SaveSystem.GetSaveNumber(); }

        internal static bool TryBegin(BlueprintConstructionPartJournal journal, BlueprintConstructionPartJournal.Record operation,
            Func<bool> custody, out BlueprintConstructionProjectPersistence verification, out string reason)
        {
            verification = null; reason = null; BlueprintConstructionProjectPersistence candidate = null;
            try
            {
                if (journal == null || operation?.Phase != BlueprintConstructionPartJournal.Observed || custody == null || !custody() ||
                    !ZNet.instance || !ZNet.instance.IsServer() || ZNet.instance.IsDedicated() || ZNet.IsOpenServer() || ZNet.instance.GetPeers().Count != 0 ||
                    ZNet.instance.IsSaving() || ZNet.instance.GetWorld()?.m_fileSource != FileHelpers.FileSource.Local ||
                    ZNet.instance.GetWorldUID() != operation.World || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) ||
                    SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                    !BlueprintStoragePlayerProfile.TryCapture(Player.m_localPlayer, operation.Operation, custody, out var profile, out reason))
                    throw new InvalidOperationException(reason ?? "Construction persistence needs one idle private Local host and exact completed custody.");
                candidate = new BlueprintConstructionProjectPersistence(journal, operation, custody, profile);
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
                    if (!BlueprintStorageWorldGeneration.TryCaptureReceipt(operation.Operation, operation.PrefabHash, operation.Creator,
                        BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionPartExecution.ReceiptKey, operation.Receipt,
                        custody, out generation, out string error) || generation.Generation != before + 1 || !generation.TryStart(cancellation.Token, out error))
                        throw new InvalidOperationException(error ?? "Construction world readback could not start.");
                    State = Result.Reading;
                }
                if (!generation.IsCompleted) return State;
                if (!generation.TryFinish(out var savedWorld, out string reason) || !profile.TryVerify(out var currentProfile, out reason) ||
                    currentProfile.ProfileHash != savedProfile.ProfileHash || currentProfile.InventoryHash != savedProfile.InventoryHash ||
                    !Current() || network.IsSaving()) throw new InvalidOperationException(reason ?? "Paired construction evidence changed.");
                journal.ConfirmSaved(operation.Project, operation.Part, operation.Operation, new BlueprintConstructionPartJournal.SavePair {
                    BeforeGeneration = before, Generation = generation.Generation, ProfileHash = currentProfile.ProfileHash,
                    InventoryHash = currentProfile.InventoryHash, WorldPayloadHash = savedWorld.PayloadSha256 });
                State = Result.Verified;
            }
            catch (Exception error) { Refuse(error.Message); }
            return State;
        }
        internal void Cancel() { if (State != Result.Verified && State != Result.Refused) Refuse("Construction persistence interrupted; reconciliation remains required."); }
        private void Refuse(string reason) { Reason = reason; State = Result.Refused; cancellation.Cancel(); }
    }
}
