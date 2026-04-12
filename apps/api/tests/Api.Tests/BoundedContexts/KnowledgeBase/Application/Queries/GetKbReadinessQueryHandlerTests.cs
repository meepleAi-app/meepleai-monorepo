using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Integration tests for <see cref="GetKbReadinessQueryHandler"/> (Session Flow v2.1 — T3).
/// Uses an isolated PostgreSQL database (via <see cref="SharedTestcontainersFixture"/>)
/// and the T0 seeders to exercise real EF Core queries — no InMemory provider.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Feature", "SessionFlowV2.1")]
public sealed class GetKbReadinessQueryHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private string _isolatedDbConnectionString = string.Empty;
    private ServiceProvider? _serviceProvider;
    private MeepleAiDbContext? _dbContext;

    private static CancellationToken TestCancellationToken => TestContext.Current.CancellationToken;

    public GetKbReadinessQueryHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_kbreadiness_{Guid.NewGuid():N}";
        _isolatedDbConnectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(_isolatedDbConnectionString);
        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<MeepleAiDbContext>();

        // Apply migrations — retry a few times to tolerate transient Npgsql startup errors.
        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _dbContext.Database.MigrateAsync(TestCancellationToken);
                break;
            }
            catch (NpgsqlException) when (attempt < 2)
            {
                await Task.Delay(500, TestCancellationToken);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _dbContext?.Dispose();
        if (_serviceProvider is not null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_databaseName))
        {
            try
            {
                await _fixture.DropIsolatedDatabaseAsync(_databaseName);
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }

    [Fact]
    public async Task Handle_NoGameFound_ReturnsNoneAndNotReady()
    {
        // Arrange
        var handler = new GetKbReadinessQueryHandler(_dbContext!);
        var query = new GetKbReadinessQuery(Guid.NewGuid());

        // Act
        var result = await handler.Handle(query, TestCancellationToken);

        // Assert
        result.Should().NotBeNull();
        result.IsReady.Should().BeFalse();
        result.State.Should().Be("None");
        result.ReadyPdfCount.Should().Be(0);
        result.FailedPdfCount.Should().Be(0);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReadyPdfWithCompletedVector_ReturnsReady()
    {
        // Arrange
        var gameId = await _fixture.SeedGameWithIndexedPdfAsync(_dbContext!, vectorCount: 5);
        var handler = new GetKbReadinessQueryHandler(_dbContext!);

        // Act
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), TestCancellationToken);

        // Assert
        result.IsReady.Should().BeTrue();
        result.State.Should().Be("Ready");
        result.ReadyPdfCount.Should().Be(1);
        result.FailedPdfCount.Should().Be(0);
        result.Warnings.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReadyPdfButMissingVector_ReturnsVectorPendingAndNotReady()
    {
        // Arrange — seeder with vectorCount:0 omits the VectorDocument row.
        var gameId = await _fixture.SeedGameWithIndexedPdfAsync(_dbContext!, vectorCount: 0);
        var handler = new GetKbReadinessQueryHandler(_dbContext!);

        // Act
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), TestCancellationToken);

        // Assert
        result.IsReady.Should().BeFalse();
        result.State.Should().Be("VectorPending");
        result.ReadyPdfCount.Should().Be(1);
        result.FailedPdfCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MixedReadyAndFailedPdfs_ReturnsPartiallyReadyWithWarning()
    {
        // Arrange — 1 Ready (with a completed vector) + 1 Failed.
        var gameId = await _fixture.SeedGameWithMixedPdfsAsync(
            _dbContext!,
            indexedCount: 1,
            failedCount: 1,
            vectorsPerIndexed: 3);

        var handler = new GetKbReadinessQueryHandler(_dbContext!);

        // Act
        var result = await handler.Handle(new GetKbReadinessQuery(gameId), TestCancellationToken);

        // Assert
        result.IsReady.Should().BeTrue();
        result.State.Should().Be("PartiallyReady");
        result.ReadyPdfCount.Should().Be(1);
        result.FailedPdfCount.Should().Be(1);
        result.Warnings.Should().NotBeEmpty();
        result.Warnings.Should().ContainSingle(w => w.Contains("failed to index", StringComparison.OrdinalIgnoreCase));
    }
}
