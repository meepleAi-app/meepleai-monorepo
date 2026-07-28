using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
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
/// HTTP-layer IDOR tests for the SessionTracking write-command endpoints
/// (<c>SessionCommandEndpoints</c>): finalize, roll dice, add note, add participant,
/// and the legacy per-participant score update.
///
/// <para>Threat model: a <c>sessionId</c> is discoverable via unauthenticated read paths
/// (<c>GET /game-sessions/code/{code}</c>, <c>/scoreboard</c>). Before this fix any
/// authenticated user could finalize (destroying the victim's live session and triggering
/// the KB cleanup + game-night link close cascade), inject scores/notes/participants, or
/// roll dice on a session they neither own nor participate in.</para>
///
/// <para>The endpoints MUST derive the acting identity from the authenticated principal and
/// the handlers MUST reject a caller who is neither the session owner nor a registered
/// (User-linked) participant with HTTP 403. This mirrors the existing IDOR guard on
/// <c>UpdateSessionScoresCommandHandler</c> (PUT /scores-polymorphic).</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Concern", "Security")]
public sealed class SessionCommandEndpointsIdorIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _attackerClient = null!;
    private HttpClient _memberClient = null!;
    private string _ownerToken = null!;
    private string _attackerToken = null!;
    private string _memberToken = null!;
    private Guid _ownerId;
    private Guid _memberId;
    private Guid _sessionId;
    private Guid _ownerParticipantId;
    private Guid _memberParticipantId;

    public SessionCommandEndpointsIdorIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"sessioncmd_idor_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var connectionString = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(connectionString);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _attackerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_memberId, _memberToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, "IDOR Command Game");

        // Seed an Active SessionTracking session owned by the owner, with the owner AND a
        // second User-linked member as participants. The member exercises the "participant"
        // authorization clause; the attacker is neither owner nor participant.
        _sessionId = Guid.NewGuid();
        _ownerParticipantId = Guid.NewGuid();
        _memberParticipantId = Guid.NewGuid();

        db.SessionTrackingSessions.Add(new SessionEntity
        {
            Id = _sessionId,
            UserId = _ownerId,
            GameId = gameId,
            Status = "Active",
            SessionCode = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant(),
            SessionType = "Generic",
            SessionDate = DateTime.UtcNow.AddMinutes(-30),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = _ownerId,
            Participants = new List<ParticipantEntity>
            {
                new()
                {
                    Id = _ownerParticipantId,
                    SessionId = _sessionId,
                    UserId = _ownerId,
                    DisplayName = "Owner",
                    IsOwner = true,
                    JoinOrder = 1,
                    CreatedAt = DateTime.UtcNow,
                },
                new()
                {
                    Id = _memberParticipantId,
                    SessionId = _sessionId,
                    UserId = _memberId,
                    DisplayName = "Member",
                    IsOwner = false,
                    JoinOrder = 2,
                    CreatedAt = DateTime.UtcNow,
                },
            },
        });
        await db.SaveChangesAsync();

        _ownerClient = _factory.CreateClient();
        _attackerClient = _factory.CreateClient();
        _memberClient = _factory.CreateClient();
    }

    public async ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _attackerClient?.Dispose();
        _memberClient?.Dispose();
        _factory?.Dispose();
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

    private string BaseUrl => $"/api/v1/game-sessions/{_sessionId}";

    private object FinalizeBody => new
    {
        sessionId = _sessionId,
        finalRanks = new Dictionary<Guid, int>
        {
            [_ownerParticipantId] = 1,
            [_memberParticipantId] = 2,
        },
    };

    private object DiceBody(Guid participantId) => new
    {
        sessionId = _sessionId,
        participantId,
        formula = "1d6",
        label = (string?)null,
    };

    private object NoteBody(Guid participantId) => new
    {
        sessionId = _sessionId,
        participantId,
        noteType = "Shared",
        templateKey = (string?)null,
        content = "note content",
        isHidden = false,
    };

    private object AddParticipantBody => new
    {
        sessionId = _sessionId,
        displayName = "Injected Player",
        userId = (Guid?)null,
    };

    private object ScoreBody(Guid participantId) => new
    {
        sessionId = _sessionId,
        participantId,
        roundNumber = 1,
        category = (string?)null,
        scoreValue = 10m,
    };

    // ── Finalize ──────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task Finalize_AsAttacker_IsForbidden()
    {
        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/finalize", _attackerToken, FinalizeBody));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-owner/non-participant must not finalize (destroy) another user's session");
    }

    [Fact(Timeout = 90_000)]
    public async Task Finalize_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/finalize", _ownerToken, FinalizeBody));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the session owner must be allowed to finalize");
    }

    // ── Roll dice ─────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task RollDice_AsAttacker_IsForbidden()
    {
        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/dice", _attackerToken, DiceBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-owner/non-participant must not roll dice in another user's session");
    }

    [Fact(Timeout = 90_000)]
    public async Task RollDice_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/dice", _ownerToken, DiceBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the session owner must be allowed to roll dice");
    }

    // ── Add note ──────────────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task AddNote_AsAttacker_IsForbidden()
    {
        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/notes", _attackerToken, NoteBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-owner/non-participant must not add notes to another user's session");
    }

    [Fact(Timeout = 90_000)]
    public async Task AddNote_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/notes", _ownerToken, NoteBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the session owner must be allowed to add notes");
    }

    [Fact(Timeout = 90_000)]
    public async Task AddNote_AsMemberParticipant_Succeeds()
    {
        // Exercises the "registered participant" authorization clause: the member is not the
        // owner but is a User-linked participant, so they must be allowed to act.
        var response = await _memberClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/notes", _memberToken, NoteBody(_memberParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "a registered (User-linked) participant must be allowed to add notes");
    }

    // ── Add participant ───────────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task AddParticipant_AsAttacker_IsForbidden()
    {
        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/participants", _attackerToken, AddParticipantBody));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-owner/non-participant must not add participants to another user's session");
    }

    [Fact(Timeout = 90_000)]
    public async Task AddParticipant_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"{BaseUrl}/participants", _ownerToken, AddParticipantBody));

        response.StatusCode.Should().Be(HttpStatusCode.Created,
            "the session owner must be allowed to add participants");
    }

    // ── Legacy update score ───────────────────────────────────────────────────

    [Fact(Timeout = 90_000)]
    public async Task UpdateScore_AsAttacker_IsForbidden()
    {
        var response = await _attackerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Put, $"{BaseUrl}/scores", _attackerToken, ScoreBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "a non-owner/non-participant must not inject scores into another user's session");
    }

    [Fact(Timeout = 90_000)]
    public async Task UpdateScore_AsOwner_Succeeds()
    {
        var response = await _ownerClient.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Put, $"{BaseUrl}/scores", _ownerToken, ScoreBody(_ownerParticipantId)));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "the session owner must be allowed to update scores");
    }
}
