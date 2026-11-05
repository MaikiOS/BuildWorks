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
    // Admission stays closed until exact return is connected; this actor never places or pays for the stand.
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
        internal bool Finished { get; private set; }
        internal string Status { get; private set; } = "prepared";
        internal string Reason { get; private set; }

        private BlueprintConstructionMountExecution(Plans store, Guid project, Guid partId) : base(store, project, partId)
        {
            var demand = plan.Blueprint.parts[part.SourceIndex].foreignData.ToGeometry();
            if (!Context() || !EmptyTarget() || zdo.GetByteArray(ReceiptKey) != null || ZNet.instance.IsSaving() ||
                SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter))
                throw new InvalidOperationException("Mount requires the unchanged empty paid target and an idle savable private Local host.");
            var supplies = new List<BlueprintItemStandPlacement.Supply>();
            if (!BlueprintItemStandPlacement.SelectSupplies(player, new[] { demand }, supplies) || supplies.Count != 1 || supplies[0]?.Item == null)
                throw new InvalidOperationException("The actual requested item is not available in the payer's inventory.");
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
        }

        internal static bool TryBegin(Plans store, Guid project, Guid part, out BlueprintConstructionMountExecution execution, out string reason)
        {
            execution = null; reason = null;
            try { execution = new BlueprintConstructionMountExecution(store, project, part); return true; }
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
        private bool Custody() => Context() && observed != null &&
            zdo.GetByteArray(ReceiptKey)?.SequenceEqual(observed.Receipt) == true &&
            BlueprintItemStandCapture.TryCapture(piece, out var actual) && actual != null &&
            actual.Info == observed.MountedInfo && actual.Zdo == observed.MountedBytes &&
            InventoryMatches(after);

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (persistence == null)
                {
                    if (attempted || !BeforeCustody()) throw new InvalidOperationException("Prepared mount custody changed; native replay refused.");
                    attempted = true;
                    if (!BlueprintItemStandPlacement.TryApply(player, target, mounted, out _, out _, supply))
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
                if (result == BlueprintConstructionProjectPersistence.Result.Verified) { Finished = true; Status = "committed"; }
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason) { if (Finished) return; persistence?.Cancel(); Finished = true; Status = "reconciliation"; Reason = reason; }
    }
}
