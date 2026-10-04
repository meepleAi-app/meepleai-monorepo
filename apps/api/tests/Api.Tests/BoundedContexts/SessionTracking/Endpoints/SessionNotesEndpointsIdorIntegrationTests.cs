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
/// La fixture di <see cref="SessionNotesEndpointsIdorIntegrationTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Database per test, non condiviso per classe, benche' i test non asseriscano su conteggi
/// globali: il seeding sta in <c>InitializeAsync</c>, che xUnit chiama una volta per METODO, quindi
/// un database condiviso accumulerebbe N copie di utenti, gioco e sessione.
/// </para>
/// <para>
/// Quelle copie sarebbero inerti — gli helper generano GUID freschi, le email portano un GUID, e
/// l'unico indice unico in gioco (<c>ix_shared_games_bgg_id</c>) e' filtrato su
/// <c>bgg_id IS NOT NULL</c> mentre il seeder lascia <c>BggId</c> nullo. Ma la condivisione
/// comprerebbe soltanto il clone del database (~0,11s per test, perche' il seeding si paga in ogni
/// caso) al prezzo di quella catena di tre verifiche: se un domani qualcuno rende unico il titolo,
/// o il seeder inizia a scrivere un <c>BggId</c>, la classe si rompe per un motivo che non
/// riguarda cio' che testa. Il guadagno vero di #4050 e' l'host, ed e' condiviso comunque.
/// </para>
/// </remarks>
public sealed class SessionNotesEndpointsIdorHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "session_notes_idor");

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
public sealed class SessionNotesEndpointsIdorIntegrationTests
    : IClassFixture<SessionNotesEndpointsIdorHostFixture>, IAsyncLifetime
{
    private readonly SessionNotesEndpointsIdorHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _otherClient = null!;
    private Guid _ownerId;
    private string _ownerToken = null!;
    private string _otherToken = null!;
    private Guid _sessionId;

    public SessionNotesEndpointsIdorIntegrationTests(SessionNotesEndpointsIdorHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // #4050. L'host viene dalla fixture (una volta per classe); qui resta un database clonato
        // dal template, piu' il seeding che questa classe aveva gia'.
        //
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory: lo scope qui sotto semina, e con
        // l'ordine invertito scriverebbe nel database di avvio mentre le richieste HTTP leggono
        // quello per test — note introvabili, cioe' un fallimento che somiglia a un bug di authz
        // invece che a un errore di cablaggio.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

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

        // Due client restano due client: i test distinguono i chiamanti con l'header di
        // autenticazione, non con l'istanza, ma crearli dall'host gia' avviato non costa un host.
        _ownerClient = _factory.CreateClient();
        _otherClient = _factory.CreateClient();
    }

    // I client sono di questa istanza e si dispongono; host e database sono della fixture.
    public ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _otherClient?.Dispose();
        return ValueTask.CompletedTask;
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
