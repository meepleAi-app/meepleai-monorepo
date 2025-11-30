using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetAllUsersQuery.
/// Returns paginated list of users with optional filtering and sorting.
/// </summary>
public class GetAllUsersQueryHandler : IQueryHandler<GetAllUsersQuery, PagedResult<UserDto>>
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
            var term = query.SearchTerm.ToLower();
            dbQuery = dbQuery.Where(u =>
                (u.Email != null && u.Email.ToLower().Contains(term)) ||
                (u.DisplayName != null && u.DisplayName.ToLower().Contains(term)));
        }

        // Role filter
        if (!string.IsNullOrWhiteSpace(query.RoleFilter) && !string.Equals(query.RoleFilter, "all", StringComparison.Ordinal))
        {
            var normalizedRole = query.RoleFilter.ToLower();
            dbQuery = dbQuery.Where(u => u.Role == normalizedRole);
        }

        // Sorting
        dbQuery = query.SortBy?.ToLower() switch
        {
            "email" => string.Equals(query.SortOrder, "asc", StringComparison.Ordinal) ? dbQuery.OrderBy(u => u.Email) : dbQuery.OrderByDescending(u => u.Email),
            "displayname" => string.Equals(query.SortOrder, "asc", StringComparison.Ordinal) ? dbQuery.OrderBy(u => u.DisplayName) : dbQuery.OrderByDescending(u => u.DisplayName),
            "role" => string.Equals(query.SortOrder, "asc", StringComparison.Ordinal) ? dbQuery.OrderBy(u => u.Role) : dbQuery.OrderByDescending(u => u.Role),
            _ => string.Equals(query.SortOrder, "asc", StringComparison.Ordinal) ? dbQuery.OrderBy(u => u.CreatedAt) : dbQuery.OrderByDescending(u => u.CreatedAt)
        };

        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var users = await dbQuery
            .Skip((query.Page - 1) * query.Limit)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

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
            LastSeenAt: lastSeenAt
        );
    }
}
