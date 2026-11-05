using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.LowLevel;
using TMPro;
using UnityEngine.UI;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object x) => x != null; }
 public class GameObject : Object { }
 public struct Vector2 { public float x,y; public Vector2(float a,float b){x=a;y=b;} }
 public static class Time { public static int frameCount; public static float realtimeSinceStartup; public static float unscaledDeltaTime=.01f; }
 public static class Screen { public static int width=1001,height=1001; }
 public enum KeyCode { F12 }
 public static class Input { public static bool GetKey(KeyCode k) => false; }
 public static class Mathf { public static float Clamp01(float f)=>Math.Max(0,Math.Min(1,f)); public static float Min(float a,float b)=>Math.Min(a,b); }
}
namespace UnityEngine.InputSystem.LowLevel {
 public enum InputUpdateType { Dynamic, Manual }
 public static class InputState { public static InputUpdateType currentUpdateType=InputUpdateType.Dynamic; }
}
public class Player : UnityEngine.Object { public object inventory=new object(); public object GetInventory()=>inventory; }
public class InventoryGrid : UnityEngine.Object { public object inventory; public object GetInventory()=>inventory; }
public class UIInputHandler : UnityEngine.Object { }
public class Cell : UnityEngine.Object {
 public InventoryGrid grid; public bool handler=true;
 public GameObject gameObject=new GameObject();
 public T GetComponentInParent<T>() where T:class => grid as T;
 public T GetComponent<T>() where T:class => handler ? new UIInputHandler() as T : null;
}
namespace TMPro { public class TMP_InputField : Cell { public bool isFocused; } }
namespace UnityEngine.UI { public class InputField : Cell { public bool isFocused; } }
public class QaEvents { public GameObject currentSelectedGameObject; }
public class InventoryGui : UnityEngine.Object { public static InventoryGui instance; public static bool visible=true; public InventoryGrid m_playerGrid; public static bool IsVisible()=>visible; }
public static class ZInput { public static bool touch; public static bool IsTouchActive()=>touch; }
public static class BuildWorksTestIsolation { public static void RequireLoadedSession(){} }
public static class BuildWorksQaControlProtocol {
 public class Step { public int milliseconds,buttons; public string uiTarget,text; public string[] keys; public bool pointer; public float x,y,lookX,lookY,scroll; }
 public class Request { public Step[] steps;
// PRODUCTION_REQUEST_SHAPES
 }
}
public class BuildWorksQaUiTargets {
 public class Entry { public float x,y; public bool writable; }
 public class Target { public Cell control; public Entry entry; public string id; public bool valid=true; public QaEvents events=new QaEvents(); }
 public Dictionary<string,Target> all=new Dictionary<string,Target>();
 public List<string> trace; public string invalidReason="label";
 public Target Resolve(string id){Require(all[id]);return all[id];}
 public void Require(Target t){trace.Add("guard"+t.id);if(!t.valid)throw new InvalidOperationException(invalidReason);}
// PRODUCTION_REQUIRE_FOCUSED
}
public class BuildWorksQaInput {
 public static Vector2 mousePosition; public bool UiModuleMatches=true,PhysicalIntervention; public int begins,releases; public Action<BuildWorksQaControlProtocol.Step,bool> onFeed; public Action onBegin;
 public void Begin(GameObject owner){begins++;onBegin?.Invoke();}
 public void Release(){releases++;}
 public void Feed(BuildWorksQaControlProtocol.Step s,bool first,Vector2 v,float f,float d){if(first)onFeed(s,first);}
 public static bool GetMouseButton(int n)=>false;
}
public class QaPairDispatchChecks {
 private BuildWorksQaControlProtocol.Request running;
 private BuildWorksQaUiTargets uiTargets=new BuildWorksQaUiTargets();
 private BuildWorksQaUiTargets.Target uiTarget;
 private bool uiReleased; private Player player=new Player(); private GameObject gameObject=new GameObject();
 private BuildWorksQaInput input=new BuildWorksQaInput();
 private float started,stepStarted; private int stepIndex,stepFrame,releaseFrame=-1; private bool stepFinished;
 private Vector2 pointerStart; private int stops,completions; private string reason;
 private List<string> trace=new List<string>();
 private bool SafeSession()=>true;
 private bool WithinDeadline()=>Time.realtimeSinceStartup-started<10;
 private void Stop(string s){reason=s;stops++;running=null;input.Release();}
 private void Finish(BuildWorksQaControlProtocol.Request r,string status,string detail){completions++;}
 private int textWrites,submits; private string value;
 private void SetFocusedText(string text){textWrites++;value=text;}
 private void SubmitFocusedText(BuildWorksQaControlProtocol.Step s,bool first){if(first && s.keys?.Contains("Return")==true)submits++;}
// PRODUCTION_SELECT
// PRODUCTION_DISPATCH
 private static void Require(bool b,string s){if(!b)throw new Exception(s);}
 private QaPairDispatchChecks(string fault=null) {
  Time.frameCount=0;Time.realtimeSinceStartup=0;ZInput.touch=false;InventoryGui.visible=true;
  var grid=new InventoryGrid { inventory=player.inventory };InventoryGui.instance=new InventoryGui {m_playerGrid=grid};
  uiTargets.trace=trace;
  foreach(string id in new[]{"A","B"})uiTargets.all[id]=new BuildWorksQaUiTargets.Target {id=id,control=new Cell {grid=grid},entry=new BuildWorksQaUiTargets.Entry {x=id=="A"?.2f:.8f,y=.5f}};
  running=new BuildWorksQaControlProtocol.Request {steps=new[]{
   new BuildWorksQaControlProtocol.Step {milliseconds=20,uiTarget="A",pointer=true,buttons=1,x=.99f},
   new BuildWorksQaControlProtocol.Step {milliseconds=20},
   new BuildWorksQaControlProtocol.Step {milliseconds=20,uiTarget="B",pointer=true,buttons=1,x=.99f},
   new BuildWorksQaControlProtocol.Step {milliseconds=20}}};
  input.onFeed=(s,first)=>{
   if(s.milliseconds==0)return;
   if(s.buttons==1){Require(Math.Abs(s.x-uiTargets.all[s.uiTarget].entry.x)<.001,"Raw coordinates bypassed observed target");trace.Add("down"+s.uiTarget);uiTargets.all[s.uiTarget].valid=false;
    if(s.uiTarget=="A" && fault!=null){uiTargets.invalidReason=fault;
     if(fault=="scope")uiTargets.all["B"].control.grid=new InventoryGrid();
     else if(fault=="handler")uiTargets.all["B"].control.handler=false;
     else if(fault=="hidden")InventoryGui.visible=false;
     else if(fault=="touch")ZInput.touch=true;
     else if(fault=="inventory")grid.inventory=new object();
     else uiTargets.all["B"].valid=false;
    }
   }else trace.Add("release");
  };
  started=stepStarted=Time.realtimeSinceStartup;SelectUiTarget(running.steps[0],running.InventoryPair,running.FocusThenText);input.Begin(gameObject);stepFrame=-1;
 }
 private void FocusText(bool separate=false,string fault=null,bool legacy=false){
  var field=legacy ? (Cell)new InputField {isFocused=true} : new TMP_InputField {isFocused=true};
  var target=new BuildWorksQaUiTargets.Target {id="F",control=field,entry=new BuildWorksQaUiTargets.Entry {writable=true,x=.5f,y=.5f}};
  target.events.currentSelectedGameObject=field.gameObject;uiTargets.all["F"]=target;
  var text=new BuildWorksQaControlProtocol.Step {milliseconds=20,uiTarget="F",text="QA copy",keys=new[]{"Return"}};
  running=new BuildWorksQaControlProtocol.Request {steps=separate ? new[]{text,new BuildWorksQaControlProtocol.Step {milliseconds=20}} : new[]{
   new BuildWorksQaControlProtocol.Step {milliseconds=20,uiTarget="F",pointer=true,buttons=1},new BuildWorksQaControlProtocol.Step {milliseconds=20},text,new BuildWorksQaControlProtocol.Step {milliseconds=20}}};
  input.begins=input.releases=0;stepIndex=0;releaseFrame=-1;stepFinished=false;stepFrame=-1;uiReleased=false;reason=null;
  Action<bool> focus=on=>{if(field is TMP_InputField tmp)tmp.isFocused=on;else ((InputField)field).isFocused=on;target.events.currentSelectedGameObject=on ? field.gameObject : null;};
  // Model the host layout callback on device creation; only the UI click can restore focus.
  input.onBegin=()=>focus(false);
  input.onFeed=(s,first)=>{
   if(s.buttons==1){trace.Add("focus-click");if(fault!="no-focus")focus(true);}
   if(stepIndex==1){
    if(fault=="focus-lost")focus(false);
    else if(fault=="selected-other")target.events.currentSelectedGameObject=new GameObject();
    else if(fault=="stale-id")uiTargets.all.Remove("F");
    else if(fault=="physical")input.PhysicalIntervention=true;
    else if(fault=="module")input.UiModuleMatches=false;
    else if(fault=="deadline")Time.realtimeSinceStartup+=11;
    else if(fault!=null && fault!="no-focus")target.valid=false;
   }
  };
  if(fault=="not-writable")target.entry.writable=false;
  try {SelectUiTarget(running.steps[0],running.InventoryPair,running.FocusThenText);input.Begin(gameObject);}
  catch(Exception error){Stop(error.Message);}
  Drive();
 }
 private void Drive(){for(int i=0;i<200 && running!=null;i++){Time.frameCount++;Time.realtimeSinceStartup+=.01f;AfterInput();}}
 public static void Run(){
  var ok=new QaPairDispatchChecks();ok.Drive();
  Require(ok.completions==1 && ok.stops==0,"Expected label changes after native pointer-down stopped pair: "+ok.reason);
  Require(ok.trace.Count(x=>x=="downA")==1 && ok.trace.Count(x=>x=="downB")==1,"Pair pressed wrong number of times");
  int a=ok.trace.IndexOf("downA"),b=ok.trace.IndexOf("downB");
  Require(a>=0 && b>a && ok.trace.Skip(a+1).Take(b-a-1).Contains("release") && ok.trace.Skip(a+1).Take(b-a-1).Contains("guardB") && ok.trace.Skip(b+1).Contains("release"),"Missing guard/release between clicks");
  Require(ok.input.begins==1 && ok.input.releases==1,"Input devices not owned by one batch");
  foreach(string fault in new[]{"label","geometry","covered","scope","handler","hidden","touch","inventory"}){
   var test=new QaPairDispatchChecks(fault);test.Drive();
   Require(test.stops==1 && test.completions==0 && test.trace.Contains("downA") && !test.trace.Contains("downB") && test.input.releases==1,"Unsafe second press: "+fault);
  }
  var separate=new QaPairDispatchChecks();separate.FocusText(separate:true);
  Require(separate.stops==1 && separate.textWrites==0 && separate.submits==0,"Separate text after layout reset bypassed focus guard");
  foreach(bool legacy in new[]{false,true}){
   var same=new QaPairDispatchChecks();same.FocusText(legacy:legacy);
   Require(same.completions==1 && same.stops==0 && same.value=="QA copy" && same.textWrites==1 && same.submits==1,"Atomic focus/text failed: "+same.reason);
   Require(same.input.begins==1 && same.input.releases==1 && same.trace.Count(x=>x=="focus-click")==1,"Focus/text created extra devices or clicks");
  }
  foreach(string fault in new[]{"not-writable","no-focus","focus-lost","selected-other","stale-id","deleted","read-only","geometry","covered","value","screen","events","physical","module","deadline"}){
   var test=new QaPairDispatchChecks();test.FocusText(fault:fault);
   Require(test.stops==1 && test.completions==0 && test.textWrites==0 && test.submits==0 && test.input.releases==1,"Unsafe focus/text: "+fault);
   if(fault=="not-writable")Require(test.input.begins==0,"Non-field click started before preflight");
  }
  Console.WriteLine("PASS actual QA dispatch/selector: one Begin, two guard/down/release pairs, post-down label mutations, eight second-target refusals before press; inert substitutes, no native proof.");
  Console.WriteLine("PASS extracted dispatch/selector/focus guard: layout reset rejects separate text; TMP/UGUI same-Begin click/text writes/submits once;15 refusals leave text unchanged. Inert host model, not native clicks.");
 }
}
