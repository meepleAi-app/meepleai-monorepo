using System.Reflection;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundServices;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Integration;

/// <summary>
/// Integration tests for <see cref="MechanicRecalcBackgroundService"/> (ADR-051 Sprint 2 / Task 8).
/// Exercises the worker's reflective <c>ProcessNextJobAsync</c> entry point against a real
/// PostgreSQL database (Testcontainers) and a real DI scope tree. The candidate analysis set is
/// intentionally empty so the per-id loop is skipped — that still drives the full lifecycle:
/// Pending → claim → Running → Reconstitute(total=0) → Complete.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicRecalcBackgroundServiceIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private string _connectionString = null!;
    private MeepleAiDbContext _dbContext = null!;
    private ServiceProvider _serviceProvider = null!;
    private Guid _testUserId;

    public MechanicRecalcBackgroundServiceIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"recalcworker_test_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        // Seed a user so the FK on triggered_by_user_id is satisfiable.
        _testUserId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = _testUserId,
            Email = $"recalcworker-{Guid.NewGuid():N}@meepleai.test",
            Role = "Admin",
            Tier = "Free",
            CreatedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Build a real DI provider so the worker's IServiceScopeFactory resolves
        // IMechanicRecalcJobRepository, IMechanicAnalysisRepository, IUnitOfWork,
        // MeepleAiDbContext, and IMediator from genuine scopes.
        var services = IntegrationServiceCollectionBuilder.CreateBase(_connectionString);
        services.AddScoped<IMechanicRecalcJobRepository, MechanicRecalcJobRepository>();
        services.AddScoped<IMechanicAnalysisRepository, MechanicAnalysisRepository>();
        _serviceProvider = services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _serviceProvider.DisposeAsync();
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    [Fact]
    public async Task ProcessNextJobAsync_HappyPath_TransitionsPendingToCompleted()
    {
        // Arrange — enqueue one Pending job. No MechanicAnalysis rows are seeded, so the
        // worker's GetIdsByStatusAsync(Published) returns an empty list, the per-id loop is
        // skipped, and the terminal Running → Completed transition is taken (Step 4 in
        // ProcessNextJobAsync).
        var job = MechanicRecalcJob.Enqueue(_testUserId);
        var repo = new MechanicRecalcJobRepository(
            _dbContext,
            CreateNoOpEventCollector());
        await repo.AddAsync(job);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new MechanicRecalcBackgroundService(
            scopeFactory,
            NullLogger<MechanicRecalcBackgroundService>.Instance);

        // Act
        await InvokeProcessNextJobAsync(worker);

        // Assert — reload from a fresh context to bypass any tracker state.
        await using var verifyCtx = _fixture.CreateDbContext(_connectionString);
        var reloaded = await verifyCtx.MechanicRecalcJobs
            .AsNoTracking()
            .SingleAsync(j => j.Id == job.Id);

        reloaded.Status.Should().Be(RecalcJobStatus.Completed);
        reloaded.StartedAt.Should().NotBeNull();
        reloaded.CompletedAt.Should().NotBeNull();
        reloaded.Total.Should().Be(0);
        reloaded.Processed.Should().Be(0);
        reloaded.Failed.Should().Be(0);
    }

    [Fact]
    public async Task ProcessNextJobAsync_CancellationRequested_HonorsFlagAndCompletesWithoutProcessing()
    {
        // Arrange — seed a SharedGame + one Published MechanicAnalysis so the worker's
        // GetIdsByStatusAsync(Published) returns exactly one id and the per-id loop has at
        // least one iteration to encounter the cancellation flag at line ~163 of the worker.
        var sharedGameId = Guid.NewGuid();
        _dbContext.Set<SharedGameEntity>().Add(new SharedGameEntity
        {
            Id = sharedGameId,
            Title = $"CancelTest Game {Guid.NewGuid():N}",
            Description = "Cancel-mid-flight integration test fixture",
            MinPlayers = 2,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            YearPublished = 2024,
            MinAge = 10,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _testUserId,
        });

        var analysisId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        _dbContext.Set<MechanicAnalysisEntity>().Add(new MechanicAnalysisEntity
        {
            Id = analysisId,
            SharedGameId = sharedGameId,
            PdfDocumentId = Guid.NewGuid(), // synthetic — no FK in scope for this worker test
            PromptVersion = "mechanic-extractor-v1",
            Status = 2, // Published — matches MechanicAnalysisStatus.Published cast in GetIdsByStatusAsync
            CreatedBy = _testUserId,
            CreatedAt = now,
            ReviewedBy = _testUserId,
            ReviewedAt = now,
            TotalTokensUsed = 1234,
            EstimatedCostUsd = 0.05m,
            ModelUsed = "test-model",
            Provider = "test-provider",
            CostCapUsd = 1.00m,
            IsSuppressed = false,
        });

        // Enqueue a Pending job and request cancellation BEFORE save so the persisted row
        // carries cancellation_requested=true. ClaimNextPendingAsync's RETURNING * projection
        // will then bring the flag back into the reconstituted aggregate, and the worker's
        // per-id loop will short-circuit on iteration 1 (line ~163: `if (job.CancellationRequested)`).
        var job = MechanicRecalcJob.Enqueue(_testUserId);
        job.RequestCancellation(); // allowed from Pending per the aggregate's transition contract
        var repo = new MechanicRecalcJobRepository(
            _dbContext,
            CreateNoOpEventCollector());
        await repo.AddAsync(job);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new MechanicRecalcBackgroundService(
            scopeFactory,
            NullLogger<MechanicRecalcBackgroundService>.Instance);

        // Act — single tick: ClaimNext (Pending→Running, flag preserved) → enumerate ids (count=1)
        // → ReconstituteWithTotal(1) → loop iter 1 sees flag → job.Complete() (Running→Completed)
        // → break → terminal persist. Per Task 6 lifecycle: cancellation is a flag, not a status.
        await InvokeProcessNextJobAsync(worker);

        // Assert — reload from a fresh context (mirroring the happy-path test pattern).
        await using var verifyCtx = _fixture.CreateDbContext(_connectionString);
        var reloaded = await verifyCtx.MechanicRecalcJobs
            .AsNoTracking()
            .SingleAsync(j => j.Id == job.Id);

        // Option A (snippet governs): worker calls job.Complete() on the cancel-honour branch,
        // so the terminal status is Completed — NOT Cancelled (the aggregate has no public path
        // to RecalcJobStatus.Cancelled; only the flag exists).
        reloaded.Status.Should().Be(RecalcJobStatus.Completed);
        reloaded.CancellationRequested.Should().BeTrue("the flag is preserved through the round-trip and persists alongside the terminal status");
        reloaded.StartedAt.Should().NotBeNull("ClaimNextPendingAsync stamps StartedAt on the Pending→Running claim");
        reloaded.CompletedAt.Should().NotBeNull("job.Complete() stamps CompletedAt on the Running→Completed transition");
        reloaded.Total.Should().Be(1, "ReconstituteWithTotalAsync set Total to the candidate count (1 Published analysis) before the per-id loop");
        reloaded.Processed.Should().Be(0, "the loop short-circuited on iteration 1 before sending the MediatR command, so RecordSuccess was never called");
        reloaded.Failed.Should().Be(0);
        reloaded.Skipped.Should().Be(0);
    }

    [Fact]
    public async Task ProcessNextJobAsync_NoPendingJob_NoOp()
    {
        // Arrange — empty queue.
        var initialCount = await _dbContext.MechanicRecalcJobs.CountAsync();
        initialCount.Should().Be(0);

        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var worker = new MechanicRecalcBackgroundService(
            scopeFactory,
            NullLogger<MechanicRecalcBackgroundService>.Instance);

        // Act + Assert — must not throw.
        var act = async () => await InvokeProcessNextJobAsync(worker);
        await act.Should().NotThrowAsync();

        // Assert — no rows materialized.
        await using var verifyCtx = _fixture.CreateDbContext(_connectionString);
        var finalCount = await verifyCtx.MechanicRecalcJobs.AsNoTracking().CountAsync();
        finalCount.Should().Be(0);
    }

    private static async Task InvokeProcessNextJobAsync(MechanicRecalcBackgroundService worker)
    {
        var method = typeof(MechanicRecalcBackgroundService)
            .GetMethod("ProcessNextJobAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull("ProcessNextJobAsync is the canonical reflective entry point for testing the worker tick");
        await (Task)method!.Invoke(worker, new object[] { CancellationToken.None })!;
    }

    private static Api.SharedKernel.Application.Services.IDomainEventCollector CreateNoOpEventCollector()
    {
        var mock = new Moq.Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mock.Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());
        return mock.Object;
    }
}
