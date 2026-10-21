using System;
using System.Threading;
using OstrixMods.BuildWorks;

namespace OstrixMods.BuildWorks.GeometryTests
{
    internal static class FogPerformanceChecks
    {
        private static int checks;
        private static void Check(bool ok, string reason) { ++checks; if (!ok) throw new InvalidOperationException(reason); }
        internal static void Run()
        {
            bool refused = false; try { new FogPerformanceCounters(0); } catch (ArgumentOutOfRangeException) { refused = true; }
            Check(refused, "Invalid clock frequency accepted.");
            var c = new FogPerformanceCounters(1000);
            c.ReceiptReadback(2048);
            Check(c.Read().physicalReceiptRequests == 1 && c.Read().physicalReceiptBytes == 2048 && c.Read().flows[0].requested == 0,
                "Physical readback counters were confused with logical owner receipts.");
            var receipt = FogPerformanceCounters.Kind.Receipt; var velocity = FogPerformanceCounters.Kind.Velocity;
            c.Elapsed(FogPerformanceCounters.Stage.Submit, 100, 200); c.Elapsed(FogPerformanceCounters.Stage.SolverSubmission, 120, 180);
            c.Elapsed(FogPerformanceCounters.Stage.Submit, 210, 230);
            c.Requested(0, 1, 4, 7, receipt, 2048, 100); c.Requested(0, 1, 4, 7, velocity, 4915200, 101);
            var first = c.Read(); Check(first.valid && first.capacity == 32 && first.flows[0].awaitingCallback == 1 && first.flows[1].awaitingCallback == 1, "Request kinds collided.");
            c.Received(0, 1, 4, 7, receipt, 64, 130); c.Received(0, 1, 4, 7, velocity, 4915200, 150);
            c.Accepted(0, 1, 4, 7, receipt, true, 140); c.Accepted(0, 1, 4, 7, velocity, true, 160);
            c.Batch(8, 8, false); c.Batch(1, 0, true); c.CachedPage(); c.NoVictim(); c.WaitingVelocity();
            var s = c.Read();
            Check(s.valid && s.stages[0].count == 2 && s.stages[0].totalMilliseconds == 120 && s.stages[0].maxMilliseconds == 100 && s.stages[1].totalMilliseconds == 60, "Overlapping intervals corrupted timing.");
            Check(s.flows[0].requested == 1 && s.flows[0].callbacks == 1 && s.flows[0].accepted == 1 && s.flows[0].requestedBytes == 2048 && s.flows[0].cpuCopiedBytes == 64, "Request bytes were confused with owner copy.");
            Check(s.flows[0].requestToCallbackMilliseconds == 30 && s.flows[0].callbackToAcceptMilliseconds == 10 && s.flows[1].requestToCallbackMilliseconds == 49, "Callback and acceptance waits were conflated.");
            Check(s.batches == 2 && s.samples == 9 && s.ticks == 8 && s.metadataBatches == 1 && s.cachedPages == 1 && s.noVictim == 1 && s.waitingVelocity == 1, "Batch/admission counters differ.");
            s.flows[0].requested = 999; s.stages[0].count = 999;
            Check(c.Read().flows[0].requested == 1 && c.Read().stages[0].count == 2 && first.flows[0].callbacks == 0, "Read snapshot aliases live counters.");
            c.Requested(0, 1, 4, 8, receipt, 2048, 200); c.SubmissionFailed(0, 1, 4, 8, receipt);
            c.Requested(0, 1, 4, 9, receipt, 2048, 220); c.Received(0, 1, 4, 9, receipt, 0, 230); c.Accepted(0, 1, 4, 9, receipt, false, 240);
            s = c.Read(); Check(s.valid && s.flows[0].requested == 3 && s.flows[0].submissionFailures == 1 && s.flows[0].unreadable == 1 && s.flows[0].rejected == 1 && s.flows[0].awaitingCallback == 0, "Failure became successful acceptance.");
            c.Requested(0, 2, 5, 1, receipt, 2048, 300);
            c.Received(0, 1, 4, 9, receipt, 64, 310); c.Received(0, 2, 4, 1, receipt, 64, 310);
            s = c.Read(); Check(s.valid && s.staleEvents == 2 && s.flows[0].awaitingCallback == 1 && s.flows[0].callbacks == 2, "Old owner/generation altered new request.");
            c.Received(0, 2, 5, 1, receipt, 64, 320); c.Accepted(0, 2, 5, 1, receipt, true, 330);
            var bad = new FogPerformanceCounters(1000);
            bad.Requested(0, 1, 1, 1, receipt, 1, 10); bad.Received(0, 1, 1, 1, receipt, 1, 9);
            Check(!bad.Read().valid && bad.Read().invalid == 1 && bad.Read().flows[0].callbacks == 0, "Backwards timestamp accepted.");
            bad = new FogPerformanceCounters(1000); bad.Requested(32, 1, 1, 1, receipt, 1, 0); bad.Requested(-1, 1, 1, 1, receipt, 1, 0);
            bad.Elapsed((FogPerformanceCounters.Stage)99, 0, 1); bad.Batch(17, 1, false); bad.Batch(1, 0, false);
            Check(!bad.Read().valid && bad.Read().invalid == 5, "Invalid bounded observations throw or silently pass.");
            bad = new FogPerformanceCounters(1000); bad.Requested(31, 1, 1, 1, receipt, 1, 0); bad.Requested(31, 1, 1, 2, receipt, 1, 1);
            bad.Accepted(31, 1, 1, 1, receipt, true, 2);
            Check(!bad.Read().valid && bad.Read().invalid == 2 && bad.Read().flows[0].accepted == 0, "Duplicate submission/accept before callback accepted.");
            var physical = new FogPerformanceCounters(1000);
            physical.ReceiptReadback(0); physical.ReceiptReadback(63); physical.ReceiptReadback(2049);
            Check(!physical.Read().valid && physical.Read().invalid == 3 && physical.Read().physicalReceiptRequests == 0,
                "Invalid physical readback sizes were counted.");
            var unseen = new FogPerformanceCounters(1000); unseen.Received(0, 1, 1, 1, receipt, 1, 1);
            Check(unseen.Read().unobservedEvents == 1 && unseen.Read().flows[0].callbacks == 0, "Preexisting request latency invented.");
            var thread = new Thread(() => unseen.Elapsed(FogPerformanceCounters.Stage.Submit, 0, 1)); thread.Start(); thread.Join();
            Check(!unseen.Read().valid && unseen.Read().threadMismatches == 1 && unseen.Read().stages[0].count == 0, "Foreign thread mutated timings.");
            var bounded = new FogPerformanceCounters(1000); bounded.Elapsed(FogPerformanceCounters.Stage.Upload, 1, 2);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 100000; ++i) bounded.Elapsed(FogPerformanceCounters.Stage.Upload, 1, 2);
            long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
            Check(bytes < 1024 && bounded.Read().stages[(int)FogPerformanceCounters.Stage.Upload].count == 100001, "Observer retains unbounded event history.");
            Console.WriteLine("PASS fog performance observer: " + checks + " actual-source CPU checks; timestamps synthetic, no GPU timing proof.");
        }
    }
}
