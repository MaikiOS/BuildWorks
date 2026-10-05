[CmdletBinding()]
param([string]$ZipPath, [string]$OutputPath, [switch]$SelfTest)
$ErrorActionPreference = 'Stop'

# Read data only. Never extract or execute a package, and never modify a game store.
function Read-BlueprintInventory([string]$Text) {
    $section = ''; $name = ''; $creator = ''; $category = ''
    $pieces = 0; $terrain = 0; $extended = 0
    $prefabs = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($line in ($Text -split '\r?\n')) {
        if ($line.StartsWith('#Name:')) { $name = $line.Substring(6).Trim(); continue }
        if ($line.StartsWith('#Creator:')) { $creator = $line.Substring(9).Trim(); continue }
        if ($line.StartsWith('#Category:')) { $category = $line.Substring(10).Trim(); continue }
        if ($line -eq '#Pieces' -or $line -eq '#Terrain') { $section = $line; continue }
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) { continue }
        if ($section -eq '#Terrain') { $terrain++; continue }
        if ($section -ne '#Pieces') { continue }
        $pieces++
        $columns = $line.Split(';')
        [void]$prefabs.Add($columns[0])
        if ($columns.Length -gt 13 -and ($columns[13..($columns.Length-1)] -join '').Trim()) { $extended++ }
    }
    [pscustomobject]@{ Name=$name; Creator=$creator; Category=$category; Pieces=$pieces;
        TerrainMarkers=$terrain; ExtendedRows=$extended; WithinCurrentSizeLimit=($pieces -ge 2 -and $pieces -le 128);
        Prefabs=@($prefabs | Sort-Object) }
}

if ($SelfTest) {
    $sample = Read-BlueprintInventory "#Name:Test`n#Creator:Builder`n#Terrain`nsquare;0`n#Pieces`nwood_wall;unused;0;0;0;0;0;0;1;info;1;1;1`nwood_wall;unused;1;0;0;0;0;0;1;info;1;1;1;zdo"
    if ($sample.Pieces -ne 2 -or $sample.TerrainMarkers -ne 1 -or $sample.Prefabs.Count -ne 1 -or
        $sample.ExtendedRows -ne 1 -or !$sample.WithinCurrentSizeLimit) { throw 'Blueprint inventory self-test failed.' }
    'PASS: read-only blueprint inventory boundaries.'
    return
}
if (!$ZipPath -or !$OutputPath) { throw 'Provide ZipPath and OutputPath, or SelfTest.' }
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead((Resolve-Path -LiteralPath $ZipPath).Path)
try {
    $entries = @($archive.Entries | Where-Object { $_.FullName.EndsWith('.blueprint', [StringComparison]::OrdinalIgnoreCase) })
    if ($entries.Count -gt 2000) { throw 'Package exceeds the inspection limit of 2000 blueprints.' }
    $inventory = foreach ($entry in $entries) {
        if ($entry.Length -gt 32MB) { throw "Blueprint too large to inspect: $($entry.FullName)" }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $record = Read-BlueprintInventory $reader.ReadToEnd() } finally { $reader.Dispose() }
        $record | Add-Member -NotePropertyName Entry -NotePropertyValue $entry.FullName
        $record
    }
    $inventory | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $OutputPath -Encoding utf8
    $small = @($inventory | Where-Object WithinCurrentSizeLimit | Sort-Object Pieces)
    "Read $($inventory.Count) blueprints; $($small.Count) meet only the 2-128-part size gate (not a compatibility guarantee)."
    $small | Select-Object -First 12 Name, Creator, Category, Pieces, TerrainMarkers, ExtendedRows | Format-Table -AutoSize
} finally { $archive.Dispose() }
