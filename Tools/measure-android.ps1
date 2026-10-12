param(
    [Parameter(Mandatory = $true)] [ValidatePattern('^[A-Za-z0-9._-]+$')] [string] $Task,
    [ValidateRange(30, 3600)] [int] $DurationSeconds = 600,
    [string] $AdbPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.4f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe',
    [string] $ApkPath = '',
    [switch] $AutoRestart
)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
. "$PSScriptRoot/evidence-privacy.ps1"
if (-not (Test-Path -LiteralPath $AdbPath)) { throw 'Supply the installed Android SDK adb with -AdbPath.' }
$package = 'com.zheleznovag.sectorlab'
$prefix = 'docs/agent_execution/evidence/' + $Task
if (Test-Path -LiteralPath ($prefix + '.json')) { throw 'Use a new task name to retain earlier measurements.' }
$referencedApkSha256 = if ($ApkPath) { (Get-FileHash -LiteralPath $ApkPath -Algorithm SHA256).Hash } else { $null }
$captureRoot = Join-Path $env:TEMP ('SectorLab-android-metrics-' + [Guid]::NewGuid().ToString('N'))
$null = New-Item -ItemType Directory -Path $captureRoot
$capturePath = Join-Path $captureRoot 'own-game.png'
$adbFailures = 0
function Read-Device([string] $Command) {
    # Native stderr must not bypass the checked exit code in Windows PowerShell 5.1.
    $ErrorActionPreference = 'Continue'
    $value = & $AdbPath shell $Command 2>$null
    if ($LASTEXITCODE -ne 0) { $script:adbFailures++; return '' }
    return ($value -join "`n")
}
function Own-GameForeground {
    return (Read-Device 'dumpsys activity activities') -match ('mResumedActivity:[^\r\n]*' + [regex]::Escape($package))
}
if (-not (Own-GameForeground)) { throw 'Connect the USB debugging device and open SectorLab before measuring.' }
$model = (Read-Device 'getprop ro.product.model').Trim()
$android = (Read-Device 'getprop ro.build.version.release').Trim()
$clockTicks = [int](Read-Device 'getconf CLK_TCK')
$layer = ((Read-Device 'dumpsys SurfaceFlinger --list') -split "`n" | Where-Object { $_.StartsWith('SurfaceView - ' + $package + '/') } | Select-Object -First 1)
if (-not $layer -or $clockTicks -le 0) { throw 'Running Unity surface/process metrics unavailable.' }
$layer = $layer.Trim()
if ($AutoRestart) { Add-Type -AssemblyName System.Drawing }
$startedUtc = [DateTime]::UtcNow.ToString('o')
$sourceHead = (& git rev-parse HEAD).Trim()
$timer = [Diagnostics.Stopwatch]::StartNew()
$presented = New-Object 'System.Collections.Generic.HashSet[long]'
$rows = New-Object 'System.Collections.Generic.List[object]'
$memory = New-Object 'System.Collections.Generic.List[object]'
$restarts = New-Object 'System.Collections.Generic.List[object]'
$lastCpuPid = ''; $lastCpuTicks = 0L; $lastCpuTime = 0.0
$lastMemoryTime = -100.0; $lastCaptureTime = -100.0
$lastProgressTime = -60.0
while ($timer.Elapsed.TotalSeconds -lt $DurationSeconds) {
    $sampleTime = $timer.Elapsed.TotalSeconds
    $taskAppPid = (Read-Device ('pidof ' + $package)).Trim()
    $foreground = Own-GameForeground
    $newFrames = 0; $cpu = $null; $pss = $null; $rss = $null
    if ($foreground -and $taskAppPid -match '^\d+$') {
        $latency = Read-Device ("dumpsys SurfaceFlinger --latency '" + $layer + "'")
        foreach ($line in ($latency -split "`n")) {
            if ($line -match '^\d+\s+(\d+)\s+\d+$') {
                $actualPresent = [long]$Matches[1]
                if ($actualPresent -gt 0 -and $actualPresent -lt [long]::MaxValue -and $presented.Add($actualPresent)) { $newFrames++ }
            }
        }
        $stat = Read-Device ('cat /proc/' + $taskAppPid + '/stat')
        if ($stat -match '\)\s+(.+)$') {
            $fields = $Matches[1] -split '\s+'
            $cpuTicks = [long]$fields[11] + [long]$fields[12]
            $cpuTime = $timer.Elapsed.TotalSeconds
            if ($lastCpuPid -eq $taskAppPid) { $cpu = 100.0 * ($cpuTicks - $lastCpuTicks) / $clockTicks / ($cpuTime - $lastCpuTime) }
            $lastCpuPid = $taskAppPid; $lastCpuTicks = $cpuTicks; $lastCpuTime = $cpuTime
        }
        if ($sampleTime - $lastMemoryTime -ge 10) {
            $mem = Read-Device ('dumpsys meminfo ' + $package)
            if ($mem -match 'TOTAL PSS:\s*(\d+)') { $pss = [int]$Matches[1] }
            if ($mem -match 'TOTAL RSS:\s*(\d+)') { $rss = [int]$Matches[1] }
            if ($null -ne $pss) { $memory.Add([PSCustomObject]@{ elapsedSeconds = $sampleTime; pssKiB = $pss; rssKiB = $rss }) }
            $lastMemoryTime = $sampleTime
        }
        if ($AutoRestart -and $sampleTime - $lastCaptureTime -ge 20) {
            $null = Read-Device 'screencap -p /sdcard/sectorlab-metrics.png'
            & $AdbPath pull /sdcard/sectorlab-metrics.png $capturePath *> $null
            if ($LASTEXITCODE -eq 0 -and (Own-GameForeground)) {
                $bitmap = [Drawing.Bitmap]::FromFile($capturePath)
                try {
                    # The existing result button spans this band; three separated yellow
                    # samples avoid confusing its text or a small unit marker with the button.
                    $isResultButton = $true
                    foreach ($fraction in @(0.32, 0.42, 0.72)) {
                        $pixel = $bitmap.GetPixel([int]($bitmap.Width * $fraction), [int]($bitmap.Height * 0.65))
                        $isResultButton = $isResultButton -and $pixel.R -gt 220 -and $pixel.G -gt 170 -and $pixel.B -lt 110
                    }
                    if ($isResultButton) {
                        Copy-Item -LiteralPath $capturePath -Destination (Join-Path $captureRoot ('result-' + $restarts.Count + '.png'))
                        $tapX = [int]($bitmap.Width * 0.5); $tapY = [int]($bitmap.Height * 0.65)
                        $null = Read-Device ('input tap ' + $tapX + ' ' + $tapY)
                        $restarts.Add([PSCustomObject]@{ elapsedSeconds = $sampleTime; action = 'Tap existing result restart button detected in own foreground screenshot' })
                    }
                } finally { $bitmap.Dispose() }
            }
            $lastCaptureTime = $sampleTime
        }
    }
    $rows.Add([PSCustomObject]@{ elapsedSeconds = $sampleTime; foreground = $foreground; appPid = $taskAppPid; newPresentedFrames = $newFrames; cpuOneCorePercent = $cpu; pssKiB = $pss; rssKiB = $rss })
    if ($sampleTime - $lastProgressTime -ge 60) {
        Write-Output ($Task + ': elapsed=' + [Math]::Round($sampleTime) + 's, foreground=' + $foreground + ', detected restart taps=' + $restarts.Count)
        $lastProgressTime = $sampleTime
    }
    $remaining = 1500 - ($timer.Elapsed.TotalSeconds - $sampleTime) * 1000
    if ($remaining -gt 0) { Start-Sleep -Milliseconds ([int]$remaining) }
}
$timestamps = @($presented | Sort-Object)
$intervals = @(for ($i = 1; $i -lt $timestamps.Count; $i++) { ($timestamps[$i] - $timestamps[$i - 1]) / 1000000.0 })
$orderedIntervals = @($intervals | Sort-Object)
$record = [ordered]@{
    task = $Task; sourceHead = $sourceHead; startedAtUtc = $startedUtc; completedAtUtc = [DateTime]::UtcNow.ToString('o')
    referencedApkSha256 = $referencedApkSha256
    durationSeconds = $timer.Elapsed.TotalSeconds; model = $model; androidVersion = $android; package = $package
    samples = $rows.Count; foregroundSamples = @($rows | Where-Object foreground).Count; uniquePresentedFrames = $timestamps.Count
    fpsMean = if ($intervals.Count) { 1000.0 / ($intervals | Measure-Object -Average).Average } else { $null }
    frameIntervalMsMedian = if ($intervals.Count) { $orderedIntervals[[int][Math]::Floor($intervals.Count * 0.5)] } else { $null }
    frameIntervalMsP95 = if ($intervals.Count) { $orderedIntervals[[int][Math]::Floor(($intervals.Count - 1) * 0.95)] } else { $null }
    gapsOverOneSecond = @($intervals | Where-Object { $_ -gt 1000 }).Count
    cpuOneCorePercentMean = ($rows | Where-Object { $null -ne $_.cpuOneCorePercent } | Measure-Object cpuOneCorePercent -Average).Average
    memory = $memory.ToArray(); detectedResultRestarts = $restarts.ToArray(); adbFailures = $adbFailures
    scriptSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    measurement = 'Actual-present timestamps of Unity SurfaceView; own /proc/PID/stat; dumpsys meminfo. Raw presentation timestamps included.'
    gcAllocations = 'NOT_AVAILABLE: no attached Unity Profiler in release APK; ART is not Mono GC.'
    limitation = 'Referenced APK hash does not independently verify installation. Restarts count detected result-button taps; screenshots retained outside Git. This automated workload is not a human playtest. GPU time unavailable.'
}
Protect-EvidenceText ($record | ConvertTo-Json -Depth 7) | Set-Content -Encoding UTF8 -LiteralPath ($prefix + '.json')
$rows | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath ($prefix + '.csv')
$timestamps | ForEach-Object { [PSCustomObject]@{ actualPresentNanoseconds = $_ } } | Export-Csv -NoTypeInformation -Encoding UTF8 -LiteralPath ($prefix + '-frames.csv')
Write-Output ($Task + ': seconds=' + [Math]::Round($timer.Elapsed.TotalSeconds, 1) + ', meanFPS=' + [Math]::Round($record.fpsMean, 2) + ', foreground=' + $record.foregroundSamples + '/' + $record.samples + ', result restarts=' + $restarts.Count)
