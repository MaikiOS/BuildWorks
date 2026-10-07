using System;
using System.Globalization;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert part-bound state, not instructions or arbitrary ZDO writes.
    public sealed class BlueprintForeignPartData
    {
        public const string NativeItemStandFormat = "valheim-itemstand-v1";
        public const string NativeContainerFormat = "valheim-container-v1";
        public const int MaximumContainerBytes = 1024 * 1024;
        public const int MaximumContainerBase64 = ((MaximumContainerBytes + 2) / 3) * 4;
        public BlueprintForeignPartData(string format, string info, string zdo, string chance, bool contentsCleared = false)
        {
            if (format != "blueprint" && format != "vbuild" && format != NativeItemStandFormat && format != NativeContainerFormat)
                throw new ArgumentException("Unknown part-state format.");
            if (contentsCleared && format != NativeItemStandFormat && format != NativeContainerFormat)
                throw new ArgumentException("Cannot clear unknown object contents.");
            foreach (string value in new[] { info, chance })
                if (value != null && value.Length > 32768) throw new ArgumentException("Foreign data is too large.");
            if (zdo != null && zdo.Length > (format == NativeContainerFormat ? MaximumContainerBase64 : 32768))
                throw new ArgumentException("Foreign data is too large.");
            if (info == null && zdo == null && chance == null) throw new ArgumentException("Empty foreign data.");
            if (format == NativeItemStandFormat)
            {
                string[] fields = info?.Split(':');
                if (fields == null || fields.Length != 4 || string.IsNullOrWhiteSpace(fields[0]) || fields[0].Length > 128 ||
                    fields[0] != fields[0].Trim() || fields[0].IndexOfAny(new[] { '/', '\\', '<', '>' }) >= 0 ||
                    chance != null) throw new ArgumentException("Invalid native item-stand state.");
                foreach (char c in fields[0])
                    if (char.IsControl(c)) throw new ArgumentException("Invalid item prefab name.");
                for (int index = 1; index < fields.Length; ++index)
                    if (!int.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) ||
                        index == 2 && value < 1) throw new ArgumentException("Invalid item-stand metadata.");
                if (zdo != null)
                {
                    byte[] bytes;
                    try { bytes = Convert.FromBase64String(zdo); }
                    catch (FormatException error) { throw new ArgumentException("Invalid native item package.", error); }
                    if (bytes.Length < 3 || bytes[0] == 0 || Convert.ToBase64String(bytes) != zdo)
                        throw new ArgumentException("Invalid native item package.");
                }
            }
            if (format == NativeContainerFormat)
            {
                // A declarative manifest plus inert exact native bytes, not an Inventory.Load recipe.
                string[] manifest = info?.Split('|');
                string[] fields = manifest?[0].Split(':');
                var values = new int[4];
                if (fields == null || fields.Length != 4 || chance != null || zdo == null)
                    throw new ArgumentException("Invalid native container state.");
                for (int index = 0; index < 4; ++index)
                    if (!int.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out values[index]))
                        throw new ArgumentException("Invalid native container manifest.");
                int width = values[0], height = values[1], slots = values[2], units = values[3];
                if (width < 1 || height < 1 || width > 256 || height > 256 || slots < 0 || slots > 4096 ||
                    slots > width * height || units < slots || (long)units > (long)slots * ushort.MaxValue ||
                    (slots == 0 ? manifest.Length != 1 : manifest.Length < 2 || manifest.Length - 1 > slots))
                    throw new ArgumentException("Invalid native container capacity.");
                string previous = null;
                foreach (string prefab in manifest.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(prefab) || prefab.Length > 128 || prefab != prefab.Trim() ||
                        prefab.Any(c => char.IsControl(c) || "/\\<>:".Contains(c)) ||
                        previous != null && string.CompareOrdinal(previous, prefab) >= 0)
                        throw new ArgumentException("Invalid container prefab manifest.");
                    previous = prefab;
                }
                byte[] bytes;
                try { bytes = Convert.FromBase64String(zdo); }
                catch (FormatException error) { throw new ArgumentException("Invalid native inventory package.", error); }
                if (bytes.Length > MaximumContainerBytes || bytes.Length < 6 + slots * 13 ||
                    bytes[0] != 109 || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0 ||
                    (bytes[4] | bytes[5] << 8) != slots || slots == 0 && bytes.Length != 6 ||
                    Convert.ToBase64String(bytes) != zdo)
                    throw new ArgumentException("Unknown, incomplete or mismatched native inventory package.");
            }
            Format = format; Info = info; Zdo = zdo; Chance = chance; ContentsCleared = contentsCleared;
        }

        public string Format { get; }
        public string Info { get; }
        public string Zdo { get; }
        public string Chance { get; }
        // Empty effective contents; Info/Zdo remain exact original evidence, never a restoration recipe.
        public bool ContentsCleared { get; }

        public BlueprintForeignPartData WithoutContents(Func<string, int> prefabHash)
        {
            if (Format != NativeItemStandFormat && Format != NativeContainerFormat ||
                Format == NativeContainerFormat && !BlueprintContainerInventory.TryRead(this, prefabHash, out _) ||
                Format == NativeItemStandFormat && Zdo != null && !BlueprintItemStandContents.TryRead(this, prefabHash, out _))
                throw new ArgumentException("Cannot clear unreadable object contents.");
            return ContentsCleared ? this : new BlueprintForeignPartData(Format, Info, Zdo, Chance, true);
        }
    }
}
