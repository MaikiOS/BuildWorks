using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    public sealed partial class KgBlueprint
    {
        public const string BinaryFormat = "kg-oprint-base64-v1";
        public const int MaximumBinaryTextLength = ((BlueprintSourceDocument.MaximumBytes + 2) / 3) * 4;
        public bool IsBinary { get; }
        public int BinaryByteCount { get; }

        private KgBlueprint(byte[] bytes)
        {
            IsBinary = true;
            BinaryByteCount = bytes.Length;
            Text = Convert.ToBase64String(bytes);
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
            {
                Name = BinaryText(reader);
                if (Name.Length == 0) Name = "Unnamed";
                Author = NullEmpty(BinaryText(reader)); Description = NullEmpty(BinaryText(reader));
                BoxRotation = BinaryVector(reader);
                int count = reader.ReadInt32();
                if (count < 1 || count > BlueprintEditorDocument.MaximumParts || count > (stream.Length - stream.Position) / 27)
                    throw Invalid("KG binary object count");
                var objects = new List<Piece>(count);
                for (int i = 0; i < count; ++i)
                {
                    bool isString = BinaryBool(reader);
                    string id = isString ? BinaryText(reader) : reader.ReadInt32().ToString(CultureInfo.InvariantCulture);
                    Point3 position = BinaryVector(reader), rotation = BinaryVector(reader);
                    string state = BinaryBool(reader) ? BinaryText(reader) : null;
                    objects.Add(new Piece(i + 1, id, position, rotation, state, null, isString));
                }
                Objects = objects.AsReadOnly();
                int previewCount = reader.ReadInt32();
                if (previewCount < 0 || previewCount > 32 || previewCount > stream.Length - stream.Position)
                    throw Invalid("KG binary preview count");
                var previews = new List<string>(previewCount);
                for (int i = 0; i < previewCount; ++i)
                {
                    string image = BinaryText(reader); Image(image); previews.Add(image);
                }
                Previews = previews.AsReadOnly(); HasPreviewList = true;
                Icon = BinaryText(reader);
                if (Icon.Length != 0) Image(Icon);
                if (stream.Position != stream.Length) throw Invalid("Trailing KG binary data");
            }
            UnknownPaths = unknown.AsReadOnly();
        }

        public static KgBlueprint ParseBinary(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0 || bytes.Length > BlueprintSourceDocument.MaximumBytes)
                throw Invalid("KG binary source is too large or empty");
            try { return new KgBlueprint((byte[])bytes.Clone()); }
            catch (Exception error) when (error is EndOfStreamException || error is DecoderFallbackException || error is ArgumentException)
            { throw new FormatException("Invalid KG binary source.", error); }
        }

        public static KgBlueprint ParseBinaryText(string text)
        {
            if (text == null || text.Length == 0 || text.Length > MaximumBinaryTextLength || text.Length % 4 != 0)
                throw Invalid("Invalid KG binary source envelope");
            // Convert permits whitespace; source identity requires one canonical encoding.
            byte[] bytes = Convert.FromBase64String(text);
            if (bytes.Length > BlueprintSourceDocument.MaximumBytes || Convert.ToBase64String(bytes) != text)
                throw Invalid("Noncanonical KG binary source envelope");
            return ParseBinary(bytes);
        }

        public byte[] BinaryBytes() => IsBinary ? Convert.FromBase64String(Text) :
            throw new InvalidOperationException("This source is KG YAML.");

        public byte[] WriteBinaryObjects(IEnumerable<Piece> objects, string name = null)
        {
            if (!IsBinary) throw new InvalidOperationException("Binary output requires a binary source profile.");
            if (objects == null) throw new ArgumentNullException(nameof(objects));
            if (name != null && name.Length == 0) throw new ArgumentException("Invalid KG name.");
            var rows = objects.Take(BlueprintEditorDocument.MaximumParts + 1).ToArray();
            if (rows.Length == 0 || rows.Length > BlueprintEditorDocument.MaximumParts || rows.Any(p => p == null || p.Record > Objects.Count || p.Prefab != null))
                throw new ArgumentException("Invalid KG output records.");
            using (var stream = new BinaryOutput())
            using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
            {
                writer.Write(name ?? Name); writer.Write(Author ?? ""); writer.Write(Description ?? "");
                WriteVector(writer, BoxRotation); writer.Write(rows.Length);
                foreach (Piece piece in rows)
                {
                    writer.Write(!piece.PrefabHash.HasValue);
                    if (piece.PrefabHash.HasValue) writer.Write(piece.PrefabHash.Value); else writer.Write(piece.Id);
                    WriteVector(writer, piece.Position); WriteVector(writer, piece.Euler);
                    writer.Write(piece.ZdoData != null);
                    if (piece.ZdoData != null) writer.Write(piece.ZdoData);
                }
                writer.Write(Previews.Count);
                foreach (string image in Previews) writer.Write(image);
                writer.Write(Icon ?? ""); writer.Flush();
                return stream.ToArray();
            }
        }

        private static string NullEmpty(string value) => value.Length == 0 ? null : value;
        private static bool BinaryBool(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw Invalid("Invalid KG binary boolean");
            return value != 0;
        }
        private static Point3 BinaryVector(BinaryReader reader)
        {
            var result = new Point3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
            ValidateVector(result); return result;
        }
        private static void WriteVector(BinaryWriter writer, Point3 value)
        { writer.Write((float)value.X); writer.Write((float)value.Y); writer.Write((float)value.Z); }
        private static string BinaryText(BinaryReader reader)
        {
            uint length = 0; int shift = 0;
            while (true)
            {
                byte current = reader.ReadByte();
                if (shift == 28 && current > 7) throw Invalid("Invalid KG binary string length");
                length |= (uint)(current & 127) << shift;
                if ((current & 128) == 0)
                {
                    if (shift != 0 && current == 0) throw Invalid("Noncanonical KG binary string length");
                    break;
                }
                shift += 7;
            }
            if (length > BlueprintSourceDocument.MaximumBytes || length > reader.BaseStream.Length - reader.BaseStream.Position)
                throw Invalid("KG binary string exceeds remaining source");
            return new UTF8Encoding(false, true).GetString(reader.ReadBytes((int)length));
        }
        private sealed class BinaryOutput : MemoryStream
        {
            private void Guard(int count)
            { if (count < 0 || Length + count > BlueprintSourceDocument.MaximumBytes) throw Invalid("KG binary output is too large"); }
            public override void Write(byte[] buffer, int offset, int count) { Guard(count); base.Write(buffer, offset, count); }
            public override void WriteByte(byte value) { Guard(1); base.WriteByte(value); }
        }
    }
}
