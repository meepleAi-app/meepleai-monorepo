#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Seeds the default POC agent (MeepleAssistant) into the database.

.DESCRIPTION
    This script creates a default multi-purpose board game AI agent with:
    - Professional consultation capabilities
    - Claude 3 Haiku (quasi-free model)
    - Tool calling enabled for KB access
    - RAG integration ready

    The seeder is idempotent - safe to run multiple times.

.PARAMETER ConnectionString
    PostgreSQL connection string. If not provided, reads from infra/secrets/database.secret

.PARAMETER AdminEmail
    Email of admin user to use as CreatedBy. Defaults to searching for any admin user.

.PARAMETER Force
    Force re-seed by deleting existing agent first (⚠️ destructive).

.EXAMPLE
    .\scripts\seed-default-agent.ps1

    Uses default connection string from secrets and auto-finds admin user.

.EXAMPLE
    .\scripts\seed-default-agent.ps1 -AdminEmail "admin@meepleai.com"

    Seeds agent with specific admin user.

.EXAMPLE
    .\scripts\seed-default-agent.ps1 -Force

    Deletes existing agent and re-seeds (⚠️ loses invocation history).

.NOTES
    Requires:
    - PostgreSQL running (docker compose up -d postgres)
    - Database migrated (dotnet ef database update)
    - Admin user in database
    - .NET SDK 9.0+
#>

param(
    [string]$ConnectionString,
    [string]$AdminEmail,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Colors for output
function Write-Success { param($Message) Write-Host "✅ $Message" -ForegroundColor Green }
function Write-Info { param($Message) Write-Host "ℹ️  $Message" -ForegroundColor Cyan }
function Write-Warning { param($Message) Write-Host "⚠️  $Message" -ForegroundColor Yellow }
function Write-Error { param($Message) Write-Host "❌ $Message" -ForegroundColor Red }

Write-Info "Default Agent Seeder - POC Baseline Agent"
Write-Host ""

# Get repository root
$RepoRoot = Split-Path -Parent $PSScriptRoot
$ApiProject = Join-Path $RepoRoot "apps\api\src\Api\Api.csproj"

if (-not (Test-Path $ApiProject)) {
    Write-Error "API project not found at: $ApiProject"
    exit 1
}

# Get connection string
if (-not $ConnectionString) {
    $SecretFile = Join-Path $RepoRoot "infra\secrets\database.secret"
    if (Test-Path $SecretFile) {
        Write-Info "Reading connection string from: $SecretFile"
        $ConnectionString = (Get-Content $SecretFile -Raw).Trim()
    }
    else {
        Write-Error "Connection string not provided and database.secret not found"
        Write-Host "Run: .\infra\secrets\setup-secrets.ps1 -SaveGenerated"
        exit 1
    }
}

# Validate connection
Write-Info "Testing database connection..."
try {
    $PgResult = & docker exec meepleai-postgres pg_isready -U postgres 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "PostgreSQL container not responding. Starting services..."
        Push-Location (Join-Path $RepoRoot "infra")
        & docker compose up -d postgres
        Start-Sleep -Seconds 5
        Pop-Location
    }
    Write-Success "Database connection OK"
}
catch {
    Write-Warning "Could not verify PostgreSQL via Docker. Proceeding with connection string..."
}

# Create temporary seeder console app
$TempDir = Join-Path $env:TEMP "meepleai-seeder-$(Get-Random)"
New-Item -ItemType Directory -Path $TempDir | Out-Null

$SeederCode = @"
using Api.Infrastructure;
using Api.Infrastructure.Seeders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var connectionString = args[0];
var adminEmail = args.Length > 1 ? args[1] : null;
var force = args.Length > 2 && args[2] == "force";

var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
    .UseNpgsql(connectionString)
    .Options;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("DefaultAgentSeeder");

await using var db = new MeepleAiDbContext(options);

// Get admin user
var adminUser = string.IsNullOrEmpty(adminEmail)
    ? await db.Users.FirstOrDefaultAsync(u => u.Role == "Admin")
    : await db.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);

if (adminUser == null)
{
    Console.WriteLine("❌ No admin user found. Create an admin user first.");
    return 1;
}

Console.WriteLine(`$"ℹ️  Using admin user: {adminUser.Email} (ID: {adminUser.Id})");

// Force delete if requested
if (force)
{
    Console.WriteLine("⚠️  Force mode: Deleting existing agent...");
    var existing = await db.Set<Api.Infrastructure.Entities.AgentEntity>()
        .FirstOrDefaultAsync(a => a.Name == "MeepleAssistant POC");

    if (existing != null)
    {
        var configs = db.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>()
            .Where(c => c.AgentId == existing.Id);
        db.RemoveRange(configs);
        db.Remove(existing);
        await db.SaveChangesAsync();
        Console.WriteLine("✅ Existing agent deleted");
    }
}

// Seed agent
await DefaultAgentSeeder.SeedDefaultAgentAsync(db, adminUser.Id, logger);

Console.WriteLine("✅ Default agent seeding complete!");
Console.WriteLine("");
Console.WriteLine("Agent Details:");
var agent = await db.Set<Api.Infrastructure.Entities.AgentEntity>()
    .FirstAsync(a => a.Name == "MeepleAssistant POC");
Console.WriteLine(`$"  ID: {agent.Id}");
Console.WriteLine(`$"  Name: {agent.Name}");
Console.WriteLine(`$"  Type: {agent.Type}");
Console.WriteLine(`$"  Strategy: {agent.StrategyName}");
Console.WriteLine(`$"  Active: {agent.IsActive}");

var config = await db.Set<Api.Infrastructure.Entities.KnowledgeBase.AgentConfigurationEntity>()
    .FirstAsync(c => c.AgentId == agent.Id);
Console.WriteLine("");
Console.WriteLine("Configuration:");
Console.WriteLine(`$"  Model: {config.LlmModel}");
Console.WriteLine(`$"  Temperature: {config.Temperature}");
Console.WriteLine(`$"  Max Tokens: {config.MaxTokens}");
Console.WriteLine(`$"  Mode: {(config.AgentMode == 0 ? "Chat" : "Other")}");
Console.WriteLine(`$"  System Prompt: {config.SystemPromptOverride?.Length ?? 0} characters");

return 0;
"@

$ProjectFile = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="$ApiProject" />
  </ItemGroup>
</Project>
"@

Set-Content -Path (Join-Path $TempDir "Program.cs") -Value $SeederCode
Set-Content -Path (Join-Path $TempDir "Seeder.csproj") -Value $ProjectFile

# Build and run
Write-Info "Building temporary seeder application..."
Push-Location $TempDir
try {
    $BuildOutput = & dotnet build --nologo --verbosity quiet 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed:"
        Write-Host $BuildOutput
        exit 1
    }

    Write-Info "Running seeder..."
    Write-Host ""

    $RunArgs = @($ConnectionString)
    if ($AdminEmail) { $RunArgs += $AdminEmail }
    if ($Force) { $RunArgs += "force" }

    & dotnet run --no-build --verbosity quiet -- @RunArgs
    $ExitCode = $LASTEXITCODE

    Write-Host ""
    if ($ExitCode -eq 0) {
        Write-Success "Seeding completed successfully!"
        Write-Host ""
        Write-Info "Next steps:"
        Write-Host "  1. Test agent via API: http://localhost:8080/scalar/v1"
        Write-Host "  2. Create chat thread with agent"
        Write-Host "  3. Verify responses are professional and accurate"
        Write-Host "  4. Prepare for RAG integration (select documents)"
    }
    else {
        Write-Error "Seeding failed with exit code: $ExitCode"
    }
}
finally {
    Pop-Location
    Remove-Item -Recurse -Force $TempDir -ErrorAction SilentlyContinue
}

exit $ExitCode
