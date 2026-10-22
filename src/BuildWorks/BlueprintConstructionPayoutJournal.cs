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
        internal const string ReceiptPrefix = "ostrix.buildworks.collected.v1.";
        internal static readonly int ItemMarkerKey = "ostrix_buildworks_refund_item_v1".GetStableHashCode();
        internal const int CreditIntent = 1, ProfileCredited = 2;
        internal const int CleanupIntent = 1, CleanupSaved = 2;
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
            [DataMember(Order = 12, EmitDefaultValue = false)] internal Collection[] Collections;
            [DataMember(Order = 13, EmitDefaultValue = false)] internal TerminalBinding Terminal;
            internal uint MinimumWorldGeneration => Math.Max(Math.Max(Terminal?.Generation ?? 0, Saved?.Generation ?? 0),
                Collections?.Select(c => c?.Removal?.SavedGeneration ?? 0u).DefaultIfEmpty(0u).Max() ?? 0u);
        }
        [DataContract] internal sealed class TerminalBinding
        {
            [DataMember(Order = 0)] internal string IssuanceHash;
            [DataMember(Order = 1)] internal uint Generation;
            [DataMember(Order = 2)] internal string ProfileHash;
        }
        [DataContract] internal sealed class Collection
        {
            [DataMember(Order = 0)] internal Guid Entry;
            [DataMember(Order = 1)] internal int Phase;
            [DataMember(Order = 2)] internal int X;
            [DataMember(Order = 3)] internal int Y;
            [DataMember(Order = 4)] internal string BeforeInfo;
            [DataMember(Order = 5)] internal string BeforeBytes;
            [DataMember(Order = 6)] internal string AfterInfo;
            [DataMember(Order = 7)] internal string AfterBytes;
            [DataMember(Order = 8)] internal string BeforeProfileHash;
            [DataMember(Order = 9)] internal string SavedProfileHash;
            [DataMember(Order = 10)] internal string ItemBlob;
            [DataMember(Order = 11, EmitDefaultValue = false)] internal Cleanup Removal;
        }
        [DataContract] internal sealed class Cleanup
        {
            [DataMember(Order = 0)] internal int Phase;
            [DataMember(Order = 1)] internal uint BeforeGeneration;
            [DataMember(Order = 2)] internal string ProfileHash;
            [DataMember(Order = 3)] internal uint SavedGeneration;
        }
        internal BlueprintConstructionPayoutJournal(Plans plans) { this.plans = plans ?? throw new ArgumentNullException(nameof(plans)); }
        internal static void RequireIssuanceBudget(BlueprintForeignPartData inventory, IEnumerable<Entry> entries)
        {
            if (inventory?.Info == null || inventory.Zdo == null || entries == null)
                throw new InvalidDataException("Payout capacity requires the current inventory and exact returned items.");
            var items = new List<Entry>();
            long rawBytes = (long)inventory.Info.Length + inventory.Zdo.Length;
            if (rawBytes > MaximumBytes) throw new InvalidDataException("Payout exceeds budget before removal or issuance.");
            foreach (var entry in entries)
            {
                if (entry?.Blob == null || items.Count >= 65536)
                    throw new InvalidDataException("Payout item/row budget is invalid before removal or issuance.");
                rawBytes += entry.Blob.Length;
                if (rawBytes > MaximumBytes) throw new InvalidDataException("Payout exceeds budget before removal or issuance.");
                items.Add(new Entry { Id = entry.Id, Blob = entry.Blob, State = Observed, ObjectUser = 1, ObjectId = 1 });
            }
            // A lower bound only: real native IDs, later inventory and collection/terminal fields may need more room.
            var minimum = new Record { Project = Guid.Empty, Operation = Guid.Empty, Player = 1, World = 1,
                RemovalHash = new string('0', 64), Revision = 2L * items.Count + 3, Phase = IssuedSaved, BeforeGeneration = 1,
                BeforeInfo = inventory.Info, BeforeBytes = inventory.Zdo, Items = items.ToArray(),
                Saved = new Proof { Generation = 1, ProfileHash = new string('0', 128), InventoryHash = new string('0', 64),
                    ItemHashes = Enumerable.Repeat(new string('0', 64), items.Count).ToArray() } };
            Encode(minimum);
        }

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
                RequireActive(project);
                var removal = new BlueprintConstructionCancellationJournal(plans).Read(project);
                if (removal?.Phase != BlueprintConstructionCancellationJournal.RemovedSaved || operation != removal.Operation || player != removal.Player ||
                    generation < removal.Saved.Generation || generation == uint.MaxValue) throw new InvalidOperationException("Payout requires the same saved removal, payer and current generation.");
                RequireIssuanceBudget(inventory, entries);
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
                RequireActive(project);
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
                RequireActive(project);
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
                RequireActive(project);
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
                RequireActive(project);
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
        internal Record BeginCredit(Guid project, Guid operation, int index, BlueprintForeignPartData before,
            string cleanItem, int x, int y, string beforeProfileHash)
        {
            using (plans.Lock(project))
            {
                var r = Require(project, operation);
                if (r.Phase != IssuedSaved || index < 0 || index >= r.Items.Length ||
                    r.Collections?.ElementAtOrDefault(index) != null ||
                    r.Collections?.Any(c => c != null && (c.Phase != ProfileCredited || c.Removal?.Phase == CleanupIntent)) == true)
                    throw new InvalidOperationException("Credit intent cannot be repeated or started while another row is unsettled.");
                var after = CreditInventory(before, cleanItem, Paid(project)[index], x, y);
                var credit = new Collection { Entry = r.Items[index].Id, Phase = CreditIntent, X = x, Y = y,
                    BeforeInfo = before.Info, BeforeBytes = before.Zdo, AfterInfo = after.Info, AfterBytes = after.Zdo,
                    BeforeProfileHash = beforeProfileHash, ItemBlob = cleanItem };
                // Slots bind immutable payout rows; an empty slot has never begun credit.
                if (r.Collections == null || r.Collections.Length <= index) Array.Resize(ref r.Collections, index + 1);
                r.Collections[index] = credit;
                r.Revision++; Write(r, true); return Read(project);
            }
        }
        internal Record ConfirmCredit(Guid project, Guid operation, int index, NativePlayerProfileReadback.CustomDataEvidence proof)
            => ConfirmCredit(project, operation, index, proof?.Player ?? 0, proof?.ProfileHash, proof?.CustomData);
        internal Record ConfirmSavedCredit(Guid project, Guid operation, int index, NativePlayerProfileReadback.Evidence proof)
            => ConfirmCredit(project, operation, index, proof?.Player ?? 0, proof?.ProfileHash, proof?.CustomData, proof);
        private Record ConfirmCredit(Guid project, Guid operation, int index, long player, string hash, IReadOnlyDictionary<string, string> customData,
            NativePlayerProfileReadback.Evidence full = null)
        {
            using (plans.Lock(project))
            {
                var r = Require(project, operation);
                if (index < 0 || r.Collections == null || index >= r.Collections.Length || r.Collections[index] == null || player != r.Player || customData == null ||
                    !Hash(hash, 128) || !customData.TryGetValue(ReceiptKey(r.Items[index].Id), out string value) || value != ReceiptValue(r, index))
                    throw new InvalidOperationException("Saved canonical receipt does not bind this payer and original payout row.");
                if (full != null && (full.Inventory?.Info != r.Collections[index].AfterInfo || full.Inventory?.Zdo != r.Collections[index].AfterBytes))
                    throw new InvalidOperationException("Fresh saved profile differs from the exact credited inventory.");
                if (r.Collections[index].Phase == ProfileCredited) return r;
                if (full == null) throw new InvalidOperationException("An unsettled credit requires its exact saved inventory and receipt pair.");
                r.Collections[index].Phase = ProfileCredited; r.Collections[index].SavedProfileHash = hash;
                r.Revision++; Write(r, true); return Read(project);
            }
        }
        internal static string ReceiptKey(Guid entry) => ReceiptPrefix + entry.ToString("N");
        internal Record BeginCleanup(Guid project, Guid operation, int index, uint generation, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            using (plans.Lock(project))
            {
                var r = Require(project, operation);
                RequireCleanupReceipt(r, index, profile);
                if (r.Collections.Any(c => c != null && c.Phase != ProfileCredited) || generation == 0 || generation == uint.MaxValue || generation < r.Saved.Generation ||
                    r.Collections.Where((c, i) => i != index).Any(c => c?.Removal?.Phase == CleanupIntent))
                    throw new InvalidOperationException("Cleanup requires one current saved-world intent at a time.");
                var c = r.Collections[index];
                if (c.Removal != null)
                {
                    if (c.Removal.BeforeGeneration != generation || c.Removal.ProfileHash != profile.ProfileHash)
                        throw new InvalidOperationException("Cleanup intent or paired profile changed; no repeated removal.");
                    return r;
                }
                if (generation < r.MinimumWorldGeneration) throw new InvalidOperationException("New cleanup predates the known saved world.");
                c.Removal = new Cleanup { Phase = CleanupIntent, BeforeGeneration = generation, ProfileHash = profile.ProfileHash };
                r.Revision++; Write(r, true); return Read(project);
            }
        }
        internal Record ConfirmCleanup(Guid project, Guid operation, int index, NativeWorldStorageReadback.AbsenceEvidence absence,
            NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            using (plans.Lock(project))
            {
                var r = Require(project, operation);
                RequireCleanupReceipt(r, index, profile);
                var cleanup = r.Collections[index].Removal;
                if (cleanup == null || absence == null || absence.World != r.World || absence.MarkerKey != ItemMarkerKey ||
                    !absence.CompleteMappedScan || absence.Operations.Count != 1 || absence.Operations[0] != r.Items[index].Id ||
                    absence.Generation != cleanup.BeforeGeneration + 1 || profile.ProfileHash != cleanup.ProfileHash)
                    throw new InvalidOperationException("Cleanup needs exact saved-world absence paired with the same permanent profile receipt.");
                if (cleanup.Phase == CleanupSaved) return r;
                cleanup.Phase = CleanupSaved; cleanup.SavedGeneration = absence.Generation;
                r.Revision++; Write(r, true); return Read(project);
            }
        }
        private static void RequireCleanupReceipt(Record r, int index, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            if (r.Phase != IssuedSaved || index < 0 || r.Collections == null || index >= r.Collections.Length ||
                r.Collections[index]?.Phase != ProfileCredited || profile == null || profile.CustomData == null || profile.Player != r.Player || !Hash(profile.ProfileHash, 128) ||
                !profile.CustomData.TryGetValue(ReceiptKey(r.Items[index].Id), out string value) || value != ReceiptValue(r, index))
                throw new InvalidOperationException("Only a durably credited row with its payer's canonical receipt permits source cleanup.");
        }
        internal static string ReceiptValue(Record r, int index)
        {
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write(r.World); writer.Write(r.Player); writer.Write(r.Project.ToByteArray()); writer.Write(r.Operation.ToByteArray());
                writer.Write(r.RemovalHash); writer.Write(r.Items[index].Id.ToByteArray()); writer.Write(r.Items[index].Blob);
                writer.Flush(); return Digest(stream.ToArray());
            }
        }
        internal static BlueprintForeignPartData CreditInventory(BlueprintForeignPartData before, string cleanItem, Parts.PaidRow paid, int x, int y)
        {
            Parts.ValidateInventory(before?.Info, before?.Zdo);
            var original = ParseItem(paid.Properties, paid.Prefab, 1);
            var clean = ParseItem(cleanItem, paid.Prefab, paid.Paid);
            if (original == null || clean == null || clean.SourceVersion != 110 || !clean.PickedUp || clean.Equipped || clean.X != x || clean.Y != y ||
                !SameProperties(original, clean) || clean.CustomData.Count != original.CustomData.Count ||
                original.CustomData.ContainsKey(CustodyKey) || clean.CustomData.ContainsKey(CustodyKey) ||
                original.CustomData.Any(p => !clean.CustomData.TryGetValue(p.Key, out string value) || value != p.Value))
                throw new InvalidDataException("Clean credit differs from the actual provided item properties.");
            if (!BlueprintContainerInventory.TryRead(before, name => name.GetStableHashCode(), out var old))
                throw new InvalidDataException("Credit baseline inventory is invalid.");
            string[] fields = before.Info.Split('|')[0].Split(':');
            int width = int.Parse(fields[0], CultureInfo.InvariantCulture), height = int.Parse(fields[1], CultureInfo.InvariantCulture);
            byte[] bytes = Convert.FromBase64String(before.Zdo), item = Convert.FromBase64String(cleanItem);
            if (bytes[0] != 110 || old.Count >= 4096 || x < 0 || y < 0 || x >= width || y >= height || old.Any(i => i.X == x && i.Y == y))
                throw new InvalidDataException("An empty bounded native inventory cell is required.");
            byte[] after = new byte[checked(bytes.Length + item.Length - 1)];
            Array.Copy(bytes, after, bytes.Length); after[4] = (byte)(old.Count + 1); after[5] = (byte)((old.Count + 1) >> 8);
            Array.Copy(item, 1, after, bytes.Length, item.Length - 1);
            string info = string.Join(":", new[] { width, height, old.Count + 1, checked(old.Sum(i => i.Stack) + paid.Paid) }
                .Select(n => n.ToString(CultureInfo.InvariantCulture)));
            info += "|" + string.Join("|", old.Select(i => i.Prefab).Concat(new[] { paid.Prefab }).Distinct().OrderBy(n => n, StringComparer.Ordinal));
            var result = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, info, Convert.ToBase64String(after), null);
            Parts.ValidateInventory(result.Info, result.Zdo); return result;
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
            if (current.Terminal != null && !plans.Load(project).Cancelled)
                throw new InvalidOperationException("Cancellation commit is pending; preserve terminal evidence before collection.");
            return current;
        }
        private void RequireActive(Guid project)
        {
            if (plans.Load(project).Cancelled) throw new InvalidOperationException("Cancelled construction cannot issue resources again.");
        }
        internal static string IssuanceFingerprint(Record record)
        {
            if (record?.Phase != IssuedSaved || record.Saved == null) throw new InvalidOperationException("Saved issuance is required.");
            return Digest(Encode(new Record { Project = record.Project, Operation = record.Operation, Player = record.Player, World = record.World,
                RemovalHash = record.RemovalHash, Phase = IssuedSaved, BeforeGeneration = record.BeforeGeneration,
                BeforeInfo = record.BeforeInfo, BeforeBytes = record.BeforeBytes, Items = record.Items, Saved = record.Saved }));
        }
        // Only ProjectStore.CancelPaid calls this while holding the project lock.
        internal void BindTerminalUnderLock(Record issued, uint generation, string profileHash)
        {
            if (issued.Terminal != null) return;
            issued.Terminal = new TerminalBinding { IssuanceHash = IssuanceFingerprint(issued), Generation = generation, ProfileHash = profileHash };
            issued.Revision++; Write(issued, true);
        }
        private void Validate(Guid project, Record r)
        {
            var removal = new BlueprintConstructionCancellationJournal(plans).Read(project);
            var plan = plans.Load(project);
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
            if (r.Collections != null)
            {
                if (r.Phase != IssuedSaved || r.Collections.Length == 0 || r.Collections.Length > r.Items.Length || r.Collections.All(c => c == null))
                    throw new InvalidDataException("Credit records require saved issuance and at least one bound row.");
                for (int i = 0; i < r.Collections.Length; i++)
                {
                    var c = r.Collections[i];
                    if (c == null) continue;
                    if (c.Entry != r.Items[i].Id || c.Phase < CreditIntent || c.Phase > ProfileCredited || !Hash(c.BeforeProfileHash, 128) ||
                        (c.Phase == CreditIntent ? c.SavedProfileHash != null : !Hash(c.SavedProfileHash, 128)))
                        throw new InvalidDataException("Credit identity, phase or profile proof differs.");
                    var before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, c.BeforeInfo, c.BeforeBytes, null);
                    var after = CreditInventory(before, c.ItemBlob, paid[i], c.X, c.Y);
                    if (after.Info != c.AfterInfo || after.Zdo != c.AfterBytes) throw new InvalidDataException("Credit is not the exact single-cell inventory delta.");
                    var cleanup = c.Removal;
                    if (cleanup != null && (c.Phase != ProfileCredited || cleanup.Phase < CleanupIntent || cleanup.Phase > CleanupSaved ||
                        cleanup.BeforeGeneration == 0 || cleanup.BeforeGeneration == uint.MaxValue || r.Saved == null ||
                        cleanup.BeforeGeneration < r.Saved.Generation || !Hash(cleanup.ProfileHash, 128) ||
                        (cleanup.Phase == CleanupIntent ? cleanup.SavedGeneration != 0 : cleanup.SavedGeneration != cleanup.BeforeGeneration + 1)))
                        throw new InvalidDataException("Cleanup phase, credit or paired save proof differs.");
                }
                if (r.Collections.Count(c => c?.Removal?.Phase == CleanupIntent) > 1 || r.Collections.Count(c => c?.Phase == CreditIntent) > 1)
                    throw new InvalidDataException("Concurrent credit or cleanup intents are not permitted.");
                if (r.Collections.Any(c => c?.Phase == CreditIntent) && r.Collections.Any(c => c?.Removal?.Phase == CleanupIntent))
                    throw new InvalidDataException("Unsettled credit and cleanup intents cannot overlap.");
            }
            if (plan.Cancelled && (r.Terminal == null || plan.CancelOperation != r.Operation || r.Phase != IssuedSaved))
                throw new InvalidDataException("Cancelled plan lacks its saved issuance binding.");
            if (r.Revision != 1L + r.Items.Sum(e => e.State) + r.Phase - 1 + (r.Collections?.Sum(c => (long)(c?.Phase ?? 0) + (c?.Removal?.Phase ?? 0)) ?? 0) + (r.Terminal == null ? 0 : 1) || r.Phase != Prepared && r.Items.Any(e => e.State != Observed))
                throw new InvalidDataException("Payout phase/revision differs.");
            if (r.Phase != IssuedSaved)
            { if (r.Saved != null || r.Terminal != null) throw new InvalidDataException("Payout falsely claims persistence."); return; }
            if (r.Saved == null || r.Saved.Generation != r.BeforeGeneration + 1 || !Hash(r.Saved.ProfileHash, 128) ||
                r.Saved.InventoryHash != Digest(Convert.FromBase64String(r.BeforeBytes)) || r.Saved.ItemHashes == null ||
                !r.Saved.ItemHashes.SequenceEqual(r.Items.Select(e => Digest(Convert.FromBase64String(e.Blob)))))
                throw new InvalidDataException("Payout paired save/items proof differs.");
            if (r.Terminal != null && (!Hash(r.Terminal.IssuanceHash, 64) || r.Terminal.IssuanceHash != IssuanceFingerprint(r) ||
                r.Terminal.Generation < r.Saved.Generation || r.Terminal.Generation == uint.MaxValue || !Hash(r.Terminal.ProfileHash, 128)))
                throw new InvalidDataException("Terminal binding differs from immutable saved issuance.");
        }
        internal static void ValidateItem(Parts.PaidRow paid, Entry entry)
        {
            if (entry.Blob == null || entry.Blob.Length > 90000) throw new InvalidDataException("Payout item exceeds budget.");
            var original = ParseItem(paid.Properties, paid.Prefab, 1); var returned = ParseItem(entry.Blob, paid.Prefab, paid.Paid);
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
        private static BlueprintContainerInventory.Item ParseItem(string bytes, string prefab, int quantity)
        {
            if (bytes == null || bytes.Length > 90000) return null;
            byte[] native = Convert.FromBase64String(bytes);
            if (native.Length < 2 || native.Length > 65536 || native[0] != 109 && native[0] != 110) return null;
            using (var stream = new MemoryStream()) using (var writer = new BinaryWriter(stream))
            {
                writer.Write((int)native[0]); writer.Write((ushort)1); writer.Write(native, 1, native.Length - 1);
                var data = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,
                    "256:256:1:" + quantity.ToString(CultureInfo.InvariantCulture) + "|" + prefab, Convert.ToBase64String(stream.ToArray()), null);
                return BlueprintContainerInventory.TryRead(data, name => name.GetStableHashCode(), out var items) ? items[0] : null;
            }
        }
        private static bool SameProperties(BlueprintContainerInventory.Item a, BlueprintContainerInventory.Item b) =>
            a.Prefab == b.Prefab && a.DurabilityHundredths == b.DurabilityHundredths && a.LegacyDurability == b.LegacyDurability &&
            a.WorldLevel == b.WorldLevel && a.Quality == b.Quality && a.Variant == b.Variant && a.CrafterId == b.CrafterId &&
            a.CrafterName == b.CrafterName && a.Cheated == b.Cheated;
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
