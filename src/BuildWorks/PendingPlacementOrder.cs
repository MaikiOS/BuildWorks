using System;

namespace OstrixMods.BuildWorks
{
    // Indices always address the original plan; a contact rejection never resolves a part.
    internal sealed class PendingPlacementOrder
    {
        private readonly bool[] resolved;
        internal int Current { get; private set; }
        internal int ResolvedCount { get; private set; }
        internal bool Ready { get; private set; }
        internal bool Complete => ResolvedCount == resolved.Length;

        internal PendingPlacementOrder(int count)
            : this(count > 0 ? new bool[count] : throw new ArgumentOutOfRangeException(nameof(count))) { }

        internal PendingPlacementOrder(bool[] alreadyResolved)
        {
            if (alreadyResolved == null || alreadyResolved.Length == 0)
                throw new ArgumentException("A nonempty original plan is required.", nameof(alreadyResolved));
            resolved = (bool[])alreadyResolved.Clone();
            foreach (bool value in resolved) if (value) ++ResolvedCount;
            RestartPass();
        }

        internal bool RestartPass()
        {
            Ready = false;
            Current = Array.FindIndex(resolved, value => !value);
            return Current >= 0;
        }

        internal bool DeferContact()
        {
            if (Current < 0 || Ready) throw new InvalidOperationException("No unselected candidate to defer.");
            for (int next = Current + 1; next < resolved.Length; ++next)
                if (!resolved[next]) { Current = next; return true; }
            Current = -1;
            return false;
        }

        internal void AcceptContact()
        {
            if (Current < 0 || Ready) throw new InvalidOperationException("No unselected candidate to accept.");
            Ready = true;
        }

        internal void Commit(int index)
        {
            if (!Ready || index != Current) throw new InvalidOperationException("Commit does not match the selected original part.");
            Resolve();
        }

        // Retains the existing explicit player-blocked skip policy, separately from contact deferral.
        internal void SkipPlayerBlocked(int index)
        {
            if (Current < 0 || index != Current) throw new InvalidOperationException("Skip does not match the original part.");
            Resolve();
        }

        private void Resolve()
        {
            resolved[Current] = true;
            ++ResolvedCount;
            RestartPass();
        }
    }
}
