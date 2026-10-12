param([switch] $Staged)

$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
$tool = Get-Command gitleaks -ErrorAction SilentlyContinue
$gitleaksExecutable = if ($tool) { $tool.Source } else {
    Join-Path $env:LOCALAPPDATA 'SectorLab/Tools/gitleaks/8.30.1/gitleaks.exe'
}
if (-not (Test-Path -LiteralPath $gitleaksExecutable)) { throw 'Install Gitleaks; see docs/agent_execution/PRIVACY_AUDIT.md.' }

$paths = if ($Staged) { @(& git -c core.quotepath=false diff --cached --name-only --diff-filter=ACMR) }
else { @(& git -c core.quotepath=false ls-files --cached --others --exclude-standard) }
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository files.' }
$forbidden = @($paths | Where-Object { $_ -match '(?i)(^|/)(\.env(?:\..*)?|[^/]+\.(keystore|jks|p12|pfx|pem|key|zip|ul[f]?))$' })
if ($forbidden.Count -gt 0) { throw ('Private file types must stay outside Git: ' + ($forbidden -join ', ')) }
$identity = & git var GIT_AUTHOR_IDENT
if ($LASTEXITCODE -ne 0 -or $identity -notmatch '<[^>]+@users\.noreply\.github\.com>') {
    throw 'Use a repository-local GitHub noreply email for new commits.'
}
if ($Staged) {
    & $gitleaksExecutable git --pre-commit --staged --config .gitleaks.toml --redact=100 --no-banner --log-level error .
    if ($LASTEXITCODE -ne 0) { throw 'Staged privacy scan failed; findings are redacted.' }
} else {
    # Scan a snapshot of publishable files, excluding ignored local credentials/caches.
    $temporaryRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $snapshot = Join-Path $temporaryRoot ('SectorLab-privacy-current-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $snapshot | Out-Null
    try {
        foreach ($path in $paths | Sort-Object -Unique) {
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { continue }
            $destination = Join-Path $snapshot $path
            New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
            Copy-Item -LiteralPath $path -Destination $destination
        }
        & $gitleaksExecutable dir --config .gitleaks.toml --redact=100 --no-banner --log-level error $snapshot
        if ($LASTEXITCODE -ne 0) { throw 'Current privacy scan failed; findings are redacted.' }
    } finally {
        $resolvedSnapshot = [IO.Path]::GetFullPath($snapshot)
        if (-not $resolvedSnapshot.StartsWith($temporaryRoot, [StringComparison]::OrdinalIgnoreCase) -or
            (Split-Path $resolvedSnapshot -Leaf) -notmatch '^SectorLab-privacy-current-[0-9a-f]{32}$') {
            throw 'Refusing to remove an unexpected privacy snapshot path.'
        }
        Remove-Item -LiteralPath $resolvedSnapshot -Recurse -Force
    }
}
Write-Output 'PASS: publishable files checked for secrets, personal paths/email and private file types; new Git author email is noreply.'
