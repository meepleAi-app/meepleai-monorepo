using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DatabaseSync.Application.Commands;

/// <summary>
/// The audit row of a sync or of a migration apply, written in the local database (#5002).
/// </summary>
/// <remarks>
/// <para>
/// Through <see cref="AuditLogEntity"/>, the type the EF model maps. The handlers used to write
/// <c>Set&lt;AuditLog&gt;()</c>, the Administration aggregate, which it does not, and threw after every commit.
/// </para>
/// <para>
/// Not <c>AuditService.LogAsync</c>: it swallows the failure without saying so, and the handlers report a missing
/// audit row in their result.
/// </para>
/// </remarks>
internal static class DatabaseSyncAuditWriter
{
    /// <summary>
    /// Writes the row and returns false, after logging, when it cannot. It runs once the outcome is decided (after
    /// the commit or the rollback), so it ignores the request's cancellation: an aborted request must not cost the
    /// trace of a change that happened.
    /// </summary>
    public static async Task<bool> TryWriteAsync(
        MeepleAiDbContext dbContext,
        ILogger logger,
        Guid adminUserId,
        string action,
        string resource,
        Guid operationId,
        string result,
        string details)
    {
        var entry = new AuditLogEntity
        {
            Id = Guid.NewGuid(),
            UserId = adminUserId,
            Action = action,
            Resource = resource,
            ResourceId = operationId.ToString(),
            Result = result,
            Details = details,
        };
        dbContext.AuditLogs.Add(entry);
        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            return true;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // Left as Added, the row would be retried by the next SaveChanges of the same scope.
            dbContext.Entry(entry).State = EntityState.Detached;
            logger.LogError(ex,
                "Failed to write the audit row {Action} ({Result}). OperationId={OpId}", action, result, operationId);
            return false;
        }
    }
}
