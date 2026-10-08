#if BUILDWORKS_TEST_STAND
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Explicitly requested offline QA terrain, never a player construction capability.
    internal static class BuildWorksTestYard
    {
        internal const int HalfExtent = 96;
        internal const int SearchExtent = 4096;
        internal static bool IsPrepared { get; private set; }
        private static bool previousDaylight;
        private static float previousTime;
        private static int environmentId;

        internal static float Plane(float minimum, float maximum, float water)
        {
            if (new[] { minimum, maximum, water }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
                maximum < minimum || maximum - minimum > 30f || minimum < water + 3f)
                throw new InvalidDataException("QA yard needs dry terrain within its isolated sixteen-metre leveling limit.");
            return minimum + (maximum - minimum) * 0.5f;
        }

        private static void Daylight()
        {
            BuildWorksTestIsolation.RequireLoadedSession();
            EnvMan environment = EnvMan.instance;
            if (!environment) throw new InvalidOperationException("QA daylight needs a loaded environment.");
            if (environmentId != environment.GetInstanceID())
            {
                previousDaylight = environment.m_debugTimeOfDay;
                previousTime = environment.m_debugTime;
                environmentId = environment.GetInstanceID();
            }
            environment.m_debugTimeOfDay = true;
            environment.m_debugTime = 0.5f;
        }

        internal static void RestoreDaylight()
        {
            if (EnvMan.instance && EnvMan.instance.GetInstanceID() == environmentId)
            {
                EnvMan.instance.m_debugTimeOfDay = previousDaylight;
                EnvMan.instance.m_debugTime = previousTime;
            }
            environmentId = 0;
            IsPrepared = false;
        }

        private static bool Inside(Vector3 point, Vector3 center, float margin = 0)
            => Mathf.Abs(point.x - center.x) <= HalfExtent + margin && Mathf.Abs(point.z - center.z) <= HalfExtent + margin;

        private static Vector3 FindSite()
        {
            WorldGenerator generator = WorldGenerator.instance;
            if (generator == null) throw new InvalidOperationException("QA yard needs a loaded world generator.");
            float best = float.MaxValue;
            float smallestDrySpan = float.MaxValue;
            Vector3 selected = Vector3.zero;
            Debug.Log("BuildWorks QA yard search: 192x192m; centres within +/-4096m; QA-only marked terrain bound 16m.");
            // ponytail: bounded scan of this QA seed; refuse instead of falling back into spawn or water.
            for (int x = -SearchExtent; x <= SearchExtent; x += 128)
                for (int z = -SearchExtent; z <= SearchExtent; z += 128)
                {
                    if (x * x + z * z < 512 * 512) continue;
                    float minimum = float.MaxValue, maximum = float.MinValue;
                    bool valid = true;
                    for (int dx = -HalfExtent; dx <= HalfExtent && valid; dx += 16)
                        for (int dz = -HalfExtent; dz <= HalfExtent; dz += 16)
                        {
                            var point = new Vector3(x + dx, 0, z + dz);
                            float height = generator.GetHeight(point.x, point.z);
                            if (generator.GetBiome(point) != Heightmap.Biome.Meadows || float.IsNaN(height) || float.IsInfinity(height))
                            { valid = false; break; }
                            minimum = Math.Min(minimum, height); maximum = Math.Max(maximum, height);
                        }
                    if (!valid || minimum < ZoneSystem.instance.m_waterLevel + 3f) continue;
                    smallestDrySpan = Math.Min(smallestDrySpan, maximum - minimum);
                    if (maximum - minimum > 30f) continue;
                    float score = maximum - minimum + Mathf.Sqrt(x * x + z * z) * 0.001f;
                    if (score >= best) continue;
                    best = score;
                    selected = new Vector3(x, Plane(minimum, maximum, ZoneSystem.instance.m_waterLevel), z);
                }
            if (best == float.MaxValue) throw new InvalidOperationException("No suitable distant 192m QA yard found within +/-4096m; smallest dry Meadows span: " +
                smallestDrySpan.ToString("R", CultureInfo.InvariantCulture) + "; no terrain changed.");
            return selected;
        }

        private static HashSet<Heightmap> CheckGround(Vector3 center, bool requireFlat)
        {
            var maps = new HashSet<Heightmap>();
            for (int x = -HalfExtent; x <= HalfExtent; ++x)
                for (int z = -HalfExtent; z <= HalfExtent; ++z)
                {
                    var point = new Vector3(center.x + x, 0, center.z + z);
                    Heightmap map = Heightmap.FindHeightmap(point);
                    if (!map || !ZoneSystem.instance.IsZoneLoaded(point) || !map.GetWorldHeight(point, out float height) ||
                        Location.IsInsideNoBuildLocation(point) || float.IsNaN(height) || float.IsInfinity(height) ||
                        Mathf.Abs(height - center.y) > (requireFlat ? 0.03f : 15.5f) ||
                        (!requireFlat && Mathf.Abs(height - WorldGenerator.instance.GetHeight(point.x, point.z)) > 0.05f))
                        throw new InvalidOperationException("QA yard footprint is unloaded, protected, nonflat or beyond native leveling limits.");
                    maps.Add(map);
                }
            if (maps.Count == 0 || maps.Count > 25) throw new InvalidDataException("Unexpected QA yard heightmap count.");
            return maps;
        }

        private static IEnumerator SaveWorld()
        {
            ZNet network = ZNet.instance;
            float deadline = Time.realtimeSinceStartup + 60;
            while (network.IsSaving() && Time.realtimeSinceStartup < deadline) yield return null;
            BuildWorksTestIsolation.RequireLoadedSession();
            if (network != ZNet.instance || network.IsSaving()) throw new IOException("QA yard save is busy or changed host.");
            uint before = SaveSystem.GetSaveNumber();
            network.Save(false, false, false);
            deadline = Time.realtimeSinceStartup + 60;
            while (network.IsSaving() && Time.realtimeSinceStartup < deadline) yield return null;
            BuildWorksTestIsolation.RequireLoadedSession();
            if (network != ZNet.instance || network.IsSaving()) throw new IOException("QA yard save timed out or changed host.");
            BuildWorksTestStations.RequireSaveAcknowledgement(before, SaveSystem.GetSaveNumber());
        }

        private static void RequireEmpty(Vector3 center)
        {
            if (UnityEngine.Object.FindObjectsByType<Piece>(FindObjectsSortMode.None).Any(p => Overlaps(p, center)) ||
                UnityEngine.Object.FindObjectsByType<Container>(FindObjectsSortMode.None).Any(p => Overlaps(p, center)))
                throw new InvalidOperationException("QA yard contains construction or storage; refusing to alter it.");
        }

        private static bool Overlaps(Component root, Vector3 center)
        {
            if (Inside(root.transform.position, center, 4)) return true;
            bool Touches(Bounds bounds) => bounds.min.x <= center.x + HalfExtent + 4 && bounds.max.x >= center.x - HalfExtent - 4 &&
                bounds.min.z <= center.z + HalfExtent + 4 && bounds.max.z >= center.z - HalfExtent - 4;
            return root.GetComponentsInChildren<Collider>(true).Any(c => c.enabled && c.gameObject.activeInHierarchy && Touches(c.bounds)) ||
                root.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled && r.gameObject.activeInHierarchy && Touches(r.bounds));
        }

        private static ZNetView[] Vegetation(Vector3 center)
        {
            var vegetation = UnityEngine.Object.FindObjectsByType<ZNetView>(FindObjectsSortMode.None)
                .Where(v => Inside(v.transform.position, center, 2) && (v.GetComponent<TreeBase>() || v.GetComponent<TreeLog>() || v.GetComponent<Destructible>()))
                .ToArray();
            if (vegetation.Length > 1000 || vegetation.Any(v => !v.IsValid() || v.GetComponent<Piece>() || v.GetComponent<Character>() ||
                v.GetComponentsInChildren<MonoBehaviour>(true).Any(c => !c || c.GetType().Assembly != typeof(Piece).Assembly)))
                throw new InvalidOperationException("QA yard contains excessive or unreviewed vegetation/objects.");
            return vegetation;
        }

        internal static IEnumerator Prepare()
        {
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            string request = BuildWorksTestIsolation.YardRequestPath;
            BuildWorksTestIsolation.ValidateSavePath(request);
            if (!File.Exists(request)) yield break;
            if (new FileInfo(request).Length > 64 || File.ReadAllText(request).Trim() != BuildWorksTestIsolation.ReadIdentityReceipt()[1])
                throw new InvalidDataException("QA yard request does not match the isolated session.");
            Daylight();
            string planPath = BuildWorksTestIsolation.YardPlanPath;
            BuildWorksTestIsolation.ValidateSavePath(planPath);
            Vector3 center;
            if (File.Exists(planPath))
            {
                if (new FileInfo(planPath).Length > 128) throw new InvalidDataException("Invalid QA yard plan.");
                string[] values = File.ReadAllLines(planPath);
                if (values.Length != 3) throw new InvalidDataException("Invalid QA yard plan.");
                center = new Vector3(float.Parse(values[0], CultureInfo.InvariantCulture), float.Parse(values[1], CultureInfo.InvariantCulture),
                    float.Parse(values[2], CultureInfo.InvariantCulture));
                if (new[] { center.x, center.y, center.z }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
                    Math.Abs(center.x) > SearchExtent || Math.Abs(center.z) > SearchExtent || center.x * center.x + center.z * center.z < 512 * 512 ||
                    center.x % 128 != 0 || center.z % 128 != 0 || center.y < ZoneSystem.instance.m_waterLevel + 3 || center.y > 200)
                    throw new InvalidDataException("QA yard plan is outside the bounded distant dry area.");
            }
            else
            {
                center = FindSite();
                using (var writer = new StreamWriter(new FileStream(planPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)))
                    foreach (float value in new[] { center.x, center.y, center.z }) writer.WriteLine(value.ToString("R", CultureInfo.InvariantCulture));
            }
            string stamp = BuildWorksTestIsolation.SupplyStamp("yard192-v1\n" + File.ReadAllText(planPath));
            string receipt = BuildWorksTestIsolation.YardReceiptPath;
            if (BuildWorksTestIsolation.OnceComplete(receipt, stamp)) { IsPrepared = true; yield break; }
            if (!player.TeleportTo(new Vector3(center.x, WorldGenerator.instance.GetHeight(center.x, center.z) + 1, center.z), Quaternion.identity, true))
                throw new InvalidOperationException("QA yard teleport refused.");
            float deadline = Time.realtimeSinceStartup + 90;
            do { BuildWorksTestIsolation.AssertPinned(); yield return null; }
            while (player && player.IsTeleporting() && Time.realtimeSinceStartup < deadline);
            BuildWorksTestIsolation.RequireLoadedSession();
            if (player != Player.m_localPlayer ||
                !Inside(player.transform.position, center, -90)) throw new InvalidOperationException("QA yard teleport did not complete.");
            HashSet<Heightmap> maps = CheckGround(center, false);
            RequireEmpty(center);
            Vegetation(center);
            MethodInfo operation = typeof(TerrainComp).GetMethod("DoOperation", BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(Vector3), typeof(Vector3), typeof(TerrainOp.Settings) }, null);
            FieldInfo initialized = typeof(TerrainComp).GetField("m_initialized", BindingFlags.NonPublic | BindingFlags.Instance);
            if (operation == null || initialized == null) throw new MissingMethodException("QA yard native terrain contract changed.");
            // Save a recoverable native world snapshot before the first terrain/object mutation.
            var saving = SaveWorld();
            while (saving.MoveNext()) yield return saving.Current;
            if (!SaveSystem.TryGetSaveByName(ZNet.instance.GetWorld().m_worldName, SaveDataType.World, out SaveWithBackups saved) || saved.PrimaryFile == null)
                throw new IOException("QA yard backup needs native primary world files.");
            string backup = Path.Combine(Path.GetDirectoryName(receipt), "yard-before");
            BuildWorksTestIsolation.ValidateSavePath(backup);
            if (Directory.Exists(backup)) throw new IOException("QA yard backup already exists without completion; inspect before retry.");
            Directory.CreateDirectory(backup);
            foreach (string path in saved.PrimaryFile.AllPaths)
            {
                BuildWorksTestIsolation.ValidateSavePath(path);
                File.Copy(path, Path.Combine(backup, Path.GetFileName(path)), false);
            }
            BuildWorksTestIsolation.RequireLoadedSession();
            CheckGround(center, false);
            RequireEmpty(center);
            if (!BuildWorksTestIsolation.BeginOnce(receipt, stamp)) yield break;
            var settings = new TerrainOp.Settings
            {
                m_level = true,
                m_levelRadius = HalfExtent,
                m_levelOffset = 0,
                m_square = true,
                m_paintCleared = true,
                m_paintRadius = HalfExtent,
                m_paintType = TerrainModifier.PaintType.Dirt,
                m_paintHeightCheck = false
            };
            var compilers = maps.Select(map => map.GetAndCreateTerrainCompiler()).ToArray();
            yield return null;
            if (BuildWorksTestIsolation.RequireLoadedSession() != player)
                throw new InvalidOperationException("QA player changed while the terrain compilers initialized.");
            CheckGround(center, false);
            RequireEmpty(center);
            var vegetation = Vegetation(center);
            foreach (TerrainComp compiler in compilers)
            {
                ZNetView view = compiler ? compiler.GetComponent<ZNetView>() : null;
                if (!view || !view.IsValid() || !(bool)initialized.GetValue(compiler)) throw new InvalidOperationException("QA terrain compiler is not ready.");
                view.ClaimOwnership();
                if (!view.IsOwner()) throw new InvalidOperationException("QA terrain ownership refused.");
            }
            // No frame boundary between the final preflight and terrain/object mutations.
            foreach (TerrainComp compiler in compilers)
            {
                ZNetView view = compiler.GetComponent<ZNetView>();
                view.GetZDO().Set(BuildWorksTestTerrain.Marker, BuildWorksTestIsolation.ReadIdentityReceipt()[1]);
                operation.Invoke(compiler, new object[] { center, Vector3.zero, settings });
                if (view.GetZDO().GetByteArray(ZDOVars.s_TCData) == null) throw new IOException("QA terrain was not persisted to its native ZDO.");
            }
            foreach (Heightmap map in maps) map.Poke(0, false);
            foreach (ZNetView view in vegetation)
            {
                BuildWorksTestIsolation.RequireLoadedSession();
                view.ClaimOwnership();
                if (!view.IsOwner()) throw new InvalidOperationException("QA vegetation ownership refused.");
                ZNetScene.instance.Destroy(view.gameObject);
            }
            yield return null;
            CheckGround(center, true);
            BuildWorksTestIsolation.RequireLoadedSession();
            player.transform.position = center + Vector3.up;
            PlayerProfile profile = Game.instance.GetPlayerProfile();
            profile.SavePlayerData(player);
            if (!profile.Save()) throw new IOException("QA yard character save failed.");
            saving = SaveWorld();
            while (saving.MoveNext()) yield return saving.Current;
            BuildWorksTestIsolation.CompleteOnce(receipt, stamp);
            IsPrepared = true;
            Debug.Log("BuildWorks QA yard prepared: 192x192m at " + center + "; permanent noon; native terrain saved; vegetation cleared: " + vegetation.Length);
        }
    }
}
#endif
