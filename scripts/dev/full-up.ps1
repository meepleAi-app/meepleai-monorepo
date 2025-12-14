Param([switch]$Rebuild)

# Move to infra directory (where compose files live)
Set-Location $PSScriptRoot/../../infra

# Optionally rebuild images without cache
if ($Rebuild) {
  docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full build --no-cache
}

# Start full profile including HyperDX
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full up -d

# Show services status
docker compose -f docker-compose.yml -f docker-compose.hyperdx.yml --profile full ps
