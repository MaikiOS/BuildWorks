using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks
{
    // Read-only source library, not a new persisted BuildWorks schema. Unknown
    // object data blocks conversion; the original file is never rewritten.
    internal static class ExternalBlueprintLibrary
    {
        internal const int MaximumFileBytes = 8 * 1024 * 1024;
        internal const int MaximumFiles = 2000;
        internal const long MaximumTotalBytes = 128L * 1024 * 1024;
        internal const int MaximumPreviewBytes = 4 * 1024 * 1024;

        internal static List<CompositeBlueprintStore.Blueprint> LoadFile(string path, string sourceName = null)
        {
            long budget = MaximumTotalBytes;
            return LoadFile(path, ref budget, sourceName);
        }

        internal static List<CompositeBlueprintStore.Blueprint> LoadFile(string path, ref long budget, string sourceName = null)
        {
            if (budget < 0 || budget > MaximumTotalBytes) throw new InvalidDataException("$buildworks_import.file_limit");
            var result = new List<CompositeBlueprintStore.Blueprint>();
            AppendFile(Path.GetFullPath(path), result, ref budget, sourceName);
            return result;
        }

        internal static List<CompositeBlueprintStore.Blueprint> Load(string directory, string stagedPath = null, string stagedName = null,
            Dictionary<string, CompositeBlueprintStore.Blueprint> validationReads = null)
        {
            var result = new List<CompositeBlueprintStore.Blueprint>();
            if (!Directory.Exists(directory)) return result;
            long budget = MaximumTotalBytes;
            string[] paths;
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("$buildworks_import.path");
                paths = Directory.EnumerateFiles(directory)
                    .OrderBy(x => x == stagedPath ? Path.Combine(directory, stagedName) : x, StringComparer.Ordinal)
                    .Take(MaximumFiles + 1).ToArray();
                if (paths.Length > MaximumFiles) throw new InvalidDataException("$buildworks_import.library_limit");
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            { result.Add(Blocked(directory, exception.Message)); return result; }
            foreach (string path in paths)
            {
                if (result.Count >= MaximumFiles) break;
                AppendFile(path, result, ref budget, path == stagedPath ? stagedName : null, validationReads);
            }
            return result;
        }

        private static void AppendFile(string path, List<CompositeBlueprintStore.Blueprint> result, ref long budget, string sourceName = null,
            Dictionary<string, CompositeBlueprintStore.Blueprint> validationReads = null)
        {
            string extension = Path.GetExtension(path).ToLowerInvariant();
            if (extension != ".zip" && !Supported(path)) return;
            try
            {
                if (new FileInfo(path).Length > 64L * 1024 * 1024)
                    throw new InvalidDataException("$buildworks_import.file_limit");
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("$buildworks_import.path");
                if (extension == ".zip")
                {
                    using (var stream = File.OpenRead(path))
                    using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
                    {
                        if (zip.Entries.Count > MaximumFiles)
                            throw new InvalidDataException("$buildworks_import.library_limit");
                        foreach (ZipArchiveEntry entry in zip.Entries)
                        {
                            if (!Supported(entry.FullName)) continue;
                            if (result.Count >= MaximumFiles) throw new InvalidDataException("$buildworks_import.library_limit");
                            string source = (sourceName ?? Path.GetFileName(path)) + "/" + entry.FullName;
                            using (Stream data = entry.Open())
                            {
                                var document = Read(data, entry.Length, source, ref budget, validationReads);
                                if (document == null) continue;
                                result.Add(document);
                            }
                            ZipArchiveEntry preview = zip.GetEntry(Path.ChangeExtension(entry.FullName, ".png"));
                            if (preview != null && preview.Length <= MaximumPreviewBytes && preview.Length <= budget)
                            {
                                budget -= preview.Length;
                                try
                                {
                                    using (Stream image = preview.Open())
                                        result[result.Count - 1].importPreview = ReadPreview(image, preview.Length);
                                }
                                catch (Exception exception) when (exception is IOException || exception is InvalidDataException)
                                { /* An optional damaged picture must not hide the remaining documents. */ }
                            }
                        }
                    }
                }
                else using (Stream stream = File.OpenRead(path))
                {
                    var document = Read(stream, stream.Length, sourceName ?? Path.GetFileName(path), ref budget, validationReads);
                    if (document != null) result.Add(document);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                result.Add(Blocked(sourceName ?? Path.GetFileName(path), exception.Message));
            }
        }

        internal static bool ValidPreview(byte[] png)
        {
            if (png == null || png.Length < 33 || png.Length > MaximumPreviewBytes) return false;
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (int i = 0; i < signature.Length; ++i) if (png[i] != signature[i]) return false;
            if (png[8] != 0 || png[9] != 0 || png[10] != 0 || png[11] != 13 ||
                png[12] != 73 || png[13] != 72 || png[14] != 68 || png[15] != 82) return false;
            uint width = ReadBigEndian(16), height = ReadBigEndian(20);
            return width > 0 && height > 0 && width <= 2048 && height <= 2048;
            uint ReadBigEndian(int offset) => (uint)png[offset] << 24 | (uint)png[offset + 1] << 16 |
                (uint)png[offset + 2] << 8 | png[offset + 3];
        }

        private static byte[] ReadPreview(Stream stream, long length)
        {
            if (length < 33 || length > MaximumPreviewBytes) return null;
            using (var bytes = new MemoryStream())
            {
                var buffer = new byte[8192]; int read;
                while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    if (bytes.Length + read > length) return null;
                    bytes.Write(buffer, 0, read);
                }
                byte[] png = bytes.ToArray();
                return ValidPreview(png) ? png : null;
            }
        }

        internal static bool Supported(string name) =>
            name.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".oprint", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".bbp", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".vtbp", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        internal static bool BrowsableFile(string path, ref long budget)
        {
            if (!Supported(path)) return false;
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase)) return true;
            if (new FileInfo(path).Length > budget) throw new InvalidDataException("$buildworks_import.file_limit");
            var documents = LoadFile(path, ref budget);
            if (documents.Any(document => document.importError == "$buildworks_import.file_limit"))
                throw new InvalidDataException("$buildworks_import.file_limit");
            return documents.Any(document => document.importError == null &&
                (document.sourceDocuments?.SingleOrDefault()?.format == Geometry.OpenKeepBlueprint.Format ||
                 document.sourceDocuments?.SingleOrDefault()?.format == Geometry.BetterBuildingBlueprint.JsonFormat ||
                 document.sourceDocuments?.SingleOrDefault()?.format == Geometry.PlannerBlueprint.Format));
        }

        private static CompositeBlueprintStore.Blueprint Read(Stream stream, long length, string source, ref long budget,
            Dictionary<string, CompositeBlueprintStore.Blueprint> validationReads = null)
        {
            int limit = source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase)
                ? BlueprintExchangeFormat.MaximumBytes : MaximumFileBytes;
            if (length < 0 || length > limit || length > budget)
                return Blocked(source, "$buildworks_import.file_limit");
            budget -= length;
            try
            {
                // Never extract archive paths or execute package contents.
                using (var bytes = new MemoryStream())
                {
                    var buffer = new byte[8192]; int read;
                    while ((read = stream.Read(buffer, 0, buffer.Length)) != 0)
                    {
                        if (bytes.Length + read > length) throw new InvalidDataException("$buildworks_import.file_limit");
                        bytes.Write(buffer, 0, read);
                    }
                    byte[] content = bytes.ToArray();
                    bool bbp = source.EndsWith(".bbp", StringComparison.OrdinalIgnoreCase);
                    // Charge the declared expansion before decoding or reusing a cached BBP model.
                    if (bbp && content.Length >= 48)
                    {
                        int expanded = content[8] | content[9] << 8 | content[10] << 16 | content[11] << 24;
                        long extra = Math.Max(0, expanded - length);
                        if (expanded > MaximumFileBytes || extra > budget) throw new InvalidDataException("$buildworks_import.file_limit");
                        budget -= extra;
                    }
                    // Binary files must never pass through a UTF-8 decoder.
                    string text = source.EndsWith(".oprint", StringComparison.OrdinalIgnoreCase) || bbp
                        ? Convert.ToBase64String(content) : new UTF8Encoding(false, true).GetString(content);
                    if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase) &&
                        !source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase) &&
                        !Geometry.OpenKeepBlueprint.IsJsonBlueprint(text) && !Geometry.BetterBuildingBlueprint.IsJsonBlueprint(text) &&
                        !Geometry.PlannerBlueprint.IsJsonBlueprint(text)) return null;
                    if (validationReads != null && !source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase) && validationReads.TryGetValue(source, out var cached) &&
                        cached.sourceDocuments[0].text == text)
                        return cached.ValidationReadCopy();
                    var parsed = ParseWithinBudget(text, source, ref budget);
                    // One import worker only; exact text is already retained in foreign source evidence.
                    if (validationReads != null && !source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase) &&
                        parsed.sourceDocuments?.Count == 1)
                    {
                        validationReads[source] = parsed;
                        return parsed.ValidationReadCopy();
                    }
                    return parsed;
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is DecoderFallbackException || exception is FormatException || exception is ArgumentException)
            { return Blocked(source, exception.Message); }
        }

        private static CompositeBlueprintStore.Blueprint Blocked(string source, string error) =>
            new CompositeBlueprintStore.Blueprint
            {
                id = Identity(source),
                name = Path.GetFileNameWithoutExtension(source),
                importSource = source,
                importError = error,
                prefabName = "external_" + Identity(source)
            };

        private static string Identity(string value)
        {
            using (var hash = SHA256.Create())
                return "external_" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").ToLowerInvariant();
        }

        internal static CompositeBlueprintStore.Blueprint Parse(string text, string source)
        {
            long budget = MaximumTotalBytes;
            return ParseWithinBudget(text, source, ref budget);
        }

        private static CompositeBlueprintStore.Blueprint ParseWithinBudget(string text, string source, ref long budget)
        {
            if (source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var native = BlueprintExchangeFormat.DecodeWithinBudget(text, ref budget);
                    native.id = Identity(source + "\n" + text);
                    native.importSource = source;
                    native.importPartCount = native.parts.Count;
                    return native;
                }
                catch (InvalidDataException exception) { return Blocked(source, exception.Message); }
            }
            if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    bool planner = Geometry.PlannerBlueprint.IsJsonBlueprint(text);
                    bool betterBuilding = Geometry.BetterBuildingBlueprint.IsJsonBlueprint(text);
                    if (planner && betterBuilding) return Blocked(source, "$buildworks_import.document_invalid");
                    return planner ? ParsePlanner(text, source) : betterBuilding ? ParseBbp(text, source, true) : ParseOpenKeep(text, source);
                }
                catch (Exception error) when (error is ArgumentException || error is FormatException)
                { return Blocked(source, "$buildworks_import.document_invalid"); }
            }
            if (source.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)) return ParseKg(text, source);
            if (source.EndsWith(".oprint", StringComparison.OrdinalIgnoreCase)) return ParseKg(text, source, true);
            if (source.EndsWith(".bbp", StringComparison.OrdinalIgnoreCase)) return ParseBbp(text, source);
            if (source.EndsWith(".vtbp", StringComparison.OrdinalIgnoreCase)) return ParseUnbound(text, source);
            var blueprint = Blocked(source, null);
            blueprint.importAbsoluteScale = true;
            blueprint.id = Identity(source + "\n" + text);
            bool vbuild = source.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase);
            Geometry.BlueprintSourceDocument evidence;
            try { evidence = new Geometry.BlueprintSourceDocument(vbuild ? "vbuild" : "blueprint", Path.GetFileName(source), text); }
            catch (ArgumentException) { return Blocked(source, "$buildworks_import.file_limit"); }
            blueprint.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument>
            { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
            if (evidence.NamedCenterPrefab != null)
                blueprint.id = Identity(source + "\n" + text + "\nsource-center-v1");
            // PlanBuild V1 starts with piece rows, before any explicit section header.
            bool pieces = true, seenPieces = vbuild, snapPoints = false;
            int lineNumber = 0, homesteadHeaders = 0;
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ++lineNumber;
                    string structural = line.Trim();
                    if (structural.Length == 0) continue;
                    if (structural[0] == '#')
                    {
                        line = structural;
                        if (line.StartsWith("#Name:", StringComparison.Ordinal)) blueprint.name = line.Substring(6).Trim();
                        else if (line.StartsWith("#Category:", StringComparison.Ordinal)) blueprint.category = line.Substring(10).Trim();
                        else if (line == "#Pieces") { pieces = true; seenPieces = true; snapPoints = false; }
                        else if (line == "#SnapPoints") { pieces = false; snapPoints = true; }
                        else if (line == "#Terrain") { pieces = false; snapPoints = false; }
                        else if (!line.StartsWith("#Creator:", StringComparison.Ordinal) &&
                            !line.StartsWith("#Description:", StringComparison.Ordinal) &&
                            (vbuild || !line.StartsWith("#Icon:", StringComparison.Ordinal)) &&
                            (vbuild || seenPieces || !Geometry.BlueprintSourceDocument.HomesteadMetadataHeader(line, ref homesteadHeaders)))
                        { pieces = false; snapPoints = false; } // Unknown sections are retained, never executed or parsed as pieces.
                        continue;
                    }
                    if (snapPoints)
                    {
                        try
                        {
                            string[] coordinates = line.Split(new[] { ';' }, 4);
                            // PlanBuild reads only XYZ; retain unrecognized trailing fields in source evidence.
                            if (coordinates.Length < 3 || vbuild && coordinates.Length != 3)
                                throw new InvalidDataException("$buildworks_import.document_invalid");
                            var point = new CompositeBlueprintStore.VectorData
                            { x = Number(coordinates[0]), y = Number(coordinates[1]), z = Number(coordinates[2]) };
                            if (Math.Abs(point.x) > 512 || Math.Abs(point.y) > 512 || Math.Abs(point.z) > 512)
                                throw new InvalidDataException("$buildworks_import.transform");
                            if (blueprint.customSnapPoints == null) blueprint.customSnapPoints = new List<CompositeBlueprintStore.VectorData>();
                            if (blueprint.customSnapPoints.Count >= CompositeBlueprintStore.MaximumCustomSnapPoints) throw new InvalidDataException("$buildworks_import.document_invalid");
                            blueprint.customSnapPoints.Add(point);
                        }
                        catch (Exception exception) when (exception is InvalidDataException || exception is FormatException || exception is OverflowException)
                        { if (blueprint.importError == null) blueprint.importError = exception.Message + "\t" + lineNumber; }
                        continue;
                    }
                    if (!pieces) continue; // terrain has no execution path
                    seenPieces = true;
                    ++blueprint.importPartCount;
                    try
                    {
                        if (blueprint.importPartCount > CompositeBlueprintStore.MaximumParts)
                            throw new InvalidDataException("$buildworks_import.part_limit");
                        // BuildShare uses whitespace; Infinity Hammer also writes a
                        // semicolon dialect. Both share the same eight core fields.
                        bool legacyVbuild = vbuild && !line.Contains(";");
                        string[] fields = PartFields(line, legacyVbuild);
                        int basic = vbuild ? 8 : 9;
                        if (fields.Length < basic || fields.Length > (vbuild ? 10 : 15) ||
                            (!vbuild && (fields.Length == 11 || fields.Length == 12)))
                            throw new InvalidDataException("$buildworks_import.document_invalid");
                        // PlanBuild accepts decimal commas in transforms; leave identifiers and object payloads exact.
                        if (!vbuild)
                            for (int index = 2; index < Math.Min(fields.Length, 13); ++index)
                                if (index != 9) fields[index] = fields[index].Replace(',', '.');
                        string prefab = fields[0].Trim();
                        // Decode only the known sign adapter; every other object payload remains inert and part-bound.
                        string signText = !vbuild && fields.Length > 9 && prefab == "sign" ? BlueprintExchangeFormat.DecodeSignText(fields[9], allowLegacyLiteral: true) : null;
                        int dataIndex = vbuild ? 8 : 13;
                        string info = !vbuild && fields.Length > 9 && prefab != "sign" && HasData(fields[9]) ? fields[9] : null;
                        string zdo = fields.Length > dataIndex && HasData(fields[dataIndex]) ? fields[dataIndex] : null;
                        string chance = fields.Length > dataIndex + 1 && fields[dataIndex + 1] != "" &&
                            fields[dataIndex + 1] != "1" ? fields[dataIndex + 1] : null;
                        CompositeBlueprintStore.ForeignPartData foreign = info == null && zdo == null && chance == null ? null :
                            CompositeBlueprintStore.ForeignPartData.FromGeometry(new Geometry.BlueprintForeignPartData(
                                vbuild ? "vbuild" : "blueprint", info, zdo, chance));
                        if (!vbuild && (prefab == "itemstand" || prefab == "itemstandh") && info != null && zdo == null && chance == null)
                        {
                            try
                            {
                                string itemInfo = BlueprintExchangeFormat.DecodeSignText(info);
                                int itemFields = itemInfo?.Split(':').Length ?? 0;
                                // PlanBuild's Info-only holder reader defaults omitted quality/orientation to 1/0.
                                if (itemFields == 2) itemInfo += ":1:0";
                                else if (itemFields == 3) itemInfo += ":0";
                                foreign = CompositeBlueprintStore.ForeignPartData.FromGeometry(new Geometry.BlueprintForeignPartData(
                                    Geometry.BlueprintForeignPartData.NativeItemStandFormat,
                                    itemInfo, null, null));
                            }
                            catch (Exception error) when (error is ArgumentException || error is InvalidDataException)
                            { /* Unknown holder dialect remains inert, with its exact original row retained. */ }
                        }
                        if (prefab.Length == 0 || prefab.Length > 128 || prefab.Any(c => char.IsControl(c) || c == '/' || c == '\\'))
                            throw new InvalidDataException("$buildworks_import.document_invalid");
                        int p = vbuild ? 5 : 2, q = vbuild ? 1 : 5;
                        var part = new CompositeBlueprintStore.Part
                        {
                            stableId = blueprint.id + "_" + blueprint.importPartCount,
                            prefabName = prefab,
                            displayName = prefab,
                            position = new CompositeBlueprintStore.VectorData { x = Number(fields[p], legacyVbuild), y = Number(fields[p + 1], legacyVbuild), z = Number(fields[p + 2], legacyVbuild) },
                            rotation = new CompositeBlueprintStore.QuaternionData { x = Number(fields[q], legacyVbuild), y = Number(fields[q + 1], legacyVbuild), z = Number(fields[q + 2], legacyVbuild), w = Number(fields[q + 3], legacyVbuild) },
                            scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                            scaleIsAbsolute = true,
                            signText = signText,
                            sourceDocumentId = evidence.Id,
                            sourceLine = lineNumber,
                            foreignData = foreign
                        };
                        if (Math.Abs(part.position.x) > 512 || Math.Abs(part.position.y) > 512 || Math.Abs(part.position.z) > 512)
                            throw new InvalidDataException("$buildworks_import.transform");
                        if (!vbuild && fields.Length >= 13)
                            part.scale = new CompositeBlueprintStore.VectorData { x = Number(fields[10]), y = Number(fields[11]), z = Number(fields[12]) };
                        double norm = Math.Sqrt((double)part.rotation.x * part.rotation.x + (double)part.rotation.y * part.rotation.y +
                            (double)part.rotation.z * part.rotation.z + (double)part.rotation.w * part.rotation.w);
                        if (norm < .999 || norm > 1.001 || !CompositeBlueprintStore.PositiveScale(part.scale))
                            throw new InvalidDataException("$buildworks_import.transform");
                        part.rotation.x = (float)(part.rotation.x / norm);
                        part.rotation.y = (float)(part.rotation.y / norm);
                        part.rotation.z = (float)(part.rotation.z / norm);
                        part.rotation.w = (float)(part.rotation.w / norm);
                        blueprint.parts.Add(part);
                    }
                    catch (Exception exception) when (exception is InvalidDataException || exception is FormatException || exception is OverflowException || exception is ArgumentException)
                    { if (blueprint.importError == null) blueprint.importError = exception.Message + "\t" + lineNumber; }
                }
            }
            if (!seenPieces || blueprint.importPartCount < 1) blueprint.importError = "$buildworks_import.part_limit";
            if (blueprint.name.Length == 0 || blueprint.name.Length > 48 || blueprint.category.Length == 0 || blueprint.category.Length > 24 ||
                (blueprint.name + blueprint.category).Any(c => char.IsControl(c) || c == '<' || c == '>'))
                blueprint.importError = "$buildworks_store.invalid_metadata";
            if (blueprint.importError == null && evidence.NamedCenterPrefab != null)
            {
                try { ApplyNamedCenter(blueprint, evidence.NamedCenterPrefab); }
                catch (ArgumentException) { blueprint.importError = "$buildworks_import.transform"; }
            }
            if (blueprint.importError != null) blueprint.parts.Clear(); // no partial construction can be used
            else blueprint.prefabName = blueprint.parts[0].prefabName;
            return blueprint;
        }

        private static CompositeBlueprintStore.Blueprint ParsePlanner(string text, string source)
        {
            var blueprint = Blocked(source, null);
            try
            {
                var evidence = new Geometry.BlueprintSourceDocument(Geometry.PlannerBlueprint.Format, Path.GetFileName(source), text);
                var model = evidence.Planner;
                blueprint.id = Identity(source + "\n" + text);
                blueprint.name = model.Title;
                blueprint.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
                blueprint.importPartCount = model.Pieces.Count;
                if (model.Layers.Count > Geometry.BlueprintEditorDocument.MaximumGroups)
                    throw new InvalidDataException("$buildworks_import.document_invalid");
                foreach (string label in model.Layers.Select(layer => layer.Name).Prepend(model.Title))
                    if (string.IsNullOrWhiteSpace(label) || label.Length > 48 || label != label.Trim() ||
                        label.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                        throw new InvalidDataException("$buildworks_store.invalid_metadata");
                var layers = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var layer in model.Layers)
                {
                    string id = blueprint.id + "_layer_" + blueprint.groups.Count;
                    layers.Add(layer.Id, id);
                    blueprint.groups.Add(new CompositeBlueprintStore.Group { stableId = id, name = layer.Name,
                        sourceDocumentId = evidence.Id, sourceLayerId = layer.Id });
                }
                // The producer's native exports specify absolute unit scale, not an unknown prefab basis.
                blueprint.importAbsoluteScale = true;
                foreach (var piece in model.Pieces)
                {
                    var position = piece.Position; var rotation = piece.Rotation.Normalized();
                    if (Math.Abs(position.X) > 512 || Math.Abs(position.Y) > 512 || Math.Abs(position.Z) > 512)
                        throw new InvalidDataException("$buildworks_import.transform");
                    blueprint.parts.Add(new CompositeBlueprintStore.Part {
                        stableId = blueprint.id + "_" + piece.Record, prefabName = piece.Prefab, displayName = piece.Prefab,
                        position = new CompositeBlueprintStore.VectorData { x = (float)position.X, y = (float)position.Y, z = (float)position.Z },
                        rotation = new CompositeBlueprintStore.QuaternionData { x = (float)rotation.X, y = (float)rotation.Y, z = (float)rotation.Z, w = (float)rotation.W },
                        parentGroupId = layers[piece.LayerId], sourceDocumentId = evidence.Id,
                        sourceRecord = piece.Record, sourceFrameApplied = true
                    });
                }
                blueprint.prefabName = blueprint.parts[0].prefabName;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidDataException)
            { blueprint.importError = error is ArgumentException ? "$buildworks_import.document_invalid" : error.Message; blueprint.parts.Clear(); blueprint.groups.Clear(); }
            return blueprint;
        }

        private static CompositeBlueprintStore.Blueprint ParseBbp(string text, string source, bool json = false)
        {
            var blueprint = Blocked(source, null);
            try
            {
                var evidence = new Geometry.BlueprintSourceDocument(json ? Geometry.BetterBuildingBlueprint.JsonFormat : Geometry.BetterBuildingBlueprint.BinaryFormat, Path.GetFileName(source), text);
                var model = evidence.BetterBuilding;
                blueprint.id = Identity(source + "\n" + text);
                blueprint.name = model.Name;
                blueprint.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
                blueprint.importPartCount = model.Pieces.Count;
                if (string.IsNullOrWhiteSpace(blueprint.name) || blueprint.name.Length > 48 || blueprint.name.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                    throw new InvalidDataException("$buildworks_store.invalid_metadata");
                foreach (var piece in model.Pieces)
                {
                    if (piece.Prefab.Length > 128 || piece.Prefab.Any(c => char.IsControl(c) || "/\\<>".Contains(c)) || !Geometry.BlueprintEditorPart.IsValidSignText(piece.SignText))
                        throw new InvalidDataException("$buildworks_import.document_invalid");
                    var rotation = piece.Rotation.Normalized();
                    var scale = piece.Scale ?? new Geometry.Point3(1, 1, 1);
                    blueprint.parts.Add(new CompositeBlueprintStore.Part {
                        stableId = blueprint.id + "_" + piece.Record, prefabName = piece.Prefab, displayName = piece.Prefab,
                        position = new CompositeBlueprintStore.VectorData { x = (float)piece.Position.X, y = (float)piece.Position.Y, z = (float)piece.Position.Z },
                        rotation = new CompositeBlueprintStore.QuaternionData { x = (float)rotation.X, y = (float)rotation.Y, z = (float)rotation.Z, w = (float)rotation.W },
                        scale = new CompositeBlueprintStore.VectorData { x = (float)scale.X, y = (float)scale.Y, z = (float)scale.Z },
                        signText = piece.SignText, sourceDocumentId = evidence.Id, sourceRecord = piece.Record, sourceFrameApplied = true
                    });
                }
                blueprint.prefabName = blueprint.parts[0].prefabName;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidDataException)
            { blueprint.importError = error is ArgumentException ? "$buildworks_import.document_invalid" : error.Message; blueprint.parts.Clear(); }
            return blueprint;
        }

        private static CompositeBlueprintStore.Blueprint ParseUnbound(string text, string source)
        {
            var blueprint = Blocked(source, null);
            try
            {
                var evidence = new Geometry.BlueprintSourceDocument(Geometry.UnboundBlueprint.Format, Path.GetFileName(source), text);
                var model = evidence.Unbound;
                blueprint.id = Identity(source + "\n" + text + "\nunbound-v2");
                blueprint.name = Path.GetFileNameWithoutExtension(source);
                if (string.IsNullOrWhiteSpace(blueprint.name) || blueprint.name.Length > 48 ||
                    blueprint.name.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                    throw new InvalidDataException("$buildworks_store.invalid_metadata");
                blueprint.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument> { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
                blueprint.importPartCount = model.Pieces.Count;
                if (model.Pieces.Count == 0) throw new InvalidDataException("$buildworks_import.part_limit");
                foreach (var piece in model.Pieces)
                {
                    if (piece.PrefabName.Length > 128 || Math.Abs(piece.Position.X) > 512 || Math.Abs(piece.Position.Y) > 512 || Math.Abs(piece.Position.Z) > 512)
                        throw new InvalidDataException("$buildworks_import.transform");
                    var rotation = piece.Rotation.Normalized();
                    blueprint.parts.Add(new CompositeBlueprintStore.Part {
                        stableId = blueprint.id + "_" + piece.Line, prefabName = piece.PrefabName, displayName = piece.PrefabName,
                        position = new CompositeBlueprintStore.VectorData { x = (float)piece.Position.X, y = (float)piece.Position.Y, z = (float)piece.Position.Z },
                        rotation = new CompositeBlueprintStore.QuaternionData { x = (float)rotation.X, y = (float)rotation.Y, z = (float)rotation.Z, w = (float)rotation.W },
                        sourceDocumentId = evidence.Id, sourceLine = piece.Line, sourceFrameApplied = true,
                        foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(new Geometry.BlueprintForeignPartData(
                            evidence.Format, piece.PrefabName, piece.ZdoData, null))
                    });
                }
                blueprint.prefabName = blueprint.parts[0].prefabName;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidDataException)
            { blueprint.importError = error is ArgumentException || error is FormatException ? "$buildworks_import.document_invalid" : error.Message; blueprint.parts.Clear(); }
            return blueprint;
        }

        private static CompositeBlueprintStore.Blueprint ParseKg(string text, string source, bool binary = false)
        {
            var blueprint=Blocked(source,null);
            try
            {
                var evidence=new Geometry.BlueprintSourceDocument(binary ? Geometry.KgBlueprint.BinaryFormat : Geometry.KgBlueprint.Format,Path.GetFileName(source),text);
                var model=evidence.Kg;
                blueprint.id=Identity(source+"\n"+text);
                blueprint.name=model.Name;
                if (string.IsNullOrWhiteSpace(blueprint.name) || blueprint.name.Length>48 ||
                    blueprint.name.Any(c=>char.IsControl(c) || c=='<' || c=='>'))
                    throw new InvalidDataException("$buildworks_store.invalid_metadata");
                blueprint.sourceDocuments=new List<CompositeBlueprintStore.SourceDocument>{CompositeBlueprintStore.SourceDocument.FromGeometry(evidence)};
                blueprint.importPartCount=model.Objects.Count;
                foreach(var piece in model.Objects)
                {
                    var pose=Geometry.KgBlueprintFrame.Canonical(piece,model.BoxRotation);
                    if (piece.ReferenceName.Length>128 || piece.ReferenceName.Any(c=>c=='/' || c=='\\') ||
                        Math.Abs(pose.Position.X)>512 || Math.Abs(pose.Position.Y)>512 || Math.Abs(pose.Position.Z)>512)
                        throw new InvalidDataException("$buildworks_import.transform");
                    blueprint.parts.Add(new CompositeBlueprintStore.Part {
                        stableId=blueprint.id+"_"+piece.Record,prefabName=piece.ReferenceName,displayName=piece.ReferenceName,
                        position=new CompositeBlueprintStore.VectorData{x=(float)pose.Position.X,y=(float)pose.Position.Y,z=(float)pose.Position.Z},
                        rotation=new CompositeBlueprintStore.QuaternionData{x=(float)pose.Rotation.X,y=(float)pose.Rotation.Y,z=(float)pose.Rotation.Z,w=(float)pose.Rotation.W},
                        sourceDocumentId=evidence.Id,sourceRecord=piece.Record,sourceFrameApplied=true,
                        foreignData=piece.ZdoData==null?null:CompositeBlueprintStore.ForeignPartData.FromGeometry(
                            new Geometry.BlueprintForeignPartData(evidence.Format,null,piece.ZdoData,null))
                    });
                }
                blueprint.prefabName=blueprint.parts[0].prefabName;
            }
            catch(Exception error) when(error is ArgumentException || error is FormatException || error is InvalidDataException)
            { blueprint.importError=error is ArgumentException ? "$buildworks_import.document_invalid" : error.Message;blueprint.parts.Clear(); }
            return blueprint;
        }

        private static CompositeBlueprintStore.Blueprint ParseOpenKeep(string text, string source)
        {
            var blueprint = Blocked(source, null);
            try
            {
                var evidence = new Geometry.BlueprintSourceDocument(Geometry.OpenKeepBlueprint.Format, Path.GetFileName(source), text);
                var document = evidence.OpenKeep;
                blueprint.id = Identity(source + "\n" + text);
                blueprint.name = document.Name;
                blueprint.sourceDocuments = new List<CompositeBlueprintStore.SourceDocument>
                { CompositeBlueprintStore.SourceDocument.FromGeometry(evidence) };
                blueprint.importPartCount = document.Pieces.Count;
                if (document.Pieces.Count == 0) throw new InvalidDataException("$buildworks_import.part_limit");
                if (string.IsNullOrWhiteSpace(blueprint.name) || blueprint.name.Length > 48 ||
                    blueprint.name.Any(c => char.IsControl(c) || c == '<' || c == '>'))
                    throw new InvalidDataException("$buildworks_store.invalid_metadata");
                foreach (var piece in document.Pieces)
                {
                    if (piece.Prefab.Length > 128 || piece.Prefab.Any(c => c == '/' || c == '\\') ||
                        Math.Abs(piece.Position.X) > 512 || Math.Abs(piece.Position.Y) > 512 || Math.Abs(piece.Position.Z) > 512)
                        throw new InvalidDataException("$buildworks_import.transform");
                    Geometry.Rotation3 rotation = piece.Rotation;
                    blueprint.parts.Add(new CompositeBlueprintStore.Part {
                        stableId = blueprint.id + "_" + piece.Record, prefabName = piece.Prefab, displayName = piece.Prefab,
                        position = new CompositeBlueprintStore.VectorData { x = (float)piece.Position.X, y = (float)piece.Position.Y, z = (float)piece.Position.Z },
                        rotation = new CompositeBlueprintStore.QuaternionData { x = (float)rotation.X, y = (float)rotation.Y, z = (float)rotation.Z, w = (float)rotation.W },
                        sourceDocumentId = evidence.Id, sourceRecord = piece.Record
                    });
                }
                blueprint.prefabName = blueprint.parts[0].prefabName;
            }
            catch (Exception error) when (error is ArgumentException || error is FormatException || error is InvalidDataException)
            { blueprint.importError = error is ArgumentException ? "$buildworks_import.document_invalid" : error.Message; blueprint.parts.Clear(); }
            return blueprint;
        }

        private static void ApplyNamedCenter(CompositeBlueprintStore.Blueprint blueprint, string centerPrefab)
        {
            var selected = blueprint.parts.FirstOrDefault(part => part.prefabName == centerPrefab);
            Geometry.Point3 center;
            var inverse = new Geometry.Rotation3(0, 0, 0, 1);
            if (selected != null)
            {
                center = new Geometry.Point3(selected.position.x, selected.position.y, selected.position.z);
                inverse = new Geometry.Rotation3(-selected.rotation.x, -selected.rotation.y,
                    -selected.rotation.z, selected.rotation.w).Normalized();
            }
            else
            {
                // The producer's fallback bounds include zero and use piece origins, never prefab bounds.
                double minX = 0, minZ = 0, maxX = 0, maxZ = 0, minY = double.PositiveInfinity;
                foreach (var part in blueprint.parts)
                {
                    minX = Math.Min(minX, part.position.x); maxX = Math.Max(maxX, part.position.x);
                    minZ = Math.Min(minZ, part.position.z); maxZ = Math.Max(maxZ, part.position.z);
                    minY = Math.Min(minY, part.position.y);
                }
                center = new Geometry.Point3((minX + maxX) / 2, minY, (minZ + maxZ) / 2);
            }
            var poses = blueprint.parts.Select(part => Geometry.BlueprintEditorDocument.TransformPart(
                new Geometry.BlueprintEditorPart(part.stableId, part.prefabName, part.prefabName,
                    new Geometry.Point3(part.position.x, part.position.y, part.position.z),
                    new Geometry.Rotation3(part.rotation.x, part.rotation.y, part.rotation.z, part.rotation.w)),
                center * -1, inverse, center, new Geometry.Point3(1, 1, 1), new Geometry.Rotation3(0, 0, 0, 1))).ToArray();
            var snaps = (blueprint.customSnapPoints ?? new List<CompositeBlueprintStore.VectorData>())
                .Select(point => inverse.Rotate(new Geometry.Point3(point.x, point.y, point.z) - center)).ToArray();
            foreach (var pose in poses)
                if (Math.Abs(pose.Position.X) > 512 || Math.Abs(pose.Position.Y) > 512 || Math.Abs(pose.Position.Z) > 512)
                    throw new ArgumentOutOfRangeException(nameof(center));
            foreach (var point in snaps)
                if (Math.Abs(point.X) > 512 || Math.Abs(point.Y) > 512 || Math.Abs(point.Z) > 512)
                    throw new ArgumentOutOfRangeException(nameof(snaps));
            for (int index = 0; index < blueprint.parts.Count; ++index)
            {
                var part = blueprint.parts[index]; var pose = poses[index];
                part.position = new CompositeBlueprintStore.VectorData {
                    x = (float)pose.Position.X, y = (float)pose.Position.Y, z = (float)pose.Position.Z };
                part.rotation = new CompositeBlueprintStore.QuaternionData {
                    x = (float)pose.Rotation.X, y = (float)pose.Rotation.Y, z = (float)pose.Rotation.Z, w = (float)pose.Rotation.W };
                part.sourceFrameApplied = true;
            }
            if (blueprint.customSnapPoints != null)
                blueprint.customSnapPoints = snaps.Select(point => new CompositeBlueprintStore.VectorData {
                    x = (float)point.X, y = (float)point.Y, z = (float)point.Z }).ToList();
        }

        internal static CompositeBlueprintStore.Blueprint MigrateLegacyRoof(CompositeBlueprintStore.Blueprint source, bool verifiedHost)
        {
            if (!verifiedHost || source == null || source.importError != null) return source;
            var evidence = Geometry.BlueprintSourceDocument.Index(source.sourceDocuments?.Select(document => document.ToGeometry()));
            bool Eligible(CompositeBlueprintStore.Part part) => part.prefabName == Geometry.BlueprintPrefabMigration.OldRoof &&
                part.originalPrefabName == null && !part.scaleIsAbsolute && part.foreignData == null && !part.materialSeed.HasValue &&
                part.sourceDocumentId != null && evidence.TryGetValue(part.sourceDocumentId, out var document) &&
                document.HasPartAddress(part.sourceLine, part.sourceRecord, part.prefabName);
            if (!source.parts.Any(Eligible)) return source;
            var copy = source.ImportCopy();
            foreach (var part in copy.parts.Where(Eligible))
            {
                part.originalPrefabName = part.prefabName;
                part.prefabName = Geometry.BlueprintPrefabMigration.CurrentRoof;
                part.metadata = null; // Commit captures metadata from the effective native prefab.
            }
            copy.prefabName = (copy.parts.FirstOrDefault(part => part.stableId == copy.primaryPartId) ?? copy.parts[0]).prefabName;
            copy.importPreview = null;
            copy.id = Identity(source.id + "\nnative-roof-migration-v1");
            return copy;
        }

        internal static string ProducerPrefabName(CompositeBlueprintStore.Part part,
            IReadOnlyDictionary<string, Geometry.BlueprintSourceDocument> sources, Func<string, bool> exists)
        {
            string name = part.prefabName, target = Geometry.PlannerPrefabNames.Native(name);
            return part.originalPrefabName == null && target != name && part.sourceDocumentId != null &&
                sources.TryGetValue(part.sourceDocumentId, out var source) && Geometry.PlannerPrefabNames.SupportsSource(source) &&
                source.HasPartAddress(part.sourceLine, part.sourceRecord, name) && !exists(name) && exists(target) ? target : name;
        }

        private static CompositeBlueprintStore.Blueprint PrepareProducerPrefabNames(CompositeBlueprintStore.Blueprint source,
            Func<string, CompositeBlueprintStore.VectorData?> resolvePrefabScale)
        {
            if (source == null || source.importError != null || !source.parts.Any(part =>
                part.originalPrefabName == null && Geometry.PlannerPrefabNames.Native(part.prefabName) != part.prefabName)) return source;
            var evidence = Geometry.BlueprintSourceDocument.Index(source.sourceDocuments?.Select(document => document.ToGeometry()));
            var known = new Dictionary<string, bool>(StringComparer.Ordinal);
            bool Exists(string name)
            {
                if (!known.TryGetValue(name, out bool value)) known.Add(name, value = resolvePrefabScale?.Invoke(name).HasValue == true);
                return value;
            }
            bool Eligible(CompositeBlueprintStore.Part part) => ProducerPrefabName(part, evidence, Exists) != part.prefabName;
            if (!source.parts.Any(Eligible)) return source;
            var copy = source.ImportCopy();
            foreach (var part in copy.parts.Where(Eligible))
            {
                part.originalPrefabName = part.prefabName;
                part.prefabName = Geometry.PlannerPrefabNames.Native(part.prefabName);
                if (part.displayName == part.originalPrefabName) part.displayName = part.prefabName;
                part.metadata = null;
            }
            copy.prefabName = (copy.parts.FirstOrDefault(part => part.stableId == copy.primaryPartId) ?? copy.parts[0]).prefabName;
            copy.importPreview = null;
            if (source.IsExternal) copy.id = Identity(source.id + "\nproducer-native-case-v1");
            return copy;
        }

        internal static CompositeBlueprintStore.Blueprint PrepareForHost(CompositeBlueprintStore.Blueprint source,
            Func<string, CompositeBlueprintStore.VectorData?> resolvePrefabScale, Func<string, int> hash = null, int? worldLevel = null,
            Func<string, bool> supportsMaterialSeed = null, Func<string, (int Width, int Height)?> containerGrid = null,
            Func<int, string> resolvePrefabHash = null, Func<string, bool?> supportsKgState = null,
            Func<string, Geometry.UnboundImportContext> unboundContext = null)
        {
            source = PrepareProducerPrefabNames(source, resolvePrefabScale);
            if (source == null || source.importError != null || !source.importAbsoluteScale && !source.parts.Any(part => part.scaleIsAbsolute) &&
                (hash == null || !BlueprintExchangeFormat.HasKgData(source) && !BlueprintExchangeFormat.HasUnboundData(source) &&
                    !source.parts.Any(part => part.foreignData?.format == "blueprint" || part.foreignData?.format == "vbuild"))) return source;
            var copy = source.ImportCopy();
            string currentPrefab = null;
            try
            {
                var scales = new Dictionary<string, CompositeBlueprintStore.VectorData?>(StringComparer.Ordinal);
                Dictionary<int, Type> ordinaryStateFields = null;
                Dictionary<int, Type> kgArchiveFields = null;
                var evidenceIndex = hash == null ? null : Geometry.BlueprintSourceDocument.Index(copy.sourceDocuments?.Select(evidence => evidence.ToGeometry()));
                foreach (CompositeBlueprintStore.Part part in copy.parts)
                {
                    if (hash != null && part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var unbound) && unbound.Unbound != null)
                    {
                        var context = unboundContext?.Invoke(part.prefabName);
                        ConvertUnboundState(part, unbound, context, resolvePrefabHash);
                        if (context != null) scales[part.prefabName] = new CompositeBlueprintStore.VectorData {
                            x = (float)context.Metadata.PrefabScale.X, y = (float)context.Metadata.PrefabScale.Y, z = (float)context.Metadata.PrefabScale.Z };
                        continue;
                    }
                    if (hash != null && part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var kgSource) && kgSource.Kg != null &&
                        part.sourceRecord >= 1 && part.sourceRecord <= kgSource.Kg.Objects.Count)
                    {
                        var original=kgSource.Kg.Objects[part.sourceRecord-1];
                        if (!original.MatchesPrefab(part.originalPrefabName ?? part.prefabName))
                            throw new InvalidDataException("$buildworks_import.document_invalid");
                        if (original.PrefabHash.HasValue && part.prefabName==original.ReferenceName && resolvePrefabHash!=null)
                        {
                            string resolved=resolvePrefabHash(original.PrefabHash.Value);
                            if (resolved!=null)
                            {
                                if (string.IsNullOrWhiteSpace(resolved) || resolved.Length>128 || resolved.Any(c=>char.IsControl(c) || "/\\<>".Contains(c)) ||
                                    hash(resolved)!=original.PrefabHash.Value) throw new InvalidDataException("$buildworks_import.document_invalid");
                                part.prefabName=resolved;part.displayName=resolved;part.metadata=null;
                            }
                        }
                        bool? supportedState=supportsKgState?.Invoke(part.prefabName);
                        if (supportedState==false && original.ZdoData==null)
                            throw new InvalidDataException("$buildworks_placement.contents_unsupported\t"+part.prefabName);
                        if (supportedState==true)
                            ConvertKgKnownState(part,kgSource,hash,supportsMaterialSeed,ref kgArchiveFields);
                    }
                    currentPrefab = part.prefabName;
                    if (source.importAbsoluteScale || part.scaleIsAbsolute)
                    {
                        part.scaleIsAbsolute = true;
                        if (!scales.TryGetValue(part.prefabName, out var prefabScale))
                        {
                            prefabScale = resolvePrefabScale?.Invoke(part.prefabName);
                            scales.Add(part.prefabName, prefabScale);
                        }
                        if (!prefabScale.HasValue) continue; // Retain the original basis, never invent intrinsic=1.
                        if (!CompositeBlueprintStore.PositiveScale(prefabScale.Value) ||
                            !CompositeBlueprintStore.PositiveScale(part.scale))
                            throw new ArgumentOutOfRangeException(nameof(part.scale));
                        part.baseScale = part.scale;
                        part.scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 };
                        part.scaleIsAbsolute = false;
                    }
                    if (hash != null && part.sourceDocumentId != null && evidenceIndex.TryGetValue(part.sourceDocumentId, out var evidence))
                    {
                        ConvertLegacyStorage(part, evidence, hash, containerGrid);
                        ConvertKnownState(part, evidence, hash, worldLevel, supportsMaterialSeed, ref ordinaryStateFields);
                    }
                }
                copy.importAbsoluteScale = false;
                string cacheIdentity=source.id;
                if (source.IsExternal && BlueprintExchangeFormat.HasKgData(copy))
                {
                    copy.prefabName=(copy.parts.FirstOrDefault(part=>part.stableId==copy.primaryPartId) ?? copy.parts[0]).prefabName;
                    cacheIdentity=Identity(source.id+"\nkg-resolved-prefabs-v1\n"+string.Join("\n",copy.parts.Select(part=>part.prefabName)));
                }
                // Derived cache identity must not reuse pre-normalization thumbnails or another prefab revision.
                if (source.IsExternal) copy.id = Identity(cacheIdentity + "\npinned-scale-v1-pending-parts\n" + (hash == null ? "inert" : worldLevel?.ToString(CultureInfo.InvariantCulture) ?? "pending-world") + "\n" + string.Join("\n", scales.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => pair.Key + "\t" + (pair.Value.HasValue ? pair.Value.Value.x.ToString("R", CultureInfo.InvariantCulture) + "\t" +
                        pair.Value.Value.y.ToString("R", CultureInfo.InvariantCulture) + "\t" + pair.Value.Value.z.ToString("R", CultureInfo.InvariantCulture) : "unknown"))));
                if (source.IsExternal && copy.parts.Any(part => part.materialSeed.HasValue))
                    copy.id = Identity(copy.id + "\nmaterial-seed-v1\n" + string.Join(",",
                        copy.parts.Select(part => part.materialSeed?.ToString(CultureInfo.InvariantCulture) ?? "null")));
                if (source.IsExternal && copy.parts.Any(part => part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeContainerFormat))
                    copy.id = Identity(copy.id + "\ncontainer-manifest-v1\n" + string.Join("\n", copy.parts.Select(part =>
                        part.foreignData?.format == Geometry.BlueprintForeignPartData.NativeContainerFormat ? part.foreignData.info : "null")));
            }
            catch (Exception error) when (error is InvalidDataException || error is ArgumentException)
            {
                copy.importError = error is ArgumentException ? "$buildworks_import.scale_range\t" + currentPrefab : error.Message;
                copy.parts.Clear(); // Atomic conversion: never publish mixed absolute/relative scales.
            }
            return copy;
        }

        private static void ConvertUnboundState(CompositeBlueprintStore.Part part, Geometry.BlueprintSourceDocument evidence,
            Geometry.UnboundImportContext context, Func<int, string> resolveItem)
        {
            var original = evidence.UnboundPartAt(part.sourceLine);
            var current = part.foreignData?.ToGeometry();
            if (!evidence.HasPartAddress(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName) ||
                !evidence.HasPartState(part.sourceLine, part.sourceRecord, part.originalPrefabName ?? part.prefabName, current,
                    part.metadata?.ToGeometry(), part.baseScale.HasValue ? new Geometry.Point3(part.baseScale.Value.x, part.baseScale.Value.y, part.baseScale.Value.z) :
                    (Geometry.Point3?)null, part.scaleIsAbsolute, part.signText, part.materialSeed))
                throw new InvalidDataException("$buildworks_import.document_invalid");
            bool raw = current?.Format == Geometry.UnboundBlueprint.Format;
            if (context == null || part.scaleIsAbsolute || !Geometry.UnboundStateProjection.TryProject(original, context, resolveItem, out var projection))
            {
                if (!raw) throw new InvalidDataException("$buildworks_placement.contents_stale");
                return;
            }
            if (!raw)
            {
                if (!part.baseScale.HasValue || part.baseScale.Value.x != (float)projection.RootScale.X ||
                    part.baseScale.Value.y != (float)projection.RootScale.Y || part.baseScale.Value.z != (float)projection.RootScale.Z ||
                    (current == null) != (projection.Contents == null) || current != null &&
                    (current.Format != projection.Contents.Format || current.Info != projection.Contents.Info || current.Zdo != projection.Contents.Zdo))
                    throw new InvalidDataException("$buildworks_placement.contents_stale");
            }
            else
            {
                part.baseScale = new CompositeBlueprintStore.VectorData {
                    x = (float)projection.RootScale.X, y = (float)projection.RootScale.Y, z = (float)projection.RootScale.Z };
                part.signText = part.signText ?? projection.SignText;
                part.materialSeed = part.materialSeed ?? projection.MaterialSeed;
                part.foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(projection.Contents);
            }
            part.metadata = CompositeBlueprintStore.PrefabMetadata.FromGeometry(context.Metadata);
        }

        private static void ConvertKgKnownState(CompositeBlueprintStore.Part part, Geometry.BlueprintSourceDocument evidence,
            Func<string,int> hash,Func<string,bool> supportsMaterialSeed,ref Dictionary<int,Type> archiveFields)
        {
            var original=part.foreignData;
            if(original?.format!=evidence.Format || evidence.Kg==null || !part.sourceFrameApplied ||
                !evidence.HasPartAddress(part.sourceLine,part.sourceRecord,part.originalPrefabName ?? part.prefabName) ||
                evidence.Kg.Objects[part.sourceRecord-1].ZdoData!=original.zdo ||
                !Geometry.BlueprintForeignZdo.TryReadKg(original.zdo,out var state)) return;
            var consumed=new HashSet<KeyValuePair<int,Type>>();
            string text=part.signText; int? seed=part.materialSeed;
            if(part.prefabName=="sign")
            {
                foreach(string key in new[]{"text","author","authorPlatformDisplayName"})
                {
                    int id=hash(key);
                    if(!state.HasHash(id)) continue;
                    if(!state.TryGet<string>(id,out string value)) return;
                    if(key=="text")
                    {
                        if(!Geometry.BlueprintEditorPart.IsValidSignText(value)) return;
                        if(text==null) text=value;
                    }
                    consumed.Add(new KeyValuePair<int,Type>(id,typeof(string)));
                }
            }
            int material=hash("RandMatSeed");
            if(state.HasHash(material))
            {
                if(supportsMaterialSeed?.Invoke(part.prefabName)!=true || !state.TryGet<int>(material,out int value) || value<0) return;
                if(!seed.HasValue) seed=value;
                consumed.Add(new KeyValuePair<int,Type>(material,typeof(int)));
            }
            // Exact source keeps these typed provenance fields. New construction assigns its own identity,
            // rotation witness, operation and payment receipt; none of the old bytes are executed or replayed.
            var archived=archiveFields ?? (archiveFields=new Dictionary<int,Type>{
                [hash("creator")]=typeof(long),[hash("creatorIndex")]=typeof(int),[hash("cheated")]=typeof(int),
                [hash("BuildWorks_ExactRotation_v1")]=typeof(Geometry.Rotation3),
                [hash("BuildWorks_ExactRotationWitness_v1")]=typeof(Geometry.Rotation3),
                [hash("ostrix_buildworks_project_operation_v1")]=typeof(string),
                [hash("ostrix_buildworks_project_receipt_v1")]=typeof(byte[])
            });
            foreach(var key in state.Keys)
                if(!consumed.Contains(key) && (!archived.TryGetValue(key.Key,out Type expected) || key.Value!=expected)) return;
            part.signText=text;part.materialSeed=seed;part.foreignData=null;
        }

        private static void ConvertLegacyStorage(CompositeBlueprintStore.Part part, Geometry.BlueprintSourceDocument evidence,
            Func<string, int> hash, Func<string, (int Width, int Height)?> containerGrid)
        {
            var original = part.foreignData;
            if (original?.format != "blueprint" || original.info == null || original.zdo != null || original.chance != null ||
                evidence?.Format != "blueprint" || evidence.Id != part.sourceDocumentId ||
                !evidence.HasPartAt(part.sourceLine, part.prefabName)) return;
            string[] fields = evidence.PartRow(part.sourceLine)?.Split(';');
            if (fields == null || fields.Length != 10 && fields.Length != 13 && fields.Length != 14 && fields.Length != 15 ||
                fields[9] != original.info || fields.Length > 13 && HasData(fields[13]) ||
                fields.Length > 14 && HasData(fields[14]) && fields[14] != "1") return;
            var grid = containerGrid?.Invoke(part.prefabName);
            if (!grid.HasValue) return;
            try
            {
                if (Geometry.BlueprintContainerInventory.TryConvertLegacy106(BlueprintExchangeFormat.DecodeInventoryInfo(original.info),
                    grid.Value.Width, grid.Value.Height, hash, out var contents))
                    part.foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(contents);
            }
            catch (InvalidDataException) { } // Unknown/corrupt Info remains inert with its exact source row.
        }

        internal static bool CanOmitArmorStandState(CompositeBlueprintStore.Part part, Geometry.BlueprintSourceDocument evidence)
        {
            var state = part?.foreignData;
            if (part?.prefabName != "ArmorStand" || state?.format != "blueprint" || state.info == null ||
                state.zdo != null || state.chance != null || evidence?.Format != "blueprint" || evidence.HasUnsupportedSections(part.sourceFrameApplied) ||
                evidence.Id != part.sourceDocumentId || !evidence.HasPartAt(part.sourceLine, part.prefabName)) return false;
            string[] row = evidence.PartRow(part.sourceLine).Split(';');
            if (row.Length != 10 && row.Length != 13 && row.Length != 14 && row.Length != 15 || row[9] != state.info ||
                row.Length > 13 && HasData(row[13]) || row.Length > 14 && HasData(row[14]) && row[14] != "1") return false;
            try
            {
                // PlanBuild stores pose, slot count and item/variant pairs here; root transforms use separate columns.
                string[] fields = BlueprintExchangeFormat.DecodeSignText(state.info)?.Split(':');
                if (fields == null || fields.Length < 3 || fields[fields.Length - 1] != "" ||
                    !int.TryParse(fields[0], NumberStyles.None, CultureInfo.InvariantCulture, out _) ||
                    !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int slots) ||
                    slots != (fields.Length - 3) / 2 || fields.Length != 3 + 2 * slots) return false;
                for (int index = 0; index < slots; ++index)
                {
                    string item = fields[2 + index * 2];
                    if (item.Length > 128 || item.Any(c => char.IsControl(c) || "/\\<>".Contains(c)) ||
                        !int.TryParse(fields[3 + index * 2], NumberStyles.None, CultureInfo.InvariantCulture, out _)) return false;
                }
                return true;
            }
            catch (InvalidDataException) { return false; }
        }

        internal static void ConvertKnownState(CompositeBlueprintStore.Part part, Geometry.BlueprintSourceDocument evidence,
            Func<string, int> hash, int? worldLevel, Func<string, bool> supportsMaterialSeed,
            ref Dictionary<int, Type> ordinaryStateFields)
        {
            var original = part.foreignData;
            if (original == null || original.format != "blueprint" && original.format != "vbuild" || original.chance != null ||
                evidence == null || evidence.Id != part.sourceDocumentId || evidence.Format != original.format ||
                !Geometry.BlueprintForeignZdo.TryRead(original.zdo, out var state) || state.ConnectionType.HasValue) return;
            string row = evidence.PartRow(part.sourceLine);
            if (row == null || !evidence.HasPartAt(part.sourceLine, part.prefabName)) return;
            bool vbuild = original.format == "vbuild";
            if ((vbuild || part.prefabName == "sign") && original.info != null) return;
            // vbuild has no row-scale columns; dropping a ZDO scale would change its geometry.
            // Keep this dialect blocked until its absolute-scale adapter is verified.
            if (vbuild && (state.HasHash(hash("scale")) || state.HasHash(hash("scaleScalar")))) return;
            string[] fields = PartFields(row, vbuild && !row.Contains(";"));
            int dataIndex = vbuild ? 8 : 13;
            if (fields.Length <= dataIndex || fields[dataIndex] != original.zdo ||
                fields.Length > dataIndex + 1 && HasData(fields[dataIndex + 1]) && fields[dataIndex + 1] != "1" ||
                (!vbuild && part.prefabName != "sign" && (HasData(fields[9]) ? fields[9] : null) != original.info) ||
                (!vbuild && part.prefabName == "sign" && BlueprintExchangeFormat.DecodeSignText(fields[9], allowLegacyLiteral: true) != part.signText)) return;
            var consumed = new HashSet<KeyValuePair<int, Type>>();
            Geometry.BlueprintForeignPartData contents = null;
            string text = part.signText;
            if (part.prefabName == "itemstand" || part.prefabName == "itemstandh")
            {
                string boundItemInfo = null;
                if (state.TryGet<int>(hash("item"), out _))
                {
                    if (original.info == null) return;
                    try { boundItemInfo = BlueprintExchangeFormat.DecodeSignText(original.info); }
                    catch (InvalidDataException) { return; }
                }
                if (!Geometry.BlueprintItemStandContents.TryConvertLegacy(state, hash, worldLevel, out contents, out consumed, boundItemInfo)) return;
            }
            else if (part.prefabName == "sign")
            {
                // With ZDO present, legacy Info is not a text fallback.
                if (!state.HasHash(hash("text")) && text != null) return;
                if (state.HasHash(hash("text")))
                {
                    if (!state.TryGet<string>(hash("text"), out text) || !Geometry.BlueprintEditorPart.IsValidSignText(text)) return;
                    consumed.Add(new KeyValuePair<int, Type>(hash("text"), typeof(string)));
                }
                if (state.HasHash(hash("author")))
                {
                    if (!state.TryGet<string>(hash("author"), out _)) return;
                    consumed.Add(new KeyValuePair<int, Type>(hash("author"), typeof(string))); // Native Sign.SetText assigns the new author.
                }
            }
            else if (original.info != null) return;
            int? materialSeed = part.materialSeed;
            int materialKey = hash("RandMatSeed");
            if (state.HasHash(materialKey))
            {
                if (supportsMaterialSeed?.Invoke(part.prefabName) != true ||
                    !state.TryGet<int>(materialKey, out int seed) || seed < 0 ||
                    materialSeed.HasValue && materialSeed.Value != seed) return;
                materialSeed = seed;
                consumed.Add(new KeyValuePair<int, Type>(materialKey, typeof(int)));
            }
            // Construction creates NEW ordinary native pieces: no inherited ownership, stale support,
            // damage, invulnerability, AI overrides or network lifetime flags. All originals remain in
            // the exact source document. This is NOT an administrator-state restoration adapter.
            // Hash callbacks belong to this conversion pass; never share the table across hosts.
            var ordinary = ordinaryStateFields ?? (ordinaryStateFields = new Dictionary<int, Type>
            {
                [hash("health")] = typeof(float),
                [hash("support")] = typeof(float),
                [hash("creator")] = typeof(long),
                [hash("scale")] = typeof(Geometry.Point3),
                [hash("scaleScalar")] = typeof(float),
                [hash("HasFields")] = typeof(int),
                [hash("HasFieldsWearNTear")] = typeof(int),
                [hash("WearNTear.m_health")] = typeof(float),
                [hash("HasFieldsStaticTarget")] = typeof(int),
                [hash("StaticTarget.m_randomTarget")] = typeof(int)
            });
            foreach (var key in state.Keys)
            {
                if (consumed.Contains(key)) continue;
                if (!ordinary.TryGetValue(key.Key, out Type expected) || key.Value != expected) return;
                if (expected == typeof(int) && (!state.TryGet<int>(key.Key, out int flag) || flag < 0 || flag > 1)) return;
            }
            part.signText = text;
            part.materialSeed = materialSeed;
            part.foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(contents);
        }

        internal static CompositeBlueprintStore.Blueprint DetachedCopy(CompositeBlueprintStore.Blueprint source)
        {
            if (source == null || !source.IsExternal || source.importError != null)
                throw new InvalidDataException("$buildworks_import.document_invalid");
            if (source.importAbsoluteScale) throw new InvalidDataException("$buildworks_import.scale_pending");
            var result = new CompositeBlueprintStore.Blueprint
            {
                id = Guid.NewGuid().ToString("N"),
                name = source.name,
                category = source.category,
                prefabName = source.prefabName,
                anchors = new List<CompositeBlueprintStore.VectorData>(source.anchors),
                customSnapPoints = source.customSnapPoints == null ? null : new List<CompositeBlueprintStore.VectorData>(source.customSnapPoints),
                supports = new List<CompositeBlueprintStore.Support>(),
                previewYaw = source.previewYaw,
                previewPitch = source.previewPitch,
                previewZoom = source.previewZoom,
                sourceDocuments = CompositeBlueprintStore.CloneSources(source.sourceDocuments),
                libraryOrigins = CompositeBlueprintStore.CloneLibraryOrigins(source.libraryOrigins),
                peerOrigins = CompositeBlueprintStore.ClonePeerOrigins(source.peerOrigins)
            };
            var ids = source.parts.Select(part => part.stableId).Concat(source.groups.Select(group => group.stableId))
                .Concat((source.supports ?? new List<CompositeBlueprintStore.Support>()).Select(support => support.stableId))
                .ToDictionary(id => id, id => Guid.NewGuid().ToString("N"), StringComparer.Ordinal);
            result.primaryPartId = source.primaryPartId == null ? null : ids[source.primaryPartId];
            result.primaryGroupId = source.primaryGroupId == null ? null : ids[source.primaryGroupId];
            result.placementSupportId = source.placementSupportId == null ? null : ids[source.placementSupportId];
            foreach (CompositeBlueprintStore.Support support in source.supports ?? new List<CompositeBlueprintStore.Support>())
                result.supports.Add(new CompositeBlueprintStore.Support
                { stableId = ids[support.stableId], name = support.name, position = support.position, rotation = support.rotation });
            foreach (CompositeBlueprintStore.Group group in source.groups)
                result.groups.Add(new CompositeBlueprintStore.Group
                {
                    stableId = ids[group.stableId],
                    name = group.name,
                    editorVisible = group.editorVisible,
                    editorLocked = group.editorLocked,
                    parentGroupId = group.parentGroupId == null ? null : ids[group.parentGroupId],
                    pivotPartId = group.pivotPartId == null ? null : ids[group.pivotPartId],
                    sourceDocumentId = group.sourceDocumentId, sourceLayerId = group.sourceLayerId
                });
            foreach (CompositeBlueprintStore.Part part in source.parts)
                result.parts.Add(new CompositeBlueprintStore.Part
                {
                    stableId = ids[part.stableId],
                    prefabName = part.prefabName,
                    displayName = part.displayName,
                    position = part.position,
                    rotation = part.rotation,
                    scale = part.scale,
                    baseScale = part.baseScale,
                    metadata = CompositeBlueprintStore.PrefabMetadata.FromGeometry(part.metadata?.ToGeometry()),
                    materialSeed = part.materialSeed,
                    scaleIsAbsolute = part.scaleIsAbsolute,
                    parentGroupId = part.parentGroupId == null ? null : ids[part.parentGroupId],
                    editorVisible = part.editorVisible,
                    editorLocked = part.editorLocked,
                    signText = part.signText,
                    sourceDocumentId = part.sourceDocumentId,
                    sourceLine = part.sourceLine,
                    sourceRecord = part.sourceRecord,
                    sourceFrameApplied = part.sourceFrameApplied,
                    originalPrefabName = part.originalPrefabName,
                    foreignData = CompositeBlueprintStore.ForeignPartData.FromGeometry(part.foreignData?.ToGeometry())
                });
            if (!CompositeBlueprintStore.Valid(result)) throw new InvalidDataException("$buildworks_import.document_invalid");
            return result;
        }

        private static bool HasData(string value) => value != "" && value != "\"\"" && value != "null";

        // Legacy BuildShare slots are positional: a blank numeric slot means zero.
        private static string[] PartFields(string row, bool legacyVbuild)
        {
            if (!legacyVbuild) return row.Split(';');
            string[] fields = row.TrimStart().Split(new[] { ' ', '\t' }, StringSplitOptions.None);
            int count = fields.Length;
            while (count > 10 && fields[count - 1].Length == 0) --count;
            if (count != fields.Length) Array.Resize(ref fields, count);
            return fields;
        }

        private static float Number(string value, bool legacyVbuild = false)
        {
            if (legacyVbuild)
            {
                if (value.Length == 0) return 0;
                value = value.Replace(',', '.');
            }
            // Repeated exact constants dominate piece transforms; all other syntax keeps the host parser.
            if (value == "0") return 0f;
            if (value == "1") return 1f;
            if (value == "-1") return -1f;
            float result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidDataException("$buildworks_import.transform");
            return result;
        }
    }
}
