# Upload and process Chess rulebook for RAG validation (AGT-018)
$ErrorActionPreference = "Stop"

$ApiBaseUrl = "http://localhost:8080"
$GameId = "30706e12-4c77-4a52-9118-8d48c94f6d9c"
$PdfPath = "data/rulebook/scacchi-fide_2017_rulebook.pdf"

Write-Host "[INFO] Chess Rulebook Upload & Processing" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# 1. Verify file exists
if (-not (Test-Path $PdfPath)) {
    Write-Host "[ERROR] PDF not found: $PdfPath" -ForegroundColor Red
    exit 1
}

$fileInfo = Get-Item $PdfPath
Write-Host "[OK] PDF found: $($fileInfo.Name)" -ForegroundColor Green
Write-Host "    Size: $([math]::Round($fileInfo.Length / 1KB, 2)) KB" -ForegroundColor Gray

# 2. Login as admin
Write-Host "`n[INFO] Logging in as admin..." -ForegroundColor Cyan
$loginBody = @{
    email = "admin@meepleai.dev"
    password = "pVKOMQNK0tFNgGlX"
} | ConvertTo-Json

try {
    $loginResponse = Invoke-WebRequest `
        -Uri "$ApiBaseUrl/api/v1/auth/login" `
        -Method Post `
        -ContentType "application/json" `
        -Body $loginBody `
        -UseBasicParsing

    $cookie = ($loginResponse.Headers['Set-Cookie'] | Where-Object { $_ -match 'meepleai_session=' }) -replace ';.*',''
    Write-Host "[OK] Logged in successfully" -ForegroundColor Green

} catch {
    Write-Host "[ERROR] Login failed: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}

# 3. Upload PDF
Write-Host "`n[INFO] Uploading PDF via /ingest/pdf endpoint..." -ForegroundColor Cyan

$uploadUrl = "$ApiBaseUrl/api/v1/ingest/pdf"

try {
    # Prepare multipart form with correct content type for PDF
    Add-Type -AssemblyName "System.Net.Http"

    $httpClient = New-Object System.Net.Http.HttpClient
    $httpClient.Timeout = [TimeSpan]::FromSeconds(60)
    $httpClient.DefaultRequestHeaders.Add("Cookie", $cookie)

    $multipart = New-Object System.Net.Http.MultipartFormDataContent

    # Add PDF file with correct content type
    $fileStream = [System.IO.File]::OpenRead((Resolve-Path $PdfPath))
    $fileContent = New-Object System.Net.Http.StreamContent($fileStream)
    $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/pdf")
    $multipart.Add($fileContent, "file", $fileInfo.Name)

    # Add form fields
    $multipart.Add((New-Object System.Net.Http.StringContent($GameId)), "gameId")
    $multipart.Add((New-Object System.Net.Http.StringContent("it")), "language")
    $multipart.Add((New-Object System.Net.Http.StringContent("base")), "versionType")
    $multipart.Add((New-Object System.Net.Http.StringContent("FIDE2017")), "versionNumber")

    # Send request
    $response = $httpClient.PostAsync($uploadUrl, $multipart).Result
    $responseContent = $response.Content.ReadAsStringAsync().Result

    $fileStream.Close()
    $httpClient.Dispose()

    if (-not $response.IsSuccessStatusCode) {
        Write-Host "[ERROR] Upload failed: $($response.StatusCode)" -ForegroundColor Red
        Write-Host "Response: $responseContent" -ForegroundColor Red
        exit 1
    }

    $uploadResponse = $responseContent | ConvertFrom-Json

    Write-Host "[OK] PDF uploaded successfully!" -ForegroundColor Green
    Write-Host "    Document ID: $($uploadResponse.id)" -ForegroundColor Yellow
    Write-Host "    Status: $($uploadResponse.processingStatus)" -ForegroundColor Gray

    # Save document ID
    @{
        documentId = $uploadResponse.id
        gameId = $GameId
        fileName = $fileInfo.Name
        uploadedAt = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    } | ConvertTo-Json | Out-File -FilePath "tools/.chess-document-id.json" -Encoding UTF8

} catch {
    Write-Host "[ERROR] Upload failed: $($_.Exception.Message)" -ForegroundColor Red
    if ($_.ErrorDetails) {
        Write-Host "Details: $($_.ErrorDetails.Message)" -ForegroundColor Red
    }
    exit 1
}

# 4. Wait for processing
Write-Host "`n[INFO] Waiting for PDF processing (this may take 1-2 minutes)..." -ForegroundColor Cyan
Write-Host "    Processing: extraction → chunking → embedding → indexing" -ForegroundColor Gray

$maxWait = 180 # 3 minutes
$waited = 0
$pollInterval = 5

while ($waited -lt $maxWait) {
    Start-Sleep -Seconds $pollInterval
    $waited += $pollInterval

    try {
        $status = Invoke-RestMethod `
            -Uri "$ApiBaseUrl/api/v1/shared-games/$GameId/documents/$($uploadResponse.id)" `
            -Method Get `
            -Headers @{ "Cookie" = $cookie }

        Write-Host "." -NoNewline

        if ($status.processingStatus -eq "Completed") {
            Write-Host ""
            Write-Host "[OK] Processing completed!" -ForegroundColor Green
            Write-Host "    Pages: $($status.pageCount)" -ForegroundColor Gray
            Write-Host "    Characters: $($status.characterCount)" -ForegroundColor Gray
            break
        }

        if ($status.processingStatus -eq "Failed") {
            Write-Host ""
            Write-Host "[ERROR] Processing failed: $($status.processingError)" -ForegroundColor Red
            exit 1
        }

    } catch {
        Write-Host "!" -NoNewline
    }
}

if ($waited -ge $maxWait) {
    Write-Host ""
    Write-Host "[WARN] Processing timeout - check status manually" -ForegroundColor Yellow
    Write-Host "[INFO] Check: GET /api/v1/shared-games/$GameId/documents/$($uploadResponse.id)" -ForegroundColor Cyan
}

# 5. Verify indexing in Qdrant
Write-Host "`n[INFO] Verifying chunks in Qdrant..." -ForegroundColor Cyan

try {
    $qdrantUrl = "http://localhost:6333/collections/meepleai_documents/points/scroll"
    $scrollBody = @{
        filter = @{
            must = @(
                @{
                    key = "game_id"
                    match = @{ value = $GameId }
                }
            )
        }
        limit = 1
        with_payload = $true
        with_vector = $false
    } | ConvertTo-Json -Depth 10

    $chunks = Invoke-RestMethod `
        -Uri $qdrantUrl `
        -Method Post `
        -ContentType "application/json" `
        -Body $scrollBody

    $chunkCount = $chunks.result.points.Count

    if ($chunkCount -gt 0) {
        Write-Host "[OK] Found $chunkCount chunks in Qdrant for Chess" -ForegroundColor Green
        Write-Host ""
        Write-Host "[SUCCESS] ✅ Chess rulebook ready for validation!" -ForegroundColor Green
        Write-Host ""
        Write-Host "[NEXT] Run validation: pwsh tools/run-rag-validation-20q.ps1" -ForegroundColor Yellow
    } else {
        Write-Host "[WARN] No chunks found in Qdrant yet" -ForegroundColor Yellow
        Write-Host "[INFO] Wait a bit longer or check embedding service logs" -ForegroundColor Cyan
    }

} catch {
    Write-Host "[WARN] Could not verify Qdrant: $($_.Exception.Message)" -ForegroundColor Yellow
}
