using System;
using System.Threading;

namespace OstrixMods.BuildWorks
{
    // Bounded observer only. Durations are CPU submission/callback waits, never GPU execution time.
    internal sealed class FogPerformanceCounters
    {
        internal enum Stage { Submit, SolverSubmission, RestoreCreate, PageRelease, Upload, CopyEnqueue, CallbackCopy, Count }
        internal enum Kind { Receipt, Velocity, Count }
        [Serializable] internal sealed class Timing { public long count; public double totalMilliseconds, maxMilliseconds; }
        [Serializable] internal sealed class Flow
        {
            public long requested, callbacks, accepted, rejected, submissionFailures, unreadable;
            public long requestedBytes, cpuCopiedBytes, awaitingCallback, awaitingAcceptance;
            public double requestToCallbackMilliseconds, maxRequestToCallbackMilliseconds, callbackToAcceptMilliseconds, maxCallbackToAcceptMilliseconds;
        }
        [Serializable] internal sealed class Snapshot
        {
            public bool valid;
            public int capacity;
            public long invalid, staleEvents, unobservedEvents, threadMismatches, batches, samples, ticks, metadataBatches, cachedPages, noVictim, waitingVelocity;
            public Timing[] stages;
            public Flow[] flows;
            public string[] stageNames, flowNames;
        }
        private sealed class Request { internal long owner, generation, sequence, requested, callback; internal int phase; }
        private readonly Request[,] requests = new Request[32, 2];
        private readonly long[] owners = new long[32];
        private readonly Timing[] stages = new Timing[(int)Stage.Count];
        private readonly Flow[] flows = { new Flow(), new Flow() };
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly double millisecondsPerTick;
        private long invalid, stale, unobserved, threadMismatches, batches, samples, ticks, metadataBatches, cachedPages, noVictim, waitingVelocity;
        internal FogPerformanceCounters(long frequency)
        {
            if (frequency <= 0) throw new ArgumentOutOfRangeException(nameof(frequency));
            millisecondsPerTick = 1000d / frequency;
            for (int s = 0; s < stages.Length; ++s) stages[s] = new Timing();
            for (int i = 0; i < 32; ++i) for (int k = 0; k < 2; ++k) requests[i, k] = new Request();
        }
        private bool ThreadValid()
        { if (Thread.CurrentThread.ManagedThreadId == thread) return true; Interlocked.Increment(ref threadMismatches); return false; }
        internal void Elapsed(Stage stage, long start, long end)
        {
            if (!ThreadValid()) return;
            if ((int)stage < 0 || stage >= Stage.Count || start < 0 || end < start) { ++invalid; return; }
            var item = stages[(int)stage]; double ms = (end - start) * millisecondsPerTick;
            ++item.count; item.totalMilliseconds += ms; item.maxMilliseconds = Math.Max(item.maxMilliseconds, ms);
        }
        private bool Address(int slot, Kind kind)
        { if (slot >= 0 && slot < 32 && (int)kind >= 0 && kind < Kind.Count) return true; ++invalid; return false; }
        internal void Requested(int slot, long owner, long generation, long sequence, Kind kind, long bytes, long at)
        {
            if (!ThreadValid() || !Address(slot, kind)) return;
            if (owner <= 0 || generation <= 0 || sequence < 0 || bytes <= 0 || at < 0) { ++invalid; return; }
            if (owner < owners[slot]) { ++stale; return; }
            if (owner > owners[slot])
            {
                for (int k = 0; k < 2; ++k) { if (requests[slot,k].phase != 0) ++invalid; requests[slot,k].phase = 0; }
                owners[slot] = owner;
            }
            var request = requests[slot, (int)kind];
            if (request.phase != 0) { ++invalid; return; }
            request.owner = owner; request.generation = generation; request.sequence = sequence; request.requested = at; request.phase = 1;
            ++flows[(int)kind].requested; flows[(int)kind].requestedBytes += bytes;
        }
        private Request Match(int slot, long owner, long generation, long sequence, Kind kind, int phase)
        {
            if (!Address(slot, kind)) return null;
            var request = requests[slot, (int)kind];
            if (owners[slot] == 0) { ++unobserved; return null; }
            if (request.owner != owner || request.generation != generation || request.sequence != sequence) { ++stale; return null; }
            if (request.phase != phase) { ++invalid; return null; }
            return request;
        }
        internal void Received(int slot, long owner, long generation, long sequence, Kind kind, long copiedBytes, long at)
        {
            if (!ThreadValid()) return;
            var request = Match(slot, owner, generation, sequence, kind, 1); if (request == null) return;
            if (copiedBytes < 0 || at < request.requested) { ++invalid; return; }
            var flow = flows[(int)kind]; ++flow.callbacks; flow.cpuCopiedBytes += copiedBytes; if (copiedBytes == 0) ++flow.unreadable;
            double ms = (at - request.requested) * millisecondsPerTick;
            flow.requestToCallbackMilliseconds += ms; flow.maxRequestToCallbackMilliseconds = Math.Max(flow.maxRequestToCallbackMilliseconds, ms);
            request.callback = at; request.phase = 2;
        }
        internal void Accepted(int slot, long owner, long generation, long sequence, Kind kind, bool accepted, long at)
        {
            if (!ThreadValid()) return;
            var request = Match(slot, owner, generation, sequence, kind, 2); if (request == null) return;
            if (at < request.callback) { ++invalid; return; }
            var flow = flows[(int)kind]; if (accepted) ++flow.accepted; else ++flow.rejected;
            double ms = (at - request.callback) * millisecondsPerTick;
            flow.callbackToAcceptMilliseconds += ms; flow.maxCallbackToAcceptMilliseconds = Math.Max(flow.maxCallbackToAcceptMilliseconds, ms);
            request.phase = 0;
        }
        internal void SubmissionFailed(int slot, long owner, long generation, long sequence, Kind kind)
        {
            if (!ThreadValid()) return;
            var request = Match(slot, owner, generation, sequence, kind, 1); if (request == null) return;
            ++flows[(int)kind].submissionFailures; request.phase = 0;
        }
        internal void Batch(int count, int deltaTicks, bool metadata)
        {
            if (!ThreadValid()) return;
            if (count < 1 || count > BlueprintFogInputQueue.MaximumBatchSamples || deltaTicks < 0 || deltaTicks > 8 || metadata != (deltaTicks == 0)) { ++invalid; return; }
            ++batches; samples += count; ticks += deltaTicks; if (metadata) ++metadataBatches;
        }
        internal void CachedPage() { if (ThreadValid()) ++cachedPages; }
        internal void NoVictim() { if (ThreadValid()) ++noVictim; }
        internal void WaitingVelocity() { if (ThreadValid()) ++waitingVelocity; }
        internal Snapshot Read()
        {
            if (!ThreadValid()) return null;
            var result = new Snapshot { valid = invalid == 0 && threadMismatches == 0, capacity = 32,
                invalid = invalid, staleEvents = stale, unobservedEvents = unobserved, threadMismatches = threadMismatches,
                batches = batches, samples = samples, ticks = ticks, metadataBatches = metadataBatches,
                cachedPages = cachedPages, noVictim = noVictim, waitingVelocity = waitingVelocity,
                stages = new Timing[stages.Length], flows = new Flow[flows.Length], stageNames = new string[stages.Length], flowNames = new string[flows.Length] };
            for (int s = 0; s < stages.Length; ++s) { result.stageNames[s] = ((Stage)s).ToString(); result.stages[s] = new Timing { count = stages[s].count, totalMilliseconds = stages[s].totalMilliseconds, maxMilliseconds = stages[s].maxMilliseconds }; }
            for (int k = 0; k < 2; ++k)
            {
                var f = flows[k]; var c = result.flows[k] = new Flow { requested = f.requested, callbacks = f.callbacks, accepted = f.accepted,
                    rejected = f.rejected, unreadable = f.unreadable, submissionFailures = f.submissionFailures,
                    requestedBytes = f.requestedBytes, cpuCopiedBytes = f.cpuCopiedBytes,
                    requestToCallbackMilliseconds = f.requestToCallbackMilliseconds, maxRequestToCallbackMilliseconds = f.maxRequestToCallbackMilliseconds,
                    callbackToAcceptMilliseconds = f.callbackToAcceptMilliseconds, maxCallbackToAcceptMilliseconds = f.maxCallbackToAcceptMilliseconds };
                result.flowNames[k] = ((Kind)k).ToString();
                for (int i = 0; i < 32; ++i) { if (requests[i,k].phase == 1) ++c.awaitingCallback; if (requests[i,k].phase == 2) ++c.awaitingAcceptance; }
            }
            return result;
        }
    }
}
