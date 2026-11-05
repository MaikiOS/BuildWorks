using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PaidContentsFixtureChecks
    {
        private static int checks;
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); ++checks; }
        private static void Refuse(CompositeBlueprintStore.ImportReview review, Guid id)
        {
            try { BuildWorksTestConstructionProject.ContentsBlueprint(review, id); }
            catch (Exception error) when (error is InvalidDataException || error is ArgumentException) { ++checks; return; }
            throw new InvalidOperationException("Invalid paid contents fixture accepted.");
        }
        private static byte[] Snapshot(CompositeBlueprintStore.Blueprint source)
        {
            var copy = source.ImportCopy(); copy.id = "11111111111111111111111111111111";
            return BlueprintExchangeFormat.Encode(copy);
        }
        internal static void Run()
        {
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "contents", "25-contents-populated.buildworks.json");
            byte[] bytes = File.ReadAllBytes(path); Guid id = Guid.NewGuid();
            var review = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            byte[] before = Snapshot(review.Documents.Single());
            var result = BuildWorksTestConstructionProject.ContentsBlueprint(review, id);
            Require(result.id == id.ToString("N") && result.name == BuildWorksTestConstructionProject.ContentsProjectName &&
                result.primaryPartId == "chest" && result.prefabName == "piece_chest_wood" && result.parts.Count == 1, "Single-chest identity/primary differs.");
            var part = result.parts[0];
            Require(part.position.x == 0 && part.position.y == 0 && part.position.z == 0 && part.rotation.w == 1 &&
                part.rotation.x == 0 && part.rotation.y == 0 && part.rotation.z == 0 &&
                part.scale.x == 1 && part.scale.y == 1 && part.scale.z == 1 && part.baseScale.HasValue && part.baseScale.Value.x == 1 && part.editorVisible && !part.editorLocked,
                "Fixture transform is not rebased native unit identity.");
            Require(!ReferenceEquals(part, review.Documents[0].parts[2]) && part.foreignData.zdo == review.Documents[0].parts[2].foreignData.zdo &&
                Snapshot(review.Documents[0]).SequenceEqual(before), "Original fixture/archival bytes changed.");
            var reopened = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(result)));
            Require(reopened.parts.Count == 1 && BlueprintContainerInventory.TryRead(reopened.parts[0].foreignData.ToGeometry(), Hash, out var items) &&
                items.Count == 1 && items[0].Prefab == "Wood" && items[0].Stack == 3, "Demand changed on portable reopen.");
            Refuse(review, Guid.Empty); Refuse(null, id);
            var empty = CompositeBlueprintStore.ReviewImportFile(Path.Combine(Path.GetDirectoryName(path), "24-contents-empty.buildworks.json"), Hash, 0);
            Refuse(empty, id);
            var invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].parts.RemoveAt(2); Refuse(invalid, id);
            invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].parts.Add(invalid.Documents[0].parts[2]); Refuse(invalid, id);
            invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].parts[2].foreignData.contentsCleared = true; Refuse(invalid, id);
            invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].parts[2].foreignData.zdo = "broken"; Refuse(invalid, id);
            invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].parts[2].foreignData = invalid.Documents[0].parts[3].foreignData; Refuse(invalid, id);
            invalid = CompositeBlueprintStore.ReviewImportFile(path, Hash, 0);
            invalid.Documents[0].importError = "invalid"; Refuse(invalid, id);
            Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Authored fixture file changed.");
            Console.WriteLine("PASS paid contents QA snapshot: " + checks + " actual bridge/parser checks; no native placement/payment tested.");
        }
        private static int Hash(string text)
        {
            unchecked { int a = 5381, b = a; for (int i = 0; i < text.Length; i += 2) { a = (a * 33) ^ text[i]; if (i + 1 < text.Length) b = (b * 33) ^ text[i + 1]; } return a + b * 1566083941; }
        }
    }
}
