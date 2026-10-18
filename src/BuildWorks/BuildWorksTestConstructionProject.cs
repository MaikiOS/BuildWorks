#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal static class BuildWorksTestConstructionProject
    {
        private static Guid lastId, cancelOperation;
        private static byte[] snapshot;
        private static Guid[] partIds;
        private static Store.Pose[] poses;
        public static string LastId => lastId.ToString("N");
        public static string Adopt(string id)
        {
            BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            var record = Runtime.Read(Guid.ParseExact(id, "N"));
            if (record.Cancelled || record.Blueprint.name != "QA unpaid construction" || record.Parts.Count != 4)
                throw new InvalidOperationException("Only the exact existing unpaid QA fixture can be adopted.");
            lastId = record.Id; cancelOperation = Guid.NewGuid(); snapshot = record.BlueprintBytes;
            partIds = record.Parts.Select(p => p.Id).ToArray(); poses = record.Parts.Select(p => p.WorldPose).ToArray(); return Inspect();
        }
        public static string Create()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            if (lastId != Guid.Empty && !Runtime.Read(lastId).Cancelled) throw new InvalidOperationException("Previous unpaid fixture is still active.");
            Vector3 origin = player.transform.position + player.transform.right * 2;
            Quaternion rotation = Quaternion.Euler(0, 37, 0);
            var blueprint = new CompositeBlueprintStore.Blueprint {
                id = Guid.NewGuid().ToString("N"), name = "QA unpaid construction", category = "QA", prefabName = "BuildWorks_QA_Unpaid", primaryPartId = "wall-0"
            };
            for (int i = 0; i < 4; ++i) blueprint.parts.Add(new CompositeBlueprintStore.Part {
                prefabName = "woodwall", displayName = "QA wall", stableId = "wall-" + i,
                position = new CompositeBlueprintStore.VectorData(new Vector3(i * 2, 0, 0)),
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                scale = new CompositeBlueprintStore.VectorData(Vector3.one), baseScale = new CompositeBlueprintStore.VectorData(Vector3.one), editorVisible = true
            });
            poses = blueprint.parts.Select(p => Pose(origin + rotation * p.position.ToVector3(), rotation)).ToArray();
            Guid id = Guid.NewGuid(); var record = Runtime.Create(id, blueprint, Pose(origin, rotation), poses);
            lastId = id; cancelOperation = Guid.NewGuid(); snapshot = record.BlueprintBytes; partIds = record.Parts.Select(p => p.Id).ToArray();
            return Inspect();
        }
        public static string Inspect()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var runtime = Runtime; var record = runtime.Read(lastId); var board = runtime.Board(lastId);
            bool same = snapshot.SequenceEqual(record.BlueprintBytes) && partIds.SequenceEqual(record.Parts.Select(p => p.Id)) &&
                poses.SelectMany(p => p.Values).SequenceEqual(record.Parts.SelectMany(p => p.WorldPose.Values));
            using (var sha = SHA256.Create()) return JsonUtility.ToJson(new Receipt {
                id = lastId.ToString("N"), exactSnapshotParts = same, cancelled = record.Cancelled, revision = record.Revision,
                world = record.World, boards = runtime.BoardCount, boardPresent = board, open = runtime.OpenBoard == board && board,
                blocked = runtime.BlocksInput, hasNativePiece = board && board.GetComponent<Piece>(), hasNetworkView = board && board.GetComponent<ZNetView>(),
                boardColliders = board ? board.GetComponentsInChildren<Collider>().Count(c => c.enabled) : 0,
                snapshotSha256 = BitConverter.ToString(sha.ComputeHash(record.BlueprintBytes)).Replace("-", "")
            });
        }
        public static bool Open()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            return Runtime.Board(lastId).Interact(player, false, false);
        }
        public static bool EquipExistingHammer()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            var hammer = player.GetInventory().GetAllItems().FirstOrDefault(i => i.m_shared?.m_buildPieces &&
                i.m_shared.m_buildPieces.m_canRemovePieces && (!i.m_shared.m_useDurability || i.m_durability > 0));
            if (hammer == null) throw new InvalidOperationException("QA inventory has no existing usable hammer; no item will be granted.");
            return player.EquipItem(hammer, false);
        }
        public static string Close() { BuildWorksTestIsolation.RequireLoadedSession(); Runtime.Close(); return "closed-project-kept"; }
        public static string RestoreRuntime()
        { BuildWorksTestIsolation.RequireLoadedSession(); Runtime.Suspend(); Runtime.Tick(); return Inspect(); }
        public static bool RejectStaleInteraction()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession(); var board = Runtime.Board(lastId);
            Runtime.Suspend(); bool rejected = !Runtime.Open(board, player); Runtime.Tick(); return rejected;
        }
        public static string Cancel()
        { BuildWorksTestIsolation.RequireLoadedSession(); Runtime.CancelUnpaid(lastId, 1, cancelOperation); return Inspect(); }
        private static BlueprintConstructionProjectRuntime Runtime => BuildWorksPlugin.QaConstructionProjects ?? throw new InvalidOperationException("Project runtime is absent.");
        private static Store.Pose Pose(Vector3 p, Quaternion q) => new Store.Pose(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, 1, 1, 1 });
        [Serializable] private sealed class Receipt
        {
            public string id, snapshotSha256;
            public long world, revision;
            public int boards, boardColliders;
            public bool exactSnapshotParts, cancelled, boardPresent, open, blocked, hasNativePiece, hasNetworkView;
        }
    }
}
#endif
