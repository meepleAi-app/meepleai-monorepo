using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SystemConfiguration.Domain.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserContributionStats;

/// <summary>
/// Handler for GetUserContributionStatsQuery.
/// Issue #2726: Application - Query per Dashboard Utente
/// </summary>
internal sealed class GetUserContributionStatsQueryHandler : IRequestHandler<GetUserContributionStatsQuery, UserContributionStatsDto>
{
    private readonly MeepleAiDbContext _context;
    private readonly IRateLimitEvaluator _rateLimitEvaluator;
    private readonly ILogger<GetUserContributionStatsQueryHandler> _logger;

    public GetUserContributionStatsQueryHandler(
        MeepleAiDbContext context,
        IRateLimitEvaluator rateLimitEvaluator,
        ILogger<GetUserContributionStatsQueryHandler> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _rateLimitEvaluator = rateLimitEvaluator ?? throw new ArgumentNullException(nameof(rateLimitEvaluator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserContributionStatsDto> Handle(
        GetUserContributionStatsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Getting contribution stats for user {UserId}", query.UserId);

        // Get share request stats
        var shareRequestStats = await GetShareRequestStatsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        // Get contribution stats
        var contributionStats = await GetContributionStatsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        // Get rate limit status
        var rateLimitStatus = await _rateLimitEvaluator.GetUserStatusAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        // Calculate consecutive approvals without changes
        var consecutiveApprovals = await CalculateConsecutiveApprovalsAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        // Calculate approval rate
        var approvalRate = shareRequestStats.Total > 0
            ? Math.Round((decimal)shareRequestStats.Approved / shareRequestStats.Total * 100, 2)
            : 0;

        var result = new UserContributionStatsDto(
            UserId: query.UserId,
            TotalShareRequests: shareRequestStats.Total,
            PendingRequests: shareRequestStats.Pending,
            ApprovedRequests: shareRequestStats.Approved,
            RejectedRequests: shareRequestStats.Rejected,
            TotalContributions: contributionStats.TotalContributions,
            GamesContributed: contributionStats.UniqueGames,
            DocumentsContributed: contributionStats.TotalDocuments,
            PrimaryContributions: contributionStats.PrimaryCount,
            ApprovalRate: approvalRate,
            ConsecutiveApprovalsWithoutChanges: consecutiveApprovals,
            FirstContributionAt: contributionStats.FirstContributionAt,
            LastContributionAt: contributionStats.LastContributionAt,
            RateLimitStatus: MapRateLimitStatus(rateLimitStatus));

        _logger.LogInformation(
            "Retrieved contribution stats for user {UserId}: {TotalRequests} requests, {TotalContributions} contributions",
            query.UserId, shareRequestStats.Total, contributionStats.TotalContributions);

        return result;
    }

    private async Task<ShareRequestStats> GetShareRequestStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var requests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Where(r => r.UserId == userId)
            .Select(r => r.Status)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ShareRequestStats(
            Total: requests.Count,
            Pending: requests.Count(s =>
                s == (int)ShareRequestStatus.Pending ||
                s == (int)ShareRequestStatus.InReview ||
                s == (int)ShareRequestStatus.ChangesRequested),
            Approved: requests.Count(s => s == (int)ShareRequestStatus.Approved),
            Rejected: requests.Count(s => s == (int)ShareRequestStatus.Rejected));
    }

    private async Task<ContributionStats> GetContributionStatsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var contributors = await _context.Set<ContributorEntity>()
            .AsNoTracking()
            .Include(c => c.Contributions)
            .Where(c => c.UserId == userId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalContributions = contributors.Sum(c => c.Contributions.Count);
        var uniqueGames = contributors.Count;
        var primaryCount = contributors.Count(c => c.IsPrimaryContributor);

        var totalDocuments = contributors
            .SelectMany(c => c.Contributions)
            .Sum(cr => CountDocuments(cr.DocumentIdsJson));

        var allContributionDates = contributors
            .SelectMany(c => c.Contributions)
            .Select(cr => cr.ContributedAt)
            .ToList();

        return new ContributionStats(
            TotalContributions: totalContributions,
            UniqueGames: uniqueGames,
            TotalDocuments: totalDocuments,
            PrimaryCount: primaryCount,
            FirstContributionAt: allContributionDates.Count > 0 ? allContributionDates.Min() : null,
            LastContributionAt: allContributionDates.Count > 0 ? allContributionDates.Max() : null);
    }

    private async Task<int> CalculateConsecutiveApprovalsAsync(Guid userId, CancellationToken cancellationToken)
    {
        var resolvedRequests = await _context.Set<ShareRequestEntity>()
            .AsNoTracking()
            .Where(r => r.UserId == userId &&
                (r.Status == (int)ShareRequestStatus.Approved || r.Status == (int)ShareRequestStatus.Rejected))
            .OrderByDescending(r => r.ResolvedAt)
            .Select(r => new { r.Status, r.AdminFeedback })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var consecutive = 0;
        foreach (var request in resolvedRequests)
        {
            // Count only approvals without changes requested (no admin feedback indicating changes)
            if (request.Status == (int)ShareRequestStatus.Approved && string.IsNullOrWhiteSpace(request.AdminFeedback))
            {
                consecutive++;
            }
            else
            {
                break;
            }
        }

        return consecutive;
    }

    private static int CountDocuments(string? documentIdsJson)
    {
        if (string.IsNullOrEmpty(documentIdsJson))
            return 0;

        try
        {
            var documentIds = JsonSerializer.Deserialize<List<Guid>>(documentIdsJson);
            return documentIds?.Count ?? 0;
        }
        catch
        {
            return 0;
        }
    }

    private static RateLimitStatusDto MapRateLimitStatus(SystemConfiguration.Domain.ValueObjects.RateLimitStatus status)
    {
        return new RateLimitStatusDto(
            CurrentPendingCount: status.CurrentPendingCount,
            MaxPendingAllowed: status.EffectiveMaxPending,
            CurrentMonthlyCount: status.CurrentMonthlyCount,
            MaxMonthlyAllowed: status.EffectiveMaxPerMonth,
            IsInCooldown: status.IsInCooldown,
            CooldownEndsAt: status.CooldownEndsAt,
            MonthResetAt: status.MonthResetAt);
    }

    private record ShareRequestStats(int Total, int Pending, int Approved, int Rejected);

    private record ContributionStats(
        int TotalContributions,
        int UniqueGames,
        int TotalDocuments,
        int PrimaryCount,
        DateTime? FirstContributionAt,
        DateTime? LastContributionAt);
}
