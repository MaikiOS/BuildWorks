using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using OstrixMods.BuildWorks.Geometry;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionMountDetachExecution
    {
        private static readonly System.Reflection.MethodInfo AddCell = AccessTools.DeclaredMethod(typeof(Inventory), "AddItem",
            new[] { typeof(ItemDrop.ItemData), typeof(int), typeof(int), typeof(int), typeof(bool) });

        // Saved credit only; the actual world source remains sealed for a separate cleanup.
        internal bool TryCredit(out string reason)
        {
            reason = null;
            try
            {
                if (!ReturnSourceCustody(out var saved) || saved.Return.Phase != Journal.MountReturnSaved || saved.Return.Credit != null ||
                    saved.Return.Saved == null || SaveSystem.GetSaveNumber() != saved.Return.Saved.Generation ||
                    AddCell?.ReturnType != typeof(bool))
                    throw new InvalidOperationException("A saved actual return without any prior credit attempt is required; never insert again.");
                uint savedGeneration = saved.Return.Saved.Generation;
                string key = Journal.MountCreditKey(operation), value = null;
                bool prepared = false;
                bool SourceCurrent()
                {
                    if (!ReturnSourceCustody(out var current) || ZNet.instance.IsSaving() || SaveSystem.GetSaveNumber() != savedGeneration ||
                        SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) ||
                        !BlueprintConstructionPayoutCollection.WithinReach(player, boundDrop)) return false;
                    var credit = current.Return.Credit;
                    return current.Return.Phase == Journal.MountReturnSaved &&
                        (prepared ? credit != null && credit.Phase == Journal.MountCreditIntent && Journal.MountCreditValue(current) == value : credit == null);
                }
                if (!SourceCurrent() || player.m_customData.ContainsKey(key) || player.m_customData.Count >= 65535 ||
                    !BlueprintStorageCapture.TrySnapshotInventory(inventory, out var inventoryBefore) ||
                    !BlueprintContainerInventory.TryRead(inventoryBefore, name => name.GetStableHashCode(), out var old) || old.Any(item => !item.PickedUp))
                    throw new InvalidOperationException("Return inventory or receipt is not a fresh canonical baseline.");
                bool Matches(BlueprintForeignPartData expected) => BlueprintStorageCapture.TrySnapshotInventory(inventory, out var actual) &&
                    actual.Info == expected.Info && actual.Zdo == expected.Zdo;
                bool BeforeCurrent() => SourceCurrent() && !player.m_customData.ContainsKey(key) && Matches(inventoryBefore);
                // A disk-only receipt must be checked before any save could overwrite it with stale loaded data.
                if (!BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, BeforeCurrent, out var prior, out string error) || prior.CustomData.ContainsKey(key))
                    throw new InvalidOperationException(error ?? "A saved or uncertain return receipt already exists; no new credit.");
                if (!BlueprintStoragePlayerProfile.TryCapture(player, operation, BeforeCurrent, out var baseline, out error) ||
                    !baseline.TrySaveAndVerify(out var beforeProfile, out error) || beforeProfile.CustomData.ContainsKey(key) || !BeforeCurrent())
                    throw new InvalidOperationException(error ?? "Canonical baseline already contains this credit receipt.");
                int x = -1, y = -1;
                for (int row = 0; row < inventory.GetHeight() && x < 0; row++)
                    for (int column = 0; column < inventory.GetWidth(); column++)
                        if (inventory.GetItemAt(column, row) == null) { x = column; y = row; break; }
                if (x < 0) throw new InvalidOperationException(BuildWorksLocalization.Text("project.refund_inventory_full"));
                var copy = boundDrop.m_itemData.Clone();
                if (ReferenceEquals(copy, boundDrop.m_itemData) || ReferenceEquals(copy.m_customData, boundDrop.m_itemData.m_customData))
                    throw new InvalidOperationException("Native return clone shares actual source data.");
                copy.m_gridPos = new Vector2i(x, y); copy.m_equipped = false; copy.m_pickedUp = true;
                string cleanItem = Convert.ToBase64String(BlueprintConstructionPayoutSeal.Package(copy));
                var after = Journal.MountCreditInventory(saved, inventoryBefore, cleanItem, x, y, returnBinding.Loaded);
                bool coherent = false; Exception nativeError = null;
                using (var guard = BlueprintPlayerSaveGuard.Begin(player))
                {
                    saved = journal.BeginMountReturnCredit(plan.Id, part.Id, operation, inventoryBefore, cleanItem, x, y, beforeProfile.ProfileHash, returnBinding.Loaded);
                    value = Journal.MountCreditValue(saved); prepared = true;
                    if (!BeforeCurrent()) throw new InvalidOperationException("Return custody changed before insertion.");
                    string pending = "pending:" + value;
                    player.m_customData.Add(key, pending);
                    try { AddCell.Invoke(inventory, new object[] { copy, 1, x, y, false }); }
                    catch (Exception failure) { nativeError = failure; }
                    if (SourceCurrent() && Matches(after) && player.m_customData.TryGetValue(key, out var marker) && marker == pending)
                    { player.m_customData[key] = value; coherent = true; }
                    else if (Matches(inventoryBefore) && player.m_customData.TryGetValue(key, out marker) && marker == pending) player.m_customData.Remove(key);
                    else player.m_customData[key] = pending;
                }
                if (!coherent) throw new InvalidOperationException("Return credit outcome is uncertain; intent retained, no repeated insertion.", nativeError);
                bool AfterCurrent() => SourceCurrent() && Matches(after) && player.m_customData.TryGetValue(key, out string receipt) && receipt == value;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    NativePlayerProfileReadback.Evidence proof = null;
                    bool written = BlueprintStoragePlayerProfile.TryCapture(player, operation, AfterCurrent, out var profile, out error) &&
                        profile.TrySaveAndVerify(out proof, out error);
                    if (written && AfterCurrent())
                    { journal.ConfirmMountReturnCredit(plan.Id, part.Id, operation, proof); return true; }
                    if (BlueprintStoragePlayerProfile.TryCapture(player, operation, AfterCurrent, out var recovery, out string readError) &&
                        recovery.TryVerifySavedCredit(out proof, out readError) && AfterCurrent())
                    { journal.ConfirmMountReturnCredit(plan.Id, part.Id, operation, proof); return true; }
                    if (written || !AfterCurrent()) break;
                }
                throw new InvalidOperationException(error ?? "Return profile credit requires reconciliation; do not insert again.");
            }
            catch (Exception failure) { reason = failure.Message; return false; }
        }
    }
}
