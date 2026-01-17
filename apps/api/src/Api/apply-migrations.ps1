$env:POSTGRES_HOST = "localhost"
$env:POSTGRES_PORT = "5432"
$env:POSTGRES_DB = "meepleai"
$env:POSTGRES_USER = "postgres"
$env:POSTGRES_PASSWORD = "DevPassword123"

Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ Migrations applied successfully!" -ForegroundColor Green
} else {
    Write-Host "❌ Migration failed with exit code $LASTEXITCODE" -ForegroundColor Red
    exit $LASTEXITCODE
}
