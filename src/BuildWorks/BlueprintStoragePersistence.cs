using System;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Local host evidence only: no settlement, item creation, refund, marker removal or remote acknowledgement.
    internal sealed class BlueprintStoragePersistence
    {
        internal enum Result { Saving, Reading, Verified, Refused }
        private static BlueprintStoragePersistence active;
        private readonly BlueprintStorageTransfer transfer;
        private readonly BlueprintStoragePlayerProfile profile;
        private readonly ZNet network;
        private readonly World world;
        private readonly uint before;
        private readonly int thread;
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private NativePlayerProfileReadback.Evidence savedProfile;
        private BlueprintStorageWorldGeneration generation;
        internal Result State { get; private set; } = Result.Saving;
        internal string Reason { get; private set; }

        private BlueprintStoragePersistence(BlueprintStorageTransfer transfer, BlueprintStoragePlayerProfile profile)
        {
            this.transfer = transfer; this.profile = profile;
            network = ZNet.instance; world = network.GetWorld(); before = SaveSystem.GetSaveNumber();
            thread = Thread.CurrentThread.ManagedThreadId;
        }

        internal static bool TryBegin(BlueprintStorageTransfer transfer, out BlueprintStoragePersistence verification, out string reason)
        {
            verification = null; reason = null;
            BlueprintStoragePersistence candidate = null;
            try
            {
                if (transfer == null || !transfer.PersistenceCustodyValid || active != null || !ZNet.instance ||
                    !ZNet.instance.IsServer() || ZNet.instance.IsDedicated() || ZNet.IsOpenServer() || ZNet.instance.GetPeers().Count != 0 ||
                    ZNet.instance.IsSaving() || ZNet.instance.GetWorld() == null ||
                    ZNet.instance.GetWorld().m_fileSource != FileHelpers.FileSource.Local ||
                    SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) ||
                    SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                    !transfer.TryPreparePlayerReadback(out var profile, out reason))
                    throw new InvalidOperationException(reason ?? "Persistence currently requires one idle, private Local host and completed exact custody.");
                candidate = new BlueprintStoragePersistence(transfer, profile); active = candidate;
                if (!profile.TrySaveAndVerify(out candidate.savedProfile, out reason))
                    throw new InvalidOperationException(reason);
                if (!candidate.Current() || ZNet.instance.IsSaving() || SaveSystem.GetSaveNumber() != candidate.before)
                    throw new InvalidOperationException("Host, generation or custody changed while saving the player.");
                // Do not freeze the game waiting for the world thread. Tick accepts only the next committed generation.
                candidate.network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
                if (!candidate.Current() || !candidate.network.IsSaving() && SaveSystem.GetSaveNumber() != candidate.before + 1)
                    throw new InvalidOperationException("The requested world save did not start the next generation.");
                verification = candidate; return true;
            }
            catch (Exception error)
            {
                candidate?.Refuse(error.Message); reason = error.Message; return false;
            }
        }

        private bool Current(bool custody = true) => Thread.CurrentThread.ManagedThreadId == thread && ReferenceEquals(ZNet.instance, network) && network &&
            network.IsServer() && !network.IsDedicated() && !ZNet.IsOpenServer() && network.GetPeers().Count == 0 &&
            ReferenceEquals(network.GetWorld(), world) && world.m_fileSource == FileHelpers.FileSource.Local &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && (!custody || transfer.PersistenceCustodyValid);

        internal Result Tick()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) return Result.Refused;
            if (State == Result.Verified || State == Result.Refused) return State;
            try
            {
                // Polling must not reread the whole WAL and both inventories on every game frame.
                if (!Current(custody: false) || cancellation.IsCancellationRequested || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Persistence context, generation or custody changed.");
                if (network.IsSaving()) return State;
                if (SaveSystem.GetSaveNumber() != before + 1)
                    throw new InvalidOperationException("World save did not commit exactly the requested next generation.");
                if (State == Result.Saving)
                {
                    if (!transfer.TryPrepareWorldReadback(out generation, out string error) || generation.Generation != before + 1 ||
                        !generation.TryStart(cancellation.Token, out error))
                        throw new InvalidOperationException(error ?? "Committed world generation differs from the save request.");
                    State = Result.Reading;
                }
                if (!generation.IsCompleted) return State;
                if (!generation.TryFinish(out var worldEvidence, out string reason) ||
                    !profile.TryVerify(out var playerEvidence, out reason) || playerEvidence.ProfileHash != savedProfile.ProfileHash ||
                    playerEvidence.InventoryHash != savedProfile.InventoryHash || !Current() || network.IsSaving() ||
                    !transfer.TryRecordPersistence(before, generation.Generation, playerEvidence, worldEvidence, out reason))
                    throw new InvalidOperationException(reason ?? "Paired save evidence changed before journal acceptance.");
                State = Result.Verified; Release();
            }
            catch (Exception error) { Refuse(error.Message); }
            return State;
        }

        internal void Cancel()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Persistence cancellation must run on its owning thread.");
            if (State != Result.Verified && State != Result.Refused) Refuse("Persistence verification cancelled; reconciliation remains required.");
        }

        private void Refuse(string reason) { Reason = reason; State = Result.Refused; cancellation.Cancel(); Release(); }
        private void Release() { if (ReferenceEquals(active, this)) active = null; }
    }
}
