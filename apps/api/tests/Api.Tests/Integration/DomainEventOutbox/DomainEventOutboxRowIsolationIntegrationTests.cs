using System.Data.Common;
using System.Diagnostics.Metrics;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.BackgroundJobs;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.Administration;
using Api.Infrastructure.Entities.DomainEventOutbox;
using Api.SharedKernel.Domain.Interfaces;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DomainEventOutbox;

/// <summary>
/// #5030: <see cref="DomainEventOutboxProcessor.RunOnceAsync"/> delivers a batch on ONE context, and the handlers write on
/// it. Before this change the batch saved once, at the end: a handler that left in the change tracker something the save
/// could not write — an entity violating a constraint, or the half-attached entry of #4876 — did not fail its own row, it
/// failed the batch. The transaction rolled back, every row stayed <c>Pending</c> with <c>Attempts</c> 0, and the next
/// poll took the same rows again, forever: no attempt was ever counted, so <c>MaxAttempts</c> never stopped it.
/// </summary>
/// <remarks>
/// <para>
/// Real processor and real PostgreSQL; the mediator calls a per-test delegate on the batch's scope, so the "handler" writes
/// on the same context the processor saves, exactly as a MediatR handler resolved from that scope does.
/// </para>
/// <para>
/// The direction that must NOT change: what a failing handler (or its siblings) already SAVED stays committed with the
/// batch. Sibling handlers with an external side effect rely on that to keep their "already sent" marker when another
/// handler of the same event fails; a fix that rolled the failing row back to a savepoint would make them repeat the side
/// effect on the retry. The third test pins it for a handler that just throws (it passes on <c>main-dev</c> as well), the
/// first for a handler that also leaves an unsavable residue.
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Issue", "5030")]
public sealed class DomainEventOutboxRowIsolationIntegrationTests : IAsyncLifetime
{
    private const string RowEventAlias = "test.outbox-row-isolation";
    private const string BatchFailedInstrument = "meepleai.domain_event_outbox.batch_failed.total";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly CommitRefusingInterceptor _commits = new();
    private readonly Mock<IDomainEventOutboxHealthTracker> _health = new();
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;
    private Func<IServiceProvider, RowEvent, CancellationToken, Task> _onPublish = (_, _, _) => Task.CompletedTask;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DomainEventOutboxRowIsolationIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_outbox_row_isolation_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(_commits));

        services.RemoveAll<IMediator>();
        services.AddScoped<IMediator>(DelegatingMediator);

        var resolver = new Mock<IDomainEventTypeResolver>();
        resolver.Setup(r => r.Resolve(RowEventAlias)).Returns(typeof(RowEvent));
        services.AddSingleton(resolver.Object);
        services.AddSingleton(_health.Object);

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

    /// <summary>One mediator per scope, so the delegate runs on the batch's context, as a MediatR handler does.</summary>
    private IMediator DelegatingMediator(IServiceProvider scope)
    {
        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Publish(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Returns((IDomainEvent evt, CancellationToken ct) => _onPublish(scope, (RowEvent)evt, ct));
        return mediator.Object;
    }

    [Fact]
    public async Task OneBatch_HandlerThrowsLeavingAnUnsavableEntity_OnlyItsRowIsRetried()
    {
        var orphanId = Guid.NewGuid();
        var savedId = Guid.NewGuid();
        _onPublish = async (scope, evt, ct) =>
        {
            if (evt.Marker != 2) return;
            var db = scope.GetRequiredService<MeepleAiDbContext>();

            // What a sibling handler of the same event saved first — e.g. an "already sent" marker.
            db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(savedId, "{}", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);

            db.RuleSpecComments.Add(OrphanComment(orphanId));
            throw new InvalidOperationException("handler-2 failed");
        };
        var ids = await EnqueueAsync(1, 2, 3);

        var processed = await Processor().RunOnceAsync(batchSize: 100, cancellationToken: Ct);

        processed.Should().Be(3);
        var rows = await ReadRowsAsync();
        rows[ids[0]].Status.Should().Be(DomainEventOutboxStatus.Sent);
        rows[ids[2]].Status.Should().Be(DomainEventOutboxStatus.Sent, "a row after the failing one is still delivered");
        var failed = rows[ids[1]];
        failed.Status.Should().Be(DomainEventOutboxStatus.Pending);
        failed.Attempts.Should().Be(1, "the failure is counted, so MaxAttempts can end it");
        failed.NextAttemptAt.Should().NotBeNull();
        failed.LastError.Should().Contain("handler-2 failed", "the handler's own error is the cause worth reading");
        (await CommentExistsAsync(orphanId)).Should().BeFalse("what the failing handler left unsaved is dropped");
        (await AuditRowExistsAsync(savedId)).Should().BeTrue(
            "only the unsaved residue is dropped: what was already saved stays, as when a handler just throws");
    }

    [Fact]
    public async Task OneBatch_HandlerReturnsLeavingAnUnsavableEntity_ItsRowIsRetriedWithTheSaveError()
    {
        var orphanId = Guid.NewGuid();
        _onPublish = (scope, evt, _) =>
        {
            if (evt.Marker == 2)
            {
                scope.GetRequiredService<MeepleAiDbContext>().RuleSpecComments.Add(OrphanComment(orphanId));
            }

            return Task.CompletedTask;
        };
        var ids = await EnqueueAsync(1, 2, 3);

        var processed = await Processor().RunOnceAsync(batchSize: 100, cancellationToken: Ct);

        processed.Should().Be(3);
        var rows = await ReadRowsAsync();
        rows[ids[0]].Status.Should().Be(DomainEventOutboxStatus.Sent);
        rows[ids[2]].Status.Should().Be(DomainEventOutboxStatus.Sent);
        var failed = rows[ids[1]];
        failed.Status.Should().Be(DomainEventOutboxStatus.Pending, "its changes were never written, so it was not delivered");
        failed.Attempts.Should().Be(1);
        failed.LastError.Should().Contain("23503", "the save's own error, not EF's «see the inner exception»");
        (await CommentExistsAsync(orphanId)).Should().BeFalse();
    }

    [Fact]
    public async Task OneBatch_HandlerSavesThenThrows_WhatItSavedStaysCommitted()
    {
        var savedId = Guid.NewGuid();
        _onPublish = async (scope, evt, ct) =>
        {
            if (evt.Marker != 2) return;
            var db = scope.GetRequiredService<MeepleAiDbContext>();
            db.AuditOutbox.Add(AuditOutboxEntity.CreatePending(savedId, "{}", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync(ct);
            throw new InvalidOperationException("handler-2 failed after saving");
        };
        var ids = await EnqueueAsync(1, 2, 3);

        await Processor().RunOnceAsync(batchSize: 100, cancellationToken: Ct);

        var rows = await ReadRowsAsync();
        rows[ids[1]].Status.Should().Be(DomainEventOutboxStatus.Pending);
        rows[ids[1]].Attempts.Should().Be(1);
        rows[ids[0]].Status.Should().Be(DomainEventOutboxStatus.Sent);
        rows[ids[2]].Status.Should().Be(DomainEventOutboxStatus.Sent);
        (await AuditRowExistsAsync(savedId)).Should().BeTrue(
            "a write the handler already saved is not rolled back by its row's failure");
    }

    /// <summary>
    /// A batch can still fail as a whole — here its commit is refused. Before this change that left no trace but a log line
    /// per poll: the health snapshot was refreshed only after a successful batch, so the oldest-Pending age the stale alert
    /// reads stayed frozen at its last value. Now the failure is counted, and the snapshot is refreshed anyway.
    /// </summary>
    [Fact]
    public async Task WholeBatchFails_FailureIsCountedAndTheHealthSnapshotIsStillRefreshed()
    {
        await EnqueueAsync(1);
        _commits.Refuse = true;

        var failures = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == BatchFailedInstrument) l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "exception_type") lock (failures) failures.Add(tag.Value as string);
            }
        });
        listener.Start();

        var run = async () => await Processor().RunOnceAsync(batchSize: 100, cancellationToken: Ct);

        await run.Should().ThrowAsync<CommitRefusedException>();
        lock (failures)
        {
            failures.Should().Contain(nameof(CommitRefusedException));
        }

        _health.Verify(h => h.RecordSnapshot(1, It.IsAny<double>(), 0), Times.Once(),
            "the one row is still Pending, and the snapshot must say so even though the batch failed");
    }

    private DomainEventOutboxProcessor Processor() => _serviceProvider!.GetRequiredService<DomainEventOutboxProcessor>();

    private static RuleSpecCommentEntity OrphanComment(Guid id) => new()
    {
        Id = id,
        GameId = Guid.NewGuid(), // no such game: the INSERT violates the foreign key (23503)
        Version = "1.0.0",
        CommentText = "left behind by a failing handler",
        UserId = Guid.NewGuid(),
        CreatedAt = DateTime.UtcNow,
    };

    private async Task<IReadOnlyList<Guid>> EnqueueAsync(params int[] markers)
    {
        var start = DateTimeOffset.UtcNow.AddMinutes(-1);
        var ids = new List<Guid>(markers.Length);
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        for (var i = 0; i < markers.Length; i++)
        {
            var evt = new RowEvent(markers[i]);
            db.DomainEventOutbox.Add(DomainEventOutboxEntity.Enqueue(
                evt,
                RowEventAlias,
                JsonSerializer.Serialize(evt, DomainEventJsonOptions.Default),
                payloadVersion: 1,
                correlationId: null,
                now: start.AddSeconds(i))); // FIFO order = marker order
            ids.Add(evt.EventId);
        }

        await db.SaveChangesAsync(Ct);
        return ids;
    }

    private async Task<Dictionary<Guid, DomainEventOutboxEntity>> ReadRowsAsync()
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().DomainEventOutbox
            .AsNoTracking()
            .ToDictionaryAsync(r => r.Id, Ct);
    }

    private async Task<bool> AuditRowExistsAsync(Guid id)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().AuditOutbox
            .AsNoTracking()
            .AnyAsync(r => r.Id == id, Ct);
    }

    private async Task<bool> CommentExistsAsync(Guid id)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().RuleSpecComments
            .AsNoTracking()
            .AnyAsync(c => c.Id == id, Ct);
    }

    internal sealed record RowEvent : IDomainEvent
    {
        public RowEvent(int Marker)
        {
            this.Marker = Marker;
            EventId = Guid.NewGuid();
            OccurredAt = DateTime.UtcNow;
        }

        // Parameterless constructor for System.Text.Json round-trip via DomainEventJsonOptions.
        public RowEvent() : this(0) { }

        public int Marker { get; init; }
        public Guid EventId { get; init; }
        public DateTime OccurredAt { get; init; }
    }

    private sealed class CommitRefusedException() : Exception("commit refused by the test");

    /// <summary>Refuses every commit while armed: the one way left to fail a batch whole.</summary>
    private sealed class CommitRefusingInterceptor : DbTransactionInterceptor
    {
        public volatile bool Refuse;

        public override ValueTask<InterceptionResult> TransactionCommittingAsync(
            DbTransaction transaction,
            TransactionEventData eventData,
            InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            if (Refuse) throw new CommitRefusedException();
            return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
        }
    }
}
