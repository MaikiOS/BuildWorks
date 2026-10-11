using System;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class LegacyContainerInventoryTests
    {
        internal static void Run()
        {
            string package = Convert.ToBase64String(Packet());
            var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                "5:2:1:3|AxeIron", package, null);
            Require(BlueprintContainerInventory.TryRead(data, _ => -101, out var items) && items.Count == 1 &&
                items[0].Prefab == "AxeIron" && items[0].Stack == 3 && items[0].X == 4 && items[0].Y == 1,
                "A source-bound Inventory106 package is not readable");
            var item = items[0];
            Require(item.SourceVersion == 106 && item.LegacyDurability == 20.125f && !item.DurabilityHundredths.HasValue &&
                item.Quality == 2 && item.Variant == 1 && item.CrafterId == -123 && item.CrafterName == "Острикс 🪓" &&
                item.WorldLevel == 3 && item.PickedUp && item.Equipped && !item.Cheated &&
                item.CustomData["mod:exact"] == "raw\0payload 🪵", "Legacy attributes were rounded or changed");
            Require(BlueprintContainerInventory.TryConvertLegacy106(package, 5, 2, _ => -101, out var converted) &&
                converted.Info == data.Info && converted.Zdo == package && !converted.ContentsCleared &&
                converted.WithoutContents(_ => -101).Zdo == package && converted.WithoutContents(_ => -101).ContentsCleared &&
                !converted.ContentsCleared, "Legacy conversion rewrote bytes or mutated original contents");
            byte[] empty = { 106, 0, 0, 0, 0, 0, 0, 0 };
            Require(BlueprintContainerInventory.TryConvertLegacy106(Convert.ToBase64String(empty), 8, 4, _ => 9, out var none) &&
                none.Info == "8:4:0:0" && BlueprintContainerInventory.TryRead(none, _ => 9, out var noItems) && noItems.Count == 0,
                "Empty106 package rejected");
            Require(!BlueprintContainerInventory.TryRead(data, _ => 0, out _) && !BlueprintContainerInventory.TryRead(data, null, out _),
                "Missing or zero item hash accepted");
            Require(!BlueprintContainerInventory.TryRead(new BlueprintForeignPartData(data.Format, "5:2:1:4|AxeIron", package, null),
                _ => -101, out _), "Incorrect units manifest accepted");
            foreach (int version in new[] { 0, 105, 107, 108, 110, int.MaxValue })
            {
                byte[] changed = Packet(); Array.Copy(BitConverter.GetBytes(version), changed, 4);
                Reject(changed);
            }
            for (int length = 0; length < Packet().Length; ++length) Reject(Packet().Take(length).ToArray());
            Reject(Packet().Concat(new byte[] { 0 }).ToArray());
            foreach (int offset in new[] { 4, 16, 24, 28, 33, 37 })
            {
                byte[] changed = Packet(); Array.Copy(BitConverter.GetBytes(-1), 0, changed, offset, 4); Reject(changed);
            }
            foreach (float invalid in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                byte[] changed = Packet(); Array.Copy(BitConverter.GetBytes(invalid), 0, changed, 20, 4); Reject(changed);
            }
            foreach (Action<byte[]> corrupt in new Action<byte[]>[] {
                bytes => bytes[32] = 2, bytes => bytes[bytes.Length - 1] = 2,
                bytes => bytes[bytes.Length - 4] = 1, bytes => bytes[24] = 5,
                bytes => bytes[28] = 2, bytes => bytes[8] = 255 }) Reject(Corrupted(corrupt));
            foreach (string invalid in new[] { null, "not base64", package + " ", new string('A', BlueprintForeignPartData.MaximumContainerBase64 + 1) })
                Require(!BlueprintContainerInventory.TryConvertLegacy106(invalid, 5, 2, _ => -101, out _), "Invalid base64/limit accepted");
            Require(!BlueprintContainerInventory.TryConvertLegacy106(package, 0, 2, _ => -101, out _) &&
                !BlueprintContainerInventory.TryConvertLegacy106(package, 257, 2, _ => -101, out _), "Invented host grid accepted");
            foreach (byte[] map in new[] { new byte[] { 255, 255, 255, 127 }, new byte[] { 255, 255, 255, 255 },
                new byte[] { 1, 0, 0, 0, 128, 0 }, new byte[] { 1, 0, 0, 0, 1, 255, 0 },
                new byte[] { 1, 0, 0, 0, 255, 255, 255, 255, 7 } }) Reject(Packet(map));
            byte[] duplicate = Packet().Concat(Packet().Skip(8)).ToArray(); duplicate[4] = 2; Reject(duplicate);
            byte[] alias = (byte[])duplicate.Clone();
            alias[Packet().Length + 16] = 0; // Second item X; keep valid but a colliding different name.
            alias[Packet().Length + 1] = (byte)'B';
            Reject(alias);
            var immutable = (System.Collections.Generic.IDictionary<string, string>)item.CustomData;
            bool blocked = false; try { immutable.Add("bad", "alias"); } catch (NotSupportedException) { blocked = true; }
            Require(blocked && data.Zdo == package, "Read result aliased source");
        }

        private static byte[] Corrupted(Action<byte[]> corrupt) { byte[] bytes = Packet(); corrupt(bytes); return bytes; }

        private static void Reject(byte[] bytes) => Require(!BlueprintContainerInventory.TryConvertLegacy106(
            Convert.ToBase64String(bytes), 5, 2, _ => -101, out _), "Corrupt/unknown Inventory106 accepted");

        internal static byte[] Packet(byte[] custom = null)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(106); writer.Write(1);
                writer.Write("AxeIron"); writer.Write(3); writer.Write(20.125f);
                writer.Write(4); writer.Write(1); writer.Write(true);
                writer.Write(2); writer.Write(1); writer.Write(-123L); writer.Write("Острикс 🪓");
                if (custom != null) writer.Write(custom);
                else { writer.Write(1); writer.Write("mod:exact"); writer.Write("raw\0payload 🪵"); }
                writer.Write(3); writer.Write(true);
                writer.Flush(); return stream.ToArray();
            }
        }

        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
