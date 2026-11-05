using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Immutable catalog facts captured on the host thread, never saved as permissions.
    public sealed class UnboundItemStandItem : IEquatable<UnboundItemStandItem>
    {
        public UnboundItemStandItem(string name, int variants, int maximumQuality, IEnumerable<int> orientations)
        {
            _ = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, name + ":0:1:0", null, null);
            if (KgBlueprint.StablePrefabHash(name) == 0 || variants < 1 || maximumQuality < 0 || orientations == null)
                throw new ArgumentException("Invalid item-stand catalog facts.");
            int[] values = orientations.Distinct().OrderBy(value => value).ToArray();
            if (values.Any(value => value < 0)) throw new ArgumentException("Invalid item-stand orientation.");
            Name = name; Variants = variants; MaximumQuality = maximumQuality; Orientations = Array.AsReadOnly(values);
        }
        public string Name { get; }
        public int Variants { get; }
        public int MaximumQuality { get; }
        public IReadOnlyList<int> Orientations { get; }
        public bool Supports(BlueprintForeignPartData data)
        {
            if (data?.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                !BlueprintItemStandContents.TryRead(data, KgBlueprint.StablePrefabHash, out _)) return false;
            string[] fields = data.Info.Split(':');
            return fields[0] == Name && (data.ContentsCleared ||
                int.Parse(fields[1], CultureInfo.InvariantCulture) < Variants &&
                int.Parse(fields[2], CultureInfo.InvariantCulture) <= MaximumQuality &&
                Orientations.Contains(int.Parse(fields[3], CultureInfo.InvariantCulture)));
        }
        public bool Equals(UnboundItemStandItem other) => other != null && Name == other.Name &&
            Variants == other.Variants && MaximumQuality == other.MaximumQuality && Orientations.SequenceEqual(other.Orientations);
        public override bool Equals(object other) => Equals(other as UnboundItemStandItem);
        public override int GetHashCode() => Name.GetHashCode() ^ Variants ^ MaximumQuality;
    }
}
