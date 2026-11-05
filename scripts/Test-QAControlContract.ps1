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
function Require-StorageLookup($assembly) {
    $control = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    $storage = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestStorage')
    $entry = @($control.Methods | Where-Object Name -eq 'LookupStorageMarker')
    Require ($entry.Count -eq 1 -and $entry[0].IsPublic -and $entry[0].ReturnType.FullName -eq 'System.String' -and
        $entry[0].Parameters.Count -eq 2 -and @($entry[0].Parameters | Where-Object { $_.ParameterType.FullName -ne 'System.String' }).Count -eq 0) 'Storage lookup scalar API moved.'
    $pending = [Collections.Generic.Queue[Mono.Cecil.MethodDefinition]]::new()
    $pending.Enqueue($entry[0]); $seen = [Collections.Generic.HashSet[string]]::new(); $calls = [Collections.Generic.List[string]]::new()
    while ($pending.Count -gt 0) {
        $method = $pending.Dequeue()
        if (-not $seen.Add($method.FullName) -or -not $method.HasBody) { continue }
        Require (-not $method.IsPInvokeImpl) 'Storage lookup gained OS control.'
        foreach ($instruction in $method.Body.Instructions) {
            if ($instruction.Operand -isnot [Mono.Cecil.MethodReference]) { continue }
            $reference = $instruction.Operand; $calls.Add($reference.FullName)
            if ($reference.DeclaringType.FullName -eq 'System.IO.BinaryWriter') {
                $memoryEncode = $method.DeclaringType.FullName -eq 'OstrixMods.BuildWorks.BlueprintStorageTransferJournal' -and $method.Name -eq 'Encode'
                $fingerprint = $method.DeclaringType.FullName -like 'OstrixMods.BuildWorks.BlueprintStorageTransferJournal/*' -and $method.Name -eq '<PersistenceFingerprint>b__0'
                Require ($memoryEncode -or ($fingerprint -and $reference.Name -eq 'Write')) ('Lookup BinaryWriter escaped the existing memory-only fingerprint encoder: ' + $method.FullName)
                if ($memoryEncode) {
                    $encode = Calls $method.DeclaringType 'Encode'
                    Require ($encode -match 'MemoryStream::.ctor\(\)' -and $encode -match 'MemoryStream::ToArray' -and $encode -notmatch 'FileStream|System.IO.File::') 'Lookup fingerprint encoder gained file access.'
                }
            }
            if ($reference.DeclaringType.FullName -eq 'System.IO.FileStream' -and $reference.Name -eq '.ctor') {
                Require ($reference.Parameters.Count -eq 4 -and $instruction.Previous.Previous.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_1 -and
                    $instruction.Previous.Previous.Previous.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_3) 'Storage lookup file stream is not existing-file/read-only.'
                if ($method.DeclaringType.FullName -eq 'OstrixMods.BuildWorks.BuildWorksTestStorage') {
                    Require ($instruction.Previous.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_1) 'Storage lookup did not lock out concurrent writers.'
                }
            }
            if ($reference.DeclaringType.Scope.Name -eq $assembly.MainModule.Name) { $pending.Enqueue($reference.Resolve()) }
        }
    }
    $text = $calls -join "`n"
    foreach ($required in 'GetAllZDOsWithPrefabIterative','ZDOMan::GetZDO','ZNetScene::FindInstance','RequireLoadedSession','get_QaReadonlyIdle','BlueprintStorageTransferJournal::Read','SHA256::Create','JsonUtility::ToJson') {
        Require ($text.Contains($required)) "Storage lookup lost its required read/guard: $required"
    }
    $effects = @($calls | Where-Object { $_ -match 'Inventory::(?:Add|Remove|Move)|ZDO::Set|ZDOMan::(?:Add|Create|Destroy)|ZNetScene::(?:Create|Destroy)|::(?:SetOwner|SaveWorld|SavePlayerProfile|SavePlayerToDisk|BeginOnce|RecordSettlement|RecordPersistence|TrySettle)|System.IO.File::(?:Write|Append|Delete|Move|Replace|Create)|FileStream::Write|System.IO.Directory::(?:Create|Delete|Move)|Application::Quit|GameObject::AddComponent' })
    Require ($effects.Count -eq 0) ('Readonly storage lookup gained effects: ' + ($effects -join '; '))
    Require ((Calls $control 'Initialize') -match 'ManagedThreadId' -and (Calls $control 'Initialize') -match 'ownerThread') 'Storage lookup lost its initialized owner thread.'
    $entryCalls = Calls $control 'LookupStorageMarker'
    Require ($entryCalls -match 'BuildWorksTestStorage::LookupStorageMarker' -and $entryCalls -notmatch 'StartStorage|::Begin\(') 'Storage lookup starts a scenario.'
    $idle = @($control.Methods | Where-Object Name -eq 'RequireStorageIdle')
    Require ($idle.Count -eq 1) 'Storage lookup idle guard moved.'
    $idleCalls = Calls $control $idle[0].Name
    foreach ($guard in 'ownerThread','ManagedThreadId','running','capture','fog','StoragePersistenceBusy','Application::get_isFocused','Input::GetKey','File::Exists','stop.flag','RequireQaStorageIdle','get_QaReadonlyIdle') {
        Require ($idleCalls.Contains($guard)) "Storage lookup lost admission guard: $guard"
    }
    Require ((Calls $storage 'ScanStorageRegistry') -match 'GetAllZDOsWithPrefabIterative') 'Storage lookup registry traversal disconnected.'
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
function Require-ContentsProject($assembly) {
    $control = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    $helper = $assembly.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestConstructionProject')
    foreach ($name in 'CreatePaidContentsProject','AdoptPaidContentsProject') {
        $entry = @($control.Methods | Where-Object Name -eq $name)
        Require ($entry.Count -eq 1 -and $entry[0].IsPublic -and $entry[0].Parameters.Count -eq 0 -and $entry[0].ReturnType.FullName -eq 'System.String') 'Contents project QA entrypoint moved.'
        $body = Calls $control $name
        Require ($body -match 'RequireStorageIdle' -and $body.Contains('BuildWorksTestConstructionProject::' + $name)) 'Contents project lost its guarded caller.'
    }
    $create = Calls $helper 'CreatePaidContentsProject'
    foreach ($required in 'Action::Invoke','RequireLoadedSession','RequireStoragePreflight','ContentsProjectId','BlueprintConstructionProjectRuntime::Read','ReadContentsBlueprint','HaveRequirements','SelectSupplies','SuppliesAvailable','FindStorageSite','PrivateArea::CheckAccess','HaveBuildStationInRange','TrySnapshotInventory','BlueprintConstructionProjectRuntime::Create','RememberContentsProject') {
        Require ($create.Contains($required)) "Contents project lost preflight/write: $required"
    }
    $method = @($helper.Methods | Where-Object Name -eq 'CreatePaidContentsProject')[0]
    $calls = @($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
    $selection = @($calls | Where-Object { [string]$_.Operand -match 'BlueprintItemStandPlacement::SelectSupplies\(' })
    Require ($selection.Count -eq 1 -and $selection[0].Operand.Parameters.Count -eq 4 -and
        $selection[0].Previous.OpCode.Name -match '^ldloc') 'Contents project lost its explicit native payment budget.'
    Require ([string]$calls[0].Operand -match 'Action::Invoke' -and @($calls | Where-Object { [string]$_.Operand -match 'Action::Invoke' }).Count -eq 2) 'Contents project lost early/final idle guards.'
    $read = @($calls | Where-Object { [string]$_.Operand -match 'BlueprintConstructionProjectRuntime::Read\(' })[0]
    $write = @($calls | Where-Object { [string]$_.Operand -match 'BlueprintConstructionProjectRuntime::Create\(' })[0]
    Require ($read.Offset -lt $write.Offset -and $create -match 'adopt it instead') 'Contents project lost one-shot prior-plan refusal.'
    $identity = Calls $helper 'ContentsProjectId'
    Require ($identity -match 'SupplyStamp' -and $identity -match 'GetWorldUID' -and $identity -match 'paid-contents-project-v1' -and $identity -notmatch 'Guid::NewGuid') 'Contents project identity is not stable per owned QA world.'
    $fixture = Calls $helper 'ReadContentsBlueprint'
    Require ($fixture -match 'ReviewImportFile' -and $fixture -match 'SHA256::Create' -and $fixture -match 'ComputeHash' -and $fixture -match '25-contents-populated.buildworks.json\|49809C3F') 'Contents project lost pinned fixture validation.'
    $adopt = Calls $helper 'AdoptPaidContentsProject'
    Require ($adopt -match 'RequireLoadedSession' -and $adopt -match 'ContentsProjectId' -and $adopt -match 'SequenceEqual' -and $adopt -notmatch 'BlueprintConstructionProjectRuntime::Create') 'Contents adoption does not bind the existing exact plan.'
    $remember = Calls $helper 'RememberContentsProject'
    Require ($remember -match 'cancelOperation' -and $remember -match 'Guid::NewGuid') 'Contents project did not initialize the existing unpaid cancellation operation.'
    $owned = @($helper.Methods | Where-Object { $_.Name -in @('CreatePaidContentsProject','AdoptPaidContentsProject','ContentsProjectId','ReadContentsBlueprint','ContentsBlueprint','RememberContentsProject') })
    $owned += @($helper.NestedTypes | ForEach-Object { $_.Methods } | Where-Object { $_.Name -match '<(?:CreatePaidContentsProject|ReadContentsBlueprint|ContentsBlueprint|RememberContentsProject)>' })
    $effects = ($owned | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] } | ForEach-Object { [string]$_.Operand } }) -join "`n"
    Require ($effects -notmatch 'Inventory::(?:Add|Remove|Move)|Player::PlacePiece|ConsumeResources|ZDO::Set|SetOwner|BeginOnce|RecordSettlement|RecordPersistence|TryBeginFromProject|TryBeginProjectReturn|StartCreated|CancelUnpaid|BeginPayout|System.IO.File::(?:Write|Append|Delete|Move|Replace)|SaveWorld|SavePlayer') 'QA fixture entry gained gameplay effects beyond the unpaid plan.'
}
try {
    Require-LoaderVersion $normal
    Require-LoaderVersion $qa
    Require ($null -eq $normal.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestStorage')) 'Normal build exports the QA storage lookup.'
    Require-StorageLookup $qa
    $lookupType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestStorage')
    $lookupCall = @($lookupType.Methods | Where-Object Name -eq 'ScanStorageRegistry')[0].Body.Instructions |
        Where-Object { [string]$_.Operand -match 'ZDOMan::GetAllZDOsWithPrefabIterative' } | Select-Object -First 1
    $originalCode = $lookupCall.OpCode; $originalOperand = $lookupCall.Operand
    try {
        $lookupCall.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $lookupCall.Operand = $null
        $rejected = $false
        try { Require-StorageLookup $qa }
        catch { if ($_.Exception.Message -notmatch 'Storage lookup lost its required read/guard|Storage lookup registry traversal disconnected') { throw }; $rejected = $true }
        Require $rejected 'Storage lookup contract accepted a disconnected registry scan.'
    } finally { $lookupCall.OpCode = $originalCode; $lookupCall.Operand = $originalOperand }
    Require-StorageLookup $qa
    $open = @($lookupType.Methods | Where-Object Name -eq 'LookupStorageMarker')[0].Body.Instructions |
        Where-Object { [string]$_.Operand -match 'FileStream::.ctor' } | Select-Object -First 1
    $access = $open.Previous.Previous; $originalCode = $access.OpCode
    try {
        $access.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I4_2
        $rejected = $false
        try { Require-StorageLookup $qa }
        catch { if ($_.Exception.Message -cne 'Storage lookup file stream is not existing-file/read-only.') { throw }; $rejected = $true }
        Require $rejected 'Storage lookup contract accepted a file stream with write access.'
    } finally { $access.OpCode = $originalCode }
    Require-StorageLookup $qa
    $sharing = $open.Previous; $originalCode = $sharing.OpCode
    try {
        $sharing.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I4_3
        $rejected = $false
        try { Require-StorageLookup $qa }
        catch { if ($_.Exception.Message -cne 'Storage lookup did not lock out concurrent writers.') { throw }; $rejected = $true }
        Require $rejected 'Storage lookup contract accepted weakened file sharing.'
    } finally { $sharing.OpCode = $originalCode }
    $lookupIdle = @($qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl').Methods | Where-Object Name -eq 'RequireStorageIdle')[0]
    foreach ($guardCall in 'ManagedThreadId','Input::GetKey','File::Exists','StoragePersistenceBusy','get_QaReadonlyIdle') {
        $call = $lookupIdle.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and [string]$_.Operand -match [regex]::Escape($guardCall) } | Select-Object -First 1
        Require ($null -ne $call) "Missing lookup admission control: $guardCall"
        $originalCode = $call.OpCode; $originalOperand = $call.Operand
        try {
            $call.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $call.Operand = $null
            $rejected = $false
            try { Require-StorageLookup $qa }
            catch { if ($_.Exception.Message -notmatch 'Storage lookup lost admission guard|Storage lookup lost its required read/guard') { throw }; $rejected = $true }
            Require $rejected "Storage lookup accepted disconnected admission: $guardCall"
        } finally { $call.OpCode = $originalCode; $call.Operand = $originalOperand }
    }
    Require-StorageLookup $qa
    'PASS storage lookup: compiled readonly call graph, scalar QA-only entrypoint; disconnected scan/admission, write access and weakened sharing rejected in memory only.'
    Require ($null -eq $normal.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestConstructionProject')) 'Normal build exports the QA contents project helper.'
    Require-ContentsProject $qa
    $contentsHelper = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksTestConstructionProject')
    foreach ($negative in @(
        @('CreatePaidContentsProject','Action::Invoke'),
        @('CreatePaidContentsProject','BlueprintConstructionProjectRuntime::Read('),
        @('CreatePaidContentsProject','SuppliesAvailable'),
        @('CreatePaidContentsProject','BlueprintConstructionProjectRuntime::Create('),
        @('ReadContentsBlueprint','ComputeHash'),
        @('AdoptPaidContentsProject','SequenceEqual'),
        @('RememberContentsProject','Guid::NewGuid')
    )) {
        $method = @($contentsHelper.Methods | Where-Object Name -eq $negative[0])[0]
        $call = $method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and ([string]$_.Operand).Contains($negative[1]) } | Select-Object -First 1
        Require ($null -ne $call) 'Missing contents negative-control call.'
        $originalCode = $call.OpCode; $originalOperand = $call.Operand
        try {
            $call.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $call.Operand = $null
            $rejected = $false
            try { Require-ContentsProject $qa }
            catch { if ($_.Exception.Message -notmatch 'Contents project lost|Contents adoption does not|Contents project did not initialize') { throw }; $rejected = $true }
            Require $rejected 'Contents contract accepted a disconnected preflight/adoption guard.'
        } finally { $call.OpCode = $originalCode; $call.Operand = $originalOperand }
    }
    Require-ContentsProject $qa
    'PASS QA contents project: pinned single-chest demand, stable identity, unpaid-only factory, exact adoption/unpaid-cancel state and seven disconnected-guard controls. Static/inert checks only.'
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
    foreach ($name in 'BuildWorksQaControl','BuildWorksQaControlProtocol','BuildWorksQaInput','BuildWorksQaUiInput','BuildWorksQaUiCancellation','BuildWorksQaFilePublisher','BuildWorksQaUiTargets','BuildWorksTestPlacementDiagnostics') {
        Require ($null -eq $normal.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type leaked into normal build: $name"
        Require ($null -ne $qa.MainModule.GetType("OstrixMods.BuildWorks.$name")) "QA type absent: $name"
    }
    Require (@($normal.MainModule.AssemblyReferences | Where-Object Name -eq 'Unity.InputSystem').Count -eq 0) 'QA InputSystem dependency leaked into normal build.'
    $control = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaControl')
    $afterInput = Calls $control 'AfterInput'
    Require ($afterInput -match 'BuildWorksQaUiTargets::Require\(' -and
        $afterInput -match 'BuildWorksQaUiTargets::RequireFocused' -and
        (Calls $control 'Update') -match 'BuildWorksQaControl::SelectUiTarget' -and
        $afterInput -match 'BuildWorksQaControl::SelectUiTarget' -and
        (Calls $control 'SelectUiTarget') -match 'BuildWorksQaUiTargets::Resolve' -and
        (Calls $control 'Respond') -match 'BuildWorksQaUiTargets::Observe' -and
        (Calls $control 'Stop') -match 'BuildWorksQaUiTargets::Clear') 'UI observer/dispatch/focus/held-input/stop guard disconnected.'
    $pairTarget = Calls $control 'SelectUiTarget'
    foreach ($method in 'Update', 'AfterInput') {
        $dispatch = Calls $control $method
        Require ($dispatch -match 'Request::get_InventoryPair' -and $dispatch -match 'Request::get_FocusThenText') "QA $method lost the inventory/focus-text distinction."
    }
    Require ($pairTarget -match 'Entry::writable' -and $pairTarget.Contains('Paired QA focus/text requires')) 'Focus/text preflight lost its writable-field guard.'
    foreach ($boundary in 'InventoryGui::IsVisible', 'ZInput::IsTouchActive', 'GetComponentInParent<InventoryGrid>', 'GetComponent<UIInputHandler>', 'Humanoid::GetInventory', 'InventoryGrid::GetInventory') {
        Require ($pairTarget.Contains($boundary)) "Guarded inventory pair lost boundary: $boundary"
    }
    & (Join-Path $root 'tests/BuildWorks.QaPairDispatchChecks/Run.ps1')
    $uiTargets = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksQaUiTargets')
    Require ((Calls $uiTargets 'Hit') -match 'EventSystem::RaycastAll' -and
        (Calls $uiTargets 'Describe') -match 'RectTransform::GetWorldCorners') 'UI targets bypass native raycast/geometry.'
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
    $manual = @($control.Methods | Where-Object Name -eq 'StartFogHouseManual')[0]
    Require ($null -ne $manual -and (Calls $control 'StartFogHouseManual') -match 'BuildWorksQaControl::StartFogHouse' -and
        @($manual.Body.Instructions | Where-Object { $_.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_R4 -and $_.Operand -eq 300 }).Count -eq 1 -and
        (Calls $control 'StartFogHouseManual') -match 'get_realtimeSinceStartup' -and
        (Calls $control 'CloseFogVisual') -match 'manualFogUntil' -and
        (Calls $control 'Update') -match 'manualFogUntil') 'Manual visual is not isolated, bounded and cleared.'
    $requireManualFogFlags = {
    foreach ($name in 'OnApplicationFocus','OnApplicationPause','OnDisable','OnDestroy') {
        $method = @($control.Methods | Where-Object Name -eq $name)[0]
        $stopCall = @($method.Body.Instructions | Where-Object { $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,[Mono.Cecil.Cil.Code]::Callvirt) -and $_.Operand.Name -eq 'Stop' })[0]
        $expected = if ($name -in @('OnApplicationFocus','OnApplicationPause')) { [Mono.Cecil.Cil.Code]::Ldc_I4_1 } else { [Mono.Cecil.Cil.Code]::Ldc_I4_0 }
        Require ($stopCall.Previous.OpCode.Code -eq $expected) "Manual visual/input lifecycle flag differs: $name"
    }
    }
    & $requireManualFogFlags
    $focus = @($control.Methods | Where-Object Name -eq 'OnApplicationFocus')[0]
    $flag = @($focus.Body.Instructions | Where-Object { $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,[Mono.Cecil.Cil.Code]::Callvirt) -and $_.Operand.Name -eq 'Stop' })[0].Previous
    $originalFlag = $flag.OpCode
    try {
        $flag.OpCode = [Mono.Cecil.Cil.OpCodes]::Ldc_I4_0
        $rejected = $false
        try { & $requireManualFogFlags } catch {
            if ($_.Exception.Message -cne 'Manual visual/input lifecycle flag differs: OnApplicationFocus') { throw }
            $rejected = $true
        }
        Require $rejected 'Manual visual lifecycle accepted a disconnected focus flag.'
    } finally { $flag.OpCode = $originalFlag }
    foreach ($call in @($control.Methods | Where-Object Name -eq 'Update')[0].Body.Instructions |
        Where-Object { $_.OpCode.Code -in @([Mono.Cecil.Cil.Code]::Call,[Mono.Cecil.Cil.Code]::Callvirt) -and $_.Operand.Name -eq 'Stop' }) {
        Require ($call.Previous.OpCode.Code -eq [Mono.Cecil.Cil.Code]::Ldc_I4_0) 'Emergency/protocol stop unexpectedly preserves manual visual.'
    }
    'PASS compiled manual visual: explicit QA-only 300-second deadline; focus/pause retain visual, disable/destroy/emergency close it; existing input release remains required. Not runtime acceptance.'

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
& (Join-Path $PSScriptRoot 'Test-BbpReadOnlyWindow.ps1')
& (Join-Path $PSScriptRoot 'Test-BbpReadOnlyWindow.ps1') -Family Planner
