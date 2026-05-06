#!/usr/bin/env pwsh
# =============================================================================
# security-audit.ps1 — External port scan against staging/prod with whitelist
# =============================================================================
# Usage:
#   pwsh infra/scripts/security-audit.ps1
#   pwsh infra/scripts/security-audit.ps1 -Target 1.2.3.4
#
# Default target: staging (204.168.135.69)
# Whitelist: only TCP/22 (SSH) is expected to be OPEN from public Internet.
# Exit 1 on drift.
#
# Spec: docs/superpowers/specs/2026-05-06-database-network-isolation-design.md
# Issue: #795
# =============================================================================

[CmdletBinding()]
param(
    [string]$Target = $(if ($env:SECURITY_AUDIT_TARGET) { $env:SECURITY_AUDIT_TARGET } else { "204.168.135.69" }),
    [int]$TimeoutSeconds = 5
)

$ErrorActionPreference = "Stop"

$AllPorts = @(22, 80, 443, 5432, 6379, 8080, 8090, 3000, 3001, 8000, 8001, 8002, 8003, 8004, 8025, 1025, 5678, 3100, 9000, 9001, 9090, 9093, 11434)
$Whitelist = @(22)

Write-Host "=== Security Audit: external port scan ===" -ForegroundColor Blue
Write-Host "Target:    $Target"
Write-Host "Scanner:   Test-NetConnection (PowerShell)"
Write-Host "Whitelist: $($Whitelist -join ', ')"
Write-Host "Total checked: $($AllPorts.Count) ports"
Write-Host ""

$OpenPorts = @()
$Drift = @()
$WhitelistVerified = @()

foreach ($p in $AllPorts) {
    $isOpen = Test-NetConnection -ComputerName $Target -Port $p `
        -WarningAction SilentlyContinue -InformationLevel Quiet

    if ($Whitelist -contains $p) {
        if ($isOpen) {
            $WhitelistVerified += $p
            Write-Host ("  ✓ {0} OPEN (whitelisted)" -f $p) -ForegroundColor Green
        } else {
            Write-Host ("  ! {0} whitelisted but NOT open — expected open" -f $p) -ForegroundColor Yellow
        }
    } else {
        if ($isOpen) {
            $Drift += $p
            $OpenPorts += $p
            Write-Host ("  ✗ {0} OPEN — DRIFT (should be filtered/closed)" -f $p) -ForegroundColor Red
        } else {
            Write-Host ("  ✓ {0} closed/filtered" -f $p) -ForegroundColor Green
        }
    }
}

Write-Host ""
Write-Host "=== Summary ===" -ForegroundColor Blue
Write-Host "Whitelist verified: $($WhitelistVerified.Count) / $($Whitelist.Count)"
Write-Host "Drift detected:     $($Drift.Count)"

if ($Drift.Count -gt 0) {
    Write-Host "❌ FAIL — unauthorized open ports: $($Drift -join ', ')" -ForegroundColor Red
    Write-Host ""
    Write-Host "Run on staging:"
    Write-Host "  ssh deploy@${Target} 'sudo ss -tlnp 2>/dev/null | grep -E `":(`"$($Drift[0])`")`"'"
    exit 1
}

Write-Host "✅ PASS — only whitelisted ports are reachable" -ForegroundColor Green
exit 0
