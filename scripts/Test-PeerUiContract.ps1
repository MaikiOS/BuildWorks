$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -Path (Join-Path (Split-Path $root) 'BepInEx/core/Mono.Cecil.dll')
$assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'artifacts/bin/BuildWorks.dll'))
try {
 $view=$assembly.MainModule.GetType('OstrixMods.BuildWorks.BlueprintExchangeView')
 function Calls([string]$name){$method=@($view.Methods | Where-Object Name -eq $name);if($method.Count -ne 1 -or -not $method[0].HasBody){throw "Missing peer UI method $name"};($method[0].Body.Instructions | ForEach-Object {[string]$_.Operand}) -join "`n"}
 function Require([bool]$ok,[string]$why){if(-not $ok){throw $why}}
 $save=Calls 'SaveAndAcceptPeer'
 Require ($save.IndexOf('TrySaveAcceptedPeerCopy') -ge 0 -and $save.IndexOf('TrySaveAcceptedPeerCopy') -lt $save.LastIndexOf('StartPeer') -and $save.IndexOf('peerSaved') -lt $save.LastIndexOf('StartPeer')) 'ACK starts before durable copy publication.'
 Require ($save -match 'RequirePeerContext' -and $save -match 'get_IsHostCurrent|IsHostCurrent' -and $save -match 'peerBytes') 'Save lost context/host/original-byte boundary.'
 $ack=@($view.Methods | Where-Object {$_.HasBody -and (($_.Body.Instructions | ForEach-Object {[string]$_.Operand}) -join "`n") -match 'BlueprintPeerClient::Accept\('})
 Require ($ack.Count -eq 1 -and $ack[0].Name -like '<SaveAndAcceptPeer>*') 'A peer ACK escaped the post-Store handler.'
 Require ((Calls 'TickPeer') -match 'SaveAndAcceptPeer' -and (Calls 'AcceptPeerAction') -match 'StartPeer' -and (Calls 'TickServer') -match 'TickPeer' -and (Calls 'ForServer') -match 'InitializePeerPanel') 'Peer consent flow is disconnected.'
 Require ((Calls 'DisposeServerOperation') -match 'DisposePeerOperation' -and (Calls 'DisposePeerOperation') -match 'BlueprintPeerClient::Cancel') 'Closing the window lost its captured task cancellation.'
 $rows=Calls 'RebuildPeerRows'
 Require ($rows.IndexOf('GameObject::SetActive') -ge 0 -and $rows.IndexOf('GameObject::SetActive') -lt $rows.IndexOf('Object::Destroy')) 'Old peer rows remain clickable until deferred Destroy.'
 foreach($name in 'BlueprintEditorController','HammerCatalogView'){
  $type=$assembly.MainModule.GetType('OstrixMods.BuildWorks.'+$name)
  $body=($type.Methods | Where-Object HasBody | ForEach-Object {$_.Body.Instructions | ForEach-Object {[string]$_.Operand}}) -join "`n"
  foreach($nested in $type.NestedTypes){$body+="`n"+(($nested.Methods | Where-Object HasBody | ForEach-Object {$_.Body.Instructions | ForEach-Object {[string]$_.Operand}}) -join "`n")}
  Require ($body -match 'get_PeerBlueprintClient') "$name does not provide the actual runtime peer client."
 }
 'PASS compiled peer UI wiring: both catalogue providers, explicit consent/status path, original-byte Store before ACK and published copy, owned-task cancellation. This is static evidence, not rendered or network acceptance.'
} finally {$assembly.Dispose()}
