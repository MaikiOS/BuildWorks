using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed class PlannerExport
    {
        internal enum BlockReason { MultipleSources, SourceBinding, PrefabScaleUnknown, Scale, Rotation, Format }
        internal readonly List<(BlockReason Reason, string Detail)> Blockers = new List<(BlockReason, string)>();
        internal int Parts, Sources, OtherSources, Layers, EmptyLayers, BoundLayers, NewLayers, SourceUnknownFields;
        internal int Signs, State, Metadata, Supports, Anchors, Hierarchy, EditorFlags;
        private byte[] native, json;
        private BlueprintExportPair files;
        internal void CancelWrite() => files?.CancelWrite();

        internal static PlannerExport Review(CompositeBlueprintStore.Blueprint source, Func<string, CompositeBlueprintStore.VectorData?> resolveScale)
        {
            if (source?.importAbsoluteScale == true) throw new InvalidDataException("$buildworks_import.scale_pending");
            var snapshot = BlueprintExportPair.Snapshot(source); var document = snapshot.Document;
            var sources = document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>();
            var roots = sources.Where(s => s.format == PlannerBlueprint.Format).ToArray();
            var review = new PlannerExport { native = snapshot.Native, Parts = document.parts.Count, Sources = roots.Length,
                OtherSources = sources.Count - roots.Length, Signs = document.parts.Count(p => p.signText != null),
                State = document.parts.Count(p => p.foreignData != null || p.materialSeed.HasValue),
                Metadata = document.parts.Count(p => p.metadata != null), Supports = document.supports?.Count ?? 0,
                Anchors = document.anchors.Count + (document.customSnapPoints?.Count ?? 0),
                Hierarchy = document.groups.Count(g => g.parentGroupId != null),
                EditorFlags = document.groups.Count(g => !g.editorVisible || g.editorLocked || g.pivotPartId != null) +
                    document.parts.Count(p => !p.editorVisible || p.editorLocked) };
            if (roots.Length > 1)
            { review.Blockers.Add((BlockReason.MultipleSources, roots.Length.ToString(System.Globalization.CultureInfo.InvariantCulture))); return review; }
            var evidence = roots.SingleOrDefault()?.ToGeometry(); var model = evidence?.Planner;
            review.SourceUnknownFields = model?.UnknownPaths.Count ?? 0;
            var output = new List<PlannerBlueprint.Layer>();
            var groupLayers = new Dictionary<string, string>(StringComparer.Ordinal);
            var used = new HashSet<string>(StringComparer.Ordinal);
            var reserved = new HashSet<string>(model?.Layers.Select(l => l.Id) ?? Enumerable.Empty<string>(), StringComparer.Ordinal);
            string NewLayerId(string stableId)
            {
                string prefix;
                // The original planner truncates layer IDs at64; keep room for collision suffixes.
                using (var hash = SHA256.Create()) prefix = "bw_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(stableId))).Replace("-", "").ToLowerInvariant().Substring(0, 32);
                string id = prefix; int suffix = 0;
                while (used.Contains(id) || reserved.Contains(id)) id = prefix + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                used.Add(id); return id;
            }
            try
            {
                foreach (var group in document.groups)
                {
                    string donor = evidence != null && group.sourceDocumentId == evidence.Id ? group.sourceLayerId : null;
                    if (donor != null && donor.Length > 64)
                    { review.Blockers.Add((BlockReason.Format, "layer ID exceeds64 characters")); return review; }
                    string id = donor != null && used.Add(donor) ? donor : NewLayerId(group.stableId);
                    groupLayers.Add(group.stableId, id);
                    output.Add(new PlannerBlueprint.Layer(id, group.name, donor));
                    if (donor != null) ++review.BoundLayers;
                    if (donor == null || donor != id) ++review.NewLayers;
                }
                string ungrouped = null;
                if (document.parts.Any(p => p.parentGroupId == null))
                {
                    ungrouped = model == null && used.Add("main") ? "main" : NewLayerId(document.id + "\nungrouped");
                    output.Add(new PlannerBlueprint.Layer(ungrouped, "Main")); ++review.NewLayers;
                }
                var rows = new List<(string Prefab, Point3 Position, Rotation3 Rotation, int Record, string Layer)>();
                var scales = new Dictionary<string, CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
                foreach (var part in document.parts)
                {
                    var rotation = new Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w).Normalized();
                    if (rotation.X != 0 || rotation.Z != 0) review.Blockers.Add((BlockReason.Rotation, part.stableId));
                    CompositeBlueprintStore.VectorData basis = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
                    if (!part.scaleIsAbsolute && !part.baseScale.HasValue)
                    {
                        if (!scales.TryGetValue(part.prefabName, out var known)) scales.Add(part.prefabName, known = resolveScale?.Invoke(part.prefabName));
                        if (!known.HasValue || !CompositeBlueprintStore.PositiveScale(known.Value))
                        { review.Blockers.Add((BlockReason.PrefabScaleUnknown, part.prefabName)); continue; }
                        basis = known.Value;
                    }
                    var actual = part.RootScale(basis.ToVector3());
                    if (actual.x != 1 || actual.y != 1 || actual.z != 1) review.Blockers.Add((BlockReason.Scale, part.stableId));
                    var position = new Point3(part.position.x, part.position.y, part.position.z);
                    int record = 0;
                    if (evidence != null && part.sourceDocumentId == evidence.Id)
                    {
                        if (!part.sourceFrameApplied || !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName))
                        { review.Blockers.Add((BlockReason.SourceBinding, part.stableId)); continue; }
                        record = part.sourceRecord; var original = model.Pieces[record - 1];
                        if (SamePose(part, original)) { position = original.Position; rotation = original.Rotation; }
                    }
                    rows.Add((part.prefabName, position, rotation, record, part.parentGroupId == null ? ungrouped : groupLayers[part.parentGroupId]));
                }
                review.Layers = output.Count;
                review.EmptyLayers = output.Count(l => !rows.Any(p => p.Layer == l.Id));
                if (review.Blockers.Count != 0) return review;
                string text = model == null ? PlannerBlueprint.WriteNew(rows, document.name, output, document.id) : model.WritePieces(rows, document.name, output);
                if (ExternalBlueprintLibrary.Parse(text, "planner.json").importError != null)
                    throw new FormatException("Output outside the supported planner profile.");
                review.json = new UTF8Encoding(false, true).GetBytes(text);
                review.files = new BlueprintExportPair(review.native, review.json, ".json");
            }
            catch (Exception error) when (error is FormatException || error is ArgumentException)
            { review.Blockers.Add((BlockReason.Format, document.name)); }
            return review;
        }

        private static bool SamePose(CompositeBlueprintStore.Part part, PlannerBlueprint.Piece original)
        {
            if ((part.originalPrefabName ?? part.prefabName) != original.Prefab || part.position.x != (float)original.Position.X ||
                part.position.y != (float)original.Position.Y || part.position.z != (float)original.Position.Z) return false;
            var q = original.Rotation.Normalized();
            return new[] { 1, -1 }.Any(sign => part.rotation.x == sign * (float)q.X && part.rotation.y == sign * (float)q.Y &&
                part.rotation.z == sign * (float)q.Z && part.rotation.w == sign * (float)q.W);
        }

        internal bool TryWrite(CompositeBlueprintStore.Blueprint source, Func<string, CompositeBlueprintStore.VectorData?> resolveScale,
            string directory, string name, out string result, out string error)
        {
            result = null; error = null;
            if (name != null && name.EndsWith(".buildworks", StringComparison.OrdinalIgnoreCase)) { error = "$buildworks_exchange.invalid_name"; return false; }
            if (files == null || Blockers.Count != 0) { error = "$buildworks_import.transform"; return false; }
            return files.TryWrite(() =>
            {
                var current = Review(source, resolveScale);
                if (current.Blockers.Count != 0) throw new InvalidDataException("$buildworks_exchange.plan_changed");
                return (current.native, current.json);
            }, directory, name, out result, out error);
        }
    }
}
