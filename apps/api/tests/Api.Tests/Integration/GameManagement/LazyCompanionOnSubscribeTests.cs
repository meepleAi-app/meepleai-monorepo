using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for SP5-c (#2600) Task 2 — lazy companion provisioning on first
/// <c>GET /api/v1/live-sessions/{id}/stream</c> subscribe.
///
/// Strategy: directly insert legacy <see cref="LiveGameSessionEntity"/> rows with
/// <c>TrackingSessionId == null</c> to simulate pre-SP0 rows, then assert that a
/// subscribe creates (or does not create) the companion as per guard semantics.
///
/// All tests use <see cref="HttpCompletionOption.ResponseHeadersRead"/> to avoid
/// blocking on the infinite SSE body.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LazyCompanionOnSubscribeTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"lazy_companion_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public LazyCompanionOnSubscribeTests(SharedTestcontainersFixture fixture)
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
        await db.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null)
            await _factory.DisposeAsync();

        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 1: GameId-backed legacy session (TrackingSessionId == null)
    //         → subscribe → companion created and persisted.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Subscribe to GameId-backed legacy session creates companion and persists TrackingSessionId")]
    public async Task Subscribe_GameIdBacked_NullCompanion_CreatesCompanion()
    {
        // Arrange — seed a user + shared game, then insert a LEGACY live session
        // (TrackingSessionId == null) that has a GameId. This simulates a pre-SP0 row.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        // Insert a legacy live session row directly (bypassing CreateLiveSessionCommand
        // which would trigger the SP0 Saga and create a companion automatically).
        var sessionId = Guid.NewGuid();
        var legacySession = BuildLegacySessionEntity(sessionId, userId, gameId, trackingSessionId: null);
        db.LiveGameSessions.Add(legacySession);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // Verify setup: no companion yet
        var before = await db.LiveGameSessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        before.TrackingSessionId.Should().BeNull("pre-condition: legacy row has no companion");

        // Create an authenticated HTTP client
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act — subscribe (ResponseHeadersRead avoids blocking on the infinite SSE body)
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert — request succeeded
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — NO X-Warning-Code: stream-not-linked on the subscribe that just linked the session.
        // This is the stale-header fix (final-review Important finding): the endpoint must use the
        // POST-ensure result, not the stale ctx.HasCompanion, for the warning decision.
        resp.Headers.Should().NotContainKey("X-Warning-Code",
            "GameId-backed session was just linked by EnsureCompanionCommand on this very subscribe; " +
            "emitting stream-not-linked would mislead the client into thinking the stream is empty");

        // Assert — companion persisted after subscribe
        // Use a fresh DbContext scope to avoid stale tracking.
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var after = await verifyDb.LiveGameSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == sessionId);

        after.TrackingSessionId.Should().NotBeNull(
            "EnsureCompanionCommand must have created and persisted a companion for the legacy session");

        // Assert — the companion Session row actually exists in SessionTracking
        var companionExists = await verifyDb.SessionTrackingSessions
            .AsNoTracking()
            .AnyAsync(s => s.Id == after.TrackingSessionId!.Value);

        companionExists.Should().BeTrue(
            "the companion SessionTracking.Session row must exist after lazy creation");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 2: Free-form session (GameId == null) — EnsureCompanionCommand no-ops.
    //         TrackingSessionId stays null; request still returns 200.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Subscribe to free-form session (GameId=null) is a no-op: TrackingSessionId stays null")]
    public async Task Subscribe_FreeForm_NoGameId_DoesNotCreateCompanion()
    {
        // Arrange — seed a user, insert a free-form live session (no GameId, no companion)
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);

        var sessionId = Guid.NewGuid();
        var freeFormSession = BuildLegacySessionEntity(sessionId, userId, gameId: null, trackingSessionId: null);
        db.LiveGameSessions.Add(freeFormSession);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert — request still succeeds (free-form is allowed, just no domain events forwarded)
        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Assert — X-Warning-Code is present (free-form: stream-not-linked is correct behaviour)
        resp.Headers.Should().ContainKey("X-Warning-Code");
        resp.Headers.GetValues("X-Warning-Code").Should().Contain("stream-not-linked");

        // Assert — TrackingSessionId is STILL null (command must not create a companion for free-form)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var after = await verifyDb.LiveGameSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == sessionId);

        after.TrackingSessionId.Should().BeNull(
            "EnsureCompanionCommand must be a no-op for free-form sessions (GameId == null)");

        // Assert — no companion row was created
        var companionCount = await verifyDb.SessionTrackingSessions.AsNoTracking().CountAsync();
        companionCount.Should().Be(0, "no companion must be created for a free-form session");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 3: Session that already has a companion → subscribe → same TrackingSessionId
    //         (no new companion, no orphan).
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Subscribe to session that already has a companion leaves TrackingSessionId unchanged")]
    public async Task Subscribe_AlreadyHasCompanion_CompanionIsUnchanged()
    {
        // Arrange — create a session that already has a companion (i.e. SP0 Saga ran normally).
        // We insert the session entity with a pre-existing TrackingSessionId and a matching
        // SessionTracking.Session row so the FK and assertion both work.
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        var existingCompanionId = Guid.NewGuid();
        await SeedCompanionSessionAsync(db, existingCompanionId, userId, gameId);

        var sessionId = Guid.NewGuid();
        var sessionWithCompanion = BuildLegacySessionEntity(
            sessionId, userId, gameId, trackingSessionId: existingCompanionId);
        db.LiveGameSessions.Add(sessionWithCompanion);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

        // Act — subscribe
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert — request succeeds, no warning
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.Should().NotContainKey("X-Warning-Code",
            "session already has a companion — no warning should be emitted");

        // Assert — TrackingSessionId is STILL the original companion id (not replaced)
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var after = await verifyDb.LiveGameSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == sessionId);

        after.TrackingSessionId.Should().Be(existingCompanionId,
            "the existing companion must not be overwritten by a second EnsureCompanionCommand");

        // Assert — STILL exactly one companion row
        var companionCount = await verifyDb.SessionTrackingSessions
            .AsNoTracking()
            .CountAsync(s => s.Id == existingCompanionId);

        companionCount.Should().Be(1, "the pre-existing companion row must still exist and not be duplicated");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Test 4: Concurrency — two concurrent subscribes → exactly ONE companion row.
    //
    // NOTE on feasibility: The concurrency test requires two concurrent HTTP requests
    // to race on the same null-TrackingSessionId row. The xmin optimistic-concurrency
    // mechanism (ADR-060) means only one SaveChanges wins; the loser catches
    // DbUpdateConcurrencyException, re-fetches, sees TrackingSessionId != null, and
    // returns idempotently. Both requests return 200.
    //
    // In the in-process TestHost the concurrent requests DO run concurrently (different
    // Task contexts with their own DI scopes), so this test is valid.
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Two concurrent subscribes to null-companion session create exactly one companion (race-safe)")]
    public async Task Subscribe_Concurrent_CreatesExactlyOneCompanion()
    {
        // Arrange — seed a legacy session (null companion) with a GameId
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        var sessionId = Guid.NewGuid();
        var legacySession = BuildLegacySessionEntity(sessionId, userId, gameId, trackingSessionId: null);
        db.LiveGameSessions.Add(legacySession);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        // Create two independent clients (independent DI scopes, no shared state)
        var clientA = _factory.CreateClient();
        clientA.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        // Act — fire both subscribes concurrently
        var taskA = clientA.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);
        var taskB = clientB.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        var results = await Task.WhenAll(taskA, taskB);

        // Assert — both requests succeeded
        results[0].StatusCode.Should().Be(HttpStatusCode.OK,
            "first concurrent subscriber must succeed");
        results[1].StatusCode.Should().Be(HttpStatusCode.OK,
            "second concurrent subscriber must succeed (idempotent race resolution)");

        // Dispose responses to release connections
        results[0].Dispose();
        results[1].Dispose();

        // Assert — exactly ONE companion row in SessionTracking
        await using var verifyScope = _factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var after = await verifyDb.LiveGameSessions
            .AsNoTracking()
            .SingleAsync(s => s.Id == sessionId);

        after.TrackingSessionId.Should().NotBeNull(
            "at least one of the concurrent subscribers must have created the companion");

        var companionCount = await verifyDb.SessionTrackingSessions
            .AsNoTracking()
            .CountAsync(s => s.Id == after.TrackingSessionId!.Value);

        companionCount.Should().Be(1,
            "the xmin optimistic-concurrency guard must ensure exactly ONE companion is created " +
            "regardless of how many concurrent subscribers raced");

        // Overall companion count across ALL sessions must also be 1 (no orphans)
        var totalCompanions = await verifyDb.SessionTrackingSessions.AsNoTracking().CountAsync();
        totalCompanions.Should().Be(1,
            "the losing concurrent EnsureCompanionCommand must not have committed an orphaned companion");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a minimal <see cref="LiveGameSessionEntity"/> that simulates a pre-SP0 "legacy" row:
    /// it has a <paramref name="gameId"/> but <c>TrackingSessionId == null</c>.
    /// Inserted directly into the DB to bypass <c>CreateLiveSessionCommand</c> (which would trigger
    /// the SP0 Saga and auto-create the companion).
    /// </summary>
    private static LiveGameSessionEntity BuildLegacySessionEntity(
        Guid sessionId,
        Guid userId,
        Guid? gameId,
        Guid? trackingSessionId)
    {
        var now = DateTime.UtcNow;
        return new LiveGameSessionEntity
        {
            Id = sessionId,
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            GameId = gameId,
            GameName = "Legacy Test Game",
            CreatedByUserId = userId,
            Visibility = 0,  // Private
            Status = 0,       // Created
            CreatedAt = now,
            UpdatedAt = now,
            CurrentTurnIndex = 0,
            CurrentPhaseIndex = 0,
            TurnAdvancePolicy = 0,  // Manual
            AgentMode = 0,           // None
            // #3633: qui c'era `{"type":0,"dimensions":[…]}`, un formato che l'applicazione non
            // produce e non ha mai prodotto — `LiveGameSessionMapper.SerializeScoringConfig` scrive
            // `enabledDimensions` / `dimensionUnits`. Quel JSON deserializzava in un DTO con
            // entrambi i campi a null e il costruttore di SessionScoringConfig lanciava
            // ArgumentNullException, che il middleware traduce in 400: tutti e quattro i test di
            // questa classe fallivano sulla `GET /stream` prima ancora di arrivare alla logica del
            // companion, con un messaggio («Invalid request parameters») che non diceva nulla.
            //
            // Formato reale, prodotto dal serializzatore del mapper:
            ScoringConfigJson = """{"enabledDimensions":["Points"],"dimensionUnits":{"Points":"pt"}}""",
            TrackingSessionId = trackingSessionId
        };
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"lazy-companion-{userId:N}@test.local",
            DisplayName = "Lazy Companion Test User",
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
            Title = $"Lazy Companion Test Game {gameId:N}",
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

    /// <summary>
    /// Seeds a minimal <c>SessionTracking.Session</c> row to act as a pre-existing companion.
    /// Used by Test 3 to verify that an already-companion session is left unchanged.
    /// String-enum values ("GameSpecific", "Active") match <see cref="SessionMapper.ToEntity"/>
    /// which calls <c>domain.SessionType.ToString()</c> / <c>domain.Status.ToString()</c>.
    /// </summary>
    private static async Task SeedCompanionSessionAsync(
        MeepleAiDbContext db,
        Guid companionId,
        Guid userId,
        Guid gameId)
    {
        var now = DateTime.UtcNow;
        db.SessionTrackingSessions.Add(new Api.Infrastructure.Entities.SessionTracking.SessionEntity
        {
            Id = companionId,
            UserId = userId,
            GameId = gameId,
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            SessionType = "GameSpecific",
            Status = "Active",
            SessionDate = now,
            ScoreData = "{}",
            ScoringType = "Points",
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = userId,
            IsDeleted = false
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
