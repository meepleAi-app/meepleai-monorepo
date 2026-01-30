# Fix Chess game - Ensure it exists in PostgreSQL with matching UUID from Qdrant
$ErrorActionPreference = "Stop"

$gameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"
$ApiBaseUrl = "http://localhost:8080"

Write-Host "[INFO] Connecting to PostgreSQL to insert Chess game..." -ForegroundColor Cyan
Write-Host "    Game ID: $gameId" -ForegroundColor Yellow

# SQL to insert Chess game with specific UUID
$sql = @"
INSERT INTO shared_game_catalog.games (
    id, bgg_id, title, year_published, description,
    min_players, max_players, playing_time_minutes, min_age,
    complexity_rating, average_rating, image_url, thumbnail_url,
    created_at, updated_at, is_deleted, created_by_id, updated_by_id
) VALUES (
    '$gameId'::uuid,
    171,
    'Chess',
    1475,
    'Classic strategy board game for two players',
    2, 2, 30, 6,
    3.5, 7.5,
    'https://cf.geekdo-images.com/ZPnKKmC_AxcbjJi6N26-aw__original/img/chess.jpg',
    'https://cf.geekdo-images.com/ZPnKKmC_AxcbjJi6N26-aw__thumb/img/chess-thumb.jpg',
    NOW(), NOW(), false,
    '00000000-0000-0000-0000-000000000000'::uuid,
    '00000000-0000-0000-0000-000000000000'::uuid
)
ON CONFLICT (id) DO NOTHING;
"@

# Execute SQL via psql
Write-Host "[INFO] Executing SQL..." -ForegroundColor Cyan
$env:PGPASSWORD = "postgres"
$result = $sql | psql -h localhost -U postgres -d meepleai_dev -t 2>&1

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Chess game inserted/updated successfully" -ForegroundColor Green
    Write-Host "[INFO] Game ID: $gameId" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "[NEXT] Run validation: pwsh tools/run-rag-validation-20q.ps1" -ForegroundColor Cyan
} else {
    Write-Host "[ERROR] SQL execution failed" -ForegroundColor Red
    Write-Host "$result" -ForegroundColor Red
    Write-Host ""
    Write-Host "[ALT] Manual insertion:" -ForegroundColor Yellow
    Write-Host $sql -ForegroundColor White
}
