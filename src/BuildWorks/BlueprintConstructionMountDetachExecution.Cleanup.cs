using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    internal sealed class BlueprintConstructionMountReturnCleanup : BlueprintConstructionMountContext
    {
        private Journal.MountRecord mount;
        private readonly ZDOMan manager = ZDOMan.instance;
        private readonly ZNetScene scene = ZNetScene.instance;
        private readonly Guid operation;
        private readonly BlueprintForeignPartData inventoryBefore;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private BlueprintStorageWorldGeneration scan;
        private ItemDrop source;
        private ZDO data;
        private ReturnSourceBinding returnBinding;
        private ZDOID id;
        private uint scanGeneration, saveBefore;
        private readonly uint sessionGeneration = SaveSystem.GetSaveNumber();
        internal string Status { get; private set; }
        internal string Reason { get; private set; }
        internal bool Finished => Status == "terminal" || Status == "reconciliation";
        private BlueprintConstructionMountReturnCleanup(Plans store, Guid project, Guid part) : base(store, project, part)
        {
            mount = journal.ReadMount(project, part); operation = mount?.Return?.Operation ?? Guid.Empty;
            if (operation == Guid.Empty || mount.ParentFingerprint != parentFingerprint || mount.Return.Credit?.Phase != Journal.MountProfileCredited ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out inventoryBefore) || !Custody() || !Receipt(out var receipt, out string error))
                throw new InvalidOperationException("Cleanup requires saved credit and the same canonical payer receipt.");
            var cleanup = mount.Return.Cleanup;
            if (cleanup == null)
            {
                if (Sources().Length != 1) throw new InvalidOperationException("New cleanup requires its actual source.");
                mount = journal.BeginMountCleanup(project, part, operation, SaveSystem.GetSaveNumber(), receipt);
            }
            cleanup = mount.Return.Cleanup;
            if (SaveSystem.GetSaveNumber() < cleanup.BeforeGeneration) throw new InvalidOperationException("World predates cleanup intent.");
            var sources = Sources();
            if (mount.Return.Phase == Journal.MountReturnComplete || cleanup.Dispatched)
            {
                if (sources.Length != 0) throw new InvalidOperationException("Previously attempted deletion has a source; no repeated native destruction.");
                if (SaveSystem.GetSaveNumber() > cleanup.BeforeGeneration) StartAbsence();
                else SaveAbsence();
                return;
            }
            if (sources.Length != 1) throw new InvalidOperationException("Cleanup needs one exact protected source, never a replacement.");
            source = sources[0]; returnBinding = ResolveReturnSource(mount, source); data = source.GetComponent<ZNetView>()?.GetZDO(); id = data?.m_uid ?? ZDOID.None;
            if (!SourceCurrent() || SaveSystem.GetSaveNumber() != sessionGeneration)
                throw new InvalidOperationException("Cleanup source or recorded generation changed.");
            var item = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.Return.ObservedInfo, mount.Return.ObservedBytes, null);
            if (!BlueprintItemStandContents.TryRead(item, name => name.GetStableHashCode(), out var packet) ||
                !BlueprintStorageWorldGeneration.TryCaptureBlob(operation, packet.Prefab.GetStableHashCode(), mount.Player,
                    BlueprintConstructionPayoutSeal.OwnerKey, BlueprintConstructionPayoutSeal.MountMarkerKey, ZDOVars.s_itemData,
                    Convert.FromBase64String(item.Zdo), SourceCurrent, out scan, out error) || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException(error ?? "Cleanup saved source readback failed.");
            Status = "reading-source";
        }
        internal static bool TryBegin(Plans store, Guid project, Guid part, out BlueprintConstructionMountReturnCleanup execution, out string reason)
        {
            execution = null; reason = null;
            try { execution = new BlueprintConstructionMountReturnCleanup(store, project, part); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }
        private ItemDrop[] Sources() => UnityEngine.Object.FindObjectsByType<ItemDrop>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None)
            .Where(drop => drop.GetComponent<ZNetView>()?.GetZDO()?.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey, "") == operation.ToString("N")).ToArray();
        private bool Custody() => Context() && ReferenceEquals(manager, ZDOMan.instance) && scene && scene == ZNetScene.instance &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) &&
            stand.m_canBeRemoved && BlueprintItemStandPlacement.IsIdleStand(stand) && !stand.HaveAttachment() && BlueprintItemStandCapture.TryCapture(piece, out var empty) && empty == null &&
            zdo.GetByteArray(BlueprintConstructionMountExecution.ReceiptKey)?.SequenceEqual(mount.Receipt) == true &&
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == inventoryBefore.Info && current.Zdo == inventoryBefore.Zdo &&
            player.m_customData.TryGetValue(Journal.MountCreditKey(operation), out var receipt) && receipt == Journal.MountCreditValue(mount);
        private bool Receipt(out NativePlayerProfileReadback.CustomDataEvidence proof, out string reason) =>
            BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, Custody, out proof, out reason) && proof.Player == mount.Player &&
            proof.CustomData.TryGetValue(Journal.MountCreditKey(operation), out var receipt) && receipt == Journal.MountCreditValue(mount);
        private bool SourceCurrent()
        {
            if (!Custody() || ZNet.instance.IsSaving() || SaveSystem.GetSaveNumber() != sessionGeneration || !source || data == null || !ReferenceEquals(manager.GetZDO(id), data) ||
                data.m_uid != id || id.IsNone() || !data.Persistent || Sources().Length != 1 ||
                !BlueprintConstructionPayoutSeal.IsHeld(source)) return false;
            var view = source.GetComponent<ZNetView>();
            return view && view.IsValid() && view.IsOwner() && ReferenceEquals(view.GetZDO(), data) &&
                data.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) == mount.Player &&
                data.GetPrefab() == source.m_itemData.m_dropPrefab.name.GetStableHashCode() &&
                data.GetString(BlueprintConstructionPayoutSeal.MountMarkerKey, "") == operation.ToString("N") &&
                returnBinding.Matches(new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.MountedInfo, mount.MountedBytes, null),
                    new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.Return.ObservedInfo, mount.Return.ObservedBytes, null));
        }
        private void SaveAbsence()
        {
            if (!Custody() || Sources().Length != 0 || !Receipt(out _, out string error) || ZNet.instance.IsSaving() ||
                SaveSystem.GetSaveNumber() != sessionGeneration)
                throw new InvalidOperationException("Absence save context differs.");
            saveBefore = SaveSystem.GetSaveNumber();
            ZNet.instance.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
            Status = "saving";
        }
        private void StartAbsence()
        {
            scanGeneration = SaveSystem.GetSaveNumber();
            bool Current() => Custody() && Sources().Length == 0 && !ZNet.instance.IsSaving() && SaveSystem.GetSaveNumber() == scanGeneration;
            if (scanGeneration <= mount.Return.Cleanup.BeforeGeneration || !Current() ||
                !BlueprintStorageWorldGeneration.TryCaptureAbsence(operation, BlueprintConstructionPayoutSeal.MountMarkerKey, new[] { operation },
                    Current, out scan, out string error) || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException("Complete mapped absence could not start.");
            Status = "reading-absence";
        }
        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (!Custody() || DateTime.UtcNow > deadline) throw new InvalidOperationException("Cleanup custody changed or timed out.");
                if (Status == "reading-source")
                {
                    if (!SourceCurrent() || SaveSystem.GetSaveNumber() != sessionGeneration) throw new InvalidOperationException("Cleanup source changed.");
                    if (!scan.IsCompleted) return;
                    if (!scan.TryFinish(out var saved, out string error) || saved.World != mount.World || saved.Operation != operation ||
                        !Receipt(out _, out error)) throw new InvalidOperationException(error ?? "Saved source/receipt differs.");
                    mount = journal.DispatchMountCleanup(plan.Id, part.Id, operation);
                    bool Fence()
                    {
                        if (!SourceCurrent() || !Receipt(out _, out _)) return false;
                        var durable = journal.ReadMount(plan.Id, part.Id);
                        return durable?.Return?.Operation == operation && durable.Return.Cleanup?.Dispatched == true &&
                            durable.Return.Cleanup.BeforeGeneration == mount.Return.Cleanup.BeforeGeneration &&
                            Journal.MountCreditValue(durable) == Journal.MountCreditValue(mount);
                    }
                    BlueprintConstructionPayoutCleanup.DestroyExact(data, Fence);
                    Status = "queued-removal"; return;
                }
                if (Status == "queued-removal")
                {
                    if (ZNet.instance.IsSaving() || SaveSystem.GetSaveNumber() != sessionGeneration)
                        throw new InvalidOperationException("Save changed before native removal completed.");
                    if (manager.GetZDO(id) != null || scene.FindInstance(id)) return;
                    SaveAbsence(); return;
                }
                if (ZNet.instance.IsSaving()) return;
                if (Status == "saving") { if (SaveSystem.GetSaveNumber() != saveBefore + 1) throw new InvalidOperationException("Cleanup save did not commit the requested next generation."); StartAbsence(); return; }
                if (Status != "reading-absence" || !scan.IsCompleted) return;
                if (!scan.TryFinishAbsence(out var absence, out string reason) || absence.Generation != scanGeneration ||
                    !Receipt(out var proof, out reason) || Sources().Length != 0 || !Custody()) throw new InvalidOperationException(reason ?? "Absence or payer receipt differs.");
                mount = journal.CompleteMountReturn(plan.Id, part.Id, operation, absence, proof); Status = "terminal";
            }
            catch (Exception error) { Cancel(error.Message); }
        }
        internal void Cancel(string reason) { if (Finished) return; cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
    }
}
