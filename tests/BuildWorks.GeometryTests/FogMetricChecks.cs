using System;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogMetricChecks
    {
        internal static void Run()
        {
            for (int bits = 0; bits <= ushort.MaxValue; ++bits)
            {
                ushort half = (ushort)bits;
                if ((bits & 0x7c00) == 0x7c00) { Reject(() => BlueprintFogSparseMetrics.Half(half)); continue; }
                float expected = (float)BitConverter.UInt16BitsToHalf(half);
                Require(BitConverter.SingleToInt32Bits(expected) == BitConverter.SingleToInt32Bits(BlueprintFogSparseMetrics.Half(half)));
            }
            var maps = new uint[57600]; Array.Fill(maps, uint.MaxValue);
            var payload = new uint[128]; payload[1] = 0x3c000000; // alpha1 in one cell; every other cell is0.
            var empty = BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1);
            Require(empty.pages == 0 && empty.maximum == 0 && empty.sum == 0);
            maps[0] = 0;
            var active = BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1);
            Require(active.pages == 1 && active.nonzero == 1 && active.aboveThreshold == 1 && active.maximum == 1 && active.sum == 1);
            var bricks = new ushort[6 * 6 * 6 * 4]; bricks[4 * (1 + 6 * (1 + 6)) + 3] = 0x3c00;
            BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1, bricks, 1, 1);
            bricks[4 * (1 + 6 * (1 + 6)) + 3] = 0;
            Reject(() => BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1, bricks, 1, 1));
            payload[1] = 0x80000000;
            var signedZero = BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1, bricks, 1, 1);
            Require(signedZero.nonzero == 0 && signedZero.negative == 0 && signedZero.sum == 0);
            payload[1] = 0x3c000000;
            maps[0] = 1; Reject(() => BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1)); maps[0] = 0;
            payload[1] = 0x7c000000; Reject(() => BlueprintFogSparseMetrics.Measure(maps, payload, 0, 0, 1));
            Reject(() => BlueprintFogSparseMetrics.Measure(maps, payload, 1, 0, 1));
            Reject(() => BlueprintFogSparseMetrics.Measure(maps, payload, 0, 3, 1));
            Reject(() => BlueprintFogSparseMetrics.Measure(maps, new uint[127], 0, 0, 1));
            var metadata = new uint[16]; var receipts = new uint[16]; var bases = new uint[1];
            metadata[8] = receipts[0] = 7; metadata[10] = receipts[2] = 3;
            metadata[12] = receipts[4] = 9; metadata[14] = receipts[6] = 10;
            metadata[7] = 3;
            BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, 0, 7, 3, 0);
            foreach (uint flags in new[] { 0u, 1u, 2u, 4u, 7u })
            {
                metadata[7] = flags;
                Reject(() => BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, 0, 7, 3, 0));
            }
            metadata[7] = 3;
            foreach (int word in new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 })
            {
                receipts[word] ^= 1;
                Reject(() => BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, 0, 7, 3, 0));
                receipts[word] ^= 1;
            }
            Reject(() => BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, 0, 7, 3, 4));
            Reject(() => BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, 1, 7, 3, 0));
            var edgeMaps = new uint[115200]; Array.Fill(edgeMaps, uint.MaxValue);
            var edgePayload = new uint[256]; edgePayload[255] = 0x38000000;
            edgeMaps[57600 + 28800 + 28799] = 1;
            var edgeBricks = new ushort[12 * 12 * 6 * 4]; edgeBricks[4 * (10 + 12 * (4 + 12 * 4)) + 3] = 0x3800;
            var edge = BlueprintFogSparseMetrics.Measure(edgeMaps, edgePayload, 86400, 2, 2, edgeBricks, 2, 1);
            Require(edge.pages == 1 && edge.nonzero == 1 && edge.maximum == .5f && edge.sum == .5);
            var inactiveBank = BlueprintFogSparseMetrics.Measure(edgeMaps, edgePayload, 57600, 2, 2);
            Require(inactiveBank.pages == 0 && inactiveBank.sum == 0);
        }
        private static void Require(bool value) { if (!value) throw new InvalidOperationException("Published metric regression."); }
        private static void Reject(Action action) { try { action(); } catch (ArgumentException) { return; } throw new InvalidOperationException("Metric input accepted."); }
    }
}
