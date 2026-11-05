using System;
using System.Linq;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Detach/observation only. Admission stays closed until saved custody, credit and cleanup are connected.
    internal sealed partial class BlueprintConstructionMountDetachExecution : BlueprintConstructionMountContext
    {
        private readonly Journal.MountRecord mount;
        private readonly BlueprintForeignPartData before, mounted;
        private readonly Guid operation = Guid.NewGuid();
        private readonly uint generation;
        private readonly DateTime deadline = DateTime.UtcNow.AddSeconds(10);
        private BlueprintConstructionPayoutSeal.MountDropObservation observation;
        private ItemDrop boundDrop;
        private ZNetView boundView;
        private ZDO boundData;
        private ZDOID boundId;
        private bool attempted;
        private ReturnSourceBinding returnBinding;
        internal bool Finished { get; private set; }
        internal string Status { get; private set; } = "prepared";
        internal string Reason { get; private set; }

        private BlueprintConstructionMountDetachExecution(Plans store, Guid project, Guid partId, bool resume = false) : base(store, project, partId)
        {
            generation = SaveSystem.GetSaveNumber(); mount = journal.ReadMount(project, partId);
            if (resume)
            {
                operation = mount?.Return?.Operation ?? Guid.Empty;
                generation = mount?.Return?.BeforeGeneration ?? 0;
                before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, mount?.Return?.BeforeInfo, mount?.Return?.BeforeBytes, null);
                mounted = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount?.MountedInfo, mount?.MountedBytes, null);
                Resume(); return;
            }
            if (mount == null || mount.Phase != Journal.Committed || mount.Return != null || mount.Player != parent.Player ||
                mount.ParentOperation != parent.Operation || mount.ParentFingerprint != parentFingerprint || mount.Saved == null ||
                generation < mount.Saved.Generation || !BlueprintItemStandCapture.TryCapture(piece, out mounted) || mounted == null ||
                mounted.Info != mount.MountedInfo || mounted.Zdo != mount.MountedBytes ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out before))
                throw new InvalidOperationException("Detach requires this payer's saved actual attachment, not an archive or pending return.");
            string error = null;
            if (!BeforeCustody() || !BlueprintStoragePlayerProfile.TryCapture(player, operation, BeforeCustody, out _, out error))
                throw new InvalidOperationException(error ?? "Detach profile/custody preflight failed.");
            journal.PrepareMountReturn(project, partId, mount.Operation, operation, parent.Player, generation, before, mounted, out bool prepared);
            if (!prepared) throw new InvalidOperationException("Existing return intent cannot issue another native detach.");
        }

        internal static bool TryBegin(Plans store, Guid project, Guid part, out BlueprintConstructionMountDetachExecution execution, out string reason)
        {
            execution = null; reason = null;
            try { execution = new BlueprintConstructionMountDetachExecution(store, project, part); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool IdleCustody()
        {
            if (!Context() || DateTime.UtcNow > deadline || SaveSystem.GetSaveNumber() != generation || generation == 0 || generation == uint.MaxValue ||
                ZNet.instance.IsSaving() || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) ||
                SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) || !stand.m_canBeRemoved || !BlueprintItemStandPlacement.IsIdleStand(stand) ||
                zdo.GetByteArray(BlueprintConstructionMountExecution.ReceiptKey)?.SequenceEqual(mount.Receipt) != true) return false;
            BlueprintConstructionPartExecution.RequireStoragePreflight(plan.World);
            return BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == before.Info && current.Zdo == before.Zdo;
        }
        private bool Reachable() => player.GetHoverObject()?.GetComponentInParent<ItemStand>() == stand &&
            player.m_maxInteractDistance > 0 && !float.IsInfinity(player.m_maxInteractDistance) &&
            target.GetComponentsInChildren<Collider>().Any(collider => collider && collider.enabled && !collider.isTrigger &&
                collider.GetComponentInParent<Piece>() == piece &&
                Vector3.Distance(player.GetEyePoint(), collider.ClosestPoint(player.GetEyePoint())) < player.m_maxInteractDistance);

        private bool BeforeCustody() => IdleCustody() && Reachable() &&
            (observation == null ? !BlueprintConstructionPayoutSeal.HasActiveDropObservation : observation.OwnsLease) &&
            BlueprintItemStandCapture.TryCapture(piece, out var actual) && actual != null && actual.Info == mounted.Info && actual.Zdo == mounted.Zdo;

        private bool IntentCurrent(BlueprintForeignPartData actual)
        {
            if (!IdleCustody() || actual?.Info != mounted.Info || actual.Zdo != mounted.Zdo ||
                observation?.HandlerSeen != true && !Reachable()) return false;
            var current = journal.ReadMount(plan.Id, part.Id); var pending = current?.Return;
            return current?.Operation == mount.Operation && current.Phase == Journal.Committed && current.ParentFingerprint == parentFingerprint &&
                current.MountedInfo == mounted.Info && current.MountedBytes == mounted.Zdo && pending != null && pending.Operation == operation &&
                pending.BeforeGeneration == generation && pending.BeforeInfo == before.Info && pending.BeforeBytes == before.Zdo &&
                (pending.Phase == Journal.MountReturnIntent && boundDrop == null || pending.Phase == Journal.MountReturnBound && boundDrop &&
                    boundView && ReferenceEquals(boundDrop.GetComponent<ZNetView>(), boundView) && boundView.IsValid() && boundView.IsOwner() && ReferenceEquals(boundView.GetZDO(), boundData) &&
                    boundData.m_uid == boundId && pending.ObjectUser == boundId.UserID && pending.ObjectId == boundId.ID);
        }
        private void BindIdentity(ItemDrop drop)
        {
            if (boundDrop || !IdleCustody() || !observation.OwnsLease || !ReferenceEquals(observation.Drop, drop) || !IntentCurrent(mounted))
                throw new InvalidOperationException("Only the first receiver inside the current durable detach may be bound.");
            var view = drop ? drop.GetComponent<ZNetView>() : null; var data = view ? view.GetZDO() : null;
            if (!view || !view.IsValid() || !view.IsOwner() || data == null || !data.Persistent || data.m_uid.IsNone())
                throw new InvalidOperationException("Actual native receiver identity is unavailable.");
            boundDrop = drop; boundView = view; boundData = data; boundId = data.m_uid;
            returnBinding = BindReturnSource(operation, drop);
            journal.BindMountReturnIdentity(plan.Id, part.Id, operation, boundId.UserID, boundId.ID, returnBinding.Epoch);
        }
        private bool ObservedCustody() => IdleCustody() && observation.Complete && IntentCurrent(mounted) &&
            observation.OwnsLease && DropCustody();

        private bool DropCustody() => (resumed || observation.Complete) && boundDrop && (resumed || ReferenceEquals(observation.Drop, boundDrop)) &&
            boundView && ReferenceEquals(boundDrop.GetComponent<ZNetView>(), boundView) && boundView.IsValid() && boundView.IsOwner() &&
            ReferenceEquals(boundView.GetZDO(), boundData) && boundData.m_uid == boundId && boundData.Persistent &&
            boundData.GetPrefab() == boundDrop.m_itemData.m_dropPrefab.name.GetStableHashCode() &&
            boundData.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey, "") == operation.ToString("N") &&
            boundData.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) == parent.Player && BlueprintConstructionPayoutSeal.IsHeld(boundDrop) &&
            returnBinding != null && returnBinding.Matches(mounted, resumed ? resumedDrop : observation.Observed) &&
            !stand.HaveAttachment() && BlueprintItemStandCapture.TryCapture(piece, out var empty) && empty == null;

        private bool ReturnSourceCustody(out Journal.MountRecord current)
        {
            current = null;
            if (!Finished || Status != "observed" || !Context() || !DropCustody() ||
                SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) ||
                !stand.m_canBeRemoved || !BlueprintItemStandPlacement.IsIdleStand(stand) ||
                zdo.GetByteArray(BlueprintConstructionMountExecution.ReceiptKey)?.SequenceEqual(mount.Receipt) != true) return false;
            BlueprintConstructionPartExecution.RequireStoragePreflight(plan.World);
            current = journal.ReadMount(plan.Id, part.Id); var returned = current?.Return;
            return current?.Operation == mount.Operation && current.Phase == Journal.Committed && current.ParentFingerprint == parentFingerprint &&
                current.MountedInfo == mounted.Info && current.MountedBytes == mounted.Zdo && returned != null && returned.Operation == operation &&
                (returned.Phase == Journal.MountReturnObserved || returned.Phase == Journal.MountReturnSaved) &&
                returned.BeforeGeneration == generation && returned.BeforeInfo == before.Info && returned.BeforeBytes == before.Zdo &&
                (resumed || returned.ObjectUser == boundId.UserID && returned.ObjectId == boundId.ID) &&
                returned.ObservedInfo == (resumed ? resumedDrop : observation.Observed).Info && returned.ObservedBytes == (resumed ? resumedDrop : observation.Observed).Zdo;
        }

        private bool PersistenceCustody() => ReturnSourceCustody(out _) &&
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var currentInventory) &&
            (currentInventory.Info == before.Info && currentInventory.Zdo == before.Zdo || resumed && BlueprintStorageLoadedInventory.Matches(inventory, before));

        internal bool TryBeginPersistence(out BlueprintConstructionProjectPersistence verification, out string reason) =>
            BlueprintConstructionProjectPersistence.TryBeginMountReturn(journal, plan.Id, part.Id, operation,
                PersistenceCustody, out verification, out reason);

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (!attempted)
                {
                    if (!BeforeCustody() || !IntentCurrent(mounted)) throw new InvalidOperationException("Prepared detach custody changed.");
                    // Reserve only immediately before dispatch; a cancelled unsent operation must not retain a global lease.
                    if (!BlueprintConstructionPayoutSeal.TryObserveMountDrop(player, stand, operation, IntentCurrent, BindIdentity, out observation, out string error))
                        throw new InvalidOperationException(error ?? "Durable detach observation could not be reserved; intent retained.");
                    attempted = true; Status = "awaiting-native";
                    if (!stand.Interact(player, true, false)) throw new InvalidOperationException("Native interaction refused or has an unknown outcome; no replay.");
                }
                if (observation.Error != null) throw new InvalidOperationException(observation.Error);
                if (!observation.HandlerFinished)
                {
                    if (!BeforeCustody() || !IntentCurrent(mounted)) throw new InvalidOperationException("Queued detach changed, expired or was cancelled; no replay.");
                    return;
                }
                if (!ObservedCustody()) throw new InvalidOperationException("Native detach did not prove the exact protected actual drop and empty holder.");
                journal.ObserveMountReturn(plan.Id, part.Id, operation, observation.Observed, emptyHolder: true);
                Finished = true; Status = "observed"; observation.Dispose();
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason)
        { if (Finished) return; observation?.Dispose(); Finished = true; Status = "reconciliation"; Reason = reason; }
    }
}
