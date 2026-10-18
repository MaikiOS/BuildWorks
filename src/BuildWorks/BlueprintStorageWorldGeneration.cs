using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    // Capture/start/finish run on the owning game thread. Only detached filesystem data enters Task.Run.
    // No save request, transfer, settlement, marker removal or refund is performed here.
    internal sealed class BlueprintStorageWorldGeneration
    {
        private readonly ZNet network;
        private readonly World world;
        private readonly long worldId;
        private readonly string directory;
        private readonly int threadId;
        private readonly Func<bool> custodyUnchanged;
        private readonly NativeWorldStorageReadback.Expectation expected;
        private Task<ReadResult> read;
        private CancellationToken cancellation;
        private bool refused;
        internal readonly Guid Operation;
        internal readonly uint Generation;
        internal bool IsCompleted => read != null && read.IsCompleted;

        private sealed class ReadResult
        {
            internal readonly bool Passed;
            internal readonly NativeWorldStorageReadback.Evidence Evidence;
            internal readonly string Reason;
            internal ReadResult(bool passed, NativeWorldStorageReadback.Evidence evidence, string reason)
            { Passed = passed; Evidence = evidence; Reason = reason; }
        }

        private BlueprintStorageWorldGeneration(Guid operation, Func<bool> custodyUnchanged,
            Func<long, NativeWorldStorageReadback.Expectation> expectation)
        {
            Operation = operation; this.custodyUnchanged = custodyUnchanged;
            threadId = Thread.CurrentThread.ManagedThreadId;
            network = ZNet.instance; world = network.GetWorld(); worldId = world.m_uid;
            string nativeDirectory = world.GetSaveDirectory(FileHelpers.FileSource.Local);
            if (!Path.IsPathRooted(nativeDirectory)) throw new InvalidOperationException("Native world directory is not absolute.");
            directory = Path.GetFullPath(nativeDirectory);
            Generation = SaveSystem.GetSaveNumber();
            expected = expectation(worldId);
        }

        internal static bool TryCapture(Guid operation, Guid marker, int prefab, long creator, byte[] items, Func<bool> custodyUnchanged,
            out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(operation, custodyUnchanged, world => new NativeWorldStorageReadback.Expectation(world, marker, prefab, creator,
                ZDOVars.s_creator, BlueprintStorageCapture.TransferMarkerKey, ZDOVars.s_items, items), out expectation, out reason);

        internal static bool TryCaptureReceipt(Guid operation, int prefab, long creator, int markerKey, int receiptKey, byte[] receipt,
            Func<bool> custodyUnchanged, out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(operation, custodyUnchanged, world => NativeWorldStorageReadback.Expectation.ForExactBlob(world, operation, prefab,
                creator, ZDOVars.s_creator, markerKey, receiptKey, receipt), out expectation, out reason);

        private static bool TryCapture(Guid operation, Func<bool> custodyUnchanged, Func<long, NativeWorldStorageReadback.Expectation> factory,
            out BlueprintStorageWorldGeneration expectation, out string reason)
        {
            expectation = null; reason = null;
            try
            {
                if (operation == Guid.Empty || custodyUnchanged == null || !ZNet.instance || !ZNet.instance.IsServer() ||
                    ZNet.instance.IsSaving() || ZNet.instance.GetWorld() == null ||
                    ZNet.instance.GetWorld().m_fileSource != FileHelpers.FileSource.Local || !custodyUnchanged())
                    throw new InvalidOperationException("Readback requires the current Local host, an idle save and exact completed item custody.");
                var candidate = new BlueprintStorageWorldGeneration(operation, custodyUnchanged, factory);
                if (!candidate.Valid()) throw new InvalidOperationException("World generation, source or custody changed during capture.");
                expectation = candidate; return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        private bool Valid() => Thread.CurrentThread.ManagedThreadId == threadId && !refused && network &&
            ReferenceEquals(ZNet.instance, network) && network.IsServer() && !network.IsSaving() &&
            ReferenceEquals(network.GetWorld(), world) && world.m_uid == worldId && world.m_fileSource == FileHelpers.FileSource.Local &&
            Generation != 0 && SaveSystem.GetSaveNumber() == Generation &&
            Path.GetFullPath(world.GetSaveDirectory(FileHelpers.FileSource.Local)) == directory &&
            SaveSystem.MainFwl2FileName() == FileName(".fwl2") && SaveSystem.MainChunksFileName() == FileName(".chunks") &&
            SaveSystem.MainOKFileName() == FileName(".ok") && SaveSystem.MainDb2FileName() == FileName(".db2") && custodyUnchanged();

        private string FileName(string extension) => "_main." + Generation.ToString(CultureInfo.InvariantCulture) + extension;

        internal bool TryStart(CancellationToken token, out string reason)
        {
            reason = null;
            try
            {
                if (read != null || token.IsCancellationRequested || !Valid())
                    throw new InvalidOperationException("World readback was already started, cancelled or its captured context changed.");
                cancellation = token;
                string root = directory; uint generation = Generation; var snapshot = expected;
                read = Task.Run(() =>
                {
                    bool passed = NativeWorldStorageFiles.TryVerify(root, generation, snapshot, out var evidence, out var error, token);
                    return new ReadResult(passed, evidence, error);
                });
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal bool TryFinish(out NativeWorldStorageReadback.Evidence evidence, out string reason)
        {
            evidence = null; reason = null;
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            { reason = "World readback must be accepted on its owning game thread."; return false; }
            if (read == null || !read.IsCompleted)
            { reason = "World readback is not complete."; return false; }
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (read.IsFaulted) throw read.Exception.GetBaseException();
                var result = read.Result;
                if (!result.Passed || !Valid()) throw new InvalidOperationException(result.Reason ?? "World generation or item custody changed during readback.");
                evidence = result.Evidence; return true;
            }
            catch (Exception error) { refused = true; reason = error.Message; return false; }
        }
    }
}
