using System;
using System.Collections.Generic;
using System.IO;

namespace ReservationChecks
{
    internal static class JournalChecks
    {
        private static int assertions;
        private static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException("Journal: " + message); }
        private static void Refuses(Action action, string message)
        {
            bool refused = false;
            try { action(); } catch (IOException) { refused = true; } catch (InvalidDataException) { refused = true; } catch (ArgumentException) { refused = true; }
            Check(refused, message);
        }
        private static byte[] ReadBytes(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(stream)) return reader.ReadBytes(checked((int)stream.Length));
        }
        internal static void Run(string path)
        {
            byte[] original = ReadBytes(path);
            var snapshot = BlueprintStorageTransferJournal.Read(path);
            var start = new System.Diagnostics.ProcessStartInfo(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName,
                "--read-journal \"" + path + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            using (var child = System.Diagnostics.Process.Start(start))
            {
                if (!child.WaitForExit(30000)) { child.Kill(); throw new InvalidOperationException("Journal child reader timed out."); }
                Check(child.ExitCode == 0 && child.StandardOutput.ReadToEnd().Trim() == snapshot.Prepared.Operation.ToString("N"), "separate-process reader lost operation identity");
            }
            Refuses(() => BlueprintStorageTransferJournal.Begin(Path.GetDirectoryName(path), snapshot.Prepared), "CreateNew overwrote operation");
            Check(ReadBytes(path).Length == original.Length, "duplicate begin changed journal");
            var frames = new List<int>();
            for (int offset = 8; offset < original.Length; offset += 4 + BitConverter.ToInt32(original, offset) + 32) frames.Add(offset);
            Check(frames.Count == 4, "expected prepared/attempt/observed/terminal frames");
            string copies = Path.Combine(Path.GetDirectoryName(path), "negative-copies"); Directory.CreateDirectory(copies);
            string copy = Path.Combine(copies, Path.GetFileName(path));
            File.WriteAllBytes(copy, original);
            Check(BlueprintStorageTransferJournal.Read(copy).Terminal, "copied complete journal unreadable");
            foreach (int cut in new[] { 0, 7, frames[1] + 5, original.Length - 1 })
            {
                var truncated = new byte[cut]; Array.Copy(original, truncated, cut); File.WriteAllBytes(copy, truncated);
                Refuses(() => BlueprintStorageTransferJournal.Read(copy), "partial frame accepted at " + cut);
            }
            foreach (int cut in new[] { frames[1], frames[2], frames[3] })
            {
                var prefix = new byte[cut]; Array.Copy(original, prefix, cut); File.WriteAllBytes(copy, prefix);
                var interrupted = BlueprintStorageTransferJournal.Read(copy);
                Check(interrupted.RequiresReconciliation && !interrupted.Terminal, "valid interrupted prefix authorized replay");
            }
            var damaged = (byte[])original.Clone(); damaged[frames[1] + 60] ^= 1; File.WriteAllBytes(copy, damaged);
            Refuses(() => BlueprintStorageTransferJournal.Read(copy), "bad checksum accepted");
            var length = (byte[])original.Clone(); Array.Copy(BitConverter.GetBytes(int.MaxValue), 0, length, 8, 4); File.WriteAllBytes(copy, length);
            Refuses(() => BlueprintStorageTransferJournal.Read(copy), "huge frame allocated/accepted");
            var removed = new byte[original.Length - (frames[2] - frames[1])];
            Array.Copy(original, removed, frames[1]); Array.Copy(original, frames[2], removed, frames[1], original.Length - frames[2]); File.WriteAllBytes(copy, removed);
            Refuses(() => BlueprintStorageTransferJournal.Read(copy), "whole removed frame accepted");
            var reordered = (byte[])original.Clone(); int firstSize = frames[2] - frames[1], secondSize = frames[3] - frames[2];
            Array.Copy(original, frames[2], reordered, frames[1], secondSize); Array.Copy(original, frames[1], reordered, frames[1] + secondSize, firstSize);
            File.WriteAllBytes(copy, reordered); Refuses(() => BlueprintStorageTransferJournal.Read(copy), "reordered intact frames accepted");
            string renamed = Path.Combine(copies, Guid.NewGuid().ToString("N") + ".bwtransfer"); File.WriteAllBytes(renamed, original);
            Refuses(() => BlueprintStorageTransferJournal.Read(renamed), "filename/operation mismatch accepted");
            using (var file = new FileStream(copy, FileMode.Create)) file.SetLength(16 * 1024 * 1024 + 1);
            Refuses(() => BlueprintStorageTransferJournal.Read(copy), "file byte budget disabled");
            var paid = snapshot.Prepared; paid.Operation = Guid.NewGuid();
            paid.Paid = new[] { new BlueprintStorageTransferJournal.Payment { Prefab = "Wood", Before = 4, Paid = 3, X = 0, Y = 0, Properties = "native-own-payment-evidence" } };
            string paymentRoot = Path.Combine(Path.GetDirectoryName(Path.GetDirectoryName(path)), "paid");
            using (var journal = BlueprintStorageTransferJournal.Begin(paymentRoot, paid))
            {
                var recorded = BlueprintStorageTransferJournal.Read(journal.Path);
                Check(recorded.Prepared.Paid.Length == 1 && recorded.Prepared.Paid[0].Paid == 3 && recorded.Prepared.Paid[0].Properties == "native-own-payment-evidence",
                    "construction debit evidence was mixed with contents or lost");
            }
            foreach (string fault in new[] { "grid", "queued", "equipped", "source-hash", "destination-hash", "zdo-hash" })
            {
                paid.Operation = Guid.NewGuid();
                using (var journal = BlueprintStorageTransferJournal.Begin(paymentRoot, paid))
                {
                    var item = paid.Items[0];
                    var attempt = new BlueprintStorageTransferJournal.Attempt { Index = 0, SourceBefore = 3, SourceX = item.SourceX, SourceY = item.SourceY,
                        Queued = true, Equipped = true, EmptyCell = true, Revision = 1, ContainerRevision = 1, SourceSnapshot = item.SourceSnapshot,
                        Properties = item.Properties, SourceManifest = new string('A', 64), DestinationManifest = new string('B', 64), ZdoHash = new string('C', 64) };
                    journal.BeforeMove(attempt);
                    var observed = new BlueprintStorageTransferJournal.Observation { Index = 0, State = 2, SourceBefore = 3, SourceAfter = 1, SourceObjectStack = 1,
                        SourcePresent = true, DestinationAfter = 2, Attempted = true, BeforeObserved = true, LocalSave = true, QueuedBefore = true, EquippedBefore = true,
                        SourceX = item.SourceX, SourceY = item.SourceY, Revision = 2, ContainerRevision = 2, SourceProperties = item.Properties, DestinationProperties = item.Properties,
                        SourceManifest = new string('D', 64), DestinationManifest = new string('E', 64), ZdoHash = new string('F', 64), NativeReturn = true };
                    if (fault == "grid") observed.SourceX++;
                    if (fault == "queued") observed.QueuedBefore = false;
                    if (fault == "equipped") observed.EquippedBefore = false;
                    if (fault == "source-hash") observed.SourceManifest = attempt.SourceManifest;
                    if (fault == "destination-hash") observed.DestinationManifest = attempt.DestinationManifest;
                    if (fault == "zdo-hash") observed.ZdoHash = attempt.ZdoHash;
                    Refuses(() => journal.Observed(observed), "contradictory success accepted: " + fault);
                    Check(BlueprintStorageTransferJournal.Read(journal.Path).InFlight == 0, "contradictory observation wrote a terminal record");
                }
            }
            paid.Operation = Guid.NewGuid(); paid.Width = 10; paid.Height = 10; paid.Items = new BlueprintStorageTransferJournal.Item[100];
            for (int i = 0; i < 100; i++) paid.Items[i] = new BlueprintStorageTransferJournal.Item { Prefab = "Wood", SourceX = i % 10, SourceY = i / 10,
                SourceQuantity = 1, Amount = 1, X = i % 10, Y = i / 10, SourceSnapshot = new string('A', 32768), Properties = new string('B', 32768) };
            int filesBefore = Directory.GetFiles(paymentRoot).Length;
            Refuses(() => BlueprintStorageTransferJournal.Begin(paymentRoot, paid), "plan could exhaust the journal only after partial transfer");
            Check(Directory.GetFiles(paymentRoot).Length == filesBefore, "oversized full-operation budget created a journal before refusal");
            paid.Operation = Guid.NewGuid(); paid.Items = new[] { paid.Items[0] }; paid.PieceName = "Chest";
            paid.Paid = new[] { new BlueprintStorageTransferJournal.Payment { Prefab = "Wood", Before = 4, Paid = 3, X = 0, Y = 0,
                Properties = new string('P', 16 * 1024 * 1024 - 210000) } };
            using (var journal = BlueprintStorageTransferJournal.Begin(paymentRoot, paid)) Check(journal != null, "near-limit ASCII control unexpectedly refused");
            paid.Operation = Guid.NewGuid(); paid.PieceName = new string('\u623f', 4096); filesBefore = Directory.GetFiles(paymentRoot).Length;
            Refuses(() => BlueprintStorageTransferJournal.Begin(paymentRoot, paid), "UTF-8 piece name omitted from complete journal budget");
            Check(Directory.GetFiles(paymentRoot).Length == filesBefore, "UTF-8 budget refusal created a partial journal");
            Console.WriteLine("PASS " + assertions + " journal IO assertions: actual append/Flush/CreateNew, full and interrupted readers, identity, checksum/sequence/order/size rejection. No game/server or two-inventory crash-atomicity proof.");
        }
    }
}
