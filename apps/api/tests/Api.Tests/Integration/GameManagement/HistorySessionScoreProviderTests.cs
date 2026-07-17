using System.Text.Json.Nodes;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for #3080 — <see cref="IHistorySessionScoreProvider"/> resolves the
/// polymorphic score for a history GameSession by bridging, on a real Postgres:
/// GameSession ← LiveGameSession.CorrelatedGameSessionId, and
/// LiveGameSession.TrackingSessionId → SessionTracking.Session (score owner).
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class HistorySessionScoreProviderTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"history_score_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public HistorySessionScoreProviderTests(SharedTestcontainersFixture fixture)
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

    // ──────────────────────────────────────────────────────────────────────────
    // The correlated live session bridges the completed GameSession to the
    // SessionTracking.Session that owns scoring_type + score_data → the provider
    // must return the Points payload keyed by the GameSession id.
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetScoresAsync_ResolvesPointsScore_ViaLiveSessionBridge()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);
        var gameSessionId = await SeedCompletedGameSessionAsync(db, gameId, userId);

        const string scoreData =
            "{\"scores\":[{\"playerId\":\"11111111-1111-1111-1111-111111111111\",\"points\":42},"
            + "{\"playerId\":\"22222222-2222-2222-2222-222222222222\",\"points\":30}]}";
        var trackingSessionId = await SeedTrackingSessionAsync(db, userId, gameId, "Points", scoreData);
        await SeedLiveBridgeAsync(db, userId, gameSessionId, trackingSessionId);

        var provider = scope.ServiceProvider.GetRequiredService<IHistorySessionScoreProvider>();

        // Act
        var result = await provider.GetScoresAsync(
            new[] { gameSessionId }, TestContext.Current.CancellationToken);

        // Assert. score_data is a jsonb column: Postgres normalizes whitespace and
        // object-key order on storage, so compare the round-tripped payload
        // semantically (this is fine for the FE, which JSON.parses it) rather than
        // byte-for-byte.
        result.Should().ContainKey(gameSessionId);
        result[gameSessionId].ScoringType.Should().Be("Points");
        JsonNode.DeepEquals(
                JsonNode.Parse(result[gameSessionId].ScoreData),
                JsonNode.Parse(scoreData))
            .Should().BeTrue("the round-tripped jsonb score payload must be semantically equal to what was stored");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // A GameSession with no correlated live session (e.g. legacy / non-live play)
    // resolves to no score → absent from the result dictionary (renders as '—').
    // ──────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task GetScoresAsync_OmitsSession_WhenNoCorrelatedLiveSession()
    {
        // Arrange
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);
        var gameSessionId = await SeedCompletedGameSessionAsync(db, gameId, userId);
        // No LiveGameSession / SessionTracking.Session correlated to this game session.

        var provider = scope.ServiceProvider.GetRequiredService<IHistorySessionScoreProvider>();

        // Act
        var result = await provider.GetScoresAsync(
            new[] { gameSessionId }, TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotContainKey(gameSessionId);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"hist-score-{userId:N}@test.local",
            DisplayName = "History Score Test User",
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
            Title = $"History Score Test Game {gameId:N}",
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

    private static async Task<Guid> SeedCompletedGameSessionAsync(MeepleAiDbContext db, Guid gameId, Guid userId)
    {
        var id = Guid.NewGuid();
        db.GameSessions.Add(new GameSessionEntity
        {
            Id = id,
            GameId = gameId,
            CreatedByUserId = userId,
            Status = "Completed",
            StartedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow,
            WinnerName = "Alice",
            Notes = null,
            PlayersJson = "[]"
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return id;
    }

    private static async Task<Guid> SeedTrackingSessionAsync(
        MeepleAiDbContext db, Guid userId, Guid gameId, string scoringType, string scoreData)
    {
        var id = Guid.NewGuid();
        db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = id,
            UserId = userId,
            GameId = gameId,
            SessionCode = "SCR001",
            SessionType = "Standard",
            Status = "Completed",
            SessionDate = DateTime.UtcNow,
            ScoringType = scoringType,
            ScoreData = scoreData,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return id;
    }

    private static async Task SeedLiveBridgeAsync(
        MeepleAiDbContext db, Guid userId, Guid correlatedGameSessionId, Guid trackingSessionId)
    {
        db.LiveGameSessions.Add(new LiveGameSessionEntity
        {
            Id = Guid.NewGuid(),
            SessionCode = "LIVE01",
            GameId = null, // free-form → avoids an extra SharedGame FK
            GameName = "Test Live Session",
            CreatedByUserId = userId,
            Visibility = 0,
            Status = 4, // Completed
            ScoringConfigJson = "{}",
            CreatedAt = DateTime.UtcNow.AddHours(-1),
            UpdatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            TrackingSessionId = trackingSessionId,
            CorrelatedGameSessionId = correlatedGameSessionId
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
    }
}
