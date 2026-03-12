using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.LivedIn;

/// <summary>
/// Layer 3: LivedIn seeding. Staging only.
/// Provides realistic demo data for staging environments.
/// </summary>
internal static class LivedInSeeder
{
    public static async Task SeedAsync(
        MeepleAiDbContext db,
        ILogger logger,
        CancellationToken ct)
    {
        logger.LogInformation("LivedIn: seeding staging demo data...");

        await UserLibrarySeeder.SeedAsync(db, logger, ct).ConfigureAwait(false);
        await PlayRecordSeeder.SeedAsync(db, logger, ct).ConfigureAwait(false);
        await ChatHistorySeeder.SeedAsync(db, logger, ct).ConfigureAwait(false);
        await GamificationSeeder.SeedAsync(db, logger, ct).ConfigureAwait(false);

        logger.LogInformation("LivedIn: staging demo data complete");
    }
}
