#if BUILDWORKS_TEST_STAND
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Native free placement is confined to owned offline QA fixtures, never blueprint construction.
    internal static class BuildWorksTestStations
    {
        internal static float FlatGround(float[] heights)
        {
            if (heights == null || heights.Length != 9 || heights.Any(h => float.IsNaN(h) || float.IsInfinity(h)))
                throw new InvalidDataException("Invalid QA ground samples.");
            float maximum = heights.Max();
            if (maximum - heights.Min() > 0.12f)
                throw new InvalidDataException("QA station requires naturally flat ground.");
            return maximum;
        }

        internal static void RequireSaveAcknowledgement(uint before, uint after)
        {
            if (after != unchecked(before + 1)) throw new IOException("Native QA world save did not commit exactly once.");
        }

        private static void Encapsulate(ref Bounds bounds, Transform root, Transform child, Bounds local)
        {
            for (int corner = 0; corner < 8; ++corner)
                bounds.Encapsulate(root.InverseTransformPoint(child.TransformPoint(new Vector3(
                    (corner & 1) == 0 ? local.min.x : local.max.x,
                    (corner & 2) == 0 ? local.min.y : local.max.y,
                    (corner & 4) == 0 ? local.min.z : local.max.z))));
        }

        private static Bounds Footprint(Piece piece)
        {
            GameObject prefab = piece.gameObject;
            if (prefab.GetComponentsInChildren<TerrainModifier>(true).Length != 0 ||
                prefab.GetComponentsInChildren<TerrainOp>(true).Length != 0 ||
                prefab.GetComponentsInChildren<TerrainComp>(true).Length != 0)
                throw new InvalidDataException("Terrain-changing QA station refused: " + prefab.name);
            if (prefab.GetComponentsInChildren<MonoBehaviour>(true).Any(component =>
                !component || component.GetType().Assembly != typeof(Piece).Assembly))
                throw new InvalidDataException("Unreviewed station behaviour in QA prefab: " + prefab.name);
            GameObject visual = PlacementGhostPreviewView.CreateVisualClone(prefab, "BuildWorks_QA_Bounds", prefab.layer);
            Bounds bounds;
            try
            {
                if (!HammerBlueprintPieceRegistry.TryGetLocalVisualBounds(visual, out bounds))
                    throw new InvalidDataException("QA station has no measurable visual bounds: " + prefab.name);
            }
            finally { UnityEngine.Object.Destroy(visual); }
            int colliders = 0;
            foreach (Collider collider in prefab.GetComponentsInChildren<Collider>(true))
            {
                if (!collider.enabled || collider.isTrigger) continue;
                Bounds local;
                if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is MeshCollider mesh && mesh.sharedMesh) local = mesh.sharedMesh.bounds;
                else throw new InvalidDataException("Unreviewed QA station collider: " + collider.GetType().Name);
                Encapsulate(ref bounds, prefab.transform, collider.transform, local);
                colliders++;
            }
            Vector3 scale = prefab.transform.localScale;
            Vector3 center = Vector3.Scale(bounds.center, scale);
            Vector3 size = Vector3.Scale(bounds.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            if (colliders == 0 || new[] { center.x, center.y, center.z, size.x, size.y, size.z, scale.x, scale.y, scale.z }
                .Any(value => float.IsNaN(value) || float.IsInfinity(value)) ||
                size.x < 0.1f || size.y < 0.1f || size.z < 0.1f || size.x > 8 || size.y > 8 || size.z > 8 ||
                scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                throw new InvalidDataException("Invalid or excessive QA station footprint: " + prefab.name);
            return new Bounds(center, size);
        }

        private static bool Clear(Bounds bounds, int mask)
            => Physics.OverlapBox(bounds.center, bounds.extents + Vector3.one * 0.15f,
                Quaternion.identity, mask, QueryTriggerInteraction.Ignore).Length == 0;

        private static Vector3 FindSite(Player player, Bounds footprint, List<Bounds> planned, int mask, float range)
        {
            // ponytail: bounded nearby search; refuse unsuitable terrain rather than alter it.
            for (int radius = 6; radius <= 16; radius += 2)
                for (int direction = 0; direction < 16; ++direction)
                {
                    float angle = direction * Mathf.PI / 8;
                    Vector3 position = player.transform.position + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                    if (radius + footprint.extents.magnitude + 1 >= range) continue;
                    var heights = new float[9];
                    bool loaded = true;
                    for (int sample = 0; sample < 9; ++sample)
                    {
                        Vector3 point = position + footprint.center + new Vector3(
                            (sample % 3 - 1) * footprint.extents.x, 0, (sample / 3 - 1) * footprint.extents.z);
                        if (!ZoneSystem.instance.IsZoneLoaded(point) || !ZoneSystem.instance.GetGroundHeight(point, out heights[sample]))
                        { loaded = false; break; }
                    }
                    if (!loaded || heights.Any(h => float.IsNaN(h) || float.IsInfinity(h)) || heights.Max() - heights.Min() > 0.12f) continue;
                    position.y = FlatGround(heights) - footprint.min.y;
                    Bounds occupied = new Bounds(position + footprint.center, footprint.size);
                    occupied.Expand(0.5f);
                    if (planned.Any(other => other.Intersects(occupied)) || !Clear(occupied, mask)) continue;
                    planned.Add(occupied);
                    return position;
                }
            throw new InvalidOperationException("No clear loaded naturally flat site for QA stations near the player; no terrain was modified.");
        }

        private static Piece ResolveStation(Piece piece)
        {
            CraftingStation required = piece.m_craftingStation;
            Piece parent = required ? required.GetComponentInParent<Piece>(true) : null;
            string name = parent ? parent.name : required ? required.gameObject.name : null;
            GameObject prefab = string.IsNullOrEmpty(name) ? null : ZNetScene.instance.GetPrefab(name);
            Piece station = prefab ? prefab.GetComponent<Piece>() : null;
            CraftingStation actual = prefab ? prefab.GetComponent<CraftingStation>() : null;
            ZNetView view = prefab ? prefab.GetComponent<ZNetView>() : null;
            if (!required || string.IsNullOrEmpty(required.m_name) || !station || !actual || !view ||
                prefab.name != name || actual.m_name != required.m_name)
                throw new InvalidDataException($"QA station resolution refused: piece={piece.name}, requirement={required?.name}/{required?.m_name}, " +
                    $"parent={parent?.name}, canonical={prefab?.name}, Piece={(bool)station}, CraftingStation={(bool)actual}/{actual?.m_name}, ZNetView={(bool)view}.");
            return station;
        }

        internal static IEnumerator Prepare()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksTestSupplies.Plan(out List<Piece> pieces);
            var prefabs = new SortedDictionary<string, Piece>(StringComparer.Ordinal);
            foreach (Piece piece in pieces)
            {
                if (!piece.m_craftingStation) continue;
                Piece station = ResolveStation(piece);
                prefabs[station.name] = station;
            }
            if (prefabs.Count == 0 || prefabs.Count > 4) throw new InvalidDataException("Unexpected QA station set.");
            if (prefabs.Values.Any(piece => float.IsNaN(piece.GetComponent<CraftingStation>().m_rangeBuild) ||
                float.IsInfinity(piece.GetComponent<CraftingStation>().m_rangeBuild) || piece.GetComponent<CraftingStation>().m_rangeBuild <= 0))
                throw new InvalidDataException("Invalid native QA station build range.");
            var bounds = prefabs.Values.Select(Footprint).ToArray();
            string description = "stations-v1\n" + string.Join("\n", prefabs.Keys.Select((name, index) => name + "|" +
                string.Join("|", new[] { bounds[index].center.x, bounds[index].center.y, bounds[index].center.z,
                    bounds[index].size.x, bounds[index].size.y, bounds[index].size.z,
                    prefabs[name].GetComponent<CraftingStation>().m_rangeBuild }.Select(value => value.ToString("R", CultureInfo.InvariantCulture)))));
            string stamp = BuildWorksTestIsolation.SupplyStamp(description), receipt = BuildWorksTestIsolation.StationReceiptPath;
            if (BuildWorksTestIsolation.OnceComplete(receipt, stamp)) yield break;
            int terrain = LayerMask.NameToLayer("terrain");
            if (terrain < 0) throw new InvalidOperationException("Native terrain layer is missing.");
            int mask = ~(1 << terrain);
            var planned = new List<Bounds>();
            var positions = new Vector3[prefabs.Count];
            int site = 0;
            foreach (Piece prefab in prefabs.Values)
            {
                positions[site] = FindSite(player, bounds[site], planned, mask, prefab.GetComponent<CraftingStation>().m_rangeBuild);
                site++;
            }
            BuildWorksTestIsolation.RequireLoadedSession();
            if (!BuildWorksTestIsolation.BeginOnce(receipt, stamp)) yield break;
            var created = new List<CraftingStation>();
            int part = 0;
            foreach (Piece prefab in prefabs.Values)
            {
                if (BuildWorksTestIsolation.RequireLoadedSession() != player || !Clear(planned[part], mask))
                    throw new InvalidOperationException("QA session/site changed before station placement.");
                var before = new HashSet<CraftingStation>(UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None));
                player.PlacePiece(prefab, positions[part], Quaternion.identity, false, false);
                yield return null; // CraftingStation registers in Start.
                if (BuildWorksTestIsolation.RequireLoadedSession() != player)
                    throw new InvalidOperationException("QA player changed during station placement.");
                CraftingStation[] added = UnityEngine.Object.FindObjectsByType<CraftingStation>(FindObjectsSortMode.None).Where(station => !before.Contains(station)).ToArray();
                if (added.Length != 1) throw new InvalidOperationException("QA native station creation was ambiguous.");
                CraftingStation actual = added[0];
                ZNetView view = actual.GetComponent<ZNetView>();
                Piece actualPiece = actual.GetComponent<Piece>();
                if (!view || !view.IsValid() || !view.IsOwner() || view.GetZDO().m_uid.IsNone() ||
                    view.GetZDO().GetPrefab() != prefab.name.GetStableHashCode() || !actualPiece ||
                    actualPiece.GetCreator() != player.GetPlayerID() ||
                    Vector3.Distance(actual.transform.position, positions[part]) > 0.01f ||
                    Quaternion.Angle(actual.transform.rotation, Quaternion.identity) > 0.01f ||
                    Vector3.Distance(actual.transform.localScale, prefab.transform.localScale) > 0.001f ||
                    CraftingStation.HaveBuildStationInRange(actual.m_name, player.transform.position) != actual)
                    throw new InvalidOperationException("QA station identity/transform/build range was not restored by native placement.");
                player.AddKnownStation(actual);
                created.Add(actual);
                part++;
            }
            BuildWorksTestIsolation.RequireLoadedSession();
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            profile.SavePlayerData(player);
            if (!profile.Save()) throw new IOException("QA station knowledge character save failed.");
            ZNet network = ZNet.instance;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (network.IsSaving() && Time.realtimeSinceStartup < deadline)
            { BuildWorksTestIsolation.RequireLoadedSession(); yield return null; }
            if (network.IsSaving()) throw new IOException("Existing native world save did not finish.");
            BuildWorksTestIsolation.RequireLoadedSession();
            uint beforeSave = SaveSystem.GetSaveNumber();
            network.Save(false, false, false);
            deadline = Time.realtimeSinceStartup + 60f;
            while (network.IsSaving() && Time.realtimeSinceStartup < deadline)
            { BuildWorksTestIsolation.RequireLoadedSession(); yield return null; }
            BuildWorksTestIsolation.RequireLoadedSession();
            if (network != ZNet.instance || network.IsSaving()) throw new IOException("QA native world save timed out or changed session.");
            RequireSaveAcknowledgement(beforeSave, SaveSystem.GetSaveNumber());
            if (created.Any(station => !station || !station.GetComponent<ZNetView>().IsValid()))
                throw new InvalidOperationException("A prepared QA station disappeared before save completion.");
            BuildWorksTestIsolation.CompleteOnce(receipt, stamp);
        }
    }
}
#endif
