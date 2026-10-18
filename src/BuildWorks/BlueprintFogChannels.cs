using System;
using System.Collections.Generic;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    // A larger compact-owner pool shares the same hard field-byte budget; no unrestored owner is evicted.
    internal sealed partial class BlueprintFogChannels : IDisposable
    {
        internal const int Capacity = 2;
        internal const long MaximumFieldBudget = Capacity * BlueprintFogSimulation.FieldBytes;
        private readonly int capacity;
        private readonly long fieldBudget;
        private readonly bool sharedScratch;
        private readonly Dictionary<string, BlueprintFogSimulation> channels = new Dictionary<string, BlueprintFogSimulation>(StringComparer.Ordinal);
        private readonly Dictionary<Vector3Int, BlueprintFogScratch> scratches = new Dictionary<Vector3Int, BlueprintFogScratch>();
        private bool disposed, creating;
        internal int Count => arena == null ? channels.Count : snapshotCount;
        internal long FieldBytes
        {
            get
            {
                if (arena != null) return snapshotsDisposed ? 0 : arena.ReservedFieldBytes -
                    (arena.ResidentCapacity - ResidentSnapshotCount) * (long)VelocityWords * 2 - (capacity - snapshotCount) * arena.LedgerBytes;
                long bytes = 0;
                foreach (BlueprintFogSimulation channel in channels.Values) bytes += channel.AllocatedFieldBytes;
                foreach (BlueprintFogScratch scratch in scratches.Values) bytes += scratch.FieldBytes;
                return bytes;
            }
        }

        internal BlueprintFogChannels() : this(Capacity, MaximumFieldBudget) { }
        internal BlueprintFogChannels(int ownerLimit, long byteBudget, bool sharedScratch = false)
        {
            if (ownerLimit < 1 || ownerLimit > 32 || byteBudget < 8 * 8 * 4 * 80 || byteBudget > MaximumFieldBudget)
                throw new ArgumentException("Fog owner/field budget exceeds its bounded allocation.");
            capacity = ownerLimit; fieldBudget = byteBudget; this.sharedScratch = sharedScratch;
        }

        internal bool TryAcquire(string owner, Vector3Int grid, Func<BlueprintFogScratch, BlueprintFogSimulation> create,
            out BlueprintFogSimulation channel)
        {
            RequireLegacy();
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not re-enter its pool.");
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 256 || create == null) throw new ArgumentException("Fog owner/factory missing.");
            long voxels = BlueprintFogSimulation.FieldVoxels(grid);
            if (!sharedScratch) return TryAcquire(owner, voxels * 80, () => create(null), out channel);
            if (channels.TryGetValue(owner, out channel)) return true;
            scratches.TryGetValue(grid, out BlueprintFogScratch scratch);
            long needed = voxels * 32 + (scratch == null ? voxels * 48 : 0);
            if (channels.Count >= capacity || needed > fieldBudget - FieldBytes) return false;
            bool allocated = scratch == null, cached = false;
            creating = true;
            try
            {
                if (allocated) scratch = new BlueprintFogScratch(grid);
                channel = create(scratch);
                foreach (BlueprintFogSimulation existing in channels.Values)
                    if (ReferenceEquals(existing, channel))
                    { channel = null; throw new InvalidOperationException("Fog transport cannot belong to two owners."); }
                if (channel == null || !channel.Smoke || !channel.Surface || channel.GridSize != grid ||
                    !ReferenceEquals(channel.Scratch, scratch) || channel.AllocatedFieldBytes != voxels * 32)
                    throw new InvalidOperationException("Fog factory did not return the reserved shared-scratch owner.");
                if (allocated) { scratches.Add(grid, scratch); cached = true; }
                channels.Add(owner, channel); ++scratch.Users;
                return true;
            }
            catch
            {
                try { channel?.Dispose(); }
                finally
                {
                    channel = null;
                    if (cached) scratches.Remove(grid);
                    if (allocated) scratch?.Dispose();
                }
                throw;
            }
            finally { creating = false; }
        }

        internal bool TryAcquire(string owner, Func<BlueprintFogSimulation> create, out BlueprintFogSimulation channel)
            => TryAcquire(owner, BlueprintFogSimulation.FieldBytes, create, out channel);

        internal bool TryAcquire(string owner, long reservedBytes, Func<BlueprintFogSimulation> create, out BlueprintFogSimulation channel)
        {
            RequireLegacy();
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not re-enter its pool.");
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 256 || create == null) throw new ArgumentException("Fog owner/factory missing.");
            if (reservedBytes < 8 * 8 * 4 * 80 || reservedBytes > BlueprintFogSimulation.FieldBytes || reservedBytes % 80 != 0)
                throw new ArgumentException("Fog field reservation is invalid.");
            if (channels.TryGetValue(owner, out channel)) return true;
            if (channels.Count >= capacity || reservedBytes > fieldBudget - FieldBytes) return false;
            creating = true;
            try { channel = create(); }
            finally { creating = false; }
            if (channel == null || !channel.Smoke || !channel.Surface) throw new InvalidOperationException("Fog owner returned no live channel.");
            foreach (BlueprintFogSimulation existing in channels.Values)
                if (ReferenceEquals(existing, channel)) throw new InvalidOperationException("Fog transport cannot belong to two owners.");
            if (channel.Scratch != null)
            {
                channel.Dispose(); channel = null;
                throw new InvalidOperationException("Shared fog scratch must be reserved through its owning pool.");
            }
            if (channel.AllocatedFieldBytes > reservedBytes)
            {
                channel.Dispose(); channel = null;
                throw new InvalidOperationException("Fog factory exceeded its reserved field budget.");
            }
            channels.Add(owner, channel); return true;
        }

        internal bool TryReleaseRecovered(string owner)
        {
            RequireLegacy();
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return true;
            if (!channel.TryGetRecovered()) return false;
            Remove(owner, channel); return true;
        }

        // Only the construction owner decides that a built/removed part no longer needs its visual.
        internal void Remove(string owner, BlueprintFogSimulation expected = null)
        {
            RequireLegacy();
            if (disposed) return;
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return;
            if (expected != null && !ReferenceEquals(expected, channel)) return;
            BlueprintFogScratch scratch = channel.Scratch;
            channels.Remove(owner); channel.Dispose();
            if (scratch != null && --scratch.Users == 0)
            { scratches.Remove(scratch.GridSize); scratch.Dispose(); }
        }

        public void Dispose()
        {
            if (arena != null) { DisposeSnapshots(); return; }
            if (disposed) return;
            if (creating) throw new InvalidOperationException("Fog channel factory must not dispose its pool.");
            disposed = true;
            foreach (BlueprintFogSimulation channel in channels.Values) channel.Dispose();
            channels.Clear();
            foreach (BlueprintFogScratch scratch in scratches.Values) scratch.Dispose();
            scratches.Clear();
        }
    }
}
