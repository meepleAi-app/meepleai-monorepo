using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Migrations;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration test for the epic #3188 Slice 5 idempotent data-migration
/// <see cref="ReconcileLegacyInProgressDraftLinks"/>.
///
/// <para>Seeds the three legacy split-brain shapes directly on a fresh Testcontainers Postgres and
/// then runs the migration's EXACT reconciliation SQL (referenced via its <c>public const</c>
/// statements — zero drift between test and migration) to assert each shape is reconciled per the
/// D4 liveness rule (<c>Session.IsLive = started_at != null AND finalized_at == null</c>), plus a
/// second application to prove idempotency and a defensive max-1-live dedup scenario.</para>
///
/// <para>Mirrors the Testcontainers + full-DI factory pattern of <see cref="GoLiveSessionConcurrencyTests"/>.
/// Each test method gets its OWN isolated migrated database (<see cref="IAsyncLifetime"/> + per-instance
/// <c>_databaseName</c>), so the dedup scenario may safely drop the partial-unique index without
/// affecting the primary scenario.</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "GameManagement")]
public sealed class ReconcileLegacyInProgressDraftLinksMigrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"reconcile_inprogress_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public ReconcileLegacyInProgressDraftLinksMigrationTests(SharedTestcontainersFixture fixture)
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

    [Fact(DisplayName = "Reconcile: finalized→Completed, live→InProgress, never-live→Pending, and idempotent on re-run")]
    public async Task ReconcileSql_ResolvesEachSplitBrainShape_AndIsIdempotent()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        // Three distinct nights (the partial-unique live-slot index forbids >1 InProgress link per
        // night, so each shape lives on its own night), each carrying ONE InProgress link whose
        // link.started_at is deliberately non-null — to prove rule 3 nulls it on demotion.
        Guid userId, gameId;
        Guid finalizedLinkId, liveLinkId, neverLiveLinkId;
        var finalizedAt = DateTime.UtcNow.AddHours(-1);
        var startedAt = DateTime.UtcNow.AddHours(-2);

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            userId = await SeedUserAsync(db);
            gameId = await SeedSharedGameAsync(db);

            // Shape 1 — tracking session FINALIZED (started + finalized) → link must CLOSE.
            var finalizedSessionId = await SeedTrackingSessionAsync(db, userId, gameId, startedAt: startedAt, finalizedAt: finalizedAt);
            finalizedLinkId = await SeedNightWithInProgressLinkAsync(db, userId, gameId, finalizedSessionId, "Finalized Night");

            // Shape 2 — tracking session GENUINELY LIVE (started, not finalized) → link must STAY InProgress.
            var liveSessionId = await SeedTrackingSessionAsync(db, userId, gameId, startedAt: startedAt, finalizedAt: null);
            liveLinkId = await SeedNightWithInProgressLinkAsync(db, userId, gameId, liveSessionId, "Live Night");

            // Shape 3 — tracking session NEVER LIVE (started_at NULL, finalized_at NULL) → link must DEMOTE.
            var neverLiveSessionId = await SeedTrackingSessionAsync(db, userId, gameId, startedAt: null, finalizedAt: null);
            neverLiveLinkId = await SeedNightWithInProgressLinkAsync(db, userId, gameId, neverLiveSessionId, "Never-Live Night");
        }

        // ── Act — run the migration's exact reconciliation SQL ─────────────────
        await RunReconciliationAsync();

        // ── Assert (first pass) ───────────────────────────────────────────────
        await using (var verify = _factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            var finalized = await GetLinkAsync(db, finalizedLinkId);
            finalized.Status.Should().Be(nameof(GameNightSessionStatus.Completed),
                "an InProgress link whose tracking session is finalized must be closed (rule 1)");
            finalized.CompletedAt.Should().NotBeNull("closing the link must stamp completed_at");
            finalized.CompletedAt!.Value.UtcDateTime.Should().BeCloseTo(finalizedAt, TimeSpan.FromSeconds(1),
                "completed_at must be taken from the tracking session's finalized_at");

            var live = await GetLinkAsync(db, liveLinkId);
            live.Status.Should().Be(nameof(GameNightSessionStatus.InProgress),
                "a link whose tracking session is genuinely live must keep its live slot (rule 2)");
            live.StartedAt.Should().NotBeNull("the genuinely-live link retains its started_at");

            var neverLive = await GetLinkAsync(db, neverLiveLinkId);
            neverLive.Status.Should().Be(nameof(GameNightSessionStatus.Pending),
                "an InProgress link whose tracking session never went live must be demoted to a draft (rule 3)");
            neverLive.StartedAt.Should().BeNull("demoting a mislabeled draft must clear the link's started_at (rule 3)");
        }

        // ── Act — apply the SAME reconciliation SQL a SECOND time ──────────────
        await RunReconciliationAsync();

        // ── Assert (idempotency) — nothing changed ─────────────────────────────
        await using (var verify = _factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            (await GetLinkAsync(db, finalizedLinkId)).Status.Should().Be(nameof(GameNightSessionStatus.Completed),
                "re-running is a no-op — the finalized link stays Completed");
            (await GetLinkAsync(db, liveLinkId)).Status.Should().Be(nameof(GameNightSessionStatus.InProgress),
                "re-running is a no-op — the live link stays InProgress");
            var neverLive = await GetLinkAsync(db, neverLiveLinkId);
            neverLive.Status.Should().Be(nameof(GameNightSessionStatus.Pending),
                "re-running is a no-op — the demoted link stays Pending");
            neverLive.StartedAt.Should().BeNull("re-running is a no-op — started_at stays null");
        }
    }

    [Fact(DisplayName = "Defensive dedup: >1 InProgress on one night keeps the most-recently-started, demotes the rest")]
    public async Task ReconcileSql_WithMultipleInProgressPerNight_KeepsMostRecentDemotesRest()
    {
        // ── Arrange ──────────────────────────────────────────────────────────
        // The partial-unique index normally makes this state impossible; drop it (isolated DB) so we
        // can seed the pathological >1-InProgress shape rule 4 defends against.
        Guid userId, gameId, nightId;
        Guid olderLinkId, newerLinkId;
        var older = DateTime.UtcNow.AddHours(-3);
        var newer = DateTime.UtcNow.AddHours(-1);

        await using (var seed = _factory.Services.CreateAsyncScope())
        {
            var db = seed.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await db.Database.ExecuteSqlRawAsync(
                $"DROP INDEX IF EXISTS {GameNightSessionEntityConfigurationIndexName};");

            userId = await SeedUserAsync(db);
            gameId = await SeedSharedGameAsync(db);
            nightId = await SeedNightAsync(db, userId, gameId, "Duplicate Live Night");

            // Both sessions genuinely live (so rules 1 & 3 leave them alone), forcing rule 4 to arbitrate.
            var olderSessionId = await SeedTrackingSessionAsync(db, userId, gameId, startedAt: older, finalizedAt: null);
            var newerSessionId = await SeedTrackingSessionAsync(db, userId, gameId, startedAt: newer, finalizedAt: null);

            olderLinkId = await SeedLinkAsync(db, nightId, gameId, olderSessionId, playOrder: 1,
                status: nameof(GameNightSessionStatus.InProgress), startedAt: older);
            newerLinkId = await SeedLinkAsync(db, nightId, gameId, newerSessionId, playOrder: 2,
                status: nameof(GameNightSessionStatus.InProgress), startedAt: newer);
        }

        // ── Act ────────────────────────────────────────────────────────────────
        await RunReconciliationAsync();

        // ── Assert ───────────────────────────────────────────────────────────
        await using (var verify = _factory.Services.CreateAsyncScope())
        {
            var db = verify.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

            (await GetLinkAsync(db, newerLinkId)).Status.Should().Be(nameof(GameNightSessionStatus.InProgress),
                "the most-recently-started InProgress link keeps the single live slot (rule 4)");
            var demoted = await GetLinkAsync(db, olderLinkId);
            demoted.Status.Should().Be(nameof(GameNightSessionStatus.Pending),
                "the older duplicate InProgress link is demoted (rule 4)");
            demoted.StartedAt.Should().BeNull("the demoted duplicate has its started_at cleared (rule 4)");

            var inProgressCount = await db.GameNightSessions.AsNoTracking()
                .CountAsync(s => s.GameNightEventId == nightId && s.Status == nameof(GameNightSessionStatus.InProgress));
            inProgressCount.Should().Be(1, "exactly one live slot may remain per night after reconciliation");
        }
    }

    // ── Reconciliation runner (runs the migration's exact SQL) ─────────────────

    private async Task RunReconciliationAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.ExecuteSqlRawAsync(ReconcileLegacyInProgressDraftLinks.CloseFinalizedInProgressLinksSql);
        await db.Database.ExecuteSqlRawAsync(ReconcileLegacyInProgressDraftLinks.DemoteNeverLiveInProgressLinksSql);
        await db.Database.ExecuteSqlRawAsync(ReconcileLegacyInProgressDraftLinks.DedupeSurvivingInProgressLinksSql);
    }

    // ── Seed / read helpers ────────────────────────────────────────────────────

    // Single source of truth for the partial-unique live-slot index name (mirrors the C# const on
    // GameNightSessionEntityConfiguration.UniqueActiveIndexName).
    private const string GameNightSessionEntityConfigurationIndexName = "ix_game_night_sessions_unique_active";

    private static async Task<GameNightSessionEntity> GetLinkAsync(MeepleAiDbContext db, Guid linkId)
        => await db.GameNightSessions.AsNoTracking().SingleAsync(s => s.Id == linkId);

    private async Task<Guid> SeedNightWithInProgressLinkAsync(
        MeepleAiDbContext db, Guid userId, Guid gameId, Guid sessionId, string title)
    {
        var nightId = await SeedNightAsync(db, userId, gameId, title);
        return await SeedLinkAsync(db, nightId, gameId, sessionId, playOrder: 1,
            status: nameof(GameNightSessionStatus.InProgress), startedAt: DateTime.UtcNow.AddHours(-2));
    }

    private static async Task<Guid> SeedNightAsync(MeepleAiDbContext db, Guid userId, Guid gameId, string title)
    {
        var nightId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = nightId,
            OrganizerId = userId,
            Title = title,
            ScheduledAt = now,
            GameIdsJson = System.Text.Json.JsonSerializer.Serialize(new List<Guid> { gameId }),
            Status = nameof(GameNightStatus.Published),
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return nightId;
    }

    private static async Task<Guid> SeedLinkAsync(
        MeepleAiDbContext db, Guid nightId, Guid gameId, Guid sessionId, int playOrder, string status, DateTimeOffset? startedAt)
    {
        var linkId = Guid.NewGuid();
        db.GameNightSessions.Add(new GameNightSessionEntity
        {
            Id = linkId,
            GameNightEventId = nightId,
            SessionId = sessionId,
            GameId = gameId,
            GameTitle = "Catan",
            PlayOrder = playOrder,
            Status = status,
            StartedAt = startedAt,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return linkId;
    }

    private static async Task<Guid> SeedTrackingSessionAsync(
        MeepleAiDbContext db, Guid userId, Guid gameId, DateTime? startedAt, DateTime? finalizedAt)
    {
        var sessionId = Guid.NewGuid();
        db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = sessionId,
            UserId = userId,
            GameId = gameId,
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            SessionType = "GameSpecific",
            Status = finalizedAt is not null ? "Completed" : (startedAt is not null ? "Active" : "Planned"),
            SessionDate = DateTime.UtcNow,
            StartedAt = startedAt,
            FinalizedAt = finalizedAt,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return sessionId;
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"reconcile-{userId:N}@test.local",
            DisplayName = "Reconcile Test User",
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
            Title = $"Reconcile Test Game {gameId:N}",
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
