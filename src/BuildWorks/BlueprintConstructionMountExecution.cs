using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Attaches one real contribution to the already paid stand; never places or pays for the stand.
    internal sealed class BlueprintConstructionMountExecution : BlueprintConstructionMountContext
    {
        internal static readonly int ReceiptKey = "ostrix_buildworks_mount_receipt_v1".GetStableHashCode();
        private readonly BlueprintItemStandPlacement.Supply supply;
        private readonly BlueprintForeignPartData before, mounted;
        private readonly Guid operation;
        private Journal.MountRecord observed;
        private BlueprintForeignPartData after;
        private BlueprintConstructionProjectPersistence persistence;
        private bool attempted;
        private bool recovery, noEffectRecovery;
        private sealed class TargetBinding
        {
            internal Guid Epoch;
            internal GameObject Target;
            internal ZNetView View;
            internal ZDO Data;
            internal ZDOID Id;
            internal bool NativeFinished;
        }
        private static readonly Dictionary<Guid, TargetBinding> targets = new Dictionary<Guid, TargetBinding>();
        private static readonly Dictionary<ZDO, Guid> loadedTargets = new Dictionary<ZDO, Guid>();
        internal static void NativeTargetLoaded(ZDO data, Exception error)
        {
            if (error == null && data != null && NativeWorldEpoch != Guid.Empty &&
                Guid.TryParseExact(data.GetString(BlueprintConstructionPartExecution.MarkerKey, ""), "N", out _))
                loadedTargets[data] = NativeWorldEpoch;
        }
        internal static void ResetTargetBindings() { targets.Clear(); loadedTargets.Clear(); }
        private TargetBinding binding;
        private void BindTarget(bool resume)
        {
            Guid epoch = CurrentReturnEpoch();
            if (targets.TryGetValue(operation, out binding) && binding.Epoch == epoch)
            {
                if (!ReferenceEquals(binding.Target, target) || !ReferenceEquals(binding.View, view) ||
                    !ReferenceEquals(binding.Data, zdo) || binding.Id != nativeId)
                    throw new InvalidOperationException("Mount target was substituted in the same loaded world.");
                return;
            }
            if (resume && (!loadedTargets.TryGetValue(zdo, out var loaded) || loaded != epoch))
                throw new InvalidOperationException("Mount recovery requires its exact original target or an actual native world load.");
            binding = new TargetBinding { Epoch = epoch, Target = target, View = view, Data = zdo, Id = nativeId };
            targets[operation] = binding;
        }
        internal bool Finished { get; private set; }
        internal string Status { get; private set; } = "prepared";
        internal string Reason { get; private set; }

        private BlueprintConstructionMountExecution(Plans store, Guid project, Guid partId, bool resume, out bool awaitingSupply) : base(store, project, partId)
        {
            awaitingSupply = false;
            if (resume)
            {
                recovery = true;
                observed = journal.ReadMount(project, partId);
                if (observed == null || observed.Return != null || observed.Player != parent.Player ||
                    observed.ParentOperation != parent.Operation || observed.ParentFingerprint != parentFingerprint)
                    throw new InvalidOperationException("Existing attachment identity differs or already belongs to return.");
                operation = observed.Operation;
                before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, observed.BeforeInfo, observed.BeforeBytes, null);
                mounted = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, observed.MountedInfo, observed.MountedBytes, null);
                BindTarget(true);
                Resume(); return;
            }
            var demand = plan.Blueprint.parts[part.SourceIndex].foreignData.ToGeometry();
            if (!Context() || !EmptyTarget() || zdo.GetByteArray(ReceiptKey) != null || ZNet.instance.IsSaving() ||
                parent.Saved == null || SaveSystem.GetSaveNumber() < parent.Saved.Generation ||
                SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter))
                throw new InvalidOperationException("Mount requires the unchanged empty paid target and an idle savable private Local host.");
            var supplies = new List<BlueprintItemStandPlacement.Supply>();
            if (!BlueprintItemStandPlacement.SelectSupplies(player, new[] { demand }, supplies) || supplies.Count != 1 || supplies[0]?.Item == null)
            { awaitingSupply = true; throw new InvalidOperationException("The actual requested item is not available in the payer's inventory."); }
            supply = supplies[0];
            var copy = supply.Item.Clone(); copy.m_equipped = false;
            mounted = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, supply.Contents.Info,
                BlueprintItemStandPlacement.SnapshotItem(copy), null);
            if (!BlueprintItemStandPlacement.CanRestore(prefab.GetComponent<Piece>(), mounted) ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out before))
                throw new InvalidOperationException("The actual supplied item or exact inventory snapshot is unsupported.");
            operation = Guid.NewGuid();
            string error = null;
            if (!BeforeCustody() || !BlueprintStoragePlayerProfile.TryCapture(player, operation, BeforeCustody, out _, out error))
                throw new InvalidOperationException(error ?? "Mount profile preflight failed.");
            journal.PrepareMount(project, partId, operation, player.GetPlayerID(), nativeId.UserID, nativeId.ID,
                before, mounted, emptyTarget: true, out bool prepared);
            if (!prepared) throw new InvalidOperationException("Existing mount operation cannot be replayed.");
            BindTarget(false);
        }

        internal static bool TryResume(Plans store, Guid project, Guid part, out BlueprintConstructionMountExecution execution, out string reason)
        {
            execution = null; reason = null;
            try { execution = new BlueprintConstructionMountExecution(store, project, part, true, out _); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool RawMounted() => Context() && BlueprintItemStandPlacement.IsIdleStand(stand) &&
            BlueprintItemStandCapture.TryCapture(piece, out var actual) && actual != null && actual.Info == mounted.Info && actual.Zdo == mounted.Zdo;
        private bool RecoveryInventory(BlueprintForeignPartData expected) => InventoryMatches(expected) ||
            loadedTargets.TryGetValue(zdo, out var epoch) && epoch == CurrentReturnEpoch() && BlueprintStorageLoadedInventory.Matches(inventory, expected);
        private void Resume()
        {
            if (!Context() || !BlueprintItemStandPlacement.IsIdleStand(stand)) throw new InvalidOperationException("Attachment queue or context is unresolved; no native replay.");
            if (observed.Phase == Journal.Committed)
            {
                if (!RawMounted() || zdo.GetByteArray(ReceiptKey)?.SequenceEqual(observed.Receipt) != true ||
                    observed.Saved == null || SaveSystem.GetSaveNumber() < observed.Saved.Generation)
                    throw new InvalidOperationException("Committed attachment holder or saved receipt differs.");
                Finished = true; Status = "committed"; return;
            }
            if (EmptyTarget() && RecoveryInventory(before) && zdo.GetByteArray(ReceiptKey) == null &&
                (observed.Phase == Journal.Intent || observed.Phase == Journal.NoEffect))
            {
                if (binding.NativeFinished)
                { journal.MountNoEffect(plan.Id, part.Id, operation, before, true); Finished = true; Status = "no-effect"; return; }
                if (!loadedTargets.TryGetValue(zdo, out var loaded) || loaded != CurrentReturnEpoch())
                    throw new InvalidOperationException("Empty holder cannot prove completion of the former native call.");
                noEffectRecovery = true;
                if (!BlueprintConstructionProjectPersistence.TryReadMountNoEffect(journal, plan.Id, part.Id, operation,
                    () => Context() && EmptyTarget() && BlueprintItemStandPlacement.IsIdleStand(stand) &&
                        zdo.GetByteArray(ReceiptKey) == null && RecoveryInventory(before), out persistence, out var error)) throw new InvalidOperationException(error);
                Status = "checking-saved-mount"; return;
            }
            if (!RawMounted()) throw new InvalidOperationException("Existing native attachment does not match its exact recorded item.");
            if (observed.Phase == Journal.Intent)
            {
                if (!BlueprintItemStandContents.TryRead(mounted, name => name.GetStableHashCode(), out var item))
                    throw new InvalidOperationException("Recorded supply is unreadable.");
                after = BlueprintContainerInventory.DebitOne(before, item.X, item.Y, name => name.GetStableHashCode());
                if (!RecoveryInventory(after)) throw new InvalidOperationException("Actual inventory is not the single recorded debit.");
                if (zdo.GetByteArray(ReceiptKey) != null) throw new InvalidOperationException("Unobserved attachment has a conflicting receipt.");
                observed = journal.ObserveMount(plan.Id, part.Id, operation, after, mounted);
            }
            else if (observed.Phase == Journal.Observed)
                after = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, observed.AfterInfo, observed.AfterBytes, null);
            else throw new InvalidOperationException("No-effect cannot become an attachment through recovery.");
            if (!RecoveryInventory(after)) throw new InvalidOperationException("Observed attachment inventory differs.");
            var receipt = zdo.GetByteArray(ReceiptKey);
            if (receipt != null && !receipt.SequenceEqual(observed.Receipt)) throw new InvalidOperationException("Conflicting child receipt cannot be overwritten.");
            if (receipt == null) zdo.Set(ReceiptKey, observed.Receipt);
            if (!BlueprintConstructionProjectPersistence.TryBeginMount(journal, plan.Id, part.Id, operation, Custody,
                out persistence, out var reason)) throw new InvalidOperationException(reason);
            Status = "checking-saved-mount";
        }

        internal static bool TryBegin(Plans store, Guid project, Guid part, out BlueprintConstructionMountExecution execution, out string reason, out bool awaitingSupply)
        {
            execution = null; reason = null; awaitingSupply = false;
            try { execution = new BlueprintConstructionMountExecution(store, project, part, false, out awaitingSupply); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool EmptyTarget() => !stand.HaveAttachment() && !stand.IsInvoking("UpdateAttach") &&
            BlueprintItemStandPlacement.CanRollback(target, forceDestroy: true) &&
            BlueprintItemStandCapture.TryCapture(piece, out var contents) && contents == null;
        private bool InventoryMatches(BlueprintForeignPartData expected) =>
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == expected.Info && current.Zdo == expected.Zdo;
        private bool BeforeCustody()
        {
            if (!Context()) return false;
            BlueprintConstructionPartExecution.RequireStoragePreflight(plan.World);
            return !ZNet.instance.IsSaving() && SaveSystem.GetSaveNumber() != 0 && SaveSystem.GetSaveNumber() != uint.MaxValue &&
                !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) &&
                EmptyTarget() && zdo.GetByteArray(ReceiptKey) == null && BlueprintItemStandPlacement.IsAvailable(player, supply) && InventoryMatches(before);
        }
        private bool Custody() => Context() && BlueprintItemStandPlacement.IsIdleStand(stand) && observed != null &&
            zdo.GetByteArray(ReceiptKey)?.SequenceEqual(observed.Receipt) == true &&
            BlueprintItemStandCapture.TryCapture(piece, out var actual) && actual != null &&
            actual.Info == observed.MountedInfo && actual.Zdo == observed.MountedBytes &&
            (recovery ? RecoveryInventory(after) : InventoryMatches(after));

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (persistence == null)
                {
                    if (attempted || !BeforeCustody()) throw new InvalidOperationException("Prepared mount custody changed; native replay refused.");
                    attempted = true;
                    bool applied;
                    try { applied = BlueprintItemStandPlacement.TryApply(player, target, mounted, out _, out _, supply); }
                    finally { binding.NativeFinished = true; }
                    if (!applied)
                    {
                        if (!BeforeCustody()) throw new InvalidOperationException("Native mount outcome is uncertain; retained intent requires reconciliation.");
                        journal.MountNoEffect(plan.Id, part.Id, operation, before, emptyTarget: true);
                        Finished = true; Status = "no-effect"; return;
                    }
                    if (!Context() || !BlueprintItemStandCapture.TryCapture(piece, out var actual) ||
                        !BlueprintStorageCapture.TrySnapshotInventory(inventory, out after))
                        throw new InvalidOperationException("Native mount readback is unavailable.");
                    observed = journal.ObserveMount(plan.Id, part.Id, operation, after, actual);
                    zdo.Set(ReceiptKey, observed.Receipt);
                    string reason = null;
                    if (!Custody() || !BlueprintConstructionProjectPersistence.TryBeginMount(journal, plan.Id, part.Id, operation,
                        Custody, out persistence, out reason)) throw new InvalidOperationException(reason ?? "Mount custody differs.");
                    Status = "saving";
                }
                var result = persistence.Tick();
                if (result == BlueprintConstructionProjectPersistence.Result.Refused) throw new InvalidOperationException(persistence.Reason);
                if (result == BlueprintConstructionProjectPersistence.Result.Verified) { Finished = true; Status = noEffectRecovery ? "no-effect" : "committed"; }
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason) { if (Finished) return; persistence?.Cancel(); Finished = true; Status = "reconciliation"; Reason = reason; }
    }
}
