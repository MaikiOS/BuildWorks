using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OstrixMods.BuildWorks
{
    // Server-only storage. The caller supplies an authenticated connection identity;
    // neither identity nor the administrator resolver may come from a request payload.
    internal sealed class ServerBlueprintLibrary
    {
        internal enum Access { Players, Administrators }
        internal const int MaximumEntries = 512;
        internal const long MaximumLibraryBytes = 128L * 1024 * 1024;
        private const string Extension = ".bwserver.zip";
        private const int MaximumMetadataBytes = 4096;
        private readonly string directory;
        private readonly Func<string, bool> isAdministrator;
        private readonly long maximumBytes;
        private readonly object gate = new object();
        private Dictionary<string, Entry> entries;

        internal sealed class Info
        {
            internal readonly string Id, Name, Category, Publisher, Editor, DocumentSha256;
            internal readonly long Revision;
            internal readonly int PartCount, DocumentBytes;
            internal readonly Access Visibility;
            internal Info(Metadata meta, CompositeBlueprintStore.Blueprint document, int bytes)
            {
                Id = meta.id; Name = document.name; Category = document.category;
                Publisher = meta.publisher; Editor = meta.editor; DocumentSha256 = meta.sha256;
                Revision = meta.revision; PartCount = document.parts.Count; Visibility = (Access)meta.access; DocumentBytes = bytes;
            }
        }

        [DataContract]
        internal sealed class Metadata
        {
            [DataMember(IsRequired = true)] public int version;
            [DataMember(IsRequired = true)] public string id;
            [DataMember(IsRequired = true)] public long revision;
            [DataMember(IsRequired = true)] public int access;
            [DataMember(IsRequired = true)] public string publisher;
            [DataMember(IsRequired = true)] public string editor;
            [DataMember(IsRequired = true)] public string modifiedUtc;
            [DataMember(IsRequired = true)] public string sha256;
        }

        internal sealed class Entry
        {
            internal Info Info;
            internal byte[] Document;
            internal string FileHash;
            internal long StoredBytes, UnpackedBytes;
        }

        internal sealed class PreparedPublish : IDisposable
        {
            internal readonly ServerBlueprintLibrary Owner;
            internal readonly string Principal;
            internal readonly Entry Previous, Candidate;
            internal string Temporary;
            internal PreparedPublish(ServerBlueprintLibrary owner, string principal, Entry previous, Entry candidate, string temporary)
            { Owner = owner; Principal = principal; Previous = previous; Candidate = candidate; Temporary = temporary; }
            public void Dispose()
            {
                lock (Owner.gate)
                {
                    if (Temporary != null && File.Exists(Temporary)) File.Delete(Temporary);
                    Temporary = null;
                }
            }
        }

        internal sealed class PreparedRead
        {
            internal readonly ServerBlueprintLibrary Owner;
            internal readonly string Principal;
            internal readonly Entry Snapshot;
            internal byte[] Bytes;
            internal PreparedRead(ServerBlueprintLibrary owner, string principal, Entry snapshot, byte[] bytes)
            { Owner = owner; Principal = principal; Snapshot = snapshot; Bytes = bytes; }
        }

        // Initialize before accepting requests; archive reads never invoke native authority callbacks.
        internal Task InitializeAsync() => Task.Run(() => { lock (gate) Load(); });

        internal Task<PreparedRead> PrepareRead(string authenticatedPrincipal, string id, long revision, CancellationToken cancellation = default)
        {
            Entry snapshot;
            lock (gate)
            {
                bool admin = Administrator(authenticatedPrincipal); Load(); snapshot = Find(id, admin);
                if (snapshot.Info.Revision != revision) throw Invalid("conflict");
                cancellation.ThrowIfCancellationRequested();
            }
            return Task.Run(() =>
            {
                cancellation.ThrowIfCancellationRequested(); EnsureUnchanged(snapshot);
                var bytes = (byte[])snapshot.Document.Clone(); cancellation.ThrowIfCancellationRequested();
                return new PreparedRead(this, authenticatedPrincipal, snapshot, bytes);
            });
        }

        internal byte[] CompleteRead(string authenticatedPrincipal, PreparedRead prepared)
        {
            lock (gate)
            {
                bool admin = Administrator(authenticatedPrincipal);
                if (prepared == null || prepared.Owner != this || prepared.Principal != authenticatedPrincipal || prepared.Bytes == null)
                    throw Invalid("invalid");
                Entry current = Find(prepared.Snapshot.Info.Id, admin);
                if (!ReferenceEquals(current, prepared.Snapshot)) throw Invalid("conflict");
                byte[] result = prepared.Bytes; prepared.Bytes = null;
                return result;
            }
        }

        internal ServerBlueprintLibrary(string directory, Func<string, bool> isAdministrator,
            long maximumBytes = MaximumLibraryBytes)
        {
            if (isAdministrator == null) throw new ArgumentNullException(nameof(isAdministrator));
            if (maximumBytes < 1 || maximumBytes > MaximumLibraryBytes) throw Invalid("capacity");
            this.directory = CompositeBlueprintStore.ValidateExchangeDirectory(directory);
            this.isAdministrator = isAdministrator;
            this.maximumBytes = maximumBytes;
        }

        internal IReadOnlyList<Info> List(string authenticatedPrincipal)
        {
            lock (gate)
            {
                bool admin = Administrator(authenticatedPrincipal);
                Load();
                return entries.Values.Where(entry => admin || entry.Info.Visibility == Access.Players)
                    .Select(entry => entry.Info).OrderBy(info => info.Name, StringComparer.Ordinal)
                    .ThenBy(info => info.Id, StringComparer.Ordinal).ToArray();
            }
        }

        internal byte[] Read(string authenticatedPrincipal, string id, long revision)
        {
            lock (gate)
            {
                bool admin = Administrator(authenticatedPrincipal);
                Load();
                Entry entry = Find(id, admin);
                if (entry.Info.Revision != revision) throw Invalid("conflict");
                EnsureUnchanged(entry);
                return (byte[])entry.Document.Clone();
            }
        }

        internal Info Describe(string authenticatedPrincipal, string id, long revision)
        {
            lock (gate)
            {
                bool admin = Administrator(authenticatedPrincipal); Load(); Entry entry = Find(id, admin);
                if (entry.Info.Revision != revision) throw Invalid("conflict");
                return entry.Info;
            }
        }

        internal Info Publish(string authenticatedPrincipal, byte[] portableDocument, Access access,
            string id = null, long expectedRevision = 0)
        {
            using (var prepared = PreparePublish(authenticatedPrincipal, portableDocument, access, id, expectedRevision).GetAwaiter().GetResult())
                return CommitPublish(authenticatedPrincipal, prepared);
        }

        internal Task<PreparedPublish> PreparePublish(string authenticatedPrincipal, byte[] portableDocument, Access access,
            string id = null, long expectedRevision = 0, CancellationToken cancellation = default)
        {
            Entry previous;
            byte[] documentBytes;
            lock (gate)
            {
                RequireAdministrator(authenticatedPrincipal);
                if (access != Access.Players && access != Access.Administrators || expectedRevision < 0) throw Invalid("invalid");
                Load(); previous = null;
                if (id != null)
                {
                    previous = Find(id, true);
                    if (previous.Info.Revision != expectedRevision || expectedRevision == long.MaxValue) throw Invalid("conflict");
                }
                else if (expectedRevision != 0) throw Invalid("conflict");
                if (portableDocument == null || portableDocument.Length > BlueprintExchangeFormat.MaximumBytes) throw Invalid("invalid");
                cancellation.ThrowIfCancellationRequested();
                documentBytes = (byte[])portableDocument.Clone();
            }
            return Task.Run(() =>
            {
                string temporary = null;
                try
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (previous != null) EnsureUnchanged(previous);
                    var document = BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(documentBytes));
                    var meta = new Metadata
                    {
                        version = 1,
                        id = id ?? Guid.NewGuid().ToString("N"),
                        revision = checked(expectedRevision + 1),
                        access = (int)access,
                        publisher = previous?.Info.Publisher ?? authenticatedPrincipal,
                        editor = authenticatedPrincipal,
                        modifiedUtc = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                        sha256 = Hash(documentBytes)
                    };
                    byte[] metadataBytes;
                    using (var memory = new MemoryStream())
                    {
                        new DataContractJsonSerializer(typeof(Metadata)).WriteObject(memory, meta);
                        metadataBytes = memory.ToArray();
                    }
                    lock (gate) CheckCapacity(previous, checked(metadataBytes.LongLength + documentBytes.LongLength), 0, document.parts.Count);
                    cancellation.ThrowIfCancellationRequested();
                    CompositeBlueprintStore.ValidateExchangeDirectory(directory); Directory.CreateDirectory(directory);
                    temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp.zip");
                    using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    using (var archive = new ZipArchive(output, ZipArchiveMode.Create))
                    {
                        Write("access.json", metadataBytes); Write("blueprint.buildworks.json", documentBytes);
                        void Write(string name, byte[] bytes)
                        {
                            using (var stream = archive.CreateEntry(name).Open())
                                for (int offset = 0; offset < bytes.Length; offset += 8192)
                                {
                                    cancellation.ThrowIfCancellationRequested();
                                    stream.Write(bytes, offset, Math.Min(8192, bytes.Length - offset));
                                }
                        }
                    }
                    cancellation.ThrowIfCancellationRequested();
                    Entry candidate = ReadEntry(temporary, meta.id);
                    lock (gate) CheckCapacity(previous, candidate.UnpackedBytes, candidate.StoredBytes, candidate.Info.PartCount);
                    cancellation.ThrowIfCancellationRequested();
                    var prepared = new PreparedPublish(this, authenticatedPrincipal, previous, candidate, temporary);
                    temporary = null; // The prepared operation now owns staging until commit/disposal.
                    return prepared;
                }
                finally { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
            });
        }

        // Host thread: current native permission callback runs here, never in PreparePublish's worker.
        internal Info CommitPublish(string authenticatedPrincipal, PreparedPublish prepared, CancellationToken cancellation = default)
        {
            lock (gate)
            {
                RequireAdministrator(authenticatedPrincipal);
                if (prepared == null || prepared.Owner != this || prepared.Principal != authenticatedPrincipal || prepared.Temporary == null)
                    throw Invalid("invalid");
                Entry previous = prepared.Previous, candidate = prepared.Candidate;
                if (previous != null && !ReferenceEquals(Find(previous.Info.Id, true), previous)) throw Invalid("conflict");
                if (previous == null && entries.ContainsKey(candidate.Info.Id)) throw Invalid("conflict");
                CheckCapacity(previous, candidate.UnpackedBytes, candidate.StoredBytes, candidate.Info.PartCount);
                if (previous != null) EnsureUnchanged(previous);
                RejectReparseFile(prepared.Temporary);
                if (Hash(ReadArchiveBytes(prepared.Temporary)) != candidate.FileHash) throw Invalid("changed");
                cancellation.ThrowIfCancellationRequested();
                RequireAdministrator(authenticatedPrincipal);
                CompositeBlueprintStore.ValidateExchangeDirectory(directory);
                string target = Path.Combine(directory, candidate.Info.Id + Extension);
                if (previous != null) File.Replace(prepared.Temporary, target, null);
                else File.Move(prepared.Temporary, target);
                entries[candidate.Info.Id] = candidate;
                prepared.Temporary = null;
                return candidate.Info;
            }
        }

        internal void Delete(string authenticatedPrincipal, string id, long expectedRevision)
        {
            lock (gate)
            {
                RequireAdministrator(authenticatedPrincipal);
                Load();
                Entry entry = Find(id, true);
                if (entry.Info.Revision != expectedRevision) throw Invalid("conflict");
                EnsureUnchanged(entry);
                RequireAdministrator(authenticatedPrincipal);
                File.Delete(Path.Combine(directory, id + Extension));
                entries.Remove(id);
            }
        }

        private bool Administrator(string principal)
        {
            if (!ValidPrincipal(principal)) throw new UnauthorizedAccessException("$buildworks_server.unauthenticated");
            return isAdministrator(principal);
        }
        private void RequireAdministrator(string principal)
        { if (!Administrator(principal)) throw new UnauthorizedAccessException("$buildworks_server.admin_required"); }
        private static bool ValidPrincipal(string principal) => !string.IsNullOrWhiteSpace(principal) &&
            principal.Length <= 256 && !principal.Any(char.IsControl) &&
            new UTF8Encoding(false, true).GetByteCount(principal) <= 1024;
        private static bool ValidId(string id) => id != null && Guid.TryParseExact(id, "N", out Guid guid) &&
            guid != Guid.Empty && guid.ToString("N") == id;
        private Entry Find(string id, bool admin)
        {
            if (!ValidId(id) || !entries.TryGetValue(id, out var entry) || !admin && entry.Info.Visibility != Access.Players)
                throw Invalid("unavailable"); // Missing and forbidden IDs have the same public result.
            return entry;
        }
        private void Load()
        {
            if (entries != null) return;
            var loaded = new Dictionary<string, Entry>(StringComparer.Ordinal);
            long stored = 0, unpacked = 0; int parts = 0;
            CompositeBlueprintStore.ValidateExchangeDirectory(directory);
            if (Directory.Exists(directory))
                foreach (string path in Directory.EnumerateFiles(directory, "*" + Extension).OrderBy(value => value, StringComparer.Ordinal))
                {
                    if (loaded.Count >= MaximumEntries) throw Invalid("capacity");
                    string name = Path.GetFileName(path);
                    string id = name.Substring(0, name.Length - Extension.Length);
                    if (new FileInfo(path).Length > maximumBytes - stored) throw Invalid("capacity");
                    Entry entry = ReadEntry(path, id, maximumBytes - unpacked);
                    stored = checked(stored + entry.StoredBytes); unpacked = checked(unpacked + entry.UnpackedBytes);
                    parts = checked(parts + entry.Info.PartCount);
                    if (stored > maximumBytes || unpacked > maximumBytes || parts > 100000) throw Invalid("capacity");
                    loaded.Add(id, entry);
                }
            entries = loaded; // A corrupt entry cannot leave a partly loaded/partly public cache.
        }
        private void CheckCapacity(Entry previous, long unpacked, long stored, int parts)
        {
            if (entries.Count + (previous == null ? 1 : 0) > MaximumEntries ||
                entries.Values.Sum(entry => entry.UnpackedBytes) - (previous?.UnpackedBytes ?? 0) + unpacked > maximumBytes ||
                entries.Values.Sum(entry => entry.StoredBytes) - (previous?.StoredBytes ?? 0) + stored > maximumBytes ||
                entries.Values.Sum(entry => entry.Info.PartCount) - (previous?.Info.PartCount ?? 0) + parts > 100000)
                throw Invalid("capacity");
        }
        private void EnsureUnchanged(Entry entry)
        {
            string path = Path.Combine(directory, entry.Info.Id + Extension);
            RejectReparseFile(path);
            var file = new FileInfo(path);
            if (!file.Exists || file.Length != entry.StoredBytes || file.Length > BlueprintExchangeFormat.MaximumBytes + 65536 ||
                Hash(ReadArchiveBytes(path)) != entry.FileHash) throw Invalid("changed");
        }
        private static Entry ReadEntry(string path, string id, long budget = MaximumLibraryBytes)
        {
            if (!ValidId(id)) throw Invalid("invalid");
            RejectReparseFile(path);
            byte[] stored = ReadArchiveBytes(path);
            using (var memory = new MemoryStream(stored, false))
            using (var archive = new ZipArchive(memory, ZipArchiveMode.Read))
            {
                if (archive.Entries.Count != 2 || archive.Entries.Count(entry => entry.FullName == "access.json") != 1 ||
                    archive.Entries.Count(entry => entry.FullName == "blueprint.buildworks.json") != 1) throw Invalid("invalid");
                byte[] access = ReadBounded(archive.GetEntry("access.json"), (int)Math.Min(MaximumMetadataBytes, budget));
                Metadata meta;
                using (var input = new MemoryStream(access, false))
                    meta = (Metadata)new DataContractJsonSerializer(typeof(Metadata)).ReadObject(input);
                if (meta == null || meta.version != 1 || meta.id != id || meta.revision < 1 ||
                    meta.access < 0 || meta.access > 1 || !ValidPrincipal(meta.publisher) || !ValidPrincipal(meta.editor) ||
                    !DateTime.TryParseExact(meta.modifiedUtc, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime timestamp) ||
                    timestamp.Kind != DateTimeKind.Utc) throw Invalid("invalid");
                byte[] bytes = ReadBounded(archive.GetEntry("blueprint.buildworks.json"),
                    (int)Math.Min(BlueprintExchangeFormat.MaximumBytes, budget - access.LongLength));
                if (Hash(bytes) != meta.sha256) throw Invalid("changed");
                var document = BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(bytes));
                return new Entry
                {
                    Info = new Info(meta, document, bytes.Length),
                    Document = bytes,
                    FileHash = Hash(stored),
                    StoredBytes = stored.LongLength,
                    UnpackedBytes = access.LongLength + bytes.LongLength
                };
            }
        }
        private static byte[] ReadArchiveBytes(string path)
        {
            using (var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (input.Length > BlueprintExchangeFormat.MaximumBytes + 65536) throw Invalid("capacity");
                var bytes = new byte[checked((int)input.Length)]; int offset = 0, count;
                while (offset < bytes.Length && (count = input.Read(bytes, offset, bytes.Length - offset)) > 0) offset += count;
                if (offset != bytes.Length) throw Invalid("invalid");
                return bytes;
            }
        }
        private static byte[] ReadBounded(ZipArchiveEntry entry, int maximum)
        {
            if (entry.Length < 0 || entry.Length > maximum) throw Invalid("capacity");
            using (var input = entry.Open())
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192]; int count;
                while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
                { if (output.Length + count > maximum) throw Invalid("capacity"); output.Write(buffer, 0, count); }
                if (output.Length != entry.Length) throw Invalid("invalid");
                return output.ToArray();
            }
        }
        private static void RejectReparseFile(string path)
        {
            CompositeBlueprintStore.ValidateExchangeDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw Invalid("invalid");
        }
        private static string Hash(byte[] bytes)
        { using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", ""); }
        private static InvalidDataException Invalid(string key) => new InvalidDataException("$buildworks_server." + key);
    }
}
