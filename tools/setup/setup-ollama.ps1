# Setup Ollama for MeepleAI embeddings
# This script pulls the required embedding model

$OllamaUrl = "http://localhost:11434"
$Model = "nomic-embed-text"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Setting up Ollama for MeepleAI" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if Ollama is running
Write-Host "[1/3] Checking Ollama availability..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$OllamaUrl/api/tags" -Method Get -ErrorAction Stop
    Write-Host "[OK] Ollama is running at $OllamaUrl" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Ollama is not reachable at $OllamaUrl" -ForegroundColor Red
    Write-Host "  Make sure Ollama is running:" -ForegroundColor Gray
    Write-Host "    - Docker: cd infra && docker compose up ollama" -ForegroundColor Gray
    Write-Host "    - Local: ollama serve" -ForegroundColor Gray
    exit 1
}
Write-Host ""

# Check if model is already pulled
Write-Host "[2/3] Checking if model '$Model' is available..." -ForegroundColor Yellow
$modelExists = $false
if ($response.models) {
    foreach ($m in $response.models) {
        if ($m.name -like "$Model*") {
            $modelExists = $true
            Write-Host "[OK] Model '$Model' is already available" -ForegroundColor Green
            break
        }
    }
}

if (-not $modelExists) {
    Write-Host "[INFO] Model '$Model' not found, pulling..." -ForegroundColor Yellow
    Write-Host "  (This may take 2-5 minutes, model size: ~274MB)" -ForegroundColor Gray
    Write-Host ""

    # Pull the model
    try {
        $pullRequest = @{
            name = $Model
        } | ConvertTo-Json

        $pullResponse = Invoke-WebRequest -Uri "$OllamaUrl/api/pull" `
            -Method Post `
            -Body $pullRequest `
            -ContentType "application/json" `
            -UseBasicParsing `
            -TimeoutSec 600

        Write-Host "[OK] Model '$Model' pulled successfully" -ForegroundColor Green
    } catch {
        Write-Host "[FAIL] Failed to pull model: $_" -ForegroundColor Red
        exit 1
    }
}
Write-Host ""

# Test embedding generation
Write-Host "[3/3] Testing embedding generation..." -ForegroundColor Yellow
try {
    $testRequest = @{
        model = $Model
        prompt = "test embedding"
    } | ConvertTo-Json

    $testResponse = Invoke-RestMethod -Uri "$OllamaUrl/api/embeddings" `
        -Method Post `
        -Body $testRequest `
        -ContentType "application/json" `
        -TimeoutSec 30

    if ($testResponse.embedding -and $testResponse.embedding.Count -gt 0) {
        Write-Host "[OK] Embedding generation working! Dimension: $($testResponse.embedding.Count)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] No embedding returned" -ForegroundColor Red
        exit 1
    }
} catch {
    Write-Host "[FAIL] Embedding test failed: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Ollama setup complete!" -ForegroundColor Green
Write-Host "Model: $Model ($(if($modelExists){'already available'}else{'newly pulled'}})" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
