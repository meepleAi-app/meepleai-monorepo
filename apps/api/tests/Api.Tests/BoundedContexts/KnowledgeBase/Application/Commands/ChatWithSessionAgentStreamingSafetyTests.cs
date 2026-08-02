using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
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
/// SP5-b T4 (Issue #2582): Streaming-safety regression tests.
///
/// GUARANTEE: a metrics <c>Record()</c> / <c>Add()</c> fault inside
/// <see cref="ChatWithSessionAgentCommandHandler"/> MUST NOT abort the user's
/// live-chat stream.  The handler wraps every Record() call in try/catch — these
/// tests lock in that contract so a future refactor cannot silently remove the guard.
///
/// Fault-injection mechanism
/// ─────────────────────────
/// <see cref="MeterListener.SetMeasurementEventCallback{T}"/> fires SYNCHRONOUSLY
/// when an instrument's <c>Record()</c> or <c>Add()</c> is called.  When the
/// callback throws, the exception propagates back to the caller's <c>Record()</c>
/// site.  This is the standard .NET SDK mechanism; no special interception layer is
/// needed.  T4-AC-4 (probe) empirically confirms the mechanism works before the
/// integration tests rely on it.
///
/// Tests
/// ─────
/// T4-AC-4  probe: confirms the MeterListener-throws mechanism actually propagates to Record()
/// T4-AC-1  first-token latency fault  → stream reaches StreamingComplete, no exception escapes
/// T4-AC-2  citations-per-answer fault → stream reaches StreamingComplete, no exception escapes
/// T4-AC-3  both metrics fault simultaneously → stream reaches StreamingComplete, no exception escapes
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Observability")]
[Trait("Issue", "2582")]
public sealed class ChatWithSessionAgentStreamingSafetyTests
{
    private const string FirstTokenLatencyName = "meepleai.rag.first_token_latency";
    private const string CitationsPerAnswerName = "meepleai.rag.citations_per_answer";

    // ──────────────────────────────────────────────────────────────────────────
    // T4-AC-4 (probe): verify the fault-injection mechanism is real, not a no-op
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T4-AC-4 (probe): MeterListener throwing callback propagates exception to Record() call site")]
    public void MeterListenerThrowingCallback_PropagatesExceptionToRecordCallSite()
    {
        // Use an isolated meter so this probe doesn't interfere with shared instruments.
        using var probeMeter = new Meter("Test.StreamingSafety.Probe", "1.0.0");
        var probeHistogram = probeMeter.CreateHistogram<double>("probe.histogram");

        var counter = new int[1]; // array so lambda can mutate
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == "Test.StreamingSafety.Probe"
                    && instrument.Name == "probe.histogram")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) =>
        {
            Interlocked.Increment(ref counter[0]);
            throw new InvalidOperationException("Simulated metric fault (probe)");
        });
        listener.Start();

        // Record() MUST propagate the callback exception
        var act = () => probeHistogram.Record(42.0);
        act.Should().Throw<InvalidOperationException>(
            "MeterListener callback exceptions propagate synchronously to the Record() call site");

        counter[0].Should().Be(1,
            "the throwing callback must have been invoked exactly once during the probe");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T4-AC-1: first-token latency metric THROWS → stream still completes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T4-AC-1: first-token latency metric fault does not abort the live chat stream")]
    public async Task Handle_FirstTokenLatencyRecordThrows_StreamStillCompletesNoExceptionEscapes()
    {
        // Arrange — listener whose callback throws on first-token latency
        var faultCounter = new int[1];
        using var faultListener = BuildThrowingListener<double>(FirstTokenLatencyName, faultCounter);

        var handler = BuildHandler(resolvedCitations: new List<ChunkCitation>(), tokenContent: "Hello");
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        // Act — drain entire stream; MUST NOT throw
        var act = async () =>
        {
            await foreach (var ev in handler.Handle(command, CancellationToken.None))
            {
                events.Add(ev);
            }
        };

        await act.Should().NotThrowAsync(
            "a metrics Record() fault must never abort the user's live-chat stream");

        // Assert: stream reached StreamingComplete
        events.Should().Contain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded even when the first-token latency metric fault is injected");

        // Assert: fault path was genuinely triggered (try/catch caught a REAL throw)
        faultCounter[0].Should().BeGreaterThan(0,
            "the throwing MeterListener callback must have been invoked, proving the try/catch caught a real throw");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T4-AC-2: citations-per-answer metric THROWS → stream still completes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T4-AC-2: citations-per-answer metric fault does not abort the live chat stream")]
    public async Task Handle_CitationsPerAnswerRecordThrows_StreamStillCompletesNoExceptionEscapes()
    {
        // Arrange — listener whose callback throws on citations-per-answer
        var faultCounter = new int[1];
        using var faultListener = BuildThrowingListener<long>(CitationsPerAnswerName, faultCounter);

        var citations = new List<ChunkCitation>
        {
            new ChunkCitation(
                DocumentId: "doc-1",
                PageNumber: 1,
                RelevanceScore: 0.9f,
                SnippetPreview: "snippet",
                CopyrightTier: CopyrightTier.Full,
                IsPublic: true),
        };

        var handler = BuildHandler(resolvedCitations: citations, tokenContent: "Answer");
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        // Act — drain; MUST NOT throw
        var act = async () =>
        {
            await foreach (var ev in handler.Handle(command, CancellationToken.None))
            {
                events.Add(ev);
            }
        };

        await act.Should().NotThrowAsync(
            "a citations metric Record() fault must never abort the user's live-chat stream");

        // Assert: stream reached StreamingComplete
        events.Should().Contain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded even when the citations-per-answer metric fault is injected");

        // Assert: fault path was genuinely triggered
        faultCounter[0].Should().BeGreaterThan(0,
            "the throwing MeterListener callback must have been invoked, proving the try/catch caught a real throw");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T4-AC-3: BOTH metrics throw simultaneously → stream still completes
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T4-AC-3: both first-token AND citations metric faults do not abort the live chat stream")]
    public async Task Handle_BothMetricsRecordThrow_StreamStillCompletesNoExceptionEscapes()
    {
        // Arrange — two throwing listeners, one per metric
        var latencyFaultCounter = new int[1];
        var citationsFaultCounter = new int[1];

        using var latencyFault = BuildThrowingListener<double>(FirstTokenLatencyName, latencyFaultCounter);
        using var citationsFault = BuildThrowingListener<long>(CitationsPerAnswerName, citationsFaultCounter);

        var handler = BuildHandler(resolvedCitations: new List<ChunkCitation>(), tokenContent: "Hi");
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        // Act — drain; MUST NOT throw
        var act = async () =>
        {
            await foreach (var ev in handler.Handle(command, CancellationToken.None))
            {
                events.Add(ev);
            }
        };

        await act.Should().NotThrowAsync(
            "simultaneous metric faults must never abort the user's live-chat stream");

        // Assert: stream reached StreamingComplete
        events.Should().Contain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded despite both metric faults being injected");

        // Assert: BOTH fault paths were genuinely exercised
        latencyFaultCounter[0].Should().BeGreaterThan(0,
            "first-token latency throwing callback must have been invoked");
        citationsFaultCounter[0].Should().BeGreaterThan(0,
            "citations-per-answer throwing callback must have been invoked");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Builds a <see cref="MeterListener"/> whose measurement callback:
    /// 1. Atomically increments <paramref name="invokeCounter"/>[0] (so callers can
    ///    assert the fault path was genuinely exercised).
    /// 2. Throws <see cref="InvalidOperationException"/> (propagates to <c>Record()</c>).
    ///
    /// Array trick: C# forbids capturing ref locals in lambdas, so we use a single-element
    /// int[] as a mutable box that the lambda can close over.
    /// </summary>
    private static MeterListener BuildThrowingListener<T>(string metricName, int[] invokeCounter)
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
        listener.SetMeasurementEventCallback<T>((_, _, _, _) =>
        {
            Interlocked.Increment(ref invokeCounter[0]);
            throw new InvalidOperationException($"Simulated metric fault for '{metricName}'");
        });
        listener.Start();
        return listener;
    }

    private static ChatWithSessionAgentCommand BuildCommand() =>
        new ChatWithSessionAgentCommand(
            AgentSessionId: Guid.NewGuid(),
            UserQuestion: "What are the rules?",
            UserId: Guid.NewGuid());

    /// <summary>
    /// Builds a fully-wired handler with happy-path mocks, identical to the setup
    /// in <see cref="ChatWithSessionAgentMetricsTests"/> — copied here to keep the
    /// streaming-safety tests self-contained and avoid cross-test class coupling.
    /// </summary>
    private static ChatWithSessionAgentCommandHandler BuildHandler(
        IReadOnlyList<ChunkCitation> resolvedCitations,
        string tokenContent = "Hello world")
    {
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

        var definition = AgentDefinition.Create(
            name: "tutor",
            description: "Test agent",
            type: AgentType.Custom("tutor", "Tutor"),
            config: AgentDefinitionConfig.Default());

        var definitionRepo = new Mock<IAgentDefinitionRepository>();
        definitionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(definition);

        var gameCoreData = new Mock<IGameCoreDataProvider>();
        gameCoreData
            .Setup(g => g.GetCoreDataAsync(It.IsAny<GameRef>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((GameCoreData?)null);

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

        var liveSessionRepo = new Mock<ILiveSessionRepository>();
        liveSessionRepo
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LiveGameSession?)null);

        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

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

        var tierResolver = new Mock<ICopyrightTierResolver>();
        tierResolver
            .Setup(r => r.ResolveAsync(
                It.IsAny<IReadOnlyList<ChunkCitation>>(),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolvedCitations);

        var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
        memoryBuilder
            .Setup(b => b.BuildContextAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(CreateLlmStream(tokenContent));

        var budgetService = new Mock<IUserBudgetService>();
        budgetService
            .Setup(b => b.HasBudgetForQueryAsync(
                It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var registry = new Mock<ICircuitBreakerRegistry>();
        registry
            .Setup(r => r.GetMonitoringStatus())
            .Returns(new Dictionary<string, (string, string)>());

        var scopeFactory = new Mock<IServiceScopeFactory>();

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
            groundedAnswerService: new GroundedAnswerService(
                leakGuard.Object, fallbackProvider.Object,
                Options.Create(new CopyrightLeakGuardOptions()),
                NullLogger<GroundedAnswerService>.Instance),
            liveSessionStreamGateway: gateway.Object);
    }

    private static async IAsyncEnumerable<StreamChunk> CreateLlmStream(string content)
    {
        await Task.Yield();
        yield return new StreamChunk(Content: content);           // first token → triggers latency metric
        yield return new StreamChunk(Content: content + " (2)"); // second chunk → once-only guard tested
        yield return new StreamChunk(
            Content: null,
            Usage: new LlmUsage(10, 5, 15),
            IsFinal: true);
    }
}
