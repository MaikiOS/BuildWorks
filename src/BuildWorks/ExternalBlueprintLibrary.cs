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
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".zip" && extension != ".blueprint" && extension != ".vbuild") continue;
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
            return result;
        }

        private static bool Supported(string name) =>
            name.EndsWith(".blueprint", StringComparison.OrdinalIgnoreCase) ||
            name.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase);

        private static CompositeBlueprintStore.Blueprint Read(Stream stream, long length, string source, ref long budget)
        {
            if (length < 0 || length > MaximumFileBytes || length > budget)
                return Blocked(source, "$buildworks_import.file_limit");
            budget -= length;
            try
            {
                // Never extract archive paths or load DLLs/images from the package.
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
            var blueprint = Blocked(source, null);
            blueprint.id = Identity(source + "\n" + text);
            bool vbuild = source.EndsWith(".vbuild", StringComparison.OrdinalIgnoreCase);
            bool pieces = vbuild, seenPieces = vbuild;
            int lineNumber = 0;
            using (var reader = new StringReader(text.TrimStart('\uFEFF')))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    ++lineNumber; line = line.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '#')
                    {
                        if (line.StartsWith("#Name:", StringComparison.Ordinal)) blueprint.name = line.Substring(6).Trim();
                        else if (line.StartsWith("#Category:", StringComparison.Ordinal)) blueprint.category = line.Substring(10).Trim();
                        else if (line == "#Pieces") { pieces = true; seenPieces = true; }
                        else if (line == "#Terrain") pieces = false;
                        else if (!line.StartsWith("#Creator:", StringComparison.Ordinal) &&
                            !line.StartsWith("#Description:", StringComparison.Ordinal))
                            blueprint.importError = "$buildworks_import.section\t" + line;
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
                        // A quote/semicolon in object data is deliberately not interpreted.
                        if (!vbuild && fields[9] != "" && fields[9] != "\"\"")
                            throw new InvalidDataException("$buildworks_import.object_data");
                        int dataIndex = vbuild ? 8 : 13;
                        if (fields.Length > dataIndex && fields[dataIndex] != "" && fields[dataIndex] != "\"\"")
                            throw new InvalidDataException("$buildworks_import.object_data");
                        if (fields.Length > dataIndex + 1 && fields[dataIndex + 1] != "" && Number(fields[dataIndex + 1]) != 1f)
                            throw new InvalidDataException("$buildworks_import.chance");
                        string prefab = fields[0].Trim();
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
                            scale = new CompositeBlueprintStore.VectorData { x = 1, y = 1, z = 1 }
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
            if (!source.IsExternal || source.importError != null) throw new InvalidDataException("$buildworks_import.fields");
            var result = new CompositeBlueprintStore.Blueprint
            { id = Guid.NewGuid().ToString("N"), name = source.name, category = source.category, prefabName = source.prefabName };
            foreach (CompositeBlueprintStore.Part part in source.parts)
                result.parts.Add(new CompositeBlueprintStore.Part
                {
                    stableId = Guid.NewGuid().ToString("N"),
                    prefabName = part.prefabName,
                    displayName = part.displayName,
                    position = part.position,
                    rotation = part.rotation,
                    scale = part.scale
                });
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
