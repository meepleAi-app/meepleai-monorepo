using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestHistory;

/// <summary>
/// Handler for GetShareRequestHistoryQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetShareRequestHistoryQueryHandler : IRequestHandler<GetShareRequestHistoryQuery, IReadOnlyList<ShareRequestHistoryEntryDto>>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetShareRequestHistoryQueryHandler> _logger;

    public GetShareRequestHistoryQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetShareRequestHistoryQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<ShareRequestHistoryEntryDto>> Handle(
        GetShareRequestHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting share request history: ShareRequestId={ShareRequestId}",
            query.ShareRequestId);

        // Verify share request exists
        var requestExists = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .AnyAsync(r => r.Id == query.ShareRequestId, cancellationToken)
            .ConfigureAwait(false);

        if (!requestExists)
        {
            throw new KeyNotFoundException($"ShareRequest with ID {query.ShareRequestId} was not found");
        }

        var history = await GetShareRequestHistoryAsync(query.ShareRequestId, cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} history entries for ShareRequestId={ShareRequestId}",
            history.Count,
            query.ShareRequestId);

        return history;
    }

    private async Task<List<ShareRequestHistoryEntryDto>> GetShareRequestHistoryAsync(
        Guid shareRequestId,
        CancellationToken cancellationToken)
    {
        var history = new List<ShareRequestHistoryEntryDto>();

        // Get audit logs for this share request
        var auditLogs = await _context.Set<AuditLogEntity>()
            .AsNoTracking()
            .Where(a => a.Resource == "ShareRequest" && a.ResourceId == shareRequestId.ToString())
            .OrderBy(a => a.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Get user IDs from audit logs
        var userIds = auditLogs
            .Where(a => a.UserId.HasValue)
            .Select(a => a.UserId!.Value)
            .Distinct()
            .ToList();

        var users = await _context.Set<UserEntity>()
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken)
            .ConfigureAwait(false);

        foreach (var log in auditLogs)
        {
            var action = MapAuditActionToHistoryAction(log.Action);
            if (action == null) continue;

            string? actorName = null;
            if (log.UserId.HasValue && users.TryGetValue(log.UserId.Value, out var actor))
            {
                actorName = actor.DisplayName ?? actor.Email;
            }

            history.Add(new ShareRequestHistoryEntryDto(
                Timestamp: log.CreatedAt,
                Action: action.Value,
                ActorId: log.UserId,
                ActorName: actorName,
                Details: log.Details));
        }

        // If no audit logs exist, reconstruct minimal history from entity data
        if (history.Count == 0)
        {
            var request = await _context.Set<ShareRequestEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == shareRequestId, cancellationToken)
                .ConfigureAwait(false);

            if (request != null)
            {
                // Get user name for created event
                UserEntity? requestUser = null;
                if (users.TryGetValue(request.UserId, out var user))
                {
                    requestUser = user;
                }
                else
                {
                    requestUser = await _context.Set<UserEntity>()
                        .AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
                        .ConfigureAwait(false);
                }

                history.Add(new ShareRequestHistoryEntryDto(
                    Timestamp: request.CreatedAt,
                    Action: ShareRequestHistoryAction.Created,
                    ActorId: request.UserId,
                    ActorName: requestUser?.DisplayName ?? requestUser?.Email,
                    Details: "Share request created"));

                // Add current status event if not pending
                if (request.Status != (int)ShareRequestStatus.Pending && request.ResolvedAt.HasValue)
                {
                    var statusAction = (ShareRequestStatus)request.Status switch
                    {
                        ShareRequestStatus.InReview => ShareRequestHistoryAction.ReviewStarted,
                        ShareRequestStatus.ChangesRequested => ShareRequestHistoryAction.ChangesRequested,
                        ShareRequestStatus.Approved => ShareRequestHistoryAction.Approved,
                        ShareRequestStatus.Rejected => ShareRequestHistoryAction.Rejected,
                        ShareRequestStatus.Withdrawn => ShareRequestHistoryAction.Withdrawn,
                        _ => (ShareRequestHistoryAction?)null
                    };

                    if (statusAction.HasValue)
                    {
                        history.Add(new ShareRequestHistoryEntryDto(
                            Timestamp: request.ResolvedAt.Value,
                            Action: statusAction.Value,
                            ActorId: request.ReviewingAdminId,
                            ActorName: null,
                            Details: $"Status changed to {(ShareRequestStatus)request.Status}"));
                    }
                }
            }
        }

        return history;
    }

    private static ShareRequestHistoryAction? MapAuditActionToHistoryAction(string action)
    {
        return action.ToLowerInvariant() switch
        {
            "create" or "created" => ShareRequestHistoryAction.Created,
            "documentsadded" or "documentsupdated" => ShareRequestHistoryAction.DocumentsUpdated,
            "reviewstarted" or "startreview" => ShareRequestHistoryAction.ReviewStarted,
            "reviewreleased" or "releasereview" => ShareRequestHistoryAction.ReviewReleased,
            "changesrequested" or "requestchanges" => ShareRequestHistoryAction.ChangesRequested,
            "resubmit" or "resubmitted" => ShareRequestHistoryAction.Resubmitted,
            "approve" or "approved" => ShareRequestHistoryAction.Approved,
            "reject" or "rejected" => ShareRequestHistoryAction.Rejected,
            "withdraw" or "withdrawn" => ShareRequestHistoryAction.Withdrawn,
            _ => null
        };
    }
}
