namespace Api.Services.Pdf;

/// <summary>
/// Feature flags driving the blob-layout migration outbox drainer (issue #1314).
///
/// Phase 4 cleanup (issue #1399, 2026-05-21): the 5-phase Legacy/Dual/New write
/// and read modes have all converged on the categorized "v2" layout. Only the
/// drainer toggle remains so the outbox table + background service stay
/// reusable for future migrations (SessionPhoto, GameImage, etc.).
/// </summary>
internal sealed class StorageLayoutOptions
{
    /// <summary>
    /// Configuration section name in appsettings.json / env-var binding.
    /// </summary>
    public const string SectionName = "StorageLayout";

    /// <summary>
    /// Enables the <c>StorageOperationOutboxBackgroundService</c> drainer.
    /// When false, the background service is registered but exits the loop
    /// after one tick — useful in dev / test environments. The outbox table
    /// + drainer infrastructure are kept for future blob-layout migrations
    /// (SessionPhoto, GameImage, etc.).
    /// </summary>
    public bool MigrationEnabled { get; init; }
}
