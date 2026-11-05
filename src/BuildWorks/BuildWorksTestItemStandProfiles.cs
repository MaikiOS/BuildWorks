#if BUILDWORKS_TEST_STAND
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Read current native profiles only; no author DLL, item creation or world changes.
    internal static class BuildWorksTestItemStandProfiles
    {
        private static readonly string[] ProbeNames = { "itemstand", "itemstandh", "wood_floor", "piece_chest_wood", "ArmorStand" };
        [Serializable] internal sealed class Item
        {
            public string name;
            public int hash, variants, maximumQuality;
            public int[] orientations;
        }
        [Serializable] internal sealed class Profile
        {
            public string name, kind, host;
            public bool holder, ordinary, container, header, persistent, distant, syncInitialScale;
            public int prefabHash, objectType;
            public double[] prefabScale;
            public Item[] items;
        }
        [Serializable] internal sealed class Report
        {
            public int process, registry, itemCatalog;
            public bool passed, inventoryUnchanged, registryUnchanged, playerUnchanged, configUnchanged, profilesUnchanged;
            public Profile[] profiles;
        }
        private static string Serialize(object value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        private static Profile Read(GameObject prefab)
        {
            var context = BlueprintUnboundImport.Capture(prefab);
            if (context == null) throw new InvalidOperationException("Missing native profile: " + prefab.name);
            return new Profile { name = prefab.name, kind = context.Metadata.Kind, host = context.Metadata.HostId,
                holder = context.ItemStandItems != null, ordinary = context.Ordinary,
                container = context.ContainerGrid.HasValue, header = context.NativeHeader.HasValue,
                prefabHash = context.NativeHeader?.PrefabHash ?? 0, persistent = context.NativeHeader?.Persistent ?? false,
                distant = context.NativeHeader?.Distant ?? false, objectType = context.NativeHeader?.ObjectType ?? 0,
                syncInitialScale = context.SyncInitialScale,
                prefabScale = new[] { context.Metadata.PrefabScale.X, context.Metadata.PrefabScale.Y, context.Metadata.PrefabScale.Z },
                items = context.ItemStandItems?.OrderBy(pair => pair.Value.Name, StringComparer.Ordinal).Select(pair => new Item {
                    hash = pair.Key, name = pair.Value.Name, variants = pair.Value.Variants,
                    maximumQuality = pair.Value.MaximumQuality, orientations = pair.Value.Orientations.ToArray() }).ToArray() };
        }
        internal static string Snapshot(Action requireIdle)
        {
            if (requireIdle == null) throw new ArgumentNullException(nameof(requireIdle));
            requireIdle();
            return ReadProfiles(requireIdle);
        }
        // Keep early guards callable without JIT-compiling host IL, as in ReadContactProfiles.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadProfiles(Action requireIdle)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            if (!Application.isBatchMode) throw new InvalidOperationException("Background QA required.");
            if (!ZNetScene.instance || !ObjectDB.instance || ObjectDB.instance.m_items == null)
                throw new InvalidOperationException("Loaded native catalog required.");
            if (ObjectDB.instance.m_items.Count > 4096)
                throw new InvalidOperationException("Native item catalog exceeds4096: " + ObjectDB.instance.m_items.Count);
            var registry = (System.Collections.Generic.List<Piece>)AccessTools.Field(typeof(Piece), "s_allPieces").GetValue(null);
            if (registry == null || registry.Count > 4096) throw new InvalidOperationException("Bounded live registry required.");
            var ids = registry.Select(piece => piece ? piece.GetInstanceID() : 0).ToArray();
            var inventory = new ZPackage(); player.GetInventory().Save(inventory);
            var position = player.transform.position; var rotation = player.transform.rotation; string config = Paths.ConfigPath;
            var prefabs = ProbeNames.Select(name => ZNetScene.instance.GetPrefab(name)).ToArray();
            if (prefabs.Where((prefab, index) => !prefab || prefab.name != ProbeNames[index]).Any() ||
                prefabs.Take(2).Any(prefab => !prefab.GetComponent<ItemStand>()) || !prefabs[4].GetComponent<ArmorStand>())
                throw new InvalidOperationException("Missing exact native profile controls.");
            var profiles = prefabs.Select(Read).ToArray();
            bool profilesUnchanged = Serialize(profiles) == Serialize(prefabs.Select(Read).ToArray());
            requireIdle(); BuildWorksTestIsolation.RequireLoadedSession();
            var after = new ZPackage(); player.GetInventory().Save(after);
            var report = new Report { process = System.Diagnostics.Process.GetCurrentProcess().Id, registry = ids.Length,
                itemCatalog = ObjectDB.instance.m_items.Count, profiles = profiles,
                inventoryUnchanged = inventory.GetArray().SequenceEqual(after.GetArray()),
                registryUnchanged = ids.SequenceEqual(registry.Select(piece => piece ? piece.GetInstanceID() : 0)),
                playerUnchanged = position.Equals(player.transform.position) && rotation.Equals(player.transform.rotation),
                configUnchanged = config == Paths.ConfigPath,
                profilesUnchanged = profilesUnchanged };
            report.passed = profiles.Take(2).All(profile => profile.holder && !profile.ordinary && !profile.container &&
                profile.header && profile.items.Length > 0) && profiles.Skip(2).All(profile => !profile.holder) &&
                profiles.Take(2).Any(profile => profile.items.Any(item => item.name == "SwordIron" && item.orientations.Length > 0)) &&
                report.inventoryUnchanged && report.registryUnchanged && report.playerUnchanged && report.configUnchanged && report.profilesUnchanged;
            return Serialize(report);
        }
    }
}
#endif
