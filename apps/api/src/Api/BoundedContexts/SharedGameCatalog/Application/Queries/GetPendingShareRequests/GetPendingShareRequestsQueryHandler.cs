using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetPendingShareRequests;

/// <summary>
/// Handler for GetPendingShareRequestsQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetPendingShareRequestsQueryHandler : IRequestHandler<GetPendingShareRequestsQuery, PagedResult<AdminShareRequestDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetPendingShareRequestsQueryHandler> _logger;

    private static readonly int[] ActiveStatuses =
    [
        (int)ShareRequestStatus.Pending,
        (int)ShareRequestStatus.InReview,
        (int)ShareRequestStatus.ChangesRequested
    ];

    public GetPendingShareRequestsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetPendingShareRequestsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<AdminShareRequestDto>> Handle(
        GetPendingShareRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting pending share requests: StatusFilter={StatusFilter}, TypeFilter={TypeFilter}, SearchTerm={SearchTerm}, SortBy={SortBy}, SortDirection={SortDirection}, Page={Page}, PageSize={PageSize}",
            query.StatusFilter?.ToString() ?? "All",
            query.TypeFilter?.ToString() ?? "All",
            query.SearchTerm ?? "None",
            query.SortBy,
            query.SortDirection,
            query.PageNumber,
            query.PageSize);

        // Build base query with eager loading
        var dbQuery = _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(r => r.SourceGame)
            .Include(r => r.TargetSharedGame)
            .Include(r => r.AttachedDocuments)
            .Where(r => ActiveStatuses.Contains(r.Status));

        // Apply status filter if provided
        if (query.StatusFilter.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Status == (int)query.StatusFilter.Value);
        }

        // Apply contribution type filter if provided
        if (query.TypeFilter.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.ContributionType == (int)query.TypeFilter.Value);
        }

        // Apply search filter if provided (PostgreSQL ILike for case-insensitive search)
        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchPattern = $"%{query.SearchTerm}%";
            dbQuery = dbQuery.Where(r =>
                EF.Functions.ILike(r.SourceGame.Title, searchPattern) ||
                (r.UserNotes != null && EF.Functions.ILike(r.UserNotes, searchPattern)));
        }

        // Apply sorting
        dbQuery = ApplySorting(dbQuery, query.SortBy, query.SortDirection);

        // Get total count before pagination
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Fetch entities with pagination
        var requests = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get user IDs to fetch user info
        var userIds = requests.Select(r => r.UserId).Distinct().ToList();
        var adminIds = requests
            .Where(r => r.ReviewingAdminId.HasValue)
            .Select(r => r.ReviewingAdminId!.Value)
            .Distinct()
            .ToList();

        var allUserIds = userIds.Concat(adminIds).Distinct().ToList();

        // Fetch users
        var users = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        // Count contributions per user
        var contributionCounts = await _context.Set<ContributorEntity>()
            .AsNoTracking()
            .Where(c => userIds.Contains(c.UserId))
            .GroupBy(c => c.UserId)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken)
            .ConfigureAwait(false);

        // Map to DTOs
        var dtos = requests.Select(r => MapToDto(r, users, contributionCounts)).ToList();

        _logger.LogInformation(
            "Retrieved {Count} pending share requests (Total: {Total})",
            dtos.Count, total);

        return new PagedResult<AdminShareRequestDto>(
            Items: dtos,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }

    private static IQueryable<ShareRequestEntity> ApplySorting(
        IQueryable<ShareRequestEntity> query,
        ShareRequestSortField sortBy,
        SortDirection direction)
    {
        return (sortBy, direction) switch
        {
            (ShareRequestSortField.CreatedAt, SortDirection.Ascending) => query.OrderBy(r => r.CreatedAt),
            (ShareRequestSortField.CreatedAt, SortDirection.Descending) => query.OrderByDescending(r => r.CreatedAt),
            (ShareRequestSortField.GameTitle, SortDirection.Ascending) => query.OrderBy(r => r.SourceGame.Title),
            (ShareRequestSortField.GameTitle, SortDirection.Descending) => query.OrderByDescending(r => r.SourceGame.Title),
            (ShareRequestSortField.Status, SortDirection.Ascending) => query.OrderBy(r => r.Status),
            (ShareRequestSortField.Status, SortDirection.Descending) => query.OrderByDescending(r => r.Status),
            // For ContributorName, we can't sort directly - we'll sort by UserId as a proxy
            (ShareRequestSortField.ContributorName, SortDirection.Ascending) => query.OrderBy(r => r.UserId),
            (ShareRequestSortField.ContributorName, SortDirection.Descending) => query.OrderByDescending(r => r.UserId),
            _ => query.OrderBy(r => r.CreatedAt)
        };
    }

    private static AdminShareRequestDto MapToDto(
        ShareRequestEntity request,
        Dictionary<Guid, UserEntity> users,
        Dictionary<Guid, int> contributionCounts)
    {
        users.TryGetValue(request.UserId, out var user);
        var adminUser = request.ReviewingAdminId.HasValue && users.TryGetValue(request.ReviewingAdminId.Value, out var admin)
            ? admin
            : null;

        return new AdminShareRequestDto(
            Id: request.Id,
            Status: (ShareRequestStatus)request.Status,
            ContributionType: (ContributionType)request.ContributionType,

            // Game preview
            SourceGameId: request.SourceGameId,
            GameTitle: request.SourceGame?.Title ?? "Unknown Game",
            GameThumbnailUrl: request.SourceGame?.ThumbnailUrl,
            BggId: request.SourceGame?.BggId,

            // Contributor info
            UserId: request.UserId,
            UserName: user?.DisplayName ?? user?.Email ?? "Unknown User",
            UserAvatarUrl: null, // User entity doesn't have avatar URL
            UserTotalContributions: contributionCounts.TryGetValue(request.UserId, out var count) ? count : 0,

            // Request details
            UserNotes: request.UserNotes,
            AttachedDocumentCount: request.AttachedDocuments?.Count ?? 0,
            CreatedAt: request.CreatedAt,

            // Lock info
            IsInReview: request.Status == (int)ShareRequestStatus.InReview,
            ReviewingAdminId: request.ReviewingAdminId,
            ReviewingAdminName: adminUser?.DisplayName ?? adminUser?.Email,
            ReviewStartedAt: request.ReviewStartedAt,

            // For additional content
            TargetSharedGameId: request.TargetSharedGameId,
            TargetSharedGameTitle: request.TargetSharedGame?.Title);
    }
}
