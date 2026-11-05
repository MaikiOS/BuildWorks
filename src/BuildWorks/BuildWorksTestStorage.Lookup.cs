#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BuildWorksTestStorage
    {
        [Serializable] internal sealed class StorageLookupResult
        {
            public int schemaVersion = 1;
            public string scope = "current isolated ZDOMan registry";
            public bool complete, valid, truncated;
            public string reason, operation, prefab, journalHash;
            public long world, player;
            public int prefabHash, frame, calls, finalIndex, rawRows, uniqueMatches;
            public StorageLookupMatch[] matches = Array.Empty<StorageLookupMatch>();
        }
        [Serializable] internal sealed class StorageLookupMatch
        {
            public long userId, creator;
            public uint id, revision;
            public int prefabHash;
            public string marker;
            public float x, y, z;
            public bool persistent, sceneInstancePresent;
        }

        internal static StorageLookupResult LookupStorageMarker(string operation, string prefabName, Action requireIdle)
        {
            if (!Guid.TryParseExact(operation, "N", out var id) || id == Guid.Empty || operation != id.ToString("N") ||
                string.IsNullOrWhiteSpace(prefabName))
                throw new ArgumentException("Canonical nonempty operation and exact prefab name are required.");
            requireIdle();
            Player player = BuildWorksTestIsolation.RequireLoadedSession();
            ZNet network = ZNet.instance; Game game = Game.instance;
            World world = network.GetWorld(); PlayerProfile profile = game.GetPlayerProfile();
            ZDOMan manager = ZDOMan.instance; ZNetScene scene = ZNetScene.instance;
            if (manager == null || !scene || network.IsSaving())
                throw new InvalidOperationException("Storage lookup needs an idle loaded registry.");
            var prefab = scene.GetPrefab(prefabName);
            int hash = prefabName.GetStableHashCode();
            if (!prefab || prefab.name != prefabName)
                throw new InvalidOperationException("Storage lookup needs an exact registered prefab root name.");
            long worldId = world.m_uid, playerId = player.GetPlayerID();
            int frame = Time.frameCount, thread = Thread.CurrentThread.ManagedThreadId, count = manager.NrOfObjects();
            uint generation = SaveSystem.GetSaveNumber();
            string worldPath = world.GetSaveFWLPath(), profilePath = profile.GetPath(), filename = profile.GetFilename();
            void RequireContext()
            {
                if (Thread.CurrentThread.ManagedThreadId != thread)
                    throw new InvalidOperationException("Storage lookup changed threads.");
                requireIdle();
                if (BuildWorksTestIsolation.RequireLoadedSession() != player || ZNet.instance != network || Game.instance != game ||
                    !ReferenceEquals(network.GetWorld(), world) || !ReferenceEquals(game.GetPlayerProfile(), profile) ||
                    !ReferenceEquals(ZDOMan.instance, manager) || ZNetScene.instance != scene || scene.GetPrefab(prefabName) != prefab ||
                    network.IsSaving() || world.m_uid != worldId || player.GetPlayerID() != playerId || profile.GetPlayerID() != playerId ||
                    world.GetSaveFWLPath() != worldPath || profile.GetPath() != profilePath || profile.GetFilename() != filename ||
                    SaveSystem.GetSaveNumber() != generation || Time.frameCount != frame || manager.NrOfObjects() != count ||
                    SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter) || SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld))
                    throw new InvalidOperationException("Storage lookup context changed or saves are disabled.");
            }
            RequireContext();
            string path = Path.Combine(BlueprintStorageTransferJournal.Root, operation + ".bwtransfer");
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(path));
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Storage lookup refuses a linked journal.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                long length = file.Length;
                if (length < 44 || length > 16 * 1024 * 1024) throw new InvalidDataException("Storage journal length is invalid.");
                string Digest()
                {
                    file.Position = 0;
                    using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "");
                }
                string before = Digest();
                var saved = BlueprintStorageTransferJournal.Read(path); var plan = saved.Prepared;
                if (saved.Schema != 4 || saved.Settled || plan == null || plan.Operation != id ||
                    plan.Route != BlueprintStorageTransferJournal.Direction.ToContainer || plan.World != worldId || plan.Player != playerId ||
                    plan.Creator != playerId || plan.PrefabHash != hash ||
                    plan.PieceName != prefabName && plan.PieceName != prefabName + "(Clone)")
                    throw new InvalidDataException("Storage lookup requires the retained schema4 forward operation in this QA world for this payer/prefab.");
                var result = ScanStorageRegistry(operation, prefabName, hash, manager, scene, RequireContext);
                result.world = worldId; result.player = playerId; result.frame = frame; result.journalHash = before;
                try
                {
                    RequireContext();
                    if (file.Length != length || Digest() != before)
                        throw new InvalidDataException("Storage journal changed during lookup.");
                }
                catch (Exception error) { result.complete = result.valid = false; result.reason = error.GetBaseException().Message; }
                return result;
            }
        }

        // Registry identity is diagnostic evidence only; it never releases retained custody.
        internal static StorageLookupResult ScanStorageRegistry(string operation, string prefab, int hash,
            ZDOMan manager, ZNetScene scene, Action requireContext)
        {
            var result = new StorageLookupResult { operation = operation, prefab = prefab, prefabHash = hash };
            var matches = new Dictionary<ZDOID, (ZDO zdo, StorageLookupMatch row)>();
            var batch = new List<ZDO>(); var clock = Stopwatch.StartNew(); int index = 0;
            try
            {
                while (true)
                {
                    requireContext();
                    if (result.calls >= 4096 || clock.ElapsedMilliseconds >= 2000)
                    { result.truncated = true; throw new InvalidOperationException("Storage registry lookup reached its call/time ceiling."); }
                    int previous = index;
                    // One native call cannot be interrupted; these ceilings apply between calls.
                    bool done = manager.GetAllZDOsWithPrefabIterative(prefab, batch, ref index);
                    ++result.calls; result.finalIndex = index; result.rawRows = checked(result.rawRows + batch.Count);
                    requireContext();
                    if (result.rawRows > 100000 || clock.ElapsedMilliseconds >= 2000)
                    { result.truncated = true; throw new InvalidOperationException("Storage registry lookup reached its row/time ceiling."); }
                    foreach (var zdo in batch)
                    {
                        if (zdo == null || !zdo.IsValid()) throw new InvalidOperationException("Storage registry returned an invalid row.");
                        if (zdo.GetPrefab() != hash || !string.Equals(zdo.GetString(BlueprintStorageCapture.TransferMarkerKey, ""), operation, StringComparison.Ordinal)) continue;
                        if (matches.TryGetValue(zdo.m_uid, out var existing))
                        {
                            if (!ReferenceEquals(existing.zdo, zdo)) throw new InvalidOperationException("Storage registry identity changed.");
                            continue;
                        }
                        if (matches.Count >= 16)
                        { result.truncated = true; throw new InvalidOperationException("Storage registry lookup reached its match ceiling."); }
                        matches.Add(zdo.m_uid, (zdo, ReadLookupMatch(zdo, scene)));
                    }
                    batch.Clear();
                    if (done) break;
                    if (index <= previous) throw new InvalidOperationException("Storage registry iterator made no progress.");
                }
                requireContext();
                foreach (var match in matches.Values)
                {
                    var zdo = match.zdo; var row = match.row;
                    if (!zdo.IsValid() || !ReferenceEquals(manager.GetZDO(zdo.m_uid), zdo) || zdo.m_uid.UserID != row.userId || zdo.m_uid.ID != row.id ||
                        !SameLookupMatch(row, ReadLookupMatch(zdo, scene)))
                        throw new InvalidOperationException("Storage registry match changed before final validation.");
                }
                requireContext();
                if (clock.ElapsedMilliseconds >= 2000)
                { result.truncated = true; throw new InvalidOperationException("Storage registry validation reached its time ceiling."); }
                result.complete = result.valid = true;
                result.reason = matches.Count == 0 ? "marker-not-found-in-current-registry" : matches.Count == 1 ? "current-identity-found-no-recovery-authority" : "ambiguous-marker";
            }
            catch (Exception error) { result.complete = result.valid = false; result.reason = error.GetBaseException().Message; }
            result.matches = matches.Values.Select(value => value.row).ToArray(); result.uniqueMatches = result.matches.Length;
            return result;
        }

        private static StorageLookupMatch ReadLookupMatch(ZDO zdo, ZNetScene scene)
        {
            Vector3 position = zdo.GetPosition();
            if (float.IsNaN(position.x) || float.IsInfinity(position.x) || float.IsNaN(position.y) || float.IsInfinity(position.y) ||
                float.IsNaN(position.z) || float.IsInfinity(position.z)) throw new InvalidOperationException("Storage position is not finite.");
            return new StorageLookupMatch { userId = zdo.m_uid.UserID, id = zdo.m_uid.ID, prefabHash = zdo.GetPrefab(),
                marker = zdo.GetString(BlueprintStorageCapture.TransferMarkerKey, ""), x = position.x, y = position.y, z = position.z,
                persistent = zdo.Persistent, revision = zdo.DataRevision, creator = zdo.GetLong(ZDOVars.s_creator, 0),
                sceneInstancePresent = scene.FindInstance(zdo.m_uid) != null };
        }
        private static bool SameLookupMatch(StorageLookupMatch a, StorageLookupMatch b) =>
            a.userId == b.userId && a.id == b.id && a.prefabHash == b.prefabHash && a.marker == b.marker &&
            a.x == b.x && a.y == b.y && a.z == b.z && a.persistent == b.persistent && a.revision == b.revision &&
            a.creator == b.creator && a.sceneInstancePresent == b.sceneInstancePresent;
    }
}
#endif
