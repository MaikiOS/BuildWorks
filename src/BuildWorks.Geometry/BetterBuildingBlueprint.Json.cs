using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using Node = OstrixMods.BuildWorks.Geometry.OpenKeepBlueprint.Node;

namespace OstrixMods.BuildWorks.Geometry
{
    public sealed partial class BetterBuildingBlueprint
    {
        public const string JsonFormat = "betterbuilding-legacy-json-v1";
        public bool IsBinary => BinaryByteCount != 0;
        public IReadOnlyList<string> UnknownPaths { get; } = Array.Empty<string>();
        public static bool IsFormat(string format) => format == BinaryFormat || format == JsonFormat;

        public static BetterBuildingBlueprint ParseJson(string text)
        {
            try { return new BetterBuildingBlueprint(text, OpenKeepBlueprint.ReadJson(text)); }
            catch (Exception error) when (error is ArgumentException || error is OverflowException || error is FormatException)
            { throw new FormatException("Invalid or oversized BetterBuilding legacy JSON.", error); }
        }

        // JSON has no magic; object piece records distinguish it from OpenKeep tuples.
        public static bool IsJsonBlueprint(string text)
        {
            Node root = OpenKeepBlueprint.ReadJson(text);
            if (root.Map == null) return false;
            Node pieces = Field(root, "pieces");
            return pieces?.Items != null && (pieces.Items.Count == 0 ? Field(root, "formatVersion") != null :
                pieces.Items[0].Map != null && (Field(root, "formatVersion") != null || Field(pieces.Items[0], "prefab") != null));
        }

        private BetterBuildingBlueprint(string text, Node root)
        {
            var unknown = new List<string>();
            void Fields(Node node, string path, params string[] known)
            {
                if (node?.Map == null) throw Invalid("JSON expected object: " + path);
                var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string key in node.Map.Keys)
                {
                    if (!names.Add(key)) throw Invalid("Ambiguous JSON field: " + path + "/" + key);
                    if (!known.Contains(key, StringComparer.OrdinalIgnoreCase))
                        unknown.Add(path + "/" + key.Replace("~", "~0").Replace("/", "~1"));
                }
            }
            Fields(root, "", "formatVersion", "name", "pieces", "terrain");
            PayloadVersion = Integer(root, "formatVersion", 1); Version(PayloadVersion);
            Name = StringValue(root, "name") ?? "Copied building"; ValidateName(Name);
            Node list = Field(root, "pieces");
            if (list?.Items == null || list.Items.Count < 1 || list.Items.Count > BlueprintEditorDocument.MaximumParts)
                throw Invalid("JSON piece count");
            var rows = new List<Piece>(list.Items.Count);
            for (int i = 0; i < list.Items.Count; ++i)
            {
                Node row = list.Items[i]; string path = "/pieces/" + i;
                Fields(row, path, "prefab", "signText", "attachment", "scale", "x", "y", "z", "qx", "qy", "qz", "qw");
                Node item = Field(row, "attachment"), scale = Field(row, "scale");
                Attachment attachment = null; Point3? multiplier = null;
                if (item != null)
                {
                    Fields(item, path + "/attachment", "prefab", "variant", "orientation");
                    attachment = new Attachment(StringValue(item, "prefab"), Integer(item, "variant"), Integer(item, "orientation"));
                }
                if (scale != null)
                {
                    Fields(scale, path + "/scale", "x", "y", "z");
                    multiplier = new Point3(Number(scale, "x", 1), Number(scale, "y", 1), Number(scale, "z", 1));
                }
                rows.Add(new Piece(i + 1, StringValue(row, "prefab"), new Point3(Number(row, "x"), Number(row, "y"), Number(row, "z")),
                    new Rotation3(Number(row, "qx"), Number(row, "qy"), Number(row, "qz"), Number(row, "qw", 1)),
                    StringValue(row, "signText"), attachment, multiplier));
            }
            CheckJsonVersion(PayloadVersion, rows);
            Node terrain = Field(root, "terrain");
            if (terrain != null)
            {
                Fields(terrain, "/terrain", "version", "width", "spacing", "radius", "originX", "originZ", "heights");
                int width = Integer(terrain, "width");
                Node heights = Field(terrain, "heights");
                if (width < 2 || width > 449 || heights?.Items == null || heights.Items.Count != width * width)
                    throw Invalid("JSON terrain sample count");
                Terrain = new TerrainProfile(Integer(terrain, "version", 1), width, Number(terrain, "spacing", 1), Number(terrain, "radius"),
                    Number(terrain, "originX"), Number(terrain, "originZ"), heights.Items.Select(n => JsonNumber(n)));
            }
            Text = text; RawByteCount = Utf8.GetByteCount(text);
            Pieces = rows.AsReadOnly(); UnknownPaths = unknown.AsReadOnly();
        }

        private static Node Field(Node node, string key) => node.Map.FirstOrDefault(p =>
            string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase)).Value is Node found && found.Kind != "null" ? found : null;
        private static string StringValue(Node node, string key)
        {
            Node value = Field(node, key);
            if (value == null) return null;
            if (value.Kind != "string") throw Invalid("JSON expected string: " + key);
            return value.Value;
        }
        private static float JsonNumber(Node value)
        {
            if ((value.Kind != "number" && value.Kind != "string") ||
                !float.TryParse(value.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result) || !FiniteFloat(result))
                throw Invalid("JSON expected finite float");
            // .NET Framework TryParse drops the sign of zero.
            return result == 0 && value.Value.TrimStart().StartsWith("-", StringComparison.Ordinal)
                ? BitConverter.ToSingle(BitConverter.GetBytes(int.MinValue), 0) : result;
        }
        private static float Number(Node node, string key, float fallback = 0) => Field(node, key) is Node n ? JsonNumber(n) : fallback;
        private static int Integer(Node node, string key, int fallback = 0)
        {
            Node n = Field(node, key);
            if (n == null) return fallback;
            if ((n.Kind != "number" && n.Kind != "string") ||
                !int.TryParse(n.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int result)) throw Invalid("JSON expected integer: " + key);
            return result;
        }
        private static void CheckJsonVersion(int version, IEnumerable<Piece> rows)
        {
            if (rows.Any(p => version < 2 && p.DisplayItem != null || version < 3 && p.SignText != null || version < 4 && p.Scale.HasValue))
                throw Invalid("JSON version would discard fields");
        }

        // Emit only known producer fields; immutable source evidence retains unrepresented extensions.
        public static string WriteJson(int version, string name, IEnumerable<Piece> pieces, TerrainProfile terrain = null)
        {
            Version(version); ValidateName(name);
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            Piece[] rows = pieces.Take(BlueprintEditorDocument.MaximumParts + 1).ToArray();
            if (rows.Length < 1 || rows.Length > BlueprintEditorDocument.MaximumParts || rows.Any(p => p == null)) throw Invalid("JSON output piece count");
            CheckJsonVersion(version, rows);
            using (var output = new BoundedOutput(BlueprintSourceDocument.MaximumBytes))
            {
                using (var writer = new StreamWriter(output, Utf8, 1024, true))
                {
                    string Quote(string value)
                    {
                        using (var bytes = new MemoryStream())
                        { new DataContractJsonSerializer(typeof(string)).WriteObject(bytes, value); return Utf8.GetString(bytes.ToArray()); }
                    }
                    string F(double value)
                    {
                        float f = (float)value;
                        return f == 0 && float.IsNegativeInfinity(1 / f) ? "-0.0" : f.ToString("R", CultureInfo.InvariantCulture);
                    }
                    void Vector(Point3 p) => writer.Write("{\"x\":" + F(p.X) + ",\"y\":" + F(p.Y) + ",\"z\":" + F(p.Z) + "}");
                    writer.Write("{\"formatVersion\":" + version + ",\"name\":" + Quote(name) + ",\"pieces\":[");
                    for (int i = 0; i < rows.Length; ++i)
                    {
                        Piece p = rows[i]; if (i != 0) writer.Write(',');
                        writer.Write("{\"prefab\":" + Quote(p.Prefab) + ",\"x\":" + F(p.Position.X) + ",\"y\":" + F(p.Position.Y) + ",\"z\":" + F(p.Position.Z) +
                            ",\"qx\":" + F(p.Rotation.X) + ",\"qy\":" + F(p.Rotation.Y) + ",\"qz\":" + F(p.Rotation.Z) + ",\"qw\":" + F(p.Rotation.W));
                        if (p.SignText != null) writer.Write(",\"signText\":" + Quote(p.SignText));
                        if (p.DisplayItem is Attachment item) writer.Write(",\"attachment\":{\"prefab\":" + Quote(item.Prefab) + ",\"variant\":" + item.Variant + ",\"orientation\":" + item.Orientation + "}");
                        if (p.Scale.HasValue) { writer.Write(",\"scale\":"); Vector(p.Scale.Value); }
                        writer.Write('}');
                    }
                    writer.Write(']');
                    if (terrain != null)
                    {
                        writer.Write(",\"terrain\":{\"version\":" + terrain.Version + ",\"width\":" + terrain.Width + ",\"spacing\":" + F(terrain.Spacing) +
                            ",\"radius\":" + F(terrain.Radius) + ",\"originX\":" + F(terrain.OriginX) + ",\"originZ\":" + F(terrain.OriginZ) + ",\"heights\":[");
                        for (int i = 0; i < terrain.Heights.Count; ++i) { if (i != 0) writer.Write(','); writer.Write(F(terrain.Heights[i])); }
                        writer.Write("]}");
                    }
                    writer.Write('}');
                }
                return Utf8.GetString(output.ToArray());
            }
        }
    }
}
