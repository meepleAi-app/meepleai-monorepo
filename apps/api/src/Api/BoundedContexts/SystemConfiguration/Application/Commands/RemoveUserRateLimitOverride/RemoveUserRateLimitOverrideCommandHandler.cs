using Api.BoundedContexts.SystemConfiguration.Domain.Events;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.RemoveUserRateLimitOverride;

/// <summary>
/// Handler for RemoveUserRateLimitOverrideCommand.
/// Removes a user-specific rate limit override, reverting them to tier-based limits.
/// </summary>
internal sealed class RemoveUserRateLimitOverrideCommandHandler
    : IRequestHandler<RemoveUserRateLimitOverrideCommand, Unit>
{
    private readonly IUserRateLimitOverrideRepository _overrideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMediator _mediator;
    private readonly ILogger<RemoveUserRateLimitOverrideCommandHandler> _logger;

    public RemoveUserRateLimitOverrideCommandHandler(
        IUserRateLimitOverrideRepository overrideRepository,
        IUnitOfWork unitOfWork,
        IMediator mediator,
        ILogger<RemoveUserRateLimitOverrideCommandHandler> logger)
    {
        _overrideRepository = overrideRepository ?? throw new ArgumentNullException(nameof(overrideRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        RemoveUserRateLimitOverrideCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get user's override (including expired ones)
        var allOverrides = await _overrideRepository.GetAllByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (allOverrides.Count == 0)
        {
            throw new NotFoundException("UserRateLimitOverride", command.UserId.ToString());
        }

        // Remove all overrides for this user (there should only be one, but clean up any orphans)
        foreach (var userOverride in allOverrides)
        {
            await _overrideRepository.DeleteAsync(userOverride, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Removed rate limit override {OverrideId} for user {UserId} by admin {AdminId}. " +
                "Override was {Status}",
                userOverride.Id,
                command.UserId,
                command.AdminId,
                userOverride.IsExpired() ? "expired" : "active");

            // Publish event for cache invalidation (entity doesn't raise this event)
            await _mediator.Publish(
                new UserRateLimitOverrideRemovedEvent(userOverride.Id, command.UserId, command.AdminId),
                cancellationToken).ConfigureAwait(false);
        }

        // Persist changes
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return default;
    }
}
