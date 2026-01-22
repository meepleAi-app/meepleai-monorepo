using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Jobs;

/// <summary>
/// Background job that automatically releases expired review locks on share requests.
/// Runs every 5 minutes to identify and release reviews that have exceeded their lock duration.
/// </summary>
/// <remarks>
/// Issue #2729: Application - Review Lock Management
/// This job ensures that review locks don't block other admins indefinitely.
/// When a lock expires, the share request returns to its previous state (Pending or ChangesRequested).
/// </remarks>
[DisallowConcurrentExecution]
internal sealed class AutoReleaseExpiredReviewsJob : IJob
{
    private readonly IShareRequestRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AutoReleaseExpiredReviewsJob> _logger;

    public AutoReleaseExpiredReviewsJob(
        IShareRequestRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<AutoReleaseExpiredReviewsJob> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting auto-release of expired review locks: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Find all share requests with expired review locks
            var staleReviews = await _repository.GetStaleReviewsAsync(
                TimeSpan.Zero, // Any review that has passed its expiration time
                context.CancellationToken).ConfigureAwait(false);

            // Filter to only those truly expired (GetStaleReviewsAsync may use ReviewStartedAt + duration)
            var expiredReviews = staleReviews
                .Where(sr => sr.IsLockExpired())
                .ToList();

            _logger.LogInformation(
                "Found {Count} expired reviews to release",
                expiredReviews.Count);

            if (expiredReviews.Count == 0)
            {
                context.Result = new
                {
                    Success = true,
                    ReleasedCount = 0,
                    TotalExpired = 0,
                    Message = "No expired reviews found"
                };
                return;
            }

            int releasedCount = 0;

            foreach (var shareRequest in expiredReviews)
            {
                try
                {
                    var previousAdminId = shareRequest.ReviewingAdminId;
                    var previousStatus = shareRequest.Status;

                    // Expire the lock (domain method handles state transition)
                    shareRequest.ExpireLock();

                    _repository.Update(shareRequest);
                    releasedCount++;

                    _logger.LogInformation(
                        "Auto-released expired review: ShareRequestId={ShareRequestId}, PreviousAdmin={AdminId}, PreviousStatus={Status}",
                        shareRequest.Id, previousAdminId, previousStatus);
                }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
                // BACKGROUND SERVICE: BACKGROUND TASK PATTERN - Scheduled task error isolation
                // Background tasks must not throw exceptions (would terminate task scheduler).
                // Errors logged for monitoring; individual failures don't prevent other releases.
#pragma warning restore S125
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to auto-release expired review: ShareRequestId={ShareRequestId}",
                        shareRequest.Id);
                    // Continue with next item
                }
#pragma warning restore CA1031
            }

            // Save all changes in a single transaction
            await _unitOfWork.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Auto-release of expired reviews completed: Released={Released}/{Total}",
                releasedCount, expiredReviews.Count);

            context.Result = new
            {
                Success = true,
                ReleasedCount = releasedCount,
                TotalExpired = expiredReviews.Count,
                FailedCount = expiredReviews.Count - releasedCount
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
                "Auto-release of expired reviews job failed");

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
