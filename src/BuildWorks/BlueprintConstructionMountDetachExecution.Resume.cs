using System;
using System.Linq;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionMountDetachExecution
    {
        private bool resumed;
        private BlueprintForeignPartData resumedDrop;
        private void Resume()
        {
            resumed = true;
            if (mount?.Phase != Journal.Committed || mount.Return == null || mount.ParentFingerprint != parentFingerprint)
                throw new InvalidOperationException("No matching existing return to resume.");
            if (mount.Return.Phase == Journal.MountReturnComplete) { Finished = true; Status = "terminal"; return; }
            var found = UnityEngine.Object.FindObjectsByType<ItemDrop>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)
                .Where(drop => drop.GetComponent<ZNetView>()?.GetZDO()?.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey, "") == operation.ToString("N")).ToArray();
            if (found.Length != 1 || !Context() || stand.HaveAttachment())
                throw new InvalidOperationException("Resume requires one protected actual drop and empty paid holder; no new detach.");
            boundDrop = found[0]; boundView = boundDrop.GetComponent<ZNetView>(); boundData = boundView.GetZDO(); boundId = boundData.m_uid;
            returnBinding = ResolveReturnSource(mount, boundDrop);
            var raw = boundData.GetByteArray(ZDOVars.s_itemData);
            if (raw == null) throw new InvalidOperationException("Return raw packet is unavailable.");
            resumedDrop = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                mount.Return.ObservedInfo ?? string.Join(":", mounted.Info.Split(':').Take(3)) + ":0",
                mount.Return.ObservedBytes ?? Convert.ToBase64String(raw), null);
            if (!returnBinding.Matches(mounted, resumedDrop)) throw new InvalidOperationException("Loaded source differs from immutable return evidence.");
            if (mount.Return.Phase < Journal.MountReturnObserved)
            {
                if (SaveSystem.GetSaveNumber() < generation || !BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) ||
                    !BlueprintStorageLoadedInventory.Matches(inventory, before) && (current.Info != before.Info || current.Zdo != before.Zdo))
                    throw new InvalidOperationException("Incomplete return has changed inventory or world.");
                // UID is a session witness only; persisted markers survive native Load's ID reassignment.
                if (mount.Return.Phase == Journal.MountReturnIntent)
                    journal.BindMountReturnIdentity(plan.Id, part.Id, operation, boundId.UserID, boundId.ID, returnBinding.Epoch);
                journal.ObserveMountReturn(plan.Id, part.Id, operation, resumedDrop, true);
            }
            Finished = true; Status = "observed";
            if (!ReturnSourceCustody(out _)) throw new InvalidOperationException("Resumed source custody differs.");
        }
        internal static bool TryResume(Plans store, Guid project, Guid part, out BlueprintConstructionMountDetachExecution execution, out string reason)
        {
            execution = null; reason = null;
            try { execution = new BlueprintConstructionMountDetachExecution(store, project, part, true); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }
        internal bool TryRecoverCredit(out string reason)
        {
            reason = null;
            try
            {
                if (!ReturnSourceCustody(out var current) || current.Return.Credit == null || !Context() ||
                    !BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, () => ReturnSourceCustody(out _), out var proof, out string error))
                    throw new InvalidOperationException("Credit recovery requires the exact source and canonical receipt; never insert again.");
                journal.RecoverMountReturnCredit(plan.Id, part.Id, operation, proof); return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
