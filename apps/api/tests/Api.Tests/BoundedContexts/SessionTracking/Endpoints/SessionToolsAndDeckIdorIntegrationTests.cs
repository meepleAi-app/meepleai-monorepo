using System.Net;
using System.Net.Http.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SessionTracking;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Endpoints;

/// <summary>
/// Fixture della classe: host e database costruiti una volta sola. Il perche', i numeri e le
/// condizioni per applicare lo stesso schema altrove stanno in <see cref="IntegrationHostFixture"/>.
///
/// <para>
/// 🔴 <b>Perche' condividere il database e' sicuro QUI.</b> Ogni test semina, nel proprio
/// <c>InitializeAsync</c>, utenti e sessione con id nuovi (<c>Guid.NewGuid()</c>), e ogni asserzione
/// e' uno status code su una richiesta scoped a quel <c>_sessionId</c>: nessun conteggio, nessuna
/// lista globale, nessun ordinamento.
/// </para>
/// </summary>
public sealed class SessionToolsAndDeckIdorHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "session_tools_idor");

/// <summary>
/// HTTP-layer IDOR tests per gli endpoint di scrittura di SessionTracking rimasti scoperti da
/// #3263: timer (start/pause/resume/reset), mazzo (create/shuffle/draw/discard), upload media e
/// chat di sessione. Issue #3756.
///
/// <para>Modello di minaccia: il <c>sessionId</c> e' scopribile da percorsi di lettura non
/// autenticati (<c>GET /game-sessions/code/{code}</c>, <c>/scoreboard</c>). Prima di questo fix
/// nessuno di questi endpoint derivava l'identita' dal principal — il comando era bindato dal body
/// e l'unico controllo era <c>sessionId != command.SessionId</c>. I timer sono il caso grave:
/// mutano il <c>TimerStateManager</c> condiviso e pubblicano eventi SSE
/// <c>EventVisibility.Public</c> a tutti i partecipanti della sessione vittima, con un
/// <c>ParticipantName</c> scelto dall'attaccante.</para>
///
/// <para>Gli endpoint DEVONO derivare l'identita' dal principal e gli handler DEVONO rifiutare con
/// 403 un chiamante che non sia ne' owner ne' partecipante registrato (User-linked).</para>
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Concern", "Security")]
public sealed class SessionToolsAndDeckIdorIntegrationTests
    : IClassFixture<SessionToolsAndDeckIdorHostFixture>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;
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

    public SessionToolsAndDeckIdorIntegrationTests(SessionToolsAndDeckIdorHostFixture host)
    {
        _factory = host.Factory;
    }

    public async ValueTask InitializeAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _attackerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_memberId, _memberToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, "IDOR Tools Game");

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

    public ValueTask DisposeAsync()
    {
        _ownerClient?.Dispose();
        _attackerClient?.Dispose();
        _memberClient?.Dispose();
        return ValueTask.CompletedTask;
    }

    private string BaseUrl => $"/api/v1/game-sessions/{_sessionId}";

    private Task<HttpResponseMessage> PostAsync(HttpClient client, string token, string path, object body) =>
        client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"{BaseUrl}{path}", token, body));

    // ── Timer ─────────────────────────────────────────────────────────────────
    // Il caso grave: TimerStateManager e' condiviso e il broadcast e' EventVisibility.Public.

    private object StartTimerBody(Guid participantId) => new
    {
        sessionId = _sessionId,
        participantId,
        participantName = "Owner",
        durationSeconds = 60,
    };

    private object TimerBody(Guid participantId) => new { sessionId = _sessionId, participantId };

    [Fact(Timeout = 90_000)]
    public async Task StartTimer_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/timer/start", StartTimerBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter far partire il timer di una sessione altrui, ne' emettere l'evento SSE che ne consegue");
    }

    [Fact(Timeout = 90_000)]
    public async Task PauseTimer_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/timer/pause", TimerBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter mettere in pausa il timer di una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task ResumeTimer_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/timer/resume", TimerBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter riprendere il timer di una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task ResetTimer_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/timer/reset", TimerBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter azzerare il timer di una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task StartTimer_AsOwner_Succeeds()
    {
        var response = await PostAsync(_ownerClient, _ownerToken, "/timer/start", StartTimerBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.OK, "l'owner deve poter far partire il timer");
    }

    [Fact(Timeout = 90_000)]
    public async Task StartTimer_AsMemberParticipant_Succeeds()
    {
        var response = await PostAsync(_memberClient, _memberToken, "/timer/start", StartTimerBody(_memberParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.OK,
            "un partecipante registrato deve poter far partire il timer: il guard e' owner OPPURE partecipante");
    }

    // ── Mazzo ─────────────────────────────────────────────────────────────────

    private object CreateDeckBody => new
    {
        sessionId = _sessionId,
        name = "IDOR Deck",
        deckType = "standard",
        includeJokers = false,
    };

    private async Task<Guid> CreateDeckAsOwnerAsync()
    {
        var response = await PostAsync(_ownerClient, _ownerToken, "/decks", CreateDeckBody);
        response.StatusCode.Should().Be(HttpStatusCode.Created, "il setup del test crea il mazzo come owner");
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
        return body!["deckId"].GetGuid();
    }

    [Fact(Timeout = 90_000)]
    public async Task CreateDeck_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/decks", CreateDeckBody);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter creare mazzi in una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task ShuffleDeck_AsAttacker_IsForbidden()
    {
        var deckId = await CreateDeckAsOwnerAsync();

        var response = await PostAsync(_attackerClient, _attackerToken, $"/decks/{deckId}/shuffle",
            new { deckId, sessionId = _sessionId, includeDiscard = false });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "il guard deve scattare prima del controllo di consistenza mazzo-sessione: qui il mazzo esiste davvero");
    }

    [Fact(Timeout = 90_000)]
    public async Task DrawCards_AsAttacker_IsForbidden()
    {
        var deckId = await CreateDeckAsOwnerAsync();

        var response = await PostAsync(_attackerClient, _attackerToken, $"/decks/{deckId}/draw",
            new { deckId, sessionId = _sessionId, participantId = _ownerParticipantId, count = 1 });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter pescare dal mazzo di una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task DiscardCards_AsAttacker_IsForbidden()
    {
        var deckId = await CreateDeckAsOwnerAsync();

        // cardIds NON vuoto di proposito: DiscardCardsCommandValidator pretende almeno una carta, e
        // la pipeline di validazione gira PRIMA del guard. Con una lista vuota la risposta sarebbe
        // 422 e il test non eserciterebbe l'autorizzazione. Le carte non esistono, ma non importa:
        // il guard deve rifiutare prima che l'handler le cerchi.
        var response = await PostAsync(_attackerClient, _attackerToken, $"/decks/{deckId}/discard",
            new { deckId, sessionId = _sessionId, participantId = _ownerParticipantId, cardIds = new[] { Guid.NewGuid() } });

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter scartare carte in una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task CreateDeck_AsOwner_Succeeds()
    {
        var response = await PostAsync(_ownerClient, _ownerToken, "/decks", CreateDeckBody);

        response.StatusCode.Should().Be(HttpStatusCode.Created, "l'owner deve poter creare un mazzo");
    }

    [Fact(Timeout = 90_000)]
    public async Task ShuffleDeck_AsOwner_Succeeds()
    {
        var deckId = await CreateDeckAsOwnerAsync();

        var response = await PostAsync(_ownerClient, _ownerToken, $"/decks/{deckId}/shuffle",
            new { deckId, sessionId = _sessionId, includeDiscard = false });

        response.IsSuccessStatusCode.Should().BeTrue(
            $"l'owner deve poter mescolare il proprio mazzo, invece ha ricevuto {response.StatusCode}");
    }

    // ── Media e chat ──────────────────────────────────────────────────────────

    private object MediaBody(Guid participantId) => new
    {
        sessionId = _sessionId,
        participantId,
        fileId = Guid.NewGuid().ToString("N"),
        fileName = "shot.png",
        contentType = "image/png",
        fileSizeBytes = 1024L,
        mediaType = "Photo",
        caption = (string?)null,
        snapshotId = (Guid?)null,
        turnNumber = (int?)null,
    };

    private object ChatBody(Guid senderId) => new
    {
        sessionId = _sessionId,
        senderId,
        content = "messaggio iniettato",
        turnNumber = (int?)null,
        mentionsJson = (string?)null,
    };

    [Fact(Timeout = 90_000)]
    public async Task UploadMedia_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/media", MediaBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter allegare media a una sessione altrui");
    }

    [Fact(Timeout = 90_000)]
    public async Task SendChatMessage_AsAttacker_IsForbidden()
    {
        var response = await PostAsync(_attackerClient, _attackerToken, "/chat", ChatBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden,
            "chi non e' ne' owner ne' partecipante non deve poter scrivere in chat spacciandosi per un partecipante della sessione vittima");
    }

    [Fact(Timeout = 90_000)]
    public async Task UploadMedia_AsOwner_Succeeds()
    {
        var response = await PostAsync(_ownerClient, _ownerToken, "/media", MediaBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "l'owner deve poter allegare media alla propria sessione");
    }

    [Fact(Timeout = 90_000)]
    public async Task SendChatMessage_AsOwner_Succeeds()
    {
        var response = await PostAsync(_ownerClient, _ownerToken, "/chat", ChatBody(_ownerParticipantId));

        response.StatusCode.Should().Be(HttpStatusCode.Created, "l'owner deve poter scrivere nella chat della propria sessione");
    }
}
