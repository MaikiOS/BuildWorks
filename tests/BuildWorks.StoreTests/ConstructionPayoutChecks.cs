using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using OstrixMods.BuildWorks.Geometry;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Removal = OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionPayoutChecks
    {
        private static int assertions;
        internal static void Run(string folder)
        {
            Directory.CreateDirectory(folder); assertions = 0;
            var library = new CompositeBlueprintStore(Path.Combine(folder, "library.json"));
            var source = new CompositeBlueprintStore.Part { stableId = "wall", prefabName = "woodwall", displayName = "Wall",
                position = new CompositeBlueprintStore.VectorData(), rotation = new CompositeBlueprintStore.QuaternionData { w = 1 },
                scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, baseScale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }, editorVisible = true };
            Check(library.TrySaveDocument("Payout fixture", "QA", new[] { source }, Array.Empty<CompositeBlueprintStore.Group>(),
                Array.Empty<CompositeBlueprintStore.VectorData>(), "wall", null, out var blueprint, out var error), error);
            var plans = new Plans(Path.Combine(folder, "projects"), 42); var parts = new Parts(plans); var removal = new Removal(plans); var journal = new Journal(plans);
            var pose = new Plans.Pose(new float[] { 0, 0, 0, 0, 0, 0, 1, 1, 1, 1 });
            var plan = plans.Create(Guid.NewGuid(), blueprint, pose, new[] { pose });
            Guid part = plan.Parts[0].Id, build = Guid.NewGuid(), operation = Guid.NewGuid();
            var before = Inventory(5); var after = Inventory(3);
            parts.Prepare(plan.Id, part, build, 17, before, out _);
            var receipt = parts.Observe(plan.Id, part, build, 31, 1, "woodwall".GetStableHashCode(), 17, after, false,
                new[] { Paid(0), Paid(1) });
            parts.ConfirmSaved(plan.Id, part, build, new Parts.SavePair { BeforeGeneration = 4, Generation = 5,
                ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(after.Zdo)), WorldPayloadHash = Digest(receipt.Receipt) });
            var entries = new[] { Entry(), Entry() }; string path = Path.Combine(plans.DirectoryPath, plan.Id.ToString("N") + ".bwpayout");
            Refuse<InvalidOperationException>(() => Prepare(entries));
            removal.Prepare(plan.Id, 1, operation, 17); removal.BeginRemoval(plan.Id, operation, 7);
            Refuse<InvalidOperationException>(() => Prepare(entries));
            removal.ConfirmRemovedSaved(plan.Id, operation, new Removal.SaveProof { BeforeGeneration = 7, Generation = 8,
                ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(after.Zdo)) });
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, operation, 18, 8, after, entries));
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, Guid.NewGuid(), 17, 8, after, entries));
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, operation, 17, 7, after, entries));
            Refuse<InvalidOperationException>(() => journal.Prepare(plan.Id, operation, 17, uint.MaxValue, after, entries));
            Refuse<InvalidDataException>(() => Prepare(new[] { entries[0] }));
            Refuse<InvalidDataException>(() => Prepare(new[] { entries[0], entries[0] }));
            var changed = Entry(); changed.Blob = Package(3, 0, changed.Id); Refuse<InvalidDataException>(() => Prepare(new[] { changed, entries[1] }));
            changed.Blob = Package(2, 0, changed.Id, durability: 9900); Refuse<InvalidDataException>(() => Prepare(new[] { changed, entries[1] }));
            changed.Blob = Package(2, 0, Guid.NewGuid()); Refuse<InvalidDataException>(() => Prepare(new[] { changed, entries[1] }));
            changed.Blob = Package(2, 0, changed.Id, custom: false); Refuse<InvalidDataException>(() => Prepare(new[] { changed, entries[1] }));
            changed.Blob = Package(2, 1, changed.Id); Refuse<InvalidDataException>(() => Prepare(new[] { changed, entries[1] }));
            Check(!File.Exists(path), "Rejected preparation created payout state.");
            var prepared = Prepare(entries); byte[] original = File.ReadAllBytes(path);
            Check(prepared.Phase == Journal.Prepared && prepared.Items.All(e => e.State == Journal.Ready), "Payout was prematurely attempted.");
            Check(Prepare(entries).Revision == 1 && original.SequenceEqual(File.ReadAllBytes(path)), "Ready replay changed payout.");
            prepared.Items[0].Blob = "mutated"; Check(new Journal(plans).Read(plan.Id).Items[0].Blob == entries[0].Blob, "Read aliases disk state.");
            Refuse<InvalidOperationException>(() => journal.BeginItem(plan.Id, operation, 1));
            Refuse<InvalidOperationException>(() => journal.Observe(plan.Id, operation, 0, 31, 4));
            Refuse<InvalidOperationException>(() => journal.BeginSave(plan.Id, operation));
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                Refuse<IOException>(() => journal.BeginItem(plan.Id, operation, 0));
            Check(original.SequenceEqual(File.ReadAllBytes(path)) && !Directory.EnumerateFiles(plans.DirectoryPath, "*.preparing").Any(), "Failed replace lost state/staging.");
            journal.BeginItem(plan.Id, operation, 0);
            Check(new Journal(plans).Read(plan.Id).Items[0].State == Journal.Attempted, "Native intent did not persist.");
            Refuse<InvalidOperationException>(() => journal.BeginItem(plan.Id, operation, 0));
            Refuse<InvalidOperationException>(() => Prepare(entries));
            Refuse<InvalidOperationException>(() => journal.BeginItem(plan.Id, operation, 1));
            Refuse<InvalidOperationException>(() => journal.Observe(plan.Id, operation, 0, 0, 4));
            journal.Observe(plan.Id, operation, 0, 31, 4);
            Check(journal.Observe(plan.Id, operation, 0, 31, 4).Revision == 3, "Observation replay advanced revision.");
            Refuse<InvalidOperationException>(() => journal.Observe(plan.Id, operation, 0, 31, 5));
            journal.BeginItem(plan.Id, operation, 1);
            Refuse<InvalidDataException>(() => journal.Observe(plan.Id, operation, 1, 31, 4));
            journal.Observe(plan.Id, operation, 1, 31, 5);
            Refuse<InvalidOperationException>(() => journal.ConfirmSaved(plan.Id, operation, Proof()));
            journal.BeginSave(plan.Id, operation);
            Check(new Journal(plans).Read(plan.Id).Phase == Journal.SaveIntent, "Save intent lost after reload.");
            var bad = Proof(); bad.Generation = 10; Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, operation, bad));
            bad = Proof(); bad.InventoryHash = new string('A', 64); Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, operation, bad));
            bad = Proof(); bad.ItemHashes = bad.ItemHashes.Reverse().ToArray(); Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, operation, bad));
            bad = Proof(); bad.ProfileHash = new string('A', 64); Refuse<InvalidDataException>(() => journal.ConfirmSaved(plan.Id, operation, bad));
            var saved = journal.ConfirmSaved(plan.Id, operation, Proof()); byte[] terminal = File.ReadAllBytes(path);
            Check(saved.Phase == Journal.IssuedSaved && saved.Revision == 7 && !plans.Load(plan.Id).Cancelled, "Issuance falsely settled cancellation.");
            Check(new Journal(plans).Read(plan.Id).Saved.ItemHashes.Length == 2, "Saved item proofs lost after reload.");
            Check(journal.ConfirmSaved(plan.Id, operation, Proof()).Revision == 7 && terminal.SequenceEqual(File.ReadAllBytes(path)), "Proof replay mutated state.");
            bad = Proof(); bad.ProfileHash = new string('B', 128); Refuse<InvalidOperationException>(() => journal.ConfirmSaved(plan.Id, operation, bad));
            Refuse<InvalidOperationException>(() => Prepare(entries));
            Refuse<InvalidOperationException>(() => journal.BeginItem(plan.Id, operation, 0));
            byte[] corrupt = (byte[])terminal.Clone(); corrupt[20] ^= 1; File.WriteAllBytes(path, corrupt); Refuse<InvalidDataException>(() => journal.Read(plan.Id));
            File.WriteAllBytes(path, terminal.Take(30).ToArray()); Refuse<InvalidDataException>(() => journal.Read(plan.Id));
            File.WriteAllBytes(path, terminal.Concat(new byte[] { 0 }).ToArray()); Refuse<InvalidDataException>(() => journal.Read(plan.Id));
            File.WriteAllBytes(path, terminal);
            Check(journal.Paid(plan.Id).Sum(p => p.Paid) == 4 && journal.Read(plan.Id).Items.Length == 2, "Exact contribution set changed.");
            Console.WriteLine("PASS payout journal: " + assertions + " assertions; exact paid properties/quantity, durable one-shot intents, replay, corruption and paired-save binding. Native issuance/collection is not claimed.");
            Journal.Record Prepare(Journal.Entry[] rows) => journal.Prepare(plan.Id, operation, 17, 8, after, rows);
            Journal.Proof Proof() => new Journal.Proof { Generation = 9, ProfileHash = new string('A', 128),
                InventoryHash = Digest(Convert.FromBase64String(after.Zdo)), ItemHashes = entries.Select(e => Digest(Convert.FromBase64String(e.Blob))).ToArray() };
        }
        private static Parts.PaidRow Paid(int x) => new Parts.PaidRow { Prefab = "Wood", Properties = Package(1, x, Guid.Empty),
            Before = 5, Paid = 2, X = x, Y = 0, Quality = 1 };
        private static Journal.Entry Entry()
        { Guid id = Guid.NewGuid(); return new Journal.Entry { Id = id, Blob = Package(2, 0, id) }; }
        private static BlueprintForeignPartData Inventory(int count)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(110); writer.Write((ushort)2); WriteItem(writer, count, 0, Guid.Empty, 4200, true); WriteItem(writer, count, 1, Guid.Empty, 4200, true);
                return new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, "8:4:2:" + (count * 2) + "|Wood", Convert.ToBase64String(stream.ToArray()), null);
            }
        }
        private static string Package(int count, int x, Guid id, int durability = 4200, bool custom = true)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            { writer.Write((byte)110); WriteItem(writer, count, x, id, durability, custom); return Convert.ToBase64String(stream.ToArray()); }
        }
        private static void WriteItem(BinaryWriter writer, int count, int x, Guid id, int durability, bool custom)
        {
            writer.Write(durability); writer.Write((byte)x); writer.Write((byte)0); writer.Write((byte)2);
            writer.Write((byte)(1 | 32 | 64 | (count == 1 ? 0 : 8) | (custom || id != Guid.Empty ? 128 : 0)));
            if (count != 1) writer.Write((ushort)count); writer.Write(77L); writer.Write("Maker"); writer.Write("Wood".GetStableHashCode());
            if (custom || id != Guid.Empty) writer.Write((byte)((custom ? 1 : 0) + (id == Guid.Empty ? 0 : 1)));
            if (custom) { writer.Write("mod.attribute"); writer.Write("kept"); }
            if (id != Guid.Empty) { writer.Write(Journal.CustodyKey); writer.Write(id.ToString("N")); }
            writer.Write((byte)1);
        }
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool value, string reason) { assertions++; if (!value) throw new InvalidOperationException(reason); }
        private static void Refuse<T>(Action action) where T : Exception
        { assertions++; try { action(); } catch (T) { return; } throw new InvalidOperationException("Expected " + typeof(T).Name); }
    }
}
