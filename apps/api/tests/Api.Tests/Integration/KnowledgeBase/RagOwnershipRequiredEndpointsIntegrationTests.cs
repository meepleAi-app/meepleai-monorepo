using System.Net;
using System.Text;
using System.Text.Json;
using Api.Models;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="RagOwnershipRequiredEndpointsIntegrationTests"/>: host per classe,
/// database per test (#4050). Il seeding sta in <c>InitializeAsync</c>, quindi un database
/// condiviso accumulerebbe un utente e un gioco per metodo.
/// </summary>
public sealed class RagOwnershipRequiredHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "rag_ownership");

/// <summary>
/// #4155. Un utente che ha il gioco in libreria ma non ne ha dichiarato il possesso viene
/// respinto dal cancello RAG (#443, B5). Il cancello resta; quello che cambia e' COME lo si dice:
/// prima la chat riceveva «An error occurred: Accesso RAG non autorizzato» con codice
/// <c>INTERNAL_ERROR</c>, cioe' un guasto, e la domanda singola un 403 <c>forbidden</c> che non
/// diceva quale gioco. Ora entrambi i trasporti portano <c>RAG_OWNERSHIP_REQUIRED</c> e il gioco,
/// e dopo la dichiarazione la stessa domanda passa il cancello.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4155")]
public sealed class RagOwnershipRequiredEndpointsIntegrationTests
    : IClassFixture<RagOwnershipRequiredHostFixture>, IAsyncLifetime
{
    private const string OwnershipCode = "RAG_OWNERSHIP_REQUIRED";

    private static readonly JsonSerializerOptions SseJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly RagOwnershipRequiredHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _gameId;

    public RagOwnershipRequiredEndpointsIntegrationTests(RagOwnershipRequiredHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // 🔴 BeginTestAsync prima di ogni uso di _factory: altrimenti il seeding finisce nel
        // database di avvio e le richieste leggono quello del test.
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        Guid userId;
        (userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db);

        // Il caso dell'issue: gioco del catalogo, non pubblico per il RAG, aggiunto in libreria
        // senza dichiararne il possesso (OwnershipDeclaredAt resta null).
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Gioco non dichiarato");
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = userId,
            SharedGameId = _gameId,
            AddedAt = DateTime.UtcNow,
        });

        // I flag stanno nel database e mancano su un database nuovo, dove valgono false: senza
        // questa riga lo stream risponderebbe 403 feature_disabled prima di arrivare al cancello.
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4155)",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
        });
        // #4206: anche la guida alla preparazione è dietro un flag, assente su un database nuovo.
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.SetupGuideGeneration",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4206)",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = userId,
        });
        await db.SaveChangesAsync(Ct);

        _client = _factory.CreateClient();
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Ask_WithoutDeclaredOwnership_Returns403_NamingTheCodeAndTheGame()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
                new { gameId = _gameId.ToString(), query = "Come si vince?" }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().Should().Be(OwnershipCode);
        json.RootElement.GetProperty("gameId").GetGuid().Should().Be(_gameId);
    }

    [Fact]
    public async Task QaStream_WithoutDeclaredOwnership_EmitsOwnershipError_NotAnInternalError()
    {
        var events = await StreamQaAsync();

        // Il diniego e' l'unico evento: nessun lavoro di ricerca o LLM prima del cancello.
        var error = events.Should().ContainSingle().Which;
        error.GetProperty("type").GetInt32().Should().Be((int)StreamingEventType.Error);

        var data = error.GetProperty("data");
        data.GetProperty("errorCode").GetString().Should().Be(OwnershipCode);
        data.GetProperty("gameId").GetGuid().Should().Be(_gameId);
        data.GetProperty("errorMessage").GetString().Should()
            .NotStartWith("An error occurred", "un diniego atteso non e' un guasto del server");
    }

    [Fact]
    public async Task QaStream_AfterDeclaringOwnership_PassesTheGate()
    {
        // La via d'uscita che il codice annuncia: la dichiarazione tramite l'endpoint vero.
        // RagAccessService legge dal database senza cache, quindi non serve invalidare nulla.
        var declare = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"/api/v1/library/{_gameId}/declare-ownership", _token),
            Ct);
        declare.StatusCode.Should().Be(HttpStatusCode.OK, await declare.Content.ReadAsStringAsync(Ct));

        var events = await StreamQaAsync();

        events.Should().NotContain(e => ErrorCodeOf(e) == OwnershipCode);
        // Prova che il cancello e' stato superato: la ricerca parte. Il gioco non ha un manuale
        // indicizzato, quindi lo stream si chiude con NO_RESULTS — lo scenario «posseduto ma senza
        // manuale» che il dialogo della chat deve spiegare invece di rinviare la domanda.
        events.Should().Contain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.StateUpdate);
        ErrorCodeOf(events[^1]).Should().Be("NO_RESULTS");

        // gameId compare solo nei dinieghi legati a un gioco: gli altri errori restano com'erano,
        // senza un "gameId": null in piu'.
        events[^1].GetProperty("data").TryGetProperty("gameId", out _).Should().BeFalse();
    }

    // ─── #4168 (5g-a): il ruolo della sessione arriva al cancello dai percorsi di risposta ───

    /// <summary>
    /// Un amministratore passa senza dichiarare il possesso. Il ruolo viaggia dalla sessione come
    /// testo, e a portarlo fino al cancello è il passo condiviso (<c>IAnswerPreconditions</c>):
    /// se lo perdesse, la chat tratterebbe l'amministratore come un utente qualsiasi.
    /// </summary>
    [Fact]
    public async Task QaStream_AsAnAdminWithoutDeclaredOwnership_PassesTheGate()
    {
        var adminToken = await CreateAdminTokenAsync();

        var events = await StreamQaAsync(adminToken);

        events.Should().NotContain(e => ErrorCodeOf(e) == OwnershipCode);
        // Come per chi ha dichiarato il possesso: la ricerca parte e non trova un manuale.
        ErrorCodeOf(events[^1]).Should().Be("NO_RESULTS");
    }

    /// <summary>Lo stesso sulla domanda singola, che ha un percorso suo.</summary>
    [Fact]
    public async Task Ask_AsAnAdminWithoutDeclaredOwnership_PassesTheGate()
    {
        var adminToken = await CreateAdminTokenAsync();

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", adminToken,
                new { gameId = _gameId.ToString(), query = "Come si vince?" }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden, body);
        body.Should().NotContain(OwnershipCode);
    }

    private async Task<string> CreateAdminTokenAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (_, token) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
        return token;
    }

    // ─── #4206: la guida alla preparazione legge la stessa KB, quindi passa dallo stesso cancello ───

    [Fact]
    public async Task SetupGuide_WithoutDeclaredOwnership_EmitsOwnershipError_BeforeAnyWork()
    {
        var events = await StreamSetupGuideAsync();

        // Prima di #4206 la guida cercava nella KB del gioco senza controllare l'accesso. Ora il
        // diniego è l'unico evento: niente «Preparing setup guide...», niente ricerca, niente LLM.
        var error = events.Should().ContainSingle().Which;
        error.GetProperty("type").GetInt32().Should().Be((int)StreamingEventType.Error);
        var data = error.GetProperty("data");
        data.GetProperty("errorCode").GetString().Should().Be(OwnershipCode);
        data.GetProperty("gameId").GetGuid().Should().Be(_gameId);
    }

    [Fact]
    public async Task SetupGuide_AfterDeclaringOwnership_PassesTheGate()
    {
        var declare = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"/api/v1/library/{_gameId}/declare-ownership", _token),
            Ct);
        declare.StatusCode.Should().Be(HttpStatusCode.OK, await declare.Content.ReadAsStringAsync(Ct));

        var events = await StreamSetupGuideAsync();

        events.Should().NotContain(e => ErrorCodeOf(e) == OwnershipCode);
        events[0].GetProperty("type").GetInt32().Should().Be((int)StreamingEventType.StateUpdate,
            "superato il cancello, la guida comincia a prepararsi");
    }

    // ─── #4294: explain e il suggerimento di mossa leggono la stessa KB e restituiscono i passaggi ───

    [Fact]
    public async Task Explain_WithoutDeclaredOwnership_Returns403_NamingTheCodeAndTheGame()
    {
        var response = await PostExplainAsync();

        // Prima di #4294 la rotta cercava nella KB del gioco e restituiva i passaggi in `citations`
        // a chiunque avesse una sessione.
        await AssertOwnershipDenialAsync(response);
    }

    [Fact]
    public async Task Explain_AfterDeclaringOwnership_PassesTheGate()
    {
        await DeclareOwnershipAsync();

        var response = await PostExplainAsync();

        // Il gioco non ha un manuale indicizzato: superato il cancello, la ricerca non trova nulla
        // e la rotta risponde lo stesso, senza passaggi.
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    [Fact]
    public async Task PlayerModeSuggest_WithoutDeclaredOwnership_Returns403_NamingTheCodeAndTheGame()
    {
        var response = await PostPlayerModeSuggestAsync();

        // Prima di #4294 il suggerimento cercava nella KB del gioco, passava i passaggi all'LLM e li
        // restituiva in `sources`.
        await AssertOwnershipDenialAsync(response);
    }

    [Fact]
    public async Task PlayerModeSuggest_AfterDeclaringOwnership_PassesTheGate()
    {
        await DeclareOwnershipAsync();

        var response = await PostPlayerModeSuggestAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
    }

    private Task<HttpResponseMessage> PostExplainAsync() =>
        _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/explain", _token,
                new { gameId = _gameId.ToString(), topic = "preparazione" }),
            Ct);

    private Task<HttpResponseMessage> PostPlayerModeSuggestAsync() =>
        _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/player-mode/suggest", _token,
                new
                {
                    gameId = _gameId.ToString(),
                    gameState = new { players = new[] { new { name = "Anna", score = 3 } } },
                    query = "Cosa mi conviene fare?",
                }),
            Ct);

    private async Task DeclareOwnershipAsync()
    {
        var declare = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, $"/api/v1/library/{_gameId}/declare-ownership", _token),
            Ct);
        declare.StatusCode.Should().Be(HttpStatusCode.OK, await declare.Content.ReadAsStringAsync(Ct));
    }

    private async Task AssertOwnershipDenialAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);

        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().Should().Be(OwnershipCode);
        json.RootElement.GetProperty("gameId").GetGuid().Should().Be(_gameId);
    }

    private async Task<IReadOnlyList<JsonElement>> StreamSetupGuideAsync()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/setup", _token,
                new { gameId = _gameId.ToString(), playerCount = 2 }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return ParseSseEvents(body);
    }

    private async Task<IReadOnlyList<JsonElement>> StreamQaAsync(string? token = null)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa/stream", token ?? _token,
                new { gameId = _gameId.ToString(), query = "Come si vince?" }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        // Lo stream scrive gli header prima di iterare: il diniego viaggia DENTRO un 200.
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return ParseSseEvents(body);
    }

    private static string? ErrorCodeOf(JsonElement evt) =>
        evt.GetProperty("type").GetInt32() == (int)StreamingEventType.Error
        && evt.GetProperty("data").TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;

    private static List<JsonElement> ParseSseEvents(string body)
    {
        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            var json = trimmed["data:".Length..].Trim();
            if (json.Length == 0) continue;
            events.Add(JsonSerializer.Deserialize<JsonElement>(json, SseJsonOptions));
        }
        return events;
    }
}
