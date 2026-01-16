# MeepleAI Secrets Setup Script (PowerShell)
# Copies all .secret.example files to .secret files for initial configuration

Write-Host "MeepleAI Secrets Setup" -ForegroundColor Cyan
Write-Host "======================" -ForegroundColor Cyan
Write-Host ""

# Change to secrets directory
$secretsDir = $PSScriptRoot
Set-Location $secretsDir

# Get all .secret.example files
$exampleFiles = Get-ChildItem -Filter "*.secret.example"

if ($exampleFiles.Count -eq 0) {
    Write-Host "❌ No .secret.example files found in current directory" -ForegroundColor Red
    Write-Host "   Expected location: infra/secrets/" -ForegroundColor Yellow
    exit 1
}

Write-Host "Found $($exampleFiles.Count) example files:" -ForegroundColor Green
Write-Host ""

# Copy each example file to .secret
$copied = 0
$skipped = 0

foreach ($file in $exampleFiles) {
    $targetName = $file.Name -replace '\.example$', ''
    $targetPath = Join-Path $secretsDir $targetName

    if (Test-Path $targetPath) {
        Write-Host "⏭️  Skipped: $targetName (already exists)" -ForegroundColor Yellow
        $skipped++
    } else {
        Copy-Item $file.FullName -Destination $targetPath
        Write-Host "✅ Copied:  $($file.Name) → $targetName" -ForegroundColor Green
        $copied++
    }
}

Write-Host ""
Write-Host "Summary:" -ForegroundColor Cyan
Write-Host "  Copied:  $copied files" -ForegroundColor Green
Write-Host "  Skipped: $skipped files (already existed)" -ForegroundColor Yellow
Write-Host ""

if ($copied -gt 0) {
    Write-Host "⚠️  IMPORTANT: Edit the .secret files with your actual credentials!" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Critical files (MUST configure before startup):" -ForegroundColor Red
    Write-Host "  • admin.secret" -ForegroundColor Red
    Write-Host "  • database.secret" -ForegroundColor Red
    Write-Host "  • jwt.secret" -ForegroundColor Red
    Write-Host "  • qdrant.secret" -ForegroundColor Red
    Write-Host "  • redis.secret" -ForegroundColor Red
    Write-Host "  • embedding-service.secret" -ForegroundColor Red
    Write-Host ""
    Write-Host "Quick edit: notepad <filename>.secret" -ForegroundColor Cyan
}

Write-Host "Done! ✨" -ForegroundColor Green
