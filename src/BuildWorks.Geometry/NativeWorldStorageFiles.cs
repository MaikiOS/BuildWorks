using System;
using System.Globalization;
using System.IO;
using System.Threading;

namespace OstrixMods.BuildWorks.Geometry
{
    // The caller supplies the trusted native world's directory and committed generation.
    // File evidence is not a server acknowledgement or a complete DB2/load validation.
    public static class NativeWorldStorageFiles
    {
        public static bool TryVerify(string directory, uint generation, NativeWorldStorageReadback.Expectation expected,
            out NativeWorldStorageReadback.Evidence evidence, out string reason, CancellationToken cancellation = default)
        {
            evidence = null; reason = null;
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathRooted(directory) || generation == 0)
                    throw new InvalidDataException("A canonical world directory and nonzero committed generation are required.");
                string root = Path.GetFullPath(directory);
                CheckRoot(root);
                cancellation.ThrowIfCancellationRequested();
                string prefix = "_main." + generation.ToString(CultureInfo.InvariantCulture);
                using (var metadata = Open(root, prefix + ".fwl2"))
                using (var mapping = Open(root, prefix + ".chunks"))
                using (var completion = Open(root, prefix + ".ok"))
                using (var database = Open(root, prefix + ".db2"))
                using (var header = new BinaryReader(database, System.Text.Encoding.UTF8, true))
                {
                    if (database.Length < 12 || database.Length > NativeWorldStorageReadback.MaximumWorldBytes || header.ReadInt32() != 41)
                        throw new InvalidDataException("Invalid committed DB2 header or budget.");
                    double time = header.ReadDouble();
                    if (double.IsNaN(time) || double.IsInfinity(time)) throw new InvalidDataException("Invalid DB2 world time.");
                    if (!NativeWorldStorageReadback.TryVerify(metadata, mapping, completion,
                        name => Open(root, name), expected, out var result, out reason, cancellation)) return false;
                    CheckRoot(root);
                    cancellation.ThrowIfCancellationRequested();
                    evidence = result; return true;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                error is ArgumentException || error is OverflowException || error is OperationCanceledException)
            { evidence = null; reason = error.Message; return false; }
        }

        private static void CheckRoot(string root)
        {
            for (var parent = new DirectoryInfo(root); parent != null; parent = parent.Parent)
                if (!parent.Exists || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw new InvalidDataException("World directory is unavailable or redirected.");
        }

        private static FileStream Open(string root, string name)
        {
            if (Path.GetFileName(name) != name) throw new InvalidDataException("Unexpected world file name.");
            string path = Path.Combine(root, name);
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidDataException("World file is redirected.");
            // Published generation files are immutable. Allow native cleanup of an old generation
            // while retaining this handle; denying delete would make an overlapping world save fail.
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 4096, FileOptions.SequentialScan);
        }
    }
}
