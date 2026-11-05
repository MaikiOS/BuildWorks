using System;
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
    internal sealed partial class BlueprintConstructionPartJournal
    {
        private static readonly byte[] MountMagic = Encoding.ASCII.GetBytes("BWPM01\r\n");
        // Detached evidence only; the native consumer must prove the empty target and paired saves.
        [DataContract] internal sealed class MountRecord
        {
            [DataMember(Order = 0)] internal Guid Project;
            [DataMember(Order = 1)] internal Guid Part;
            [DataMember(Order = 2)] internal Guid ParentOperation;
            [DataMember(Order = 3)] internal string ParentFingerprint;
            [DataMember(Order = 4)] internal Guid Operation;
            [DataMember(Order = 5)] internal long World;
            [DataMember(Order = 6)] internal long Player;
            [DataMember(Order = 7)] internal long Revision;
            [DataMember(Order = 8)] internal int Phase;
            // Observed session ID; the parent's saved marker, not this ID alone, identifies a reload.
            [DataMember(Order = 9)] internal long ObjectUser;
            [DataMember(Order = 10)] internal uint ObjectId;
            [DataMember(Order = 11)] internal string BeforeInfo;
            [DataMember(Order = 12)] internal string BeforeBytes;
            [DataMember(Order = 13)] internal string MountedInfo;
            [DataMember(Order = 14)] internal string MountedBytes;
            [DataMember(Order = 15)] internal string AfterInfo;
            [DataMember(Order = 16)] internal string AfterBytes;
            [DataMember(Order = 17)] internal byte[] Receipt;
            [DataMember(Order = 18)] internal SavePair Saved;
            [DataMember(Order = 19, EmitDefaultValue = false)] internal MountReturnRecord Return;
            internal bool RequiresReconciliation => Phase == Intent || Phase == Observed || Return != null && Return.Phase != MountReturnComplete;
        }

        internal MountRecord PrepareMount(Guid project, Guid part, Guid operation, long player,
            long objectUser, uint objectId, BlueprintForeignPartData before, BlueprintForeignPartData mounted,
            bool emptyTarget, out bool newlyPrepared)
        {
            newlyPrepared = false;
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var parent = MountParent(project, part, player);
                if (!emptyTarget || before?.Format != BlueprintForeignPartData.NativeContainerFormat || mounted == null || mounted.ContentsCleared ||
                    mounted.Format != BlueprintForeignPartData.NativeItemStandFormat)
                    throw new InvalidOperationException("Mount intent requires an empty target and the actual supplied item snapshot.");
                var current = ReadMount(project, part);
                var candidate = new MountRecord { Project = project, Part = part, ParentOperation = parent.Operation,
                    ParentFingerprint = parent.Fingerprint, Operation = operation, World = plan.World, Player = player,
                    Revision = checked((current?.Revision ?? 0) + 1), Phase = Intent, ObjectUser = objectUser, ObjectId = objectId,
                    BeforeInfo = before?.Info, BeforeBytes = before?.Zdo, MountedInfo = mounted.Info, MountedBytes = mounted.Zdo };
                ValidateMount(plan, part, candidate);
                if (current != null && current.Operation == operation)
                {
                    candidate.Revision = current.Revision; candidate.Phase = current.Phase;
                    candidate.AfterInfo = current.AfterInfo; candidate.AfterBytes = current.AfterBytes;
                    candidate.Receipt = current.Receipt; candidate.Saved = current.Saved;
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Repeated mount intent has different inputs.");
                    return current;
                }
                if (current != null && current.Phase != NoEffect)
                    throw new InvalidOperationException("Mount evidence already exists; native replay is forbidden.");
                RequireMountCapacity(candidate);
                WriteMount(plan, candidate, current != null); newlyPrepared = true; return ReadMount(project, part);
            }
        }

        internal MountRecord ObserveMount(Guid project, Guid part, Guid operation,
            BlueprintForeignPartData after, BlueprintForeignPartData actualMounted)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMount(project, part, operation);
                if (current.Phase == NoEffect || after?.Format != BlueprintForeignPartData.NativeContainerFormat || actualMounted == null || actualMounted.ContentsCleared ||
                    actualMounted.Format != BlueprintForeignPartData.NativeItemStandFormat ||
                    actualMounted.Info != current.MountedInfo || actualMounted.Zdo != current.MountedBytes)
                    throw new InvalidOperationException("Native attachment readback differs from the supplied item.");
                var candidate = CloneMount(current); candidate.AfterInfo = after?.Info; candidate.AfterBytes = after?.Zdo;
                candidate.Phase = Observed; candidate.Saved = null; candidate.Receipt = MountReceipt(candidate);
                if (current.Phase == Observed || current.Phase == Committed)
                {
                    candidate.Phase = current.Phase; candidate.Saved = current.Saved;
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Repeated mount observation differs from durable evidence.");
                    return current;
                }
                candidate.Revision = checked(current.Revision + 1);
                WriteMount(plan, candidate, true); return ReadMount(project, part);
            }
        }

        internal MountRecord ConfirmMountSaved(Guid project, Guid part, Guid operation, SavePair evidence)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMount(project, part, operation);
                if (current.Phase != Observed && current.Phase != Committed)
                    throw new InvalidOperationException("Mount must be observed before save confirmation.");
                var candidate = CloneMount(current); candidate.Saved = evidence;
                if (current.Phase == Committed)
                {
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Repeated mount save confirmation differs.");
                    return current;
                }
                candidate.Phase = Committed; candidate.Revision = checked(current.Revision + 1);
                WriteMount(plan, candidate, true); return ReadMount(project, part);
            }
        }

        internal MountRecord MountNoEffect(Guid project, Guid part, Guid operation,
            BlueprintForeignPartData unchanged, bool emptyTarget)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMount(project, part, operation);
                if (current.Phase != Intent && current.Phase != NoEffect || !emptyTarget || unchanged == null ||
                    unchanged.Info != current.BeforeInfo || unchanged.Zdo != current.BeforeBytes)
                    throw new InvalidOperationException("Mount no-effect needs the unchanged inventory and empty target.");
                if (current.Phase == NoEffect) return current;
                current.Phase = NoEffect; current.Revision = checked(current.Revision + 1);
                WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }

        internal MountRecord ReadMount(Guid project, Guid part)
        {
            string path = MountPath(project, part);
            return File.Exists(path) ? ReadMountFile(path, plans.Load(project), part) : null;
        }
        internal static void RequireNoOutstandingMounts(Plan plans, Guid project)
        {
            var journal = new BlueprintConstructionPartJournal(plans);
            var remaining = plans.Load(project).Parts.Select(part => part.Id).ToHashSet();
            foreach (string path in Directory.EnumerateFiles(plans.DirectoryPath, project.ToString("N") + ".*.bwmount"))
            {
                string[] names = Path.GetFileName(path).Split('.');
                if (names.Length != 3 || !Guid.TryParseExact(names[1], "N", out var part) || !remaining.Remove(part))
                    throw new InvalidDataException("Malformed mount journal filename.");
                var record = journal.ReadMount(project, part);
                if (record == null || record.Phase != NoEffect && record.Return?.Phase != MountReturnComplete)
                    throw new InvalidOperationException("Mount custody requires reconciliation or a saved item return before structural cancellation.");
            }
        }
        private MountRecord RequireMount(Guid project, Guid part, Guid operation)
        {
            var record = ReadMount(project, part);
            if (record == null || operation == Guid.Empty || record.Operation != operation)
                throw new InvalidOperationException("Mount operation identity differs.");
            return record;
        }
        private (Guid Operation, string Fingerprint) MountParent(Guid project, Guid part, long player)
        {
            var payment = ReadPayment(project, part);
            var legacy = payment.Legacy; var sources = payment.Sources;
            if (legacy?.Phase == Committed && legacy.Player == player && !legacy.CostFree && legacy.Paid.Length > 0)
                return (legacy.Operation, Fingerprint(legacy));
            if (sources?.Phase == Committed && sources.Player == player && !sources.CostFree && sources.Sources.Any(source => source.Paid.Length > 0))
                return (sources.Operation, FingerprintSources(sources));
            throw new InvalidOperationException("Mount requires this payer's complete saved construction payment.");
        }

        private void ValidateMount(Plan.Record plan, Guid part, MountRecord r)
        {
            if (r == null || r.Project != plan.Id || r.Part != part || r.World != plan.World || r.Player == 0 ||
                r.Operation == Guid.Empty || r.Operation == r.ParentOperation || r.Revision < 1 || r.Phase < Intent || r.Phase > NoEffect ||
                r.ObjectUser == 0 || r.ObjectId == 0 || !Hash(r.ParentFingerprint, 64) ||
                r.BeforeBytes == null || r.BeforeBytes.Length > MaximumBytes / 4 || r.MountedInfo == null || r.MountedInfo.Length > 1024 ||
                (r.AfterBytes?.Length ?? 0) > MaximumBytes / 4 || r.MountedBytes == null || r.MountedBytes.Length > 32768)
                throw new InvalidDataException("Invalid mount identity or evidence budget.");
            var parent = MountParent(plan.Id, part, r.Player);
            if (r.ParentOperation != parent.Operation || r.ParentFingerprint != parent.Fingerprint)
                throw new InvalidDataException("Mount parent payment changed.");
            var source = plan.Blueprint.parts[plan.Parts.First(p => p.Id == part).SourceIndex].foreignData?.ToGeometry();
            if (source?.Format != BlueprintForeignPartData.NativeItemStandFormat || source.ContentsCleared)
                throw new InvalidDataException("Mount does not match this part's requested item.");
            var mounted = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, r.MountedInfo, r.MountedBytes, null);
            if (!BlueprintItemStandContents.TryRead(mounted, name => name.GetStableHashCode(), out var item) ||
                item.SourceVersion != 110 || item.Equipped)
                throw new InvalidDataException("Mount needs a full current unequipped item snapshot.");
            // Survival keeps supplied attributes; the blueprint selects only the prefab and orientation.
            string[] requested = source.Info.Split(':'), actual = r.MountedInfo.Split(':');
            if (requested[0] != actual[0] || int.Parse(requested[3], CultureInfo.InvariantCulture) != int.Parse(actual[3], CultureInfo.InvariantCulture))
                throw new InvalidDataException("Mount prefab or orientation differs from the requested item.");
            var originals = ValidateInventory(r.BeforeInfo, r.BeforeBytes);
            var supplied = originals.SingleOrDefault(value => value.X == item.X && value.Y == item.Y);
            if (supplied == null || supplied.SourceVersion != 110 || !SameItem(supplied, item, false, true))
                throw new InvalidDataException("Mounted properties do not belong to the actual supplied inventory item.");
            if (r.Phase == Intent || r.Phase == NoEffect)
            {
                if (r.AfterInfo != null || r.AfterBytes != null || r.Receipt != null || r.Saved != null || r.Return != null)
                    throw new InvalidDataException("Unobserved mount claims effects or persistence.");
                return;
            }
            if (r.AfterBytes == null || r.AfterBytes.Length > r.BeforeBytes.Length + 4 ||
                r.AfterInfo == null || r.AfterInfo.Length > r.BeforeInfo.Length + 16)
                throw new InvalidDataException("Mount outcome exceeds its pre-native evidence reservation.");
            ValidateDebits(new Record { BeforeInfo = r.BeforeInfo, BeforeBytes = r.BeforeBytes,
                AfterInfo = r.AfterInfo, AfterBytes = r.AfterBytes, Paid = new[] { new PaidRow {
                    Prefab = item.Prefab, Properties = r.MountedBytes, Before = supplied.Stack, Paid = 1,
                    X = item.X, Y = item.Y, Variant = item.Variant, Quality = item.Quality } } }, true);
            if (r.Receipt == null || !r.Receipt.SequenceEqual(MountReceipt(r)) || r.Phase == Observed && r.Saved != null)
                throw new InvalidDataException("Mount receipt or phase differs.");
            if (r.Phase == Committed && (r.Saved == null || r.Saved.BeforeGeneration == 0 || r.Saved.BeforeGeneration == uint.MaxValue ||
                r.Saved.Generation != r.Saved.BeforeGeneration + 1 || !Hash(r.Saved.ProfileHash, 128) ||
                r.Saved.InventoryHash != Digest(Convert.FromBase64String(r.AfterBytes)) || r.Saved.WorldPayloadHash != Digest(r.Receipt)))
                throw new InvalidDataException("Mount lacks matching paired-save evidence.");
            ValidateMountReturn(r);
        }

        private string MountPath(Guid project, Guid part)
        {
            if (project == Guid.Empty || part == Guid.Empty) throw new ArgumentException("Project and part are required.");
            return Path.Combine(CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath), project.ToString("N") + "." + part.ToString("N") + ".bwmount");
        }
        private static void RequireMountCapacity(MountRecord intent)
        {
            // Reserve worst JSON escaping before native debit; this counterfactual record is never written.
            var bound = CloneMount(intent); bound.Revision = long.MaxValue; bound.Phase = Committed;
            bound.AfterInfo = new string('\0', intent.BeforeInfo.Length + 16);
            bound.AfterBytes = new string('/', intent.BeforeBytes.Length + 4);
            bound.Receipt = Enumerable.Repeat((byte)255, MountReceipt(bound).Length).ToArray();
            bound.Saved = new SavePair { BeforeGeneration = uint.MaxValue - 1, Generation = uint.MaxValue,
                ProfileHash = new string('F', 128), InventoryHash = new string('F', 64), WorldPayloadHash = new string('F', 64) };
            EncodeMount(bound);
        }
        private MountRecord ReadMountFile(string path, Plan.Record plan, Guid part)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Mount journal cannot be a reparse point.");
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44 || !reader.ReadBytes(8).SequenceEqual(MountMagic))
                    throw new InvalidDataException("Unknown mount journal framing.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid mount journal size.");
                byte[] bytes = reader.ReadBytes(size), hash = reader.ReadBytes(32);
                using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(hash)) throw new InvalidDataException("Mount checksum differs.");
                MountRecord record;
                using (var input = new MemoryStream(bytes, false)) record = (MountRecord)MountSerializer().ReadObject(input);
                ValidateMount(plan, part, record);
                if (!EncodeMount(record).SequenceEqual(bytes)) throw new InvalidDataException("Unknown or noncanonical mount fields.");
                return record;
            }
        }
        private void WriteMount(Plan.Record plan, MountRecord record, bool replace)
        {
            ValidateMount(plan, record.Part, record); byte[] bytes = EncodeMount(record);
            string target = MountPath(plan.Id, record.Part), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
            try
            {
                using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(file))
                using (var sha = SHA256.Create())
                { writer.Write(MountMagic); writer.Write(bytes.Length); writer.Write(bytes); writer.Write(sha.ComputeHash(bytes)); writer.Flush(); file.Flush(true); }
                if (!EncodeMount(ReadMountFile(temp, plan, record.Part)).SequenceEqual(bytes)) throw new InvalidDataException("Prepared mount evidence changed.");
                CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath);
                if (File.Exists(target) && (File.GetAttributes(target) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Mount journal target changed.");
                if (replace) File.Replace(temp, target, null); else File.Move(temp, target);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        private static byte[] MountReceipt(MountRecord r)
        {
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write("BuildWorks mounted item receipt v1"); writer.Write(r.Project.ToByteArray()); writer.Write(r.Part.ToByteArray());
                    writer.Write(r.ParentOperation.ToByteArray()); writer.Write(r.ParentFingerprint); writer.Write(r.Operation.ToByteArray());
                    writer.Write(r.World); writer.Write(r.Player); writer.Write(r.ObjectUser); writer.Write(r.ObjectId);
                    writer.Write(Digest(Encoding.UTF8.GetBytes(r.BeforeInfo + "\n" + r.BeforeBytes)));
                    writer.Write(Digest(Encoding.UTF8.GetBytes(r.AfterInfo + "\n" + r.AfterBytes)));
                    writer.Write(r.MountedInfo); writer.Write(Digest(Convert.FromBase64String(r.MountedBytes)));
                }
                return output.ToArray();
            }
        }
        private static DataContractJsonSerializer MountSerializer() => new DataContractJsonSerializer(typeof(MountRecord),
            new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 131072 });
        private static byte[] EncodeMount(MountRecord record)
        {
            using (var stream = new MemoryStream())
            { MountSerializer().WriteObject(stream, record); if (stream.Length > MaximumBytes) throw new InvalidDataException("Mount journal exceeds limit."); return stream.ToArray(); }
        }
        private static MountRecord CloneMount(MountRecord record)
        { using (var stream = new MemoryStream(EncodeMount(record), false)) return (MountRecord)MountSerializer().ReadObject(stream); }
    }
}
