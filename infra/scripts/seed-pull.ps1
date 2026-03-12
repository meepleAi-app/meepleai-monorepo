#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Pull a seed dump from a remote environment via SSH/SCP.
.PARAMETER Environment
    Source environment (staging, prod).
.PARAMETER Host
    SSH host for the remote server.
.PARAMETER OutputDir
    Local directory for downloaded dump.
#>
param(
    [Parameter(Mandatory)]
    [ValidateSet('staging', 'prod')]
    [string]$Environment,

    [Parameter(Mandatory)]
    [string]$Host,

    [string]$OutputDir = "$PSScriptRoot/../seed-dumps",
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$timestamp = Get-Date -Format 'yyyy-MM-dd-HHmmss'
$localFile = "$OutputDir/$timestamp-$Environment-pulled.dump"

if ($DryRun) {
    Write-Host "[DRY RUN] Would pull from $Host ($Environment)"
    Write-Host "[DRY RUN] Would save to: $localFile"
    exit 0
}

Write-Host "Pulling seed dump from $Host ($Environment)..."

# SSH to remote, run seed-dump, then SCP the result
$remoteDump = "/tmp/meepleai-seed-$timestamp.dump"
ssh $Host "cd /opt/meepleai && pwsh infra/scripts/seed-dump.ps1 -Profile $Environment -OutputDir /tmp"

# Find the most recent dump
$remoteFile = ssh $Host "ls -t /tmp/*-$Environment.dump 2>/dev/null | head -1"
if (-not $remoteFile) {
    throw "No dump file found on remote host"
}

scp "${Host}:${remoteFile}" $localFile
scp "${Host}:${remoteFile}.sha256" "$localFile.sha256"

# Clean up remote
ssh $Host "rm -f $remoteFile ${remoteFile}.sha256"

Write-Host "Pull complete: $localFile"
