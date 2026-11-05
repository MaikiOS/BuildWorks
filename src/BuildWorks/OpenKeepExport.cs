using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed class OpenKeepExport
    {
        internal enum Consumer { OpenKeep, DevBridge }
        internal enum BlockReason { MultipleSites, Rotation, Scale, PrefabScaleUnknown, DevBridgeSite, DevBridgeExtra, DevBridgeRelief, DevBridgePrefab }
        internal readonly List<(BlockReason Reason, string Detail)> Blockers = new List<(BlockReason, string)>();
        internal Consumer Target { get; private set; }
        internal int Parts, Sources, OtherSources, Signs, State, MaterialSeeds, Groups, Supports, Anchors, Metadata;
        internal bool OriginalSite, DerivedSite;
        private byte[] native, json;
        private BlueprintExportPair files;

        internal void CancelWrite() => files?.CancelWrite();

        internal static OpenKeepExport Review(CompositeBlueprintStore.Blueprint source, Consumer target,
            Func<string, CompositeBlueprintStore.VectorData?> resolveScale)
        {
            if (!Enum.IsDefined(typeof(Consumer), target)) throw new ArgumentOutOfRangeException(nameof(target));
            var snapshot = BlueprintExportPair.Snapshot(source);
            var document = snapshot.Document;
            var sources = document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>();
            var roots = sources.Where(s => s.format == OpenKeepBlueprint.Format).ToArray();
            var review = new OpenKeepExport { Target = target, native = snapshot.Native, Parts = document.parts.Count,
                Sources = sources.Count, OtherSources = sources.Count - roots.Length,
                Signs = document.parts.Count(p => p.signText != null), State = document.parts.Count(p => p.foreignData != null),
                MaterialSeeds = document.parts.Count(p => p.materialSeed.HasValue), Groups = document.groups.Count,
                Supports = document.supports?.Count ?? 0, Anchors = document.anchors.Count,
                Metadata = document.parts.Count(p => p.metadata != null) };
            if (roots.Length > 1)
            { review.Blockers.Add((BlockReason.MultipleSites, roots.Length.ToString(CultureInfo.InvariantCulture))); return review; }
            var evidence = roots.SingleOrDefault()?.ToGeometry();
            OpenKeepBlueprint model = evidence?.OpenKeep;
            var extraPaths = target == Consumer.DevBridge && model != null
                ? new HashSet<string>(model.UnknownPaths, StringComparer.Ordinal) : null;
            var tuples = new List<(string Prefab, Point3 Position, double Yaw, int Record)>();
            var scales = new Dictionary<string, CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
            foreach (var part in document.parts)
            {
                // The original DevBridge script inserts this ID into a C# string without escaping it.
                if (target == Consumer.DevBridge && part.prefabName.IndexOfAny(new[] { '"', '\u2028', '\u2029' }) >= 0)
                    review.Blockers.Add((BlockReason.DevBridgePrefab, part.prefabName));
                var rotation = new Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w).Normalized();
                if (rotation.X != 0 || rotation.Z != 0) review.Blockers.Add((BlockReason.Rotation, part.stableId));
                bool unitRelative = !part.scaleIsAbsolute && !part.baseScale.HasValue &&
                    part.scale.x == 1 && part.scale.y == 1 && part.scale.z == 1;
                if (!unitRelative)
                {
                    if (!scales.TryGetValue(part.prefabName, out var basis))
                        scales.Add(part.prefabName, basis = resolveScale?.Invoke(part.prefabName));
                    if (!basis.HasValue || !CompositeBlueprintStore.PositiveScale(basis.Value))
                        review.Blockers.Add((BlockReason.PrefabScaleUnknown, part.prefabName));
                    else
                    {
                        var actual = part.RootScale(basis.Value.ToVector3());
                        if (actual.x != basis.Value.x || actual.y != basis.Value.y || actual.z != basis.Value.z)
                            review.Blockers.Add((BlockReason.Scale, part.stableId));
                    }
                }
                int record = part.sourceDocumentId == evidence?.Id ? part.sourceRecord : 0;
                if (record > 0 && extraPaths?.Contains("/pieces/" + (record - 1) + "/5+") == true)
                    review.Blockers.Add((BlockReason.DevBridgeExtra, part.stableId));
                double yaw = (Math.Atan2(rotation.Y, rotation.W) * 360 / Math.PI + 360) % 360;
                tuples.Add((part.prefabName, new Point3(part.position.x, part.position.y, part.position.z), yaw, record));
            }
            if (model == null)
            {
                string site = "";
                if (target == Consumer.DevBridge)
                {
                    double half = tuples.Max(p => Math.Max(Math.Abs(p.Position.X), Math.Abs(p.Position.Z))) + .5;
                    double reach = Math.Ceiling(tuples.Max(p => Math.Sqrt(p.Position.X * p.Position.X + p.Position.Z * p.Position.Z)) + 1.5);
                    double stand = tuples.Min(p => p.Position.Z) - 3;
                    site = ",\"site\":{\"level_half\":" + Number(half) + ",\"clear_radius\":" + Number(reach) + ",\"stand\":[0," + Number(stand) + "]}";
                }
                model = OpenKeepBlueprint.Parse("{\"name\":" + Quote(document.name) + ",\"description\":\"BuildWorks export; full data is in the adjacent native document\",\"source\":\"BuildWorks\",\"pieces\":[]" + site + "}", document.name);
                review.DerivedSite = true;
            }
            else { review.OriginalSite = model.HasSite; review.DerivedSite = !model.HasSite; }
            if (target == Consumer.DevBridge && (!model.ClearRadius.HasValue || !model.Stand.HasValue ||
                !(model.LevelHalf.HasValue || model.Terrain.Count > 0)))
                review.Blockers.Add((BlockReason.DevBridgeSite, evidence?.Name ?? document.name));
            if (target == Consumer.DevBridge && model.Relief != null)
                review.Blockers.Add((BlockReason.DevBridgeRelief, evidence?.Name ?? document.name));
            if (review.Blockers.Count > 0) return review;
            string text = model.WritePieces(tuples, document.name);
            review.json = new UTF8Encoding(false, true).GetBytes(text);
            review.files = new BlueprintExportPair(review.native, review.json, ".json");
            return review;
        }

        internal bool TryWrite(CompositeBlueprintStore.Blueprint source, Func<string, CompositeBlueprintStore.VectorData?> resolveScale,
            string directory, string name, out string result, out string error)
        {
            result = null; error = null;
            if (files == null || Blockers.Count > 0) { error = "$buildworks_import.transform"; return false; }
            return files.TryWrite(() =>
            {
                var current = Review(source, Target, resolveScale);
                if (current.Blockers.Count > 0) throw new InvalidDataException("$buildworks_exchange.plan_changed");
                return (current.native, current.json);
            }, directory, name, out result, out error);
        }
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static string Quote(string value)
        {
            using (var stream = new MemoryStream())
            { new DataContractJsonSerializer(typeof(string)).WriteObject(stream, value); return Encoding.UTF8.GetString(stream.ToArray()); }
        }
    }
}
