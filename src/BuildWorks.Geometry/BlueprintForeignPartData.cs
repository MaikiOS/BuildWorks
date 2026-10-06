using System;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert foreign state bound to its owning part, not instructions or arbitrary ZDO writes.
    public sealed class BlueprintForeignPartData
    {
        public BlueprintForeignPartData(string format, string info, string zdo, string chance)
        {
            if (format != "blueprint" && format != "vbuild") throw new ArgumentException("Unknown foreign format.");
            foreach (string value in new[] { info, zdo, chance })
                if (value != null && value.Length > 32768) throw new ArgumentException("Foreign data is too large.");
            if (info == null && zdo == null && chance == null) throw new ArgumentException("Empty foreign data.");
            Format = format; Info = info; Zdo = zdo; Chance = chance;
        }

        public string Format { get; }
        public string Info { get; }
        public string Zdo { get; }
        public string Chance { get; }
    }
}
