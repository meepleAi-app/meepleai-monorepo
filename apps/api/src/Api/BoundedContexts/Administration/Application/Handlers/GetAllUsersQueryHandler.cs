using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace Api.BoundedContexts.Administration.Application.Handlers;

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
            // Direct comparison - Role values are standardized (admin/user/editor)
            dbQuery = dbQuery.Where(u => u.Role == query.RoleFilter);
        }

        // Status filter (active/suspended)
        if (!string.IsNullOrWhiteSpace(query.StatusFilter) && !string.Equals(query.StatusFilter, "all", StringComparison.OrdinalIgnoreCase))
        {
            var isSuspended = string.Equals(query.StatusFilter, "suspended", StringComparison.OrdinalIgnoreCase);
            dbQuery = dbQuery.Where(u => u.IsSuspended == isSuspended);
        }

        // Sorting
        var sortOrder = query.SortOrder ?? "asc";
        var isAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);

        dbQuery = query.SortBy switch
        {
            var s when string.Equals(s, "email", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.Email) : dbQuery.OrderByDescending(u => u.Email),
            var s when string.Equals(s, "displayname", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.DisplayName) : dbQuery.OrderByDescending(u => u.DisplayName),
            var s when string.Equals(s, "role", StringComparison.OrdinalIgnoreCase) => isAsc ? dbQuery.OrderBy(u => u.Role) : dbQuery.OrderByDescending(u => u.Role),
            _ => isAsc ? dbQuery.OrderBy(u => u.CreatedAt) : dbQuery.OrderByDescending(u => u.CreatedAt)
        };

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var users = await dbQuery
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new PagedResult<UserDto>(
            Items: users.Select(MapToDto).ToList(),
            Total: total,
            Page: query.Page,
            PageSize: query.Limit
        );
    }

    private static UserDto MapToDto(UserEntity user)
    {
        var lastSeenAt = user.Sessions
            .Where(s => s.RevokedAt == null)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .FirstOrDefault()
            ?.LastSeenAt;

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
            ExperiencePoints: user.ExperiencePoints
        );
    }
}