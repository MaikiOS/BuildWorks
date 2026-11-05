using System;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Transfers = OstrixMods.BuildWorks.BlueprintStorageTransferJournal;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintStorageTransfer
    {
        private static Transfers.Payment[] PaidRows(Parts.Record parent) => parent.Paid.Select(row => new Transfers.Payment {
            Prefab = row.Prefab, Properties = row.Properties, Before = row.Before, Paid = row.Paid, X = row.X, Y = row.Y }).ToArray();

        private static (Transfers.Snapshot Forward, Transfers.Snapshot Returned) CancellationHistory(Parts.Record parent, bool allowUnsettledReturn = false)
        {
            var history = Transfers.ReadCancellation(parent.Project, parent.Part, parent.Operation, parent.World, parent.Player,
                Parts.Fingerprint(parent), PaidRows(parent));
            foreach (var snapshot in new[] { history.Forward, history.Returned }.Where(value => value != null))
                if (snapshot.Prepared.Creator != parent.Creator || snapshot.Prepared.PrefabHash != parent.PrefabHash ||
                    !snapshot.Settled && (!allowUnsettledReturn || ReferenceEquals(snapshot, history.Forward)))
                    throw new InvalidOperationException("Interrupted or different project contents evidence requires reconciliation.");
            return history;
        }

        private static ConstructionBinding BindReturn(Plans plans, Guid project, Guid partId, Player player,
            out Parts.Record parent, out GameObject placed)
        {
            if (plans == null || !player || player != Player.m_localPlayer || player.IsDead() ||
                !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement)
                throw new InvalidOperationException("Contents return requires the living payer on the private Local host.");
            var parents = new Parts(plans);
            var saved = parents.RequireSavedPayment(project, partId, player.GetPlayerID());
            var plan = plans.Load(project); var part = plan.Parts.First(value => value.Id == partId);
            var source = plan.Blueprint.parts[part.SourceIndex];
            var target = BlueprintConstructionPartExecution.FindMarked(saved.Operation, source.prefabName);
            string fingerprint = Parts.Fingerprint(saved);
            bool Current()
            {
                try
                {
                    return player && player == Player.m_localPlayer && !player.IsDead() &&
                        BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement && plan.World == ZNet.instance.GetWorldUID() &&
                        Parts.Fingerprint(parents.RequireSavedPayment(project, partId, player.GetPlayerID())) == fingerprint &&
                        ConstructionTargetMatches(part, saved, source.prefabName, target);
                }
                catch (Exception) { return false; }
            }
            if (!Current()) throw new InvalidOperationException("Paid storage target/receipt/owner/pose changed before return.");
            parent = saved; placed = target;
            return new ConstructionBinding(project, partId, fingerprint, Current);
        }

        internal static bool TryBeginProjectReturn(Plans plans, Guid project, Guid partId, Player player,
            out BlueprintStorageTransfer transfer, out string reason)
        {
            transfer = null; reason = null;
            try
            {
                var binding = BindReturn(plans, project, partId, player, out var parent, out var placed);
                var history = CancellationHistory(parent, allowUnsettledReturn: true);
                if (history.Returned != null && !history.Returned.Settled)
                {
                    transfer = history.Returned.SavedPair == null
                        ? ResumeCurrentReturn(history.Returned, player, placed.GetComponent<Piece>(), placed.GetComponent<Container>(), placed.GetComponent<ZNetView>(), binding)
                        : ResumePairedReturn(history.Returned, player, placed.GetComponent<Piece>(), placed.GetComponent<Container>(), placed.GetComponent<ZNetView>(), binding, parent.Receipt);
                    return true;
                }
                if (ProjectEmpty(plans, project, partId, player, placed)) return true;
                if (history.Returned != null) throw new InvalidOperationException("Saved contents return cannot be repeated; inspect the current additions.");
                if (Transfers.HasUnresolved(parent.World)) throw new InvalidOperationException("Other unresolved contents evidence prevents return.");
                var piece = placed.GetComponent<Piece>(); var container = placed.GetComponent<Container>();
                var view = placed.GetComponent<ZNetView>();
                string marker = BlueprintStorageCapture.TransferMarker(view.GetZDO());
                if (!string.IsNullOrEmpty(marker) && (marker != parent.Operation.ToString("N") || history.Forward == null))
                    throw new InvalidOperationException("Storage marker is not bound to the saved paid part.");
                if (!BlueprintStorageCapture.TryCapture(piece, player, out _) || container.GetInventory().GetAllItems().Count == 0)
                    throw new InvalidOperationException("Current native contents cannot be read exactly.");
                var items = container.GetInventory().GetAllItems().Select(item => new BlueprintItemStandPlacement.Reservation(
                    item, item.m_stack, BlueprintItemStandPlacement.SnapshotItem(item))).ToArray();
                transfer = PrepareReturn(parent.Operation, player, piece, container, view, items, binding);
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        // Removal may already have a cancellation intent: read the canonical parent, not the transfer's active-parent fence.
        internal static bool ProjectEmpty(Plans plans, Guid project, Guid partId, Player player, GameObject placed)
        {
            try
            {
                var plan = plans.Load(project); var part = plan.Parts.First(value => value.Id == partId);
                var parent = new Parts(plans).Read(project, partId);
                if (!player || player != Player.m_localPlayer || player.IsDead() || !BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement ||
                    plan.Cancelled || plan.World != ZNet.instance.GetWorldUID() || parent?.Phase != Parts.Committed ||
                    parent.Player != player.GetPlayerID() || parent.CostFree || parent.Paid.Length == 0 ||
                    !ConstructionTargetMatches(part, parent, plan.Blueprint.parts[part.SourceIndex].prefabName, placed)) return false;
                var history = CancellationHistory(parent);
                string marker = BlueprintStorageCapture.TransferMarker(placed.GetComponent<ZNetView>().GetZDO());
                if (!string.IsNullOrEmpty(marker) && (marker != parent.Operation.ToString("N") ||
                    history.Forward == null && history.Returned == null) || Transfers.HasUnresolved(plan.World)) return false;
                return BlueprintStorageCapture.EmptyState(placed, player);
            }
            catch (Exception) { return false; }
        }
    }
}
