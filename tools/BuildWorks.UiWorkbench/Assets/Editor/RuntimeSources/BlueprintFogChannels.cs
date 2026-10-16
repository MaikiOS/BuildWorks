using System;
using System.Collections.Generic;

namespace OstrixMods.BuildWorks
{
    // shortcut: two active owners only; expand the measured budget before whole-house contact admission.
    internal sealed class BlueprintFogChannels : IDisposable
    {
        internal const int Capacity = 2;
        private readonly Dictionary<string, BlueprintFogSimulation> channels = new Dictionary<string, BlueprintFogSimulation>(StringComparer.Ordinal);
        private bool disposed, creating;
        internal int Count => channels.Count;
        internal long FieldBytes => Count * BlueprintFogSimulation.FieldBytes;

        internal bool TryAcquire(string owner, Func<BlueprintFogSimulation> create, out BlueprintFogSimulation channel)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not re-enter its pool.");
            if (string.IsNullOrWhiteSpace(owner) || owner.Length > 256 || create == null) throw new ArgumentException("Fog owner/factory missing.");
            if (channels.TryGetValue(owner, out channel)) return true;
            if (channels.Count >= Capacity) return false;
            creating = true;
            try { channel = create(); }
            finally { creating = false; }
            if (channel == null || !channel.Smoke || !channel.Surface) throw new InvalidOperationException("Fog owner returned no live channel.");
            foreach (BlueprintFogSimulation existing in channels.Values)
                if (ReferenceEquals(existing, channel)) throw new InvalidOperationException("Fog transport cannot belong to two owners.");
            channels.Add(owner, channel); return true;
        }

        internal bool TryReleaseRecovered(string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return true;
            if (!channel.TryGetRecovered()) return false;
            Remove(owner); return true;
        }

        // Only the construction owner decides that a built/removed part no longer needs its visual.
        internal void Remove(string owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(BlueprintFogChannels));
            if (creating) throw new InvalidOperationException("Fog channel factory must not mutate its pool.");
            if (!channels.TryGetValue(owner, out BlueprintFogSimulation channel)) return;
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
