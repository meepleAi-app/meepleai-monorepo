<#
.SYNOPSIS
    Cleanup anonymous Docker volumes created by Testcontainers

.DESCRIPTION
    Removes orphaned anonymous Docker volumes with hash names (64 hex chars)
    that are not used by any container. Preserves named volumes like
    infra_pgdata, infra_qdrantdata, claude-memory, etc.

.EXAMPLE
    .\cleanup-anonymous-volumes.ps1

.NOTES
    Author: MeepleAI Team
    Issue: #2513 - Prevent uncontrolled Docker volume creation
    Related: cleanup-testcontainers.ps1 (handles labeled Testcontainers)
#>

Write-Host "🧹 Cleaning up anonymous Docker volumes..." -ForegroundColor Cyan
Write-Host ""

# Check if Docker is available
$dockerAvailable = Get-Command docker -ErrorAction SilentlyContinue

if (-not $dockerAvailable) {
    Write-Host "   ⚠️  Docker not found, cannot cleanup volumes" -ForegroundColor Yellow
    exit 0
}

# Get all Docker volumes
$allVolumes = docker volume ls -q 2>$null

if (-not $allVolumes) {
    Write-Host "   ✅ No Docker volumes found" -ForegroundColor Green
    exit 0
}

$danglingVolumes = @()

foreach ($volume in $allVolumes) {
    # Check if this is an anonymous volume (64 hexadecimal characters)
    if ($volume -match '^[a-f0-9]{64}$') {
        # Check if it's used by any container (running or stopped)
        $inUse = docker ps -a --filter "volume=$volume" -q 2>$null

        if (-not $inUse) {
            $danglingVolumes += $volume
        }
    }
}

if ($danglingVolumes.Count -eq 0) {
    Write-Host "   ✅ No orphaned anonymous volumes found" -ForegroundColor Green
} else {
    Write-Host "   Found $($danglingVolumes.Count) orphaned anonymous volume(s):" -ForegroundColor Yellow
    Write-Host ""

    foreach ($volume in $danglingVolumes) {
        $shortId = $volume.Substring(0, 12)
        Write-Host "   🗑️  Removing: $shortId..." -ForegroundColor Gray

        $removeResult = docker volume rm $volume 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-Host "      ✅ Removed successfully" -ForegroundColor Green
        } else {
            Write-Host "      ⚠️  Failed to remove: $removeResult" -ForegroundColor Yellow
        }
    }

    Write-Host ""
    Write-Host "   ✅ Cleanup complete! Removed $($danglingVolumes.Count) volume(s)" -ForegroundColor Green
}

Write-Host ""
Write-Host "Current Docker volumes:" -ForegroundColor Cyan
docker volume ls --format 'table {{.Name}}\t{{.Driver}}' 2>$null | Select-Object -First 10
