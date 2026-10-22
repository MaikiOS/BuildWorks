using System;
using System.Linq;
using System.Threading;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks
{
    // Reads current native saves only; it never issues, collects, destroys or requests a save.
    internal sealed class BlueprintConstructionPaidCancellation
    {
        private readonly Plans plans;
        private readonly Journal journal;
        private readonly Journal.Record record;
        private readonly Player player;
        private readonly Inventory inventory;
        private readonly BlueprintForeignPartData before;
        private readonly Game game;
        private readonly PlayerProfile identity;
        private readonly ZNet network;
        private readonly World world;
        private readonly uint generation;
        private readonly int thread = Thread.CurrentThread.ManagedThreadId;
        private readonly string issuanceHash, profileHash, path, name, filename;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private readonly DateTime deadline = DateTime.UtcNow.AddMinutes(2);
        private readonly BlueprintStorageWorldGeneration scan;
        internal Guid Project => record.Project;
        internal string Status { get; private set; } = "reading-cancellation";
        internal string Reason { get; private set; }
        internal bool Finished => Status == "cancelled" || Status == "reconciliation";

        internal BlueprintConstructionPaidCancellation(Plans plans, Guid project)
        {
            this.plans = plans; journal = new Journal(plans); record = journal.Read(project);
            player = Player.m_localPlayer; inventory = player ? player.GetInventory() : null;
            game = Game.instance; identity = game ? game.GetPlayerProfile() : null;
            path = identity?.GetPath(); name = identity?.GetName(); filename = identity?.GetFilename();
            network = ZNet.instance; world = network ? network.GetWorld() : null; generation = SaveSystem.GetSaveNumber();
            if (record?.Phase != Journal.IssuedSaved || plans.Load(project).Cancelled || !Context() ||
                generation == 0 || generation == uint.MaxValue || generation < record.MinimumWorldGeneration ||
                record.Collections?.Any(c => c != null && (c.Phase != Journal.ProfileCredited || c.Removal?.Phase == Journal.CleanupIntent)) == true ||
                !BlueprintStorageCapture.TrySnapshotInventory(inventory, out before))
                throw new InvalidOperationException("Paid close requires saved issuance, settled existing intents and its idle private Local payer.");
            issuanceHash = Journal.IssuanceFingerprint(record);
            if (!ReadProfile(out var profile, out string error)) throw new InvalidOperationException(error);
            profileHash = profile.ProfileHash;
            var removal = new BlueprintConstructionCancellationJournal(plans).Read(project);
            var operations = removal.Parts.Where(p => p.Phase == BlueprintConstructionPartJournal.Committed).Select(p => p.Operation);
            if (!BlueprintStorageWorldGeneration.TryCaptureAbsence(record.Operation, Plans.ConstructionMarkerKey, operations,
                Custody, out scan, out error) || scan.Generation != generation || !scan.TryStart(cancellation.Token, out error))
                throw new InvalidOperationException(error ?? "Current construction absence scan could not start.");
        }

        private bool Context() => Thread.CurrentThread.ManagedThreadId == thread && player && player == Player.m_localPlayer && !player.IsDead() &&
            ReferenceEquals(player.GetInventory(), inventory) && BlueprintConstructionProjectRuntime.IsPrivateLocalPlacement &&
            game && game == Game.instance && !game.IsShuttingDown() && ReferenceEquals(game.GetPlayerProfile(), identity) && identity != null &&
            identity.m_fileSource == FileHelpers.FileSource.Local && identity.GetPlayerID() == record.Player &&
            identity.GetPath() == path && identity.GetName() == name && identity.GetFilename() == filename &&
            player.GetPlayerID() == record.Player && network && network == ZNet.instance && !network.IsSaving() &&
            ReferenceEquals(network.GetWorld(), world) && world != null && world.m_uid == record.World &&
            SaveSystem.GetSaveNumber() == generation && !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveWorld) &&
            !SaveSystem.HasSessionFlag(SaveSystemSessionFlags.DontSaveCharacter);
        private bool Custody() => Context() && BlueprintStorageCapture.TrySnapshotInventory(inventory, out var current) &&
            current.Info == before.Info && current.Zdo == before.Zdo;
        private bool ReadProfile(out NativePlayerProfileReadback.CustomDataEvidence profile, out string error)
        {
            if (!BlueprintStoragePlayerProfile.TryReadSavedCustomData(player, Custody, out profile, out error)) return false;
            for (int index = 0; index < (record.Collections?.Length ?? 0); index++)
            {
                if (record.Collections[index] == null) continue;
                string key = Journal.ReceiptKey(record.Items[index].Id), value = Journal.ReceiptValue(record, index);
                if (!profile.CustomData.TryGetValue(key, out var saved) || saved != value ||
                    !player.m_customData.TryGetValue(key, out var live) || live != value)
                { error = "Paid close requires every existing permanent credit receipt in both saved and loaded payer data."; return false; }
            }
            return true;
        }

        internal void Tick()
        {
            if (Finished) return;
            try
            {
                if (cancellation.IsCancellationRequested || DateTime.UtcNow > deadline || !Custody())
                    throw new InvalidOperationException("Paid close context changed or timed out; evidence retained.");
                if (!scan.IsCompleted) return;
                if (!scan.TryFinishAbsence(out var absence, out string error) || !ReadProfile(out var profile, out error) || profile.ProfileHash != profileHash)
                    throw new InvalidOperationException(error ?? "Payer profile changed during cancellation readback.");
                var current = journal.Read(Project);
                if (current.Revision != record.Revision || Journal.IssuanceFingerprint(current) != issuanceHash || !Custody())
                    throw new InvalidOperationException("Cancellation journals changed during readback.");
                try { plans.CancelPaid(Project, 1, record.Operation, generation, absence, profile); }
                catch
                {
                    // A reported write failure can follow a successful atomic replace; read durable state before retrying anything.
                    var terminal = plans.Load(Project);
                    if (!terminal.Cancelled || terminal.CancelOperation != record.Operation || journal.Read(Project).Terminal?.IssuanceHash != issuanceHash) throw;
                }
                Status = "cancelled";
            }
            catch (Exception error) { Refuse(error.Message); }
        }
        internal void Refuse(string reason) { cancellation.Cancel(); Reason = reason; Status = "reconciliation"; }
    }
}
