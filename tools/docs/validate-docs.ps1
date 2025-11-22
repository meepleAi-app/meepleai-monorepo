# Validation script for DOC-03 - CONTRIBUTING e SECURITY
# Verifies presence and content of documentation files

$ErrorActionPreference = "Stop"

Write-Host "=== DOC-03 Documentation Validation ===" -ForegroundColor Cyan
Write-Host ""

$rootPath = Split-Path -Parent $PSScriptRoot
$failureCount = 0

function Test-FileExists {
    param(
        [string]$Path,
        [string]$Description
    )

    $fullPath = Join-Path $rootPath $Path
    if (Test-Path $fullPath) {
        Write-Host "[PASS] $Description exists: $Path" -ForegroundColor Green
        return $true
    } else {
        Write-Host "[FAIL] $Description missing: $Path" -ForegroundColor Red
        $script:failureCount++
        return $false
    }
}

function Test-FileContains {
    param(
        [string]$Path,
        [string[]]$RequiredStrings,
        [string]$Description
    )

    $fullPath = Join-Path $rootPath $Path
    if (-not (Test-Path $fullPath)) {
        Write-Host "[SKIP] Cannot check content - file missing: $Path" -ForegroundColor Yellow
        return $false
    }

    $content = Get-Content $fullPath -Raw -ErrorAction SilentlyContinue
    $allFound = $true

    foreach ($required in $RequiredStrings) {
        if ($content -match [regex]::Escape($required)) {
            Write-Host "  [PASS] Contains: '$required'" -ForegroundColor Green
        } else {
            Write-Host "  [FAIL] Missing: '$required'" -ForegroundColor Red
            $script:failureCount++
            $allFound = $false
        }
    }

    return $allFound
}

# Test 1: CONTRIBUTING.md exists in root
Write-Host "Test 1: CONTRIBUTING.md in root" -ForegroundColor Yellow
$contributingExists = Test-FileExists -Path "CONTRIBUTING.md" -Description "CONTRIBUTING.md"

if ($contributingExists) {
    Write-Host "  Checking CONTRIBUTING.md content..." -ForegroundColor Yellow
    Test-FileContains -Path "CONTRIBUTING.md" -Description "CONTRIBUTING.md content" -RequiredStrings @(
        "Getting Started",
        "Development Setup",
        "Coding Standards",
        "Testing",
        "BDD",
        "Pull Request",
        "Code Review"
    )
}

Write-Host ""

# Test 2: SECURITY.md exists in root
Write-Host "Test 2: SECURITY.md in root" -ForegroundColor Yellow
$securityExists = Test-FileExists -Path "SECURITY.md" -Description "SECURITY.md"

if ($securityExists) {
    Write-Host "  Checking SECURITY.md content..." -ForegroundColor Yellow
    Test-FileContains -Path "SECURITY.md" -Description "SECURITY.md content" -RequiredStrings @(
        "Reporting a Vulnerability",
        "security vulnerability",
        "Security Policy"
    )
}

Write-Host ""

# Test 3: Issue templates exist
Write-Host "Test 3: Issue templates" -ForegroundColor Yellow
Test-FileExists -Path ".github/ISSUE_TEMPLATE/bug_report.yml" -Description "Bug report template"
Test-FileExists -Path ".github/ISSUE_TEMPLATE/feature_request.yml" -Description "Feature request template"

Write-Host ""

# Test 4: PR template exists and has BDD references
Write-Host "Test 4: Pull Request template" -ForegroundColor Yellow
$prTemplateExists = Test-FileExists -Path ".github/PULL_REQUEST_TEMPLATE.md" -Description "PR template"

if ($prTemplateExists) {
    Write-Host "  Checking PR template content..." -ForegroundColor Yellow
    Test-FileContains -Path ".github/PULL_REQUEST_TEMPLATE.md" -Description "PR template content" -RequiredStrings @(
        "BDD",
        "Test Coverage",
        "Related Issue"
    )
}

Write-Host ""

# Test 5: Cross-references between documents
Write-Host "Test 5: Document cross-references" -ForegroundColor Yellow
if ($contributingExists) {
    Write-Host "  Checking CONTRIBUTING.md links to other docs..." -ForegroundColor Yellow
    Test-FileContains -Path "CONTRIBUTING.md" -Description "CONTRIBUTING.md links" -RequiredStrings @(
        "SECURITY.md"
    )
}

Write-Host ""
Write-Host "=== Validation Summary ===" -ForegroundColor Cyan

if ($failureCount -eq 0) {
    Write-Host "All validation checks passed!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Validation failed with $failureCount error(s)" -ForegroundColor Red
    exit 1
}
