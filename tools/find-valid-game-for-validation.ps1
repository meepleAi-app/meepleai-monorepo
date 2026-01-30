# Find a game with PDF documents indexed for validation
$ErrorActionPreference = "Stop"

Write-Host "[INFO] Finding games with active PDF documents..." -ForegroundColor Cyan

# Query PostgreSQL for games with active documents
$gamesWithDocs = docker exec -i meepleai-postgres psql -U postgres -d meepleai -t -c "SELECT DISTINCT shared_game_id FROM shared_game_documents WHERE is_active = true LIMIT 10;" 2>&1

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Database query failed" -ForegroundColor Red
    Write-Host $gamesWithDocs -ForegroundColor Red
    exit 1
}

$gameIds = $gamesWithDocs -split "`n" | Where-Object { $_ -match '\S' } | ForEach-Object { $_.Trim() }

if ($gameIds.Count -eq 0) {
    Write-Host "[WARN] No games with active documents found!" -ForegroundColor Yellow
    Write-Host "[INFO] You need to upload and process a PDF rulebook first (RAG-001)" -ForegroundColor Cyan
    exit 1
}

Write-Host "[OK] Found $($gameIds.Count) games with documents" -ForegroundColor Green

# Get details for first game
$firstGameId = $gameIds[0]
Write-Host "[INFO] Using first game: $firstGameId" -ForegroundColor Cyan

$gameDetails = docker exec -i meepleai-postgres psql -U postgres -d meepleai -t -c "SELECT id, title, bgg_id FROM shared_games WHERE id = '$firstGameId'::uuid;" 2>&1

Write-Host "[OK] Game details:" -ForegroundColor Green
Write-Host $gameDetails -ForegroundColor White

# Save for updating validation questions
@{
    gameId = $firstGameId
    timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
} | ConvertTo-Json | Out-File -FilePath "tools/.valid-game-id.json" -Encoding UTF8

Write-Host ""
Write-Host "[OK] Game ID saved to: tools/.valid-game-id.json" -ForegroundColor Green
Write-Host "[NEXT] Update validation questions:" -ForegroundColor Cyan
Write-Host "    pwsh tools/update-questions-gameid.ps1 -GameId '$firstGameId'" -ForegroundColor Yellow
