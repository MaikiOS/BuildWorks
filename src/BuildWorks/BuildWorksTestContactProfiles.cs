#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Read prefab/ghost facts only; no Instantiate, component setters or gameplay methods.
    internal static class BuildWorksTestContactProfiles
    {
        [Serializable] internal sealed class Profile
        {
            public string name, material;
            public int registryHash, instanceId, category, wearComponents;
            public long usage;
            public bool? supports, noSupportWear;
            public bool groundPiece, groundOnly, cultivatedOnly, vegetationOnly, waterPiece,
                notOnWood, ceilingOnly, notOnFloor, notOnTilt;
            public string[] components, behaviours, colliders;
            public ColliderProfile[] colliderDetails;
        }
        [Serializable] internal sealed class ColliderProfile
        {
            public string path, type, meshName;
            public int instanceId, layer;
            public bool enabled, isTrigger, activeSelf, activeInHierarchy, attachedRigidbody, staticSolidBranch;
            public bool? convex, readable;
            public int? vertexCount;
        }
        [Serializable] internal sealed class Page
        {
            public int offset, total, frame, registryTotal, missingPrefabs;
            public string registryFingerprint, hostMvid;
            public Profile[] profiles;
            public ColliderProfile groundCollider;
        }

        [Serializable] internal sealed class GhostObservation
        {
            public int frame, nativeId, sessionId, placeRayMask;
            public string state, mode, nativeName;
            public bool sameGhost, nativeActive, sessionActive;
            public ColliderProfile[] colliders;
        }

        internal static string CurrentGhost(Action requireObservation)
        {
            if (requireObservation == null) throw new ArgumentNullException(nameof(requireObservation));
            requireObservation();
            return ReadCurrentGhost(requireObservation);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadCurrentGhost(Action requireObservation)
        {
            var player = Player.m_localPlayer;
            if (!player) throw new InvalidOperationException("Loaded player required for ghost observation.");
            var nativeField = AccessTools.Field(typeof(Player), "m_placementGhost");
            var sessionField = AccessTools.Field(typeof(PrecisionPlacementSession), "placementGhost");
            var stateField = AccessTools.Field(typeof(PrecisionPlacementSession), "state");
            var modeField = AccessTools.Field(typeof(PrecisionPlacementSession), "sessionMode");
            var maskField = AccessTools.Field(typeof(Player), "m_placeRayMask");
            if (nativeField == null || sessionField == null || stateField == null || modeField == null || maskField == null)
                throw new InvalidOperationException("Native/session ghost observation contract unavailable.");
            var session = BuildWorksPlugin.QaPlacementSession;
            if (session == null) throw new InvalidOperationException("Placement session unavailable.");
            var native = nativeField.GetValue(player) as GameObject;
            var current = sessionField.GetValue(session) as GameObject;
            object rawMask = maskField.GetValue(player);
            int mask = rawMask is LayerMask layerMask ? layerMask.value : rawMask is int value ? value :
                throw new InvalidOperationException("Native placement mask type unavailable.");
            Collider[] colliders = native ? native.GetComponentsInChildren<Collider>(true) : Array.Empty<Collider>();
            if (colliders.Length > 128) throw new InvalidOperationException("Ghost observation exceeds128colliders.");
            var result = new GhostObservation { frame = Time.frameCount, placeRayMask = mask,
                nativeId = native ? native.GetInstanceID() : 0, sessionId = current ? current.GetInstanceID() : 0,
                sameGhost = native && current && native == current, nativeName = native ? native.name : null,
                nativeActive = native && native.activeInHierarchy, sessionActive = current && current.activeInHierarchy,
                state = stateField.GetValue(session)?.ToString(), mode = modeField.GetValue(session)?.ToString(),
                colliders = colliders.Select(c => ReadCollider(c, native.transform)).ToArray() };
            requireObservation();
            if (Player.m_localPlayer != player || BuildWorksPlugin.QaPlacementSession != session ||
                nativeField.GetValue(player) as GameObject != native || sessionField.GetValue(session) as GameObject != current ||
                stateField.GetValue(session)?.ToString() != result.state || modeField.GetValue(session)?.ToString() != result.mode)
                throw new InvalidOperationException("Placement ghost changed during observation.");
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(GhostObservation)).WriteObject(stream, result);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        internal static string Snapshot(int offset, int count, Action requireIdle)
        {
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (count < 1 || count > 32) throw new ArgumentOutOfRangeException(nameof(count));
            if (requireIdle == null) throw new ArgumentNullException(nameof(requireIdle));
            requireIdle();
            return ReadPage(offset, count, requireIdle);
        }

        // Keep early guards callable without JIT-compiling Unity ECalls outside the game.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ReadPage(int offset, int count, Action requireIdle)
        {
            var scene = ZNetScene.instance;
            if (!scene) throw new InvalidOperationException("Loaded native prefab registry required.");
            var field = AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs");
            var registry = field?.GetValue(scene) as Dictionary<int, GameObject>;
            if (registry == null) throw new InvalidOperationException("Native prefab registry unavailable.");
            var all = registry.OrderBy(pair => pair.Key).ToArray();
            string fingerprint = Fingerprint(all);
            var pieces = all.Where(pair => pair.Value && pair.Value.GetComponent<Piece>()).ToArray();
            if (offset > pieces.Length) throw new ArgumentOutOfRangeException(nameof(offset));
            var page = new Page { offset = offset, total = pieces.Length, frame = Time.frameCount,
                registryTotal = all.Length, missingPrefabs = all.Count(pair => !pair.Value),
                registryFingerprint = fingerprint, hostMvid = typeof(Piece).Module.ModuleVersionId.ToString(),
                profiles = pieces.Skip(offset).Take(count).Select(pair => Read(pair.Key, pair.Value)).ToArray(),
                groundCollider = ReadGround() };
            requireIdle();
            if (ZNetScene.instance != scene || !ReferenceEquals(field.GetValue(scene), registry) || registry.Count != all.Length ||
                Fingerprint(registry.OrderBy(pair => pair.Key).ToArray()) != fingerprint)
                throw new InvalidOperationException("Prefab registry changed during observation.");
            // Each page is an observation, not an atomic inventory across calls or a placement permission.
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(Page)).WriteObject(stream, page);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static Profile Read(int hash, GameObject prefab)
        {
            var piece = prefab.GetComponent<Piece>();
            var wear = prefab.GetComponent<WearNTear>();
            return new Profile { name = prefab.name, registryHash = hash, instanceId = prefab.GetInstanceID(),
                category = (int)piece.m_category, usage = (long)piece.m_usage,
                wearComponents = prefab.GetComponentsInChildren<WearNTear>(true).Length,
                supports = wear ? wear.m_supports : (bool?)null, noSupportWear = wear ? wear.m_noSupportWear : (bool?)null,
                material = wear ? wear.m_materialType.ToString() : null,
                groundPiece = piece.m_groundPiece, groundOnly = piece.m_groundOnly,
                cultivatedOnly = piece.m_cultivatedGroundOnly, vegetationOnly = piece.m_vegetationGroundOnly,
                waterPiece = piece.m_waterPiece, notOnWood = piece.m_notOnWood,
                ceilingOnly = piece.m_inCeilingOnly, notOnFloor = piece.m_notOnFloor, notOnTilt = piece.m_notOnTiltingSurface,
                components = prefab.GetComponents<Component>().Select(component => component ? component.GetType().FullName : "<missing>").ToArray(),
                behaviours = prefab.GetComponentsInChildren<MonoBehaviour>(true).Select(component => component ? component.GetType().FullName : "<missing>")
                    .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                colliders = prefab.GetComponentsInChildren<Collider>(true).Select(component => component ? component.GetType().FullName : "<missing>")
                    .Distinct(StringComparer.Ordinal).OrderBy(name => name, StringComparer.Ordinal).ToArray(),
                colliderDetails = prefab.GetComponentsInChildren<Collider>(true).Select(component => ReadCollider(component, prefab.transform)).ToArray() };
        }

        private static ColliderProfile ReadGround()
        {
            Player player = Player.m_localPlayer;
            Heightmap ground = player ? Heightmap.FindHeightmap(player.transform.position) : null;
            Collider collider = ground ? AccessTools.Field(typeof(Heightmap), "m_collider")?.GetValue(ground) as Collider : null;
            return collider ? ReadCollider(collider, ground.transform) : null;
        }

        private static ColliderProfile ReadCollider(Collider collider, Transform root)
        {
            if (!collider) return new ColliderProfile { type = "<missing>" };
            var path = new Stack<string>();
            for (Transform node = collider.transform; node; node = node.parent)
            {
                path.Push(node.name + "[" + node.GetSiblingIndex() + "]");
                if (node == root) break;
            }
            MeshCollider meshCollider = collider as MeshCollider;
            Mesh mesh = meshCollider ? meshCollider.sharedMesh : null;
            return new ColliderProfile { path = string.Join("/", path), type = collider.GetType().FullName,
                instanceId = collider.GetInstanceID(), layer = collider.gameObject.layer, enabled = collider.enabled,
                isTrigger = collider.isTrigger, activeSelf = collider.gameObject.activeSelf,
                activeInHierarchy = collider.gameObject.activeInHierarchy, attachedRigidbody = collider.attachedRigidbody,
                // This is only a query prerequisite; it does not certify shape/pair support or anchoring.
                staticSolidBranch = BuildWorksPlacementValidation.UsableCollider(collider, root),
                convex = meshCollider ? (bool?)meshCollider.convex : null,
                meshName = mesh ? mesh.name : null, readable = mesh ? (bool?)mesh.isReadable : null,
                vertexCount = mesh ? (int?)mesh.vertexCount : null };
        }

        private static string Fingerprint(KeyValuePair<int, GameObject>[] registry)
        {
            string text = string.Join("\n", registry.Select(pair => pair.Key + ":" + (pair.Value ? pair.Value.GetInstanceID() + ":" + pair.Value.name : "<missing>")));
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "");
        }
    }
}
#endif
