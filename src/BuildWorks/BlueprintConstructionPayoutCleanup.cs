using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    // shortcut: private Local cleanup only; terminal cancellation and general rollback recovery remain separate.
    internal sealed class BlueprintConstructionPayoutCleanup
    {
        internal sealed class Witness
        {
            internal ZDO Data;
            internal ZDOID Id;
            internal ZDOMan Manager;
            internal int BeforeCount;
            internal Func<bool> Custody;
            internal bool Attempted, Confirmed;
        }
        private static Witness active;
        private static readonly FieldInfo DestroyQueue = typeof(ZDOMan).GetField("m_destroySendList", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Journal journal;
        private Journal.Record record;
        private readonly int index, thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly ZNet network;
        private readonly ZDOMan manager;
        private readonly ZNetScene scene;
        private readonly World world;
        private readonly Game game;
        private readonly PlayerProfile identity;
        private readonly string path, name, filename, key, value;
        private readonly uint beforeGeneration;
        private readonly BlueprintForeignPartData beforeInventory;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private BlueprintStoragePlayerProfile profile;
        private BlueprintStorageWorldGeneration scan;
        private ItemDrop source;
        private ZDOID removedId;
        private uint absenceGeneration;
        private bool replay;
        internal string Status { get; private set; }
        internal string Reason { get; private set; }
        internal Guid Project => record.Project;
        internal int Index => index;
        internal int DestroyCalls { get; private set; }
        internal int WorldSaveRequests { get; private set; }
        internal int ProfileSaveRequests { get; private set; }
        internal bool Finished => Status == "cleanup-saved" || Status == "reconciliation";

        internal BlueprintConstructionPayoutCleanup(Plans plans, Guid project, int index)
        {
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            game = Game.instance; identity = game ? game.GetPlayerProfile() : null;
            path = identity?.GetPath(); name = identity?.GetName(); filename = identity?.GetFilename();
            network = ZNet.instance; world = network ? network.GetWorld() : null; manager = ZDOMan.instance; scene = ZNetScene.instance;
            journal = new Journal(plans); record = journal.Read(project); this.index = index;
            if (record?.Phase != Journal.IssuedSaved || record.Collections == null || index < 0 || index >= record.Collections.Length ||
                record.Collections.Any(c => c.Phase != Journal.ProfileCredited) || !Context() || network.IsSaving() ||
                SaveSystem.GetSaveNumber() < record.MinimumWorldGeneration ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out beforeInventory))
                throw new InvalidOperationException("Cleanup requires settled credit, an idle private Local payer and unchanged context.");
            key = Journal.ReceiptKey(record.Items[index].Id); value = Journal.ReceiptValue(record, index);
            var cleanup = record.Collections[index].Removal;
            beforeGeneration = cleanup?.BeforeGeneration ?? SaveSystem.GetSaveNumber();
            string error = null;
            if (beforeGeneration == 0 || beforeGeneration == uint.MaxValue || !ReadReceipt(out var disk, out error) ||
                !BlueprintStoragePlayerProfile.TryCapture(player, record.Items[index].Id, Custody, out profile, out error))
                throw new InvalidOperationException(error ?? "Cleanup profile context is unavailable.");
            uint current = SaveSystem.GetSaveNumber();
            if (cleanup?.Phase == Journal.CleanupSaved)
            {
                if (current < cleanup.SavedGeneration || !NoSources()) throw new InvalidOperationException("Saved cleanup conflicts with the current world; no repeated deletion.");
                replay = true; StartAbsence(current); return;
            }
            if (cleanup != null && disk.ProfileHash != cleanup.ProfileHash)
                throw new InvalidOperationException("Cleanup profile changed; preserve reconciliation evidence.");
            // Branch before source resolution: successful saved deletion must not require its old object.
            if (cleanup?.Phase == Journal.CleanupIntent && current == beforeGeneration + 1)
            {
                if (!NoSources()) throw new InvalidOperationException("Cleanup next-generation recovery still has a loaded source.");
                StartAbsence(current); return;
            }
            if (current != beforeGeneration || !Context() || network.IsSaving())
                throw new InvalidOperationException("Cleanup generation changed; only the recorded N/N+1 paths are supported.");
            var found = LoadedSources();
            if (found.Length != 1) throw new InvalidOperationException("Cleanup requires one exact loaded sealed source; absence is not permission to retry.");
            source = found[0];
            if (!BeforeCustody() || DestroyQueue?.GetValue(manager) is not List<ZDOID> queue || queue.Contains(source.GetComponent<ZNetView>().GetZDO().m_uid))
                throw new InvalidOperationException("Cleanup source changed or its native destruction is already queued.");
            var data = source.GetComponent<ZNetView>().GetZDO();
            if (!BlueprintStorageWorldGeneration.TryCaptureBlob(record.Items[index].Id, data.GetPrefab(), record.Player,
                BlueprintConstructionPayoutSeal.OwnerKey, Journal.ItemMarkerKey, ZDOVars.s_itemData,
                Convert.FromBase64String(record.Items[index].Blob), BeforeCustody, out scan, out error) ||
                scan.Generation != beforeGeneration || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException(error ?? "Cleanup source readback failed.");
            Status = "checking-source";
        }
        private bool Context() => Thread.CurrentThread.ManagedThreadId == thread && player && player == Player.m_localPlayer && !player.IsDead() &&
            ReferenceEquals(player.GetInventory(), inventory) && BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement &&
            game && game == Game.instance && ReferenceEquals(game.GetPlayerProfile(), identity) && identity != null &&
            identity.m_fileSource == FileHelpers.FileSource.Local && identity.GetPlayerID() == record.Player &&
            identity.GetPath() == path && identity.GetName() == name && identity.GetFilename() == filename &&
            network && network == ZNet.instance && ReferenceEquals(network.GetWorld(), world) && world != null && world.m_uid == record.World &&
            manager != null && ReferenceEquals(manager, ZDOMan.instance) && scene && scene == ZNetScene.instance &&
            player.GetPlayerID() == record.Player &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter);
        private bool Custody() => Context() && BlueprintStorageCapture.TrySnapshotInventory(inventory, out var actual) &&
            actual.Info == beforeInventory.Info && actual.Zdo == beforeInventory.Zdo &&
            player.m_customData.TryGetValue(key, out string receipt) && receipt == value;
        private ItemDrop[] LoadedSources() => UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(drop => drop.GetComponent<ZNetView>()?.GetZDO()?.GetString(Journal.ItemMarkerKey, "") == record.Items[index].Id.ToString("N")).ToArray();
        private bool NoSources() => LoadedSources().Length == 0;
        private bool BeforeCustody()
        {
            if (!Custody() || network.IsSaving() || SaveSystem.GetSaveNumber() != beforeGeneration || !source || LoadedSources().Length != 1) return false;
            var view = source.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
            return view && view.IsValid() && view.IsOwner() && data != null && data.IsValid() && !data.m_uid.IsNone() && data.Persistent &&
                source.m_itemData.m_dropPrefab && data.GetPrefab() == source.m_itemData.m_dropPrefab.name.GetStableHashCode() &&
                BlueprintConstructionPayoutSeal.IsHeld(source) && data.GetString(Journal.ItemMarkerKey, "") == record.Items[index].Id.ToString("N") &&
                data.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) == record.Player &&
                Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(source.m_itemData)) == record.Items[index].Blob &&
                data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Convert.FromBase64String(record.Items[index].Blob)) == true;
        }
        private bool ReadReceipt(out NativePlayerProfileReadback.CustomDataEvidence disk, out string error)
        {
            if (!BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, Custody, out disk, out error) || disk.Player != record.Player ||
                !disk.CustomData.TryGetValue(key, out string receipt) || receipt != value)
            { error = error ?? "Cleanup canonical receipt is missing or belongs to another payer."; return false; }
            return true;
        }
        private bool ExactProfile(out NativePlayerProfileReadback.CustomDataEvidence disk, out string error) =>
            ReadReceipt(out disk, out error) && disk.ProfileHash == record.Collections[index].Removal.ProfileHash;
        private void StartAbsence(uint generation)
        {
            absenceGeneration = generation;
            bool AbsentCustody() => Custody() && !network.IsSaving() && SaveSystem.GetSaveNumber() == absenceGeneration && NoSources();
            string error = null;
            if (!AbsentCustody() || !BlueprintStorageWorldGeneration.TryCaptureAbsence(record.Items[index].Id, Journal.ItemMarkerKey,
                new[] { record.Items[index].Id }, AbsentCustody, out scan, out error) || scan.Generation != generation || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException(error ?? "Cleanup absence scan could not start.");
            Status = "reading-absence";
        }
        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (!Custody() || DateTime.UtcNow > deadline) throw new InvalidOperationException("Cleanup custody changed or timed out.");
                if (Status == "checking-source")
                {
                    if (!BeforeCustody()) throw new InvalidOperationException("Cleanup source changed while reading.");
                    if (!scan.IsCompleted) return;
                    if (!scan.TryFinish(out var saved, out string error) || saved.World != record.World || saved.Operation != record.Items[index].Id || !BeforeCustody())
                        throw new InvalidOperationException(error ?? "Cleanup source is not exactly saved.");
                    if (record.Collections[index].Removal == null)
                    {
                        ProfileSaveRequests++;
                        if (!profile.TrySaveAndVerify(out var baseline, out error) || !BeforeCustody() || !ReadReceipt(out var disk, out error) || disk.ProfileHash != baseline.ProfileHash)
                            throw new InvalidOperationException(error ?? "Cleanup baseline profile changed.");
                        record = journal.BeginCleanup(record.Project, record.Operation, index, beforeGeneration, disk);
                    }
                    Delete(); return;
                }
                if (Status == "queued-removal")
                {
                    if (network.IsSaving() || SaveSystem.GetSaveNumber() != beforeGeneration)
                        throw new InvalidOperationException("Cleanup generation changed before native deletion completed.");
                    if (manager.GetZDO(removedId) != null || scene.FindInstance(removedId)) return;
                    string error = null;
                    if (!NoSources() || !ExactProfile(out _, out error)) throw new InvalidOperationException(error ?? "Cleanup source/profile changed before save.");
                    WorldSaveRequests++;
                    network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
                    if (!Custody() || !network.IsSaving() && SaveSystem.GetSaveNumber() != beforeGeneration + 1)
                        throw new InvalidOperationException("Cleanup next world save did not start.");
                    Status = "saving"; return;
                }
                if (network.IsSaving()) return;
                if (Status == "saving")
                {
                    if (SaveSystem.GetSaveNumber() != beforeGeneration + 1) throw new InvalidOperationException("Cleanup world save committed a different generation.");
                    StartAbsence(beforeGeneration + 1);
                }
                if (Status != "reading-absence" || !scan.IsCompleted) return;
                if (!scan.TryFinishAbsence(out var absence, out string reason) || absence.World != record.World || absence.Generation != absenceGeneration ||
                    absence.MarkerKey != Journal.ItemMarkerKey || !absence.CompleteMappedScan || absence.Operations.Count != 1 || absence.Operations[0] != record.Items[index].Id ||
                    !NoSources() || !ReadReceipt(out var currentProfile, out reason)) throw new InvalidOperationException(reason ?? "Cleanup absence/receipt evidence differs.");
                if (!replay) record = journal.ConfirmCleanup(record.Project, record.Operation, index, absence, currentProfile);
                Status = "cleanup-saved";
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        private void Delete()
        {
            string error = null;
            if (active != null || !BeforeCustody() || !ExactProfile(out _, out error)) throw new InvalidOperationException(error ?? "Cleanup deletion fence changed.");
            var durable = journal.Read(record.Project); var cleanup = durable.Collections[index].Removal;
            if (durable.Operation != record.Operation || cleanup?.Phase != Journal.CleanupIntent || cleanup.BeforeGeneration != beforeGeneration ||
                cleanup.ProfileHash != record.Collections[index].Removal.ProfileHash || Journal.ReceiptValue(durable, index) != value)
                throw new InvalidOperationException("Cleanup durable intent changed before native effect.");
            ZDO data = source.GetComponent<ZNetView>().GetZDO();
            var queue = DestroyQueue.GetValue(manager) as List<ZDOID>;
            if (queue == null || queue.Contains(data.m_uid)) throw new InvalidOperationException("Cleanup target is already queued or native queue is unavailable.");
            var witness = new Witness { Data = data, Id = data.m_uid, Manager = manager, BeforeCount = queue.Count,
                Custody = () => BeforeCustody() && ReferenceEquals(source.GetComponent<ZNetView>().GetZDO(), data) };
            active = witness;
            try { DestroyCalls++; manager.DestroyZDO(data); }
            finally { active = null; }
            if (!witness.Attempted || !witness.Confirmed) throw new InvalidOperationException("Cleanup native call did not prove one exact queued destruction.");
            removedId = witness.Id; Status = "queued-removal";
        }
        internal static bool AllowNetwork(ZDO data, out Witness witness)
        {
            witness = null;
            if (active == null || !ReferenceEquals(active.Data, data)) return !BlueprintConstructionPayoutSeal.IsHeld(data);
            var queue = DestroyQueue?.GetValue(active.Manager) as List<ZDOID>;
            if (active.Attempted || !ReferenceEquals(active.Manager, ZDOMan.instance) || data.m_uid != active.Id || !active.Custody() ||
                queue == null || queue.Count != active.BeforeCount || queue.Contains(active.Id)) return false;
            active.Attempted = true; witness = active; return true;
        }
        internal static void ConfirmNetwork(Witness witness, Exception error)
        {
            if (witness == null) return;
            var queue = DestroyQueue?.GetValue(witness.Manager) as List<ZDOID>;
            witness.Confirmed = error == null && witness.Attempted && ReferenceEquals(active, witness) && ReferenceEquals(witness.Manager, ZDOMan.instance) && queue != null &&
                queue.Count == witness.BeforeCount + 1 && queue[witness.BeforeCount] == witness.Id;
        }
        internal void Refuse(string reason) { cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
    }
}
