using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed class UnboundExport
    {
        internal enum BlockReason { MultipleSources, SourceBinding, HostProfile, StateAdapter, TerrainFrame, Precision, Format }
        internal readonly List<(BlockReason Reason, string Detail)> Blockers = new List<(BlockReason, string)>();
        internal int Parts, Sources, OtherSources, Signs, MaterialSeeds, Storages, ClearedStorages, Mounts, ClearedMounts, TerrainSamples, HeaderChanges;
        private byte[] native, foreign;
        private BlueprintExportPair files;
        private readonly Dictionary<string, UnboundImportContext> facts = new Dictionary<string, UnboundImportContext>(StringComparer.Ordinal);
        internal void CancelWrite() => files?.CancelWrite();

        internal static UnboundExport Review(CompositeBlueprintStore.Blueprint source, Func<string, UnboundImportContext> context)
        {
            var snapshot = BlueprintExportPair.Snapshot(source); var document = snapshot.Document;
            var roots = (document.sourceDocuments ?? new List<CompositeBlueprintStore.SourceDocument>())
                .Where(item => item.format == UnboundBlueprint.Format).ToArray();
            var review = new UnboundExport { native = snapshot.Native, Parts = document.parts.Count, Sources = roots.Length,
                OtherSources = (document.sourceDocuments?.Count ?? 0) - roots.Length,
                Signs = document.parts.Count(p => p.signText != null), MaterialSeeds = document.parts.Count(p => p.materialSeed.HasValue) };
            if (roots.Length > 1) { review.Blockers.Add((BlockReason.MultipleSources, document.name)); return review; }
            var evidence = roots.SingleOrDefault()?.ToGeometry(); var model = evidence?.Unbound;
            bool terrain = model?.TerrainSize > 0;
            var boundLines = new HashSet<int>();
            var text = new StringBuilder("vtbp 2\n");
            if (terrain)
            {
                // Terrain has no independently editable frame; retain it only with the original complete layout.
                using (var reader = new StringReader(model.Text))
                    for (int line = 1; line <= model.TerrainLine; ++line)
                    { string row = reader.ReadLine(); if (line == model.TerrainLine) text.Append(row).Append('\n'); }
                review.TerrainSamples = model.Terrain.Count;
            }
            try
            {
                foreach (var part in document.parts)
                {
                    if (!review.facts.TryGetValue(part.prefabName, out var profile))
                        review.facts.Add(part.prefabName, profile = context?.Invoke(part.prefabName));
                    if (profile?.NativeHeader == null || profile.Metadata.PrefabId != part.prefabName)
                    { review.Blockers.Add((BlockReason.HostProfile, part.prefabName)); continue; }
                    UnboundBlueprint.Piece original = null;
                    if (evidence != null && part.sourceDocumentId == evidence.Id)
                    {
                        if (!part.sourceFrameApplied || !evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName))
                        { review.Blockers.Add((BlockReason.SourceBinding, part.stableId)); continue; }
                        original = evidence.UnboundPartAt(part.sourceLine);
                        boundLines.Add(part.sourceLine);
                    }
                    if (terrain && (original == null || !SamePose(part, original)))
                        review.Blockers.Add((BlockReason.TerrainFrame, part.stableId));
                    var contents = part.foreignData?.ToGeometry();
                    if (contents != null && contents.Format != BlueprintForeignPartData.NativeContainerFormat &&
                        contents.Format != BlueprintForeignPartData.NativeItemStandFormat)
                    { review.Blockers.Add((BlockReason.StateAdapter, part.stableId)); continue; }
                    var basis = profile.Metadata.PrefabScale;
                    var scale = part.RootScale(new UnityEngine.Vector3((float)basis.X, (float)basis.Y, (float)basis.Z));
                    if (!UnboundStateProjection.TryEncode(original, profile, new Point3(scale.x, scale.y, scale.z),
                        part.signText, part.materialSeed, contents, out string packet))
                    { review.Blockers.Add((BlockReason.StateAdapter, part.stableId)); continue; }
                    var header = profile.NativeHeader.Value;
                    if (original != null && (original.State.Persistent != header.Persistent ||
                        original.State.Distant != header.Distant || original.State.NativeObjectType != header.ObjectType)) ++review.HeaderChanges;
                    if (profile.ContainerGrid.HasValue) { ++review.Storages; if (contents?.ContentsCleared == true) ++review.ClearedStorages; }
                    if (profile.ItemStandItems != null) { ++review.Mounts; if (contents?.ContentsCleared == true) ++review.ClearedMounts; }
                    string[] fields = { part.prefabName, Number(part.position.x), Number(part.position.y), Number(part.position.z),
                        Number(part.rotation.x), Number(part.rotation.y), Number(part.rotation.z), Number(part.rotation.w), packet };
                    text.Append(string.Join("|", fields)).Append('\n');
                    if (text.Length > BlueprintSourceDocument.MaximumBytes) throw new FormatException("Unbound export size.");
                }
                if (terrain && (document.parts.Count != model.Pieces.Count || boundLines.Count != model.Pieces.Count))
                    review.Blockers.Add((BlockReason.TerrainFrame, document.name));
                if (review.Blockers.Count != 0) return review;
                var reopened = UnboundBlueprint.Parse(text.ToString());
                for (int i = 0; i < document.parts.Count; ++i)
                    if (!MatchesWorking(document.parts[i], reopened.Pieces[i], review.facts[document.parts[i].prefabName]))
                        review.Blockers.Add((BlockReason.Precision, document.parts[i].stableId));
                if (review.Blockers.Count != 0) return review;
                review.foreign = new UTF8Encoding(false, true).GetBytes(text.ToString());
                review.files = new BlueprintExportPair(review.native, review.foreign, ".vtbp");
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is IOException)
            { review.Blockers.Add((BlockReason.Format, document.name)); }
            return review;
        }

        private static string Number(float value) => value.ToString("R", CultureInfo.InvariantCulture);
        private static bool SamePose(CompositeBlueprintStore.Part part, UnboundBlueprint.Piece original)
        {
            if (part.prefabName != original.PrefabName || part.position.x != (float)original.Position.X ||
                part.position.y != (float)original.Position.Y || part.position.z != (float)original.Position.Z) return false;
            var rotation = original.Rotation.Normalized();
            return new[] { 1, -1 }.Any(sign => part.rotation.x == sign * (float)rotation.X && part.rotation.y == sign * (float)rotation.Y &&
                part.rotation.z == sign * (float)rotation.Z && part.rotation.w == sign * (float)rotation.W);
        }

        private static bool MatchesWorking(CompositeBlueprintStore.Part part, UnboundBlueprint.Piece output, UnboundImportContext profile)
        {
            var contents = part.foreignData?.ToGeometry();
            var names = (contents?.Format == BlueprintForeignPartData.NativeItemStandFormat ? new[] { contents.Info.Split(':')[0] } :
                contents?.Info.Split('|').Skip(1) ?? Enumerable.Empty<string>()).ToDictionary(name => name.GetStableHashCode(), name => name);
            if (!UnboundStateProjection.TryProject(output, profile, hash => names.TryGetValue(hash, out string name) ? name : null, out var state) ||
                output.PrefabName != part.prefabName || output.Position.X != part.position.x || output.Position.Y != part.position.y ||
                output.Position.Z != part.position.z || output.Rotation.X != part.rotation.x || output.Rotation.Y != part.rotation.y ||
                output.Rotation.Z != part.rotation.z || output.Rotation.W != part.rotation.w || state.SignText != part.signText ||
                state.MaterialSeed != part.materialSeed) return false;
            var basis = profile.Metadata.PrefabScale;
            var expected = part.RootScale(new UnityEngine.Vector3((float)basis.X, (float)basis.Y, (float)basis.Z));
            if (state.RootScale.X != expected.x || state.RootScale.Y != expected.y || state.RootScale.Z != expected.z) return false;
            if (profile.ItemStandItems != null)
                return contents == null || contents.ContentsCleared ? state.Contents == null :
                    state.Contents?.Format == contents.Format && state.Contents.Info == contents.Info && state.Contents.Zdo == contents.Zdo;
            if (!profile.ContainerGrid.HasValue) return state.Contents == null;
            if (contents == null || contents.ContentsCleared)
                return BlueprintContainerInventory.TryRead(state.Contents, name => name.GetStableHashCode(), out var items) && items.Count == 0;
            return state.Contents?.Zdo == contents.Zdo && state.Contents.Info == contents.Info;
        }

        private static bool SameFacts(UnboundImportContext a, UnboundImportContext b) => a != null && b != null &&
            a.NativeHeader.Equals(b.NativeHeader) && a.ContainerGrid.Equals(b.ContainerGrid) && a.Ordinary == b.Ordinary &&
            a.Sign == b.Sign && a.MaterialSeed == b.MaterialSeed && a.SyncInitialScale == b.SyncInitialScale &&
            a.Metadata.PrefabId == b.Metadata.PrefabId && a.Metadata.HostId == b.Metadata.HostId &&
            a.Metadata.PrefabScale.X == b.Metadata.PrefabScale.X && a.Metadata.PrefabScale.Y == b.Metadata.PrefabScale.Y &&
            a.Metadata.PrefabScale.Z == b.Metadata.PrefabScale.Z &&
            (a.ItemStandItems == null ? b.ItemStandItems == null : b.ItemStandItems != null && a.ItemStandItems.Count == b.ItemStandItems.Count &&
                a.ItemStandItems.All(pair => b.ItemStandItems.TryGetValue(pair.Key, out var item) && pair.Value.Equals(item)));

        internal bool TryWrite(CompositeBlueprintStore.Blueprint source, Func<string, UnboundImportContext> context,
            string directory, string name, out string result, out string error)
        {
            result = null; error = null;
            if (files == null || Blockers.Count != 0) { error = "$buildworks_import.transform"; return false; }
            return files.TryWrite(() =>
            {
                var current = Review(source, context);
                if (current.Blockers.Count != 0 || facts.Count != current.facts.Count ||
                    facts.Any(pair => !current.facts.TryGetValue(pair.Key, out var value) || !SameFacts(pair.Value, value)))
                    throw new InvalidDataException("$buildworks_exchange.plan_changed");
                return (current.native, current.foreign);
            }, directory, name, out result, out error);
        }
    }
}
