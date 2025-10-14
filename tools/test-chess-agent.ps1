# Test Chess Agent with real questions
$BaseUrl = "http://localhost:8080"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Testing Chess Agent" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Login
Write-Host "[1/4] Logging in..." -ForegroundColor Yellow
$loginBody = @{
    email = "admin@meepleai.dev"
    password = "Demo123!"
} | ConvertTo-Json

try {
    $session = $null
    $loginResponse = Invoke-WebRequest -Uri "$BaseUrl/auth/login" `
        -Method Post `
        -Body $loginBody `
        -ContentType "application/json" `
        -SessionVariable session `
        -UseBasicParsing
    Write-Host "[OK] Logged in" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Login failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Test Questions
$questions = @(
    @{
        Question = "What is the Italian Game opening?"
        Description = "Testing knowledge retrieval about chess openings"
    },
    @{
        Question = "How do I perform an en passant capture?"
        Description = "Testing knowledge about special chess rules"
    },
    @{
        Question = "What is a fork tactic?"
        Description = "Testing knowledge about chess tactics"
    }
)

$testNumber = 2
foreach ($q in $questions) {
    Write-Host "[$testNumber/4] Testing: $($q.Description)" -ForegroundColor Yellow
    Write-Host "  Question: '$($q.Question)'" -ForegroundColor Gray

    $requestBody = @{
        question = $q.Question
    } | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$BaseUrl/agents/chess" `
            -Method Post `
            -Body $requestBody `
            -ContentType "application/json" `
            -WebSession $session `
            -TimeoutSec 30

        Write-Host "[OK] Response received" -ForegroundColor Green
        Write-Host "  Answer preview: $($response.answer.Substring(0, [Math]::Min(150, $response.answer.Length)))..." -ForegroundColor Cyan
        Write-Host "  Sources found: $($response.sources.Count)" -ForegroundColor Gray
        Write-Host "  Confidence: $($response.confidence)" -ForegroundColor Gray
        Write-Host "  Tokens used: $($response.totalTokens)" -ForegroundColor Gray
        Write-Host ""
    } catch {
        Write-Host "[FAIL] Request failed: $_" -ForegroundColor Red
        Write-Host ""
    }

    $testNumber++
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Chess Agent Testing Complete!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Cyan
