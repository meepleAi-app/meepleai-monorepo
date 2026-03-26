using Api.BoundedContexts.AgentMemory.Application.Commands;
using Api.BoundedContexts.AgentMemory.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.AgentMemory.Application.Commands;

/// <summary>
/// Handles claiming a guest player's memory for a registered user.
/// Simplified MVP: skips host confirmation.
/// </summary>
internal sealed class ClaimGuestPlayerCommandHandler : ICommandHandler<ClaimGuestPlayerCommand>
{
    private readonly IPlayerMemoryRepository _playerRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFeatureFlagService _featureFlags;
    private readonly ILogger<ClaimGuestPlayerCommandHandler> _logger;

    public ClaimGuestPlayerCommandHandler(
        IPlayerMemoryRepository playerRepo,
        IUnitOfWork unitOfWork,
        IFeatureFlagService featureFlags,
        ILogger<ClaimGuestPlayerCommandHandler> logger)
    {
        _playerRepo = playerRepo ?? throw new ArgumentNullException(nameof(playerRepo));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _featureFlags = featureFlags ?? throw new ArgumentNullException(nameof(featureFlags));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(ClaimGuestPlayerCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var isEnabled = await _featureFlags
            .IsEnabledAsync("Features:AgentMemory.GuestClaim")
            .ConfigureAwait(false);

        if (!isEnabled)
            throw new InvalidOperationException("Feature AgentMemory.GuestClaim is disabled");

        var playerMemory = await _playerRepo
            .GetByIdAsync(command.PlayerMemoryId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException($"Player memory {command.PlayerMemoryId} not found");

        playerMemory.ClaimByUser(command.UserId);

        await _playerRepo.UpdateAsync(playerMemory, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} claimed guest player memory {PlayerMemoryId}",
            command.UserId, command.PlayerMemoryId);
    }
}
