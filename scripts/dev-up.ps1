Param([switch]$Rebuild)

Set-Location $PSScriptRoot/../infra

foreach ($file in @('api', 'web', 'n8n')) {
  $target = "./env/$file.env"
  $source = "./env/$file.env.example"
  if (-not (Test-Path $target) -and (Test-Path $source)) {
    Write-Host "Creating $target from template"
    Copy-Item $source $target
  }
}

if ($Rebuild) {
  docker compose build --no-cache
}
docker compose up -d
docker compose ps
