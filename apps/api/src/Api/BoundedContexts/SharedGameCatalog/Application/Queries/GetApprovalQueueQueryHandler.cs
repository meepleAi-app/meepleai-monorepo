using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for retrieving the approval queue of games pending approval.
/// Calculates days pending and PDF count for each game.
/// Supports filtering by urgency (>7 days pending), submitter, and PDF presence.
/// Issue #3533: Admin API Endpoints - Approval Queue Management
/// </summary>
internal sealed class GetApprovalQueueQueryHandler : IRequestHandler<GetApprovalQueueQuery, IReadOnlyList<ApprovalQueueItemDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetApprovalQueueQueryHandler> _logger;

    public GetApprovalQueueQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetApprovalQueueQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ApprovalQueueItemDto>> Handle(GetApprovalQueueQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting approval queue: Urgency={Urgency}, Submitter={Submitter}, HasPdfs={HasPdfs}",
            query.Urgency,
            query.Submitter,
            query.HasPdfs);

        var now = DateTime.UtcNow;
        const int urgencyThresholdDays = 7;

        // Start with games in PendingApproval status
        var dbQuery = _context.SharedGames
            .AsNoTracking()
            .Where(g => g.Status == (int)GameStatus.PendingApproval);

        // Apply submitter filter if specified
        if (query.Submitter.HasValue)
        {
            dbQuery = dbQuery.Where(g => g.CreatedBy == query.Submitter.Value);
        }

        // Get all matching games with their document counts, joining users for display info
        var games = await dbQuery
            .Join(
                _context.Users,
                g => g.CreatedBy,
                u => u.Id,
                (g, u) => new
                {
                    g.Id,
                    g.Title,
                    g.CreatedBy,
                    g.CreatedAt,
                    UserDisplayName = u.DisplayName ?? u.Email,
                    UserEmail = u.Email,
                    DocumentCount = g.Documents.Count()
                })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Apply client-side filtering and transformation
        var queueItems = games
            .Where(g =>
            {
                // Filter by urgency if specified (>7 days pending)
                if (query.Urgency.HasValue && query.Urgency.Value)
                {
                    var daysPending = (now - g.CreatedAt).Days;
                    if (daysPending < urgencyThresholdDays)
                        return false;
                }

                // Filter by PDF presence if specified
                if (query.HasPdfs.HasValue)
                {
                    bool hasPdfs = g.DocumentCount > 0;
                    if (hasPdfs != query.HasPdfs.Value)
                        return false;
                }

                return true;
            })
            .Select(g => new ApprovalQueueItemDto(
                GameId: g.Id,
                Title: g.Title,
                SubmittedBy: g.CreatedBy,
                SubmittedByName: g.UserDisplayName,
                SubmittedByEmail: g.UserEmail,
                SubmittedAt: g.CreatedAt,
                DaysPending: (now - g.CreatedAt).Days,
                PdfCount: g.DocumentCount))
            .OrderBy(g => g.SubmittedAt) // Oldest submissions first
            .ToList()
            .AsReadOnly();

        _logger.LogInformation(
            "Retrieved {Count} games from approval queue",
            queueItems.Count);

        return queueItems;
    }
}
