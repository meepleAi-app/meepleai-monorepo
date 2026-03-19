using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetAllUsersQuery.
/// Returns paginated list of users with optional filtering and sorting.
/// </summary>
internal class GetAllUsersQueryHandler : IQueryHandler<GetAllUsersQuery, PagedResult<UserDto>>
{
    private readonly MeepleAiDbContext _dbContext;

    public GetAllUsersQueryHandler(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<PagedResult<UserDto>> Handle(GetAllUsersQuery query, CancellationToken cancellationToken)
    {
        var dbQuery = _dbContext.Users
            .Include(u => u.Sessions)
            .AsNoTracking();

        // Search filter (email or display name)
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm;
            dbQuery = dbQuery.Where(u =>
                (u.Email != null && u.Email.Contains(term, StringComparison.InvariantCultureIgnoreCase)) ||
                (u.DisplayName != null && u.DisplayName.Contains(term, StringComparison.InvariantCultureIgnoreCase)));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(query.RoleFilter) && !string.Equals(query.RoleFilter, "all", StringComparison.Ordinal))
        {
            dbQuery = dbQuery.Where(u => u.Role == query.RoleFilter);
        }

        // Status filter (active/suspended)
        if (!string.IsNullOrWhiteSpace(query.StatusFilter) && !string.Equals(query.StatusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            var isSuspended = string.Equals(query.StatusFilter, "suspended", StringComparison.OrdinalIgnoreCase);
            dbQuery = dbQuery.Where(u => u.IsSuspended == isSuspended);
        }

        // Tier filter - Issue #3698
        if (!string.IsNullOrWhiteSpace(query.TierFilter) && !string.Equals(query.TierFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            dbQuery = dbQuery.Where(u => u.Tier.Equals(query.TierFilter, StringComparison.OrdinalIgnoreCase));
        }

        // Sorting - Issue #3698: Added tier sorting
        var sortOrder = query.SortOrder ?? "asc";
        var isAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        dbQuery = query.SortBy switch
        {
            var s when string.Equals(s, "email", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.Email) : dbQuery.OrderByDescending(u => u.Email),
            var s when string.Equals(s, "displayname", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.DisplayName) : dbQuery.OrderByDescending(u => u.DisplayName),
            var s when string.Equals(s, "role", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.Role) : dbQuery.OrderByDescending(u => u.Role),
            var s when string.Equals(s, "tier", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.Tier) : dbQuery.OrderByDescending(u => u.Tier),
            _ => isAsc ? dbQuery.OrderBy(u => u.CreatedAt) : dbQuery.OrderByDescending(u => u.CreatedAt)
        };

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var users = await dbQuery
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Issue #3698: Fetch token usage for returned users
        var userIds = users.Select(u => u.Id).ToList();
        var tokenUsages = await _dbContext.UserTokenUsages
            .Where(usage => userIds.Contains(usage.UserId))
            .AsNoTracking()
            .ToDictionaryAsync(usage => usage.UserId, cancellationToken)
            .ConfigureAwait(false);

        var tierIds = tokenUsages.Values.Select(u => u.TierId).Distinct().ToList();
        var tiers = await _dbContext.Set<Api.BoundedContexts.Administration.Domain.Entities.TokenTier>()
            .Where(t => tierIds.Contains(t.Id))
            .AsNoTracking()
            .ToDictionaryAsync(t => t.Id, cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<UserDto>(
            Items: users.Select(u => MapToDto(u, tokenUsages.GetValueOrDefault(u.Id), tiers)).ToList(),
            Total: total,
            Page: query.Page,
            PageSize: query.Limit
        );
    }

    private static UserDto MapToDto(
        UserEntity user,
        Api.BoundedContexts.Administration.Domain.Entities.UserTokenUsage? usage,
        Dictionary<Guid, Api.BoundedContexts.Administration.Domain.Entities.TokenTier> tiers)
    {
        var lastSeenAt = user.Sessions
            .Where(s => s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .FirstOrDefault()
            ?.LastSeenAt;

        // Issue #3698: Get token usage and tier info
        var tokensUsed = usage?.TokensUsed ?? 0;
        var tokenLimit = 10_000; // Default Free tier

        if (usage != null && tiers.TryGetValue(usage.TierId, out var tier))
        {
            tokenLimit = tier.Limits.TokensPerMonth;
        }

        return new UserDto(
            Id: user.Id.ToString(),
            Email: user.Email,
            DisplayName: user.DisplayName ?? string.Empty,
            Role: user.Role,
            CreatedAt: user.CreatedAt,
            LastSeenAt: lastSeenAt,
            IsSuspended: user.IsSuspended,
            SuspendReason: user.SuspendReason,
            Level: user.Level,
            ExperiencePoints: user.ExperiencePoints,
            Tier: user.Tier,            // Issue #3698: Tier from UserEntity
            TokenUsage: tokensUsed,     // Issue #3698: From UserTokenUsage
            TokenLimit: tokenLimit      // Issue #3698: From TokenTier
        );
    }
}
