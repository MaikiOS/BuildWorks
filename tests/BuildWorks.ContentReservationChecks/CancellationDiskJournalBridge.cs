using System;
using System.Linq;
using System.Collections.Generic;
using OstrixMods.BuildWorks.Geometry;
using DiskRemoval=OstrixMods.BuildWorks.BlueprintConstructionCancellationJournal;
using DiskPayout=OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal;
using DiskParts=OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
namespace ReservationChecks
{
    internal static class JournalDisk
    {
        internal static OstrixMods.BuildWorks.BlueprintConstructionProjectStore Store(BlueprintConstructionProjectStore plans)=>
            new OstrixMods.BuildWorks.BlueprintConstructionProjectStore(new BlueprintConstructionPartJournal(plans).DiskPath(),plans.Plan.World);
        internal static T Copy<T>(object value)=>BlueprintConstructionPartJournal.Copy<T>(value);
    }
#pragma warning disable CS0649 // Detached mirrors are populated by the existing reflection disk bridge.
    internal sealed class BlueprintConstructionCancellationJournal
    {
        internal const int Prepared=1,RemovalIntent=2,RemovedSaved=3;
        internal sealed class Entry{internal Guid Part,Operation;internal int Phase;internal string Hash;}
        internal sealed class SaveProof{internal uint BeforeGeneration,Generation;internal string ProfileHash,InventoryHash;}
        internal sealed class Record{internal Guid Project,Operation;internal long World,Player,PlanRevision,Revision;internal string PlanHash;internal int Phase;internal Entry[] Parts;internal uint BeforeGeneration;internal SaveProof Saved;}
        private readonly DiskRemoval disk;
        internal BlueprintConstructionCancellationJournal(BlueprintConstructionProjectStore plans){disk=new DiskRemoval(JournalDisk.Store(plans));}
        internal static void RequireNotStarted(BlueprintConstructionProjectStore plans,Guid project){if(new BlueprintConstructionPartJournal(plans).Disk!=null)DiskRemoval.RequireNotStarted(JournalDisk.Store(plans),project);}
        internal Record Read(Guid project)=>JournalDisk.Copy<Record>(disk.Read(project));
        internal Record Prepare(Guid project,long revision,Guid op,long player)=>JournalDisk.Copy<Record>(disk.Prepare(project,revision,op,player));
        internal Record BeginRemoval(Guid project,Guid op,uint generation)=>JournalDisk.Copy<Record>(disk.BeginRemoval(project,op,generation));
        internal Record ConfirmRemovedSaved(Guid project,Guid op,SaveProof proof)=>JournalDisk.Copy<Record>(disk.ConfirmRemovedSaved(project,op,JournalDisk.Copy<DiskRemoval.SaveProof>(proof)));
    }
    internal sealed partial class BlueprintConstructionCancellationRemoval { internal static int Started; }
    internal sealed partial class BlueprintConstructionPayoutJournal
    {
        private readonly DiskPayout disk;
        internal const int Ready=0,SaveIntent=2,IssuedSaved=3;
        internal sealed class Proof{internal uint Generation;internal string ProfileHash,InventoryHash;internal string[] ItemHashes;}
        internal BlueprintConstructionPayoutJournal(){}
        internal BlueprintConstructionPayoutJournal(BlueprintConstructionProjectStore plans){disk=new DiskPayout(JournalDisk.Store(plans));}
        internal BlueprintConstructionPartJournal.PaidRow[] Paid(Guid project)=>JournalDisk.Copy<BlueprintConstructionPartJournal.PaidRow[]>(disk.Paid(project));
        internal Record Prepare(Guid project,Guid op,long player,uint generation,BlueprintForeignPartData inventory,Entry[] entries)=>JournalDisk.Copy<Record>(disk.Prepare(project,op,player,generation,inventory,JournalDisk.Copy<DiskPayout.Entry[]>(entries)));
        internal Record BeginItem(Guid project,Guid op,int index)=>JournalDisk.Copy<Record>(disk.BeginItem(project,op,index));
        internal Record Observe(Guid project,Guid op,int index,long user,uint id)=>JournalDisk.Copy<Record>(disk.Observe(project,op,index,user,id));
        internal Record BeginSave(Guid project,Guid op)=>JournalDisk.Copy<Record>(disk.BeginSave(project,op));
        internal Record ConfirmSaved(Guid project,Guid op,Proof proof)=>JournalDisk.Copy<Record>(disk.ConfirmSaved(project,op,JournalDisk.Copy<DiskPayout.Proof>(proof)));
        internal static void ValidateItem(BlueprintConstructionPartJournal.PaidRow row,Entry entry)=>DiskPayout.ValidateItem(JournalDisk.Copy<DiskParts.PaidRow>(row),JournalDisk.Copy<DiskPayout.Entry>(entry));
        internal static void RequireIssuanceBudget(BlueprintForeignPartData inventory,IEnumerable<Entry> entries)=>DiskPayout.RequireIssuanceBudget(inventory,entries.Select(JournalDisk.Copy<DiskPayout.Entry>));
    }
#pragma warning restore CS0649
}
