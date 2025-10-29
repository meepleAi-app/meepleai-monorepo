# GitHub Actions Simulator - Quick Start (Windows PowerShell)

Write-Host "🎭 GitHub Actions Simulator - Quick Start" -ForegroundColor Cyan
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host ""

# Check Docker
Write-Host "🔍 Checking Docker..." -ForegroundColor Yellow
try {
    docker --version | Out-Null
    Write-Host "   ✅ Docker is installed" -ForegroundColor Green
} catch {
    Write-Host "   ❌ Docker is not installed or not running!" -ForegroundColor Red
    Write-Host "   Please install Docker Desktop and try again." -ForegroundColor Red
    exit 1
}

# Check docker-compose
Write-Host "🔍 Checking docker-compose..." -ForegroundColor Yellow
try {
    docker compose version | Out-Null
    Write-Host "   ✅ docker-compose is installed" -ForegroundColor Green
} catch {
    Write-Host "   ❌ docker-compose is not installed!" -ForegroundColor Red
    exit 1
}

# Setup secrets
Write-Host ""
Write-Host "🔐 Setting up secrets..." -ForegroundColor Yellow
if (-not (Test-Path "config/.secrets")) {
    Copy-Item "config/.secrets.example" "config/.secrets"
    Write-Host "   ⚠️  Please edit config/.secrets with your actual API keys" -ForegroundColor Yellow
    Write-Host "   Press Enter to continue after editing secrets..." -ForegroundColor Yellow
    Read-Host
} else {
    Write-Host "   ✅ config/.secrets already exists" -ForegroundColor Green
}

# Build
Write-Host ""
Write-Host "🏗️  Building Docker environment..." -ForegroundColor Yellow
Write-Host "   (This may take 5-10 minutes on first run)" -ForegroundColor Gray
docker compose build
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ Build complete!" -ForegroundColor Green

# Start services
Write-Host ""
Write-Host "🚀 Starting services..." -ForegroundColor Yellow
docker compose up -d
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ❌ Failed to start services!" -ForegroundColor Red
    exit 1
}
Write-Host "   ✅ Services started!" -ForegroundColor Green

# Wait for services
Write-Host ""
Write-Host "⏳ Waiting for services to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 10

# Health check
Write-Host ""
Write-Host "🏥 Running health check..." -ForegroundColor Yellow
docker compose exec act-runner health-check.sh
if ($LASTEXITCODE -ne 0) {
    Write-Host "   ⚠️  Some services may not be ready yet" -ForegroundColor Yellow
}

# Summary
Write-Host ""
Write-Host "=========================================" -ForegroundColor Cyan
Write-Host "✅ Setup complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📚 Quick Reference:" -ForegroundColor Cyan
Write-Host "   Shell:        docker compose exec act-runner bash" -ForegroundColor White
Write-Host "   Test CI:      make test-ci" -ForegroundColor White
Write-Host "   Test API:     make test-api" -ForegroundColor White
Write-Host "   View Logs:    make view-logs" -ForegroundColor White
Write-Host "   Health:       make health" -ForegroundColor White
Write-Host "   Stop:         make down" -ForegroundColor White
Write-Host ""
Write-Host "🌐 Web Interfaces:" -ForegroundColor Cyan
Write-Host "   Log Viewer:   http://localhost:9999" -ForegroundColor White
Write-Host ""
Write-Host "📖 Documentation:" -ForegroundColor Cyan
Write-Host "   README:       github-actions-simulator/README.md" -ForegroundColor White
Write-Host "   Help:         make help" -ForegroundColor White
Write-Host ""
Write-Host "🎯 Try it now:" -ForegroundColor Cyan
Write-Host "   docker compose exec act-runner bash" -ForegroundColor Yellow
Write-Host "   run-workflow.sh .github/workflows/ci.yml" -ForegroundColor Yellow
Write-Host "=========================================" -ForegroundColor Cyan
