param([ValidateSet('Bbp', 'Planner')][string] $Family = 'Bbp')
$ErrorActionPreference = 'Stop'
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
Add-Type -Path (Join-Path (Split-Path $root) 'BepInEx/core/Mono.Cecil.dll')
$normal = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'artifacts/bin/BuildWorks.dll'))
$qa = [Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $root 'artifacts/test-stand-bin/BuildWorks.dll'))
function Assert-ReadWindow($method) {
    $calls = @($method.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] })
    $loads = @($calls | Where-Object { $_.Operand.DeclaringType.Name -eq 'ExternalBlueprintLibrary' -and $_.Operand.Name -eq 'LoadFile' })
    $saves = @($calls | Where-Object { $_.Operand.DeclaringType.Name -eq 'Inventory' -and $_.Operand.Name -eq 'Save' })
    if ($loads.Count -ne 2 -or $saves.Count -ne 2) { throw "$Family check needs both file reads and two inventory snapshots." }
    if ($saves[0].Offset -ge $loads[0].Offset -or $saves[1].Offset -le $loads[1].Offset) {
        throw "$Family inventory snapshots do not bracket both file reads."
    }
    foreach ($guard in @('RequireLoadedSession', 'RequireQaStorageIdle')) {
        $guardCalls = @($calls | Where-Object { $_.Operand.Name -eq $guard })
        if ($guardCalls.Count -ne 1 -or $guardCalls[0].Offset -ge $saves[0].Offset) { throw "$Family isolation/idle guard missing before snapshots." }
    }
    if ($method.ReturnType.FullName -ne 'System.String' -or $method.Parameters.Count -ne 1 -or
        $method.Parameters[0].ParameterType.FullName -ne 'System.String') { throw "$Family helper is not scalar Check(string)." }
    $forbidden = @($calls | Where-Object { $_.Operand.Name -in @('TryWrite','WriteAllText','WriteAllBytes','Instantiate','TeleportTo','AddItem','RemoveItem','ConsumeResources','TryPlacePiece') })
    if ($forbidden.Count -ne 0) { throw "$Family readonly check gained a direct write/world operation." }
}
try {
    $name = "OstrixMods.BuildWorks.BuildWorksTest${Family}Blueprint"
    if ($normal.MainModule.GetType($name)) { throw "Normal build exports $Family QA helper." }
    $type = $qa.MainModule.GetType($name)
    if (-not $type) { throw "QA build lacks $Family helper." }
    $check = @($type.Methods | Where-Object Name -eq 'Check')[0]
    Assert-ReadWindow $check
    foreach ($target in @('LoadFile','Save')) {
        $call = $check.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $target } | Select-Object -Last 1
        $code = $call.OpCode; $operand = $call.Operand
        try {
            $call.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $call.Operand = $null
            $rejected = $false
            try { Assert-ReadWindow $check }
            catch { if ($_.Exception.Message -ne "$Family check needs both file reads and two inventory snapshots.") { throw }; $rejected = $true }
            if (-not $rejected) { throw "$Family read window accepted disconnected $target." }
        } finally { $call.OpCode = $code; $call.Operand = $operand }
    }
    foreach ($guard in @('RequireLoadedSession', 'RequireQaStorageIdle')) {
        $call = $check.Body.Instructions | Where-Object { $_.Operand -is [Mono.Cecil.MethodReference] -and $_.Operand.Name -eq $guard }
        $code = $call.OpCode; $operand = $call.Operand
        try {
            $call.OpCode = [Mono.Cecil.Cil.OpCodes]::Nop; $call.Operand = $null
            $rejected = $false
            try { Assert-ReadWindow $check }
            catch { if ($_.Exception.Message -ne "$Family isolation/idle guard missing before snapshots.") { throw }; $rejected = $true }
            if (-not $rejected) { throw "$Family accepted disconnected isolation/idle guard." }
        } finally { $call.OpCode = $code; $call.Operand = $operand }
    }
    "PASS $Family readonly window: compiled inventory snapshots bracket both file reads; missing final read/snapshot rejected in memory. No game or full player-state proof."
} finally { $normal.Dispose(); $qa.Dispose() }
