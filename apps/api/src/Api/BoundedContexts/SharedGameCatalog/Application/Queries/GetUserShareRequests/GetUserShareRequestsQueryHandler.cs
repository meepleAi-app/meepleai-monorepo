using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserShareRequests;

/// <summary>
/// Handler for GetUserShareRequestsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserShareRequestsQueryHandler : IRequestHandler<GetUserShareRequestsQuery, PagedResult<UserShareRequestDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetUserShareRequestsQueryHandler> _logger;

    public GetUserShareRequestsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetUserShareRequestsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<UserShareRequestDto>> Handle(
        GetUserShareRequestsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting share requests for user {UserId}: StatusFilter={StatusFilter}, Page={Page}, PageSize={PageSize}",
            query.UserId,
            query.StatusFilter?.ToString() ?? "All",
            query.PageNumber,
            query.PageSize);

        var dbQuery = _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(r => r.SourceGame)
            .Include(r => r.AttachedDocuments)
            .Where(r => r.UserId == query.UserId);

        // Apply status filter if provided
        if (query.StatusFilter.HasValue)
        {
            dbQuery = dbQuery.Where(r => r.Status == (int)query.StatusFilter.Value);
        }

        // Order by most recent first
        dbQuery = dbQuery.OrderByDescending(r => r.CreatedAt);

        // Get total count
        var total = await dbQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        // Apply pagination
        var requests = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new UserShareRequestDto(
                r.Id,
                r.SourceGameId,
                r.SourceGame != null ? r.SourceGame.Title : "Unknown Game",
                r.SourceGame != null ? r.SourceGame.ThumbnailUrl : null,
                (ShareRequestStatus)r.Status,
                (ContributionType)r.ContributionType,
                r.UserNotes,
                r.AdminFeedback,
                r.AttachedDocuments.Count,
                r.CreatedAt,
                r.ResolvedAt,
                r.TargetSharedGameId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} share requests (Total: {Total}) for user {UserId}",
            requests.Count, total, query.UserId);

        return new PagedResult<UserShareRequestDto>(
            Items: requests,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }
}
