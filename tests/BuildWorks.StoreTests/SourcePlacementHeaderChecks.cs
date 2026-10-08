using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class SourcePlacementHeaderChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            const string headers = "#Center:\r\n#Coordinates:-2480.165,400.894,126.336\r\n#Rotation:128.572,0,0\r\n";
            const string piece = "#Pieces\r\nwoodwall;;3;4;5;0;0.7071068;0;0.7071068;\"\";2;.5;1.25\r\n";
            string text = "\uFEFF#Name:QA116 Source Position\r\n" + headers + piece;
            var source = ExternalBlueprintLibrary.Parse(text, "15-source-position.blueprint");
            Require(source.importError == null && source.parts.Count == 1, "Known source position did not parse");
            Require(!source.sourceDocuments.Single().ToGeometry().HasOpaqueSections,
                "Empty Center and original world pose falsely block construction");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            Require(!CompositeBlueprintStore.HasForeignData(prepared) &&
                CompositeBlueprintStore.InspectContents(prepared, null).Unknown == 0,
                "Known source placement provenance reported as unknown gameplay state");
            var part = prepared.parts.Single();
            Require(part.position.x == 3 && part.position.y == 4 && part.position.z == 5 &&
                part.rotation.y > .7071f && part.rotation.w > .7071f &&
                part.baseScale.Value.x == 2 && part.baseScale.Value.y == .5f && part.baseScale.Value.z == 1.25f,
                "Original world pose was incorrectly applied to local piece transform");
            var store = new CompositeBlueprintStore(Path.Combine(directory, "library.json"));
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            var reopened = new CompositeBlueprintStore(Path.Combine(directory, "library.json")).All().Single();
            var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            Require(portable.sourceDocuments.Single().text == text && portable.parts.Single().position.x == 3 &&
                portable.parts.Single().sourceLine == 6 && !CompositeBlueprintStore.HasForeignData(portable),
                "Native/portable roundtrip changed original headers, row binding or classification");
            foreach (string unsupported in new[] {
                "#Center:woodwall\n", "#Center\n", "#Coordinates:1,2\n", "#Coordinates:NaN,0,0\n",
                "#Rotation:Infinity,0,0\n", "#Rotation:1,2,3,4\n", "#Coordinates:1e999,0,0\n",
                headers + "#Rotation:0,0,0\n", "#Future:0,0,0\n", "#TerrainHeight:0,0,0;0;1\n" })
            {
                var evidence = new BlueprintSourceDocument("blueprint", "blocked.blueprint", unsupported + piece);
                Require(evidence.HasOpaqueSections && evidence.HasPartAt(unsupported.Count(c => c == '\n') + 2, "woodwall"),
                    "Unsupported, duplicate or malformed header lost its blocker or valid piece rows");
            }
            var afterPieces = new BlueprintSourceDocument("blueprint", "late.blueprint", piece + headers);
            Require(afterPieces.HasOpaqueSections, "Late placement metadata silently changed section meaning");
            var vbuild = new BlueprintSourceDocument("vbuild", "other.vbuild", headers + "woodwall 0 0 0 1 3 4 5\n");
            Require(vbuild.HasOpaqueSections, "Blueprint placement metadata leaked into vbuild dialect");
            string fixture = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../../..", "tests/fixtures/placement-headers/15-source-position.blueprint"));
            var fixtureSource = ExternalBlueprintLibrary.LoadFile(fixture).Single();
            var fixtureCopy = ExternalBlueprintLibrary.DetachedCopy(ExternalBlueprintLibrary.PrepareForHost(fixtureSource,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }));
            Require(fixtureCopy.importError == null && fixtureCopy.name == "QA116 Source Position" &&
                fixtureCopy.parts.Count == 1 && fixtureCopy.parts.Single().position.x == 3 &&
                fixtureCopy.parts.Single().baseScale.Value.y == .5f && !CompositeBlueprintStore.HasForeignData(fixtureCopy),
                "Ready source-position fixture failed conversion");
            Console.WriteLine("PASS original world pose provenance: local TRS unchanged, native/portable source exact, malformed/named-center/late/future headers remain blocked");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
