#if BUILDWORKS_TEST_STAND
using System;
using System.Collections;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Explicit offline QA only. No automatic provisioning, replay, refunds or journal settlement.
    internal sealed class BuildWorksTestStorage : MonoBehaviour
    {
        private const string Scenario = BuildWorksTestStorageReceipt.Scenario;
        private IEnumerator steps;
        private BlueprintStorageTransfer transfer;
        private CancellationTokenSource cancellation;
        private Player player;
        private ZNet network;
        private float deadline;
        private string stamp;
        private Guid operation;
        public string Status { get; private set; } = "idle";
        internal bool Busy => steps != null;

        internal string Begin(bool reload)
        {
            if (steps != null) throw new InvalidOperationException("Storage QA is already running.");
            BuildWorksPlugin.RequireQaStorageIdle();
            player = BuildWorksTestIsolation.RequireLoadedSession(); network = ZNet.instance;
            if (!Application.isFocused || network.IsSaving() || InventoryGui.IsVisible())
                throw new InvalidOperationException("Storage QA needs focused idle Local survival and a closed inventory.");
            stamp = BuildWorksTestIsolation.SupplyStamp(Scenario);
            operation = Guid.ParseExact(stamp.Substring(0, 32), "N");
            if (reload)
            {
                if (!BuildWorksTestIsolation.OnceComplete(BuildWorksTestIsolation.StorageReceiptPath, stamp))
                    throw new InvalidOperationException("No complete saved storage QA receipt.");
            }
            else if (File.Exists(BuildWorksTestIsolation.StorageReceiptPath) || File.Exists(BuildWorksTestIsolation.StorageDetailsPath))
                throw new InvalidOperationException("Storage QA was already requested; inspect its retained receipt, never replay it.");
            cancellation?.Dispose(); cancellation = new CancellationTokenSource();
            steps = reload ? Reload() : Transfer(); deadline = Time.realtimeSinceStartup + 180f;
            Status = reload ? "checking-reload" : "starting";
            return Status;
        }

        private void Update()
        {
            if (steps == null) return;
            try
            {
                BuildWorksPlugin.RequireQaStorageIdle();
                if (!Application.isFocused || UnityEngine.Input.GetKey(KeyCode.F12) || Time.realtimeSinceStartup > deadline ||
                    BuildWorksTestIsolation.RequireLoadedSession() != player || ZNet.instance != network)
                    throw new InvalidOperationException("Storage QA stopped by focus, session, F12 or deadline.");
                if (!steps.MoveNext()) Release();
            }
            catch (Exception error)
            {
                Status = "refused: " + error.GetBaseException().Message;
                Debug.LogError("Storage QA " + Status);
                Cancel(error.GetBaseException().Message);
            }
        }

        private static string PathFor(string path) { BuildWorksTestIsolation.ValidateSavePath(path); return path; }
        private void Record(string stage)
        {
            Status = stage;
            using (var file = new FileStream(PathFor(BuildWorksTestIsolation.StorageLogPath), FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var writer = new StreamWriter(file))
            { writer.WriteLine(DateTime.UtcNow.ToString("o") + "|" + operation.ToString("N") + "|" + stage.Replace('\n', ' ').Replace('\r', ' ')); writer.Flush(); file.Flush(true); }
        }
        private string JournalPath => Path.Combine(BlueprintStorageTransferJournal.Root, operation.ToString("N") + ".bwtransfer");
        private static int Wood(Inventory inventory) => inventory.GetAllItems().Where(item => item.m_dropPrefab && item.m_dropPrefab.name == "Wood").Sum(item => checked(item.m_stack));

        private IEnumerator Transfer()
        {
            if (BlueprintStorageTransferJournal.HasUnresolved(network.GetWorld().m_uid))
                throw new InvalidOperationException("An unresolved storage journal already exists in this world.");
            GameObject prefab = ZNetScene.instance.GetPrefab("piece_chest_wood");
            Piece piece = prefab ? prefab.GetComponent<Piece>() : null;
            var grid = BlueprintStorageCapture.PrefabGrid(prefab);
            if (!piece || !grid.HasValue || !player.HaveRequirements(piece, Player.RequirementMode.CanBuild) ||
                (piece.m_resources ?? Array.Empty<Piece.Requirement>()).Any(row => row == null || !row.m_resItem || row.m_resItem.gameObject.name != "Wood" || row.m_upgraderResource))
                throw new InvalidOperationException("Known native wood chest, station and exact paid recipe are required.");
            int cost = piece.m_resources.Sum(row => checked(row.GetAmount(0)));
            int before = Wood(player.GetInventory());
            if (cost < 1 || cost > 100 || before < cost + 3 || before > 10000)
                throw new InvalidOperationException("QA backpack lacks chest cost plus three own Wood; nothing was issued.");
            Vector3 position = BuildWorksTestStations.FindStorageSite(player, piece);
            if (!PrivateArea.CheckAccess(position, 0f, false, false) || piece.m_craftingStation &&
                !CraftingStation.HaveBuildStationInRange(piece.m_craftingStation.m_name, position))
                throw new InvalidOperationException("QA chest site lacks native access/station range.");
            if (!BuildWorksTestIsolation.BeginOnce(BuildWorksTestIsolation.StorageReceiptPath, stamp))
                throw new InvalidOperationException("Storage QA receipt already exists.");
            using (var file = new FileStream(PathFor(BuildWorksTestIsolation.StorageDetailsPath), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
            {
                foreach (string value in new[] { Scenario, operation.ToString("N"), network.GetWorld().m_uid.ToString(CultureInfo.InvariantCulture),
                    player.GetPlayerID().ToString(CultureInfo.InvariantCulture), System.Diagnostics.Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture),
                    player.GetInstanceID().ToString(CultureInfo.InvariantCulture), position.x.ToString("R", CultureInfo.InvariantCulture),
                    position.y.ToString("R", CultureInfo.InvariantCulture), position.z.ToString("R", CultureInfo.InvariantCulture),
                    before.ToString(CultureInfo.InvariantCulture), cost.ToString(CultureInfo.InvariantCulture) }) writer.WriteLine(value);
                writer.Flush(); file.Flush(true);
            }
            Record("before-native-chest-placement");
            var previous = new System.Collections.Generic.HashSet<Container>(UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None));
            var payment = new BlueprintConstructionRefund(player.GetInventory(), piece.m_resources);
            player.PlacePiece(piece, position, Quaternion.identity, false, false);
            yield return null;
            Container[] created = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None).Where(value => !previous.Contains(value)).ToArray();
            if (created.Length != 1) throw new InvalidOperationException("Native chest creation was ambiguous; no automatic retry.");
            Container target = created[0];
            Piece actual = target.GetComponent<Piece>(); var view = target.GetComponent<ZNetView>();
            if (!actual || !view || !view.IsValid() || !view.IsOwner() || view.GetZDO().GetPrefab() != prefab.name.GetStableHashCode() ||
                actual.GetCreator() != player.GetPlayerID() || Vector3.Distance(target.transform.position, position) > 0.01f ||
                Quaternion.Angle(target.transform.rotation, Quaternion.identity) > 0.01f || target.GetInventory().GetAllItems().Count != 0 ||
                Vector3.Distance(target.transform.localScale, prefab.transform.localScale) > 0.001f ||
                !player.HaveRequirements(piece, Player.RequirementMode.CanBuild))
                throw new InvalidOperationException("Native chest identity, empty state or payment conditions changed.");
            Record("before-native-construction-payment");
            player.ConsumeResources(piece.m_resources, 0, -1, 1); payment.Complete();
            if (!payment.Recorded || payment.Error != null || payment.DescribePaid().Sum(row => row.Paid) != cost || Wood(player.GetInventory()) != before - cost)
                throw new InvalidOperationException("Exact native chest payment was not observed.");
            Record("native-construction-paid");
            ItemDrop.ItemData provided = player.GetInventory().GetAllItems().FirstOrDefault(item => item.m_dropPrefab && item.m_dropPrefab.name == "Wood" && item.m_stack >= 3);
            if (provided == null) throw new InvalidOperationException("No single remaining own Wood stack supports the partial-transfer test.");
            ItemDrop.ItemData archived = provided.Clone(); archived.m_stack = 2; archived.m_equipped = false;
            archived.m_customData = new System.Collections.Generic.Dictionary<string, string>(archived.m_customData) { ["BuildWorks.QA.ArchivedOnly"] = "must-not-copy" };
            var recipe = new Inventory("QA storage recipe", null, grid.Value.Width, grid.Value.Height);
            if (!recipe.AddItem(archived) || !BlueprintStorageCapture.TrySnapshotInventory(recipe, out var demand))
                throw new InvalidOperationException("Detached archived recipe could not be serialized.");
            var supply = new BlueprintItemStandPlacement.Supply(new System.Collections.Generic.List<BlueprintItemStandPlacement.Reservation> {
                new BlueprintItemStandPlacement.Reservation(provided, 2, BlueprintItemStandPlacement.SnapshotItem(provided)) });
            if (!BlueprintStorageTransfer.TryBegin(operation, player, target.gameObject, demand, supply, payment, new[] { supply }, out transfer, out string reason))
                throw new InvalidOperationException(reason);
            Record("transfer-prepared");
            while (!transfer.CompleteInMemory)
            {
                var result = transfer.MoveNext();
                if (result == BlueprintStorageTransfer.Result.Unchanged || result == BlueprintStorageTransfer.Result.Uncertain)
                    throw new InvalidOperationException("Native transfer refused or uncertain; inspect WAL. " + transfer.Entries.FirstOrDefault()?.Error);
                yield return null;
            }
            if (Wood(player.GetInventory()) != before - cost - 2 || Wood(target.GetInventory()) != 2 ||
                target.GetInventory().GetAllItems().Any(item => item.m_customData.ContainsKey("BuildWorks.QA.ArchivedOnly")))
                throw new InvalidOperationException("Own contents/counts differ or archived properties were copied.");
            Record("own-items-transferred");
            if (!transfer.TryBeginPersistence(out var proof, out reason)) throw new InvalidOperationException(reason);
            while (proof.Tick() != BlueprintStoragePersistence.Result.Verified)
            {
                if (proof.State == BlueprintStoragePersistence.Result.Refused) throw new InvalidOperationException(proof.Reason);
                yield return null;
            }
            if (BlueprintStorageTransferJournal.Read(JournalPath).SavedPair == null) throw new InvalidOperationException("Paired WAL evidence missing.");
            BuildWorksTestIsolation.CompleteOnce(BuildWorksTestIsolation.StorageReceiptPath, stamp);
            Record("saved-awaiting-reload");
        }

        private IEnumerator Reload()
        {
            string path = PathFor(BuildWorksTestIsolation.StorageDetailsPath);
            if (!File.Exists(path) || new FileInfo(path).Length > 2048) throw new InvalidDataException("Storage QA details missing or too large.");
            string[] lines = File.ReadAllLines(path);
            var details = BuildWorksTestStorageReceipt.ParseReloadDetails(lines, operation, network.GetWorld().m_uid, player.GetPlayerID(), System.Diagnostics.Process.GetCurrentProcess().Id, player.GetInstanceID());
            var position = new Vector3(details.X, details.Y, details.Z);
            if (!ZoneSystem.instance.IsZoneLoaded(position))
                throw new InvalidOperationException("QA storage area is not loaded; do not infer missing objects.");
            Container[] all = UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None);
            if (all.Length > 10000) throw new InvalidOperationException("Loaded container observation exceeds QA budget.");
            Container[] matches = all.Where(value => BlueprintStorageCapture.TransferMarker(value.GetComponent<ZNetView>()?.GetZDO()) == operation.ToString("N")).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected exactly one loaded chest with the persisted operation marker.");
            Container target = matches[0];
            string reason = null;
            if (Vector3.Distance(target.transform.position, position) > 0.01f || Quaternion.Angle(target.transform.rotation, Quaternion.identity) > 0.01f ||
                BlueprintStorageTransfer.InspectRecovery(JournalPath, player, target.gameObject, out reason) != BlueprintStorageTransfer.RecoveryState.ExactDelivered)
                throw new InvalidOperationException("Reloaded native inventory/target differs: " + reason);
            var saved = BlueprintStorageTransferJournal.Read(JournalPath);
            if (saved.SavedPair == null || Wood(target.GetInventory()) != 2 || Wood(player.GetInventory()) !=
                details.Before - details.Cost - 2)
                throw new InvalidOperationException("Reloaded quantities or paired receipt differ.");
            Func<bool> custody = () => BlueprintStorageTransfer.InspectRecovery(JournalPath, player, target.gameObject, out _) == BlueprintStorageTransfer.RecoveryState.ExactDelivered;
            if (!BlueprintStoragePlayerProfile.TryCapture(player, operation, custody, out var profile, out reason) || !profile.TryVerify(out var profileEvidence, out reason) ||
                profileEvidence.InventoryHash != saved.SavedPair.PlayerInventoryHash)
                throw new InvalidOperationException(reason ?? "Reloaded canonical character inventory differs.");
            ZDO zdo = target.GetComponent<ZNetView>().GetZDO();
            if (!BlueprintStorageWorldGeneration.TryCapture(operation, operation, zdo.GetPrefab(), saved.Prepared.Creator,
                zdo.GetByteArray(ZDOVars.s_items, null), custody, out var generation, out reason) || !generation.TryStart(cancellation.Token, out reason))
                throw new InvalidOperationException(reason);
            while (!generation.IsCompleted) yield return null;
            if (!generation.TryFinish(out var evidence, out reason) || evidence.InventorySha256 != saved.SavedPair.WorldInventoryHash)
                throw new InvalidOperationException(reason ?? "Reloaded canonical world inventory differs.");
            Record("reload-verified-no-reissue-no-settlement");
        }

        internal void Cancel(string reason)
        {
            if (steps == null) return;
            try { Record("refused: " + reason); }
            catch (Exception error) { Status = "refused: " + reason; Debug.LogError("Storage QA log failed: " + error.Message); }
            finally { cancellation?.Cancel(); Release(); }
        }
        private void Release()
        {
            var previousSteps = steps; var previousTransfer = transfer; steps = null; transfer = null;
            try { (previousSteps as IDisposable)?.Dispose(); }
            finally { previousTransfer?.Dispose(); }
        }
        private void OnDisable() { Cancel("QA storage disabled; completed native actions were not undone."); }
        private void OnDestroy() { Cancel("QA storage destroyed; completed native actions were not undone."); cancellation?.Dispose(); cancellation = null; }
    }
    internal static class BuildWorksTestStorageReceipt
    {
        internal const string Scenario = "native-storage-persistence-v1";
        internal static (float X, float Y, float Z, int Before, int Cost) ParseReloadDetails(string[] lines, Guid operation,
            long world, long player, int process, int instance)
        {
            if (lines == null || lines.Length != 11 || operation == Guid.Empty || world == 0 || player == 0 || process <= 0 || instance == 0 ||
                lines[0] != Scenario || lines[1] != operation.ToString("N") ||
                !long.TryParse(lines[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long savedWorld) || savedWorld != world ||
                !long.TryParse(lines[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long savedPlayer) || savedPlayer != player ||
                !int.TryParse(lines[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int savedProcess) || savedProcess <= 0 ||
                !int.TryParse(lines[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int savedInstance) || savedInstance == 0 ||
                savedProcess == process && savedInstance == instance ||
                !float.TryParse(lines[6], NumberStyles.Float, CultureInfo.InvariantCulture, out float x) ||
                !float.TryParse(lines[7], NumberStyles.Float, CultureInfo.InvariantCulture, out float y) ||
                !float.TryParse(lines[8], NumberStyles.Float, CultureInfo.InvariantCulture, out float z) ||
                new[] { x, y, z }.Any(value => float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 1000000f) ||
                !int.TryParse(lines[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int before) || before > 10000 ||
                !int.TryParse(lines[10], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cost) || cost < 1 || cost > 100 || before < cost + 3)
                throw new InvalidDataException("Storage reload receipt identity, new Player instance, position or quantities are invalid.");
            return (x, y, z, before, cost);
        }

    }
}
#endif
