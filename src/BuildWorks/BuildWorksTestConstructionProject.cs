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
            Quaternion rotation = Quaternion.Euler(0, 37, 0);
            Vector3 origin = player.transform.position + rotation * (Vector3.forward * 3 - Vector3.right * 3);
            var wall = ZNetScene.instance.GetPrefab("woodwall");
            if (!wall || !BuildWorksPlacementValidation.TryGetBounds(wall, out var wallBounds)) throw new InvalidOperationException("QA wall bounds are missing.");
            var heights = new float[4];
            for (int i = 0; i < heights.Length; ++i)
            {
                Vector3 point = origin + rotation * new Vector3(i * 2, 0, 0);
                if (!ZoneSystem.instance.IsZoneLoaded(point) || !ZoneSystem.instance.GetGroundHeight(point, out heights[i]))
                    throw new InvalidOperationException("QA wall ground is not loaded; terrain will not be changed.");
            }
            if (heights.Max() - heights.Min() > .015f) throw new InvalidOperationException("QA wall requires an already flat site; no terrain preparation will run.");
            origin.y = heights.Max() - (wallBounds.min.y - wall.transform.position.y);
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
        public static string Continue()
        { BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle(); Runtime.Continue(lastId); return "queued-native-paid-construction"; }
        public static string RecoverKnownObject(string part, string operation)
        {
            BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            Runtime.RecoverKnownObject(lastId, Guid.ParseExact(part, "N"), Guid.ParseExact(operation, "N"));
            return "verifying-existing-debit-and-object";
        }
        public static bool SnapshotVersionAndProvenance()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var inventory) ||
                !OstrixMods.BuildWorks.Geometry.BlueprintContainerInventory.TryRead(inventory, name => name.GetStableHashCode(), out var originals)) return false;
            foreach (var item in player.GetInventory().GetAllItems())
            {
                var data = new OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData(OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeItemStandFormat,
                    item.m_dropPrefab.name + ":" + item.m_variant + ":" + item.m_quality + ":0", BlueprintItemStandPlacement.SnapshotItem(item), null);
                if (!OstrixMods.BuildWorks.Geometry.BlueprintItemStandContents.TryRead(data, name => name.GetStableHashCode(), out var parsed) || parsed.SourceVersion != 110 ||
                    parsed.Cheated != originals.Single(original => original.X == item.m_gridPos.x && original.Y == item.m_gridPos.y).Cheated) return false;
            }
            return true;
        }
        public static bool ScopeGuards()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            Piece Prefab(string name) => ZNetScene.instance.GetPrefab(name)?.GetComponent<Piece>() ?? throw new InvalidOperationException("QA prefab missing: " + name);
            return PrecisionPlacementSession.SupportsProjectPart(Prefab("woodwall")) && PrecisionPlacementSession.SupportsProjectPart(Prefab("wood_floor")) &&
                !PrecisionPlacementSession.SupportsProjectPart(Prefab("fire_pit")) && !PrecisionPlacementSession.SupportsProjectPart(Prefab("piece_chest_wood")) &&
                !PrecisionPlacementSession.SupportsProjectPart(Prefab("itemstand"));
        }
        public static object PaidProgress()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession(); var runtime = Runtime; var record = runtime.Read(lastId);
            var progress = runtime.Progress(lastId); var operation = runtime.Execution;
            var parts = record.Parts.Select(part => {
                var receipt = runtime.ReadPart(lastId, part.Id);
                var instance = receipt == null ? null : BlueprintConstructionPartExecution.FindMarked(receipt.Operation, record.Blueprint.parts[part.SourceIndex].prefabName);
                float[] pose = part.WorldPose.Values;
                return new PaidPart { part = part.Id.ToString("N"), phase = receipt?.Phase ?? 0, operation = receipt?.Operation.ToString("N"),
                    present = instance, exactPose = instance && Vector3.Distance(instance.transform.position, new Vector3(pose[0], pose[1], pose[2])) <= .001f &&
                        Quaternion.Angle(instance.transform.rotation, new Quaternion(pose[3], pose[4], pose[5], pose[6])) <= .01f &&
                        Vector3.Distance(instance.transform.localScale, new Vector3(pose[7], pose[8], pose[9])) <= .001f,
                    paid = receipt?.Paid?.Sum(row => row.Paid) ?? 0,
                    receiptMatches = instance && receipt?.Receipt != null && instance.GetComponent<ZNetView>().GetZDO()
                        .GetByteArray(BlueprintConstructionPartExecution.ReceiptKey)?.SequenceEqual(receipt.Receipt) == true };
            }).ToArray();
            return new PaidReceipt { built = progress.Built, total = progress.Total, needsReview = progress.NeedsReview,
                status = operation?.Status, reason = runtime.Failure ?? operation?.Reason, blocked = runtime.BlocksInput,
                wood = player.GetInventory().CountItems("$item_wood", -1, true), parts = parts };
        }
        private static BlueprintConstructionProjectRuntime Runtime => BuildWorksPlugin.QaConstructionProjects ?? throw new InvalidOperationException("Project runtime is absent.");
        private static Store.Pose Pose(Vector3 p, Quaternion q) => new Store.Pose(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, 1, 1, 1 });
        [Serializable] private sealed class Receipt
        {
            public string id, snapshotSha256;
            public long world, revision;
            public int boards, boardColliders;
            public bool exactSnapshotParts, cancelled, boardPresent, open, blocked, hasNativePiece, hasNetworkView;
        }
        [Serializable] private sealed class PaidPart { public string part, operation; public int phase, paid; public bool present, exactPose, receiptMatches; }
        [Serializable] private sealed class PaidReceipt { public int built, total, wood; public bool needsReview, blocked; public string status, reason; public PaidPart[] parts; }
    }
}
#endif
