using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Api.SharedKernel.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for sending daily digest of pending share requests to admins.
/// ISSUE-2740: Daily admin digest email with pending share request statistics.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AdminShareRequestDigestJob : IJob
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IPublicBaseUrlProvider _publicBaseUrl;
    private readonly ILogger<AdminShareRequestDigestJob> _logger;

    public AdminShareRequestDigestJob(
        IShareRequestRepository shareRequestRepository,
        MeepleAiDbContext dbContext,
        IEmailService emailService,
        IPublicBaseUrlProvider publicBaseUrl,
        ILogger<AdminShareRequestDigestJob> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _publicBaseUrl = publicBaseUrl ?? throw new ArgumentNullException(nameof(publicBaseUrl));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the daily digest job.
    /// Scheduled to run daily at 9:00 AM UTC via Quartz configuration.
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting admin share request digest job: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Get pending stats
            var pendingStats = await _shareRequestRepository
                .GetPendingStatsAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (pendingStats.TotalPending == 0)
            {
                _logger.LogInformation("No pending share requests, skipping digest email");
                context.Result = new { Success = true, Skipped = true, Reason = "No pending requests" };
                return;
            }

            // #4774: the review queue is a page of the front-end, whose address is configured in one place. Resolved
            // once, before anything else is read and before the first send: with no address configured (a deployed
            // host) the job ends in the catch below instead of mailing every admin a link that leads nowhere.
            var reviewQueueUrl = $"{_publicBaseUrl.GetBaseUrl()}/admin/share-requests";

            // Get all admin users
            // NOTE: Can be refactored to IUserRepository.GetByRoleAsync when implemented
            var admins = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => new { u.Id, u.Email, u.DisplayName })
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (admins.Count == 0)
            {
                _logger.LogWarning("No admin users found for digest email");
                context.Result = new { Success = true, Skipped = true, Reason = "No admins" };
                return;
            }

            // Send digest email to each admin
            var sentCount = 0;

            foreach (var admin in admins)
            {
                try
                {
                    await _emailService.SendAdminShareRequestDigestEmailAsync(
                        admin.Email,
                        admin.DisplayName ?? "Admin",
                        pendingStats.TotalPending,
                        (int)pendingStats.OldestPendingAge.TotalDays,
                        pendingStats.CreatedToday,
                        pendingStats.ByType,
                        reviewQueueUrl,
                        context.CancellationToken).ConfigureAwait(false);

                    sentCount++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send digest to admin {AdminId}", admin.Id);
                    // Continue with other admins
                }
            }

            _logger.LogInformation(
                "Admin share request digest job completed: Sent={SentCount}, TotalPending={TotalPending}",
                sentCount,
                pendingStats.TotalPending);

            context.Result = new
            {
                Success = true,
                SentCount = sentCount,
                TotalPending = pendingStats.TotalPending,
                Stats = pendingStats
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin share request digest job failed");
            context.Result = new { Success = false, Error = ex.Message };
            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}
