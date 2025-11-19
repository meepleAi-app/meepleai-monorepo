Param([switch]$Rebuild)

Set-Location $PSScriptRoot/../infra

foreach ($file in @('api', 'web', 'n8n')) {
  $target = "./env/$file.env.dev"
  $source = "./env/$file.env.dev.example"
  if (-not (Test-Path $target) -and (Test-Path $source)) {
    Write-Host "Creating $target from template"
    Copy-Item $source $target
  }
}

if ($Rebuild) {
  docker compose build --no-cache
}
docker compose up -d

$pgUser = if ($env:POSTGRES_USER) { $env:POSTGRES_USER } else { 'meeple' }
$pgDb = if ($env:POSTGRES_DB) { $env:POSTGRES_DB } else { 'meepleai' }
$migrationScript = "./init/api-migrations-20251118.sql"

if (Test-Path $migrationScript) {
  Write-Host "Applying EF Core migrations ($migrationScript) to Postgres..."
  try {
    $composeArgs = @('compose','exec','-T','meepleai-postgres','psql','-U',$pgUser,'-d',$pgDb,'-v','ON_ERROR_STOP=1')
    Get-Content $migrationScript -Raw | & docker @composeArgs
    Write-Host "Database is up-to-date."
  } catch {
    Write-Warning "Database migration script failed. Inspect logs and rerun if needed."
  }
} else {
  Write-Warning "Migration script not found at $migrationScript. Skipping DB update."
}

docker compose ps
