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
