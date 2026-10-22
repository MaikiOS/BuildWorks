[CmdletBinding()]
param([string]$ReportPath = '')
$ErrorActionPreference = 'Stop'
$taskSource = Get-Content (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintConstructionPayoutCollection.cs') -Raw
$taskCredit = $taskSource.IndexOf('        private void Credit()', [StringComparison]::Ordinal)
$taskStart = $taskSource.IndexOf('            int x = -1, y = -1;', $taskCredit, [StringComparison]::Ordinal)
$taskEnd = $taskSource.IndexOf('            var clean = source.m_itemData.Clone();', $taskStart, [StringComparison]::Ordinal)
if ($taskCredit -lt 0 -or $taskStart -lt 0 -or $taskEnd -lt $taskStart) { throw 'Actual refund-cell check missing.' }
$taskBlock = $taskSource.Substring($taskStart, $taskEnd - $taskStart)
$taskCode = @'
using System;
public static class RefundInventoryFullChecks {
 static class BuildWorksLocalization { public static string Text(string key)=>key; }
 static class MessageHud { public enum MessageType { Center } }
 sealed class Player {
  public int Messages;public string Text;public MessageHud.MessageType Type;
  public void Message(MessageHud.MessageType type,string text) { Messages++;Type=type;Text=text; }
 }
 sealed class Inventory {
  public readonly bool[,] Filled;public Inventory(int w,int h) { Filled=new bool[w,h]; }
  public int GetWidth()=>Filled.GetLength(0);public int GetHeight()=>Filled.GetLength(1);
  public object GetItemAt(int x,int y)=>Filled[x,y]?(object)this:null;
 }
 static int checks;
 static void Check(bool valid,string reason) { checks++;if(!valid)throw new Exception(reason); }
 static (int x,int y) Find(Inventory inventory,Player player) {
__BLOCK__
  return (x,y);
 }
 static void Full(int width,int height) {
  var inventory=new Inventory(width,height);var player=new Player();
  for(int y=0;y<height;y++)for(int x=0;x<width;x++)inventory.Filled[x,y]=true;
  try { Find(inventory,player);throw new Exception("Full inventory admitted credit"); }
  catch(InvalidOperationException error) {
   Check(player.Messages==1&&player.Text=="project.refund_inventory_full"&&player.Type==MessageHud.MessageType.Center,"Full inventory produced no localized player feedback");
   Check(error.Message==player.Text,"Player feedback and retained failure disagree");
  }
 }
 public static int Run() {
  checks=0;Full(2,2);Full(0,1);Full(1,0);
  var inventory=new Inventory(2,2);var player=new Player();
  inventory.Filled[0,0]=inventory.Filled[1,0]=true;
  var cell=Find(inventory,player);
  Check(cell.x==0&&cell.y==1&&player.Messages==0,"Free-cell selection changed or false full feedback emitted");
  return checks;
 }
}
'@
Add-Type -TypeDefinition $taskCode.Replace('__BLOCK__', $taskBlock)
$taskChecks = [RefundInventoryFullChecks]::Run()
if ($taskChecks -ne 7) { throw ('Full-inventory coverage differs: ' + $taskChecks) }
if ($ReportPath) { @{passed=$true;checks=$taskChecks;actualCellCheckCompiled=$true;nativeBoundariesSubstituted=$true;gameHostTested=$false} | ConvertTo-Json | Set-Content $ReportPath }
'PASS refund inventory full: 7 actual-block checks; player feedback and unchanged row-major free-cell selection. No native game proof.'
