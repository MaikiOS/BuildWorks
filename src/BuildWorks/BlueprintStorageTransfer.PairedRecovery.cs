using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintStorageTransfer
    {
        internal enum PairedRecoveryResult { Pending, Settled, Refused }
        private PairedReturnRecovery pairedRecovery;
        internal bool RecoveringSavedPair => pairedRecovery != null;
        internal PairedRecoveryResult TickPairedReturnRecovery(out string reason)
        {
            reason = "No saved return pair is being recovered.";
            return pairedRecovery == null ? PairedRecoveryResult.Refused : pairedRecovery.Tick(out reason);
        }

        private static BlueprintStorageTransfer ResumePairedReturn(BlueprintStorageTransferJournal.Snapshot expected, Player player,
            Piece piece, Container container, ZNetView view, ConstructionBinding binding, byte[] receipt)
        {
            BlueprintStorageTransferJournal writer = null; BlueprintStorageTransfer candidate = null;
            try
            {
                if (binding == null || !binding.Current() || receipt == null || receipt.Length == 0)
                    throw new InvalidOperationException("Saved paid return parent/receipt is unavailable.");
                writer = BlueprintStorageTransferJournal.ResumePairedReturn(expected);
                var saved = BlueprintStorageTransferJournal.Read(writer.Path); var plan = saved.Prepared;
                if (saved.SavedPair.SourceSession == BlueprintStoragePlayerProfile.ProcessSession ||
                    plan.ConstructionProject != binding.Project || plan.ConstructionPart != binding.Part || plan.ConstructionFingerprint != binding.Fingerprint ||
                    plan.Player != player.GetPlayerID() || plan.World != ZNet.instance.GetWorldUID() || plan.Creator != piece.GetCreator() ||
                    plan.PrefabHash != view.GetZDO().GetPrefab() || plan.Width != player.GetInventory().GetWidth() || plan.Height != player.GetInventory().GetHeight())
                    throw new InvalidOperationException("Persisted return recovery requires a new process and the same saved payer/parent/target.");
                ReceiverCells(player.GetInventory());
                var current = new ConstructionBinding(binding.Project, binding.Part, binding.Fingerprint, () => binding.Current() && writer.OnlyUnresolvedReturn());
                candidate = new BlueprintStorageTransfer(plan.Operation, player, piece, container, view, new List<Entry>(),
                    Array.Empty<BlueprintItemStandPlacement.Supply>(), returning: true, marker: plan.Parent, construction: current);
                candidate.journal = writer;
                candidate.pairedRecovery = new PairedReturnRecovery(candidate, saved, receipt);
                candidate.pairedRecovery.Start();
                return candidate;
            }
            catch { candidate?.Dispose(); writer?.Dispose(); throw; }
        }

        private sealed class PairedReturnRecovery
        {
            private readonly BlueprintStorageTransfer owner;
            private readonly BlueprintStorageTransferJournal.Snapshot saved;
            private readonly byte[] receipt;
            private readonly Game game;
            private readonly PlayerProfile profile;
            private readonly World world;
            private readonly string path, filename, name;
            private readonly uint generation;
            private readonly BlueprintForeignPartData inventory;
            private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
            private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
            private BlueprintStorageWorldGeneration storageRead, receiptRead;
            private string profileHash, reason;
            private PairedRecoveryResult result;

            internal PairedReturnRecovery(BlueprintStorageTransfer owner, BlueprintStorageTransferJournal.Snapshot saved, byte[] receipt)
            {
                this.owner = owner; this.saved = saved; this.receipt = (byte[])receipt.Clone();
                game = Game.instance; profile = game ? game.GetPlayerProfile() : null; world = owner.network.GetWorld();
                path = profile?.GetPath(); filename = profile?.GetFilename(); name = profile?.GetName(); generation = SaveSystem.GetSaveNumber();
                if (!BlueprintStorageCapture.TrySnapshotInventory(owner.destination, out inventory))
                    throw new InvalidOperationException("Current full loaded inventory is unreadable.");
            }
            private bool Current() => Thread.CurrentThread.ManagedThreadId == owner.ownerThread && !cancellation.IsCancellationRequested &&
                DateTime.UtcNow <= deadline && owner.JournalError == null && owner.ContextValid() && owner.DestinationReady() &&
                game && Game.instance == game && !game.IsShuttingDown() && profile != null && ReferenceEquals(game.GetPlayerProfile(), profile) &&
                profile.m_fileSource == FileHelpers.FileSource.Local && profile.GetPath() == path && profile.GetFilename() == filename && profile.GetName() == name &&
                owner.network.IsServer() && !owner.network.IsDedicated() && !ZNet.IsOpenServer() && owner.network.GetPeers().Count == 0 && !owner.network.IsSaving() &&
                ReferenceEquals(owner.network.GetWorld(), world) && world.m_fileSource == FileHelpers.FileSource.Local && generation != 0 && SaveSystem.GetSaveNumber() == generation &&
                !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) &&
                owner.zdo.GetString(BlueprintConstructionPartExecution.MarkerKey, "") == saved.Prepared.Parent.ToString("N") &&
                owner.zdo.GetByteArray(BlueprintConstructionPartExecution.ReceiptKey, null)?.SequenceEqual(receipt) == true &&
                BlueprintStorageCapture.EmptyState(owner.piece.gameObject, owner.player) &&
                InventoryDigest(owner.source) == saved.Observations.Last().SourceManifest &&
                BytesDigest(owner.zdo.GetByteArray(ZDOVars.s_items, null)) == saved.SavedPair.WorldInventoryHash &&
                BlueprintStorageCapture.TrySnapshotInventory(owner.destination, out var current) && current.Info == inventory.Info && current.Zdo == inventory.Zdo;

            internal void Start()
            {
                string error = null;
                if (!Current() || !BlueprintStoragePlayerProfile.TryVerifyLoaded(owner.player, saved.SavedPair, Current, out var first, out error))
                    throw new InvalidOperationException(error ?? "Saved return custody changed before durable readback.");
                profileHash = first.ProfileHash;
                if (!BlueprintStorageWorldGeneration.TryCapture(owner.OperationId, saved.Prepared.Parent, saved.Prepared.PrefabHash, saved.Prepared.Creator,
                        owner.zdo.GetByteArray(ZDOVars.s_items, null), Current, out storageRead, out error) ||
                    !BlueprintStorageWorldGeneration.TryCaptureReceipt(saved.Prepared.Parent, saved.Prepared.PrefabHash, saved.Prepared.Creator,
                        BlueprintConstructionPartExecution.MarkerKey, BlueprintConstructionPartExecution.ReceiptKey, receipt, Current, out receiptRead, out error) ||
                    storageRead.Generation != generation || receiptRead.Generation != generation ||
                    !storageRead.TryStart(cancellation.Token, out error) || !receiptRead.TryStart(cancellation.Token, out error))
                    throw new InvalidOperationException(error ?? "Both current durable proofs must share one frozen world generation.");
            }
            internal PairedRecoveryResult Tick(out string failure)
            {
                failure = reason;
                if (Thread.CurrentThread.ManagedThreadId != owner.ownerThread)
                { failure = "Saved return pair must finish on its owning thread."; return PairedRecoveryResult.Refused; }
                if (result != PairedRecoveryResult.Pending) return result;
                try
                {
                    if (!Current()) throw new InvalidOperationException("Saved return context, inventory or generation changed.");
                    if (!storageRead.IsCompleted || !receiptRead.IsCompleted) return result;
                    if (!storageRead.TryFinish(out var storage, out string error) || !receiptRead.TryFinish(out var construction, out error) ||
                        storageRead.Generation != generation || receiptRead.Generation != generation || storage.World != saved.Prepared.World ||
                        storage.Operation != saved.Prepared.Parent || storage.InventorySha256 != saved.SavedPair.WorldInventoryHash ||
                        construction.World != saved.Prepared.World || construction.Operation != saved.Prepared.Parent || construction.PayloadSha256 != BytesDigest(receipt) ||
                        storage.Chunk != construction.Chunk || storage.ObjectOffset != construction.ObjectOffset ||
                        !BlueprintStoragePlayerProfile.TryVerifyLoaded(owner.player, saved.SavedPair, Current, out var final, out error) ||
                        final.ProfileHash != profileHash || final.InventoryHash != saved.SavedPair.PlayerInventoryHash || !Current())
                        throw new InvalidOperationException(error ?? "Current player, empty storage and paid receipt do not prove one durable outcome.");
                    owner.journal.RecordSettlement(owner.construction.Fingerprint);
                    if (!BlueprintStorageTransferJournal.Read(owner.JournalPath).Settled) throw new InvalidOperationException("Return settlement could not be read back.");
                    result = PairedRecoveryResult.Settled;
                }
                catch (Exception error) { reason = failure = error.Message; result = PairedRecoveryResult.Refused; Cancel(); }
                return result;
            }
            internal void Cancel() => cancellation.Cancel();
        }
    }
}
