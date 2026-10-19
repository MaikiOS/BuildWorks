using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert native slot evidence. Decoding does not authorize equipment restoration.
    public sealed class BlueprintArmorStandState
    {
        public const int MaximumSlots = 64;
        public const int MaximumItemBytes = 24576;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public sealed class Slot
        {
            private readonly byte[] package;
            public int VisualSlot { get; }
            public int ItemId { get; }
            public string Prefab { get; }
            public int Variant { get; }
            public byte[] Package => package == null ? null : (byte[])package.Clone();

            public Slot(int visualSlot, int itemId, string prefab, int variant, byte[] package)
            {
                if (prefab != null && (string.IsNullOrWhiteSpace(prefab) || prefab.Length > 128 ||
                    prefab != prefab.Trim() || prefab.Any(c => char.IsControl(c) || "/\\<>:".Contains(c))))
                    throw new ArgumentException("Invalid armor item name.");
                if (package?.Length > MaximumItemBytes) throw new ArgumentException("Armor item package is too large.");
                VisualSlot = visualSlot; ItemId = itemId; Prefab = prefab; Variant = variant;
                this.package = package == null ? null : (byte[])package.Clone();
            }

            public bool TryInspect(Func<string, int> hash, out BlueprintContainerInventory.Item item)
            {
                item = null;
                if (ItemId != 0 && Prefab != null && hash != null && hash(Prefab) == ItemId &&
                    BlueprintItemStandContents.TryReadPackage(Prefab, package, hash, out item) && item.Variant == Variant) return true;
                item = null; return false;
            }
        }

        public int Pose { get; }
        public IReadOnlyList<Slot> Slots { get; }

        public BlueprintArmorStandState(int pose, IEnumerable<Slot> slots)
        {
            if (slots == null) throw new ArgumentNullException(nameof(slots));
            Slot[] copy = slots.Take(MaximumSlots + 1).ToArray();
            if (copy.Length > MaximumSlots || copy.Any(slot => slot == null))
                throw new ArgumentException("Invalid armor slot list.");
            Pose = pose; Slots = Array.AsReadOnly(copy);
        }

        public BlueprintForeignPartData ToForeignData(bool contentsCleared = false)
        {
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output, Utf8))
            {
                writer.Write((byte)1);
                foreach (Slot slot in Slots)
                {
                    writer.Write(slot.VisualSlot); writer.Write(slot.ItemId); writer.Write(slot.Variant);
                    byte[] name = slot.Prefab == null ? null : Utf8.GetBytes(slot.Prefab);
                    writer.Write(name?.Length ?? -1); if (name != null) writer.Write(name);
                    byte[] package = slot.Package;
                    writer.Write(package?.Length ?? -1); if (package != null) writer.Write(package);
                    if (output.Length > BlueprintForeignPartData.MaximumContainerBytes)
                        throw new ArgumentException("Armor state is too large.");
                }
                writer.Flush();
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeArmorStandFormat,
                    Pose.ToString(CultureInfo.InvariantCulture) + ":" + Slots.Count.ToString(CultureInfo.InvariantCulture),
                    Convert.ToBase64String(output.ToArray()), null, contentsCleared);
            }
        }

        public static BlueprintArmorStandState Read(BlueprintForeignPartData data)
        {
            if (data?.Format != BlueprintForeignPartData.NativeArmorStandFormat)
                throw new ArgumentException("Not an armor state.");
            return Decode(data.Info, data.Zdo, data.Chance);
        }

        internal static BlueprintArmorStandState Decode(string info, string encoded, string chance)
        {
            string[] fields = info?.Split(':');
            if (fields == null || fields.Length != 2 || chance != null || encoded == null ||
                encoded.Length > BlueprintForeignPartData.MaximumContainerBase64 ||
                !int.TryParse(fields[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int pose) ||
                !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
                count > MaximumSlots || info != pose.ToString(CultureInfo.InvariantCulture) + ":" + count.ToString(CultureInfo.InvariantCulture))
                throw new ArgumentException("Invalid armor state manifest.");
            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                if (bytes.Length > BlueprintForeignPartData.MaximumContainerBytes || Convert.ToBase64String(bytes) != encoded)
                    throw new InvalidDataException("Invalid armor state package.");
                using (var input = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(input, Utf8))
                {
                    if (reader.ReadByte() != 1) throw new InvalidDataException("Unknown armor envelope version.");
                    var slots = new List<Slot>(count);
                    for (int i = 0; i < count; ++i)
                    {
                        int visual = reader.ReadInt32(), id = reader.ReadInt32(), variant = reader.ReadInt32();
                        byte[] name = ReadBytes(reader, 512), packet = ReadBytes(reader, MaximumItemBytes);
                        slots.Add(new Slot(visual, id, name == null ? null : Utf8.GetString(name), variant, packet));
                    }
                    if (input.Position != input.Length) throw new InvalidDataException("Trailing armor state bytes.");
                    return new BlueprintArmorStandState(pose, slots);
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is ArgumentException)
            { throw new ArgumentException("Invalid armor state package.", error); }
        }

        private static byte[] ReadBytes(BinaryReader reader, int limit)
        {
            int length = reader.ReadInt32();
            if (length == -1) return null;
            if (length < 0 || length > limit || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid armor field length.");
            return reader.ReadBytes(length);
        }
    }
}
