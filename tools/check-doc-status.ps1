# Check document processing status
$DocumentId = "c133c767-27ca-40fb-8fd7-152c2780e2ff"
$ApiBaseUrl = "http://localhost:8080"

# Login
$loginBody = @{ email = "admin@meepleai.dev"; password = "pVKOMQNK0tFNgGlX" } | ConvertTo-Json
$loginFile = New-TemporaryFile
$cookieJar = New-TemporaryFile

$loginBody | Out-File -FilePath $loginFile -Encoding UTF8 -NoNewline
& curl.exe -s -X POST "$ApiBaseUrl/api/v1/auth/login" -H "Content-Type: application/json" -d "@$loginFile" -c $cookieJar -o nul

# Check status
$response = & curl.exe -s -b $cookieJar "$ApiBaseUrl/api/v1/pdf/$DocumentId"
$doc = $response | ConvertFrom-Json

Write-Host "Document: $DocumentId" -ForegroundColor Cyan
Write-Host "  Status: $($doc.ProcessingStatus)" -ForegroundColor $(if ($doc.ProcessingStatus -eq 'Completed') { 'Green' } elseif ($doc.ProcessingStatus -eq 'Failed') { 'Red' } else { 'Yellow' })
Write-Host "  Pages: $($doc.PageCount)"
Write-Host "  Characters: $($doc.CharacterCount)"
if ($doc.ProcessingError) {
    Write-Host "  Error: $($doc.ProcessingError)" -ForegroundColor Red
}

# Check Qdrant chunks count
Write-Host "`nQdrant Chunks:" -ForegroundColor Cyan
$qdrant = & curl.exe -s "http://localhost:6333/collections/meepleai_documents"  | ConvertFrom-Json
Write-Host "  Total points: $($qdrant.result.points_count)"

Remove-Item $cookieJar -ErrorAction SilentlyContinue
