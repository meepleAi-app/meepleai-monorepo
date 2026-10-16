using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="AgentSessionOwnershipIntegrationTests"/>: host per classe, database per
/// test, perché ogni test semina le proprie sessioni e alcuni le chiudono.
/// </summary>
public sealed class AgentSessionOwnershipHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "agent_session_owner");

/// <summary>
/// #4282 — le rotte della sessione agente (<c>/game-sessions/{id}/agent/*</c>) le usa solo chi le
/// possiede. L'id della sessione agente arriva dal corpo della richiesta: prima bastava conoscerlo per
/// chiedere dentro la sessione di un altro (con la memoria del suo agente nel prompt), sovrascriverne
/// lo stato, cambiarne l'agente o chiuderla, e per avviare l'agente sulla sessione di gioco di un altro.
/// </summary>
/// <remarks>
/// <para>
/// Ogni rotta è provata nelle due direzioni: chi non è il proprietario riceve 403, e il proprietario
/// riesce. Senza la seconda, una guardia troppo larga spegnerebbe la funzione senza che un test lo
/// noti. I payload dei rifiuti sono validi: la validazione gira prima del gestore, e un payload non
/// valido risponderebbe 422 senza esercitare il controllo (la lezione di #3756).
/// </para>
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4282")]
public sealed class AgentSessionOwnershipIntegrationTests
    : IClassFixture<AgentSessionOwnershipHostFixture>, IAsyncLifetime
{
    private const string ValidGameStateJson =
        """{"CurrentTurn":2,"ActivePlayer":"11111111-1111-1111-1111-111111111111","PlayerScores":{},"GamePhase":"main","LastAction":"moved"}""";

    private readonly AgentSessionOwnershipHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    private Guid _ownerId;
    private string _ownerToken = null!;
    private string _otherToken = null!;

    private Guid _gameId;
    private Guid _agentDefinitionId;
    private Guid _otherAgentDefinitionId;

    /// <summary>La sessione di gioco del proprietario con l'agente già attivo.</summary>
    private Guid _gameSessionId;

    /// <summary>Una seconda sessione di gioco del proprietario, senza agente: per l'avvio.</summary>
    private Guid _freeGameSessionId;

    /// <summary>La sessione agente del proprietario su <see cref="_gameSessionId"/>.</summary>
    private Guid _agentSessionId;

    public AgentSessionOwnershipIntegrationTests(AgentSessionOwnershipHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync DEVE precedere l'uso di _factory: con l'ordine invertito il seeding andrebbe
        // nel database di avvio e le richieste leggerebbero quello per test.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        (_ownerId, _ownerToken) = await TestSessionHelper.CreateUserSessionAsync(db);
        (_, _otherToken) = await TestSessionHelper.CreateUserSessionAsync(db);

        // Il proprietario possiede il gioco: il cancello RAG dell'avvio lo lascia passare.
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Agent Session Owner Game");
        await TestSessionHelper.SeedOwnedLibraryGameAsync(db, _ownerId, _gameId);

        _agentDefinitionId = SeedActiveAgentDefinition(db, "Owner Agent");
        _otherAgentDefinitionId = SeedActiveAgentDefinition(db, "Other Agent");

        _gameSessionId = SeedGameSession(db, _gameId, _ownerId);
        _freeGameSessionId = SeedGameSession(db, _gameId, _ownerId);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        // La sessione agente passa dal repository vero: è la forma che i gestori leggono.
        var repository = scope.ServiceProvider.GetRequiredService<IAgentSessionRepository>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var agentSession = new AgentSession(
            id: Guid.NewGuid(),
            agentDefinitionId: _agentDefinitionId,
            gameSessionId: _gameSessionId,
            userId: _ownerId,
            gameId: _gameId,
            initialState: GameState.Initial(_ownerId));
        await repository.AddAsync(agentSession);
        await unitOfWork.SaveChangesAsync();
        _agentSessionId = agentSession.Id;

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    private static Guid SeedActiveAgentDefinition(MeepleAiDbContext db, string name)
    {
        var agent = AgentDefinition.Create(
            name: name,
            description: "Agent for #4282",
            config: AgentDefinitionConfig.Create("gpt-4", 1000, 0.7f));
        agent.Activate();
        db.AgentDefinitions.Add(agent);
        return agent.Id;
    }

    private static Guid SeedGameSession(MeepleAiDbContext db, Guid gameId, Guid ownerId)
    {
        var gameSession = new GameSessionEntity
        {
            Id = Guid.NewGuid(),
            GameId = gameId,
            CreatedByUserId = ownerId,
            Status = "InProgress",
            StartedAt = DateTime.UtcNow,
            // GameSession requires at least one player: the repository rebuilds the aggregate.
            PlayersJson = """[{"PlayerName":"Owner","PlayerOrder":1,"Color":null}]""",
        };
        db.GameSessions.Add(gameSession);
        return gameSession.Id;
    }

    private string AgentUrl(Guid gameSessionId, string? action = null) =>
        action is null
            ? $"/api/v1/game-sessions/{gameSessionId}/agent"
            : $"/api/v1/game-sessions/{gameSessionId}/agent/{action}";

    private Task<HttpResponseMessage> SendAsync<T>(HttpMethod method, string url, string token, T body) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(method, url, token, body));

    private async Task<int> AgentSessionCountAsync(Guid gameSessionId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AgentSessions.CountAsync(s => s.GameSessionId == gameSessionId);
    }

    private async Task<AgentSessionEntity> ReadAgentSessionAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.AgentSessions.AsNoTracking().SingleAsync(s => s.Id == _agentSessionId);
    }

    private object LaunchBody(Guid gameId) => new
    {
        agentDefinitionId = _agentDefinitionId,
        gameId,
        initialGameStateJson = string.Empty,
    };

    // ── avvio ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Launch_by_another_user_is_forbidden_creates_nothing_and_leaves_the_owner_free_to_launch()
    {
        var refused = await SendAsync(HttpMethod.Post, AgentUrl(_freeGameSessionId, "launch"), _otherToken, LaunchBody(_gameId));

        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AgentSessionCountAsync(_freeGameSessionId)).Should().Be(0,
            "un avvio rifiutato non crea la sessione agente");

        // Prima chi arrivava primo occupava l'unico posto, e il proprietario riceveva 409.
        var launched = await SendAsync(HttpMethod.Post, AgentUrl(_freeGameSessionId, "launch"), _ownerToken, LaunchBody(_gameId));

        launched.StatusCode.Should().Be(HttpStatusCode.OK, await launched.Content.ReadAsStringAsync());
        (await AgentSessionCountAsync(_freeGameSessionId)).Should().Be(1);
    }

    [Fact]
    public async Task Launch_on_a_game_session_that_does_not_exist_returns_404()
    {
        var response = await SendAsync(HttpMethod.Post, AgentUrl(Guid.NewGuid(), "launch"), _ownerToken, LaunchBody(_gameId));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound,
            "prima l'unico controllo era la chiave esterna di agent_sessions, al salvataggio");
    }

    [Fact]
    public async Task Launch_with_a_game_other_than_the_game_of_the_session_is_rejected_and_creates_nothing()
    {
        // Un gioco che il proprietario possiede: il rifiuto viene dal confronto, non dal cancello RAG.
        Guid otherGameId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            otherGameId = await TestSessionHelper.SeedSharedGameAsync(db, "Another Owned Game");
            await TestSessionHelper.SeedOwnedLibraryGameAsync(db, _ownerId, otherGameId);
        }

        var response = await SendAsync(HttpMethod.Post, AgentUrl(_freeGameSessionId, "launch"), _ownerToken, LaunchBody(otherGameId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await AgentSessionCountAsync(_freeGameSessionId)).Should().Be(0);
    }

    [Fact]
    public async Task Launch_on_a_game_the_owner_cannot_query_returns_403_rag_ownership_required_and_creates_nothing()
    {
        // La sessione di gioco è del proprietario, ma il gioco non è nella sua libreria né pubblico per il RAG.
        Guid lockedGameId;
        Guid lockedGameSessionId;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            lockedGameId = await TestSessionHelper.SeedSharedGameAsync(db, "Locked Manual Game");
            lockedGameSessionId = SeedGameSession(db, lockedGameId, _ownerId);
            await db.SaveChangesAsync();
        }

        var response = await SendAsync(HttpMethod.Post, AgentUrl(lockedGameSessionId, "launch"), _ownerToken, LaunchBody(lockedGameId));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        json.RootElement.GetProperty("error").GetString().Should().Be("RAG_OWNERSHIP_REQUIRED");
        (await AgentSessionCountAsync(lockedGameSessionId)).Should().Be(0);
    }

    // ── stato, definizione, configurazione ───────────────────────────────────────

    [Fact]
    public async Task UpdateState_by_another_user_is_forbidden_and_the_owner_updates_it()
    {
        var body = new { agentSessionId = _agentSessionId, gameStateJson = ValidGameStateJson };

        var refused = await SendAsync(HttpMethod.Put, AgentUrl(_gameSessionId, "state"), _otherToken, body);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAgentSessionAsync()).CurrentGameStateJson.Should().NotContain("moved");

        var updated = await SendAsync(HttpMethod.Put, AgentUrl(_gameSessionId, "state"), _ownerToken, body);
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
        (await ReadAgentSessionAsync()).CurrentGameStateJson.Should().Contain("moved");
    }

    [Fact]
    public async Task UpdateState_on_an_agent_session_of_another_game_session_returns_404()
    {
        var body = new { agentSessionId = _agentSessionId, gameStateJson = ValidGameStateJson };

        var response = await SendAsync(HttpMethod.Put, AgentUrl(_freeGameSessionId, "state"), _ownerToken, body);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateDefinition_by_another_user_is_forbidden_and_the_owner_updates_it()
    {
        var body = new { agentSessionId = _agentSessionId, newAgentDefinitionId = _otherAgentDefinitionId };

        var refused = await SendAsync(HttpMethod.Patch, AgentUrl(_gameSessionId, "definition"), _otherToken, body);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Solo lo status: la riga non è una prova, perché questa rotta non salva la definizione
        // nemmeno per il proprietario (#4367). Lo status dice che il controllo lo lascia passare.
        var updated = await SendAsync(HttpMethod.Patch, AgentUrl(_gameSessionId, "definition"), _ownerToken, body);
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UpdateConfig_by_another_user_is_forbidden_and_the_owner_updates_it()
    {
        var body = new
        {
            agentSessionId = _agentSessionId,
            modelType = "gpt-4o-mini",
            temperature = 0.3,
            maxTokens = 1024,
            ragStrategy = "VectorOnly",
            ragParams = new Dictionary<string, object>(),
        };

        var refused = await SendAsync(HttpMethod.Patch, AgentUrl(_gameSessionId, "config"), _otherToken, body);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Solo lo status, come per la definizione: la configurazione non ha colonne (#4367).
        var updated = await SendAsync(HttpMethod.Patch, AgentUrl(_gameSessionId, "config"), _ownerToken, body);
        updated.StatusCode.Should().Be(HttpStatusCode.NoContent, await updated.Content.ReadAsStringAsync());
    }

    // ── chiusura ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task End_by_another_user_is_forbidden_and_the_owner_ends_it()
    {
        var body = new { agentSessionId = _agentSessionId };

        var refused = await SendAsync(HttpMethod.Delete, AgentUrl(_gameSessionId), _otherToken, body);
        refused.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ReadAgentSessionAsync()).IsActive.Should().BeTrue("un rifiuto non chiude la sessione agente");

        var ended = await SendAsync(HttpMethod.Delete, AgentUrl(_gameSessionId), _ownerToken, body);
        ended.StatusCode.Should().Be(HttpStatusCode.NoContent, await ended.Content.ReadAsStringAsync());
        (await ReadAgentSessionAsync()).IsActive.Should().BeFalse();
    }
}
