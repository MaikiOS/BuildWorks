using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Plan metadata only; paid cancellation consumes saved evidence and never issues resources.
    internal sealed class BlueprintConstructionProjectStore
    {
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("BWCP01\r\n");
        private const int MaximumBytes = BlueprintExchangeFormat.MaximumBytes + CompositeBlueprintStore.MaximumParts * 64 + 256;
        private readonly string directory;
        private readonly long world;
        internal static readonly int ConstructionMarkerKey = "ostrix_buildworks_project_operation_v1".GetStableHashCode();
#if BUILDWORKS_TEST_STAND
        internal static bool QaFailAfterCommit;
#endif
        internal string DirectoryPath => directory;

        internal readonly struct Pose
        {
            private readonly float[] values;
            internal float[] Values => values == null ? throw new InvalidDataException("Missing project pose.") : (float[])values.Clone();
            internal Pose(float[] components)
            {
                if (components == null || components.Length != 10 || components.Any(v => float.IsNaN(v) || float.IsInfinity(v) || Math.Abs(v) > 1000000))
                    throw new InvalidDataException("Invalid project pose.");
                double norm = components.Skip(3).Take(4).Sum(v => (double)v * v);
                if (Math.Abs(norm - 1) > .001 || components.Skip(7).Any(v => v <= 0))
                    throw new InvalidDataException("Project rotation/scale is invalid.");
                values = (float[])components.Clone();
            }
        }
        internal sealed class Part
        {
            internal readonly Guid Id;
            internal readonly int SourceIndex;
            internal readonly Pose WorldPose;
            internal Part(Guid id, int sourceIndex, Pose pose) { Id = id; SourceIndex = sourceIndex; WorldPose = pose; }
        }
        internal sealed class Record
        {
            internal readonly Guid Id, CancelOperation;
            internal readonly long World, Revision;
            internal readonly Pose RootPose;
            internal readonly IReadOnlyList<Part> Parts;
            private readonly byte[] blueprint;
            internal bool Cancelled => CancelOperation != Guid.Empty;
            internal byte[] BlueprintBytes => (byte[])blueprint.Clone();
            internal CompositeBlueprintStore.Blueprint Blueprint => BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(blueprint));
            internal Record(Guid id, long world, long revision, Guid cancel, Pose root, byte[] snapshot, Part[] parts)
            {
                Id = id; World = world; Revision = revision; CancelOperation = cancel; RootPose = root;
                blueprint = (byte[])snapshot.Clone(); Parts = Array.AsReadOnly((Part[])parts.Clone());
            }
        }

        internal BlueprintConstructionProjectStore(string directory, long world)
        {
            if (world == 0) throw new ArgumentException("An actual world identity is required.", nameof(world));
            this.directory = CompositeBlueprintStore.ValidateExchangeDirectory(directory); this.world = world;
            Directory.CreateDirectory(this.directory);
        }

        internal Record Create(Guid id, CompositeBlueprintStore.Blueprint blueprint, Pose root, IReadOnlyList<Pose> partPoses)
        {
            if (id == Guid.Empty || partPoses == null) throw new ArgumentException("Project identity/parts are required.");
            byte[] snapshot = BlueprintExchangeFormat.Encode(blueprint);
            if (partPoses.Count != blueprint.parts.Count) throw new InvalidDataException("Project does not cover its complete blueprint.");
            var record = new Record(id, world, 1, Guid.Empty, root, snapshot,
                partPoses.Select((pose, index) => new Part(Guid.NewGuid(), index, pose)).ToArray());
            using (Lock(id))
            {
                if (File.Exists(PathFor(id)))
                {
                    Record prior = Load(id);
                    if (!prior.Cancelled && prior.BlueprintBytes.SequenceEqual(snapshot) && SamePose(prior.RootPose, root) &&
                        prior.Parts.Select(p => p.WorldPose).Zip(partPoses, SamePose).All(v => v)) return prior;
                    throw new InvalidOperationException("Project identity already has a different plan or terminal state.");
                }
                Commit(record, false);
#if BUILDWORKS_TEST_STAND
                if (QaFailAfterCommit) { QaFailAfterCommit = false; throw new IOException("QA deliberate post-commit observation failure."); }
#endif
                return Load(id);
            }
        }

        internal Record CancelUnpaid(Guid id, long expectedRevision, Guid operation)
        {
            if (operation == Guid.Empty) throw new ArgumentException("Cancellation operation is required.", nameof(operation));
            using (Lock(id))
            {
                Record current = Load(id);
                BlueprintConstructionCancellationJournal.RequireNotStarted(this, id);
                if (current.Cancelled)
                {
                    if (current.CancelOperation == operation && expectedRevision == 1 &&
                        !BlueprintConstructionPartJournal.HasExecutionEvidence(this, current)) return current;
                    throw new InvalidOperationException("Cancelled project operation, original revision or unpaid evidence differs.");
                }
                if (current.Revision != expectedRevision) throw new InvalidOperationException("Project revision changed.");
                if (BlueprintConstructionPartJournal.HasExecutionEvidence(this, current))
                    throw new InvalidOperationException("Project has construction execution evidence; unpaid cancellation is refused.");
                var cancelled = new Record(id, world, 2, operation, current.RootPose, current.BlueprintBytes, current.Parts.ToArray());
                Commit(cancelled, true); return Load(id);
            }
        }

        internal Record Load(Guid id) => Read(id, PathFor(id));

        internal Record CancelPaid(Guid id, long expectedRevision, Guid operation, uint currentGeneration,
            NativeWorldStorageReadback.AbsenceEvidence absence, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            if (operation == Guid.Empty || expectedRevision != 1) throw new ArgumentException("Original cancellation identity is required.");
            using (Lock(id))
            {
                var plan = Load(id);
                if (plan.Cancelled ? plan.Revision != 2 || plan.CancelOperation != operation : plan.Revision != expectedRevision)
                    throw new InvalidOperationException("Cancellation operation or original plan changed.");
                var removal = new BlueprintConstructionCancellationJournal(this).Read(id);
                var payouts = new BlueprintConstructionPayoutJournal(this);
                var issued = payouts.Read(id);
                if (removal?.Phase != BlueprintConstructionCancellationJournal.RemovedSaved || issued?.Phase != BlueprintConstructionPayoutJournal.IssuedSaved ||
                    removal.Operation != operation || issued.Operation != operation || absence == null || profile == null ||
                    currentGeneration == 0 || currentGeneration == uint.MaxValue || absence.Generation != currentGeneration ||
                    currentGeneration < issued.MinimumWorldGeneration || absence.World != world || !absence.CompleteMappedScan ||
                    !plan.Cancelled && issued.Collections?.Any(c => c.Phase != BlueprintConstructionPayoutJournal.ProfileCredited ||
                        c.Removal?.Phase == BlueprintConstructionPayoutJournal.CleanupIntent) == true ||
                    absence.MarkerSets.Count != 1 || absence.MarkerKey != ConstructionMarkerKey ||
                    !absence.Operations.SequenceEqual(removal.Parts.Where(p => p.Phase == BlueprintConstructionPartJournal.Committed).Select(p => p.Operation)) ||
                    profile.Player != issued.Player || profile.ProfileHash == null || profile.ProfileHash.Length != 128 ||
                    !profile.ProfileHash.All(c => c >= '0' && c <= '9' || c >= 'A' && c <= 'F'))
                    throw new InvalidOperationException("Paid cancellation requires current construction absence and the saved payer profile.");
                // Persist the binding first; a crash here leaves a frozen intent, never permission to reissue.
                payouts.BindTerminalUnderLock(issued, currentGeneration, profile.ProfileHash);
                if (plan.Cancelled) return plan;
                Commit(new Record(id, world, 2, operation, plan.RootPose, plan.BlueprintBytes, plan.Parts.ToArray()), true);
#if BUILDWORKS_TEST_STAND
                if (QaFailAfterCommit) { QaFailAfterCommit = false; throw new IOException("QA deliberate post-commit observation failure."); }
#endif
                return Load(id);
            }
        }

        internal IEnumerable<Guid> Ids()
        {
            CompositeBlueprintStore.ValidateExchangeDirectory(directory);
            foreach (string path in Directory.EnumerateFiles(directory, "*.bwproject").OrderBy(p => p, StringComparer.Ordinal))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(path), "N", out Guid id) || id == Guid.Empty)
                    throw new InvalidDataException("Invalid project filename.");
                yield return id;
            }
        }

        private Record Read(Guid id, string path, byte[] expected = null)
        {
            CheckFile(path);
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length < Magic.Length + 32 || stream.Length > MaximumBytes) throw new InvalidDataException("Project file size is invalid.");
                using (var reader = new BinaryReader(stream, Encoding.UTF8, true))
                {
                    if (!reader.ReadBytes(Magic.Length).SequenceEqual(Magic)) throw new InvalidDataException("Unknown project format.");
                    int size = checked((int)stream.Length - Magic.Length - 32);
                    byte[] bytes = ReadExact(reader, size), hash = ReadExact(reader, 32);
                    using (var sha = SHA256.Create()) if (!sha.ComputeHash(bytes).SequenceEqual(hash)) throw new InvalidDataException("Project checksum differs.");
                    if (expected != null && !bytes.SequenceEqual(expected)) throw new InvalidDataException("Prepared project changed before commit.");
                    return Decode(id, bytes);
                }
            }
        }

        private Record Decode(Guid expectedId, byte[] bytes)
        {
            try
            {
                using (var input = new MemoryStream(bytes, false))
                using (var reader = new BinaryReader(input))
                {
                    Guid id = new Guid(ReadExact(reader, 16)); long worldId = reader.ReadInt64(), revision = reader.ReadInt64();
                    Guid cancelled = new Guid(ReadExact(reader, 16)); Pose root = ReadPose(reader);
                    int size = reader.ReadInt32();
                    if (size < 1 || size > BlueprintExchangeFormat.MaximumBytes || size > input.Length - input.Position) throw new InvalidDataException("Invalid blueprint snapshot size.");
                    byte[] snapshot = ReadExact(reader, size);
                    var blueprint = BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(snapshot));
                    int count = reader.ReadInt32();
                    if (id != expectedId || worldId != world || revision != (cancelled == Guid.Empty ? 1 : 2) || count != blueprint.parts.Count || count < 1 || count > CompositeBlueprintStore.MaximumParts)
                        throw new InvalidDataException("Project identity/world/revision/parts differ.");
                    var parts = new Part[count]; var ids = new HashSet<Guid>();
                    for (int i = 0; i < count; ++i)
                    {
                        Guid partId = new Guid(ReadExact(reader, 16)); int index = reader.ReadInt32();
                        if (partId == Guid.Empty || !ids.Add(partId) || index != i) throw new InvalidDataException("Invalid project part identity.");
                        parts[i] = new Part(partId, index, ReadPose(reader));
                    }
                    if (input.Position != input.Length) throw new InvalidDataException("Unexpected project data; unpaid format accepts no payment records.");
                    return new Record(id, world, revision, cancelled, root, snapshot, parts);
                }
            }
            catch (Exception error) when (error is EndOfStreamException || error is DecoderFallbackException || error is ArgumentException)
            { throw new InvalidDataException("Incomplete or malformed project record.", error); }
        }

        private void Commit(Record record, bool replace)
        {
            string target = PathFor(record.Id), temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".preparing");
            byte[] bytes;
            using (var output = new MemoryStream())
            {
                using (var writer = new BinaryWriter(output, Encoding.UTF8, true))
                {
                    writer.Write(record.Id.ToByteArray()); writer.Write(record.World); writer.Write(record.Revision); writer.Write(record.CancelOperation.ToByteArray());
                    WritePose(writer, record.RootPose); byte[] snapshot = record.BlueprintBytes; writer.Write(snapshot.Length); writer.Write(snapshot);
                    writer.Write(record.Parts.Count);
                    foreach (Part part in record.Parts) { writer.Write(part.Id.ToByteArray()); writer.Write(part.SourceIndex); WritePose(writer, part.WorldPose); }
                }
                if (output.Length > MaximumBytes - Magic.Length - 32) throw new InvalidDataException("Project exceeds file limit.");
                bytes = output.ToArray();
            }
            Decode(record.Id, bytes);
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var sha = SHA256.Create())
                { stream.Write(Magic, 0, Magic.Length); stream.Write(bytes, 0, bytes.Length); byte[] hash = sha.ComputeHash(bytes); stream.Write(hash, 0, hash.Length); stream.Flush(true); }
                Read(record.Id, temporary, bytes);
                CheckFile(target); CompositeBlueprintStore.ValidateExchangeDirectory(directory);
                if (replace) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private string PathFor(Guid id)
        {
            if (id == Guid.Empty) throw new ArgumentException("Project ID is required.", nameof(id));
            CompositeBlueprintStore.ValidateExchangeDirectory(directory); return Path.Combine(directory, id.ToString("N") + ".bwproject");
        }
        internal FileStream Lock(Guid id)
        { string path = PathFor(id) + ".lock"; CheckFile(path); return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
        private static void CheckFile(string path)
        { if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new InvalidDataException("Project path cannot be a reparse point."); }
        private static bool SamePose(Pose a, Pose b) => a.Values.SequenceEqual(b.Values);
        private static Pose ReadPose(BinaryReader reader) => new Pose(Enumerable.Range(0, 10).Select(_ => reader.ReadSingle()).ToArray());
        private static void WritePose(BinaryWriter writer, Pose pose) { foreach (float value in pose.Values) writer.Write(value); }
        private static byte[] ReadExact(BinaryReader reader, int count)
        { byte[] bytes = reader.ReadBytes(count); if (bytes.Length != count) throw new EndOfStreamException(); return bytes; }
    }
}
