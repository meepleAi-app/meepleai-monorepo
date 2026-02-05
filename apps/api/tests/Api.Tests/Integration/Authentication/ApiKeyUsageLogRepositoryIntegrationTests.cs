using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Authentication;

/// <summary>
/// Integration tests for ApiKeyUsageLogRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, usage log tracking, and query operations.
/// Issue #2307: Week 3 - Repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2307")]
public sealed class ApiKeyUsageLogRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IApiKeyUsageLogRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestUsageLogId1 = new("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUsageLogId2 = new("40000000-0000-0000-0000-000000000002");
    private static readonly Guid TestApiKeyId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId1 = new("10000000-0000-0000-0000-000000000001");

    public ApiKeyUsageLogRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_apiusagelog_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector()); // Issue #3547: Enable pgvector
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IApiKeyUsageLogRepository, ApiKeyUsageLogRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IApiKeyUsageLogRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema with retry (Issue #2005 pattern)
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(TestConstants.Timing.RetryDelay, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public async Task AddAsync_ValidUsageLog_ShouldPersistSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(TestUserId1, "test@example.com");
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);

        await SeedTestUserAsync(user);
        await SeedTestApiKeyAsync(apiKey);

        var usageLog = ApiKeyUsageLog.Create(
            id: TestUsageLogId1,
            keyId: TestApiKeyId1,
            endpoint: "/api/v1/games",
            ipAddress: "192.168.1.100",
            userAgent: "MeepleAI Client/1.0",
            httpMethod: "GET",
            statusCode: 200,
            responseTimeMs: 150,
            usedAt: DateTime.UtcNow
        );

        // Act
        await _repository!.AddAsync(usageLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var retrieved = await _dbContext!.ApiKeyUsageLogs
            .FirstOrDefaultAsync(x => x.Id == TestUsageLogId1, TestCancellationToken);

        retrieved.Should().NotBeNull();
        retrieved!.KeyId.Should().Be(TestApiKeyId1);
        retrieved.Endpoint.Should().Be("/api/v1/games");
        retrieved.IpAddress.Should().Be("192.168.1.100");
        retrieved.StatusCode.Should().Be(200);
        retrieved.ResponseTimeMs.Should().Be(150);
    }

    [Fact]
    public async Task GetByKeyIdAsync_WithExistingLogs_ShouldReturnOrderedByDescending()
    {
        // Arrange
        var user = CreateTestUser(TestUserId1, "test@example.com");
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);

        await SeedTestUserAsync(user);
        await SeedTestApiKeyAsync(apiKey);

        var log1 = ApiKeyUsageLog.Create(
            id: TestUsageLogId1,
            keyId: TestApiKeyId1,
            endpoint: "/api/v1/games",
            ipAddress: "192.168.1.1",
            userAgent: "Client/1.0",
            httpMethod: "GET",
            statusCode: 200,
            responseTimeMs: 100,
            usedAt: DateTime.UtcNow.AddHours(-2)
        );

        var log2 = ApiKeyUsageLog.Create(
            id: TestUsageLogId2,
            keyId: TestApiKeyId1,
            endpoint: "/api/v1/chat",
            ipAddress: "192.168.1.1",
            userAgent: "Client/1.0",
            httpMethod: "POST",
            statusCode: 201,
            responseTimeMs: 250,
            usedAt: DateTime.UtcNow.AddHours(-1)
        );

        await _repository!.AddAsync(log1, TestCancellationToken);
        await _repository.AddAsync(log2, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var logs = await _repository.GetByKeyIdAsync(TestApiKeyId1, cancellationToken: TestCancellationToken);

        // Assert
        logs.Should().HaveCount(2);
        logs[0].UsedAt.Should().BeAfter(logs[1].UsedAt); // Most recent first
        logs[0].Endpoint.Should().Be("/api/v1/chat");
        logs[1].Endpoint.Should().Be("/api/v1/games");
    }

    [Fact]
    public async Task GetByKeyIdAndDateRangeAsync_WithinRange_ShouldReturnFilteredLogs()
    {
        // Arrange
        var user = CreateTestUser(TestUserId1, "test@example.com");
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);

        await SeedTestUserAsync(user);
        await SeedTestApiKeyAsync(apiKey);

        var now = DateTime.UtcNow;
        var logInRange = ApiKeyUsageLog.Create(
            id: TestUsageLogId1,
            keyId: TestApiKeyId1,
            endpoint: "/api/v1/games",
            ipAddress: "192.168.1.1",
            userAgent: "Client/1.0",
            httpMethod: "GET",
            statusCode: 200,
            responseTimeMs: 100,
            usedAt: now.AddDays(-2)
        );

        var logOutOfRange = ApiKeyUsageLog.Create(
            id: TestUsageLogId2,
            keyId: TestApiKeyId1,
            endpoint: "/api/v1/chat",
            ipAddress: "192.168.1.1",
            userAgent: "Client/1.0",
            httpMethod: "POST",
            statusCode: 201,
            responseTimeMs: 250,
            usedAt: now.AddDays(-10)
        );

        await _repository!.AddAsync(logInRange, TestCancellationToken);
        await _repository.AddAsync(logOutOfRange, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var logs = await _repository.GetByKeyIdAndDateRangeAsync(
            TestApiKeyId1,
            now.AddDays(-7),
            now,
            TestCancellationToken
        );

        // Assert
        logs.Should().HaveCount(1);
        logs[0].Id.Should().Be(TestUsageLogId1);
        logs[0].Endpoint.Should().Be("/api/v1/games");
    }

    [Fact]
    public async Task GetUsageCountAsync_WithMultipleLogs_ShouldReturnCorrectCount()
    {
        // Arrange
        var user = CreateTestUser(TestUserId1, "test@example.com");
        var (apiKey, _) = CreateTestApiKey(TestApiKeyId1, TestUserId1);

        await SeedTestUserAsync(user);
        await SeedTestApiKeyAsync(apiKey);

        for (var i = 0; i < 5; i++)
        {
            var log = ApiKeyUsageLog.Create(
                id: Guid.NewGuid(),
                keyId: TestApiKeyId1,
                endpoint: $"/api/v1/endpoint{i}",
                ipAddress: "192.168.1.1",
                userAgent: "Client/1.0",
                httpMethod: "GET",
                statusCode: 200,
                responseTimeMs: 100 + i * 10,
                usedAt: DateTime.UtcNow.AddMinutes(-i)
            );
            await _repository!.AddAsync(log, TestCancellationToken);
        }

        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var count = await _repository!.GetUsageCountAsync(TestApiKeyId1, TestCancellationToken);

        // Assert
        count.Should().Be(5);
    }

    #region Helper Methods

    private static User CreateTestUser(
        Guid id,
        string email,
        string displayName = "Test User",
        Role? role = null,
        UserTier? tier = null)
    {
        return new User(
            id: id,
            email: new Email(email),
            displayName: displayName,
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: role ?? Role.User,
            tier: tier ?? UserTier.Free
        );
    }

    private static (ApiKey apiKey, string plaintextKey) CreateTestApiKey(
        Guid apiKeyId,
        Guid userId,
        string keyName = "Test Key")
    {
        return ApiKey.Create(
            id: apiKeyId,
            userId: userId,
            keyName: keyName,
            scopes: "read,write",
            expiresAt: null,
            metadata: null
        );
    }

    private async Task SeedTestUserAsync(User user)
    {
        var userRepo = _serviceProvider!.GetRequiredService<IUserRepository>();
        await userRepo.AddAsync(user, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
    }

    private async Task SeedTestApiKeyAsync(ApiKey apiKey)
    {
        var apiKeyRepo = _serviceProvider!.GetRequiredService<IApiKeyRepository>();
        await apiKeyRepo.AddAsync(apiKey, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);
    }

    #endregion
}
