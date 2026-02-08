using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.Administration.Infrastructure.Scheduling;

/// <summary>
/// Quartz.NET job for cleaning up expired audit log entries.
/// Issue #3691: Retention policy - default 90 days.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AuditLogRetentionJob : IJob
{
    /// <summary>
    /// Default retention period in days.
    /// </summary>
    internal const int DefaultRetentionDays = 90;

    private readonly MeepleAiDbContext _db;
    private readonly ILogger<AuditLogRetentionJob> _logger;

    public AuditLogRetentionJob(
        MeepleAiDbContext db,
        ILogger<AuditLogRetentionJob> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "Starting audit log retention cleanup: RetentionDays={RetentionDays}, FireTime={FireTime}",
            DefaultRetentionDays, context.FireTimeUtc);

        try
        {
            var cutoffDate = DateTime.UtcNow.AddDays(-DefaultRetentionDays);

            var expiredEntries = await _db.AuditLogs
                .Where(a => a.CreatedAt < cutoffDate)
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            var deletedCount = expiredEntries.Count;

            if (deletedCount > 0)
            {
                _db.AuditLogs.RemoveRange(expiredEntries);
                await _db.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Audit log retention cleanup completed: DeletedCount={DeletedCount}, CutoffDate={CutoffDate}",
                deletedCount, cutoffDate);

            context.Result = new
            {
                Success = true,
                DeletedCount = deletedCount,
                CutoffDate = cutoffDate
            };
        }
#pragma warning disable CA1031 // Background task: errors must not propagate
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Audit log retention cleanup failed: RetentionDays={RetentionDays}",
                DefaultRetentionDays);

            context.Result = new
            {
                Success = false,
                Error = ex.Message
            };
        }
#pragma warning restore CA1031
    }
}
