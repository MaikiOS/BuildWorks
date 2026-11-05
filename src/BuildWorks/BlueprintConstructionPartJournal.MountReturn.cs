using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountReturnIntent = 1, MountReturnBound = 2, MountReturnObserved = 3, MountReturnSaved = 4;

        // Detach/saved-source evidence; credit is recorded separately and never permits source cleanup or structural cancellation here.
        [DataContract] internal sealed class MountReturnRecord
        {
            [DataMember(Order = 0)] internal Guid Operation;
            [DataMember(Order = 1)] internal int Phase;
            [DataMember(Order = 2)] internal uint BeforeGeneration;
            [DataMember(Order = 3)] internal string BeforeInfo;
            [DataMember(Order = 4)] internal string BeforeBytes;
            [DataMember(Order = 5)] internal long ObjectUser;
            [DataMember(Order = 6)] internal uint ObjectId;
            [DataMember(Order = 7)] internal string ObservedInfo;
            [DataMember(Order = 8)] internal string ObservedBytes;
            [DataMember(Order = 9, EmitDefaultValue = false)] internal SavePair Saved;
            [DataMember(Order = 10, EmitDefaultValue = false)] internal string SavedDropHash;
            [DataMember(Order = 11, EmitDefaultValue = false)] internal MountReturnCredit Credit;
            [DataMember(Order = 12, EmitDefaultValue = false)] internal MountCleanup Cleanup;
            [DataMember(Order = 13, EmitDefaultValue = false)] internal Guid SourceEpoch;
        }

        internal MountRecord PrepareMountReturn(Guid project, Guid part, Guid mountOperation, Guid operation, long player,
            uint generation, BlueprintForeignPartData before, BlueprintForeignPartData actualMounted, out bool newlyPrepared)
        {
            newlyPrepared = false;
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMount(project, part, mountOperation);
                if (current.Phase != Committed || current.Player != player || actualMounted == null || actualMounted.ContentsCleared ||
                    actualMounted.Format != BlueprintForeignPartData.NativeItemStandFormat || actualMounted.Info != current.MountedInfo ||
                    actualMounted.Zdo != current.MountedBytes || before?.Format != BlueprintForeignPartData.NativeContainerFormat || before.ContentsCleared)
                    throw new InvalidOperationException("Return intent requires this payer's saved mount and unchanged actual attached item.");
                var candidate = CloneMount(current);
                candidate.Return = new MountReturnRecord { Operation = operation, Phase = MountReturnIntent, BeforeGeneration = generation,
                    BeforeInfo = before.Info, BeforeBytes = before.Zdo };
                ValidateMountReturn(candidate);
                if (current.Return != null)
                {
                    candidate.Revision = current.Revision;
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Return already has intent/evidence; native detach must not replay.");
                    return current;
                }
                RequireMountReturnCapacity(candidate);
                candidate.Revision = checked(current.Revision + 1);
                WriteMount(plan, candidate, true); newlyPrepared = true; return ReadMount(project, part);
            }
        }

        internal MountRecord BindMountReturnIdentity(Guid project, Guid part, Guid operation, long objectUser, uint objectId, Guid sourceEpoch = default)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                var returned = current.Return;
                if (returned.Phase != MountReturnIntent)
                {
                    if (returned.ObjectUser == objectUser && returned.ObjectId == objectId) return current;
                    throw new InvalidOperationException("Return drop identity was already bound; it cannot be replaced.");
                }
                returned.SourceEpoch = sourceEpoch; returned.ObjectUser = objectUser; returned.ObjectId = objectId; returned.Phase = MountReturnBound;
                current.Revision = checked(current.Revision + 1);
                WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }

        internal MountRecord ObserveMountReturn(Guid project, Guid part, Guid operation, BlueprintForeignPartData actualDrop, bool emptyHolder)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                if (!emptyHolder || current.Return.Phase < MountReturnBound || actualDrop == null || actualDrop.ContentsCleared ||
                    actualDrop.Format != BlueprintForeignPartData.NativeItemStandFormat)
                    throw new InvalidOperationException("An empty actual holder and uniquely bound native drop are required.");
                var candidate = CloneMount(current);
                candidate.Return.ObservedInfo = actualDrop.Info; candidate.Return.ObservedBytes = actualDrop.Zdo;
                candidate.Return.Phase = MountReturnObserved;
                if (current.Return.Phase >= MountReturnObserved)
                {
                    candidate.Return.Phase = current.Return.Phase;
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Repeated return observation differs from durable actual-drop evidence.");
                    return current;
                }
                candidate.Revision = checked(current.Revision + 1);
                WriteMount(plan, candidate, true); return ReadMount(project, part);
            }
        }

        internal MountRecord ConfirmMountReturnSaved(Guid project, Guid part, Guid operation, SavePair evidence, string dropHash)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                if (current.Return.Phase < MountReturnObserved)
                    throw new InvalidOperationException("Return must have its actual observed drop before paired-save confirmation.");
                var candidate = CloneMount(current);
                candidate.Return.Saved = evidence; candidate.Return.SavedDropHash = dropHash;
                candidate.Return.Phase = MountReturnSaved;
                if (current.Return.Phase == MountReturnSaved)
                {
                    if (!EncodeMount(candidate).SequenceEqual(EncodeMount(current)))
                        throw new InvalidOperationException("Repeated return save evidence differs.");
                    return current;
                }
                candidate.Revision = checked(current.Revision + 1);
                WriteMount(plan, candidate, true); return ReadMount(project, part);
            }
        }

        private MountRecord RequireMountReturn(Guid project, Guid part, Guid operation)
        {
            var record = ReadMount(project, part);
            if (record?.Return == null || operation == Guid.Empty || record.Return.Operation != operation)
                throw new InvalidOperationException("Durable return operation identity differs.");
            return record;
        }

        private static void ValidateMountReturn(MountRecord mount)
        {
            var r = mount.Return; if (r == null) return;
            if (mount.Phase != Committed || r.Operation == Guid.Empty || r.Operation == mount.Operation || r.Operation == mount.ParentOperation ||
                r.Phase < MountReturnIntent || r.Phase > MountReturnComplete || r.BeforeGeneration == 0 || r.BeforeGeneration == uint.MaxValue ||
                r.BeforeGeneration < mount.Saved.Generation || r.BeforeBytes == null || r.BeforeBytes.Length > MaximumBytes / 4)
                throw new InvalidDataException("Invalid mounted-return identity, generation or inventory budget.");
            if (r.Phase < MountReturnSaved && (r.Saved != null || r.SavedDropHash != null))
                throw new InvalidDataException("Unverified return claims saved-pair evidence.");
            if (r.Phase < MountReturnSaved && (r.Credit != null || r.Cleanup != null))
                throw new InvalidDataException("Unsaved return claims inventory credit.");
            ValidateInventory(r.BeforeInfo, r.BeforeBytes);
            if (BitConverter.ToInt32(Convert.FromBase64String(r.BeforeBytes), 0) != 110)
                throw new InvalidDataException("Return inventory must be the full current native format.");
            if (r.Phase == MountReturnIntent)
            {
                if (r.SourceEpoch != Guid.Empty || r.ObjectUser != 0 || r.ObjectId != 0 || r.ObservedInfo != null || r.ObservedBytes != null)
                    throw new InvalidDataException("Unbound return claims native identity or observation.");
                return;
            }
            if (r.ObjectUser == 0 || r.ObjectId == 0) throw new InvalidDataException("Return drop needs its actual observed native identity.");
            if (r.Phase == MountReturnBound)
            {
                if (r.ObservedInfo != null || r.ObservedBytes != null) throw new InvalidDataException("Bound return falsely claims a loaded packet.");
                return;
            }
            if (r.ObservedInfo == null || r.ObservedInfo.Length > 1024 || r.ObservedBytes == null ||
                r.ObservedBytes.Length > Math.Min(32768, mount.MountedBytes.Length + 64))
                throw new InvalidDataException("Return packet exceeds its pre-detach evidence reservation.");
            var actual = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, r.ObservedInfo, r.ObservedBytes, null);
            var original = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.MountedInfo, mount.MountedBytes, null);
            if (!BlueprintItemStandContents.TryRead(original, name => name.GetStableHashCode(), out var a) ||
                !BlueprintItemStandContents.TryRead(actual, name => name.GetStableHashCode(), out var b) ||
                a.SourceVersion != 110 || b.SourceVersion != 110 || a.Stack != 1 || b.Stack != 1 ||
                actual.Info.Split(':')[3] != "0" || !SameItem(a, b, false, nativeLoadSave: true))
                throw new InvalidDataException("Observed return differs from the exact native-load/save projection of the actual mounted item.");
            if (r.Phase >= MountReturnSaved && (r.Saved == null || r.Saved.BeforeGeneration != r.BeforeGeneration ||
                r.Saved.Generation != r.BeforeGeneration + 1 || !Hash(r.Saved.ProfileHash, 128) ||
                r.Saved.InventoryHash != Digest(Convert.FromBase64String(r.BeforeBytes)) ||
                r.Saved.WorldPayloadHash != Digest(mount.Receipt) || r.SavedDropHash != Digest(Convert.FromBase64String(r.ObservedBytes))))
                throw new InvalidDataException("Return lacks the exact saved empty-holder/drop/profile pair.");
            ValidateMountReturnCredit(mount);
            ValidateMountCleanup(mount);
        }

        private static void RequireMountReturnCapacity(MountRecord intent)
        {
            // Reserve the largest permitted post-load evidence before any detach or identity callback.
            var bound = CloneMount(intent); bound.Revision = long.MaxValue;
            bound.Return.SourceEpoch = Guid.NewGuid(); bound.Return.Phase = MountReturnSaved; bound.Return.ObjectUser = long.MinValue; bound.Return.ObjectId = uint.MaxValue;
            bound.Return.ObservedInfo = new string('\0', 1024);
            bound.Return.ObservedBytes = new string('/', Math.Min(32768, bound.MountedBytes.Length + 64));
            bound.Return.Saved = new SavePair { BeforeGeneration = uint.MaxValue - 1, Generation = uint.MaxValue,
                ProfileHash = new string('F', 128), InventoryHash = new string('F', 64), WorldPayloadHash = new string('F', 64) };
            bound.Return.SavedDropHash = new string('F', 64);
            EncodeMount(bound);
        }
    }
}
