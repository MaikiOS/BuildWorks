using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read the verified native v109 layout without Inventory.Load, prefabs or scripts.
    // The original payload remains authoritative; inspection never rewrites its bytes.
    public static class BlueprintContainerInventory
    {
        public sealed class Item
        {
            internal Item(string prefab, int durability, int x, int y, int worldLevel, int flags,
                int quality, int stack, int variant, long crafterId, string crafterName,
                Dictionary<string, string> customData, bool cheated)
            {
                Prefab = prefab; DurabilityHundredths = durability; X = x; Y = y; WorldLevel = worldLevel;
                PickedUp = (flags & 1) != 0; Equipped = (flags & 2) != 0;
                Quality = quality; Stack = stack; Variant = variant; CrafterId = crafterId;
                CrafterName = crafterName; CustomData = new ReadOnlyDictionary<string, string>(customData); Cheated = cheated;
            }
            public string Prefab { get; }
            public int DurabilityHundredths { get; }
            public int X { get; }
            public int Y { get; }
            public int WorldLevel { get; }
            public bool PickedUp { get; }
            public bool Equipped { get; }
            public int Quality { get; }
            public int Stack { get; }
            public int Variant { get; }
            public long CrafterId { get; }
            public string CrafterName { get; }
            public IReadOnlyDictionary<string, string> CustomData { get; }
            public bool Cheated { get; }
        }

        public static bool TryRead(BlueprintForeignPartData data, Func<string, int> prefabHash, out IReadOnlyList<Item> items)
        {
            items = null;
            if (data?.Format != BlueprintForeignPartData.NativeContainerFormat || prefabHash == null) return false;
            string[] manifest = data.Info.Split('|'), fields = manifest[0].Split(':');
            int width = int.Parse(fields[0], CultureInfo.InvariantCulture), height = int.Parse(fields[1], CultureInfo.InvariantCulture);
            int count = int.Parse(fields[2], CultureInfo.InvariantCulture), units = int.Parse(fields[3], CultureInfo.InvariantCulture);
            var names = new Dictionary<int, string>();
            foreach (string name in manifest.Skip(1))
            {
                int hash = prefabHash(name);
                if (hash == 0 || names.ContainsKey(hash)) return false; // Native Load skips hash 0; collisions cannot identify an item.
                names.Add(hash, name);
            }
            try
            {
                using (var stream = new MemoryStream(Convert.FromBase64String(data.Zdo), false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadInt32() != 109 || reader.ReadUInt16() != count) return false;
                    var result = new List<Item>(count);
                    var cells = new HashSet<int>();
                    var usedNames = new HashSet<string>(StringComparer.Ordinal);
                    long total = 0;
                    for (int index = 0; index < count; ++index)
                    {
                        Item item = ReadItem(reader, names);
                        if (item.X >= width || item.Y >= height || !cells.Add(item.Y * width + item.X)) return false;
                        total += item.Stack; usedNames.Add(item.Prefab);
                        result.Add(item);
                    }
                    if (stream.Position != stream.Length || total != units || usedNames.Count != names.Count) return false;
                    items = result.AsReadOnly(); return true;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is DecoderFallbackException)
            { return false; }
        }

        // ItemStand and Inventory use the same native ItemData.Save record, with different envelopes.
        internal static Item ReadItem(BinaryReader reader, Dictionary<int, string> names)
        {
            int durability = reader.ReadInt32(), x = reader.ReadByte(), y = reader.ReadByte();
            int worldLevel = reader.ReadByte(), flags = reader.ReadByte();
            int quality = (flags & 4) != 0 ? reader.ReadUInt16() : 1;
            int stack = (flags & 8) != 0 ? reader.ReadUInt16() : 1;
            int variant = (flags & 16) != 0 ? reader.ReadInt32() : 0;
            long crafterId = (flags & 32) != 0 ? reader.ReadInt64() : 0;
            string crafterName = (flags & 32) != 0 ? ReadString(reader) : "";
            if ((flags & 4) != 0 && quality == 1 || (flags & 8) != 0 && stack == 1 ||
                (flags & 16) != 0 && variant == 0 || (flags & 32) != 0 && crafterId == 0 || quality < 1 || stack < 1)
                throw new InvalidDataException("Invalid native item defaults.");
            if ((flags & 64) == 0 || !names.TryGetValue(reader.ReadInt32(), out string prefab))
                throw new InvalidDataException("Unknown native item prefab.");
            var customData = new Dictionary<string, string>(StringComparer.Ordinal);
            int customCount = 0;
            if ((flags & 128) != 0)
            {
                customCount = reader.ReadByte();
                if ((customCount & 128) != 0)
                {
                    customCount = (customCount & 127) << 8 | reader.ReadByte();
                    if (customCount < 128) throw new InvalidDataException("Invalid native item map count.");
                }
                if (customCount == 0 || customCount * 2L > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("Invalid native item map size.");
            }
            for (int entry = 0; entry < customCount; ++entry)
            {
                string key = ReadString(reader), value = ReadString(reader);
                if (customData.ContainsKey(key)) throw new InvalidDataException("Duplicate native item key.");
                customData.Add(key, value);
            }
            int extraFlags = reader.ReadByte();
            if ((extraFlags & ~1) != 0) throw new InvalidDataException("Unknown native item flags.");
            return new Item(prefab, durability, x, y, worldLevel, flags, quality, stack, variant,
                crafterId, crafterName, customData, (extraFlags & 1) != 0);
        }

        // BinaryWriter strings use a canonical 7-bit UTF-8 byte length. Bound it before allocating.
        internal static string ReadString(BinaryReader reader)
        {
            uint length = 0;
            for (int shift = 0; shift < 35; shift += 7)
            {
                byte part = reader.ReadByte();
                if (shift == 28 && part > 7) throw new InvalidDataException("Invalid string length.");
                length |= (uint)(part & 127) << shift;
                if ((part & 128) != 0) continue;
                if (shift > 0 && part == 0 || length > reader.BaseStream.Length - reader.BaseStream.Position)
                    throw new InvalidDataException("Invalid string length.");
                byte[] bytes = reader.ReadBytes((int)length);
                return new UTF8Encoding(false, true).GetString(bytes);
            }
            throw new InvalidDataException("Invalid string length.");
        }
    }
}
