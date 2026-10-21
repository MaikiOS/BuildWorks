using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    // Profile credit only. Old drops remain sealed until a separate saved-world cleanup succeeds.
    internal sealed class BlueprintConstructionPayoutCollection
    {
        private static readonly MethodInfo AddCell = AccessTools.DeclaredMethod(typeof(Inventory), "AddItem",
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });
        private readonly Journal journal;
        private Journal.Record record;
        private readonly int index, thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly ZNet network;
        private readonly World world;
        private readonly Game game;
        private readonly PlayerProfile playerProfile;
        private readonly string profilePath, profileName, profileFilename;
        private readonly uint generation;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly BlueprintForeignPartData before;
        private readonly Guid[] removed;
        private readonly string key, value;
        private ItemDrop source;
        private BlueprintStorageWorldGeneration scan;
        internal string Status { get; private set; } = "checking-removal";
        internal string Reason { get; private set; }
        internal string FirstSaveError { get; private set; }
        internal int SaveAttempts { get; private set; }
        internal int BlockedSaves { get; private set; }
        internal Guid Project => record.Project;
        internal int Index => index;
        internal bool Finished => Status == "profile-credited" || Status == "reconciliation";

        internal BlueprintConstructionPayoutCollection(Plans plans, Guid project, int index)
        {
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            game = Game.instance; playerProfile = game ? game.GetPlayerProfile() : null;
            profilePath = playerProfile?.GetPath(); profileName = playerProfile?.GetName(); profileFilename = playerProfile?.GetFilename();
            network = ZNet.instance; world = network ? network.GetWorld() : null; generation = SaveSystem.GetSaveNumber();
            journal = new Journal(plans); record = journal.Read(project); this.index = index;
            if (record?.Phase != Journal.IssuedSaved || index < 0 || index >= record.Items.Length || AddCell?.ReturnType != typeof(bool) ||
                !Context() || network.IsSaving() || generation < record.Saved.Generation || generation == uint.MaxValue)
                throw new InvalidOperationException("Credit requires saved issuance, its payer, idle private Local context and exact native cell API.");
            key = Journal.ReceiptKey(record.Items[index].Id); value = Journal.ReceiptValue(record, index);
            VerifySource();
            if (!BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, SavedCustody, out var disk, out string error))
                throw new InvalidOperationException(error);
            bool saved = disk.CustomData.TryGetValue(key, out string savedValue), loaded = player.m_customData.TryGetValue(key, out string loadedValue);
            var credit = record.Collections != null && index < record.Collections.Length ? record.Collections[index] : null;
            if (saved || credit?.Phase == Journal.ProfileCredited)
            {
                if (credit == null || !saved || savedValue != value || !loaded || loadedValue != value || !SavedCustody())
                    throw new InvalidOperationException("Permanent credit receipt conflicts with disk/loaded state; re-credit is forbidden.");
                if (credit.Phase == Journal.ProfileCredited) journal.ConfirmCredit(project, record.Operation, index, disk);
                else
                {
                    var credited = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, credit.AfterInfo, credit.AfterBytes, null);
                    bool UnsettledPair() => SavedCustody() && Matches(credited) && player.m_customData.TryGetValue(key, out string receipt) && receipt == value;
                    if (!BlueprintStoragePlayerProfile.TryCapture(player, record.Items[index].Id, UnsettledPair, out var recovery, out error) ||
                        !recovery.TryVerifySavedCredit(out var proof, out error))
                        throw new InvalidOperationException(error ?? "Unsettled receipt requires its exact saved credited inventory; no re-credit.");
                    journal.ConfirmSavedCredit(project, record.Operation, index, proof);
                }
                Status = "profile-credited"; return;
            }
            if (loaded || credit != null || index != (record.Collections?.Length ?? 0) || player.m_customData.Count >= 65535 ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out before))
                throw new InvalidOperationException("An earlier credit attempt or loaded-only receipt requires reconciliation, never another insertion.");
            // Native pickup marks every existing inventory item picked-up. Do not hide an unrelated delta.
            if (!BlueprintContainerInventory.TryRead(before, name => name.GetStableHashCode(), out var old) || old.Any(i => !i.PickedUp))
                throw new InvalidOperationException("Credit requires a canonical already-picked inventory baseline.");
            removed = new BlueprintConstructionCancellationJournal(plans).Read(project).Parts
                .Where(p => p.Phase == BlueprintConstructionPartJournal.Committed).Select(p => p.Operation).ToArray();
            if (!BlueprintStorageWorldGeneration.TryCaptureAbsence(record.Operation, BlueprintConstructionPartExecution.MarkerKey,
                removed, BeforeCustody, out scan, out error) || scan.Generation != generation || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException(error ?? "Credit removal scan could not start.");
        }
        private bool Context() => Thread.CurrentThread.ManagedThreadId == thread && player && player == Player.m_localPlayer && !player.IsDead() &&
            ReferenceEquals(player.GetInventory(), inventory) && BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement &&
            game && ReferenceEquals(game, Game.instance) && ReferenceEquals(game.GetPlayerProfile(), playerProfile) && playerProfile != null &&
            playerProfile.m_fileSource == FileHelpers.FileSource.Local && playerProfile.GetPlayerID() == record.Player &&
            playerProfile.GetPath() == profilePath && playerProfile.GetName() == profileName && playerProfile.GetFilename() == profileFilename &&
            network && network == ZNet.instance && ReferenceEquals(network.GetWorld(), world) && world != null &&
            world.m_uid == record.World && player.GetPlayerID() == record.Player &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter);
        private bool Matches(BlueprintForeignPartData expected) => BlueprintStorageCapture.TrySnapshotInventory(inventory, out var actual) &&
            actual.Info == expected.Info && actual.Zdo == expected.Zdo;
        private bool SavedCustody() => Context() && SaveSystem.GetSaveNumber() == generation && !network.IsSaving() && SourceMatches();
        private bool CreditedCustody(BlueprintForeignPartData after) => SavedCustody() && Matches(after) &&
            player.m_customData.TryGetValue(key, out string marker) && marker == value;
        private bool BeforeCustody() => SavedCustody() && !player.m_customData.ContainsKey(key) && Matches(before);
        internal static bool WithinReach(Player player, ItemDrop source)
        {
            if (!player || !source || !source.gameObject.activeInHierarchy) return false;
            float distance = player.m_maxInteractDistance + source.GetHoverOffset();
            if (!(distance > 0) || float.IsInfinity(distance)) return false;
            Vector3 eye = player.GetEyePoint();
            return source.GetComponentsInChildren<Collider>().Any(collider => collider && collider.enabled &&
                collider.GetComponentInParent<ItemDrop>() == source && Vector3.Distance(eye, collider.ClosestPoint(eye)) < distance);
        }
        private bool SourceMatches()
        {
            var view = source ? source.GetComponent<ZNetView>() : null; var data = view ? view.GetZDO() : null;
            return WithinReach(player, source) && view && view.IsValid() && view.IsOwner() && data != null && data.Persistent &&
                source.m_itemData.m_dropPrefab && data.GetPrefab() == source.m_itemData.m_dropPrefab.name.GetStableHashCode() &&
                BlueprintConstructionPayoutSeal.IsHeld(source) && data.GetString(BlueprintConstructionPayoutSeal.MarkerKey, "") == record.Items[index].Id.ToString("N") &&
                data.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) == record.Player &&
                Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(source.m_itemData)) == record.Items[index].Blob &&
                data.GetByteArray(ZDOVars.s_itemData)?.SequenceEqual(Convert.FromBase64String(record.Items[index].Blob)) == true;
        }
        private void VerifySource()
        {
            var found = UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(drop => drop.GetComponent<ZNetView>()?.GetZDO()?.GetString(BlueprintConstructionPayoutSeal.MarkerKey, "") == record.Items[index].Id.ToString("N")).ToArray();
            if (found.Length != 1) throw new InvalidOperationException("The uniquely marked sealed source must be loaded; old ZDO IDs are not identity.");
            source = found[0];
            if (!SourceMatches()) throw new InvalidOperationException("Sealed credit source package/owner changed.");
        }
        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (DateTime.UtcNow > deadline || !BeforeCustody()) throw new InvalidOperationException("Credit context/inventory/source changed or timed out.");
                if (!scan.IsCompleted) return;
                if (Status == "checking-removal")
                {
                    if (!scan.TryFinishAbsence(out var absence, out string reason) || absence.World != record.World ||
                        absence.MarkerKey != BlueprintConstructionPartExecution.MarkerKey || !absence.Operations.SequenceEqual(removed) || absence.Generation != generation)
                        throw new InvalidOperationException(reason ?? "Original paid results are not absent from the saved world.");
                    if (!BlueprintStorageWorldGeneration.TryCaptureBlob(record.Items[index].Id, source.m_itemData.m_dropPrefab.name.GetStableHashCode(),
                        record.Player, BlueprintConstructionPayoutSeal.OwnerKey, BlueprintConstructionPayoutSeal.MarkerKey, ZDOVars.s_itemData,
                        Convert.FromBase64String(record.Items[index].Blob), BeforeCustody, out scan, out reason) || scan.Generation != generation ||
                        !scan.TryStart(cancellation.Token, out reason)) throw new InvalidOperationException(reason);
                    Status = "checking-item"; return;
                }
                if (!scan.TryFinish(out var item, out string error) || item.World != record.World || item.Operation != record.Items[index].Id)
                    throw new InvalidOperationException(error ?? "Full saved source item evidence differs.");
                Credit();
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        private void Credit()
        {
            if (!BlueprintStoragePlayerProfile.TryCapture(player, record.Items[index].Id, BeforeCustody, out var baseline, out string error) ||
                !baseline.TrySaveAndVerify(out var beforeProfile, out error) || beforeProfile.CustomData.ContainsKey(key) || !BeforeCustody())
                throw new InvalidOperationException(error ?? "Credit baseline profile is not canonical or already contains this receipt.");
            int x = -1, y = -1;
            for (int row = 0; row < inventory.GetHeight() && x < 0; row++)
                for (int column = 0; column < inventory.GetWidth(); column++)
                    if (inventory.GetItemAt(column, row) == null) { x = column; y = row; break; }
            if (x < 0) throw new InvalidOperationException("A free inventory cell is required; nothing was collected.");
            var clean = source.m_itemData.Clone();
            if (ReferenceEquals(clean, source.m_itemData) || ReferenceEquals(clean.m_customData, source.m_itemData.m_customData))
                throw new InvalidOperationException("Native item clone shares source custody data.");
            clean.m_customData.Remove(Journal.CustodyKey); clean.m_gridPos = new Vector2i(x, y); clean.m_equipped = false; clean.m_pickedUp = true;
            string package = Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(clean));
            var after = Journal.CreditInventory(before, package, journal.Paid(record.Project)[index], x, y);
            string pending = "pending:" + value; bool coherent = false; Exception nativeError = null;
            using (var guard = BlueprintPlayerSaveGuard.Begin(player))
            {
                record = journal.BeginCredit(record.Project, record.Operation, index, before, package, x, y, beforeProfile.ProfileHash);
                try
                {
                    if (!BeforeCustody()) throw new InvalidOperationException("Credit custody changed before staging.");
                    player.m_customData.Add(key, pending);
                    try { AddCell.Invoke(inventory, new object[] { clean, clean.m_stack, x, y, false }); }
                    catch (Exception failure) { nativeError = failure; }
                    if (SavedCustody() && Matches(after) && player.m_customData.TryGetValue(key, out string marker) && marker == pending)
                    { player.m_customData[key] = value; coherent = true; }
                    else if (Matches(before) && player.m_customData.TryGetValue(key, out marker) && marker == pending) player.m_customData.Remove(key);
                    else player.m_customData[key] = pending;
                }
                finally { BlockedSaves = guard.BlockedAttempts; }
            }
            if (!coherent) throw new InvalidOperationException("Native credit is not the exact expected pair; intent/pending evidence retained, no repeated insertion.", nativeError);
            bool AfterCustody() => CreditedCustody(after);
            for (int attempt = 0; attempt < 2; attempt++)
            {
                SaveAttempts++;
                NativePlayerProfileReadback.Evidence full = null;
                bool saved = BlueprintStoragePlayerProfile.TryCapture(player, record.Items[index].Id, AfterCustody, out var profile, out error) &&
                    profile.TrySaveAndVerify(out full, out error);
                if (saved && AfterCustody())
                { journal.ConfirmSavedCredit(record.Project, record.Operation, index, full); Status = "profile-credited"; return; }
                // A native failure after replacement is not proof of failure: read the canonical file before deciding.
                if (BlueprintStoragePlayerProfile.TryCapture(player, record.Items[index].Id, AfterCustody, out var recovery, out string readError) &&
                    recovery.TryVerifySavedCredit(out full, out readError) && AfterCustody())
                {
                    journal.ConfirmSavedCredit(record.Project, record.Operation, index, full); Status = "profile-credited"; return;
                }
                if (FirstSaveError == null) FirstSaveError = error ?? readError ?? "Saved profile has no matching credit receipt.";
                if (saved || !AfterCustody()) break;
                // Retry only the save of the same coherent pair, never insertion.
            }
            throw new InvalidOperationException(FirstSaveError ?? "Profile credit requires reconciliation; no repeated insertion.");
        }
        internal void Refuse(string reason) { cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
    }
}
