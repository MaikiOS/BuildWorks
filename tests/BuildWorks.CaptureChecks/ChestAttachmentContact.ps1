param([switch]$OriginalWaterGate)
$ErrorActionPreference = 'Stop'
$project = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$source = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/PrecisionPlacementSession.cs'))
function Read-Method([string]$signature, [string]$text = $source) {
    $start = $text.IndexOf($signature, [StringComparison]::Ordinal)
    if ($start -lt 0) { throw "Method missing: $signature" }
    $brace = $text.IndexOf('{', $start); $depth = 1; $end = $brace + 1
    while ($depth -gt 0 -and $end -lt $text.Length) {
        if ($text[$end] -eq '{') { $depth++ }; if ($text[$end] -eq '}') { $depth-- }; $end++
    }
    if ($depth -ne 0) { throw 'Unbalanced method.' }
    $text.Substring($start, $end - $start)
}
$policySource = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/BuildWorksPlacementValidation.cs'))
$policy = Read-Method 'internal static bool UsesSurfaceAttachment(' $policySource
$policy += Read-Method 'internal static bool IsSurfaceAttachmentSupport(' $policySource
$record = Read-Method 'internal void RecordNativeChestAttachmentContact('
$begin = Read-Method 'internal void BeginNativePlacementRay()'
$has = [regex]::Match($source, 'internal bool HasNativeChestAttachmentContact\(Player player\) =>\s*[^;]+;').Value
$plugin = [IO.File]::ReadAllText((Join-Path $project 'src/BuildWorks/BuildWorksPlugin.cs'))
$rule = [regex]::Match($plugin, 'private static bool ApplyChestAttachmentTiltRule\(bool requireHorizontal, Player player\) =>\s*[^;]+;').Value
$supportRule = [regex]::Match($plugin, 'private static bool ApplySurfaceAttachmentSupportRule\(bool nativeSupports, Player player\) =>\s*[^;]+;').Value
$clearanceRule = [regex]::Match($plugin, 'private static float ApplySurfaceAttachmentClearance\(float nativeClearance, Player player\) =>\s*[^;]+;').Value
$biomeRule = [regex]::Match($plugin, 'private static Heightmap.Biome ApplySurfaceAttachmentBiome\(Heightmap.Biome nativeBiome, Player player\) =>\s*[^;]+;').Value
$connectionRule = [regex]::Match($plugin, 'private static ZNetView ApplySurfaceAttachmentConnection\(ZNetView nativeConnection, Player player\) =>\s*[^;]+;').Value
$stationRule = [regex]::Match($plugin, 'private static Player.PlacementStatus ApplySurfaceAttachmentStationRule\(Player.PlacementStatus nativeStatus, Player player\) =>\s*[^;]+;').Value
$contactCallback = Read-Method 'private static bool ApplyBlueprintValidationContact(' $plugin
if ($OriginalWaterGate) {
    $needle = 'if (!surfaceAttachment && source.m_noInWater'
    if (!$contactCallback.Contains($needle)) { throw 'The water regression control no longer matches the actual callback.' }
    $contactCallback = $contactCallback.Replace($needle, 'if (source.m_noInWater')
}
if (!$has -or !$rule -or !$supportRule -or !$clearanceRule -or !$biomeRule -or !$connectionRule -or !$stationRule) { throw 'Actual contact accessor/geometry/environment rules missing.' }
$fixture = @'
using System;
using UnityEngine;
namespace UnityEngine {
 public class Object { public static implicit operator bool(Object value) => value != null; }
 public class GameObject : Object { public string name="piece_chest_wood"; public int layer; public bool activeInHierarchy=true; }
 public struct Vector3 { public float x,y,z; public Vector3(float a,float b,float c){x=a;y=b;z=c;} public static Vector3 zero=>default; }
 public struct Bounds { public Vector3 center,min; }
 public static class LayerMask { public static int NameToLayer(string name)=>4; }
 public static class Debug { public static void LogError(object value){throw new Exception("Unexpected callback error: "+value);} }
 public class Rigidbody : Object {}
 public class Collider : Object {
  public bool isTrigger; public bool enabled=true; public Rigidbody attachedRigidbody; public WearNTear Support; public Container Container; public GameObject gameObject=new GameObject();
  public T GetComponentInParent<T>() where T:class => typeof(T)==typeof(Container) ? Container as T : Support as T;
  public T GetComponent<T>() where T:class => null;
 }
 public struct RaycastHit { public Collider collider; public Vector3 point,normal; }
 public static class Time { public static int frameCount=17; }
}
public class Piece : UnityEngine.Object { public GameObject gameObject=new GameObject(); public UsageTagFlags m_usage=(UsageTagFlags)1152;
 public bool m_inCeilingOnly,m_notOnFloor,m_groundPiece,m_groundOnly,m_cultivatedGroundOnly,m_vegetationGroundOnly,m_noInWater;
 public float m_extraPlacementDistance;
    [System.Flags] public enum UsageTagFlags
    { Crafting = 2, Building = 4, Floor = 8, Wall = 16, Roof = 32, Architecture = 64,
      Furniture = 128, Storage = 1024, Transport = 2048, Stairs = 131072, Doors = 262144 }
}
public enum PlacementContactSides { All,Below,Above,NonFloor }
public enum PlacementContactFailure { None,NoContact }
public static class BuildWorksPlacementValidation { __POLICY__
 public static bool Contact=true; public static Collider Hit=new Collider();
 public static bool TryGetBounds(GameObject ghost,out Bounds bounds){bounds=new Bounds{center=new Vector3(0,.5f,0)};return true;}
 public static float ContactTolerance(Bounds bounds)=>.002f;
 public static bool TryFindContact(GameObject ghost,Bounds bounds,int mask,Vector3 eye,float distance,PlacementContactSides sides,out RaycastHit hit,out PlacementContactFailure failure,bool surfaceAttachment=false){hit=new RaycastHit{collider=Hit};failure=Contact?PlacementContactFailure.None:PlacementContactFailure.NoContact;return Contact;}
}
public static class BlueprintSurfaceContact { public static bool NeedsAdapter(GameObject ghost)=>false; }
public enum LiquidType { Water }
public class WaterVolume : UnityEngine.Object { public Collider Shape; public T GetComponent<T>() where T:class =>Shape as T; }
public static class Floating {
 public static float Level=1; public static WaterVolume Volume; public static int LiquidReads,WaterReads;
 public static float GetLiquidLevel(Vector3 position,float wave,LiquidType type){LiquidReads++;return Level;}
 public static float GetWaterLevel(Vector3 position,ref WaterVolume volume){WaterReads++;volume=Volume;return Level;}
}
public class Heightmap { [Flags] public enum Biome { None=0, Meadows=1, Mistlands=512 } }
public class Player : UnityEngine.Object {
 public enum PlacementStatus { Valid, Invalid, PrivateZone, BlockedbyPlayer, ExtensionMissingStation=7 }
 public PlacementStatus CurrentStatus;
 public PlacementStatus GetPlacementStatus()=>CurrentStatus;
 public int RayMask=1;
 public Vector3 GetEyePoint()=>new Vector3(0,2,0);
}
public class ZNetView : UnityEngine.Object { public bool Valid=true; public bool IsValid()=>Valid; }
public class Container : UnityEngine.Object {}
public class WearNTear : UnityEngine.Object {
 public bool m_supports=true; public ZNetView View=new ZNetView();
 public T GetComponent<T>() where T:class => View as T;
}
public static class Utils { public static string GetPrefabName(GameObject value)=>value.name; }
public class ChestAttachmentFixture {
 public readonly Player Actor=new Player(); public bool InContext=true;
 public Piece Source=new Piece(); public bool DistanceReady=true;
 public bool TryTakeNativePlacementRayDistance(out float distance){distance=10;return DistanceReady;}
 public bool TryPrepareBlueprintValidation(Player player,out GameObject ghost,out Piece source){source=Source;ghost=Source.gameObject;return InContext;}
 public void RecordPlacementContactFailure(PlacementContactFailure failure){}
 public bool TryFindSurfaceContact(GameObject ghost,int mask,Vector3 eye,float distance,out RaycastHit hit,out PlacementContactFailure failure)=>BuildWorksPlacementValidation.TryFindContact(ghost,default,mask,eye,distance,PlacementContactSides.All,out hit,out failure,true);
 private int nativeChestAttachmentFrame=-1; private float? nativePlacementRayDistance;
 public float? Distance=>nativePlacementRayDistance;
 public bool RequiresPlacementContact(Player player)=>InContext && player==Actor;
 __BEGIN__
 __HAS__
 __RECORD__
 public bool Has()=>HasNativeChestAttachmentContact(Actor);
 public void Begin()=>BeginNativePlacementRay();
 public void Record(Piece source, Collider collider)=>RecordNativeChestAttachmentContact(Actor,source,new RaycastHit{collider=collider});
}
public class BuildWorksPlugin {
 public ChestAttachmentFixture session;
 private static BuildWorksPlugin instance;
 private static System.Reflection.FieldInfo PlacementRayMaskField=typeof(Player).GetField("RayMask"),PlacementWaterRayMaskField=PlacementRayMaskField;
 __RULE__
 __SUPPORT_RULE__
 __CLEARANCE_RULE__
 __BIOME_RULE__
 __CONNECTION_RULE__
 __STATION_RULE__
 __CONTACT_CALLBACK__
 public static bool Rule(ChestAttachmentFixture value,bool native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplyChestAttachmentTiltRule(native,player);
 }
 public static bool Support(ChestAttachmentFixture value,bool native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplySurfaceAttachmentSupportRule(native,player);
 }
 public static float Clearance(ChestAttachmentFixture value,float native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplySurfaceAttachmentClearance(native,player);
 }
 public static Heightmap.Biome Biome(ChestAttachmentFixture value,Heightmap.Biome native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplySurfaceAttachmentBiome(native,player);
 }
 public static ZNetView Connection(ChestAttachmentFixture value,ZNetView native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplySurfaceAttachmentConnection(native,player);
 }
 public static Player.PlacementStatus Station(ChestAttachmentFixture value,Player.PlacementStatus native,Player player) {
  instance=value==null ? null : new BuildWorksPlugin{session=value};
  return ApplySurfaceAttachmentStationRule(native,player);
 }
 public static bool ContactCallback(ChestAttachmentFixture value,out Collider waterSurface) {
  instance=new BuildWorksPlugin{session=value};Vector3 point=default,normal=default;Piece hitPiece=null;Heightmap heightmap=null;waterSurface=null;
  return ApplyBlueprintValidationContact(true,value.Actor,ref point,ref normal,ref hitPiece,ref heightmap,ref waterSurface,false);
 }
}
'@
Add-Type -TypeDefinition ($fixture.Replace('__POLICY__',$policy).Replace('__BEGIN__',$begin).Replace('__HAS__',$has).Replace('__RECORD__',$record).Replace('__RULE__',$rule).Replace('__SUPPORT_RULE__',$supportRule).Replace('__CLEARANCE_RULE__',$clearanceRule).Replace('__BIOME_RULE__',$biomeRule).Replace('__CONNECTION_RULE__',$connectionRule).Replace('__STATION_RULE__',$stationRule).Replace('__CONTACT_CALLBACK__',$contactCallback))
$f=[ChestAttachmentFixture]::new(); $piece=[Piece]::new();$wall=[UnityEngine.Collider]::new();$wall.Support=[WearNTear]::new()
if (![BuildWorksPlugin]::Rule($f,$true,$f.Actor)) { throw 'Initial native tilt rule was bypassed.' }
$f.Record($piece,$wall)
if (!$f.Has() -or [BuildWorksPlugin]::Rule($f,$true,$f.Actor)) { throw 'Real wall contact did not scope the tilt exception.' }
if ([BuildWorksPlugin]::Rule($f,$false,$f.Actor)) { throw 'A native false rule changed to true.' }
if (![BuildWorksPlugin]::Rule($null,$true,$f.Actor) -or ![BuildWorksPlugin]::Rule($f,$true,[Player]::new())) { throw 'Absent/other context bypassed native rule.' }
[UnityEngine.Time]::frameCount++
if ($f.Has()) { throw 'Old-frame contact remained valid.' }
$f.Record($piece,$wall);$f.Begin()
if ($f.Has()) { throw 'New native ray retained old contact.' }
foreach($fault in @('missing','trigger','rigidbody','missingView','invalidView','wrongPrefab','inactive')) {
    $f=[ChestAttachmentFixture]::new();$p=[Piece]::new();$c=[UnityEngine.Collider]::new();$c.Support=[WearNTear]::new()
    switch($fault) {
        missing {$c=$null}
        trigger {$c.isTrigger=$true}
        rigidbody {$c.attachedRigidbody=[UnityEngine.Rigidbody]::new()}
        missingView {$c.Support.View=$null}
        invalidView {$c.Support.View.Valid=$false}
        wrongPrefab {$p.gameObject.name='woodwall';$p.m_usage=[Piece+UsageTagFlags]20}
        inactive {$f.InContext=$false}
    }
    $f.Record($p,$c)
    if($f.Has() -or ![BuildWorksPlugin]::Rule($f,$true,$f.Actor)){throw "Invalid contact accepted: $fault"}
}
foreach($usage in @(2,128,1024,1152)) {
    $f=[ChestAttachmentFixture]::new();$p=[Piece]::new();$p.gameObject.name='functional_test';$p.m_usage=[Piece+UsageTagFlags]$usage
    $f.Record($p,$wall)
    if(!$f.Has()){throw "Functional contact was restricted to wooden chest: $usage"}
}
foreach($usage in @(0,20,36,68,2048,144)) {
    $f=[ChestAttachmentFixture]::new();$p=[Piece]::new();$p.m_usage=[Piece+UsageTagFlags]$usage
    $f.Record($p,$wall)
    if($f.Has()){throw "Structural/unknown/mixed role acquired contact exception: $usage"}
}
$f=[ChestAttachmentFixture]::new();$solid=[UnityEngine.Collider]::new();$f.Record($piece,$solid)
if(!$f.Has()){throw 'Solid static native support was rejected.'}
$f.Record($piece,$null)
if($f.Has()){throw 'A later failed contact retained a prior grant.'}
foreach($usage in @(2,3,384)) {
    foreach($material in @(0,3)) {
        foreach($fault in @('fresh','stale','structural','inactive','nonSupporting')) {
            $f=[ChestAttachmentFixture]::new();$p=[Piece]::new();$p.m_usage=[Enum]::ToObject([Piece+UsageTagFlags],[int]$usage)
            $c=[UnityEngine.Collider]::new();$c.Support=[WearNTear]::new()
            if($fault -eq 'structural'){$p.m_usage=[Piece+UsageTagFlags]20}
            if($fault -eq 'inactive'){$f.InContext=$false}
            if($fault -eq 'nonSupporting'){$c.Support.m_supports=$false}
            $f.Record($p,$c)
            if($fault -eq 'stale'){[UnityEngine.Time]::frameCount++}
            $blocked=[BuildWorksPlugin]::Rule($f,$true,$f.Actor) -and $material -in @(0,3)
            if($blocked -ne ($fault -ne 'fresh')){throw "Wood/HardWood surface predicate regression: usage=$usage material=$material context=$fault"}
            if([BuildWorksPlugin]::Rule($f,$false,$f.Actor)){throw 'False native surface flag became true.'}
        }
    }
}
foreach($fault in @('fresh','stale','structural','inactive','otherPlayer','noContact','trigger','rigidbody','invalidView','nonSupporting','container','disabled','inactiveCollider')) {
    $f=[ChestAttachmentFixture]::new();$p=[Piece]::new();$c=[UnityEngine.Collider]::new();$c.Support=[WearNTear]::new()
    if($fault -eq 'structural'){$p.m_usage=[Piece+UsageTagFlags]20}
    if($fault -eq 'inactive'){$f.InContext=$false}
    if($fault -eq 'trigger'){$c.isTrigger=$true}
    if($fault -eq 'rigidbody'){$c.attachedRigidbody=[UnityEngine.Rigidbody]::new()}
    if($fault -eq 'invalidView'){$c.Support.View.Valid=$false}
    if($fault -eq 'nonSupporting'){$c.Support.m_supports=$false}
    if($fault -eq 'container'){$c.Container=[Container]::new()}
    if($fault -eq 'disabled'){$c.enabled=$false}
    if($fault -eq 'inactiveCollider'){$c.gameObject.activeInHierarchy=$false}
    if($fault -ne 'noContact'){$f.Record($p,$c)}
    if($fault -eq 'stale'){[UnityEngine.Time]::frameCount++}
    $actor=if($fault -eq 'otherPlayer'){[Player]::new()}else{$f.Actor}
    $allow=$fault -eq 'fresh'
    if([BuildWorksPlugin]::Support($f,$false,$actor) -ne $allow){throw "Functional non-load-bearing contact: $fault"}
    if(![BuildWorksPlugin]::Support($f,$true,$actor)){throw 'Native support=true must be retained.'}
    foreach($radius in @(0,0.2,2.5)) {
        $expected=if($allow){[float]0}else{[float]$radius}
        if([BuildWorksPlugin]::Clearance($f,[float]$radius,$actor) -ne $expected){throw "Overlap clearance escaped fresh context: $fault/$radius"}
    }
    $biome=[Heightmap+Biome]513
    $expectedBiome=if($allow){[Heightmap+Biome]0}else{$biome}
    if([BuildWorksPlugin]::Biome($f,$biome,$actor) -ne $expectedBiome){throw "Environment biome escaped fresh scope: $fault"}
    $connection=[ZNetView]::new()
    $actualConnection=[BuildWorksPlugin]::Connection($f,$connection,$actor)
    if($allow -and $null -ne $actualConnection -or !$allow -and ![object]::ReferenceEquals($connection,$actualConnection)){throw "Environment connection escaped fresh scope: $fault"}
    if($null -ne [BuildWorksPlugin]::Connection($f,$null,$actor)){throw 'An absent native connection was manufactured.'}
    foreach($status in @('Valid','Invalid','PrivateZone','BlockedbyPlayer')) {
        $f.Actor.CurrentStatus=[Enum]::Parse([Player+PlacementStatus],$status)
        $native=[Player+PlacementStatus]::ExtensionMissingStation
        $expectedStatus=if($allow){$f.Actor.CurrentStatus}else{$native}
        if([BuildWorksPlugin]::Station($f,$native,$actor) -ne $expectedStatus){throw "Missing-station exception erased an unrelated denial: $fault/$status"}
        if([BuildWorksPlugin]::Station($f,$f.Actor.CurrentStatus,$actor) -ne $f.Actor.CurrentStatus){throw "Station exception rewrote another native failure: $fault/$status"}
    }
    if($c.Support.m_supports -ne ($fault -ne 'nonSupporting')){throw 'Functional attachment changed native load-bearing flag.'}
}
'PASS fresh functional geometry/environment predicates: native-supporting/static targets only; non-supporting/disabled/inactive targets rejected; overlap/clearance/biome/connection scoped, donor flags unchanged. Host substituted; game/payment/reload not proved.'
foreach($role in @('functional','structural')) {
 foreach($hasContact in @($true,$false)) {
  $f=[ChestAttachmentFixture]::new();$f.Source.m_noInWater=$true
  if($role -eq 'structural'){$f.Source.m_usage=[Piece+UsageTagFlags]20}
  [BuildWorksPlacementValidation]::Contact=$hasContact;[Floating]::Volume=$null;[Floating]::Level=1;[Floating]::LiquidReads=0;[Floating]::WaterReads=0
  $water=$null;$accepted=[BuildWorksPlugin]::ContactCallback($f,[ref]$water)
  if($accepted -ne ($role -eq 'functional' -and $hasContact)){throw "Actual callback LiquidSurface-without-WaterVolume regression: $role/$hasContact"}
  if($role -eq 'functional' -and ([Floating]::LiquidReads -ne 0 -or [Floating]::WaterReads -ne 0)){throw 'Functional callback still depends on native water volume.'}
  if($hasContact -and $role -eq 'structural' -and ([Floating]::LiquidReads -ne 1 -or [Floating]::WaterReads -ne 1)){throw 'Structural underwater protection was skipped.'}
  if($f.Has() -ne ($role -eq 'functional' -and $hasContact)){throw 'Callback manufactured or lost a fresh contact stamp.'}
 }
}
$f=[ChestAttachmentFixture]::new();$f.Source.m_noInWater=$true;$f.Source.m_usage=[Piece+UsageTagFlags]20
[BuildWorksPlacementValidation]::Contact=$true;[Floating]::Volume=[WaterVolume]::new();[Floating]::Volume.Shape=[UnityEngine.Collider]::new()
$water=$null;if(![BuildWorksPlugin]::ContactCallback($f,[ref]$water) -or ![object]::ReferenceEquals($water,[Floating]::Volume.Shape)){throw 'Structural callback lost its actual water collider.'}
[Floating]::Volume=$null;[Floating]::Level=-1;$water=$null
if(![BuildWorksPlugin]::ContactCallback($f,[ref]$water) -or $null -ne $water){throw 'Dry structural callback was changed.'}
'PASS actual contact callback: functional underwater contact works without WaterVolume, no-contact refuses; structural water-volume requirement and actual collider retained. Native geometry/liquid APIs substituted, not game proof.'
