using System;
using System.Collections.Generic;
using System.Linq;

namespace OstrixMods.BuildWorks.Geometry
{
    // Passive provenance of an accepted personal copy; never a permission or signature.
    public sealed class BlueprintPeerOrigin
    {
        public const int MaximumCount = 16;
        public const string LimitError = "Maximum peer origin count reached.";
        public const string ConflictError = "Conflicting peer origin.";
        public int Version { get; }
        public string Server { get; }
        public long WorldUid { get; }
        public string Sender { get; }
        public string Recipient { get; }
        public string OfferId { get; }
        public string DocumentId { get; }
        public string Sha256 { get; }
        public int FormatVersion { get; }

        public BlueprintPeerOrigin(string server, long worldUid, string sender, string recipient, string offerId,
            string documentId, string sha256, int formatVersion, int version = 1)
        {
            if (version != 1 || worldUid == 0 || !Label(server) || !Label(sender) || !Label(recipient) ||
                !Identity(offerId) || !Identity(documentId) || sha256 == null || sha256.Length != 64 ||
                sha256.Any(c => !Uri.IsHexDigit(c)) || sha256 != sha256.ToUpperInvariant() || formatVersion < 1)
                throw new ArgumentException("Invalid peer origin.");
            Version = version; Server = server; WorldUid = worldUid; Sender = sender; Recipient = recipient;
            OfferId = offerId; DocumentId = documentId; Sha256 = sha256; FormatVersion = formatVersion;
        }
        private static bool Identity(string value) => Guid.TryParseExact(value, "N", out Guid id) && id != Guid.Empty && value == id.ToString("N");
        private static bool Label(string value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 256 && value == value.Trim() &&
            !value.Any(c => char.IsControl(c) || c == '<' || c == '>');
        public bool SameKey(BlueprintPeerOrigin other) => other != null && Server == other.Server && WorldUid == other.WorldUid && OfferId == other.OfferId;
        public bool SameValue(BlueprintPeerOrigin other) => SameKey(other) && Sender == other.Sender && Recipient == other.Recipient &&
            DocumentId == other.DocumentId && Sha256 == other.Sha256 && FormatVersion == other.FormatVersion && Version == other.Version;

        internal static void Validate(IReadOnlyList<BlueprintPeerOrigin> origins)
        {
            if (origins.Count > MaximumCount) throw new InvalidOperationException(LimitError);
            for (int i = 0; i < origins.Count; ++i)
            {
                if (origins[i] == null) throw new ArgumentException("Null peer origin.");
                for (int j = 0; j < i; ++j)
                    if (origins[i].SameKey(origins[j])) throw new ArgumentException("Duplicate peer origin.");
            }
        }
        internal static List<BlueprintPeerOrigin> Merge(IReadOnlyList<BlueprintPeerOrigin> destination, IReadOnlyList<BlueprintPeerOrigin> source)
        {
            var result = new List<BlueprintPeerOrigin>(destination);
            foreach (var origin in source)
            {
                var existing = result.Find(item => item.SameKey(origin));
                if (existing != null)
                { if (!existing.SameValue(origin)) throw new InvalidOperationException(ConflictError); }
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
