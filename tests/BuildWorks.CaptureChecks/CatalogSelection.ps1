$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/HammerBlueprintPieceRegistry.cs'))
$start = $source.IndexOf('public bool SelectPiece(Player player, Piece piece)', [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Actual SelectPiece method missing.' }
$brace = $source.IndexOf('{', $start); $depth = 1; $end = $brace + 1
while ($depth -gt 0 -and $end -lt $source.Length) {
    if ($source[$end] -eq '{') { $depth++ }; if ($source[$end] -eq '}') { $depth-- }; $end++
}
if ($depth -ne 0) { throw 'Actual SelectPiece method is unbalanced.' }
$method = $source.Substring($start, $end - $start)
$session = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/PrecisionPlacementSession.cs'))
$start = $session.IndexOf('private bool SelectBuildPiece(Player player, Piece piece)', [StringComparison]::Ordinal)
if ($start -lt 0) { throw 'Actual SelectBuildPiece method missing.' }
$brace = $session.IndexOf('{', $start); $depth = 1; $end = $brace + 1
while ($depth -gt 0 -and $end -lt $session.Length) {
    if ($session[$end] -eq '{') { $depth++ }; if ($session[$end] -eq '}') { $depth-- }; $end++
}
if ($depth -ne 0) { throw 'Actual SelectBuildPiece method is unbalanced.' }
$sessionMethod = $session.Substring($start, $end - $start).Replace('private bool', 'public bool')
$fixture = @'
using System;
using System.Collections.Generic;
using UnityEngine;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object value) => value != null; }
 public class GameObject : Object { public bool activeInHierarchy=true; public void SetActive(bool active) { activeInHierarchy=active; } }
 public struct Vector2Int { public int x,y; public Vector2Int(int value, int row=0) { x=value; y=row; } }
}
public class Piece : UnityEngine.Object {
 public enum PieceCategory { Misc, Blueprint }
 public PieceCategory m_category = PieceCategory.Blueprint;
}
public class PieceTable : UnityEngine.Object {
 public const int m_gridWidth = 15;
 public readonly List<Piece> Visible = new List<Piece>();
 public Piece.PieceCategory Category;
 public bool GetPieceIndex(Piece piece, out Vector2Int index, out Piece.PieceCategory category) {
  index = new Vector2Int(Visible.IndexOf(piece)); category=piece.m_category;
  return index.x >= 0;
 }
 public Piece.PieceCategory GetSelectedCategory() => Category;
}
public class Player : UnityEngine.Object {
 public PieceTable Table = new PieceTable();
 public Piece Selected;
 public GameObject Ghost = new GameObject();
 public int SetCellCalls, GhostCalls;
 public bool DropOnRefresh, WrongCategory, WrongSelection;
 public void SetBuildCategory(Piece.PieceCategory category) {
  Table.Category = WrongCategory ? Piece.PieceCategory.Misc : category;
  Table.Visible.Reverse();
  if (DropOnRefresh) Table.Visible.Clear();
 }
 public void SetSelectedPiece(Vector2Int index) {
  SetCellCalls++; Selected = Table.Visible[WrongSelection ? 0 : index.x+index.y*PieceTable.m_gridWidth];
 }
 public bool SetSelectedPiece(Piece piece) {
  Vector2Int index; Piece.PieceCategory category;
  if (!Table.GetPieceIndex(piece, out index, out category)) return false;
  SetBuildCategory(category); SetSelectedPiece(index); return true;
 }
 public Piece GetSelectedPiece() => Selected;
}
public static class PrecisionPlacementSession {
 public static PieceTable GetBuildPieceTable(Player player) => player.Table;
 public static List<Piece> AvailableBuildPieces(PieceTable table, Piece.PieceCategory category) => table.Visible;
}
public static class HammerCatalogOrganizer {
 public static bool EnsureVisible(PieceTable table, Piece piece) => table.Visible.Contains(piece);
}
public class CatalogGhostHook { public void Invoke(Player player, object args) { player.GhostCalls++; player.Ghost=new GameObject(); } }
public class GhostFieldHook { public object GetValue(Player player) => player.Ghost; }
public class SessionGhostSelectionFixture {
 public bool isolatedEditorView; public int Restores, Layers;
 public Piece selectedPiece; public GameObject placementGhost;
 private readonly GhostFieldHook PlacementGhostField = new GhostFieldHook();
 private readonly CatalogSelectionFixture blueprintPieceRegistry = new CatalogSelectionFixture();
 private static PieceTable GetBuildPieceTable(Player player) => player.Table;
 private void RestoreEditorGhostLayers() { Restores++; }
 private void ApplyEditorGhostLayer(GameObject ghost) { Layers++; }
 __SESSION_METHOD__
}
public class CatalogSelectionFixture {
 private readonly CatalogGhostHook SetupPlacementGhostMethod = new CatalogGhostHook();
 __ACTUAL_METHOD__
 public static Player Create(Piece wanted) {
  var player = new Player(); player.Table.Visible.Add(wanted); player.Table.Visible.Add(new Piece()); return player;
 }
}
'@
Add-Type -TypeDefinition ($fixture.Replace('__ACTUAL_METHOD__', $method).Replace('__SESSION_METHOD__', $sessionMethod))
$check = [CatalogSelectionFixture]::new(); $wanted = [Piece]::new()
$legacy = [CatalogSelectionFixture]::Create($wanted)
if (!$legacy.SetSelectedPiece($wanted) -or $legacy.Selected -eq $wanted) { throw 'Fixture did not reproduce stale native cell.' }
$good = [CatalogSelectionFixture]::Create($wanted)
if (!$check.SelectPiece($good,$wanted) -or $good.Selected -ne $wanted -or $good.GhostCalls -ne 1) { throw 'Refresh selected the wrong piece.' }
foreach ($fault in @('DropOnRefresh','WrongCategory','WrongSelection')) {
    $bad = [CatalogSelectionFixture]::Create($wanted); $bad.$fault = $true
    if ($check.SelectPiece($bad,$wanted) -or $bad.GhostCalls -ne 0) { throw "Selection accepted $fault." }
    if ($fault -ne 'WrongSelection' -and $bad.SetCellCalls -ne 0) { throw "Invalid $fault selected a cell." }
}
if ($check.SelectPiece($null,$wanted) -or $check.SelectPiece($good,$null)) { throw 'Null selection accepted.' }
$sessionCheck = [SessionGhostSelectionFixture]::new()
$reuse = [CatalogSelectionFixture]::Create($wanted); $reuse.Selected=$wanted; $reuse.Ghost.SetActive($false)
$originalGhost = $reuse.Ghost
if (!$sessionCheck.SelectBuildPiece($reuse,$wanted) -or !$reuse.Ghost.activeInHierarchy -or
    $reuse.GhostCalls -ne 0 -or ![object]::ReferenceEquals($reuse.Ghost,$originalGhost)) {
    throw 'Same-prefab support candidate retained the ghost hidden by the previous native NoRayHits.'
}
$reuse.Ghost.SetActive($false); $sessionCheck.isolatedEditorView=$true
if (!$sessionCheck.SelectBuildPiece($reuse,$wanted) -or !$reuse.Ghost.activeInHierarchy -or
    $sessionCheck.Restores-ne1 -or $sessionCheck.Layers-ne1) { throw 'Editor ghost reuse lost activation/layer recovery.' }
$fresh = [CatalogSelectionFixture]::Create($wanted)
if (!$sessionCheck.SelectBuildPiece($fresh,$wanted) -or $fresh.Selected-ne$wanted -or
    $fresh.GhostCalls-ne1 -or !$fresh.Ghost.activeInHierarchy) { throw 'Changed-prefab native setup regressed.' }
$missing = [CatalogSelectionFixture]::Create($wanted); $missing.Selected=$wanted; $missing.Ghost=$null
if ($sessionCheck.SelectBuildPiece($missing,$wanted)) { throw 'Missing native ghost accepted.' }
$rejected = [CatalogSelectionFixture]::Create($wanted); $rejected.WrongSelection=$true; $rejected.Ghost.SetActive($false)
if ($sessionCheck.SelectBuildPiece($rejected,$wanted) -or $rejected.Ghost.activeInHierarchy -or
    $sessionCheck.SelectBuildPiece($null,$wanted) -or $sessionCheck.SelectBuildPiece($reuse,$null)) { throw 'Rejected selection activated a ghost.' }
$catalog = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/UnifiedHammerCatalog.cs'))
if ($catalog -notmatch '(?s)private static void DeleteBlueprint\(.*?if \(!restore && !blueprintRegistry\.SelectSafePieceBeforeDelete\(player, blueprint\)\)\s*\{\s*ShowStatus\([^;]+;\s*return;\s*\}\s*string error;\s*bool success = blueprint\.IsExternal \? blueprintStore\.TrySetHidden') {
    throw 'Failed safe selection no longer stops deletion before the store mutation.'
}
'PASS actual SelectPiece: reproduced stale native cell; fresh cell selects exact reference; missing/category/mismatch/null fail closed. SDK refresh substituted; native UI still required.'
'PASS actual SelectBuildPiece: hidden same-prefab ghost reactivated without setup/replacement; editor layers, changed prefab, missing ghost and rejected selection checked. Unity lifecycle/native placement still require game QA.'
