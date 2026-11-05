using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace OstrixMods.BuildWorks
{
    // A reviewed foreign file and its complete native backup publish as one directory.
    internal sealed class BlueprintExportPair
    {
        private readonly byte[] native, foreign;
        private readonly string extension;
        private readonly Action<string> afterForeignWrite;
        private readonly object publishGate = new object();
        private bool cancelled;
        internal BlueprintExportPair(byte[] native, byte[] foreign, string extension, Action<string> afterForeignWrite = null)
        {
            if (native == null || foreign == null || extension != ".blueprint" && extension != ".json" && extension != ".yml" && extension != ".oprint" && extension != ".bbp")
                throw new ArgumentException("Invalid paired export.");
            this.native = native; this.foreign = foreign; this.extension = extension; this.afterForeignWrite = afterForeignWrite;
        }
        internal void CancelWrite() { lock (publishGate) cancelled = true; }
        private void CheckCancelled()
        { lock (publishGate) if (cancelled) throw new OperationCanceledException("$buildworks_exchange.plan_cancelled"); }

        internal static (byte[] Native, CompositeBlueprintStore.Blueprint Document) Snapshot(CompositeBlueprintStore.Blueprint source)
        {
            if (source == null || source.importError != null)
                throw new InvalidDataException(source?.importError ?? "$buildworks_import.format");
            var portable = source.ImportCopy();
            if (source.IsExternal)
                using (var hash = SHA256.Create())
                    portable.id = new Guid(hash.ComputeHash(Encoding.UTF8.GetBytes(source.id)).Take(16).ToArray()).ToString("N");
            byte[] bytes = BlueprintExchangeFormat.Encode(portable);
            return (bytes, BlueprintExchangeFormat.Decode(new UTF8Encoding(false, true).GetString(bytes)));
        }

        internal bool TryWrite(Func<(byte[] Native, byte[] Foreign)> currentSnapshot, string directory, string name,
            out string result, out string error)
        {
            result = null; error = null; string temporary = null;
            try
            {
                CheckCancelled();
                var current = currentSnapshot();
                CheckCancelled();
                if (!native.SequenceEqual(current.Native) || !foreign.SequenceEqual(current.Foreign))
                    throw new InvalidDataException("$buildworks_exchange.plan_changed");
                directory = CompositeBlueprintStore.ValidateExchangeDirectory(directory);
                CompositeBlueprintStore.ValidateExportName(name);
                string target = Path.Combine(directory, name);
                if (Directory.Exists(target) || File.Exists(target))
                    throw new IOException("$buildworks_exchange.exists\t" + target);
                string staging = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
                // Unity's Windows Mono file streams cannot use legacy paths of 260 characters or more.
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                    foreach (string folder in new[] { target, staging })
                        if (Path.Combine(folder, name + ".buildworks.json").Length >= 260)
                            throw new IOException("$buildworks_exchange.plan_path\t" + target);
                Directory.CreateDirectory(directory);
                temporary = staging;
                Directory.CreateDirectory(temporary);
                CompositeBlueprintStore.ValidateExchangeDirectory(temporary);
                CheckCancelled();
                Write(Path.Combine(temporary, name + extension), foreign);
                afterForeignWrite?.Invoke(temporary); // Lets tests cancel at the closed first-file boundary, without timing races.
                CheckCancelled();
                Write(Path.Combine(temporary, name + ".buildworks.json"), native);
                // Closing the view and final publication have one ordering, even on different threads.
                lock (publishGate)
                {
                    CheckCancelled();
                    Directory.Move(temporary, target); // Publish both closed files together, never a partial pair.
                    temporary = null; result = target; return true;
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception exception) { error = exception.Message; return false; }
            finally
            {
                if (temporary != null)
                {
                    try
                    {
                        CompositeBlueprintStore.ValidateExchangeDirectory(temporary);
                        foreach (string suffix in new[] { extension, ".buildworks.json" })
                        {
                            string file = Path.Combine(temporary, name + suffix);
                            if (File.Exists(file)) File.Delete(file);
                        }
                        Directory.Delete(temporary, false);
                    }
                    catch (Exception exception) { UnityEngine.Debug.LogWarning("BuildWorks export cleanup: " + exception.Message); }
                }
            }
        }

        private static void Write(string path, byte[] bytes)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes, 0, bytes.Length); stream.Flush(true); }
            if (!File.ReadAllBytes(path).SequenceEqual(bytes)) throw new IOException("$buildworks_import.fields");
        }
    }
}
