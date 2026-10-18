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
                owner.evictionStamp = owner.LastSubmission; owner.evictionPending = owner.evictionReceived = owner.evictionRelease = true;
                owner.evictionReply = owner.coldVelocity; return false;
            }
            RequestSnapshotVelocity(owner, owner.LastSubmission, true); return false;
        }

        private void RequestSnapshotVelocity(SnapshotChannel owner, BlueprintFogSnapshotState.Submission stamp, bool release)
        {
            owner.evictionStamp = stamp; owner.evictionRelease = release; owner.evictionPending = owner.evictionNativePending = true;
            owner.evictionReceived = false; owner.evictionReply = null;
            try
            {
                owner.evictionRequest = AsyncGPUReadback.Request(owner.Velocity, 0, request => {
                    try
                    {
                        ushort[] copy = null;
                        if (!request.hasError && request.layerCount > 0 && request.layerCount <= 80)
                        {
                            copy = new ushort[VelocityWords]; int offset = 0;
                            for (int layer = 0; layer < request.layerCount; ++layer)
                            {
                                var data = request.GetData<ushort>(layer);
                                if (data.Length == 0 || data.Length > copy.Length - offset) { copy = null; break; }
                                Unity.Collections.NativeArray<ushort>.Copy(data, 0, copy, offset, data.Length); offset += data.Length;
                            }
                            if (offset != VelocityWords) copy = null;
                        }
                        owner.ReceiveVelocity(stamp, copy);
                    }
                    catch { owner.ReceiveVelocity(stamp, null); }
                    finally { owner.evictionNativePending = false; }
                });
            }
            catch { owner.evictionNativePending = false; snapshotTeardown = true; owner.Error = "Fog velocity readback submission failed; state retained."; throw; }
        }

        private void PollSnapshotVelocity()
        {
            if (snapshotTeardown) return;
            foreach (var owner in snapshotSlots)
            {
                if (owner == null || !owner.evictionPending || owner.evictionNativePending || !owner.evictionReceived) continue;
                if (owner.pendingBatch) continue;
                if (owner.evictionReply == null || owner.evictionReply.Length != VelocityWords || !SnapshotChannel.Same(owner.evictionStamp, owner.LastSubmission))
                { owner.Error = "Fog velocity page was not confirmed; GPU state retained."; snapshotTeardown = true; continue; }
                // The solver and its five CPU integrator values remain alive; maps and ledgers never move.
                owner.coldVelocity = owner.evictionReply; owner.coldVelocityStamp = owner.evictionStamp; owner.evictionReply = null;
                if (owner.evictionRelease) { owner.Velocity.Release(); UnityEngine.Object.DestroyImmediate(owner.Velocity); owner.Velocity = null; }
                owner.evictionPending = owner.evictionReceived = false;
            }
        }

        internal bool EnsureSnapshotVelocity(SnapshotChannel owner)
        {
            RequireOwner(owner);
            if (snapshotTeardown) throw new InvalidOperationException("Stopped fog pool cannot restore velocity.");
            if (owner.evictionPending) return false;
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
                return false;
            }
            // Keep the RAM page until the next confirmed step; an uncertain upload must retain both copies.
            try
            {
                owner.Velocity = BlueprintFogSimulation.AllocateField(new Vector3Int(96, 80, 80), "Fog restored velocity " + owner.Slot);
                arena.VelocityUpload.SetPixelData(owner.coldVelocity, 0); arena.VelocityUpload.Apply(false, false);
                Graphics.CopyTexture(arena.VelocityUpload, owner.Velocity);
                owner.velocityUse = checked(++velocityUse); return true;
            }
            catch { snapshotTeardown = true; owner.Error = "Fog velocity upload failed; RAM and GPU state retained."; throw; }
        }
    }
}
