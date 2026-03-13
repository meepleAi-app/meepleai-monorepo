using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders.LivedIn;

/// <summary>
/// LivedIn seed layer: synthetic game sessions, activity data.
/// Only runs in Dev profile for realistic development data.
/// Currently a placeholder — will be expanded with synthetic data generators.
/// </summary>
internal sealed class LivedInSeedLayer : ISeedLayer
{
    public string Name => "LivedIn";
    public SeedProfile MinimumProfile => SeedProfile.Dev;

    public Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default)
    {
        context.Logger.LogInformation("[LivedIn] Synthetic data seeding (placeholder — no-op)");
        return Task.CompletedTask;
    }
}
