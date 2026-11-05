param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = (Resolve-Path -LiteralPath $BuildWorksRoot).Path
$source = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/PrecisionPlacementSession.cs'))
$plugin = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BuildWorksPlugin.cs'))
$guard = [regex]::Match($source, '(?s)        internal bool ShouldBlockMenuEscape\(\)\s*\{.*?\n        \}')
$escapeStart = $source.IndexOf('if (Input.GetKeyDown(KeyCode.Escape))', [StringComparison]::Ordinal)
$cancel = [regex]::Match($source.Substring($escapeStart), '(?s)if \(sessionMode == SessionMode.BlueprintWorldSelection\)\s*\{(?<body>.*?)\n                    \}')
$prefix = [regex]::Match($plugin, 'private static bool Prefix\(\) => instance\?\.session\?\.ShouldBlockMenuEscape\(\) != true;')
if (!$guard.Success -or !$cancel.Success -or !$prefix.Success) { throw 'Production Menu Escape guard/cancel/prefix missing or changed.' }
if ($plugin -notmatch '\[HarmonyPatch\(typeof\(Menu\), "Update"\)\]\s*private static class WorldSelectionMenuEscapePatch') { throw 'Menu.Update patch target missing.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'MenuEscapeHarness.cs'))
foreach ($qa in @($false, $true)) {
    $suffix = if ($qa) { 'Qa' } else { 'Normal' }
    $definition = $harness.Replace('// GUARD', $guard.Value).Replace('// CANCEL_BODY', $cancel.Groups['body'].Value).Replace('// PREFIX', $prefix.Value)
    $definition = $definition.Replace('MenuEscapeChecks', "MenuEscapeChecks$suffix").Replace('NormalInput', "NormalInput$suffix").Replace('QaInput', "QaInput$suffix").Replace('KeyCode', "KeyCode$suffix").Replace('Time', "Time$suffix").Replace('BuildWorksLocalization', "BuildWorksLocalization$suffix")
    if ($qa) { $definition = "#define BUILDWORKS_TEST_STAND`n" + $definition }
    Add-Type -TypeDefinition $definition
    $type = "MenuEscapeChecks$suffix" -as [type]
    $type::Run()
}
