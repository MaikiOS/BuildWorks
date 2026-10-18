using System;

namespace OstrixMods.BuildWorks
{
    // Decode only pages reachable through a published map; free pages contain stale payload.
    internal static class BlueprintFogSparseMetrics
    {
        [Serializable] internal sealed class Alpha
        {
            public int pages, nonzero, aboveThreshold, negative;
            public float minimum, maximum;
            public double sum;
        }
        private static readonly float[] powers = MakePowers();
        private static float[] MakePowers()
        {
            var values = new float[31]; values[0] = (float)Math.Pow(2, -24);
            for (int i = 1; i < values.Length; ++i) values[i] = (float)Math.Pow(2, i - 25);
            return values;
        }
        internal static float Half(ushort bits)
        {
            int exponent = bits >> 10 & 31, fraction = bits & 1023;
            if (exponent == 31) throw new ArgumentException("Nonfinite published fog value.");
            float value = (exponent == 0 ? fraction : 1024 + fraction) * powers[exponent];
            return (bits & 32768) == 0 ? value : -value;
        }
        internal static long Long(uint low, uint high) => ((long)high << 32) | low;
        internal static void RequireBinding(uint[] metadata, uint[] bases, uint[] receipts, int slot, long owner, long generation, uint ledger)
        {
            if (metadata == null || bases == null || receipts == null || bases.Length < 1 || bases.Length > 32 ||
                metadata.Length != bases.Length * 16 || receipts.Length != metadata.Length || slot < 0 || slot >= bases.Length || owner <= 0 || generation <= 0)
                throw new ArgumentException("Invalid published fog metric identity buffers.");
            int m = slot * 16;
            if (metadata[m + 7] != 3 || Long(metadata[m + 8], metadata[m + 9]) != owner || Long(metadata[m + 10], metadata[m + 11]) != generation ||
                Long(receipts[m], receipts[m + 1]) != owner || Long(receipts[m + 2], receipts[m + 3]) != generation ||
                Long(metadata[m + 12], metadata[m + 13]) <= 0 || metadata[m + 12] != receipts[m + 4] || metadata[m + 13] != receipts[m + 5] ||
                metadata[m + 14] != receipts[m + 6] || metadata[m + 15] != receipts[m + 7] || receipts[m + 8] != 0 || receipts[m + 9] != bases[slot] ||
                (bases[slot] != slot * 57600 && bases[slot] != slot * 57600 + 28800) || (ledger & ~3u) != 0)
                throw new ArgumentException("Published fog identity/receipt/bank/ledger inconsistent; not a zero-density result.");
        }
        internal static Alpha Measure(uint[] maps, uint[] payload, int mapBase, int field, int capacity,
            ushort[] bricks = null, int brickSide = 0, int brickDepth = 0)
        {
            const int mapWords = 28800, blocks = mapWords / 3;
            if (maps == null || payload == null || capacity < 1 || capacity > 98304000 / 516 ||
                payload.Length != capacity * 128 || maps.Length % (mapWords * 2) != 0 ||
                mapBase < 0 || mapBase % mapWords != 0 || mapBase > maps.Length - mapWords || field < 0 || field > 2)
                throw new ArgumentException("Invalid published fog metric buffers or binding.");
            if (bricks != null && (brickSide < 1 || brickSide > 58 || brickDepth < 1 || brickDepth > brickSide ||
                (long)brickSide * brickSide * brickDepth < capacity || bricks.LongLength != (long)brickSide * brickSide * brickDepth * 216 * 4))
                throw new ArgumentException("Invalid published fog brick extent.");
            var result = new Alpha { minimum = float.PositiveInfinity, maximum = float.NegativeInfinity };
            for (int block = 0; block < blocks; ++block)
            {
                uint page = maps[mapBase + field * blocks + block];
                if (page == uint.MaxValue) { result.minimum = Math.Min(result.minimum, 0); result.maximum = Math.Max(result.maximum, 0); continue; }
                if (page >= capacity) throw new ArgumentException("Published fog page exceeds capacity.");
                ++result.pages;
                for (int cell = 0; cell < 64; ++cell)
                {
                    int address = checked(((int)page * 64 + cell) * 2);
                    uint a = payload[address], b = payload[address + 1];
                    for (int channel = 0; channel < 4; ++channel)
                    {
                        ushort bits = (ushort)((channel < 2 ? a : b) >> (channel % 2 * 16));
                        if ((bits & 0x7c00) == 0x7c00) throw new ArgumentException("Nonfinite published fog payload.");
                        if (bricks == null) continue;
                        int x = ((int)page % brickSide) * 6 + (cell % 4) + 1;
                        int y = ((int)page / brickSide % brickSide) * 6 + (cell / 4 % 4) + 1;
                        int z = ((int)page / (brickSide * brickSide)) * 6 + (cell / 16) + 1;
                        ushort actual = bricks[4 * (x + brickSide * 6 * (y + brickSide * 6 * z)) + channel];
                        if (actual != bits && ((actual | bits) & 0x7fff) != 0)
                            throw new ArgumentException("Published brick texel differs from committed payload.");
                    }
                    float alpha = Half((ushort)(b >> 16));
                    if (alpha != 0) ++result.nonzero;
                    if (alpha >= .001f) ++result.aboveThreshold;
                    if (alpha < 0) ++result.negative;
                    result.minimum = Math.Min(result.minimum, alpha); result.maximum = Math.Max(result.maximum, alpha); result.sum += alpha;
                }
            }
            return result;
        }
    }
}
