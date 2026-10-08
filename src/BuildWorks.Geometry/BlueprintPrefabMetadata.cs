using System;

namespace OstrixMods.BuildWorks.Geometry
{
    // A recorded visual envelope, never a model, snap surface or permission grant.
    public sealed class BlueprintPrefabMetadata
    {
        public BlueprintPrefabMetadata(int version, string prefabId, string name, string kind, string hostId,
            Point3 prefabScale, Point3? minimum = null, Point3? maximum = null,
            string sourceId = null, string sourceVersion = null)
        {
            if (version != 1) throw new ArgumentException("Unknown prefab metadata version.");
            Label(prefabId, 128); Label(name, 256); Label(kind, 32); Label(hostId, 128);
            if (sourceId != null) Label(sourceId, 128);
            if (sourceVersion != null) { Label(sourceVersion, 64); if (sourceId == null) throw new ArgumentException("Source version has no source."); }
            BlueprintEditorPart.NormalizeScale(prefabScale, nameof(prefabScale), true);
            if (minimum.HasValue != maximum.HasValue) throw new ArgumentException("Incomplete prefab bounds.");
            if (minimum.HasValue)
            {
                BoundsPoint(minimum.Value); BoundsPoint(maximum.Value);
                if (minimum.Value.X > maximum.Value.X || minimum.Value.Y > maximum.Value.Y || minimum.Value.Z > maximum.Value.Z)
                    throw new ArgumentException("Reversed prefab bounds.");
            }
            Version = version; PrefabId = prefabId; Name = name; Kind = kind; HostId = hostId;
            PrefabScale = prefabScale; Minimum = minimum; Maximum = maximum;
            SourceId = sourceId; SourceVersion = sourceVersion;
        }

        public int Version { get; }
        public string PrefabId { get; }
        public string Name { get; }
        public string Kind { get; }
        public string HostId { get; }
        public Point3 PrefabScale { get; }
        public Point3? Minimum { get; }
        public Point3? Maximum { get; }
        public string SourceId { get; }
        public string SourceVersion { get; }

        public void ValidateEnvelope(Point3 rootScale)
        {
            if (!Minimum.HasValue) return;
            if (Math.Max(Math.Abs(Minimum.Value.X), Math.Abs(Maximum.Value.X)) * rootScale.X > float.MaxValue / 4.0 ||
                Math.Max(Math.Abs(Minimum.Value.Y), Math.Abs(Maximum.Value.Y)) * rootScale.Y > float.MaxValue / 4.0 ||
                Math.Max(Math.Abs(Minimum.Value.Z), Math.Abs(Maximum.Value.Z)) * rootScale.Z > float.MaxValue / 4.0)
                throw new ArgumentException("Prefab envelope overflows the renderer range.");
        }

        private static void Label(string text, int limit)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > limit || text.Trim() != text)
                throw new ArgumentException("Invalid prefab metadata label.");
            foreach (char c in text) if (char.IsControl(c) || c == '<' || c == '>') throw new ArgumentException("Invalid prefab metadata label.");
        }
        private static void BoundsPoint(Point3 point)
        {
            BlueprintEditorPart.Validate(point, nameof(point));
            if (Math.Abs(point.X) > 10000 || Math.Abs(point.Y) > 10000 || Math.Abs(point.Z) > 10000)
                throw new ArgumentException("Prefab bounds outside supported range.");
        }
    }
}
