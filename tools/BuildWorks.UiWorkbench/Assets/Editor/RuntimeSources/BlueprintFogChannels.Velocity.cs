using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintFogChannels
    {
        private const int VelocityWords = 96 * 80 * 80 * 4;
        private long velocityUse;
        internal int ResidentSnapshotCount
        {
            get { int count = 0; if (snapshotSlots != null) foreach (var owner in snapshotSlots) if (owner != null && owner.Velocity) ++count; return count; }
        }
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
        internal long VelocityRamBytes => velocityPages?.Bytes ?? 0;
        internal FogVelocityPages.Snapshot ReadVelocityPages() => velocityPages?.Read();
#else
        internal long VelocityRamBytes
        {
            get
            {
                long bytes = 0;
                if (snapshotSlots != null) foreach (var owner in snapshotSlots) if (owner != null)
                { bytes += (owner.coldVelocity?.LongLength ?? 0) * 2; bytes += (owner.evictionReply?.LongLength ?? 0) * 2; }
                return bytes;
            }
        }
#endif

        internal bool PageSnapshotVelocity(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (snapshotTeardown) throw new InvalidOperationException("Stopped fog pool retains velocity ownership.");
            if (!arena.VelocityUpload) throw new InvalidOperationException("Fog pool has no paging reservation.");
            if (!owner.Velocity) return true;
            if (owner.evictionPending || owner.pendingBatch || owner.nativePending || owner.recoveryNativePending || owner.releasePending ||
                owner.State.State != BlueprintFogSnapshotState.Phase.Ready) return false;
            if (owner.coldVelocity != null && SnapshotChannel.Same(owner.coldVelocityStamp, owner.LastSubmission))
            {
                PerfCached();
                owner.evictionStamp = owner.LastSubmission; owner.evictionPending = owner.evictionReceived = owner.evictionRelease = true;
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                owner.evictionLease = owner.coldLease;
#endif
                owner.evictionReply = owner.coldVelocity; return false;
            }
            RequestSnapshotVelocity(owner, owner.LastSubmission, true); return false;
        }

        private void RequestSnapshotVelocity(SnapshotChannel owner, BlueprintFogSnapshotState.Submission stamp, bool release)
        {
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
            var lease = velocityPages.Rent(false);
            owner.evictionLease = lease;
#endif
            owner.evictionStamp = stamp; owner.evictionRelease = release; owner.evictionPending = owner.evictionNativePending = true;
            owner.evictionReceived = false; owner.evictionReply = null;
            try
            {
                PerfRequested(owner, FogPerformanceCounters.Kind.Velocity, stamp, VelocityWords * 2L);
                owner.evictionRequest = AsyncGPUReadback.Request(owner.Velocity, 0, request => {
                    long perfCopy = 0; PerfStart(ref perfCopy); long perfCopied = 0;
                    try
                    {
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                        if (!owner.CanCopyVelocity(stamp, lease)) return;
#endif
                        ushort[] copy = null;
                        if (!request.hasError && request.layerCount > 0 && request.layerCount <= 80)
                        {
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                            copy = lease.Words;
#else
                            copy = new ushort[VelocityWords];
#endif
                            int offset = 0;
                            for (int layer = 0; layer < request.layerCount; ++layer)
                            {
                                var data = request.GetData<ushort>(layer);
                                if (data.Length == 0 || data.Length > copy.Length - offset) { copy = null; break; }
                                Unity.Collections.NativeArray<ushort>.Copy(data, 0, copy, offset, data.Length); offset += data.Length;
                            }
                            if (offset != VelocityWords) copy = null;
                        }
                        owner.ReceiveVelocity(stamp, copy);
#if BUILDWORKS_TEST_STAND || UNITY_EDITOR
                        if (copy != null) perfCopied = VelocityWords * 2L;
#endif
                    }
                    catch { owner.ReceiveVelocity(stamp, null); }
                    finally {
                        PerfElapsed(FogPerformanceCounters.Stage.CallbackCopy, perfCopy);
                        PerfReceived(owner, FogPerformanceCounters.Kind.Velocity, stamp, perfCopied);
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                        if (ReferenceEquals(owner.evictionLease, lease) && SnapshotChannel.Same(owner.evictionStamp, stamp)) owner.evictionNativePending = false;
#else
                        owner.evictionNativePending = false;
#endif
                    }
                });
            }
            catch { PerfFailed(owner, FogPerformanceCounters.Kind.Velocity, stamp); owner.evictionNativePending = false; snapshotTeardown = true; owner.Error = "Fog velocity readback submission failed; state retained."; throw; }
        }

        private void PollSnapshotVelocity()
        {
            if (snapshotTeardown) return;
            foreach (var owner in snapshotSlots)
            {
                if (owner == null || !owner.evictionPending || owner.evictionNativePending || !owner.evictionReceived) continue;
                if (owner.pendingBatch) continue;
                if (owner.evictionReply == null || owner.evictionReply.Length != VelocityWords || !SnapshotChannel.Same(owner.evictionStamp, owner.LastSubmission)
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                    || (owner.LastSubmission.Sequence != 0 && owner.LastCompletion != BlueprintFogSnapshotState.Completion.Committed)
#endif
                    )
                { PerfVelocityAccepted(owner, false); owner.Error = "Fog velocity page was not confirmed; GPU state retained."; snapshotTeardown = true; continue; }
                PerfVelocityAccepted(owner, true);
                // The solver and its five CPU integrator values remain alive; maps and ledgers never move.
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                var previous = owner.coldLease;
                owner.coldLease = owner.evictionLease; owner.evictionLease = null;
#endif
                owner.coldVelocity = owner.evictionReply; owner.coldVelocityStamp = owner.evictionStamp; owner.evictionReply = null;
#if BUILDWORKS_FOG_PAGE_REUSE_CANDIDATE || UNITY_EDITOR
                if (previous != null && !ReferenceEquals(previous, owner.coldLease)) velocityPages.Return(previous);
#endif
                if (owner.evictionRelease) {
                    long perfRelease = 0; PerfStart(ref perfRelease);
                    owner.Velocity.Release(); UnityEngine.Object.DestroyImmediate(owner.Velocity); owner.Velocity = null;
                    PerfElapsed(FogPerformanceCounters.Stage.PageRelease, perfRelease);
                }
                owner.evictionPending = owner.evictionReceived = false;
            }
        }

        internal bool EnsureSnapshotVelocity(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (snapshotTeardown) throw new InvalidOperationException("Stopped fog pool cannot restore velocity.");
            if (owner.evictionPending) { PerfWaiting(); return false; }
            if (owner.Velocity) { owner.velocityUse = checked(++velocityUse); return true; }
            if (owner.pendingBatch || owner.State.State != BlueprintFogSnapshotState.Phase.Ready) return false;
            if (owner.coldVelocity == null || owner.coldVelocity.Length != VelocityWords || !arena.VelocityUpload)
                throw new InvalidOperationException("Fog cold velocity or upload reservation missing.");
            if (ResidentSnapshotCount >= arena.ResidentCapacity)
            {
                SnapshotChannel victim = null;
                foreach (var other in snapshotSlots)
                    if (other != null && other != owner && other.Velocity && !other.evictionPending && !other.pendingBatch && !other.nativePending &&
                        !other.recoveryNativePending && !other.releasePending && other.State.State == BlueprintFogSnapshotState.Phase.Ready &&
                        (victim == null || other.velocityUse < victim.velocityUse)) victim = other;
                if (victim != null)
                {
                    PageSnapshotVelocity(victim); PollSnapshotVelocity();
                    if (ResidentSnapshotCount < arena.ResidentCapacity) return EnsureSnapshotVelocity(owner);
                }
                else PerfNoVictim();
                return false;
            }
            // Keep the RAM page until the next confirmed step; an uncertain upload must retain both copies.
            try
            {
                long perfCreate = 0; PerfStart(ref perfCreate);
                owner.Velocity = BlueprintFogSimulation.AllocateField(new Vector3Int(96, 80, 80), "Fog restored velocity " + owner.Slot);
                PerfElapsed(FogPerformanceCounters.Stage.RestoreCreate, perfCreate);
                long perfUpload = 0; PerfStart(ref perfUpload);
                arena.VelocityUpload.SetPixelData(owner.coldVelocity, 0); arena.VelocityUpload.Apply(false, false);
                PerfElapsed(FogPerformanceCounters.Stage.Upload, perfUpload);
                long perfCopy = 0; PerfStart(ref perfCopy);
                Graphics.CopyTexture(arena.VelocityUpload, owner.Velocity);
                PerfElapsed(FogPerformanceCounters.Stage.CopyEnqueue, perfCopy);
                owner.velocityUse = checked(++velocityUse); return true;
            }
            catch { snapshotTeardown = true; owner.Error = "Fog velocity upload failed; RAM and GPU state retained."; throw; }
        }
    }
}
