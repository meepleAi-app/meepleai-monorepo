# Delete Qdrant collection to recreate with correct dimensions
$QdrantUrl = "http://localhost:6333"
$CollectionName = "meepleai_documents"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Deleting Qdrant Collection" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Write-Host "[1/2] Checking if collection exists..." -ForegroundColor Yellow
try {
    $response = Invoke-RestMethod -Uri "$QdrantUrl/collections/$CollectionName" -Method Get -ErrorAction Stop
    Write-Host "[OK] Collection '$CollectionName' exists" -ForegroundColor Green
    Write-Host "  Vector size: $($response.result.config.params.vectors.size)" -ForegroundColor Gray
    Write-Host "  Points count: $($response.result.points_count)" -ForegroundColor Gray
} catch {
    if ($_.Exception.Response.StatusCode -eq 404) {
        Write-Host "[INFO] Collection '$CollectionName' does not exist" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Nothing to delete. You can now restart the API to create the collection with correct dimensions." -ForegroundColor Cyan
        exit 0
    }
    Write-Host "[FAIL] Failed to check collection: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "[2/2] Deleting collection '$CollectionName'..." -ForegroundColor Yellow
try {
    $deleteResponse = Invoke-RestMethod -Uri "$QdrantUrl/collections/$CollectionName" -Method Delete -ErrorAction Stop
    Write-Host "[OK] Collection '$CollectionName' deleted successfully" -ForegroundColor Green
} catch {
    Write-Host "[FAIL] Failed to delete collection: $_" -ForegroundColor Red
    exit 1
}
Write-Host ""

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Collection deleted successfully!" -ForegroundColor Green
Write-Host "Next step: Rebuild and restart the API to recreate the collection with 768 dimensions" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
