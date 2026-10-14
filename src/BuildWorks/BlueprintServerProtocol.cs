using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks
{
    // No peer identity or role in requests. Connection authentication belongs to the host adapter.
    internal static class BlueprintServerProtocol
    {
        internal const int MaximumFrameBytes = 64 * 1024;
        internal const int ChunkBytes = 60 * 1024;
        internal const int PageSize = 12;
        internal const int MaximumDocumentBytes = BlueprintExchangeFormat.MaximumBytes;
        internal enum Command : byte { List, ListResult, Read, DataStart, Chunk, PublishStart, Commit, Published, Delete, Deleted, Error, Cancel }
        internal enum Failure : byte { Invalid, Unavailable, Denied, Conflict, Capacity, Busy, Cancelled, Failed, Timeout }

        internal sealed class Item
        {
            internal Guid Id;
            internal long Revision;
            internal byte Access;
            internal int Parts, Bytes;
            internal string Name, Category, Publisher, Editor, Sha256;
        }
        internal sealed class Message
        {
            internal Command Kind;
            internal Guid Request, Session, Id;
            internal long World, Revision;
            internal int Offset, Total, Length;
            internal byte Access;
            internal bool Administrator;
            internal string Sha256;
            internal byte[] Bytes;
            internal Item[] Items;
            internal Item Item;
            internal Failure Error;
        }

        internal static byte[] Encode(Message message)
        {
            if (message == null || message.Request == Guid.Empty || message.World == 0) throw Invalid();
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, new UTF8Encoding(false, true)))
            {
                writer.Write((byte)1); writer.Write((byte)message.Kind); writer.Write(message.World); writer.Write(message.Request.ToByteArray());
                writer.Write(message.Session.ToByteArray());
                switch (message.Kind)
                {
                    case Command.List: writer.Write(message.Offset); break;
                    case Command.ListResult:
                        writer.Write(message.Administrator); writer.Write(message.Total); writer.Write(message.Offset);
                        if (message.Items == null || message.Items.Length > PageSize) throw Invalid();
                        writer.Write(message.Items.Length); foreach (var item in message.Items) WriteItem(writer, item); break;
                    case Command.Read:
                    case Command.Delete:
                        writer.Write(message.Id.ToByteArray()); writer.Write(message.Revision); break;
                    case Command.DataStart: case Command.Published: WriteItem(writer, message.Item); break;
                    case Command.Chunk:
                        if (message.Bytes == null || message.Bytes.Length < 1 || message.Bytes.Length > ChunkBytes) throw Invalid();
                        writer.Write(message.Offset); writer.Write(message.Bytes.Length); writer.Write(message.Bytes); break;
                    case Command.PublishStart:
                        writer.Write(message.Id.ToByteArray()); writer.Write(message.Revision); writer.Write(message.Access);
                        writer.Write(message.Length); WriteHash(writer, message.Sha256); break;
                    case Command.Deleted: writer.Write(message.Id.ToByteArray()); break;
                    case Command.Error: writer.Write((byte)message.Error); break;
                    case Command.Commit: case Command.Cancel: break;
                    default: throw Invalid();
                }
                writer.Flush();
                if (memory.Length > MaximumFrameBytes) throw Invalid();
                byte[] bytes = memory.ToArray();
                Decode(bytes); // One validation boundary for locally built and received frames.
                return bytes;
            }
        }

        internal static Message Decode(byte[] frame)
        {
            if (frame == null || frame.Length < 42 || frame.Length > MaximumFrameBytes) throw Invalid();
            try
            {
                using (var memory = new MemoryStream(frame, false))
                using (var reader = new BinaryReader(memory, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadByte() != 1) throw Invalid();
                    var message = new Message { Kind = (Command)reader.ReadByte(), World = reader.ReadInt64(), Request = ReadGuid(reader), Session = ReadGuid(reader) };
                    if (message.World == 0 || message.Request == Guid.Empty || message.Kind != Command.List && message.Session == Guid.Empty) throw Invalid();
                    switch (message.Kind)
                    {
                        case Command.List: message.Offset = ReadRange(reader, 0, ServerBlueprintLibrary.MaximumEntries); break;
                        case Command.ListResult:
                            byte admin = reader.ReadByte(); if (admin > 1) throw Invalid(); message.Administrator = admin == 1;
                            message.Total = ReadRange(reader, 0, ServerBlueprintLibrary.MaximumEntries);
                            message.Offset = ReadRange(reader, 0, message.Total);
                            int count = ReadRange(reader, 0, Math.Min(PageSize, message.Total - message.Offset));
                            message.Items = new Item[count];
                            for (int i = 0; i < count; ++i) message.Items[i] = ReadItem(reader);
                            break;
                        case Command.Read:
                        case Command.Delete:
                            message.Id = ReadGuid(reader); message.Revision = reader.ReadInt64();
                            if (message.Id == Guid.Empty || message.Revision < 1) throw Invalid(); break;
                        case Command.DataStart: case Command.Published: message.Item = ReadItem(reader); break;
                        case Command.Chunk:
                            message.Offset = ReadRange(reader, 0, MaximumDocumentBytes - 1);
                            int length = ReadRange(reader, 1, Math.Min(ChunkBytes, MaximumDocumentBytes - message.Offset));
                            message.Bytes = ReadExact(reader, length); break;
                        case Command.PublishStart:
                            message.Id = ReadGuid(reader); message.Revision = reader.ReadInt64(); message.Access = reader.ReadByte();
                            if (message.Access > 1 || message.Id == Guid.Empty && message.Revision != 0 ||
                                message.Id != Guid.Empty && message.Revision < 1) throw Invalid();
                            message.Length = ReadRange(reader, 1, MaximumDocumentBytes); message.Sha256 = ReadHash(reader); break;
                        case Command.Deleted: message.Id = ReadGuid(reader); if (message.Id == Guid.Empty) throw Invalid(); break;
                        case Command.Error: message.Error = (Failure)reader.ReadByte(); if ((byte)message.Error > (byte)Failure.Timeout) throw Invalid(); break;
                        case Command.Commit: case Command.Cancel: break;
                        default: throw Invalid();
                    }
                    if (memory.Position != memory.Length) throw Invalid();
                    return message;
                }
            }
            catch (Exception exception) when (exception is EndOfStreamException || exception is DecoderFallbackException || exception is ArgumentException)
            { throw Invalid(); }
        }

        private static void WriteItem(BinaryWriter writer, Item item)
        {
            if (item == null) throw Invalid();
            writer.Write(item.Id.ToByteArray()); writer.Write(item.Revision); writer.Write(item.Access); writer.Write(item.Parts); writer.Write(item.Bytes);
            WriteText(writer, item.Name); WriteText(writer, item.Category); WriteText(writer, item.Publisher); WriteText(writer, item.Editor);
            WriteHash(writer, item.Sha256);
        }
        private static Item ReadItem(BinaryReader reader)
        {
            var item = new Item
            {
                Id = ReadGuid(reader),
                Revision = reader.ReadInt64(),
                Access = reader.ReadByte(),
                Parts = ReadRange(reader, 1, 100000),
                Bytes = ReadRange(reader, 1, MaximumDocumentBytes),
                Name = ReadText(reader),
                Category = ReadText(reader),
                Publisher = ReadText(reader),
                Editor = ReadText(reader),
                Sha256 = ReadHash(reader)
            };
            if (item.Id == Guid.Empty || item.Revision < 1 || item.Access > 1 || string.IsNullOrWhiteSpace(item.Name) ||
                string.IsNullOrWhiteSpace(item.Publisher) || string.IsNullOrWhiteSpace(item.Editor)) throw Invalid();
            return item;
        }
        private static void WriteText(BinaryWriter writer, string text)
        {
            if (text == null || text.Length > 1024) throw Invalid();
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            if (bytes.Length > 1024) throw Invalid(); writer.Write(bytes.Length); writer.Write(bytes);
        }
        private static string ReadText(BinaryReader reader) => new UTF8Encoding(false, true).GetString(ReadExact(reader, ReadRange(reader, 0, 1024)));
        private static Guid ReadGuid(BinaryReader reader) => new Guid(ReadExact(reader, 16));
        private static void WriteHash(BinaryWriter writer, string hash)
        {
            if (hash == null || hash.Length != 64) throw Invalid();
            for (int i = 0; i < 64; i += 2)
            {
                if (!Uri.IsHexDigit(hash[i]) || !Uri.IsHexDigit(hash[i + 1])) throw Invalid();
                if (!byte.TryParse(hash.Substring(i, 2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out byte value)) throw Invalid();
                writer.Write(value);
            }
        }
        private static string ReadHash(BinaryReader reader) => BitConverter.ToString(ReadExact(reader, 32)).Replace("-", "");
        private static int ReadRange(BinaryReader reader, int minimum, int maximum)
        { int value = reader.ReadInt32(); if (value < minimum || value > maximum) throw Invalid(); return value; }
        private static byte[] ReadExact(BinaryReader reader, int length)
        {
            if (length > reader.BaseStream.Length - reader.BaseStream.Position) throw Invalid();
            byte[] result = reader.ReadBytes(length); if (result.Length != length) throw Invalid(); return result;
        }
        private static InvalidDataException Invalid() => new InvalidDataException("$buildworks_server.invalid");

        internal sealed class Transfer : IDisposable
        {
            private byte[] bytes;
            private SHA256 hash;
            private readonly string expected;
            internal int Received { get; private set; }
            internal Transfer(int length, string sha256)
            {
                if (length < 1 || length > MaximumDocumentBytes || sha256 == null || sha256.Length != 64) throw Invalid();
                using (var memory = new MemoryStream()) using (var writer = new BinaryWriter(memory)) WriteHash(writer, sha256);
                expected = sha256.ToUpperInvariant(); bytes = new byte[length]; hash = SHA256.Create();
            }
            internal void Append(int offset, byte[] chunk)
            {
                if (bytes == null || offset != Received || chunk == null || chunk.Length < 1 || chunk.Length > ChunkBytes || chunk.Length > bytes.Length - Received)
                    throw Invalid();
                Buffer.BlockCopy(chunk, 0, bytes, Received, chunk.Length); hash.TransformBlock(chunk, 0, chunk.Length, null, 0);
                Received += chunk.Length;
            }
            internal byte[] Complete()
            {
                if (bytes == null || Received != bytes.Length) throw Invalid();
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                string actual = BitConverter.ToString(hash.Hash).Replace("-", "");
                if (actual != expected) { Dispose(); throw Invalid(); }
                byte[] result = bytes; bytes = null; hash.Dispose(); hash = null; return result;
            }
            public void Dispose() { bytes = null; hash?.Dispose(); hash = null; }
        }
    }
}
