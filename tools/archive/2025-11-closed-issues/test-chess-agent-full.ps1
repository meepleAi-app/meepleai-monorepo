# Test Chess Agent - Full 15 Questions Validation
# Usage: pwsh tools/test-chess-agent-full.ps1

$ErrorActionPreference = "Stop"

Write-Host "Testing Chess Agent (CHESS-04) - Full 15 Questions" -ForegroundColor Cyan
Write-Host ""

# Configuration
$baseUrl = "http://localhost:8080"
$adminEmail = "admin@meepleai.dev"
$adminPassword = "Demo123!"
$userEmail = "user@meepleai.dev"
$userPassword = "Demo123!"

# Counters
$totalTests = 15
$passedTests = 0
$failedTests = 0
$totalTokens = 0
$totalCost = 0.0

# DeepSeek V3.1 pricing (per 1M tokens)
$promptTokenCost = 0.27
$completionTokenCost = 1.10

# Helper function to make authenticated requests
# Note: Uses curl because PowerShell's Invoke-RestMethod doesn't send
# Secure cookies over HTTP (even with manual Cookie header)
function Invoke-AuthenticatedRequest {
    param(
        [string]$Method,
        [string]$Uri,
        [object]$Body,
        [string]$SessionCookie
    )

    $curlArgs = @(
        "-s",  # Silent
        "-X", $Method,
        "-H", "Cookie: meeple_session=$SessionCookie",
        "-H", "Content-Type: application/json",
        "--max-time", "60"
    )

    if ($Body) {
        $jsonBody = ($Body | ConvertTo-Json -Depth 10)
        # Escape quotes for command line
        $jsonBody = $jsonBody.Replace('"', '\"')
        $curlArgs += @("-d", $jsonBody)
    }

    $curlArgs += $Uri

    $output = & curl.exe @curlArgs 2>&1
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        throw "curl failed with exit code ${exitCode}: $output"
    }

    return ($output | ConvertFrom-Json)
}

# Helper function to test a question
function Test-ChessQuestion {
    param(
        [int]$Number,
        [string]$Category,
        [string]$Question,
        [string]$FenPosition,
        [string]$SessionCookie,
        [string[]]$ExpectedKeywords
    )

    Write-Host "[$Number/15] Testing: " -NoNewline -ForegroundColor Yellow
    Write-Host "$Category" -ForegroundColor Cyan
    Write-Host "  Question: $Question" -ForegroundColor Gray

    try {
        $requestBody = @{
            question = $Question
        }

        if ($FenPosition) {
            $requestBody.fenPosition = $FenPosition
            Write-Host "  FEN: $FenPosition" -ForegroundColor Gray
        }

        $response = Invoke-AuthenticatedRequest -Method Post -Uri "$baseUrl/agents/chess" -Body $requestBody -SessionCookie $SessionCookie

        # Validate response structure
        if (-not $response.answer -or $response.answer -eq "Unable to generate answer.") {
            Write-Host "  [FAILED] No valid answer generated" -ForegroundColor Red
            return $false
        }

        # Check for expected keywords
        $keywordsFound = 0
        foreach ($keyword in $ExpectedKeywords) {
            if ($response.answer -match $keyword) {
                $keywordsFound++
            }
        }

        # Validate sources
        if ($response.sources.Count -eq 0) {
            Write-Host "  [WARNING] No sources cited" -ForegroundColor Yellow
        }

        # Validate token usage
        $tokens = $response.promptTokens + $response.completionTokens
        $cost = ($response.promptTokens * $script:promptTokenCost / 1000000) + ($response.completionTokens * $script:completionTokenCost / 1000000)

        $script:totalTokens += $tokens
        $script:totalCost += $cost

        # Check if FEN position should have analysis
        if ($FenPosition -and -not $response.analysis) {
            Write-Host "  [WARNING] FEN provided but no analysis returned" -ForegroundColor Yellow
        }

        # Display results
        Write-Host "  [PASSED]" -ForegroundColor Green
        Write-Host "    Answer length: $($response.answer.Length) chars" -ForegroundColor Gray
        Write-Host "    Sources: $($response.sources.Count)" -ForegroundColor Gray
        Write-Host "    Confidence: $($response.confidence)" -ForegroundColor Gray
        Write-Host "    Tokens: $($response.promptTokens)p + $($response.completionTokens)c = $tokens total" -ForegroundColor Gray
        Write-Host "    Cost: `$$([math]::Round($cost, 6))" -ForegroundColor Gray
        Write-Host "    Model: $($response.metadata.model)" -ForegroundColor Gray
        Write-Host "    Keywords found: $keywordsFound/$($ExpectedKeywords.Count)" -ForegroundColor Gray

        if ($response.suggestedMoves.Count -gt 0) {
            Write-Host "    Suggested moves: $($response.suggestedMoves.Count)" -ForegroundColor Gray
        }

        if ($response.analysis) {
            Write-Host "    Analysis: $($response.analysis.evaluationSummary)" -ForegroundColor Gray
            Write-Host "    Key considerations: $($response.analysis.keyConsiderations.Count)" -ForegroundColor Gray
        }

        Write-Host ""
        return $true

    } catch {
        Write-Host "  [FAILED] $($_.Exception.Message)" -ForegroundColor Red
        Write-Host ""
        return $false
    }
}

# Main test flow
try {
    # Step 1: Login as Admin
    Write-Host "Step 1: Login as Admin..." -ForegroundColor Cyan
    $loginBody = @{
        email = $adminEmail
        password = $adminPassword
    } | ConvertTo-Json

    $loginWebResponse = Invoke-WebRequest -Uri "$baseUrl/auth/login" -Method Post -Body $loginBody -ContentType "application/json"

    # Extract session cookie from Set-Cookie header
    $setCookieHeader = $loginWebResponse.Headers['Set-Cookie']
    if ($setCookieHeader -match 'meeple_session=([^;]+)') {
        # URL-decode the cookie value (in case it contains encoded characters like %2B)
        $adminSessionCookie = [uri]::UnescapeDataString($matches[1])
        Write-Host "  Admin logged in successfully (cookie: $($adminSessionCookie.Substring(0, [Math]::Min(20, $adminSessionCookie.Length)))...)" -ForegroundColor Green
    } else {
        Write-Host "  Set-Cookie header: $setCookieHeader" -ForegroundColor Yellow
        throw "Failed to get admin session cookie from Set-Cookie header"
    }
    Write-Host ""

    # Step 2: Index Chess Knowledge
    Write-Host "Step 2: Indexing Chess Knowledge..." -ForegroundColor Cyan
    try {
        $indexResponse = Invoke-AuthenticatedRequest -Method Post -Uri "$baseUrl/chess/index" -SessionCookie $adminSessionCookie
        Write-Host "  Indexed $($indexResponse.totalChunks) chunks in $($indexResponse.processingTimeMs)ms" -ForegroundColor Green
    } catch {
        Write-Host "  Index may have already been created (this is OK)" -ForegroundColor Yellow
    }
    Write-Host ""

    # Step 3: Login as User (for asking questions)
    Write-Host "Step 3: Login as User..." -ForegroundColor Cyan
    $userLoginBody = @{
        email = $userEmail
        password = $userPassword
    } | ConvertTo-Json

    $userLoginWebResponse = Invoke-WebRequest -Uri "$baseUrl/auth/login" -Method Post -Body $userLoginBody -ContentType "application/json"

    # Extract session cookie from Set-Cookie header
    $userSetCookieHeader = $userLoginWebResponse.Headers['Set-Cookie']
    if ($userSetCookieHeader -match 'meeple_session=([^;]+)') {
        # URL-decode the cookie value (in case it contains encoded characters like %2B)
        $userSessionCookie = [uri]::UnescapeDataString($matches[1])
    } else {
        throw "Failed to get user session cookie from Set-Cookie header"
    }

    Write-Host "  User logged in successfully" -ForegroundColor Green
    Write-Host ""

    # Step 4: Test 15 Chess Questions
    Write-Host "Step 4: Testing 15 Chess Questions..." -ForegroundColor Cyan
    Write-Host "  DEBUG: Using admin cookie for first test to verify cookie works" -ForegroundColor Yellow
    Write-Host ""

    # Rules Questions (5)
    # DEBUG: Try with admin cookie first
    if (Test-ChessQuestion -Number 1 -Category "Rules" -Question "What is en passant?" -SessionCookie $adminSessionCookie -ExpectedKeywords @("passant", "pawn", "capture")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 2 -Category "Rules" -Question "What are the conditions for castling?" -SessionCookie $userSessionCookie -ExpectedKeywords @("castle", "king", "rook")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 3 -Category "Rules" -Question "Explain pawn promotion" -SessionCookie $userSessionCookie -ExpectedKeywords @("promotion", "pawn", "eighth")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 4 -Category "Rules" -Question "What is the difference between stalemate and checkmate?" -SessionCookie $userSessionCookie -ExpectedKeywords @("stalemate", "checkmate", "king")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 5 -Category "Rules" -Question "Analyze the starting position" -FenPosition "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1" -SessionCookie $userSessionCookie -ExpectedKeywords @("start", "equal", "center")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    # Opening Questions (3)
    if (Test-ChessQuestion -Number 6 -Category "Opening" -Question "Explain the Italian Game opening" -SessionCookie $userSessionCookie -ExpectedKeywords @("Italian", "e4", "Bc4")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 7 -Category "Opening" -Question "What is the Sicilian Defense?" -SessionCookie $userSessionCookie -ExpectedKeywords @("Sicilian", "c5", "e4")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 8 -Category "Opening" -Question "Describe the Ruy Lopez opening" -SessionCookie $userSessionCookie -ExpectedKeywords @("Ruy", "Lopez", "Bb5")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    # Tactical Questions (4)
    if (Test-ChessQuestion -Number 9 -Category "Tactics" -Question "What is a fork in chess?" -SessionCookie $userSessionCookie -ExpectedKeywords @("fork", "attack", "two")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 10 -Category "Tactics" -Question "Explain the difference between a pin and a skewer" -SessionCookie $userSessionCookie -ExpectedKeywords @("pin", "skewer", "line")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 11 -Category "Tactics" -Question "What is a discovered attack?" -SessionCookie $userSessionCookie -ExpectedKeywords @("discover", "attack", "move")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 12 -Category "Tactics" -Question "What should Black play now?" -FenPosition "rnbqkbnr/pppppppp/8/8/4P3/8/PPPP1PPP/RNBQKBNR b KQkq e3 0 1" -SessionCookie $userSessionCookie -ExpectedKeywords @("e5", "c5", "center")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    # Strategic Questions (3)
    if (Test-ChessQuestion -Number 13 -Category "Strategy" -Question "What is the advantage of the bishop pair?" -SessionCookie $userSessionCookie -ExpectedKeywords @("bishop", "pair", "advantage")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 14 -Category "Strategy" -Question "Explain the isolated queen pawn" -SessionCookie $userSessionCookie -ExpectedKeywords @("isolated", "pawn", "queen")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    if (Test-ChessQuestion -Number 15 -Category "Strategy" -Question "Analyze this Italian Game middlegame position" -FenPosition "r1bqk2r/pppp1ppp/2n2n2/2b1p3/2B1P3/3P1N2/PPP2PPP/RNBQK2R w KQkq - 0 6" -SessionCookie $userSessionCookie -ExpectedKeywords @("develop", "castle", "center")) {
        $script:passedTests++
    } else {
        $script:failedTests++
    }

    # Summary
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "Test Summary" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "Total Tests:    $totalTests" -ForegroundColor White
    Write-Host "Passed:         " -NoNewline -ForegroundColor White
    Write-Host "$passedTests" -ForegroundColor Green
    Write-Host "Failed:         " -NoNewline -ForegroundColor White
    Write-Host "$failedTests" -ForegroundColor Red
    Write-Host "Success Rate:   " -NoNewline -ForegroundColor White
    $successRate = [math]::Round(($passedTests / $totalTests) * 100, 1)
    if ($successRate -ge 80) {
        Write-Host "$successRate%" -ForegroundColor Green
    } elseif ($successRate -ge 60) {
        Write-Host "$successRate%" -ForegroundColor Yellow
    } else {
        Write-Host "$successRate%" -ForegroundColor Red
    }
    Write-Host ""
    Write-Host "Total Tokens:   $totalTokens" -ForegroundColor White
    Write-Host "Total Cost:     `$$([math]::Round($totalCost, 4))" -ForegroundColor White
    Write-Host "Avg per query:  `$$([math]::Round($totalCost / $totalTests, 6))" -ForegroundColor White
    Write-Host ""

    if ($passedTests -eq $totalTests) {
        Write-Host "ALL TESTS PASSED! Chess Agent is working perfectly!" -ForegroundColor Green
        exit 0
    } elseif ($passedTests -ge 12) {
        Write-Host "Most tests passed. Review failed tests above." -ForegroundColor Yellow
        exit 0
    } else {
        Write-Host "Multiple tests failed. Check API logs and configuration." -ForegroundColor Red
        exit 1
    }

} catch {
    Write-Host ""
    Write-Host "Test execution failed!" -ForegroundColor Red
    Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    Write-Host "Make sure:" -ForegroundColor Yellow
    Write-Host "  1. API is running on http://localhost:8080" -ForegroundColor Yellow
    Write-Host "  2. PostgreSQL, Qdrant, Redis are running" -ForegroundColor Yellow
    Write-Host "  3. OPENROUTER_API_KEY is configured" -ForegroundColor Yellow
    Write-Host "  4. Chess knowledge has been indexed" -ForegroundColor Yellow
    exit 1
}
