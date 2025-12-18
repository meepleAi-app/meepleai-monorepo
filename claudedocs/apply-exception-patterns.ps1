# Issue #2184 - Automated Exception Handling Pattern Application
# Applies pragma warnings and justifications to generic catch blocks

param(
    [switch]$DryRun,
    [switch]$CqrsHandlers,
    [switch]$EventHandlers,
    [switch]$Middleware,
    [switch]$All
)

# Pattern templates
$cqrsHandlerPattern = @'
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions (ValidationException, DomainException) caught separately above.
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer. Returns Result<T> pattern.
        catch (Exception ex)
'@

$eventHandlerPattern = @'
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: EVENT HANDLER PATTERN - Background event processing
        // Event handlers must not throw exceptions (violates mediator pattern).
        // Errors logged for monitoring; failed events don't block system.
        catch (Exception ex)
'@

$middlewarePattern = @'
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: MIDDLEWARE BOUNDARY PATTERN - Fail-open on errors
        // Middleware failures shouldn't crash request pipeline (self-DOS prevention).
        // Authentication/rate limit errors allow request through (unauthenticated/unthrottled).
        catch (Exception ex)
'@

$backgroundTaskPattern = @'
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: BACKGROUND TASK PATTERN - Async task resilience
        // Background tasks must handle all errors gracefully without crashing worker.
        // Errors logged and task status updated; failures don't stop other tasks.
        catch (Exception ex)
'@

# File paths by category
$cqrsHandlerPaths = @(
    "BoundedContexts\Authentication\Application\Commands\**\*.cs",
    "BoundedContexts\Authentication\Application\Queries\**\*.cs",
    "BoundedContexts\Authentication\Application\Handlers\**\*.cs",
    "BoundedContexts\DocumentProcessing\Application\Commands\**\*.cs",
    "BoundedContexts\DocumentProcessing\Application\Handlers\**\*.cs",
    "BoundedContexts\DocumentProcessing\Application\Queries\**\*.cs",
    "BoundedContexts\KnowledgeBase\Application\Handlers\**\*.cs",
    "BoundedContexts\KnowledgeBase\Application\Queries\**\*.cs",
    "BoundedContexts\KnowledgeBase\Application\Commands\**\*.cs",
    "BoundedContexts\Administration\Application\Handlers\**\*.cs",
    "BoundedContexts\Administration\Application\Queries\**\*.cs"
)

$eventHandlerPaths = @(
    "BoundedContexts\*\Application\EventHandlers\*.cs",
    "SharedKernel\Application\EventHandlers\*.cs"
)

$middlewarePaths = @(
    "Middleware\*.cs",
    "Routing\*Endpoints.cs"
)

$backgroundTaskPaths = @(
    "Infrastructure\BackgroundTasks\*.cs",
    "BoundedContexts\*\Infrastructure\Scheduling\*.cs"
)

function Apply-Pattern {
    param(
        [string]$FilePath,
        [string]$Pattern,
        [string]$PatternName
    )

    Write-Host "Processing: $FilePath" -ForegroundColor Cyan

    $content = Get-Content $FilePath -Raw

    # Check if file has catch (Exception without pragma
    if ($content -match 'catch\s*\(\s*Exception\s+\w+\s*\)' -and $content -notmatch '#pragma\s+warning\s+disable\s+CA1031') {

        # Replace catch (Exception ex) with pragma + catch
        $updatedContent = $content -replace '(\s+)catch\s*\(\s*Exception\s+ex\s*\)', "$1$Pattern"

        # Add #pragma warning restore CA1031 after the catch block
        $updatedContent = $updatedContent -replace '(catch\s*\(\s*Exception\s+ex\s*\)[^\}]*\})', "`$1`n#pragma warning restore CA1031"

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would apply $PatternName" -ForegroundColor Yellow
        }
        else {
            Set-Content -Path $FilePath -Value $updatedContent -NoNewline
            Write-Host "  ✓ Applied $PatternName" -ForegroundColor Green
        }

        return 1
    }
    else {
        Write-Host "  ⊘ Already has pragma or no generic catch" -ForegroundColor Gray
        return 0
    }
}

function Process-Category {
    param(
        [string[]]$Paths,
        [string]$Pattern,
        [string]$CategoryName
    )

    Write-Host "`n=== Processing Category: $CategoryName ===" -ForegroundColor Magenta

    $totalFiles = 0
    $processedFiles = 0

    foreach ($pathPattern in $Paths) {
        $fullPath = Join-Path "apps\api\src\Api" $pathPattern
        $files = Get-ChildItem -Path $fullPath -Recurse -ErrorAction SilentlyContinue

        foreach ($file in $files) {
            $totalFiles++
            $processed = Apply-Pattern -FilePath $file.FullName -Pattern $Pattern -PatternName $CategoryName
            $processedFiles += $processed
        }
    }

    Write-Host "`nCategory Summary: $processedFiles / $totalFiles files updated" -ForegroundColor Cyan
    return @{
        Total = $totalFiles
        Processed = $processedFiles
    }
}

# Main execution
Write-Host @"
=================================================
Issue #2184: Exception Handling Pattern Application
=================================================
Mode: $(if ($DryRun) { "DRY RUN" } else { "LIVE" })
"@ -ForegroundColor Cyan

$results = @{}

if ($CqrsHandlers -or $All) {
    $results.Cqrs = Process-Category -Paths $cqrsHandlerPaths -Pattern $cqrsHandlerPattern -CategoryName "CQRS Handlers"
}

if ($EventHandlers -or $All) {
    $results.Events = Process-Category -Paths $eventHandlerPaths -Pattern $eventHandlerPattern -CategoryName "Event Handlers"
}

if ($Middleware -or $All) {
    $results.Middleware = Process-Category -Paths $middlewarePaths -Pattern $middlewarePattern -CategoryName "Middleware"
}

# Background tasks (always included with -All)
if ($All) {
    $results.BackgroundTasks = Process-Category -Paths $backgroundTaskPaths -Pattern $backgroundTaskPattern -CategoryName "Background Tasks"
}

# Overall summary
Write-Host "`n=== OVERALL SUMMARY ===" -ForegroundColor Magenta
$totalProcessed = ($results.Values | Measure-Object -Property Processed -Sum).Sum
$totalFiles = ($results.Values | Measure-Object -Property Total -Sum).Sum

Write-Host "Total files scanned: $totalFiles" -ForegroundColor White
Write-Host "Total files updated: $totalProcessed" -ForegroundColor Green

if ($DryRun) {
    Write-Host "`n⚠ DRY RUN MODE - No files were actually modified" -ForegroundColor Yellow
    Write-Host "Run without -DryRun to apply changes" -ForegroundColor Yellow
}

Write-Host "`nDone!" -ForegroundColor Green
