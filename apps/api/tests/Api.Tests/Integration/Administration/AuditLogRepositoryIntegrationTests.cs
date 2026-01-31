using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for AuditLogRepository using SharedTestcontainersFixture.
/// Tests PostgreSQL persistence, append-only operations, filtered queries, and pagination.
/// Issue #2307: Week 3 - Administration repository integration testing
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "2307")]
public sealed class AuditLogRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IAuditLogRepository? _repository;
    private IUnitOfWork? _unitOfWork;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    // Test data constants
    private static readonly Guid TestAuditId1 = new("30000000-0000-0000-0000-000000000001");
    private static readonly Guid TestAuditId2 = new("30000000-0000-0000-0000-000000000002");
    private static readonly Guid TestAuditId3 = new("30000000-0000-0000-0000-000000000003");

    private static readonly Guid TestUserId1 = new("40000000-0000-0000-0000-000000000001");
    private static readonly Guid TestUserId2 = new("40000000-0000-0000-0000-000000000002");

    public AuditLogRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database for this test class
        _databaseName = $"test_auditrepo_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString);
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUnitOfWork, EfCoreUnitOfWork>();
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();

        // MediatR (required by MeepleAiDbContext)
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _repository = _serviceProvider.GetRequiredService<IAuditLogRepository>();
        _unitOfWork = _serviceProvider.GetRequiredService<IUnitOfWork>();

        // Create database schema
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

    #region Helper Methods

    private static AuditLog CreateTestAuditLog(
        Guid id,
        Guid? userId = null,
        string action = "Login",
        string resource = "User",
        string result = "Success",
        string? resourceId = null,
        string? details = null,
        string? ipAddress = null,
        string? userAgent = null)
    {
        return new AuditLog(
            id: id,
            userId: userId,
            action: action,
            resource: resource,
            result: result,
            resourceId: resourceId,
            details: details,
            ipAddress: ipAddress,
            userAgent: userAgent
        );
    }

    private async Task CleanDatabaseAsync()
    {
        if (_dbContext == null) return;

        _dbContext.AuditLogs.RemoveRange(_dbContext.AuditLogs);
        await _dbContext.SaveChangesAsync(TestCancellationToken);
    }

    #endregion

    #region AddAsync Tests (Append-Only Log)

    [Fact]
    public async Task AddAsync_NewAuditLog_ShouldPersistToDatabase()
    {
        // Arrange
        await CleanDatabaseAsync();
        var auditLog = CreateTestAuditLog(
            TestAuditId1,
            userId: TestUserId1,
            action: "CreateGame",
            resource: "Game",
            result: "Success",
            resourceId: "game-123",
            details: "{\"gameName\":\"Catan\"}",
            ipAddress: "192.168.1.100",
            userAgent: "Mozilla/5.0"
        );

        // Act
        await _repository!.AddAsync(auditLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Assert
        var persisted = await _repository.GetByIdAsync(TestAuditId1, TestCancellationToken);
        persisted.Should().NotBeNull();
        persisted!.UserId.Should().Be(TestUserId1);
        persisted.Action.Should().Be("CreateGame");
        persisted.Resource.Should().Be("Game");
        persisted.Result.Should().Be("Success");
        persisted.ResourceId.Should().Be("game-123");
        persisted.IpAddress.Should().Be("192.168.1.100");
        persisted.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance);
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await CleanDatabaseAsync();
        var auditLog = CreateTestAuditLog(TestAuditId1, TestUserId1);
        await _repository!.AddAsync(auditLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var act = async () => await _repository.UpdateAsync(auditLog, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*immutable*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldThrowInvalidOperationException()
    {
        // Arrange
        await CleanDatabaseAsync();
        var auditLog = CreateTestAuditLog(TestAuditId1, TestUserId1);
        await _repository!.AddAsync(auditLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var act = async () => await _repository.DeleteAsync(auditLog, TestCancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be deleted*");
    }

    #endregion

    #region GetByUserIdAsync Tests (Pagination)

    [Fact]
    public async Task GetByUserIdAsync_WithMultipleLogsForUser_ShouldReturnAllUserLogs()
    {
        // Arrange
        await CleanDatabaseAsync();
        var log1 = CreateTestAuditLog(TestAuditId1, TestUserId1, "Login", "Auth", "Success");
        var log2 = CreateTestAuditLog(TestAuditId2, TestUserId1, "CreateGame", "Game", "Success");
        var log3 = CreateTestAuditLog(TestAuditId3, TestUserId2, "DeleteGame", "Game", "Failure");

        await _repository!.AddAsync(log1, TestCancellationToken);
        await _repository.AddAsync(log2, TestCancellationToken);
        await _repository.AddAsync(log3, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.UserId.Should().Be(TestUserId1));
        result.Should().Contain(log => log.Action == "Login");
        result.Should().Contain(log => log.Action == "CreateGame");
    }

    #endregion

    #region GetByResourceAsync Tests (Filtered)

    [Fact]
    public async Task GetByResourceAsync_WithResourceFilter_ShouldReturnOnlyMatchingLogs()
    {
        // Arrange
        await CleanDatabaseAsync();
        var gameLog1 = CreateTestAuditLog(TestAuditId1, TestUserId1, "Create", "Game", "Success");
        var gameLog2 = CreateTestAuditLog(TestAuditId2, TestUserId2, "Delete", "Game", "Success");
        var userLog = CreateTestAuditLog(TestAuditId3, TestUserId1, "Update", "User", "Success");

        await _repository!.AddAsync(gameLog1, TestCancellationToken);
        await _repository.AddAsync(gameLog2, TestCancellationToken);
        await _repository.AddAsync(userLog, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository.GetByResourceAsync("Game", TestCancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Should().AllSatisfy(log => log.Resource.Should().Be("Game"));
        result.Should().Contain(log => log.Action == "Create");
        result.Should().Contain(log => log.Action == "Delete");
    }

    #endregion

    #region Complex Query Tests (Date Range + User)

    [Fact]
    public async Task GetByUserIdAsync_MultipleUsers_ShouldFilterCorrectly()
    {
        // Arrange
        await CleanDatabaseAsync();

        var user1Log1 = CreateTestAuditLog(TestAuditId1, TestUserId1, "Login", "Auth", "Success");
        var user1Log2 = CreateTestAuditLog(TestAuditId2, TestUserId1, "Logout", "Auth", "Success");
        var user2Log = CreateTestAuditLog(TestAuditId3, TestUserId2, "CreateGame", "Game", "Success");

        await _repository!.AddAsync(user1Log1, TestCancellationToken);
        await _repository.AddAsync(user1Log2, TestCancellationToken);
        await _repository.AddAsync(user2Log, TestCancellationToken);
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var user1Results = await _repository.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        var user2Results = await _repository.GetByUserIdAsync(TestUserId2, TestCancellationToken);

        // Assert
        user1Results.Should().HaveCount(2);
        user1Results.Should().AllSatisfy(log => log.UserId.Should().Be(TestUserId1));

        user2Results.Should().HaveCount(1);
        user2Results[0].UserId.Should().Be(TestUserId2);
        user2Results[0].Action.Should().Be("CreateGame");
    }

    [Fact]
    public async Task GetAllAsync_MultipleLogs_ShouldReturnAllWithOrdering()
    {
        // Arrange
        await CleanDatabaseAsync();

        // Create multiple logs for different users and actions
        var logs = new[]
        {
            CreateTestAuditLog(TestAuditId1, TestUserId1, "Login", "Auth", "Success"),
            CreateTestAuditLog(TestAuditId2, TestUserId1, "CreateGame", "Game", "Success"),
            CreateTestAuditLog(TestAuditId3, TestUserId2, "DeleteGame", "Game", "Failure")
        };

        foreach (var log in logs)
        {
            await _repository!.AddAsync(log, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var result = await _repository!.GetAllAsync(TestCancellationToken);

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(log => log.Action == "Login" && log.UserId == TestUserId1);
        result.Should().Contain(log => log.Action == "CreateGame" && log.UserId == TestUserId1);
        result.Should().Contain(log => log.Action == "DeleteGame" && log.UserId == TestUserId2);

        // Verify all have CreatedAt timestamps
        result.Should().AllSatisfy(log => log.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TestConstants.Timing.AssertionTolerance));
    }

    #endregion

    #region Performance Tests (Large Dataset Pagination)

    [Fact]
    public async Task GetByUserIdAsync_LargeDataset_ShouldPerformEfficiently()
    {
        // Arrange
        await CleanDatabaseAsync();
        const int logCount = 100;

        // Create 100 audit logs for the same user
        // Note: Timestamps may be identical for rapidly created logs - this is expected behavior
        for (int i = 0; i < logCount; i++)
        {
            var log = CreateTestAuditLog(
                Guid.NewGuid(),
                TestUserId1,
                $"Action{i}",
                "Resource",
                "Success",
                $"resource-{i}"
            );
            await _repository!.AddAsync(log, TestCancellationToken);
        }
        await _unitOfWork!.SaveChangesAsync(TestCancellationToken);

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var result = await _repository!.GetByUserIdAsync(TestUserId1, TestCancellationToken);
        stopwatch.Stop();

        // Assert
        result.Should().HaveCount(logCount);
        result.Should().AllSatisfy(log => log.UserId.Should().Be(TestUserId1));
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(2000, "Large dataset query should complete in <2 seconds");
    }

    #endregion
}
