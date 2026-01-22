using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetAllBadges;

/// <summary>
/// Handler for GetAllBadgesQuery with caching support.
/// Returns all badge definitions available in the system.
/// Issue #2736: API - Badge Endpoints
/// </summary>
internal sealed class GetAllBadgesQueryHandler : IRequestHandler<GetAllBadgesQuery, List<BadgeDefinitionDto>>
{
    private sealed record CachedBadgeDefinitionsResult(List<BadgeDefinitionDto> BadgeDefinitions);

    private readonly IBadgeRepository _badgeRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetAllBadgesQueryHandler> _logger;

    public GetAllBadgesQueryHandler(
        IBadgeRepository badgeRepository,
        IHybridCacheService cache,
        ILogger<GetAllBadgesQueryHandler> logger)
    {
        _badgeRepository = badgeRepository ?? throw new ArgumentNullException(nameof(badgeRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<BadgeDefinitionDto>> Handle(
        GetAllBadgesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        const string cacheKey = "meepleai:badges:all-definitions";
        string[] cacheTags = ["badge-definitions"];

        _logger.LogInformation("Retrieving all badge definitions with caching");

        try
        {
            CachedBadgeDefinitionsResult cachedResult = await _cache.GetOrCreateAsync(
                cacheKey,
                async ct =>
                {
                    var badges = await _badgeRepository.GetAllActiveAsync(ct).ConfigureAwait(false);

                    var badgeDtos = badges
                        .OrderBy(b => b.DisplayOrder)
                        .ThenBy(b => b.Tier)
                        .Select(MapToDto)
                        .ToList();

                    return new CachedBadgeDefinitionsResult(badgeDtos);
                },
                cacheTags,
                TimeSpan.FromHours(24),
                cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Retrieved {Count} badge definitions from cache",
                cachedResult.BadgeDefinitions.Count);

            return cachedResult.BadgeDefinitions;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cache operation failed for badge definitions, falling back to direct query");

            var badges = await _badgeRepository.GetAllActiveAsync(cancellationToken).ConfigureAwait(false);

            var result = badges
                .OrderBy(b => b.DisplayOrder)
                .ThenBy(b => b.Tier)
                .Select(MapToDto)
                .ToList();

            _logger.LogInformation("Retrieved {Count} badge definitions via fallback", result.Count);

            return result;
        }
    }

    private static BadgeDefinitionDto MapToDto(Domain.Entities.Badge badge)
    {
        return new BadgeDefinitionDto
        {
            Id = badge.Id,
            Code = badge.Code,
            Name = badge.Name,
            Description = badge.Description,
            IconUrl = badge.IconUrl,
            Tier = badge.Tier,
            Category = badge.Category,
            RequirementDescription = badge.Requirement.GetDescription()
        };
    }
}
