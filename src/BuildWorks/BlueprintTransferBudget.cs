using System;

namespace OstrixMods.BuildWorks
{
    // Includes workers which still hold buffers after a world or connection reset.
    internal sealed class BlueprintTransferBudget
    {
        private readonly long maximum;
        private long reserved;
        private readonly object gate = new object();
        internal BlueprintTransferBudget(long maximum)
        { if (maximum < 1) throw new ArgumentOutOfRangeException(nameof(maximum)); this.maximum = maximum; }
        internal long Reserved { get { lock (gate) return reserved; } }
        internal bool TryReserve(long bytes)
        {
            lock (gate)
            {
                if (bytes < 0 || bytes > maximum - reserved) return false;
                reserved += bytes; return true;
            }
        }
        internal void Release(long bytes)
        {
            lock (gate)
            {
                if (bytes < 0 || bytes > reserved) throw new InvalidOperationException("Invalid transfer reservation release.");
                reserved -= bytes;
            }
        }
    }
}
