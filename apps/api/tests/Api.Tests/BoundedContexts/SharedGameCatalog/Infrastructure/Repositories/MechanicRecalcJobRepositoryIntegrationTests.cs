using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Integration tests for <see cref="MechanicRecalcJobRepository"/> (ADR-051 Sprint 2 / Task 7)
/// against a real PostgreSQL database (Testcontainers). The headline test verifies that the
/// <c>SELECT ... FOR UPDATE SKIP LOCKED</c> claim primitive yields a distinct job to each
/// concurrent caller — the core safety property of the worker-pool design.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicRecalcJobRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private MechanicRecalcJobRepository _repository = null!;
    private Guid _testUserId;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public MechanicRecalcJobRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_recalcjob_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        // Seed a user row so the FK on triggered_by_user_id is satisfiable.
        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"recalc-test-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _repository = new MechanicRecalcJobRepository(_dbContext, CreateEventCollector());
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // Happy path: AddAsync + GetByIdAsync round-trip
    // ============================================================

    [Fact]
    public async Task AddAsync_AndReload_PersistsAggregateState()
    {
        // Arrange
        var job = MechanicRecalcJob.Enqueue(_testUserId);

        // Act
        await _repository.AddAsync(job);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(job.Id);

        // Assert
        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(job.Id);
        reloaded.Status.Should().Be(RecalcJobStatus.Pending);
        reloaded.TriggeredByUserId.Should().Be(_testUserId);
        reloaded.CreatedAt.Should().BeCloseTo(job.CreatedAt, TimeSpan.FromSeconds(1));
        reloaded.StartedAt.Should().BeNull();
        reloaded.CancellationRequested.Should().BeFalse();
    }

    // ============================================================
    // Headline test: concurrent claims yield distinct jobs (SKIP LOCKED)
    // ============================================================

    [Fact]
    public async Task ClaimNextPendingAsync_ConcurrentCallers_EachGetsDistinctJob()
    {
        // Arrange — seed 2 pending jobs so two parallel claimers can each grab one.
        var jobA = MechanicRecalcJob.Enqueue(_testUserId);
        var jobB = MechanicRecalcJob.Enqueue(_testUserId);
        await _repository.AddAsync(jobA);
        await _repository.AddAsync(jobB);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Each parallel claimer needs its own DbContext (DbContext is not thread-safe
        // and an open transaction is bound to the underlying connection).
        await using var ctx1 = _fixture.CreateDbContext(_connectionString);
        await using var ctx2 = _fixture.CreateDbContext(_connectionString);
        var repo1 = new MechanicRecalcJobRepository(ctx1, CreateEventCollector());
        var repo2 = new MechanicRecalcJobRepository(ctx2, CreateEventCollector());

        // Act — fire both claims concurrently.
        var task1 = repo1.ClaimNextPendingAsync();
        var task2 = repo2.ClaimNextPendingAsync();
        var results = await Task.WhenAll(task1, task2);

        // Assert — both succeeded, ids differ, both Running with StartedAt stamped.
        results[0].Should().NotBeNull("claim 1 must succeed when 2 pending rows exist");
        results[1].Should().NotBeNull("claim 2 must succeed when 2 pending rows exist");
        results[0]!.Id.Should().NotBe(results[1]!.Id, "SKIP LOCKED must hand out distinct rows");

        var seenIds = new HashSet<Guid> { results[0]!.Id, results[1]!.Id };
        seenIds.Should().BeEquivalentTo(new HashSet<Guid> { jobA.Id, jobB.Id });

        foreach (var claimed in results)
        {
            claimed!.Status.Should().Be(RecalcJobStatus.Running);
            claimed.StartedAt.Should().NotBeNull();
        }

        // Cross-check by re-reading from a fresh DbContext: both rows are now Running in the DB.
        await using var verifyCtx = _fixture.CreateDbContext(_connectionString);
        var rows = await verifyCtx.MechanicRecalcJobs
            .AsNoTracking()
            .OrderBy(j => j.CreatedAt)
            .ToListAsync();
        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(r =>
        {
            r.Status.Should().Be(RecalcJobStatus.Running);
            r.StartedAt.Should().NotBeNull();
        });
    }

    // ============================================================
    // Edge case: no pending jobs → null
    // ============================================================

    [Fact]
    public async Task ClaimNextPendingAsync_WhenNoPendingJobs_ReturnsNull()
    {
        // Arrange — table empty.
        // Act
        var claimed = await _repository.ClaimNextPendingAsync();

        // Assert
        claimed.Should().BeNull();
    }

    // ============================================================
    // ListRecentAsync: returns top-N descending by CreatedAt
    // ============================================================

    [Fact]
    public async Task ListRecentAsync_ReturnsRequestedCountInDescendingOrder()
    {
        // Arrange — seed 3 jobs with deliberately spaced CreatedAt to make the order
        // observable. We mutate via Reconstitute (used by the repo's mapper anyway) to
        // backdate CreatedAt without invoking lifecycle transitions.
        var older = MechanicRecalcJob.Reconstitute(
            id: Guid.NewGuid(),
            status: RecalcJobStatus.Completed,
            triggeredByUserId: _testUserId,
            total: 0,
            processed: 0,
            failed: 0,
            skipped: 0,
            consecutiveFailures: 0,
            lastError: null,
            lastProcessedAnalysisId: null,
            cancellationRequested: false,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(-30),
            startedAt: null,
            completedAt: null,
            heartbeatAt: null);

        var middle = MechanicRecalcJob.Reconstitute(
            id: Guid.NewGuid(),
            status: RecalcJobStatus.Failed,
            triggeredByUserId: _testUserId,
            total: 0,
            processed: 0,
            failed: 0,
            skipped: 0,
            consecutiveFailures: 0,
            lastError: null,
            lastProcessedAnalysisId: null,
            cancellationRequested: false,
            createdAt: DateTimeOffset.UtcNow.AddMinutes(-15),
            startedAt: null,
            completedAt: null,
            heartbeatAt: null);

        var newest = MechanicRecalcJob.Enqueue(_testUserId);

        await _repository.AddAsync(older);
        await _repository.AddAsync(middle);
        await _repository.AddAsync(newest);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Act
        var top2 = await _repository.ListRecentAsync(limit: 2);

        // Assert: newest first, middle next, older excluded.
        top2.Should().HaveCount(2);
        top2[0].Id.Should().Be(newest.Id);
        top2[1].Id.Should().Be(middle.Id);
    }

    // ============================================================
    // Helpers
    // ============================================================

    private static IDomainEventCollector CreateEventCollector()
    {
        var mock = new Mock<IDomainEventCollector>();
        mock.Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return mock.Object;
    }
}
