using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Queries.Integration;

[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
public sealed class GetUserActivityQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _keyPrefix = null!;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;
    private IAuditLogRepository _auditLogRepository = null!;
    private GetUserActivityQueryHandler _handler = null!;
    private IConnectionMultiplexer _redis = null!;

    public GetUserActivityQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _keyPrefix = $"test:user_activity:{Guid.NewGuid():N}:";
        _dbName = $"test_user_activity_{Guid.NewGuid():N}";

        // Create isolated database and get connection string
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);

        // Setup DbContext with mocks
        var optionsBuilder = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector()) // Issue #3547
            .EnableSensitiveDataLogging()
            .EnableDetailedErrors()
            .EnableThreadSafetyChecks(false); // Allow concurrent access if needed

        var mockMediator = TestDbContextFactory.CreateMockMediator();
        var mockEventCollector = TestDbContextFactory.CreateMockEventCollector();

        _dbContext = new MeepleAiDbContext(optionsBuilder.Options, mockMediator.Object, mockEventCollector.Object);
        await _dbContext.Database.MigrateAsync();

        _redis = await ConnectionMultiplexer.ConnectAsync(_fixture.RedisConnectionString);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        var serviceProvider = services.BuildServiceProvider();

        // Setup repository with real DB and event collector
        _auditLogRepository = new AuditLogRepository(_dbContext, mockEventCollector.Object);

        var logger = serviceProvider.GetRequiredService<ILogger<GetUserActivityQueryHandler>>();
        _handler = new GetUserActivityQueryHandler(_auditLogRepository, logger);
    }

    public async ValueTask DisposeAsync()
    {
        await _fixture.FlushRedisByPrefixAsync(_keyPrefix + "*");
        await _fixture.DropIsolatedDatabaseAsync(_dbName);
        await _redis.CloseAsync();
        _redis.Dispose();
        await _dbContext.DisposeAsync();
    }

    [Fact]
    public async Task Handle_WithValidUserId_ReturnsUserActivity()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId);

        var query = new GetUserActivityQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Activities);
        Assert.True(result.Activities.Count > 0, "Should have at least one activity");
        Assert.True(result.TotalCount > 0);
    }

    [Fact]
    public async Task Handle_WithActionFilter_ReturnsFilteredResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId, activityCount: 20);

        var query = new GetUserActivityQuery(
            userId,
            ActionFilter: "Login,Logout"
        );

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Activities, activity =>
            Assert.True(activity.Action == "Login" || activity.Action == "Logout")
        );
    }

    [Fact]
    public async Task Handle_WithResourceFilter_ReturnsFilteredResults()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId);

        var query = new GetUserActivityQuery(
            userId,
            ResourceFilter: "User"
        );

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Activities, activity =>
            Assert.Equal("User", activity.Resource)
        );
    }

    [Fact]
    public async Task Handle_WithNonExistentUser_ReturnsEmptyResult()
    {
        // Arrange
        var nonExistentUserId = Guid.NewGuid();
        var query = new GetUserActivityQuery(nonExistentUserId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Empty(result.Activities);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Handle_WithLimit_RespectsLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId, activityCount: 50);

        var query = new GetUserActivityQuery(userId, Limit: 10);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Activities.Count <= 10);
        Assert.Equal(50, result.TotalCount); // Total should still be 50
    }

    [Fact]
    public async Task Handle_WithDateRangeFilter_ReturnsOnlyActivitiesInRange()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId);

        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;
        var query = new GetUserActivityQuery(userId, StartDate: fromDate, EndDate: toDate);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Activities, activity =>
        {
            Assert.True(activity.CreatedAt >= fromDate);
            Assert.True(activity.CreatedAt <= toDate);
        });
    }

    [Fact]
    public async Task Handle_WithMultipleFilters_AppliesAllFilters()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId, activityCount: 30);

        var fromDate = DateTime.UtcNow.AddDays(-7);
        var toDate = DateTime.UtcNow;

        var query = new GetUserActivityQuery(
            userId,
            ActionFilter: "Login",
            ResourceFilter: "User",
            StartDate: fromDate,
            EndDate: toDate,
            Limit: 20
        );

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Activities, activity =>
        {
            Assert.Equal("Login", activity.Action);
            Assert.Equal("User", activity.Resource);
            Assert.True(activity.CreatedAt >= fromDate);
            Assert.True(activity.CreatedAt <= toDate);
        });
    }

    [Fact]
    public async Task Handle_WithMaxLimit_EnforcesMaxLimit()
    {
        // Arrange
        var userId = Guid.NewGuid();
        await SeedTestUserActivityAsync(userId, activityCount: 600);

        var query = new GetUserActivityQuery(userId, Limit: 999); // Exceeds max

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Activities.Count <= 500, "Should respect max limit of 500");
    }

    [Fact]
    public async Task Handle_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var query = new GetUserActivityQuery(userId);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _handler.Handle(query, cts.Token)
        );
    }

    [Fact]
    public async Task Handle_WithDatabaseQuery_RetrievesRealData()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var expectedAction = "PasswordChange";
        var expectedResource = "User";

        // Seed specific audit log
        var auditLog = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = expectedAction,
            Resource = expectedResource,
            ResourceId = userId.ToString(),
            Result = "Success",
            Details = "Password changed successfully",
            IpAddress = "127.0.0.1",
            CreatedAt = DateTime.UtcNow
        };

        await _dbContext.AuditLogs.AddAsync(auditLog);
        await _dbContext.SaveChangesAsync();

        var query = new GetUserActivityQuery(userId);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Single(result.Activities);
        var activity = result.Activities.First();
        Assert.Equal(expectedAction, activity.Action);
        Assert.Equal(expectedResource, activity.Resource);
    }

    private async Task SeedTestUserActivityAsync(Guid userId, int activityCount = 10)
    {
        var activityTypes = new[] { "Login", "Logout", "PasswordChange", "ProfileUpdate" };
        var resources = new[] { "User", "Session", "Profile" };
#pragma warning disable CA5394 // Random is acceptable for test data generation, not cryptographic purposes
        var random = new Random(userId.GetHashCode());

        for (int i = 0; i < activityCount; i++)
        {
            var auditLog = new AuditLogEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Action = activityTypes[random.Next(activityTypes.Length)],
                Resource = resources[random.Next(resources.Length)],
                ResourceId = Guid.NewGuid().ToString(),
                Result = "Success",
                Details = $"Test activity {i}",
                IpAddress = "127.0.0.1",
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 30))
            };

            await _dbContext.AuditLogs.AddAsync(auditLog);
        }
#pragma warning restore CA5394

        await _dbContext.SaveChangesAsync();
    }
}
