using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
        internal const int MaximumCustomSnapPoints = Geometry.BlueprintEditorDocument.MaximumCustomSnapPoints;
        internal const int InteractivePartBudget = 512;
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
        internal const int EditorPartBudget = MaximumParts;
#else
        internal const int EditorPartBudget = InteractivePartBudget;
#endif
        internal static string EditorError(int count) => count > EditorPartBudget
            ? UserError("import.interactive_limit", count + "/" + EditorPartBudget) : null;
        internal static string InteractiveError(int count) => count > InteractivePartBudget
            ? UserError("import.interactive_limit", count + "/" + InteractivePartBudget) : null;
        private const int MaximumLibraryParts = 100000;
        internal const string DefaultCategory = "OTHER";
        private const string LegacyDefaultCategory = "ПРОЧЕЕ";
        private const string LocalizationPrefix = "$buildworks_";
        private const int FormatVersion = 22;
        private const int MaximumBlueprints = 32;
        private const int MaximumCategories = 12;
        private const int MaximumAnchors = 512;
        private const int MaximumGroups = 64;
        private readonly string path;
        private LibraryData data = new LibraryData();
        private List<Blueprint> externalBlueprints;
        private IReadOnlyList<Blueprint> catalogSnapshot;
        private IReadOnlyList<Blueprint> fullCatalogSnapshot;
        private HashSet<string> hiddenSourceSnapshot;
        private IReadOnlyList<Blueprint> preparedExternalBlueprints;
        private Task<List<Blueprint>> importTask;
        private readonly bool backgroundImports;
        private Task<List<Blueprint>> preparationTask;
        private int importGeneration;
        private int preparationGeneration;
        private Func<string, VectorData?> importPrefabScale;
        private Func<string, PrefabMetadata> prefabMetadata;
        private Func<string, int> importPrefabHash;
        private Func<int> importWorldLevel;
        private Func<string, bool> importMaterialSeedSupport;
        private int? preparedWorldLevel;
        private Func<object> importHostContext;
        private object preparedHostContext;
        internal int Revision { get; private set; }
        internal bool CatalogLoading => importTask != null || preparationTask != null;
        internal string ImportError { get; private set; }
        internal string ImportResultPath { get; private set; }
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
            this.backgroundImports = backgroundImports;
            Load();
            if (backgroundImports)
                importTask = Task.Run(() => ExternalBlueprintLibrary.Load(ImportDirectory));
        }

        public IReadOnlyList<Blueprint> All() => data.blueprints;

        // Called on the Unity thread only; prefabHash must be a pure stable string hash.
        internal void ConfigureImportScale(Func<string, VectorData?> resolve, Func<object> context = null,
            Func<string, int> prefabHash = null, Func<int> worldLevel = null, Func<string, bool> supportsMaterialSeed = null)
        {
            importPrefabScale = resolve;
            importHostContext = context;
            importPrefabHash = prefabHash;
            importWorldLevel = worldLevel;
            importMaterialSeedSupport = supportsMaterialSeed;
            catalogSnapshot = null;
            preparedExternalBlueprints = null;
            ++importGeneration;
        }

        internal IReadOnlyList<Blueprint> CatalogAll(bool includeHidden = false)
        {
            object hostContext = importHostContext?.Invoke();
            int? worldLevel = importWorldLevel?.Invoke();
            if (!ReferenceEquals(hostContext, preparedHostContext) || preparedWorldLevel != worldLevel)
            { preparedHostContext = hostContext; preparedWorldLevel = worldLevel; catalogSnapshot = null; preparedExternalBlueprints = null; ++importGeneration; }
            if (importTask != null && importTask.IsCompleted)
            {
                try { externalBlueprints = importTask.GetAwaiter().GetResult(); ImportError = null; }
                catch (Exception exception) { ImportError = exception.Message; }
                importTask = null;
                externalBlueprints = externalBlueprints ?? new List<Blueprint>();
                catalogSnapshot = null;
                preparedExternalBlueprints = null;
                ++importGeneration;
            }
            if (externalBlueprints == null)
            {
                if (importTask != null) return data.blueprints;
                externalBlueprints = ExternalBlueprintLibrary.Load(ImportDirectory);
            }
            if (catalogSnapshot != null) return includeHidden ? fullCatalogSnapshot : catalogSnapshot;
            if (preparedExternalBlueprints == null)
            {
                if (backgroundImports)
                {
                    if (preparationTask != null)
                    {
                        if (!preparationTask.IsCompleted) return data.blueprints;
                        try
                        {
                            var prepared = preparationTask.GetAwaiter().GetResult();
                            if (preparationGeneration == importGeneration)
                                preparedExternalBlueprints = prepared;
                        }
                        catch (Exception exception)
                        {
                            if (preparationGeneration == importGeneration)
                            { ImportError = exception.Message; preparedExternalBlueprints = new List<Blueprint>(); }
                        }
                        preparationTask = null;
                    }
                    if (preparedExternalBlueprints == null)
                    {
                        // Snapshot Unity values once; conversion of the library never touches Unity.
                        var scales = externalBlueprints.SelectMany(source => source.parts).Select(part => part.prefabName)
                            .Distinct(StringComparer.Ordinal).ToDictionary(name => name, name => importPrefabScale?.Invoke(name), StringComparer.Ordinal);
                        var sources = externalBlueprints;
                        var materialSupport = scales.Keys.ToDictionary(name => name,
                            name => importMaterialSeedSupport?.Invoke(name) == true, StringComparer.Ordinal);
                        var hash = importPrefabHash;
                        preparationGeneration = importGeneration;
                        preparationTask = Task.Run(() => sources.Select(source => ExternalBlueprintLibrary.PrepareForHost(
                            source, name => scales.TryGetValue(name, out var scale) ? scale : null, hash, worldLevel,
                            name => materialSupport.TryGetValue(name, out bool supported) && supported)).ToList());
                        return data.blueprints;
                    }
                }
                else preparedExternalBlueprints = externalBlueprints.Select(source => ExternalBlueprintLibrary.PrepareForHost(source, importPrefabScale, importPrefabHash, worldLevel, importMaterialSeedSupport)).ToList();
            }
            var result = new List<Blueprint>(data.blueprints);
            result.AddRange(preparedExternalBlueprints);
            fullCatalogSnapshot = result;
            catalogSnapshot = result.Where(blueprint => !IsHidden(blueprint)).ToArray();
            return includeHidden ? fullCatalogSnapshot : catalogSnapshot;
        }

        internal bool IsHidden(Blueprint blueprint) => blueprint?.IsExternal == true &&
            (hiddenSourceSnapshot ?? (hiddenSourceSnapshot = new HashSet<string>(data.hiddenSources, StringComparer.Ordinal)))
                .Contains(blueprint.importSource);

        internal bool TrySetHidden(Blueprint blueprint, bool hidden, out string error)
        {
            error = writeBlockReason;
            if (error != null) return false;
            if (blueprint?.IsExternal != true || !CatalogAll(true).Any(candidate =>
                candidate.id == blueprint.id && candidate.importSource == blueprint.importSource))
            { error = UserError("store.blueprint_missing"); return false; }
            bool previous = IsHidden(blueprint);
            if (previous == hidden) return true;
            if (hidden && data.hiddenSources.Count >= ExternalBlueprintLibrary.MaximumFiles)
            { error = UserError("import.library_limit"); return false; }
            var before = new List<string>(data.hiddenSources);
            if (hidden) data.hiddenSources.Add(blueprint.importSource);
            else data.hiddenSources.Remove(blueprint.importSource);
            hiddenSourceSnapshot = null;
            return TryCommit(() => { data.hiddenSources = before; hiddenSourceSnapshot = null; }, out error);
        }

        private static bool ValidHiddenSources(List<string> sources) => sources != null &&
            sources.Count <= ExternalBlueprintLibrary.MaximumFiles &&
            sources.All(source => !string.IsNullOrWhiteSpace(source) && source.Length <= 32768) &&
            sources.Distinct(StringComparer.Ordinal).Count() == sources.Count;

        internal void ConfigurePrefabMetadata(Func<string, PrefabMetadata> resolve) => prefabMetadata = resolve;

        private void CaptureMetadata(IEnumerable<Part> parts)
        {
            if (prefabMetadata == null) return;
            var snapshot = new Dictionary<string, PrefabMetadata>(StringComparer.Ordinal);
            foreach (Part part in parts)
            {
                if (part == null || string.IsNullOrWhiteSpace(part.prefabName)) continue;
                if (!snapshot.TryGetValue(part.prefabName, out PrefabMetadata metadata))
                    snapshot.Add(part.prefabName, metadata = prefabMetadata(part.prefabName));
                if (metadata != null)
                {
                    if (metadata.ToGeometry().PrefabId != part.prefabName) throw new ArgumentException("Prefab metadata identity mismatch.");
                    var fresh = metadata.Copy();
                    if (fresh.sourceId == null && part.metadata != null)
                    { fresh.sourceId = part.metadata.sourceId; fresh.sourceVersion = part.metadata.sourceVersion; }
                    part.metadata = fresh;
                }
            }
        }

        internal bool TryGetOrCreatePlacementCopy(Blueprint source, out Blueprint copy, out string error)
        {
            copy = null; error = null;
            if (source == null || !source.IsExternal || source.importError != null ||
                string.IsNullOrEmpty(source.id))
            { error = UserError("import.fields"); return false; }
            if (source.importAbsoluteScale)
            { error = UserError("import.scale_pending"); return false; }
            copy = data.blueprints.FirstOrDefault(candidate => candidate.placementImportId == source.id);
            if (copy != null) return true;
            var detached = ExternalBlueprintLibrary.DetachedCopy(source);
            detached.placementImportId = source.id;
            return TrySaveDocument(detached.name, detached.category, detached.parts, detached.groups,
                detached.anchors, detached.primaryPartId, detached.primaryGroupId, detached.supports,
                detached.placementSupportId, out copy, out error, detached.customSnapPoints, detached);
        }

        internal string ExchangeDirectory => Path.GetDirectoryName(path);
        internal string ImportDirectory => Path.Combine(ExchangeDirectory, "imports");
        internal string ExportDirectory => Path.Combine(ExchangeDirectory, "exports");

        internal static string ValidateExchangeDirectory(string directory)
        {
            string full = Path.GetFullPath(directory ?? "");
            for (var current = new DirectoryInfo(full); current != null; current = current.Parent)
                if (current.Exists && (current.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("$buildworks_import.path");
            return full;
        }

        internal static string[] BrowseExchangeDirectory(string directory)
        {
            string full = ValidateExchangeDirectory(directory);
            var entries = Directory.EnumerateFileSystemEntries(full)
                .Take(2001).ToArray();
            if (entries.Length > 2000) throw new InvalidDataException("$buildworks_import.library_limit");
            return entries.Where(entry => (File.GetAttributes(entry) & FileAttributes.ReparsePoint) == 0 &&
                (Directory.Exists(entry) || ExternalBlueprintLibrary.Supported(entry) ||
                    entry.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
                .OrderBy(entry => Directory.Exists(entry) ? 0 : 1)
                .ThenBy(entry => Path.GetFileName(entry), StringComparer.CurrentCultureIgnoreCase).ToArray();
        }

        internal static string ExportName(string name)
        {
            string safe = new string((name ?? "blueprint").Select(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '_' : c).ToArray());
            safe = safe.Trim().TrimEnd('.');
            return "blueprint_" + (safe.Length > 96 ? safe.Substring(0, 96) : safe);
        }

        internal bool TryImportFile(string source, out string error)
        {
            try { return TryImportReviewed(ReviewImportFile(source, null), false, false, null, out error); }
            catch (Exception exception) { error = exception.Message; return false; }
        }

        internal sealed class ImportReview
        {
            internal string Source;
            internal byte[] Bytes;
            internal List<Blueprint> Documents;
            internal ContentsSummary Contents;
        }

        // Unity thread only; workers receive the resulting pure name lookup.
        internal Func<string, bool> MaterialSeedSupportSnapshot(IEnumerable<Blueprint> documents)
        {
            var supported = documents.SelectMany(document => document.parts).Select(part => part.prefabName)
                .Distinct(StringComparer.Ordinal).ToDictionary(name => name, SupportsMaterialSeed, StringComparer.Ordinal);
            return name => supported.TryGetValue(name, out bool value) && value;
        }

        internal bool SupportsMaterialSeed(string name) =>
            (importHostContext == null || importHostContext() != null) && importMaterialSeedSupport?.Invoke(name) == true;

        // Refine conservative file inspection with the current host before presenting choices.
        internal ImportReview PrepareImportReview(ImportReview review, Func<string, int> prefabHash)
        {
            var supports = MaterialSeedSupportSnapshot(review.Documents);
            var result = new ImportReview
            {
                Source = review.Source,
                Bytes = review.Bytes,
                Contents = new ContentsSummary(),
                Documents = review.Documents.Select(document => ExternalBlueprintLibrary.PrepareForHost(document,
                    importPrefabScale, prefabHash, importWorldLevel?.Invoke(), supports)).ToList()
            };
            foreach (Blueprint document in result.Documents)
                result.Contents.Add(InspectContents(document, prefabHash, supports));
            return result;
        }

        private static byte[] ReadImportBytes(string source)
        {
            ValidateExchangeDirectory(Path.GetDirectoryName(source));
            if ((File.GetAttributes(source) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("$buildworks_import.path");
            using (var input = File.OpenRead(source))
            using (var output = new MemoryStream())
            {
                var buffer = new byte[8192]; int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (output.Length + read > 64L * 1024 * 1024)
                        throw new InvalidDataException("$buildworks_import.file_limit");
                    output.Write(buffer, 0, read);
                }
                return output.ToArray();
            }
        }

        // Pure file inspection: no library writes and no Unity prefab access.
        internal static ImportReview ReviewImportFile(string source, Func<string, int> prefabHash, int? worldLevel = null)
        {
            source = Path.GetFullPath((source ?? "").Trim().Trim('"'));
            byte[] bytes = ReadImportBytes(source);
            string folder = Path.Combine(Path.GetTempPath(), "BuildWorks-review-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string temporary = Path.Combine(folder, Path.GetFileName(source));
            try
            {
                File.WriteAllBytes(temporary, bytes);
                var documents = ExternalBlueprintLibrary.LoadFile(temporary);
                if (documents.Count == 0) throw new InvalidDataException("$buildworks_import.format");
                if (2 + documents.Count + documents.Count(document => ExternalBlueprintLibrary.ValidPreview(document.importPreview)) >
                    ExternalBlueprintLibrary.MaximumFiles)
                    throw new InvalidDataException("$buildworks_import.library_limit");
                var summary = new ContentsSummary();
                foreach (Blueprint document in documents)
                {
                    if (prefabHash != null && document.importError == null)
                    {
                        var evidenceIndex = Geometry.BlueprintSourceDocument.Index(document.sourceDocuments?.Select(evidence => evidence.ToGeometry()));
                        Dictionary<int, Type> ordinaryStateFields = null;
                        foreach (Part part in document.parts)
                            if (part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var evidence))
                                ExternalBlueprintLibrary.ConvertKnownState(part, evidence, prefabHash, worldLevel, null, ref ordinaryStateFields);
                    }
                    var contents = InspectContents(document, prefabHash); // Reject every blocked member, not just empty archives.
                    summary.Add(contents);
                }
                return new ImportReview { Source = source, Bytes = bytes, Documents = documents, Contents = summary };
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
                Directory.Delete(folder);
            }
        }

        // Called on the Unity thread: normalize against installed prefab bases before the file worker.
        internal bool TryImportReviewed(ImportReview review, bool clearStorage, bool clearMounts,
            Func<string, int> prefabHash, out string error, bool omitArmorState = false, MissingPartsReview omitMissing = null)
        {
            error = null;
            if (CatalogLoading) { error = UserError("exchange.loading"); return false; }
            CatalogAll();
            try
            {
                if (review == null) throw new InvalidDataException("$buildworks_import.format");
                if (omitMissing != null) ValidateMissingReview(review.Documents, omitMissing, prefabHash);
                var prepared = review.Documents.Select(document => ExternalBlueprintLibrary.PrepareForHost(document, importPrefabScale, importPrefabHash, importWorldLevel?.Invoke(), importMaterialSeedSupport)).ToArray();
                if (prepared.Any(document => document.importError != null))
                    throw new InvalidDataException(prepared.First(document => document.importError != null).importError);
                if (omitMissing != null)
                    prepared = prepared.Select(document => CopyWithoutMissing(document, omitMissing.PrefabIds)).ToArray();
                CaptureMetadata(prepared.SelectMany(document => document.parts));
                string target = Path.Combine(ImportDirectory, "import_" + Guid.NewGuid().ToString("N") + ".zip");
                ImportError = null;
                ImportResultPath = null;
                importTask = Task.Run(() =>
                {
                    if (!ReadImportBytes(review.Source).SequenceEqual(review.Bytes))
                        throw new InvalidDataException("$buildworks_exchange.source_changed");
                    ValidateExchangeDirectory(ImportDirectory);
                    Directory.CreateDirectory(ImportDirectory);
                    var validationReads = new Dictionary<string, Blueprint>(StringComparer.Ordinal);
                    var catalog = ExternalBlueprintLibrary.Load(ImportDirectory, validationReads: validationReads);
                    string temporary = Path.Combine(ImportDirectory, Guid.NewGuid().ToString("N") + ".tmp.zip");
                    try
                    {
                        using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        using (var zip = new ZipArchive(output, ZipArchiveMode.Create))
                        {
                            WriteEntry("original/source.bin", review.Bytes); // Inert exact original, never parsed as a second blueprint.
                            WriteEntry("original/name.txt", Encoding.UTF8.GetBytes(Path.GetFileName(review.Source)));
                            for (int i = 0; i < prepared.Length; ++i)
                            {
                                var copy = CopyWithContentsPolicy(prepared[i], clearStorage, clearMounts, prefabHash, omitArmorState);
                                string name = "document_" + i + ".buildworks.json";
                                WriteEntry(name, BlueprintExchangeFormat.Encode(copy));
                                if (!clearStorage && !clearMounts && !omitArmorState && omitMissing == null && ExternalBlueprintLibrary.ValidPreview(prepared[i].importPreview))
                                    WriteEntry(Path.ChangeExtension(name, ".png"), prepared[i].importPreview);
                            }
                            void WriteEntry(string name, byte[] data)
                            { using (var entry = zip.CreateEntry(name).Open()) entry.Write(data, 0, data.Length); }
                        }
                        var validated = ExternalBlueprintLibrary.LoadFile(temporary, Path.GetFileName(target));
                        if (validated.Count != prepared.Length || validated.Any(document => document.importError != null))
                            throw new InvalidDataException("$buildworks_import.format");
                        if (catalog.Count + validated.Count > ExternalBlueprintLibrary.MaximumFiles)
                            throw new InvalidDataException("$buildworks_import.library_limit");
                        // Validate the exact future sort order and shared read budget, not just this ZIP in isolation.
                        var candidate = ExternalBlueprintLibrary.Load(ImportDirectory, temporary, Path.GetFileName(target), validationReads);
                        var validCopies = candidate.Where(document => document.importError == null)
                            .GroupBy(document => document.id).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                        if (candidate.Count != catalog.Count + validated.Count ||
                            catalog.Where(document => document.importError == null).Concat(validated).GroupBy(document => document.id)
                                .Any(group => !validCopies.TryGetValue(group.Key, out int count) || count < group.Count()))
                            throw new InvalidDataException("$buildworks_import.library_limit");
                        File.Move(temporary, target); // The whole reviewed archive becomes visible at once; never overwrite.
                        ImportResultPath = target;
                        return candidate;
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

        internal bool TryExportFile(Blueprint blueprint, out string exportedPath, out string error) =>
            TryExportFile(blueprint, ExportDirectory, "blueprint_" + Guid.NewGuid().ToString("N"), false, out exportedPath, out error);

        internal bool TryExportFile(Blueprint blueprint, string directory, string fileName, bool archive,
            out string exportedPath, out string error, bool clearStorage = false, bool clearMounts = false,
            Func<string, int> prefabHash = null, bool omitArmorState = false, MissingPartsReview omitMissing = null)
        {
            exportedPath = null; error = null; string temporary = null;
            try
            {
                if (blueprint == null || !string.IsNullOrEmpty(blueprint.importError))
                    throw new InvalidDataException(blueprint?.importError ?? "$buildworks_import.format");
                if (omitMissing != null)
                {
                    ValidateMissingReview(new[] { blueprint }, omitMissing, prefabHash);
                    blueprint = CopyWithoutMissing(ExternalBlueprintLibrary.PrepareForHost(blueprint,
                        importPrefabScale, importPrefabHash, importWorldLevel?.Invoke(), importMaterialSeedSupport), omitMissing.PrefabIds);
                }
                blueprint = blueprint.ImportCopy();
                CaptureMetadata(blueprint.parts);
                Blueprint prepared = clearStorage || clearMounts || omitArmorState
                    ? CopyWithContentsPolicy(blueprint, clearStorage, clearMounts, prefabHash, omitArmorState)
                    : blueprint.IsExternal ? ExternalBlueprintLibrary.DetachedCopy(blueprint) : blueprint;
                byte[] bytes = BlueprintExchangeFormat.Encode(prepared);
                BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(bytes));
                directory = ValidateExchangeDirectory(directory);
                if (string.IsNullOrWhiteSpace(fileName) || fileName.Length > 120 || fileName != fileName.Trim() ||
                    fileName.EndsWith(".", StringComparison.Ordinal) || fileName.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c)) ||
                    fileName == "." || fileName == "..")
                    throw new InvalidDataException("$buildworks_exchange.invalid_name");
                string target = Path.Combine(directory, fileName + (archive ? ".zip" : ".buildworks.json"));
                if (File.Exists(target)) throw new IOException(UserError("exchange.exists", target));
                Directory.CreateDirectory(directory);
                temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    if (!archive) output.Write(bytes, 0, bytes.Length);
                    else using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
                    using (var entry = zip.CreateEntry(fileName + ".buildworks.json").Open())
                        entry.Write(bytes, 0, bytes.Length);
                }
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

        internal sealed class ContentsSummary
        {
            internal int Storages, Mounts, Unknown, UnreadableStorages, UnreadableMounts, AlreadyCleared;
            internal long StoredItems;
            internal int ArmorStates;
            internal readonly SortedDictionary<string, int> ArmorStateSources = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal int RemainingUnknown(bool omitArmorState) => Unknown - (omitArmorState ? ArmorStates : 0);
            internal bool CanClearStorage => Unknown == UnreadableMounts;
            internal bool CanClearMounts => Unknown == UnreadableStorages;

            internal void Add(ContentsSummary other)
            {
                Storages += other.Storages; StoredItems += other.StoredItems; Mounts += other.Mounts; Unknown += other.Unknown;
                UnreadableStorages += other.UnreadableStorages; UnreadableMounts += other.UnreadableMounts;
                AlreadyCleared += other.AlreadyCleared; ArmorStates += other.ArmorStates;
                foreach (var entry in other.ArmorStateSources)
                {
                    ArmorStateSources.TryGetValue(entry.Key, out int count);
                    ArmorStateSources[entry.Key] = count + entry.Value;
                }
            }
        }

        internal sealed class MissingPartsReview
        {
            internal bool Known;
            internal object HostContext;
            internal int Parts, EmptyDocuments;
            internal readonly HashSet<string> PrefabIds = new HashSet<string>(StringComparer.Ordinal);
            internal readonly SortedDictionary<string, int> Sources = new SortedDictionary<string, int>(StringComparer.Ordinal);
            internal readonly ContentsSummary RetainedContents = new ContentsSummary();
            internal bool CanOmit => Known && Parts > 0 && EmptyDocuments == 0;
        }

        // Unity thread only. Recipe availability is not prefab presence.
        internal MissingPartsReview InspectMissing(IReadOnlyList<Blueprint> documents, Func<string, int> prefabHash)
        {
            var result = new MissingPartsReview { HostContext = importHostContext?.Invoke() };
            if (importPrefabScale == null || importHostContext != null && result.HostContext == null) return result;
            foreach (string prefab in documents.SelectMany(document => document.parts).Select(part => part.prefabName).Distinct(StringComparer.Ordinal))
                if (!importPrefabScale(prefab).HasValue) result.PrefabIds.Add(prefab);
            result.Known = true;
            foreach (Blueprint document in documents)
            {
                int removed = 0;
                var names = (document.sourceDocuments ?? new List<SourceDocument>()).ToDictionary(source => source.id, source => source.name, StringComparer.Ordinal);
                foreach (Part part in document.parts)
                {
                    if (!result.PrefabIds.Contains(part.prefabName)) continue;
                    ++removed;
                    string name = part.sourceDocumentId != null && names.TryGetValue(part.sourceDocumentId, out string sourceName) ? sourceName : document.name;
                    string key = name + " · " + part.prefabName;
                    result.Sources.TryGetValue(key, out int count); result.Sources[key] = count + 1;
                }
                result.Parts += removed;
                if (removed == document.parts.Count) ++result.EmptyDocuments;
                else result.RetainedContents.Add(InspectContents(removed == 0 ? document : CopyWithoutMissing(document, result.PrefabIds), prefabHash, SupportsMaterialSeed));
            }
            return result;
        }

        private void ValidateMissingReview(IReadOnlyList<Blueprint> documents, MissingPartsReview expected, Func<string, int> prefabHash)
        {
            MissingPartsReview current = InspectMissing(documents, prefabHash);
            if (!current.Known) throw new InvalidDataException("$buildworks_exchange.missing_unavailable");
            if (!expected.Known || !ReferenceEquals(current.HostContext, expected.HostContext) ||
                !current.PrefabIds.SetEquals(expected.PrefabIds) || current.Parts != expected.Parts)
                throw new InvalidDataException("$buildworks_exchange.missing_changed");
            if (current.EmptyDocuments > 0) throw new InvalidDataException("$buildworks_exchange.missing_empty");
            if (!current.CanOmit) throw new InvalidDataException("$buildworks_exchange.missing_changed");
        }

        internal static Blueprint CopyWithoutMissing(Blueprint source, ISet<string> missing)
        {
            Blueprint copy = source.ImportCopy();
            copy.groups = CloneGroups(source.groups);
            copy.parts.RemoveAll(part => missing.Contains(part.prefabName));
            if (copy.parts.Count == 0) throw new InvalidDataException("$buildworks_exchange.missing_empty");
            if (copy.parts.Count == source.parts.Count) return copy;
            var ids = new HashSet<string>(copy.parts.Select(part => part.stableId), StringComparer.Ordinal);
            foreach (Group group in copy.groups)
                if (group.pivotPartId != null && !ids.Contains(group.pivotPartId)) group.pivotPartId = null;
            if (copy.primaryPartId != null && !ids.Contains(copy.primaryPartId)) copy.primaryPartId = null;
            if (!ValidPrimaryGroup(copy.parts, copy.groups, copy.primaryGroupId)) copy.primaryGroupId = null;
            copy.prefabName = PrimaryPrefabName(copy.parts, copy.primaryPartId);
            return copy;
        }

        // Read effective contents only. Unknown data is never reported as an empty inventory.
        internal static ContentsSummary InspectContents(Blueprint source, Func<string, int> prefabHash,
            Func<string, bool> supportsMaterialSeed = null)
        {
            var result = new ContentsSummary();
            if (source == null || !string.IsNullOrEmpty(source.importError))
                throw new InvalidDataException(source?.importError ?? "$buildworks_import.format");
            var evidenceIndex = Geometry.BlueprintSourceDocument.Index(source.sourceDocuments?.Select(document => document.ToGeometry()));
            result.Unknown = evidenceIndex.Values.Count(document => document.HasOpaqueSections);
            foreach (Part part in source.parts)
            {
                if (part.materialSeed.HasValue && supportsMaterialSeed?.Invoke(part.prefabName) != true) ++result.Unknown;
                var contents = part.foreignData?.ToGeometry();
                if (contents == null) continue;
                if (contents.Format == Geometry.BlueprintForeignPartData.NativeItemStandFormat && contents.Zdo != null &&
                    !Geometry.BlueprintItemStandContents.TryRead(contents, prefabHash, out _))
                {
                    ++result.Unknown; ++result.UnreadableMounts;
                    if (contents.ContentsCleared) ++result.AlreadyCleared;
                    continue;
                }
                if (contents.ContentsCleared)
                {
                    ++result.AlreadyCleared;
                    if (contents.Format == Geometry.BlueprintForeignPartData.NativeContainerFormat &&
                        !Geometry.BlueprintContainerInventory.TryRead(contents, prefabHash, out _))
                    { ++result.Unknown; ++result.UnreadableStorages; }
                    continue;
                }
                if (contents.Format == Geometry.BlueprintForeignPartData.NativeItemStandFormat) ++result.Mounts;
                else if (contents.Format == Geometry.BlueprintForeignPartData.NativeContainerFormat)
                {
                    if (Geometry.BlueprintContainerInventory.TryRead(contents, prefabHash, out var items))
                    { ++result.Storages; result.StoredItems += items.Sum(item => (long)item.Stack); }
                    else { ++result.Unknown; ++result.UnreadableStorages; }
                }
                else
                {
                    ++result.Unknown;
                    if (part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var evidence) &&
                        ExternalBlueprintLibrary.CanOmitArmorStandState(part, evidence))
                    {
                        ++result.ArmorStates;
                        string key = evidence.Name + " · " + part.prefabName;
                        result.ArmorStateSources.TryGetValue(key, out int count);
                        result.ArmorStateSources[key] = count + 1;
                    }
                }
            }
            return result;
        }

        // Always a private recipe. Retaining never resurrects previously cleared contents.
        internal static Blueprint CopyWithContentsPolicy(Blueprint source, bool clearStorage, bool clearMounts,
            Func<string, int> prefabHash, bool omitArmorState = false)
        {
            if (source == null || !string.IsNullOrEmpty(source.importError))
                throw new InvalidDataException(source?.importError ?? "$buildworks_import.format");
            Blueprint copy = BlueprintExchangeFormat.Decode(Encoding.UTF8.GetString(BlueprintExchangeFormat.Encode(
                source.IsExternal ? ExternalBlueprintLibrary.DetachedCopy(source) : source)));
            if (!clearStorage && !clearMounts && !omitArmorState) return copy;
            var evidenceIndex = Geometry.BlueprintSourceDocument.Index(copy.sourceDocuments?.Select(document => document.ToGeometry()));
            if ((clearStorage || clearMounts) && evidenceIndex.Values.Any(document => document.HasOpaqueSections))
                throw new InvalidDataException("$buildworks_import.object_data");
            foreach (Part part in copy.parts)
            {
                var contents = part.foreignData?.ToGeometry();
                if (contents == null) continue;
                if (omitArmorState && part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var evidence) &&
                    ExternalBlueprintLibrary.CanOmitArmorStandState(part, evidence))
                { part.foreignData = null; continue; }
                if (!clearStorage && !clearMounts) continue;
                if (contents.Format != Geometry.BlueprintForeignPartData.NativeContainerFormat &&
                    contents.Format != Geometry.BlueprintForeignPartData.NativeItemStandFormat)
                    throw new InvalidDataException("$buildworks_import.object_data");
                if (contents.Format == Geometry.BlueprintForeignPartData.NativeContainerFormat ? clearStorage : clearMounts)
                    part.foreignData = ForeignPartData.FromGeometry(contents.WithoutContents(prefabHash));
            }
            return copy;
        }

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
            Blueprint previewTemplate = null, IReadOnlyList<SourceDocument> sourceDocuments = null)
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
            if (parts == null || parts.Count < 1 || parts.Count > MaximumParts ||
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
                previewZoom = previewTemplate?.previewZoom ?? 1f,
                sourceDocuments = CloneSources(sourceDocuments ?? previewTemplate?.sourceDocuments),
                placementImportId = previewTemplate?.placementImportId
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
            List<Part> prepared = CloneParts(parts);
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
                    prepared[index].sourceDocumentId = blueprint.parts[index].sourceDocumentId;
                    prepared[index].sourceLine = blueprint.parts[index].sourceLine;
                    prepared[index].baseScale = prepared[index].baseScale ?? blueprint.parts[index].baseScale;
                    prepared[index].metadata = prepared[index].metadata ?? PrefabMetadata.FromGeometry(blueprint.parts[index].metadata?.ToGeometry());
                    prepared[index].materialSeed = prepared[index].materialSeed ?? blueprint.parts[index].materialSeed;
                    if (!prepared[index].baseScale.HasValue)
                        prepared[index].scaleIsAbsolute = blueprint.parts[index].scaleIsAbsolute;
                    prepared[index].foreignData = CloneForeignData(blueprint.parts[index].foreignData);
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
            out string error, IReadOnlyList<VectorData> customSnapPoints = null,
            IReadOnlyList<SourceDocument> sourceDocuments = null)
        {
            error = null;
            name = (name ?? string.Empty).Trim();
            category = NormalizeCategory(category);
            primaryPartId = string.IsNullOrWhiteSpace(primaryPartId) ? null : primaryPartId.Trim();
            primaryGroupId = string.IsNullOrWhiteSpace(primaryGroupId) ? null : primaryGroupId.Trim();
            customSnapPoints = customSnapPoints ?? blueprint?.customSnapPoints;
            sourceDocuments = sourceDocuments ?? blueprint?.sourceDocuments;
            if (!ValidSources(parts, sourceDocuments))
            { error = UserError("store.invalid_composition"); return false; }
            if (!CanWrite(blueprint, out error) || parts == null || groups == null ||
                anchors == null ||
                parts.Count < 1 || parts.Count > MaximumParts ||
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
            List<SourceDocument> previousSources = blueprint.sourceDocuments;
            string previousPlacementImportId = blueprint.placementImportId;
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
            blueprint.sourceDocuments = CloneSources(sourceDocuments);
            // An edited copy must not replace the external original on the next placement.
            blueprint.placementImportId = null;
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
                blueprint.sourceDocuments = previousSources;
                blueprint.placementImportId = previousPlacementImportId;
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
                    baseScale = part.baseScale,
                    metadata = part.metadata?.Copy(),
                    materialSeed = part.materialSeed,
                    scaleIsAbsolute = part.scaleIsAbsolute,
                    stableId = part.stableId,
                    displayName = part.displayName,
                    parentGroupId = part.parentGroupId,
                    editorVisible = part.editorVisible,
                    editorLocked = part.editorLocked,
                    signText = part.signText,
                    sourceDocumentId = part.sourceDocumentId,
                    sourceLine = part.sourceLine,
                    foreignData = CloneForeignData(part.foreignData)
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
                    allowSupportFields: loaded.version >= 11, allowSourceFields: loaded.version >= 12,
                    allowForeignFields: loaded.version >= 13, allowContentsFields: loaded.version >= 14,
                    allowAbsoluteScaleFields: loaded.version >= 15, allowPlacementImportFields: loaded.version >= 16,
                    allowBaseScaleFields: loaded.version >= 17, allowMetadataFields: loaded.version >= 19,
                    allowMaterialSeedFields: loaded.version >= 20, allowHiddenSources: loaded.version >= 21);
                loaded.blueprints = loaded.blueprints ?? new List<Blueprint>();
                loaded.categories = loaded.categories ?? new List<string>();
                loaded.hiddenSources = loaded.hiddenSources ?? new List<string>();
                if (!ValidHiddenSources(loaded.hiddenSources))
                    throw new InvalidDataException(UserError("store.invalid_composition"));
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
                bool allowSinglePart = loaded.version >= 18;
                bool allowLargeSnapSets = loaded.version >= 22;
                loaded.version = FormatVersion;
                List<Blueprint> valid = new List<Blueprint>();
                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (Blueprint blueprint in loaded.blueprints)
                {
                    if (Valid(blueprint) && (allowSinglePart || blueprint.parts.Count >= 2) &&
                        (allowLargeSnapSets || (blueprint.customSnapPoints?.Count ?? 0) <= 512) && ids.Add(blueprint.id)) valid.Add(blueprint);
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
                    verified.categories.Count != data.categories.Count || !ValidHiddenSources(verified.hiddenSources) ||
                    !verified.hiddenSources.SequenceEqual(data.hiddenSources, StringComparer.Ordinal))
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
                blueprint.parts == null || blueprint.parts.Count < 1 ||
                blueprint.parts.Count > MaximumParts || blueprint.anchors == null ||
                blueprint.anchors.Count > MaximumAnchors || blueprint.groups == null ||
                blueprint.groups.Count > MaximumGroups ||
                !ValidDocument(blueprint.parts, blueprint.groups, blueprint.supports) ||
                !ValidSources(blueprint.parts, blueprint.sourceDocuments) ||
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
            if (points.Count > MaximumCustomSnapPoints) return false;
            foreach (VectorData point in points) if (!Valid(point)) return false;
            return true;
        }

        internal static List<SourceDocument> CloneSources(IReadOnlyList<SourceDocument> sources) => sources?.Select(source =>
            source?.Copy()).ToList();

        internal static bool ValidSources(IReadOnlyList<Part> parts, IReadOnlyList<SourceDocument> sources)
        {
            try
            {
                if (parts == null) return false;
                var index = Geometry.BlueprintSourceDocument.Index(sources?.Select(source => source?.ToGeometry()));
                foreach (Part part in parts)
                {
                    if (part == null) return false;
                    if (part.sourceDocumentId == null)
                    { if (part.sourceLine != 0) return false; }
                    else if (!index.TryGetValue(part.sourceDocumentId, out var evidence) ||
                        !evidence.HasPartAt(part.sourceLine, part.prefabName)) return false;
                }
                return true;
            }
            catch (ArgumentException) { return false; }
        }

        internal static bool HasForeignData(Blueprint blueprint) => blueprint?.parts?.Any(part => part?.foreignData != null || part?.materialSeed != null) == true ||
            blueprint?.sourceDocuments?.Any(source => source.ToGeometry().HasOpaqueSections) == true;

        private static ForeignPartData CloneForeignData(ForeignPartData value) => value == null ? null :
            new ForeignPartData
            {
                format = value.format,
                info = value.info,
                zdo = value.zdo,
                chance = value.chance,
                contentsCleared = value.contentsCleared
            };

        private static bool ValidForeignData(ForeignPartData value)
        {
            try { value?.ToGeometry(); return true; } catch (ArgumentException) { return false; }
        }

        private static bool Valid(Part part)
        {
            if (part == null || string.IsNullOrWhiteSpace(part.prefabName) ||
                string.IsNullOrWhiteSpace(part.stableId) ||
                string.IsNullOrWhiteSpace(part.displayName) ||
                !Valid(part.position) || !Valid(part.rotation) ||
                !(part.scaleIsAbsolute ? PositiveScale(part.scale) : ValidScale(part.scale)) ||
                part.baseScale.HasValue && (part.scaleIsAbsolute || !PositiveScale(part.baseScale.Value) ||
                    !PositiveScale(new VectorData(Vector3.Scale(part.baseScale.Value.ToVector3(), part.scale.ToVector3())))) ||
                part.materialSeed.HasValue && part.materialSeed.Value < 0 ||
                !Geometry.BlueprintEditorPart.IsValidSignText(part.signText) || !ValidForeignData(part.foreignData)) return false;
            try
            {
                if (part.metadata != null)
                {
                    var metadata = part.metadata.ToGeometry();
                    if (metadata.PrefabId != part.prefabName) return false;
                    Vector3 rootScale = part.RootScale(part.metadata.prefabScale.ToVector3());
                    metadata.ValidateEnvelope(new Geometry.Point3(rootScale.x, rootScale.y, rootScale.z));
                }
            }
            catch (ArgumentException) { return false; }
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

        private List<Part> PrepareParts(IReadOnlyList<Part> source)
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
            CaptureMetadata(result);
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
            value.x <= Geometry.BlueprintEditorDocument.MaximumScale &&
            value.y <= Geometry.BlueprintEditorDocument.MaximumScale &&
            value.z <= Geometry.BlueprintEditorDocument.MaximumScale;

        internal static VectorData RelativeScale(VectorData absolute, VectorData prefab)
        {
            if (!PositiveScale(absolute) || !PositiveScale(prefab))
                throw new ArgumentOutOfRangeException(nameof(absolute), "$buildworks_import.transform");
            var relative = Geometry.BlueprintEditorPart.NormalizeScale(new Geometry.Point3(
                absolute.x / prefab.x, absolute.y / prefab.y, absolute.z / prefab.z), nameof(absolute));
            return new VectorData { x = (float)relative.X, y = (float)relative.Y, z = (float)relative.Z };
        }

        internal static bool PositiveScale(VectorData value) =>
            IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z) && value.x > 0 && value.y > 0 && value.z > 0;

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
            [DataMember(Order = 3, EmitDefaultValue = false)]
            public List<string> hiddenSources = new List<string>();
        }

        [DataContract]
        internal sealed class Blueprint
        {
            // Transient read-only import diagnostics; not persisted library fields.
            internal byte[] importPreview;
            internal bool importAbsoluteScale;
            // Parsed lists stay read-only within one validation worker; each read owns its optional preview.
            internal Blueprint ValidationReadCopy() => (Blueprint)MemberwiseClone();
            internal Blueprint ImportCopy()
            {
                var copy = (Blueprint)MemberwiseClone();
                copy.parts = CloneParts(parts);
                copy.sourceDocuments = CloneSources(sourceDocuments);
                return copy;
            }
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
            [DataMember(Order = 15, EmitDefaultValue = false)]
            public List<SourceDocument> sourceDocuments;
            [DataMember(Order = 16, EmitDefaultValue = false)]
            public string placementImportId;
        }

        [DataContract]
        internal sealed class SourceDocument
        {
            private Geometry.BlueprintSourceDocument cached;
            [DataMember(Order = 0)] public string id;
            [DataMember(Order = 1)] public string format;
            [DataMember(Order = 2)] public string name;
            [DataMember(Order = 3)] public string text;

            internal Geometry.BlueprintSourceDocument ToGeometry()
            {
                if (cached != null && cached.Id == id && cached.Format == format && cached.Name == name && cached.Text == text) return cached;
                var source = new Geometry.BlueprintSourceDocument(format, name, text);
                if (source.Id != id) throw new ArgumentException("Source identity does not match its contents.");
                return cached = source;
            }

            internal static SourceDocument FromGeometry(Geometry.BlueprintSourceDocument source) => new SourceDocument
            { id = source.Id, format = source.Format, name = source.Name, text = source.Text };

            // Share only immutable evidence; ToGeometry checks every mutable identity field before reuse.
            internal SourceDocument Copy() => (SourceDocument)MemberwiseClone();
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
        internal sealed class ForeignPartData
        {
            [DataMember(Order = 0)] public string format;
            [DataMember(Order = 1)] public string info;
            [DataMember(Order = 2)] public string zdo;
            [DataMember(Order = 3)] public string chance;
            [DataMember(Order = 4, EmitDefaultValue = false)] public bool contentsCleared;
            internal Geometry.BlueprintForeignPartData ToGeometry() => new Geometry.BlueprintForeignPartData(format, info, zdo, chance, contentsCleared);
            internal static ForeignPartData FromGeometry(Geometry.BlueprintForeignPartData value) => value == null ? null :
                new ForeignPartData
                {
                    format = value.Format,
                    info = value.Info,
                    zdo = value.Zdo,
                    chance = value.Chance,
                    contentsCleared = value.ContentsCleared
                };
        }

        [DataContract]
        internal sealed class PrefabMetadata
        {
            internal PrefabMetadata Copy() => (PrefabMetadata)MemberwiseClone();
            [DataMember(Order = 0)] public int version = 1;
            [DataMember(Order = 1)] public string prefabId;
            [DataMember(Order = 2)] public string name;
            [DataMember(Order = 3)] public string kind;
            [DataMember(Order = 4)] public string hostId;
            [DataMember(Order = 5)] public VectorData prefabScale;
            [DataMember(Order = 6, EmitDefaultValue = false)] public VectorData? minimum;
            [DataMember(Order = 7, EmitDefaultValue = false)] public VectorData? maximum;
            [DataMember(Order = 8, EmitDefaultValue = false)] public string sourceId;
            [DataMember(Order = 9, EmitDefaultValue = false)] public string sourceVersion;
            private static Geometry.Point3 Point(VectorData value) => new Geometry.Point3(value.x, value.y, value.z);
            private static VectorData Vector(Geometry.Point3 value) => new VectorData { x = (float)value.X, y = (float)value.Y, z = (float)value.Z };
            internal Geometry.BlueprintPrefabMetadata ToGeometry() => new Geometry.BlueprintPrefabMetadata(version, prefabId, name, kind, hostId,
                Point(prefabScale), minimum.HasValue ? Point(minimum.Value) : (Geometry.Point3?)null,
                maximum.HasValue ? Point(maximum.Value) : (Geometry.Point3?)null, sourceId, sourceVersion);
            internal static PrefabMetadata FromGeometry(Geometry.BlueprintPrefabMetadata value) => value == null ? null : new PrefabMetadata
            {
                version = value.Version,
                prefabId = value.PrefabId,
                name = value.Name,
                kind = value.Kind,
                hostId = value.HostId,
                prefabScale = Vector(value.PrefabScale),
                minimum = value.Minimum.HasValue ? Vector(value.Minimum.Value) : (VectorData?)null,
                maximum = value.Maximum.HasValue ? Vector(value.Maximum.Value) : (VectorData?)null,
                sourceId = value.SourceId,
                sourceVersion = value.SourceVersion
            };
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
            [DataMember(Order = 10, EmitDefaultValue = false)]
            public string sourceDocumentId;
            [DataMember(Order = 11, EmitDefaultValue = false)]
            public int sourceLine;
            [DataMember(Order = 12, EmitDefaultValue = false)]
            public ForeignPartData foreignData;
            // True only while source absolute scale awaits a known prefab intrinsic scale.
            [DataMember(Order = 13, EmitDefaultValue = false)]
            public bool scaleIsAbsolute;
            [DataMember(Order = 14, EmitDefaultValue = false)]
            public VectorData? baseScale;
            [DataMember(Order = 15, EmitDefaultValue = false)]
            public PrefabMetadata metadata;
            [DataMember(Order = 16, EmitDefaultValue = false)]
            public int? materialSeed;

            internal Vector3 RootScale(Vector3 prefabScale)
            {
                Vector3 root = scaleIsAbsolute ? scale.ToVector3() :
                    Vector3.Scale(baseScale?.ToVector3() ?? prefabScale, scale.ToVector3());
                if (!PositiveScale(new VectorData(root)))
                    throw new ArgumentOutOfRangeException(nameof(scale), "$buildworks_import.transform");
                return root;
            }

            internal Vector3 PlacementScale(Vector3 prefabScale)
            {
                if (scaleIsAbsolute)
                    throw new ArgumentException("Unresolved absolute scale.");
                return RelativeScale(new VectorData(RootScale(prefabScale)), new VectorData(prefabScale)).ToVector3();
            }
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
