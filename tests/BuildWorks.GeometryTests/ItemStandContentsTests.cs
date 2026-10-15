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
                item.PickedUp && item.Equipped && item.Cheated && item.CrafterId == -123 && item.CrafterName == "Острикс 🪓" &&
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
        }
    }
}
