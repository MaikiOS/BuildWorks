using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;
using Bbp = OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class BetterBuildingJsonChecks
    {
        private static void Check(bool ok, string reason) { if (!ok) throw new Exception(reason); }
        private static void Reject(Action action, string reason)
        {
            try { action(); } catch (FormatException) { return; }
            throw new Exception("Accepted legacy JSON " + reason);
        }
        internal static void Run()
        {
            const string minimal = "{\"pieces\":[{\"prefab\":\"wood_floor\"}]}";
            var basic = Bbp.ParseJson(minimal);
            Check(basic.Text == minimal && !basic.IsBinary && basic.PayloadVersion == 1 && basic.Name == "Copied building" &&
                basic.Pieces[0].Record == 1 && basic.Pieces[0].Rotation.W == 1 && basic.Pieces[0].Position.X == 0, "Legacy defaults/raw/record");
            Check(Bbp.IsJsonBlueprint(minimal) && !Bbp.IsJsonBlueprint("{\"pieces\":[[\"x\",0,0,0,0]]}") &&
                !Bbp.IsJsonBlueprint("{\"config\":true}"), "Legacy discriminator/OpenKeep/config");
            const string openKeep = "{\"formatVersion\":1,\"pieces\":[[\"wood_floor\",0,0,0,0]]}";
            Check(OpenKeepBlueprint.Parse(openKeep, "x").UnknownPaths.Contains("/formatVersion") && !Bbp.IsJsonBlueprint(openKeep),
                "Valid OpenKeep tuple with unknown formatVersion must not become BetterBuilding");
            var defaults = Bbp.ParseJson("{\"formatVersion\":null,\"name\":null,\"pieces\":[{\"prefab\":\"x\",\"qw\":null}]}");
            Check(defaults.Name == basic.Name && defaults.PayloadVersion == 1 && defaults.Pieces[0].Rotation.W == 1, "NullValueHandling defaults");
            var casing = Bbp.ParseJson("{\"FORMATVERSION\":4,\"PIECES\":[{\"PREFAB\":\"x\",\"SCALE\":{}}]}");
            Check(casing.Pieces[0].Scale.Value.X == 1 && casing.UnknownPaths.Count == 0, "Case-insensitive producer fields/scale defaults");
            string full = "{\"formatVersion\":4,\"name\":\"Дом 😀\",\"$type\":\"inert\",\"pieces\":[{" +
                "\"prefab\":\"sign\",\"x\":-0.0,\"qx\":0,\"qy\":0.6,\"qz\":0,\"qw\":0.8,\"signText\":\"\"," +
                "\"attachment\":{\"prefab\":\"SwordIron\",\"variant\":255,\"orientation\":63,\"future\":1}," +
                "\"scale\":{\"x\":0.1,\"y\":10,\"z\":1.25},\"future\":{\"unknown\":1}}]," +
                "\"terrain\":{\"width\":2,\"spacing\":0.5,\"radius\":1,\"originX\":-1,\"originZ\":-1,\"heights\":[-0.0,0.25,-0.5,1]}}";
            var model = Bbp.ParseJson(full);
            Check(model.UnknownPaths.SequenceEqual(new[]{"/$type","/pieces/0/future","/pieces/0/attachment/future"}), "Unknown paths/metadata never executed");
            Check(model.Pieces[0].SignText == "" && model.Pieces[0].DisplayItem.Orientation == 63 && model.Terrain.Version == 1 &&
                BitConverter.SingleToInt32Bits((float)model.Pieces[0].Position.X) == int.MinValue && BitConverter.SingleToInt32Bits(model.Terrain.Heights[0]) == int.MinValue,
                "All known fields/terrain default/signed-zero decode");
            string output = Bbp.WriteJson(model.PayloadVersion, model.Name, model.Pieces, model.Terrain);
            var back = Bbp.ParseJson(output);
            Check(back.UnknownPaths.Count == 0 && model.Text == full && output.Contains("-0.0") &&
                Bbp.WriteBinary(4, model.Name, model.Pieces, model.Terrain).SequenceEqual(Bbp.WriteBinary(4, back.Name, back.Pieces, back.Terrain)),
                "Known writer exact float data; extensions remain in original only");
            for (int i = 0; i < minimal.Length; ++i) { int length = i; Reject(()=>Bbp.ParseJson(minimal.Substring(0,length)), "truncation"); }
            foreach (string text in new[] { "{}", "[]", "null", minimal+"{}", minimal.Replace("}]", "},]"),
                minimal.Replace("\"pieces\":", "\"pieces\":[],\"pieces\":"),
                minimal.Replace("\"prefab\":\"wood_floor\"", "\"prefab\":\"x\",\"PREFAB\":\"y\""),
                "{\"formatVersion\":5,\"pieces\":[{\"prefab\":\"x\"}]}", "{\"pieces\":[[\"x\",0,0,0,0]]}",
                "{\"pieces\":[null]}", "{\"pieces\":[]}", "{\"pieces\":[{\"prefab\":\"x\",\"qw\":0}]}",
                "{\"pieces\":[{\"prefab\":\"x\",\"x\":1e39}]}", minimal.Replace("wood_floor", "bad/path"),
                minimal.Replace("\"wood_floor\"", "true"), full.Replace("\"width\":2", "\"width\":2147483647"),
                full.Replace("\"heights\":[-0.0,0.25,-0.5,1]", "\"heights\":[0]"), full.Replace("\"variant\":255", "\"variant\":256") })
                Reject(()=>Bbp.ParseJson(text), "malformed/schema/version/bounds");
            for (int version = 1; version < 4; ++version)
            {
                int v = version;
                Reject(()=>Bbp.ParseJson(full.Replace("\"formatVersion\":4", "\"formatVersion\":"+v)), "version fields");
                Reject(()=>Bbp.WriteJson(v,model.Name,model.Pieces), "writer version fields");
            }
            var large = new Bbp.Piece(0, "sign", new Point3(), new Rotation3(0,0,0,1), new string('x',4096));
            Reject(()=>Bbp.WriteJson(3,"bounded",Enumerable.Repeat(large,2500)), "bounded streaming writer");
        }
        internal static int Corpus(string directory, string output)
        {
            if (Directory.Exists(output)) throw new IOException("Refusing to replace legacy JSON exports.");
            using var receipt = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,"receipt.json")));
            var inputs = receipt.RootElement.GetProperty("results").EnumerateArray().ToArray();
            Check(inputs.Length == 5 && receipt.RootElement.GetProperty("passed").GetBoolean(), "Five original JSON writer results required");
            Directory.CreateDirectory(output);
            foreach (var input in inputs)
            {
                string name = input.GetProperty("name").GetString();
                byte[] bytes = File.ReadAllBytes(Path.Combine(directory,name+".json"));
                Check(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)) == input.GetProperty("sha256").GetString() &&
                    bytes.Length == input.GetProperty("bytes").GetInt32(), "Original input pin differs");
                string text = new UTF8Encoding(false,true).GetString(bytes);
                var model = Bbp.ParseJson(text);
                using var dto = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(directory,name+".dto.json")));
                BetterBuildingChecks.Compare(model,dto.RootElement);
                string written = Bbp.WriteJson(model.PayloadVersion,model.Name,model.Pieces,model.Terrain);
                BetterBuildingChecks.Compare(Bbp.ParseJson(written),dto.RootElement);
                Check(model.Text == text && !model.IsBinary && model.UnknownPaths.Count == 0 && Bbp.IsJsonBlueprint(text), "Original text/type/classification changed");
                File.WriteAllText(Path.Combine(output,name+".json"),written,new UTF8Encoding(false,true));
                Console.WriteLine("PASS original legacy JSON " + name + " v" + model.PayloadVersion + " parts=" + model.Pieces.Count + " bytes=" + bytes.Length);
            }
            return 0;
        }
    }
}
