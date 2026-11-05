using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintStorageTransfer
    {
        internal static bool TryBeginFromProject(Plans plans, Guid project, Guid partId, Player player,
            BlueprintItemStandPlacement.Supply supply, IReadOnlyList<BlueprintItemStandPlacement.Supply> heldSupplies,
            out BlueprintStorageTransfer transfer, out string reason)
        {
            transfer = null; reason = null;
            try
            {
                if (plans == null || !player || player != Player.m_localPlayer || player.IsDead() ||
                    !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement)
                    throw new InvalidOperationException("Contents require the current private Local host and a living payer.");
                var parents = new Parts(plans);
                var parent = parents.RequireSavedPayment(project, partId, player.GetPlayerID());
                var plan = plans.Load(project);
                var part = plan.Parts.First(value => value.Id == partId);
                var source = plan.Blueprint.parts[part.SourceIndex];
                var data = source.foreignData?.ToGeometry();
                if (plan.World != ZNet.instance.GetWorldUID() || data == null || data.ContentsCleared ||
                    data.Format != BlueprintForeignPartData.NativeContainerFormat ||
                    !BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) || items.Count == 0)
                    throw new InvalidOperationException("Paid part does not contain a supported native storage demand.");
                var placed = BlueprintConstructionPartExecution.FindMarked(parent.Operation, source.prefabName);
                if (!ConstructionTargetMatches(part, parent, source.prefabName, placed))
                    throw new InvalidOperationException("Paid storage target, receipt, creator or pose differs from the project.");
                string fingerprint = Parts.Fingerprint(parent);
                bool Current()
                {
                    try
                    {
                        if (!player || player != Player.m_localPlayer || player.IsDead() ||
                            !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement || ZNet.instance.GetWorldUID() != parent.World) return false;
                        var current = parents.RequireSavedPayment(project, partId, player.GetPlayerID());
                        return current.Operation == parent.Operation && Parts.Fingerprint(current) == fingerprint &&
                            ConstructionTargetMatches(part, current, source.prefabName, placed);
                    }
                    catch (Exception) { return false; }
                }
                var binding = new ConstructionBinding(project, partId, fingerprint, Current);
                var paid = parent.Paid.Select(row => new BlueprintStorageTransferJournal.Payment {
                    Prefab = row.Prefab, Properties = row.Properties, Before = row.Before, Paid = row.Paid, X = row.X, Y = row.Y }).ToArray();
                return TryBeginCore(parent.Operation, player, placed, data, supply, paid, heldSupplies, binding, out transfer, out reason);
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private static bool ConstructionTargetMatches(Plans.Part part, Parts.Record parent, string prefabName, GameObject placed)
        {
            var prefab = ZNetScene.instance ? ZNetScene.instance.GetPrefab(prefabName) : null;
            var view = placed ? placed.GetComponent<ZNetView>() : null;
            var piece = placed ? placed.GetComponent<Piece>() : null;
            var wear = placed ? placed.GetComponent<WearNTear>() : null;
            var data = view ? view.GetZDO() : null;
            if (!prefab || !piece || !view || !view.IsValid() || !view.IsOwner() || data == null || data.m_uid.IsNone() ||
                !BlueprintStorageCapture.PrefabGrid(prefab).HasValue || !BlueprintStorageCapture.PrefabGrid(placed).HasValue ||
                !BlueprintConstructionContainerGuard.NativeRoot(placed, wear) ||
                data.GetPrefab() != parent.PrefabHash || piece.GetCreator() != parent.Creator ||
                data.GetString(BlueprintConstructionPartExecution.MarkerKey, "") != parent.Operation.ToString("N") ||
                data.GetByteArray(BlueprintConstructionPartExecution.ReceiptKey)?.SequenceEqual(parent.Receipt) != true) return false;
            float[] pose = part.WorldPose.Values;
            return Vector3.Distance(placed.transform.position, new Vector3(pose[0], pose[1], pose[2])) <= .001f &&
                Quaternion.Angle(placed.transform.rotation, new Quaternion(pose[3], pose[4], pose[5], pose[6])) <= .01f &&
                Vector3.Distance(placed.transform.localScale, Vector3.Scale(prefab.transform.localScale, new Vector3(pose[7], pose[8], pose[9]))) <= .001f;
        }

        internal bool TrySettleConstruction(out string reason)
        {
            reason = null;
            try
            {
                if (System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread || construction == null ||
                    !construction.Current() || !PersistenceCustodyValid || persistence == null || !persistence.TryVerifyCurrent(out reason))
                    throw new InvalidOperationException(reason ?? "Settlement requires the same saved payer, target and current paired custody.");
                var saved = BlueprintStorageTransferJournal.Read(JournalPath);
                if (saved.Prepared.ConstructionProject != construction.Project || saved.Prepared.ConstructionPart != construction.Part ||
                    saved.Prepared.ConstructionFingerprint != construction.Fingerprint || saved.SavedPair == null ||
                    saved.SavedPair.SourceSession != BlueprintStoragePlayerProfile.ProcessSession || saved.SavedPair.SourcePlayerInstance != player.GetInstanceID())
                    throw new InvalidOperationException("Settlement parent or native save binding changed.");
                journal.RecordSettlement(construction.Fingerprint);
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
