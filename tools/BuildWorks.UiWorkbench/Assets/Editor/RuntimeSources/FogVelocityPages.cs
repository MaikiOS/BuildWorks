using System;
using System.Collections.Generic;
using System.Threading;

namespace OstrixMods.BuildWorks
{
    // A returned lease never becomes writable again, even when its array is reused.
    internal sealed class FogVelocityPages
    {
        internal sealed class Lease
        {
            internal readonly FogVelocityPages Pool;
            internal ushort[] Words { get; private set; }
            internal Lease(FogVelocityPages pool, ushort[] words) { Pool = pool; Words = words; }
            internal void Close() { Words = null; }
        }

        [Serializable] internal sealed class Snapshot
        {
            public int limit, freeLimit, words, allocated, active, free, peak, allocations, reuses;
            public long bytes;
        }

        private readonly int limit, freeLimit, words, thread = Thread.CurrentThread.ManagedThreadId;
        private readonly Stack<ushort[]> free;
        private int active, allocated, peak, allocations, reuses;
        internal long Bytes => allocated * (long)words * 2;

        internal FogVelocityPages(int capacity, int resident, int wordCount)
        {
            if (capacity < 1 || capacity > 32 || resident < 1 || resident > capacity || wordCount < 1 || wordCount > 96 * 80 * 80 * 4)
                throw new ArgumentOutOfRangeException(nameof(capacity));
            limit = capacity + resident; freeLimit = resident; words = wordCount;
            free = new Stack<ushort[]>(resident);
        }

        internal Lease Rent(bool clear)
        {
            RequireThread();
            ushort[] data;
            if (free.Count != 0) { data = free.Pop(); ++reuses; }
            else
            {
                if (allocated == limit) throw new InvalidOperationException("Fog velocity RAM page reservation exhausted.");
                data = new ushort[words]; ++allocated; ++allocations; peak = Math.Max(peak, allocated);
            }
            ++active;
            if (clear) Array.Clear(data, 0, data.Length);
            return new Lease(this, data);
        }

        internal bool CanWrite(Lease lease) => Thread.CurrentThread.ManagedThreadId == thread &&
            lease != null && ReferenceEquals(lease.Pool, this) && lease.Words != null;

        internal void Return(Lease lease)
        {
            RequireThread();
            if (!CanWrite(lease)) throw new InvalidOperationException("Fog velocity lease is stale or belongs to another pool.");
            var data = lease.Words; lease.Close(); --active;
            if (free.Count < freeLimit) free.Push(data);
            else --allocated;
        }

        internal void ClearFree()
        {
            RequireThread();
            if (active != 0) throw new InvalidOperationException("Fog velocity owners must drain before clearing RAM pages.");
            free.Clear(); allocated = 0;
        }

        internal Snapshot Read()
        {
            RequireThread();
            return new Snapshot { limit = limit, freeLimit = freeLimit, words = words, allocated = allocated,
                active = active, free = free.Count, peak = peak, allocations = allocations, reuses = reuses,
                bytes = allocated * (long)words * 2 };
        }

        private void RequireThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != thread) throw new InvalidOperationException("Fog RAM page ownership requires its creating thread.");
        }
    }
}
