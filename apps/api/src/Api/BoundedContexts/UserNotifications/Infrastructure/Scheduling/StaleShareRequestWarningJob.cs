using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.UserNotifications.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for warning admins about stale share requests.
/// ISSUE-2740: Stale request warning system for share request management.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class StaleShareRequestWarningJob : IJob
{
    private readonly IShareRequestRepository _shareRequestRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StaleShareRequestWarningJob> _logger;

    public StaleShareRequestWarningJob(
        IShareRequestRepository shareRequestRepository,
        INotificationRepository notificationRepository,
        MeepleAiDbContext dbContext,
        IConfiguration configuration,
        ILogger<StaleShareRequestWarningJob> logger)
    {
        _shareRequestRepository = shareRequestRepository ?? throw new ArgumentNullException(nameof(shareRequestRepository));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the stale request warning job.
    /// Scheduled to run every 6 hours via Quartz configuration.
    /// </summary>
    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting stale share request warning job: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            // Get stale threshold from configuration (default: 7 days)
            var staleDays = _configuration.GetValue<int>("ShareRequest:StaleThresholdDays", 7);
            var staleThreshold = DateTime.UtcNow.AddDays(-staleDays);

            // Get stale pending requests
            var staleRequests = await _shareRequestRepository
                .GetPendingOlderThanAsync(staleThreshold, context.CancellationToken)
                .ConfigureAwait(false);

            if (staleRequests.Count == 0)
            {
                _logger.LogInformation("No stale share requests found");
                context.Result = new { Success = true, StaleCount = 0 };
                return;
            }

            // Get all admin users
            var admins = await _dbContext.Set<UserEntity>()
                .AsNoTracking()
                .Where(u => u.Role == "admin")
                .Select(u => u.Id)
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            if (admins.Count == 0)
            {
                _logger.LogWarning("No admin users found to notify about stale requests");
                context.Result = new { Success = true, Skipped = true, Reason = "No admins" };
                return;
            }

            // Calculate oldest request age
            var oldestDays = staleRequests
                .Max(r => (DateTime.UtcNow - r.CreatedAt).Days);

            // Create high-priority notification for each admin
            foreach (var adminId in admins)
            {
                var warning = new Notification(
                    id: Guid.NewGuid(),
                    userId: adminId,
                    type: NotificationType.AdminStaleShareRequests,
                    severity: NotificationSeverity.Warning,
                    title: $"{staleRequests.Count} Share Requests Need Attention",
                    message: $"There are {staleRequests.Count} share requests waiting more than {staleDays} days.",
                    link: "/admin/share-requests?sort=oldest",
                    metadata: System.Text.Json.JsonSerializer.Serialize(new
                    {
                        staleCount = staleRequests.Count,
                        oldestDays,
                        thresholdDays = staleDays
                    }));

                await _notificationRepository.AddAsync(warning, context.CancellationToken).ConfigureAwait(false);
            }

            await _dbContext.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Stale share request warning job completed: StaleCount={StaleCount}, OldestDays={OldestDays}, AdminsNotified={AdminCount}",
                staleRequests.Count,
                oldestDays,
                admins.Count);

            context.Result = new
            {
                Success = true,
                StaleCount = staleRequests.Count,
                OldestDays = oldestDays,
                AdminsNotified = admins.Count
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Stale share request warning job failed");
            context.Result = new { Success = false, Error = ex.Message };
            // Don't rethrow - Quartz will mark job as failed
        }
#pragma warning restore CA1031
    }
}
