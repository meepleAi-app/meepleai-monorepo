using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetShareRequestDetails;

/// <summary>
/// Handler for GetShareRequestDetailsQuery.
/// Issue #2727: Application - Query per Admin Dashboard
/// </summary>
internal sealed class GetShareRequestDetailsQueryHandler : IRequestHandler<GetShareRequestDetailsQuery, ShareRequestDetailsDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<GetShareRequestDetailsQueryHandler> _logger;

    public GetShareRequestDetailsQueryHandler(
        MeepleAiDbContext context,
        ILogger<GetShareRequestDetailsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ShareRequestDetailsDto> Handle(
        GetShareRequestDetailsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Getting share request details: ShareRequestId={ShareRequestId}, AdminId={AdminId}",
            query.ShareRequestId,
            query.AdminId);

        // Fetch share request with all related data
        var request = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Include(r => r.SourceGame)
                .ThenInclude(g => g.Categories)
            .Include(r => r.SourceGame)
                .ThenInclude(g => g.Mechanics)
            .Include(r => r.TargetSharedGame)
                .ThenInclude(g => g!.Categories)
            .Include(r => r.TargetSharedGame)
                .ThenInclude(g => g!.Mechanics)
            .Include(r => r.AttachedDocuments)
            .FirstOrDefaultAsync(r => r.Id == query.ShareRequestId, cancellationToken)
            .ConfigureAwait(false);

        if (request == null)
        {
            throw new KeyNotFoundException($"ShareRequest with ID {query.ShareRequestId} was not found");
        }

        // Fetch user info for contributor
        var user = await _context.Set<UserEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Fetch admin info if request is in review
        UserEntity? reviewingAdmin = null;
        if (request.ReviewingAdminId.HasValue)
        {
            reviewingAdmin = await _context.Set<UserEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.ReviewingAdminId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        // Get contributor stats
        var contributorStats = await GetContributorStatsAsync(request.UserId, cancellationToken)
            .ConfigureAwait(false);

        // Get history from audit logs
        var history = await GetShareRequestHistoryAsync(query.ShareRequestId, cancellationToken)
            .ConfigureAwait(false);

        var dto = MapToDetailsDto(
            request,
            user,
            reviewingAdmin,
            contributorStats,
            history,
            query.AdminId);

        _logger.LogInformation(
            "Retrieved share request details for ShareRequestId={ShareRequestId}",
            query.ShareRequestId);

        return dto;
    }

    private async Task<ContributorStatsData> GetContributorStatsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        // Count total contributions
        var totalContributions = await _context.Set<ContributorEntity>()
            .AsNoTracking()
            .CountAsync(c => c.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        // Count approved share requests
        var approvedRequests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId && r.Status == (int)ShareRequestStatus.Approved, cancellationToken)
            .ConfigureAwait(false);

        // Count total share requests
        var totalRequests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .CountAsync(r => r.UserId == userId, cancellationToken)
            .ConfigureAwait(false);

        return new ContributorStatsData(
            TotalContributions: totalContributions,
            ApprovedContributions: approvedRequests,
            TotalRequests: totalRequests);
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
                history.Add(new ShareRequestHistoryEntryDto(
                    Timestamp: request.CreatedAt,
                    Action: ShareRequestHistoryAction.Created,
                    ActorId: request.UserId,
                    ActorName: null,
                    Details: "Share request created"));
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

    private static ShareRequestDetailsDto MapToDetailsDto(
        ShareRequestEntity request,
        UserEntity? user,
        UserEntity? reviewingAdmin,
        ContributorStatsData contributorStats,
        List<ShareRequestHistoryEntryDto> history,
        Guid currentAdminId)
    {
        var sourceGame = MapToGameDetailsDto(request.SourceGame);
        var targetGame = request.TargetSharedGame != null
            ? MapToGameDetailsDto(request.TargetSharedGame)
            : null;

        var contributor = new ContributorProfileDto(
            UserId: request.UserId,
            UserName: user?.DisplayName ?? user?.Email ?? "Unknown User",
            AvatarUrl: null, // User entity doesn't have avatar
            JoinedAt: user?.CreatedAt ?? DateTime.MinValue,
            TotalContributions: contributorStats.TotalContributions,
            ApprovedContributions: contributorStats.ApprovedContributions,
            ApprovalRate: contributorStats.TotalRequests > 0
                ? (decimal)contributorStats.ApprovedContributions / contributorStats.TotalRequests
                : 0m,
            Badges: []); // Badge system not yet implemented in DB

        var documents = request.AttachedDocuments
            .Select(d => new DocumentPreviewDto(
                DocumentId: d.DocumentId,
                FileName: d.FileName,
                ContentType: d.ContentType,
                FileSize: d.FileSize,
                PreviewUrl: null, // Would be generated by a document service
                PageCount: null)) // Would be extracted from document metadata
            .ToList();

        var lockStatus = new LockStatusDto(
            IsLocked: request.Status == (int)ShareRequestStatus.InReview,
            IsLockedByCurrentAdmin: request.ReviewingAdminId == currentAdminId,
            LockedByAdminId: request.ReviewingAdminId,
            LockedByAdminName: reviewingAdmin?.DisplayName ?? reviewingAdmin?.Email,
            LockExpiresAt: request.ReviewLockExpiresAt);

        return new ShareRequestDetailsDto(
            Id: request.Id,
            Status: (ShareRequestStatus)request.Status,
            ContributionType: (ContributionType)request.ContributionType,
            SourceGame: sourceGame,
            TargetSharedGame: targetGame,
            Contributor: contributor,
            UserNotes: request.UserNotes,
            AttachedDocuments: documents,
            History: history,
            LockStatus: lockStatus,
            CreatedAt: request.CreatedAt,
            ResolvedAt: request.ResolvedAt);
    }

    private static GameDetailsDto MapToGameDetailsDto(SharedGameEntity game)
    {
        return new GameDetailsDto(
            Id: game.Id,
            Title: game.Title,
            Description: game.Description,
            ThumbnailUrl: game.ThumbnailUrl,
            BggId: game.BggId,
            MinPlayers: game.MinPlayers,
            MaxPlayers: game.MaxPlayers,
            PlayingTime: game.PlayingTimeMinutes,
            Complexity: game.ComplexityRating,
            Categories: game.Categories?.Select(c => c.Name ?? "Unknown").ToList() ?? [],
            Mechanisms: game.Mechanics?.Select(m => m.Name ?? "Unknown").ToList() ?? []);
    }

    private sealed record ContributorStatsData(
        int TotalContributions,
        int ApprovedContributions,
        int TotalRequests);
}
