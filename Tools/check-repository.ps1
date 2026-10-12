$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
& "$PSScriptRoot/check-privacy.ps1"
& git diff --check
if ($LASTEXITCODE -ne 0) { throw 'Working-tree whitespace errors.' }
& git diff --cached --check
if ($LASTEXITCODE -ne 0) { throw 'Staged whitespace errors.' }

$null = Get-Content Packages/manifest.json -Raw -Encoding UTF8 | ConvertFrom-Json
$null = Get-Content Packages/packages-lock.json -Raw -Encoding UTF8 | ConvertFrom-Json
$metaGuids = @(rg --no-heading '^guid: ' Assets -g '*.meta' | ForEach-Object { ($_ -split 'guid: ')[-1] })
if (@($metaGuids | Group-Object | Where-Object Count -gt 1).Count -ne 0) { throw 'Duplicate asset GUIDs.' }
$assetPaths = @(rg --files Assets/_Project)
foreach ($path in $assetPaths | Where-Object { $_ -notlike '*.meta' }) {
    if (-not (Test-Path -LiteralPath ($path + '.meta'))) { throw ('Missing meta: ' + $path) }
}
$tasks = @(Import-Csv docs/agent_execution/WORK_ITEMS.csv -Encoding UTF8)
if (@($tasks | Group-Object id | Where-Object Count -gt 1).Count -gt 0) { throw 'Duplicate task IDs.' }
foreach ($task in $tasks) {
    foreach ($dependency in $task.dependencies -split ';' | Where-Object { $_ }) {
        if ($dependency -notin $tasks.id) { throw ('Unknown dependency: ' + $dependency) }
    }
}
Write-Output ('PASS: whitespace, package JSON, ' + $metaGuids.Count + ' unique GUIDs, project asset metadata, task dependencies.')
