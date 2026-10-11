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
function Require-NativeInput($game, $utils) {
    $controller = $game.MainModule.GetType('PlayerController')
    $input = $utils.MainModule.GetType('ZInput')
    Require ($null -ne $controller -and $null -ne $input) 'Native PlayerController/ZInput moved; review the QA input adapter.'
    Require ((Calls $controller 'FixedUpdate') -match 'ZInput::GetButton\(' -and
        (Calls $controller 'LateUpdate') -match 'ZInput::GetMouseDelta\(') 'Native movement/camera no longer reads the verified ZInput path.'
    $analog = Calls $input 'SetUpAnalogHandlers'
    foreach ($binding in '<Mouse>/delta','<Mouse>/position','<Mouse>/scroll/y') {
        Require ($analog.Contains($binding)) "Native mouse binding changed: $binding"
    }
    Require ($analog -match 'InputAction::Enable\(' -and
        (Calls $input 'Internal_GetMouseDelta') -match 'InputAction::ReadValue<UnityEngine.Vector2>') 'Native camera does not read enabled InputSystem actions.'
    $keys = Calls $input 'TryGetKeyStateLowLevel'
    Require ($keys -match 'Keyboard::get_current' -and $keys -match 'Mouse::get_current') 'Native keys do not read the current InputSystem devices.'
    $subscribe = Calls $input 'SubscribeButton'
    Require ($subscribe -match 'InputAction::Enable' -and $subscribe -match 'InputAction::add_performed' -and
        $subscribe -match 'InputAction::add_canceled') 'Native button action press/release subscriptions changed.'
    Require ((Calls $input 'OnActionPerformed') -match 'ButtonDef::Press' -and
        (Calls $input 'OnActionCanceled') -match 'ButtonDef::Release') 'Native actions do not reach button press/release state.'
}
try {
    # Inspect the real host assemblies, not Workbench stubs or our own input aliases.
    [xml] $project = Get-Content -LiteralPath (Join-Path $root 'src/BuildWorks/BuildWorks.csproj') -Raw
    $managedNode = $project.SelectSingleNode('/Project/PropertyGroup/ValheimManagedDir')
    $managed = if ($managedNode) { $managedNode.InnerText } else { '' }
    Require (-not [string]::IsNullOrWhiteSpace($managed)) 'ValheimManagedDir is absent from the current project.'
    $game = $utils = $null
    try {
        $game = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'assembly_valheim.dll'))
        $utils = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'assembly_utils.dll'))
        Require-NativeInput $game $utils
        $binding = @($utils.MainModule.GetType('ZInput').Methods | Where-Object Name -eq 'SetUpAnalogHandlers')[0].Body.Instructions |
            Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldstr -and $_.Operand -ceq '<Mouse>/delta' } |
            Select-Object -First 1
        Require ($null -ne $binding) 'Native input contract self-check could not locate the camera binding.'
        $original = $binding.Operand
        try {
            $binding.Operand = '<Mouse>/qa_missing_delta' # In-memory negative control; never write the game assembly.
            $rejected = $false
            try { Require-NativeInput $game $utils }
            catch { if ($_.Exception.Message -cne 'Native mouse binding changed: <Mouse>/delta') { throw }; $rejected = $true }
            Require $rejected 'Native input contract accepted a disconnected camera binding.'
        } finally { $binding.Operand = $original }
        Require-NativeInput $game $utils
        'PASS native host input contract: PlayerController movement/camera -> ZInput -> enabled InputSystem mouse/actions/current devices; broken-binding negative control rejected. Static evidence only.'
    } finally { if ($game) { $game.Dispose() }; if ($utils) { $utils.Dispose() } }
    foreach ($name in 'BuildWorksQaControl','BuildWorksQaControlProtocol','BuildWorksQaInput','BuildWorksQaUiInput','BuildWorksQaUiCancellation','BuildWorksQaFilePublisher') {
        Require ($null -eq $normal.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type leaked into normal build: $name"
        Require ($null -ne $qa.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type absent: $name"
    }
    Require (@($normal.MainModule.AssemblyReferences | Where-Object Name -eq 'Unity.InputSystem').Count -eq 0) 'QA InputSystem dependency leaked into normal build.'
    $control = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    $inputType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaInput')
    $publisher = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaFilePublisher')
    Require ($null -ne $publisher -and @($publisher.Fields | Where-Object Name -eq 'ReplaceForTest').Count -eq 0) 'Publisher fault injection leaked into the installed QA build.'
    Require ((Calls $control 'Publish') -match 'BuildWorksQaFilePublisher::ReplaceObservation') 'QA response publication bypasses the bounded retry helper.'
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
