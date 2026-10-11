using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class VbuildDialectChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            PlanBuildDecimals(directory);
            NumericParity();
            const string row = "woodwall  .7071068  .7071068 3,25 -4,5 ";
            const string text = "\uFEFF#Name:QA Legacy Vbuild\r\n" + row + "\r\nwoodwall  1   -2 0 1\r\n";
            var culture = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new[] { "en-US", "ru-RU" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    var source = ExternalBlueprintLibrary.Parse(text, "legacy.vbuild");
                    Require(source.importError == null && source.parts.Count == 2,
                        "Legacy vbuild empty zero slots and comma decimals were rejected: " + source.importError);
                    var part = source.parts[0];
                    Require(part.position.x == 3.25f && part.position.y == -4.5f && part.position.z == 0 &&
                        part.rotation.x == 0 && part.rotation.z == 0 && part.rotation.y > .7071f && part.rotation.w > .7071f &&
                        source.parts[1].rotation.y == 1 && source.parts[1].rotation.w == 0,
                        "Legacy vbuild numeric slots shifted or zero quaternion components changed");
                    var prepared = ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
                    var store = new CompositeBlueprintStore(Path.Combine(directory, name + ".json"));
                    Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                    var reopened = new CompositeBlueprintStore(Path.Combine(directory, name + ".json")).All().Single();
                    var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
                    foreach (var copy in new[] { reopened, portable })
                        Require(copy.parts[0].position.x == 3.25f && copy.parts[0].position.z == 0 &&
                            copy.sourceDocuments.Single().ToGeometry().Text == text && copy.parts[0].sourceLine == 2,
                            "Legacy vbuild save/portable roundtrip lost transforms or exact source evidence");
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }

            const string emptyState = "AAAAAA==";
            string withState = "woodwall  .7071068  .7071068 3,25 -4,5  " + emptyState + " 1\n";
            var padded = ExternalBlueprintLibrary.Parse(withState.TrimEnd('\n') + " \t \n", "padded.vbuild");
            Require(padded.importError == null && padded.parts.Single().foreignData.zdo == emptyState &&
                padded.parts.Single().position.z == 0,
                "Trailing whitespace after legacy ZDO/chance became an extra field or shifted its numeric slots");
            var original = ExternalBlueprintLibrary.Parse(withState, "state.vbuild");
            Require(original.importError == null && original.parts.Single().foreignData.zdo == emptyState,
                "Legacy vbuild state slot shifted");
            var converted = ExternalBlueprintLibrary.PrepareForHost(original,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, ForeignZdoChecks.Hash, 0);
            Require(converted.parts.Single().foreignData == null &&
                original.parts.Single().foreignData.zdo == emptyState &&
                converted.sourceDocuments.Single().ToGeometry().Text == withState,
                "Legacy source-bound state conversion uses a different field layout or mutates its source");
            original.parts[0].foreignData.zdo = Convert.ToBase64String(new byte[] { 2, 0, 0, 0, 0 });
            var refused = ExternalBlueprintLibrary.PrepareForHost(original,
                _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, ForeignZdoChecks.Hash, 0);
            Require(refused.parts.Single().foreignData != null, "Mismatched legacy state lost its blocker");

            foreach (string invalid in new[] { "woodwall 0 0 0 0 0 0 0", "woodwall 0 0 0 1 NaN 0 0",
                "woodwall 0 0 0 1 1,2,3 0 0", "woodwall 0 0 0 1 1e999 0 0", "woodwall 0 0 0 1 0 0",
                "woodwall 0 0 0 1 0 0 0 opaque 1 extra" })
                Require(ExternalBlueprintLibrary.Parse(invalid, "invalid.vbuild").importError != null,
                    "Malformed legacy vbuild became usable: " + invalid);
            Require(ExternalBlueprintLibrary.Parse("woodwall;0;0;0;1;3,25;0;0", "strict.vbuild").importError != null,
                "Legacy numeric convention leaked into semicolon vbuild");
            string fixture = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../../..", "tests/fixtures/vbuild/22-legacy-zero-comma.vbuild"));
            var ready = ExternalBlueprintLibrary.LoadFile(fixture).Single();
            Require(ready.importError == null && ready.name == "QA118 Legacy Vbuild" && ready.parts.Count == 2 &&
                ready.parts[0].position.x == 3.25f && ready.parts[1].rotation.w == 1,
                "Ready legacy vbuild fixture failed actual file import");
            Console.WriteLine("PASS legacy vbuild slots/comma decimals: invariant cultures, source-bound state, native/portable reopen and strict dialect boundaries");
        }

        private static void PlanBuildDecimals(string directory)
        {
            const string row = "sign;;3,25;-4,5;0,125;0;0,7071068;0;0,7071068;Привет, мир;1,5;0,5;2,25";
            const string text = "\uFEFF#Name:QA147 PlanBuild Comma\r\n#Pieces\r\n" + row + "\r\n";
            string fixture = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../../..", "tests/fixtures/planbuild/26-planbuild-decimal-comma.blueprint"));
            Require(File.ReadAllBytes(fixture).SequenceEqual(Encoding.UTF8.GetBytes(text)) &&
                ExternalBlueprintLibrary.LoadFile(fixture).Single().importError == null,
                "Ready PlanBuild comma fixture differs from the tested BOM/CRLF source or fails actual file import");
            var culture = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new[] { "en-US", "ru-RU" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    var parsed = ExternalBlueprintLibrary.Parse(text, "comma.blueprint");
                    Require(parsed.importError == null && parsed.parts.Count == 1,
                        "PlanBuild decimal comma transforms rejected: " + parsed.importError);
                    var part = parsed.parts.Single();
                    Require(part.position.x == 3.25f && part.position.y == -4.5f && part.position.z == .125f &&
                        part.rotation.x == 0 && part.rotation.z == 0 && part.rotation.y > .7071f && part.rotation.w > .7071f &&
                        part.scale.x == 1.5f && part.scale.y == .5f && part.scale.z == 2.25f &&
                        part.scaleIsAbsolute && part.signText == "Привет, мир",
                        "PlanBuild comma changed transform semantics or sign punctuation");
                    var prepared = ExternalBlueprintLibrary.PrepareForHost(parsed,
                        _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
                    string path = Path.Combine(directory, "planbuild-comma-" + name + ".json");
                    var store = new CompositeBlueprintStore(path);
                    Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                    var reopened = new CompositeBlueprintStore(path).All().Single();
                    var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
                    foreach (var copy in new[] { reopened, portable })
                        Require(copy.parts.Single().position.x == 3.25f && copy.parts[0].rotation.y == part.rotation.y &&
                            copy.parts[0].baseScale.Value.x == 1.5f && copy.parts[0].baseScale.Value.y == .5f &&
                            copy.parts[0].baseScale.Value.z == 2.25f && copy.parts[0].scale.x == 1 &&
                            copy.parts[0].signText == "Привет, мир" && copy.parts[0].sourceLine == 3 &&
                            copy.parts[0].sourceDocumentId == parsed.sourceDocuments.Single().id &&
                            copy.sourceDocuments.Single().ToGeometry().Text == text,
                            "PlanBuild comma save/portable reopen lost transforms, punctuation or exact BOM/CRLF source");
                    Require(parsed.sourceDocuments.Single().text == text && parsed.parts[0].scaleIsAbsolute,
                        "Preparing PlanBuild comma modified the original");
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }

            var opaque = ExternalBlueprintLibrary.Parse("#Pieces\nwood,wall;;1,5;0;0;0;0;0;1;info,raw;1;1;1;zdo,raw;0,5", "opaque.blueprint");
            Require(opaque.importError == null && opaque.parts.Single().prefabName == "wood,wall" &&
                opaque.parts[0].foreignData.info == "info,raw" && opaque.parts[0].foreignData.zdo == "zdo,raw" &&
                opaque.parts[0].foreignData.chance == "0,5", "Comma normalization changed unknown identifiers or object payloads");
            foreach (string token in new[] { "", "1,2,3", "1,2.3", "NaN", "Infinity", "1e1000", "513,0" })
            {
                var refused = ExternalBlueprintLibrary.Parse("#Pieces\n" + row + "\n" + row.Replace("3,25", token), "bad.blueprint");
                Require(refused.importError != null && refused.parts.Count == 0, "Malformed PlanBuild number accepted: " + token);
            }
            Require(ExternalBlueprintLibrary.Parse("#Pieces\n" + row + "\n#SnapPoints\n1,5;0;0", "snap.blueprint").importError != null,
                "PlanBuild transform adapter changed the strict snap-point dialect");
            Console.WriteLine("PASS PlanBuild comma transforms: en-US/ru-RU, position/rotation/scale, exact source and native/portable reopen; payloads and snap dialect unchanged");
        }

        private static void NumericParity()
        {
            foreach (string token in new[] { "0", "1", "-1", "-0", " 1 ", "+1", "1e0", "1.0", ".5", "1e-10", "-1.5e2", "1,0", "-1,5" })
                Check(token, false);
            foreach (string token in new[] { "", "0", "1", "-1", "-0", "+1", "1e0", "1,0", "-1,5" })
                Check(token, true);
            foreach (string token in new[] { "NaN", "Infinity", "-Infinity", "1e1000", "--1", "0x1", "1x" })
            foreach (bool legacy in new[] { false, true })
            {
                var rejected = ExternalBlueprintLibrary.Parse(Row(token, legacy), legacy ? "numeric.vbuild" : "numeric.blueprint");
                Require(rejected.importError != null && rejected.parts.Count == 0, "Invalid numeric token accepted: " + token);
            }
            var empty = ExternalBlueprintLibrary.Parse(Row("", false), "numeric.blueprint");
            Require(empty.importError != null && empty.parts.Count == 0, "Blueprint dialect accepted legacy empty zero slot");
            Console.WriteLine("PASS imported numeric parity: exact float bits, signed zero/whitespace/exponents, legacy comma/empty, invalid/overflow refusal and exact source");

            void Check(string token, bool legacy)
            {
                string text = Row(token, legacy);
                var parsed = ExternalBlueprintLibrary.Parse(text, legacy ? "numeric.vbuild" : "numeric.blueprint");
                float expected = legacy && token == "" ? 0 : float.Parse(token.Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture);
                Require(parsed.importError == null && parsed.parts.Count == 1 &&
                    BitConverter.GetBytes(parsed.parts[0].position.x).SequenceEqual(BitConverter.GetBytes(expected)) &&
                    parsed.parts[0].rotation.w == 1 && parsed.sourceDocuments.Single().text == text,
                    "Imported numeric semantics changed: " + token + " legacy=" + legacy + " error=" + parsed.importError);
            }
            string Row(string token, bool legacy) => legacy
                ? "#Name:Numeric\nwoodwall 0 0 0 1 " + token + " 0 0\n"
                : "#Name:Numeric\n#Pieces\nwoodwall;Workbench;" + token + ";0;0;0;0;0;1;\"\";1;1;1\n";
        }
        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
