using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace ReservationChecks
{
    internal static class StorageSettlementChecks
    {
        private static void Check(bool value, string reason) { if (!value) throw new Exception("Settlement: " + reason); }
        private static void Refuse(Action action)
        {
            try { action(); } catch (InvalidOperationException) { return; }
            throw new Exception("Invalid settlement was accepted.");
        }
        internal static void Run(TransferChecks.Frame frame, string root, BlueprintStorageTransferJournal.Snapshot original)
        {
            Check(frame.Transfer.TryPreparePlayerReadback(out var profile, out string error) && profile.TryVerify(out _, out error), error);
            Check(profile.TryVerify(out var playerEvidence, out error), error);
            Check(frame.Transfer.TryPrepareWorldReadback(out var world, out error) && world.TryStart(CancellationToken.None, out error), error);
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (!world.IsCompleted && DateTime.UtcNow < deadline) Thread.Sleep(1);
            Check(world.IsCompleted && world.TryFinish(out _, out error), error);
            Check(world.TryFinish(out var worldEvidence, out error), error);

            byte[] bytes = JournalChecks.ReadBytes(frame.Transfer.JournalPath);
            int prefixLength = 8;
            for (int i = 0; i < 2; i++) prefixLength += BitConverter.ToInt32(bytes, prefixLength) + 36;
            string prefixRoot = Path.Combine(root, "attempt-prefix"); Directory.CreateDirectory(prefixRoot);
            string prefix = Path.Combine(prefixRoot, original.Prepared.Operation.ToString("N") + ".bwtransfer");
            File.WriteAllBytes(prefix, bytes.Take(prefixLength).ToArray());
            var pending = BlueprintStorageTransferJournal.Read(prefix).Pending;
            Check(pending != null, "Missing real attempt prefix.");
            string previousConfig = BepInEx.Paths.ConfigPath;
            BepInEx.Paths.ConfigPath = Path.Combine(root, "linked");
            try
            {
                var plan = original.Prepared;
                plan.ConstructionProject = Guid.NewGuid(); plan.ConstructionPart = Guid.NewGuid();
                plan.ConstructionFingerprint = new string('A', 64);
                // Journal framing fixture only; real parent payment is separately read/validated by the production caller.
                plan.Paid = new[] { new BlueprintStorageTransferJournal.Payment { Prefab = "Wood", Properties = "fixture-parent-paid", Before = 3, Paid = 1 } };
                BlueprintStorageTransferJournal.Snapshot Joined() => BlueprintStorageTransferJournal.ReadConstruction(
                    plan.ConstructionProject, plan.ConstructionPart, plan.Operation, plan.World, plan.Player, plan.ConstructionFingerprint, plan.Paid);
                Check(Joined() == null, "absent child was not distinguished from settled contents");
                string orphanReturn = Path.Combine(BlueprintStorageTransferJournal.Root,
                    BlueprintStorageTransferJournal.ReturnOperationId(plan.Operation).ToString("N") + ".bwtransfer");
                Directory.CreateDirectory(BlueprintStorageTransferJournal.Root);
                File.WriteAllBytes(orphanReturn, new byte[] { 1 });
                Refuse(() => Joined());
                File.Delete(orphanReturn); // Fixture cleanup only; a retained return is never ignored in production.
                using (var journal = BlueprintStorageTransferJournal.Begin(BlueprintStorageTransferJournal.Root, plan))
                {
                    Check(!Joined().Settled, "prepared child completed the project part");
                    Refuse(() => journal.RecordSettlement(plan.ConstructionFingerprint));
                    journal.BeforeMove(pending); journal.Observed(original.Observations[0]);
                    Refuse(() => journal.RecordSettlement(plan.ConstructionFingerprint));
                    journal.RecordPersistence(original.SavedPair.BeforeGeneration, original.SavedPair.Generation, playerEvidence, worldEvidence,
                        original.SavedPair.SourceSession, original.SavedPair.SourcePlayerInstance);
                    var paired = BlueprintStorageTransferJournal.Read(journal.Path);
                    Check(paired.Schema == 6 && paired.Prepared.ConstructionProject == plan.ConstructionProject &&
                        paired.Prepared.ConstructionPart == plan.ConstructionPart && paired.RequiresReconciliation && !paired.Settled,
                        "Parent link lost or pair prematurely released custody.");
                    Check(BlueprintStorageTransferJournal.HasUnresolved(plan.World), "Missing linked pending hold.");
                    Refuse(() => journal.RecordSettlement(new string('B', 64)));
                    byte[] beforeTerminal = JournalChecks.ReadBytes(journal.Path);
                    journal.RecordSettlement(plan.ConstructionFingerprint);
                    var settled = BlueprintStorageTransferJournal.Read(journal.Path);
                    Check(settled.Settled && !settled.RequiresReconciliation && !BlueprintStorageTransferJournal.HasUnresolved(plan.World),
                        "Valid linked settlement did not release world hold.");
                    Check(Joined().Settled, "same saved parent did not bind the completed child");
                    try
                    {
                        BlueprintStorageTransferJournal.ReadConstruction(Guid.NewGuid(), plan.ConstructionPart, plan.Operation,
                            plan.World, plan.Player, plan.ConstructionFingerprint, plan.Paid);
                        throw new Exception("A different project reused settled contents.");
                    }
                    catch (InvalidDataException) { }
                    var wrongPaid = new[] { new BlueprintStorageTransferJournal.Payment { Prefab = "Wood", Properties = "different", Before = 3, Paid = 1 } };
                    try
                    {
                        BlueprintStorageTransferJournal.ReadConstruction(plan.ConstructionProject, plan.ConstructionPart, plan.Operation,
                            plan.World, plan.Player, plan.ConstructionFingerprint, wrongPaid);
                        throw new Exception("A different payment reused settled contents.");
                    }
                    catch (InvalidDataException) { }
                    byte[] terminal = JournalChecks.ReadBytes(journal.Path);
                    journal.RecordSettlement(plan.ConstructionFingerprint);
                    Check(terminal.SequenceEqual(JournalChecks.ReadBytes(journal.Path)), "Settlement replay appended or changed bytes.");
                    Refuse(() => journal.RecordSettlement(new string('B', 64)));
                    string crashRoot = Path.Combine(root, "settlement-crash"); Directory.CreateDirectory(crashRoot);
                    string crash = Path.Combine(crashRoot, Path.GetFileName(journal.Path));
                    File.WriteAllBytes(crash, beforeTerminal);
                    Check(BlueprintStorageTransferJournal.Read(crash).RequiresReconciliation, "Pre-terminal crash lost custody hold.");
                    File.WriteAllBytes(crash, terminal.Take(terminal.Length - 1).ToArray());
                    try { BlueprintStorageTransferJournal.Read(crash); throw new Exception("Torn terminal accepted."); }
                    catch (InvalidDataException) { }
                    File.WriteAllBytes(crash, terminal.Concat(terminal.Skip(beforeTerminal.Length)).ToArray());
                    try { BlueprintStorageTransferJournal.Read(crash); throw new Exception("Duplicate terminal accepted."); }
                    catch (InvalidDataException) { }
                    string returnPath = Path.Combine(BlueprintStorageTransferJournal.Root,
                        BlueprintStorageTransferJournal.ReturnOperationId(plan.Operation).ToString("N") + ".bwtransfer");
                    File.WriteAllBytes(returnPath, new byte[] { 1 });
                    Refuse(() => Joined());
                    File.Delete(returnPath); // Fixture only; production retains every return attempt.
                    plan.Operation = Guid.NewGuid(); plan.ConstructionPart = Guid.NewGuid();
                    using (var next = BlueprintStorageTransferJournal.Begin(BlueprintStorageTransferJournal.Root, plan))
                        Check(BlueprintStorageTransferJournal.HasUnresolved(plan.World), "Second pending operation was not held.");
                    Check(File.Exists(journal.Path), "Settled evidence was deleted to allow the next operation.");
                }
            }
            finally { BepInEx.Paths.ConfigPath = previousConfig; }
            Check(original.RequiresReconciliation, "Unlinked legacy pair was silently settled.");
            Console.WriteLine("PASS linked storage WAL: paid-parent keys, paired-save fingerprint, terminal/replay/crash holds, second operation without deleting settled evidence. File/host fixtures; not native payment or completion authority.");
        }
    }
}
