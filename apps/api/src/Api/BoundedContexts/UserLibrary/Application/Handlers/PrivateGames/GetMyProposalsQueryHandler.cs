using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.Queries.PrivateGames;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.PrivateGames;

/// <summary>
/// Handler for GetMyProposalsQuery.
/// Returns user's private game proposals (NewGameProposal ShareRequests).
/// Issue #3665: Phase 4 - Proposal System.
/// </summary>
internal sealed class GetMyProposalsQueryHandler : IRequestHandler<GetMyProposalsQuery, PagedResult<UserShareRequestDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetMyProposalsQueryHandler> _logger;

    public GetMyProposalsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetMyProposalsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<UserShareRequestDto>> Handle(
        GetMyProposalsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting proposals for user {UserId}: StatusFilter={StatusFilter}, Page={Page}, PageSize={PageSize}",
            query.UserId,
            query.StatusFilter?.ToString() ?? "All",
            query.PageNumber,
            query.PageSize);

        var dbQuery = _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(r => r.PrivateGame)
            .Include(r => r.AttachedDocuments)
            .Where(r => r.UserId == query.UserId &&
                       r.ContributionType == (int)ContributionType.NewGameProposal);

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
        var proposals = await dbQuery
            .Skip((query.PageNumber - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(r => new UserShareRequestDto(
                r.Id,
                r.SourceGameId,
                r.PrivateGame != null ? r.PrivateGame.Title : "Unknown Game",
                r.PrivateGame != null ? r.PrivateGame.ThumbnailUrl : null,
                (ShareRequestStatus)r.Status,
                ContributionType.NewGameProposal,
                r.UserNotes,
                r.AdminFeedback,
                r.AttachedDocuments.Count,
                r.CreatedAt,
                r.ResolvedAt,
                r.TargetSharedGameId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} proposals (Total: {Total}) for user {UserId}",
            proposals.Count, total, query.UserId);

        return new PagedResult<UserShareRequestDto>(
            Items: proposals,
            Total: total,
            Page: query.PageNumber,
            PageSize: query.PageSize);
    }
}
