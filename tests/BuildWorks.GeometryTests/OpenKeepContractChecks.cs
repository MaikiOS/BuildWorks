using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class OpenKeepContractChecks
    {
        internal static void Run()
        {
            const string piece = "[\"wood_floor\",1,2,3,90]";
            var basic = OpenKeepBlueprint.Parse("{\"pieces\":[" + piece + "," + piece + "]}", "fallback");
            Require(basic.Pieces.Count == 2 && basic.Name == "fallback" && basic.Pieces[1].Record == 2, "ordinal/fallback");
            Point3 turned = basic.Pieces[0].Rotation.Rotate(new Point3(1, 2, 3));
            Require(Math.Abs(turned.X - 3) < 1e-10 && Math.Abs(turned.Z + 1) < 1e-10 && turned.Y == 2, "yaw axes");
            Require(basic.DefaultPad.Half == .5 && basic.GroundOffset(1, 3) == 0, "default pivot pad");
            var emptyTerrain = OpenKeepBlueprint.Parse("{\"pieces\":[" + piece + "],\"site\":{\"level_half\":20,\"terrain\":[]}}", "x");
            Require(emptyTerrain.HasTerrain && emptyTerrain.DefaultPad.Half == .5, "terrain[] suppresses explicit half");
            var half = OpenKeepBlueprint.Parse("{\"pieces\":[],\"site\":{\"level_half\":20}}", "x");
            Require(half.DefaultPad.Half == 20 && half.GroundOffset(0, 0) == 0, "explicit half without pieces");
            string full = "{\"name\":\"\",\"description\":\"d\",\"source\":\"s.py\"," +
                "\"pieces\":[[\"wood_floor\",\"1\",2,3,-270]],\"marks\":{\"a\":[1,2,3]},\"aims\":{\"b\":[3,2,1]}," +
                "\"mark_notes\":{\"_frame\":\"ground\"},\"copied_from\":{\"at\":[100,200],\"ground\":30,\"facing\":90}," +
                "\"site\":{\"limits\":\"vanilla\",\"level_half\":50,\"clear\":\"per_level\",\"clear_radius\":10,\"stand\":[1,2]," +
                "\"water\":{\"floor\":-5,\"depth\":2},\"terrain\":[" +
                "{\"op\":\"level\",\"at\":[0,0],\"half\":10,\"y\":4}," +
                "{\"op\":\"level\",\"at\":[0,0],\"half\":3,\"y\":7}," +
                "{\"op\":\"paint\",\"at\":[0,0],\"half\":2,\"paint\":\"Paved\",\"shape\":\"circle\",\"clear_here\":true}]," +
                "\"relief\":{\"x\":0,\"z\":0,\"width\":2,\"depth\":2,\"offsets\":[0,2,4,6]}}}";
            var data = OpenKeepBlueprint.Parse(full, "x");
            Require(data.Text == full && data.Name == "" && data.Source == "s.py" && data.Limits == "vanilla", "root values/raw");
            Require(data.UnknownPaths.Count == 0 && data.Marks["a"].Y == 2 && data.Aims["b"].X == 3 && data.MarkNotes["_frame"] == "ground", "author markers");
            Require(data.CopiedFrom.At.X == 100 && data.CopiedFrom.At.Z == 200 && data.CopiedFrom.FrameYaw == 270 &&
                data.ClearRadius == 10 && data.Clear == "per_level" && data.Stand.Value.Z == 2 && data.Water.Floor == -5 && data.Water.Depth == 2, "site provenance/water");
            Require(data.Terrain[2].PaintCode == 3 && data.Terrain[2].ClearHere && data.Terrain[2].Shape == "circle", "paint/extensions");
            Require(data.GroundOffset(.5, .5) == 3 && data.GroundOffset(2, 2) == 7 && data.GroundOffset(8, 8) == 4 &&
                data.GroundOffset(11, 11) == null && data.DefaultPad == null, "relief/last-level/outside precedence");
            var unknown = OpenKeepBlueprint.Parse("{\"pieces\":[[\"x\",0,0,0,0,{\"state\":1}]],\"new/key\":{},\"site\":{\"future\":1}}", "x");
            Require(unknown.UnknownPaths.Contains("/pieces/0/5+") && unknown.UnknownPaths.Contains("/new~1key") &&
                unknown.UnknownPaths.Contains("/site/future"), "unknown paths/raw retention");
            var unicode = OpenKeepBlueprint.Parse("\uFEFF{\"name\":\"Дом \\uD83D\\uDE00\",\"pieces\":[],\"__type\":\"inert\",\"a\\u002fb\":null}", "x");
            Require(unicode.Name == "Дом 😀" && unicode.UnknownPaths.Contains("/__type") && unicode.UnknownPaths.Contains("/a~1b"), "Unicode/BOM/special keys");
            var stepPaths = OpenKeepBlueprint.Parse("{\"pieces\":[],\"site\":{\"terrain\":[" +
                "{\"op\":\"level\",\"at\":[0,0],\"half\":1,\"future\":1}," +
                "{\"op\":\"level\",\"at\":[0,0],\"half\":1,\"future\":2,\"shape\":\"future\"}]}}", "x");
            Require(stepPaths.UnknownPaths.SequenceEqual(new[] { "/site/terrain/0/future", "/site/terrain/1/future", "/site/terrain/1/shape" }),
                "Unknown terrain diagnostics lost their record index");
            int acceptedInvalid = 0;
            foreach (string invalid in new[] {
                "{}", "[]", "{\"pieces\":null}", "{\"pieces\":[{}]}", "{\"pieces\":[],\"pieces\":[]}",
                "{\"pieces\":[[\"x\",1e999,0,0,0]]}", "{\"pieces\":[[\"x\",\"NaN\",0,0,0]]}",
                "{\"pieces\":[[\"x\",true,0,0,0]]}", "{\"pieces\":[],\"site\":{\"level_half\":-1}}", full + "{}",
                full.Replace("\"width\":2", "\"width\":2.5"), full.Replace("\"width\":2", "\"width\":2147483647"),
                full.Replace("[0,2,4,6]", "[0,2,4]"), full.Replace("\"Paved\"", "\"lava\""),
                "{\"pieces\":[],\"u\":{\"a\":1,\"a\":2}}", "{\"pieces\":[],\"u\":NaN}",
                "{\"pieces\":[],}", "{\"pieces\":[],\"u\":01}", "{\"pieces\":[],\"u\":+1}", "{\"pieces\":[],\"u\":.1}",
                "{\"pieces\":[],\"u\":1.}", "{\"pieces\":[],\"u\":1e}", "{\"pieces\":[],\"u\":-01}",
                "{\"pieces\":[],\"u\":\"\\uD800\"}", "{\"pieces\":[],\"u\":\"\\x\"}",
                "{\"pieces\":[],\"u\":\"a\nb\"}", "{\"pieces\":[],\"u\":[0,]}",
                "{\"pieces\":[],\"u\":1,\"\\u0075\":2}", "{\"pieces\":[]}/*comment*/",
                "{\"pieces\":[],\"u\":" + new string('[', 40) + "0" + new string(']', 40) + "}" })
            {
                bool refused = false;
                try { OpenKeepBlueprint.Parse(invalid, "x"); }
                catch (Exception e) when (e is FormatException || e is System.Xml.XmlException || e is ArgumentException) { refused = true; }
                if (!refused) { ++acceptedInvalid; Console.WriteLine("INVALID ACCEPTED: " + invalid); }
            }
            Require(acceptedInvalid == 0, "invalid sources accepted: " + acceptedInvalid);
            Console.WriteLine("PASS OpenKeep contract: frame, ordinals, full known metadata/site, relief precedence, unknowns and malformed input");
        }

        internal static int Corpus(string directory)
        {
            Run();
            int count = 0, parts = 0;
            foreach (string path in Directory.EnumerateFiles(directory, "*.json", SearchOption.AllDirectories))
            {
                string text = File.ReadAllText(path);
                var value = OpenKeepBlueprint.Parse(text, Path.GetFileNameWithoutExtension(path));
                ++count; parts += value.Pieces.Count;
                Require(value.HasSite && value.Stand.HasValue && value.ClearRadius.HasValue && value.UnknownPaths.Count == 0, "actual known data: " + path);
                using var json = JsonDocument.Parse(text);
                JsonElement root = json.RootElement, site = root.GetProperty("site");
                Require(value.Text == text && value.Name == root.GetProperty("name").GetString() &&
                    value.Description == root.GetProperty("description").GetString(), "actual identity/text");
                int record = 0;
                foreach (JsonElement row in root.GetProperty("pieces").EnumerateArray())
                {
                    var part = value.Pieces[record++];
                    Require(part.Record == record && part.Prefab == row[0].GetString() && part.Position.X == row[1].GetSingle() &&
                        part.Position.Y == row[2].GetSingle() && part.Position.Z == row[3].GetSingle() && part.Yaw == row[4].GetSingle(), "actual complete tuple");
                }
                Require(value.ClearRadius == site.GetProperty("clear_radius").GetSingle() && value.Stand.Value.X == site.GetProperty("stand")[0].GetSingle() &&
                    value.Stand.Value.Z == site.GetProperty("stand")[1].GetSingle(), "actual clear/stand");
                Require(value.Limits == (site.TryGetProperty("limits", out var limits) ? limits.GetString() : null), "actual site limits");
                Require(value.Source == (root.TryGetProperty("source", out var source) ? source.GetString() : null), "actual source provenance");
            }
            Require(count == 23 && parts == 17975, "actual corpus count: " + count + "/" + parts);
            Console.WriteLine("PASS actual author corpus23paths/17975parts; decoder only, not library/editor/native acceptance");
            return 0;
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException("OpenKeep contract: " + message); }
    }
}
