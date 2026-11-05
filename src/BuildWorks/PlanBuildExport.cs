using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;

namespace OstrixMods.BuildWorks
{
    // Contract: PlanBuild e12ed86ff2498bbd4a9c0f5d81cb388e3aaeb549, thirteen-field PieceEntry.
    internal sealed class PlanBuildExport
    {
        private readonly byte[] native;
        private byte[] plan;
        private BlueprintExportPair files;
        internal readonly SortedDictionary<string, int> OmittedSigns = new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal readonly SortedDictionary<string, int> OmittedState = new SortedDictionary<string, int>(StringComparer.Ordinal);
        internal int Parts, Groups, Supports, Anchors, Metadata, Sources, MaterialSeeds, CustomSnaps, LibraryOrigins, PeerOrigins;
        internal int IgnoredSnapPointRows;
        internal int PrefabMigrations;

        private PlanBuildExport(byte[] nativeBytes, byte[] planBytes)
        { native = nativeBytes; plan = planBytes; }

        internal void CancelWrite() => files.CancelWrite();

        internal static PlanBuildExport Review(CompositeBlueprintStore.Blueprint source,
            Func<string, CompositeBlueprintStore.VectorData?> resolveScale)
        {
            var snapshot = BlueprintExportPair.Snapshot(source);
            byte[] backup = snapshot.Native;
            var document = snapshot.Document;
            var output = new StringBuilder();
            output.Append("#Name:").Append(document.name).Append("\n#Creator:").Append(Creators(document)).Append("\n#Description:")
                .Append(JsonString("Exported with BuildWorks; original source credits, terms and full construction data are in the adjacent .buildworks.json."))
                .Append("\n#Category:").Append(document.category).Append("\n#Pieces\n");
            var scales = new Dictionary<string, CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
            var review = new PlanBuildExport(backup, null)
            {
                Parts = document.parts.Count,
                Groups = document.groups.Count,
                Supports = document.supports?.Count ?? 0,
                Anchors = document.anchors.Count,
                Metadata = document.parts.Count(part => part.metadata != null),
                Sources = document.sourceDocuments?.Count ?? 0,
                LibraryOrigins = document.libraryOrigins?.Count ?? 0,
                PeerOrigins = document.peerOrigins?.Count ?? 0,
                PrefabMigrations = document.parts.Count(part => part.originalPrefabName != null),
                MaterialSeeds = document.parts.Count(part => part.materialSeed.HasValue),
                CustomSnaps = document.customSnapPoints?.Count ?? 0,
                IgnoredSnapPointRows = document.sourceDocuments?.Sum(sourceDocument => sourceDocument.ToGeometry().IgnoredSnapPointRows) ?? 0
            };
            foreach (var part in document.parts)
            {
                if (part.prefabName.StartsWith("#", StringComparison.Ordinal) || part.prefabName.IndexOfAny(new[] { ';', ',', '(' }) >= 0)
                    throw new InvalidDataException("$buildworks_exchange.plan_identifier");
                var basis = part.baseScale;
                if (!part.scaleIsAbsolute && !basis.HasValue)
                {
                    if (!scales.TryGetValue(part.prefabName, out basis))
                        scales.Add(part.prefabName, basis = resolveScale?.Invoke(part.prefabName));
                    if (!basis.HasValue || !CompositeBlueprintStore.PositiveScale(basis.Value))
                        throw new InvalidDataException("$buildworks_import.scale_pending");
                }
                var scale = part.RootScale((basis ?? new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }).ToVector3());
                string info = "\"\"";
                if (part.signText != null)
                {
                    // PlanBuild replaces raw commas, strips decoded semicolons, and ignores empty SetText.
                    if (part.prefabName == "sign" && part.signText.Length > 0 && part.signText.IndexOfAny(new[] { ',', ';' }) < 0)
                        info = JsonString(part.signText);
                    else Add(review.OmittedSigns, part.prefabName + " / " + part.stableId);
                }
                if (part.foreignData != null)
                    Add(review.OmittedState, part.prefabName + " / " + part.foreignData.format);
                output.Append(part.prefabName).Append(";Building;")
                    .Append(Number(part.position.x)).Append(';').Append(Number(part.position.y)).Append(';').Append(Number(part.position.z)).Append(';')
                    .Append(Number(part.rotation.x)).Append(';').Append(Number(part.rotation.y)).Append(';').Append(Number(part.rotation.z)).Append(';')
                    .Append(Number(part.rotation.w)).Append(';').Append(info).Append(';')
                    .Append(Number(scale.x)).Append(';').Append(Number(scale.y)).Append(';').Append(Number(scale.z)).Append('\n');
                if (output.Length > BlueprintExchangeFormat.MaximumBytes)
                    throw new InvalidDataException("$buildworks_import.file_limit");
            }
            output.Append("#SnapPoints\n");
            foreach (var point in document.customSnapPoints ?? new List<CompositeBlueprintStore.VectorData>())
                output.Append(Number(point.x)).Append(';').Append(Number(point.y)).Append(';').Append(Number(point.z)).Append('\n');
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(output.ToString());
            if (bytes.Length > BlueprintExchangeFormat.MaximumBytes) throw new InvalidDataException("$buildworks_import.file_limit");
            review.plan = bytes;
            review.files = new BlueprintExportPair(backup, bytes, ".blueprint");
            return review;
        }

        internal bool TryWrite(CompositeBlueprintStore.Blueprint source,
            Func<string, CompositeBlueprintStore.VectorData?> resolveScale, string directory, string name,
            out string result, out string error) => files.TryWrite(() =>
            {
                var current = Review(source, resolveScale);
                return (current.native, current.plan);
            }, directory, name, out result, out error);

        private static void Add(IDictionary<string, int> counts, string value)
        { counts.TryGetValue(value, out int count); counts[value] = count + 1; }
        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Creators(CompositeBlueprintStore.Blueprint document)
        {
            var names = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var source in document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
            {
                if (source.format != "blueprint") continue;
                using (var reader = new StringReader(source.text))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        line = line.Trim().TrimStart('\uFEFF');
                        if (line == "#Pieces") break;
                        if (!line.StartsWith("#Creator:", StringComparison.Ordinal)) continue;
                        string name = line.Substring(9).Trim();
                        if (name.Length > 0 && !name.Any(char.IsControl)) names.Add(name);
                    }
                }
            }
            return names.Count > 0 ? string.Join(" | ", names) : "Unspecified (BuildWorks export)";
        }
        private static string JsonString(string value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(string)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
    }
}
