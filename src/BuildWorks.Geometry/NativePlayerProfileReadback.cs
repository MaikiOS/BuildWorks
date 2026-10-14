using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read-only evidence for current Local profiles. Never loads a Player or Inventory.
    public static class NativePlayerProfileReadback
    {
        public const int MaximumProfileBytes = 32 * 1024 * 1024;
        public sealed class Evidence
        {
            internal Evidence(long player, string profileHash, string inventoryHash, int inventoryBytes)
            { Player = player; ProfileHash = profileHash; InventoryHash = inventoryHash; InventoryBytes = inventoryBytes; }
            public long Player { get; }
            public string ProfileHash { get; }
            public string InventoryHash { get; }
            public int InventoryBytes { get; }
        }

        public static bool TryVerify(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, out Evidence evidence, out string reason)
            => Verify(file, player, name, expected, prefabHash, null, out evidence, out reason);

        public static bool TryVerifyPlayerData(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, byte[] playerData, out Evidence evidence, out string reason)
            => Verify(file, player, name, expected, prefabHash, playerData ?? Array.Empty<byte>(), out evidence, out reason);

        private static bool Verify(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, byte[] playerData, out Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            try
            {
                if (player == 0 || name == null || expected == null || expected.ContentsCleared ||
                    !BlueprintContainerInventory.TryRead(expected, prefabHash, out _))
                    throw new InvalidDataException("Invalid expected live inventory.");
                byte[] inventory = Convert.FromBase64String(expected.Zdo);
                if (inventory.Length < 6 || BitConverter.ToInt32(inventory, 0) != 109)
                    throw new InvalidDataException("Only native inventory 109 is covered.");
                if (file == null || !file.CanRead || !file.CanSeek || file.Position != 0 ||
                    file.Length < 72 || file.Length > MaximumProfileBytes)
                    throw new InvalidDataException("Profile file exceeds the supported framing or budget.");
                byte[] payload, checksum;
                using (var reader = new BinaryReader(file, new UTF8Encoding(false, true), true))
                {
                    int length = reader.ReadInt32();
                    if (length < 0 || length != file.Length - 72) throw new InvalidDataException("Invalid profile payload length.");
                    payload = reader.ReadBytes(length);
                    if (payload.Length != length || reader.ReadInt32() != 64) throw new InvalidDataException("Invalid profile hash framing.");
                    checksum = reader.ReadBytes(64);
                    if (checksum.Length != 64 || file.Position != file.Length) throw new InvalidDataException("Truncated or trailing profile bytes.");
                }
                using (var hash = SHA512.Create())
                {
                    byte[] actual = hash.ComputeHash(payload); int difference = 0;
                    for (int i = 0; i < actual.Length; i++) difference |= actual[i] ^ checksum[i];
                    if (difference != 0) throw new InvalidDataException("Canonical profile checksum differs.");
                }
                using (var stream = new MemoryStream(payload, false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadInt32() != 46 || reader.ReadInt32() != 205 || reader.ReadInt32() != 10)
                        throw new InvalidDataException("Only native profile 46 is covered.");
                    for (int group = 0; group < 10; group++)
                    {
                        Skip(reader, 205 * 4);
                        for (int map = 0; map < 3; map++) ReadMap(reader);
                        if (reader.ReadInt32() != 5) throw new InvalidDataException("Unexpected enemy statistics groups.");
                        for (int map = 0; map < 10; map++) ReadMap(reader);
                    }
                    ReadBool(reader);
                    int worlds = ReadCount(reader, 1024, 60);
                    var worldIds = new HashSet<long>();
                    for (int i = 0; i < worlds; i++)
                    {
                        if (!worldIds.Add(reader.ReadInt64())) throw new InvalidDataException("Duplicate profile world.");
                        for (int point = 0; point < 3; point++) { ReadBool(reader); Skip(reader, 12); }
                        Skip(reader, 12);
                        if (ReadBool(reader)) Skip(reader, ReadCount(reader, MaximumProfileBytes, 1));
                    }
                    if (ReadString(reader) != name || reader.ReadInt64() != player)
                        throw new InvalidDataException("Canonical profile belongs to a different player.");
                    ReadString(reader); ReadBool(reader); reader.ReadInt64();
                    if (!ReadBool(reader)) throw new InvalidDataException("Profile has no player data.");
                    int playerBytes = ReadCount(reader, MaximumProfileBytes, 1);
                    if (playerData != null)
                    {
                        if (playerData.Length != playerBytes || playerData.Length < 4)
                            throw new InvalidDataException("Canonical player data differs from the fresh save request.");
                        long begin = stream.Position;
                        for (int i = 0; i < playerData.Length; ++i)
                            if (reader.ReadByte() != playerData[i]) throw new InvalidDataException("Canonical player data differs from the fresh save request.");
                        stream.Position = begin;
                    }
                    if (playerBytes != stream.Length - stream.Position || reader.ReadInt32() != 33)
                        throw new InvalidDataException("Invalid player-data framing or unsupported version.");
                    for (int i = 0; i < 4; i++) ReadFinite(reader);
                    ReadString(reader); ReadFinite(reader);
                    if (inventory.Length > stream.Length - stream.Position)
                        throw new InvalidDataException("Truncated saved inventory.");
                    // The retained blob was validated as a complete inventory, so this exact span has a known end.
                    for (int i = 0; i < inventory.Length; i++)
                        if (reader.ReadByte() != inventory[i]) throw new InvalidDataException("Saved inventory differs from the provided items after transfer.");
                    // Player's remaining tail is deliberately opaque: no claim of complete Player.Load validity.
                }
                using (var hash = SHA256.Create())
                    evidence = new Evidence(player, Hex(checksum), Hex(hash.ComputeHash(inventory)), inventory.Length);
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is FormatException || error is OverflowException)
            { reason = error.Message; return false; }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
        private static string ReadString(BinaryReader reader)
        {
            return BlueprintContainerInventory.ReadString(reader, 32768);
        }
        private static int ReadCount(BinaryReader reader, int cap, int minimumBytes)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > cap || (long)count * minimumBytes > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Invalid profile count or section length.");
            return count;
        }
        private static void ReadMap(BinaryReader reader)
        {
            int count = ReadCount(reader, 65536, 5);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                if (!keys.Add(ReadString(reader))) throw new InvalidDataException("Duplicate profile statistic key.");
                reader.ReadSingle();
            }
        }
        private static bool ReadBool(BinaryReader reader)
        {
            byte value = reader.ReadByte();
            if (value > 1) throw new InvalidDataException("Invalid native boolean.");
            return value == 1;
        }
        private static void ReadFinite(BinaryReader reader)
        {
            float value = reader.ReadSingle();
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Invalid player prefix value.");
        }
        private static void Skip(BinaryReader reader, int bytes)
        {
            if (bytes < 0 || bytes > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated profile section.");
            reader.BaseStream.Position += bytes;
        }
    }
}
