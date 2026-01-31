using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries.GetRateLimitConfig;

/// <summary>
/// Handler for GetRateLimitConfigQuery.
/// Retrieves all tier-based rate limit configurations from the repository.
/// </summary>
internal sealed class GetRateLimitConfigQueryHandler
    : IRequestHandler<GetRateLimitConfigQuery, IReadOnlyList<RateLimitConfigDto>>
{
    private readonly IRateLimitConfigRepository _repository;

    public GetRateLimitConfigQueryHandler(IRateLimitConfigRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<IReadOnlyList<RateLimitConfigDto>> Handle(
        GetRateLimitConfigQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Fetch configurations based on filter
        var configs = query.ActiveOnly
            ? await _repository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.GetAllAsync(cancellationToken).ConfigureAwait(false);

        // Map to DTOs
        return configs
            .Select(MapToDto)
            .ToList()
            .AsReadOnly();
    }

    private static RateLimitConfigDto MapToDto(Domain.Entities.ShareRequestLimitConfig config)
    {
        return new RateLimitConfigDto
        {
            Id = config.Id,
            Tier = config.Tier,
            MaxPendingRequests = config.MaxPendingRequests,
            MaxRequestsPerMonth = config.MaxRequestsPerMonth,
            CooldownAfterRejection = config.CooldownAfterRejection,
            IsActive = config.IsActive,
            UpdatedAt = config.UpdatedAt
        };
    }
}
