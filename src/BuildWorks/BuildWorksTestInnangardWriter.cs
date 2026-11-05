#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // Requires separate owner approval; one author-Write attempt, no foreign plugin lifecycle.
    internal static class BuildWorksTestInnangardWriter
    {
        private const string AuthorHash = "8685ED6E92BF3369A34D4204EBA45815BFDBECC46A3A25524F8723126B0DAA68";
        private const string AuthorMvid = "fdd5ec0b-724c-4adb-b703-1c0c1cec217d";
        private static bool attempted;

        public static string Capture(string assemblyPath, string name, int[] pieceIds, int processId)
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            if (attempted || System.Diagnostics.Process.GetCurrentProcess().Id != processId ||
                pieceIds == null || pieceIds.Length != 3 || pieceIds.Distinct().Count() != 3 ||
                name == null || !Regex.IsMatch(name, "\\Abw_in218_writer_[a-f0-9]{16}\\z"))
                throw new InvalidOperationException("One fresh-process capture of three inspected pieces is required.");
            NoPlugin();
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Innangard"))
                throw new InvalidOperationException("Author assembly must not already be loaded.");
            string expectedDll = Path.GetFullPath(Path.Combine(Paths.ConfigPath, "..", "..", "..", "Default",
                "BuildWorks", "artifacts", "in218", "Innangard037.dll"));
            string dll = Path.GetFullPath(assemblyPath);
            if (!string.Equals(dll, expectedDll, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Author DLL must be the separately pinned private input.");
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(dll));
            if ((File.GetAttributes(dll) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("Linked author DLL refused.");
            var jotunn = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "Jotunn");
            if (jotunn.ManifestModule.ModuleVersionId.ToString() != "90c424ee-ad1c-4440-8dc8-8bd2adad75b1" ||
                Hash(File.ReadAllBytes(jotunn.Location)) != "9509221046842A70F94B98BAB4CB41005DEE026823A7A79345829ED46A30F796")
                throw new InvalidOperationException("Reviewed Jotunn binding is required.");
            string directory = CompositeBlueprintStore.ValidateExchangeDirectory(Path.Combine(Paths.ConfigPath, "Innangard", "blueprints"));
            string output = Path.Combine(directory, name + ".blueprint");
            if (File.Exists(output) || Directory.Exists(output)) throw new IOException("Capture output already exists.");
            var pieces = pieceIds.Select(id =>
            {
                var go = Resources.InstanceIDToObject(id) as GameObject;
                var piece = go ? go.GetComponent<Piece>() : null;
                var view = piece ? piece.GetComponent<ZNetView>() : null;
                var zdo = view ? view.GetZDO() : null;
                var prefab = zdo != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
                if (!piece || !go.activeInHierarchy || !view || zdo == null || !zdo.IsValid() || !prefab ||
                    (prefab.name != "woodwall" && prefab.name != "wood_floor") ||
                    go.name != prefab.name + "(Clone)" || Vector3.Distance(player.transform.position, piece.transform.position) > 5)
                    throw new InvalidOperationException("An inspected nearby native wall/floor is required.");
                return piece;
            }).ToList();
            var original = pieces.Select(ReadPiece).ToArray();
            if (original.Count(p => p.prefab == "woodwall") != 1 || original.Count(p => p.prefab == "wood_floor") != 2)
                throw new InvalidOperationException("This capture requires one wall and two floors.");
            var before = State(player, original);
            using (var heldDll = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (Hash(heldDll) != AuthorHash) throw new InvalidDataException("Author DLL SHA differs.");
                attempted = true;
                var author = Assembly.LoadFile(dll);
                if (author.GetName().Name != "Innangard" || author.GetName().Version.ToString() != "0.3.7.0" ||
                    author.ManifestModule.ModuleVersionId.ToString() != AuthorMvid)
                    throw new InvalidDataException("Loaded author identity differs.");
                var type = author.GetType("Innangard.Blueprints.BlueprintCaptureCommand", true);
                var write = type.GetMethod("Write", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(string), typeof(List<Piece>), typeof(Player) }, null);
                if (type.TypeInitializer != null || write == null || !write.IsPrivate || write.ReturnType != typeof(string))
                    throw new InvalidOperationException("Reviewed original Write signature is required.");
                string result = write.Invoke(null, new object[] { name, pieces, player }) as string;
                if (result == null || !string.Equals(Path.GetFullPath(result), output, StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(output) || new FileInfo(output).Length > 65536)
                    throw new InvalidDataException("Original Write did not return the expected bounded file.");
                CompositeBlueprintStore.ValidateExchangeDirectory(directory);
                if ((File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Linked capture output refused.");
                byte[] bytes = File.ReadAllBytes(output);
                var raw = ExternalBlueprintLibrary.LoadFile(output).Single();
                if (raw.importError != null || raw.parts.Count != 3 || !raw.sourceDocuments.Single().ToGeometry().OriginalBytes().SequenceEqual(bytes))
                    throw new InvalidDataException("Captured source rows/bytes differ.");
                Vector3 origin = Vector3.zero;
                foreach (var piece in original) origin += piece.position.ToVector3();
                origin = new Vector3(origin.x / 3, original.Min(p => p.position.y), origin.z / 3);
                float yaw = Mathf.Round(player.transform.rotation.eulerAngles.y / 22.5f) * 22.5f;
                Quaternion inverse = Quaternion.Inverse(Quaternion.Euler(0, yaw, 0));
                float positionError = 0, axisError = 0, scaleError = 0;
                for (int i = 0; i < 3; ++i)
                {
                    var row = raw.parts[i]; var source = original[i];
                    if (row.prefabName != source.prefab) throw new InvalidDataException("Original writer prefab order differs.");
                    positionError = Mathf.Max(positionError, Vector3.Distance(row.position.ToVector3(), inverse * (source.position.ToVector3() - origin)));
                    Quaternion rotation = new Quaternion(row.rotation.x, row.rotation.y, row.rotation.z, row.rotation.w);
                    foreach (var axis in new[] { Vector3.right, Vector3.up, Vector3.forward })
                        axisError = Mathf.Max(axisError, Vector3.Distance(rotation * axis, inverse *
                            new Quaternion(source.rotation.x, source.rotation.y, source.rotation.z, source.rotation.w) * axis));
                    scaleError = Mathf.Max(scaleError, Vector3.Distance(row.scale.ToVector3(), source.scale.ToVector3()));
                }
                if (new[] { positionError, axisError, scaleError }.Any(v => float.IsNaN(v) || float.IsInfinity(v) || v > .0001f))
                    throw new InvalidDataException("Author output differs from inspected live transforms.");
                string native = BuildWorksTestPlannerBlueprint.CheckNativeSource(output);
                NoPlugin();
                var afterPieces = pieces.Select(ReadPiece).ToArray();
                var after = State(player, afterPieces);
                if (Resources.FindObjectsOfTypeAll(typeof(BaseUnityPlugin)).Any(p => p.GetType().FullName == "Innangard.InnangardPlugin") ||
                    Hash(heldDll) != AuthorHash ||
                    !File.ReadAllBytes(output).SequenceEqual(bytes) || !SameState(before, after))
                    throw new InvalidOperationException("Writer/profile/selected-piece/runtime state changed; retain evidence and stop.");
                return Serialize(new Result { path = output, sourceSha256 = Hash(bytes), sourceBytes = bytes.Length,
                    sourceParts = original, sourceOrigin = new CompositeBlueprintStore.VectorData(origin),
                    stateBefore = before, stateAfter = after, sourceYaw = yaw, positionError = positionError,
                    axisError = axisError, scaleError = scaleError, nativeSourceProfile = native, inventoryRecipesRegistryCountFrameUnchanged = true,
                    authorPluginRegistered = false, authorPluginComponents = 0,
                    provenance = "Original Innangard0.3.7 private Write invoked once on live Piece/Player refs;not console/UI/plugin startup" });
            }
        }

        private static void NoPlugin()
        {
            if (Chainloader.PluginInfos.ContainsKey("com.yololo.innangard"))
                throw new InvalidOperationException("Full Innangard plugin must not be installed/started for writer-only QA.");
        }

        private static PieceState ReadPiece(Piece piece)
        {
            var zdo = piece.GetComponent<ZNetView>().GetZDO();
            var tr = piece.transform;
            var rotation = tr.rotation;
            if (!zdo.IsValid() || new[] { rotation.x, rotation.y, rotation.z, rotation.w }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
                rotation.x * rotation.x + rotation.y * rotation.y + rotation.z * rotation.z + rotation.w * rotation.w < .000001f ||
                tr.localScale.x == 0 || tr.localScale.y == 0 || tr.localScale.z == 0 || new[] { tr.position, tr.localScale }.Any(v =>
                float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)))
                throw new InvalidDataException("Invalid live source transform.");
            return new PieceState { id = piece.gameObject.GetInstanceID(), prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab()).name,
                zdo = zdo.m_uid.ToString(), position = new CompositeBlueprintStore.VectorData(tr.position),
                rotation = RawRotation(tr.rotation), scale = new CompositeBlueprintStore.VectorData(tr.localScale) };
        }

        private static RuntimeState State(Player player, PieceState[] pieces)
        {
            var inventory = new ZPackage(); player.GetInventory().Save(inventory);
            var recipes = (HashSet<string>)AccessTools.Field(typeof(Player), "m_knownRecipes").GetValue(player);
            var registry = (Dictionary<int, GameObject>)AccessTools.Field(typeof(ZNetScene), "m_namedPrefabs").GetValue(ZNetScene.instance);
            return new RuntimeState { pieces = pieces, inventory = Convert.ToBase64String(inventory.GetArray()),
                recipes = recipes.OrderBy(s => s, StringComparer.Ordinal).ToArray(),
                registry = registry.OrderBy(p => p.Key).Select(p => p.Key + ":" + p.Value.GetInstanceID() + ":" + p.Value.name).ToArray(),
                worldObjects = ZDOMan.instance.NrOfObjects(), playerId = player.GetPlayerID(), yaw = player.transform.rotation.eulerAngles.y,
                playerPosition = new CompositeBlueprintStore.VectorData(player.transform.position),
                playerRotation = RawRotation(player.transform.rotation), frame = Time.frameCount };
        }

        private static bool SameState(RuntimeState a, RuntimeState b) =>
            a.inventory == b.inventory && a.recipes.SequenceEqual(b.recipes) && a.registry.SequenceEqual(b.registry) &&
            a.worldObjects == b.worldObjects && a.frame == b.frame && a.playerId == b.playerId && a.yaw.Equals(b.yaw) &&
            a.playerPosition.Equals(b.playerPosition) && a.playerRotation.Equals(b.playerRotation) &&
            a.pieces.Length == b.pieces.Length && !a.pieces.Where((p, i) =>
                p.id != b.pieces[i].id || p.prefab != b.pieces[i].prefab || p.zdo != b.pieces[i].zdo ||
                !p.position.Equals(b.pieces[i].position) || !p.rotation.Equals(b.pieces[i].rotation) ||
                !p.scale.Equals(b.pieces[i].scale)).Any();

        // QuaternionData's constructor normalizes; preservation checks need the exact native components.
        private static CompositeBlueprintStore.QuaternionData RawRotation(Quaternion q) =>
            new CompositeBlueprintStore.QuaternionData { x = q.x, y = q.y, z = q.z, w = q.w };

        // The native JSON report omitted PieceState[]; snapshots must retain every array member.
        private static string Serialize(object value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static string Hash(Stream stream) { stream.Position = 0; using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", ""); }

        [Serializable] private sealed class PieceState
        { public int id; public string prefab, zdo; public CompositeBlueprintStore.VectorData position, scale;
            public CompositeBlueprintStore.QuaternionData rotation; }
        [Serializable] private sealed class RuntimeState
        { public PieceState[] pieces; public string inventory; public string[] recipes, registry; public int worldObjects, frame;
            public long playerId; public float yaw; public CompositeBlueprintStore.VectorData playerPosition;
            public CompositeBlueprintStore.QuaternionData playerRotation; }
        [Serializable] private sealed class Result
        {
            public string path, sourceSha256, nativeSourceProfile, provenance;
            public int sourceBytes, authorPluginComponents;
            public PieceState[] sourceParts; public CompositeBlueprintStore.VectorData sourceOrigin;
            public RuntimeState stateBefore, stateAfter; public float sourceYaw, positionError, axisError, scaleError;
            public bool inventoryRecipesRegistryCountFrameUnchanged, authorPluginRegistered;
        }
    }
}
#endif
