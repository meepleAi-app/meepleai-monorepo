using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Background job that awards and revokes the leaderboard badges, the ones with requirement TopContributor
/// (TOP_CONTRIBUTOR, TOP_THREE, NUMBER_ONE). Runs on the 1st of each month and judges the month just closed (#5136,
/// decision of 2026-11-23). It recalculates everyone ranked within the largest TopContributorRank of those badges, and
/// every current holder of one of them, so the ones who left the ranking lose it. RecalculateBadges decides, through
/// BadgeEvaluator, which reads the same ranks.
/// </summary>
/// <remarks>
/// Issue #2728: Application - Badge Assignment Handlers
/// </remarks>
[DisallowConcurrentExecution]
internal sealed class TopContributorBadgeJob : IJob
{
    private readonly IMediator _mediator;
    private readonly IBadgeRepository _badgeRepository;
    private readonly IUserBadgeRepository _userBadgeRepository;
    private readonly ILogger<TopContributorBadgeJob> _logger;

    public TopContributorBadgeJob(
        IMediator mediator,
        IBadgeRepository badgeRepository,
        IUserBadgeRepository userBadgeRepository,
        ILogger<TopContributorBadgeJob> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _badgeRepository = badgeRepository ?? throw new ArgumentNullException(nameof(badgeRepository));
        _userBadgeRepository = userBadgeRepository ?? throw new ArgumentNullException(nameof(userBadgeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "TopContributorBadgeJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // The leaderboard badges and their ranks come from the badge definitions, not from a list written here: a new
            // one, or a different rank, is picked up without touching the job.
            var leaderboardBadges = (await _badgeRepository.GetAllActiveAsync(context.CancellationToken).ConfigureAwait(false))
                .Where(b => b.Requirement.Type == BadgeRequirementType.TopContributor && b.Requirement.TopContributorRank.HasValue)
                .ToList();

            if (leaderboardBadges.Count == 0)
            {
                _logger.LogWarning("No active leaderboard badge: nothing to award");
                context.Result = new { Success = true, Awarded = 0, Revoked = 0, Message = "No leaderboard badges" };
                return;
            }

            var widestRank = leaderboardBadges.Max(b => b.Requirement.TopContributorRank!.Value);

            // The ranks of the month just closed. Empty or not, the holders below are recalculated: a month with no
            // contributor takes the badges away from last month's (review of #5137).
            var ranks = await _mediator.Send(new GetPreviousMonthContributionRanksQuery(), context.CancellationToken)
                .ConfigureAwait(false);
            var candidates = ranks.Where(r => r.Value <= widestRank).Select(r => r.Key).ToHashSet();

            foreach (var badge in leaderboardBadges)
            {
                var holders = await _userBadgeRepository.GetUsersByBadgeCodeAsync(badge.Code, context.CancellationToken)
                    .ConfigureAwait(false);
                candidates.UnionWith(holders.Select(h => h.UserId));
            }

            int awardedCount = 0;
            int revokedCount = 0;
            foreach (var userId in candidates)
            {
                try
                {
                    var response = await _mediator.Send(
                        new RecalculateBadgesCommand { UserId = userId },
                        context.CancellationToken).ConfigureAwait(false);

                    awardedCount += response.BadgesAwarded;
                    revokedCount += response.BadgesRevoked;

                    _logger.LogDebug(
                        "Recalculated badges for leaderboard candidate: UserId={UserId}, Awarded={Awarded}, Revoked={Revoked}",
                        userId,
                        response.BadgesAwarded,
                        response.BadgesRevoked);
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
                // Background tasks must not throw exceptions (would terminate task scheduler).
                // Errors logged for monitoring; individual failures don't prevent other assignments.
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to recalculate badges for leaderboard candidate: UserId={UserId}",
                        userId);
                    // Continue with next user
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "TopContributorBadgeJob completed: Candidates={Candidates}, Awarded={Awarded}, Revoked={Revoked}",
                candidates.Count,
                awardedCount,
                revokedCount);

            context.Result = new
            {
                Success = true,
                Awarded = awardedCount,
                Revoked = revokedCount
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // BACKGROUND SERVICE: Job-level error isolation
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "TopContributorBadgeJob failed");

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };

            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}
