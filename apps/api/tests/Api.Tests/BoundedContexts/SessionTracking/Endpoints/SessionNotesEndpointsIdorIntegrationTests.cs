using System.Net;
using System.Net.Http.Json;
using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Endpoints;

/// <summary>
/// HTTP-layer IDOR tests for the private-notes endpoints (Issue #3263).
/// The endpoints must derive the caller identity from the authenticated principal,
/// NOT from a client-supplied <c>requesterId</c>/<c>participantId</c>. A second
/// authenticated user must not be able to read, or act on, another participant's
/// private note by spoofing that participant's id.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "3263")]
public sealed class SessionNotesEndpointsIdorIntegrationTests : IAsyncLifetime
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

    public SessionNotesEndpointsIdorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sessionnotes_idor_{Guid.NewGuid():N}";
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

        // The private-notes SessionId FK targets GameManagement's GameSessions
        // table. Seed a SharedGame (GameSessions.GameId FK) + a GameSession.
        var sharedGameId = await TestSessionHelper.SeedSharedGameAsync(db, "IDOR Test Game");
        var gameSession = new GameSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = sharedGameId,
            CreatedByUserId = _ownerId,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            PlayersJson = "[]",
        };
        db.GameSessions.Add(gameSession);
        await db.SaveChangesAsync();
        _sessionId = gameSession.Id;

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

    private string SessionNotesUrl => $"/api/v1/game-sessions/{_sessionId}/private-notes";

    /// <summary>Owner creates a private note and returns its id.</summary>
    private async Task<Guid> SaveOwnerNoteAsync(string content)
    {
        // participantId is deliberately the owner's id so the note is owned by the
        // owner both before the fix (body-supplied) and after (auth-derived).
        var command = new { sessionId = _sessionId, participantId = _ownerId, content };
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, SessionNotesUrl, _ownerToken, command));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "seeding a note must succeed");
        var body = await response.Content.ReadFromJsonAsync<SaveNoteResponse>();
        return body!.NoteId;
    }

    [Fact(Timeout = 90_000)]
    public async Task GetNotes_AsOwner_ReturnsOwnNoteContent()
    {
        var noteId = await SaveOwnerNoteAsync("owner secret");

        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, SessionNotesUrl, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetSessionNotesResponse>();
        body!.Notes.Should().ContainSingle(n => n.Id == noteId)
            .Which.Content.Should().Be("owner secret");
    }

    [Fact(Timeout = 90_000)]
    public async Task GetNotes_AsOtherUserSpoofingRequesterId_DoesNotLeakOwnersNote()
    {
        var noteId = await SaveOwnerNoteAsync("confidential");

        // IDOR exploit: the attacker passes the victim's id as requesterId.
        var url = $"{SessionNotesUrl}?requesterId={_ownerId}";
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Get, url, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<GetSessionNotesResponse>();
        body!.Notes.Should().NotContain(
            n => n.Id == noteId,
            "identity must derive from the authenticated principal, not the client-supplied requesterId");
    }

    [Fact(Timeout = 90_000)]
    public async Task DeleteNote_AsOtherUserSpoofingParticipantId_IsForbidden()
    {
        var noteId = await SaveOwnerNoteAsync("do not delete");

        // IDOR exploit: the attacker passes the victim's id as participantId.
        var url = $"{SessionNotesUrl}/{noteId}?participantId={_ownerId}";
        var response = await _otherClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Delete, url, _otherToken));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact(Timeout = 90_000)]
    public async Task DeleteNote_AsOwner_Succeeds()
    {
        var noteId = await SaveOwnerNoteAsync("temporary");

        // The owner deletes without supplying any participant id — identity comes
        // from the authenticated principal.
        var url = $"{SessionNotesUrl}/{noteId}";
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(HttpMethod.Delete, url, _ownerToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
