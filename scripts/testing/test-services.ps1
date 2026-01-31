# MeepleAI Services Health Check Script (PowerShell)
# Tests all Docker services and reports their status

[CmdletBinding()]
param()

$ErrorActionPreference = 'Continue'

Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "   MeepleAI Services Health Check" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""

# Function to check HTTP service
function Test-HttpService {
    param(
        [string]$Name,
        [string]$Url
    )

    try {
        $response = Invoke-WebRequest -Uri $Url -Method Get -TimeoutSec 5 -UseBasicParsing -ErrorAction Stop
        $statusCode = $response.StatusCode

        if ($statusCode -eq 200 -or $statusCode -eq 302) {
            Write-Host ("{0,-18}: " -f $Name) -NoNewline
            Write-Host "✅ OK" -ForegroundColor Green -NoNewline
            Write-Host " (HTTP $statusCode)"
        }
        else {
            Write-Host ("{0,-18}: " -f $Name) -NoNewline
            Write-Host "⚠️  WARN" -ForegroundColor Yellow -NoNewline
            Write-Host " (HTTP $statusCode)"
        }
    }
    catch {
        Write-Host ("{0,-18}: " -f $Name) -NoNewline
        Write-Host "❌ DOWN" -ForegroundColor Red -NoNewline
        Write-Host " (Connection refused)"
    }
}

# Web Services
Write-Host "=== Web Services ===" -ForegroundColor Cyan
Test-HttpService "Frontend" "http://localhost:3000"
Test-HttpService "API Health" "http://localhost:8080/health"
Test-HttpService "API Swagger" "http://localhost:8080/scalar/v1"
Write-Host ""

# Monitoring Services
Write-Host "=== Monitoring & Observability ===" -ForegroundColor Cyan
Test-HttpService "Grafana" "http://localhost:3001"
Test-HttpService "Prometheus" "http://localhost:9090/-/healthy"
Test-HttpService "Alertmanager" "http://localhost:9093"
Test-HttpService "cAdvisor" "http://localhost:8082"
Test-HttpService "Node Exporter" "http://localhost:9100/metrics"
Write-Host ""

# Development Tools
Write-Host "=== Development Tools ===" -ForegroundColor Cyan
Test-HttpService "Mailpit UI" "http://localhost:8025"
Test-HttpService "n8n Workflow" "http://localhost:5678"
Write-Host ""

# AI Services
Write-Host "=== AI Services ===" -ForegroundColor Cyan
Test-HttpService "Embedding Service" "http://localhost:8000/health"
Test-HttpService "Unstructured" "http://localhost:8001/health"
Test-HttpService "SmolDocling" "http://localhost:8002/health"
Test-HttpService "Reranker Service" "http://localhost:8003/health"
Test-HttpService "Ollama" "http://localhost:11434/api/tags"
Write-Host ""

# Storage Services
Write-Host "=== Storage Services ===" -ForegroundColor Cyan
Test-HttpService "Qdrant HTTP" "http://localhost:6333/collections"

# PostgreSQL
Write-Host ("{0,-18}: " -f "PostgreSQL") -NoNewline
try {
    $pgStatus = docker exec meepleai-postgres pg_isready -U meepleai 2>&1
    if ($pgStatus -like "*accepting connections*") {
        Write-Host "✅ OK" -ForegroundColor Green -NoNewline
        Write-Host " (Accepting connections)"
    }
    else {
        Write-Host "❌ DOWN" -ForegroundColor Red -NoNewline
        Write-Host " ($pgStatus)"
    }
}
catch {
    Write-Host "❌ DOWN" -ForegroundColor Red -NoNewline
    Write-Host " (Container not found)"
}

# Redis
Write-Host ("{0,-18}: " -f "Redis") -NoNewline
try {
    $redisPasswordPath = Join-Path $PSScriptRoot "../../infra/secrets/redis-password.txt"
    if (Test-Path $redisPasswordPath) {
        $redisPassword = (Get-Content $redisPasswordPath -Raw).Trim()
        $redisStatus = docker exec meepleai-redis redis-cli -a $redisPassword PING 2>&1

        if ($redisStatus -eq "PONG") {
            Write-Host "✅ OK" -ForegroundColor Green -NoNewline
            Write-Host " (PONG)"
        }
        else {
            Write-Host "❌ DOWN" -ForegroundColor Red -NoNewline
            Write-Host " (No response)"
        }
    }
    else {
        Write-Host "⚠️  WARN" -ForegroundColor Yellow -NoNewline
        Write-Host " (Password file not found)"
    }
}
catch {
    Write-Host "❌ DOWN" -ForegroundColor Red -NoNewline
    Write-Host " (Container not found)"
}

Write-Host ""
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host "   Summary" -ForegroundColor Cyan
Write-Host "===========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Legend:"
Write-Host "  ✅ OK   = Service is healthy"
Write-Host "  ⚠️  WARN = Service responded but with non-200 status"
Write-Host "  ❌ DOWN = Service is not responding"
Write-Host ""
Write-Host "Quick Links:"
Write-Host "  Frontend:    http://localhost:3000"
Write-Host "  API Docs:    http://localhost:8080/scalar/v1"
Write-Host "  Grafana:     http://localhost:3001 (admin/admin)"
Write-Host "  Prometheus:  http://localhost:9090"
Write-Host "  Mailpit:     http://localhost:8025"
Write-Host ""
