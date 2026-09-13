[CmdletBinding()]
param(
    [string] $ValheimManagedDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim\valheim_Data\Managed'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$profileRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Add-Type -Path (Join-Path $profileRoot 'BepInEx\core\Mono.Cecil.dll')
$assembly = [Mono.Cecil.AssemblyDefinition]::ReadAssembly(
    (Join-Path $ValheimManagedDir 'assembly_valheim.dll'))

foreach ($typeName in @('Piece', 'Player', 'PieceTable')) {
    $type = $assembly.MainModule.Types | Where-Object FullName -eq $typeName
    Write-Output "TYPE $typeName"
    $type.Fields |
        Where-Object Name -Match 'snap|place|attach|connect|piece|prefab' |
        ForEach-Object { Write-Output "FIELD $($_.FieldType.FullName) $($_.Name)" }
    $type.Methods |
        Where-Object Name -Match 'snap|place|attach|connect|piece|prefab' |
        ForEach-Object {
            $parameters = ($_.Parameters | ForEach-Object { $_.ParameterType.FullName }) -join ', '
            Write-Output "METHOD $($_.ReturnType.FullName) $($_.Name)($parameters)"
        }
}

foreach ($methodName in @(
    'GetSnapPoints',
    'FindClosestSnapPoints',
    'FindClosestSnappoint')) {
    $methods = $assembly.MainModule.Types.Methods | Where-Object Name -eq $methodName
    foreach ($method in $methods) {
        Write-Output "IL $($method.DeclaringType.FullName)::$($method.Name)"
        foreach ($instruction in $method.Body.Instructions) {
            Write-Output ("{0}: {1} {2}" -f
                $instruction.Offset,
                $instruction.OpCode.Name,
                [string]$instruction.Operand)
        }
    }
}

$update = $assembly.MainModule.Types.Methods |
    Where-Object { $_.DeclaringType.FullName -eq 'Player' -and $_.Name -eq 'UpdatePlacementGhost' }
$updateInstructions = @($update.Body.Instructions)
for ($index = 0; $index -lt $updateInstructions.Count; ++$index) {
    $operand = [string]$updateInstructions[$index].Operand
    if ($operand -notmatch 'PieceRayTest|FindClosestSnapPoints|Transform::set_position') { continue }
    Write-Output "IL Player::UpdatePlacementGhost around $operand"
    $start = [Math]::Max(0, $index - 24)
    $end = [Math]::Min($updateInstructions.Count - 1, $index + 32)
    for ($near = $start; $near -le $end; ++$near) {
        $instruction = $updateInstructions[$near]
        Write-Output ("{0}: {1} {2}" -f $instruction.Offset, $instruction.OpCode.Name, [string]$instruction.Operand)
    }
}
Write-Output 'IL Player::UpdatePlacementGhost placement-frame section'
foreach ($instruction in $updateInstructions) {
    if ($instruction.Offset -ge 1500 -and $instruction.Offset -le 2380) {
        Write-Output ("{0}: {1} {2}" -f $instruction.Offset, $instruction.OpCode.Name, [string]$instruction.Operand)
    }
}
