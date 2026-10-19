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

    // #4168 (5g-b): il piano di chi chiede. Senza limite, finché un test non ne dà uno.
    private Moq.Mock<Api.SharedKernel.Services.ITierEnforcementService> _tiers =
        AnswerPipelineTestDoubles.PlanWith(used: 0, max: int.MaxValue);

    // #4168: l'handler è un adattatore; la risposta sta nel flusso cross-game della pipeline, con il
    // profilo del seed e un reranker che non riordina.
    private CrossGameStreamQaQueryHandler CreateSut() => new(new CrossGameAnswerFlow(
        Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering.AnswerPipelineTestDoubles.Preconditions(_ragAccessService, _tiers.Object),
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
            Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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

    // ── #4168 (5g-b): la quota del piano, passo 2 ───────────────────────────

    private void ThePlanSays(int used, int max) =>
        _tiers = AnswerPipelineTestDoubles.PlanWith(used, max);

    private void NothingWasCounted() =>
        _tiers.Verify(
            t => t.RecordUsageAsync(
                Moq.It.IsAny<Guid>(), Moq.It.IsAny<Api.SharedKernel.Services.TierAction>(), Moq.It.IsAny<CancellationToken>()),
            Moq.Times.Never);

    /// <summary>
    /// Tre risposte consegnate oggi su un piano da tre: la domanda fra giochi si ferma con lo
    /// stesso evento della chat, senza cercare e senza chiamare il gateway.
    /// </summary>
    [Fact]
    public async Task OverTheQuotaOfThePlan_EndsWithTheTierLimitError_BeforeSearchAndLlm()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("unused");
        ThePlanSays(used: 3, max: 3);

        var events = await CollectEventsAsync(CreateSut(), new CrossGameStreamQaQuery("How do I win?", UserId, UserRole.User));

        var only = events.Should().ContainSingle().Which;
        only.Type.Should().Be(StreamingEventType.Error);
        only.Data.Should().Be(new StreamingError(
            "Tier limit exceeded: AgentQuery (3/3)",
            "TIER_LIMIT_EXCEEDED",
            limitType: "AgentQuery",
            current: 3,
            max: 3,
            upgradeUrl: "/pricing"));
        await _searchService.DidNotReceive().SearchAsync(
            Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
            Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
            Arg.Any<IReadOnlyList<Guid>?>(),
            Arg.Any<CancellationToken>());
        _llmService.DidNotReceive().GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>());
        NothingWasCounted();
    }

    /// <summary>Una domanda su più giochi è una domanda: conta uno, non uno per gioco.</summary>
    [Fact]
    public async Task AnAnswerAcrossTwoGames_IsCountedOnce_BeforeCompleteIsSent()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("Hello World");
        ThePlanSays(used: 2, max: 3);
        bool? countedWhenCompleteArrived = null;

        await foreach (var evt in CreateSut().Handle(
            new CrossGameStreamQaQuery("How do I win?", UserId, UserRole.User), TestContext.Current.CancellationToken))
        {
            if (evt.Type == StreamingEventType.Complete)
            {
                countedWhenCompleteArrived = _tiers.Invocations.Any(i =>
                    string.Equals(i.Method.Name, nameof(Api.SharedKernel.Services.ITierEnforcementService.RecordUsageAsync), StringComparison.Ordinal));
            }
        }

        countedWhenCompleteArrived.Should().BeTrue();
        _tiers.Verify(
            t => t.RecordUsageAsync(UserId, Api.SharedKernel.Services.TierAction.AgentQuery, Moq.It.IsAny<CancellationToken>()),
            Moq.Times.Once);
    }

    [Fact]
    public async Task WhenTheGatewayRefuses_NothingIsCounted()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(RefusedStream());
        ThePlanSays(used: 2, max: 3);

        var events = await CollectEventsAsync(CreateSut(), new CrossGameStreamQaQuery("What rules apply?", UserId, UserRole.User));

        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
        NothingWasCounted();
    }

    /// <summary>
    /// Nessun gioco da interrogare: lo stream si chiude senza cercare e senza una risposta. Non c'è
    /// niente da contare.
    /// </summary>
    [Fact]
    public async Task WithNoGameToAskAbout_NothingIsCounted()
    {
        _ragAccessService
            .GetAccessibleGameIdsAsync(UserId, UserRole.User, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)Array.Empty<Guid>());
        ThePlanSays(used: 2, max: 3);

        var events = await CollectEventsAsync(CreateSut(), new CrossGameStreamQaQuery("What are the rules?", UserId, UserRole.User));

        events.Should().Contain(e => e.Type == StreamingEventType.Complete);
        NothingWasCounted();
    }

    private static async IAsyncEnumerable<StreamChunk> RefusedStream()
    {
        await Task.Yield();
        yield return new StreamChunk(null, IsFinal: true, ErrorCode: "AI_BUDGET_DAILY_EXHAUSTED",
            ErrorMessage: "Il servizio AI è in pausa fino a domani: è stato raggiunto il budget di spesa del giorno.");
    }

    /// <summary>Il modello non ha scritto niente: non c'è una risposta da contare.</summary>
    [Fact]
    public async Task WhenTheModelWritesNothing_NothingIsCounted()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(SilentStream());
        ThePlanSays(used: 2, max: 3);

        var events = await CollectEventsAsync(CreateSut(), new CrossGameStreamQaQuery("What rules apply?", UserId, UserRole.User));

        events.Should().NotContain(e => e.Type == StreamingEventType.Token);
        NothingWasCounted();
    }

    /// <summary>
    /// Il client se n'è andato e lo stream del modello finisce senza un'eccezione: la risposta non
    /// è arrivata a nessuno, e non si conta.
    /// </summary>
    [Fact]
    public async Task WhenTheClientGoesAwayAndTheStreamJustEnds_NothingIsCounted()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        using var clientGone = new CancellationTokenSource();
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(StreamThatEndsWhenTheClientLeaves(clientGone));
        ThePlanSays(used: 2, max: 3);

        try
        {
            await foreach (var _ in CreateSut().Handle(new CrossGameStreamQaQuery("What rules apply?", UserId, UserRole.User), clientGone.Token))
            {
            }
        }
        catch (OperationCanceledException)
        {
            // Anche uscire con la cancellazione va bene: conta che non si sia contato.
        }

        clientGone.IsCancellationRequested.Should().BeTrue("il test ha senso solo se il client se n'è andato");
        NothingWasCounted();
    }

    private static async IAsyncEnumerable<StreamChunk> SilentStream()
    {
        await Task.Yield();
        yield return new StreamChunk(null, IsFinal: true, FinishReason: "stop");
    }

    private static async IAsyncEnumerable<StreamChunk> StreamThatEndsWhenTheClientLeaves(CancellationTokenSource client)
    {
        await Task.Yield();
        yield return new StreamChunk("Le regole");
        await client.CancelAsync();
    }

    // ── Exception in search → Error event, no propagation ───────────────────

    [Fact]
    public async Task Exception_EmitsErrorEvent_DoesNotThrow()
    {
        // Arrange
        SetupTwoAccessibleGames();

        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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
                         Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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
            Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
            Arg.Any<IReadOnlyList<Guid>?>(),
            Arg.Any<CancellationToken>());
    }

    // ── #3850, #4481: la rinuncia del modello nella lingua della risposta ─────

    [Fact]
    public async Task ItalianQuestion_TheEnglishRefusal_ReachesTheUserInItalian_AndIsUngrounded()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("This information is not available in the provided rulebook.");

        var events = await CollectEventsAsync(
            CreateSut(), new CrossGameStreamQaQuery("Si può?", UserId, UserRole.User, AgentLanguage: "it"));

        var tokens = events.Where(e => e.Type == StreamingEventType.Token)
            .Select(e => ((StreamingToken)e.Data!).token).ToList();
        string.Concat(tokens).Trim().Should().Be("Questa informazione non è disponibile nel regolamento fornito.");
        tokens.Should().NotContain(t => t.Contains("rulebook", StringComparison.OrdinalIgnoreCase));
        var complete = (StreamingComplete)events.Single(e => e.Type == StreamingEventType.Complete).Data!;
        complete.GroundingStatus.Should().Be("Ungrounded");
    }

    [Fact]
    public async Task ItalianQuestion_AnAnswer_IsGrounded()
    {
        SetupTwoAccessibleGames();
        SetupSearchResults();
        SetupLlmStream("Sì, puoi [1].");

        var events = await CollectEventsAsync(
            CreateSut(), new CrossGameStreamQaQuery("Si può?", UserId, UserRole.User, AgentLanguage: "it"));

        var complete = (StreamingComplete)events.Single(e => e.Type == StreamingEventType.Complete).Data!;
        complete.GroundingStatus.Should().Be("Grounded");
    }

    [Fact]
    public async Task AnInterruptedStream_DropsWhatWasHeld_WhichIsOnlyTheStartOfTheEnglishSentence()
    {
        // Review di #4494: il trattenuto non esce dopo AI_STREAM_INTERRUPTED; è solo l'inizio della frase.
        SetupTwoAccessibleGames();
        SetupSearchResults();
        _llmService
            .GenerateAnswerStreamAsync(Arg.Any<LlmAnswerRequest>(), Arg.Any<CancellationToken>())
            .Returns(InterruptedInTheMiddleOfTheRefusal());

        var events = await CollectEventsAsync(
            CreateSut(), new CrossGameStreamQaQuery("Si può?", UserId, UserRole.User, AgentLanguage: "it"));

        events.Where(e => e.Type == StreamingEventType.Token)
            .Select(e => ((StreamingToken)e.Data!).token).Should().Equal("Sì [1]. ");
        ((StreamingError)events.Single(e => e.Type == StreamingEventType.Error).Data!).errorCode
            .Should().Be(LlmAnswerErrorCodes.StreamInterrupted);
        events.Should().NotContain(e => e.Type == StreamingEventType.Complete);
    }

    private static async IAsyncEnumerable<StreamChunk> InterruptedInTheMiddleOfTheRefusal()
    {
        await Task.Yield();
        yield return new StreamChunk("Sì [1]. ");
        yield return new StreamChunk("This");
        yield return new StreamChunk(" information");
        yield return new StreamChunk(null, IsFinal: true,
            ErrorCode: LlmAnswerErrorCodes.StreamInterrupted, ErrorMessage: "The answer was interrupted before it was complete.");
    }

    // ── Private helpers ─────────────────────────────────────────────────────

    private void SetupTwoAccessibleGames() =>
        _ragAccessService
            .GetAccessibleGameIdsAsync(UserId, UserRole.User, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Guid>)new[] { GameId1, GameId2 });

    private void SetupSearchResults() =>
        _searchService
            .SearchAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<Guid>>(),
                         Arg.Any<int>(), Arg.Any<PerGameRetrievalParameters>(), Arg.Any<SearchMode>(), Arg.Any<double>(),
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
