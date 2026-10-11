param([switch] $RequireComplete)
$ErrorActionPreference = 'Stop'
$evidenceRoot = Join-Path (Split-Path $PSScriptRoot -Parent) 'docs/agent_execution'
$registry = Get-Content (Join-Path $evidenceRoot 'GATES.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$ids = @($registry.gates.id)
if ($ids.Count -ne 10 -or @($ids | Select-Object -Unique).Count -ne 10) { throw 'Expected G0-G9 once each.' }
foreach ($gate in $registry.gates) {
    if ($gate.status -notin @('TODO','IN_PROGRESS','PASS','FAIL','BLOCKED')) { throw ('Unknown status: ' + $gate.id) }
    if ($gate.status -eq 'PASS') {
        if (@($gate.checks).Count -eq 0) { throw ('PASS without checks: ' + $gate.id) }
        foreach ($check in $gate.checks) {
            if ($check.status -ne 'PASS' -or -not $check.evidence) { throw ('Unproven PASS: ' + $gate.id) }
            $path = Join-Path $evidenceRoot $check.evidence
            if (-not (Test-Path -LiteralPath $path)) { throw ('Missing evidence: ' + $path) }
            if ($path.EndsWith('.json')) {
                $result = Get-Content -LiteralPath $path -Raw -Encoding UTF8 | ConvertFrom-Json
                if ($result.response) { $result = $result.response }
                $summary = $result.data.result.summary
                if ($summary -and ($summary.total -le 0 -or $summary.passed -ne $summary.total -or $summary.failed -ne 0 -or $summary.skipped -ne 0)) {
                    throw ('Gate references failing/incomplete tests: ' + $gate.id)
                }
                if ($result.result -and $result.result -ne 'Succeeded') { throw ('Gate references failed build: ' + $gate.id) }
            }
        }
    }
    Write-Output ($gate.id + ': ' + $gate.status)
}
if ($RequireComplete -and @($registry.gates | Where-Object status -ne 'PASS').Count -gt 0) {
    throw 'Release gates remain open. See GATES.json and IMPLEMENTATION_STATUS.md.'
}
