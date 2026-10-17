using System;

namespace OstrixMods.BuildWorks
{
    // CPU confirmation only. A receipt can confirm GPU work, never infer rollback after a readback error.
    internal sealed class BlueprintFogSnapshotState
    {
        internal enum Phase { Ready, Pending, Faulted, Retiring, Retired }
        internal enum Completion { Committed, Rejected, Unknown }
        internal readonly struct Submission
        {
            internal readonly long Owner, Generation, Sequence;
            internal readonly int Tick, EmissionRevision;
            internal Submission(long owner, long generation, long sequence, int tick, int emissionRevision)
            { Owner = owner; Generation = generation; Sequence = sequence; Tick = tick; EmissionRevision = emissionRevision; }
        }

        private readonly long owner, generation;
        private long sequence;
        private Submission? pending;
        private bool uncertain, retiring;
        internal Phase State { get; private set; } = Phase.Ready;
        internal int ConfirmedTick { get; private set; }
        internal int ConfirmedEmissionRevision { get; private set; }
        internal bool CanReleaseResources => State == Phase.Retired;
        internal bool RequiresGpuDrain => pending.HasValue || uncertain;

        internal BlueprintFogSnapshotState(long owner, long generation, int initialTick = 0)
        {
            if (owner <= 0 || generation <= 0 || initialTick < 0 || initialTick > 2592000)
                throw new ArgumentException("Fog snapshot identity/initial clock invalid.");
            this.owner = owner; this.generation = generation; ConfirmedTick = initialTick;
        }

        internal Submission Submit(int tick, int emissionRevision, bool metadataOnly = false)
        {
            if (State != Phase.Ready) throw new InvalidOperationException("Fog snapshot cannot advance before confirmation or after retirement/failure.");
            if (tick < ConfirmedTick || tick > 2592000 || tick - ConfirmedTick > 8 || emissionRevision < ConfirmedEmissionRevision ||
                (metadataOnly ? tick != ConfirmedTick || emissionRevision != ConfirmedEmissionRevision : tick == ConfirmedTick))
                throw new ArgumentException("Fog snapshot advance exceeds its bounded clock/revision.");
            var submitted = new Submission(owner, generation, checked(sequence + 1), tick, emissionRevision);
            sequence = submitted.Sequence; pending = submitted; State = Phase.Pending;
            return submitted;
        }

        internal bool Complete(Submission receipt, Completion result)
        {
            if (result != Completion.Committed && result != Completion.Rejected && result != Completion.Unknown)
                throw new ArgumentException("Unknown fog snapshot completion code.");
            if (!pending.HasValue) return false;
            Submission expected = pending.Value;
            if (receipt.Owner != expected.Owner || receipt.Generation != expected.Generation || receipt.Sequence != expected.Sequence ||
                receipt.Tick != expected.Tick || receipt.EmissionRevision != expected.EmissionRevision) return false;
            pending = null;
            if (result == Completion.Committed)
            {
                ConfirmedTick = expected.Tick; ConfirmedEmissionRevision = expected.EmissionRevision;
                State = retiring ? Phase.Retired : Phase.Ready;
            }
            else
            {
                uncertain = result == Completion.Unknown;
                State = retiring ? (uncertain ? Phase.Retiring : Phase.Retired) : Phase.Faulted;
            }
            return true;
        }

        internal bool CanRecover(int emissionRevision)
            => State == Phase.Ready && emissionRevision == ConfirmedEmissionRevision;

        internal void Retire()
        {
            if (State == Phase.Retired) return;
            retiring = true; State = RequiresGpuDrain ? Phase.Retiring : Phase.Retired;
        }

        // The whole shared pool must be quiesced and torn down; queue drain alone cannot resolve an unknown commit.
        internal void ConfirmPoolTeardown()
        {
            if (!retiring) throw new InvalidOperationException("A live fog owner cannot release its resources through teardown.");
            pending = null; uncertain = false; State = Phase.Retired;
        }

        // Full 96x80x80 v11 fields only; source/atlas/driver readback staging are separate budgets.
        internal static long EstimateFieldBytes(int owners, int blockCapacity)
        {
            const long cells = 96L * 80 * 80, ledgerBytes = 1024 * 4 * 2 + 4;
            if (owners < 1 || owners > 32 || blockCapacity < 1 || blockCapacity > 98304000 / 516)
                throw new ArgumentException("Fog sparse pool sizing invalid.");
            return cells * 8 * (9 + owners) + owners * (2 * 3 * (cells / 64) * 4 + ledgerBytes + 128) +
                ledgerBytes + 64 + blockCapacity * 516L;
        }
    }
}
