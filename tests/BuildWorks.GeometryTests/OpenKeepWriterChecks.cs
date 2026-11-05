using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class OpenKeepWriterChecks
    {
        internal static void Run()
        {
            const string site = "\"site\":{\"terrain\":[{\"op\":\"level\",\"at\":[1,2],\"half\":3,\"y\":-1}]," +
                "\"water\":{\"floor\":-4,\"depth\":2},\"relief\":{\"x\":0,\"z\":0,\"width\":2,\"depth\":2,\"offsets\":[0,1,2,3]}," +
                "\"clear_radius\":7,\"stand\":[0,-3],\"limits\":\"vanilla\"}";
            const string provenance = "\"source\":\"original.py\",\"copied_from\":{\"at\":[100,200],\"ground\":30,\"facing\":90}," +
                "\"marks\":{\"door\":[1,2,3]},\"aims\":{\"camera\":[3,2,1]},\"mark_notes\":{\"_frame\":\"original frame\"}," +
                "\"unknown_root\":{\"literal\":\" keep  \\u0061 \"}";
            string original = "\uFEFF{\r\n\"name\":\"Original\",\r\n\"pieces\":[[\"wood_floor\",0,0,0,0]," +
                "[\"wood_floor\",1,0,0,90, {\"unknown_state\":\"exact  \\u0061\"} ]],\r\n" + site + ",\r\n" + provenance + "\r\n}";
            var source = OpenKeepBlueprint.Parse(original, "fallback");
            var edited = new[] {
                (Prefab: "wood_floor", Position: new Point3(4.25, .05, -7), Yaw: -90d, Record: 2),
                (Prefab: "wood_wall", Position: new Point3(0, 2, 0), Yaw: 180d, Record: 0),
                (Prefab: "wood_floor", Position: new Point3(9, 0, 0), Yaw: 90d, Record: 2) };
            string output = source.WritePieces(edited, "Изменено \"дом\"");
            var parsed = OpenKeepBlueprint.Parse(output, "other");
            Require(source.Text == original && source.Pieces.Count == 2 && output.StartsWith("\uFEFF{\r\n") &&
                output.Contains(site) && output.Contains(provenance), "Original evidence/root/site bytes changed");
            Require(parsed.Name == "Изменено \"дом\"" && parsed.Pieces.Count == 3 && parsed.Pieces[0].Position.X == 4.25 &&
                parsed.Pieces[0].Position.Z == -7 && parsed.Pieces[0].Yaw == -90 && parsed.Pieces[1].Prefab == "wood_wall", "Edited tuples/name lost");
            const string rawTail = ", {\"unknown_state\":\"exact  \\u0061\"} ";
            Require(output.Split(new[] { rawTail }, StringSplitOptions.None).Length == 3, "Raw tail escapes/whitespace changed during copying");
            using (var json = JsonDocument.Parse(output.TrimStart('\uFEFF')))
            {
                var pieces = json.RootElement.GetProperty("pieces");
                Require(pieces[0].GetArrayLength() == 6 && pieces[1].GetArrayLength() == 5 && pieces[2].GetArrayLength() == 6 &&
                    pieces[0][5].GetProperty("unknown_state").GetString() == "exact  a" &&
                    pieces[2][5].GetProperty("unknown_state").GetString() == "exact  a", "Unknown record tail lost on reorder/copy");
            }
            foreach (string text in new[] { "{\"pieces\":[[\"x\",0,0,0,0]],\"name\":\"After\"}",
                " {\"pieces\":[[\"x\",0,0,0,0]]} ", "{\"name\":null,\"pieces\":[[\"x\",0,0,0,0]]}" })
            {
                var model = OpenKeepBlueprint.Parse(text, "fallback");
                string renamed = model.WritePieces(new[] { ("x", new Point3(2, 3, 4), 45d, 1) }, "New");
                Require(OpenKeepBlueprint.Parse(renamed, "fallback").Name == "New", "Missing/late/null name replacement failed");
                Require(model.WritePieces(new[] { ("x", new Point3(0, 0, 0), 0d, 1) }).Contains(text.Contains("After") ? "\"After\"" : text.Contains("null") ? "\"name\":null" : "\"pieces\""), "Optional rename changed source metadata");
            }
            foreach (var input in new[] {
                new[] { ("x", new Point3(double.NaN, 0, 0), 0d, 0) },
                new[] { ("x", new Point3(0, 0, 0), double.PositiveInfinity, 0) },
                new[] { ("x", new Point3(0, 0, 0), 0d, 3) },
                new[] { ("x", new Point3(0, 0, 0), 0d, -1) } })
            {
                bool refused = false; try { source.WritePieces(input); } catch (ArgumentException) { refused = true; }
                Require(refused && source.Text == original, "Invalid writer input accepted/mutated original");
            }
            bool tooMany = false;
            try { source.WritePieces(Enumerable.Repeat(("x", new Point3(0, 0, 0), 0d, 0), BlueprintEditorDocument.MaximumParts + 1)); }
            catch (ArgumentException) { tooMany = true; }
            Require(tooMany && source.Text == original, "Writer count limit was not atomic");
            string unicodeTail = "{\"pieces\":[[\"x\",0,0,0,0,\"" + new string('é', 1100000) + "\"]]}";
            var largeSource = OpenKeepBlueprint.Parse(unicodeTail, "source");
            bool utf8Limited = false;
            try { largeSource.WritePieces(Enumerable.Repeat(("x", new Point3(0, 0, 0), 0d, 1), 4)); }
            catch (FormatException error) when (error.Message == "OpenKeep: JSON too large") { utf8Limited = true; }
            Require(utf8Limited && largeSource.Text == unicodeTail, "UTF8 output limit after cloning unknown tail was not atomic");
            Console.WriteLine("PASS OpenKeep writer preserves root/site/provenance/raw tails while editing/reordering/copying tuples;synthetic check,not original producer execution");
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("OpenKeep writer: " + message); }

        internal static int Corpus(string directory, string outputDirectory)
        {
            Run();
            string inputRoot = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string outputRoot = Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Require(!outputRoot.StartsWith(inputRoot, StringComparison.OrdinalIgnoreCase) &&
                (!Directory.Exists(outputRoot) || !Directory.EnumerateFileSystemEntries(outputRoot).Any()), "Choose a fresh output directory outside inputs");
            string[] inputs = Directory.GetFiles(directory, "*.json", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            var culture = CultureInfo.CurrentCulture;
            int count = 0, parts = 0;
            try
            {
                foreach (string path in inputs)
                {
                    byte[] before = File.ReadAllBytes(path);
                    string text = new UTF8Encoding(false, true).GetString(before);
                    var source = OpenKeepBlueprint.Parse(text, Path.GetFileNameWithoutExtension(path));
                    var tuples = source.Pieces.Select(p => (p.Prefab, p.Position, p.Yaw, p.Record)).ToArray();
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("en-US");
                    string exported = source.WritePieces(tuples);
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                    Require(exported == source.WritePieces(tuples), "Culture changed export");
                    using (var input = JsonDocument.Parse(text.TrimStart('\uFEFF')))
                    using (var output = JsonDocument.Parse(exported.TrimStart('\uFEFF')))
                    {
                        foreach (var property in input.RootElement.EnumerateObject())
                            if (property.Name != "pieces") Require(property.Value.GetRawText() == output.RootElement.GetProperty(property.Name).GetRawText(), "Root/site/provenance value changed: " + property.Name);
                        var actual = output.RootElement.GetProperty("pieces");
                        int record = 0;
                        foreach (var expected in input.RootElement.GetProperty("pieces").EnumerateArray())
                        {
                            var row = actual[record++];
                            Require(row[0].GetString() == expected[0].GetString(), "Prefab changed");
                            for (int slot = 1; slot < 5; ++slot) Require(row[slot].GetSingle() == expected[slot].GetSingle(), "Float pose changed");
                        }
                        Require(actual.GetArrayLength() == record, "Record count changed");
                    }
                    Require(File.ReadAllBytes(path).SequenceEqual(before) && source.Text == text, "Original input/evidence changed");
                    string folder = Path.Combine(outputRoot, count.ToString(CultureInfo.InvariantCulture));
                    Directory.CreateDirectory(folder);
                    File.WriteAllText(Path.Combine(folder, Path.GetFileName(path)), exported, new UTF8Encoding(false, true));
                    ++count; parts += source.Pieces.Count;
                }
            }
            finally { CultureInfo.CurrentCulture = culture; }
            Require(count == 23 && parts == 17975, "Writer author corpus count changed");
            Console.WriteLine("PASS actual OpenKeep writer23paths/17975tuples,en-US+ru-RU,all non-piece root values exact,originals unchanged;outputs prepared for original reader");
            return 0;
        }
    }
}
