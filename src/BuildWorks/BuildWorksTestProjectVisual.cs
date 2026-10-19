#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    // Real unpaid project; the existing QA route/recorder borrows its visual without owning another renderer.
    internal static class BuildWorksTestProjectVisual
    {
        private static Store.Record record;
        private static BuildWorksTestFog probe;
        private static Vector3 centre, start;
        private static Bounds envelope;
        private static OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData inventory;
        private static BlueprintConstructionProjectRuntime Runtime => BuildWorksPlugin.QaConstructionProjects;
        public static bool Ready => record != null && Runtime.Fog?.Building(record.Id)?.Ready == true && Runtime.FogError(record.Id) == null;
        public static string Create(string kind)
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.QaPlacementSession.QaClosePassiveConfirm(); BuildWorksPlugin.RequireQaStorageIdle();
            if (record != null || probe || kind != "wall" && kind != "house") throw new InvalidOperationException("Close the previous project visual fixture first; choose wall or house.");
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out inventory)) throw new InvalidOperationException("QA inventory unavailable.");
            var blueprint = kind == "house" ? BuildWorksPlugin.QaBlueprint("645bf7b76d9d419b8c21c5dad4fd31ab") : Wall();
            if (blueprint.parts.Count != (kind == "house" ? 28 : 4)) throw new InvalidOperationException("Unexpected existing fixture composition.");
            bool first = true; envelope = default;
            foreach (var part in blueprint.parts)
            {
                var prefab = ZNetScene.instance.GetPrefab(part.prefabName);
                if (!prefab || !BlueprintPrefabMetadataCapture.TryGetLocalVisualBounds(prefab, out var bounds, true))
                    throw new InvalidOperationException("Real prefab visual bounds missing: " + part.prefabName);
                Matrix4x4 matrix = Matrix4x4.TRS(part.position.ToVector3(), part.rotation.ToQuaternion(), part.RootScale(prefab.transform.localScale));
                for (int corner = 0; corner < 8; ++corner)
                {
                    Vector3 point = matrix.MultiplyPoint3x4(new Vector3((corner & 1) == 0 ? bounds.min.x : bounds.max.x,
                        (corner & 2) == 0 ? bounds.min.y : bounds.max.y, (corner & 4) == 0 ? bounds.min.z : bounds.max.z));
                    if (first) { envelope = new Bounds(point, Vector3.zero); first = false; } else envelope.Encapsulate(point);
                }
            }
            if (envelope.size.magnitude > 25) throw new InvalidOperationException("Fixture exceeds the bounded loaded QA site.");
            start = player.transform.position; Vector3 origin = default; bool found = false;
            foreach (Vector3 offset in new[] { new Vector3(15, 0, -15), new Vector3(-15, 0, -15), new Vector3(15, 0, 15), new Vector3(-15, 0, 15) })
            {
                Vector3 target = start + offset; float high = float.MinValue, low = float.MaxValue; bool loaded = true;
                for (int corner = 0; corner < 4; ++corner)
                {
                    Vector3 point = target + new Vector3((corner & 1) == 0 ? -envelope.extents.x : envelope.extents.x, 0,
                        (corner & 2) == 0 ? -envelope.extents.z : envelope.extents.z);
                    if (!ZoneSystem.instance.IsZoneLoaded(point) || !ZoneSystem.instance.GetGroundHeight(point, out float height)) { loaded = false; break; }
                    high = Mathf.Max(high, height); low = Mathf.Min(low, height);
                }
                if (!loaded || high - low > .015f) continue;
                origin = new Vector3(target.x - envelope.center.x, high - envelope.min.y, target.z - envelope.center.z);
                centre = origin + envelope.center;
                if (Physics.OverlapBox(centre, envelope.extents + new Vector3(1, .05f, 1), Quaternion.identity, LayerMask.GetMask("piece", "piece_nonsolid")).Length != 0) continue;
                found = true; break;
            }
            if (!found) throw new InvalidOperationException("No already flat, loaded and unoccupied QA site; terrain/old structures will not change.");
            var poses = blueprint.parts.Select(part => Pose(origin + part.position.ToVector3(), part.rotation.ToQuaternion(),
                part.PlacementScale(ZNetScene.instance.GetPrefab(part.prefabName).transform.localScale))).ToArray();
            record = Runtime.Create(Guid.NewGuid(), blueprint, Pose(origin, Quaternion.identity, Vector3.one), poses);
            return Status();
        }
        public static string Attach()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            if (!Ready || BuildWorksTestFog.CurrentProbe) throw new InvalidOperationException("A ready real project and no existing QA fog probe are required.");
            probe = new GameObject("BuildWorks_QA_ProjectRoute").AddComponent<BuildWorksTestFog>();
            try { probe.AttachProject(Runtime, record.Id, centre, new Vector2(envelope.extents.x + 2, envelope.extents.z + 2)); }
            catch { UnityEngine.Object.DestroyImmediate(probe.gameObject); probe = null; throw; }
            return Status();
        }
        public static string Status()
        {
            BuildWorksTestIsolation.RequireLoadedSession(); var fog = Runtime.Fog; var building = record == null ? null : fog?.Building(record.Id);
            var parts = building == null ? null : (BlueprintFogVisual[])AccessTools.Field(typeof(BlueprintFogBuilding), "indexedParts").GetValue(building);
            bool exactInventory = BlueprintStorageCapture.TrySnapshotInventory(Player.m_localPlayer.GetInventory(), out var current) &&
                inventory != null && inventory.Info == current.Info && inventory.Zdo == current.Zdo;
            return JsonUtility.ToJson(new Receipt { id = record?.Id.ToString("N"), ready = Ready, attached = probe, parts = record?.Parts.Count ?? 0,
                visible = building?.VisibleCount ?? 0, board = record != null && Runtime.Board(record.Id), blocked = Runtime.BlocksInput,
                failure = record == null ? null : fog?.Error(record.Id), inventoryUnchanged = exactInventory,
                nonphysical = parts != null && parts.All(part => part?.Root && !part.Root.GetComponentsInChildren<Collider>(true).Any(c => c.enabled) && part.Root.GetComponentsInChildren<ZNetView>(true).Length == 0),
                centre = centre, start = start, camera = centre + new Vector3(9, 3.3f, -11),
                fieldBytes = fog?.FieldBytes ?? 0, renderBytes = fog?.RenderBytes ?? 0, frames = fog?.RenderFrames ?? 0,
                clock = building?.Clock ?? 0, maxQueue = parts?.Max(part => part?.LogicalSource?.Inputs.Count ?? 0) ?? 0 });
        }
        public static string Close()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            if (probe) { if (probe.PassRunning) throw new InvalidOperationException("Stop/complete the route before fixture cleanup."); UnityEngine.Object.DestroyImmediate(probe.gameObject); probe = null; }
            if (record != null) { Runtime.CancelUnpaid(record.Id, record.Revision, Guid.NewGuid()); record = null; }
            return Status();
        }
        private static CompositeBlueprintStore.Blueprint Wall()
        {
            var result = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "QA233 unpaid four-section wall", category = "QA", prefabName = "woodwall", primaryPartId = "wall-0" };
            for (int i = 0; i < 4; ++i) result.parts.Add(new CompositeBlueprintStore.Part { prefabName = "woodwall", displayName = "QA wall", stableId = "wall-" + i,
                position = new CompositeBlueprintStore.VectorData(new Vector3(i * 2, 0, 0)), rotation = new CompositeBlueprintStore.QuaternionData(Quaternion.identity),
                scale = new CompositeBlueprintStore.VectorData(Vector3.one), baseScale = new CompositeBlueprintStore.VectorData(Vector3.one), editorVisible = true });
            return result;
        }
        private static Store.Pose Pose(Vector3 p, Quaternion q, Vector3 s) => new Store.Pose(new[] { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z });
        [Serializable] private sealed class Receipt
        {
            public string id, failure; public bool ready, attached, board, blocked, inventoryUnchanged, nonphysical;
            public int parts, visible, frames, maxQueue; public float clock;
            public long fieldBytes, renderBytes; public Vector3 centre, start, camera;
        }
    }
}
#endif
