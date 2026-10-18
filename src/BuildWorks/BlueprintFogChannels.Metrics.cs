#if BUILDWORKS_TEST_STAND
using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
        internal sealed class MetricsProbe : IDisposable
        {
            private readonly SnapshotChannel[] owners;
            private readonly int capacity, side, depth;
            private readonly bool compareBricks;
            private uint[] metadata, bases, maps, payload, receipts;
            private uint[][] ledgers;
            private ushort[] bricks;
            private int received, expected;
            private bool disposed;
            private string error, result;
            private readonly float started;
            private readonly string phase;
            private readonly int passSide;
            private readonly long managedCopyBytes;
            internal bool Pending => received < expected && !disposed;
            internal bool Succeeded { get; private set; }

            internal MetricsProbe(BlueprintFogChannels pool, bool compare, string phase, int passSide)
            {
                pool.RequireSnapshots();
                if (pool.snapshotTeardown || pool.snapshotCount == 0) throw new InvalidOperationException("Live published owners required.");
                owners = Array.FindAll(pool.snapshotSlots, owner => owner != null);
                capacity = pool.arena.BlockCapacity; side = pool.arena.BrickGrid.x; depth = pool.arena.BrickGrid.z;
                compareBricks = compare; this.phase = phase; this.passSide = passSide;
                ledgers = new uint[owners.Length][]; expected = 5 + owners.Length + (compare ? 1 : 0); started = Time.realtimeSinceStartup;
                long bytes = (long)pool.arena.Buffers["_Maps"].count * 4 + (long)capacity * 512 + pool.capacity * 132L + owners.Length * 4L;
                if (compare) bytes += pool.arena.RenderBytes;
                if (bytes > 128L * 1024 * 1024) throw new InvalidOperationException("QA metric managed copies exceed128MiB; no requests submitted.");
                // Native readback staging and driver overhead are additional, not part of this managed-copy limit.
                managedCopyBytes = bytes;
                // One command buffer fixes a common queue boundary before the next simulation submission.
                using (var commands = new CommandBuffer { name = "BuildWorks QA published metrics" })
                {
                    void Read(string name, Action<uint[]> copy)
                    {
                        var buffer = pool.arena.Buffers[name]; int count = checked(buffer.count * buffer.stride / 4);
                        commands.RequestAsyncReadback(buffer, request => Receive(request, count, copy));
                    }
                    Read("_PublishedMetadata", values => metadata = values); Read("_PublishedMapBases", values => bases = values);
                    Read("_Maps", values => maps = values); Read("_Payload", values => payload = values); Read("_Receipts", values => receipts = values);
                    for (int i = 0; i < owners.Length; ++i)
                    {
                        int index = i;
                        commands.RequestAsyncReadback(owners[i].Ledger[2], request => Receive(request, 1, values => ledgers[index] = values));
                    }
                    if (compare)
                        commands.RequestAsyncReadback(pool.arena.Bricks, 0, TextureFormat.RGBAHalf, request => {
                            try
                            {
                                if (disposed) return;
                                if (request.hasError) throw new InvalidOperationException("Published brick readback failed.");
                                bricks = new ushort[checked(side * side * depth * 216 * 4)]; int offset = 0;
                                for (int layer = 0; layer < request.layerCount; ++layer)
                                {
                                    var data = request.GetData<ushort>(layer);
                                    if (data.Length > bricks.Length - offset) throw new InvalidOperationException("Published brick readback exceeds extent.");
                                    for (int j = 0; j < data.Length; ++j) bricks[offset++] = data[j];
                                }
                                if (offset != bricks.Length) throw new InvalidOperationException("Published brick readback incomplete.");
                            }
                            catch (Exception failure) { error = error ?? failure.Message; }
                            finally { ++received; }
                        });
                    Graphics.ExecuteCommandBuffer(commands);
                }
            }
            private void Receive(AsyncGPUReadbackRequest request, int count, Action<uint[]> copy)
            {
                try
                {
                    if (disposed) return;
                    if (request.hasError) throw new InvalidOperationException("Published fog buffer readback failed.");
                    var data = request.GetData<uint>();
                    if (data.Length != count) throw new InvalidOperationException("Published fog buffer readback incomplete.");
                    var values = new uint[count]; for (int i = 0; i < count; ++i) values[i] = data[i]; copy(values);
                }
                catch (Exception failure) { error = error ?? failure.Message; }
                finally { ++received; }
            }
            internal string Status()
            {
                if (result != null) return result;
                if (Pending) return "pending";
                var report = new MetricsReport { success = false, phase = phase, passSide = passSide, bricksCompared = false,
                    captureMilliseconds = (Time.realtimeSinceStartup - started) * 1000, partsJson = new string[owners.Length] };
                report.managedCopyBytes = managedCopyBytes; report.estimatedCopiesAndStagingBytes = managedCopyBytes * 2;
                var analysis = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    if (disposed || error != null) throw new InvalidOperationException(error ?? "Published metrics cancelled.");
                    for (int i = 0; i < owners.Length; ++i)
                    {
                        var owner = owners[i]; int m = owner.Slot * 16;
                        BlueprintFogSparseMetrics.RequireBinding(metadata, bases, receipts, owner.Slot, owner.Identity, owner.Generation, ledgers[i][0]);
                        var smoke = BlueprintFogSparseMetrics.Measure(maps, payload, (int)bases[owner.Slot], 0, capacity, bricks, side, depth);
                        var surface = BlueprintFogSparseMetrics.Measure(maps, payload, (int)bases[owner.Slot], 2, capacity, bricks, side, depth);
                        if (compareBricks) report.comparedTexels += (smoke.pages + surface.pages) * 64L;
                        float clock = BitConverter.ToSingle(BitConverter.GetBytes(metadata[m + 5]), 0);
                        if (float.IsNaN(clock) || float.IsInfinity(clock) || clock < 0 || clock > 86400)
                            throw new InvalidOperationException("Published fog clock is invalid.");
                        report.partsJson[i] = JsonUtility.ToJson(new PartMetric {
                            slot = owner.Slot, owner = owner.Identity, generation = owner.Generation,
                            sequence = BlueprintFogSparseMetrics.Long(metadata[m + 12], metadata[m + 13]), tick = (int)metadata[m + 14], ledgerFlags = ledgers[i][0],
                            clock = clock,
                            smokeJson = JsonUtility.ToJson(smoke), surfaceJson = JsonUtility.ToJson(surface) });
                    }
                    report.success = true; report.bricksCompared = compareBricks; Succeeded = true;
                }
                catch (Exception failure) { report.error = failure.Message; }
                analysis.Stop(); report.analysisMilliseconds = analysis.Elapsed.TotalMilliseconds;
                result = JsonUtility.ToJson(report); Dispose(); return result;
            }
            public void Dispose()
            {
                disposed = true; metadata = bases = maps = payload = receipts = null; ledgers = null; bricks = null;
            }
            [Serializable] private sealed class MetricsReport
            {
                public bool success, bricksCompared;
                public string error, phase;
                public int passSide;
                public float captureMilliseconds;
                public double analysisMilliseconds;
                public long comparedTexels;
                public long managedCopyBytes, estimatedCopiesAndStagingBytes;
                public string[] partsJson;
            }
            [Serializable] private sealed class PartMetric
            {
                public int slot, tick;
                public long owner, generation, sequence;
                public uint ledgerFlags;
                public float clock;
                public string smokeJson, surfaceJson;
            }
        }
    }
}
#endif
