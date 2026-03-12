using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.LivedIn;

internal static class ChatHistorySeeder
{
    public static Task SeedAsync(MeepleAiDbContext db, ILogger logger, CancellationToken ct)
    {
        logger.LogInformation("LivedIn: ChatHistory seeding (stub - will be implemented for staging)");
        return Task.CompletedTask;
    }
}
