using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Threading;
using Api.BoundedContexts.Administration.Application.Services;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Configuration;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Models;
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
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

// Disambiguate UserTier from two namespaces
using SharedUserTier = Api.SharedKernel.Domain.ValueObjects.UserTier;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// SP5-c Task 3 (Issue #2600): Per-chunk LLM stream timeout in session agent chat.
///
/// Tests:
/// T3-AC-1  A stalled chunk (no data within perChunkTimeout) → handler emits
///          LLM_TIMEOUT error event and completes gracefully (no hang, no escaping exception).
/// T3-AC-2  A valid stream whose chunks each arrive within the timeout, but TOTAL
///          duration exceeds one chunk-timeout → NOT killed; full response delivered.
///          (Verifies the disarm-between-chunks logic.)
/// T3-AC-3  Client disconnect (original CancellationToken cancelled mid-stream) → stream
///          stops WITHOUT emitting a timeout error event.
/// T3-AC-4  Happy-path regression: normal fast stream yields all tokens + StreamingComplete.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Area", "Reliability")]
[Trait("Issue", "2600")]
public sealed class ChatWithSessionAgentPerChunkTimeoutTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-1: stalled chunk → LLM_TIMEOUT error, graceful completion (no hang)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-1: stalled LLM chunk exceeds per-chunk timeout → LLM_TIMEOUT error event, stream completes gracefully")]
    public async Task Handle_StalledChunk_EmitsLlmTimeoutErrorAndCompletesGracefully()
    {
        // #3601: la deadline è su un orologio finto, quindi "50 ms" e "500 ms" sono tempo virtuale e
        // il test non misura niente di reale. Questo caso era già il verso sicuro — VUOLE che la
        // deadline scatti, e un runner lento non fa che allungare lo stallo — ma tenerlo sull'orologio
        // finto lo rende immediato invece di mezzo secondo.
        const double perChunkTimeoutSeconds = 0.05; // 50 ms virtuali
        var clock = new FakeTimeProvider();

        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(StalledStream(clock, stall: TimeSpan.FromMilliseconds(500)));

        var handler = BuildHandler(llmService.Object, perChunkTimeoutSeconds, clock);
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        // Overall test guard: if the handler HANGS, this CTS will unblock the drain.
        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var act = async () =>
        {
            await foreach (var ev in handler.Handle(command, testCts.Token))
            {
                events.Add(ev);
            }
        };

        // Must NOT throw — timeout must surface as a streaming error event.
        await act.Should().NotThrowAsync(
            "per-chunk timeout must be surfaced as a streaming error event, not an escaping exception");

        // Must emit exactly one Error event with the timeout / LLM error code.
        events.Should().Contain(
            e => e.Type == StreamingEventType.Error,
            "an error event must be emitted when the per-chunk deadline is exceeded");

        var errorPayload = events
            .Where(e => e.Type == StreamingEventType.Error)
            .Select(e => e.Data)
            .OfType<StreamingError>()
            .FirstOrDefault();

        errorPayload.Should().NotBeNull("the error event must carry a StreamingError payload");
        errorPayload!.errorCode.Should().BeOneOf("LLM_TIMEOUT", "LLM_ERROR",
            "the timeout must use LLM_TIMEOUT (preferred) or fallback to LLM_ERROR");

        // StreamingComplete must NOT appear (the error short-circuits the happy path).
        events.Should().NotContain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must not be emitted when the LLM stream timed out");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-2: slow-but-steady stream (each chunk within timeout, total > timeout)
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-2: slow-but-steady stream delivers full response even when total exceeds one chunk deadline")]
    public async Task Handle_SlowButSteadyStream_DeliversFullResponseWhenEachChunkArrrivesWithinDeadline()
    {
        // The invariant under test: TOTAL duration exceeds one chunk deadline, while EVERY
        // individual chunk arrives within it — so the disarm-between-chunks logic must re-arm the
        // deadline after each received chunk. Two constraints, both preserved below:
        //   chunkCount × chunkAdvance > perChunkTimeout   (total trips one deadline's worth)
        //   chunkAdvance              < perChunkTimeout   (no single chunk trips it)
        //
        // 🔴 #3601, chiusa due volte. La prima volta allargando il margine: da 200 ms di deadline
        // contro 80 ms di chunk (120 ms di margine) a 1000 contro 25 (975 ms, 40× invece di 2,5×).
        // Il commento che stava qui dichiarava il limite di quella mitigazione — «does not make it
        // impossible, the deadline is still real wall-clock» — e aveva ragione: il test è tornato
        // rosso il 2026-10-04, su una suite cresciuta da 21.365 a 23.680 test.
        //
        // Ed è lì il punto che il margine non poteva risolvere: **il carico contro cui questo test
        // gareggiava era la suite stessa**. Un margine assoluto compra tempo in proporzione inversa
        // alla crescita della suite, quindi ogni PR che aggiunge test unit avvicinava la ricomparsa.
        // Allargarlo una terza volta avrebbe comprato un intervallo più corto del secondo.
        //
        // Ora il tempo è VIRTUALE: lo stream fa avanzare un FakeTimeProvider, e la deadline del
        // handler è armata sullo stesso orologio (`CancellationTokenSource(delay, timeProvider)`
        // più `CancelAfter`, contratto verificato in CancellationTokenSourceTimeProviderContractTests).
        // Il test non misura più niente di reale: non può fallire per una pausa GC, e gira in
        // millisecondi invece di ~1,1 s.
        const double perChunkTimeoutSeconds = 1.0;                          // 1000 ms virtuali
        var chunkAdvance = TimeSpan.FromMilliseconds(25);                   // < deadline
        const int chunkCount = 45;                                          // 45 × 25 = 1125 > 1000
        const string expectedToken = "hello";

        var clock = new FakeTimeProvider();
        var start = clock.GetUtcNow();

        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(SlowButSteadyStream(clock, chunkAdvance, content: expectedToken, chunkCount: chunkCount));

        var handler = BuildHandler(llmService.Object, perChunkTimeoutSeconds, clock);
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        using var testCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        await foreach (var ev in handler.Handle(command, testCts.Token))
        {
            events.Add(ev);
        }

        // 🔴 Questa asserzione viene prima delle altre due perché senza di essa il test passa per
        // niente. Con l'orologio finto, uno stream troncato al primo chunk produce comunque «nessun
        // Error» e «c'è Complete»: le due asserzioni sotto sono soddisfatte da un test che non ha
        // esercitato la ripetizione. Verificato per perturbazione — spostando l'armo della deadline
        // fuori dal ciclo (cioè togliendo il riarmo per chunk) le altre due restavano verdi.
        //
        // Qui si asserisce ciò che rende il caso significativo: il tempo VIRTUALE accumulato ha
        // superato una deadline intera mentre nessun intervallo singolo la superava.
        var elapsed = clock.GetUtcNow() - start;
        elapsed.Should().Be(chunkAdvance * chunkCount,
            "lo stream deve essere stato consumato per interno: è l'avanzamento totale a superare " +
            "la deadline, e se i chunk non arrivano tutti il caso sotto test non si verifica");
        elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(perChunkTimeoutSeconds),
            "la durata TOTALE deve superare una deadline, altrimenti il riarmo non è in gioco");

        events.OfType<RagStreamingEvent>()
            .Count(e => e.Type == StreamingEventType.Token)
            .Should().Be(chunkCount, "ogni chunk consegnato deve diventare un token");

        // No error — each chunk arrived before its deadline.
        events.Should().NotContain(
            e => e.Type == StreamingEventType.Error,
            "no error must be emitted when each individual chunk arrives within the per-chunk deadline");

        // Full response delivered.
        events.Should().Contain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded for a slow-but-steady stream");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-3: client disconnect → no spurious timeout error event
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-3: client disconnect does NOT emit a LLM_TIMEOUT error event")]
    public async Task Handle_ClientDisconnect_StreamStopsWithoutTimeoutErrorEvent()
    {
        // #3601: la deadline resta lunga (5 s virtuali) e non viene mai raggiunta, perché l'orologio
        // finto non avanza. Il disconnect avviene subito dopo il primo chunk, in un istante deciso
        // dal test: prima era un `Task.Delay(100).ContinueWith(…)` che sperava che 100 ms reali
        // cadessero dentro una finestra di 5 s reali — vero quasi sempre, ma per un motivo
        // ambientale, non per costruzione.
        const double perChunkTimeoutSeconds = 5.0;
        var clock = new FakeTimeProvider();

        using var clientCts = new CancellationTokenSource();

        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(DisconnectingStream(clientCts, content: "partial"));

        var handler = BuildHandler(llmService.Object, perChunkTimeoutSeconds, clock);
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        var act = async () =>
        {
            await foreach (var ev in handler.Handle(command, clientCts.Token))
            {
                events.Add(ev);
            }
        };

        // Must NOT throw (client disconnect is normal flow).
        await act.Should().NotThrowAsync(
            "client disconnect must not propagate as an unhandled exception");

        // No timeout / LLM error event.
        var timeoutOrLlmErrors = events
            .Where(e => e.Type == StreamingEventType.Error)
            .Select(e => e.Data as StreamingError)
            .Where(err => err is not null
                && (err.errorCode == "LLM_TIMEOUT" || err.errorCode == "LLM_ERROR"))
            .ToList();

        timeoutOrLlmErrors.Should().BeEmpty(
            "client disconnect must NOT surface as a LLM_TIMEOUT or LLM_ERROR event");

        // No StreamingComplete (stream was cut short by client).
        events.Should().NotContain(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must not be emitted when the client disconnected mid-stream");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // T3-AC-4: happy-path regression — normal stream → tokens + StreamingComplete
    // ──────────────────────────────────────────────────────────────────────────

    [Fact(DisplayName = "T3-AC-4: happy-path regression — normal stream delivers all tokens and StreamingComplete")]
    public async Task Handle_NormalStream_DeliversAllTokensAndStreamingComplete()
    {
        const string tokenContent = "The answer is 42";
        const double perChunkTimeoutSeconds = 30.0; // generous — won't fire

        var llmService = new Mock<ILlmService>();
        llmService
            .Setup(l => l.GenerateCompletionStreamAsync(
                It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<RequestSource>(), It.IsAny<CancellationToken>()))
            .Returns(InstantStream(tokenContent));

        var handler = BuildHandler(llmService.Object, perChunkTimeoutSeconds);
        var command = BuildCommand();

        var events = new List<RagStreamingEvent>();

        await foreach (var ev in handler.Handle(command, CancellationToken.None))
        {
            events.Add(ev);
        }

        events.Should().Contain(
            e => e.Type == StreamingEventType.Token,
            "Token events must be yielded on the happy path");

        events.Should().ContainSingle(
            e => e.Type == StreamingEventType.Complete,
            "StreamingComplete must be yielded exactly once on the happy path");

        events.Should().NotContain(
            e => e.Type == StreamingEventType.Error,
            "no error events must be emitted on the happy path");
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Fake LLM streams
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// A stream that delays <paramref name="delayMs"/> ms without yielding any chunk.
    /// Propagates OperationCanceledException so the handler's per-chunk catch can
    /// distinguish a timeout (streamCts fired) from a client disconnect (original ct).
    /// </summary>
    /// <summary>
    /// Uno stream che si blocca: fa passare <paramref name="stall"/> sull'orologio del handler e poi
    /// attende la cancellazione che quella scadenza produce.
    /// </summary>
    /// <remarks>
    /// #3601. <c>clock.Advance</c> fa scattare i timer dovuti <b>sincronamente</b>, quindi al ritorno
    /// la deadline del handler è già scattata e il token linkato è già cancellato: la
    /// <c>Task.Delay(Infinite, ct)</c> sotto solleva subito, senza attendere tempo reale. Non si
    /// cattura l'eccezione — deve arrivare a <c>MoveNextAsync()</c>, che è il punto in cui il handler
    /// distingue scadenza e disconnessione.
    /// </remarks>
    private static async IAsyncEnumerable<StreamChunk> StalledStream(
        FakeTimeProvider clock,
        TimeSpan stall,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        clock.Advance(stall);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        yield break;
    }

    /// <summary>
    /// Uno stream che si disconnette: consegna un chunk, poi cancella il token del client.
    /// </summary>
    /// <remarks>
    /// #3601. Sostituisce un <c>Task.Delay(100).ContinueWith(_ =&gt; clientCts.Cancel())</c>, che
    /// sperava che 100 ms reali cadessero dentro una finestra di 5 s reali. Qui il disconnect avviene
    /// in un istante deciso dal test — dopo il primo chunk — e non da quando il thread pool pianifica
    /// una continuazione.
    /// </remarks>
    private static async IAsyncEnumerable<StreamChunk> DisconnectingStream(
        CancellationTokenSource clientCts,
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        yield return new StreamChunk(Content: content);

        await clientCts.CancelAsync().ConfigureAwait(false);
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken).ConfigureAwait(false);
        yield break;
    }

    /// <summary>
    /// Consegna <paramref name="chunkCount"/> chunk, facendo passare
    /// <paramref name="advancePerChunk"/> sull'orologio del handler prima di ognuno, poi il chunk
    /// finale con l'uso.
    /// </summary>
    /// <remarks>
    /// #3601. 🔴 Il tempo qui è VIRTUALE, e questo è il fix: prima c'era un
    /// <c>Task.Delay(delayPerChunkMs)</c> reale che gareggiava con una deadline reale, e il margine
    /// era l'unica difesa. Un margine è una mitigazione con una vita media — il carico contro cui
    /// questo test gareggiava è la suite stessa, che cresce a ogni PR — e infatti il test è tornato
    /// rosso su una suite passata da 21.365 a 23.680 test.
    ///
    /// <para>
    /// L'ordine conta: il handler arma la deadline PRIMA di <c>MoveNextAsync</c>, l'avanzamento
    /// avviene DENTRO, e il disarmo DOPO che il chunk è arrivato. Quindi ogni intervallo si misura
    /// da un armo fresco, ed è per questo che la somma può superare la deadline mentre nessun
    /// intervallo singolo la supera.
    /// </para>
    /// </remarks>
    private static async IAsyncEnumerable<StreamChunk> SlowButSteadyStream(
        FakeTimeProvider clock,
        TimeSpan advancePerChunk,
        string content,
        int chunkCount,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        for (int i = 0; i < chunkCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            clock.Advance(advancePerChunk);
            yield return new StreamChunk(Content: content);
        }

        await Task.CompletedTask.ConfigureAwait(false);
        yield return new StreamChunk(Content: null, Usage: new LlmUsage(10, 5, 15), IsFinal: true);
    }

    /// <summary>Yields chunks immediately (no delay).</summary>
    private static async IAsyncEnumerable<StreamChunk> InstantStream(
        string content,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        yield return new StreamChunk(Content: content);
        yield return new StreamChunk(Content: null, Usage: new LlmUsage(10, 5, 15), IsFinal: true);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Builder helpers
    // ──────────────────────────────────────────────────────────────────────────

    private static ChatWithSessionAgentCommand BuildCommand() =>
        new ChatWithSessionAgentCommand(
            AgentSessionId: Guid.NewGuid(),
            UserQuestion: "What are the rules?",
            UserId: Guid.NewGuid());

    /// <summary>
    /// Builds a fully-wired handler with the given <paramref name="llmService"/> and
    /// a <see cref="SessionAgentOptions"/> whose <c>LlmPerChunkTimeoutSeconds</c> is
    /// set to <paramref name="perChunkTimeoutSeconds"/>.
    /// </summary>
    private static ChatWithSessionAgentCommandHandler BuildHandler(
        ILlmService llmService,
        double perChunkTimeoutSeconds,
        TimeProvider? timeProvider = null)
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
            Citations: new List<ChunkCitation>(),
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
            .ReturnsAsync(new List<ChunkCitation>());

        var memoryBuilder = new Mock<IAgentMemoryContextBuilder>();
        memoryBuilder
            .Setup(b => b.BuildContextAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

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

        var sessionAgentOptions = Options.Create(new SessionAgentOptions
        {
            LlmPerChunkTimeoutSeconds = perChunkTimeoutSeconds,
        });

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
            llmService: llmService,
            userBudgetService: budgetService.Object,
            circuitBreakerRegistry: registry.Object,
            scopeFactory: scopeFactory.Object,
            logger: NullLogger<ChatWithSessionAgentCommandHandler>.Instance,
            groundedAnswerService: new GroundedAnswerService(
                leakGuard.Object, fallbackProvider.Object,
                Options.Create(new CopyrightLeakGuardOptions()),
                NullLogger<GroundedAnswerService>.Instance),
            liveSessionStreamGateway: gateway.Object,
            sessionAgentOptions: sessionAgentOptions,
            timeProvider: timeProvider);
    }
}
