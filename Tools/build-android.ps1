param(
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Task
)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)

function Invoke-BuildCommand([string[]] $Arguments) {
    $raw = & unity command --json --no-banner --timeout 30 @Arguments
    if ($LASTEXITCODE -ne 0) { throw ($raw -join "`n") }
    $response = ($raw -join "`n") | ConvertFrom-Json
    if (-not $response.success -or -not $response.data.success) { throw ($raw -join "`n") }
    return $response
}

$sourceHead = (& git rev-parse HEAD).Trim()
$started = [DateTime]::UtcNow.ToString('o')
$apkPath = 'Builds/Android/SectorLab-' + $Task + '.apk'
if (Test-Path -LiteralPath $apkPath) { throw 'Use a new Task name to preserve an existing APK.' }
$inputHashes = @(rg --files Assets Packages ProjectSettings | Sort-Object | ForEach-Object {
    [PSCustomObject]@{ path = $_.Replace('\', '/'); sha256 = (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash }
})
$null = Invoke-BuildCommand @('build', '--target', 'Android', '--outputPath', $apkPath, '--confirm', 'true')
$timer = [Diagnostics.Stopwatch]::StartNew()
do {
    Start-Sleep -Seconds 3
    $status = Invoke-BuildCommand @('build_status')
    if ($timer.Elapsed.TotalMinutes -gt 20) { throw 'Android build timeout; inspect Editor and preserve the pending build.' }
} while ($status.data.result.status -ne 'completed')
$status | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath ('Temp/build-' + $Task + '-full.json') -Encoding UTF8
$result = $status.data.result
$record = [ordered]@{
    task = $Task; sourceHead = $sourceHead; startedAtUtc = $started; completedAtUtc = [DateTime]::UtcNow.ToString('o')
    command = ('unity command build --target Android --outputPath ' + $apkPath + ' --confirm true')
    status = $result.status; result = $result.result; buildId = $result.buildId
    buildStartedAt = $result.buildStartedAt; buildEndedAt = $result.buildEndedAt; buildTimeMs = $result.buildTimeMs
    totalErrors = $result.totalErrors; totalWarnings = $result.totalWarnings
    apkPath = $apkPath; inputHashes = $inputHashes
}
if ($result.result -eq 'Succeeded' -and (Test-Path -LiteralPath $apkPath)) {
    $apk = Get-Item -LiteralPath $apkPath
    $record.apkBytes = $apk.Length
    $record.apkSha256 = (Get-FileHash -LiteralPath $apkPath -Algorithm SHA256).Hash
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($apk.FullName)
    try {
        $record.localContent = @($archive.Entries | Where-Object { $_.FullName -match '^assets/aa/.*' } | ForEach-Object {
            $stream = $_.Open()
            $hasher = [Security.Cryptography.SHA256]::Create()
            try { $hash = [BitConverter]::ToString($hasher.ComputeHash($stream)).Replace('-', '') }
            finally { $stream.Dispose(); $hasher.Dispose() }
            [PSCustomObject]@{ path = $_.FullName; bytes = $_.Length; sha256 = $hash }
        })
    }
    finally { $archive.Dispose() }
}
$evidencePath = 'docs/agent_execution/evidence/android-build-' + $Task + '.json'
$record | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $evidencePath -Encoding UTF8
Write-Output ($Task + ': result=' + $result.result + ', errors=' + $result.totalErrors + ' -> ' + $evidencePath)
if ($result.result -ne 'Succeeded' -or $result.totalErrors -ne 0 -or -not $record.apkSha256) { throw 'Android build failed.' }
if ($record.localContent.Count -eq 0) { throw 'APK contains no local Addressables content.' }
