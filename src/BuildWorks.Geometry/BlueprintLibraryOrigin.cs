using System;
using System.Collections.Generic;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Informational document provenance, never an authorization or a per-object signature.
    public sealed class BlueprintLibraryOrigin
    {
        public const int MaximumCount = 16;
        public const string LimitError = "Maximum library origin count reached.";
        public const string ConflictError = "Conflicting library origin.";
        public int Version { get; }
        public string Server { get; }
        public long WorldUid { get; }
        public string DocumentId { get; }
        public long Revision { get; }
        public int Access { get; }
        public string Publisher { get; }
        public string Sha256 { get; }
        public int FormatVersion { get; }

        public BlueprintLibraryOrigin(string server, long worldUid, string documentId, long revision,
            int access, string publisher, string sha256, int formatVersion, int version = 1)
        {
            if (version != 1 || worldUid == 0 || revision < 1 || access < 0 || access > 1 || formatVersion < 1 ||
                !Guid.TryParseExact(documentId, "N", out Guid id) || documentId != id.ToString("N") ||
                sha256 == null || sha256.Length != 64 || sha256.Any(c => !Uri.IsHexDigit(c)) || sha256 != sha256.ToUpperInvariant() ||
                !Label(server) || !Label(publisher)) throw new ArgumentException("Invalid library origin.");
            Version = version; Server = server; WorldUid = worldUid; DocumentId = documentId;
            Revision = revision; Access = access; Publisher = publisher; Sha256 = sha256; FormatVersion = formatVersion;
        }
        private static bool Label(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && value == value.Trim() &&
            !value.Any(c => char.IsControl(c) || c == '<' || c == '>');
        public bool SameKey(BlueprintLibraryOrigin other) => other != null && Server == other.Server && WorldUid == other.WorldUid &&
            DocumentId == other.DocumentId && Revision == other.Revision && Sha256 == other.Sha256;
        public bool SameValue(BlueprintLibraryOrigin other) => SameKey(other) && Version == other.Version && Access == other.Access &&
            Publisher == other.Publisher && FormatVersion == other.FormatVersion;

        internal static void Validate(IReadOnlyList<BlueprintLibraryOrigin> origins)
        {
            if (origins.Count > MaximumCount) throw new InvalidOperationException(LimitError);
            for (int i = 0; i < origins.Count; ++i)
            {
                if (origins[i] == null) throw new ArgumentException("Null library origin.");
                for (int j = 0; j < i; ++j)
                    if (origins[i].SameKey(origins[j])) throw new ArgumentException(ConflictError);
            }
        }
        internal static List<BlueprintLibraryOrigin> Merge(IReadOnlyList<BlueprintLibraryOrigin> destination, IReadOnlyList<BlueprintLibraryOrigin> source)
        {
            var result = new List<BlueprintLibraryOrigin>(destination);
            foreach (var origin in source)
            {
                var existing = result.Find(item => item.SameKey(origin));
                if (existing != null)
                {
                    if (!existing.SameValue(origin)) throw new InvalidOperationException(ConflictError);
                }
                else
                {
                    if (result.Count == MaximumCount) throw new InvalidOperationException(LimitError);
                    result.Add(origin);
                }
            }
            return result;
        }
    }
}
