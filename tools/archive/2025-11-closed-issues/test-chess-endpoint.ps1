# Test script for CHESS-04 Chess Agent endpoint

$BaseUrl = "http://localhost:8080"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Chess Agent Endpoint (CHESS-04)" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Health Check
Write-Host "[1/4] Checking API health..." -ForegroundColor Yellow
try {
    $healthResponse = Invoke-RestMethod -Uri "$BaseUrl/health" -Method Get
    Write-Host "[OK] API is healthy" -ForegroundColor Green
    Write-Host "  Status: $($healthResponse.status)" -ForegroundColor Gray
} catch {
    Write-Host "[FAIL] API health check failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 2: Login
Write-Host "[2/4] Logging in as user@meepleai.dev..." -ForegroundColor Yellow
try {
    $loginBody = @{
        email = "user@meepleai.dev"
        password = "Demo123!"
    } | ConvertTo-Json

    $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SessionVariable session

    Write-Host "[OK] Login successful" -ForegroundColor Green

    # Extract session cookie
    $sessionCookie = $session.Cookies.GetCookies($BaseUrl) | Where-Object { $_.Name -like "*Session*" }
    if ($null -eq $sessionCookie) {
        Write-Host "[FAIL] No session cookie found!" -ForegroundColor Red
        Write-Host "  Available cookies:" -ForegroundColor Gray
        $session.Cookies.GetCookies($BaseUrl) | ForEach-Object {
            Write-Host "    - $($_.Name)" -ForegroundColor Gray
        }
        exit 1
    }

    Write-Host "  Session cookie: $($sessionCookie.Name)" -ForegroundColor Gray
} catch {
    Write-Host "[FAIL] Login failed: $_" -ForegroundColor Red
    Write-Host "  Status: $($_.Exception.Response.StatusCode)" -ForegroundColor Gray
    exit 1
}
Write-Host ""

# Step 3: Test Simple Chess Question
Write-Host "[3/4] Testing simple chess question..." -ForegroundColor Yellow
try {
    $chessBody = @{
        question = "What is en passant?"
    } | ConvertTo-Json

    $chessResponse = Invoke-WebRequest -Uri "$BaseUrl/agents/chess" `
        -Method Post `
        -Body $chessBody `
        -ContentType "application/json" `
        -WebSession $session

    Write-Host "[OK] Chess agent responded successfully" -ForegroundColor Green

    $responseData = $chessResponse.Content | ConvertFrom-Json
    Write-Host "  Answer preview: $($responseData.answer.Substring(0, [Math]::Min(100, $responseData.answer.Length)))..." -ForegroundColor Gray
    Write-Host "  Sources: $($responseData.sources.Count)" -ForegroundColor Gray
    Write-Host "  Suggested moves: $($responseData.suggestedMoves.Count)" -ForegroundColor Gray
    Write-Host "  Token usage: $($responseData.totalTokens) total (prompt: $($responseData.promptTokens), completion: $($responseData.completionTokens))" -ForegroundColor Gray
} catch {
    Write-Host "[FAIL] Chess agent request failed" -ForegroundColor Red
    Write-Host "  Status: $($_.Exception.Response.StatusCode.value__) $($_.Exception.Response.StatusCode)" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray

    if ($_.Exception.Response.StatusCode.value__ -eq 401) {
        Write-Host ""
        Write-Host "  This is an authentication issue. The session cookie might not be working." -ForegroundColor Yellow
    }
    exit 1
}
Write-Host ""

# Step 4: Test Position Analysis
Write-Host "[4/4] Testing FEN position analysis..." -ForegroundColor Yellow
try {
    $positionBody = @{
        question = "What should White play in this position?"
        fenPosition = "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1"
    } | ConvertTo-Json

    $positionResponse = Invoke-WebRequest -Uri "$BaseUrl/agents/chess" `
        -Method Post `
        -Body $positionBody `
        -ContentType "application/json" `
        -WebSession $session

    Write-Host "[OK] Position analysis completed successfully" -ForegroundColor Green

    $positionData = $positionResponse.Content | ConvertFrom-Json
    Write-Host "  Answer preview: $($positionData.answer.Substring(0, [Math]::Min(100, $positionData.answer.Length)))..." -ForegroundColor Gray
    if ($null -ne $positionData.analysis) {
        Write-Host "  Analysis: $($positionData.analysis.evaluationSummary)" -ForegroundColor Gray
        Write-Host "  Key considerations: $($positionData.analysis.keyConsiderations.Count)" -ForegroundColor Gray
    }
    Write-Host "  Suggested moves: $($positionData.suggestedMoves.Count)" -ForegroundColor Gray
} catch {
    Write-Host "[FAIL] Position analysis failed" -ForegroundColor Red
    Write-Host "  Status: $($_.Exception.Response.StatusCode.value__) $($_.Exception.Response.StatusCode)" -ForegroundColor Gray
    Write-Host "  Error: $($_.Exception.Message)" -ForegroundColor Gray
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "All tests passed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
