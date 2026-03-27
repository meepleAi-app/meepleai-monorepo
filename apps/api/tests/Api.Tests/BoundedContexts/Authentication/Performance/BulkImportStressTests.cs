using Api.BoundedContexts.Authentication.Application.Commands.ApiKeys;
using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Queries;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using System.Diagnostics;
using System.Text;
using FluentAssertions;
using Xunit;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;

namespace Api.Tests.BoundedContexts.Authentication.Performance;

/// <summary>
/// Performance Stress Tests for Bulk API Key Import (Issue #914).
/// Validates bulk import performance requirements:
/// - Import 1000+ users in under 30 seconds
/// - All keys generated successfully
/// - Database performance under load
/// </summary>
/// <remarks>
/// Performance Requirements:
/// - Target: 1000 users in &lt;30 seconds
/// - Baseline: 500 users for comparison
/// - Validation: All keys must be valid and unique
///
/// Infrastructure:
/// - Testcontainers PostgreSQL for realistic database performance
/// - Stopwatch for precise timing measurement
/// - Connection pooling enabled for optimal performance
///
/// Pattern: Integration testing with real database, performance-focused assertions
/// Issue #2603: Marked as Skip=CI to run separately in performance suite
/// </remarks>
[Trait("Category", TestCategories.Performance)]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "Testcontainers")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "914")]
[Trait("Skip", "CI")] // Issue #2603: Run separately in performance suite
public class BulkImportStressTests : IAsyncLifetime
{
    private IContainer? _postgresContainer;
    private MeepleAiDbContext? _dbContext;
    private BulkImportApiKeysCommandHandler? _handler;
    private readonly Action<string> _output;

    // Performance constants
    private const int StressTestUserCount = 1000;
    private const int BaselineUserCount = 500;
    private const int MaxExecutionTimeSeconds = 30;

    public BulkImportStressTests()
    {
        _output = Console.WriteLine;
    }

    public async ValueTask InitializeAsync()
    {
        _output("⚡ Initializing Performance Stress Test Infrastructure...");

        // Start PostgreSQL container with optimized settings
        _postgresContainer = new ContainerBuilder()
            .WithImage("pgvector/pgvector:pg16")  // Issue #3547: Use pgvector image for Vector column support
            .WithEnvironment("POSTGRES_USER", "postgres")
            .WithEnvironment("POSTGRES_PASSWORD", "postgres")
            .WithEnvironment("POSTGRES_DB", "stress_test")
            // Performance tuning for faster bulk inserts
            .WithEnvironment("POSTGRES_SHARED_BUFFERS", "256MB")
            .WithEnvironment("POSTGRES_MAX_CONNECTIONS", "200")
            .WithPortBinding(5432, true)
            .Build();

        await _postgresContainer.StartAsync(CancellationToken.None);

        // IMPORTANT: Wait for container stability (prevents intermittent failures)
        await Task.Delay(TimeSpan.FromSeconds(2));

        var containerPort = _postgresContainer.GetMappedPublicPort(5432);
        var connectionString = $"Host=localhost;Port={containerPort};Database=stress_test;Username=postgres;Password=postgres;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100;Connection Lifetime=0;";

        _output($"✅ PostgreSQL started at localhost:{containerPort} with pooling enabled");

        // Setup DI
        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton(TimeProvider.System);

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations
        await _dbContext.Database.MigrateAsync();

        _output("✅ Database migrations applied");

        // Create handler
        var apiKeyRepo = serviceProvider.GetRequiredService<IApiKeyRepository>();
        var userRepo = serviceProvider.GetRequiredService<IUserRepository>();
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        var logger = serviceProvider.GetRequiredService<ILogger<BulkImportApiKeysCommandHandler>>();

        _handler = new BulkImportApiKeysCommandHandler(apiKeyRepo, userRepo, unitOfWork, logger);
    }

    [Fact(Skip = "Performance test requires Testcontainers and is flaky in CI environments", Timeout = 60000)] // 60s timeout (includes container setup + test execution)
    public async Task BulkImport_1000Users_CompletesUnder30Seconds()
    {
        // Arrange
        _output("⚡ STRESS TEST: Importing 1000 users with API keys...");

        var userIds = await SeedTestUsersAsync(StressTestUserCount);
        var csv = GenerateBulkImportCsv(userIds);

        var command = new BulkImportApiKeysCommand(
            CsvContent: csv,
            RequesterId: Guid.NewGuid()
        );

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _handler!.Handle(command, CancellationToken.None);
        sw.Stop();

        // Assert - Performance requirement: <30 seconds
        _output($"⏱️  Execution time: {sw.Elapsed.TotalSeconds:F2}s (target: <{MaxExecutionTimeSeconds}s)");

        sw.Elapsed.TotalSeconds.Should().BeLessThan(MaxExecutionTimeSeconds,
            $"Bulk import of {StressTestUserCount} users took {sw.Elapsed.TotalSeconds:F2}s, exceeding {MaxExecutionTimeSeconds}s limit");

        // Assert - All keys created successfully
        result.TotalRequested.Should().Be(StressTestUserCount);
        result.SuccessCount.Should().Be(StressTestUserCount);
        result.FailedCount.Should().Be(0);
        result.Errors.Should().BeEmpty();

        // Assert - All plaintext keys returned
        result.Data.Count.Should().Be(StressTestUserCount);
        result.Data.Should().OnlyContain(dto => !string.IsNullOrWhiteSpace(dto.PlaintextKey));

        _output($"✅ Successfully imported {result.SuccessCount}/{result.TotalRequested} API keys");
        _output($"📊 Performance: {StressTestUserCount / sw.Elapsed.TotalSeconds:F0} keys/second");
    }

    [Fact(Skip = "Performance test requires Testcontainers and is flaky in CI environments", Timeout = 30000)] // 30s timeout
    public async Task BulkImport_500Users_Baseline()
    {
        // Arrange - Baseline test for comparison
        _output($"📊 BASELINE TEST: Importing {BaselineUserCount} users...");

        var userIds = await SeedTestUsersAsync(BaselineUserCount);
        var csv = GenerateBulkImportCsv(userIds);

        var command = new BulkImportApiKeysCommand(
            CsvContent: csv,
            RequesterId: Guid.NewGuid()
        );

        // Act
        var sw = Stopwatch.StartNew();
        var result = await _handler!.Handle(command, CancellationToken.None);
        sw.Stop();

        // Assert
        _output($"⏱️  Baseline execution time: {sw.Elapsed.TotalSeconds:F2}s");
        _output($"📊 Baseline performance: {BaselineUserCount / sw.Elapsed.TotalSeconds:F0} keys/second");

        result.SuccessCount.Should().Be(BaselineUserCount);
        result.Errors.Should().BeEmpty();

        // Baseline should be faster than stress test (linear scaling check)
        sw.Elapsed.TotalSeconds.Should().BeLessThan(MaxExecutionTimeSeconds / 2.0,
            "Baseline should complete in less than half the stress test time limit");
    }

    [Fact(Skip = "Performance test requires Testcontainers and is flaky in CI environments", Timeout = 60000)] // 60s timeout
    public async Task BulkImport_1000Users_AllKeysValid()
    {
        // Arrange
        _output("🔐 VALIDATION TEST: Verifying all 1000 keys are valid and unique...");

        var userIds = await SeedTestUsersAsync(StressTestUserCount);
        var csv = GenerateBulkImportCsv(userIds);

        var command = new BulkImportApiKeysCommand(
            CsvContent: csv,
            RequesterId: Guid.NewGuid()
        );

        // Act
        var result = await _handler!.Handle(command, CancellationToken.None);

        // Assert - All keys are unique
        var plaintextKeys = result.Data.Select(d => d.PlaintextKey).ToList();
        var uniqueKeys = plaintextKeys.Distinct().ToList();

        uniqueKeys.Count.Should().Be(StressTestUserCount);
        _output($"✅ All {StressTestUserCount} keys are unique");

        // Assert - All keys have valid format (Base64, minimum length)
        plaintextKeys.Should().OnlyContain(key => key.Length >= 40, "Key should be at least 40 characters");
        plaintextKeys.Should().AllSatisfy(key =>
        {
            key.Should().MatchRegex(@"^[A-Za-z0-9+/=]+$"); // Valid Base64
        });

        _output($"✅ All {StressTestUserCount} keys have valid format");

        // Assert - All keys are properly persisted in database
        var storedKeys = await _dbContext!.ApiKeys
            .Where(k => userIds.Contains(k.UserId))
            .ToListAsync();

        storedKeys.Count.Should().Be(StressTestUserCount);
        _output($"✅ All {StressTestUserCount} keys persisted to database");

        // Assert - No plaintext keys in database (security check)
        storedKeys.Should().AllSatisfy(key =>
        {
            key.KeyHash.Should().NotBeNull();
            key.KeyHash.Should().NotBe(string.Empty);
            // Verify hash is different from any plaintext key (no storage of plaintext)
            plaintextKeys.Should().NotContain(plaintextKey => plaintextKey == key.KeyHash);
        });

        _output($"✅ Security verified: No plaintext keys in database");
    }

    private async Task<List<Guid>> SeedTestUsersAsync(int count)
    {
        _output($"📦 Seeding {count} test users...");

        var userIds = new List<Guid>();
        var userEntities = new List<UserEntity>();

        // Create password hash once for efficiency
        var passwordHashValue = Api.BoundedContexts.Authentication.Domain.ValueObjects.PasswordHash.Create("TestPassword123!").Value;

        for (int i = 0; i < count; i++)
        {
            var userId = Guid.NewGuid();
            userIds.Add(userId);

            userEntities.Add(new UserEntity
            {
                Id = userId,
                Email = $"stresstest{i}_{Guid.NewGuid()}@example.com",
                DisplayName = $"Stress Test User {i}",
                PasswordHash = passwordHashValue,
                Role = "user",
                CreatedAt = DateTime.UtcNow,
                IsTwoFactorEnabled = false,
                TotpSecretEncrypted = null,
                TwoFactorEnabledAt = null
            });
        }

        await _dbContext!.Users.AddRangeAsync(userEntities);
        await _dbContext.SaveChangesAsync();

        _output($"✅ Seeded {count} users to database");

        return userIds;
    }

    private string GenerateBulkImportCsv(List<Guid> userIds)
    {
        var csv = new StringBuilder();
        csv.AppendLine("userId,keyName,scopes,expiresAt,metadata");

        var expiresAt = DateTime.UtcNow.AddYears(1).ToString("yyyy-MM-dd HH:mm:ss");

        foreach (var userId in userIds)
        {
            csv.AppendLine($"{userId},Bulk Import Key,read:games,{expiresAt},null");
        }

        return csv.ToString();
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext != null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }

        _output("🧹 Stress test infrastructure disposed");
    }
}
