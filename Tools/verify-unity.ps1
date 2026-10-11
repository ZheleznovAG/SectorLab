param(
    [ValidateSet('EditMode', 'PlayMode')] [string] $Mode = 'EditMode',
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Task
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Invoke-Editor([string[]] $Arguments) {
    $connectionTimer = [Diagnostics.Stopwatch]::StartNew()
    do {
        $raw = & unity command --json --no-banner --timeout 15 @Arguments
        $commandExit = $LASTEXITCODE
        if ($commandExit -eq 0) { break }
        # A domain reload temporarily removes the Pipeline endpoint. Retry only read-only polling.
        if ($Arguments[0] -notin @('console_status', 'test_status') -or $connectionTimer.Elapsed.TotalSeconds -gt 60) {
            throw ('Unity CLI failed: ' + ($Arguments -join ' ') + "`n" + ($raw -join "`n"))
        }
        Write-Output ('Waiting for Unity polling endpoint after domain reload: ' + $Arguments[0]) | Out-Host
        Start-Sleep -Seconds 2
    } while ($true)
    $response = ($raw -join "`n") | ConvertFrom-Json
    if (-not $response.success -or -not $response.data.success) { throw ($raw -join "`n") }
    return $response
}

$sourceHead = (& git rev-parse HEAD).Trim()
$started = [DateTime]::UtcNow.ToString('o')
$inputHashes = @(rg --files Assets/_Project Packages ProjectSettings | Sort-Object | ForEach-Object {
    [PSCustomObject]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})
$null = Invoke-Editor @('eval', '--code', 'UnityEditor.AssetDatabase.Refresh();')
$timer = [Diagnostics.Stopwatch]::StartNew()
do {
    $console = Invoke-Editor @('console_status')
    if ($console.data.result.groundTruth.compilationFailed) { throw 'Unity compilation failed; inspect console.' }
    if ($timer.Elapsed.TotalSeconds -gt 120) { throw 'Timed out waiting for compilation.' }
    if ($console.data.result.groundTruth.compiling) { Start-Sleep -Milliseconds 500 }
} while ($console.data.result.groundTruth.compiling)

$assembly = 'SectorLab.Tests.' + $Mode
$null = Invoke-Editor @('run_tests', '--mode', $Mode, '--filter_type', 'assembly', '--filter', $assembly, '--async_tests', 'true')
$timer.Restart()
do {
    Start-Sleep -Seconds 2
    $status = Invoke-Editor @('test_status')
    if ($timer.Elapsed.TotalSeconds -gt 240) { throw 'Timed out waiting for Unity tests.' }
} while ($status.data.result.status -ne 'completed')

$record = [PSCustomObject]@{
    task = $Task; mode = $Mode; sourceHead = $sourceHead
    startedAtUtc = $started; completedAtUtc = [DateTime]::UtcNow.ToString('o')
    command = ('unity command run_tests --mode ' + $Mode + ' --filter_type assembly --filter ' + $assembly + ' --async_tests true')
    inputHashes = $inputHashes; response = $status
}
$logPath = 'docs/agent_execution/evidence/tests-' + $Task + '-' + $Mode.ToLowerInvariant() + '.json'
$record | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $logPath -Encoding UTF8
$summary = $status.data.result.summary
Write-Output ($Task + ' ' + $Mode + ': ' + ($summary | ConvertTo-Json -Compress) + ' -> ' + $logPath)
if ($summary.total -le 0 -or $summary.failed -ne 0 -or $summary.skipped -ne 0 -or $summary.inconclusive -ne 0) {
    throw 'Tests did not all pass; see evidence JSON.'
}
