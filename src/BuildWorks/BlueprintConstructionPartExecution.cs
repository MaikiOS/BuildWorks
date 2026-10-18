using System;
using System.Collections.Generic;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // One native structural placement; ambiguous outcomes remain durable and never retry or refund here.
    internal sealed class BlueprintConstructionPartExecution
    {
        internal static readonly int MarkerKey = "ostrix_buildworks_project_operation_v1".GetStableHashCode();
        internal static readonly int ReceiptKey = "ostrix_buildworks_project_receipt_v1".GetStableHashCode();
        private readonly Journal journal;
        private readonly Plans.Record plan;
        private readonly Plans.Part part;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData before;
        private readonly long playerId;
        private readonly Guid operation;
        private GameObject created;
        private ZDO zdo;
        private ZDOID nativeId;
        private Journal.Record observed;
        private BlueprintConstructionProjectPersistence persistence;
        private bool nativeAttempt, nativeSuccess, signFailed;
        internal readonly Piece Piece;
        internal readonly PrecisionPlacementSession.TransformSnapshot Pose;
        internal bool Finished { get; private set; }
        internal bool Saving => persistence != null && !Finished;
        internal string Status { get; private set; } = "prepared";
        internal string Reason { get; private set; }
        internal Guid Project => plan.Id;
        internal Guid Part => part.Id;
        internal Guid Operation => operation;

        internal BlueprintConstructionPartExecution(Plans store, Guid project, Guid partId, Guid recoveryOperation = default)
        {
            player = Player.m_localPlayer; playerId = player ? player.GetPlayerID() : 0;
            inventory = player ? player.GetInventory() : null;
            plan = store.Load(project); part = plan.Parts.First(p => p.Id == partId); journal = new Journal(store);
            var blueprint = plan.Blueprint; var source = blueprint.parts[part.SourceIndex];
            if (!Context() || plan.Cancelled || !PrecisionPlacementSession.HasUsableHammer(player) ||
                !PrecisionPlacementSession.TryResolveBuildPiece(player, source.prefabName, out Piece) ||
                !PrecisionPlacementSession.SupportsProjectPart(Piece) ||
                source.foreignData != null || !BlueprintSignText.CanApply(Piece, source.signText))
                throw new InvalidOperationException("Project part is unavailable or requires a separate contents workflow.");
            var pieces = blueprint.parts.Select(p => ZNetScene.instance.GetPrefab(p.prefabName)?.GetComponent<Piece>()).ToArray();
            if (pieces.Any(p => !p)) throw new InvalidOperationException("Project still contains missing pieces; choose an explicit supported copy first.");
            var points = BlueprintPlacementFrame.SnapLocalSets(blueprint, pieces)[part.SourceIndex];
            float[] pose = part.WorldPose.Values;
            Pose = new PrecisionPlacementSession.TransformSnapshot(new Vector3(pose[0], pose[1], pose[2]),
                new Quaternion(pose[3], pose[4], pose[5], pose[6]), new Vector3(pose[7], pose[8], pose[9]), source.signText,
                customSnapPoints: points, materialSeed: source.materialSeed);
            if (!BlueprintStorageCapture.TrySnapshotInventory(inventory, out var currentInventory)) throw new InvalidOperationException("Could not snapshot exact construction inventory.");
            if (recoveryOperation != Guid.Empty)
            {
                // Bounded recovery cannot infer whether special construction state was applied before the interruption.
                if (source.signText != null || source.materialSeed.HasValue)
                    throw new InvalidOperationException("Recovery of sign text or material variants requires separate live-state evidence.");
                var intent = journal.Read(project, partId);
                if (intent?.Phase != Journal.Intent || intent.Operation != recoveryOperation || intent.Player != playerId)
                    throw new InvalidOperationException("Recovery requires the same durable intent and operation identity.");
                before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, intent.BeforeInfo, intent.BeforeBytes, null);
                operation = intent.Operation; created = FindMarked(operation, source.prefabName);
                zdo = created ? created.GetComponent<ZNetView>()?.GetZDO() : null;
                nativeId = zdo?.m_uid ?? ZDOID.None;
                if (!ObjectValid() || zdo.GetByteArray(ReceiptKey) != null)
                    throw new InvalidOperationException("Recovery native marker, identity, pose or ownership is ambiguous.");
                if (!BlueprintContainerInventory.TryRead(before, name => name.GetStableHashCode(), out var originals))
                    throw new InvalidOperationException("Recovery before-inventory is unreadable.");
                var remaining = inventory.GetAllItems().ToDictionary(item => (item.m_gridPos.x, item.m_gridPos.y));
                var paid = originals.Select(original => {
                    if (!remaining.TryGetValue((original.X, original.Y), out var item) || item.m_stack > original.Stack)
                        throw new InvalidOperationException("Recovery needs surviving exact inventory stacks; missing or increased stacks are not inferred.");
                    return item.m_stack == original.Stack ? null : Row(item, original.Stack, original.Stack - item.m_stack);
                }).Where(row => row != null).ToArray();
                CraftingStation station = player.GetCurrentCraftingStation();
                var recoveryCosts = (Piece.m_resources ?? Array.Empty<Piece.Requirement>()).Where(r => r?.m_resItem &&
                    (station ? station.m_upgrader == r.m_upgraderResource : !r.m_upgraderResource) && r.GetAmount(0) > 0)
                    .GroupBy(r => r.m_resItem.m_itemData.m_shared.m_name)
                    .ToDictionary(group => group.Key, group => group.Sum(r => r.GetAmount(0)));
                var debits = paid.GroupBy(row => ObjectDB.instance.GetItemPrefab(row.Prefab)?.GetComponent<ItemDrop>()?.m_itemData.m_shared.m_name ?? "")
                    .ToDictionary(group => group.Key, group => group.Sum(row => row.Paid));
                if (recoveryCosts.Count == 0 || recoveryCosts.Count != debits.Count || recoveryCosts.Any(cost => !debits.TryGetValue(cost.Key, out int amount) || amount != cost.Value))
                    throw new InvalidOperationException("Recovery delta does not equal the current native recipe.");
                Observe(currentInventory, false, paid); return;
            }
            before = currentInventory;
            var costs = (Piece.m_resources ?? Array.Empty<Piece.Requirement>()).Where(r => r?.m_resItem)
                .Select(r => r.m_resItem.m_itemData.m_shared.m_name).ToHashSet(StringComparer.Ordinal);
            Journal.RequireCapacity(before, inventory.GetAllItems().Where(item => costs.Contains(item.m_shared.m_name))
                .Select(item => Row(item, item.m_stack, 1)).ToArray());
            operation = Guid.NewGuid();
            journal.Prepare(project, partId, operation, playerId, before, out bool prepared);
            if (!prepared) throw new InvalidOperationException("Construction operation was already prepared; native replay refused.");
        }

        private bool Context() => player && player == Player.m_localPlayer && !player.IsDead() && player.GetPlayerID() == playerId &&
            ReferenceEquals(player.GetInventory(), inventory) && Game.instance && !Game.instance.IsShuttingDown() && ZNet.instance &&
            ZNet.instance.IsServer() && !ZNet.instance.IsDedicated() && !ZNet.IsOpenServer() && ZNet.instance.GetPeers().Count == 0 &&
            ZNet.instance.GetWorldUID() == plan.World && ZNet.instance.GetWorld().m_fileSource == FileHelpers.FileSource.Local &&
            ZNet.instance.LocalPlayerIsAdminOrHost();

        internal static GameObject FindMarked(Guid operation, string prefab)
        {
            // ZDO.Load assigns new network IDs; only the exact saved operation marker identifies a reloaded part.
            if (operation == Guid.Empty) throw new ArgumentException("A persisted construction operation is required.");
            if (ZDOMan.instance == null || !ZNetScene.instance) throw new InvalidOperationException("Construction world is unavailable.");
            var candidates = new List<ZDO>(); int index = 0;
            while (!ZDOMan.instance.GetAllZDOsWithPrefabIterative(prefab, candidates, ref index))
                if (candidates.Count > 1000000) throw new InvalidOperationException("Construction identity scan exceeds its memory budget.");
            var matches = candidates.Where(data => data != null && data.IsValid() && data.GetString(MarkerKey, "") == operation.ToString("N"))
                .GroupBy(data => data.m_uid).Select(group => group.First()).ToArray();
            if (matches.Length > 1) throw new InvalidOperationException("Repeated native construction marker; reconciliation is required.");
            return matches.Length == 1 ? ZNetScene.instance.FindInstance(matches[0].m_uid) : null;
        }

        internal void BeforeNative()
        {
            if (Finished || persistence != null || !Context() || nativeAttempt ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) || current.Info != before.Info || current.Zdo != before.Zdo)
                throw new InvalidOperationException("Construction context or before-inventory changed; native attempt refused.");
            nativeAttempt = true; Status = "placing";
        }
        internal void Created(GameObject instance)
        {
            if (!nativeAttempt || created || !Context() || !instance) throw new InvalidOperationException("Unexpected native construction object.");
            var view = instance.GetComponent<ZNetView>();
            if (!view || !view.IsValid() || !view.IsOwner() || view.GetZDO().m_uid.IsNone() ||
                view.GetZDO().GetPrefab() != Piece.gameObject.name.GetStableHashCode()) throw new InvalidOperationException("Fresh native identity is missing.");
            created = instance; zdo = view.GetZDO(); nativeId = zdo.m_uid;
            if (!string.IsNullOrEmpty(zdo.GetString(MarkerKey, ""))) throw new InvalidOperationException("Native object already carries project evidence.");
            zdo.Set(MarkerKey, operation.ToString("N"));
        }
        internal void PlacementResult(bool success, bool failedSign) { nativeSuccess = success; signFailed = failedSign; }

        internal void EndNative(BlueprintConstructionRefund payment, Exception error)
        {
            try
            {
                if (Finished || !nativeAttempt || error != null || payment == null || payment.Error != null || !payment.IsFor(inventory) || !Context() ||
                    !BlueprintStorageCapture.TrySnapshotInventory(inventory, out var after))
                    throw new InvalidOperationException(error?.Message ?? payment?.Error ?? "Native construction outcome is uncertain.");
                var paid = payment.DescribePaid().Select(row => Row(row.Item, row.Before, row.Paid)).ToArray();
                if (!nativeSuccess)
                {
                    if (created || zdo != null || paid.Length != 0) throw new InvalidOperationException("Native failure retains object or payment evidence.");
                    journal.RecordNoEffect(plan.Id, part.Id, operation, after, freshObjectAbsent: true);
                    Status = "no-effect"; Finished = true; return;
                }
                if (signFailed || !ObjectValid()) throw new InvalidOperationException("Native construction pose/sign/identity differs from the plan.");
                bool free = paid.Length == 0 && (player.NoCostCheat() || BlueprintResourceHudView.Free(GlobalKeys.NoBuildCost) ||
                    !(Piece.m_resources ?? Array.Empty<Piece.Requirement>()).Any(r => r?.m_resItem && r.m_amount > 0));
                Observe(after, free, paid);
            }
            catch (Exception failure) { Refuse(failure.Message); }
        }

        private void Observe(BlueprintForeignPartData after, bool free, Journal.PaidRow[] paid)
        {
            observed = journal.Observe(plan.Id, part.Id, operation, nativeId.UserID, nativeId.ID, zdo.GetPrefab(),
                created.GetComponent<Piece>().GetCreator(), after, free, paid);
            zdo.Set(ReceiptKey, observed.Receipt);
            if (!BlueprintConstructionProjectPersistence.TryBegin(journal, observed, Custody, out persistence, out string reason))
                throw new InvalidOperationException(reason);
            Status = "saving";
        }

        private bool ObjectValid()
        {
            if (!Context() || !created || zdo == null || zdo.m_uid != nativeId || !created.GetComponent<ZNetView>() ||
                !ReferenceEquals(created.GetComponent<ZNetView>().GetZDO(), zdo) || !created.GetComponent<ZNetView>().IsOwner() ||
                zdo.GetPrefab() != Piece.gameObject.name.GetStableHashCode() || zdo.GetString(MarkerKey, "") != operation.ToString("N") ||
                !created.GetComponent<Piece>() || created.GetComponent<Piece>().GetCreator() != playerId) return false;
            return Vector3.Distance(created.transform.position, Pose.Position) <= .001f && Quaternion.Angle(created.transform.rotation, Pose.Rotation) <= .01f &&
                Vector3.Distance(created.transform.localScale, Vector3.Scale(Piece.transform.localScale, Pose.Scale)) <= .001f;
        }
        private bool Custody() => ObjectValid() && observed != null && zdo.GetByteArray(ReceiptKey)?.SequenceEqual(observed.Receipt) == true &&
            BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) && current.Info == observed.AfterInfo && current.Zdo == observed.AfterBytes;
        internal void Tick()
        {
            if (Finished || persistence == null) return;
            var state = persistence.Tick();
            if (state == BlueprintConstructionProjectPersistence.Result.Verified) { Finished = true; Status = "committed"; }
            else if (state == BlueprintConstructionProjectPersistence.Result.Refused) Refuse(persistence.Reason);
        }
        internal void StopBeforeNative(string reason)
        {
            if (Finished || Saving) return;
            try
            {
                if (nativeAttempt || created || zdo != null || !Context() || !BlueprintStorageCapture.TrySnapshotInventory(inventory, out var unchanged))
                    throw new InvalidOperationException(reason);
                journal.RecordNoEffect(plan.Id, part.Id, operation, unchanged, freshObjectAbsent: true);
                Finished = true; Status = "no-effect"; Reason = reason;
            }
            catch (Exception failure) { Refuse(failure.Message); }
        }
        internal void Refuse(string reason) { persistence?.Cancel(); Finished = true; Status = "reconciliation"; Reason = reason; }
        private static Journal.PaidRow Row(ItemDrop.ItemData item, int before, int paid) => new Journal.PaidRow {
            Prefab = item.m_dropPrefab.name, Properties = BlueprintItemStandPlacement.SnapshotItem(item), Before = before, Paid = paid,
            X = item.m_gridPos.x, Y = item.m_gridPos.y, Variant = item.m_variant, Quality = item.m_quality };
    }
}
