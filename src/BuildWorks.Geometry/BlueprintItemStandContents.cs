using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Full read-only native v109 item package. A null package is a legacy/authoring recipe, not a snapshot.
    public static class BlueprintItemStandContents
    {
        // EWD legacy loose fields -> verified native record, never ZDO.Deserialize.
        // Caller must classify EVERY unconsumed outer field before enabling placement.
        public static bool TryConvertLegacy(BlueprintForeignZdo source, Func<string, int> hash, int? defaultWorldLevel,
            out BlueprintForeignPartData converted, out HashSet<KeyValuePair<int, Type>> consumed) =>
            TryConvertLegacy(source, hash, defaultWorldLevel, out converted, out consumed, null);

        public static bool TryConvertLegacy(BlueprintForeignZdo source, Func<string, int> hash, int? defaultWorldLevel,
            out BlueprintForeignPartData converted, out HashSet<KeyValuePair<int, Type>> consumed, string boundItemInfo)
        {
            converted = null; consumed = new HashSet<KeyValuePair<int, Type>>();
            if (source == null || hash == null) return false;
            var used = new HashSet<KeyValuePair<int, Type>>();
            try
            {
                string[] info = null;
                string prefab;
                if (source.TryGet<string>(hash("item"), out prefab))
                {
                    if (string.IsNullOrEmpty(prefab)) return false;
                    used.Add(new KeyValuePair<int, Type>(hash("item"), typeof(string)));
                }
                else
                {
                    if (!source.TryGet<int>(hash("item"), out int savedId) || savedId == 0 || boundItemInfo == null) return false;
                    // Resolve only from the same source-bound PlanBuild Info, never a guessed hash/name table.
                    var manifest = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, boundItemInfo, null, null);
                    info = manifest.Info.Split(':'); prefab = info[0];
                    if (hash(prefab) != savedId) return false;
                    used.Add(new KeyValuePair<int, Type>(hash("item"), typeof(int)));
                }
                int prefabId = hash(prefab);
                if (prefabId == 0) return false;
                BlueprintContainerInventory.Item original = null;
                if (source.HasHash(hash("itemData")))
                {
                    if (!source.TryGet<byte[]>(hash("itemData"), out var packet)) return false;
                    using (var input = new MemoryStream(packet, false))
                    using (var reader = new BinaryReader(input, new UTF8Encoding(false, true)))
                    {
                        if (reader.ReadByte() != 109) return false;
                        original = BlueprintContainerInventory.ReadItem(reader, new Dictionary<int, string> { [prefabId] = prefab });
                        if (input.Position != input.Length) return false;
                    }
                    used.Add(new KeyValuePair<int, Type>(hash("itemData"), typeof(byte[])));
                }
                int quality = Get("quality", original?.Quality ?? 1), stack = Get("stack", original?.Stack ?? 1);
                int variant = Get("variant", original?.Variant ?? 0), orientation = Get("type", 0);
                int world = Get("worldLevel", original?.WorldLevel ?? defaultWorldLevel ?? -1);
                long crafter = Get("crafterID", original?.CrafterId ?? 0);
                string crafterName = Get("crafterName", original?.CrafterName ?? "");
                bool pickedUp = Get("pickedUp", original?.PickedUp == true ? 1 : 0) != 0;
                bool cheated = Get("cheated", original?.Cheated == true ? 1 : 0) != 0;
                int durability = original?.DurabilityHundredths ?? 10000;
                if (source.HasHash(hash("durability")))
                {
                    float saved = Get("durability", 100f) * 100f; // Native float multiply then conv.i4, NOT Round.
                    if (saved < 0 || saved >= 2147483648f) return false;
                    durability = (int)saved;
                }
                if (quality < 1 || quality > ushort.MaxValue || stack != 1 || variant < 0 || orientation < 0 ||
                    world < 0 || world > byte.MaxValue || crafter == 0 && crafterName != "") return false;
                if (info != null && (variant != int.Parse(info[1], CultureInfo.InvariantCulture) ||
                    quality != int.Parse(info[2], CultureInfo.InvariantCulture) || orientation != int.Parse(info[3], CultureInfo.InvariantCulture))) return false;
                var custom = new Dictionary<string, string>(StringComparer.Ordinal);
                if (original != null) foreach (var pair in original.CustomData) custom.Add(pair.Key, pair.Value);
                int count = Get("dataCount", 0);
                if (count < 0 || count > 4096) return false;
                var looseKeys = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < count; ++index)
                {
                    string suffix = index.ToString(CultureInfo.InvariantCulture);
                    string key = Get("data_" + suffix, ""), value = Get("data__" + suffix, "");
                    if (key == "") { if (value != "") return false; continue; }
                    if (!looseKeys.Add(key)) return false;
                    custom[key] = value;
                }
                if (custom.Count > 32767) return false;
                using (var output = new MemoryStream())
                using (var writer = new BinaryWriter(output, new UTF8Encoding(false, true)))
                {
                    writer.Write((byte)109); writer.Write(durability);
                    writer.Write((byte)(original?.X ?? 0)); writer.Write((byte)(original?.Y ?? 0)); writer.Write((byte)world);
                    int flags = 64 | (pickedUp ? 1 : 0) | (original?.Equipped == true ? 2 : 0) |
                        (quality != 1 ? 4 : 0) | (variant != 0 ? 16 : 0) | (crafter != 0 ? 32 : 0) | (custom.Count != 0 ? 128 : 0);
                    writer.Write((byte)flags);
                    if (quality != 1) writer.Write((ushort)quality);
                    if (variant != 0) writer.Write(variant);
                    if (crafter != 0) { writer.Write(crafter); writer.Write(crafterName); }
                    writer.Write(prefabId);
                    if (custom.Count != 0)
                    {
                        if (custom.Count < 128) writer.Write((byte)custom.Count);
                        else { writer.Write((byte)(128 | custom.Count >> 8)); writer.Write((byte)(custom.Count & 255)); }
                        // Native ItemData.Save enumerates this map; preserve source order for exact inventory matching.
                        foreach (var pair in custom)
                        { writer.Write(pair.Key); writer.Write(pair.Value); }
                    }
                    writer.Write((byte)(cheated ? 1 : 0)); writer.Flush();
                    var result = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                        prefab + ":" + variant.ToString(CultureInfo.InvariantCulture) + ":" + quality.ToString(CultureInfo.InvariantCulture) +
                        ":" + orientation.ToString(CultureInfo.InvariantCulture), Convert.ToBase64String(output.ToArray()), null);
                    if (!TryRead(result, hash, out _)) return false;
                    converted = result; consumed = used; return true;
                }
                T Get<T>(string name, T fallback)
                {
                    int key = hash(name);
                    if (!source.HasHash(key)) return fallback;
                    if (!source.TryGet<T>(key, out T value)) throw new InvalidDataException("Wrong legacy item field type.");
                    used.Add(new KeyValuePair<int, Type>(key, typeof(T))); return value;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is ArgumentException || error is FormatException)
            { return false; }
        }

        public static bool TryRead(BlueprintForeignPartData data, Func<string, int> prefabHash,
            out BlueprintContainerInventory.Item item)
        {
            item = null;
            if (data?.Format != BlueprintForeignPartData.NativeItemStandFormat || data.Zdo == null || prefabHash == null)
                return false;
            string[] fields = data.Info.Split(':');
            int hash = prefabHash(fields[0]);
            if (hash == 0) return false;
            try
            {
                using (var stream = new MemoryStream(Convert.FromBase64String(data.Zdo), false))
                using (var reader = new BinaryReader(stream, new UTF8Encoding(false, true)))
                {
                    if (reader.ReadByte() != 109) return false;
                    var parsed = BlueprintContainerInventory.ReadItem(reader, new Dictionary<int, string> { [hash] = fields[0] });
                    if (stream.Position != stream.Length || parsed.Stack != 1 ||
                        parsed.Quality != int.Parse(fields[2], CultureInfo.InvariantCulture) ||
                        parsed.Variant != int.Parse(fields[1], CultureInfo.InvariantCulture)) return false;
                    item = parsed; return true;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is FormatException || error is DecoderFallbackException)
            { return false; }
        }
    }
}
