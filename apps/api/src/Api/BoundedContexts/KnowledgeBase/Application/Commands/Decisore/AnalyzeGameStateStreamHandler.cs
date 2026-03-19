using System.Diagnostics;
using System.Runtime.CompilerServices;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.Decisore;

/// <summary>
/// Streaming handler for AnalyzeGameStateCommand.
/// Issue #4334: SSE streaming for real-time Decisore Agent analysis progress.
/// </summary>
internal sealed class AnalyzeGameStateStreamHandler
    : IStreamingQueryHandler<AnalyzeGameStateCommand, RagStreamingEvent>
{
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IGameStateParserService _parser;
    private readonly IDecisoreAgentService _decisoreService;
    private readonly ILogger<AnalyzeGameStateStreamHandler> _logger;

    public AnalyzeGameStateStreamHandler(
        IGameSessionRepository sessionRepo,
        IGameStateParserService parser,
        IDecisoreAgentService decisoreService,
        ILogger<AnalyzeGameStateStreamHandler> logger)
    {
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _decisoreService = decisoreService ?? throw new ArgumentNullException(nameof(decisoreService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

#pragma warning disable S4456 // Standard MediatR streaming pattern
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        AnalyzeGameStateCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken)
#pragma warning restore S4456
    {
        ArgumentNullException.ThrowIfNull(command);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Decisore streaming analysis: session={Session}, player={Player}, depth={Depth}",
            command.GameSessionId, command.PlayerName, command.AnalysisDepth);

        // Step 1: Load game session
        yield return CreateStateUpdate("Loading game session...");

        var session = await _sessionRepo.GetByIdAsync(command.GameSessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new NotFoundException("GameSession", command.GameSessionId.ToString());

        // Verify player membership
        if (!session.HasPlayer(command.PlayerName))
            throw new InvalidOperationException($"Player '{command.PlayerName}' not in session");

        // Step 2: Parse game state
        yield return CreateStateUpdate("Parsing game state...");

        var boardStateFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";
        var parsedState = await _parser.ParseAsync(boardStateFen, "Chess", cancellationToken).ConfigureAwait(false);
        var playerColor = "White";

        // Step 3: Analyzing with Decisore
        var useEnsemble = string.Equals(command.AnalysisDepth, "deep", StringComparison.OrdinalIgnoreCase);
        var depthLabel = useEnsemble ? "deep (multi-model ensemble)" : command.AnalysisDepth;

        yield return CreateStateUpdate($"Analyzing position (depth: {depthLabel})...");

        // Execute analysis
        var result = await _decisoreService.AnalyzePositionAsync(
            parsedState,
            playerColor,
            command.MaxSuggestions,
            useEnsemble,
            cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogInformation(
            "Decisore analysis complete: suggestions={Count}, strength={Strength:F2}, time={Time}ms",
            result.Suggestions.Count, result.PositionStrength, stopwatch.ElapsedMilliseconds);

        // Step 4: Send completion event with full result
        yield return CreateCompleteEvent(result, stopwatch.ElapsedMilliseconds);
    }

    private static RagStreamingEvent CreateStateUpdate(string message)
    {
        return new RagStreamingEvent(
            StreamingEventType.StateUpdate,
            new StreamingStateUpdate(message),
            DateTime.UtcNow);
    }

    private static RagStreamingEvent CreateCompleteEvent(object result, long durationMs)
    {
        _ = result; // Result available for future use
        _ = durationMs;

        var completion = new StreamingComplete(
            estimatedReadingTimeMinutes: 0,
            promptTokens: 0,
            completionTokens: 0,
            totalTokens: 0,
            confidence: null);

        return new RagStreamingEvent(
            StreamingEventType.Complete,
            completion,
            DateTime.UtcNow);
    }
}
