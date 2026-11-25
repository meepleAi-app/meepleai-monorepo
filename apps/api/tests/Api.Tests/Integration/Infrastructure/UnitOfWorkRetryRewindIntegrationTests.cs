using System.Data.Common;
using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Administration.Application.Behaviors;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Domain.Events;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

/// <summary>
/// #5145 — the retrying execution strategy may run the delegate of <c>ExecuteInTransactionAsync</c> again after a
/// transient failure, and nothing put the change tracker back between one attempt and the next. A failed save left
/// the attempt's rows <c>Added</c>, written again next to the retry's own; a failed commit left them
/// <c>Unchanged</c>, so a change the retry repeated was not written at all. Each attempt now starts from the tracker
/// the caller handed in — and only from that: what the caller tracked before stays tracked (the PDF pipeline saves
/// it after the transaction).
/// </summary>
/// <remarks>
/// The host has the retrying strategy (<c>IntegrationServiceCollectionBuilder.CreateBase</c> enables it), unlike
/// the <c>Testing</c> environment of the application, where the delegate runs once and the defect cannot be seen.
/// The failure is the one the strategy replays: an <see cref="NpgsqlException"/> around a <see cref="TimeoutException"/>.
/// </remarks>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "SharedKernel")]
[Trait("Issue", "5145")]
public sealed class UnitOfWorkRetryRewindIntegrationTests : IAsyncLifetime
{
    private const string Marker = "@retry-5145.test";

    private readonly SharedTestcontainersFixture _fixture;
    private readonly TransientFaults _faults = new();
    private string _databaseName = string.Empty;
    private ServiceProvider? _serviceProvider;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public UnitOfWorkRetryRewindIntegrationTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_uowretry_{Guid.NewGuid():N}";
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        var services = IntegrationServiceCollectionBuilder.CreateBase(connectionString);
        services.ConfigureDbContext<MeepleAiDbContext>(options => options.AddInterceptors(_faults.Save, _faults.Commit));

        // The atomic audit path is a second retry loop: a probe command goes through it. No HttpContext: the row has
        // no caller, which is not what these tests look at.
        services.AddSingleton(new Moq.Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>().Object);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(AuditLoggingBehavior<,>));
        services.AddTransient<IRequestHandler<AtomicRetryProbeCommand, Guid>, AtomicRetryProbeHandler>();
        _serviceProvider = services.BuildServiceProvider();

        await using var scope = _serviceProvider.CreateAsyncScope();
        await TestMigrationHelper.MigrateWithRetryAsync(scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>(), Ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_serviceProvider is not null)
            await _serviceProvider.DisposeAsync();
        try { await _fixture.DropIsolatedDatabaseAsync(_databaseName); }
        catch { /* ignore cleanup errors */ }
    }

    [Fact]
    public async Task AFailedSave_IsNotWrittenAgainNextToTheRetry()
    {
        var email = $"once{Marker}";
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        _faults.Save.Armed = true;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            db.Users.Add(NewUser(email)); // a new row on every attempt, as a handler builds its aggregate
            return Task.CompletedTask;
        }, Ct);

        _faults.Save.Tripped.Should().BeTrue("the first save must have failed, or this proves nothing");
        (await CountUsersAsync(email)).Should().Be(1, "the failed attempt's row is not written next to the retry's");
    }

    [Fact]
    public async Task AFailedCommit_DoesNotLoseAChangeTheRetryRepeats()
    {
        var email = $"renamed{Marker}";
        var userId = await SeedUserAsync(email, "before");

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var user = await db.Users.AsTracking().SingleAsync(u => u.Id == userId, Ct); // tracked before the transaction

        _faults.Commit.Armed = true;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            user.DisplayName = "after";
            return Task.CompletedTask;
        }, Ct);

        _faults.Commit.Tripped.Should().BeTrue("the first commit must have failed, or this proves nothing");
        (await ReadDisplayNameAsync(userId)).Should().Be("after",
            "the first attempt's save was rolled back; the retry has to write the change again");
    }

    /// <summary>
    /// A row with a concurrency token (<c>xmin</c>, ADR-060). The failed attempt's update was saved and then rolled
    /// back, and the tracker kept, as the original token, the one that update produced: the retry's
    /// <c>UPDATE … WHERE xmin = …</c> matched no row and answered a concurrency conflict.
    /// </summary>
    [Fact]
    public async Task AFailedCommit_DoesNotLeaveTheRolledBackTokenAsTheOriginal()
    {
        var organizerId = await SeedUserAsync($"organizer{Marker}", "Organizer");
        var nightId = await SeedNightAsync(organizerId, $"before{Marker}");

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var night = await db.GameNightEvents.AsTracking().SingleAsync(n => n.Id == nightId, Ct);

        _faults.Commit.Armed = true;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            night.Title = $"after{Marker}";
            return Task.CompletedTask;
        }, Ct);

        _faults.Commit.Tripped.Should().BeTrue("the first commit must have failed, or this proves nothing");
        await using var read = _serviceProvider!.CreateAsyncScope();
        (await read.ServiceProvider.GetRequiredService<MeepleAiDbContext>().GameNightEvents.AsNoTracking()
            .Where(n => n.Id == nightId).Select(n => n.Title).SingleAsync(Ct)).Should().Be($"after{Marker}");
    }

    [Fact]
    public async Task WhatTheCallerTrackedBefore_StaysTrackedAfterARetry()
    {
        var callerEmail = $"caller{Marker}";
        var callerId = await SeedUserAsync(callerEmail, "caller");

        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var callers = await db.Users.AsTracking().SingleAsync(u => u.Id == callerId, Ct);

        _faults.Save.Armed = true;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            db.Users.Add(NewUser($"inside{Marker}"));
            return Task.CompletedTask;
        }, Ct);

        _faults.Save.Tripped.Should().BeTrue();
        db.Entry(callers).State.Should().Be(EntityState.Unchanged, "the retry forgets only what the failed attempt brought in");

        // What the PDF pipeline does: it saves, after the transaction, an entity it tracked before.
        callers.DisplayName = "saved after";
        await db.SaveChangesAsync(Ct);
        (await ReadDisplayNameAsync(callerId)).Should().Be("saved after");
    }

    [Fact]
    public async Task AFailedSave_DoesNotQueueTheAttemptsEventsTwice()
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var events = scope.ServiceProvider.GetRequiredService<IDomainEventCollector>();
        var marker = Guid.NewGuid();

        _faults.Save.Armed = true;
        await uow.ExecuteInTransactionAsync(_ =>
        {
            db.Users.Add(NewUser($"events{Marker}"));
            events.Collect(new RetryProbeEvent { Marker = marker }); // raised again by every attempt
            return Task.CompletedTask;
        }, Ct);

        _faults.Save.Tripped.Should().BeTrue();
        var queued = await db.DomainEventOutbox.AsNoTracking()
            .Where(r => r.EventType == typeof(RetryProbeEvent).FullName)
            .CountAsync(Ct);
        queued.Should().Be(1, "the failed attempt's event stays in the collector unless the retry puts it back as it was");
    }

    [Fact]
    public async Task AnAtomicAuditCommand_RetriedAfterAFailedSave_WritesItsRowAndItsAuditOnce()
    {
        var email = $"atomic{Marker}";
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        _faults.Save.Armed = true;
        await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new AtomicRetryProbeCommand(email), Ct);

        _faults.Save.Tripped.Should().BeTrue("the handler's first save must have failed, or this proves nothing");
        (await CountUsersAsync(email)).Should().Be(1, "the failed attempt's row is not written next to the retry's");
        // payload_json is jsonb: the match is made here, not in SQL.
        var audits = (await db.AuditOutbox.AsNoTracking().ToListAsync(Ct))
            .Count(r => r.PayloadJson.Contains(nameof(AtomicRetryProbeCommand), StringComparison.Ordinal));
        audits.Should().Be(1);
    }

    private static UserEntity NewUser(string email) => new()
    {
        Id = Guid.NewGuid(),
        Email = email,
        PasswordHash = "x",
        DisplayName = "Retry probe",
    };

    private async Task<Guid> SeedUserAsync(string email, string displayName)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var user = NewUser(email);
        user.DisplayName = displayName;
        db.Users.Add(user);
        await db.SaveChangesAsync(Ct);
        return user.Id;
    }

    private async Task<Guid> SeedNightAsync(Guid organizerId, string title)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var night = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = title,
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(1),
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.GameNightEvents.Add(night);
        await db.SaveChangesAsync(Ct);
        return night.Id;
    }

    private async Task<int> CountUsersAsync(string email)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users.AsNoTracking()
            .CountAsync(u => u.Email == email, Ct);
    }

    private async Task<string?> ReadDisplayNameAsync(Guid userId)
    {
        await using var scope = _serviceProvider!.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => u.DisplayName).SingleAsync(Ct);
    }

    [AuditableAction("RetryProbe", "User")]
    [AtomicAudit]
    internal sealed record AtomicRetryProbeCommand(string Email) : IRequest<Guid>;

    /// <summary>A handler as they are written: it builds its row on every run and saves it.</summary>
    internal sealed class AtomicRetryProbeHandler(MeepleAiDbContext db) : IRequestHandler<AtomicRetryProbeCommand, Guid>
    {
        public async Task<Guid> Handle(AtomicRetryProbeCommand request, CancellationToken cancellationToken)
        {
            var user = NewUser(request.Email);
            db.Users.Add(user);
            await db.SaveChangesAsync(cancellationToken);
            return user.Id;
        }
    }

    internal sealed class RetryProbeEvent : DomainEventBase
    {
        public Guid Marker { get; init; }
    }

    /// <summary>One transient failure at the save, or just before the commit, of a probe user. Armed per test.</summary>
    private sealed class TransientFaults
    {
        public SaveFault Save { get; } = new();
        public CommitFault Commit { get; } = new();

        private static NpgsqlException Transient(string where) =>
            new($"transient failure at the {where}, injected by the test", new TimeoutException());

        private static bool TouchesAProbe(DbContext? context) =>
            context!.ChangeTracker.Entries<UserEntity>().Any(e => e.Entity.Email.EndsWith(Marker, StringComparison.Ordinal))
            || context.ChangeTracker.Entries<GameNightEventEntity>().Any(e => e.Entity.Title.EndsWith(Marker, StringComparison.Ordinal));

        public sealed class SaveFault : SaveChangesInterceptor
        {
            public volatile bool Armed;
            public volatile bool Tripped;

            public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
                DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
            {
                if (Armed && eventData.Context!.ChangeTracker.Entries<UserEntity>()
                        .Any(e => e.State is EntityState.Added or EntityState.Modified && e.Entity.Email.EndsWith(Marker, StringComparison.Ordinal)))
                {
                    Armed = false;
                    Tripped = true;
                    throw Transient("save");
                }

                return base.SavingChangesAsync(eventData, result, cancellationToken);
            }
        }

        public sealed class CommitFault : DbTransactionInterceptor
        {
            public volatile bool Armed;
            public volatile bool Tripped;

            public override ValueTask<InterceptionResult> TransactionCommittingAsync(
                DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
                CancellationToken cancellationToken = default)
            {
                if (Armed && TouchesAProbe(eventData.Context))
                {
                    Armed = false;
                    Tripped = true;
                    throw Transient("commit");
                }

                return base.TransactionCommittingAsync(transaction, eventData, result, cancellationToken);
            }
        }
    }
}
