using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class KgYamlChecks
    {
        internal static void Run()
        {
            string simple = "Name: Test\nBoxRotation: {x: '1,25', y: 45, z: -0.5}\nObjects:\n- Id: sign\n  RelativePosition: {x: '1,25', y: -2.5, z: 3e-2}\n  Rotation: {x: 10, y: 270, z: 20}\n";
            var originalCulture = CultureInfo.CurrentCulture;
            Require(!KgBlueprint.Parse(simple).HasPreviewList && !KgBlueprint.Parse(simple+"Previews: null\n").HasPreviewList &&
                KgBlueprint.Parse(simple+"Previews: []\n").HasPreviewList,"Absent/null/empty KG preview-list state lost");
            try
            {
                foreach (string culture in new[] { "en-US", "ru-RU", "de-DE", "ar-SA", "fa-IR", "sv-SE" })
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    var model = KgBlueprint.Parse(simple);
                    Require(model.Name == "Test" && model.Objects.Count == 1 && model.Objects[0].Position.X == 1.25 &&
                        model.Objects[0].Euler.Z == 20 && model.Objects[0].Record == 1 && model.UnknownPaths.Count == 0, "Culture changed KG vectors");
                    var output = KgBlueprint.Parse(model.WriteObjects(model.Objects));
                    Compare(model, output);
                }
            }
            finally { CultureInfo.CurrentCulture = originalCulture; }
            var hash = KgBlueprint.Parse(simple.Replace("Id: sign", "Id: ' +123 '"));
            Require(hash.Objects[0].PrefabHash == 123 && hash.Objects[0].PrefabName == null, "Quoted numeric Id must follow KG int.TryParse");
            Require(KgBlueprint.Parse(simple.Replace("Id: sign", "Id: 2147483648")).Objects[0].PrefabName == "2147483648", "Overflow Id became an int");
            Require(KgBlueprint.Parse("Name: Defaults\nObjects: [{}]").Objects[0].Id == "0", "Native default IntOrString changed");
            Require(KgBlueprint.Parse("Name: V\nObjects: [{Id: sign, RelativePosition: {x: 1, x: 2}}]").Objects[0].Position.X == 2,
                "Native vector duplicate last-wins changed");
            string future = simple + "Author: |\n  One\n  Два\nDescription: ''\nFuture: {flag: true, number: 42, quoted: 'false', list: [null, 'null']}\n";
            future = future.Replace("  Rotation:", "  FutureObject: {enabled: false}\n  Rotation:")
                .Replace("y: -2.5", "FutureVector: {flag: true}, y: -2.5");
            var source = KgBlueprint.Parse(future);
            Require(source.UnknownPaths.Contains("/Future") && source.UnknownPaths.Contains("/Objects/0/FutureObject"), "Unknown fields disappeared");
            string emitted = source.WriteObjects(source.Objects, "Edited: name");
            var reloaded = KgBlueprint.Parse(emitted);
            Require(reloaded.Author == "One\nДва\n" && reloaded.Description == "" && reloaded.Name == "Edited: name" &&
                reloaded.UnknownPaths.SequenceEqual(source.UnknownPaths) && source.Text == future,
                "Metadata/raw/unknown paths changed");
            Require(emitted.Contains("'false'") && emitted.Contains("'null'") && emitted.Contains("true"), "Unknown scalar YAML types changed");
            Reject("foo: bar"); Reject("Name: X\nObjects: []"); Reject("Name: X\nName: Y\nObjects: [{Id: sign}]");
            Reject("Name: X\nObjects: [{Id: sign, Id: wood}]"); Reject(simple + "---\nName: Other\nObjects: [{Id: sign}]");
            Reject("Name: X\nObjects: &parts [{Id: sign}]\nOther: *parts"); Reject("Name: !!str X\nObjects: [{Id: sign}]");
            var flowComma = KgBlueprint.Parse(simple.Replace("'1,25'", "1,234.5"));
            Require(flowComma.Objects[0].Position.X == 1 && flowComma.UnknownPaths.Contains("/Objects/0/RelativePosition/234.5"),
                "Flow comma must stay a YAML separator, not a guessed decimal");
            Reject(simple.Replace("'1,25'", "'1,234.5'")); Reject(simple.Replace("'1,25'", ".inf"));
            Reject(simple.Replace("'1,25'", "NaN")); Reject(simple.Replace("Id: sign", "Id: {x: 1}"));
            Reject(simple.Replace("  Rotation:", "  ZDOData: AAAA\n  Rotation:")); Reject(simple + "Icon: not-base64!\n");
            Reject("Name: X\nObjects: [{Id: sign, Rotation: [0, 0, 0]}]");
            Reject("Name: X\nObjects: [{Id: sign}]\nFuture: " + new string('[', 40) + "0" + new string(']', 40));
            Zdo();
            ExportFrame();
            WriterBudget();
        }

        private static void ExportFrame()
        {
            var random=new Random(318);
            var rotations=new System.Collections.Generic.List<Rotation3>();
            foreach(double x in new[]{-90d,-89.99999,0,89.99999,90,180,359.99999})
                foreach(double y in new[]{-179d,0,135}) rotations.Add(KgBlueprintFrame.Euler(new Point3(x,y,37)));
            for(int i=0;i<500;i++) rotations.Add(new Rotation3(random.NextDouble()-.5,random.NextDouble()-.5,
                random.NextDouble()-.5,random.NextDouble()-.5).Normalized());
            foreach(var q in rotations)
            {
                var angles=KgBlueprintFrame.ToEuler(q);
                var f32=new Point3((float)angles.X,(float)angles.Y,(float)angles.Z);
                foreach(var axis in new[]{new Point3(1,0,0),new Point3(0,1,0),new Point3(0,0,1)})
                    Near(q.Rotate(axis),Matrix(f32,axis),2e-6);
                var negative=KgBlueprintFrame.ToEuler(new Rotation3(-q.X,-q.Y,-q.Z,-q.W));
                Near(angles,negative,1e-10);
                var position=new Point3(2,-3,7);var box=new Point3(10,45,20);
                var stored=KgBlueprintFrame.Stored(position,q,box);
                var reopened=KgBlueprintFrame.Canonical(new KgBlueprint.Piece(0,"sign",
                    new Point3((float)stored.Position.X,(float)stored.Position.Y,(float)stored.Position.Z),
                    new Point3((float)stored.Euler.X,(float)stored.Euler.Y,(float)stored.Euler.Z)),box);
                Near(position,reopened.Position,1e-6);
                foreach(var axis in new[]{new Point3(1,0,0),new Point3(0,1,0),new Point3(0,0,1)})
                    Near(q.Rotate(axis),reopened.Rotation.Rotate(axis),2e-6);
            }
            Console.WriteLine("PASS KG export frame: 521 quaternion/gimbal/sign/f32 cases, independent Z-X-Y matrix; not native Euler-branch identity");
            void Near(Point3 a,Point3 b,double tolerance)=>Require(Math.Abs(a.X-b.X)<tolerance && Math.Abs(a.Y-b.Y)<tolerance && Math.Abs(a.Z-b.Z)<tolerance,"KG export frame/orientation differs");
            Point3 Matrix(Point3 a,Point3 p)
            {
                double x=a.X*Math.PI/180,y=a.Y*Math.PI/180,z=a.Z*Math.PI/180;
                var rz=new Point3(p.X*Math.Cos(z)-p.Y*Math.Sin(z),p.X*Math.Sin(z)+p.Y*Math.Cos(z),p.Z);
                var rx=new Point3(rz.X,rz.Y*Math.Cos(x)-rz.Z*Math.Sin(x),rz.Y*Math.Sin(x)+rz.Z*Math.Cos(x));
                return new Point3(rx.X*Math.Cos(y)+rx.Z*Math.Sin(y),rx.Y,-rx.X*Math.Sin(y)+rx.Z*Math.Cos(y));
            }
        }

        internal static int WriterBudget()
        {
            string text = "Name: Budget\nObjects:\n- Id: sign\n  Future: [" + string.Join(",", Enumerable.Repeat("0", 500000)) + "]\n";
            var model = KgBlueprint.Parse(text);
            string single = model.WriteObjects(model.Objects);
            Require(KgBlueprint.Parse(single).Objects.Count == 1, "One large unknown subtree lost");
            try
            {
                string invalid = model.WriteObjects(new[] { model.Objects[0], model.Objects[0] });
                throw new InvalidOperationException("KG writer returned " + Encoding.UTF8.GetByteCount(invalid) +
                    " bytes with more nodes than its reader allows");
            }
            catch (FormatException error)
            {
                Require(error.Message.Contains("structure limit"), "Writer refused for a different reason than node budget");
            }
            Require(model.Text == text && model.Objects.Count == 1, "Writer budget refusal mutated original source");
            Console.WriteLine("PASS KG writer node-budget refusal before returning unreadable duplicate;single subtree/source preserved");
            return 0;
        }

        internal static int Capture(string directory, string outputDirectory = null)
        {
            if (outputDirectory != null)
            {
                Require(!Directory.Exists(outputDirectory) && !File.Exists(outputDirectory), "KG test output must be a new directory");
                Directory.CreateDirectory(outputDirectory);
            }
            using (var receipt = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "receipt.json"))))
            {
                foreach (var kind in new[] { "Capture", "Control" })
                {
                    string file = Path.Combine(directory, kind == "Capture" ? "original-kg-capture.yml" : "controlled-kg-writer.yml");
                    byte[] original = File.ReadAllBytes(file);
                    string digest = Convert.ToHexString(SHA256.HashData(original));
                    Require(digest == receipt.RootElement.GetProperty(kind + "SHA256").GetString(), "Actual KG file pin changed");
                    var model = KgBlueprint.Parse(new UTF8Encoding(false, true).GetString(original));
                    var expected = receipt.RootElement.GetProperty(kind + "Read");
                    Require(model.Name == expected.GetProperty("Name").GetString() && model.Author == expected.GetProperty("Author").GetString() &&
                        model.Description == expected.GetProperty("Description").GetString() && model.Objects.Count == 4 && model.UnknownPaths.Count == 0,
                        "Original KG metadata/shape mismatch");
                    Vector(model.BoxRotation, expected.GetProperty("BoxRotation"));
                    for (int i = 0; i < model.Objects.Count; ++i)
                    {
                        var actual = model.Objects[i]; var row = expected.GetProperty("Objects")[i];
                        Require(actual.Id == (row.GetProperty("IdString").GetString() ?? row.GetProperty("IdInt").GetInt32().ToString(CultureInfo.InvariantCulture)) &&
                            actual.ZdoData == row.GetProperty("ZDOData").GetString() && actual.Prefab == row.GetProperty("Prefab").GetString(), "Actual Id/state/prefab mismatch");
                        Vector(actual.Position, row.GetProperty("Position")); Vector(actual.Euler, row.GetProperty("Rotation"));
                        Require(BlueprintForeignZdo.TryReadKg(actual.ZdoData, out var state) && state.TryGet<long>(881008290, out long creator) && creator == 376043676,
                            "Actual KG ZDO framing/creator mismatch");
                        if (i < 2) Require(state.TryGet<int>(-1049702188, out int seed) && seed == (i == 0 ? 0 : 1234), "Actual material seed mismatch");
                        else Require(state.TryGet<string>(-871206003, out string text) && text == (i == 2 ? "BuildWorks QA 2.7" : ""), "Actual sign text mismatch");
                    }
                    if (kind == "Control")
                    {
                        Require(Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(model.Icon))) == expected.GetProperty("IconSHA256").GetString() &&
                            model.Previews.Count == 1 && model.Previews[0] == model.Icon, "Old PNG/control preview changed");
                    }
                    else Require(model.Icon == null && model.Previews.Count == 0, "Genuine capture invented images");
                    string emitted = model.WriteObjects(model.Objects);
                    Compare(model, KgBlueprint.Parse(emitted));
                    if (outputDirectory != null)
                        File.WriteAllText(Path.Combine(outputDirectory, kind.ToLowerInvariant() + "-reexport.yml"), emitted, new UTF8Encoding(false, true));
                    Require(original.SequenceEqual(File.ReadAllBytes(file)), "Source original bytes changed");
                }
            }
            Console.WriteLine("PASS KG genuine4/fullZDO + controlled writer: bit-exact original-reader vectors/metadata/Id/ZDO/oldPNG and own YAML re-emission;no game/product integration claim");
            return 0;
        }
        private static void Compare(KgBlueprint a, KgBlueprint b)
        {
            Require(a.Name == b.Name && a.Author == b.Author && a.Description == b.Description && a.Icon == b.Icon &&
                a.Previews.SequenceEqual(b.Previews) && a.Objects.Count == b.Objects.Count && a.BoxRotation.Equals(b.BoxRotation), "KG root changed on own writer roundtrip");
            foreach (var pair in a.Objects.Zip(b.Objects))
                Require(pair.First.Id == pair.Second.Id && pair.First.Position.Equals(pair.Second.Position) && pair.First.Euler.Equals(pair.Second.Euler) &&
                    pair.First.Prefab == pair.Second.Prefab && pair.First.ZdoData == pair.Second.ZdoData, "KG object changed on own writer roundtrip");
        }
        private static void Vector(Point3 value, JsonElement expected)
        {
            var components = new[] { value.X, value.Y, value.Z };
            for (int i = 0; i < 3; ++i)
                Require(BitConverter.SingleToInt32Bits((float)components[i]) == BitConverter.SingleToInt32Bits(expected[i].GetSingle()), "KG original-reader float32 mismatch");
        }
        private static void Zdo()
        {
            byte[] bytes;
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream, new UTF8Encoding(false, true)))
            {
                w.Write((byte)1); w.Write((byte)1); w.Write(1); w.Write(1.25f);
                w.Write((byte)1); w.Write((byte)1); w.Write(2); w.Write(1f); w.Write(2f); w.Write(3f);
                w.Write((byte)1); w.Write((byte)1); w.Write(3); w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f);
                w.Write((byte)1); w.Write((byte)1); w.Write(4); w.Write(-7);
                w.Write((byte)1); w.Write((byte)1); w.Write(5); w.Write(long.MaxValue);
                w.Write((byte)1); w.Write((byte)1); w.Write(6); w.Write("Привет\n");
                w.Write((byte)1); w.Write((byte)1); w.Write(7); w.Write(3); w.Write(new byte[] { 1, 2, 3 });
                bytes = stream.ToArray();
            }
            string base64 = Convert.ToBase64String(bytes);
            Require(BlueprintForeignZdo.TryReadKg(base64, out var data) && data.Count == 7 &&
                data.TryGet<float>(1, out float f) && f == 1.25f && data.TryGet<Point3>(2, out var v) && v.Z == 3 &&
                data.TryGet<Rotation3>(3, out var q) && q.W == 1 && data.TryGet<int>(4, out int n) && n == -7 &&
                data.TryGet<long>(5, out long l) && l == long.MaxValue && data.TryGet<string>(6, out string s) && s == "Привет\n" &&
                data.TryGet<byte[]>(7, out var b) && b.SequenceEqual(new byte[] { 1, 2, 3 }), "Seven KG ZDO sections mismatch");
            Require(data.TryGet<byte[]>(7, out var copy), "Missing KG byte-array section");
            copy[0] = 99; Require(data.TryGet<byte[]>(7, out var again) && again[0] == 1, "Mutable KG ZDO bytes leaked");
            var changes=new[]{new System.Collections.Generic.KeyValuePair<int,object>(6,"edited"),
                new System.Collections.Generic.KeyValuePair<int,object>(4,17),new System.Collections.Generic.KeyValuePair<int,object>(6,8)};
            string written=data.WriteKg(changes,new[]{new System.Collections.Generic.KeyValuePair<int,Type>(5,typeof(long))});
            Require(BlueprintForeignZdo.TryReadKg(written,out var updated) && updated.Count==7 &&
                updated.TryGet<string>(6,out string edited) && edited=="edited" && updated.TryGet<int>(6,out int sibling) && sibling==8 &&
                updated.TryGet<int>(4,out int editedInt) && editedInt==17 && !updated.HasHash(5) &&
                updated.TryGet<byte[]>(7,out var keptBytes) && keptBytes.SequenceEqual(new byte[]{1,2,3}) &&
                data.TryGet<string>(6,out string untouched) && untouched=="Привет\n" && data.Count==7,
                "KG rewrite lost typed values, omitted the wrong section or mutated its source");
            foreach(object invalid in new object[]{float.NaN,new Point3(double.MaxValue,0,0),1d,
                new string('x',BlueprintForeignPartData.MaximumContainerBytes+1),new byte[BlueprintForeignPartData.MaximumContainerBytes+1],"\ud800"})
            {
                bool refused=false;
                try{data.WriteKg(new[]{new System.Collections.Generic.KeyValuePair<int,object>(8,invalid)});}
                catch(ArgumentException){refused=true;}
                Require(refused,"Invalid KG rewrite value accepted");
            }
            Require(BlueprintForeignZdo.TryRead(Convert.ToBase64String(new byte[4]),out var legacy),"Legacy empty fixture invalid");
            bool wrongDialect=false;try{legacy.WriteKg();}catch(InvalidOperationException){wrongDialect=true;}
            Require(wrongDialect,"Legacy dialect silently treated as KG");
            for (int i = 0; i < bytes.Length; ++i) RejectZdo(bytes.Take(i).ToArray());
            RejectZdo(bytes.Concat(new byte[] { 0 }).ToArray());
            Require(!BlueprintForeignZdo.TryReadKg(" " + base64, out _), "KG Base64 whitespace accepted");
            Require(BlueprintForeignZdo.TryReadKg("AAAAAAAAAA==", out var empty) && empty.Count == 0, "Canonical empty seven-section payload");
            RejectZdo(new byte[] { 2, 0, 0, 0, 0, 0, 0 });
            RejectZdo(new byte[] { 1, 0, 0, 0, 0, 0, 0, 0 });
            RejectZdo(new byte[] { 1, 0x80, 0, 0, 0, 0, 0, 0, 0 });
            byte[] notFinite = (byte[])bytes.Clone(); Array.Copy(BitConverter.GetBytes(float.NaN), 0, notFinite, 6, 4); RejectZdo(notFinite);
            using (var stream = new MemoryStream())
            using (var w = new BinaryWriter(stream))
            {
                w.Write(new byte[] { 0, 0, 0, 1, 0x80, 0x80 });
                for (int i = 0; i < 128; ++i) { w.Write(i); w.Write(i * 3); }
                w.Write(new byte[] { 0, 0, 0 });
                Require(BlueprintForeignZdo.TryReadKg(Convert.ToBase64String(stream.ToArray()), out var large) && large.Count == 128 &&
                    large.TryGet<int>(127, out int last) && last == 381, "KG big-endian tagged count128 mismatch");
                Require(BlueprintForeignZdo.TryReadKg(large.WriteKg(),out var reencoded) && reencoded.Count==128 &&
                    reencoded.TryGet<int>(127,out int lastAgain) && lastAgain==381,"KG writer count128 changed");
            }
        }
        private static void Reject(string text)
        {
            try { KgBlueprint.Parse(text); }
            catch (FormatException) { return; }
            throw new InvalidOperationException("Malformed KG YAML was accepted: " + text.Substring(0, Math.Min(80, text.Length)));
        }
        private static void RejectZdo(byte[] bytes) => Require(!BlueprintForeignZdo.TryReadKg(Convert.ToBase64String(bytes), out var data) && data == null, "Malformed KG ZDO accepted");
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    }
}
