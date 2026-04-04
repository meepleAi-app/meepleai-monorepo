namespace Api.Infrastructure.Seeders;

/// <summary>
/// A composable seed layer. Layers run in registration order.
/// Each layer is idempotent — safe to re-run.
/// </summary>
public interface ISeedLayer
{
    /// <summary>Display name for logging.</summary>
    string Name { get; }

    /// <summary>Minimum profile required to run this layer.</summary>
    SeedProfile MinimumProfile { get; }

    /// <summary>Execute seeding logic.</summary>
    Task SeedAsync(SeedContext context, CancellationToken cancellationToken = default);
}
