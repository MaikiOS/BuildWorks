using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Immutable source evidence. Text retains UTF-8 bytes; the explicit binary profile retains canonical Base64.
    public sealed class BlueprintSourceDocument
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        public const int MaximumSources = 64;
        public const int MaximumTotalBytes = 24 * 1024 * 1024;
        public const string WorldCaptureFormat = "world-capture-v1";
        private readonly Dictionary<int, (string Prefab, int Start, int Length)> partRows =
            new Dictionary<int, (string, int, int)>();
        private Dictionary<int, UnboundBlueprint.Piece> unboundRows;
        private string namedCenter;
        private bool invalidPlacementFrame, otherOpaqueSections;

        public BlueprintSourceDocument(string format, string name, string text)
        {
            if (format != "blueprint" && format != "vbuild" && format != WorldCaptureFormat && format != OpenKeepBlueprint.Format && format != PlannerBlueprint.Format && format != UnboundBlueprint.Format && !BetterBuildingBlueprint.IsFormat(format) && !KgBlueprint.IsFormat(format))
                throw new ArgumentException("Unknown source format.", nameof(format));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name != name.Trim())
                throw new ArgumentException("Invalid source name.", nameof(name));
            foreach (char c in name)
                if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' || c == '<' || c == '>')
                    throw new ArgumentException("Source name must not be a path.", nameof(name));
            bool binary = format == KgBlueprint.BinaryFormat || format == BetterBuildingBlueprint.BinaryFormat;
            int transportLimit = binary ? KgBlueprint.MaximumBinaryTextLength : MaximumBytes;
            if (text == null || text.Length > transportLimit) throw new ArgumentException("Source is too large.", nameof(text));
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            if (bytes.Length > transportLimit) throw new ArgumentException("Source is too large.", nameof(text));
            Format = format; Name = name; Text = text; ByteCount = bytes.Length;
            using (var hash = SHA256.Create())
            {
                byte[] prefix = Encoding.UTF8.GetBytes(format + "\n" + name + "\n");
                hash.TransformBlock(prefix, 0, prefix.Length, null, 0);
                hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                Id = "source_" + BitConverter.ToString(hash.Hash)
                    .Replace("-", "").ToLowerInvariant();
            }
            if (format == WorldCaptureFormat) { ValidateWorldCapture(text); return; }
            if (format == UnboundBlueprint.Format)
            {
                try { Unbound = UnboundBlueprint.Parse(text); }
                catch (FormatException error) { throw new ArgumentException("Invalid Unbound source.", nameof(text), error); }
                unboundRows = Unbound.Pieces.ToDictionary(piece => piece.Line);
                otherOpaqueSections = Unbound.TerrainSize != 0;
                return;
            }
            if (BetterBuildingBlueprint.IsFormat(format))
            {
                try { BetterBuilding = binary ? BetterBuildingBlueprint.ParseBinaryText(text) : BetterBuildingBlueprint.ParseJson(text); }
                catch (FormatException error) { throw new ArgumentException("Invalid or oversized BetterBuilding source.", nameof(text), error); }
                // Other object JSON schemas must not become zero-position pieces through legacy defaults.
                if (!binary && BetterBuilding.UnknownPaths.Any(path => path.EndsWith("/position", StringComparison.OrdinalIgnoreCase) ||
                    path.EndsWith("/rotation", StringComparison.OrdinalIgnoreCase) || path.EndsWith("/transform", StringComparison.OrdinalIgnoreCase)))
                    throw new ArgumentException("Unrecognised BetterBuilding placement frame.", nameof(text));
                if (binary) ByteCount = BetterBuilding.BinaryByteCount;
                otherOpaqueSections = BetterBuilding.Terrain != null || BetterBuilding.UnknownPaths.Count != 0 || BetterBuilding.Pieces.Any(piece => piece.DisplayItem != null);
                return;
            }
            if (KgBlueprint.IsFormat(format))
            {
                try { Kg = binary ? KgBlueprint.ParseBinaryText(text) : KgBlueprint.Parse(text); }
                catch (FormatException error) { throw new ArgumentException("Invalid KG source.", nameof(text), error); }
                if (binary) ByteCount = Kg.BinaryByteCount;
                otherOpaqueSections = Kg.UnknownPaths.Count != 0;
                return;
            }
            if (format == PlannerBlueprint.Format)
            {
                try { Planner = PlannerBlueprint.Parse(text); }
                catch (FormatException error) { throw new ArgumentException("Invalid planner source.", nameof(text), error); }
                otherOpaqueSections = Planner.UnknownPaths.Count != 0;
                return;
            }
            if (format == OpenKeepBlueprint.Format)
            {
                try { OpenKeep = OpenKeepBlueprint.Parse(text, System.IO.Path.GetFileNameWithoutExtension(name)); }
                catch (FormatException error) { throw new ArgumentException("Invalid OpenKeep source.", nameof(text), error); }
                otherOpaqueSections = OpenKeep.UnknownPaths.Count != 0;
                return;
            }
            bool pieces = true;
            bool snapPoints = false;
            bool seenPieces = format == "vbuild";
            int placementHeaders = 0, homesteadHeaders = 0;
            int line = 1;
            for (int start = 0; start < text.Length; ++line)
            {
                int end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n') ++end;
                int first = start, last = end;
                if (first == 0) while (first < last && text[first] == '\uFEFF') ++first;
                while (first < last && char.IsWhiteSpace(text[first])) ++first;
                while (last > first && char.IsWhiteSpace(text[last - 1])) --last;
                if (first < last && text[first] == '#')
                {
                    if (last - first == 7 && string.CompareOrdinal(text, first, "#Pieces", 0, 7) == 0)
                    { pieces = true; seenPieces = true; snapPoints = false; }
                    else if (!HeaderAt(text, first, last, "#Name:") && !HeaderAt(text, first, last, "#Category:") &&
                        !HeaderAt(text, first, last, "#Creator:") && !HeaderAt(text, first, last, "#Description:") &&
                        !(format == "blueprint" && HeaderAt(text, first, last, "#Icon:")) &&
                        !(format == "blueprint" && !seenPieces &&
                            HomesteadMetadataHeader(text.Substring(first, last - first), ref homesteadHeaders)))
                    {
                        pieces = false;
                        snapPoints = format == "blueprint" && last - first == 11 &&
                            string.CompareOrdinal(text, first, "#SnapPoints", 0, 11) == 0;
                        if (!(last - first == 8 && string.CompareOrdinal(text, first, "#Terrain", 0, 8) == 0) &&
                            !(last - first == 11 && string.CompareOrdinal(text, first, "#SnapPoints", 0, 11) == 0) &&
                            !(format == "blueprint" && !seenPieces &&
                                PlacementProvenanceHeader(text, first, last, ref placementHeaders)))
                        {
                            otherOpaqueSections = true;
                            if (format == "blueprint" && (HeaderAt(text, first, last, "#Center") ||
                                HeaderAt(text, first, last, "#Coordinates") || HeaderAt(text, first, last, "#Rotation")))
                                invalidPlacementFrame = true;
                        }
                    }
                }
                else if (pieces && first < last)
                {
                    seenPieces = true;
                    int separator = text.IndexOf(';', first, last - first);
                    if (separator < 0 && format == "vbuild")
                        for (int i = first; i < last; ++i)
                            if (char.IsWhiteSpace(text[i])) { separator = i; break; }
                    if (separator > first)
                    {
                        while (separator > first && char.IsWhiteSpace(text[separator - 1])) --separator;
                        if (partRows.Count >= BlueprintEditorDocument.MaximumParts)
                            throw new ArgumentException("Too many source parts.", nameof(text));
                        partRows.Add(line, (text.Substring(first, separator - first), first, last - first));
                    }
                }
                else if (snapPoints && first < last)
                {
                    int separators = 0;
                    for (int i = first; i < last; ++i)
                        if (text[i] == ';' && ++separators == 3) { ++IgnoredSnapPointRows; break; }
                }
                start = end;
                if (start < text.Length && text[start++] == '\r' && start < text.Length && text[start] == '\n') ++start;
            }
        }

        // Informational source frame only; never another transform or a grant of world authority.
        public static BlueprintSourceDocument CaptureWorld(long worldUid, Point3 position, Rotation3 rotation,
            string hostId, string buildWorksVersion) => new BlueprintSourceDocument(WorldCaptureFormat, "world-capture.txt",
                string.Format(CultureInfo.InvariantCulture,
                    "BuildWorks world capture\t1\nworld\t{0}\nposition\t{1:R}\t{2:R}\t{3:R}\n" +
                    "rotation\t{4:R}\t{5:R}\t{6:R}\t{7:R}\nhost\t{8}\nbuildworks\t{9}",
                    worldUid == 0 ? "unknown" : worldUid.ToString(CultureInfo.InvariantCulture),
                    position.X, position.Y, position.Z, rotation.X, rotation.Y, rotation.Z, rotation.W, hostId, buildWorksVersion));

        private static void ValidateWorldCapture(string text)
        {
            if (text.Length > 1024) throw new ArgumentException("World capture origin is too large.");
            string[] rows = text.Split('\n');
            if (rows.Length != 6 || rows[0] != "BuildWorks world capture\t1")
                throw new ArgumentException("Unknown world capture origin.");
            string[] Row(int index, string key, int count)
            {
                string[] fields = rows[index].Split('\t');
                if (fields.Length != count || fields[0] != key) throw new ArgumentException("Invalid world capture field.");
                return fields;
            }
            double Number(string value)
            {
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) ||
                    !GeometryMath.IsFinite(number) || Math.Abs(number) > float.MaxValue)
                    throw new ArgumentException("Invalid world capture coordinate.");
                return number;
            }
            string world = Row(1, "world", 2)[1];
            if (world != "unknown" && (!long.TryParse(world, NumberStyles.Integer, CultureInfo.InvariantCulture, out long uid) ||
                uid == 0 || uid.ToString(CultureInfo.InvariantCulture) != world)) throw new ArgumentException("Invalid capture world.");
            string[] position = Row(2, "position", 4), rotation = Row(3, "rotation", 5);
            for (int i = 1; i < position.Length; ++i) Number(position[i]);
            double length = 0;
            for (int i = 1; i < rotation.Length; ++i) { double value = Number(rotation[i]); length += value * value; }
            if (Math.Abs(length - 1) > .0001) throw new ArgumentException("Invalid capture rotation.");
            if (!Guid.TryParseExact(Row(4, "host", 2)[1], "N", out Guid host) || host == Guid.Empty)
                throw new ArgumentException("Invalid capture host fingerprint.");
            string version = Row(5, "buildworks", 2)[1];
            if (!Version.TryParse(version, out Version parsed) || parsed.ToString() != version)
                throw new ArgumentException("Invalid capture version.");
        }

        private static bool HeaderAt(string text, int first, int last, string header) =>
            last - first >= header.Length && string.CompareOrdinal(text, first, header, 0, header.Length) == 0;

        // Exact descriptive records only; their raw text is retained, never applied to terrain or world state.
        public static bool HomesteadMetadataHeader(string line, ref int seen)
        {
            int colon = line.IndexOf(':');
            if (colon < 0) return false;
            string key = line.Substring(0, colon);
            int flag;
            switch (key)
            {
                case "#HomesteadVersion": flag = 1; break;
                case "#HomesteadWorld": flag = 2; break;
                case "#HomesteadSavedAt": flag = 4; break;
                case "#HomesteadRadius": flag = 8; break;
                case "#HomesteadTerrainContact": flag = 0; break;
                default: return false;
            }
            if (flag != 1 && (seen & 1) == 0) return false;
            if ((seen & flag) != 0) return false;
            string value = line.Substring(colon + 1).Trim();
            if (value.Length > 256) return false;
            foreach (char c in value) if (char.IsControl(c)) return false;
            if (flag == 1)
            {
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version) || version != 1)
                    return false;
            }
            else if (flag == 8 || flag == 0)
            {
                string[] fields = value.Split(';');
                if (fields.Length != (flag == 8 ? 1 : 3)) return false;
                foreach (string field in fields)
                    if (!float.TryParse(field.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                        float.IsNaN(number) || float.IsInfinity(number) || flag == 8 && number < 0) return false;
            }
            seen |= flag;
            return true;
        }

        private bool PlacementProvenanceHeader(string text, int first, int last, ref int seen)
        {
            int flag, offset;
            if (HeaderAt(text, first, last, "#Center:")) { flag = 1; offset = 8; }
            else if (HeaderAt(text, first, last, "#Coordinates:")) { flag = 2; offset = 13; }
            else if (HeaderAt(text, first, last, "#Rotation:")) { flag = 4; offset = 10; }
            else return false;
            if ((seen & flag) != 0) return false;
            seen |= flag;
            // Saved world pose is provenance, not another transform of the already-local piece rows.
            if (flag == 1)
            {
                if (first + offset == last) return true;
                string value = text.Substring(first + offset, last - first - offset);
                if (value.Length > 128) return false;
                foreach (char c in value)
                    if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' || c == ';') return false;
                namedCenter = value;
                return true;
            }
            if (last - first - offset > 256) return false;
            string[] values = text.Substring(first + offset, last - first - offset).Split(',');
            if (values.Length != 3) return false;
            foreach (string value in values)
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) ||
                    float.IsNaN(number) || float.IsInfinity(number)) return false;
            return true;
        }

        public string Id { get; }
        public string Format { get; }
        public string Name { get; }
        public string Text { get; }
        public int ByteCount { get; }
        public PlannerBlueprint Planner { get; }
        public bool HasLayerAddress(string layer) => Planner != null && Planner.Layers.Any(l => l.Id == layer);
        public OpenKeepBlueprint OpenKeep { get; }
        public KgBlueprint Kg { get; }
        public BetterBuildingBlueprint BetterBuilding { get; }
        public UnboundBlueprint Unbound { get; }
        public byte[] OriginalBytes() => Format == BetterBuildingBlueprint.BinaryFormat ? BetterBuilding.BinaryBytes() : Kg?.IsBinary == true ? Kg.BinaryBytes() : new UTF8Encoding(false, true).GetBytes(Text);
        public bool HasPlacementFrame => NamedCenterPrefab != null || Kg != null || BetterBuilding != null || Planner != null || Unbound != null;
        public string NamedCenterPrefab => invalidPlacementFrame ? null : namedCenter;
        public bool HasOpaqueSections => HasUnsupportedSections(frameApplied: false);
        public bool HasUnsupportedSections(bool frameApplied) => otherOpaqueSections ||
            (HasPlacementFrame && !frameApplied);
        public int IgnoredSnapPointRows { get; }

        // A raw source may be shared by both interpreted and legacy instances.
        public static IEnumerable<BlueprintSourceDocument> Unresolved(
            IEnumerable<BlueprintSourceDocument> sources,
            IEnumerable<(string SourceId, bool FrameApplied)> bindings)
        {
            if (sources == null) yield break;
            var applied = new HashSet<string>(StringComparer.Ordinal);
            var pending = new HashSet<string>(StringComparer.Ordinal);
            foreach (var binding in bindings ?? Array.Empty<(string SourceId, bool FrameApplied)>())
            {
                if (binding.SourceId == null) continue;
                if (binding.FrameApplied) applied.Add(binding.SourceId);
                else pending.Add(binding.SourceId);
            }
            applied.ExceptWith(pending);
            foreach (var source in sources)
            {
                if (source == null) throw new ArgumentException("Null source evidence.", nameof(sources));
                if (source.HasUnsupportedSections(applied.Contains(source.Id))) yield return source;
            }
        }

        public bool HasPartAddress(int line, int record, string prefab) => OpenKeep != null
            ? line == 0 && record >= 1 && record <= OpenKeep.Pieces.Count && OpenKeep.Pieces[record - 1].Prefab == prefab
            : Planner != null ? line == 0 && record >= 1 && record <= Planner.Pieces.Count && Planner.Pieces[record - 1].Prefab == prefab
            : BetterBuilding != null ? line == 0 && record >= 1 && record <= BetterBuilding.Pieces.Count && BetterBuilding.Pieces[record - 1].Prefab == prefab
            : Kg != null ? line == 0 && record >= 1 && record <= Kg.Objects.Count && Kg.Objects[record - 1].MatchesPrefab(prefab)
            : record == 0 && HasPartAt(line, prefab);

        public bool HasPartAt(int line, string prefab)
        {
            if (unboundRows != null) return unboundRows.TryGetValue(line, out var piece) && piece.PrefabName == prefab;
            return partRows.TryGetValue(line, out var row) && row.Prefab == prefab;
        }

        public UnboundBlueprint.Piece UnboundPartAt(int line) => unboundRows != null && unboundRows.TryGetValue(line, out var piece) ? piece : null;
        public string PartRow(int line)
        {
            if (Unbound != null)
            {
                var piece = UnboundPartAt(line);
                return piece == null ? null : Text.Substring(piece.Start, piece.Length);
            }
            return partRows.TryGetValue(line, out var row) ? Text.Substring(row.Start, row.Length) : null;
        }

        public bool HasPartState(int line, int record, string prefab, BlueprintForeignPartData state,
            BlueprintPrefabMetadata metadata = null, Point3? basis = null, bool absoluteScale = false, string text = null, int? seed = null)
        {
            if (Unbound == null) return state?.Format != UnboundBlueprint.Format;
            var original = UnboundPartAt(line);
            if (record != 0 || original == null || original.PrefabName != prefab) return false;
            if (state?.Format == UnboundBlueprint.Format) return state.Info == prefab && state.Zdo == original.ZdoData;
            return UnboundStateProjection.Matches(original, metadata, basis, absoluteScale, text, seed, state);
        }

        public static Dictionary<string, BlueprintSourceDocument> Index(IEnumerable<BlueprintSourceDocument> sources)
        {
            var index = new Dictionary<string, BlueprintSourceDocument>(StringComparer.Ordinal);
            int total = 0;
            if (sources == null) return index;
            foreach (BlueprintSourceDocument source in sources)
            {
                if (source == null || index.Count >= MaximumSources || index.ContainsKey(source.Id))
                    throw new ArgumentException("Invalid or duplicate source document.");
                // Charge expanded BBPF as well: compression must not bypass the aggregate source budget.
                total += Math.Max(source.ByteCount, source.BetterBuilding?.RawByteCount ?? 0);
                if (total > MaximumTotalBytes) throw new ArgumentException("Source documents are too large.");
                index.Add(source.Id, source);
            }
            return index;
        }
    }
}
