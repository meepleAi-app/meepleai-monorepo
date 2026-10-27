using System.Net;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Models;
using Api.Services;
using Api.Services.LlmClients;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="AnswerOpenRequestsLimitIntegrationTests"/>: i doppi ai margini sono quelli
/// di <see cref="AgentProfileFieldEffectHostFixture"/>, ma il gateway LLM è un <see cref="GatedLlmService"/>
/// (le risposte restano aperte finché il test non apre il cancello) e il rate limiting è acceso.
/// </summary>
public sealed class AnswerOpenRequestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "answer_open")
{
    internal GatedLlmService Llm { get; } = new();
    internal InMemoryAiResponseCache Cache { get; } = new();

    protected override bool EnableRateLimiting => true;

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
        }));
}

/// <summary>
/// #4388 (decisione c di #4168): al massimo due risposte aperte per utente sulle rotte di risposta.
/// La quota resta «verifica prima, conteggio a risposta consegnata»: fra la verifica e il conteggio
/// passa l'intera generazione, quindi N richieste parallele sotto il limite passano tutte, e un solo
/// utente può esaurire il tetto globale del gateway per tutti. Il rifiuto arriva prima della
/// pipeline: il gateway non è chiamato (le prove lo asseriscono); il contatore della quota non cresce
/// per costruzione, ma qui non è asserito.
/// </summary>
/// <remarks>
/// Le richieste restano aperte davvero: il gateway di prova si ferma su un cancello, e il test sa
/// che una richiesta è «in corso» perché è entrata nel gateway, non perché è passato del tempo.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4388")]
public sealed class AnswerOpenRequestsLimitIntegrationTests
    : IClassFixture<AnswerOpenRequestsHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";

    /// <summary>Quanto aspettare che una richiesta entri nel gateway, o che una rifiutata risponda.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly AnswerOpenRequestsHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _gameId;
    private int _questionNumber;

    /// <summary>Tutte le richieste che il test ha avviato: a fine test si aspetta che finiscano.</summary>
    private readonly List<Task<HttpResponseMessage>> _requests = [];

    public AnswerOpenRequestsLimitIntegrationTests(AnswerOpenRequestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private GatedLlmService Llm => _hostFixture.Llm;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        Llm.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        Guid userId;
        (userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni del limite");
        db.UserLibraryEntries.Add(OwnedBy(userId));
        db.SystemConfigurations.Add(new SystemConfigurationEntity
        {
            Id = Guid.NewGuid(),
            Key = "Features.StreamingResponses",
            Value = "true",
            ValueType = "bool",
            Description = "Integration test seed (#4388)",
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
        await IndexedRulebookSeed.SeedAsync(db, _gameId, userId, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        // Un test che fallisce con le richieste ancora aperte non deve lasciarle appese all'host né al test
        // dopo: il cancello si apre e si aspetta (a tempo) che finiscano, prima che il prossimo test lo richiuda.
        Llm.Open();
        await Task.WhenAny(Task.WhenAll(_requests.Select(SettleAsync)), Task.Delay(Patience));
        _client?.Dispose();
    }

    private static async Task SettleAsync(Task<HttpResponseMessage> request)
    {
        try
        {
            using var response = await request;
        }
        catch (HttpRequestException)
        {
            // Una richiesta che cade dopo che il test ha già deciso il suo esito non aggiunge niente.
        }
        catch (OperationCanceledException)
        {
        }
    }

    // ─── Il limite ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Il «rosso prima» della issue: tre richieste concorrenti dello stesso utente, un gateway che si
    /// ferma su un cancello, un 429 sulla terza. Prima del limitatore la terza entra nel gateway e
    /// aspetta con le altre.
    /// </summary>
    [Fact]
    public async Task TheThirdOpenAnswerOfAUser_IsRefused_BeforeTheGatewayIsCalled()
    {
        var first = OpenAsync(Route.AgentsQaStream, _token);
        var second = OpenAsync(Route.AgentsQaStream, _token);
        await WaitForTheGatewayAsync(2, first, second);

        var third = await TryThirdAsync(Route.AgentsQaStream, _token);

        third.Status.Should().Be(HttpStatusCode.TooManyRequests, third.Body);
        third.Body.Should().Contain("Too Many Requests");
        Llm.Entered.Should().Be(2, "il rifiuto arriva prima della pipeline: il gateway non vede la terza");

        Llm.Open();
        (await first).StatusCode.Should().Be(HttpStatusCode.OK);
        (await second).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AnAnswerThatFinishes_FreesASlot()
    {
        var first = OpenAsync(Route.AgentsQaStream, _token);
        var second = OpenAsync(Route.AgentsQaStream, _token);
        await WaitForTheGatewayAsync(2, first, second);
        Llm.Open();
        await Task.WhenAll(first, second);

        var next = await TryThirdAsync(Route.AgentsQaStream, _token);

        next.Status.Should().Be(HttpStatusCode.OK, "le due risposte sono finite: ne può aprire un'altra. " + next.Body);
    }

    [Fact]
    public async Task TwoUsers_WithTwoOpenAnswersEach_AllPass()
    {
        var other = await SeedAnotherUserAsync();
        var requests = new[]
        {
            OpenAsync(Route.AgentsQaStream, _token),
            OpenAsync(Route.AgentsQaStream, _token),
            OpenAsync(Route.AgentsQaStream, other),
            OpenAsync(Route.AgentsQaStream, other),
        };
        await WaitForTheGatewayAsync(4, requests);

        Llm.Open();
        var responses = await Task.WhenAll(requests);

        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK,
            "il limite è per utente: il secondo utente non paga per il primo");
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("admin")]
    [InlineData("editor")]
    public async Task AnAdminOrAnEditor_IsNotLimited(string role)
    {
        var token = await SeedPrivilegedUserAsync(role);
        var requests = new[]
        {
            OpenAsync(Route.AgentsQaStream, token),
            OpenAsync(Route.AgentsQaStream, token),
            OpenAsync(Route.AgentsQaStream, token),
        };

        await WaitForTheGatewayAsync(3, requests);

        Llm.Open();
        var responses = await Task.WhenAll(requests);
        responses.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.OK,
            "come per la quota, chi gestisce il sistema non è soggetto al limite delle risposte aperte");
    }

    /// <summary>
    /// Le quattro rotte di risposta hanno lo stesso limite, e ciascuna tiene il conto delle proprie
    /// richieste insieme alle altre: due aperte su una rotta e una terza su un'altra sono tre.
    /// </summary>
    [Theory]
    [InlineData(Route.AgentsQaStream)]
    [InlineData(Route.AgentsQa)]
    [InlineData(Route.KnowledgeBaseAsk)]
    [InlineData(Route.KnowledgeBaseAskGlobal)]
    public async Task EveryAnswerRoute_RefusesTheThirdOpenAnswer(Route route)
    {
        var first = OpenAsync(route, _token);
        var second = OpenAsync(route, _token);
        await WaitForTheGatewayAsync(2, first, second);

        var third = await TryThirdAsync(route, _token);

        third.Status.Should().Be(HttpStatusCode.TooManyRequests, third.Body);
        Llm.Entered.Should().Be(2);
    }

    [Fact]
    public async Task TheCountIsSharedAcrossTheAnswerRoutes()
    {
        var first = OpenAsync(Route.AgentsQaStream, _token);
        var second = OpenAsync(Route.KnowledgeBaseAskGlobal, _token);
        await WaitForTheGatewayAsync(2, first, second);

        var third = await TryThirdAsync(Route.AgentsQa, _token);

        third.Status.Should().Be(HttpStatusCode.TooManyRequests, third.Body);
    }

    // ─── Aiuti ──────────────────────────────────────────────────────────────────

    public enum Route
    {
        AgentsQaStream,
        AgentsQa,
        KnowledgeBaseAsk,
        KnowledgeBaseAskGlobal,
    }

    private HttpRequestMessage RequestFor(Route route, string token)
    {
        // Una domanda diversa a ogni richiesta: nessuna può rispondere dalla cache di un'altra.
        var query = $"{Question} #{Interlocked.Increment(ref _questionNumber)}";
        return route switch
        {
            Route.AgentsQaStream => TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", token,
                new { gameId = _gameId.ToString(), query }),
            Route.AgentsQa => TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa", token,
                new { gameId = _gameId.ToString(), query }),
            Route.KnowledgeBaseAsk => TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", token,
                new { gameId = _gameId.ToString(), query }),
            Route.KnowledgeBaseAskGlobal => TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask/global", token,
                new { query }),
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
    }

    /// <summary>Apre una richiesta e non aspetta la fine: resta aperta finché il cancello non si apre.</summary>
    private Task<HttpResponseMessage> OpenAsync(Route route, string token)
    {
        var request = Task.Run(() => _client.SendAsync(RequestFor(route, token), Ct), Ct);
        _requests.Add(request);
        return request;
    }

    /// <summary>
    /// Una richiesta che dovrebbe rispondere subito: o perché è rifiutata, o perché c'è posto. Se non
    /// risponde, è entrata nel gateway ed è ferma sul cancello: lo si dice invece di restare appesi.
    /// </summary>
    private async Task<(HttpStatusCode Status, string Body)> TryThirdAsync(Route route, string token)
    {
        var request = OpenAsync(route, token);
        var finished = await Task.WhenAny(request, Task.Delay(Patience, Ct));
        if (finished != request)
        {
            return (HttpStatusCode.RequestTimeout,
                $"la richiesta non ha risposto in {Patience.TotalSeconds:0} s: è stata ammessa ed è ferma sul cancello del gateway (entrate: {Llm.Entered})");
        }

        using var response = await request;
        return (response.StatusCode, await response.Content.ReadAsStringAsync(Ct));
    }

    /// <summary>
    /// Aspetta che <paramref name="count"/> richieste siano entrate nel gateway. Una richiesta che
    /// finisce prima è un guasto del test (un 4xx, un errore), e lo dice con il suo corpo.
    /// </summary>
    private async Task WaitForTheGatewayAsync(int count, params Task<HttpResponseMessage>[] requests)
    {
        var deadline = DateTime.UtcNow + Patience;
        while (Llm.Entered < count)
        {
            var early = requests.FirstOrDefault(r => r.IsCompleted);
            if (early is not null)
            {
                using var response = await early;
                var body = await response.Content.ReadAsStringAsync(Ct);
                throw new Xunit.Sdk.XunitException(
                    $"Una richiesta è finita prima di entrare nel gateway: {(int)response.StatusCode} {body}");
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new Xunit.Sdk.XunitException(
                    $"Nel gateway sono entrate {Llm.Entered} richieste su {count} attese in {Patience.TotalSeconds:0} s.");
            }

            await Task.Delay(25, Ct);
        }
    }

    private UserLibraryEntryEntity OwnedBy(Guid userId) => new()
    {
        UserId = userId,
        SharedGameId = _gameId,
        AddedAt = DateTime.UtcNow,
        OwnershipDeclaredAt = DateTime.UtcNow,
    };

    private async Task<string> SeedAnotherUserAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        db.UserLibraryEntries.Add(OwnedBy(userId));
        await db.SaveChangesAsync(Ct);
        return token;
    }

    private async Task<string> SeedPrivilegedUserAsync(string role)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, token) = role switch
        {
            "superadmin" => await TestSessionHelper.CreateSuperAdminSessionAsync(db, cancellationToken: Ct),
            "admin" => await TestSessionHelper.CreateAdminSessionAsync(db, cancellationToken: Ct),
            _ => await TestSessionHelper.CreateEditorSessionAsync(db, cancellationToken: Ct),
        };
        db.UserLibraryEntries.Add(OwnedBy(userId));
        await db.SaveChangesAsync(Ct);
        return token;
    }
}

/// <summary>
/// Un gateway LLM che non risponde finché il test non apre il cancello, e che conta quante richieste
/// sono entrate. Quando il cancello è aperto si comporta come <see cref="RecordingLlmService"/>.
/// </summary>
internal sealed class GatedLlmService : ILlmService
{
    private readonly RecordingLlmService _inner = new();
    private TaskCompletionSource _gate = NewGate();
    private int _entered;

    /// <summary>Quante richieste sono arrivate al gateway dall'ultimo <see cref="Reset"/>.</summary>
    public int Entered => Volatile.Read(ref _entered);

    public void Reset()
    {
        _inner.Reset();
        Interlocked.Exchange(ref _entered, 0);
        Volatile.Write(ref _gate, NewGate());
    }

    /// <summary>Apre il cancello: le richieste ferme proseguono, e quelle nuove non si fermano.</summary>
    public void Open() => Volatile.Read(ref _gate).TrySetResult();

    private static TaskCompletionSource NewGate() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private async Task PassAsync(CancellationToken ct)
    {
        Interlocked.Increment(ref _entered);
        await Volatile.Read(ref _gate).Task.WaitAsync(ct);
    }

    public async Task<LlmCompletionResult> GenerateAnswerAsync(LlmAnswerRequest request, CancellationToken ct = default)
    {
        await PassAsync(ct);
        return await _inner.GenerateAnswerAsync(request, ct);
    }

    public async IAsyncEnumerable<StreamChunk> GenerateAnswerStreamAsync(
        LlmAnswerRequest request, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await PassAsync(ct);
        await foreach (var chunk in _inner.GenerateAnswerStreamAsync(request, ct).WithCancellation(ct))
        {
            yield return chunk;
        }
    }

    public async IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await PassAsync(ct);
        await foreach (var chunk in _inner.GenerateCompletionStreamAsync(systemPrompt, userPrompt, source, ct)
                           .WithCancellation(ct))
        {
            yield return chunk;
        }
    }

    public async Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
    {
        await PassAsync(ct);
        return await _inner.GenerateCompletionAsync(systemPrompt, userPrompt, source, ct);
    }

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        where T : class
        => _inner.GenerateJsonAsync<T>(systemPrompt, userPrompt, source, ct);

    public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => _inner.GenerateMultimodalCompletionAsync(messages, source, ct);

    public IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => _inner.GenerateMultimodalCompletionStreamAsync(messages, source, ct);

    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel, string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
        => _inner.GenerateCompletionWithModelAsync(explicitModel, systemPrompt, userPrompt, source, maxTokens, ct);
}
