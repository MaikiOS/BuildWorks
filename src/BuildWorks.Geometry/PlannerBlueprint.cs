using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Node = OstrixMods.BuildWorks.Geometry.OpenKeepBlueprint.Node;

namespace OstrixMods.BuildWorks.Geometry
{
    // Planner coordinates describe a browser scene, never permission to alter a world.
    public sealed class PlannerBlueprint
    {
        public const string Format = "valheim-base-builder-json-v3";
        private readonly Node root;
        private readonly List<string> unknown = new List<string>();
        private readonly Dictionary<int, Node> records = new Dictionary<int, Node>();
        private readonly Dictionary<string, Node> layerRecords = new Dictionary<string, Node>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Mapping> catalogue = ReadCatalogue();
        private static readonly Dictionary<string, Mapping> byPrefab = catalogue.Values.ToDictionary(m => m.Prefab, StringComparer.Ordinal);
        public string Text { get; }
        public string Title { get; }
        public IReadOnlyList<Piece> Pieces { get; }
        public IReadOnlyList<Layer> Layers { get; }
        public IReadOnlyList<string> UnknownPaths => unknown.AsReadOnly();

        public static bool IsJsonBlueprint(string text)
        {
            Node r = OpenKeepBlueprint.ReadJson(text);
            // Recognize the declared family before validating its values. A broken planner
            // must not fall through to another reader's optional coordinate defaults.
            Node rows = Get(r, "pieces");
            int headers = (Get(r, "version") != null ? 1 : 0) + (Get(r, "title") != null ? 1 : 0) +
                (Get(r, "layers") != null ? 1 : 0);
            return rows != null && (headers >= 2 || rows.Items?.Any(row => Get(row, "pieceId") != null) == true);
        }

        public static PlannerBlueprint Parse(string text) => new PlannerBlueprint(text);

        public static string WriteNew(IEnumerable<(string Prefab, Point3 Position, Rotation3 Rotation, int Record, string Layer)> pieces,
            string title, IEnumerable<Layer> layers, string identity)
        {
            // A private empty template supplies deterministic UID namespacing; final output must contain pieces.
            var template = new PlannerBlueprint("{\"version\":3,\"title\":" + Quote(CheckedString(identity, 256, false)) +
                ",\"layers\":[{\"id\":\"main\",\"name\":\"Main\"}],\"pieces\":[]}", true);
            return template.WritePieces(pieces, CheckedString(title, 4096, true), layers);
        }

        private PlannerBlueprint(string text, bool emptyTemplate = false)
        {
            Text = text; root = OpenKeepBlueprint.ReadJson(text);
            Fields(root, "", "version", "title", "layers", "pieces");
            if (Number(Get(root, "version")) != 3) throw Invalid("Unsupported version");
            Title = String(Get(root, "title"), 4096, true);
            Node layerArray = Get(root, "layers");
            if (layerArray?.Items == null || layerArray.Items.Count < 1 || layerArray.Items.Count > BlueprintEditorDocument.MaximumParts)
                throw Invalid("Layer count");
            var layers = new List<Layer>();
            foreach (Node row in layerArray.Items)
            {
                Fields(row, "/layers/" + layers.Count, "id", "name");
                var layer = new Layer(String(Get(row, "id")), String(Get(row, "name"), 4096, true));
                if (layerRecords.ContainsKey(layer.Id)) throw Invalid("Duplicate layer ID");
                layerRecords.Add(layer.Id, row); layers.Add(layer);
            }
            Layers = layers.AsReadOnly();
            Node array = Get(root, "pieces");
            if (array?.Items == null || array.Items.Count < (emptyTemplate ? 0 : 1) || array.Items.Count > BlueprintEditorDocument.MaximumParts)
                throw Invalid("Piece count");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var pieces = new List<Piece>();
            foreach (Node row in array.Items)
            {
                int record = pieces.Count + 1;
                Fields(row, "/pieces/" + (record - 1), "uid", "pieceId", "material", "position", "rotation", "rotationStep", "layer");
                string uid = String(Get(row, "uid")), id = String(Get(row, "pieceId")), material = String(Get(row, "material"));
                if (!ids.Add(uid)) throw Invalid("Duplicate piece UID");
                if (!catalogue.TryGetValue(id + "\t" + material, out var map)) throw Invalid("Unknown piece/material: " + id + "/" + material);
                string layer = Get(row, "layer") == null ? "main" : String(Get(row, "layer"));
                if (!layerRecords.ContainsKey(layer)) throw Invalid("Dangling layer reference");
                Node pos = Get(row, "position");
                if (pos?.Items == null || pos.Items.Count != 3) throw Invalid("Position vector");
                var position = new Point3(Number(pos.Items[0]), Number(pos.Items[1]), Number(pos.Items[2]));
                double yaw = Number(Get(row, "rotation")), step = Number(Get(row, "rotationStep"));
                if (step != Math.Floor(step) || step < 0 || step > 15) throw Invalid("Rotation step");
                pieces.Add(new Piece(record, uid, layer, map, position, yaw, (int)step)); records.Add(record, row);
            }
            Pieces = pieces.AsReadOnly();
        }

        public sealed class Layer
        {
            public string Id { get; }
            public string Name { get; }
            public string SourceLayerId { get; }
            public Layer(string id, string name) : this(id, name, null) { }
            public Layer(string id, string name, string sourceLayerId)
            {
                Id = CheckedString(id, 256, false); Name = CheckedString(name, 4096, true);
                SourceLayerId = sourceLayerId == null ? null : CheckedString(sourceLayerId, 256, false);
            }
        }

        public sealed class Piece
        {
            public int Record { get; }
            public string Uid { get; }
            public string LayerId { get; }
            public string PieceId => map.Id;
            public string Material => map.Material;
            public string Prefab => map.Prefab;
            public Point3 PlannerPosition { get; }
            public double PlannerYaw { get; }
            public int RotationStep { get; }
            public Point3 Position => Reflect(PlannerPosition) + NativeYaw(PlannerYaw).Rotate(map.Origin);
            public Rotation3 Rotation => new Rotation3(0, Math.Sin((map.BaseYaw - PlannerYaw) / 2), 0, Math.Cos((map.BaseYaw - PlannerYaw) / 2));
            private readonly Mapping map;
            internal Piece(int record, string uid, string layer, Mapping mapping, Point3 position, double yaw, int step)
            { Record = record; Uid = uid; LayerId = layer; map = mapping; PlannerPosition = position; PlannerYaw = yaw; RotationStep = step; }
        }

        // Keep unknown root/layer/piece fields verbatim while changing only declared planner data.
        public string WritePieces(IEnumerable<(string Prefab, Point3 Position, Rotation3 Rotation, int Record, string Layer)> pieces,
            string title = null, IEnumerable<Layer> layers = null)
        {
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            Layer[] outputLayers = (layers ?? Layers).Take(BlueprintEditorDocument.MaximumParts + 1).ToArray();
            if (outputLayers.Any(l => l == null) || outputLayers.Length < 1 || outputLayers.Length > BlueprintEditorDocument.MaximumParts ||
                outputLayers.Select(l => l.Id).Distinct(StringComparer.Ordinal).Count() != outputLayers.Length)
                throw Invalid("Output layers");
            if (outputLayers.Any(l => l.SourceLayerId != null && !layerRecords.ContainsKey(l.SourceLayerId)))
                throw Invalid("Unknown output layer source");
            var layerIds = new HashSet<string>(outputLayers.Select(l => l.Id), StringComparer.Ordinal);
            var output = new StringBuilder("["); var used = new HashSet<string>(StringComparer.Ordinal); int count = 0;
            var reserved = new HashSet<string>(Pieces.Select(p => p.Uid), StringComparer.Ordinal);
            string sourceHash = null;
            string NewUid()
            {
                // Pair review revalidates the same snapshot; IDs must be reproducible and avoid retained UIDs.
                if (sourceHash == null) using (var hash = SHA256.Create())
                    sourceHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Text))).Replace("-", "").ToLowerInvariant();
                string value; int nonce = 0;
                do { value = "bw_" + sourceHash + "_" + count.ToString(CultureInfo.InvariantCulture) + "_" + (nonce++).ToString(CultureInfo.InvariantCulture); }
                while (reserved.Contains(value) || used.Contains(value));
                return value;
            }
            bool unchanged = (title == null || title == Title) && outputLayers.Length == Layers.Count &&
                outputLayers.Select((l, i) => l.Id == Layers[i].Id && l.Name == Layers[i].Name &&
                    (l.SourceLayerId == null || l.SourceLayerId == l.Id)).All(v => v);
            foreach (var row in pieces)
            {
                var p = row;
                p.Prefab = PlannerPrefabNames.Producer(p.Prefab);
                if (++count > BlueprintEditorDocument.MaximumParts) throw Invalid("Output piece count");
                if (p.Prefab == null || !byPrefab.TryGetValue(p.Prefab, out var m)) throw Invalid("Unknown output prefab");
                if (p.Record < 0 || p.Record > Pieces.Count || !layerIds.Contains(p.Layer)) throw Invalid("Output record/layer");
                Piece old = p.Record > 0 ? Pieces[p.Record - 1] : null;
                bool samePose = old != null && p.Prefab == old.Prefab &&
                    p.Position.X == old.Position.X && p.Position.Y == old.Position.Y && p.Position.Z == old.Position.Z &&
                    p.Rotation.X == old.Rotation.X && p.Rotation.Y == old.Rotation.Y && p.Rotation.Z == old.Rotation.Z && p.Rotation.W == old.Rotation.W;
                unchanged &= samePose && p.Record == count && p.Layer == old.LayerId;
                Rotation3 q = p.Rotation.Normalized();
                if (q.X != 0 || q.Z != 0) throw Invalid("Planner output cannot represent pitch or roll");
                double yaw = m.BaseYaw - 2 * Math.Atan2(q.Y, q.W);
                yaw = (yaw % (2 * Math.PI) + 2 * Math.PI) % (2 * Math.PI);
                Point3 pos = Reflect(p.Position - NativeYaw(yaw).Rotate(m.Origin));
                string uid = p.Record == 0 ? NewUid() : Pieces[p.Record - 1].Uid;
                if (!used.Add(uid)) { uid = NewUid(); used.Add(uid); }
                var fields = new Dictionary<string, string>();
                if (old == null || uid != old.Uid) fields["uid"] = Quote(uid);
                if (old == null || p.Prefab != old.Prefab) { fields["pieceId"] = Quote(m.Id); fields["material"] = Quote(m.Material); }
                if (old == null || p.Layer != old.LayerId) fields["layer"] = Quote(p.Layer);
                if (!samePose)
                {
                    fields["position"] = "[" + F(pos.X) + "," + F(pos.Y) + "," + F(pos.Z) + "]";
                    fields["rotation"] = F(yaw);
                    fields["rotationStep"] = ((int)Math.Round(yaw / (Math.PI / 8), MidpointRounding.AwayFromZero) % 16).ToString(CultureInfo.InvariantCulture);
                }
                if (count > 1) output.Append(',');
                output.Append(Patch(p.Record == 0 ? null : records[p.Record], fields));
                if (output.Length > BlueprintSourceDocument.MaximumBytes) throw Invalid("Output too large");
            }
            if (count == 0) throw Invalid("Empty output");
            if (unchanged && count == Pieces.Count) return Text;
            output.Append(']');
            var layerText = new StringBuilder("[");
            for (int i = 0; i < outputLayers.Length; ++i)
            {
                Layer l = outputLayers[i]; if (i != 0) layerText.Append(',');
                layerRecords.TryGetValue(l.SourceLayerId ?? l.Id, out Node row);
                var layerFields = new Dictionary<string, string>();
                if (row == null || Get(row, "id").Value != l.Id) layerFields["id"] = Quote(l.Id);
                if (row == null || Get(row, "name").Value != l.Name) layerFields["name"] = Quote(l.Name);
                layerText.Append(Patch(row, layerFields));
                if (layerText.Length > BlueprintSourceDocument.MaximumBytes) throw Invalid("Output layers too large");
            }
            layerText.Append(']');
            string result = Patch(root, new Dictionary<string, string> { ["pieces"] = output.ToString(), ["layers"] = layerText.ToString(),
                ["title"] = Quote(title == null ? Title : CheckedString(title, 4096, true)) });
            Parse(result); return result;
        }

        private string Patch(Node row, Dictionary<string, string> fields)
        {
            if (row == null) return "{" + string.Join(",", fields.Select(f => Quote(f.Key) + ":" + f.Value)) + "}";
            string text = Text.Substring(row.Start, row.Length);
            var replacements = new List<(int Start, int Length, string Text)>();
            var added = new List<string>();
            foreach (var field in fields)
                if (row.Map.TryGetValue(field.Key, out Node value)) replacements.Add((value.Start - row.Start, value.Length, field.Value));
                else added.Add(Quote(field.Key) + ":" + field.Value);
            if (added.Count > 0) replacements.Add((1, 0, string.Join(",", added) + (row.Map.Count > 0 ? "," : "")));
            foreach (var change in replacements.OrderByDescending(p => p.Start))
                text = text.Substring(0, change.Start) + change.Text + text.Substring(change.Start + change.Length);
            return text;
        }

        private void Fields(Node node, string path, params string[] known)
        {
            if (node?.Map == null) throw Invalid("Expected object: " + path);
            foreach (string key in node.Map.Keys)
                if (!known.Contains(key, StringComparer.Ordinal)) unknown.Add(path + "/" + key.Replace("~", "~0").Replace("/", "~1"));
        }
        private static Node Get(Node n, string key) => n?.Map != null && n.Map.TryGetValue(key, out Node value) ? value : null;
        private static string String(Node n, int limit = 256, bool empty = false) => n?.Kind == "string" ? CheckedString(n.Value, limit, empty) : throw Invalid("Expected string");
        private static string CheckedString(string value, int limit, bool empty)
        {
            if (value == null || value.Length > limit || (!empty && value.Length == 0) || value.Any(char.IsControl)) throw Invalid("Invalid string");
            return value;
        }
        private static double Number(Node n)
        {
            if (n?.Kind != "number" || !double.TryParse(n.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ||
                !GeometryMath.IsFinite(d) || Math.Abs(d) > float.MaxValue) throw Invalid("Expected finite number");
            return d;
        }
        private static string F(double d) => GeometryMath.IsFinite(d) && Math.Abs(d) <= float.MaxValue ? d.ToString("R", CultureInfo.InvariantCulture) : throw Invalid("Output number");
        private static string Quote(string s)
        { using (var stream = new MemoryStream()) { new DataContractJsonSerializer(typeof(string)).WriteObject(stream, s); return Encoding.UTF8.GetString(stream.ToArray()); } }
        private static Point3 Reflect(Point3 p) => new Point3(p.X, p.Y, -p.Z);
        private static Rotation3 NativeYaw(double yaw) => new Rotation3(0, -Math.Sin(yaw / 2), 0, Math.Cos(yaw / 2));
        private static FormatException Invalid(string message) => new FormatException("Valheim Base Builder: " + message);

        internal sealed class Mapping
        {
            internal string Id, Material, Prefab;
            internal Point3 Origin;
            internal double BaseYaw;
        }
        private static Dictionary<string, Mapping> ReadCatalogue()
        {
            var result = new Dictionary<string, Mapping>(StringComparer.Ordinal);
            using (var stream = typeof(PlannerBlueprint).Assembly.GetManifestResourceStream("BuildWorks.PlannerCatalogue.tsv"))
            using (var reader = new StreamReader(stream ?? throw new InvalidOperationException("Missing planner catalogue"), Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    string[] f = line.Split('\t');
                    if (f.Length != 7) throw new InvalidOperationException("Invalid planner catalogue row");
                    double N(int i) => double.Parse(f[i], CultureInfo.InvariantCulture);
                    result.Add(f[0] + "\t" + f[1], new Mapping { Id = f[0], Material = f[1], Prefab = f[2],
                        Origin = new Point3(N(3), N(4), N(5)), BaseYaw = N(6) * Math.PI / 180 });
                }
            }
            return result;
        }
    }
}
