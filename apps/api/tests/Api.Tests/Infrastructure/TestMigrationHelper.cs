using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Shared migration retry helper for integration tests against Testcontainers Postgres.
/// Extracted from 8 duplicated implementations (issue #2162 follow-up).
/// </summary>
/// <remarks>
/// PR2 follow-up to #1684: Testcontainers on Docker Desktop Windows produces
/// transient connection drops under load. Wrap <see cref="DatabaseFacade.MigrateAsync"/>
/// in a small retry loop so those surface as test failures only when persistent.
/// </remarks>
internal static class TestMigrationHelper
{
    /// <summary>
    /// Apply EF Core migrations with retry on transient failures.
    /// </summary>
    /// <param name="context">DbContext to migrate.</param>
    /// <param name="cancellationToken">Cancellation token, typically <c>TestContext.Current.CancellationToken</c>.</param>
    /// <param name="maxRetries">Number of attempts before bubbling up the last failure (default 3).</param>
    /// <param name="onRetry">Optional callback for logging each retry attempt (signature: message string).</param>
    public static async Task MigrateWithRetryAsync(
        MeepleAiDbContext context,
        CancellationToken cancellationToken,
        int maxRetries = 3,
        Action<string>? onRetry = null)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                await context.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (i < maxRetries - 1 && ex is not OperationCanceledException)
            {
                onRetry?.Invoke($"Migration attempt {i + 1} failed: {ex.Message}. Retrying...");
                await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
