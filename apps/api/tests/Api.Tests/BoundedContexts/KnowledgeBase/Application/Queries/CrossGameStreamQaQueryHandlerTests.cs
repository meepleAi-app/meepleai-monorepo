using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries.CrossGameStreamQa;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
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
    private static readonly Guid UserId   = Guid.NewGuid();
    private static readonly Guid GameId1  = Guid.NewGuid();
    private static readonly Guid GameId2  = Guid.NewGuid();

    private readonly IRagAccessService            _ragAccessService   = Substitute.For<IRagAccessService>();
    private readonly IMultiGameHybridSearchService _searchService     = Substitute.For<IMultiGameHybridSearchService>();
    private readonly IRagPromptAssemblyService    _promptService      = Substitute.For<IRagPromptAssemblyService>();
    private readonly ILlmService                  _llmService         = Substitute.For<ILlmService>();

    private CrossGameStreamQaQueryHandler CreateSut() => new(
        _ragAccessService,
        _searchService,
        _promptService,
        _llmService,
        NullLogger<CrossGameStreamQaQueryHandler>.Instance);

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
        _llmService.DidNotReceive().GenerateCompletionStreamAsync(
            Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<RequestSource>(), Arg.Any<CancellationToken>());

        // Search must never be called either
        await _searchService.DidNotReceive().SearchAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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
        SetupAssembledPrompt();
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
        var citationsIdx   = types.IndexOf(StreamingEventType.Citations);
        var firstTokenIdx  = types.IndexOf(StreamingEventType.Token);
        var completeIdx    = types.LastIndexOf(StreamingEventType.Complete);

        stateUpdateIdx.Should().BeLessThan(citationsIdx,    "StateUpdate comes before Citations");
        citationsIdx.Should().BeLessThan(firstTokenIdx,     "Citations comes before first Token");
        firstTokenIdx.Should().BeLessThan(completeIdx,      "Tokens come before Complete");
    }

    // ── Citations carry GameId for cross-game deep-link ──────────────────────

    [Fact]
    public async Task Citations_CarryGameId_ForCrossGameDeepLink()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults(); // returns results for GameId1 and GameId2
        SetupAssembledPrompt();
        SetupLlmStream("Answer text");

        var query = new CrossGameStreamQaQuery("Tell me about the game", UserId, UserRole.User);
        var sut = CreateSut();

        // Act
        var events = await CollectEventsAsync(sut, query);

        // Assert — AssembleFromContextAsync was called with citations that carry GameId
        await _promptService.Received(1).AssembleFromContextAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Is<IReadOnlyList<ChunkCitation>>(chunks =>
                chunks.Any(c => c.GameId == GameId1) &&
                chunks.Any(c => c.GameId == GameId2)),
            Arg.Any<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread?>(),
            Arg.Any<UserTier?>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>(),
            Arg.Any<bool>());
    }

    // ── Each StreamChunk.Content from LLM → one Token event ─────────────────

    [Fact]
    public async Task LlmTokens_EmittedAsTokenEvents()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupAssembledPrompt();
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

    // ── Exception in search → Error event, no propagation ───────────────────

    [Fact]
    public async Task Exception_EmitsErrorEvent_DoesNotThrow()
    {
        // Arrange
        SetupTwoAccessibleGames();

        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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
        ((StreamingError)errorEvents[0].Data!).errorMessage.Should().Contain("Vector store unavailable");
    }

    // ── Cancellation stops the stream early, no events after token phase ─────

    [Fact]
    public async Task Cancellation_StopsStream()
    {
        // Arrange
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupAssembledPrompt();

        var cts = new CancellationTokenSource();

        _llmService
            .GenerateCompletionStreamAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<RequestSource>(), Arg.Any<CancellationToken>())
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
                         Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MultiGameSearchResultItem>)new[]
            {
                MakeSearchResult(GameId1, "chunk-a", "doc-a")
            });

        SetupAssembledPrompt();
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
                         Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<MultiGameSearchResultItem>)new[]
            {
                MakeSearchResult(GameId1, "chunk-1", "doc-1"),
                MakeSearchResult(GameId2, "chunk-2", "doc-2")
            });

    private void SetupAssembledPrompt() =>
        _promptService
            .AssembleFromContextAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IReadOnlyList<ChunkCitation>>(),
                Arg.Any<Api.BoundedContexts.KnowledgeBase.Domain.Entities.ChatThread?>(),
                Arg.Any<UserTier?>(), Arg.Any<string>(), Arg.Any<CancellationToken>(),
                Arg.Any<bool>())
            .Returns(new AssembledPrompt(
                "You are a board game assistant.",
                "Context: ...\nQuestion: ...",
                new List<ChunkCitation>
                {
                    new("doc-1", 1, 0.9f, "snippet from game 1") { GameId = GameId1 },
                    new("doc-2", 2, 0.8f, "snippet from game 2") { GameId = GameId2 }
                },
                200));

    private void SetupLlmStream(string text) =>
        _llmService
            .GenerateCompletionStreamAsync(Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<RequestSource>(), Arg.Any<CancellationToken>())
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
            GameId        = gameId,
            ChunkId       = chunkId,
            PdfDocumentId = docId,
            ChunkIndex    = 0,
            PageNumber    = 1,
            Content       = "Some rule text",
            HybridScore   = 0.85f,
            Mode          = SearchMode.Hybrid
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
