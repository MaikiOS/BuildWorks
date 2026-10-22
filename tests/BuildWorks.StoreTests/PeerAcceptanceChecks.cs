using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerAcceptanceChecks
    {
        private static int checks;
        internal static void Run(string folder)
        {
            checks = 0; Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "library.json"); var store = new B(path);
            var part = new B.Part { stableId = "a", prefabName = "woodwall", displayName = "Стена", position = new B.VectorData(),
                rotation = new B.QuaternionData { w = 1 }, scale = new B.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true, signText = "Дом Ostrix" };
            Require(store.TrySaveDocument("Источник", "QA", new[] { part }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "a", null, out var source, out string error), error);
            byte[] portable = BlueprintExchangeFormat.Encode(source);
            var origin = new B.PeerOrigin { server = "server", worldUid = 71, sender = "Steam_sender", recipient = "Steam_receiver",
                offerId = Guid.NewGuid().ToString("N"), documentId = source.id, sha256 = Hash(portable), formatVersion = BlueprintExchangeFormat.Version };
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out error), error);
            string saved = File.ReadAllText(path); int revision = store.Revision;
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var replay, out error), error);
            Require(replay.id == copy.id && store.All().Count == 2 && store.Revision == revision && File.ReadAllText(path) == saved,
                "Repeated acceptance created a second card or wrote the library.");
            var fresh = new B(path);
            Require(fresh.TrySaveAcceptedPeerCopy(portable, origin, out replay, out error) && replay.id == copy.id && fresh.All().Count == 2,
                "Fresh-process acceptance recreated the same offer.");
            Require(store.TryRename(copy, "Изменённая копия", out error), error);
            Require(store.TryUpdateDocument(copy, copy.name, copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId,
                copy.primaryGroupId, copy.supports, copy.placementSupportId, out error, peerOrigins: Array.Empty<B.PeerOrigin>()), error);
            fresh = new B(path); saved = File.ReadAllText(path);
            Require(fresh.TrySaveAcceptedPeerCopy(portable, origin, out replay, out error) && replay.id == copy.id &&
                replay.name == "Изменённая копия" && replay.peerOrigins.Count == 0 && File.ReadAllText(path) == saved,
                "Edited or cleared provenance lost the durable acceptance binding.");
            var changed = origin.Copy(); changed.sender = "Different sender"; Refuse(store, path, portable, changed);
            changed = origin.Copy(); changed.recipient = "Different recipient"; Refuse(store, path, portable, changed);
            var changedSource = source.ImportCopy(); changedSource.name = "Changed source";
            byte[] different = BlueprintExchangeFormat.Encode(changedSource);
            changed = origin.Copy(); changed.sha256 = Hash(different); Refuse(store, path, different, changed);
            changedSource.id = Guid.NewGuid().ToString("N"); different = BlueprintExchangeFormat.Encode(changedSource);
            changed = origin.Copy(); changed.documentId = changedSource.id; changed.sha256 = Hash(different); Refuse(store, path, different, changed);
            changed = origin.Copy(); changed.formatVersion--; Refuse(store, path, portable, changed);
            byte[] legacyPortable = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(portable).Replace("\"version\":15", "\"version\":14"));
            changed = origin.Copy(); changed.formatVersion = 14; changed.sha256 = Hash(legacyPortable); Refuse(store, path, legacyPortable, changed);
            Refuse(store, path, portable.Take(portable.Length - 1).ToArray(), origin);
            Refuse(store, path, Array.Empty<byte>(), origin);
            var second = origin.Copy(); second.offerId = Guid.NewGuid().ToString("N");
            Require(store.TrySaveAcceptedPeerCopy(portable, second, out var another, out error) && another.id != copy.id && store.All().Count == 3,
                "A distinct accepted offer did not create its own copy.");
            var library = ReadLibrary(path);
            Require(library.peerReceipts.Count == 2 && library.peerReceipts[0].copyId == copy.id &&
                library.peerReceipts[0].origin.ToGeometry().SameValue(origin.ToGeometry()), "Persisted acceptance differs from exact origin/copy ID.");
            origin.sender = "Caller mutation";
            Require(ReadLibrary(path).peerReceipts[0].origin.sender == "Steam_sender", "Receipt aliases caller provenance.");
            origin.sender = "Steam_sender";
            Require(!Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(another)).Contains("peerReceipts") &&
                !File.ReadAllText(Path.Combine(store.ShareDirectory, another.id + ".buildworks.json")).Contains("peerReceipts"),
                "Private acceptance receipts leaked into portable/share.");

            SchemaChecks(folder, path, portable, origin);
            CapacityChecks(folder, path, portable, origin);
            RollbackChecks(folder, portable, origin);
            ScopeChecks(folder, portable, origin);

            Require(store.TrySaveDocument("Save As", copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId,
                copy.primaryGroupId, copy.supports, copy.placementSupportId, out var saveAs, out error, copy.customSnapPoints, copy), error);
            Require(store.TryDelete(copy, out error), error);
            fresh = new B(path); saved = File.ReadAllText(path); revision = fresh.Revision;
            Require(!fresh.TrySaveAcceptedPeerCopy(portable, origin, out replay, out error) && replay == null && error.Contains("peer.accepted_deleted") &&
                fresh.All().Any(value => value.id == saveAs.id) && fresh.All().Count == 3 && fresh.Revision == revision && File.ReadAllText(path) == saved,
                "Deleted accepted copy was recreated or replaced by SaveAs.");
            Require(ReadLibrary(path).peerReceipts.Count == 2 && Hash(portable) == Hash(BlueprintExchangeFormat.Encode(source)),
                "Deletion erased acceptance history or changed source bytes.");
            Console.WriteLine("PASS durable peer acceptance: " + checks + " assertions; local file scope, no sender authentication or network/UI proof.");
        }

        private static void SchemaChecks(string folder, string path, byte[] portable, B.PeerOrigin origin)
        {
            string json = File.ReadAllText(path);
            string without = json.Substring(0, json.IndexOf(",\"peerReceipts\":", StringComparison.Ordinal)) + "}";
            string legacyPath = Path.Combine(folder, "legacy26.json");
            File.WriteAllText(legacyPath, without.Replace("\"version\":27", "\"version\":26"));
            var legacy = new B(legacyPath);
            Require(legacy.All().Count == 3 && legacy.TryAddCategory("Legacy", out string error) &&
                File.ReadAllText(legacyPath).Contains("\"version\":27"), "Native26 without local receipts did not migrate.");
            foreach (string value in new[] { "null", "[]" })
                RefuseLibrary(folder, without.Replace("\"version\":27", "\"version\":26").TrimEnd('}') + ",\"peerReceipts\":" + value + "}", portable, origin);
            RefuseLibrary(folder, json.Replace("\"version\":27", "\"version\":26"), portable, origin);
            RefuseLibrary(folder, json.Replace("\"version\":27", "\"version\":28"), portable, origin);
            RefuseLibrary(folder, json.Replace("\"peerReceipts\":[", "\"peerReceipts\":[null,"), portable, origin);
            RefuseLibrary(folder, json.Replace("\"origin\":{", "\"origin\":{\"admin\":true,"), portable, origin);
            RefuseLibrary(folder, json.Replace("\"copyId\":", "\"unexpected\":"), portable, origin);
            RefuseLibrary(folder, json.Replace("\"copyId\":\"", "\"copyId\":\"bad"), portable, origin);
            var data = ReadLibrary(path);
            data.peerReceipts.Add(new B.PeerReceipt { origin = data.peerReceipts[0].origin.Copy(), copyId = Guid.NewGuid().ToString("N") });
            RefuseLibrary(folder, Serialize(data), portable, origin);
            data = ReadLibrary(path); data.peerReceipts[1].copyId = data.peerReceipts[0].copyId;
            RefuseLibrary(folder, Serialize(data), portable, origin);
            data = ReadLibrary(path); data.peerReceipts[0].origin = null;
            RefuseLibrary(folder, Serialize(data), portable, origin);
            // An invalid card must also block the otherwise exact duplicate-read branch.
            data = ReadLibrary(path); data.blueprints[0].parts[0].prefabName = "";
            RefuseLibrary(folder, Serialize(data), portable, origin, expectEmpty: false);
        }
        private static void CapacityChecks(string folder, string path, byte[] portable, B.PeerOrigin origin)
        {
            var data = ReadLibrary(path);
            while (data.peerReceipts.Count < B.MaximumPeerReceipts)
            {
                var entry = origin.Copy(); entry.offerId = Guid.NewGuid().ToString("N");
                data.peerReceipts.Add(new B.PeerReceipt { origin = entry, copyId = Guid.NewGuid().ToString("N") });
            }
            string fullPath = Path.Combine(folder, "full-receipts.json"); string json = Serialize(data); File.WriteAllText(fullPath, json);
            var store = new B(fullPath);
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out string error) && copy.id == data.peerReceipts[0].copyId &&
                store.Revision == 0 && File.ReadAllText(fullPath) == json, "Full ledger refused or rewrote an already accepted offer.");
            var next = origin.Copy(); next.offerId = Guid.NewGuid().ToString("N");
            Refuse(store, fullPath, portable, next, "peer.receipt_limit");
            data.peerReceipts.Add(new B.PeerReceipt { origin = next, copyId = Guid.NewGuid().ToString("N") });
            RefuseLibrary(folder, Serialize(data), portable, origin);
            data = ReadLibrary(path);
            while (data.blueprints.Count < 32)
            { var extra = data.blueprints[0].ImportCopy(); extra.id = Guid.NewGuid().ToString("N"); data.blueprints.Add(extra); }
            string fullCatalog = Path.Combine(folder, "full-catalogue.json"); json = Serialize(data); File.WriteAllText(fullCatalog, json);
            store = new B(fullCatalog);
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out copy, out error) && copy.id == data.peerReceipts[0].copyId &&
                store.Revision == 0 && File.ReadAllText(fullCatalog) == json, "Full catalogue refused or rewrote an existing acceptance.");
        }
        private static void RollbackChecks(string folder, byte[] portable, B.PeerOrigin origin)
        {
            string path = Path.Combine(folder, "rollback", "library.json"); Directory.CreateDirectory(Path.GetDirectoryName(path));
            var store = new B(path); Directory.CreateDirectory(path + ".tmp");
            Require(!store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out _) && copy == null && store.All().Count == 0 &&
                store.Revision == 0 && !File.Exists(path), "Failed write retained a new card/receipt.");
            Directory.Delete(path + ".tmp");
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out copy, out string error), error);
            Require(ReadLibrary(path).peerReceipts.Count == 1 && new B(path).All().Count == 1, "Retry after failed write lost its receipt/card.");
            string saved = File.ReadAllText(path); var next = origin.Copy(); next.offerId = Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(path + ".tmp");
            Refuse(store, path, portable, next);
            Directory.Delete(path + ".tmp");
            Require(File.ReadAllText(path) == saved && store.TrySaveAcceptedPeerCopy(portable, next, out copy, out error) &&
                ReadLibrary(path).peerReceipts.Count == 2, "Existing receipt list rollback consumed a failed offer.");
        }
        private static void ScopeChecks(string folder, byte[] portable, B.PeerOrigin origin)
        {
            var store = new B(Path.Combine(folder, "domains", "library.json"));
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var first, out string error), error);
            var other = origin.Copy(); other.server = "other server";
            Require(store.TrySaveAcceptedPeerCopy(portable, other, out var second, out error) && second.id != first.id, "Another server was treated as a replay.");
            other = origin.Copy(); other.worldUid++;
            Require(store.TrySaveAcceptedPeerCopy(portable, other, out var third, out error) && third.id != first.id && third.id != second.id,
                "Another world was treated as a replay.");
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var replay, out error) && replay.id == first.id && store.All().Count == 3,
                "Server/world/offer key did not preserve the original acceptance.");
        }
        private static void Refuse(B store, string path, byte[] portable, B.PeerOrigin origin, string expected = null)
        {
            string saved = File.ReadAllText(path); int count = store.All().Count, revision = store.Revision;
            Require(!store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out string error) && copy == null &&
                (expected == null || error.Contains(expected)) && store.All().Count == count && store.Revision == revision && File.ReadAllText(path) == saved,
                "Invalid/conflicting offer changed a card, receipt or file.");
        }
        private static void RefuseLibrary(string folder, string json, byte[] portable, B.PeerOrigin origin, bool expectEmpty = true)
        {
            string path = Path.Combine(folder, "invalid-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, json);
            var store = new B(path);
            Require((!expectEmpty || store.All().Count == 0) && !store.TryAddCategory("Blocked", out _) &&
                !store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out _) && copy == null && File.ReadAllText(path) == json,
                "Invalid receipt/schema or corrupt library accepted a replay or rewrote the file.");
        }
        private static B.LibraryData ReadLibrary(string path)
        { using (var stream = File.OpenRead(path)) return (B.LibraryData)new DataContractJsonSerializer(typeof(B.LibraryData)).ReadObject(stream); }
        private static string Serialize(B.LibraryData data)
        { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(B.LibraryData)).WriteObject(stream, data); return Encoding.UTF8.GetString(stream.ToArray()); } }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Require(bool value, string error) { checks++; if (!value) throw new Exception(error); }
    }
}
