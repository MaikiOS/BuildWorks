using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class ItemStandContentsTests
    {
        internal static byte[] Packet(int hash = -101, ushort stack = 1, int customCount = 1)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write((byte)109);
                writer.Write(12505); writer.Write((byte)254); writer.Write((byte)255); writer.Write((byte)3);
                writer.Write((byte)(247 | (stack != 1 ? 8 : 0)));
                writer.Write((ushort)3); if (stack != 1) writer.Write(stack);
                writer.Write(2); writer.Write((long)-123); writer.Write("Острикс 🪓"); writer.Write(hash);
                if (customCount < 128) writer.Write((byte)customCount);
                else { writer.Write((byte)(128 | customCount >> 8)); writer.Write((byte)customCount); }
                for (int i = 0; i < customCount; ++i)
                { writer.Write("mod:key" + i); writer.Write("exact\0payload\r\n🪵"); }
                writer.Write((byte)1); writer.Flush(); return stream.ToArray();
            }
        }

        internal static void Run()
        {
            void Require(bool value) { if (!value) throw new InvalidOperationException("Native ItemStand inspection failed."); }
            BlueprintForeignPartData State(byte[] bytes, string info = "SwordIron:2:3:0", bool cleared = false) =>
                new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, info,
                    Convert.ToBase64String(bytes), null, cleared);
            var source = State(Packet());
            byte[] currentPacket = Packet(); currentPacket[0] = 110;
            var currentSource = State(currentPacket);
            Require(BlueprintItemStandContents.TryRead(currentSource, _ => -101, out var current) &&
                current.SourceVersion == 110 && current.Prefab == "SwordIron" && current.Quality == 3 &&
                current.Variant == 2 && current.DurabilityHundredths == 12505 && current.Cheated &&
                currentSource.Zdo == Convert.ToBase64String(currentPacket));
            currentPacket[0] = 111;
            Require(!BlueprintItemStandContents.TryRead(State(currentPacket), _ => -101, out _));
            Require(BlueprintItemStandContents.TryRead(source, _ => -101, out var item));
            Require(item.Prefab == "SwordIron" && item.Stack == 1 && item.Quality == 3 && item.Variant == 2 &&
                item.DurabilityHundredths == 12505 && item.X == 254 && item.Y == 255 && item.WorldLevel == 3 &&
                item.PickedUp && item.Equipped && !item.Cheated && item.CrafterId == -123 && item.CrafterName == "Острикс 🪓" &&
                item.CustomData["mod:key0"] == "exact\0payload\r\n🪵");
            bool immutable = false;
            try { ((IDictionary<string, string>)item.CustomData).Clear(); } catch (NotSupportedException) { immutable = true; }
            Require(immutable && source.Zdo == Convert.ToBase64String(Packet()));
            Require(BlueprintItemStandContents.TryRead(State(Packet(customCount: 128)), _ => -101, out var extended) && extended.CustomData.Count == 128);
            foreach (var invalid in new[] { State(Packet(hash: 0)), State(Packet(hash: 42)), State(Packet(stack: 2)),
                State(Packet(), "SwordIron:1:3:0"), State(Packet(), "SwordIron:2:4:0"), State(Packet(), "SwordIron:2:3:0", true),
                State(Packet().Concat(new byte[] { 0 }).ToArray()), State(Packet(customCount: 0)) })
            {
                // A valid cleared snapshot remains inspectable; its archived fields are never effective items.
                if (invalid.ContentsCleared) { Require(BlueprintItemStandContents.TryRead(invalid, _ => -101, out _)); continue; }
                Require(!BlueprintItemStandContents.TryRead(invalid, _ => -101, out var partial) && partial == null);
                bool refused = false;
                try { invalid.WithoutContents(_ => -101); } catch (ArgumentException) { refused = true; }
                Require(refused && !invalid.ContentsCleared);
            }
            for (int length = 3; length < Packet().Length; ++length)
                Require(!BlueprintItemStandContents.TryRead(State(Packet().Take(length).ToArray()), _ => -101, out var partial) && partial == null);
            foreach (Action<byte[]> corrupt in new Action<byte[]>[] {
                bytes => bytes[0] = 111, bytes => bytes[8] &= 191, // Unsupported future version / missing prefab flag.
                bytes => bytes[bytes.Length - 1] = 2, bytes => bytes[9] = 0, // Future flags / zero quality.
                bytes => bytes[24] = 255 }) // Malformed crafter UTF-8.
            {
                byte[] bytes = Packet(); corrupt(bytes);
                Require(!BlueprintItemStandContents.TryRead(State(bytes), _ => -101, out _));
            }
            Require(!BlueprintItemStandContents.TryRead(source, _ => 0, out _) && !BlueprintItemStandContents.TryRead(source, null, out _) &&
                !BlueprintItemStandContents.TryRead(null, _ => -101, out _));
            var recipe = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, "SwordIron:0:1:0", null, null);
            Require(!BlueprintItemStandContents.TryRead(recipe, _ => -101, out _) && recipe.WithoutContents(null).ContentsCleared);
            Require(source.WithoutContents(_ => -101).Zdo == source.Zdo);
            NativeConversion();
        }

        private static void NativeConversion()
        {
            int Hash(string name) => name switch {
                "itemstand" => -1235119491, "SwordIron" => -110263489, "item" => -1126421579,
                "variant" => 1759467715, "quality" => 917906861, "type" => 1421151742, "itemData" => 949524933,
                _ => throw new InvalidOperationException("Unpinned holder fixture hash")
            };
            string Resolve(int id) => id == Hash("SwordIron") ? "SwordIron" : null;
            BlueprintForeignZdo State(params (string Key, object Value)[] fields)
            {
                string packet = BlueprintForeignZdo.CreateNative(Hash("itemstand"), true, false, 2).WriteNative(
                    fields.Select(field => new KeyValuePair<int, object>(Hash(field.Key), field.Value)));
                if (!BlueprintForeignZdo.TryReadNative(packet, Hash("itemstand"), out var parsed)) throw new InvalidOperationException("Native holder fixture invalid");
                return parsed;
            }
            void Check(bool value, string why) { if (!value) throw new InvalidOperationException(why); }
            var bytes = Packet(Hash("SwordIron"));
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
                {
                    writer.Write((byte)110); writer.Write(10000); writer.Write((byte)0); writer.Write((byte)0);
                    writer.Write((byte)0); writer.Write((byte)64); writer.Write(Hash("SwordIron")); writer.Write((byte)0);
                }
                Check(BlueprintItemStandContents.TryConvertNative(State(("item", Hash("SwordIron")), ("itemData", stream.ToArray())),
                    Resolve, out var defaultsOnly) && defaultsOnly.Info == "SwordIron:0:1:0", "Native variant/quality/orientation defaults changed");
            }
            foreach (byte version in new byte[] { 109, 110 })
            {
                var current = (byte[])bytes.Clone(); current[0] = version;
                var source = State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 3), ("type", 1), ("itemData", current));
                Check(BlueprintItemStandContents.TryConvertNative(source, Resolve, out var converted) &&
                    converted.Info == "SwordIron:2:3:1" && converted.Zdo == Convert.ToBase64String(current) &&
                    BlueprintItemStandContents.TryRead(converted, Hash, out var item) && item.Stack == 1 && item.Quality == 3 &&
                    item.Variant == 2 && item.WorldLevel == 3 && item.DurabilityHundredths == 12505 && item.CrafterId == -123 &&
                    item.CustomData["mod:key0"] == "exact\0payload\r\n🪵", "Native holder conversion changed complete item properties");
            }
            var stale = State(("item", 0), ("variant", 99), ("quality", -2), ("itemData", new byte[] { 255 }));
            Check(BlueprintItemStandContents.TryConvertNative(stale, null, out var empty) && empty == null,
                "Native empty attachment resurrected stale itemData");
            Check(BlueprintItemStandContents.TryConvertNative(State(), null, out empty) && empty == null, "Missing native item default is not empty");
            var valid = State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 3), ("itemData", bytes));
            Check(BlueprintItemStandContents.TryConvertNative(valid, Resolve, out var defaults) && defaults.Info == "SwordIron:2:3:0",
                "Native orientation default changed");
            foreach (var invalid in new[] {
                State(("item", Hash("SwordIron"))),
                State(("item", Hash("SwordIron")), ("itemData", bytes)),
                State(("item", Hash("SwordIron")), ("variant", 1), ("quality", 3), ("itemData", bytes)),
                State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 4), ("itemData", bytes)),
                State(("item", Hash("SwordIron")), ("item", "SwordIron"), ("itemData", bytes)),
                State(("item", Hash("SwordIron")), ("itemData", 1)),
                State(("item", 0), ("itemData", 1)),
                State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 3), ("type", -1), ("itemData", bytes)),
                State(("item", Hash("SwordIron")), ("itemData", new byte[24577])),
                State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 3), ("itemData", Packet(Hash("SwordIron"), stack: 2))),
                State(("item", Hash("SwordIron")), ("variant", 2), ("quality", 3), ("itemData", bytes.Concat(new byte[] { 0 }).ToArray())) })
                Check(!BlueprintItemStandContents.TryConvertNative(invalid, Resolve, out var partial) && partial == null,
                    "Invalid native holder created partial item data");
            Check(!BlueprintItemStandContents.TryConvertNative(valid, _ => "Wood", out _) &&
                !BlueprintItemStandContents.TryConvertNative(valid, _ => null, out _) &&
                !BlueprintItemStandContents.TryConvertNative(valid, null, out _) &&
                !BlueprintItemStandContents.TryConvertNative(null, Resolve, out _), "Unresolved native holder item was guessed");
            Console.WriteLine("PASS native ItemStand data conversion: exact109/110 package, independent properties, empty/default/stale semantics, typed/hash/metadata/size/stack/trailing controls; no host capability or placement proof");
        }
    }
}
