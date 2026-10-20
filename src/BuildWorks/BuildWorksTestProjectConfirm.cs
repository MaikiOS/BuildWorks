#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using HarmonyLib;
using OstrixMods.BuildWorks.Geometry;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksTestProjectConfirm
    {
        private static Guid created;
        private static Vector3 createdRoot;
        private static BlueprintForeignPartData removalInventory;
        private static string[] removalDrops;
        public static bool RemovalFinished => BuildWorksPlugin.QaConstructionProjects?.Removal?.Finished == true;
        public static string PrepareRemoval()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession(); var runtime = BuildWorksPlugin.QaConstructionProjects;
            if (created == Guid.Empty || runtime.Read(created).Blueprint.id != "cc552d6270924447b3ebf51e66e09a5b" ||
                runtime.Progress(created).Built != 2 || runtime.Execution?.Status != "committed" ||
                !BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out removalInventory))
                throw new InvalidOperationException("Removal fixture must be this process's own completed two-wall ordinary paid project.");
            removalDrops = DropIdentities();
            var plans = (Store)AccessTools.Field(typeof(BlueprintConstructionProjectRuntime), "store").GetValue(runtime);
            new BlueprintConstructionCancellationJournal(plans).Prepare(created, runtime.Read(created).Revision, Guid.NewGuid(), player.GetPlayerID());
            return RemovalStatus();
        }
        public static string BeginRemoval()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession(); var runtime = BuildWorksPlugin.QaConstructionProjects;
            if (created == Guid.Empty || removalInventory == null) throw new InvalidOperationException("Prepare this process's owned removal fixture first.");
            if (!runtime.Open(runtime.Board(created), player)) throw new InvalidOperationException("Own removal fixture board could not open.");
            runtime.ResumePreparedRemoval(created, runtime.Read(created).Revision);
            return RemovalStatus();
        }
        public static string RemovalStatus()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession(); var runtime = BuildWorksPlugin.QaConstructionProjects;
            var plans = (Store)AccessTools.Field(typeof(BlueprintConstructionProjectRuntime), "store").GetValue(runtime);
            var record = runtime.Read(created); var removal = new BlueprintConstructionCancellationJournal(plans).Read(created);
            var rows = record.Parts.Select(part => runtime.ReadPart(created, part.Id)).ToArray();
            bool inventoryUnchanged = removalInventory != null && BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var current) &&
                current.Info == removalInventory.Info && current.Zdo == removalInventory.Zdo;
            return JsonUtility.ToJson(new RemovalReceipt { id = created.ToString("N"), status = runtime.Removal?.Status,
                reason = runtime.Removal?.Reason, phase = removal?.Phase ?? 0, beforeGeneration = removal?.BeforeGeneration ?? 0,
                savedGeneration = removal?.Saved?.Generation ?? 0, inventoryUnchanged = inventoryUnchanged,
                dropsUnchanged = removalDrops != null && removalDrops.SequenceEqual(DropIdentities()),
                paid = rows.Where(row => row != null).Sum(row => row.Paid.Sum(item => item.Paid)),
                nativeAbsent = rows.All(row => row?.Phase == BlueprintConstructionPartJournal.Committed &&
                    !BlueprintConstructionPartExecution.FindMarked(row.Operation, "woodwall")),
                cancelled = record.Cancelled, board = runtime.Board(created), blocked = runtime.BlocksInput });
        }
        private static string[] DropIdentities() => UnityEngine.Object.FindObjectsByType<ItemDrop>(FindObjectsSortMode.None)
            .Select(drop => drop.GetComponent<ZNetView>()).Where(view => view && view.IsValid())
            .Select(view => view.GetZDO().m_uid.ToString()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        [Serializable] private sealed class RemovalReceipt
        {
            public string id, status, reason;
            public int phase, paid;
            public uint beforeGeneration, savedGeneration;
            public bool inventoryUnchanged, dropsUnchanged, nativeAbsent, cancelled, board, blocked;
        }
        public static string Prepare(int site)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            var session = BuildWorksPlugin.QaPlacementSession;
            session.QaClosePassiveConfirm(); BuildWorksPlugin.RequireQaStorageIdle();
            if (site == 4)
            {
                Exception refusal = null;
                for (int candidate = 0; candidate < 4; ++candidate)
                    try { return session.QaPrepareProjectConfirm(player, BuildWorksPlugin.QaBlueprint("cc552d6270924447b3ebf51e66e09a5b"), candidate); }
                    catch (InvalidOperationException error) { refusal = error; }
                throw new InvalidOperationException("No already flat and empty bounded QA site was found.", refusal);
            }
            return session.QaPrepareProjectConfirm(player, BuildWorksPlugin.QaBlueprint("cc552d6270924447b3ebf51e66e09a5b"), site);
        }
        public static bool Ready => BuildWorksPlugin.QaPlacementSession?.QaProjectConfirmReady == true;
        public static bool Complete => BuildWorksPlugin.QaPlacementSession?.LastCreatedProject is Guid id && id != Guid.Empty &&
            BuildWorksPlugin.QaConstructionProjects.Progress(id).Built == 2;
        public static string Confirm(string mode)
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var session = BuildWorksPlugin.QaPlacementSession;
            string result = session.QaConfirmProject(mode);
            if (session.LastCreatedProject != Guid.Empty) { created = session.LastCreatedProject; createdRoot = session.QaConfirmOrigin; }
            return result;
        }
        public static string RestoreCreated()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            if (created == Guid.Empty) throw new InvalidOperationException("No ordinary project was confirmed by this QA process.");
            BuildWorksPlugin.QaPlacementSession.QaRestoreConfirm(created, createdRoot); return Status();
        }
        public static string Status()
        { BuildWorksTestIsolation.RequireLoadedSession(); return BuildWorksPlugin.QaPlacementSession.QaProjectConfirmStatus(); }
        public static string CancelUnpaid()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var session = BuildWorksPlugin.QaPlacementSession;
            var runtime = BuildWorksPlugin.QaConstructionProjects; var record = runtime.Read(session.LastCreatedProject);
            runtime.CancelUnpaid(record.Id, record.Revision, Guid.NewGuid()); return session.QaProjectConfirmStatus();
        }
    }
    internal sealed partial class PrecisionPlacementSession
    {
        private CompositeBlueprintStore.Blueprint qaConfirmSource;
        private Vector3 qaConfirmRoot;
        private readonly Quaternion qaConfirmRotation = Quaternion.Euler(0, 37, 0);
        internal Vector3 QaConfirmOrigin => qaConfirmRoot;
        internal void QaClosePassiveConfirm()
        {
            if (QaStorageIdle) return;
            if (state != PlacementState.Inactive || sessionMode != SessionMode.BlueprintWorldPlacement || pendingCatalogBlueprint != null)
                throw new InvalidOperationException("QA confirmation cannot interrupt an active editor or placement.");
            Finish();
        }
        internal void QaRestoreConfirm(Guid id, Vector3 origin)
        {
            var record = BuildWorksPlugin.QaConstructionProjects.Read(id);
            if (record.Blueprint.id != "cc552d6270924447b3ebf51e66e09a5b" || record.Parts.Count != 2)
                throw new InvalidOperationException("The confirmed ordinary QA project identity changed.");
            LastCreatedProject = id; qaConfirmRoot = origin; qaConfirmSource = record.Blueprint;
        }
        internal bool QaProjectConfirmReady => state == PlacementState.Inactive && sessionMode == SessionMode.BlueprintWorldPlacement &&
            pendingCatalogBlueprint == null && Player.m_localPlayer && blueprintPieceRegistry.TryGetBlueprint(Player.m_localPlayer.GetSelectedPiece(), out var selected) &&
            selected.id == qaConfirmSource?.id && PlacementGhostField.GetValue(Player.m_localPlayer) is GameObject ghost && ghost.activeInHierarchy;
        internal string QaPrepareProjectConfirm(Player player, CompositeBlueprintStore.Blueprint blueprint, int site)
        {
            if (site < 0 || site > 3 || blueprint.parts.Count != 2 || blueprint.parts.Any(part => part.prefabName != "woodwall" || part.foreignData != null))
                throw new InvalidOperationException("QA confirmation requires the existing two-wall personal blueprint.");
            if ((bool)NoPlacementCostField.GetValue(player) || BlueprintResourceHudView.Free(GlobalKeys.NoBuildCost))
                throw new InvalidOperationException("QA native payment requires normal survival costs.");
            qaConfirmSource = blueprint;
            var offsets = new[] { new Vector3(-3, 0, 3), new Vector3(3, 0, 3), new Vector3(3, 0, -3), new Vector3(-3, 0, -3) };
            qaConfirmRoot = player.transform.position + offsets[site];
            float high = float.MinValue, low = float.MaxValue;
            foreach (var part in blueprint.parts)
            {
                Vector3 point = qaConfirmRoot + qaConfirmRotation * part.position.ToVector3();
                if (!ZoneSystem.instance.IsZoneLoaded(point) || !ZoneSystem.instance.GetGroundHeight(point, out float height))
                    throw new InvalidOperationException("QA confirmation needs already loaded ground; terrain will not change.");
                high = Mathf.Max(high, height); low = Mathf.Min(low, height);
                if (Physics.OverlapBox(new Vector3(point.x, height + 1, point.z), new Vector3(1.05f, .95f, .18f), qaConfirmRotation,
                    LayerMask.GetMask("piece", "piece_nonsolid")).Length > 0) throw new InvalidOperationException("QA confirmation site is occupied; existing construction will not be changed.");
            }
            if (high - low > .015f) throw new InvalidOperationException("QA confirmation site is not flat; terrain will not change.");
            var prefab = ZNetScene.instance.GetPrefab("woodwall");
            if (!BuildWorksPlacementValidation.TryGetBounds(prefab, out var bounds)) throw new InvalidOperationException("QA wall bounds missing.");
            qaConfirmRoot.y = high - (bounds.min.y - prefab.transform.position.y);
            string error = PrepareCatalogSelection(player, blueprint, false, null);
            if (error != null) throw new InvalidOperationException(error);
            return "ordinary-catalog-selection-prepared";
        }
        internal string QaConfirmProject(string mode)
        {
            if (!QaProjectConfirmReady || BuildWorksQaInput.Active || mode != "build" && mode != "plan" && mode != "store-failure" && mode != "post-commit" && mode != "board-failure")
                throw new InvalidOperationException("QA ordinary confirmation context or mode is unavailable.");
            Player player = Player.m_localPlayer; var runtime = BuildWorksPlugin.ConstructionProjects;
            var ghost = (GameObject)PlacementGhostField.GetValue(player);
            blueprintPieceRegistry.TryGetPlacementOrigin(player.GetSelectedPiece(), out Vector3 support);
            Quaternion supportRotation = Quaternion.identity;
            if (!BlueprintPlacementFrame.TryResolve(qaConfirmSource, out support, out supportRotation))
            { blueprintPieceRegistry.TryGetPlacementOrigin(player.GetSelectedPiece(), out support); supportRotation = Quaternion.identity; }
            ghost.transform.SetPositionAndRotation(qaConfirmRoot + qaConfirmRotation * support, qaConfirmRotation * supportRotation);
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var before)) throw new InvalidOperationException("QA inventory snapshot failed.");
            string[] objects = NativeObjects(); int boards = runtime.BoardCount;
            var plans = (Store)AccessTools.Field(typeof(BlueprintConstructionProjectRuntime), "store").GetValue(runtime);
            Guid[] beforeIds = plans.Ids().ToArray();
            var failField = AccessTools.Field(typeof(BlueprintConstructionProjectRuntime), "failed"); bool oldFailure = (bool)failField.GetValue(runtime);
            var input = new BuildWorksQaInput(); var owner = new GameObject("BuildWorks_QA_ConfirmationInput");
            try
            {
                if (mode == "store-failure") failField.SetValue(runtime, true);
                Store.QaFailAfterCommit = mode == "post-commit";
                BlueprintConstructionProjectRuntime.QaFailNextBoard = mode == "board-failure";
                input.Begin(owner);
                input.Feed(new BuildWorksQaControlProtocol.Step { milliseconds = 1, buttons = 1 }, true, Vector2.zero, 1, 0);
                UpdatePlacementMethod.Invoke(player, new object[] { true, Time.deltaTime });
                UpdatePlacementMethod.Invoke(player, new object[] { true, Time.deltaTime });
            }
            finally
            {
                input.Release(abort: true); UnityEngine.Object.DestroyImmediate(owner);
                failField.SetValue(runtime, oldFailure); Store.QaFailAfterCommit = false; BlueprintConstructionProjectRuntime.QaFailNextBoard = false;
            }
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var after) || before.Info != after.Info || before.Zdo != after.Zdo || !objects.SequenceEqual(NativeObjects()))
                throw new InvalidOperationException("Confirmation leaked a native object or debit before its queued journal operation.");
            if (mode == "store-failure" || mode == "post-commit")
            {
                if (LastCreatedProject != Guid.Empty || state != PlacementState.Inactive || runtime.BoardCount != boards)
                    throw new InvalidOperationException("Failed ordinary confirmation fell through to native placement.");
                var afterIds = plans.Ids().ToArray();
                if (mode == "store-failure" ? !beforeIds.SequenceEqual(afterIds) : afterIds.Length != beforeIds.Length + 1 || !afterIds.Contains(placementProjectId))
                    throw new InvalidOperationException("Failed confirmation duplicated or lost its durable identity.");
                return "ordinary-confirmation-refused-no-native-effect;pending=" + placementProjectId.ToString("N");
            }
            if (LastCreatedProject == Guid.Empty) throw new InvalidOperationException("Ordinary patched Player.UpdatePlacement did not create a project.");
            if (plans.Ids().Count() != beforeIds.Length + (beforeIds.Contains(LastCreatedProject) ? 0 : 1))
                throw new InvalidOperationException("Repeated same-frame confirmation created more than one project.");
            if (mode != "build")
            {
                var queue = AccessTools.Field(typeof(BlueprintConstructionProjectRuntime), "queued");
                if ((Guid)queue.GetValue(runtime) == LastCreatedProject) queue.SetValue(runtime, Guid.Empty);
            }
            return QaProjectConfirmStatus();
        }
        internal string QaProjectConfirmStatus()
        {
            var runtime = BuildWorksPlugin.ConstructionProjects;
            if (LastCreatedProject == Guid.Empty) return "no-confirmed-project";
            var record = runtime.Read(LastCreatedProject); var progress = runtime.Progress(record.Id);
            var blueprint = record.Blueprint;
            bool exact = record.Parts.Select((part, i) => {
                float[] p = part.WorldPose.Values; Vector3 expected = qaConfirmRoot + qaConfirmRotation * blueprint.parts[i].position.ToVector3();
                return Vector3.Distance(expected, new Vector3(p[0], p[1], p[2])) < .001f &&
                    Quaternion.Angle(qaConfirmRotation * blueprint.parts[i].rotation.ToQuaternion(), new Quaternion(p[3], p[4], p[5], p[6])) < .01f;
            }).All(value => value);
            var rows = record.Parts.Select(part => runtime.ReadPart(record.Id, part.Id)).ToArray();
            bool native = rows.Where(row => row?.Phase == BlueprintConstructionPartJournal.Committed).All(row => {
                var instance = BlueprintConstructionPartExecution.FindMarked(row.Operation, "woodwall");
                float[] p = record.Parts.Single(part => part.Id == row.Part).WorldPose.Values;
                return instance && instance.GetComponent<ZNetView>().GetZDO().GetByteArray(BlueprintConstructionPartExecution.ReceiptKey)?.SequenceEqual(row.Receipt) == true &&
                    Vector3.Distance(instance.transform.position, new Vector3(p[0], p[1], p[2])) < .001f &&
                    Quaternion.Angle(instance.transform.rotation, new Quaternion(p[3], p[4], p[5], p[6])) < .01f &&
                    Vector3.Distance(instance.transform.localScale, new Vector3(p[7], p[8], p[9])) < .001f;
            });
            return JsonUtility.ToJson(new ProjectConfirmReceipt { id = record.Id.ToString("N"), built = progress.Built, total = progress.Total,
                board = runtime.Board(record.Id), cancelled = record.Cancelled, exactPoses = exact, nativeReceipts = native,
                sessionClosed = QaStorageIdle, blocked = runtime.BlocksInput, failure = runtime.Failure ?? runtime.Execution?.Reason,
                wood = Player.m_localPlayer.GetInventory().CountItems("$item_wood", -1, true),
                fogVisible = runtime.Fog?.Building(record.Id)?.VisibleCount ?? 0,
                journalRows = rows.Count(row => row != null), paid = rows.Where(row => row != null).Sum(row => row.Paid.Sum(item => item.Paid)) });
        }
        private static string[] NativeObjects() => UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
            .Where(view => view && view.IsValid()).Select(view => view.GetZDO().m_uid.ToString()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        [Serializable] private sealed class ProjectConfirmReceipt
        {
            public string id, failure;
            public bool board, cancelled, exactPoses, nativeReceipts, sessionClosed, blocked;
            public int built, total, wood, fogVisible, journalRows, paid;
        }
    }
}
#endif
