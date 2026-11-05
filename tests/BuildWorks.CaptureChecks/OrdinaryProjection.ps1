param(
    [string]$SourcePath = (Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintStorageCapture.cs'),
    [string]$BuiltAssembly = ''
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '../..')).Path
$managed = 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed'
Add-Type -Path (Join-Path $project '../BepInEx/core/Mono.Cecil.dll')
$meshModule = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'assembly_simplemeshcombine.dll'))
try {
    $meshType = $meshModule.MainModule.GetType('SimpleMeshCombine')
    if (!$meshType -or $meshType.BaseType.FullName -ne 'UnityEngine.MonoBehaviour' -or $meshType.Interfaces.Count -ne 0) { throw 'Native mesh component contract changed.' }
    $methods = @('.ctor','EnableRenderers','FindEnabledMeshes','CombineMeshes','Contains')
    if (@($meshType.Methods | Where-Object { $_.Name -notin $methods }).Count) { throw 'Unknown native mesh callback/method.' }
    $fieldTypes = @('UnityEngine.GameObject[]','UnityEngine.GameObject','UnityEngine.Mesh','System.String','System.Boolean','System.Int32','System.Single')
    if (@($meshType.Fields | Where-Object { $_.FieldType.FullName -notin $fieldTypes }).Count) { throw 'Unknown native mesh state.' }
} finally { $meshModule.Dispose() }

$source = [IO.File]::ReadAllText((Resolve-Path -LiteralPath $SourcePath))
$start = $source.IndexOf('        internal static bool? SupportsKgStateProjection(')
$end = $source.IndexOf('        // Only construction compatibility;', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Actual ordinary projection methods not found.' }
$methods = $source.Substring($start, $end - $start).Replace('internal static', 'public static')
$code = @'
using System; using System.Linq; using UnityEngine;
namespace UnityEngine {
 public class MonoBehaviour { public static implicit operator bool(MonoBehaviour value) { return value != null; } }
 public class GameObject {
  readonly bool piece; readonly MonoBehaviour[] components;
  public GameObject(bool piece, params MonoBehaviour[] components) { this.piece=piece; this.components=components; }
  public T GetComponent<T>() where T:class { return piece ? new Piece() as T : null; }
  public T[] GetComponentsInChildren<T>(bool inactive) where T:class { if(!inactive)throw new Exception("Inactive children skipped");return components.Cast<T>().ToArray(); }
  public static implicit operator bool(GameObject value) { return value!=null; }
 }
}
public class Piece:MonoBehaviour {} public class ZNetView:MonoBehaviour {} public class WearNTear:MonoBehaviour {}
public class Sign:MonoBehaviour {} public class RandomMaterialValues:MonoBehaviour {} public class SimpleMeshCombine:MonoBehaviour {}
public class Container:MonoBehaviour {} public class ItemStand:MonoBehaviour {} public class ArmorStand:MonoBehaviour {}
public class UnknownScript:MonoBehaviour {} public class DerivedMesh:SimpleMeshCombine {}
// This isolated predicate fixture has no caches; production Append/root-cache integration is in MidpointChecks.
public static class VanillaMidpointSnapPoints { public static bool IsRootCache(MonoBehaviour component,GameObject root)=>false; }
namespace TMPro { public class TextMeshPro:MonoBehaviour {} public class TextMeshProUGUI:MonoBehaviour {} }
namespace UnityEngine.UI { public class CanvasScaler:MonoBehaviour {} }
public static class OrdinaryProjectionChecks {
/*SOURCE*/
 static int count;
 static void Check(bool value,string why) { count++;if(!value)throw new Exception(why); }
 public static int Run() {
  Check(SupportsKgStateProjection(new GameObject(true,new Piece(),new WearNTear(),new ZNetView()))==true,"Bare ordinary floor rejected");
  Check(SupportsKgStateProjection(new GameObject(true,new Piece(),new WearNTear(),new ZNetView(),new SimpleMeshCombine()))==true,"Ordinary floor mesh component rejected");
  Check(SupportsKgStateProjection(null)==null,"Missing prefab admitted");
  Check(SupportsKgStateProjection(new GameObject(false,new SimpleMeshCombine()))==false,"Mesh-only object promoted to piece");
  Check(SupportsKgStateProjection(new GameObject(true,new Piece(),null))==false,"Missing child script admitted");
  foreach(var component in new MonoBehaviour[]{new Container(),new ItemStand(),new ArmorStand(),new UnknownScript(),new DerivedMesh()})
   Check(SupportsKgStateProjection(new GameObject(true,new Piece(),component))==false,"Special/unknown/derived child admitted: "+component.GetType().Name);
  foreach(var type in new[]{typeof(Piece),typeof(ZNetView),typeof(WearNTear),typeof(Sign),typeof(RandomMaterialValues),typeof(SimpleMeshCombine),typeof(TMPro.TextMeshPro),typeof(TMPro.TextMeshProUGUI),typeof(UnityEngine.UI.CanvasScaler)})
   Check(KgOrdinaryComponent(type),"Existing or native mesh exact type rejected: "+type.Name);
  var fake=System.Reflection.Emit.AssemblyBuilder.DefineDynamicAssembly(new System.Reflection.AssemblyName("OtherAssembly"),System.Reflection.Emit.AssemblyBuilderAccess.Run)
   .DefineDynamicModule("Other").DefineType("SimpleMeshCombine",System.Reflection.TypeAttributes.Public,typeof(MonoBehaviour)).CreateTypeInfo().AsType();
  Check(fake.FullName==typeof(SimpleMeshCombine).FullName&&!KgOrdinaryComponent(fake),"Same-name foreign type admitted");
  return count;
 }
}
'@
Add-Type -TypeDefinition $code.Replace('/*SOURCE*/', $methods)
$checks = [OrdinaryProjectionChecks]::Run()
if ($checks -ne 20) { throw "Unexpected ordinary projection coverage: $checks" }
if ($BuiltAssembly) {
    $assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Resolve-Path -LiteralPath $BuiltAssembly).Path)
    try {
        $predicate = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BlueprintStorageCapture').Methods | Where-Object Name -eq 'KgOrdinaryComponent'
        $tokens = @($predicate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldtoken' -and $_.Operand.FullName -eq 'SimpleMeshCombine' })
        if ($tokens.Count -ne 1 -or $tokens[0].Operand.Scope.Name -ne 'assembly_simplemeshcombine') { throw 'Compiled predicate is not bound to the exact native mesh type.' }
    } finally { $assembly.Dispose() }
}
"PASS $checks actual source predicate cases; native mesh module metadata inspected; compiled binding checked=$([bool]$BuiltAssembly). Unity/GameObject host calls substituted, no target game method executed."
