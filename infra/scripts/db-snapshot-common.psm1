# infra/scripts/db-snapshot-common.psm1
# Shared helpers for db-save-state, db-reset-migrations, db-restore-state.
# Imported via: Import-Module (Join-Path $PSScriptRoot 'db-snapshot-common.psm1') -Force

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# --- Functions are added below by subsequent tasks ---

function Test-LocalhostHost {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PgHost
    )
    if ([string]::IsNullOrWhiteSpace($PgHost)) { return $false }
    $normalized = $PgHost.Trim().ToLowerInvariant()
    return ($normalized -eq 'localhost') -or ($normalized -eq '127.0.0.1')
}

function ConvertFrom-SecretFile {
    [CmdletBinding()]
    [OutputType([hashtable])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Secret file not found: $Path"
    }
    $result = @{}
    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_.Trim()
        if ([string]::IsNullOrWhiteSpace($line)) { return }
        if ($line.StartsWith('#')) { return }
        $eqIndex = $line.IndexOf('=')
        if ($eqIndex -lt 1) { return }
        $key = $line.Substring(0, $eqIndex).Trim()
        $value = $line.Substring($eqIndex + 1).Trim()
        $result[$key] = $value
    }
    return $result
}

# --- Exports ---
Export-ModuleMember -Function @('Test-LocalhostHost', 'ConvertFrom-SecretFile')
