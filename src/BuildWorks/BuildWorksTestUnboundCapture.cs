#if BUILDWORKS_TEST_STAND
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // One separately approved original capture; never starts the foreign plugin.
    internal static class BuildWorksTestUnboundCapture
    {
        private const string AuthorHash = "5EA1913CB4FA96B0A945A5A2E3DCDBAC9225875DE06B164888C8E50757B21FFD";
        private static bool attempted;
        [Serializable] private sealed class Snapshot
        { public int id; public string prefab, zdo, packet; public CompositeBlueprintStore.VectorData position, scale; public CompositeBlueprintStore.QuaternionData rotation; }
        [Serializable] private sealed class Survey
        { public int process, registry, candidateCount, floorsConsidered, overlapRejected; public Snapshot[] candidates; public CompositeBlueprintStore.VectorData player; public string config; }
        [Serializable] private sealed class Result
        {
            public bool passed, inventoryUnchanged, sourceUnchanged, registryUnchanged, configUnchanged, configFolderCreated, importedModelPreserved;
            public int process, entries, statefulCount; public float farthest;
            public string file, sha256, inputInventory, outputInventory; public Snapshot before, after;
        }
        // The native JSON response in QA46440 omitted the candidate array; retain the complete DTO graph.
        private static string Serialize(object value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(value.GetType()).WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }
        private static Player Session()
        {
            var player = BuildWorksTestIsolation.RequireLoadedSession();
            BuildWorksPlugin.RequireQaStorageIdle();
            var control = Resources.FindObjectsOfTypeAll<BuildWorksQaControl>().Single();
            int main = (int)AccessTools.Field(typeof(BuildWorksQaControl), "ownerThread").GetValue(control);
            if (main <= 0 || System.Threading.Thread.CurrentThread.ManagedThreadId != main || !Application.isBatchMode)
                throw new InvalidOperationException("Background QA main thread required.");
            return player;
        }
        private static List<Piece> Registry()
        {
            var list = (List<Piece>)AccessTools.Field(typeof(Piece), "s_allPieces").GetValue(null);
            if (list == null || list.Count > 4096) throw new InvalidOperationException("Missing/oversized live registry.");
            return list;
        }
        // Match original FindPieces: origin cylinder, no collider/owner/category substitutions.
        private static Piece[] Selected(List<Piece> list, Vector3 anchor) => list.Where(piece =>
        {
            if (!piece || piece.gameObject.layer == LayerMask.NameToLayer("ghost")) return false;
            Vector3 delta = piece.transform.position - anchor;
            return new Vector2(delta.x, delta.z).magnitude <= .25f && delta.y >= -.25f && delta.y <= .25f;
        }).ToArray();
        private static Snapshot Read(Piece piece)
        {
            var view = piece ? piece.GetComponent<ZNetView>() : null;
            var zdo = view && view.IsValid() ? view.GetZDO() : null;
            var prefab = zdo != null ? ZNetScene.instance.GetPrefab(zdo.GetPrefab()) : null;
            if (!piece || !piece.gameObject.activeInHierarchy || zdo == null || !zdo.IsValid() || !prefab ||
                prefab.name != "wood_floor" || piece.gameObject.name != prefab.name + "(Clone)" || piece.GetComponent<Container>())
                throw new InvalidOperationException("An inspected ordinary native floor is required.");
            var tr = piece.transform; var packet = new ZPackage(); zdo.Serialize(packet);
            var bytes = packet.GetArray();
            if (bytes.Length > 32768 || tr.localScale.x <= 0 || tr.localScale.y <= 0 || tr.localScale.z <= 0 ||
                new[] { tr.rotation.x, tr.rotation.y, tr.rotation.z, tr.rotation.w }.Any(v => float.IsNaN(v) || float.IsInfinity(v)) ||
                new[] { tr.position, tr.localScale }.Any(v =>
                float.IsNaN(v.x) || float.IsInfinity(v.x) || float.IsNaN(v.y) || float.IsInfinity(v.y) || float.IsNaN(v.z) || float.IsInfinity(v.z)))
                throw new InvalidOperationException("Invalid/oversized native source.");
            return new Snapshot { id = piece.gameObject.GetInstanceID(), prefab = prefab.name, zdo = zdo.m_uid.ToString(),
                packet = Convert.ToBase64String(bytes), position = new CompositeBlueprintStore.VectorData(tr.position),
                rotation = new CompositeBlueprintStore.QuaternionData { x = tr.rotation.x, y = tr.rotation.y, z = tr.rotation.z, w = tr.rotation.w },
                scale = new CompositeBlueprintStore.VectorData(tr.localScale) };
        }
        public static string Inspect()
        {
            var player = Session(); var list = Registry(); var candidates = new List<Snapshot>();
            int floorsConsidered = 0, overlapRejected = 0;
            foreach (var piece in list)
            {
                if (!piece || !piece.name.StartsWith("wood_floor(", StringComparison.Ordinal) ||
                    Vector3.Distance(piece.transform.position, player.transform.position) > 64) continue;
                ++floorsConsidered;
                var selected = Selected(list, piece.transform.position);
                if (selected.Length != 1 || selected[0] != piece) { ++overlapRejected; continue; }
                candidates.Add(Read(piece)); if (candidates.Count == 8) break;
            }
            return Serialize(new Survey { process = System.Diagnostics.Process.GetCurrentProcess().Id,
                registry = list.Count, candidateCount = candidates.Count, floorsConsidered = floorsConsidered, overlapRejected = overlapRejected,
                candidates = candidates.ToArray(), player = new CompositeBlueprintStore.VectorData(player.transform.position), config = Paths.ConfigPath });
        }
        public static string Capture(int pieceId, int processId, string run)
        {
            var player = Session();
            if (attempted || processId != System.Diagnostics.Process.GetCurrentProcess().Id ||
                run == null || !Regex.IsMatch(run, "\\A[a-f0-9]{16}\\z")) throw new InvalidOperationException("Fresh one-shot request required.");
            if (AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "Unbound"))
                throw new InvalidOperationException("Author assembly must not be loaded yet.");
            var config = CompositeBlueprintStore.ValidateExchangeDirectory(Paths.ConfigPath);
            if (new DirectoryInfo(config).Parent?.Parent?.Name != "TerrainRamp-1.0-Test") throw new InvalidOperationException("Wrong profile.");
            string workspace = Path.GetFullPath(Path.Combine(config, "..", "..", "..", "Default", "BuildWorks"));
            string dll = Path.Combine(workspace, "artifacts", "point-2.19-unbound-20261031", "package", "BepInEx", "plugins", "Unbound.dll");
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(dll));
            if ((File.GetAttributes(dll) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked DLL refused.");
            string folder = CompositeBlueprintStore.ValidateExchangeDirectory(Path.Combine(workspace, "artifacts", "ubw", run));
            string output = Path.Combine(folder, "capture.vtbp"); string name = Path.Combine(folder, "capture");
            string authorFolder = Path.Combine(config, "Unbound_blueprints"); bool hadFolder = Directory.Exists(authorFolder);
            // Check the actual game's Mono Path.Combine before any foreign load; ConfigPath is never redirected.
            if (output.Length >= 260 || !Path.IsPathRooted(name) || Path.GetFullPath(Path.Combine(authorFolder, name + ".vtbp")) != output ||
                Directory.Exists(folder) || File.Exists(folder) || File.Exists(output)) throw new IOException("Fresh bounded absolute output required.");
            if (hadFolder) CompositeBlueprintStore.ValidateExchangeDirectory(authorFolder);
            var list = Registry(); var registryIds = list.Select(p => p ? p.GetInstanceID() : 0).ToArray();
            var go = Resources.InstanceIDToObject(pieceId) as GameObject; var piece = go ? go.GetComponent<Piece>() : null;
            var before = Read(piece);
            if (Vector3.Distance(player.transform.position, before.position.ToVector3()) > 64 || Selected(list, before.position.ToVector3()).Length != 1 ||
                Selected(list, before.position.ToVector3())[0] != piece) throw new InvalidOperationException("Selection no longer matches inspected single floor.");
            var inventory = new ZPackage(); player.GetInventory().Save(inventory); string originalInventory = Convert.ToBase64String(inventory.GetArray());
            Vector3 playerPosition = player.transform.position; Quaternion playerRotation = player.transform.rotation;
            Directory.CreateDirectory(folder);
            using (var marker = new FileStream(Path.Combine(folder, "attempt.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(marker)) { writer.Write(processId.ToString()); writer.Flush(); marker.Flush(true); }
            File.WriteAllText(Path.Combine(folder, "before.json"), Serialize(new Result { process = processId,
                before = before, inputInventory = originalInventory, file = output }));
            attempted = true;
            using (var held = new FileStream(dll, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var hash = SHA256.Create())
            {
                if (BitConverter.ToString(hash.ComputeHash(held)).Replace("-", "") != AuthorHash) throw new InvalidDataException("Author hash changed.");
                var author = Assembly.LoadFile(dll);
                if (author.ManifestModule.ModuleVersionId.ToString() != "4f0c19dc-5b92-4b3b-a2f3-eabc762d7ab3") throw new InvalidDataException("Author MVID changed.");
                var type = author.GetType("ValheimTweaks.Blueprint", true);
                var capture = type.GetMethod("Capture", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null,
                    new[] { typeof(string), typeof(Vector3), typeof(float), typeof(float), typeof(float), typeof(float), typeof(int).MakeByRefType(), typeof(float).MakeByRefType() }, null);
                var save = type.GetMethod("Save", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
                if (type.TypeInitializer != null || capture == null || save == null || capture.ReturnType != type || save.ReturnType != typeof(void))
                    throw new InvalidOperationException("Reviewed author contract required.");
                object[] args = { name, before.position.ToVector3(), 0f, .25f, .25f, .25f, 0, 0f };
                object blueprint = capture.Invoke(null, args);
                var entries = blueprint == null ? null : type.GetField("Entries", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(blueprint) as System.Collections.IList;
                if (entries == null || entries.Count != 1 || (int)args[6] != 0 || (float)args[7] != 0)
                    throw new InvalidDataException("Original Capture returned an unexpected selection; Save refused.");
                save.Invoke(blueprint, null);
                if (!File.Exists(output) || new FileInfo(output).Length > 65536 || (File.GetAttributes(output) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("Missing/invalid capture output.");
                var data = Geometry.UnboundBlueprint.Parse(File.ReadAllText(output)); var after = Read(piece);
                var afterInventory = new ZPackage(); player.GetInventory().Save(afterInventory);
                var result = new Result { process = processId, entries = data.Pieces.Count, statefulCount = (int)args[6], farthest = (float)args[7],
                    before = before, after = after, file = output, inputInventory = originalInventory,
                    outputInventory = Convert.ToBase64String(afterInventory.GetArray()), configFolderCreated = !hadFolder && Directory.Exists(authorFolder),
                    sourceUnchanged = Serialize(before) == Serialize(after), registryUnchanged = registryIds.SequenceEqual(Registry().Select(p => p ? p.GetInstanceID() : 0)),
                    configUnchanged = config == Paths.ConfigPath };
                result.inventoryUnchanged = result.inputInventory == result.outputInventory;
                var bytes = File.ReadAllBytes(output);
                var imported = ExternalBlueprintLibrary.LoadFile(output).Single();
                result.importedModelPreserved = imported.importError == null && imported.parts.Count == 1 && imported.parts[0].prefabName == before.prefab &&
                    imported.sourceDocuments.Count == 1 && imported.sourceDocuments[0].ToGeometry().OriginalBytes().SequenceEqual(bytes);
                result.sha256 = BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "");
                result.passed = result.entries == 1 && result.statefulCount == 0 && result.farthest == 0 && data.TerrainSize == 0 &&
                    data.Pieces[0].PrefabName == before.prefab && data.Pieces[0].ZdoData == before.packet &&
                    data.Pieces[0].Position.LengthSquared <= .000000000001 && Quaternion.Angle(before.rotation.ToQuaternion(), new Quaternion((float)data.Pieces[0].Rotation.X, (float)data.Pieces[0].Rotation.Y, (float)data.Pieces[0].Rotation.Z, (float)data.Pieces[0].Rotation.W)) < .001f &&
                    result.sourceUnchanged && result.registryUnchanged && result.configUnchanged && result.inventoryUnchanged && result.importedModelPreserved &&
                    playerPosition.Equals(player.transform.position) && playerRotation.Equals(player.transform.rotation) &&
                    !Resources.FindObjectsOfTypeAll(typeof(BaseUnityPlugin)).Any(p => p.GetType().Assembly == author);
                string report = Serialize(result); File.WriteAllText(Path.Combine(folder, "result.json"), report);
                if (!result.passed) throw new InvalidDataException("Capture comparison failed; evidence retained in " + folder);
                return report;
            }
        }
    }
}
#endif
