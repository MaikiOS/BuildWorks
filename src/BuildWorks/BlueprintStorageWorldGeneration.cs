using System;
using System.Collections.Generic;
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
        private readonly NativeWorldStorageReadback.AbsenceExpectation absent;
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
            internal readonly NativeWorldStorageReadback.AbsenceEvidence Absence;
            internal readonly string Reason;
            internal ReadResult(bool passed, NativeWorldStorageReadback.Evidence evidence, NativeWorldStorageReadback.AbsenceEvidence absence, string reason)
            { Passed = passed; Evidence = evidence; Absence = absence; Reason = reason; }
        }

        private BlueprintStorageWorldGeneration(Guid operation, Func<bool> custodyUnchanged,
            Func<long, NativeWorldStorageReadback.Expectation> expectation, Func<long, NativeWorldStorageReadback.AbsenceExpectation> absence)
        {
            Operation = operation; this.custodyUnchanged = custodyUnchanged;
            threadId = Thread.CurrentThread.ManagedThreadId;
            network = ZNet.instance; world = network.GetWorld(); worldId = world.m_uid;
            string nativeDirectory = world.GetSaveDirectory(FileHelpers.FileSource.Local);
            if (!Path.IsPathRooted(nativeDirectory)) throw new InvalidOperationException("Native world directory is not absolute.");
            directory = Path.GetFullPath(nativeDirectory);
            Generation = SaveSystem.GetSaveNumber();
            expected = expectation?.Invoke(worldId); absent = absence?.Invoke(worldId);
        }

        internal static bool TryCapture(Guid operation, Guid marker, int prefab, long creator, byte[] items, Func<bool> custodyUnchanged,
            out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(operation, custodyUnchanged, world => new NativeWorldStorageReadback.Expectation(world, marker, prefab, creator,
                ZDOVars.s_creator, BlueprintStorageCapture.TransferMarkerKey, ZDOVars.s_items, items), out expectation, out reason);

        internal static bool TryCaptureReceipt(Guid operation, int prefab, long creator, int markerKey, int receiptKey, byte[] receipt,
            Func<bool> custodyUnchanged, out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(operation, custodyUnchanged, world => NativeWorldStorageReadback.Expectation.ForExactBlob(world, operation, prefab,
                creator, ZDOVars.s_creator, markerKey, receiptKey, receipt), out expectation, out reason);

        internal static bool TryCaptureAbsence(Guid operation, int markerKey, IEnumerable<Guid> parts, Func<bool> custodyUnchanged,
            out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(operation, custodyUnchanged, null, out expectation, out reason,
                world => new NativeWorldStorageReadback.AbsenceExpectation(world, markerKey, parts));

        internal static bool TryCaptureBlob(Guid item, int prefab, long payer, int ownerKey, int markerKey, int blobKey, byte[] package, Func<bool> custodyUnchanged,
            out BlueprintStorageWorldGeneration expectation, out string reason)
            => TryCapture(item, custodyUnchanged, world => NativeWorldStorageReadback.Expectation.ForExactBlob(world, item, prefab,
                payer, ownerKey, markerKey, blobKey, package),
                out expectation, out reason);

        private static bool TryCapture(Guid operation, Func<bool> custodyUnchanged, Func<long, NativeWorldStorageReadback.Expectation> factory,
            out BlueprintStorageWorldGeneration expectation, out string reason, Func<long, NativeWorldStorageReadback.AbsenceExpectation> absence = null)
        {
            expectation = null; reason = null;
            try
            {
                if (operation == Guid.Empty || custodyUnchanged == null || !ZNet.instance || !ZNet.instance.IsServer() ||
                    ZNet.instance.IsSaving() || ZNet.instance.GetWorld() == null ||
                    ZNet.instance.GetWorld().m_fileSource != FileHelpers.FileSource.Local || !custodyUnchanged())
                    throw new InvalidOperationException("Readback requires the current Local host, an idle save and exact completed item custody.");
                var candidate = new BlueprintStorageWorldGeneration(operation, custodyUnchanged, factory, absence);
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
                var absentSnapshot = absent;
                read = Task.Run(() =>
                {
                    if (absentSnapshot != null)
                    {
                        bool missing = NativeWorldStorageFiles.TryVerifyAbsence(root, generation, absentSnapshot,
                            out var proof, out var failure, token);
                        return new ReadResult(missing, null, proof, failure);
                    }
                    bool passed = NativeWorldStorageFiles.TryVerify(root, generation, snapshot, out var evidence, out var error, token);
                    return new ReadResult(passed, evidence, null, error);
                });
                return true;
            }
            catch (Exception error) { reason = error.Message; return false; }
        }

        internal bool TryFinish(out NativeWorldStorageReadback.Evidence evidence, out string reason)
        {
            evidence = null;
            if (!TryFinishRead(false, out var result, out reason)) return false;
            evidence = result.Evidence; return true;
        }

        internal bool TryFinishAbsence(out NativeWorldStorageReadback.AbsenceEvidence evidence, out string reason)
        {
            evidence = null;
            if (!TryFinishRead(true, out var result, out reason)) return false;
            evidence = result.Absence; return true;
        }

        private bool TryFinishRead(bool wantAbsence, out ReadResult completed, out string reason)
        {
            completed = null; reason = null;
            if (Thread.CurrentThread.ManagedThreadId != threadId)
            { reason = "World readback must be accepted on its owning game thread."; return false; }
            if (read == null || !read.IsCompleted)
            { reason = "World readback is not complete."; return false; }
            try
            {
                cancellation.ThrowIfCancellationRequested();
                if (read.IsFaulted) throw read.Exception.GetBaseException();
                var result = read.Result;
                if (wantAbsence != (absent != null) || !result.Passed || !Valid() ||
                    (wantAbsence ? result.Absence == null || result.Absence.Generation != Generation : result.Evidence == null))
                    throw new InvalidOperationException(result.Reason ?? "World generation, evidence kind or item custody changed during readback.");
                completed = result; return true;
            }
            catch (Exception error) { refused = true; reason = error.Message; return false; }
        }
    }
}
