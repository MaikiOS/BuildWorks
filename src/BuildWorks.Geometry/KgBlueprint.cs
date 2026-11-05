using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace OstrixMods.BuildWorks.Geometry
{
    // Inert KG 1.7.1 YAML data. No native deserializer, prefab construction or ZDO writes.
    public sealed partial class KgBlueprint
    {
        public const string Format = "kg-yaml-v1";
        public static bool IsFormat(string format) => format == Format || format == BinaryFormat;
        private const int MaximumYamlNodes = 1000000, MaximumYamlDepth = 32;
        private readonly Node root;
        private readonly List<string> unknown = new List<string>();
        private readonly List<Node> objectNodes;
        private static readonly Lazy<CultureInfo[]> NumberCultures = new Lazy<CultureInfo[]>(() =>
            CultureInfo.GetCultures(CultureTypes.SpecificCultures).GroupBy(c => c.NumberFormat.NumberDecimalSeparator + "\n" +
                c.NumberFormat.NegativeSign + "\n" + c.NumberFormat.PositiveSign).Select(g => g.First()).ToArray());
        public string Text { get; }
        public string Name { get; }
        public string Author { get; }
        public string Description { get; }
        public string Icon { get; }
        public Point3 BoxRotation { get; }
        public IReadOnlyList<string> Previews { get; }
        public bool HasPreviewList { get; }
        public IReadOnlyList<Piece> Objects { get; }
        public IReadOnlyList<string> UnknownPaths { get; }

        public sealed class Piece
        {
            public Piece(int record, string id, Point3 position, Point3 euler, string zdoData = null, string prefab = null)
                : this(record, id, position, euler, zdoData, prefab, null) { }

            public Piece(int record, string id, Point3 position, Point3 euler, string zdoData, string prefab, bool? isStringId)
            {
                if (record < 0 || string.IsNullOrWhiteSpace(id) || id.Length > 256 || id.Any(char.IsControl))
                    throw new ArgumentException("Invalid KG object identifier.");
                ValidateVector(position); ValidateVector(euler);
                if (prefab != null && (prefab.Length > 256 || prefab.Any(char.IsControl)))
                    throw new ArgumentException("Invalid KG descriptive prefab.");
                if (zdoData != null && !BlueprintForeignZdo.TryReadKg(zdoData, out _))
                    throw new ArgumentException("Invalid KG ZDO payload.");
                Record = record; Id = id; Position = position; Euler = euler; ZdoData = zdoData; Prefab = prefab;
                // Binary IsString is authoritative even when its text looks like an integer.
                if (isStringId != true && int.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out int hash)) PrefabHash = hash;
                else if (isStringId == false) throw new ArgumentException("Invalid KG integer identifier.");
            }
            public int Record { get; }
            public string Id { get; }
            public int? PrefabHash { get; }
            public string PrefabName => PrefabHash.HasValue ? null : Id;
            public string ReferenceName => PrefabName ?? "kg_hash_" + PrefabHash.Value.ToString(CultureInfo.InvariantCulture);
            public bool MatchesPrefab(string name) => name == ReferenceName || PrefabHash.HasValue &&
                !string.IsNullOrEmpty(name) && StablePrefabHash(name) == PrefabHash.Value;
            // Public KG Prefab is retained metadata; KG resolves placement through Id only.
            public string Prefab { get; }
            public Point3 Position { get; }
            public Point3 Euler { get; }
            public string ZdoData { get; }
        }

        // Verified Valheim StringExtensionMethods hash; never the randomized CLR string hash.
        internal static int StablePrefabHash(string text)
        {
            unchecked
            {
                int first=5381, second=5381;
                for (int i=0; i<text.Length && text[i]!=0; i+=2)
                {
                    first=first*33^text[i];
                    if (i+1>=text.Length || text[i+1]==0) break;
                    second=second*33^text[i+1];
                }
                return first+second*1566083941;
            }
        }

        private KgBlueprint(string text, Node node)
        {
            Text = text; root = node;
            var map = Mapping(root, "/");
            Unknown(map, "", "Name", "Author", "Description", "Icon", "BoxRotation", "Objects", "Previews");
            Name = OptionalText(map, "Name");
            if (string.IsNullOrEmpty(Name)) throw Invalid("/Name: required");
            Author = OptionalText(map, "Author"); Description = OptionalText(map, "Description");
            Icon = OptionalText(map, "Icon"); if (Icon != null) Image(Icon);
            BoxRotation = Vector(Get(map, "BoxRotation"), "/BoxRotation");
            var previews = new List<string>();
            Node images = Get(map, "Previews");
            HasPreviewList = images != null && !IsNull(images);
            if (HasPreviewList)
            {
                var entries = Sequence(images, "/Previews");
                if (entries.Count > 32) throw Invalid("/Previews: too many images");
                foreach (var image in entries)
                {
                    string value = ScalarText(image, "/Previews"); Image(value); previews.Add(value);
                }
            }
            Previews = previews.AsReadOnly();
            objectNodes = Sequence(Get(map, "Objects"), "/Objects");
            if (objectNodes.Count == 0 || objectNodes.Count > BlueprintEditorDocument.MaximumParts)
                throw Invalid("/Objects: invalid count");
            var objects = new List<Piece>(objectNodes.Count);
            for (int i = 0; i < objectNodes.Count; ++i)
            {
                string path = "/Objects/" + i;
                var row = Mapping(objectNodes[i], path);
                Unknown(row, path, "Id", "RelativePosition", "Rotation", "ZDOData", "Prefab");
                string id = Get(row, "Id") is Node idNode ? ScalarText(idNode, path + "/Id") : "0";
                try { objects.Add(new Piece(i + 1, id, Vector(Get(row, "RelativePosition"), path + "/RelativePosition"),
                    Vector(Get(row, "Rotation"), path + "/Rotation"), OptionalText(row, "ZDOData"), OptionalText(row, "Prefab"))); }
                catch (ArgumentException error) { throw Invalid(path + ": " + error.Message); }
            }
            Objects = objects.AsReadOnly(); UnknownPaths = unknown.AsReadOnly();
        }

        public static KgBlueprint Parse(string text)
        {
            if (text == null || text.Length > BlueprintSourceDocument.MaximumBytes ||
                new UTF8Encoding(false, true).GetByteCount(text) > BlueprintSourceDocument.MaximumBytes)
                throw Invalid("KG source is too large");
            try
            {
                var parser = new Parser(new StringReader(text));
                parser.Consume<StreamStart>(); parser.Consume<DocumentStart>();
                int nodes = 0;
                Node root = Read(parser, 0, ref nodes);
                parser.Consume<DocumentEnd>(); parser.Consume<StreamEnd>();
                return new KgBlueprint(text, root);
            }
            catch (YamlException error) { throw new FormatException("Invalid KG YAML.", error); }
        }

        // Re-emission changes known object fields only; root metadata and unknown nodes retain YAML types/styles.
        // The immutable Text remains the byte-preserving original in native/portable source evidence.
        public string WriteObjects(IEnumerable<Piece> objects, string name = null)
        {
            if (IsBinary) throw new InvalidOperationException("Binary KG must use the binary writer.");
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (name != null && string.IsNullOrEmpty(name)) throw new ArgumentException("Invalid KG name.");
            var rows = new List<Node>();
            foreach (Piece piece in objects)
            {
                if (piece == null || piece.Record > Objects.Count || rows.Count >= BlueprintEditorDocument.MaximumParts)
                    throw new ArgumentException("Invalid KG output record.");
                if (!piece.PrefabHash.HasValue && new[] { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture }.Concat(NumberCultures.Value)
                    .Any(culture => int.TryParse(piece.Id, NumberStyles.Integer, culture, out _)))
                    throw new ArgumentException("KG YAML cannot preserve this binary string identifier.");
                var fields = piece.Record == 0 ? new List<KeyValuePair<string, Node>>() :
                    objectNodes[piece.Record - 1].Fields.Where(p => !new[] { "Id", "RelativePosition", "Rotation", "ZDOData", "Prefab" }.Contains(p.Key)).ToList();
                fields.Add(Field("Id", Value(piece.PrefabHash?.ToString(CultureInfo.InvariantCulture) ?? piece.Id)));
                Node original = piece.Record == 0 ? null : objectNodes[piece.Record - 1];
                fields.Add(Field("RelativePosition", VectorNode(piece.Position, Child(original, "RelativePosition"))));
                fields.Add(Field("Rotation", VectorNode(piece.Euler, Child(original, "Rotation"))));
                if (piece.ZdoData != null) fields.Add(Field("ZDOData", Value(piece.ZdoData)));
                if (piece.Prefab != null) fields.Add(Field("Prefab", Value(piece.Prefab)));
                rows.Add(new Node { Fields = fields });
            }
            if (rows.Count == 0) throw new ArgumentException("Empty KG output.");
            var output = new Node { Fields = root.Fields.Select(p => p.Key == "Objects" ? Field(p.Key, new Node { Items = rows }) :
                p.Key == "Name" && name != null ? Field(p.Key, Value(name)) :
                p.Key == "BoxRotation" ? Field(p.Key, VectorNode(BoxRotation, p.Value)) : p).ToList() };
            using (var writer = new BoundedWriter())
            {
                var emitter = new Emitter(writer); emitter.Emit(new StreamStart()); emitter.Emit(new DocumentStart());
                int nodes = 0;
                Emit(emitter, output, 0, ref nodes); emitter.Emit(new DocumentEnd(true)); emitter.Emit(new StreamEnd());
                string result = writer.ToString();
                if (Encoding.UTF8.GetByteCount(result) > BlueprintSourceDocument.MaximumBytes)
                    throw new ArgumentException("KG output is too large.");
                return result;
            }
        }

        private Point3 Vector(Node node, string path)
        {
            if (node == null) return new Point3(0, 0, 0);
            // The native Vector3 converter permits duplicate components; its last value wins.
            var map = Mapping(node, path, allowDuplicates: true); Unknown(map, path, "x", "y", "z");
            return new Point3(Number(Get(map, "x"), path), Number(Get(map, "y"), path), Number(Get(map, "z"), path));
        }
        private static float Number(Node node, string path)
        {
            if (node == null) return 0;
            string scalar = ScalarText(node, path);
            // KG ToString uses the producer culture; its general-number format has no grouping separators.
            if (scalar.Length > 128 || scalar.IndexOf(',') >= 0 && scalar.IndexOf('.') >= 0)
                throw Invalid(path + ": invalid finite float");
            // NumberStyles.Float excludes grouping: 1,234 cannot silently become 1234 on another machine.
            foreach (var culture in new[] { CultureInfo.InvariantCulture, CultureInfo.CurrentCulture }.Concat(NumberCultures.Value))
                if (float.TryParse(scalar, NumberStyles.Float, culture, out float value))
                {
                    if (float.IsNaN(value) || float.IsInfinity(value)) throw Invalid(path + ": invalid finite float");
                    return value;
                }
            throw Invalid(path + ": invalid finite float");
        }
        private static void ValidateVector(Point3 value)
        {
            foreach (double n in new[] { value.X, value.Y, value.Z })
                if (!GeometryMath.IsFinite(n) || Math.Abs(n) > float.MaxValue) throw new ArgumentException("Invalid KG vector.");
        }
        private static void Image(string value)
        {
            if (value == null || value.Length > ((4 * 1024 * 1024 + 2) / 3) * 4) throw Invalid("KG image is too large");
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value); }
            catch (FormatException error) { throw new FormatException("Invalid KG image Base64.", error); }
            if (Convert.ToBase64String(bytes) != value || bytes.Length < 33 ||
                !bytes.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }) ||
                bytes[8] != 0 || bytes[9] != 0 || bytes[10] != 0 || bytes[11] != 13 ||
                Encoding.ASCII.GetString(bytes, 12, 4) != "IHDR") throw Invalid("Invalid KG PNG header");
            long Dimension(int start) => (long)bytes[start] << 24 | (long)bytes[start + 1] << 16 | (long)bytes[start + 2] << 8 | bytes[start + 3];
            long width = Dimension(16), height = Dimension(20);
            if (width < 1 || height < 1 || width > 8192 || height > 8192 || width * height > 16777216)
                throw Invalid("KG image dimensions are too large");
        }
        private static Node Get(Dictionary<string, Node> map, string key) => map.TryGetValue(key, out Node node) ? node : null;
        private static bool IsNull(Node n) => n.Scalar != null && n.Scalar.Style == ScalarStyle.Plain &&
            (n.Scalar.Value == "" || n.Scalar.Value == "~" || string.Equals(n.Scalar.Value, "null", StringComparison.OrdinalIgnoreCase));
        private static string OptionalText(Dictionary<string, Node> map, string key) =>
            Get(map, key) is Node n && !IsNull(n) ? ScalarText(n, "/" + key) : null;
        private static string ScalarText(Node node, string path) => node?.Scalar?.Value ?? throw Invalid(path + ": expected scalar");
        private static List<Node> Sequence(Node node, string path) => node?.Items ?? throw Invalid(path + ": expected sequence");
        private static Dictionary<string, Node> Mapping(Node node, string path, bool allowDuplicates = false)
        {
            if (node?.Fields == null) throw Invalid(path + ": expected mapping");
            var result = new Dictionary<string, Node>(StringComparer.Ordinal);
            foreach (var pair in node.Fields)
            {
                if (!allowDuplicates && result.ContainsKey(pair.Key)) throw Invalid(path + ": duplicate field " + pair.Key);
                result[pair.Key] = pair.Value;
            }
            return result;
        }
        private void Unknown(Dictionary<string, Node> map, string path, params string[] known)
        {
            foreach (string key in map.Keys) if (!known.Contains(key)) unknown.Add(path + "/" + key);
        }
        private sealed class Node
        {
            internal Scalar Scalar;
            internal List<KeyValuePair<string, Node>> Fields;
            internal List<Node> Items;
        }
        private static Node Read(IParser parser, int depth, ref int nodes)
        {
            CheckNodeBudget(depth, ref nodes);
            if (parser.Current is NodeEvent node && (!node.Anchor.IsEmpty || !node.Tag.IsEmpty))
                throw Invalid("KG YAML anchors and explicit tags are unsupported");
            if (parser.Current is AnchorAlias) throw Invalid("KG YAML aliases are unsupported");
            if (parser.TryConsume<Scalar>(out var scalar)) return new Node { Scalar = scalar };
            if (parser.TryConsume<SequenceStart>(out _))
            {
                var result = new Node { Items = new List<Node>() };
                while (!parser.TryConsume<SequenceEnd>(out _)) result.Items.Add(Read(parser, depth + 1, ref nodes));
                return result;
            }
            parser.Consume<MappingStart>();
            var mapping = new Node { Fields = new List<KeyValuePair<string, Node>>() };
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                Node key = Read(parser, depth + 1, ref nodes);
                mapping.Fields.Add(Field(ScalarText(key, "mapping key"), Read(parser, depth + 1, ref nodes)));
            }
            return mapping;
        }
        private static void Emit(IEmitter emitter, Node node, int depth, ref int nodes)
        {
            CheckNodeBudget(depth, ref nodes);
            if (node.Scalar != null) emitter.Emit(node.Scalar);
            else if (node.Items != null)
            {
                emitter.Emit(new SequenceStart(null, null, true, SequenceStyle.Block));
                foreach (var item in node.Items) Emit(emitter, item, depth + 1, ref nodes);
                emitter.Emit(new SequenceEnd());
            }
            else
            {
                emitter.Emit(new MappingStart());
                foreach (var pair in node.Fields)
                {
                    CheckNodeBudget(depth + 1, ref nodes); // Mapping keys consume nodes in the reader too.
                    emitter.Emit(new Scalar(pair.Key)); Emit(emitter, pair.Value, depth + 1, ref nodes);
                }
                emitter.Emit(new MappingEnd());
            }
        }
        private static void CheckNodeBudget(int depth, ref int nodes)
        {
            if (depth > MaximumYamlDepth || ++nodes > MaximumYamlNodes) throw Invalid("KG YAML structure limit");
        }
        private static KeyValuePair<string, Node> Field(string key, Node value) => new KeyValuePair<string, Node>(key, value);
        private static Node Value(string value) => new Node { Scalar = new Scalar(null, null, value, ScalarStyle.DoubleQuoted, true, false) };
        private static Node Child(Node node, string key) => node?.Fields?.LastOrDefault(p => p.Key == key).Value;
        private static Node VectorNode(Point3 p, Node original = null) => new Node { Fields =
            (original?.Fields?.Where(f => f.Key != "x" && f.Key != "y" && f.Key != "z") ?? Enumerable.Empty<KeyValuePair<string, Node>>())
                .Concat(new[] { Field("x", NumberNode(p.X)), Field("y", NumberNode(p.Y)), Field("z", NumberNode(p.Z)) }).ToList() };
        // Original KG parses floats with the reader's current culture; emit that culture too.
        private static Node NumberNode(double n) => new Node { Scalar = new Scalar(((float)n).ToString("R", CultureInfo.CurrentCulture)) };
        private static FormatException Invalid(string message) => new FormatException(message);
        private sealed class BoundedWriter : StringWriter
        {
            private void Guard(int length) { if ((long)GetStringBuilder().Length + length > BlueprintSourceDocument.MaximumBytes) throw Invalid("KG output is too large"); }
            public override void Write(char value) { Guard(1); base.Write(value); }
            public override void Write(string value) { Guard(value?.Length ?? 0); base.Write(value); }
            public override void Write(char[] buffer, int index, int count) { Guard(count); base.Write(buffer, index, count); }
        }
    }
}
