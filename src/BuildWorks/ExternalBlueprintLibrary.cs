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
        private const int MaximumFiles = 2000;
        private const long MaximumTotalBytes = 128L * 1024 * 1024;
        internal const int MaximumPreviewBytes = 4 * 1024 * 1024;

        internal static List<CompositeBlueprintStore.Blueprint> LoadFile(string path)
        {
            var result = new List<CompositeBlueprintStore.Blueprint>();
            long budget = MaximumTotalBytes;
            AppendFile(Path.GetFullPath(path), result, ref budget);
            return result;
        }

        internal static List<CompositeBlueprintStore.Blueprint> Load(string directory)
        {
            var result = new List<CompositeBlueprintStore.Blueprint>();
            if (!Directory.Exists(directory)) return result;
            long budget = MaximumTotalBytes;
            string[] paths;
            try
            {
                if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("$buildworks_import.path");
                paths = Directory.EnumerateFiles(directory).OrderBy(x => x, StringComparer.Ordinal).Take(MaximumFiles + 1).ToArray();
                if (paths.Length > MaximumFiles) throw new InvalidDataException("$buildworks_import.library_limit");
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException)
            { result.Add(Blocked(directory, exception.Message)); return result; }
            foreach (string path in paths)
            {
                if (result.Count >= MaximumFiles) break;
                AppendFile(path, result, ref budget);
            }
            return result;
        }

        private static void AppendFile(string path, List<CompositeBlueprintStore.Blueprint> result, ref long budget)
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
                                string source = Path.GetFileName(path) + "/" + entry.FullName;
                                using (Stream data = entry.Open())
                                    result.Add(Read(data, entry.Length, source, ref budget));
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
                        result.Add(Read(stream, stream.Length, Path.GetFileName(path), ref budget));
                }
                catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is UnauthorizedAccessException || exception is ArgumentException)
                {
                    result.Add(Blocked(Path.GetFileName(path), exception.Message));
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

        private static bool Supported(string name) =>
            name.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase);

        private static CompositeBlueprintStore.Blueprint Read(Stream stream, long length, string source, ref long budget)
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
                    return Parse(new UTF8Encoding(false, true).GetString(bytes.ToArray()), source);
                }
            }
            catch (Exception exception) when (exception is IOException || exception is InvalidDataException || exception is DecoderFallbackException)
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
            if (source.EndsWith(".buildworks.json", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var native = BlueprintExchangeFormat.Decode(text);
                    native.id = Identity(source + "\n" + text);
                    native.importSource = source;
                    native.importPartCount = native.parts.Count;
                    return native;
                }
                catch (InvalidDataException exception) { return Blocked(source, exception.Message); }
            }
            var blueprint = Blocked(source, null);
            blueprint.id = Identity(source + "\n" + text);
            bool vbuild = source.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase);
            bool pieces = vbuild, seenPieces = vbuild, snapPoints = false;
            int lineNumber = 0;
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
                            !line.StartsWith("#Description:", StringComparison.Ordinal))
                            blueprint.importError = "$buildworks_import.section\t" + line;
                        continue;
                    }
                    if (snapPoints)
                    {
                        try
                        {
                            string[] coordinates = line.Split(';');
                            if (coordinates.Length != 3) throw new InvalidDataException("$buildworks_import.fields");
                            var point = new CompositeBlueprintStore.VectorData
                            { x = Number(coordinates[0]), y = Number(coordinates[1]), z = Number(coordinates[2]) };
                            if (Math.Abs(point.x) > 512 || Math.Abs(point.y) > 512 || Math.Abs(point.z) > 512)
                                throw new InvalidDataException("$buildworks_import.transform");
                            if (blueprint.customSnapPoints == null) blueprint.customSnapPoints = new List<CompositeBlueprintStore.VectorData>();
                            if (blueprint.customSnapPoints.Count >= 512) throw new InvalidDataException("$buildworks_import.fields");
                            blueprint.customSnapPoints.Add(point);
                        }
                        catch (Exception exception) when (exception is InvalidDataException || exception is FormatException || exception is OverflowException)
                        { if (blueprint.importError == null) blueprint.importError = exception.Message + "\t" + lineNumber; }
                        continue;
                    }
                    if (!pieces) continue; // terrain has no execution path
                    ++blueprint.importPartCount;
                    try
                    {
                        if (blueprint.importPartCount > CompositeBlueprintStore.MaximumParts)
                            throw new InvalidDataException("$buildworks_import.part_limit");
                        // BuildShare uses whitespace; Infinity Hammer also writes a
                        // semicolon dialect. Both share the same eight core fields.
                        string[] fields = vbuild && !line.Contains(";")
                            ? line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries) : line.Split(';');
                        int basic = vbuild ? 8 : 10;
                        if (fields.Length < basic || fields.Length > (vbuild ? 10 : 15) ||
                            (!vbuild && (fields.Length == 11 || fields.Length == 12)))
                            throw new InvalidDataException("$buildworks_import.fields");
                        string prefab = fields[0].Trim();
                        // PlanBuild reuses info for doors, inventory and signs. Only its exact native sign is unambiguous.
                        if (!vbuild && prefab != "sign" && fields[9] != "" && fields[9] != "\"\"" && fields[9] != "null")
                            throw new InvalidDataException("$buildworks_import.object_data");
                        string signText = !vbuild && prefab == "sign" ? BlueprintExchangeFormat.DecodeSignText(fields[9]) : null;
                        int dataIndex = vbuild ? 8 : 13;
                        if (fields.Length > dataIndex && fields[dataIndex] != "" && fields[dataIndex] != "\"\"")
                            throw new InvalidDataException("$buildworks_import.object_data");
                        if (fields.Length > dataIndex + 1 && fields[dataIndex + 1] != "" && Number(fields[dataIndex + 1]) != 1f)
                            throw new InvalidDataException("$buildworks_import.chance");
                        if (prefab.Length == 0 || prefab.Length > 128 || prefab.Any(c => char.IsControl(c) || c == '/' || c == '\\'))
                            throw new InvalidDataException("$buildworks_import.fields");
                        int p = vbuild ? 5 : 2, q = vbuild ? 1 : 5;
                        var part = new CompositeBlueprintStore.Part
                        {
                            stableId = blueprint.id + "_" + blueprint.importPartCount,
                            prefabName = prefab,
                            displayName = prefab,
                            position = new CompositeBlueprintStore.VectorData { x = Number(fields[p]), y = Number(fields[p + 1]), z = Number(fields[p + 2]) },
                            rotation = new CompositeBlueprintStore.QuaternionData { x = Number(fields[q]), y = Number(fields[q + 1]), z = Number(fields[q + 2]), w = Number(fields[q + 3]) },
                            scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 },
                            signText = signText
                        };
                        if (Math.Abs(part.position.x) > 512 || Math.Abs(part.position.y) > 512 || Math.Abs(part.position.z) > 512)
                            throw new InvalidDataException("$buildworks_import.transform");
                        if (!vbuild && fields.Length >= 13)
                            part.scale = new CompositeBlueprintStore.VectorData { x = Number(fields[10]), y = Number(fields[11]), z = Number(fields[12]) };
                        double norm = Math.Sqrt((double)part.rotation.x * part.rotation.x + (double)part.rotation.y * part.rotation.y +
                            (double)part.rotation.z * part.rotation.z + (double)part.rotation.w * part.rotation.w);
                        if (norm < .999 || norm > 1.001 || !CompositeBlueprintStore.ValidScale(part.scale))
                            throw new InvalidDataException("$buildworks_import.transform");
                        part.rotation.x = (float)(part.rotation.x / norm);
                        part.rotation.y = (float)(part.rotation.y / norm);
                        part.rotation.z = (float)(part.rotation.z / norm);
                        part.rotation.w = (float)(part.rotation.w / norm);
                        blueprint.parts.Add(part);
                    }
                    catch (Exception exception) when (exception is InvalidDataException || exception is FormatException || exception is OverflowException)
                    { if (blueprint.importError == null) blueprint.importError = exception.Message + "\t" + lineNumber; }
                }
            }
            if (!seenPieces || blueprint.importPartCount < 2) blueprint.importError = "$buildworks_import.part_limit";
            if (blueprint.name.Length == 0 || blueprint.name.Length > 48 || blueprint.category.Length == 0 || blueprint.category.Length > 24 ||
                (blueprint.name + blueprint.category).Any(c => char.IsControl(c) || c == '<' || c == '>'))
                blueprint.importError = "$buildworks_store.invalid_metadata";
            if (blueprint.importError != null) blueprint.parts.Clear(); // no partial construction can be used
            else blueprint.prefabName = blueprint.parts[0].prefabName;
            return blueprint;
        }

        internal static CompositeBlueprintStore.Blueprint DetachedCopy(CompositeBlueprintStore.Blueprint source)
        {
            if (source == null || !source.IsExternal || source.importError != null)
                throw new InvalidDataException("$buildworks_import.fields");
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
                previewZoom = source.previewZoom
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
                    pivotPartId = group.pivotPartId == null ? null : ids[group.pivotPartId]
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
                    parentGroupId = part.parentGroupId == null ? null : ids[part.parentGroupId],
                    editorVisible = part.editorVisible,
                    editorLocked = part.editorLocked,
                    signText = part.signText
                });
            if (!CompositeBlueprintStore.Valid(result)) throw new InvalidDataException("$buildworks_import.fields");
            return result;
        }

        private static float Number(string value)
        {
            float result = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(result) || float.IsInfinity(result)) throw new InvalidDataException("$buildworks_import.transform");
            return result;
        }
    }
}
