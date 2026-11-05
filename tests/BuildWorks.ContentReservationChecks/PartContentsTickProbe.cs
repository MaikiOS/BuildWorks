namespace ReservationChecks
{
    // The implementation is extracted verbatim from the production PartExecution; only its containing host is substituted.
    internal sealed partial class PartContentsTickProbe
    {
        private readonly BlueprintStorageTransfer storageTransfer;
        private BlueprintStoragePersistence storagePersistence;
        internal bool Finished { get; private set; }
        internal string Status { get; private set; }
        internal string Reason { get; private set; }
        internal PartContentsTickProbe(BlueprintStorageTransfer transfer) { storageTransfer = transfer; }
        internal void Tick() => TickContents();
        private void Refuse(string reason)
        { storageTransfer.Dispose(); Finished = true; Status = "reconciliation"; Reason = reason; }
    }
}
