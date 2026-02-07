using Api.BoundedContexts.Administration.Application.Queries.Resources;
using Api.Infrastructure;
using Api.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.ResourcesTests;

/// <summary>
/// Integration tests for database metrics query.
/// Issue #3695: Resources Monitoring - Database metrics
/// </summary>
[Collection("Sequential")]
[Trait("Category", "Integration")]
[Trait("BoundedContext", "Administration")]
[Trait("Epic", "3685")]
public class DatabaseMetricsQueryTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private ServiceProvider? _serviceProvider;

    public async ValueTask InitializeAsync()
    {
        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("test_db")
            .WithUsername("test_user")
            .WithPassword("test_pass")
            .Build();

        await _postgres.StartAsync().ConfigureAwait(false);

        var services = new ServiceCollection();
        services.AddDbContext<MeepleAiDbContext>(options =>
            options.UseNpgsql(_postgres.GetConnectionString()));

        _serviceProvider = services.BuildServiceProvider();

        // Initialize database schema
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.EnsureCreatedAsync().ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync().ConfigureAwait(false);
        }

        if (_postgres != null)
        {
            await _postgres.DisposeAsync().ConfigureAwait(false);
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
        var result = await handler.Handle(query, CancellationToken.None).ConfigureAwait(false);

        // Assert
        result.Should().NotBeNull();
        result.SizeBytes.Should().BeGreaterThan(0, "database size should be positive");
        result.SizeFormatted.Should().NotBeNullOrWhiteSpace();
        result.MaxConnections.Should().BeGreaterThan(0);
        result.ActiveConnections.Should().BeGreaterOrEqualTo(0);
        result.TransactionsCommitted.Should().BeGreaterOrEqualTo(0);
        result.TransactionsRolledBack.Should().BeGreaterOrEqualTo(0);
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
        var result = await handler.Handle(query, CancellationToken.None).ConfigureAwait(false);

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
        await act.Should().ThrowAsync<ArgumentNullException>()
            .ConfigureAwait(false);
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
