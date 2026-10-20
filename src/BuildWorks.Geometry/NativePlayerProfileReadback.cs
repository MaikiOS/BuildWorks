using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Read-only evidence for current Local profiles. Never loads a Player or Inventory.
    public static class NativePlayerProfileReadback
    {
        public const int MaximumProfileBytes = 32 * 1024 * 1024;
        // Receipt lookup is not evidence that the saved inventory matches a live or earlier inventory.
        public sealed class CustomDataEvidence
        {
            internal CustomDataEvidence(Evidence parsed)
            { Player = parsed.Player; ProfileHash = parsed.ProfileHash; CustomData = parsed.CustomData; }
            public long Player { get; }
            public string ProfileHash { get; }
            public IReadOnlyDictionary<string, string> CustomData { get; }
        }
        public sealed class Evidence
        {
            internal Evidence(long player, string profileHash, string inventoryHash, int inventoryBytes, BlueprintForeignPartData inventory,
                IReadOnlyDictionary<string, string> customData)
            { Player = player; ProfileHash = profileHash; InventoryHash = inventoryHash; InventoryBytes = inventoryBytes; Inventory = inventory; CustomData = customData; }
            public long Player { get; }
            public string ProfileHash { get; }
            public string InventoryHash { get; }
            public int InventoryBytes { get; }
            public BlueprintForeignPartData Inventory { get; }
            // Null for the legacy inventory-only proof; an empty verified map is different from an unparsed tail.
            public IReadOnlyDictionary<string, string> CustomData { get; }
        }

        public static bool TryVerify(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, out Evidence evidence, out string reason)
            => Verify(file, player, name, expected, prefabHash, null, out evidence, out reason);

        public static bool TryVerifyPlayerData(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, byte[] playerData, out Evidence evidence, out string reason)
            => Verify(file, player, name, expected, prefabHash, playerData ?? Array.Empty<byte>(), out evidence, out reason);

        public static bool TryReadCustomData(Stream file, long player, string name, out CustomDataEvidence evidence, out string reason)
        {
            evidence = null;
            if (!Verify(file, player, name, null, null, null, out var parsed, out reason, savedCustomData: true)) return false;
            evidence = new CustomDataEvidence(parsed); return true;
        }

        private static bool Verify(Stream file, long player, string name, BlueprintForeignPartData expected,
            Func<string, int> prefabHash, byte[] playerData, out Evidence evidence, out string reason, bool savedCustomData = false)
        {
            evidence = null; reason = null;
            try
            {
                if (player == 0 || name == null) throw new InvalidDataException("Invalid expected player identity.");
                byte[] inventory = null;
                if (!savedCustomData)
                {
                    if (expected == null || expected.ContentsCleared || !BlueprintContainerInventory.TryRead(expected, prefabHash, out _))
                        throw new InvalidDataException("Invalid expected live inventory.");
                    inventory = Convert.FromBase64String(expected.Zdo);
                    if (inventory.Length < 6 || BitConverter.ToInt32(inventory, 0) != 109 && BitConverter.ToInt32(inventory, 0) != 110)
                        throw new InvalidDataException("Only native inventories 109 and 110 are covered.");
                }
                IReadOnlyDictionary<string, string> customData = null;
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
                    int profileVersion = reader.ReadInt32();
                    if (profileVersion != 46 && profileVersion != 47 || reader.ReadInt32() != 205 || reader.ReadInt32() != 10)
                        throw new InvalidDataException("Only native profiles 46 and 47 are covered.");
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
                    if (playerBytes != stream.Length - stream.Position)
                        throw new InvalidDataException("Invalid player-data framing.");
                    int playerVersion = reader.ReadInt32();
                    if (playerVersion != 33 && playerVersion != 34)
                        throw new InvalidDataException("Invalid player-data framing or unsupported version.");
                    for (int i = 0; i < 4; i++) ReadFinite(reader);
                    ReadString(reader); ReadFinite(reader);
                    if (savedCustomData) ReadInventorySpan(reader);
                    else
                    {
                        if (inventory.Length > stream.Length - stream.Position) throw new InvalidDataException("Truncated saved inventory.");
                        // The retained blob was validated as a complete inventory, so this exact span has a known end.
                        for (int i = 0; i < inventory.Length; i++)
                            if (reader.ReadByte() != inventory[i]) throw new InvalidDataException("Saved inventory differs from the provided items after transfer.");
                    }
                    if (savedCustomData || playerData != null) customData = ReadPlayerTail(reader, playerVersion);
                    // Inventory-only callers keep their opaque-tail contract; no complete Player.Load claim.
                }
                using (var hash = SHA256.Create())
                    evidence = new Evidence(player, Hex(checksum), inventory == null ? null : Hex(hash.ComputeHash(inventory)), inventory?.Length ?? 0, expected, customData);
                return true;
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is FormatException || error is OverflowException)
            { reason = error.Message; return false; }
        }

        private static void ReadInventorySpan(BinaryReader reader)
        {
            int version = reader.ReadInt32(), count = reader.ReadUInt16();
            if (version != 109 && version != 110 || count > 4096 || count * 9L > reader.BaseStream.Length - reader.BaseStream.Position)
                throw new InvalidDataException("Unsupported or truncated saved inventory span.");
            // Decode only to locate the following fields. No prefab lookup, loading or inventory proof is returned.
            for (int i = 0; i < count; i++) BlueprintContainerInventory.SkipItem(reader, version);
        }

        private static IReadOnlyDictionary<string, string> ReadPlayerTail(BinaryReader reader, int version)
        {
            ReadStringSet(reader); // Recipes.
            int stations = ReadCount(reader, 65536, 5);
            var stationKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < stations; i++)
            {
                if (!stationKeys.Add(ReadString(reader, MaximumProfileBytes))) throw new InvalidDataException("Duplicate player station.");
                reader.ReadInt32();
            }
            for (int i = 0; i < 5; i++) ReadStringSet(reader); // Materials, tutorials, uniques, trophies, biomes.
            int texts = ReadCount(reader, 65536, 2);
            // Native Save strips control characters from known-text keys, so serialized keys can collide.
            for (int i = 0; i < texts; i++) { ReadString(reader, MaximumProfileBytes); ReadString(reader, MaximumProfileBytes); }
            ReadString(reader, MaximumProfileBytes); ReadString(reader, MaximumProfileBytes); // Beard and hair.
            for (int i = 0; i < 6; i++) ReadFinite(reader);
            reader.ReadInt32(); // Model.
            int foods = ReadCount(reader, 65536, 5);
            for (int i = 0; i < foods; i++) { ReadString(reader, MaximumProfileBytes); ReadFinite(reader); }
            if (reader.ReadInt32() != 2) throw new InvalidDataException("Only native Skills.Save version 2 is covered.");
            int skills = ReadCount(reader, 65536, 12);
            for (int i = 0; i < skills; i++)
            {
                reader.ReadInt32(); // Native Save writes the value's skill ID, which mods may repeat.
                ReadFinite(reader); ReadFinite(reader);
            }
            var customData = new ReadOnlyDictionary<string, string>(ReadStringMap(reader));
            for (int i = 0; i < 3; i++) ReadFinite(reader); // Stamina, maximum eitr, eitr.
            // BuildUi uses its own serializer. Bound its opaque bytes; they cannot move the preceding receipt map.
            Skip(reader, ReadCount(reader, MaximumProfileBytes, 1));
            if (version == 34) { ReadIntMap(reader); ReadIntMap(reader); }
            if (reader.BaseStream.Position != reader.BaseStream.Length) throw new InvalidDataException("Trailing player-data bytes.");
            return customData;
        }

        private static void ReadStringSet(BinaryReader reader)
        {
            int count = ReadCount(reader, 65536, 1);
            var keys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
                if (!keys.Add(ReadString(reader, MaximumProfileBytes))) throw new InvalidDataException("Duplicate player set entry.");
        }

        private static Dictionary<string, string> ReadStringMap(BinaryReader reader)
        {
            int count = ReadCount(reader, 65536, 2);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i < count; i++)
            {
                string key = ReadString(reader, MaximumProfileBytes), value = ReadString(reader, MaximumProfileBytes);
                if (values.ContainsKey(key)) throw new InvalidDataException("Duplicate player data key.");
                values.Add(key, value);
            }
            return values;
        }

        private static void ReadIntMap(BinaryReader reader)
        {
            int count = ReadCount(reader, 65536, 8);
            var keys = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                if (!keys.Add(reader.ReadInt32())) throw new InvalidDataException("Duplicate player integer key.");
                reader.ReadInt32();
            }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "");
        private static string ReadString(BinaryReader reader, int maximumBytes = 32768)
        {
            return BlueprintContainerInventory.ReadString(reader, maximumBytes);
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
            if (float.IsNaN(value) || float.IsInfinity(value)) throw new InvalidDataException("Invalid player numeric value.");
        }
        private static void Skip(BinaryReader reader, int bytes)
        {
            if (bytes < 0 || bytes > reader.BaseStream.Length - reader.BaseStream.Position) throw new InvalidDataException("Truncated profile section.");
            reader.BaseStream.Position += bytes;
        }
    }
}
