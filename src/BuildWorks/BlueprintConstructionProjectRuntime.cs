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
        private Guid queued;
        private int queuedFrame;
        private bool recoveryOnly;
        private BlueprintConstructionProjectFog fog;
        private BlueprintConstructionPartExecution fogExecution;
        private string fogExecutionStatus;
#if BUILDWORKS_TEST_STAND
        internal static bool QaFailNextBoard;
#endif
        internal BlueprintConstructionProjectFog Fog => fog;
        internal string FogError(Guid id) => fog ? fog.Error(id) : null;
        internal BlueprintConstructionPartExecution Execution => execution;
        internal BlueprintConstructionCancellationRemoval Removal => removal;
        internal BlueprintConstructionPayoutIssuance Payout => payout;
        internal int BoardCount => boards.Count;
        internal long World => world;
        internal string Failure { get; private set; }
        internal BlueprintConstructionProjectBoard OpenBoard { get; private set; }
        private bool PayoutBusy => payout != null && !payout.Finished;
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
            if (!Allowed()) { execution?.Refuse("Construction context closed; evidence retained."); removal?.Refuse("Construction context closed; removal evidence retained."); payout?.Refuse("Construction context closed; payout evidence retained."); execution = null; queued = Guid.Empty; ClearBoards(); store = null; return; }
            removal?.Tick();
            payout?.Tick();
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

        // Internal operator path until the durable restitution protocol can complete paid cancellation.
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
            Close(); removal = new BlueprintConstructionCancellationRemoval(store, id, revision, operation);
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

        internal void CancelUnpaid(Guid id, long revision, Guid operation)
        {
            RequireContext();
            if (PayoutBusy) throw new InvalidOperationException(BuildWorksLocalization.Text("project.unavailable"));
            // Commit the terminal record before removing any world presentation; cancellation cannot refund an unpaid plan.
            store.CancelUnpaid(id, revision, operation);
            try { fog?.Remove(id); }
            catch (Exception error) { log?.Invoke(error); }
            if (boards.TryGetValue(id, out var board))
            {
                if (OpenBoard == board) OpenBoard = null;
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
        { execution?.Refuse("Construction suspended; evidence retained."); removal?.Refuse("Construction suspended; removal evidence retained."); payout?.Refuse("Construction suspended; payout evidence retained."); payout = null; removal = null; execution = null; queued = Guid.Empty;
            ClearBoards(); store = null; network = null; world = 0; failed = false; Failure = null; }
        public void Dispose() { if (disposed) return; Suspend(); disposed = true; }
    }
}
