using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading.Tasks;
using BepInEx;
using UnityEngine;

namespace OstrixMods.BuildWorks
{
    /// <summary>
    /// Validates, migrates, clones, and atomically persists the blueprint
    /// library. This type deliberately has no UI-localization responsibility.
    /// </summary>
    internal sealed class CompositeBlueprintStore
    {
        internal const int MaximumParts = Geometry.BlueprintEditorDocument.MaximumParts;
        internal const int InteractivePartBudget = 512;
        internal static string InteractiveError(int count) => count > InteractivePartBudget
            ? UserError("import.interactive_limit", count + "/" + InteractivePartBudget) : null;
        private const int MaximumLibraryParts = 100000;
        internal const string DefaultCategory = "OTHER";
        private const string LegacyDefaultCategory = "ПРОЧЕЕ";
        private const string LocalizationPrefix = "$buildworks_";
        private const int FormatVersion = 11;
        private const int MaximumBlueprints = 32;
        private const int MaximumCategories = 12;
        private const int MaximumAnchors = 512;
        private const int MaximumGroups = 64;
        private readonly string path;
        private LibraryData data = new LibraryData();
        private List<Blueprint> externalBlueprints;
        private IReadOnlyList<Blueprint> catalogSnapshot;
        private Task<List<Blueprint>> importTask;
        internal int Revision { get; private set; }
        internal bool CatalogLoading => importTask != null && !importTask.IsCompleted;
        internal string ImportError { get; private set; }
        private string writeBlockReason;

        public CompositeBlueprintStore()
            : this(Path.Combine(Paths.ConfigPath, "BuildWorks", "blueprints.json"), true)
        {
        }

        internal CompositeBlueprintStore(string storagePath) : this(storagePath, false) { }

        internal CompositeBlueprintStore(string storagePath, bool backgroundImports)
        {
            if (string.IsNullOrWhiteSpace(storagePath))
                throw new ArgumentException("Storage path is required.", nameof(storagePath));
            path = Path.GetFullPath(storagePath);
            Load();
            if (backgroundImports)
                importTask = Task.Run(() => ExternalBlueprintLibrary.Load(ImportDirectory));
        }

        public IReadOnlyList<Blueprint> All() => data.blueprints;

        internal IReadOnlyList<Blueprint> CatalogAll()
        {
            if (importTask != null && importTask.IsCompleted)
            {
                try { externalBlueprints = importTask.GetAwaiter().GetResult(); ImportError = null; }
                catch (Exception exception) { ImportError = exception.Message; }
                importTask = null;
                externalBlueprints = externalBlueprints ?? new List<Blueprint>();
                catalogSnapshot = null;
            }
            if (externalBlueprints == null)
            {
                if (importTask != null) return data.blueprints;
                externalBlueprints = ExternalBlueprintLibrary.Load(ImportDirectory);
            }
            if (catalogSnapshot != null) return catalogSnapshot;
            var result = new List<Blueprint>(data.blueprints);
            result.AddRange(externalBlueprints);
            return catalogSnapshot = result;
        }

        private string ImportDirectory => Path.Combine(Path.GetDirectoryName(path), "imports");

        internal bool TryImportFile(string source, out string error)
        {
            error = null;
            if (CatalogLoading) { error = UserError("exchange.loading"); return false; }
            CatalogAll();
            try
            {
                source = Path.GetFullPath((source ?? "").Trim().Trim('"'));
                if (!File.Exists(source)) throw new FileNotFoundException(source);
                string target = Path.Combine(ImportDirectory, Path.GetFileName(source));
                if (File.Exists(target)) throw new IOException(UserError("exchange.exists", target));
                string input = source;
                ImportError = null;
                importTask = Task.Run(() =>
                {
                    Directory.CreateDirectory(ImportDirectory);
                    if ((File.GetAttributes(ImportDirectory) & FileAttributes.ReparsePoint) != 0 ||
                        (File.GetAttributes(input) & FileAttributes.ReparsePoint) != 0)
                        throw new InvalidDataException("$buildworks_import.path");
                    string temporary = Path.Combine(ImportDirectory, Guid.NewGuid().ToString("N") + ".tmp" + Path.GetExtension(target));
                    try
                    {
                        using (Stream from = File.OpenRead(input))
                        using (Stream to = File.Create(temporary))
                        {
                            var buffer = new byte[8192]; int read; long total = 0;
                            while ((read = from.Read(buffer, 0, buffer.Length)) != 0)
                            {
                                total += read;
                                if (total > 64L * 1024 * 1024) throw new InvalidDataException("$buildworks_import.file_limit");
                                to.Write(buffer, 0, read);
                            }
                        }
                        // Native names must retain the complete suffix during validation.
                        string validation = temporary;
                        if (target.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase))
                        { validation = temporary + ".buildworks.json"; File.Move(temporary, validation); temporary = validation; }
                        if (ExternalBlueprintLibrary.LoadFile(validation).Count == 0)
                            throw new InvalidDataException("$buildworks_import.format");
                        if (Directory.EnumerateFiles(ImportDirectory).Take(2001).Count() >= 2001)
                            throw new InvalidDataException("$buildworks_import.library_limit");
                        File.Move(temporary, target); // Never overwrite either source or an existing library file.
                        return ExternalBlueprintLibrary.Load(ImportDirectory);
                    }
                    finally
                    {
                        try { if (File.Exists(temporary)) File.Delete(temporary); }
                        catch (Exception exception) { Debug.LogWarning("BuildWorks import cleanup: " + exception.Message); }
                    }
                });
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        internal bool TryExportFile(Blueprint blueprint, out string exportedPath, out string error)
        {
            exportedPath = null; error = null; string temporary = null;
            try
            {
                if (blueprint == null || !string.IsNullOrEmpty(blueprint.importError))
                    throw new InvalidDataException(blueprint?.importError ?? "$buildworks_import.format");
                byte[] bytes = BlueprintExchangeFormat.Encode(blueprint.IsExternal
                    ? ExternalBlueprintLibrary.DetachedCopy(blueprint) : blueprint);
                BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(bytes));
                string directory = Path.Combine(Path.GetDirectoryName(path), "exports");
                Directory.CreateDirectory(directory);
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("$buildworks_import.path");
                string target = Path.Combine(directory, "blueprint_" + Guid.NewGuid().ToString("N") + ".buildworks.json");
                temporary = target + ".tmp";
                File.WriteAllBytes(temporary, bytes);
                File.Move(temporary, target);
                exportedPath = target;
                return true;
            }
            catch (Exception exception) { error = exception.Message; return false; }
            finally
            {
                try { if (temporary != null && File.Exists(temporary)) File.Delete(temporary); }
                catch (Exception exception) { Debug.LogWarning("BuildWorks export cleanup: " + exception.Message); }
            }
        }

        public IReadOnlyList<string> Categories() => data.categories;

        public bool TrySave(
            IReadOnlyList<Part> parts,
            IReadOnlyList<VectorData> anchors,
            out Blueprint blueprint,
            out string error) => TrySaveDocument(
                parts,
                Array.Empty<Group>(),
                anchors,
                out blueprint,
                out error);

        public bool TrySave(
            string name,
            IReadOnlyList<Part> parts,
            IReadOnlyList<VectorData> anchors,
            out Blueprint blueprint,
            out string error) => TrySaveDocument(
                name,
                DefaultCategory,
                parts,
                Array.Empty<Group>(),
                anchors,
                out blueprint,
                out error);

        public bool TrySaveDocument(
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            out Blueprint blueprint,
            out string error) => TrySaveDocument(
                "Group " + (data.blueprints.Count + 1),
                DefaultCategory,
                parts,
                groups,
                anchors,
                out blueprint,
                out error);

        public bool TrySaveDocument(
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            out Blueprint blueprint,
            out string error) => TrySaveDocument(
                name, category, parts, groups, anchors, null, null, out blueprint, out error);

        public bool TrySaveDocument(
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            string primaryPartId,
            out Blueprint blueprint,
            out string error) => TrySaveDocument(name, category, parts, groups, anchors,
                primaryPartId, null, out blueprint, out error);

        public bool TrySaveDocument(
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            string primaryPartId,
            string primaryGroupId,
            out Blueprint blueprint,
            out string error,
            IReadOnlyList<VectorData> customSnapPoints = null)
            => TrySaveDocument(name, category, parts, groups, anchors, primaryPartId,
                primaryGroupId, null, null, out blueprint, out error, customSnapPoints);

        public bool TrySaveDocument(
            string name, string category, IReadOnlyList<Part> parts, IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors, string primaryPartId, string primaryGroupId,
            IReadOnlyList<Support> supports, string placementSupportId,
            out Blueprint blueprint, out string error, IReadOnlyList<VectorData> customSnapPoints = null,
            Blueprint previewTemplate = null)
        {
            blueprint = null;
            error = null;
            name = (name ?? string.Empty).Trim();
            category = NormalizeCategory(category);
            primaryPartId = string.IsNullOrWhiteSpace(primaryPartId) ? null : primaryPartId.Trim();
            primaryGroupId = string.IsNullOrWhiteSpace(primaryGroupId) ? null : primaryGroupId.Trim();
            if (!string.IsNullOrEmpty(writeBlockReason))
            {
                error = writeBlockReason;
                return false;
            }
            if (name.Length < 1 || name.Length > 48 ||
                category.Length < 1 || category.Length > 24)
            {
                error = UserError("store.invalid_metadata");
                return false;
            }
            bool addCategory = !data.categories.Contains(category);
            if (addCategory && data.categories.Count >= MaximumCategories)
            {
                error = UserError("store.category_limit");
                return false;
            }
            if (parts == null || parts.Count < 2 || parts.Count > MaximumParts ||
                groups == null || groups.Count > MaximumGroups || anchors == null ||
                anchors.Count > MaximumAnchors)
            {
                error = UserError("store.invalid_composition");
                return false;
            }
            if (!ValidCustomSnapPoints(customSnapPoints))
            {
                error = UserError("store.invalid_anchor");
                return false;
            }
            if (data.blueprints.Count >= MaximumBlueprints)
            {
                error = UserError("store.blueprint_limit");
                return false;
            }
            int libraryParts = parts.Count;
            foreach (Blueprint existing in data.blueprints) libraryParts += existing.parts.Count;
            if (libraryParts > MaximumLibraryParts)
            {
                error = UserError("store.invalid_composition");
                return false;
            }
            List<Part> preparedParts = PrepareParts(parts);
            List<Group> preparedGroups = CloneGroups(groups);
            if (!ValidDocument(preparedParts, preparedGroups, supports) ||
                !ValidPlacementSupport(supports, placementSupportId))
            {
                error = UserError("store.invalid_parts_or_groups");
                return false;
            }
            if (!ValidPrimaryPart(preparedParts, primaryPartId))
            {
                error = UserError("store.primary_part_missing");
                return false;
            }
            if (!ValidPrimaryGroup(preparedParts, preparedGroups, primaryGroupId))
            {
                error = UserError("store.primary_group_missing");
                return false;
            }
            foreach (VectorData anchor in anchors)
            {
                if (!Valid(anchor))
                {
                    error = UserError("store.invalid_anchor");
                    return false;
                }
            }

            blueprint = new Blueprint
            {
                id = Guid.NewGuid().ToString("N"),
                name = name,
                prefabName = PrimaryPrefabName(preparedParts, primaryPartId),
                category = category,
                parts = preparedParts,
                groups = preparedGroups,
                anchors = new List<VectorData>(anchors),
                customSnapPoints = customSnapPoints == null ? null : new List<VectorData>(customSnapPoints),
                supports = CloneSupports(supports),
                placementSupportId = placementSupportId,
                primaryPartId = primaryPartId,
                primaryGroupId = primaryGroupId,
                previewYaw = previewTemplate?.previewYaw ?? 45f,
                previewPitch = previewTemplate?.previewPitch ?? 30f,
                previewZoom = previewTemplate?.previewZoom ?? 1f
            };
            if (!Valid(blueprint)) { blueprint = null; error = UserError("store.verify_data_failed"); return false; }
            if (addCategory) data.categories.Add(category);
            data.blueprints.Add(blueprint);
            try
            {
                Save();
                return true;
            }
            catch (Exception exception)
            {
                data.blueprints.Remove(blueprint);
                if (addCategory) data.categories.Remove(category);
                blueprint = null;
                error = exception.Message;
                return false;
            }
        }

        public bool TryRename(Blueprint blueprint, string name, out string error)
        {
            error = null;
            name = (name ?? string.Empty).Trim();
            if (!CanWrite(blueprint, out error) || name.Length < 1 || name.Length > 48)
            {
                if (error == null) error = UserError("store.invalid_name");
                return false;
            }
            string previous = blueprint.name;
            blueprint.name = name;
            return TryCommit(() => blueprint.name = previous, out error);
        }

        public bool TryDelete(Blueprint blueprint, out string error)
        {
            error = null;
            if (!CanWrite(blueprint, out error)) return false;
            int index = data.blueprints.IndexOf(blueprint);
            data.blueprints.RemoveAt(index);
            return TryCommit(() => data.blueprints.Insert(index, blueprint), out error);
        }

        public bool TrySetCategory(Blueprint blueprint, string category, out string error)
        {
            error = null;
            category = NormalizeCategory(category);
            if (!CanWrite(blueprint, out error) || !data.categories.Contains(category))
            {
                if (error == null) error = UserError("store.category_missing");
                return false;
            }
            string previous = blueprint.category;
            blueprint.category = category;
            return TryCommit(() => blueprint.category = previous, out error);
        }

        public bool TryAddCategory(string category, out string error)
        {
            error = null;
            category = NormalizeCategory(category);
            if (!string.IsNullOrEmpty(writeBlockReason))
            {
                error = writeBlockReason;
                return false;
            }
            if (category.Length < 1 || category.Length > 24)
            {
                error = UserError("store.invalid_category_name");
                return false;
            }
            if (data.categories.Exists(value => string.Equals(
                value, category, StringComparison.OrdinalIgnoreCase)))
            {
                error = UserError("store.duplicate_category");
                return false;
            }
            if (data.categories.Count >= MaximumCategories)
            {
                error = UserError("store.category_limit");
                return false;
            }
            data.categories.Add(category);
            return TryCommit(() => data.categories.Remove(category), out error);
        }

        public bool TryDeleteCategory(string category, out string error)
        {
            error = null;
            category = NormalizeCategory(category);
            if (!string.IsNullOrEmpty(writeBlockReason))
            {
                error = writeBlockReason;
                return false;
            }
            int index = data.categories.IndexOf(category);
            if (index < 0 || string.Equals(category, DefaultCategory, StringComparison.Ordinal))
            {
                error = UserError("store.protected_category");
                return false;
            }
            var moved = new List<Blueprint>();
            foreach (Blueprint blueprint in data.blueprints)
            {
                if (!string.Equals(blueprint.category, category, StringComparison.Ordinal))
                    continue;
                blueprint.category = DefaultCategory;
                moved.Add(blueprint);
            }
            data.categories.RemoveAt(index);
            return TryCommit(() =>
            {
                data.categories.Insert(index, category);
                foreach (Blueprint blueprint in moved) blueprint.category = category;
            }, out error);
        }

        public bool TrySetPreview(
            Blueprint blueprint,
            float yaw,
            float pitch,
            float zoom,
            out string error)
        {
            error = null;
            if (!CanWrite(blueprint, out error) || !IsFinite(yaw) || !IsFinite(pitch) ||
                !IsFinite(zoom) || pitch < -80f || pitch > 80f || zoom < 0.5f || zoom > 3f)
            {
                if (error == null) error = UserError("store.invalid_preview");
                return false;
            }
            float previousYaw = blueprint.previewYaw;
            float previousPitch = blueprint.previewPitch;
            float previousZoom = blueprint.previewZoom;
            blueprint.previewYaw = yaw;
            blueprint.previewPitch = pitch;
            blueprint.previewZoom = zoom;
            return TryCommit(() =>
            {
                blueprint.previewYaw = previousYaw;
                blueprint.previewPitch = previousPitch;
                blueprint.previewZoom = previousZoom;
            }, out error);
        }

        public bool TryUpdateParts(
            Blueprint blueprint,
            IReadOnlyList<Part> parts,
            IReadOnlyList<VectorData> anchors,
            out string error)
        {
            List<Part> prepared = PrepareParts(parts);
            if (blueprint?.parts != null && prepared.Count == blueprint.parts.Count)
            {
                for (int index = 0; index < prepared.Count; ++index)
                {
                    if (!string.Equals(prepared[index].prefabName,
                            blueprint.parts[index].prefabName, StringComparison.Ordinal))
                        continue;
                    prepared[index].stableId = blueprint.parts[index].stableId;
                    prepared[index].displayName = blueprint.parts[index].displayName;
                    prepared[index].parentGroupId = blueprint.parts[index].parentGroupId;
                    prepared[index].editorVisible = blueprint.parts[index].editorVisible;
                    prepared[index].editorLocked = blueprint.parts[index].editorLocked;
                }
            }
            return TryUpdateDocument(
                blueprint,
                prepared,
                blueprint?.groups ?? new List<Group>(),
                anchors,
                out error);
        }

        public bool TryUpdateDocument(
            Blueprint blueprint,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            out string error) => TryUpdateDocument(
                blueprint,
                blueprint?.name,
                blueprint?.category,
                parts,
                groups,
                anchors,
                out error);

        public bool TryUpdateDocument(
            Blueprint blueprint,
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            out string error) => TryUpdateDocument(
                blueprint, name, category, parts, groups, anchors,
                blueprint?.primaryPartId, blueprint?.primaryGroupId, out error);

        public bool TryUpdateDocument(
            Blueprint blueprint,
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            string primaryPartId,
            out string error)
            => TryUpdateDocument(blueprint, name, category, parts, groups, anchors,
                primaryPartId, blueprint?.primaryGroupId, out error);

        public bool TryUpdateDocument(
            Blueprint blueprint,
            string name,
            string category,
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<VectorData> anchors,
            string primaryPartId,
            string primaryGroupId,
            out string error,
            IReadOnlyList<VectorData> customSnapPoints = null)
            => TryUpdateDocument(blueprint, name, category, parts, groups, anchors, primaryPartId,
                primaryGroupId, blueprint?.supports, blueprint?.placementSupportId, out error, customSnapPoints);

        public bool TryUpdateDocument(
            Blueprint blueprint, string name, string category, IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups, IReadOnlyList<VectorData> anchors, string primaryPartId,
            string primaryGroupId, IReadOnlyList<Support> supports, string placementSupportId,
            out string error, IReadOnlyList<VectorData> customSnapPoints = null)
        {
            error = null;
            name = (name ?? string.Empty).Trim();
            category = NormalizeCategory(category);
            primaryPartId = string.IsNullOrWhiteSpace(primaryPartId) ? null : primaryPartId.Trim();
            primaryGroupId = string.IsNullOrWhiteSpace(primaryGroupId) ? null : primaryGroupId.Trim();
            customSnapPoints = customSnapPoints ?? blueprint?.customSnapPoints;
            if (!CanWrite(blueprint, out error) || parts == null || groups == null ||
                anchors == null ||
                parts.Count < 2 || parts.Count > MaximumParts ||
                groups.Count > MaximumGroups ||
                anchors.Count > MaximumAnchors)
            {
                if (error == null) error = UserError("store.invalid_composition");
                return false;
            }
            int libraryParts = parts.Count;
            foreach (Blueprint existing in data.blueprints)
                if (!ReferenceEquals(existing, blueprint)) libraryParts += existing.parts.Count;
            if (libraryParts > MaximumLibraryParts || !ValidCustomSnapPoints(customSnapPoints))
            {
                error = UserError("store.invalid_composition");
                return false;
            }
            if (name.Length < 1 || name.Length > 48 ||
                category.Length < 1 || category.Length > 24)
            {
                error = UserError("store.invalid_metadata");
                return false;
            }
            bool addCategory = !data.categories.Contains(category);
            if (addCategory && data.categories.Count >= MaximumCategories)
            {
                error = UserError("store.category_limit");
                return false;
            }
            List<Part> preparedParts = PrepareParts(parts);
            List<Group> preparedGroups = CloneGroups(groups);
            if (!ValidDocument(preparedParts, preparedGroups, supports) ||
                !ValidPlacementSupport(supports, placementSupportId))
            {
                error = UserError("store.invalid_parts_or_groups");
                return false;
            }
            if (!ValidPrimaryPart(preparedParts, primaryPartId))
            {
                error = UserError("store.primary_part_missing");
                return false;
            }
            if (!ValidPrimaryGroup(preparedParts, preparedGroups, primaryGroupId))
            {
                error = UserError("store.primary_group_missing");
                return false;
            }
            foreach (VectorData anchor in anchors)
            {
                if (!Valid(anchor))
                {
                    error = UserError("store.invalid_anchor");
                    return false;
                }
            }

            List<Part> previousParts = blueprint.parts;
            List<Group> previousGroups = blueprint.groups;
            List<VectorData> previousAnchors = blueprint.anchors;
            List<VectorData> previousCustomSnapPoints = blueprint.customSnapPoints;
            List<Support> previousSupports = blueprint.supports;
            string previousPlacementSupportId = blueprint.placementSupportId;
            string previousName = blueprint.name;
            string previousCategory = blueprint.category;
            string previousPrimaryPartId = blueprint.primaryPartId;
            string previousPrimaryGroupId = blueprint.primaryGroupId;
            string previousPrefabName = blueprint.prefabName;
            if (addCategory) data.categories.Add(category);
            blueprint.name = name;
            blueprint.category = category;
            blueprint.parts = preparedParts;
            blueprint.groups = preparedGroups;
            blueprint.anchors = new List<VectorData>(anchors);
            blueprint.customSnapPoints = customSnapPoints == null ? null : new List<VectorData>(customSnapPoints);
            blueprint.supports = CloneSupports(supports);
            blueprint.placementSupportId = placementSupportId;
            blueprint.primaryPartId = primaryPartId;
            blueprint.primaryGroupId = primaryGroupId;
            blueprint.prefabName = PrimaryPrefabName(preparedParts, primaryPartId);
            return TryCommit(() =>
            {
                blueprint.parts = previousParts;
                blueprint.groups = previousGroups;
                blueprint.anchors = previousAnchors;
                blueprint.customSnapPoints = previousCustomSnapPoints;
                blueprint.supports = previousSupports;
                blueprint.placementSupportId = previousPlacementSupportId;
                blueprint.name = previousName;
                blueprint.category = previousCategory;
                blueprint.primaryPartId = previousPrimaryPartId;
                blueprint.primaryGroupId = previousPrimaryGroupId;
                blueprint.prefabName = previousPrefabName;
                if (addCategory) data.categories.Remove(category);
            }, out error);
        }

        internal static List<Part> CloneParts(IReadOnlyList<Part> parts)
        {
            var clone = new List<Part>(parts?.Count ?? 0);
            if (parts == null) return clone;
            foreach (Part part in parts)
            {
                if (part == null)
                {
                    clone.Add(null);
                    continue;
                }
                clone.Add(new Part
                {
                    prefabName = part.prefabName,
                    position = part.position,
                    rotation = part.rotation,
                    scale = part.scale,
                    stableId = part.stableId,
                    displayName = part.displayName,
                    parentGroupId = part.parentGroupId,
                    editorVisible = part.editorVisible,
                    editorLocked = part.editorLocked,
                    signText = part.signText
                });
            }
            return clone;
        }

        internal static List<Group> CloneGroups(IReadOnlyList<Group> groups)
        {
            var clone = new List<Group>(groups?.Count ?? 0);
            if (groups == null) return clone;
            foreach (Group group in groups)
            {
                if (group == null)
                {
                    clone.Add(null);
                    continue;
                }
                clone.Add(new Group
                {
                    stableId = group.stableId,
                    name = group.name,
                    parentGroupId = group.parentGroupId,
                    pivotPartId = group.pivotPartId,
                    editorVisible = group.editorVisible,
                    editorLocked = group.editorLocked
                });
            }
            return clone;
        }

        internal static List<Support> CloneSupports(IReadOnlyList<Support> supports)
        {
            var result = new List<Support>(supports?.Count ?? 0);
            if (supports != null)
                foreach (Support support in supports)
                    result.Add(support == null ? null : new Support
                    {
                        stableId = support.stableId,
                        name = support.name,
                        position = support.position,
                        rotation = support.rotation
                    });
            return result;
        }

        private void Load()
        {
            if (!File.Exists(path)) return;
            try
            {
                if (new FileInfo(path).Length > 128L * 1024 * 1024)
                    throw new InvalidDataException(UserError("store.invalid_composition"));
                string json = File.ReadAllText(path);
                LibraryData loaded = Deserialize(json);
                if (loaded == null || loaded.version < 1 || loaded.version > FormatVersion)
                {
                    writeBlockReason = UserError("store.unknown_version");
                    return;
                }
                BlueprintExchangeFormat.ValidateLibraryJson(json, requireFields: loaded.version >= 6,
                    allowSupportFields: loaded.version >= 11);
                loaded.blueprints = loaded.blueprints ?? new List<Blueprint>();
                loaded.categories = loaded.categories ?? new List<string>();
                if (loaded.version == 1)
                {
                    foreach (Blueprint blueprint in loaded.blueprints)
                    {
                        if (blueprint?.parts == null) continue;
                        foreach (Part part in blueprint.parts)
                            if (part != null && string.IsNullOrWhiteSpace(part.prefabName))
                                part.prefabName = blueprint.prefabName;
                    }
                }
                if (loaded.version < 3)
                {
                    foreach (Blueprint blueprint in loaded.blueprints)
                    {
                        if (blueprint == null) continue;
                        blueprint.category = DefaultCategory;
                        blueprint.previewYaw = 45f;
                        blueprint.previewPitch = 30f;
                        blueprint.previewZoom = 1f;
                    }
                }
                if (loaded.version < 4)
                {
                    foreach (Blueprint blueprint in loaded.blueprints)
                    {
                        if (blueprint == null) continue;
                        blueprint.groups = new List<Group>();
                        if (blueprint.parts == null) continue;
                        for (int index = 0; index < blueprint.parts.Count; ++index)
                        {
                            Part part = blueprint.parts[index];
                            if (part == null) continue;
                            part.stableId = blueprint.id + "_part_" + index.ToString("D3");
                            part.displayName = part.prefabName;
                            part.parentGroupId = null;
                            part.editorVisible = true;
                            part.editorLocked = false;
                        }
                    }
                }
                else
                {
                    foreach (Blueprint blueprint in loaded.blueprints)
                        if (blueprint != null)
                            blueprint.groups = blueprint.groups ?? new List<Group>();
                }
                if (loaded.version < 6)
                    foreach (Blueprint blueprint in loaded.blueprints)
                        if (blueprint?.parts != null)
                            foreach (Part part in blueprint.parts)
                                if (part != null) part.scale = new VectorData { x = 1f, y = 1f, z = 1f };
                NormalizeCategories(loaded);
                foreach (Blueprint blueprint in loaded.blueprints)
                    if (blueprint != null) blueprint.supports = blueprint.supports ?? new List<Support>();
                loaded.version = FormatVersion;
                List<Blueprint> valid = new List<Blueprint>();
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Blueprint blueprint in loaded.blueprints)
                {
                    if (Valid(blueprint) && ids.Add(blueprint.id)) valid.Add(blueprint);
                }
                if (valid.Count != loaded.blueprints.Count)
                    writeBlockReason = UserError("store.corrupt_entries");
                loaded.blueprints = valid;
                int libraryParts = 0;
                foreach (Blueprint blueprint in valid) libraryParts += blueprint.parts.Count;
                if (libraryParts > MaximumLibraryParts || valid.Count > MaximumBlueprints)
                    throw new InvalidDataException(UserError("store.invalid_composition"));
                data = loaded;
            }
            catch (Exception exception)
            {
                writeBlockReason = UserError("store.corrupt_file", exception.Message);
                Debug.LogWarning("BuildWorks blueprint library was not loaded: " + exception);
            }
        }

        private void Save()
        {
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);
            string temporary = path + ".tmp";
            string json = Serialize(data);
            if (Encoding.UTF8.GetByteCount(json) > 128L * 1024 * 1024)
                throw new InvalidDataException(UserError("store.invalid_composition"));
            try
            {
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                LibraryData verified = Deserialize(File.ReadAllText(temporary));
                if (verified == null || verified.version != FormatVersion ||
                    verified.blueprints == null || verified.categories == null ||
                    verified.blueprints.Count != data.blueprints.Count ||
                    verified.categories.Count != data.categories.Count)
                    throw new InvalidDataException(UserError("store.verify_file_failed"));
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Blueprint blueprint in verified.blueprints)
                    if (!Valid(blueprint) || !ids.Add(blueprint.id))
                        throw new InvalidDataException(UserError("store.verify_data_failed"));
                if (File.Exists(path))
                {
                    string backup = path + ".bak";
                    File.Replace(temporary, path, backup, true);
                    try
                    {
                        if (File.Exists(backup)) File.Delete(backup);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogWarning("BuildWorks could not remove blueprint backup: " +
                            exception.Message);
                    }
                }
                else
                {
                    File.Move(temporary, path);
                }
                ++Revision;
                catalogSnapshot = null;
            }
            catch
            {
                try
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("BuildWorks could not remove failed blueprint temp: " +
                        exception.Message);
                }
                throw;
            }
        }

        private bool CanWrite(Blueprint blueprint, out string error)
        {
            error = writeBlockReason;
            if (!string.IsNullOrEmpty(error)) return false;
            if (blueprint != null && data.blueprints.Contains(blueprint)) return true;
            error = UserError("store.blueprint_missing");
            return false;
        }

        private bool TryCommit(Action rollback, out string error)
        {
            try
            {
                Save();
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                rollback();
                error = exception.Message;
                return false;
            }
        }

        private static string NormalizeCategory(string value)
        {
            string normalized = (value ?? string.Empty).Trim().ToUpperInvariant();
            return string.Equals(normalized, LegacyDefaultCategory, StringComparison.Ordinal)
                ? DefaultCategory
                : normalized;
        }

        private static string UserError(string key, string detail = null) =>
            LocalizationPrefix + key + (detail == null ? string.Empty : "\t" + detail);

        private static void NormalizeCategories(LibraryData library)
        {
            var categories = new List<string> { DefaultCategory };
            foreach (string raw in library.categories)
            {
                string category = NormalizeCategory(raw);
                if (category.Length > 0 && category.Length <= 24 &&
                    !categories.Contains(category) && categories.Count < MaximumCategories)
                    categories.Add(category);
            }
            library.categories = categories;
            foreach (Blueprint blueprint in library.blueprints)
            {
                if (blueprint == null) continue;
                blueprint.category = NormalizeCategory(blueprint.category);
                if (!categories.Contains(blueprint.category))
                    blueprint.category = DefaultCategory;
                if (!IsFinite(blueprint.previewYaw) || !IsFinite(blueprint.previewPitch) ||
                    !IsFinite(blueprint.previewZoom) || blueprint.previewPitch < -80f ||
                    blueprint.previewPitch > 80f || blueprint.previewZoom < 0.5f ||
                    blueprint.previewZoom > 3f)
                {
                    blueprint.previewYaw = 45f;
                    blueprint.previewPitch = 30f;
                    blueprint.previewZoom = 1f;
                }
            }
        }

        internal static bool Valid(Blueprint blueprint)
        {
            if (blueprint == null || string.IsNullOrWhiteSpace(blueprint.id) ||
                !Guid.TryParseExact(blueprint.id, "N", out _) ||
                string.IsNullOrWhiteSpace(blueprint.name) ||
                string.IsNullOrWhiteSpace(blueprint.prefabName) ||
                blueprint.parts == null || blueprint.parts.Count < 2 ||
                blueprint.parts.Count > MaximumParts || blueprint.anchors == null ||
                blueprint.anchors.Count > MaximumAnchors || blueprint.groups == null ||
                blueprint.groups.Count > MaximumGroups ||
                !ValidDocument(blueprint.parts, blueprint.groups, blueprint.supports) ||
                !ValidPlacementSupport(blueprint.supports, blueprint.placementSupportId) ||
                !ValidCustomSnapPoints(blueprint.customSnapPoints) ||
                !ValidPrimaryPart(blueprint.parts, blueprint.primaryPartId) ||
                !ValidPrimaryGroup(blueprint.parts, blueprint.groups, blueprint.primaryGroupId))
                return false;
            foreach (VectorData anchor in blueprint.anchors)
            {
                if (!Valid(anchor)) return false;
            }
            return true;
        }

        private static bool ValidCustomSnapPoints(IReadOnlyList<VectorData> points)
        {
            if (points == null) return true;
            if (points.Count > MaximumAnchors) return false;
            foreach (VectorData point in points) if (!Valid(point)) return false;
            return true;
        }

        private static bool Valid(Part part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.prefabName) ||
                string.IsNullOrWhiteSpace(part.stableId) ||
                string.IsNullOrWhiteSpace(part.displayName) ||
                !Valid(part.position) || !Valid(part.rotation) || !ValidScale(part.scale) ||
                !Geometry.BlueprintEditorPart.IsValidSignText(part.signText)) return false;
            float length = part.rotation.x * part.rotation.x +
                part.rotation.y * part.rotation.y +
                part.rotation.z * part.rotation.z +
                part.rotation.w * part.rotation.w;
            return length > 0.9f && length < 1.1f;
        }

        private static bool ValidDocument(
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            IReadOnlyList<Support> supports = null)
        {
            if (parts == null || groups == null || parts.Count > MaximumParts ||
                groups.Count > MaximumGroups) return false;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            if (supports != null)
            {
                if (supports.Count > Geometry.BlueprintEditorDocument.MaximumSupports) return false;
                foreach (Support support in supports)
                    if (!Valid(support) || !ids.Add(support.stableId)) return false;
            }
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (Group group in groups)
            {
                if (group == null || string.IsNullOrWhiteSpace(group.stableId) ||
                    string.IsNullOrWhiteSpace(group.name) || !ids.Add(group.stableId))
                    return false;
                groupIds.Add(group.stableId);
            }
            var parents = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Group group in groups)
            {
                if (group.parentGroupId != null &&
                    !groupIds.Contains(group.parentGroupId)) return false;
                parents[group.stableId] = group.parentGroupId;
            }
            foreach (Group group in groups)
            {
                var ancestry = new HashSet<string>(StringComparer.Ordinal);
                string groupId = group.stableId;
                while (groupId != null)
                {
                    if (!ancestry.Add(groupId)) return false;
                    groupId = parents[groupId];
                }
            }
            foreach (Part part in parts)
            {
                if (!Valid(part) || !ids.Add(part.stableId) ||
                    part.parentGroupId != null && !groupIds.Contains(part.parentGroupId))
                    return false;
            }
            foreach (Group group in groups)
                if (!ValidGroupPivot(parts, groups, group)) return false;
            return true;
        }

        private static bool ValidGroupPivot(
            IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups,
            Group group)
        {
            if (string.IsNullOrWhiteSpace(group.pivotPartId)) return true;
            Part pivot = null;
            foreach (Part part in parts)
                if (part != null && part.stableId == group.pivotPartId)
                {
                    pivot = part;
                    break;
                }
            string groupId = pivot?.parentGroupId;
            while (!string.IsNullOrEmpty(groupId))
            {
                if (groupId == group.stableId) return true;
                Group parent = null;
                foreach (Group candidate in groups)
                    if (candidate?.stableId == groupId) { parent = candidate; break; }
                groupId = parent?.parentGroupId;
            }
            return false;
        }

        private static bool ValidPrimaryPart(IReadOnlyList<Part> parts, string primaryPartId)
        {
            if (string.IsNullOrWhiteSpace(primaryPartId)) return true;
            foreach (Part part in parts)
                if (part != null && string.Equals(
                    part.stableId, primaryPartId, StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool ValidPrimaryGroup(IReadOnlyList<Part> parts,
            IReadOnlyList<Group> groups, string primaryGroupId)
        {
            if (string.IsNullOrWhiteSpace(primaryGroupId)) return true;
            bool found = false;
            foreach (Group group in groups)
                if (group != null && group.stableId == primaryGroupId) { found = true; break; }
            if (!found) return false;
            foreach (Part part in parts)
            {
                string groupId = part?.parentGroupId;
                while (!string.IsNullOrEmpty(groupId))
                {
                    if (groupId == primaryGroupId) return true;
                    Group group = null;
                    foreach (Group candidate in groups)
                        if (candidate?.stableId == groupId) { group = candidate; break; }
                    groupId = group?.parentGroupId;
                }
            }
            return false;
        }

        internal static int PrimaryPartIndex(Blueprint blueprint)
        {
            if (string.IsNullOrEmpty(blueprint?.primaryPartId)) return -1;
            for (int index = 0; index < blueprint.parts.Count; ++index)
                if (string.Equals(blueprint.parts[index].stableId, blueprint.primaryPartId,
                    StringComparison.Ordinal)) return index;
            return -1;
        }

        internal static bool HasExplicitFrame(Blueprint blueprint) =>
            PrimaryPartIndex(blueprint) >= 0 || !string.IsNullOrEmpty(blueprint?.primaryGroupId);

        internal static int FramePartIndex(Blueprint blueprint)
        {
            int primary = PrimaryPartIndex(blueprint);
            if (primary >= 0) return primary;
            if (blueprint?.parts == null) return 0;
            for (int index = 0; index < blueprint.parts.Count; ++index)
                if (IsFramePart(blueprint, index)) return index;
            return 0;
        }

        internal static bool IsFramePart(Blueprint blueprint, int partIndex)
        {
            if (blueprint?.parts == null || partIndex < 0 || partIndex >= blueprint.parts.Count)
                return false;
            int primary = PrimaryPartIndex(blueprint);
            if (primary >= 0) return partIndex == primary;
            if (string.IsNullOrEmpty(blueprint.primaryGroupId)) return true;
            string groupId = blueprint.parts[partIndex].parentGroupId;
            while (!string.IsNullOrEmpty(groupId))
            {
                if (groupId == blueprint.primaryGroupId) return true;
                Group group = blueprint.groups?.Find(candidate => candidate.stableId == groupId);
                groupId = group?.parentGroupId;
            }
            return false;
        }

        private static string PrimaryPrefabName(IReadOnlyList<Part> parts, string primaryPartId)
        {
            if (!string.IsNullOrEmpty(primaryPartId))
                foreach (Part part in parts)
                    if (part != null && string.Equals(part.stableId, primaryPartId,
                        StringComparison.Ordinal)) return part.prefabName;
            return parts[0].prefabName;
        }

        private static List<Part> PrepareParts(IReadOnlyList<Part> source)
        {
            List<Part> result = CloneParts(source);
            foreach (Part part in result)
            {
                if (part == null) continue;
                if (string.IsNullOrWhiteSpace(part.stableId))
                    part.stableId = Guid.NewGuid().ToString("N");
                if (string.IsNullOrWhiteSpace(part.displayName))
                    part.displayName = part.prefabName;
            }
            return result;
        }

        private static bool Valid(Support support)
        {
            if (support == null || !ValidSupportLabel(support.stableId, 128) ||
                !ValidSupportLabel(support.name, 48) || !Valid(support.position) || !Valid(support.rotation))
                return false;
            double length = (double)support.rotation.x * support.rotation.x +
                (double)support.rotation.y * support.rotation.y +
                (double)support.rotation.z * support.rotation.z + (double)support.rotation.w * support.rotation.w;
            return Math.Abs(length - 1) <= 0.0001;
        }

        private static bool ValidSupportLabel(string text, int limit)
        {
            if (string.IsNullOrWhiteSpace(text) || text.Length > limit || text != text.Trim()) return false;
            foreach (char c in text) if (char.IsControl(c) || c == '<' || c == '>') return false;
            return true;
        }

        private static bool ValidPlacementSupport(IReadOnlyList<Support> supports, string id)
        {
            if (id == null) return true;
            if (supports != null)
                foreach (Support support in supports)
                    if (support != null && support.stableId == id) return true;
            return false;
        }

        private static bool Valid(VectorData value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) &&
            Mathf.Abs(value.x) <= 512f && Mathf.Abs(value.y) <= 512f &&
            Mathf.Abs(value.z) <= 512f;

        private static bool Valid(QuaternionData value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && IsFinite(value.w);

        internal static bool ValidScale(VectorData value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) &&
            value.x >= 0.01f && value.y >= 0.01f && value.z >= 0.01f &&
            value.x <= 4f && value.y <= 4f && value.z <= 4f;

        private static bool IsFinite(float value) =>
            !float.IsNaN(value) && !float.IsInfinity(value);

        private static string Serialize(LibraryData value)
        {
            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(LibraryData));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, value);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static LibraryData Deserialize(string json)
        {
            DataContractJsonSerializer serializer =
                new DataContractJsonSerializer(typeof(LibraryData));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                return serializer.ReadObject(stream) as LibraryData;
        }

        // These lower-case field names are the persisted JSON schema. Renaming
        // them is a file-format migration, not a code-style cleanup.
        [DataContract]
        internal sealed class LibraryData
        {
            [DataMember(Order = 0)]
            public int version = FormatVersion;
            [DataMember(Order = 1)]
            public List<Blueprint> blueprints = new List<Blueprint>();
            [DataMember(Order = 2)]
            public List<string> categories = new List<string> { DefaultCategory };
        }

        [DataContract]
        internal sealed class Blueprint
        {
            // Transient read-only import diagnostics; not persisted library fields.
            internal byte[] importPreview;
            public string importSource;
            public string importError;
            public int importPartCount;
            internal bool IsExternal => importSource != null;
            [DataMember(Order = 0)]
            public string id;
            [DataMember(Order = 1)]
            public string name;
            [DataMember(Order = 2)]
            public string prefabName;
            [DataMember(Order = 3)]
            public List<Part> parts = new List<Part>();
            [DataMember(Order = 4)]
            public List<VectorData> anchors = new List<VectorData>();
            [DataMember(Order = 5)]
            public string category = DefaultCategory;
            [DataMember(Order = 6)]
            public float previewYaw = 45f;
            [DataMember(Order = 7)]
            public float previewPitch = 30f;
            [DataMember(Order = 8)]
            public float previewZoom = 1f;
            [DataMember(Order = 9)]
            public List<Group> groups = new List<Group>();
            [DataMember(Order = 10, EmitDefaultValue = false)]
            public string primaryPartId;
            [DataMember(Order = 11, EmitDefaultValue = false)]
            public string primaryGroupId;
            [DataMember(Order = 12, EmitDefaultValue = false)]
            public List<VectorData> customSnapPoints;
            [DataMember(Order = 13, EmitDefaultValue = false)]
            public List<Support> supports;
            [DataMember(Order = 14, EmitDefaultValue = false)]
            public string placementSupportId;
        }

        [DataContract]
        internal sealed class Support
        {
            [DataMember(Order = 0)] public string stableId;
            [DataMember(Order = 1)] public string name;
            [DataMember(Order = 2)] public VectorData position;
            [DataMember(Order = 3)] public QuaternionData rotation;
        }

        [DataContract]
        internal sealed class Part
        {
            [DataMember(Order = 0)]
            public string prefabName;
            [DataMember(Order = 1)]
            public VectorData position;
            [DataMember(Order = 2)]
            public QuaternionData rotation;
            [DataMember(Order = 3)]
            public string stableId;
            [DataMember(Order = 4)]
            public string displayName;
            [DataMember(Order = 5, EmitDefaultValue = false)]
            public string parentGroupId;
            [DataMember(Order = 6)]
            public bool editorVisible = true;
            [DataMember(Order = 7)]
            public bool editorLocked;
            [DataMember(Order = 8)]
            public VectorData scale = new VectorData { x = 1f, y = 1f, z = 1f };
            [DataMember(Order = 9, EmitDefaultValue = false)]
            public string signText;
        }

        [DataContract]
        internal sealed class Group
        {
            [DataMember(Order = 0)]
            public string stableId;
            [DataMember(Order = 1)]
            public string name;
            [DataMember(Order = 2)]
            public bool editorVisible = true;
            [DataMember(Order = 3)]
            public bool editorLocked;
            [DataMember(Order = 4, EmitDefaultValue = false)]
            public string parentGroupId;
            [DataMember(Order = 5, EmitDefaultValue = false)]
            public string pivotPartId;
        }

        [DataContract]
        internal struct VectorData
        {
            [DataMember(Order = 0)]
            public float x;
            [DataMember(Order = 1)]
            public float y;
            [DataMember(Order = 2)]
            public float z;

            public VectorData(Vector3 value)
            {
                x = value.x;
                y = value.y;
                z = value.z;
            }

            public Vector3 ToVector3() => new Vector3(x, y, z);
        }

        [DataContract]
        internal struct QuaternionData
        {
            [DataMember(Order = 0)]
            public float x;
            [DataMember(Order = 1)]
            public float y;
            [DataMember(Order = 2)]
            public float z;
            [DataMember(Order = 3)]
            public float w;

            public QuaternionData(Quaternion value)
            {
                value = value.normalized;
                x = value.x;
                y = value.y;
                z = value.z;
                w = value.w;
            }

            public Quaternion ToQuaternion() => new Quaternion(x, y, z, w).normalized;
        }
    }
}
