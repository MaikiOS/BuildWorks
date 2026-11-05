using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class PlannerBlueprintChecks
    {
        private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        private static void Reject(Action action, string reason)
        { try { action(); } catch (ArgumentException) { return; } catch (FormatException) { return; } throw new Exception("Accepted " + reason); }
        internal static void Run(string directory)
        {
            foreach (string name in new[] { "catalog-yaw0", "catalog-yaw22-added" })
            {
                string original = File.ReadAllText(Path.Combine(directory, name + ".json"));
                var model = PlannerBlueprint.Parse(original);
                string[][] native = File.ReadAllLines(Path.Combine(directory, name + ".blueprint")).Where(l => l.Length != 0 && l[0] != '#').Select(l => l.Split(';')).ToArray();
                Check(model.Pieces.Count == native.Length && PlannerBlueprint.IsJsonBlueprint(original), "Original count/discriminator");
                for (int i = 0; i < native.Length; ++i)
                {
                    var p = model.Pieces[i]; var row = native[i]; double N(int k) => double.Parse(row[k], CultureInfo.InvariantCulture);
                    Check(p.Record == i + 1 && p.Prefab == row[0], "Original mapping/record");
                    Check(Math.Abs(p.Position.X - N(2)) < 5.1e-7 && Math.Abs(p.Position.Y - N(3)) < 5.1e-7 && Math.Abs(p.Position.Z - N(4)) < 5.1e-7, "Original origin/axes " + p.PieceId);
                    double error(int sign) => Math.Max(Math.Abs(p.Rotation.Y - sign * N(6)), Math.Abs(p.Rotation.W - sign * N(8)));
                    Check(N(5) == 0 && N(7) == 0 && Math.Min(error(1), error(-1)) < 5.1e-7, "Original base/yaw rotation " + p.PieceId);
                }
                Check(model.WritePieces(model.Pieces.Select(p => (p.Prefab, p.Position, p.Rotation, p.Record, p.LayerId))) == original, "No-edit exact bytes");
                var first = model.Pieces[0]; var shifted = first.Position + new Point3(1.25, 2.5, -3.75);
                string edited = model.WritePieces(new[] { (first.Prefab, shifted, first.Rotation, first.Record, first.LayerId),
                    (first.Prefab, shifted, first.Rotation, first.Record, first.LayerId) }, "Edited");
                var reopened = PlannerBlueprint.Parse(edited);
                Check(reopened.Title == "Edited" && reopened.Pieces.Count == 2 && reopened.Pieces[0].Uid == first.Uid && reopened.Pieces[0].Uid != reopened.Pieces[1].Uid, "Edit/delete/duplicate UID");
                Check(Math.Abs(reopened.Pieces[0].Position.X - shifted.X) < 1e-12 && Math.Abs(reopened.Pieces[0].Position.Y - shifted.Y) < 1e-12 && Math.Abs(reopened.Pieces[0].Position.Z - shifted.Z) < 1e-12, "Inverse edited frame");
                Reject(() => model.WritePieces(new[] { (first.Prefab, first.Position, new Rotation3(.1, 0, 0, 1), first.Record, first.LayerId) }), "lossy pitch export");
                Reject(() => model.WritePieces(new[] { ("unknown", first.Position, first.Rotation, first.Record, first.LayerId) }), "unknown output prefab");
                Check(model.Text == original, "Source mutated");
            }
            Core();
            Console.WriteLine("Planner original corpus:753 mappings/frames and no-edit/edit roundtrips PASS; no host/game proof.");
        }
        internal static void Core()
        {
            const string json = "{\"version\":3,\"title\":\"Дом 😀\",\"extra\":{\"$type\":\"inert\"},\"layers\":[{\"id\":\"main\",\"name\":\"Main\",\"custom\":[1,2]}],\"pieces\":[{\"uid\":\"one\",\"pieceId\":\"floor-2\",\"material\":\"wood\",\"position\":[0,0,0],\"rotation\":0,\"rotationStep\":0,\"metadata\":{\"answer\":42}}]}";
            var sample = PlannerBlueprint.Parse(json); var part = sample.Pieces[0];
            string clonedLayer = sample.WritePieces(new[] { (part.Prefab, part.Position, part.Rotation, 1, "copy") },
                layers: new[] { new PlannerBlueprint.Layer("main", "Main"), new PlannerBlueprint.Layer("copy", "Copy", "main") });
            using (var doc = JsonDocument.Parse(clonedLayer))
                Check(doc.RootElement.GetProperty("layers")[1].TryGetProperty("custom", out var custom) && custom[1].GetInt32() == 2,
                    "Copied layer lost unknown source fields");
            Check(typeof(PlannerBlueprint.Layer).GetConstructor(new[] { typeof(string), typeof(string) }) != null, "Old Layer constructor ABI removed");
            string emptyClone = sample.WritePieces(new[] { (part.Prefab, part.Position, part.Rotation, 1, "main") },
                layers: new[] { new PlannerBlueprint.Layer("main", "Main"), new PlannerBlueprint.Layer("empty", "Empty", "main") });
            using (var doc = JsonDocument.Parse(emptyClone))
                Check(doc.RootElement.GetProperty("layers")[1].GetProperty("custom")[1].GetInt32() == 2 && PlannerBlueprint.Parse(emptyClone).Pieces[0].LayerId == "main",
                    "Empty copied layer lost donor fields or reassigned pieces");
            string NewDocument() => PlannerBlueprint.WriteNew(new[] { (part.Prefab, part.Position, part.Rotation, 0, "main") }, "New", null, "native-identity");
            Check(NewDocument() == NewDocument() && PlannerBlueprint.Parse(NewDocument()).Title == "New", "New planner output not deterministic/valid");
            Reject(() => PlannerBlueprint.Parse("{\"version\":3,\"title\":\"Empty\",\"layers\":[{\"id\":\"main\",\"name\":\"Main\"}],\"pieces\":[]}"), "private template allowance escaped into Parse");
            Reject(() => sample.WritePieces(new[] { (part.Prefab, part.Position, part.Rotation, 1, "main") },
                layers: new[] { new PlannerBlueprint.Layer("main", "Main", "missing") }), "missing donor bypasses no-edit");
            var twoLayers = PlannerBlueprint.Parse(json.Replace("\"custom\":[1,2]}]", "\"custom\":[1,2]},{\"id\":\"other\",\"name\":\"Other\",\"custom\":[9]}]"));
            string replacedDonor = twoLayers.WritePieces(twoLayers.Pieces.Select(p => (p.Prefab, p.Position, p.Rotation, p.Record, p.LayerId)),
                layers: new[] { new PlannerBlueprint.Layer("main", "Main", "other"), new PlannerBlueprint.Layer("other", "Other") });
            using (var doc = JsonDocument.Parse(replacedDonor))
                Check(doc.RootElement.GetProperty("layers")[0].GetProperty("id").GetString() == "main" &&
                    doc.RootElement.GetProperty("layers")[0].GetProperty("custom")[0].GetInt32() == 9, "Donor ignored by no-edit shortcut or ID not patched");
            string stepSource = json.Replace("floor-2", "beam-26").Replace("\"rotation\":0", "\"rotation\":0.3").Replace("\"rotationStep\":0", "\"rotationStep\":2");
            var independentStep = PlannerBlueprint.Parse(stepSource); var beam = independentStep.Pieces[0];
            string renamed = independentStep.WritePieces(new[] { (beam.Prefab, beam.Position, beam.Rotation, 1, beam.LayerId) }, "Renamed");
            using (var before = JsonDocument.Parse(stepSource)) using (var after = JsonDocument.Parse(renamed))
                Check(before.RootElement.GetProperty("pieces")[0].GetRawText() == after.RootElement.GetProperty("pieces")[0].GetRawText(), "Rename-only changed untouched piece tokens/step");
            string Duplicated() => sample.WritePieces(new[] { (part.Prefab, part.Position, part.Rotation, 1, part.LayerId), (part.Prefab, part.Position, part.Rotation, 1, part.LayerId) });
            Check(Duplicated() == Duplicated(), "Same snapshot produced different duplicate UIDs");
            string Added() => sample.WritePieces(new[] { (part.Prefab, part.Position, part.Rotation, 0, part.LayerId) });
            Check(Added() == Added() && PlannerBlueprint.Parse(Added()).Pieces[0].Uid != part.Uid, "New UID is not deterministic/distinct");
            string boundaryBase = json.Replace("Дом 😀", "Ascii").Replace("\"uid\":\"one\"", "\"uid\":\"" + new string('/', 256) + "\"");
            const string paddingField = "\"padding\":\"\",";
            int paddingLength = BlueprintSourceDocument.MaximumBytes - Encoding.UTF8.GetByteCount(boundaryBase) - paddingField.Length;
            string boundary = boundaryBase.Replace("\"metadata\":{", "\"metadata\":{\"padding\":\"" + new string('a', paddingLength) + "\",");
            Check(Encoding.UTF8.GetByteCount(boundary) == BlueprintSourceDocument.MaximumBytes, "Boundary fixture size");
            var largest = PlannerBlueprint.Parse(boundary); var largestPart = largest.Pieces[0];
            Check(largest.WritePieces(new[] { (largestPart.Prefab, largestPart.Position, largestPart.Rotation, 1, largestPart.LayerId) }) == boundary, "Accepted8MiB no-edit expanded/rejected");
            Reject(() => PlannerBlueprint.Parse(boundary + " "), "8MiB+1 source");
            string output = sample.WritePieces(new[] { (part.Prefab, new Point3(1, 2, 3), part.Rotation, 1, "upper") }, layers: new[] { new PlannerBlueprint.Layer("main", "Переименован"), new PlannerBlueprint.Layer("upper", "Этаж") });
            using (var doc = JsonDocument.Parse(output))
            {
                var root = doc.RootElement; Check(root.GetProperty("extra").GetProperty("$type").GetString() == "inert" && root.GetProperty("layers")[0].GetProperty("custom")[1].GetInt32() == 2 && root.GetProperty("pieces")[0].GetProperty("metadata").GetProperty("answer").GetInt32() == 42, "Unknown fields lost");
            }
            Check(sample.UnknownPaths.Count == 3 && PlannerBlueprint.Parse(output).Pieces[0].LayerId == "upper", "Unknown paths/layer editing");
            using (var doc = JsonDocument.Parse(json))
            {
                string piece = doc.RootElement.GetProperty("pieces")[0].GetRawText();
                string layer = doc.RootElement.GetProperty("layers")[0].GetRawText();
                Reject(() => PlannerBlueprint.Parse(json.Replace(piece, piece + "," + piece)), "duplicate UID");
                Reject(() => PlannerBlueprint.Parse(json.Replace(layer, layer + "," + layer)), "duplicate layer ID");
            }
            using (var doc = JsonDocument.Parse(output))
            {
                string removedMain = output.Replace(doc.RootElement.GetProperty("layers")[0].GetRawText() + ",", "");
                Check(PlannerBlueprint.Parse(removedMain).Layers.Count == 1, "Explicit valid layer requires no main ID");
            }
            foreach (var culture in new[] { "ru-RU", "en-US" })
            {
                var before = CultureInfo.CurrentCulture;
                try { CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture); Check(PlannerBlueprint.Parse(output).Pieces[0].Position.Z == 3, "Culture"); }
                finally { CultureInfo.CurrentCulture = before; }
            }
            foreach (string invalid in new[] { json.Replace("\"version\":3", "\"version\":4"), json.Replace("floor-2", "future-piece"), json.Replace("\"wood\"", "\"future-material\""), json.Replace("[0,0,0]", "[1e999,0,0]"), json.Replace("[0,0,0]", "[0,0]"), json.Replace("\"rotationStep\":0", "\"rotationStep\":16"), json.Replace("\"uid\":\"one\"", "\"uid\":\"one\",\"layer\":\"missing\""), json + "{}", json.Replace("\"version\":3", "\"version\":3,\"version\":3") })
                Reject(() => PlannerBlueprint.Parse(invalid), "malformed/unsupported planner");
            Check(!PlannerBlueprint.IsJsonBlueprint("{\"pieces\":[{\"prefab\":\"wood_floor\"}]}") && !PlannerBlueprint.IsJsonBlueprint("{\"pieces\":[[\"wood_floor\",0,0,0,0]]}"), "Other JSON confused with planner");
            Console.WriteLine("Planner JSONv3:layers/opaque fields/cultures/invalid inputs PASS.");
        }
    }
}
