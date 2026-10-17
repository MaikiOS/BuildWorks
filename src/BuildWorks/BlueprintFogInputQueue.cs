using System;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Keep every accepted observation until its GPU snapshot is confirmed; never replace a path with its last point.
    internal sealed class BlueprintFogInputQueue
    {
        internal const int Capacity = 64;
        internal const int MaximumBatchSamples = 16;
        internal readonly struct Sample
        {
            internal readonly float Clock, Radius, HalfHeight;
            internal readonly Point3 Actor;
            internal readonly bool Emit, EmissionPossible, Nearby;
            internal Sample(float clock, Point3 actor, float radius, float halfHeight, bool emit, bool emissionPossible, bool nearby)
            {
                Clock = clock; Actor = new Point3((float)actor.X, (float)actor.Y, (float)actor.Z);
                Radius = radius; HalfHeight = halfHeight; Emit = emit; EmissionPossible = emissionPossible; Nearby = nearby;
                Validate(this);
            }
        }
        private readonly Sample[] samples = new Sample[Capacity];
        internal BlueprintFogSnapshotState Owner { get; private set; }
        private readonly bool detachable;
        internal int Count { get; private set; }
        internal int PendingCount { get; private set; }
        internal bool PendingMetadataOnly { get; private set; }
        internal bool Faulted { get; private set; }
        internal Sample Committed { get; private set; }
        private int head;
        private float capturedClock;

        internal BlueprintFogInputQueue(BlueprintFogSnapshotState owner, Sample initial)
        {
            Validate(initial);
            if (owner == null || owner.State != BlueprintFogSnapshotState.Phase.Ready || owner.ConfirmedTick != Tick(initial.Clock))
                throw new ArgumentException("Fog input queue requires its owner's confirmed initial clock.");
            Owner = owner; Committed = initial; capturedClock = initial.Clock;
        }

        internal BlueprintFogInputQueue(Sample initial)
        {
            Validate(initial); detachable = true; Committed = initial; capturedClock = initial.Clock;
        }

        internal void Attach(BlueprintFogSnapshotState owner)
        {
            if (!detachable || Owner != null || Faulted || PendingCount != 0 || owner == null ||
                owner.State != BlueprintFogSnapshotState.Phase.Ready || owner.ConfirmedTick != Tick(Committed.Clock) || owner.ConfirmedEmissionRevision != 0)
                throw new InvalidOperationException("Fog logical input cannot attach to this generation/baseline.");
            Owner = owner;
        }

        // The pool calls this only after confirming GPU release; it never removes accepted observations.
        internal void Detach(BlueprintFogSnapshotState owner)
        {
            if (!detachable || owner == null || !ReferenceEquals(Owner, owner) || PendingCount != 0 || !owner.CanReleaseResources || owner.RequiresGpuDrain)
                throw new InvalidOperationException("Fog logical input cannot detach before confirmed retirement.");
            Owner = null;
        }

        internal void TrackDormant(Sample sample)
        {
            Validate(sample);
            if (!detachable || Owner != null || Count != 0 || Faulted || (sample.Emit && sample.EmissionPossible) ||
                sample.Clock < capturedClock || Tick(sample.Clock) - Tick(capturedClock) > 8)
                throw new InvalidOperationException("Only an empty dormant source may update its non-emitting baseline.");
            Committed = sample; capturedClock = sample.Clock;
        }

        internal bool HasEmission
        {
            get { for (int i = 0; i < Count; ++i) if (At(i).Emit && At(i).EmissionPossible) return true; return false; }
        }

        internal void Capture(Sample sample)
        {
            Validate(sample);
            if (Faulted || (Owner != null && Owner.State == BlueprintFogSnapshotState.Phase.Faulted) ||
                (!detachable && (Owner.State != BlueprintFogSnapshotState.Phase.Ready && Owner.State != BlueprintFogSnapshotState.Phase.Pending)))
                throw new InvalidOperationException("Fog input owner stopped; retain its published snapshot and accepted history.");
            if (sample.Clock < capturedClock || Tick(sample.Clock) - Tick(capturedClock) > 8)
                throw new ArgumentException("Fog observation clock must be monotonic and bounded to eight fixed steps.");
            if (Count == Capacity)
            {
                Faulted = true;
                throw new InvalidOperationException("Fog input queue is full; observation rejected, accepted history retained.");
            }
            samples[(head + Count) % Capacity] = sample; ++Count; capturedClock = sample.Clock;
        }

        internal Sample At(int index)
        {
            if (index < 0 || index >= Count) throw new ArgumentOutOfRangeException(nameof(index));
            return samples[(head + index) % Capacity];
        }

        internal bool TryBegin(out BlueprintFogSnapshotState.Submission receipt)
        {
            receipt = default;
            if (Faulted) throw new InvalidOperationException("Faulted fog input must not be replayed automatically.");
            if (PendingCount != 0 || Count == 0 || Owner == null || Owner.State != BlueprintFogSnapshotState.Phase.Ready) return false;
            if (Tick(Committed.Clock) != Owner.ConfirmedTick) throw new InvalidOperationException("Fog queue and owner clocks differ.");
            int count = 0, tick = Owner.ConfirmedTick, revision = Owner.ConfirmedEmissionRevision;
            while (count < Count && count < MaximumBatchSamples && Tick(At(count).Clock) - Owner.ConfirmedTick <= 8)
            {
                Sample next = At(count); int nextTick = Tick(next.Clock);
                if (nextTick > tick && next.Emit && next.EmissionPossible) revision = checked(revision + 1);
                tick = nextTick; ++count;
            }
            if (count == 0) throw new InvalidOperationException("Oldest fog observation exceeds the bounded batch.");
            bool metadataOnly = tick == Owner.ConfirmedTick;
            receipt = Owner.Submit(tick, revision, metadataOnly);
            PendingCount = count; PendingMetadataOnly = metadataOnly; return true;
        }

        internal bool Complete(BlueprintFogSnapshotState.Submission receipt, BlueprintFogSnapshotState.Completion result)
        {
            if (PendingCount == 0 || !Owner.Complete(receipt, result)) return false;
            if (result == BlueprintFogSnapshotState.Completion.Committed)
            {
                Committed = At(PendingCount - 1); head = (head + PendingCount) % Capacity; Count -= PendingCount;
            }
            else Faulted = true;
            PendingCount = 0; PendingMetadataOnly = false; return true;
        }

        internal bool CanObserveRecovery(int emissionRevision)
            => PendingCount == 0 && !Faulted && Owner != null && Owner.CanRecover(emissionRevision);
        internal bool CanRecover(int emissionRevision) => Count == 0 && CanObserveRecovery(emissionRevision);

        private static int Tick(float clock) => (int)Math.Floor(clock / (1f / 30) + .0001f);
        private static void Validate(Sample sample)
        {
            bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
            if (!Finite(sample.Clock) || sample.Clock < 0 || sample.Clock > 86400 || !Finite(sample.Actor.X) ||
                !Finite(sample.Actor.Y) || !Finite(sample.Actor.Z) || Math.Abs(sample.Actor.X) > 64 || Math.Abs(sample.Actor.Y) > 64 ||
                Math.Abs(sample.Actor.Z) > 64 || !Finite(sample.Radius) || !Finite(sample.HalfHeight) || sample.Radius <= 0 ||
                sample.HalfHeight < sample.Radius || sample.HalfHeight > 8)
                throw new ArgumentException("Invalid fog observation clock, local actor or body capsule.");
        }
    }
}
