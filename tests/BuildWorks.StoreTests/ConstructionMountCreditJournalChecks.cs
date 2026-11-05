using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Reflection;
using OstrixMods.BuildWorks.Geometry;
using Journal=OstrixMods.BuildWorks.BlueprintConstructionPartJournal;
using Plans=OstrixMods.BuildWorks.BlueprintConstructionProjectStore;

namespace OstrixMods.BuildWorks.StoreTests
{
    internal static partial class ConstructionMountJournalChecks
    {
        private static void RunCreditChecks(string directory)
        {
            var f=Fixture(Path.Combine(directory,"credit"));var mount=Guid.NewGuid();var operation=Guid.NewGuid();
            var a=Mounted(durability:10000);var before=Inventory(0,1);var raw=Convert.FromBase64String(before.Zdo);
            Array.Copy(BitConverter.GetBytes(10000),0,raw,6,4);before=new BlueprintForeignPartData(before.Format,before.Info,Convert.ToBase64String(raw),null);
            f.Journal.PrepareMount(f.Plan.Id,f.Part,mount,17,90,4,before,a,true,out _);
            var observed=f.Journal.ObserveMount(f.Plan.Id,f.Part,mount,Inventory(0,0),a);
            f.Journal.ConfirmMountSaved(f.Plan.Id,f.Part,mount,Saved(Inventory(0,0),observed.Receipt));
            f.Journal.PrepareMountReturn(f.Plan.Id,f.Part,mount,operation,17,8,Inventory(1,0),a,out _);
            f.Journal.BindMountReturnIdentity(f.Plan.Id,f.Part,operation,90,5);
            f.Journal.ObserveMountReturn(f.Plan.Id,f.Part,operation,a,true);
            string clean=Properties("SwordIron",1,1,false,10000,"actual-A");
            ReturnRefuse<InvalidOperationException>(()=>Begin());
            f.Journal.ConfirmMountReturnSaved(f.Plan.Id,f.Part,operation,new Journal.SavePair {BeforeGeneration=8,Generation=9,
                ProfileHash=new string('E',128),InventoryHash=Digest(Convert.FromBase64String(Inventory(1,0).Zdo)),WorldPayloadHash=Digest(observed.Receipt)},Digest(Convert.FromBase64String(a.Zdo)));
            ReturnRefuse<InvalidDataException>(()=>Begin(x:0));
            ReturnRefuse<InvalidDataException>(()=>Begin(x:8));
            ReturnRefuse<InvalidDataException>(()=>Begin(profile:"bad"));
            foreach(var wrong in new[]{Properties("SwordIron",1,1,false,9999,"actual-A"),Properties("SwordIron",1,1,false,10000,"other"),
                Properties("SwordIron",2,1,false,10000,"actual-A"),Properties("SwordIron",1,1,true,10000,"actual-A")})
                ReturnRefuse<InvalidDataException>(()=>Begin(item:wrong));
            var intent=Begin();var c=intent.Return.Credit;
            ReturnCheck(!c.LoadedSource,"old intent default representation changed");
            string legacyKey=Journal.MountCreditKey(operation),legacyValue=Journal.MountCreditValue(intent);
            var legacyRead=new Journal(new Plans(f.Plans.DirectoryPath,42)).ReadMount(f.Plan.Id,f.Part);
            ReturnCheck(Journal.MountCreditKey(operation)==legacyKey&&Journal.MountCreditValue(legacyRead)==legacyValue&&!legacyRead.Return.Credit.LoadedSource,"old receipt key/value/schema changed");
            string legacyFile=Path.Combine(f.Plans.DirectoryPath,f.Plan.Id.ToString("N")+"."+f.Part.ToString("N")+".bwmount");
            ReturnCheck(!System.Text.Encoding.UTF8.GetString(File.ReadAllBytes(legacyFile)).Contains("\"LoadedSource\""),"false representation field emitted into old canonical records");
            var projection=new Journal.MountRecord{Return=new Journal.MountReturnRecord{ObservedInfo="SwordIron:0:1:0",ObservedBytes=Properties("SwordIron",1,1,false,4,"actual-A")}};
            string projectedItem=Properties("SwordIron",1,1,false,3,"actual-A");
            ReturnRefuse<InvalidDataException>(()=>Journal.MountCreditInventory(projection,Inventory(1,0),projectedItem,1,0));
            ReturnCheck(Journal.MountCreditInventory(projection,Inventory(1,0),projectedItem,1,0,true)!=null,"exact B4->C3 projection refused");
            ReturnRefuse<InvalidDataException>(()=>Journal.MountCreditInventory(projection,Inventory(1,0),Properties("SwordIron",1,1,false,2,"actual-A"),1,0,true));

            var after=new BlueprintForeignPartData(BlueprintForeignPartData.NativeContainerFormat,c.AfterInfo,c.AfterBytes,null);
            string key=Journal.MountCreditKey(operation),value=Journal.MountCreditValue(intent);
            ReturnCheck(c.Phase==Journal.MountCreditIntent&&intent.RequiresReconciliation,"credit intent incorrectly settled return");
            ReturnRefuse<InvalidOperationException>(()=>Begin());
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.ConfirmMountReturnCredit(f.Plan.Id,f.Part,operation,null));
            foreach(var proof in new[]{Proof(18,key,value,after),Proof(17,key,"wrong",after),Proof(17,key,value,Inventory(1,0))})
                ReturnRefuse<InvalidOperationException>(()=>f.Journal.ConfirmMountReturnCredit(f.Plan.Id,f.Part,operation,proof));
            var credited=f.Journal.ConfirmMountReturnCredit(f.Plan.Id,f.Part,operation,Proof(17,key,value,after));
            ReturnCheck(credited.Return.Credit.Phase==Journal.MountProfileCredited&&credited.RequiresReconciliation&&Journal.MountCreditValue(credited)==value,
                "profile credit lost receipt or enabled structural cancellation");
            ReturnCheck(f.Journal.ConfirmMountReturnCredit(f.Plan.Id,f.Part,operation,Proof(17,key,value,after)).Revision==credited.Revision,"same confirmation rewrote credit");
            ReturnRefuse<InvalidOperationException>(()=>Begin());
            ReturnRefuse<InvalidOperationException>(()=>Cancel(f));
            credited.Return.Credit.AfterBytes="changed";
            var reopened=new Journal(new Plans(f.Plans.DirectoryPath,42)).ReadMount(f.Plan.Id,f.Part);
            ReturnCheck(reopened.Return.Credit.AfterBytes==after.Zdo&&reopened.Return.Credit.SavedProfileHash==new string('C',128),"saved credit aliases caller or was not durable");
            NativePlayerProfileReadback.CustomDataEvidence Receipt(long player=17,string receipt=null) =>
                (NativePlayerProfileReadback.CustomDataEvidence)Activator.CreateInstance(typeof(NativePlayerProfileReadback.CustomDataEvidence),
                    BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{Proof(player,key,receipt??value,after)},null);
            NativeWorldStorageReadback.AbsenceEvidence Absent(uint generation=10,long world=42,int? marker=null,Guid? id=null) =>
                (NativeWorldStorageReadback.AbsenceEvidence)Activator.CreateInstance(typeof(NativeWorldStorageReadback.AbsenceEvidence),
                    BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{new NativeWorldStorageReadback.AbsenceExpectation(world,
                        marker??Journal.MountReturnMarkerKey,new[]{id??operation}),42,1,0,generation},null);
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.BeginMountCleanup(f.Plan.Id,f.Part,operation,9,Receipt(18)));
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.BeginMountCleanup(f.Plan.Id,f.Part,operation,9,Receipt(receipt:"wrong")));
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.BeginMountCleanup(f.Plan.Id,f.Part,operation,8,Receipt()));
            var cleanup=f.Journal.BeginMountCleanup(f.Plan.Id,f.Part,operation,9,Receipt());
            ReturnCheck(!cleanup.Return.Cleanup.Dispatched&&cleanup.RequiresReconciliation,"cleanup intent falsely terminal");
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.CompleteMountReturn(f.Plan.Id,f.Part,operation,Absent(),Receipt()));
            var attempted=f.Journal.DispatchMountCleanup(f.Plan.Id,f.Part,operation);
            ReturnCheck(new Journal(new Plans(f.Plans.DirectoryPath,42)).ReadMount(f.Plan.Id,f.Part).Return.Cleanup.Dispatched,"dispatch fence not durable");
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.DispatchMountCleanup(f.Plan.Id,f.Part,operation));
            foreach(var wrong in new[]{Absent(9),Absent(world:43),Absent(marker:42),Absent(id:Guid.NewGuid())})
                ReturnRefuse<InvalidOperationException>(()=>f.Journal.CompleteMountReturn(f.Plan.Id,f.Part,operation,wrong,Receipt()));
            ReturnRefuse<InvalidOperationException>(()=>f.Journal.CompleteMountReturn(f.Plan.Id,f.Part,operation,Absent(),Receipt(receipt:"wrong")));
            var terminal=f.Journal.CompleteMountReturn(f.Plan.Id,f.Part,operation,Absent(),Receipt());
            ReturnCheck(terminal.Return.Phase==Journal.MountReturnComplete&&!terminal.RequiresReconciliation,"saved absence did not settle child");
            ReturnCheck(f.Journal.CompleteMountReturn(f.Plan.Id,f.Part,operation,Absent(),Receipt()).Revision==terminal.Revision,"terminal replay rewrote child");
            Journal.RequireNoOutstandingMounts(f.Plans,f.Plan.Id);
            ReturnCheck(new Journal(new Plans(f.Plans.DirectoryPath,42)).ReadMount(f.Plan.Id,f.Part).Return.Phase==Journal.MountReturnComplete,"terminal did not survive file reopen");
            Journal.MountRecord Begin(int x=1,string item=null,string profile=null)=>f.Journal.BeginMountReturnCredit(f.Plan.Id,f.Part,operation,
                Inventory(1,0),item??clean,x,0,profile??new string('B',128));
            NativePlayerProfileReadback.Evidence Proof(long player,string receiptKey,string receiptValue,BlueprintForeignPartData inventory)=>
                (NativePlayerProfileReadback.Evidence)Activator.CreateInstance(typeof(NativePlayerProfileReadback.Evidence),BindingFlags.Instance|BindingFlags.NonPublic,null,
                    new object[]{player,new string('C',128),null,0,inventory,new ReadOnlyDictionary<string,string>(new Dictionary<string,string>{{receiptKey,receiptValue}})},null);
        }
    }
}
