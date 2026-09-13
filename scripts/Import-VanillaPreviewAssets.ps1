param(
    [string]$ExportedProject = (Join-Path $PSScriptRoot '..\artifacts\valheim-unity-export\ExportedProject'),
    [string]$Target = (Join-Path $PSScriptRoot '..\tools\BuildWorks.UiWorkbench\Assets\LocalVanillaPreview')
)

$ErrorActionPreference = 'Stop'
$assets = Join-Path (Resolve-Path -LiteralPath $ExportedProject) 'Assets'
$targetPath = [IO.Path]::GetFullPath($Target)
if (Test-Path -LiteralPath $targetPath) {
    throw "Target already exists: $targetPath"
}

$rootAssets = @(
    'GameElements\Pieces\woodwall.prefab',
    'GameElements\Pieces\wood_beam.prefab',
    'GameElements\Pieces\wood_pole.prefab'
)
$allowed = [Collections.Generic.HashSet[string]]::new(
    [string[]]@('.asset', '.mat', '.png', '.tga', '.jpg', '.jpeg', '.dds',
        '.shader', '.shadergraph', '.fbx', '.obj'),
    [StringComparer]::OrdinalIgnoreCase)
$guidToAsset = @{}
Get-ChildItem -LiteralPath $assets -Filter '*.meta' -Recurse -File | ForEach-Object {
    $line = Select-String -LiteralPath $_.FullName -Pattern '^guid: ([0-9a-f]{32})$' |
        Select-Object -First 1
    if ($line) {
        $guidToAsset[$line.Matches[0].Groups[1].Value] =
            $_.FullName.Substring(0, $_.FullName.Length - 5)
    }
}

$roots = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$queue = [Collections.Generic.Queue[string]]::new()
foreach ($relative in $rootAssets) {
    $path = Join-Path $assets $relative
    if (-not (Test-Path -LiteralPath $path)) { throw "Missing vanilla prefab: $path" }
    $roots.Add([IO.Path]::GetFullPath($path)) | Out-Null
    $queue.Enqueue([IO.Path]::GetFullPath($path))
}

$seen = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
while ($queue.Count -gt 0) {
    $source = $queue.Dequeue()
    if (-not $seen.Add($source)) { continue }
    $relative = [IO.Path]::GetRelativePath($assets, $source)
    $destination = if ($roots.Contains($source)) {
        Join-Path $targetPath ('Resources\VanillaPreview\' + [IO.Path]::GetFileName($source))
    } else {
        Join-Path $targetPath ('Dependencies\' + $relative)
    }
    New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force |
        Out-Null
    Copy-Item -LiteralPath $source -Destination $destination
    Copy-Item -LiteralPath ($source + '.meta') -Destination ($destination + '.meta')

    if ([IO.Path]::GetExtension($source) -notin @('.prefab', '.asset', '.mat',
            '.shader', '.shadergraph')) { continue }
    $content = Get-Content -LiteralPath $source -Raw
    foreach ($match in [regex]::Matches($content, 'guid: ([0-9a-f]{32})')) {
        $dependency = $guidToAsset[$match.Groups[1].Value]
        if (-not $dependency) { continue }
        $extension = [IO.Path]::GetExtension($dependency)
        if ($extension -eq '.prefab' -or -not $allowed.Contains($extension)) { continue }
        $queue.Enqueue([IO.Path]::GetFullPath($dependency))
    }
}

Write-Host "Imported $($seen.Count) local vanilla preview assets into $targetPath"
