#if BUILDWORKS_TEST_STAND
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using HarmonyLib;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Data = OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData;

namespace OstrixMods.BuildWorks
{
    // Synthetic journals live only in a new temporary folder; none of these methods place or pay for native pieces.
    internal static class BuildWorksTestProjectFog
    {
        private static BlueprintConstructionProjectRuntime fixture;
        private static Store plans;
        private static Store.Record[] records;
        private static Guid operation;
        private static string directory;
        private static Data inventory;
        private static string lastError;
        private static bool neighbourPreserved = true;
        public static bool Ready => records != null && records.All(record => fixture?.Fog?.Building(record.Id)?.Ready == true);
        public static string Start()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession(); BuildWorksPlugin.RequireQaStorageIdle();
            if (fixture != null) throw new InvalidOperationException("Previous temporary project fixture is still active.");
            if ((BuildWorksPlugin.QaConstructionProjects?.Fog?.FieldBytes ?? 0) != 0 || (BuildWorksPlugin.QaConstructionProjects?.Fog?.RenderBytes ?? 0) != 0)
                throw new InvalidOperationException("Production fog already owns GPU resources; temporary fixture cannot allocate a second pool.");
            directory = Path.Combine(Path.GetTempPath(), "BuildWorks.ProjectFog." + Guid.NewGuid().ToString("N"));
            fixture = new BlueprintConstructionProjectRuntime(directory, error => lastError = error.Message);
            fixture.Tick();
            plans = new Store(Path.Combine(directory, "BuildWorks", "server", "world_" + fixture.World, "projects"), fixture.World);
            Vector3 origin = player.transform.position + Vector3.forward * 6;
            Quaternion rotation = Quaternion.Euler(0, 37, 0);
            records = new Store.Record[2];
            for (int project = 0; project < 2; ++project)
            {
                var blueprint = Blueprint(2);
                var poses = blueprint.parts.Select((part, index) => Pose(origin + rotation * Vector3.right * ((project * 2 + index) * 2), rotation,
                    project == 1 && index == 1 ? new Vector3(.75f, 1.15f, 1) : Vector3.one)).ToArray();
                records[project] = fixture.Create(Guid.NewGuid(), blueprint, Pose(origin, rotation), poses);
            }
            return Status();
        }
        public static string Status()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            var fog = fixture?.Fog;
            return JsonUtility.ToJson(new Receipt {
                syntheticJournalFixture = true, temporaryDirectory = directory, error = lastError,
                active = fixture != null, boards = fixture?.BoardCount ?? 0, blocked = fixture?.BlocksInput ?? false,
                visible = fog?.VisibleParts ?? 0, sources = fog?.ActiveSources ?? 0, frames = fog?.RenderFrames ?? 0,
                fieldBytes = fog?.FieldBytes ?? 0, renderBytes = fog?.RenderBytes ?? 0,
                assets = fog?.Assets ?? 0, kernels = fog?.Kernels ?? 0, blockCapacity = fog?.BlockCapacity ?? 0,
                residentCapacity = fog?.ResidentCapacity ?? 0, neighbourPreserved = neighbourPreserved,
                ready = records != null && records.All(record => fog?.Building(record.Id)?.Ready == true),
                errors = records?.Select(record => fixture.FogError(record.Id) ?? "").ToArray() ?? Array.Empty<string>(),
                exactPoses = records != null && records.All(record => ExactPoses(record, fog?.Building(record.Id)))
            });
        }
        public static string Intent()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out inventory)) throw new InvalidOperationException("Native inventory snapshot unavailable.");
            operation = Guid.NewGuid();
            new Journal(plans).Prepare(records[0].Id, records[0].Parts[0].Id, operation, player.GetPlayerID(), inventory, out _);
            Refresh(); return Status();
        }
        public static string NoEffect()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            new Journal(plans).RecordNoEffect(records[0].Id, records[0].Parts[0].Id, operation, inventory, freshObjectAbsent: true);
            Refresh(); return Status();
        }
        public static string Observe()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            new Journal(plans).Observe(records[0].Id, records[0].Parts[0].Id, operation, 31, 9,
                "woodwall".GetStableHashCode(), player.GetPlayerID(), inventory, true, Array.Empty<Journal.PaidRow>());
            Refresh(); return Status();
        }
        public static string Commit()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var journal = new Journal(plans);
            var row = journal.Read(records[0].Id, records[0].Parts[0].Id);
            journal.ConfirmSaved(records[0].Id, records[0].Parts[0].Id, operation, new Journal.SavePair {
                BeforeGeneration = 7, Generation = 8, ProfileHash = new string('A', 128),
                InventoryHash = Hash(Convert.FromBase64String(inventory.Zdo)), WorldPayloadHash = Hash(row.Receipt) });
            Refresh(); return Status();
        }
        public static string RejectCapacity()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            var blueprint = Blueprint(33); var pose = Pose(player.transform.position, Quaternion.identity);
            var record = fixture.Create(Guid.NewGuid(), blueprint, pose, Enumerable.Repeat(pose, 33).ToArray());
            if (fixture.FogError(record.Id) == null || fixture.Read(record.Id).Cancelled || !fixture.Board(record.Id))
                throw new InvalidOperationException("Rejected fog removed its durable plan or interaction board.");
            lastError = null;
            return Status();
        }
        public static string Reload()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); fixture.Suspend(); fixture.Tick(); return Status();
        }
        public static string Close()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); fixture?.Dispose(); fixture = null; records = null; plans = null;
            return Status();
        }
        private static bool ExactPoses(Store.Record record, BlueprintFogBuilding building)
        {
            if (building == null || !building.Ready) return false;
            var visuals = (BlueprintFogVisual[])AccessTools.Field(typeof(BlueprintFogBuilding), "indexedParts").GetValue(building);
            var pending = record.Parts.Where(part => new Journal(plans).Read(record.Id, part.Id)?.Phase != Journal.Committed).ToArray();
            var live = visuals.Where(visual => visual != null).ToArray();
            if (live.Length != pending.Length) return false;
            for (int i = 0; i < live.Length; ++i)
            {
                var p = pending[i].WorldPose.Values;
                var prefab = ZNetScene.instance.GetPrefab(record.Blueprint.parts[pending[i].SourceIndex].prefabName);
                if (Vector3.Distance(live[i].Root.transform.position, new Vector3(p[0], p[1], p[2])) > .001f ||
                    Quaternion.Angle(live[i].Root.transform.rotation, new Quaternion(p[3], p[4], p[5], p[6])) > .01f ||
                    !prefab || Vector3.Distance(live[i].Root.transform.localScale, Vector3.Scale(prefab.transform.localScale, new Vector3(p[7], p[8], p[9]))) > .001f) return false;
            }
            return true;
        }
        private static void Refresh()
        {
            var neighbour = fixture.Fog.Building(records[1].Id);
            var visuals = (BlueprintFogVisual[])AccessTools.Field(typeof(BlueprintFogBuilding), "indexedParts").GetValue(neighbour);
            var source = visuals[0].LogicalSource; var snapshot = visuals[0].Snapshot; float clock = visuals[0].Clock;
            fixture.Fog.Refresh(records[0].Id);
            neighbourPreserved &= ReferenceEquals(neighbour, fixture.Fog.Building(records[1].Id)) &&
                ReferenceEquals(source, visuals[0].LogicalSource) && ReferenceEquals(snapshot, visuals[0].Snapshot) && visuals[0].Clock == clock;
        }
        private static CompositeBlueprintStore.Blueprint Blueprint(int count)
        {
            var blueprint = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "QA synthetic project fog", category = "QA", prefabName = "BuildWorks_QA_SyntheticProject" };
            for (int i = 0; i < count; ++i) blueprint.parts.Add(new CompositeBlueprintStore.Part {
                stableId = "wall-" + i, prefabName = "woodwall", displayName = "QA synthetic wall",
                rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                scale = new CompositeBlueprintStore.VectorData(Vector3.one), baseScale = new CompositeBlueprintStore.VectorData(Vector3.one), editorVisible = true });
            blueprint.primaryPartId = "wall-0"; return blueprint;
        }
        private static Store.Pose Pose(Vector3 p, Quaternion q) => Pose(p, q, Vector3.one);
        private static Store.Pose Pose(Vector3 p, Quaternion q, Vector3 scale) => new Store.Pose(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, scale.x, scale.y, scale.z });
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        [Serializable] private sealed class Receipt
        {
            public bool syntheticJournalFixture, active, blocked, ready, exactPoses, neighbourPreserved;
            public string temporaryDirectory, error;
            public string[] errors;
            public int boards, visible, sources, frames, assets, kernels, blockCapacity, residentCapacity;
            public long fieldBytes, renderBytes;
        }
    }
}
#endif
