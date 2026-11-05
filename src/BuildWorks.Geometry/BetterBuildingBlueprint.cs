using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // BBPF data only: terrain and displayed items never authorize world or inventory changes.
    public sealed partial class BetterBuildingBlueprint
    {
        public const string BinaryFormat = "betterbuilding-bbp-base64-v1";
        public const int MaximumBinaryTextLength = ((BlueprintSourceDocument.MaximumBytes + 2) / 3) * 4;
        public const int MaximumRawBytes = BlueprintSourceDocument.MaximumBytes;
        private const int Magic = 1179664962, HeaderBytes = 48;
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        public string Text { get; }
        public string Name { get; }
        public int PayloadVersion { get; }
        public int BinaryByteCount { get; }
        public int RawByteCount { get; }
        public IReadOnlyList<Piece> Pieces { get; }
        public TerrainProfile Terrain { get; }

        private BetterBuildingBlueprint(byte[] bytes)
        {
            BinaryByteCount = bytes.Length; Text = Convert.ToBase64String(bytes);
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream, Utf8))
            {
                if (reader.ReadInt32() != Magic || reader.ReadInt32() != 1) throw Invalid("BBPF container version/magic");
                RawByteCount = reader.ReadInt32(); int packedLength = reader.ReadInt32();
                if (RawByteCount < 1 || RawByteCount > MaximumRawBytes || packedLength < 1 || packedLength != bytes.Length - HeaderBytes)
                    throw Invalid("BBPF size exceeds the BuildWorks limit");
                byte[] expected = reader.ReadBytes(32), packed = reader.ReadBytes(packedLength);
                using (var hash = SHA256.Create())
                    if (!expected.SequenceEqual(hash.ComputeHash(packed))) throw Invalid("BBPF checksum");
                using (var raw = new BoundedOutput(RawByteCount))
                {
                    using (var input = new MemoryStream(packed, false))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        var buffer = new byte[8192]; int count;
                        while ((count = deflate.Read(buffer, 0, buffer.Length)) != 0) raw.Write(buffer, 0, count);
                    }
                    if (raw.Length != RawByteCount) throw Invalid("Incomplete BBPF payload");
                    raw.Position = 0;
                    using (var data = new BinaryReader(raw, Utf8, true))
                    {
                        PayloadVersion = data.ReadInt32(); Version(PayloadVersion);
                        Name = ReadText(data, 80); ValidateName(Name);
                        int tableCount = Count(data, 1, BlueprintEditorDocument.MaximumParts);
                        if (tableCount > Remaining(data)) throw Invalid("BBPF prefab table exceeds payload");
                        var table = new string[tableCount];
                        for (int i = 0; i < tableCount; ++i) { table[i] = ReadText(data, 160); ValidatePrefab(table[i]); }
                        int partCount = Count(data, tableCount, BlueprintEditorDocument.MaximumParts);
                        Require(data, partCount * 32L + 1);
                        var rows = new (string Prefab, Point3 Position, Rotation3 Rotation)[partCount];
                        for (int i = 0; i < partCount; ++i)
                        {
                            int index = data.ReadInt32();
                            if (index < 0 || index >= table.Length) throw Invalid("BBPF prefab index");
                            rows[i] = (table[index], ReadVector(data), new Rotation3(data.ReadSingle(), data.ReadSingle(), data.ReadSingle(), data.ReadSingle()));
                        }
                        byte terrainFlag = data.ReadByte();
                        if (terrainFlag > 1) throw Invalid("BBPF terrain flag");
                        if (terrainFlag == 1)
                        {
                            int version = data.ReadInt32(), width = data.ReadInt32();
                            float spacing = data.ReadSingle(), radius = data.ReadSingle(), ox = data.ReadSingle(), oz = data.ReadSingle();
                            if (width < 2 || width > 449) throw Invalid("BBPF terrain width");
                            Require(data, width * (long)width * 4);
                            var heights = new float[width * width];
                            for (int i = 0; i < heights.Length; ++i) heights[i] = data.ReadSingle();
                            Terrain = new TerrainProfile(version, width, spacing, radius, ox, oz, heights);
                        }
                        var attachments = new Attachment[partCount]; var signs = new string[partCount]; var scales = new Point3?[partCount];
                        if (PayloadVersion >= 2)
                        {
                            int count = Count(data, 0, partCount); Require(data, count * 14L);
                            for (int i = 0; i < count; ++i)
                            {
                                int index = Index(data, partCount);
                                if (attachments[index] != null) throw Invalid("Duplicate BBPF attachment index");
                                attachments[index] = new Attachment(ReadText(data, 160), data.ReadInt32(), data.ReadInt32());
                            }
                        }
                        if (PayloadVersion >= 3)
                        {
                            int count = Count(data, 0, partCount); Require(data, count * 5L);
                            for (int i = 0; i < count; ++i)
                            {
                                int index = Index(data, partCount);
                                if (signs[index] != null) throw Invalid("Duplicate BBPF sign index");
                                signs[index] = ReadText(data, 4096);
                            }
                        }
                        if (PayloadVersion >= 4)
                        {
                            int count = Count(data, 0, partCount); Require(data, count * 16L);
                            for (int i = 0; i < count; ++i)
                            {
                                int index = Index(data, partCount);
                                if (scales[index].HasValue) throw Invalid("Duplicate BBPF scale index");
                                scales[index] = ReadVector(data);
                            }
                        }
                        if (raw.Position != raw.Length) throw Invalid("Trailing BBPF payload data");
                        Pieces = Array.AsReadOnly(rows.Select((row, i) => new Piece(i + 1, row.Prefab, row.Position, row.Rotation, signs[i], attachments[i], scales[i])).ToArray());
                    }
                }
            }
        }

        public static BetterBuildingBlueprint ParseBinary(byte[] bytes)
        {
            if (bytes == null || bytes.Length < HeaderBytes || bytes.Length > BlueprintSourceDocument.MaximumBytes)
                throw Invalid("BBPF file exceeds the BuildWorks limit or is incomplete");
            try { return new BetterBuildingBlueprint((byte[])bytes.Clone()); }
            catch (Exception error) when (error is EndOfStreamException || error is DecoderFallbackException || error is ArgumentException || error is InvalidDataException || error is OverflowException)
            { throw new FormatException("Invalid or oversized BBPF source.", error); }
        }

        public static BetterBuildingBlueprint ParseBinaryText(string text)
        {
            if (text == null || text.Length == 0 || text.Length > MaximumBinaryTextLength || text.Length % 4 != 0)
                throw Invalid("BBPF source envelope");
            byte[] bytes = Convert.FromBase64String(text);
            if (Convert.ToBase64String(bytes) != text) throw Invalid("Noncanonical BBPF source envelope");
            return ParseBinary(bytes);
        }
        public byte[] BinaryBytes() => Convert.FromBase64String(Text);

        public static byte[] WriteBinary(int payloadVersion, string name, IEnumerable<Piece> pieces, TerrainProfile terrain = null)
        {
            Version(payloadVersion); ValidateName(name);
            if (pieces == null) throw new ArgumentNullException(nameof(pieces));
            var rows = pieces.Take(BlueprintEditorDocument.MaximumParts + 1).ToArray();
            if (rows.Length < 1 || rows.Length > BlueprintEditorDocument.MaximumParts || rows.Any(p => p == null)) throw Invalid("BBPF output piece count");
            if (rows.Any(p => payloadVersion < 2 && p.DisplayItem != null || payloadVersion < 3 && p.SignText != null || payloadVersion < 4 && p.Scale.HasValue))
                throw Invalid("BBPF payload version would discard fields");
            byte[] packed; int rawLength;
            using (var raw = new BoundedOutput(MaximumRawBytes))
            {
                using (var writer = new BinaryWriter(raw, Utf8, true))
                {
                    writer.Write(payloadVersion); writer.Write(name);
                    var table = rows.Select(p => p.Prefab).Distinct(StringComparer.Ordinal).ToArray();
                    var indices = table.Select((p, i) => (p, i)).ToDictionary(p => p.p, p => p.i, StringComparer.Ordinal);
                    writer.Write(table.Length); foreach (string prefab in table) writer.Write(prefab);
                    writer.Write(rows.Length);
                    foreach (Piece row in rows)
                    {
                        writer.Write(indices[row.Prefab]); WriteVector(writer, row.Position);
                        writer.Write((float)row.Rotation.X); writer.Write((float)row.Rotation.Y); writer.Write((float)row.Rotation.Z); writer.Write((float)row.Rotation.W);
                    }
                    writer.Write(terrain != null);
                    if (terrain != null)
                    {
                        writer.Write(terrain.Version); writer.Write(terrain.Width); writer.Write(terrain.Spacing); writer.Write(terrain.Radius);
                        writer.Write(terrain.OriginX); writer.Write(terrain.OriginZ); foreach (float height in terrain.Heights) writer.Write(height);
                    }
                    if (payloadVersion >= 2)
                    {
                        writer.Write(rows.Count(p => p.DisplayItem != null));
                        for (int i = 0; i < rows.Length; ++i) if (rows[i].DisplayItem is Attachment item)
                        { writer.Write(i); writer.Write(item.Prefab); writer.Write(item.Variant); writer.Write(item.Orientation); }
                    }
                    if (payloadVersion >= 3)
                    {
                        writer.Write(rows.Count(p => p.SignText != null));
                        for (int i = 0; i < rows.Length; ++i) if (rows[i].SignText != null) { writer.Write(i); writer.Write(rows[i].SignText); }
                    }
                    if (payloadVersion >= 4)
                    {
                        writer.Write(rows.Count(p => p.Scale.HasValue));
                        for (int i = 0; i < rows.Length; ++i) if (rows[i].Scale.HasValue) { writer.Write(i); WriteVector(writer, rows[i].Scale.Value); }
                    }
                    writer.Flush(); rawLength = checked((int)raw.Length);
                }
                raw.Position = 0;
                using (var output = new BoundedOutput(BlueprintSourceDocument.MaximumBytes - HeaderBytes))
                {
                    using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, true)) raw.CopyTo(deflate);
                    packed = output.ToArray();
                }
            }
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output, Utf8, true))
            using (var hash = SHA256.Create())
            {
                writer.Write(Magic); writer.Write(1); writer.Write(rawLength); writer.Write(packed.Length); writer.Write(hash.ComputeHash(packed)); writer.Write(packed);
                writer.Flush(); return output.ToArray();
            }
        }

        public sealed class Piece
        {
            public Piece(int record, string prefab, Point3 position, Rotation3 rotation, string signText = null, Attachment displayItem = null, Point3? scale = null)
            {
                if (record < 0 || record > BlueprintEditorDocument.MaximumParts) throw Invalid("BBPF record");
                ValidatePrefab(prefab); ValidateVector(position, 150);
                var wireRotation = new Rotation3((float)rotation.X, (float)rotation.Y, (float)rotation.Z, (float)rotation.W);
                if (!FiniteFloat(rotation.X) || !FiniteFloat(rotation.Y) || !FiniteFloat(rotation.Z) || !FiniteFloat(rotation.W) ||
                    Math.Abs(NormSquared(rotation) - 1) > .005 || Math.Abs(NormSquared(wireRotation) - 1) > .005)
                    throw Invalid("BBPF quaternion");
                if (signText != null && (signText.Length > 4096 || signText.IndexOf('\0') >= 0)) throw Invalid("BBPF sign text");
                if (signText != null) Utf8.GetByteCount(signText);
                if (scale.HasValue) { ValidateVector(scale.Value, 10); if (scale.Value.X < .1 || scale.Value.Y < .1 || scale.Value.Z < .1) throw Invalid("BBPF scale"); }
                Record = record; Prefab = prefab; Position = position; Rotation = rotation; SignText = signText; DisplayItem = displayItem; Scale = scale;
            }
            public int Record { get; }
            public string Prefab { get; }
            public Point3 Position { get; }
            public Rotation3 Rotation { get; }
            public string SignText { get; }
            public Attachment DisplayItem { get; }
            public Point3? Scale { get; }
        }
        public sealed class Attachment
        {
            public Attachment(string prefab, int variant, int orientation)
            {
                ValidatePrefab(prefab);
                if (variant < 0 || variant > 255 || orientation < 0 || orientation > 63) throw Invalid("BBPF display item");
                Prefab = prefab; Variant = variant; Orientation = orientation;
            }
            public string Prefab { get; }
            public int Variant { get; }
            public int Orientation { get; }
        }
        public sealed class TerrainProfile
        {
            public TerrainProfile(int version, int width, float spacing, float radius, float originX, float originZ, IEnumerable<float> heights)
            {
                if ((version != 1 && version != 2) || width < 2 || width > (version == 1 ? 129 : 449) ||
                    !FiniteFloat(spacing) || spacing < .25f || spacing > 4 || !FiniteFloat(radius) || radius <= 0 || radius > (version == 1 ? 64 : 224) ||
                    !FiniteFloat(originX) || !FiniteFloat(originZ) || Math.Abs(originX) > 450 || Math.Abs(originZ) > 450 ||
                    version == 2 && (spacing != 1 || width != radius * 2 + 1) || heights == null) throw Invalid("BBPF terrain profile");
                var samples = heights.Take(width * width + 1).ToArray();
                if (samples.Length != width * width || samples.Any(h => !FiniteFloat(h) || Math.Abs(h) > 150)) throw Invalid("BBPF terrain heights");
                Version = version; Width = width; Spacing = spacing; Radius = radius; OriginX = originX; OriginZ = originZ; Heights = Array.AsReadOnly(samples);
            }
            public int Version { get; }
            public int Width { get; }
            public float Spacing { get; }
            public float Radius { get; }
            public float OriginX { get; }
            public float OriginZ { get; }
            public IReadOnlyList<float> Heights { get; }
        }

        private static bool FiniteFloat(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= -float.MaxValue && value <= float.MaxValue;
        private static double NormSquared(Rotation3 q) => q.X * q.X + q.Y * q.Y + q.Z * q.Z + q.W * q.W;
        private static void ValidateVector(Point3 v, double bound)
        { if (!FiniteFloat(v.X) || !FiniteFloat(v.Y) || !FiniteFloat(v.Z) || Math.Abs(v.X) > bound || Math.Abs(v.Y) > bound || Math.Abs(v.Z) > bound) throw Invalid("BBPF vector"); }
        private static void ValidatePrefab(string value)
        { if (string.IsNullOrWhiteSpace(value) || value.Length > 160 || value.IndexOfAny(new[] { '/', '\\', '\0' }) >= 0) throw Invalid("BBPF prefab"); Utf8.GetByteCount(value); }
        private static void ValidateName(string value)
        { if (string.IsNullOrWhiteSpace(value) || value.Length > 80) throw Invalid("BBPF name"); Utf8.GetByteCount(value); }
        private static void Version(int version) { if (version < 1 || version > 4) throw Invalid("BBPF payload version"); }
        private static Point3 ReadVector(BinaryReader reader) => new Point3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        private static void WriteVector(BinaryWriter writer, Point3 value) { writer.Write((float)value.X); writer.Write((float)value.Y); writer.Write((float)value.Z); }
        private static long Remaining(BinaryReader reader) => reader.BaseStream.Length - reader.BaseStream.Position;
        private static void Require(BinaryReader reader, long count) { if (count < 0 || count > Remaining(reader)) throw Invalid("Incomplete BBPF payload"); }
        private static int Count(BinaryReader reader, int minimum, int maximum)
        { int value = reader.ReadInt32(); if (value < minimum || value > maximum) throw Invalid("BBPF table count"); return value; }
        private static int Index(BinaryReader reader, int count) => Count(reader, 0, count - 1);
        private static string ReadText(BinaryReader reader, int maximum)
        {
            int length = 0;
            for (int shift = 0; shift < 21; shift += 7)
            {
                byte next = reader.ReadByte(); length |= (next & 127) << shift;
                if (length > maximum * 3) throw Invalid("BBPF text byte limit");
                if ((next & 128) == 0)
                {
                    Require(reader, length); string text = Utf8.GetString(reader.ReadBytes(length));
                    if (text.Length > maximum) throw Invalid("BBPF text character limit");
                    return text;
                }
            }
            throw Invalid("BBPF string prefix");
        }
        private static FormatException Invalid(string reason) => new FormatException(reason);
        private sealed class BoundedOutput : Stream
        {
            // Stream's span/async fallbacks reach Write; MemoryStream's newer overloads bypass an array-only override.
            private readonly MemoryStream buffer = new MemoryStream();
            private readonly int maximum;
            public BoundedOutput(int maximum) { this.maximum = maximum; }
            private void Guard(int count) { if (count < 0 || Position + count > maximum) throw Invalid("BBPF expanded/output limit"); }
            public byte[] ToArray() => buffer.ToArray();
            public override bool CanRead => true;
            public override bool CanWrite => true;
            public override bool CanSeek => true;
            public override long Length => buffer.Length;
            public override long Position { get => buffer.Position; set { if (value < 0 || value > maximum) throw Invalid("BBPF stream position"); buffer.Position = value; } }
            public override void Flush() => buffer.Flush();
            public override int Read(byte[] bytes, int offset, int count) => buffer.Read(bytes, offset, count);
            public override long Seek(long offset, SeekOrigin origin)
            { long position = origin == SeekOrigin.Begin ? offset : origin == SeekOrigin.Current ? Position + offset : Length + offset; Position = position; return Position; }
            public override void SetLength(long length) { if (length < 0 || length > maximum) throw Invalid("BBPF stream length"); buffer.SetLength(length); }
            public override void Write(byte[] bytes, int offset, int count) { Guard(count); buffer.Write(bytes, offset, count); }
            public override void WriteByte(byte value) { Guard(1); buffer.WriteByte(value); }
            protected override void Dispose(bool disposing) { if (disposing) buffer.Dispose(); base.Dispose(disposing); }
        }
    }
}
