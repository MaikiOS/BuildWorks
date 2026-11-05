using System;
using System.IO;
using System.Runtime.Serialization;
using OstrixMods.BuildWorks.Geometry;

namespace OstrixMods.BuildWorks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal const int MountCreditIntent = 1, MountProfileCredited = 2;
        [DataContract] internal sealed class MountReturnCredit
        {
            [DataMember(Order = 0)] internal int Phase;
            [DataMember(Order = 1)] internal int X;
            [DataMember(Order = 2)] internal int Y;
            [DataMember(Order = 3)] internal string BeforeInfo;
            [DataMember(Order = 4)] internal string BeforeBytes;
            [DataMember(Order = 5)] internal string ItemBytes;
            [DataMember(Order = 6)] internal string AfterInfo;
            [DataMember(Order = 7)] internal string AfterBytes;
            [DataMember(Order = 8)] internal string BeforeProfileHash;
            [DataMember(Order = 9, EmitDefaultValue = false)] internal string SavedProfileHash;
            [DataMember(Order = 10, EmitDefaultValue = false)] internal bool LoadedSource;
        }

        internal MountRecord BeginMountReturnCredit(Guid project, Guid part, Guid operation, BlueprintForeignPartData before,
            string cleanItem, int x, int y, string beforeProfileHash, bool loadedSource = false)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                if (current.Return.Phase != MountReturnSaved || current.Return.Credit != null)
                    throw new InvalidOperationException("Only a saved return without any earlier credit attempt may insert an item.");
                var after = MountCreditInventory(current, before, cleanItem, x, y, loadedSource);
                current.Return.Credit = new MountReturnCredit { Phase = MountCreditIntent, X = x, Y = y,
                    BeforeInfo = before.Info, BeforeBytes = before.Zdo, ItemBytes = cleanItem,
                    AfterInfo = after.Info, AfterBytes = after.Zdo, BeforeProfileHash = beforeProfileHash, LoadedSource = loadedSource };
                // Reserve the confirmation write before the only inventory insertion.
                var maximum = CloneMount(current); maximum.Revision = long.MaxValue;
                maximum.Return.Credit.Phase = MountProfileCredited; maximum.Return.Credit.SavedProfileHash = new string('F', 128);
                maximum.Return.Phase = MountReturnComplete;
                maximum.Return.Cleanup = new MountCleanup { BeforeGeneration = uint.MaxValue - 1, ProfileHash = new string('F', 128),
                    Dispatched = true, SavedGeneration = uint.MaxValue };
                EncodeMount(maximum);
                current.Revision = checked(current.Revision + 1);
                WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }

        internal MountRecord ConfirmMountReturnCredit(Guid project, Guid part, Guid operation, NativePlayerProfileReadback.Evidence proof)
        {
            using (plans.Lock(project))
            {
                var plan = ActivePlan(project, part); var current = RequireMountReturn(project, part, operation);
                var credit = current.Return.Credit;
                if (credit == null || proof == null || proof.Player != current.Player || !Hash(proof.ProfileHash, 128) ||
                    proof.Inventory?.Info != credit.AfterInfo || proof.Inventory.Zdo != credit.AfterBytes || proof.CustomData == null ||
                    !proof.CustomData.TryGetValue(MountCreditKey(operation), out var value) || value != MountCreditValue(current))
                    throw new InvalidOperationException("Credit requires the same payer's exact saved inventory and permanent return receipt.");
                if (credit.Phase == MountProfileCredited) return current;
                credit.Phase = MountProfileCredited; credit.SavedProfileHash = proof.ProfileHash;
                current.Revision = checked(current.Revision + 1);
                WriteMount(plan, current, true); return ReadMount(project, part);
            }
        }

        internal static string MountCreditKey(Guid operation) => "ostrix.buildworks.mount.collected.v1." + operation.ToString("N");
        internal static string MountCreditValue(MountRecord mount)
        {
            var r = mount.Return; var c = r.Credit;
            using (var output = new MemoryStream()) using (var writer = new BinaryWriter(output))
            {
                writer.Write(mount.World); writer.Write(mount.Player); writer.Write(mount.Project.ToByteArray()); writer.Write(mount.Part.ToByteArray());
                writer.Write(mount.ParentOperation.ToByteArray()); writer.Write(mount.Operation.ToByteArray()); writer.Write(r.Operation.ToByteArray());
                writer.Write(r.ObservedBytes); writer.Write(c.BeforeInfo); writer.Write(c.BeforeBytes); writer.Write(c.ItemBytes);
                writer.Write(c.X); writer.Write(c.Y); writer.Write(c.AfterInfo); writer.Write(c.AfterBytes); writer.Write(c.BeforeProfileHash);
                writer.Flush(); return Digest(output.ToArray());
            }
        }
        internal static BlueprintForeignPartData MountCreditInventory(MountRecord mount, BlueprintForeignPartData before, string cleanItem, int x, int y, bool loadedSource = false)
        {
            string source = mount.Return.ObservedBytes;
            if (mount.Return.Credit?.LoadedSource ?? loadedSource)
            {
                var raw = new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat, mount.Return.ObservedInfo, source, null);
                if (!BlueprintItemStandContents.TryRead(raw, name => name.GetStableHashCode(), out var item) || item.SourceVersion != 110 ||
                    item.Stack != 1 || !item.DurabilityHundredths.HasValue) throw new InvalidDataException("Invalid immutable loaded return packet.");
                byte[] projected = Convert.FromBase64String(source);
                // Same Mono Load/Save rule as SameItem; only a comparison packet, never a decoded inventory insertion.
                int durability = (int)((double)(float)((float)item.DurabilityHundredths.Value * 0.01f) * 100d);
                Buffer.BlockCopy(BitConverter.GetBytes(durability), 0, projected, 1, 4);
                source = Convert.ToBase64String(projected);
            }
            return BlueprintConstructionPayoutJournal.CreditInventory(before, source, cleanItem, mount.Return.ObservedInfo.Split(':')[0], 1, x, y);
        }

        private static void ValidateMountReturnCredit(MountRecord mount)
        {
            var c = mount.Return.Credit; if (c == null) return;
            if (c.Phase != MountCreditIntent && c.Phase != MountProfileCredited || !Hash(c.BeforeProfileHash, 128) ||
                c.BeforeBytes == null || c.BeforeBytes.Length > MaximumBytes / 4 ||
                c.Phase == MountCreditIntent && c.SavedProfileHash != null || c.Phase == MountProfileCredited && !Hash(c.SavedProfileHash, 128))
                throw new InvalidDataException("Invalid mounted-item credit phase or profile baseline.");
            var before = new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat, c.BeforeInfo, c.BeforeBytes, null);
            var after = MountCreditInventory(mount, before, c.ItemBytes, c.X, c.Y);
            if (after.Info != c.AfterInfo || after.Zdo != c.AfterBytes)
                throw new InvalidDataException("Return credit differs from one exact source-derived item in an empty cell.");
        }
    }
}
