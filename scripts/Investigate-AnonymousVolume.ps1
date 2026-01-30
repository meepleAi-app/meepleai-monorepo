# Investigate-AnonymousVolume.ps1
# PowerShell script to investigate anonymous Docker volume

param(
    [string]$VolumeHash = "26cb8e29619dcb476170ddc27a5ca7ddba922d77cb5e98bef083e1fd45f7bf8f"
)

Write-Host "🔍 Investigating Anonymous Volume" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
Write-Host "Volume: $VolumeHash" -ForegroundColor Yellow
Write-Host ""

# 1. Check if volume exists
Write-Host "1️⃣ Checking volume existence..." -ForegroundColor Cyan
try {
    $volumeInfo = docker volume inspect $VolumeHash 2>$null | ConvertFrom-Json
    if ($null -eq $volumeInfo) {
        Write-Host "❌ Volume not found!" -ForegroundColor Red
        exit 1
    }
    Write-Host "✅ Volume exists" -ForegroundColor Green
}
catch {
    Write-Host "❌ Error inspecting volume: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# 2. Display metadata
Write-Host "2️⃣ Volume Metadata:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
$volumeInfo | Select-Object Name, Driver, Mountpoint, CreatedAt, @{N='Labels';E={$_.Labels | ConvertTo-Json}} | Format-List
Write-Host ""

# 3. Docker Compose association
Write-Host "3️⃣ Docker Compose Association:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
$project = $volumeInfo.Labels.'com.docker.compose.project'
$service = $volumeInfo.Labels.'com.docker.compose.volume'
Write-Host "Project: $($project ?? 'none')" -ForegroundColor $(if ($project) { 'Green' } else { 'Yellow' })
Write-Host "Service: $($service ?? 'none')" -ForegroundColor $(if ($service) { 'Green' } else { 'Yellow' })
Write-Host ""

# 4. Find containers using this volume
Write-Host "4️⃣ Containers Using This Volume:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
$containers = docker ps -a --format "{{.ID}}"
$foundContainer = $false

foreach ($containerId in $containers) {
    $containerInfo = docker inspect $containerId | ConvertFrom-Json
    $containerName = $containerInfo.Name -replace '^/', ''
    $containerImage = $containerInfo.Config.Image

    foreach ($mount in $containerInfo.Mounts) {
        if ($mount.Name -eq $VolumeHash) {
            Write-Host "✅ FOUND: $containerName ($containerImage)" -ForegroundColor Green
            Write-Host "   Status: $($containerInfo.State.Status)" -ForegroundColor Yellow
            Write-Host "   Source: $($mount.Source)"
            Write-Host "   Destination: $($mount.Destination)"
            Write-Host "   Read/Write: $($mount.RW)"
            $foundContainer = $true
        }
    }
}

if (-not $foundContainer) {
    Write-Host "⚠️  No containers currently using this volume (DANGLING)" -ForegroundColor Yellow
}
Write-Host ""

# 5. Volume contents
Write-Host "5️⃣ Volume Contents (first 20 items):" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
docker run --rm -v "${VolumeHash}:/data" alpine ls -lah /data 2>$null | Select-Object -First 20
Write-Host ""

# 6. File analysis
Write-Host "6️⃣ File Type Analysis:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
$fileCount = docker run --rm -v "${VolumeHash}:/data" alpine find /data -type f 2>$null | Measure-Object -Line | Select-Object -ExpandProperty Lines
$totalSize = docker run --rm -v "${VolumeHash}:/data" alpine du -sh /data 2>$null | ForEach-Object { ($_ -split '\s+')[0] }
Write-Host "Total files: $fileCount"
Write-Host "Total size: $totalSize"
Write-Host ""

Write-Host "File patterns detected:"
docker run --rm -v "${VolumeHash}:/data" alpine find /data -type f \( -name "*.db" -o -name "PG_VERSION" -o -name "*.pdf" -o -name "*.json" -o -name "*.log" \) 2>$null | Select-Object -First 10
Write-Host ""

# 7. Search docker-compose for missing volume declarations
Write-Host "7️⃣ Checking docker-compose.yml Configuration:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
Push-Location "$PSScriptRoot\..\infra"

$composeServices = docker compose config 2>$null | Select-String -Pattern "^\s{2}[a-z]" | ForEach-Object { ($_.Line -replace ':', '').Trim() }
Write-Host "Services in docker-compose:"
$composeServices | ForEach-Object { Write-Host "  - $_" }
Write-Host ""

Pop-Location

# 8. Check Dockerfiles
Write-Host "8️⃣ Scanning Dockerfiles for VOLUME Directives:" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
$dockerfiles = Get-ChildItem -Path "$PSScriptRoot\..\apps" -Filter "Dockerfile" -Recurse
if ($dockerfiles.Count -gt 0) {
    foreach ($dockerfile in $dockerfiles) {
        $volumeDirectives = Select-String -Path $dockerfile.FullName -Pattern "^VOLUME" -AllMatches
        if ($volumeDirectives) {
            Write-Host "File: $($dockerfile.FullName)" -ForegroundColor Yellow
            $volumeDirectives | ForEach-Object { Write-Host "  $($_.Line)" }
        }
    }
} else {
    Write-Host "✅ No VOLUME directives in custom Dockerfiles" -ForegroundColor Green
}
Write-Host ""

# 9. Summary and recommendations
Write-Host ("═" * 70) -ForegroundColor Cyan
Write-Host "📋 SUMMARY & RECOMMENDATIONS" -ForegroundColor Cyan
Write-Host ("═" * 70) -ForegroundColor Cyan
Write-Host ""

# Check if dangling
$isDangling = docker volume ls --filter "dangling=true" --format "{{.Name}}" | Select-String -Pattern $VolumeHash

if ($isDangling) {
    Write-Host "Status: ⚠️  DANGLING (not used by any container)" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "✅ SAFE TO REMOVE" -ForegroundColor Green
    Write-Host ""
    Write-Host "Recommended actions:" -ForegroundColor Cyan
    Write-Host "  1. Backup (safety first):"
    Write-Host "     docker run --rm -v ${VolumeHash}:/src:ro -v D:/backups:/dst alpine tar czf /dst/anonymous-volume-backup-$(Get-Date -Format 'yyyyMMdd').tar.gz -C /src ." -ForegroundColor White
    Write-Host ""
    Write-Host "  2. Remove:"
    Write-Host "     docker volume rm $VolumeHash" -ForegroundColor White
    Write-Host ""
} else {
    Write-Host "Status: ⚠️  IN USE by a container" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "⚠️  NOT SAFE TO REMOVE" -ForegroundColor Red
    Write-Host ""
    Write-Host "Recommended actions:" -ForegroundColor Cyan
    Write-Host "  1. Identify the container (see section 4 above)"
    Write-Host "  2. Stop the container"
    Write-Host "  3. Backup volume data"
    Write-Host "  4. Create named volume in docker-compose.yml"
    Write-Host "  5. Migrate data to named volume"
    Write-Host "  6. Update service to use named volume"
    Write-Host "  7. Remove anonymous volume"
    Write-Host ""
}

# Likely culprit analysis
Write-Host "🔎 Likely Culprit Analysis:" -ForegroundColor Cyan
Write-Host ""

if ($service -eq "none" -and $project -eq "none") {
    Write-Host "  Created by: Manual 'docker run' command (not docker-compose)" -ForegroundColor Yellow
    Write-Host "  Cause: Someone ran 'docker run -v /path ...' without volume name"
} elseif ($project -eq "meepleai") {
    Write-Host "  Created by: Docker Compose (project: meepleai)" -ForegroundColor Yellow
    Write-Host "  Service: $service"
    Write-Host "  Cause: Missing volume declaration in docker-compose.yml"
    Write-Host ""
    Write-Host "  Fix: Add to docker-compose.yml:"
    Write-Host "    volumes:"
    Write-Host "      ${service}_data:/path/to/mount"
    Write-Host ""
    Write-Host "    volumes:"
    Write-Host "      ${service}_data:"
} else {
    Write-Host "  Created by: Unknown (check Labels above)" -ForegroundColor Yellow
}

Write-Host ""
Write-Host ("═" * 70) -ForegroundColor Cyan
Write-Host "Investigation complete! 🎯" -ForegroundColor Green
