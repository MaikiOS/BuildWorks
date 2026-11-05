using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert Unbound38 file data; native packets and terrain never execute during parsing.
    public sealed class UnboundBlueprint
    {
        public const string Format = "unbound-vtbp-v2";
        public string Text { get; }
        public IReadOnlyList<Piece> Pieces { get; }
        public int TerrainSize { get; }
        public int TerrainLine { get; }
        // One-metre grid, z outer/x inner, centred on the local anchor; heights are offsets.
        public IReadOnlyList<float> Terrain { get; }

        public sealed class Piece
        {
            internal Piece(int line, int start, int length, string[] fields)
            {
                string prefab = fields[0];
                if (string.IsNullOrWhiteSpace(prefab) || prefab != prefab.Trim() || prefab.Length > 256 ||
                    prefab.Any(c => char.IsControl(c) || c == '\uFEFF' || c == '/' || c == '\\' || c == '<' || c == '>'))
                    throw Invalid("invalid prefab at line " + line);
                Position = new Point3(Number(fields[1]), Number(fields[2]), Number(fields[3]));
                Rotation = new Rotation3(Number(fields[4]), Number(fields[5]), Number(fields[6]), Number(fields[7]));
                try { Rotation.Normalized(); }
                catch (ArgumentOutOfRangeException) { throw Invalid("invalid rotation at line " + line); }
                if (!BlueprintForeignZdo.TryReadNative(fields[8], KgBlueprint.StablePrefabHash(prefab), out var state))
                    throw Invalid("invalid or mismatched native state at line " + line);
                Line = line; Start = start; Length = length; PrefabName = prefab; ZdoData = fields[8]; State = state;
            }
            public int Line { get; }
            public int Start { get; }
            public int Length { get; }
            public string PrefabName { get; }
            public Point3 Position { get; }
            public Rotation3 Rotation { get; }
            public string ZdoData { get; }
            public BlueprintForeignZdo State { get; }
        }

        private UnboundBlueprint(string text)
        {
            Text = text;
            var pieces = new List<Piece>();
            Terrain = Array.AsReadOnly(Array.Empty<float>());
            int line = 1;
            for (int start = 0; start < text.Length; ++line)
            {
                int end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n') ++end;
                string row = text.Substring(start, end - start);
                if (line == 1)
                {
                    if (row.StartsWith("\uFEFF", StringComparison.Ordinal)) row = row.Substring(1);
                    if (row != "vtbp 2") throw Invalid("unsupported header");
                }
                else if (row.StartsWith("#terrain|", StringComparison.Ordinal))
                {
                    if (TerrainLine != 0 || pieces.Count != 0) throw Invalid("duplicate or misplaced terrain");
                    int separator = row.IndexOf('|', 9);
                    if (separator < 0 || !int.TryParse(row.Substring(9, separator - 9), NumberStyles.None,
                        CultureInfo.InvariantCulture, out int size) || size < 1 || size % 2 == 0 ||
                        (long)size * size > (row.Length - separator) / 2) throw Invalid("invalid terrain dimensions");
                    var heights = new float[size * size];
                    int field = separator + 1;
                    for (int i = 0; i < heights.Length; ++i)
                    {
                        int next = row.IndexOf('|', field);
                        if ((next < 0) != (i == heights.Length - 1)) throw Invalid("terrain sample count");
                        heights[i] = Number(row.Substring(field, (next < 0 ? row.Length : next) - field));
                        field = next + 1;
                    }
                    TerrainSize = size; TerrainLine = line; Terrain = Array.AsReadOnly(heights);
                }
                else if (!string.IsNullOrWhiteSpace(row))
                {
                    if (row[0] == '#' || row.Count(c => c == '|') != 8) throw Invalid("unknown or invalid row at line " + line);
                    if (pieces.Count >= BlueprintEditorDocument.MaximumParts) throw Invalid("too many parts");
                    pieces.Add(new Piece(line, start, end - start, row.Split('|')));
                }
                start = end;
                if (start < text.Length && text[start++] == '\r' && start < text.Length && text[start] == '\n') ++start;
            }
            Pieces = pieces.AsReadOnly();
        }

        public static UnboundBlueprint Parse(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > BlueprintSourceDocument.MaximumBytes) throw Invalid("source size");
            try
            {
                if (new UTF8Encoding(false, true).GetByteCount(text) > BlueprintSourceDocument.MaximumBytes) throw Invalid("source size");
            }
            catch (EncoderFallbackException) { throw Invalid("invalid UTF-8 source text"); }
            return new UnboundBlueprint(text);
        }

        private static float Number(string text)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) ||
                float.IsNaN(result) || float.IsInfinity(result)) throw Invalid("expected finite Single");
            // .NET Framework TryParse loses negative zero, which remains part of source evidence.
            return result == 0 && text.TrimStart().StartsWith("-", StringComparison.Ordinal)
                ? BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0) : result;
        }
        private static FormatException Invalid(string reason) => new FormatException("Invalid Unbound vtbp 2: " + reason);
    }
}
