using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks
{
    // A larger compact-owner pool shares the same hard field-byte budget; no unrestored owner is evicted.
    internal sealed class BlueprintFogChannels : IDisposable
    {
        internal const int Capacity = 2;
        internal const long MaximumFieldBudget = Capacity * BlueprintFogSimulation.FieldBytes;
        private readonly int capacity;
        private readonly long fieldBudget;
        private readonly Dictionary<string, BlueprintFogSimulation> channels = new Dictionary<string, BlueprintFogSimulation>(StringComparer.Ordinal);
        private bool disposed, creating;
        internal int Count => channels.Count;
        internal long FieldBytes
        {
            get { long bytes = 0; foreach (BlueprintFogSimulation channel in channels.Values) bytes += channel.AllocatedFieldBytes; return bytes; }
        }

        internal BlueprintFogChannels() : this(Capacity, MaximumFieldBudget) { }
        internal BlueprintFogChannels(int ownerLimit, long byteBudget)
        {
            if (ownerLimit < 1 || ownerLimit > 32 || byteBudget < 8 * 8 * 4 * 80 || byteBudget > MaximumFieldBudget)
                throw new ArgumentException("Fog owner/field budget exceeds its bounded allocation.");
            capacity = ownerLimit; fieldBudget = byteBudget;
        }

        internal bool TryAcquire(string owner, Func<BlueprintFogSimulation> create, out BlueprintFogSimulation channel)
            => TryAcquire(owner, BlueprintFogSimulation.FieldBytes, create, out channel);

        internal bool TryAcquire(string owner, long reservedBytes, Func<BlueprintFogSimulation> create, out BlueprintFogSimulation channel)
        {
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
            if (channel.AllocatedFieldBytes > reservedBytes)
            {
                channel.Dispose(); channel = null;
                throw new InvalidOperationException("Fog factory exceeded its reserved field budget.");
            }
            channels.Add(owner, channel); return true;
        }

        internal bool TryReleaseRecovered(string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return true;
            if (!channel.TryGetRecovered()) return false;
            Remove(owner, channel); return true;
        }

        // Only the construction owner decides that a built/removed part no longer needs its visual.
        internal void Remove(string owner, BlueprintFogSimulation expected = null)
        {
            if (disposed) return;
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return;
            if (expected != null && !ReferenceEquals(expected, channel)) return;
            channels.Remove(owner); channel.Dispose();
        }

        public void Dispose()
        {
            if (disposed) return;
            if (creating) throw new InvalidOperationException("Fog channel factory must not dispose its pool.");
            disposed = true;
            foreach (BlueprintFogSimulation channel in channels.Values) channel.Dispose();
            channels.Clear();
        }
    }
}
