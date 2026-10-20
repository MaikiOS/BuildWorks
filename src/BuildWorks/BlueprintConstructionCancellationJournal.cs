using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Plan = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Removal evidence only. No terminal project cancellation or resource issuance is permitted here.
    internal sealed class BlueprintConstructionCancellationJournal
    {
        internal const int Prepared = 1, RemovalIntent = 2, RemovedSaved = 3;
        private const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BWCR01\r\n");
        private readonly Plan plans;
        [DataContract] internal sealed class Entry
        {
            [DataMember(Order = 0)] internal Guid Part;
            [DataMember(Order = 1)] internal Guid Operation;
            [DataMember(Order = 2)] internal int Phase;
            [DataMember(Order = 3)] internal string Hash;
        }
        [DataContract] internal sealed class SaveProof
        {
            [DataMember(Order = 0)] internal uint BeforeGeneration;
            [DataMember(Order = 1)] internal uint Generation;
            [DataMember(Order = 2)] internal string ProfileHash;
            [DataMember(Order = 3)] internal string InventoryHash;
        }
        [DataContract] internal sealed class Record
        {
            [DataMember(Order = 0)] internal Guid Project;
            [DataMember(Order = 1)] internal Guid Operation;
            [DataMember(Order = 2)] internal long World;
            [DataMember(Order = 3)] internal long Player;
            [DataMember(Order = 4)] internal long PlanRevision;
            [DataMember(Order = 5)] internal string PlanHash;
            [DataMember(Order = 6)] internal long Revision;
            [DataMember(Order = 7)] internal int Phase;
            [DataMember(Order = 8)] internal Entry[] Parts;
            [DataMember(Order = 9)] internal uint BeforeGeneration;
            [DataMember(Order = 10)] internal SaveProof Saved;
        }

        internal BlueprintConstructionCancellationJournal(Plan plans) { this.plans = plans ?? throw new ArgumentNullException(nameof(plans)); }
        internal static void RequireNotStarted(Plan plans, Guid project)
        {
            if (File.Exists(new BlueprintConstructionCancellationJournal(plans).PathFor(project)))
                throw new InvalidOperationException("Project cancellation is pending; construction and unpaid cancellation are frozen.");
        }
        internal Record Prepare(Guid project, long revision, Guid operation, long player)
        {
            if (operation == Guid.Empty || player == 0) throw new ArgumentException("Cancellation operation and player are required.");
            using (plans.Lock(project))
            {
                var plan = plans.Load(project);
                var current = Read(project);
                if (current != null)
                {
                    if (current.Operation != operation || current.Player != player || current.PlanRevision != revision)
                        throw new InvalidOperationException("Cancellation replay has different inputs.");
                    return current;
                }
                if (plan.Cancelled || plan.Revision != revision) throw new InvalidOperationException("Project revision/state changed.");
                var entries = Snapshot(plan, player);
                if (!entries.Any(p => p.Phase == Parts.Committed)) throw new InvalidOperationException("No committed construction to remove.");
                var created = new Record { Project = project, Operation = operation, World = plan.World, Player = player,
                    PlanRevision = revision, PlanHash = Digest(plan.BlueprintBytes), Revision = 1, Phase = Prepared, Parts = entries };
                Write(created, false); return Read(project);
            }
        }
        internal Record BeginRemoval(Guid project, Guid operation, uint beforeGeneration)
        {
            using (plans.Lock(project))
            {
                var current = RequireOperation(project, operation);
                if (beforeGeneration == 0 || beforeGeneration == uint.MaxValue) throw new ArgumentException("Invalid removal save generation.");
                if (current.Phase != Prepared)
                {
                    if (current.BeforeGeneration != beforeGeneration) throw new InvalidOperationException("Removal generation changed.");
                    return current;
                }
                current.Phase = RemovalIntent; current.BeforeGeneration = beforeGeneration; current.Revision++;
                Write(current, true); return Read(project);
            }
        }
        internal Record ConfirmRemovedSaved(Guid project, Guid operation, SaveProof proof)
        {
            using (plans.Lock(project))
            {
                var current = RequireOperation(project, operation);
                if (current.Phase == Prepared) throw new InvalidOperationException("Removal intent is required before saved absence.");
                if (current.Phase == RemovedSaved)
                {
                    if (!Encode(proof).SequenceEqual(Encode(current.Saved))) throw new InvalidOperationException("Removal save replay differs.");
                    return current;
                }
                current.Saved = proof; current.Phase = RemovedSaved; current.Revision++;
                Write(current, true); return Read(project);
            }
        }
        internal Record Read(Guid project)
        {
            string path = PathFor(project);
            if (!File.Exists(path)) return null;
            CheckFile(path);
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44L || !reader.ReadBytes(8).SequenceEqual(Magic))
                    throw new InvalidDataException("Invalid construction removal framing.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid construction removal size.");
                byte[] bytes = reader.ReadBytes(size);
                using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(reader.ReadBytes(32)))
                    throw new InvalidDataException("Construction removal checksum differs.");
                Record record;
                using (var input = new MemoryStream(bytes, false)) record = (Record)Serializer(typeof(Record)).ReadObject(input);
                Validate(project, record);
                if (!Encode(record).SequenceEqual(bytes)) throw new InvalidDataException("Noncanonical construction removal fields.");
                return record;
            }
        }
        private Record RequireOperation(Guid project, Guid operation)
        {
            var record = Read(project);
            if (record == null || operation == Guid.Empty || record.Operation != operation) throw new InvalidOperationException("Cancellation operation differs.");
            return record;
        }
        private Entry[] Snapshot(Plan.Record plan, long player)
        {
            var journal = new Parts(plans);
            var entries = plan.Parts.Select(part => journal.Read(plan.Id, part.Id)).Where(r => r != null).ToArray();
            if (entries.Any(r => r.RequiresReconciliation || r.Phase == Parts.Committed && r.Player != player))
                throw new InvalidOperationException("Unresolved construction or mixed payers require reconciliation before cancellation.");
            string[] files = Directory.GetFiles(plans.DirectoryPath, plan.Id.ToString("N") + ".*.bwpart");
            if (files.Length != entries.Length) throw new InvalidDataException("Unexpected construction receipt prevents cancellation.");
            return entries.Select(r => new Entry { Part = r.Part, Operation = r.Operation, Phase = r.Phase, Hash = Parts.Fingerprint(r) }).ToArray();
        }
        private void Validate(Guid project, Record r)
        {
            var plan = plans.Load(project);
            if (r == null || r.Project != project || r.Operation == Guid.Empty || r.Player == 0 || plan.Cancelled ||
                r.World != plan.World || r.PlanRevision != plan.Revision || r.PlanHash != Digest(plan.BlueprintBytes) ||
                r.Phase < Prepared || r.Phase > RemovedSaved || r.Revision != r.Phase || r.Parts == null ||
                !r.Parts.Any(p => p?.Phase == Parts.Committed)) throw new InvalidDataException("Construction removal identity/state differs.");
            if (!Encode(r.Parts).SequenceEqual(Encode(Snapshot(plan, r.Player)))) throw new InvalidDataException("Linked construction receipts changed.");
            if (r.Phase == Prepared ? r.BeforeGeneration != 0 : r.BeforeGeneration == 0 || r.BeforeGeneration == uint.MaxValue)
                throw new InvalidDataException("Removal generation is missing or premature.");
            if (r.Phase == RemovedSaved)
            {
                if (r.Saved == null || r.Saved.BeforeGeneration != r.BeforeGeneration || r.Saved.Generation != r.BeforeGeneration + 1 ||
                    !Hash(r.Saved.ProfileHash, 128) || !Hash(r.Saved.InventoryHash, 64)) throw new InvalidDataException("Removal paired-save evidence differs.");
            }
            else if (r.Saved != null) throw new InvalidDataException("Removal falsely claims saved absence.");
        }
        private void Write(Record record, bool replace)
        {
            Validate(record.Project, record); byte[] bytes = Encode(record);
            string target = PathFor(record.Project), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                using (var sha = SHA256.Create())
                { writer.Write(Magic); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); writer.Flush(); file.Flush(true); }
                CheckFile(target); CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath);
                if (replace) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private string PathFor(Guid project)
        {
            if (project == Guid.Empty) throw new ArgumentException("Project identity is required.");
            return Path.Combine(CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath), project.ToString("N") + ".bwremoval");
        }
        private static void CheckFile(string path)
        { if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Removal journal cannot be a reparse point."); }
        private static byte[] Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            { Serializer(typeof(T)).WriteObject(stream, value); if (stream.Length > MaximumBytes) throw new InvalidDataException("Removal journal exceeds budget."); return stream.ToArray(); }
        }
        private static DataContractJsonSerializer Serializer(Type type) => new DataContractJsonSerializer(type,
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 262144 });
        private static bool Hash(string text, int size) => text != null && text.Length == size && text.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
