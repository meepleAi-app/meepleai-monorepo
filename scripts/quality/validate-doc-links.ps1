# Documentation Link Validator (PowerShell)
# Tests all markdown links in INDEX.md and README.md

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

# Dynamic path resolution
$ScriptDir = Split-Path -Parent $PSCommandPath
$ProjectRoot = Split-Path -Parent (Split-Path -Parent $ScriptDir)
$DocsDir = Join-Path $ProjectRoot 'docs'
$ErrorCount = 0

Write-Host "🔍 Validating documentation links..." -ForegroundColor Blue
Write-Host ""

# Function to validate a link
function Test-DocumentLink {
    param(
        [string]$MarkdownFile,
        [string]$Link
    )

    $baseDir = Split-Path -Parent $MarkdownFile

    # Skip external URLs
    if ($Link -match '^https?://') {
        return $true
    }

    # Skip anchors
    if ($Link -match '^#') {
        return $true
    }

    # Construct full path
    if ($Link -match '^\.\.') {
        # Parent directory
        $fullPath = Join-Path $baseDir $Link
    }
    elseif ($Link -match '^\.') {
        # Current directory
        $fullPath = Join-Path $baseDir $Link
    }
    else {
        # Relative to base
        $fullPath = Join-Path $baseDir $Link
    }

    # Normalize path
    $fullPath = [System.IO.Path]::GetFullPath($fullPath)

    # Check if file/directory exists
    if (-not (Test-Path $fullPath)) {
        Write-Host "❌ BROKEN LINK in $MarkdownFile:" -ForegroundColor Red
        Write-Host "   Link: $Link" -ForegroundColor Red
        Write-Host "   Expected: $fullPath" -ForegroundColor Red
        $script:ErrorCount++
        return $false
    }

    return $true
}

# Function to extract markdown links
function Get-MarkdownLinks {
    param([string]$FilePath)

    if (-not (Test-Path $FilePath)) {
        return @()
    }

    $content = Get-Content -Path $FilePath -Raw

    # Regex to match markdown links: [text](link)
    # Exclude anchors (starting with #)
    $pattern = '\[.*?\]\(([^)#]+)\)'

    $matches = [regex]::Matches($content, $pattern)

    $links = @()
    foreach ($match in $matches) {
        if ($match.Groups.Count -ge 2) {
            $links += $match.Groups[1].Value
        }
    }

    return $links
}

# Validate INDEX.md
Write-Host "📄 Validating INDEX.md..." -ForegroundColor Cyan
$indexPath = Join-Path $DocsDir 'INDEX.md'

if (Test-Path $indexPath) {
    $links = Get-MarkdownLinks -FilePath $indexPath
    foreach ($link in $links) {
        Test-DocumentLink -MarkdownFile $indexPath -Link $link | Out-Null
    }
}
else {
    Write-Host "⚠️  INDEX.md not found" -ForegroundColor Yellow
}

Write-Host ""

# Validate README.md
Write-Host "📄 Validating README.md..." -ForegroundColor Cyan
$readmePath = Join-Path $DocsDir 'README.md'

if (Test-Path $readmePath) {
    $links = Get-MarkdownLinks -FilePath $readmePath
    foreach ($link in $links) {
        Test-DocumentLink -MarkdownFile $readmePath -Link $link | Out-Null
    }
}
else {
    Write-Host "⚠️  README.md not found" -ForegroundColor Yellow
}

Write-Host ""

# Summary
if ($ErrorCount -eq 0) {
    Write-Host "✅ All links validated successfully!" -ForegroundColor Green
    exit 0
}
else {
    Write-Host "❌ Found $ErrorCount broken links" -ForegroundColor Red
    exit 1
}
