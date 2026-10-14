using System;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;

namespace ReservationChecks
{
    internal static class TransferChecks
    {
        private static int assertions;
        private static void Check(bool value, string reason) { assertions++; if (!value) throw new InvalidOperationException("Transfer: " + reason); }
        internal sealed class Frame
        {
            internal static readonly List<Frame> Created = new List<Frame>();
            internal readonly Player Player = new Player();
            internal readonly GameObject Placed = new GameObject();
            internal readonly ItemDrop.ItemData Item;
            internal readonly ItemDrop.ItemData SecondItem;
            internal BlueprintItemStandPlacement.Supply Supply;
            internal readonly BlueprintForeignPartData Recipe;
            internal readonly List<BlueprintItemStandPlacement.Supply> Held = new List<BlueprintItemStandPlacement.Supply>();
            internal readonly BlueprintConstructionRefund Payment;
            internal BlueprintStorageTransfer Transfer;
            internal string Error;
            internal string SavedJournalPath;
            internal readonly Guid Operation = Guid.NewGuid();
            internal BlueprintStorageTransferJournal Journal => (BlueprintStorageTransferJournal)typeof(BlueprintStorageTransfer)
                .GetField("journal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(Transfer);
            internal Frame(int sourceAmount = 3, int requested = 2, bool second = false)
            {
                Created.Add(this);
                BepInEx.Paths.ConfigPath = System.IO.Path.Combine(System.IO.Directory.GetCurrentDirectory(), "artifacts/tj", Guid.NewGuid().ToString("N").Substring(0, 8));
                Game.instance = new Game(); ZNet.instance = new ZNet(); ZDOMan.instance = new ZDOMan();
                ReservationChecks.Player.m_localPlayer = Player; ReservationChecks.Player.m_localPlayerExists = true; InventoryGui.Visible = false; PrivateArea.Allowed = true;
                Placed.Piece = new Piece { gameObject = Placed }; Placed.Container = new Container(3);
                Item = Program.Item("Wood", sourceAmount, "provided"); Item.m_quality = 2; Item.m_durability = 7.53f; Item.m_gridPos = new Vector2i(5, 4);
                Player.Inventory.Items.Add(Item);
                var archived = Program.Item("Wood", requested, "archive-admin"); archived.m_quality = 3; archived.m_durability = 999;
                if (second)
                {
                    SecondItem = Program.Item("Stone", 1, "second-provided"); SecondItem.m_gridPos = new Vector2i(6, 4);
                    Player.Inventory.Items.Add(SecondItem);
                    Recipe = Program.Container(3, archived, Program.Item("Stone", 1, "second-archive"));
                }
                else Recipe = Program.Container(3, archived);
                var supplies = new List<BlueprintItemStandPlacement.Supply>();
                Check(BlueprintItemStandPlacement.SelectSupplies(Player, new[] { Recipe }, supplies), "fixture reservation failed");
                Supply = supplies[0]; Held.Add(Supply); Payment = new BlueprintConstructionRefund(Player.Inventory);
            }
            internal bool Begin(bool synchronizeMarker = true)
            {
                bool ready = BlueprintStorageTransfer.TryBegin(Operation, Player, Placed, Recipe, Supply, Payment, Held, out Transfer, out Error);
                if (ready) { SavedJournalPath = Transfer.JournalPath; if (synchronizeMarker) { Placed.Container.SyncCacheForTest(); Transfer.MoveNext(); } }
                return ready;
            }
        }
        internal static void Run()
        {
            var normal = new Frame(); normal.Item.m_equipped = true; normal.Player.Equipped = normal.Item; normal.Player.Queue.Add(normal.Item);
            // Explicit re-preparation after changing the equipped flag keeps the immutable source binding honest.
            normal.Supply = new BlueprintItemStandPlacement.Supply(new List<BlueprintItemStandPlacement.Reservation> {
                new BlueprintItemStandPlacement.Reservation(normal.Item, 2, BlueprintItemStandPlacement.SnapshotItem(normal.Item)) });
            normal.Held[0] = normal.Supply;
            Check(normal.Begin(), normal.Error);
            normal.Player.BeforeUnequip = () => { var pending = BlueprintStorageTransferJournal.Read(normal.Transfer.JournalPath);
                Check(pending.InFlight == 0 && pending.Pending.Equipped && pending.Pending.Queued && pending.Pending.SourceBefore == 3 &&
                    pending.Prepared.Player == 100 && pending.Prepared.TargetUser == 99 && pending.Prepared.TargetId == 1 && pending.RequiresReconciliation,
                    "write-ahead attempt was not readable before native unequip"); };
            Check(normal.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && normal.Transfer.CompleteInMemory, "normal native-style move failed");
            var moved = normal.Transfer.Entries[0];
            var persisted = BlueprintStorageTransferJournal.Read(normal.Transfer.JournalPath);
            Check(persisted.Terminal && persisted.Observations.Count == 1 && persisted.Observations[0].SourceAfter == 1 &&
                persisted.Observations[0].DestinationAfter == 2 && persisted.Observations[0].DestinationProperties == moved.ProvidedProperties &&
                persisted.RequiresReconciliation && persisted.InFlight == null, "exact custody observation/terminal was not persisted");
            Check(BlueprintStorageTransferJournal.HasUnresolved(1) && !BlueprintStorageTransferJournal.HasUnresolved(2),
                "unresolved target guard lost world identity");
            normal.Journal.Dispose();
            JournalChecks.Run(normal.Transfer.JournalPath);
            Check(normal.Item.m_stack == 1 && moved.DestinationAfter == 2 && moved.DestinationItem.m_quality == 2 &&
                moved.DestinationItem.m_durability == 7.53f && moved.DestinationItem.Custom == "provided" &&
                moved.DestinationItem.m_gridPos.x == 0 && moved.QueuedBefore && moved.EquippedBefore &&
                !moved.QueuedAfter && !moved.EquippedAfter && moved.LocalSaveConfirmed, "own properties/equip/cell/quantity receipt wrong");
            normal.Transfer.MoveNext(); Check(normal.Placed.Container.Inventory.MoveCalls == 1, "completed move replayed");
            var multiple = new Frame(second: true); Check(multiple.Begin(), multiple.Error);
            Check(multiple.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && !multiple.Transfer.CompleteInMemory &&
                multiple.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && multiple.Transfer.CompleteInMemory &&
                multiple.Placed.Container.Inventory.Items.Count == 2, "multiple provided entries did not settle separately");
            var alteredPrevious = new Frame(second: true); Check(alteredPrevious.Begin(), alteredPrevious.Error);
            Check(alteredPrevious.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "first entry setup");
            alteredPrevious.Placed.Container.Inventory.AfterCredit = () => alteredPrevious.Placed.Container.Inventory.Items[0].m_durability = 999;
            Check(alteredPrevious.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && !alteredPrevious.Transfer.CompleteInMemory,
                "later callback repaired an earlier provided item unnoticed");

            foreach (bool nativeReturn in new[] { false, true })
            {
                var unchanged = new Frame(); Check(unchanged.Begin(), unchanged.Error);
                unchanged.Placed.Container.Inventory.NoChange = true; unchanged.Placed.Container.Inventory.ReturnFalse = !nativeReturn;
                Check(unchanged.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && unchanged.Item.m_stack == 3 &&
                    unchanged.Placed.Container.Inventory.Items.Count == 0, "native bool treated as custody proof");
                unchanged.Transfer.MoveNext(); Check(unchanged.Placed.Container.Inventory.MoveCalls == 1, "unchanged attempt automatically replayed");
            }
            var falseAfter = new Frame(); Check(falseAfter.Begin(), falseAfter.Error); falseAfter.Placed.Container.Inventory.ReturnFalse = true;
            Check(falseAfter.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && falseAfter.Transfer.Entries[0].NativeReturn == false,
                "exact observed move was discarded solely because native bool was false");

            foreach (bool afterSave in new[] { false, true })
            {
                var thrown = new Frame(2); Check(thrown.Begin(), thrown.Error);
                Action fail = () => throw new InvalidOperationException("after mutation");
                if (afterSave) thrown.Placed.Container.Inventory.AfterSave = fail; else thrown.Placed.Container.Inventory.AfterCredit = fail;
                Check(thrown.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain, "throw after debit+credit accepted as unchanged/success");
                var receipt = thrown.Transfer.Entries[0];
                Check(receipt.SourceAfter == 0 && receipt.SourcePresentAfter && receipt.DestinationAfter == 2 &&
                    receipt.LocalSaveConfirmed == afterSave && receipt.BeforeObserved, "zero-stack ghost or separate destination save was not observed");
                thrown.Transfer.MoveNext(); Check(thrown.Placed.Container.Inventory.MoveCalls == 1 && thrown.Placed.Container.Inventory.Items.Count == 1,
                    "uncertain move replayed or duplicated items");
            }
            var sourceThrow = new Frame(2); Check(sourceThrow.Begin(), sourceThrow.Error);
            sourceThrow.Player.Inventory.Changed = () => throw new InvalidOperationException("source callback");
            Check(sourceThrow.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && !sourceThrow.Player.Inventory.ContainsItem(sourceThrow.Item) &&
                sourceThrow.Transfer.Entries[0].NativeReturn == null && sourceThrow.Transfer.Entries[0].Error != null,
                "source callback exception hid exact in-memory custody");
            var partial = new Frame(); Check(partial.Begin(), partial.Error); partial.Placed.Container.Inventory.PartialAmount = 1;
            Check(partial.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && partial.Transfer.Entries[0].SourceAfter == 2 &&
                partial.Transfer.Entries[0].DestinationAfter == 1, "partial native mutation treated as complete");
            var repaired = new Frame(); Check(repaired.Begin(), repaired.Error);
            repaired.Placed.Container.Inventory.AfterCredit = () => repaired.Placed.Container.Inventory.Items[0].m_durability = 999;
            Check(repaired.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain, "unexpected repair accepted");
            var enlarged = new Frame(); Check(enlarged.Begin(), enlarged.Error);
            enlarged.Placed.Container.Inventory.AfterCredit = () => enlarged.Placed.Container.Inventory.Items[0].Custom = new string('x', 2000);
            Check(enlarged.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && enlarged.Transfer.JournalError == null,
                "unexpected large mutation exhausted reserved journal space");
            var enlargedEvidence = BlueprintStorageTransferJournal.Read(enlarged.Transfer.JournalPath);
            Check(enlargedEvidence.Observations[0].DestinationProperties.StartsWith("sha256:") &&
                enlargedEvidence.Prepared.Items[0].Properties == enlarged.Transfer.Entries[0].ProvidedProperties && enlargedEvidence.Observations[0].State == 3,
                "oversized unexpected properties were presented as full preserved or exact success");

            foreach (string fault in new[] { "owner", "world", "revision", "save", "extra-debit" })
            {
                var broken = new Frame(); Check(broken.Begin(), broken.Error);
                if (fault == "owner") broken.Placed.Container.Inventory.AfterCredit = () => broken.Placed.Container.View.Owner = false;
                if (fault == "world") broken.Placed.Container.Inventory.AfterCredit = () => ZNet.instance = new ZNet();
                if (fault == "revision") broken.Placed.Container.Inventory.AfterSave = () => broken.Placed.Container.MakeRevisionStale();
                if (fault == "save") broken.Placed.Container.Inventory.AfterSave = () => broken.Placed.Container.CaptureAllowed = false;
                if (fault == "extra-debit") broken.Placed.Container.Inventory.AfterCredit = () => broken.Item.m_stack--;
                Check(broken.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && !broken.Transfer.CompleteInMemory, "unverified " + fault + " accepted");
            }
            foreach (string denial in new[] { "payment", "payment-error", "quest", "loading", "in-use", "shutdown", "ward", "creator", "ui", "access", "wrong-inventory", "no-local-player", "future-reservation" })
            {
                var denied = new Frame();
                if (denial == "payment") denied.Payment.Recorded = false;
                if (denial == "payment-error") denied.Payment.Error = "unverified debit";
                if (denial == "quest") denied.Item.m_shared.m_questItem = true;
                if (denial == "loading") denied.Placed.Container.SetLoading(true);
                if (denial == "in-use") denied.Placed.Container.InUse = true;
                if (denial == "shutdown") Game.instance.ShuttingDown = true;
                if (denial == "ward") PrivateArea.Allowed = false;
                if (denial == "creator") denied.Placed.Piece.Creator = 999;
                if (denial == "ui") InventoryGui.Visible = true;
                if (denial == "access") denied.Placed.Container.Allowed = false;
                if (denial == "wrong-inventory") denied.Player.Inventory = new Inventory();
                if (denial == "no-local-player") Player.m_localPlayerExists = false;
                if (denial == "future-reservation") denied.Held.Add(new BlueprintItemStandPlacement.Supply(new List<BlueprintItemStandPlacement.Reservation> {
                    new BlueprintItemStandPlacement.Reservation(denied.Item, 2, BlueprintItemStandPlacement.SnapshotItem(denied.Item)) }));
                Check(!denied.Begin() && denied.Item.m_stack == 3 && denied.Placed.Container.Inventory.MoveCalls == 0, "preflight denial failed: " + denial);
            }
            var stale = new Frame(); Check(stale.Begin(), stale.Error); stale.Item.Custom = "changed";
            Check(stale.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && stale.Placed.Container.Inventory.MoveCalls == 0,
                "changed source properties transferred");
            foreach (string changedTarget in new[] { "extra", "revision", "raw" })
            {
                var changed = new Frame(); Check(changed.Begin(), changed.Error);
                if (changedTarget == "extra") { var extra = Program.Item("Stone", 1); extra.m_gridPos = new Vector2i(2, 0); changed.Placed.Container.Inventory.Items.Add(extra); changed.Placed.Container.SaveForTest(); }
                if (changedTarget == "revision") changed.Placed.Container.View.Data.DataRevision++;
                if (changedTarget == "raw") changed.Placed.Container.View.Data.Raw = new byte[] { 0 };
                Check(changed.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && changed.Placed.Container.Inventory.MoveCalls == 0 && changed.Item.m_stack == 3,
                    "changed or stale target mutated inventory: " + changedTarget);
            }
            var beforeMove = new Frame(); Check(beforeMove.Begin(), beforeMove.Error);
            beforeMove.Placed.Container.Inventory.BeforeMove = () => throw new InvalidOperationException("before native mutation");
            Check(beforeMove.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && beforeMove.Item.m_stack == 3 &&
                beforeMove.Placed.Container.Inventory.Items.Count == 0, "exception before mutation consumed items");
            beforeMove.Transfer.MoveNext(); Check(beforeMove.Placed.Container.Inventory.MoveCalls == 1, "pre-mutation exception automatically replayed");
            var occupied = new Frame(); Check(occupied.Begin(), occupied.Error); occupied.Placed.Container.Inventory.Items.Add(Program.Item("Stone", 1));
            Check(occupied.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && occupied.Placed.Container.Inventory.MoveCalls == 0,
                "occupied cell was merged into");
            var unequip = new Frame(); Check(unequip.Begin(), unequip.Error);
            unequip.Player.BeforeUnequip = () => throw new InvalidOperationException("unequip callback");
            Check(unequip.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && unequip.Transfer.Entries[0].Attempted &&
                unequip.Placed.Container.Inventory.MoveCalls == 0, "pre-move exception invoked transfer");
            var changedView = new Frame(); Check(changedView.Begin(), changedView.Error); changedView.Placed.Container.ReplaceView();
            Check(changedView.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && changedView.Placed.Container.Inventory.MoveCalls == 0,
                "replaced native view accepted");
            var removal = new Frame();
            Check(BlueprintStorageCapture.CanRemoveEmpty(removal.Placed, removal.Player), "verified empty chest cannot be cancelled");
            removal.Placed.Container.Inventory.Items.Add(Program.Item("Wood", 1));
            Check(!BlueprintStorageCapture.CanRemoveEmpty(removal.Placed, removal.Player), "filled chest can be destroyed/refunded through cancellation");
            removal.Placed.Container.Inventory.Items.Clear(); removal.Placed.Container.CaptureAllowed = false;
            Check(!BlueprintStorageCapture.CanRemoveEmpty(removal.Placed, removal.Player), "unsynced empty chest allowed for destructive fallback");
            removal.Placed.Container.CaptureAllowed = true; removal.Placed.Container.View.Owner = false;
            Check(!BlueprintStorageCapture.CanRemoveEmpty(removal.Placed, removal.Player), "foreign owner allowed to remove chest");
            var beforeFailure = new Frame(); Check(beforeFailure.Begin(), beforeFailure.Error); beforeFailure.Journal.Dispose();
            Check(beforeFailure.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && beforeFailure.Transfer.JournalError != null &&
                beforeFailure.Placed.Container.Inventory.MoveCalls == 0 && beforeFailure.Item.m_stack == 3 && !beforeFailure.Transfer.Entries[0].Attempted,
                "journal write failure before native call consumed items");
            var oversizedSource = new Frame(); oversizedSource.Item.m_stack = ushort.MaxValue + 1;
            Check(!oversizedSource.Begin() && oversizedSource.Placed.Container.Inventory.MoveCalls == 0, "unserializable native source count was accepted");
            var afterFailure = new Frame(); Check(afterFailure.Begin(), afterFailure.Error);
            afterFailure.Placed.Container.Inventory.AfterSave = () => afterFailure.Journal.Dispose();
            Check(afterFailure.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Uncertain && afterFailure.Transfer.JournalError != null &&
                afterFailure.Item.m_stack == 1 && afterFailure.Placed.Container.Inventory.Items.Count == 1, "observation write failure hid custody");
            var interrupted = BlueprintStorageTransferJournal.Read(afterFailure.Transfer.JournalPath);
            Check(interrupted.InFlight == 0 && interrupted.Observations.Count == 0 && interrupted.RequiresReconciliation && !interrupted.Terminal,
                "interrupted write was treated as completed or replayable");
            afterFailure.Transfer.MoveNext(); Check(afterFailure.Placed.Container.Inventory.MoveCalls == 1, "journal failure replayed native move");
            afterFailure.Placed.Container.Inventory.Items.Clear(); afterFailure.Placed.Container.SaveForTest();
            Check(!BlueprintStorageCapture.CanRemoveEmpty(afterFailure.Placed, afterFailure.Player), "empty target with unresolved journal could be destroyed");
            var identity = new Frame(); Check(identity.Begin(), identity.Error); var first = identity.Transfer;
            Check(!identity.Begin() && identity.Item.m_stack == 3 && identity.Placed.Container.Inventory.MoveCalls == 0,
                "existing operation/target was prepared again"); first.Dispose();
            foreach (Frame frame in Frame.Created)
                if (frame.SavedJournalPath != null) Check(BlueprintStorageTransferJournal.Read(frame.SavedJournalPath).RequiresReconciliation,
                    "one false/exception/context/failure case produced unreadable or replayable evidence");
            ReturnChecks.Run();
            RecoveryChecks.Run();
            ProfileChecks.Run();
            WorldGenerationChecks.Run();
            PersistenceChecks.Run();
            Console.WriteLine("PASS " + assertions + " transfer assertions: actual adapter, inert native/capture services; bool/exception custody, no replay, own properties, equip, payment/context guards. No game/native API/server/disk proof.");
        }
    }
}
