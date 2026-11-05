using System;
using System.Collections.Generic;
using System.Linq;
namespace PaymentReadChecks {
 public sealed class Store {
  public sealed class Part { public Guid Id=Guid.NewGuid(); public int SourceIndex; }
  public sealed class Blueprint { public List<object> parts=new List<object>{new object()}; }
  public sealed class Record { public List<Part> Parts=new List<Part>{new Part()}; public Blueprint Blueprint=new Blueprint(); public bool Cancelled; }
 }
 public sealed class Journal {
  public const int Intent=1,Observed=2,Committed=3,NoEffect=4;
  public sealed class Record { public int Phase; public bool RequiresReconciliation=>Phase==Intent||Phase==Observed; }
  public sealed class SourcesRecord { public int Phase; }
  public sealed class OutcomeRecord { public int Phase=5; }
  public static (Record Legacy,SourcesRecord Sources,OutcomeRecord Outcome) Payment;
  public static int LegacyReads,Reads;
  public Journal(Store store){}
  public (Record Legacy,SourcesRecord Sources,OutcomeRecord Outcome) ReadPayment(Guid project,Guid part){Reads++;return Payment;}
  public Record Read(Guid project,Guid part){LegacyReads++;return Payment.Legacy;}
 }
 public static class BuildWorksLocalization { public static string Text(string key)=>key; }
 public static class BlueprintConstructionCancellationJournal { public static void RequireNotStarted(Store store,Guid id){} }
 public static class BlueprintConstructionPartExecution {
  public static int Calls; public static Store LastStore;
  public static bool Complete(Store.Record plan,Store.Part part,Journal.Record row,object source,Store store){Calls++;LastStore=store;return row?.Phase==Journal.Committed;}
 }
 public sealed class Runtime {
  private readonly Store store=new Store();
  private readonly Store.Record plan=new Store.Record();
  private bool context=true;
  private int effects;
  private void RequireContext(){if(!context)throw new InvalidOperationException("context");}
  private Store.Record Read(Guid id)=>plan;
  private Journal.Record ReadPart(Guid id,Guid part){RequireContext();return new Journal(store).Read(id,part);}
  __READ_PAYMENT__
  __READ_PHASE__
  __PROGRESS__
  __NEXT__
  __PENDING__
  private void StartGuard(Guid project,Guid partId){
   var journal=new Journal(store);
   __START_GUARD__
   effects++;
  }
  private static int checks;
  private static void Check(bool value,string message){checks++;if(!value)throw new Exception(message);}
  private static void Refuse(Action action){checks++;try{action();}catch(InvalidOperationException){return;}throw new Exception("Unsupported source continuation admitted");}
  private static Runtime Reset(){Journal.Reads=Journal.LegacyReads=BlueprintConstructionPartExecution.Calls=0;return new Runtime();}
  public static int Run(){
   checks=0;var id=Guid.NewGuid();
   foreach(int phase in new[]{Journal.Intent,Journal.Observed,Journal.Committed,Journal.NoEffect}){
    var runtime=Reset();Journal.Payment=(null,new Journal.SourcesRecord{Phase=phase},null);
    var progress=runtime.Progress(id);
    Check(progress.Built==0&&progress.Total==1&&progress.NeedsReview&&runtime.ReadPartPhase(id,runtime.plan.Parts[0].Id)==phase,"Source phase lost or accepted as complete");
    Refuse(()=>runtime.NextPart(id));
    Check(Journal.LegacyReads==0&&BlueprintConstructionPartExecution.Calls==0,"Source record reached legacy reader or native completion");
    Refuse(()=>runtime.StartGuard(id,runtime.plan.Parts[0].Id));
    Check(runtime.effects==0,"Source start guard allowed an effect");
   }
   foreach(int phase in new[]{0,Journal.NoEffect,Journal.Committed}){
    var runtime=Reset();Journal.Payment=(phase==0?null:new Journal.Record{Phase=phase},null,null);
    var progress=runtime.Progress(id);
    Check(!progress.NeedsReview&&progress.Built==(phase==Journal.Committed?1:0),"Legacy progress changed");
    Check(ReferenceEquals(BlueprintConstructionPartExecution.LastStore,runtime.store),"Completion did not receive its exact project store");
    Check((runtime.NextPart(id)==null)==(phase==Journal.Committed),"Legacy next-part choice changed");
    runtime.StartGuard(id,runtime.plan.Parts[0].Id);Check(runtime.effects==1,"Legacy start refused");
   }
   {
    var runtime=Reset();Journal.Payment=(null,null,new Journal.OutcomeRecord());
    Check(runtime.Progress(id).NeedsReview&&runtime.ReadPartPhase(id,runtime.plan.Parts[0].Id)==5,"Partial outcome lost or treated as legacy");
    Refuse(()=>runtime.NextPart(id));Refuse(()=>runtime.StartGuard(id,runtime.plan.Parts[0].Id));
    Check(runtime.effects==0&&Journal.LegacyReads==0&&BlueprintConstructionPartExecution.Calls==0,"Partial outcome reached legacy completion or native replay");
   }
   foreach(int phase in new[]{Journal.Intent,Journal.Observed}){
    var runtime=Reset();Journal.Payment=(new Journal.Record{Phase=phase},null,null);
    Check(runtime.Progress(id).NeedsReview,"Legacy reconciliation lost");Refuse(()=>runtime.NextPart(id));
   }
   var guarded=Reset();guarded.context=false;
   Refuse(()=>guarded.ReadPartPhase(id,guarded.plan.Parts[0].Id));Check(Journal.Reads==0,"Read before runtime context guard");
   return checks;
  }
 }
}
