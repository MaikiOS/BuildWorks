param([switch]$Stress)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$directory = Join-Path $root ('artifacts/qa-client-read-checks/' + [guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($directory) | Out-Null
$tokens = $null; $errorsFound = $null
$ast = [Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'scripts/Invoke-QAControl.ps1'), [ref]$tokens, [ref]$errorsFound)
if ($errorsFound.Count) { throw 'QA client has parse errors.' }
$function = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Read-ControlJson' }, $true)
. ([scriptblock]::Create($function.Extent.Text))
$publisher = [IO.File]::ReadAllText((Join-Path $root 'src/BuildWorks/BuildWorksQaFilePublisher.cs'))
Add-Type -CompilerOptions '/define:BUILDWORKS_QA_CONTROL_TESTS' -TypeDefinition ($publisher + @'

public static class QaPublisherHarness {
    public static int Replace(string source, string target) {
        return OstrixMods.BuildWorks.BuildWorksQaFilePublisher.ReplaceObservation(source, target, () => {});
    }
}
'@)
function Require([bool]$condition, [string]$reason) { if (-not $condition) { throw $reason } }
function Refuses([scriptblock]$action, [string]$reason) {
    try { & $action | Out-Null } catch { return }
    throw $reason
}
$path = Join-Path $directory 'response.json'
[IO.File]::WriteAllText($path, '{"value":17}')
Require ((Read-ControlJson 'response.json').value -eq 17) 'Ordinary reply failed.'
[IO.File]::WriteAllText($path, '{"value":17}' + (' ' * (32768 - 12)))
Require ((Read-ControlJson 'response.json').value -eq 17) '32768-byte reply refused.'
[IO.File]::AppendAllText($path, ' ')
Refuses { Read-ControlJson 'response.json' } 'Oversized reply accepted.'
[IO.File]::WriteAllBytes($path, [byte[]]@(255))
Refuses { Read-ControlJson 'response.json' } 'Invalid UTF8 accepted.'
[IO.File]::WriteAllText($path, '{invalid')
Refuses { Read-ControlJson 'response.json' } 'Invalid JSON accepted.'
[IO.File]::WriteAllText($path, '{"value":17}')
$locked = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
$ready = [Threading.ManualResetEventSlim]::new($false)
$worker = [PowerShell]::Create()
[void]$worker.AddScript('param($handle,$ready) $ready.Set(); Start-Sleep -Milliseconds 25; $handle.Dispose()').AddArgument($locked).AddArgument($ready)
$async = $worker.BeginInvoke()
try {
    Require ($ready.Wait(2000)) 'Transient-lock worker did not start.'
    Require ((Read-ControlJson 'response.json').value -eq 17) 'Transient Windows sharing violation was not retried.'
    [void]$worker.EndInvoke($async)
    Require (-not $worker.HadErrors) 'Transient-lock worker failed.'
} finally { $locked.Dispose(); $worker.Dispose(); $ready.Dispose() }
$locked = [IO.File]::Open($path, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
try { Refuses { Read-ControlJson 'response.json' } 'Persistent lock did not terminate.' }
finally { $locked.Dispose() }
[IO.File]::Delete($path)
$ready = [Threading.ManualResetEventSlim]::new($false)
$worker = [PowerShell]::Create()
[void]$worker.AddScript('param($path,$ready) $ErrorActionPreference="Stop"; [IO.File]::WriteAllText($path+".tmp","{`"value`":17}"); $ready.Set(); Start-Sleep -Milliseconds 25; [IO.File]::Move($path+".tmp",$path)').AddArgument($path).AddArgument($ready)
$async = $worker.BeginInvoke()
try {
    Require ($ready.Wait(2000)) 'Missing-file worker did not start.'
    Require ((Read-ControlJson 'response.json').value -eq 17) 'Transient missing reply was not retried.'
    [void]$worker.EndInvoke($async)
    Require (-not $worker.HadErrors) 'Missing-file worker failed.'
} finally { $worker.Dispose(); $ready.Dispose() }
[IO.File]::Delete($path)
Refuses { Read-ControlJson 'response.json' } 'Permanently missing reply did not terminate.'
[IO.File]::WriteAllText($path, '{"value":17}')
$worker = [PowerShell]::Create()
$ready = [Threading.ManualResetEventSlim]::new($false)
$interval = if ($Stress) { 1 } else { 50 } # Match the bridge's 50ms poll; keep the reproduced stress case explicit.
[void]$worker.AddScript('param($path,$ready,$interval) $ErrorActionPreference="Stop"; $retries=0; $ready.Set(); for($i=0;$i -lt 100;$i++){ $temporary=$path+".tmp"; [IO.File]::WriteAllText($temporary,"{`"value`":17}"); $retries += [QaPublisherHarness]::Replace($temporary,$path); Start-Sleep -Milliseconds $interval }; "Publisher recovered retries: $retries"').AddArgument($path).AddArgument($ready).AddArgument($interval)
$async = $worker.BeginInvoke()
try {
    Require ($ready.Wait(2000)) 'Atomic publisher did not start.'
    for ($index = 0; $index -lt 100; $index++) {
        Require ((Read-ControlJson 'response.json').value -eq 17) 'Concurrent atomic reply replacement failed.'
        Start-Sleep -Milliseconds $interval
    }
    $worker.EndInvoke($async)
    Require (-not $worker.HadErrors) ('Atomic publisher failed while the client read: ' + ($worker.Streams.Error | Out-String))
} finally { $worker.Dispose(); $ready.Dispose() }
"PASS QA client reads: actual helper, bounded bytes, strict UTF8/JSON, transient/persistent lock and missing file,100 concurrent atomic replacements at ${interval}ms interval; no live channel request or game input."
