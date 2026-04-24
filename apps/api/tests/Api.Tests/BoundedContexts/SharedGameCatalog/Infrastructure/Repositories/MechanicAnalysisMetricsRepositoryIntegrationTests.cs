using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="MechanicAnalysisMetricsRepository"/> (ADR-051 Sprint 1 / Task 15)
/// against a real PostgreSQL database (Testcontainers).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisMetricsRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicAnalysisMetricsRepository _repository = null!;
    private MechanicAnalysisRepository _analysisRepository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public MechanicAnalysisMetricsRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_metrics_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new MechanicAnalysisMetricsRepository(_dbContext, mockCollector.Object);
        _analysisRepository = new MechanicAnalysisRepository(_dbContext, mockCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Happy path: AddAsync + GetByAnalysisAsync round-trip
    // ============================================================

    [Fact]
    public async Task AddAsync_AndReload_PersistsAllFields()
    {
        // Arrange
        var sharedGameId = await SeedSharedGameAsync();
        var analysisId = await SeedMechanicAnalysisAsync(sharedGameId);

        var thresholds = CertificationThresholds.Default();
        var metrics = MechanicAnalysisMetrics.Create(
            analysisId: analysisId,
            sharedGameId: sharedGameId,
            coveragePct: 72.5m,
            pageAccuracyPct: 90m,
            bggMatchPct: 82.5m,
            thresholds: thresholds,
            goldenVersionHash: new string('a', 64),
            matchDetailsJson: "{\"hits\":3}");

        // Act
        await _repository.AddAsync(metrics);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var byAnalysis = await _repository.GetByAnalysisAsync(analysisId);

        // Assert
        byAnalysis.Should().NotBeNull();
        byAnalysis!.Id.Should().Be(metrics.Id);
        byAnalysis.SharedGameId.Should().Be(sharedGameId);
        byAnalysis.CoveragePct.Should().Be(72.5m);
        byAnalysis.PageAccuracyPct.Should().Be(90m);
        byAnalysis.BggMatchPct.Should().Be(82.5m);
        byAnalysis.CertificationStatus.Should().Be(CertificationStatus.Certified);
        byAnalysis.GoldenVersionHash.Should().HaveLength(64);
        byAnalysis.MatchDetailsJson.Should().Be("{\"hits\":3}");
    }

    // ============================================================
    // Edge case: GetDashboardAsync aggregates latest-per-game
    // ============================================================

    [Fact]
    public async Task GetDashboardAsync_ReturnsLatestSnapshotPerSharedGame()
    {
        // Arrange: two shared games, one with 2 snapshots (only latest should surface),
        // one with a single snapshot.
        var gameA = await SeedSharedGameAsync("Game A");
        var gameB = await SeedSharedGameAsync("Game B");

        var analysisA1 = await SeedMechanicAnalysisAsync(gameA);
        var analysisA2 = await SeedMechanicAnalysisAsync(gameA);
        var analysisB = await SeedMechanicAnalysisAsync(gameB);

        // Older snapshot for A — NotCertified (coverage below threshold).
        var olderA = BuildMetrics(analysisA1, gameA, coverage: 40m, page: 70m, bgg: 50m,
            computedAt: DateTimeOffset.UtcNow.AddDays(-2));
        // Newer snapshot for A — Certified.
        var newerA = BuildMetrics(analysisA2, gameA, coverage: 90m, page: 95m, bgg: 90m,
            computedAt: DateTimeOffset.UtcNow.AddDays(-1));
        // Single snapshot for B.
        var onlyB = BuildMetrics(analysisB, gameB, coverage: 80m, page: 88m, bgg: 85m,
            computedAt: DateTimeOffset.UtcNow.AddHours(-3));

        await _repository.AddAsync(olderA);
        await _repository.AddAsync(newerA);
        await _repository.AddAsync(onlyB);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var dashboard = await _repository.GetDashboardAsync();

        // Assert: exactly one row per shared game, and Game A's row is the LATEST one.
        dashboard.Should().HaveCount(2);
        var rowA = dashboard.Single(r => r.SharedGameId == gameA);
        var rowB = dashboard.Single(r => r.SharedGameId == gameB);

        rowA.Name.Should().Be("Game A");
        rowA.Status.Should().Be(CertificationStatus.Certified);
        rowA.OverallScore.Should().Be(newerA.OverallScore);
        rowA.LastComputedAt.Should().BeCloseTo(newerA.ComputedAt, TimeSpan.FromSeconds(1));

        rowB.Name.Should().Be("Game B");
        rowB.OverallScore.Should().Be(onlyB.OverallScore);
    }

    // ============================================================
    // GetTrendAsync: returns top-N descending by ComputedAt
    // ============================================================

    [Fact]
    public async Task GetTrendAsync_ReturnsTopNDescending()
    {
        // Arrange
        var gameId = await SeedSharedGameAsync();

        var a1 = await SeedMechanicAnalysisAsync(gameId);
        var a2 = await SeedMechanicAnalysisAsync(gameId);
        var a3 = await SeedMechanicAnalysisAsync(gameId);

        await _repository.AddAsync(BuildMetrics(a1, gameId, 60m, 80m, 70m, DateTimeOffset.UtcNow.AddDays(-3)));
        await _repository.AddAsync(BuildMetrics(a2, gameId, 70m, 85m, 75m, DateTimeOffset.UtcNow.AddDays(-2)));
        await _repository.AddAsync(BuildMetrics(a3, gameId, 80m, 90m, 85m, DateTimeOffset.UtcNow.AddDays(-1)));
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var top2 = await _repository.GetTrendAsync(gameId, take: 2);

        // Assert: newest first, then next newest.
        top2.Should().HaveCount(2);
        top2[0].ComputedAt.Should().BeAfter(top2[1].ComputedAt);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task<Guid> SeedSharedGameAsync(string title = "Metrics Test Game")
    {
        var sharedGameId = Guid.NewGuid();
        _dbContext.SharedGames.Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = title,
            Description = "Integration test game",
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            YearPublished = 2024,
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 45,
            MinAge = 8,
            Status = 1,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return sharedGameId;
    }

    private async Task<Guid> SeedMechanicAnalysisAsync(Guid sharedGameId)
    {
        var analysis = MechanicAnalysis.Create(
            sharedGameId: sharedGameId,
            pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1",
            createdBy: Guid.NewGuid(),
            createdAt: DateTime.UtcNow,
            modelUsed: "deepseek-chat",
            provider: "deepseek",
            costCapUsd: 1m);

        await _analysisRepository.AddAsync(analysis);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return analysis.Id;
    }

    private static MechanicAnalysisMetrics BuildMetrics(
        Guid analysisId,
        Guid sharedGameId,
        decimal coverage,
        decimal page,
        decimal bgg,
        DateTimeOffset computedAt)
    {
        var metrics = MechanicAnalysisMetrics.Create(
            analysisId: analysisId,
            sharedGameId: sharedGameId,
            coveragePct: coverage,
            pageAccuracyPct: page,
            bggMatchPct: bgg,
            thresholds: CertificationThresholds.Default(),
            goldenVersionHash: new string('b', 64),
            matchDetailsJson: "{}");

        // Override ComputedAt for time-ordering scenarios. Use Reconstitute to set the fixed timestamp.
        return MechanicAnalysisMetrics.Reconstitute(
            id: metrics.Id,
            mechanicAnalysisId: metrics.MechanicAnalysisId,
            sharedGameId: metrics.SharedGameId,
            coveragePct: metrics.CoveragePct,
            pageAccuracyPct: metrics.PageAccuracyPct,
            bggMatchPct: metrics.BggMatchPct,
            overallScore: metrics.OverallScore,
            certificationStatus: metrics.CertificationStatus,
            goldenVersionHash: metrics.GoldenVersionHash,
            thresholdsSnapshotJson: metrics.ThresholdsSnapshotJson,
            matchDetailsJson: metrics.MatchDetailsJson,
            computedAt: computedAt);
    }
}
