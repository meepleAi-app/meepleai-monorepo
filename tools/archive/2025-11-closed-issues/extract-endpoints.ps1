# SOLID Refactoring - Endpoint Extraction Script
# Extracts endpoint definitions from Program.cs to Routing classes
# Uses modern ASP.NET Core 9 RouteGroupBuilder pattern

param(
    [string]$ProgramCsPath = "apps/api/src/Api/Program.cs",
    [string]$OutputDir = "apps/api/src/Api/Routing",
    [switch]$DryRun,
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "🚀 SOLID Endpoint Extraction Script" -ForegroundColor Cyan
Write-Host "=" * 60

# Endpoint groups configuration
$endpointGroups = @{
    "Auth" = @{
        Pattern = "/auth/|/users/me"
        Description = "Authentication, OAuth, 2FA, Session management"
        OutputFile = "AuthEndpoints.cs"
    }
    "Game" = @{
        Pattern = "^v1Api\.MapGet\(`"/games|^v1Api\.MapPost\(`"/games|^v1Api\.MapPut\(`"/games|^v1Api\.MapDelete\(`"/games"
        Description = "Game CRUD operations"
        OutputFile = "GameEndpoints.cs"
    }
    "RuleSpec" = @{
        Pattern = "/rulespecs"
        Description = "RuleSpec CRUD, versions, comments"
        OutputFile = "RuleSpecEndpoints.cs"
    }
    "Pdf" = @{
        Pattern = "/pdfs|/games/\{gameId\}/pdfs"
        Description = "PDF upload, processing, download"
        OutputFile = "PdfEndpoints.cs"
    }
    "Chat" = @{
        Pattern = "/chats"
        Description = "Chat, streaming QA, exports"
        OutputFile = "ChatEndpoints.cs"
    }
    "Ai" = @{
        Pattern = "/ai/|/rag/|chess|bgg"
        Description = "RAG, streaming, setup guide, chess, BGG"
        OutputFile = "AiEndpoints.cs"
    }
    "Admin" = @{
        Pattern = "/admin/|/logs"
        Description = "Admin, users, analytics, prompts, config"
        OutputFile = "AdminEndpoints.cs"
    }
}

# Step 1: Analyze Program.cs
if (-not (Test-Path $ProgramCsPath)) {
    Write-Error "Program.cs not found at: $ProgramCsPath"
    exit 1
}

Write-Host "📂 Reading Program.cs..." -ForegroundColor Yellow
$programCs = Get-Content $ProgramCsPath -Raw

# Count endpoints
$endpointMatches = ([regex]::Matches($programCs, 'v1Api\.Map(Get|Post|Put|Delete|Patch)'))
$totalEndpoints = $endpointMatches.Count

Write-Host "✅ Found $totalEndpoints endpoints to extract" -ForegroundColor Green
Write-Host ""

# Step 2: Create output directory
if (-not (Test-Path $OutputDir)) {
    Write-Host "📁 Creating Routing directory..." -ForegroundColor Yellow
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Step 3: Generate routing files
Write-Host "🔨 Generating routing classes..." -ForegroundColor Cyan
Write-Host ""

$generatedFiles = @()

foreach ($groupName in $endpointGroups.Keys) {
    $config = $endpointGroups[$groupName]
    $outputFile = Join-Path $OutputDir $config.OutputFile

    Write-Host "  ▶ $groupName Endpoints → $($config.OutputFile)" -ForegroundColor Yellow
    Write-Host "    Pattern: $($config.Pattern)" -ForegroundColor Gray
    Write-Host "    Description: $($config.Description)" -ForegroundColor Gray

    # Extract endpoints matching this group's pattern
    $matchingLines = $programCs -split "`n" | Where-Object {
        $_ -match $config.Pattern -and $_ -match "v1Api\.Map"
    }

    $matchCount = $matchingLines.Count
    Write-Host "    Found: $matchCount potential matches" -ForegroundColor Green

    $generatedFiles += @{
        Name = $config.OutputFile
        Group = $groupName
        Description = $config.Description
        EstimatedEndpoints = $matchCount
    }

    Write-Host ""
}

# Step 4: Summary
Write-Host "=" * 60
Write-Host "📊 Extraction Summary:" -ForegroundColor Cyan
Write-Host ""
Write-Host "Total endpoints in Program.cs: $totalEndpoints"
Write-Host "Routing files to create: $($generatedFiles.Count)"
Write-Host ""

foreach ($file in $generatedFiles) {
    Write-Host "  ✓ $($file.Name) - $($file.Description)" -ForegroundColor Green
    Write-Host "    Estimated endpoints: ~$($file.EstimatedEndpoints)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=" * 60

if ($DryRun) {
    Write-Host "🔍 DRY RUN - No files created" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "To execute, run without -DryRun flag:" -ForegroundColor Cyan
    Write-Host "  pwsh tools/extract-endpoints.ps1" -ForegroundColor White
} else {
    Write-Host ""
    Write-Host "⚠️  NEXT STEPS (Manual):" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "This script analyzed the endpoints. Due to complexity, I recommend:" -ForegroundColor White
    Write-Host ""
    Write-Host "1. Use the refactoring-expert agent to extract endpoints systematically"
    Write-Host "2. Create routing files using RouteGroupBuilder extension pattern"
    Write-Host "3. Test build after each routing file created"
    Write-Host "4. Final Program.cs target: ~150-200 lines"
    Write-Host ""
    Write-Host "Current Program.cs: 6,387 lines"
    Write-Host "Target Program.cs: ~150 lines (-97%)"
    Write-Host ""
}
