using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PeerOriginChecks
    {
        private static int checks;
        internal static void Run(string folder)
        {
            checks = 0; Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "library.json"); var store = new B(path);
            var part = new B.Part { stableId = "a", prefabName = "woodwall", displayName = "Стена", position = new B.VectorData(),
                rotation = new B.QuaternionData { w = 1 }, scale = new B.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true, signText = "Дом\nOstrix" };
            Require(store.TrySaveDocument("Источник", "QA", new[] { part }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "a", null, out var source, out string error), error);
            byte[] portable = BlueprintExchangeFormat.Encode(source);
            var origin = new B.PeerOrigin { server = "server", worldUid = 71, sender = "Steam_sender", recipient = "Steam_receiver",
                offerId = Guid.NewGuid().ToString("N"), documentId = source.id, sha256 = Hash(portable), formatVersion = 15 };
            Require(store.TrySaveAcceptedPeerCopy(portable, origin, out var copy, out error), error);
            Require(copy.id != source.id && copy.parts[0].signText == part.signText && copy.peerOrigins.Count == 1 && source.peerOrigins == null,
                "Acceptance lost supported data, reused ID or changed source.");
            origin.sender = "Changed caller";
            Require(copy.peerOrigins[0].sender == "Steam_sender", "Accepted provenance aliases caller.");
            var cloned = copy.ImportCopy(); cloned.peerOrigins[0].sender = "changed clone";
            Require(copy.peerOrigins[0].sender == "Steam_sender", "Import copy aliases mutable provenance.");
            Require(new B(path).All().Single(value => value.id == copy.id).peerOrigins[0].sha256 == Hash(portable), "Native reload lost peer origin.");
            Require(File.ReadAllText(Path.Combine(store.ShareDirectory, copy.id + ".buildworks.json")).Contains("peerOrigins"), "Share file lost peer origin.");
            string accepted = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(copy));
            Require(BlueprintExchangeFormat.Decode(accepted).peerOrigins[0].recipient == "Steam_receiver", "Portable reload lost recipient.");
            Require(store.TryUpdateDocument(copy, "Edited", copy.category, copy.parts, copy.groups, copy.anchors,
                copy.primaryPartId, copy.primaryGroupId, out error, copy.customSnapPoints) && copy.peerOrigins.Count == 1, error);
            Require(store.TrySaveDocument("Save As", copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId, copy.primaryGroupId,
                copy.supports, copy.placementSupportId, out var saveAs, out error, copy.customSnapPoints, copy) && saveAs.id != copy.id && saveAs.peerOrigins.Count == 1, error);
            Require(ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.Parse(accepted, "peer.buildworks.json")).peerOrigins[0].sha256 == Hash(portable), "External detached copy lost peer origin.");
            Require(PlanBuildExport.Review(copy, _ => new B.VectorData { x = 1, y = 1, z = 1 }).PeerOrigins == 1, "Foreign export does not report omitted peer history.");
            int count = store.All().Count;
            var bad = copy.peerOrigins[0].Copy(); bad.sha256 = new string('F', 64);
            Require(!store.TrySaveAcceptedPeerCopy(portable, bad, out _, out _) && store.All().Count == count, "Wrong source hash created a card.");
            bad = copy.peerOrigins[0].Copy(); bad.documentId = Guid.NewGuid().ToString("N");
            Require(!store.TrySaveAcceptedPeerCopy(portable, bad, out _, out _) && store.All().Count == count, "Wrong source ID created a card.");
            Refuse(accepted.Replace("\"version\":15", "\"version\":14"));
            Refuse(accepted.Replace("\"formatVersion\":15", "\"formatVersion\":16"));
            Refuse(accepted.Replace("\"worldUid\":71", "\"worldUid\":0"));
            Refuse(accepted.Replace("\"sender\":\"Steam_sender\"", "\"sender\":\"<admin>\""));
            Refuse(accepted.Replace("\"sender\":\"Steam_sender\"", "\"sender\":\"Steam_sender\",\"admin\":true"));
            string old = Encoding.UTF8.GetString(portable).Replace("\"version\":15", "\"version\":14");
            Require(BlueprintExchangeFormat.Decode(old).peerOrigins == null, "Legacy portable without peer field refused.");
            foreach (string value in new[] { "null", "[]" })
                Refuse(old.Replace("\"blueprint\":{", "\"blueprint\":{\"peerOrigins\":" + value + ","));
            string native = File.ReadAllText(path); string oldPath = Path.Combine(folder, "old.json");
            File.WriteAllText(oldPath, native.Replace("\"version\":27", "\"version\":25"));
            var oldStore = new B(oldPath);
            Require(oldStore.All().Count == 0 && !oldStore.TryAddCategory("blocked", out _) &&
                File.ReadAllText(oldPath) == native.Replace("\"version\":27", "\"version\":25"), "Legacy native accepted peer fields or rewrote refused file.");
            var editorPart = new BlueprintEditorPart("a", "woodwall", "Wall", new Point3(), new Rotation3(0, 0, 0, 1));
            var editor = new BlueprintEditorDocument(null, "Destination", "QA", new[] { editorPart });
            var incoming = new BlueprintEditorDocument(null, "Incoming", "QA", new[] { editorPart }, sourcePeerOrigins: copy.peerOrigins.Select(value => value.ToGeometry()));
            Require(editor.InsertBlueprint(incoming, new Point3(2, 0, 0), new Rotation3(0, 0, 0, 1)) && editor.PeerOrigins.Count == 1, "Insert lost peer provenance.");
            Require(editor.Undo() && editor.PeerOrigins.Count == 0 && editor.Redo() && editor.PeerOrigins.Count == 1, "Undo/Redo lost peer provenance.");
            Require(store.TryUpdateDocument(copy, copy.name, copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId, copy.primaryGroupId,
                copy.supports, copy.placementSupportId, out error, peerOrigins: Array.Empty<B.PeerOrigin>()) && copy.peerOrigins.Count == 0, "Explicit empty list failed to clear provenance.");
            Require(Hash(portable) == Hash(BlueprintExchangeFormat.Encode(source)), "Acceptance changed source bytes.");
            Console.WriteLine("PASS peer provenance: " + checks + " assertions; fresh copy/hash/identity, clone/update/SaveAs/share/reload, strict old/future schemas, editor insert/Undo/Redo. Passive data only; no peer authentication, wire/UI or remote proof.");
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Refuse(string json)
        { try { BlueprintExchangeFormat.Decode(json); throw new Exception("Invalid peer origin accepted."); } catch (InvalidDataException) { checks++; } }
        private static void Require(bool value, string error) { checks++; if (!value) throw new Exception(error); }
    }
}
