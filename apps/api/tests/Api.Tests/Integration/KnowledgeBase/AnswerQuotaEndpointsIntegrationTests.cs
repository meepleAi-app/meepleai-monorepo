using System.Net;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="AnswerQuotaEndpointsIntegrationTests"/>. Il modello, il reranker, la
/// cache delle risposte e gli embedding sono i doppi di <see cref="AgentProfileFieldEffectHostFixture"/>;
/// in più il servizio dei piani conta su un <b>Redis vero</b>, quello del contenitore condiviso.
/// </summary>
/// <remarks>
/// Gli host di integrazione montano un Redis finto, che a ogni lettura risponde «niente»: con
/// quello la quota non scatterebbe mai, e questi test passerebbero o fallirebbero senza dire nulla
/// del contatore. Il Redis vero va solo al servizio dei piani: il resto dell'host tiene il suo
/// doppio, così nient'altro cambia comportamento.
/// </remarks>
public sealed class AnswerQuotaHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "answer_quota")
{
    // Letto alla costruzione: la fixture di collezione ha già avviato il contenitore.
    private readonly string _redisAddress = shared.RedisConnectionString;
    private IConnectionMultiplexer? _redis;

    private IConnectionMultiplexer Redis => _redis ??= ConnectionMultiplexer.Connect(_redisAddress);

    internal RecordingLlmService Llm { get; } = new();
    internal InMemoryAiResponseCache Cache { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmService>();
            services.AddSingleton<ILlmService>(Llm);
            services.RemoveAll<IAiResponseCacheService>();
            services.AddSingleton<IAiResponseCacheService>(Cache);
            services.RemoveAll<ICrossEncoderReranker>();
            services.AddSingleton<ICrossEncoderReranker>(new RecordingReranker());
            services.RemoveAll<IEmbeddingService>();
            services.AddScoped<IEmbeddingService>(_ => QueryVectorEmbeddings.Create());

            services.RemoveAll<ITierEnforcementService>();
            services.AddScoped<ITierEnforcementService>(sp => new TierEnforcementService(
                Redis,
                sp.GetRequiredService<MeepleAiDbContext>(),
                sp.GetRequiredService<IMemoryCache>(),
                sp.GetRequiredService<ILogger<TierEnforcementService>>()));
        }));
}

/// <summary>
/// #4168 (5g-b): la quota del piano sui tre percorsi che rispondono a una domanda, via HTTP, con
/// Postgres e un Redis vero. I test unitari dicono che cosa il passo decide; questi dicono che il
/// contatore conta davvero, che il piano è quello dell'utente, e che cosa esce sul filo — è ciò
/// che il frontend legge.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("Dependency", "Redis")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class AnswerQuotaEndpointsIntegrationTests
    : IClassFixture<AnswerQuotaHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    /// <summary>Il piano di prova: due domande al giorno.</summary>
    private const int QuestionsADay = 2;

    private readonly AnswerQuotaHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;

    public AnswerQuotaEndpointsIntegrationTests(AnswerQuotaHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _hostFixture.Llm.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);

        // L'utente di prova è sul piano «free» (i nomi di piano sono un insieme chiuso): qui quel
        // piano vale due domande al giorno. Il servizio tiene i piani in memoria per nome e l'host è
        // condiviso fra i test della classe, ma il limite è lo stesso per tutti.
        var limits = TierLimits.Create(
            maxPrivateGames: 3, maxPdfUploadsPerMonth: 3, maxPdfSizeBytes: 50L * 1024 * 1024,
            maxAgentQueriesPerDay: QuestionsADay, maxSessionQueries: 30,
            maxSessionPlayers: 6, maxPhotosPerSession: 5,
            sessionSaveEnabled: false, maxCatalogProposalsPerWeek: 1, maxLibraryGames: 5);
        var free = await db.TierDefinitions.AsTracking().FirstOrDefaultAsync(t => t.Name == "free", Ct);
        if (free is null)
        {
            db.TierDefinitions.Add(TierDefinition.Create("free", "Free", limits, "free"));
        }
        else
        {
            free.UpdateLimits(limits);
        }

        await db.SaveChangesAsync(Ct);

        _gameId = await OwnedGameAsync(db, "Coloni della quota", withRulebook: true);
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4168)",
            Category = "Features",
            IsActive = true,
            RequiresRestart = false,
            Environment = "All",
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedByUserId = _userId,
        });
        await db.SaveChangesAsync(Ct);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Il viaggio intero: la prima domanda la genera il modello, la seconda — identica — esce dalla
    /// cache, e tutte e due contano. La terza si ferma, con i numeri del piano e il link ai piani
    /// dentro un evento <c>Error</c>: gli stessi campi del 403 <c>tier_limit_exceeded</c>.
    /// </summary>
    [Fact]
    public async Task TheQuestionAfterTheLastOfThePlan_IsStoppedInTheStream_WithTheNumbersOfThePlan()
    {
        var first = await AskAsync(Question);
        TypesOf(first).Should().Contain((int)StreamingEventType.Complete);
        (await QuestionsCountedTodayAsync()).Should().Be(1);

        var second = await AskAsync(Question);
        StatesOf(second).Should().Contain("Retrieved from cache", "la seconda domanda deve uscire dalla cache");
        (await QuestionsCountedTodayAsync()).Should().Be(2);

        var third = await AskAsync(Question);

        var only = third.Should().ContainSingle().Which;
        only.GetProperty("type").GetInt32().Should().Be((int)StreamingEventType.Error);
        var data = only.GetProperty("data");
        data.GetProperty("errorCode").GetString().Should().Be("TIER_LIMIT_EXCEEDED");
        data.GetProperty("limitType").GetString().Should().Be("AgentQuery");
        data.GetProperty("current").GetInt32().Should().Be(QuestionsADay);
        data.GetProperty("max").GetInt32().Should().Be(QuestionsADay);
        data.GetProperty("upgradeUrl").GetString().Should().Be("/pricing");
        // Fermata prima di contare: il contatore resta dov'era.
        (await QuestionsCountedTodayAsync()).Should().Be(QuestionsADay);
    }

    /// <summary>Fuori dallo stream la stessa domanda riceve il 403 degli altri limiti di piano.</summary>
    [Fact]
    public async Task TheSingleQuestion_OverTheQuota_Answers403TierLimitExceeded()
    {
        await AlreadyAnsweredTodayAsync(QuestionsADay);

        var response = await PostAskAsync(_token);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("error").GetString().Should().Be("tier_limit_exceeded");
        json.RootElement.GetProperty("limitType").GetString().Should().Be("AgentQuery");
        json.RootElement.GetProperty("current").GetInt32().Should().Be(QuestionsADay);
        json.RootElement.GetProperty("max").GetInt32().Should().Be(QuestionsADay);
        json.RootElement.GetProperty("upgradeUrl").GetString().Should().Be("/pricing");
    }

    [Fact]
    public async Task TheSingleQuestion_UnderTheQuota_IsAnsweredAndCounted()
    {
        var response = await PostAskAsync(_token);

        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Ct));
        (await QuestionsCountedTodayAsync()).Should().Be(1);
    }

    /// <summary>Una domanda fra giochi è una domanda: conta uno, e alla quota si ferma come le altre.</summary>
    [Fact]
    public async Task AQuestionAcrossTheLibrary_CountsOne_AndStopsAtTheQuota()
    {
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await OwnedGameAsync(db, "Un secondo gioco con il manuale", withRulebook: true);
        }

        var answered = await AskLibraryAsync();
        TypesOf(answered).Should().Contain((int)StreamingEventType.Complete);
        (await QuestionsCountedTodayAsync()).Should().Be(1);

        await AlreadyAnsweredTodayAsync(1);
        var stopped = await AskLibraryAsync();

        ErrorCodeOf(stopped.Should().ContainSingle().Which).Should().Be("TIER_LIMIT_EXCEEDED");
    }

    /// <summary>
    /// Una domanda che non trova niente non ha avuto risposta: lo stream si chiude con
    /// <c>NO_RESULTS</c> e il contatore non cresce.
    /// </summary>
    [Fact]
    public async Task AQuestionThatFindsNothing_IsNotCounted()
    {
        Guid emptyGameId;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            emptyGameId = await OwnedGameAsync(db, "Un gioco senza manuale", withRulebook: false);
        }

        var events = await AskAsync(Question, gameId: emptyGameId);

        ErrorCodeOf(events[^1]).Should().Be("NO_RESULTS");
        (await QuestionsCountedTodayAsync()).Should().Be(0);
    }

    /// <summary>Il modello non risponde: l'utente non ha avuto la sua risposta, e non gli viene contata.</summary>
    [Fact]
    public async Task AQuestionTheModelDoesNotAnswer_IsNotCounted()
    {
        _hostFixture.Llm.RefuseNext = true;

        var response = await PostAskAsync(_token);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, body);
        body.Should().Contain(LlmAnswerErrorCodes.ProviderUnavailable);
        (await QuestionsCountedTodayAsync()).Should().Be(0);
    }

    /// <summary>
    /// Un amministratore non ha un limite: risponde anche oltre il piano di prova, e il suo
    /// contatore non cresce.
    /// </summary>
    [Fact]
    public async Task AnAdmin_IsNeverStopped_AndIsNotCounted()
    {
        Guid adminId;
        string adminToken;
        using (var scope = _hostFixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            (adminId, adminToken) = await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct);
        }

        for (var i = 0; i <= QuestionsADay; i++)
        {
            var events = await AskAsync($"{Question} ({i})", token: adminToken);
            TypesOf(events).Should().Contain((int)StreamingEventType.Complete);
        }

        (await QuestionsCountedTodayAsync(adminId)).Should().Be(0);
    }

    // ─── Supporto ────────────────────────────────────────────────

    /// <summary>Un gioco del catalogo che l'utente possiede (dichiarato), con o senza manuale indicizzato.</summary>
    private async Task<Guid> OwnedGameAsync(MeepleAiDbContext db, string title, bool withRulebook)
    {
        var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title);
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = _userId,
            SharedGameId = gameId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        if (withRulebook)
        {
            await IndexedRulebookSeed.SeedAsync(db, gameId, _userId, Ct);
        }

        return gameId;
    }

    /// <summary>Quante domande il servizio dei piani ha contato oggi, lette dal Redis vero.</summary>
    private async Task<int> QuestionsCountedTodayAsync(Guid? userId = null)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var tiers = scope.ServiceProvider.GetRequiredService<ITierEnforcementService>();
        return (await tiers.GetUsageAsync(userId ?? _userId, Ct)).AgentQueriesToday;
    }

    /// <summary>Porta il contatore di oggi avanti di tante risposte, come se fossero già state consegnate.</summary>
    private async Task AlreadyAnsweredTodayAsync(int answers)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var tiers = scope.ServiceProvider.GetRequiredService<ITierEnforcementService>();
        for (var i = 0; i < answers; i++)
        {
            await tiers.RecordUsageAsync(_userId, TierAction.AgentQuery, Ct);
        }
    }

    private Task<HttpResponseMessage> PostAskAsync(string token) =>
        _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", token,
                new { gameId = _gameId.ToString(), query = Question }),
            Ct);

    private async Task<List<JsonElement>> AskAsync(string query, Guid? gameId = null, string? token = null)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", token ?? _token,
                new { gameId = (gameId ?? _gameId).ToString(), query }),
            Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        // Lo stream scrive gli header prima di iterare: anche la quota viaggia dentro un 200.
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return EventsOf(body);
    }

    private async Task<List<JsonElement>> AskLibraryAsync()
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask/global", _token, new { query = Question }),
            Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        return EventsOf(body);
    }

    private static List<JsonElement> EventsOf(string body)
    {
        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            events.Add(doc.RootElement.Clone());
        }

        return events;
    }

    private static List<int> TypesOf(IEnumerable<JsonElement> events) =>
        events.Select(e => e.GetProperty("type").GetInt32()).ToList();

    private static List<string?> StatesOf(IEnumerable<JsonElement> events) =>
        events.Where(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.StateUpdate)
            .Select(e => e.GetProperty("data").GetProperty("message").GetString())
            .ToList();

    private static string? ErrorCodeOf(JsonElement evt) =>
        evt.GetProperty("type").GetInt32() == (int)StreamingEventType.Error
        && evt.GetProperty("data").TryGetProperty("errorCode", out var code)
            ? code.GetString()
            : null;
}
