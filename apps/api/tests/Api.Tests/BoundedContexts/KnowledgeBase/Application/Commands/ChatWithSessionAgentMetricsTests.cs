using System.Collections.Generic;
using System.Diagnostics.Metrics;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

// Disambiguate UserTier from two namespaces
using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// SP5-b T2 (Issue #2582): Verifies that <see cref="ChatWithSessionAgentCommandHandler"/>
/// records the RAG first-token latency and citations-per-answer metrics inside the
/// streaming handler.
///
/// Tests:
/// 1. <c>meepleai.rag.first_token_latency</c> is recorded EXACTLY once per streamed chat
///    (even when the LLM stream emits multiple content chunks — proves the once-only guard).
///    Observation is ≥ 0 ms.
/// 2. <c>meepleai.rag.citations_per_answer</c> is recorded EXACTLY once per streamed chat
///    with the correct citation count.
/// 3. The zero-citation case records 0 (grounded-but-uncited signal).
/// 4. Both metrics are recorded in the same streamed request (end-to-end happy path).
///    NOTE: streaming-safety under metrics fault (try/catch guard) is covered by
///    SP5-b Task 4 (#2582 T4) — not in this file.
///
/// Uses <see cref="MeterListener"/> to capture measurement events, mirroring
/// <see cref="SseMetricsTests"/> and <see cref="RagObservabilityMetricsTests"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Observability")]
[Trait("Issue", "2582")]
public sealed class ChatWithSessionAgentMetricsTests
{
    private const string FirstTokenLatencyName = "meepleai.rag.first_token_latency";
    private const string CitationsPerAnswerName = "meepleai.rag.citations_per_answer";

    // ──────────────────────────────────────────────────────────────────────────
    // T2-AC-1: first-token latency recorded exactly once, value ≥ 0
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2-AC-1: first-token latency is recorded exactly once per streamed chat (≥ 0 ms)")]
    public async Task Handle_StreamedChat_RecordsFirstTokenLatencyExactlyOnce()
    {
        // Arrange
        var observations = new List<double>();
        using var listener = BuildHistogramListener<double>(FirstTokenLatencyName, observations);

        var handler = BuildHandler(resolvedCitations: new List<ChunkCitation>(), ragPromptMock: out _, tokenContent: "Hello world");
        var command = BuildCommand();

        // Act — drain to completion
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — exactly one observation, ≥ 0 ms
        observations.Should().ContainSingle(
            "first-token latency must be recorded exactly once per request");
        observations[0].Should().BeGreaterThanOrEqualTo(0d,
            "elapsed time cannot be negative");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2-AC-2: citations-per-answer recorded once with correct count (non-zero)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2-AC-2: citations-per-answer is recorded exactly once with the citation count")]
    public async Task Handle_StreamedChatWithCitations_RecordsCitationCountExactlyOnce()
    {
        // Arrange — two Full-tier citations
        var citations = new List<ChunkCitation>
        {
            new ChunkCitation(
                DocumentId: "doc-1",
                PageNumber: 1,
                RelevanceScore: 0.9f,
                SnippetPreview: "snippet A",
                CopyrightTier: CopyrightTier.Full,
                IsPublic: true),
            new ChunkCitation(
                DocumentId: "doc-2",
                PageNumber: 2,
                RelevanceScore: 0.8f,
                SnippetPreview: "snippet B",
                CopyrightTier: CopyrightTier.Full,
                IsPublic: true),
        };

        var observations = new List<long>();
        using var listener = BuildHistogramListener<long>(CitationsPerAnswerName, observations);

        var handler = BuildHandler(resolvedCitations: citations, ragPromptMock: out _, tokenContent: "Answer with citations");
        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert
        observations.Should().ContainSingle(
            "citations-per-answer must be recorded exactly once per request");
        observations[0].Should().Be(2L,
            "two citations → observation must be 2");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2-AC-3: zero-citation case records 0 (grounded-but-uncited signal)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2-AC-3: zero-citation case records 0 for citations-per-answer (le=0 bucket)")]
    public async Task Handle_StreamedChatNoCitations_RecordsZeroCitationsPerAnswer()
    {
        // Arrange — no citations
        var observations = new List<long>();
        using var listener = BuildHistogramListener<long>(CitationsPerAnswerName, observations);

        var handler = BuildHandler(resolvedCitations: new List<ChunkCitation>(), ragPromptMock: out _, tokenContent: "Grounded but uncited");
        var command = BuildCommand();

        // Act
        var complete = await DrainAndCaptureCompleteAsync(handler, command);

        // Assert
        observations.Should().ContainSingle(
            "citations-per-answer must be recorded exactly once even when there are no citations");
        observations[0].Should().Be(0L,
            "grounded-but-uncited signal: le=0 bucket must be populated");

        // #3388 (Task 2): mode-parity signal — the SSE RAG path must emit a non-nullable
        // grounding string on StreamingComplete, mirroring Task 1's REST-path assertion
        // (AskSessionAgentResult.GroundingStatus) so both in-session agent paths carry the
        // same grounding contract for a zero-citation answer.
        complete.Should().NotBeNull();
        complete!.GroundingStatus.Should().Be("Ungrounded");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2-AC-3b (#3388): with-citations case emits GroundingStatus="Grounded"
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2-AC-3b (#3388): with-citations case emits GroundingStatus=\"Grounded\" on StreamingComplete")]
    public async Task Handle_StreamedChatWithCitations_EmitsGroundedStatus()
    {
        // Arrange — one Full-tier citation
        var citations = new List<ChunkCitation>
        {
            new ChunkCitation(
                DocumentId: "doc-1",
                PageNumber: 1,
                RelevanceScore: 0.9f,
                SnippetPreview: "snippet A",
                CopyrightTier: CopyrightTier.Full,
                IsPublic: true),
        };

        var handler = BuildHandler(resolvedCitations: citations, ragPromptMock: out _, tokenContent: "Answer with citation");
        var command = BuildCommand();

        // Act
        var complete = await DrainAndCaptureCompleteAsync(handler, command);

        // Assert — mode-parity signal (see zero-citation test comment above)
        complete.Should().NotBeNull();
        complete!.GroundingStatus.Should().Be("Grounded");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T2-AC-4: both metrics are recorded in the same request
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T2-AC-4: both first-token latency and citations-per-answer are recorded in the same streamed chat")]
    public async Task Handle_StreamedChat_RecordsBothMetrics()
    {
        // Arrange
        var latencyObs = new List<double>();
        var citationsObs = new List<long>();
        using var latencyListener = BuildHistogramListener<double>(FirstTokenLatencyName, latencyObs);
        using var citationsListener = BuildHistogramListener<long>(CitationsPerAnswerName, citationsObs);

        var handler = BuildHandler(resolvedCitations: new List<ChunkCitation>(), ragPromptMock: out _, tokenContent: "Token");
        var command = BuildCommand();

        // Act
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — both recorded
        latencyObs.Should().ContainSingle("first-token latency must be recorded");
        citationsObs.Should().ContainSingle("citations-per-answer must be recorded");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static MeterListener BuildHistogramListener<T>(string metricName, List<T> observations)
        where T : struct
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName
                    && instrument.Name == metricName)
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<T>((_, value, _, _) => observations.Add(value));
        listener.Start();
        return listener;
    }

    /// <summary>
    /// Drains the handler's streamed events and returns the <see cref="StreamingComplete"/>
    /// payload carried by the terminal <see cref="StreamingEventType.Complete"/> event.
    /// </summary>
    private static async Task<StreamingComplete?> DrainAndCaptureCompleteAsync(
        ChatWithSessionAgentCommandHandler handler,
        ChatWithSessionAgentCommand command)
    {
        StreamingComplete? complete = null;
        await foreach (var evt in handler.Handle(command, CancellationToken.None))
        {
            if (evt.Type == StreamingEventType.Complete)
            {
                complete = evt.Data as StreamingComplete;
            }
        }

        return complete;
    }

    private static ChatWithSessionAgentCommand BuildCommand() =>
        new ChatWithSessionAgentCommand(
            AgentSessionId: Guid.NewGuid(),
            UserQuestion: "What are the rules?",
            UserId: Guid.NewGuid());

    /// <summary>
    /// Builds a fully-wired handler with happy-path mocks.
    /// <paramref name="resolvedCitations"/> controls the citation list returned by the
    /// copyright resolver. <paramref name="tokenContent"/> controls what the LLM emits.
    /// </summary>
    // ──────────────────────────────────────────────────────────────────────────
    // #3389: live path passes an explicit RetrievalPolicy.LiveSession
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "#3389: in-session live path passes RetrievalPolicy.LiveSession (decoupled from tier, not silent Default)")]
    public async Task Handle_LivePath_PassesLiveSessionRetrievalPolicy()
    {
        // Arrange
        var handler = BuildHandler(
            resolvedCitations: new List<ChunkCitation>(),
            ragPromptMock: out var ragPromptMock,
            tokenContent: "Answer");
        var command = BuildCommand();

        // Act — drain to completion
        await foreach (var _ in handler.Handle(command, CancellationToken.None)) { }

        // Assert — the live path assembled the prompt with the explicit LiveSession policy,
        // so retrieval is decoupled from tier and never silently falls back to Default (#3389).
        ragPromptMock.Verify(r => r.AssemblePromptAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
            It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
            It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(),
            It.Is<RetrievalPolicy?>(p => p == RetrievalPolicy.LiveSession)),
            Times.Once);
    }

    private static ChatWithSessionAgentCommandHandler BuildHandler(
        IReadOnlyList<ChunkCitation> resolvedCitations,
        out Mock<IRagPromptAssemblyService> ragPromptMock,
        string tokenContent = "Hello world")
    {
        // --- AgentSession repo ---
        var playerId = Guid.NewGuid();
        var initialState = GameState.Create(
            currentTurn: 1,
            activePlayer: playerId,
            playerScores: new Dictionary<Guid, decimal> { [playerId] = 0 },
            gamePhase: "Setup",
            lastAction: "Game started");

        var agentSession = new AgentSession(
            id: Guid.NewGuid(),
            agentDefinitionId: Guid.NewGuid(),
            gameSessionId: Guid.NewGuid(),
            userId: Guid.NewGuid(),
            gameId: Guid.NewGuid(),
            initialState: initialState);

        var sessionRepo = new Mock<IAgentSessionRepository>();
        sessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(agentSession);

        // --- AgentDefinition repo ---
        var definition = AgentDefinition.Create(
            name: "tutor",
            description: "Test agent",
            type: AgentType.Custom("tutor", "Tutor"),
            config: AgentDefinitionConfig.Default());

        var definitionRepo = new Mock<IAgentDefinitionRepository>();
        definitionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        // --- Game core data ---
        var gameCoreData = new Mock<IGameCoreDataProvider>();
        gameCoreData
            .Setup(g => g.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameCoreData?)null);

        // --- ChatThread repo: null → auto-create ---
        var chatThreadRepo = new Mock<IChatThreadRepository>();
        chatThreadRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ChatThread?)null);
        chatThreadRepo
            .Setup(r => r.AddAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        chatThreadRepo
            .Setup(r => r.UpdateAsync(It.IsAny<ChatThread>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        // --- LiveSession repo → null (no live session needed for metrics) ---
        var liveSessionRepo = new Mock<ILiveSessionRepository>();
        liveSessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        // --- UoW ---
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        // --- RAG prompt assembly → citations become the resolved set ---
        var assembled = new AssembledPrompt(
            SystemPrompt: "You are a helpful assistant.",
            UserPrompt: "What are the rules?",
            Citations: resolvedCitations.ToList(),
            EstimatedTokens: 100);

        var ragPromptService = new Mock<IRagPromptAssemblyService>();
        ragPromptService
            .Setup(r => r.AssemblePromptAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<GameState?>(),
                It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<ChatThread?>(),
                It.IsAny<SharedUserTier?>(), It.IsAny<string>(), It.IsAny<CancellationToken>(),
                It.IsAny<IRagDebugEventCollector?>(), It.IsAny<RetrievalProfile?>(), It.IsAny<RetrievalPolicy?>()))
            .ReturnsAsync(assembled);
        ragPromptMock = ragPromptService;

        // --- Copyright tier resolver → pass-through ---
        var tierResolver = new Mock<ICopyrightTierResolver>();
        tierResolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedCitations);

        // --- Agent memory context builder ---
        var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
        memoryBuilder
            .Setup(b => b.BuildContextAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        // --- LLM service: one content token + final ---
        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(CreateLlmStream(tokenContent));

        // --- Budget service ---
        var budgetService = new Mock<IUserBudgetService>();
        budgetService
            .Setup(b => b.HasBudgetForQueryAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // --- Circuit breaker ---
        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string, string)>());

        // --- ServiceScopeFactory (chunk usage increment fire-and-forget) ---
        var scopeFactory = new Mock<IServiceScopeFactory>();

        // --- Copyright leak guard (no leak) ---
        var noLeak = new CopyrightLeakResult(HasLeak: false, Matches: Array.Empty<LeakMatch>());
        var leakGuard = new Mock<ICopyrightLeakGuard>();
        leakGuard
            .Setup(g => g.ScanAsync(
                It.IsAny<string>(), It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(noLeak);

        var fallbackProvider = new Mock<ICopyrightFallbackMessageProvider>();

        var gateway = new Mock<ILiveSessionStreamGateway>();

        return new ChatWithSessionAgentCommandHandler(
            sessionRepository: sessionRepo.Object,
            definitionRepository: definitionRepo.Object,
            chatThreadRepository: chatThreadRepo.Object,
            gameCoreData: gameCoreData.Object,
            liveSessionRepository: liveSessionRepo.Object,
            unitOfWork: unitOfWork.Object,
            ragPromptService: ragPromptService.Object,
            copyrightTierResolver: tierResolver.Object,
            agentMemoryContextBuilder: memoryBuilder.Object,
            llmService: llmService.Object,
            userBudgetService: budgetService.Object,
            circuitBreakerRegistry: registry.Object,
            scopeFactory: scopeFactory.Object,
            logger: NullLogger<ChatWithSessionAgentCommandHandler>.Instance,
            copyrightLeakGuard: leakGuard.Object,
            fallbackMessageProvider: fallbackProvider.Object,
            copyrightOptions: Options.Create(new CopyrightLeakGuardOptions()),
            liveSessionStreamGateway: gateway.Object);
    }

    /// <summary>
    /// Emits TWO content chunks followed by the final/usage chunk.
    /// Two chunks are required so that the once-only guard (<c>if (!firstTokenRecorded)</c>)
    /// is genuinely exercised: a regression that records on every token would produce 2
    /// observations, causing <c>ContainSingle</c> to fail.
    /// </summary>
    private static async IAsyncEnumerable<StreamChunk> CreateLlmStream(string content)
    {
        await Task.Yield();
        yield return new StreamChunk(Content: content);          // first token → records latency
        yield return new StreamChunk(Content: content + " (2)"); // second token → guard must suppress
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(10, 5, 15),
            IsFinal: true);
    }
}
