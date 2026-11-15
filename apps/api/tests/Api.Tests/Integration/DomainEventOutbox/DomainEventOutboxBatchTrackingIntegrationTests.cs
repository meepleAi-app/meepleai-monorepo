using System.Text.Json;
using Api.BoundedContexts.AgentMemory.Application.EventHandlers;
using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.AgentMemory.Infrastructure.Persistence;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DomainEventOutbox;

/// <summary>
/// #4876: an outbox batch delivers its events on ONE context (<see cref="DomainEventOutboxProcessor.RunOnceAsync"/> opens
/// one scope per batch), and the handlers save on it. The statistics handler reads a memory with <c>AsNoTracking</c> and
/// the repository attached a new instance with <c>DbSet.Update</c>: when the first event of the batch had already saved
/// that row, it was still tracked, and the second event failed with «already being tracked». The entry it left made the
/// batch's own save fail, so the whole batch rolled back and came again on every poll.
/// <see cref="ProcessedSessionsIntegrationTests"/> does not see it: it gives each delivery a context of its own, as a
/// request has.
/// </summary>
/// <remarks>
/// <para>
/// Real processor, real handler, real repository and real PostgreSQL. The mediator delivers the event to the statistics
/// handler only, resolved from the batch scope as MediatR does: the other handlers of the event are not the subject, and
/// a mocked repository would not have a change tracker to conflict on.
/// </para>
/// <para>
/// The per-player stats are retired and their table is dropped (#4876): the same-user variant now proves that two
/// completions without a group still leave the batch whole, and the group is the row the two events still share. The
/// unnamed-guest variant is gone: without the player row it was the same-user case again.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "AgentMemory")]
[Trait("Issue", "4876")]
public sealed class DomainEventOutboxBatchTrackingIntegrationTests : IAsyncLifetime
{
    private const string LiveSessionCompletedAlias = "test.live-session-completed";

    private readonly SharedTestcontainersFixture _fixture;
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DomainEventOutboxBatchTrackingIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_outbox_batch_tracking_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);

        services.RemoveAll<IMediator>();
        services.AddScoped<IMediator>(StatisticsOnlyMediator);

        var resolver = new Mock<IDomainEventTypeResolver>();
        resolver.Setup(r => r.Resolve(LiveSessionCompletedAlias)).Returns(typeof(LiveSessionCompletedEvent));
        services.AddSingleton(resolver.Object);
        services.AddSingleton(Mock.Of<IDomainEventOutboxHealthTracker>());

        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabledAsync("Features:AgentMemory.Enabled", null)).ReturnsAsync(true);
        services.RemoveAll<IFeatureFlagService>();
        services.AddSingleton(flags.Object);

        services.AddScoped<IGroupMemoryRepository, GroupMemoryRepository>();
        services.AddScoped<OnSessionCompletedUpdateStatsHandler>();

        services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
            Options.Create(new DomainEventOutboxOptions
            {
                Mode = DomainEventDispatchMode.OutboxOnly,
                PollIntervalSeconds = 5,
                BatchSize = 100,
                MaxAttempts = 10,
                InitialBackoffMs = 1000,
                MaxBackoffSeconds = 64.0,
            }));
        services.AddSingleton<DomainEventOutboxProcessor>();

        _serviceProvider = services.BuildServiceProvider();

        await using var bootstrap = _serviceProvider.CreateAsyncScope();
        await bootstrap.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync(Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null) await _serviceProvider.DisposeAsync();
        if (!string.IsNullOrEmpty(_databaseName))
        {
            try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
            catch { /* ignore cleanup errors */ }
        }
    }

    /// <summary>One mediator per scope, so the handler it calls lives on the batch's context, as with MediatR.</summary>
    private static IMediator StatisticsOnlyMediator(IServiceProvider scope)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns((IDomainEvent evt, CancellationToken ct) => evt is LiveSessionCompletedEvent completed
                ? scope.GetRequiredService<OnSessionCompletedUpdateStatsHandler>().Handle(completed, ct)
                : Task.CompletedTask);
        return mediator.Object;
    }

    [Fact]
    public async Task OneBatch_TwoSessionsOfTheSameRegisteredUser_BothRowsSent()
    {
        var gameId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        await EnqueueAsync(
            Completed(gameId, groupId: null, Player(userId, "Alice", rank: 1)),
            Completed(gameId, groupId: null, Player(userId, "Alice", rank: 2)));

        await RunOneBatchAndExpectBothRowsSentAsync();
    }

    [Fact]
    public async Task OneBatch_TwoSessionsOfTheSameGroup_BothRowsSentAndTheGroupCountedTwice()
    {
        var gameId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var group = GroupMemory.Create(creatorId, "Giovedi");
        await using (var seed = _serviceProvider!.CreateAsyncScope())
        {
            await seed.ServiceProvider.GetRequiredService<IGroupMemoryRepository>().AddAsync(group, Ct);
            await seed.ServiceProvider.GetRequiredService<MeepleAiDbContext>().SaveChangesAsync(Ct);
        }

        // Different players in the two sessions: only the group row is shared by the two events.
        await EnqueueAsync(
            Completed(gameId, group.Id, Player(Guid.NewGuid(), "Anna", rank: 1)),
            Completed(gameId, group.Id, Player(Guid.NewGuid(), "Bruno", rank: 1)));

        await RunOneBatchAndExpectBothRowsSentAsync();

        await using var read = _serviceProvider!.CreateAsyncScope();
        var counted = await read.ServiceProvider.GetRequiredService<IGroupMemoryRepository>().GetByIdAsync(group.Id, Ct);
        counted!.Stats.TotalSessions.Should().Be(2);
        counted.Stats.GamePlayCounts[gameId].Should().Be(2);
        counted.ProcessedSessionIds.Should().HaveCount(2);
    }

    private async Task RunOneBatchAndExpectBothRowsSentAsync()
    {
        var processor = _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();
        var processed = await processor.RunOnceAsync(batchSize: 100, cancellationToken: Ct);
        processed.Should().Be(2, "both events are in the same batch");

        await using var verify = _serviceProvider!.CreateAsyncScope();
        var rows = await verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>().DomainEventOutbox
            .AsNoTracking()
            .OrderBy(r => r.EnqueuedAt)
            .ToListAsync(Ct);
        rows.Should().HaveCount(2);
        rows.Should().AllSatisfy(r =>
        {
            r.LastError.Should().BeNull("no event of the batch may fail on the other's tracked row");
            r.Status.Should().Be(DomainEventOutboxStatus.Sent);
            r.Attempts.Should().Be(0);
        });
    }

    private async Task EnqueueAsync(params LiveSessionCompletedEvent[] events)
    {
        var now = DateTimeOffset.UtcNow;
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        for (var i = 0; i < events.Length; i++)
        {
            db.DomainEventOutbox.Add(DomainEventOutboxEntity.Enqueue(
                events[i],
                LiveSessionCompletedAlias,
                JsonSerializer.Serialize(events[i], DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: now.AddSeconds(i)));
        }
        await db.SaveChangesAsync(Ct);
    }

    private static CompletedPlayerSnapshot Player(Guid? userId, string displayName, int rank) =>
        new(Guid.NewGuid(), userId, displayName, TotalScore: 10, CurrentRank: rank);

    private static LiveSessionCompletedEvent Completed(Guid gameId, Guid? groupId, CompletedPlayerSnapshot player)
    {
        var now = DateTime.UtcNow;
        return new LiveSessionCompletedEvent(
            sessionId: Guid.NewGuid(),
            completedAt: now,
            totalTurns: 5,
            gameId: gameId,
            gameName: "Catan",
            createdByUserId: player.UserId ?? Guid.NewGuid(),
            visibility: PlayRecordVisibility.Private,
            groupId: groupId,
            sessionDate: now.AddHours(-1),
            startedAt: now.AddHours(-1),
            notes: null,
            players: [player],
            scores: []);
    }
}
