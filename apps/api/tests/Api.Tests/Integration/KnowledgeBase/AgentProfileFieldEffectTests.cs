using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.Reranking;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit;

namespace Api.Tests.Integration.KnowledgeBase;

/// <summary>
/// La fixture di <see cref="AgentProfileFieldEffectTests"/>: come quella della caratterizzazione
/// SSE, doppi solo ai margini, e un LLM e un reranker che registrano ciò che ricevono.
/// </summary>
public sealed class AgentProfileFieldEffectHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "profile_effect")
{
    internal RecordingLlmService Llm { get; } = new();
    internal RecordingReranker Reranker { get; } = new();
    internal InMemoryAiResponseCache Cache { get; } = new();

    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILlmService>();
            services.AddSingleton<ILlmService>(Llm);
            services.RemoveAll<IAiResponseCacheService>();
            services.AddSingleton<IAiResponseCacheService>(Cache);
            services.RemoveAll<ICrossEncoderReranker>();
            services.AddSingleton<ICrossEncoderReranker>(Reranker);
            services.RemoveAll<IEmbeddingService>();
            services.AddScoped<IEmbeddingService>(_ => QueryVectorEmbeddings.Create());
        }));
}

/// <summary>
/// Fetta 5 di ADR-095 (#4168), il «rosso prima»: ogni campo della versione pubblicata del profilo
/// cambia qualcosa che si osserva. Prima della pipeline il profilo aveva diciassette campi e un solo
/// lettore (la riserva del degrado): la temperatura restava 0,3 qualunque cosa si pubblicasse. Una
/// tabella invece di un test per la temperatura, perché un campo collegato non dimostra gli altri.
/// </summary>
/// <remarks>
/// Ogni caso pubblica una versione nuova con un solo campo cambiato, come farà l'admin, poi fa una
/// domanda su <c>/agents/qa/stream</c> e guarda ciò che arriva al gateway e al reranker, o ciò che
/// esce sul filo.
/// </remarks>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("Dependency", "PostgreSQL")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4168")]
public sealed class AgentProfileFieldEffectTests
    : IClassFixture<AgentProfileFieldEffectHostFixture>, IAsyncLifetime
{
    private const string Question = "How many cards does each player draw at setup?";
    private const string ItalianQuestion = "Quante carte pesca ogni giocatore durante la preparazione della partita?";

    private readonly AgentProfileFieldEffectHostFixture _hostFixture;
    private HttpClient _client = null!;
    private string _token = null!;
    private Guid _userId;
    private Guid _gameId;

    public AgentProfileFieldEffectTests(AgentProfileFieldEffectHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _hostFixture.Llm.Reset();
        _hostFixture.Reranker.Reset();
        _hostFixture.Cache.Clear();
        _client = _hostFixture.Factory.CreateClient();

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _token) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        _gameId = await TestSessionHelper.SeedSharedGameAsync(db, "Coloni del profilo");
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = _userId,
            SharedGameId = _gameId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow,
        });
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
        await SeedRulebookAsync(db);
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── La tabella ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Temperature_IsTheOneSentToTheGateway()
    {
        await PublishAsync(c => Modified(c, temperature: 1.1m));

        await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull("la risposta passa dalla pipeline, con i parametri del profilo");
        _hostFixture.Llm.LastAnswer!.Temperature.Should().Be(1.1);
    }

    [Fact]
    public async Task PrimaryModel_IsTheOneSentToTheGateway()
    {
        await PublishAsync(c => Modified(c, primaryModelId: "test/primary-model"));

        await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull();
        _hostFixture.Llm.LastAnswer!.PrimaryModelId.Should().Be("test/primary-model");
    }

    [Fact]
    public async Task FallbackModel_IsTheReserveSentToTheGateway()
    {
        await PublishAsync(c => Modified(c, fallbackModelId: "test/reserve-model"));

        await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull();
        _hostFixture.Llm.LastAnswer!.FallbackModelId.Should().Be("test/reserve-model");
    }

    [Fact]
    public async Task MaxResponseTokens_IsTheMaxTokensOfTheRequest()
    {
        await PublishAsync(c => Modified(c, maxResponseTokens: 321));

        await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull();
        _hostFixture.Llm.LastAnswer!.MaxTokens.Should().Be(321);
    }

    [Fact]
    public async Task FinalTopK_IsTheNumberOfPassagesInThePrompt()
    {
        await PublishAsync(c => Modified(c, finalTopK: 1));

        var events = await AskAsync(Question);

        CitationsOf(events).Should().ContainSingle();
        CountOf(_hostFixture.Llm.LastUserPrompt, "[Page ").Should().Be(1);
    }

    [Fact]
    public async Task CandidatePoolSize_IsTheNumberOfCandidatesTheRerankerSees()
    {
        await PublishAsync(c => Modified(c, candidatePoolSize: 2, finalTopK: 2));

        await AskAsync(Question);

        _hostFixture.Reranker.LastChunkCount.Should().Be(2, "il manuale ha tre chunk sopra la soglia, il pool ne ammette due");
    }

    [Fact]
    public async Task RerankerDisabled_TheRerankerIsNotCalled()
    {
        await PublishAsync(c => Modified(c, rerankerEnabled: false));

        var events = await AskAsync(Question);

        _hostFixture.Reranker.Calls.Should().Be(0);
        CitationsOf(events).Should().NotBeEmpty("senza reranking restano i primi K della fusione");
    }

    [Fact]
    public async Task SystemPrompt_InTheLanguageOfTheQuestion_IsTheOneSent()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
            ["it"] = "SENTINEL-IT. Rispondi dal regolamento.",
        }));

        await AskAsync(Question);
        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-EN");

        await AskAsync(ItalianQuestion);
        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-IT");
    }

    [Fact]
    public async Task SystemPrompt_WithoutThePromptOfTheQuestionLanguage_FallsBackToTheFallbackLanguage()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
        }));

        await AskAsync(ItalianQuestion);

        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-EN");
        // Il prompt è quello di ripiego, la risposta resta nella lingua di chi chiede.
        _hostFixture.Llm.LastSystemPrompt.Should().Contain(AnswerPromptBuilder.AnswerLanguageInstruction("it"));
    }

    [Fact]
    public async Task OneWordQuestion_UsesTheFirstAcceptLanguage()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
            ["it"] = "SENTINEL-IT. Rispondi dal regolamento.",
        }));

        await AskAsync("Setup?", acceptLanguage: "it-IT,it;q=0.9,en;q=0.8");

        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-IT");
    }

    [Theory]
    [InlineData(CitationStyle.PageReferencesInText)]
    [InlineData(CitationStyle.StructuredEvents)]
    public async Task CitationStyle_DecidesTheCitationInstruction(CitationStyle style)
    {
        await PublishAsync(c => Modified(c, citationStyle: style));

        await AskAsync(Question);

        _hostFixture.Llm.LastSystemPrompt.Should().Contain(AnswerPromptBuilder.CitationInstruction(style));
        _hostFixture.Llm.LastSystemPrompt.Should().NotContain(AnswerPromptBuilder.CitationInstruction(
            style == CitationStyle.PageReferencesInText ? CitationStyle.StructuredEvents : CitationStyle.PageReferencesInText));
    }

    // ─── La domanda singola (/knowledge-base/ask) ───────────────────────────────

    /// <summary>
    /// 5d: la domanda singola passa dagli stessi passi. Prima usava la strategia di instradamento,
    /// con un override per complessità, e la temperatura e la lunghezza compilate del gateway.
    /// </summary>
    [Fact]
    public async Task Ask_ModelTemperatureAndLength_ComeFromTheProfile()
    {
        await PublishAsync(c => Modified(c, primaryModelId: "test/primary-model", temperature: 1.1m, maxResponseTokens: 321));

        await AskSingleAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull("la domanda singola passa dalla pipeline, con i parametri del profilo");
        _hostFixture.Llm.LastAnswer!.PrimaryModelId.Should().Be("test/primary-model");
        _hostFixture.Llm.LastAnswer.Temperature.Should().Be(1.1);
        _hostFixture.Llm.LastAnswer.MaxTokens.Should().Be(321);
    }

    [Fact]
    public async Task Ask_SystemPrompt_IsTheOneOfTheQuestionLanguage()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
            ["it"] = "SENTINEL-IT. Rispondi dal regolamento.",
        }));

        await AskSingleAsync(ItalianQuestion);

        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-IT");
    }

    [Fact]
    public async Task Ask_FinalTopK_IsTheNumberOfPassagesInThePrompt()
    {
        await PublishAsync(c => Modified(c, finalTopK: 1));

        await AskSingleAsync(Question);

        CountOf(_hostFixture.Llm.LastUserPrompt, "[Page ").Should().Be(1);
    }

    [Fact]
    public async Task Ask_RerankerDisabled_TheRerankerIsNotCalled()
    {
        await PublishAsync(c => Modified(c, rerankerEnabled: false));

        await AskSingleAsync(Question);

        _hostFixture.Reranker.Calls.Should().Be(0);
    }

    /// <summary>
    /// Prima un rifiuto del gateway diventava una risposta vuota, servita con <c>success: true</c> e
    /// valutata dalla validazione come se fosse una risposta: ora è un errore che dice il motivo.
    /// </summary>
    [Fact]
    public async Task Ask_WhenNoModelAnswers_IsA503WithTheCode_NotAnEmptyAnswer()
    {
        _hostFixture.Llm.RefuseNext = true;

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
                new { gameId = _gameId.ToString(), query = Question }),
            Ct);

        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable, body);
        body.Should().Contain(LlmAnswerErrorCodes.ProviderUnavailable);
    }

    // ─── La ricerca nella libreria (/knowledge-base/ask/global) ─────────────────

    /// <summary>
    /// 5e-b: anche la ricerca fra i giochi passa dai passi della pipeline. Prima usava la strategia
    /// di instradamento con temperatura e lunghezza fisse, un prompt «tutor» suo, nessun reranker, e
    /// il numero di passaggi lo decideva la richiesta.
    /// </summary>
    [Fact]
    public async Task CrossGame_ModelTemperatureAndLength_ComeFromTheProfile()
    {
        await PublishAsync(c => Modified(c, primaryModelId: "test/primary-model", temperature: 1.1m, maxResponseTokens: 321));

        await AskLibraryAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull("la ricerca nella libreria passa dalla pipeline");
        _hostFixture.Llm.LastAnswer!.PrimaryModelId.Should().Be("test/primary-model");
        _hostFixture.Llm.LastAnswer.Temperature.Should().Be(1.1);
        _hostFixture.Llm.LastAnswer.MaxTokens.Should().Be(321);
    }

    [Fact]
    public async Task CrossGame_SystemPrompt_IsTheOneOfTheQuestionLanguage()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
            ["it"] = "SENTINEL-IT. Rispondi dal regolamento.",
        }));

        await AskLibraryAsync(ItalianQuestion);

        _hostFixture.Llm.LastSystemPrompt.Should().StartWith("SENTINEL-IT");
    }

    [Fact]
    public async Task CrossGame_TheCandidatesGoThroughTheReranker()
    {
        await AskLibraryAsync(Question);

        _hostFixture.Reranker.Calls.Should().Be(1, "il profilo del seed ha il reranker attivo");
    }

    [Fact]
    public async Task CrossGame_FinalTopK_IsTheNumberOfCitations()
    {
        await PublishAsync(c => Modified(c, finalTopK: 1));

        var events = await AskLibraryAsync(Question);

        CitationsOf(events).Should().ContainSingle();
    }

    // ─── Il registro: versione e lingua di ogni risposta (5f, ADR-095 D2) ────────

    /// <summary>
    /// La versione usata e la lingua della risposta escono con il completamento, così che chi
    /// salva la risposta e chi la valuta sappia con quale versione è stata prodotta.
    /// </summary>
    [Fact]
    public async Task Complete_CarriesTheVersionAndTheLanguageOfTheAnswer()
    {
        await PublishAsync(c => Modified(c, temperature: 0.4m));
        var published = await PublishedVersionIdAsync();

        var events = await AskAsync(ItalianQuestion);

        var complete = events.Single(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Complete)
            .GetProperty("data");
        complete.GetProperty("profileVersionId").GetGuid().Should().Be(published);
        complete.GetProperty("language").GetString().Should().Be("it");
    }

    /// <summary>La riga di <c>ai_request_logs</c> della chat porta la versione e la lingua.</summary>
    [Fact]
    public async Task RequestLog_RecordsTheVersionAndTheLanguage()
    {
        var published = await PublishedVersionIdAsync();

        await AskAsync(ItalianQuestion);

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = await db.AiRequestLogs.AsNoTracking()
            .Where(l => l.Endpoint == "qa-stream")
            .OrderByDescending(l => l.CreatedAt)
            .FirstAsync(Ct);
        log.ProfileVersionId.Should().Be(published);
        log.Language.Should().Be("it");
    }

    /// <summary>
    /// La chat salva la risposta dopo lo stream: il messaggio conserva la versione e la lingua che
    /// il completamento gli ha dato, e le restituisce quando il thread si rilegge.
    /// </summary>
    [Fact]
    public async Task ChatMessage_KeepsTheVersionAndTheLanguage()
    {
        var published = await PublishedVersionIdAsync();
        var created = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", _token, new { gameId = _gameId, title = "Profilo" }), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        using var thread = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", _token,
            new { content = "Five cards.", role = "assistant", profileVersionId = published, language = "it" }), Ct);
        var addedBody = await added.Content.ReadAsStringAsync(Ct);
        added.IsSuccessStatusCode.Should().BeTrue(addedBody);
        // anche il thread restituito dal salvataggio: è da lì che la chat prende l'id del messaggio
        using (var addedJson = JsonDocument.Parse(addedBody))
        {
            var saved = addedJson.RootElement.GetProperty("messages").EnumerateArray()
                .Single(m => m.GetProperty("role").GetString() == "assistant");
            saved.GetProperty("profileVersionId").GetGuid().Should().Be(published);
            saved.GetProperty("language").GetString().Should().Be("it");
        }

        var reread = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Get, $"/api/v1/chat-threads/{threadId}", _token), Ct);
        using var body = JsonDocument.Parse(await reread.Content.ReadAsStringAsync(Ct));
        var message = body.RootElement.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("role").GetString() == "assistant");
        message.GetProperty("profileVersionId").GetGuid().Should().Be(published);
        message.GetProperty("language").GetString().Should().Be("it");
    }

    /// <summary>La risposta non in streaming porta anch'essa la versione e la lingua.</summary>
    [Fact]
    public async Task SingleAnswer_CarriesTheVersionAndTheLanguage()
    {
        var published = await PublishedVersionIdAsync();

        var answer = await AskSingleAsync(ItalianQuestion);

        answer.GetProperty("profileVersionId").GetGuid().Should().Be(published);
        answer.GetProperty("language").GetString().Should().Be("it");
    }

    /// <summary>
    /// <c>/agents/qa</c> risponde con gli stessi passi e scrive la sua riga di
    /// <c>ai_request_logs</c>: anche quella porta la versione e la lingua.
    /// </summary>
    [Fact]
    public async Task SingleAnswerRequestLog_RecordsTheVersionAndTheLanguage()
    {
        var published = await PublishedVersionIdAsync();

        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/agents/qa?generateFollowUps=false", _token,
                new { gameId = _gameId.ToString(), query = ItalianQuestion }),
            Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var json = JsonDocument.Parse(body);
        json.RootElement.GetProperty("profileVersionId").GetGuid().Should().Be(published);
        json.RootElement.GetProperty("language").GetString().Should().Be("it");

        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var log = await db.AiRequestLogs.AsNoTracking()
            .Where(l => l.Endpoint == "qa")
            .OrderByDescending(l => l.CreatedAt)
            .FirstAsync(Ct);
        log.ProfileVersionId.Should().Be(published);
        log.Language.Should().Be("it");
    }

    /// <summary>
    /// Il feedback su una risposta salvata porta la versione e la lingua di quella risposta, prese
    /// dal messaggio: è il terzo posto di ADR-095 D2.
    /// </summary>
    [Fact]
    public async Task Feedback_RecordsTheVersionAndTheLanguageOfTheAnswer()
    {
        var published = await PublishedVersionIdAsync();
        var (threadId, messageId) = await SaveAnswerAsync(published, "it");

        var response = await SendFeedbackAsync(threadId, messageId);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent, await response.Content.ReadAsStringAsync(Ct));
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var feedback = await db.KbUserFeedbacks.AsNoTracking().SingleAsync(f => f.MessageId == messageId, Ct);
        feedback.ProfileVersionId.Should().Be(published);
        feedback.Language.Should().Be("it");
    }

    /// <summary>
    /// Un feedback su un messaggio che non c'è non si registra: prima finiva in tabella con un id
    /// qualunque, e non c'era modo di ricondurlo a una risposta.
    /// </summary>
    [Fact]
    public async Task Feedback_OnAMessageThatIsNotInTheThread_IsNotFound()
    {
        var (threadId, _) = await SaveAnswerAsync(await PublishedVersionIdAsync(), "it");

        var response = await SendFeedbackAsync(threadId, Guid.NewGuid());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound, await response.Content.ReadAsStringAsync(Ct));
    }

    private async Task<(Guid ThreadId, Guid MessageId)> SaveAnswerAsync(Guid profileVersionId, string language)
    {
        var created = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", _token, new { gameId = _gameId, title = "Feedback" }), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        using var thread = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", _token,
            new { content = "Five cards.", role = "assistant", profileVersionId, language }), Ct);
        var body = await added.Content.ReadAsStringAsync(Ct);
        added.IsSuccessStatusCode.Should().BeTrue(body);
        using var saved = JsonDocument.Parse(body);
        var messageId = saved.RootElement.GetProperty("messages").EnumerateArray()
            .Single(m => m.GetProperty("role").GetString() == "assistant")
            .GetProperty("id").GetGuid();
        return (threadId, messageId);
    }

    private Task<HttpResponseMessage> SendFeedbackAsync(Guid threadId, Guid messageId) =>
        _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/games/{_gameId}/knowledge-base/feedback", _token,
            new { chatSessionId = threadId, messageId, outcome = "helpful" }), Ct);

    private async Task<Guid> PublishedVersionIdAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
        return profile!.Published.Id;
    }

    // ─── La cache delle risposte ────────────────────────────────────────────────

    /// <summary>
    /// La chiave della cache era (gioco, domanda). Con la lingua che dipende da
    /// <c>Accept-Language</c> sulle domande brevi, la risposta in inglese a «Setup?» sarebbe stata
    /// riservita a chi chiede in italiano.
    /// </summary>
    [Fact]
    public async Task Cache_DoesNotServeAnAnswerInAnotherLanguage()
    {
        await PublishAsync(c => Modified(c, systemPrompts: new Dictionary<string, string>
        {
            ["en"] = "SENTINEL-EN. Answer from the rulebook.",
            ["it"] = "SENTINEL-IT. Rispondi dal regolamento.",
        }));
        await AskAsync("Setup?", acceptLanguage: "en-US");
        _hostFixture.Llm.Reset();

        await AskAsync("Setup?", acceptLanguage: "it-IT");

        _hostFixture.Llm.LastSystemPrompt.Should().NotBeNull("la risposta in italiano non è quella in cache")
            .And.StartWith("SENTINEL-IT");
    }

    /// <summary>
    /// Una versione pubblicata vale dalla domanda successiva (D2): la cache non riserve per 24 ore
    /// le risposte prodotte con la versione precedente.
    /// </summary>
    [Fact]
    public async Task Cache_DoesNotServeAnAnswerOfThePreviousVersion()
    {
        await AskAsync(Question);
        await PublishAsync(c => Modified(c, temperature: 1.1m));
        _hostFixture.Llm.Reset();

        await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull("la versione nuova risponde da sé");
        _hostFixture.Llm.LastAnswer!.Temperature.Should().Be(1.1);
    }

    [Fact]
    public async Task Cache_StillServesTheSameQuestionInTheSameLanguageAndVersion()
    {
        await AskAsync(Question);
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question);

        _hostFixture.Llm.LastAnswer.Should().BeNull("la seconda risposta viene dalla cache");
        events[0].GetProperty("data").GetProperty("message").GetString().Should().Be("Retrieved from cache");
    }

    // ─── La cronologia del thread (#4242) ───────────────────────────────────────
    // Stanno qui perché vogliono lo stesso banco: la richiesta HTTP, Postgres, e il prompt che
    // arriva al modello. I test unit fissano la regola; questi, che la domanda la attraversi.

    private const string ThreadSecret = "ZAFFIRO-7731";

    /// <summary>
    /// Il controllo: la cronologia del proprio thread entra nel prompt. Senza questa direzione una
    /// guardia troppo larga spegnerebbe la cronologia a tutti, e nessun test se ne accorgerebbe.
    /// </summary>
    [Fact]
    [Trait("Issue", "4242")]
    public async Task ThreadHistory_OfTheAskersOwnThread_IsInThePrompt()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");

        await AskAsync(Question, chatId: threadId);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("il modello è stato interrogato")
            .And.Contain(ThreadSecret);
    }

    /// <summary>
    /// Un altro utente, sullo stesso gioco, nomina quel thread nella sua domanda: riceve la sua
    /// risposta, senza la conversazione di chi il thread l'ha scritto.
    /// </summary>
    [Fact]
    [Trait("Issue", "4242")]
    public async Task ThreadHistory_OfAnotherUsersThread_IsNotInThePrompt()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");
        var strangerToken = await CreateAnotherOwnerOfTheGameAsync();

        await AskAsync(Question, chatId: threadId, token: strangerToken);

        _hostFixture.Llm.LastUserPrompt.Should().NotBeNull("la domanda riceve comunque la sua risposta")
            .And.NotContain(ThreadSecret);
    }

    // ─── La cache e ciò che dà forma alla risposta (#4279) ───
    // La cache è condivisa fra gli utenti. La chiave portava gioco, lingua, versione e domanda; ma
    // la risposta dipende anche dalla cronologia del thread e dal filtro per documento.

    private const string FollowUp = "And how many with three players?";

    private static string? FirstStateOf(IReadOnlyList<JsonElement> events) =>
        events[0].GetProperty("data").TryGetProperty("message", out var message) ? message.GetString() : null;

    /// <summary>
    /// Una risposta nata con la conversazione di un utente nel prompt non viene riservita a chi fa
    /// la stessa domanda senza quella conversazione. Non serve nominare il thread altrui: bastava il
    /// testo della domanda.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_DoesNotServeAnAnswerBuiltOnAnotherUsersConversation()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");
        await AskAsync(Question, chatId: threadId);
        _hostFixture.Llm.LastUserPrompt.Should().Contain(ThreadSecret, "la prima risposta nasce con la cronologia");
        var strangerToken = await CreateAnotherOwnerOfTheGameAsync();
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, token: strangerToken);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull($"primo evento: «{FirstStateOf(events)}»");
        _hostFixture.Llm.LastUserPrompt.Should().NotContain(ThreadSecret);
    }

    /// <summary>
    /// Una domanda di seguito dipende dai turni precedenti: la stessa frase, in due conversazioni
    /// diverse, non riceve la stessa risposta dalla cache.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_DoesNotServeAFollowUpTheAnswerOfAnotherConversation()
    {
        var aboutSetup = await CreateThreadWithMessageAsync("We are setting up the base game.");
        await AskAsync(FollowUp, chatId: aboutSetup);
        var aboutVariant = await CreateThreadWithMessageAsync($"We play the variant called {ThreadSecret}.");
        _hostFixture.Llm.Reset();

        var events = await AskAsync(FollowUp, chatId: aboutVariant);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull($"primo evento: «{FirstStateOf(events)}»");
        _hostFixture.Llm.LastUserPrompt.Should().Contain(ThreadSecret, "la risposta nasce dalla sua conversazione");
    }

    /// <summary>
    /// Il controllo. La chat salva la domanda nel thread prima di chiedere, quindi una prima
    /// domanda ha sempre una cronologia: la domanda stessa. Due utenti che fanno la stessa prima
    /// domanda hanno la stessa cronologia, e continuano a condividere la cache. Senza questa
    /// direzione una correzione troppo larga spegnerebbe la cache della chat senza che nessun test
    /// lo noti.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_IsStillSharedByTwoUsersAskingTheSameFirstQuestionInTheirOwnThreads()
    {
        var mine = await CreateThreadWithMessageAsync(Question);
        await AskAsync(Question, chatId: mine);
        var strangerToken = await CreateAnotherOwnerOfTheGameAsync();
        var theirs = await CreateThreadWithMessageAsync(Question, strangerToken);
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, chatId: theirs, token: strangerToken);

        _hostFixture.Llm.LastAnswer.Should().BeNull("la stessa prima domanda viene dalla cache");
        FirstStateOf(events).Should().Be("Retrieved from cache");
    }

    /// <summary>
    /// Il filtro per documento decide da quali passaggi nasce la risposta: una domanda filtrata non
    /// riceve la risposta in cache di quella non filtrata.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_DoesNotServeAFilteredQuestionTheAnswerOfTheUnfilteredOne()
    {
        await AskAsync(Question);
        var documentId = await TheVectorDocumentOfTheGameAsync();
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, documentIds: [documentId]);

        _hostFixture.Llm.LastAnswer.Should().NotBeNull($"primo evento: «{FirstStateOf(events)}»");
    }

    /// <summary>Il controllo: lo stesso filtro, la stessa domanda, la stessa risposta dalla cache.</summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_StillServesTheSameFilteredQuestion()
    {
        var documentId = await TheVectorDocumentOfTheGameAsync();
        await AskAsync(Question, documentIds: [documentId]);
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, documentIds: [documentId]);

        _hostFixture.Llm.LastAnswer.Should().BeNull("la stessa domanda con lo stesso filtro viene dalla cache");
        FirstStateOf(events).Should().Be("Retrieved from cache");
    }

    /// <summary>
    /// «Continua» manda una domanda VUOTA e la risposta parziale da proseguire. Con la domanda sola
    /// nella chiave, tutte le continuazioni di un gioco avevano la stessa chiave: la seconda
    /// riceveva il seguito della prima.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_DoesNotServeAContinuationTheSequelOfAnotherAnswer()
    {
        await AskAsync(string.Empty, continuationToken: ContinuationOf("Each player draws five cards and then"));
        _hostFixture.Llm.Reset();

        var events = await AskAsync(
            string.Empty, continuationToken: ContinuationOf($"The variant called {ThreadSecret} changes the setup:"));

        _hostFixture.Llm.LastAnswer.Should().NotBeNull($"primo evento: «{FirstStateOf(events)}»");
        _hostFixture.Llm.LastUserPrompt.Should().Contain(ThreadSecret, "si prosegue la risposta di questa richiesta");
    }

    /// <summary>
    /// Lo stile cambia le istruzioni al modello: chi chiede una risposta estesa non riceve dalla
    /// cache quella concisa.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_DoesNotServeAConciseAnswerToADetailedRequest()
    {
        await AskAsync(Question, responseStyle: "concise");
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, responseStyle: "detailed");

        _hostFixture.Llm.LastSystemPrompt.Should().NotBeNull($"primo evento: «{FirstStateOf(events)}»")
            .And.Contain("Answer thoroughly");
    }

    /// <summary>
    /// Un thread che non è di chi chiede non entra nel prompt (#4242): la domanda è quindi quella di
    /// chi non nomina alcun thread, e ne condivide la risposta in cache.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_TreatsARefusedThreadAsNoThread()
    {
        var threadId = await CreateThreadWithMessageAsync($"La nostra variante si chiama {ThreadSecret}.");
        var strangerToken = await CreateAnotherOwnerOfTheGameAsync();
        await AskAsync(Question, token: strangerToken);
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, chatId: threadId, token: strangerToken);

        _hostFixture.Llm.LastAnswer.Should().BeNull("il thread altrui non conta: è la stessa domanda di prima");
        FirstStateOf(events).Should().Be("Retrieved from cache");
    }

    /// <summary>
    /// Il filtro per documento in forma canonica: un elenco vuoto è «nessun filtro», e lo stesso
    /// documento nominato due volte è lo stesso filtro.
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_ReadsTheDocumentFilterInCanonicalForm()
    {
        var documentId = await TheVectorDocumentOfTheGameAsync();
        await AskAsync(Question);
        await AskAsync(Question, documentIds: [documentId]);
        _hostFixture.Llm.Reset();

        var unfiltered = await AskAsync(Question, documentIds: []);
        var sameDocumentTwice = await AskAsync(Question, documentIds: [documentId, documentId]);

        _hostFixture.Llm.LastAnswer.Should().BeNull("nessuna delle due domande è nuova");
        FirstStateOf(unfiltered).Should().Be("Retrieved from cache");
        FirstStateOf(sameDocumentTwice).Should().Be("Retrieved from cache");
    }

    /// <summary>
    /// Uno stile che il costruttore del prompt non conosce riceve le istruzioni di quello conciso:
    /// stesso prompt, stessa risposta, stessa voce di cache. (Senza stile la rotta manda già
    /// «concise», quindi quel caso non distingue nulla.)
    /// </summary>
    [Fact]
    [Trait("Issue", "4279")]
    public async Task Cache_TreatsAnUnknownStyleAsConcise()
    {
        await AskAsync(Question, responseStyle: "concise");
        _hostFixture.Llm.Reset();

        var events = await AskAsync(Question, responseStyle: "bullet-points");

        _hostFixture.Llm.LastAnswer.Should().BeNull("uno stile sconosciuto riceve le stesse istruzioni di quello conciso");
        FirstStateOf(events).Should().Be("Retrieved from cache");
    }

    /// <summary>Il token che il client manda con «Continua»: la risposta parziale, in base64.</summary>
    private static string ContinuationOf(string partialAnswer) =>
        Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(new { partialAnswer })));

    private async Task<Guid> TheVectorDocumentOfTheGameAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.VectorDocuments.Where(v => v.GameId == _gameId).Select(v => v.Id).SingleAsync(Ct);
    }

    /// <summary>Un thread sul gioco del test, con un messaggio dentro: dell'utente del test, o di chi porta il token.</summary>
    private async Task<Guid> CreateThreadWithMessageAsync(string content, string? token = null)
    {
        token ??= _token;
        var created = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/chat-threads", token, new { gameId = _gameId, title = "Cronologia" }), Ct);
        created.StatusCode.Should().Be(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Ct));
        using var thread = JsonDocument.Parse(await created.Content.ReadAsStringAsync(Ct));
        var threadId = thread.RootElement.GetProperty("id").GetGuid();

        var added = await _client.SendAsync(TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, $"/api/v1/chat-threads/{threadId}/messages", token,
            new { content, role = "user" }), Ct);
        added.IsSuccessStatusCode.Should().BeTrue(await added.Content.ReadAsStringAsync(Ct));
        return threadId;
    }

    /// <summary>
    /// Un secondo utente che possiede lo stesso gioco: può fare domande su di esso, quindi ciò che
    /// lo ferma sul thread altrui è la proprietà del thread e non l'accesso al gioco.
    /// </summary>
    private async Task<string> CreateAnotherOwnerOfTheGameAsync()
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (strangerId, strangerToken) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        db.UserLibraryEntries.Add(new UserLibraryEntryEntity
        {
            UserId = strangerId,
            SharedGameId = _gameId,
            AddedAt = DateTime.UtcNow,
            OwnershipDeclaredAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync(Ct);
        return strangerToken;
    }

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private async Task PublishAsync(Func<AgentProfileContent, AgentProfileContent> change)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
        profile.Should().NotBeNull("le migrazioni pubblicano la prima versione del profilo");
        profile!.CreateDraft(change(profile.Published.Content));
        profile.PublishDraft(new HashSet<string>(StringComparer.Ordinal));
        await db.SaveChangesAsync(Ct);
    }

    private static AgentProfileContent Modified(
        AgentProfileContent c,
        string? primaryModelId = null,
        string? fallbackModelId = null,
        decimal? temperature = null,
        int? maxResponseTokens = null,
        CitationStyle? citationStyle = null,
        IReadOnlyDictionary<string, string>? systemPrompts = null,
        int? candidatePoolSize = null,
        int? finalTopK = null,
        bool? rerankerEnabled = null) =>
        AgentProfileContent.Create(
            c.Name,
            c.Description,
            c.Persona,
            primaryModelId ?? c.PrimaryModelId,
            fallbackModelId ?? c.FallbackModelId,
            temperature ?? c.Temperature,
            maxResponseTokens ?? c.MaxResponseTokens,
            citationStyle ?? c.CitationStyle,
            systemPrompts ?? c.SystemPrompts,
            c.FallbackLanguage,
            candidatePoolSize ?? c.CandidatePoolSize,
            finalTopK ?? c.FinalTopK,
            c.MinScore,
            c.VectorWeight,
            c.KeywordWeight,
            rerankerEnabled ?? c.RerankerEnabled,
            c.AllowedCategories.ToList(),
            c.SessionAssistant);

    private async Task<List<JsonElement>> AskAsync(
        string query, string? acceptLanguage = null, Guid? chatId = null, string? token = null,
        IReadOnlyList<Guid>? documentIds = null, string? continuationToken = null, string? responseStyle = null)
    {
        // Il thread e il filtro entrano nel corpo solo quando ci sono: le altre domande restano
        // quelle di sempre.
        var payload = new Dictionary<string, object?>
        {
            ["gameId"] = _gameId.ToString(),
            ["query"] = query,
        };
        if (chatId is not null) payload["chatId"] = chatId;
        if (documentIds is not null) payload["documentIds"] = documentIds;
        if (continuationToken is not null) payload["continuationToken"] = continuationToken;
        if (responseStyle is not null) payload["responseStyle"] = responseStyle;
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", token ?? _token, payload);
        if (acceptLanguage is not null)
        {
            request.Headers.Add("Accept-Language", acceptLanguage);
        }

        var response = await _client.SendAsync(request, Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            events.Add(doc.RootElement.Clone());
        }

        events.Should().NotContain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error, body);
        return events;
    }

    private async Task<List<JsonElement>> AskLibraryAsync(string query)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask/global", _token, new { query }),
            Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);

        var events = new List<JsonElement>();
        foreach (var line in body.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("data:", StringComparison.Ordinal)) continue;
            using var doc = JsonDocument.Parse(trimmed["data:".Length..].Trim());
            events.Add(doc.RootElement.Clone());
        }

        events.Should().NotContain(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Error, body);
        return events;
    }

    private async Task<JsonElement> AskSingleAsync(string query)
    {
        var response = await _client.SendAsync(
            TestSessionHelper.CreateAuthenticatedRequest(
                HttpMethod.Post, "/api/v1/knowledge-base/ask", _token,
                new { gameId = _gameId.ToString(), query }),
            Ct);
        var body = await response.Content.ReadAsStringAsync(Ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK, body);
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static List<JsonElement> CitationsOf(IEnumerable<JsonElement> events) =>
        events.Single(e => e.GetProperty("type").GetInt32() == (int)StreamingEventType.Citations)
            .GetProperty("data").GetProperty("citations").EnumerateArray().ToList();

    private static int CountOf(string? text, string fragment) =>
        text is null ? 0 : (text.Length - text.Replace(fragment, string.Empty, StringComparison.Ordinal).Length) / fragment.Length;

    /// <summary>
    /// Tre chunk del manuale, tutti sopra la soglia del braccio vettoriale (coseno 1, 0,8 e 0,7) e
    /// con parole della domanda, così che il pool di candidati e il top-K finale si vedano.
    /// </summary>
    private async Task SeedRulebookAsync(MeepleAiDbContext db)
    {
        var pdfId = Guid.NewGuid();
        db.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = pdfId,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            FileSizeBytes = 1024,
            UploadedByUserId = _userId,
            ProcessingState = "Ready",
            ProcessedAt = DateTime.UtcNow,
            SharedGameId = _gameId,
        });

        var vectorDocumentId = Guid.NewGuid();
        db.VectorDocuments.Add(new VectorDocumentEntity
        {
            Id = vectorDocumentId,
            GameId = _gameId,
            PdfDocumentId = pdfId,
            ChunkCount = 3,
            TotalCharacters = 300,
            IndexingStatus = "completed",
            IndexedAt = DateTime.UtcNow,
            EmbeddingModel = "test-model",
            EmbeddingDimensions = QueryVectorEmbeddings.Dimensions,
        });

        var chunks = new[]
        {
            (Page: 3, Text: "At setup each player draws five cards and keeps them hidden.", Vector: QueryVectorEmbeddings.Axis(1f, 0f)),
            (Page: 4, Text: "Each player draws one card at the start of every turn.", Vector: QueryVectorEmbeddings.Axis(0.8f, 0.6f)),
            (Page: 9, Text: "A player who has no cards left draws two cards at setup of the next round.", Vector: QueryVectorEmbeddings.Axis(0.7f, 0.714f)),
        };

        var entities = chunks.Select((c, i) => new TextChunkEntity
        {
            Id = Guid.NewGuid(),
            GameId = _gameId,
            PdfDocumentId = pdfId,
            Content = c.Text,
            ChunkIndex = i,
            PageNumber = c.Page,
            CharacterCount = c.Text.Length,
        }).ToList();
        db.TextChunks.AddRange(entities);
        await db.SaveChangesAsync(Ct);

        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            await connection.OpenAsync(Ct);

        for (var i = 0; i < entities.Count; i++)
        {
            await using var command = (NpgsqlCommand)connection.CreateCommand();
            command.CommandText = """
                INSERT INTO pgvector_embeddings
                    (id, vector_document_id, game_id, text_content, vector, model,
                     chunk_index, page_number, created_at, lang, source_chunk_id, is_translation, role_tags)
                VALUES
                    (@id, @vdId, @gameId, @text, @vec, 'test-model',
                     @chunkIndex, @pageNumber, NOW(), 'en', @sourceChunkId, false, 0)
                """;
            command.Parameters.AddWithValue("@id", Guid.NewGuid());
            command.Parameters.AddWithValue("@vdId", vectorDocumentId);
            command.Parameters.AddWithValue("@gameId", _gameId);
            command.Parameters.AddWithValue("@text", entities[i].Content);
            command.Parameters.AddWithValue("@vec", new Pgvector.Vector(chunks[i].Vector));
            command.Parameters.AddWithValue("@chunkIndex", i);
            command.Parameters.AddWithValue("@pageNumber", chunks[i].Page);
            command.Parameters.AddWithValue("@sourceChunkId", entities[i].Id);
            await command.ExecuteNonQueryAsync(Ct);
        }
    }
}

/// <summary>
/// Un LLM che registra ciò che riceve: la richiesta di risposta della pipeline, e i prompt per
/// qualunque via arrivino. Risponde sempre con la stessa frase.
/// </summary>
internal sealed class RecordingLlmService : ILlmService
{
    public LlmAnswerRequest? LastAnswer { get; private set; }
    public string? LastSystemPrompt { get; private set; }
    public string? LastUserPrompt { get; private set; }

    /// <summary>Se vero, la prossima risposta fallisce come un gateway senza modelli disponibili.</summary>
    public bool RefuseNext { get; set; }

    public void Reset()
    {
        LastAnswer = null;
        LastSystemPrompt = null;
        LastUserPrompt = null;
        RefuseNext = false;
    }

    public Task<LlmCompletionResult> GenerateAnswerAsync(LlmAnswerRequest request, CancellationToken ct = default)
    {
        LastAnswer = request;
        return Complete(request.SystemPrompt, request.UserPrompt);
    }

    private Task<LlmCompletionResult> Complete(string systemPrompt, string userPrompt)
    {
        LastSystemPrompt = systemPrompt;
        LastUserPrompt = userPrompt;
        if (RefuseNext)
        {
            var failure = LlmCompletionResult.CreateFailure("No AI model could answer right now.");
            return Task.FromResult(failure with
            {
                Metadata = new Dictionary<string, string> { ["error_code"] = LlmAnswerErrorCodes.ProviderUnavailable },
            });
        }

        return Task.FromResult(LlmCompletionResult.CreateSuccess(
            "Five cards.",
            new LlmUsage(100, 3, 103),
            new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "test-model", Provider = "Test" }));
    }

    public IAsyncEnumerable<StreamChunk> GenerateAnswerStreamAsync(LlmAnswerRequest request, CancellationToken ct = default)
    {
        LastAnswer = request;
        return Answer(request.SystemPrompt, request.UserPrompt, ct);
    }

    public IAsyncEnumerable<StreamChunk> GenerateCompletionStreamAsync(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default) =>
        Answer(systemPrompt, userPrompt, ct);

    private async IAsyncEnumerable<StreamChunk> Answer(
        string systemPrompt, string userPrompt, [EnumeratorCancellation] CancellationToken ct)
    {
        LastSystemPrompt = systemPrompt;
        LastUserPrompt = userPrompt;
        await Task.Yield();
        yield return new StreamChunk("Five cards.");
        yield return new StreamChunk(null, new LlmUsage(100, 3, 103),
            new LlmCost { InputCost = 0m, OutputCost = 0m, ModelId = "test-model", Provider = "Test" },
            IsFinal: true, FinishReason: "stop");
    }

    public Task<LlmCompletionResult> GenerateCompletionAsync(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Complete(systemPrompt, userPrompt);

    public Task<T?> GenerateJsonAsync<T>(
        string systemPrompt, string userPrompt, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        where T : class
        => Task.FromResult<T?>(null);

    public Task<LlmCompletionResult> GenerateMultimodalCompletionAsync(
        IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));

    public async IAsyncEnumerable<StreamChunk> GenerateMultimodalCompletionStreamAsync(
        IReadOnlyList<LlmMessage> messages, RequestSource source = RequestSource.Manual,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<LlmCompletionResult> GenerateCompletionWithModelAsync(
        string explicitModel, string systemPrompt, string userPrompt,
        RequestSource source = RequestSource.Manual, int? maxTokens = null, CancellationToken ct = default)
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));
}

/// <summary>Un reranker che non riordina e registra quante volte è chiamato e su quanti chunk.</summary>
internal sealed class RecordingReranker : ICrossEncoderReranker
{
    private int _calls;

    public int Calls => Volatile.Read(ref _calls);
    public int? LastChunkCount { get; private set; }

    public void Reset()
    {
        Interlocked.Exchange(ref _calls, 0);
        LastChunkCount = null;
    }

    public Task<RerankResult> RerankAsync(
        string query, IReadOnlyList<RerankChunk> chunks, int? topK = null, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _calls);
        LastChunkCount = chunks.Count;
        var kept = chunks.Take(topK ?? chunks.Count)
            .Select(c => new RerankedChunk(c.Id, c.Content, c.OriginalScore, c.OriginalScore, c.Metadata))
            .ToList();
        return Task.FromResult(new RerankResult(kept, "recording", 0));
    }

    public Task<bool> IsHealthyAsync(CancellationToken cancellationToken = default) => Task.FromResult(true);
}
