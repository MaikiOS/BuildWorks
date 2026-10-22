using System;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Removal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    // shortcut: sealed private-Local issuance; controlled collection and terminal paid cancellation remain separate.
    internal sealed class BlueprintConstructionPayoutIssuance
    {
        private readonly Journal journal;
        private Journal.Record record;
        private readonly Removal.Record removed;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData before;
        private readonly ZNet network;
        private readonly World world;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly uint initialGeneration;
        private readonly Guid[] parts;
        private readonly Journal.Entry[] entries;
        private readonly ItemDrop.ItemData[] items;
        private readonly Vector3 position;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly string[] hashes;
        private BlueprintStorageWorldGeneration scan;
        private BlueprintStoragePlayerProfile profile;
        private NativePlayerProfileReadback.Evidence savedProfile;
        private int issuedFrame, readingItem;
        internal string Status { get; private set; } = "checking-removal";
        internal string Reason { get; private set; }
        internal Guid Project => removed.Project;
        internal bool Finished => Status == "issued-saved" || Status == "reconciliation";

        internal BlueprintConstructionPayoutIssuance(Plans plans, Guid project, Vector3 position)
        {
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            network = ZNet.instance; world = network ? network.GetWorld() : null;
            journal = new Journal(plans); removed = new Removal(plans).Read(project);
            initialGeneration = SaveSystem.GetSaveNumber(); this.position = position;
            if (removed?.Phase != Removal.RemovedSaved || !Context() || network.IsSaving() ||
                initialGeneration < removed.Saved.Generation || initialGeneration == uint.MaxValue ||
                !PrecisionPlacementSession.HasUsableHammer(player) || !BlueprintStorageCapture.TrySnapshotInventory(inventory, out before))
                throw new InvalidOperationException("Payout requires saved removal, its payer, idle private Local world and exact inventory.");
            record = journal.Read(project);
            if (record != null && (record.Phase != Journal.Prepared || record.Items.Any(e => e.State != Journal.Ready)))
                throw new InvalidOperationException("Payout already has a native attempt; reconcile saved evidence instead of issuing again.");
            var paid = journal.Paid(project);
            entries = new Journal.Entry[paid.Length]; items = new ItemDrop.ItemData[paid.Length]; hashes = new string[paid.Length];
            for (int i = 0; i < paid.Length; ++i)
            {
                Guid id = record == null ? Guid.NewGuid() : record.Items[i].Id;
                items[i] = Restore(paid[i], id);
                entries[i] = new Journal.Entry { Id = id, Blob = Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(items[i])) };
            }
            parts = removed.Parts.Where(p => p.Phase == BlueprintConstructionPartJournal.Committed).Select(p => p.Operation).ToArray();
            if (!StartAbsence(initialGeneration, out string error)) throw new InvalidOperationException(error);
        }

        internal static void RequireRefundable(Plans plans, Guid project, long player, BlueprintForeignPartData inventory)
        {
            var parts = new BlueprintConstructionPartJournal(plans);
            var records = plans.Load(project).Parts.Select(part => parts.Read(project, part.Id)).Where(row => row != null).ToArray();
            if (records.Any(row => row.RequiresReconciliation || row.Phase == BlueprintConstructionPartJournal.Committed && row.Player != player))
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.review"));
            var paid = records.Where(row => row.Phase == BlueprintConstructionPartJournal.Committed)
                .SelectMany(row => row.Paid).Take(65537).ToArray();
            if (paid.Length > 65536) throw new InvalidOperationException("Payout row budget exceeded; removal cannot start.");
            System.Collections.Generic.IEnumerable<Journal.Entry> Restored()
            {
                foreach (var row in paid)
                {
                    Guid id = Guid.NewGuid();
                    var item = Restore(row, id);
                    var entry = new Journal.Entry { Id = id, Blob = Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(item)) };
                    Journal.ValidateItem(row, entry);
                    yield return entry;
                }
            }
            Journal.RequireIssuanceBudget(inventory, Restored());
        }

        private static ItemDrop.ItemData Restore(BlueprintConstructionPartJournal.PaidRow paid, Guid id)
        {
            var prefab = ObjectDB.instance ? ObjectDB.instance.GetItemPrefab(paid.Prefab) : null;
            var drop = prefab ? prefab.GetComponent<ItemDrop>() : null; var view = prefab ? prefab.GetComponent<ZNetView>() : null;
            if (!drop || !view || !view.m_persistent || !prefab.GetComponent<Rigidbody>() || prefab.GetComponent<Piece>() ||
                prefab.GetComponent<WearNTear>() || drop.m_onDrop != null || paid.Paid < 1 || paid.Paid > drop.m_itemData.m_shared.m_maxStackSize)
                throw new InvalidOperationException("Payout prefab/callback/persistence/stack is unsupported; nothing was issued.");
            var item = drop.m_itemData.Clone();
            if (item == null || ReferenceEquals(item, drop.m_itemData) || ReferenceEquals(item.m_customData, drop.m_itemData.m_customData))
                throw new InvalidOperationException("Native item clone shares prefab state; exact restitution is unsupported.");
            var package = new ZPackage(Convert.FromBase64String(paid.Properties));
            var version = (Version.Item)package.ReadByte();
            int hash = ItemDrop.ItemData.Load(package, item, version);
            if (hash != paid.Prefab.GetStableHashCode() || item.m_quality != paid.Quality || item.m_variant != paid.Variant ||
                item.m_customData.ContainsKey(Journal.CustodyKey)) throw new InvalidOperationException("Paid item identity differs or already has payout custody.");
            item.m_dropPrefab = prefab; item.m_gridPos = new Vector2i(0, 0); item.m_equipped = false; item.m_stack = paid.Paid;
            if (item.m_variant < 0 || item.m_variant > 0 && item.m_variant >= item.m_shared.m_variants || !item.m_cheated && item.GetDamage().GetTotalDamage() > 10000f)
                throw new InvalidOperationException("Native Start would modify paid item properties; issuance is unsupported.");
            item.m_customData.Add(Journal.CustodyKey, id.ToString("N")); return item;
        }
        private bool Context() => Thread.CurrentThread.ManagedThreadId == thread && network && ReferenceEquals(network, ZNet.instance) &&
            BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement && ReferenceEquals(network.GetWorld(), world) && world != null &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && player && player == Player.m_localPlayer && !player.IsDead() &&
            ReferenceEquals(inventory, player.GetInventory()) && removed != null && world.m_uid == removed.World && player.GetPlayerID() == removed.Player;
        private bool Custody() => Context() && BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) &&
            current.Info == before.Info && current.Zdo == before.Zdo;
        private bool StartAbsence(uint generation, out string reason)
        {
            if (!BlueprintStorageWorldGeneration.TryCaptureAbsence(removed.Operation, BlueprintConstructionPartExecution.MarkerKey, parts,
                Custody, out scan, out reason)) return false;
            if (scan.Generation != generation) { reason = "Payout generation changed before absence scan."; return false; }
            return scan.TryStart(cancellation.Token, out reason);
        }
        private void FinishAbsence(uint generation)
        {
            if (!scan.TryFinishAbsence(out var evidence, out string reason) || evidence.World != removed.World || evidence.Generation != generation ||
                !evidence.CompleteMappedScan || !evidence.Operations.SequenceEqual(parts))
                throw new InvalidOperationException(reason ?? "Original construction is not proven absent.");
        }
        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (!Custody() || cancellation.IsCancellationRequested || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Payout context/inventory changed or timed out; evidence retained.");
                if (Status != "checking-removal") VerifyLiveItems();
                if (Status == "checking-removal")
                {
                    if (!scan.IsCompleted) return;
                    FinishAbsence(initialGeneration);
                    record = journal.Prepare(removed.Project, removed.Operation, removed.Player, initialGeneration, before, entries);
                    for (int i = 0; i < items.Length; ++i)
                    {
                        if (!Custody() || network.IsSaving() || SaveSystem.GetSaveNumber() != initialGeneration)
                            throw new InvalidOperationException("Payout context changed before native issuance.");
                        record = journal.BeginItem(record.Project, record.Operation, i);
                        var drop = BlueprintConstructionPayoutSeal.Create(journal, record, i, items[i], position + Vector3.up * (.05f * i));
                        var uid = drop.GetComponent<ZNetView>().GetZDO().m_uid;
                        record = journal.Observe(record.Project, record.Operation, i, uid.UserID, uid.ID);
                    }
                    issuedFrame = Time.frameCount; Status = "waiting-items"; return;
                }
                if (Status == "waiting-items")
                {
                    if (Time.frameCount <= issuedFrame) return;
                    if (network.IsSaving() || SaveSystem.GetSaveNumber() != initialGeneration)
                        throw new InvalidOperationException("Payout world changed before save intent.");
                    record = journal.BeginSave(record.Project, record.Operation);
                    if (!BlueprintStoragePlayerProfile.TryCapture(player, record.Operation, Custody, out profile, out string reason) ||
                        !profile.TrySaveAndVerify(out savedProfile, out reason)) throw new InvalidOperationException(reason);
                    if (!Custody() || network.IsSaving() || SaveSystem.GetSaveNumber() != initialGeneration)
                        throw new InvalidOperationException("Payout generation changed before save request.");
                    network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
                    Status = "saving"; return;
                }
                if (network.IsSaving()) return;
                if (SaveSystem.GetSaveNumber() != initialGeneration + 1) throw new InvalidOperationException("Payout save generation differs.");
                if (Status == "saving")
                {
                    if (!StartAbsence(initialGeneration + 1, out string reason)) throw new InvalidOperationException(reason);
                    Status = "reading-removal";
                }
                if (!scan.IsCompleted) return;
                if (Status == "reading-removal") { FinishAbsence(initialGeneration + 1); StartItem(); return; }
                if (!scan.TryFinish(out var itemEvidence, out string error) || itemEvidence.World != removed.World ||
                    itemEvidence.Operation != entries[readingItem].Id) throw new InvalidOperationException(error ?? "Saved payout identity differs.");
                hashes[readingItem++] = itemEvidence.InventorySha256; StartItem();
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        private void StartItem()
        {
            if (readingItem < entries.Length)
            {
                // Full mapped scans are deliberately sequential; large payout batching needs its own measured gate.
                if (!BlueprintStorageWorldGeneration.TryCaptureBlob(entries[readingItem].Id, items[readingItem].m_dropPrefab.name.GetStableHashCode(),
                    removed.Player, BlueprintConstructionPayoutSeal.OwnerKey, BlueprintConstructionPayoutSeal.MarkerKey, ZDOVars.s_itemData,
                    Convert.FromBase64String(entries[readingItem].Blob), Custody, out scan, out string reason) ||
                    scan.Generation != initialGeneration + 1 || !scan.TryStart(cancellation.Token, out reason)) throw new InvalidOperationException(reason);
                Status = "reading-items"; return;
            }
            if (!profile.TryVerify(out var current, out string error) || current.ProfileHash != savedProfile.ProfileHash ||
                current.InventoryHash != savedProfile.InventoryHash || !Custody() || network.IsSaving())
                throw new InvalidOperationException(error ?? "Payout paired profile evidence differs.");
            journal.ConfirmSaved(record.Project, record.Operation, new Journal.Proof { Generation = initialGeneration + 1,
                ProfileHash = current.ProfileHash, InventoryHash = current.InventoryHash, ItemHashes = hashes });
            Status = "issued-saved";
        }
        private void VerifyLiveItems()
        {
            foreach (var entry in record.Items)
            {
                var uid = new ZDOID(entry.ObjectUser, entry.ObjectId); var data = ZDOMan.instance.GetZDO(uid);
                var instance = ZNetScene.instance.FindInstance(uid); var drop = instance ? instance.GetComponent<ItemDrop>() : null;
                if (!drop || !BlueprintConstructionPayoutSeal.IsHeld(drop) || data == null || !data.Persistent ||
                    data.GetString(BlueprintConstructionPayoutSeal.MarkerKey, "") != entry.Id.ToString("N") ||
                    data.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) != removed.Player ||
                    Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(drop.m_itemData)) != entry.Blob ||
                    data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Convert.FromBase64String(entry.Blob)) != true)
                    throw new InvalidOperationException("Native payout changed after Start; retained custody requires reconciliation.");
            }
        }
        internal void Refuse(string reason) { cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
    }
}
