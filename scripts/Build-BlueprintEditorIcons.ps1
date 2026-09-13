[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$sourceRoot = Join-Path $projectRoot 'assets\blueprint-editor\icons\source'
$outputRoot = Join-Path $projectRoot 'src\BuildWorks\Assets\BlueprintEditorIcons'
$magick = Get-Command magick -ErrorAction Stop

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

$sources = Get-ChildItem -LiteralPath $sourceRoot -Filter '*.svg' -File | Sort-Object Name
if ($sources.Count -eq 0) {
    throw "No Blueprint Editor icon sources found in $sourceRoot"
}

$outputs = @()
foreach ($source in $sources) {
    $destination = Join-Path $outputRoot ($source.BaseName + '.png')
    & $magick.Source -background none -density 384 $source.FullName -resize '128x128' -strip "PNG32:$destination"
    if ($LASTEXITCODE -ne 0) {
        throw "ImageMagick failed for $($source.Name)"
    }

    $geometry = & $magick.Source identify -format '%wx%h %[channels]' $destination
    if ($LASTEXITCODE -ne 0 -or $geometry -notmatch '^128x128 .*a') {
        throw "Invalid icon output $destination ($geometry)"
    }
    $outputs += $destination
}

$sheetPath = Join-Path $projectRoot 'specs\blueprint-editor-icon-sheet-v1.png'
$montageArguments = @(
    'montage', '-background', '#202830', '-fill', '#F2D79B',
    '-font', 'Arial', '-pointsize', '14', '-label', '%t'
) + $outputs + @('-tile', '5x5', '-geometry', '128x128+16+32', $sheetPath)
& $magick.Source @montageArguments
if ($LASTEXITCODE -ne 0) {
    throw 'ImageMagick failed to build the Blueprint Editor icon sheet.'
}

Write-Host "PASS: generated $($sources.Count) transparent Blueprint Editor icons."
