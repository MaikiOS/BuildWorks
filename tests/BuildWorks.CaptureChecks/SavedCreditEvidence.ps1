[CmdletBinding()]
param(
 [Parameter(Mandatory)][string]$Projects,
 [Parameter(Mandatory)][string]$Profile,
 [Parameter(Mandatory)][string]$Project,
 [Parameter(Mandatory)][long]$World,
 [Parameter(Mandatory)][string]$Name,
 [Parameter(Mandatory)][ValidateSet(0,1)][int]$Index,
 [string]$ReportPath=''
)
$ErrorActionPreference='Stop'
$taskExe=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../BuildWorks.StoreTests/bin/Release/net472/BuildWorks.StoreTests.exe'))
$taskGuid=[Guid]::ParseExact($Project,'N')
if($taskGuid -eq [Guid]::Empty){throw 'A real retained two-row project is required'}
$taskDirectory=Join-Path ([IO.Path]::GetTempPath()) ('BuildWorks.SavedCreditEvidence.'+[Guid]::NewGuid().ToString('N'))
$taskProfileBytes=[IO.File]::ReadAllBytes($Profile)
$taskOriginalProfileHash=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskProfileBytes))
$taskOriginals=@{}
foreach($taskFile in Get-ChildItem -LiteralPath $Projects -Filter ($Project+'.*') -File){
 if($taskFile.Extension -in @('.bwproject','.bwpart','.bwremoval','.bwpayout')){$taskOriginals[$taskFile.FullName]=[IO.File]::ReadAllBytes($taskFile.FullName)}
}
$taskChecks=0
function Invoke-Check([string]$Stage,[string]$Hash,[bool]$Pass,[string]$Label,[string]$PlayerName=$Name,[int]$Row=$Index){
 $taskOutput=& $taskExe --saved-credit-evidence $taskDirectory $World $Project (Join-Path $taskDirectory 'profile.fch') $PlayerName $Row $Stage $Hash 2>&1
 if(($LASTEXITCODE -eq 0) -ne $Pass){throw ("Unexpected result: $Label`n"+($taskOutput -join "`n"))}
 if($Pass -and !($taskOutput -match 'PASS saved credit pair:')){throw 'Missing positive evidence output'}
 $script:taskChecks++; "PASS check: $Label"
}
function Write-Payout($Record){
 $taskBytes=$taskEncode.MakeGenericMethod($taskRecordType).Invoke($null,@($Record))
 $taskStream=[IO.MemoryStream]::new();$taskWriter=[IO.BinaryWriter]::new($taskStream)
 try{$taskWriter.Write([Text.Encoding]::ASCII.GetBytes("BWPI01`r`n"));$taskWriter.Write([int]$taskBytes.Length);$taskWriter.Write([byte[]]$taskBytes);$taskWriter.Write([Security.Cryptography.SHA256]::HashData($taskBytes));$taskWriter.Flush();[IO.File]::WriteAllBytes($taskPayout,$taskStream.ToArray())}
 finally{$taskWriter.Dispose();$taskStream.Dispose()}
}
try{
 [IO.Directory]::CreateDirectory($taskDirectory)|Out-Null
 foreach($taskPath in $taskOriginals.Keys){[IO.File]::WriteAllBytes((Join-Path $taskDirectory ([IO.Path]::GetFileName($taskPath))),$taskOriginals[$taskPath])}
 [IO.File]::WriteAllBytes((Join-Path $taskDirectory 'profile.fch'),$taskProfileBytes)
 $taskPayloadLength=[BitConverter]::ToInt32($taskProfileBytes,0)
 $taskHash=[Convert]::ToHexString([Security.Cryptography.SHA512]::HashData($taskProfileBytes[4..(3+$taskPayloadLength)]))
 Invoke-Check credited $taskHash $true 'retained native profile: exact full pair and receipt'
 Invoke-Check intent $taskHash $false 'confirmed row cannot claim interrupted intent'
 Invoke-Check credited ('F'*128) $false 'wrong captured profile hash'
 Invoke-Check credited $taskHash $false 'wrong player name' ($Name+'_wrong')
 Invoke-Check credited $taskHash $false 'different row receipt cannot replace exact inventory' $Name (1-$Index)
 Invoke-Check credited $taskHash $false 'out-of-range credit row' $Name 65536
 $taskAssembly=[Reflection.Assembly]::LoadFrom($taskExe)
 $taskRecordType=$taskAssembly.GetType('OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal+Record',$true)
 $taskJournalType=$taskAssembly.GetType('OstrixMods.BuildWorks.BlueprintConstructionPayoutJournal',$true)
 $taskFlags=[Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
 $taskEncode=$taskJournalType.GetMethod('Encode',$taskFlags)
 $taskPayout=Join-Path $taskDirectory ($Project+'.bwpayout')
 $taskOriginalPayout=[IO.File]::ReadAllBytes($taskPayout)
 $taskSize=[BitConverter]::ToInt32($taskOriginalPayout,8)
 $taskJson=[IO.MemoryStream]::new($taskOriginalPayout[12..(11+$taskSize)])
 try{$taskSerializer=[Runtime.Serialization.Json.DataContractJsonSerializer]::new($taskRecordType);$taskRecord=$taskSerializer.ReadObject($taskJson)}finally{$taskJson.Dispose()}
 $taskRows=$taskRecordType.GetField('Collections',$taskFlags).GetValue($taskRecord)
 $taskRow=$taskRows[$Index];$taskRowType=$taskRow.GetType()
 $taskRemoval=$taskRowType.GetField('Removal',$taskFlags).GetValue($taskRow)
 $taskRevision=$taskRecordType.GetField('Revision',$taskFlags)
 $taskRevision.SetValue($taskRecord,[long]($taskRevision.GetValue($taskRecord)-1-$taskRemoval.GetType().GetField('Phase',$taskFlags).GetValue($taskRemoval)))
 $taskRowType.GetField('Phase',$taskFlags).SetValue($taskRow,1)
 $taskRowType.GetField('SavedProfileHash',$taskFlags).SetValue($taskRow,$null)
 $taskRowType.GetField('Removal',$taskFlags).SetValue($taskRow,$null)
 Write-Payout $taskRecord
 $taskIntentBytes=[IO.File]::ReadAllBytes($taskPayout)
 Invoke-Check intent $taskHash $true 'synthetic intent: saved exact pair survives unconfirmed journal'
 Invoke-Check credited $taskHash $false 'synthetic intent cannot claim confirmed stage'
 $taskCorrupt=[byte[]]$taskIntentBytes.Clone();$taskCorrupt[20]=$taskCorrupt[20] -bxor 1
 [IO.File]::WriteAllBytes($taskPayout,$taskCorrupt)
 Invoke-Check intent $taskHash $false 'corrupt payout checksum'
 [IO.File]::WriteAllBytes($taskPayout,$taskIntentBytes)
 $taskItem=$taskRecordType.GetField('Items',$taskFlags).GetValue($taskRecord)[$Index]
 $taskReceiptKey='ostrix.buildworks.collected.v1.'+$taskItem.GetType().GetField('Id',$taskFlags).GetValue($taskItem).ToString('N')
 $taskPayload=[byte[]]$taskProfileBytes[4..(3+$taskPayloadLength)]
 $taskLocation=[Text.Encoding]::Latin1.GetString($taskPayload).IndexOf($taskReceiptKey,[StringComparison]::Ordinal)
 if($taskLocation -lt 0){throw 'Native receipt fixture not found'}
 $taskPayload[$taskLocation]=[byte][char]'x'
 $taskChangedHash=[Security.Cryptography.SHA512]::HashData($taskPayload)
 [Array]::Copy($taskPayload,0,$taskProfileBytes,4,$taskPayload.Length);[Array]::Copy($taskChangedHash,0,$taskProfileBytes,$taskProfileBytes.Length-64,64)
 [IO.File]::WriteAllBytes((Join-Path $taskDirectory 'profile.fch'),$taskProfileBytes)
 Invoke-Check intent ([Convert]::ToHexString($taskChangedHash)) $false 'exact inventory without canonical receipt'
 $taskProfileBytes[20]=$taskProfileBytes[20] -bxor 1
 [IO.File]::WriteAllBytes((Join-Path $taskDirectory 'profile.fch'),$taskProfileBytes)
 Invoke-Check intent ([Convert]::ToHexString($taskChangedHash)) $false 'corrupt profile checksum'
 foreach($taskPath in $taskOriginals.Keys){if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($taskPath))) -cne [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskOriginals[$taskPath]))){throw 'Original project evidence changed'}}
 if([Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([IO.File]::ReadAllBytes($Profile))) -cne $taskOriginalProfileHash){throw 'Original profile evidence changed'}
 if($ReportPath){@{passed=$true;checks=$taskChecks;nativeRetainedPair=$true;interruptedIntentFixtureSynthetic=$true;gameLaunched=$false;nativeInterruptionProved=$false}|ConvertTo-Json|Set-Content -LiteralPath $ReportPath}
 "PASS saved credit evidence: $taskChecks checks; synthetic intent is not native interruption proof."
}finally{
 $taskResolved=[IO.Path]::GetFullPath($taskDirectory)
 $taskTemp=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
 if(!$taskResolved.StartsWith($taskTemp,[StringComparison]::OrdinalIgnoreCase) -or [IO.Path]::GetFileName($taskResolved) -notlike 'BuildWorks.SavedCreditEvidence.*'){throw 'Unsafe test cleanup path'}
 if([IO.Directory]::Exists($taskResolved)){[IO.Directory]::Delete($taskResolved,$true)}
}
