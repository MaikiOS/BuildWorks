using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionPartJournalChecks
    {
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder);
            var library = new B(Path.Combine(folder, "library.json"));
            var source = new B.Part { stableId = "wall", prefabName = "woodwall", displayName = "Wall",
                position = new B.VectorData(), rotation = new B.QuaternionData { w = 1 },
                scale = new B.VectorData { x = 1, y = 1, z = 1 }, baseScale = new B.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true };
            Check(library.TrySaveDocument("Journal fixture", "QA", new[] { source }, Array.Empty<B.Group>(), Array.Empty<B.VectorData>(),
                "wall", null, out var blueprint, out var error), error);
            var plans = new Plans(Path.Combine(folder, "projects"), 42);
            var pose = new Plans.Pose(new float[] { 5, 2, 7, 0, 0, 0, 1, 1, 1, 1 });
            var plan = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose });
            Guid part = plan.Parts[0].Id, operation = Guid.NewGuid();
            var journal = new Journal(plans); var before = Inventory(3); var after = Inventory(1);
            var prepared = journal.Prepare(plan.Id, part, operation, 17, before, out bool created);
            Check(created && prepared.Phase == Journal.Intent && prepared.RequiresReconciliation, "Fresh intent missing.");
            Check(journal.Prepare(plan.Id, part, operation, 17, before, out created).Phase == Journal.Intent && !created, "Intent replay became a new execution.");
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, part, Guid.NewGuid(), 17, before, out _));
            Refuse<InvalidOperationException>(() => plans.CancelUnpaid(plan.Id, 1, Guid.NewGuid()));
            string path = Path.Combine(plans.DirectoryPath, plan.Id.ToString("N") + "." + part.ToString("N") + ".bwpart");
            byte[] intent = File.ReadAllBytes(path);
            Journal.RequireCapacity(before, new[] { Paid() });
            var excessive = Paid(); excessive.Prefab = new string('x', 65536);
            Refuse<InvalidDataException>(() => Journal.RequireCapacity(before, new[] { excessive }));
            excessive = Paid(); excessive.Properties = new string('x', 4 * 1024 * 1024);
            Refuse<InvalidDataException>(() => Journal.RequireCapacity(before, new[] { excessive }));
            Refuse<InvalidDataException>(() => Journal.RequireCapacity(before, new Journal.PaidRow[] { null }));
            Refuse<InvalidDataException>(() => Observe(before, new[] { Paid() }));
            var invalid = Paid(); invalid.X = 7; Refuse<InvalidDataException>(() => Observe(after, new[] { invalid }));
            invalid = Paid(); invalid.Before = 4; Refuse<InvalidDataException>(() => Observe(after, new[] { invalid }));
            invalid = Paid(); invalid.Properties = Properties(9999); Refuse<InvalidDataException>(() => Observe(after, new[] { invalid }));
            Refuse<InvalidDataException>(() => Observe(after, new Journal.PaidRow[] { null }));
            Refuse<InvalidDataException>(() => Observe(after, Enumerable.Repeat(Paid(), 4097).ToArray()));
            Check(intent.SequenceEqual(File.ReadAllBytes(path)), "Rejected debit mutated the intent.");
            var observed = Observe(after, new[] { Paid() });
            Check(observed.Phase == Journal.Observed && observed.Revision == 2 && observed.RequiresReconciliation, "Observed evidence was not persisted.");
            observed.Paid[0].Paid = 99; observed.Receipt[0] ^= 1;
            Check(journal.Read(plan.Id, part).Paid[0].Paid == 2, "Returned DTO aliases durable state.");
            Check(Observe(after, new[] { Paid() }).Revision == 2, "Exact observation replay advanced revision.");
            Refuse<InvalidOperationException>(() => journal.RecordNoEffect(plan.Id, part, operation, before, true));
            var proof = new Journal.SavePair { BeforeGeneration = 7, Generation = 8,
                ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(after.Zdo)),
                WorldPayloadHash = Digest(journal.Read(plan.Id, part).Receipt) };
            var badProof = new Journal.SavePair { BeforeGeneration = 7, Generation = 8,
                ProfileHash = new string('A', 128), InventoryHash = new string('0', 64), WorldPayloadHash = proof.WorldPayloadHash };
            Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, part, operation, badProof));
            badProof.InventoryHash = proof.InventoryHash; badProof.ProfileHash = new string('A', 64);
            Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, part, operation, badProof));
            var committed = journal.ConfirmSaved(plan.Id, part, operation, proof);
            Check(committed.Phase == Journal.Committed && committed.Revision == 3 && !committed.RequiresReconciliation, "Commit evidence not durable.");
            Check(journal.ConfirmSaved(plan.Id, part, operation, proof).Revision == 3, "Save replay advanced revision.");
            Refuse<InvalidOperationException>(() => plans.CancelUnpaid(plan.Id, 1, Guid.NewGuid()));
            byte[] terminal = File.ReadAllBytes(path); terminal[20] ^= 1; File.WriteAllBytes(path, terminal);
            Refuse<InvalidDataException>(() => journal.Read(plan.Id, part));

            var second = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose }); Guid secondPart = second.Parts[0].Id, secondOp = Guid.NewGuid();
            journal.Prepare(second.Id, secondPart, secondOp, 17, before, out _);
            Refuse<InvalidOperationException>(() => journal.RecordNoEffect(second.Id, secondPart, secondOp, before, false));
            Refuse<InvalidOperationException>(() => journal.RecordNoEffect(second.Id, secondPart, secondOp, after, true));
            Check(journal.RecordNoEffect(second.Id, secondPart, secondOp, before, true).Phase == Journal.NoEffect, "No-effect outcome missing.");
            journal.Prepare(second.Id, secondPart, Guid.NewGuid(), 17, before, out created);
            Check(created, "Proven no-effect refused a new operation.");
            // Wood record captured from the failed native .226 intent; after has only its 50 -> 48 debit.
            var nativeBefore = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                "8:4:1:50|Wood", "bgAAAAEAECcAAAMDAEkyAMMk8/YB", null);
            var nativeAfter = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                "8:4:1:48|Wood", "bgAAAAEAECcAAAMDAEkwAMMk8/YB", null);
            var native = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose });
            Guid nativePart = native.Parts[0].Id, nativeOp = Guid.NewGuid();
            journal.Prepare(native.Id, nativePart, nativeOp, 17, nativeBefore, out _);
            var nativeRow = new Journal.PaidRow { Prefab = "Wood", Properties = "bhAnAAADAwBBwyTz9gE=", Before = 50, Paid = 2, X = 3, Y = 3, Quality = 1 };
            byte[] packet = Convert.FromBase64String(nativeRow.Properties); packet[0] = 109;
            nativeRow.Properties = Convert.ToBase64String(packet);
            Refuse<InvalidDataException>(() => NativeObserve());
            packet[0] = 110; packet[packet.Length - 1] = 0; nativeRow.Properties = Convert.ToBase64String(packet);
            Refuse<InvalidDataException>(() => NativeObserve());
            packet[packet.Length - 1] = 1; nativeRow.Properties = Convert.ToBase64String(packet);
            Check(NativeObserve().Phase == Journal.Observed, "Native v110 cheated provenance was lost or its correct debit refused.");
            Console.WriteLine("PASS detached construction journal: intent/replay, exact stack/property debit checks, paired-hash binding, corruption, cancel guard and no-effect retry. Supplied save hashes are fixtures; no native payment, rights, save or refund executed.");

            Journal.Record Observe(BlueprintForeignPartData inventory, Journal.PaidRow[] rows) =>
                journal.Observe(plan.Id, part, operation, 31, 9, "woodwall".GetStableHashCode(), 17, inventory, false, rows);
            Journal.Record NativeObserve() => journal.Observe(native.Id, nativePart, nativeOp, 31, 9, "woodwall".GetStableHashCode(), 17, nativeAfter, false, new[] { nativeRow });
        }
        private static Journal.PaidRow Paid() => new Journal.PaidRow { Prefab = "Wood", Properties = Properties(), Before = 3, Paid = 2, X = 0, Y = 0, Variant = 0, Quality = 1 };
        private static BlueprintForeignPartData Inventory(int count)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            { writer.Write(109); writer.Write((ushort)1); WriteItem(writer, count, 10000);
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, "8:4:1:" + count + "|Wood", Convert.ToBase64String(stream.ToArray()), null); }
        }
        private static string Properties(int durability = 10000)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            { writer.Write((byte)109); WriteItem(writer, 1, durability); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void WriteItem(BinaryWriter writer, int count, int durability)
        {
            writer.Write(durability); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
            writer.Write((byte)(64 | (count == 1 ? 0 : 8))); if (count != 1) writer.Write((ushort)count);
            writer.Write("Wood".GetStableHashCode()); writer.Write((byte)0);
        }
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
        private static void Refuse<T>(Action action) where T : Exception { try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
    }
}
