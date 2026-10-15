using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read-only structural evidence for one committed world41/42 generation.
    // Does not load game state, authorize a refund or prove a two-file transaction.
    public static class NativeWorldStorageReadback
    {
        public const int MaximumMetadataBytes = 4 * 1024 * 1024;
        public const int MaximumChunkBytes = 64 * 1024 * 1024;
        public const long MaximumWorldBytes = 1024L * 1024 * 1024;
        public const int MaximumObjects = 4000000;
        public const int MaximumChunks = 65536;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        internal static bool SupportedVersion(int version) => version == 41 || version == 42;
        public sealed class Expectation
        {
            public long World { get; }
            public Guid Operation { get; }
            public int Prefab { get; }
            public long Creator { get; }
            internal readonly int CreatorKey, MarkerKey, ItemsKey;
            internal readonly byte[] Inventory;
            internal readonly string Marker;
            public Expectation(long world, Guid operation, int prefab, long creator, int creatorKey, int markerKey, int itemsKey, byte[] inventory)
            {
                if (world == 0 || operation == Guid.Empty || prefab == 0 || creator == 0 || creatorKey == markerKey || markerKey == itemsKey || creatorKey == itemsKey ||
                    inventory == null || inventory.Length < 6 || inventory.Length > 1024 * 1024 ||
                    BitConverter.ToInt32(inventory, 0) != 109 && BitConverter.ToInt32(inventory, 0) != 110)
                    throw new ArgumentException("Invalid saved-storage expectation.");
                World = world; Operation = operation; Prefab = prefab; Creator = creator;
                CreatorKey = creatorKey; MarkerKey = markerKey; ItemsKey = itemsKey; Inventory = (byte[])inventory.Clone();
                Marker = operation.ToString("N");
            }
        }
        public sealed class Evidence
        {
            public int FormatVersion { get; }
            public long World { get; }
            public Guid Operation { get; }
            public int Chunks { get; }
            public int Objects { get; }
            public string Chunk { get; }
            public long ObjectOffset { get; }
            public string InventorySha256 { get; }
            internal Evidence(Expectation expected, int version, int chunks, int objects, string chunk, long offset)
            {
                FormatVersion = version;
                World = expected.World; Operation = expected.Operation; Chunks = chunks; Objects = objects; Chunk = chunk; ObjectOffset = offset;
                using (var hash = SHA256.Create()) InventorySha256 = BitConverter.ToString(hash.ComputeHash(expected.Inventory)).Replace("-", "");
            }
        }
        private sealed class ChunkInfo
        {
            internal string Name;
            internal int Count;
        }
        public static bool TryVerify(Stream metadata, Stream mapping, Stream completion, Func<string, Stream> openChunk,
            Expectation expected, out Evidence evidence, out string reason, CancellationToken cancellation = default)
        {
            evidence = null; reason = null;
            try
            {
                if (expected == null || openChunk == null) throw new InvalidDataException("Missing saved-storage context.");
                cancellation.ThrowIfCancellationRequested();
                int version;
                using (var ok = Reader(completion, 4))
                {
                    version = ok.ReadInt32();
                    if (!SupportedVersion(version)) throw new InvalidDataException("Unsupported world completion version.");
                    End(ok);
                }
                using (var meta = Reader(metadata, MaximumMetadataBytes))
                {
                    if (meta.ReadInt32() != metadata.Length - 4 || meta.ReadInt32() != version)
                        throw new InvalidDataException("Invalid world metadata framing/version.");
                    Text(meta); Text(meta); meta.ReadInt32();
                    if (meta.ReadInt64() != expected.World) throw new InvalidDataException("Different saved world UID.");
                    meta.ReadInt32();
                    if (meta.ReadByte() != 1) throw new InvalidDataException("Saved world does not require object data.");
                    int keys = Count(meta, 65536, 1);
                    for (int i = 0; i < keys; ++i) { cancellation.ThrowIfCancellationRequested(); Text(meta); }
                    int players = Count(meta, 65536, 4);
                    for (int i = 0; i < players; ++i) { cancellation.ThrowIfCancellationRequested(); for (int j = 0; j < 4; ++j) Text(meta); }
                    End(meta);
                }
                var chunks = new List<ChunkInfo>();
                using (var map = Reader(mapping, MaximumMetadataBytes))
                {
                    if (map.ReadUInt16() != version) throw new InvalidDataException("Unsupported chunk mapping version.");
                    int declared = map.ReadInt32(), count = Count(map, MaximumChunks, 11);
                    if (declared < 0 || declared > MaximumObjects) throw new InvalidDataException("World object budget exceeded.");
                    var seen = new HashSet<(ushort, byte)>(); long actual = 0;
                    for (int i = 0; i < count; ++i)
                    {
                        ushort chunk = map.ReadUInt16(); byte size = map.ReadByte(); uint generation = map.ReadUInt32(); int objects = map.ReadInt32();
                        if (!seen.Add((chunk, size)) || generation == 0 || objects < 0 || (actual += objects) > MaximumObjects)
                            throw new InvalidDataException("Invalid or duplicate chunk mapping.");
                        chunks.Add(new ChunkInfo
                        {
                            Name = (chunk >> 8).ToString("x2", CultureInfo.InvariantCulture) + "_" +
                            (chunk & 255).ToString("x2", CultureInfo.InvariantCulture) + "__" + size.ToString(CultureInfo.InvariantCulture) + "_" +
                            generation.ToString(CultureInfo.InvariantCulture) + ".chunk",
                            Count = objects
                        });
                    }
                    // Native Save sums entries marked size-changed too, although those entries are omitted.
                    if (actual > declared) throw new InvalidDataException("Chunk counts exceed declared world count.");
                    End(map);
                }
                long bytes = metadata.Length + mapping.Length + completion.Length, foundOffset = 0;
                var scratchKeys = new int[32767];
                int matches = 0, totalObjects = 0; string foundChunk = null;
                foreach (var chunk in chunks)
                {
                    cancellation.ThrowIfCancellationRequested();
                    using (Stream stream = openChunk(chunk.Name))
                    using (var reader = Reader(stream, MaximumChunkBytes))
                    {
                        bytes = checked(bytes + stream.Length);
                        if (bytes > MaximumWorldBytes) throw new InvalidDataException("World readback byte budget exceeded.");
                        if (!SupportedVersion(reader.ReadUInt16()) || Count(reader, MaximumObjects, 10) != chunk.Count)
                            throw new InvalidDataException("Chunk header differs from committed mapping.");
                        for (int i = 0; i < chunk.Count; ++i)
                        {
                            cancellation.ThrowIfCancellationRequested(); long offset = stream.Position;
                            if (ReadObject(reader, expected, scratchKeys, cancellation))
                            { if (++matches > 1) throw new InvalidDataException("Duplicate saved operation marker."); foundChunk = chunk.Name; foundOffset = offset; }
                            ++totalObjects;
                        }
                        End(reader);
                    }
                }
                if (matches != 1) throw new InvalidDataException("Saved operation marker not found.");
                cancellation.ThrowIfCancellationRequested();
                evidence = new Evidence(expected, version, chunks.Count, totalObjects, foundChunk, foundOffset); return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                error is ArgumentException || error is OverflowException || error is OperationCanceledException)
            { reason = error.Message; return false; }
        }
        private static bool ReadObject(BinaryReader reader, Expectation expected, int[] scratchKeys, CancellationToken cancellation)
        {
            ushort flags = reader.ReadUInt16();
            if ((flags & ~0x3fff) != 0) throw new InvalidDataException("Unsupported ZDO flags.");
            if ((flags & 8192) != 0) Skip(reader, 4); else Finite(reader, 3);
            int prefab = reader.ReadInt32();
            if ((flags & 4096) != 0)
            {
                ushort first = reader.ReadUInt16();
                if ((first & 32768) == 0) reader.ReadUInt16();
            }
            if ((flags & 1) != 0) Skip(reader, 5);
            long creator = 0; string marker = null; bool itemsPresent = false, itemsMatch = false;
            for (int map = 0; map < 7; ++map)
            {
                if ((flags & (2 << map)) == 0) continue;
                int count = SmallCount(reader);
                for (int i = 0; i < count; ++i)
                {
                    if ((i & 127) == 0) cancellation.ThrowIfCancellationRequested();
                    int key = reader.ReadInt32();
                    scratchKeys[i] = key;
                    switch (map)
                    {
                        case 0: Finite(reader, 1); break;
                        case 1: Finite(reader, 3); break;
                        case 2: Finite(reader, 4); break;
                        case 3: reader.ReadInt32(); break;
                        case 4: long value = reader.ReadInt64(); if (key == expected.CreatorKey) creator = value; break;
                        case 5:
                            if (key == expected.MarkerKey) marker = Text(reader);
                            else SkipText(reader);
                            break;
                        case 6:
                            int length = Count(reader, MaximumChunkBytes, 1);
                            if (key == expected.ItemsKey && marker == expected.Marker)
                            {
                                itemsPresent = true; itemsMatch = length == expected.Inventory.Length;
                                if (length > 1024 * 1024) throw new InvalidDataException("Marked inventory exceeds supported budget.");
                                for (int start = 0; start < length;)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    int take = Math.Min(8192, length - start); byte[] data = reader.ReadBytes(take);
                                    if (data.Length != take) throw new EndOfStreamException();
                                    if (itemsMatch)
                                        for (int n = 0; n < take; ++n) if (data[n] != expected.Inventory[start + n]) { itemsMatch = false; break; }
                                    start += take;
                                }
                            }
                            else Skip(reader, length);
                            break;
                    }
                }
                Array.Sort(scratchKeys, 0, count);
                for (int i = 1; i < count; ++i)
                    if (scratchKeys[i] == scratchKeys[i - 1]) throw new InvalidDataException("Duplicate typed ZDO key.");
            }
            if (marker != expected.Marker) return false;
            if ((flags & 256) == 0 || prefab != expected.Prefab || creator != expected.Creator || !itemsPresent)
                throw new InvalidDataException("Saved marked object's identity or contents differ.");
            if (!itemsMatch) throw new InvalidDataException("Saved marked inventory differs.");
            return true;
        }
        private static BinaryReader Reader(Stream stream, long cap)
        {
            if (stream == null || !stream.CanRead || !stream.CanSeek || stream.Position != 0 || stream.Length < 4 || stream.Length > cap)
                throw new InvalidDataException("Invalid saved-world stream or budget.");
            return new BinaryReader(stream, Utf8, true);
        }
        private static int Count(BinaryReader reader, int cap, int minimum)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > cap || (long)count * minimum > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid saved-world count/length.");
            return count;
        }
        private static int SmallCount(BinaryReader reader)
        {
            int first = reader.ReadByte(), count = first;
            if ((first & 128) != 0) { count = ((first & 127) << 8) | reader.ReadByte(); if (count < 128) throw new InvalidDataException("Noncanonical ZDO count."); }
            if (count < 1 || (long)count * 5 > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid typed ZDO map count.");
            return count;
        }
        private static string Text(BinaryReader reader)
        {
            int length = StringLength(reader, 32768);
            byte[] data = reader.ReadBytes(length);
            if (data.Length != length) throw new EndOfStreamException();
            return Utf8.GetString(data);
        }
        private static void SkipText(BinaryReader reader) => Skip(reader, StringLength(reader, MaximumChunkBytes));
        private static int StringLength(BinaryReader reader, int cap)
        {
            uint length = 0;
            for (int shift = 0; shift <= 28; shift += 7)
            {
                byte value = reader.ReadByte();
                if (shift == 28 && (value & 248) != 0) throw new InvalidDataException("Invalid native string length.");
                length |= (uint)(value & 127) << shift;
                if ((value & 128) == 0)
                {
                    if (shift > 0 && value == 0 || length > cap || length > reader.BaseStream.Length - reader.BaseStream.Position)
                        throw new InvalidDataException("Invalid native string budget.");
                    return (int)length;
                }
            }
            throw new InvalidDataException("Unterminated native string length.");
        }
        private static void Finite(BinaryReader reader, int count)
        {
            for (int i = 0; i < count; ++i)
            { float value = reader.ReadSingle(); if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Nonfinite ZDO value."); }
        }
        private static void Skip(BinaryReader reader, int bytes)
        {
            if (bytes < 0 || bytes > reader.BaseStream.Length - reader.BaseStream.Position) throw new EndOfStreamException();
            reader.BaseStream.Position += bytes;
        }
        private static void End(BinaryReader reader)
        { if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing saved-world bytes."); }
    }
}
