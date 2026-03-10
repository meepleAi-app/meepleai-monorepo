#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Initialize Knowledge Base for board games with PDF rulebooks.

.DESCRIPTION
    Three-phase script:
      Phase 1: Bulk import SharedGames from BGG IDs
      Phase 2: Upload PDF rulebooks linked to SharedGames
      Phase 3: Monitor processing queue until complete

.PARAMETER BaseUrl
    API base URL (e.g., https://staging.meepleai.com or http://localhost:8080)

.PARAMETER Email
    Admin account email

.PARAMETER Password
    Admin account password

.PARAMETER MappingFile
    Path to mapping.json (default: docs/rulebooks/mapping.json)

.PARAMETER SkipImport
    Skip Phase 1 (games already imported)

.PARAMETER SkipUpload
    Skip Phase 2 (PDFs already uploaded)

.PARAMETER DryRun
    Show what would be done without executing

.EXAMPLE
    ./scripts/initialize-kb.ps1 -BaseUrl "http://localhost:8080" -Email "admin@meepleai.com" -Password "secret"
    ./scripts/initialize-kb.ps1 -BaseUrl "https://staging.meepleai.com" -Email "admin@meepleai.com" -Password "secret" -SkipImport
#>

param(
    [Parameter(Mandatory)]
    [string]$BaseUrl,

    [Parameter(Mandatory)]
    [string]$Email,

    [Parameter(Mandatory)]
    [string]$Password,

    [string]$MappingFile = "",

    [switch]$SkipImport,
    [switch]$SkipUpload,
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# ─── Resolve paths ───────────────────────────────────────────────────────
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot  = Split-Path -Parent $scriptDir

if (-not $MappingFile) {
    $MappingFile = Join-Path $repoRoot "docs" "rulebooks" "mapping.json"
}

# Resolve PDF source directory from mapping.json or default to data/rulebook
$rulebooksDir = $null  # Will be set after reading mapping

# ─── Helpers ─────────────────────────────────────────────────────────────
function Write-Phase { param([string]$Phase, [string]$Message) Write-Host "`n[$Phase] $Message" -ForegroundColor Cyan }
function Write-OK    { param([string]$Message) Write-Host "  OK: $Message" -ForegroundColor Green }
function Write-Warn  { param([string]$Message) Write-Host "  WARN: $Message" -ForegroundColor Yellow }
function Write-Err   { param([string]$Message) Write-Host "  ERROR: $Message" -ForegroundColor Red }
function Write-Info  { param([string]$Message) Write-Host "  $Message" -ForegroundColor Gray }

$BaseUrl = $BaseUrl.TrimEnd('/')

# ─── Pre-flight checks ──────────────────────────────────────────────────
Write-Phase "PRE-FLIGHT" "Validating configuration"

if (-not (Test-Path $MappingFile)) {
    Write-Err "Mapping file not found: $MappingFile"
    Write-Info "Create it with the game list. See docs/rulebooks/mapping.json template."
    exit 1
}

$mapping = Get-Content $MappingFile -Raw | ConvertFrom-Json
$games = $mapping.games

if ($games.Count -eq 0) {
    Write-Err "No games found in mapping file"
    exit 1
}

# Resolve PDF source directory
if ($mapping.sourceDir) {
    $rulebooksDir = Join-Path $repoRoot $mapping.sourceDir
}
else {
    $rulebooksDir = Join-Path $repoRoot "data" "rulebook"
}

Write-OK "Found $($games.Count) games in mapping"
Write-OK "PDF source: $rulebooksDir"

# Validate all PDF files exist
$missingPdfs = @()
foreach ($game in $games) {
    $pdfPath = Join-Path $rulebooksDir $game.filename
    if (-not (Test-Path $pdfPath)) {
        $missingPdfs += $game.filename
    }
}

if ($missingPdfs.Count -gt 0 -and -not $SkipUpload) {
    Write-Err "Missing PDF files ($($missingPdfs.Count)):"
    $missingPdfs | ForEach-Object { Write-Info "  - $_" }
    exit 1
}

Write-OK "All $($games.Count) PDF files found"

# Validate BGG IDs
$invalidBgg = $games | Where-Object { $_.bggId -le 0 }
if ($invalidBgg.Count -gt 0 -and -not $SkipImport) {
    Write-Err "Invalid BGG IDs (must be > 0):"
    $invalidBgg | ForEach-Object { Write-Info "  - $($_.name): bggId=$($_.bggId)" }
    exit 1
}

Write-OK "All BGG IDs valid"

if ($DryRun) {
    Write-Phase "DRY-RUN" "Would process:"
    Write-Info "  Games to import: $($games.Count)"
    Write-Info "  PDFs to upload:  $($games.Count)"
    Write-Info "  API: $BaseUrl"
    $games | ForEach-Object { Write-Info "    $($_.bggId) | $($_.name) | $($_.filename)" }
    exit 0
}

# ─── Authentication ─────────────────────────────────────────────────────
Write-Phase "AUTH" "Logging in as admin"

try {
    $loginBody = @{ email = $Email; password = $Password } | ConvertTo-Json
    $loginResponse = Invoke-WebRequest `
        -Uri "$BaseUrl/api/v1/auth/login" `
        -Method POST `
        -ContentType "application/json" `
        -Body $loginBody `
        -SessionVariable webSession `
        -UseBasicParsing

    $loginData = $loginResponse.Content | ConvertFrom-Json

    if ($loginData.requiresTwoFactor) {
        Write-Err "Account requires 2FA. Not supported in this script."
        exit 1
    }

    # Extract session token from cookies
    $sessionCookie = $webSession.Cookies.GetCookies("$BaseUrl") | Where-Object { $_.Name -like "*session*" }
    $sessionToken = $sessionCookie.Value

    if (-not $sessionToken) {
        # Fallback: try X-Session-Token header approach
        Write-Warn "No session cookie found, will use cookie jar from WebSession"
    }

    $userRole = $loginData.user.role
    if ($userRole -ne "Admin") {
        Write-Err "User role is '$userRole', Admin required"
        exit 1
    }

    Write-OK "Logged in as $($loginData.user.email) (Role: $userRole)"
}
catch {
    Write-Err "Login failed: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
        Write-Info $reader.ReadToEnd()
    }
    exit 1
}

# Build common request headers (session from cookie jar)
$headers = @{}
if ($sessionToken) {
    $headers["X-Session-Token"] = $sessionToken
}

# ─── Phase 1: Bulk Import SharedGames from BGG ──────────────────────────
if (-not $SkipImport) {
    Write-Phase "PHASE 1" "Bulk importing $($games.Count) games from BGG"

    $bggArray = $games | ForEach-Object { @{ bggId = [int]$_.bggId; name = $_.name } }
    $jsonContent = $bggArray | ConvertTo-Json -Compress

    $importBody = @{ jsonContent = $jsonContent } | ConvertTo-Json -Depth 5

    try {
        $importResponse = Invoke-RestMethod `
            -Uri "$BaseUrl/api/v1/admin/games/bulk-import" `
            -Method POST `
            -ContentType "application/json" `
            -Body $importBody `
            -Headers $headers `
            -WebSession $webSession

        Write-OK "Import result: Total=$($importResponse.total) Enqueued=$($importResponse.enqueued) Skipped=$($importResponse.skipped) Failed=$($importResponse.failed)"

        if ($importResponse.errors -and $importResponse.errors.Count -gt 0) {
            Write-Warn "Import issues:"
            $importResponse.errors | ForEach-Object {
                Write-Info "  $($_.gameName) (BGG:$($_.bggId)): $($_.reason) [$($_.errorType)]"
            }
        }

        if ($importResponse.enqueued -eq 0 -and $importResponse.skipped -eq $importResponse.total) {
            Write-OK "All games already in catalog (skipped as duplicates)"
        }
    }
    catch {
        Write-Err "Bulk import failed: $($_.Exception.Message)"
        if ($_.Exception.Response) {
            $statusCode = [int]$_.Exception.Response.StatusCode
            if ($statusCode -eq 429) {
                Write-Warn "Rate limited (1 req/5min). Wait 5 minutes and retry with -SkipImport if games were partially imported."
            }
            $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
            Write-Info $reader.ReadToEnd()
        }
        exit 1
    }

    # Wait for BGG background service to process all imports
    Write-Phase "PHASE 1b" "Waiting for BGG import processing (~1 game/sec)"

    $maxWaitSeconds = [math]::Max(120, $games.Count * 3)
    $elapsed = 0
    $lastEnqueued = -1

    while ($elapsed -lt $maxWaitSeconds) {
        Start-Sleep -Seconds 5
        $elapsed += 5

        try {
            # Check progress via SSE endpoint (single poll)
            $progressResponse = Invoke-RestMethod `
                -Uri "$BaseUrl/api/v1/admin/games/bulk-import/progress" `
                -Method GET `
                -Headers $headers `
                -WebSession $webSession `
                -TimeoutSec 10

            # SSE returns text; parse last progress event
            if ($progressResponse -match '"isActive"\s*:\s*false') {
                Write-OK "BGG import complete"
                break
            }

            # Extract counts from progress
            if ($progressResponse -match '"completed"\s*:\s*(\d+)') {
                $completed = [int]$Matches[1]
                Write-Info "Progress: $completed/$($games.Count) completed ($elapsed sec elapsed)"
            }
        }
        catch {
            # SSE endpoint may timeout; that's OK, just retry
            Write-Info "Polling... ($elapsed sec elapsed)"
        }
    }

    if ($elapsed -ge $maxWaitSeconds) {
        Write-Warn "Timeout waiting for BGG import. Some games may still be processing."
        Write-Info "Continue with -SkipImport when ready."
    }
}
else {
    Write-Info "Skipping Phase 1 (games already imported)"
}

# ─── Phase 2: Upload PDFs ───────────────────────────────────────────────
if (-not $SkipUpload) {
    Write-Phase "PHASE 2" "Uploading $($games.Count) PDF rulebooks"

    # For each game: resolve SharedGame ID via check-duplicate, then upload PDF
    $uploaded = 0
    $failed = 0
    $skippedUpload = 0

    foreach ($game in $games) {
        $pdfPath = Join-Path $rulebooksDir $game.filename
        $gameName = $game.name
        $bggId = [int]$game.bggId
        $language = if ($game.language) { $game.language } else { "en" }

        Write-Info "[$($uploaded + $failed + $skippedUpload + 1)/$($games.Count)] $gameName (BGG:$bggId)"

        # Step 2a: Get SharedGame ID via BGG duplicate check
        $sharedGameId = $null
        try {
            $dupCheck = Invoke-RestMethod `
                -Uri "$BaseUrl/api/v1/admin/shared-games/bgg/check-duplicate/$bggId" `
                -Method GET `
                -Headers $headers `
                -WebSession $webSession

            if ($dupCheck.existingGame) {
                $sharedGameId = $dupCheck.existingGame.id
                Write-Info "  SharedGame found: $sharedGameId"
            }
            elseif ($dupCheck.exists -eq $true -and $dupCheck.gameId) {
                $sharedGameId = $dupCheck.gameId
                Write-Info "  SharedGame found: $sharedGameId"
            }
        }
        catch {
            $statusCode = 0
            if ($_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
            }
            if ($statusCode -eq 429) {
                Write-Warn "  Rate limited on duplicate check. Waiting 10s..."
                Start-Sleep -Seconds 10
                # Retry once
                try {
                    $dupCheck = Invoke-RestMethod `
                        -Uri "$BaseUrl/api/v1/admin/shared-games/bgg/check-duplicate/$bggId" `
                        -Method GET `
                        -Headers $headers `
                        -WebSession $webSession

                    if ($dupCheck.existingGame) {
                        $sharedGameId = $dupCheck.existingGame.id
                    }
                }
                catch {
                    Write-Warn "  Could not resolve SharedGame ID for $gameName"
                }
            }
            else {
                Write-Warn "  Duplicate check failed: $($_.Exception.Message)"
            }
        }

        # Step 2b: Upload PDF
        try {
            $boundary = [System.Guid]::NewGuid().ToString()
            $LF = "`r`n"

            # Build multipart/form-data manually for PowerShell compatibility
            $fileBytes = [System.IO.File]::ReadAllBytes($pdfPath)
            $fileName = [System.IO.Path]::GetFileName($pdfPath)

            $bodyLines = @()

            # File field
            $bodyLines += "--$boundary"
            $bodyLines += "Content-Disposition: form-data; name=`"file`"; filename=`"$fileName`""
            $bodyLines += "Content-Type: application/pdf"
            $bodyLines += ""

            # Convert to bytes
            $headerBytes = [System.Text.Encoding]::UTF8.GetBytes(($bodyLines -join $LF) + $LF)

            # Additional form fields
            $formFields = @()

            if ($sharedGameId) {
                $formFields += "--$boundary${LF}Content-Disposition: form-data; name=`"gameId`"${LF}${LF}$sharedGameId"
            }
            else {
                # Fallback: use gameName for auto-creation
                $formFields += "--$boundary${LF}Content-Disposition: form-data; name=`"gameName`"${LF}${LF}$gameName"
            }

            $formFields += "--$boundary${LF}Content-Disposition: form-data; name=`"versionType`"${LF}${LF}base"
            $formFields += "--$boundary${LF}Content-Disposition: form-data; name=`"language`"${LF}${LF}$language"
            $formFields += "--$boundary${LF}Content-Disposition: form-data; name=`"versionNumber`"${LF}${LF}1.0"

            $fieldBytes = [System.Text.Encoding]::UTF8.GetBytes(($formFields -join $LF) + $LF)
            $endBytes   = [System.Text.Encoding]::UTF8.GetBytes("${LF}--${boundary}--${LF}")

            # Combine all parts
            $bodyStream = [System.IO.MemoryStream]::new()
            $bodyStream.Write($headerBytes, 0, $headerBytes.Length)
            $bodyStream.Write($fileBytes, 0, $fileBytes.Length)
            $bodyStream.Write($fieldBytes, 0, $fieldBytes.Length)
            $bodyStream.Write($endBytes, 0, $endBytes.Length)
            $bodyArray = $bodyStream.ToArray()
            $bodyStream.Dispose()

            $uploadResponse = Invoke-RestMethod `
                -Uri "$BaseUrl/api/v1/ingest/pdf" `
                -Method POST `
                -ContentType "multipart/form-data; boundary=$boundary" `
                -Body $bodyArray `
                -Headers $headers `
                -WebSession $webSession

            if ($uploadResponse.document) {
                Write-OK "  Uploaded: $($uploadResponse.document.id) (state: $($uploadResponse.document.processingState))"
                $uploaded++
            }
            elseif ($uploadResponse.documentId) {
                Write-OK "  Uploaded: $($uploadResponse.documentId)"
                $uploaded++
            }
            else {
                Write-OK "  Uploaded successfully"
                $uploaded++
            }
        }
        catch {
            $statusCode = 0
            $errorBody = ""
            if ($_.Exception.Response) {
                $statusCode = [int]$_.Exception.Response.StatusCode
                try {
                    $reader = [System.IO.StreamReader]::new($_.Exception.Response.GetResponseStream())
                    $errorBody = $reader.ReadToEnd()
                }
                catch {}
            }

            if ($statusCode -eq 409) {
                Write-Warn "  Duplicate PDF (already uploaded)"
                $skippedUpload++
            }
            elseif ($statusCode -eq 429) {
                Write-Warn "  Rate limited. Waiting 30s then retrying..."
                Start-Sleep -Seconds 30
                # Simple retry (won't recurse further)
                try {
                    $retryResponse = Invoke-RestMethod `
                        -Uri "$BaseUrl/api/v1/ingest/pdf" `
                        -Method POST `
                        -ContentType "multipart/form-data; boundary=$boundary" `
                        -Body $bodyArray `
                        -Headers $headers `
                        -WebSession $webSession
                    Write-OK "  Uploaded on retry"
                    $uploaded++
                }
                catch {
                    Write-Err "  Upload failed on retry: $($_.Exception.Message)"
                    $failed++
                }
            }
            else {
                Write-Err "  Upload failed ($statusCode): $errorBody"
                $failed++
            }
        }

        # Small delay between uploads to avoid rate limiting
        Start-Sleep -Milliseconds 500
    }

    Write-Phase "PHASE 2 COMPLETE" "Uploaded: $uploaded | Skipped: $skippedUpload | Failed: $failed"
}
else {
    Write-Info "Skipping Phase 2 (PDFs already uploaded)"
}

# ─── Phase 3: Monitor Processing Queue ──────────────────────────────────
Write-Phase "PHASE 3" "Monitoring processing queue (concurrency: 3)"
Write-Info "PDFs will process through: Pending > Uploading > Extracting > Chunking > Embedding > Indexing > Ready"
Write-Info "Estimated time: ~4 min/PDF x $($games.Count) games / 3 concurrent = ~$([math]::Ceiling($games.Count * 4 / 3)) min"
Write-Info ""

$maxWaitMinutes = 120
$checkIntervalSeconds = 30
$elapsed = 0

while ($elapsed -lt ($maxWaitMinutes * 60)) {
    Start-Sleep -Seconds $checkIntervalSeconds
    $elapsed += $checkIntervalSeconds

    try {
        $queueStatus = Invoke-RestMethod `
            -Uri "$BaseUrl/api/v1/admin/kb/processing-queue?pageSize=100" `
            -Method GET `
            -Headers $headers `
            -WebSession $webSession

        # Count by state
        $items = if ($queueStatus.items) { $queueStatus.items } elseif ($queueStatus.data) { $queueStatus.data } else { @() }

        $states = @{}
        foreach ($item in $items) {
            $state = $item.processingState
            if (-not $state) { $state = $item.status }
            if (-not $state) { $state = "Unknown" }
            if ($states.ContainsKey($state)) { $states[$state]++ } else { $states[$state] = 1 }
        }

        $readyCount     = if ($states.ContainsKey("Ready"))      { $states["Ready"] }      else { 0 }
        $failedCount    = if ($states.ContainsKey("Failed"))     { $states["Failed"] }     else { 0 }
        $processingCount = $items.Count - $readyCount - $failedCount

        $elapsedMin = [math]::Round($elapsed / 60, 1)
        $statesSummary = ($states.GetEnumerator() | Sort-Object Name | ForEach-Object { "$($_.Name):$($_.Value)" }) -join " | "

        Write-Info "[$elapsedMin min] $statesSummary"

        if ($processingCount -eq 0 -and $items.Count -gt 0) {
            Write-Phase "COMPLETE" "All documents processed!"
            Write-OK "Ready: $readyCount | Failed: $failedCount | Total: $($items.Count)"

            if ($failedCount -gt 0) {
                Write-Warn "Failed documents:"
                $items | Where-Object { $_.processingState -eq "Failed" -or $_.status -eq "Failed" } | ForEach-Object {
                    $name = if ($_.fileName) { $_.fileName } else { $_.name }
                    $err  = if ($_.processingError) { $_.processingError } else { $_.errorCategory }
                    Write-Info "  - ${name}: ${err}"
                }
                Write-Info ""
                Write-Info "Retry failed: POST $BaseUrl/api/v1/admin/queue/reindex-failed"
            }
            break
        }
    }
    catch {
        Write-Warn "Queue poll failed: $($_.Exception.Message) (retrying in ${checkIntervalSeconds}s)"
    }
}

if ($elapsed -ge ($maxWaitMinutes * 60)) {
    Write-Warn "Timeout ($maxWaitMinutes min). Processing may still be running."
    Write-Info "Check manually: GET $BaseUrl/api/v1/admin/kb/processing-queue"
}

# ─── Validation Checklist ────────────────────────────────────────────────
Write-Phase "VALIDATION" "Post-initialization checklist"
Write-Info ""
Write-Info "Manual validation steps:"
Write-Info "  1. Check all PDFs Ready:   GET $BaseUrl/api/v1/admin/kb/processing-queue?statusFilter=Ready"
Write-Info "  2. Check vector collections: GET $BaseUrl/api/v1/admin/kb/vector-collections"
Write-Info "  3. Retry failures:         POST $BaseUrl/api/v1/admin/queue/reindex-failed"
Write-Info "  4. RAG smoke test:         Ask agent 'What are the rules of [Game]?' for 3 random games"
Write-Info "  5. Purge stale:            POST $BaseUrl/api/v1/admin/pdfs/maintenance/purge-stale"
Write-Info ""
Write-Host "Done!" -ForegroundColor Green
