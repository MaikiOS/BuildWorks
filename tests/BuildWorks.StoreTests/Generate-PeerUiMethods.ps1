$ErrorActionPreference='Stop'
$source=[IO.File]::ReadAllText((Join-Path $PSScriptRoot '../../src/BuildWorks/BlueprintExchangePeerView.cs'))
$parts=@(@('        private bool PeerContext(', '        private void RequirePeerContext('),
 @('        private void RequirePeerContext(', '        private void RefreshPeer('),
 @('        private static (CompositeBlueprintStore.Blueprint Document, int Version) DecodePeerDocument(', '        private void AcceptPeerAction('),
 @('        private void SaveAndAcceptPeer(', '        private void ShowPeerTerminal('),
 @('        private void ShowPeerTerminal(', '        private void CancelPeer('),
 @('        private string PeerRecipientName(', '        private void SetPeerDetails('))
$methods=foreach($pair in $parts){$start=$source.IndexOf($pair[0],[StringComparison]::Ordinal);$end=$source.IndexOf($pair[1],$start,[StringComparison]::Ordinal);if($start -lt 0 -or $end -le $start){throw 'Peer production method boundaries changed'};$source.Substring($start,$end-$start)}
$target=Join-Path $PSScriptRoot 'obj/PeerUiProduction.generated.cs'
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target)) | Out-Null
$text="using System; using System.IO; using System.Linq; using System.Text; using System.Security.Cryptography; using P=OstrixMods.BuildWorks.BlueprintPeerProtocol;`nnamespace OstrixMods.BuildWorks { internal sealed partial class BlueprintExchangeView {`n"+($methods -join "`n")+"`n} }`n"
[IO.File]::WriteAllText($target,$text,[Text.UTF8Encoding]::new($false))
