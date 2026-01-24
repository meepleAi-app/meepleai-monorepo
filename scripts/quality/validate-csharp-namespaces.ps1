#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates that C# using statements reference existing namespaces.

.DESCRIPTION
    Scans all .cs files in apps/api/src for using statements that reference
    namespaces starting with 'Api.' and verifies they exist in the codebase.

    This prevents orphan imports that would cause build failures.

.EXAMPLE
    .\scripts\validate-csharp-namespaces.ps1
#>

$ErrorActionPreference = "Stop"

$apiSrcPath = Join-Path $PSScriptRoot ".." "apps" "api" "src"

if (-not (Test-Path $apiSrcPath)) {
    Write-Error "API source path not found: $apiSrcPath"
    exit 1
}

Write-Host "Scanning C# files for namespace validation..." -ForegroundColor Cyan

# Get all namespaces defined in the codebase
$definedNamespaces = @{}
Get-ChildItem -Path $apiSrcPath -Recurse -Filter "*.cs" | ForEach-Object {
    $content = Get-Content $_.FullName -Raw
    if ($content -match 'namespace\s+([\w.]+)') {
        $ns = $Matches[1]
        $definedNamespaces[$ns] = $true

        # Also add parent namespaces
        $parts = $ns -split '\.'
        for ($i = 1; $i -lt $parts.Length; $i++) {
            $parentNs = ($parts[0..$i] -join '.')
            $definedNamespaces[$parentNs] = $true
        }
    }
}

Write-Host "Found $($definedNamespaces.Count) defined namespaces" -ForegroundColor Gray

# Check all using statements
$errors = @()
Get-ChildItem -Path $apiSrcPath -Recurse -Filter "*.cs" | ForEach-Object {
    $file = $_
    $lineNum = 0
    Get-Content $file.FullName | ForEach-Object {
        $lineNum++
        if ($_ -match '^\s*using\s+(Api\.[\w.]+)\s*;') {
            $usedNamespace = $Matches[1]

            # Check if namespace exists (exact match or as parent)
            $exists = $definedNamespaces.ContainsKey($usedNamespace)

            if (-not $exists) {
                $relativePath = $file.FullName.Replace((Get-Item $apiSrcPath).Parent.Parent.FullName, "").TrimStart('\', '/')
                $errors += "$relativePath`:$lineNum`: Orphan namespace 'using $usedNamespace;' - namespace does not exist"
            }
        }
    }
}

if ($errors.Count -gt 0) {
    Write-Host "`nNAMESPACE VALIDATION FAILED:" -ForegroundColor Red
    $errors | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
    Write-Host "`nHint: Check if namespace was renamed during refactoring" -ForegroundColor Yellow
    exit 1
}

Write-Host "`nAll namespace imports validated successfully." -ForegroundColor Green
exit 0
