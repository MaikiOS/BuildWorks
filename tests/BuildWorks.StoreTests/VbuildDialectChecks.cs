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
            Require(ExternalBlueprintLibrary.Parse("woodwall;0;0;0;1;3,25;0;0", "strict.vbuild").importError != null &&
                ExternalBlueprintLibrary.Parse("#Pieces\nwoodwall;;3,25;0;0;0;0;0;1;\"\"", "strict.blueprint").importError != null,
                "Legacy numeric convention leaked into another dialect");
            string fixture = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "../../../../..", "tests/fixtures/vbuild/22-legacy-zero-comma.vbuild"));
            var ready = ExternalBlueprintLibrary.LoadFile(fixture).Single();
            Require(ready.importError == null && ready.name == "QA118 Legacy Vbuild" && ready.parts.Count == 2 &&
                ready.parts[0].position.x == 3.25f && ready.parts[1].rotation.w == 1,
                "Ready legacy vbuild fixture failed actual file import");
            Console.WriteLine("PASS legacy vbuild slots/comma decimals: invariant cultures, source-bound state, native/portable reopen and strict dialect boundaries");
        }

        private static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }
    }
}
