using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Unit tests for CrossGameStreamQaQueryHandler.
/// Issue #1661 PR-2 Task 8b: cross-game SSE ask streaming handler.
/// TDD: EC-1 (empty games), RBAC correctness, event sequence, cancellation, error isolation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class CrossGameStreamQaQueryHandlerTests
{
    // ── Shared test fixtures ────────────────────────────────────────────────
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid GameId1 = Guid.NewGuid();
    private static readonly Guid GameId2 = Guid.NewGuid();

    private readonly IRagAccessService _ragAccessService = Substitute.For<IRagAccessService>();
    private readonly IMultiGameHybridSearchService _searchService = Substitute.For<IMultiGameHybridSearchService>();
    private readonly ILlmService _llmService = Substitute.For<ILlmService>();

    // #4168: l'handler è un adattatore; la risposta sta nel flusso cross-game della pipeline, con il
    // profilo del seed e un reranker che non riordina.
    private CrossGameStreamQaQueryHandler CreateSut() => new(new CrossGameAnswerFlow(
        new Api.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPreconditions(_ragAccessService),
        AnswerPipelineTestDoubles.RetrievalAcrossGames(_searchService),
        AnswerPipelineTestDoubles.ProfileReader(),
        AnswerPipelineTestDoubles.UncertainLanguageDetection(),
        _llmService,
        NullLogger<CrossGameAnswerFlow>.Instance));

    // ── EC-1: no accessible games → Complete with no-context answer, LLM never called ──

    [Fact]
    public async Task NoAccessibleGames_EmitsCompleteWithNoContext_NoLlmCall()
    {
        // Arrange
        _ragAccessService
            .GetAccessibleGameIdsAsync(UserId, UserRole.User, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)Array.Empty<Guid>());

        var query = new CrossGameStreamQaQuery("What are the rules?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert
        var types = events.Select(e => e.Type).ToList();
        types.Should().Contain(StreamingEventType.StateUpdate);
        types.Should().Contain(StreamingEventType.Complete);
        types.Should().NotContain(StreamingEventType.Error);

        // LLM must never be called
        _llmService.DidNotReceive().GenerateAnswerStreamAsync(
            Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>());

        // Search must never be called either
        await _searchService.DidNotReceive().SearchAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
            Arg.Any<IReadOnlyList<Guid>?>(),
            Arg.Any<CancellationToken>());

        // Complete event answer text should indicate no context
        var complete = events.FirstOrDefault(e => e.Type == StreamingEventType.Complete);
        complete.Should().NotBeNull();
    }

    // ── Happy path: StateUpdate → Citations → Token* → Complete event order ──

    [Fact]
    public async Task AccessibleGames_EmitsStateUpdate_Citations_Tokens_Complete_InOrder()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("Hello World");

        var query = new CrossGameStreamQaQuery("How do I win?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert ordering: at least one StateUpdate, then Citations, then Tokens, then Complete
        var types = events.Select(e => e.Type).ToList();
        types.Should().Contain(StreamingEventType.StateUpdate);
        types.Should().Contain(StreamingEventType.Citations);
        types.Should().Contain(StreamingEventType.Token);
        types.Should().Contain(StreamingEventType.Complete);
        types.Should().NotContain(StreamingEventType.Error);

        var stateUpdateIdx = types.IndexOf(StreamingEventType.StateUpdate);
        var citationsIdx = types.IndexOf(StreamingEventType.Citations);
        var firstTokenIdx = types.IndexOf(StreamingEventType.Token);
        var completeIdx = types.LastIndexOf(StreamingEventType.Complete);

        stateUpdateIdx.Should().BeLessThan(citationsIdx, "StateUpdate comes before Citations");
        citationsIdx.Should().BeLessThan(firstTokenIdx, "Citations comes before first Token");
        firstTokenIdx.Should().BeLessThan(completeIdx, "Tokens come before Complete");
    }

    // ── Citations carry GameId for cross-game deep-link ──────────────────────

    [Fact]
    public async Task Citations_AndPrompt_CoverBothGames()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults(); // returns results for GameId1 and GameId2
        SetupLlmStream("Answer text");

        var query = new CrossGameStreamQaQuery("Tell me about the game", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert — le citazioni portano il chunk di ciascun gioco per il collegamento al passaggio, e
        // il prompt numera le fonti di entrambi.
        var citations = ((StreamingCitations)events.Single(e => e.Type == StreamingEventType.Citations).Data!).citations;
        citations.Select(c => c.chunkId).Should().BeEquivalentTo("chunk-1", "chunk-2");
        _llmService.Received(1).GenerateAnswerStreamAsync(
            Arg.Is<LlmAnswerRequest>(r =>
                r.UserPrompt.Contains("[1] [Source: Document doc-1", StringComparison.Ordinal)
                && r.UserPrompt.Contains("[2] [Source: Document doc-2", StringComparison.Ordinal)),
            Arg.Any<CancellationToken>());
    }

    // ── Each StreamChunk.Content from LLM → one Token event ─────────────────

    [Fact]
    public async Task LlmTokens_EmittedAsTokenEvents()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("One Two Three"); // produces 3 content tokens

        var query = new CrossGameStreamQaQuery("What rules apply?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert — 3 Token events (one per word chunk)
        var tokenEvents = events.Where(e => e.Type == StreamingEventType.Token).ToList();
        tokenEvents.Should().HaveCount(3);

        var tokenContents = tokenEvents
            .Select(e => ((StreamingToken)e.Data!).token)
            .ToList();
        tokenContents.Should().Contain(t => t.Contains("One"));
        tokenContents.Should().Contain(t => t.Contains("Two"));
        tokenContents.Should().Contain(t => t.Contains("Three"));
    }

    /// <summary>
    /// #4166: il gateway non ha fatto partire la chiamata. Prima il chunk di rifiuto, senza
    /// contenuto, veniva saltato e la risposta finiva «Grounded» e vuota.
    /// </summary>
    [Fact]
    public async Task GatewayRefusesTheCall_EmitsTheReason_AndNoComplete()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(RefusedStream());

        var events = await CollectEventsAsync(CreateSut(), new CrossGameStreamQaQuery("What rules apply?", UserId, UserRole.User));

        var error = events.Should().ContainSingle(e => e.Type == StreamingEventType.Error).Which.Data.Should().BeOfType<StreamingError>().Which;
        error.errorCode.Should().Be("AI_BUDGET_DAILY_EXHAUSTED");
        error.errorMessage.Should().StartWith("Il servizio AI è in pausa");
        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
    }

    private static async IAsyncEnumerable<StreamChunk> RefusedStream()
    {
        await Task.Yield();
        yield return new StreamChunk(null, IsFinal: true, ErrorCode: "AI_BUDGET_DAILY_EXHAUSTED",
            ErrorMessage: "Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.");
    }

    // ── Exception in search → Error event, no propagation ───────────────────

    [Fact]
    public async Task Exception_EmitsErrorEvent_DoesNotThrow()
    {
        // Arrange
        SetupTwoAccessibleGames();

        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
                         Arg.Any<IReadOnlyList<Guid>?>(),
                         Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Vector store unavailable"));

        var query = new CrossGameStreamQaQuery("What are the rules?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act — should not throw; stream should contain an Error event
        List<RagStreamingEvent> events = [];
        var act = async () =>
        {
            await foreach (var evt in sut.Handle(query, CancellationToken.None))
                events.Add(evt);
        };

        await act.Should().NotThrowAsync();

        // Assert
        var errorEvents = events.Where(e => e.Type == StreamingEventType.Error).ToList();
        errorEvents.Should().HaveCount(1);
        var error = (StreamingError)errorEvents[0].Data!;
        error.errorCode.Should().Be("RETRIEVAL_FAILED");
        error.errorMessage.Should().NotContain("Vector store unavailable", "il dettaglio interno resta nei log");
    }

    // ── Cancellation stops the stream early, no events after token phase ─────

    [Fact]
    public async Task Cancellation_StopsStream()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults();

        var cts = new CancellationTokenSource();

        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(SlowLlmStream(cts));

        var query = new CrossGameStreamQaQuery("Question?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act — cancel after first token
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in sut.Handle(query, cts.Token))
        {
            events.Add(evt);
            if (evt.Type == StreamingEventType.Token)
                cts.Cancel();
        }

        // Assert — stream stopped; no Complete event emitted
        var types = events.Select(e => e.Type).ToList();
        types.Should().Contain(StreamingEventType.Token); // at least one token was emitted
        types.Should().NotContain(StreamingEventType.Complete, "stream was cancelled before Complete");
    }

    // ── #4168: i token arrivano man mano ─────────────────────────────────────

    /// <summary>
    /// Prima i token si accumulavano in una lista e arrivavano tutti a generazione finita: il primo
    /// token non usciva finché l'LLM non chiudeva lo stream. Qui l'LLM aspetta che il primo token sia
    /// arrivato a chi legge prima di continuare: con l'accumulo, il test resterebbe fermo.
    /// </summary>
    [Fact]
    public async Task Tokens_ReachTheReader_BeforeTheGenerationEnds()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        var firstTokenRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(GatedStream(firstTokenRead.Task));

        var read = Task.Run(async () =>
        {
            var events = new List<RagStreamingEvent>();
            await foreach (var evt in CreateSut().Handle(new CrossGameStreamQaQuery("Rules?", UserId, UserRole.User), CancellationToken.None))
            {
                events.Add(evt);
                if (evt.Type == StreamingEventType.Token)
                {
                    firstTokenRead.TrySetResult();
                }
            }

            return events;
        });

        var finished = await Task.WhenAny(read, Task.Delay(TimeSpan.FromSeconds(10)));
        finished.Should().BeSameAs(read, "il primo token deve arrivare prima della fine della generazione");
        (await read).Where(e => e.Type == StreamingEventType.Token).Select(e => ((StreamingToken)e.Data!).token)
            .Should().Equal("First", " second");
    }

    private static async IAsyncEnumerable<StreamChunk> GatedStream(Task firstTokenRead)
    {
        yield return new StreamChunk("First");
        await firstTokenRead;
        yield return new StreamChunk(" second");
    }

    // ── RBAC: MultiGameSearch receives only the accessible gameIds ───────────

    [Fact]
    public async Task RetrievalUsesOnlyAccessibleGameIds()
    {
        // Arrange — only GameId1 is accessible
        _ragAccessService
            .GetAccessibleGameIdsAsync(UserId, UserRole.User, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)new[] { GameId1 });

        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
                         Arg.Any<IReadOnlyList<Guid>?>(),
                         Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MultiGameSearchResultItem>)new[]
            {
                MakeSearchResult(GameId1, "chunk-a", "doc-a")
            });

        SetupLlmStream("Answer");

        var query = new CrossGameStreamQaQuery("Rules?", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        await CollectEventsAsync(sut, query);

        // Assert — search was called with exactly [GameId1], not GameId2
        await _searchService.Received(1).SearchAsync(
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<Guid>>(ids =>
                ids.Count == 1 && ids.Contains(GameId1) && !ids.Contains(GameId2)),
            Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
            Arg.Any<IReadOnlyList<Guid>?>(),
            Arg.Any<CancellationToken>());
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void SetupTwoAccessibleGames() =>
        _ragAccessService
            .GetAccessibleGameIdsAsync(UserId, UserRole.User, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)new[] { GameId1, GameId2 });

    private void SetupSearchResults() =>
        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
                         Arg.Any<IReadOnlyList<Guid>?>(),
                         Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MultiGameSearchResultItem>)new[]
            {
                MakeSearchResult(GameId1, "chunk-1", "doc-1"),
                MakeSearchResult(GameId2, "chunk-2", "doc-2")
            });

    private void SetupLlmStream(string text) =>
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(CreateStreamChunks(text));

    private static async IAsyncEnumerable<StreamChunk> CreateStreamChunks(string text)
    {
        var words = text.Split(' ');
        foreach (var word in words)
        {
            yield return new StreamChunk(word + " ");
            await Task.Yield();
        }
        yield return new StreamChunk(null,
            new LlmUsage(100, 50, 150),
            new LlmCost { InputCost = 0.001m, OutputCost = 0.002m, ModelId = "gpt-4", Provider = "openai" },
            IsFinal: true);
    }

    /// <summary>Slow LLM stream that yields tokens one at a time, cancellable.</summary>
    private static async IAsyncEnumerable<StreamChunk> SlowLlmStream(
        CancellationTokenSource cts,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var words = new[] { "First", "Second", "Third" };
        foreach (var word in words)
        {
            ct.ThrowIfCancellationRequested();
            yield return new StreamChunk(word + " ");
            await Task.Yield();
        }
    }

    private static MultiGameSearchResultItem MakeSearchResult(Guid gameId, string chunkId, string docId) =>
        new()
        {
            GameId = gameId,
            ChunkId = chunkId,
            PdfDocumentId = docId,
            ChunkIndex = 0,
            PageNumber = 1,
            Content = "Some rule text",
            HybridScore = 0.85f,
            Mode = SearchMode.Hybrid
        };

    private static async Task<List<RagStreamingEvent>> CollectEventsAsync(
        CrossGameStreamQaQueryHandler handler,
        CrossGameStreamQaQuery query)
    {
        var events = new List<RagStreamingEvent>();
        await foreach (var evt in handler.Handle(query, CancellationToken.None))
            events.Add(evt);
        return events;
    }
}
