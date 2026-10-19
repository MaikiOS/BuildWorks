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
            => TryReadGeneration(directory, generation, expected, null, out evidence, out _, out reason, cancellation);

        public static bool TryVerifyAbsence(string directory, uint generation, NativeWorldStorageReadback.AbsenceExpectation expected,
            out NativeWorldStorageReadback.AbsenceEvidence evidence, out string reason, CancellationToken cancellation = default)
            => TryReadGeneration(directory, generation, null, expected, out _, out evidence, out reason, cancellation);

        private static bool TryReadGeneration(string directory, uint generation, NativeWorldStorageReadback.Expectation expected,
            NativeWorldStorageReadback.AbsenceExpectation absent, out NativeWorldStorageReadback.Evidence evidence,
            out NativeWorldStorageReadback.AbsenceEvidence absence, out string reason, CancellationToken cancellation)
        {
            evidence = null; absence = null; reason = null;
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
                    if (database.Length < 12 || database.Length > NativeWorldStorageReadback.MaximumWorldBytes)
                        throw new InvalidDataException("Invalid committed DB2 header or budget.");
                    int version = header.ReadInt32();
                    if (!NativeWorldStorageReadback.SupportedVersion(version)) throw new InvalidDataException("Unsupported DB2 version.");
                    double time = header.ReadDouble();
                    if (double.IsNaN(time) || double.IsInfinity(time)) throw new InvalidDataException("Invalid DB2 world time.");
                    NativeWorldStorageReadback.Evidence result = null;
                    NativeWorldStorageReadback.AbsenceEvidence absentResult = null;
                    if (absent != null)
                    {
                        if (!NativeWorldStorageReadback.TryVerifyAbsence(metadata, mapping, completion,
                            name => Open(root, name), absent, out absentResult, out reason, cancellation)) return false;
                    }
                    else if (!NativeWorldStorageReadback.TryVerify(metadata, mapping, completion,
                        name => Open(root, name), expected, out result, out reason, cancellation)) return false;
                    if ((result?.FormatVersion ?? absentResult.FormatVersion) != version)
                        throw new InvalidDataException("DB2 and committed generation versions differ.");
                    CheckRoot(root);
                    cancellation.ThrowIfCancellationRequested();
                    evidence = result; absence = absentResult?.SavedAt(generation); return true;
                }
            }
            catch (Exception error) when (error is IOException || error is InvalidDataException || error is UnauthorizedAccessException ||
                error is ArgumentException || error is OverflowException || error is OperationCanceledException)
            { evidence = null; absence = null; reason = error.Message; return false; }
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
