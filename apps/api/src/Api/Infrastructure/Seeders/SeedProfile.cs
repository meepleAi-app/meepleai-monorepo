namespace Api.Infrastructure.Seeders;

/// <summary>
/// Controls which seeding layers execute at startup.
/// Set via SEED_PROFILE environment variable (default: Dev).
/// </summary>
internal enum SeedProfile
{
    /// <summary>Core + Catalog(dev.yml). Auto-runs on startup.</summary>
    Dev,

    /// <summary>Core + Catalog(staging.yml) + LivedIn. Script-driven via dump/restore.</summary>
    Staging,

    /// <summary>Core + Catalog(prod.yml). Script-driven via dump/restore.</summary>
    Prod,

    /// <summary>Skip all seeding (used when restoring from dump).</summary>
    None
}
