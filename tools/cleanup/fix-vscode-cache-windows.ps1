# Fix VS Code Cache Errors on Windows
# Resolves "Unable to move the cache: Accesso negato (0x5)" errors

param(
    [switch]$DryRun = $false,
    [switch]$Verbose = $false,
    [switch]$Yes = $false,
    [switch]$Help = $false
)

# Show help if requested
if ($Help) {
    Write-Host "Fix VS Code Cache Errors on Windows" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "This script fixes VS Code/Electron cache permission errors on Windows:" -ForegroundColor White
    Write-Host "  - ERROR:net\disk_cache\cache_util_win.cc:20] Unable to move the cache" -ForegroundColor Gray
    Write-Host "  - Accesso negato (0x5) / Access Denied errors" -ForegroundColor Gray
    Write-Host "  - GPU cache creation failures" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Usage: pwsh tools/cleanup/fix-vscode-cache-windows.ps1 [OPTIONS]" -ForegroundColor White
    Write-Host ""
    Write-Host "Options:" -ForegroundColor Yellow
    Write-Host "  -DryRun    Show what would be deleted without actually deleting" -ForegroundColor Gray
    Write-Host "  -Verbose   Show detailed output" -ForegroundColor Gray
    Write-Host "  -Yes       Skip confirmation prompt" -ForegroundColor Gray
    Write-Host "  -Help      Show this help message" -ForegroundColor Gray
    Write-Host ""
    Write-Host "What this script does:" -ForegroundColor Yellow
    Write-Host "  1. Closes all VS Code instances" -ForegroundColor Gray
    Write-Host "  2. Cleans VS Code cache directories" -ForegroundColor Gray
    Write-Host "  3. Cleans Chromium/Electron cache" -ForegroundColor Gray
    Write-Host "  4. Removes lock files" -ForegroundColor Gray
    Write-Host ""
    Write-Host "Examples:" -ForegroundColor Yellow
    Write-Host "  pwsh tools/cleanup/fix-vscode-cache-windows.ps1 -DryRun    # Preview" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup/fix-vscode-cache-windows.ps1            # Fix cache issues" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup/fix-vscode-cache-windows.ps1 -Yes       # Skip confirmation" -ForegroundColor Gray
    Write-Host "  pwsh tools/cleanup/fix-vscode-cache-windows.ps1 -Verbose   # Detailed output" -ForegroundColor Gray
    exit 0
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

# Function to close VS Code instances
function Close-VSCodeInstances {
    if ($DryRun) {
        # Count all VS Code related processes
        $allProcesses = Get-Process | Where-Object { $_.ProcessName -like "Code*" } -ErrorAction SilentlyContinue
        if ($allProcesses) {
            Write-Host "[DRY RUN] Would close $($allProcesses.Count) VS Code related process(es)" -ForegroundColor Yellow
            if ($Verbose) {
                foreach ($proc in $allProcesses) {
                    Write-Host "  - $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Gray
                }
            }
        }
        else {
            Write-Host "[DRY RUN] No VS Code instances running" -ForegroundColor Yellow
        }
        return $allProcesses.Count
    }

    Write-Host "Closing VS Code instances..." -ForegroundColor Cyan

    # Get all VS Code related processes (main process and helpers)
    $processes = Get-Process | Where-Object { $_.ProcessName -like "Code*" } -ErrorAction SilentlyContinue

    if ($processes) {
        $closedCount = 0
        foreach ($proc in $processes) {
            if ($Verbose) {
                Write-Host "  Closing $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Gray
            }

            try {
                # Try graceful close first
                $proc.CloseMainWindow() | Out-Null
                Start-Sleep -Milliseconds 500

                # Refresh process state to check if it actually exited
                $proc.Refresh()

                # If still running, force kill
                if (!$proc.HasExited) {
                    $proc.Kill()
                    Start-Sleep -Milliseconds 200
                }

                Write-Host "  ✓ Closed $($proc.ProcessName) (PID: $($proc.Id))" -ForegroundColor Green
                $closedCount++
            }
            catch {
                Write-Host "  ⚠ Failed to close $($proc.ProcessName) (PID: $($proc.Id)): $_" -ForegroundColor Yellow
            }
        }

        # Wait for processes to fully exit
        Start-Sleep -Seconds 2

        return $closedCount
    }
    else {
        Write-Host "  No VS Code instances running" -ForegroundColor Green
        return 0
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
                Write-Host "  ✓ Deleted successfully" -ForegroundColor Green
            }
            catch {
                Write-Host "  ⚠ Failed to delete ${Path}: $_" -ForegroundColor Magenta
                Write-Host "  Tip: Try running as Administrator or close all applications" -ForegroundColor Yellow
            }
        }

        return $size
    }
    else {
        if ($Verbose) {
            Write-Host "Not found: $Path (skipping)" -ForegroundColor Green
        }
        return 0
    }
}

# Main script
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║   Fix VS Code Cache Errors (Windows)       ║" -ForegroundColor Blue
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Blue
Write-Host ""

# Check if running on Windows
if ($PSVersionTable.Platform -and $PSVersionTable.Platform -ne "Win32NT") {
    Write-Host "❌ Error: This script is only for Windows" -ForegroundColor Red
    Write-Host "   Current platform: $($PSVersionTable.Platform)" -ForegroundColor Yellow
    exit 1
}

# Check if VS Code is installed
$appData = [Environment]::GetFolderPath('ApplicationData')
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')

if (-not (Test-Path "$appData\Code")) {
    Write-Host "⚠️  Warning: VS Code directory not found at $appData\Code" -ForegroundColor Yellow
    Write-Host "   VS Code may not be installed or uses a different location" -ForegroundColor Gray
    Write-Host ""
    $response = Read-Host "Continue anyway? (y/N)"
    if ($response -notmatch "^[Yy]$") {
        Write-Host "Cancelled." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# Check if running as Administrator
$isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin -and -not $DryRun) {
    Write-Host "⚠️  Not running as Administrator" -ForegroundColor Yellow
    Write-Host "   Some operations may fail due to insufficient permissions" -ForegroundColor Gray
    Write-Host "   Tip: Right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Cyan
    Write-Host ""
}

if ($DryRun) {
    Write-Host "⚠️  DRY RUN MODE - No files will be deleted" -ForegroundColor Yellow
    Write-Host ""
}

# Check for running VS Code instances and warn about unsaved work
$runningProcesses = Get-Process | Where-Object { $_.ProcessName -like "Code*" } -ErrorAction SilentlyContinue
if ($runningProcesses -and -not $DryRun -and -not $Yes) {
    Write-Host "⚠️  WARNING: VS Code is currently running" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This script will:" -ForegroundColor White
    Write-Host "  1. Close all VS Code instances (may lose unsaved work)" -ForegroundColor Gray
    Write-Host "  2. Delete cache directories (safe, but regenerated on restart)" -ForegroundColor Gray
    Write-Host "  3. Remove lock files" -ForegroundColor Gray
    Write-Host ""
    Write-Host "💡 Recommendation: Save all your work in VS Code before continuing!" -ForegroundColor Cyan
    Write-Host ""
    $response = Read-Host "Continue? (y/N)"

    if ($response -notmatch "^[Yy]$") {
        Write-Host "Cancelled. Please save your work and run the script again." -ForegroundColor Yellow
        exit 0
    }
    Write-Host ""
}

# Step 1: Close VS Code
Write-Host "📋 Step 1: Closing VS Code instances" -ForegroundColor Cyan
Write-Host ""
$closedCount = Close-VSCodeInstances
Write-Host ""

# Step 2: Clean cache directories
Write-Host "🧹 Step 2: Cleaning VS Code cache directories" -ForegroundColor Cyan
Write-Host ""

$totalFreed = 0

# VS Code cache directories
$appData = [Environment]::GetFolderPath('ApplicationData')
$localAppData = [Environment]::GetFolderPath('LocalApplicationData')

$cachePaths = @(
    @{Path = "$appData\Code\Cache"; Description = "VS Code Cache"},
    @{Path = "$appData\Code\CachedData"; Description = "VS Code Cached Data"},
    @{Path = "$appData\Code\GPUCache"; Description = "VS Code GPU Cache"},
    @{Path = "$appData\Code\Code Cache"; Description = "VS Code Code Cache"},
    @{Path = "$localAppData\Microsoft\vscode-cpptools"; Description = "VS Code C++ Tools Cache"}
)

foreach ($cache in $cachePaths) {
    $freed = Remove-CacheDirectory -Path $cache.Path -Description $cache.Description
    $totalFreed += $freed
}

Write-Host ""

# Step 3: Clean Chromium/Electron cache
Write-Host "🧹 Step 3: Cleaning Chromium/Electron cache" -ForegroundColor Cyan
Write-Host ""

$chromiumCachePaths = @(
    @{Path = "$localAppData\Chromium\User Data\Default\Cache"; Description = "Chromium Cache"},
    @{Path = "$localAppData\Chromium\User Data\Default\GPUCache"; Description = "Chromium GPU Cache"},
    @{Path = "$localAppData\Chromium\User Data\ShaderCache"; Description = "Chromium Shader Cache"}
)

foreach ($cache in $chromiumCachePaths) {
    $freed = Remove-CacheDirectory -Path $cache.Path -Description $cache.Description
    $totalFreed += $freed
}

Write-Host ""

# Step 4: Remove lock files
Write-Host "🔓 Step 4: Removing lock files" -ForegroundColor Cyan
Write-Host ""

$lockFiles = @(
    "$appData\Code\SingletonLock",
    "$appData\Code\SingletonCookie",
    "$appData\Code\SingletonSocket"
)

foreach ($lockFile in $lockFiles) {
    if (Test-Path $lockFile) {
        if ($DryRun) {
            Write-Host "[DRY RUN] Would delete: $lockFile" -ForegroundColor Yellow
        }
        else {
            try {
                Remove-Item -Path $lockFile -Force -ErrorAction Stop
                Write-Host "  ✓ Removed lock file: $lockFile" -ForegroundColor Green
            }
            catch {
                Write-Host "  ⚠ Failed to remove: $lockFile" -ForegroundColor Yellow
            }
        }
    }
    elseif ($Verbose) {
        Write-Host "  Not found: $lockFile" -ForegroundColor Gray
    }
}

Write-Host ""

# Summary
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Blue
Write-Host "║   Summary                                  ║" -ForegroundColor Blue
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Blue
Write-Host ""

$totalFreedFormatted = Format-Bytes -Bytes $totalFreed

if ($DryRun) {
    Write-Host "✓ Dry run completed. No files were deleted." -ForegroundColor Yellow
    Write-Host "  Estimated space that would be freed: $totalFreedFormatted" -ForegroundColor Yellow
    Write-Host "  VS Code processes that would be closed: $closedCount" -ForegroundColor Yellow
}
else {
    Write-Host "✅ Cache cleanup complete!" -ForegroundColor Green
    Write-Host "  Space freed: $totalFreedFormatted" -ForegroundColor Green
    Write-Host "  VS Code processes closed: $closedCount" -ForegroundColor Green
}

Write-Host ""

# Recommendations
Write-Host "💡 Next steps:" -ForegroundColor Cyan
Write-Host "  1. Restart VS Code" -ForegroundColor Gray
Write-Host "  2. If issues persist, run as Administrator" -ForegroundColor Gray
Write-Host "  3. Check antivirus exclusions for VS Code directories" -ForegroundColor Gray
Write-Host ""

Write-Host "💡 To prevent future issues:" -ForegroundColor Cyan
Write-Host "  - Close VS Code properly (File > Exit)" -ForegroundColor Gray
Write-Host "  - Add VS Code directories to antivirus exclusions" -ForegroundColor Gray
Write-Host "  - Avoid running multiple instances from different terminals" -ForegroundColor Gray
Write-Host ""

# Exit with appropriate code
if ($DryRun) {
    exit 0
}
else {
    # Check if VS Code can be started
    $vscodeExe = "$localAppData\Programs\Microsoft VS Code\Code.exe"
    if (Test-Path $vscodeExe) {
        Write-Host "✅ VS Code executable found at: $vscodeExe" -ForegroundColor Green
        exit 0
    }
    else {
        Write-Host "⚠️  Could not verify VS Code installation" -ForegroundColor Yellow
        exit 0
    }
}
