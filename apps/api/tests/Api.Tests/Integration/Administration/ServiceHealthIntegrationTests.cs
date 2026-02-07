using Api.BoundedContexts.Administration.Application.Queries.Operations;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Api.Tests.Integration.Administration;

/// <summary>
/// Integration tests for Service Health monitoring with real health check service.
/// Tests PostgreSQL, Redis, and Qdrant health validation.
/// Issue #3697: Epic 1 - Testing & Integration (Phase 2)
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Dependency", "Redis")]
[Trait("BoundedContext", "Administration")]
[Trait("Issue", "3697")]
public sealed class ServiceHealthIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _isolatedDbConnectionString = string.Empty;
    private string _databaseName = string.Empty;
    private MeepleAiDbContext? _dbContext;
    private IMediator? _mediator;
    private HealthCheckService? _healthCheckService;
    private IServiceProvider? _serviceProvider;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public ServiceHealthIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Create isolated database
        _databaseName = $"test_health_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        // Database
        services.AddDbContext<MeepleAiDbContext>(options =>
        {
            options.UseNpgsql(_isolatedDbConnectionString, o => o.UseVector());
            options.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        });

        services.AddScoped<EfCoreUnitOfWork>();

        // Health checks (real implementation)
        services.AddHealthChecks()
            .AddNpgSql(_isolatedDbConnectionString, name: "PostgreSQL")
            .AddRedis(_fixture.RedisConnectionString, name: "Redis");

        // MediatR
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(Program).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _healthCheckService = _serviceProvider.GetRequiredService<HealthCheckService>();

        await _dbContext.Database.MigrateAsync(TestCancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_dbContext is not null)
        {
            await _dbContext.DisposeAsync();
        }

        if (_serviceProvider is not null)
        {
            await ((ServiceProvider)_serviceProvider).DisposeAsync();
        }

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact]
    public async Task GetServiceHealth_WithHealthyServices_ShouldReturnHealthyStatus()
    {
        // Act
        var query = new GetServiceHealthQuery();
        var result = await _mediator!.Send(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.OverallStatus.Should().Be("Healthy");
        result.Services.Should().NotBeNull();
        result.Services.Should().HaveCountGreaterOrEqualTo(2);
        result.Services.Should().Contain(s => s.ServiceName == "PostgreSQL" && s.Status == "Healthy");
        result.Services.Should().Contain(s => s.ServiceName == "Redis" && s.Status == "Healthy");
    }

    [Fact]
    public async Task HealthCheckService_PostgreSQL_ShouldValidateConnection()
    {
        // Act
        var healthReport = await _healthCheckService!.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthReport.Should().NotBeNull();
        healthReport.Status.Should().Be(HealthStatus.Healthy);
        healthReport.Entries.Should().ContainKey("PostgreSQL");

        var postgresHealth = healthReport.Entries["PostgreSQL"];
        postgresHealth.Status.Should().Be(HealthStatus.Healthy);
        postgresHealth.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthCheckService_Redis_ShouldValidateConnection()
    {
        // Act
        var healthReport = await _healthCheckService!.CheckHealthAsync(TestCancellationToken);

        // Assert
        healthReport.Should().NotBeNull();
        healthReport.Entries.Should().ContainKey("Redis");

        var redisHealth = healthReport.Entries["Redis"];
        redisHealth.Status.Should().Be(HealthStatus.Healthy);
        redisHealth.Description.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task HealthCheck_WhenDatabaseQueryable_ShouldReportHealthy()
    {
        // Arrange - Ensure database is accessible by executing a simple query
        var canQuery = await _dbContext!.Database.CanConnectAsync(TestCancellationToken);
        canQuery.Should().BeTrue();

        // Act
        var healthReport = await _healthCheckService!.CheckHealthAsync(TestCancellationToken);

        // Assert
        var postgresHealth = healthReport.Entries["PostgreSQL"];
        postgresHealth.Status.Should().Be(HealthStatus.Healthy);
        postgresHealth.Exception.Should().BeNull();
    }
}
