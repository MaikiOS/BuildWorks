using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ForeignZdoChecks
    {
        // Match the verified host string hash, without loading the game in this file-only test.
        internal static int Hash(string value)
        {
            unchecked
            {
                int a = 5381, b = a;
                for (int index = 0; index < value.Length && value[index] != '\0'; index += 2)
                {
                    a = ((a << 5) + a) ^ value[index];
                    if (index == value.Length - 1 || value[index + 1] == '\0') break;
                    b = ((b << 5) + b) ^ value[index + 1];
                }
                return a + b * 1566083941;
            }
        }
        internal static void Run()
        {
            Require(Hash("item") == -1126421579 && Hash("health") == -883022132, "Host hash drift");
            string raw = Package(w => {
                w.Write(9); w.Write((byte)2);
                w.Write(Hash("health")); w.Write(-1f); w.Write(Hash("WearNTear.m_health")); w.Write(-2f);
                w.Write((byte)4);
                foreach (string key in new[] { "HasFields", "HasFieldsWearNTear", "HasFieldsStaticTarget", "StaticTarget.m_randomTarget" })
                { w.Write(Hash(key)); w.Write(key == "StaticTarget.m_randomTarget" ? 0 : 1); }
            });
            string text = "#Name:Native rules\n#Pieces\nwoodwall;;0;0;0;0;0;0;1;\"\";1;1;1;" + raw + "\nwoodwall;;1;0;0;0;0;0;1;\"\"";
            var source = ExternalBlueprintLibrary.Parse(text, "rules.blueprint");
            var prepared = Prepare(source);
            Require(prepared.importError == null && prepared.parts[0].foreignData == null && source.parts[0].foreignData.zdo == raw &&
                prepared.sourceDocuments[0].ToGeometry().Text == text && !CompositeBlueprintStore.HasForeignData(prepared),
                "Native-rules conversion lost original evidence or left admin fields active");
            var portable = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(ExternalBlueprintLibrary.DetachedCopy(prepared))));
            Require(portable.parts[0].foreignData == null && portable.sourceDocuments[0].ToGeometry().Text == text, "Conversion not portable/lossless archive");
            string unknown = Package(w => { w.Write(8); w.Write((byte)1); w.Write(Hash("Unreviewed.m_field")); w.Write(1); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, unknown), "unknown.blueprint")).parts[0].foreignData.zdo == unknown,
                "Unknown field silently discarded");
            string wrongType = Package(w => { w.Write(8); w.Write((byte)1); w.Write(Hash("health")); w.Write(100); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, wrongType), "wrong.blueprint")).parts[0].foreignData.zdo == wrongType,
                "Wrong metadata type silently discarded");
            string sibling = Package(w => { w.Write(17); w.Write((byte)1); w.Write(Hash("health")); w.Write(-1f);
                w.Write((byte)1); w.Write(Hash("health")); w.Write("unknown sibling"); });
            Require(Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, sibling), "sibling.blueprint")).parts[0].foreignData.zdo == sibling,
                "Known typed key consumed its unknown sibling");
            var modified = ExternalBlueprintLibrary.Parse(text, "unbound.blueprint");
            modified.parts[0].foreignData.zdo = Package(w => { w.Write(1); w.Write((byte)1); w.Write(Hash("health")); w.Write(50f); });
            Require(Prepare(modified).parts[0].foreignData != null, "Unarchived modified payload discarded under unrelated source evidence");
            string sign = Package(w => { w.Write(16); w.Write((byte)2); w.Write(Hash("text")); w.Write("Дом Ostrix\nВход");
                w.Write(Hash("author")); w.Write("старый автор"); });
            var signSource = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(raw, sign), "sign.blueprint"));
            Require(signSource.parts[0].signText == "Дом Ostrix\nВход" && signSource.parts[0].foreignData == null, "ZDO sign lost");
            var unarchivedSignInfo = ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(raw, sign), "unarchived-sign.blueprint");
            unarchivedSignInfo.parts[0].foreignData.info = "unarchived override";
            Require(Prepare(unarchivedSignInfo).parts[0].foreignData?.info == "unarchived override", "Unarchived sign Info lost");
            var signFallback = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "sign").Replace(";\"\";", ";\"stale Info\";"), "sign-fallback.blueprint"));
            Require(signFallback.parts[0].foreignData != null, "Info falsely used as missing ZDO text fallback");
            string scaled = Package(w => { w.Write(2); w.Write((byte)1); w.Write(Hash("scale")); w.Write(2f); w.Write(3f); w.Write(4f); });
            var vbuild = Prepare(ExternalBlueprintLibrary.Parse("woodwall;0;0;0;1;0;0;0;" + scaled + "\nwoodwall;0;0;0;1;1;0;0", "scaled.vbuild"));
            Require(vbuild.parts[0].foreignData != null, "vbuild absolute ZDO scale discarded without row-scale replacement");
            string mount = Package(w => { w.Write(25); w.Write((byte)1); w.Write(Hash("durability")); w.Write(20.125f);
                w.Write((byte)2); w.Write(Hash("quality")); w.Write(3); w.Write(Hash("worldLevel")); w.Write(0);
                w.Write((byte)1); w.Write(Hash("item")); w.Write("AxeIron"); });
            var mounted = Prepare(ExternalBlueprintLibrary.Parse(text.Replace("woodwall", "itemstand").Replace(raw, mount), "mount.blueprint"));
            Require(mounted.parts[0].foreignData.format == BlueprintForeignPartData.NativeItemStandFormat &&
                BlueprintItemStandContents.TryRead(mounted.parts[0].foreignData.ToGeometry(), Hash, out var item) &&
                item.DurabilityHundredths == 2012 && item.Quality == 3 && item.Prefab == "AxeIron", "Legacy mount did not become native full snapshot");
            var malformed = Prepare(ExternalBlueprintLibrary.Parse(text.Replace(raw, "not base64"), "bad.blueprint"));
            Require(malformed.parts[0].foreignData != null, "Malformed state enabled construction");
            Console.WriteLine("PASS foreign typed conversion: ordinary survival rules, exact archived source, sign, full mount, unknown/malformed refusal");
        }
        private static CompositeBlueprintStore.Blueprint Prepare(CompositeBlueprintStore.Blueprint source) =>
            ExternalBlueprintLibrary.PrepareForHost(source, _ => new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, Hash, 0);
        private static string Package(Action<BinaryWriter> write)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream, Encoding.UTF8))
            { write(writer); writer.Flush(); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    }
}
