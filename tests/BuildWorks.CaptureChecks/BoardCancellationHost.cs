using System;
using System.Collections.Generic;
using System.Text;
namespace BoardCancellationChecks {
 public class HostObject { public static implicit operator bool(HostObject value)=>value!=null; }
 public class Transform { public float position; }
 public static class Vector3 { public static float Distance(float a,float b)=>Math.Abs(a-b); }
 public class Player:HostObject {
  public static Player m_localPlayer=new Player(); public bool Dead; public Transform transform=new Transform();
  public bool IsDead()=>Dead; public long GetPlayerID()=>7;
 }
 public class Board:HostObject { public Guid Id=Guid.NewGuid(); public Transform transform=new Transform(); }
 public static class BuildWorksPlugin { public static bool CanOpenConstructionProject=true; }
 public static class BuildWorksLocalization { public static string Text(string key,params object[] args)=>key; }
 public class BlueprintConstructionProjectStore {}
 public class BlueprintConstructionCancellationJournal {
  public const int Prepared=1; public static Record Pending;
  public class Record { public int Phase=1; public long PlanRevision=1,Player=7; public Guid Operation=Guid.NewGuid(); }
  public BlueprintConstructionCancellationJournal(BlueprintConstructionProjectStore store){}
  public Record Read(Guid id)=>Pending;
 }
 __ENUM__
 public class BlueprintConstructionProjectRuntime {
  private bool IsPrivateLocalPlacement=true,Context=true,Hammer=true;
  private Board OpenBoard=new Board(),Registered;
  private Guid queued;
  private class Operation { public bool Finished=false; }
  private Operation execution,removal;
  private Operation payout,collection,cleanup,paidCancellation;
  private class ReturnOperation { public Guid Project; public bool Busy,Finished; public string Status; }
  private ReturnOperation contentsReturn;
  private class Button { public bool interactable; }
  __BUSY__
  __PENDING__
  private readonly BlueprintConstructionProjectStore store=new BlueprintConstructionProjectStore();
  private readonly HashSet<Guid> cancellationProjects=new HashSet<Guid>();
  private readonly List<string> calls=new List<string>();
  private ConstructionCancellationAction action;
  private Guid dispatchedOperation;
  private long dispatchedRevision;
  private void RequireContext() { if(!Context)throw new InvalidOperationException("Context"); }
  private bool HasHammer(Player player)=>Hammer;
  private Board Board(Guid id)=>Registered;
  private ConstructionCancellationAction CancellationAction(Guid id,long revision)=>action;
  private void CancelUnpaid(Guid id,long revision,Guid operation) { calls.Add("unpaid");dispatchedOperation=operation; }
  private void BeginPaidRemoval(Guid id,long revision,Guid operation) { calls.Add("remove");dispatchedOperation=operation;dispatchedRevision=revision; }
  private void BeginNativePaidRemoval(Guid id,long revision,Guid operation) { calls.Add("resume");dispatchedOperation=operation;dispatchedRevision=revision; }
  private void BeginPayout(Guid id) { calls.Add("payout"); }
  private void Close() { calls.Add("close"); }
  __ROUTER__
  __RESUME__
  __CONFIRMATION__
  private bool summaryNeedsReview;
  private (int Built,int Total,bool NeedsReview) Progress(Guid id)=>(1,2,summaryNeedsReview);
  private static string Summary(ConstructionCancellationAction action,bool needsReview) {
   var runtime=Reset();runtime.summaryNeedsReview=needsReview;var board=runtime.OpenBoard;var text=new StringBuilder();
   __BUILDING__
   __SUMMARY__
   return string.Join("|",text.ToString().Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries));
  }
  private static bool ContinueEnabled(ConstructionCancellationAction action,bool pending) {
   var runtime=Reset();var board=runtime.OpenBoard;var continueButton=new Button();
   if(pending)runtime.contentsReturn=new ReturnOperation{Project=board.Id,Status="waiting-for-stand"};
   __BUILDING__
   __CONTINUE__
   return continueButton.interactable;
  }
  private static int checks;
  private static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
  private static BlueprintConstructionProjectRuntime Reset() {
   Player.m_localPlayer=new Player();BuildWorksPlugin.CanOpenConstructionProject=true;
   BlueprintConstructionCancellationJournal.Pending=new BlueprintConstructionCancellationJournal.Record();
   var r=new BlueprintConstructionProjectRuntime();r.Registered=r.OpenBoard;return r;
  }
  public static int Run() {
   checks=0;
   foreach(var kind in new[]{ConstructionCancellationAction.Unpaid,ConstructionCancellationAction.Remove,
    ConstructionCancellationAction.ResumeRemoval,ConstructionCancellationAction.IssueRefund,ConstructionCancellationAction.AwaitCollection}) {
    var r=Reset();r.action=kind;var fresh=Guid.NewGuid();r.RequestCancellationFromBoard(r.OpenBoard.Id,1,fresh);
    string expected=kind==ConstructionCancellationAction.Unpaid?"unpaid":kind==ConstructionCancellationAction.IssueRefund?"payout":
     kind==ConstructionCancellationAction.AwaitCollection?"close":kind==ConstructionCancellationAction.ResumeRemoval?"resume":"remove";
    Check(r.calls.Count==1&&r.calls[0]==expected,"Wrong branch "+kind);
    if(kind==ConstructionCancellationAction.ResumeRemoval)
     Check(r.dispatchedOperation==BlueprintConstructionCancellationJournal.Pending.Operation&&r.dispatchedRevision==1,"Prepared operation was replaced");
    if(kind==ConstructionCancellationAction.Remove||kind==ConstructionCancellationAction.Unpaid)
     Check(r.dispatchedOperation==fresh,"Fresh operation was lost");
    if(kind==ConstructionCancellationAction.AwaitCollection)
     Check(r.cancellationProjects.Contains(r.OpenBoard.Id),"Issued return not queued for finalization");
   }
   var failures=new Action<BlueprintConstructionProjectRuntime>[] {
    r=>r.Context=false,r=>r.IsPrivateLocalPlacement=false,r=>Player.m_localPlayer=null,r=>Player.m_localPlayer.Dead=true,
    r=>r.OpenBoard=null,r=>r.Registered=new Board(),r=>BuildWorksPlugin.CanOpenConstructionProject=false,r=>r.Hammer=false,
    r=>Player.m_localPlayer.transform.position=5.01f,r=>r.queued=Guid.NewGuid(),r=>r.payout=new Operation(),
    r=>r.execution=new Operation(),r=>r.removal=new Operation(),r=>r.action=ConstructionCancellationAction.Review
   };
   foreach(var mutate in failures) {
    var r=Reset();var id=r.OpenBoard.Id;mutate(r);bool refused=false;
    try { r.RequestCancellationFromBoard(id,1,Guid.NewGuid()); }catch(InvalidOperationException){refused=true;}
    Check(refused&&r.calls.Count==0&&r.cancellationProjects.Count==0,"Unsafe request caused an effect");
   }
   foreach(int slot in new[]{0,1,2}) {
    var r=Reset();if(slot==0)r.collection=new Operation();else if(slot==1)r.cleanup=new Operation();else r.paidCancellation=new Operation();
    bool refused=false;try{r.RequestCancellationFromBoard(r.OpenBoard.Id,1,Guid.NewGuid());}catch(InvalidOperationException){refused=true;}
    Check(refused&&r.calls.Count==0,"Other active payout admitted board cancellation");
   }
   {var r=Reset();r.payout=new Operation{Finished=true};r.RequestCancellationFromBoard(r.OpenBoard.Id,1,Guid.NewGuid());Check(r.calls.Count==1,"Completed payout blocked board cancellation");}
   foreach(string state in new[]{"durable-step","waiting-E","other-project","returned"}) {
    var r=Reset();r.action=ConstructionCancellationAction.Remove;r.contentsReturn=new ReturnOperation{
     Project=state=="other-project"?Guid.NewGuid():r.OpenBoard.Id,Busy=state=="durable-step",Finished=state=="returned",Status=state};
    bool refused=false;try{r.RequestCancellationFromBoard(r.OpenBoard.Id,1,Guid.NewGuid());}catch(InvalidOperationException){refused=true;}
    bool blocked=state=="durable-step"||state=="other-project";
    Check(refused==blocked&&r.calls.Count==(blocked?0:1),"Wrong contents return board guard "+state);
   }
   foreach(bool empty in new[]{false,true}) {
    var r=Reset();bool refused=false;
    try {r.RequestCancellationFromBoard(empty?r.OpenBoard.Id:Guid.NewGuid(),1,empty?Guid.Empty:Guid.NewGuid());}
    catch(InvalidOperationException){refused=true;}
    Check(refused&&r.calls.Count==0,"Wrong project/operation admitted");
   }
   var exact=Reset();Player.m_localPlayer.transform.position=5;exact.RequestCancellationFromBoard(exact.OpenBoard.Id,1,Guid.NewGuid());
   Check(exact.calls.Count==1,"Exact reach boundary refused");
   Check(ConfirmationKey(ConstructionCancellationAction.Unpaid)=="project.cancel_unpaid_confirm","Unpaid confirmation promised a refund");
   Check(ConfirmationKey(ConstructionCancellationAction.Remove)=="project.cancel_confirm","Paid removal confirmation differs");
   Check(ConfirmationKey(ConstructionCancellationAction.ResumeRemoval)=="project.cancel_confirm","Removal recovery confirmation differs");
   Check(ConfirmationKey(ConstructionCancellationAction.IssueRefund)=="project.refund_confirm","Refund confirmation differs");
   foreach(var kind in new[]{ConstructionCancellationAction.Unpaid,ConstructionCancellationAction.AwaitCollection})foreach(bool pending in new[]{false,true})
    Check(ContinueEnabled(kind,pending)==(kind==ConstructionCancellationAction.Unpaid&&!pending),"Continue ignored pending return or action");
   foreach(var kind in new[]{ConstructionCancellationAction.IssueRefund,ConstructionCancellationAction.Unpaid,
    ConstructionCancellationAction.Remove,ConstructionCancellationAction.ResumeRemoval,ConstructionCancellationAction.AwaitCollection,
    ConstructionCancellationAction.Review}) foreach(bool needsReview in new[]{true,false}) {
    bool building=kind==ConstructionCancellationAction.Unpaid||kind==ConstructionCancellationAction.Remove;
    string expected=building?"project.progress"+(needsReview?"|project.review":""):
     kind==ConstructionCancellationAction.Review?"project.review":
     kind==ConstructionCancellationAction.AwaitCollection?"project.refund_wait":"project.cancel_pending";
    Check(Summary(kind,needsReview)==expected,"Wrong phase summary "+kind+"/NeedsReview="+needsReview);
   }
   return checks;
  }
 }
}
