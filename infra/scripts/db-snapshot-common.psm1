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

function Read-Manifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Manifest not found: $Path"
    }
    $raw = Get-Content -LiteralPath $Path -Raw
    return $raw | ConvertFrom-Json
}

function Write-Manifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Path,
        [Parameter(Mandatory)]
        [object]$Object
    )
    $tmpPath = "$Path.tmp"
    $json = $Object | ConvertTo-Json -Depth 10
    Set-Content -LiteralPath $tmpPath -Value $json -Encoding UTF8
    Move-Item -LiteralPath $tmpPath -Destination $Path -Force
}

function Normalize-PgSchema {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$Sql
    )
    if ([string]::IsNullOrWhiteSpace($Sql)) { return '' }

    # Phase 1: line-by-line filter
    $kept = New-Object System.Collections.Generic.List[string]
    foreach ($rawLine in ($Sql -split "`r?`n")) {
        $line = $rawLine.TrimEnd()
        if ($line -match '^\s*--') { continue }                        # comment
        if ($line -match '^\s*SET\s') { continue }                     # SET statements
        if ($line -match '^\s*SELECT\s+pg_catalog\.set_config') { continue }
        $kept.Add($line)
    }

    # Phase 2: collapse runs of blank lines into a single blank
    $compact = New-Object System.Collections.Generic.List[string]
    $prevBlank = $false
    foreach ($line in $kept) {
        $isBlank = [string]::IsNullOrWhiteSpace($line)
        if ($isBlank -and $prevBlank) { continue }
        $compact.Add($line)
        $prevBlank = $isBlank
    }

    # Phase 3: sort top-level statements alphabetically
    $joined = ($compact -join "`n").Trim()
    if ($joined.Length -eq 0) { return '' }

    # Split into statements by semicolon, respecting single-quoted string literals
    # (PostgreSQL: '...' for strings, '' to escape a single quote inside)
    $statements = New-Object System.Collections.Generic.List[string]
    $current = New-Object System.Text.StringBuilder
    $inString = $false
    $chars = $joined.ToCharArray()
    for ($i = 0; $i -lt $chars.Length; $i++) {
        $ch = $chars[$i]
        [void]$current.Append($ch)
        if ($ch -eq "'") {
            # Toggle string state, but handle '' (escaped quote) inside strings
            if ($inString -and $i + 1 -lt $chars.Length -and $chars[$i + 1] -eq "'") {
                # Escaped quote — append the next ' and skip it
                [void]$current.Append($chars[$i + 1])
                $i++
            } else {
                $inString = -not $inString
            }
            continue
        }
        if ($ch -eq ';' -and -not $inString) {
            $stmt = $current.ToString().Trim()
            if ($stmt.Length -gt 0) { $statements.Add($stmt) }
            [void]$current.Clear()
        }
    }
    $tail = $current.ToString().Trim()
    if ($tail.Length -gt 0) { $statements.Add($tail) }

    $sorted = $statements | Sort-Object { $_ }
    return ($sorted -join "`n`n")
}

function Test-SchemaDriftClass {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PreSchema,
        [Parameter(Mandatory)]
        [AllowEmptyString()]
        [string]$PostSchema
    )

    # Byte-equal short circuit
    if ($PreSchema -ceq $PostSchema) { return 'identical' }

    # Whitespace-only difference → minor (or identical after trim)
    $preTrimmed = ($PreSchema -replace '\s+', ' ').Trim()
    $postTrimmed = ($PostSchema -replace '\s+', ' ').Trim()
    if ($preTrimmed -ceq $postTrimmed) { return 'minor' }

    # Anything else is significant
    return 'significant'
}

function Write-Timestamped {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$Message,
        [string]$LogFile = $null
    )
    $line = "[$(Get-Date -Format 'HH:mm:ss')] $Message"
    Write-Host $line
    if ($LogFile) {
        Add-Content -LiteralPath $LogFile -Value $line
    }
}

function Get-FileSha256 {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$Path
    )
    if (-not (Test-Path -LiteralPath $Path)) {
        throw "File not found: $Path"
    }
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Confirm-UserAction {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$Prompt,
        [switch]$Force
    )
    if ($Force) { return $true }
    Write-Host ''
    Write-Host $Prompt -ForegroundColor Yellow
    Write-Host ''
    Write-Host -NoNewline "Type 'yes' to continue: "
    $answer = Read-Host
    return ($answer -ceq 'yes')
}

function Invoke-PgDump {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $allArgs = @(
            '-h', $Config.Host,
            '-p', $Config.Port.ToString(),
            '-U', $Config.User,
            '-d', $Config.Db
        ) + $Arguments
        & pg_dump @allArgs
        if ($LASTEXITCODE -ne 0) {
            throw "pg_dump failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-PgRestore {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string[]]$Arguments
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $allArgs = @(
            '-h', $Config.Host,
            '-p', $Config.Port.ToString(),
            '-U', $Config.User,
            '-d', $Config.Db
        ) + $Arguments
        & pg_restore @allArgs
        if ($LASTEXITCODE -ne 0) {
            throw "pg_restore failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Invoke-Psql {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config,
        [Parameter(Mandatory)]
        [string]$Sql,
        [string]$Database = $null
    )
    $env:PGPASSWORD = $Config.Password
    try {
        $db = if ($Database) { $Database } else { $Config.Db }
        $output = & psql -h $Config.Host -p $Config.Port -U $Config.User -d $db -t -A -c $Sql
        if ($LASTEXITCODE -ne 0) {
            throw "psql failed with exit code $LASTEXITCODE"
        }
        return $output
    }
    finally {
        Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
    }
}

function Test-DockerVolumeExists {
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter(Mandatory)]
        [string]$VolumeName
    )
    $null = & docker volume inspect $VolumeName 2>$null
    return ($LASTEXITCODE -eq 0)
}

function Get-DockerVolumeStats {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$VolumeName
    )
    if (-not (Test-DockerVolumeExists -VolumeName $VolumeName)) {
        return [pscustomobject]@{ Exists = $false; FileCount = 0; SizeBytes = 0L }
    }
    # Use a tiny alpine container to du -sb the volume contents
    $sizeRaw = & docker run --rm -v "${VolumeName}:/data:ro" alpine sh -c 'du -sb /data 2>/dev/null | cut -f1'
    $countRaw = & docker run --rm -v "${VolumeName}:/data:ro" alpine sh -c 'find /data -type f 2>/dev/null | wc -l'
    return [pscustomobject]@{
        Exists    = $true
        FileCount = [int]($countRaw.Trim())
        SizeBytes = [long]($sizeRaw.Trim())
    }
}

function Get-StorageProvider {
    [CmdletBinding()]
    [OutputType([string])]
    param(
        [Parameter(Mandatory)]
        [string]$StorageSecretPath
    )
    if (-not (Test-Path -LiteralPath $StorageSecretPath)) {
        return 'local'  # default if no secret file
    }
    $secrets = ConvertFrom-SecretFile -Path $StorageSecretPath
    if ($secrets.ContainsKey('STORAGE_PROVIDER')) {
        return $secrets['STORAGE_PROVIDER'].ToLowerInvariant()
    }
    return 'local'
}

function Get-DbSizeBytes {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = "SELECT pg_database_size('$($Config.Db)');"
    $result = Invoke-Psql -Config $Config -Sql $sql
    return [long]($result.Trim())
}

function Get-XactCommitCount {
    [CmdletBinding()]
    [OutputType([long])]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = "SELECT xact_commit FROM pg_stat_database WHERE datname = '$($Config.Db)';"
    $result = Invoke-Psql -Config $Config -Sql $sql
    return [long]($result.Trim())
}

function Get-ActiveBackendConnections {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [pscustomobject]$Config
    )
    $sql = @"
SELECT pid || '|' || COALESCE(application_name,'') || '|' || COALESCE(client_addr::text,'') || '|' || state
FROM pg_stat_activity
WHERE datname = '$($Config.Db)'
  AND pid <> pg_backend_pid()
  AND (application_name LIKE 'Npgsql%' OR application_name LIKE '%api%' OR usename = '$($Config.User)')
"@
    $output = Invoke-Psql -Config $Config -Sql $sql
    if ([string]::IsNullOrWhiteSpace($output)) { return @() }
    return ($output -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

# --- Exports ---
Export-ModuleMember -Function @(
    'Test-LocalhostHost',
    'ConvertFrom-SecretFile',
    'Get-PostgresConfig',
    'Assert-LocalhostOnly',
    'Get-RequiredDiskSpaceBytes',
    'Read-Manifest',
    'Write-Manifest',
    'Normalize-PgSchema',
    'Test-SchemaDriftClass',
    'Write-Timestamped',
    'Get-FileSha256',
    'Confirm-UserAction',
    'Invoke-PgDump',
    'Invoke-PgRestore',
    'Invoke-Psql',
    'Test-DockerVolumeExists',
    'Get-DockerVolumeStats',
    'Get-StorageProvider',
    'Get-DbSizeBytes',
    'Get-XactCommitCount',
    'Get-ActiveBackendConnections'
)
