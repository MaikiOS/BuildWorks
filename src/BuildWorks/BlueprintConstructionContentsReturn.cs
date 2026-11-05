using System;
using System.Linq;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // No cancellation intent until every current storage inventory has been returned or proved empty.
    internal sealed class BlueprintConstructionContentsReturn
    {
        private readonly Plans plans;
        private readonly Func<bool> context;
        private readonly Guid[] parts;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly long payer;
        private int index;
        private BlueprintStorageTransfer transfer;
        private BlueprintStoragePersistence persistence;
        internal Guid Project { get; }
        internal long Revision { get; }
        internal Guid Operation { get; }
        internal bool Finished { get; private set; }
        internal string Status { get; private set; } = "returning-contents";
        internal string Reason { get; private set; }

        internal BlueprintConstructionContentsReturn(Plans plans, Guid project, long revision, Guid operation, Func<bool> context)
        {
            this.plans = plans; this.context = context; Project = project; Revision = revision; Operation = operation;
            if (operation == Guid.Empty) throw new InvalidOperationException("Cancellation operation is required before returning contents.");
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null; payer = player ? player.GetPlayerID() : 0;
            RequireCurrent();
            var plan = plans.Load(project); var journal = new Parts(plans); var blueprint = plan.Blueprint;
            parts = plan.Parts.Where(part => {
                var row = journal.Read(project, part.Id);
                if (row?.RequiresReconciliation == true || row?.Phase == Parts.Committed && row.Player != payer)
                    throw new InvalidOperationException("Construction payment needs reconciliation before contents return.");
                if (row?.Phase != Parts.Committed) return false;
                var prefab = ZNetScene.instance.GetPrefab(blueprint.parts[part.SourceIndex].prefabName);
                if (!prefab) throw new InvalidOperationException("Paid construction prefab is unavailable.");
                return prefab.GetComponent<Container>();
            }).Select(part => part.Id).ToArray();
            Advance();
        }

        private void RequireCurrent()
        {
            var plan = plans.Load(Project);
            if (context == null || !context() || !player || player != Player.m_localPlayer || player.IsDead() ||
                player.GetPlayerID() != payer || !ReferenceEquals(player.GetInventory(), inventory) || plan.Cancelled || plan.Revision != Revision)
                throw new InvalidOperationException("Contents return context or construction revision changed.");
            BlueprintConstructionCancellationJournal.RequireNotStarted(plans, Project);
        }

        private void Advance()
        {
            while (index < parts.Length)
            {
                RequireCurrent();
                if (!BlueprintStorageTransfer.TryBeginProjectReturn(plans, Project, parts[index], player, out transfer, out string reason))
                    throw new InvalidOperationException(reason);
                if (transfer != null) return;
                index++;
            }
            Finished = true; Status = "returned";
        }

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                RequireCurrent();
                if (transfer.RecoveringSavedPair)
                {
                    Status = "checking-saved-return";
                    var recovered = transfer.TickPairedReturnRecovery(out string reason);
                    if (recovered == BlueprintStorageTransfer.PairedRecoveryResult.Refused) throw new InvalidOperationException(reason);
                    if (recovered != BlueprintStorageTransfer.PairedRecoveryResult.Settled) return;
                }
                else
                {
                    if (persistence == null)
                    {
                        var result = transfer.MoveNext();
                        if (result == BlueprintStorageTransfer.Result.Uncertain || result == BlueprintStorageTransfer.Result.Unchanged)
                            throw new InvalidOperationException(transfer.JournalError ?? "Contents return needs reconciliation.");
                        if (!transfer.CompleteInMemory) return;
                        if (!transfer.TryBeginPersistence(out persistence, out string reason)) throw new InvalidOperationException(reason);
                        Status = "saving-contents-return";
                    }
                    var state = persistence.Tick();
                    if (state == BlueprintStoragePersistence.Result.Refused) throw new InvalidOperationException(persistence.Reason);
                    if (state != BlueprintStoragePersistence.Result.Verified) return;
                    if (!transfer.TrySettleConstruction(out string error)) throw new InvalidOperationException(error);
                }
                transfer.Dispose(); transfer = null; persistence = null; index++; Status = "returning-contents"; Advance();
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason) { transfer?.Dispose(); Finished = true; Status = "reconciliation"; Reason = reason; }
    }
}
