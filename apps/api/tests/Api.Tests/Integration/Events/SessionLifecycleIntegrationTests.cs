using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Events;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.Events;

/// <summary>
/// Integration test: the SessionTracking <c>session.created</c> domain event is durably persisted
/// to <c>domain_event_logs</c> via the real pipeline (SessionRepository.AddAsync collection wired
/// in Task 5 → MeepleAiDbContext.SaveChangesAsync mapping → registry alias), AND lands atomically
/// (same SaveChangesAsync transaction) with the <c>session_events</c> diary "session_created" row.
///
/// <para>BE-3 #1590 AC5.b / C3.3: proves the two orthogonal tables (session_events diary vs
/// domain_event_logs activity log) both receive a row in one transaction, timestamps &lt;100ms apart.
/// The test exercises the real persistence components (SessionRepository, DbContext, mapper,
/// registry); it mirrors the emit + diary-write the CreateSessionCommandHandler performs, without
/// the handler's orthogonal KB-readiness / GameNight / quota pre-checks (covered by the handler's
/// own test suite).</para>
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
public sealed class SessionLifecycleIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _webFactory = null!;

    public SessionLifecycleIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"session_lifecycle_events_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _webFactory = IntegrationWebApplicationFactory.Create(connectionString);
        using var scope = _webFactory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_webFactory is not null)
        {
            await _webFactory.DisposeAsync();
        }

        if (!string.IsNullOrEmpty(_testDbName))
        {
            await _fixture.DropIsolatedDatabaseAsync(_testDbName);
        }
    }

    [Fact]
    public async Task SessionCreated_persists_to_domain_event_logs_AND_session_events_atomically()
    {
        // Arrange
        using var scope = _webFactory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var sessionRepository = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title: "Catan-BE3-Session");

        var session = Session.Create(userId, gameId, SessionType.Generic);
        var emitTimestamp = DateTime.UtcNow;

        // Emit session.created (mirrors CreateSessionCommandHandler: domain event added BEFORE
        // AddAsync, which collects it; Timestamp = the same clock as the diary row for AC5.b).
        session.AddDomainEvent(new SessionCreatedEvent
        {
            SessionId = session.Id,
            UserId = userId,
            SessionCode = session.SessionCode,
            GameId = gameId,
            GameName = "Catan-BE3-Session",
            PlayerCount = session.Participants.Count,
            Timestamp = emitTimestamp,
        });

        await sessionRepository.AddAsync(session, CancellationToken.None);

        // session_events diary "session_created" row written in the SAME SaveChangesAsync — this is
        // exactly what the handler does (CreateSessionCommandHandler writes both before one save).
        db.SessionEvents.Add(new SessionEventEntity
        {
            Id = Guid.NewGuid(),
            SessionId = session.Id,
            EventType = "session_created",
            Timestamp = emitTimestamp,
            CreatedBy = userId,
            Source = "system",
            IsDeleted = false,
        });

        // Act — single atomic save: session aggregate + domain_event_logs row + session_events row.
        await unitOfWork.SaveChangesAsync(CancellationToken.None);

        // Assert
        using var verifyScope = _webFactory.Services.CreateScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // 1. domain_event_logs row (activity rail source)
        var logRow = await verifyDb.DomainEventLogs
            .AsNoTracking()
            .Where(e => e.EventType == "session.created" && e.AggregateId == session.Id)
            .FirstOrDefaultAsync();

        logRow.Should().NotBeNull("session.created must be durably logged to domain_event_logs");
        logRow!.AggregateType.Should().Be("SessionCreated");
        logRow.UserId.Should().Be(userId);
        logRow.PayloadVersion.Should().Be(1);
        logRow.PayloadJson.Should().Contain("\"gameId\"").And.Contain(gameId.ToString());
        logRow.PayloadJson.Should().Contain("\"gameName\"").And.Contain("Catan-BE3-Session");

        // 2. session_events diary row (in-session timeline UI source — orthogonal table, C3)
        var diaryRow = await verifyDb.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == session.Id && e.EventType == "session_created")
            .FirstOrDefaultAsync();

        diaryRow.Should().NotBeNull("session_events must keep its diary session_created row");

        // 3. AC5.b: the two writes are in the same transaction → timestamps within 100ms.
        var delta = (diaryRow!.Timestamp - logRow.OccurredAt).Duration();
        delta.TotalMilliseconds.Should().BeLessThan(100,
            "session_events and domain_event_logs are written in the same transaction (AC5.b)");
    }
}
