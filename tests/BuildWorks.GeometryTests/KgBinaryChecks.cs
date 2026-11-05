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
    internal static class KgBinaryChecks
    {
        internal static void Run()
        {
            byte[] original = Fixture();
            var model = KgBlueprint.ParseBinary(original);
            Require(model.IsBinary && model.Name == "Бинарный" && model.Author == null && model.Icon == "" && model.HasPreviewList,
                "Binary defaults/metadata changed");
            Require(model.Objects[0].PrefabHash == -123 && model.Objects[1].PrefabHash == null &&
                model.Objects[1].PrefabName == "706748652" && !model.Objects[1].MatchesPrefab("kg_hash_706748652"),
                "Binary string/int identity branches merged");
            Require(model.WriteBinaryObjects(model.Objects).SequenceEqual(original), "Binary writer changed unedited bytes");
            Require(BitConverter.SingleToInt32Bits((float)model.BoxRotation.X) == int.MinValue, "Negative zero wire bits lost");
            Require(KgBlueprint.ParseBinaryText(model.Text).BinaryBytes().SequenceEqual(original), "Binary envelope lost bytes");
            var exposed = model.BinaryBytes(); exposed[0] ^= 1; original[0] ^= 1;
            Require(model.BinaryBytes().SequenceEqual(Fixture()), "Binary model aliases mutable input/output");
            Reject(() => model.WriteObjects(model.Objects));
            var yaml = KgBlueprint.Parse("Name: crossing\nObjects: [{Id: one}, {Id: two}]\n");
            Reject(() => yaml.WriteObjects(model.Objects));
            Reject(() => yaml.WriteObjects(new[] { new KgBlueprint.Piece(0, "\u061c-123", new Point3(0,0,0), new Point3(0,0,0), null, null, true) }));
            Reject(() => model.WriteBinaryObjects(new[] { new KgBlueprint.Piece(0, "named", new Point3(0,0,0), new Point3(0,0,0), prefab: "not-in-wire") }));
            Reject(() => KgBlueprint.ParseBinaryText(model.Text + "\n"));
            Reject(() => KgBlueprint.ParseBinaryText(" " + model.Text.Substring(1)));
            byte[] small = Fixture();
            for (int i = 0; i < small.Length; ++i)
            { int cut = i; Reject(() => KgBlueprint.ParseBinary(small.Take(cut).ToArray())); }
            Reject(() => KgBlueprint.ParseBinary(small.Concat(new byte[] { 0 }).ToArray()));
            Reject(() => KgBlueprint.ParseBinary(new byte[] { 128, 0 }));
            Reject(() => KgBlueprint.ParseBinary(new byte[] { 255, 255, 255, 255, 8 }));
            Reject(() => KgBlueprint.ParseBinary(new byte[] { 1, 255 }));
            Reject(() => KgBlueprint.ParseBinary(Fixture(objectCount: -1)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(objectCount: 50001)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(idBoolean: 2)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(zdoBoolean: 2)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(previewCount: -1)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(previewCount: 33)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(coordinate: float.NaN)));
            Reject(() => KgBlueprint.ParseBinary(Fixture(coordinate: float.PositiveInfinity)));
            Reject(() => KgBlueprint.ParseBinary(new byte[BlueprintSourceDocument.MaximumBytes + 1]));
            foreach (string culture in new[] { "en-US", "ru-RU", "ar-SA" })
            {
                var saved = CultureInfo.CurrentCulture;
                try
                {
                    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
                    var parsed = KgBlueprint.ParseBinary(small);
                    Require(parsed.WriteBinaryObjects(parsed.Objects).SequenceEqual(small), "Binary became culture-dependent");
                }
                finally { CultureInfo.CurrentCulture = saved; }
            }
            SourceEvidence();
        }

        private static void SourceEvidence()
        {
            byte[] bytes = Fixture();
            var source = new BlueprintSourceDocument(KgBlueprint.BinaryFormat, "own.oprint", Convert.ToBase64String(bytes));
            Require(source.ByteCount == bytes.Length && source.OriginalBytes().SequenceEqual(bytes) &&
                source.HasPartAddress(0, 2, "706748652") && !source.HasPartAddress(0, 2, "kg_hash_706748652") &&
                !source.HasPartAddress(1, 2, "706748652"), "Binary raw quota/record binding lost");
            var other = new BlueprintSourceDocument(KgBlueprint.BinaryFormat, "other.oprint", source.Text);
            Require(other.Id != source.Id, "Binary source name absent from identity");
            Reject(() => new BlueprintSourceDocument(KgBlueprint.Format, "own.oprint", source.Text));
            Reject(() => new BlueprintSourceDocument("blueprint", "legacy.blueprint", new string('x', BlueprintSourceDocument.MaximumBytes + 1)));
            Reject(() => BlueprintSourceDocument.Index(Enumerable.Range(0, 65).Select(i =>
                new BlueprintSourceDocument(KgBlueprint.BinaryFormat, i + ".oprint", source.Text))));
            int baseLength = Fixture(description: "").Length;
            // A large string adds three more length-prefix bytes than an empty one.
            byte[] maximum = Fixture(description: new string('A', BlueprintSourceDocument.MaximumBytes - baseLength - 3));
            Require(maximum.Length == BlueprintSourceDocument.MaximumBytes, "8MiB boundary fixture differs");
            var full = new BlueprintSourceDocument(KgBlueprint.BinaryFormat, "maximum.oprint", Convert.ToBase64String(maximum));
            Require(full.ByteCount == BlueprintSourceDocument.MaximumBytes && full.Text.Length == KgBlueprint.MaximumBinaryTextLength &&
                full.OriginalBytes().SequenceEqual(maximum), "8MiB raw source/envelope boundary lost");
            Require(full.Kg.WriteBinaryObjects(full.Kg.Objects).SequenceEqual(maximum), "8MiB binary writer boundary differs");
            Reject(() => full.Kg.WriteBinaryObjects(full.Kg.Objects, full.Kg.Name + "x"));
            Require(BlueprintSourceDocument.Index(Enumerable.Range(0, 3).Select(i =>
                new BlueprintSourceDocument(KgBlueprint.BinaryFormat, i + ".oprint", full.Text))).Count == 3, "24MiB decoded aggregate rejected");
            Reject(() => BlueprintSourceDocument.Index(Enumerable.Range(0, 4).Select(i =>
                new BlueprintSourceDocument(KgBlueprint.BinaryFormat, i + ".oprint", full.Text))));
        }

        internal static int Capture(string path, string receiptPath)
        {
            byte[] bytes = File.ReadAllBytes(path);
            var model = KgBlueprint.ParseBinary(bytes);
            using var receipt = JsonDocument.Parse(File.ReadAllText(receiptPath));
            bool control = Path.GetFileName(path) == "controlled-kg-writer.oprint";
            var expected = receipt.RootElement.GetProperty(control ? "ControlReadExpected" : "CaptureReadExpected");
            Compare(model, expected);
            Require(model.WriteBinaryObjects(model.Objects).SequenceEqual(bytes), "Actual producer bytes differ after unedited write");
            Require(model.BinaryBytes().SequenceEqual(bytes), "Actual original bytes changed");
            var reversed = model.Objects.Reverse().ToArray();
            var written = KgBlueprint.ParseBinary(model.WriteBinaryObjects(reversed, "Edited binary"));
            Require(written.Name == "Edited binary" && written.Objects.Select(p => p.Id).SequenceEqual(reversed.Select(p => p.Id)) &&
                written.Objects.Select(p => p.PrefabHash).SequenceEqual(reversed.Select(p => p.PrefabHash)), "Edited writer lost order/Id branch");
            Console.WriteLine(JsonSerializer.Serialize(new { passed = true, path, bytes = bytes.Length,
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), records = model.Objects.Count,
                previews = model.Previews.Count, exactUneditedBytes = true, metadataFields = true, float32Bits = true,
                jsonReceiptPreservesSignedZero = false,
                idBranches = true, rawZdo = true, writerReorder = true, originalConsumerOfOwnWriterExecuted = false }));
            return 0;
        }

        private static void Compare(KgBlueprint model, JsonElement expected)
        {
            foreach (var entry in new[] { ("Name", model.Name), ("Author", model.Author), ("Description", model.Description) })
                Require(expected.GetProperty(entry.Item1).GetString() == entry.Item2, "Actual metadata mismatch " + entry.Item1);
            Vector(model.BoxRotation, expected.GetProperty("BoxRotation"));
            var rows = expected.GetProperty("Objects").EnumerateArray().ToArray();
            Require(rows.Length == model.Objects.Count, "Actual record count mismatch");
            for (int i = 0; i < rows.Length; ++i)
            {
                var part = model.Objects[i]; var row = rows[i];
                string id = row.GetProperty("IdString").GetString();
                Require(id == null ? part.PrefabHash == row.GetProperty("IdInt").GetInt32() :
                    part.PrefabHash == null && part.Id == id, "Actual Id branch mismatch");
                Require(part.ZdoData == row.GetProperty("ZDOData").GetString(), "Actual raw ZDO mismatch");
                Vector(part.Position, row.GetProperty("Position")); Vector(part.Euler, row.GetProperty("Rotation"));
            }
            string HashImage(string text) => Convert.ToHexString(SHA256.HashData(Convert.FromBase64String(text)));
            Require(HashImage(model.Icon) == expected.GetProperty("IconSHA256").GetString(), "Actual icon hash mismatch");
            Require(model.Previews.Select(HashImage).SequenceEqual(expected.GetProperty("PreviewSHA256").EnumerateArray().Select(x => x.GetString())),
                "Actual preview hashes mismatch");
        }
        private static void Vector(Point3 value, JsonElement expected)
        {
            var fields = expected.EnumerateArray().Select(x => BitConverter.SingleToInt32Bits(x.GetSingle())).ToArray();
            var actual = new[] { value.X, value.Y, value.Z }.Select(x => BitConverter.SingleToInt32Bits((float)x)).ToArray();
            // DataContract JSON loses -0; exact wire preservation is checked separately by byte equality.
            Require(fields.Zip(actual, (a, b) => a == b || (a & int.MaxValue) == 0 && (b & int.MaxValue) == 0).All(x => x),
                "Actual float32 mismatch beyond receipt signed-zero loss");
        }
        private static byte[] Fixture(int objectCount = 2, byte idBoolean = 0, byte zdoBoolean = 0, int previewCount = 0, float coordinate = -0.0f, string description = "description")
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true);
            writer.Write("Бинарный"); writer.Write(""); writer.Write(description);
            writer.Write(coordinate); writer.Write(45f); writer.Write(20f); writer.Write(objectCount);
            for (int i = 0; i < 2; ++i)
            {
                writer.Write(i == 0 ? idBoolean : (byte)1);
                if (i == 0) writer.Write(-123); else writer.Write("706748652");
                for (int j = 0; j < 6; ++j) writer.Write(j == 0 ? coordinate : (float)(j * 1.25));
                writer.Write(zdoBoolean);
            }
            writer.Write(previewCount); writer.Write(""); writer.Flush();
            return stream.ToArray();
        }
        private static void Reject(Action action)
        {
            try { action(); }
            catch (Exception error) when (error is FormatException || error is ArgumentException || error is InvalidOperationException) { return; }
            throw new Exception("Malformed/unsupported KG binary input accepted");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
