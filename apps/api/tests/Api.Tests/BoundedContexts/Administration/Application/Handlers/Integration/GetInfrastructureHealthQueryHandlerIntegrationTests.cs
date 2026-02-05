using Api.BoundedContexts.Administration.Application.Handlers;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers.Integration;

[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
public sealed class GetInfrastructureHealthQueryHandlerIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _keyPrefix = null!;
    private string _dbName = null!;
    private MeepleAiDbContext _dbContext = null!;
    private Mock<IInfrastructureHealthService> _healthServiceMock = null!;
    private GetInfrastructureHealthQueryHandler _handler = null!;
    private IConnectionMultiplexer _redis = null!;

    public GetInfrastructureHealthQueryHandlerIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _keyPrefix = $"test:infra_health:{Guid.NewGuid():N}:";
        _dbName = $"test_infra_health_{Guid.NewGuid():N}";

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

        // Mock the health service for simplified integration testing
        _healthServiceMock = new Mock<IInfrastructureHealthService>();

        var logger = serviceProvider.GetRequiredService<ILogger<GetInfrastructureHealthQueryHandler>>();
        _handler = new GetInfrastructureHealthQueryHandler(_healthServiceMock.Object, logger);
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
    public async Task Handle_WithAllServices_ReturnsOverallHealthStatus()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            2,
            2,
            0,
            0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("PostgreSQL", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)),
            new("Redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(5))
        };

        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);
        _healthServiceMock.Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Services);
        Assert.Equal(2, result.Services.Count);
        Assert.Contains(result.Services, s => s.ServiceName == "PostgreSQL");
        Assert.Contains(result.Services, s => s.ServiceName == "Redis");
    }

    [Fact]
    public async Task Handle_WithSpecificService_ReturnsOnlyThatService()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery { ServiceName = "PostgreSQL" };

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            1,
            1,
            0,
            0,
            DateTime.UtcNow
        );

        var postgresHealth = new ServiceHealthStatus(
            "PostgreSQL",
            HealthState.Healthy,
            null,
            DateTime.UtcNow,
            TimeSpan.FromMilliseconds(10)
        );

        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);
        _healthServiceMock.Setup(s => s.GetServiceHealthAsync("PostgreSQL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(postgresHealth);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Services);
        Assert.Single(result.Services);
        Assert.Equal("PostgreSQL", result.Services.First().ServiceName);
    }

    [Fact]
    public async Task Handle_WithHealthyServices_ReturnsHealthyStatus()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            2,
            2,
            0,
            0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("PostgreSQL", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)),
            new("Redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(5))
        };

        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);
        _healthServiceMock.Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthState.Healthy, result.Overall.State);
        Assert.All(result.Services, service =>
        {
            Assert.Equal("Healthy", service.State);
            Assert.Null(service.ErrorMessage);
        });
    }

    [Fact]
    public async Task Handle_WithCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        // Configure mock to throw OperationCanceledException
        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        // Act & Assert
        await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _handler.Handle(query, cts.Token)
        );
    }

    [Fact]
    public async Task Handle_WithDegradedService_ReturnsDegradedStatus()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var overallHealth = new OverallHealthStatus(
            HealthState.Degraded,
            2,
            1,
            1,
            0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("PostgreSQL", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)),
            new("Redis", HealthState.Degraded, "High latency", DateTime.UtcNow, TimeSpan.FromMilliseconds(500))
        };

        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);
        _healthServiceMock.Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(HealthState.Degraded, result.Overall.State);
        Assert.Contains(result.Services, s => s.ServiceName == "Redis" && s.State == "Degraded");
    }

    [Fact]
    public async Task Handle_IncludesResponseTimeForAllServices()
    {
        // Arrange
        var query = new GetInfrastructureHealthQuery();

        var overallHealth = new OverallHealthStatus(
            HealthState.Healthy,
            2,
            2,
            0,
            0,
            DateTime.UtcNow
        );

        var services = new List<ServiceHealthStatus>
        {
            new("PostgreSQL", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(10)),
            new("Redis", HealthState.Healthy, null, DateTime.UtcNow, TimeSpan.FromMilliseconds(5))
        };

        _healthServiceMock.Setup(s => s.GetOverallHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(overallHealth);
        _healthServiceMock.Setup(s => s.GetAllServicesHealthAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(services);

        // Act
        var result = await _handler.Handle(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(result);
        Assert.All(result.Services, service =>
        {
            Assert.True(service.ResponseTimeMs >= 0);
            Assert.True(service.CheckedAt <= DateTime.UtcNow);
        });
    }
}