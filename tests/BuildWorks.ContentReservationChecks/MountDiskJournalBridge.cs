using System;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using DiskJournal=OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
namespace ReservationChecks
{
    internal sealed partial class BlueprintConstructionPartJournal
    {
        internal DiskJournal Disk;
        internal MountRecord Refresh(DiskJournal.MountRecord record) { Mount=Copy<MountRecord>(record);return Mount; }
        internal static T Copy<T>(object value) => (T)Copy(value,typeof(T));
        private static object Copy(object value,Type target)
        {
            if(value==null)return null;
            if(target.IsInstanceOfType(value))return value;
            var copy=Activator.CreateInstance(target,true);
            foreach(var field in target.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic))
            {
                var from=value.GetType().GetField(field.Name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic);
                if(from!=null)field.SetValue(copy,Copy(from.GetValue(value),field.FieldType));
            }
            return copy;
        }
    }
    internal static partial class ConstructionMountDetachChecks
    {
        // Only the host/store lookup is inert. Every child transition goes through actual journal IO.
        private static void ReconnectDisk(Rig r,string directory,Guid project,Guid part)
        {
            var plans=new OstrixMods.BuildWorks.BlueprintConstructionProjectStore(directory,42);
            var journal=new DiskJournal(plans);var parent=journal.RequireSavedPayment(project,part,100);
            r.Forward.Store.Plan.Id=project;r.Forward.Store.Plan.World=42;r.Forward.Store.Plan.Parts[0].Id=part;
            var actual=BlueprintConstructionPartJournal.Copy<BlueprintConstructionPartJournal.Record>(parent);
            foreach(var field in typeof(BlueprintConstructionPartJournal.Record).GetFields(BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public))field.SetValue(r.Journal.Parent,field.GetValue(actual));
            BlueprintConstructionPartJournal.DiskFingerprints[parent.Operation]=DiskJournal.Fingerprint(parent);
            r.Forward.Target.RootView.Data.BeforeSet=null;r.Forward.Target.RootView.Data.PrefabHash=parent.PrefabHash;
            r.Forward.Target.RootView.Data.Set(BlueprintConstructionPartExecution.MarkerKey,parent.Operation.ToString("N"));
            r.Forward.Target.RootView.Data.Set(BlueprintConstructionPartExecution.ReceiptKey,parent.Receipt);ZNet.instance.World.m_uid=42;
            r.Journal.Disk=journal;r.Journal.Refresh(journal.ReadMount(project,part));
            r.Stand.Contents=null;r.Forward.Target.RootView.Data.Set(BlueprintConstructionMountExecution.ReceiptKey,r.Journal.Mount.Receipt);
        }
        private static void ConnectDisk(Rig r)
        {
            var fixture=OstrixMods.BuildWorks.StoreTests.ConstructionMountJournalChecks.RuntimeFixture(Path.Combine(r.Forward.Root,"disk-journal"));
            var row=r.Journal.Mount;
            var parent=fixture.Journal.RequireSavedPayment(fixture.Plan.Id,fixture.Part,100);
            r.Forward.Store.Plan.Id=fixture.Plan.Id;r.Forward.Store.Plan.World=42;r.Forward.Store.Plan.Parts[0].Id=fixture.Part;
            r.Journal.Parent.Project=fixture.Plan.Id;r.Journal.Parent.Part=fixture.Part;r.Journal.Parent.Operation=parent.Operation;
            r.Journal.Parent.World=42;r.Journal.Parent.Creator=100;r.Journal.Parent.PrefabHash=parent.PrefabHash;r.Journal.Parent.Receipt=parent.Receipt;
            BlueprintConstructionPartJournal.DiskFingerprints[parent.Operation]=DiskJournal.Fingerprint(parent);
            r.Forward.Target.RootView.Data.PrefabHash=parent.PrefabHash;
            r.Forward.Target.RootView.Data.Set(BlueprintConstructionPartExecution.MarkerKey,parent.Operation.ToString("N"));
            r.Forward.Target.RootView.Data.Set(BlueprintConstructionPartExecution.ReceiptKey,parent.Receipt);
            ZNet.instance.World.m_uid=42;
            var before=new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,row.BeforeInfo,row.BeforeBytes,null);
            var mounted=new BlueprintForeignPartData(BlueprintForeignPartData.NativeItemStandFormat,row.MountedInfo,row.MountedBytes,null);
            var after=new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,row.AfterInfo,row.AfterBytes,null);
            fixture.Journal.PrepareMount(fixture.Plan.Id,fixture.Part,row.Operation,100,99,1,before,mounted,true,out _);
            var observed=fixture.Journal.ObserveMount(fixture.Plan.Id,fixture.Part,row.Operation,after,mounted);
            var saved=BlueprintConstructionPartJournal.Copy<DiskJournal.SavePair>(row.Saved);
            using(var sha=System.Security.Cryptography.SHA256.Create())saved.WorldPayloadHash=BitConverter.ToString(sha.ComputeHash(observed.Receipt)).Replace("-","");
            fixture.Journal.ConfirmMountSaved(fixture.Plan.Id,fixture.Part,row.Operation,saved);
            r.Journal.Disk=fixture.Journal;r.Journal.Refresh(fixture.Journal.ReadMount(fixture.Plan.Id,fixture.Part));
            r.Forward.Target.RootView.Data.Set(BlueprintConstructionMountExecution.ReceiptKey,r.Journal.Mount.Receipt);
        }
    }
}
