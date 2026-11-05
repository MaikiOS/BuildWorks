using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class UnboundBlueprintChecks
    {
        internal static void Run()
        {
            // Independent literal native prefix; expected hash is not obtained from the reader.
            const int floorHash = -1045925142;
            const string payload = "AAHqcqjB";
            const string row = "wood_floor|1.25|-0|-2.5|0|0.6|0|0.8|" + payload;
            const string grid = "#terrain|3|-4.000|-3.000|-2.000|-1.000|-0.000|1.000|2.000|3.000|4.000";
            string original = "\uFEFFvtbp 2\r\n" + grid + "\r\n\r\n" + row + "\n" + row + "\r";
            var oldCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("ru-RU");
                var file = UnboundBlueprint.Parse(original);
                Check(file.Text == original && Encoding.UTF8.GetBytes(file.Text).SequenceEqual(Encoding.UTF8.GetBytes(original)), "Raw bytes/newlines/BOM");
                Check(file.Pieces.Count == 2 && file.Pieces[0].Line == 4 && file.Pieces[1].Line == 5 &&
                    original.Substring(file.Pieces[0].Start, file.Pieces[0].Length) == row &&
                    original.Substring(file.Pieces[1].Start, file.Pieces[1].Length) == row, "Independent source row addresses");
                var piece = file.Pieces[0];
                Check(piece.PrefabName == "wood_floor" && piece.State.NativePrefabHash == floorHash &&
                    piece.Position.X == 1.25 && piece.Position.Z == -2.5 &&
                    piece.Rotation.Y == (double).6f && piece.Rotation.W == (double).8f && piece.ZdoData == payload,
                    "Invariant geometry/native binding/raw quaternion");
                Check(BitConverter.DoubleToInt64Bits(piece.Position.Y) == long.MinValue && file.TerrainSize == 3 &&
                    file.TerrainLine == 2 && file.Terrain.Count == 9 && file.Terrain[0] == -4 && file.Terrain[8] == 4 &&
                    BitConverter.SingleToInt32Bits(file.Terrain[4]) == int.MinValue, "Terrain indexing and signed zero");
                bool immutable = false;
                try { ((IList<float>)file.Terrain)[0] = 99; } catch (NotSupportedException) { immutable = true; }
                Check(immutable, "Mutable terrain leaked");
                immutable = false;
                try { ((IList<UnboundBlueprint.Piece>)file.Pieces).Clear(); } catch (NotSupportedException) { immutable = true; }
                Check(immutable, "Mutable rows leaked");
                Check(UnboundBlueprint.Parse("vtbp 2").Pieces.Count == 0 &&
                    UnboundBlueprint.Parse("vtbp 2\n\n" + row).TerrainSize == 0 &&
                    UnboundBlueprint.Parse("vtbp 2\n#terrain|1|2.000").Terrain[0] == 2,
                    "Optional terrain/no forced geometry defaults");
                string rich;
                using (var stream = new MemoryStream())
                {
                    using (var writer = new BinaryWriter(stream, new UTF8Encoding(false, true), true))
                    { writer.Write((ushort)64); writer.Write(floorHash); writer.Write((byte)1); writer.Write(17); writer.Write("табличка ♞"); }
                    rich = Convert.ToBase64String(stream.ToArray());
                }
                var stateful = UnboundBlueprint.Parse("vtbp 2\n" + row.Replace(payload, rich));
                Check(stateful.Pieces[0].State.TryGet<string>(17, out var value) && value == "табличка ♞", "State was not stripped to geometry");
                int rejected = 0;
                void Reject(string text)
                {
                    bool failed = false; try { UnboundBlueprint.Parse(text); } catch (FormatException) { failed = true; }
                    Check(failed, "Malformed file accepted"); ++rejected;
                }
                foreach (string header in new[] { "", "vtbp", "vtbp 1", "vtbp 3", "vtbp 2extra", " vtbp 2", "vtbp 2 ", "\uFEFF\uFEFFvtbp 2" })
                    Reject(header + "\n" + row);
                Reject(null); Reject(""); Reject("vtbp 2\n#unknown");
                foreach (string terrain in new[] { "#terrain", "#terrain|0|0", "#terrain|-3|0", "#terrain|2|0|0|0|0", "#terrain|2147483647|0",
                    "#terrain|3|0", "#terrain|1|0|1", "#terrain|1|", "#terrain|1|NaN", "#terrain|1|Infinity", "#terrain|1|1,25" })
                    Reject("vtbp 2\n" + terrain + "\n" + row);
                Reject("vtbp 2\n" + grid + "\n" + grid + "\n" + row);
                Reject("vtbp 2\n" + row + "\n" + grid);
                Reject("vtbp 2\n" + row + "|tail");
                Reject("vtbp 2\n" + row.Substring(0, row.LastIndexOf('|')));
                foreach (string prefab in new[] { "wood_wall", "", " wood_floor", "wood_floor ", "../wood_floor", "wood\\floor", "wood\0floor", "\uFEFFwood_floor", new string('a', 257) })
                    Reject("vtbp 2\n" + prefab + row.Substring(row.IndexOf('|')));
                for (int field = 1; field <= 7; ++field)
                    foreach (string bad in new[] { "NaN", "Infinity", "1e40", "1,25", "" })
                    { var fields = row.Split('|'); fields[field] = bad; Reject("vtbp 2\n" + string.Join("|", fields)); }
                Reject("vtbp 2\nwood_floor|0|0|0|0|0|0|0|" + payload);
                foreach (string bad in new[] { "", "!", " " + payload, payload + "\t", "AA==" })
                    Reject("vtbp 2\n" + row.Replace(payload, bad));
                Reject("vtbp 2\n" + row + "\n\ud800");
                Reject(new string('a', BlueprintSourceDocument.MaximumBytes + 1));
                Reject("vtbp 2\n" + new string('\u0410', BlueprintSourceDocument.MaximumBytes / 2));
                string many = "vtbp 2\n" + string.Concat(Enumerable.Repeat(row + "\n", BlueprintEditorDocument.MaximumParts));
                Check(UnboundBlueprint.Parse(many).Pieces.Count == BlueprintEditorDocument.MaximumParts, "Part count boundary");
                Reject(many + row);
                Console.WriteLine("PASS Unbound vtbp2 file: raw/row bindings/geometry/native state/terrain/culture/count ceiling;" + rejected +
                    " malformed controls rejected. Synthetic controls only; no actual author file, source pipeline, or game proof.");
            }
            finally { CultureInfo.CurrentCulture = oldCulture; }
        }
        private static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    }
}
