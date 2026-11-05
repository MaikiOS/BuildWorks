[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/PrecisionPlacementSession.cs'))
$start = $source.IndexOf('        public bool ApplyPassiveAutoJoin(')
if ($start -lt 0) { throw 'ApplyPassiveAutoJoin not found.' }
$end = $source.IndexOf('        public void ApplyValidationPoint(', $start)
if ($end -le $start) { throw 'ApplyPassiveAutoJoin end not found.' }
$method = $source.Substring($start, $end - $start)
# Execute the real method with inert host substitutes; no game/Unity invocation.
$fixture = @'
using System;
using System.Collections.Generic;
using System.Reflection;
namespace PassiveAutoJoinChecks {
public class Obj { public static implicit operator bool(Obj o) => o != null; }
public struct Vector3 {
    public float x,y,z;
    public Vector3(float x,float y,float z) {this.x=x;this.y=y;this.z=z;}
    public static Vector3 operator -(Vector3 a,Vector3 b) => new Vector3(a.x-b.x,a.y-b.y,a.z-b.z);
    public float sqrMagnitude => x*x+y*y+z*z;
    public static float Distance(Vector3 a,Vector3 b) => (float)Math.Sqrt((a-b).sqrMagnitude);
}
public class Transform : Obj {
    public Vector3 position; public Vector3 localScale=new Vector3(1,1,1); public int rotation; public Transform parent; public Piece owner;
    public bool IsChildOf(Transform target) {for(var p=this;p!=null;p=p.parent)if(p==target)return true;return false;}
    public T GetComponentInParent<T>() where T:class {for(var p=this;p!=null;p=p.parent)if(p.owner is T)return p.owner as T;return null;}
}
public class GameObject : Obj {
    public Transform transform; public Piece piece; public bool activeInHierarchy=true;
    public T GetComponent<T>() where T:class => piece as T;
}
public class Piece : Obj {
    public Transform transform=new Transform(); public List<Transform> points=new List<Transform>();
    public int Reads; public bool Supported=true;
    public Piece(){transform.owner=this;}
    public void GetSnapPoints(List<Transform> result){++Reads;result.AddRange(points);}
}
public class Player {public static Player m_localPlayer;public bool AlternativePlacementActive;public GameObject Ghost;public int Manual=-1;public Piece Selected;public Piece GetSelectedPiece()=>Selected;}
public enum PlacementState {Inactive,Armed}
public class Registry {public bool IsBlueprint;public Piece RegisteredPiece;public bool TryGetBlueprint(Piece piece,out object value){value=null;return IsBlueprint&&(RegisteredPiece==null||ReferenceEquals(piece,RegisteredPiece));}}
public static class BlueprintPlacementFrame {
    public static int Calls; public static bool Blueprint;
    public static int AutoJoinRotation(int target,int ghost,bool blueprint){++Calls;Blueprint=blueprint;return blueprint?ghost:target+ghost;}
}
public partial class Harness {
    public PlacementState state=PlacementState.Inactive;public bool autoAlignmentEnabled=true;
    private static FieldInfo PlacementGhostField=typeof(Player).GetField("Ghost"),ManualSnapPointField=typeof(Player).GetField("Manual");
    private List<Transform> sourceNativeSnapPoints=new List<Transform>(),nativeSnapPoints=new List<Transform>();
    public Registry blueprintPieceRegistry=null;
    private bool SupportsPrecisionPlacement(Piece selected)=>selected&&selected.Supported;
}
public static class Cases {
    private static int assertions;
    private static void Require(bool value,string label){++assertions;if(!value)throw new InvalidOperationException(label);}
    private sealed class Setup {
        internal Piece GhostPiece=new Piece(), NativePiece=new Piece(), AimedPiece=new Piece();
        internal Player Player;internal Harness Session=new Harness();internal Transform Source,Target;
        internal Setup(){
            var ghost=new GameObject{transform=GhostPiece.transform,piece=GhostPiece};
            Source=new Transform{parent=ghost.transform};GhostPiece.points.Add(Source);
            Target=new Transform{parent=NativePiece.transform,position=new Vector3(.1f,0,0)};
            NativePiece.points.Add(Target);NativePiece.transform.rotation=7;AimedPiece.transform.rotation=13;ghost.transform.rotation=5;
            Player=new Player{Ghost=ghost,Selected=GhostPiece};Player.m_localPlayer=Player;
            Session.blueprintPieceRegistry=new Registry{IsBlueprint=true};
            BlueprintPlacementFrame.Calls=0;
        }
        internal Transform Aim(float x,float y=0){var t=new Transform{parent=AimedPiece.transform,position=new Vector3(x,y,0)};AimedPiece.points.Add(t);return t;}
        internal bool Apply(Piece aimed){return Session.ApplyPassiveAutoJoin(Player,ref Source,ref Target,aimed);}
    }
    public static string Run(){
        foreach(int manual in new[]{-1,0})foreach(int targetRotation in new[]{0,7,90,180,270})
        foreach(var scale in new[]{new Vector3(1,1,1),new Vector3(1.3f,.8f,1.1f),new Vector3(.5f,2,1.25f)}){
            var s=new Setup();s.Session.blueprintPieceRegistry.IsBlueprint=false;s.Player.Manual=manual;
            s.AimedPiece.transform.rotation=targetRotation;s.NativePiece.transform.rotation=targetRotation;
            s.AimedPiece.transform.localScale=s.NativePiece.transform.localScale=scale;
            var ghost=s.Player.Ghost.transform;ghost.position=new Vector3(10,20,30);ghost.localScale=new Vector3(.75f,2,1.5f);
            var position=ghost.position;var ghostScale=ghost.localScale;
            s.Aim(0);var source=s.Source;var target=s.Target;
            Require(s.Apply(s.AimedPiece),"ordinary native pair vetoed");
            Require(s.Source==source&&s.Target==target,"ordinary native pair replaced by aimed-piece search");
            Require(s.Player.Ghost.transform.rotation==5,"ordinary native wheel rotation inherited hidden target frame");
            Require(ghost.position.Equals(position)&&ghost.localScale.Equals(ghostScale),"ordinary hook changed supplied ghost position/scale");
            Require(s.AimedPiece.Reads==0&&s.GhostPiece.Reads==0,"ordinary snap performed a second point scan");
        }
        {
            // Keep one session/registry across tool and host-ghost replacements; no cached classification may leak.
            var s=new Setup();var composite=new Piece();var ordinaryB=new Piece();ordinaryB.transform.rotation=90;
            var unsupported=new Piece{Supported=false};
            s.Session.blueprintPieceRegistry.RegisteredPiece=composite;
            var candidate=s.Aim(.2f);
            foreach(Piece selected in new[]{s.GhostPiece,ordinaryB,null,unsupported,composite,s.GhostPiece}){
                s.Player.Selected=selected;
                var host=new Piece();host.transform.position=new Vector3(7,8,9);host.transform.rotation=37;
                host.transform.localScale=new Vector3(.5f,2,1.25f);
                s.Player.Ghost=new GameObject{piece=host,transform=host.transform};
                s.Source=new Transform{parent=host.transform};host.points.Add(s.Source);
                s.Target=new Transform{parent=s.NativePiece.transform,position=new Vector3(.1f,0,0)};
                var source=s.Source;var target=s.Target;int reads=s.AimedPiece.Reads;
                Require(s.Apply(s.AimedPiece),"tool switch vetoed native snap");
                Require(s.Player.Ghost.transform.rotation==37&&host.transform.position.Equals(new Vector3(7,8,9))&&
                    host.transform.localScale.Equals(new Vector3(.5f,2,1.25f)),"tool/ghost switch inherited old pose");
                if(selected==composite)
                    Require(s.Target==candidate&&s.AimedPiece.Reads==reads+1,"fresh composite selection failed to use current ghost/aim");
                else Require(s.Source==source&&s.Target==target&&s.AimedPiece.Reads==reads&&host.Reads==0,
                    "ordinary/null/unsupported selection reused composite rerank after tool switch");
            }
        }
        foreach(float radius in new[]{.499f,.5f}){
            var s=new Setup();var candidate=s.Aim(radius);
            Require(s.Apply(s.AimedPiece),"valid aimed pair vetoed at "+radius);
            Require(s.Target==candidate,"aimed pair not selected at inclusive radius "+radius);
        }
        foreach(int aim in new[]{0,1,2,3,4,5}){
            var s=new Setup();Transform originalSource=s.Source,originalTarget=s.Target;
            Piece aimed=aim==0?null:aim==1?s.GhostPiece:s.AimedPiece;
            if(aim==2)s.Aim(.5001f);if(aim==3)s.Aim(float.NaN);if(aim==4)s.Aim(float.PositiveInfinity);
            if(aim==5)s.AimedPiece.transform.parent=s.Player.Ghost.transform;
            Require(s.Apply(aimed),"native pair vetoed without eligible aim "+aim);
            Require(s.Source==originalSource&&s.Target==originalTarget,"native pair overwritten without candidate "+aim);
            Require(BlueprintPlacementFrame.Calls==0&&s.Player.Ghost.transform.rotation==5,"fallback changed native wheel rotation "+aim);
        }
        {
            var s=new Setup();var first=s.Aim(.25f,.000075f);var second=s.Aim(.25f);
            Require(Vector3.Distance(first.position,s.Source.position)==Vector3.Distance(second.position,s.Source.position),"fixture no longer has rounded distance tie");
            Require((first.position-s.Source.position).sqrMagnitude>(second.position-s.Source.position).sqrMagnitude,"fixture must distinguish squared ordering");
            Require(s.Apply(s.AimedPiece)&&s.Target==second,"rounded sqrt tie did not use geometric target order");
            s.AimedPiece.points.Reverse();
            Require(s.Apply(s.AimedPiece)&&s.Target==second,"rounded sqrt tie changed after permutation");
        }
        {
            var s=new Setup();s.Aim(.25f);var canonical=s.Aim(-.25f);
            Require(s.Apply(s.AimedPiece)&&s.Target==canonical,"exact tie did not use geometric target order");
            s.AimedPiece.points.Reverse();
            Require(s.Apply(s.AimedPiece)&&s.Target==canonical,"exact tie changed after permutation");
        }
        {
            var s=new Setup();var candidate=s.Aim(.2f);var nearest=s.Aim(.15f);
            s.AimedPiece.points.Insert(0,null);s.GhostPiece.points.Insert(0,null);
            Require(s.Apply(s.AimedPiece)&&s.Target==nearest&&s.Target!=candidate,"null entries or nearest target changed");
        }
        foreach(int guard in new[]{0,1,2,3,4,5,6,7,8}){
            var s=new Setup();var original=s.Target;s.Aim(0);
            if(guard==0)s.Player.Manual=0;if(guard==1)s.Session.autoAlignmentEnabled=false;
            if(guard==2)s.Session.state=PlacementState.Armed;if(guard==3)s.Player.AlternativePlacementActive=true;
            if(guard==4)Player.m_localPlayer=new Player();if(guard==5)s.Player.Ghost.activeInHierarchy=false;
            if(guard==6)s.Player.Selected=null;if(guard==7)s.Source=null;if(guard==8)s.Target=null;
            var beforeTarget=s.Target;
            Require(s.Apply(s.AimedPiece),"guard vetoed supplied native result "+guard);
            Require(s.Target==beforeTarget&&s.AimedPiece.Reads==0,"guard performed automatic rerank "+guard);
            Require(BlueprintPlacementFrame.Calls==0&&s.Player.Ghost.transform.rotation==5,"guard changed native rotation "+guard);
        }
        {
            var s=new Setup();s.Session.blueprintPieceRegistry=new Registry{IsBlueprint=true};
            s.Aim(.25f);
            Require(s.Apply(s.AimedPiece)&&s.Player.Ghost.transform.rotation==5&&BlueprintPlacementFrame.Calls==0,"composite changed wheel rotation");
        }
        return "PASS "+assertions+" assertions: actual session method, ordinary pair/position/scale/rotation invariant across supplied target scales/hidden frames/manual-auto and selected/tool/ghost switches, composite inclusive radius/sqrt ties, native fallback, null/invalid/guards. Inert host only; rotation is an integer substitute, supplied scale is not native TRS; no native translation, quaternion math, physics or game proof.";
    }
}
}
'@
$fixture += "`nnamespace PassiveAutoJoinChecks { public partial class Harness {`n"+$method+"`n} }"
$selector = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BlueprintHammerSnapSearch.cs'))
$pairStart = $selector.IndexOf('        internal static bool PairPrecedes(')
$pairEnd = $selector.IndexOf('        private static bool FindClosest(', $pairStart)
if ($pairStart -lt 0 -or $pairEnd -le $pairStart) { throw 'Geometric pair ordering not found.' }
$fixture += "`nnamespace PassiveAutoJoinChecks { public static class BlueprintHammerSnapSearch {`n" + $selector.Substring($pairStart, $pairEnd-$pairStart) + "`n} }"
Add-Type -TypeDefinition $fixture
[PassiveAutoJoinChecks.Cases]::Run()
