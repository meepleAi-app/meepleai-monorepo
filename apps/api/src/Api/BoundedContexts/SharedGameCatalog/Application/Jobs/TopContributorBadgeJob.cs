using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetBadgeLeaderboard;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Background job that periodically recalculates and awards/revokes TopContributor badges.
/// Runs monthly to identify the top 10 contributors and manage badge assignments.
/// </summary>
/// <remarks>
/// STUB IMPLEMENTATION - Requires background job infrastructure (Hangfire/Quartz).
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
            // Get top 10 contributors for the current month
            var leaderboard = await _mediator.Send(
                new GetBadgeLeaderboardQuery
                {
                    Period = LeaderboardPeriod.Month,
                    PageSize = 10
                },
                context.CancellationToken).ConfigureAwait(false);

            if (leaderboard.Count == 0)
            {
                _logger.LogInformation("No contributors found for TopContributor badge assignment");

                context.Result = new
                {
                    Success = true,
                    Awarded = 0,
                    Revoked = 0,
                    Message = "No contributors found"
                };
                return;
            }

            // Get TopContributor badge definition
            var topContributorBadge = await _badgeRepository.GetByCodeAsync(
                "TOP_CONTRIBUTOR",
                context.CancellationToken).ConfigureAwait(false);

            if (topContributorBadge == null)
            {
                _logger.LogError("TOP_CONTRIBUTOR badge definition not found in system");

                context.Result = new
                {
                    Success = false,
                    Error = "TOP_CONTRIBUTOR badge not found"
                };
                return;
            }

            var topUserIds = leaderboard.Take(10).Select(l => l.UserId).ToHashSet();

            // Award badge to top 10 using RecalculateBadgesCommand for each user
            int awardedCount = 0;
            foreach (var userId in topUserIds)
            {
                try
                {
                    var response = await _mediator.Send(
                        new RecalculateBadgesCommand { UserId = userId },
                        context.CancellationToken).ConfigureAwait(false);

                    awardedCount += response.BadgesAwarded;

                    _logger.LogDebug(
                        "Recalculated badges for top contributor: UserId={UserId}, Awarded={Awarded}",
                        userId,
                        response.BadgesAwarded);
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
                        "Failed to recalculate badges for top contributor: UserId={UserId}",
                        userId);
                    // Continue with next user
                }
#pragma warning restore CA1031
            }

            // Revoke badge from users no longer in top 10
            var currentHolders = await _userBadgeRepository.GetUsersByBadgeCodeAsync(
                "TOP_CONTRIBUTOR",
                context.CancellationToken).ConfigureAwait(false);

            int revokedCount = 0;
            foreach (var holder in currentHolders.Where(h => !topUserIds.Contains(h.UserId)))
            {
                try
                {
                    var response = await _mediator.Send(
                        new RecalculateBadgesCommand { UserId = holder.UserId },
                        context.CancellationToken).ConfigureAwait(false);

                    revokedCount += response.BadgesRevoked;

                    _logger.LogInformation(
                        "Revoked TOP_CONTRIBUTOR badge from user: UserId={UserId}",
                        holder.UserId);
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to revoke badge from user: UserId={UserId}",
                        holder.UserId);
                    // Continue with next user
                }
#pragma warning restore CA1031
            }

            _logger.LogInformation(
                "TopContributorBadgeJob completed: Processed={ProcessedCount} top contributors, Awarded={Awarded}, Revoked={Revoked}",
                topUserIds.Count,
                awardedCount,
                revokedCount);

            context.Result = new
            {
                Success = true,
                Awarded = awardedCount,
                Revoked = revokedCount,
                TopContributorsCount = topUserIds.Count
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
        // Background tasks must not throw exceptions (would terminate task scheduler).
        // Errors logged for monitoring; task failures don't impact application.
#pragma warning restore S125
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
