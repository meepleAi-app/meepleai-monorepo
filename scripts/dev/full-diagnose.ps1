Param()

# Move to infra directory
Set-Location $PSScriptRoot/../../infra

Write-Host "=== Compose status (full profile) ==="
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full ps

Write-Host "`n=== Non-running containers (exited/dead/created) ==="
docker ps -a --filter "status=exited" --filter "status=dead" --filter "status=created" --format "table {{.Names}}\t{{.Status}}\t{{.Image}}"

Write-Host "`n=== Services from compose file ==="
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full ps --services

# Get services and print last 200 lines of logs for each
$services = docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full ps --services 2>$null
if ($services) {
  foreach ($s in $services.Trim().Split("`n")) {
    $svc = $s.Trim()
    if ($svc) {
      Write-Host "`n----- Logs for $svc (last 200 lines) -----"
      docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full logs --tail 200 $svc 2>$null
    }
  }
} else {
  Write-Host "No services listed or failed to list services. Try running 'docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full ps' manually."
}

Write-Host "`n=== Docker disk/usage summary ==="
docker system df
