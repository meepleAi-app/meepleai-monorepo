using System.Net;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for the native SSE stream endpoint
/// <c>GET /api/v1/live-sessions/{sessionId}/stream</c> (Issue #2561 SP2 T4).
///
/// Test strategy:
/// - Use <see cref="HttpCompletionOption.ResponseHeadersRead"/> so we receive the response
///   status + headers without draining the infinite SSE body — avoids test hangs.
/// - A short <see cref="CancellationTokenSource"/> timeout guards against edge-case hangs.
/// - Asserting status code + Content-Type + X-Warning-Code headers is sufficient for these ACs
///   per the task brief ("asserting response HEADERS via ResponseHeadersRead is sufficient").
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class LiveSessionStreamEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_stream_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public LiveSessionStreamEndpointTests(SharedTestcontainersFixture fixture)
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
    // AC-1: Authorized user, session WITH companion → 200 + text/event-stream
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Stream returns 200 text/event-stream for authorized user whose session has a companion")]
    public async Task Stream_returns_event_stream_for_authorized_user()
    {
        // Arrange
        var (client, sessionId) = await CreateLiveSessionWithCompanionAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act — read only headers; do NOT drain the SSE body (would block forever)
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Content.Headers.ContentType?.MediaType.Should().Be("text/event-stream");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC-2: Authorized user, session WITHOUT companion → 200 + X-Warning-Code
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Stream returns 200 with X-Warning-Code stream-not-linked when session has no companion")]
    public async Task Stream_warns_when_session_has_no_companion()
    {
        // Arrange — session created WITHOUT a GameId so the SP0 Saga never fires
        var (client, sessionId) = await CreateLiveSessionWithoutGameIdAsync();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        resp.Headers.Should().ContainKey("X-Warning-Code");
        resp.Headers.GetValues("X-Warning-Code").Should().Contain("stream-not-linked");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC-3: No auth → 401
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Stream returns 401 when the caller is not authenticated")]
    public async Task Stream_returns_401_when_unauthenticated()
    {
        // Arrange — unauthenticated client (no session cookie)
        var client = _factory.CreateClient();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act — session ID is irrelevant; auth check happens first
        using var resp = await client.GetAsync(
            $"/api/v1/live-sessions/{Guid.NewGuid()}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert — RequireAuthenticatedUser() returns 401
        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // AC-4: Authenticated user who is NOT a participant → 403
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "Stream returns 403 when the caller is authenticated but not a participant")]
    public async Task Stream_returns_403_for_non_participant()
    {
        // Arrange — create a session owned by user-A, then request as user-B
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        // User A creates the session
        var (_, sessionId) = await CreateLiveSessionWithoutGameIdAsync();

        // User B has a valid auth session but is NOT linked to that live session
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db);
        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        // Act
        using var resp = await clientB.GetAsync(
            $"/api/v1/live-sessions/{sessionId}/stream",
            HttpCompletionOption.ResponseHeadersRead,
            cts.Token);

        // Assert
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates a LiveGameSession WITH a companion (GameId → SP0 Saga creates TrackingSessionId).
    /// Returns an authenticated HttpClient + the session ID.
    /// </summary>
    private async Task<(HttpClient Client, Guid SessionId)> CreateLiveSessionWithCompanionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var gameId = await SeedSharedGameAsync(db);

        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Test Game With Companion",
            GameId: gameId));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return (client, sessionId);
    }

    /// <summary>
    /// Creates a LiveGameSession WITHOUT a GameId (no companion, TrackingSessionId == null).
    /// Returns an authenticated HttpClient + the session ID.
    /// </summary>
    private async Task<(HttpClient Client, Guid SessionId)> CreateLiveSessionWithoutGameIdAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId,
            GameName: "Test Game Without Companion",
            GameId: null));   // no GameId → no companion

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return (client, sessionId);
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"stream-test-{userId:N}@test.local",
            DisplayName = "Stream Test User",
            PasswordHash = "not-a-real-hash",
            Role = "user",
            Tier = "free",
            Status = "Active",
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();
        return userId;
    }

    private static async Task<Guid> SeedSharedGameAsync(MeepleAiDbContext db)
    {
        var gameId = Guid.NewGuid();
        db.SharedGames.Add(new SharedGameEntity
        {
            Id = gameId,
            Title = "Stream Test Game",
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
        return gameId;
    }
}
