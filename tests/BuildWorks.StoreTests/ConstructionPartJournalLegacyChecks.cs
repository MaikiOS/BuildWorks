using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using B = OstrixMods.BuildWorks.CompositeBlueprintStore;
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Journal = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static class ConstructionPartJournalLegacyChecks
    {
        private static readonly Guid Project = new Guid("d7714c25-4b1a-4f65-9a25-25e5efbdbf01");
        private static readonly Guid Operation = new Guid("dbaf5bca-32d8-4ee2-b643-69558785a101");
        private const string Fixture = "bwpp01-legacy-vectors.tsv";

        internal static void Run(string folder)
        {
            string fixture = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", "construction", Fixture);
            string[][] lines = File.ReadAllLines(fixture).Select(line => line.Split('\t')).ToArray();
            var vectors = lines.ToDictionary(line => line[0]);
            Check(vectors.Count == 10, "Legacy vector set changed.");
            var plans = new Plans(folder, 42);
            File.WriteAllBytes(Path.Combine(folder, Project.ToString("N") + ".bwproject"), Convert.FromBase64String(vectors["plan"][1]));
            var plan = plans.Load(Project); Guid part = plan.Parts.Single().Id;
            string path = Path.Combine(folder, Project.ToString("N") + "." + part.ToString("N") + ".bwpart");
            var before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, vectors["before"][1], vectors["before"][2], null);
            var after = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, vectors["after"][1], vectors["after"][2], null);
            var journal = new Journal(plans);
            journal.Prepare(Project, part, Operation, 17, before, out bool created);
            Check(created, "Legacy fixture did not create a fresh intent."); Verify("intent");
            Observe(after, false, new[] { new Journal.PaidRow { Prefab = "Wood", Properties = vectors["properties"][1], Before = 3, Paid = 2, X = 0, Y = 0, Quality = 1 } });
            Verify("observed-paid"); Commit(); Verify("committed-paid");
            Reset(); journal.RecordNoEffect(Project, part, Operation, before, true); Verify("no-effect");
            Reset(); Observe(before, true, Array.Empty<Journal.PaidRow>()); Verify("observed-free"); Commit(); Verify("committed-free");
            Console.WriteLine("PASS BWPP01 legacy vectors: six exact framed files, canonical fingerprints, receipts, detached reads and replay; synthetic save hashes only, no game/save/provider proof.");

            void Verify(string name)
            {
                string[] row = vectors[name]; byte[] bytes = Convert.FromBase64String(row[1]);
                Check(File.ReadAllBytes(path).SequenceEqual(bytes), name + " framing/canonical bytes changed.");
                var record = new Journal(plans).Read(Project, part);
                Check(Journal.Fingerprint(record) == row[2], name + " canonical fingerprint changed.");
                Check((record.Receipt == null ? "-" : Convert.ToBase64String(record.Receipt)) == row[3], name + " receipt changed.");
                if (record.Phase == Journal.Intent) journal.Prepare(Project, part, Operation, 17, before, out _);
                else if (record.Phase == Journal.NoEffect) journal.RecordNoEffect(Project, part, Operation, before, true);
                else
                {
                    Observe(record.CostFree ? before : after, record.CostFree, record.Paid);
                    if (record.Phase == Journal.Committed) journal.ConfirmSaved(Project, part, Operation, record.Saved);
                }
                Check(File.ReadAllBytes(path).SequenceEqual(bytes), name + " replay rewrote legacy evidence.");
            }
            void Reset() => File.WriteAllBytes(path, Convert.FromBase64String(vectors["intent"][1]));
            Journal.Record Observe(BlueprintForeignPartData inventory, bool free, Journal.PaidRow[] paid) =>
                journal.Observe(Project, part, Operation, 31, 9, "woodwall".GetStableHashCode(), 17, inventory, free, paid);
            void Commit()
            {
                var record = journal.Read(Project, part);
                journal.ConfirmSaved(Project, part, Operation, new Journal.SavePair { BeforeGeneration = 7, Generation = 8,
                    ProfileHash = new string('A', 128), InventoryHash = Digest(Convert.FromBase64String(record.AfterBytes)), WorldPayloadHash = Digest(record.Receipt) });
            }
        }

        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
    }
}
