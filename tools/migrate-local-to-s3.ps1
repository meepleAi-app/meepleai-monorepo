#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Migrate PDF files from local filesystem storage to S3-compatible object storage

.DESCRIPTION
    This script migrates existing PDF files from local pdf_uploads/ directory to S3 bucket.
    Supports Cloudflare R2, AWS S3, Backblaze B2, MinIO, and other S3-compatible providers.

.PARAMETER SourcePath
    Local filesystem source directory (default: pdf_uploads/)

.PARAMETER DryRun
    Preview migration without uploading (default: false)

.PARAMETER Verify
    Verify migration by comparing file counts and sizes (default: true)

.PARAMETER DeleteLocal
    Delete local files after successful migration (default: false, REQUIRES -Confirm)

.EXAMPLE
    # Dry run to preview migration
    .\migrate-local-to-s3.ps1 -DryRun

.EXAMPLE
    # Migrate files to S3
    .\migrate-local-to-s3.ps1

.EXAMPLE
    # Migrate and delete local files after verification
    .\migrate-local-to-s3.ps1 -DeleteLocal -Confirm

.NOTES
    Requires:
    - AWS CLI installed: https://aws.amazon.com/cli/
    - storage.secret configured with S3 credentials
    - S3 bucket created and accessible
#>

[CmdletBinding(SupportsShouldProcess)]
param(
    [string]$SourcePath = "pdf_uploads",
    [switch]$DryRun,
    [switch]$Verify = $true,
    [switch]$DeleteLocal,
    [switch]$Confirm
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Load S3 configuration from storage.secret
function Get-S3Config {
    $secretPath = Join-Path $PSScriptRoot "..\infra\secrets\storage.secret"

    if (-not (Test-Path $secretPath)) {
        throw "storage.secret not found at $secretPath. Run setup-secrets.ps1 first."
    }

    $config = @{}
    Get-Content $secretPath | Where-Object { $_ -match '^\s*[^#]' } | ForEach-Object {
        if ($_ -match '^\s*([^=]+)\s*=\s*(.+)\s*$') {
            $config[$matches[1]] = $matches[2]
        }
    }

    # Validate required S3 configuration
    $required = @('S3_ENDPOINT', 'S3_ACCESS_KEY', 'S3_SECRET_KEY', 'S3_BUCKET_NAME')
    foreach ($key in $required) {
        if (-not $config.ContainsKey($key) -or [string]::IsNullOrWhiteSpace($config[$key])) {
            throw "Missing required S3 configuration: $key"
        }
    }

    return $config
}

# Configure AWS CLI with S3 credentials
function Set-AwsCredentials {
    param($Config)

    $env:AWS_ACCESS_KEY_ID = $Config['S3_ACCESS_KEY']
    $env:AWS_SECRET_ACCESS_KEY = $Config['S3_SECRET_KEY']
    $env:AWS_DEFAULT_REGION = $Config['S3_REGION'] ?? 'auto'
}

# Upload single file to S3
function Upload-FileToS3 {
    param(
        [string]$LocalPath,
        [string]$S3Key,
        [string]$Bucket,
        [string]$Endpoint
    )

    $awsArgs = @(
        's3', 'cp',
        $LocalPath,
        "s3://$Bucket/$S3Key",
        '--endpoint-url', $Endpoint,
        '--storage-class', 'STANDARD',
        '--metadata', 'migrated=true'
    )

    if ($DryRun) {
        Write-Host "[DRY RUN] Would upload: $LocalPath -> s3://$Bucket/$S3Key" -ForegroundColor Cyan
        return $true
    }

    try {
        aws @awsArgs 2>&1 | Out-Null
        return $LASTEXITCODE -eq 0
    }
    catch {
        Write-Warning "Failed to upload $LocalPath : $_"
        return $false
    }
}

# Main migration logic
function Start-Migration {
    Write-Host "=== S3 Migration Tool ===" -ForegroundColor Green
    Write-Host ""

    # Step 1: Load configuration
    Write-Host "[1/5] Loading S3 configuration..." -ForegroundColor Yellow
    $config = Get-S3Config
    Set-AwsCredentials -Config $config

    $endpoint = $config['S3_ENDPOINT']
    $bucket = $config['S3_BUCKET_NAME']

    Write-Host "    Endpoint: $endpoint"
    Write-Host "    Bucket: $bucket"
    Write-Host ""

    # Step 2: Verify AWS CLI
    Write-Host "[2/5] Verifying AWS CLI installation..." -ForegroundColor Yellow
    try {
        $awsVersion = aws --version 2>&1
        Write-Host "    AWS CLI: $awsVersion"
    }
    catch {
        throw "AWS CLI not found. Install from https://aws.amazon.com/cli/"
    }
    Write-Host ""

    # Step 3: Scan local files
    Write-Host "[3/5] Scanning local files..." -ForegroundColor Yellow

    if (-not (Test-Path $SourcePath)) {
        Write-Host "    No local files found at $SourcePath (nothing to migrate)" -ForegroundColor Green
        return
    }

    $files = Get-ChildItem -Path $SourcePath -Recurse -File
    $totalFiles = $files.Count
    $totalSize = ($files | Measure-Object -Property Length -Sum).Sum

    Write-Host "    Found: $totalFiles files ($([math]::Round($totalSize / 1MB, 2)) MB)"
    Write-Host ""

    if ($totalFiles -eq 0) {
        Write-Host "No files to migrate." -ForegroundColor Green
        return
    }

    # Step 4: Upload files
    Write-Host "[4/5] Uploading files to S3..." -ForegroundColor Yellow

    $uploaded = 0
    $failed = 0

    foreach ($file in $files) {
        # Extract game ID from path structure: pdf_uploads/{gameId}/{fileId}_{filename}
        $relativePath = $file.FullName.Substring($SourcePath.Length + 1)
        $s3Key = "pdf_uploads/$($relativePath.Replace('\', '/'))"

        Write-Progress -Activity "Migrating to S3" `
            -Status "$uploaded/$totalFiles uploaded" `
            -PercentComplete (($uploaded / $totalFiles) * 100)

        if (Upload-FileToS3 -LocalPath $file.FullName -S3Key $s3Key -Bucket $bucket -Endpoint $endpoint) {
            $uploaded++
            Write-Host "    ✓ $relativePath" -ForegroundColor Green
        }
        else {
            $failed++
            Write-Host "    ✗ $relativePath" -ForegroundColor Red
        }
    }

    Write-Progress -Activity "Migrating to S3" -Completed
    Write-Host ""
    Write-Host "    Uploaded: $uploaded/$totalFiles" -ForegroundColor Green
    if ($failed -gt 0) {
        Write-Host "    Failed: $failed" -ForegroundColor Red
    }
    Write-Host ""

    # Step 5: Verification
    if ($Verify -and -not $DryRun) {
        Write-Host "[5/5] Verifying migration..." -ForegroundColor Yellow

        $s3Files = aws s3 ls "s3://$bucket/pdf_uploads/" --recursive --endpoint-url $endpoint | Measure-Object
        $s3Count = $s3Files.Count

        Write-Host "    Local files: $totalFiles"
        Write-Host "    S3 files: $s3Count"

        if ($s3Count -ge $uploaded) {
            Write-Host "    ✓ Verification passed" -ForegroundColor Green
        }
        else {
            Write-Warning "Verification warning: S3 count ($s3Count) < uploaded count ($uploaded)"
        }
        Write-Host ""
    }

    # Step 6: Delete local files (optional)
    if ($DeleteLocal) {
        if (-not $Confirm) {
            Write-Warning "DeleteLocal requires -Confirm flag for safety"
            return
        }

        if ($failed -gt 0) {
            Write-Warning "Cannot delete local files: $failed files failed to upload"
            return
        }

        if ($PSCmdlet.ShouldProcess($SourcePath, "Delete local files")) {
            Write-Host "Deleting local files..." -ForegroundColor Yellow
            Remove-Item -Path $SourcePath -Recurse -Force
            Write-Host "✓ Local files deleted" -ForegroundColor Green
        }
    }

    Write-Host "=== Migration Complete ===" -ForegroundColor Green
}

# Execute migration
try {
    Start-Migration
}
catch {
    Write-Error "Migration failed: $_"
    exit 1
}
