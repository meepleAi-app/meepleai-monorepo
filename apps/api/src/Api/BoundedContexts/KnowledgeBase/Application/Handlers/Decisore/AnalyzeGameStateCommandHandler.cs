using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Application.Commands.Decisore;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.Decisore;

/// <summary>
/// Handler for AnalyzeGameStateCommand.
/// Issue #3769: Orchestrates Decisore Agent analysis pipeline.
/// </summary>
internal sealed class AnalyzeGameStateCommandHandler
    : IRequestHandler<AnalyzeGameStateCommand, StrategicAnalysisResultDto>
{
    private readonly IGameSessionRepository _sessionRepo;
    private readonly IGameStateParserService _parser;
    private readonly IDecisoreAgentService _decisoreService;
    private readonly ILogger<AnalyzeGameStateCommandHandler> _logger;

    public AnalyzeGameStateCommandHandler(
        IGameSessionRepository sessionRepo,
        IGameStateParserService parser,
        IDecisoreAgentService decisoreService,
        ILogger<AnalyzeGameStateCommandHandler> logger)
    {
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _parser = parser ?? throw new ArgumentNullException(nameof(parser));
        _decisoreService = decisoreService ?? throw new ArgumentNullException(nameof(decisoreService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<StrategicAnalysisResultDto> Handle(
        AnalyzeGameStateCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Handling AnalyzeGameStateCommand: session={Session}, player={Player}",
            request.GameSessionId,
            request.PlayerName);

        // Load game session
        var session = await _sessionRepo.GetByIdAsync(request.GameSessionId, cancellationToken).ConfigureAwait(false);
        if (session == null)
            throw new NotFoundException("GameSession", request.GameSessionId.ToString());

        // Verify player is in session
        if (!session.HasPlayer(request.PlayerName))
            throw new InvalidOperationException($"Player '{request.PlayerName}' not in session");

        // Get current board state (MVP: use starting position, will integrate with GameSession.State in Phase 2)
        var boardStateFen = "rnbqkbnr/pppppppp/8/8/8/8/PPPPPPPP/RNBQKBNR w KQkq - 0 1";

        // Parse game state (Chess for MVP)
        var parsedState = await _parser.ParseAsync(boardStateFen, "Chess", cancellationToken).ConfigureAwait(false);

        // Determine player color (MVP: White by default, will use session player mapping in Phase 2)
        var playerColor = "White";

        // Analyze with Decisore service
        var useEnsemble = string.Equals(request.AnalysisDepth, "deep", StringComparison.OrdinalIgnoreCase);

        var result = await _decisoreService.AnalyzePositionAsync(
            parsedState,
            playerColor,
            request.MaxSuggestions,
            useEnsemble,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Analysis complete: suggestions={Count}, strength={Strength:F2}, time={Time}ms",
            result.Suggestions.Count,
            result.PositionStrength,
            result.ExecutionTimeMs);

        return result;
    }
}
