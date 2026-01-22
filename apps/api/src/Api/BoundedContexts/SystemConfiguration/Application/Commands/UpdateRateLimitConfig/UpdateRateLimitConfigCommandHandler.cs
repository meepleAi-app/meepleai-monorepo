using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateRateLimitConfig;

/// <summary>
/// Handler for UpdateRateLimitConfigCommand.
/// Updates tier-based rate limit configuration and invalidates cache.
/// </summary>
internal sealed class UpdateRateLimitConfigCommandHandler : IRequestHandler<UpdateRateLimitConfigCommand, Unit>
{
    private readonly IRateLimitConfigRepository _configRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<UpdateRateLimitConfigCommandHandler> _logger;

    public UpdateRateLimitConfigCommandHandler(
        IRateLimitConfigRepository configRepository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        ILogger<UpdateRateLimitConfigCommandHandler> logger)
    {
        _configRepository = configRepository ?? throw new ArgumentNullException(nameof(configRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(
        UpdateRateLimitConfigCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get or create config for tier
        var config = await _configRepository.GetByTierAsync(command.Tier, cancellationToken).ConfigureAwait(false);

        if (config == null)
        {
            // Create new config if doesn't exist
            config = ShareRequestLimitConfig.Create(
                command.Tier,
                command.MaxPendingRequests,
                command.MaxRequestsPerMonth,
                command.CooldownAfterRejection);

            await _configRepository.AddAsync(config, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created new rate limit config for tier {Tier} by admin {AdminId}",
                command.Tier,
                command.AdminId);
        }
        else
        {
            // Update existing config
            config.Update(
                command.MaxPendingRequests,
                command.MaxRequestsPerMonth,
                command.CooldownAfterRejection);

            await _configRepository.UpdateAsync(config, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Updated rate limit config for tier {Tier} by admin {AdminId}. " +
                "MaxPending: {MaxPending}, MaxMonthly: {MaxMonthly}, Cooldown: {Cooldown}",
                command.Tier,
                command.AdminId,
                command.MaxPendingRequests,
                command.MaxRequestsPerMonth,
                command.CooldownAfterRejection);
        }

        // Persist changes (triggers domain events)
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Invalidate cache for this tier
        var cacheKey = $"rate_limit_config_{command.Tier}";
        await _cache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        return default;
    }
}
