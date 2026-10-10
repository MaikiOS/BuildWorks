[CmdletBinding()]
param(
    [ValidateSet('state', 'batch')][string] $Action = 'state',
    [string] $StepsJson,
    [switch] $Capture,
    [switch] $Stop,
    [ValidateRange(1, 25)][int] $WaitSeconds = 15
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$profiles = Split-Path (Split-Path ([IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))))
$config = Join-Path $profiles 'TerrainRamp-1.0-Test/BepInEx/config/BuildWorks'
$token = [IO.File]::ReadAllText((Join-Path $config 'qa-isolation.token')).Trim()
$guid = [guid]::Empty
if (-not [guid]::TryParseExact($token, 'N', [ref]$guid) -or $guid -eq [guid]::Empty) { throw 'Invalid QA isolation token.' }
$directory = Join-Path $config "qa-saves/$token/qa-control"
for ($parent = [IO.DirectoryInfo]::new($directory); $null -ne $parent; $parent = $parent.Parent) {
    if ($parent.Exists -and ($parent.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "QA path is a reparse point: $($parent.FullName)" }
}
function Read-ControlJson([string] $name) {
    $path = Join-Path $directory $name
    $file = Get-Item -LiteralPath $path
    if ($file.Length -gt 32768 -or ($file.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Invalid QA file: $name" }
    [IO.File]::ReadAllText($path) | ConvertFrom-Json
}
$session = Read-ControlJson 'session.json'
$process = Get-Process -Id $session.processId -ErrorAction Stop
if ($process.ProcessName -ine 'valheim' -or $session.version -ne 1 -or $session.nonce -notmatch '^[0-9a-f]{32}$') {
    throw 'QA bridge session is not attached to a live Valheim process.'
}
if ($Stop) {
    $stopPath = Join-Path $directory 'stop.flag'
    if (Test-Path -LiteralPath $stopPath) { throw 'A stop request is already pending.' }
    $stream = [IO.File]::Open($stopPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    $stream.Dispose()
    'Emergency stop queued. Inspect response.json for confirmation.'
    return
}
$id = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$request = [ordered]@{ version = 1; nonce = $session.nonce; id = $id; expiresUtc = $id + 15000; action = $Action; capture = [bool]$Capture }
if ($Action -eq 'batch') {
    if (-not $StepsJson) { throw 'batch requires -StepsJson, a JSON array of bounded input steps.' }
    $request.steps = @($StepsJson | ConvertFrom-Json)
} elseif ($StepsJson) { throw 'state does not accept input steps.' }
$bytes = [Text.UTF8Encoding]::new($false).GetBytes(($request | ConvertTo-Json -Depth 8 -Compress))
if ($bytes.Length -gt 32768) { throw 'QA request exceeds 32768 bytes.' }
$pending = Join-Path $directory 'request.json'
$temporary = Join-Path $directory ('request-' + [guid]::NewGuid().ToString('N') + '.tmp')
try {
    $stream = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
    try { $stream.Write($bytes, 0, $bytes.Length); $stream.Flush($true) } finally { $stream.Dispose() }
    [IO.File]::Move($temporary, $pending)
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary } }
$deadline = [DateTime]::UtcNow.AddSeconds($WaitSeconds)
do {
    Start-Sleep -Milliseconds 100
    $current = Read-ControlJson 'session.json'
    if ($current.nonce -ne $session.nonce) { throw 'QA session changed; this request must not be retried automatically.' }
    $reply = Read-ControlJson 'response.json'
    if ($reply.nonce -eq $session.nonce -and $reply.status -eq 'rejected' -and $reply.utc -ge $id) {
        throw "QA bridge rejected the request: $($reply.detail)"
    }
    if ($reply.nonce -eq $session.nonce -and $reply.id -eq $id -and $reply.status -ne 'running') {
        $reply | ConvertTo-Json -Depth 5
        if ($reply.screenshot) { "Screenshot: $(Join-Path $directory $reply.screenshot)" }
        return
    }
} while ([DateTime]::UtcNow -lt $deadline)
throw "Observation timeout for QA request $id. Execution state is unknown; inspect response.json or send -Stop. Do not replay the request."
