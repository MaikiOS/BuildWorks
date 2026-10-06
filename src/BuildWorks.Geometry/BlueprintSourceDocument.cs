using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks.Geometry
{
    // Immutable source evidence, never executable game state. UTF-8 text retains BOM and line endings.
    public sealed class BlueprintSourceDocument
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        public const int MaximumSources = 64;
        public const int MaximumTotalBytes = 24 * 1024 * 1024;
        private readonly Dictionary<int, string> partRows = new Dictionary<int, string>();

        public BlueprintSourceDocument(string format, string name, string text)
        {
            if (format != "blueprint" && format != "vbuild") throw new ArgumentException("Unknown source format.", nameof(format));
            if (string.IsNullOrWhiteSpace(name) || name.Length > 256 || name != name.Trim())
                throw new ArgumentException("Invalid source name.", nameof(name));
            foreach (char c in name)
                if (char.IsControl(c) || c == '/' || c == '\\' || c == ':' || c == '<' || c == '>')
                    throw new ArgumentException("Source name must not be a path.", nameof(name));
            if (text == null || text.Length > MaximumBytes) throw new ArgumentException("Source is too large.", nameof(text));
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            if (bytes.Length > MaximumBytes) throw new ArgumentException("Source is too large.", nameof(text));
            Format = format; Name = name; Text = text; ByteCount = bytes.Length;
            using (var hash = SHA256.Create())
                Id = "source_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(format + "\n" + name + "\n" + text)))
                    .Replace("-", "").ToLowerInvariant();
            bool pieces = format == "vbuild";
            int line = 1;
            for (int start = 0; start < text.Length; ++line)
            {
                int end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n') ++end;
                int first = start, last = end;
                if (first == 0) while (first < last && text[first] == '\uFEFF') ++first;
                while (first < last && char.IsWhiteSpace(text[first])) ++first;
                while (last > first && char.IsWhiteSpace(text[last - 1])) --last;
                if (first < last && text[first] == '#')
                {
                    if (last - first == 7 && string.CompareOrdinal(text, first, "#Pieces", 0, 7) == 0) pieces = true;
                    else if (!HeaderAt(text, first, last, "#Name:") && !HeaderAt(text, first, last, "#Category:") &&
                        !HeaderAt(text, first, last, "#Creator:") && !HeaderAt(text, first, last, "#Description:")) pieces = false;
                }
                else if (pieces && first < last)
                {
                    int separator = text.IndexOf(';', first, last - first);
                    if (separator < 0 && format == "vbuild")
                        for (int i = first; i < last; ++i)
                            if (char.IsWhiteSpace(text[i])) { separator = i; break; }
                    if (separator > first)
                    {
                        while (separator > first && char.IsWhiteSpace(text[separator - 1])) --separator;
                        if (partRows.Count >= BlueprintEditorDocument.MaximumParts)
                            throw new ArgumentException("Too many source parts.", nameof(text));
                        partRows.Add(line, text.Substring(first, separator - first));
                    }
                }
                start = end;
                if (start < text.Length && text[start++] == '\r' && start < text.Length && text[start] == '\n') ++start;
            }
        }

        private static bool HeaderAt(string text, int first, int last, string header) =>
            last - first >= header.Length && string.CompareOrdinal(text, first, header, 0, header.Length) == 0;

        public string Id { get; }
        public string Format { get; }
        public string Name { get; }
        public string Text { get; }
        public int ByteCount { get; }

        public bool HasPartAt(int line, string prefab)
        {
            return partRows.TryGetValue(line, out string sourcePrefab) && sourcePrefab == prefab;
        }

        public static Dictionary<string, BlueprintSourceDocument> Index(IEnumerable<BlueprintSourceDocument> sources)
        {
            var index = new Dictionary<string, BlueprintSourceDocument>(StringComparer.Ordinal);
            int total = 0;
            if (sources == null) return index;
            foreach (BlueprintSourceDocument source in sources)
            {
                if (source == null || index.Count >= MaximumSources || index.ContainsKey(source.Id))
                    throw new ArgumentException("Invalid or duplicate source document.");
                total += source.ByteCount;
                if (total > MaximumTotalBytes) throw new ArgumentException("Source documents are too large.");
                index.Add(source.Id, source);
            }
            return index;
        }
    }
}
