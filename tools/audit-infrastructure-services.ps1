#!/usr/bin/env pwsh
# Infrastructure Services Audit Script
# Issue #1680 - Analyzes usage and metadata for all infrastructure services

param(
    [string]$ServicesPath = "apps/api/src/Api/Services",
    [string]$OutputPath = "infrastructure-services-inventory.json"
)

Write-Host "🔍 Infrastructure Services Audit - Issue #1680" -ForegroundColor Cyan
Write-Host "================================================" -ForegroundColor Cyan
Write-Host ""

# Find all service implementation files (exclude interfaces)
$serviceFiles = Get-ChildItem -Path $ServicesPath -Filter "*Service.cs" |
    Where-Object { $_.Name -notmatch "^I[A-Z]" }

Write-Host "Found $($serviceFiles.Count) service files" -ForegroundColor Green
Write-Host ""

$inventory = @()

foreach ($file in $serviceFiles) {
    $serviceName = $file.BaseName
    $relativePath = $file.FullName.Replace((Get-Location).Path, "").TrimStart('\', '/')

    Write-Host "Analyzing: $serviceName" -ForegroundColor Yellow

    # Count references across codebase
    $apiPath = "apps/api/src/Api"
    $references = (Select-String -Path "$apiPath/**/*.cs" -Pattern "\b$serviceName\b" -ErrorAction SilentlyContinue).Count

    # Find DI registrations
    $diRegistrations = Select-String -Path "$apiPath/Program.cs","$apiPath/**/ServiceCollectionExtensions.cs" `
        -Pattern "Add(Scoped|Singleton|Transient)<.*$serviceName" -ErrorAction SilentlyContinue

    # Determine category based on service name pattern
    $category = switch -Regex ($serviceName) {
        "Admin|Stats|Audit" { "Administration" }
        "Alert|Email|Slack|PagerDuty" { "Alerting" }
        "OAuth|Password|Totp|TempSession|Session|ApiKey|Encryption" { "Authentication" }
        "Pdf|Upload|Document" { "DocumentProcessing" }
        "Bgg|Game" { "GameManagement" }
        "Rag|Embedding|Hybrid|Keyword|Qdrant|TextChunking|LlmService" { "KnowledgeBase" }
        "Configuration|FeatureFlag|Cache" { "SystemConfiguration" }
        "N8n|Workflow|BackgroundTask" { "WorkflowIntegration" }
        "Export|Language" { "Utilities" }
        default { "Uncategorized" }
    }

    # Get file size and line count
    $lineCount = (Get-Content $file.FullName).Count
    $fileSize = [math]::Round($file.Length / 1KB, 2)

    $serviceData = [PSCustomObject]@{
        Name = $serviceName
        Path = $relativePath
        Category = $category
        References = $references
        DIRegistrations = $diRegistrations.Count
        LineCount = $lineCount
        FileSizeKB = $fileSize
        LastModified = $file.LastWriteTime.ToString("yyyy-MM-dd")
    }

    $inventory += $serviceData

    Write-Host "  References: $references | DI: $($diRegistrations.Count) | Lines: $lineCount" -ForegroundColor Gray
}

# Sort by references (most used first)
$inventory = $inventory | Sort-Object -Property References -Descending

# Export to JSON
$inventory | ConvertTo-Json -Depth 3 | Out-File $OutputPath -Encoding UTF8

Write-Host ""
Write-Host "✅ Audit complete!" -ForegroundColor Green
Write-Host "📊 Total services analyzed: $($inventory.Count)" -ForegroundColor Cyan
Write-Host "📄 Output saved to: $OutputPath" -ForegroundColor Cyan
Write-Host ""

# Summary statistics
$totalRefs = ($inventory | Measure-Object -Property References -Sum).Sum
$avgRefs = [math]::Round(($inventory | Measure-Object -Property References -Average).Average, 1)
$unusedServices = ($inventory | Where-Object { $_.References -le 1 }).Count

Write-Host "📈 Statistics:" -ForegroundColor Magenta
Write-Host "  Total references: $totalRefs" -ForegroundColor Gray
Write-Host "  Average refs/service: $avgRefs" -ForegroundColor Gray
Write-Host "  Potentially unused: $unusedServices" -ForegroundColor Gray
Write-Host ""

# Top 10 most used services
Write-Host "🏆 Top 10 Most Used Services:" -ForegroundColor Magenta
$inventory | Select-Object -First 10 | Format-Table Name, References, Category -AutoSize
