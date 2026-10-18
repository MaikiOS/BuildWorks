using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    // shortcut: unpaid local-host projects only; add authenticated remote/payment operations before enabling ordinary construction.
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
        internal int BoardCount => boards.Count;
        internal long World => world;
        internal string Failure { get; private set; }
        internal BlueprintConstructionProjectBoard OpenBoard { get; private set; }
        internal bool BlocksInput => view.IsOpen;

        internal BlueprintConstructionProjectRuntime(string configPath, Action<Exception> log)
        { root = Path.Combine(configPath ?? throw new ArgumentNullException(nameof(configPath)), "BuildWorks", "server"); this.log = log; }

        internal void Tick()
        {
            if (disposed) return;
            ZNet current = ZNet.instance; long uid = WorldId(current);
            if (network != current || uid != world) { Suspend(); network = current; world = uid; }
            if (!Allowed()) { ClearBoards(); store = null; return; }
            if (OpenBoard && (!Player.m_localPlayer || Player.m_localPlayer.IsDead() ||
                !HasHammer(Player.m_localPlayer) || Vector3.Distance(Player.m_localPlayer.transform.position, OpenBoard.transform.position) > 5)) Close();
            try { view.Tick(this, OpenBoard); }
            catch (Exception error) { Close(); log?.Invoke(error); }
            if (failed || store != null) return;
            try
            {
                store = new Store(Path.Combine(root, "world_" + world.ToString(CultureInfo.InvariantCulture), "projects"), world);
                foreach (Guid id in store.Ids())
                {
                    Store.Record record = store.Load(id);
                    if (!record.Cancelled) AddBoard(record);
                }
            }
            catch (Exception error) { ClearBoards(); store = null; failed = true; Failure = error.Message; log?.Invoke(error); }
        }

        internal Store.Record Create(Guid id, CompositeBlueprintStore.Blueprint blueprint, Store.Pose rootPose, IReadOnlyList<Store.Pose> poses)
        {
            RequireContext();
            Store.Record record = store.Create(id, blueprint, rootPose, poses);
            if (!boards.ContainsKey(id)) AddBoard(record);
            return record;
        }

        internal Store.Record Read(Guid id) { RequireContext(); return store.Load(id); }
        internal BlueprintConstructionProjectBoard Board(Guid id) => boards.TryGetValue(id, out var board) ? board : null;

        internal void CancelUnpaid(Guid id, long revision, Guid operation)
        {
            RequireContext();
            // Commit the terminal record before removing any world presentation; cancellation cannot refund an unpaid plan.
            store.CancelUnpaid(id, revision, operation);
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
                if (disposed || failed || store == null || !Allowed() || !board || !BuildWorksPlugin.CanOpenConstructionProject ||
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
            foreach (var board in boards.Values) if (board) board.Retire();
            boards.Clear();
        }
        internal void Suspend()
        { ClearBoards(); store = null; network = null; world = 0; failed = false; Failure = null; }
        public void Dispose() { if (disposed) return; Suspend(); disposed = true; }
    }
}
