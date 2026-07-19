using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration test for the explicit go-live sub-resource (epic #3188 Slice 2):
/// two concurrent <see cref="GoLiveSessionCommand"/>s targeting two DISTINCT pending drafts of the
/// SAME game night must resolve to exactly one live session — the other loses with a 409-mapping
/// exception (via the max-1-live guard, the aggregate xmin race, or the partial-unique live-slot
/// index). No 500, no orphaned second live session.
///
/// <para>Mirrors the Testcontainers + full-DI factory pattern of <c>CorrelatedGameSessionOnStartTests</c>.
/// The <c>DomainEventOutboxProcessor</c> BackgroundService is not running in the test host, so the
/// invariante #15 night promotion (Published → InProgress) is not asserted here — the contract under
/// test is the single-live-slot arbitration on <c>game_night_sessions</c>.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
public sealed class GoLiveSessionConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"golive_concurrency_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public GoLiveSessionConcurrencyTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(connectionString);

        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        db.Database.Migrate();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
            await _factory.DisposeAsync();

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "Two concurrent go-lives on the same night: exactly one wins, the other 409s, no double-live")]
    public async Task TwoConcurrentGoLive_SameNight_OnlyOneWins()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        Guid userId;
        Guid gameId;
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            userId = await SeedUserAsync(db);
            gameId = await SeedSharedGameAsync(db);
        }

        // Two real (standalone) tracking sessions — SkipGameNightEnvelope so the create path does NOT
        // mint its own night link; we attach both to ONE Published night as pending drafts below.
        var sessionId1 = await CreateStandaloneSessionAsync(userId, gameId);
        var sessionId2 = await CreateStandaloneSessionAsync(userId, gameId);

        var nightId = Guid.NewGuid();
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.GameNightEvents.Add(new GameNightEventEntity
            {
                Id = nightId,
                OrganizerId = userId,
                Title = "Concurrent Go-Live Night",
                ScheduledAt = now,
                GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { gameId }),
                Status = nameof(GameNightStatus.Published),
                CreatedAt = now,
                UpdatedAt = now,
            });
            db.GameNightSessions.Add(new GameNightSessionEntity
            {
                Id = Guid.NewGuid(),
                GameNightEventId = nightId,
                SessionId = sessionId1,
                GameId = gameId,
                GameTitle = "Catan",
                PlayOrder = 1,
                Status = nameof(GameNightSessionStatus.Pending),
            });
            db.GameNightSessions.Add(new GameNightSessionEntity
            {
                Id = Guid.NewGuid(),
                GameNightEventId = nightId,
                SessionId = sessionId2,
                GameId = gameId,
                GameTitle = "Dixit",
                PlayOrder = 2,
                Status = nameof(GameNightSessionStatus.Pending),
            });
            await db.SaveChangesAsync();
        }

        // ── Act — two go-lives race on the night's single live slot ─────────────
        // Both callers are the organizer (userId) — authorization must pass so the race is on the
        // single live slot, not on the 403 gate.
        var results = await Task.WhenAll(
            GoLiveAsync(sessionId1, userId),
            GoLiveAsync(sessionId2, userId));

        // ── Assert ──────────────────────────────────────────────────────────
        var succeeded = results.Where(r => r.Exception is null).ToList();
        var failed = results.Where(r => r.Exception is not null).ToList();

        succeeded.Should().HaveCount(1, "exactly one go-live may take the night's single live slot");
        failed.Should().HaveCount(1, "the losing go-live must be rejected, not silently succeed");

        // The loser maps to a 409 (MaxLiveSessionsExceededException extends ConflictException) — never a raw 500.
        var loserEx = failed[0].Exception!;
        loserEx.Should().BeAssignableTo<ConflictException>(
            "the losing go-live must map to HTTP 409 (max-live guard / xmin race / unique-index violation), not a 500");
        loserEx.Should().NotBeAssignableTo<DbUpdateException>(
            "a raw EF write exception would surface as an unhandled 500 — it must be caught and re-mapped");

        // DB truth: exactly ONE InProgress link for the night; the other stays Pending (no double-live).
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var links = await verifyDb.GameNightSessions
            .AsNoTracking()
            .Where(s => s.GameNightEventId == nightId)
            .ToListAsync();

        links.Count(s => s.Status == nameof(GameNightSessionStatus.InProgress))
            .Should().Be(1, "no more than one session may be live in a game night at a time (invariante #10)");
        links.Count(s => s.Status == nameof(GameNightSessionStatus.Pending))
            .Should().Be(1, "the blocked draft must remain Pending — its promotion rolled back");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid SessionId, Exception? Exception)> GoLiveAsync(Guid sessionId, Guid organizerUserId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
            await mediator.Send(new GoLiveSessionCommand(sessionId, organizerUserId));
            return (sessionId, null);
        }
        catch (Exception ex)
        {
            return (sessionId, ex);
        }
    }

    private async Task<Guid> CreateStandaloneSessionAsync(Guid userId, Guid gameId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var result = await mediator.Send(new CreateSessionCommand(
            UserId: userId,
            GameId: gameId,
            SessionType: "GameSpecific",
            SessionDate: DateTime.UtcNow,
            Location: null,
            Participants: new List<ParticipantDto>
            {
                new() { Id = Guid.NewGuid(), UserId = userId, DisplayName = "Host", IsOwner = true, JoinOrder = 0 }
            },
            SkipGameNightEnvelope: true,
            SkipKbReadinessGate: true));
        return result.SessionId;
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"golive-{userId:N}@test.local",
            DisplayName = "Go-Live Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return userId;
    }

    private static async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = $"Go-Live Test Game {gameId:N}",
            YearPublished = 2024,
            MinPlayers = 1,
            MaxPlayers = 4,
            PlayingTimeMinutes = 60,
            MinAge = 10,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Description = string.Empty,
            CreatedBy = Guid.NewGuid(),
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return gameId;
    }
}
