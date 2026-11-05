using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using Plan = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int OutcomeUnsettled = 5;
        private static readonly byte[] OutcomeMagic = Encoding.ASCII.GetBytes("BWPP03\r\n");
        [DataContract] internal sealed class OutcomeRecord
        {
            [DataMember(Order = 0)] internal byte[] Origin;
            [DataMember(Order = 1)] internal BlueprintConstructionPaymentOutcome Observation;
            [DataMember(Order = 2)] internal string Failure;
            internal Record Original;
            internal int Phase => OutcomeUnsettled;
        }

        internal void RequireOutcomeCapacity(Guid project, Guid part, Guid operation, BlueprintConstructionPaymentOutcome observation)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part);
                var origin = Read(project, part);
                if (origin?.Phase != Intent || origin.Operation != operation) throw new InvalidOperationException("Partial outcome capacity needs the same fresh intent.");
                var candidate = new OutcomeRecord { Origin = File.ReadAllBytes(PathFor(project, part)), Observation = observation, Failure = "" };
                ValidateOutcome(plan, part, candidate);
                // Use the actual codec, including byte[] origin expansion and worst-case JSON escaping, before any native effect.
                candidate.Failure = new string('\0', 1024);
                candidate.Observation = new BlueprintConstructionPaymentOutcome { ExplicitSources = observation.ExplicitSources,
                    Sources = observation.Sources.Select(source => new BlueprintConstructionPaymentOutcome.Source { Token = source.Token,
                        Items = source.Items.Select(item => new BlueprintConstructionPaymentOutcome.Item {
                            Properties = item.Properties, Before = item.Before, After = int.MaxValue }).ToArray() }).ToArray() };
                if (EncodeOutcome(candidate).Length > MaximumBytes) throw new InvalidDataException("Partial outcome exceeds its pre-native serialization budget.");
            }
        }

        // Failure-write only. Unenrolled external coverage and the target remain unknown.
        internal bool RecordOutcome(Guid project, Guid part, Guid operation, BlueprintConstructionPaymentOutcome observation, string failure)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part);
                var payment = ReadPayment(project, part);
                if (payment.Outcome != null)
                {
                    if (payment.Outcome.Original.Operation != operation ||
                        !EncodeOutcome(new OutcomeRecord { Origin = payment.Outcome.Origin, Observation = observation, Failure = failure })
                            .SequenceEqual(EncodeOutcome(payment.Outcome)))
                        throw new InvalidOperationException("Repeated partial outcome differs; retained evidence cannot be replaced.");
                    return true;
                }
                var origin = payment.Legacy;
                if (origin == null || operation == Guid.Empty || origin.Operation != operation)
                    throw new InvalidOperationException("Partial outcome requires the same legacy operation.");
                if (origin.Phase != Intent) return false;
                var record = new OutcomeRecord { Origin = File.ReadAllBytes(PathFor(project, part)), Observation = observation, Failure = failure };
                WriteOutcome(plan, part, record);
                return true;
            }
        }

        private static Record ValidateOutcome(Plan.Record plan, Guid part, OutcomeRecord record)
        {
            if (record?.Origin == null || record.Origin.Length < 44 || record.Origin.Length > MaximumBytes + 44 ||
                record.Failure == null || record.Failure.Length > 1024 || record.Observation?.Sources == null)
                throw new InvalidDataException("Incomplete or oversized partial construction outcome.");
            Record origin;
            using (var stream = new MemoryStream(record.Origin, false))
            using (var reader = new BinaryReader(stream))
            {
                if (!reader.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Partial outcome requires an exact BWPP01 origin.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || stream.Length != size + 44L) throw new InvalidDataException("Invalid partial origin framing.");
                byte[] bytes = reader.ReadBytes(size);
                using (var sha = SHA256.Create())
                    if (!sha.ComputeHash(bytes).SequenceEqual(reader.ReadBytes(32))) throw new InvalidDataException("Partial origin checksum differs.");
                using (var input = new MemoryStream(bytes, false)) origin = (Record)Serializer().ReadObject(input);
                Validate(plan, part, origin);
                if (origin.Phase != Intent || !Encode(origin).SequenceEqual(bytes)) throw new InvalidDataException("Partial origin is not a canonical intent.");
            }
            var sources = record.Observation.Sources;
            if (sources.Length < 1 || sources.Length > 128 || sources.Any(source => source == null || source.Items == null) ||
                sources[0].Token != Guid.Empty || sources.Select(source => source.Token).Distinct().Count() != sources.Length ||
                (record.Observation.ExplicitSources ? sources.Length < 2 : sources.Length != 1) ||
                sources.Sum(source => (long)source.Items.Length) > 4096)
                throw new InvalidDataException("Partial recipe observation identities or bounds differ.");
            long characters = 0;
            foreach (var source in sources)
                foreach (var item in source.Items)
                {
                    if (item == null || item.Before < 1 || item.Properties == null && source.Exact || item.Properties?.Length > 32768 ||
                        (source.Exact ? item.After < 0 || item.After > item.Before : item.After != -1))
                        throw new InvalidDataException("Invalid exact/unknown recipe observation.");
                    if (item.Properties == null) continue;
                    characters += item.Properties.Length;
                    if (characters > MaximumBytes / 2) throw new InvalidDataException("Partial recipe observations exceed their budget.");
                    try
                    {
                        if (Convert.ToBase64String(Convert.FromBase64String(item.Properties)) != item.Properties || item.Properties.Length == 0)
                            throw new InvalidDataException("Noncanonical recipe properties.");
                    }
                    catch (FormatException error) { throw new InvalidDataException("Unreadable recipe properties.", error); }
                }
            return origin;
        }

        private OutcomeRecord ReadOutcomeFile(string path, Plan.Record plan, Guid part)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Partial journal cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44 || !reader.ReadBytes(8).SequenceEqual(OutcomeMagic))
                    throw new InvalidDataException("Invalid partial construction framing.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid partial construction size.");
                byte[] bytes = reader.ReadBytes(size);
                using (var sha = SHA256.Create())
                    if (!sha.ComputeHash(bytes).SequenceEqual(reader.ReadBytes(32))) throw new InvalidDataException("Partial construction checksum differs.");
                OutcomeRecord record;
                using (var input = new MemoryStream(bytes, false)) record = (OutcomeRecord)OutcomeSerializer().ReadObject(input);
                record.Original = ValidateOutcome(plan, part, record);
                if (!EncodeOutcome(record).SequenceEqual(bytes)) throw new InvalidDataException("Noncanonical partial construction fields.");
                return record;
            }
        }

        private void WriteOutcome(Plan.Record plan, Guid part, OutcomeRecord record)
        {
            ValidateOutcome(plan, part, record);
            byte[] bytes = EncodeOutcome(record);
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Partial construction outcome exceeds its file budget.");
            string target = PathFor(plan.Id, part), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                using (var sha = SHA256.Create())
                { writer.Write(OutcomeMagic); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); writer.Flush(); file.Flush(true); }
                if (!EncodeOutcome(ReadOutcomeFile(temp, plan, part)).SequenceEqual(bytes)) throw new InvalidDataException("Prepared partial outcome changed.");
                CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath);
                if ((File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Partial outcome target changed.");
                if (!File.ReadAllBytes(target).SequenceEqual(record.Origin)) throw new InvalidDataException("Partial origin changed before commit.");
                File.Replace(temp, target, null);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static DataContractJsonSerializer OutcomeSerializer() => new DataContractJsonSerializer(typeof(OutcomeRecord),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 100000 });
        private static byte[] EncodeOutcome(OutcomeRecord record)
        { using (var stream = new MemoryStream()) { OutcomeSerializer().WriteObject(stream, record); return stream.ToArray(); } }
    }
}
