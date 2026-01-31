# MeepleAI Cache Cleanup Script
# Automatically cleans cache directories to free disk space

param(
    [switch]$DryRun = $false,
    [switch]$Verbose = $false,
    [switch]$SkipBuild = $false,
    [switch]$Yes = $false,
    [switch]$Help = $false
)

# Show help if requested
if ($Help) {
    Write-Host "MeepleAI Cache Cleanup Script" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Usage: pwsh tools/cleanup-caches.ps1 [OPTIONS]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -DryRun             Show what would be deleted without actually deleting" -ForegroundColor Gray
    Write-Host "  -Verbose            Show detailed output" -ForegroundColor Gray
    Write-Host "  -SkipBuild          Skip build artifacts cleanup (.NET obj/bin, Next.js .next)" -ForegroundColor Gray
    Write-Host "  -Yes                Skip confirmation prompt" -ForegroundColor Gray
    Write-Host "  -Help               Show this help message" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  pwsh tools/cleanup-caches.ps1 -DryRun        # Preview cleanup" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup-caches.ps1 -Verbose       # Run with detailed output" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup-caches.ps1 -SkipBuild     # Clean only cache directories" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup-caches.ps1 -Yes           # Run without confirmation" -ForegroundColor Gray
    exit 0
}

# Function to get directory size
function Get-DirectorySize {
    param([string]$Path)

    if (Test-Path $Path) {
        $size = (Get-ChildItem -Path $Path -Recurse -Force -ErrorAction SilentlyContinue |
                 Measure-Object -Property Length -Sum -ErrorAction SilentlyContinue).Sum

        if ($null -eq $size) { return 0 }
        return $size
    }
    return 0
}

# Function to format bytes
function Format-Bytes {
    param([long]$Bytes)

    if ($Bytes -ge 1GB) {
        return "{0:N2} GB" -f ($Bytes / 1GB)
    }
    elseif ($Bytes -ge 1MB) {
        return "{0:N2} MB" -f ($Bytes / 1MB)
    }
    elseif ($Bytes -ge 1KB) {
        return "{0:N2} KB" -f ($Bytes / 1KB)
    }
    else {
        return "$Bytes B"
    }
}

# Function to clean directory
function Remove-CacheDirectory {
    param(
        [string]$Path,
        [string]$Description
    )

    if (Test-Path $Path) {
        $size = Get-DirectorySize -Path $Path
        $sizeFormatted = Format-Bytes -Bytes $size

        if ($Verbose) {
            Write-Host "Found: $Path ($sizeFormatted)" -ForegroundColor Cyan
        }

        if ($DryRun) {
            Write-Host "[DRY RUN] Would delete: $Path ($sizeFormatted) - $Description" -ForegroundColor Yellow
        }
        else {
            Write-Host "Deleting: $Path ($sizeFormatted) - $Description" -ForegroundColor Red
            try {
                Remove-Item -Path $Path -Recurse -Force -ErrorAction Stop
            }
            catch {
                Write-Host "[ERROR] Failed to delete ${Path}: $_" -ForegroundColor Magenta
            }
        }

        return $true
    }
    else {
        if ($Verbose) {
            Write-Host "Not found: $Path (skipping)" -ForegroundColor Green
        }
        return $false
    }
}

# Function to find and clean build artifacts
function Remove-BuildArtifacts {
    param(
        [string]$BaseDir,
        [string]$Pattern,
        [string]$Description
    )

    if (Test-Path $BaseDir) {
        $directories = Get-ChildItem -Path $BaseDir -Directory -Recurse -Force -ErrorAction SilentlyContinue |
                       Where-Object { $_.Name -eq $Pattern }

        if ($directories) {
            foreach ($dir in $directories) {
                Remove-CacheDirectory -Path $dir.FullName -Description $Description
            }
        }
        elseif ($Verbose) {
            Write-Host "No $Pattern directories found in $BaseDir" -ForegroundColor Green
        }
    }
}

# Main script
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║   MeepleAI Cache Cleanup                   ║" -ForegroundColor Blue
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Blue
Write-Host ""

if ($DryRun) {
    Write-Host "⚠️  DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Calculate sizes before cleanup
Write-Host "📊 Calculating current sizes..." -ForegroundColor Cyan
$beforeSize = Get-DirectorySize -Path "."

# Track what we're going to clean
$targets = @()

# Check cache directories
if (Test-Path ".serena") {
    $targets += ".serena"
}

if (Test-Path "codeql-db") {
    $targets += "codeql-db"
}

if (Test-Path ".playwright-mcp") {
    $targets += ".playwright-mcp"
}

# Check build artifacts if not skipped
if (-not $SkipBuild) {
    if (Test-Path "apps/api") {
        $objCount = (Get-ChildItem -Path "apps/api" -Directory -Recurse -Force -ErrorAction SilentlyContinue |
                     Where-Object { $_.Name -eq "obj" }).Count
        $binCount = (Get-ChildItem -Path "apps/api" -Directory -Recurse -Force -ErrorAction SilentlyContinue |
                     Where-Object { $_.Name -eq "bin" }).Count

        if ($objCount -gt 0 -or $binCount -gt 0) {
            $targets += "apps/api build artifacts"
        }
    }

    if (Test-Path "apps/web/.next") {
        $targets += "apps/web/.next"
    }
}

# Show what will be cleaned
if ($targets.Count -gt 0) {
    Write-Host "📁 Target directories:" -ForegroundColor Cyan
    foreach ($target in $targets) {
        Write-Host "  - $target"
    }
    Write-Host ""
}
else {
    Write-Host "✅ No cache directories found. Nothing to clean!" -ForegroundColor Green
    exit 0
}

# Ask for confirmation unless -Yes flag is set or dry-run mode
if (-not $DryRun -and -not $Yes) {
    Write-Host "⚠️  This will permanently delete the directories above." -ForegroundColor Yellow
    $response = Read-Host "Continue? (y/N)"

    if ($response -notmatch "^[Yy]$") {
        Write-Host "Cleanup cancelled." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# Clean cache directories
Write-Host "🧹 Cleaning cache directories..." -ForegroundColor Cyan
Write-Host ""

Remove-CacheDirectory -Path ".serena" -Description "Serena MCP cache"
Remove-CacheDirectory -Path "codeql-db" -Description "CodeQL database cache"
Remove-CacheDirectory -Path ".playwright-mcp" -Description "Playwright MCP cache"

# Clean build artifacts if not skipped
if (-not $SkipBuild) {
    Write-Host ""
    Write-Host "🏗️  Cleaning build artifacts..." -ForegroundColor Cyan
    Write-Host ""

    Remove-BuildArtifacts -BaseDir "apps/api" -Pattern "obj" -Description ".NET obj directory"
    Remove-BuildArtifacts -BaseDir "apps/api" -Pattern "bin" -Description ".NET bin directory"
    Remove-CacheDirectory -Path "apps/web/.next" -Description "Next.js build cache"
}

# Calculate sizes after cleanup
Write-Host ""
Write-Host "📊 Calculating final sizes..." -ForegroundColor Cyan
$afterSize = Get-DirectorySize -Path "."

# Calculate space freed
$freedBytes = $beforeSize - $afterSize
$freedFormatted = Format-Bytes -Bytes $freedBytes

# Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║   Cleanup Summary                          ║" -ForegroundColor Blue
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Blue
Write-Host ""

if ($DryRun) {
    Write-Host "✓ Dry run completed. No files were deleted." -ForegroundColor Yellow
    Write-Host "  Estimated space that would be freed: $freedFormatted" -ForegroundColor Yellow
}
else {
    Write-Host "✅ Cleanup complete!" -ForegroundColor Green
    Write-Host "  Space freed: $freedFormatted" -ForegroundColor Green
}

Write-Host ""

# Show recommendations
if ($SkipBuild) {
    Write-Host "💡 Tip: Run without -SkipBuild to also clean build artifacts" -ForegroundColor Cyan
}

if ($DryRun) {
    Write-Host "💡 Tip: Run without -DryRun to actually perform the cleanup" -ForegroundColor Cyan
}
