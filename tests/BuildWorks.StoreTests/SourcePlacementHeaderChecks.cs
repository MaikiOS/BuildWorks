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
            QuaternionGeometryParity(directory, headers);
            NamedCenterRetainsBlocker(directory, "#Center:woodwall\n" + piece, "named-center.blueprint");
        }

        private static void QuaternionGeometryParity(string directory, string headers)
        {
            foreach (int sign in new[] { 1, -1 })
            {
                string component = sign > 0 ? ".707" : "-.707";
                string text = headers + "#Pieces\nwoodwall;;3;4;5;0;" + component + ";0;" + component + ";\"\";2;.5;1.25\n";
                var raw = ExternalBlueprintLibrary.Parse(text, "quaternion.blueprint");
                Require(raw.importError == null, raw.importError);
                var basis = new CompositeBlueprintStore.VectorData { x = 2, y = .5f, z = 3 };
                var prepared = ExternalBlueprintLibrary.PrepareForHost(raw, _ => basis);
                var store = new CompositeBlueprintStore(Path.Combine(directory, "quaternion" + sign + ".json"));
                Require(store.TryGetOrCreatePlacementCopy(prepared, out _, out string error), error);
                var reopened = new CompositeBlueprintStore(Path.Combine(directory, "quaternion" + sign + ".json")).All().Single();
                var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
                foreach (var document in new[] { prepared, reopened, portable })
                {
                    var part = document.parts.Single();
                    var scale = part.RootScale(new UnityEngine.Vector3(.25f, 5, .125f));
                    var rotation = new Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                    Point3 corner = new Point3(part.position.x, part.position.y, part.position.z) +
                        rotation.Rotate(new Point3(scale.x, 2 * scale.y, 3 * scale.z));
                    // A 90-degree Y turn maps (2,1,3.75) to (3.75,1,-2), regardless of quaternion sign.
                    Require((corner - new Point3(6.75, 5, 3)).LengthSquared < 1e-12 &&
                        part.rotation.y * sign > 0 && part.rotation.w * sign > 0 &&
                        document.sourceDocuments.Single().text == text,
                        "Quaternion sign, pinned size or informational world pose changed local geometry/source");
                    var multiplier = part.PlacementScale(basis.ToVector3());
                    var nativeScale = UnityEngine.Vector3.Scale(basis.ToVector3(), multiplier);
                    Require((nativeScale - scale).sqrMagnitude < 1e-10f,
                        "Native prefab multiplier disagrees with pinned editor root size");
                }
                Require(raw.sourceDocuments.Single().text == text && raw.parts[0].scaleIsAbsolute,
                    "Geometry parity check mutated the imported original");
            }
            Console.WriteLine("PASS q/-q imported geometry: rounded quaternion, nonunit prefab bases, local/world provenance, native/portable reopen and native scale multiplier");
        }

        internal static void RunNamedCenterFile(string path, string directory)
        {
            byte[] bytes = File.ReadAllBytes(path);
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Require(BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "") ==
                    "436A44ED21D89854DB51C6EAFBF1D7CC9F0EB8D23FA08D18B7BD8E80B7201909",
                    "Expected the pinned Infinity Hammer rotated-center fixture");
            NamedCenterRetainsBlocker(directory, File.ReadAllText(path), Path.GetFileName(path));
            NamedCenterChecks.RunAuthor(File.ReadAllText(path), Path.GetFileName(path), Path.Combine(directory, "normalized"));
            Require(File.ReadAllBytes(path).SequenceEqual(bytes), "Named-center check changed its original file");
        }

        private static void NamedCenterRetainsBlocker(string directory, string text, string sourceName)
        {
            var prepared = ExternalBlueprintLibrary.PrepareForHost(ExternalBlueprintLibrary.Parse(text, sourceName),
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            // A lost interpretation marker must stay blocked even if the stored poses look normalized.
            foreach (var part in prepared.parts) part.sourceFrameApplied = false;
            float expectedX = prepared.parts[0].position.x + 1;
            int expectedSourceLine = prepared.parts[0].sourceLine;
            string path = Path.Combine(directory, "named-center.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            var edited = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
            edited.parts[0].position.x += 1;
            Require(store.TryUpdateParts(saved, edited.parts, edited.anchors, out error), error);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(reopened)));
            foreach (var document in new[] { prepared, saved, reopened, portable })
            {
                Require(document.sourceDocuments.Single().text == text &&
                    document.sourceDocuments.Single().ToGeometry().HasOpaqueSections,
                    "Unsupported named frame lost the source predicate used by the native placement gate");
                var retained = CompositeBlueprintStore.CopyWithContentsPolicy(document, false, false, null, omitArmorState: true);
                Require(retained.sourceDocuments.Single().ToGeometry().HasOpaqueSections,
                    "Omitting unrelated armor state removed the named frame blocker");
                foreach (bool clearStorage in new[] { false, true })
                {
                    bool refused = false;
                    try { CompositeBlueprintStore.CopyWithContentsPolicy(document, clearStorage, !clearStorage, null); }
                    catch (InvalidDataException exception) { refused = exception.Message == "$buildworks_import.object_data"; }
                    Require(refused, "Contents clearing silently discarded an unsupported source frame");
                }
            }
            Require(portable.parts[0].position.x == expectedX && portable.parts[0].sourceLine == expectedSourceLine,
                "Named-frame editor save lost the intended edit or source-row binding");
            Console.WriteLine("PASS unsupported named frame: edit/save/portable keep exact raw and native blocker; unrelated state omission cannot remove it");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
