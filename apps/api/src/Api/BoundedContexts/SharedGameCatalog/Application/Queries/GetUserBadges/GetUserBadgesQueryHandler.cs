using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;

/// <summary>
/// Handler for GetUserBadgesQuery.
/// Returns all badges earned by a specific user with optional visibility filtering.
/// Issue #2728: Application - Badge Assignment Handlers
/// </summary>
internal sealed class GetUserBadgesQueryHandler : IRequestHandler<GetUserBadgesQuery, List<UserBadgeDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetUserBadgesQueryHandler> _logger;

    public GetUserBadgesQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetUserBadgesQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<UserBadgeDto>> Handle(
        GetUserBadgesQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting badges for user {UserId} (IncludeHidden={IncludeHidden})",
            query.UserId,
            query.IncludeHidden);

        var dbQuery = _context.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(ub => ub.Badge)
            .Where(ub => ub.UserId == query.UserId && ub.RevokedAt == null);

        // Apply visibility filter if needed
        if (!query.IncludeHidden)
        {
            dbQuery = dbQuery.Where(ub => ub.IsDisplayed);
        }

        var userBadges = await dbQuery
            .OrderBy(ub => ub.Badge!.DisplayOrder)
            .ThenByDescending(ub => ub.EarnedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = userBadges.Select(MapToDto).ToList();

        _logger.LogInformation(
            "Retrieved {Count} badges for user {UserId}",
            result.Count,
            query.UserId);

        return result;
    }

    private static UserBadgeDto MapToDto(UserBadgeEntity userBadge)
    {
        return new UserBadgeDto
        {
            Id = userBadge.Id,
            Code = userBadge.Badge!.Code,
            Name = userBadge.Badge.Name,
            Description = userBadge.Badge.Description,
            IconUrl = userBadge.Badge.IconUrl,
            Tier = (Domain.ValueObjects.BadgeTier)userBadge.Badge.Tier,
            EarnedAt = userBadge.EarnedAt,
            IsDisplayed = userBadge.IsDisplayed
        };
    }
}
