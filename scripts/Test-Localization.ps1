[CmdletBinding()]
param(
    [string] $AssemblyPath,
    [switch] $RequireBuiltAssembly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$sourceRoot = Join-Path $projectRoot 'src\BuildWorks'
$allSourceRoot = Join-Path $projectRoot 'src'
$englishPath = Join-Path $sourceRoot 'Translations\English.tsv'
$russianPath = Join-Path $sourceRoot 'Translations\Russian.tsv'

function Read-Catalog([string] $path) {
    $catalog = [ordered]@{}
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadAllLines($path)) {
        $lineNumber++
        if ($line.Length -eq 0 -or $line[0] -eq '#') { continue }
        $separator = $line.IndexOf("`t")
        if ($separator -lt 1) { throw "$path`:$lineNumber has no tab separator." }
        $key = $line.Substring(0, $separator)
        $value = $line.Substring($separator + 1)
        if ($catalog.Contains($key)) { throw "$path`:$lineNumber duplicates '$key'." }
        if ([string]::IsNullOrWhiteSpace($value)) { throw "$path`:$lineNumber has an empty value." }
        if ($value.Contains("`t")) { throw "$path`:$lineNumber contains an unescaped tab in its value." }
        $catalog[$key] = $value
    }
    return $catalog
}

function Placeholder-Set([string] $text) {
    return @([regex]::Matches($text, '\{\d+(?:[^}]*)?\}') |
        ForEach-Object Value | Sort-Object -Unique)
}

$english = Read-Catalog $englishPath
$russian = Read-Catalog $russianPath

$missingRussian = @($english.Keys | Where-Object { -not $russian.Contains($_) })
$missingEnglish = @($russian.Keys | Where-Object { -not $english.Contains($_) })
if ($missingRussian.Count -gt 0 -or $missingEnglish.Count -gt 0) {
    throw "Catalog key mismatch. Missing RU: $($missingRussian -join ', '); missing EN: $($missingEnglish -join ', ')."
}

foreach ($key in $english.Keys) {
    if ($key -notmatch '^[a-z0-9_]+(?:\.[a-z0-9_-]+)+$') {
        throw "Localization key '$key' is not a stable lower-case dotted identifier."
    }
    if ($english[$key] -match '[А-Яа-яЁё]') {
        throw "English translation '$key' contains Cyrillic text."
    }
    $englishPlaceholders = (Placeholder-Set $english[$key]) -join ','
    $russianPlaceholders = (Placeholder-Set $russian[$key]) -join ','
    if ($englishPlaceholders -ne $russianPlaceholders) {
        throw "Placeholder mismatch for '$key': EN [$englishPlaceholders], RU [$russianPlaceholders]."
    }
}

$runtimeKeys = @{}
foreach ($key in $english.Keys) {
    $runtimeKey = 'buildworks_' + $key.Replace('.', '_')
    if ($runtimeKey -notmatch '^[a-z0-9_]+$') {
        throw "Valheim runtime localization key '$runtimeKey' contains an unsupported character."
    }
    if ($runtimeKeys.ContainsKey($runtimeKey)) {
        throw "Valheim runtime localization collision: '$($runtimeKeys[$runtimeKey])' and '$key' both map to '$runtimeKey'."
    }
    $runtimeKeys[$runtimeKey] = $key
}

$localizationSourcePath = Join-Path $sourceRoot 'BuildWorksLocalization.cs'
$localizationSource = [IO.File]::ReadAllText($localizationSourcePath)
if ($localizationSource -notmatch
    'Token\(string key\) => "\$" \+ RuntimeKey\(key\)' -or
    $localizationSource -notmatch
    'RuntimeKey\(entry\.Key\)') {
    throw 'Production localization no longer routes tokens and registrations through one Valheim-safe runtime key.'
}

$sourceFiles = @(Get-ChildItem $allSourceRoot -Recurse -Filter '*.cs' -File |
    Where-Object { $_.FullName -notmatch '\\(?:bin|obj)\\' })
$sourceText = ($sourceFiles | ForEach-Object { [IO.File]::ReadAllText($_.FullName) }) -join "`n"
$nonLocalizationDottedLiterals = @(
    'blueprints.json',
    'catalog.group.',
    'com.jotunn.jotunn',
    'ostrmod.buildworks'
)
$keyPattern = '"((?:[a-z][a-z0-9_-]*\.)+[A-Za-z0-9_.-]+)"'
$usedKeys = @([regex]::Matches($sourceText, $keyPattern) |
    ForEach-Object { $_.Groups[1].Value } |
    Where-Object { $_ -notin $nonLocalizationDottedLiterals } |
    Sort-Object -Unique)
$missingRuntimeKeys = @($usedKeys | Where-Object { -not $english.Contains($_) })
if ($missingRuntimeKeys.Count -gt 0) {
    throw "Runtime keys are missing from the catalogs: $($missingRuntimeKeys -join ', ')."
}

$legacyCategoryLine = 'private const string LegacyDefaultCategory = "ПРОЧЕЕ";'
foreach ($file in $sourceFiles) {
    $lineNumber = 0
    foreach ($line in [IO.File]::ReadAllLines($file.FullName)) {
        $lineNumber++
        if ($line -notmatch '[А-Яа-яЁё]') { continue }
        if ($file.Name -eq 'CompositeBlueprintStore.cs' -and
            $line.Trim() -eq $legacyCategoryLine) { continue }
        throw "$($file.FullName)`:$lineNumber contains embedded Cyrillic runtime text."
    }
}

$projectFile = [IO.File]::ReadAllText((Join-Path $sourceRoot 'BuildWorks.csproj'))
if ($projectFile -notmatch '<EmbeddedResource Include="Translations\\\*\.tsv"') {
    throw 'BuildWorks.csproj does not embed the translation catalogs.'
}

if ([string]::IsNullOrWhiteSpace($AssemblyPath)) {
    $AssemblyPath = Join-Path $projectRoot 'artifacts\bin\BuildWorks.dll'
}
if (Test-Path -LiteralPath $AssemblyPath -PathType Leaf) {
    $assembly = [Reflection.Assembly]::LoadFile([IO.Path]::GetFullPath($AssemblyPath))
    $resourceNames = @($assembly.GetManifestResourceNames())
    $catalogResources = @{
        'Translations.English.tsv' = $englishPath
        'Translations.Russian.tsv' = $russianPath
    }
    foreach ($suffix in $catalogResources.Keys) {
        $resourceName = $resourceNames | Where-Object {
            $_.EndsWith($suffix, [StringComparison]::Ordinal)
        } | Select-Object -First 1
        if (-not $resourceName) {
            throw "$AssemblyPath does not embed a resource ending in '$suffix'."
        }
        $stream = $assembly.GetManifestResourceStream($resourceName)
        try {
            $resourceHash = (Get-FileHash -InputStream $stream -Algorithm SHA256).Hash
        }
        finally {
            if ($null -ne $stream) { $stream.Dispose() }
        }
        $sourceHash = (Get-FileHash -LiteralPath $catalogResources[$suffix] -Algorithm SHA256).Hash
        if ($resourceHash -ne $sourceHash) {
            throw "$AssemblyPath embeds a stale '$suffix' catalog."
        }
    }
}
elseif ($RequireBuiltAssembly) {
    throw "Built assembly not found: $AssemblyPath"
}

$assemblyStatus = if (Test-Path -LiteralPath $AssemblyPath -PathType Leaf) {
    '; built DLL resources verified'
} else {
    '; source-only check'
}
Write-Output "PASS: localization catalogs match ($($english.Count) keys; $($runtimeKeys.Count) unique Valheim-safe runtime tokens; $($usedKeys.Count) statically referenced runtime keys$assemblyStatus)."
