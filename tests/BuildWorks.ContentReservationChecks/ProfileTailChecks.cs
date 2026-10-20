using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static partial class ProfileChecks
    {
        internal static void WriteEmptyNativeTail(BinaryWriter writer, IDictionary<string, string> customData)
        {
            for (int i = 0; i < 8; i++) writer.Write(0); // Recipes/stations/five sets/texts.
            writer.Write(""); writer.Write("");
            for (int i = 0; i < 6; i++) writer.Write(0f);
            writer.Write(0); writer.Write(0); // Model/foods.
            writer.Write(2); writer.Write(0); // Skills version/count.
            writer.Write(customData.Count);
            foreach (var pair in customData) { writer.Write(pair.Key); writer.Write(pair.Value); }
            for (int i = 0; i < 3; i++) writer.Write(0f);
            writer.Write(0); // Empty native BuildUi byte array; caller writes PlayerData 33.
        }

        private sealed class TailFixture
        {
            internal readonly byte[] PlayerData;
            internal readonly Dictionary<string, int> Offsets = new Dictionary<string, int>();
            internal readonly Fixture Profile;
            internal const string Receipt = "ostrix.buildworks.collection.fixture";
            internal TailFixture(byte[] inventory, int version)
            {
                using (var stream = new MemoryStream())
                using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true)))
                {
                    Action<string> mark = key => Offsets.Add(key, (int)stream.Position);
                    writer.Write(version); for (int i = 0; i < 4; i++) writer.Write(10f);
                    writer.Write(""); writer.Write(0f); writer.Write(inventory); mark("tail");
                    mark("recipes"); writer.Write(2); writer.Write("wood_wall"); writer.Write("wood_floor");
                    mark("stations"); writer.Write(2); writer.Write("workbench"); writer.Write(3); writer.Write("forge"); writer.Write(2);
                    for (int i = 0; i < 5; i++)
                    { mark("set" + i); writer.Write(2); writer.Write("A"); mark("duplicateSet" + i); writer.Write("B"); }
                    mark("texts"); writer.Write(2); writer.Write(Receipt); writer.Write("decoy-text");
                    mark("otherText"); writer.Write("other-text"); writer.Write("unrelated");
                    mark("beard"); writer.Write("beard"); writer.Write("hair"); mark("color");
                    for (int i = 0; i < 6; i++) writer.Write(0.5f);
                    writer.Write(1); mark("foods"); writer.Write(2);
                    writer.Write("Raspberries"); writer.Write(42f); writer.Write("Meat"); writer.Write(91f);
                    mark("skillVersion"); writer.Write(2); mark("skills"); writer.Write(2);
                    writer.Write(1); mark("skillValue"); writer.Write(22f); writer.Write(0.2f);
                    mark("skillId"); writer.Write(4242); writer.Write(12f); writer.Write(0.3f); // Framing permits mod skill IDs.
                    mark("custom"); writer.Write(2); writer.Write(Receipt); mark("receiptValue"); writer.Write("credited-once");
                    mark("customKey"); writer.Write("Другой мод"); writer.Write("свойство");
                    mark("stamina"); for (int i = 0; i < 3; i++) writer.Write(10f);
                    mark("uiLength"); var ui = Encoding.UTF8.GetBytes(Receipt + "=decoy-ui"); writer.Write(ui.Length); writer.Write(ui);
                    if (version == 34)
                    {
                        mark("pseudo"); writer.Write(2); writer.Write(1); writer.Write(21); mark("duplicateInt"); writer.Write(2); writer.Write(22);
                        mark("seeds"); writer.Write(1); writer.Write(3); writer.Write(23);
                    }
                    writer.Flush(); PlayerData = stream.ToArray();
                }
                Profile = new Fixture(inventory, PlayerData);
            }

            internal byte[] ChangedInt(string key, int value)
            { byte[] data = (byte[])PlayerData.Clone(); Array.Copy(BitConverter.GetBytes(value), 0, data, Offsets[key], 4); return data; }

            internal byte[] ChangedString(string key, string value)
            {
                using (var input = new MemoryStream(PlayerData))
                using (var reader = new BinaryReader(input, Encoding.UTF8))
                using (var output = new MemoryStream())
                using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true)))
                {
                    input.Position = Offsets[key]; reader.ReadString();
                    writer.Write(PlayerData, 0, Offsets[key]); writer.Write(value);
                    writer.Write(PlayerData, (int)input.Position, PlayerData.Length - (int)input.Position);
                    writer.Flush(); return output.ToArray();
                }
            }
        }

        private static bool VerifyTail(BlueprintForeignPartData expected, byte[] data, out NativePlayerProfileReadback.Evidence evidence,
            byte[] fresh = null)
        {
            var fixture = new Fixture(Convert.FromBase64String(expected.Zdo), data);
            using (var stream = new MemoryStream(fixture.File()))
                return NativePlayerProfileReadback.TryVerifyPlayerData(stream, 100, "QA", expected,
                    name => name.GetStableHashCode(), fresh ?? data, out evidence, out _);
        }

        private static void RunTail(BlueprintForeignPartData expected)
        {
            int before = assertions;
            foreach (int version in new[] { 33, 34 })
            {
                var fixture = new TailFixture(Convert.FromBase64String(expected.Zdo), version);
                Check(VerifyTail(expected, fixture.PlayerData, out var evidence) && evidence.CustomData.Count == 2 &&
                    evidence.CustomData[TailFixture.Receipt] == "credited-once" && evidence.CustomData["Другой мод"] == "свойство", "native custom-data map " + version);
                bool immutable = false;
                try { ((IDictionary<string, string>)evidence.CustomData).Add("forged", "yes"); }
                catch (NotSupportedException) { immutable = true; }
                Check(immutable && !evidence.CustomData.ContainsKey("forged"), "mutable saved receipt evidence");
                using (var file = new MemoryStream(fixture.Profile.File()))
                    Check(NativePlayerProfileReadback.TryVerify(file, 100, "QA", expected, name => name.GetStableHashCode(),
                        out var legacy, out _) && legacy.CustomData == null, "legacy contract was widened");
                Check(!VerifyTail(expected, fixture.ChangedInt("skillId", 4243), out _, fixture.PlayerData), "fresh byte mismatch accepted");
                for (int length = fixture.Offsets["tail"]; length < fixture.PlayerData.Length; length++)
                    Check(!VerifyTail(expected, fixture.PlayerData.Take(length).ToArray(), out var failed) && failed == null, "rehashed tail truncation " + version + ":" + length);
                foreach (string section in new[] { "recipes", "stations", "set0", "set1", "set2", "set3", "set4", "texts", "foods", "skills", "custom", "uiLength" })
                    foreach (int count in new[] { -1, int.MaxValue, 65537 })
                        Check(!VerifyTail(expected, fixture.ChangedInt(section, count), out _), "invalid count " + section);
                foreach (int skillVersion in new[] { -1, 0, 1, 3, int.MaxValue })
                    Check(!VerifyTail(expected, fixture.ChangedInt("skillVersion", skillVersion), out _), "unknown skills schema accepted");
                Check(VerifyTail(expected, fixture.ChangedInt("skillId", 1), out _), "native repeated wire skill ID refused");
                Check(VerifyTail(expected, fixture.ChangedString("otherText", TailFixture.Receipt), out var texts) &&
                    texts.CustomData[TailFixture.Receipt] == "credited-once", "native sanitized text key collision confused with receipt");
                Check(!VerifyTail(expected, fixture.ChangedString("customKey", TailFixture.Receipt), out _), "duplicate receipt-map key accepted");
                string largeValue = new string('r', 33000);
                Check(VerifyTail(expected, fixture.ChangedString("receiptValue", largeValue), out var large) &&
                    large.CustomData[TailFixture.Receipt] == largeValue, "bounded native value above 32KiB refused");
                foreach (string number in new[] { "color", "skillValue", "stamina" })
                    foreach (float value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                        Check(!VerifyTail(expected, fixture.ChangedInt(number, BitConverter.ToInt32(BitConverter.GetBytes(value), 0)), out _), "nonfinite " + number);
                for (int set = 0; set < 5; set++)
                {
                    byte[] data = (byte[])fixture.PlayerData.Clone(); data[fixture.Offsets["duplicateSet" + set] + 1] = (byte)'A';
                    Check(!VerifyTail(expected, data, out _), "duplicate set accepted");
                }
                foreach (string key in new[] { "beard", "customKey", "receiptValue" })
                    foreach (byte prefix in new byte[] { 0xff, 0x7f })
                    {
                        byte[] data = (byte[])fixture.PlayerData.Clone(); data[fixture.Offsets[key]] = prefix;
                        Check(!VerifyTail(expected, data, out _), "invalid string framing " + key);
                    }
                byte[] invalidUtf8 = (byte[])fixture.PlayerData.Clone(); invalidUtf8[fixture.Offsets["customKey"] + 1] = 0xff;
                Check(!VerifyTail(expected, invalidUtf8, out _), "invalid UTF8 map key accepted");
                foreach (byte[] prefix in new[] { new byte[] { 0x85, 0 }, new byte[] { 0xff, 0xff, 0xff, 0xff, 8 } })
                {
                    int at = fixture.Offsets["beard"];
                    byte[] data = fixture.PlayerData.Take(at).Concat(prefix).Concat(fixture.PlayerData.Skip(at + 1)).ToArray();
                    Check(!VerifyTail(expected, data, out _), "noncanonical or overflowing 7-bit string prefix accepted");
                }
                Check(!VerifyTail(expected, fixture.PlayerData.Concat(new byte[] { 0 }).ToArray(), out _), "tail trailing byte accepted");
                if (version == 34)
                {
                    foreach (string section in new[] { "pseudo", "seeds" })
                        foreach (int count in new[] { -1, int.MaxValue })
                            Check(!VerifyTail(expected, fixture.ChangedInt(section, count), out _), "invalid int map");
                    Check(!VerifyTail(expected, fixture.ChangedInt("duplicateInt", 1), out _), "duplicate int key accepted");
                }
            }
            byte[] inventory = Convert.FromBase64String(expected.Zdo);
            byte[] empty;
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(33); for (int i = 0; i < 4; i++) writer.Write(10f);
                writer.Write(""); writer.Write(0f); writer.Write(inventory); WriteEmptyNativeTail(writer, new Dictionary<string, string>());
                writer.Flush(); empty = stream.ToArray();
            }
            Check(VerifyTail(expected, empty, out var emptyEvidence) && emptyEvidence.CustomData != null && emptyEvidence.CustomData.Count == 0,
                "empty verified map confused with opaque tail");
            var opaqueFixture = new Fixture(inventory);
            byte[] opaquePlayer = opaqueFixture.Payload.Skip(opaqueFixture.Offset["playerVersion"]).ToArray();
            Check(!VerifyTail(expected, opaquePlayer, out _), "opaque tail accepted as custom-data proof");
            Console.WriteLine("PASS " + (assertions - before) + " saved custom-data assertions: PlayerData33/34, immutable map, exact fresh bytes, bounds and opaque legacy separation; no collection or game save invoked.");
        }
    }
}
