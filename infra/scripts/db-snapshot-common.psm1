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

function Get-PostgresConfig {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$SecretPath
    )
    $secrets = ConvertFrom-SecretFile -Path $SecretPath
    if (-not $secrets.ContainsKey('POSTGRES_USER')) {
        throw "Missing POSTGRES_USER in $SecretPath"
    }
    if (-not $secrets.ContainsKey('POSTGRES_PASSWORD')) {
        throw "Missing POSTGRES_PASSWORD in $SecretPath"
    }
    return [pscustomobject]@{
        Host     = if ($secrets.ContainsKey('POSTGRES_HOST') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_HOST'])) { $secrets['POSTGRES_HOST'] } else { 'localhost' }
        Port     = if ($secrets.ContainsKey('POSTGRES_PORT') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_PORT'])) { [int]$secrets['POSTGRES_PORT'] } else { 5432 }
        Db       = if ($secrets.ContainsKey('POSTGRES_DB') -and -not [string]::IsNullOrWhiteSpace($secrets['POSTGRES_DB'])) { $secrets['POSTGRES_DB'] } else { 'meepleai_db' }
        User     = $secrets['POSTGRES_USER']
        Password = $secrets['POSTGRES_PASSWORD']
    }
}

function Assert-LocalhostOnly {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    if (-not (Test-LocalhostHost -PgHost $Config.Host)) {
        throw "REFUSED: target host is '$($Config.Host)' but only 'localhost' or '127.0.0.1' are allowed for safety. This script is for LOCAL DEV ONLY."
    }
}

function Get-RequiredDiskSpaceBytes {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [long]$DbSizeBytes,
        [Parameter(Mandatory)]
        [long]$VolumeSizeBytes
    )
    if ($DbSizeBytes -lt 0) { throw "DbSizeBytes must be non-negative, got $DbSizeBytes" }
    if ($VolumeSizeBytes -lt 0) { throw "VolumeSizeBytes must be non-negative, got $VolumeSizeBytes" }
    $overheadBytes = 64L * 1024 * 1024
    $rawTotal = $DbSizeBytes + $VolumeSizeBytes + $overheadBytes
    return [long][math]::Ceiling($rawTotal * 1.1)
}

# --- Exports ---
Export-ModuleMember -Function @('Test-LocalhostHost', 'ConvertFrom-SecretFile', 'Get-PostgresConfig', 'Assert-LocalhostOnly', 'Get-RequiredDiskSpaceBytes')
