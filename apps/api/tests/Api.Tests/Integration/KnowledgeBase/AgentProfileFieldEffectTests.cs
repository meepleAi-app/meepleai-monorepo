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

    // ─── Supporto ───────────────────────────────────────────────────────────────

    private async Task PublishAsync(Func<AgentProfileContent, AgentProfileContent> change)
    {
        using var scope = _hostFixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var profile = await scope.ServiceProvider.GetRequiredService<IAgentProfileRepository>().GetAsync(Ct);
        profile.Should().NotBeNull("le migrazioni pubblicano la prima versione del profilo");
        profile!.CreateDraft(change(profile.Published.Content));
        profile.PublishDraft();
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
            c.AllowedCategories.ToList());

    private async Task<List<JsonElement>> AskAsync(string query, string? acceptLanguage = null)
    {
        var request = TestSessionHelper.CreateAuthenticatedRequest(
            HttpMethod.Post, "/api/v1/agents/qa/stream?generateFollowUps=false", _token,
            new { gameId = _gameId.ToString(), query });
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

    public void Reset()
    {
        LastAnswer = null;
        LastSystemPrompt = null;
        LastUserPrompt = null;
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
        => Task.FromResult(LlmCompletionResult.CreateFailure("Not scripted"));

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
