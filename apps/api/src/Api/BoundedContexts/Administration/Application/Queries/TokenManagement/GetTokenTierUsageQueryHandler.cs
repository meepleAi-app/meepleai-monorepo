using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.TokenManagement;

/// <summary>
/// Handler for GetTokenTierUsageQuery (Issue #3692)
/// Returns token usage breakdown per tier
/// </summary>
internal class GetTokenTierUsageQueryHandler : IQueryHandler<GetTokenTierUsageQuery, TierUsageListDto>
{
    private readonly ITokenTierRepository _tierRepository;
    private readonly IUserTokenUsageRepository _usageRepository;
    private readonly ILogger<GetTokenTierUsageQueryHandler> _logger;

    public GetTokenTierUsageQueryHandler(
        ITokenTierRepository tierRepository,
        IUserTokenUsageRepository usageRepository,
        ILogger<GetTokenTierUsageQueryHandler> logger)
    {
        _tierRepository = tierRepository ?? throw new ArgumentNullException(nameof(tierRepository));
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TierUsageListDto> Handle(GetTokenTierUsageQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Retrieving token tier usage breakdown");

        // Get all active tiers
        var tiers = await _tierRepository
            .GetAllActiveAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get usage data per tier
        var usageByTier = await _usageRepository
            .GetUsageByTierAsync(cancellationToken)
            .ConfigureAwait(false);

        var tierDtos = new List<TierUsageDto>();

        foreach (var tier in tiers)
        {
            // Get total usage for this tier
            var currentUsage = usageByTier.TryGetValue(tier.Id, out var usage) ? usage : 0;

            // Get user count for this tier
            var userCount = await _usageRepository
                .CountUsersByTierAsync(tier.Id, cancellationToken)
                .ConfigureAwait(false);

            // Calculate usage percentage
            var usagePercent = tier.Limits.TokensPerMonth > 0
                ? (double)currentUsage / tier.Limits.TokensPerMonth * 100
                : 0.0;

            tierDtos.Add(new TierUsageDto(
                Tier: tier.Name.ToString(),
                LimitPerMonth: tier.Limits.TokensPerMonth,
                CurrentUsage: currentUsage,
                UserCount: userCount,
                UsagePercent: usagePercent));
        }

        return new TierUsageListDto(Tiers: tierDtos);
    }
}
