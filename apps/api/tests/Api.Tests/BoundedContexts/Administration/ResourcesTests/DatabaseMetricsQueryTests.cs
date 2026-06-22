using Api.BoundedContexts.Administration.Application.Queries.Resources;
using Api.Infrastructure;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for database metrics query.
/// Issue #3695: Resources Monitoring - Database metrics
/// </summary>
[Collection("SharedTestcontainers")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class DatabaseMetricsQueryTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private ServiceProvider? _serviceProvider;

    public DatabaseMetricsQueryTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        // Issue #1628 follow-up: use the shared Postgres container with an isolated database
        // instead of spinning up a dedicated container per test class.
        var dbName = $"test_dbmetrics_{Guid.NewGuid():N}";
        var connStr = await _fixture.CreateIsolatedDatabaseAsync(dbName).ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(connStr, o => o.UseVector()));

        // Mock dependencies required by MeepleAiDbContext
        services.AddScoped<IMediator>(_ => Mock.Of<IMediator>());
        services.AddScoped<Api.SharedKernel.Application.Services.IDomainEventCollector>(_ => Mock.Of<Api.SharedKernel.Application.Services.IDomainEventCollector>());

        _serviceProvider = services.BuildServiceProvider();

        // CreateIsolatedDatabaseAsync only runs CREATE DATABASE — schema is empty.
        // Apply EF migrations explicitly (pre-flight Q2).
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }
    }

    [Fact]
    public async Task Handle_ReturnsValidDatabaseMetrics()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new GetDatabaseMetricsQueryHandler(db);
        var query = new GetDatabaseMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.SizeBytes.Should().BeGreaterThan(0, "database size should be positive");
        result.SizeFormatted.Should().NotBeNullOrWhiteSpace();
        result.MaxConnections.Should().BeGreaterThan(0);
        result.ActiveConnections.Should().BeGreaterThanOrEqualTo(0);
        result.TransactionsCommitted.Should().BeGreaterThanOrEqualTo(0);
        result.TransactionsRolledBack.Should().BeGreaterThanOrEqualTo(0);
        result.MeasuredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Handle_FormatsBytesCorrectly()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new GetDatabaseMetricsQueryHandler(db);
        var query = new GetDatabaseMetricsQuery();

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SizeFormatted.Should().MatchRegex(@"^\d+\.?\d* (B|KB|MB|GB|TB)$",
            "formatted size should match byte format pattern");
    }

    [Fact]
    public async Task Handle_WithNullQuery_ThrowsArgumentNullException()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var handler = new GetDatabaseMetricsQueryHandler(db);

        // Act
        var act = () => handler.Handle(null!, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task Constructor_WithNullDb_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new GetDatabaseMetricsQueryHandler(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }
}
