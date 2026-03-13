<#
.SYNOPSIS
    Sync secrets between local and staging server with service restart orchestration.

.DESCRIPTION
    Manages bidirectional secret sync between local dev/staging files and the
    staging server at meepleai.app. Uses a YAML manifest (secrets-sync.yml)
    to determine sync policy per secret and which services to restart.

.PARAMETER Pull
    Download server secrets to local staging/ mirror.

.PARAMETER Push
    Upload local secrets to server (default mode if no switch specified).

.PARAMETER Status
    Show diff between local and server secrets without taking action.

.PARAMETER Only
    Filter to a single secret name (without .secret extension).

.PARAMETER DryRun
    Show actions without executing them.

.PARAMETER Force
    Skip confirmation prompts.

.PARAMETER SkipRestart
    Upload secrets but don't restart services.

.EXAMPLE
    # Pull all secrets from server
    pwsh infra/scripts/sync-secrets.ps1 -Pull

    # Push changes (default mode)
    pwsh infra/scripts/sync-secrets.ps1

    # Check status only
    pwsh infra/scripts/sync-secrets.ps1 -Status

    # Push single secret with dry run
    pwsh infra/scripts/sync-secrets.ps1 -Only openrouter -DryRun
#>

[CmdletBinding(DefaultParameterSetName = 'Push')]
param(
    [Parameter(ParameterSetName = 'Pull')]
    [switch]$Pull,

    [Parameter(ParameterSetName = 'Push')]
    [switch]$Push,

    [Parameter(ParameterSetName = 'Status')]
    [switch]$Status,

    [string]$Only,
    [switch]$DryRun,
    [switch]$Force,
    [switch]$SkipRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# ── Paths ──────────────────────────────────────────────────────────────────────
$ScriptRoot = $PSScriptRoot
$InfraRoot = Split-Path $ScriptRoot -Parent
$SecretsDir = Join-Path $InfraRoot 'secrets'
$StagingDir = Join-Path $SecretsDir 'staging'
$ManifestPath = Join-Path $SecretsDir 'secrets-sync.yml'
$TempRoot = Join-Path $env:TEMP 'meepleai-secrets-sync'

# ── Cleanup trap ───────────────────────────────────────────────────────────────
function Cleanup-Temp {
    if (Test-Path $TempRoot) {
        Remove-Item $TempRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

trap { Cleanup-Temp }

# ── YAML Parser (regex-based, no external deps) ───────────────────────────────
function Parse-Manifest {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        Write-Host "ERROR: Manifest not found: $Path" -ForegroundColor Red
        exit 1
    }

    $content = Get-Content $Path -Raw
    $result = @{
        secrets       = @{}
        service_map   = @{}
        stateful      = @()
        server        = @{
            host = ''; user = ''; key_path = ''
            remote_secrets_path = ''; compose_dir = ''; compose_cmd = ''
        }
    }

    # Parse secrets section
    $inSection = $null
    foreach ($line in (Get-Content $Path)) {
        $trimmed = $line.Trim()

        # Skip comments and empty lines
        if ($trimmed -eq '' -or $trimmed.StartsWith('#')) { continue }

        # Detect top-level sections
        if ($trimmed -eq 'secrets:') { $inSection = 'secrets'; continue }
        if ($trimmed -eq 'service_map:') { $inSection = 'service_map'; continue }
        if ($trimmed -match '^stateful_services:\s*\[(.+)\]$') {
            $result.stateful = $Matches[1] -split ',\s*' | ForEach-Object { $_.Trim() }
            $inSection = $null
            continue
        }
        if ($trimmed -eq 'server:') { $inSection = 'server'; continue }

        # Parse key-value pairs within sections
        if ($inSection -eq 'secrets' -and $trimmed -match '^\s*(\S+\.secret):\s*(sync|staging-only|skip)\s*$') {
            $result.secrets[$Matches[1]] = $Matches[2]
        }
        elseif ($inSection -eq 'service_map' -and $trimmed -match '^\s*(\S+\.secret):\s*\[(.+)\]\s*$') {
            $result.service_map[$Matches[1]] = $Matches[2] -split ',\s*' | ForEach-Object { $_.Trim() }
        }
        elseif ($inSection -eq 'server') {
            if ($trimmed -match '^\s*host:\s*(.+)$') { $result.server.host = $Matches[1].Trim() }
            elseif ($trimmed -match '^\s*user:\s*(.+)$') { $result.server.user = $Matches[1].Trim() }
            elseif ($trimmed -match '^\s*key_path:\s*"?([^"]+)"?\s*$') { $result.server.key_path = $Matches[1].Trim() }
            elseif ($trimmed -match '^\s*remote_secrets_path:\s*(.+)$') { $result.server.remote_secrets_path = $Matches[1].Trim() }
            elseif ($trimmed -match '^\s*compose_dir:\s*(.+)$') { $result.server.compose_dir = $Matches[1].Trim() }
            elseif ($trimmed -match '^\s*compose_cmd:\s*>-?\s*$') { $inSection = 'server_compose_cmd' }
        }
        elseif ($inSection -eq 'server_compose_cmd') {
            if ($trimmed -match '^-f\s' -or $trimmed -match '^docker\s') {
                if (-not $result.server.compose_cmd) {
                    $result.server.compose_cmd = $trimmed
                } else {
                    $result.server.compose_cmd += ' ' + $trimmed
                }
            } else {
                # End of multi-line compose_cmd, re-parse this line
                $inSection = $null
                if ($trimmed -eq 'secrets:') { $inSection = 'secrets' }
                elseif ($trimmed -eq 'service_map:') { $inSection = 'service_map' }
                elseif ($trimmed -eq 'server:') { $inSection = 'server' }
            }
        }
    }

    return $result
}

# ── SSH helpers ────────────────────────────────────────────────────────────────
function Get-SshArgs {
    param([hashtable]$Server)
    $keyPath = "`"$($Server.key_path)`""
    return @(
        '-o', 'ConnectTimeout=5',
        '-o', 'BatchMode=yes',
        '-o', 'StrictHostKeyChecking=accept-new',
        '-i', $keyPath,
        "$($Server.user)@$($Server.host)"
    )
}

function Get-ScpArgs {
    param([hashtable]$Server)
    $keyPath = "`"$($Server.key_path)`""
    return @(
        '-o', 'ConnectTimeout=5',
        '-o', 'BatchMode=yes',
        '-o', 'StrictHostKeyChecking=accept-new',
        '-i', $keyPath
    )
}

function Test-SshConnection {
    param([hashtable]$Server)
    Write-Host 'Checking SSH connectivity...' -ForegroundColor Cyan
    $sshArgs = Get-SshArgs $Server
    $proc = Start-Process -FilePath 'ssh' -ArgumentList ($sshArgs + @('echo', 'ok')) `
        -NoNewWindow -Wait -PassThru -RedirectStandardOutput "$TempRoot\ssh-test.txt" -RedirectStandardError "$TempRoot\ssh-test-err.txt"
    if ($proc.ExitCode -ne 0) {
        $errMsg = if (Test-Path "$TempRoot\ssh-test-err.txt") { Get-Content "$TempRoot\ssh-test-err.txt" -Raw } else { 'Unknown error' }
        Write-Host "ERROR: SSH connection failed to $($Server.user)@$($Server.host)" -ForegroundColor Red
        Write-Host $errMsg -ForegroundColor Red
        exit 1
    }
    Write-Host "  Connected to $($Server.user)@$($Server.host)" -ForegroundColor Green
}

function Invoke-Ssh {
    param([hashtable]$Server, [string]$Command)
    $sshArgs = Get-SshArgs $Server
    $outFile = Join-Path $TempRoot "ssh-out-$([guid]::NewGuid().ToString('N')).txt"
    $errFile = Join-Path $TempRoot "ssh-err-$([guid]::NewGuid().ToString('N')).txt"
    $proc = Start-Process -FilePath 'ssh' -ArgumentList ($sshArgs + @($Command)) `
        -NoNewWindow -Wait -PassThru -RedirectStandardOutput $outFile -RedirectStandardError $errFile
    $output = if (Test-Path $outFile) { Get-Content $outFile -Raw } else { '' }
    $errOutput = if (Test-Path $errFile) { Get-Content $errFile -Raw } else { '' }
    if ($proc.ExitCode -ne 0) {
        Write-Host "SSH command failed: $Command" -ForegroundColor Red
        if ($errOutput) { Write-Host $errOutput -ForegroundColor Red }
        return $null
    }
    return $output
}

function Invoke-ScpDownload {
    param([hashtable]$Server, [string]$RemotePath, [string]$LocalPath)
    $scpArgs = Get-ScpArgs $Server
    $remote = "$($Server.user)@$($Server.host):$RemotePath"
    $proc = Start-Process -FilePath 'scp' -ArgumentList ($scpArgs + @($remote, $LocalPath)) `
        -NoNewWindow -Wait -PassThru -RedirectStandardError "$TempRoot\scp-err.txt"
    return $proc.ExitCode -eq 0
}

function Invoke-ScpUpload {
    param([hashtable]$Server, [string]$LocalPath, [string]$RemotePath)
    $scpArgs = Get-ScpArgs $Server
    $remote = "$($Server.user)@$($Server.host):$RemotePath"
    $proc = Start-Process -FilePath 'scp' -ArgumentList ($scpArgs + @($LocalPath, $remote)) `
        -NoNewWindow -Wait -PassThru -RedirectStandardError "$TempRoot\scp-err.txt"
    return $proc.ExitCode -eq 0
}

# ── Hash helper ────────────────────────────────────────────────────────────────
function Get-FileHashValue {
    param([string]$Path)
    if (-not (Test-Path $Path)) { return $null }
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash
}

# ── Confirmation helper ───────────────────────────────────────────────────────
function Confirm-Action {
    param([string]$Message)
    if ($Force) { return $true }
    if ($DryRun) { return $false }
    Write-Host ""
    $response = Read-Host "$Message [y/N]"
    return $response -eq 'y' -or $response -eq 'Y'
}

# ── Table display helpers ─────────────────────────────────────────────────────
function Show-StatusTable {
    param([array]$Rows, [bool]$ShowServices = $false)

    # Calculate column widths
    $nameWidth = ($Rows | ForEach-Object { $_.Name.Length } | Measure-Object -Maximum).Maximum
    $nameWidth = [Math]::Max($nameWidth, 6)  # minimum "Secret" header
    $statusWidth = 8

    if ($ShowServices) {
        $svcWidth = ($Rows | ForEach-Object { $_.Services.Length } | Measure-Object -Maximum).Maximum
        $svcWidth = [Math]::Max($svcWidth, 8)  # minimum "Services" header

        $headerFmt = "  {0,-$nameWidth}  {1,-$statusWidth}  {2,-$svcWidth}"
        Write-Host ""
        Write-Host ($headerFmt -f 'Secret', 'Status', 'Services') -ForegroundColor White
        Write-Host ("  " + ('-' * $nameWidth) + "  " + ('-' * $statusWidth) + "  " + ('-' * $svcWidth)) -ForegroundColor DarkGray
    } else {
        $headerFmt = "  {0,-$nameWidth}  {1,-$statusWidth}"
        Write-Host ""
        Write-Host ($headerFmt -f 'Secret', 'Status') -ForegroundColor White
        Write-Host ("  " + ('-' * $nameWidth) + "  " + ('-' * $statusWidth)) -ForegroundColor DarkGray
    }

    foreach ($row in $Rows) {
        $color = switch ($row.Status) {
            'NEW'     { 'Yellow' }
            'UPDATED' { 'Yellow' }
            'CHANGED' { 'Yellow' }
            'OK'      { 'Green' }
            'SKIP'    { 'DarkGray' }
            'MISSING' { 'Red' }
            default   { 'White' }
        }

        if ($ShowServices) {
            $line = $headerFmt -f $row.Name, $row.Status, $row.Services
        } else {
            $line = ("  {0,-$nameWidth}  {1,-$statusWidth}" -f $row.Name, $row.Status)
        }
        Write-Host $line -ForegroundColor $color
    }
    Write-Host ""
}

# ── PULL MODE ──────────────────────────────────────────────────────────────────
function Invoke-Pull {
    $manifest = Parse-Manifest $ManifestPath
    $server = $manifest.server

    # Ensure temp dir
    New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

    # Test connectivity
    Test-SshConnection $server

    Write-Host "`nPulling secrets from server..." -ForegroundColor Cyan

    $remotePath = $server.remote_secrets_path
    $downloadDir = Join-Path $TempRoot 'server'
    New-Item -ItemType Directory -Path $downloadDir -Force | Out-Null

    # List secret files on server
    $listOutput = Invoke-Ssh $server "ls -1 $remotePath/*.secret 2>/dev/null | xargs -n1 basename"
    if (-not $listOutput) {
        Write-Host "ERROR: No secret files found on server" -ForegroundColor Red
        Cleanup-Temp
        exit 1
    }
    $remoteFiles = @($listOutput.Trim() -split "`n" | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne '' })

    if ($Only) {
        $filterName = if ($Only.EndsWith('.secret')) { $Only } else { "$Only.secret" }
        $remoteFiles = @($remoteFiles | Where-Object { $_ -eq $filterName })
    }

    # Download each file via SCP
    $failedDownloads = @()
    if (-not $DryRun) {
        foreach ($name in $remoteFiles) {
            Write-Host "  Downloading $name..." -ForegroundColor Gray
            $ok = Invoke-ScpDownload $server "$remotePath/$name" (Join-Path $downloadDir $name)
            if (-not $ok) {
                Write-Host "  WARNING: Failed to download $name, retrying..." -ForegroundColor Yellow
                Start-Sleep -Milliseconds 500
                $ok = Invoke-ScpDownload $server "$remotePath/$name" (Join-Path $downloadDir $name)
                if (-not $ok) {
                    Write-Host "  ERROR: Failed to download $name after retry" -ForegroundColor Red
                    $failedDownloads += $name
                }
            }
        }
        # Exclude failed downloads
        if ($failedDownloads.Count -gt 0) {
            $remoteFiles = @($remoteFiles | Where-Object { $_ -notin $failedDownloads })
        }
    }

    # Compare with local staging/
    $rows = @()
    foreach ($name in ($remoteFiles | Sort-Object)) {
        $serverFile = Join-Path $downloadDir $name
        $localFile = Join-Path $StagingDir $name

        if ($DryRun) {
            $status = if (Test-Path $localFile) { 'UPDATED' } else { 'NEW' }
        } else {
            $serverHash = Get-FileHashValue $serverFile
            $localHash = Get-FileHashValue $localFile

            if (-not $localHash) {
                $status = 'NEW'
            } elseif ($serverHash -eq $localHash) {
                $status = 'OK'
            } else {
                $status = 'UPDATED'
            }
        }

        $rows += @{ Name = $name; Status = $status; Services = '' }
    }

    Show-StatusTable $rows

    $changed = @($rows | Where-Object { $_.Status -ne 'OK' })
    $changedCount = $changed.Count
    if ($changedCount -eq 0) {
        Write-Host "All secrets are up to date." -ForegroundColor Green
        Cleanup-Temp
        return
    }

    if ($DryRun) {
        Write-Host "[DRY RUN] Would save $changedCount secret(s) to staging/" -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    if (-not (Confirm-Action "Save $changedCount secret(s) to staging/?")) {
        Write-Host "Cancelled." -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    # Ensure staging dir exists
    New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

    # Copy changed/new files to staging
    foreach ($row in $changed) {
        $src = Join-Path $downloadDir $row.Name
        $dst = Join-Path $StagingDir $row.Name
        Copy-Item $src $dst -Force
        Write-Host "  Saved: $($row.Name)" -ForegroundColor Green
    }

    Write-Host "`nPull complete. $changedCount secret(s) updated in staging/." -ForegroundColor Green
    Cleanup-Temp
}

# ── PUSH / STATUS MODE ────────────────────────────────────────────────────────
function Invoke-PushOrStatus {
    param([bool]$StatusOnly = $false)

    $manifest = Parse-Manifest $ManifestPath
    $server = $manifest.server

    # Ensure temp dir
    New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

    # Test connectivity
    Test-SshConnection $server

    $modeName = if ($StatusOnly) { 'Status' } else { 'Push' }
    Write-Host "`n$modeName — comparing local secrets with server..." -ForegroundColor Cyan

    # Step 1: Resolve source for each secret per manifest policy
    $secretSources = @{}
    $errors = @()
    foreach ($entry in $manifest.secrets.GetEnumerator()) {
        $name = $entry.Key
        $policy = $entry.Value

        if ($Only) {
            $filterName = if ($Only.EndsWith('.secret')) { $Only } else { "$Only.secret" }
            if ($name -ne $filterName) { continue }
        }

        switch ($policy) {
            'sync' {
                $src = Join-Path $SecretsDir $name
                if (-not (Test-Path $src)) {
                    $errors += "Missing source for '$name' (policy=sync): $src"
                } else {
                    $secretSources[$name] = $src
                }
            }
            'staging-only' {
                $src = Join-Path $StagingDir $name
                if (-not (Test-Path $src)) {
                    $errors += "Missing source for '$name' (policy=staging-only): $src"
                } else {
                    $secretSources[$name] = $src
                }
            }
            'skip' {
                # Intentionally skipped
            }
        }
    }

    if (@($errors).Count -gt 0) {
        Write-Host "`nWARNING: Some secrets have missing source files:" -ForegroundColor Yellow
        foreach ($err in $errors) {
            Write-Host "  $err" -ForegroundColor Yellow
        }
        Write-Host ""
    }

    if (@($secretSources.Keys).Count -eq 0) {
        Write-Host "No secrets to process." -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    # Step 2: Download current server files for comparison
    $serverDir = Join-Path $TempRoot 'server-current'
    New-Item -ItemType Directory -Path $serverDir -Force | Out-Null

    if (-not $DryRun) {
        Write-Host "  Downloading current server secrets for comparison..." -ForegroundColor Gray
        foreach ($name in $secretSources.Keys) {
            $remoteFile = "$($server.remote_secrets_path)/$name"
            $localTemp = Join-Path $serverDir $name
            Invoke-ScpDownload $server $remoteFile $localTemp | Out-Null
        }
    }

    # Step 3: Compare each secret
    $rows = @()
    $changedSecrets = @()

    foreach ($entry in ($secretSources.GetEnumerator() | Sort-Object -Property Key)) {
        $name = $entry.Key
        $localPath = $entry.Value
        $serverPath = Join-Path $serverDir $name

        $services = if ($manifest.service_map.ContainsKey($name)) {
            ($manifest.service_map[$name] -join ', ')
        } else { '-' }

        if ($DryRun) {
            $status = 'CHANGED'
            $changedSecrets += $name
        } else {
            $localHash = Get-FileHashValue $localPath
            $serverHash = Get-FileHashValue $serverPath

            if (-not $serverHash) {
                $status = 'NEW'
                $changedSecrets += $name
            } elseif ($localHash -eq $serverHash) {
                $status = 'OK'
            } else {
                $status = 'CHANGED'
                $changedSecrets += $name
            }
        }

        $rows += @{ Name = $name; Status = $status; Services = $services }
    }

    # Add skipped secrets to table
    foreach ($entry in $manifest.secrets.GetEnumerator()) {
        if ($entry.Value -eq 'skip') {
            if ($Only) {
                $filterName = if ($Only.EndsWith('.secret')) { $Only } else { "$Only.secret" }
                if ($entry.Key -ne $filterName) { continue }
            }
            $rows += @{ Name = $entry.Key; Status = 'SKIP'; Services = '-' }
        }
    }

    $rows = $rows | Sort-Object { $_.Name }
    Show-StatusTable $rows -ShowServices $true

    # Status mode: stop here
    if ($StatusOnly) {
        $changedCount = $changedSecrets.Count
        if ($changedCount -eq 0) {
            Write-Host "All secrets are in sync." -ForegroundColor Green
        } else {
            Write-Host "$changedCount secret(s) have changes." -ForegroundColor Yellow
        }
        Cleanup-Temp
        return
    }

    # Push mode: continue with upload
    if (@($changedSecrets).Count -eq 0) {
        Write-Host "All secrets are in sync. Nothing to push." -ForegroundColor Green
        Cleanup-Temp
        return
    }

    # Step 5: Stateful service warning
    $impactedServices = @()
    foreach ($name in $changedSecrets) {
        if ($manifest.service_map.ContainsKey($name)) {
            $impactedServices += $manifest.service_map[$name]
        }
    }
    $impactedServices = $impactedServices | Select-Object -Unique | Sort-Object

    $statefulImpacted = $impactedServices | Where-Object { $manifest.stateful -contains $_ }
    if ($statefulImpacted) {
        Write-Host "" -ForegroundColor Red
        foreach ($svc in $statefulImpacted) {
            $causingSecret = $changedSecrets | Where-Object {
                $manifest.service_map.ContainsKey($_) -and $manifest.service_map[$_] -contains $svc
            }
            Write-Host "  WARNING: $($causingSecret -join ', ') changed -> $svc is stateful!" -ForegroundColor Red
        }
        Write-Host "  Changing passwords on running stateful services may cause data" -ForegroundColor Red
        Write-Host "  inaccessibility. The volume retains the old password." -ForegroundColor Red
        Write-Host "  Proceed only if you know what you're doing." -ForegroundColor Red
        Write-Host ""
    }

    if ($DryRun) {
        Write-Host "[DRY RUN] Would upload $($changedSecrets.Count) secret(s) and restart: $($impactedServices -join ', ')" -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    if (-not (Confirm-Action "Upload $($changedSecrets.Count) changed secret(s) to server?")) {
        Write-Host "Cancelled." -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    # Step 6: Backup on server
    Write-Host "`nBacking up changed secrets on server..." -ForegroundColor Cyan
    $dateStamp = Get-Date -Format 'yyyyMMdd'
    $backupDir = "$($server.remote_secrets_path)/backup"
    Invoke-Ssh $server "mkdir -p $backupDir" | Out-Null

    foreach ($name in $changedSecrets) {
        $remoteSrc = "$($server.remote_secrets_path)/$name"
        $backupDst = "$backupDir/$name.$dateStamp"
        $result = Invoke-Ssh $server "[ -f '$remoteSrc' ] && cp '$remoteSrc' '$backupDst' && echo 'ok' || echo 'skip'"
        if ($result -and $result.Trim() -eq 'ok') {
            Write-Host "  Backed up: $name -> backup/$name.$dateStamp" -ForegroundColor Gray
        }
    }

    # Step 7: Upload changed files
    Write-Host "`nUploading secrets..." -ForegroundColor Cyan
    foreach ($name in $changedSecrets) {
        $localPath = $secretSources[$name]
        $remotePath = "$($server.remote_secrets_path)/$name"
        $ok = Invoke-ScpUpload $server $localPath $remotePath
        if ($ok) {
            Write-Host "  Uploaded: $name" -ForegroundColor Green
        } else {
            Write-Host "  FAILED: $name" -ForegroundColor Red
        }
    }

    # Step 8-9: Restart services
    if ($SkipRestart) {
        Write-Host "`nSkipping service restart (-SkipRestart)." -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    if (@($impactedServices).Count -eq 0) {
        Write-Host "`nNo services to restart." -ForegroundColor Green
        Cleanup-Temp
        return
    }

    Write-Host "`nServices to restart: $($impactedServices -join ', ')" -ForegroundColor Cyan

    if (-not (Confirm-Action "Restart these services?")) {
        Write-Host "Skipping restart." -ForegroundColor Yellow
        Cleanup-Temp
        return
    }

    # Step 10: Re-source env and restart
    Write-Host "`nRestarting services..." -ForegroundColor Cyan
    $serviceList = $impactedServices -join ' '
    $composeDir = $server.compose_dir
    $composeCmd = $server.compose_cmd

    $restartCmd = "cd $composeDir && set -a; for f in secrets/*.secret; do source `"`$f`" 2>/dev/null; done; set +a && $composeCmd up -d --force-recreate $serviceList"
    $result = Invoke-Ssh $server $restartCmd
    if ($result) {
        Write-Host $result -ForegroundColor Gray
    }

    # Step 11: Verify health
    Write-Host "`nVerifying service status..." -ForegroundColor Cyan
    $psCmd = "cd $composeDir && $composeCmd ps $serviceList"
    $result = Invoke-Ssh $server $psCmd
    if ($result) {
        Write-Host $result
    }

    Write-Host "`nPush complete." -ForegroundColor Green
    Cleanup-Temp
}

# ── MAIN ───────────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "=== MeepleAI Secrets Sync ===" -ForegroundColor Cyan
Write-Host ""

# Ensure temp dir exists
New-Item -ItemType Directory -Path $TempRoot -Force | Out-Null

if ($Pull) {
    Invoke-Pull
} elseif ($Status) {
    Invoke-PushOrStatus -StatusOnly $true
} else {
    # Default: Push mode
    Invoke-PushOrStatus -StatusOnly $false
}
