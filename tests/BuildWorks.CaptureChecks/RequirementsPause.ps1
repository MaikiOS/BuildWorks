[CmdletBinding()]
param([string]$ReportPath = '')
$ErrorActionPreference='Stop'
$taskSource=Get-Content (Join-Path $PSScriptRoot '../../src/BuildWorks/PrecisionPlacementSession.cs') -Raw
$taskSignature='private string RequirementsPauseReason(Player player)'
$taskStart=$taskSource.IndexOf($taskSignature,[StringComparison]::Ordinal)
if($taskStart -lt 0){throw 'Actual pause-reason method missing'}
$taskBrace=$taskSource.IndexOf('{',$taskStart);$taskDepth=1;$taskEnd=$taskBrace+1
while($taskDepth -gt 0 -and $taskEnd -lt $taskSource.Length){if($taskSource[$taskEnd] -eq '{'){$taskDepth++};if($taskSource[$taskEnd] -eq '}'){$taskDepth--};$taskEnd++}
if($taskDepth){throw 'Unbalanced pause-reason method'}
if($taskSource -notmatch 'if \(!noCost && !player.HaveRequirements\(\s*selectedPiece,\s*Player.RequirementMode.CanBuild\)\)\s*\{\s*PauseAutomaticPlacement\(player, RequirementsPauseReason\(player\)\);') {throw 'Native requirements gate changed or reason is not wired'}
$taskHost=@'
using System;
public sealed class RequirementsPauseChecks {
 class Position { }
 class Transform {public Position position=new Position();}
 class Player {public Transform transform=new Transform();}
 class CraftingStation {
  public string m_name="$piece_workbench";
  public static bool InRange;public static int Calls;public static string Name;public static Position At;
  public static implicit operator bool(CraftingStation value)=>value!=null;
  public static bool HaveBuildStationInRange(string name,Position position){Calls++;Name=name;At=position;return InRange;}
 }
 class Piece {public CraftingStation m_craftingStation;public string m_dlc="";}
 enum GlobalKeys{NoWorkbench}
 class ZoneSystem {public static ZoneSystem instance=new ZoneSystem();public bool NoStation;public bool GetGlobalKey(GlobalKeys key)=>NoStation;}
 class DLCMan {public static DLCMan instance=new DLCMan();public bool Installed=true;public bool IsDLCInstalled(string dlc)=>Installed;}
 class Localization {public static Localization instance=new Localization();public string Localize(string key)=>"localized:"+key;}
 static class BuildWorksLocalization {public static string Text(string key,params object[] args)=>key+(args.Length==0?"":":"+args[0]);}
 Piece selectedPiece=new Piece();
__METHOD__
 static int checks;
 static void Check(bool valid,string reason){checks++;if(!valid)throw new Exception(reason);}
 public static int Run(){
  checks=0;var session=new RequirementsPauseChecks();var player=new Player();CraftingStation.Calls=0;
  Check(session.RequirementsPauseReason(player)=="placement.pause.resources"&&CraftingStation.Calls==0,"No-station material refusal mislabeled");
  session.selectedPiece.m_craftingStation=new CraftingStation();CraftingStation.InRange=false;
  Check(session.RequirementsPauseReason(player)=="placement.pause.station:localized:$piece_workbench","Missing workbench still says resources");
  Check(CraftingStation.Name=="$piece_workbench"&&ReferenceEquals(CraftingStation.At,player.transform.position),"Native station query did not use player position/name");
  CraftingStation.InRange=true;
  Check(session.RequirementsPauseReason(player)=="placement.pause.resources","In-range station masked material shortage");
  CraftingStation.InRange=false;ZoneSystem.instance.NoStation=true;
  Check(session.RequirementsPauseReason(player)=="placement.pause.resources","NoWorkbench global key ignored");
  session.selectedPiece.m_dlc="odin";DLCMan.instance.Installed=false;
  Check(session.RequirementsPauseReason(player)=="placement.pause.dlc","Missing DLC mislabeled resources");
  ZoneSystem.instance.NoStation=false;
  Check(session.RequirementsPauseReason(player).StartsWith("placement.pause.station:"),"Station/DLC order differs from native CanBuild");
  DLCMan.instance.Installed=true;Localization.instance=null;session.selectedPiece.m_craftingStation.m_name="$modded_station";
  Check(session.RequirementsPauseReason(player)=="placement.pause.station:$modded_station","Native name fallback lost without Localization");
  CraftingStation.InRange=true;
  Check(session.RequirementsPauseReason(player)=="placement.pause.resources","Installed DLC and station blocked material reason");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $taskHost.Replace('__METHOD__',$taskSource.Substring($taskStart,$taskEnd-$taskStart))
$taskChecks=[RequirementsPauseChecks]::Run()
if($taskChecks -ne 9){throw ('Requirements reason coverage differs: '+$taskChecks)}
if($ReportPath){@{passed=$true;checks=$taskChecks;actualReasonMethodCompiled=$true;nativeBoundariesSubstituted=$true;gameHostTested=$false}|ConvertTo-Json|Set-Content $ReportPath}
"PASS requirements pause: $taskChecks actual-method assertions; native requirements gate retained; no game proof."
