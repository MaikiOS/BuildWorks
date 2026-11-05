using System;
using System.Collections.Generic;
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
    internal sealed partial class BlueprintConstructionPartJournal
    {
        // Production debit/save/refund consumers must support this format before enrolling sources.
        private static readonly byte[] SourcesMagic = Encoding.ASCII.GetBytes("BWPP02\r\n");
        [DataContract] internal sealed class SourceState
        {
            [DataMember(Order = 0)] internal Guid Token = default;
            [DataMember(Order = 1)] internal long ObjectUser = default;
            [DataMember(Order = 2)] internal uint ObjectId = default;
            [DataMember(Order = 3)] internal int PrefabHash = default;
            [DataMember(Order = 4)] internal long Creator = default;
            [DataMember(Order = 5)] internal string BeforeInfo = default;
            [DataMember(Order = 6)] internal string BeforeBytes = default;
            [DataMember(Order = 7)] internal string AfterInfo = default;
            [DataMember(Order = 8)] internal string AfterBytes = default;
            [DataMember(Order = 9)] internal PaidRow[] Paid = Array.Empty<PaidRow>();
        }
        [DataContract] internal sealed class SourceSave
        {
            [DataMember(Order = 0)] internal Guid Token = default;
            [DataMember(Order = 1)] internal long ObjectUser = default;
            [DataMember(Order = 2)] internal uint ObjectId = default;
            [DataMember(Order = 3)] internal long World = default;
            [DataMember(Order = 4)] internal uint Generation = default;
            [DataMember(Order = 5)] internal string InventoryHash = default;
        }
        [DataContract] internal sealed class SourcesSavePair
        {
            [DataMember(Order = 0)] internal SavePair Placement = default;
            [DataMember(Order = 1)] internal SourceSave[] Sources = default;
        }
        [DataContract] internal sealed class SourcesRecord
        {
            [DataMember(Order = 0)] internal Guid Project;
            [DataMember(Order = 1)] internal Guid Part;
            [DataMember(Order = 2)] internal Guid Operation;
            [DataMember(Order = 3)] internal long World;
            [DataMember(Order = 4)] internal long Player;
            [DataMember(Order = 5)] internal long Revision;
            [DataMember(Order = 6)] internal int Phase;
            [DataMember(Order = 7)] internal string PlanHash;
            [DataMember(Order = 8)] internal string Provider;
            [DataMember(Order = 9)] internal string ProviderHash;
            [DataMember(Order = 10)] internal string PolicyHash;
            [DataMember(Order = 11)] internal SourceState[] Sources;
            [DataMember(Order = 12)] internal long ObjectUser;
            [DataMember(Order = 13)] internal uint ObjectId;
            [DataMember(Order = 14)] internal int PrefabHash;
            [DataMember(Order = 15)] internal long Creator;
            [DataMember(Order = 16)] internal bool CostFree;
            [DataMember(Order = 17)] internal byte[] Receipt;
            [DataMember(Order = 18)] internal SourcesSavePair Saved;
            internal bool RequiresReconciliation => Phase == Intent || Phase == Observed;
        }

        internal static void RequireSourcesCapacity(long player, SourceState[] sources, PaidRow[][] possibleDebits)
        {
            ValidateSourceSet(player, sources, true);
            if (possibleDebits == null || possibleDebits.Length != sources.Length || possibleDebits.Any(rows => rows == null) ||
                possibleDebits.Sum(rows => (long)rows.Length) > 4096 ||
                possibleDebits.SelectMany(rows => rows).Any(row => row?.Prefab == null || row.Properties == null))
                throw new InvalidDataException("All-source debit preflight is incomplete.");
            long receipt = 1024 + sources.Length * 384L + possibleDebits.SelectMany(rows => rows).Sum(row => 128L + Encoding.UTF8.GetByteCount(row.Prefab));
            long bytes = sources.Sum(source => source.BeforeBytes.Length * 2L + source.BeforeInfo.Length * 12L) +
                possibleDebits.SelectMany(rows => rows).Sum(row => (long)row.Properties.Length + row.Prefab.Length * 6L);
            if (receipt > 65536 || bytes > MaximumBytes / 2) throw new InvalidDataException("All-source construction evidence exceeds its preflight budget.");
        }

        internal SourcesRecord PrepareSources(Guid project, Guid part, Guid operation, long player,
            string provider, string providerHash, string policyHash, SourceState[] sources, out bool newlyPrepared)
        {
            newlyPrepared = false;
            ValidateSourceSet(player, sources, true);
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = ReadSources(project, part);
                if (current != null && current.Operation == operation)
                {
                    if (current.Player != player || current.Provider != provider || current.ProviderHash != providerHash ||
                        current.PolicyHash != policyHash || !SameSourceInputs(current.Sources, sources))
                        throw new InvalidOperationException("Repeated all-source operation has different inputs.");
                    return current;
                }
                if (current != null && current.Phase != NoEffect) throw new InvalidOperationException("Part already has all-source execution evidence; no automatic replay.");
                var created = new SourcesRecord { Project = project, Part = part, Operation = operation, Player = player, World = plan.World,
                    Revision = checked((current?.Revision ?? 0) + 1), Phase = Intent, PlanHash = Digest(plan.BlueprintBytes),
                    Provider = provider, ProviderHash = providerHash, PolicyHash = policyHash, Sources = sources };
                WriteSources(plan, created, current != null); newlyPrepared = true; return ReadSources(project, part);
            }
        }

        internal SourcesRecord ObserveSources(Guid project, Guid part, Guid operation, long objectUser, uint objectId,
            int prefab, long creator, SourceState[] sources, bool costFree)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireSourcesOperation(project, part, operation);
                if (current.Phase == NoEffect) throw new InvalidOperationException("No-effect operation cannot become a placement.");
                ValidateSourceSet(current.Player, sources, false);
                if (!SameSourceInputs(current.Sources, sources)) throw new InvalidOperationException("Observed source manifest differs from the intent.");
                var candidate = CloneSources(current); candidate.Sources = sources; candidate.ObjectUser = objectUser; candidate.ObjectId = objectId;
                candidate.PrefabHash = prefab; candidate.Creator = creator; candidate.CostFree = costFree;
                candidate.Receipt = SourcesReceipt(plan, candidate);
                if (current.Phase == Observed || current.Phase == Committed)
                {
                    ValidateSources(plan, part, candidate);
                    if (!EncodeSources(candidate).SequenceEqual(EncodeSources(current))) throw new InvalidOperationException("Observed all-source placement differs from durable evidence.");
                    return current;
                }
                candidate.Phase = Observed; candidate.Revision = checked(current.Revision + 1);
                WriteSources(plan, candidate, true); return ReadSources(project, part);
            }
        }

        internal SourcesRecord ConfirmSavedSources(Guid project, Guid part, Guid operation, SourcesSavePair evidence)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireSourcesOperation(project, part, operation);
                if (current.Phase != Observed && current.Phase != Committed) throw new InvalidOperationException("All-source placement was not observed.");
                var candidate = CloneSources(current); candidate.Saved = evidence;
                if (current.Phase == Committed)
                {
                    ValidateSources(plan, part, candidate);
                    if (!EncodeSources(candidate).SequenceEqual(EncodeSources(current))) throw new InvalidOperationException("All-source save confirmation differs.");
                    return current;
                }
                candidate.Phase = Committed; candidate.Revision = checked(current.Revision + 1);
                WriteSources(plan, candidate, true); return ReadSources(project, part);
            }
        }

        internal SourcesRecord RecordNoEffectSources(Guid project, Guid part, Guid operation, SourceState[] unchanged, bool freshObjectAbsent)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireSourcesOperation(project, part, operation);
                if (current.Phase != Intent && current.Phase != NoEffect || !freshObjectAbsent || !SameSourceInputs(current.Sources, unchanged))
                    throw new InvalidOperationException("No-effect needs every enrolled source and fresh-object absence.");
                var candidate = CloneSources(current); candidate.Sources = unchanged; candidate.Phase = NoEffect;
                if (current.Phase == NoEffect)
                {
                    ValidateSources(plan, part, candidate);
                    if (!EncodeSources(candidate).SequenceEqual(EncodeSources(current))) throw new InvalidOperationException("Repeated all-source no-effect differs.");
                    return current;
                }
                candidate.Revision = checked(current.Revision + 1); WriteSources(plan, candidate, true); return ReadSources(project, part);
            }
        }

        internal SourcesRecord ReadSources(Guid project, Guid part)
        {
            string path = PathFor(project, part); return File.Exists(path) ? ReadSourcesFile(path, plans.Load(project), part) : null;
        }
        internal (Record Legacy, SourcesRecord Sources, OutcomeRecord Outcome) ReadPayment(Guid project, Guid part)
        {
            string path = PathFor(project, part);
            if (!File.Exists(path)) return (null, null, null);
            var plan = plans.Load(project);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Part journal cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44) throw new InvalidDataException("Invalid part journal framing.");
                byte[] magic = reader.ReadBytes(8);
                if (magic.SequenceEqual(Magic)) return (ReadFile(path, plan, part), null, null);
                if (magic.SequenceEqual(SourcesMagic)) return (null, ReadSourcesFile(path, plan, part), null);
                if (magic.SequenceEqual(OutcomeMagic)) return (null, null, ReadOutcomeFile(path, plan, part));
                throw new InvalidDataException("Unknown construction payment journal format.");
            }
        }
        internal SourcesRecord RequireSavedSourcesPayment(Guid project, Guid part, long player)
        {
            using (plans.Lock(project))
            {
                ActivePlan(project, part); var record = ReadSources(project, part);
                if (record?.Phase != Committed || record.Player != player || record.CostFree || record.Sources.All(source => source.Paid.Length == 0))
                    throw new InvalidOperationException("Contents require this payer's complete saved all-source payment.");
                return record;
            }
        }
        private SourcesRecord RequireSourcesOperation(Guid project, Guid part, Guid operation)
        {
            var record = ReadSources(project, part);
            if (record == null || operation == Guid.Empty || record.Operation != operation) throw new InvalidOperationException("All-source construction operation differs.");
            return record;
        }

        private static bool SameSourceInputs(SourceState[] a, SourceState[] b) => a != null && b != null && a.Length == b.Length &&
            a.Zip(b, (x, y) => x != null && y != null && x.Token == y.Token && x.ObjectUser == y.ObjectUser && x.ObjectId == y.ObjectId &&
                x.PrefabHash == y.PrefabHash && x.Creator == y.Creator && x.BeforeInfo == y.BeforeInfo && x.BeforeBytes == y.BeforeBytes).All(equal => equal);

        private static void ValidateSourceSet(long player, SourceState[] sources, bool intent)
        {
            if (player == 0 || sources == null || sources.Length < 2 || sources.Length > 128 || sources.Any(source => source == null || source.Paid == null))
                throw new InvalidDataException("All-source inventory manifest is incomplete.");
            long bytes = sources.Sum(source => (long)(source.BeforeBytes?.Length ?? 0) + (source.AfterBytes?.Length ?? 0) +
                (source.BeforeInfo?.Length ?? 0) * 6L + (source.AfterInfo?.Length ?? 0) * 6L +
                source.Paid.Sum(row => (long)(row?.Properties?.Length ?? 0) + (row?.Prefab?.Length ?? 0) * 6L));
            if (bytes > MaximumBytes / 2 || sources.Sum(source => (long)source.Paid.Length) > 4096)
                throw new InvalidDataException("All-source inventory evidence exceeds its budget.");
            var tokens = new HashSet<Guid>(); var objects = new HashSet<(long, uint)>(); long items = 0;
            for (int i = 0; i < sources.Length; ++i)
            {
                var source = sources[i];
                if (i == 0 ? source.Token != Guid.Empty || source.ObjectUser != 0 || source.ObjectId != 0 || source.PrefabHash != 0 || source.Creator != player :
                    source.Token == Guid.Empty || !tokens.Add(source.Token) || source.ObjectUser == 0 || source.ObjectId == 0 || source.PrefabHash == 0 ||
                    source.Creator != player || !objects.Add((source.ObjectUser, source.ObjectId)))
                    throw new InvalidDataException("Source identity is missing, duplicated or outside the payer-owned native-container slice.");
                items += ValidateInventory(source.BeforeInfo, source.BeforeBytes).Count;
                if (items > 4096) throw new InvalidDataException("All-source item count exceeds its budget.");
                if (intent && (source.AfterInfo != null || source.AfterBytes != null || source.Paid.Length != 0))
                    throw new InvalidDataException("Source intent contains outcome data.");
            }
        }

        private static void ValidateSources(Plan.Record plan, Guid part, SourcesRecord r)
        {
            if (r == null || r.Project != plan.Id || r.Part != part || !plan.Parts.Any(p => p.Id == part) || r.World != plan.World ||
                r.Operation == Guid.Empty || r.Player == 0 || r.Revision < 1 || r.Phase < Intent || r.Phase > NoEffect ||
                r.PlanHash != Digest(plan.BlueprintBytes) || string.IsNullOrWhiteSpace(r.Provider) || r.Provider.Length > 128 ||
                r.Provider.Any(char.IsControl) || !Hash(r.ProviderHash, 64) || !Hash(r.PolicyHash, 64))
                throw new InvalidDataException("All-source journal identity, plan, provider or state differs.");
            ValidateSourceSet(r.Player, r.Sources, r.Phase == Intent);
            if (r.Phase == Intent || r.Phase == NoEffect)
            {
                if (r.ObjectUser != 0 || r.ObjectId != 0 || r.PrefabHash != 0 || r.Creator != 0 || r.CostFree || r.Receipt != null || r.Saved != null ||
                    r.Sources.Any(source => source.Paid.Length != 0 || r.Phase == NoEffect &&
                        (source.AfterInfo != source.BeforeInfo || source.AfterBytes != source.BeforeBytes)))
                    throw new InvalidDataException("Unobserved all-source operation contains effects or lacks unchanged-source evidence.");
                return;
            }
            var blueprint = plan.Blueprint.parts[plan.Parts.First(p => p.Id == part).SourceIndex];
            long paid = r.Sources.Sum(source => source.Paid.Sum(row => (long)(row?.Paid ?? 0)));
            if (r.ObjectUser == 0 || r.ObjectId == 0 || r.Creator != r.Player || r.PrefabHash != blueprint.prefabName.GetStableHashCode() ||
                r.CostFree != (paid == 0) || r.Sources.Skip(1).Any(source => source.ObjectUser == r.ObjectUser && source.ObjectId == r.ObjectId))
                throw new InvalidDataException("All-source creation or payment mode differs.");
            foreach (var source in r.Sources)
            {
                ValidateDebits(new Record { BeforeInfo = source.BeforeInfo, BeforeBytes = source.BeforeBytes,
                    AfterInfo = source.AfterInfo, AfterBytes = source.AfterBytes, Paid = source.Paid, CostFree = source.Paid.Length == 0 });
                if (r.CostFree && (source.BeforeInfo != source.AfterInfo || source.BeforeBytes != source.AfterBytes))
                    throw new InvalidDataException("Cost-free placement changed an enrolled source.");
            }
            if (r.Receipt == null || !r.Receipt.SequenceEqual(SourcesReceipt(plan, r))) throw new InvalidDataException("All-source receipt differs.");
            if (r.Phase == Observed && r.Saved != null) throw new InvalidDataException("Observed all-source operation falsely claims persistence.");
            if (r.Phase == Committed)
            {
                var pair = r.Saved?.Placement;
                if (pair == null || pair.BeforeGeneration == 0 || pair.BeforeGeneration == uint.MaxValue || pair.Generation != pair.BeforeGeneration + 1 ||
                    !Hash(pair.ProfileHash, 128) || pair.InventoryHash != Digest(Convert.FromBase64String(r.Sources[0].AfterBytes)) ||
                    pair.WorldPayloadHash != Digest(r.Receipt) || r.Saved.Sources == null || r.Saved.Sources.Length != r.Sources.Length - 1)
                    throw new InvalidDataException("All-source paired-save evidence is incomplete.");
                for (int i = 1; i < r.Sources.Length; ++i)
                {
                    var source = r.Sources[i]; var proof = r.Saved.Sources[i - 1];
                    if (proof == null || proof.Token != source.Token || proof.ObjectUser != source.ObjectUser || proof.ObjectId != source.ObjectId ||
                        proof.World != r.World || proof.Generation != pair.Generation || proof.InventoryHash != Digest(Convert.FromBase64String(source.AfterBytes)))
                        throw new InvalidDataException("Saved external source identity, generation or inventory differs.");
                }
            }
        }

        private SourcesRecord ReadSourcesFile(string path, Plan.Record plan, Guid part)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("All-source journal cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44 || !reader.ReadBytes(8).SequenceEqual(SourcesMagic))
                    throw new InvalidDataException("Expected BWPP02 all-source evidence; implicit legacy conversion is refused.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid all-source journal size.");
                byte[] bytes = reader.ReadBytes(size), hash = reader.ReadBytes(32);
                using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(hash)) throw new InvalidDataException("All-source checksum differs.");
                SourcesRecord record;
                using (var input = new MemoryStream(bytes, false)) record = (SourcesRecord)SourcesSerializer().ReadObject(input);
                ValidateSources(plan, part, record);
                if (!EncodeSources(record).SequenceEqual(bytes)) throw new InvalidDataException("Unknown or noncanonical all-source journal fields.");
                return record;
            }
        }
        private void WriteSources(Plan.Record plan, SourcesRecord record, bool replace)
        {
            ValidateSources(plan, record.Part, record); byte[] bytes = EncodeSources(record);
            string target = PathFor(plan.Id, record.Part), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                using (var sha = SHA256.Create())
                { writer.Write(SourcesMagic); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); writer.Flush(); file.Flush(true); }
                if (!EncodeSources(ReadSourcesFile(temp, plan, record.Part)).SequenceEqual(bytes)) throw new InvalidDataException("Prepared all-source evidence changed before commit.");
                CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath);
                if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("All-source journal target changed.");
                if (replace) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static byte[] SourcesReceipt(Plan.Record plan, SourcesRecord r)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write("BuildWorks construction receipt v2"); writer.Write(plan.World); writer.Write(plan.Id.ToByteArray());
                    writer.Write(r.Part.ToByteArray()); writer.Write(r.Operation.ToByteArray()); writer.Write(r.Player); writer.Write(r.PlanHash);
                    foreach (float value in plan.Parts.First(p => p.Id == r.Part).WorldPose.Values) writer.Write(value);
                    writer.Write(r.ObjectUser); writer.Write(r.ObjectId); writer.Write(r.PrefabHash); writer.Write(r.Creator); writer.Write(r.CostFree);
                    writer.Write(r.Provider ?? ""); writer.Write(r.ProviderHash ?? ""); writer.Write(r.PolicyHash ?? "");
                    if (r.Sources == null || r.Sources.Length < 2 || r.Sources.Length > 128) throw new InvalidDataException("Missing receipt sources.");
                    writer.Write(r.Sources.Length);
                    foreach (var source in r.Sources)
                    {
                        if (source == null || source.Paid == null || source.Paid.Length > 4096) throw new InvalidDataException("Missing receipt source rows.");
                        writer.Write(source.Token.ToByteArray()); writer.Write(source.ObjectUser); writer.Write(source.ObjectId); writer.Write(source.PrefabHash); writer.Write(source.Creator);
                        writer.Write(Digest(Encoding.UTF8.GetBytes((source.BeforeInfo ?? "") + "\n" + source.BeforeBytes)));
                        writer.Write(Digest(Encoding.UTF8.GetBytes((source.AfterInfo ?? "") + "\n" + source.AfterBytes))); writer.Write(source.Paid.Length);
                        foreach (var row in source.Paid)
                        {
                            if (row?.Prefab == null || row.Properties == null) throw new InvalidDataException("Incomplete source payment row.");
                            writer.Write(row.Prefab); writer.Write(Digest(Encoding.UTF8.GetBytes(row.Properties))); writer.Write(row.Before); writer.Write(row.Paid);
                            writer.Write(row.X); writer.Write(row.Y); writer.Write(row.Variant); writer.Write(row.Quality);
                            if (output.Length > 65536) throw new InvalidDataException("All-source receipt exceeds its budget.");
                        }
                    }
                }
                if (output.Length > 65536) throw new InvalidDataException("All-source receipt exceeds its budget.");
                return output.ToArray();
            }
        }
        internal static string FingerprintSources(SourcesRecord record) => Digest(EncodeSources(record));
        private static byte[] EncodeSources(SourcesRecord record)
        {
            using (var stream = new MemoryStream())
            { SourcesSerializer().WriteObject(stream, record); if (stream.Length > MaximumBytes) throw new InvalidDataException("All-source journal exceeds its limit."); return stream.ToArray(); }
        }
        private static SourcesRecord CloneSources(SourcesRecord record)
        { using (var stream = new MemoryStream(EncodeSources(record), false)) return (SourcesRecord)SourcesSerializer().ReadObject(stream); }
        private static DataContractJsonSerializer SourcesSerializer() => new DataContractJsonSerializer(typeof(SourcesRecord),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 131072 });
    }
}
