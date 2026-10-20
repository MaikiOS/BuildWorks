using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using Plan = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    // Detached receipts only: this journal cannot debit, place, authorize, settle a crash or issue a refund.
    internal sealed class BlueprintConstructionPartJournal
    {
        internal const int Intent = 1, Observed = 2, Committed = 3, NoEffect = 4;
        private const int MaximumBytes = 8 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BWPP01\r\n");
        private readonly Plan plans;
        [DataContract] internal sealed class PaidRow
        {
            [DataMember(Order = 0)] internal string Prefab;
            [DataMember(Order = 1)] internal string Properties;
            [DataMember(Order = 2)] internal int Before;
            [DataMember(Order = 3)] internal int Paid;
            [DataMember(Order = 4)] internal int X;
            [DataMember(Order = 5)] internal int Y;
            [DataMember(Order = 6)] internal int Variant;
            [DataMember(Order = 7)] internal int Quality;
        }
        [DataContract] internal sealed class SavePair
        {
            [DataMember(Order = 0)] internal uint BeforeGeneration;
            [DataMember(Order = 1)] internal uint Generation;
            [DataMember(Order = 2)] internal string ProfileHash;
            [DataMember(Order = 3)] internal string InventoryHash;
            [DataMember(Order = 4)] internal string WorldPayloadHash;
        }
        [DataContract] internal sealed class Record
        {
            [DataMember(Order = 0)] internal Guid Project;
            [DataMember(Order = 1)] internal Guid Part;
            [DataMember(Order = 2)] internal Guid Operation;
            [DataMember(Order = 3)] internal long World;
            [DataMember(Order = 4)] internal long Player;
            [DataMember(Order = 5)] internal long Revision;
            [DataMember(Order = 6)] internal int Phase;
            [DataMember(Order = 7)] internal string PlanHash;
            [DataMember(Order = 8)] internal string BeforeInfo;
            [DataMember(Order = 9)] internal string BeforeBytes;
            [DataMember(Order = 10)] internal string AfterInfo;
            [DataMember(Order = 11)] internal string AfterBytes;
            [DataMember(Order = 12)] internal long ObjectUser;
            [DataMember(Order = 13)] internal uint ObjectId;
            [DataMember(Order = 14)] internal int PrefabHash;
            [DataMember(Order = 15)] internal long Creator;
            [DataMember(Order = 16)] internal bool CostFree;
            [DataMember(Order = 17)] internal PaidRow[] Paid = Array.Empty<PaidRow>();
            [DataMember(Order = 18)] internal byte[] Receipt;
            [DataMember(Order = 19)] internal SavePair Saved;
            internal bool RequiresReconciliation => Phase == Intent || Phase == Observed;
        }

        internal BlueprintConstructionPartJournal(Plan plans) { this.plans = plans ?? throw new ArgumentNullException(nameof(plans)); }

        internal static void RequireCapacity(BlueprintForeignPartData before, PaidRow[] possibleDebits)
        {
            ValidateInventory(before?.Info, before?.Zdo);
            if (possibleDebits == null || possibleDebits.Length > 4096 || possibleDebits.Any(row => row?.Prefab == null || row.Properties == null))
                throw new InvalidDataException("Construction debit preflight is incomplete.");
            // Actual receipts have fixed-size hashes; reserve more than their framing and each row before native debit.
            long receipt = 1024 + possibleDebits.Sum(row => 128L + Encoding.UTF8.GetByteCount(row.Prefab));
            long bytes = before.Zdo.Length * 2L + possibleDebits.Sum(row => (long)row.Properties.Length);
            if (receipt > 65536 || bytes > MaximumBytes / 2) throw new InvalidDataException("Construction evidence exceeds its preflight budget.");
        }

        internal Record Prepare(Guid project, Guid part, Guid operation, long player,
            BlueprintForeignPartData inventory, out bool newlyPrepared)
        {
            newlyPrepared = false;
            if (operation == Guid.Empty || player == 0) throw new ArgumentException("Operation/player are required.");
            ValidateInventory(inventory?.Info, inventory?.Zdo);
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part);
                var current = Read(project, part);
                if (current != null && current.Operation == operation)
                {
                    if (current.Player != player || current.BeforeInfo != inventory.Info || current.BeforeBytes != inventory.Zdo)
                        throw new InvalidOperationException("Repeated operation has different inputs.");
                    return current;
                }
                if (current != null && current.Phase != NoEffect) throw new InvalidOperationException("Part already has execution evidence; no automatic replay.");
                var created = new Record { Project = project, Part = part, Operation = operation, World = plan.World, Player = player,
                    Revision = checked((current?.Revision ?? 0) + 1), Phase = Intent, PlanHash = Digest(plan.BlueprintBytes),
                    BeforeInfo = inventory.Info, BeforeBytes = inventory.Zdo };
                Write(plan, created, current != null); newlyPrepared = true; return Read(project, part);
            }
        }

        internal Record Observe(Guid project, Guid part, Guid operation, long objectUser, uint objectId, int prefab, long creator,
            BlueprintForeignPartData after, bool costFree, PaidRow[] paid)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireOperation(project, part, operation);
                if (current.Phase == NoEffect) throw new InvalidOperationException("No-effect operation cannot become a placement.");
                var candidate = Clone(current);
                candidate.ObjectUser = objectUser; candidate.ObjectId = objectId; candidate.PrefabHash = prefab; candidate.Creator = creator;
                candidate.AfterInfo = after?.Info; candidate.AfterBytes = after?.Zdo; candidate.CostFree = costFree;
                candidate.Paid = paid ?? throw new ArgumentNullException(nameof(paid));
                ValidateDebits(candidate);
                candidate.Receipt = Receipt(plan, candidate);
                if (current.Phase == Observed || current.Phase == Committed)
                {
                    candidate.Revision = current.Revision; candidate.Phase = current.Phase; candidate.Saved = current.Saved;
                    if (!Encode(candidate).SequenceEqual(Encode(current))) throw new InvalidOperationException("Observed placement differs from durable evidence.");
                    return current;
                }
                candidate.Revision = checked(current.Revision + 1); candidate.Phase = Observed;
                Write(plan, candidate, true); return Read(project, part);
            }
        }

        internal Record ConfirmSaved(Guid project, Guid part, Guid operation, SavePair evidence)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireOperation(project, part, operation);
                if (current.Phase != Observed && current.Phase != Committed) throw new InvalidOperationException("Placement was not observed.");
                var candidate = Clone(current); candidate.Saved = evidence;
                if (current.Phase == Committed)
                {
                    if (!Encode(candidate).SequenceEqual(Encode(current))) throw new InvalidOperationException("Save confirmation differs.");
                    return current;
                }
                candidate.Phase = Committed; candidate.Revision = checked(current.Revision + 1);
                Write(plan, candidate, true); return Read(project, part);
            }
        }

        internal Record RecordNoEffect(Guid project, Guid part, Guid operation, BlueprintForeignPartData unchanged,
            bool freshObjectAbsent)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireOperation(project, part, operation);
                if (current.Phase != Intent && current.Phase != NoEffect || !freshObjectAbsent || unchanged == null ||
                    unchanged.Info != current.BeforeInfo || unchanged.Zdo != current.BeforeBytes)
                    throw new InvalidOperationException("No-effect result requires exact unchanged inventory and fresh-object absence.");
                if (current.Phase == NoEffect) return current;
                current.Phase = NoEffect; current.Revision = checked(current.Revision + 1);
                Write(plan, current, true); return Read(project, part);
            }
        }

        internal Record Read(Guid project, Guid part)
        {
            string path = PathFor(project, part); if (!File.Exists(path)) return null;
            return ReadFile(path, plans.Load(project), part);
        }
        internal static bool HasExecutionEvidence(Plan plans, Plan.Record plan)
        {
            var journal = new BlueprintConstructionPartJournal(plans);
            foreach (string path in Directory.EnumerateFiles(plans.DirectoryPath, plan.Id.ToString("N") + ".*.bwpart"))
            {
                string[] names = Path.GetFileName(path).Split('.');
                if (names.Length != 3 || !Guid.TryParseExact(names[1], "N", out var part)) throw new InvalidDataException("Malformed part receipt filename.");
                if (journal.Read(plan.Id, part).Phase != NoEffect) return true;
            }
            return false;
        }

        private Plan.Record ActivePlan(Guid project, Guid part)
        {
            BlueprintConstructionCancellationJournal.RequireNotStarted(plans, project);
            var plan = plans.Load(project);
            if (plan.Cancelled || !plan.Parts.Any(p => p.Id == part)) throw new InvalidOperationException("Project/part is not active.");
            return plan;
        }
        private Record RequireOperation(Guid project, Guid part, Guid operation)
        {
            var record = Read(project, part);
            if (record == null || operation == Guid.Empty || record.Operation != operation) throw new InvalidOperationException("Construction operation differs.");
            return record;
        }
        private string PathFor(Guid project, Guid part)
        {
            if (project == Guid.Empty || part == Guid.Empty) throw new ArgumentException("Project/part identity is required.");
            return Path.Combine(CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath), project.ToString("N") + "." + part.ToString("N") + ".bwpart");
        }
        private Record ReadFile(string path, Plan.Record plan, Guid part)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Part journal cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44 || !reader.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Invalid part journal framing.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid part journal size.");
                byte[] bytes = reader.ReadBytes(size), hash = reader.ReadBytes(32);
                using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(hash)) throw new InvalidDataException("Part journal checksum differs.");
                Record record;
                using (var input = new MemoryStream(bytes, false)) record = (Record)Serializer().ReadObject(input);
                Validate(plan, part, record);
                if (!Encode(record).SequenceEqual(bytes)) throw new InvalidDataException("Unknown or noncanonical part journal fields.");
                return record;
            }
        }
        private void Write(Plan.Record plan, Record record, bool replace)
        {
            Validate(plan, record.Part, record); byte[] bytes = Encode(record);
            string target = PathFor(plan.Id, record.Part), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                using (var sha = SHA256.Create())
                { writer.Write(Magic); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); writer.Flush(); file.Flush(true); }
                if (!Encode(ReadFile(temp, plan, record.Part)).SequenceEqual(bytes)) throw new InvalidDataException("Prepared part journal changed before commit.");
                CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath);
                if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Part journal target changed.");
                if (replace) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }

        private static void Validate(Plan.Record plan, Guid part, Record r)
        {
            if (r == null || r.Project != plan.Id || r.Part != part || !plan.Parts.Any(p => p.Id == part) || r.World != plan.World ||
                r.Operation == Guid.Empty || r.Player == 0 || r.Revision < 1 || r.Phase < Intent || r.Phase > NoEffect ||
                r.PlanHash != Digest(plan.BlueprintBytes) || r.Paid == null || r.Paid.Length > 4096)
                throw new InvalidDataException("Part journal identity/plan/state differs.");
            long budget = (long)(r.BeforeBytes?.Length ?? 0) + (r.AfterBytes?.Length ?? 0) + r.Paid.Sum(p => (long)(p?.Properties?.Length ?? 0));
            if (budget > MaximumBytes / 2) throw new InvalidDataException("Part journal data exceeds its budget.");
            if (r.Phase == Intent || r.Phase == NoEffect)
            {
                ValidateInventory(r.BeforeInfo, r.BeforeBytes);
                if (r.AfterInfo != null || r.AfterBytes != null || r.ObjectUser != 0 || r.ObjectId != 0 || r.PrefabHash != 0 ||
                    r.Creator != 0 || r.CostFree || r.Paid.Length != 0 || r.Receipt != null || r.Saved != null)
                    throw new InvalidDataException("Unobserved operation contains payment/object evidence.");
                return;
            }
            var source = plan.Blueprint.parts[plan.Parts.First(p => p.Id == part).SourceIndex];
            if (r.ObjectUser == 0 || r.ObjectId == 0 || r.Creator != r.Player || r.PrefabHash != source.prefabName.GetStableHashCode() ||
                r.Paid.Length == 0 && !r.CostFree || r.Paid.Length != 0 && r.CostFree)
                throw new InvalidDataException("Incomplete native creation/debit evidence.");
            ValidateDebits(r);
            if (r.Receipt == null || !r.Receipt.SequenceEqual(Receipt(plan, r))) throw new InvalidDataException("Native receipt does not bind the saved operation.");
            if (r.Phase == Observed && r.Saved != null) throw new InvalidDataException("Observed operation falsely claims persistence.");
            if (r.Phase == Committed && (r.Saved == null || r.Saved.BeforeGeneration == 0 || r.Saved.BeforeGeneration == uint.MaxValue ||
                r.Saved.Generation != r.Saved.BeforeGeneration + 1 || !Hash(r.Saved.ProfileHash, 128) ||
                r.Saved.InventoryHash != Digest(Convert.FromBase64String(r.AfterBytes)) ||
                r.Saved.WorldPayloadHash != Digest(r.Receipt))) throw new InvalidDataException("Missing or mismatched paired-save evidence.");
        }
        private static void ValidateDebits(Record r)
        {
            var before = ValidateInventory(r.BeforeInfo, r.BeforeBytes);
            var after = ValidateInventory(r.AfterInfo, r.AfterBytes);
            if (r.Paid == null || r.Paid.Length > 4096 || r.CostFree != (r.Paid.Length == 0) ||
                !r.BeforeInfo.Split(':').Take(2).SequenceEqual(r.AfterInfo.Split(':').Take(2)))
                throw new InvalidDataException("Invalid debit mode or inventory dimensions.");
            var originals = before.ToDictionary(item => (item.X, item.Y));
            var remaining = after.ToDictionary(item => (item.X, item.Y));
            var debits = new Dictionary<(int, int), PaidRow>();
            foreach (var row in r.Paid)
            {
                if (row == null || row.Paid < 1 || !originals.TryGetValue((row.X, row.Y), out var original) ||
                    row.Before != original.Stack || row.Paid > row.Before || debits.ContainsKey((row.X, row.Y)))
                    throw new InvalidDataException("Debit does not identify an original inventory stack.");
                var properties = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,
                    row.Prefab + ":" + row.Variant.ToString(CultureInfo.InvariantCulture) + ":" + row.Quality.ToString(CultureInfo.InvariantCulture) + ":0", row.Properties, null);
                if (!BlueprintItemStandContents.TryRead(properties, name => name.GetStableHashCode(), out var item) || !SameItem(original, item, false))
                    throw new InvalidDataException("Debit properties differ from the original item.");
                debits.Add((row.X, row.Y), row);
            }
            foreach (var item in before)
            {
                debits.TryGetValue((item.X, item.Y), out var debit);
                int expected = item.Stack - (debit?.Paid ?? 0);
                bool exists = remaining.TryGetValue((item.X, item.Y), out var current);
                if (expected == 0 ? exists : !exists || current.Stack != expected || !SameItem(item, current, debit == null))
                    throw new InvalidDataException("Inventory delta differs from the recorded debit.");
            }
            if (remaining.Keys.Any(slot => !originals.ContainsKey(slot))) throw new InvalidDataException("Construction added an unrecorded inventory stack.");
        }
        private static bool SameItem(BlueprintContainerInventory.Item before, BlueprintContainerInventory.Item after, bool allowWear) =>
            before.Prefab == after.Prefab && before.X == after.X && before.Y == after.Y && before.WorldLevel == after.WorldLevel &&
            before.PickedUp == after.PickedUp && before.Equipped == after.Equipped && before.Quality == after.Quality && before.Variant == after.Variant &&
            before.CrafterId == after.CrafterId && before.CrafterName == after.CrafterName && before.Cheated == after.Cheated &&
            before.CustomData.Count == after.CustomData.Count && before.CustomData.All(pair => after.CustomData.TryGetValue(pair.Key, out var value) && pair.Value == value) &&
            (before.DurabilityHundredths == after.DurabilityHundredths && before.LegacyDurability == after.LegacyDurability ||
             allowWear && before.Equipped && (before.DurabilityHundredths.HasValue && after.DurabilityHundredths.HasValue && after.DurabilityHundredths <= before.DurabilityHundredths ||
             before.LegacyDurability.HasValue && after.LegacyDurability.HasValue && after.LegacyDurability <= before.LegacyDurability));
        internal static IReadOnlyList<BlueprintContainerInventory.Item> ValidateInventory(string info, string bytes)
        {
            if (info == null || bytes == null) throw new InvalidDataException("Exact inventory evidence is required.");
            var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, bytes, null);
            if (!BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items)) throw new InvalidDataException("Unreadable native inventory evidence.");
            return items;
        }
        private static byte[] Receipt(Plan.Record plan, Record r)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write("BuildWorks construction receipt v1"); writer.Write(plan.World); writer.Write(plan.Id.ToByteArray());
                    writer.Write(r.Part.ToByteArray()); writer.Write(r.Operation.ToByteArray()); writer.Write(r.Player); writer.Write(r.PlanHash);
                    foreach (float value in plan.Parts.First(p => p.Id == r.Part).WorldPose.Values) writer.Write(value);
                    writer.Write(r.ObjectUser); writer.Write(r.ObjectId); writer.Write(r.PrefabHash); writer.Write(r.Creator); writer.Write(r.CostFree);
                    writer.Write(Digest(Encoding.UTF8.GetBytes((r.BeforeInfo ?? "") + "\n" + r.BeforeBytes)));
                    writer.Write(Digest(Encoding.UTF8.GetBytes((r.AfterInfo ?? "") + "\n" + r.AfterBytes)));
                    writer.Write(r.Paid.Length);
                    foreach (var row in r.Paid)
                    {
                        writer.Write(row.Prefab); writer.Write(Digest(Encoding.UTF8.GetBytes(row.Properties))); writer.Write(row.Before);
                        writer.Write(row.Paid); writer.Write(row.X); writer.Write(row.Y); writer.Write(row.Variant); writer.Write(row.Quality);
                    }
                }
                if (output.Length > 65536) throw new InvalidDataException("Native construction receipt exceeds its budget.");
                return output.ToArray();
            }
        }
        internal static string Fingerprint(Record record) => Digest(Encode(record));
        private static byte[] Encode(Record record)
        {
            using (var stream = new MemoryStream())
            { Serializer().WriteObject(stream, record); if (stream.Length > MaximumBytes) throw new InvalidDataException("Part journal exceeds limit."); return stream.ToArray(); }
        }
        private static Record Clone(Record record)
        { using (var stream = new MemoryStream(Encode(record), false)) return (Record)Serializer().ReadObject(stream); }
        private static DataContractJsonSerializer Serializer() => new DataContractJsonSerializer(typeof(Record),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 131072 });
        private static bool Hash(string value, int length) => value != null && value.Length == length && value.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
