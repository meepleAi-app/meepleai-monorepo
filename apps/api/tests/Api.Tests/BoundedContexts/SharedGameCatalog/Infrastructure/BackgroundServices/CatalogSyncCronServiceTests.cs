using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Models;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure.BackgroundServices;

/// <summary>
/// Integration tests for <see cref="CatalogSyncCronService"/> (#1861 Phase 5).
/// Exercises the single-tick body (<see cref="CatalogSyncCronService.TryTriggerSyncAsync"/>)
/// against a real PostgreSQL database via <see cref="IntegrationWebApplicationFactory"/>.
/// The PeriodicTimer wrapper is trivial and out of scope.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class CatalogSyncCronServiceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private CatalogSyncCronService _cronService = null!;

    public CatalogSyncCronServiceTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"test_cron_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await dbContext.Database.MigrateAsync(TestContext.Current.CancellationToken);
        }

        var config = Options.Create(new CatalogSyncCronConfiguration
        {
            Enabled = true,
            IntervalHours = 6,
            InitialDelayMinutes = 0,
        });

        _cronService = new CatalogSyncCronService(
            _factory.Services.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<CatalogSyncCronService>.Instance,
            config);
    }

    public async ValueTask DisposeAsync()
    {
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    // ============================================================
    // Tick body
    // ============================================================

    [Fact]
    public async Task TryTriggerSync_NoRunningRun_CreatesNewQueuedRun()
    {
        await _cronService.TryTriggerSyncAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
        var (items, total) = await repo.GetPagedAsync(1, 12);

        total.Should().Be(1);
        items.Should().HaveCount(1);
        items[0].Provider.Should().Be(CatalogSyncProvider.BggApi);
        items[0].Status.Should().Be(CatalogSyncStatus.Queued);
        items[0].Title.Should().Be("BGG cron sync");
        items[0].TriggeredByUserId.Should().BeNull();
    }

    [Fact]
    public async Task TryTriggerSync_AlreadyRunningRunExists_SkipsCreation()
    {
        await SeedRunningRunAsync();

        await _cronService.TryTriggerSyncAsync(TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
        var (_, total) = await repo.GetPagedAsync(1, 12);
        total.Should().Be(1);
    }

    [Fact]
    public async Task TryTriggerSync_MultipleTicks_EachCreatesARun_WhenPreviousIsTerminal()
    {
        await _cronService.TryTriggerSyncAsync(TestContext.Current.CancellationToken);

        // Mark the first run as terminal so the next tick is allowed to create another.
        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var (firstBatch, _) = await repo.GetPagedAsync(1, 12);
            var firstRun = firstBatch.Single();
            firstRun.MarkRunning();
            firstRun.Complete();
            await repo.UpdateAsync(firstRun);
            await uow.SaveChangesAsync();
        }

        await _cronService.TryTriggerSyncAsync(TestContext.Current.CancellationToken);

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
            var (_, total) = await repo.GetPagedAsync(1, 12);
            total.Should().Be(2);
        }
    }

    // ============================================================
    // Helpers
    // ============================================================

    private async Task SeedRunningRunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ICatalogSyncRunRepository>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var run = CatalogSyncRun.Enqueue(CatalogSyncProvider.BggApi, "manual", null);
        run.MarkRunning();
        await repo.AddAsync(run);
        await uow.SaveChangesAsync();
    }
}
