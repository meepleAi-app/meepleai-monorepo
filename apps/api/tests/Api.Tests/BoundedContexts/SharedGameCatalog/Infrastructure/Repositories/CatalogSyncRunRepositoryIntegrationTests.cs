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
/// Integration tests for <see cref="CatalogSyncRunRepository"/> (#1861, F4-A6 BE)
/// against a real PostgreSQL database (Testcontainers).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSyncRunRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private CatalogSyncRunRepository _repository = null!;
    private Guid _testUserId;
    private string _databaseName = null!;
    private string _connectionString = null!;

    public CatalogSyncRunRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_catalogsyncrun_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"catalog-test-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _repository = new CatalogSyncRunRepository(_dbContext, CreateEventCollector());
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ============================================================
    // 1. AddAsync + GetByIdAsync round-trip
    // ============================================================

    [Fact]
    public async Task AddAsync_AndReload_PersistsAllProperties()
    {
        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "BGG full sync", _testUserId);

        await _repository.AddAsync(run);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(run.Id);

        reloaded.Should().NotBeNull();
        reloaded!.Id.Should().Be(run.Id);
        reloaded.Provider.Should().Be(CatalogSyncProvider.BggApi);
        reloaded.Status.Should().Be(CatalogSyncStatus.Queued);
        reloaded.Title.Should().Be("BGG full sync");
        reloaded.TriggeredByUserId.Should().Be(_testUserId);
        reloaded.CreatedAt.Should().BeCloseTo(run.CreatedAt, TimeSpan.FromSeconds(1));
        reloaded.StartedAt.Should().BeNull();
        reloaded.CompletedAt.Should().BeNull();
    }

    [Fact]
    public async Task AddAsync_CronTriggered_PersistsNullTriggeredByUser()
    {
        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "Cron sync", triggeredBy: null);

        await _repository.AddAsync(run);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var reloaded = await _repository.GetByIdAsync(run.Id);

        reloaded.Should().NotBeNull();
        reloaded!.TriggeredByUserId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_AfterLifecycle_PersistsTransitionAndCounters()
    {
        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "Lifecycle test", _testUserId);
        await _repository.AddAsync(run);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _repository.GetByIdAsync(run.Id);
        loaded!.MarkRunning();
        loaded.RecordItemsAdded(12);
        loaded.RecordItemsUpdated(847);
        loaded.RecordItemsFailed(0);
        loaded.Complete();

        await _repository.UpdateAsync(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var final = await _repository.GetByIdAsync(run.Id);
        final.Should().NotBeNull();
        final!.Status.Should().Be(CatalogSyncStatus.Success);
        final.ItemsAdded.Should().Be(12);
        final.ItemsUpdated.Should().Be(847);
        final.ItemsFailed.Should().Be(0);
        final.StartedAt.Should().NotBeNull();
        final.CompletedAt.Should().NotBeNull();
    }

    // ============================================================
    // 2. GetCurrentRunningAsync
    // ============================================================

    [Fact]
    public async Task GetCurrentRunningAsync_NoRunningRun_ReturnsNull()
    {
        var queued = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "queued", null);
        await _repository.AddAsync(queued);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var running = await _repository.GetCurrentRunningAsync();

        running.Should().BeNull();
    }

    [Fact]
    public async Task GetCurrentRunningAsync_OneRunningExists_ReturnsIt()
    {
        var run1 = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "completed", null);
        run1.MarkRunning();
        run1.Complete();
        await _repository.AddAsync(run1);

        var run2 = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "currently running", null);
        run2.MarkRunning();
        await _repository.AddAsync(run2);

        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var running = await _repository.GetCurrentRunningAsync();

        running.Should().NotBeNull();
        running!.Id.Should().Be(run2.Id);
        running.Status.Should().Be(CatalogSyncStatus.Running);
    }

    // ============================================================
    // 3. GetLatestCompletedAsync
    // ============================================================

    [Fact]
    public async Task GetLatestCompletedAsync_ReturnsMostRecentTerminalRun()
    {
        // 3 terminal runs created in sequence; we expect the last one back
        var oldest = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "oldest", null);
        oldest.MarkRunning();
        oldest.Complete();
        await _repository.AddAsync(oldest);
        await _dbContext.SaveChangesAsync();

        await Task.Delay(10);
        var middle = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "middle", null);
        middle.MarkRunning();
        middle.Fail("ERR", "transient");
        await _repository.AddAsync(middle);
        await _dbContext.SaveChangesAsync();

        await Task.Delay(10);
        var newest = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "newest", null);
        newest.MarkRunning();
        newest.Complete();
        await _repository.AddAsync(newest);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var latest = await _repository.GetLatestCompletedAsync();

        latest.Should().NotBeNull();
        latest!.Id.Should().Be(newest.Id);
    }

    [Fact]
    public async Task GetLatestCompletedAsync_OnlyRunningInDb_ReturnsNull()
    {
        var running = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "running", null);
        running.MarkRunning();
        await _repository.AddAsync(running);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var latest = await _repository.GetLatestCompletedAsync();

        latest.Should().BeNull();
    }

    // ============================================================
    // 4. GetPagedAsync
    // ============================================================

    [Fact]
    public async Task GetPagedAsync_30Runs_Page1Size12_Returns12_TotalAccurate()
    {
        // Seed 30 runs with slightly staggered CreatedAt
        var allRuns = new List<CatalogSyncRun>();
        for (var i = 0; i < 30; i++)
        {
            var r = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, $"run-{i:D2}", null);
            r.MarkRunning();
            r.Complete();
            await _repository.AddAsync(r);
            await _dbContext.SaveChangesAsync();
            allRuns.Add(r);
            await Task.Delay(5);
        }
        _dbContext.ChangeTracker.Clear();

        var (items, total) = await _repository.GetPagedAsync(page: 1, pageSize: 12);

        total.Should().Be(30);
        items.Should().HaveCount(12);
        // Most recent first → run-29 should be at index 0
        items[0].Title.Should().Be("run-29");
        items[11].Title.Should().Be("run-18");
    }

    [Fact]
    public async Task GetPagedAsync_PaginationConsistency_NoOverlapBetweenPages()
    {
        for (var i = 0; i < 24; i++)
        {
            var r = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, $"r-{i:D2}", null);
            r.MarkRunning();
            r.Complete();
            await _repository.AddAsync(r);
            await _dbContext.SaveChangesAsync();
            await Task.Delay(5);
        }
        _dbContext.ChangeTracker.Clear();

        var (page1, _) = await _repository.GetPagedAsync(page: 1, pageSize: 10);
        var (page2, _) = await _repository.GetPagedAsync(page: 2, pageSize: 10);
        var (page3, total) = await _repository.GetPagedAsync(page: 3, pageSize: 10);

        page1.Should().HaveCount(10);
        page2.Should().HaveCount(10);
        page3.Should().HaveCount(4);
        total.Should().Be(24);

        var ids = page1.Concat(page2).Concat(page3).Select(r => r.Id).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task GetPagedAsync_EmptyDb_ReturnsEmptyAndZeroTotal()
    {
        var (items, total) = await _repository.GetPagedAsync(page: 1, pageSize: 12);

        items.Should().BeEmpty();
        total.Should().Be(0);
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
