using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class LibraryOriginChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            string path = Path.Combine(folder, "library.json");
            var store = new B(path);
            Require(store.TrySaveDocument("Server source", "QA", new[] { Part("a", 0), Part("b", 2) }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "a", null, out B.Blueprint source, out string error), error);
            byte[] original = BlueprintExchangeFormat.Encode(source);
            B.Blueprint decoded = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(original), out int formatVersion);
            Require(formatVersion == 12 && decoded.libraryOrigins == null, "Current format version or empty origin changed");
            var legacyPortable = BlueprintExchangeFormat.DecodeWithVersion(Encoding.UTF8.GetString(original).Replace("\"version\":12", "\"version\":11"), out int legacyVersion);
            Require(legacyVersion == 11 && legacyPortable.libraryOrigins == null && legacyPortable.parts.Count == 2, "Installed .183 portable compatibility lost");
            string legacyPath = Path.Combine(folder, "legacy-22.json");
            string legacyNative = File.ReadAllText(path).Replace("\"version\":23", "\"version\":22");
            File.WriteAllText(legacyPath, legacyNative);
            var legacyStore = new B(legacyPath);
            Require(legacyStore.All().Count == 1 && File.ReadAllText(legacyPath) == legacyNative && legacyStore.TryAddCategory("Legacy", out error) &&
                File.ReadAllText(legacyPath).Contains("\"version\":23"), "Installed .183 native22 compatibility/migration lost");
            var origin = new B.LibraryOrigin("server.test:2456", 71, source.id, 3, 1, "Steam_admin", Hash(original), formatVersion);
            Require(store.TrySaveAcceptedCopy(decoded, origin, out B.Blueprint copy, out error), error);
            Require(copy.id != source.id && copy.libraryOrigins.Count == 1 && decoded.libraryOrigins == null && source.libraryOrigins == null,
                "Acceptance reused source identity or changed source provenance");
            string accepted = Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(copy));
            Require(copy.libraryOrigins[0].server == origin.server && !ReferenceEquals(copy.libraryOrigins[0], origin), "Origin snapshot aliased caller");
            origin.publisher = "changed caller";
            Require(copy.libraryOrigins[0].publisher == "Steam_admin", "Caller mutation changed accepted copy");
            var detached = copy.ImportCopy(); detached.libraryOrigins[0].server = "other";
            Require(copy.libraryOrigins[0].server != "other", "ImportCopy shared mutable provenance");
            Require(new B(path).All().Single(item => item.id == copy.id).libraryOrigins[0].sha256 == Hash(original), "Native reload lost origin");
            Require(File.ReadAllText(Path.Combine(store.ShareDirectory, copy.id + ".buildworks.json")).Contains("libraryOrigins"), "Automatic share file lost origin");
            Require(store.TryUpdateDocument(copy, "Edited", copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId, copy.primaryGroupId,
                out error, copy.customSnapPoints), error);
            Require(copy.libraryOrigins[0].revision == 3, "Document update cleared origin");
            Require(store.TrySaveDocument("Save As", copy.category, copy.parts, copy.groups, copy.anchors, copy.primaryPartId, copy.primaryGroupId,
                copy.supports, copy.placementSupportId, out B.Blueprint saveAs, out error, copy.customSnapPoints, copy), error);
            Require(saveAs.id != copy.id && saveAs.libraryOrigins[0].sha256 == Hash(original) && !ReferenceEquals(saveAs.libraryOrigins, copy.libraryOrigins), "Save As lost/aliased origin");

            string zip = store.ExportBatch(new[] { copy, saveAs }, folder, "origin-roundtrip", false, false, _ => 0).GetAwaiter().GetResult();
            var imports = ExternalBlueprintLibrary.LoadFile(zip, "origin-roundtrip.zip");
            Require(imports.Count == 2 && imports.All(item => item.importError == null && item.libraryOrigins.Count == 1), "Batch ZIP import dropped provenance");
            var placement = ExternalBlueprintLibrary.DetachedCopy(imports[0]);
            Require(placement.libraryOrigins[0].documentId == source.id && !ReferenceEquals(placement.libraryOrigins, imports[0].libraryOrigins), "External placement copy lost origin");
            Require(PlanBuildExport.Review(copy, _ => new B.VectorData { x = 1, y = 1, z = 1 }).LibraryOrigins == 1, "Foreign export did not report omitted provenance");

            Refuse(accepted.Replace("\"version\":12", "\"version\":11"));
            Refuse(accepted.Replace("\"formatVersion\":12", "\"formatVersion\":13"));
            Refuse(accepted.Replace("\"revision\":3", "\"revision\":0"));
            Refuse(accepted.Replace("\"access\":1", "\"access\":2"));
            Refuse(accepted.Replace("\"worldUid\":71", "\"worldUid\":0"));
            Refuse(accepted.Replace("\"publisher\":\"Steam_admin\"", "\"publisher\":\"<admin>\""));
            Refuse(accepted.Replace("\"revision\":3", "\"revision\":3,\"revision\":3"));
            Refuse(accepted.Replace("\"revision\":3", "\"revision\":3,\"role\":\"administrator\""));
            Refuse(accepted.Replace(Hash(original), Hash(original).ToLowerInvariant()));
            string native = File.ReadAllText(path);
            string invalidOld = Path.Combine(folder, "invalid-old.json");
            File.WriteAllText(invalidOld, native.Replace("\"version\":23", "\"version\":22"));
            var old = new B(invalidOld);
            Require(old.All().Count == 0 && !old.TryAddCategory("Blocked", out _) && File.ReadAllText(invalidOld) == native.Replace("\"version\":23", "\"version\":22"),
                "Old native schema accepted provenance or rewrote refused source");
            var bounded = decoded.ImportCopy();
            bounded.libraryOrigins = Enumerable.Range(1, B.MaximumLibraryOrigins).Select(index => new B.LibraryOrigin("server", 71, source.id, index, 0, "admin", Hash(original), 12)).ToList();
            Require(B.Valid(bounded), "Maximum origin count rejected");
            int before = store.All().Count;
            var next = new B.LibraryOrigin("server", 71, source.id, 17, 0, "admin", Hash(original), 12);
            Require(!store.TrySaveAcceptedCopy(bounded, next, out _, out error) && error.Contains("server.origin_limit") && store.All().Count == before,
                "Over-limit accepted copy silently dropped history or added a card");
            bounded.libraryOrigins.Add(next); Refuse(Encoding.UTF8.GetString(UnsafeEncode(bounded)));
            var duplicate = copy.ImportCopy(); duplicate.libraryOrigins.Add(duplicate.libraryOrigins[0].Copy());
            Require(!B.Valid(duplicate), "Duplicate provenance accepted");
            // A verified current receipt replaces a conflicting informational record, never grants authority.
            var repeated = copy.ImportCopy(); repeated.libraryOrigins[0].publisher = "forged old label";
            var receipt = new B.LibraryOrigin("server.test:2456", 71, source.id, 3, 1, "Steam_admin", Hash(original), 12);
            Require(store.TrySaveAcceptedCopy(repeated, receipt, out B.Blueprint refreshed, out error) && refreshed.libraryOrigins.Count == 1 && refreshed.libraryOrigins[0].publisher == "Steam_admin", error);
            byte[] beforeFailure = File.ReadAllBytes(path); int countBeforeFailure = store.All().Count;
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Require(!store.TrySaveAcceptedCopy(decoded, receipt, out _, out error), "Locked native commit unexpectedly succeeded");
            Require(store.All().Count == countBeforeFailure && beforeFailure.SequenceEqual(File.ReadAllBytes(path)), "Failed acceptance left a card or changed durable data");
            Require(Encoding.UTF8.GetString(original) == Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(source)), "Source bytes changed after acceptance");
            int owner = Environment.CurrentManagedThreadId;
            store.ConfigurePrefabMetadata(_ => { Require(Environment.CurrentManagedThreadId == owner, "Publication called native metadata on worker"); return null; });
            var publicationSource = source.ImportCopy(); publicationSource.parts[0].displayName = "Frozen publication";
            var publication = store.PrepareServerPublication(publicationSource); publicationSource.parts[0].displayName = "Changed caller";
            Require(BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(publication.GetAwaiter().GetResult())).parts[0].displayName == "Frozen publication",
                "Server publication aliased mutable source or ran native metadata on worker");
            Console.WriteLine("PASS library origin native23/portable12: fresh accepted ID, immutable/copy/update/share/reload/batch/external persistence, source unchanged, explicit foreign omission, strict versions/fields/count/duplicates and atomic over-limit refusal. Informational only; not authorization or live server/UI proof.");
        }
        private static byte[] UnsafeEncode(B.Blueprint document)
        {
            // Bypass Encode validation only to test the untrusted decoder boundary.
            using (var stream = new MemoryStream())
            {
                new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(B.Blueprint)).WriteObject(stream, document);
                string json = "{\"format\":\"buildworks-blueprint\",\"version\":12,\"requiredPrefabIds\":[\"woodwall\"],\"blueprint\":" + Encoding.UTF8.GetString(stream.ToArray()) + "}";
                return Encoding.UTF8.GetBytes(json);
            }
        }
        private static B.Part Part(string id, float x) => new B.Part
        {
            stableId = id,
            prefabName = "woodwall",
            displayName = "Wall",
            position = new B.VectorData { x = x },
            rotation = new B.QuaternionData { w = 1 },
            scale = new B.VectorData { x = 1, y = 1, z = 1 },
            baseScale = new B.VectorData { x = 1, y = 1, z = 1 },
            editorVisible = true
        };
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Refuse(string json)
        { try { BlueprintExchangeFormat.Decode(json); throw new Exception("Invalid origin accepted"); } catch (InvalidDataException) { } }
        private static void Require(bool value, string error) { if (!value) throw new Exception(error); }
    }
}
