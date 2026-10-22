using System;
using System.Collections.Generic;
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
 public static class BuildWorksLocalization { public static string Text(string key)=>key; }
 public class BlueprintConstructionProjectStore {}
 public class BlueprintConstructionCancellationJournal {
  public const int Prepared=1; public static Record Pending;
  public class Record { public int Phase=1; public long PlanRevision=1,Player=7; public Guid Operation=Guid.NewGuid(); }
  public BlueprintConstructionCancellationJournal(BlueprintConstructionProjectStore store){}
  public Record Read(Guid id)=>Pending;
 }
 __ENUM__
 public class BlueprintConstructionProjectRuntime {
  private bool IsPrivateLocalPlacement=true,Context=true,Hammer=true,PayoutBusy;
  private Board OpenBoard=new Board(),Registered;
  private Guid queued;
  private class Operation { public bool Finished=false; }
  private Operation execution,removal;
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
  private void BeginPayout(Guid id) { calls.Add("payout"); }
  private void Close() { calls.Add("close"); }
  __ROUTER__
  __RESUME__
  __CONFIRMATION__
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
     kind==ConstructionCancellationAction.AwaitCollection?"close":"remove";
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
    r=>Player.m_localPlayer.transform.position=5.01f,r=>r.queued=Guid.NewGuid(),r=>r.PayoutBusy=true,
    r=>r.execution=new Operation(),r=>r.removal=new Operation(),r=>r.action=ConstructionCancellationAction.Review
   };
   foreach(var mutate in failures) {
    var r=Reset();var id=r.OpenBoard.Id;mutate(r);bool refused=false;
    try { r.RequestCancellationFromBoard(id,1,Guid.NewGuid()); }catch(InvalidOperationException){refused=true;}
    Check(refused&&r.calls.Count==0&&r.cancellationProjects.Count==0,"Unsafe request caused an effect");
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
   return checks;
  }
 }
}
