using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using OstrixMods.BuildWorks;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionCancellationChecks
    {
        private static int assertions;
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder); assertions = 0;
            var library = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            var sources = Enumerable.Range(0, 3).Select(i => new CompositeBlueprintStore.Part { stableId = "wall" + i,
                prefabName = "woodwall", displayName = "Wall", position = new CompositeBlueprintStore.VectorData(),
                rotation = new CompositeBlueprintStore.QuaternionData { w = 1 }, scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                baseScale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true }).ToArray();
            Check(library.TrySaveDocument("Removal fixture", "QA", sources, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "wall0", null, out var blueprint, out var error), error);
            var plans = new Plans(Path.Combine(folder, "projects"), 42); var parts = new Parts(plans); var journal = new Journal(plans);
            var pose = new Plans.Pose(new float[] { 5, 2, 7, 0, 0, 0, 1, 1, 1, 1 });
            var plan = Create(); Commit(plan, 0, 17);
            var noEffect = parts.Prepare(plan.Id, plan.Parts[1].Id, Guid.NewGuid(), 17, Inventory(3), out _);
            parts.RecordNoEffect(plan.Id, noEffect.Part, noEffect.Operation, Inventory(3), true);
            var operation = Guid.NewGuid(); string path = RemovalPath(plan);
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, 2, operation, 17));
            Refuse<ArgumentException>(() => journal.Prepare(plan.Id, 1, Guid.Empty, 17));
            Check(!File.Exists(path), "Rejected preparation wrote a journal.");
            var prepared = journal.Prepare(plan.Id, 1, operation, 17);
            Check(prepared.Phase == Journal.Prepared && prepared.Parts.Length == 2 && prepared.BeforeGeneration == 0, "Prepared receipt set differs.");
            byte[] preparedBytes = File.ReadAllBytes(path);
            prepared.Parts[0].Hash = "mutated";
            Check(journal.Read(plan.Id).Parts[0].Hash != "mutated", "Returned removal DTO aliases durable state.");
            Check(journal.Prepare(plan.Id, 1, operation, 17).Revision == 1 && preparedBytes.SequenceEqual(File.ReadAllBytes(path)), "Prepare replay mutated state.");
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, 1, Guid.NewGuid(), 17));
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, 1, operation, 18));
            Refuse<InvalidOperationException>(() => parts.Prepare(plan.Id, plan.Parts[2].Id, Guid.NewGuid(), 17, Inventory(3), out _));
            Refuse<InvalidOperationException>(() => parts.Prepare(plan.Id, noEffect.Part, Guid.NewGuid(), 17, Inventory(3), out _));
            Refuse<InvalidOperationException>(() => plans.CancelUnpaid(plan.Id, 1, Guid.NewGuid()));
            Refuse<InvalidOperationException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, Proof(7, 8)));
            Refuse<ArgumentException>(() => journal.BeginRemoval(plan.Id, operation, 0));
            Refuse<ArgumentException>(() => journal.BeginRemoval(plan.Id, operation, uint.MaxValue));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Refuse<IOException>(() => journal.BeginRemoval(plan.Id, operation, 7));
            Check(preparedBytes.SequenceEqual(File.ReadAllBytes(path)) && !Directory.EnumerateFiles(plans.DirectoryPath, "*.preparing").Any(), "Failed atomic replace changed state or leaked staging files.");
            var intent = journal.BeginRemoval(plan.Id, operation, 7);
            Check(intent.Phase == Journal.RemovalIntent && new Journal(plans).Read(plan.Id).Revision == 2, "Removal intent did not survive reload.");
            byte[] intentBytes = File.ReadAllBytes(path);
            Check(journal.BeginRemoval(plan.Id, operation, 7).Revision == 2, "Intent replay advanced revision.");
            Refuse<InvalidOperationException>(() => journal.BeginRemoval(plan.Id, operation, 8));
            Refuse<InvalidDataException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, Proof(6, 7)));
            Refuse<InvalidDataException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, Proof(7, 7)));
            Refuse<InvalidDataException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, Proof(7, 9)));
            var invalid = Proof(7, 8); invalid.ProfileHash = new string('A', 64);
            Refuse<InvalidDataException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, invalid));
            Check(intentBytes.SequenceEqual(File.ReadAllBytes(path)), "Rejected save proof mutated removal intent.");
            var terminal = journal.ConfirmRemovedSaved(plan.Id, operation, Proof(7, 8));
            Check(terminal.Phase == Journal.RemovedSaved && terminal.Revision == 3 && !plans.Load(plan.Id).Cancelled, "Removal falsely completed cancellation.");
            byte[] terminalBytes = File.ReadAllBytes(path);
            Check(new Journal(plans).Read(plan.Id).Saved.Generation == 8, "Saved removal did not survive reload.");
            Check(journal.ConfirmRemovedSaved(plan.Id, operation, Proof(7, 8)).Revision == 3, "Terminal replay advanced revision.");
            var conflict = Proof(7, 8); conflict.InventoryHash = new string('B', 64);
            Refuse<InvalidOperationException>(() => journal.ConfirmRemovedSaved(plan.Id, operation, conflict));
            Refuse<InvalidOperationException>(() => PartsGuard());
            foreach (var bytes in new[] { preparedBytes, intentBytes, terminalBytes })
            {
                File.WriteAllBytes(path, bytes); Check(new Journal(plans).Read(plan.Id) != null, "Durable phase failed after simulated process restart.");
                var corrupt = (byte[])bytes.Clone(); corrupt[20] ^= 1; File.WriteAllBytes(path, corrupt);
                Refuse<InvalidDataException>(() => journal.Read(plan.Id));
                File.WriteAllBytes(path, bytes.Take(bytes.Length - 1).ToArray()); Refuse<InvalidDataException>(() => journal.Read(plan.Id));
                File.WriteAllBytes(path, bytes.Concat(new byte[] { 0 }).ToArray()); Refuse<InvalidDataException>(() => journal.Read(plan.Id));
                Refuse<InvalidOperationException>(() => PartsGuard());
            }
            File.WriteAllBytes(path, terminalBytes);
            string partPath = Path.Combine(plans.DirectoryPath, plan.Id.ToString("N") + "." + plan.Parts[0].Id.ToString("N") + ".bwpart");
            byte[] originalPart = File.ReadAllBytes(partPath); var changedPart = (byte[])originalPart.Clone(); changedPart[20] ^= 1;
            File.WriteAllBytes(partPath, changedPart); Refuse<InvalidDataException>(() => journal.Read(plan.Id)); File.WriteAllBytes(partPath, originalPart);
            var unresolved = Create(); parts.Prepare(unresolved.Id, unresolved.Parts[0].Id, Guid.NewGuid(), 17, Inventory(3), out _);
            Refuse<InvalidOperationException>(() => journal.Prepare(unresolved.Id, 1, Guid.NewGuid(), 17));
            var mixed = Create(); Commit(mixed, 0, 17); Commit(mixed, 1, 18);
            Refuse<InvalidOperationException>(() => journal.Prepare(mixed.Id, 1, Guid.NewGuid(), 17));
            var unpaid = Create(); Refuse<InvalidOperationException>(() => journal.Prepare(unpaid.Id, 1, Guid.NewGuid(), 17));
            string extra = Path.Combine(plans.DirectoryPath, plan.Id.ToString("N") + "." + Guid.NewGuid().ToString("N") + ".bwpart");
            File.WriteAllText(extra, "unrelated receipt"); Refuse<InvalidDataException>(() => journal.Read(plan.Id)); File.Delete(extra);
            // A terminal plan without its removal journal must never pass through the unpaid replay API.
            var paidReplay = Create(); Commit(paidReplay, 0, 17); Guid paidOperation = Guid.NewGuid();
            string paidFile = Path.Combine(plans.DirectoryPath, paidReplay.Id.ToString("N") + ".bwproject");
            byte[] paidTerminal = File.ReadAllBytes(paidFile);
            BitConverter.GetBytes(2L).CopyTo(paidTerminal, 32); paidOperation.ToByteArray().CopyTo(paidTerminal, 40);
            using (var sha = SHA256.Create()) sha.ComputeHash(paidTerminal.Skip(8).Take(paidTerminal.Length - 40).ToArray()).CopyTo(paidTerminal, paidTerminal.Length - 32);
            File.WriteAllBytes(paidFile, paidTerminal);
            Refuse<InvalidOperationException>(() => plans.CancelUnpaid(paidReplay.Id, 1, paidOperation));
            Check(paidTerminal.SequenceEqual(File.ReadAllBytes(paidFile)), "Rejected paid replay changed the terminal record.");
            Console.WriteLine("PASS construction cancellation journal: " + assertions + " assertions; durable phases, freeze, replay, exact linked receipts, mixed payer, unresolved, corrupt/truncated framing and next-generation binding. Save proofs are fixtures; no native deletion/refund is claimed.");

            Plans.Record Create() => plans.Create(Guid.NewGuid(), blueprint, pose, Enumerable.Repeat(pose, 3).ToArray());
            string RemovalPath(Plans.Record p) => Path.Combine(plans.DirectoryPath, p.Id.ToString("N") + ".bwremoval");
            void PartsGuard() => parts.Prepare(plan.Id, plan.Parts[2].Id, Guid.NewGuid(), 17, Inventory(3), out _);
            void Commit(Plans.Record p, int index, long payer)
            {
                Guid part = p.Parts[index].Id, op = Guid.NewGuid();
                parts.Prepare(p.Id, part, op, payer, Inventory(3), out _);
                var observed = parts.Observe(p.Id, part, op, 31, (uint)(index + 1), "woodwall".GetStableHashCode(), payer, Inventory(1), false,
                    new[] { new Parts.PaidRow { Prefab = "Wood", Properties = Properties(), Before = 3, Paid = 2, X = 0, Y = 0, Quality = 1 } });
                parts.ConfirmSaved(p.Id, part, op, new Parts.SavePair { BeforeGeneration = 4, Generation = 5, ProfileHash = new string('A', 128),
                    InventoryHash = Digest(Convert.FromBase64String(observed.AfterBytes)), WorldPayloadHash = Digest(observed.Receipt) });
            }
        }
        private static Journal.SaveProof Proof(uint before, uint after) => new Journal.SaveProof { BeforeGeneration = before, Generation = after,
            ProfileHash = new string('A', 128), InventoryHash = new string('A', 64) };
        private static BlueprintForeignPartData Inventory(int count)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(109); writer.Write((ushort)1); WriteItem(writer, count);
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, "8:4:1:" + count + "|Wood", Convert.ToBase64String(stream.ToArray()), null);
            }
        }
        private static string Properties()
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            { writer.Write((byte)109); WriteItem(writer, 1); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void WriteItem(BinaryWriter writer, int count)
        {
            writer.Write(10000); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((byte)(64 | (count == 1 ? 0 : 8))); if (count != 1) writer.Write((ushort)count);
            writer.Write("Wood".GetStableHashCode()); writer.Write((byte)0);
        }
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool value, string message) { assertions++; if (!value) throw new InvalidOperationException(message); }
        private static void Refuse<T>(Action action) where T : Exception
        { assertions++; try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    }
}
