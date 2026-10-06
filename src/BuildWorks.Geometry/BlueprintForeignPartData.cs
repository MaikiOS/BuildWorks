using System;
using System.Globalization;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert part-bound state, not instructions or arbitrary ZDO writes.
    public sealed class BlueprintForeignPartData
    {
        public const string NativeItemStandFormat = "valheim-itemstand-v1";
        public BlueprintForeignPartData(string format, string info, string zdo, string chance)
        {
            if (format != "blueprint" && format != "vbuild" && format != NativeItemStandFormat)
                throw new ArgumentException("Unknown part-state format.");
            foreach (string value in new[] { info, zdo, chance })
                if (value != null && value.Length > 32768) throw new ArgumentException("Foreign data is too large.");
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
            Format = format; Info = info; Zdo = zdo; Chance = chance;
        }

        public string Format { get; }
        public string Info { get; }
        public string Zdo { get; }
        public string Chance { get; }
    }
}
