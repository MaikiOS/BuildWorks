using System;
using System.IO;
using System.Linq;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlacementContentsFixtureChecks
    {
        internal static void Run()
        {
            foreach (bool populated in new[] { false, true })
            {
                string file = populated ? "25-contents-populated.buildworks.json" : "24-contents-empty.buildworks.json";
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "contents", file);
                byte[] bytes = File.ReadAllBytes(path);
                var source = ExternalBlueprintLibrary.LoadFile(path).Single();
                Require(source.importError == null && source.parts.Count == 4 &&
                    source.parts.Select(part => part.prefabName).SequenceEqual(new[] { "wood_floor", "woodwall", "piece_chest_wood", "itemstand" }) &&
                    source.parts.All(part => part.scale.x == 1 && part.scale.y == 1 && part.scale.z == 1 && !part.scaleIsAbsolute),
                    "QA contents structure or unit scale changed: " + source.importError + "; parts=" + source.parts.Count);
                source = BlueprintExchangeFormat.Decode(File.ReadAllText(path));
                var storage = source.parts[2].foreignData.ToGeometry();
                var mount = source.parts[3].foreignData.ToGeometry();
                Require(BlueprintContainerInventory.TryRead(storage, Hash, out var stored) && stored.Count == (populated ? 1 : 0) &&
                    (!populated || stored[0].Prefab == "Wood" && stored[0].Stack == 3) &&
                    BlueprintItemStandContents.TryRead(mount, Hash, out var item) && item.Prefab == "SwordIron" &&
                    item.Quality == 3 && item.DurabilityHundredths == 12505 && !item.Cheated,
                    "QA contents payload is unreadable or lost distinct archived item attributes");
                byte[] original = BlueprintExchangeFormat.Encode(source);
                foreach (bool clearStorage in new[] { false, true })
                foreach (bool clearMounts in new[] { false, true })
                {
                    var recipe = CompositeBlueprintStore.CopyWithContentsPolicy(source, clearStorage, clearMounts, Hash);
                    var reopened = BlueprintExchangeFormat.Decode(System.Text.Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(recipe)));
                    Require(!ReferenceEquals(source, recipe) && !ReferenceEquals(source.parts[2], recipe.parts[2]) &&
                        reopened.parts[2].foreignData.contentsCleared == clearStorage && reopened.parts[3].foreignData.contentsCleared == clearMounts &&
                        reopened.parts[2].foreignData.zdo == storage.Zdo && reopened.parts[3].foreignData.zdo == mount.Zdo &&
                        BlueprintExchangeFormat.Encode(source).SequenceEqual(original) && File.ReadAllBytes(path).SequenceEqual(bytes),
                        "QA contents choices coupled, rewrote archival payloads or modified the fixture");
                }
            }
            Console.WriteLine("PASS authored QA contents fixtures: native v109 payloads, source quality3/durability, two independent policies, portable reopen and immutable originals. Native geometry/placement not tested.");
        }

        private static int Hash(string text)
        {
            unchecked
            {
                int first = 5381, second = first;
                for (int index = 0; index < text.Length; index += 2)
                {
                    first = (first * 33) ^ text[index];
                    if (index + 1 < text.Length) second = (second * 33) ^ text[index + 1];
                }
                return first + second * 1566083941;
            }
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException(message); }
    }
}
