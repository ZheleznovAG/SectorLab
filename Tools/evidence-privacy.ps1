function Protect-EvidenceText([string] $Text) {
    # Redact only machine-local user directories. Test results, SHA and timestamps stay intact.
    return [regex]::Replace($Text, '(?i)(?:[a-z]:[\\/]+Users[\\/]+|/Users/|/home/)[\p{L}\p{N}_.-]+', '[LOCAL_USER_HOME]')
}
