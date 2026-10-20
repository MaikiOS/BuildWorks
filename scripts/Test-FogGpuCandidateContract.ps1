[CmdletBinding()]
param([Parameter(Mandatory)][string]$BuildDirectory,[string]$BaselineDirectory)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -Path (Join-Path $root '../BepInEx/core/Mono.Cecil.dll')
function Types($type){$type;foreach($child in $type.NestedTypes){Types $child}}
function Code($method,[bool]$normalizeBudget=$false){
    (@($method.Body.Instructions|ForEach-Object {
        $line=$_.ToString() -replace '<>c__DisplayClass\d+_\d+','<>c__DisplayClass'
        if($normalizeBudget){$line=$line -replace '(ldc\.i[48] )(98304000|268435456)$','$1FIELD_BUDGET'}
        $line
    }) -join "`n")+
    (@($method.Body.ExceptionHandlers|ForEach-Object {'{0}:{1}:{2}:{3}:{4}:{5}' -f $_.HandlerType,$_.TryStart.Offset,$_.TryEnd.Offset,$_.HandlerStart.Offset,$_.HandlerEnd.Offset,$_.CatchType}) -join '|')
}
$rows=@()
foreach($kind in @('normal','qa','candidate')){
    $path=Join-Path $BuildDirectory ($kind+'/BuildWorks.dll')
    $assembly=[Mono.Cecil.AssemblyDefinition]::ReadAssembly([IO.Path]::GetFullPath($path))
    $baselinePath=if($BaselineDirectory){Join-Path $BaselineDirectory $(if($kind -eq 'normal'){'normal/BuildWorks.dll'}else{'qa/BuildWorks.dll'})}
        else{Join-Path $root $(if($kind -eq 'normal'){'artifacts/bin/BuildWorks.dll'}else{'artifacts/test-stand-bin/BuildWorks.dll'})}
    $baseline=[Mono.Cecil.AssemblyDefinition]::ReadAssembly($baselinePath)
    try {
        $arena=$assembly.MainModule.GetType('OstrixMods.BuildWorks.BlueprintFogArena')
        $channels=$assembly.MainModule.GetType('OstrixMods.BuildWorks.BlueprintFogChannels')
        $expected=if($kind -eq 'normal'){98304000L}else{268435456L}
        foreach($pair in @(@($arena,'MaximumFieldBytes'),@($channels,'MaximumFieldBudget'))){
            $field=@($pair[0].Fields|Where-Object Name -CEQ $pair[1])
            if($field.Count -ne 1 -or !$field[0].HasConstant -or $field[0].Constant -ne $expected){throw ('Wrong compiled cap: '+$kind+'/'+$pair[1])}
        }
        $compared=0
        foreach($typeName in @('BlueprintFogSimulation','BlueprintFogSnapshotState','BlueprintFogInputQueue','BlueprintFogChannels','BlueprintFogArena','BlueprintFogAtlas')){
            $type=$assembly.MainModule.GetType('OstrixMods.BuildWorks.'+$typeName)
            $oldType=$baseline.MainModule.GetType('OstrixMods.BuildWorks.'+$typeName)
            $types=@(Types $type); $oldTypes=@(Types $oldType)
            if((($types|ForEach-Object FullName|Sort-Object)-join '|') -cne (($oldTypes|ForEach-Object FullName|Sort-Object)-join '|')){throw ('Compiled type set differs: '+$typeName)}
            foreach($t in @(Types $type)){
                $old=@(Types $oldType|Where-Object FullName -CEQ $t.FullName)
                if($old.Count -ne 1){throw ('Baseline type differs: '+$t.FullName)}
                if((($t.Methods|ForEach-Object FullName|Sort-Object)-join '|') -cne (($old[0].Methods|ForEach-Object FullName|Sort-Object)-join '|')){throw ('Compiled method set differs: '+$t.FullName)}
                foreach($method in @($t.Methods|Where-Object HasBody)){
                    $budget=$kind -ne 'normal' -and (($typeName -eq 'BlueprintFogChannels' -and $method.Name -in @('.ctor','CreateArena')) -or
                        ($typeName -eq 'BlueprintFogArena' -and $method.Name -eq '.ctor'))
                    $prior=@($old[0].Methods|Where-Object FullName -CEQ $method.FullName)
                    if($prior.Count -ne 1 -or (Code $method $budget) -cne (Code $prior[0] $budget)){throw ('Executable instructions/handlers changed: '+$kind+'/'+$method.FullName)}
                    ++$compared
                }
            }
        }
        $rows+=@{kind=$kind;fieldCap=$expected;unchangedMethodsAndHandlers=$compared;sha256=(Get-FileHash -LiteralPath $path).Hash;
            baselineSha256=(Get-FileHash -LiteralPath $baselinePath).Hash;baselineVersion=$baseline.Name.Version.ToString()}
    } finally {$assembly.Dispose();$baseline.Dispose()}
}
@{passed=$true;compiledContractOnly=$true;gpuExecution=$false;builds=$rows}|ConvertTo-Json -Depth 5
