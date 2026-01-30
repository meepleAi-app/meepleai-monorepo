# Check if Chess game exists
$ApiBaseUrl = "http://localhost:8080"

# Login
$loginBody = @{ email = "admin@meepleai.dev"; password = "pVKOMQNK0tFNgGlX" } | ConvertTo-Json
$login = Invoke-WebRequest -Uri "$ApiBaseUrl/api/v1/auth/login" -Method Post -ContentType "application/json" -Body $loginBody -UseBasicParsing
$cookie = ($login.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=' }) -replace ';.*',''

Write-Host "[OK] Logged in" -ForegroundColor Green

# Get games
$games = Invoke-RestMethod -Uri "$ApiBaseUrl/api/v1/shared-games?pageSize=100" -Method Get -Headers @{ "Cookie" = $cookie }

$chess = $games.games | Where-Object { $_.title -match 'Chess' } | Select-Object -First 1

if ($chess) {
    Write-Host "[OK] Chess game found:" -ForegroundColor Green
    Write-Host "    ID: $($chess.id)" -ForegroundColor Yellow
    Write-Host "    Title: $($chess.title)" -ForegroundColor White
    Write-Host "    BGG ID: $($chess.bggId)" -ForegroundColor Gray

    # Save to file for updating validation questions
    $chess.id | Out-File -FilePath "tools/.chess-game-id" -Encoding UTF8 -NoNewline
    Write-Host "[INFO] Game ID saved to: tools/.chess-game-id" -ForegroundColor Cyan
} else {
    Write-Host "[WARN] No Chess game found in catalog!" -ForegroundColor Yellow
    Write-Host "[INFO] Available games:" -ForegroundColor Cyan
    $games.games | Select-Object -First 5 | ForEach-Object {
        Write-Host "    - $($_.title) ($($_.id))" -ForegroundColor Gray
    }
}
