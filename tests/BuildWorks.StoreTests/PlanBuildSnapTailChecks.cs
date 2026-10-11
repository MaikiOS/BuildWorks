using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class PlanBuildSnapTailChecks
    {
        internal static void Run(string directory)
        {
            Directory.CreateDirectory(directory);
            string text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,
                "fixtures", "planbuild", "27-planbuild-snap-tail.blueprint"));
            var culture = CultureInfo.CurrentCulture;
            try
            {
                foreach (string name in new[] { "en-US", "ru-RU" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(name);
                    var source = ExternalBlueprintLibrary.Parse(text, "27-planbuild-snap-tail.blueprint");
                    Require(source.importError == null && source.parts.Count == 1 && source.customSnapPoints.Count == 2,
                        "PlanBuild snap trailing fields rejected: " + source.importError);
                    Require(source.customSnapPoints[0].x == 1.25f && source.customSnapPoints[0].y == .5f &&
                        source.customSnapPoints[0].z == -2.75f && source.customSnapPoints[1].x == -3 &&
                        source.customSnapPoints[1].y == 1 && source.customSnapPoints[1].z == 2,
                        "Trailing fields changed XYZ or point order");
                    var summary = CompositeBlueprintStore.InspectContents(source, ForeignZdoChecks.Hash);
                    Require(summary.IgnoredSnapPointRows == 2 && summary.Unknown == 0,
                        "Trailing-field warning count lost or confused with blocked object state");
                    var combined = new CompositeBlueprintStore.ContentsSummary();
                    combined.Add(summary); combined.Add(summary);
                    Require(combined.IgnoredSnapPointRows == 4, "Multi-document warning count lost");
                    var prepared = ExternalBlueprintLibrary.PrepareForHost(source,
                        _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 });
                    string path = Path.Combine(directory, name + ".json");
                    var store = new CompositeBlueprintStore(path);
                    Require(store.TryGetOrCreatePlacementCopy(prepared, out var saved, out string error), error);
                    Require(PlanBuildExport.Review(saved, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }).IgnoredSnapPointRows == 2,
                        "PlanBuild export lost the explicit archived-tail warning");
                    var reopened = new CompositeBlueprintStore(path).All().Single();
                    var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(saved)));
                    foreach (var copy in new[] { reopened, portable })
                        Require(copy.customSnapPoints.Count == 2 && copy.customSnapPoints[0].x == 1.25f &&
                            copy.customSnapPoints[1].z == 2 && copy.sourceDocuments.Single().ToGeometry().Text == text &&
                            CompositeBlueprintStore.InspectContents(copy, ForeignZdoChecks.Hash).IgnoredSnapPointRows == 2,
                            "Snap coordinates or exact trailing-field source lost after save/reopen/export");
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
            foreach (string row in new[] { "1;2", "NaN;0;0;tail", "Infinity;0;0;tail", "513;0;0;tail", "1,5;0;0;tail" })
            {
                var invalid = ExternalBlueprintLibrary.Parse("#SnapPoints\n" + row + "\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"", "invalid.blueprint");
                Require(invalid.importError != null && invalid.parts.Count == 0, "Invalid snap prefix accepted: " + row);
            }
            var vbuild = ExternalBlueprintLibrary.Parse("#SnapPoints\n0;0;0;tail\n#Pieces\nwoodwall;0;0;0;1;0;0;0", "unknown.vbuild");
            Require(vbuild.importError != null, "PlanBuild-only tail grammar leaked into vbuild");
            var longTail = ExternalBlueprintLibrary.Parse("#SnapPoints\n0;0;0;" + new string(';', 100000) +
                "\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"", "long.blueprint");
            Require(longTail.importError == null && longTail.customSnapPoints.Count == 1 &&
                longTail.sourceDocuments.Single().ToGeometry().IgnoredSnapPointRows == 1,
                "Large trailing payload became coordinates or changed warning count");
            var overflow = ExternalBlueprintLibrary.Parse("#SnapPoints\n" +
                string.Join("\n", Enumerable.Repeat("0;0;0;tail", CompositeBlueprintStore.MaximumCustomSnapPoints + 1)) +
                "\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\"", "overflow.blueprint");
            Require(overflow.importError != null && overflow.parts.Count == 0, "Trailing fields bypassed snap count guard");
            Console.WriteLine("PASS PlanBuild snap XYZ with archived trailing fields: cultures/native/portable/source and strict invalid-prefix/vbuild guards");
        }

        internal static void CheckActual(string source, string text, CompositeBlueprintStore.Blueprint parsed)
        {
            string name = Path.GetFileName(source);
            if (name != "hs_ashlands_house.blueprint" && name != "hs_ashlands_portalhub_dome.blueprint") return;
            Require(parsed.importError == null && parsed.parts.Count > 0 && parsed.customSnapPoints.Count > 0,
                "Actual extended-snap document still refused: " + source + " " + parsed.importError);
            Require(parsed.sourceDocuments.Single().ToGeometry().Text == text, "Actual source tail was rewritten");
            string first = text.Split('\n')[8].TrimEnd('\r');
            string[] fields = first.Split(';');
            Require(fields.Length == (name == "hs_ashlands_house.blueprint" ? 7 : 12) &&
                parsed.customSnapPoints[0].x == float.Parse(fields[0], CultureInfo.InvariantCulture) &&
                parsed.customSnapPoints[0].y == float.Parse(fields[1], CultureInfo.InvariantCulture) &&
                parsed.customSnapPoints[0].z == float.Parse(fields[2], CultureInfo.InvariantCulture),
                "Actual extended snap XYZ changed: " + name);
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
