#if BUILDWORKS_TEST_STAND
using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using Store = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    // Transient native checks: no project writes, construction, grants or desktop input.
    internal static class BuildWorksTestProjectBoards
    {
        public static string Check(string id)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            var runtime = BuildWorksPlugin.ConstructionProjects;
            var board = runtime.Board(Guid.ParseExact(id, "N"));
            if (!board || runtime.BlocksInput || !BlueprintConstructionProjectRuntime.HasHammer(player) ||
                !BuildWorksPlugin.CanOpenConstructionProject || !GameCamera.instance)
                throw new InvalidOperationException("Ready board, idle UI and existing equipped hammer are required.");
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var before))
                throw new InvalidOperationException("Inventory is unavailable.");
            var view = GameCamera.instance.transform;
            Vector3 oldPlayer = player.transform.position, oldCamera = view.position;
            Quaternion oldFacing = player.transform.rotation, oldCameraFacing = view.rotation;
            GameObject obstacle = null; int checks = 0;
            var hover = AccessTools.Field(typeof(Player), "m_hovering");
            object oldHover = hover.GetValue(player);
            var update = AccessTools.Method(typeof(Player), "UpdateHover");
            try
            {
                var collider = board.GetComponent<BoxCollider>();
                Check(collider && collider.enabled, "Board collider is disabled.", ref checks);
                Check(Quaternion.Angle(board.transform.rotation, Quaternion.Euler(0, 180, 0)) < .001f,
                    "Board is tilted or its written side faces the building.", ref checks);
                Check(ZoneSystem.instance.GetGroundHeight(board.transform.position, out float ground) &&
                    Mathf.Abs(board.transform.position.y - ground) < .002f, "Board is floating.", ref checks);
                Check(!board.GetComponentsInChildren<ZNetView>(true).Any() && !board.GetComponentsInChildren<Piece>(true).Any() &&
                    !board.GetComponentsInChildren<Sign>(true).Any(), "Board cloned native gameplay components.", ref checks);
                Check(board.GetComponentsInChildren<MeshRenderer>(true).Any(renderer => renderer.sharedMaterial &&
                    renderer.sharedMaterial.mainTexture), "Board has no textured native renderer.", ref checks);
                var glyphs = board.GetComponentsInChildren<MeshFilter>(true).Where(filter => filter.name.StartsWith("Glyphs_", StringComparison.Ordinal)).ToArray();
                Check(glyphs.Length > 0 && glyphs.All(filter => filter.GetComponent<Renderer>().enabled &&
                    filter.transform.TransformDirection(filter.sharedMesh.normals[0]).z < -.99f &&
                    filter.GetComponent<Renderer>().bounds.center.z < board.transform.position.z),
                    "Written side does not face away from the house.", ref checks);
                Check(glyphs.All(filter => filter.sharedMesh.colors32.All(colour => colour.r == 255 && colour.g == 255 && colour.b == 255 && colour.a == 255)),
                    "Project label inherited the unreadable dark native vertex tint.", ref checks);
                Vector3 target = collider.bounds.center;
                player.transform.position = board.transform.position + new Vector3(0, 0, -2);
                view.position = player.transform.position + Vector3.up * 1.5f; view.LookAt(target);
                Physics.SyncTransforms(); hover.SetValue(player, null); update.Invoke(player, null);
                Check(player.GetHoverObject()?.GetComponentInParent<BlueprintConstructionProjectBoard>() == board,
                    "Native hammer hover did not select the board.", ref checks);
                // Exercise native Interact dispatch after patched hover; this is not a physical E-key assertion.
                AccessTools.Method(typeof(Player), "Interact", new[] { typeof(GameObject), typeof(bool), typeof(bool) })
                    .Invoke(player, new object[] { player.GetHoverObject(), false, false });
                Check(runtime.OpenBoard == board, "Native interaction did not open the board.", ref checks);
                runtime.Close();
                obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube); obstacle.name = "BuildWorks_QA_BoardOccluder";
                obstacle.layer = LayerMask.NameToLayer("piece"); obstacle.transform.position = Vector3.Lerp(view.position, target, .5f);
                obstacle.transform.localScale = Vector3.one * .5f; Physics.SyncTransforms();
                hover.SetValue(player, null); update.Invoke(player, null);
                Check(!player.GetHoverObject(), "Hammer board hover bypassed a native obstacle.", ref checks);
                UnityEngine.Object.DestroyImmediate(obstacle); obstacle = null;
                player.transform.position += Vector3.back * 20;
                view.position += Vector3.back * 20; view.LookAt(target); Physics.SyncTransforms();
                hover.SetValue(player, null); update.Invoke(player, null);
                Check(!player.GetHoverObject(), "Hammer board hover bypassed native reach.", ref checks);
                CheckFootprints(ref checks);
            }
            finally
            {
                if (obstacle) UnityEngine.Object.DestroyImmediate(obstacle);
                runtime.Close(); player.transform.SetPositionAndRotation(oldPlayer, oldFacing);
                view.SetPositionAndRotation(oldCamera, oldCameraFacing); hover.SetValue(player, oldHover); Physics.SyncTransforms();
            }
            if (!BlueprintStorageCapture.TrySnapshotInventory(player.GetInventory(), out var after) || before.Info != after.Info || before.Zdo != after.Zdo)
                throw new InvalidOperationException("Board checks changed inventory.");
            return "passed=" + checks + ";inventoryUnchanged=true;nativeHover=true;nativeInteract=true;physicalEKey=false";
        }

        private static void CheckFootprints(ref int checks)
        {
            GameObject prefab = ZNetScene.instance.GetPrefab("woodwall");
            if (!prefab) throw new InvalidOperationException("Native footprint prefab is missing.");
            foreach (Vector3 angles in new[] { Vector3.zero, new Vector3(0, 90, 0), new Vector3(23, 37, 64) })
            foreach (Vector3 scale in new[] { Vector3.one, new Vector3(2, .5f, 3) })
            {
                var position = new Vector3(3000, 60, 200); var rotation = Quaternion.Euler(angles);
                GameObject visual = PlacementGhostPreviewView.CreateVisualClone(prefab, "QA_Footprint", prefab.layer);
                try
                {
                    visual.transform.SetPositionAndRotation(position, rotation);
                    visual.transform.localScale = Vector3.Scale(prefab.transform.localScale, scale);
                    Physics.SyncTransforms();
                    var renderers = visual.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
                    Bounds envelope = renderers[0].bounds;
                    foreach (var renderer in renderers.Skip(1)) envelope.Encapsulate(renderer.bounds);
                    var blueprint = new CompositeBlueprintStore.Blueprint { id = Guid.NewGuid().ToString("N"), name = "Transient board footprint", prefabName = "woodwall", primaryPartId = "a" };
                    blueprint.parts.Add(new CompositeBlueprintStore.Part { stableId = "a", prefabName = "woodwall", displayName = "QA wall",
                        rotation = new CompositeBlueprintStore.QuaternionData { w = 1 } });
                    var pose = new Store.Pose(new[] { position.x, position.y, position.z, rotation.x, rotation.y, rotation.z, rotation.w, scale.x, scale.y, scale.z });
                    var project = new Store.Record(Guid.NewGuid(), 1, 1, Guid.Empty, pose, BlueprintExchangeFormat.Encode(blueprint),
                        new[] { new Store.Part(Guid.NewGuid(), 0, pose) });
                    Vector3 point = BlueprintConstructionProjectBoard.GroundPoint(project);
                    // Stored local envelopes include all LODs and conservatively grow when rotated.
                    // Require clearance from the real LOD0 geometry, rather than identical tight AABBs.
                    Check(point.z <= envelope.min.z - .998f && point.x >= envelope.min.x - .002f &&
                        point.x <= envelope.max.x + .002f && point.y <= envelope.min.y + .002f,
                        "Board is not outside the transformed native renderer footprint.", ref checks);
                    blueprint.parts[0].metadata = BlueprintPrefabMetadataCapture.Capture(prefab);
                    blueprint.parts[0].prefabName = "BuildWorks_QA_BoardAbsentPrefab";
                    blueprint.parts[0].metadata.prefabId = blueprint.parts[0].prefabName;
                    project = new Store.Record(Guid.NewGuid(), 1, 1, Guid.Empty, pose, BlueprintExchangeFormat.Encode(blueprint), project.Parts.ToArray());
                    Check(Vector3.Distance(point, BlueprintConstructionProjectBoard.GroundPoint(project)) < .002f,
                        "Stored missing-prefab bounds changed the footprint.", ref checks);
                }
                finally { UnityEngine.Object.DestroyImmediate(visual); }
            }
        }
        private static void Check(bool condition, string message, ref int checks)
        { if (!condition) throw new InvalidOperationException(message); ++checks; }
    }
}
#endif
