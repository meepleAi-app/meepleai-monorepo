using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.GameManagement;

/// <summary>
/// Integration tests for the per-session participant authorization filter (#2573),
/// proving end-to-end that RequireLiveSessionParticipant blocks non-participants with 403,
/// returns 404 before 403 for unknown sessions, 401 for anonymous callers, and lets the
/// creator/active participant through. Covers representative write + read endpoints; the
/// per-handler authz breadth is unit-tested in LiveGameSessionAuthorizationTests /
/// GetLiveSessionParticipantContextQueryHandlerTests / RequireLiveSessionParticipantFilterTests.
///
/// Mirrors LiveSessionDiaryEndpointTests (Testcontainers fixture, cookie auth, IMediator setup).
/// </summary>
[Collection("Integration-GroupB")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "2573")]
public sealed class LiveSessionParticipantAuthzEndpointTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"live_authz_{Guid.NewGuid():N}";
    private Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> _factory = null!;

    public LiveSessionParticipantAuthzEndpointTests(SharedTestcontainersFixture fixture)
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
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    // ── Write 403s ──────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /notes returns 403 for an authenticated non-participant")]
    public async Task Write_Notes_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/live-sessions/{sessionId}/notes", tokenB);
        request.Content = JsonContent.Create(new { notes = "intruder" });

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /scores returns 403 for an authenticated non-participant")]
    public async Task Write_RecordScore_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/live-sessions/{sessionId}/scores", tokenB);
        request.Content = JsonContent.Create(new { playerId = Guid.NewGuid(), round = 1, dimension = "points", value = 0 });

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "POST /complete returns 403 for an authenticated non-participant")]
    public async Task Write_Complete_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/live-sessions/{sessionId}/complete", tokenB);

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Read 403s ───────────────────────────────────────────────────────────────

    [Fact(DisplayName = "GET /{sessionId} returns 403 for an authenticated non-participant")]
    public async Task Read_GetSession_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/live-sessions/{sessionId}", tokenB);

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(DisplayName = "GET /resume-context returns 403 for an authenticated non-participant")]
    public async Task Read_ResumeContext_NonParticipant_Returns403()
    {
        var (_, sessionId) = await CreateCreatorSessionAsync();
        var (clientB, tokenB) = await CreateOtherUserClientAsync();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/live-sessions/{sessionId}/resume-context", tokenB);

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    // ── Positive path ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /notes succeeds (204) for the session creator")]
    public async Task Write_Notes_Creator_Succeeds()
    {
        var (client, sessionId) = await CreateCreatorSessionAsync();

        var token = client.DefaultRequestHeaders.GetValues("X-Test-Session-Token").First();
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/live-sessions/{sessionId}/notes", token);
        request.Content = JsonContent.Create(new { notes = "session notes by the owner" });

        var response = await client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "the creator is a participant and the session is not Completed");
    }

    // ── 404 before 403 ────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /notes on a nonexistent session returns 404 (not 403)")]
    public async Task Write_Notes_NonexistentSession_Returns404()
    {
        var (clientB, tokenB) = await CreateOtherUserClientAsync();
        var unknownSessionId = Guid.NewGuid();

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/live-sessions/{unknownSessionId}/notes", tokenB);
        request.Content = JsonContent.Create(new { notes = "no such session" });

        var response = await clientB.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "a missing session must yield 404 before the participant check produces 403");
    }

    // ── 401 anonymous ─────────────────────────────────────────────────────────────

    [Fact(DisplayName = "PUT /notes without a session cookie returns 401")]
    public async Task Write_Notes_Unauthenticated_Returns401()
    {
        var anon = _factory.CreateClient();
        var sessionId = Guid.NewGuid();

        var response = await anon.PutAsJsonAsync(
            $"/api/v1/live-sessions/{sessionId}/notes", new { notes = "anon" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Removed player loses access (#2561 IsActive semantics) ─────────────────────

    [Fact(DisplayName = "Removed linked player gets 403 on a write endpoint")]
    public async Task Write_RemovedPlayer_Returns403()
    {
        // Arrange: creator owns the session; user B is added as a linked player, then removed.
        var (_, sessionId) = await CreateCreatorSessionAsync();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userBId = await SeedUserAsync(db);
        var (_, tokenB) = await TestSessionHelper.CreateUserSessionAsync(db, userBId);

        var playerId = await mediator.Send(new AddPlayerToLiveSessionCommand(
            SessionId: sessionId, DisplayName: "B", Color: PlayerColor.Blue,
            UserId: userBId, Role: null, AvatarUrl: null));
        await mediator.Send(new RemovePlayerFromLiveSessionCommand(sessionId, playerId));

        var clientB = _factory.CreateClient();
        clientB.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={tokenB}");

        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Put, $"/api/v1/live-sessions/{sessionId}/notes", tokenB);
        request.Content = JsonContent.Create(new { notes = "I was removed" });

        // Act
        var response = await clientB.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a removed (deactivated) player loses participant access per #2561");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────

    /// <summary>Seeds a user + session, creates a LiveGameSession owned by that user, returns an authenticated client.</summary>
    private async Task<(HttpClient Client, Guid SessionId)> CreateCreatorSessionAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var userId = await SeedUserAsync(db);
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db, userId);

        var sessionId = await mediator.Send(new CreateLiveSessionCommand(
            UserId: userId, GameName: "Authz Test Game", GameId: null));

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        client.DefaultRequestHeaders.Add("X-Test-Session-Token", token);
        return (client, sessionId);
    }

    /// <summary>Creates an authenticated client for a different user who is NOT a participant of any session.</summary>
    private async Task<(HttpClient Client, string Token)> CreateOtherUserClientAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateUserSessionAsync(db);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Cookie", $"{TestSessionHelper.SessionCookieName}={token}");
        return (client, token);
    }

    private static async Task<Guid> SeedUserAsync(MeepleAiDbContext db)
    {
        var userId = Guid.NewGuid();
        db.Users.Add(new UserEntity
        {
            Id = userId,
            Email = $"authz-test-{userId:N}@test.local",
            DisplayName = "Authz Test User",
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
}
