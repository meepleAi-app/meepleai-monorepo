using System.Security.Cryptography;
using System.Text;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;

/// <summary>
/// Handler for GetGameContributorsQuery.
/// Retrieves all contributors for a shared game with caching.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
internal sealed class GetGameContributorsQueryHandler
    : IRequestHandler<GetGameContributorsQuery, List<GameContributorDto>>
{
    private readonly IContributorRepository _contributorRepository;
    private readonly IUserBadgeRepository _userBadgeRepository;
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly ILogger<GetGameContributorsQueryHandler> _logger;

    public GetGameContributorsQueryHandler(
        IContributorRepository contributorRepository,
        IUserBadgeRepository userBadgeRepository,
        MeepleAiDbContext context,
        HybridCache cache,
        ILogger<GetGameContributorsQueryHandler> logger)
    {
        _contributorRepository = contributorRepository ?? throw new ArgumentNullException(nameof(contributorRepository));
        _userBadgeRepository = userBadgeRepository ?? throw new ArgumentNullException(nameof(userBadgeRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<GameContributorDto>> Handle(
        GetGameContributorsQuery request,
        CancellationToken cancellationToken)
    {
        var cacheKey = GenerateCacheKey(request.SharedGameId);

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation(
                    "Cache miss for game contributors. Fetching from database. GameId: {GameId}",
                    request.SharedGameId);

                return await FetchFromDatabaseAsync(request.SharedGameId, cancel)
                    .ConfigureAwait(false);
            },
            new HybridCacheEntryOptions
            {
                LocalCacheExpiration = TimeSpan.FromMinutes(30),
                Expiration = TimeSpan.FromHours(2)
            },
            cancellationToken: cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<List<GameContributorDto>> FetchFromDatabaseAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken)
    {
        var contributors = await _contributorRepository
            .GetBySharedGameAsync(sharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (contributors.Count == 0)
        {
            _logger.LogInformation("No contributors found for game {GameId}", sharedGameId);
            return new List<GameContributorDto>();
        }

        var result = new List<GameContributorDto>();

        foreach (var contributor in contributors)
        {
            // Fetch user info from Authentication context
            var user = await _context.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Id == contributor.UserId)
                .Select(u => new
                {
                    u.DisplayName,
                    u.Email
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            var userName = user?.DisplayName ?? user?.Email ?? "Unknown User";

            // Fetch top 3 badges for this contributor
            var topBadges = await _userBadgeRepository
                .GetTopBadgesByUserAsync(contributor.UserId, 3, cancellationToken)
                .ConfigureAwait(false);

            var badgeSummaries = topBadges
                .Where(ub => ub.Badge != null)
                .Select(ub => new BadgeSummaryDto
                {
                    Code = ub.Badge!.Code,
                    Name = ub.Badge.Name,
                    IconUrl = ub.Badge.IconUrl ?? string.Empty,
                    Tier = ub.Badge.Tier
                })
                .ToList();

            var firstContribution = contributor.Contributions.Count > 0
                ? contributor.Contributions.Min(c => c.ContributedAt)
                : contributor.CreatedAt;

            result.Add(new GameContributorDto
            {
                UserId = contributor.UserId,
                UserName = userName,
                AvatarUrl = null,
                IsPrimaryContributor = contributor.IsPrimaryContributor,
                ContributionCount = contributor.ContributionCount,
                FirstContributionAt = firstContribution,
                TopBadges = badgeSummaries
            });
        }

        _logger.LogInformation(
            "Fetched {Count} contributors for game {GameId}",
            result.Count,
            sharedGameId);

        return result;
    }

    private static string GenerateCacheKey(Guid sharedGameId)
    {
        var input = $"game-contributors-{sharedGameId}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return $"game-contributors-{Convert.ToHexString(hash)}";
    }
}
