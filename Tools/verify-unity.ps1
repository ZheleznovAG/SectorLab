param(
    [ValidateSet('EditMode', 'PlayMode')] [string] $Mode = 'EditMode',
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Task,
    [string] $TestFilter = ''
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
. "$PSScriptRoot/evidence-privacy.ps1"

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
$inputHashes = @(rg --files Assets Packages ProjectSettings | Sort-Object | ForEach-Object {
    [PSCustomObject]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})
# Compilation reloads runtime-only state; the test runner must start outside Play Mode.
$null = Invoke-Editor @('editor_stop')
$stopTimer = [Diagnostics.Stopwatch]::StartNew()
do {
    $editorState = Invoke-Editor @('eval', '--code', 'return UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;')
    if (-not $editorState.data.result.result) { break }
    if ($stopTimer.Elapsed.TotalSeconds -gt 60) { throw 'Timed out leaving Play Mode before tests.' }
    Start-Sleep -Milliseconds 500
} while ($true)
$null = Invoke-Editor @('eval', '--code', 'UnityEditor.AssetDatabase.Refresh();')
$timer = [Diagnostics.Stopwatch]::StartNew()
$stableSamples = 0
do {
    $console = Invoke-Editor @('console_status')
    $truth = $console.data.result.groundTruth
    # Refresh schedules compilation; its first status can still describe the previous assembly.
    if ($truth.compilationFailed -and -not $truth.compiling -and $timer.Elapsed.TotalSeconds -gt 5) {
        throw 'Unity compilation failed; inspect console.'
    }
    if ($timer.Elapsed.TotalSeconds -gt 120) { throw 'Timed out waiting for compilation.' }
    if ($truth.compiling -or $truth.compilationFailed) { $stableSamples = 0 }
    else { $stableSamples++ }
    Start-Sleep -Milliseconds 500
} while ($stableSamples -lt 3 -or $timer.Elapsed.TotalSeconds -lt 3)

$assembly = 'SectorLab.Tests.' + $Mode
$filterType = if ([string]::IsNullOrWhiteSpace($TestFilter)) { 'assembly' } else { 'testName' }
$filter = if ($filterType -eq 'assembly') { $assembly } else { $TestFilter }
$taskContextPath = 'Temp/verification-task.cs'
# Editor runs in another process; pass the evidence task explicitly before starting its tests.
('System.Environment.SetEnvironmentVariable("SECTORLAB_VERIFICATION_TASK", "' + $Task + '");') |
    Set-Content -LiteralPath $taskContextPath -Encoding UTF8
$null = Invoke-Editor @('eval_file', '--file', $taskContextPath)
$null = Invoke-Editor @('run_tests', '--mode', $Mode, '--filter_type', $filterType, '--filter', $filter, '--async_tests', 'true')
$timer.Restart()
do {
    Start-Sleep -Seconds 2
    $status = Invoke-Editor @('test_status')
    if ($timer.Elapsed.TotalSeconds -gt 600) { throw 'Timed out waiting for Unity tests.' }
} while ($status.data.result.status -ne 'completed')

$record = [PSCustomObject]@{
    task = $Task; mode = $Mode; sourceHead = $sourceHead
    startedAtUtc = $started; completedAtUtc = [DateTime]::UtcNow.ToString('o')
    command = ('unity command run_tests --mode ' + $Mode + ' --filter_type ' + $filterType + ' --filter ' + $filter + ' --async_tests true')
    inputHashes = $inputHashes; response = $status
}
$logPath = 'docs/agent_execution/evidence/tests-' + $Task + '-' + $Mode.ToLowerInvariant() + '.json'
$record | Add-Member -NotePropertyName privacyRedaction -NotePropertyValue 'Machine-local user directories redacted; test results and source hashes unchanged.'
Protect-EvidenceText ($record | ConvertTo-Json -Depth 20) | Set-Content -LiteralPath $logPath -Encoding UTF8
$summary = $status.data.result.summary
Write-Output ($Task + ' ' + $Mode + ': ' + ($summary | ConvertTo-Json -Compress) + ' -> ' + $logPath)
if ($summary.total -le 0 -or $summary.failed -ne 0 -or $summary.skipped -ne 0 -or $summary.inconclusive -ne 0) {
    throw 'Tests did not all pass; see evidence JSON.'
}
