Param([switch]$Rebuild)

Set-Location $PSScriptRoot/../docker
if (Test-Path ".env") { Write-Host "Using .env" } else { Copy-Item ".env.example" ".env" }

if ($Rebuild) {
  docker compose build --no-cache
}
docker compose up -d
docker compose ps
