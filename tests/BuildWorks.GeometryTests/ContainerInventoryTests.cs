using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class ContainerInventoryTests
    {
        internal static void Run()
        {
            int Hash(string name) => name == "AxeIron" ? -101 : 100;
            BlueprintForeignPartData State(byte[] bytes, string info = "5:2:2:13|AxeIron|Wood") =>
                new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(bytes), null);
            void Require(bool value) { if (!value) throw new InvalidOperationException("Native container inspection failed."); }
            byte[] packet = Packet();
            byte[] currentPacket = Packet(); currentPacket[0] = 110;
            var currentSource = State(currentPacket);
            Require(BlueprintContainerInventory.TryRead(currentSource, Hash, out var current) &&
                current[0].SourceVersion == 110 && current[0].Stack == 12 && current[0].Quality == 3 &&
                current[0].DurabilityHundredths == 12505 && current[0].Cheated && current[0].CrafterId == -123 &&
                current[0].CustomData["мод:данные"] == "exact\0payload\r\n🪵" && current[1].Stack == 1 && !current[1].Cheated &&
                currentSource.Zdo == Convert.ToBase64String(currentPacket));
            for (int length = 0; length < currentPacket.Length; length++)
            {
                bool rejected;
                try { rejected = !BlueprintContainerInventory.TryRead(State(currentPacket.Take(length).ToArray()), Hash, out _); }
                catch (ArgumentException) { rejected = true; }
                Require(rejected);
            }
            byte[] future = (byte[])currentPacket.Clone(); future[0] = 111;
            bool futureRejected = false;
            try { State(future); } catch (ArgumentException) { futureRejected = true; }
            Require(futureRejected);
            var source = State(packet);
            Require(BlueprintContainerInventory.TryRead(source, Hash, out var items) && items.Count == 2);
            var first = items[0]; var second = items[1];
            Require(first.SourceVersion == 109 && first.Prefab == "AxeIron" && first.Stack == 12 && first.Quality == 3 && first.Variant == 2 &&
                first.X == 4 && first.Y == 1 && first.WorldLevel == 3 && first.DurabilityHundredths == 12505 &&
                first.PickedUp && first.Equipped && first.Cheated && first.CrafterId == -123 && first.CrafterName == "Острикс 🪓" &&
                first.CustomData.Count == 2 && first.CustomData["мод:данные"] == "exact\0payload\r\n🪵");
            Require(second.Prefab == "Wood" && second.Stack == 1 && second.Quality == 1 && second.Variant == 0 &&
                second.CrafterId == 0 && second.CrafterName == "" && second.CustomData.Count == 0 && !second.Cheated && !second.Equipped);
            byte[] empty = { 109, 0, 0, 0, 0, 0 };
            Require(BlueprintContainerInventory.TryRead(State(empty, "5:2:0:0"), Hash, out var noItems) && noItems.Count == 0);
            packet[14] = 0;
            Require(first.Quality == 3 && source.Zdo == Convert.ToBase64String(Packet())); // Detached result/source.
            bool immutable = false;
            try { ((IDictionary<string, string>)first.CustomData).Add("alias", "changed"); }
            catch (NotSupportedException) { immutable = true; }
            Require(immutable);
            Require(!BlueprintContainerInventory.TryRead(source, _ => 7, out var collision) && collision == null);
            Require(!BlueprintContainerInventory.TryRead(State(Packet(firstHash: 0)), name => name == "AxeIron" ? 0 : 100, out _));
            Require(!BlueprintContainerInventory.TryRead(source, _ => 42, out _) &&
                !BlueprintContainerInventory.TryRead(source, null, out _) && !BlueprintContainerInventory.TryRead(null, Hash, out _));
            Require(!BlueprintContainerInventory.TryRead(State(Packet(), "5:2:2:14|AxeIron|Wood"), Hash, out _));
            for (int field = 0; field < 4; ++field)
            {
                byte[] noncanonical = Packet();
                int offset = new[] { 14, 16, 18, 22 }[field], size = new[] { 2, 2, 4, 8 }[field];
                Array.Clear(noncanonical, offset, size);
                if (field < 2) noncanonical[offset] = 1;
                string info = field == 1 ? "5:2:2:2|AxeIron|Wood" : "5:2:2:13|AxeIron|Wood";
                Require(!BlueprintContainerInventory.TryRead(State(noncanonical, info), Hash, out _));
            }
            for (int length = 32; length < Packet().Length; ++length)
                Require(!BlueprintContainerInventory.TryRead(State(Packet().Take(length).ToArray()), Hash, out var partial) && partial == null);
            foreach (Action<byte[]> corrupt in new Action<byte[]>[] {
                bytes => bytes[10] = 5, // Grid outside width.
                bytes => { bytes[bytes.Length - 9] = 4; bytes[bytes.Length - 8] = 1; }, // Duplicate cell.
                bytes => { bytes[14] = bytes[15] = 0; }, // Quality zero.
                bytes => { bytes[16] = bytes[17] = 0; }, // Stack zero.
                bytes => bytes[bytes.Length - 5] ^= 1, // Undeclared item hash.
                bytes => bytes[bytes.Length - 1] = 2 }) // Unknown future record flags.
            {
                byte[] malformed = Packet(); corrupt(malformed);
                Require(!BlueprintContainerInventory.TryRead(State(malformed), Hash, out _));
            }
            Require(!BlueprintContainerInventory.TryRead(State(Packet().Concat(new byte[] { 0 }).ToArray()), Hash, out _));
            foreach (byte[] custom in new[] {
                new byte[] { 0 }, // Flag claims a nonempty map, native Save would not emit this.
                new byte[] { 128, 1 }, // Noncanonical extended count.
                new byte[] { 1, 255, 255, 255, 255, 7 }, // String declares 2 GiB, no allocation allowed.
                new byte[] { 1, 128, 0 }, // Noncanonical string prefix.
                new byte[] { 1, 1, 255, 0 }, // Invalid UTF-8.
                CustomPairs(2, duplicate: true) })
                Require(!BlueprintContainerInventory.TryRead(State(Packet(custom)), Hash, out _));
            Require(BlueprintContainerInventory.TryRead(State(Packet(CustomPairs(128))), Hash, out var extended) &&
                extended[0].CustomData.Count == 128);
            Require(BlueprintContainerInventory.TryRead(State(Packet(CustomPairs(1, large: true))), Hash, out var large) &&
                large[0].CustomData["key0"].Length == 40000);
        }

        private static byte[] Packet(byte[] custom = null, int firstHash = -101)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(109); writer.Write((ushort)2);
                writer.Write(12505); writer.Write((byte)4); writer.Write((byte)1); writer.Write((byte)3); writer.Write((byte)255);
                writer.Write((ushort)3); writer.Write((ushort)12); writer.Write(2); writer.Write((long)-123); writer.Write("Острикс 🪓");
                writer.Write(firstHash);
                writer.Write(custom ?? CustomPairs(2));
                writer.Write((byte)1);
                writer.Write(0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)64);
                writer.Write(100); writer.Write((byte)0);
                writer.Flush(); return stream.ToArray();
            }
        }

        private static byte[] CustomPairs(int count, bool duplicate = false, bool large = false)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                if (count < 128) writer.Write((byte)count);
                else { writer.Write((byte)(128 | count >> 8)); writer.Write((byte)count); }
                for (int index = 0; index < count; ++index)
                {
                    writer.Write(count == 2 ? duplicate ? "same" : index == 0 ? "мод:данные" : "other" : "key" + index);
                    writer.Write(large ? new string('x', 40000) : "exact\0payload\r\n🪵");
                }
                writer.Flush(); return stream.ToArray();
            }
        }
    }
}
