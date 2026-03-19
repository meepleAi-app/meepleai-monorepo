using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for ValidateMoveCommand - Arbitro Agent move validation.
/// Issue #3760: Arbitro Agent Move Validation Logic with Game State Analysis.
/// </summary>
internal sealed class ValidateMoveCommandHandler : IRequestHandler<ValidateMoveCommand, MoveValidationResultDto>
{
    private readonly IGameSessionRepository _gameSessionRepository;
    private readonly IArbitroAgentService _arbitroAgentService;
    private readonly ILogger<ValidateMoveCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public ValidateMoveCommandHandler(
        IGameSessionRepository gameSessionRepository,
        IArbitroAgentService arbitroAgentService,
        ILogger<ValidateMoveCommandHandler> logger,
        TimeProvider timeProvider)
    {
        _gameSessionRepository = gameSessionRepository ?? throw new ArgumentNullException(nameof(gameSessionRepository));
        _arbitroAgentService = arbitroAgentService ?? throw new ArgumentNullException(nameof(arbitroAgentService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<MoveValidationResultDto> Handle(
        ValidateMoveCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "Handling ValidateMoveCommand: session={SessionId}, player={Player}, action={Action}",
            request.GameSessionId,
            request.PlayerName,
            request.Action);

        // Load GameSession from repository
        var session = await _gameSessionRepository
            .GetByIdAsync(request.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            throw new NotFoundException("GameSession", request.GameSessionId.ToString());
        }

        // Create Move value object from command
        var move = new Move(
            playerName: request.PlayerName,
            action: request.Action,
            position: request.Position,
            timestamp: _timeProvider.GetUtcNow().UtcDateTime,
            additionalContext: request.AdditionalContext);

        // Delegate to ArbitroAgentService for AI-powered validation
        var result = await _arbitroAgentService.ValidateMoveAsync(
            session,
            move,
            cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "ValidateMoveCommand completed: decision={Decision}, confidence={Confidence:F2}, latency={Latency}ms",
            result.Decision,
            result.Confidence,
            result.LatencyMs);

        return result;
    }
}
