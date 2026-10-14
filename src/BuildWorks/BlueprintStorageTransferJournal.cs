using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Write-ahead evidence, not a two-inventory transaction or permission to replay/refund.
    internal sealed class BlueprintStorageTransferJournal : IDisposable
    {
        private const int MaximumBytes = 16 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BWSTJ4\r\n");
        private static readonly byte[] LegacyMagic3 = Encoding.ASCII.GetBytes("BWSTJ3\r\n");
        private static readonly byte[] LegacyMagic2 = Encoding.ASCII.GetBytes("BWSTJ2\r\n");
        private static readonly byte[] LegacyMagic = Encoding.ASCII.GetBytes("BWSTJ1\r\n");
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        internal enum Direction { ToContainer, ToPlayer }
        internal sealed class Item
        {
            internal string Prefab, SourceSnapshot, Properties;
            internal int SourceX, SourceY, SourceQuantity, Amount, X, Y;
        }
        internal sealed class Plan
        {
            internal Guid Operation;
            internal Guid Parent;
            internal Direction Route;
            internal long World, Player, TargetUser;
            internal uint TargetId;
            internal int Width, Height;
            internal long Creator;
            internal int PrefabHash;
            internal string PieceName;
            internal Item[] Items;
            internal Payment[] Paid = Array.Empty<Payment>();
            internal Guid TargetMarker => Route == Direction.ToContainer ? Operation : Parent;
        }
        internal sealed class Payment { internal string Prefab, Properties; internal int Before, Paid, X, Y; }
        internal sealed class Attempt
        {
            internal int Index, SourceBefore, SourceX, SourceY;
            internal bool Queued, Equipped, EmptyCell;
            internal uint Revision, ContainerRevision;
            internal string SourceSnapshot, Properties, SourceManifest, DestinationManifest, ZdoHash;
        }
        internal sealed class Observation
        {
            internal int Index, State, SourceBefore, SourceAfter, DestinationAfter, SourceObjectStack;
            internal bool Attempted, BeforeObserved, SourcePresent, LocalSave, QueuedBefore, EquippedBefore, QueuedAfter, EquippedAfter;
            internal bool? NativeReturn;
            internal string SourceProperties, DestinationProperties, Error;
            internal int SourceX, SourceY;
            internal uint Revision, ContainerRevision;
            internal string SourceManifest, DestinationManifest, ZdoHash;
        }
        internal sealed class Snapshot
        {
            internal int Schema;
            internal Plan Prepared;
            internal readonly List<Observation> Observations = new List<Observation>();
            internal int? InFlight;
            internal Attempt Pending;
            internal bool Terminal;
            internal Persistence SavedPair;
            // Even fully observed local moves need source/target persistence and server reconciliation.
            internal bool RequiresReconciliation => true;
        }
        internal sealed class Persistence
        {
            internal uint BeforeGeneration, Generation;
            internal long World, Player;
            internal Guid Marker;
            internal string ProfileHash, PlayerInventoryHash, WorldInventoryHash;
        }

        private readonly FileStream stream;
        private int sequence, next;
        private bool stopped;
        private bool complete, pairRecorded;
        private string lastZdoHash;
        private Plan prepared;
        private Attempt pending;
        private readonly int count;
        private readonly Guid operation;
        private byte[] previousHash = new byte[32];
        internal string Path { get; }

        private BlueprintStorageTransferJournal(string path, FileStream stream, int count, Guid operation)
        { Path = path; this.stream = stream; this.count = count; this.operation = operation; }

        internal static string Root => System.IO.Path.Combine(BepInEx.Paths.ConfigPath, "BuildWorks", "Transfers");

        internal static bool HasUnresolved(long world, string marker = null)
        {
            try { if ((File.GetAttributes(Root) & FileAttributes.Directory) == 0) return true; }
            catch (DirectoryNotFoundException) { return false; }
            catch (FileNotFoundException) { return false; }
            CheckParents(System.IO.Path.GetFullPath(Root));
            string[] paths = Directory.EnumerateFileSystemEntries(Root).Take(129).ToArray();
            if (paths.Length > 128) return true;
            foreach (string path in paths)
            {
                try
                {
                    Snapshot record = Read(path, preparedOnly: true);
                    // Native IDs are remapped on world load. Unmarked/legacy targets cannot be distinguished safely.
                    if (record.Prepared.World == world && (record.Schema < 3 || string.IsNullOrEmpty(marker) ||
                        record.Prepared.TargetMarker.ToString("N") == marker)) return true;
                }
                catch (Exception) { return true; } // Corrupt/unknown evidence cannot authorize destructive cleanup.
            }
            return false;
        }

        internal static Guid ReturnOperationId(Guid parent)
        {
            using (var hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(Encoding.ASCII.GetBytes("BuildWorks-return:" + parent.ToString("N")));
                var bytes = new byte[16]; Array.Copy(digest, bytes, bytes.Length); return new Guid(bytes);
            }
        }

        internal static bool CanStartReturn(Guid parent, long world, long player, Guid marker)
        {
            if (marker != parent || !Directory.Exists(Root)) return false;
            CheckParents(System.IO.Path.GetFullPath(Root));
            string[] paths = Directory.EnumerateFileSystemEntries(Root).Take(129).ToArray();
            if (paths.Length > 127) return false;
            bool found = false;
            foreach (string path in paths)
            {
                Snapshot record;
                try { record = Read(path, preparedOnly: true); } catch (Exception) { return false; }
                Plan plan = record.Prepared;
                if (plan.World != world) continue;
                if (record.Schema < 3) return false;
                if (plan.TargetMarker != marker) continue;
                if (plan.Operation != parent || plan.Player != player || plan.Route != Direction.ToContainer) return false;
                found = true;
            }
            return found;
        }

        internal static bool HasReturnEvidence(Guid parent)
        {
            try { File.GetAttributes(System.IO.Path.Combine(Root, ReturnOperationId(parent).ToString("N") + ".bwtransfer")); return true; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
            catch (Exception) { return true; }
        }

        internal static BlueprintStorageTransferJournal Begin(string directory, Plan plan)
        {
            Validate(plan);
            string root = System.IO.Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory)));
            CheckParents(root); Directory.CreateDirectory(root); CheckParents(root);
            // Retain evidence; never silently delete an old unresolved operation to make space.
            if (Directory.EnumerateFileSystemEntries(root).Take(129).Count() >= 128)
                throw new IOException("Transfer journal directory requires reconciliation before adding more operations.");
            string path = System.IO.Path.Combine(root, plan.Operation.ToString("N") + ".bwtransfer");
            byte[] prepared = Encode(writer =>
            {
                writer.Write(plan.Operation.ToByteArray()); writer.Write(plan.World); writer.Write(plan.Player);
                writer.Write((byte)plan.Route); writer.Write(plan.Parent.ToByteArray());
                writer.Write(plan.TargetUser); writer.Write(plan.TargetId); writer.Write(plan.Width); writer.Write(plan.Height);
                writer.Write(plan.Creator); writer.Write(plan.PrefabHash); writer.Write(plan.PieceName);
                writer.Write(plan.Paid.Length);
                foreach (Payment paid in plan.Paid)
                { writer.Write(paid.Prefab); writer.Write(paid.Properties); writer.Write(paid.Before); writer.Write(paid.Paid); writer.Write(paid.X); writer.Write(paid.Y); }
                writer.Write(plan.Items.Length);
                foreach (Item item in plan.Items)
                {
                    writer.Write(item.Prefab); writer.Write(item.SourceX); writer.Write(item.SourceY); writer.Write(item.SourceQuantity);
                    writer.Write(item.Amount); writer.Write(item.X); writer.Write(item.Y);
                    writer.Write(item.SourceSnapshot); writer.Write(item.Properties);
                }
            });
            // CreateNew and a retained write handle exclude duplicate IDs and concurrent writers.
            var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            var journal = new BlueprintStorageTransferJournal(path, file, plan.Items.Length, plan.Operation);
            try { file.Write(Magic, 0, Magic.Length); journal.Append(1, prepared); journal.prepared = Read(path, preparedOnly: true).Prepared; return journal; }
            catch { journal.Dispose(); throw; } // Preserve even a partial file for inspection; do not retry its ID.
        }

        internal void BeforeMove(Attempt attempt)
        {
            if (attempt == null || attempt.Index != next || pending != null || next >= count)
                throw new InvalidOperationException("Journal move order changed.");
            ValidateAttempt(prepared.Items[next], attempt, prepared.Route);
            byte[] payload = Encode(writer => WriteAttempt(writer, attempt));
            Append(2, payload);
            using (var memory = new MemoryStream(payload, false))
            using (var reader = new BinaryReader(memory, Utf8)) pending = ReadAttempt(reader);
        }

        internal void Observed(Observation observation)
        {
            if (observation == null || observation.Index != next || next >= count || observation.Attempted != (pending != null) ||
                observation.State < 1 || observation.State > 3) throw new InvalidOperationException("Journal observation order changed.");
            ValidateObservation(prepared.Items[next], pending, observation, prepared.Route);
            Append(3, Encode(writer => WriteObservation(writer, observation)));
            pending = null; next++;
            lastZdoHash = observation.ZdoHash;
            if (observation.State != 2 || next == count)
            { bool success = next == count && observation.State == 2; Append(4, Encode(writer => writer.Write(success))); stopped = true; complete = success; }
        }

        internal void RecordPersistence(uint before, uint generation, NativePlayerProfileReadback.Evidence profile, NativeWorldStorageReadback.Evidence world)
        {
            if (!complete || pairRecorded || profile == null || world == null)
                throw new InvalidOperationException("Paired persistence requires one completed local transfer and two readback receipts.");
            var pair = new Persistence
            {
                BeforeGeneration = before,
                Generation = generation,
                World = world.World,
                Player = profile.Player,
                Marker = world.Operation,
                ProfileHash = profile.ProfileHash,
                PlayerInventoryHash = profile.InventoryHash,
                WorldInventoryHash = world.InventorySha256
            };
            ValidatePersistence(prepared, lastZdoHash, pair);
            Append(5, Encode(writer =>
            {
                writer.Write(pair.BeforeGeneration); writer.Write(pair.Generation); writer.Write(pair.World); writer.Write(pair.Player);
                writer.Write(pair.Marker.ToByteArray()); writer.Write(pair.ProfileHash); writer.Write(pair.PlayerInventoryHash); writer.Write(pair.WorldInventoryHash);
            }), afterComplete: true);
            pairRecorded = true;
        }

        private static void ValidatePersistence(Plan plan, string zdoHash, Persistence pair)
        {
            if (pair.BeforeGeneration == 0 || pair.BeforeGeneration == uint.MaxValue || pair.Generation != pair.BeforeGeneration + 1 ||
                pair.World != plan.World || pair.Player != plan.Player || pair.Marker != plan.TargetMarker ||
                pair.ProfileHash == null || pair.ProfileHash.Length != 128 || pair.ProfileHash.Any(c => !Uri.IsHexDigit(c)) ||
                !Digest(pair.PlayerInventoryHash) || !Digest(pair.WorldInventoryHash) || pair.WorldInventoryHash != zdoHash)
                throw new InvalidDataException("Paired persistence identity, generation or inventory evidence differs from the transfer.");
        }

        private void Append(byte kind, byte[] payload, bool afterComplete = false)
        {
            if (stopped && !(afterComplete && complete && !pairRecorded)) throw new InvalidOperationException("Transfer journal is stopped.");
            byte[] body = Encode(writer => { writer.Write(4); writer.Write(operation.ToByteArray()); writer.Write(sequence); writer.Write(kind); writer.Write(previousHash); writer.Write(payload); });
            if (body.Length > MaximumBytes || stream.Length + body.Length + 36 > MaximumBytes)
                throw new InvalidDataException("Transfer journal exceeds its byte budget.");
            try
            {
                using (var hash = SHA256.Create())
                using (var writer = new BinaryWriter(stream, Utf8, true))
                { byte[] digest = hash.ComputeHash(body); writer.Write(body.Length); writer.Write(body); writer.Write(digest); writer.Flush(); stream.Flush(true); previousHash = digest; }
                sequence++;
            }
            catch { complete = false; stopped = true; throw; }
        }

        internal static Snapshot Read(string path, bool preparedOnly = false)
        {
            CheckParents(System.IO.Path.GetFullPath(path));
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new BinaryReader(file, Utf8, true))
            {
                long length = file.Length;
                byte[] magic = reader.ReadBytes(Magic.Length);
                if (length < Magic.Length + 36 || length > MaximumBytes || !magic.SequenceEqual(Magic) && !magic.SequenceEqual(LegacyMagic3) && !magic.SequenceEqual(LegacyMagic2) && !magic.SequenceEqual(LegacyMagic))
                    throw new InvalidDataException("Unknown or incomplete transfer journal.");
                int schema = magic[5] - '0';
                var result = new Snapshot { Schema = schema }; int sequence = 0; byte[] previous = new byte[32]; Guid operation = Guid.Empty;
                while (file.Position < length)
                {
                    if (length - file.Position < 36) throw new InvalidDataException("Partial transfer journal frame.");
                    int size = reader.ReadInt32();
                    if (size < 57 || size > MaximumBytes || size > length - file.Position - 32 || sequence > 8194)
                        throw new InvalidDataException("Invalid transfer journal frame length.");
                    byte[] body = reader.ReadBytes(size), expected = reader.ReadBytes(32);
                    using (var hash = SHA256.Create())
                        if (!hash.ComputeHash(body).SequenceEqual(expected)) throw new InvalidDataException("Transfer journal checksum mismatch.");
                    using (var packet = new MemoryStream(body, false))
                    using (var fields = new BinaryReader(packet, Utf8))
                    {
                        if (fields.ReadInt32() != schema) throw new InvalidDataException("Unknown transfer journal schema.");
                        Guid frameOperation = new Guid(fields.ReadBytes(16));
                        if (operation == Guid.Empty) operation = frameOperation;
                        if (frameOperation == Guid.Empty || frameOperation != operation || fields.ReadInt32() != sequence++)
                            throw new InvalidDataException("Transfer journal identity/sequence mismatch.");
                        byte kind = fields.ReadByte();
                        if (result.Terminal && (schema < 4 || kind != 5 || result.SavedPair != null))
                            throw new InvalidDataException("Unexpected record after completed local transfer.");
                        if (!fields.ReadBytes(32).SequenceEqual(previous)) throw new InvalidDataException("Transfer journal chain mismatch.");
                        previous = expected;
                        if (kind == 1 && result.Prepared == null && sequence == 1)
                        {
                            var plan = new Plan { Operation = new Guid(fields.ReadBytes(16)), World = fields.ReadInt64(), Player = fields.ReadInt64() };
                            if (schema >= 2) { plan.Route = (Direction)fields.ReadByte(); plan.Parent = new Guid(fields.ReadBytes(16)); }
                            plan.TargetUser = fields.ReadInt64(); plan.TargetId = fields.ReadUInt32(); plan.Width = fields.ReadInt32(); plan.Height = fields.ReadInt32();
                            plan.Creator = fields.ReadInt64(); plan.PrefabHash = fields.ReadInt32(); plan.PieceName = fields.ReadString();
                            int paidCount = fields.ReadInt32();
                            if (paidCount < 0 || paidCount > 4096) throw new InvalidDataException("Invalid construction payment count.");
                            plan.Paid = new Payment[paidCount];
                            for (int i = 0; i < paidCount; i++) plan.Paid[i] = new Payment { Prefab = fields.ReadString(), Properties = fields.ReadString(),
                                Before = fields.ReadInt32(), Paid = fields.ReadInt32(), X = fields.ReadInt32(), Y = fields.ReadInt32() };
                            int count = fields.ReadInt32();
                            if (count < 1 || count > 4096) throw new InvalidDataException("Invalid transfer item count.");
                            plan.Items = new Item[count];
                            for (int i = 0; i < count; i++) plan.Items[i] = new Item { Prefab = fields.ReadString(), SourceX = fields.ReadInt32(),
                                SourceY = fields.ReadInt32(), SourceQuantity = fields.ReadInt32(), Amount = fields.ReadInt32(), X = fields.ReadInt32(), Y = fields.ReadInt32(),
                                SourceSnapshot = fields.ReadString(), Properties = fields.ReadString() };
                            Validate(plan);
                            if (plan.Operation != operation || System.IO.Path.GetFileName(path) != operation.ToString("N") + ".bwtransfer")
                                throw new InvalidDataException("Transfer journal filename/plan identity mismatch.");
                            result.Prepared = plan;
                        }
                        else if (kind == 2 && result.Prepared != null && result.InFlight == null && result.Observations.Count < result.Prepared.Items.Length &&
                            result.Observations.All(value => value.State == 2))
                        {
                            Attempt attempt = ReadAttempt(fields);
                            if (attempt.Index != result.Observations.Count) throw new InvalidDataException("Invalid transfer attempt order.");
                            ValidateAttempt(result.Prepared.Items[attempt.Index], attempt, result.Prepared.Route);
                            result.InFlight = attempt.Index; result.Pending = attempt;
                        }
                        else if (kind == 3 && result.Prepared != null && result.Observations.Count < result.Prepared.Items.Length &&
                            result.Observations.All(value => value.State == 2))
                        {
                            Observation observed = ReadObservation(fields);
                            if (observed.Index != result.Observations.Count || observed.Attempted != result.InFlight.HasValue ||
                                observed.State < 1 || observed.State > 3) throw new InvalidDataException("Invalid transfer observation order.");
                            ValidateObservation(result.Prepared.Items[observed.Index], result.Pending, observed, result.Prepared.Route);
                            result.Observations.Add(observed); result.InFlight = null; result.Pending = null;
                        }
                        else if (kind == 4 && result.Prepared != null && result.InFlight == null && result.Observations.Count > 0)
                        {
                            bool completed = fields.ReadBoolean();
                            if (completed != (result.Observations.Count == result.Prepared.Items.Length && result.Observations.All(value => value.State == 2)) ||
                                !completed && result.Observations.Last().State == 2) throw new InvalidDataException("Invalid local terminal record.");
                            result.Terminal = true;
                        }
                        else if (schema == 4 && kind == 5 && result.Terminal && result.InFlight == null && result.SavedPair == null &&
                            result.Observations.Count == result.Prepared.Items.Length && result.Observations.All(value => value.State == 2))
                        {
                            var pair = new Persistence
                            {
                                BeforeGeneration = fields.ReadUInt32(),
                                Generation = fields.ReadUInt32(),
                                World = fields.ReadInt64(),
                                Player = fields.ReadInt64(),
                                Marker = new Guid(fields.ReadBytes(16)),
                                ProfileHash = fields.ReadString(),
                                PlayerInventoryHash = fields.ReadString(),
                                WorldInventoryHash = fields.ReadString()
                            };
                            ValidatePersistence(result.Prepared, result.Observations.Last().ZdoHash, pair); result.SavedPair = pair;
                        }
                        else throw new InvalidDataException("Unknown or reordered transfer journal record.");
                        if (packet.Position != packet.Length) throw new InvalidDataException("Unexpected transfer journal fields.");
                        if (preparedOnly && result.Prepared != null) return result;
                    }
                }
                if (result.Prepared == null || file.Length != length) throw new InvalidDataException("Transfer journal changed while reading.");
                return result;
            }
        }

        private static void Validate(Plan plan)
        {
            if (plan == null || plan.Paid == null || plan.Operation == Guid.Empty || plan.World == 0 || plan.Player == 0 || plan.TargetUser == 0 || plan.TargetId == 0 ||
                plan.Route != Direction.ToContainer && plan.Route != Direction.ToPlayer ||
                plan.Route == Direction.ToContainer && plan.Parent != Guid.Empty ||
                plan.Route == Direction.ToPlayer && (plan.Parent == Guid.Empty || plan.Operation != ReturnOperationId(plan.Parent) || plan.Paid.Length != 0) ||
                plan.Creator != plan.Player || plan.PrefabHash == 0 || string.IsNullOrEmpty(plan.PieceName) || plan.PieceName.Length > 4096 || plan.Paid == null || plan.Paid.Length > 4096 ||
                plan.Width < 1 || plan.Width > 256 || plan.Height < 1 || plan.Height > 256 || plan.Items == null ||
                plan.Items.Length < 1 || plan.Items.Length > 4096 || plan.Items.Length > plan.Width * plan.Height)
                throw new InvalidDataException("Invalid transfer journal plan.");
            var cells = new HashSet<int>();
            long budget = 2048 + Utf8.GetByteCount(plan.PieceName);
            foreach (Payment paid in plan.Paid)
            {
                if (paid == null || string.IsNullOrEmpty(paid.Prefab) || string.IsNullOrEmpty(paid.Properties) || paid.Paid < 1 || paid.Before > ushort.MaxValue || paid.Before < paid.Paid ||
                    paid.X < 0 || paid.X > 255 || paid.Y < 0 || paid.Y > 255) throw new InvalidDataException("Invalid construction debit evidence.");
                budget += Utf8.GetByteCount(paid.Prefab) + (long)Utf8.GetByteCount(paid.Properties) + 32;
            }
            foreach (Item item in plan.Items)
            {
                if (item == null || string.IsNullOrEmpty(item.Prefab) || item.Prefab.Length > 4096 || item.SourceX < 0 || item.SourceX > 255 ||
                    item.SourceY < 0 || item.SourceY > 255 || item.Amount < 1 || item.Amount > ushort.MaxValue || item.SourceQuantity > ushort.MaxValue || item.SourceQuantity < item.Amount ||
                    item.X < 0 || item.X >= plan.Width || item.Y < 0 || item.Y >= plan.Height || !cells.Add(item.Y * plan.Width + item.X) ||
                    string.IsNullOrEmpty(item.SourceSnapshot) || string.IsNullOrEmpty(item.Properties))
                    throw new InvalidDataException("Invalid transfer journal item.");
                if (plan.Route == Direction.ToPlayer && item.SourceQuantity != item.Amount)
                    throw new InvalidDataException("Return must bind the entire known native source stack.");
                long source = Utf8.GetByteCount(item.SourceSnapshot), properties = Utf8.GetByteCount(item.Properties);
                // Preparation + every attempt + both observed properties + bounded error/hashes/framing/terminal.
                budget += Utf8.GetByteCount(item.Prefab) + 2 * source + 2 * properties + 2 * Math.Max(properties, 80) + 8192;
                if (budget > MaximumBytes) throw new InvalidDataException("Complete transfer evidence exceeds its journal budget.");
            }
        }

        private static void ValidateAttempt(Item item, Attempt attempt, Direction route)
        {
            if (attempt.SourceBefore < item.Amount || attempt.SourceBefore > ushort.MaxValue || !attempt.EmptyCell ||
                route == Direction.ToPlayer && (attempt.SourceBefore != item.SourceQuantity || attempt.Queued || attempt.Equipped) ||
                attempt.SourceX != item.SourceX || attempt.SourceY != item.SourceY || attempt.SourceSnapshot != item.SourceSnapshot ||
                attempt.Properties != item.Properties || attempt.Revision != attempt.ContainerRevision ||
                !Digest(attempt.SourceManifest) || !Digest(attempt.DestinationManifest) || !(Digest(attempt.ZdoHash) || attempt.ZdoHash == "missing"))
                throw new InvalidDataException("Invalid transfer attempt evidence.");
        }
        private static void ValidateObservation(Item item, Attempt attempt, Observation observed, Direction route)
        {
            int propertyBudget = Math.Max(Utf8.GetByteCount(item.Properties), 80);
            if (Utf8.GetByteCount(observed.SourceProperties ?? "") > propertyBudget || Utf8.GetByteCount(observed.DestinationProperties ?? "") > propertyBudget ||
                Utf8.GetByteCount(observed.Error ?? "") > 4096) throw new InvalidDataException("Transfer observations exceed reserved evidence space.");
            if (observed.State != 2) return;
            if (!observed.Attempted || !observed.BeforeObserved || !observed.LocalSave || attempt == null ||
                route == Direction.ToPlayer && (observed.SourceBefore != item.Amount || observed.SourceAfter != 0 || observed.SourcePresent) ||
                observed.SourceBefore != attempt.SourceBefore || observed.SourceX != attempt.SourceX || observed.SourceY != attempt.SourceY ||
                observed.QueuedBefore != attempt.Queued || observed.EquippedBefore != attempt.Equipped ||
                observed.SourceAfter != observed.SourceBefore - item.Amount || observed.SourceObjectStack != observed.SourceAfter ||
                observed.SourcePresent != (observed.SourceAfter > 0) || observed.DestinationAfter != item.Amount || observed.QueuedAfter || observed.EquippedAfter ||
                observed.SourceProperties != item.Properties || observed.DestinationProperties != item.Properties ||
                observed.Revision != observed.ContainerRevision || observed.Revision == attempt.Revision ||
                !Digest(observed.SourceManifest) || !Digest(observed.DestinationManifest) || !Digest(observed.ZdoHash) ||
                observed.SourceManifest == attempt.SourceManifest || observed.DestinationManifest == attempt.DestinationManifest || observed.ZdoHash == attempt.ZdoHash)
                throw new InvalidDataException("Transferred observation does not prove exact local custody.");
        }

        private static bool Digest(string text) => text != null && text.Length == 64 && text.All(value => value >= '0' && value <= '9' || value >= 'A' && value <= 'F');
        private static byte[] Encode(Action<BinaryWriter> write)
        {
            using (var memory = new MemoryStream())
            using (var writer = new BinaryWriter(memory, Utf8, true))
            { write(writer); writer.Flush(); return memory.ToArray(); }
        }
        private static void WriteObservation(BinaryWriter writer, Observation value)
        {
            writer.Write(value.Index); writer.Write(value.State); writer.Write(value.SourceBefore); writer.Write(value.SourceAfter);
            writer.Write(value.DestinationAfter); writer.Write(value.SourceObjectStack); writer.Write(value.Attempted); writer.Write(value.BeforeObserved);
            writer.Write(value.SourcePresent); writer.Write(value.LocalSave); writer.Write(value.QueuedBefore); writer.Write(value.EquippedBefore);
            writer.Write(value.QueuedAfter); writer.Write(value.EquippedAfter); writer.Write(value.NativeReturn.HasValue);
            if (value.NativeReturn.HasValue) writer.Write(value.NativeReturn.Value);
            writer.Write(value.SourceProperties ?? ""); writer.Write(value.DestinationProperties ?? ""); writer.Write(value.Error ?? "");
            writer.Write(value.SourceX); writer.Write(value.SourceY); writer.Write(value.Revision); writer.Write(value.ContainerRevision);
            writer.Write(value.SourceManifest ?? ""); writer.Write(value.DestinationManifest ?? ""); writer.Write(value.ZdoHash ?? "");
        }
        private static Observation ReadObservation(BinaryReader reader) => new Observation {
            Index = reader.ReadInt32(), State = reader.ReadInt32(), SourceBefore = reader.ReadInt32(), SourceAfter = reader.ReadInt32(),
            DestinationAfter = reader.ReadInt32(), SourceObjectStack = reader.ReadInt32(), Attempted = reader.ReadBoolean(), BeforeObserved = reader.ReadBoolean(),
            SourcePresent = reader.ReadBoolean(), LocalSave = reader.ReadBoolean(), QueuedBefore = reader.ReadBoolean(), EquippedBefore = reader.ReadBoolean(),
            QueuedAfter = reader.ReadBoolean(), EquippedAfter = reader.ReadBoolean(), NativeReturn = reader.ReadBoolean() ? (bool?)reader.ReadBoolean() : null,
            SourceProperties = reader.ReadString(), DestinationProperties = reader.ReadString(), Error = reader.ReadString(),
            SourceX = reader.ReadInt32(), SourceY = reader.ReadInt32(), Revision = reader.ReadUInt32(), ContainerRevision = reader.ReadUInt32(),
            SourceManifest = reader.ReadString(), DestinationManifest = reader.ReadString(), ZdoHash = reader.ReadString() };

        private static void WriteAttempt(BinaryWriter writer, Attempt value)
        {
            writer.Write(value.Index); writer.Write(value.SourceBefore); writer.Write(value.SourceX); writer.Write(value.SourceY);
            writer.Write(value.Queued); writer.Write(value.Equipped); writer.Write(value.EmptyCell); writer.Write(value.Revision); writer.Write(value.ContainerRevision);
            writer.Write(value.SourceSnapshot); writer.Write(value.Properties); writer.Write(value.SourceManifest); writer.Write(value.DestinationManifest); writer.Write(value.ZdoHash);
        }
        private static Attempt ReadAttempt(BinaryReader reader) => new Attempt { Index = reader.ReadInt32(), SourceBefore = reader.ReadInt32(),
            SourceX = reader.ReadInt32(), SourceY = reader.ReadInt32(), Queued = reader.ReadBoolean(), Equipped = reader.ReadBoolean(), EmptyCell = reader.ReadBoolean(),
            Revision = reader.ReadUInt32(), ContainerRevision = reader.ReadUInt32(), SourceSnapshot = reader.ReadString(), Properties = reader.ReadString(),
            SourceManifest = reader.ReadString(), DestinationManifest = reader.ReadString(), ZdoHash = reader.ReadString() };

        private static void CheckParents(string path)
        {
            for (string current = path; !string.IsNullOrEmpty(current); current = System.IO.Path.GetDirectoryName(current))
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Transfer journal cannot follow a reparse point.");
        }
        public void Dispose() { complete = false; stopped = true; stream.Dispose(); }
    }
}
