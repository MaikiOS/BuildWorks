using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    // shortcut: private Local host only; authenticated remote operations require their own persistence/authority proof.
    internal sealed class BlueprintConstructionProjectRuntime : IDisposable
    {
        private readonly string root;
        private readonly Action<Exception> log;
        private readonly Dictionary<Guid, BlueprintConstructionProjectBoard> boards = new Dictionary<Guid, BlueprintConstructionProjectBoard>();
        private ZNet network;
        private long world;
        private Store store;
        private readonly BlueprintConstructionProjectView view = new BlueprintConstructionProjectView();
        private bool failed, disposed;
        private BlueprintConstructionPartExecution execution;
        private BlueprintConstructionCancellationRemoval removal;
        private BlueprintConstructionPayoutIssuance payout;
        private BlueprintConstructionPayoutCollection collection;
        private BlueprintConstructionPayoutCleanup cleanup;
        private BlueprintConstructionPaidCancellation paidCancellation;
        private readonly HashSet<Guid> cleanupProjects = new HashSet<Guid>();
        private readonly HashSet<Guid> cancellationProjects = new HashSet<Guid>();
        private Guid queued;
        private int queuedFrame;
        private bool recoveryOnly;
        private BlueprintConstructionProjectFog fog;
        private BlueprintConstructionPartExecution fogExecution;
        private string fogExecutionStatus;
#if BUILDWORKS_TEST_STAND
        internal static bool QaFailNextBoard;
        internal bool? QaImmediateFogReceipts { get; private set; }

        internal void QaResetFogForReceipts(Guid project, bool? immediate)
        {
            BuildWorksTestIsolation.RequireLoadedSession(); RequireContext();
            if (!IsPrivateLocalPlacement || BlocksInput || queued != Guid.Empty || OpenBoard ||
                execution != null && !execution.Finished || removal != null && !removal.Finished || PayoutBusy)
                throw new InvalidOperationException("Receipt comparison needs an idle private QA project.");
            var plan = Read(project); var progress = Progress(project);
            if (plan.Cancelled || progress.Built != 0 || progress.Total != 28 || progress.NeedsReview ||
                boards.Values.Any(board => board && board.Id != project && Progress(board.Id).Built != board.PartCount))
                throw new InvalidOperationException("Receipt comparison requires exactly one unfinished 28-part project.");
            if (!ReferenceEquals(fog, null) && !fog.Close())
                throw new InvalidOperationException("Previous fog pool did not confirm GPU teardown; receipt mode unchanged.");
            fog = null; fogExecution = null; fogExecutionStatus = null;
            QaImmediateFogReceipts = immediate;
            TickFog();
        }
#endif
        internal BlueprintConstructionProjectFog Fog => fog;
        internal string FogError(Guid id) => fog ? fog.Error(id) : null;
        internal BlueprintConstructionPartExecution Execution => execution;
        internal BlueprintConstructionCancellationRemoval Removal => removal;
        internal BlueprintConstructionPayoutIssuance Payout => payout;
        internal BlueprintConstructionPayoutCollection Collection => collection;
        internal BlueprintConstructionPayoutCleanup Cleanup => cleanup;
        internal BlueprintConstructionPaidCancellation PaidCancellation => paidCancellation;
        internal int BoardCount => boards.Count;
        internal long World => world;
        internal string Failure { get; private set; }
        internal BlueprintConstructionProjectBoard OpenBoard { get; private set; }
        private bool PayoutBusy => payout != null && !payout.Finished || collection != null && !collection.Finished || cleanup != null && !cleanup.Finished ||
            paidCancellation != null && !paidCancellation.Finished;
        internal bool BlocksInput => view.IsOpen || execution?.Saving == true || removal != null && !removal.Finished || PayoutBusy;
        internal static bool IsPrivateLocalPlacement => ZNet.instance && Player.m_localPlayer && Game.instance &&
            !Game.instance.IsShuttingDown() && ZNet.instance.IsServer() && !ZNet.instance.IsDedicated() &&
            !ZNet.IsOpenServer() && ZNet.instance.GetPeers().Count == 0 && ZNet.instance.LocalPlayerIsAdminOrHost() &&
            ZNet.instance.GetWorld()?.m_fileSource == FileHelpers.FileSource.Local;
        internal bool CanCreateFromPlacement => !disposed && !failed && store != null && Allowed() &&
            IsPrivateLocalPlacement;

        internal BlueprintConstructionProjectRuntime(string configPath, Action<Exception> log)
        { root = Path.Combine(configPath ?? throw new ArgumentNullException(nameof(configPath)), "BuildWorks", "server"); this.log = log; }

        internal void Tick()
        {
            if (disposed) return;
            ZNet current = ZNet.instance; long uid = WorldId(current);
            if (network != current || uid != world) { Suspend(); network = current; world = uid; }
            if (!Allowed()) { execution?.Refuse("Construction context closed; evidence retained."); removal?.Refuse("Construction context closed; removal evidence retained."); payout?.Refuse("Construction context closed; payout evidence retained."); collection?.Refuse("Construction context closed; credit evidence retained."); cleanup?.Refuse("Construction context closed; cleanup evidence retained."); paidCancellation?.Refuse("Construction context closed; cancellation evidence retained."); execution = null; queued = Guid.Empty; cleanupProjects.Clear(); cancellationProjects.Clear(); ClearBoards(); store = null; return; }
            removal?.Tick();
            bool paying = payout != null && !payout.Finished;
            payout?.Tick();
            if (paying && payout.Finished && payout.Status == "issued-saved") cancellationProjects.Add(payout.Project);
            TickPaidCancellation();
            TickPayoutCollection();
            if (execution != null && !execution.Finished)
            {
                execution.Tick();
                if (execution.Finished && execution.Status == "committed" && !recoveryOnly) { queued = execution.Project; queuedFrame = Time.frameCount; }
            }
            if (queued != Guid.Empty && Time.frameCount > queuedFrame && !view.IsOpen && BuildWorksPlugin.CanOpenConstructionProject)
            {
                Guid id = queued; queued = Guid.Empty;
                try
                {
                    var part = NextPart(id);
                    if (part != null)
                    {
                        recoveryOnly = false;
                        execution = new BlueprintConstructionPartExecution(store, id, part.Id);
                        if (!BuildWorksPlugin.BeginConstructionProjectPart(execution)) execution.StopBeforeNative("Native session could not start.");
                    }
                }
                catch (Exception error) { Failure = error.Message; log?.Invoke(error); }
            }
            if (OpenBoard && (!Player.m_localPlayer || Player.m_localPlayer.IsDead() ||
                !HasHammer(Player.m_localPlayer) || Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5)) Close();
            try { view.Tick(this, OpenBoard); }
            catch (Exception error) { Close(); log?.Invoke(error); }
            if (!failed && store == null) try
            {
                store = new Store(Path.Combine(root, "world_" + world.ToString(CultureInfo.InvariantCulture), "projects"), world);
                foreach (Guid id in store.Ids())
                {
                    Store.Record record = store.Load(id);
                    if (!record.Cancelled) AddBoard(record);
                    cleanupProjects.Add(id);
                    if (!record.Cancelled) cancellationProjects.Add(id);
                }
            }
            catch (Exception error) { ClearBoards(); store = null; failed = true; Failure = error.Message; log?.Invoke(error); }
            TickFog();
        }

        internal Store.Record Create(Guid id, CompositeBlueprintStore.Blueprint blueprint, Store.Pose rootPose, IReadOnlyList<Store.Pose> poses)
        {
            RequireContext();
            if (PayoutBusy) throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            Store.Record record = store.Create(id, blueprint, rootPose, poses);
            // A confirmed durable write remains successful even when its presentation cannot be created.
            try
            {
#if BUILDWORKS_TEST_STAND
                if (QaFailNextBoard) { QaFailNextBoard = false; throw new IOException("QA deliberate post-commit board failure."); }
#endif
                if (!boards.ContainsKey(id)) AddBoard(record); TickFog();
            }
            catch (Exception error) { Failure = error.Message; log?.Invoke(error); }
            return record;
        }

        internal Store.Record Read(Guid id) { RequireContext(); return store.Load(id); }
        internal bool StartCreated(Guid id)
        {
            RequireContext();
            if (!CanCreateFromPlacement || !Board(id) || queued != Guid.Empty || OpenBoard || PayoutBusy || execution != null && !execution.Finished) return false;
            if (NextPart(id) == null) return false;
            Failure = null; recoveryOnly = false; queued = id; queuedFrame = Time.frameCount; return true;
        }
        internal BlueprintConstructionPartJournal.Record ReadPart(Guid id, Guid part) { RequireContext(); return new BlueprintConstructionPartJournal(store).Read(id, part); }
        internal (int Built, int Total, bool NeedsReview) Progress(Guid id)
        {
            var record = Read(id); var rows = record.Parts.Select(part => ReadPart(id, part.Id)).ToArray();
            return (rows.Count(row => row?.Phase == BlueprintConstructionPartJournal.Committed), rows.Length, rows.Any(row => row?.RequiresReconciliation == true));
        }
        private Store.Part NextPart(Guid id)
        {
            BlueprintConstructionCancellationJournal.RequireNotStarted(store, id);
            var plan = Read(id); if (plan.Cancelled) throw new InvalidOperationException("Construction project was cancelled.");
            if (Progress(id).NeedsReview) throw new InvalidOperationException(BuildWorksLocalization.Text("project.review"));
            return plan.Parts.FirstOrDefault(part => ReadPart(id, part.Id)?.Phase != BlueprintConstructionPartJournal.Committed);
        }
        internal void Continue(Guid id)
        {
            RequireContext();
            if (!OpenBoard || OpenBoard.Id != id || !HasHammer(Player.m_localPlayer) || queued != Guid.Empty || PayoutBusy || execution != null && !execution.Finished ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            if (NextPart(id) == null) return;
            Failure = null; queued = id; queuedFrame = Time.frameCount; Close();
        }
        // Explicit operator recovery only; this never places, pays, refunds or automatically advances the project.
        internal void RecoverKnownObject(Guid project, Guid part, Guid operation)
        {
            RequireContext();
            BlueprintConstructionCancellationJournal.RequireNotStarted(store, project);
            if (!OpenBoard || OpenBoard.Id != project || queued != Guid.Empty || PayoutBusy || execution != null && !execution.Finished ||
                !HasHammer(Player.m_localPlayer) ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            Close(); recoveryOnly = true;
            execution = new BlueprintConstructionPartExecution(store, project, part, operation);
        }
        internal BlueprintConstructionProjectBoard Board(Guid id) => boards.TryGetValue(id, out var board) ? board : null;

        internal ConstructionCancellationAction CancellationAction(Guid id, long revision)
        {
            RequireContext();
            return BlueprintConstructionCancellationAction.Read(store, id, revision, Player.m_localPlayer.GetPlayerID());
        }
        internal void RequestCancellationFromBoard(Guid id, long revision, Guid operation)
        {
            RequireContext();
            var player = Player.m_localPlayer;
            if (!IsPrivateLocalPlacement || !player || player.IsDead() || !OpenBoard || OpenBoard.Id != id || Board(id) != OpenBoard ||
                !BuildWorksPlugin.CanOpenConstructionProject || !HasHammer(player) ||
                Vector3.Distance(player.transform.position, OpenBoard.transform.position) > 5 || queued != Guid.Empty ||
                PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished || operation == Guid.Empty)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            switch (CancellationAction(id, revision))
            {
                case ConstructionCancellationAction.Unpaid: CancelUnpaid(id, revision, operation); return;
                case ConstructionCancellationAction.Remove: BeginPaidRemoval(id, revision, operation); return;
                case ConstructionCancellationAction.ResumeRemoval: ResumePreparedRemoval(id, revision); return;
                case ConstructionCancellationAction.IssueRefund: BeginPayout(id); return;
                case ConstructionCancellationAction.AwaitCollection: cancellationProjects.Add(id); Close(); return;
                default: throw new InvalidOperationException(BuildWorksLocalization.Text("project.review"));
            }
        }

        internal void ResumePreparedRemoval(Guid id, long revision)
        {
            RequireContext();
            var pending = new BlueprintConstructionCancellationJournal(store).Read(id);
            if (pending?.Phase != BlueprintConstructionCancellationJournal.Prepared || pending.PlanRevision != revision ||
                pending.Player != Player.m_localPlayer.GetPlayerID()) throw new InvalidOperationException("Only the same player's prepared cancellation can resume.");
            BeginPaidRemoval(id, revision, pending.Operation);
        }
        internal void BeginPaidRemoval(Guid id, long revision, Guid operation)
        {
            RequireContext();
            if (!IsPrivateLocalPlacement || !OpenBoard || OpenBoard.Id != id || !HasHammer(Player.m_localPlayer) ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5 || queued != Guid.Empty ||
                PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            var next = new BlueprintConstructionCancellationRemoval(store, id, revision, operation);
            Close(); removal = next;
        }

        internal void BeginPayout(Guid id)
        {
            RequireContext();
            if (!IsPrivateLocalPlacement || !OpenBoard || OpenBoard.Id != id || !HasHammer(Player.m_localPlayer) ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5 || queued != Guid.Empty ||
                PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            Vector3 position = OpenBoard.transform.position + Vector3.up;
            var next = new BlueprintConstructionPayoutIssuance(store, id, position);
            Close(); payout = next;
        }

        internal void BeginCollection(Guid id, int index)
        {
            RequireContext();
            if (!IsPrivateLocalPlacement || !OpenBoard || OpenBoard.Id != id || !HasHammer(Player.m_localPlayer) ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5 || queued != Guid.Empty ||
                PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            StartCollection(id, index);
        }

        private void StartCollection(Guid id, int index)
        {
            var next = new BlueprintConstructionPayoutCollection(store, id, index);
            Close(); collection = next; Failure = null;
            if (next.Status == "profile-credited") { cleanupProjects.Add(id); cancellationProjects.Add(id); }
        }

        private void TickPaidCancellation()
        {
            bool finishing = paidCancellation != null && !paidCancellation.Finished;
            paidCancellation?.Tick();
            if (finishing && paidCancellation.Finished)
            {
                if (paidCancellation.Status == "cancelled") { RetireProject(paidCancellation.Project); cleanupProjects.Add(paidCancellation.Project); }
                else { Failure = paidCancellation.Reason; log?.Invoke(new InvalidOperationException(Failure)); }
            }
            if (!IsPrivateLocalPlacement || store == null || cancellationProjects.Count == 0 || queued != Guid.Empty || PayoutBusy ||
                execution != null && !execution.Finished || removal != null && !removal.Finished) return;
            Guid id = cancellationProjects.First(); cancellationProjects.Remove(id);
            try
            {
                if (store.Load(id).Cancelled) return;
                var record = new BlueprintConstructionPayoutJournal(store).Read(id);
                if (record?.Phase != BlueprintConstructionPayoutJournal.IssuedSaved || record.Player != Player.m_localPlayer.GetPlayerID() ||
                    record.Collections?.Any(c => c != null && (c.Phase != BlueprintConstructionPayoutJournal.ProfileCredited ||
                        c.Removal?.Phase == BlueprintConstructionPayoutJournal.CleanupIntent)) == true) return;
                // An existing credit/cleanup intent completes through its own recovery path, then queues this read-only finalizer.
                paidCancellation = new BlueprintConstructionPaidCancellation(store, id);
            }
            catch (Exception error) { Failure = error.Message; log?.Invoke(error); }
        }

        internal bool TryBeginCollection(ItemDrop source, Humanoid user)
        {
            try
            {
                RequireContext();
                var player = Player.m_localPlayer;
                if (!IsPrivateLocalPlacement || !source || !player || user != player || player.IsDead() ||
                    !BlueprintConstructionPayoutSeal.IsHeld(source) ||
                    !BlueprintConstructionPayoutCollection.WithinReach(player, source) ||
                    player.GetHoverObject()?.GetComponentInParent<ItemDrop>() != source || queued != Guid.Empty ||
                    PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished)
                    return false;
                var view = source.GetComponent<ZNetView>(); var data = view ? view.GetZDO() : null;
                if (!view || !view.IsValid() || !view.IsOwner() || data == null || !data.IsValid() || !data.Persistent || data.m_uid.IsNone() ||
                    data.GetLong(BlueprintConstructionPayoutSeal.OwnerKey, 0) != player.GetPlayerID() ||
                    !Guid.TryParseExact(data.GetString(BlueprintConstructionPayoutSeal.MarkerKey, ""), "N", out Guid marker) || marker == Guid.Empty)
                    return false;
                Guid project = Guid.Empty; int index = -1;
                var journal = new BlueprintConstructionPayoutJournal(store);
                // Match durable item identity after reload; an old native ZDO ID is not an entitlement.
                foreach (Guid id in store.Ids())
                {
                    var record = journal.Read(id);
                    if (record?.Phase != BlueprintConstructionPayoutJournal.IssuedSaved) continue;
                    for (int i = 0; i < record.Items.Length; ++i) if (record.Items[i].Id == marker)
                    {
                        if (index >= 0 || record.Player != player.GetPlayerID() || record.World != world) return false;
                        project = id; index = i;
                    }
                }
                if (index < 0) return false;
                StartCollection(project, index); return true;
            }
            catch (Exception error) { Failure = error.Message; log?.Invoke(error); return false; }
        }

        private void TickPayoutCollection()
        {
            bool collecting = collection != null && !collection.Finished;
            collection?.Tick();
            if (collecting && collection.Finished)
            {
                if (collection.Status == "profile-credited") { cleanupProjects.Add(collection.Project); cancellationProjects.Add(collection.Project); }
                else { cleanupProjects.Remove(collection.Project); Failure = collection.Reason; log?.Invoke(new InvalidOperationException(Failure)); }
            }
            bool cleaning = cleanup != null && !cleanup.Finished;
            cleanup?.Tick();
            if (cleaning && cleanup.Finished)
            {
                if (cleanup.Status == "cleanup-saved") { cleanupProjects.Add(cleanup.Project); cancellationProjects.Add(cleanup.Project); }
                else { cleanupProjects.Remove(cleanup.Project); Failure = cleanup.Reason; log?.Invoke(new InvalidOperationException(Failure)); }
            }
            if (!IsPrivateLocalPlacement || store == null || cleanupProjects.Count == 0 || queued != Guid.Empty || PayoutBusy ||
                execution != null && !execution.Finished || removal != null && !removal.Finished) return;
            Guid id = cleanupProjects.First(); cleanupProjects.Remove(id);
            try
            {
                var record = new BlueprintConstructionPayoutJournal(store).Read(id);
                if (record?.Phase != BlueprintConstructionPayoutJournal.IssuedSaved || record.Player != Player.m_localPlayer.GetPlayerID() ||
                    record.Collections == null || record.Collections.Any(c => c != null && c.Phase != BlueprintConstructionPayoutJournal.ProfileCredited)) return;
                int index = Array.FindIndex(record.Collections, c => c?.Removal?.Phase == BlueprintConstructionPayoutJournal.CleanupIntent);
                if (index < 0) index = Array.FindIndex(record.Collections, c => c != null && c.Removal == null);
                if (index < 0) return;
                // Constructor/Tick alone own the exact source, saved receipt and N/N+1 recovery checks.
                cleanup = new BlueprintConstructionPayoutCleanup(store, id, index);
            }
            catch (Exception error) { Failure = error.Message; log?.Invoke(error); }
        }

        internal void BeginCleanup(Guid id, int index)
        {
            RequireContext();
            if (!IsPrivateLocalPlacement || !OpenBoard || OpenBoard.Id != id || !HasHammer(Player.m_localPlayer) ||
                Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5 || queued != Guid.Empty ||
                PayoutBusy || execution != null && !execution.Finished || removal != null && !removal.Finished)
                throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            var next = new BlueprintConstructionPayoutCleanup(store, id, index);
            Close(); cleanup = next;
        }

        internal void CancelUnpaid(Guid id, long revision, Guid operation)
        {
            RequireContext();
            if (PayoutBusy) throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            // Commit the terminal record before removing any world presentation; cancellation cannot refund an unpaid plan.
            store.CancelUnpaid(id, revision, operation);
            RetireProject(id);
        }
        private void RetireProject(Guid id)
        {
            try { fog?.Remove(id); }
            catch (Exception error) { log?.Invoke(error); }
            if (boards.TryGetValue(id, out var board))
            {
                if (OpenBoard == board) Close();
                board.Retire(); boards.Remove(id);
            }
        }

        internal bool Open(BlueprintConstructionProjectBoard board, Humanoid user)
        {
            try
            {
                if (disposed || failed || store == null || !Allowed() || PayoutBusy || !board || !BuildWorksPlugin.CanOpenConstructionProject ||
                    user != Player.m_localPlayer || Player.m_localPlayer.IsDead() || !HasHammer(user) || Board(board.Id) != board ||
                    Vector3.Distance(user.transform.position, board.transform.position) > 5 || store.Load(board.Id).Cancelled) return false;
                OpenBoard = board; view.Tick(this, board); return true;
            }
            catch (Exception error) { Close(); log?.Invoke(error); return false; }
        }
        internal void Close() { OpenBoard = null; view.Close(); }
        internal static bool HasHammer(Humanoid user) => user is Player player && PrecisionPlacementSession.HasUsableHammer(player);
        private bool Allowed() => network && network == ZNet.instance && world != 0 && WorldId(network) == world &&
            network.IsServer() && network.GetPeers().Count == 0 && Player.m_localPlayer && Game.instance &&
            !Game.instance.IsShuttingDown() && network.LocalPlayerIsAdminOrHost();
        private void RequireContext()
        { if (disposed || failed || store == null || !Allowed()) throw new InvalidOperationException("Unpaid construction projects require the current isolated local-host context."); }
        private static long WorldId(ZNet net)
        { try { return net ? net.GetWorldUID() : 0; } catch (NullReferenceException) { return 0; } }
        private void AddBoard(Store.Record record)
        {
            if (record.Cancelled || boards.ContainsKey(record.Id)) return;
            var root = new GameObject("BuildWorks_Project_" + record.Id.ToString("N"));
            try
            {
                float[] pose = record.RootPose.Values;
                root.transform.position = new Vector3(pose[0], pose[1], pose[2]);
                root.transform.rotation = new Quaternion(pose[3], pose[4], pose[5], pose[6]);
                var board = root.AddComponent<BlueprintConstructionProjectBoard>();
                board.Initialize(this, record); boards.Add(record.Id, board);
            }
            catch { UnityEngine.Object.Destroy(root); throw; }
        }
        private void ClearBoards()
        {
            Close();
            if (!ReferenceEquals(fog, null) && fog.Close()) fog = null;
            fogExecution = null; fogExecutionStatus = null;
            foreach (var board in boards.Values) if (board) board.Retire();
            boards.Clear();
        }
        private void TickFog()
        {
            if (failed || store == null || boards.Count == 0) return;
            try
            {
                Camera camera = Camera.main;
                if (!ReferenceEquals(fog, null) && (!fog || fog.gameObject != (camera ? camera.gameObject : null)))
                { if (!fog.Close()) return; fog = null; }
                if (!camera) return;
                if (!fog) { fog = camera.gameObject.AddComponent<BlueprintConstructionProjectFog>(); fog.Initialize(this, log); }
                foreach (var board in boards.Values) if (board && !fog.Has(board.Id)) fog.Add(board.Record);
                if (execution != null && (fogExecution != execution || fogExecutionStatus != execution.Status))
                { fog.Refresh(execution.Project); fogExecution = execution; fogExecutionStatus = execution.Status; }
            }
            catch (Exception error) { log?.Invoke(error); }
        }
        internal void Suspend()
        {
#if BUILDWORKS_TEST_STAND
            QaImmediateFogReceipts = null;
#endif
            execution?.Refuse("Construction suspended; evidence retained."); removal?.Refuse("Construction suspended; removal evidence retained."); payout?.Refuse("Construction suspended; payout evidence retained."); collection?.Refuse("Construction suspended; credit evidence retained."); cleanup?.Refuse("Construction suspended; cleanup evidence retained."); paidCancellation?.Refuse("Construction suspended; cancellation evidence retained."); paidCancellation = null; cleanup = null; collection = null; payout = null; removal = null; execution = null; queued = Guid.Empty;
            cleanupProjects.Clear(); cancellationProjects.Clear(); ClearBoards(); store = null; network = null; world = 0; failed = false; Failure = null; }
        public void Dispose() { if (disposed) return; Suspend(); disposed = true; }
    }
}
