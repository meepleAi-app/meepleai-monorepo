using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// PostgreSQL advisory lock wrapper for safe multi-replica seeding.
/// Uses non-blocking try-lock: if another replica holds the lock, this one skips.
/// </summary>
internal static class AdvisoryLockHelper
{
    /// <summary>
    /// Lock key matching the original SeedOrchestrator constant ("MeepleAI" as long).
    /// Must remain stable across deployments for rolling-update safety.
    /// </summary>
    public const long SeedingLockKey = 0x4D65_6570_6C65_4149;

    /// <summary>
    /// Attempts to acquire the advisory lock without blocking.
    /// Returns true if acquired, false if another replica holds it.
    /// </summary>
    public static async Task<bool> TryAcquireAsync(MeepleAiDbContext db, ILogger logger, CancellationToken ct = default)
    {
        logger.LogInformation("Attempting seeding advisory lock (key={LockKey})...", SeedingLockKey);

        var acquired = await db.Database
            .SqlQueryRaw<bool>($"SELECT pg_try_advisory_lock({SeedingLockKey}) AS \"Value\"")
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (acquired)
            logger.LogInformation("Seeding advisory lock acquired");
        else
            logger.LogInformation("Another replica holds the seeding lock — skipping");

        return acquired;
    }

    /// <summary>
    /// Releases the advisory lock.
    /// </summary>
    public static async Task ReleaseAsync(MeepleAiDbContext db, ILogger logger, CancellationToken ct = default)
    {
        await db.Database
            .ExecuteSqlRawAsync($"SELECT pg_advisory_unlock({SeedingLockKey})", ct)
            .ConfigureAwait(false);
        logger.LogInformation("Seeding advisory lock released");
    }
}
