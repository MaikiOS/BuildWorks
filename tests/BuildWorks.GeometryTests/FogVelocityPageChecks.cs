using System;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogVelocityPageChecks
    {
        internal static void Run()
        {
            int checks = 0;
            void Check(bool ok, string why) { ++checks; if (!ok) throw new InvalidOperationException(why); }
            bool Refused(Action action) { try { action(); return false; } catch (InvalidOperationException) { return true; } }
            var pool = new FogVelocityPages(32, 4, 16);
            var leases = Enumerable.Range(0, 36).Select(_ => pool.Rent(false)).ToArray();
            Check(pool.Read().allocated == 36 && pool.Read().active == 36 && pool.Bytes == 1152 && Refused(() => pool.Rent(false)), "RAM reservation exceeded capacity+resident.");
            Check(leases.Select(l => l.Words).Distinct().Count() == 36, "Concurrent owners share a writable page.");
            var stale = leases[0]; var data = stale.Words; data[3] = 991;
            pool.Return(stale); var reused = pool.Rent(false);
            Check(ReferenceEquals(data, reused.Words) && reused.Words[3] == 991 && !pool.CanWrite(stale) && pool.CanWrite(reused), "A stale lease can write a recycled page.");
            Check(Refused(() => pool.Return(stale)), "Double return accepted.");
            var foreign = new FogVelocityPages(1, 1, 16);
            Check(!foreign.CanWrite(reused) && Refused(() => foreign.Return(reused)), "Cross-pool ownership accepted.");
            pool.Return(reused); var zero = pool.Rent(true);
            Check(ReferenceEquals(data, zero.Words) && zero.Words.All(word => word == 0), "New owner inherited previous velocity.");
            Check(Refused(pool.ClearFree), "Live pages cleared before drain.");
            bool wrongThread = true, threadRefused = false;
            var thread = new Thread(() => { wrongThread = pool.CanWrite(zero); threadRefused = Refused(() => pool.Return(zero)); });
            thread.Start(); thread.Join();
            Check(!wrongThread && threadRefused && pool.CanWrite(zero), "Wrong-thread callback can mutate custody.");
            var snapshot = pool.Read(); snapshot.allocated = 999;
            Check(pool.Read().allocated == 36, "Snapshot mutates pool accounting.");
            pool.Return(zero); foreach (var lease in leases.Skip(1)) pool.Return(lease);
            Check(pool.Read().active == 0 && pool.Read().free == 4 && pool.Read().allocated == 4 && pool.Bytes == 128, "Free pages exceed resident bound or aliases are counted twice.");
            pool.ClearFree(); pool.ClearFree();
            Check(pool.Bytes == 0 && !pool.CanWrite(reused), "Drained disposal retains pages or reactivates a stale lease.");
            var small = new FogVelocityPages(1, 1, 16);
            var committed = small.Rent(true); committed.Words[0] = 123;
            var pending = small.Rent(false); pending.Words[0] = 456;
            Check(committed.Words[0] == 123 && Refused(() => small.Rent(false)), "Staging overwrote the previous committed state.");
            // Failed/partial readback retains both pages until the caller's GPU drain.
            Check(small.Bytes == 64 && Refused(small.ClearFree), "An uncertain copy lost its retained reservation.");
            small.Return(pending); small.Return(committed); small.ClearFree();
            var steady = new FogVelocityPages(32, 4, 16);
            var current = Enumerable.Range(0, 32).Select(_ => steady.Rent(true)).ToArray();
            var staging = new FogVelocityPages.Lease[4];
            for (int pass = 0; pass < 10000; ++pass)
            {
                for (int i = 0; i < 4; ++i) staging[i] = steady.Rent(false);
                for (int i = 0; i < 4; ++i) { int owner = (pass * 4 + i) % 32; var old = current[owner]; current[owner] = staging[i]; steady.Return(old); }
            }
            Check(steady.Read().allocations == 36 && steady.Read().reuses == 39996 && steady.Read().allocated == 36 && steady.Read().peak == 36, "Array allocations grow after warmup.");
            foreach (var lease in current) steady.Return(lease); steady.ClearFree();
            Check(steady.Read().allocated == 0 && steady.Read().active == 0, "Final owners leaked pages.");
            Console.WriteLine("Fog RAM pages: " + checks + " grouped ownership/bound/zero/reuse checks passed.");
        }
    }
}
