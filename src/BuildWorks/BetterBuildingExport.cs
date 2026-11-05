using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;
using Bbp = OstrixMods.BuildWorks.Geometry.BetterBuildingBlueprint;

namespace OstrixMods.BuildWorks
{
    internal sealed class BetterBuildingExport
    {
        internal enum Target { Bbp, LegacyJson }
        internal enum BlockReason { SourceBinding, TerrainFrame, PrefabScaleUnknown, AttachmentPrefab, Format }
        internal readonly List<(BlockReason Reason, string Detail)> Blockers = new List<(BlockReason, string)>();
        internal int Parts, Sources, OtherSources, Signs, Attachments, Scales, TerrainSamples, ArchivedState, Groups, Supports, Anchors;
        internal int PayloadVersion, UnknownFields;
        private readonly Target target;
        private BetterBuildingExport(Target target) { this.target = target; }
        private byte[] native, foreign;
        private BlueprintExportPair files;
        internal void CancelWrite() => files?.CancelWrite();

        internal static BetterBuildingExport Review(CompositeBlueprintStore.Blueprint source, Func<string, CompositeBlueprintStore.VectorData?> resolveScale, Target target = Target.Bbp)
        {
            var snapshot = BlueprintExportPair.Snapshot(source);
            var document = snapshot.Document;
            var sources = document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>();
            var roots = BlueprintSourceDocument.Index(sources.Where(s => Bbp.IsFormat(s.format)).Select(s => s.ToGeometry()));
            var review = new BetterBuildingExport(target) { native = snapshot.Native, Parts = document.parts.Count, Sources = roots.Count,
                OtherSources = sources.Count - roots.Count, Signs = document.parts.Count(p => p.signText != null),
                ArchivedState = document.parts.Count(p => p.foreignData != null || p.materialSeed.HasValue),
                Groups = document.groups.Count, Supports = document.supports?.Count ?? 0, Anchors = document.anchors.Count + (document.customSnapPoints?.Count ?? 0),
                PayloadVersion = roots.Count == 0 ? 1 : roots.Values.Max(s => s.BetterBuilding.PayloadVersion),
                UnknownFields = roots.Values.Sum(s => s.BetterBuilding.UnknownPaths.Count) };
            var terrainRoots = roots.Values.Where(s => s.BetterBuilding.Terrain != null).ToArray();
            var terrain = terrainRoots.Length == 1 ? terrainRoots[0].BetterBuilding.Terrain : null;
            review.TerrainSamples = terrain?.Heights.Count ?? 0;
            if (terrainRoots.Length > 1) review.Blockers.Add((BlockReason.TerrainFrame, "multiple terrain sources"));
            if (terrain != null && (document.parts.Count != terrainRoots[0].BetterBuilding.Pieces.Count ||
                document.parts.Any(p => p.sourceDocumentId != terrainRoots[0].Id) ||
                document.parts.Select(p => p.sourceRecord).Distinct().Count() != document.parts.Count))
                review.Blockers.Add((BlockReason.TerrainFrame, terrainRoots[0].Name));
            var rows = new List<Bbp.Piece>();
            var bases = new Dictionary<string, CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
            foreach (var part in document.parts)
            {
                Bbp.Piece original = null;
                if (part.sourceDocumentId != null && roots.TryGetValue(part.sourceDocumentId, out var evidence))
                {
                    if (!part.sourceFrameApplied || !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName))
                    { review.Blockers.Add((BlockReason.SourceBinding, part.stableId)); continue; }
                    original = evidence.BetterBuilding.Pieces[part.sourceRecord - 1];
                }
                var position = new Point3(part.position.x, part.position.y, part.position.z);
                var rotation = new Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w);
                bool samePose = original != null && SamePose(part, original);
                if (samePose) { position = original.Position; rotation = original.Rotation; }
                Point3 multiplier = new Point3(part.scale.x, part.scale.y, part.scale.z);
                if (part.scaleIsAbsolute || part.baseScale.HasValue)
                {
                    if (!bases.TryGetValue(part.prefabName, out var basis)) bases.Add(part.prefabName, basis = resolveScale?.Invoke(part.prefabName));
                    if (!basis.HasValue || !CompositeBlueprintStore.PositiveScale(basis.Value))
                    { review.Blockers.Add((BlockReason.PrefabScaleUnknown, part.prefabName)); continue; }
                    var actual = part.RootScale(basis.Value.ToVector3());
                    multiplier = new Point3(actual.x / basis.Value.x, actual.y / basis.Value.y, actual.z / basis.Value.z);
                }
                Point3? scale = multiplier.X == 1 && multiplier.Y == 1 && multiplier.Z == 1 && original?.Scale == null ? (Point3?)null : multiplier;
                if (original?.DisplayItem != null && part.prefabName != original.Prefab)
                    review.Blockers.Add((BlockReason.AttachmentPrefab, part.stableId));
                if (terrain != null && (!samePose || part.prefabName != original?.Prefab || !SameScale(scale, original?.Scale)))
                    review.Blockers.Add((BlockReason.TerrainFrame, part.stableId));
                try
                {
                    rows.Add(new Bbp.Piece(original?.Record ?? 0, part.prefabName, position, rotation, part.signText, original?.DisplayItem, scale));
                }
                catch (FormatException) { review.Blockers.Add((BlockReason.Format, part.stableId)); }
            }
            review.Attachments = rows.Count(p => p.DisplayItem != null); review.Scales = rows.Count(p => p.Scale.HasValue);
            if (review.Attachments > 0) review.PayloadVersion = Math.Max(review.PayloadVersion, 2);
            if (review.Signs > 0) review.PayloadVersion = Math.Max(review.PayloadVersion, 3);
            if (review.Scales > 0) review.PayloadVersion = 4;
            if (review.Blockers.Count > 0) return review;
            try
            {
                review.foreign = target == Target.LegacyJson ? new UTF8Encoding(false, true).GetBytes(Bbp.WriteJson(review.PayloadVersion, document.name, rows, terrain)) :
                    Bbp.WriteBinary(review.PayloadVersion, document.name, rows, terrain);
                var read = target == Target.LegacyJson ? Bbp.ParseJson(new UTF8Encoding(false, true).GetString(review.foreign)) : Bbp.ParseBinary(review.foreign);
                if (read.Pieces.Count != rows.Count) throw new FormatException("BBPF output count differs");
            }
            catch (FormatException) { review.Blockers.Add((BlockReason.Format, document.name)); return review; }
            review.files = new BlueprintExportPair(review.native, review.foreign, target == Target.LegacyJson ? ".json" : ".bbp");
            return review;
        }
        private static bool SamePose(CompositeBlueprintStore.Part part, Bbp.Piece original)
        {
            Rotation3 q = original.Rotation.Normalized();
            return part.position.x == (float)original.Position.X && part.position.y == (float)original.Position.Y && part.position.z == (float)original.Position.Z &&
                (part.rotation.x == (float)q.X && part.rotation.y == (float)q.Y && part.rotation.z == (float)q.Z && part.rotation.w == (float)q.W ||
                 part.rotation.x == -(float)q.X && part.rotation.y == -(float)q.Y && part.rotation.z == -(float)q.Z && part.rotation.w == -(float)q.W);
        }
        private static bool SameScale(Point3? left, Point3? right)
        {
            Point3 a = left ?? new Point3(1, 1, 1), b = right ?? new Point3(1, 1, 1);
            return (float)a.X == (float)b.X && (float)a.Y == (float)b.Y && (float)a.Z == (float)b.Z;
        }
        internal bool TryWrite(CompositeBlueprintStore.Blueprint source, Func<string, CompositeBlueprintStore.VectorData?> resolveScale,
            string directory, string name, out string result, out string error)
        {
            result = null; error = null;
            if (files == null || Blockers.Count > 0) { error = "$buildworks_import.transform"; return false; }
            if (target == Target.LegacyJson && name?.EndsWith(".buildworks", StringComparison.OrdinalIgnoreCase) == true)
            { error = "$buildworks_exchange.bbp_json_reserved_name"; return false; }
            return files.TryWrite(() => {
                var current = Review(source, resolveScale, target);
                if (current.Blockers.Count > 0) throw new InvalidDataException("$buildworks_exchange.plan_changed");
                return (current.native, current.foreign);
            }, directory, name, out result, out error);
        }
    }
}
