# Test Generation Script for Issue #2308 Week 4
# Generates comprehensive backend handler tests from priority handler list
# Pattern: Null command, valid success, validation errors, repository exceptions

param(
    [Parameter(Mandatory=$false)]
    [string]$HandlerListFile = ".\scripts\priority-handlers.txt",

    [Parameter(Mandatory=$false)]
    [string]$OutputBaseDir = ".\apps\api\tests\Api.Tests\BoundedContexts",

    [Parameter(Mandatory=$false)]
    [switch]$DryRun,

    [Parameter(Mandatory=$false)]
    [switch]$Verbose
)

$ErrorActionPreference = "Stop"

Write-Host "🧪 Test Generation Script for Issue #2308 Week 4" -ForegroundColor Cyan
Write-Host "=" * 70

# Priority handlers from quality-engineer analysis
# Top 15 handlers for initial batch (expand to 40-50 total)
$priorityHandlers = @(
    # Authentication - High Priority (User-facing, security critical)
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\Registration\RegisterCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\Login\LoginCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\ApiKeys\CreateApiKeyCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\ApiKeys\RotateApiKeyCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\Sessions\CreateSessionCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\Sessions\RevokeSessionCommandHandler.cs",

    # Game Management - Core Business Logic
    ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\StartGameSessionCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\EndGameSessionCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\CreateGameFAQCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\UpdateGameFAQCommandHandler.cs",

    # Knowledge Base - RAG Core
    ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\CreateChatThreadCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\AddMessageCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\CreateAgentCommandHandler.cs",

    # Administration - System Management
    ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\CreateUserCommandHandler.cs",
    ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\ChangeUserRoleCommandHandler.cs"
)

# Additional handlers to reach 40-50 tests (commented for Phase 2)
$additionalHandlers = @(
    # Authentication - Extended
    # ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\ApiKeys\CreateApiKeyManagementCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\ApiKeys\RevokeApiKeyManagementCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\OAuth\InitiateOAuthLoginCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\OAuth\HandleOAuthCallbackCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Authentication\Application\Commands\PasswordReset\RequestPasswordResetCommandHandler.cs",

    # Game Management - Extended
    # ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\PauseGameSessionCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\ResumeGameSessionCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\AddPlayerToSessionCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\GameManagement\Application\Handlers\GenerateRuleSpecFromPdfCommandHandler.cs",

    # Knowledge Base - Extended
    # ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\CloseThreadCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\ReopenThreadCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\KnowledgeBase\Application\Handlers\DeleteChatThreadCommandHandler.cs",

    # System Configuration
    # ".\apps\api\src\Api\BoundedContexts\SystemConfiguration\Application\Handlers\UpdateConfigValueCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\SystemConfiguration\Application\Handlers\ToggleConfigurationCommandHandler.cs",

    # Administration - Extended
    # ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\UpdateUserCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\DeleteUserCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\SendAlertCommandHandler.cs",
    # ".\apps\api\src\Api\BoundedContexts\Administration\Application\Handlers\ResolveAlertCommandHandler.cs"
)

function Get-BoundedContextFromPath {
    param([string]$Path)

    if ($Path -match "BoundedContexts\\(\w+)\\") {
        return $Matches[1]
    }
    return "Unknown"
}

function Get-TestOutputPath {
    param(
        [string]$HandlerPath,
        [string]$BaseDir
    )

    $boundedContext = Get-BoundedContextFromPath $HandlerPath
    $handlerFileName = [System.IO.Path]::GetFileNameWithoutExtension($HandlerPath)
    $testFileName = "${handlerFileName}Tests.cs"

    # Determine subdirectory structure from handler path
    $subPath = ""
    if ($HandlerPath -match "BoundedContexts\\$boundedContext\\(.+)\\$handlerFileName") {
        $subPath = $Matches[1]
    }

    $testDir = Join-Path $BaseDir $boundedContext $subPath
    return Join-Path $testDir $testFileName
}

function Test-DotnetScriptInstalled {
    try {
        $null = dotnet script --version 2>&1
        return $true
    } catch {
        return $false
    }
}

# Check prerequisites
if (-not (Test-DotnetScriptInstalled)) {
    Write-Host "❌ dotnet-script is not installed" -ForegroundColor Red
    Write-Host "   Install with: dotnet tool install -g dotnet-script" -ForegroundColor Yellow
    exit 1
}

if (-not (Test-Path ".\scripts\TestGenerator.csx")) {
    Write-Host "❌ TestGenerator.csx not found in .\scripts\" -ForegroundColor Red
    exit 1
}

# Statistics
$totalHandlers = $priorityHandlers.Count
$successCount = 0
$failCount = 0
$skippedCount = 0
$totalTestsGenerated = 0

Write-Host "`n📊 Processing $totalHandlers priority handlers..." -ForegroundColor Cyan

foreach ($handlerPath in $priorityHandlers) {
    $handlerName = [System.IO.Path]::GetFileName($handlerPath)
    $boundedContext = Get-BoundedContextFromPath $handlerPath

    Write-Host "`n[$($successCount + $failCount + $skippedCount + 1)/$totalHandlers] $handlerName" -ForegroundColor White
    Write-Host "  Context: $boundedContext" -ForegroundColor Gray

    if (-not (Test-Path $handlerPath)) {
        Write-Host "  ⚠️  Handler file not found, skipping" -ForegroundColor Yellow
        $skippedCount++
        continue
    }

    $testOutputPath = Get-TestOutputPath -HandlerPath $handlerPath -BaseDir $OutputBaseDir

    # Check if test already exists
    if (Test-Path $testOutputPath) {
        Write-Host "  ℹ️  Test file already exists: $testOutputPath" -ForegroundColor Cyan

        if (-not $DryRun) {
            $response = Read-Host "  Overwrite? (y/N)"
            if ($response -ne 'y' -and $response -ne 'Y') {
                Write-Host "  ⏭️  Skipped" -ForegroundColor Yellow
                $skippedCount++
                continue
            }
        } else {
            Write-Host "  ⏭️  Skipped (dry-run)" -ForegroundColor Yellow
            $skippedCount++
            continue
        }
    }

    if ($DryRun) {
        Write-Host "  📝 Would generate: $testOutputPath" -ForegroundColor Gray
        $successCount++
        continue
    }

    try {
        # Create output directory
        $testDir = [System.IO.Path]::GetDirectoryName($testOutputPath)
        if (-not (Test-Path $testDir)) {
            New-Item -ItemType Directory -Path $testDir -Force | Out-Null
        }

        # Run test generator
        $generatorOutput = & dotnet script .\scripts\TestGenerator.csx $handlerPath $testDir 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "  ✅ Generated successfully" -ForegroundColor Green

            if ($Verbose) {
                $generatorOutput | ForEach-Object {
                    Write-Host "     $_" -ForegroundColor DarkGray
                }
            }

            # Extract estimated test count from generator output
            $testCountMatch = $generatorOutput | Select-String "Estimated test count: (\d+)"
            if ($testCountMatch) {
                $testCount = [int]$testCountMatch.Matches[0].Groups[1].Value
                $totalTestsGenerated += $testCount
                Write-Host "  📊 Generated ~$testCount tests" -ForegroundColor Gray
            }

            $successCount++
        } else {
            Write-Host "  ❌ Generation failed" -ForegroundColor Red
            if ($Verbose) {
                $generatorOutput | ForEach-Object {
                    Write-Host "     $_" -ForegroundColor Red
                }
            }
            $failCount++
        }
    } catch {
        Write-Host "  ❌ Error: $($_.Exception.Message)" -ForegroundColor Red
        $failCount++
    }
}

# Summary
Write-Host "`n" + ("=" * 70) -ForegroundColor Cyan
Write-Host "📈 Generation Summary" -ForegroundColor Cyan
Write-Host ("=" * 70) -ForegroundColor Cyan
Write-Host "Total Handlers:  $totalHandlers"
Write-Host "✅ Successful:   $successCount" -ForegroundColor Green
Write-Host "❌ Failed:       $failCount" -ForegroundColor Red
Write-Host "⏭️  Skipped:      $skippedCount" -ForegroundColor Yellow
Write-Host "📊 Tests Generated: ~$totalTestsGenerated" -ForegroundColor Cyan

if ($DryRun) {
    Write-Host "`n💡 This was a dry run. Run without -DryRun to generate files." -ForegroundColor Yellow
}

if ($successCount -gt 0) {
    Write-Host "`n✨ Next Steps:" -ForegroundColor Cyan
    Write-Host "  1. Review generated test files in $OutputBaseDir"
    Write-Host "  2. Manually adjust mock setups and assertions as needed"
    Write-Host "  3. Run tests: dotnet test"
    Write-Host "  4. Update test count in Issue #2308 progress tracker"
}

exit $failCount
