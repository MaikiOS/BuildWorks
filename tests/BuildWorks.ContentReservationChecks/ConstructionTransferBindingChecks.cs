using System;
using System.IO;
using System.Reflection;

namespace ReservationChecks
{
    internal static class ConstructionTransferBindingChecks
    {
        private static void Check(bool value, string reason) { if (!value) throw new Exception("Construction binding: " + reason); }
        internal static bool Begin(TransferChecks.Frame frame, Func<bool> current, bool paid = true)
        {
            var bindingType = typeof(BlueprintStorageTransfer).GetNestedType("ConstructionBinding", BindingFlags.NonPublic);
            var binding = Activator.CreateInstance(bindingType, BindingFlags.Instance | BindingFlags.NonPublic, null,
                new object[] { Guid.NewGuid(), Guid.NewGuid(), new string('A', 64), current }, null);
            var payment = paid ? new[] { new BlueprintStorageTransferJournal.Payment {
                Prefab = "Wood", Properties = "fixture-paid-parent", Before = 3, Paid = 1 } } : Array.Empty<BlueprintStorageTransferJournal.Payment>();
            object[] args = { frame.Operation, frame.Player, frame.Placed, frame.Recipe, frame.Supply, payment, frame.Held, binding, null, null };
            bool result = (bool)typeof(BlueprintStorageTransfer).GetMethod("TryBeginCore", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args);
            frame.Transfer = (BlueprintStorageTransfer)args[8]; frame.Error = (string)args[9];
            if (result)
            {
                frame.SavedJournalPath = frame.Transfer.JournalPath;
                frame.Placed.Container.SyncCacheForTest(); frame.Transfer.MoveNext();
            }
            return result;
        }
        internal static void Run()
        {
            var refused = new TransferChecks.Frame();
            Check(!Begin(refused, () => false) && refused.Item.m_stack == 3 && refused.Placed.Container.Inventory.MoveCalls == 0 &&
                !Directory.Exists(BlueprintStorageTransferJournal.Root), "Invalid parent caused a journal, marker or move.");
            var unpaid = new TransferChecks.Frame();
            Check(!Begin(unpaid, () => true, paid: false) && unpaid.Placed.Container.Inventory.MoveCalls == 0 &&
                !Directory.Exists(BlueprintStorageTransferJournal.Root), "Linked transfer accepted empty payment evidence.");
            bool valid = true;
            var changed = new TransferChecks.Frame(); Check(Begin(changed, () => valid), changed.Error);
            var prepared = BlueprintStorageTransferJournal.Read(changed.Transfer.JournalPath);
            Check(prepared.Schema == 6 && prepared.Prepared.HasConstructionBinding && prepared.Prepared.Paid.Length == 1,
                "Production core omitted the linked parent/payment.");
            valid = false; changed.Transfer.MoveNext();
            Check(changed.Item.m_stack == 3 && changed.Placed.Container.Inventory.MoveCalls == 0 &&
                BlueprintStorageTransferJournal.Read(changed.Transfer.JournalPath).RequiresReconciliation,
                "Changed parent moved contents or released custody.");
            var callback = new TransferChecks.Frame(); valid = true; Check(Begin(callback, () => valid), callback.Error);
            callback.Player.BeforeUnequip = () => valid = false;
            callback.Transfer.MoveNext();
            Check(callback.Item.m_stack == 3 && callback.Placed.Container.Inventory.MoveCalls == 0 &&
                BlueprintStorageTransferJournal.Read(callback.Transfer.JournalPath).RequiresReconciliation,
                "Parent invalidation during native callback was ignored.");
            var moved = new TransferChecks.Frame(); valid = true; Check(Begin(moved, () => valid), moved.Error);
            Check(moved.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred && moved.Item.m_stack == 1,
                "Valid linked core failed to transfer once.");
            Check(!BlueprintStorageTransfer.TryBeginRecoveredReturn(moved.Transfer.JournalPath, moved.Player, moved.Placed, out _, out _) &&
                moved.Item.m_stack == 1 && moved.Placed.Container.Inventory.MoveCalls == 1,
                "Legacy recovery detached a paid transfer from its construction authority.");
            valid = false;
            Check(!moved.Transfer.TryBeginPersistence(out _, out _) && !moved.Transfer.TryBeginReturn(out _, out _) &&
                moved.Item.m_stack == 1 && moved.Placed.Container.Inventory.MoveCalls == 1,
                "Changed parent authorized another save/return/move.");
            Console.WriteLine("PASS actual transfer core parent fence: linked WAL/payment, pre-effect rejection and callback/save/return invalidation. Authority callback is substituted; real Store payment is tested separately, no native authorization proof.");
        }
    }
}
