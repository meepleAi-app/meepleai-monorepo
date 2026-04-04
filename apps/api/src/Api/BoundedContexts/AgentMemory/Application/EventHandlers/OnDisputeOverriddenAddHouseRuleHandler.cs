using Api.BoundedContexts.AgentMemory.Domain.Entities;
using Api.BoundedContexts.AgentMemory.Domain.Enums;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Events;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.EventHandlers;

/// <summary>
/// When a structured dispute is resolved with VerdictOverridden outcome and an override rule is provided,
/// automatically adds the override rule as a house rule to the game's memory.
/// </summary>
internal sealed class OnDisputeOverriddenAddHouseRuleHandler : INotificationHandler<StructuredDisputeResolvedEvent>
{
    private readonly ILiveSessionRepository _sessionRepo;
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OnDisputeOverriddenAddHouseRuleHandler> _logger;

    public OnDisputeOverriddenAddHouseRuleHandler(
        ILiveSessionRepository sessionRepo,
        IGameMemoryRepository gameMemoryRepo,
        IUnitOfWork unitOfWork,
        ILogger<OnDisputeOverriddenAddHouseRuleHandler> logger)
    {
        _sessionRepo = sessionRepo ?? throw new ArgumentNullException(nameof(sessionRepo));
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(StructuredDisputeResolvedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.FinalOutcome != DisputeOutcome.VerdictOverridden)
            return;

        if (string.IsNullOrWhiteSpace(notification.OverrideRule))
            return;

        var session = await _sessionRepo
            .GetByIdAsync(notification.SessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session == null)
        {
            _logger.LogWarning(
                "Session {SessionId} not found when handling dispute override for house rule",
                notification.SessionId);
            return;
        }

        var ownerId = session.CreatedByUserId;
        var gameId = notification.GameId;

        var gameMemory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(gameId, ownerId, cancellationToken)
            .ConfigureAwait(false);

        if (gameMemory == null)
        {
            gameMemory = GameMemory.Create(gameId, ownerId);
            gameMemory.AddHouseRule(notification.OverrideRule, HouseRuleSource.DisputeOverride);
            await _gameMemoryRepo.AddAsync(gameMemory, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            gameMemory.AddHouseRule(notification.OverrideRule, HouseRuleSource.DisputeOverride);
            await _gameMemoryRepo.UpdateAsync(gameMemory, cancellationToken).ConfigureAwait(false);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Added house rule from dispute override for game {GameId}, owner {OwnerId}, dispute {DisputeId}",
            gameId, ownerId, notification.DisputeId);
    }
}
