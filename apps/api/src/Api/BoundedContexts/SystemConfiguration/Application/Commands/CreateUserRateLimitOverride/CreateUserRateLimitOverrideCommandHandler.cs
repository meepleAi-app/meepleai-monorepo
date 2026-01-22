using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.CreateUserRateLimitOverride;

/// <summary>
/// Handler for CreateUserRateLimitOverrideCommand.
/// Creates a new user-specific rate limit override.
/// Removes any existing expired override before creating the new one.
/// </summary>
internal sealed class CreateUserRateLimitOverrideCommandHandler
    : IRequestHandler<CreateUserRateLimitOverrideCommand, Unit>
{
    private readonly IUserRateLimitOverrideRepository _overrideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateUserRateLimitOverrideCommandHandler> _logger;

    public CreateUserRateLimitOverrideCommandHandler(
        IUserRateLimitOverrideRepository overrideRepository,
        IUnitOfWork unitOfWork,
        ILogger<CreateUserRateLimitOverrideCommandHandler> logger)
    {
        _overrideRepository = overrideRepository ?? throw new ArgumentNullException(nameof(overrideRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        CreateUserRateLimitOverrideCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check for existing override
        var existingOverride = await _overrideRepository.GetByUserIdAsync(command.UserId, cancellationToken).ConfigureAwait(false);

        if (existingOverride != null)
        {
            if (existingOverride.IsActive())
            {
                // Active override already exists
                throw new ConflictException(
                    $"User {command.UserId} already has an active rate limit override. " +
                    "Remove the existing override before creating a new one.");
            }

            // Expired override exists - remove it first
            await _overrideRepository.DeleteAsync(existingOverride, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Removed expired override {OverrideId} for user {UserId} before creating new one",
                existingOverride.Id,
                command.UserId);
        }

        // Create new override
        var newOverride = UserRateLimitOverride.Create(
            command.UserId,
            command.AdminId,
            command.Reason,
            command.ExpiresAt);

        // Update limits
        newOverride.UpdateLimits(
            command.MaxPendingRequests,
            command.MaxRequestsPerMonth,
            command.CooldownAfterRejection);

        // Add to repository
        await _overrideRepository.AddAsync(newOverride, cancellationToken).ConfigureAwait(false);

        // Persist changes (triggers domain events)
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Created rate limit override {OverrideId} for user {UserId} by admin {AdminId}. " +
            "MaxPending: {MaxPending}, MaxMonthly: {MaxMonthly}, Cooldown: {Cooldown}, ExpiresAt: {ExpiresAt}",
            newOverride.Id,
            command.UserId,
            command.AdminId,
            command.MaxPendingRequests?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default",
            command.MaxRequestsPerMonth?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "default",
            command.CooldownAfterRejection?.ToString() ?? "default",
            command.ExpiresAt?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "permanent");

        return default;
    }
}
