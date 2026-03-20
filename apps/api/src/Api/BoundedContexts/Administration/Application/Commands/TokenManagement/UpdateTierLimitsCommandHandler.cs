using Api.BoundedContexts.Administration.Application.Commands.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Administration.Application.Commands.TokenManagement;

/// <summary>
/// Handler for UpdateTierLimitsCommand (Issue #3692)
/// Updates token limits for a specific tier
/// </summary>
internal class UpdateTierLimitsCommandHandler : ICommandHandler<UpdateTierLimitsCommand>
{
    private readonly ITokenTierRepository _tierRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateTierLimitsCommandHandler> _logger;

    public UpdateTierLimitsCommandHandler(
        ITokenTierRepository tierRepository,
        IUnitOfWork unitOfWork,
        ILogger<UpdateTierLimitsCommandHandler> logger)
    {
        _tierRepository = tierRepository ?? throw new ArgumentNullException(nameof(tierRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(UpdateTierLimitsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Parse tier name
        if (!Enum.TryParse<TierName>(command.Tier, ignoreCase: true, out var tierName))
        {
            throw new DomainException($"Invalid tier name: {command.Tier}");
        }

        _logger.LogInformation("Updating limits for tier {TierName} to {TokensPerMonth} tokens/month",
            tierName, command.TokensPerMonth);

        // Find tier by name
        var tier = await _tierRepository
            .GetByNameAsync(tierName, cancellationToken)
            .ConfigureAwait(false);

        if (tier == null)
        {
            throw new NotFoundException("TokenTier", tierName.ToString());
        }

        // Create new limits (preserving existing values except TokensPerMonth)
        var newLimits = TierLimits.Create(
            tokensPerMonth: command.TokensPerMonth,
            tokensPerDay: tier.Limits.TokensPerDay,
            messagesPerDay: tier.Limits.MessagesPerDay,
            maxCollectionSize: tier.Limits.MaxCollectionSize,
            maxPdfUploadsPerMonth: tier.Limits.MaxPdfUploadsPerMonth,
            maxAgentsCreated: tier.Limits.MaxAgentsCreated,
            dailyCreditsLimit: tier.Limits.DailyCreditsLimit,
            weeklyCreditsLimit: tier.Limits.WeeklyCreditsLimit);

        // Update tier
        tier.UpdateLimits(newLimits);

        // Persist changes
        await _tierRepository.UpdateAsync(tier, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Successfully updated limits for tier {TierName}", tierName);
    }
}
