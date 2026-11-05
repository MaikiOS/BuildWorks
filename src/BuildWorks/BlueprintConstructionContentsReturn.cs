using System;
using System.Linq;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // No cancellation intent until every current storage inventory has been returned or proved empty.
    internal sealed class BlueprintConstructionContentsReturn
    {
#if BUILDWORKS_TEST_STAND
        private static Guid qaArmedMount, qaPausedReturn;
        internal static string QaPauseState => qaArmedMount.ToString("N") + ":" + qaPausedReturn.ToString("N");
        internal static bool QaPauseArmed => qaArmedMount != Guid.Empty;
        internal static void QaArmPause(Guid mount)
        {
            if (mount == Guid.Empty || QaPauseArmed) throw new InvalidOperationException("One explicit mount operation may be armed at a time.");
            qaArmedMount = mount;
        }
        internal static void QaReleasePause() { qaArmedMount = Guid.Empty; qaPausedReturn = Guid.Empty; }
        private static bool QaHoldCredit(Parts.MountRecord mount)
        {
            if (mount.Operation != qaArmedMount || mount.Return?.Credit?.Phase != Parts.MountProfileCredited) return false;
            qaPausedReturn = mount.Return.Operation; return true;
        }
#endif
        private readonly Plans plans;
        private readonly Func<bool> context;
        private readonly Guid[] parts;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly long payer;
        private int index;
        private BlueprintStorageTransfer transfer;
        private BlueprintStoragePersistence persistence;
        private Guid mountPart;
        private BlueprintConstructionMountDetachExecution detach;
        private BlueprintConstructionProjectPersistence mountPersistence;
        private BlueprintConstructionMountReturnCleanup mountCleanup;
        private BlueprintConstructionMountExecution attachRecovery;
        internal bool Busy => !Finished && (transfer != null || detach != null || mountCleanup != null || attachRecovery != null);
        internal int Remaining => parts.Skip(index).Count(id => IsStand(id) && !MountTerminal(id));
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
                return prefab.GetComponent<Container>() || prefab.GetComponent<ItemStand>();
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
                var plan = plans.Load(Project);
                var part = plan.Parts.First(value => value.Id == parts[index]);
                var prefab = ZNetScene.instance.GetPrefab(plan.Blueprint.parts[part.SourceIndex].prefabName);
                if (prefab.GetComponent<ItemStand>())
                {
                    if (MountTerminal(part.Id)) { index++; continue; }
                    var mount = new Parts(plans).ReadMount(Project, part.Id);
                    if (mount == null || mount.Phase == Parts.NoEffect)
                        Reason = BuildWorksLocalization.Text("project.mount_return_untracked");
                    Status = "awaiting-stand"; return;
                }
                if (!BlueprintStorageTransfer.TryBeginProjectReturn(plans, Project, parts[index], player, out transfer, out string reason))
                    throw new InvalidOperationException(reason);
                if (transfer != null) return;
                index++;
            }
            Finished = true; Status = "returned";
        }

        private bool MountTerminal(Guid id)
        {
            var mount = new Parts(plans).ReadMount(Project, id);
            if (mount?.Return?.Phase == Parts.MountReturnComplete) return true;
            if (mount != null && mount.Phase != Parts.NoEffect) return false;
            var plan = plans.Load(Project); var part = plan.Parts.First(value => value.Id == id);
            var parent = new Parts(plans).Read(Project, id);
            return BlueprintConstructionCancellationRemoval.EmptyMount(plans, Project, id,
                BlueprintConstructionPartExecution.FindMarked(parent.Operation, plan.Blueprint.parts[part.SourceIndex].prefabName));
        }
        private bool IsStand(Guid id)
        {
            var plan = plans.Load(Project); var part = plan.Parts.First(value => value.Id == id);
            var prefab = ZNetScene.instance.GetPrefab(plan.Blueprint.parts[part.SourceIndex].prefabName);
            return prefab && prefab.GetComponent<ItemStand>();
        }

        internal bool IsPendingStand(ItemStand stand)
        {
            if (Finished || !stand) return false;
            try
            {
                RequireCurrent();
                var data = stand.GetComponent<ZNetView>()?.GetZDO();
                if (data == null) return false;
                var journal = new Parts(plans);
                return parts.Skip(index).Any(id => {
                    var mount = journal.ReadMount(Project, id);
                    return !MountTerminal(id) && mount != null && mount.Phase != Parts.NoEffect &&
                        journal.Read(Project, id)?.Operation.ToString("N") == data.GetString(BlueprintConstructionPartExecution.MarkerKey, "");
                });
            }
            catch (Exception) { return false; }
        }

        internal bool TryBeginStand(ItemStand stand, Humanoid user, out string reason)
        {
            reason = null;
            try
            {
                RequireCurrent();
                if (Busy || user != player || !IsPendingStand(stand) ||
                    player.GetHoverObject()?.GetComponentInParent<ItemStand>() != stand)
                    throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
                var journal = new Parts(plans);
                string marker = stand.GetComponent<ZNetView>().GetZDO().GetString(BlueprintConstructionPartExecution.MarkerKey, "");
                mountPart = parts.Skip(index).Single(id => journal.Read(Project, id)?.Operation.ToString("N") == marker);
                var mount = journal.ReadMount(Project, mountPart);
                if (mount != null && mount.Return == null && mount.Phase != Parts.Committed)
                {
                    if (!BlueprintConstructionMountExecution.TryResume(plans, Project, mountPart, out attachRecovery, out reason))
                        throw new InvalidOperationException(reason);
                }
                else if (mount?.Return?.Credit?.Phase == Parts.MountProfileCredited)
                {
                    if (!BlueprintConstructionMountReturnCleanup.TryBegin(plans, Project, mountPart, out mountCleanup, out reason))
                        throw new InvalidOperationException(reason);
                }
                else if (!(mount?.Return != null
                    ? BlueprintConstructionMountDetachExecution.TryResume(plans, Project, mountPart, out detach, out reason)
                    : BlueprintConstructionMountDetachExecution.TryBegin(plans, Project, mountPart, out detach, out reason)))
                    throw new InvalidOperationException(reason);
                Status = "returning-stand"; return true;
            }
            catch (Exception error) { reason = error.Message; Reason = reason; return false; }
        }

        private void TickMount()
        {
            if (attachRecovery != null)
            {
                attachRecovery.Tick();
                if (!attachRecovery.Finished) return;
                if (attachRecovery.Status == "no-effect") { attachRecovery = null; Advance(); return; }
                if (attachRecovery.Status != "committed") throw new InvalidOperationException(attachRecovery.Reason);
                attachRecovery = null;
                if (!BlueprintConstructionMountDetachExecution.TryBegin(plans, Project, mountPart, out detach, out var error))
                    throw new InvalidOperationException(error);
            }
            if (mountCleanup != null)
            {
                mountCleanup.Tick();
                if (!mountCleanup.Finished) return;
                if (mountCleanup.Status != "terminal") throw new InvalidOperationException(mountCleanup.Reason);
                mountCleanup = null; detach = null; mountPersistence = null; mountPart = Guid.Empty;
                Reason = null; Advance(); return;
            }
            detach.Tick();
            if (!detach.Finished) return;
            if (detach.Status != "observed") throw new InvalidOperationException(detach.Reason);
            var mount = new Parts(plans).ReadMount(Project, mountPart);
#if BUILDWORKS_TEST_STAND
            if (QaHoldCredit(mount)) { Status = "qa-paused-credited"; return; }
#endif
            string reason;
            if (mount.Return.Phase != Parts.MountReturnSaved)
            {
                if (mountPersistence == null && !detach.TryBeginPersistence(out mountPersistence, out reason))
                    throw new InvalidOperationException(reason);
                var result = mountPersistence.Tick();
                if (result == BlueprintConstructionProjectPersistence.Result.Refused) throw new InvalidOperationException(mountPersistence.Reason);
                if (result != BlueprintConstructionProjectPersistence.Result.Verified) return;
                mount = new Parts(plans).ReadMount(Project, mountPart);
            }
            if (!(mount.Return.Credit == null ? detach.TryCredit(out reason) : detach.TryRecoverCredit(out reason)))
                throw new InvalidOperationException(reason);
#if BUILDWORKS_TEST_STAND
            if (QaHoldCredit(new Parts(plans).ReadMount(Project, mountPart))) { Status = "qa-paused-credited"; return; }
#endif
            if (!BlueprintConstructionMountReturnCleanup.TryBegin(plans, Project, mountPart, out mountCleanup, out reason))
                throw new InvalidOperationException(reason);
        }

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                RequireCurrent();
                if (detach != null || mountCleanup != null || attachRecovery != null) { TickMount(); return; }
                if (transfer == null) { Advance(); return; }
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
        internal void Refuse(string reason)
        {
            transfer?.Dispose(); attachRecovery?.Refuse(reason); detach?.Refuse(reason); mountPersistence?.Cancel(); mountCleanup?.Cancel(reason);
            Finished = true; Status = "reconciliation"; Reason = reason;
#if BUILDWORKS_TEST_STAND
            QaReleasePause();
#endif
        }
    }
}
