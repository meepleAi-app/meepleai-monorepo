namespace Api.Infrastructure.Seeders;

/// <summary>
/// Determines which seed layers run and with what data.
/// Set via SEED_PROFILE env var or Seeding:Profile in appsettings.
/// Ordinal values control layer filtering: a layer runs if MinimumProfile &lt;= active profile.
/// </summary>
public enum SeedProfile
{
    /// <summary>No seeding (CI/testing environments).</summary>
    None = 0,
    /// <summary>Production: Core layer only (admin + AI models).</summary>
    Prod = 1,
    /// <summary>Staging: Core + Catalog (shared games, badges, flags).</summary>
    Staging = 2,
    /// <summary>Development: Core + Catalog + LivedIn (synthetic data).</summary>
    Dev = 3
}
