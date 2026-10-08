[CmdletBinding()]
param([switch]$CheckLaunchProfileOnly, [switch]$CheckCompiledOnly)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$profile = Join-Path (Split-Path (Split-Path $root)) 'TerrainRamp-1.0-Test'
$qaPath = Join-Path $root 'artifacts/test-stand-bin/BuildWorks.dll'
$normalPath = Join-Path $root 'artifacts/bin/BuildWorks.dll'
if ($CheckLaunchProfileOnly) {
    $bootstrap = @(Get-ChildItem -LiteralPath (Join-Path $profile 'BepInEx/plugins') -Recurse -File -Filter 'TerrainRamp.TestBootstrap.dll')
    if ($bootstrap.Count) {
        if ($bootstrap.Count -ne 1 -or (Get-FileHash -LiteralPath $bootstrap[0].FullName).Hash -ne '67EDB9A9BCA33974C879BB9554AC51876B62996BFCA7729C17E852130DB35E41') {
            throw 'Unknown or duplicate TerrainRamp bootstrap: save-writing behavior requires review before QA launch.'
        }
        $configuration = Join-Path $profile 'BepInEx/config/com.ostrix.terrainramp.testbootstrap.cfg'
        if (-not (Test-Path -LiteralPath $configuration -PathType Leaf) -or (Get-Item -LiteralPath $configuration).Length -gt 128KB) {
            throw 'Missing or invalid TerrainRamp bootstrap config; automatic save creation defaults to enabled.'
        }
        $inBootstrap = $false
        $values = @()
        foreach ($line in Get-Content -LiteralPath $configuration) {
            if ($line -match '^\s*\[([^\]]+)\]\s*$') { $inBootstrap = $Matches[1] -eq 'Bootstrap' }
            elseif ($inBootstrap -and $line -match '^\s*CreateMissingSaves\s*=\s*(.*?)\s*$') { $values += $Matches[1] }
        }
        if ($values.Count -ne 1 -or $values[0] -ine 'false') {
            throw "QA launch blocked: $configuration [Bootstrap] CreateMissingSaves must be false before starting Valheim. No profile changes were made."
        }
    }
    'PASS preflight: known TerrainRamp automatic save writer is absent or explicitly disabled. This does not launch the game or prove every other plugin.'
    return
}
Add-Type -Path (Join-Path $profile 'BepInEx/core/Mono.Cecil.dll')
function Require([bool]$value, [string]$message) { if (-not $value) { throw $message } }
$qa = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($qaPath)
$normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly($normalPath)
$typeName = 'OstrixMods.BuildWorks.BuildWorksTestIsolation'
$type = $qa.MainModule.GetType($typeName)
Require ($null -ne $type -and $null -eq $normal.MainModule.GetType($typeName)) 'QA isolation leaked into normal assembly or is missing from the QA build.'
$activate = $type.Methods | Where-Object Name -eq 'Activate'
$calls = @($activate.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') } | ForEach-Object { [string]$_.Operand })
$cold = $activate.Body.Instructions | Where-Object { [string]$_.Operand -match '::RequireColdStorage' } | Select-Object -First 1
$owned = $activate.Body.Instructions | Where-Object { [string]$_.Operand -match '::EnsureOwnedRoot' } | Select-Object -First 1
Require (($calls -join "`n") -match 'RequireColdStorage' -and ($calls -join "`n") -match 'Utils::SetSaveDataPath' -and
    $cold.Offset -lt $owned.Offset) 'Native root activation lost the cold-storage-before-write guard.'
$strings = @($activate.Body.Instructions | Where-Object { $_.OpCode.Name -eq 'ldstr' } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('CloudStorageSupported','CloudStorageSupportedAndEnabled','BeforeStorage','BeforeSetRoot','BeforeResetRoot')) {
    Require ($strings -contains $name) "QA isolation omits native guard: $name"
}
$managed = 'C:/Program Files (x86)/Steam/steamapps/common/Valheim/valheim_Data/Managed'
foreach ($entry in @(@('assembly_valheim.dll','25A0A107DCE4D834C44C2B72D0EAFD5CB7793933BDA81816ACCFA1EA9543DACE'),
    @('assembly_utils.dll','6BE2AFEB97B5B26FD3176E1B8584C07E57AD48DDCD6E8E5E79E32BEE943827C1'))) {
    Require ($strings -contains $entry[1] -and (Get-FileHash -LiteralPath (Join-Path $managed $entry[0])).Hash -eq $entry[1]) 'QA native save contract fingerprint changed.'
}
$allCalls = ($type.Methods | Where-Object HasBody | ForEach-Object { $_.Body.Instructions | ForEach-Object { [string]$_.Operand } }) -join "`n"
Require ($allCalls -notmatch 'ResetSaveDataPath\(\)|SaveSystem::ForceRefreshCache|Directory::Delete|File::Delete|File::Move') 'QA isolation gained cache clearing, root reset or destructive filesystem operations.'
$loaded = $type.Methods | Where-Object Name -eq 'RequireLoadedSession'
$loadedCalls = @($loaded.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('AssertPinned','IsServer','IsDedicated','IsOpenServer','GetPeers','IsShuttingDown','WaitingForRespawn','IsDead','IsTeleporting','InIntro',
    'GetInventory','NoCostCheat','GetGlobalKey','GetWorld','GetPlayerProfile','GetSaveFWLPath','RequirePersistedWorld','RequirePersistedPaths','IsSaving','GetPath','GetFilename',
    'GetPlayerID','ReadIdentityReceipt','RequireIdentityMatch')) {
    Require ([bool]($loadedCalls -match "::$name\(")) "Loaded QA session omits native identity/survival guard: $name"
}
$loadedFields = @($loaded.Body.Instructions | Where-Object { $_.OpCode.Name -in @('ldfld','ldsfld') } | ForEach-Object { [string]$_.Operand })
foreach ($name in @('m_debugMode','m_worldLevel','m_fileSource','m_worldName','m_uid','m_loadError')) {
    Require ([bool]($loadedFields -match "::$name$")) "Loaded QA session omits native identity/survival field: $name"
}
$resume = ($type.Methods | Where-Object Name -eq 'PrepareFreshSaves').Body.Instructions
foreach ($name in @('TryGetSaveByName','RequirePersistedWorld','LoadWorld','Load','RequireIdentityMatch')) {
    Require ([bool](@($resume | ForEach-Object { [string]$_.Operand }) -match "::$name\(")) "QA identity replay omits native persisted-save validation: $name"
}
Require (-not [bool]($loadedCalls -match 'System.IO.File::Exists\(')) 'Loaded guard directly checks a dynamic write-slot file.'
$saving = $loaded.Body.Instructions | Where-Object { [string]$_.Operand -match 'ZNet::IsSaving\(' } | Select-Object -First 1
$disk = $loaded.Body.Instructions | Where-Object { [string]$_.Operand -match '::RequirePersistedWorld\(' } | Select-Object -First 1
Require ($saving.Next.OpCode.Name -in @('brtrue','brtrue.s') -and $saving.Next.Operand.Offset -gt $disk.Offset) 'Live async-save polling reads the rotating persisted-primary collection.'
$persisted = ($type.Methods | Where-Object Name -eq 'RequirePersistedWorld').Body.Instructions.Operand -join "`n"
foreach ($name in @('get_IsDeleted','get_PrimaryFile','m_source','get_AllPaths','RequirePersistedPaths')) {
    Require ($persisted -match $name) "Persisted native primary validation omits $name"
}
$pluginType = $qa.MainModule.GetType('OstrixMods.BuildWorks.BuildWorksPlugin')
$suppliesName = 'OstrixMods.BuildWorks.BuildWorksTestSupplies'
$suppliesType = $qa.MainModule.GetType($suppliesName)
Require ($null -ne $suppliesType -and $null -eq $normal.MainModule.GetType($suppliesName)) 'QA supplies leaked into normal build or are missing.'
$prepare = $suppliesType.Methods | Where-Object Name -eq 'Prepare'
$prepareCalls = @($prepare.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') })
foreach ($name in @('RequireLoadedSession','Plan','SupplyStamp','OnceComplete','BeginOnce','Count','AddItem','AddKnownItem','SavePlayerData','Save','CompleteOnce')) {
    Require ([bool]($prepareCalls | Where-Object { [string]$_.Operand -match "::$name\(" })) "Native supply path omits $name"
}
function SupplyOffset([string]$name) { ($prepareCalls | Where-Object { [string]$_.Operand -match "::$name\(" } | Select-Object -First 1).Offset }
Require ((SupplyOffset 'BeginOnce') -lt (SupplyOffset 'AddItem') -and (SupplyOffset 'Save') -lt (SupplyOffset 'CompleteOnce')) 'Supply receipt ordering lost started-before-grant or saved-before-complete.'
$coroutine = $pluginType.NestedTypes | Where-Object Name -Like '<PrepareQaSaves>*'
function Require-StorageStartGuard($method) {
    $instructions = @($method.Body.Instructions)
    $started = $instructions | Where-Object { [string]$_.Operand -match 'PlatformInitializer::get_StartedSaveDataInitialization\(' } | Select-Object -First 1
    $ready = $instructions | Where-Object { [string]$_.Operand -match 'PlatformInitializer::get_SaveDataInitialized\(' } | Select-Object -First 1
    Require ($null -ne $started -and $null -ne $ready) "Missing native storage lifecycle guard in $($method.FullName)"
    Require ($started.Offset -lt $ready.Offset -and $started.Next.OpCode.Name -in @('brfalse','brfalse.s')) "Early QA startup can dereference an absent platform in $($method.FullName)"
    $cursor = $started.Next.Operand
    $stopped = $false
    for ($step = 0; $step -lt $instructions.Count; $step++) {
        Require ($null -ne $cursor -and $cursor.Offset -ne $ready.Offset) 'Unstarted storage reaches the unsafe readiness getter.'
        if ($cursor.OpCode.Name -in @('ret','throw','leave','leave.s')) { $stopped = $true; break }
        Require ($cursor.OpCode.FlowControl.ToString() -ne 'Cond_Branch') 'Unexpected branch on the unstarted-storage refusal/yield path.'
        $cursor = if ($cursor.OpCode.Name -in @('br','br.s')) { $cursor.Operand } else { $cursor.Next }
    }
    Require $stopped 'Unstarted storage neither yields nor refuses.'
}
Require-StorageStartGuard ($coroutine.Methods | Where-Object Name -eq 'MoveNext')
Require-StorageStartGuard ($type.Methods | Where-Object Name -eq 'PrepareFreshSaves')
$iterator = ($coroutine.Methods | Where-Object Name -eq 'MoveNext').Body.Instructions
$respawnWait = $iterator | Where-Object { [string]$_.Operand -match 'Game::WaitingForRespawn\(' } | Select-Object -First 1
$playerIdWait = $iterator | Where-Object { [string]$_.Operand -match 'Player::GetPlayerID\(' } | Select-Object -First 1
Require ($null -ne $respawnWait -and $null -ne $playerIdWait) 'QA preparation does not wait for native respawn and a nonzero player identity.'
Require ($respawnWait.Next.OpCode.Name -in @('brtrue','brtrue.s') -and
    $playerIdWait.Next.OpCode.Name -in @('brfalse','brfalse.s') -and
    $respawnWait.Next.Operand.Offset -eq $playerIdWait.Next.Operand.Offset -and
    $respawnWait.Next.Operand.Next.Next.OpCode.Name -eq 'stfld' -and
    [string]$respawnWait.Next.Operand.Next.Next.Operand -match '<>2__current') 'Respawning/zero-ID player does not yield before provisioning.'
Require ([bool](@($coroutine.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -match '::RequireLoadedSession\(')) 'QA loaded gate is not called by the actual plugin coroutine.'
Require ([bool](@($coroutine.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -match '(Character|Player)::InIntro\(')) 'QA provisioning does not wait for the fresh-player intro flight.'
Require ([bool](@($coroutine.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -match 'BuildWorksTestSupplies::Prepare\(')) 'QA supply path is not called by actual plugin coroutine.'
$stationsName = 'OstrixMods.BuildWorks.BuildWorksTestStations'
$stations = $qa.MainModule.GetType($stationsName)
Require ($null -ne $stations -and $null -eq $normal.MainModule.GetType($stationsName)) 'QA stations leaked into the normal build or are missing.'
$resolver = $stations.Methods | Where-Object Name -eq 'ResolveStation'
Require ($null -ne $resolver) 'QA station requirements are not resolved through the registered prefab.'
$resolverIl = @($resolver.Body.Instructions)
$parentLookup = $resolverIl | Where-Object { [string]$_.Operand -match '::GetComponentInParent<Piece>\(System.Boolean\)' } | Select-Object -First 1
Require ($null -ne $parentLookup -and $parentLookup.Previous.OpCode.Name -eq 'ldc.i4.1') 'QA station resolution skips inactive prefab parents.'
foreach ($component in @('Piece','CraftingStation','ZNetView')) {
    Require ([bool]($resolverIl.Operand -match "::GetComponent<$component>\(")) "Canonical QA station omits root $component validation."
}
Require ([bool]($resolverIl.Operand -match 'ZNetScene::GetPrefab\(System.String\)') -and
    [bool]($resolverIl.Operand -match 'CraftingStation::m_name') -and
    [bool]($resolverIl.Operand -match 'System.String::op_Inequality') -and
    -not [bool]($resolverIl.Operand -match 'UnityEngine.Object::op_(Equality|Inequality)')) 'QA station resolver requires serialized object-reference equality or omits exact native station type matching.'
Require ([bool](@($coroutine.Methods.Body.Instructions | ForEach-Object { [string]$_.Operand }) -match 'BuildWorksTestStations::Prepare\(')) 'QA station path is not called by the plugin coroutine.'
$stationIterator = $stations.NestedTypes | Where-Object Name -Like '<Prepare>*'
$stationCalls = @($stationIterator.Methods.Body.Instructions | Where-Object { $_.OpCode.Name -in @('call','callvirt') })
foreach ($name in @('RequireLoadedSession','Plan','ResolveStation','OnceComplete','FindSite','BeginOnce','PlacePiece','IsValid','IsOwner','GetCreator',
    'HaveBuildStationInRange','AddKnownStation','SavePlayerData','Save','IsSaving','GetSaveNumber','RequireSaveAcknowledgement','CompleteOnce')) {
    Require ([bool]($stationCalls | Where-Object { [string]$_.Operand -match "::$name\(" })) "Native QA station path omits $name"
}
function StationOffset([string]$name) { ($stationCalls | Where-Object { [string]$_.Operand -match "::$name\(" } | Select-Object -First 1).Offset }
Require ((StationOffset 'FindSite') -lt (StationOffset 'BeginOnce') -and (StationOffset 'BeginOnce') -lt (StationOffset 'PlacePiece') -and
    (StationOffset 'RequireSaveAcknowledgement') -lt (StationOffset 'CompleteOnce')) 'QA station receipt ordering lost preflight-before-start, start-before-place or saved-before-complete.'
$footprint = ($stations.Methods | Where-Object Name -eq 'Footprint').Body.Instructions.Operand -join "`n"
foreach ($name in @('TerrainModifier','TerrainOp','TerrainComp','BoxCollider','MeshCollider','CreateVisualClone','TryGetLocalVisualBounds')) {
    Require ($footprint -match $name) "Station footprint omits $name"
}
Require ($footprint -notmatch 'Object::Instantiate|Collider::get_bounds') 'Station preflight instantiates gameplay objects or trusts inactive collider bounds.'
$yardName = 'OstrixMods.BuildWorks.BuildWorksTestYard'
$terrainName = 'OstrixMods.BuildWorks.BuildWorksTestTerrain'
$terrain = $qa.MainModule.GetType($terrainName)
Require ($null -ne $terrain -and $null -eq $normal.MainModule.GetType($terrainName)) 'QA terrain clamp leaked into normal assembly or is missing.'
$terrainTargets = ($terrain.NestedTypes.Methods.Body.Instructions.Operand -join "`n")
Require ($terrainTargets -match 'LevelTerrain' -and $terrainTargets -match 'ApplyToHeightmap') 'QA clamp omits preparation or reload.'
$limit = ($terrain.Methods | Where-Object Name -eq 'Limit').Body.Instructions.Operand -join "`n"
foreach ($guard in @('AssertPinned','ReadIdentityReceipt','GetString','m_fileSource','m_worldName','m_uid','IsServer','IsDedicated','IsOpenServer','GetPeers')) {
    Require ($limit -match $guard) "QA terrain bound omits $guard"
}
$nativeTerrain = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $managed 'assembly_valheim.dll')).MainModule.GetType('TerrainComp')
foreach ($method in @('LevelTerrain','ApplyToHeightmap')) {
    $body = ($nativeTerrain.Methods | Where-Object Name -eq $method).Body.Instructions
    Require (@($body | Where-Object { $_.OpCode.Name -eq 'ldc.r4' -and [Math]::Abs([single]$_.Operand) -eq 8 }).Count -eq 2 -and
        [bool]($body.Operand -match 'Mathf::Clamp')) "Native $method clamp contract changed."
}
$yard = $qa.MainModule.GetType($yardName)
Require ($null -ne $yard -and $null -eq $normal.MainModule.GetType($yardName)) 'QA yard leaked into normal assembly or is missing.'
$searchExtent = $yard.Fields | Where-Object Name -eq 'SearchExtent'
Require ($null -ne $searchExtent -and $searchExtent.Constant -eq 4096) 'QA yard search is not bounded to the expanded four-kilometre square.'
$yardIterator = $yard.NestedTypes | Where-Object Name -Like '<Prepare>*'
$yardIl = @($yardIterator.Methods.Body.Instructions)
foreach ($name in @('RequireLoadedSession','ValidateSavePath','ReadIdentityReceipt','OnceComplete','TeleportTo','CheckGround',
    'Copy','SaveWorld','BeginOnce','CheckGround','RequireEmpty','Vegetation','ClaimOwnership','IsOwner','Invoke','GetByteArray','CompleteOnce')) {
    Require ([bool]($yardIl.Operand -match "::$name\(")) "QA yard omits $name"
}
function YardOffset([string]$name) { ($yardIl | Where-Object { [string]$_.Operand -match "::$name\(" } | Select-Object -First 1).Offset }
$compilerCall = @($yard.NestedTypes.Methods.Body.Instructions | Where-Object { [string]$_.Operand -match '::GetAndCreateTerrainCompiler\(' })
Require ($compilerCall.Count -eq 1) 'QA yard does not initialize its native terrain compiler batch.'
$ownership = YardOffset 'ClaimOwnership'
$lastPreflight = ($yardIl | Where-Object { [string]$_.Operand -match '::CheckGround\(' -and $_.Offset -lt $ownership } | Select-Object -Last 1).Offset
Require ($lastPreflight -gt (YardOffset 'BeginOnce') -and
    [bool]($yardIl | Where-Object { $_.Offset -gt (YardOffset 'BeginOnce') -and $_.Offset -lt $ownership -and [string]$_.Operand -match '::RequireLoadedSession\(' }) -and
    [bool]($yardIl | Where-Object { $_.Offset -gt $lastPreflight -and $_.Offset -lt $ownership -and [string]$_.Operand -match '::RequireEmpty\(' })) 'QA yard omits post-yield session/ground/object revalidation.'
$overlap = ($yard.Methods | Where-Object Name -eq 'Overlaps').Body.Instructions.Operand -join "`n"
Require ($overlap -match 'Collider' -and $overlap -match 'Renderer') 'QA yard ignores objects crossing its boundary.'
$destroyOffset = YardOffset 'Destroy'
Require ($null -ne $destroyOffset -and -not [bool]($yardIl | Where-Object { $_.Offset -gt (YardOffset 'Invoke') -and $_.Offset -lt $destroyOffset -and
    $_.OpCode.Name -eq 'stfld' -and [string]$_.Operand -match '<>2__current' })) 'QA yard yields after terrain preflight but before completing object mutations.'
Require ((YardOffset 'CheckGround') -lt (YardOffset 'BeginOnce') -and (YardOffset 'Copy') -lt (YardOffset 'BeginOnce') -and
    (YardOffset 'BeginOnce') -lt $lastPreflight -and
    (YardOffset 'SaveWorld') -lt (YardOffset 'Copy') -and
    ($yardIl | Where-Object { [string]$_.Operand -match '::SaveWorld\(' } | Select-Object -Last 1).Offset -gt (YardOffset 'Invoke') -and
    ($yardIl | Where-Object { [string]$_.Operand -match '::SaveWorld\(' } | Select-Object -Last 1).Offset -lt (YardOffset 'CompleteOnce')) 'QA yard lost preflight/backup/start/native-save ordering.'
$daylight = ($yard.Methods | Where-Object Name -eq 'Daylight').Body.Instructions
Require ([bool]($daylight.Operand -match '::RequireLoadedSession\(') -and [bool]($daylight.Operand -match 'EnvMan::m_debugTimeOfDay')) 'QA daylight lost identity gate.'
Require (-not [bool]($yardIl.Operand -match '::SetNoCost|::SetGodMode|Player::m_debugMode|::SetGlobalKey|Inventory::AddItem')) 'QA yard grants gameplay cheats/resources.'
$qa.Dispose(); $normal.Dispose()
if ($CheckCompiledOnly) {
    'PASS: QA/normal separation, native fingerprints and compiled provisioning guards; no file writes or native execution.'
    return
}

# Only BCL path/ownership functions execute here. No native API/root setter/save is invoked.
[Reflection.Assembly]::LoadFrom((Join-Path $profile 'BepInEx/core/0Harmony.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $root 'artifacts/test-stand-bin/BuildWorks.Geometry.dll')) | Out-Null
$assembly = [Reflection.Assembly]::LoadFrom($qaPath)
$runtimeType = $assembly.GetType($typeName,$true)
$flags = [Reflection.BindingFlags]'Static,Public,NonPublic'
function Invoke-PathCheck([string]$name, [string[]]$arguments) {
    $runtimeType.GetMethod($name,$flags).Invoke($null,$arguments)
}
function Refuses([scriptblock]$action, [string]$message) {
    $refused = $false
    try { & $action | Out-Null } catch {
        if ($_.Exception.GetBaseException() -isnot [IO.InvalidDataException]) { throw }
        $refused = $true
    }
    Require $refused $message
}
$output = Join-Path $root ('artifacts/qa-isolation-checks/' + [guid]::NewGuid().ToString('N'))
$config = Join-Path $output 'TerrainRamp-1.0-Test/BepInEx/config'
[IO.Directory]::CreateDirectory($config) | Out-Null
$token = [guid]::NewGuid().ToString('N')
$isolated = Invoke-PathCheck 'ResolveRoot' @($config,$token)
Require ($isolated -eq [IO.Path]::GetFullPath((Join-Path $config "BuildWorks/qa-saves/$token"))) 'QA root is not fixed under its own config.'
Refuses { Invoke-PathCheck 'ResolveRoot' @($config,'../escape') } 'Traversal token accepted.'
Refuses { Invoke-PathCheck 'ResolveRoot' @($config,[guid]::Empty.ToString('N')) } 'Empty GUID accepted.'
Refuses { Invoke-PathCheck 'ResolveRoot' @((Join-Path $output 'Default/BepInEx/config'),$token) } 'Default profile accepted.'
Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,$token)
$owner = Join-Path $isolated 'buildworks-qa-root.txt'
Require ([IO.File]::ReadAllText($owner) -eq $token) 'Root owner token was not written exactly.'
$sentinel = Join-Path $isolated 'untouched.txt'
[IO.File]::WriteAllText($sentinel,'sentinel')
Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,$token)
Require ([IO.File]::ReadAllText($sentinel) -eq 'sentinel') 'Reopening owned root changed existing data.'
Refuses { Invoke-PathCheck 'EnsureOwnedRoot' @($isolated,[guid]::NewGuid().ToString('N')) } 'Foreign root ownership accepted.'
$unknown = Join-Path $output 'unknown'
[IO.Directory]::CreateDirectory($unknown) | Out-Null
[IO.File]::WriteAllText((Join-Path $unknown 'untouched.txt'),'sentinel')
Refuses { Invoke-PathCheck 'EnsureOwnedRoot' @($unknown,$token) } 'Unowned nonempty root adopted.'
Require (-not [IO.File]::Exists((Join-Path $unknown 'buildworks-qa-root.txt'))) 'Refusal wrote an owner marker.'
$runtimeType.GetField('root',$flags).SetValue($null,$isolated)
Invoke-PathCheck 'ValidateSavePath' @((Join-Path $isolated 'characters_local/BW_QA_12345678.fch'))
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path ($isolated+'-sibling') 'escape.fch')) } 'Sibling path prefix escaped isolation.'
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path $isolated '../escape.fch')) } 'Traversal save destination escaped isolation.'
$link = Join-Path $isolated 'linked'
New-Item -ItemType Junction -Path $link -Value $unknown | Out-Null
Refuses { Invoke-PathCheck 'ValidateSavePath' @((Join-Path $link 'escape.fch')) } 'Junction escaped isolated save destination.'
# Execute the real receipt/matching helpers; native host methods above are compiled-contract proof only.
$name = 'BW_QA_' + $token.Substring(0,8)
[string[]]$receipt = @('BuildWorks-QA-1',$token,$name,'9223372036854775806',$name,'-9223372036854775807','started','complete')
function Invoke-IdentityCheck([string]$method, [object[]]$arguments) {
    $runtimeType.GetMethod($method,$flags).Invoke($null,$arguments)
}
$persistedChecks = 0
$rotated = Join-Path $isolated ('worlds_local/' + $name)
[IO.Directory]::CreateDirectory($rotated) | Out-Null
$slot1 = Join-Path $rotated '_main.1.fwl2'
$slot0 = Join-Path $rotated '_main.0.fwl2'
$database = Join-Path $rotated '_main.1.db2'
[IO.File]::WriteAllText($slot1,'persisted primary slot one')
Invoke-IdentityCheck 'RequirePersistedPaths' @(,[string[]]@($slot1))
$persistedChecks++
Refuses { Invoke-IdentityCheck 'RequirePersistedPaths' @(,[string[]]@($slot0)) } 'Absent global write slot passed as persisted primary.'
$persistedChecks++
Refuses { Invoke-IdentityCheck 'RequirePersistedPaths' @(,[string[]]@($slot1,$database)) } 'Missing associated save file accepted.'
$persistedChecks++
[IO.File]::WriteAllText($database,'persisted database')
Invoke-IdentityCheck 'RequirePersistedPaths' @(,[string[]]@($slot1,$database))
$persistedChecks++
foreach ($paths in @($null,[string[]]@(),[string[]]@((Join-Path $unknown 'untouched.txt')),[string[]]@((Join-Path $link 'untouched.txt')))) {
    Refuses { Invoke-IdentityCheck 'RequirePersistedPaths' @(,$paths) } 'Empty or escaping persisted save paths accepted.'
    $persistedChecks++
}
$identityChecks = 0
[object[]]$identity = @($receipt,$token,$name,[long]9223372036854775806,$name,[long]::Parse('-9223372036854775807'),[long]::Parse('-9223372036854775807'))
Invoke-IdentityCheck 'RequireIdentityMatch' $identity
$identityChecks++
foreach ($index in 0..7) {
    [string[]]$badReceipt = $receipt.Clone()
    $badReceipt[$index] = 'invalid'
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($badReceipt,$token) } "Malformed receipt field $index accepted."
    $identityChecks++
}
foreach ($invalidId in @(@{index=3;value='0'},@{index=5;value='0'},@{index=3;value='9223372036854775808'},@{index=5;value='-9223372036854775809'})) {
    [string[]]$lines = $receipt.Clone(); $lines[$invalidId.index] = $invalidId.value
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($lines,$token) } 'Zero or overflowing native ID accepted.'
    $identityChecks++
}
foreach ($length in @(7,9)) {
    [string[]]$lines = if ($length -eq 7) { $receipt[0..6] } else { @($receipt) + @('extra') }
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($lines,$token) } 'Partial or extended receipt accepted.'
    $identityChecks++
}
Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($null,$token) } 'Null receipt accepted.'
$identityChecks++
foreach ($badToken in @([guid]::Empty.ToString('N'),[guid]::NewGuid().ToString('N'),'../escape')) {
    Refuses { Invoke-IdentityCheck 'ValidateIdentityReceipt' @($receipt,$badToken) } 'Foreign or malformed identity token accepted.'
    $identityChecks++
}
foreach ($index in 2..6) {
    [object[]]$mismatch = $identity.Clone()
    $mismatch[$index] = if ($index -in @(2,4)) { 'other' } else { [long]17 }
    Refuses { Invoke-IdentityCheck 'RequireIdentityMatch' $mismatch } "Loaded identity mismatch $index accepted."
    $identityChecks++
}
[object[]]$noZdo = $identity.Clone(); $noZdo[6] = [long]0
Refuses { Invoke-IdentityCheck 'RequireIdentityMatch' $noZdo } 'Uninitialized local player ZDO ID accepted.'
$identityChecks++
$onceChecks = 0
$supplyReceipt = Join-Path $isolated 'qa-supplies.txt'
$stamp = 'A' * 64
Require ([bool](Invoke-PathCheck 'BeginOnce' @($supplyReceipt,$stamp))) 'Fresh preparation did not start.'
$onceChecks++
$startedHash = (Get-FileHash $supplyReceipt).Hash
Refuses { Invoke-PathCheck 'BeginOnce' @($supplyReceipt,$stamp) } 'Interrupted preparation retried.'
Require ((Get-FileHash $supplyReceipt).Hash -eq $startedHash) 'Refusal rewrote started receipt.'
$onceChecks++
Refuses { Invoke-PathCheck 'CompleteOnce' @($supplyReceipt,('B'*64)) } 'Foreign stamp completed preparation.'
$onceChecks++
Invoke-PathCheck 'CompleteOnce' @($supplyReceipt,$stamp)
Require ([bool](Invoke-PathCheck 'OnceComplete' @($supplyReceipt,$stamp))) 'Completed receipt not recognized.'
$onceChecks++
$completeHash = (Get-FileHash $supplyReceipt).Hash
Require (-not [bool](Invoke-PathCheck 'BeginOnce' @($supplyReceipt,$stamp))) 'Completed preparation reissued supplies.'
Require ((Get-FileHash $supplyReceipt).Hash -eq $completeHash) 'Replay changed complete receipt.'
$onceChecks++
foreach ($invalidStamp in @('A',('a'*64),('G'*64),'../escape')) {
    Refuses { Invoke-PathCheck 'BeginOnce' @((Join-Path $isolated 'never-created.txt'),$invalidStamp) } 'Malformed supply stamp accepted.'
    $onceChecks++
}
Refuses { Invoke-PathCheck 'BeginOnce' @($supplyReceipt,('B'*64)) } 'Different plan adopted completed receipt.'
$onceChecks++
Refuses { Invoke-PathCheck 'BeginOnce' @((Join-Path $link 'escape.txt'),$stamp) } 'Supply receipt escaped through junction.'
$onceChecks++
$batchChecks = 0
$batchType = $assembly.GetType($suppliesName,$true)
function Batches([int]$amount,[int]$maximum) { $batchType.GetMethod('StackBatches',$flags).Invoke($null,@($amount,$maximum)) }
foreach ($case in @(@(1,50),@(50,50),@(51,50),@(123,50),@(10000,1),@(10000,10000))) {
    [int[]]$batches = Batches $case[0] $case[1]
    Require (($batches | Measure-Object -Sum).Sum -eq $case[0] -and -not ($batches | Where-Object { $_ -lt 1 -or $_ -gt $case[1] })) 'Stack batching lost/truncated items.'
    $batchChecks++
}
foreach ($case in @(@(0,50),@(-1,50),@(10001,50),@(1,0),@(1,-1),@(1,10001),@([int]::MaxValue,1))) {
    Refuses { Batches $case[0] $case[1] } 'Invalid/excessive supply stack accepted.'
    $batchChecks++
}
$stationCases = 0
$stationType = $assembly.GetType($stationsName,$true)
function Ground([single[]]$heights) { $stationType.GetMethod('FlatGround',$flags).Invoke($null,[object[]]@(,$heights)) }
Require ((Ground ([single[]]@(1,1,1,1,1,1,1,1,1))) -eq 1) 'Flat ground was rejected.'
$stationCases++
Require ((Ground ([single[]]@(0,0,0,0.1,0.1,0.1,0,0,0))) -eq [single]0.1) 'Ground plan did not use highest supported sample.'
$stationCases++
foreach ($heights in @(@(),@(0,0,0),@(0,0,0,0,0,0,0,0,0.2),@(0,0,0,0,0,0,0,0,[single]::NaN),
    @(0,0,0,0,0,0,0,0,[single]::PositiveInfinity),@(0,0,0,0,0,0,0,0,[single]::NegativeInfinity))) {
    Refuses { Ground ([single[]]$heights) } 'Invalid or nonflat QA ground accepted.'
    $stationCases++
}
Refuses { Ground $null } 'Null QA ground accepted.'
$stationCases++
function SaveAck([uint32]$before,[uint32]$after) { $stationType.GetMethod('RequireSaveAcknowledgement',$flags).Invoke($null,@($before,$after)) }
SaveAck 10 11
SaveAck ([uint32]::MaxValue) 0
$stationCases += 2
foreach ($numbers in @(@(10,10),@(10,12),@(10,9),@(0,0))) {
    $refused = $false
    try { SaveAck $numbers[0] $numbers[1] } catch {
        if ($_.Exception.GetBaseException() -isnot [IO.IOException]) { throw }
        $refused = $true
    }
    Require $refused 'Uncommitted or ambiguous native world save was acknowledged.'
    $stationCases++
}
$yardType = $assembly.GetType($yardName,$true)
$plane = $yardType.GetMethod('Plane',$flags)
Require ($plane.Invoke($null,@([single]35,[single]45,[single]30)) -eq 40) 'QA plane did not preserve safe midpoint.'
Require ($plane.Invoke($null,@([single]33,[single]33,[single]30)) -eq 33) 'Dry flat QA terrain rejected.'
Require ([Math]::Abs($plane.Invoke($null,@([single]35,[single]53.5402069,[single]30)) - 44.27010345) -lt 0.00001) 'Observed dry18.54m QA candidate rejected.'
Require ($plane.Invoke($null,@([single]35,[single]65,[single]30)) -eq 50) 'Maximum QA midpoint rejected.'
$yardCases = 4
foreach ($values in @(@(32,35,30),@(35,66,30),@(40,35,30),@([single]::NaN,40,30),@(35,[single]::PositiveInfinity,30),@(35,40,[single]::NegativeInfinity))) {
    Refuses { $plane.Invoke($null,@([single]$values[0],[single]$values[1],[single]$values[2])) } 'Unsafe QA yard plane accepted.'
    $yardCases++
}
@{status='passed_paths_receipts_stacks_ground_yard_save_ack_and_compiled_contract_only';persistedCases=$persistedChecks;identityCases=$identityChecks;onceCases=$onceChecks;batchCases=$batchChecks;stationCases=$stationCases;yardCases=$yardCases;qaSha256=(Get-FileHash $qaPath).Hash;normalSha256=(Get-FileHash $normalPath).Hash;gameHostTested=$false;saveFilesModified=$false;fixtures=$output} |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'verification.json')
"PASS: QA-only assemblies; $persistedChecks persisted-slot, $identityChecks identity, $onceChecks one-shot, $batchChecks stack, $stationCases ground/save-ack, $yardCases yard plane cases; native wiring only. No native grants/saves or game execution."
