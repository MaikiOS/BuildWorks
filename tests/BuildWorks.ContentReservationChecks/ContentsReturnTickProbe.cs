using System;

namespace ReservationChecks
{
    // Actual Advance/Tick/Refuse; only plan/parent lookup and its producer are substituted.
    internal sealed partial class ContentsReturnTickProbe
    {
        private readonly object plans = new object();
        private readonly Player player = Player.m_localPlayer;
        private readonly Guid[] parts;
        private int index;
        private BlueprintStorageTransfer transfer;
        private BlueprintStoragePersistence persistence;
        private readonly Action current;
        internal Guid Project { get; } = Guid.NewGuid();
        internal bool Finished { get; private set; }
        internal string Status { get; private set; } = "returning-contents";
        internal string Reason { get; private set; }
        internal ContentsReturnTickProbe(Action current, int count = 1)
        { this.current = current; parts = new Guid[count]; Advance(); }
        private void RequireCurrent() => current();
    }
    internal sealed partial class BlueprintStorageTransfer
    {
        internal static Func<BlueprintStorageTransfer> ReturnProducer;
        internal static bool TryBeginProjectReturn(object plans, Guid project, Guid part, Player player,
            out BlueprintStorageTransfer transfer, out string reason)
        {
            transfer = null; reason = null;
            try { transfer = ReturnProducer(); return true; }
            catch (Exception error) { reason = error.Message; return false; }
        }
    }
}
