using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="CertificationThresholdsConfigRepository"/> (ADR-051 Sprint 1 / Task 15)
/// against a real PostgreSQL database (Testcontainers).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CertificationThresholdsConfigRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private CertificationThresholdsConfigRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public CertificationThresholdsConfigRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_thresholds_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new CertificationThresholdsConfigRepository(_dbContext, mockCollector.Object);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Happy path: GetAsync returns the seeded singleton row
    // ============================================================

    [Fact]
    public async Task GetAsync_ReturnsSeededSingleton_WithDefaultThresholds()
    {
        // Arrange — migration M2_0_MechanicGoldenAndValidation seeds Id=1 with Default() values.

        // Act
        var config = await _repository.GetAsync();

        // Assert
        config.Should().NotBeNull();
        config.Id.Should().Be(1);
        config.Thresholds.Should().BeEquivalentTo(CertificationThresholds.Default());
    }

    // ============================================================
    // UpdateAsync: round-trip preserves new thresholds + audit fields
    // ============================================================

    [Fact]
    public async Task UpdateAsync_PersistsNewThresholdsAndAuditFields()
    {
        // Arrange
        var loaded = await _repository.GetAsync();
        var actor = Guid.NewGuid();
        var newThresholds = CertificationThresholds.Create(
            minCoveragePct: 85m,
            maxPageTolerance: 5,
            minBggMatchPct: 90m,
            minOverallScore: 75m);

        loaded.Update(newThresholds, actor);

        // Act
        await _repository.UpdateAsync(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetAsync();

        // Assert
        reloaded.Thresholds.Should().BeEquivalentTo(newThresholds);
        reloaded.UpdatedByUserId.Should().Be(actor);
        reloaded.UpdatedAt.Should().BeCloseTo(loaded.UpdatedAt, TimeSpan.FromSeconds(1));
    }

    // ============================================================
    // Edge case: missing seed row surfaces as InvalidOperationException
    // (fail-fast diagnostic — no silent re-seeding mid-UoW).
    // ============================================================

    [Fact]
    public async Task GetAsync_MissingSeedRow_Throws()
    {
        // Arrange: delete the migration-seeded singleton row to simulate a broken deployment.
        var seeded = await _dbContext.CertificationThresholdsConfigs
            .FirstOrDefaultAsync(c => c.Id == 1);
        seeded.Should().NotBeNull("migration M2.0 must seed the singleton row");
        _dbContext.CertificationThresholdsConfigs.Remove(seeded!);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var act = async () => await _repository.GetAsync();

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*singleton row (Id=1) is missing*");
    }

    // ============================================================
    // Edge case: optimistic concurrency via xmin raises
    // DbUpdateConcurrencyException on concurrent writes.
    // ============================================================

    [Fact]
    public async Task ConcurrentUpdate_ThrowsDbUpdateConcurrencyException()
    {
        // Arrange: open two parallel contexts against the same row.
        await using var ctxA = _fixture.CreateDbContext(_connectionString);
        await using var ctxB = _fixture.CreateDbContext(_connectionString);

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        var repoA = new CertificationThresholdsConfigRepository(ctxA, mockCollector.Object);
        var repoB = new CertificationThresholdsConfigRepository(ctxB, mockCollector.Object);

        var copyA = await repoA.GetAsync();
        var copyB = await repoB.GetAsync();

        copyA.Update(CertificationThresholds.Create(75m, 8, 82m, 65m), Guid.NewGuid());
        copyB.Update(CertificationThresholds.Create(88m, 3, 92m, 78m), Guid.NewGuid());

        // Act: first write wins — xmin advances; second write sees stale xmin.
        await repoA.UpdateAsync(copyA);
        await ctxA.SaveChangesAsync();

        await repoB.UpdateAsync(copyB);
        var act = async () => await ctxB.SaveChangesAsync();

        // Assert
        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}
