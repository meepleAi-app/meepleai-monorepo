using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Handles removing a house rule by id (#1464). Throws NotFoundException when the
/// GameMemory or the rule doesn't exist (HTTP 404 at the endpoint).
/// </summary>
internal sealed class RemoveHouseRuleCommandHandler : ICommandHandler<RemoveHouseRuleCommand>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<RemoveHouseRuleCommandHandler> _logger;

    public RemoveHouseRuleCommandHandler(
        IGameMemoryRepository gameMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<RemoveHouseRuleCommandHandler> logger)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(RemoveHouseRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);
        if (!isEnabled)
            throw new ConflictException("Feature AgentMemory.Enabled is disabled");

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(command.GameId, command.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (memory == null)
            throw new NotFoundException($"GameMemory not found for game {command.GameId}");

        var removed = memory.RemoveHouseRule(command.RuleId);
        if (!removed)
            throw new NotFoundException($"House rule {command.RuleId} not found");

        await _gameMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Removed house rule {RuleId} from game memory {MemoryId}",
            command.RuleId, memory.Id);
    }
}
