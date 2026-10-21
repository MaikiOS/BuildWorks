using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using System.Text;
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
            Check(saved.Collections == null && !Encoding.UTF8.GetString(terminal).Contains("Collections"), "Additive credit changed legacy canonical bytes.");
            TerminalChecks(plans, journal, removal, plan.Id, operation, after, path);
            CreditChecks(journal, plan.Id, operation, after, path);
            Console.WriteLine("PASS payout journal: " + assertions + " assertions; exact paid properties/quantity, durable one-shot intents, replay, corruption and paired-save binding. Native issuance/collection is not claimed.");
            Journal.Record Prepare(Journal.Entry[] rows) => journal.Prepare(plan.Id, operation, 17, 8, after, rows);
            Journal.Proof Proof() => new Journal.Proof { Generation = 9, ProfileHash = new string('A', 128),
                InventoryHash = Digest(Convert.FromBase64String(after.Zdo)), ItemHashes = entries.Select(e => Digest(Convert.FromBase64String(e.Blob))).ToArray() };
        }
        private static void TerminalChecks(Plans plans, Journal journal, Removal removal, Guid project, Guid operation, BlueprintForeignPartData inventory, string path)
        {
            string planPath = Path.Combine(plans.DirectoryPath, project.ToString("N") + ".bwproject");
            string removalPath = Path.Combine(plans.DirectoryPath, project.ToString("N") + ".bwremoval");
            byte[] planBytes = File.ReadAllBytes(planPath), issuedBytes = File.ReadAllBytes(path), removalBytes = File.ReadAllBytes(removalPath);
            var issued = journal.Read(project); string hash = Journal.IssuanceFingerprint(issued), receipt = Journal.ReceiptValue(issued, 0);
            var originalPlan = plans.Load(project);
            var profile = Proof(17, "unrelated", "not-a-collection-receipt");
            Guid[] parts = removal.Read(project).Parts.Where(p => p.Phase == Parts.Committed).Select(p => p.Operation).ToArray();
            NativeWorldStorageReadback.AbsenceEvidence Absence(uint generation = 9, long world = 42, int? marker = null, Guid[] ids = null)
                => (NativeWorldStorageReadback.AbsenceEvidence)Activator.CreateInstance(typeof(NativeWorldStorageReadback.AbsenceEvidence),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                        new NativeWorldStorageReadback.AbsenceExpectation(world, marker ?? Plans.ConstructionMarkerKey, ids ?? parts), 36, 1, 0, generation }, null);
            try
            {
                Refuse<ArgumentException>(() => plans.CancelPaid(project, 2, operation, 9, Absence(), profile));
                Refuse<ArgumentException>(() => plans.CancelPaid(project, 1, Guid.Empty, 9, Absence(), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, Guid.NewGuid(), 9, Absence(), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 8, Absence(), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 8, Absence(8), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(world: 43), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(marker: Journal.ItemMarkerKey), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(ids: issued.Items.Select(i => i.Id).ToArray()), profile));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), null));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), Proof(18, "unrelated", "value")));
                Check(planBytes.SequenceEqual(File.ReadAllBytes(planPath)) && issuedBytes.SequenceEqual(File.ReadAllBytes(path)), "Refused terminal changed old files.");
                journal.BeginCredit(project, operation, 0, inventory, Package(2, 2, Guid.Empty), 2, 0, new string('A', 128));
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), profile));
                File.WriteAllBytes(path, issuedBytes);
                foreach (Action<Journal.Record> mutate in new Action<Journal.Record>[] {
                    r => r.Project = Guid.NewGuid(), r => r.Operation = Guid.NewGuid(), r => r.Player++, r => r.World++,
                    r => r.RemovalHash = new string('F', 64), r => r.BeforeGeneration++, r => r.BeforeInfo += "changed",
                    r => r.BeforeBytes += "changed", r => r.Items[0].Id = Guid.NewGuid(), r => r.Items[0].Blob += "changed",
                    r => r.Items[0].ObjectUser++, r => r.Items[0].ObjectId++, r => r.Items = r.Items.Reverse().ToArray(),
                    r => r.Saved.Generation++, r => r.Saved.ProfileHash = new string('F',128), r => r.Saved.InventoryHash = new string('F',64),
                    r => r.Saved.ItemHashes = r.Saved.ItemHashes.Reverse().ToArray() })
                {
                    var changed = journal.Read(project); mutate(changed);
                    Check(Journal.IssuanceFingerprint(changed) != hash, "Immutable issuance change was not bound.");
                }
                var counter = journal.Read(project); counter.Revision++;
                Check(Journal.IssuanceFingerprint(counter) == hash && issuedBytes.SequenceEqual(File.ReadAllBytes(path)), "Fingerprint depends on mutable revision or writes files.");
                // Force the second file commit to fail, after the durable binding has been written.
                using (var locked = new FileStream(planPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    Refuse<IOException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), profile));
                Check(!plans.Load(project).Cancelled && journal.Read(project).Terminal != null && planBytes.SequenceEqual(File.ReadAllBytes(planPath)), "Partial terminal commit lost its frozen intent.");
                Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 0, inventory, Package(2, 2, Guid.Empty), 2, 0, new string('A', 128)));
                var closed = plans.CancelPaid(project, 1, operation, 9, Absence(), profile);
                Check(closed.Cancelled && closed.Revision == 2 && closed.CancelOperation == operation && closed.BlueprintBytes.SequenceEqual(originalPlan.BlueprintBytes) &&
                    closed.RootPose.Values.SequenceEqual(originalPlan.RootPose.Values) && closed.Parts.Select(p => p.Id).SequenceEqual(originalPlan.Parts.Select(p => p.Id)) &&
                    closed.Parts.Zip(originalPlan.Parts, (a,b) => a.WorldPose.Values.SequenceEqual(b.WorldPose.Values)).All(same => same), "Paid terminal identity/snapshot differs.");
                Check(journal.Read(project).Collections == null && journal.Read(project).Terminal.IssuanceHash == hash &&
                    removal.Read(project).Phase == Removal.RemovedSaved && Journal.ReceiptValue(journal.Read(project), 0) == receipt,
                    "Terminal waited for collection or changed entitlement.");
                byte[] closedPlan = File.ReadAllBytes(planPath), closedPayout = File.ReadAllBytes(path);
                plans.CancelPaid(project, 1, operation, 9, Absence(), profile);
                Check(closedPlan.SequenceEqual(File.ReadAllBytes(planPath)) && closedPayout.SequenceEqual(File.ReadAllBytes(path)), "Terminal replay changed persisted bytes.");
                Refuse<InvalidOperationException>(() => removal.Prepare(project, 1, operation, 17));
                Refuse<InvalidOperationException>(() => removal.BeginRemoval(project, operation, 7));
                Refuse<InvalidOperationException>(() => removal.ConfirmRemovedSaved(project, operation, removal.Read(project).Saved));
                Refuse<InvalidOperationException>(() => journal.Prepare(project, operation, 17, 9, inventory, issued.Items));
                Refuse<InvalidOperationException>(() => journal.BeginItem(project, operation, 0));
                Refuse<InvalidOperationException>(() => journal.Observe(project, operation, 0, 31, 4));
                Refuse<InvalidOperationException>(() => journal.BeginSave(project, operation));
                Refuse<InvalidOperationException>(() => journal.ConfirmSaved(project, operation, issued.Saved));
                File.WriteAllBytes(path, issuedBytes);
                Refuse<InvalidDataException>(() => journal.Read(project));
                File.WriteAllBytes(path, closedPayout);
                File.Delete(path); Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), profile));
                File.WriteAllBytes(path, closedPayout);
                var credit = journal.BeginCredit(project, operation, 0, inventory, Package(2, 2, Guid.Empty), 2, 0, new string('A', 128));
                var creditedInventory = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, credit.Collections[0].AfterInfo, credit.Collections[0].AfterBytes, null);
                string key = Journal.ReceiptKey(credit.Items[0].Id);
                journal.ConfirmSavedCredit(project, operation, 0, FullProof(17, key, receipt, creditedInventory));
                credit = journal.BeginCredit(project, operation, 1, creditedInventory, Package(2, 3, Guid.Empty), 3, 0, new string('B', 128));
                var secondInventory = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, credit.Collections[1].AfterInfo, credit.Collections[1].AfterBytes, null);
                journal.ConfirmSavedCredit(project, operation, 1, FullProof(17, Journal.ReceiptKey(credit.Items[1].Id), Journal.ReceiptValue(credit, 1), secondInventory));
                journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, receipt));
                var absentPayout = (NativeWorldStorageReadback.AbsenceEvidence)Activator.CreateInstance(typeof(NativeWorldStorageReadback.AbsenceEvidence),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                        new NativeWorldStorageReadback.AbsenceExpectation(42, Journal.ItemMarkerKey, new[] { credit.Items[0].Id }), 36, 1, 0, (uint)10 }, null);
                journal.ConfirmCleanup(project, operation, 0, absentPayout, Proof(17, key, receipt));
                Check(Journal.IssuanceFingerprint(journal.Read(project)) == hash && plans.Load(project).Cancelled &&
                    journal.Read(project).Collections[0].Removal.Phase == Journal.CleanupSaved, "Credit/cleanup invalidated terminal binding.");
                // A legacy settled prefix must retain its historical generation after a later terminal.
                var settled = journal.Read(project); settled.Terminal = null; settled.Revision--;
                File.WriteAllBytes(planPath, planBytes);
                using (var body = new MemoryStream())
                {
                    new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(Journal.Record)).WriteObject(body, settled);
                    byte[] bytes = body.ToArray();
                    using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
                    using (var writer = new BinaryWriter(file))
                    using (var sha = SHA256.Create())
                    { writer.Write(issuedBytes, 0, 8); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); }
                }
                Check(journal.Read(project).MinimumWorldGeneration == 10, "Known cleanup generation was lost.");
                Refuse<InvalidOperationException>(() => plans.CancelPaid(project, 1, operation, 9, Absence(), profile));
                plans.CancelPaid(project, 1, operation, 11, Absence(11), profile);
                byte[] later = File.ReadAllBytes(path);
                Check(journal.Read(project).Collections[0].Removal.BeforeGeneration == 9 && journal.Read(project).Terminal.Generation == 11,
                    "Later terminal rewrote historical cleanup evidence.");
                journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, receipt));
                Check(later.SequenceEqual(File.ReadAllBytes(path)), "Historical cleanup replay changed terminal or receipt bytes.");
                string secondKey = Journal.ReceiptKey(credit.Items[1].Id), secondValue = Journal.ReceiptValue(credit, 1);
                Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 1, 9, Proof(17, secondKey, secondValue)));
                Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 1, 10, Proof(17, secondKey, secondValue)));
                Check(journal.BeginCleanup(project, operation, 1, 11, Proof(17, secondKey, secondValue)).Collections[1].Removal.BeforeGeneration == 11,
                    "New cleanup did not use current terminal generation.");
                Check(removalBytes.SequenceEqual(File.ReadAllBytes(removalPath)) && !Directory.EnumerateFiles(plans.DirectoryPath, "*.preparing").Any(), "Terminal changed removal proof or leaked staging.");
            }
            finally { File.WriteAllBytes(planPath, planBytes); File.WriteAllBytes(path, issuedBytes); File.WriteAllBytes(removalPath, removalBytes); }
        }
        private static void CreditChecks(Journal journal, Guid project, Guid operation, BlueprintForeignPartData before, string path)
        {
            string clean = Package(2, 2, Guid.Empty);
            var paid = journal.Paid(project)[0];
            Refuse<InvalidDataException>(() => Journal.CreditInventory(before, clean, paid, 0, 0));
            Refuse<InvalidDataException>(() => Journal.CreditInventory(before, clean, paid, 8, 0));
            Refuse<InvalidDataException>(() => Journal.CreditInventory(before, Package(2, 2, Guid.NewGuid()), paid, 2, 0));
            Refuse<InvalidDataException>(() => Journal.CreditInventory(before, Package(2, 2, Guid.Empty, durability: 999), paid, 2, 0));
            Refuse<InvalidDataException>(() => Journal.CreditInventory(before, Package(2, 2, Guid.Empty, custom: false), paid, 2, 0));
            Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 1, before, clean, 2, 0, new string('A', 128)));
            Refuse<InvalidDataException>(() => journal.BeginCredit(project, operation, 0, before, clean, 2, 0, "short"));
            Check(journal.Read(project).Collections == null, "Rejected credit intent mutated legacy state.");
            var intent = journal.BeginCredit(project, operation, 0, before, clean, 2, 0, new string('A', 128));
            var credit = intent.Collections.Single();
            Check(intent.Revision == 8 && credit.Phase == Journal.CreditIntent && credit.SavedProfileHash == null, "Credit prematurely claimed saved receipt.");
            var after = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, credit.AfterInfo, credit.AfterBytes, null);
            Check(BlueprintContainerInventory.TryRead(after, s => s.GetStableHashCode(), out var items) && items.Count == 3 && items.Sum(i => i.Stack) == 8 &&
                items[0].Stack == 3 && items[1].Stack == 3 && items[2].Stack == 2 && items[2].X == 2 && items[2].CustomData["mod.attribute"] == "kept" &&
                !items[2].CustomData.ContainsKey(Journal.CustodyKey), "Exact append merged/lost existing units or provided attributes.");
            byte[] pending = File.ReadAllBytes(path);
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9,
                Proof(17, Journal.ReceiptKey(intent.Items[0].Id), Journal.ReceiptValue(intent, 0))));
            Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 0, before, clean, 2, 0, new string('A', 128)));
            Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 1, after, Package(2, 3, Guid.Empty), 3, 0, new string('A', 128)));
            string key = Journal.ReceiptKey(intent.Items[0].Id), value = Journal.ReceiptValue(intent, 0);
            Refuse<InvalidOperationException>(() => journal.ConfirmCredit(project, operation, 0, null));
            Refuse<InvalidOperationException>(() => journal.ConfirmCredit(project, operation, 0, Proof(18, key, value)));
            Refuse<InvalidOperationException>(() => journal.ConfirmCredit(project, operation, 0, Proof(17, key, "pending:" + value)));
            Refuse<InvalidOperationException>(() => journal.ConfirmCredit(project, operation, 0, Proof(17, "wrong-key", value)));
            Check(pending.SequenceEqual(File.ReadAllBytes(path)), "Rejected confirmation changed durable intent.");
            // Recompute framing/hash too: semantic validation must reject a hostile but correctly hashed body.
            string body = Encoding.UTF8.GetString(pending, 12, BitConverter.ToInt32(pending, 8));
            string altered = body.Replace("\"AfterInfo\":\"" + credit.AfterInfo + "\"", "\"AfterInfo\":\"8:4:3:999|Wood\"");
            Check(altered != body, "Credit corruption fixture did not alter its target.");
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(file))
            using (var sha = SHA256.Create())
            { byte[] bytes = Encoding.UTF8.GetBytes(altered); writer.Write(pending, 0, 8); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); }
            Refuse<InvalidDataException>(() => journal.Read(project)); File.WriteAllBytes(path, pending);
            Refuse<InvalidOperationException>(() => journal.ConfirmCredit(project, operation, 0, Proof(17, key, value)));
            Check(pending.SequenceEqual(File.ReadAllBytes(path)), "Receipt-only proof settled or mutated an unconfirmed credit.");
            var done = journal.ConfirmSavedCredit(project, operation, 0, FullProof(17, key, value, after));
            Check(done.Revision == 9 && done.Collections[0].Phase == Journal.ProfileCredited && Journal.ReceiptValue(done, 0) == value,
                "Receipt depends on mutable credit revision or confirmation was not durable.");
            byte[] confirmed = File.ReadAllBytes(path);
            Check(journal.ConfirmCredit(project, operation, 0, Proof(17, key, value)).Revision == 9 && confirmed.SequenceEqual(File.ReadAllBytes(path)),
                "Permanent receipt replay mutated the journal.");
            // The evidence API intentionally has no inventory field: spending later cannot authorize re-credit.
            Check(typeof(NativePlayerProfileReadback.CustomDataEvidence).GetProperty("Inventory") == null, "Receipt evidence widened to inventory proof.");
            Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 0, after, clean, 2, 0, new string('B', 128)));
            // Fork this temporary fixture to prove the cleanup guard independently of the item-count bound.
            journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, value));
            byte[] cleaning = File.ReadAllBytes(path);
            Refuse<InvalidOperationException>(() => journal.BeginCredit(project, operation, 1, after, Package(2, 3, Guid.Empty), 3, 0, new string('B', 128)));
            Check(cleaning.SequenceEqual(File.ReadAllBytes(path)), "A new credit changed an unsettled cleanup.");
            File.WriteAllBytes(path, confirmed);
            var next = journal.BeginCredit(project, operation, 1, after, Package(2, 3, Guid.Empty), 3, 0, new string('B', 128));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, value)));
            Check(next.Revision == 10 && next.Collections.Length == 2 && next.Collections[0].Phase == Journal.ProfileCredited, "Next credit lost prior receipt.");
            string secondKey = Journal.ReceiptKey(next.Items[1].Id), secondValue = Journal.ReceiptValue(next, 1);
            Refuse<InvalidOperationException>(() => journal.ConfirmSavedCredit(project, operation, 1, FullProof(17, secondKey, secondValue, before)));
            var secondAfter = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, next.Collections[1].AfterInfo, next.Collections[1].AfterBytes, null);
            var second = journal.ConfirmSavedCredit(project, operation, 1, FullProof(17, secondKey, secondValue, secondAfter));
            Check(second.Revision == 11 && second.Collections.All(c => c.Phase == Journal.ProfileCredited) && second.Phase == Journal.IssuedSaved,
                "Collection falsely settled world cleanup/cancellation.");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(path)).Contains("\"Removal\":"), "Unset cleanup changed prior canonical credit bytes.");
            CleanupChecks(journal, project, operation, path);
        }
        private static void CleanupChecks(Journal journal, Guid project, Guid operation, string path)
        {
            var original = journal.Read(project);
            string key = Journal.ReceiptKey(original.Items[0].Id), value = Journal.ReceiptValue(original, 0);
            string key1 = Journal.ReceiptKey(original.Items[1].Id), value1 = Journal.ReceiptValue(original, 1);
            var proof = Proof(17, key, value);
            byte[] before = File.ReadAllBytes(path);
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, Guid.NewGuid(), 0, 9, proof));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, -1, 9, proof));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 2, 9, proof));
            foreach (uint generation in new uint[] { 0, 8, uint.MaxValue })
                Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, generation, proof));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9, null));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9, Proof(18, key, value)));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, "pending:" + value)));
            Check(before.SequenceEqual(File.ReadAllBytes(path)), "Rejected cleanup mutated paid rows or profile credit.");
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10), proof));
            var intent = journal.BeginCleanup(project, operation, 0, 9, proof);
            Check(intent.Revision == 12 && intent.Collections[0].Removal.Phase == Journal.CleanupIntent && intent.Collections[0].Removal.SavedGeneration == 0,
                "Cleanup intent falsely claimed saved absence.");
            byte[] pending = File.ReadAllBytes(path);
            Check(journal.BeginCleanup(project, operation, 0, 9, proof).Revision == 12 && pending.SequenceEqual(File.ReadAllBytes(path)), "Exact cleanup intent replay mutated bytes.");
            string body = Encoding.UTF8.GetString(pending, 12, BitConverter.ToInt32(pending, 8));
            string altered = body.Replace("\"SavedGeneration\":0", "\"SavedGeneration\":10");
            Check(body != altered, "Cleanup corruption fixture did not alter the target.");
            using (var file = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(file))
            using (var sha = SHA256.Create())
            { byte[] bytes = Encoding.UTF8.GetBytes(altered); writer.Write(pending, 0, 8); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); }
            Refuse<InvalidDataException>(() => journal.Read(project)); File.WriteAllBytes(path, pending);
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 10, proof));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 0, 9, Proof(17, key, value, new string('D', 128))));
            Refuse<InvalidOperationException>(() => journal.BeginCleanup(project, operation, 1, 9, Proof(17, key1, value1)));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, null, proof));
            foreach (uint generation in new uint[] { 0, 9, 11, uint.MaxValue })
                Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(generation), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10, world: original.World + 1), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10, marker: Journal.ItemMarkerKey + 1), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10, ids: new[] { original.Items[1].Id }), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10, ids: original.Items.Select(i => i.Id).ToArray()), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10), Proof(18, key, value)));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10), Proof(17, key, value, new string('D', 128))));
            Check(pending.SequenceEqual(File.ReadAllBytes(path)), "Rejected cleanup proof mutated intent.");
            var saved = journal.ConfirmCleanup(project, operation, 0, Absence(10), proof);
            Check(saved.Revision == 13 && saved.Collections[0].Removal.Phase == Journal.CleanupSaved && saved.Collections[0].Removal.SavedGeneration == 10 &&
                saved.Collections.All(c => c.Phase == Journal.ProfileCredited) && saved.Phase == Journal.IssuedSaved,
                "Cleanup lost credit proof or falsely settled project cancellation.");
            byte[] terminal = File.ReadAllBytes(path);
            Check(journal.ConfirmCleanup(project, operation, 0, Absence(10), proof).Revision == 13 && terminal.SequenceEqual(File.ReadAllBytes(path)), "Cleanup saved replay mutated bytes.");
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(11), proof));
            Refuse<InvalidOperationException>(() => journal.ConfirmCleanup(project, operation, 0, Absence(10), Proof(17, key, value, new string('D', 128))));
            Check(terminal.SequenceEqual(File.ReadAllBytes(path)), "Changed proof mutated settled cleanup.");
            var next = journal.BeginCleanup(project, operation, 1, 10, Proof(17, key1, value1));
            Check(next.Revision == 14 && next.Collections[0].Removal.Phase == Journal.CleanupSaved && next.Collections[1].Removal.Phase == Journal.CleanupIntent,
                "Next cleanup lost prior saved absence.");
            // Test-only evidence substitute; the complete mapped-world parser has separate checks.
            NativeWorldStorageReadback.AbsenceEvidence Absence(uint generation, long? world = null, int? marker = null, Guid[] ids = null)
                => (NativeWorldStorageReadback.AbsenceEvidence)Activator.CreateInstance(typeof(NativeWorldStorageReadback.AbsenceEvidence),
                    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] {
                        new NativeWorldStorageReadback.AbsenceExpectation(world ?? original.World, marker ?? Journal.ItemMarkerKey, ids ?? new[] { original.Items[0].Id }),
                        36, 1, 0, generation }, null);
        }
        // Test-only journal evidence substitute. Native file parsing is covered separately by ProfileReceiptChecks.
        private static NativePlayerProfileReadback.CustomDataEvidence Proof(long player, string key, string value, string hash = null)
        {
            var evidence = FullProof(player, key, value, null, hash);
            return (NativePlayerProfileReadback.CustomDataEvidence)Activator.CreateInstance(typeof(NativePlayerProfileReadback.CustomDataEvidence),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { evidence }, null);
        }
        private static NativePlayerProfileReadback.Evidence FullProof(long player, string key, string value, BlueprintForeignPartData inventory, string hash = null)
        {
            var map = new ReadOnlyDictionary<string, string>(new Dictionary<string, string> { [key] = value });
            return (NativePlayerProfileReadback.Evidence)Activator.CreateInstance(typeof(NativePlayerProfileReadback.Evidence),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { player, hash ?? new string('C', 128), null, 0, inventory, map }, null);
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
