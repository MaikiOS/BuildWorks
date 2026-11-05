$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot '../src/BuildWorks/BlueprintItemStandPlacement.cs'
$source = [IO.File]::ReadAllText($path)
$blocks = [regex]::Matches($source, 'expectedPackage = SnapshotItem\(item\);(?<tail>.*?)int wanted =', 'Singleline')
$comparisons = [regex]::Matches($source, '(?:captured|held)\.Zdo == (?<name>\w+)')
if ($blocks.Count -ne 1 -or $comparisons.Count -ne 3 -or
    -not $source.Contains('SnapshotItem(item) == expectedPackage')) { throw 'Snapshot role source boundaries changed.' }
$roles = @($comparisons | ForEach-Object { $_.Groups['name'].Value } | Select-Object -Unique)
if ($roles.Count -ne 1) { throw 'Completion/catch/rollback snapshot roles differ.' }
$block = 'expectedPackage = SnapshotItem(item);' + $blocks[0].Groups['tail'].Value
$role = $roles[0]
# Execute the actual source block and selected comparison role; native serialization is substituted.
$test = @"
using System;
namespace BuildWorksSnapshotRoleChecks {
public sealed class ItemData {
    public bool m_equipped;
    public int m_stack=7, gridX=9, gridY=3, quality=4;
    public string custom=""owner-data"";
    public ItemData Clone() { return (ItemData)MemberwiseClone(); }
}
public static class ItemStandPackageRoles {
    static string SnapshotItem(ItemData item) {
        return ""1|""+item.m_equipped+""|""+item.gridX+""|""+item.gridY+""|""+item.quality+""|""+item.custom;
    }
    public static void Run() {
        foreach(bool equipped in new[]{false,true}) {
            var item=new ItemData{m_equipped=equipped};
            string expectedPackage=null, expectedMountedPackage=null;
            $block
            _ = expectedMountedPackage;
            if(SnapshotItem(item)!=expectedPackage || item.m_equipped!=equipped || item.m_stack!=7)
                throw new Exception(""Original reservation snapshot or inventory object changed"");
            var nativeMounted=item.Clone(); nativeMounted.m_equipped=false;
            if(SnapshotItem(nativeMounted)!=$role)
                throw new Exception(""Mounted expectation differs from new native equipped=false record"");
            item.m_equipped=!equipped;
            if(SnapshotItem(item)==expectedPackage)
                throw new Exception(""Preflight snapshot no longer detects equipment changes"");
        }
    }
}
}
"@
$test = $test.Replace('""', '"')
Add-Type -TypeDefinition $test -WarningAction SilentlyContinue
[BuildWorksSnapshotRoleChecks.ItemStandPackageRoles]::Run()
Write-Output 'PASS actual TryApply snapshot block/three comparison roles: equipped and unequipped; original state and nonzero grid/quality/custom retained. Native serialization/placement substituted, no game proof.'
