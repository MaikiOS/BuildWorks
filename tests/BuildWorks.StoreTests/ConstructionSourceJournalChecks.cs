using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionSourceJournalChecks
    {
        internal static void Run(string folder)
        {
            var fixture = File.ReadAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "construction", "bwpp01-legacy-vectors.tsv"))
                .Select(line => line.Split('\t')).ToDictionary(line => line[0]);
            Guid project = new Guid("d7714c25-4b1a-4f65-9a25-25e5efbdbf01"), operation = new Guid("dbaf5bca-32d8-4ee2-b643-69558785a101");
            string hash = new string('A', 64); int cases = 0, sequence = 0;
            var current = Setup(); var journal = current.Journal;
            Check(journal.ReadPayment(project, current.Part) == (null, null, null), "Missing payment was not absent.");
            var supplied = Sources();
            Journal.RequireSourcesCapacity(17, supplied, supplied.Select(_ => new[] { Row(2) }).ToArray()); ++cases;
            var prepared = Prepare(supplied);
            Check(prepared.Phase == Journal.Intent && prepared.RequiresReconciliation && prepared.Revision == 1, "Fresh source intent missing.");
            Check(BlueprintConstructionCancellationAction.Read(current.Plans, project, 1, 17) == ConstructionCancellationAction.Review,
                "Actual cancellation router did not retain BWPP02 intent for review.");
            supplied[1].BeforeBytes = "changed";
            Check(journal.ReadSources(project, current.Part).Sources[1].BeforeBytes == fixture["before"][2], "Caller aliases persisted source intent.");
            byte[] intent = File.ReadAllBytes(current.Path);
            CheckConsumer(prepared);
            Check(Prepare(Sources(), false).Phase == Journal.Intent, "Intent replay changed phase.");
            Refuse<InvalidOperationException>(() => journal.PrepareSources(project, current.Part, operation, 17, "other", hash, hash, Sources(), out _));
            var changed = Sources(); changed[1].ObjectId = 99;
            Refuse<InvalidOperationException>(() => Prepare(changed));
            Refuse<InvalidDataException>(() => journal.Read(project, current.Part));
            Refuse<InvalidDataException>(() => journal.Prepare(project, current.Part, Guid.NewGuid(), 17, Before(), out _));
            Refuse<InvalidOperationException>(() => current.Plans.CancelUnpaid(project, 1, Guid.NewGuid()));
            Refuse<InvalidOperationException>(() => journal.RequireSavedSourcesPayment(project, current.Part, 17));
            Check(File.ReadAllBytes(current.Path).SequenceEqual(intent), "Rejected/replayed source intent changed bytes.");

            foreach (Action<Journal.SourceState[]> mutation in new Action<Journal.SourceState[]>[] {
                a => a[1].AfterBytes = null, a => a[1].Paid = null,
                a => a[1].Paid[0].Paid = 1, a => a[1].Paid[0].Before = 4,
                a => a[1].Paid[0].X = 7, a => a[1].Paid = new[] { Row(2), Row(2) },
                a => a[1].AfterInfo = a[1].BeforeInfo, a => a[1].AfterBytes = a[1].BeforeBytes })
            {
                var bad = Outcomes(0, 2, 0); mutation(bad);
                Refuse<InvalidDataException>(() => Observe(bad, false));
                Check(File.ReadAllBytes(current.Path).SequenceEqual(intent), "Bad source delta changed durable intent.");
            }
            changed = Outcomes(0, 2, 0); changed[1].Paid[0].Properties = changed[1].Paid[0].Properties.Substring(1);
            Refuse<ArgumentException>(() => Observe(changed, false));
            Check(File.ReadAllBytes(current.Path).SequenceEqual(intent), "Malformed source item package changed durable intent.");
            changed = Outcomes(0, 2, 0); var swap = changed[1]; changed[1] = changed[2]; changed[2] = swap;
            Refuse<InvalidOperationException>(() => Observe(changed, false));
            Refuse<InvalidDataException>(() => Observe(Outcomes(0, 2, 0), true));
            Refuse<InvalidDataException>(() => Observe(Outcomes(0, 0, 0), false));
            Refuse<InvalidDataException>(() => Observe(Outcomes(0, 2, 0), false, 101, 1));
            Refuse<InvalidDataException>(() => Observe(Outcomes(0, 2, 0), false, 31, 9, 18));

            var observed = Observe(Outcomes(0, 2, 2), false);
            CheckConsumer(observed);
            Check(observed.Phase == Journal.Observed && observed.Revision == 2 && observed.Sources[0].Paid.Length == 0 &&
                observed.Sources[1].Paid[0].Paid == 2 && observed.Sources[2].Paid[0].Paid == 2, "External-only same-slot costs merged, lost or treated as free.");
            string fingerprint = Journal.FingerprintSources(observed); byte[] pending = File.ReadAllBytes(current.Path);
            Check(Observe(Outcomes(0, 2, 2), false).Revision == 2 && File.ReadAllBytes(current.Path).SequenceEqual(pending), "Observed source replay changed bytes.");
            Refuse<InvalidOperationException>(() => Observe(Outcomes(2, 2, 2), false));
            Refuse<InvalidOperationException>(() => journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 0, 0), true));
            Refuse<InvalidOperationException>(() => journal.RequireSavedSourcesPayment(project, current.Part, 17));
            foreach (Action<Journal.SourcesSavePair> mutation in new Action<Journal.SourcesSavePair>[] {
                p => p.Placement = null, p => p.Sources = null, p => p.Sources = p.Sources.Take(1).ToArray(),
                p => p.Sources[0] = null, p => p.Sources[0].InventoryHash = hash, p => p.Sources[0].Generation = 9,
                p => p.Sources[0].ObjectId = 99, p => p.Sources[0].ObjectUser = 99, p => p.Sources[0].World = 43,
                p => p.Sources[0].Token = Guid.NewGuid(), p => p.Sources = p.Sources.Reverse().ToArray(),
                p => p.Placement.InventoryHash = hash, p => p.Placement.WorldPayloadHash = hash,
                p => p.Placement.ProfileHash = hash, p => p.Placement.Generation = 9 })
            {
                var bad = Proof(observed); mutation(bad);
                Refuse<InvalidDataException>(() => journal.ConfirmSavedSources(project, current.Part, operation, bad));
                Check(File.ReadAllBytes(current.Path).SequenceEqual(pending), "Incomplete/wrong source save committed or changed bytes.");
            }
            var committed = journal.ConfirmSavedSources(project, current.Part, operation, Proof(observed));
            CheckConsumer(committed);
            Check(committed.Phase == Journal.Committed && committed.Revision == 3 && !committed.RequiresReconciliation &&
                Journal.FingerprintSources(committed) != fingerprint && committed.Receipt.SequenceEqual(observed.Receipt), "Source commit failed or changed native receipt.");
            byte[] terminal = File.ReadAllBytes(current.Path);
            Check(journal.ConfirmSavedSources(project, current.Part, operation, Proof(observed)).Revision == 3 &&
                Observe(Outcomes(0, 2, 2), false).Phase == Journal.Committed && File.ReadAllBytes(current.Path).SequenceEqual(terminal), "Committed source replay changed bytes.");
            Refuse<InvalidOperationException>(() => journal.RequireSavedSourcesPayment(project, current.Part, 18));
            var parent = journal.RequireSavedSourcesPayment(project, current.Part, 17); parent.Sources[1].Paid[0].Paid = 99;
            parent.Saved.Sources[0].InventoryHash = "changed";
            Check(journal.RequireSavedSourcesPayment(project, current.Part, 17).Sources[1].Paid[0].Paid == 2, "Saved all-source authority aliases stored rows.");

            string json = Encoding.UTF8.GetString(terminal, 12, BitConverter.ToInt32(terminal, 8));
            Frame(json.Substring(0, json.Length - 1) + ",\"Unknown\":0}");
            Refuse<InvalidDataException>(() => journal.ReadSources(project, current.Part));
            File.WriteAllBytes(current.Path, terminal.Take(terminal.Length - 1).ToArray());
            Refuse<InvalidDataException>(() => journal.ReadSources(project, current.Part));
            var corrupt = (byte[])terminal.Clone(); corrupt[20] ^= 1; File.WriteAllBytes(current.Path, corrupt);
            Refuse<InvalidDataException>(() => journal.ReadPayment(project, current.Part));
            Refuse<InvalidDataException>(() => BlueprintConstructionCancellationAction.Read(current.Plans, project, 1, 17));
            Refuse<InvalidDataException>(() => journal.ReadSources(project, current.Part)); File.WriteAllBytes(current.Path, terminal);
            corrupt = (byte[])terminal.Clone(); corrupt[0] ^= 1; File.WriteAllBytes(current.Path, corrupt);
            Refuse<InvalidDataException>(() => journal.ReadPayment(project, current.Part));
            Refuse<InvalidDataException>(() => BlueprintConstructionCancellationAction.Read(current.Plans, project, 1, 17));
            File.WriteAllBytes(current.Path, terminal.Take(12).ToArray());
            Refuse<InvalidDataException>(() => journal.ReadPayment(project, current.Part));
            File.WriteAllBytes(current.Path, terminal);
            string extra = Path.Combine(current.Plans.DirectoryPath, project.ToString("N") + "." + Guid.NewGuid().ToString("N") + ".bwpart");
            File.WriteAllBytes(extra, terminal.Take(12).ToArray());
            Refuse<InvalidDataException>(() => Journal.HasExecutionEvidence(current.Plans, current.Plans.Load(project))); File.Delete(extra);

            current = Setup(); journal = current.Journal; Prepare(Sources());
            Refuse<InvalidOperationException>(() => journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 0, 0), false));
            Refuse<InvalidOperationException>(() => journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 0, 0).Take(2).ToArray(), true));
            changed = Outcomes(0, 0, 0); changed[1].AfterBytes = null;
            Refuse<InvalidDataException>(() => journal.RecordNoEffectSources(project, current.Part, operation, changed, true));
            Refuse<InvalidDataException>(() => journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 2, 0), true));
            Check(journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 0, 0), true).Phase == Journal.NoEffect, "Complete unchanged sources did not produce no-effect.");
            CheckConsumer(journal.ReadSources(project, current.Part));
            Check(journal.RecordNoEffectSources(project, current.Part, operation, Outcomes(0, 0, 0), true).Revision == 2, "No-effect replay advanced revision.");
            Refuse<InvalidOperationException>(() => Observe(Outcomes(0, 2, 0), false));
            Check(journal.PrepareSources(project, current.Part, Guid.NewGuid(), 17, "fixture.provider", hash, hash, Sources(), out bool retried).Revision == 3 && retried,
                "Proven all-source no-effect refused a fresh operation.");

            current = Setup(); journal = current.Journal; Prepare(Sources()); observed = Observe(Outcomes(0, 0, 0), true);
            journal.ConfirmSavedSources(project, current.Part, operation, Proof(observed));
            Refuse<InvalidOperationException>(() => journal.RequireSavedSourcesPayment(project, current.Part, 17));
            current = Setup(); journal = current.Journal; Prepare(Sources()); observed = Observe(Outcomes(2, 2, 2), false);
            Check(observed.Sources.Sum(source => source.Paid.Sum(row => row.Paid)) == 6, "Mixed same-slot payment lost a source.");
            current = Setup(); journal = current.Journal; Prepare(Sources());
            changed = Outcomes(0, 2, 2);
            foreach (var source in changed.Skip(1))
            { source.Paid[0].Paid = 3; source.AfterInfo = "8:4:0:0"; source.AfterBytes = Convert.ToBase64String(new byte[] { 109, 0, 0, 0, 0, 0 }); }
            Check(Observe(changed, false).Sources.Skip(1).All(source => source.Paid[0].Paid == 3), "Fully removed external stacks lost their debit.");

            current = Setup(); journal = current.Journal;
            var largest = Enumerable.Range(0, 128).Select(index => new Journal.SourceState { Token = index == 0 ? Guid.Empty : new Guid(index, 0, 0, new byte[8]),
                ObjectUser = index == 0 ? 0 : index, ObjectId = (uint)index, PrefabHash = index == 0 ? 0 : 1, Creator = 17,
                BeforeInfo = fixture["before"][1], BeforeBytes = fixture["before"][2] }).ToArray();
            Journal.RequireSourcesCapacity(17, largest, largest.Select(_ => Array.Empty<Journal.PaidRow>()).ToArray()); ++cases;
            Check(Prepare(largest).Sources.Length == 128, "Valid maximum source manifest was rejected.");

            current = Setup(); journal = current.Journal;
            foreach (Action<Journal.SourceState[]> mutation in new Action<Journal.SourceState[]>[] {
                a => a[0].Token = Guid.NewGuid(), a => a[0].ObjectId = 1, a => a[1].Token = Guid.Empty,
                a => a[2].Token = a[1].Token, a => { a[2].ObjectUser = a[1].ObjectUser; a[2].ObjectId = a[1].ObjectId; },
                a => a[1].Creator = 18, a => a[1].ObjectId = 0, a => a[1].PrefabHash = 0,
                a => a[1] = null, a => a[1].Paid = new[] { Row(2) }, a => a[1].AfterBytes = fixture["before"][2] })
            { var bad = Sources(); mutation(bad); Refuse<InvalidDataException>(() => Prepare(bad)); }
            changed = Sources(); changed[1].BeforeBytes = "bad"; Refuse<ArgumentException>(() => Prepare(changed));
            Refuse<InvalidDataException>(() => Prepare(Sources().Take(1).ToArray()));
            Refuse<InvalidDataException>(() => Prepare(Enumerable.Repeat(Sources()[0], 129).ToArray()));
            Refuse<InvalidDataException>(() => journal.PrepareSources(project, current.Part, operation, 17, "fixture.provider", "bad", hash, Sources(), out _));
            var enormous = Row(2); enormous.Properties = new string('x', 4 * 1024 * 1024);
            Refuse<InvalidDataException>(() => Journal.RequireSourcesCapacity(17, Sources(), new[] { new[] { enormous }, Array.Empty<Journal.PaidRow>(), Array.Empty<Journal.PaidRow>() }));
            Refuse<InvalidDataException>(() => Journal.RequireSourcesCapacity(17, Sources(), new[] { Enumerable.Repeat(Row(2), 4097).ToArray(), Array.Empty<Journal.PaidRow>(), Array.Empty<Journal.PaidRow>() }));
            Check(!File.Exists(current.Path), "Refused source enrollment created a journal.");
            journal.Prepare(project, current.Part, operation, 17, Before(), out _);
            var legacyPayment = journal.ReadPayment(project, current.Part);
            Check(legacyPayment.Legacy?.Phase == Journal.Intent && legacyPayment.Sources == null, "Legacy payment was reinterpreted as a source record.");
            Refuse<InvalidDataException>(() => journal.ReadSources(project, current.Part));
            Refuse<InvalidDataException>(() => Prepare(Sources()));
            Console.WriteLine("PASS BWPP02 all-source journal: " + cases + " assertions; durable intent/deltas/save coverage/replay/format isolation. Hashes and authority are fixtures; no native debit/save/refund/provider/network proof.");

            void CheckConsumer(Journal.SourcesRecord expected)
            {
                byte[] beforeRead = File.ReadAllBytes(current.Path);
                var payment = journal.ReadPayment(project, current.Part);
                Check(payment.Legacy == null && payment.Sources?.Phase == expected.Phase &&
                    Journal.FingerprintSources(payment.Sources) == Journal.FingerprintSources(expected), "Typed consumer lost source identity or fields.");
                Check(BlueprintConstructionCancellationAction.Read(current.Plans, project, 1, 17) == ConstructionCancellationAction.Review,
                    "Source phase escaped the authoritative review route.");
                Check(Journal.HasExecutionEvidence(current.Plans, current.Plans.Load(project)) == (expected.Phase != Journal.NoEffect), "Source execution evidence phase differs.");
                Check(File.ReadAllBytes(current.Path).SequenceEqual(beforeRead), "Read/phase routing modified the journal.");
            }
            (Plans Plans, Journal Journal, Guid Part, string Path) Setup()
            {
                var plans = new Plans(Path.Combine(folder, (++sequence).ToString()), 42);
                File.WriteAllBytes(Path.Combine(plans.DirectoryPath, project.ToString("N") + ".bwproject"), Convert.FromBase64String(fixture["plan"][1]));
                Guid part = plans.Load(project).Parts.Single().Id;
                return (plans, new Journal(plans), part, Path.Combine(plans.DirectoryPath, project.ToString("N") + "." + part.ToString("N") + ".bwpart"));
            }
            Journal.SourceState[] Sources() => Enumerable.Range(0, 3).Select(index => new Journal.SourceState {
                Token = index == 0 ? Guid.Empty : new Guid(index, 0, 0, new byte[8]), ObjectUser = index == 0 ? 0 : 100 + index,
                ObjectId = (uint)index, PrefabHash = index == 0 ? 0 : "piece_chest_wood".GetStableHashCode(), Creator = 17,
                BeforeInfo = fixture["before"][1], BeforeBytes = fixture["before"][2] }).ToArray();
            Journal.SourceState[] Outcomes(params int[] paid)
            {
                var result = Sources();
                for (int i = 0; i < result.Length; ++i) { result[i].AfterInfo = fixture[paid[i] == 0 ? "before" : "after"][1];
                    result[i].AfterBytes = fixture[paid[i] == 0 ? "before" : "after"][2]; result[i].Paid = paid[i] == 0 ? Array.Empty<Journal.PaidRow>() : new[] { Row(paid[i]) }; }
                return result;
            }
            Journal.PaidRow Row(int paid) => new Journal.PaidRow { Prefab = "Wood", Properties = fixture["properties"][1], Before = 3, Paid = paid, X = 0, Y = 0, Quality = 1 };
            OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData Before() => new OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData(
                OstrixMods.BuildWorks.Geometry.BlueprintForeignPartData.NativeContainerFormat, fixture["before"][1], fixture["before"][2], null);
            Journal.SourcesRecord Prepare(Journal.SourceState[] sources, bool expectedCreated = true)
            {
                var result = journal.PrepareSources(project, current.Part, operation, 17, "fixture.provider", hash, hash, sources, out bool created);
                Check(created == expectedCreated, "Source intent replay flag differs."); return result;
            }
            Journal.SourcesRecord Observe(Journal.SourceState[] sources, bool free, long user = 31, uint id = 9, long creator = 17) =>
                journal.ObserveSources(project, current.Part, operation, user, id, "woodwall".GetStableHashCode(), creator, sources, free);
            Journal.SourcesSavePair Proof(Journal.SourcesRecord record) => new Journal.SourcesSavePair {
                Placement = new Journal.SavePair { BeforeGeneration = 7, Generation = 8, ProfileHash = new string('A', 128),
                    InventoryHash = Digest(Convert.FromBase64String(record.Sources[0].AfterBytes)), WorldPayloadHash = Digest(record.Receipt) },
                Sources = record.Sources.Skip(1).Select(source => new Journal.SourceSave { Token = source.Token, ObjectUser = source.ObjectUser,
                    ObjectId = source.ObjectId, World = 42, Generation = 8, InventoryHash = Digest(Convert.FromBase64String(source.AfterBytes)) }).ToArray() };
            void Frame(string payload)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload);
                using (var stream = File.Create(current.Path)) using (var writer = new BinaryWriter(stream))
                { writer.Write(Encoding.ASCII.GetBytes("BWPP02\r\n")); writer.Write(bytes.Length); writer.Write(bytes); using (var sha = SHA256.Create()) writer.Write(sha.ComputeHash(bytes)); }
            }
            void Check(bool condition, string message) { ++cases; if (!condition) throw new Exception(message); }
            void Refuse<T>(Action action) where T : Exception { ++cases; try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name); }
        }
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
