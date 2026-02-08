using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries.TokenManagement;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers.TokenManagement;

/// <summary>
/// Handler for GetTopConsumersQuery (Issue #3692)
/// Returns top token consumers with user information
/// </summary>
internal class GetTopConsumersQueryHandler : IQueryHandler<GetTopConsumersQuery, TopConsumersListDto>
{
    private readonly IUserTokenUsageRepository _usageRepository;
    private readonly ITokenTierRepository _tierRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetTopConsumersQueryHandler> _logger;

    public GetTopConsumersQueryHandler(
        IUserTokenUsageRepository usageRepository,
        ITokenTierRepository tierRepository,
        MeepleAiDbContext dbContext,
        ILogger<GetTopConsumersQueryHandler> logger)
    {
        _usageRepository = usageRepository ?? throw new ArgumentNullException(nameof(usageRepository));
        _tierRepository = tierRepository ?? throw new ArgumentNullException(nameof(tierRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TopConsumersListDto> Handle(GetTopConsumersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = Math.Max(1, Math.Min(query.Limit, 100)); // Clamp between 1-100

        _logger.LogInformation("Retrieving top {Limit} token consumers", limit);

        // Get top consumers from repository
        var topConsumers = await _usageRepository
            .GetTopConsumersAsync(limit, cancellationToken)
            .ConfigureAwait(false);

        // Get all active tiers for lookup
        var tiers = await _tierRepository
            .GetAllActiveAsync(cancellationToken)
            .ConfigureAwait(false);

        var tierMap = tiers.ToDictionary(t => t.Id, t => t);

        var consumerDtos = new List<TopConsumerDto>();

        foreach (var usage in topConsumers)
        {
            // Get user information
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == usage.UserId, cancellationToken)
                .ConfigureAwait(false);

            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for token usage record", usage.UserId);
                continue;
            }

            // Get tier information
            var tierName = tierMap.TryGetValue(usage.TierId, out var tier)
                ? tier.Name.ToString()
                : "Unknown";

            // Calculate percentage of tier limit
            var percentOfLimit = tier != null && tier.Limits.TokensPerMonth > 0
                ? (double)usage.TokensUsed / tier.Limits.TokensPerMonth * 100
                : 0.0;

            consumerDtos.Add(new TopConsumerDto(
                UserId: usage.UserId,
                DisplayName: user.DisplayName ?? "User",
                Email: user.Email,
                Tier: tierName,
                TokensUsed: usage.TokensUsed,
                PercentOfTierLimit: percentOfLimit));
        }

        return new TopConsumersListDto(Consumers: consumerDtos);
    }
}
