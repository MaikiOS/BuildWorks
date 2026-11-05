using System;
using System.IO;
using System.Runtime.Serialization;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountReturnComplete = 5;
        internal static readonly int MountReturnMarkerKey = "ostrix_buildworks_mount_return_v1".GetStableHashCode();
        [DataContract] internal sealed class MountCleanup
        {
            [DataMember(Order = 0)] internal uint BeforeGeneration;
            [DataMember(Order = 1)] internal string ProfileHash;
            [DataMember(Order = 2)] internal bool Dispatched;
            [DataMember(Order = 3)] internal uint SavedGeneration;
        }
        internal MountRecord BeginMountCleanup(Guid project, Guid part, Guid operation, uint generation,
            NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                RequireMountReceipt(current, profile);
                if (current.Return.Phase != MountReturnSaved || current.Return.Credit?.Phase != MountProfileCredited ||
                    generation < current.Return.Saved.Generation || generation == uint.MaxValue)
                    throw new InvalidOperationException("Cleanup requires settled credit and its canonical payer receipt.");
                if (current.Return.Cleanup != null) return current;
                current.Return.Cleanup = new MountCleanup { BeforeGeneration = generation, ProfileHash = profile.ProfileHash };
                var maximum = CloneMount(current); maximum.Revision = long.MaxValue;
                maximum.Return.Phase = MountReturnComplete; maximum.Return.Cleanup.Dispatched = true;
                maximum.Return.Cleanup.SavedGeneration = generation + 1;
                EncodeMount(maximum);
                current.Revision = checked(current.Revision + 1); WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }
        internal MountRecord DispatchMountCleanup(Guid project, Guid part, Guid operation)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                if (current.Return.Phase != MountReturnSaved || current.Return.Cleanup == null || current.Return.Cleanup.Dispatched)
                    throw new InvalidOperationException("Cleanup dispatch already attempted or not durably prepared; never repeat destruction.");
                current.Return.Cleanup.Dispatched = true;
                current.Revision = checked(current.Revision + 1); WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }
        internal MountRecord CompleteMountReturn(Guid project, Guid part, Guid operation,
            NativeWorldStorageReadback.AbsenceEvidence absence, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                RequireMountReceipt(current, profile); var cleanup = current.Return.Cleanup;
                if (cleanup == null || !cleanup.Dispatched || absence == null || !absence.CompleteMappedScan ||
                    absence.World != current.World || absence.Generation <= cleanup.BeforeGeneration || absence.MarkerSets.Count != 1 ||
                    absence.MarkerKey != MountReturnMarkerKey || absence.Operations.Count != 1 || absence.Operations[0] != operation)
                    throw new InvalidOperationException("Terminal return requires complete saved absence and the permanent payer receipt.");
                if (current.Return.Phase == MountReturnComplete) return current;
                cleanup.SavedGeneration = absence.Generation; current.Return.Phase = MountReturnComplete;
                current.Revision = checked(current.Revision + 1); WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }
        internal MountRecord RecoverMountReturnCredit(Guid project, Guid part, Guid operation, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                if (current.Return.Phase != MountReturnSaved || current.Return.Credit == null || profile == null || profile.Player != current.Player ||
                    !Hash(profile.ProfileHash, 128) || !profile.CustomData.TryGetValue(MountCreditKey(operation), out var value) || value != MountCreditValue(current))
                    throw new InvalidOperationException("Recovery requires this operation's permanent canonical payer receipt.");
                if (current.Return.Credit.Phase == MountProfileCredited) return current;
                current.Return.Credit.Phase = MountProfileCredited; current.Return.Credit.SavedProfileHash = profile.ProfileHash;
                current.Revision = checked(current.Revision + 1); WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }
        private static void RequireMountReceipt(MountRecord current, NativePlayerProfileReadback.CustomDataEvidence profile)
        {
            if (current.Return.Credit?.Phase != MountProfileCredited || profile == null || profile.Player != current.Player ||
                !Hash(profile.ProfileHash, 128) || profile.CustomData == null ||
                !profile.CustomData.TryGetValue(MountCreditKey(current.Return.Operation), out var receipt) || receipt != MountCreditValue(current))
                throw new InvalidOperationException("The canonical payer profile must retain this return's permanent receipt.");
        }
        private static void ValidateMountCleanup(MountRecord mount)
        {
            var r = mount.Return; var c = r.Cleanup;
            if (c == null) { if (r.Phase == MountReturnComplete) throw new InvalidDataException("Terminal return lacks cleanup evidence."); return; }
            if (r.Credit?.Phase != MountProfileCredited || c.BeforeGeneration < r.Saved.Generation || c.BeforeGeneration == uint.MaxValue ||
                !Hash(c.ProfileHash, 128) || (r.Phase == MountReturnComplete ? !c.Dispatched || c.SavedGeneration <= c.BeforeGeneration : c.SavedGeneration != 0))
                throw new InvalidDataException("Invalid return cleanup phase, generation or receipt evidence.");
        }
    }
}
