using System.Net;
using Api.Infrastructure;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Endpoints;

/// <summary>
/// HTTP-layer IDOR tests for the by-<c>sessionId</c> chat-session endpoints.
/// <para>
/// GET/DELETE <c>/chat/sessions/{sessionId}</c>, POST <c>/rename</c> and POST <c>/messages</c>
/// are keyed purely by the route <c>sessionId</c>. They must derive the caller identity from
/// the authenticated principal and refuse to read, delete, rename, or append to a chat session
/// owned by another user. A non-owner must receive the same 404 as for a non-existent session
/// (no existence disclosure), mirroring the collection-endpoint hardening (IDOR #3120).
/// </para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class ChatSessionByIdEndpointsIdorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _otherClient = null!;
    private Guid _ownerId;
    private string _ownerToken = null!;
    private string _otherToken = null!;
    private Guid _sessionId;

    public ChatSessionByIdEndpointsIdorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"chatsession_byid_idor_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _otherToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        // ChatSessions.GameId FKs the SharedGame catalog.
        var sharedGameId = await TestSessionHelper.SeedSharedGameAsync(db, "IDOR Chat Game");
        var chatSession = new ChatSessionEntity
        {
            Id = Guid.NewGuid(),
            UserId = _ownerId,
            GameId = sharedGameId,
            Title = "Owner private chat",
            CreatedAt = DateTime.UtcNow,
            LastMessageAt = DateTime.UtcNow,
        };
        db.ChatSessions.Add(chatSession);
        await db.SaveChangesAsync();
        _sessionId = chatSession.Id;

        _ownerClient = _factory.CreateClient();
        _otherClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _otherClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private string SessionUrl => $"/api/v1/chat/sessions/{_sessionId}";

    // ── GET ───────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task GetSession_AsOwner_ReturnsSession()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, SessionUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task GetSession_AsOtherUser_Returns404()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, SessionUrl, _otherToken));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a non-owner must not be able to read another user's chat session (nor its UserId)");
    }

    // ── DELETE ──────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task DeleteSession_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Delete, SessionUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact(Timeout = 90_000)]
    public async Task DeleteSession_AsOtherUser_Returns404()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Delete, SessionUrl, _otherToken));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a non-owner must not be able to delete another user's chat session");

        // The session must still exist after the blocked delete.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (await db.ChatSessions.AnyAsync(s => s.Id == _sessionId)).Should().BeTrue();
    }

    // ── RENAME ──────────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task RenameSession_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{SessionUrl}/rename", _ownerToken, new { Title = "Owner rename" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task RenameSession_AsOtherUser_Returns404()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{SessionUrl}/rename", _otherToken, new { Title = "Hacked title" }));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a non-owner must not be able to rename another user's chat session");

        // The title must be untouched after the blocked rename.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var session = await db.ChatSessions.AsNoTracking().FirstAsync(s => s.Id == _sessionId);
        session.Title.Should().Be("Owner private chat");
    }

    // ── ADD MESSAGE ─────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task AddMessage_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{SessionUrl}/messages", _ownerToken,
                new { Role = "user", Content = "hello" }));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact(Timeout = 90_000)]
    public async Task AddMessage_AsOtherUser_Returns404()
    {
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{SessionUrl}/messages", _otherToken,
                new { Role = "user", Content = "injected" }));

        response.StatusCode.Should().Be(
            HttpStatusCode.NotFound,
            "a non-owner must not be able to inject messages into another user's chat session");
    }
}
