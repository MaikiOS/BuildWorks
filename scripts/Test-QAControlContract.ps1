[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profile = Split-Path $root
Add-Type -Path (Join-Path $profile 'BepInEx/core/Mono.Cecil.dll')
$normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'artifacts/bin/BuildWorks.dll'))
$qa = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'artifacts/test-stand-bin/BuildWorks.dll'))
function Require([bool] $condition, [string] $message) { if (-not $condition) { throw $message } }
function Calls($type, [string] $name) {
    $methods = @($type.Methods | Where-Object Name -eq $name)
    Require ($methods.Count -eq 1 -and $methods[0].HasBody) "Missing QA method: $name"
    ($methods[0].Body.Instructions | ForEach-Object { [string] $_.Operand }) -join "`n"
}
try {
    foreach ($name in 'BuildWorksQaControl','BuildWorksQaControlProtocol','BuildWorksQaInput','BuildWorksQaUiInput','BuildWorksQaUiCancellation') {
        Require ($null -eq $normal.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type leaked into normal build: $name"
        Require ($null -ne $qa.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type absent: $name"
    }
    Require (@($normal.MainModule.AssemblyReferences | Where-Object Name -eq 'Unity.InputSystem').Count -eq 0) 'QA InputSystem dependency leaked into normal build.'
    $control = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    $inputType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaInput')
    Require ((Calls $control 'Initialize') -match 'BuildWorksTestIsolation::get_ControlDirectory') 'Bridge directory is not rooted in QA isolation.'
    Require ((Calls $control 'Update') -match 'BuildWorksTestIsolation::RequireLoadedSession' -and
        (Calls $control 'Update') -match 'BuildWorksQaControlProtocol::Read') 'Input bypasses session or protocol validation.'
    foreach ($name in 'OnApplicationFocus','OnApplicationPause','OnDisable','OnDestroy') {
        Require ((Calls $control $name) -match 'BuildWorksQaControl::Stop') "Missing release lifecycle: $name"
    }
    Require ((Calls $control 'Stop') -match 'BuildWorksQaInput::Release') 'Stop does not release input.'
    Require ((Calls $control 'Stop') -match 'BuildWorksPlugin::AbortQaControlInput') 'Emergency stop leaves active editor/world gestures.'
    Require ((Calls $control 'WithinDeadline') -match 'BuildWorksQaControlProtocol::CanContinue') 'UTC expiry/elapsed deadline is not revalidated.'
    Require ((Calls $control 'AfterInput') -match 'BuildWorksTestIsolation::RequireLoadedSession' -and
        (Calls $control 'AfterInput') -match 'get_PhysicalIntervention' -and
        (Calls $control 'AfterInput') -match 'get_realtimeSinceStartup') 'Per-step safety/time/physical-intervention gate absent.'
    Require ((Calls $inputType 'Release') -match 'InputSystem::RemoveDevice' -and
        (Calls $inputType 'Release') -match 'MakeCurrent' -and
        (Calls $inputType 'Release') -match 'set_inputOverride') 'Test devices or UI override are not restored.'
    Require ((Calls $inputType 'CancelUiPointers') -match 'BuildWorksQaUiCancellation::Cancel') 'Abort leaves native UI pointer gestures.'
    $abort = Calls ($qa.MainModule.GetType('OstrixMods.BuildWorks.PrecisionPlacementSession')) 'AbortQaInput'
    Require ($abort -match 'blueprintSelectionDragging' -and $abort -match 'blueprintWorkspaceRightTracking' -and
        $abort -match 'HideSelectionBox') 'Abort leaves world volume-selection gestures.'
    $qaMethods = @($control.Methods) + @($inputType.Methods)
    Require (@($qaMethods | Where-Object IsPInvokeImpl).Count -eq 0) 'QA bridge gained OS-level control.'
    $allCalls = ($qaMethods | Where-Object HasBody | ForEach-Object {
        $_.Body.Instructions | ForEach-Object { [string] $_.Operand }
    }) -join "`n"
    Require ($allCalls -notmatch 'Player::TeleportTo|Inventory::AddItem|ConsumeResources|ZDO::Set|TryPlacePiece|HttpListener|System.Net.Sockets|Process::Start') 'QA bridge gained a world/cost bypass, launcher or network service.'
    $bridgeOwn = @($qa.MainModule.Types | Where-Object { $_.FullName -notmatch 'BuildWorksQa' } | ForEach-Object { $_.Methods } |
        Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object { [string] $_.Operand -match 'BuildWorksQaInput::' })
    Require ($bridgeOwn.Count -gt 20) 'BuildWorks legacy input callsites are not connected to QA input.'
    'PASS QA control compiled contract: QA-only types/reference, isolated path, native input-only boundary, lifecycle release, physical intervention, temporary devices/UGUI and legacy callsites. Static evidence; not an executed game session.'
} finally { $normal.Dispose(); $qa.Dispose() }
