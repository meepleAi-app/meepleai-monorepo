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
/// Epic #3188 post-review (HIGH) — two concurrent <see cref="CreateSessionCommand"/>s attaching a
/// DRAFT to the SAME existing Published night race on the per-night play-order unique index
/// (IX_game_night_sessions_event_play_order): both read an identical <c>nightEntity.Sessions.Count</c>
/// snapshot and compute the SAME PlayOrder, so the 2nd INSERT violates the index. Post-#3188 a create
/// mints only Pending drafts (invariante #19), so the InProgress live-slot index is NOT tripped — the
/// play-order index is the sole violation. It MUST surface as a retryable 409 (ConflictException),
/// never a raw 500 (project rule #2568).
///
/// <para>Mirrors the Testcontainers + full-DI factory pattern of <c>GoLiveSessionConcurrencyTests</c>.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
public sealed class CreateSessionConcurrencyTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"create_session_concurrency_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public CreateSessionConcurrencyTests(SharedTestcontainersFixture fixture)
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

    [Fact(DisplayName = "Two concurrent draft creates on the same night: no 500, the loser 409s, no orphan link")]
    public async Task TwoConcurrentCreates_SameNight_PlayOrderCollision_MapsTo409NotRaw500()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        Guid userId;
        Guid gameId;
        var nightId = Guid.NewGuid();
        await using (var seedScope = _factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            userId = await SeedUserAsync(db);
            gameId = await SeedSharedGameAsync(db);

            var now = DateTimeOffset.UtcNow;
            db.GameNightEvents.Add(new GameNightEventEntity
            {
                Id = nightId,
                OrganizerId = userId,
                Title = "Concurrent Draft Create Night",
                ScheduledAt = now,
                GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { gameId }),
                Status = nameof(GameNightStatus.Published),
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
        }

        // ── Act — two draft creates race to attach to the SAME night ───────────
        var results = await Task.WhenAll(
            CreateDraftAsync(nightId, userId, gameId),
            CreateDraftAsync(nightId, userId, gameId));

        // ── Assert ──────────────────────────────────────────────────────────
        var succeeded = results.Where(r => r.Exception is null).ToList();
        var failed = results.Where(r => r.Exception is not null).ToList();

        // Core guarantee of the fix: the concurrent same-night PlayOrder collision NEVER surfaces as a
        // raw 500. Any loser maps to a ConflictException (→ HTTP 409), never an unmapped EF write.
        foreach (var f in failed)
        {
            f.Exception.Should().BeAssignableTo<ConflictException>(
                "a concurrent same-night PlayOrder collision must map to HTTP 409, not a raw 500");
            f.Exception.Should().NotBeAssignableTo<DbUpdateException>(
                "a raw EF write exception would surface as an unhandled 500 — it must be caught and re-mapped");
        }

        // At least one create must win a distinct PlayOrder. With two racers on one contested order at
        // most one can lose (the other commits it); the common interleaving yields exactly one 409, but
        // both may also succeed if the 2nd observed the 1st's commit and picked the next order.
        succeeded.Should().NotBeEmpty("at least one concurrent create must persist its draft");
        failed.Count.Should().BeLessThanOrEqualTo(1);

        // DB truth: every persisted link is a Pending draft (#19) with a DISTINCT PlayOrder, and the
        // count equals the number of winners — the loser's whole transaction rolled back (no orphan).
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var links = await verifyDb.GameNightSessions
            .AsNoTracking()
            .Where(s => s.GameNightEventId == nightId)
            .ToListAsync();

        links.Should().HaveCount(succeeded.Count, "only the winning creates leave a persisted link");
        links.Should().OnlyContain(
            s => s.Status == nameof(GameNightSessionStatus.Pending),
            "a direct create mints a Pending draft — going live is a separate step (Slice 2)");
        links.Select(s => s.PlayOrder).Should().OnlyHaveUniqueItems(
            "distinct PlayOrders — the collision is resolved, not persisted twice");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private async Task<(Guid? SessionId, Exception? Exception)> CreateDraftAsync(Guid nightId, Guid userId, Guid gameId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        try
        {
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
                GameNightEventId: nightId,
                SkipKbReadinessGate: true));
            return (result.SessionId, null);
        }
        catch (Exception ex)
        {
            return (null, ex);
        }
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"create-concurrency-{userId:N}@test.local",
            DisplayName = "Create Concurrency Test User",
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
            Title = $"Create Concurrency Test Game {gameId:N}",
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
