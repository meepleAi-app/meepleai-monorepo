using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Handles updating an existing house rule's description (#1464). Throws NotFoundException
/// when either the GameMemory or the rule doesn't exist (HTTP 404 at the endpoint).
/// </summary>
internal sealed class UpdateHouseRuleCommandHandler : ICommandHandler<UpdateHouseRuleCommand>
{
    private readonly IGameMemoryRepository _gameMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<UpdateHouseRuleCommandHandler> _logger;

    public UpdateHouseRuleCommandHandler(
        IGameMemoryRepository gameMemoryRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<UpdateHouseRuleCommandHandler> logger)
    {
        _gameMemoryRepo = gameMemoryRepo ?? throw new ArgumentNullException(nameof(gameMemoryRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateHouseRuleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.Enabled")
            .ConfigureAwait(false);
        if (!isEnabled)
            throw new ConflictException("Feature AgentMemory.Enabled is disabled");

        if (string.IsNullOrWhiteSpace(command.Description))
            throw new ArgumentException("Description cannot be empty.", nameof(command));

        var memory = await _gameMemoryRepo
            .GetByGameAndOwnerAsync(command.GameId, command.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (memory == null)
            throw new NotFoundException($"GameMemory not found for game {command.GameId}");

        try
        {
            memory.UpdateHouseRule(command.RuleId, command.Description);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            // Map the domain "rule not found" sentinel to a typed 404 — explicit and
            // consistent with RemoveHouseRuleCommandHandler instead of relying on the
            // middleware's string-match fallback.
            throw new NotFoundException($"House rule {command.RuleId} not found");
        }

        await _gameMemoryRepo.UpdateAsync(memory, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Updated house rule {RuleId} in game memory {MemoryId}",
            command.RuleId, memory.Id);
    }
}
