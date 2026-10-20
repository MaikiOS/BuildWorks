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
using Plans = OstrixMods.BuildWorks.BlueprintConstructionProjectStore;
using Parts = OstrixMods.BuildWorks.BlueprintConstructionPartJournal;

namespace OstrixMods.BuildWorks
{
    // Sealed world issuance only; an issued item is not yet a settled inventory credit.
    internal sealed class BlueprintConstructionPayoutJournal
    {
        internal const string CustodyKey = "ostrix.buildworks.refund.v1";
        internal const int Prepared = 1, SaveIntent = 2, IssuedSaved = 3;
        internal const int Ready = 0, Attempted = 1, Observed = 2;
        private const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BWPI01\r\n");
        private readonly Plans plans;
        [DataContract] internal sealed class Entry
        {
            [DataMember(Order = 0)] internal Guid Id;
            [DataMember(Order = 1)] internal string Blob;
            [DataMember(Order = 2)] internal int State;
            [DataMember(Order = 3)] internal long ObjectUser;
            [DataMember(Order = 4)] internal uint ObjectId;
        }
        [DataContract] internal sealed class Proof
        {
            [DataMember(Order = 0)] internal uint Generation;
            [DataMember(Order = 1)] internal string ProfileHash;
            [DataMember(Order = 2)] internal string InventoryHash;
            [DataMember(Order = 3)] internal string[] ItemHashes;
        }
        [DataContract] internal sealed class Record
        {
            [DataMember(Order = 0)] internal Guid Project;
            [DataMember(Order = 1)] internal Guid Operation;
            [DataMember(Order = 2)] internal long Player;
            [DataMember(Order = 3)] internal long World;
            [DataMember(Order = 4)] internal string RemovalHash;
            [DataMember(Order = 5)] internal long Revision;
            [DataMember(Order = 6)] internal int Phase;
            [DataMember(Order = 7)] internal uint BeforeGeneration;
            [DataMember(Order = 8)] internal string BeforeInfo;
            [DataMember(Order = 9)] internal string BeforeBytes;
            [DataMember(Order = 10)] internal Entry[] Items;
            [DataMember(Order = 11)] internal Proof Saved;
        }
        internal BlueprintConstructionPayoutJournal(Plans plans) { this.plans = plans ?? throw new ArgumentNullException(nameof(plans)); }
        internal Parts.PaidRow[] Paid(Guid project)
        {
            var parts = new Parts(plans);
            var rows = plans.Load(project).Parts.SelectMany(part => parts.Read(project, part.Id)?.Paid ?? Array.Empty<Parts.PaidRow>()).Take(65537).ToArray();
            if (rows.Length > 65536) throw new InvalidDataException("Payout row budget exceeded; no issuance is permitted.");
            return rows;
        }
        internal Record Prepare(Guid project, Guid operation, long player, uint generation, BlueprintForeignPartData inventory, Entry[] entries)
        {
            using (plans.Lock(project))
            {
                var removal = new BlueprintConstructionCancellationJournal(plans).Read(project);
                if (removal?.Phase != BlueprintConstructionCancellationJournal.RemovedSaved || operation != removal.Operation || player != removal.Player ||
                    generation < removal.Saved.Generation || generation == uint.MaxValue) throw new InvalidOperationException("Payout requires the same saved removal, payer and current generation.");
                var candidate = new Record { Project = project, Operation = operation, Player = player, World = removal.World,
                    RemovalHash = BlueprintConstructionCancellationJournal.Fingerprint(removal), Revision = 1, Phase = Prepared,
                    BeforeGeneration = generation, BeforeInfo = inventory?.Info, BeforeBytes = inventory?.Zdo, Items = entries };
                var current = Read(project);
                if (current != null)
                {
                    if (current.Phase != Prepared || current.Items.Any(e => e.State != Ready) || !Encode(current).SequenceEqual(Encode(candidate)))
                        throw new InvalidOperationException("Payout already has intent/evidence or its inputs differ; no repeated issuance.");
                    return current;
                }
                Write(candidate, false); return Read(project);
            }
        }
        internal Record BeginItem(Guid project, Guid operation, int index)
        {
            using (plans.Lock(project))
            {
                var current = Require(project, operation);
                if (current.Phase != Prepared || index < 0 || index >= current.Items.Length || current.Items[index].State != Ready ||
                    current.Items.Take(index).Any(e => e.State != Observed)) throw new InvalidOperationException("Payout attempt cannot be repeated or reordered.");
                current.Items[index].State = Attempted; current.Revision++;
                Write(current, true); return Read(project);
            }
        }
        internal Record Observe(Guid project, Guid operation, int index, long user, uint id)
        {
            using (plans.Lock(project))
            {
                var current = Require(project, operation);
                if (index < 0 || index >= current.Items.Length) throw new ArgumentOutOfRangeException(nameof(index));
                var item = current.Items[index];
                if (current.Phase != Prepared || item.State == Ready || user == 0 || id == 0) throw new InvalidOperationException("Payout observation requires a native attempt and identity.");
                if (item.State == Observed)
                {
                    if (item.ObjectUser != user || item.ObjectId != id) throw new InvalidOperationException("Payout native observation changed.");
                    return current;
                }
                item.State = Observed; item.ObjectUser = user; item.ObjectId = id; current.Revision++;
                Write(current, true); return Read(project);
            }
        }
        internal Record BeginSave(Guid project, Guid operation)
        {
            using (plans.Lock(project))
            {
                var current = Require(project, operation);
                if (current.Items.Any(e => e.State != Observed)) throw new InvalidOperationException("Unobserved payout cannot claim persistence.");
                if (current.Phase != Prepared) return current;
                current.Phase = SaveIntent; current.Revision++; Write(current, true); return Read(project);
            }
        }
        internal Record ConfirmSaved(Guid project, Guid operation, Proof proof)
        {
            using (plans.Lock(project))
            {
                var current = Require(project, operation);
                if (current.Phase == Prepared) throw new InvalidOperationException("Payout save intent is required.");
                if (current.Phase == IssuedSaved)
                {
                    if (!Encode(current.Saved).SequenceEqual(Encode(proof))) throw new InvalidOperationException("Payout save proof changed.");
                    return current;
                }
                current.Saved = proof; current.Phase = IssuedSaved; current.Revision++;
                Write(current, true); return Read(project);
            }
        }
        internal Record Read(Guid project)
        {
            string path = PathFor(project); if (!File.Exists(path)) return null; CheckFile(path);
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var reader = new BinaryReader(file))
            {
                if (file.Length < 44 || file.Length > MaximumBytes + 44L || !reader.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Invalid payout framing.");
                int size = reader.ReadInt32();
                if (size < 1 || size > MaximumBytes || file.Length != size + 44L) throw new InvalidDataException("Invalid payout size.");
                byte[] bytes = reader.ReadBytes(size);
                using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(reader.ReadBytes(32))) throw new InvalidDataException("Payout checksum differs.");
                Record record;
                using (var input = new MemoryStream(bytes, false)) record = (Record)Serializer(typeof(Record)).ReadObject(input);
                Validate(project, record);
                if (!Encode(record).SequenceEqual(bytes)) throw new InvalidDataException("Noncanonical payout fields.");
                return record;
            }
        }
        private Record Require(Guid project, Guid operation)
        {
            var current = Read(project);
            if (current == null || operation == Guid.Empty || current.Operation != operation) throw new InvalidOperationException("Payout operation differs.");
            return current;
        }
        private void Validate(Guid project, Record r)
        {
            var removal = new BlueprintConstructionCancellationJournal(plans).Read(project);
            if (r == null || removal?.Phase != BlueprintConstructionCancellationJournal.RemovedSaved || r.Project != project || r.Operation != removal.Operation ||
                r.Player != removal.Player || r.World != removal.World || r.RemovalHash != BlueprintConstructionCancellationJournal.Fingerprint(removal) ||
                r.BeforeGeneration < removal.Saved.Generation || r.BeforeGeneration == uint.MaxValue || r.Phase < Prepared || r.Phase > IssuedSaved ||
                r.Items == null || r.Items.Length > 65536) throw new InvalidDataException("Payout/removal identity differs.");
            Parts.ValidateInventory(r.BeforeInfo, r.BeforeBytes);
            var paid = Paid(project);
            if (r.Items.Length != paid.Length || r.Items.Any(e => e == null || e.Id == Guid.Empty || e.State < Ready || e.State > Observed) ||
                r.Items.Select(e => e.Id).Distinct().Count() != r.Items.Length) throw new InvalidDataException("Payout must bind every original paid row once.");
            bool suffix = false;
            for (int i = 0; i < r.Items.Length; ++i)
            {
                var item = r.Items[i];
                if (suffix && item.State != Ready) throw new InvalidDataException("Payout observations are not a contiguous prefix.");
                if (item.State != Observed) suffix = true;
                if (item.State == Observed ? item.ObjectUser == 0 || item.ObjectId == 0 : item.ObjectUser != 0 || item.ObjectId != 0)
                    throw new InvalidDataException("Payout native identity is missing or premature.");
                ValidateItem(paid[i], item);
            }
            var native = r.Items.Where(e => e.State == Observed).ToArray();
            if (native.Select(e => (e.ObjectUser, e.ObjectId)).Distinct().Count() != native.Length)
                throw new InvalidDataException("Payout rows cannot share a native item identity.");
            if (r.Revision != 1L + r.Items.Sum(e => e.State) + r.Phase - 1 || r.Phase != Prepared && r.Items.Any(e => e.State != Observed))
                throw new InvalidDataException("Payout phase/revision differs.");
            if (r.Phase != IssuedSaved)
            { if (r.Saved != null) throw new InvalidDataException("Payout falsely claims persistence."); return; }
            if (r.Saved == null || r.Saved.Generation != r.BeforeGeneration + 1 || !Hash(r.Saved.ProfileHash, 128) ||
                r.Saved.InventoryHash != Digest(Convert.FromBase64String(r.BeforeBytes)) || r.Saved.ItemHashes == null ||
                !r.Saved.ItemHashes.SequenceEqual(r.Items.Select(e => Digest(Convert.FromBase64String(e.Blob)))))
                throw new InvalidDataException("Payout paired save/items proof differs.");
        }
        private static void ValidateItem(Parts.PaidRow paid, Entry entry)
        {
            BlueprintContainerInventory.Item Parse(string bytes, int quantity)
            {
                byte[] native = Convert.FromBase64String(bytes);
                if (native.Length < 2 || native.Length > 65536 || native[0] != 109 && native[0] != 110) return null;
                // Reuse the inventory codec: ItemStand intentionally rejects stacks larger than one.
                using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
                {
                    writer.Write((int)native[0]); writer.Write((ushort)1); writer.Write(native, 1, native.Length - 1);
                    var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                        "256:256:1:" + quantity.ToString(CultureInfo.InvariantCulture) + "|" + paid.Prefab, Convert.ToBase64String(stream.ToArray()), null);
                    return BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) ? items[0] : null;
                }
            }
            if (entry.Blob == null || entry.Blob.Length > 90000) throw new InvalidDataException("Payout item exceeds budget.");
            var original = Parse(paid.Properties, 1); var returned = Parse(entry.Blob, paid.Paid);
            if (original == null || returned == null || original.CustomData.ContainsKey(CustodyKey) || returned.SourceVersion != 110 ||
                returned.Stack != paid.Paid || returned.X != 0 || returned.Y != 0 || returned.Equipped || original.Prefab != returned.Prefab ||
                original.DurabilityHundredths != returned.DurabilityHundredths || original.LegacyDurability != returned.LegacyDurability ||
                original.WorldLevel != returned.WorldLevel || original.PickedUp != returned.PickedUp || original.Quality != returned.Quality ||
                original.Variant != returned.Variant || original.CrafterId != returned.CrafterId || original.CrafterName != returned.CrafterName ||
                original.Cheated != returned.Cheated || returned.CustomData.Count != original.CustomData.Count + 1 ||
                !returned.CustomData.TryGetValue(CustodyKey, out string token) || token != entry.Id.ToString("N") ||
                original.CustomData.Any(pair => !returned.CustomData.TryGetValue(pair.Key, out string value) || value != pair.Value))
                throw new InvalidDataException("Payout differs from provided item properties or actual paid quantity.");
        }
        private void Write(Record r, bool replace)
        {
            Validate(r.Project, r); byte[] bytes = Encode(r); string target = PathFor(r.Project), temp = Path.Combine(plans.DirectoryPath, Guid.NewGuid().ToString("N") + ".preparing");
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
            return Path.Combine(CompositeBlueprintStore.ValidateExchangeDirectory(plans.DirectoryPath), project.ToString("N") + ".bwpayout");
        }
        private static void CheckFile(string path)
        { if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Payout cannot be a reparse point."); }
        private static byte[] Encode<T>(T value)
        {
            using (var stream = new MemoryStream())
            { Serializer(typeof(T)).WriteObject(stream, value); if (stream.Length > MaximumBytes) throw new InvalidDataException("Payout exceeds budget."); return stream.ToArray(); }
        }
        private static DataContractJsonSerializer Serializer(Type type) => new DataContractJsonSerializer(type, new DataContractJsonSerializerSettings { MaxItemsInObjectGraph = 524288 });
        private static bool Hash(string text, int size) => text != null && text.Length == size && text.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F');
        private static string Digest(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", ""); }
    }
}
