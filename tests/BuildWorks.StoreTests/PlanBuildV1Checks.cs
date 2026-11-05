using System;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlanBuildV1Checks
    {
        internal static void Run(string directory)
        {
            // Own synthetic rows; the pinned author's exact V1 file is tested from the private corpus.
            const string first = "woodwall;Building;1;2;-3;0;0;0;1;\r\n";
            const string second = "wood_floor;Building;-2;0;1;0;.7071068;0;.7071068;\r\n";
            string text = "\uFEFF" + first + second;
            var source = ExternalBlueprintLibrary.Parse(text, "own-legacy.blueprint");
            Require(source.importError == null && source.parts.Count == 2,
                "Headerless PlanBuild V1 rows were not parsed: " + source.importError);
            Require(source.parts[0].sourceLine == 1 && source.parts[1].sourceLine == 2 &&
                source.parts[0].position.x == 1 && source.parts[1].position.x == -2,
                "Headerless piece transforms or physical source lines changed");
            var evidence = source.sourceDocuments.Single().ToGeometry();
            Require(!evidence.HasOpaqueSections && evidence.HasPartAt(1, "woodwall") &&
                evidence.HasPartAt(2, "wood_floor") && evidence.Text == text,
                "Headerless source row index disagrees with the importer");
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            byte[] portable = BlueprintExchangeFormat.Encode(saved);
            var reopened = new CompositeBlueprintStore(path).All().Single();
            var decoded = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(portable));
            foreach (var copy in new[] { reopened, decoded })
                Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(copy)) &&
                    copy.sourceDocuments.Single().text == text && copy.parts[1].sourceLine == 2,
                    "Headerless native/portable roundtrip changed data, raw source or bindings");
            var reimported = ExternalBlueprintLibrary.Parse(Encoding.UTF8.GetString(portable), "copy.buildworks.json");
            Require(reimported.importError == null, reimported.importError);
            reimported.id = saved.id;
            Require(portable.SequenceEqual(BlueprintExchangeFormat.Encode(reimported)),
                "Headerless portable reimport changed fields or dependencies");

            var invalid = ExternalBlueprintLibrary.Parse(first + second.Replace(";-2;", ";NaN;"), "bad.blueprint");
            Require(invalid.importError != null && invalid.parts.Count == 0 && invalid.importPartCount == 2,
                "Invalid second legacy row exposed a partial construction");
            string mixed = first + "#Future\r\n" + second + "#Pieces\r\n" + second;
            var resumed = ExternalBlueprintLibrary.Parse(mixed, "future.blueprint");
            var mixedEvidence = resumed.sourceDocuments.Single().ToGeometry();
            Require(resumed.importError == null && resumed.parts.Count == 2 &&
                resumed.parts[1].sourceLine == 5 && mixedEvidence.HasOpaqueSections &&
                !mixedEvidence.HasPartAt(3, "wood_floor") && mixedEvidence.HasPartAt(5, "wood_floor"),
                "Unknown legacy section was parsed as pieces or did not resume at explicit #Pieces");
            var late = new BlueprintSourceDocument("blueprint", "late.blueprint",
                first + "#Coordinates:1,2,3\r\n#Pieces\r\n" + second);
            Require(late.HasOpaqueSections && late.HasPartAt(1, "woodwall") && late.HasPartAt(4, "wood_floor"),
                "Late provenance after an implicit piece was trusted as a pre-piece header");
            SourcePlacementHeaderChecks.Run(Path.Combine(directory, "existing-headers"));
            Console.WriteLine("PASS headerless PlanBuild V1: raw/line bindings/TRS/native/portable/reimport; bad second row atomic; unknown section stop/resume; late provenance remains opaque. No game proof.");
        }

        internal static void RunAuthorFixture(string file, string directory)
        {
            byte[] original = File.ReadAllBytes(file);
            using (var hash = SHA256.Create())
                Require(BitConverter.ToString(hash.ComputeHash(original)).Replace("-", "") ==
                    "38D318C60DEE3776CF432D946A994E226E8DAC4E02CC4C42748D5F2C633065B8",
                    "Expected the exact pinned PlanBuild TestBox_V1 author fixture");
            var source = ExternalBlueprintLibrary.LoadFile(file).Single();
            Require(source.importError == null && source.parts.Count == 6, source.importError);
            string[] rows = Encoding.UTF8.GetString(original).Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var evidence = source.sourceDocuments.Single().ToGeometry();
            for (int i = 0; i < 6; ++i)
            {
                string[] fields = rows[i].TrimEnd('\r').Split(';');
                var part = source.parts[i];
                Require(part.prefabName == fields[0] && part.sourceLine == i + 1 && evidence.HasPartAt(i + 1, fields[0]) &&
                    part.position.x == float.Parse(fields[2], CultureInfo.InvariantCulture) &&
                    part.position.y == float.Parse(fields[3], CultureInfo.InvariantCulture) &&
                    part.position.z == float.Parse(fields[4], CultureInfo.InvariantCulture) &&
                    Math.Abs(part.rotation.x - float.Parse(fields[5], CultureInfo.InvariantCulture)) < .000001f &&
                    Math.Abs(part.rotation.y - float.Parse(fields[6], CultureInfo.InvariantCulture)) < .000001f &&
                    Math.Abs(part.rotation.z - float.Parse(fields[7], CultureInfo.InvariantCulture)) < .000001f &&
                    Math.Abs(part.rotation.w - float.Parse(fields[8], CultureInfo.InvariantCulture)) < .000001f &&
                    part.scale.x == 1 && part.scale.y == 1 && part.scale.z == 1,
                    "Author V1 prefab/TRS/default scale/physical row mismatch");
            }
            var prepared = ExternalBlueprintLibrary.PrepareForHost(source,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
            string path = Path.Combine(directory, "library.json");
            var store = new CompositeBlueprintStore(path);
            Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
            Require(saved.sourceDocuments.Single().text == Encoding.UTF8.GetString(original) &&
                saved.parts.Select(part => part.sourceLine).SequenceEqual(Enumerable.Range(1, 6)),
                "Actual author source text or row identities changed during copy");
            CorpusRoundTripChecks.RunSaved(path, saved.id);
            Require(File.ReadAllBytes(file).SequenceEqual(original), "Author fixture was modified");
            Console.WriteLine("PASS exact PlanBuild author V1: 303bytes/6parts, physical rows1..6, native reopen/portable/full-field reimport; unit bases, no game/target-reader execution.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
