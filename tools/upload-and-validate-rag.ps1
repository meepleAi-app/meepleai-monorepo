# Complete RAG validation workflow: Upload PDF → Process → Validate
# Issue: AGT-018 (#3192)
$ErrorActionPreference = "Stop"

$ApiBaseUrl = "http://localhost:8080"
$GameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"
$PdfPath = "data/rulebook/scacchi-fide_2017_rulebook.pdf"

Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║     AGT-018: RAG Quality Validation Workflow          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Step 1: Login
Write-Host "[1/5] Logging in..." -ForegroundColor Yellow

$loginJson = '{"email":"admin@meepleai.dev","password":"pVKOMQNK0tFNgGlX"}'
$loginFile = New-TemporaryFile
$loginJson | Out-File -FilePath $loginFile -Encoding UTF8 -NoNewline

$cookieJar = New-TemporaryFile

& curl.exe -s -X POST "$ApiBaseUrl/api/v1/auth/login" `
    -H "Content-Type: application/json" `
    -d "@$loginFile" `
    -c $cookieJar `
    -w "%{http_code}" `
    -o nul

if ($LASTEXITCODE -ne 0) {
    Write-Host "[ERROR] Login failed" -ForegroundColor Red
    exit 1
}

Write-Host "      ✅ Logged in" -ForegroundColor Green

# Step 2: Upload PDF
Write-Host "[2/5] Uploading Chess rulebook (601KB)..." -ForegroundColor Yellow

$uploadResult = & curl.exe -s -X POST "$ApiBaseUrl/api/v1/ingest/pdf" `
    -b $cookieJar `
    -F "file=@$PdfPath;type=application/pdf" `
    -F "gameId=$GameId" `
    -F "language=it" `
    -F "versionType=base" `
    -F "versionNumber=FIDE2017" `
    -w "`n%{http_code}"

$lines = $uploadResult -split "`n"
$statusCode = $lines[-1]
$responseBody = $lines[0..($lines.Length-2)] -join "`n"

if ($statusCode -ne "200") {
    Write-Host "[ERROR] Upload failed (HTTP $statusCode)" -ForegroundColor Red
    Write-Host $responseBody -ForegroundColor Red
    exit 1
}

$upload = $responseBody | ConvertFrom-Json
Write-Host "      ✅ PDF uploaded" -ForegroundColor Green
Write-Host "      Document ID: $($upload.documentId)" -ForegroundColor Gray

# Step 3: Wait for processing
Write-Host "[3/5] Processing PDF (extraction + chunking + embedding)..." -ForegroundColor Yellow
Write-Host "      This may take 2-3 minutes for 601KB PDF..." -ForegroundColor Gray

$documentId = $upload.documentId
$maxWait = 180
$waited = 0

while ($waited -lt $maxWait) {
    Start-Sleep -Seconds 10
    $waited += 10
    Write-Host "." -NoNewline

    $statusJson = & curl.exe -s -b $cookieJar "$ApiBaseUrl/api/v1/pdf/$documentId"

    try {
        $docStatus = $statusJson | ConvertFrom-Json

        if ($docStatus.ProcessingStatus -eq "Completed") {
            Write-Host ""
            Write-Host "      ✅ Processing completed!" -ForegroundColor Green
            Write-Host "      Pages: $($docStatus.PageCount)" -ForegroundColor Gray
            Write-Host "      Chunks indexed in Qdrant" -ForegroundColor Gray
            break
        }

        if ($docStatus.ProcessingStatus -eq "Failed") {
            Write-Host ""
            Write-Host "[ERROR] Processing failed: $($docStatus.ProcessingError)" -ForegroundColor Red
            exit 1
        }

    } catch {
        # Continue waiting
    }
}

if ($waited -ge $maxWait) {
    Write-Host ""
    Write-Host "[WARN] Processing timeout after ${maxWait}s" -ForegroundColor Yellow
    Write-Host "[INFO] Check status manually: GET /api/v1/pdf/$documentId" -ForegroundColor Cyan
}

# Step 4: Verify Qdrant indexing
Write-Host "[4/5] Verifying Qdrant indexing..." -ForegroundColor Yellow

$qdrantCheck = & curl.exe -s -X POST "http://localhost:6333/collections/meepleai_documents/points/scroll" `
    -H "Content-Type: application/json" `
    -d "{`"filter`":{`"must`":[{`"key`":`"game_id`",`"match`":{`"value`":`"$GameId`"}}]},`"limit`":1}"

$qdrantResult = $qdrantCheck | ConvertFrom-Json

if ($qdrantResult.result.points.Count -gt 0) {
    Write-Host "      ✅ Chunks found in Qdrant" -ForegroundColor Green
} else {
    Write-Host "      ⚠️  No chunks yet (may still be processing)" -ForegroundColor Yellow
}

# Cleanup temp files
Remove-Item $loginFile -ErrorAction SilentlyContinue
Remove-Item $cookieJar -ErrorAction SilentlyContinue

# Step 5: Run validation
Write-Host "[5/5] Running RAG quality validation (20 questions)..." -ForegroundColor Yellow
Write-Host ""

& pwsh ./tools/run-rag-validation-20q.ps1

Write-Host ""
Write-Host "╔════════════════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║              AGT-018 WORKFLOW COMPLETE!               ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════════════════╝" -ForegroundColor Green
