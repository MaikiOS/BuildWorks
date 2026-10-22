using System;
using System.Reflection;
namespace BoardInputChecks {
 public class HostObject { public static implicit operator bool(HostObject value)=>value!=null; }
 public class Player:HostObject {
  public static Player m_localPlayer=new Player(); public bool NativeInput=true,Throw;
  private bool TakeInput() { if(Throw)throw new Exception("native failure"); return NativeInput&&!BuildWorksPlugin.Block(this); }
  public bool Input()=>TakeInput();
 }
 public static class Hud { public static bool Visible; public static bool IsPieceSelectionVisible()=>Visible; }
 public static class HammerCatalogView { public static bool BlocksExchangeInput; }
 public sealed class Editor { public bool BlocksWorldInput; }
 public sealed class Projects { public bool BlocksInput; }
 public sealed class PrecisionPlacementSession {
  private enum PlacementState { Inactive, Armed, Editing }
  private enum SessionMode { None, BlueprintWorldPlacement, Editor }
  private PlacementState state; private SessionMode sessionMode;
  private object pendingCatalogBlueprint; private bool probingNativeInput;
  private static readonly MethodInfo TakeInputMethod=typeof(Player).GetMethod("TakeInput",BindingFlags.Instance|BindingFlags.NonPublic);
  __PROBE__
  __CAN_OPEN__
  __GAMEPLAY__
  __SESSION_BLOCK__
  internal void Busy(int mode) { state=mode==1?PlacementState.Editing:PlacementState.Inactive;
   sessionMode=mode==2?SessionMode.Editor:SessionMode.None;pendingCatalogBlueprint=mode==3?new object():null; }
 }
 public sealed class BuildWorksPlugin:HostObject {
  private static BuildWorksPlugin instance;
  private Projects constructionProjects=new Projects(); private Editor blueprintEditor=new Editor();
  private PrecisionPlacementSession session=new PrecisionPlacementSession();
  __PLUGIN_CAN_OPEN__
  __PLUGIN_BLOCK__
  public static bool Block(Player player)=>ShouldBlockPlayerInput(player);
  private static int checks;
  private static void Check(bool value,string message) { checks++;if(!value)throw new Exception(message); }
  public static int Run() {
   checks=0;instance=new BuildWorksPlugin();Player.m_localPlayer=new Player();
   Check(CanOpenConstructionProject,"Idle player cannot open board");
   instance.constructionProjects.BlocksInput=true;
   Check(!Player.m_localPlayer.Input(),"Open board leaked real player input");
   Check(CanOpenConstructionProject,"Open board blocks its own cancellation preflight");
   Check(!Player.m_localPlayer.Input()&&!instance.session.ProbingNativeInput,"Native probe leaked after return");
   Player.m_localPlayer.NativeInput=false;Check(!CanOpenConstructionProject,"Native menu refusal bypassed");
   Player.m_localPlayer.NativeInput=true;Player.m_localPlayer.Throw=true;
   Check(!CanOpenConstructionProject&&!instance.session.ProbingNativeInput,"Failed native probe leaked");Player.m_localPlayer.Throw=false;
   instance.blueprintEditor.BlocksWorldInput=true;Check(!CanOpenConstructionProject,"Editor bypassed");instance.blueprintEditor.BlocksWorldInput=false;
   HammerCatalogView.BlocksExchangeInput=true;Check(!CanOpenConstructionProject,"Catalog bypassed");HammerCatalogView.BlocksExchangeInput=false;
   Hud.Visible=true;Check(!CanOpenConstructionProject,"Hammer menu bypassed");Hud.Visible=false;
   for(int mode=1;mode<=3;mode++) {instance.session.Busy(mode);Check(!CanOpenConstructionProject,"Busy placement bypassed");}
   instance.session.Busy(0);Check(CanOpenConstructionProject&&!Player.m_localPlayer.Input(),"Board cancellation/input separation regressed");
   instance.constructionProjects.BlocksInput=false;Check(Player.m_localPlayer.Input(),"Closing board did not release input");
   instance.constructionProjects.BlocksInput=true;instance.session=null;
   Check(!Player.m_localPlayer.Input(),"Missing session bypassed board input block");
   Check(!CanOpenConstructionProject,"Missing session admitted project operation");
   return checks;
  }
 }
}
