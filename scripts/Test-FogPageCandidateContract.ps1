[CmdletBinding()]
param([string]$CandidateDll)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -Path (Join-Path $root '../BepInEx/core/Mono.Cecil.dll')
function Types($type){$type; foreach($child in $type.NestedTypes){Types $child}}
$rows=@()
foreach($kind in @('normal','qa')+$(if($CandidateDll){@('candidate')}else{@()})){
    $path=switch($kind){'normal'{Join-Path $root 'artifacts/bin/BuildWorks.dll'}'qa'{Join-Path $root 'artifacts/test-stand-bin/BuildWorks.dll'}'candidate'{$CandidateDll}}
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($path))
    try {
        $types=@(Types ($assembly.MainModule.GetType('OstrixMods.BuildWorks.BlueprintFogChannels')))
        $methods=@($types | ForEach-Object {$_.Methods} | Where-Object HasBody)
        $fields=@($types | ForEach-Object {$_.Fields} | Where-Object Name -in @('velocityPages','coldLease','evictionLease'))
        $rents=@($methods | ForEach-Object {$_.Body.Instructions} | Where-Object {[string]$_.Operand -match 'FogVelocityPages::Rent'})
        $candidate=$kind -ceq 'candidate'
        if(($candidate -and ($fields.Count -ne 4 -or $rents.Count -ne 2)) -or (!$candidate -and ($fields.Count -ne 0 -or $rents.Count -ne 0))){throw ('Fog RAM candidate leaked into '+$kind+' or is missing from explicit candidate.')}
        if($candidate){
            $callback=@($methods | Where-Object Name -like '<RequestSnapshotVelocity>b__*')
            if($callback.Count -ne 1){throw 'Candidate callback is ambiguous.'}
            $instructions=@($callback[0].Body.Instructions)
            $guard=@($instructions | Where-Object {[string]$_.Operand -match '::CanCopyVelocity\('})
            $copy=@($instructions | Where-Object {[string]$_.Operand -match 'NativeArray.*::Copy\('})
            if($guard.Count -ne 1 -or $copy.Count -ne 1 -or $guard[0].Offset -ge $copy[0].Offset){throw 'Candidate custody guard must precede writing the array.'}
        }
        $rows+=@{kind=$kind;candidateEnabled=$candidate;leaseFields=$fields.Count;rentCalls=$rents.Count;sha256=(Get-FileHash -LiteralPath $path).Hash}
    } finally {$assembly.Dispose()}
}
@{passed=$true;compiledContractOnly=$true;nativeExecution=$false;builds=$rows} | ConvertTo-Json -Depth 4
