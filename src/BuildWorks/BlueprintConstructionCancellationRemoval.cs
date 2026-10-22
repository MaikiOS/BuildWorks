using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;

namespace OstrixMods.BuildWorks
{
    // shortcut: private Local structural removal only; enable UI cancellation after durable item restitution is complete.
    internal sealed class BlueprintConstructionCancellationRemoval
    {
        internal sealed class Witness
        {
            internal WearNTear Wear;
            internal ZDO Data;
            internal ZDOID Id;
            internal bool WearConfirmed, NetworkConfirmed;
        }
        private static Witness active;
        private static readonly FieldInfo NativeDestroyQueue = typeof(ZDOMan).GetField("m_destroySendList", BindingFlags.Instance | BindingFlags.NonPublic);
        private readonly Journal journal;
        private readonly Journal.Record record;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData beforeInventory;
        private readonly ZNet network;
        private readonly World world;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly Guid[] operations;
        private readonly Witness[] targets;
        private BlueprintStoragePlayerProfile profile;
        private Geometry.NativePlayerProfileReadback.Evidence savedProfile;
        private BlueprintStorageWorldGeneration generation;
        internal string Status { get; private set; } = "removing";
        internal string Reason { get; private set; }
        internal bool Finished => Status == "removed-saved" || Status == "reconciliation";

        internal BlueprintConstructionCancellationRemoval(Plans plans, Guid project, long revision, Guid operation)
        {
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            network = ZNet.instance; world = network ? network.GetWorld() : null;
            journal = new Journal(plans);
            var pending = journal.Read(project);
            if (active != null || !Context() || network.IsSaving() || SaveSystem.GetSaveNumber() == 0 || SaveSystem.GetSaveNumber() == uint.MaxValue ||
                pending != null && (pending.Phase != Journal.Prepared || pending.Operation != operation || pending.Player != player.GetPlayerID()) ||
                !PrecisionPlacementSession.HasUsableHammer(player) ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out beforeInventory))
                throw new InvalidOperationException("Removal needs an idle private Local context and exact inventory; uncertain removal intent cannot be replayed.");
            var plan = plans.Load(project); var parts = new Parts(plans);
            targets = plan.Parts.Select(part => (Part: part, Receipt: parts.Read(project, part.Id)))
                .Where(row => row.Receipt?.Phase == Parts.Committed).Select(row => Resolve(plan, row.Part, row.Receipt)).ToArray();
            if (targets.Length == 0 || NativeDestroyQueue?.GetValue(ZDOMan.instance) is not List<ZDOID>)
                throw new InvalidOperationException("No supported committed targets or native removal queue is unavailable.");
            BlueprintConstructionPayoutIssuance.RequireRefundable(plans, project, player.GetPlayerID());
            record = journal.Prepare(project, revision, operation, player.GetPlayerID());
            operations = record.Parts.Where(p => p.Phase == Parts.Committed).Select(p => p.Operation).ToArray();
            if (targets.Length != operations.Length || !Custody()) throw new InvalidOperationException("Removal preflight changed.");
            record = journal.BeginRemoval(project, operation, SaveSystem.GetSaveNumber());
            try
            {
                foreach (var target in targets)
                {
                    if (!Custody() || !target.Wear || !target.Data.IsValid()) throw new InvalidOperationException("Removal target/context changed.");
                    active = target;
                    try { target.Wear.Remove(true); }
                    finally { active = null; }
                    if (!target.WearConfirmed || !target.NetworkConfirmed)
                        throw new InvalidOperationException("Native removal did not prove suppressed drops and the exact queued identity.");
                }
                Status = "queued-removal";
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        private Witness Resolve(Plans.Record plan, Plans.Part part, Parts.Record receipt)
        {
            var source = plan.Blueprint.parts[part.SourceIndex];
            var prefab = ZNetScene.instance.GetPrefab(source.prefabName); var piece = prefab ? prefab.GetComponent<Piece>() : null;
            var instance = BlueprintConstructionPartExecution.FindMarked(receipt.Operation, source.prefabName);
            var view = instance ? instance.GetComponent<ZNetView>() : null;
            var data = view ? view.GetZDO() : null; var wear = instance ? instance.GetComponent<WearNTear>() : null;
            float[] pose = part.WorldPose.Values;
            if (receipt.Player != player.GetPlayerID() || !piece || !PrecisionPlacementSession.SupportsProjectPart(piece) ||
                !instance || !view || !view.IsValid() || !view.IsOwner() || data == null || !data.IsValid() || data.m_uid.IsNone() ||
                data.GetPrefab() != receipt.PrefabHash || !instance.GetComponent<Piece>() || instance.GetComponent<Piece>().GetCreator() != receipt.Creator ||
                data.GetString(BlueprintConstructionPartExecution.MarkerKey, "") != receipt.Operation.ToString("N") ||
                data.GetByteArray(BlueprintConstructionPartExecution.ReceiptKey)?.SequenceEqual(receipt.Receipt) != true ||
                !wear || wear.m_onDestroyed != null || instance.GetComponentsInChildren<MonoBehaviour>(true).Any(component => component is IRemoved) ||
                source.foreignData != null ||
                Vector3.Distance(instance.transform.position, new Vector3(pose[0], pose[1], pose[2])) > .001f ||
                Quaternion.Angle(instance.transform.rotation, new Quaternion(pose[3], pose[4], pose[5], pose[6])) > .01f ||
                Vector3.Distance(instance.transform.localScale, Vector3.Scale(prefab.transform.localScale, new Vector3(pose[7], pose[8], pose[9]))) > .001f)
                throw new InvalidOperationException("Removal requires one loaded, owned structural object with exact receipt, creator and pose; removal callbacks are unsupported.");
            return new Witness { Wear = wear, Data = data, Id = data.m_uid };
        }
        private bool Context() => Thread.CurrentThread.ManagedThreadId == thread && network && ReferenceEquals(network, ZNet.instance) &&
            BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement && ReferenceEquals(network.GetWorld(), world) && world != null &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) && player && player == Player.m_localPlayer && !player.IsDead() &&
            ReferenceEquals(inventory, player.GetInventory()) && (record == null || world.m_uid == record.World && player.GetPlayerID() == record.Player);
        private bool Custody() => Context() && BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) &&
            current.Info == beforeInventory.Info && current.Zdo == beforeInventory.Zdo;
        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (!Custody() || cancellation.IsCancellationRequested || DateTime.UtcNow > deadline)
                    throw new InvalidOperationException("Removal context/inventory changed; reconciliation is required.");
                if (Status == "queued-removal")
                {
                    if (network.IsSaving() || SaveSystem.GetSaveNumber() != record.BeforeGeneration)
                        throw new InvalidOperationException("Removal save generation changed before native deletion completed.");
                    // DestroyZDO queues the ID; only a later native SendDestroyed removes it from the save set.
                    if (targets.Any(target => ZDOMan.instance.GetZDO(target.Id) != null || ZNetScene.instance.FindInstance(target.Id))) return;
                    if (!BlueprintStoragePlayerProfile.TryCapture(player, record.Operation, Custody, out profile, out string error) ||
                        !profile.TrySaveAndVerify(out savedProfile, out error)) throw new InvalidOperationException(error);
                    if (!Custody() || network.IsSaving() || SaveSystem.GetSaveNumber() != record.BeforeGeneration)
                        throw new InvalidOperationException("Removal save generation changed before the save request.");
                    network.Save(sync: false, saveOtherPlayerProfiles: false, waitForNextFrame: false);
                    if (!Custody() || !network.IsSaving() && SaveSystem.GetSaveNumber() != record.BeforeGeneration + 1)
                        throw new InvalidOperationException("Removal world save did not start the next generation.");
                    Status = "saving"; return;
                }
                if (network.IsSaving()) return;
                if (SaveSystem.GetSaveNumber() != record.BeforeGeneration + 1) throw new InvalidOperationException("Removal committed a different save generation.");
                if (Status == "saving")
                {
                    if (!BlueprintStorageWorldGeneration.TryCaptureAbsence(record.Operation, BlueprintConstructionPartExecution.MarkerKey,
                        operations, Custody, out generation, out string error) || generation.Generation != record.BeforeGeneration + 1 ||
                        !generation.TryStart(cancellation.Token, out error)) throw new InvalidOperationException(error ?? "Removal absence scan could not start.");
                    Status = "reading";
                }
                if (!generation.IsCompleted) return;
                if (!generation.TryFinishAbsence(out var absence, out string reason) || absence.World != record.World ||
                    absence.Generation != record.BeforeGeneration + 1 || absence.MarkerKey != BlueprintConstructionPartExecution.MarkerKey ||
                    !absence.CompleteMappedScan || !absence.Operations.SequenceEqual(operations) ||
                    !profile.TryVerify(out var currentProfile, out reason) || currentProfile.ProfileHash != savedProfile.ProfileHash ||
                    currentProfile.InventoryHash != savedProfile.InventoryHash || !Custody() || network.IsSaving())
                    throw new InvalidOperationException(reason ?? "Removal paired save or exact absence evidence differs.");
                journal.ConfirmRemovedSaved(record.Project, record.Operation, new Journal.SaveProof { BeforeGeneration = record.BeforeGeneration,
                    Generation = absence.Generation, ProfileHash = currentProfile.ProfileHash, InventoryHash = currentProfile.InventoryHash });
                Status = "removed-saved";
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason) { cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
        internal static Witness CaptureWear(WearNTear wear, bool blockDrop)
        {
            if (active == null || !ReferenceEquals(active.Wear, wear)) return null;
            if (!blockDrop) throw new InvalidOperationException("Construction cancellation cannot invoke native resource drops.");
            return active;
        }
        internal static void ConfirmWear(Witness witness, bool blockDrop, Exception error)
        { if (witness != null) witness.WearConfirmed = blockDrop && error == null; }
        internal static Witness CaptureNetwork(ZDO data) => active != null && ReferenceEquals(active.Data, data) ? active : null;
        internal static void ConfirmNetwork(Witness witness, Exception error)
        {
            if (witness == null) return;
            var queue = NativeDestroyQueue?.GetValue(ZDOMan.instance) as List<ZDOID>;
            witness.NetworkConfirmed = error == null && queue != null && queue.Count > 0 && queue[queue.Count - 1] == witness.Id;
        }
    }
}
