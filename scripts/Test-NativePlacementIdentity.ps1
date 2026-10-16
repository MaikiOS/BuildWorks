[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/PrecisionPlacementSession.cs'))
$start = $source.IndexOf('        internal static bool SharesNativePlacementData(')
$end = $source.IndexOf('        internal void RequireNativePlacementMaterialCreation(', $start)
if ($start -lt 0 -or $end -le $start) { throw 'Native placement identity guard not found.' }
$method = $source.Substring($start, $end - $start)
# Execute the actual source guard against inert references; no Unity/network deletion.
$fixture = @'
using System;
using System.Reflection;
namespace NativePlacementIdentityChecks {
public class Obj { public static implicit operator bool(Obj o) => o != null; }
public class ZDO { public int Id; }
public class ZNetView : Obj { public ZDO Data; public ZDO GetZDO()=>Data; }
public class GameObject : Obj { public ZNetView View; public T GetComponent<T>() where T:class => View as T; }
public class Player : Obj { public static Player m_localPlayer; public GameObject Ghost; }
public static class BlueprintMaterialState {
    public static bool SharesData(ZDO data,GameObject root) => root?.View?.Data != null &&
        (ReferenceEquals(data,root.View.Data) || data.Id==root.View.Data.Id);
}
public partial class Harness {
    private static FieldInfo PlacementGhostField=typeof(Player).GetField("Ghost");
}
public static class Cases {
    private static int count;
    private static void Check(bool condition){++count;if(!condition)throw new Exception("Identity regression case "+count);}
    public static string Run(){
        var source=new GameObject();var ghost=new GameObject();var fresh=new GameObject();
        Player.m_localPlayer=new Player{Ghost=ghost};
        Check(Harness.SharesNativePlacementData(source,source));
        Check(Harness.SharesNativePlacementData(ghost,source));
        Check(!Harness.SharesNativePlacementData(fresh,source));
        Check(!Harness.SharesNativePlacementData(null,source));
        fresh.View=new ZNetView{Data=new ZDO{Id=1}};
        source.View=new ZNetView{Data=new ZDO{Id=1}};
        Check(Harness.SharesNativePlacementData(fresh,source));
        source.View.Data.Id=2;ghost.View=new ZNetView{Data=new ZDO{Id=1}};
        Check(Harness.SharesNativePlacementData(fresh,source));
        ghost.View.Data.Id=3;Check(!Harness.SharesNativePlacementData(fresh,source));
        Player.m_localPlayer=null;Check(Harness.SharesNativePlacementData(source,source));
        return "PASS "+count+" actual source identity guard cases: literal source/ghost without ZDO, shared/distinct IDs, null/local-player absence; inert references only.";
    }
}
}
'@
$fixture += "`nnamespace NativePlacementIdentityChecks { public partial class Harness {`n"+$method+"`n} }"
Add-Type -TypeDefinition $fixture
[NativePlacementIdentityChecks.Cases]::Run()
