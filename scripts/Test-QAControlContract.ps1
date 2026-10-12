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
function Require-LoaderVersion($assembly) {
    $pluginType = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin')
    $identity = $pluginType.CustomAttributes | Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' }
    Require ($null -ne $identity -and [string]$identity.ConstructorArguments[2].Value -eq
        $assembly.Name.Version.ToString(3)) 'Loader/QA version differs from assembly.'
}
function Calls($type, [string] $name) {
    $methods = @($type.Methods | Where-Object Name -eq $name)
    Require ($methods.Count -eq 1 -and $methods[0].HasBody) "Missing QA method: $name"
    ($methods[0].Body.Instructions | ForEach-Object { [string] $_.Operand }) -join "`n"
}
function Require-SelectionAbort($sessionType) {
    $abort = Calls $sessionType 'AbortQaInput'
    $cancel = Calls $sessionType 'CancelBlueprintSelectionDrag'
    Require ($abort -match 'CancelBlueprintSelectionDrag' -and $abort -match 'blueprintWorkspaceRightTracking' -and
        $abort -match 'HideSelectionBox' -and $cancel -match 'blueprintSelectionDragging' -and
        $cancel -match 'blueprintVolumeDragValid' -and $cancel -match 'HideSelectionBox') 'Abort leaves world volume-selection gestures.'
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
    Require-LoaderVersion $normal
    Require-LoaderVersion $qa
    $versionAttribute = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin').CustomAttributes |
        Where-Object { $_.AttributeType.Name -eq 'BepInPlugin' }
    $originalVersion = $versionAttribute.ConstructorArguments[2]
    try {
        $versionAttribute.ConstructorArguments[2] = [Mono.Cecil.CustomAttributeArgument]::new(
            $originalVersion.Type, '0.19.160')
        $rejected = $false
        try { Require-LoaderVersion $qa }
        catch { if ($_.Exception.Message -cne 'Loader/QA version differs from assembly.') { throw }; $rejected = $true }
        Require $rejected 'Loader-version regression accepted the stale .160 attribute.'
    } finally { $versionAttribute.ConstructorArguments[2] = $originalVersion }
    Require-LoaderVersion $qa
    'PASS loader/QA version matches both assemblies; stale .160 in-memory negative control rejected.'
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
    foreach ($name in 'BuildWorksQaControl','BuildWorksQaControlProtocol','BuildWorksQaInput','BuildWorksQaUiInput','BuildWorksQaUiCancellation','BuildWorksQaFilePublisher','BuildWorksTestPlacementDiagnostics') {
        Require ($null -eq $normal.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type leaked into normal build: $name"
        Require ($null -ne $qa.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type absent: $name"
    }
    Require (@($normal.MainModule.AssemblyReferences | Where-Object Name -eq 'Unity.InputSystem').Count -eq 0) 'QA InputSystem dependency leaked into normal build.'
    $control = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    Require ((Calls $control 'AfterInput') -match 'BuildWorksQaControl::SubmitFocusedText') 'QA Return does not reach the text-submit bridge.'
    Require ((Calls $control 'SubmitFocusedText') -match 'ExecuteEvents::Execute' -and
        (Calls $control 'SubmitFocusedText') -match 'get_sendNavigationEvents') 'QA text submit bypasses native UI or its duplicate-submit guard.'
    $inputType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaInput')
    $publisher = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaFilePublisher')
    $observations = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestPlacementDiagnostics')
    $observationReply = Calls $control 'Respond'
    foreach ($name in 'DescribeLastSnap','DescribeLastContact','DescribeInventory') {
        Require ($observationReply -match "BuildWorksTestPlacementDiagnostics::$name") "QA reply is missing observation: $name"
    }
    Require ((Calls $control 'ResetSession') -match 'BuildWorksTestPlacementDiagnostics::Reset') 'QA observations survive a session reset.'
    $qaPlugin = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin')
    $snapTrace = Calls $qaPlugin 'ApplyPassiveAutoJoinFromNativeSnap'
    Require ($snapTrace -match 'BuildWorksTestPlacementDiagnostics::BeginSnap' -and
        $snapTrace -match 'BuildWorksTestPlacementDiagnostics::EndSnap' -and
        $snapTrace -match 'PrecisionPlacementSession::ApplyPassiveAutoJoin') 'QA snap observation bypasses the real session decision.'
    Require ((Calls $qaPlugin 'ApplyBlueprintValidationContact') -match 'BuildWorksTestPlacementDiagnostics::RecordContact') 'QA contact observation is disconnected.'
    Require ($null -ne $publisher -and @($publisher.Fields | Where-Object Name -eq 'ReplaceForTest').Count -eq 0) 'Publisher fault injection leaked into the installed QA build.'
    Require ((Calls $control 'Publish') -match 'BuildWorksQaFilePublisher::ReplaceObservation') 'QA response publication bypasses the bounded retry helper.'
    Require ((Calls $control 'Initialize') -match 'BuildWorksTestIsolation::get_ControlDirectory') 'Bridge directory is not rooted in QA isolation.'
    Require ((Calls $control 'Update') -match 'BuildWorksTestIsolation::RequireLoadedSession' -and
        (Calls $control 'Update') -match 'BuildWorksQaControlProtocol::Read') 'Input bypasses session or protocol validation.'
    foreach ($name in 'OnApplicationFocus','OnApplicationPause','OnDisable','OnDestroy') {
        Require ((Calls $control $name) -match 'BuildWorksQaControl::Stop') "Missing release lifecycle: $name"
    }
    Require ((Calls $control 'Stop') -match 'BuildWorksQaInput::Release') 'Stop does not release input.'
    $revisionStores = @($control.Methods | Where-Object Name -eq 'Stop' | ForEach-Object { $_.Body.Instructions } |
        Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Stfld -and $_.Operand.Name -eq 'stopRevision' })
    Require ($revisionStores.Count -eq 1) 'Idle emergency stop does not invalidate local model decisions.'
    Require ($observationReply -match 'BuildWorksQaControl::stopRevision' -and
        $observationReply -match 'InventoryGui::IsVisible') 'Local agent lacks authoritative stop/inventory observations.'
    Require ((Calls $control 'Stop') -match 'BuildWorksPlugin::AbortQaControlInput') 'Emergency stop leaves active editor/world gestures.'
    Require ((Calls $control 'WithinDeadline') -match 'BuildWorksQaControlProtocol::CanContinue') 'UTC expiry/elapsed deadline is not revalidated.'
    Require ((Calls $control 'AfterInput') -match 'BuildWorksTestIsolation::RequireLoadedSession' -and
        (Calls $control 'AfterInput') -match 'get_PhysicalIntervention' -and
        (Calls $control 'AfterInput') -match 'get_realtimeSinceStartup') 'Per-step safety/time/physical-intervention gate absent.'
    Require ((Calls $inputType 'Release') -match 'InputSystem::RemoveDevice' -and
        (Calls $inputType 'Release') -match 'MakeCurrent' -and
        (Calls $inputType 'Release') -match 'set_inputOverride') 'Test devices or UI override are not restored.'
    Require ((Calls $inputType 'CancelUiPointers') -match 'BuildWorksQaUiCancellation::Cancel') 'Abort leaves native UI pointer gestures.'
    $sessionType = $qa.MainModule.GetType('OstrixMods.BuildWorks.PrecisionPlacementSession')
    Require-SelectionAbort $sessionType
    $cancelCall = @($sessionType.Methods | Where-Object Name -eq 'AbortQaInput')[0].Body.Instructions |
        Where-Object { [string] $_.Operand -match '::CancelBlueprintSelectionDrag\(' } | Select-Object -First 1
    Require ($null -ne $cancelCall) 'Selection abort negative control cannot locate the cancel call.'
    $originalCode = $cancelCall.OpCode; $originalOperand = $cancelCall.Operand
    try {
        $cancelCall.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $cancelCall.Operand = $null
        $rejected = $false
        try { Require-SelectionAbort $sessionType }
        catch { if ($_.Exception.Message -cne 'Abort leaves world volume-selection gestures.') { throw }; $rejected = $true }
        Require $rejected 'Selection abort contract accepted a disconnected cancellation path.'
    } finally { $cancelCall.OpCode = $originalCode; $cancelCall.Operand = $originalOperand }
    Require-SelectionAbort $sessionType
    'PASS selection abort: real compiled helper chain; disconnected cancel call rejected in memory only.'
    $qaMethods = @($control.Methods) + @($inputType.Methods) + @($observations.Methods)
    Require (@($qaMethods | Where-Object IsPInvokeImpl).Count -eq 0) 'QA bridge gained OS-level control.'
    $allCalls = ($qaMethods | Where-Object HasBody | ForEach-Object {
        $_.Body.Instructions | ForEach-Object { [string] $_.Operand }
    }) -join "`n"
    Require ($allCalls -notmatch 'Player::TeleportTo|Inventory::AddItem|Inventory::RemoveItem|Inventory::MoveItem|ItemDrop::DropItem|ConsumeResources|ZDO::Set|TryPlacePiece|HttpListener|System.Net.Sockets|Process::Start') 'QA bridge gained a world/cost bypass, launcher or network service.'
    # Worst-case JSON escaping is six ASCII bytes per observed name character.
    $maximumInventoryBytes = 64 * ((32 + 3) * 6 + 64)
    Require ($maximumInventoryBytes + 8192 -lt 32768) 'Bounded observation budget exceeds the mailbox reader limit.'
    $bridgeOwn = @($qa.MainModule.Types | Where-Object { $_.FullName -notmatch 'BuildWorksQa' } | ForEach-Object { $_.Methods } |
        Where-Object HasBody | ForEach-Object { $_.Body.Instructions } | Where-Object { [string] $_.Operand -match 'BuildWorksQaInput::' })
    Require ($bridgeOwn.Count -gt 20) 'BuildWorks legacy input callsites are not connected to QA input.'
    'PASS QA control compiled contract: QA-only types/reference, isolated path, native input-only boundary, lifecycle release, physical intervention, temporary devices/UGUI and legacy callsites. Static evidence; not an executed game session.'
} finally { $normal.Dispose(); $qa.Dispose() }
