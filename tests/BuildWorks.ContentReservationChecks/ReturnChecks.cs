using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace ReservationChecks
{
    internal static class ReturnChecks
    {
        private static int assertions;
        private static void Check(bool value, string reason)
        { assertions++; if (!value) throw new InvalidOperationException("Return: " + reason); }
        private static TransferChecks.Frame Delivered(bool second = false, bool partial = false)
        {
            var frame = new TransferChecks.Frame(second: second);
            Check(frame.Begin(), frame.Error);
            Check(frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "forward setup failed");
            if (second && !partial) Check(frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "second forward setup failed");
            return frame;
        }
        private static BlueprintStorageTransfer Begin(TransferChecks.Frame frame)
        {
            Check(frame.Transfer.TryBeginReturn(out var result, out string error), error);
            Check(frame.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && !frame.Transfer.CompleteInMemory, "parent was not frozen");
            Check(!frame.Transfer.TryBeginReturn(out _, out _), "second child permitted");
            var record = BlueprintStorageTransferJournal.Read(result.JournalPath);
            Check(record.Schema == 4 && record.Prepared.Parent == frame.Operation && record.Prepared.Route == BlueprintStorageTransferJournal.Direction.ToPlayer &&
                record.Prepared.Operation == BlueprintStorageTransferJournal.ReturnOperationId(frame.Operation) && record.Prepared.Paid.Length == 0,
                "inverse direction/identity/payment evidence wrong");
            return result;
        }
        private static BlueprintStorageTransferJournal Writer(BlueprintStorageTransfer transfer) => (BlueprintStorageTransferJournal)typeof(BlueprintStorageTransfer)
            .GetField("journal", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(transfer);

        internal static void Run()
        {
            var normal = Delivered(); var item = normal.Placed.Container.Inventory.Items[0];
            item.m_durability = 1.25f; item.Custom = "current-damaged"; normal.Placed.Container.SaveForTest();
            string normalReturnPath;
            using (var inverse = Begin(normal))
            {
                normalReturnPath = inverse.JournalPath;
                var plan = BlueprintStorageTransferJournal.Read(inverse.JournalPath).Prepared;
                using (var invalid = BlueprintStorageTransferJournal.Begin(Path.Combine(BepInEx.Paths.ConfigPath, "invalid-attempt"), plan))
                {
                    var row = plan.Items[0]; bool refused = false;
                    try { invalid.BeforeMove(new BlueprintStorageTransferJournal.Attempt { Index = 0, SourceBefore = 3,
                        SourceX = row.SourceX, SourceY = row.SourceY, EmptyCell = true, SourceSnapshot = row.SourceSnapshot, Properties = row.Properties,
                        SourceManifest = new string('A', 64), DestinationManifest = new string('B', 64), ZdoHash = new string('C', 64) }); }
                    catch (InvalidDataException) { refused = true; }
                    Check(refused && !BlueprintStorageTransferJournal.Read(invalid.Path).InFlight.HasValue, "journal writer accepted partial return as full");
                }
                normal.Player.BeforeUnequip = () => Check(BlueprintStorageTransferJournal.Read(inverse.JournalPath).InFlight == 0, "inverse write-ahead missing");
                Check(inverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred && inverse.CompleteInMemory, "native-style return failed");
                var returned = inverse.Entries[0].DestinationItem;
                Check(normal.Item.m_stack == 1 && returned.m_stack == 2 && returned.m_durability == 1.25f && returned.Custom == "current-damaged" &&
                    !ReferenceEquals(returned, item) && !ReferenceEquals(returned, normal.Item) && !normal.Placed.Container.Inventory.Items.Any(),
                    "return merged, repaired, lost or reconstructed the wrong item");
                Check(BlueprintStorageTransferJournal.HasUnresolved(1) && !BlueprintStorageCapture.CanRemoveEmpty(normal.Placed, normal.Player),
                    "local return released destructive cleanup/refund");
                Check(BlueprintStorageTransferJournal.Read(inverse.JournalPath).RequiresReconciliation, "return claimed durable completion");
                inverse.MoveNext(); Check(normal.Player.Inventory.MoveCalls == 1, "return replayed");
            }
            RejectPartialReturnReader(normalReturnPath);
            foreach (bool partial in new[] { false, true })
            {
                var multiple = Delivered(second: true, partial: partial);
                using (var inverse = Begin(multiple))
                {
                    Check(inverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "first multi return failed");
                    if (!partial) Check(inverse.MoveNext() == BlueprintStorageTransfer.Result.Transferred, "second multi return failed");
                    Check(inverse.CompleteInMemory && multiple.Player.Inventory.Items.Sum(value => value.m_stack) == 4 &&
                        multiple.Placed.Container.Inventory.Items.Count == 0, "partial/multi cancellation lost custody");
                }
            }
            foreach (string fault in new[] { "quantity", "max-stack", "reference", "cell", "prefab", "quest", "equip", "queue", "extra", "stale", "raw", "access", "world", "gui", "full", "grid" })
            {
                var frame = Delivered(); var delivered = frame.Placed.Container.Inventory.Items[0];
                switch (fault)
                {
                    case "quantity": delivered.m_stack++; break;
                    case "max-stack": delivered.m_shared.m_maxStackSize = 1; break;
                    case "reference": frame.Placed.Container.Inventory.Items[0] = delivered.Clone(); break;
                    case "cell": delivered.m_gridPos.x++; break;
                    case "prefab": delivered.m_dropPrefab = new Prefab { name = "Stone" }; break;
                    case "quest": delivered.m_shared.m_questItem = true; break;
                    case "equip": frame.Player.Equipped = delivered; break;
                    case "queue": frame.Player.Queue.Add(delivered); break;
                    case "extra": var extra = Program.Item("Stone", 1); extra.m_gridPos = new Vector2i(2, 0); frame.Placed.Container.Inventory.Items.Add(extra); break;
                    case "access": frame.Placed.Container.Allowed = false; break;
                    case "world": ZNet.instance.World.m_uid++; break;
                    case "gui": InventoryGui.Visible = true; break;
                    case "full": frame.Player.Inventory.Width = 1; frame.Player.Inventory.Height = 1; frame.Item.m_gridPos = new Vector2i(0, 0); break;
                    case "grid": var duplicate = Program.Item("Stone", 1); duplicate.m_gridPos = frame.Item.m_gridPos; frame.Player.Inventory.Items.Add(duplicate); break;
                }
                frame.Placed.Container.SaveForTest();
                if (fault == "stale") frame.Placed.Container.MakeRevisionStale();
                if (fault == "raw") frame.Placed.Container.View.Data.Raw = null;
                Check(!frame.Transfer.TryBeginReturn(out _, out _) && frame.Player.Inventory.MoveCalls == 0 && delivered.m_stack >= 2,
                    "return preflight accepted " + fault);
            }
            foreach (string fault in new[] { "properties", "max-stack", "source-cell", "receiver", "future", "grid", "queued", "callback-future", "callback-receiver", "callback-max" })
            {
                var frame = Delivered(second: true); using (var inverse = Begin(frame))
                {
                    var delivered = frame.Placed.Container.Inventory.Items[0];
                    switch (fault)
                    {
                        case "properties": delivered.m_durability--; break;
                        case "max-stack": delivered.m_shared.m_maxStackSize = 1; break;
                        case "source-cell": delivered.m_gridPos.x = 2; break;
                        case "receiver": frame.Item.Custom = "changed"; break;
                        case "future": frame.Placed.Container.Inventory.Items[1].Custom = "changed"; break;
                        case "grid": frame.Player.Inventory.Width--; break;
                        case "queued": frame.Player.Queue.Add(delivered); break;
                        case "callback-future": frame.Player.BeforeUnequip = () => { frame.Placed.Container.Inventory.Items[1].Custom = "changed"; frame.Placed.Container.SaveForTest(); }; break;
                        case "callback-receiver": frame.Player.BeforeUnequip = () => frame.Item.Custom = "changed"; break;
                        case "callback-max": frame.Player.BeforeUnequip = () => delivered.m_shared.m_maxStackSize = 1; break;
                    }
                    frame.Placed.Container.SaveForTest();
                    Check(inverse.MoveNext() != BlueprintStorageTransfer.Result.Transferred && frame.Player.Inventory.MoveCalls == 0,
                        "rebound changed return inventory: " + fault);
                    Check(BlueprintStorageTransferJournal.Read(inverse.JournalPath).RequiresReconciliation, "denied return lost readable evidence");
                }
            }
            foreach (string fault in new[] { "false", "before", "after-credit", "source-save", "after-source-save", "receiver-properties", "receiver-grid", "before-journal", "after-journal" })
            {
                var frame = Delivered(); using (var inverse = Begin(frame))
                {
                    var receiver = frame.Player.Inventory;
                    switch (fault)
                    {
                        case "false": receiver.NoChange = true; receiver.ReturnFalse = true; break;
                        case "before": receiver.BeforeMove = () => throw new InvalidOperationException("before"); break;
                        case "after-credit": receiver.AfterCredit = () => throw new InvalidOperationException("after credit"); break;
                        case "source-save": frame.Placed.Container.Inventory.Changed = () => throw new InvalidOperationException("source save"); break;
                        case "after-source-save": frame.Placed.Container.Inventory.Changed = () => { frame.Placed.Container.SaveForTest(); throw new InvalidOperationException("after source save"); }; break;
                        case "receiver-properties": receiver.AfterCredit = () => frame.Item.m_durability = 999; break;
                        case "receiver-grid": receiver.AfterCredit = () => receiver.Height = 0; break;
                        case "before-journal": Writer(inverse).Dispose(); break;
                        case "after-journal": receiver.AfterCredit = () => Writer(inverse).Dispose(); break;
                    }
                    var result = inverse.MoveNext();
                    var expected = fault == "false" || fault == "before" ? BlueprintStorageTransfer.Result.Unchanged :
                        fault == "after-source-save" ? BlueprintStorageTransfer.Result.Transferred : BlueprintStorageTransfer.Result.Uncertain;
                    Check(result == expected, "inverse custody classification wrong: " + fault + " -> " + result);
                    int moves = receiver.MoveCalls; inverse.MoveNext(); Check(receiver.MoveCalls == moves, "inverse retry after " + fault);
                    var record = BlueprintStorageTransferJournal.Read(inverse.JournalPath);
                    Check(record.RequiresReconciliation && (fault != "after-journal" || record.InFlight == 0), "inverse error evidence lost: " + fault);
                    Check(frame.Item.m_stack + receiver.Items.Where(value => !ReferenceEquals(value, frame.Item)).Sum(value => value.m_stack) +
                        frame.Placed.Container.Inventory.Items.Sum(value => value.m_stack) == 3, "inverse quantity lost/duplicated: " + fault);
                }
            }
            var blocked = Delivered(second: true, partial: true);
            string childPath = Path.Combine(BlueprintStorageTransferJournal.Root, BlueprintStorageTransferJournal.ReturnOperationId(blocked.Operation).ToString("N") + ".bwtransfer");
            File.WriteAllText(childPath, "unknown child");
            Check(!blocked.Transfer.TryBeginReturn(out _, out _) && blocked.Player.Inventory.MoveCalls == 0, "existing corrupt child was overwritten");
            Check(blocked.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && blocked.Placed.Container.Inventory.MoveCalls == 1,
                "existing corrupt child allowed parent continuation");
            var collision = Delivered(second: true, partial: true); int captures = 0;
            BlueprintStorageCapture.Captured = () => {
                if (++captures == 2) File.WriteAllText(Path.Combine(BlueprintStorageTransferJournal.Root,
                    BlueprintStorageTransferJournal.ReturnOperationId(collision.Operation).ToString("N") + ".bwtransfer"), "concurrent child"); };
            try
            {
                Check(!collision.Transfer.TryBeginReturn(out _, out _), "concurrent CreateNew claim permitted");
                Check(collision.Transfer.MoveNext() == BlueprintStorageTransfer.Result.Unchanged && collision.Placed.Container.Inventory.MoveCalls == 1 &&
                    collision.SecondItem.m_stack == 1, "failed child prepare allowed parent continuation");
            }
            finally { BlueprintStorageCapture.Captured = null; }
            var legacy = Delivered(); legacy.Transfer.Dispose();
            WriteLegacy(legacy.Transfer.JournalPath);
            var old = BlueprintStorageTransferJournal.Read(legacy.Transfer.JournalPath);
            Check(old.Schema == 1 && old.Prepared.Parent == Guid.Empty && old.Prepared.Route == BlueprintStorageTransferJournal.Direction.ToContainer &&
                old.Terminal && old.RequiresReconciliation && BlueprintStorageTransferJournal.HasUnresolved(1) &&
                !BlueprintStorageTransferJournal.CanStartReturn(legacy.Operation, 1, 100, legacy.Operation), "legacy v1 journal not retained safely");
            Console.WriteLine("PASS " + assertions + " return assertions: actual inverse adapter/journal, inert native host; current attributes, empty-cell custody, refusal, no replay, legacy v1 retained. No live game/save/server proof.");
        }

        // Recompute checksums so the reader must reject semantics, not incidental corruption.
        private static void RejectPartialReturnReader(string path)
        {
            byte[] bytes = File.ReadAllBytes(path), previous = new byte[32];
            string directory = Path.Combine(BepInEx.Paths.ConfigPath, "invalid-reader"); Directory.CreateDirectory(directory);
            string copy = Path.Combine(directory, Path.GetFileName(path));
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            using (var hash = SHA256.Create())
            {
                writer.Write(Encoding.ASCII.GetBytes("BWSTJ4\r\n"));
                for (int offset = 8; offset < bytes.Length;)
                {
                    int size = BitConverter.ToInt32(bytes, offset); var body = new byte[size]; Array.Copy(bytes, offset + 4, body, 0, size);
                    if (body[24] == 2) Array.Copy(BitConverter.GetBytes(3), 0, body, 61, 4);
                    Array.Copy(previous, 0, body, 25, 32); previous = hash.ComputeHash(body);
                    writer.Write(body.Length); writer.Write(body); writer.Write(previous); offset += 4 + size + 32;
                }
                writer.Flush(); File.WriteAllBytes(copy, output.ToArray());
            }
            bool refused = false; try { BlueprintStorageTransferJournal.Read(copy); } catch (InvalidDataException) { refused = true; }
            Check(refused, "journal reader accepted a checksummed overfull inverse source");
        }

        // Produce the exact older wire layout, including recalculated frame hashes and chain.
        internal static void WriteLegacy(string path, int schema = 1)
        {
            byte[] bytes = File.ReadAllBytes(path), previous = new byte[32];
            using (var output = new MemoryStream())
            using (var writer = new BinaryWriter(output))
            using (var hash = SHA256.Create())
            {
                writer.Write(Encoding.ASCII.GetBytes("BWSTJ" + schema + "\r\n"));
                for (int offset = 8; offset < bytes.Length;)
                {
                    int size = BitConverter.ToInt32(bytes, offset); var body = new byte[size]; Array.Copy(bytes, offset + 4, body, 0, size);
                    if (body[24] == 1 && schema == 1)
                    {
                        var old = new byte[size - 17]; Array.Copy(body, old, 89); Array.Copy(body, 106, old, 89, body.Length - 106); body = old;
                    }
                    Array.Copy(BitConverter.GetBytes(schema), body, 4); Array.Copy(previous, 0, body, 25, 32);
                    previous = hash.ComputeHash(body); writer.Write(body.Length); writer.Write(body); writer.Write(previous); offset += 4 + size + 32;
                }
                writer.Flush(); File.WriteAllBytes(path, output.ToArray());
            }
        }
    }
}
