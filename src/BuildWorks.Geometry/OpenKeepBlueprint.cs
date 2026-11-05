using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Descriptive source data only. Reading a site never authorizes changing the world.
    public sealed class OpenKeepBlueprint
    {
        public const string Format = "openkeep-json-v1";
        private readonly List<string> unknown = new List<string>();
        private readonly Dictionary<int, (int Start, int Length)> extraFields = new Dictionary<int, (int, int)>();
        private readonly int rootStart, piecesStart, piecesLength, nameStart, nameLength;
        public string Text { get; }
        public string Name { get; }
        public string Description { get; }
        public string Source { get; }
        public string Limits { get; }
        public IReadOnlyList<Piece> Pieces { get; }
        public bool HasSite { get; }
        public bool HasTerrain { get; }
        public double? LevelHalf { get; }
        public IReadOnlyList<TerrainStep> Terrain { get; }
        public ReliefGrid Relief { get; }
        public WaterRule Water { get; }
        public string Clear { get; }
        public double? ClearRadius { get; }
        public Point3? Stand { get; }
        public CaptureOrigin CopiedFrom { get; }
        public IReadOnlyDictionary<string, Point3> Marks { get; }
        public IReadOnlyDictionary<string, Point3> Aims { get; }
        public IReadOnlyDictionary<string, string> MarkNotes { get; }
        public IReadOnlyList<string> UnknownPaths { get; }
        public TerrainStep DefaultPad { get; }

        private OpenKeepBlueprint(Node root, string text, string fallbackName)
        {
            Text = text;
            Object(root, "/");
            rootStart = root.Start;
            Node nameNode = Get(root, "name");
            if (nameNode != null) { nameStart = nameNode.Start; nameLength = nameNode.Length; }
            Unknown(root, "", "name", "description", "pieces", "site", "source", "copied_from", "marks", "aims", "mark_notes");
            Name = OptionalText(root, "name") ?? fallbackName;
            Description = OptionalText(root, "description") ?? "";
            Source = OptionalText(root, "source");
            var rows = Array(Get(root, "pieces"), "/pieces");
            piecesStart = Get(root, "pieces").Start; piecesLength = Get(root, "pieces").Length;
            if (rows.Count > BlueprintEditorDocument.MaximumParts) throw Invalid("/pieces: too many parts");
            var pieces = new List<Piece>(rows.Count);
            for (int i = 0; i < rows.Count; ++i)
            {
                string path = "/pieces/" + i;
                var row = Array(rows[i], path);
                if (row.Count < 5) throw Invalid(path + ": expected [prefab,x,y,z,yaw]");
                string prefab = Scalar(row[0], path + "/0");
                if (string.IsNullOrWhiteSpace(prefab) || prefab.Length > 256 || prefab.Any(char.IsControl))
                    throw Invalid(path + ": invalid prefab");
                pieces.Add(new Piece(i + 1, prefab, new Point3(Number(row[1], path), Number(row[2], path),
                    Number(row[3], path)), Number(row[4], path)));
                if (row.Count > 5)
                {
                    unknown.Add(path + "/5+");
                    int start = row[4].Start + row[4].Length;
                    extraFields.Add(i + 1, (start, rows[i].Start + rows[i].Length - 1 - start));
                }
            }
            Pieces = pieces.AsReadOnly();
            Marks = Vectors(Get(root, "marks"), "/marks"); Aims = Vectors(Get(root, "aims"), "/aims");
            var notes = new Dictionary<string, string>(StringComparer.Ordinal);
            Node noteNode = Get(root, "mark_notes");
            if (noteNode != null)
                foreach (var pair in Object(noteNode, "/mark_notes")) notes.Add(pair.Key, Scalar(pair.Value, "/mark_notes"));
            MarkNotes = new ReadOnlyDictionary<string, string>(notes);
            Node origin = Get(root, "copied_from");
            if (origin != null)
            {
                Object(origin, "/copied_from"); Unknown(origin, "/copied_from", "at", "ground", "facing");
                CopiedFrom = new CaptureOrigin(Vector(Get(origin, "at"), "/copied_from/at", 2),
                    Number(Get(origin, "ground"), "/copied_from/ground"), Number(Get(origin, "facing"), "/copied_from/facing"));
            }
            var steps = new List<TerrainStep>();
            Node site = Get(root, "site"); HasSite = site != null;
            if (site != null)
            {
                Object(site, "/site");
                Unknown(site, "/site", "terrain", "level_half", "relief", "water", "clear", "clear_radius", "stand", "limits");
                Limits = OptionalText(site, "limits");
                LevelHalf = OptionalNumber(site, "level_half", true);
                Clear = OptionalText(site, "clear"); ClearRadius = OptionalNumber(site, "clear_radius", true);
                if (Get(site, "stand") is Node stand) Stand = Vector(stand, "/site/stand", 2);
                Node terrain = Get(site, "terrain"); HasTerrain = terrain != null;
                if (terrain != null)
                {
                    var values = Array(terrain, "/site/terrain");
                    for (int i = 0; i < values.Count; ++i)
                    {
                        Node step = values[i];
                        string path = "/site/terrain/" + i;
                        Object(step, path);
                        Unknown(step, path, "op", "at", "half", "y", "paint", "shape", "clear_here");
                        string op = Scalar(Get(step, "op"), path + "/op");
                        if (op != "level" && op != "paint") throw Invalid("/site/terrain: unknown operation");
                        Point3 at = Vector(Get(step, "at"), "/site/terrain/at", 2);
                        double half = Nonnegative(Number(Get(step, "half"), "/site/terrain/half"));
                        double y = OptionalNumber(step, "y", false) ?? 0;
                        string paint = OptionalText(step, "paint");
                        int? paintCode = op == "paint" ? (int?)PaintCode(paint) : null;
                        string shape = OptionalText(step, "shape") ?? "square";
                        if (shape != "square" && shape != "circle") unknown.Add(path + "/shape");
                        bool clear = Get(step, "clear_here") is Node flag && Boolean(flag, "/site/terrain/clear_here");
                        steps.Add(new TerrainStep(op, at.X, at.Z, half, y, paint, paintCode, shape, clear));
                    }
                }
                Node relief = Get(site, "relief");
                if (relief != null)
                {
                    Object(relief, "/site/relief"); Unknown(relief, "/site/relief", "x", "z", "width", "depth", "offsets");
                    int width = Dimension(Get(relief, "width")), depth = Dimension(Get(relief, "depth"));
                    var offsets = Array(Get(relief, "offsets"), "/site/relief/offsets");
                    if ((long)width * depth != offsets.Count) throw Invalid("/site/relief: grid size mismatch");
                    Relief = new ReliefGrid(Number(Get(relief, "x"), "/site/relief/x"), Number(Get(relief, "z"), "/site/relief/z"),
                        width, depth, offsets.Select(v => Number(v, "/site/relief/offsets")).ToArray());
                }
                Node water = Get(site, "water");
                if (water != null)
                {
                    Object(water, "/site/water"); Unknown(water, "/site/water", "floor", "depth");
                    Water = new WaterRule(Number(Get(water, "floor"), "/site/water/floor"),
                        Nonnegative(Number(Get(water, "depth"), "/site/water/depth")));
                }
            }
            Terrain = steps.AsReadOnly();
            if (Relief == null && !steps.Any(s => s.Operation == "level"))
            {
                if (!HasTerrain && LevelHalf.HasValue) DefaultPad = new TerrainStep("level", 0, 0, LevelHalf.Value, 0);
                else if (pieces.Count > 0)
                {
                    var low = pieces.Where(p => p.Position.Y < 1).ToArray();
                    IEnumerable<Piece> bounds = low.Length > 0 ? low : pieces;
                    double minX = bounds.Min(p => p.Position.X), maxX = bounds.Max(p => p.Position.X);
                    double minZ = bounds.Min(p => p.Position.Z), maxZ = bounds.Max(p => p.Position.Z);
                    DefaultPad = new TerrainStep("level", (minX + maxX) / 2, (minZ + maxZ) / 2,
                        Math.Max(maxX - minX, maxZ - minZ) / 2 + .5, 0);
                }
            }
            UnknownPaths = unknown.AsReadOnly();
        }

        public static OpenKeepBlueprint Parse(string text, string fallbackName)
            => new OpenKeepBlueprint(ReadJson(text), text, fallbackName);

        // Known site/provenance remain in their original frame; only the requested tuples/name change.
        public string WritePieces(IEnumerable<(string Prefab, Point3 Position, double Yaw, int Record)> pieces, string name = null)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            var output = new StringBuilder("[");
            int count = 0;
            foreach (var piece in pieces)
            {
                if (++count > BlueprintEditorDocument.MaximumParts) throw new ArgumentException("Too many OpenKeep output parts.");
                if (piece.Record < 0 || piece.Record > Pieces.Count) throw new ArgumentException("Invalid source record.");
                if (string.IsNullOrWhiteSpace(piece.Prefab) || piece.Prefab.Length > 256 || piece.Prefab.Any(char.IsControl))
                    throw new ArgumentException("Invalid output prefab.");
                if (count > 1) output.Append(',');
                output.Append('[').Append(JsonString(piece.Prefab)).Append(',').Append(OutputNumber(piece.Position.X)).Append(',')
                    .Append(OutputNumber(piece.Position.Y)).Append(',').Append(OutputNumber(piece.Position.Z)).Append(',').Append(OutputNumber(piece.Yaw));
                if (extraFields.TryGetValue(piece.Record, out var tail)) output.Append(Text, tail.Start, tail.Length);
                output.Append(']');
                if (output.Length > BlueprintSourceDocument.MaximumBytes) throw new ArgumentException("OpenKeep output is too large.");
            }
            output.Append(']');
            var patches = new List<(int Start, int Length, string Text)> { (piecesStart, piecesLength, output.ToString()) };
            if (name != null)
            {
                if (name.Length > BlueprintSourceDocument.MaximumBytes) throw new ArgumentException("Output name is too large.");
                patches.Add(nameLength > 0 ? (nameStart, nameLength, JsonString(name)) : (rootStart + 1, 0, "\"name\":" + JsonString(name) + ","));
            }
            string result = Text;
            foreach (var patch in patches.OrderByDescending(p => p.Start))
                result = result.Substring(0, patch.Start) + patch.Text + result.Substring(patch.Start + patch.Length);
            Parse(result, name ?? Name); // Validate the complete output before any caller can publish it.
            return result;
        }

        private static string OutputNumber(double value)
        {
            if (!GeometryMath.IsFinite(value) || Math.Abs(value) > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            return ((float)value).ToString("R", CultureInfo.InvariantCulture);
        }
        private static string JsonString(string value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(string)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }

        public static bool IsJsonBlueprint(string text)
            => ReadJson(text).Map?.ContainsKey("pieces") == true;

        internal static Node ReadJson(string text)
        {
            if (text == null || text.Length > BlueprintSourceDocument.MaximumBytes) throw Invalid("JSON too large");
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            if (bytes.Length > BlueprintSourceDocument.MaximumBytes) throw Invalid("JSON too large");
            return new JsonInput(text).Document();
        }

        public double? GroundOffset(double x, double z)
        {
            double? sampled = Relief?.At(x, z);
            if (sampled.HasValue) return sampled;
            for (int i = Terrain.Count - 1; i >= 0; --i)
                if (Terrain[i].Operation == "level" && Terrain[i].ContainsOpenKeep(x, z)) return Terrain[i].Y;
            return DefaultPad != null && DefaultPad.ContainsOpenKeep(x, z) ? (double?)DefaultPad.Y : null;
        }

        private void Unknown(Node value, string path, params string[] keys)
        {
            foreach (string key in value.Map.Keys)
                if (!keys.Contains(key, StringComparer.Ordinal)) unknown.Add(path + "/" + key.Replace("~", "~0").Replace("/", "~1"));
        }
        private static IReadOnlyDictionary<string, Point3> Vectors(Node node, string path)
        {
            var result = new Dictionary<string, Point3>(StringComparer.Ordinal);
            if (node != null) foreach (var pair in Object(node, path)) result.Add(pair.Key, Vector(pair.Value, path, 3));
            return new ReadOnlyDictionary<string, Point3>(result);
        }
        private static Point3 Vector(Node node, string path, int count)
        {
            var values = Array(node, path);
            if (values.Count != count) throw Invalid(path + ": invalid vector size");
            return count == 2 ? new Point3(Number(values[0], path), 0, Number(values[1], path)) :
                new Point3(Number(values[0], path), Number(values[1], path), Number(values[2], path));
        }
        private static Dictionary<string, Node> Object(Node node, string path) =>
            node?.Map ?? throw Invalid(path + ": expected object");
        private static List<Node> Array(Node node, string path) => node?.Items ?? throw Invalid(path + ": expected array");
        private static Node Get(Node node, string key) => node.Map.TryGetValue(key, out Node value) ? value : null;
        private static string OptionalText(Node node, string key) => Get(node, key) is Node value && value.Kind != "null" ? Scalar(value, key) : null;
        private static string Scalar(Node node, string path) => node != null &&
            (node.Kind == "string" || node.Kind == "number" || node.Kind == "boolean") ? node.Value : throw Invalid(path + ": expected scalar");
        private static double Number(Node node, string path)
        {
            if (node == null || (node.Kind != "string" && node.Kind != "number") ||
                !float.TryParse(node.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ||
                float.IsNaN(value) || float.IsInfinity(value)) throw Invalid(path + ": expected finite float");
            return value;
        }
        private static double? OptionalNumber(Node node, string key, bool nonnegative)
        {
            Node value = Get(node, key);
            if (value == null) return null;
            double number = Number(value, key); return nonnegative ? Nonnegative(number) : number;
        }
        private static double Nonnegative(double value) => value >= 0 ? value : throw Invalid("Negative site size/depth");
        private static int Dimension(Node node)
        {
            string scalar = Scalar(node, "/site/relief/dimension");
            if (!double.TryParse(scalar, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                !GeometryMath.IsFinite(value) || value < 2 || value > BlueprintSourceDocument.MaximumBytes || value != Math.Floor(value))
                throw Invalid("Invalid relief dimension");
            return (int)value;
        }
        private static bool Boolean(Node node, string path) => node.Kind == "boolean" ? node.Value == "true" : throw Invalid(path + ": expected boolean");
        private static int PaintCode(string value)
        {
            string[] names = { "none", "dirt", "cultivated", "paved", "grass", "clearvegetation" };
            for (int i = 0; i < names.Length; ++i) if (string.Equals(value, names[i], StringComparison.OrdinalIgnoreCase)) return i;
            if (string.Equals(value, "original", StringComparison.OrdinalIgnoreCase)) return 8;
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int code) && (code >= 0 && code <= 5 || code == 8)) return code;
            throw Invalid("Unknown terrain paint");
        }
        private static FormatException Invalid(string message) => new FormatException("OpenKeep: " + message);

        internal sealed class Node
        {
            internal string Kind, Value;
            internal int Start, Length;
            internal Dictionary<string, Node> Map;
            internal List<Node> Items;
        }
        // The host JSON/XML reader accepts trailing documents, commas and leading-zero numbers.
        // Validate the actual JSON grammar here before trusting source-record addresses.
        private sealed class JsonInput
        {
            private readonly string text;
            private int position, remaining = 1024 * 1024;
            internal JsonInput(string text) { this.text = text; if (text.Length > 0 && text[0] == '\uFEFF') position = 1; }
            internal Node Document()
            {
                Node value = Read(0); Space();
                if (position != text.Length) throw Invalid("Trailing JSON content");
                return value;
            }
            private Node Read(int depth)
            {
                Space();
                int start = position;
                Node node = ReadValue(depth); node.Start = start; node.Length = position - start; return node;
            }
            private Node ReadValue(int depth)
            {
                if (depth > 32 || --remaining < 0 || position == text.Length) throw Invalid("JSON depth/value limit or missing value");
                char first = text[position];
                if (first == '{' || first == '[')
                {
                    ++position;
                    var node = new Node { Kind = first == '{' ? "object" : "array" };
                    if (first == '{') node.Map = new Dictionary<string, Node>(StringComparer.Ordinal);
                    else node.Items = new List<Node>();
                    char end = first == '{' ? '}' : ']';
                    Space();
                    if (Take(end)) return node;
                    while (true)
                    {
                        Space();
                        string key = null;
                        if (node.Map != null)
                        {
                            key = String(); Space();
                            if (!Take(':')) throw Invalid("Missing JSON colon");
                            if (node.Map.ContainsKey(key)) throw Invalid("Duplicate JSON key: " + key);
                        }
                        Node child = Read(depth + 1);
                        if (node.Map != null) node.Map.Add(key, child);
                        else node.Items.Add(child);
                        Space();
                        if (Take(end)) return node;
                        if (!Take(',')) throw Invalid("Missing JSON separator");
                    }
                }
                if (first == '"') return new Node { Kind = "string", Value = String() };
                if (first == 't' || first == 'f' || first == 'n')
                {
                    string literal = first == 't' ? "true" : first == 'f' ? "false" : "null";
                    if (text.Length - position < literal.Length || string.CompareOrdinal(text, position, literal, 0, literal.Length) != 0)
                        throw Invalid("Invalid JSON literal");
                    position += literal.Length; return new Node { Kind = literal == "null" ? "null" : "boolean", Value = literal };
                }
                int start = position;
                Take('-');
                if (!Take('0')) Digits(true);
                if (Take('.')) Digits(true);
                if (Take('e') || Take('E')) { if (!Take('+')) Take('-'); Digits(true); }
                if (start == position) throw Invalid("Invalid JSON value");
                string number = text.Substring(start, position - start);
                if (!double.TryParse(number, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) || !GeometryMath.IsFinite(value))
                    throw Invalid("Nonfinite JSON number");
                return new Node { Kind = "number", Value = number };
            }
            private string String()
            {
                if (!Take('"')) throw Invalid("Expected JSON string");
                var value = new StringBuilder();
                while (position < text.Length)
                {
                    char c = text[position++];
                    if (c == '"')
                    {
                        string result = value.ToString();
                        new UTF8Encoding(false, true).GetByteCount(result);
                        return result;
                    }
                    if (c < 32) throw Invalid("Control character in JSON string");
                    if (c == '\\')
                    {
                        if (position == text.Length) throw Invalid("Incomplete JSON escape");
                        switch (text[position++])
                        {
                            case '"': c = '"'; break;
                            case '\\': c = '\\'; break;
                            case '/': c = '/'; break;
                            case 'b': c = '\b'; break;
                            case 'f': c = '\f'; break;
                            case 'n': c = '\n'; break;
                            case 'r': c = '\r'; break;
                            case 't': c = '\t'; break;
                            case 'u':
                                if (position + 4 > text.Length || !ushort.TryParse(text.Substring(position, 4),
                                    NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out ushort code)) throw Invalid("Invalid Unicode escape");
                                position += 4; c = (char)code; break;
                            default: throw Invalid("Invalid JSON escape");
                        }
                    }
                    value.Append(c);
                }
                throw Invalid("Unterminated JSON string");
            }
            private bool Take(char value)
            { if (position >= text.Length || text[position] != value) return false; ++position; return true; }
            private void Digits(bool required)
            {
                int start = position;
                while (position < text.Length && text[position] >= '0' && text[position] <= '9') ++position;
                if (required && position == start) throw Invalid("Missing JSON digits");
            }
            private void Space()
            { while (position < text.Length && (text[position] == ' ' || text[position] == '\t' || text[position] == '\r' || text[position] == '\n')) ++position; }
        }

        public sealed class Piece
        {
            internal Piece(int record, string prefab, Point3 position, double yaw) { Record = record; Prefab = prefab; Position = position; Yaw = yaw; }
            public int Record { get; }
            public string Prefab { get; }
            public Point3 Position { get; }
            public double Yaw { get; }
            public Rotation3 Rotation { get { double angle = (Yaw % 360) * Math.PI / 360; return new Rotation3(0, Math.Sin(angle), 0, Math.Cos(angle)); } }
        }
        public sealed class TerrainStep
        {
            internal TerrainStep(string op, double x, double z, double half, double y, string paint = null,
                int? paintCode = null, string shape = "square", bool clear = false)
            { Operation = op; X = x; Z = z; Half = half; Y = y; Paint = paint; PaintCode = paintCode; Shape = shape; ClearHere = clear; }
            public string Operation { get; }
            public double X { get; }
            public double Z { get; }
            public double Half { get; }
            public double Y { get; }
            public string Paint { get; }
            public int? PaintCode { get; }
            public string Shape { get; }
            public bool ClearHere { get; }
            // OpenKeep uses squares even for DevBridge's shape="circle" extension.
            public bool ContainsOpenKeep(double x, double z) => Math.Abs(x - X) <= Half && Math.Abs(z - Z) <= Half;
        }
        public sealed class ReliefGrid
        {
            internal ReliefGrid(double x, double z, int width, int depth, double[] offsets)
            { X = x; Z = z; Width = width; Depth = depth; Offsets = System.Array.AsReadOnly(offsets); }
            public double X { get; }
            public double Z { get; }
            public int Width { get; }
            public int Depth { get; }
            public IReadOnlyList<double> Offsets { get; }
            public double? At(double x, double z)
            {
                double dx = x - X, dz = z - Z;
                if (!GeometryMath.IsFinite(dx) || !GeometryMath.IsFinite(dz) || dx < 0 || dz < 0 || dx > Width - 1 || dz > Depth - 1) return null;
                int ix = Math.Min((int)dx, Width - 2), iz = Math.Min((int)dz, Depth - 2), start = iz * Width + ix;
                double u = dx - ix, v = dz - iz;
                double front = Offsets[start] * (1 - u) + Offsets[start + 1] * u;
                double back = Offsets[start + Width] * (1 - u) + Offsets[start + Width + 1] * u;
                return front * (1 - v) + back * v;
            }
        }
        public sealed class WaterRule
        {
            internal WaterRule(double floor, double depth) { Floor = floor; Depth = depth; }
            public double Floor { get; }
            public double Depth { get; }
        }
        public sealed class CaptureOrigin
        {
            internal CaptureOrigin(Point3 at, double ground, double facing) { At = at; Ground = ground; Facing = facing; }
            public Point3 At { get; }
            public double Ground { get; }
            public double Facing { get; }
            public double FrameYaw => ((Facing + 180) % 360 + 360) % 360;
        }
    }
}
