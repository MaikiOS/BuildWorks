param([string]$BuildWorksRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$source = [IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/BuildWorksTestStorage.Lookup.cs'))
$types = $source.IndexOf('        [Serializable] internal sealed class StorageLookupResult', [StringComparison]::Ordinal)
$entry = $source.IndexOf('        internal static StorageLookupResult LookupStorageMarker(', [StringComparison]::Ordinal)
$scan = $source.IndexOf('        // Registry identity is diagnostic evidence only;', [StringComparison]::Ordinal)
$end = $source.LastIndexOf("    }`n}`n#endif", [StringComparison]::Ordinal)
if ($types -lt 0 -or $entry -le $types -or $scan -le $entry -or $end -le $scan) { throw 'Storage lookup source boundaries changed.' }
$harness = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'Harness.cs'))
$controlSource = [IO.File]::ReadAllText((Join-Path $BuildWorksRoot 'src/BuildWorks/BuildWorksQaControl.cs')).Replace("`r`n", "`n")
$admissionStart = $controlSource.IndexOf('        internal void RequireStorageIdle()', [StringComparison]::Ordinal)
$admissionEnd = $controlSource.IndexOf('        public string LookupStorageMarker(', $admissionStart, [StringComparison]::Ordinal)
if ($admissionStart -lt 0 -or $admissionEnd -le $admissionStart) { throw 'Storage admission source boundaries changed.' }
$code = $harness.Replace('// PRODUCTION_TYPES', $source.Substring($types, $entry - $types)).Replace('// PRODUCTION_CONTEXT', $source.Substring($entry, $scan - $entry)).Replace('// PRODUCTION_SCAN', $source.Substring($scan, $end - $scan)).Replace('// PRODUCTION_ADMISSION', $controlSource.Substring($admissionStart, $admissionEnd - $admissionStart))
Add-Type -TypeDefinition $code
[BuildWorksStorageLookupFixtures.StorageLookupChecks]::Run()
foreach ($control in @(
    @{ Name='Schema'; Before='saved.Schema != 4'; After='4 != 4'; Error='Invalid WAL/context accepted: schema'; Method='Run' },
    @{ Name='Sharing'; Before='FileAccess.Read, FileShare.Read)'; After='FileAccess.Read, FileShare.ReadWrite)'; Error='Read lock allowed a concurrent writer'; Method='Run' },
    @{ Name='Thread'; Before='System.Threading.Thread.CurrentThread.ManagedThreadId != ownerThread'; After='false'; Error='Admission accepted thread'; Method='RunAdmission' },
    @{ Name='Emergency'; Before='UnityEngine.Input.GetKey(KeyCode.F12)'; After='false'; Error='Admission accepted F12'; Method='RunAdmission' }
)) {
    if (-not $code.Contains($control.Before)) { throw "Missing lookup negative control: $($control.Name)" }
    $namespace = 'BuildWorksStorageLookupFixtures' + $control.Name
    $broken = $code.Replace('BuildWorksStorageLookupFixtures', $namespace).Replace($control.Before, $control.After)
    Add-Type -TypeDefinition $broken
    $type = ($namespace + '.StorageLookupChecks') -as [type]
    $rejected = $false
    try { $type.GetMethod($control.Method).Invoke($null, @()) }
    catch { if ($_.Exception.GetBaseException().Message -cne $control.Error) { throw }; $rejected = $true }
    if (-not $rejected) { throw "Lookup checks accepted broken $($control.Name) guard" }
}
'PASS lookup test controls: bypassed schema/thread/F12 and weakened sharing rejected; changes compiled in memory only.'
