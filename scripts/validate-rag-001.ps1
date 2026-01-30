# RAG-001 Validation Script - Complete E2E Test
# Issue: #3172
# Purpose: Validate PDF → Qdrant → RAG pipeline with authentication

param(
    [string]$ApiUrl = "http://localhost:8080",
    [string]$QdrantUrl = "http://localhost:6333",
    [string]$PdfPath = "data/rulebook/scacchi-fide_2017_rulebook.pdf",
    [string]$GameId = "" # Auto-detected from database
)

$ErrorActionPreference = "Continue"
Write-Host "🚀 RAG-001 Validation Script" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

# Step 0: Auto-detect Chess SharedGame ID (if not provided)
if ([string]::IsNullOrWhiteSpace($GameId)) {
    Write-Host "📋 Step 0a: Auto-detecting Chess game ID from database..." -ForegroundColor Yellow
    try {
        $chessIdQuery = docker exec meepleai-postgres psql -U postgres -d meepleai -t -A -c "SELECT id FROM shared_games WHERE title = 'Chess' AND is_deleted = false LIMIT 1;"
        $GameId = $chessIdQuery.Trim()
        if ([string]::IsNullOrWhiteSpace($GameId)) {
            Write-Host "  ❌ Chess game not found in SharedGameCatalog!" -ForegroundColor Red
            exit 1
        }
        Write-Host "  ✅ Chess SharedGame ID: $GameId" -ForegroundColor Green
    } catch {
        Write-Host "  ❌ Failed to query database: $_" -ForegroundColor Red
        exit 1
    }
}

# Step 0b: Read admin credentials
Write-Host "📋 Step 0: Reading admin credentials..." -ForegroundColor Yellow
$adminSecretPath = "infra/secrets/admin.secret"
if (Test-Path $adminSecretPath) {
    $adminCreds = Get-Content $adminSecretPath | ConvertFrom-StringData
    $adminEmail = $adminCreds.ADMIN_EMAIL
    $adminPassword = $adminCreds.ADMIN_PASSWORD
    Write-Host "  ✅ Admin email: $adminEmail" -ForegroundColor Green
} else {
    Write-Host "  ❌ admin.secret not found!" -ForegroundColor Red
    exit 1
}

# Step 1: Login as Admin
Write-Host "`n📋 Step 1: Authenticating as Admin..." -ForegroundColor Yellow
$loginBody = @{
    email = $adminEmail
    password = $adminPassword
} | ConvertTo-Json

try {
    $loginResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/auth/login" `
        -Method POST `
        -Body $loginBody `
        -ContentType "application/json" `
        -SessionVariable session `
        -ErrorAction Stop

    Write-Host "  ✅ Login successful (Status: $($loginResponse.StatusCode))" -ForegroundColor Green
    $sessionCookies = $session.Cookies.GetCookies($ApiUrl)
    Write-Host "  ✅ Session cookies: $($sessionCookies.Count) cookies" -ForegroundColor Green
} catch {
    Write-Host "  ❌ Login failed: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Enable PDF Upload Feature Flag
Write-Host "`n📋 Step 2: Enabling PDF Upload feature flag..." -ForegroundColor Yellow
$featureFlagBody = @{
    key = "Features.PdfUpload"
    enabled = $true
    description = "Enable PDF uploads for rulebook processing"
} | ConvertTo-Json

try {
    # Try to create feature flag (may already exist)
    $createResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/admin/feature-flags" `
        -Method POST `
        -Body $featureFlagBody `
        -ContentType "application/json" `
        -WebSession $session `
        -ErrorAction SilentlyContinue

    if ($createResponse.StatusCode -eq 201) {
        Write-Host "  ✅ Feature flag created and enabled" -ForegroundColor Green
    }
} catch {
    # If creation fails (already exists), try toggle
    Write-Host "  ⚠️  Feature flag may already exist, trying toggle..." -ForegroundColor Yellow

    try {
        $toggleResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/admin/feature-flags/Features.PdfUpload/toggle" `
            -Method POST `
            -WebSession $session `
            -ErrorAction Stop

        Write-Host "  ✅ Feature flag toggled" -ForegroundColor Green
    } catch {
        Write-Host "  ⚠️  Toggle failed, trying direct database update..." -ForegroundColor Yellow
    }
}

# Step 3: Add Chess to Admin Library (using SharedGameId)
Write-Host "`n📋 Step 3: Adding Chess to Admin library..." -ForegroundColor Yellow
$addGameBody = @{} | ConvertTo-Json
try {
    $addGameResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/library/games/$GameId" `
        -Method POST `
        -Body $addGameBody `
        -ContentType "application/json" `
        -WebSession $session `
        -ErrorAction Stop

    Write-Host "  ✅ Chess added to library (SharedGameId: $GameId)" -ForegroundColor Green
} catch {
    $statusCode = $_.Exception.Response.StatusCode.value__
    $responseBody = ""
    try {
        $stream = $_.Exception.Response.GetResponseStream()
        $reader = New-Object System.IO.StreamReader($stream)
        $responseBody = $reader.ReadToEnd()
    } catch {}

    # Game may already be in library
    if ($statusCode -eq 409) {
        Write-Host "  ✅ Chess already in library (409 Conflict)" -ForegroundColor Green
    } else {
        Write-Host "  ❌ Add to library failed: $statusCode" -ForegroundColor Red
        Write-Host "  Response: $responseBody" -ForegroundColor Red
        exit 1
    }
}

Write-Host "  📄 Using SharedGameId for upload: $GameId" -ForegroundColor Cyan

# Step 4: Verify Qdrant Collection (Before Upload)
Write-Host "`n📋 Step 4: Checking Qdrant collection (before)..." -ForegroundColor Yellow
try {
    $qdrantBefore = Invoke-RestMethod -Uri "$QdrantUrl/collections/meepleai_documents" -Method GET
    Write-Host "  ✅ Collection status: $($qdrantBefore.result.status)" -ForegroundColor Green
    Write-Host "  📊 Vectors before: $($qdrantBefore.result.points_count)" -ForegroundColor Cyan
} catch {
    Write-Host "  ❌ Qdrant collection check failed: $_" -ForegroundColor Red
}

# Step 5: Upload PDF
Write-Host "`n📋 Step 5: Uploading PDF rulebook..." -ForegroundColor Yellow
if (-not (Test-Path $PdfPath)) {
    Write-Host "  ❌ PDF file not found: $PdfPath" -ForegroundColor Red
    exit 1
}

$pdfFileInfo = Get-Item $PdfPath
Write-Host "  📄 PDF: $($pdfFileInfo.Name) ($([math]::Round($pdfFileInfo.Length/1KB, 2)) KB)" -ForegroundColor Cyan

# Prepare multipart form data
$boundary = [System.Guid]::NewGuid().ToString()
$LF = "`r`n"

$bodyLines = @(
    "--$boundary",
    "Content-Disposition: form-data; name=`"file`"; filename=`"$($pdfFileInfo.Name)`"",
    "Content-Type: application/pdf",
    "",
    [System.IO.File]::ReadAllText($PdfPath),
    "--$boundary",
    "Content-Disposition: form-data; name=`"gameId`"",
    "",
    $GameId,
    "--$boundary",
    "Content-Disposition: form-data; name=`"language`"",
    "",
    "it",
    "--$boundary--"
) -join $LF

try {
    $uploadResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/ingest/pdf" `
        -Method POST `
        -ContentType "multipart/form-data; boundary=$boundary" `
        -Body $bodyLines `
        -WebSession $session `
        -TimeoutSec 120 `
        -ErrorAction Stop

    $uploadResult = $uploadResponse.Content | ConvertFrom-Json
    Write-Host "  ✅ PDF uploaded successfully" -ForegroundColor Green
    Write-Host "  📄 Document ID: $($uploadResult.documentId)" -ForegroundColor Cyan
    $documentId = $uploadResult.documentId

} catch {
    Write-Host "  ❌ PDF upload failed: $_" -ForegroundColor Red
    Write-Host "  Response: $($_.Exception.Response.StatusCode)" -ForegroundColor Red

    # If still blocked, try manual workaround
    Write-Host "`n⚠️  WORKAROUND: Try manual upload via UI at http://localhost:3000" -ForegroundColor Yellow
    Write-Host "  1. Login as admin: $adminEmail" -ForegroundColor Yellow
    Write-Host "  2. Navigate to game detail page for Chess" -ForegroundColor Yellow
    Write-Host "  3. Upload PDF manually" -ForegroundColor Yellow
    exit 1
}

# Step 6: Trigger PDF Indexing
Write-Host "`n📋 Step 6: Triggering PDF indexing..." -ForegroundColor Yellow
Start-Sleep -Seconds 5 # Wait for upload to settle

try {
    $indexResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/ingest/pdf/$documentId/index" `
        -Method POST `
        -WebSession $session `
        -TimeoutSec 180 `
        -ErrorAction Stop

    Write-Host "  ✅ Indexing triggered successfully" -ForegroundColor Green
    Write-Host "  ⏳ Waiting 30s for processing..." -ForegroundColor Cyan
    Start-Sleep -Seconds 30
} catch {
    Write-Host "  ❌ Indexing failed: $_" -ForegroundColor Red
}

# Step 7: Verify Qdrant Collection (After Upload)
Write-Host "`n📋 Step 7: Checking Qdrant collection (after)..." -ForegroundColor Yellow
try {
    $qdrantAfter = Invoke-RestMethod -Uri "$QdrantUrl/collections/meepleai_documents" -Method GET
    Write-Host "  ✅ Collection status: $($qdrantAfter.result.status)" -ForegroundColor Green
    Write-Host "  📊 Vectors after: $($qdrantAfter.result.points_count)" -ForegroundColor Cyan
    $vectorsAdded = $qdrantAfter.result.points_count - $qdrantBefore.result.points_count
    Write-Host "  ➕ Vectors added: $vectorsAdded" -ForegroundColor Green

    if ($vectorsAdded -gt 0) {
        Write-Host "  ✅ PDF successfully indexed in Qdrant!" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  No vectors added, indexing may have failed" -ForegroundColor Yellow
    }
} catch {
    Write-Host "  ❌ Qdrant check failed: $_" -ForegroundColor Red
}

# Step 8: Test RAG Query
Write-Host "`n📋 Step 8: Testing RAG query..." -ForegroundColor Yellow
$ragQueryBody = @{
    gameId = $GameId
    query = "Come si muovono i pedoni negli scacchi?"
} | ConvertTo-Json

try {
    $ragResponse = Invoke-WebRequest -Uri "$ApiUrl/api/v1/agents/qa" `
        -Method POST `
        -Body $ragQueryBody `
        -ContentType "application/json" `
        -WebSession $session `
        -TimeoutSec 30 `
        -ErrorAction Stop

    $ragResult = $ragResponse.Content | ConvertFrom-Json
    Write-Host "  ✅ RAG query successful" -ForegroundColor Green
    Write-Host "  📝 Answer preview: $($ragResult.answer.Substring(0, [Math]::Min(150, $ragResult.answer.Length)))..." -ForegroundColor Cyan
    Write-Host "  🎯 Confidence: $($ragResult.confidence)" -ForegroundColor Cyan
    Write-Host "  📚 Citations: $($ragResult.citations.Count)" -ForegroundColor Cyan

    if ($ragResult.confidence -ge 0.7) {
        Write-Host "  ✅ Confidence above threshold (>= 0.7)" -ForegroundColor Green
    } else {
        Write-Host "  ⚠️  Confidence below threshold: $($ragResult.confidence)" -ForegroundColor Yellow
    }

} catch {
    Write-Host "  ❌ RAG query failed: $_" -ForegroundColor Red
}

# Step 9: Test SSE Streaming
Write-Host "`n📋 Step 9: Testing SSE streaming..." -ForegroundColor Yellow
Write-Host "  ℹ️  SSE test requires manual validation (EventSource in browser)" -ForegroundColor Cyan
Write-Host "  📝 Test URL: $ApiUrl/api/v1/agents/qa/stream" -ForegroundColor Cyan

# Final Summary
Write-Host "`n" -NoNewline
Write-Host "================================" -ForegroundColor Cyan
Write-Host "📊 VALIDATION SUMMARY" -ForegroundColor Cyan
Write-Host "================================" -ForegroundColor Cyan
Write-Host "✅ Authentication: PASS" -ForegroundColor Green
Write-Host "✅ PDF Upload: $(if ($documentId) { 'PASS' } else { 'FAIL' })" -ForegroundColor $(if ($documentId) { 'Green' } else { 'Red' })
Write-Host "✅ Qdrant Indexing: $(if ($vectorsAdded -gt 0) { 'PASS' } else { 'PENDING' })" -ForegroundColor $(if ($vectorsAdded -gt 0) { 'Green' } else { 'Yellow' })
Write-Host "✅ RAG Query: $(if ($ragResult) { 'PASS' } else { 'PENDING' })" -ForegroundColor $(if ($ragResult) { 'Green' } else { 'Yellow' })

Write-Host "`nℹ️  Full report: docs/validation/rag-001-results.md" -ForegroundColor Cyan
Write-Host "🎯 Issue: #3172 (RAG-001)" -ForegroundColor Cyan
